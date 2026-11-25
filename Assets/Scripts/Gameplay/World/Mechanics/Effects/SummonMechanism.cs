using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// summon - "pulled / dragged / thrown away" scene (e.g. after ringing the West Bell, Isolde throws Ren into the Bower).
    /// When flags `when` are set and flag `sets` is NOT (the scene only plays once, also after loading a save):
    ///   wait `delay` seconds -> controls locked, Ren invulnerable, `color` glow + ground shake -> dialogue `speaker` (line1..line8, auto-advance
    ///   after `auto` seconds) -> flag `sets` turns on -> Ren goes to `target` ("x,y" Tiled tile):
    ///   * with `actor` (enemy name, e.g. Matriarch_Isolde): an "actor" (sprite + Animator only, no brain) appears from mist right BEHIND Ren
    ///     (`actor_behind` units north), Ren turns to look, the actor casts (`actor_anim_appear`, default Cast) then swings
    ///     (`actor_anim_throw`, default Attack) - Ren flies in an arc (`throw_time`, `throw_arc`) over everything to `target` and lands
    ///     (shake + dust + stagger); the actor dissolves into mist. `throw_line`: line spoken (speech) at the throw.
    ///   * without `actor`: black screen teleport (`warp_text`, `shake`).
    /// The destination area (arena with a close_on_enter gate) starts by itself once Ren stands inside its box -> gate locks, boss appears (BossIntro).
    /// </summary>
    public sealed class SummonMechanism : Mechanism
    {
        private bool running;

        /// <summary>Flag `sets` cleared -> the scene can play again (falling mid-fight against Isolde: ring the bell again -> pulled down again).</summary>
        public override void OnFlagsReset()
        {
            string sets = Str("sets", "");
            if (Done && !running && sets.Length > 0 && !Ctx.Flags.HasAll(sets)) Done = false;
        }

        public override void OnFlagsChanged()
        {
            if (Done || running) return;
            string unless = Str("unless", "");                         // e.g. Isolde already defeated (walked into the Bower) -> ringing the bell no longer pulls Ren down
            if (unless.Length > 0 && Ctx.Flags.HasAll(unless)) { Done = true; return; }
            string sets = Str("sets", "");
            if (sets.Length > 0 && Ctx.Flags.HasAll(sets)) { Done = true; return; }
            if (!Ctx.Flags.HasAll(Str("when", "-"))) return;
            running = true;
            Ctx.Run(Play(sets));
        }

        private void SetFlags(string sets)
        {
            if (sets.Length > 0) foreach (var f in sets.Split(',')) if (f.Trim().Length > 0) Ctx.Flags.Set(f.Trim());
        }

        private IEnumerator Play(string sets)
        {
            float wait = Float("delay", 1.6f);
            for (float t = 0f; t < wait; t += Time.deltaTime) yield return null;
            while (SceneTransition.Busy || DialogueBox.Active) yield return null;
            var player = Ctx.Player;
            if (player == null) { running = false; yield break; }

            CutsceneState.Begin();
            GameInput.Blocked = true;
            Ctx.SetPlayerControl(false);
            var hp = Ctx.PlayerHealth;
            if (hp != null) hp.Invulnerable = true;
            var psr = player.GetComponentInChildren<SpriteRenderer>();
            int layer = psr != null ? psr.sortingLayerID : 0, order = psr != null ? psr.sortingOrder : 2;

            // glow around Ren + ground shake while talking
            var glowGo = new GameObject("Summon Glow");
            glowGo.transform.position = player.position;
            var glow = glowGo.AddComponent<Light2D>();
            glow.lightType = Light2D.LightType.Point;
            glow.color = MechanismContext.ParseColor(Str("color", "#ff5a8a"), 1f);
            glow.pointLightOuterRadius = 2.6f; glow.pointLightInnerRadius = 0.2f; glow.intensity = 0f;

            var lines = new List<string>();
            for (int i = 1; i <= 8; i++)
            {
                string l = Str("line" + i, "");
                if (string.IsNullOrEmpty(l)) break;
                lines.Add(l);
            }
            CameraFollow.KickMain(Vector2.down, 0.08f, 0.35f);
            Sfx.Play2D("summon_rift", 0.8f);                                       // sound
            if (lines.Count > 0) DialogueBox.Open(Str("speaker", ""), lines.ToArray(), null, Float("auto", 2.6f));
            float nextKick = 0f, tt = 0f;
            while (DialogueBox.Active)
            {
                GameInput.Blocked = true;                                // closing the Pause menu releases Blocked -> lock again
                tt += Time.deltaTime;
                glowGo.transform.position = player.position;
                glow.intensity = 0.9f + 0.35f * Mathf.Sin(tt * 3.2f);
                if (tt >= nextKick) { nextKick = tt + Random.Range(0.6f, 1.2f); CameraFollow.KickMain(Random.insideUnitCircle.normalized, 0.025f, 0.18f); }
                yield return null;
            }

            Vector3 to = Ctx.TilePoint(Str("target", ""), player.position);
            var actor = MakeActor(Str("actor", ""), player.position + Vector3.up * Float("actor_behind", 0.55f));
            if (actor != null)
            {
                yield return Throw(actor, player, to, glow, layer, order, sets);
                CutsceneState.End();
                GameInput.Blocked = false;
            }
            else
            {
                // pulled away: glow rises, then a black screen teleport
                for (float t = 0f; t < 0.7f; t += Time.deltaTime)
                {
                    glow.intensity = Mathf.Lerp(1f, 3.2f, t / 0.7f);
                    glow.pointLightOuterRadius = Mathf.Lerp(2.6f, 1.2f, t / 0.7f);
                    yield return null;
                }
                SetFlags(sets);
                CutsceneState.End();
                GameInput.Blocked = false;
                SceneTransition.Warp(player, to, Str("warp_text", ""), Float("shake", 0.08f));
                yield return null;
                while (SceneTransition.Busy) yield return null;
            }
            if (glowGo != null) Object.Destroy(glowGo);
            if (hp != null) hp.Invulnerable = false;
            Ctx.SetPlayerControl(true);
            Done = true;
            running = false;
        }

        /// play PixelLab effect Resources/VFX/&lt;name in property key&gt; (`key_scale`, `key_fps` override). Returns true if something played.
        private bool Vfx(string key, Vector3 at, float scale, int layer, int order)
        {
            string n = Str(key, "");
            if (n.Length == 0 || n == "none") return false;
            return SpriteVfx.Spawn(n, at, Float(key + "_scale", scale), layer, order, false, Float(key + "_fps", 14f)) != null;
        }

        // ---- actor: only a SpriteRenderer + Animator copied from the enemy prefab (no EnemyAI / Health - cannot attack, cannot be hit)
        private GameObject MakeActor(string enemy, Vector3 at)
        {
            if (string.IsNullOrEmpty(enemy) || Ctx.Director == null) return null;
            var prefab = Ctx.Director.EnemyPrefab(enemy);
            if (prefab == null) { Debug.LogWarning("[Summon] no enemy prefab '" + enemy + "'"); return null; }
            var srcSr = prefab.GetComponentInChildren<SpriteRenderer>(true);
            var srcAn = prefab.GetComponentInChildren<Animator>(true);
            if (srcSr == null) return null;
            var go = new GameObject("Cutscene " + enemy);
            go.transform.position = at;
            go.transform.localScale = srcSr.transform.lossyScale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = srcSr.sprite; sr.sharedMaterial = srcSr.sharedMaterial;
            sr.sortingLayerID = srcSr.sortingLayerID; sr.sortingOrder = srcSr.sortingOrder; sr.spriteSortPoint = srcSr.spriteSortPoint;
            sr.color = new Color(1f, 1f, 1f, 0f);
            if (srcAn != null && srcAn.runtimeAnimatorController != null)
            {
                var an = go.AddComponent<Animator>();
                an.runtimeAnimatorController = srcAn.runtimeAnimatorController;
            }
            return go;
        }

        private static float PlayActor(GameObject actor, string state, float fallback)
        {
            var an = actor != null ? actor.GetComponent<Animator>() : null;
            if (an == null || an.runtimeAnimatorController == null) return 0f;
            string pick = an.HasState(0, Animator.StringToHash(state + "_S")) ? state + "_S" : an.HasState(0, Animator.StringToHash(state)) ? state : null;
            if (pick == null) return 0f;
            an.Play(Animator.StringToHash(pick), 0, 0f);
            foreach (var c in an.runtimeAnimatorController.animationClips)
                if (c != null && c.name.EndsWith("_" + pick)) return Mathf.Clamp(c.length, 0.2f, 1.6f);
            return fallback;
        }

        private IEnumerator Throw(GameObject actor, Transform player, Vector3 to, Light2D glow, int layer, int order, string sets)
        {
            var asr = actor.GetComponent<SpriteRenderer>();
            Vector3 ap = actor.transform.position;
            // appears from mist behind Ren
            PlayActor(actor, "Idle", 0.5f);
            // `actor_vfx` (e.g. m3_intro_bats: PixelLab bats + blood mist) bursts exactly where the actor appears; wait for the mist-gathering beat
            float aScale = Mathf.Abs(actor.transform.localScale.y) * 100f / Mathf.Max(1f, asr != null && asr.sprite != null ? asr.sprite.pixelsPerUnit : 100f);
            if (Vfx("actor_vfx", ap, aScale, asr != null ? asr.sortingLayerID : layer, (asr != null ? asr.sortingOrder : order) + 1))
                yield return new WaitForSeconds(Float("actor_vfx_lead", 0.45f));
            for (int i = 0; i < 6; i++)
            {
                float a = i * 1.05f;
                Ctx.Run(CutsceneFx.Puff(ap + new Vector3(Mathf.Cos(a) * 0.45f, Mathf.Sin(a) * 0.25f + 0.15f, 0f), layer, order + 1, 16f, i * 0.06f));
            }
            for (float t = 0f; t < 0.6f; t += Time.deltaTime)
            {
                if (asr != null) asr.color = new Color(1f, 1f, 1f, t / 0.6f);
                glow.transform.position = Vector3.Lerp(player.position, ap, t / 0.6f);
                yield return null;
            }
            if (asr != null) asr.color = Color.white;
            var pc = player.GetComponent<PlayerCombat>();
            if (pc != null) pc.LookAt(Vector2.up, 3f);                   // Ren turns to look
            yield return new WaitForSeconds(0.35f);
            float castT = PlayActor(actor, Str("actor_anim_appear", "Cast"), 0.7f);
            string tl = Str("throw_line", "");
            if (tl.Length > 0) SpeechBubble.Show(ap + Vector3.up * 1.0f, tl, 1.8f, new Color(1f, 0.45f, 0.5f)).Follow(actor.transform);
            yield return new WaitForSeconds(Mathf.Max(0.5f, castT));
            PlayActor(actor, Str("actor_anim_throw", "Attack"), 0.6f);
            yield return new WaitForSeconds(0.22f);
            SetFlags(sets);

            // Ren flies in an arc to the arena
            var rb = player.GetComponent<Rigidbody2D>();
            bool sim = rb == null || rb.simulated;
            if (rb != null) { rb.linearVelocity = Vector2.zero; rb.simulated = false; }
            Vector3 from = player.position;
            float dur = Float("throw_time", 1.1f), arc = Float("throw_arc", 1.4f);
            float spinDir = to.x >= from.x ? -1f : 1f;
            CameraFollow.KickMain((Vector2)(to - from).normalized, 0.12f, 0.3f);
            if (actor != null) Vfx("actor_vfx", actor.transform.position, aScale, asr != null ? asr.sortingLayerID : layer, (asr != null ? asr.sortingOrder : order) + 1);   // dissolves into a bat swarm
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                float u = t / dur;
                player.position = Vector3.Lerp(from, to, u) + Vector3.up * (arc * 4f * u * (1f - u));
                player.rotation = Quaternion.Euler(0f, 0f, spinDir * 540f * u);
                glow.transform.position = player.position;
                glow.intensity = Mathf.Lerp(1.6f, 0.4f, u);
                if (asr != null) asr.color = new Color(1f, 1f, 1f, Mathf.Clamp01(1f - u * 2.2f));     // the actor dissolves into mist
                yield return null;
            }
            player.rotation = Quaternion.identity;
            player.position = to;
            if (rb != null) { rb.position = to; rb.simulated = sim; }
            if (actor != null) Object.Destroy(actor);
            // landing
            CameraFollow.KickMain(Vector2.down, 0.16f, 0.4f);
            CutsceneFx.Burst(Ctx.Host, to, 0.6f, 0.22f, 8, 6, layer, order);
            Vfx("land_vfx", to, 0.5f, layer, order - 1);                 // e.g. ground_impact (wave + cracked ground)
            if (pc != null) { pc.StopLook(); pc.PlayStagger(); }
            yield return new WaitForSeconds(0.7f);
        }
    }
}
