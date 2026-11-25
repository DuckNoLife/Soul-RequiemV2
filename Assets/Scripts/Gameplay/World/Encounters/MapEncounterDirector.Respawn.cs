using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// DEATH & RESPAWN of MapEncounterDirector: wait respawnDelay -> pick a point (start of an unfinished trial, otherwise the point nearest to where he fell)
    /// -> full HP + flask -> clear the reset_on_death flags of the unfinished area -> restart every area in combat (ResetLockedZones) -> save.
    /// </summary>
    public partial class MapEncounterDirector
    {
        private IEnumerator Respawn()
        {
            respawning = true;
            Vector2 diedAt = player.position;
            // the death screen (RequiemMenus) shows YOU HAVE FALLEN; no duplicate hint
            yield return new WaitForSeconds(respawnDelay);
            if (!ChooseStageRespawn(diedAt))                             // fell inside an unfinished trial -> wake at the trial start
                ChooseNearestRespawn(diedAt);                            // otherwise -> the visited point nearest to where he fell
            player.position = respawnPoint;
            playerHealth.Revive();
            var kit = player.GetComponent<PlayerCombat>();
            if (kit != null) kit.RefillPotions();                       // respawn = full HP + flask refilled
            UndoZoneFlagsOnDeath();                                      // e.g. West Bell -> must be rung again, Isolde pulls Ren down for a rematch
            ResetLockedZones();
            ResetOpenStages();                                           // every unfinished trial respawns its enemies
            SaveProgress();                                              // save the restarted state at once (quitting midway does not get stuck)
            respawning = false;
            FirstTimeHints.Show("tut_death", "You wake close to where you fell. Foes you had not finished have risen again.", 6f);
        }

        /// <summary>The player fell and respawned: every area mid-fight restarts from scratch - remaining enemies removed, arena locks opened.
        /// Every area restarts (not only gate-locked ones), otherwise normal-area enemies chased Ren all the way to the respawn point and surrounded him on revival.
        /// The area holding the respawn point restarts too, but waits until Ren has walked a few steps away from the campfire before the fight resumes.</summary>
        private const float RespawnHoldDistance = 2f;

        private void ResetLockedZones()
        {
            foreach (var z in layout.Zones.Values)
            {
                if (!z.started || z.cleared) continue;
                if (z.caves.Count > 0) continue;                       // area with an enemy cave: caves cannot restart -> left as is
                bool arena = gateCtl.HasArenaLock(z.id);
                foreach (var e in z.alive)
                    if (e != null) { if (e == Boss) Boss = null; if (e == BossPartner) BossPartner = null; Destroy(e.gameObject); }
                z.alive.Clear();
                if (arena) gateCtl.SetArenaClosed(z.id, false);
                z.started = false;
                z.quietSince = -1f; z.stragglersCalled = false; z.fought = false;
                z.bossDown = false;
                RaiseZoneReset(z.rect);
                z.waveIndex = -1;
                z.killed = 0;
                z.pending = 0; z.generation++;
                z.holdNearRespawn = z.rect.Contains((Vector2)respawnPoint);
            }
            RefreshStandIns();                                     // area restarted -> pre-placed actors show up again
        }

        /// <summary>An unfinished area with Tiled property `reset_on_death` = "flag,flag" -> when the player falls those flags are CLEARED
        /// (e.g. Isolde: "west_bell_rung,isolde_summoned" - the West Bell must be rung again, the scene where Isolde drags Ren into the Bower replays, then the fight restarts;
        /// otherwise the bell and scene were already done and there was no way back into the fight). Mechanisms tied to those flags reopen by themselves (Mechanism.OnFlagsReset).</summary>
        private void UndoZoneFlagsOnDeath()
        {
            if (layout == null) return;
            foreach (var z in layout.Zones.Values)
                if (z.started && !z.cleared && z.resetOnDeath.Length > 0) UndoFlags(z);
        }

        private void UndoFlags(EncounterZone z)
        {
            foreach (var f in z.resetOnDeath.Split(','))
                if (f.Trim().Length > 0) Flags.Unset(f.Trim());
        }

        /// <summary>Loading a save: an unfinished `reset_on_death` area whose respawn point lies OUTSIDE the area (quit mid-fight, old save)
        /// -> clear the flags as on death, so the player is not stuck outside the arena.</summary>
        private void UndoZoneFlagsOnLoad()
        {
            if (layout == null) return;
            foreach (var z in layout.Zones.Values)
                if (!z.cleared && z.resetOnDeath.Length > 0 && !z.rect.Contains((Vector2)respawnPoint)) UndoFlags(z);
        }
    }
}
