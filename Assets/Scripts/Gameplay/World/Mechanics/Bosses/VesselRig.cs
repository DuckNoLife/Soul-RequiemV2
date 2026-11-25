using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// THE CRYSTAL VESSEL assembled from Giant Boss parts (Volcano pack, recoloured: Resources/VFX/vessel/&lt;part&gt;_dormant | _awake).
    /// Coordinates = `_tools/s402/vessel_rig.json`: source image px, origin = sternum centre, y DOWN; the right arm mirrors the left arm.
    /// Draw order (inside the body SortingGroup): ribs_4 .. ribs_0, sternum, jaw, head, eyes. Arms: their own SortingGroup, shoulder BELOW the arm segment chain,
    /// lower segments over upper ones, hand on top.
    /// Arm = quadratic Bezier curve from shoulder to wrist; segments (arm_0..4 in order) are spaced evenly by arc length and ROTATE to the tangent.
    /// Attack code only sets the hand target (<see cref="SetHand"/>) — the arm chain lays itself out.
    /// Not a MonoBehaviour: VesselDomainMechanism calls <see cref="Tick"/> every frame.
    /// </summary>
    public sealed class VesselRig
    {
        // ---- source coordinates (px, y down) — vessel_rig.json
        private static readonly float[] RibY = { -63f, -47f, -30f, -16f, 1f };
        private const float JawY = -99f, HeadY = -194f, EyeX = 35f, EyeY = -150f;
        private static readonly Vector2 Shoulder = new Vector2(-134f, -52f);
        private static readonly Vector2 ArmP0 = new Vector2(-132f, -33f);
        private static readonly Vector2 HandRest = new Vector2(-183f, 119f);
        private static readonly Vector2 WristFromHand = new Vector2(-5f, -44f);          // wrist = hand + (-5, -44) (left arm)
        private static readonly Vector2 ElbowBulge = new Vector2(-15f, -19f);             // control point = segment middle + elbow bulge (left arm)
        private const float ArmStep = 4f, ArmScale0 = 0.95f, ArmScale1 = 1f;

        public Transform Root { get; private set; }
        public float Scale { get; private set; }
        public bool Awake { get; private set; }
        /// <summary>0 = closed, 1 = fully open (roar).</summary>
        public float JawOpen;
        /// <summary>Slump forward (0..1) — dormant husk / when slamming a hand.</summary>
        public float Slump;
        /// <summary>HEAD DIP toward the arena (0..1) — head / jaw / eyes lower ~75 source px and grow 12 % (closer to the camera), used for breath / lava spit.</summary>
        public float HeadDip;
        /// <summary>HEAD LUNGE onto the arena (0..1) — ONLY head + jaw + both eyes lower `HeadLungePx` source px,
        /// grow 10 %; torso / shoulders / arms stay still. While lowered the head group moves to its own SortingGroup drawn IN FRONT of the floor (`LungeOrder`)
        /// so mouth / eyes show over the arena edge. Used by every eye / mouth move.</summary>
        public float Lunge;
        public int LungeOrder = -12;
        public float HeadLungePx = 170f;
        /// <summary>Head lowering cap (source px, for both HeadDip and Lunge) — the domain sets it so the jaw only reaches the NORTH EDGE of the arena (map wall).</summary>
        public float MaxDipPx = float.MaxValue;
        /// <summary>Bottom of the jaw sprite (world) — the domain measures it to compute MaxDipPx.</summary>
        public float JawBottomY => parts.TryGetValue("jaw", out var jr) && jr != null ? jr.bounds.min.y : 0f;
        private Transform headRoot;
        private SortingGroup headGroup;
        private bool headDetached;
        private static readonly string[] headParts = { "head", "jaw", "eye_l", "eye_r" };
        private Vector2 jawPx;
        /// <summary>Whole-body tint (multiplied into each part's colour) — element crystal being absorbed, white while disintegrating.</summary>
        public Color Tint = Color.white;
        /// <summary>Element Absalom is channelling (fire / ice / storm / umbral); null = original violet. Only applies to AWAKE parts (Resources/VFX/vessel/&lt;part&gt;_awake_&lt;element&gt;).</summary>
        public string Element { get; private set; }

        private readonly string[] torsoParts = { "ribs_4", "ribs_3", "ribs_2", "ribs_1", "ribs_0", "sternum", "jaw", "head", "eye_l", "eye_r" };
        private readonly Dictionary<string, SpriteRenderer> parts = new Dictionary<string, SpriteRenderer>();
        private readonly Dictionary<string, bool> partAwake = new Dictionary<string, bool>();
        private readonly Dictionary<string, float> flashUntil = new Dictionary<string, float>();
        private Transform torso;
        private readonly Arm[] arms = new Arm[2];
        private Light2D mouthLight, eyeLight, coreLight;
        private float t0;

        private class Arm
        {
            public int side;                 // -1 left, +1 right
            public Transform root;
            public SpriteRenderer shoulder, hand;
            public readonly List<SpriteRenderer> pieces = new List<SpriteRenderer>();
            public Vector2 hand_px, target_px;   // current / target hand position (source px, y down)
            public float follow = 10f;           // target follow strength (higher = faster)
            public string handSprite = "hand_open";
            public bool awake;
            public Color handTint = Color.white;  // stunned hand -> joints flash blue (hittable spot)
        }

        public VesselRig(Vector2 chest, float scale, Transform parent, int sortingLayer, int torsoOrder, int armOrder, bool awake)
        {
            Scale = scale;
            Awake = awake;
            t0 = Random.Range(0f, 10f);
            Root = new GameObject("The Crystal Vessel").transform;
            Root.SetParent(parent, false);
            Root.position = new Vector3(chest.x, chest.y, 0f);
            Root.localScale = new Vector3(scale, scale, 1f);

            torso = new GameObject("torso").transform;
            torso.SetParent(Root, false);
            var sg = torso.gameObject.AddComponent<SortingGroup>();
            sg.sortingLayerID = sortingLayer; sg.sortingOrder = torsoOrder;
            // separate HEAD group (same origin as the torso) — head / jaw / eyes move here while the head lunges onto the arena
            headRoot = new GameObject("head (lunge)").transform;
            headRoot.SetParent(Root, false);
            headGroup = headRoot.gameObject.AddComponent<SortingGroup>();
            headGroup.sortingLayerID = sortingLayer; headGroup.sortingOrder = torsoOrder;
            for (int i = 0; i < torsoParts.Length; i++)
            {
                var r = NewPart(torsoParts[i], torso, i, awake);
                parts[torsoParts[i]] = r;
            }
            for (int k = 0; k < 2; k++) arms[k] = BuildArm(k == 0 ? -1 : 1, sortingLayer, armOrder, awake);

            mouthLight = MakeLight(torso, new Vector2(0f, JawY + 10f), new Color(0.85f, 0.55f, 1f), 1.1f);
            eyeLight = MakeLight(torso, new Vector2(0f, EyeY), new Color(0.9f, 0.7f, 1f), 0.9f);
            coreLight = MakeLight(torso, new Vector2(0f, 0f), new Color(0.75f, 0.45f, 1f), 1.0f);
            Tick(0f);
        }

        // ------------------------------------------------------------------ build
        private SpriteRenderer NewPart(string name, Transform parent, int order, bool awake)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var r = go.AddComponent<SpriteRenderer>();
            FxLighting.MakeUnlit(r);
            r.sortingOrder = order;
            partAwake[name] = awake;
            r.sprite = Spr(name, awake);
            return r;
        }

        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        public static Sprite Load(string part, bool awake)
        {
            string key = part + (awake ? "_awake" : "_dormant");
            if (cache.TryGetValue(key, out var s) && s != null) return s;
            s = Resources.Load<Sprite>("VFX/vessel/" + key);
            if (s == null) Debug.LogWarning("[Vessel] Missing Resources/VFX/vessel/" + key + ".png (run _tools/s402/make_assets.py)");
            cache[key] = s;
            return s;
        }

        /// <summary>Sprite of part `part` for the current element (missing element sprite -> violet version).</summary>
        private Sprite Spr(string part, bool awake)
        {
            if (!awake || string.IsNullOrEmpty(Element)) return Load(part, awake);
            string key = part + "_awake_" + Element;
            if (!cache.TryGetValue(key, out var s) || s == null) { s = Resources.Load<Sprite>("VFX/vessel/" + key); cache[key] = s; }
            return s != null ? s : Load(part, true);
        }

        /// <summary>Change element (Absalom activates an energy pylon). null = back to original violet. Every awake part swaps sprites + flashes; lights change colour.</summary>
        public void SetElement(string el)
        {
            if (Element == el) return;
            Element = el;
            foreach (var n in torsoParts)
            {
                if (!partAwake[n]) continue;
                parts[n].sprite = Spr(n, true);
                flashUntil[n] = Time.time + 0.35f;
            }
            for (int k = 0; k < 2; k++)
            {
                var a = arms[k];
                if (!a.awake) continue;
                a.shoulder.sprite = Spr("shoulder", true);
                for (int i = 0; i < a.pieces.Count; i++) a.pieces[i].sprite = Spr("arm_" + (i % 5), true);
                a.hand.sprite = Spr(a.handSprite, true);
                flashUntil["arm" + a.side] = Time.time + 0.35f;
            }
            Color c = el == "fire" ? new Color(1f, 0.55f, 0.25f) : el == "ice" ? new Color(0.6f, 0.9f, 1f)
                    : el == "storm" ? new Color(1f, 0.9f, 0.4f) : el == "umbral" ? new Color(0.85f, 0.35f, 1f) : new Color(0.85f, 0.55f, 1f);
            if (mouthLight != null) mouthLight.color = c;
            if (eyeLight != null) eyeLight.color = Color.Lerp(c, Color.white, 0.25f);
            if (coreLight != null) coreLight.color = c;
        }

        /// <summary>Tint only the hand (stunned = blue). Color.white = normal.</summary>
        public void SetHandTint(int side, Color c) => arms[side < 0 ? 0 : 1].handTint = c;

        /// <summary>Hand rest point (world) — Stomp when Ren stands next to it.</summary>
        public Vector2 RestWorld(int side) => Root.TransformPoint(Px(new Vector2(Mx(HandRest.x, side), HandRest.y)));

        /// <summary>Hand is resting (target = rest point and it has nearly arrived).</summary>
        public bool IsResting(int side)
        {
            var a = arms[side < 0 ? 0 : 1];
            var rest = new Vector2(Mx(HandRest.x, side), HandRest.y);
            return (a.target_px - rest).sqrMagnitude < 4f && (a.hand_px - rest).sqrMagnitude < 400f;
        }

        private Arm BuildArm(int side, int layer, int order, bool awake)
        {
            var a = new Arm { side = side, awake = awake };
            a.root = new GameObject(side < 0 ? "arm L" : "arm R").transform;
            a.root.SetParent(Root, false);
            var sg = a.root.gameObject.AddComponent<SortingGroup>();
            sg.sortingLayerID = layer; sg.sortingOrder = order;
            a.shoulder = PieceRenderer(a, "shoulder", 0);
            // fixed segment count from the rest arm length (stretching / bending spaces them evenly)
            Vector2 w = HandRest + WristFromHand;
            float len = ArcLength(ArmP0, (ArmP0 + w) * 0.5f + ElbowBulge, w);
            int n = Mathf.Max(6, Mathf.RoundToInt(len / ArmStep) + 1);
            for (int i = 0; i < n; i++) a.pieces.Add(PieceRenderer(a, "arm_" + (i % 5), 1 + i));
            a.hand = PieceRenderer(a, "hand_open", 2 + n);
            a.hand_px = a.target_px = new Vector2(Mx(HandRest.x, side), HandRest.y);
            return a;
        }

        private SpriteRenderer PieceRenderer(Arm a, string part, int order)
        {
            var go = new GameObject(part);
            go.transform.SetParent(a.root, false);
            var r = go.AddComponent<SpriteRenderer>();
            FxLighting.MakeUnlit(r);
            r.sortingOrder = order;
            r.flipX = a.side > 0;
            r.sprite = Spr(part, a.awake);
            return r;
        }

        private static Light2D MakeLight(Transform parent, Vector2 px, Color c, float radius)
        {
            var g = new GameObject("vessel light");
            g.transform.SetParent(parent, false);
            g.transform.localPosition = Px(px);
            var l = g.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point;
            l.color = c; l.pointLightOuterRadius = radius; l.pointLightInnerRadius = 0.05f; l.intensity = 0f;
            return l;
        }

        private static Vector3 Px(Vector2 p) => new Vector3(p.x * 0.01f, -p.y * 0.01f, 0f);

        // ------------------------------------------------------------------ API
        /// <summary>Hand target (side -1 left / +1 right) in WORLD COORDINATES; follow = follow strength (10 normal, 30+ = fast slam).</summary>
        public void SetHand(int side, Vector2 world, float follow = 10f, string handSprite = null)
        {
            var a = arms[side < 0 ? 0 : 1];
            Vector3 local = Root.InverseTransformPoint(world);
            a.target_px = new Vector2(local.x * 100f, -local.y * 100f);
            a.follow = follow;
            if (handSprite != null && handSprite != a.handSprite) { a.handSprite = handSprite; a.hand.sprite = Spr(handSprite, a.awake); }
        }

        /// <summary>Return the hand to its rest point (resting on the island edge).</summary>
        public void RestHand(int side, float follow = 6f) => SetHandPx(side, HandRest, follow, "hand_open");
        private void SetHandPx(int side, Vector2 px, float follow, string sprite)
        {
            var a = arms[side < 0 ? 0 : 1];
            a.target_px = new Vector2(side < 0 ? -Mathf.Abs(px.x) : Mathf.Abs(px.x), px.y);
            a.follow = follow;
            if (sprite != a.handSprite) { a.handSprite = sprite; a.hand.sprite = Spr(sprite, a.awake); }
        }

        /// <summary>Mouth (lower jaw edge) — follows jaw opening / head dip (world).</summary>
        public Vector2 MouthWorld() => torso.TransformPoint(Px(jawPx + new Vector2(0f, 26f * (1f + 0.12f * HeadDip))));

        /// <summary>Eye centre (side -1 left / +1 right) — follows the lowered head (world).</summary>
        public Vector2 EyeWorld(int side) => parts[side < 0 ? "eye_l" : "eye_r"].transform.position;

        /// <summary>Detach the head group to draw in front of the floor (lunge) / re-attach it to the torso.</summary>
        private void SetHeadDetached(bool on)
        {
            if (headDetached == on || headRoot == null) return;
            headDetached = on;
            foreach (var n in headParts)
                if (parts.TryGetValue(n, out var r)) r.transform.SetParent(on ? headRoot : torso, false);
            headGroup.sortingOrder = LungeOrder;
        }

        /// <summary>Current hand position (world).</summary>
        public Vector2 HandWorld(int side)
        {
            var a = arms[side < 0 ? 0 : 1];
            return Root.TransformPoint(Px(a.hand_px));
        }

        /// <summary>Body point (source px, y down) -> world. E.g. eye socket (-35,-150), sternum (0,0), top of the head (0,-200).</summary>
        public Vector2 PointWorld(Vector2 px) => torso.TransformPoint(Px(px));
        /// <summary>Torso transform (cracks running over chest / head are parented here so they follow the breathing).</summary>
        public Transform TorsoTransform => torso;
        /// <summary>Transform chain from the HAND -> arm segments (wrist -> elbow -> upper arm) -> SHOULDER — the crawl path of the ending cracks.</summary>
        public List<Transform> ArmPathTransforms(int side)
        {
            var a = arms[side < 0 ? 0 : 1];
            var list = new List<Transform>();
            if (a.hand != null) list.Add(a.hand.transform);
            for (int i = a.pieces.Count - 1; i >= 0; i--) if (a.pieces[i] != null) list.Add(a.pieces[i].transform);
            if (a.shoulder != null) list.Add(a.shoulder.transform);
            return list;
        }

        /// <summary>Wake a part group (dormant -> awake sprites + white flash). "head" = head + eyes; "spine" = ribs + sternum + both arms; "ears" = jaw.</summary>
        public void AwakenGroup(string group)
        {
            string[] names = group == "head" ? new[] { "head", "eye_l", "eye_r" }
                           : group == "spine" ? new[] { "ribs_0", "ribs_1", "ribs_2", "ribs_3", "ribs_4", "sternum" }
                           : group == "ears" ? new[] { "jaw" } : torsoParts;
            foreach (var n in names) SetPartAwake(n, true);
            if (group == "spine" || group == "all") for (int k = 0; k < 2; k++) SetArmAwake(arms[k], true);
            Awake = true;
            foreach (var n in torsoParts) if (!partAwake[n]) Awake = false;
        }

        public void SetAllAwake(bool on)
        {
            foreach (var n in torsoParts) SetPartAwake(n, on);
            for (int k = 0; k < 2; k++) SetArmAwake(arms[k], on);
            Awake = on;
        }

        private void SetPartAwake(string n, bool on)
        {
            if (!parts.TryGetValue(n, out var r)) return;
            if (partAwake[n] != on) flashUntil[n] = Time.time + 0.35f;
            partAwake[n] = on;
            r.sprite = Spr(n, on);
        }

        /// <summary>One arm awake / dark (a dead hand greys out).</summary>
        public void SetArmAwake(int side, bool on) => SetArmAwake(arms[side < 0 ? 0 : 1], on);

        private void SetArmAwake(Arm a, bool on)
        {
            if (a.awake == on) return;
            a.awake = on;
            a.shoulder.sprite = Spr("shoulder", on);
            for (int i = 0; i < a.pieces.Count; i++) a.pieces[i].sprite = Spr("arm_" + (i % 5), on);
            a.hand.sprite = Spr(a.handSprite, on);
            flashUntil["arm" + a.side] = Time.time + 0.35f;
        }

        public void Destroy() { if (Root != null) Object.Destroy(Root.gameObject); }

        // ------------------------------------------------------------------ every frame
        public void Tick(float dt)
        {
            if (Root == null) return;
            float t = Time.time + t0;
            bool any = Awake || partAwake["head"] || partAwake["sternum"];
            // breathing: awake = clear 3.2 s cycle; dormant = 7 s, very faint
            float period = any ? 3.2f : 7f, amp = any ? 1f : 0.35f;
            float br = Mathf.Sin(t * Mathf.PI * 2f / period) * amp;
            float brLate = Mathf.Sin((t - 0.35f) * Mathf.PI * 2f / period) * amp;
            float slumpPx = Slump * 8f;
            float lu = Mathf.Clamp01(Lunge); lu = lu * lu * (3f - 2f * lu);                // head lunge only
            SetHeadDetached(lu > 0.04f);
            float dip = Mathf.Min(HeadDip * 75f + lu * HeadLungePx, MaxDipPx), dipK = 1f + 0.12f * HeadDip + 0.1f * lu;

            for (int k = 0; k < 5; k++)
                Place("ribs_" + k, new Vector2(0f, RibY[k] + br * (k - 2) * 0.7f - br * 1.5f + slumpPx * 0.5f));
            Place("sternum", new Vector2(0f, -br * 1.5f + slumpPx * 0.5f));
            float headDy = HeadY - brLate * 1.6f + slumpPx * 1.3f + dip;
            float jaw = JawOpen * 12f + (any ? Mathf.Max(0f, Mathf.Sin(t * 0.9f)) * 1.2f : 0f);
            Place("head", new Vector2(0f, headDy));
            jawPx = new Vector2(0f, JawY - brLate * 1.4f + slumpPx * 1.2f + jaw + dip * 1.05f);
            Place("jaw", jawPx);
            Place("eye_l", new Vector2(-EyeX * dipK, EyeY - brLate * 1.6f + slumpPx * 1.3f + dip));
            Place("eye_r", new Vector2(EyeX * dipK, EyeY - brLate * 1.6f + slumpPx * 1.3f + dip));
            foreach (var n in new[] { "head", "jaw", "eye_l", "eye_r" }) parts[n].transform.localScale = new Vector3(dipK, dipK, 1f);
            parts["eye_l"].enabled = partAwake["eye_l"];
            parts["eye_r"].enabled = partAwake["eye_r"];
            float tilt = Slump * 4f;
            parts["head"].transform.localRotation = Quaternion.Euler(0f, 0f, tilt);
            parts["jaw"].transform.localRotation = Quaternion.Euler(0f, 0f, tilt * 0.6f);

            // white flash when a part has just woken
            foreach (var n in torsoParts)
            {
                float f = flashUntil.TryGetValue(n, out var fu) ? Mathf.Clamp01((fu - Time.time) / 0.35f) : 0f;
                var r = parts[n];
                float shade = partAwake[n] ? 0.95f : 0.8f;
                r.color = Color.Lerp(new Color(shade, shade, shade, 1f), new Color(1.6f, 1.4f, 1.9f, 1f), f) * Tint;
            }

            for (int k = 0; k < 2; k++) TickArm(arms[k], dt, br, slumpPx, t);

            // mouth / eye lights follow the lowered head
            if (mouthLight != null) mouthLight.transform.localPosition = Px(jawPx + new Vector2(0f, 10f));
            if (eyeLight != null) eyeLight.transform.localPosition = Px(new Vector2(0f, EyeY - brLate * 1.6f + slumpPx * 1.3f + dip));
            // lights: mouth / eyes / sternum core — only when awake
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 2.3f);
            if (mouthLight != null) mouthLight.intensity = partAwake["jaw"] ? 0.7f + 0.3f * pulse + JawOpen * 1.2f : 0f;
            if (eyeLight != null) eyeLight.intensity = partAwake["eye_l"] ? 0.6f + 0.25f * pulse : 0f;
            if (coreLight != null) coreLight.intensity = partAwake["sternum"] ? 0.45f + 0.25f * pulse : 0.05f;
        }

        private void Place(string n, Vector2 px) { if (parts.TryGetValue(n, out var r)) r.transform.localPosition = Px(px); }

        private void TickArm(Arm a, float dt, float br, float slumpPx, float t)
        {
            float s = a.side;
            // rest target bobs slightly; attacks set a different target_px
            Vector2 target = a.target_px;
            if ((a.target_px - new Vector2(Mx(HandRest.x, s), HandRest.y)).sqrMagnitude < 4f)
                target += new Vector2(Mathf.Sin(t * 0.7f + s) * 2.5f, br * 2f + slumpPx * 0.8f);
            a.hand_px = Vector2.Lerp(a.hand_px, target, 1f - Mathf.Exp(-a.follow * Mathf.Max(0f, dt)));
            if (dt <= 0f) a.hand_px = target;

            Vector2 p0 = new Vector2(Mx(ArmP0.x, s), ArmP0.y - br * 1.2f + slumpPx * 0.5f);
            Vector2 sh = new Vector2(Mx(Shoulder.x, s), Shoulder.y - br * 1.2f + slumpPx * 0.5f);
            Vector2 wrist = a.hand_px + new Vector2(Mx(WristFromHand.x, s), WristFromHand.y);
            Vector2 cp = (p0 + wrist) * 0.5f + new Vector2(Mx(ElbowBulge.x, s), ElbowBulge.y);
            a.shoulder.transform.localPosition = Px(sh);

            // space segments evenly by arc length (40-sample table)
            const int N = 40;
            var pts = new Vector2[N + 1]; var acc = new float[N + 1];
            for (int i = 0; i <= N; i++)
            {
                pts[i] = Bez(p0, cp, wrist, i / (float)N);
                acc[i] = i == 0 ? 0f : acc[i - 1] + Vector2.Distance(pts[i], pts[i - 1]);
            }
            float L = acc[N];
            int n = a.pieces.Count, j = 0;
            float flash = flashUntil.TryGetValue("arm" + a.side, out var fu) ? Mathf.Clamp01((fu - Time.time) / 0.35f) : 0f;
            Color col = Color.Lerp(a.awake ? new Color(0.95f, 0.95f, 0.95f, 1f) : new Color(0.8f, 0.8f, 0.8f, 1f), new Color(1.6f, 1.4f, 1.9f, 1f), flash) * Tint;
            for (int i = 0; i < n; i++)
            {
                float d = L * i / Mathf.Max(1, n - 1);
                while (j < N - 1 && acc[j + 1] < d) j++;
                float seg = Mathf.Max(0.0001f, acc[j + 1] - acc[j]);
                Vector2 q = Vector2.Lerp(pts[j], pts[j + 1], (d - acc[j]) / seg);
                Vector2 tg = pts[j + 1] - pts[j];                                     // px, y down
                var tr = a.pieces[i].transform;
                tr.localPosition = Px(q);
                tr.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(tg.x, tg.y) * Mathf.Rad2Deg);   // segment "down" axis follows the tangent
                float sc = Mathf.Lerp(ArmScale0, ArmScale1, i / (float)Mathf.Max(1, n - 1));
                tr.localScale = new Vector3(sc, sc, 1f);
                a.pieces[i].color = col;
            }
            a.hand.transform.localPosition = Px(a.hand_px);
            a.hand.color = col * a.handTint;
            a.shoulder.color = col;
        }

        /// <summary>Source x (left arm) -> arm on side `s` (-1 left unchanged, +1 right mirrored).</summary>
        private static float Mx(float x, float s) => s < 0f ? x : -x;

        private static Vector2 Bez(Vector2 a, Vector2 b, Vector2 c, float u) { float w = 1f - u; return w * w * a + 2f * w * u * b + u * u * c; }

        private static float ArcLength(Vector2 a, Vector2 b, Vector2 c)
        {
            float L = 0f; Vector2 prev = a;
            for (int i = 1; i <= 40; i++) { var q = Bez(a, b, c, i / 40f); L += Vector2.Distance(q, prev); prev = q; }
            return L;
        }
    }
}
