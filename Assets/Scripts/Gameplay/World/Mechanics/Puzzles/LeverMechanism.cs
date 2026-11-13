using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// lever — cần gạt (nhấn T, một lần): cờ `sets` + chữ `text`. `spawn` ("Quai|Quai") xuất hiện tại `spawn_at` (ô Tiled)
    /// trong khu `zone` (vd mở nhầm buồng giam). `hint` hiện khi lại gần.
    /// 08/11 (§196): `sprite` = dải khung Resources/VFX (trap_gear = cần quay bánh răng, trap_plate_wall = nút tường) đặt giữa mép dưới
    /// vùng, chạy khi gạt. `fires` = phát xung tín hiệu cho bẫy (<see cref="TrapSignals"/>, vd tắt sân gai bằng `disable_on`);
    /// có `rearm` (giây) thì gạt lại được (cần quay về chỗ cũ sau khi nạp); có `fires` mà không ghi `sets` thì không bật cờ.
    /// 09/11 (§200): `requires_key` (id chìa trong túi đồ) + `locked_say` (Ren nói khi chưa có chìa) — vd khoá bình mẫu vật Specimen Cells.
    /// Nạp bản lưu: đã có cờ `sets` (không có rearm) -> Done (không thả quái lần nữa).
    /// </summary>
    public sealed class LeverMechanism : Mechanism
    {
        private bool announced;
        private SpriteRenderer sr;
        private Sprite[] frames;
        private float shown, target, readyAt;

        public override string DefaultVerb => "Pull";
        public override bool Usable => !Done && Time.time >= readyAt;

        public override void Init()
        {
            string done = Str("sets", "");
            if (done.Length > 0 && Float("rearm", 0f) <= 0f && Ctx.Flags.HasAll(done)) Done = true;   // 09/11 (§200)
            frames = TrapArt.Frames(Str("sprite", ""));
            if (frames.Length == 0) return;
            var f0 = frames[0];
            var bl = new Vector3(Rect.center.x - f0.bounds.size.x / 2f, Rect.yMin, 0f);
            sr = TrapArt.MakeRenderer(Ctx.Host.transform, "Lever " + Name, bl, Ctx.Player, 0);
            sr.sprite = f0;
        }

        public override void Animate()
        {
            if (sr == null || frames.Length < 2) return;
            if (readyAt > 0f && Time.time >= readyAt && !Done) target = 0f;     // nạp xong: cần quay về
            shown = Mathf.MoveTowards(shown, target, Time.deltaTime * 16f);
            sr.sprite = frames[Mathf.Clamp(Mathf.RoundToInt(shown), 0, frames.Length - 1)];
        }

        public override void Tick(Vector2 playerPos, bool inside)
        {
            bool near = Vector2.Distance(playerPos, Rect.center) < 0.7f;
            if (near && !announced) { announced = true; Hints.Show(Str("hint", ""), 3f); }
            if (!near) announced = false;
        }

        public override void Use()
        {
            if (Done || Time.time < readyAt) return;
            string key = Str("requires_key", "");                       // 09/11 (§200)
            if (key.Length > 0 && !KeyRing.Has(Ctx.Flags, key))
            {
                KeyRing.Say(Ctx.Director != null ? Ctx.Director.Player : null, Str("locked_say", "Locked tight. I need a key."));
                return;
            }
            if (key.Length > 0) Ctx.Flags.Set(KeyRing.UsedFlag(key));
            target = frames != null && frames.Length > 0 ? frames.Length - 1 : 0f;
            string fires = Str("fires", "");
            float rearm = Float("rearm", 0f);
            if (fires.Length > 0) TrapSignals.Fire(fires);
            if (rearm > 0f) readyAt = Time.time + rearm;
            else Done = true;
            Hints.Show(Str("text", "The lever grinds down."), 3f);
            string sets = Str("sets", fires.Length > 0 ? "" : Name);
            if (sets.Length > 0) Ctx.Flags.Set(sets);
            string spawn = Str("spawn", "");
            if (string.IsNullOrEmpty(spawn)) return;
            Vector3 at = Ctx.TilePoint(Str("spawn_at", ""), Rect.center);
            foreach (var en in spawn.Split('|')) Ctx.Director.SpawnInZone(en.Trim(), at, Str("zone", ""));
        }
    }
}
