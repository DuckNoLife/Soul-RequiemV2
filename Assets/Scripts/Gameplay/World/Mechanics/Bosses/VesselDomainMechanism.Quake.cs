using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    ///   SLAM HIT AREA = EXACTLY THE HAND SHADOW (<see cref="InHandPrint"/>): ellipse from the hand sprite x rig.Scale, squashed 0.45 like the warning shadow.
    ///     The shadow shows where it hurts; leaving the shadow dodges it. Slam / Stomp have NO wave ring.
    ///   QUAKE WAVE (<see cref="QuakeWave"/>) — only TRIPLE SLAM / THUNDER DRUM: fixed reach (hits 1–2: 1.4 u, hit 3: 2.2 u), travels at 3.4 u/s (readable),
    ///     hits when the ring EDGE passes Ren's feet (roll through the edge, or stand outside its reach). VFX: PixelLab crack at the centre (vc_quake_crack,
    ///     tinted per phase), soft dust ring with a bright rim, CRACKS crawling along the ring (bright phase-colour core, dark edges, forks), pebbles bouncing
    ///     at the rim, rolling dust.
    /// Called by: Slam / Stomp / TripleSlam / Grab (.Physical.cs).
    /// </summary>
    public sealed partial class VesselDomainMechanism
    {
        private static Sprite quakeBandSprite;

        /// <summary>Ren's feet are inside the shadow of hand `side` slamming down at `at`.</summary>
        private bool InHandPrint(Vector2 at, int side, string hand)
        {
            var s = VesselRig.Load(hand, true);
            float S = rig != null ? rig.Scale : 1.6f, k = Float("print_k", 0.8f), pad = 0.08f;
            Vector2 c = at, e = new Vector2(0.6f, 0.3f);
            if (s != null)
            {
                var b = s.bounds;
                c = at + new Vector2(b.center.x * S * (side > 0 ? -1f : 1f), b.center.y * S * 0.45f);
                e = new Vector2(b.extents.x * S * k, b.extents.y * S * 0.45f * k);
            }
            e += new Vector2(pad, pad * 0.6f);
            Vector2 d = Feet - c;
            return (d.x * d.x) / (e.x * e.x) + (d.y * d.y) / (e.y * e.y) <= 1f;
        }

        private sealed class Fissure
        {
            public float ang, stop, next;
            public Vector2 tip;
            public readonly List<SpriteRenderer> dark = new List<SpriteRenderer>(), core = new List<SpriteRenderer>();
        }

        private sealed class Pebble
        {
            public SpriteRenderer sr;
            public Vector2 p, v;
            public float h, vh, life, age;
        }

        /// <summary>Quake wave from `at` out to radius `r1` (ellipse squashed 0.62 like the angled floor). tier = strength (the last hit is bigger).</summary>
        private IEnumerator QuakeWave(Vector2 at, float r1, float dmg, int tier)
        {
            const float R0 = 0.3f, Sq = 0.62f, Step = 0.16f;
            float speed = Float("quake_speed", 3.4f);
            Color pc = PhaseColor;
            Color core = Color.Lerp(pc, Color.white, 0.35f);
            Color dust = Color.Lerp(new Color(0.78f, 0.74f, 0.68f), pc, 0.25f);
            SpriteVfx.Spawn("vc_quake_crack", at, 1f + 0.3f * tier, layer, -13, Random.value < 0.5f, 16f, false)?.Tint(Color.Lerp(pc, Color.white, 0.55f));
            Sfx.Play(tier >= 2 ? "boss_slam" : "rock_crash", at, tier >= 2 ? 1f : 0.75f);   // sound
            Sfx.Play("earth_fissure", at, 0.6f + 0.15f * tier);

            var g = new GameObject("quake wave");
            fieldObjs.Add(g);
            g.transform.position = at;
            var band = g.AddComponent<SpriteRenderer>();
            band.sprite = QuakeBand(); FxLighting.MakeUnlit(band);
            band.sortingLayerID = layer; band.sortingOrder = -14;

            int n = Mathf.RoundToInt(Mathf.Lerp(6f, 10f, Mathf.Clamp01(r1 / 2.4f)));
            float a0 = Random.Range(0f, 360f);
            var rays = new List<Fissure>();
            for (int i = 0; i < n; i++)
                rays.Add(new Fissure { ang = a0 + i * 360f / n + Random.Range(-14f, 14f), tip = at, stop = r1 * Random.Range(0.6f, 1f), next = R0 });
            var pebbles = new List<Pebble>();
            bool hit = false;
            float r = R0, nextDust = 0f, nextPeb = 0f;
            float half = QuakeBand().bounds.size.x * 0.5f;

            while (r < r1 && !aborted)
            {
                float dt = Time.deltaTime, now = Time.time;
                r = Mathf.Min(r1, r + speed * dt);
                float u = (r - R0) / Mathf.Max(0.01f, r1 - R0);
                float k = r / half;
                g.transform.localScale = new Vector3(k, k * Sq, 1f);
                band.color = new Color(dust.r, dust.g, dust.b, 0.85f * (1f - u * u));
                // hit: ring edge passes Ren's feet
                Vector2 d = Feet - at;
                float rr = new Vector2(d.x, d.y / Sq).magnitude;
                if (!hit && dmg > 0f && Mathf.Abs(rr - r) < 0.15f) hit = Hurt(dmg, at, 0.8f);
                // cracks crawl along the ring
                foreach (var f in rays)
                    while (f.next <= Mathf.Min(r, f.stop))
                    {
                        float ang = (f.ang + Random.Range(-16f, 16f)) * Mathf.Deg2Rad;
                        Vector2 to = at + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang) * Sq) * f.next;
                        AddCrack(f, f.tip, to, core);
                        if (Random.value < 0.2f && f.next > 0.5f)                       // short fork
                        {
                            float b = ang + Random.Range(0.5f, 0.9f) * (Random.value < 0.5f ? -1f : 1f);
                            AddCrack(f, to, to + new Vector2(Mathf.Cos(b), Mathf.Sin(b) * Sq) * Random.Range(0.12f, 0.24f), core);
                        }
                        f.tip = to;
                        f.next += Step;
                    }
                // pebbles bounce + dust rolls at the rim
                if (now >= nextPeb)
                {
                    nextPeb = now + 0.035f;
                    for (int j = 0; j < 2; j++)
                    {
                        float ang = Random.Range(0f, Mathf.PI * 2f);
                        Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang) * Sq);
                        pebbles.Add(NewPebble(at + dir * r, dir * Random.Range(0.4f, 0.9f), dust));
                    }
                }
                if (now >= nextDust)
                {
                    nextDust = now + 0.09f;
                    float ang = Random.Range(0f, Mathf.PI * 2f);
                    Vector2 p = at + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang) * Sq) * r;
                    SpriteVfx.Spawn("m3_dash_dust", p, Random.Range(0.45f, 0.65f), layer, p.y < Feet.y ? order + 1 : order - 1, Mathf.Cos(ang) < 0f, 16f, false)?.Tint(dust);
                }
                TickPebbles(pebbles, dt);
                yield return null;
            }
            DestroyObj(g);
            // cracks cool down: bright core fades in 0.5 s, dark edges in 1.4 s
            for (float t = 0f; t < 1.4f && !aborted; t += Time.deltaTime)
            {
                float ca = Mathf.Clamp01(1f - t / 0.5f), da = Mathf.Clamp01(1f - t / 1.4f);
                foreach (var f in rays)
                {
                    foreach (var s in f.core) if (s != null) s.color = new Color(core.r, core.g, core.b, ca);
                    foreach (var s in f.dark) if (s != null) s.color = new Color(0.07f, 0.05f, 0.05f, 0.8f * da);
                }
                TickPebbles(pebbles, Time.deltaTime);
                yield return null;
            }
            foreach (var f in rays) { foreach (var s in f.core) DestroyObj(s); foreach (var s in f.dark) DestroyObj(s); }
            foreach (var p in pebbles) DestroyObj(p.sr);
        }

        private void AddCrack(Fissure f, Vector2 a, Vector2 b, Color core)
        {
            var dk = NewLine("quake crack", -14);
            StretchLine(dk, a, b, 0.05f);
            dk.color = new Color(0.07f, 0.05f, 0.05f, 0.8f);
            f.dark.Add(dk);
            var c = NewLine("quake crack glow", -13);
            StretchLine(c, a, b, 0.018f);
            c.color = core;
            f.core.Add(c);
        }

        private Pebble NewPebble(Vector2 at, Vector2 v, Color dust)
        {
            var sr = NewLine("quake pebble", order);
            float s = Random.Range(0.03f, 0.055f);
            sr.transform.localScale = new Vector3(s * 100f, s * 100f, 1f);
            float shade = Random.Range(0.45f, 0.85f);
            sr.color = new Color(dust.r * shade, dust.g * shade, dust.b * shade, 1f);
            return new Pebble { sr = sr, p = at, v = v, vh = Random.Range(1.2f, 2.2f), life = Random.Range(0.45f, 0.7f) };
        }

        private void TickPebbles(List<Pebble> list, float dt)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var b = list[i];
                b.age += dt;
                if (b.sr == null || b.age >= b.life) { DestroyObj(b.sr); list.RemoveAt(i); continue; }
                b.vh -= 9f * dt;
                b.h = Mathf.Max(0f, b.h + b.vh * dt);
                if (b.h <= 0f && b.vh < 0f) { b.vh = -b.vh * 0.35f; b.v *= 0.5f; }
                b.p += b.v * dt;
                b.sr.transform.position = b.p + Vector2.up * b.h;
                b.sr.sortingOrder = b.p.y < Feet.y ? order + 1 : order - 1;
                var c = b.sr.color; c.a = Mathf.Clamp01((b.life - b.age) / 0.2f); b.sr.color = c;
            }
        }

        /// <summary>Dust ring: bright 2 px outer rim, fading inside (dust trailing the wave front). White — tinted when drawn. Radius 64 px.</summary>
        private static Sprite QuakeBand()
        {
            if (quakeBandSprite != null) return quakeBandSprite;
            const int R = 64, N = R * 2 + 4;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "vessel_quake_band" };
            var px = new Color32[N * N];
            float c = (N - 1) * 0.5f;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    float a;
                    if (d > R + 1f) a = 0f;
                    else if (d > R - 2f) a = 1f;                                                    // bright rim
                    else { float w = (R - 2f - d) / 14f; a = w < 1f ? 0.42f * (1f - w) * (1f - w) : 0f; }   // dust behind the rim
                    // step the alpha to suit pixel art
                    a = Mathf.Round(a * 6f) / 6f;
                    px[y * N + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(px); tex.Apply(false, true);
            return quakeBandSprite = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
