using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// ABSALOM's body in the Hall of Naming (not an enemy: no HP, no AI — AbsalomHallMechanism drives it).
    /// Sprites = PixelLab NPC `Resources/NPCs/hooded_one/&lt;anim&gt;_&lt;S|E|N&gt;_NN` (idle / walk in 3 directions, strike only E; W = flipped E).
    /// Blue halo (Light2D) = the Ascended's left hand shielding him. Lift = height above his feet (Pos does not change).
    /// REAL FLIGHT — when Airborne is true: idle / walk -> `hover` (floating, robe flowing, no steps), strike -> `cast` (raises both hands,
    /// charges, releases); also `ascend` (shoots up into the sky) and `dive` (drops down, crouching on impact). PixelLab 29e6e52c (canvas 156/160, pivot at feet).
    /// ROUND SHADOW on the floor at Pos: smaller and fainter the higher he flies — the player reads his height from the shadow.
    /// MOVING FLIGHT — walking while airborne -> `fly` (body leans into the direction, robe and hood stream behind; PixelLab
    /// a7ddb212 E / cf851d99 S / 55b114cd N, canvas 160/156, same pivot as hover) + a pale blue AFTERIMAGE trail.
    /// No hover sprite for N yet -> uses fly_N (back to the camera, robe swaying).
    /// Called by: AbsalomHallMechanism (Play / Face / Lift / Airborne / Scale every frame).
    /// </summary>
    internal sealed class AbsalomActor
    {
        public readonly SpriteRenderer Sr;
        public Vector2 Pos;
        public float Lift, Alpha = 1f;
        public const float Scale = 1.15f;
        /// <summary>Size factor — outside the fight he is only REN'S HEIGHT (~0.57); the "transformation" intro scales him up to 1 (boss size).</summary>
        public float Size = 1f;
        private Sprite[] frames;
        private string anim = "", dir = "S";
        private bool flip, loop = true;
        private float fps = 6f, start, flashUntil, flashLen = 0.1f;
        private Color flashColor = Color.white;
        private readonly Light2D halo;
        private readonly SpriteRenderer shadow;
        private static Sprite shadowSprite;
        private bool airborne;

        /// <summary>Flying — switches sprite set (hover / cast) and shows the floor shadow.</summary>
        public bool Airborne
        {
            get => airborne;
            set
            {
                if (airborne == value) return;
                airborne = value;
                string a = baseAnim.Length > 0 ? baseAnim : "idle";
                anim = "";
                Play(a, fps, loop);
            }
        }

        private string baseAnim = "";
        private static readonly Dictionary<string, Sprite[]> cache = new Dictionary<string, Sprite[]>();
        /// <summary>Afterimage trail while gliding (copies of the current frame, fading over 0.32 s).</summary>
        private readonly Transform parent;
        private readonly List<(SpriteRenderer sr, float born)> ghosts = new List<(SpriteRenderer, float)>();
        private float nextGhost;
        public Color TrailColor = new Color(0.55f, 0.78f, 1f);

        public AbsalomActor(Vector2 pos, Transform parent, Transform player)
        {
            Pos = pos;
            this.parent = parent;
            Sr = TrapArt.MakeRenderer(parent, "Absalom", pos, player, 0);
            Sr.transform.localScale = new Vector3(Scale, Scale, 1f);
            var lg = new GameObject("Absalom Halo");
            lg.transform.SetParent(Sr.transform, false);
            lg.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            halo = lg.AddComponent<Light2D>();
            halo.lightType = Light2D.LightType.Point;
            halo.color = new Color(0.55f, 0.78f, 1f);
            halo.pointLightOuterRadius = 1.4f; halo.pointLightInnerRadius = 0.2f;
            halo.intensity = 0.75f;
            // round shadow under his feet (Default layer, above the floor, below characters)
            var sg = new GameObject("Absalom Shadow");
            sg.transform.SetParent(parent, false);
            shadow = sg.AddComponent<SpriteRenderer>();
            shadow.sprite = ShadowSprite;
            shadow.sortingOrder = -3;
            shadow.color = new Color(0f, 0f, 0f, 0f);
            Play("idle", 5f);
            Tick();
        }

        private static Sprite[] Load(string a, string d)
        {
            string key = a + "_" + d;
            if (cache.TryGetValue(key, out var f) && f != null && f.Length > 0) return f;
            var list = new List<Sprite>();
            for (int i = 0; i < 40; i++)
            {
                var s = Resources.Load<Sprite>("NPCs/hooded_one/" + key + "_" + i.ToString("00"));
                if (s == null) break;
                list.Add(s);
            }
            f = list.ToArray();
            cache[key] = f;
            return f;
        }

        /// <summary>Soft ellipse 64 x 20 px (solid centre, fading edge).</summary>
        private static Sprite ShadowSprite
        {
            get
            {
                if (shadowSprite != null) return shadowSprite;
                const int w = 64, h = 20;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                var px = new Color32[w * h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float dx = (x - (w - 1) * 0.5f) / (w * 0.5f), dy = (y - (h - 1) * 0.5f) / (h * 0.5f);
                        float r = Mathf.Sqrt(dx * dx + dy * dy);
                        byte a = r > 1f ? (byte)0 : r > 0.75f ? (byte)110 : (byte)190;       // 2 pixel-style steps
                        px[y * w + x] = new Color32(0, 0, 0, a);
                    }
                tex.SetPixels32(px); tex.Apply();
                shadowSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
                return shadowSprite;
            }
        }

        /// <summary>Change animation (strike only exists for E — other directions use E / flipped). Airborne -> idle / walk = hover, strike = cast.</summary>
        public void Play(string name, float framesPerSecond, bool looping = true)
        {
            baseAnim = name;
            if (airborne)
            {
                if (name == "idle") name = "hover";
                else if (name == "walk") name = "fly";                              // gliding through the air = flight animation
                else if (name == "strike") name = "cast";
            }
            string d = name == "strike" ? "E" : dir;
            var f = Load(name, d);
            if (f.Length == 0 && name == "hover" && d == "N") f = Load("fly", "N");   // no hover N yet
            if (f.Length == 0) f = Load(name, "S");
            if (f.Length == 0) return;
            if (name == anim && looping == loop && frames == f) { fps = framesPerSecond; return; }
            anim = name; frames = f; fps = framesPerSecond; loop = looping; start = Time.time;
        }

        public string Anim => anim;
        public bool AnimDone => !loop && frames != null && (Time.time - start) * fps >= frames.Length;
        public Vector2 Body => Pos + Vector2.up * (0.42f * Size + Lift);

        /// <summary>Face direction d (S / N / E, W = flipped E).</summary>
        public void Face(Vector2 d)
        {
            if (d.sqrMagnitude < 0.0001f) return;
            string nd; bool nf = false;
            if (Mathf.Abs(d.x) > Mathf.Abs(d.y) * 0.8f) { nd = "E"; nf = d.x < 0f; }
            else nd = d.y > 0f ? "N" : "S";
            flip = nf;
            if (nd == dir) return;
            dir = nd;
            string a = baseAnim.Length > 0 ? baseAnim : anim; anim = "";   // base name (idle / strike) so Airborne can swap to the right sprite set
            Play(a.Length > 0 ? a : "idle", fps, loop);
        }

        public void Flash(Color c, float seconds) { flashColor = c; flashLen = Mathf.Max(0.01f, seconds); flashUntil = Time.time + seconds; }

        public void HaloPulse(float intensity) { if (halo != null) halo.intensity = intensity; }

        public void Tick()
        {
            if (Sr == null) return;
            Sr.transform.position = Pos + Vector2.up * Lift;
            Sr.transform.localScale = new Vector3(Scale * Size, Scale * Size, 1f);
            if (frames != null && frames.Length > 0)
            {
                int i = (int)((Time.time - start) * fps);
                Sr.sprite = frames[loop ? i % frames.Length : Mathf.Min(i, frames.Length - 1)];
            }
            Sr.flipX = flip && (anim == "strike" || dir == "E");
            TickGhosts();
            var c = Color.white;
            if (Time.time < flashUntil) c = Color.Lerp(Color.white, flashColor, (flashUntil - Time.time) / flashLen);
            c.a = Alpha;
            Sr.color = c;
            if (shadow != null)
            {
                // floor shadow — smaller and fainter the higher he flies; on the ground (Lift ~0) almost hidden (feet touch the floor)
                float h = Mathf.Max(0f, Lift);
                float s = Mathf.Lerp(1.05f, 0.45f, Mathf.Clamp01(h / 4f));
                shadow.transform.position = Pos + Vector2.up * 0.02f;
                shadow.transform.localScale = new Vector3(s * Size, s * Size, 1f);
                float a = h < 0.03f ? 0f : Mathf.Lerp(0.55f, 0.18f, Mathf.Clamp01(h / 4f)) * Mathf.Clamp01(h / 0.12f);
                shadow.color = new Color(0f, 0f, 0f, a * Mathf.Max(Alpha, h > 1.5f ? 1f : 0f));
            }
            if (halo != null)
            {
                halo.enabled = Alpha > 0.05f;
                halo.intensity = Mathf.MoveTowards(halo.intensity, 0.75f + 0.15f * Mathf.Sin(Time.time * 2.4f), Time.deltaTime * 2f);
            }
        }

        /// <summary>Gliding -> every 0.05 s leave one afterimage (same frame, tinted TrailColor, fading over 0.32 s, behind him).</summary>
        private void TickGhosts()
        {
            for (int i = ghosts.Count - 1; i >= 0; i--)
            {
                var g = ghosts[i];
                float k = (Time.time - g.born) / 0.32f;
                if (g.sr == null || k >= 1f) { if (g.sr != null) Object.Destroy(g.sr.gameObject); ghosts.RemoveAt(i); continue; }
                g.sr.color = new Color(TrailColor.r, TrailColor.g, TrailColor.b, 0.42f * (1f - k) * (1f - k) * Alpha);
            }
            if (anim != "fly" || Alpha < 0.5f || Time.time < nextGhost || Sr.sprite == null) return;
            nextGhost = Time.time + 0.05f;
            var go = new GameObject("Absalom Afterimage");
            go.transform.SetParent(parent, false);
            go.transform.position = Sr.transform.position;
            go.transform.localScale = Sr.transform.localScale;
            var s = go.AddComponent<SpriteRenderer>();
            SoulRequiem.Core.FxLighting.MakeUnlit(s);
            s.sprite = Sr.sprite;
            s.flipX = Sr.flipX;
            s.sortingLayerID = Sr.sortingLayerID;
            s.sortingOrder = Sr.sortingOrder - 1;
            s.spriteSortPoint = Sr.spriteSortPoint;
            s.color = new Color(TrailColor.r, TrailColor.g, TrailColor.b, 0.42f * Alpha);
            ghosts.Add((s, Time.time));
        }

        public void Destroy()
        {
            foreach (var g in ghosts) if (g.sr != null) Object.Destroy(g.sr.gameObject);
            ghosts.Clear();
            if (Sr != null) Object.Destroy(Sr.gameObject);
            if (shadow != null) Object.Destroy(shadow.gameObject);
        }
    }
}
