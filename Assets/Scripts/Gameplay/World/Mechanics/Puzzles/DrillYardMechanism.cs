using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// drill_yard — BURLAP'S PROVING GROUNDS (own scene Map4d_DrillYard, map4d_drill_yard.tmx): 3 SEPARATE YARDS linked by doors,
    /// EACH YARD = ONE STATION = one drill_yard object with `station`:
    ///   0 BLADE DRILL  (dodge)        — training dummies SPIN across the yard in formation, telegraphed by arrow lines. Survive 45 s straight.
    ///   1 LANTERN RUN  (carry)        — carry 3 lanterns from the pedestal (left) to the rack (right) across 4 lanes of dummies running VERTICALLY.
    ///                                   Holding a lantern = no rolling. 75 s.
    ///   2 FINAL EXAM   (quiz + shoot) — 5 maths questions on trapdoors (time up while not on a door = Burlap interrogates then rams, guaranteed hit);
    ///                                   LIVE FIRE: haul fire pots from crates, load mortars, time the aim ring; hits only while Burlap is stuck head-first in hay.
    /// EACH STATION HAS 3 LIVES (`morale`, default 3) — getting hit / wrong answer / rammed = lose 1; out of lives = REPLAY THE WHOLE STATION
    /// (blow the horn). Passing a station -> flag `sets` (drill_s1 / drill_s2 / drill_passed) -> the door to the next yard opens (unlock_when),
    /// Burlap marches to the next yard. Station 3 done -> key ring on the pedestal (`key_at`, pickup drill_key needs drill_passed).
    /// Properties: station (0..2), sets, requires (flags needed before the horn works), morale, seal_gate (gate behind Ren — closed while playing),
    ///   king_at, exit_at (where Burlap leaves after station 0/1), key_at, key_id, lantern_at, rack_at (station 1), quest (name on the quest board).
    /// Camera: FIXED frame around the whole yard (size ~2.1 — near normal, not zoomed out) while Ren is inside; compact HUD at the top edge.
    /// Files: .cs (flow, lives, camera, Burlap lines) · .Blades (station 0) · .Lantern (station 1) · .Exam + .Interrogate (station 2 quiz)
    ///        · .Artillery (station 2 Live Fire) · .Cutscenes · .Guide (quest board). HUD reads DrillBoard. Music: SceneMusic (boss_burlap while DrillBoard.Active).
    /// </summary>
    public sealed partial class DrillYardMechanism : Mechanism
    {
        private enum Phase { Idle, Running, Done }
        private Phase phase = Phase.Idle;
        private readonly List<DrillActor> actors = new List<DrillActor>();
        private readonly List<Coroutine> routines = new List<Coroutine>();
        private DrillActor king;
        private Vector2 kingHome, keyAt;
        private SpriteRenderer keyring;
        private Coroutine main;
        private int station, stationHits;
        /// <summary>Lives left in the current run.</summary>
        private int lives = -1;
        private bool passed, aborted, stationFailed, wasInside, kingGone, kingLeaving;
        /// <summary>Live play (after "GO!", not the intro) -> Burlap's lines go to ONE LINE on the HUD board instead of the bottom dialogue box.</summary>
        private bool live;
        /// <summary>How-to-play tips already shown (each tip only the first time; replaying a station does not repeat them).</summary>
        private readonly HashSet<string> tipsSeen = new HashSet<string>();
        private float hitCooldown, nextLine, idleGreetAt, readyAt, nextDodgeSay;
        private PlayerCombat combat;

        private static readonly string[] Names = { "BLADE DRILL", "LANTERN RUN", "FINAL EXAM" };
        private static readonly Color Gold = new Color(1f, 0.85f, 0.45f), Red = new Color(1f, 0.38f, 0.32f), Green = new Color(0.55f, 1f, 0.6f), Sky = new Color(0.6f, 0.85f, 1f);

        public bool Passed => passed;
        public bool Busy => phase == Phase.Running;
        public bool Ready => phase == Phase.Idle && !passed && !inScene && Time.time >= readyAt && Ctx.Flags.HasAll(Str("requires", ""));
        /// <summary>Station of this yard (0-based).</summary>
        public int Station => station;
        private Rect A => Rect;
        private Vector2 PlayerFeet => Ctx.Player != null ? (Vector2)Ctx.Player.position : A.center;
        private int MoraleMax => Mathf.Clamp(Int("morale", 3), 1, 5);
        private string Sets => Str("sets", station == 0 ? "drill_s1" : station == 1 ? "drill_s2" : "drill_passed");

        public override void Init()
        {
            station = Mathf.Clamp(Int("station", 0), 0, 2);
            DrillBoard.Active = false;
            kingHome = Ctx.TilePoint(Str("king_at", ""), new Vector2(A.center.x, A.yMax - 0.55f));
            keyAt = Ctx.TilePoint(Str("key_at", ""), new Vector2(A.center.x, A.yMax - 0.2f));
            king = new DrillActor("king", kingHome, Ctx.Host.transform, Ctx.Player) { Harmless = true, Radius = 0.34f, FaceByVel = false, EdgeAlpha = 0f };
            king.Tick(0f);
            keyring = TrapArt.MakeRenderer(Ctx.Host.transform, "Drill Keyring", keyAt, Ctx.Player, 1);
            keyring.sprite = WardenStatueMechanism.Load("VFX/props/drill_keyring");
            keyring.enabled = false;
            combat = Ctx.Player != null ? Ctx.Player.GetComponent<PlayerCombat>() : null;
            if (station == 1) InitLantern();
            if (station == 2) InitExam();                                              // trapdoors + blackboard + mortars / hay / ammo crates
            dismissed = Ctx.Flags.HasAll("key_" + Str("key_id", "drill_key"));           // key already taken -> do not shout again
            OnFlagsChanged();
            kingGone = passed && station < 2;                                          // station already passed (scene reloaded): Burlap is no longer in this yard
            if (kingGone) HideKing();
        }

        /// <summary>Burlap has moved to the next yard -> turn his sprite OFF completely (fading alone sometimes left a ghost at the door).</summary>
        private void HideKing()
        {
            if (king == null) return;
            king.SetAlpha(0f); king.EdgeAlpha = 0f; king.Whirl(false);
            if (king.Sr != null) king.Sr.enabled = false;
        }

        public override void OnFlagsChanged()
        {
            if (!passed && Ctx.Flags.HasAll(Sets)) { passed = true; phase = Phase.Done; }
            bool keyTaken = Ctx.Flags.HasAll("key_" + Str("key_id", "drill_key"));
            if (keyring != null) keyring.enabled = station == 2 && passed && !keyTaken;
            // key just picked up -> Burlap dismisses the squad and points to the entrance (quest board points to to_store at the right edge)
            if (station == 2 && passed && keyTaken && !dismissed) { dismissed = true; Ctx.Run(Dismissed()); }
        }

        /// <summary>Dismissal already shouted (or the scene reloaded with the key in hand -> no shout).</summary>
        private bool dismissed;

        private IEnumerator Dismissed()
        {
            yield return new WaitForSeconds(0.8f);
            Bark("DISMISSED, recruit! Out the way you came in - the STORE door, far end! Every gate's open. MARCH!", 4.5f, true);
            Sfx.Play("rf_war_drum", A.center, 0.5f);
        }

        // ------------------------------------------------------------------ start (horn call)
        public void Begin()
        {
            if (!Ready) return;
            phase = Phase.Running; aborted = false; stationFailed = false;
            Seal(true);                                                                // blowing the horn closes the gate behind Ren
            main = Ctx.Run(Drill());
        }

        /// <summary>ONE RUN = THE WHOLE STATION, 3 lives. Out of lives / time -> fail -> blow the horn to replay FROM THE START of the station.</summary>
        private IEnumerator Drill()
        {
            stationHits = 0;
            yield return Wait(1.1f);                                                   // let the horn ring out first
            if (aborted) yield break;
            bool scene = station == 2 && !Ctx.Flags.HasAll(SergeantFlag);
            if (scene) { DrillBoard.Active = false; yield return SergeantScene(); if (aborted) yield break; }
            DrillBoard.Begin(station + 1, Names[station], MoraleMax);
            lives = MoraleMax;
            Sfx.Play("rf_war_drum", A.center);
            Bark(StationLine(station), 4.2f, true);
            // count down 3 - 2 - 1 (drum on each beat) before the station starts
            for (int k = 3; k >= 1 && !aborted; k--)
            {
                DrillBoard.Goal = "GET READY  " + k;
                Sfx.Play("footstep", A.center, 0.7f);
                yield return Wait(1f);
            }
            if (aborted) yield break;
            DrillBoard.Finish("GO!", Gold);
            Sfx.Play("rf_war_drum", A.center, 0.9f);
            SetLive(true);
            switch (station)
            {
                case 0: yield return Blades(); break;
                case 1: yield return Lanterns(); break;
                default: yield return FinalExam(); break;
            }
            SetLive(false);
            StopRoutines();
            if (aborted) yield break;
            if (stationFailed) { Fail(); yield break; }
            Pass();
        }

        private static string StationLine(int s)
        {
            switch (s)
            {
                case 0: return "STATION ONE! BLADE DRILL! My boys SPIN! RED ARROWS = INCOMING, green arrows = the GAP! THREE hits and you start OVER!";
                case 1: return "STATION TWO! LANTERN RUN! Three lamps, pedestal to rack! NO ROLLING with a lit lamp! Touch a soldier: light OUT, one life GONE!";
                default: return "STATION THREE! THE FINAL EXAM! Stand on the RIGHT ANSWER! Wrong door - you DROP! No answer - you meet ME!";
            }
        }

        private void Start(IEnumerator r) => routines.Add(Ctx.Run(r));

        private void StopRoutines()
        {
            foreach (var c in routines) if (c != null) Ctx.Host.StopCoroutine(c);
            routines.Clear();
        }

        private IEnumerator Wait(float s)
        {
            float end = Time.time + s;
            while (Time.time < end && !aborted && !stationFailed) yield return null;
        }

        // ------------------------------------------------------------------ finish
        private void Pass()
        {
            ClearField();
            passed = true; phase = Phase.Done;
            Seal(false);
            Sfx.Play("puzzle_solved", A.center);
            Ctx.Flags.Set(Sets);
            OnFlagsChanged();
            if (station < 2)
            {
                DrillBoard.Finish("STATION " + (station + 1) + " CLEARED", Green);
                Bark((stationHits == 0 ? Pick("NOT ONE SCRATCH?! *WHIRR* RECALIBRATING EXPECTATIONS.", "Flawless! I'm writing this down! In PENCIL!")
                                       : Pick("Passable! PASSABLE!", "You live! Barely! That's the regulation amount!"))
                     + " STATION " + (station + 2) + " is through THAT door. Double time!", 4.4f, true);
                Ctx.Run(KingLeaves());
            }
            else
            {
                string rank = stationHits == 0 ? "S" : stationHits == 1 ? "A" : "B";
                DrillBoard.Finish("FINAL EXAM PASSED - RANK " + rank, Gold);
                Bark(rank == "S" ? "...RANK S?! Nobody gets S. I don't even HAVE an S stamp. Take the key. Take my HAT. No - just the key."
                     : rank == "A" ? "Rank A. ...Fine soldiering, recruit. I am NOT crying. That's hay. The key is on the pedestal."
                     : "Rank B. Bruised! Battered! ...Adequate. The key is yours. Then out of my yard before I get sentimental.", 5.5f, true);
            }
            Ctx.Run(HideBoard(4.5f));
        }

        /// <summary>Station 0 / 1 passed: Burlap marches to the next yard's door (`exit_at`) and disappears.
        /// He stays visible the whole way (even if Ren already ran ahead); fades with SetAlpha (does not fight Animate over EdgeAlpha),
        /// then his sprite is turned OFF (HideKing) — no ghost left at the door.</summary>
        private IEnumerator KingLeaves()
        {
            kingLeaving = true;
            yield return new WaitForSeconds(2.2f);
            Vector2 to = Ctx.TilePoint(Str("exit_at", ""), new Vector2(A.xMax + 0.6f, A.center.y));
            king.Play("move", 14f);
            float end = Time.time + 6f;
            while ((king.Pos - to).sqrMagnitude > 0.0004f && Time.time < end && phase == Phase.Done)
            {
                king.Sr.flipX = to.x < king.Pos.x;
                king.Pos = Vector2.MoveTowards(king.Pos, to, 2.2f * Time.deltaTime);
                yield return null;
            }
            for (float t = 0f; t < 0.4f; t += Time.deltaTime) { king.SetAlpha(1f - t / 0.4f); yield return null; }
            kingGone = true; kingLeaving = false;
            HideKing();
        }

        /// <summary>Station failed -> open the gate; blow the horn to replay FROM THE START (full 3 lives).</summary>
        private void Fail()
        {
            ClearField();
            phase = Phase.Idle; readyAt = Time.time + 2f;
            DrillBoard.Finish("STATION " + (station + 1) + " FAILED", Red);
            Sfx.Play("ui_back", A.center);
            Seal(false);
            Bark(failWhy + " *BZZT* Sound the horn when you're ready - STATION " + (station + 1) + " again, from the TOP. Three lives!", 5f, true);
            Ctx.Run(HideBoard(4.5f));
        }

        private string failWhy = "PATHETIC!";

        private IEnumerator HideBoard(float s)
        {
            yield return new WaitForSeconds(s);
            if (phase != Phase.Running) DrillBoard.Active = false;
        }

        private void Abort(string why)
        {
            if (phase != Phase.Running) return;
            aborted = true;
            SetLive(false);
            if (main != null) Ctx.Host.StopCoroutine(main);
            StopRoutines();
            ClearField();
            phase = Phase.Idle; readyAt = Time.time + 2f;
            Seal(false);
            DrillBoard.Finish("DRILL CANCELLED", Red);
            if (!string.IsNullOrEmpty(why)) Bark(why, 4f, true);
            Ctx.Run(HideBoard(3f));
        }

        private void ClearField()
        {
            foreach (var a in actors) a.Destroy();
            actors.Clear();
            if (station == 1) ResetLantern(stationFailed || aborted);                   // station passed: hung lanterns stay on the rack
            ResetKing();
            if (questOwned) { QuestGuide.Clear(); questOwned = false; }
            Guide(null);
            DrillBoard.Objective = "";
            DrillBoard.Command = "";
            DrillBoard.TrackFrac = -1f; DrillBoard.TimeFrac = -1f; DrillBoard.TimeText = "";
        }

        // ------------------------------------------------------------------ gate behind Ren
        private bool sealedIn;

        /// <summary>Close / open `seal_gate` (front yard gate / way back) while a station is running.</summary>
        private void Seal(bool on)
        {
            if (sealedIn == on || Ctx.Director == null) return;
            string g = Str("seal_gate", "");
            if (g.Length == 0 || !Ctx.Director.SetGateClosed(g, on)) return;
            sealedIn = on;
            if (on) CameraFollow.KickMain(Vector2.right, 0.05f, 0.2f);
        }

        // ----------------------------------------------------------------- fixed yard camera
        private bool camOwned;
        /// <summary>Top share of the screen reserved for the compact HUD board (fraction of height).</summary>
        private const float HudBand = 0.075f;

        /// <summary>Ren inside the yard (and not in a cutscene) -> the camera stays still, framing the yard from the top wall to the bottom wall foot;
        /// frame size just big enough (never smaller than the map's normal size, never far out). Leaving the yard -> camera follows Ren again.</summary>
        private void ArenaCam(bool inside)
        {
            var cf = Ctx.Camera != null ? Ctx.Camera : (Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null);
            if (cf == null) return;
            bool want = inside && !inScene;
            if (!want)
            {
                if (camOwned) { camOwned = false; if (!inScene) cf.Release(); }
                return;
            }
            var cam = Camera.main;
            float aspect = cam != null ? Mathf.Max(0.5f, cam.aspect) : 16f / 9f;
            float top = A.yMax + 0.36f, bottom = A.yMin - 0.42f;                      // top wall edge .. part of the bottom wall foot
            float size = Mathf.Max(Float("cam_size", 2f), (top - bottom) / (2f - 2f * HudBand), (A.width + 0.7f) * 0.5f / aspect);
            var c = new Vector2(A.center.x, bottom + size);
            cf.Focus(c, size);
            camOwned = true;
        }

        /// <summary>Station failed (out of lives / time) — the station loop exits on its own.</summary>
        private void FailStation(string why)
        {
            if (stationFailed) return;
            failWhy = why;
            stationFailed = true;
        }

        // ------------------------------------------------------------------ every frame
        public override void Animate()
        {
            float dt = Time.deltaTime;
            bool inside = A.Contains(PlayerFeet);
            bool near = new Rect(A.xMin - 1.2f, A.yMin - 0.6f, A.width + 2.4f, A.height + 1.2f).Contains(PlayerFeet);
            if (king != null)
            {
                // Burlap only shows in the yard Ren stands in (3 yards = 3 copies); once he has moved on, stop.
                // While marching to the next yard -> always visible up to the door; once gone -> sprite off, no more Tick.
                if (kingGone) { if (king.Sr != null && king.Sr.enabled) HideKing(); }
                else
                {
                    bool show = kingLeaving || near || phase == Phase.Running || inScene;
                    king.EdgeAlpha = Mathf.MoveTowards(king.EdgeAlpha, show ? 1f : 0f, dt * 3f);
                    king.Tick(dt);
                }
            }
            for (int i = actors.Count - 1; i >= 0; i--)
            {
                var a = actors[i];
                float m = Mathf.Min(Mathf.Min(a.Pos.x - A.xMin, A.xMax - a.Pos.x), Mathf.Min(a.Pos.y - A.yMin + 0.2f, A.yMax - a.Pos.y));
                a.EdgeAlpha = Mathf.Clamp01((m + 0.35f) / 0.45f);
                a.Tick(dt);
            }
            if (station == 1) AnimateLantern(dt);
            if (station == 2) AnimateExam(dt);
            if (SceneTransition.Busy) return;                                          // scene is transitioning: no acting / no logic yet
            ArenaCam(inside || (phase == Phase.Running && near));
            UpdateGuide(inside);
            if (phase == Phase.Running)
            {
                if (Ctx.PlayerHealth == null || Ctx.PlayerHealth.IsDead) { Abort(null); return; }
                if (!near && !frozen && !falling) { Abort("DESERTER! Get back here, recruit! ...Fine. Drill CANCELLED. My stuffing is just... loose."); return; }
                Collide();
                SpinAudio();
                for (int i = actors.Count - 1; i >= 0; i--)
                {
                    var a = actors[i];
                    if (a.Pos.x < A.xMin - 1.8f || a.Pos.x > A.xMax + 1.8f || a.Pos.y < A.yMin - 1.8f || a.Pos.y > A.yMax + 1.8f) { a.Destroy(); actors.RemoveAt(i); }
                }
            }
            else if (inside && !wasInside && station == 0 && !passed && !inScene && !Ctx.Flags.HasAll(MusterFlag))
                Ctx.Run(Muster());                                                     // first time in yard 1 -> parade inspection
            else if (inside && !wasInside && !inScene && !kingGone && Time.time >= idleGreetAt && (station == 0 ? Ctx.Flags.HasAll(MusterFlag) : true))
            {
                idleGreetAt = Time.time + 25f;
                Bark(passed ? (station == 2 ? "You PASSED. Take the key and go. Shoo. Write home." : "This station is DONE! Onward, recruit!")
                     : station == 1 && !Ctx.Flags.HasAll(MusterFlag + "_s2") ? Arrive(1)
                     : station == 2 && !Ctx.Flags.HasAll(MusterFlag + "_s3") ? Arrive(2)
                     : "RECRUIT! Sound the HORN when you're ready for STATION " + (station + 1) + "!", 4f, true);
            }
            wasInside = inside;
        }

        /// <summary>First time in yard 2 / 3: Burlap is already waiting (after marching through the door).</summary>
        private string Arrive(int s)
        {
            Ctx.Flags.Set(MusterFlag + (s == 1 ? "_s2" : "_s3"));
            return s == 1 ? "STATION TWO! Lamps! Fire! Soldiers! ...Mostly soldiers. Sound the HORN, recruit!"
                          : "STATION THREE. The FINAL EXAM. I've sharpened my chalk AND my temper. Sound the HORN!";
        }

        // ----------------------------------------------------------------- spinning dummy sound
        private readonly object spinKeyA = new object(), spinKeyB = new object();
        private float nextWhoosh;

        private void SpinAudio()
        {
            DrillActor a1 = null, a2 = null;
            float d1 = float.MaxValue, d2 = float.MaxValue;
            Vector2 p = PlayerFeet;
            foreach (var a in actors)
            {
                if (a.Harmless || !a.Spinning || a.EdgeAlpha < 0.3f || a.Sr == null) continue;
                float d = (a.Pos - p).sqrMagnitude;
                if (d < d1) { d2 = d1; a2 = a1; d1 = d; a1 = a; }
                else if (d < d2) { d2 = d; a2 = a; }
            }
            if (a1 != null && d1 < 12f) Sfx.Loop(spinKeyA, "drill_spin", a1.Sr.transform, 0.9f);
            if (a2 != null && d2 < 12f) Sfx.Loop(spinKeyB, "drill_spin", a2.Sr.transform, 0.55f);
            if (a1 != null && d1 < 0.49f && Time.time >= nextWhoosh)
            {
                nextWhoosh = Time.time + 0.35f;
                Sfx.Play("drill_whoosh", a1.Pos, 0.85f);
            }
        }

        /// <summary>The player is rolling (invulnerable because of the roll, not because of a recent hit).</summary>
        private bool Rolling => combat != null && Time.time - combat.LastRollTime < 0.5f && Ctx.PlayerHealth != null && Ctx.PlayerHealth.Invulnerable;

        private void Collide()
        {
            if (frozen || falling) return;
            Vector2 p = PlayerFeet + Vector2.up * 0.12f;
            for (int i = 0; i < actors.Count + 1; i++)
            {
                var a = i < actors.Count ? actors[i] : king;
                if (a == null || a.Harmless || a.EdgeAlpha < 0.5f) continue;
                float r = a.Radius + 0.12f;
                if ((p - a.Body).sqrMagnitude > r * r) continue;
                Struck(a.Body, a == king ? 6f : 4f, a);
            }
        }

        /// <summary>Ren hit by something in the yard: rolling through = CLEAN DODGE; otherwise lose 1 LIFE.</summary>
        private void Struck(Vector2 from, float damage, DrillActor who, bool rollDodges = true)
        {
            if (Time.time < hitCooldown || phase != Phase.Running) return;
            if (rollDodges && Rolling)
            {
                if (who != null && who.Credited) return;
                if (who != null) who.Credited = true;
                if (Time.time >= nextDodgeSay)
                {
                    nextDodgeSay = Time.time + 0.5f;
                    DrillBoard.Say("CLEAN DODGE!", Gold);
                    Sfx.Play("parry", PlayerFeet, 0.45f);
                    if (Time.time >= nextLine && Random.value < 0.3f) Bark(Pick("Was that a ROLL? Do it AGAIN!", "...Not bad. NOT GOOD EITHER!", "Show-off! I LOVE it! I mean - SILENCE!"), 2.2f, false);
                }
                return;
            }
            if (Ctx.PlayerHealth == null) return;
            hitCooldown = Time.time + 1.1f;
            Vector2 p = PlayerFeet;
            Vector2 dir = (p - from).sqrMagnitude > 0.0001f ? (p - from).normalized : Vector2.down;
            DrillFx.Impact(Ctx.Host, p, dir, Layer, Order);
            Sfx.Play("blunt_strike", p);
            Sfx.Play("hit_wood", p, 0.6f);
            if (who != null) who.Flash(new Color(1f, 0.9f, 0.6f), 0.25f);
            if (!Ctx.PlayerHealth.Invulnerable)
            {
                if (rollDodges) Ctx.HurtPlayer(damage, dir, 6f);
                else TrapArt.HurtPlayerCrush(Ctx, damage, dir, 6f);
            }
            if (carrying) Snuff(p);
            if (station == 2) DropShell(p);
            LoseLife("HIT!  LIFE -1");
            if (stationFailed) return;
            if (Time.time >= nextLine) Bark(Pick("Did I say HUG the soldier?!", "That's going in your FILE!", "OW! Mind the STUFFING!",
                                                 "My grandmother dodges better, and she's a SCARECROW!", "Ten demerits! TWENTY!"), 2.2f, false);
        }

        /// <summary>Lose 1 life (hit, falling through a trapdoor, rammed during interrogation, own shell). Out of lives = station failed.</summary>
        private void LoseLife(string pop)
        {
            stationHits++;
            DrillBoard.LoseMorale();
            lives = DrillBoard.Morale;
            DrillBoard.Say(pop, Red);
            if (DrillBoard.Morale <= 0) FailStation(Pick("OUT OF LIVES!", "DOWN AND OUT!", "FLATTENED!"));
        }

        private int Layer => king != null && king.Sr != null ? king.Sr.sortingLayerID : 0;
        private int Order => king != null && king.Sr != null ? king.Sr.sortingOrder : 2;

        // ------------------------------------------------------------------ the sergeant's lines
        /// <summary>Enter / leave live play. Entering -> close any open dialogue box (intro line) so it does not hide the dodge area.</summary>
        private void SetLive(bool on)
        {
            if (live == on) return;
            live = on;
            if (on && Subtitles.Visible && !DialogueBox.Active) Subtitles.Clear();
        }

        /// <summary>How-to-play tip — each tip only the first time (not on replays).</summary>
        private void Tip(string text, float seconds)
        {
            if (tipsSeen.Add(text)) Hints.Show(text, seconds);
        }

        private void Bark(string text, float seconds, bool force)
        {
            if (!force && Time.time < nextLine) return;
            if (king == null || king.EdgeAlpha < 0.05f && phase != Phase.Running && !inScene) return;
            // During live play the shout becomes ONE LINE on the HUD board; flavour quips (force = false) are dropped entirely.
            // The dialogue box is only used for the intro, station pass / fail, cutscenes and interrogation.
            if (live && !frozen && !inScene)
            {
                if (!force) return;
                nextLine = Time.time + 1.2f;
                DrillBoard.Yell(text, Mathf.Clamp(seconds, 1.8f, 3.2f));
                return;
            }
            nextLine = Time.time + Mathf.Max(2.5f, seconds * 0.8f);
            var b = SpeechBubble.Show(king.Pos + Vector2.up * 1.1f, text, seconds, Gold, "Sgt. Burlap");
            if (b != null && king.Sr != null) b.Follow(king.Sr.transform);
        }

        private static string Pick(params string[] s) => s[Random.Range(0, s.Length)];

        internal override void DebugSolve()
        {
            if (passed) return;
            passed = true; phase = Phase.Done;
            Ctx.Flags.Set(Sets);
            OnFlagsChanged();
        }
    }
}
