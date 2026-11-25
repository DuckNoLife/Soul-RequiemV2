using System.Collections;
using SuperTiled2Unity;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// First time Ren reaches the Cistern Gate (before diverting the Ichor flow): the camera pans to the cistern pit - the Eye sleeping inside
    /// its Ichor shield suddenly flares, the ground shakes - then pans back to Ren: speech bubble say1, hint after_hint (the only exit lies behind it).
    /// Plays only once (flag "eye_glimpsed"); skipped if flag `unless` is set (default ichor_diverted).
    /// Tiled data: point "eye_glimpse" (triggers layer, type cutscene_point) = trigger spot. Properties:
    ///   radius (world units), look = "x,y" (tiles) where the Eye is, say1, after_hint, unless, glow_color.
    /// Space / Enter: fast-forward.
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

            // wait for Ren to arrive (do not interrupt another scene / a map change / a respawn)
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

            // --- 1. pan to the pit ---------------------------------------------------------------------
            var cam = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            float oldSharp = cam != null ? cam.focusSharpness : 6f;
            float size = Camera.main != null ? Camera.main.orthographicSize : 3f;
            if (cam != null)
            {
                cam.focusSharpness = 1.6f;
                cam.Focus(look + new Vector3(0f, -0.25f, 0f), size * 1.15f);
            }
            yield return Wait(2.0f);

            // --- 2. the Eye stirs: glow flares, ground shakes ----------------------------------
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
                // fast flare (0.25 s) -> hold, flickering like a pulse -> fade out (last 0.9 s)
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

            // --- 3. pan back to Ren ------------------------------------------------------------------------
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

        /// <summary>Keep controls locked for the whole scene (closing the Pause menu releases Blocked -> lock again every frame); Space / Enter: fast-forward.</summary>
        private IEnumerator Hold()
        {
            while (!done)
            {
                GameInput.Blocked = true;
                // story scenes CANNOT be skipped (only the boss intro card can)
                yield return null;
            }
            GameInput.Blocked = false;
            CutsceneState.End();
        }
    }
}
