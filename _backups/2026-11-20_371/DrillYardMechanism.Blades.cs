using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 20/11 (§360): TRẠM 1 — BLADE DRILL. Người nộm XOAY quét sân theo đội hình, mỗi đợt báo trước bằng hàng mũi tên đỏ
    /// (khe an toàn = mũi tên xanh nhỏ). Kịch bản 45 s tăng dần: hàng -> hàng đôi -> cột -> bao cát lăn nảy tường -> chữ thập ->
    /// cọc săn người -> CỐI XAY (2 cánh người nộm quay quanh tâm sân) -> chữ thập + bao lăn.
    /// Toạ độ theo sân A (Rect thế giới). Khoảng cách 2 người nộm trong hàng = Slot (0,42 đv); khe = 3 slot (~1,26 đv).
    /// </summary>
    public sealed partial class DrillYardMechanism
    {
        private const float Slot = 0.42f, Telegraph = 0.95f, BladeTime = 45f;

        private DrillActor Spawn(string kind, Vector2 at, Vector2 vel, string anim, float fps, float radius, bool whirl = true)
        {
            var a = new DrillActor(kind, at, Ctx.Host.transform, Ctx.Player) { Vel = vel, Radius = radius };
            a.Play(anim, fps);
            if (whirl) a.Whirl(true, kind == "pell" ? 1.25f : kind == "sack" ? 0.7f : 0.95f);
            a.EdgeAlpha = 0f;
            a.Tick(0f);
            actors.Add(a);
            return a;
        }

        /// <summary>Người nộm quay: rơm = "spin" (lưỡi trắng), cọc = "spin" (áo choàng đỏ), bao cát = "roll" (lăn tròn).</summary>
        private DrillActor Spinner(string kind, Vector2 at, Vector2 vel)
        {
            switch (kind)
            {
                case "sack": return Spawn("sack", at, vel, "roll", 22f, 0.17f);
                case "pell": return Spawn("pell", at, vel, "spin", 20f, 0.26f);
                default: return Spawn("straw", at, vel, "spin", 20f, 0.21f);
            }
        }

        private IEnumerator Blades()
        {
            DrillBoard.Goal = "SURVIVE";
            float t0 = Time.time;
            Start(BladeClock(t0));
            int nx = Mathf.FloorToInt((A.width - 0.2f) / Slot), ny = Mathf.FloorToInt((A.height - 0.6f) / Slot);
            int gx = Random.Range(3, nx - 6), gy = Random.Range(3, ny - 6);
            // (giây kể từ đầu trạm, đợt)
            yield return At(t0, 0.5f); Row(-1, gx, 1.5f);
            yield return At(t0, 4.0f); gx = Shift(gx, nx); Row(-1, gx, 1.8f);
            yield return At(t0, 5.4f); gx = Shift(gx, nx); Row(-1, gx, 1.8f);
            yield return At(t0, 9.0f); Column(1, gy, 1.7f);
            yield return At(t0, 12.5f); Start(Rollers(3, 6.5f));
            yield return At(t0, 15.0f); gx = Shift(gx, nx); Row(1, gx, 1.9f);
            yield return At(t0, 19.0f); gx = Shift(gx, nx); gy = Shift(gy, ny); Row(-1, gx, 1.6f); Column(-1, gy, 1.6f);
            yield return At(t0, 23.0f); Start(Hunters(2, 6.5f));
            yield return At(t0, 26.5f); gx = Shift(gx, nx); Row(-1, gx, 2.1f);
            yield return At(t0, 30.0f); Start(Mill(9f));
            yield return At(t0, 34.0f); Start(Rollers(2, 5f));
            yield return At(t0, 40.0f); gx = Shift(gx, nx); gy = Shift(gy, ny); Row(1, gx, 1.9f); Column(1, gy, 1.9f); Start(Rollers(2, 4.5f));
            yield return At(t0, BladeTime);
        }

        private IEnumerator At(float t0, float t) { while (Time.time - t0 < t && !aborted && !stationFailed) yield return null; }

        private IEnumerator BladeClock(float t0)
        {
            while (!aborted)
            {
                float left = Mathf.Max(0f, BladeTime - (Time.time - t0));
                DrillBoard.TimeFrac = left / BladeTime;
                DrillBoard.TimeText = Clock(left);
                DrillBoard.GoalFrac = 1f - left / BladeTime;
                yield return null;
            }
        }

        private static string Clock(float s) { int k = Mathf.CeilToInt(s); return (k / 60) + ":" + (k % 60).ToString("00"); }

        /// <summary>Khe dời tối đa 4 slot (luôn chạy tới kịp).</summary>
        private static int Shift(int g, int n) => Mathf.Clamp(g + Random.Range(-4, 5), 1, n - 4);

        // ------------------------------------------------------------------ hàng ngang / cột dọc
        /// <summary>Hàng người rơm XOAY quét dọc sân (dir -1: từ bắc xuống, +1: từ nam lên), chừa khe 3 slot ở `gapAt`. Báo trước rồi mới vào.</summary>
        private void Row(int dir, int gapAt, float speed) => Start(RowRoutine(dir, gapAt, speed));

        private IEnumerator RowRoutine(int dir, int gapAt, float speed)
        {
            int n = Mathf.FloorToInt((A.width - 0.2f) / Slot);
            float x0 = A.xMin + (A.width - (n - 1) * Slot) * 0.5f;
            float yIn = dir < 0 ? A.yMax - 0.35f : A.yMin + 0.25f;
            float g0 = (gapAt - 0.5f) / (n - 1), g1 = (gapAt + 2.5f) / (n - 1);
            DrillFx.Warn(Ctx.Host, new Vector2(x0, yIn), new Vector2(x0 + (n - 1) * Slot, yIn), Telegraph, new Vector2(0f, dir), Ctx.Player, Slot, g0, g1);
            Sfx.Play("rf_war_drum", new Vector2(A.center.x, yIn), 0.55f);
            yield return Wait(Telegraph);
            if (aborted || stationFailed) yield break;
            float y = dir < 0 ? A.yMax + 0.2f : A.yMin - 0.45f;
            for (int i = 0; i < n; i++)
            {
                if (i >= gapAt && i < gapAt + 3) continue;
                var a = Spinner("straw", new Vector2(x0 + i * Slot, y), new Vector2(0f, speed * dir));
                a.FaceByVel = false; a.Sr.flipX = (i & 1) == 1;
            }
            Sfx.Play("lg_whirl", new Vector2(A.center.x, y), 0.7f);
        }

        private void Column(int dir, int gapAt, float speed) => Start(ColumnRoutine(dir, gapAt, speed));

        private IEnumerator ColumnRoutine(int dir, int gapAt, float speed)
        {
            int n = Mathf.FloorToInt((A.height - 0.6f) / Slot);
            float y0 = A.yMin + 0.25f;
            float xIn = dir > 0 ? A.xMin + 0.25f : A.xMax - 0.25f;
            float g0 = (gapAt - 0.5f) / (n - 1), g1 = (gapAt + 2.5f) / (n - 1);
            DrillFx.Warn(Ctx.Host, new Vector2(xIn, y0), new Vector2(xIn, y0 + (n - 1) * Slot), Telegraph, new Vector2(dir, 0f), Ctx.Player, Slot, g0, g1);
            Sfx.Play("rf_war_drum", new Vector2(xIn, A.center.y), 0.55f);
            yield return Wait(Telegraph);
            if (aborted || stationFailed) yield break;
            float x = dir > 0 ? A.xMin - 0.4f : A.xMax + 0.4f;
            for (int i = 0; i < n; i++)
            {
                if (i >= gapAt && i < gapAt + 3) continue;
                Spinner("pell", new Vector2(x, y0 + i * Slot), new Vector2(speed * dir, 0f));
            }
            Sfx.Play("lg_whirl", new Vector2(x, A.center.y), 0.7f);
        }

        // ------------------------------------------------------------------ bao cát lăn nảy tường
        private IEnumerator Rollers(int count, float life)
        {
            for (int k = 0; k < count; k++)
            {
                // góc xuất phát cách xa người chơi
                Vector2 at = Vector2.zero; float best = -1f;
                for (int t = 0; t < 6; t++)
                {
                    var c = new Vector2(Random.value < 0.5f ? A.xMin + 0.4f : A.xMax - 0.4f, Random.Range(A.yMin + 0.6f, A.yMax - 0.8f));
                    float d = (c - PlayerFeet).magnitude;
                    if (d > best) { best = d; at = c; }
                }
                Vector2 v = new Vector2(at.x < A.center.x ? 1f : -1f, Random.value < 0.5f ? 0.75f : -0.75f).normalized * 2.7f;
                DrillFx.Warn(Ctx.Host, at, at + v.normalized * 1.0f, Telegraph, v, Ctx.Player, 0.3f);
                Start(Roller(at, v, life, Telegraph + k * 0.25f));
            }
            yield break;
        }

        private IEnumerator Roller(Vector2 at, Vector2 v, float life, float delay)
        {
            yield return Wait(delay);
            if (aborted || stationFailed) yield break;
            var a = Spinner("sack", at, v);
            a.EdgeAlpha = 1f;
            Sfx.Play("trap_roller", at, 0.5f);
            float end = Time.time + life;
            while (Time.time < end && !aborted && !stationFailed && actors.Contains(a))
            {
                if (a.Pos.x < A.xMin + 0.25f && a.Vel.x < 0f || a.Pos.x > A.xMax - 0.25f && a.Vel.x > 0f) { a.Vel.x = -a.Vel.x; Bonk(a); }
                if (a.Pos.y < A.yMin + 0.15f && a.Vel.y < 0f || a.Pos.y > A.yMax - 0.7f && a.Vel.y > 0f) { a.Vel.y = -a.Vel.y; Bonk(a); }
                if (a.Credited && (a.Body - PlayerFeet).magnitude > 0.8f) a.Credited = false;   // né lại được lần nữa
                yield return null;
            }
            yield return Vanish(a);
        }

        private void Bonk(DrillActor a)
        {
            Sfx.Play("hit_wood", a.Pos, 0.35f);
            SpriteVfx.Spawn("m3_dash_dust", a.Pos, 0.8f, Layer, a.Sr.sortingOrder - 1, a.Vel.x > 0f, 16f, false);
        }

        /// <summary>Người nộm rời đội hình: ngừng quay, mờ dần rồi biến mất (không còn gây đau).</summary>
        private IEnumerator Vanish(DrillActor a)
        {
            if (a == null || !actors.Contains(a)) yield break;
            a.Harmless = true; a.Vel *= 0.3f; a.Whirl(false);
            for (float t = 0f; t < 0.4f; t += Time.deltaTime) { a.SetAlpha(1f - t / 0.4f); yield return null; }
            actors.Remove(a); a.Destroy();
        }

        // ------------------------------------------------------------------ cọc săn người
        private IEnumerator Hunters(int count, float life)
        {
            Bark(Pick("CORPORAL PELL! Escort the recruit! CLOSELY!", "Hunters! Shadow that recruit! Politely! VIOLENTLY polite!"), 2.4f, true);
            var list = new List<DrillActor>();
            for (int k = 0; k < count; k++)
            {
                var at = new Vector2(k % 2 == 0 ? A.xMin + 0.5f : A.xMax - 0.5f, k < 2 ? A.yMax - 0.8f : A.yMin + 0.4f);
                DrillFx.Warn(Ctx.Host, at + Vector2.left * 0.35f, at + Vector2.right * 0.35f, Telegraph, (A.center - at).normalized, Ctx.Player, 0.35f);
            }
            yield return Wait(Telegraph);
            if (aborted || stationFailed) yield break;
            for (int k = 0; k < count; k++)
            {
                var at = new Vector2(k % 2 == 0 ? A.xMin + 0.5f : A.xMax - 0.5f, k < 2 ? A.yMax - 0.8f : A.yMin + 0.4f);
                var a = Spinner("pell", at, Vector2.zero);
                a.EdgeAlpha = 1f;
                list.Add(a);
            }
            float end = Time.time + life;
            while (Time.time < end && !aborted && !stationFailed)
            {
                foreach (var a in list)
                {
                    if (!actors.Contains(a)) continue;
                    // đuổi CHẬM hơn Ren (1,15 đv/s) + tránh dồn vào nhau
                    Vector2 want = (PlayerFeet - a.Pos);
                    want = want.sqrMagnitude > 0.01f ? want.normalized * 1.15f : Vector2.zero;
                    foreach (var b in list) if (b != a && (b.Pos - a.Pos).sqrMagnitude < 0.5f * 0.5f) want += (a.Pos - b.Pos).normalized * 0.6f;
                    a.Vel = Vector2.MoveTowards(a.Vel, want, 3f * Time.deltaTime);
                    if (a.Credited && (a.Body - PlayerFeet).magnitude > 0.8f) a.Credited = false;
                }
                yield return null;
            }
            foreach (var a in list) Ctx.Run(Vanish(a));
        }

        // ------------------------------------------------------------------ CỐI XAY: 2 cánh người nộm quay quanh tâm sân
        private IEnumerator Mill(float life)
        {
            Vector2 c = A.center + Vector2.down * 0.15f;
            const int Per = 5;
            float r0 = 0.75f, step = 0.5f, ang = Random.Range(0f, 360f);
            Bark("THE MILL! Round and round, like my promotion prospects!", 2.6f, true);
            // báo trước: hai cánh mũi tên tại chỗ cánh sẽ mọc, chỉ theo chiều quay
            for (int arm = 0; arm < 2; arm++)
            {
                float d = (ang + arm * 180f) * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(d), Mathf.Sin(d));
                DrillFx.Warn(Ctx.Host, c + dir * r0, c + dir * (r0 + step * (Per - 1)), Telegraph + 0.35f, new Vector2(-dir.y, dir.x), Ctx.Player, step);
            }
            yield return Wait(Telegraph + 0.35f);
            if (aborted || stationFailed) yield break;
            var hub = Spawn("pell", c, Vector2.zero, "spin", 22f, 0.3f);
            hub.EdgeAlpha = 1f;
            var arms = new List<DrillActor>();
            for (int i = 0; i < Per * 2; i++) { var a = Spinner("straw", c, Vector2.zero); a.EdgeAlpha = 1f; a.FaceByVel = false; arms.Add(a); }
            Sfx.Play("lg_whirl", c);
            float end = Time.time + life, spin = 62f, t0 = Time.time;
            bool flipped = false;
            while (Time.time < end && !aborted && !stationFailed)
            {
                // nửa sau đổi chiều quay (báo bằng tiếng trống + câu hô)
                if (!flipped && Time.time - t0 > life * 0.5f)
                {
                    flipped = true; spin = -78f;
                    Bark("REVERSE! ...Did I say that BEFORE or AFTER? Doesn't matter! REVERSE!", 2.2f, true);
                    Sfx.Play("rf_war_drum", c, 0.6f);
                }
                ang += spin * Time.deltaTime;
                for (int i = 0; i < arms.Count; i++)
                {
                    int arm = i / Per, k = i % Per;
                    float d = (ang + arm * 180f) * Mathf.Deg2Rad;
                    arms[i].Pos = c + new Vector2(Mathf.Cos(d), Mathf.Sin(d) * 0.9f) * (r0 + step * k);
                    if (arms[i].Credited && (arms[i].Body - PlayerFeet).magnitude > 0.8f) arms[i].Credited = false;
                }
                yield return null;
            }
            foreach (var a in arms) Ctx.Run(Vanish(a));
            Ctx.Run(Vanish(hub));
        }
    }
}
