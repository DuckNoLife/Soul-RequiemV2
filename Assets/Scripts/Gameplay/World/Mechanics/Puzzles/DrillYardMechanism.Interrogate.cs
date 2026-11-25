using System.Collections;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// FINAL EXAM — TIME UP WITHOUT STANDING ON ANY DOOR = WRONG.
    /// Short, undodgeable scene:
    ///   1) Ren FREEZES: controls locked, cold blue tint, stun stars overhead, "FREEZE!".
    ///   2) Burlap walks right up to Ren and INTERROGATES via speech bubbles: "NAME?!" -> Ren "...Seven." -> "RANK?!" -> Ren "...None."
    ///      -> "And the ANSWER to …?!" -> Ren "......".
    ///   3) Burlap backs off 1.3 u, warning line, RAMS Ren — always hits (ignores rolling / invulnerability): Ren is flung spinning, -1 life.
    ///   4) Controls return, Burlap goes back to the lectern; the question is redone (new question, same level).
    /// Cancelled midway (abort / death) -> Unfreeze() (called by ClearField) always restores controls + body colour.
    /// </summary>
    public sealed partial class DrillYardMechanism
    {
        private bool frozen;
        private SpriteRenderer frozenSr;
        private Color frozenColor = Color.white;
        private SpriteVfx frozenStars;

        private void Freeze()
        {
            if (frozen || Ctx.Player == null) return;
            frozen = true;
            GameInput.Blocked = true;
            Ctx.SetPlayerControl(false);
            frozenSr = Ctx.Player.GetComponentInChildren<SpriteRenderer>();
            if (frozenSr != null) { frozenColor = frozenSr.color; frozenSr.color = new Color(0.62f, 0.78f, 1f, frozenColor.a); }
            frozenStars = SpriteVfx.SpawnAttached("stun_stars", Ctx.Player, 0.8f, Layer, Order + 3, 12f, 30f);
            if (frozenStars != null) frozenStars.transform.position = PlayerFeet + Vector2.up * 0.95f;
            combat?.LookAt(king.Pos - PlayerFeet, 60f);
        }

        private void Unfreeze()
        {
            if (!frozen) return;
            frozen = false;
            if (frozenSr != null) frozenSr.color = frozenColor;
            frozenSr = null;
            if (frozenStars != null) frozenStars.Stop();
            frozenStars = null;
            combat?.StopLook();
            if (Ctx.Player != null) { Ctx.Player.rotation = Quaternion.identity; Ctx.SetPlayerControl(true); }
            GameInput.Blocked = false;
        }

        private void RenSays(string text, float seconds)
        {
            if (Ctx.Player == null) return;
            SpeechBubble.Show(Ctx.Player.position + new Vector3(0f, 0.95f, 0f), text, seconds).Follow(Ctx.Player);
        }

        /// <summary>Wait `s` seconds (stops if the station is cancelled / failed). Not Wait(), because this scene must run until controls are returned.</summary>
        private IEnumerator IWait(float s)
        {
            float end = Time.time + s;
            while (Time.time < end && !aborted) yield return null;
        }

        private IEnumerator Interrogate(Quiz quiz)
        {
            // 1) freeze
            Freeze();
            Sfx.Play("drill_wrong", PlayerFeet, 0.9f);
            Sfx.Play("lift_jam", king.Pos, 0.6f);
            DrillBoard.Say("TIME'S UP - FREEZE!", Red);
            Bark(Pick("TIME! Nobody MOVES!", "PENCILS DOWN! *CLICK* ...You. Don't. Move.", "SILENCE IS NOT AN ANSWER!"), 1.6f, true);
            yield return IWait(1.0f);
            if (aborted) { Unfreeze(); yield break; }
            // 2) Burlap walks up to Ren and interrogates
            Vector2 ren = PlayerFeet;
            float side = ren.x >= A.center.x ? -1f : 1f;
            Vector2 face = ren + new Vector2(side * 0.62f, 0.05f);
            face = new Vector2(Mathf.Clamp(face.x, A.xMin + 0.4f, A.xMax - 0.4f), Mathf.Clamp(face.y, A.yMin + 0.3f, A.yMax - 0.6f));
            king.Play("move", 14f);
            while ((king.Pos - face).sqrMagnitude > 0.0004f && !aborted)
            {
                king.Sr.flipX = face.x < king.Pos.x;
                king.Pos = Vector2.MoveTowards(king.Pos, face, 3.2f * Time.deltaTime);
                if (Mathf.Repeat(Time.time, 0.3f) < Time.deltaTime) Sfx.Play("heavy_land", king.Pos, 0.3f);
                yield return null;
            }
            if (aborted) { Unfreeze(); yield break; }
            king.Pos = face; king.Play("idle", 6f);
            king.Sr.flipX = ren.x < king.Pos.x;
            CameraFollow.KickMain(Vector2.down, 0.04f, 0.15f);
            Bark("NAME?!", 1.2f, true); Sfx.Play("hit_wood", king.Pos, 0.5f);
            yield return IWait(1.1f);
            RenSays("...Seven.", 1.2f);
            yield return IWait(1.2f);
            Bark("RANK?!", 1.2f, true); Sfx.Play("hit_wood", king.Pos, 0.5f);
            yield return IWait(1.1f);
            RenSays("...None.", 1.2f);
            yield return IWait(1.2f);
            Bark("And the ANSWER to " + quiz.Hud.Replace(" = ?", "") + "?!", 1.8f, true); Sfx.Play("hit_wood", king.Pos, 0.6f);
            yield return IWait(1.6f);
            RenSays("......", 1.2f);
            yield return IWait(1.2f);
            Bark(Pick(quiz.Answer + "! It was " + quiz.Answer + "! DISCIPLINARY ACTION!", "WRONG! The answer was " + quiz.Answer + "! BRACE!",
                      "Silence?! In MY classroom?! It was " + quiz.Answer + "! *REVVING*"), 1.8f, true);
            if (aborted) { Unfreeze(); yield break; }
            // 3) back off for a run-up
            Vector2 back = ren + new Vector2(side * 1.9f, 0f);
            if (back.x < A.xMin + 0.35f || back.x > A.xMax - 0.35f) back = ren + new Vector2(-side * 1.9f, 0f);
            back = new Vector2(Mathf.Clamp(back.x, A.xMin + 0.35f, A.xMax - 0.35f), back.y);
            king.Play("move", 10f);
            for (float t = 0f; t < 0.5f && !aborted; t += Time.deltaTime)
            {
                king.Pos = Vector2.Lerp(face, back, t / 0.5f);
                yield return null;
            }
            king.Pos = back;
            Vector2 dir = (ren - back).sqrMagnitude > 0.0001f ? (ren - back).normalized : Vector2.right;
            king.Sr.flipX = dir.x < 0f;
            DrillFx.Warn(Ctx.Host, back + dir * 0.35f, ren, 0.7f, dir, Ctx.Player, 0.34f);
            for (float t = 0f; t < 0.7f && !aborted; t += Time.deltaTime) { king.Pos = back + Random.insideUnitCircle * 0.02f; yield return null; }
            if (aborted) { Unfreeze(); yield break; }
            // 4) RAM — always hits
            king.Play("dash", 20f);
            Sfx.Play("ram_charge", king.Pos);
            float dur = Mathf.Max(0.08f, (ren - back).magnitude / 8.5f);
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                king.Pos = Vector2.Lerp(back, ren - dir * 0.15f, t / dur);
                king.Sr.transform.rotation = Quaternion.Euler(0f, 0f, -10f * Mathf.Sign(dir.x));
                SpriteVfx.Spawn("m3_dash_dust", king.Pos, 1.1f, Layer, king.Sr.sortingOrder - 1, dir.x < 0f, 18f, false);
                yield return null;
            }
            king.Sr.transform.rotation = Quaternion.identity;
            king.Play("dashend", 14f, false);
            DrillFx.Impact(Ctx.Host, ren, dir, Layer, Order, 0.14f);
            DrillFx.HitStop(Ctx.Host, 0.12f);
            Sfx.Play("blunt_strike", ren);
            Sfx.Play("duo_punch", ren, 0.8f);
            if (Ctx.PlayerHealth != null && !Ctx.PlayerHealth.IsDead) TrapArt.HurtPlayerCrush(Ctx, 6f, dir, 0f);
            LoseLife("INTERROGATED!  LIFE -1");
            // Ren is flung spinning and lands inside the yard
            Vector2 land = ren + dir * 1.25f;
            land = new Vector2(Mathf.Clamp(land.x, A.xMin + 0.3f, A.xMax - 0.3f), Mathf.Clamp(land.y, A.yMin + 0.2f, A.yMax - 0.5f));
            var p = Ctx.Player;
            if (p != null && !(Ctx.PlayerHealth != null && Ctx.PlayerHealth.IsDead))
            {
                for (float t = 0f; t < 0.45f; t += Time.deltaTime)
                {
                    float u = t / 0.45f;
                    PlacePlayer(Vector2.Lerp(ren, land, u) + Vector2.up * Mathf.Sin(u * Mathf.PI) * 0.5f);
                    p.rotation = Quaternion.Euler(0f, 0f, -360f * u * Mathf.Sign(dir.x));
                    yield return null;
                }
                p.rotation = Quaternion.identity;
                PlacePlayer(land);
                Sfx.Play("body_fall", land, 0.7f);
                SpriteVfx.Spawn("m3_dash_dust", land, 1.2f, Layer, Order - 1, false, 16f, false);
                CameraFollow.KickMain(Vector2.down, 0.08f, 0.25f);
            }
            yield return IWait(0.5f);
            Unfreeze();
            if (aborted || stationFailed) yield break;
            Bark(Pick("Class is STILL in session! Again!", "Let's try that AGAIN. With NUMBERS this time.", "Answer the QUESTION, recruit!"), 2f, true);
            yield return WalkKing(lectern, 2.4f);
        }
    }
}
