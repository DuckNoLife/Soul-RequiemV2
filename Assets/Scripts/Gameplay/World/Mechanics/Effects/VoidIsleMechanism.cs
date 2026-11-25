using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// void_isle - FLOATING ROCK ISLANDS / ELEMENTAL CRYSTALS around The Unwritten domain.
    /// Placed as tile objects in the `mechanics_art` layer (tileset SR-void-deco.tsx: isle_a..g, crystal_fire / ice / storm / umbral).
    /// Each object: slow bobbing, very slight sideways drift, with DEPTH (`depth`):
    ///   0..1  behind the main island: the farther, the darker, fainter, slower bobbing, more camera shift (parallax) - drawn behind the island tile layer
    ///   &lt; 0   FOREGROUND: large black silhouettes covering the screen corners, shifting against the camera -> strong depth (drawn over everything)
    /// Properties: depth (default 0.4), bob (amplitude, default 0.06), glow (#hex - crystals: pulsing Light2D + rising light motes),
    ///   thread_to "x,y" (Tiled tile) - a fine Ether stream flowing from the crystal to that point (The Crystal Vessel's chest): the crystal is being drained.
    /// </summary>
    public sealed class VoidIsleMechanism : Mechanism
    {
        private class Spark { public SpriteRenderer r; public Vector2 off; public float born, life, speed; }
        private class Bead { public SpriteRenderer r; public float born, life; }

        private Transform root;
        private SpriteRenderer body;
        private Light2D glow;
        private Vector2 home;
        private float depth, bobAmp, bobFreq, driftAmp, phase, halfW;
        private Color glowCol;
        private bool hasGlow, hasThread;
        private Vector2 threadTo;
        private float nextBead;
        private readonly List<Spark> sparks = new List<Spark>();
        private readonly List<Bead> beads = new List<Bead>();
        private Vector3 camStart; private bool hasCam;
        private static Sprite dot;

        // the Vessel ABSORBS the elemental crystals (Absorb)
        private static readonly List<VoidIsleMechanism> all = new List<VoidIsleMechanism>();
        /// <summary>Crystal element (fire / ice / storm / umbral); empty = plain rock island.</summary>
        public string Element { get; private set; } = "";
        private bool absorbed;
        private Vector2 absorbFrom, absorbTo;
        private float absorbT0, absorbDur = 1f;
        /// <summary>Elemental crystals on the current map not yet absorbed.</summary>
        public static List<VoidIsleMechanism> Crystals()
        {
            all.RemoveAll(x => x == null || x.root == null);
            return all.FindAll(x => x.Element.Length > 0 && !x.absorbed);
        }
        // crystal -> ELEMENTAL ENERGY PILLAR (image crystal_<element> + rune layer _runes + broken version _broken). Absalom activates the phase's pillar
        //       (Channel) -> runes flare white, strong light, a dense stream of energy motes flows to the Vessel's chest; when the phase ends the pillar BREAKS (Shatter).
        //       Normally: no mote stream (it only flows while activated).
        private SpriteRenderer runes;
        private Sprite brokenSprite;
        private Sprite origSprite, graySprite;                                          // plain rock islands change tone with the phase (DomainTone)
        private SpriteRenderer isleTone;                                                // energy pillar - a GREY layer over the rock island part at its base (x phase colour)
        private Sprite isleG, isleBrokenG;
        private Color baseCol;
        private bool broken, channelOn;
        private float surgeUntil, channelK;
        /// <summary>Pillar of element `el` (broken / absorbed ones included); null if the map has none.</summary>
        public static VoidIsleMechanism Find(string el)
        {
            all.RemoveAll(x => x == null || x.root == null);
            return all.Find(x => x.Element == el);
        }
        public Vector2 Pos => root != null ? (Vector2)root.position : home;
        public bool Broken => broken;
        /// <summary>Turn the energy stream flowing to the Vessel on / off.</summary>
        public void Channel(bool on) => channelOn = on && !broken;
        /// <summary>Flare strongly for `seconds` seconds (power being drawn out).</summary>
        public void Surge(float seconds) => surgeUntil = Time.time + seconds;
        /// <summary>Pillar breaks: the middle rock block snaps, runes go out, light dies. instant = re-entering the map after that phase (no effects).</summary>
        public void Shatter(bool instant)
        {
            if (broken || root == null) return;
            broken = true; channelOn = false;
            if (brokenSprite != null && body != null) body.sprite = brokenSprite;
            if (runes != null) runes.enabled = false;
            foreach (var sp in sparks) if (sp.r != null) sp.r.enabled = false;
            if (instant) return;
            Vector2 p = Pos + Vector2.up * 0.6f;
            BossJuice.Flash(p, glowCol, 3f, 3f, 0.6f);
            BossJuice.Shake(2);
            Sfx.Play("glass_break", p, 1f);
            Sfx.Play("ward_break", p, 0.9f);
            for (int i = 0; i < 8; i++)
                SpriteVfx.Spawn("abs_crackle", p + Random.insideUnitCircle * 0.8f, 0.7f, body.sortingLayerID, body.sortingOrder + 5, false, 18f, false)?.Tint(glowCol);
        }

        /// <summary>Drawn toward `to` over `seconds` seconds: flies in an arc, shrinks, flares, then vanishes.</summary>
        public void Absorb(Vector2 to, float seconds)
        {
            if (absorbed || root == null) return;
            absorbed = true; absorbFrom = root.position; absorbTo = to; absorbT0 = Time.time; absorbDur = Mathf.Max(0.2f, seconds);
        }

        public override void Init()
        {
            var art = Art.Count > 0 ? Art[0] : null;
            var s = art != null ? art.Resolve("sprite") : Resources.Load<Sprite>(Str("sprite", ""));
            if (s == null) { Debug.LogWarning("[VoidIsle] " + Name + ": missing image"); return; }
            float k = art != null ? art.ScaleFor(s) : Float("scale", 1f);
            home = art != null ? art.PivotFor(s) : Rect.center;
            depth = Float("depth", 0.4f);
            bobAmp = Float("bob", 0.06f) * (depth >= 0f ? Mathf.Lerp(1f, 0.6f, depth) : 1.2f);
            bobFreq = Random.Range(0.12f, 0.2f) * (depth >= 0f ? Mathf.Lerp(1f, 0.7f, depth) : 0.8f);
            driftAmp = Random.Range(0.03f, 0.08f);
            phase = Random.Range(0f, 20f);
            halfW = s.bounds.size.x * k * 0.5f;

            root = new GameObject("Void Isle " + Name).transform;
            root.SetParent(Ctx.Host.transform, false);
            root.position = new Vector3(home.x, home.y, 0f);
            var bg = new GameObject("body");
            bg.transform.SetParent(root, false);
            bg.transform.localScale = new Vector3(k, k, 1f);
            body = bg.AddComponent<SpriteRenderer>();
            body.sprite = s;
            body.flipX = art != null && art.FlipX;
            FxLighting.MakeUnlit(body);
            if (depth >= 0f)
            {
                float shade = Mathf.Lerp(0.85f, 0.34f, depth);
                body.color = new Color(shade * 0.96f, shade * 0.92f, shade * 1.06f, Mathf.Lerp(1f, 0.82f, depth));
                body.sortingOrder = -30 - Mathf.RoundToInt(depth * 10f);
            }
            else
            {
                body.color = new Color(0.07f, 0.05f, 0.1f, 0.94f);                      // foreground: black silhouette, faint purple rim from the background light
                body.sortingOrder = 40;
            }

            string g = Str("glow", "");
            if (g.Length > 0 && depth >= 0f)
            {
                hasGlow = true;
                glowCol = MechanismContext.ParseColor(g, 1f);
                var lg = new GameObject("glow");
                lg.transform.SetParent(root, false);
                lg.transform.localPosition = new Vector3(0f, s.bounds.size.y * k * 0.22f, 0f);
                glow = lg.AddComponent<Light2D>();
                glow.lightType = Light2D.LightType.Point;
                glow.color = glowCol;
                glow.pointLightOuterRadius = Float("glow_radius", 2.1f);
                glow.pointLightInnerRadius = 0.1f;
                glow.intensity = 0.9f;
                if (dot == null) dot = MakeDot();
                for (int i = 0; i < 9; i++) sparks.Add(NewSpark(true));
            }
            Element = Str("element", "");
            all.Add(this);
            // energy pillars draw IN FRONT of the island under the watch tower (order -24 / -23) - otherwise the tower island hid them
            if (Element.Length > 0 && depth >= 0f) { body.sortingOrder = Int("order", -22); foreach (var sp0 in sparks) sp0.r.sortingOrder = body.sortingOrder + 1; }
            origSprite = s; baseCol = body.color;
            if (Element.Length == 0 && depth >= 0f) graySprite = DomainTone.Gray(Str("sprite", ""));
            if (Element.Length > 0)                                                   // rune layer + broken version of the energy pillar
            {
                string sp = Str("sprite", "VFX/void/crystal_" + Element);
                brokenSprite = Resources.Load<Sprite>(sp + "_broken");
                var rs = Resources.Load<Sprite>(sp + "_runes");
                if (rs != null)
                {
                    var rg = new GameObject("runes");
                    rg.transform.SetParent(bg.transform, false);
                    runes = rg.AddComponent<SpriteRenderer>();
                    runes.sprite = rs; runes.flipX = body.flipX;
                    FxLighting.MakeUnlit(runes);
                    runes.sortingLayerID = body.sortingLayerID; runes.sortingOrder = body.sortingOrder + 1;
                    runes.color = new Color(1f, 1f, 1f, 0f);
                }
                isleG = Resources.Load<Sprite>(sp + "_isle_g");
                isleBrokenG = Resources.Load<Sprite>(sp + "_broken_isle_g");
                if (isleG != null)
                {
                    var tg = new GameObject("isle tone");
                    tg.transform.SetParent(bg.transform, false);
                    isleTone = tg.AddComponent<SpriteRenderer>();
                    isleTone.sprite = isleG; isleTone.flipX = body.flipX;
                    FxLighting.MakeUnlit(isleTone);
                    isleTone.sortingLayerID = body.sortingLayerID; isleTone.sortingOrder = body.sortingOrder + 1;
                    isleTone.enabled = false;
                }
            }
            string tt = Str("thread_to", "");
            if (tt.Length > 0 && hasGlow) { hasThread = true; threadTo = Ctx.TilePoint(tt, home); nextBead = Time.time + Random.Range(0f, 1f); }
        }

        private Spark NewSpark(bool anyAge)
        {
            var sp = new Spark();
            var go = new GameObject("spark");
            go.transform.SetParent(root, false);
            sp.r = go.AddComponent<SpriteRenderer>();
            sp.r.sprite = dot;
            FxLighting.MakeUnlit(sp.r);
            sp.r.sortingOrder = body.sortingOrder + 1;
            ResetSpark(sp, anyAge);
            return sp;
        }

        private void ResetSpark(Spark sp, bool anyAge)
        {
            sp.off = new Vector2(Random.Range(-halfW * 0.5f, halfW * 0.5f), Random.Range(0.1f, 0.5f));
            sp.life = Random.Range(1.6f, 3.2f);
            sp.speed = Random.Range(0.12f, 0.3f);
            sp.born = Time.time - (anyAge ? Random.Range(0f, sp.life) : 0f);
        }

        public override void Animate()
        {
            if (root == null) return;
            float now = Time.time, dt = Time.deltaTime;
            if (absorbed)
            {
                float au = Mathf.Clamp01((now - absorbT0) / absorbDur), ae = au * au;
                Vector2 amid = (absorbFrom + absorbTo) * 0.5f + Vector2.up * 1.5f;     // own name: 'mid' is already used in the Ether stream part below (CS0136)
                float aw = 1f - ae;
                root.position = aw * aw * absorbFrom + 2f * aw * ae * amid + ae * ae * absorbTo;
                float asc = Mathf.Lerp(1f, 0.15f, ae);
                root.localScale = new Vector3(asc, asc, 1f);
                if (body != null) { var ac = body.color; ac.a = 1f - au * au; body.color = ac; }
                if (glow != null) glow.intensity = 0.9f + 2.5f * au;
                foreach (var b in beads) if (b.r != null) Object.Destroy(b.r.gameObject);
                beads.Clear();
                if (au >= 1f) root.gameObject.SetActive(false);
                return;
            }
            var cam = Camera.main;
            Vector3 cp = cam != null ? cam.transform.position : Vector3.zero;
            if (!hasCam && cam != null) { camStart = cp; hasCam = true; }
            Vector2 camDelta = hasCam ? (Vector2)(cp - camStart) : Vector2.zero;
            Vector2 par = depth >= 0f ? camDelta * Mathf.Lerp(0.05f, 0.45f, depth) : -camDelta * 0.3f;
            float t = (now + phase) * bobFreq * Mathf.PI * 2f;
            Vector2 p = home + par + new Vector2(Mathf.Sin(t * 0.37f) * driftAmp, Mathf.Sin(t) * bobAmp);
            root.position = new Vector3(p.x, p.y, 0f);
            if (graySprite != null && body != null)                                    // phase tone (grey image x phase colour)
            {
                bool g = DomainTone.Active;
                var want = g ? graySprite : origSprite;
                if (body.sprite != want) body.sprite = want;
                body.color = g ? DomainTone.Mul(baseCol, DomainTone.Tint) : baseCol;
            }
            if (isleTone != null)                                                      // the island under the pillar follows the phase tone, the pillar keeps its element colour
            {
                isleTone.enabled = DomainTone.Active && !absorbed;
                var want = broken && isleBrokenG != null ? isleBrokenG : isleG;
                if (isleTone.sprite != want) isleTone.sprite = want;
                isleTone.color = DomainTone.Mul(baseCol, DomainTone.Tint);
            }

            if (!hasGlow) return;
            float pulse = 0.5f + 0.5f * Mathf.Sin((now + phase) * 1.7f);
            channelK = Mathf.MoveTowards(channelK, !broken && (channelOn || now < surgeUntil) ? 1f : 0f, dt * 1.5f);
            if (glow != null) glow.intensity = broken ? 0.1f : (0.65f + 0.45f * pulse) * (1f + 1.8f * channelK);
            if (runes != null)                                                        // runes flare white gradually while activated, blinking fast
            {
                float flick = 0.75f + 0.25f * Mathf.Sin(now * 11f + phase);
                runes.color = new Color(1f, 1f, 1f, channelK * flick);
            }
            if (broken) return;
            foreach (var sp in sparks)
            {
                float age = now - sp.born;
                if (age > sp.life) { ResetSpark(sp, false); age = 0f; }
                float u = age / sp.life;
                sp.r.transform.localPosition = new Vector3(sp.off.x + Mathf.Sin((now + sp.born) * 2.1f) * 0.03f, sp.off.y + age * sp.speed, 0f);
                float a = Mathf.Sin(u * Mathf.PI) * (0.55f + 0.45f * Mathf.Sin(now * 9f + sp.born * 5f));
                sp.r.color = new Color(Mathf.Lerp(glowCol.r, 1f, 0.4f), Mathf.Lerp(glowCol.g, 1f, 0.4f), Mathf.Lerp(glowCol.b, 1f, 0.4f), a * 0.9f);
            }

            if (!hasThread) return;
            // Ether stream: small light motes drifting along a curve from the crystal to the Vessel's chest (fading at both ends)
            // only flows while the pillar is activated (channelK) - the stronger, the denser
            if (now >= nextBead && channelK > 0.05f)
            {
                nextBead = now + Mathf.Lerp(0.6f, 0.06f, channelK) * Random.Range(0.8f, 1.2f);
                var go = new GameObject("ether bead");
                go.transform.SetParent(Ctx.Host.transform, false);
                var r = go.AddComponent<SpriteRenderer>();
                r.sprite = dot; FxLighting.MakeUnlit(r); r.sortingOrder = body.sortingOrder + 2;
                beads.Add(new Bead { r = r, born = now, life = Random.Range(3.2f, 4.2f) });
            }
            Vector2 a0 = (Vector2)root.position + new Vector2(0f, 0.35f), a1 = threadTo;
            Vector2 mid = (a0 + a1) * 0.5f + new Vector2(0f, 0.9f);
            for (int i = beads.Count - 1; i >= 0; i--)
            {
                var b = beads[i];
                float u = (now - b.born) / b.life;
                if (u >= 1f || b.r == null) { if (b.r != null) Object.Destroy(b.r.gameObject); beads.RemoveAt(i); continue; }
                float w = 1f - u;
                Vector2 q = w * w * a0 + 2f * w * u * mid + u * u * a1;                 // quadratic Bezier - the stream curves up, then falls into the chest
                q += new Vector2(Mathf.Sin(u * 18f + b.born) * 0.03f, 0f);
                b.r.transform.position = new Vector3(q.x, q.y, 0f);
                b.r.color = new Color(Mathf.Lerp(glowCol.r, 1f, 0.3f), Mathf.Lerp(glowCol.g, 1f, 0.3f), Mathf.Lerp(glowCol.b, 1f, 0.3f), Mathf.Sin(u * Mathf.PI) * Mathf.Lerp(0.55f, 0.95f, channelK));
                float bs = Mathf.Lerp(1f, 1.8f, channelK); b.r.transform.localScale = new Vector3(bs, bs, 1f);
            }
        }

        private static Sprite MakeDot()
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "void_spark" };
            tex.SetPixels32(new[] { new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 170), new Color32(255, 255, 255, 170), new Color32(255, 255, 255, 90) });
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
