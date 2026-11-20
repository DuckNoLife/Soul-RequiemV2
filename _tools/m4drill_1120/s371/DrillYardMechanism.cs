using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 20/11 (§356) → viết lại (§360): drill_yard — BURLAP'S PROVING GROUNDS (The Drill Yard, Map 4). "Trận boss" KHÔNG KIẾM do
    /// TRUNG SĨ BURLAP (người nộm đội vương miện, MEGAPACK Dummy_LVL4) chỉ huy — nghiêm như quân đội, nhưng nhí nhảnh.
    /// Vùng object = SÂN (rời sân giữa chừng = "DESERTER!", huỷ lượt). Bắt đầu / chơi lại bằng tù và (drill_horn).
    ///
    /// 3 TRẠM, mỗi trạm một ĐỘNG TỪ mới (không lặp một kiểu né 4 lần như §356):
    ///   1 BLADE DRILL  (né)        — người nộm XOAY (rơm quay lưỡi, cọc quay áo choàng, bao cát lăn) + vòng gió xoáy dưới chân;
    ///                                 mọi đợt có VẠCH MŨI TÊN ĐỎ báo trước ~0,9 s (khe an toàn = mũi tên xanh nhỏ). Sống sót 45 s.
    ///   2 LANTERN RUN  (mang vác)  — nhặt đèn lồng ở bệ (`lantern_at`), mang lên giá đèn (`rack_at`) qua 6 làn người nộm xoay chạy
    ///                                 ngược chiều (kiểu Frogger). Cầm đèn = KHÔNG LĂN ĐƯỢC. Chạm người nộm = đèn tắt, về bệ thắp lại. 3 đèn / 75 s.
    ///   3 FINAL EXAM   (§368)      — pha 1 THI TOÁN trên 4 cửa sập (sai = tụt hố, không trả lời = Burlap húc) -> pha 2 LIVE FIRE:
    ///                                 dụ Burlap lao cắm đầu vào đống rơm / tường rồi bắn PHÁO CỐI rơm (đạn bám theo hắn). Trúng 3 lần.
    ///                                 (Exam.cs + Artillery.cs; Sergeant Says §365 và máy phóng §360 đã bỏ.)
    /// §371 (thay mạng chung §368 — quá khó): mỗi trạm 3 MÀN, tinh thần 5 ô đầy lại đầu mỗi màn; thua màn nào (hết tinh thần / hết giờ / gục)
    /// thì thổi kèn chơi lại đúng màn đó. Giữa các màn tự sang sau 3 s; giữa các TRẠM vẫn phải thổi kèn. Xong cả 3 -> cờ `sets` (drill_passed), chùm chìa hiện
    /// trên bệ (`key_at`). Hạng theo tổng số lần dính đòn: S ≤ 1, A ≤ 4, còn lại B.
    /// Máy phóng: "x,y,hướng,tín_hiệu" (ô Tiled của chân máy; hướng 1 = bắn sang đông, -1 = sang tây). Máy luôn bắn được (cả khi chưa chơi).
    /// HUD đọc DrillBoard.
    /// </summary>
    public sealed partial class DrillYardMechanism : Mechanism
    {
        private enum Phase { Idle, Running, Done }
        private Phase phase = Phase.Idle;
        private readonly List<DrillActor> actors = new List<DrillActor>();
        private readonly List<Coroutine> routines = new List<Coroutine>();
        private DrillActor king;
        private Vector2 kingHome, keyAt;
        private SpriteRenderer keyring;
        private Coroutine main;
        private int checkpoint, station, hitsTotal, stationHits;
        /// <summary>Tinh thần còn lại của màn đang chơi (§371: đầy lại đầu mỗi màn).</summary>
        private int lives = -1;
        private bool passed, aborted, stationFailed, wasInside;
        private float hitCooldown, nextLine, idleGreetAt, readyAt, nextDodgeSay;
        private PlayerCombat combat;

        private static readonly string[] Names = { "BLADE DRILL", "LANTERN RUN", "FINAL EXAM" };
        private static readonly Color Gold = new Color(1f, 0.85f, 0.45f), Red = new Color(1f, 0.38f, 0.32f), Green = new Color(0.55f, 1f, 0.6f), Sky = new Color(0.6f, 0.85f, 1f);

        public bool Passed => passed;
        public bool Busy => phase == Phase.Running;
        public bool Ready => phase == Phase.Idle && !passed && !inScene && Time.time >= readyAt;
        /// <summary>Trạm sẽ chơi khi thổi tù và (0-based) — tù và nói "Resume".</summary>
        public int Checkpoint => checkpoint;
        private Rect A => Rect;
        private Vector2 PlayerFeet => Ctx.Player != null ? (Vector2)Ctx.Player.position : A.center;
        private int MoraleMax => Mathf.Max(1, Int("morale", 5));

        public override void Init()
        {
            DrillBoard.Active = false;
            kingHome = Ctx.TilePoint(Str("king_at", ""), new Vector2(A.center.x, A.yMax - 0.55f));
            keyAt = Ctx.TilePoint(Str("key_at", ""), new Vector2(A.center.x, A.yMax - 0.2f));
            king = new DrillActor("king", kingHome, Ctx.Host.transform, Ctx.Player) { Harmless = true, Radius = 0.34f, FaceByVel = false };
            king.Tick(0f);
            keyring = TrapArt.MakeRenderer(Ctx.Host.transform, "Drill Keyring", keyAt, Ctx.Player, 1);
            keyring.sprite = WardenStatueMechanism.Load("VFX/props/drill_keyring");
            keyring.enabled = false;
            combat = Ctx.Player != null ? Ctx.Player.GetComponent<PlayerCombat>() : null;
            InitLantern();
            InitExam();                                                                // §368: cửa sập + bảng đen + pháo cối của Final Exam
            OnFlagsChanged();
        }

        public override void OnFlagsChanged()
        {
            string sets = Str("sets", "drill_passed");
            if (!passed && Ctx.Flags.HasAll(sets)) { passed = true; phase = Phase.Done; }
            if (keyring != null) keyring.enabled = passed && !Ctx.Flags.HasAll("key_" + Str("key_id", "drill_key"));
        }

        // ------------------------------------------------------------------ bắt đầu (tù và gọi)
        public void Begin()
        {
            if (!Ready) return;
            phase = Phase.Running; aborted = false;
            allowLeave = false;
            Seal(true, false);                                                         // §365: thổi kèn = sân khoá lại
            main = Ctx.Run(Drill());
        }

        /// <summary>§365: MỖI LẦN THỔI KÈN = MỘT TRẠM. §371: mỗi trạm 3 MÀN (ROUND), tự sang màn kế sau 3 s nghỉ; tinh thần đầy lại đầu mỗi màn;
        /// thua màn nào (hết tinh thần / hết giờ / gục) -> thổi kèn chơi lại ĐÚNG màn đó (checkpoint + roundCheckpoint).</summary>
        private IEnumerator Drill()
        {
            int s = checkpoint;
            if (s == 0 && roundCheckpoint == 0) hitsTotal = 0;
            yield return Wait(1.1f);                                                   // §363: để tiếng kèn hiệu vang trước khi vào trạm
            if (aborted) yield break;
            stationFailed = false; stationHits = 0; station = s;
            // §361: lần đầu vào trạm 3 -> cảnh quân sự "Trung sĩ ra trận" (bảng ẩn trong lúc diễn)
            bool scene = s == 2 && !Ctx.Flags.HasAll(SergeantFlag);
            if (scene) { DrillBoard.Active = false; yield return SergeantScene(); if (aborted) yield break; }
            DrillZoom(true);                                                           // §365: camera lùi xa thấy cả sân (§371: không kẹp biên — Ren luôn giữa màn hình)
            BeginRound(s, roundCheckpoint);
            DrillBoard.Goal = "GET READY";
            Sfx.Play("rf_war_drum", A.center);
            Bark(scene ? "PROGRAM THREE: THE FINAL EXAM. PENCILS... UP!" : roundCheckpoint > 0 ? RetryLine(s, roundCheckpoint) : StationLine(s), scene ? 2f : 4.6f, true);
            yield return Wait(scene ? 1.4f : 3.2f);
            if (aborted) yield break;
            for (int r = roundCheckpoint; r < Rounds; r++)
            {
                if (r > roundCheckpoint) BeginRound(s, r);
                stationFailed = false;
                DrillBoard.Finish("ROUND " + (r + 1) + " / " + Rounds, Gold);
                Sfx.Play("rf_war_drum", A.center, 0.8f);
                yield return Wait(1.2f);
                if (aborted) yield break;
                switch (s)
                {
                    case 0: yield return Blades(r); break;
                    case 1: yield return Lanterns(r); break;
                    default: yield return FinalExam(r); break;
                }
                StopRoutines();
                if (aborted) yield break;
                if (stationFailed) { roundCheckpoint = r; Fail(s, r); yield break; }
                if (r >= Rounds - 1) break;
                // màn xong: dọn sân, nghỉ 3 s rồi tự sang màn kế
                roundCheckpoint = r + 1;
                foreach (var a in actors) Ctx.Run(Vanish(a));
                DrillBoard.Command = ""; DrillBoard.TrackFrac = -1f; DrillBoard.TimeFrac = -1f; DrillBoard.TimeText = "";
                DrillBoard.Finish("ROUND " + (r + 1) + " CLEAR!", Green);
                Sfx.Play("puzzle_solved", A.center, 0.55f);
                Bark(RoundLine(s, r), 2.8f, true);
                for (float t = 3f; t > 0f && !aborted; t -= Time.deltaTime)
                {
                    DrillBoard.Goal = "NEXT ROUND IN " + Mathf.CeilToInt(t);
                    yield return null;
                }
                if (aborted) yield break;
            }
            roundCheckpoint = 0;
            lives = DrillBoard.Morale;
            ClearField();
            checkpoint = s + 1;
            if (s >= 2) { Pass(); yield break; }
            phase = Phase.Idle; readyAt = Time.time + 1.5f;
            Sfx.Play("puzzle_solved", A.center, 0.7f);
            DrillBoard.Finish("STATION " + (s + 1) + " CLEARED", Green);
            Bark((stationHits == 0 ? Pick("NOT ONE SCRATCH?! *WHIRR* RECALIBRATING EXPECTATIONS.", "Flawless! I'm writing this down! In PENCIL, so I can erase it if you get cocky!")
                                   : Pick("Passable! PASSABLE!", "You live! Barely! That's the regulation amount!"))
                 + " Sound the HORN when you're ready for STATION " + (s + 2) + ".", 4.4f, true);
            Ctx.Run(HideBoard(4f));
        }

        private const int Rounds = 3;
        /// <summary>§371: màn sẽ chơi khi thổi kèn (0-based) trong trạm `checkpoint`.</summary>
        private int roundCheckpoint;
        public int RoundCheckpoint => roundCheckpoint;

        /// <summary>§371: bảng HUD cho một màn — tiêu đề "BLADE DRILL - ROUND 2/3", tinh thần đầy lại.</summary>
        private void BeginRound(int s, int r)
        {
            DrillBoard.Begin(s + 1, Names[s] + "  -  ROUND " + (r + 1) + "/" + Rounds, MoraleMax);
            lives = MoraleMax;
            stationHits = 0;
        }

        private static string RoundLine(int s, int r)
        {
            switch (s)
            {
                case 0: return r == 0 ? "Round ONE survived! Next: the SACKS roll and my HUNTERS hunt!" : "Round TWO! Last round - THE MILL! Round and round!";
                case 1: return r == 0 ? "One lamp hung! Two to go - my boys just got FASTER!" : "Two lamps! One more! Mind the hems, they're FRESHLY STARCHED!";
                default: return r == 0 ? "Basic arithmetic: PASSED. Now the HARD sums. Division! Order of operations! FEAR!" : "All sums CORRECT?! *KRRK* ...Then we move on to the PRACTICAL.";
            }
        }

        private static string RetryLine(int s, int r)
        {
            return "Back for ROUND " + (r + 1) + " of STATION " + (s + 1) + "! Full morale, recruit - don't waste it!";
        }

        private static string StationLine(int s)
        {
            switch (s)
            {
                case 0: return "STATION ONE! BLADE DRILL! My boys SPIN, recruit! RED ARROWS mean INCOMING! Find the GAP or eat the BURLAP!";
                case 1: return "STATION TWO! LANTERN RUN! Carry a lamp to the rack - THREE lamps! NO ROLLING with a lit lamp! Touch a soldier and your light goes OUT!";
                default: return "STATION THREE! THE FINAL EXAM! Stand on the RIGHT ANSWER! Wrong door - you DROP! *CLICK* Sharpening chalk...";
            }
        }

        private void Start(IEnumerator r) => routines.Add(Ctx.Run(r));

        private void StopRoutines()
        {
            foreach (var c in routines) if (c != null) Ctx.Host.StopCoroutine(c);
            routines.Clear();
        }

        private IEnumerator Wait(float s)
        {
            float end = Time.time + s;
            while (Time.time < end && !aborted && !stationFailed) yield return null;
        }

        // ------------------------------------------------------------------ kết thúc
        private void Pass()
        {
            string rank = hitsTotal <= 1 ? "S" : hitsTotal <= 4 ? "A" : "B";
            ClearField();
            passed = true; phase = Phase.Done; checkpoint = 0; roundCheckpoint = 0;
            Seal(false, false);                                                        // §365: qua hết -> mở sân
            DrillBoard.Finish("RANK " + rank + " - PASSED", Gold);
            Sfx.Play("puzzle_solved", A.center);
            Bark(rank == "S" ? "...RANK S?! Nobody gets S. I don't even HAVE an S stamp. Take the key. Take my HAT. No - just the key."
                 : rank == "A" ? "Rank A. ...Fine soldiering, recruit. I am NOT crying. That's hay. The key is on the pedestal."
                 : "Rank B. Bruised! Battered! ...Adequate. The key is yours. Now get out of my yard before I get sentimental.", 5.5f, true);
            Ctx.Flags.Set(Str("sets", "drill_passed"));
            OnFlagsChanged();
            Ctx.Run(HideBoard(5f));
        }

        /// <summary>§371: thua một màn -> thổi kèn chơi lại đúng màn đó (không về trạm 1 nữa).</summary>
        private void Fail(int s, int r)
        {
            ClearField();
            phase = Phase.Idle; readyAt = Time.time + 2f;
            DrillBoard.Finish("ROUND " + (r + 1) + " FAILED", Red);
            Sfx.Play("ui_back", A.center);
            allowLeave = true; Seal(false, false);                                     // §365: trượt -> cho ra ngoài hồi sức
            Bark(failWhy + " *BZZT* Sound the horn when you're ready - we retry ROUND " + (r + 1) + " of STATION " + (s + 1) + ". Full morale!", 5f, true);
            Ctx.Run(HideBoard(4.5f));
        }

        private string failWhy = "PATHETIC!";

        private IEnumerator HideBoard(float s)
        {
            yield return new WaitForSeconds(s);
            if (phase != Phase.Running) DrillBoard.Active = false;
        }

        private void Abort(string why)
        {
            if (phase != Phase.Running) return;
            aborted = true;
            if (main != null) Ctx.Host.StopCoroutine(main);
            StopRoutines();
            ClearField();
            phase = Phase.Idle; readyAt = Time.time + 2f;
            allowLeave = true; Seal(false, false);                                     // §365: gục / bỏ dở -> mở sân (hồi sinh ở ngoài vẫn vào lại được)
            // §371: gục / bỏ dở -> giữ checkpoint: thổi kèn chơi lại đúng màn đang dở
            DrillBoard.Finish("DRILL CANCELLED", Red);
            if (!string.IsNullOrEmpty(why)) Bark(why, 4f, true);
            Ctx.Run(HideBoard(3f));
        }

        private void ClearField()
        {
            foreach (var a in actors) a.Destroy();
            actors.Clear();
            ResetLantern();
            ResetKing();
            QuestGuide.Clear();
            Guide(null);
            DrillBoard.Objective = "";
            DrillZoom(false);
        }

        // ------------------------------------------------------------------ §365: sân khoá (cỗ máy nhốt mọi người vào sân tới khi qua bài kiểm tra)
        private bool sealedIn, allowLeave;

        /// <summary>Đóng / mở cổng `seal_gate` (mặc định tally_gate — cổng vào sân). announce = Burlap hô + rung.</summary>
        private void Seal(bool on, bool announce)
        {
            if (sealedIn == on || Ctx.Director == null) return;
            if (!Ctx.Director.SetGateClosed(Str("seal_gate", "tally_gate"), on)) return;
            sealedIn = on;
            if (!on || !announce) return;
            CameraFollow.KickMain(Vector2.right, 0.06f, 0.25f);
            Bark(Pick("*CLICK* SUBJECT RE-ENTERED THE YARD. YARD: SEALED. ENLISTMENT: STILL MANDATORY.", "*WHIRR* WELCOME BACK, RECRUIT. THE DOOR IS LOCKED. FOR MORALE."), 3.4f, true);
        }

        /// <summary>Camera lùi xa 1,45 lần trong lúc chơi trạm (thấy vạch báo / đội hình từ xa).</summary>
        private void DrillZoom(bool on)
        {
            var cam = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            if (cam == null) return;
            if (on) cam.SetZoom(1.45f); else cam.ClearZoom();
            cam.SetFreeFollow(on);                                                     // §371: không kẹp biên map -> Ren luôn ở giữa màn hình
        }

        /// <summary>Trượt trạm (hết tinh thần / hết giờ) — vòng lặp của trạm tự thoát.</summary>
        private void FailStation(string why)
        {
            if (stationFailed) return;
            failWhy = why;
            stationFailed = true;
        }

        // ------------------------------------------------------------------ mỗi khung
        public override void Animate()
        {
            float dt = Time.deltaTime;
            if (king != null) king.Tick(dt);
            for (int i = actors.Count - 1; i >= 0; i--)
            {
                var a = actors[i];
                // mờ dần khi ra / vào mép sân
                float m = Mathf.Min(Mathf.Min(a.Pos.x - A.xMin, A.xMax - a.Pos.x), Mathf.Min(a.Pos.y - A.yMin + 0.2f, A.yMax - a.Pos.y));
                a.EdgeAlpha = Mathf.Clamp01((m + 0.35f) / 0.45f);
                a.Tick(dt);
            }
            AnimateLantern(dt);
            AnimateExam(dt);                                                           // §368: cửa sập / bảng / pháo cối
            UpdateGuide();                                                             // §362: chỉ dẫn / nhiệm vụ
            bool inside = A.Contains(PlayerFeet);
            if (phase == Phase.Running)
            {
                if (Ctx.PlayerHealth == null || Ctx.PlayerHealth.IsDead) { Abort(null); return; }
                if (!inside) { Abort("DESERTER! Get back here, recruit! ...Fine. Drill CANCELLED. I'm not hurt. My stuffing is just... loose."); return; }
                Collide();
                SpinAudio();                                                           // §364: tiếng người nộm quay
                for (int i = actors.Count - 1; i >= 0; i--)
                {
                    var a = actors[i];
                    if (a.Pos.x < A.xMin - 1.8f || a.Pos.x > A.xMax + 1.8f || a.Pos.y < A.yMin - 1.8f || a.Pos.y > A.yMax + 1.8f) { a.Destroy(); actors.RemoveAt(i); }
                }
            }
            else if (inside && !wasInside && !passed && !inScene && !Ctx.Flags.HasAll(MusterFlag))
                Ctx.Run(Muster());                                                   // §361: lần đầu vào sân -> duyệt binh
            else if (inside && !passed && !inScene && !sealedIn && !allowLeave && Ctx.Flags.HasAll(MusterFlag) && PlayerFeet.x < A.xMax - 0.35f)
                Seal(true, true);                                                    // §365: vào lại sân khi chưa qua -> khoá
            else if (inside && !wasInside && !inScene && Time.time >= idleGreetAt)
            {
                idleGreetAt = Time.time + 25f;
                Bark(passed ? "You again? You PASSED. Go. Shoo. Be free. Write home."
                     : checkpoint > 0 || roundCheckpoint > 0 ? "Back for more? Sound the HORN - we pick up at STATION " + (checkpoint + 1) + ", ROUND " + (roundCheckpoint + 1) + "!"
                     : "RECRUIT! Fall in! Sound the HORN when you're ready to be INSPECTED!", 3.5f, true);
            }
            if (!inside && allowLeave && phase != Phase.Running) allowLeave = false;   // §365: đã ra ngoài -> lần vào sau lại khoá
            wasInside = inside;
        }

        // ------------------------------------------------------------------ §364: âm thanh người nộm quay
        private readonly object spinKeyA = new object(), spinKeyB = new object();
        private float nextWhoosh;

        /// <summary>2 người nộm đang quay gần Ren nhất -> tiếng quay lặp bám theo (Sfx tự nhỏ dần theo khoảng cách);
        /// con nào vụt qua sát người (&lt; 0,7 đv) -> tiếng "vút".</summary>
        private void SpinAudio()
        {
            DrillActor a1 = null, a2 = null;
            float d1 = float.MaxValue, d2 = float.MaxValue;
            Vector2 p = PlayerFeet;
            foreach (var a in actors)
            {
                if (a.Harmless || !a.Spinning || a.EdgeAlpha < 0.3f || a.Sr == null) continue;
                float d = (a.Pos - p).sqrMagnitude;
                if (d < d1) { d2 = d1; a2 = a1; d1 = d; a1 = a; }
                else if (d < d2) { d2 = d; a2 = a; }
            }
            if (a1 != null && d1 < 12f) Sfx.Loop(spinKeyA, "drill_spin", a1.Sr.transform, 0.9f);
            if (a2 != null && d2 < 12f) Sfx.Loop(spinKeyB, "drill_spin", a2.Sr.transform, 0.55f);
            if (a1 != null && d1 < 0.49f && Time.time >= nextWhoosh)
            {
                nextWhoosh = Time.time + 0.35f;
                Sfx.Play("drill_whoosh", a1.Pos, 0.85f);
            }
        }

        /// <summary>Người chơi đang lăn (bất tử vì lăn, không phải vì vừa dính đòn).</summary>
        private bool Rolling => combat != null && Time.time - combat.LastRollTime < 0.5f && Ctx.PlayerHealth != null && Ctx.PlayerHealth.Invulnerable;

        private void Collide()
        {
            Vector2 p = PlayerFeet + Vector2.up * 0.12f;
            for (int i = 0; i < actors.Count + 1; i++)
            {
                var a = i < actors.Count ? actors[i] : king;
                if (a == null || a.Harmless || a.EdgeAlpha < 0.5f) continue;
                float r = a.Radius + 0.12f;
                if ((p - a.Body).sqrMagnitude > r * r) continue;
                Struck(a.Body, a == king ? 6f : 4f, a);
            }
        }

        /// <summary>Ren bị một thứ trong sân đánh trúng (người nộm, Burlap, sóng dậm, bao cỏ lạc): lăn xuyên = NÉ SẠCH; còn lại mất 1 tinh thần.</summary>
        private void Struck(Vector2 from, float damage, DrillActor who, bool rollDodges = true)
        {
            if (Time.time < hitCooldown) return;
            if (rollDodges && Rolling)
            {
                if (who != null && who.Credited) return;
                if (who != null) who.Credited = true;
                if (Time.time >= nextDodgeSay)
                {
                    nextDodgeSay = Time.time + 0.5f;
                    DrillBoard.Say("CLEAN DODGE!", Gold);
                    Sfx.Play("parry", PlayerFeet, 0.45f);
                    if (Time.time >= nextLine && Random.value < 0.3f) Bark(Pick("Was that a ROLL? Do it AGAIN!", "...Not bad. NOT GOOD EITHER!", "Show-off! I LOVE it! I mean - SILENCE!"), 2.2f, false);
                }
                return;
            }
            if (Ctx.PlayerHealth == null || (Ctx.PlayerHealth.Invulnerable && !(Rolling && !rollDodges))) return;
            hitCooldown = Time.time + 1.1f;
            hitsTotal++; stationHits++;
            Vector2 p = PlayerFeet;
            Vector2 dir = (p - from).sqrMagnitude > 0.0001f ? (p - from).normalized : Vector2.down;
            DrillFx.Impact(Ctx.Host, p, dir, Layer, Order);
            Sfx.Play("blunt_strike", p);
            Sfx.Play("hit_wood", p, 0.6f);
            if (who != null) who.Flash(new Color(1f, 0.9f, 0.6f), 0.25f);
            if (rollDodges) Ctx.HurtPlayer(damage, dir, 6f);
            else TrapArt.HurtPlayerCrush(Ctx, damage, dir, 6f);                    // §365: dậm / bao cát rơi — lăn không cứu
            DrillBoard.LoseMorale();
            lives = DrillBoard.Morale;                                                 // §368: mạng chung
            DrillBoard.Say("HIT!  MORALE -1", Red);
            if (carrying) Snuff(p);
            if (DrillBoard.Morale <= 0) { FailStation(Pick("OUT OF MORALE!", "DOWN AND OUT!", "FLATTENED!")); return; }
            if (Time.time >= nextLine) Bark(Pick("Did I say HUG the soldier?!", "That's going in your FILE!", "OW! Mind the STUFFING!",
                                                 "My grandmother dodges better, and she's a SCARECROW!", "Ten demerits! TWENTY!"), 2.2f, false);
        }

        private int Layer => king != null && king.Sr != null ? king.Sr.sortingLayerID : 0;
        private int Order => king != null && king.Sr != null ? king.Sr.sortingOrder : 2;

        // ------------------------------------------------------------------ lời trung sĩ
        private void Bark(string text, float seconds, bool force)
        {
            if (!force && Time.time < nextLine) return;
            nextLine = Time.time + Mathf.Max(2.5f, seconds * 0.8f);
            var b = SpeechBubble.Show(king.Pos + Vector2.up * 1.1f, text, seconds, Gold, "Sgt. Burlap");
            if (b != null && king.Sr != null) b.Follow(king.Sr.transform);
        }

        private static string Pick(params string[] s) => s[Random.Range(0, s.Length)];

        internal override void DebugSolve()
        {
            if (passed) return;
            passed = true; phase = Phase.Done;
            Ctx.Flags.Set(Str("sets", "drill_passed"));
            OnFlagsChanged();
        }
    }
}
