using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// SHARED VFX TOOLKIT (plan: `_tools/s433/VESSEL_VFX_PLAN.md`):
    ///   Beam     — multi-band beam (16-row pixel cross-section: faint rim -> body -> white core), flash at the origin + glow at the impact point,
    ///              thickness wobble / flicker over time. Replaces the single-colour StretchLine ("ruler line" beams).
    ///   Motes    — light pixel particles: drift, spiral into a centre, burst out; streak along their velocity (frost, sparks, ink, accretion disc).
    ///   BandRing — thin multi-band light ring (Singularity rim, spreading waves).
    /// Textures are generated at runtime (point-filtered Texture2D) — no assets needed, recoloured per phase.
    /// Used by: every Vessel move file. Particles are updated every frame by TickMotes.
    /// </summary>
    public sealed partial class VesselDomainMechanism
    {
        // ------------------------------------------------------------------ band textures
        private static readonly Dictionary<int, Sprite> bandCache = new Dictionary<int, Sprite>();

        /// <summary>Beam cross-section 4 x 16 px: faint outer rows (edge) -> body (mid) -> core. Stretched along x.</summary>
        private static Sprite BandSprite(Color core, Color mid, Color edge)
        {
            int key = core.GetHashCode() ^ (mid.GetHashCode() * 31) ^ (edge.GetHashCode() * 131);
            if (bandCache.TryGetValue(key, out var s) && s != null) return s;
            const int H = 16;
            var tex = new Texture2D(4, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < H; y++)
            {
                float d = Mathf.Abs(y + 0.5f - H * 0.5f) / (H * 0.5f);              // 0 centre -> 1 edge
                Color c = d < 0.22f ? core : d < 0.5f ? mid : d < 0.78f ? new Color(edge.r, edge.g, edge.b, edge.a) : new Color(edge.r, edge.g, edge.b, edge.a * 0.35f);
                for (int x = 0; x < 4; x++) tex.SetPixel(x, y, c);
            }
            tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, 4, H), new Vector2(0f, 0.5f), 4f);  // 1 x 4 source units; pivot at the left end
            bandCache[key] = s;
            return s;
        }

        private static Sprite bandRingSprite;
        /// <summary>64 px ring: thin bright rim (2 px core + 2 px glow each side), transparent inside.</summary>
        private static Sprite BandRing()
        {
            if (bandRingSprite != null) return bandRingSprite;
            const int N = 64;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float r = new Vector2(x + 0.5f - N * 0.5f, y + 0.5f - N * 0.5f).magnitude;
                    float d = Mathf.Abs(r - (N * 0.5f - 4f));
                    float a = d < 1f ? 1f : d < 2.2f ? 0.55f : d < 3.6f ? 0.18f : 0f;
                    float w = d < 1f ? 1f : 0.75f;
                    tex.SetPixel(x, y, new Color(w, w, w, a));
                }
            tex.Apply();
            bandRingSprite = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N / 2f);   // diameter 2 source units
            return bandRingSprite;
        }

        // ------------------------------------------------------------------ BEAM
        // 6-layer beam — soft glow 2.6x wide (bottom) · 4-band body · white core ·
        // rotating 4-point star at the origin · glow + particle splash at the impact point · light streaks running along the beam (particles streak in the firing direction).
        // plain = body only (warning line).
        private sealed class FxBeam
        {
            public SpriteRenderer body, glow, flare, tip, starA, starB;
            public Light2D l;
            public float thick, seed, born;
            public Color core, mid;
            public bool plain;
        }

        private FxBeam NewBeam(Color core, Color mid, Color edge, float thick, int ord, bool plain = false)
        {
            var b = new FxBeam { thick = thick, seed = Random.Range(0f, 10f), core = core, mid = mid, plain = plain, born = Time.time };
            b.body = NewBand("beam fx", ord, BandSprite(core, mid, edge));
            if (plain) return b;
            b.glow = NewBand("beam glow", ord - 1, BandSprite(new Color(mid.r, mid.g, mid.b, 0.32f), new Color(edge.r, edge.g, edge.b, 0.2f), new Color(edge.r, edge.g, edge.b, 0.08f)));
            b.starA = NewBand("beam star", ord + 2, BandSprite(Color.white, core, new Color(mid.r, mid.g, mid.b, 0.5f)));
            b.starB = NewBand("beam star", ord + 2, BandSprite(Color.white, core, new Color(mid.r, mid.g, mid.b, 0.5f)));
            b.flare = NewDot("beam flare", ord + 1, Color.Lerp(mid, Color.white, 0.5f));
            b.tip = NewDot("beam tip", ord + 1, Color.Lerp(mid, Color.white, 0.3f));
            b.l = BossJuice.Light(Vector2.zero, mid, 1.4f, 0f, 0f);
            if (b.l != null) fieldObjs.Add(b.l.gameObject);
            return b;
        }

        private SpriteRenderer NewBand(string name, int ord, Sprite sp)
        {
            var g = new GameObject(name);
            fieldObjs.Add(g);
            var sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = sp;
            FxLighting.MakeUnlit(sr);
            sr.sortingLayerID = layer; sr.sortingOrder = ord;
            return sr;
        }

        private SpriteRenderer NewDot(string name, int ord, Color c)
        {
            var g = new GameObject(name);
            fieldObjs.Add(g);
            var sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = Disc(); FxLighting.MakeUnlit(sr);
            sr.sortingLayerID = layer; sr.sortingOrder = ord;
            sr.color = c;
            return sr;
        }

        private static void PlaceBand(SpriteRenderer sr, Vector2 a, Vector2 e, float th, Color c)
        {
            if (sr == null) return;
            Vector2 d = e - a;
            var t = sr.transform;
            t.position = a;
            t.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            t.localScale = new Vector3(Mathf.Max(0.01f, d.magnitude), th / 4f, 1f);                  // sprite is 4 source units tall
            sr.color = c;
        }

        /// <summary>Place the beam from a to b; alpha 0..1, k = thickness factor (warnings use 0.1–0.4).</summary>
        private void SetBeam(FxBeam b, Vector2 a, Vector2 e, float alpha, float k = 1f)
        {
            if (b == null || b.body == null) return;
            Vector2 d = e - a;
            float len = Mathf.Max(0.01f, d.magnitude);
            Vector2 dir = d / len, nrm = new Vector2(-dir.y, dir.x);
            float now = Time.time + b.seed;
            float th = b.thick * k * (1f + 0.1f * Mathf.Sin(now * 37f) + 0.05f * Mathf.Sin(now * 91f));
            float flick = 0.9f + 0.1f * Mathf.Sin(now * 53f);
            PlaceBand(b.body, a, e, th, new Color(1f, 1f, 1f, alpha * flick));
            if (b.plain) return;
            PlaceBand(b.glow, a, e, th * 2.6f, new Color(1f, 1f, 1f, alpha * (0.75f + 0.25f * Mathf.Sin(now * 9f))));
            // rotating 4-point star at the origin
            float sl = th * (3.2f + 0.6f * Mathf.Sin(now * 21f)), ang = now * 140f;
            Vector2 sa = Rot(Vector2.right, ang) * sl * 0.5f, sb = Rot(Vector2.right, ang + 90f) * sl * 0.35f;
            PlaceBand(b.starA, a - sa, a + sa, th * 0.35f, new Color(1f, 1f, 1f, alpha));
            PlaceBand(b.starB, a - sb, a + sb, th * 0.3f, new Color(1f, 1f, 1f, alpha * 0.8f));
            float fs = th * 2.2f;
            b.flare.transform.position = a; b.flare.transform.localScale = new Vector3(fs, fs, 1f);
            b.flare.color = new Color(b.flare.color.r, b.flare.color.g, b.flare.color.b, alpha * 0.9f);
            float ts = th * (2.4f + 0.5f * Mathf.Sin(now * 29f));
            b.tip.transform.position = e; b.tip.transform.localScale = new Vector3(ts, ts * 0.55f, 1f);
            b.tip.color = new Color(b.tip.color.r, b.tip.color.g, b.tip.color.b, alpha * 0.85f);
            if (b.l != null) { b.l.transform.position = e; b.l.intensity = 1.4f * alpha * flick; }
            // light streaks along the beam + particle splash at the impact point (only once the beam really fires)
            if (alpha > 0.6f && k > 0.5f)
            {
                if (Random.value < Mathf.Clamp01(len * 0.35f))
                    AddMote(a + d * Random.Range(0.05f, 0.9f) + nrm * Random.Range(-0.4f, 0.4f) * th, dir * Random.Range(5f, 9f), new Color(b.core.r, b.core.g, b.core.b, 0.9f), 0.22f, Mathf.Max(0.025f, th * 0.12f), true);
                if (Random.value < 0.6f)
                    AddMote(e, (Rot(-dir, Random.Range(-70f, 70f)) + Vector2.up * 0.4f) * Random.Range(1.5f, 3.5f), new Color(b.mid.r, b.mid.g, b.mid.b, 1f), Random.Range(0.18f, 0.35f), 0.035f, true);
            }
        }

        private void KillBeam(FxBeam b)
        {
            if (b == null) return;
            DestroyObj(b.body); DestroyObj(b.glow); DestroyObj(b.flare); DestroyObj(b.tip); DestroyObj(b.starA); DestroyObj(b.starB);
            if (b.l != null) DestroyObj(b.l.gameObject);
            b.body = null;
        }

        // ------------------------------------------------------------------ WARNING LINES (replace the Vessel's BeamWarn)
        /// <summary>Band warning — faint fill + 2 blinking edges + a core thickening over time (shows how long is left) + particles running along both edges.
        /// Colour WarnViolet follows the phase (Fire orange / Ice blue / Storm yellow / Shadow violet).</summary>
        private sealed class WarnBand
        {
            public FxBeam band, edgeA, edgeB, fill;
            public Vector2 a, b;
            public float half, t0, dur;
            public Color c;
            public bool dead;
            public void Release() => dead = true;
            public void Aim(Vector2 from, Vector2 to) { a = from; b = to; }
        }

        private Color PhaseWarn(Color c)
        {
            if (c != WarnViolet) return c;
            int p = Mathf.Clamp(phase, 0, 4);
            return p == 1 ? new Color(1f, 0.55f, 0.25f) : p == 2 ? IceWarn : p == 3 ? StormWarn : WarnViolet;
        }

        private WarnBand Warn(Vector2 a, Vector2 b, float half, Color c, float dur)
        {
            c = PhaseWarn(c);
            Color hot = Color.Lerp(c, Color.white, 0.6f);
            var w = new WarnBand { a = a, b = b, half = half, c = c, t0 = Time.time, dur = Mathf.Max(0.05f, dur) };
            w.band = NewBeam(new Color(c.r, c.g, c.b, 0.22f), new Color(c.r, c.g, c.b, 0.16f), new Color(c.r, c.g, c.b, 0.08f), half * 2f, -14, true);
            w.edgeA = NewBeam(hot, c, new Color(c.r, c.g, c.b, 0.3f), 0.05f, -13, true);
            w.edgeB = NewBeam(hot, c, new Color(c.r, c.g, c.b, 0.3f), 0.05f, -13, true);
            w.fill = NewBeam(Color.white, hot, new Color(c.r, c.g, c.b, 0.45f), half * 1.7f, -13, true);
            StartR(RunWarn(w));
            return w;
        }

        private IEnumerator RunWarn(WarnBand w)
        {
            while (!w.dead && !aborted && Time.time - w.t0 < w.dur + 0.6f)
            {
                float t = Time.time - w.t0, k = Mathf.Clamp01(t / w.dur);
                Vector2 d = w.b - w.a;
                float len = Mathf.Max(0.01f, d.magnitude);
                Vector2 n = new Vector2(-d.y, d.x) / len * w.half;
                float blink = k < 0.75f ? 0.8f + 0.2f * Mathf.Sin(t * 12f) : 0.55f + 0.45f * Mathf.Sin(t * 40f);
                SetBeam(w.band, w.a, w.b, 0.9f, 1f);
                SetBeam(w.edgeA, w.a + n, w.b + n, blink, 1f);
                SetBeam(w.edgeB, w.a - n, w.b - n, blink, 1f);
                SetBeam(w.fill, w.a, w.b, 0.2f + 0.6f * k, Mathf.Max(0.04f, k * k));
                if (Random.value < 0.5f)
                {
                    float u = Random.value;
                    Vector2 side = Random.value < 0.5f ? n : -n;
                    AddMote(w.a + d * u + side, d / len * Random.Range(0.8f, 1.8f), new Color(w.c.r, w.c.g, w.c.b, 0.9f), 0.45f, 0.03f, true, null, 0f, 0f, -12);
                }
                yield return null;
            }
            KillBeam(w.band); KillBeam(w.edgeA); KillBeam(w.edgeB); KillBeam(w.fill);
        }

        // ------------------------------------------------------------------ SEAL WARNINGS (rings) + CONES
        private static Sprite sigilSprite, coneSprite;
        /// <summary>SEAL-STYLE WARNING RING — 2 px bright rim + dashed inner ring + 12 engraved ticks (same size as Ring(): 132 px, PPU 100).
        /// NewRing (every Vessel warning ring) and the seal under the hand shadow use this instead of a plain white ring.</summary>
        private static Sprite SigilRing()
        {
            if (sigilSprite != null) return sigilSprite;
            const int R = 64, N = R * 2 + 4;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "vessel_sigil" };
            var px = new Color32[N * N];
            float c = (N - 1) * 0.5f;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = x - c, dy = y - c, r = Mathf.Sqrt(dx * dx + dy * dy);
                    float deg = (Mathf.Atan2(dy, dx) * Mathf.Rad2Deg + 360f) % 360f;
                    float a = 0f;
                    float dOut = Mathf.Abs(r - R);
                    if (dOut < 1f) a = 1f; else if (dOut < 2f) a = 0.6f; else if (dOut < 3.2f) a = 0.2f;
                    if (Mathf.Abs(r - (R - 8)) < 0.8f && (deg % 8f) < 4.5f) a = Mathf.Max(a, 0.55f);       // dashed inner ring
                    float tick = Mathf.Abs(((deg + 15f) % 30f) - 15f);                                      // 12 engraved ticks
                    if (tick < 1.4f && r > R - 6f && r < R - 1f) a = Mathf.Max(a, 0.85f);
                    if (r < R - 10f) a = Mathf.Max(a, 0.06f);                                              // very faint fill inside
                    px[y * N + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px); tex.Apply(false, true);
            return sigilSprite = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>Soft SEARCH CONE (64 x 128, PPU 64 -> 1 wide x 2 tall units, apex at the top — pivot (0.5, 1)): thin bright edge,
        /// denser in the middle fading to the edges, fading near the apex, a bright band at the base (the lit pool). Replaces void/beam_cone for Shadow Watch.</summary>
        private static Sprite ConeSprite()
        {
            if (coneSprite != null) return coneSprite;
            const int W = 64, H = 128;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "vessel_cone" };
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
            {
                float t = (H - 1 - y) / (float)(H - 1);                       // 0 apex -> 1 base
                float hw = Mathf.Max(0.5f, t * W * 0.5f);
                float apex = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.25f));
                for (int x = 0; x < W; x++)
                {
                    float dx = Mathf.Abs(x + 0.5f - W * 0.5f);
                    float a = 0f;
                    if (dx <= hw)
                    {
                        float edge = hw - dx;
                        a = 0.1f + 0.22f * (1f - dx / hw);
                        if (edge < 1.3f) a = 0.75f;
                        if (y < 5) a = Mathf.Max(a, 0.5f);
                        a *= apex;
                    }
                    px[y * W + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255));
                }
            }
            tex.SetPixels32(px); tex.Apply(false, true);
            return coneSprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 1f), 64f);
        }

        /// <summary>Ripple ring SHRINKING INTO the mouth (Devour) — thin ring pulling in from afar to the centre, brightening, then gone.</summary>
        private IEnumerator DevourRipple(Vector2 m)
        {
            var r = NewDot("devour ripple", -12, new Color(0.85f, 0.5f, 1f, 0f));
            Sfx.Play("void_pull", m, 0.4f);                                        // sound
            r.sprite = BandRing();
            for (float t = 0f; t < 0.9f && !aborted; t += Time.deltaTime)
            {
                float u = t / 0.9f, rr = Mathf.Lerp(3.2f, 0.25f, u * u);
                r.transform.position = m;
                r.transform.localScale = new Vector3(rr, rr * 0.5f, 1f);
                r.color = new Color(0.85f, 0.5f, 1f, Mathf.Sin(u * Mathf.PI) * 0.7f);
                yield return null;
            }
            DestroyObj(r);
        }

        // ------------------------------------------------------------------ SEAL UNDER THE HAND SHADOW
        private readonly Dictionary<SpriteRenderer, SpriteRenderer> shadowSigil = new Dictionary<SpriteRenderer, SpriteRenderer>();

        /// <summary>Phase-colour seal ring under the hand shadow (Slam / Stomp / Triple Slam / Grab / Crushing Grip) — shrinks from 1.5x to the hand-print size,
        /// darkens, blinks once locked. Child of the shadow (destroyed with it).</summary>
        private void ShadowSigil(SpriteRenderer shadow, float u, bool locked, float t)
        {
            if (shadow == null || shadow.sprite == null) return;
            if (!shadowSigil.TryGetValue(shadow, out var sg) || sg == null)
            {
                if (shadowSigil.Count > 16) { var dead = new List<SpriteRenderer>(); foreach (var kv in shadowSigil) if (kv.Key == null) dead.Add(kv.Key); foreach (var dk in dead) shadowSigil.Remove(dk); }
                var g = new GameObject("slam sigil");
                g.transform.SetParent(shadow.transform, false);
                sg = g.AddComponent<SpriteRenderer>();
                sg.sprite = SigilRing(); FxLighting.MakeUnlit(sg);
                sg.sortingLayerID = layer; sg.sortingOrder = -14;
                shadowSigil[shadow] = sg;
            }
            Vector3 ps = shadow.transform.localScale;
            float rx = shadow.sprite.bounds.size.x * Mathf.Abs(ps.x) * 0.5f * Mathf.Lerp(1.5f, 1.02f, u);
            float k = rx / (SigilRing().bounds.size.x * 0.5f);
            sg.transform.localScale = new Vector3(k / Mathf.Max(0.001f, Mathf.Abs(ps.x)) * Mathf.Sign(ps.x), k * 0.55f / Mathf.Max(0.001f, ps.y), 1f);
            Color c = Color.Lerp(PhaseColor, Color.white, 0.35f);
            sg.color = new Color(c.r, c.g, c.b, Mathf.Lerp(0.25f, 0.9f, u) * (locked ? 0.6f + 0.4f * Mathf.Sin(t * 40f) : 1f));
        }

        // ------------------------------------------------------------------ MOTES
        private sealed class Mote
        {
            public SpriteRenderer sr;
            public Vector2 p, v, pull;          // pull: attraction centre (spiral in)
            public float born, life, size, swirl, pullK;
            public Color c;
            public bool stretch;
        }
        private readonly List<Mote> motes = new List<Mote>();

        /// <summary>Emit one particle. swirl &gt; 0 = spiral around `pull` (accretion disc / suction), stretch = streak along the velocity.</summary>
        private void AddMote(Vector2 p, Vector2 v, Color c, float life, float size, bool stretch = false, Vector2? pull = null, float pullK = 0f, float swirl = 0f, int ord = 0)
        {
            if (motes.Count > 260) return;
            var g = new GameObject("mote");
            fieldObjs.Add(g);
            var sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = MarginArt.Pixel(); FxLighting.MakeUnlit(sr);
            sr.sortingLayerID = layer; sr.sortingOrder = ord != 0 ? ord : order + 5;
            sr.color = c;
            g.transform.position = p;
            motes.Add(new Mote { sr = sr, p = p, v = v, c = c, born = Time.time, life = life, size = size, stretch = stretch, pull = pull ?? p, pullK = pullK, swirl = swirl });
        }

        /// <summary>Every frame (TickFight).</summary>
        private void TickMotes(float dt)
        {
            float now = Time.time;
            for (int i = motes.Count - 1; i >= 0; i--)
            {
                var m = motes[i];
                float u = (now - m.born) / m.life;
                if (m.sr == null || u >= 1f) { if (m.sr != null) DestroyObj(m.sr.gameObject); motes.RemoveAt(i); continue; }
                if (m.pullK > 0f)
                {
                    Vector2 d = m.pull - m.p;
                    float r = Mathf.Max(0.05f, d.magnitude);
                    Vector2 tang = new Vector2(-d.y, d.x) / r;
                    m.v = Vector2.Lerp(m.v, d / r * m.pullK * (1.2f / Mathf.Sqrt(r)) + tang * m.swirl / Mathf.Sqrt(r), 1f - Mathf.Exp(-6f * dt));
                    if (r < 0.08f) u = 1f;
                }
                m.p += m.v * dt;
                var t = m.sr.transform;
                t.position = m.p;
                float s = m.size * (1f - u * 0.5f);
                if (m.stretch && m.v.sqrMagnitude > 0.01f)
                {
                    t.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(m.v.y, m.v.x) * Mathf.Rad2Deg);
                    t.localScale = new Vector3(s * (1f + Mathf.Min(4f, m.v.magnitude * 1.6f)), s, 1f);
                }
                else { t.rotation = Quaternion.identity; t.localScale = new Vector3(s, s, 1f); }
                float a = u < 0.15f ? u / 0.15f : 1f - (u - 0.15f) / 0.85f;
                m.sr.color = new Color(m.c.r, m.c.g, m.c.b, m.c.a * a);
            }
        }

        private void KillMotes()
        {
            foreach (var m in motes) if (m.sr != null) DestroyObj(m.sr.gameObject);
            motes.Clear();
        }

        // ------------------------------------------------------------------ forked lightning
        /// <summary>Real lightning (not a laser): jagged body re-jittering every frame + 2–3 short forks, white flash, sparks at the impact point.</summary>
        private IEnumerator ForkBolt(Vector2 a, Vector2 b, float seconds)
        {
            var main = MakeArc(a, b, 10);
            var forks = new List<VArc>();
            int nf = Random.Range(2, 4);
            for (int k = 0; k < nf; k++)
            {
                float u = Random.Range(0.3f, 0.8f);
                Vector2 p = Vector2.Lerp(a, b, u);
                Vector2 dir = (b - a).normalized;
                Vector2 side = new Vector2(-dir.y, dir.x) * (Random.value < 0.5f ? -1f : 1f);
                forks.Add(MakeArc(p, p + (dir * 0.5f + side * Random.Range(0.4f, 0.9f)) * Random.Range(0.6f, 1.1f), 4, false));
            }
            for (int k = 0; k < 10; k++)
                AddMote(b, Random.insideUnitCircle.normalized * Random.Range(1.5f, 4f), new Color(1f, 0.95f, 0.6f, 1f), Random.Range(0.2f, 0.45f), 0.045f, true);
            for (float t = 0f; t < seconds && main.Halo != null; t += Time.deltaTime)
            {
                float a1 = t < 0.06f ? 1f : (Mathf.Sin(t * 60f) > -0.3f ? 1f - t / seconds * 0.7f : 0.25f);   // lightning-style flicker
                main.Alpha = a1; main.Jag = 0.2f; JitterArc(main, false);
                foreach (var f in forks) { f.Alpha = a1 * 0.8f; f.Jag = 0.25f; JitterArc(f, false); }
                yield return null;
            }
            KillArc(main);
            foreach (var f in forks) KillArc(f);
        }
    }
}
