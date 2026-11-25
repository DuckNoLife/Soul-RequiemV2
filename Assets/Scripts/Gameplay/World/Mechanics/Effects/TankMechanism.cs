using System.Collections;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// tank - glass tank (Culture Hall): the Gorgemaw toad slams down nearby (<see cref="MapMechanics.Shatter"/>) -> hides `obj`, shows `broken`.
    /// Tank broken -> flag `sets` (default &lt;name&gt;_broken) and Ichor spills into pool `spill` (Tiled object, spreading from the tank base).
    /// The matching stinging zone = a hazard mechanism with `start_when` = that flag (hidden / harmless until the tank breaks). The Ichor pool is also where the toadlets rise
    /// in phase 2 (<see cref="SpillPoint"/>). Reloading a save with the flag: already broken, pool already showing.
    /// </summary>
    public sealed class TankMechanism : Mechanism
    {
        public override void Init()
        {
            Ctx.Show(Str("broken", ""), false);
            Ctx.Show(Str("spill", ""), false);
            OnFlagsChanged();
        }

        public override void OnFlagsChanged()
        {
            if (Done || !Ctx.Flags.Has(Flag)) return;
            Done = true;                                            // save / flag set elsewhere: already broken, no effect
            Ctx.Show(Str("obj", ""), false);
            Ctx.Show(Str("broken", ""), true);
            Ctx.Show(Str("spill", ""), true);
        }

        private string Flag => Str("sets", Name + "_broken");

        /// <summary>Centre of the Ichor pool (where the toadlets rise) - centre of the `spill` image if any, otherwise the tank area centre.</summary>
        internal Vector2 SpillPoint
        {
            get
            {
                foreach (var go in Ctx.NamedObjects(Str("spill", "")))
                {
                    var r = go != null ? go.GetComponentInChildren<SpriteRenderer>(true) : null;
                    if (r != null) return r.bounds.center;
                }
                return Rect.center;
            }
        }

        /// <summary>Break if the slam (centre at, radius r) touches the tank. Returns true if it broke.</summary>
        internal bool TryShatter(Vector2 at, float r)
        {
            if (Done || !Overlaps(at, r)) return false;
            Done = true;
            SfxHooks.MechanismEvent(this, "break", "glass_break"); // sound
            Ctx.Show(Str("obj", ""), false);
            Ctx.Show(Str("broken", ""), true);
            Ctx.Run(Spill());
            return true;
        }

        /// <summary>Ichor spills: the pool spreads from the tank base (shrink -> grow), the flag is set once it has spread enough (the stinging zone starts biting).</summary>
        private IEnumerator Spill()
        {
            // Tiled tile objects have their origin at the bottom-left -> scale around the image CENTRE (offset the position) so the pool spreads evenly in every direction
            var list = Ctx.NamedObjects(Str("spill", ""));
            int n = list.Count;
            var scales = new Vector3[n]; var pos = new Vector3[n]; var off = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                if (list[i] == null) continue;
                list[i].SetActive(true);
                var tr = list[i].transform;
                scales[i] = tr.localScale; pos[i] = tr.position;
                var r = list[i].GetComponentInChildren<SpriteRenderer>(true);
                off[i] = r != null ? r.bounds.center - tr.position : Vector3.zero;
            }
            for (float t = 0f; t <= 0.7f; t += Time.deltaTime)
            {
                float k = Mathf.Lerp(0.2f, 1f, 1f - (1f - t / 0.7f) * (1f - t / 0.7f));
                for (int i = 0; i < n; i++)
                {
                    if (list[i] == null) continue;
                    var tr = list[i].transform;
                    tr.localScale = new Vector3(scales[i].x * k, scales[i].y * k, scales[i].z);
                    tr.position = pos[i] + off[i] * (1f - k);
                }
                if (t > 0.25f && !Ctx.Flags.Has(Flag)) Ctx.Flags.Set(Flag);
                yield return null;
            }
            for (int i = 0; i < n; i++)
                if (list[i] != null) { list[i].transform.localScale = scales[i]; list[i].transform.position = pos[i]; }
            if (!Ctx.Flags.Has(Flag)) Ctx.Flags.Set(Flag);
        }
    }
}
