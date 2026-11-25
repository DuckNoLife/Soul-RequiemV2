using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// THE GRIP — The Registrar lifts Ren with magic, throws him onto the floor, then ink quills spike up right where he lands.
    ///   1. A violet sigil CLINGS under Ren's feet for 0.85 s (running does not escape), then CLOSES for 0.4 s (WHITE flash, still clinging).
    ///      Rolling as it closes (roll invulnerability) = escape. No roll = caught.
    ///   2. Caught: Ren is lifted (violet aura around the whole body — EnergyAura) and hangs shaking for a beat.
    ///   3. Thrown: fast 0.55 s arc to a far spot (shadow + ring mark the landing), slams into the floor (shake, dust), stunned 0.2 s.
    ///   4. An ink pool spreads under the landing for 0.55 s, then a CLUSTER OF INK QUILLS SPIKES UP: still standing in it (no roll) = `spikeDamage` (20).
    /// Steps 2–4 live in Toss(), shared with "get close and get thrown" (.Tease.cs; no quills there, only `tossDamage` on impact).
    /// While lifted: Ren's controls + collision are off (cannot get stuck in walls), other moves cannot hit him. Boss gone / Ren dies -> everything
    /// is restored (EndGrip).
    /// </summary>
    public sealed partial class RegistrarSkills
    {
        private bool gripping, tossing;
        private Transform gripped;
        private Vector3 gripScale;
        private Quaternion gripRot;
        private EnergyAura gripAura;
        private readonly List<Collider2D> gripCols = new List<Collider2D>();

        private bool RollProtected => pCombat != null && Time.time - pCombat.LastRollTime < Mathf.Max(0.3f, pCombat.rollIFrames);

        private IEnumerator Grip(bool teach)
        {
            if (Halt || tossing) yield break;
            if (teach)
            {
                taughtGrip = true;
                Hints.Show("A sigil grips the floor beneath you. ROLL [" + GameInput.Label(GameAction.Roll) + "] the moment it flashes WHITE to slip free. If he throws you, get off the ink before the quills burst out!", 6f);
                yield return W(1.6f);
            }
            busyAnim = true;
            ai.PlayState("Attack");
            if (ai.Vfx != null) ai.Vfx.Cast();
            Sfx.Play("reg_grip", PlayerFeet, 0.85f);
            // 1) sigil clings, then closes
            Vector2 at = PlayerFeet;
            var sig = Make("grip sigil", MarginArt.Sigil(), at, -3);
            const float Follow = 0.85f, Close = 0.4f, R = 0.45f;
            for (float t = 0f; t < Follow + Close && !Halt; t += Dt)
            {
                at = Vector2.MoveTowards(at, PlayerFeet, (t < Follow ? 9f : 6f) * Dt);
                sig.transform.position = at;
                sig.transform.rotation = Quaternion.Euler(0f, 0f, t * (t < Follow ? 120f : 420f));
                float s = t < Follow ? 0.95f + 0.08f * Mathf.Sin(t * 12f) : Mathf.Lerp(0.95f, 0.7f, (t - Follow) / Close);
                sig.transform.localScale = new Vector3(s, s * 0.62f, 1f);   // lies on the floor: squashed vertically
                sig.color = t < Follow ? new Color(0.8f, 0.55f, 1f, 0.8f) : (Mathf.Repeat(t * 16f, 1f) < 0.5f ? Color.white : new Color(0.85f, 0.6f, 1f, 1f));
                yield return null;
            }
            Vector2 dd = PlayerFeet - at;
            bool inside = (dd.x * dd.x) / (R * R) + (dd.y * dd.y) / (R * R * 0.45f) < 1f;
            bool caught = !Halt && inside && !RollProtected && !tossing && !gripping;
            if (sig != null) Destroy(sig.gameObject);
            if (!caught)
            {
                if (!Halt && !tossing) DrillBoard.Finish("SLIPPED FREE!", new Color(0.85f, 1f, 0.8f));   // HUD banner text, no dialogue box
                ai.PlayState("Idle"); busyAnim = false;
                yield break;
            }
            yield return Toss(true);
            ai.PlayState("Idle");
            busyAnim = false;
        }

        /// <summary>
        /// LIFT + THROW (steps 2–4 of The Grip). spikes = true: ink pool + quills at the landing (The Grip); false: only `tossDamage` on impact
        /// (Ren walked up to him).
        /// </summary>
        private IEnumerator Toss(bool spikes)
        {
            if (Halt || tossing || gripping) yield break;
            tossing = true;
            try
            {
                // 2) lift, hang shaking
                BeginGrip();
                Vector2 g = PlayerFeet;
                Sfx.Play("reg_grip", g, 1f);
                var shadow = Make("grip shadow", DrillChalk.Shadow(), g, -5);
                const float Lift = 0.5f, Hold = 0.35f, H = 0.7f;
                for (float t = 0f; t < Lift + Hold && !Halt; t += Dt)
                {
                    float u = Mathf.Clamp01(t / Lift);
                    float h = H * (1f - (1f - u) * (1f - u)) + (t > Lift ? Mathf.Sin(t * 45f) * 0.018f : 0f);
                    PlaceGripped(g + Vector2.up * h);
                    if (shadow != null) shadow.transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.65f, u);
                    yield return null;
                }
                if (Halt) { if (shadow != null) Destroy(shadow.gameObject); EndGrip(); yield break; }
                // 3) throw to a far spot
                Vector2 target = ThrowTarget(g);
                var ring = Make("grip land", MarginArt.Ring(), target, -4);
                ring.transform.localScale = new Vector3(1f, 0.62f, 1f);
                Sfx.Play("drill_whoosh", g, 0.9f);
                Kick((target - g).normalized, 0.05f, 0.2f);
                const float Fly = 0.55f;
                for (float t = 0f; t < Fly && !Halt; t += Dt)
                {
                    float u = Mathf.Clamp01(t / Fly);
                    Vector2 ground = Vector2.Lerp(g, target, u * u);
                    float h = Mathf.Lerp(H, 0f, u * u) + Mathf.Sin(u * Mathf.PI) * 0.35f;
                    PlaceGripped(ground + Vector2.up * h);
                    if (gripped != null) gripped.rotation = Quaternion.Euler(0f, 0f, (target.x >= g.x ? -1f : 1f) * u * 300f);
                    if (shadow != null) { shadow.transform.position = (Vector3)ground; shadow.transform.localScale = Vector3.one * Mathf.Lerp(0.65f, 1f, u); }
                    if (ring != null) ring.color = new Color(0.8f, 0.55f, 1f, 0.55f + 0.35f * Mathf.Sin(t * 22f));
                    yield return null;
                }
                if (ring != null) Destroy(ring.gameObject);
                if (shadow != null) Destroy(shadow.gameObject);
                if (Halt) { EndGrip(); yield break; }
                PlaceGripped(target);
                if (gripped != null) gripped.rotation = gripRot;
                Vector2 away = (target - g).normalized;
                Sfx.Play("heavy_land", target, 1f);
                Kick(Vector2.down, 0.1f, 0.3f);
                SpriteVfx.Spawn("reg_paper_dust", target, 1.1f, Layer, pOrder - 1, false, 14f, false);
                if (!spikes)
                {
                    yield return W(0.2f);                                       // stunned 0.2 s
                    EndGrip();
                    HurtPlayer(tossDamage, away, 2f, true);
                    yield break;
                }
                // 4) ink pool spreads at the landing -> ink quills spike up
                var pool = Make("spike ink", MarginArt.Prop("margin_puddle"), target - new Vector2(0f, 0.2f), -6);
                var warn = Make("spike warn", MarginArt.Ring(), target, -4);
                Sfx.Play("ink_splash", target, 0.9f);
                const float Down = 0.2f, Spread = 0.55f;
                for (float t = 0f; t < Spread && !Halt; t += Dt)
                {
                    if (t >= Down && gripping) EndGrip();                         // stunned 0.2 s, then controls return
                    float u = t / Spread;
                    if (pool != null) pool.transform.localScale = new Vector3(Mathf.Lerp(0.3f, 1.25f, u), Mathf.Lerp(0.3f, 1.25f, u), 1f);
                    if (warn != null)
                    {
                        warn.transform.localScale = new Vector3(1.3f, 0.8f, 1f);
                        warn.color = Mathf.Repeat(t * 14f, 1f) < 0.5f ? new Color(1f, 0.85f, 1f, 0.95f) : new Color(0.7f, 0.35f, 1f, 0.6f);
                    }
                    yield return null;
                }
                if (gripping) EndGrip();
                if (warn != null) Destroy(warn.gameObject);
                if (Halt) { if (pool != null) Destroy(pool.gameObject); yield break; }
                var spike = Make("ink spike", MarginArt.Prop("margin_inkspike"), target - new Vector2(0f, 0.22f), 1);
                Sfx.Play("spear_burst", target, 1f);
                Sfx.Play("ink_splash", target, 0.8f);
                Kick(Vector2.up, 0.08f, 0.25f);
                SpriteVfx.Spawn("reg_ink_splash", target, 1f, Layer, pOrder + 3, false, 18f, false);
                if ((PlayerFeet - target).magnitude < 0.5f)
                    HurtPlayer(spikeDamage, (PlayerFeet - target).sqrMagnitude > 0.0001f ? (PlayerFeet - target).normalized : away, 4f);
                for (float t = 0f; t < 0.85f && !Halt; t += Dt)
                {
                    float k = t < 0.12f ? Mathf.Lerp(0.15f, 1.15f, t / 0.12f) : t < 0.2f ? Mathf.Lerp(1.15f, 1f, (t - 0.12f) / 0.08f) : 1f;
                    if (spike != null) { spike.transform.localScale = new Vector3(1f, k, 1f); if (t > 0.55f) spike.color = new Color(1f, 1f, 1f, 1f - (t - 0.55f) / 0.3f); }
                    if (pool != null && t > 0.55f) pool.color = new Color(1f, 1f, 1f, 1f - (t - 0.55f) / 0.3f);
                    yield return null;
                }
                if (spike != null) Destroy(spike.gameObject);
                if (pool != null) Destroy(pool.gameObject);
            }
            finally
            {
                if (gripping) EndGrip();
                tossing = false;
            }
        }

        /// <summary>Landing spot: a point near the edge of the remaining playable page, far from the grab point (random among the 3 farthest).</summary>
        private Vector2 ThrowTarget(Vector2 from)
        {
            var c = new List<Vector2>();
            float i = 0.32f;
            for (int k = 0; k < 5; k++)
            {
                float u = (k + 0.5f) / 5f;
                c.Add(new Vector2(Mathf.Lerp(safe.xMin + i, safe.xMax - i, u), safe.yMin + i));
                c.Add(new Vector2(Mathf.Lerp(safe.xMin + i, safe.xMax - i, u), safe.yMax - i));
            }
            for (int k = 0; k < 3; k++)
            {
                float u = (k + 0.5f) / 3f;
                c.Add(new Vector2(safe.xMin + i, Mathf.Lerp(safe.yMin + i, safe.yMax - i, u)));
                c.Add(new Vector2(safe.xMax - i, Mathf.Lerp(safe.yMin + i, safe.yMax - i, u)));
            }
            c.Sort((a, b) => (b - from).sqrMagnitude.CompareTo((a - from).sqrMagnitude));
            return c[Random.Range(0, Mathf.Min(3, c.Count))];
        }

        private void BeginGrip()
        {
            if (gripping || ai.Player == null) return;
            gripping = true;
            gripped = ai.Player;
            gripScale = gripped.localScale;
            gripRot = gripped.rotation;
            if (pCtrl != null) pCtrl.enabled = false;
            if (pCombat != null) pCombat.enabled = false;
            var an = gripped.GetComponent<Animator>();
            if (an != null) foreach (var p in an.parameters) if (p.name == "Speed" && p.type == AnimatorControllerParameterType.Float) an.SetFloat("Speed", 0f);
            gripCols.Clear();
            foreach (var col in gripped.GetComponentsInChildren<Collider2D>())
                if (col.enabled && !col.isTrigger) { col.enabled = false; gripCols.Add(col); }
            gripAura = EnergyAura.Attach(gripped, MarginArt.Violet);   // aura around the whole body (replaces the old bubble)
        }

        private void PlaceGripped(Vector2 at)
        {
            if (gripped == null) return;
            var rb = gripped.GetComponent<Rigidbody2D>();
            if (rb != null) { rb.position = at; rb.linearVelocity = Vector2.zero; }
            gripped.position = new Vector3(at.x, at.y, gripped.position.z);
        }

        /// <summary>Return Ren to normal (also called when the boss is removed mid-lift).</summary>
        private void EndGrip()
        {
            if (!gripping) return;
            gripping = false;
            if (gripped != null)
            {
                gripped.rotation = gripRot;
                gripped.localScale = gripScale;
                if (pCtrl != null) pCtrl.enabled = true;
                if (pCombat != null) pCombat.enabled = true;
                foreach (var col in gripCols) if (col != null) col.enabled = true;
            }
            gripCols.Clear();
            gripped = null;
            if (gripAura != null) gripAura.Stop();
            gripAura = null;
        }
    }
}
