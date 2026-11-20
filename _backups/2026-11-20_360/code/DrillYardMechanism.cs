using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 20/11 (§356): drill_yard — THE DRILL YARD (Map 4): "trận boss" KHÔNG KIẾM — minigame né kiểu Undertale với đội người nộm luyện tập
    /// do TRUNG SĨ BURLAP (người nộm đá đội vương miện, MEGAPACK Dummy_LVL4) chỉ huy. Nghiêm như quân đội, nhưng nhí nhảnh.
    /// Vùng object = SÂN (Ren rời sân giữa chừng = "DESERTER!", huỷ lượt). Bắt đầu bằng tù và (drill_horn, `yard` = tên object này).
    ///   4 hiệp: 1 FORMATION MARCH  — hàng bao cát (sack) dàn ngang hành quân từ bắc xuống, mỗi hàng chừa một KHE (khe dời dần).
    ///           2 ABOUT FACE!       — cột người rơm (straw) quét ngang trái <-> phải + hàng bao cát dọc -> lưới chéo nhau.
    ///           3 THE WHEEL         — vòng người nộm cọc (pell) xoay quanh tâm sân, phồng / co, 2 khe; + 1 con quay nảy tường.
    ///           4 INSPECTION        — Burlap ra trận: hô LEFT / RIGHT / UP / DOWN rồi một bức tường bao cát lăn quét nửa kia (có lúc hô nhầm
    ///                                 rồi sửa — luôn sửa trước 0,6 s); lao thẳng vào Ren theo vạch báo; dậm đất ra sóng có khe.
    ///   ĐIỂM: sống sót +10/s; qua khe (hàng / cột / vòng / né lao / đúng khẩu lệnh) +; LĂN XUYÊN người nộm đúng lúc = PERFECT (++); nhặt
    ///         DẤU ĐÈN (cầu sáng) +; mọi điểm cộng nhân HỆ SỐ COMBO (x1 -> x4, tăng theo mỗi lần né / nhặt). Dính đòn: -150, combo về x1,
    ///         Ren mất 2 máu + bị đẩy. Đủ `target` điểm (mặc định 3000) -> QUA: cờ `sets` (drill_passed), chùm chìa hiện trên bệ (`key_at`).
    ///         Thiếu -> "AGAIN!", bấm tù và chơi lại ngay. Hạng: S >= 1,8x, A >= 1,4x, B >= 1x mục tiêu, C = trượt.
    /// HUD đọc DrillBoard. Thuộc tính: target, sets, king_at ("x,y" ô), key_at ("x,y" ô).
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
        private float score, combo = 1f, hitCooldown, nextBark, nextLine, idleGreetAt, readyAt;
        private bool passed, aborted, wasInside;
        private int hits, round;
        // điểm "qua khe" theo hàng: mã hàng -> (đã dính đòn?, đã tính điểm?, phía ban đầu)
        private readonly Dictionary<int, RowState> rows = new Dictionary<int, RowState>();
        private int nextRow = 1;
        private readonly List<Mark> marks = new List<Mark>();

        private class RowState { public bool hit, credited; public float side; public System.Func<float> coord; public bool vertical; public int bonus = 25; public string label = "IN STEP!"; }
        private class Mark { public SpriteVfx fx; public Vector2 pos; public float until; }

        private int Target => Int("target", 3000);
        public bool Passed => passed;
        public bool Busy => phase == Phase.Running;
        public bool Ready => phase == Phase.Idle && !passed && Time.time >= readyAt;
        private Rect A => Rect;
        private Vector2 PlayerFeet => Ctx.Player != null ? (Vector2)Ctx.Player.position : A.center;
        private static readonly Color Gold = new Color(1f, 0.85f, 0.45f), Red = new Color(1f, 0.38f, 0.32f), Green = new Color(0.55f, 1f, 0.6f), Sky = new Color(0.6f, 0.85f, 1f);

        public override void Init()
        {
            DrillBoard.Active = false;
            kingHome = Ctx.TilePoint(Str("king_at", ""), new Vector2(A.center.x, A.yMax - 0.55f));
            keyAt = Ctx.TilePoint(Str("key_at", ""), new Vector2(A.center.x, A.yMax - 0.2f));
            king = new DrillActor("king", kingHome, Ctx.Host.transform, Ctx.Player) { Harmless = true, Radius = 0.42f, FaceByVel = false };
            king.Tick(0f);
            keyring = TrapArt.MakeRenderer(Ctx.Host.transform, "Drill Keyring", keyAt, Ctx.Player, 1);
            keyring.sprite = WardenStatueMechanism.Load("VFX/props/drill_keyring");
            keyring.enabled = false;
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
            main = Ctx.Run(Drill());
        }

        private IEnumerator Drill()
        {
            score = 0f; combo = 1f; hits = 0; round = 0;
            DrillBoard.Reset(Target);
            Sfx.Play("rf_war_drum", A.center);
            Bark("ATTEN-TION! Rules: DODGE my soldiers. Grab the lantern marks. Score " + Target + " or you're scrubbing the yard with your TONGUE!", 4.2f, true);
            yield return Wait(3.4f);
            string[] names = { "FORMATION MARCH", "ABOUT FACE", "THE WHEEL", "INSPECTION" };
            float[] lens = { 25f, 25f, 25f, 30f };
            for (round = 0; round < 4 && !aborted; round++)
            {
                DrillBoard.Title = "ROUND " + (round + 1) + "/4 - " + names[round] + "!";
                DrillBoard.RoundLength = lens[round];
                Sfx.Play("bell_toll", A.center, 0.7f);
                Bark(RoundLine(round), 2.6f, true);
                yield return Wait(1.6f);
                if (aborted) yield break;
                yield return Round(round, lens[round]);
                if (aborted) yield break;
                ClearField(true);
                if (round < 3) { DrillBoard.Title = "AT EASE... BRIEFLY."; yield return Wait(2.2f); }
            }
            if (!aborted) Finish();
        }

        private static string RoundLine(int r)
        {
            switch (r)
            {
                case 0: return "ROUND ONE! FORMATION MARCH! Find the GAP, maggot - my sacks do NOT stop for tourists!";
                case 1: return "ROUND TWO! ABOUT... FACE! Columns, SIDEWAYS! Rows, DOWN! Recruit, ALIVE!";
                case 2: return "ROUND THREE! THE WHEEL! Round and round, like my promotion prospects!";
                default: return "FINAL ROUND! INSPECTION! I inspect PERSONALLY. With my FACE.";
            }
        }

        private IEnumerator Round(int r, float len)
        {
            float end = Time.time + len;
            switch (r)
            {
                case 0: Start(MarchRows(end, 2.6f, 1.0f, 3)); break;
                case 1: Start(SideColumns(end)); Start(MarchRows(end, 5.2f, 1.1f, 3, 7f)); break;
                case 2: Start(Wheel(end)); Start(Top(end, 7f)); break;
                default: Start(Inspection(end)); break;
            }
            float nextMark = Time.time + 1.5f;
            while (Time.time < end && !aborted)
            {
                DrillBoard.RoundLeft = end - Time.time;
                AddScore(10f * Time.deltaTime, false);
                if (Time.time >= nextMark) { nextMark = Time.time + 3.4f; SpawnMark(); }
                yield return null;
            }
            StopRoutines();
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
            while (Time.time < end && !aborted) yield return null;
        }

        // ------------------------------------------------------------------ kết thúc
        private void Finish()
        {
            int s = Mathf.RoundToInt(score), t = Target;
            string rank = s >= t * 1.8f ? "S" : s >= t * 1.4f ? "A" : s >= t ? "B" : "C";
            ClearField(true);
            if (rank != "C")
            {
                passed = true; phase = Phase.Done;
                DrillBoard.Finish("RANK " + rank + " - PASSED", Gold);
                Sfx.Play("puzzle_solved", A.center);
                Bark(rank == "S" ? "...RANK S?! Nobody gets S. I don't even HAVE an S stamp. Take the key. Take my HAT. No - just the key."
                     : rank == "A" ? "Rank A. ...Fine work, recruit. I am NOT crying. That's sawdust. Your key is on the pedestal."
                     : "Rank B. Barely! Sloppy! ...Adequate. The key is yours. Now get out of my yard before I get sentimental.", 5.5f, true);
                Ctx.Flags.Set(Str("sets", "drill_passed"));
                OnFlagsChanged();
            }
            else
            {
                phase = Phase.Idle; readyAt = Time.time + 2f;
                DrillBoard.Finish("RANK C - " + s + " / " + t, Red);
                Sfx.Play("ui_back", A.center);
                Bark("PATHETIC! " + s + " points! I needed " + t + "! AGAIN! From the TOP! Sound the horn when you've stopped embarrassing my yard!", 5f, true);
            }
            Ctx.Run(HideBoard(4.5f));
        }

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
            ClearField(false);
            phase = Phase.Idle; readyAt = Time.time + 2f;
            DrillBoard.Finish("DRILL CANCELLED", Red);
            if (!string.IsNullOrEmpty(why)) Bark(why, 4f, true);
            Ctx.Run(HideBoard(3f));
        }

        private void ClearField(bool salute)
        {
            foreach (var a in actors) a.Destroy();
            actors.Clear();
            rows.Clear();
            foreach (var m in marks) if (m.fx != null) m.fx.Stop();
            marks.Clear();
            if (king != null) { king.Pos = kingHome; king.Vel = Vector2.zero; king.Harmless = true; king.Play("idle", 4f); king.SetAlpha(1f); }
        }

        // ------------------------------------------------------------------ mỗi khung
        public override void Animate()
        {
            float dt = Time.deltaTime;
            if (king != null) king.Tick(dt);
            for (int i = actors.Count - 1; i >= 0; i--) actors[i].Tick(dt);
            bool inside = A.Contains(PlayerFeet);
            if (phase == Phase.Running)
            {
                if (Ctx.PlayerHealth == null || Ctx.PlayerHealth.IsDead) { Abort(null); return; }
                if (!inside) { Abort("DESERTER! Get back here, recruit! ...Fine. Drill CANCELLED. I'm not hurt. My stuffing is just... loose."); return; }
                Collide();
                Rows();
                Marks();
                // dọn người nộm đi quá xa ngoài sân
                for (int i = actors.Count - 1; i >= 0; i--)
                {
                    var a = actors[i];
                    if (a.Pos.x < A.xMin - 1.6f || a.Pos.x > A.xMax + 1.6f || a.Pos.y < A.yMin - 1.6f || a.Pos.y > A.yMax + 1.6f) { a.Destroy(); actors.RemoveAt(i); }
                }
                DrillBoard.Score = Mathf.RoundToInt(score);
                DrillBoard.Combo = combo;
                DrillBoard.Hits = hits;
            }
            else if (inside && !wasInside && Time.time >= idleGreetAt)
            {
                idleGreetAt = Time.time + 25f;
                Bark(passed ? "You again? You PASSED. Go. Shoo. Be free. Write home." : "RECRUIT! Fall in! Sound the HORN when you're ready to be INSPECTED!", 3.5f, true);
            }
            wasInside = inside;
        }

        private void Collide()
        {
            var hp = Ctx.PlayerHealth;
            Vector2 p = PlayerFeet + Vector2.up * 0.12f;
            bool inv = hp != null && hp.Invulnerable;
            for (int i = 0; i < actors.Count + 1; i++)
            {
                var a = i < actors.Count ? actors[i] : king;
                if (a == null || a.Harmless) continue;
                float r = a.Radius + 0.12f;
                if ((p - a.Body).sqrMagnitude > r * r) continue;
                if (inv)
                {
                    if (a.Credited) continue;
                    a.Credited = true;
                    float gain = 60f * combo;
                    AddScore(gain, true);
                    combo = Mathf.Min(4f, combo + 0.5f);
                    DrillBoard.Say("PERFECT! +" + Mathf.RoundToInt(gain), Gold);
                    Sfx.Play("parry", p, 0.6f);
                    if (Time.time >= nextLine && Random.value < 0.35f) Bark(Pick("Was that a ROLL? Do it AGAIN!", "...Not bad. NOT GOOD EITHER!", "Show-off! I LOVE it! I mean - SILENCE!"), 2.2f, false);
                }
                else if (Time.time >= hitCooldown)
                {
                    hitCooldown = Time.time + 0.9f;
                    hits++;
                    score = Mathf.Max(0f, score - 150f);
                    combo = 1f;
                    if (a.Row >= 0 && rows.TryGetValue(a.Row, out var rs)) rs.hit = true;
                    Vector2 dir = (p - a.Body).sqrMagnitude > 0.0001f ? (p - a.Body).normalized : Vector2.down;
                    Ctx.HurtPlayer(2f, dir, 3.5f);
                    DrillBoard.Say("HIT! -150", Red);
                    Sfx.Play("hit_wood", p, 0.8f);
                    if (Time.time >= nextLine) Bark(Pick("Did I say HUG the soldier?!", "That's going in your FILE!", "Ten demerits! TWENTY!", "OW! Mind the STUFFING!", "My grandmother dodges better, and she's a SCARECROW!"), 2.2f, false);
                }
            }
        }

        private void Rows()
        {
            Vector2 p = PlayerFeet;
            foreach (var kv in rows)
            {
                var rs = kv.Value;
                if (rs.credited || rs.coord == null) continue;
                float c = rs.coord();
                float side = Mathf.Sign((rs.vertical ? p.x : p.y) - c);
                if (rs.side == 0f) { rs.side = side; continue; }
                if (side == rs.side) continue;
                rs.credited = true;
                if (rs.hit) continue;
                float gain = rs.bonus * combo;
                AddScore(gain, true);
                combo = Mathf.Min(4f, combo + 0.25f);
                DrillBoard.Say(rs.label + " +" + Mathf.RoundToInt(gain), Green);
            }
        }

        private void Marks()
        {
            Vector2 p = PlayerFeet + Vector2.up * 0.1f;
            for (int i = marks.Count - 1; i >= 0; i--)
            {
                var m = marks[i];
                if (Time.time >= m.until) { if (m.fx != null) m.fx.Stop(); marks.RemoveAt(i); continue; }
                if (m.fx != null && m.until - Time.time < 1.2f) m.fx.Tint(new Color(1f, 1f, 1f, 0.5f + 0.5f * Mathf.Sin(Time.time * 18f)));
                if ((p - m.pos).sqrMagnitude > 0.3f * 0.3f) continue;
                float gain = 40f * combo;
                AddScore(gain, true);
                combo = Mathf.Min(4f, combo + 0.25f);
                DrillBoard.Say("LANTERN MARK +" + Mathf.RoundToInt(gain), Sky);
                Sfx.Play("pickup_item", m.pos, 0.6f);
                if (m.fx != null) m.fx.Stop();
                marks.RemoveAt(i);
            }
        }

        private void SpawnMark()
        {
            if (marks.Count >= 3) return;
            for (int k = 0; k < 8; k++)
            {
                var at = new Vector2(Random.Range(A.xMin + 0.4f, A.xMax - 0.4f), Random.Range(A.yMin + 0.4f, A.yMax - 1.1f));
                float d = (at - PlayerFeet).magnitude;
                if (d < 1.0f || d > 3.4f) continue;
                var fx = SpriteVfx.Spawn("duo_light_orb", at, 0.75f, Layer, Order + 1, false, 12f, false);
                if (fx != null) fx.Looping(9999f);
                marks.Add(new Mark { fx = fx, pos = at, until = Time.time + 6f });
                return;
            }
        }

        private void AddScore(float v, bool popScale) { score += v; }

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
