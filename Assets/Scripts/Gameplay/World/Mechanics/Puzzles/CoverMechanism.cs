using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// cover - FLIPPABLE TABLE used as a shield (Old Prison interactive table, Enter the Gungeon style). Object area = where the table stands
    /// (collider blocks walking). Standing table: bullets fly over it (LOW collider, <see cref="TrapLowCover"/>). Press T "Flip": the table flips face-down
    /// toward the north into a bullet shield; after `hits` shots (default 6) it breaks through 3 stages, then shatters (no more blocking).
    /// The counter to shooters / Choir archers. `text`: line shown when flipped.
    /// </summary>
    public sealed class CoverMechanism : Mechanism
    {
        private Sprite[] flip, broken;
        private SpriteRenderer sr;
        private BoxCollider2D col;
        private bool flipped;
        private float shown;
        private int taken, stage = -1;
        private readonly HashSet<Projectile> counted = new HashSet<Projectile>();

        public override string DefaultVerb => "Flip";
        public override bool Usable => !flipped && !Done;

        public override void Init()
        {
            flip = TrapArt.Frames("trap_table_up");
            broken = TrapArt.Frames("trap_table_up_break");
            if (flip.Length > 0)
            {
                sr = TrapArt.MakeRenderer(Ctx.Host.transform, "Cover " + Name, new Vector3(Rect.center.x, Rect.yMin, 0f), Ctx.Player, 0);
                sr.sprite = flip[0];
            }
            var go = new GameObject("CoverCollider " + Name);
            go.transform.SetParent(Ctx.Host.transform, true);
            go.transform.position = Rect.center;
            col = go.AddComponent<BoxCollider2D>();
            col.size = Rect.size;
            go.AddComponent<TrapLowCover>();
        }

        public override void Use()
        {
            if (flipped) return;
            flipped = true;
            var low = col != null ? col.GetComponent<TrapLowCover>() : null;
            if (low != null) Object.Destroy(low);
            string t = Str("text", "");
            if (t.Length > 0) Hints.Show(t, 2.5f);
        }

        public override void Animate()
        {
            if (sr == null) return;
            if (flipped && stage < 0)
            {
                shown = Mathf.MoveTowards(shown, flip.Length - 1, Time.deltaTime * 18f);
                sr.sprite = flip[Mathf.RoundToInt(shown)];
            }
            if (!flipped || Done) return;
            var area = new Rect(Rect.xMin - 0.12f, Rect.yMin - 0.12f, Rect.width + 0.24f, Rect.height + 0.4f);
            foreach (var p in Projectile.Live)
            {
                if (p == null || !p.Exploding || counted.Contains(p)) continue;
                if (!area.Contains((Vector2)p.transform.position)) continue;
                counted.Add(p);
                taken++;
            }
            counted.RemoveWhere(p => p == null);
            int hits = Mathf.Max(3, Int("hits", 6));
            int st = taken >= hits ? 2 : taken >= hits * 2 / 3 ? 1 : taken >= hits / 3 ? 0 : -1;
            if (st > stage && broken.Length > 0)
            {
                stage = st;
                sr.sprite = broken[Mathf.Min(st, broken.Length - 1)];
                if (st >= 2) { Done = true; if (col != null) col.enabled = false; }
            }
        }

        internal override void DebugSolve() { }
    }
}
