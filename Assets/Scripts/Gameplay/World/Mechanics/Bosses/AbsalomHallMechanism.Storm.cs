using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// STORM school (violet-white) — GEOMETRY: read the shape, then stand in the right spot.
    ///   Tempest Lattice — 4 lightning rods drop around Ren (rods rising = 1.1 s warning), then bolts link rods 1-2-3-4 into an OPEN chain for 1.6 s:
    ///                     crossing a bolt = shock. Dodge: leave through the open side before the bolts fire (or stand still where no bolt runs).
    ///   Thunderclap     — 3–5 concentric rings around Absalom blink for 1 s, then dense sky lightning strikes EACH RING from inside out (0.5 s apart).
    ///                     Dodge: stand in the gap between two rings, or step into a ring that was just struck (follow the wave).
    /// Visuals: each rod lands as a lightning strike (abs_zap2 94 px + violet light flash) with CRACKLING sparks around it (abs_crackle) + violet light;
    ///   the links are 3-layer bolts (glow / body / white core, Bolt); when they fire: shake 2 + light flash + sparks at every rod, then random
    ///   re-flashes at both ends for 1.6 s.
    /// Called by: ElementSkill / PressureSkill (.Lamps.cs) and the Ctrl+F7 showcase. Uses: Bolt, Hazard, RingSprite, FlashLight (.Skills.cs).
    /// </summary>
    public sealed partial class AbsalomHallMechanism
    {
        private readonly List<GameObject> rods = new List<GameObject>();
        private readonly List<Bolt> arcs = new List<Bolt>();

        private void ClearRods()
        {
            foreach (var r in rods) if (r != null) Object.Destroy(r);
            rods.Clear();
            foreach (var b in arcs) KillBolt(b);
            arcs.Clear();
        }

        private GameObject PlantRod(Vector2 at)
        {
            var go = new GameObject("Storm Rod");
            go.transform.SetParent(Ctx.Host.transform, true);
            go.transform.position = at;
            var sr = go.AddComponent<SpriteRenderer>();
            var f = SpriteVfx.Load("abs_storm_rod");
            if (f.Length > 0) sr.sprite = f[0];
            sr.sortingLayerID = Layer;
            sr.sortingOrder = Order - (at.y > PlayerFeet.y ? 1 : -1);
            sr.spriteSortPoint = SpriteSortPoint.Pivot;
            var arcFx = SpriteVfx.Spawn("abs_crackle", at + Vector2.up * 0.3f, 1.1f, Layer, Order + 2, false, 14f, false);
            if (arcFx == null) arcFx = SpriteVfx.Spawn("abs_storm_arcs", at + Vector2.up * 0.38f, 1f, Layer, Order + 2, false, 12f, false);
            if (arcFx != null) { arcFx.Looping(999f); arcFx.transform.SetParent(go.transform, true); }
            var glow = GlowAt(at + Vector2.up * 0.38f, Storm, 1.1f, 0.8f, 0f);          // rods glow violet
            glow.transform.SetParent(go.transform, true);
            rods.Add(go);
            return go;
        }

        private IEnumerator TempestLattice(int power)
        {
            yield return Cast(School.Storm, "TEMPEST LATTICE", 0.45f, "lg_charge");
            if (aborted) yield break;
            Vector2 c = PlayerFeet;
            int n = power >= 2 ? 4 : 3;
            float a0 = Random.Range(0f, 360f), rad = power >= 2 ? 1.25f : 1.45f;
            var pts = new List<Vector2>();
            for (int i = 0; i < n; i++)
            {
                float ang = a0 + i * (power >= 2 ? 82f : 105f) + Random.Range(-8f, 8f);       // OPEN chain: total < 360° -> there is always an exit side
                pts.Add(InArena(c + Rot(Vector2.right, ang) * rad * Random.Range(0.9f, 1.1f), 0.3f));
            }
            foreach (var p in pts)
            {
                // a rod lands = a small bolt from the sky + a big spark burst at its base
                Grounded("abs_sky_bolt", p, 0.6f, 30f, false, 0f);
                Fx("abs_zap2", p + Vector2.up * 0.25f, 1f, Order + 3, 18f, Color.white);
                FlashLight(p, Storm, 1.8f, 1.6f, 0.25f);
                PlantRod(p);
                Sfx.Play("lg_spear_stuck", p, 0.5f);
                yield return Wait(0.12f);
            }
            // warning: faint lines between rods
            var tels = new List<DashTelegraph>();
            for (int i = 0; i + 1 < pts.Count; i++)
                tels.Add(DashTelegraph.Spawn(pts[i], (pts[i + 1] - pts[i]).normalized, (pts[i + 1] - pts[i]).magnitude, 0.14f, Storm, 1.1f, Layer, Order - 2));
            yield return Wait(1.1f);
            foreach (var t in tels) if (t != null) t.Release();
            if (aborted) yield break;
            Sfx.Play("bolt_strike", c, 0.9f);
            Shake(2);
            FlashLight(c, Storm, 3.4f, 2f, 0.35f);
            foreach (var p in pts) Fx("abs_zap2", p + Vector2.up * 0.38f, 1.1f, Order + 3, 20f, Color.white);
            var hz = new List<Hazard>();
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                Vector2 a = pts[i] + Vector2.up * 0.38f, b = pts[i + 1] + Vector2.up * 0.38f;
                arcs.Add(MakeBolt(a, b, new Color(0.92f, 0.9f, 1f, 0.95f)));
                var h = AddHazard(HazardShape.Segment, pts[i], pts[i + 1], 0f, 0.13f, 9f, 0f, 1.6f, 0.6f);
                hz.Add(h);
            }
            float end = Time.time + 1.6f;
            while (!aborted && Time.time < end)
            {
                foreach (var b in arcs) JitterBolt(b, false);
                if (Random.value < 0.08f)
                {
                    Vector2 q = pts[Random.Range(0, pts.Count)];
                    Sfx.Play("bolt_strike", q, 0.25f);
                    Fx("abs_zap2", q + Vector2.up * 0.38f, 0.8f, Order + 3, 22f, Color.white);  // re-flash at the rod ends
                }
                yield return null;
            }
            ClearRods();
            abs.Play("idle", 5f);
        }

        /// <summary>THUNDERCLAP: Absalom flies higher with lightning crawling over him -> 3–5 concentric warning rings (thick, bright rim, blinking faster) ->
        /// each ring: dense LIGHTNING FROM ABOVE around the ring (abs_sky_bolt) -> 0.14 s later the ring flashes white + deals damage + violet flash over the hall + shake
        /// (outer rings shake harder). Rings 0.5 s apart — follow behind the wave or stand between two rings.</summary>
        private IEnumerator Thunderclap(int power)
        {
            var arcs0 = SpriteVfx.Spawn("abs_storm_arcs", abs.Body, 1.6f, Layer, Order + 2, false, 14f, false);
            if (arcs0 != null) arcs0.Looping(1.4f);
            liftBoost = 0.5f;
            yield return Cast(School.Storm, "THUNDERCLAP", 0.6f, "lg_charge");
            if (aborted) { liftBoost = 0f; yield break; }
            Vector2 c = abs.Pos;
            float[] radii = power >= 2 ? new[] { 1.1f, 2.2f, 3.3f, 4.4f, 5.5f } : new[] { 1.4f, 2.8f, 4.2f };
            const float Half = 0.32f;
            var marks = new List<SpriteRenderer>();
            var holder = AddHazard(HazardShape.Circle, c, c, 0f, 0f, 0f, 0f, 7f);              // keep the warning rings: fight reset -> ClearHazards removes them too
            foreach (var r in radii) { var m = RingMark(c, r, Half, new Color(Storm.r, Storm.g, Storm.b, 0.3f), 0f); marks.Add(m); holder.Objs.Add(m.gameObject); }
            Sfx.Play("lg_charge", c, 0.8f);
            float warn = 1.0f, t0 = Time.time;
            while (!aborted && Time.time - t0 < warn)
            {
                float u = (Time.time - t0) / warn;
                float a = 0.25f + 0.25f * u + 0.18f * Mathf.Sin(Time.time * Mathf.Lerp(10f, 28f, u));
                foreach (var m in marks) if (m != null) m.color = new Color(Storm.r, Storm.g, Storm.b, a);
                yield return null;
            }
            for (int k = 0; k < radii.Length && !aborted; k++)
            {
                float r = radii[k];
                int bolts = Mathf.Max(5, Mathf.RoundToInt(2f * Mathf.PI * r / 0.75f));
                float off = Random.Range(0f, 360f);
                for (int i = 0; i < bolts; i++)
                {
                    Vector2 p = c + Rot(Vector2.right, off + i * 360f / bolts) * r;
                    if (!A.Contains(p)) continue;
                    Grounded("abs_sky_bolt", p, Random.Range(0.75f, 0.95f), 28f, false, 0f);
                }
                Sfx.Play("bolt_strike", c + Vector2.right * r, 0.9f);
                yield return Wait(0.14f);                                               // frame where the bolt hits the ground (frame 4 / 28 fps)
                if (aborted) break;
                if (marks[k] != null) marks[k].color = new Color(1f, 1f, 1f, 0.95f);
                if (Mathf.Abs((PlayerFeet - c).magnitude - r) <= Half) HurtRen(11f, c, 1.2f);
                FlashLight(c, Storm, r + 1.5f, 1.3f, 0.28f);
                Shake(k >= radii.Length - 2 ? 2 : 1);
                int idx = k;
                Ctx.Run(FadeMark(marks[idx], 0.4f));
                yield return Wait(0.36f);
            }
            holder.Dead = true;
            liftBoost = 0f;
            abs.Play("idle", 5f);
        }

        private IEnumerator FadeMark(SpriteRenderer sr, float seconds)
        {
            if (sr == null) yield break;
            Color c0 = sr.color;
            for (float t = 0f; t < seconds && sr != null; t += Time.deltaTime)
            {
                sr.color = new Color(c0.r, c0.g, c0.b, c0.a * (1f - t / seconds));
                yield return null;
            }
            if (sr != null) Object.Destroy(sr.gameObject);
        }
    }
}
