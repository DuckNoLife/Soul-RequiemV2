using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// After phase II -> a VIOLET PORTAL drags Ren into the domain THE UNWRITTEN.
    /// Absalom returns before the altar, says 2 lines, raises a hand -> the violet portal (Volcano pack: dom_portal_open 29 frames -> dom_portal_loop) opens from a
    /// spark right in front of him -> its pull slides Ren toward it (Ren resists in pulses, dust under his feet, camera shake) -> at the portal: Ren shrinks,
    /// spins, flashes -> exit `to_unwritten` (Map 4 triggers layer, requires absalom_p2) -> scene Map4n_TheUnwritten.
    /// Phase II cleared but the domain is not (e.g. died inside the domain and came back) -> entering the hall replays this scene (short version).
    /// Scene missing from Build Settings -> put Ren back, explain how to build it, retry on the next entry to the hall.
    /// Called by: Encounter (.cs) after phase II or when re-entering with absalom_p2 set. Calls: MapEncounterDirector.ExitNow(`portal_exit`, default `to_unwritten`) -> scene load.
    /// </summary>
    public sealed partial class AbsalomHallMechanism
    {
        private static readonly Color PortalViolet = new Color(0.72f, 0.45f, 1f);
        private const string PortalExit = "to_unwritten";

        private IEnumerator PortalPull(bool again)
        {
            state = State.Rest;
            DrillBoard.Active = false;
            ClearField();
            ClearGuide();
            ArenaCam(false);
            BeginScene();
            var player = Ctx.Player;
            if (player == null) { EndScene(); yield break; }
            var rb = player.GetComponent<Rigidbody2D>();
            var psr = player.GetComponentInChildren<SpriteRenderer>();
            Vector3 sprScale = psr != null ? psr.transform.localScale : Vector3.one;
            Quaternion sprRot = psr != null ? psr.transform.localRotation : Quaternion.identity;
            Vector2 stand = home + Vector2.up * 0.25f;
            Vector2 gate = home + Vector2.down * 1.15f;                                     // portal opens right in front of Absalom, between him and Ren

            var cf = Ctx.Camera;
            if (cf != null) cf.Focus(Vector2.Lerp(gate, player.position, 0.45f) + Vector2.up * 0.2f, Mathf.Max(2.6f, Float("cam_size", 2.6f)));

            // Absalom returns to the altar, facing Ren
            abs.Airborne = true;
            if ((abs.Pos - stand).sqrMagnitude > 0.01f) yield return Glide(stand, 2.4f);
            abs.Face((Vector2)player.position - abs.Pos);
            abs.Play("idle", 5f);
            if (!again)
            {
                yield return Line("Lamps. Faithful. You break everything that holds me up.", 3f);
                yield return Line("Then come and see what they were holding.", 2.6f);
            }
            else yield return Line("You came back. The Vessel is still waiting.", 2.6f);

            // portal opens
            abs.Play("strike", 8f, false);
            abs.Flash(PortalViolet, 1.2f);
            Sfx.Play("teleport_dark", gate, 1f);
            Sfx.Play("quake_rumble", gate, 0.55f);
            var glowGo = new GameObject("Absalom portal glow");
            glowGo.transform.position = gate;
            var glow = glowGo.AddComponent<Light2D>();
            glow.lightType = Light2D.LightType.Point;
            glow.color = PortalViolet; glow.pointLightOuterRadius = 2.6f; glow.pointLightInnerRadius = 0.2f; glow.intensity = 0f;
            var open = SpriteVfx.Spawn("dom_portal_open", gate, 1f, Layer, Order - 1, false, 14f, false);
            CameraFollow.KickMain(Vector2.down, 0.05f, 0.5f);
            for (float t = 0f; t < 29f / 14f; t += Time.deltaTime)
            {
                glow.intensity = Mathf.Lerp(0f, 1.5f, t / (29f / 14f)) + Mathf.Sin(t * 30f) * 0.08f;
                yield return null;
            }
            if (open != null) open.Stop();
            var loop = SpriteVfx.Spawn("dom_portal_loop", gate, 1f, Layer, Order - 1, false, 12f, false);
            if (loop != null) loop.Looping(1e6f);
            abs.Play("idle", 5f);
            SpeechBubble.Show((Vector3)(Vector2)player.position + Vector3.up * 0.95f, "...!", 1.2f);

            // pull: Ren slides toward the portal, resisting in pulses
            Vector2 p = player.position;
            float speed = 0f, nextDust = 0f, t0 = Time.time;
            while ((p - gate).magnitude > 0.32f)
            {
                float t = Time.time - t0, dt = Time.deltaTime;
                speed = Mathf.Min(2.6f, speed + dt * (t < 1.6f ? 0.55f : 2.2f));
                Vector2 d = (gate - p).normalized;
                float resist = t < 1.8f ? Mathf.Max(0f, Mathf.Sin(t * 8.5f)) * 0.55f : 0f;   // resist: jerk back in pulses
                p += d * (speed - resist) * dt;
                p += new Vector2(Mathf.Sin(t * 47f), 0f) * 0.004f;                               // run
                if (rb != null) { rb.position = p; rb.linearVelocity = Vector2.zero; }
                player.position = new Vector3(p.x, p.y, player.position.z);
                if (Time.time >= nextDust)
                {
                    nextDust = Time.time + 0.16f;
                    SpriteVfx.Spawn("m3_dash_dust", p, 0.55f, Layer, Order - 1, d.x < 0f, 18f, false);
                }
                glow.intensity = 1.5f + Mathf.Sin(t * 20f) * 0.25f;
                if (t > 6f) break;                                                                 // safety cap
                yield return null;
            }
            // swallowed: shrink + spin + flash
            Sfx.Play("teleport_dark", gate, 1f);
            BossJuice.Shake(2);
            for (float t = 0f; t < 0.45f; t += Time.deltaTime)
            {
                float u = t / 0.45f;
                Vector2 q = Vector2.Lerp(p, gate, u);
                if (rb != null) { rb.position = q; rb.linearVelocity = Vector2.zero; }
                player.position = new Vector3(q.x, q.y, player.position.z);
                if (psr != null)
                {
                    psr.transform.localScale = sprScale * Mathf.Lerp(1f, 0.08f, u * u);
                    psr.transform.localRotation = sprRot * Quaternion.Euler(0f, 0f, u * 540f);
                }
                yield return null;
            }
            BossJuice.Flash(gate, Color.white, 4f, 4f, 0.5f);
            if (psr != null) psr.enabled = false;
            yield return new WaitForSeconds(0.35f);

            // sang domain
            if (psr != null) { psr.transform.localScale = sprScale; psr.transform.localRotation = sprRot; psr.enabled = true; }
            EndScene();
            if (cf != null) cf.Release();
            Ctx.Director.ExitNow(Str("portal_exit", PortalExit));
            yield return new WaitForSeconds(0.25f);
            if (!SceneTransition.Busy)
            {
                // transfer failed (missing exit / scene not built): put Ren back in front of the portal, close it, retry on the next entry to the hall
                Vector2 back = gate + Vector2.down * 1.4f;
                if (rb != null) { rb.position = back; rb.linearVelocity = Vector2.zero; }
                player.position = new Vector3(back.x, back.y, player.position.z);
                Hints.Show("[WIP] The Unwritten is not built yet: Soul Requiem > Build Map > Map 4, then try again.", 7f);
                Seal(false);
                state = State.Waiting;
                readyAt = Time.time + 6f;
            }
            if (loop != null) loop.Stop();
            Object.Destroy(glowGo);
        }

        private IEnumerator Line(string text, float seconds)
        {
            var b = SpeechBubble.Show(abs.Pos + Vector2.up * 1.3f, text, seconds, Azure, "Absalom");
            if (b != null && abs.Sr != null) b.Follow(abs.Sr.transform);
            yield return new WaitForSeconds(seconds + 0.15f);
        }
    }
}
