using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// AWAKENING cutscene of The Crystal Vessel (first entry into the domain). Told through light + motion, short lines:
    ///   Absalom appears in the air beside the Vessel's head -> 3 lines -> draws 3 relics out of Ren's chest (BLUE orbs = free relics, lore colour rule):
    ///   Eye -> eye sockets (head + eyes flare violet), Spine -> sternum (ribs + sternum + both arms flare violet, the Vessel slowly lifts its head),
    ///   Ears -> top of the head (jaw flares violet). The Vessel rises fully and ROARS (jaw open, heavy shake, flash) -> Mantras lost notice
    ///   -> Absalom fades -> flag vessel_awake, control returns.
    /// Dying in the domain afterwards does not replay this scene (flag already set).
    /// Called by: Animate (first frame with the player, flag not set). Calls: AbsalomActor, VesselRig (wake parts), MusicStart (.Music.cs), CamWide.
    /// </summary>
    public sealed partial class VesselDomainMechanism
    {
        private static readonly Color RelicBlue = new Color(0.45f, 0.75f, 1f);
        private static readonly Color VesselViolet = new Color(0.75f, 0.45f, 1f);
        private AbsalomActor actor;
        private bool sceneOwned;

        private void TickActor() { if (actor != null) actor.Tick(); }

        private IEnumerator Awakening()
        {
            busy = true;
            sceneOwned = true;
            CutsceneState.Begin();
            GameInput.Blocked = true;
            Ctx.SetPlayerControl(false);
            var player = Ctx.Player;
            yield return new WaitForSeconds(1.4f);                                       // let the player see the giant husk first

            // Absalom appears beside the Vessel's shoulder
            Vector2 at = rig.PointWorld(new Vector2(215f, -35f));
            actor = new AbsalomActor(at, Ctx.Host.transform, player) { Alpha = 0f };
            actor.Airborne = true;
            actor.Lift = 0.35f;
            actor.Face((Vector2)player.position - at);
            actor.Play("idle", 5f);
            Sfx.Play("teleport_dark", at, 0.9f);
            var halo = BossJuice.Light(at + Vector2.up * 0.6f, new Color(0.6f, 0.82f, 1f), 1.6f, 1f, 0f);
            for (float t = 0f; t < 0.9f; t += Time.deltaTime) { actor.Alpha = t / 0.9f; yield return null; }
            actor.Alpha = 1f;

            yield return Say("This is the page I never wrote, Nameless.", 3f);
            yield return Say("You were never their keeper. Only their road.", 3.2f);
            yield return Say("Give them back to the one that was made to hold them.", 3f);

            // draw out the 3 relics
            actor.Play("strike", 8f, false);
            yield return Siphon(player, new Vector2(-35f, -150f), "head", "The Eye is torn from you.", 0.66f);
            yield return Siphon(player, new Vector2(0f, 0f), "spine", "The Spine is torn from you.", 0.33f);
            yield return Siphon(player, new Vector2(0f, -205f), "ears", "The Ears are torn from you.", 0f);
            actor.Play("idle", 5f);

            // roar
            yield return new WaitForSeconds(0.5f);
            Sfx.Play("quake_rumble", chest, 1f);
            Sfx.Play("bell_toll", chest, 0.8f);
            MusicStart(1.2f);                                                           // music starts on the roar (intro ~12 s -> the main beat lands on the first move)
            BossJuice.Shake(3);
            BossJuice.Flash(rig.PointWorld(new Vector2(0f, -110f)), VesselViolet, 5f, 3f, 0.8f);
            for (float t = 0f; t < 0.45f; t += Time.deltaTime) { rig.JawOpen = t / 0.45f; yield return null; }
            rig.JawOpen = 1f;
            CameraFollow.KickMain(Vector2.down, 0.12f, 1.1f);
            yield return new WaitForSeconds(1.3f);
            for (float t = 0f; t < 0.6f; t += Time.deltaTime) { rig.JawOpen = 1f - t / 0.6f; yield return null; }
            rig.JawOpen = 0f;
            Hints.Show("Your mantras were torn out with the relics.", 4f);
            yield return new WaitForSeconds(0.6f);

            yield return Say("Wake, Vessel. Wear what he carried.", 2.8f);
            Sfx.Play("teleport_dark", actor.Pos, 0.9f);
            for (float t = 0f; t < 0.8f; t += Time.deltaTime) { actor.Alpha = 1f - t / 0.8f; yield return null; }
            actor.Destroy();
            actor = null;
            if (halo != null) Object.Destroy(halo.gameObject);

            Ctx.Flags.Set(AwakeFlag);
            CamWide(6f);                                                                // keep the wide frame straight into the Phase 1 intro (no zoom in then out again)
            EndCutscene();
            busy = false;
            main = null;
        }

        /// <summary>One relic: blue glow gathers on Ren's chest -> the orb arcs up to a Vessel body part -> that part flares violet.</summary>
        private IEnumerator Siphon(Transform player, Vector2 targetPx, string group, string hint, float slumpAfter)
        {
            Vector2 from = (Vector2)player.position + Vector2.up * 0.45f;
            Sfx.Play("eye_beam_charge", from, 0.7f);
            var orbGo = new GameObject("relic orb " + group);
            orbGo.transform.position = from;
            var sr = orbGo.AddComponent<SpriteRenderer>();
            sr.sprite = MarginArt.Pixel();
            FxLighting.MakeUnlit(sr);
            sr.sortingLayerID = layer; sr.sortingOrder = 12;
            var l = orbGo.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point;
            l.color = RelicBlue; l.pointLightOuterRadius = 1.2f; l.pointLightInnerRadius = 0.05f; l.intensity = 0f;
            var aura = EnergyAura.Attach(player, RelicBlue);
            // gather: Ren freezes, the orb grows on his chest
            for (float t = 0f; t < 0.8f; t += Time.deltaTime)
            {
                float u = t / 0.8f;
                float s = Mathf.Lerp(2f, 9f, u) + Mathf.Sin(t * 40f) * 0.6f;
                orbGo.transform.position = from + new Vector2(Mathf.Sin(t * 37f), 0f) * 0.01f;
                orbGo.transform.localScale = new Vector3(s, s, 1f);
                sr.color = new Color(0.75f, 0.9f, 1f, 0.95f);
                l.intensity = Mathf.Lerp(0.2f, 1.4f, u);
                yield return null;
            }
            if (aura != null) aura.Stop();
            CameraFollow.KickMain(Vector2.up, 0.04f, 0.25f);
            Sfx.Play("hymn_pop", from, 0.8f);
            // arc up to the body
            Vector2 to = rig.PointWorld(targetPx);
            Vector2 mid = (from + to) * 0.5f + new Vector2(from.x < to.x ? -1.4f : 1.4f, 0.8f);
            for (float t = 0f; t < 1.15f; t += Time.deltaTime)
            {
                float u = t / 1.15f, e = u * u * (3f - 2f * u), w = 1f - e;
                to = rig.PointWorld(targetPx);
                Vector2 q = w * w * from + 2f * w * e * mid + e * e * to;
                orbGo.transform.position = q;
                float s = 9f + Mathf.Sin(t * 25f) * 1.2f;
                orbGo.transform.localScale = new Vector3(s, s, 1f);
                if (Random.value < 0.5f) Trail(q);
                yield return null;
            }
            Object.Destroy(orbGo);
            // slot in: that part flares violet
            rig.AwakenGroup(group);
            slumpTarget = slumpAfter;
            Sfx.Play("ward_break", to, 0.9f);
            Sfx.Play("glass_break", to, 0.5f);
            BossJuice.Flash(to, VesselViolet, 2.8f, 2.6f, 0.45f);
            BossJuice.Shake(1);
            Hints.Show(hint, 2.2f);
            yield return new WaitForSeconds(0.9f);
        }

        /// <summary>Blue light trail left behind the orb (fades over 0.4 s).</summary>
        private void Trail(Vector2 at)
        {
            var g = new GameObject("relic trail");
            g.transform.position = at + Random.insideUnitCircle * 0.04f;
            var r = g.AddComponent<SpriteRenderer>();
            r.sprite = MarginArt.Pixel();
            FxLighting.MakeUnlit(r);
            r.sortingLayerID = layer; r.sortingOrder = 11;
            float s = Random.Range(2f, 4f);
            g.transform.localScale = new Vector3(s, s, 1f);
            Ctx.Run(FadeOut(r, 0.4f));
        }

        private static IEnumerator FadeOut(SpriteRenderer r, float seconds)
        {
            for (float t = 0f; t < seconds && r != null; t += Time.deltaTime)
            {
                r.color = new Color(0.6f, 0.85f, 1f, 0.8f * (1f - t / seconds));
                yield return null;
            }
            if (r != null) Object.Destroy(r.gameObject);
        }

        private IEnumerator Say(string text, float seconds)
        {
            if (actor == null) yield break;
            var b = SpeechBubble.Show(actor.Pos + Vector2.up * 1.3f, text, seconds, new Color(0.6f, 0.82f, 1f), "Absalom");
            if (b != null && actor.Sr != null) b.Follow(actor.Sr.transform);
            yield return new WaitForSeconds(seconds + 0.15f);
        }

        private void EndCutscene()
        {
            if (actor != null) { actor.Destroy(); actor = null; }
            if (!sceneOwned) return;
            sceneOwned = false;
            if (DialogueBox.Active) DialogueBox.Close();
            CutsceneState.End();
            GameInput.Blocked = false;
            if (Ctx.Player != null) Ctx.SetPlayerControl(true);
        }
    }
}
