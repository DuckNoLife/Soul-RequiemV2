using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// TRIALS (stage): a group of areas for one quest, ending with a miniboss / the quest objective.
    /// Tiled data:
    ///   area (zone)           : `stage` = trial code (e.g. "west"). Areas without it = cleared once, cleared forever.
    ///   rest point (checkpoint): `stage` = trial code for which this point is the RESPAWN POINT (placed at the trial start), `stage_name` = name shown when the trial is passed.
    /// Rules:
    ///   • Falling while the trial is NOT done: wake at the respawn point of the trial in progress (if lit; unlit -> the last campfire),
    ///     and EVERY unfinished trial respawns its enemies - every area of that trial is fought again from scratch.
    ///     &lt;zone&gt;_cleared flags are NOT cleared: opened shortcuts, puzzles, picked-up items, rung bells are kept (Souls style).
    ///   • Trial passed (every area WITH ENEMIES in the trial cleared): flag stage_&lt;code&gt;_done -> the trial is locked for good, full HP + flask, autosave,
    ///     line "&lt;trial name&gt; - trial complete".
    ///   • Loading a save / returning to the map: unfinished trials respawn too (the _cleared flags of their areas are ignored when building the areas).
    /// Areas with an enemy cave cannot restart -> left as is.
    /// </summary>
    public partial class MapEncounterDirector
    {
        private string lastStage = "";

        public static string StageDoneFlag(string stage) => "stage_" + stage + "_done";

        /// <summary>Trial passed (flag stage_&lt;code&gt;_done).</summary>
        public bool StageDone(string stage) => !string.IsNullOrEmpty(stage) && Flags != null && Flags.Has(StageDoneFlag(stage));

        /// <summary>Area belongs to an unfinished trial.</summary>
        private bool InOpenStage(EncounterZone z) => !string.IsNullOrEmpty(z.stage) && !StageDone(z.stage);

        /// <summary>Called after restoring areas from flags (Start): area cleared but trial unfinished -> respawn enemies.</summary>
        private void ApplyStagesOnLoad()
        {
            foreach (var z in layout.Zones.Values)
                if (z.cleared && InOpenStage(z) && z.caves.Count == 0) Rearm(z);
        }

        /// <summary>Restart one area from scratch: remove living enemies, open arena locks, drop the cleared state (flags kept).</summary>
        private void Rearm(EncounterZone z)
        {
            foreach (var e in z.alive)
                if (e != null) { if (e == Boss) Boss = null; if (e == BossPartner) BossPartner = null; Destroy(e.gameObject); }
            z.alive.Clear();
            gateCtl.RearmArena(z.id);
            z.started = z.cleared = false;
            z.quietSince = -1f; z.stragglersCalled = false; z.fought = false;
            z.bossDown = false;
            RaiseZoneReset(z.rect);
            z.waveIndex = -1;
            z.killed = 0;
            z.pending = 0; z.generation++;
            z.holdNearRespawn = z.rect.Contains((Vector2)respawnPoint);
        }

        /// <summary>Falling: every unfinished trial respawns its enemies.</summary>
        private void ResetOpenStages()
        {
            foreach (var z in layout.Zones.Values)
                if (InOpenStage(z) && z.caves.Count == 0 && (z.started || z.cleared)) Rearm(z);
        }

        /// <summary>Trial the player was fighting when he fell: the area (of an unfinished trial) containing the fall spot, otherwise the most recently entered trial.</summary>
        private string DeathStage(Vector2 at)
        {
            foreach (var z in layout.Zones.Values)
                if (InOpenStage(z) && z.rect.Contains(at)) return z.stage;
            return !string.IsNullOrEmpty(lastStage) && !StageDone(lastStage) ? lastStage : "";
        }

        /// <summary>Falling in an unfinished trial -> that trial's respawn point (if lit).</summary>
        private bool ChooseStageRespawn(Vector2 diedAt)
        {
            string st = DeathStage(diedAt);
            if (st.Length == 0) return false;
            if (triggerHandler.TryStageSpawn(st, out var pos)) { SetRespawnPoint(pos); return true; }
            return false;
        }

        /// <summary>Area just cleared: if it is the trial's last area (with enemies) -> trial passed. Returns true if the trial was just passed.</summary>
        private bool CheckStageComplete(EncounterZone z)
        {
            if (!InOpenStage(z)) return false;
            foreach (var o in layout.Zones.Values)
                if (o.stage == z.stage && o.points.Count > 0 && !o.cleared) return false;
            CompleteStage(z.stage);
            return true;
        }

        private void CompleteStage(string stage)
        {
            Flags.Set(StageDoneFlag(stage));
            if (lastStage == stage) lastStage = "";
            if (playerHealth != null && !playerHealth.IsDead)
            {
                playerHealth.Heal(playerHealth.Max);
                var kit = player != null ? player.GetComponent<PlayerCombat>() : null;
                if (kit != null) kit.RefillPotions();
            }
            SaveProgress();
            StartCoroutine(StageHint(triggerHandler.StageName(stage)));
            StartCoroutine(AnchorStageRespawn());                        // respawn in the room where the trial was passed
        }

        /// <summary>After passing a trial the respawn point moves to the room just cleared - no walking back to the campfire.
        /// Picks a calm spot where Ren stands (no damage for 0.8 s, no cutscene) within 8 s after passing; none -> the spot at the moment of passing.
        /// Falling in a later trial: that trial has a lit campfire -> back to that campfire (ChooseStageRespawn), otherwise back here.</summary>
        private IEnumerator AnchorStageRespawn()
        {
            if (player == null || playerHealth == null || playerHealth.IsDead) yield break;
            Vector2 at = player.position;
            float hp = playerHealth.Current, calmSince = Time.time, until = Time.time + 8f;
            while (Time.time < until)
            {
                if (playerHealth.IsDead || respawning) yield break;
                if (playerHealth.Current < hp - 0.01f) calmSince = Time.time;
                hp = playerHealth.Current;
                if (Time.time - calmSince >= 0.8f && !CutsceneState.Active) { at = player.position; break; }
                yield return null;
            }
            SetRespawnPoint(at);
            SaveProgress();
        }

        private IEnumerator StageHint(string name)
        {
            yield return new WaitForSeconds(2.6f);                       // after "The gate is open" / the relic scene
            while (CutsceneState.Active) yield return null;
            Hints.Show($"{name} - trial complete. Your strength and flasks are restored - if you fall, you rise here.", 5f);
        }
    }
}
