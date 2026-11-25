using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// sluice - sluice valve: slash (or press T on) the valve -> cold water floods down a lane for `flow_seconds` seconds,
    /// then the pipe needs `cooldown` seconds to refill. Shared by every map; bosses / enemies ask <see cref="Wet"/> to react to water
    /// (e.g. the Ether Ram charging through the water lane -> hisses, stunned). Clean water: Ren loses no HP.
    /// Tiled properties (object in the mechanics layer; object area = the valve the player slashes):
    ///   lane_a, lane_b   two opposite corners of the water lane, "x,y" in Tiled tiles (e.g. "95.5,85.6" and "97.5,100")
    ///   flow_seconds     water flow time (default 3)
    ///   cooldown         refill time after the water stops (default 10)
    ///   hint             text shown the first time Ren gets close to the valve; text = text on the first opening; sets = flag set on the first opening (optional)
    ///   outlet           "x,y" Tiled tile of the pipe mouth - the falling stream starts exactly here and drops to the lane's top edge (default: valve centre)
    /// <see cref="Alert"/> = true (a water-fearing boss is in combat, e.g. the Ether Ram) -> ready valves get a bobbing arrow + strong glow.
    /// Images: Resources VFX/props/sluice_stream_0..7 (water pouring from the pipe), sluice_splash_0..7 (foam at the wall base),
    ///       sluice_ripple_0..7 (ripples at the lane end) - cut from the EPIC Sewers pack, tinted clean-water blue. The water surface is drawn in code.
    /// </summary>
    public sealed class SluiceMechanism : Mechanism
    {
        private static readonly List<SluiceMechanism> all = new List<SluiceMechanism>();
        private static Sprite[] waterFrames, streamFrames, splashFrames, rippleFrames;

        private enum State { Ready, Flowing, Draining, Refill }
        private State state = State.Ready;
        private float stateT, flowSeconds, cooldown, fill;
        private bool announced, openedOnce;
        private Rect lane;
        private SpriteRenderer water, stream, splash, ripple, arrow;
        private Light2D glow;
        private Sprite[] myStream;                                      // the stream image cropped to the pipe-mouth distance -> water surface
        private Vector2 outlet;
        private static Sprite arrowSprite;

        /// <summary>Mark every valve that matters (a water-fearing boss in combat): arrow + strong glow while the valve is ready.</summary>
        public static bool Alert { get; set; }

        private const float FillTime = 0.35f, DrainTime = 0.4f;

        public override string DefaultVerb => state == State.Ready ? "Open" : null;
        public override bool ReactsToStrike => true;
        public override bool Usable => state == State.Ready;

        /// <summary>Point p (world) is inside the flowing water lane of some valve.</summary>
        public static bool Wet(Vector2 p)
        {
            for (int i = all.Count - 1; i >= 0; i--)
            {
                var m = all[i];
                if (m == null || m.Ctx == null || m.Ctx.Host == null) { all.RemoveAt(i); continue; }
                if (m.WetAt(p)) return true;
            }
            return false;
        }

        /// <summary>Segment a -> b crosses a flowing water lane (so "smart" enemies avoid charging into water).</summary>
        public static bool CrossesFlow(Vector2 a, Vector2 b)
        {
            for (int i = 0; i < 12; i++)
                if (Wet(Vector2.Lerp(a, b, i / 11f))) return true;
            return false;
        }

        /// <summary>Is any valve flowing.</summary>
        public static bool AnyFlowing
        {
            get
            {
                foreach (var m in all) if (m != null && m.Ctx != null && m.Ctx.Host != null && m.state == State.Flowing) return true;
                return false;
            }
        }

        public override void Init()
        {
            flowSeconds = Mathf.Max(0.5f, Float("flow_seconds", 3f));
            cooldown = Mathf.Max(0f, Float("cooldown", 10f));
            Vector2 a = Ctx.TilePoint(Str("lane_a", ""), Rect.center), b = Ctx.TilePoint(Str("lane_b", ""), Rect.center);
            lane = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
            outlet = Ctx.TilePoint(Str("outlet", ""), Rect.center);
            if (lane.width < 0.05f || lane.height < 0.05f) Debug.LogWarning("[Sluice] " + Name + ": missing lane_a / lane_b");
            all.RemoveAll(m => m == null || m.Ctx == null || m.Ctx.Host == null);
            all.Add(this);
            Build();
        }

        public override void Tick(Vector2 playerPos, bool inside)
        {
            if (announced || Vector2.Distance(playerPos, Rect.center) > 1.1f) return;
            announced = true;
            Hints.Show(Str("hint", "A sluice valve. Strike it to flush the channel with cold water."), 3f);
        }

        public override void OnStruck() => Open();
        public override void Use() => Open();

        private void Open()
        {
            if (state != State.Ready)
            {
                if (state == State.Refill) Hints.Show("The pipe gurgles - still refilling.", 1.5f);
                return;
            }
            state = State.Flowing;
            stateT = 0f;
            fill = 0f;
            CameraFollow.KickMain(Vector2.down, 0.04f, 0.2f);
            Sfx.Play("water_rush", lane.center, 1f);                               // sound
            if (!openedOnce)
            {
                openedOnce = true;
                Hints.Show(Str("text", "Cold water roars down the channel!"), 2.5f);
                string sets = Str("sets", "");
                if (sets.Length > 0) Ctx.Flags.Set(sets);
            }
        }

        private bool WetAt(Vector2 p)
        {
            if (state != State.Flowing || fill <= 0f) return false;
            if (p.x < lane.xMin || p.x > lane.xMax) return false;
            float front = lane.yMax - lane.height * fill;                    // water flows down from the top edge (wall)
            return p.y <= lane.yMax + 0.1f && p.y >= front;
        }

        public override void Animate()
        {
            if (water == null) return;
            float dt = Time.deltaTime;
            stateT += dt;
            switch (state)
            {
                case State.Flowing:
                    fill = Mathf.Min(1f, stateT / FillTime);
                    if (stateT >= flowSeconds) { state = State.Draining; stateT = 0f; }
                    break;
                case State.Draining:
                    if (stateT >= DrainTime) { state = State.Refill; stateT = 0f; fill = 0f; }
                    break;
                case State.Refill:
                    if (stateT >= cooldown) { state = State.Ready; stateT = 0f; }
                    break;
            }

            bool on = state == State.Flowing || state == State.Draining;
            float alpha = state == State.Draining ? 1f - stateT / DrainTime : 1f;
            int f = Mathf.FloorToInt(Time.time * 12f);
            SetOn(water, on);
            SetOn(stream, on);
            SetOn(splash, on);
            SetOn(ripple, on && fill >= 1f);
            if (on)
            {
                float h = Mathf.Max(0.01f, lane.height * (state == State.Draining ? 1f : fill));
                water.size = new Vector2(lane.width, h);
                water.sprite = waterFrames[f % waterFrames.Length];
                water.color = new Color(1f, 1f, 1f, alpha);
                if (stream != null) { stream.sprite = myStream[f % myStream.Length]; stream.color = water.color; }
                if (splash != null) { splash.sprite = splashFrames[f % splashFrames.Length]; splash.color = water.color; }
                if (ripple != null) { ripple.sprite = rippleFrames[f % rippleFrames.Length]; ripple.color = water.color; }
            }

            bool alert = Alert && state == State.Ready;
            if (glow != null)
            {
                glow.enabled = state != State.Refill;
                glow.intensity = state == State.Ready ? (alert ? 1.3f + 0.7f * Mathf.Sin(Time.time * 6f) : 0.55f + 0.3f * Mathf.Sin(Time.time * 3f))
                                                      : 1.1f * alpha;
                glow.pointLightOuterRadius = alert ? Float("light_radius", 1.3f) * 1.6f : Float("light_radius", 1.3f);
            }
            if (arrow != null)
            {
                SetOn(arrow, alert);
                if (alert)
                {
                    float bob = Mathf.Abs(Mathf.Sin(Time.time * 4f)) * 0.08f;
                    arrow.transform.position = new Vector3(outlet.x, outlet.y + 0.42f + bob, 0f);
                    arrow.color = new Color(0.6f, 0.9f, 1f, 0.75f + 0.25f * Mathf.Sin(Time.time * 8f));
                }
            }
        }

        private static void SetOn(SpriteRenderer r, bool on)
        {
            if (r != null && r.enabled != on) r.enabled = on;
        }

        // ------------------------------------------------------------------ images
        private void Build()
        {
            LoadSprites();
            var psr = Ctx.Player != null ? Ctx.Player.GetComponentInChildren<SpriteRenderer>() : null;
            int layer = psr != null ? psr.sortingLayerID : 0, order = psr != null ? psr.sortingOrder : 0;

            water = NewRenderer("Sluice Water " + Name, layer, order - 2);   // below characters' feet, above the Ram's poison trail
            water.drawMode = SpriteDrawMode.Tiled;
            water.sprite = waterFrames[0];
            water.transform.position = new Vector3(lane.center.x, lane.yMax, 0f);   // top-edge pivot: the water grows downward
            water.size = new Vector2(lane.width, 0.01f);
            water.enabled = false;

            if (streamFrames.Length > 0)
            {
                // the original image is 2 tiles tall; with its feet at the lane edge it sticks up past the pipe.
                // Keep only the top part of the image (where the water just leaves the pipe), exactly as long as the pipe mouth -> lane top edge distance.
                float gap = Mathf.Max(0.04f, outlet.y - lane.yMax);
                myStream = new Sprite[streamFrames.Length];
                for (int i = 0; i < streamFrames.Length; i++) myStream[i] = CropTop(streamFrames[i], gap);
                stream = NewRenderer("Sluice Stream " + Name, layer, order);
                stream.spriteSortPoint = SpriteSortPoint.Pivot;               // stream base = wall edge: Ren standing south draws over it
                stream.sprite = myStream[0];
                stream.transform.position = new Vector3(lane.center.x, lane.yMax, 0f);
                stream.enabled = false;
            }
            if (splashFrames.Length > 0)
            {
                splash = NewRenderer("Sluice Splash " + Name, layer, order - 1);
                splash.sprite = splashFrames[0];
                splash.transform.position = new Vector3(lane.center.x, lane.yMax - 0.3f, 0f);
                splash.enabled = false;
            }
            if (rippleFrames.Length > 0)
            {
                ripple = NewRenderer("Sluice Ripple " + Name, layer, order - 1);
                ripple.sprite = rippleFrames[0];
                ripple.transform.position = new Vector3(lane.center.x, lane.yMin + 0.02f, 0f);
                ripple.enabled = false;
            }

            arrow = NewRenderer("Sluice Arrow " + Name, layer, order + 8);
            arrow.sprite = ArrowSprite;
            arrow.enabled = false;

            var lg = new GameObject("Sluice Glow " + Name);
            lg.transform.SetParent(Ctx.Host.transform, false);
            lg.transform.position = new Vector3(Rect.center.x, Rect.center.y, 0f);
            glow = lg.AddComponent<Light2D>();
            glow.lightType = Light2D.LightType.Point;
            glow.color = MechanismContext.ParseColor(Str("light_color", "#7fd0ff"), 1f);
            glow.pointLightOuterRadius = Float("light_radius", 1.3f);
            glow.pointLightInnerRadius = 0.05f;
            glow.intensity = 0.6f;
        }

        private SpriteRenderer NewRenderer(string name, int layer, int order)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(Ctx.Host.transform, true);
            sr.sortingLayerID = layer;
            sr.sortingOrder = order;
            FxLighting.MakeUnlit(sr);                                         // the water glows slightly by itself, readable in a dark room
            return sr;
        }

        private static void LoadSprites()
        {
            if (waterFrames != null) return;
            streamFrames = LoadSeq("VFX/props/sluice_stream_");
            splashFrames = LoadSeq("VFX/props/sluice_splash_");
            rippleFrames = LoadSeq("VFX/props/sluice_ripple_");
            waterFrames = new Sprite[4];
            for (int i = 0; i < waterFrames.Length; i++) waterFrames[i] = MakeWater(i);
        }

        /// <summary>Keep the top `keep` units of the image, pivot at the bottom-centre (image loaded from Resources, no pixel reads needed).</summary>
        private static Sprite CropTop(Sprite s, float keep)
        {
            var r = s.textureRect;
            float px = Mathf.Clamp(keep * s.pixelsPerUnit, 1f, r.height);
            if (px >= r.height - 0.5f) return s;
            return Sprite.Create(s.texture, new Rect(r.x, r.yMax - px, r.width, px), new Vector2(0.5f, 0f), s.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        }

        /// <summary>Down arrow (11x8 px, dark outline) - placed above the valve when <see cref="Alert"/>.</summary>
        private static Sprite ArrowSprite
        {
            get
            {
                if (arrowSprite != null) return arrowSprite;
                const int W = 11, H = 8;
                var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                var px = new Color32[W * H];
                var fill = new Color32(255, 255, 255, 255); var edge = new Color32(20, 40, 60, 255);
                for (int y = 0; y < H; y++)                                   // y = 0 is the tip (bottom)
                {
                    int half = y * 5 / (H - 1);
                    for (int x = 0; x < W; x++)
                    {
                        int dx = Mathf.Abs(x - 5);
                        if (dx > half) continue;
                        px[y * W + x] = (dx == half || y == H - 1) ? edge : fill;
                    }
                }
                tex.SetPixels32(px);
                tex.Apply();
                return arrowSprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0f), 100f);
            }
        }

        private static Sprite[] LoadSeq(string prefix)
        {
            var list = new List<Sprite>();
            for (int i = 0; i < 16; i++)
            {
                var s = Resources.Load<Sprite>(prefix + i);
                if (s == null) break;
                list.Add(s);
            }
            if (list.Count == 0) Debug.LogWarning("[Sluice] Image not found " + prefix + "0");
            return list.ToArray();
        }

        /// <summary>64x32 px water surface (tiled vertically): clear blue base, light streaks flowing down, white foam on both edges.</summary>
        private static Sprite MakeWater(int frame)
        {
            const int W = 64, H = 32;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            var px = new Color32[W * H];
            var baseC = new Color32(88, 160, 200, 120);
            var deep = new Color32(64, 128, 172, 135);
            var streak = new Color32(186, 228, 248, 160);
            var foam = new Color32(220, 244, 255, 190);
            var rng = new System.Random(17);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    px[y * W + x] = ((x / 8 + y / 6) % 3 == 0) ? deep : baseC;
            // vertical light streaks: shifted down per frame (8 px each) -> looks like flowing water
            for (int k = 0; k < 14; k++)
            {
                int x = 4 + rng.Next(W - 8), y0 = rng.Next(H), len = 3 + rng.Next(6);
                for (int j = 0; j < len; j++)
                {
                    int y = ((y0 - j - frame * 8) % H + H) % H;
                    px[y * W + x] = streak;
                }
            }
            // foam on both edges
            for (int y = 0; y < H; y++)
            {
                bool on = ((y + frame * 3) % 4) != 0;
                if (on) { px[y * W + 0] = foam; px[y * W + W - 1] = foam; }
                if (((y + frame * 5) % 3) == 0) { px[y * W + 1] = foam; px[y * W + W - 2] = foam; }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 1f), 100f, 0, SpriteMeshType.FullRect);
        }
    }
}
