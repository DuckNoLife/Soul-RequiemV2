using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// ambush - ENCIRCLEMENT SCENE (e.g. the Black Lantern searching and then surrounding Ren at The Lychgate, Map 3).
    /// Ren walks into the object area (`when` met, no `unless` / `sets` yet, not in combat) ->
    ///   controls locked, camera pulls back; marching sound; each "actor" (sprite + Animator copied from the enemy prefab, NO AI) carrying a lantern
    ///   runs from its start spot to its encircling spot (staggered), turns to face Ren; the lines play (speech) -> flag `sets` turns on.
    ///   The fight area with `start_when` = that flag -> the area starts, REAL enemies spawn at the spawn points (placed on the encircling spots) -> the actors fade away.
    /// Properties:
    ///   actors = "Enemy@sx,sy>ex,ey;..." (Tiled tiles: start spot > encircling spot - the encircling spot should match an area spawn point)
    ///   line1..line8 = "a0: line" (a0 = actor 0) | "ren: line" ; speaker = name shown for the actors' lines (default "Black Lantern")
    ///   sets (flag set when done), when, unless, light_color (#ff8a4a: lanterns), run_speed (units/s, 2.2), zoom (1.25), march_sfx (ghost_march)
    /// </summary>
    public sealed class AmbushMechanism : Mechanism
    {
        private class Actor { public GameObject go; public SpriteRenderer sr; public Animator an; public Vector2 from, to; public Light2D lamp; public string state; }
        private bool playing;

        private bool Ready
        {
            get
            {
                string sets = Str("sets", "");
                if (sets.Length > 0 && Ctx.Flags.HasAll(sets)) return false;
                string unless = Str("unless", "");
                if (unless.Length > 0 && Ctx.Flags.HasAll(unless)) return false;
                return Ctx.Flags.HasAll(Str("when", ""));
            }
        }

        public override void OnFlagsChanged()
        {
            string sets = Str("sets", ""), unless = Str("unless", "");
            if ((sets.Length > 0 && Ctx.Flags.HasAll(sets)) || (unless.Length > 0 && Ctx.Flags.HasAll(unless))) Done = true;
        }

        public override void Tick(Vector2 playerPos, bool inside)
        {
            if (playing || !inside || !Ready || Ctx.Player == null) return;
            if (Ctx.Director.InCombat || Ctx.Director.Respawning || CutsceneState.Active || SceneTransition.Busy || StoryScenes.Busy || DialogueBox.Active) return;
            if (Ctx.PlayerHealth != null && Ctx.PlayerHealth.IsDead) return;
            playing = true;
            Ctx.Run(Scene());
        }

        private IEnumerator Scene()
        {
            var player = Ctx.Player;
            CutsceneState.Begin();
            GameInput.Blocked = true;
            Ctx.SetPlayerControl(false);
            var cam = Ctx.Camera;
            float size = Camera.main != null ? Camera.main.orthographicSize : 3f;
            Vector2 ren = player.position;
            if (cam != null) cam.Focus(ren + Vector2.up * 0.2f, size * Float("zoom", 1.25f));

            // actors
            var actors = new List<Actor>();
            Color lampC = MechanismContext.ParseColor(Str("light_color", "#ff8a4a"), 1f);
            foreach (var part in Str("actors", "").Split(';'))
            {
                var a = ParseActor(part.Trim());
                if (a == null) continue;
                a.lamp = a.go.AddComponent<Light2D>();
                a.lamp.lightType = Light2D.LightType.Point;
                a.lamp.color = lampC; a.lamp.intensity = 0f;
                a.lamp.pointLightOuterRadius = 1.7f; a.lamp.pointLightInnerRadius = 0.15f;
                actors.Add(a);
            }
            Sfx.Play(Str("march_sfx", "ghost_march"), ren, 0.9f);
            yield return new WaitForSeconds(0.5f);
            Sfx.Play("v_cultist_alert", actors.Count > 0 ? actors[0].from : ren, 0.9f);

            // run in to encircle (0.18 s stagger each)
            float speed = Mathf.Max(0.5f, Float("run_speed", 2.2f));
            float t0 = Time.time;
            bool all = false;
            while (!all)
            {
                all = true;
                for (int i = 0; i < actors.Count; i++)
                {
                    var a = actors[i];
                    if (a.go == null) continue;
                    float delay = i * 0.18f;
                    float e = Time.time - t0 - delay;
                    if (e < 0f) { all = false; continue; }
                    a.sr.color = new Color(1f, 1f, 1f, Mathf.Clamp01(e / 0.3f));
                    a.lamp.intensity = Mathf.Clamp01(e / 0.4f) * (0.85f + Mathf.Sin(Time.time * 9f + i) * 0.08f);
                    Vector2 p = a.go.transform.position;
                    if ((p - a.to).sqrMagnitude > 0.0004f)
                    {
                        all = false;
                        Play(a, "Move");
                        a.sr.flipX = a.to.x < p.x;
                        a.go.transform.position = Vector2.MoveTowards(p, a.to, speed * Time.deltaTime);
                    }
                    else
                    {
                        Play(a, "Idle");
                        a.sr.flipX = ren.x < a.to.x;
                    }
                }
                if (Time.time - t0 > 8f) break;                                  // stuck guard
                yield return null;
            }
            foreach (var a in actors) if (a.go != null) { a.go.transform.position = a.to; Play(a, "Idle"); a.sr.flipX = ren.x < a.to.x; }
            CameraFollow.KickMain(Vector2.down, 0.03f, 0.2f);

            // Ren looks around
            var combat = player.GetComponent<PlayerCombat>();
            if (combat != null && actors.Count > 0) combat.FacingOverride = ((Vector2)actors[0].to - ren).normalized;

            // dialogue
            string speaker = Str("speaker", "Black Lantern");
            for (int i = 1; i <= 8; i++)
            {
                string line = Str("line" + i, "");
                if (line.Length == 0) continue;
                int c = line.IndexOf(':');
                if (c < 0) continue;
                string who = line.Substring(0, c).Trim().ToLowerInvariant(), text = line.Substring(c + 1).Trim();
                float secs = Mathf.Clamp(1.2f + text.Length * 0.045f, 2f, 4.2f);
                if (who == "ren")
                {
                    SpeechBubble.Show(player.position + new Vector3(0f, 0.95f, 0f), text, secs).Follow(player);
                }
                else if (who.StartsWith("a") && int.TryParse(who.Substring(1), out int k) && k >= 0 && k < actors.Count && actors[k].go != null)
                {
                    var a = actors[k];
                    float top = a.sr.bounds.max.y + 0.15f;
                    SpeechBubble.Show(new Vector3(a.to.x, top, 0f), text, secs, new Color(1f, 0.78f, 0.55f), speaker);
                    if (combat != null) combat.FacingOverride = ((Vector2)a.to - ren).normalized;
                }
                yield return new WaitForSeconds(secs + 0.15f);
            }

            // return control, start the fight (area start_when = flag sets) -> real enemies spawn at the encircling spots -> actors fade
            if (combat != null) combat.FacingOverride = Vector2.zero;
            if (cam != null) cam.Release();
            GameInput.Blocked = false;
            Ctx.SetPlayerControl(true);
            CutsceneState.End();
            string sets = Str("sets", "");
            if (sets.Length > 0) Ctx.Flags.Set(sets);
            Done = true;
            float wait = 0f;
            while (wait < 1.5f && !Ctx.Director.InCombat) { wait += Time.deltaTime; yield return null; }
            yield return new WaitForSeconds(0.15f);
            for (float f = 0f; f < 0.25f; f += Time.deltaTime)
            {
                foreach (var a in actors) if (a.go != null) { a.sr.color = new Color(1f, 1f, 1f, 1f - f / 0.25f); a.lamp.intensity *= 0.85f; }
                yield return null;
            }
            foreach (var a in actors) if (a.go != null) Object.Destroy(a.go);
        }

        /// <summary>"Enemy@sx,sy>ex,ey" -> actor (hidden, standing at its start spot).</summary>
        private Actor ParseActor(string s)
        {
            int at = s.IndexOf('@'), gt = s.IndexOf('>');
            if (at <= 0 || gt <= at) return null;
            string enemy = s.Substring(0, at).Trim();
            if (!TryTile(s.Substring(at + 1, gt - at - 1), out var from) || !TryTile(s.Substring(gt + 1), out var to)) return null;
            var prefab = Ctx.Director.EnemyPrefab(enemy);
            if (prefab == null) { Debug.LogWarning("[Ambush] no enemy prefab '" + enemy + "'"); return null; }
            var srcSr = prefab.GetComponentInChildren<SpriteRenderer>(true);
            var srcAn = prefab.GetComponentInChildren<Animator>(true);
            if (srcSr == null) return null;
            var go = new GameObject("Ambush " + enemy);
            go.transform.position = from;
            go.transform.localScale = srcSr.transform.lossyScale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = srcSr.sprite; sr.sharedMaterial = srcSr.sharedMaterial;
            sr.sortingLayerID = srcSr.sortingLayerID; sr.sortingOrder = srcSr.sortingOrder; sr.spriteSortPoint = srcSr.spriteSortPoint;
            sr.color = new Color(1f, 1f, 1f, 0f);
            Animator an = null;
            if (srcAn != null && srcAn.runtimeAnimatorController != null)
            {
                an = go.AddComponent<Animator>();
                an.runtimeAnimatorController = srcAn.runtimeAnimatorController;
            }
            var a = new Actor { go = go, sr = sr, an = an, from = from, to = to };
            Play(a, "Idle");
            return a;
        }

        private bool TryTile(string s, out Vector2 world)
        {
            world = Vector2.zero;
            var p = s.Split(',');
            if (p.Length != 2 || !float.TryParse(p[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) ||
                !float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y)) return false;
            world = Ctx.Director.TileToWorld(x, y);
            return true;
        }

        private static void Play(Actor a, string state)
        {
            if (a.an == null || a.an.runtimeAnimatorController == null) return;
            string pick = a.an.HasState(0, Animator.StringToHash(state + "_S")) ? state + "_S" : a.an.HasState(0, Animator.StringToHash(state)) ? state : null;
            if (pick == null) return;
            if (a.state == pick) return;
            a.state = pick;
            a.an.Play(Animator.StringToHash(pick), 0, 0f);
        }
    }
}
