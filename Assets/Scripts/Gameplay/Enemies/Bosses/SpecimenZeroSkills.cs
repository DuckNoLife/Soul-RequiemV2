using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// SPECIMEN ZERO (hidden boss, Containment Vault) - a failed body of The Vessel: giant face, a body of fleshy root bundles, a vertical eye slit
    /// in the middle of the chest, chained to the wall. Never moves. The skill set fits the sprite (nothing on the body could spit orbs).
    /// Every skill has a clear warning:
    ///   Grasping Roots  : a purple crack runs from the boss feet to Ren, then roots burst out one after another along it - hit: damage + slow.
    ///                     Phase 2: 3 cracks in a fan.
    ///   Tentacle Lash   : a horizontal lane at Ren's height lights up, tentacles burst out in a chain sweeping from one side of the room to the other.
    ///   Vessel's Gaze   : the chest eye slit opens (purple light rises, thin aim line), then a beam sweeps an arc through Ren - roll through it to dodge.
    ///   Requiem Wail    : 4 SEAL PILLARS (sprite_prop group sz_ward). The boss inhales for 2.6 s (phase 2: 2.2 s): ONE pillar lights up,
    ///                     that pillar's RUNE glows large under the boss + a dotted gold line runs to that pillar's seal ring; a RED ring follows Ren's feet
    ///                     (turns gold inside the seal ring). Wave: inside the seal ring = safe, outside = HP loss (rolling still dodges). The first ring
    ///                     locks Mantras. Reward for surviving it: the boss is SPENT for 2.5 s afterwards, face down, takes +50% damage.
    ///   Hand of the Vessel: a shadow spreads under Ren, follows him, then locks, and a giant corpse hand (PixelLab) bursts up to grab:
    ///                     heavy damage + launch. Phase 2: 3 hands chasing in a row.
    ///   Writhing Bed    : against "hugging the boss and slashing nonstop": Ren next to the boss feet for over 2.5 s -> the floor around it writhes purple (0.8 s),
    ///                     then 3 root rings burst out and launch Ren far away. 7 s cooldown.
    ///   Phase 2 (≤50%)  : the chains snap (strong shake, purple flash), 4 WALL EYES (PixelLab props group sz_walleye on the north wall) open wide.
    ///   The Watchers    : the wall eyes are ONE scheduled attack (not random shots, so damage never comes "from nowhere"):
    ///                     the 4 eyes in turn (starting with the one farthest from Ren) lock an eye-shaped TARGET MARK under Ren, charge up, then a deep red beam
    ///                     (dark outline + white core, readable over the red low-HP screen edge) strikes down with a burst + scorch mark. Shots are 0.55 s apart,
    ///                     so the player must keep moving; a rest beat follows each volley. VFX images: Resources/VFX/specimen/eyefx_* (missing -> drawn in code).
    /// Root images: Resources/VFX/specimen/roots{a..d}_0..7 - 4 PixelLab root styles (source Catalog/v2/art/pixellab_roots_source.png), each
    /// built into 8 frames "rise - stand - sink"; each root picks a random style. Missing -> brown-tinted tentacle frames.
    /// Defeated -> achievement "Breaker of Seals" + flag specimen_zero_slain.
    /// Class files: SpecimenZeroSkills.cs (settings, setup, skill choice, helpers) · SpecimenZeroSkills.Attacks.cs (each skill as a coroutine).
    /// </summary>
    public partial class SpecimenZeroSkills : MonoBehaviour, IEnemySkills
    {
        public Sprite[] tentacleFrames;
        public Sprite[] rootFrames;                           // empty -> Resources/VFX/specimen/roots{a..d}_*
        public GameObject boltPrefab;                         // (unused - kept for old prefabs)
        public Vector2 eyeOffset = new Vector2(0f, 0.67f);   // eye slit in the middle of the body (px 134/208), times the enemy scale
        public float leash = 6.5f;                            // farther = the player has left the room
        public Color tint = new Color(0.78f, 0.45f, 1f);
        public Color rootTint = new Color(0.62f, 0.42f, 0.55f);
        public string achievementId = "breaker_of_seals", achievementTitle = "Breaker of Seals";
        public string wardGroup = "sz_ward";                  // sprite_prop group of the seal pillars in Tiled
        public Color wardGold = new Color(1f, 0.83f, 0.5f);

        private EnemyAI ai;
        private bool engaged, chainsBroken;
        private float nextRoots, nextLash, nextGaze, nextWail, nextHand;
        private float fade = 1f;
        private Rect room;
        private bool hasRoom;
        private readonly List<GameObject> fx = new List<GameObject>();
        private static Sprite pixel;
        private readonly List<Sprite[]> rootSets = new List<Sprite[]>();
        private readonly List<SpritePropMechanism> litWards = new List<SpritePropMechanism>();
        private Sprite[] handFrames;
        private static Sprite blob;
        private bool shelterTold;

        [Header("Spent - exhausted after Requiem Wail")]
        public float spentSeconds = 2.5f;
        public float spentDamageMul = 1.5f;
        [Header("Writhing Bed - anti-hugging")]
        public float bedRadius = 1.2f;                        // measured flattened (y / 0.6) around the boss feet
        public float bedHugSeconds = 2.5f;
        private float nextBed, nextWatch, closeT;

        // wall eyes open (phase 2)
        private class WatchEye { public Vector2 at; public Light2D light; public float idle; }
        private readonly List<WatchEye> watchEyes = new List<WatchEye>();
        private Sprite[] eyeCharge, eyeBeam, eyeBurst;
        private Sprite eyeScorch, eyeReticle;
        private bool eyeFxLoaded;

        private void Awake()
        {
            var h = GetComponent<Health>();
            if (h != null) h.Died += _ =>
            {
                MapEncounterDirector.Instance?.Flags.Set("specimen_zero_slain");
                Achievements.Unlock(achievementId, achievementTitle);
                Cleanup();
            };
        }

        private void OnDisable() => Cleanup();

        private void Cleanup()
        {
            StopAllCoroutines();
            foreach (var g in fx) if (g != null) Destroy(g);
            fx.Clear();
            foreach (var w in litWards) if (w != null) w.SetLit(false);
            litWards.Clear();
            watchEyes.Clear();
            var h = ai != null ? ai.Health : GetComponent<Health>();
            if (h != null) h.DamageTakenMul = 1f;
        }

        // time Ren spends "hugging" the boss feet (decays twice as fast when he leaves) -> Writhing Bed
        private void Update()
        {
            if (!engaged || ai == null || ai.Player == null || ai.Health == null || ai.Health.IsDead) return;
            Vector2 d = (Vector2)ai.Player.position - Base;
            bool hug = !wailing && new Vector2(d.x, d.y / 0.6f).magnitude < bedRadius;
            closeT = hug ? closeT + Time.deltaTime : Mathf.Max(0f, closeT - Time.deltaTime * 2f);
        }

        // the boss body is ~3 units tall: fade it when the player stands behind so the character stays visible
        private void LateUpdate()
        {
            if (ai == null) ai = GetComponent<EnemyAI>();
            var body = ai != null ? ai.Body : null;
            if (body == null || ai.Player == null || ai.Health == null || ai.Health.IsDead) return;
            Vector2 p = (Vector2)ai.Player.position + new Vector2(0f, 0.3f);
            var b = body.bounds;
            bool behind = ai.Player.position.y > transform.position.y && b.Contains(new Vector3(p.x, p.y, b.center.z));
            fade = Mathf.MoveTowards(fade, behind ? 0.45f : 1f, Time.deltaTime * 4f);
            var c = body.color;
            if (!Mathf.Approximately(c.a, fade)) { c.a = fade; body.color = c; }   // reset every frame (Flash overrides the colour)
        }

        private Vector2 Base => transform.position;
        private Vector2 Eye => (Vector2)transform.position + eyeOffset * transform.localScale.y;
        private int Order => ai.Body != null ? ai.Body.sortingOrder : 2;
        private bool P2 => ai.InPhase2;
        private float Pace => P2 ? 0.72f : 1f;                 // phase 2: every cooldown is shorter

        // ------------------------------------------------------------------ 0. Skill choice
        // A fixed priority list with cooldowns counted from the START let the ~11 s Requiem Wail (inhale 2.2 + rings + pillar switch + Spent)
        // come off its 10 s phase 2 cooldown the moment it ended, so it wailed nonstop and The Watchers / Gaze / Hand never got a turn.
        // So: (1) cooldowns count from when the attack ENDS; (2) weighted random pick among ready attacks, never repeating the last one;
        // (3) the signature Wail only comes after at least 4 (phase 2: 3) other attacks in between - then it is always chosen (steady, predictable rhythm).
        private string lastAttack = "";
        private int sinceWail, sinceWatch = 99;

        private string PickAttack()
        {
            float now = Time.time;
            if (now >= nextWail && sinceWail >= (P2 ? 3 : 4)) return "wail";
            var ids = new List<string>();
            var ws = new List<float>();
            void Add(string id, bool ready, float w) { if (ready && id != lastAttack) { ids.Add(id); ws.Add(w); } }
            Add("watch", P2 && watchEyes.Count > 0 && now >= nextWatch && sinceWatch >= 1, 3f);
            Add("gaze", now >= nextGaze, 2f);
            Add("hand", now >= nextHand, 2f);
            Add("lash", now >= nextLash, 1.5f);
            Add("roots", now >= nextRoots, 1f);
            if (ids.Count == 0) return now >= nextRoots ? "roots" : null;   // only roots left (just used) -> allow the repeat, roots are the filler attack
            float sum = 0f;
            foreach (var w in ws) sum += w;
            float r = Random.value * sum;
            for (int i = 0; i < ids.Count; i++) { r -= ws[i]; if (r <= 0f) return ids[i]; }
            return ids[ids.Count - 1];
        }

        /// <summary>Start the cooldown from when the attack ends + count the attacks in between.</summary>
        private void AfterAttack(string id)
        {
            float now = Time.time;
            lastAttack = id;
            sinceWail = id == "wail" ? 0 : sinceWail + 1;
            sinceWatch = id == "watch" ? 0 : sinceWatch + 1;
            switch (id)
            {
                case "wail": nextWail = now + (P2 ? 13f : 16f); break;
                case "watch": nextWatch = now + 7f; break;
                case "gaze": nextGaze = now + 9f * Pace; break;
                case "hand": nextHand = now + 8f * Pace; break;
                case "lash": nextLash = now + 6f * Pace; break;
                default: nextRoots = now + 3.2f * Pace; break;
            }
        }

        // ------------------------------------------------------------------ 4. Requiem Wail
        //  - ONE seal pillar lights up; that pillar's own RUNE glows large under the boss + a dotted gold line runs from the boss to the pillar's seal ring.
        //  - A RED RING follows Ren's feet while the boss inhales; standing in the lit pillar's seal ring -> it turns gold (safe).
        //  - Wave bursts: Ren inside the seal ring = no HP loss (the ring flashes); outside = hit (rolling still dodges). The first ring locks Mantras.
        //  - While wailing every other skill pauses (wall eyes wait, an aim in progress is cancelled) - the player has to stand still in the ring.
        //  - 3 rings; phase 2: after ring 1 another pillar lights up (the player must run over).
        //  - Not interruptible by damage (two slashes would erase the signature attack). After the wail -> Spent (exhausted, +50% damage).
        private const float SigilRadius = 0.42f;                 // safe radius of the seal ring (measured flattened like the wave)
        private bool wailing;
        private SpriteRenderer runeGlyph, footRing;
        private readonly List<SpriteRenderer> trail = new List<SpriteRenderer>();

        /// <summary>Ren (feet) stands inside the lit pillar's seal ring.</summary>
        private static bool InSigil(SpritePropMechanism ward, Vector2 p)
        {
            if (ward == null || ward.Body == null) return false;
            Vector2 d = p - ward.DecalAt;
            return new Vector2(d.x, d.y / 0.6f).magnitude <= SigilRadius;
        }

        /// <summary>The lit pillar's rune glows large under the boss (same image as the rune carved on the pillar - shows which pillar to run to).</summary>
        private void ShowRune(SpritePropMechanism ward, Vector2 c, float k, float t)
        {
            var icon = ward != null ? ward.Icon : null;
            if (icon == null) return;
            if (runeGlyph == null)
            {
                runeGlyph = EtherVfx.Spawn("SZ Wail Rune", icon, c, wardGold, ai.Body != null ? ai.Body.sortingLayerID : 0, Order - 2);
                FxLighting.MakeUnlit(runeGlyph);
                fx.Add(runeGlyph.gameObject);
            }
            if (runeGlyph.sprite != icon) runeGlyph.sprite = icon;
            float s = 1.25f / Mathf.Max(0.01f, icon.bounds.size.y);                 // ~1.25 units above the floor
            float pop = 1f + 0.06f * Mathf.Sin(t * 7f);
            runeGlyph.transform.position = c + Vector2.down * 0.25f;
            runeGlyph.transform.localScale = new Vector3(s * pop, s * 0.6f * pop, 1f);
            runeGlyph.color = new Color(wardGold.r, wardGold.g, wardGold.b, Mathf.Lerp(0.15f, 0.95f, Mathf.Clamp01(k * 1.6f)));
        }

        /// <summary>Dotted gold line from the boss feet to the lit pillar's seal ring (light dots run along it).</summary>
        private void ShowTrail(Vector2 from, Vector2 to, float t)
        {
            const int n = 14;
            while (trail.Count < n)
            {
                var d = EtherVfx.Spawn("SZ Wail Trail", Pixel, from, wardGold, ai.Body != null ? ai.Body.sortingLayerID : 0, Order - 2);
                FxLighting.MakeUnlit(d);
                d.transform.localScale = new Vector3(0.07f, 0.07f, 1f);
                fx.Add(d.gameObject);
                trail.Add(d);
            }
            Vector2 a = from + (to - from).normalized * 0.7f, b = to - (to - from).normalized * 0.45f;
            for (int i = 0; i < n; i++)
            {
                if (trail[i] == null) continue;
                float u = i / (float)(n - 1);
                trail[i].transform.position = Vector2.Lerp(a, b, u);
                float wave = Mathf.Repeat(u - t * 0.9f, 1f);                         // light dots running toward the pillar
                trail[i].color = new Color(wardGold.r, wardGold.g, wardGold.b, 0.25f + 0.65f * Mathf.Pow(1f - wave, 4f));
            }
        }

        /// <summary>Warning ring following Ren's feet: red = will be hit by the wave; gold = inside the seal ring (safe).</summary>
        private void ShowFootRing(SpritePropMechanism ward, float k, float t)
        {
            if (ai.Player == null) return;
            if (footRing == null)
            {
                footRing = EtherVfx.Spawn("SZ Wail Danger", EtherVfx.Ring, ai.Player.position, Color.red, ai.Body != null ? ai.Body.sortingLayerID : 0, Order - 2);
                FxLighting.MakeUnlit(footRing);
                fx.Add(footRing.gameObject);
            }
            bool safe = InSigil(ward, ai.Player.position);
            footRing.transform.position = ai.Player.position;
            float s = (safe ? 0.42f : 0.5f) * (1f + 0.08f * Mathf.Sin(t * (safe ? 4f : 14f)));
            footRing.transform.localScale = new Vector3(s, s * 0.6f, 1f);
            Color col = safe ? wardGold : new Color(1f, 0.18f, 0.15f);
            footRing.color = new Color(col.r, col.g, col.b, Mathf.Lerp(0.5f, 1f, k));
        }

        private void ClearWailFx()
        {
            if (runeGlyph != null) { Destroy(runeGlyph.gameObject); runeGlyph = null; }
            if (footRing != null) { Destroy(footRing.gameObject); footRing = null; }
            foreach (var d in trail) if (d != null) Destroy(d.gameObject);
            trail.Clear();
        }

        /// <summary>Radius (measured flattened like Shockwave) large enough for the wave to cover the whole room - no corner to dodge by standing still.</summary>
        private float CoverRadius(Vector2 c)
        {
            if (!hasRoom) return 5.5f;
            float m = 0f;
            foreach (var p in new[] { new Vector2(room.xMin, room.yMin), new Vector2(room.xMax, room.yMin), new Vector2(room.xMin, room.yMax), new Vector2(room.xMax, room.yMax) })
            {
                Vector2 d = p - c;
                m = Mathf.Max(m, new Vector2(d.x, d.y / 0.6f).magnitude);
            }
            return m + 0.3f;
        }

        /// <summary>Pick one of the 2 pillars nearest Ren at random (skipping `except`): always reachable in time, but not always the one right next to him.</summary>
        private SpritePropMechanism PickWard(List<SpritePropMechanism> wards, SpritePropMechanism except)
        {
            if (wards == null || wards.Count == 0 || ai.Player == null) return null;
            var list = new List<SpritePropMechanism>(wards);
            list.Remove(except);
            if (list.Count == 0) return null;
            Vector2 p = ai.Player.position;
            list.Sort((a, b) => ((a.DecalAt - p).sqrMagnitude).CompareTo((b.DecalAt - p).sqrMagnitude));
            return list[Random.Range(0, Mathf.Min(2, list.Count))];
        }

        /// <summary>Light the pillar by progress k (0..1): switch to the gold rune image, candle light rises, the floor seal ring brightens and pulses.</summary>
        private void GlowWard(SpritePropMechanism ward, float k, float t)
        {
            if (ward == null || ward.Body == null) return;
            if (!ward.Lit) { ward.SetLit(true); if (!litWards.Contains(ward)) litWards.Add(ward); }
            ward.Glow.color = wardGold;
            ward.Glow.intensity = Mathf.Lerp(0.4f, 2.2f, k) + 0.2f * Mathf.Sin(t * 9f);
            if (ward.Decal != null)
            {
                float a = Mathf.Lerp(0.35f, 1f, k) * (0.85f + 0.15f * Mathf.Sin(t * 6f));
                ward.Decal.color = new Color(1f, 1f, 1f, a);
            }
        }

        private void LoadHand()
        {
            var list = new List<Sprite>();
            for (int i = 0; i < 8; i++)
            {
                var sp = Resources.Load<Sprite>($"VFX/specimen/hand_{i}");
                if (sp == null) break;
                list.Add(sp);
            }
            handFrames = list.Count == 8 ? list.ToArray() : null;
        }

        /// <summary>Soft round spot (shadow spreading on the ground), 1 unit diameter.</summary>
        private static Sprite Blob
        {
            get
            {
                if (blob != null) return blob;
                const int n = 48;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                var px = new Color32[n * n];
                float c = (n - 1) / 2f;
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float r = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                        float a = r >= 1f ? 0f : Mathf.Clamp01((1f - r) * 2.2f);
                        px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
                    }
                tex.SetPixels32(px);
                tex.Apply();
                blob = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
                return blob;
            }
        }

        // ------------------------------------------------------------------ 8. Phase 2: chains snap + wall eyes open
        // The wall eyes are real PROPS (sprite_prop group sz_walleye, PixelLab images) lying closed on the north wall from the start; when the chains snap they
        // open wide one by one (red flash) from left to right. The eyes do NOT fire on their own - only during The Watchers (section 9). No props -> hand-drawn red dots.
        public string wallEyeGroup = "sz_walleye";

        // ------------------------------------------------------------------ 9. The Watchers - chasing volley of the wall eyes
        // Starts from the eye FARTHEST from Ren and moves toward Ren (the player can read the volley direction). Each shot: an eye-shaped target mark follows Ren's feet
        // then locks (blinking), the eye charges + PixelLab flash, the beam strikes down. Shots are 0.55 s apart -> standing still gets hit, running dodges.
        private const float WatchGap = 0.55f, WatchLock = 0.35f, WatchHitRadius = 0.34f;

        /// <summary>Load the wall eye VFX images (PixelLab) from Resources/VFX/specimen/eyefx_*. Any missing image is drawn in code instead.</summary>
        private void LoadEyeFx()
        {
            if (eyeFxLoaded) return;
            eyeFxLoaded = true;
            eyeCharge = Frames("VFX/specimen/eyefx_charge_");
            eyeBeam = Frames("VFX/specimen/eyefx_beam_");
            eyeBurst = Frames("VFX/specimen/eyefx_burst_");
            eyeScorch = Resources.Load<Sprite>("VFX/specimen/eyefx_scorch");
            eyeReticle = Resources.Load<Sprite>("VFX/specimen/eyefx_reticle");
        }

        private static Sprite[] Frames(string prefix)
        {
            var list = new List<Sprite>();
            for (int i = 0; i < 24; i++)
            {
                var sp = Resources.Load<Sprite>(prefix + i);
                if (sp == null) break;
                list.Add(sp);
            }
            return list.Count > 0 ? list.ToArray() : null;
        }

        // ------------------------------------------------------------------ drawing helpers
        private static Sprite Pixel
        {
            get
            {
                if (pixel != null) return pixel;
                var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                tex.SetPixel(0, 0, Color.white); tex.Apply();
                pixel = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0f, 0.5f), 1f);
                return pixel;
            }
        }

        private SpriteRenderer Line(Vector2 a, Vector2 b, float width, Color c, int order)
        {
            var sr = EtherVfx.Spawn("SZ Line", Pixel, a, c, ai.Body != null ? ai.Body.sortingLayerID : 0, order);
            fx.Add(sr.gameObject);
            Vector2 d = b - a;
            Place(sr, a, d.sqrMagnitude > 0.0001f ? d.normalized : Vector2.right, d.magnitude, width);
            return sr;
        }

        private static void Place(SpriteRenderer sr, Vector2 a, Vector2 dir, float len, float width)
        {
            sr.transform.position = new Vector3(a.x, a.y, 0f);
            sr.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            sr.transform.localScale = new Vector3(Mathf.Max(0.001f, len), width, 1f);
        }

        private SpriteRenderer Bar(Vector2 center, Vector2 size, Color c, int order, Transform parent = null)
        {
            var sr = EtherVfx.Spawn("SZ Rect", Pixel, center - new Vector2(size.x * 0.5f, 0f), c, ai.Body != null ? ai.Body.sortingLayerID : 0, order, parent);
            if (parent == null) fx.Add(sr.gameObject);
            sr.transform.position = new Vector3(center.x - size.x * 0.5f, center.y, 0f);
            sr.transform.localScale = new Vector3(size.x, size.y, 1f);
            return sr;
        }

        private static float DistToSeg(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            return (p - (a + ab * t)).magnitude;
        }

        private void LoadRoots()
        {
            rootSets.Clear();
            if (rootFrames != null && rootFrames.Length > 0) { rootSets.Add(rootFrames); return; }
            foreach (char k in "abcd")
            {
                var list = new List<Sprite>();
                for (int i = 0; i < 8; i++)
                {
                    var s = Resources.Load<Sprite>($"VFX/specimen/roots{k}_{i}");
                    if (s == null) break;
                    list.Add(s);
                }
                if (list.Count == 8) rootSets.Add(list.ToArray());
            }
            if (rootSets.Count == 0 && tentacleFrames != null && tentacleFrames.Length > 0) rootSets.Add(tentacleFrames);
        }
    }
}
