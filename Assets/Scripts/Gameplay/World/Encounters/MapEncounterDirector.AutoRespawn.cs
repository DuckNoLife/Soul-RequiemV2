using System.Collections.Generic;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// AUTOMATIC RESPAWN POINTS. No light rings / "light a fire to save": the game silently records a point whenever Ren enters an area
    /// (where Ren stands on entering) and when passing a rest point. Falling -> wake at the recorded point NEAREST TO WHERE HE FELL (an unfinished trial still prefers its start).
    /// No points inside arena areas with lock gates (bosses) - falling in a boss fight wakes you outside the gate, never stuck inside the arena.
    /// </summary>
    public partial class MapEncounterDirector
    {
        private readonly List<Vector2> autoRespawns = new List<Vector2>();
        private string autoZoneId = "";
        private const float AutoRespawnMinGap = 0.6f;          // two points closer than this are merged

        /// <summary>Record a respawn point (also the save point when quitting the game).</summary>
        internal void AddAutoRespawn(Vector2 p)
        {
            for (int i = 0; i < autoRespawns.Count; i++)
                if ((autoRespawns[i] - p).sqrMagnitude < AutoRespawnMinGap * AutoRespawnMinGap) { autoRespawns[i] = p; return; }
            autoRespawns.Add(p);
        }

        private void TickAutoRespawn(Vector2 pos)
        {
            if (layout == null || respawning || playerHealth == null || playerHealth.IsDead) return;
            if (autoRespawns.Count == 0) autoRespawns.Add(respawnPoint);              // start point / destination / save
            // smallest area containing Ren
            EncounterZone best = null;
            float area = float.MaxValue;
            foreach (var z in layout.Zones.Values)
                if (z.rect.Contains(pos) && z.rect.width * z.rect.height < area) { area = z.rect.width * z.rect.height; best = z; }
            string id = best != null ? best.id : "";
            if (id == autoZoneId) return;
            autoZoneId = id;
            if (best == null) return;
            triggerHandler?.LightCheckpointsIn(best.id, best.rect);              // entering the area = the area's rest point lights up on the minimap
            if (gateCtl != null && gateCtl.HasArenaLock(best.id)) return;            // gate-locked arena: no respawning inside
            if (best.started && !best.cleared && best.alive.Count > 0) return;      // surrounded: do not record
            AddAutoRespawn(pos);
            SetRespawnPoint(pos);
            SaveProgress();
        }

        /// <summary>Falling: pick the recorded point nearest to where he fell (straight-line distance). No points -> keep the current one.</summary>
        private void ChooseNearestRespawn(Vector2 diedAt)
        {
            if (autoRespawns.Count == 0) return;
            Vector2 pick = autoRespawns[0];
            float bestD = float.MaxValue;
            foreach (var p in autoRespawns)
            {
                float d = (p - diedAt).sqrMagnitude;
                if (d < bestD) { bestD = d; pick = p; }
            }
            SetRespawnPoint(pick);
        }
    }
}
