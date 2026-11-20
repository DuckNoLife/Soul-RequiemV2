using System.Collections;
using SuperTiled2Unity;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 03/11: lần đầu Ren tới Cistern Gate (trước khi chuyển dòng Ichor): camera lia sang hố của bể chứa — con Mắt đang ngủ trong
    /// lớp khiên Ichor chợt bừng sáng, mặt đất rung — rồi lia về Ren: bóng thoại say1, gợi ý after_hint (lối ra duy nhất nằm sau nó).
    /// Chỉ diễn một lần (cờ "eye_glimpsed"); không diễn nếu đã có cờ `unless` (mặc định ichor_diverted).
    /// Dữ liệu Tiled: điểm "eye_glimpse" (lớp triggers, type cutscene_point) = chỗ kích hoạt. Thuộc tính:
    ///   radius (đv thế giới), look = "x,y" (ô) chỗ con Mắt, say1, after_hint, unless, glow_color.
    /// Space / Enter: tua nhanh.
    /// </summary>
    public class EyeGlimpse : MonoBehaviour
    {
        private const string Flag = "eye_glimpsed";
        private bool done = true, skip;

        private IEnumerator Start()
        {
            yield return null;
            var dir = MapEncounterDirector.Instance;
            if (dir == null || dir.Flags == null || dir.mapRoot == null) yield break;
            SuperObject point = null;
            foreach (var so in dir.mapRoot.GetComponentsInChildren<SuperObject>(true))
                if (so.m_TiledName == "eye_glimpse") { point = so; break; }
            var props = point != null ? point.GetComponent<SuperCustomProperties>() : null;
            if (props == null) yield break;

            string unless = props.GetString("unless", "ichor_diverted");
            float radius = props.GetFloat("radius", 0.55f);
            Vector3 at = point.transform.position;
            Vector3 look = at;
            var xy = props.GetString("look", "").Split(',');
            if (xy.Length == 2 && float.TryParse(xy[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float lx)
                               && float.TryParse(xy[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float ly))
                look = dir.TileToWorld(lx, ly);
            Color glowCol = ColorUtility.TryParseHtmlString(props.GetString("glow_color", "#b8ff6a"), out var gc) ? gc : new Color(0.72f, 1f, 0.42f);

            // đợi Ren tới chỗ (không chen ngang cảnh khác / lúc đang chuyển map / hồi sinh)
            while (true)
            {
                if (dir.Flags.Has(Flag) || (unless.Length > 0 && dir.Flags.HasAll(unless))) yield break;
                var p = dir.Player;
                if (p != null && ((Vector2)(p.position - at)).magnitude < radius
                    && !CutsceneState.Active && !SceneTransition.Busy && !dir.Respawning && !TutorialPrompt.Active) break;
                yield return new WaitForSeconds(0.1f);
            }
            dir.Flags.Set(Flag);

            var player = dir.Player;
            var combat = player.GetComponent<PlayerCombat>();
            done = false;
            CutsceneState.Begin();
            StartCoroutine(Hold());
            if (combat != null) combat.FacingOverride = (Vector2)(look - player.position);

            // --- 1. lia sang hố ---------------------------------------------------------------------
            var cam = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            float oldSharp = cam != null ? cam.focusSharpness : 6f;
            float size = Camera.main != null ? Camera.main.orthographicSize : 3f;
            if (cam != null)
            {
                cam.focusSharpness = 1.6f;
                cam.Focus(look + new Vector3(0f, -0.25f, 0f), size * 1.15f);
            }
            yield return Wait(2.0f);

            // --- 2. con Mắt bừng tỉnh: quầng sáng loé lên, đất rung ----------------------------------
            var go = new GameObject("Eye Glimpse Glow");
            go.transform.position = look;
            var glow = go.AddComponent<Light2D>();
            glow.lightType = Light2D.LightType.Point;
            glow.pointLightOuterRadius = 2.6f;
            glow.pointLightInnerRadius = 0.25f;
            glow.color = glowCol;
            glow.intensity = 0f;
            const float wake = 2.4f;
            float nextKick = 0f;
            for (float t = 0f; t < wake && !skip; t += Time.deltaTime)
            {
                // loé nhanh (0.25 s) -> giữ, chập chờn như nhịp đập -> tắt dần (0.9 s cuối)
                float up = Mathf.Clamp01(t / 0.25f), down = Mathf.Clamp01((wake - t) / 0.9f);
                float beat = 1f + 0.25f * Mathf.Sin(t * 9f);
                glow.intensity = 2.4f * up * down * beat;
                if (t < 1.3f && t >= nextKick)
                {
                    nextKick = t + 0.09f;
                    CameraFollow.KickMain(Random.insideUnitCircle, Mathf.Lerp(0.04f, 0.012f, t / 1.3f), 0.14f);
                }
                yield return null;
            }
            Destroy(go);

            // --- 3. lia về Ren ------------------------------------------------------------------------
            if (cam != null) cam.Release();
            yield return Wait(1.0f);
            if (cam != null) cam.focusSharpness = oldSharp;
            string line = props.GetString("say1", "");
            if (line.Length > 0)
            {
                skip = false;
                SpeechBubble.Show(player.position + new Vector3(0f, 0.75f, 0f), line, 2.6f).Follow(player);
                yield return Wait(2.6f);
            }
            if (combat != null) combat.FacingOverride = Vector2.zero;
            Hints.Show(props.GetString("after_hint", ""), 5.5f);
            done = true;
        }

        private IEnumerator Wait(float s)
        {
            for (float t = 0f; t < s && !skip; t += Time.deltaTime) yield return null;
            skip = false;
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
