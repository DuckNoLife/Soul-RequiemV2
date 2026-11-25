using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// void_tower - WATCH TOWER in The Unwritten domain (tower + watching eye from the Volcano pack, recoloured to match The Crystal Vessel).
    /// On top of the tower a purple eye (SpriteVfx "void_eye") casts a LIGHT CONE down onto the island; the cone sweeps slowly back and forth inside `sweep`,
    /// and where it touches the floor there is a light spot + a real purple Light2D lighting the floor tiles. During the Vessel fight the domain drives the light (Hunt / SetPool / Boost / InPool) - the light has a gameplay effect per phase;
    /// API: <see cref="InCone"/> / <see cref="Hunt"/> / <see cref="SetMood"/>.
    /// Placed as a TILE OBJECT in the `mechanics_art` layer, dragged from the SR-void-deco.tsx tileset (the tile already carries class void_tower + `sprite`).
    /// Properties (object or tile):
    ///   sprite      tower image (bottom-centre pivot) - VFX/void/tower_tall | tower_short
    ///   isle        rock island image placed under the tower (empty = none) - e.g. VFX/void/isle_pedestal
    ///   sweep       "x,y,w,h" (Tiled tiles) - area the cone sweeps over (default = the whole object area)
    ///   beam_alpha  cone opacity (default 0.2), beam_width cone base width (units, default 1.15), beam_color (#c98cff)
    ///   eye_dy      eye height tweak relative to the tower top (units, default 0.02), bob bobbing amplitude (default 0.05 with `isle`, otherwise 0)
    ///   order       sortingOrder of the tower body (default -23: behind the island tile layer -20 -> the tower stands behind the island edge)
    /// </summary>
    public sealed class WatchTowerMechanism : Mechanism
    {
        private static readonly List<WatchTowerMechanism> all = new List<WatchTowerMechanism>();
        /// <summary>Towers on the current map.</summary>
        public static List<WatchTowerMechanism> Towers() { all.RemoveAll(t => t == null || t.root == null); return new List<WatchTowerMechanism>(all); }

        private Transform root, coneT;
        private SpriteRenderer body, isleR, cone, spot;
        private SpriteVfx eye;
        private Light2D eyeLight, spotLight;
        private Vector2 foot, eyeLocal;
        private Rect sweep;
        private Vector2 aim, aimFrom, aimTo;
        private float aimT0, aimDur, holdUntil, bobAmp, phase, beamWidth, coneLen, coneWide;
        private Color beamColor;
        private float beamAlpha;
        // phase tone (DomainTone) - grey tower / island / eye images times the phase colour; cone, light = phase colour
        private Sprite towerS, towerG, isleS0, isleG;
        private Color bodyBase, isleBase, beamBase;
        private float eyeScale; private int eyeOrder;
        private bool grayOn;
        private Vector2? hunt;
        // the Vessel domain drives the searchlight (the light HAS an effect) - sweep speed, spot width, brightness
        private float huntSpeed = 2.6f, widthGoal = -1f, widthCur = -1f, boostCur = 1f;
        /// <summary>Brightness multiplier for cone + spot + light (1 = normal; &gt;1 = lighting something important). Smooth transition.</summary>
        public float Boost = 1f;
        /// <summary>Force the cone / spot / light colour of EVERY tower (null = DomainTone phase colour). Phase 5: purple-white.</summary>
        public static Color? GlowOverride;
        // light OFF / ON (during the Vessel fight the lights are off and now and then turn on to search for Ren). On by default (decoration).
        private float litGoal = 1f, litCur = 1f;
        /// <summary>0 = light off (cone + spot + floor light off, eye dark), 1 = on. Smooth ~0.3 s transition.</summary>
        public void SetLit(float on) => litGoal = Mathf.Clamp01(on);
        public float Lit => litCur;
        /// <summary>Set the aim point at once (when turning the light on).</summary>
        public void SnapAim(Vector2 p) { aim = p; hunt = p; }

        /// <summary>Eye position (world).</summary>
        public Vector2 EyePos => root != null ? (Vector2)root.position + eyeLocal : foot;
        /// <summary>Point where the cone touches the floor (world).</summary>
        public Vector2 BeamEnd => aim;
        public float BeamWidth => widthCur > 0f ? widthCur : beamWidth;

        public override void Init()
        {
            var art = Art.Count > 0 ? Art[0] : null;
            var s = art != null ? art.Resolve("sprite") : Resources.Load<Sprite>(Str("sprite", "VFX/void/tower_tall"));
            if (s == null) { Debug.LogWarning("[WatchTower] " + Name + ": missing tower image (run _tools/s406/make_void_deco.py)"); return; }
            float k = art != null ? art.ScaleFor(s) : Float("scale", 1f);
            foot = art != null ? art.PivotFor(s) : new Vector2(Rect.center.x, Rect.yMin);

            root = new GameObject("Watch Tower " + Name).transform;
            root.SetParent(Ctx.Host.transform, false);
            root.position = new Vector3(foot.x, foot.y, 0f);
            int order = Int("order", -23);

            string islePath = Str("isle", "");
            var isleS = islePath.Length > 0 ? Resources.Load<Sprite>(islePath) : null;
            if (isleS != null)
            {
                var ig = new GameObject("isle");
                ig.transform.SetParent(root, false);
                // island surface (2nd tile row of the image) right under the tower
                float top = isleS.bounds.max.y;                                   // centre pivot -> top edge of the image
                ig.transform.localPosition = new Vector3(0f, -(top - 0.42f), 0f);
                isleR = ig.AddComponent<SpriteRenderer>();
                isleR.sprite = isleS;
                FxLighting.MakeUnlit(isleR);
                isleR.sortingOrder = order - 1;
                isleR.color = new Color(0.78f, 0.76f, 0.84f, 1f);
                isleS0 = isleS; isleG = DomainTone.Gray(islePath); isleBase = isleR.color;
            }

            var bg = new GameObject("body");
            bg.transform.SetParent(root, false);
            bg.transform.localScale = new Vector3(k, k, 1f);
            body = bg.AddComponent<SpriteRenderer>();
            body.sprite = s;
            body.flipX = art != null && art.FlipX;
            FxLighting.MakeUnlit(body);                                           // moderately self-lit, not swallowed by the domain's 0.22 ambient
            body.sortingOrder = order;
            body.color = new Color(0.86f, 0.84f, 0.92f, 1f);
            towerS = s; towerG = DomainTone.Gray(Str("sprite", "")); bodyBase = body.color;
            eyeScale = k; eyeOrder = order + 1;

            float h = s.bounds.size.y * k;
            eyeLocal = new Vector2(0f, h + Float("eye_dy", 0.02f));
            eye = SpriteVfx.Spawn("void_eye", (Vector2)root.position + eyeLocal, k, body.sortingLayerID, order + 1, false, 10f, false);
            if (eye != null) { eye.Looping(1e7f); eye.Follow(root); }

            eyeLight = MakeLight(root, eyeLocal, new Color(0.78f, 0.45f, 1f), 1.3f, 0.9f, "eye light");

            beamColor = MechanismContext.ParseColor(Str("beam_color", "#c98cff"), 1f);
            beamBase = beamColor;
            beamAlpha = Float("beam_alpha", 0.2f);
            beamWidth = Float("beam_width", 1.15f);
            var coneS = Resources.Load<Sprite>("VFX/void/beam_cone");
            var spotS = Resources.Load<Sprite>("VFX/void/beam_spot");
            if (coneS != null)
            {
                coneT = new GameObject("Beam " + Name).transform;
                coneT.SetParent(Ctx.Host.transform, false);
                cone = coneT.gameObject.AddComponent<SpriteRenderer>();
                cone.sprite = coneS;
                FxLighting.MakeUnlit(cone);
                cone.sortingOrder = 3;                                             // above Ren (order 2): the light falls over anyone standing in the cone
                coneLen = coneS.bounds.size.y; coneWide = coneS.bounds.size.x;
            }
            if (spotS != null)
            {
                var sg = new GameObject("Beam spot " + Name);
                sg.transform.SetParent(Ctx.Host.transform, false);
                spot = sg.AddComponent<SpriteRenderer>();
                spot.sprite = spotS;
                FxLighting.MakeUnlit(spot);
                spot.sortingOrder = -15;                                           // on the floor, below characters
                spotLight = MakeLight(sg.transform, Vector2.zero, beamColor, 1.1f, 0.85f, "spot light");
            }

            sweep = ParseTileRect(Str("sweep", ""), Rect);
            bobAmp = Float("bob", isleS != null ? 0.05f : 0f);
            phase = Random.Range(0f, 10f);
            aim = aimTo = RandomAim();
            NextAim();
            all.Add(this);
        }

        private static Light2D MakeLight(Transform parent, Vector2 local, Color c, float radius, float intensity, string name)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = new Vector3(local.x, local.y, 0f);
            var l = g.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point;
            l.color = c;
            l.pointLightOuterRadius = radius;
            l.pointLightInnerRadius = 0.05f;
            l.intensity = intensity;
            return l;
        }

        private Rect ParseTileRect(string s, Rect fallback)
        {
            var v = s.Split(',');
            if (v.Length == 4 &&
                float.TryParse(v[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                float.TryParse(v[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) &&
                float.TryParse(v[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float w) &&
                float.TryParse(v[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float hh))
            {
                Vector3 a = Ctx.Director.TileToWorld(x, y), b = Ctx.Director.TileToWorld(x + w, y + hh);
                return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
            }
            return fallback;
        }

        private Vector2 RandomAim() => new Vector2(Random.Range(sweep.xMin, sweep.xMax), Random.Range(sweep.yMin, sweep.yMax));

        private void NextAim()
        {
            aimFrom = aim; aimTo = RandomAim();
            aimDur = Mathf.Clamp(Vector2.Distance(aimFrom, aimTo) / 0.9f, 1.6f, 4.5f);   // slow, steady sweep - like a guard scanning the island
            aimT0 = Time.time;
        }

        // ------------------------------------------------------------------ API for the Vessel domain
        /// <summary>Force the cone to sweep to this point (null = back to free sweeping).</summary>
        public void Hunt(Vector2? target, float speed = 2.6f)
        {
            bool was = hunt.HasValue;
            hunt = target; huntSpeed = speed;
            if (target == null && was) NextAim();
        }
        /// <summary>Light spot width (units); &lt;= 0 = back to the Tiled `beam_width`. Smooth transition.</summary>
        public void SetPool(float width) => widthGoal = width;
        /// <summary>Is point p inside the LIGHT SPOT on the floor (an ellipse matching the beam_spot image). pad &gt; 0 = enlarge.</summary>
        public bool InPool(Vector2 p, float pad = 0f)
        {
            if (litCur < 0.5f) return false;                                      // light off = not searching
            Vector2 r = PoolRadii;
            r += new Vector2(pad, pad * (r.y / Mathf.Max(0.01f, r.x)));
            Vector2 d = p - aim;
            return (d.x * d.x) / (r.x * r.x) + (d.y * d.y) / (r.y * r.y) <= 1f;
        }
        /// <summary>Light spot semi-axes (x, y) - matches the drawing (88% of the image edge, because the edge fades out).</summary>
        public Vector2 PoolRadii
        {
            get
            {
                float rx = BeamWidth * 1.05f * 0.5f * 0.88f;
                float ratio = spot != null && spot.sprite != null ? spot.sprite.bounds.size.y / Mathf.Max(0.01f, spot.sprite.bounds.size.x) : 0.5f;
                return new Vector2(rx, rx * ratio);
            }
        }
        /// <summary>Change the cone colour / opacity (e.g. harsh red-purple when Ren is spotted).</summary>
        public void SetMood(Color c, float alpha) { beamColor = c; beamAlpha = alpha; if (spotLight != null) spotLight.color = c; }
        /// <summary>Is point p inside the cone (trapezoid from the eye to the base).</summary>
        public bool InCone(Vector2 p)
        {
            Vector2 e = EyePos, d = aim - e;
            float L = d.magnitude; if (L < 0.01f) return false;
            Vector2 u = d / L;
            float t = Vector2.Dot(p - e, u);
            if (t < 0f || t > L + 0.2f) return false;
            float half = Mathf.Lerp(0.03f, BeamWidth * 0.5f, t / L);
            return Mathf.Abs(Vector2.Dot(p - e, new Vector2(-u.y, u.x))) <= half;
        }

        public override void Animate()
        {
            if (root == null) return;
            float now = Time.time;
            float bob = bobAmp > 0f ? Mathf.Sin((now + phase) * 0.55f * Mathf.PI * 2f) * bobAmp : 0f;
            root.position = new Vector3(foot.x, foot.y + bob, 0f);

            if (widthCur < 0f) widthCur = beamWidth;
            widthCur = Mathf.MoveTowards(widthCur, widthGoal > 0f ? widthGoal : beamWidth, 1.6f * Time.deltaTime);
            boostCur = Mathf.MoveTowards(boostCur, Boost, 3f * Time.deltaTime);
            litCur = Mathf.MoveTowards(litCur, litGoal, 3.5f * Time.deltaTime);
            float W = widthCur;
            if (hunt.HasValue)
            {
                // sweep steadily to the target at the speed set by the domain
                Vector2 to = hunt.Value - aim;
                float step = huntSpeed * Time.deltaTime;
                aim = to.magnitude <= step ? hunt.Value : aim + to.normalized * step;
            }
            else
            {
                float u = Mathf.Clamp01((now - aimT0) / aimDur);
                float e = u * u * (3f - 2f * u);
                aim = Vector2.Lerp(aimFrom, aimTo, e);
                if (u >= 1f)
                {
                    if (holdUntil < aimT0 + aimDur) holdUntil = now + Random.Range(0.5f, 1.4f);   // pause to look for a beat, then keep sweeping
                    else if (now >= holdUntil) NextAim();
                }
            }

            Vector2 eyeP = EyePos;
            float flick = 0.88f + 0.12f * Mathf.Sin((now + phase) * 7.3f) * Mathf.Sin((now + phase) * 3.1f);   // flickering gaze
            if (cone != null)
            {
                Vector2 d = aim - eyeP;
                float L = Mathf.Max(0.1f, d.magnitude);
                coneT.position = new Vector3(eyeP.x, eyeP.y, 0f);
                coneT.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.x, -d.y) * Mathf.Rad2Deg);
                coneT.localScale = new Vector3(W / coneWide, L / coneLen, 1f);
                cone.color = new Color(beamColor.r, beamColor.g, beamColor.b, Mathf.Min(1f, beamAlpha * flick * boostCur) * litCur);
            }
            if (spot != null)
            {
                spot.transform.position = new Vector3(aim.x, aim.y, 0f);
                float sw = W * 1.05f / Mathf.Max(0.01f, spot.sprite.bounds.size.x);
                spot.transform.localScale = new Vector3(sw, sw, 1f);
                spot.color = new Color(beamColor.r, beamColor.g, beamColor.b, Mathf.Min(1f, beamAlpha * 1.6f * boostCur) * flick * litCur);
                spot.sortingOrder = -14;                                           // above the lava surface (-15) -> the light spot visibly cools the lava
                if (spotLight != null) { spotLight.intensity = 0.85f * flick * boostCur * litCur; spotLight.pointLightOuterRadius = W * 0.95f; }
            }
            if (eyeLight != null) eyeLight.intensity = (0.75f + 0.25f * flick) * (0.3f + 0.7f * litCur) * Mathf.Max(1f, boostCur * 0.8f);
            ApplyTone();
        }

        /// <summary>Swap grey / original images when the domain toggles the phase tone; tint body, island, eye, cone, light.</summary>
        private void ApplyTone()
        {
            bool g = DomainTone.Active;
            if (g != grayOn)
            {
                grayOn = g;
                if (body != null) body.sprite = g && towerG != null ? towerG : towerS;
                if (isleR != null) isleR.sprite = g && isleG != null ? isleG : isleS0;
                if (eye != null) eye.Stop();
                eye = SpriteVfx.Spawn(g ? "void_eye_g" : "void_eye", (Vector2)root.position + eyeLocal, eyeScale, body.sortingLayerID, eyeOrder, false, 10f, false);
                if (eye == null && g) eye = SpriteVfx.Spawn("void_eye", (Vector2)root.position + eyeLocal, eyeScale, body.sortingLayerID, eyeOrder, false, 10f, false);
                if (eye != null) { eye.Looping(1e7f); eye.Follow(root); }
            }
            Color glow = GlowOverride ?? (g ? DomainTone.Glow : beamBase);
            beamColor = glow;
            if (body != null) body.color = g && towerG != null ? DomainTone.Mul(bodyBase, DomainTone.Tint) : bodyBase;
            if (isleR != null) isleR.color = g && isleG != null ? DomainTone.Mul(isleBase, DomainTone.Tint) : isleBase;
            if (eye != null) eye.Tint(g ? Color.Lerp(DomainTone.Glow, Color.white, 0.3f) : Color.white);
            if (eyeLight != null) eyeLight.color = g ? glow : new Color(0.78f, 0.45f, 1f);
            if (spotLight != null) spotLight.color = glow;
        }
    }
}
