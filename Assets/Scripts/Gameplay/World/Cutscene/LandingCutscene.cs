using System.Collections;
using System.Collections.Generic;
using SuperTiled2Unity;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 03/11: cảnh TIẾP ĐẤT đầu Map 2 (nối tiếp cảnh sập hầm cuối Map 1). Chỉ diễn một lần (cờ "landed").
    ///   Phòng hầm trống, cột sáng rọi xuống -> Ren rơi từ trên miệng hầm xuống (sỏi rơi theo) -> chạm đất: bụi + giật camera ->
    ///   Ren ngẩng lên, trần rung, sỏi lộp độp -> Ren lăn né xuống phía nam -> đống đất đá sụp xuống đúng chỗ vừa đứng, bịt miệng hầm
    ///   (cột sáng + đèn mờ hẳn) -> Ren nhìn đống đá, bóng thoại say1 / say2 -> gợi ý after_hint, trả điều khiển.
    /// Dữ liệu Tiled: điểm "landing_point" (lớp triggers, type cutscene_point) = tâm đống đá. Thuộc tính:
    ///   say1, say2, after_hint, beam (tên tile object cột sáng), light (tên đèn, Light2D "Light &lt;tên&gt;"),
    ///   mute_sign (lời dẫn tự hiện bị thay bằng cảnh này), beam_dim / light_dim (độ sáng còn lại sau khi bịt, 0..1).
    /// Hình: Resources/VFX/landing/landing_rubble, landing_pebble_1..8, landing_dust_0..5.
    /// Vào lại map khi đã có cờ (Continue): đặt sẵn đống đá + cột sáng mờ, không diễn lại.
    /// </summary>
    public class LandingCutscene : MonoBehaviour
    {
        private const string Flag = "landed";
        // 03/11 (chạy thử): đá bộ Map 1 dưới cột sáng xanh của hầm trông vàng chói, lạc tông -> nhuộm tối, ngả xám lạnh
        private static readonly Color RubbleTint = new Color(0.6f, 0.64f, 0.62f), PebbleTint = new Color(0.68f, 0.7f, 0.68f);

        private class Bit { public SpriteRenderer sr; public Vector3 from, to; public float t0, dur, arc, landedAt = -1f; }
        private class Puff { public SpriteRenderer sr; public float t0, fps; }

        private readonly List<Bit> bits = new List<Bit>();
        private readonly List<Puff> puffs = new List<Puff>();
        private readonly List<Sprite> pebbles = new List<Sprite>();
        private Sprite[] dust;
        private SpriteRenderer psr;
        private int layer, order = 2;
        private bool done, letGo, skip;

        private void Awake()
        {
            // ẩn Ren ngay từ khung đầu (đang rơi trên miệng hầm); Start hiện lại nếu cảnh đã diễn
            var dir = MapEncounterDirector.Instance;
            if (dir == null || dir.Player == null) return;
            psr = dir.Player.GetComponentInChildren<SpriteRenderer>();
            if (psr != null) { psr.enabled = false; layer = psr.sortingLayerID; order = psr.sortingOrder; }
            if (dir.mapRoot != null)
                foreach (var so in dir.mapRoot.GetComponentsInChildren<SuperObject>(true))
                    if (so.m_TiledName == "landing_point") { point = so; break; }
            props = point != null ? point.GetComponent<SuperCustomProperties>() : null;
            string mute = props != null ? props.GetString("mute_sign", "") : "";
            if (dir.Flags != null && mute.Length > 0) dir.Flags.Set("seen_" + mute);   // trước khi Ren chạm vùng lời dẫn (va chạm vật lý đầu tiên)
        }

        private SuperObject point;
        private SuperCustomProperties props;

        private IEnumerator Start()
        {
            var dir = MapEncounterDirector.Instance;
            yield return null;                                         // đợi cờ bản lưu (Continue) nạp xong
            if (dir == null || dir.Player == null || dir.Flags == null || props == null) { ShowRen(); yield break; }

            Vector3 rubbleAt = point.transform.position;
            float beamDim = props.GetFloat("beam_dim", 0.3f), lightDim = props.GetFloat("light_dim", 0.35f);
            var beam = FindBeam(dir, props.GetString("beam", ""));
            var light = FindLight(props.GetString("light", ""));

            if (dir.Flags.Has(Flag))
            {
                ShowRen();
                PlaceRubble(rubbleAt);
                SetBeam(beam, beamDim);
                SetLight(light, lightDim, 1f);
                yield break;
            }

            dust = new Sprite[6];
            for (int i = 0; i < 6; i++) dust[i] = Resources.Load<Sprite>("VFX/landing/landing_dust_" + i);
            if (System.Array.Exists(dust, s => s == null)) dust = null;
            for (int i = 1; i <= 8; i++) { var s = Resources.Load<Sprite>("VFX/landing/landing_pebble_" + i); if (s != null) pebbles.Add(s); }

            var player = dir.Player;
            var combat = player.GetComponent<PlayerCombat>();
            CutsceneState.Begin();
            StartCoroutine(Hold());

            // phòng hầm trống trong giây lát (đợi màn đen chuyển map tan hẳn)
            bool fromTransition = SceneTransition.Busy;
            for (float w = 0f; SceneTransition.Busy && w < 5f; w += Time.deltaTime) yield return null;
            yield return new WaitForSeconds(fromTransition ? 0.5f : 1.2f);

            // --- 1. rơi xuống ---------------------------------------------------------------
            Vector3 feet = psr != null ? psr.transform.position : player.position;
            const float drop = 2.6f, fallT = 0.45f;
            SpriteRenderer ghost = null;
            if (psr != null)
            {
                ghost = new GameObject("Landing Fall").AddComponent<SpriteRenderer>();
                ghost.sharedMaterial = psr.sharedMaterial;
                ghost.sortingLayerID = layer; ghost.sortingOrder = order + 5;
            }
            for (int i = 0; i < 4; i++)
                Drop(feet + new Vector3(Random.Range(-0.45f, 0.45f), Random.Range(-0.25f, 0.2f), 0f), drop + Random.Range(0.2f, 0.9f), Random.Range(0.4f, 0.6f), Random.Range(0f, 0.15f));
            for (float t = 0f; t < fallT; t += Time.deltaTime)
            {
                float u = t / fallT;
                if (ghost != null)
                {
                    ghost.sprite = psr.sprite; ghost.flipX = psr.flipX;      // Animator vẫn chạy khi SpriteRenderer tắt
                    ghost.transform.position = Snap(feet + Vector3.up * (drop * (1f - u * u)));
                }
                yield return null;
            }
            if (ghost != null) Destroy(ghost.gameObject);
            ShowRen();
            CameraFollow.KickMain(Vector2.down, 0.12f, 0.3f);
            SoulRequiem.Core.Sfx.Play2D("rock_crash"); // 19/11 (§337): âm
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2f + Random.Range(-0.3f, 0.3f);
                Dust(feet + new Vector3(Mathf.Cos(a) * 0.3f, Mathf.Sin(a) * 0.1f - 0.02f, 0f), Random.Range(12f, 16f));
            }
            yield return new WaitForSeconds(0.75f);

            // --- 2. ngẩng lên: trần rung, sỏi rơi ---------------------------------------------------
            if (combat != null) combat.FacingOverride = Vector2.up;
            const float rumble = 1.1f;
            float nextKick = 0f, nextPebble = 0f;
            for (float t = 0f; t < rumble; t += Time.deltaTime)
            {
                float k = t / rumble;
                if (t >= nextKick) { nextKick = t + 0.08f; CameraFollow.KickMain(Random.insideUnitCircle, Mathf.Lerp(0.01f, 0.045f, k), 0.12f); }
                if (t >= nextPebble)
                {
                    nextPebble = t + Mathf.Lerp(0.16f, 0.06f, k);
                    Vector2 r = Random.insideUnitCircle;
                    Drop(rubbleAt + new Vector3(r.x * 0.6f, r.y * 0.3f, 0f), Random.Range(1.4f, 2.2f), Random.Range(0.3f, 0.42f), 0f);
                }
                yield return null;
            }

            // --- 3. lăn né xuống phía nam (thả khoá một chút cho PlayerCombat nhận lệnh lăn) ---------------------
            if (combat != null)
            {
                combat.FacingOverride = Vector2.zero;
                letGo = true;
                GameInput.Blocked = false;
                combat.QueueAction(GameAction.Roll, Vector2.down);
                yield return new WaitForSeconds(0.12f);
                letGo = false;
            }
            yield return new WaitForSeconds(0.08f);

            // --- 4. đống đất đá sụp xuống, bịt miệng hầm -----------------------------------------------
            var rub = PlaceRubble(rubbleAt);
            Vector3 rubTo = rub != null ? rub.transform.position : rubbleAt;
            if (rub != null)
            {
                rub.sortingOrder = order + 4;
                var col = rub.GetComponent<Collider2D>();
                if (col != null) col.enabled = false;
                const float rubT = 0.32f, rubH = 2.4f;
                for (float t = 0f; t < rubT; t += Time.deltaTime)
                {
                    float u = t / rubT;
                    rub.transform.position = Snap(rubTo + Vector3.up * (rubH * (1f - u * u)));
                    yield return null;
                }
                rub.transform.position = rubTo;
                rub.sortingOrder = order;
                if (col != null) col.enabled = true;
            }
            CameraFollow.KickMain(Vector2.down, 0.17f, 0.45f);
            SoulRequiem.Core.Sfx.Play2D("rock_crash"); // 19/11 (§337): âm
            SoulRequiem.Core.Sfx.Play2D("rock_fall");
            for (int i = 0; i < 12; i++)
            {
                float a = i / 12f * Mathf.PI * 2f + Random.Range(-0.2f, 0.2f);
                Dust(rubTo + new Vector3(Mathf.Cos(a) * 0.5f, Mathf.Sin(a) * 0.2f - 0.08f, 0f), Random.Range(10f, 15f), Random.Range(0f, 0.12f));
            }
            for (int i = 0; i < 7; i++)                                 // đá văng ra quanh đống
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                var d = new Vector3(Mathf.Cos(a) * Random.Range(0.45f, 0.8f), Mathf.Sin(a) * Random.Range(0.2f, 0.4f) - 0.1f, 0f);
                Scatter(rubTo + new Vector3(0f, 0.05f, 0f), rubTo + d, Random.Range(0.25f, 0.4f));
            }
            for (float t = 0f; t < 1f; t += Time.deltaTime)             // ánh sáng trên miệng hầm tắt dần
            {
                SetBeam(beam, Mathf.Lerp(1f, beamDim, t));
                SetLight(light, lightDim, t);
                yield return null;
            }
            SetBeam(beam, beamDim);
            SetLight(light, lightDim, 1f);

            // --- 5. Ren nhìn đống đá -----------------------------------------------------------------------
            if (combat != null) combat.FacingOverride = (Vector2)(rubTo - player.position);
            yield return new WaitForSeconds(0.3f);
            yield return Say(player, props.GetString("say1", ""));
            yield return Say(player, props.GetString("say2", ""));
            if (combat != null) combat.FacingOverride = Vector2.zero;

            dir.Flags.Set(Flag);
            dir.SetRespawnPoint(player.position);                      // gục trước khi thắp điểm nghỉ: không hồi sinh trong đống đá
            Hints.Show(props.GetString("after_hint", ""), 5f);
            done = true;
        }

        private IEnumerator Say(Transform player, string line)
        {
            if (string.IsNullOrEmpty(line)) yield break;
            skip = false;
            SpeechBubble.Show(player.position + new Vector3(0f, 0.75f, 0f), line, 2.4f).Follow(player);
            for (float t = 0f; t < 2.4f && !skip; t += Time.deltaTime) yield return null;
        }

        /// <summary>Giữ khoá điều khiển suốt cảnh (menu Pause đóng sẽ nhả Blocked -> khoá lại mỗi khung); Space / Enter: qua câu thoại.</summary>
        private IEnumerator Hold()
        {
            while (!done)
            {
                if (!letGo) GameInput.Blocked = true;
                // 11/11 (§229): cảnh cốt truyện KHÔNG bỏ qua được (chỉ bảng giới thiệu boss mới bỏ qua được)
                yield return null;
            }
            GameInput.Blocked = false;
            CutsceneState.End();
        }

        private void OnDestroy()
        {
            if (!done && psr != null) psr.enabled = true;
        }

        // ------------------------------------------------------------------ hình
        private void ShowRen() { if (psr != null) psr.enabled = true; }

        private static Vector3 Snap(Vector3 p) => new Vector3(Mathf.Round(p.x * 100f) / 100f, Mathf.Round(p.y * 100f) / 100f, p.z);

        private SpriteRenderer PlaceRubble(Vector3 at)
        {
            var s = Resources.Load<Sprite>("VFX/landing/landing_rubble");
            if (s == null) return null;
            var go = new GameObject("Landing Rubble");
            go.transform.position = Snap(at);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s;
            sr.color = RubbleTint;
            if (psr != null) sr.sharedMaterial = psr.sharedMaterial;
            sr.sortingLayerID = layer; sr.sortingOrder = order;       // cùng lớp nhân vật: đứng phía bắc đống đá thì bị che
            var col = go.AddComponent<CapsuleCollider2D>();             // chân đống đá chặn đường (không có Rigidbody -> tĩnh)
            col.direction = CapsuleDirection2D.Horizontal;             // §303: đống đá tròn -> capsule ngang, trượt quanh không kẹt góc
            col.size = new Vector2(0.7f, 0.2f);
            col.offset = new Vector2(0f, -0.08f);
            return sr;
        }

        private static List<(SpriteRenderer sr, Color c)> FindBeam(MapEncounterDirector dir, string name)
        {
            var list = new List<(SpriteRenderer, Color)>();
            if (string.IsNullOrEmpty(name)) return list;
            foreach (var so in dir.mapRoot.GetComponentsInChildren<SuperObject>(true))
                if (so.m_TiledName == name)
                    foreach (var sr in so.GetComponentsInChildren<SpriteRenderer>(true)) list.Add((sr, sr.color));
            return list;
        }

        private static void SetBeam(List<(SpriteRenderer sr, Color c)> beam, float k)
        {
            foreach (var b in beam) if (b.sr != null) b.sr.color = new Color(b.c.r, b.c.g, b.c.b, b.c.a * k);
        }

        private class LightRef { public Light2D l; public LightAnimator la; public float i0; }

        private static LightRef FindLight(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (var l in FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (l.name == "Light " + name)
                {
                    var la = l.GetComponent<LightAnimator>();
                    return new LightRef { l = l, la = la, i0 = la != null && la.baseIntensity > 0f ? la.baseIntensity : l.intensity };
                }
            return null;
        }

        private static void SetLight(LightRef r, float dim, float t)
        {
            if (r == null || r.l == null) return;
            float i = r.i0 * Mathf.Lerp(1f, dim, Mathf.Clamp01(t));
            if (r.la != null) r.la.baseIntensity = i; else r.l.intensity = i;
        }

        // ------------------------------------------------------------------ sỏi & bụi
        private void Drop(Vector3 to, float height, float dur, float delay)
        {
            if (pebbles.Count == 0) return;
            var sr = NewBit(order + 3);
            bits.Add(new Bit { sr = sr, to = to, from = to + new Vector3(Random.Range(-0.08f, 0.08f), height, 0f), t0 = Time.time + delay, dur = dur });
            sr.transform.position = Snap(to + Vector3.up * height);
            sr.enabled = delay <= 0f;
        }

        private void Scatter(Vector3 from, Vector3 to, float dur)
        {
            if (pebbles.Count == 0) return;
            var sr = NewBit(order + 3);
            bits.Add(new Bit { sr = sr, from = from, to = to, t0 = Time.time, dur = dur, arc = Random.Range(0.15f, 0.3f) });
            sr.transform.position = Snap(from);
        }

        private SpriteRenderer NewBit(int sortOrder)
        {
            var sr = new GameObject("Landing Pebble").AddComponent<SpriteRenderer>();
            SoulRequiem.Core.Sfx.Play2D("rock_fall", 0.6f); // 19/11 (§337): âm
            sr.sprite = pebbles[Random.Range(0, pebbles.Count)];
            sr.flipX = Random.value < 0.5f;
            sr.color = PebbleTint;
            if (psr != null) sr.sharedMaterial = psr.sharedMaterial;
            sr.sortingLayerID = layer; sr.sortingOrder = sortOrder;
            return sr;
        }

        private void Dust(Vector3 p, float fps, float delay = 0f)
        {
            if (dust == null) return;
            var sr = new GameObject("Landing Dust").AddComponent<SpriteRenderer>();
            sr.transform.position = Snap(p);
            sr.sprite = dust[0]; sr.flipX = Random.value < 0.5f;
            sr.sortingLayerID = layer; sr.sortingOrder = order + 1;
            sr.enabled = delay <= 0f;
            puffs.Add(new Puff { sr = sr, t0 = Time.time + delay, fps = fps });
        }

        private void Update()
        {
            float now = Time.time;
            for (int i = bits.Count - 1; i >= 0; i--)
            {
                var b = bits[i];
                if (b.sr == null) { bits.RemoveAt(i); continue; }
                float u = (now - b.t0) / b.dur;
                if (u < 0f) continue;
                b.sr.enabled = true;
                if (u < 1f)
                {
                    if (b.arc > 0f)   // văng ra: bay vòng cung trên mặt đất
                        b.sr.transform.position = Snap(Vector3.Lerp(b.from, b.to, u) + Vector3.up * (b.arc * 4f * u * (1f - u)));
                    else              // rơi thẳng, nhanh dần
                        b.sr.transform.position = Snap(Vector3.LerpUnclamped(b.from, b.to, u * u));
                    continue;
                }
                if (b.landedAt < 0f)
                {
                    b.landedAt = now;
                    b.sr.transform.position = Snap(b.to);
                    b.sr.sortingOrder = order - 2;                       // nằm trên mặt đất
                    if (b.arc <= 0f) Dust(b.to, 18f);
                }
                float fade = Mathf.Clamp01((now - b.landedAt - 1.2f) / 0.6f);
                b.sr.color = new Color(PebbleTint.r, PebbleTint.g, PebbleTint.b, 1f - fade);
                if (fade >= 1f) { Destroy(b.sr.gameObject); bits.RemoveAt(i); }
            }
            if (dust == null) return;
            for (int i = puffs.Count - 1; i >= 0; i--)
            {
                var p = puffs[i];
                if (p.sr == null) { puffs.RemoveAt(i); continue; }
                float t = now - p.t0;
                if (t < 0f) continue;
                p.sr.enabled = true;
                int f = (int)(t * p.fps);
                if (f >= dust.Length) { Destroy(p.sr.gameObject); puffs.RemoveAt(i); continue; }
                p.sr.sprite = dust[f];
            }
        }
    }
}
