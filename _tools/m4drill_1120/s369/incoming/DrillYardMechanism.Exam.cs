using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 20/11 (§368): TRẠM 3 — BURLAP'S FINAL EXAM, pha 1: THI TOÁN TRÊN CỬA SẬP (thay Sergeant Says §365 — người dùng chê thiếu VFX, chán).
    /// Burlap đứng bục giảng, kéo BẢNG ĐEN (PixelLab `props/drill_board`) ra, viết phép tính bằng phấn (chữ hiện từng ký tự + bụi phấn + tiếng phấn).
    /// Giữa sân 4 CỬA SẬP gỗ (`drill/fx_trapdoor`, 6 khung đóng -> mở), mỗi cửa sơn một đáp án.
    /// Hết vạch đếm ngược (6,5 -> 4,5 s):
    ///   • Đứng ĐÚNG cửa  -> cửa lóe xanh, "CORRECT!", dấu tích trên bảng; 3 cửa sai bật mở cho thấy hố.
    ///   • Đứng SAI cửa   -> cửa bật mở, Ren TỤT XUỐNG HỐ (thu nhỏ + xoay + bụi), mất 1 mạng, rồi bị lò xo rơm hất văng ra trước hàng cửa.
    ///   • Không đứng cửa nào -> Burlap gồng (vạch báo) rồi LAO vào húc (lăn né được) — mất 1 mạng nếu trúng.
    /// Cần 6 câu đúng (độ khó theo số câu đã đúng: cộng -> trừ -> nhân -> chia -> 2 phép tính). Sai không tính, ra câu mới cùng mức.
    /// Xong 6 câu -> Burlap đá văng bảng, gọi pháo cối: pha 2 LIVE FIRE (DrillYardMechanism.Artillery.cs).
    /// </summary>
    public sealed partial class DrillYardMechanism
    {
        private const int ExamQuestions = 6;
        // §369: mọi asset vẽ đúng mật độ điểm ảnh của gói (1 px = 1 px, PPU 100) — không phóng to
        private const float DoorScale = 1f, DoorHalf = 0.34f, BoardScale = 1f, DoorGap = 1.2f;

        private sealed class Door
        {
            public Vector2 At;
            public SpriteRenderer Sr, Num;
            public int Value;
            public bool Open;
            public float Frame, Jitter, PopAt = -9f, TintUntil;
            public Color Tint = Color.white;
        }

        private readonly List<Door> doors = new List<Door>();
        private Sprite[] doorFrames;
        private SpriteRenderer board, chalk, chalkMark;
        private Vector2 boardAt, lectern;
        private bool examShown, boardShown, falling;
        private float boardDrop, examShownAt;
        private int examRight;
        private Vector3 fallScale = Vector3.one;

        // ------------------------------------------------------------------ dựng
        private void InitExam()
        {
            doorFrames = SpriteVfx.Load("drill/fx_trapdoor");
            float y = Ctx.TilePoint(Str("doors_at", "12.5,132.4"), A.center).y;
            for (int i = 0; i < 4; i++)
            {
                var at = new Vector2(A.center.x + (i - 1.5f) * DoorGap, y);
                var d = new Door { At = at };
                d.Sr = TrapArt.MakeRenderer(Ctx.Host.transform, "Drill Trapdoor " + i, at, Ctx.Player, -8);
                d.Sr.transform.localScale = new Vector3(DoorScale, DoorScale, 1f);
                if (doorFrames.Length > 0) d.Sr.sprite = doorFrames[0];
                d.Sr.enabled = false;
                d.Num = TrapArt.MakeRenderer(Ctx.Host.transform, "Drill Trapdoor Number " + i, at, Ctx.Player, -7);
                d.Num.transform.localScale = new Vector3(DoorScale, DoorScale, 1f);
                FxLighting.MakeUnlit(d.Num);
                d.Num.enabled = false;
                doors.Add(d);
            }
            lectern = kingHome + Vector2.down * 0.55f;
            boardAt = new Vector2(kingHome.x + 1.45f, kingHome.y - 0.3f);
            board = Prop("Drill Chalkboard", boardAt, "VFX/props/drill_board", 0);
            board.transform.localScale = new Vector3(BoardScale, BoardScale, 1f);
            board.enabled = false;
            chalk = TrapArt.MakeRenderer(board.transform, "Drill Chalk", boardAt, Ctx.Player, 1);
            chalk.transform.localPosition = new Vector3(0f, 0.205f, 0f);                  // §369: tâm lòng bảng (20,5 px trên chân ảnh; lòng 70 x 21 px)
            chalk.transform.localScale = Vector3.one;
            FxLighting.MakeUnlit(chalk);
            chalkMark = TrapArt.MakeRenderer(board.transform, "Drill Chalk Mark", boardAt, Ctx.Player, 2);
            chalkMark.transform.localPosition = new Vector3(0.3f, 0.205f, 0f);
            FxLighting.MakeUnlit(chalkMark);
            InitArtillery();
        }

        /// <summary>Hiện / ẩn bảng + 4 cửa sập (khi bật: bảng rơi từ trên xuống, cửa trồi lên lần lượt).</summary>
        private void ShowExam(bool on)
        {
            examShown = on; examShownAt = Time.time;
            foreach (var d in doors)
            {
                d.Sr.enabled = on; d.Num.enabled = false; d.Open = false; d.Frame = 0f; d.Jitter = 0f; d.TintUntil = 0f;
                d.Sr.transform.position = d.At;
            }
            ShowBoard(on);
        }

        private void ShowBoard(bool on)
        {
            boardShown = on;
            if (board == null) return;
            board.enabled = on;
            board.transform.rotation = Quaternion.identity;
            board.color = Color.white;
            boardDrop = on ? 1f : 0f;
            board.transform.position = boardAt;
            SetChalk("");
            SetMark("", Color.white);
        }

        private void SetChalk(string s)
        {
            if (chalk == null) return;
            if (chalk.sprite != null) { Object.Destroy(chalk.sprite.texture); Object.Destroy(chalk.sprite); }
            chalk.sprite = string.IsNullOrEmpty(s) ? null : DrillChalk.Text(s, 2, new Color32(236, 240, 232, 235), new Color32(0, 0, 0, 0), true, s.Length);
        }

        private void SetMark(string s, Color c)
        {
            if (chalkMark == null) return;
            if (chalkMark.sprite != null) { Object.Destroy(chalkMark.sprite.texture); Object.Destroy(chalkMark.sprite); }
            chalkMark.sprite = string.IsNullOrEmpty(s) ? null : DrillChalk.Text(s, 3, new Color32(255, 255, 255, 240), new Color32(0, 0, 0, 0), true, 7);
            chalkMark.color = c;
        }

        /// <summary>Mỗi khung: cửa mở / đóng theo khung hình, rung khi sắp hết giờ, số nảy lên; bảng rơi xuống; phần pha 2.</summary>
        private void AnimateExam(float dt)
        {
            if (examShown)
            {
                float rise = Mathf.Clamp01((Time.time - examShownAt) / 0.5f);
                for (int i = 0; i < doors.Count; i++)
                {
                    var d = doors[i];
                    d.Frame = Mathf.MoveTowards(d.Frame, d.Open ? doorFrames.Length - 1 : 0f, dt * 22f);
                    if (doorFrames.Length > 0) d.Sr.sprite = doorFrames[Mathf.Clamp(Mathf.RoundToInt(d.Frame), 0, doorFrames.Length - 1)];
                    float r = Mathf.Clamp01(rise * 1.6f - i * 0.15f);
                    float s = DoorScale * Mathf.Lerp(0.2f, 1f, 1f - (1f - r) * (1f - r));
                    d.Sr.transform.localScale = new Vector3(s, s, 1f);
                    Vector2 j = d.Jitter > 0f ? new Vector2(Mathf.Sin(Time.time * 71f + i) * 0.012f, Mathf.Cos(Time.time * 83f + i) * 0.008f) * d.Jitter : Vector2.zero;
                    d.Sr.transform.position = d.At + j;
                    var tint = Time.time < d.TintUntil ? Color.Lerp(Color.white, d.Tint, 0.5f + 0.5f * Mathf.Sin(Time.time * 20f)) : Color.white;
                    d.Sr.color = tint;
                    // số: chỉ thấy khi cửa đóng; nảy lên khi vừa sơn
                    bool show = d.Num.sprite != null && d.Frame < 1.2f && r >= 1f;
                    d.Num.enabled = show;
                    if (show)
                    {
                        float p = Mathf.Clamp01((Time.time - d.PopAt) / 0.25f);
                        float ns = DoorScale * (p < 1f ? Mathf.Lerp(0.3f, 1.15f, p) : 1f);
                        d.Num.transform.localScale = new Vector3(ns, ns, 1f);
                        d.Num.transform.position = d.At + j + Vector2.down * 0.02f;
                        d.Num.color = Time.time < d.TintUntil ? d.Tint : Color.white;
                    }
                }
            }
            if (boardShown && board != null && boardDrop > 0f)
            {
                boardDrop = Mathf.MoveTowards(boardDrop, 0f, dt / 0.35f);
                board.transform.position = boardAt + Vector2.up * (boardDrop * boardDrop * 2.2f);
                if (boardDrop <= 0f)
                {
                    board.transform.position = boardAt;
                    Sfx.Play("heavy_land", boardAt, 0.7f);
                    SpriteVfx.Spawn("m3_dash_dust", boardAt, 1.3f, Layer, Order - 1, false, 16f, false);
                    CameraFollow.KickMain(Vector2.down, 0.05f, 0.2f);
                }
            }
            AnimateArtillery(dt);
        }

        private Door DoorOf(Vector2 feet)
        {
            foreach (var d in doors)
                if (Mathf.Abs(feet.x - d.At.x) <= DoorHalf && Mathf.Abs(feet.y - d.At.y) <= DoorHalf) return d;
            return null;
        }

        // ------------------------------------------------------------------ trạm 3
        private IEnumerator FinalExam()
        {
            examRight = 0;
            artilleryOn = false;
            DrillBoard.Goal = "CORRECT 0 / " + ExamQuestions; DrillBoard.GoalFrac = 0f;
            yield return ExamSetup();
            int q = 0;
            while (examRight < ExamQuestions && !aborted && !stationFailed)
            {
                yield return Question(q++);
                if (aborted || stationFailed) yield break;
                yield return Wait(0.8f);
            }
            if (aborted || stationFailed) yield break;
            yield return ExamToArtillery();
            if (aborted || stationFailed) yield break;
            yield return Artillery();
        }

        private IEnumerator ExamSetup()
        {
            yield return WalkKing(lectern, 1.6f);
            if (aborted || stationFailed) yield break;
            Bark("CLASS IS IN SESSION! *CLANK* Deploying... EDUCATIONAL APPARATUS!", 2.4f, true);
            Sfx.Play("drill_deploy", A.center, 0.8f);
            ShowExam(true);
            yield return Wait(0.9f);
            Sfx.Play("trap_pit_crack", A.center, 0.6f);
            Hints.Show("Stand on the trapdoor with the RIGHT ANSWER before the bar runs out. Wrong door = you drop. No door = Burlap charges you.", 7f);
            Bark("I write a sum. You STAND on the answer. Wrong door? You DROP. No answer? You get the BOOT. Six right answers, recruit!", 4.2f, true);
            yield return Wait(2.6f);
        }

        // ------------------------------------------------------------------ câu hỏi
        private struct Quiz { public string Board, Hud, Flavor; public int Answer; public int[] Options; }

        /// <summary>Đề theo mức (= số câu đã đúng): cộng, trừ, nhân, chia, a x b - c, a + b x c (bẫy thứ tự phép tính).</summary>
        private static Quiz MakeQuiz(int level)
        {
            int a, b, c, ans, swap;
            string op;
            switch (Mathf.Clamp(level, 0, 5))
            {
                case 0: a = Random.Range(3, 10); b = Random.Range(2, 10); ans = a + b; swap = a * b; op = a + "+" + b; break;
                case 1: a = Random.Range(11, 20); b = Random.Range(3, 10); ans = a - b; swap = a + b; op = a + "-" + b; break;
                case 2: a = Random.Range(3, 10); b = Random.Range(3, 10); ans = a * b; swap = a + b; op = a + "x" + b; break;
                case 3: b = Random.Range(2, 10); c = Random.Range(2, 10); a = b * c; ans = c; swap = a - b; op = a + "/" + b; break;
                case 4: a = Random.Range(2, 7); b = Random.Range(3, 7); c = Random.Range(1, 10); ans = a * b - c; swap = a * (b - c); op = a + "x" + b + "-" + c; break;
                default: a = Random.Range(2, 10); b = Random.Range(2, 6); c = Random.Range(2, 6); ans = a + b * c; swap = (a + b) * c; op = a + "+" + b + "x" + c; break;
            }
            var pool = new List<int> { swap, ans + 1, ans - 1, ans + 2, ans - 2, ans + 10, ans - 10, ans + 3 };
            var opts = new List<int> { ans };
            if (level >= 4 && swap != ans && swap >= 0 && swap <= 99) opts.Add(swap);               // bẫy thứ tự phép tính luôn có mặt
            while (opts.Count < 4 && pool.Count > 0)
            {
                int k = Random.Range(0, pool.Count), v = pool[k];
                pool.RemoveAt(k);
                if (v < 0 || v > 99 || opts.Contains(v)) continue;
                opts.Add(v);
            }
            for (int v = ans + 4; opts.Count < 4; v++) if (v <= 99 && !opts.Contains(v)) opts.Add(v);
            for (int i = opts.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (opts[i], opts[j]) = (opts[j], opts[i]); }
            string[] flavor =
            {
                "SUPPLY COUNT!", "CASUALTY REPORT!", "RATION MATH!", "SPLIT THE PLATOON!", "LOGISTICS!", "ORDER OF OPERATIONS, RECRUIT!",
            };
            return new Quiz
            {
                Board = op + "=?",
                Hud = op.Replace("x", " x ").Replace("+", " + ").Replace("-", " - ").Replace("/", " / ") + " = ?",
                Flavor = flavor[Mathf.Clamp(level, 0, 5)],
                Answer = ans,
                Options = opts.ToArray(),
            };
        }

        private IEnumerator Question(int n)
        {
            var quiz = MakeQuiz(examRight);
            // 1) quay ra bảng, viết phấn từng ký tự
            king.Sr.flipX = boardAt.x < king.Pos.x;
            king.Play("idle", 10f);
            SetMark("", Color.white);
            Bark(quiz.Flavor, 1.4f, true);
            Sfx.Play("drill_chalk", boardAt, 0.85f);
            for (int i = 1; i <= quiz.Board.Length && !aborted && !stationFailed; i++)
            {
                SetChalk(quiz.Board.Substring(0, i));
                if (i % 2 == 1)
                {
                    float w = quiz.Board.Length * 8f * BoardScale / 100f;
                    var at = boardAt + new Vector2(-w * 0.5f + w * i / quiz.Board.Length, 0.24f);
                    var dust = SpriteVfx.Spawn("drill/fx_snuff", at, 0.45f, Layer, Order + 3, false, 16f, false);
                    if (dust != null) dust.Tint(new Color(1f, 1f, 1f, 0.8f));
                }
                king.Pos = lectern + new Vector2(0.03f * Mathf.Sin(i * 2.1f), 0f);
                yield return Wait(0.09f);
            }
            king.Pos = lectern;
            if (aborted || stationFailed) yield break;
            king.Sr.flipX = PlayerFeet.x < king.Pos.x;
            // 2) sơn đáp án lên cửa
            for (int i = 0; i < doors.Count; i++)
            {
                doors[i].Value = quiz.Options[i];
                doors[i].Num.sprite = DrillChalk.Number(quiz.Options[i]);
                doors[i].PopAt = Time.time + i * 0.08f;
                doors[i].Open = false;
            }
            Sfx.Play("ui_confirm", A.center, 0.4f);
            float window = Mathf.Lerp(6.5f, 4.5f, examRight / (float)(ExamQuestions - 1));
            Bark("QUESTION " + (n + 1) + "! " + quiz.Hud + " STAND ON THE ANSWER!", window, true);
            DrillBoard.Command = quiz.Hud; DrillBoard.CommandTime = Time.time;
            // 3) đếm ngược: trống mỗi giây, 1,5 s cuối cửa rung
            float t0 = Time.time, nextTick = 0f;
            bool rattled = false;
            while (Time.time - t0 < window && !aborted && !stationFailed)
            {
                float e = Time.time - t0;
                DrillBoard.TrackFrac = 1f - e / window;
                if (e >= nextTick) { nextTick += 1f; Sfx.Play("footstep", king.Pos, 0.55f); }
                if (window - e < 1.5f)
                {
                    foreach (var d in doors) d.Jitter = 1f;
                    if (!rattled) { rattled = true; Sfx.Play("trap_pit_crack", A.center, 0.5f); Bark(Pick("TICK. TOCK.", "PENCILS DOWN!", "Three... two..."), 1.2f, true); }
                }
                yield return null;
            }
            foreach (var d in doors) d.Jitter = 0f;
            DrillBoard.TrackFrac = 0f;
            DrillBoard.Command = "";
            if (aborted || stationFailed) yield break;
            // 4) chấm bài
            var stand = DoorOf(PlayerFeet);
            Door right = null;
            foreach (var d in doors) if (d.Value == quiz.Answer) right = d;
            if (right != null) { right.Tint = new Color(0.45f, 1f, 0.5f); right.TintUntil = Time.time + 1.4f; }
            Sfx.Play("drill_trapdoor", A.center, 0.8f);
            foreach (var d in doors) if (d != right) d.Open = true;                       // mọi cửa SAI bật mở cho thấy hố
            if (stand != null && stand == right)
            {
                examRight++;
                DrillBoard.Goal = "CORRECT " + examRight + " / " + ExamQuestions; DrillBoard.GoalFrac = examRight / (float)ExamQuestions;
                DrillBoard.Say("CORRECT!", Green);
                Sfx.Play("drill_correct", PlayerFeet, 0.9f);
                SetMark("V", new Color(0.55f, 1f, 0.6f));
                SpriteVfx.Spawn("duo_light_orb", right.At, 0.8f, Layer, Order + 2, false, 18f, false);
                king.Flash(new Color(1f, 0.95f, 0.6f), 0.3f);
                Bark(examRight >= ExamQuestions ? "...SIX. Six correct answers. *whirr* ...*whirr* ...*KRRK*"
                     : Pick("*STAMP* CORRECT. ...Lucky guess.", "Correct! Did you CHEAT? Show me your hands!", "ACCEPTABLE ARITHMETIC!",
                            "Hmph. The trapdoor is disappointed in you. It wanted a snack."), 2.2f, true);
            }
            else if (stand != null)
            {
                Sfx.Play("drill_wrong", PlayerFeet, 0.9f);
                SetMark("X", new Color(1f, 0.45f, 0.4f));
                Bark(Pick("WRONG! It was " + quiz.Answer + "! DOWN YOU GO!", "INCORRECT! The trapdoor has OPINIONS!", quiz.Answer + "! The answer was " + quiz.Answer + "! ENJOY THE BASEMENT!"), 2.4f, true);
                yield return FallThrough(stand);
            }
            else
            {
                Sfx.Play("drill_wrong", king.Pos, 0.7f);
                SetMark("X", new Color(1f, 0.45f, 0.4f));
                Bark(Pick("NO ANSWER?! Then you get the BOOT!", "SILENCE IS NOT AN ANSWER! *CHARGE*", "Skipping class? NOT IN MY YARD!"), 1.8f, true);
                yield return Wait(0.5f);
                if (!aborted && !stationFailed) yield return Boot();
            }
            yield return Wait(0.8f);
            foreach (var d in doors) { d.Open = false; d.Num.sprite = null; }
            if (doors.Count > 0) Sfx.Play("door_wood", A.center, 0.4f);
        }

        // ------------------------------------------------------------------ rơi xuống hố
        private void PlacePlayer(Vector2 at)
        {
            var p = Ctx.Player;
            if (p == null) return;
            var rb = p.GetComponent<Rigidbody2D>();
            if (rb != null) { rb.position = at; rb.linearVelocity = Vector2.zero; }
            p.position = at;
        }

        /// <summary>Ren tụt xuống cửa sập: bị hút vào giữa hố, thu nhỏ + xoay + bụi; mất 1 mạng; rồi bị lò xo rơm hất ra trước hàng cửa.</summary>
        private IEnumerator FallThrough(Door d)
        {
            var p = Ctx.Player;
            if (p == null) yield break;
            falling = true;
            fallScale = p.localScale;
            GameInput.Blocked = true;
            Vector2 from = p.position, hole = d.At + Vector2.down * 0.06f;
            Sfx.Play("drill_fall", hole, 0.9f);
            for (float t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                float u = t / 0.5f;
                PlacePlayer(Vector2.Lerp(from, hole, Mathf.Min(1f, u * 2.5f)));
                float k = Mathf.Max(0.02f, 1f - u * u);
                p.localScale = new Vector3(fallScale.x * k, fallScale.y * k, fallScale.z);
                p.rotation = Quaternion.Euler(0f, 0f, u * 240f);
                yield return null;
            }
            p.localScale = new Vector3(fallScale.x * 0.02f, fallScale.y * 0.02f, fallScale.z);
            SpriteVfx.Spawn("m3_dash_dust", hole, 1.2f, Layer, Order - 1, false, 16f, false);
            Sfx.Play("body_fall", hole, 0.7f);
            CameraFollow.KickMain(Vector2.down, 0.07f, 0.25f);
            LoseLife(6f, "DROPPED!  MORALE -1");
            yield return new WaitForSeconds(0.6f);                                       // không dùng Wait(): luôn phải trả Ren về
            if (Ctx.PlayerHealth != null && Ctx.PlayerHealth.IsDead) { RestorePlayer(); yield break; }
            // lò xo rơm: bật ra trước hàng cửa
            Vector2 outAt = new Vector2(d.At.x, d.At.y - 1.0f);
            Sfx.Play("spring_boing", outAt, 0.9f);
            SpriteVfx.Spawn("drill/fx_hayburst", outAt + Vector2.up * 0.15f, 0.8f, Layer, Order + 2, false, 18f, false);
            for (float t = 0f; t < 0.35f; t += Time.deltaTime)
            {
                float u = t / 0.35f;
                float k = u < 0.6f ? Mathf.Lerp(0.2f, 1.25f, u / 0.6f) : Mathf.Lerp(1.25f, 1f, (u - 0.6f) / 0.4f);
                p.localScale = new Vector3(fallScale.x * k, fallScale.y * k, fallScale.z);
                p.rotation = Quaternion.Euler(0f, 0f, (1f - u) * 360f);
                PlacePlayer(outAt + Vector2.up * Mathf.Sin(u * Mathf.PI) * 0.45f);
                yield return null;
            }
            PlacePlayer(outAt);
            RestorePlayer();
            if (!stationFailed) Bark(Pick("*BOING* Returned to sender!", "Back so soon? The basement didn't want you either!", "Spring-loaded! Like my TEMPER!"), 2f, true);
        }

        private void RestorePlayer()
        {
            if (!falling) return;
            falling = false;
            var p = Ctx.Player;
            if (p != null) { p.localScale = fallScale; p.rotation = Quaternion.identity; }
            GameInput.Blocked = false;
        }

        /// <summary>Mất 1 mạng không qua va chạm (rơi hố, đạn rơm của chính mình).</summary>
        private void LoseLife(float damage, string pop)
        {
            hitsTotal++; stationHits++;
            if (Ctx.PlayerHealth != null && !Ctx.PlayerHealth.IsDead) TrapArt.HurtPlayerCrush(Ctx, damage, Vector2.down, 0f);
            DrillBoard.LoseMorale();
            lives = DrillBoard.Morale;
            DrillBoard.Say(pop, Red);
            if (DrillBoard.Morale <= 0) FailStation(Pick("OUT OF MORALE!", "DOWN AND OUT!", "EXPELLED!"));
        }

        // ------------------------------------------------------------------ Burlap đi / lao (dùng cả 2 pha)
        private IEnumerator WalkKing(Vector2 to, float speed)
        {
            if ((king.Pos - to).sqrMagnitude < 0.0004f) yield break;
            king.Play("move", 12f);
            while ((king.Pos - to).sqrMagnitude > 0.0004f && !aborted && !stationFailed)
            {
                king.Sr.flipX = to.x < king.Pos.x;
                king.Pos = Vector2.MoveTowards(king.Pos, to, speed * Time.deltaTime);
                yield return null;
            }
            king.Pos = to;
            king.Play("idle", 4f);
        }

        /// <summary>Không trả lời: Burlap gồng (vạch báo 0,85 s) rồi lao tới chỗ Ren đứng (+0,7) — trúng mất 1 mạng, lăn né được; rồi về bục.</summary>
        private IEnumerator Boot()
        {
            Vector2 from = king.Pos, target = PlayerFeet;
            Vector2 dir = (target - from).sqrMagnitude > 0.01f ? (target - from).normalized : Vector2.down;
            float want = (target - from).magnitude + 0.7f, dist = 0f;
            while (dist < want && Inside(from + dir * (dist + 0.05f))) dist += 0.05f;
            Vector2 stop = from + dir * dist;
            king.Sr.flipX = dir.x < 0f;
            DrillFx.Warn(Ctx.Host, from + dir * 0.35f, stop, 0.85f, dir, Ctx.Player, 0.34f);
            for (float t = 0f; t < 0.85f && !aborted && !stationFailed; t += Time.deltaTime) { king.Pos = from + Random.insideUnitCircle * 0.02f; yield return null; }
            king.Pos = from;
            if (aborted || stationFailed) yield break;
            king.Harmless = false; king.Credited = false;
            king.Play("dash", 18f);
            Sfx.Play("ram_charge", king.Pos);
            float dur = Mathf.Max(0.05f, dist / 7f), dustAt = 0f;
            for (float t = 0f; t < dur && !aborted && !stationFailed; t += Time.deltaTime)
            {
                king.Pos = Vector2.Lerp(from, stop, t / dur);
                king.Sr.transform.rotation = Quaternion.Euler(0f, 0f, -8f * Mathf.Sign(dir.x));
                if (t >= dustAt) { dustAt = t + 0.07f; SpriteVfx.Spawn("m3_dash_dust", king.Pos, 1.1f, Layer, king.Sr.sortingOrder - 1, dir.x < 0f, 18f, false); }
                yield return null;
            }
            king.Sr.transform.rotation = Quaternion.identity;
            king.Harmless = true;
            if (aborted || stationFailed) yield break;
            king.Play("dashend", 14f, false);
            Sfx.Play("heavy_land", king.Pos, 0.6f);
            yield return Wait(0.6f);
            yield return WalkKing(lectern, 2.2f);
        }

        /// <summary>Điểm còn trong sân (chừa mép tường).</summary>
        private bool Inside(Vector2 q) => q.x > A.xMin + 0.3f && q.x < A.xMax - 0.3f && q.y > A.yMin + 0.15f && q.y < A.yMax - 0.45f;

        // ------------------------------------------------------------------ dọn
        private void ResetKing()
        {
            RestorePlayer();
            ShowExam(false);
            ResetArtillery();
            DrillBoard.Command = "";
            if (king == null) return;
            king.Pos = kingHome; king.Vel = Vector2.zero; king.Harmless = true; king.Play("idle", 4f); king.SetAlpha(1f); king.Lift = 0f;
            if (king.Sr != null) king.Sr.transform.rotation = Quaternion.identity;
        }

        private void ExamGuide()
        {
            if (artilleryOn) { ArtilleryGuide(); return; }
            DrillBoard.Objective = "Read the sum on Burlap's chalkboard. Stand on the trapdoor with the right answer before the bar runs out!";
            Guide(null);
            QuestGuide.Set(QuestTitle + " - Final Exam", "Answer the sums (" + examRight + "/" + ExamQuestions + ")", null, examRight, ExamQuestions);
        }
    }
}
