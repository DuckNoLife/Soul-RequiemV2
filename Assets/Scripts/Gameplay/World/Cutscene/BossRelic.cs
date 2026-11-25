using System.Collections;
using SuperTiled2Unity;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// RELIC DROPPED BY A BOSS (e.g. Plague Eye -> the Eye's core). The boss falls: the relic rises from the corpse, hovers, then flies straight into Ren's chest
    /// (camera follows, flash, Ether ring spreads) -> unlocks + auto-equips the skill / skill set `relic_skill`
    /// (no NEW MANTRA pause - only the relic_hint line). Skills can be swapped at rest points (the rest panel shows the set as one card).
    /// Tiled data - properties on the "boss" object (triggers layer, e.g. plague_eye_fight):
    ///   relic_skill (SkillDefinition id, required), relic_flag (default relic_&lt;skill&gt;), relic_color (#8fd0ff), relic_delay (1.4 s),
    ///   relic_text (line shown when the relic rises), relic_say (Ren's line after receiving it), relic_hint (hint at the end of the scene).
    ///   relic_sprite = relic image: "eye" (default, eye drawn in code) or a Resources path (e.g. VFX/specimen/relic_knot);
    ///   relic_beat (bool) = the relic beats like a heart (default true unless it is the eye); relic_size (float, units) = longest side of the image (default 0.6).
    ///   Available holy relics: VFX/relics/relic_eye_left, relic_eye_right, relic_ears, relic_arm_left, relic_heart; VFX/specimen/relic_spine.
    ///   Each boss drops the holy body part it carries: Plague Eye = the eye (default), Specimen Zero = VFX/specimen/relic_spine (relic_beat=false,
    ///   so it is not mistaken for a heart). Spare image VFX/specimen/relic_knot (root knot).
    /// Old save that already beat the boss (flag &lt;zone_id&gt;_cleared) but has no relic flag -> grant it at once, no scene.
    /// Relic flag set but the relic skill not owned -> unlocked quietly (not equipped).
    /// Space / Enter: fast-forward.
    /// </summary>
    public class BossRelic : MonoBehaviour
    {
        private MapEncounterDirector dir;
        private bool running, done = true, skip;
        private static Sprite eyeSprite;

        private void Awake()
        {
            dir = GetComponent<MapEncounterDirector>();
            if (dir != null) dir.BossSpawned += OnBossSpawned;
        }

        private void OnDestroy()
        {
            if (dir != null) dir.BossSpawned -= OnBossSpawned;
        }

        private static string FlagOf(SuperCustomProperties p) => p.GetString("relic_flag", "relic_" + p.GetString("relic_skill", ""));

        private IEnumerator Start()
        {
            yield return null;
            yield return null;
            if (dir == null || dir.Flags == null || dir.mapRoot == null) yield break;
            foreach (var so in dir.mapRoot.GetComponentsInChildren<SuperObject>(true))
            {
                if (so.m_Type != MapSchema.ObjectType.BossArena) continue;
                var p = so.GetComponent<SuperCustomProperties>();
                if (p == null || p.GetString("relic_skill", "").Length == 0) continue;
                string zone = p.GetString("zone_id", "");
                if (zone.Length == 0) continue;
                if (dir.Flags.Has(FlagOf(p))) { Restore(p); continue; }
                if (!dir.Flags.Has(zone + "_cleared")) continue;
                Grant(p, false);
            }
        }

        private void OnBossSpawned(EnemyAI boss, string hint)
        {
            var p = BossIntro.PropsFor(dir, boss);
            if (p == null || p.GetString("relic_skill", "").Length == 0 || dir.Flags == null || dir.Flags.Has(FlagOf(p))) return;
            boss.Health.Died += _ => { if (!running && isActiveAndEnabled) StartCoroutine(Run(boss, p)); };
        }

        /// <summary>Relic flag set but its skill is not in the loadout (e.g. the relic now grants a different skill set) -> unlock it quietly, not equipped.</summary>
        private void Restore(SuperCustomProperties p)
        {
            var lo = dir.Player != null ? dir.Player.GetComponent<PlayerLoadout>() : null;
            var s = PlayerLoadout.Find(p.GetString("relic_skill", ""));
            if (lo != null && s != null && !lo.Owns(s)) lo.Unlock(s, false);
        }

        /// <summary>Unlock the relic skill + set the flag. `fresh` = the scene just played, false = making up for an old save.</summary>
        private void Grant(SuperCustomProperties p, bool fresh)
        {
            var player = dir.Player;
            var lo = player != null ? player.GetComponent<PlayerLoadout>() : null;
            if (lo != null) lo.Unlock(p.GetString("relic_skill", ""));
            dir.Flags.Set(FlagOf(p));
            // NO NEW MANTRA tutorial pause after the relic scene (wrong moment right after a boss kill) -> new_mantra is not set
            if (!fresh) Hints.Show(p.GetString("relic_hint", "A relic has joined your mantras."), 5f);
        }

        private IEnumerator Run(EnemyAI boss, SuperCustomProperties p)
        {
            running = true;
            Vector3 from = boss.Body != null ? boss.Body.bounds.center : boss.transform.position;
            yield return new WaitForSeconds(p.GetFloat("relic_delay", 1.4f));         // boss death animation
            while (CutsceneState.Active || TutorialPrompt.Active || SceneTransition.Busy || dir.Respawning) yield return new WaitForSeconds(0.2f);
            var player = dir.Player;
            if (player == null || dir.Flags.Has(FlagOf(p))) { running = false; yield break; }

            Color col = ColorUtility.TryParseHtmlString(p.GetString("relic_color", "#8fd0ff"), out var c) ? c : EtherVfx.Blue;
            SoulRequiem.Core.Sfx.Play2D("relic_absorb"); // sound
            var combat = player.GetComponent<PlayerCombat>();
            var psr = player.GetComponentInChildren<SpriteRenderer>();
            int layer = psr != null ? psr.sortingLayerID : 0, order = psr != null ? psr.sortingOrder + 6 : 8;
            done = false; skip = false;
            CutsceneState.Begin();
            StartCoroutine(Hold());
            var cam = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            float size = Camera.main != null ? Camera.main.orthographicSize : 3f;
            float oldSharp = cam != null ? cam.focusSharpness : 6f;
            if (cam != null) { cam.focusSharpness = 2.4f; cam.Focus(from, size); }

            // relic: eye (drawn in code) + soft glow + light
            var relic = new GameObject("Boss Relic");
            relic.transform.position = from;
            var halo = EtherVfx.Spawn("halo", EtherVfx.Orb, from, new Color(col.r, col.g, col.b, 0f), layer, order - 1, relic.transform);
            Sprite look = RelicSprite(p, out bool beat);
            var eye = EtherVfx.Spawn("eye", look, from, new Color(1f, 1f, 1f, 0f), layer, order, relic.transform);
            float baseScale = eye.transform.localScale.x;
            float big = Mathf.Max(look.bounds.size.x, look.bounds.size.y);
            if (look != EyeSprite && big > 0.01f) baseScale *= p.GetFloat("relic_size", 0.6f) / big;   // longest side = relic_size units (default 0.6)
            eye.transform.localScale = Vector3.one * baseScale;
            var light = relic.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = col; light.pointLightOuterRadius = 2.2f; light.pointLightInnerRadius = 0.15f; light.intensity = 0f;
            if (combat != null) combat.FacingOverride = (Vector2)(from - player.position);

            // 1. rises from the corpse, light grows, ground shakes lightly
            // no extra hint - visuals / animation / boss title already tell the player (relic_text)
            Vector3 up = from + Vector3.up * 0.7f;
            for (float t = 0f; t < 1.6f && !skip; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / 1.6f);
                relic.transform.position = Vector3.Lerp(from, up, k) + Vector3.up * 0.04f * Mathf.Sin(t * 5f);
                eye.color = new Color(1f, 1f, 1f, k);
                if (beat) eye.transform.localScale = Vector3.one * baseScale * Beat(t);
                halo.color = new Color(col.r, col.g, col.b, 0.55f * k);
                halo.transform.localScale = Vector3.one * (1.4f + 0.2f * Mathf.Sin(t * 7f));
                light.intensity = 1.6f * k;
                if (Random.value < 0.15f) CameraFollow.KickMain(Random.insideUnitCircle, 0.015f, 0.1f);
                if (cam != null) cam.Focus(relic.transform.position, size);
                yield return null;
            }
            relic.transform.position = up;
            eye.color = Color.white;
            // hovers for a beat, pulsing light - then "looks" toward Ren
            for (float t = 0f; t < 0.8f && !skip; t += Time.deltaTime)
            {
                relic.transform.position = up + Vector3.up * 0.04f * Mathf.Sin(t * 5f);
                light.intensity = beat ? 1.4f + 1.2f * (Beat(t) - 1f) / 0.16f : 1.6f + 0.8f * Mathf.Abs(Mathf.Sin(t * 8f));
                if (beat) eye.transform.localScale = Vector3.one * baseScale * Beat(t);
                yield return null;
            }
            skip = false;

            // 2. flies into Ren's chest along an arc, speeding up; camera follows
            Vector3 start = relic.transform.position;
            float dur = Mathf.Clamp(((Vector2)(player.position - start)).magnitude * 0.22f, 0.8f, 1.8f);
            float nextTrail = 0f;
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                if (player == null) break;
                Vector3 chest = player.position + Vector3.up * 0.45f;
                float u = t / dur, k = u * u;                                  // slow then fast
                Vector3 pos = Vector3.Lerp(start, chest, k) + Vector3.up * (0.9f * 4f * u * (1f - u));
                relic.transform.position = pos;
                if (beat && eye != null) eye.transform.localScale = Vector3.one * baseScale * Beat(t);
                if (cam != null) cam.Focus(pos, size);
                if (combat != null) combat.FacingOverride = (Vector2)(pos - player.position);
                if (t >= nextTrail)
                {
                    nextTrail = t + 0.04f;
                    StartCoroutine(Trail(pos, col, layer, order - 2));
                }
                yield return null;
            }
            Destroy(relic);

            // 3. enters Ren: flash, 2 Ether rings spread, Ren glows blue then returns to normal
            Vector3 at = player.position + Vector3.up * 0.45f;
            CameraFollow.KickMain(Vector2.down, 0.09f, 0.35f);
            StartCoroutine(Wave(at, col, layer, order, 0f));
            StartCoroutine(Wave(at, col, layer, order, 0.18f));
            StartCoroutine(Flash(player, at, col, psr));
            SoulRequiem.Core.Sfx.Play2D("new_mantra"); // sound
            Grant(p, true);
            Hints.Show(p.GetString("relic_hint", "The relic sinks into you. New mantras stir in your blood."), 4.5f);
            if (cam != null) { cam.focusSharpness = 3f; cam.Release(); }
            for (float t = 0f; t < 1.1f && !skip; t += Time.deltaTime) yield return null;
            skip = false;
            if (cam != null) cam.focusSharpness = oldSharp;

            string line = p.GetString("relic_say", "");
            if (line.Length > 0)
            {
                SpeechBubble.Show(player.position + new Vector3(0f, 0.75f, 0f), line, 2.8f).Follow(player);
                for (float t = 0f; t < 2.8f && !skip; t += Time.deltaTime) yield return null;
            }
            if (combat != null) combat.FacingOverride = Vector2.zero;
            done = true;
            running = false;
        }

        private static IEnumerator Trail(Vector3 at, Color col, int layer, int order)
        {
            var s = EtherVfx.Spawn("Relic Trail", EtherVfx.Orb, at, new Color(col.r, col.g, col.b, 0.6f), layer, order);
            for (float t = 0f; t < 0.35f && s != null; t += Time.deltaTime)
            {
                float k = t / 0.35f;
                s.color = new Color(col.r, col.g, col.b, 0.6f * (1f - k));
                s.transform.localScale = Vector3.one * Mathf.Lerp(0.8f, 0.2f, k);
                yield return null;
            }
            if (s != null) Destroy(s.gameObject);
        }

        private static IEnumerator Wave(Vector3 at, Color col, int layer, int order, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            var s = EtherVfx.Spawn("Relic Wave", EtherVfx.Ring, at, col, layer, order);
            for (float t = 0f; t < 0.7f && s != null; t += Time.deltaTime)
            {
                float k = t / 0.7f;
                s.transform.localScale = new Vector3(1f, 0.6f, 1f) * Mathf.Lerp(0.3f, 3.2f, 1f - (1f - k) * (1f - k));
                s.color = new Color(col.r, col.g, col.b, 1f - k);
                yield return null;
            }
            if (s != null) Destroy(s.gameObject);
        }

        private static IEnumerator Flash(Transform player, Vector3 at, Color col, SpriteRenderer psr)
        {
            var go = new GameObject("Relic Flash");
            go.transform.position = at;
            var l = go.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point;
            l.color = col; l.pointLightOuterRadius = 3.5f; l.pointLightInnerRadius = 0.3f;
            Color c0 = psr != null ? psr.color : Color.white;
            for (float t = 0f; t < 1.2f; t += Time.deltaTime)
            {
                float k = 1f - t / 1.2f;
                l.intensity = 4f * k * k;
                if (player != null) go.transform.position = player.position + Vector3.up * 0.45f;
                if (psr != null) psr.color = Color.Lerp(new Color(c0.r, c0.g, c0.b, c0.a), new Color(col.r, col.g, col.b, c0.a), 0.75f * k);
                yield return null;
            }
            if (psr != null) psr.color = c0;
            Destroy(go);
        }

        /// <summary>Relic image from the relic_sprite property ("eye" = eye drawn in code, anything else = a Resources path).
        /// Image fails to load -> fall back to the eye and log it in the Console.</summary>
        private static Sprite RelicSprite(SuperCustomProperties p, out bool beat)
        {
            string path = p.GetString("relic_sprite", "eye");
            Sprite s = null;
            if (path.Length > 0 && path != "eye")
            {
                s = Resources.Load<Sprite>(path);
                if (s == null) Debug.LogWarning("[BossRelic] Could not load relic_sprite '" + path + "' -> using the eye.");
            }
            if (s == null) s = EyeSprite;
            beat = p.GetBool("relic_beat", s != EyeSprite);
            return s;
        }

        /// <summary>Double heartbeat (thump-thump, pause) ~ 75 bpm: returns a scale factor 1.0 .. 1.16.</summary>
        private static float Beat(float t)
        {
            float ph = Mathf.Repeat(t, 0.8f);
            float a = Mathf.Exp(-Mathf.Pow((ph - 0.08f) / 0.05f, 2f));
            float b = Mathf.Exp(-Mathf.Pow((ph - 0.26f) / 0.05f, 2f)) * 0.6f;
            return 1f + 0.16f * Mathf.Max(a, b);
        }

        /// <summary>22x14 px eye: pale blue white, dark outline, Ether blue iris, vertical pupil, highlight.</summary>
        private static Sprite EyeSprite
        {
            get
            {
                if (eyeSprite != null) return eyeSprite;
                const int W = 22, H = 14;
                var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[W * H];
                float cx = (W - 1) / 2f, cy = (H - 1) / 2f;
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        float ex = (x - cx) / (W / 2f), ey = (y - cy) / (H / 2f);
                        // lemon shape (two arcs): |y| <= (1 - x^2)
                        float lid = 1f - ex * ex;
                        Color32 c = new Color32(0, 0, 0, 0);
                        if (Mathf.Abs(ey) <= lid)
                        {
                            bool rim = Mathf.Abs(ey) > lid - 0.22f || Mathf.Abs(ex) > 0.9f;
                            float r = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                            if (rim) c = new Color32(0x1d, 0x3d, 0x66, 255);
                            else if (Mathf.Abs(x - cx) <= 0.8f && Mathf.Abs(y - cy) <= 3.2f) c = new Color32(0x0b, 0x18, 0x30, 255);   // vertical pupil
                            else if (r <= 4.6f) c = r > 3.6f ? new Color32(0x2a, 0x6f, 0xc0, 255) : new Color32(0x6c, 0xc4, 0xff, 255);
                            else c = new Color32(0xdf, 0xf4, 0xff, 255);
                        }
                        px[y * W + x] = c;
                    }
                px[(H / 2 + 2) * W + (W / 2 - 3)] = new Color32(255, 255, 255, 255);                                            // highlight
                tex.SetPixels32(px);
                tex.Apply();
                eyeSprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 60f);
                return eyeSprite;
            }
        }

        /// <summary>Keep controls locked for the whole scene; Space / Enter: fast-forward.</summary>
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
