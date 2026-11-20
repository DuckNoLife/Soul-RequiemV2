using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// SPECIMEN ZERO (boss ẩn, Containment Vault) — thân xác thất bại của The Vessel: khuôn mặt khổng lồ, thân là bó rễ thịt, khe mắt dọc
    /// giữa ngực, bị xích vào tường. Không di chuyển. 04/11 (người dùng): làm lại bộ chiêu cho khớp với hình — bỏ loạt cầu tím
    /// (thân không có chỗ nào phun ra được). Mỗi chiêu có báo trước rõ ràng:
    ///   Grasping Roots  : vết nứt tím chạy từ chân boss tới chỗ Ren, rồi rễ trồi lần lượt dọc vết nứt — trúng: sát thương + chậm.
    ///                     Pha 2: 3 vết nứt xoè quạt.
    ///   Tentacle Lash   : một làn ngang ngang tầm Ren sáng lên, xúc tu trồi nối đuôi nhau quét từ mép phòng bên này sang bên kia.
    ///   Vessel's Gaze   : khe mắt giữa ngực mở (đèn tím dâng, vạch nhắm mảnh), rồi tia quét một cung qua chỗ Ren — lăn xuyên tia để né.
    ///   Requiem Wail    : 05/11 (bản 4) — 4 TRỤ PHONG ẤN (sprite_prop nhóm sz_ward). Boss hít vào 2,6 s (pha 2: 2,2 s): MỘT trụ bừng sáng,
    ///                     RUNE của trụ đó sáng to dưới chân boss + vệt chấm vàng chạy tới vòng ấn của trụ; vòng ĐỎ bám dưới chân Ren
    ///                     (đứng trong vòng ấn -> chuyển vàng). Sóng: trong vòng ấn = an toàn, ngoài = mất máu (lăn vẫn né). Vòng đầu khoá
    ///                     mantra. 05/11 tối: BỎ ngắt chiêu (dồn 5% máu lúc hít vào -> nghẹn) vì quá dễ — đòn đặc trưng bị bỏ qua.
    ///                     Thay bằng phần thưởng SAU khi vượt qua: gào xong boss KIỆT SỨC (Spent) 2,5 s, gục mặt, nhận +50% sát thương.
    ///   Hand of the Vessel: 04/11 — bóng tối loang dưới chân Ren bám theo rồi khoá, một bàn tay xác khổng lồ (PixelLab) vọt lên tóm:
    ///                     sát thương lớn + hất văng. Pha 2: 3 bàn tay đuổi nối tiếp.
    ///   Writhing Bed    : 05/11 tối — chống "ôm boss chém liên tục": Ren đứng sát chân boss quá 2,5 s -> sàn quanh boss cuộn tím (0,8 s),
    ///                     rồi 3 vòng rễ bung ra, hất Ren văng xa. Hồi 7 s.
    ///   Pha 2 (≤50%)    : xích đứt (rung mạnh, loé tím), 4 MẮT TƯỜNG (prop PixelLab nhóm sz_walleye trên tường bắc) mở to.
    ///   The Watchers    : 05/11 tối — mắt tường thành MỘT ĐÒN có lịch (trước đây mỗi mắt tự bắn ngẫu nhiên -> mất máu "không rõ từ đâu"):
    ///                     4 mắt lần lượt (từ mắt xa Ren nhất) khoá DẤU NGẮM hình mắt dưới chân Ren, mắt nạp sáng, rồi tia đỏ thẫm
    ///                     (viền tối + lõi trắng, nổi trên viền đỏ màn hình lúc thấp máu) đâm xuống, nổ + vết cháy. Mỗi phát cách 0,55 s
    ///                     -> phải chạy liên tục; xong loạt có nhịp nghỉ. Ảnh VFX: Resources/VFX/specimen/eyefx_* (thiếu -> vẽ bằng code).
    /// Hình rễ: Resources/VFX/specimen/roots{a..d}_0..7 — 4 kiểu rễ PixelLab (ảnh gốc Catalog/v2/art/pixellab_roots_source.png), mỗi kiểu
    /// dựng thành 8 khung "trồi lên - đứng - lún xuống"; mỗi gốc rễ chọn ngẫu nhiên một kiểu. Không có thì dùng khung xúc tu nhuộm nâu.
    /// Hạ được -> thành tựu "Breaker of Seals" + cờ specimen_zero_slain.
    /// </summary>
    public class SpecimenZeroSkills : MonoBehaviour, IEnemySkills
    {
        public Sprite[] tentacleFrames;
        public Sprite[] rootFrames;                           // để trống -> Resources/VFX/specimen/roots{a..d}_*
        public GameObject boltPrefab;                         // (không dùng từ 04/11 — giữ cho prefab cũ)
        public Vector2 eyeOffset = new Vector2(0f, 0.67f);   // khe mắt giữa thân (px 134/208), nhân scale của quái
        public float leash = 6.5f;                            // xa hơn = người chơi đã ra khỏi phòng
        public Color tint = new Color(0.78f, 0.45f, 1f);
        public Color rootTint = new Color(0.62f, 0.42f, 0.55f);
        public string achievementId = "breaker_of_seals", achievementTitle = "Breaker of Seals";
        public string wardGroup = "sz_ward";                  // nhóm sprite_prop trụ phong ấn trong Tiled
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

        [Header("Spent — kiệt sức sau Requiem Wail")]
        public float spentSeconds = 2.5f;
        public float spentDamageMul = 1.5f;
        [Header("Writhing Bed — chống áp sát")]
        public float bedRadius = 1.2f;                        // đo dẹt (y / 0,6) quanh chân boss
        public float bedHugSeconds = 2.5f;
        private float nextBed, nextWatch, closeT;

        // mắt tường đã mở (pha 2)
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

        // đếm thời gian Ren "ôm" chân boss (rời ra thì giảm nhanh gấp đôi) -> Writhing Bed
        private void Update()
        {
            if (!engaged || ai == null || ai.Player == null || ai.Health == null || ai.Health.IsDead) return;
            Vector2 d = (Vector2)ai.Player.position - Base;
            bool hug = !wailing && new Vector2(d.x, d.y / 0.6f).magnitude < bedRadius;
            closeT = hug ? closeT + Time.deltaTime : Mathf.Max(0f, closeT - Time.deltaTime * 2f);
        }

        // thân boss cao ~3 đơn vị: người chơi đứng phía sau thì làm mờ thân để vẫn thấy nhân vật
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
            if (!Mathf.Approximately(c.a, fade)) { c.a = fade; body.color = c; }   // đặt lại mỗi khung (Flash ghi đè màu)
        }

        private Vector2 Base => transform.position;
        private Vector2 Eye => (Vector2)transform.position + eyeOffset * transform.localScale.y;
        private int Order => ai.Body != null ? ai.Body.sortingOrder : 2;
        private bool P2 => ai.InPhase2;
        private float Pace => P2 ? 0.72f : 1f;                 // pha 2: mọi hồi chiêu ngắn lại

        public IEnumerator Act(EnemyAI owner)
        {
            ai = owner;
            ai.Desired = Vector2.zero;
            if (!engaged)
            {
                engaged = true;
                var dir = MapEncounterDirector.Instance;
                hasRoom = dir != null && dir.TryGetZoneAt(transform.position, out _, out room);
                LoadRoots();
                LoadHand();
                ai.PlayState("Awake");
                Hints.Show("Specimen Zero awakens.", 3f);
                yield return ai.Flash(1.4f, tint);
                nextRoots = Time.time + 0.6f;
                nextLash = Time.time + 4f;
                nextGaze = Time.time + 7f;
                nextWail = Time.time + 11f;
                nextHand = Time.time + 5.5f;
                nextBed = Time.time + 3f;
            }
            if (ai.Player == null) yield break;
            // xích: chỉ đánh khi người chơi còn trong Containment Vault
            if (((Vector2)ai.Player.position - Base).magnitude > leash) { yield return ai.WaitFor(0.5f); yield break; }

            if (P2 && !chainsBroken) { chainsBroken = true; yield return ChainsBreak(); }
            else if (closeT >= bedHugSeconds && Time.time >= nextBed) yield return Bed();
            else
            {
                string pick = PickAttack();
                if (pick == null) { yield return ai.WaitFor(0.3f); yield break; }
                switch (pick)
                {
                    case "wail": yield return Wail(); break;
                    case "watch": yield return Watchers(); break;
                    case "gaze": yield return Gaze(); break;
                    case "hand": yield return Hand(); break;
                    case "lash": yield return Lash(); break;
                    default: yield return Roots(); break;
                }
                AfterAttack(pick);
            }
            yield return ai.WaitFor(P2 ? 0.45f : 0.65f);
        }

        // ------------------------------------------------------------------ 0. Chọn chiêu (05/11 tối)
        // Lỗi cũ (ảnh chạy thử: "boss spam shockwave, mắt tường không ra chiêu"): chọn theo THỨ TỰ ƯU TIÊN cố định + hồi chiêu tính từ LÚC BẮT ĐẦU.
        // Requiem Wail dài ~11 s (hít 2,2 + 4 vòng + đổi trụ + Spent) > hồi chiêu pha 2 (14 x 0,72 = 10 s) -> vừa xong đã sẵn, lại đứng đầu ưu tiên
        // -> gào liên tục, The Watchers / Gaze / Hand không bao giờ tới lượt.
        // Cách mới: (1) hồi chiêu tính từ lúc đòn KẾT THÚC; (2) bốc ngẫu nhiên có trọng số trong các đòn đã sẵn, không lặp lại đòn vừa dùng;
        // (3) đòn đặc trưng Wail chỉ ra khi đã có ít nhất 4 (pha 2: 3) đòn khác xen giữa — khi đủ điều kiện thì luôn được chọn (nhịp đều, đoán được).
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
            if (ids.Count == 0) return now >= nextRoots ? "roots" : null;   // chỉ còn rễ (vừa dùng) -> vẫn cho lặp, rễ là đòn lót
            float sum = 0f;
            foreach (var w in ws) sum += w;
            float r = Random.value * sum;
            for (int i = 0; i < ids.Count; i++) { r -= ws[i]; if (r <= 0f) return ids[i]; }
            return ids[ids.Count - 1];
        }

        /// <summary>Đặt hồi chiêu tính từ lúc đòn kết thúc + đếm số đòn xen giữa.</summary>
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

        // ------------------------------------------------------------------ 1. Grasping Roots
        private IEnumerator Roots()
        {
            nextRoots = Time.time + 3.4f * Pace;
            if (rootSets.Count == 0) yield break;
            Vector2 target = ai.Player.position;
            Vector2 start = Base + Vector2.up * 0.15f;
            Vector2 main = target - start;
            float len = Mathf.Clamp(main.magnitude + 0.6f, 1.5f, 6f);
            var dirs = new List<Vector2> { main.normalized };
            if (P2) { dirs.Add(EtherVfx.Rotate(main.normalized, 28f)); dirs.Add(EtherVfx.Rotate(main.normalized, -28f)); }

            // vết nứt báo trước
            var cracks = new List<SpriteRenderer>();
            foreach (var d in dirs) cracks.Add(Line(start, start + d * len, 0.05f, new Color(0.55f, 0.25f, 0.75f, 0f), Order - 3));
            ai.PlayState("Awake");
            for (float t = 0f; t < 0.7f; t += Time.deltaTime)
            {
                foreach (var c in cracks) if (c != null) c.color = new Color(0.55f, 0.25f, 0.75f, 0.25f + 0.55f * (t / 0.7f) * (0.7f + 0.3f * Mathf.Sin(t * 30f)));
                yield return null;
            }
            foreach (var c in cracks) if (c != null) Destroy(c.gameObject);
            // rễ trồi nối đuôi dọc vết nứt
            const float step = 0.38f;
            int n = Mathf.CeilToInt(len / step);
            for (int i = 1; i <= n && !ai.Health.IsDead; i++)
            {
                foreach (var d in dirs)
                {
                    Vector2 p = start + d * (i * step);
                    if (hasRoom && !room.Contains(p)) continue;
                    var fr = rootSets[Random.Range(0, rootSets.Count)];
                    bool fallback = fr == tentacleFrames;
                    var g = GroundStrike.Spawn(p, 0.12f, 0.24f, ai.damage * 0.7f, fr, Mathf.Min(2, fr.Length - 1), 16f, Order, tint);
                    g.bodyScale = fallback ? 0.9f : Random.Range(0.85f, 1.05f);
                    g.bodyColor = fallback ? rootTint : Color.white;
                    g.onHit = h => { var st = h != null ? h.GetComponent<PlayerStatus>() : null; if (st != null) st.Slow(0.5f, 1.4f, "Roots grip your legs!"); };
                }
                if (i % 3 == 0) CameraFollow.KickMain(Random.insideUnitCircle, 0.015f, 0.08f);
                yield return new WaitForSeconds(0.05f);
            }
            yield return ai.WaitFor(0.4f);
        }

        // ------------------------------------------------------------------ 2. Tentacle Lash
        private IEnumerator Lash()
        {
            nextLash = Time.time + 6.5f * Pace;
            if (tentacleFrames == null || tentacleFrames.Length == 0 || !hasRoom) yield break;
            float y = Mathf.Clamp(ai.Player.position.y, room.yMin + 0.3f, room.yMax - 0.3f);
            bool fromLeft = ai.Player.position.x > room.center.x;          // quét từ phía xa Ren về phía Ren
            float x0 = fromLeft ? room.xMin + 0.2f : room.xMax - 0.2f, x1 = fromLeft ? room.xMax - 0.2f : room.xMin + 0.2f;
            var lane = Bar(new Vector2((x0 + x1) * 0.5f, y), new Vector2(Mathf.Abs(x1 - x0), 0.5f), new Color(0.7f, 0.3f, 1f, 0f), Order - 3);
            Hints.Show("The floor ripples - tentacles surge across the vault!", 2f);
            for (float t = 0f; t < 0.9f; t += Time.deltaTime)
            {
                lane.color = new Color(0.7f, 0.3f, 1f, 0.12f + 0.18f * Mathf.Abs(Mathf.Sin(t * 9f)));
                yield return null;
            }
            Destroy(lane.gameObject);
            int n = Mathf.CeilToInt(Mathf.Abs(x1 - x0) / 0.42f);
            for (int i = 0; i <= n && !ai.Health.IsDead; i++)
            {
                Vector2 p = new Vector2(Mathf.Lerp(x0, x1, i / (float)n), y + (i % 2 == 0 ? 0.08f : -0.08f));
                var g = GroundStrike.Spawn(p, 0.05f, 0.3f, ai.damage * 0.9f, tentacleFrames, Mathf.Min(2, tentacleFrames.Length - 1), 18f, Order, tint);
                g.bodyScale = 1.6f; g.bodyColor = tint; g.knockback = 3f;
                yield return new WaitForSeconds(P2 ? 0.035f : 0.05f);
            }
            yield return ai.WaitFor(0.5f);
        }

        // ------------------------------------------------------------------ 3. Vessel's Gaze
        private IEnumerator Gaze()
        {
            nextGaze = Time.time + 9f * Pace;
            Vector2 eye = Eye;
            var glowGo = new GameObject("SZ Eye Glow"); fx.Add(glowGo);
            glowGo.transform.position = eye;
            var glow = glowGo.AddComponent<Light2D>();
            glow.lightType = Light2D.LightType.Point; glow.color = tint; glow.pointLightOuterRadius = 2.2f; glow.pointLightInnerRadius = 0.1f;
            var aim = Line(eye, eye, 0.025f, new Color(tint.r, tint.g, tint.b, 0.35f), Order + 2);
            Hints.Show("The slit in its chest peels open...", 2f);
            // nạp: đèn dâng, vạch nhắm bám theo Ren
            Vector2 toP = Vector2.down;
            for (float t = 0f; t < 1.1f && !ai.Health.IsDead; t += Time.deltaTime)
            {
                if (ai.Player != null) toP = ((Vector2)ai.Player.position + Vector2.up * 0.25f - eye).normalized;
                Place(aim, eye, toP, 9f, 0.025f);
                glow.intensity = 3f * (t / 1.1f);
                yield return null;
            }
            Destroy(aim.gameObject);
            // quét: cung 70° (pha 2: 100°) đi qua chỗ Ren đứng lúc khoá
            float arc = P2 ? 100f : 70f, dur = P2 ? 1.3f : 1.6f, sideSign = Random.value < 0.5f ? 1f : -1f;
            var beam = EtherVfx.Spawn("SZ Gaze", EtherVfx.Beam, eye, tint, ai.Body != null ? ai.Body.sortingLayerID : 0, Order + 3);
            fx.Add(beam.gameObject);
            float nextTick = 0f;
            for (float t = 0f; t < dur && !ai.Health.IsDead; t += Time.deltaTime)
            {
                float a = Mathf.Lerp(-arc * 0.5f, arc * 0.5f, t / dur) * sideSign;
                Vector2 d = EtherVfx.Rotate(toP, a);
                float reach = 9f;
                beam.transform.position = eye;
                beam.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                beam.transform.localScale = new Vector3(reach / EtherVfx.BeamWidthUnits, (0.22f + 0.04f * Mathf.Sin(t * 40f)) / EtherVfx.BeamHeightUnits, 1f);
                glow.intensity = 3f + Mathf.Sin(t * 30f);
                if (t >= nextTick && ai.Player != null)
                {
                    Vector2 pp = (Vector2)ai.Player.position + Vector2.up * 0.25f;
                    if (DistToSeg(pp, eye, eye + d * reach) < 0.2f) { nextTick = t + 0.25f; ai.Hurt(ai.damage * 0.45f, eye, 1.2f); }
                }
                if (Random.value < 0.25f) CameraFollow.KickMain(Random.insideUnitCircle, 0.012f, 0.06f);
                yield return null;
            }
            Destroy(beam.gameObject);
            Destroy(glowGo);
            yield return ai.WaitFor(0.5f);
        }

        // ------------------------------------------------------------------ 4. Requiem Wail
        // 05/11 bản 4 (chạy thử: "cột vẫn chưa có tác dụng, sóng chưa báo trước, phòng rộng không biết trốn cột nào, chiêu khác chen vào"):
        //  - MỘT trụ phong ấn bừng sáng; RUNE của chính trụ đó sáng to dưới chân boss + vệt chấm vàng chạy từ boss tới vòng ấn của trụ.
        //  - VÒNG ĐỎ bám dưới chân Ren suốt lúc boss hít vào; đứng trong vòng ấn của trụ sáng -> vòng chuyển vàng (đã an toàn).
        //  - Sóng nổ: Ren trong vòng ấn = không mất máu (vòng ấn loé); ngoài vòng ấn = trúng (lăn vẫn né). Vòng đầu khoá mantra.
        //  - Đang gào: mọi chiêu khác dừng (mắt tường chờ, đang nhắm thì huỷ) — người chơi đang phải đứng yên trong vòng.
        //  - 3 vòng; pha 2: sau vòng 1 trụ khác bừng sáng (phải chạy sang).
        //  - 05/11 tối: bỏ "dồn 5% máu -> nghẹn" (2 nhát chém là xoá đòn đặc trưng). Gào xong -> Spent (kiệt sức, +50% sát thương).
        private const float SigilRadius = 0.42f;                 // bán kính an toàn của vòng ấn (đo dẹt như sóng)
        private bool wailing;
        private SpriteRenderer runeGlyph, footRing;
        private readonly List<SpriteRenderer> trail = new List<SpriteRenderer>();

        private IEnumerator Wail()
        {
            nextWail = Time.time + 14f * Pace;
            var wards = SpritePropMechanism.Group(wardGroup);
            var ward = PickWard(wards, null);
            if (ward == null) yield break;                       // map không có trụ: không dùng chiêu này (không có cách núp)
            wailing = true;
            try
            {
                Vector2 c = Base + Vector2.up * 0.1f;
                float cover = CoverRadius(c), speed = 3.4f;
                float warn = P2 ? 2.2f : 2.6f;
                Hints.Show("Specimen Zero draws a rattling breath - its rune flares on a ward-stone. Get inside that ward's circle!", warn + 0.8f);
                for (float t = 0f; t < warn && !ai.Health.IsDead; t += Time.deltaTime)
                {
                    float k = t / warn;
                    GlowWard(ward, k, t);
                    ShowRune(ward, c, k, t);
                    ShowTrail(c, ward.DecalAt, t);
                    ShowFootRing(ward, k, t);
                    if (t > warn - 0.5f && Random.value < 0.35f) CameraFollow.KickMain(Random.insideUnitCircle, 0.04f, 0.1f);
                    yield return null;
                }
                if (ai.Health.IsDead) yield break;
                yield return ai.Flash(0.2f, new Color(1f, 0.55f, 0.85f));
                int rings = 3;                                        // pha 2: vẫn 3 vòng nhưng đổi trụ giữa chừng (bản cũ 4 vòng: đòn quá dài)
                for (int i = 0; i < rings && !ai.Health.IsDead; i++)
                {
                    if (P2 && i == 1)
                    {
                        var next = PickWard(wards, ward);
                        if (next != null)
                        {
                            Hints.Show("Another ward wakes - move!", 1.6f);
                            StartCoroutine(DimWard(ward, 0.9f));
                            ward = next;
                            for (float t = 0f; t < 1.4f && !ai.Health.IsDead; t += Time.deltaTime)
                            {
                                GlowWard(ward, t / 1.4f, t); ShowRune(ward, c, 1f, t); ShowTrail(c, ward.DecalAt, t); ShowFootRing(ward, 1f, t);
                                yield return null;
                            }
                        }
                    }
                    var w = Shockwave.Spawn(c, cover, cover / speed, ai.damage * 0.6f, Order - 1, new Color(tint.r, tint.g, tint.b, 0.85f));
                    w.knockback = 2f;
                    var wd = ward;
                    w.shelter = p => InSigil(wd, p);
                    w.onSheltered = () => { if (this != null && isActiveAndEnabled) StartCoroutine(Sheltered(wd)); };
                    if (i == 0)
                        w.onHit = h => { var st = h != null ? h.GetComponent<PlayerStatus>() : null; if (st != null) st.Silence(P2 ? 3.5f : 2.5f, "The wail drowns out your mantras!"); };
                    float hold = P2 ? 0.75f : 0.6f;
                    for (float t = 0f; t < hold && !ai.Health.IsDead; t += Time.deltaTime)
                    {
                        GlowWard(ward, 1f, Time.time); ShowRune(ward, c, 1f, Time.time); ShowFootRing(ward, 1f, Time.time);
                        yield return null;
                    }
                }
                for (float t = 0f; t < cover / speed && !ai.Health.IsDead; t += Time.deltaTime)
                {
                    GlowWard(ward, 1f, Time.time); ShowFootRing(ward, 1f, Time.time);
                    yield return null;
                }
                ClearWailFx();
                StartCoroutine(DimWard(ward, 0.6f));
                yield return Spent();
            }
            finally
            {
                wailing = false;
                ClearWailFx();
            }
        }

        /// <summary>Ren (chân) đứng trong vòng ấn của trụ đang sáng.</summary>
        private static bool InSigil(SpritePropMechanism ward, Vector2 p)
        {
            if (ward == null || ward.Body == null) return false;
            Vector2 d = p - ward.DecalAt;
            return new Vector2(d.x, d.y / 0.6f).magnitude <= SigilRadius;
        }

        /// <summary>Rune của trụ sáng sáng to dưới chân boss (cùng hình với rune khắc trên trụ — nhìn là biết chạy tới trụ nào).</summary>
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
            float s = 1.25f / Mathf.Max(0.01f, icon.bounds.size.y);                 // cao ~1,25 đv trên sàn
            float pop = 1f + 0.06f * Mathf.Sin(t * 7f);
            runeGlyph.transform.position = c + Vector2.down * 0.25f;
            runeGlyph.transform.localScale = new Vector3(s * pop, s * 0.6f * pop, 1f);
            runeGlyph.color = new Color(wardGold.r, wardGold.g, wardGold.b, Mathf.Lerp(0.15f, 0.95f, Mathf.Clamp01(k * 1.6f)));
        }

        /// <summary>Vệt chấm vàng chạy từ chân boss tới vòng ấn của trụ sáng (chấm sáng chạy dọc vệt).</summary>
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
                float wave = Mathf.Repeat(u - t * 0.9f, 1f);                         // chấm sáng chạy về phía trụ
                trail[i].color = new Color(wardGold.r, wardGold.g, wardGold.b, 0.25f + 0.65f * Mathf.Pow(1f - wave, 4f));
            }
        }

        /// <summary>Vòng cảnh báo bám dưới chân Ren: đỏ = sẽ trúng sóng; vàng = đang đứng trong vòng ấn (an toàn).</summary>
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

        /// <summary>Bán kính (đo dẹt như Shockwave) đủ để sóng phủ cả phòng — không còn góc nào đứng yên mà né được.</summary>
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

        /// <summary>Chọn ngẫu nhiên một trong 2 trụ gần Ren nhất (bỏ trụ `except`): luôn chạy tới kịp, nhưng không phải lúc nào cũng là trụ sát bên.</summary>
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

        /// <summary>Thắp trụ theo tiến độ k (0..1): đổi ảnh rune vàng, đèn nến dâng, vòng ấn trên sàn sáng dần và đập nhẹ.</summary>
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

        private IEnumerator DimWard(SpritePropMechanism ward, float dur)
        {
            if (ward == null || ward.Body == null) yield break;
            float i0 = ward.Glow.intensity, a0 = ward.Decal != null ? ward.Decal.color.a : 0f;
            for (float t = 0f; t < dur && ward.Body != null; t += Time.deltaTime)
            {
                float k = t / dur;
                ward.Glow.intensity = Mathf.Lerp(i0, 0f, k);
                if (ward.Decal != null) ward.Decal.color = new Color(1f, 1f, 1f, Mathf.Lerp(a0, ward.DecalAlpha, k));
                yield return null;
            }
            ward.SetLit(false);
            litWards.Remove(ward);
        }

        /// <summary>Sóng vừa đi qua mà Ren đứng trong vòng ấn: vòng ấn loé, đèn giật lên.</summary>
        private IEnumerator Sheltered(SpritePropMechanism ward)
        {
            if (ward == null || ward.Body == null) yield break;
            if (!shelterTold) { shelterTold = true; Hints.Show("The ward holds - the wail breaks around the stone.", 2.2f); }
            CameraFollow.KickMain(Vector2.up, 0.015f, 0.08f);
            Vector3 s0 = ward.Decal != null ? ward.Decal.transform.localScale : Vector3.one;
            for (float t = 0f; t < 0.3f && ward.Body != null; t += Time.deltaTime)
            {
                float k = 1f - t / 0.3f;
                ward.Glow.intensity = 2.2f + 2.5f * k;
                if (ward.Decal != null) { ward.Decal.color = new Color(1f, 1f, 1f, 1f); ward.Decal.transform.localScale = s0 * (1f + 0.12f * k); }
                yield return null;
            }
            if (ward.Decal != null) ward.Decal.transform.localScale = s0;
        }

        // ------------------------------------------------------------------ 5. Hand of the Vessel
        // 04/11: bóng tối loang dưới chân Ren bám theo (0,9 s), khoá lại (vòng cảnh báo co), rồi bàn tay xác vọt lên tóm.
        // Pha 2: 3 bàn tay nối tiếp, mỗi tay nhắm chỗ Ren đứng lúc đó — phải chạy liên tục hoặc lăn đúng lúc.
        private IEnumerator Hand()
        {
            nextHand = Time.time + 8f * Pace;
            if (handFrames == null || handFrames.Length == 0) yield break;
            int n = P2 ? 3 : 1;
            Hints.Show("The floor beneath you darkens - something reaches up!", 1.8f);
            ai.PlayState("Awake");
            for (int k = 0; k < n && !ai.Health.IsDead && ai.Player != null; k++)
            {
                Vector2 mark = ai.Player.position;
                var shadow = EtherVfx.Spawn("SZ Hand Shadow", Blob, mark, new Color(0.07f, 0.02f, 0.1f, 0f), ai.Body != null ? ai.Body.sortingLayerID : 0, Order - 3);
                fx.Add(shadow.gameObject);
                float track = k == 0 ? 0.9f : 0.45f;
                for (float t = 0f; t < track && ai.Player != null && !ai.Health.IsDead; t += Time.deltaTime)
                {
                    mark = Vector2.Lerp(mark, ai.Player.position, Time.deltaTime * 7f);
                    float g = t / track;
                    shadow.transform.position = mark;
                    shadow.transform.localScale = new Vector3(1f, 0.6f, 1f) * Mathf.Lerp(0.35f, 0.95f, g);
                    shadow.color = new Color(0.07f, 0.02f, 0.1f, 0.75f * g);
                    yield return null;
                }
                if (shadow != null) Destroy(shadow.gameObject);
                var strike = GroundStrike.Spawn(mark, 0.45f, 0.42f, ai.damage * 1.1f, handFrames, 3, 14f, Order, new Color(0.75f, 0.3f, 1f));
                strike.knockback = 3.5f;
                strike.onHit = h => CameraFollow.KickMain(Vector2.up, 0.05f, 0.15f);
                yield return new WaitForSeconds(P2 ? 0.35f : 0.2f);
            }
            yield return ai.WaitFor(0.7f);
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

        /// <summary>Đốm tròn mềm (bóng loang dưới đất), đường kính 1 đơn vị.</summary>
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

        // ------------------------------------------------------------------ 6. Spent — kiệt sức sau Requiem Wail
        // Phần thưởng cho người đã núp đúng trụ: boss gục mặt (khung ngủ "Idle"), sáng nhạt, nhận x1,5 sát thương trong 2,5 s.
        private IEnumerator Spent()
        {
            if (ai.Health.IsDead) yield break;
            Hints.Show("The wail leaves it spent - Specimen Zero slumps. Strike now!", 2.4f);
            CameraFollow.KickMain(Vector2.down, 0.05f, 0.25f);
            ai.PlayState("Idle");
            ai.Health.DamageTakenMul = spentDamageMul;
            try { yield return ai.Flash(spentSeconds, new Color(1f, 0.93f, 0.68f)); }
            finally { if (ai != null && ai.Health != null) ai.Health.DamageTakenMul = 1f; }
            if (!ai.Health.IsDead) ai.PlayState("Awake");
            yield return ai.WaitFor(0.3f);
        }

        // ------------------------------------------------------------------ 7. Writhing Bed — chống áp sát
        // Đĩa tím loang + viền sáng co lại quanh chân boss (0,8 s) -> 3 vòng rễ bung từ trong ra ngoài, hất văng (lăn để né).
        private IEnumerator Bed()
        {
            nextBed = Time.time + 7f * Pace;
            closeT = 0f;
            if (rootSets.Count == 0) yield break;
            Vector2 c = Base + Vector2.up * 0.1f;
            float r = bedRadius + 0.15f;
            int layer = ai.Body != null ? ai.Body.sortingLayerID : 0;
            Hints.Show("The roots around its base writhe - back away!", 1.6f);
            ai.PlayState("Awake");
            var disk = EtherVfx.Spawn("SZ Bed Warn", Blob, c, new Color(0.45f, 0.15f, 0.6f, 0f), layer, Order - 4);
            fx.Add(disk.gameObject);
            disk.transform.localScale = new Vector3(r * 2f, r * 2f * 0.6f, 1f);
            var edge = EtherVfx.Spawn("SZ Bed Edge", EtherVfx.Ring, c, new Color(0.8f, 0.4f, 1f, 0f), layer, Order - 3);
            fx.Add(edge.gameObject);
            float ringK = r * 2f / Mathf.Max(0.01f, EtherVfx.Ring.bounds.size.x);
            const float warn = 0.8f;
            for (float t = 0f; t < warn && !ai.Health.IsDead; t += Time.deltaTime)
            {
                float k = t / warn;
                disk.color = new Color(0.45f, 0.15f, 0.6f, (0.15f + 0.4f * k) * (0.75f + 0.25f * Mathf.Sin(t * 26f)));
                edge.color = new Color(0.8f, 0.4f, 1f, 0.4f + 0.6f * k);
                float s = ringK * Mathf.Lerp(1.15f, 1f, k);
                edge.transform.localScale = new Vector3(s, s * 0.6f, 1f);
                if (t > warn - 0.3f && Random.value < 0.4f) CameraFollow.KickMain(Random.insideUnitCircle, 0.025f, 0.08f);
                yield return null;
            }
            Destroy(disk.gameObject);
            Destroy(edge.gameObject);
            if (ai.Health.IsDead) yield break;
            // 3 vòng rễ nối nhau từ trong ra: ai đứng trong đĩa đều bị một vòng quét qua
            float[] rings = { 0.3f, 0.65f, 1f };
            int[] counts = { 4, 7, 11 };
            for (int i = 0; i < rings.Length && !ai.Health.IsDead; i++)
            {
                float rr = r * rings[i], a0 = Random.value * 360f;
                for (int j = 0; j < counts[i]; j++)
                {
                    float a = (a0 + 360f * j / counts[i]) * Mathf.Deg2Rad;
                    Vector2 p = c + new Vector2(Mathf.Cos(a) * rr, Mathf.Sin(a) * rr * 0.6f);
                    var fr = rootSets[Random.Range(0, rootSets.Count)];
                    var g = GroundStrike.Spawn(p, 0.02f, 0.3f, ai.damage * 0.8f, fr, Mathf.Min(2, fr.Length - 1), 18f, Order, tint);
                    g.bodyScale = Random.Range(0.9f, 1.1f);
                    g.bodyColor = fr == tentacleFrames ? rootTint : Color.white;
                    g.knockback = 4f;
                }
                CameraFollow.KickMain(Vector2.up, 0.04f, 0.12f);
                yield return new WaitForSeconds(0.09f);
            }
            yield return ai.WaitFor(0.5f);
        }

        // ------------------------------------------------------------------ 8. Pha 2: xích đứt + mắt tường mở
        // Mắt tường là PROP thật (sprite_prop nhóm sz_walleye, ảnh PixelLab) nằm nhắm trên tường bắc từ đầu trận; xích đứt -> lần lượt
        // mở to (loé đỏ) từ trái sang phải. Mắt KHÔNG tự bắn — chỉ bắn trong đòn The Watchers (mục 9). Không có prop -> chấm đỏ vẽ tay.
        public string wallEyeGroup = "sz_walleye";

        private IEnumerator ChainsBreak()
        {
            Hints.Show("The chains snap - Specimen Zero tears free of its seals!", 3f);
            for (float t = 0f; t < 1.2f; t += 0.07f) { CameraFollow.KickMain(Random.insideUnitCircle, 0.09f, 0.12f); yield return new WaitForSeconds(0.07f); }
            yield return ai.Flash(0.6f, tint);
            LoadEyeFx();
            watchEyes.Clear();
            var eyes = SpritePropMechanism.Group(wallEyeGroup);
            if (eyes.Count > 0)
            {
                Hints.Show("Eyes open in the vault walls...", 2.5f);
                eyes.Sort((x, y) => x.Foot.x.CompareTo(y.Foot.x));
                for (int i = 0; i < eyes.Count; i++) StartCoroutine(OpenEye(eyes[i], eyes[i].Foot, 0.4f + i * 0.35f));
            }
            else if (hasRoom)
            {
                var spots = new[]
                {
                    new Vector2(room.xMin + 0.25f, room.yMax - 0.35f), new Vector2(room.xMax - 0.25f, room.yMax - 0.35f),
                    new Vector2(room.xMin + 0.25f, room.center.y), new Vector2(room.xMax - 0.25f, room.center.y),
                };
                for (int i = 0; i < spots.Length; i++) StartCoroutine(OpenEye(null, spots[i], i * 0.35f));
            }
            nextWatch = Time.time + 3.5f;
        }

        private IEnumerator OpenEye(SpritePropMechanism prop, Vector2 at, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (ai == null || ai.Health.IsDead) yield break;
            var e = new WatchEye { at = at };
            if (prop != null && prop.Body != null)
            {
                prop.SetLit(true);
                if (!litWards.Contains(prop)) litWards.Add(prop);
                e.light = prop.Glow;
                e.light.color = new Color(1f, 0.22f, 0.3f);
                e.idle = 0.8f;
                for (float t = 0f; t < 0.5f; t += Time.deltaTime) { e.light.intensity = 3f * (1f - t / 0.5f) + e.idle; yield return null; }
            }
            else
            {
                var own = new GameObject("SZ Wall Eye"); fx.Add(own);
                own.transform.position = at;
                e.light = own.AddComponent<Light2D>();
                e.light.lightType = Light2D.LightType.Point; e.light.color = new Color(1f, 0.25f, 0.45f);
                e.light.pointLightOuterRadius = 0.7f; e.light.pointLightInnerRadius = 0.02f; e.light.intensity = 0f;
                var e1 = Bar(at + new Vector2(-0.05f, 0f), new Vector2(0.04f, 0.03f), new Color(1f, 0.3f, 0.5f, 0f), Order + 4, own.transform);
                var e2 = Bar(at + new Vector2(0.05f, 0f), new Vector2(0.04f, 0.03f), new Color(1f, 0.3f, 0.5f, 0f), Order + 4, own.transform);
                e.idle = 1.2f;
                for (float t = 0f; t < 1f; t += Time.deltaTime) { e.light.intensity = e.idle * t; e1.color = e2.color = new Color(1f, 0.3f, 0.5f, t); yield return null; }
            }
            e.light.intensity = e.idle;
            watchEyes.Add(e);
        }

        // ------------------------------------------------------------------ 9. The Watchers — loạt bắn đuổi của mắt tường
        // Bắt đầu từ mắt XA Ren nhất, lần lượt về phía Ren (người chơi đọc được hướng loạt bắn). Mỗi phát: dấu ngắm hình mắt bám chân Ren
        // rồi khoá (nhấp nháy), mắt nạp sáng + loé PixelLab, tia đâm xuống. Các phát cách nhau 0,55 s -> đứng yên là trúng, chạy là né.
        private const float WatchGap = 0.55f, WatchLock = 0.35f, WatchHitRadius = 0.34f;

        private IEnumerator Watchers()
        {
            nextWatch = Time.time + 8f;
            if (ai.Player == null) yield break;
            LoadEyeFx();
            var order = new List<WatchEye>(watchEyes);
            Vector2 p = ai.Player.position;
            order.Sort((x, y) => Mathf.Abs(y.at.x - p.x).CompareTo(Mathf.Abs(x.at.x - p.x)));
            Hints.Show("The wall-eyes fix on you, one after another - keep moving!", 2.2f);
            int running = 0;
            for (int i = 0; i < order.Count && !ai.Health.IsDead; i++)
            {
                running++;
                StartCoroutine(WatcherShot(order[i], i == 0 ? 0.9f : 0.6f, () => running--));
                yield return new WaitForSeconds(WatchGap);
            }
            for (float t = 0f; running > 0 && t < 4f && !ai.Health.IsDead; t += Time.deltaTime) yield return null;
            yield return ai.WaitFor(0.7f);                                             // nhịp thở sau loạt bắn
        }

        private IEnumerator WatcherShot(WatchEye e, float track, System.Action done)
        {
            var temp = new List<SpriteRenderer>();
            try
            {
                if (ai.Player == null) yield break;
                int layer = ai.Body != null ? ai.Body.sortingLayerID : 0;
                Vector2 at = e.at, mark = ai.Player.position;
                var crimson = new Color(1f, 0.16f, 0.24f);

                // dấu ngắm dưới chân Ren (ảnh PixelLab; thiếu -> vòng code đỏ thẫm)
                var ret = EtherVfx.Spawn("SZ Eye Reticle", eyeReticle != null ? eyeReticle : EtherVfx.Ring, mark,
                                         eyeReticle != null ? Color.white : crimson, layer, Order - 3);
                temp.Add(ret);
                float retK = 0.85f / Mathf.Max(0.01f, ret.sprite.bounds.size.x);
                var aim = Line(at, mark, 0.02f, new Color(crimson.r, crimson.g, crimson.b, 0.3f), Order + 3);
                temp.Add(aim);
                SpriteRenderer charge = null;
                float chargeK = 1f;
                if (eyeCharge != null)
                {
                    charge = EtherVfx.Spawn("SZ Eye Charge", eyeCharge[0], at, Color.white, layer, Order + 6);
                    temp.Add(charge);
                    chargeK = 0.75f / Mathf.Max(0.01f, eyeCharge[0].bounds.size.x);
                    charge.transform.localScale = Vector3.one * chargeK;
                }
                float total = track + WatchLock;
                for (float t = 0f; t < total && ai.Player != null && !ai.Health.IsDead; t += Time.deltaTime)
                {
                    bool locked = t >= track;
                    if (!locked) mark = Vector2.Lerp(mark, ai.Player.position, Time.deltaTime * 8f);
                    float s = retK * (locked ? 1f + 0.1f * Mathf.Sin(t * 45f) : Mathf.Lerp(1.4f, 1f, t / track));
                    ret.transform.position = mark;
                    ret.transform.localScale = new Vector3(s, s * 0.62f, 1f);
                    float blink = locked ? 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(t * 28f)) : 0.75f;
                    ret.color = eyeReticle != null ? new Color(1f, 1f, 1f, blink) : new Color(crimson.r, crimson.g, crimson.b, blink);
                    Vector2 d = mark - at;
                    Place(aim, at, d.normalized, d.magnitude, locked ? 0.035f : 0.018f);
                    aim.color = new Color(crimson.r, crimson.g, crimson.b, locked ? 0.35f + 0.55f * Mathf.Abs(Mathf.Sin(t * 30f)) : 0.28f);
                    if (charge != null) charge.sprite = eyeCharge[Mathf.Min(eyeCharge.Length - 1, (int)(t / total * eyeCharge.Length))];
                    if (e.light != null) e.light.intensity = e.idle + 3f * t / total;
                    yield return null;
                }
                foreach (var r in temp) if (r != null) Destroy(r.gameObject);
                temp.Clear();
                if (ai.Player == null || ai.Health.IsDead) yield break;

                // đâm: viền tối (đọc được trên nền đỏ) + thân tia + lõi trắng
                Vector2 dir = mark - at; float len = dir.magnitude;
                if (((Vector2)ai.Player.position - mark).magnitude < WatchHitRadius) ai.Hurt(ai.damage * 0.55f, mark, 1f);
                CameraFollow.KickMain(dir.normalized, 0.04f, 0.12f);
                StartCoroutine(Scorch(mark, layer));
                var outline = Line(at, mark, 0.3f, new Color(0.16f, 0f, 0.04f, 0.85f), Order + 3);
                temp.Add(outline);
                SpriteRenderer beam;
                float bw, bh;
                if (eyeBeam != null)
                {
                    beam = EtherVfx.Spawn("SZ Eye Beam", eyeBeam[0], at, Color.white, layer, Order + 4);
                    beam.drawMode = SpriteDrawMode.Tiled;                          // lặp đoạn tia PixelLab theo chiều dài (meta: Full Rect)
                    beam.tileMode = SpriteTileMode.Continuous;
                    bw = eyeBeam[0].bounds.size.x; bh = eyeBeam[0].bounds.size.y;
                }
                else
                {
                    beam = EtherVfx.Spawn("SZ Eye Beam", EtherVfx.Beam, at, crimson, layer, Order + 4);
                    bw = EtherVfx.BeamWidthUnits; bh = EtherVfx.BeamHeightUnits;
                }
                temp.Add(beam);
                beam.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                var core = Line(at, mark, 0.06f, Color.white, Order + 5);
                temp.Add(core);
                const float dur = 0.4f;
                for (float t = 0f; t < dur; t += Time.deltaTime)
                {
                    float k = 1f - t / dur;
                    float thick = 0.12f + 0.2f * k;
                    if (eyeBeam != null)
                    {
                        beam.sprite = eyeBeam[(int)(t * 16f) % eyeBeam.Length];
                        beam.size = new Vector2(len, bh);
                        beam.transform.localScale = new Vector3(1f, thick / bh, 1f);
                    }
                    else beam.transform.localScale = new Vector3(len / bw, thick / bh, 1f);
                    beam.color = eyeBeam != null ? new Color(1f, 1f, 1f, Mathf.Clamp01(k * 1.4f)) : new Color(crimson.r, crimson.g, crimson.b, k);
                    Place(outline, at, dir.normalized, len, thick + 0.1f);
                    outline.color = new Color(0.16f, 0f, 0.04f, 0.85f * k);
                    Place(core, at, dir.normalized, len, 0.02f + 0.05f * k);
                    core.color = new Color(1f, 1f, 1f, k);
                    if (e.light != null) e.light.intensity = e.idle + 4f * k;
                    yield return null;
                }
                if (e.light != null) e.light.intensity = e.idle;
            }
            finally
            {
                foreach (var r in temp) if (r != null) Destroy(r.gameObject);
                done?.Invoke();
            }
        }

        /// <summary>Chỗ tia đâm: nổ (PixelLab eyefx_burst, thiếu -> vòng nở) + vết cháy (eyefx_scorch, thiếu -> đốm tối) mờ dần 1,6 s.</summary>
        private IEnumerator Scorch(Vector2 at, int layer)
        {
            var mark = EtherVfx.Spawn("SZ Eye Scorch", eyeScorch != null ? eyeScorch : Blob, at,
                                      eyeScorch != null ? new Color(1f, 1f, 1f, 0.9f) : new Color(0.25f, 0.02f, 0.05f, 0.6f), layer, Order - 4);
            fx.Add(mark.gameObject);
            float mk = 0.7f / Mathf.Max(0.01f, mark.sprite.bounds.size.x);
            mark.transform.localScale = new Vector3(mk, mk * 0.6f, 1f);
            Color m0 = mark.color;
            SpriteRenderer burst;
            if (eyeBurst != null)
            {
                burst = EtherVfx.Spawn("SZ Eye Burst", eyeBurst[0], at, Color.white, layer, Order + 5);
                float bk = 1.1f / Mathf.Max(0.01f, eyeBurst[0].bounds.size.x);
                burst.transform.localScale = Vector3.one * bk;
            }
            else burst = EtherVfx.Spawn("SZ Eye Burst", EtherVfx.Ring, at, new Color(1f, 0.4f, 0.35f, 1f), layer, Order - 2);
            fx.Add(burst.gameObject);
            const float burstT = 0.4f;
            for (float t = 0f; t < 1.6f; t += Time.deltaTime)
            {
                if (burst != null)
                {
                    float k = Mathf.Clamp01(t / burstT);
                    if (eyeBurst != null) burst.sprite = eyeBurst[Mathf.Min(eyeBurst.Length - 1, (int)(k * eyeBurst.Length))];
                    else
                    {
                        burst.transform.localScale = new Vector3(1f, 0.6f, 1f) * Mathf.Lerp(0.2f, 0.75f, k);
                        burst.color = new Color(1f, 0.4f, 0.35f, 1f - k);
                    }
                    if (k >= 1f) { Destroy(burst.gameObject); burst = null; }
                }
                if (mark != null) mark.color = new Color(m0.r, m0.g, m0.b, m0.a * (1f - t / 1.6f));
                yield return null;
            }
            if (burst != null) Destroy(burst.gameObject);
            if (mark != null) Destroy(mark.gameObject);
        }

        /// <summary>Nạp ảnh VFX mắt tường (PixelLab) từ Resources/VFX/specimen/eyefx_*. Thiếu ảnh nào thì phần đó vẽ bằng code.</summary>
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

        // ------------------------------------------------------------------ tiện ích vẽ
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
