using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 2 MILITARY CUTSCENES of Burlap's Proving Grounds (FIRST time only — saved flags; Space / Enter = skip):
    ///   MUSTER (first time Ren enters, flag drill_muster_seen): war drums, 3 companies of dummies (sandbag / straw / post) march in from both yard
    ///     edges and form 3 rows before Burlap -> "TEN-HUT!" (everyone snaps to attention) -> ROLL CALL per company (sandbags hop, straw spins blades,
    ///     posts spin cloaks) -> "PRESENT... ARMS!" (all spin at once) -> Burlap briefs the 3 stations (dialogue box) -> "DIS-MISSED!" (they march off and vanish).
    ///   THE SERGEANT TAKES THE FIELD (first time at station 3, flag drill_sergeant_seen): drum roll, 2 HONOUR ROWS of straw dummies form an aisle,
    ///     "PRESENT ARMS!", Burlap walks down the aisle to mid-yard, stomps (shake + wave), briefs, BOSS NAME CARD (BossIntro.Card*), the guard disperses.
    /// During cutscenes: controls locked (GameInput.Blocked), Ren invulnerable and facing Burlap, camera framing (CameraFollow.Focus),
    /// CutsceneState.Active (HUD hides arrows; SceneMusic mutes music).
    /// </summary>
    public sealed partial class DrillYardMechanism
    {
        private const string MusterFlag = "drill_muster_seen", SergeantFlag = "drill_sergeant_seen";
        private bool inScene, sceneSkip;
        private CameraFollow sceneCam;
        private float sceneSize, sceneSharp;
        private PlayerCombat scenePc;

        // ------------------------------------------------------------------ shared cutscene framing
        private void BeginScene(Vector2 focus)
        {
            inScene = true; sceneSkip = false;
            Ctx.Run(SceneLock());
            CutsceneState.Begin();
            if (Ctx.PlayerHealth != null) Ctx.PlayerHealth.Invulnerable = true;
            scenePc = Ctx.Player != null ? Ctx.Player.GetComponent<PlayerCombat>() : null;
            if (scenePc != null) scenePc.LookAt(king.Pos - PlayerFeet, 60f);
            sceneCam = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            if (sceneCam != null)
            {
                sceneSize = Camera.main.orthographicSize;            // keep the frame size (resizing makes pixels shimmer out of step)
                sceneSharp = sceneCam.focusSharpness;
                sceneCam.focusSharpness = 3f;
                lookHint = focus;
                Ctx.Run(SceneFrame());                                            // camera frames every character automatically
            }
        }

        /// <summary>Point the scene wants to emphasise (kept inside the frame) — the actual frame is computed by SceneFrame.</summary>
        private void Look(Vector2 focus) { lookHint = focus; }

        private Vector2? lookHint;

        /// <summary>
        /// Every cutscene frame: bounding box of Burlap + Ren + every visible dummy (feet -> head top) + the focus point;
        /// centre the camera on it and pull back just enough to see everything (at least the size on entering the scene, at most 2.2x).
        /// Only changes target on a clear difference (centre > 5 cm, size > 4%) so the frame does not wobble with every footstep.
        /// </summary>
        private IEnumerator SceneFrame()
        {
            var cam = Camera.main;
            float size = sceneSize;
            Vector2 centre = lookHint ?? king.Pos;
            bool first = true;
            while (inScene && sceneCam != null && cam != null)
            {
                Vector2 lo = king.Pos, hi = king.Pos + Vector2.up * 1.15f;
                void Add(Vector2 feet, float h)
                {
                    lo = Vector2.Min(lo, feet + Vector2.left * 0.3f);
                    hi = Vector2.Max(hi, feet + new Vector2(0.3f, h));
                }
                Add(king.Pos, 1.15f);
                if (Ctx.Player != null) Add(PlayerFeet, 0.95f);
                foreach (var a in actors)
                {
                    if (a == null || a.Sr == null || !a.Sr.enabled || a.EdgeAlpha < 0.25f) continue;
                    if (a.Pos.x < A.xMin - 0.3f || a.Pos.x > A.xMax + 0.3f || a.Pos.y < A.yMin - 0.3f || a.Pos.y > A.yMax + 0.3f) continue;
                    Add(a.Pos, 0.9f * a.Scale / 1.35f);
                }
                if (lookHint.HasValue) Add(lookHint.Value, 0.2f);
                lo.y -= 0.6f;                                                          // leave room at the bottom for the dialogue box (do not cover the characters' feet)
                Vector2 c = (lo + hi) * 0.5f;
                float need = Mathf.Max(sceneSize, (hi.y - lo.y) * 0.5f + 0.45f, ((hi.x - lo.x) * 0.5f + 0.6f) / Mathf.Max(0.1f, cam.aspect));
                need = Mathf.Min(need, sceneSize * 2.2f);
                if (first || Mathf.Abs(need - size) > size * 0.04f) size = need;
                if (first || (c - centre).sqrMagnitude > 0.0025f) centre = c;
                first = false;
                sceneCam.Focus(centre, size);
                yield return null;
            }
        }

        private IEnumerator EndScene()
        {
            if (DialogueBox.Active) DialogueBox.Close();
            if (sceneCam != null) { sceneCam.Release(); }
            yield return new WaitForSeconds(0.35f);
            if (sceneCam != null) sceneCam.focusSharpness = sceneSharp;
            CutsceneState.End();
            if (scenePc != null) scenePc.StopLook();
            if (Ctx.PlayerHealth != null) Ctx.PlayerHealth.Invulnerable = false;
            inScene = false;
        }

        /// <summary>Lock controls for the whole scene (closing the Pause menu releases Blocked -> re-lock every frame); Space / Enter = skip.</summary>
        private IEnumerator SceneLock()
        {
            while (inScene)
            {
                GameInput.Blocked = true;
                var kb = Keyboard.current;
                if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)) sceneSkip = true;
                yield return null;
            }
            GameInput.Blocked = false;
        }

        private IEnumerator SWait(float s)
        {
            float end = Time.time + s;
            while (Time.time < end && !sceneSkip) yield return null;
        }

        /// <summary>Bottom dialogue box (name "Sgt. Burlap"; T / click = next line, auto-advances after 1.6 s).</summary>
        private IEnumerator Speech(params string[] lines)
        {
            if (sceneSkip) yield break;
            DialogueBox.Open("Sgt. Burlap", lines, null, 1.6f);
            while (DialogueBox.Active && !sceneSkip) yield return null;
            if (DialogueBox.Active) DialogueBox.Close();
        }

        /// <summary>Short shout (subtitle) + one drum beat.</summary>
        private void Shout(string text, float seconds = 1.6f, string drum = "rf_war_drum")
        {
            Bark(text, seconds, true);
            if (!string.IsNullOrEmpty(drum)) Sfx.Play(drum, king.Pos, 0.8f);
        }

        // ------------------------------------------------------------------ dummy moves
        private DrillActor Soldier(string kind, Vector2 at)
        {
            var a = new DrillActor(kind, at, Ctx.Host.transform, Ctx.Player) { Harmless = true, FaceByVel = true, EdgeAlpha = 0f };
            a.Play("move", 12f);
            a.Tick(0f);
            actors.Add(a);
            return a;
        }

        /// <summary>March to a spot (steady steps); on arrival stand at attention facing south (toward Ren).</summary>
        private IEnumerator March(DrillActor a, Vector2 to, float speed, float delay)
        {
            float t0 = Time.time + delay;
            while (Time.time < t0 && !sceneSkip) yield return null;
            a.Play("move", 12f);
            while (actors.Contains(a) && (a.Pos - to).sqrMagnitude > 0.0004f)
            {
                if (sceneSkip) { a.Pos = to; break; }
                a.Pos = Vector2.MoveTowards(a.Pos, to, speed * Time.deltaTime);
                a.Sr.flipX = to.x < a.Pos.x;
                yield return null;
            }
            a.Vel = Vector2.zero;
            a.Play("idle", 4f);
            a.Sr.flipX = false;
        }

        /// <summary>"HUT!" hop in place.</summary>
        private IEnumerator Hop(DrillActor a, float delay, float height = 0.09f)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            for (float t = 0f; t < 0.22f && a.Sr != null; t += Time.deltaTime) { a.Lift = Mathf.Sin(t / 0.22f * Mathf.PI) * height; yield return null; }
            a.Lift = 0f;
        }

        /// <summary>Salute: one spin (straw spins blades, posts spin cloaks, sandbags roll) + wind ring, then back to attention.</summary>
        private IEnumerator Salute(DrillActor a, float delay, float seconds = 0.55f)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (a.Sr == null) yield break;
            a.Play(a.Kind == "sack" ? "roll" : "spin", 22f);
            a.Whirl(true, a.Kind == "pell" ? 1.25f : a.Kind == "sack" ? 0.7f : 0.95f);
            yield return new WaitForSeconds(seconds);
            if (a.Sr == null) yield break;
            a.Whirl(false);
            a.Play("idle", 4f);
            a.Sr.transform.localScale = new Vector3(a.Scale, a.Scale, 1f);
        }

        /// <summary>Dismiss: march to the nearest yard edge and vanish.</summary>
        private IEnumerator Dismiss(List<DrillActor> squad)
        {
            foreach (var a in squad)
            {
                if (!actors.Contains(a)) continue;
                var to = new Vector2(a.Pos.x < A.center.x ? A.xMin - 0.6f : A.xMax + 0.6f, a.Pos.y);
                Ctx.Run(MarchOut(a, to));
            }
            yield break;
        }

        private IEnumerator MarchOut(DrillActor a, Vector2 to)
        {
            yield return new WaitForSeconds(Random.Range(0f, 0.25f));
            a.Play("move", 14f);
            float end = Time.time + 4f;
            while (actors.Contains(a) && (a.Pos - to).sqrMagnitude > 0.0004f && Time.time < end)
            {
                a.Pos = Vector2.MoveTowards(a.Pos, to, 2.4f * Time.deltaTime);
                a.Sr.flipX = to.x < a.Pos.x;
                yield return null;
            }
            yield return Vanish(a);
        }

        // ------------------------------------------------------------------ SCENE 1: MUSTER (first time in the yard)
        private IEnumerator Muster()
        {
            Ctx.Flags.Set(MusterFlag);
            BeginScene(kingHome + Vector2.down * 1.3f);
            var squad = new List<DrillActor>();
            string[] kinds = { "sack", "straw", "pell" };
            const int Per = 7;
            float sp = 0.5f;
            yield return SWait(0.3f);
            // the machine starts + the yard LOCKS behind Ren
            Sfx.Play("reel_spin", king.Pos, 0.8f);
            king.Flash(new Color(0.6f, 0.9f, 1f), 0.6f);
            Shout("*CLICK-WHIRR* ...DRILL AUTOMATON B-4 'BURLAP': ONLINE. INTRUDER DETECTED. ENLISTMENT: MANDATORY.", 2.6f, "reel_stop");
            yield return SWait(1.6f);
            Seal(true);
            CameraFollow.KickMain(Vector2.right, 0.08f, 0.3f);
            Shout("YARD: SEALED. Nobody leaves my yard until they PASS. Or faint. Fainting is also acceptable.", 2.4f, "heavy_land");
            yield return SWait(1.8f);
            Shout("SQUAAAD! FALL... IN!", 1.8f);
            for (int r = 0; r < 3; r++)
                for (int i = 0; i < Per; i++)
                {
                    var slot = new Vector2(kingHome.x + (i - (Per - 1) * 0.5f) * sp, kingHome.y - 0.95f - r * 0.52f);
                    bool west = i < Per / 2 || (i == Per / 2 && r % 2 == 0);
                    var from = new Vector2(west ? A.xMin - 0.5f : A.xMax + 0.5f, slot.y);
                    var a = Soldier(kinds[r], from);
                    squad.Add(a);
                    int k = west ? (Per / 2 - i) : (i - Per / 2);          // the leader walks deepest -> the whole row arrives together
                    Ctx.Run(March(a, slot, 1.9f, r * 0.45f + Mathf.Abs(k) * 0.05f));
                }
            // marching drum beat
            for (int b = 0; b < 8 && !sceneSkip; b++)
            {
                Sfx.Play(b % 2 == 0 ? "rf_war_drum" : "footstep", A.center, b % 2 == 0 ? 0.55f : 0.6f);
                yield return SWait(0.5f);
            }
            yield return SWait(0.6f);
            // TEN-HUT! — everyone snaps to attention
            Shout("TEN... HUT!", 1.4f, "hit_wood");
            foreach (var a in squad) Ctx.Run(Hop(a, 0f, 0.07f));
            CameraFollow.KickMain(Vector2.down, 0.04f, 0.15f);
            yield return SWait(1.2f);
            // roll call
            string[] calls = { "SACK COMPANY!", "STRAW COMPANY!", "CORPORAL PELL'S CAVALRY!" };
            for (int r = 0; r < 3 && !sceneSkip; r++)
            {
                Shout(calls[r], 1.3f, "footstep");
                yield return SWait(0.45f);
                for (int i = 0; i < Per; i++)
                {
                    var a = squad[r * Per + i];
                    if (r == 0) Ctx.Run(Hop(a, i * 0.07f, 0.11f));
                    else Ctx.Run(Salute(a, i * 0.07f, 0.5f));
                }
                Sfx.Play(r == 0 ? "hit_wood" : "lg_whirl", A.center, 0.6f);
                yield return SWait(1.1f);
            }
            // salute (everyone spins at once)
            Shout("PRESENT... ARMS!", 1.6f);
            yield return SWait(0.5f);
            foreach (var a in squad) Ctx.Run(Salute(a, 0f, 0.8f));
            Sfx.Play("lg_whirl", A.center);
            CameraFollow.KickMain(Vector2.up, 0.06f, 0.2f);
            yield return SWait(1.3f);
            // briefing
            Look(kingHome + Vector2.down * 0.9f);
            yield return Speech(
                "RECRUIT! I was built by the Warders to drill every soul who wanders into this yard. Four hundred years. I have NEVER been switched off.",
                "Three stations. Three YARDS. ONE: Blade Drill, right here. My soldiers SPIN. You do NOT get spun.",
                "TWO: Lantern Run, through that door. You carry a light through my ranks. You drop it, you go back. Like a FOOL.",
                "THREE: the FINAL EXAM, at the far end. Arithmetic. Then... live fire. My programming is very... thorough.",
                "THREE LIVES per station. A hit, a wrong answer, a hug from my soldiers - one life. Lose all three and you do that station AGAIN. From the TOP.",
                "One station per horn. Sound the HORN when you are ready, recruit! *CLICK* ...Morale is LOW.");
            Shout("COMPANY... DIS-MISSED!", 1.8f);
            yield return SWait(0.4f);
            foreach (var a in squad) Ctx.Run(Hop(a, 0f, 0.06f));
            yield return SWait(0.3f);
            yield return Dismiss(squad);
            yield return SWait(1.2f);
            yield return EndScene();
            Hints.Show("Sound the horn (T) to begin the drill.", 4f);
            idleGreetAt = Time.time + 25f;
        }

        // ------------------------------------------------------------------ SCENE 2: THE SERGEANT TAKES THE FIELD (first time at station 3)
        private IEnumerator SergeantScene()
        {
            Ctx.Flags.Set(SergeantFlag);
            ResetKing();
            BeginScene(kingHome + Vector2.down * 1.2f);
            var guard = new List<DrillActor>();
            Vector2 stage = kingHome + Vector2.down * 0.55f;                                       // / Burlap steps up to the lectern (Final Exam)
            // war horn announces the Sergeant, then a drum roll
            Sfx.Play("drill_warhorn", king.Pos);
            yield return SWait(1.4f);
            for (int b = 0; b < 3 && !sceneSkip; b++) { Sfx.Play("rf_war_drum", king.Pos, 0.5f + 0.2f * b); yield return SWait(0.55f - 0.12f * b); }
            Shout("HONOR GUARD! FORM... THE LINE!", 1.8f);
            // 2 honour rows of straw dummies: march down from the north edge, forming an aisle from Burlap to mid-yard
            const int Per = 5;
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < Per; i++)
                {
                    var slot = new Vector2(kingHome.x + side * 0.62f, kingHome.y - 0.45f - i * 0.5f);
                    var a = Soldier("straw", new Vector2(slot.x + side * 1.6f, A.yMax + 0.4f));
                    guard.Add(a);
                    Ctx.Run(March(a, slot, 2.2f, i * 0.16f));
                }
            for (int b = 0; b < 6 && !sceneSkip; b++) { Sfx.Play("footstep", A.center, 0.6f); yield return SWait(0.4f); }
            yield return SWait(0.5f);
            Shout("PRESENT... ARMS!", 1.4f, "hit_wood");
            for (int i = 0; i < guard.Count; i++) Ctx.Run(Salute(guard[i], (i % Per) * 0.06f, 0.9f));
            Sfx.Play("lg_whirl", king.Pos);
            yield return SWait(1.0f);
            // Burlap walks down the aisle to mid-yard
            king.Play("move", 12f);
            while ((king.Pos - stage).sqrMagnitude > 0.0004f && !sceneSkip)
            {
                king.Pos = Vector2.MoveTowards(king.Pos, stage, 1.3f * Time.deltaTime);
                Look(king.Pos + Vector2.up * 0.4f);
                if (Mathf.Repeat(Time.time, 0.42f) < Time.deltaTime) Sfx.Play("heavy_land", king.Pos, 0.35f);
                yield return null;
            }
            king.Pos = stage;
            Look(stage + Vector2.up * 0.45f);
            // stomp
            king.Play("dashend", 14f, false);
            yield return SWait(0.55f);
            Sfx.Play("duo_slam", king.Pos);
            CameraFollow.KickMain(Vector2.down, 0.1f, 0.35f);
            SpriteVfx.Spawn("duo_quake", king.Pos, 1.4f, Layer, Order - 1, false, 14f, false);
            var w = Shockwave.Spawn(king.Pos, 2.4f, 0.9f, 0f, Order - 1, new Color(1f, 0.8f, 0.5f, 0.9f));
            w.knockback = 0f;
            foreach (var g in guard) Ctx.Run(Hop(g, Random.Range(0.05f, 0.2f), 0.06f));
            yield return SWait(0.9f);
            king.Play("idle", 4f);
            yield return Speech(
                "You dodged my soldiers. You carried my lamps. Not bad... for a CIVILIAN.",
                "PROGRAM THREE: THE FINAL EXAM. I write a sum on my board. You STAND on the trapdoor with the answer.",
                "Wrong door? It opens. You FALL. No answer at all? Then we have a little TALK. Up close. And then you meet my BOOT.",
                "Pass that, and we move on to... the practical. Swords? Don't bother. I'm STUFFED. ATTEN-TION!");
            // boss name card (drawn by the HUD, like BossIntro)
            BossIntro.CardName = "SGT. BURLAP";
            BossIntro.CardSub = "DRILL AUTOMATON OF THE WARDERS";
            BossIntro.CardSprite = king.Sr != null ? king.Sr.sprite : null;
            BossIntro.CardPortrait = "";
            BossIntro.CardColor = new Color(0.55f, 0.18f, 0.16f);
            BossIntro.CardStart = Time.time; BossIntro.CardLength = 2.8f;
            Sfx.Play2D("boss_intro");
            yield return SWait(2.6f);
            if (sceneSkip && BossIntro.CardActive) BossIntro.CardLength = Mathf.Min(BossIntro.CardLength, Time.time - BossIntro.CardStart + 0.5f);
            Shout("HONOR GUARD... DIS-MISSED! Recruit - BRACE YOURSELF!", 2f);
            yield return Dismiss(guard);
            yield return SWait(0.8f);
            yield return EndScene();
        }
    }
}
