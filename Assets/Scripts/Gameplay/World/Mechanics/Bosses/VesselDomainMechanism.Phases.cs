using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
using SuperTiled2Unity;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 5 ELEMENT PHASES.
    ///   1 HOLLOW (physical, pale grey Vessel) · 2 CINDER (fire) · 3 RIME (ice) · 4 TEMPEST (storm) · 5 UMBRA (shadow) -> ENDING (Last Grasp, portal, coming-soon screen).
    /// VESSEL bar: one HP block per phase (`PhaseHits` sword hits), ONLY reduced while a hand LIES on the floor after Slam / Triple Slam (.Physical.cs).
    /// Phase change: the Vessel slumps, the old phase's energy pylon breaks; Absalom appears beside the new phase's pylon and channels it ->
    ///   the WHOLE MAP TINT changes (Tilemap.color + ZoneAmbience.ColorMul + nebula VoidDecorMechanism.Tint, ~2 s blend) + the Vessel swaps to that element's sprites.
    /// Death: replay the current phase (flags vessel_p1..p5 = cleared). Phase cleared: flasks refilled.
    /// Each phase has its own move table (PhaseMoves): P1 here, P2 .Fire.cs, P3 .Ice.cs, P4 .Storm.cs, P5 .Shadow.cs.
    /// Called by: VesselDomainMechanism.Animate -> TickFight every frame; TickFight starts PhaseLoop when the fight is idle.
    /// </summary>
    public sealed partial class VesselDomainMechanism
    {
        public const string NamedFlag = "vessel_named", SlainFlag = "vessel_slain", FledFlag = "vessel_fled";
        private static readonly string[] Elements = { "fire", "ice", "storm", "umbral" };              // energy pylons (VoidIsleMechanism)
        private static readonly string[] PhaseName = { "HOLLOW", "CINDER", "RIME", "TEMPEST", "UMBRA" };
        private static readonly string[] PhaseRoman = { "I", "II", "III", "IV", "V" };
        private static readonly string[] PhaseEl = { "pale", "fire", "ice", "storm", "umbral" };       // Vessel sprite set (Resources/VFX/vessel/*_awake_<el>)
        private static readonly Color[] PhaseCol = { new Color(0.86f, 0.88f, 0.92f), new Color(1f, 0.5f, 0.22f), new Color(0.6f, 0.9f, 1f),
                                                     new Color(1f, 0.88f, 0.3f), new Color(0.75f, 0.4f, 1f) };
        private static readonly Color[] TileTint = { new Color(0.86f, 0.87f, 0.9f), new Color(1f, 0.55f, 0.42f), new Color(0.66f, 0.86f, 1f),
                                                     new Color(1f, 0.92f, 0.58f), Color.white };
        private static readonly Color[] LightMul = { new Color(1.25f, 1.4f, 0.95f), new Color(1.8f, 0.9f, 0.45f), new Color(0.9f, 1.55f, 1.25f),
                                                     new Color(1.85f, 1.85f, 0.6f), new Color(0.72f, 0.6f, 0.92f) };   // UMBRA goes fully dark
        private static readonly Color[] DecorTint = { new Color(0.8f, 0.8f, 0.86f), new Color(1f, 0.45f, 0.3f), new Color(0.55f, 0.85f, 1f),
                                                      new Color(1f, 0.9f, 0.45f), Color.white };
        private static readonly string[] InfuseLine = { "", "Burn for me, Vessel.", "Be still. Be cold.", "Sing with the storm.", "Be NOTHING." };
        private static readonly string[] PhaseHint =
        {
            "A slammed hand stays down - strike it. If a searchlight finds you, the Vessel grabs you - strike its hand to break free.",
            "Follow the gaps in the magma. Kill the imps. A searchlight that finds you calls down firebombs.",
            "The floor turns to ice. A searchlight that finds you freezes you solid - mash attack to break out before the icicle falls.",
            "Storm rods drink lightning - stay near one. A searchlight charges you: touch a rod to ground it. A full rod bursts into a fallen hand.",
            "Cut the anvil's chain when it glows blue. Strike the ink sigil to cut its tether. Strike orbs and your own shadow.",
        };
        private static readonly string[] Relics = { "EYE", "SPINE", "EARS" };
        private static readonly Vector2[] RelicSeat = { new Vector2(-35f, -150f), new Vector2(0f, 0f), new Vector2(0f, -205f) };   // px on the Vessel body
        private static string PhaseFlag(int p) => "vessel_p" + (p + 1);

        private enum ActState { Idle, Playing, Rest }
        private ActState actState = ActState.Idle;
        private int phase;
        private float vhp = 1f, restartAt, jawUntil;
        private bool aborted, pylonsSynced;
        private Coroutine actMain;
        private readonly List<(Transform t, Vector2 home, float phase)> freed = new List<(Transform, Vector2, float)>();

        private Color PhaseColor => PhaseCol[Mathf.Clamp(phase, 0, 4)];
        // more hits needed + each time a hand lies down it can lose at most `window_cap` (34 %) of the phase HP
        private float PhaseHits(int p) => new[] { 26f, 30f, 32f, 34f, 38f }[Mathf.Clamp(p, 0, 4)] * Float("hits_mul", 1f);
        private static readonly Color[] GrayTint = { new Color(0.84f, 0.86f, 0.9f), new Color(1f, 0.5f, 0.34f), new Color(0.56f, 0.82f, 1f),
                                                     new Color(1f, 0.88f, 0.48f), new Color(0.8f, 0.6f, 1f) };

        private int NextPhase()
        {
            for (int p = 0; p < 5; p++) if (!Ctx.Flags.HasAll(PhaseFlag(p))) return p;
            return 5;
        }

        /// <summary>Vessel sprite set on map load: the upcoming phase (not awake yet = pale grey for the awakening scene).</summary>
        private string StartElement() => PhaseEl[Mathf.Min(NextPhase(), 4)];

        // ------------------------------------------------------------------ every frame (called from Animate)
        private void TickFight(float dt)
        {
            SyncPylons();
            TrackVelocity(dt);
            TickBurn();                                                                // lava / fire burns
            TickIce(dt);                                                               // ice floor, ice fence, snowfall
            TickStorm(dt);                                                             // distant lightning
            TickLight(dt);                                                             // watchtower searchlights
            TickUmbra(dt);                                                             // dark orbs, Eclipse
            TickPortal();                                                              // portal Absalom left open
            TickFreed();
            TickTone(dt);
            TrackLungeReach(); TickRework(dt);                                                         // head dip capped at the map wall (.Pose.cs) + element moves (.Rework.cs)
            TickMusic();
            if (rig != null)                                                           // mouth moves (jawHold) / eye moves (lungeGoal) -> LUNGE down onto the arena
                rig.Lunge = Mathf.MoveTowards(rig.Lunge, actState == ActState.Playing ? Mathf.Max(lungeGoal, jawHold >= 0f ? 1f : 0f) : 0f, dt * 1.8f);
            if (rig != null && actState == ActState.Playing)
                rig.JawOpen = Mathf.MoveTowards(rig.JawOpen, jawHold >= 0f ? jawHold : Time.time < jawUntil ? 0.4f : 0f, dt * 2.5f);   // jawHold = breath / spit
            var renHp = Ctx.PlayerHealth;
            if (actState == ActState.Playing)
            {
                if (renHp == null || renHp.IsDead) { AbortAct(); return; }
                TickStomp(dt);
                if (grabbing) HoldRen();
                DrillBoard.GoalFrac = vhp;
                DrillBoard.TrackFrac = vhp;
                DrillBoard.Goal = "VESSEL " + Mathf.CeilToInt(vhp * 100f) + "%";
                return;
            }
            if (actState == ActState.Idle && main == null && Ctx.Flags.HasAll(AwakeFlag) && !Ctx.Flags.HasAll(DoneFlag)
                && renHp != null && !renHp.IsDead && Ctx.Director != null && !Ctx.Director.Respawning
                && !CutsceneState.Active && !SceneTransition.Busy && !DialogueBox.Active && Time.time >= restartAt)
                actMain = Ctx.Run(PhaseLoop());
        }

        // ------------------------------------------------------------------ one phase loop
        private IEnumerator PhaseLoop()
        {
            aborted = false;
            int p = NextPhase();
            if (p >= 5) yield break;
            MusicStart(2f);                                                             // re-entering the domain / replaying a phase (keep music if already playing)
            if (p == 4 && Ctx.Flags.HasAll(GraspFlag))                                 // died during LAST GRASP -> replay from LAST GRASP
            {
                rig.SetElement(PhaseEl[4]);
                yield return Finale();
                yield break;
            }
            actState = ActState.Playing;
            phase = p;
            vhp = 1f;
            HookStruck();
            BeginBoard(p);
            yield return PhaseIntro(p);
            int last = -1, sinceSlam = 0;
            while (!aborted && vhp > 0f)
            {
                float pressure = Mathf.Clamp(p * 0.45f + (1f - vhp) * 0.6f, 0f, 2f);
                int st = StompSide();
                if (st != 0 && HandFree(st)) { yield return Stomp(st); yield return Wait(0.45f); continue; }
                if (spottedReady) { spottedReady = false; yield return Spotted(); yield return Wait(0.45f); continue; }   // searchlight caught Ren (Phase 1)
                var moves = Moves(p);
                int k = PickMove(moves, last, sinceSlam);
                if (k < 0) { yield return null; continue; }
                last = k;
                sinceSlam = moves[k].slam ? 0 : sinceSlam + 1;
                jawUntil = Time.time + 0.5f;
                yield return moves[k].run(pressure);
                if (vhp <= 0f) break;
                yield return WaitOrStomp(Mathf.Lerp(1.5f, 0.85f, pressure * 0.5f));
            }
            if (aborted) yield break;
            yield return PhaseFalls(p);
        }

        private struct Move
        {
            public string name;
            public float w;
            public bool slam;                                                           // move that leaves a hand lying down (counter window)
            public System.Func<float, IEnumerator> run;
        }

        /// <summary>Move table of phase `p` (each phase file defines its own; Phase 1 is the default).</summary>
        private Move[] Moves(int p)
        {
            if (p == 1) return FireMoves();                                            // V2
            if (p == 2) return IceMoves();                                             // V3
            if (p == 3) return StormMoves();                                           // V4
            if (p == 4) return UmbraMoves();                                           // V5
            return new[]
            {
                new Move { name = "SLAM", w = 2f, slam = true, run = pr => Slam(Feet.x < chest.x ? -1 : 1, pr) },
                new Move { name = "CLAP", w = 1.4f, run = Clap },
                new Move { name = "RAKE", w = 1.3f, run = Rake },
                new Move { name = "TRIPLE SLAM", w = 1.4f, slam = true, run = TripleSlam },
                new Move { name = "GRAB", w = 1.1f, run = Grab },
            };
        }

        /// <summary>Weighted move pick, no immediate repeats; after 4 moves without a counter window -> force a SLAM.</summary>
        private int PickMove(Move[] m, int last, int sinceSlam)
        {
            if (sinceSlam >= 4) for (int i = 0; i < m.Length; i++) if (m[i].name == "SLAM") return i;   // moves without a counter window before a forced SLAM
            // MOVE BAG: moves already used this round keep only 12 % weight -> every move appears before any repeats
            if (bagPhase != phase) { moveBag.Clear(); bagPhase = phase; }
            float sum = 0f;
            var w = new float[m.Length];
            for (int i = 0; i < m.Length; i++) { w[i] = m[i].w * (i == last ? 0.15f : 1f) * (moveBag.Contains(m[i].name) ? 0.12f : 1f); sum += w[i]; }
            if (sum <= 0f) return -1;
            float r = Random.value * sum;
            int k = m.Length - 1;
            for (int i = 0; i < m.Length; i++) { if (r < w[i]) { k = i; break; } r -= w[i]; }
            moveBag.Add(m[k].name);
            bool all = true;
            for (int i = 0; i < m.Length; i++) if (m[i].w > 0.06f && m[i].name != "SLAM" && !moveBag.Contains(m[i].name)) all = false;
            if (all) { moveBag.Clear(); moveBag.Add(m[k].name); }
            return k;
        }

        // ------------------------------------------------------------------ phase start / end
        private IEnumerator PhaseIntro(int p)
        {
            DrillBoard.Finish(PhaseRoman[p] + ". " + PhaseName[p], PhaseCol[p]);
            Sfx.Play("bell_toll", chest, 0.8f);
            if (p == 0)
            {
                CamWide(30f);                                                          // stay wide until CamSettle brings it back
                toneP = 0;
                rig.SetElement(PhaseEl[0]);
                Roar(1f);
                DrillBoard.Yell("Hollow, still. But my hands remember.", 2.4f);
                yield return Wait(1.8f);
                if (aborted) yield break;
                yield return CamSettle();
                yield break;
            }
            // Absalom appears beside the phase's energy pylon and channels it
            var py = Pylon(p - 1);
            Vector2 at = py != null ? py.Pos : chest + new Vector2(p % 2 == 1 ? -8f : 8f, -2f);
            CamWide(30f);
            if (actor != null) { actor.Destroy(); actor = null; }
            Vector2 stand = at + new Vector2(at.x < chest.x ? 1.1f : -1.1f, 1.3f);
            actor = new AbsalomActor(stand, Ctx.Host.transform, Ctx.Player) { Alpha = 0f };
            actor.Airborne = true;
            actor.Lift = 0.3f;
            actor.Face(at - stand);
            actor.Play("idle", 5f);
            Sfx.Play("teleport_dark", stand, 0.8f);
            for (float t = 0f; t < 0.6f && !aborted; t += Time.deltaTime) { actor.Alpha = t / 0.6f; yield return null; }
            if (aborted) yield break;
            actor.Alpha = 1f;
            actor.Play("strike", 8f, false);
            if (py != null) py.Channel(true);
            Sfx.Play("eye_beam_charge", at, 0.8f);
            DrillBoard.ShoutName = "ABSALOM";
            DrillBoard.Yell(InfuseLine[p], 2.6f);
            yield return Wait(1.2f);
            if (aborted) yield break;
            toneP = p;                                                                  // whole map changes tint
            rig.SetElement(PhaseEl[p]);
            BossJuice.Flash(rig.PointWorld(Vector2.zero), PhaseCol[p], 6f, 3f, 0.7f);
            Roar(1.2f);
            actor.Play("idle", 5f);
            actor.Face((Vector2)chest - stand);
            DrillBoard.ShoutName = "THE VESSEL";
            yield return Wait(1.2f);
            if (aborted) yield break;
            yield return CamSettle();                                                   // fight only after the camera has fully returned
        }

        private IEnumerator PhaseFalls(int p)
        {
            if (p == 4) { ClearField(); yield return Finale(); yield break; }          // end of Phase 5 -> LAST GRASP -> Vessel falls -> Absalom flees (.Finale.cs)
            actState = ActState.Rest;
            ClearField();
            DrillBoard.TrackFrac = 0f; DrillBoard.GoalFrac = 0f; DrillBoard.Goal = "VESSEL 0%";
            CamWide(30f);                                                               // keep the wide frame STRAIGHT into the next phase intro (CamSettle brings it back) — only one camera pull-out / return
            slumpTarget = 0.6f;
            Roar(1.4f);
            BossJuice.Shake(3);
            Sfx.Play("bell_toll", chest, 1f);
            yield return Wait(1.2f);
            // relics do NOT pop out between phases — all 3 burst out together when the Vessel dies (VesselDies)
            if (p >= 1) yield return PylonBreaks(p - 1);
            Ctx.Flags.Set(PhaseFlag(p));
            DrillBoard.Finish(PhaseName[p] + " FADES", PhaseCol[p]);
            RefillFlasks();
            yield return Wait(1.6f);
            slumpTarget = 0f;
            yield return Wait(1.2f);
            DrillBoard.Active = false;
            actState = ActState.Idle;
            restartAt = Time.time + 1f;
            actMain = null;
        }

        private void BeginBoard(int p)
        {
            DrillBoard.Begin(p + 1, PhaseName[p], 5);
            DrillBoard.Stations = 5;
            DrillBoard.StationLabel = "PHASE";
            DrillBoard.PipLabel = "";                                                  // no RELICS slot on the HUD (relics only return at the end of the fight)
            DrillBoard.ShoutName = "THE VESSEL";
            DrillBoard.MoraleMax = 0;
            DrillBoard.Morale = 0;
            DrillBoard.Objective = PhaseHint[p];
            DrillBoard.TrackFrac = 1f;
            DrillBoard.Goal = "VESSEL 100%";
        }

        private int RelicsFreed()
        {
            return Ctx.Flags.HasAll(SlainFlag) ? 3 : 0;                                // only when the Vessel dies
        }

        /// <summary>Died mid-phase: clear the arena, phase HP back to full, replay when Ren wakes.</summary>
        private void AbortAct()
        {
            if (actState == ActState.Idle) return;
            aborted = true;
            lastGrasp = false; noLie = false;
            lungeGoal = 0f;
            wideUntil = 0f;                                                             // died during a phase intro -> do not get stuck in the wide frame
            endCam = false;
            if (actMain != null) Ctx.Host.StopCoroutine(actMain);
            actMain = null;
            ClearField();
            for (int e = 0; e < 4; e++) { var py = Pylon(e); if (py != null) py.Channel(false); }
            if (actor != null) { actor.Destroy(); actor = null; }
            DrillBoard.Active = false;
            actState = ActState.Idle;
            vhp = 1f;
            restartAt = Time.time + 3f;
            slumpTarget = 0f;
            if (rig != null) rig.JawOpen = 0f;
        }

        private void Roar(float seconds)
        {
            jawUntil = Time.time + seconds;
            if (rig != null) rig.JawOpen = 0.8f;
            Sfx.Play("quake_rumble", chest, 0.7f);
            BossJuice.Shake(1);
        }

        private void RefillFlasks()
        {
            var combat = Ctx.Player != null ? Ctx.Player.GetComponent<PlayerCombat>() : null;
            if (combat == null) return;
            combat.RefillPotions();
            Hints.Show($"Your flasks refill: {combat.Potions}/{combat.maxPotions}.", 3f);
        }

        // ------------------------------------------------------------------ energy pylons (background, lit per phase)
        private VoidIsleMechanism Pylon(int e) => e >= 0 && e < Elements.Length ? VoidIsleMechanism.Find(Elements[e]) : null;

        /// <summary>Re-entering the map: pylons of cleared phases are already broken.</summary>
        private void SyncPylons()
        {
            if (pylonsSynced) return;
            pylonsSynced = true;
            for (int p = 1; p < 5; p++) if (Ctx.Flags.HasAll(PhaseFlag(p))) { var py = Pylon(p - 1); if (py != null) py.Shatter(true); }
            int np = NextPhase();
            toneP = Ctx.Flags.HasAll(AwakeFlag) ? Mathf.Min(np, 4) : -1;               // map carries the tint of the upcoming phase
        }

        private IEnumerator PylonBreaks(int e)
        {
            var py = Pylon(e);
            if (py != null) { py.Channel(false); py.Shatter(false); }
            if (actor == null) yield break;
            Sfx.Play("teleport_dark", actor.Pos, 0.7f);
            for (float t = 0f; t < 0.8f; t += Time.deltaTime) { if (actor == null) yield break; actor.Alpha = 1f - t / 0.8f; yield return null; }
            if (actor != null) { actor.Destroy(); actor = null; }
        }

        // ------------------------------------------------------------------ map tint per phase
        private int toneP = -1;
        private Color tileCur = Color.white, lightCur = Color.white, decoCur = Color.white;
        private List<Tilemap> tilemaps;

        private void TickTone(float dt)
        {
            if (tilemaps == null)
            {
                tilemaps = new List<Tilemap>();
                var d = MapEncounterDirector.Instance;
                if (d != null && d.mapRoot != null) tilemaps.AddRange(d.mapRoot.GetComponentsInChildren<Tilemap>(true));
                foreach (var tm in tilemaps)
                    if (tm != null) DomainTone.UseToneMaterial(tm.GetComponent<TilemapRenderer>());   // desaturatable floor
            }
            float k = 1f - Mathf.Exp(-dt * 1.6f);
            tileCur = Color.Lerp(tileCur, toneP >= 0 ? TileTint[toneP] : Color.white, k);
            lightCur = Color.Lerp(lightCur, toneP >= 0 ? LightMul[toneP] : Color.white, k);
            decoCur = Color.Lerp(decoCur, toneP >= 0 ? DecorTint[toneP] : Color.white, k);
            foreach (var tm in tilemaps)
                if (tm != null) tm.color = new Color(tileCur.r, tileCur.g, tileCur.b, tm.color.a);
            ZoneAmbience.ColorMul = lightCur * (1f - 0.45f * stormDim) * (1f - 0.55f * eclipseDim);   // Eye of the Storm / Eclipse: the arena goes dark
            VoidDecorMechanism.Tint = decoCur;
            // void decor uses GREY SPRITES x phase colour; violet lights in the map -> phase colour
            DomainTone.Active = toneP >= 0;
            grayCur = Color.Lerp(grayCur, toneP >= 0 ? GrayTint[toneP] : Color.white, k);
            glowCur = Color.Lerp(glowCur, toneP >= 0 ? PhaseCol[toneP] : new Color(0.78f, 0.45f, 1f), k);
            DomainTone.Tint = grayCur;
            DomainTone.Glow = glowCur;
            if (mapSprites == null) CollectToneTargets();
            // floor + tile objects use the desaturate shader -> violet becomes grey, then multiplied by the phase colour. UMBRA keeps the original violet.
            desatCur = Mathf.Lerp(desatCur, toneP >= 0 && toneP < 4 ? 1f : 0f, k);
            DomainTone.SetDesat(desatCur, Float("tone_gain", 1.3f));
            foreach (var (sr, c) in mapSprites) if (sr != null) sr.color = toneP >= 0 ? DomainTone.Mul(c, tileCur) : c;
            foreach (var (l, c) in mapLights) if (l != null) l.color = toneP >= 0 ? glowCur : c;
        }

        private Color grayCur = Color.white, glowCur = new Color(0.78f, 0.45f, 1f);
        private float desatCur;
        private List<(SpriteRenderer, Color)> mapSprites;
        private List<(Light2D, Color)> mapLights;

        /// <summary>Sprites tinted by other mechanisms (island / pylons / watchtowers / void decor / fight objects) — TickTone does not overwrite their colour.</summary>
        private bool ToneManaged(Transform t)
        {
            for (var p = t; p != null; p = p.parent)
            {
                string n = p.name;
                if (n.StartsWith("Void Isle") || n.StartsWith("Watch Tower") || n.StartsWith("Void Decor") || n.StartsWith("Beam") || n.StartsWith("freed relic") || n.StartsWith("magma") || n.StartsWith("spitfire")) return true;
                if (fieldObjs.Contains(p.gameObject)) return true;
                if (p.GetComponent<SuperObject>() != null) break;
            }
            return false;
        }

        /// <summary>Map objects (Tiled tile objects: pentagon ring, rocks…) + the map's violet lights (not Vessel / pylon / tower lights) to tint per phase.</summary>
        private void CollectToneTargets()
        {
            mapSprites = new List<(SpriteRenderer, Color)>();
            mapLights = new List<(Light2D, Color)>();
            var d = MapEncounterDirector.Instance;
            if (d != null && d.mapRoot != null)
                foreach (var sr in d.mapRoot.GetComponentsInChildren<SpriteRenderer>(true))
                    if (sr.GetComponentInParent<SuperObject>() != null && (rig == null || rig.Root == null || !sr.transform.IsChildOf(rig.Root)) && !ToneManaged(sr.transform))
                    {
                        mapSprites.Add((sr, sr.color));                                // only Tiled tile objects (never sprites built by code)
                        DomainTone.UseToneMaterial(sr);
                    }
            foreach (var l in Object.FindObjectsByType<Light2D>(FindObjectsSortMode.None))
            {
                if (l == null || l.lightType == Light2D.LightType.Global) continue;
                if (rig != null && rig.Root != null && l.transform.IsChildOf(rig.Root)) continue;
                string n = l.transform.root.name + "/" + l.transform.parent?.name;
                if (n.Contains("Void Isle") || n.Contains("Watch Tower") || n.Contains("relic")) continue;
                Color.RGBToHSV(l.color, out float h, out float s, out float v);
                if (s > 0.2f && h > 0.65f && h < 0.9f) mapLights.Add((l, l.color));     // only VIOLET lights (void glow)
            }
        }

        // ------------------------------------------------------------------ freed relics + floating blue orbs
        private Vector2 RelicHome(int a)
        {
            var f = Floor;
            return a == 0 ? new Vector2(f.xMin - 1.3f, f.center.y + 0.9f)
                 : a == 1 ? new Vector2(f.xMax + 1.3f, f.center.y + 0.9f)
                 : new Vector2(f.center.x, f.yMin - 1.4f);
        }

        private IEnumerator RelicBreaksFree(int a, bool roar = true)
        {
            Vector2 from = rig.PointWorld(RelicSeat[a]);
            Sfx.Play("glass_break", from, 1f);
            Sfx.Play("ward_break", from, 0.9f);
            BossJuice.Shake(3);
            BossJuice.Flash(from, RelicBlue, 3.5f, 3f, 0.6f);
            if (roar) Roar(1.2f);
            var orb = MakeOrb("freed relic " + Relics[a], from);
            Vector2 to = RelicHome(a);
            Vector2 mid = (from + to) * 0.5f + Vector2.up * 1.6f;
            for (float t = 0f; t < 1.4f; t += Time.deltaTime)
            {
                float u = t / 1.4f, e = u * u * (3f - 2f * u), w = 1f - e;
                orb.position = w * w * from + 2f * w * e * mid + e * e * to;
                yield return null;
            }
            orb.position = to;
            freed.Add((orb, to, Random.Range(0f, 6f)));
            BossJuice.Flash(to, RelicBlue, 2f, 2f, 0.4f);
            Sfx.Play("hymn_pop", to, 0.9f);
        }

        private Transform MakeOrb(string name, Vector2 at)
        {
            var g = new GameObject(name);
            g.transform.SetParent(Ctx.Host.transform, false);
            g.transform.position = at;
            var core = g.AddComponent<SpriteRenderer>();
            core.sprite = MarginArt.Pixel();
            FxLighting.MakeUnlit(core);
            core.sortingLayerID = layer; core.sortingOrder = 9;
            core.color = new Color(0.8f, 0.92f, 1f, 1f);
            g.transform.localScale = new Vector3(10f, 10f, 1f);
            var halo = new GameObject("halo").AddComponent<SpriteRenderer>();
            halo.transform.SetParent(g.transform, false);
            halo.transform.localScale = new Vector3(1.8f, 1.8f, 1f);
            halo.sprite = MarginArt.Pixel();
            FxLighting.MakeUnlit(halo);
            halo.sortingLayerID = layer; halo.sortingOrder = 8;
            halo.color = new Color(RelicBlue.r, RelicBlue.g, RelicBlue.b, 0.35f);
            var l = g.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point;
            l.color = RelicBlue; l.pointLightOuterRadius = 1.4f; l.pointLightInnerRadius = 0.05f; l.intensity = 1.1f;
            return g.transform;
        }

        /// <summary>Freed relics — rebuilt when entering the domain after the Vessel has fallen.</summary>
        private void SpawnFreedOrbs()
        {
            for (int a = 0; a < 3; a++)
                if (Ctx.Flags.HasAll(SlainFlag)) freed.Add((MakeOrb("freed relic " + Relics[a], RelicHome(a)), RelicHome(a), Random.Range(0f, 6f)));
        }

        private void TickFreed()
        {
            foreach (var f in freed)
            {
                if (f.t == null) continue;
                float t = Time.time + f.phase;
                f.t.position = f.home + new Vector2(Mathf.Sin(t * 0.7f) * 0.06f, Mathf.Sin(t * 1.3f) * 0.12f);
                float s = 10f + Mathf.Sin(t * 3.1f) * 1.2f;
                f.t.localScale = new Vector3(s, s, 1f);
            }
        }

        // ------------------------------------------------------------------ test keys
        /// <summary>F7: the current phase loses all its HP.</summary>
        internal bool DebugSkipAct()
        {
            if (actState != ActState.Playing) return false;
            vhp = 0f;
            ResetHands();
            Hints.Show("[TEST] The Vessel: phase " + (phase + 1) + " " + PhaseName[phase] + " emptied", 1.5f);
            return true;
        }

        private int showcase;
        /// <summary>Ctrl+F7: use the current phase's next move now.</summary>
        internal bool DebugShowcase()
        {
            if (actState != ActState.Playing) return false;
            var m = Moves(phase);
            int k = showcase++ % m.Length;
            aborted = false;
            StartR(m[k].run(1f));
            Hints.Show("[TEST] " + PhaseName[phase] + " move " + (k + 1) + "/" + m.Length + ": " + m[k].name, 2f);
            return true;
        }
    }
}
