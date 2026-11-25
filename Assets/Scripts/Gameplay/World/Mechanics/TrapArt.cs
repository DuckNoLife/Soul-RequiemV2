using System.Collections.Generic;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Shared helpers for animated traps / gates: load frame strip Resources/VFX/&lt;name&gt; (sliced with
    /// Catalog/v2/tools/traps196/slice_traps.py, bottom-left pivot, frame 0 = rest / closed state), create a SpriteRenderer on the
    /// character layer, find enemies standing in an area.
    /// </summary>
    public static class TrapArt
    {
        private static readonly Dictionary<string, Sprite[]> cache = new Dictionary<string, Sprite[]>();

        /// <summary>Frames in numeric suffix order (&lt;name&gt;_0, _1, ...). Empty if there are no images.</summary>
        public static Sprite[] Frames(string name)
        {
            if (string.IsNullOrEmpty(name)) return new Sprite[0];
            if (cache.TryGetValue(name, out var f) && f != null && f.Length > 0 && f[0] != null) return f;
            var all = Resources.LoadAll<Sprite>("VFX/" + name);
            var list = new List<Sprite>(all);
            list.Sort((a, b) => Suffix(a.name).CompareTo(Suffix(b.name)));
            f = list.ToArray();
            if (f.Length == 0) Debug.LogWarning("[TrapArt] frames not found Resources/VFX/" + name);
            cache[name] = f;
            return f;
        }

        private static int Suffix(string n)
        {
            int i = n.LastIndexOf('_');
            return i >= 0 && int.TryParse(n.Substring(i + 1), out int v) ? v : 0;
        }

        /// <summary>New SpriteRenderer, same sorting layer + material as characters; negative orderOffset = below the feet (floor traps).</summary>
        public static SpriteRenderer MakeRenderer(Transform parent, string name, Vector3 bottomLeft, Transform player, int orderOffset)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, true);
            go.transform.position = bottomLeft;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.spriteSortPoint = SpriteSortPoint.Pivot;
            var psr = player != null ? player.GetComponentInChildren<SpriteRenderer>() : null;
            if (psr != null)
            {
                sr.sortingLayerID = psr.sortingLayerID;
                sr.sortingOrder = psr.sortingOrder + orderOffset;
                sr.sharedMaterial = psr.sharedMaterial;
            }
            return sr;
        }

        private static readonly List<Collider2D> buf = new List<Collider2D>();
        private static ContactFilter2D filter;
        private static bool filterReady;

        /// <summary>Enemies (living Health that is not the player) whose collider touches the area.</summary>
        public static void EnemiesIn(Rect r, List<Health> into)
        {
            into.Clear();
            if (!filterReady) { filter = new ContactFilter2D(); filter.useTriggers = true; filter.useLayerMask = false; filterReady = true; }
            buf.Clear();
            Physics2D.OverlapBox(r.center, r.size, 0f, filter, buf);
            for (int i = 0; i < buf.Count; i++)
            {
                var h = buf[i] != null ? buf[i].GetComponentInParent<Health>() : null;
                if (h == null || h.IsPlayer || h.IsDead || into.Contains(h)) continue;
                into.Add(h);
            }
        }

        /// <summary>Deal trap damage to the player: counts as a ranged hit (Riposte cannot parry traps, Veil still reduces it).</summary>
        public static void HurtPlayer(MechanismContext ctx, float amount, Vector2 dir, float knock)
        {
            Health.IncomingRanged = true;
            try { ctx.HurtPlayer(amount, dir, knock); }
            finally { Health.IncomingRanged = false; }
        }

        /// <summary>MECHANICAL traps (floor spikes, rolling logs, saw blades, water jets) - like HurtPlayer but rolling does not dodge them
        /// (roll invulnerability is ignored; shields / cutscenes still block). Shot arrows still use HurtPlayer (rolling past arrows makes sense).</summary>
        public static void HurtPlayerCrush(MechanismContext ctx, float amount, Vector2 dir, float knock)
        {
            Health.IncomingRanged = true; Health.TrapIgnoresRoll = true;
            try { ctx.HurtPlayer(amount, dir, knock); }
            finally { Health.IncomingRanged = false; Health.TrapIgnoresRoll = false; }
        }
    }
}
