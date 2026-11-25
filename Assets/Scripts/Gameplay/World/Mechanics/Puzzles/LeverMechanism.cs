using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// lever - a lever (press T, once): flag `sets` + text `text`. `spawn` ("Enemy|Enemy") appears at `spawn_at` (Tiled tile)
    /// in area `zone` (e.g. opening the wrong cell). `hint` shows when getting close.
    /// `sprite` = Resources/VFX frame strip (trap_gear = gear crank, trap_plate_wall = wall button) placed at the middle of the area's bottom edge,
    /// played when pulled. `fires` = sends a signal pulse to traps (<see cref="TrapSignals"/>, e.g. turning off a spike field with `disable_on`);
    /// with `rearm` (seconds) it can be pulled again (the lever returns to its rest position after loading); `fires` without `sets` sets no flag.
    /// `requires_key` (inventory key id) + `locked_say` (Ren's line without the key) - e.g. the specimen jar lock in the Specimen Cells.
    /// Loading a save: flag `sets` already set (no rearm) -> Done (no second enemy release).
    /// `requires` = list of flags needed before it can be pulled + `locked_say`.
    /// </summary>
    public sealed class LeverMechanism : Mechanism
    {
        private bool announced;
        private SpriteRenderer sr;
        private Sprite[] frames;
        private float shown, target, readyAt;

        public override string DefaultVerb => "Pull";
        public override bool Usable => !Hidden && !Done && Time.time >= readyAt;
        /// <summary>Another mechanism puts the lever away (image hidden, no T, no prompt) - e.g. the hay-bale launcher lever only appears at drill yard station 3.</summary>
        public bool Hidden { get; set; }

        public override void Init()
        {
            string done = Str("sets", "");
            if (done.Length > 0 && Float("rearm", 0f) <= 0f && Ctx.Flags.HasAll(done)) Done = true;
            frames = TrapArt.Frames(Str("sprite", ""));
            if (frames.Length == 0) return;
            var f0 = frames[0];
            var bl = new Vector3(Rect.center.x - f0.bounds.size.x / 2f, Rect.yMin, 0f);
            sr = TrapArt.MakeRenderer(Ctx.Host.transform, "Lever " + Name, bl, Ctx.Player, 0);
            sr.sprite = f0;
        }

        public override void Animate()
        {
            if (sr != null && sr.enabled == Hidden) sr.enabled = !Hidden;
            if (sr == null || frames.Length < 2) return;
            if (readyAt > 0f && Time.time >= readyAt && !Done) target = 0f;     // loaded: the lever returns
            shown = Mathf.MoveTowards(shown, target, Time.deltaTime * 16f);
            sr.sprite = frames[Mathf.Clamp(Mathf.RoundToInt(shown), 0, frames.Length - 1)];
        }

        public override void Tick(Vector2 playerPos, bool inside)
        {
            if (Hidden) { announced = false; return; }
            bool near = Vector2.Distance(playerPos, Rect.center) < 0.7f;
            if (near && !announced) { announced = true; Hints.Show(Str("hint", ""), 3f); }
            if (!near) announced = false;
        }

        public override void Use()
        {
            if (Done || Time.time < readyAt) return;
            // `requires` = flags needed (e.g. Pell's cell winch is wrapped in a chain: cut the chain first); missing -> Ren says `locked_say`
            if (!Ctx.Flags.HasAll(Str("requires", "")))
            {
                KeyRing.Say(Ctx.Director != null ? Ctx.Director.Player : null, Str("locked_say", "It won't move."));
                return;
            }
            string key = Str("requires_key", "");
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
