using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// warden_statue - STATUES OF PAST CHAMPIONS in The Warden Gallery (Map 4, Reliquary Wardens duo fight).
    /// Object area = the statue base (solid collider: blocks walking + straight bullets). Role in the fight:
    ///   • Cover: the Brazen Reliquary's Grapeshot (fired from the turret pedestal) breaks on statues -> Ren hides behind them. Every hit / nearby mortar
    ///     cracks the statue further (`hits`, default 6; `sprite_damaged` images getting worse) -> out of `hits` it breaks, leaving a low base (bullets fly over).
    ///   • Champion trap: the Hollow Champion charging (Rush / Beacon Charge) into a statue -> the statue shatters and he is STUNNED (HollowChampionSkills).
    /// Ren's slashes do not break statues (stone). Dying mid-fight / fight reset -> statues are rebuilt intact (ZoneReset).
    /// Properties: `sprite`, `sprite_damaged` (list), `sprite_broken` (empty base), `hits`.
    /// </summary>
    public sealed class WardenStatueMechanism : Mechanism
    {
        private SpriteRenderer sr;
        private BoxCollider2D col;
        private Sprite whole, broken;
        private readonly List<Sprite> damaged = new List<Sprite>();
        private readonly HashSet<Projectile> counted = new HashSet<Projectile>();
        private int taken;

        /// <summary>Still standing (blocks bullets).</summary>
        public bool Standing => !Done;
        public Vector2 Center => Rect.center;

        public override void Init()
        {
            whole = Load(Str("sprite", "VFX/props/warden_statue_0"));
            broken = Load(Str("sprite_broken", ""));
            foreach (var p in Str("sprite_damaged", "").Split(',')) { var s = Load(p.Trim()); if (s != null) damaged.Add(s); }
            sr = TrapArt.MakeRenderer(Ctx.Host.transform, "Statue " + Name, new Vector3(Rect.center.x, Rect.yMin + 0.02f, 0f), Ctx.Player, 0);
            sr.sprite = whole;
            var go = new GameObject("StatueCollider " + Name);
            go.transform.SetParent(Ctx.Host.transform, true);
            go.transform.position = Rect.center;
            col = go.AddComponent<BoxCollider2D>();
            col.size = Rect.size;
            MapEncounterDirector.ZoneReset += OnZoneReset;
        }

        internal static Sprite Load(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            var all = Resources.LoadAll<Sprite>(path);
            if (all.Length == 0) { Debug.LogWarning("[WardenStatue] Sprite not found: " + path); return null; }
            return all[0];
        }

        public override void Animate()
        {
            if (Done) return;
            // a bullet bursting next to the statue = hits the statue (like the CoverMechanism flipped table)
            var area = new Rect(Rect.xMin - 0.1f, Rect.yMin - 0.1f, Rect.width + 0.2f, Rect.height + 0.3f);
            foreach (var p in Projectile.Live)
            {
                if (p == null || !p.Exploding || counted.Contains(p)) continue;
                if (!area.Contains((Vector2)p.transform.position)) continue;
                counted.Add(p);
                Chip(1);
            }
            counted.RemoveWhere(p => p == null);
        }

        /// <summary>Crack `n` more steps (bullets, nearby mortars). Out of steps -> breaks.</summary>
        public void Chip(int n)
        {
            if (Done) return;
            taken += n;
            int hits = Mathf.Max(2, Int("hits", 6));
            if (taken >= hits) { Shatter(); return; }
            Ctx.Run(Jiggle(0.12f, 0.02f));
            if (damaged.Count > 0)
            {
                int idx = Mathf.Clamp(taken * (damaged.Count + 1) / hits - 1, -1, damaged.Count - 1);
                if (idx >= 0 && sr != null) sr.sprite = damaged[idx];
            }
            Sfx.Play("break_stone", Rect.center, 0.35f);
        }

        /// <summary>Shatter (Champion charge / out of steps): dust + rock shards, only a low base remains (bullets fly over).</summary>
        public void Shatter()
        {
            if (Done) return;
            Done = true;
            Sfx.Play("statue_shatter", Rect.center);
            var psr = Ctx.Player != null ? Ctx.Player.GetComponentInChildren<SpriteRenderer>() : null;
            if (psr != null) CutsceneFx.Burst(Ctx.Host, new Vector3(Rect.center.x, Rect.center.y + 0.3f, 0f), 0.4f, 0.35f, 7, 0, psr.sortingLayerID, psr.sortingOrder + 1);
            SpriteVfx.Spawn("m3_ground_burst", Rect.center, 1.1f, sr != null ? sr.sortingLayerID : 0, (sr != null ? sr.sortingOrder : 0) + 1, false, 16f, false);
            CameraFollow.KickMain(Vector2.down, 0.06f, 0.22f);
            if (sr != null)
            {
                if (broken != null) sr.sprite = broken; else sr.enabled = false;
            }
            if (col != null && col.GetComponent<TrapLowCover>() == null) col.gameObject.AddComponent<TrapLowCover>();   // low base: bullets fly over
        }

        private void Restore()
        {
            Done = false; taken = 0; counted.Clear();
            if (sr != null) { sr.enabled = true; sr.sprite = whole; }
            var low = col != null ? col.GetComponent<TrapLowCover>() : null;
            if (low != null) Object.Destroy(low);
        }

        private void OnZoneReset(Rect zone)
        {
            if (Ctx.Host == null) { MapEncounterDirector.ZoneReset -= OnZoneReset; return; }
            if (zone.Contains(Rect.center)) Restore();
        }

        private IEnumerator Jiggle(float seconds, float amp)
        {
            if (sr == null) yield break;
            var t = sr.transform; Vector3 p0 = new Vector3(Rect.center.x, Rect.yMin + 0.02f, 0f);
            for (float e = 0f; e < seconds && t != null; e += Time.deltaTime)
            {
                t.position = p0 + new Vector3(Mathf.Sin(e * 90f) * amp * (1f - e / seconds), 0f, 0f);
                yield return null;
            }
            if (t != null) t.position = p0;
        }

        internal override void DebugSolve() { }
    }
}
