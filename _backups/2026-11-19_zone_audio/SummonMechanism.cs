using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 07/11: summon — cảnh "bị hút / kéo / ném đi" (vd rung chuông Tây xong, Isolde ném Ren vào Bower).
    /// Khi đủ cờ `when` và CHƯA có cờ `sets` (cảnh chỉ chạy một lần, kể cả sau khi nạp bản lưu):
    ///   đợi `delay` giây -> khoá điều khiển, Ren bất tử, quầng sáng `color` + đất rung -> hội thoại `speaker` (line1..line8, tự sang câu
    ///   sau `auto` giây) -> bật cờ `sets` -> Ren tới `target` ("x,y" ô Tiled):
    ///   * có `actor` (tên quái, vd Matriarch_Isolde): "diễn viên" (chỉ hình + Animator, không não) hiện ra từ sương ngay SAU lưng Ren
    ///     (`actor_behind` đv phía Bắc), Ren quay lại nhìn, diễn viên niệm (`actor_anim_appear`, mặc định Cast) rồi vung tay
    ///     (`actor_anim_throw`, mặc định Attack) — Ren bay vòng cung (`throw_time`, `throw_arc`) qua đầu mọi thứ tới `target`, tiếp đất
    ///     (rung + bụi + loạng choạng); diễn viên tan vào sương. `throw_line`: câu nói (bóng thoại) lúc ra tay.
    ///   * không có `actor`: tối màn dịch chuyển (`warp_text`, `shake`).
    /// Khu đích (đấu trường có cổng close_on_enter) tự bắt đầu khi Ren đứng trong khung -> cổng khoá, boss xuất hiện (BossIntro).
    /// </summary>
    public sealed class SummonMechanism : Mechanism
    {
        private bool running;

        public override void OnFlagsChanged()
        {
            if (Done || running) return;
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

            // quầng sáng quanh Ren + đất rung trong lúc nói
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
            if (lines.Count > 0) DialogueBox.Open(Str("speaker", ""), lines.ToArray(), null, Float("auto", 2.6f));
            float nextKick = 0f, tt = 0f;
            while (DialogueBox.Active)
            {
                GameInput.Blocked = true;                                // đóng menu Pause nhả Blocked -> khoá lại
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
                // bị kéo đi: quầng sáng dâng, rồi tối màn dịch chuyển
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

        /// 07/11: phát hiệu ứng PixelLab Resources/VFX/&lt;tên trong thuộc tính key&gt; (`key_scale`, `key_fps` ghi đè). Trả về true nếu có phát.
        private bool Vfx(string key, Vector3 at, float scale, int layer, int order)
        {
            string n = Str(key, "");
            if (n.Length == 0 || n == "none") return false;
            return SpriteVfx.Spawn(n, at, Float(key + "_scale", scale), layer, order, false, Float(key + "_fps", 14f)) != null;
        }

        // ---- diễn viên: chỉ SpriteRenderer + Animator chép từ prefab quái (không EnemyAI / Health — không đánh, không bị đánh)
        private GameObject MakeActor(string enemy, Vector3 at)
        {
            if (string.IsNullOrEmpty(enemy) || Ctx.Director == null) return null;
            var prefab = Ctx.Director.EnemyPrefab(enemy);
            if (prefab == null) { Debug.LogWarning("[Summon] không có prefab quái '" + enemy + "'"); return null; }
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
            // hiện ra từ sương sau lưng Ren
            PlayActor(actor, "Idle", 0.5f);
            // 07/11: `actor_vfx` (vd m3_intro_bats: dơi + sương máu PixelLab) nổ đúng chỗ diễn viên hiện hình; chờ nhịp gom sương
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
            if (pc != null) pc.LookAt(Vector2.up, 3f);                   // Ren quay lại nhìn
            yield return new WaitForSeconds(0.35f);
            float castT = PlayActor(actor, Str("actor_anim_appear", "Cast"), 0.7f);
            string tl = Str("throw_line", "");
            if (tl.Length > 0) SpeechBubble.Show(ap + Vector3.up * 1.0f, tl, 1.8f, new Color(1f, 0.45f, 0.5f)).Follow(actor.transform);
            yield return new WaitForSeconds(Mathf.Max(0.5f, castT));
            PlayActor(actor, Str("actor_anim_throw", "Attack"), 0.6f);
            yield return new WaitForSeconds(0.22f);
            SetFlags(sets);

            // Ren bay vòng cung tới đấu trường
            var rb = player.GetComponent<Rigidbody2D>();
            bool sim = rb == null || rb.simulated;
            if (rb != null) { rb.linearVelocity = Vector2.zero; rb.simulated = false; }
            Vector3 from = player.position;
            float dur = Float("throw_time", 1.1f), arc = Float("throw_arc", 1.4f);
            float spinDir = to.x >= from.x ? -1f : 1f;
            CameraFollow.KickMain((Vector2)(to - from).normalized, 0.12f, 0.3f);
            if (actor != null) Vfx("actor_vfx", actor.transform.position, aScale, asr != null ? asr.sortingLayerID : layer, (asr != null ? asr.sortingOrder : order) + 1);   // tan thành bầy dơi
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                float u = t / dur;
                player.position = Vector3.Lerp(from, to, u) + Vector3.up * (arc * 4f * u * (1f - u));
                player.rotation = Quaternion.Euler(0f, 0f, spinDir * 540f * u);
                glow.transform.position = player.position;
                glow.intensity = Mathf.Lerp(1.6f, 0.4f, u);
                if (asr != null) asr.color = new Color(1f, 1f, 1f, Mathf.Clamp01(1f - u * 2.2f));     // diễn viên tan vào sương
                yield return null;
            }
            player.rotation = Quaternion.identity;
            player.position = to;
            if (rb != null) { rb.position = to; rb.simulated = sim; }
            if (actor != null) Object.Destroy(actor);
            // tiếp đất
            CameraFollow.KickMain(Vector2.down, 0.16f, 0.4f);
            CutsceneFx.Burst(Ctx.Host, to, 0.6f, 0.22f, 8, 6, layer, order);
            Vfx("land_vfx", to, 0.5f, layer, order - 1);                 // 07/11: vd ground_impact (sóng + nứt đất)
            if (pc != null) { pc.StopLook(); pc.PlayStagger(); }
            yield return new WaitForSeconds(0.7f);
        }
    }
}
