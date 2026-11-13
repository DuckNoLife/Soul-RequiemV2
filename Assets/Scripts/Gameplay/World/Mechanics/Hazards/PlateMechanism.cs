using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 08/11 (§196): plate — VÙNG KÍCH BẪY (tấm sàn). Ai đứng vào vùng thì sau `delay` giây phát xung tín hiệu `fires`
    /// ("a,b"; mặc định = tên object) cho mọi bẫy có `trigger` trùng tên (xem <see cref="TrapSignals"/>).
    ///   by       any (mặc định: cả người chơi lẫn quái -> dụ / kéo quái vào tấm sàn) | player | enemy
    ///   rearm    giây nạp lại sau mỗi lần phát (mặc định 3)        once = true: phát một lần rồi thôi
    ///   sets     cờ bật vĩnh viễn khi phát lần đầu (vd mở cổng)      start_when: chỉ hoạt động khi đủ cờ
    ///   sprite   khung hoạt ảnh trong Resources/VFX (trap_plate_h / trap_plate_v): tấm HIỆN RÕ, lún xuống khi bị đè.
    ///            Để trống = tấm ẨN (map tự đặt dấu hiệu quanh: xương, vết máu, tên cắm...).
    /// </summary>
    public sealed class PlateMechanism : Mechanism
    {
        private SpriteRenderer sr;
        private Sprite[] frames;
        private float shown;
        private bool pressed;
        private float readyAt, fireAt = -1f, nextScan;
        private bool enemyOn;
        private readonly List<Health> found = new List<Health>();

        public override void Init()
        {
            frames = TrapArt.Frames(Str("sprite", ""));
            if (frames.Length == 0) return;
            var f0 = frames[0];
            var bl = new Vector3(Rect.center.x - f0.bounds.size.x / 2f, Rect.center.y - f0.bounds.size.y / 2f, 0f);
            sr = TrapArt.MakeRenderer(Ctx.Host.transform, "Plate " + Name, bl, Ctx.Player, -6);
            sr.sprite = f0;
        }

        private bool Armed()
        {
            string w = Str("start_when", "");
            return string.IsNullOrEmpty(w) || Ctx.Flags.HasAll(w);
        }

        public override void Tick(Vector2 playerPos, bool inside)
        {
            if (!Armed()) { pressed = false; return; }
            string by = Str("by", "any");
            if (by != "player" && Time.time >= nextScan)
            {
                nextScan = Time.time + 0.1f;
                TrapArt.EnemiesIn(Rect, found);
                enemyOn = found.Count > 0;
            }
            bool playerOn = inside && by != "enemy";
            pressed = playerOn || (by != "player" && enemyOn);

            if (pressed && fireAt < 0f && Time.time >= readyAt) fireAt = Time.time + Float("delay", 0f);
            if (fireAt >= 0f && Time.time >= fireAt)
            {
                fireAt = -1f;
                TrapSignals.Fire(Str("fires", Name));
                string sets = Str("sets", "");
                if (sets.Length > 0 && !Ctx.Flags.Has(sets)) Ctx.Flags.Set(sets);
                readyAt = Time.time + Float("rearm", 3f);
                if (Props.GetBool("once", false)) Done = true;
            }
        }

        public override void Animate()
        {
            if (sr == null || frames.Length < 2) return;
            float target = pressed || Done ? frames.Length - 1 : 0f;
            shown = Mathf.MoveTowards(shown, target, Time.deltaTime * 24f);
            sr.sprite = frames[Mathf.Clamp(Mathf.RoundToInt(shown), 0, frames.Length - 1)];
        }

        internal override void DebugSolve() { }
    }
}
