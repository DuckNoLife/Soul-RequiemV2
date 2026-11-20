using System.Collections;
using SuperTiled2Unity;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 03/11: cảnh THOÁT THÂN cuối Map 2 — con Mắt chết (cờ `when`, mặc định r8_cleared): hố vỡ trào Ether ngược lên (rung, loé xanh, bụi),
    /// camera lia từ hố sang ống thoát (`exit_at`) — "không khí thật" — rồi trả điều khiển: bóng thoại say1, gợi ý after_hint.
    /// Sau cảnh (và khi nạp lại bản lưu đã qua cảnh): hố vẫn phập phồng ánh xanh, đất thỉnh thoảng rung — giục người chơi rời đi.
    /// Dữ liệu Tiled: điểm "escape_surge" (lớp triggers, type cutscene_point) đặt ở tâm hố. Thuộc tính:
    ///   when, exit_at = "x,y" (ô), surge_text, exit_text, say1, after_hint, glow_color. Chỉ diễn một lần (cờ "escape_seen").
    /// </summary>
    public class EscapeSurge : MonoBehaviour
    {
        private const string Flag = "escape_seen";
        private bool done = true, skip;
        private Light2D glow;

        private IEnumerator Start()
        {
            yield return null;
            var dir = MapEncounterDirector.Instance;
            if (dir == null || dir.Flags == null || dir.mapRoot == null) yield break;
            SuperObject point = null;
            foreach (var so in dir.mapRoot.GetComponentsInChildren<SuperObject>(true))
                if (so.m_TiledName == "escape_surge") { point = so; break; }
            var props = point != null ? point.GetComponent<SuperCustomProperties>() : null;
            if (props == null) yield break;

            string when = props.GetString("when", "r8_cleared");
            Vector3 pit = point.transform.position;
            Vector3 exitAt = CutsceneFx.TileProp(dir, props, "exit_at", pit);
            Color col = ColorUtility.TryParseHtmlString(props.GetString("glow_color", "#8dff5a"), out var c) ? c : new Color(0.55f, 1f, 0.35f);

            while (!dir.Flags.HasAll(when)) yield return new WaitForSeconds(0.25f);
            var go = new GameObject("Escape Surge Glow");
            go.transform.position = pit;
            glow = go.AddComponent<Light2D>();
            glow.lightType = Light2D.LightType.Point;
            glow.pointLightOuterRadius = 5f; glow.pointLightInnerRadius = 0.4f;
            glow.color = col; glow.intensity = 0f;

            if (!dir.Flags.Has(Flag))
            {
                // đợi boss gục hẳn + bảng / gợi ý của trận tắt bớt
                yield return new WaitForSeconds(props.GetFloat("delay", 2.2f));
                while (CutsceneState.Active || TutorialPrompt.Active || SceneTransition.Busy || dir.Respawning) yield return new WaitForSeconds(0.2f);
                dir.Flags.Set(Flag);
                yield return Scene(dir, props, pit, exitAt);
            }

            // dư chấn: hố phập phồng, đất rung lâu lâu một lần cho tới khi rời map
            float nextQuake = Time.time + Random.Range(2.5f, 4.5f);
            while (glow != null)
            {
                glow.intensity = 1.1f + 0.45f * Mathf.Sin(Time.time * 2.4f);
                if (Time.time >= nextQuake && !CutsceneState.Active)
                {
                    nextQuake = Time.time + Random.Range(3f, 5.5f);
                    StartCoroutine(Quake(0.6f, 0.03f));
                    var psr = dir.Player != null ? dir.Player.GetComponentInChildren<SpriteRenderer>() : null;
                    if (psr != null) StartCoroutine(CutsceneFx.Puff(pit + new Vector3(Random.Range(-1.2f, 1.2f), Random.Range(-0.5f, 0.3f), 0f), psr.sortingLayerID, psr.sortingOrder + 1));
                }
                yield return null;
            }
        }

        private IEnumerator Scene(MapEncounterDirector dir, SuperCustomProperties props, Vector3 pit, Vector3 exitAt)
        {
            var player = dir.Player;
            if (player == null) yield break;
            var combat = player.GetComponent<PlayerCombat>();
            var psr = player.GetComponentInChildren<SpriteRenderer>();
            int layer = psr != null ? psr.sortingLayerID : 0, order = psr != null ? psr.sortingOrder : 2;
            done = false;
            CutsceneState.Begin();
            StartCoroutine(Hold());
            var ph = player.GetComponent<Health>();
            if (ph != null) ph.Invulnerable = true;

            var cam = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            float size = Camera.main != null ? Camera.main.orthographicSize : 3f;
            float oldSharp = cam != null ? cam.focusSharpness : 6f;
            if (cam != null) { cam.focusSharpness = 2.2f; cam.Focus(pit, size); }
            if (combat != null) combat.FacingOverride = (Vector2)(pit - player.position);

            // 1. hố trào Ether: rung mạnh dần, loé xanh từng nhịp, bụi đá bật quanh miệng hố
            Hints.Show(props.GetString("surge_text", "The shattered pit heaves - the Ether is welling back up!"), 3.5f);
            float nextKick = 0f, nextBurst = 0.3f;
            SoulRequiem.Core.Sfx.Play2D("quake_rumble"); // 19/11 (§337): âm: hố trào Ether
            SoulRequiem.Core.Sfx.Play2D("rock_fall");
            for (float t = 0f; t < 2.4f && !skip; t += Time.deltaTime)
            {
                float k = t / 2.4f;
                glow.intensity = Mathf.Lerp(0.5f, 2.6f, k) * (0.75f + 0.25f * Mathf.Sin(t * 14f));
                if (t >= nextKick) { nextKick = t + 0.08f; CameraFollow.KickMain(Random.insideUnitCircle, Mathf.Lerp(0.02f, 0.07f, k), 0.12f); }
                if (t >= nextBurst)
                {
                    nextBurst = t + 0.6f;
                    CutsceneFx.Burst(this, pit + new Vector3(Random.Range(-0.8f, 0.8f), Random.Range(-0.6f, 0.2f), 0f), 0.6f, 0.25f, 5, 3, layer, order);
                }
                yield return null;
            }
            skip = false;

            // 2. lia sang ống thoát — lối ra duy nhất
            if (cam != null) { cam.focusSharpness = 1.6f; cam.Focus(exitAt + new Vector3(0f, -0.3f, 0f), size); }
            Hints.Show(props.GetString("exit_text", "Above the pit, the great outflow gapes open. Cold air - real air - pours down through it."), 3.5f);
            for (float t = 0f; t < 2.6f && !skip; t += Time.deltaTime) { glow.intensity = 1.6f + 0.6f * Mathf.Sin(t * 6f); yield return null; }
            skip = false;

            // 3. trả camera về Ren
            if (cam != null) { cam.focusSharpness = 3f; cam.Release(); }
            for (float t = 0f; t < 0.9f && !skip; t += Time.deltaTime) yield return null;
            if (cam != null) cam.focusSharpness = oldSharp;
            if (combat != null) combat.FacingOverride = (Vector2)(exitAt - player.position);
            string line = props.GetString("say1", "");
            if (line.Length > 0)
            {
                skip = false;
                SpeechBubble.Show(player.position + new Vector3(0f, 0.75f, 0f), line, 2.4f).Follow(player);
                for (float t = 0f; t < 2.4f && !skip; t += Time.deltaTime) yield return null;
            }
            if (combat != null) combat.FacingOverride = Vector2.zero;
            if (ph != null) ph.Invulnerable = false;
            Hints.Show(props.GetString("after_hint", ""), 5f);
            done = true;
        }

        private static IEnumerator Quake(float seconds, float amp)
        {
            for (float t = 0f; t < seconds; t += 0.08f)
            {
                CameraFollow.KickMain(Random.insideUnitCircle, amp * (1f - t / seconds), 0.12f);
                yield return new WaitForSeconds(0.08f);
            }
        }

        /// <summary>Giữ khoá điều khiển suốt cảnh (menu Pause đóng sẽ nhả Blocked -> khoá lại mỗi khung); Space / Enter: tua nhanh.</summary>
        private IEnumerator Hold()
        {
            while (!done)
            {
                GameInput.Blocked = true;
                // 11/11 (§229): cảnh cốt truyện KHÔNG bỏ qua được (chỉ bảng giới thiệu boss mới bỏ qua được)
                yield return null;
            }
            GameInput.Blocked = false;
            CutsceneState.End();
        }
    }
}
