using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// The Registrar fights in 3 TIMED PHASES.
    ///   • A phase is passed when its timer runs out: a Vessel-style HUD board ("PHASE n/3  FIRST DRAFT", bar + "PAGE x%") shows the time left.
    ///     The boss HP bar = what is left of the ledger (all 3 phases).
    ///   • Within a phase he picks moves at RANDOM, weighted, with cooldowns, never repeating the last move; later phases unlock more moves and
    ///     often pair 2 light moves at once. Every phase has at least one Grip. Moves NOT yet used this phase get a x2.5 weight boost.
    ///     Phase I  : Ledger Maws (aimed), Summon 2 skeletons, Quill Stab, Ink Pots, The Grip, Cranes, Saws, Tease.
    ///     Phase II : + lane beams (horizontal / vertical), Summon skeleton + mage, tear one margin (Erase), Saws (bounce twice), Eraser,
    ///                Page Fold (single).
    ///     Phase III: + sweeping beam, Summon 2 mages, tear another margin, Written Backwards; Quill Stab 2 quills per stab;
    ///                saws bounce 3 times; Page Fold folds TWICE.
    ///     Eraser gets more weight while summoned monsters are alive (it erases his own monsters too).
    ///   • Drinking a flask in the Margin -> the current phase lasts `flaskPenalty` seconds longer (the PAGE bar moves back).
    ///   • Dying -> replay from the START of the unfinished phase (ResumeChapter = phase * 3; old variable name kept for compatibility).
    /// Related: .Craft.cs (Cranes, Saws, Eraser), .Fold.cs (Page Fold), .Tease.cs (Tease, grab on touch), .Sketch.cs, .Grip.cs, .Attacks.cs.
    /// </summary>
    public sealed partial class RegistrarSkills
    {
        [Header("Timed phases")]
        public float flaskPenalty = 8f;              // seconds added to the current phase each time Ren drinks a flask

        private const int Phases = 3;
        private static readonly float[] PhaseSeconds = { 45f, 50f, 55f };
        private static readonly string[] PhaseRoman = { "I", "II", "III" };
        private static readonly string[] PhaseHint = { "Survive until the page is full.", "Survive the revisions.", "Survive the final draft." };
        private static readonly string[] MoveIds = { "maw", "stab", "rain", "summon", "grip", "lanes", "erase", "sweep", "backwards", "cranes", "saws", "eraser", "fold", "tease" };

        private int part;                            // current phase 0..2 (named `part` because BossSkillsBase already has `phase`)
        private float phaseT, phaseDur = 1f, pendingPenalty;
        private bool phaseRunning, boardOwned, gripThisPhase;
        private float fightClock;                    // fight clock (paused during cutscenes / dialogue) — used for cooldowns
        private string lastMove = "";
        private readonly Dictionary<string, float> moveReady = new Dictionary<string, float>();
        private readonly HashSet<string> usedThisPart = new HashSet<string>();   // moves already used in this phase
        private float lastPagesFrac = -1f;
        private int lastPct = -1;

        // ------------------------------------------------------------------ fight flow: 3 phases
        private IEnumerator Book()
        {
            bookStarted = true;
            while (ai != null && ai.Player == null) yield return null;   // Act() calls OnEngage before checking for the player
            if (ai == null) yield break;
            var pl = ai.Player;
            pHp = pl.GetComponent<Health>();
            pCombat = pl.GetComponent<PlayerCombat>();
            pCtrl = pl.GetComponent<PlayerController>();
            var psr = pl.GetComponentInChildren<SpriteRenderer>();
            if (psr != null) pOrder = psr.sortingOrder;
            PlayerFlask.Stolen = OnFlaskStolen;                        // in the Margin flasks heal HIM, not Ren
            yield return W(0.4f);
            while (Frozen && !Halt) yield return null;                 // wait for the boss name card
            int start = Mathf.Clamp(chapter / 3, 0, Phases - 1);
            if (start > 0) Hints.Show("The Registrar turns back to Part " + PhaseRoman[start] + ".", 3f);
            else Hints.Show("Survive his three drafts. In the Margin, every potion you drink feeds his book instead of you.", 4.5f);

            for (part = start; part < Phases && !Halt; part++)
            {
                chapter = part * 3;                                   // ActNo / Warn() read the chapter
                ResumeChapter = part * 3;
                gripThisPhase = false;
                lastMove = "";
                moveReady.Clear();
                usedThisPart.Clear();
                phaseT = 0f;
                phaseDur = PhaseSeconds[part] + pendingPenalty;
                pendingPenalty = 0f;
                BeginBoard();
                yield return ActCard(part);
                if (Halt) yield break;
                phaseRunning = true;
                while (phaseT < phaseDur && !Halt)
                {
                    yield return NextMove();
                    if (phaseT >= phaseDur || Halt) break;
                    yield return W(Mathf.Lerp(0.75f, 0.4f, part / 2f));
                }
                phaseRunning = false;
                if (Halt) yield break;
                yield return Drain();
                if (Halt) yield break;
                phaseT = phaseDur;
                UpdateBoard();
                yield return ScribbleOut();
                if (Halt) yield break;
                if (part < Phases - 1)
                {
                    DrillBoard.Finish(ActTitle[part] + " SURVIVED", new Color(0.86f, 0.78f, 1f));
                    Sfx.Play("registrar_quill", Pos, 0.7f);
                    yield return W(1.1f);
                    yield return PageTurn();
                }
            }
            if (Halt) yield break;
            HideBoard();
            yield return Collapse();
        }

        /// <summary>Every frame (from Update): fight clock, phase timer, HUD board, HP bar = what is left of the ledger.</summary>
        private void TickPhase()
        {
            if (Halt)
            {
                if (pHp != null && pHp.IsDead) HideBoard();
                return;
            }
            float dt = Dt;
            fightClock += dt;
            if (phaseRunning) phaseT = Mathf.Min(phaseDur, phaseT + dt);
            if (boardOwned) UpdateBoard();
            SetPages();
        }

        /// <summary>Boss HP bar = remaining ledger (3 phases combined; a stolen flask lengthens the phase -> the bar moves back with it).</summary>
        private void SetPages()
        {
            if (hp == null || hp.IsDead || collapsed) return;
            float total = 0f, done = 0f;
            for (int i = 0; i < Phases; i++) total += PhaseSeconds[i];
            for (int i = 0; i < part && i < Phases; i++) done += PhaseSeconds[i];
            if (part < Phases) done += PhaseSeconds[part] * Mathf.Clamp01(phaseT / Mathf.Max(1f, phaseDur));
            float left = Mathf.Clamp01(1f - done / total);
            if (Mathf.Abs(left - lastPagesFrac) < 0.004f) return;
            lastPagesFrac = left;
            hp.SetCurrent(Mathf.Max(1f, hp.Max * left));
        }

        // ------------------------------------------------------------------ HUD board (shares DrillBoard with the Vessel fight)
        private void BeginBoard()
        {
            DrillBoard.Begin(part + 1, ActTitle[part], 0);
            DrillBoard.Stations = Phases;
            DrillBoard.StationLabel = "PHASE";
            DrillBoard.PipLabel = "";
            DrillBoard.MoraleMax = 0;
            DrillBoard.Morale = 0;
            DrillBoard.ShoutName = "THE REGISTRAR";
            DrillBoard.Objective = PhaseHint[part];
            boardOwned = true;
            lastPct = -1;
            UpdateBoard();
        }

        private void UpdateBoard()
        {
            float f = Mathf.Clamp01(phaseT / Mathf.Max(1f, phaseDur));
            DrillBoard.TrackFrac = -1f;
            DrillBoard.GoalFrac = f;
            int pct = Mathf.FloorToInt(f * 100f + 0.0001f);
            if (pct == lastPct) return;
            lastPct = pct;
            DrillBoard.Goal = "PAGE " + pct + "%";
        }

        private void HideBoard()
        {
            if (!boardOwned) return;
            boardOwned = false;
            DrillBoard.Active = false;
        }

        /// <summary>Stolen flask -> the current phase gets longer (between two phases it is added to the next one).</summary>
        private void PhasePenalty()
        {
            if (phaseRunning || phaseT < phaseDur) phaseDur += flaskPenalty;
            else pendingPenalty += flaskPenalty;
            DrillBoard.Say("+" + Mathf.RoundToInt(flaskPenalty) + "s  THE BOOK DRINKS", MarginArt.Violet);
            Hints.Show("The Margin drinks your potion. The Registrar writes more of this page.", 3f);
        }

        // ------------------------------------------------------------------ move picker
        private IEnumerator NextMove()
        {
            string id = PickMove(lastMove, false);
            lastMove = id;
            moveReady[id] = fightClock + Cooldown(id);
            usedThisPart.Add(id);
            if (id == "grip") gripThisPhase = true;
            float combo = part == 0 ? 0f : part == 1 ? 0.3f : 0.45f;     // phase II / III: sometimes pair a light move to run alongside
            if (IsLightMove(id) && Random.value < combo)
            {
                string b = PickMove(id, true);
                if (b != null)
                {
                    moveReady[b] = fightClock + Cooldown(b);
                    usedThisPart.Add(b);
                    yield return Par(RunMove(id), DelayThen(0.7f, RunMove(b)));
                    yield break;
                }
            }
            yield return RunMove(id);
        }

        private string PickMove(string exclude, bool lightOnly)
        {
            var ids = new List<string>();
            var ws = new List<float>();
            float sum = 0f;
            foreach (var id in MoveIds)
            {
                if (id == exclude || lightOnly && !IsLightMove(id)) continue;
                float w = Weight(id) * (usedThisPart.Contains(id) ? 1f : 2.5f);   // every phase shows its full move set
                if (w <= 0f) continue;
                if (moveReady.TryGetValue(id, out float ready) && fightClock < ready) continue;
                ids.Add(id); ws.Add(w); sum += w;
            }
            if (ids.Count == 0) return lightOnly ? null : exclude == "maw" ? "rain" : "maw";
            float r = Random.value * sum;
            for (int i = 0; i < ids.Count; i++) { r -= ws[i]; if (r <= 0f) return ids[i]; }
            return ids[ids.Count - 1];
        }

        /// <summary>Light move = may run together with another move.</summary>
        private static bool IsLightMove(string id) => id == "maw" || id == "stab" || id == "rain";

        private float Weight(string id)
        {
            float prog = phaseT / Mathf.Max(1f, phaseDur);
            switch (id)
            {
                case "maw": return 3f;
                case "stab": return 2.6f;
                case "rain": return 2f;
                case "summon": return AliveSummons() == 0 && prog < 0.8f ? 2.6f : 0f;
                case "grip": return gripping ? 0f : !gripThisPhase && prog > 0.3f ? 8f : 1.1f;
                case "lanes": return part >= 1 ? 2f : 0f;
                case "erase": return part >= 1 && EraseSide() >= 0 ? 1.4f : 0f;
                case "sweep": return part >= 2 ? 1.6f : 0f;
                case "backwards": return part >= 2 && GameInput.InvertedUntil <= Time.time ? 1.1f : 0f;
                case "cranes": return part == 0 ? 2.2f : 2f;
                case "saws": return 2.2f;                                                          // from phase I
                case "eraser": return part >= 1 ? (AliveSummons() > 0 ? 3f : 1.5f) : 0f;
                case "fold": return part == 1 ? 1.6f : part >= 2 ? 1.8f : 0f;
                case "tease": return teasing ? 0f : 1.3f;
            }
            return 0f;
        }

        private static float Cooldown(string id)
        {
            switch (id)
            {
                case "maw": return 2f;
                case "stab": return 7f;
                case "rain": return 6f;
                case "summon": return 12f;
                case "grip": return 12f;
                case "lanes": return 7f;
                case "erase": return 14f;
                case "sweep": return 10f;
                case "backwards": return 20f;
                case "cranes": return 10f;
                case "saws": return 9f;
                case "eraser": return 13f;
                case "fold": return 12f;
                case "tease": return 14f;
            }
            return 4f;
        }

        /// <summary>Margin that can still be torn: phase II at most 1 (left / right), phase III at most 2 (adds the bottom). -1 = none.</summary>
        private int EraseSide()
        {
            if (erased.Count >= (part >= 2 ? 2 : 1)) return -1;
            bool left = false, right = false, bottom = false;
            foreach (var cut in erased)
            {
                if (cut.width > cut.height) bottom = true;
                else if (cut.center.x < page.center.x) left = true;
                else right = true;
            }
            var free = new List<int>();
            if (!left) free.Add(0);
            if (!right) free.Add(1);
            if (!bottom && part >= 2) free.Add(2);
            return free.Count == 0 ? -1 : free[Random.Range(0, free.Count)];
        }

        private IEnumerator RunMove(string id)
        {
            switch (id)
            {
                case "maw":
                {
                    if (part == 0) yield return MawVolley(new[] { EdgeSpot(0, Random.value), EdgeSpot(1, Random.value) }, Aimed, Warn(1f));
                    else yield return MawVolley(new[] { EdgeSpot(0, Random.value), EdgeSpot(1, Random.value), EdgeSpot(2, Random.value) }, Aimed, Warn(1f));
                    if (part >= 2 && !Halt)
                    {
                        yield return W(0.25f);
                        yield return MawVolley(new[] { EdgeSpot(Random.Range(0, 3), Random.value) }, Aimed, Warn(0.95f));
                    }
                    break;
                }
                case "stab":
                    yield return QuillStab(3, part >= 2 ? 2 : 1);
                    break;
                case "rain":
                    yield return InkRain(4 + part, 0.38f - 0.03f * part);
                    break;
                case "summon":
                    yield return Summon(part == 2 ? "Mage_Skeleton" : "Skeleton", part == 0 ? "Skeleton" : "Mage_Skeleton");
                    break;
                case "grip":
                    yield return Grip(!taughtGrip);
                    break;
                case "lanes":
                {
                    int reps = part >= 2 ? 2 : 1;
                    for (int i = 0; i < reps && !Halt; i++)
                    {
                        yield return LanesH(Warn(0.95f)); yield return W(0.3f);
                        yield return LanesV(Warn(0.95f)); yield return W(0.3f);
                    }
                    break;
                }
                case "erase":
                {
                    int side = EraseSide();
                    if (side >= 0) yield return Erase(side, side == 2 ? 0.62f : 0.9f, Warn(1.3f));
                    break;
                }
                case "sweep":
                    yield return Sweep(Random.value < 0.5f, !taughtSweep);
                    break;
                case "cranes":                                                                     // (.Craft.cs)
                    yield return Cranes();
                    break;
                case "saws":
                    yield return Saws();
                    break;
                case "eraser":
                    yield return EraserPass();
                    break;
                case "fold":                                                                       // (.Fold.cs)
                    yield return PageFolds();
                    break;
                case "tease":                                                                      // (.Tease.cs)
                    yield return Tease();
                    break;
                case "backwards":
                {
                    RunBg(Backwards(5.5f));                                   // inverted keys run in the background, followed right away by a light move
                    yield return W(0.6f);
                    string b = PickMove("backwards", true);
                    if (b != null && !Halt)
                    {
                        moveReady[b] = fightClock + Cooldown(b);
                        yield return RunMove(b);
                    }
                    break;
                }
            }
        }
    }
}
