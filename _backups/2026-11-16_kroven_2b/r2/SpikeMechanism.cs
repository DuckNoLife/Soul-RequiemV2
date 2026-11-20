using System.Collections.Generic;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 08/11 (§196): spikes — Ô GAI SÀN (Crypt spike trap 1-4, 9 khung: 0 phẳng, 1-2 ló đầu = báo, 3-8 đâm lên). Vùng object
    /// được lát kín bằng các ô 32 px; mỗi ô có nhịp riêng. Trúng CẢ QUÁI (đẩy / kéo quái vào gai là một cách đánh).
    ///   mode     cycle (mặc định): nghỉ `off_seconds` -> báo `warn_seconds` -> đâm `on_seconds`, lệch `offset`
    ///            step   : ô bị giẫm thì báo `warn_seconds` (mặc định 0,35) rồi đâm, nghỉ `cooldown` trước khi giẫm lại được
    ///            trigger: chỉ chạy một lượt khi có xung `trigger` (tấm sàn, cần gạt) — xem <see cref="TrapSignals"/>
    ///   wave_step  giây lệch giữa hai ô liền nhau theo `wave_axis` (x | y | -x | -y) -> gai chạy thành sóng
    ///   damage (người chơi, mặc định 10), enemy_damage (mặc định 25), knock (2)
    ///   style 1-4 (màu nền ô)   disable_on: xung làm gai thụt hết `disable_seconds` (mặc định 6) — cần gạt tắt sân gai
    ///   start_when: chỉ chạy khi đủ cờ   stop_when: dừng hẳn (lớp nền)
    /// </summary>
    public sealed class SpikeMechanism : HazardMechanismBase
    {
        private Sprite[] frames;
        private SpriteRenderer[] tiles;
        private Rect[] rects;
        private float[] start;          // step / trigger: thời điểm bắt đầu lượt (-1 = nghỉ)
        private float[] ready;          // step: giẫm lại được từ lúc này
        private int nx, ny;
        private TrapListener trigger, disabler;
        private float disabledUntil, nextScan, nextHurt;
        private readonly List<Health> found = new List<Health>();
        private readonly Dictionary<Health, float> enemyHitAt = new Dictionary<Health, float>();

        private float Warn => Float("warn_seconds", Str("mode", "cycle") == "step" ? 0.35f : 0.6f);
        private float On => Float("on_seconds", 1.2f);
        private const float Retract = 0.3f;

        public override void Init()
        {
            frames = TrapArt.Frames("trap_spike" + Mathf.Clamp(Int("style", 1), 1, 4));
            float ts = frames.Length > 0 ? frames[0].bounds.size.x : 0.32f;
            nx = Mathf.Max(1, Mathf.RoundToInt(Rect.width / ts));
            ny = Mathf.Max(1, Mathf.RoundToInt(Rect.height / ts));
            tiles = new SpriteRenderer[nx * ny];
            rects = new Rect[nx * ny];
            start = new float[nx * ny];
            ready = new float[nx * ny];
            for (int j = 0; j < ny; j++)
                for (int i = 0; i < nx; i++)
                {
                    int k = j * nx + i;
                    var bl = new Vector2(Rect.xMin + i * ts, Rect.yMin + j * ts);
                    rects[k] = new Rect(bl.x + ts * 0.15f, bl.y + ts * 0.1f, ts * 0.7f, ts * 0.7f);
                    start[k] = -1f;
                    if (frames.Length == 0) continue;
                    tiles[k] = TrapArt.MakeRenderer(Ctx.Host.transform, "Spike " + Name + " " + k, bl, Ctx.Player, -6);
                    tiles[k].sprite = frames[0];
                }
            trigger = new TrapListener(Str("trigger", ""));
            disabler = new TrapListener(Str("disable_on", ""));
        }

        /// <summary>Thứ tự ô trên trục sóng (0 = ô đầu tiên chạy).</summary>
        private int WaveIndex(int k)
        {
            int i = k % nx, j = k / nx;
            switch (Str("wave_axis", "x"))
            {
                case "-x": return nx - 1 - i;
                case "y": return ny - 1 - j;          // y Tiled hướng xuống: "y" = chạy từ bắc xuống nam
                case "-y": return j;
                default: return i;
            }
        }

        /// <summary>Pha + thời gian trong pha của ô k. pha 0 nghỉ, 1 báo, 2 đâm, 3 thụt.</summary>
        private int Phase(int k, out float e)
        {
            string mode = Str("mode", "cycle");
            float warn = Warn, on = On;
            if (mode == "cycle")
            {
                float off = Float("off_seconds", 2f);
                float t = Mathf.Repeat(Time.time + Float("offset", 0f) - Float("wave_step", 0f) * WaveIndex(k), off + warn + on);
                if (t < off) { e = t; return t < Retract ? 3 : 0; }
                if (t < off + warn) { e = t - off; return 1; }
                e = t - off - warn; return 2;
            }
            if (start[k] < 0f) { e = 0f; return 0; }
            e = Time.time - start[k];
            if (e < 0f) return 0;
            if (e < warn) return 1;
            if (e < warn + on) { e -= warn; return 2; }
            if (e < warn + on + Retract) { e -= warn + on; return 3; }
            start[k] = -1f;
            ready[k] = Time.time + Float("cooldown", 0.6f);
            e = 0f; return 0;
        }

        private bool Armed()
        {
            string w = Str("start_when", "");
            return string.IsNullOrEmpty(w) || Ctx.Flags.HasAll(w);
        }

        public override void Tick(Vector2 playerPos, bool inside)
        {
            if (disabler.Poll()) disabledUntil = Time.time + Float("disable_seconds", 6f);
            bool fired = trigger.Poll();
            if (!Armed() || Time.time < disabledUntil) { ShowAll(0); return; }
            string mode = Str("mode", "cycle");
            if (fired && mode == "trigger")
                for (int k = 0; k < start.Length; k++)
                    if (start[k] < 0f) start[k] = Time.time + Float("wave_step", 0f) * WaveIndex(k);

            if (Time.time >= nextScan) { nextScan = Time.time + 0.1f; TrapArt.EnemiesIn(Rect, found); }

            float now = Time.time;
            bool hurtPlayer = false;
            for (int k = 0; k < rects.Length; k++)
            {
                if (mode == "step" && start[k] < 0f && now >= ready[k])
                {
                    bool stepped = rects[k].Contains(playerPos);
                    for (int f = 0; !stepped && f < found.Count; f++)
                        stepped = found[f] != null && rects[k].Contains((Vector2)found[f].transform.position);
                    if (stepped) start[k] = now;
                }
                int p = Phase(k, out float e);
                ShowFrame(k, p, e);
                if (p != 2) continue;
                if (rects[k].Contains(playerPos)) hurtPlayer = true;
                for (int f = 0; f < found.Count; f++)
                {
                    var h = found[f];
                    if (h == null || h.IsDead || !rects[k].Contains((Vector2)h.transform.position)) continue;
                    if (enemyHitAt.TryGetValue(h, out float t) && now < t) continue;
                    enemyHitAt[h] = now + 0.6f;
                    h.TakeDamage(Float("enemy_damage", 25f), Vector2.up, Float("knock", 2f));
                }
            }
            if (hurtPlayer && now >= nextHurt)
            {
                nextHurt = now + Mathf.Max(0.5f, Ctx.DamageTick);
                TrapArt.HurtPlayer(Ctx, Float("damage", 10f), Vector2.up, Float("knock", 2f));
            }
        }

        private void ShowFrame(int k, int phase, float e)
        {
            if (tiles[k] == null) return;
            int n = frames.Length, f;
            switch (phase)
            {
                case 1: f = 1 + (int)(e * 8f) % 2; break;                                   // ló đầu, rung rung
                case 2: f = Mathf.Min(n - 1, 3 + (int)(e / 0.02f)); break;                 // đâm lên rất nhanh rồi giữ
                case 3: f = Mathf.Clamp(n - 1 - (int)(e / Retract * (n - 1)), 0, n - 1); break;
                default: f = 0; break;
            }
            tiles[k].sprite = frames[Mathf.Clamp(f, 0, n - 1)];
        }

        private void ShowAll(int f)
        {
            if (tiles == null || frames.Length == 0) return;
            foreach (var t in tiles) if (t != null) t.sprite = frames[Mathf.Clamp(f, 0, frames.Length - 1)];
            if (start != null) for (int k = 0; k < start.Length; k++) start[k] = -1f;
        }

        protected override void OnStopped() => ShowAll(0);

        internal override void DebugSolve() { }
    }
}
