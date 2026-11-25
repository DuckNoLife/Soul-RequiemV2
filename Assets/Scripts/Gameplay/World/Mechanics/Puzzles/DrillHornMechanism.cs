using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// drill_horn — BRASS HORN on a post in the drill yard: Ren presses T ("Sound the horn") -> start / replay the minigame of the DrillYardMechanism named
    /// `yard` (default "drill_yard"). Draws VFX/props/drill_horn (image foot at the bottom of the object area) + a small collider at its base.
    /// One horn per yard (station) — blowing it = start / replay that yard's station FROM THE START.
    /// Blowing = military bugle call (sound `drill_horn`: 4 brass notes ringing across the stone yard) + the horn shakes 1.4 s; the minigame waits 1.1 s
    /// for the call before the station starts.
    /// </summary>
    public sealed class DrillHornMechanism : Mechanism
    {
        private DrillYardMechanism yard;
        private SpriteRenderer sr;

        public override string DefaultVerb => "Sound the horn";
        /// <summary>Name of the yard (drill_yard) this horn calls — 3 yards share the scene, one horn each.</summary>
        public string YardName => Str("yard", "drill_yard");
        public override bool Usable => Yard != null && (Yard.Ready || Yard.Passed);

        private DrillYardMechanism Yard
        {
            get
            {
                if (yard != null) return yard;
                string n = YardName;
                foreach (var m in Ctx.All) if (m is DrillYardMechanism d && d.Name == n) { yard = d; break; }
                return yard;
            }
        }

        public override void Init()
        {
            sr = TrapArt.MakeRenderer(Ctx.Host.transform, "Drill Horn", new Vector3(Rect.center.x, Rect.yMin, 0f), Ctx.Player, 0);
            sr.sprite = WardenStatueMechanism.Load("VFX/props/drill_horn");
            var go = new GameObject("DrillHornCollider");
            go.transform.SetParent(Ctx.Host.transform, true);
            go.transform.position = new Vector3(Rect.center.x, Rect.yMin + 0.06f, 0f);
            go.AddComponent<BoxCollider2D>().size = new Vector2(0.22f, 0.12f);
        }

        public override void Use()
        {
            var y = Yard;
            if (y == null) return;
            if (y.Passed) { Hints.Show("The horn is silent. This station is already passed.", 2f); return; }
            if (!y.Ready) return;
            Sfx.Play("drill_horn", Rect.center);                                     // bugle call
            Ctx.Run(Toot());
            y.Begin();
        }

        private System.Collections.IEnumerator Toot()
        {
            if (sr == null) yield break;
            const float Len = 1.4f;                                                    // shake with the call (longer)
            for (float t = 0f; t < Len; t += Time.deltaTime)
            {
                float k = Mathf.Sin(t * 40f) * 0.05f * (1f - t / Len);
                sr.transform.localScale = new Vector3(1f + k, 1f - k, 1f);
                yield return null;
            }
            sr.transform.localScale = Vector3.one;
        }

        internal override void DebugSolve() { }
    }
}
