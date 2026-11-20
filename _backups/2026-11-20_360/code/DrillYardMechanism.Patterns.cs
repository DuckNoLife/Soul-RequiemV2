using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 20/11 (§356): các ĐỘI HÌNH của The Drill Yard (kiểu né đạn Undertale: người nộm xếp hàng chặn đường, chừa đúng một khe).
    /// Toạ độ theo sân A (Rect thế giới). Khoảng cách 2 người nộm trong hàng = SLOT (0,42 đv); khe = 2-3 slot.
    /// </summary>
    public sealed partial class DrillYardMechanism
    {
        private const float Slot = 0.42f;

        private DrillActor Spawn(string kind, Vector2 at, Vector2 vel, string anim, float fps, float radius, int row)
        {
            var a = new DrillActor(kind, at, Ctx.Host.transform, Ctx.Player) { Vel = vel, Radius = radius, Row = row };
            a.Play(anim, fps);
            a.Tick(0f);
            actors.Add(a);
            return a;
        }

        private int NewRow(System.Func<float> coord, bool vertical, int bonus = 25, string label = "IN STEP!")
        {
            int id = nextRow++;
            rows[id] = new RowState { coord = coord, vertical = vertical, bonus = bonus, label = label };
            return id;
        }

        /// <summary>Hàng NGANG bao cát hành quân từ mép bắc xuống (dir = -1) hoặc từ nam lên (+1), chừa khe `gap` slot quanh ô `gapAt`.</summary>
        private void MarchRow(int gapAt, int gap, float speed, int dir, string kind = "sack")
        {
            int n = Mathf.FloorToInt((A.width - 0.2f) / Slot);
            float x0 = A.xMin + (A.width - (n - 1) * Slot) * 0.5f;
            float y = dir < 0 ? A.yMax + 0.25f : A.yMin - 0.35f;
            var lead = new List<DrillActor>();
            int row = -1;
            DrillActor first = null;
            for (int i = 0; i < n; i++)
            {
                if (i >= gapAt && i < gapAt + gap) continue;
                var a = Spawn(kind, new Vector2(x0 + i * Slot, y), new Vector2(0f, speed * dir), "move", 14f, 0.16f, -1);
                a.FaceByVel = false; a.Sr.flipX = (i & 1) == 1;
                if (first == null) first = a;
                lead.Add(a);
            }
            if (first == null) return;
            var f = first;
            row = NewRow(() => f.Pos.y + 0.1f, false);
            foreach (var a in lead) a.Row = row;
            Sfx.Play("footstep", new Vector2(A.center.x, y), 0.5f);
        }

        /// <summary>Cột DỌC người rơm quét ngang (dir = +1: từ tây sang đông).</summary>
        private void SideColumn(int gapAt, int gap, float speed, int dir)
        {
            int n = Mathf.FloorToInt((A.height - 0.6f) / Slot);
            float y0 = A.yMin + 0.25f;
            float x = dir > 0 ? A.xMin - 0.35f : A.xMax + 0.35f;
            DrillActor first = null;
            var col = new List<DrillActor>();
            for (int i = 0; i < n; i++)
            {
                if (i >= gapAt && i < gapAt + gap) continue;
                var a = Spawn("straw", new Vector2(x, y0 + i * Slot), new Vector2(speed * dir, 0f), "move", 14f, 0.17f, -1);
                if (first == null) first = a;
                col.Add(a);
            }
            if (first == null) return;
            var f = first;
            int row = NewRow(() => f.Pos.x, true);
            foreach (var a in col) a.Row = row;
        }

        // ------------------------------------------------------------------ HIỆP 1 / 2
        private IEnumerator MarchRows(float end, float every, float speed, int gap, float delay = 0f)
        {
            if (delay > 0f) yield return Wait(delay);
            int n = Mathf.FloorToInt((A.width - 0.2f) / Slot);
            int gapAt = Random.Range(2, n - gap - 2);
            float t0 = Time.time;
            while (Time.time < end - 1.5f && !aborted)
            {
                bool late = Time.time - t0 > 12f;
                MarchRow(gapAt, gap, speed * (late ? 1.15f : 1f), -1);
                // khe dời dần (đi được tới kịp): tối đa 4 slot mỗi hàng
                gapAt = Mathf.Clamp(gapAt + Random.Range(-4, 5), 1, n - gap - 1);
                yield return Wait(late ? every * 0.85f : every);
            }
        }

        private IEnumerator SideColumns(float end)
        {
            int n = Mathf.FloorToInt((A.height - 0.6f) / Slot);
            int gapAt = Random.Range(2, n - 5);
            int dir = 1;
            while (Time.time < end - 1.5f && !aborted)
            {
                SideColumn(gapAt, 3, 1.15f, dir);
                dir = -dir;
                gapAt = Mathf.Clamp(gapAt + Random.Range(-4, 5), 1, n - 4);
                yield return Wait(2.5f);
            }
        }

        // ------------------------------------------------------------------ HIỆP 3: THE WHEEL
        private IEnumerator Wheel(float end)
        {
            const int N = 14;
            Vector2 c = A.center + Vector2.down * 0.2f;
            float maxR = Mathf.Min(A.width, A.height) * 0.5f - 0.35f;
            var ring = new List<DrillActor>();
            int g0 = Random.Range(0, N);
            for (int i = 0; i < N; i++)
            {
                if (i == g0 || i == (g0 + 1) % N || i == (g0 + N / 2) % N || i == (g0 + N / 2 + 1) % N) { ring.Add(null); continue; }
                var a = Spawn("pell", c, Vector2.zero, "spin", 16f, 0.22f, -1);
                a.FaceByVel = false;
                ring.Add(a);
            }
            // điểm "qua vòng": Ren đổi phía trong <-> ngoài vòng mà không dính đòn
            float rNow = maxR;
            int row = NewRow(() => rNow, false, 40, "THROUGH THE WHEEL!");
            var rs = rows[row];
            rs.coord = null;                                                     // tự tính bên dưới
            foreach (var a in ring) if (a != null) a.Row = row;
            float ang = Random.Range(0f, 360f), t0 = Time.time;
            bool wasIn = (PlayerFeet - c).magnitude < rNow;
            while (Time.time < end && !aborted)
            {
                float t = Time.time - t0;
                rNow = Mathf.Lerp(maxR, 0.95f, 0.5f - 0.5f * Mathf.Cos(t * Mathf.PI * 2f / 6.5f));
                ang += (t > 12f ? 52f : 38f) * Time.deltaTime;
                for (int i = 0; i < N; i++)
                {
                    var a = ring[i];
                    if (a == null) continue;
                    float d = (ang + i * 360f / N) * Mathf.Deg2Rad;
                    a.Pos = c + new Vector2(Mathf.Cos(d) * rNow, Mathf.Sin(d) * rNow * 0.8f);
                }
                // đổi phía trong / ngoài
                Vector2 q = PlayerFeet - c; q.y /= 0.8f;
                bool isIn = q.magnitude < rNow;
                if (isIn != wasIn)
                {
                    wasIn = isIn;
                    if (!rs.hit)
                    {
                        float gain = rs.bonus * combo;
                        AddScore(gain, true);
                        combo = Mathf.Min(4f, combo + 0.25f);
                        DrillBoard.Say(rs.label + " +" + Mathf.RoundToInt(gain), Green);
                    }
                    rs.hit = false;
                }
                yield return null;
            }
        }

        /// <summary>Con quay (pell xoay áo choàng) nảy quanh tường sân.</summary>
        private IEnumerator Top(float end, float delay)
        {
            yield return Wait(delay);
            if (aborted) yield break;
            var a = Spawn("pell", new Vector2(A.xMin + 0.5f, A.yMax - 0.6f), new Vector2(1.5f, -1.1f), "top", 16f, 0.24f, -1);
            Bark("SPINNING TOP! Corporal Pell, show them the WALTZ!", 2.4f, false);
            while (Time.time < end && !aborted)
            {
                if (a.Pos.x < A.xMin + 0.3f && a.Vel.x < 0f || a.Pos.x > A.xMax - 0.3f && a.Vel.x > 0f) { a.Vel.x = -a.Vel.x; Sfx.Play("hit_wood", a.Pos, 0.4f); }
                if (a.Pos.y < A.yMin + 0.2f && a.Vel.y < 0f || a.Pos.y > A.yMax - 0.9f && a.Vel.y > 0f) { a.Vel.y = -a.Vel.y; Sfx.Play("hit_wood", a.Pos, 0.4f); }
                if (a.Credited && (a.Body - PlayerFeet).magnitude > 0.8f) a.Credited = false;   // né lại được lần nữa
                yield return null;
            }
        }

        // ------------------------------------------------------------------ HIỆP 4: INSPECTION (Burlap ra trận)
        private IEnumerator Inspection(float end)
        {
            int step = 0;
            while (Time.time < end - 1f && !aborted)
            {
                switch (step % 4)
                {
                    case 0: yield return Command(); break;
                    case 1: yield return KingDash(step >= 4 ? 2 : 1); break;
                    case 2: yield return Command(); break;
                    default: yield return Stomp(); break;
                }
                step++;
                if (step % 2 == 0 && Time.time < end - 4f)
                {
                    int n = Mathf.FloorToInt((A.width - 0.2f) / Slot);
                    MarchRow(Random.Range(2, n - 5), 3, 1.25f, Random.value < 0.5f ? -1 : 1);
                }
                yield return Wait(0.5f);
            }
            king.Harmless = true; king.Vel = Vector2.zero;
        }

        /// <summary>Khẩu lệnh: hô một hướng -> bức tường bao cát lăn quét NỬA KIA của sân. Có lúc hô nhầm rồi sửa (luôn sửa trước 0,6 s).</summary>
        private IEnumerator Command()
        {
            string[] dirs = { "LEFT", "RIGHT", "UP", "DOWN" };
            int want = Random.Range(0, 4);
            bool fake = Random.value < 0.3f;
            int said = fake ? (want ^ 1) : want;
            Bark(dirs[said] + "!", 1.2f, true);
            if (fake)
            {
                yield return Wait(0.55f);
                Bark(Pick("...No! My " + dirs[want] + "! YOUR " + dirs[want] + "!", dirs[want] + "! I said " + dirs[want] + "! Clean your EARS!"), 1.4f, true);
            }
            yield return Wait(fake ? 0.9f : 1.25f);
            if (aborted) yield break;
            // tường lăn quét nửa KHÔNG được hô
            bool horiz = want <= 1;
            bool safeLow = want == 0 || want == 3;                               // LEFT -> nửa trái an toàn, DOWN -> nửa dưới an toàn
            var wall = new List<DrillActor>();
            Sfx.Play("ram_charge", A.center, 0.7f);
            if (horiz)
            {
                int n = Mathf.FloorToInt((A.height - 0.4f) / 0.34f);
                float x = safeLow ? A.xMax + 0.3f : A.xMin - 0.3f;
                float vx = safeLow ? -4.2f : 4.2f;
                for (int i = 0; i < n; i++) wall.Add(Spawn("sack", new Vector2(x, A.yMin + 0.2f + i * 0.34f), new Vector2(vx, 0f), "roll", 22f, 0.16f, -1));
            }
            else
            {
                int n = Mathf.FloorToInt((A.width - 0.2f) / 0.34f);
                float y = safeLow ? A.yMax + 0.2f : A.yMin - 0.3f;
                float vy = safeLow ? -4.2f : 4.2f;
                for (int i = 0; i < n; i++) wall.Add(Spawn("sack", new Vector2(A.xMin + 0.2f + i * 0.34f, y), new Vector2(0f, vy), "roll", 22f, 0.16f, -1));
            }
            int row = NewRow(null, false, 60, "BY THE BOOK!");
            foreach (var a in wall) a.Row = row;
            // dừng ở giữa sân rồi tan
            float half = horiz ? A.width * 0.5f + 0.3f : A.height * 0.5f + 0.3f;
            yield return Wait(half / 4.2f);
            foreach (var a in wall) { a.Vel = Vector2.zero; a.Harmless = true; }
            if (rows.TryGetValue(row, out var rs) && !rs.hit)
            {
                float gain = rs.bonus * combo;
                AddScore(gain, true);
                combo = Mathf.Min(4f, combo + 0.25f);
                DrillBoard.Say(rs.label + " +" + Mathf.RoundToInt(gain), Green);
            }
            yield return Wait(0.35f);
            foreach (var a in wall) { actors.Remove(a); a.Destroy(); }
        }

        /// <summary>Burlap lao thẳng vào Ren theo vạch báo (đi lại về chỗ đứng).</summary>
        private IEnumerator KingDash(int times)
        {
            for (int k = 0; k < times && !aborted; k++)
            {
                Vector2 from = king.Pos, to = PlayerFeet;
                Vector2 dir = (to - from).sqrMagnitude > 0.01f ? (to - from).normalized : Vector2.down;
                Vector2 stop = from + dir * Mathf.Min(6f, (to - from).magnitude + 1.2f);
                stop = new Vector2(Mathf.Clamp(stop.x, A.xMin + 0.4f, A.xMax - 0.4f), Mathf.Clamp(stop.y, A.yMin + 0.3f, A.yMax - 0.5f));
                Bark(Pick("CHAAARGE!", "INSPECTION!", "HOLD STILL!"), 1f, true);
                var line = SpriteVfx.Spawn("m3_ray_warn", from, 1f, Layer, Order - 1, false, 12f, false);
                if (line != null)
                {
                    line.Looping(0.8f);
                    Vector2 d = stop - from;
                    line.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                    line.transform.localScale = new Vector3(Mathf.Max(0.01f, d.magnitude / 0.62f), 1.8f, 1f);
                    line.Tint(new Color(1f, 0.45f, 0.35f, 0.85f));
                }
                king.Play("idle", 10f);
                king.Sr.flipX = dir.x < 0f;
                yield return Wait(0.8f);
                if (aborted) yield break;
                int row = NewRow(null, false, 45, "CLOSE SHAVE!");
                king.Row = row; king.Harmless = false; king.Credited = false;
                king.Play("dash", 18f);
                Sfx.Play("ram_charge", king.Pos);
                float dist = (stop - king.Pos).magnitude, t = 0f, dur = dist / 6f;
                Vector2 s0 = king.Pos;
                bool near = false;
                while (t < dur && !aborted)
                {
                    t += Time.deltaTime;
                    king.Pos = Vector2.Lerp(s0, stop, t / dur);
                    if ((king.Body - PlayerFeet).magnitude < 1.0f) near = true;
                    yield return null;
                }
                king.Harmless = true;
                king.Play("dashend", 18f, false);
                CameraFollow.KickMain(dir, 0.04f, 0.2f);
                if (near && rows.TryGetValue(row, out var rs) && !rs.hit)
                {
                    float gain = rs.bonus * combo;
                    AddScore(gain, true);
                    combo = Mathf.Min(4f, combo + 0.25f);
                    DrillBoard.Say(rs.label + " +" + Mathf.RoundToInt(gain), Green);
                }
                yield return Wait(0.5f);
                // đi về chỗ đứng chỉ huy
                king.Play("move", 14f);
                while ((king.Pos - kingHome).magnitude > 0.05f && !aborted)
                {
                    king.Pos = Vector2.MoveTowards(king.Pos, kingHome, 2.6f * Time.deltaTime);
                    king.Sr.flipX = kingHome.x < king.Pos.x;
                    yield return null;
                }
                king.Play("idle", 4f);
            }
        }

        /// <summary>Burlap dậm đất: sóng chấn động có 3 khe (khe là chỗ né), dính -> phạt như chạm người nộm.</summary>
        private IEnumerator Stomp()
        {
            Bark(Pick("STOMP DRILL! Find the gaps!", "Feel that? That's DISCIPLINE!"), 1.4f, true);
            king.Play("dashend", 14f, false);
            yield return Wait(0.7f);
            if (aborted) yield break;
            Sfx.Play("duo_slam", king.Pos);
            CameraFollow.KickMain(Vector2.down, 0.07f, 0.3f);
            SpriteVfx.Spawn("duo_quake", king.Pos, 1.4f, Layer, Order - 1, false, 14f, false);
            bool hit = false;
            var w = Shockwave.Spawn(king.Pos, Mathf.Max(A.width, A.height) * 0.95f, 1.6f, 0f, Order - 1, new Color(1f, 0.8f, 0.5f, 0.95f));
            w.knockback = 0f;
            float g = Random.Range(0f, 120f); w.gapAngles = new[] { g, g + 120f, g + 240f }; w.gapWidth = 34f;
            w.onHit = h => { hit = true; Penalty(king.Pos); };
            yield return Wait(1.7f);
            if (!hit && !aborted)
            {
                float gain = 45f * combo;
                AddScore(gain, true);
                combo = Mathf.Min(4f, combo + 0.25f);
                DrillBoard.Say("STEADY FEET! +" + Mathf.RoundToInt(gain), Green);
            }
            king.Play("idle", 4f);
        }

        private void Penalty(Vector2 from)
        {
            if (Time.time < hitCooldown) return;
            hitCooldown = Time.time + 0.9f;
            hits++;
            score = Mathf.Max(0f, score - 150f);
            combo = 1f;
            Vector2 p = PlayerFeet;
            Ctx.HurtPlayer(2f, (p - from).sqrMagnitude > 0.0001f ? (p - from).normalized : Vector2.down, 3.5f);
            DrillBoard.Say("HIT! -150", Red);
            if (Time.time >= nextLine) Bark(Pick("You call that a STANCE?!", "Feet! FEET! You have TWO!"), 2f, false);
        }
    }
}
