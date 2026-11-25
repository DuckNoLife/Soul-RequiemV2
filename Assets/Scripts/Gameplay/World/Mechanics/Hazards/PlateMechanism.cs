using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// plate - TRAP TRIGGER ZONE (pressure plate). Anyone standing in the zone fires signal pulse `fires` after `delay` seconds
    /// ("a,b"; default = object name) to every trap whose `trigger` matches (see <see cref="TrapSignals"/>).
    ///   by       any (default: both the player and enemies -> lure / drag enemies onto the plate) | player | enemy
    ///   rearm    reload seconds after each pulse (default 3)        once = true: fire once and stop
    ///   sets     flag set for good on the first pulse (e.g. open a gate)      start_when: only active once the flags are set
    ///   sprite   animation frames in Resources/VFX (trap_plate_h / trap_plate_v): the plate is CLEARLY VISIBLE and sinks when pressed.
    ///            Empty = HIDDEN plate (the map places its own clues around it: bones, blood stains, stuck arrows...).
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

            if (pressed && fireAt < 0f && Time.time >= readyAt) { fireAt = Time.time + Float("delay", 0f); SoulRequiem.Core.Sfx.Play("trap_plate", Rect.center); }   // sound
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
