using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// OPENING CUTSCENE "THE HIEROPHANT RISES" (first time; replays use a 2 s short version). Four beats:
    ///   1. SILENCE — the door slams behind Ren (light shake), the camera glides up to the altar: a HUMAN-SIZED hooded old man prays with his back turned
    ///      before the seal. He speaks without turning (about the 6 vessels he carried down himself — none ever climbed back up).
    ///   2. FACE TO FACE — a bell, he turns, the camera pushes in on his face: he sees Ren escaped the cell, recalls the blow at the Requiem Door, names himself.
    ///   3. THE HAND — his left hand (the holy arm grafted onto his shoulder): "You thought this hand was only for carrying you?"
    ///   4. TRANSFORMATION — no words: the ground shakes harder, the 4 Votive Lamps ignite one by one (one element each, light tethers to him), the floor seal
    ///      blazes white, light particles are drawn in, he GROWS 0.57 -> 1 (boss size, ~1.8x Ren) and RISES into the air (hover, golden trail), the camera pulls
    ///      back to the whole hall; climax: light pillar + shockwave + white flash over the hall, Ren is knocked back; boss name card "ABSALOM"; closing line
    ///      "Kneel, Seven. Or be lifted." -> fight.
    /// Cutscene rules: no chapters, no title lines in the dialogue box, epic tone; Map 4 may reveal the truth.
    /// No arm-swinging gestures anywhere in the scene:
    ///   He is an old priest: still, slow-spoken; all the drama comes from LIGHT + CAMERA + SOUND, not gestures.
    ///   • Silence: glowing ash drifts slowly down a light shaft over the altar; the camera drifts up smoothly (no jerks), the halo behind him breathes.
    ///   • Face to face: TWO-STEP TURN (back -> side -> front, 0.5 s each), no strong flash.
    ///   • The hand: the holy arm (white arm grafted on his left shoulder) is NOT raised — it slowly GLOWS from within (warm gold light grows,
    ///     gold particles rise from the hand), the camera pushes in slowly. That golden glow is the lead-in to the transformation.
    ///   • Transformation: he rises with both arms hanging (slow hover animation), robe flowing — the whole hall swells with light around him.
    /// Called by: Encounter (.cs): IntroScene on first entry, ShortIntro on replays. Calls: DialogueBox, SpeechBubble, CameraFollow, AbsalomActor, lamps (.Lamps.cs), floor seal (.cs).
    /// </summary>
    public sealed partial class AbsalomHallMechanism
    {
        private const float SmallSize = 0.57f;
        private bool introGround, sizeDrive, camDrive;

        /// <summary>Called every frame (Animate): size + hover from the state when no scene is driving them.</summary>
        private void AnimateSize(float dt)
        {
            if (abs == null || sizeDrive) return;
            bool big = state == State.Fight || (state == State.Intro && !introGround);
            abs.Size = Mathf.MoveTowards(abs.Size, big ? 1f : SmallSize, dt * 0.6f);
        }

        private CameraFollow Cam => Ctx.Camera != null ? Ctx.Camera : (Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null);

        private void CamFocus(Vector2 p, float size)
        {
            var cf = Cam;
            if (cf == null) return;
            cf.Focus(p, size);
            camOwned = true;
        }

        private IEnumerator IntroScene()
        {
            BeginScene();
            camDrive = true;
            introGround = true;
            sizeDrive = true;
            abs.Size = SmallSize; abs.Lift = 0f; abs.Airborne = false;
            abs.Face(Vector2.up);
            abs.Play("idle", 4f);
            abs.Alpha = 1f;

            abs.Play("idle", 3f);                                                       // slow breathing, no gestures
            introHold = AddHazard(HazardShape.Circle, abs.Pos, abs.Pos, 0f, 0f, 0f, 0f, 90f);   // keep the scene's lights / particles: a reset -> ClearHazards removes them
            var shaft = GlowAt(abs.Pos + Vector2.up * 0.9f, new Color(0.75f, 0.82f, 1f), 1.6f, 0f, 0f);   // cold light shaft over the altar
            introHold.Objs.Add(shaft);
            Ctx.Run(IntroAsh());

            // 1) SILENCE — door slams, camera drifts up to the altar, his back is turned
            Sfx.Play("gate_close", PlayerFeet, 1f);
            CameraFollow.KickMain(Vector2.down, 0.05f, 0.25f);
            yield return Wait(0.9f);
            Vector2 camFrom = PlayerFeet + Vector2.up * 0.3f, camTo = Vector2.Lerp(PlayerFeet, abs.Pos, 0.62f) + Vector2.up * 0.25f;
            yield return CamGlide(camFrom, camTo, 2.6f, 1.9f, 1.9f, shaft, 0.9f);       // drift 2.6 s, light shaft slowly rises
            if (aborted) yield break;
            Sfx.Play("tremor_soft", abs.Pos, 0.3f);
            yield return Wait(0.7f);
            yield return Say("Absalom",
                "Six cells. Six vessels. I carried each of them down these stairs myself.",
                "Not one of them ever walked back up.");
            if (aborted) yield break;

            // 2) FACE TO FACE — bell, two-step turn (back -> side -> front), slow camera push
            Sfx.Play("bell_toll", abs.Pos, 0.8f);
            yield return Wait(0.9f);
            Vector2 toRen = PlayerFeet - abs.Pos;
            abs.Face(new Vector2(toRen.x >= 0f ? 1f : -1f, 0.1f));                     // side (E / W)
            yield return Wait(0.55f);
            abs.Face(toRen);                                                            // front
            abs.Flash(Azure, 1.2f);                                                     // soft light, slow fade (no flash)
            abs.HaloPulse(1.4f);
            yield return CamGlide(camTo, abs.Pos + Vector2.up * 0.35f, 1.4f, 1.9f, 1.45f, null, 0f);
            yield return Wait(0.4f);
            yield return Say("Absalom",
                "And yet here you stand, Seven. Out of the cell I laid you in, with a blade I never gave back.",
                "We have met. At the Requiem Door. You did not see my face. You saw the floor.",
                "I am Absalom, Hierophant of the Soul Requiem. Seven hundred years I have kept His hand warm.");
            if (aborted) yield break;

            // 3) THE HAND — not raised: the holy arm glows from within, gold particles rise, camera pushes in
            Vector2 hand = HandAtSide();
            var handGlow = GlowAt(hand, Sun, 0.5f, 0f, 0f);
            introHold.Objs.Add(handGlow);
            Sfx.Play("lg_charge", abs.Pos, 0.3f);
            Ctx.Run(HandEmbers(handGlow));
            yield return CamGlide(abs.Pos + Vector2.up * 0.35f, Vector2.Lerp(abs.Body, hand, 0.4f), 1.6f, 1.45f, 1.2f, handGlow, 1.6f, 1.3f);
            if (aborted) yield break;
            yield return Say("Absalom",
                "You think this hand only carried you down the stairs.",
                "It once held up a god.");
            if (aborted) yield break;
            if (DialogueBox.Active) DialogueBox.Close();

            // 4) TRANSFORMATION
            yield return Ascension(true);
            if (aborted) yield break;
            yield return Say("Absalom", "Kneel, Seven. Or be lifted.");
            EndScene();
        }

        private Hazard introHold;

        /// <summary>Position of the holy hand when hanging (white arm on his left = screen right when facing down; side view = in front).</summary>
        private Vector2 HandAtSide()
        {
            float s = abs.Size;
            return abs.Pos + new Vector2(0.25f * s, 0.29f * s + abs.Lift);           // measured on idle_S: hand at +22 px horizontal, +25 px above the feet (x1.15)
        }

        /// <summary>Smooth camera move (ease in-out) from a to b, frame size z0 -> z1; also brightens `light` to `toIntensity`
        /// (and widens its radius to `toRadius` if > 0).</summary>
        private IEnumerator CamGlide(Vector2 a, Vector2 b, float secs, float z0, float z1, GameObject light, float toIntensity, float toRadius = 0f)
        {
            var l = light != null ? light.GetComponent<Light2D>() : null;
            float i0 = l != null ? l.intensity : 0f, r0 = l != null ? l.pointLightOuterRadius : 0f;
            for (float t = 0f; t < secs && !aborted; t += Time.deltaTime)
            {
                float u = Mathf.SmoothStep(0f, 1f, t / secs);
                CamFocus(Vector2.Lerp(a, b, u), Mathf.Lerp(z0, z1, u));
                if (l != null)
                {
                    l.intensity = Mathf.Lerp(i0, toIntensity, u);
                    if (toRadius > 0f) { l.pointLightOuterRadius = Mathf.Lerp(r0, toRadius, u); l.pointLightInnerRadius = l.pointLightOuterRadius * 0.15f; }
                }
                yield return null;
            }
            CamFocus(b, z1);
        }

        /// <summary>Glowing ash drifting slowly down the light shaft over the altar during the grounded part of the scene (harmless, atmosphere only).</summary>
        private IEnumerator IntroAsh()
        {
            float next = 0f;
            while (!aborted && state == State.Intro && introGround)
            {
                if (Time.time >= next)
                {
                    next = Time.time + 0.16f;
                    Vector2 p = abs.Pos + new Vector2(Random.Range(-1.1f, 1.1f), Random.Range(1.2f, 2.1f));
                    var m = Shot("abs_light_mote", p, new Vector2(Random.Range(-0.06f, 0.06f), -Random.Range(0.18f, 0.32f)),
                                 new Color(0.85f, 0.9f, 1f, 0.45f), Random.Range(0.18f, 0.3f), 0f, 0f, Random.Range(3f, 4.5f));
                    if (m != null) { m.harmless = true; m.stopOnWalls = false; m.popFx = "none"; }
                }
                yield return null;
            }
        }

        /// <summary>Small gold particles rising slowly from the holy hand while it glows (until the transformation); the hand light breathes like a heartbeat.</summary>
        private IEnumerator HandEmbers(GameObject glow)
        {
            var l = glow != null ? glow.GetComponent<Light2D>() : null;
            float next = 0f;
            while (!aborted && state == State.Intro && introHold != null && glow != null)   // follows the hand even while he rises
            {
                Vector2 hand = HandAtSide();
                glow.transform.position = hand;
                if (l != null && l.intensity > 1.2f) l.intensity = 1.45f + 0.15f * Mathf.Sin(Time.time * 4.2f);
                if (Time.time >= next)
                {
                    next = Time.time + 0.09f;
                    var m = Shot("abs_light_mote", hand + Random.insideUnitCircle * 0.06f, new Vector2(Random.Range(-0.05f, 0.05f), Random.Range(0.25f, 0.45f)),
                                 new Color(1f, 0.86f, 0.5f, 0.85f), Random.Range(0.16f, 0.26f), 0f, 0f, Random.Range(0.9f, 1.4f));
                    if (m != null) { m.harmless = true; m.stopOnWalls = false; m.popFx = "none"; }
                }
                yield return null;
            }
        }

        /// <summary>Replay after death: one short line + short transformation (~2 s).</summary>
        private IEnumerator ShortIntro()
        {
            camDrive = true;
            introGround = true;
            sizeDrive = true;
            abs.Size = SmallSize; abs.Lift = 0f; abs.Airborne = false;
            abs.Face(PlayerFeet - abs.Pos);
            abs.Flash(Azure, 0.5f);
            SpeechBubble.Show(abs.Pos + Vector2.up * 0.9f, Pick("Again, Seven. Again until you understand.", "Rise. The rite is not finished with you.",
                                                                 "The vessel returns. Good."), 2.2f, Azure, "Absalom");
            yield return Wait(1.1f);
            yield return Ascension(false);
        }

        /// <summary>Transformation: growing shake, lamps ignite one by one + tethers, floor seal blazes, particles drawn in, grow + rise, camera pulls back,
        /// climax light burst. full = full version (first time, with boss name card); otherwise the short replay version.</summary>
        private IEnumerator Ascension(bool full)
        {
            float dur = full ? 3.2f : 1.4f;
            Vector2 c = abs.Pos;
            abs.Airborne = true;
            introGround = false;
            liftOverride = 0f;
            abs.Play("idle", 4f);                                                       // slow hover, arms hanging (no swing)
            Sfx.Play("tremor_soft", c, full ? 1f : 0.7f);
            Sfx.Play("sz_inhale", c, 0.7f);
            SealGlow(1f);
            var cf = Cam;
            float camFrom = full ? 1.2f : 2.2f;                                          // continues from the close-up frame on the hand
            Vector2 camA = full ? Vector2.Lerp(abs.Body, HandAtSide(), 0.4f) : abs.Pos + Vector2.up * 0.35f;
            // camera target = whole-hall frame (same as ArenaCam)
            var cam = Camera.main;
            float aspect = cam != null ? Mathf.Max(0.5f, cam.aspect) : 16f / 9f;
            float top = A.yMax + 0.45f, bottom = A.yMin - 0.3f;
            float camTo = Mathf.Max(Float("cam_size", 2.6f), (top - bottom) / (2f - 2f * HudBand), (A.width + 0.4f) * 0.5f / aspect);
            Vector2 camB = new Vector2(A.center.x, bottom + camTo);

            var lit = new List<Lamp>();
            var tethers = new List<SpriteRenderer>();
            var keep = AddHazard(HazardShape.Circle, c, c, 0f, 0f, 0f, 0f, dur + 2f);   // reset midway -> ClearHazards removes the tethers
            float nextMote = 0f, nextLamp = full ? 0.35f : 0.05f, nextShake = 0f;
            var order = new List<Lamp>(lamps);
            for (float t = 0f; t < dur && !aborted; t += Time.deltaTime)
            {
                float u = t / dur, e = 1f - Mathf.Pow(1f - u, 3f);
                // grow (slight overshoot, then settle at 1) + rise
                float over = u < 0.85f ? Mathf.Lerp(SmallSize, 1.08f, e / 0.94f) : Mathf.Lerp(1.08f, 1f, (u - 0.85f) / 0.15f);
                abs.Size = Mathf.Clamp(over, SmallSize, 1.08f);
                liftOverride = Mathf.Lerp(0f, 0.95f, e) + 0.04f * Mathf.Sin(t * 9f);
                abs.HaloPulse(Mathf.Lerp(1.2f, 3f, u));
                // lamps ignite one by one + tethers to him
                if (t >= nextLamp && lit.Count < order.Count)
                {
                    var l = order[lit.Count];
                    lit.Add(l);
                    nextLamp = t + (full ? 0.5f : 0.2f);
                    Color lc = SchoolColor(l.Element);
                    if (!l.Broken)
                    {
                        Fx("abs_charge2", l.Feet + Vector2.up * 0.45f, 0.8f, Order + 4, 30f, lc);
                        FlashLight(l.Feet + Vector2.up * 0.4f, lc, 2f, 2f, 0.5f);
                        if (l.Light != null) l.Light.intensity = 2.5f;
                        var ln = MakeLine("Ascension Tether");
                        ln.sortingOrder = Order + 2; ln.enabled = true; ln.color = lc;
                        tethers.Add(ln);
                        keep.Objs.Add(ln.gameObject);
                    }
                    Sfx.Play("bell_toll", l.Feet, 0.45f + 0.1f * lit.Count);
                    SealCast(lc, 0.6f);
                }
                for (int i = 0, k = 0; i < lit.Count; i++)
                {
                    if (lit[i].Broken) continue;
                    if (k < tethers.Count && tethers[k] != null)
                    {
                        Stretch(tethers[k], lit[i].Feet + Vector2.up * 0.45f, abs.Body, 0.05f + 0.03f * Mathf.Sin(Time.time * 30f + i));
                        var col = tethers[k].color; col.a = 0.55f + 0.35f * Mathf.Sin(Time.time * 18f + i);
                        tethers[k].color = col;
                    }
                    k++;
                }
                // light particles drawn into his body
                if (Time.time >= nextMote)
                {
                    nextMote = Time.time + 0.04f;
                    Vector2 p = abs.Body + Rot(Vector2.right, Random.Range(0f, 360f)) * Random.Range(1.6f, 3.2f);
                    var m = Shot("abs_light_orb", p, (abs.Body - p) * 2.6f, new Color(1f, 1f, 1f, 0.85f), 0.7f, 0f, 0f, 0.36f);
                    m.harmless = true; m.stopOnWalls = false; m.popFx = "none";
                }
                // growing shake
                if (Time.time >= nextShake) { nextShake = Time.time + 0.12f; CameraFollow.KickMain(Random.insideUnitCircle.normalized, Mathf.Lerp(0.01f, 0.06f, u), 0.12f); }
                // camera slowly pulls back to the whole hall
                if (cf != null) { cf.Focus(Vector2.Lerp(camA, camB, e), Mathf.Lerp(camFrom, camTo, e)); camOwned = true; }
                yield return null;
            }
            keep.Dead = true;
            if (introHold != null) { introHold.Dead = true; introHold = null; }        // turn off the scene's light shaft / hand light
            if (aborted) { sizeDrive = false; camDrive = false; yield break; }

            // CLIMAX — light pillar, shockwave, white flash over the hall, Ren knocked back
            abs.Size = 1f;
            Vector2 at = abs.Pos;
            Fx("abs_sunfall", at, 1.1f, Order + 3, 18f, Color.white);
            Fx("abs_quake", at, 1.3f, Order - 1, 16f, Color.white);
            FlashLight(at, Color.white, 7f, 3.2f, 0.7f);
            Shake(3);
            Sfx.Play("mantra_groundslam", at, 1f);
            Sfx.Play("bell_toll", at, 1f);
            Sfx.Play("shockwave", at, 0.9f);
            var php = Ctx.PlayerHealth;
            if (php != null && !php.IsDead)
            {
                Vector2 away = PlayerFeet - at;
                if (away.sqrMagnitude < 0.01f) away = Vector2.down;
                php.AddImpulse(away.normalized * (full ? 3.2f : 2.2f));
            }
            abs.Flash(Color.white, 0.6f);
            SealGlow(0f);
            for (int i = 0; i < 14; i++)
            {
                Vector2 d = Rot(Vector2.right, i * 360f / 14f);
                var m = Shot("abs_light_orb", at + Vector2.up * 0.4f + d * 0.3f, d * 3.2f, Color.white, 0.8f, 0f, 0f, 0.5f);
                m.harmless = true; m.popFx = "none";
            }
            // settle to the normal hover height
            for (float t = 0f; t < 0.5f && !aborted; t += Time.deltaTime)
            {
                liftOverride = Mathf.Lerp(0.95f, 0.42f, t / 0.5f);
                yield return null;
            }
            liftOverride = -1f;
            sizeDrive = false;
            camDrive = false;
            ArenaCam(true);
            if (full)
            {
                BossIntro.CardName = "ABSALOM";
                BossIntro.CardSub = "Hierophant of the Soul Requiem";
                BossIntro.CardColor = new Color(0.45f, 0.08f, 0.1f);
                BossIntro.CardSprite = abs.Sr != null ? abs.Sr.sprite : null;
                BossIntro.CardPortrait = "";
                BossIntro.CardStart = Time.time;
                BossIntro.CardLength = 3.4f;
                yield return Wait(3.2f);
            }
            else yield return Wait(0.4f);
        }
    }
}
