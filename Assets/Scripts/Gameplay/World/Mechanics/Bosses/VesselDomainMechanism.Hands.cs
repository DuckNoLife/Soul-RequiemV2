using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// SHARED KIT of The Crystal Vessel — arena rect, Ren's feet, Ren's velocity, hand shadows, wave rings,
    /// damage to Ren, arena cleanup, coroutines, beams / ripple rings. The moves live in .Physical.cs (Phase 1 + shared hand moves) and the phase files.
    /// Used by: every VesselDomainMechanism.* file.
    /// </summary>
    public sealed partial class VesselDomainMechanism
    {
        private static readonly Color Violet = new Color(0.75f, 0.45f, 1f);
        private static readonly Color WarnViolet = new Color(0.85f, 0.35f, 1f);
        private readonly List<GameObject> fieldObjs = new List<GameObject>();
        private readonly List<Coroutine> routines = new List<Coroutine>();
        private static Sprite ringSprite;

        private Rect Floor => Rect;                                                    // object box = walkable island floor
        private Vector2 Feet => Ctx.Player != null ? (Vector2)Ctx.Player.position : Floor.center;

        private Vector2 ClampFloor(Vector2 p, float m = 0.35f) =>
            new Vector2(Mathf.Clamp(p.x, Floor.xMin + m, Floor.xMax - m), Mathf.Clamp(p.y, Floor.yMin + m, Floor.yMax - m));

        // ------------------------------------------------------------------ damage to Ren
        /// <summary>Heavy hit (slammed / clapped): strong knockback.</summary>
        private bool HeavyHit(float dmg, Vector2 from) => Hurt(dmg, from, 1.4f);

        private bool Hurt(float dmg, Vector2 from, float knock)
        {
            var hp = Ctx.PlayerHealth;
            if (hp == null || hp.IsDead || hp.Invulnerable) return false;
            Vector2 d = Feet - from;
            return hp.TakeDamage(dmg, d.sqrMagnitude > 0.0001f ? d.normalized : Vector2.down, knock);
        }

        // ------------------------------------------------------------------ warnings / effects
        /// <summary>Pentagon wave ring spreading from the impact: hits Ren when the rim passes his feet (a roll dodges it).</summary>
        private IEnumerator Shockwave(Vector2 at, float r0, float r1, float speed, float dmg)
        {
            var g = new GameObject("vessel shockwave");
            fieldObjs.Add(g);
            g.transform.position = at;
            var sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = Ring();
            FxLighting.MakeUnlit(sr);
            sr.sortingLayerID = layer; sr.sortingOrder = -14;
            bool hit = false;
            Color c = PhaseColor;
            for (float r = r0; r < r1 && !aborted; r += speed * Time.deltaTime)
            {
                float k = r / (Ring().bounds.size.x * 0.5f);
                g.transform.localScale = new Vector3(k, k * 0.62f, 1f);
                float a = 1f - (r - r0) / (r1 - r0);
                sr.color = new Color(Mathf.Lerp(c.r, 1f, 0.2f), Mathf.Lerp(c.g, 1f, 0.2f), Mathf.Lerp(c.b, 1f, 0.2f), 0.85f * a);
                Vector2 d = Feet - at;
                float rr = new Vector2(d.x, d.y / 0.62f).magnitude;                    // ellipse rim squashed to 0.62 like the angled floor
                if (!hit && Mathf.Abs(rr - r) < 0.16f) hit = Hurt(dmg, at, 0.8f);
                yield return null;
            }
            DestroyObj(g);
        }

        private SpriteRenderer Shadow(int side, Vector2 at, string hand = "hand_open")
        {
            var g = new GameObject("hand shadow");
            fieldObjs.Add(g);
            g.transform.position = at;
            var sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = VesselRig.Load(hand, true);
            FxLighting.MakeUnlit(sr);
            sr.sortingLayerID = layer; sr.sortingOrder = -15;
            sr.color = new Color(0f, 0f, 0f, 0f);
            return sr;
        }

        /// <summary>Hand shadow growing with progress u (0..1) — faint -> solid, blinks once locked.</summary>
        private void GrowShadow(SpriteRenderer shadow, int side, Vector2 at, float u, bool locked, float t)
        {
            if (shadow == null) return;
            shadow.transform.position = at;
            float s = Mathf.Lerp(0.55f, 1f, u) * rig.Scale;
            shadow.transform.localScale = new Vector3(s * (side > 0 ? -1f : 1f), s * 0.45f, 1f);
            shadow.color = new Color(0.05f, 0f, 0.1f, Mathf.Lerp(0.15f, 0.62f, u) + (locked ? 0.15f * Mathf.Sin(t * 40f) : 0f));
            ShadowSigil(shadow, u, locked, t);                                         // phase-colour seal ring under the hand shadow
        }

        private void SlamFx(Vector2 at, int tier)
        {
            Sfx.Play("quake_rumble", at, 0.8f);
            Sfx.Play("hit_block", at, 0.7f);
            BossJuice.Shake(tier);
            BossJuice.Flash(at, PhaseColor, 2.2f, 2.2f, 0.3f);
            SpriteVfx.Spawn("abs_dive_impact", at, 0.9f, layer, order - 1, false, 18f, false)?.Tint(Color.Lerp(PhaseColor, Color.white, 0.3f));
            for (int i = 0; i < 2; i++)
                SpriteVfx.Spawn("m3_dash_dust", at + new Vector2(i == 0 ? -0.45f : 0.45f, -0.1f), 0.7f, layer, order - 1, i == 0, 16f, false);
            if (phase == 1 && actState == ActState.Playing)                            // Phase 2 — hand impact bursts lava
            {
                SpriteVfx.Spawn("vc_lava_impact", at, 1.3f + 0.25f * tier, layer, order + 1, false, 16f, false);
                SpriteVfx.Spawn("abs_fire_blast", at + Vector2.up * 0.2f, 0.8f, layer, order + 2, false, 18f, false);
            }
            if (phase == 3 && actState == ActState.Playing) SkyBolt(at, 0.9f + 0.15f * tier);   // Phase 4 — lightning strikes the fist
            if (phase == 2 && actState == ActState.Playing)                            // Phase 3 — hand impact bursts ice
            {
                SpriteVfx.Spawn("vc_snow_impact", at, 1.3f + 0.2f * tier, layer, order + 1, false, 14f, false);
                SpriteVfx.Spawn("duo_ice_burst", at + Vector2.down * 0.1f, 1.1f, layer, order + 2, false, 20f, false);
                Sfx.Play("ice_shatter", at, 0.7f);
            }
        }

        private static void StretchLine(SpriteRenderer sr, Vector2 a, Vector2 b, float thick)
        {
            if (sr == null) return;
            Vector2 d = b - a;
            sr.transform.position = (a + b) * 0.5f;
            sr.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            sr.transform.localScale = new Vector3(Mathf.Max(0.01f, d.magnitude) * 100f, thick * 100f, 1f);
        }

        /// <summary>Continuous ripple rings at `at` (telegraph) — destroy the returned GameObject to stop it.</summary>
        private GameObject RippleAt(Vector2 at, Color c)
        {
            var g = new GameObject("ripple");
            fieldObjs.Add(g);
            g.transform.position = at;
            var sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = Ring(); FxLighting.MakeUnlit(sr);
            sr.sortingLayerID = layer; sr.sortingOrder = -13;
            Ctx.Run(RippleAnim(sr, c));
            return g;
        }

        private static IEnumerator RippleAnim(SpriteRenderer sr, Color c)
        {
            for (float t = 0f; sr != null; t += Time.deltaTime)
            {
                float u = (t * 1.8f) % 1f;
                float k = Mathf.Lerp(0.2f, 0.85f, u) / (Ring().bounds.size.x * 0.5f);
                sr.transform.localScale = new Vector3(k, k * 0.62f, 1f);
                sr.color = new Color(c.r, c.g, c.b, 0.9f * (1f - u));
                yield return null;
            }
        }

        private static float SegDist(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            return (p - (a + ab * t)).magnitude;
        }

        private SpriteRenderer Beam(Vector2 from, Vector2 to, float thick, Color c)
        {
            var g = new GameObject("vessel beam");
            fieldObjs.Add(g);
            var sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = MarginArt.Beam();
            FxLighting.MakeUnlit(sr);
            sr.sortingLayerID = layer; sr.sortingOrder = order + 4;
            sr.color = c;
            Place(sr, from, to, thick);
            var l = g.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point; l.color = c; l.pointLightOuterRadius = 1.4f; l.intensity = 1.2f;
            return sr;
        }

        /// <summary>Beam sprite (pivot at middle-left; length = scale.x * 0.04, thickness = scale.y * 0.24).</summary>
        private static void Place(SpriteRenderer sr, Vector2 from, Vector2 to, float thick)
        {
            if (sr == null) return;
            Vector2 d = to - from;
            sr.transform.position = from;
            sr.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            sr.transform.localScale = new Vector3(d.magnitude / 0.04f, thick / 0.24f, 1f);
            var l = sr.GetComponent<Light2D>();
            if (l != null) l.transform.position = sr.transform.position;
        }

        // ------------------------------------------------------------------ arena cleanup + coroutines
        private void DestroyObj(Component c) { if (c != null) { fieldObjs.Remove(c.gameObject); Object.Destroy(c.gameObject); } }
        private void DestroyObj(GameObject g) { if (g != null) { fieldObjs.Remove(g); Object.Destroy(g); } }

        private void ClearField()
        {
            StopRoutines();
            HallBullet.ClearAll();
            foreach (var g in fieldObjs) if (g != null) Object.Destroy(g);
            fieldObjs.Clear();
            StopLoopFx(); KillRework();                                                              // no flamethrower / spinning fire / looping breath left on the arena
            ReleaseGrab(false);
            ResetHands();
            KillImps();
            KillMagma();
            KillIce();
            KillStorm();
            KillUmbra();
            KillLightFx();                                                             // watchtower shadow / ice cage
            lungeGoal = 0f;
            ClearBurn();
            if (rig != null) rig.HeadDip = 0f;
            jawHold = -1f;
            handBusy[0] = handBusy[1] = false;
            if (rig != null) { rig.RestHand(-1, 4f); rig.RestHand(1, 4f); }
        }

        private void StartR(IEnumerator r) => routines.Add(Ctx.Run(r));

        private void StopRoutines()
        {
            foreach (var c in routines) if (c != null) Ctx.Host.StopCoroutine(c);
            routines.Clear();
        }

        private IEnumerator Wait(float s)
        {
            float end = Time.time + s;
            while (Time.time < end && !aborted) yield return null;
        }

        // ------------------------------------------------------------------ Ren's velocity (for lead aiming)
        private Vector2 lastFeet, playerVel;
        private Vector2 PlayerVel => playerVel;
        private void TrackVelocity(float dt)
        {
            if (dt <= 0f) return;
            Vector2 f = Feet;
            playerVel = Vector2.Lerp(playerVel, (f - lastFeet) / dt, 1f - Mathf.Exp(-8f * dt));
            lastFeet = f;
        }

        /// <summary>White 2 px ring, radius 64 px (tinted + scaled when drawn).</summary>
        private static Sprite Ring()
        {
            if (ringSprite != null) return ringSprite;
            const int R = 64, N = R * 2 + 4;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "vessel_ring" };
            var px = new Color32[N * N];
            float c = (N - 1) * 0.5f;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float d = Mathf.Abs(Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) - R);
                    byte a = d < 1f ? (byte)255 : d < 2f ? (byte)150 : d < 3.2f ? (byte)50 : (byte)0;
                    px[y * N + x] = new Color32(255, 255, 255, a);
                }
            tex.SetPixels32(px); tex.Apply(false, true);
            return ringSprite = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
