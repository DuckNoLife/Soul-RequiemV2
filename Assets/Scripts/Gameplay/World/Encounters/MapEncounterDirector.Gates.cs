using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// DOORS of MapEncounterDirector: Ren reaches a closed door -> with the key (requires_key) it opens, otherwise he says `locked_say`
    /// (+ calls out enemies still hiding in the area). New flag -> open the unlock_when gates whose flags are now set (OnFlagSet).
    /// </summary>
    public partial class MapEncounterDirector
    {
        // ------------------------------------------------------------------ key-locked doors + Ren speaks at locked doors
        private readonly Dictionary<MapGate, float> gateBarkAt = new Dictionary<MapGate, float>();

        /// <summary>Ren reaches a closed door: has the key (requires_key) -> uses it, the door opens; no key -> says `locked_say`
        /// (instead of an "Examine" sign standing alone next to the door).</summary>
        private void TickGateLocks(Vector2 pos)
        {
            if (gateCtl == null || CutsceneState.Active || respawning || GameInput.Blocked) return;
            foreach (var g in gateCtl.All)
            {
                if (g.opened || g.closeOnEnter) continue;
                if (g.requiresKey.Length == 0 && g.lockedSay.Length == 0) continue;
                if (g.unlockWhen.Length > 0 && Flags.HasAll(g.unlockWhen)) continue;
                if (DistanceToRect(g.bounds, pos) > 0.4f) continue;
                if (g.requiresKey.Length > 0 && KeyRing.Has(Flags, g.requiresKey))
                {
                    Flags.Set(KeyRing.UsedFlag(g.requiresKey));
                    Flags.Set(g.name + "_unlocked");                  // OnFlagSet opens the gate
                    SfxHooks.KeyUsed(g.bounds.center); // sound
                    Hints.Show($"Used: {KeyRing.NameOf(g.requiresKey)}. The lock turns.", 2.5f);
                    CameraFollow.KickMain(Vector2.down, 0.02f, 0.12f);
                    continue;
                }
                if (g.lockedSay.Length == 0) continue;
                if (gateBarkAt.TryGetValue(g, out float t) && Time.time < t) continue;
                gateBarkAt[g] = Time.time + 4f;
                SfxHooks.DoorLocked(g.bounds.center); // sound
                StartCoroutine(GateBark(g, pos));
            }
        }

        private IEnumerator GateBark(MapGate g, Vector2 pos)
        {
            // only speaks - no turning around / stepping back
            // door locked by an area with enemies still alive (hiding in dark corners / underground) -> call them out at once, say how many are left
            string say = g.lockedSay;
            int left = CallOutRemaining(g.zoneId);
            if (left > 0) say += left == 1 ? " (one still stirs in the dark)" : $" ({left} still stir in the dark)";
            KeyRing.Say(player, say, 2.6f);
            yield break;
        }

        /// <summary>Area `zoneId` started, not cleared, enemies still alive -> Provoke them all (red dots on the minimap). Returns how many remain.</summary>
        private int CallOutRemaining(string zoneId)
        {
            if (string.IsNullOrEmpty(zoneId) || layout == null || !layout.Zones.TryGetValue(zoneId, out var z)) return 0;
            if (!z.started || z.cleared || HasBoss(z)) return 0;
            int n = 0;
            foreach (var e in z.alive)
            {
                if (e == null || e.Health == null || e.Health.IsDead) continue;
                n++;
                if (z.killed > 0 || z.fought) e.Provoke();
            }
            if (n > 0 && (z.killed > 0 || z.fought)) z.stragglersCalled = true;
            return n;
        }

        // ------------------------------------------------------------------ flags -> gates
        /// <summary>New flag: open the unlock_when gates whose flags are set.</summary>
        private void OnFlagSet(bool announce)
        {
            if (gateCtl == null) return;                 // map not fully read yet (Start will call again)
            if (gateCtl.OpenUnlocked(Flags) && announce) Hints.Show("Somewhere, a way opens.", 2.5f);
        }
    }
}
