using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// registrar_summons - scene where The Registrar pulls Ren into THE MARGIN (Scriptorium, Map 4).
    /// Flags `when` met (default c2_cleared: every Scriptorium enemy defeated), no `unless` (c2r_cleared: already won inside the Margin),
    /// Ren within `radius` (units) of the object and not in combat -> scene:
    ///   ink wells up on the altar, the Registrar (actor copied from prefab `actor`, x2 size) rises; a few lines (long the first time, one line afterwards);
    ///   he POINTS (Attack animation) -> a purple sigil under Ren's feet, Ren is lifted -> flag `sets` (c2_summoned) -> leaves the map through exit `exit`
    ///   (triggers layer, type exit, target map4m_margin, requires c2_summoned - placed inside the wall, used only by this scene).
    /// Actor feet = middle of the object area's bottom edge.
    /// </summary>
    public sealed class RegistrarSummonsMechanism : Mechanism
    {
        private bool playing;
        private float nextCheck;

        private bool Ready
        {
            get
            {
                if (!Ctx.Flags.HasAll(Str("when", "c2_cleared"))) return false;
                string unless = Str("unless", "c2r_cleared");
                return unless.Length == 0 || !Ctx.Flags.HasAll(unless);
            }
        }

        public override void OnFlagsChanged()
        {
            string unless = Str("unless", "c2r_cleared");
            if (unless.Length > 0 && Ctx.Flags.HasAll(unless)) Done = true;
        }

        public override void Tick(Vector2 playerPos, bool inside)
        {
            if (playing || Time.time < nextCheck) return;
            nextCheck = Time.time + 0.25f;
            if (!Ready || Ctx.Player == null) return;
            if (Ctx.Director.InCombat || Ctx.Director.Respawning || CutsceneState.Active || SceneTransition.Busy || StoryScenes.Busy || DialogueBox.Active) return;
            if (Ctx.PlayerHealth != null && Ctx.PlayerHealth.IsDead) return;
            if (DistanceTo(playerPos) > Float("radius", 9f)) return;
            playing = true;
            Ctx.Run(Scene());
        }

        private IEnumerator Scene()
        {
            var player = Ctx.Player;
            bool again = Ctx.Flags.HasAll(Str("sets", "c2_summoned"));
            Vector2 foot = new Vector2(Rect.center.x, Rect.yMin);
            CutsceneState.Begin();
            GameInput.Blocked = true;
            Ctx.SetPlayerControl(false);
            var cam = Ctx.Camera;
            float size = Camera.main != null ? Camera.main.orthographicSize : 3f;
            if (cam != null) cam.Focus(Vector3.Lerp(foot + Vector2.up * 0.9f, player.position, 0.45f), size);
            yield return new WaitForSeconds(again ? 0.4f : 0.9f);

            // ink wells up, the Registrar rises
            int layer = 0, order = 2;
            var psr = player.GetComponentInChildren<SpriteRenderer>();
            if (psr != null) { layer = psr.sortingLayerID; order = psr.sortingOrder; }
            Sfx.Play("teleport_dark", foot, 0.9f);
            Sfx.Play("quake_rumble", foot, 0.6f);
            SpriteVfx.Spawn("m3_vespers", foot + Vector2.up * 0.4f, 1.6f, layer, order + 2, false, 14f);
            CameraFollow.KickMain(Vector2.down, 0.05f, 0.4f);
            var glowGo = new GameObject("Registrar summons glow");
            glowGo.transform.position = foot + Vector2.up * 0.6f;
            var glow = glowGo.AddComponent<Light2D>();
            glow.lightType = Light2D.LightType.Point;
            glow.color = new Color(0.66f, 0.42f, 1f);
            glow.pointLightOuterRadius = 2.8f; glow.pointLightInnerRadius = 0.4f; glow.intensity = 0f;
            var actor = MakeActor(Str("actor", "The_Registrar"), foot);
            var asr = actor != null ? actor.GetComponent<SpriteRenderer>() : null;
            Vector3 full = actor != null ? actor.transform.localScale : Vector3.one;
            for (float t = 0f; t < 0.9f; t += Time.deltaTime)
            {
                float u = t / 0.9f;
                glow.intensity = Mathf.Lerp(0f, 1.4f, u);
                if (actor != null)
                {
                    actor.transform.localScale = new Vector3(full.x, full.y * Mathf.Lerp(0.15f, 1f, u * (2f - u)), full.z);
                    if (asr != null) { asr.color = new Color(1f, 1f, 1f, u); asr.flipX = player.position.x < foot.x; }
                }
                yield return null;
            }
            if (actor != null) actor.transform.localScale = full;
            PlayActor(actor, "Idle");

            Vector3 head = foot + Vector2.up * 2.45f;
            if (!again)
            {
                yield return Line(head, "Seven. You were struck from this book once already.", "The Registrar", 3.2f);
                yield return Line(player.position + new Vector3(0f, 0.95f, 0f), "Then you'll have to try harder.", null, 2.4f);
                yield return Line(head, "Oh, I intend to. But not here, among the others.", "The Registrar", 3f);
                yield return Line(head, "You deserve a page of your own.", "The Registrar", 2.6f);
            }
            else yield return Line(head, "Back to your page, Seven. We were not finished.", "The Registrar", 2.8f);

            // point -> purple sigil under Ren's feet -> lifted
            PlayActor(actor, "Attack");
            Sfx.Play("registrar_quill", foot, 1f);
            yield return new WaitForSeconds(0.35f);
            Sfx.Play("reg_grip", player.position, 1f);
            var sig = new GameObject("Registrar summons sigil").AddComponent<SpriteRenderer>();
            sig.sprite = MarginArt.Sigil();
            sig.sortingLayerID = layer; sig.sortingOrder = order - 3;
            FxLighting.MakeUnlit(sig);
            Vector3 p0 = player.position;
            var rb = player.GetComponent<Rigidbody2D>();
            EnergyAura aura = null;
            for (float t = 0f; t < 1.1f; t += Time.deltaTime)
            {
                float u = t / 1.1f;
                sig.transform.position = p0;
                sig.transform.rotation = Quaternion.Euler(0f, 0f, t * 140f);
                float s = Mathf.Lerp(0.2f, 0.95f, Mathf.Min(1f, u * 2f));
                sig.transform.localScale = new Vector3(s, s * 0.62f, 1f);
                sig.color = new Color(0.8f, 0.55f, 1f, 0.85f);
                if (u > 0.4f)
                {
                    float h = Mathf.Lerp(0f, 0.38f, (u - 0.4f) / 0.6f) + Mathf.Sin(t * 45f) * 0.012f;
                    Vector3 at = p0 + Vector3.up * h;
                    if (rb != null) { rb.position = at; rb.linearVelocity = Vector2.zero; }
                    player.position = at;
                    if (aura == null) aura = EnergyAura.Attach(player, MarginArt.Violet);   // aura around the whole body
                }
                glow.intensity = 1.4f + Mathf.Sin(t * 20f) * 0.3f;
                yield return null;
            }
            Ctx.Flags.Set(Str("sets", "c2_summoned"));
            // hand over to SceneTransition (it locks controls during the map change itself)
            GameInput.Blocked = false;
            Ctx.SetPlayerControl(true);
            if (cam != null) cam.Release();
            CutsceneState.End();
            Ctx.Director.ExitNow(Str("exit", "to_margin"));
            yield return new WaitForSeconds(0.2f);
            if (!SceneTransition.Busy)
            {
                // change failed (scene not in Build Settings): put Ren back on the ground, clean up the scene, try again later
                if (rb != null) { rb.position = p0; rb.linearVelocity = Vector2.zero; }
                player.position = p0;
                if (aura != null) aura.Stop();
                if (actor != null) Object.Destroy(actor);
                Object.Destroy(sig.gameObject);
                Object.Destroy(glowGo);
                nextCheck = Time.time + 15f;
                playing = false;
            }
        }

        private static IEnumerator Line(Vector3 at, string text, string speaker, float seconds)
        {
            if (speaker != null) SpeechBubble.Show(at, text, seconds, new Color(0.82f, 0.72f, 1f), speaker);
            else SpeechBubble.Show(at, text, seconds);
            yield return new WaitForSeconds(seconds + 0.15f);
        }

        // ---- actor: only a SpriteRenderer + Animator copied from the enemy prefab (like SummonMechanism)
        private GameObject MakeActor(string enemy, Vector3 at)
        {
            if (string.IsNullOrEmpty(enemy) || Ctx.Director == null) return null;
            var prefab = Ctx.Director.EnemyPrefab(enemy);
            if (prefab == null) { Debug.LogWarning("[RegistrarSummons] no enemy prefab '" + enemy + "'"); return null; }
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

        private static void PlayActor(GameObject actor, string state)
        {
            var an = actor != null ? actor.GetComponent<Animator>() : null;
            if (an == null || an.runtimeAnimatorController == null) return;
            string pick = an.HasState(0, Animator.StringToHash(state + "_S")) ? state + "_S" : an.HasState(0, Animator.StringToHash(state)) ? state : null;
            if (pick != null) an.Play(Animator.StringToHash(pick), 0, 0f);
        }
    }
}
