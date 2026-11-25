#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Director test commands (called from DevTools/DevCheats: F3 defeat all enemies, Shift+F3 pass area, F4 / Shift+F4 / Ctrl+F4 teleport, F10 area picker).
    /// This "partial" part is only compiled in the Editor / Development Build - release builds do not have it.
    /// </summary>
    public partial class MapEncounterDirector
    {
        /// <summary>One row of the area picker (F10).</summary>
        public struct DebugZoneInfo
        {
            public string id, title, tag;
            public bool cleared, here, optional;
        }

        /// <summary>The director has finished building the map (Start has run) - DevCheats waits for this before jumping areas after a map reload.</summary>
        public bool DebugReady => layout != null && gateCtl != null && player != null;

        /// <summary>(F3) Kill every living enemy, caves count as fully spawned - tests the wave / gate flow.</summary>
        public void DebugKillAll()
        {
            foreach (var e in FindObjectsByType<EnemyAI>(FindObjectsSortMode.None))
                if (e != null && !e.Health.IsDead) e.Health.TakeDamage(99999f);
            foreach (var z in layout.Zones.Values)
                foreach (var c in z.caves) if (c != null) c.DebugFinish();
            Hints.Show("[TEST] All enemies defeated", 1.5f);
        }

        /// <summary>(F4) Go to the next main story step (areas with property `story`); unmarked maps -> next area in Tiled order.</summary>
        public void DebugNextZone() => DebugStepZone(1);

        private int storyCursor = -1;                       // step F4 just jumped to (areas reused by several steps, e.g. r8 = 2.2 and 5)

        private struct StoryStep { public EncounterZone zone; public float order; public string done; }

        /// <summary>Main story steps sorted by `story` number (skipping ambush / side areas - they have no `story`).</summary>
        private List<StoryStep> StorySteps()
        {
            var list = new List<StoryStep>();
            foreach (var z in layout.Zones.Values)
                for (int i = 0; i < z.story.Length; i++)
                    list.Add(new StoryStep { zone = z, order = z.story[i], done = i < z.storyDone.Length ? z.storyDone[i] : "" });
            list.Sort((a, b) => a.order.CompareTo(b.order));
            return list;
        }

        /// <summary>(F4 / Shift+F4) Jump `step` main story steps. On arrival, sets the "done" flags of EVERY earlier step (doors, keys, quests,
        /// bosses as if the player really went there) and opens those areas' gates. Maps without `story` -> jump areas in Tiled order.</summary>
        public void DebugStepZone(int step)
        {
            var steps = StorySteps();
            if (steps.Count == 0) { StepZoneByTiledOrder(step); return; }
            Vector2 p = player.position;
            int cur = storyCursor >= 0 && storyCursor < steps.Count && steps[storyCursor].zone.rect.Contains(p) ? storyCursor : -1;
            if (cur < 0) for (int i = 0; i < steps.Count; i++) if (steps[i].zone.rect.Contains(p)) { cur = i; break; }
            int next = Mathf.Clamp(cur < 0 ? (step > 0 ? 0 : steps.Count - 1) : cur + step, 0, steps.Count - 1);
            JumpToStep(steps, next);
        }

        /// <summary>Go to main story step `next` - set the done flags + open the gates of every earlier step, put Ren in that step's area.</summary>
        private void JumpToStep(List<StoryStep> steps, int next)
        {
            storyCursor = next;
            for (int i = 0; i < next; i++)
            {
                foreach (var f in steps[i].done.Split(','))
                    if (f.Trim().Length > 0) Flags.Set(f.Trim());
                foreach (var g in gateCtl.All) if (g.zoneId == steps[i].zone.id && !g.closeOnEnter) gateCtl.Open(g);
            }
            var r = steps[next].zone.rect;
            player.position = FindFreeSpot(r.center, r, player.position.z);
            Hints.Show($"[TEST] Story {steps[next].order:0.0#} - {steps[next].zone.title}  ({next + 1}/{steps.Count})", 2f);
        }

        /// <summary>Main (non-side) areas in Tiled creation order.</summary>
        private List<string> MainZonesByTiledOrder()
        {
            var zones = layout.Zones;
            var ids = new List<string>();
            foreach (var id in layout.ZoneOrder) if (!zones[id].optional) ids.Add(id);
            ids.Sort((a, b) => zones[a].order.CompareTo(zones[b].order));
            return ids;
        }

        /// <summary>Map without story marks: jump areas in Tiled creation order (skipping side areas), opening the gates of the areas before it.</summary>
        private void StepZoneByTiledOrder(int step)
        {
            var ids = MainZonesByTiledOrder();
            if (ids.Count == 0) { Hints.Show("[TEST] Map has no zones (logic layers hidden in Tiled?)", 2.5f); return; }
            int cur = -1;
            for (int i = 0; i < ids.Count; i++) if (layout.Zones[ids[i]].rect.Contains((Vector2)player.position)) cur = i;
            int next = Mathf.Clamp(cur < 0 && step < 0 ? 0 : cur + step, 0, ids.Count - 1);
            JumpTiled(ids, next);
        }

        private void JumpTiled(List<string> ids, int next)
        {
            for (int i = 0; i < next; i++)
                foreach (var g in gateCtl.All) if (g.zoneId == ids[i] && !g.closeOnEnter) gateCtl.Open(g);
            var r = layout.Zones[ids[next]].rect;
            player.position = FindFreeSpot(r.center, r, player.position.z);
            Hints.Show($"[TEST] Teleported to {Title(layout.Zones[ids[next]])}", 1.5f);
        }

        private static string Title(EncounterZone z) => string.IsNullOrEmpty(z.title) ? z.id : z.title;

        // ------------------------------------------------------------------ area picker (F10), pass area (Shift+F3)
        /// <summary>Smallest area containing point p (nested areas -> the innermost).</summary>
        private EncounterZone ZoneAt(Vector2 p)
        {
            EncounterZone best = null;
            float area = float.MaxValue;
            foreach (var z in layout.Zones.Values)
                if (z.rect.Contains(p) && z.rect.width * z.rect.height < area) { area = z.rect.width * z.rect.height; best = z; }
            return best;
        }

        /// <summary>Id of the area Ren is standing in ("" if outside every area).</summary>
        public string DebugZoneHere()
        {
            if (layout == null || player == null) return "";
            var z = ZoneAt(player.position);
            return z != null ? z.id : "";
        }

        /// <summary>Area list for the F10 panel: main story steps by `story` number first, then the remaining main areas in Tiled order, finally side / secret areas.</summary>
        public List<DebugZoneInfo> DebugZones()
        {
            var list = new List<DebugZoneInfo>();
            if (layout == null) return list;
            var seen = new HashSet<string>();
            string here = DebugZoneHere();
            void Add(EncounterZone z, string tag)
            {
                if (z == null || !seen.Add(z.id)) return;
                list.Add(new DebugZoneInfo { id = z.id, title = Title(z), tag = tag, cleared = z.cleared, here = z.id == here, optional = z.optional });
            }
            foreach (var s in StorySteps()) Add(s.zone, "story " + s.order.ToString("0.0#"));
            foreach (var id in MainZonesByTiledOrder()) Add(layout.Zones[id], "");
            foreach (var id in layout.ZoneOrder) Add(layout.Zones[id], "optional");
            return list;
        }

        /// <summary>(F10 Enter) Go to area `id` like F4: area on the main story -> set the done flags + open the gates of every earlier step;
        /// unmarked main area -> open the gates of the areas before it in Tiled order; side area -> teleport only.</summary>
        public void DebugJumpToZone(string id)
        {
            if (layout == null || string.IsNullOrEmpty(id) || !layout.Zones.TryGetValue(id, out var target)) { Hints.Show("[TEST] Zone not found: " + id, 2f); return; }
            JumpToZoneCore(id, target);
            DebugSkipStoryOutside(target.rect);                                     // after setting the earlier steps' flags
        }

        /// <summary>Skip story scenes that do not belong to the target area (cell, map opening, consequence scenes of earlier steps) - see StoryScenes.DebugSkipOutside.</summary>
        public void DebugSkipStoryOutside(Rect keep)
        {
            if (StoryScenes.Instance != null) StoryScenes.Instance.DebugSkipOutside(keep);
        }

        private void JumpToZoneCore(string id, EncounterZone target)
        {
            var steps = StorySteps();
            for (int i = 0; i < steps.Count; i++) if (steps[i].zone == target) { JumpToStep(steps, i); return; }
            var ids = MainZonesByTiledOrder();
            int k = ids.IndexOf(id);
            if (k >= 0 && steps.Count == 0) { JumpTiled(ids, k); return; }
            player.position = FindFreeSpot(target.rect.center, target.rect, player.position.z);
            Hints.Show($"[TEST] Teleported to {Title(target)}", 1.5f);
        }

        /// <summary>(Shift+F3) PASS the current area: defeat its enemies, caves fully spawned, drop the remaining waves, solve every mechanism / puzzle / minigame
        /// inside the area, set the "done" flags of the area's story steps, the area counts as cleared (gates, exits open). A living boss -> really defeated (relic drops as usual).</summary>
        public void DebugPassZone()
        {
            if (layout == null || player == null) return;
            var z = ZoneAt(player.position);
            if (z == null) { Hints.Show("[TEST] Not inside a zone", 1.5f); return; }
            bool fighting = z.started && !z.cleared;
            foreach (var e in z.alive.ToArray())
                if (e != null && e.Health != null && !e.Health.IsDead) e.Health.TakeDamage(99999f);
            foreach (var c in z.caves) if (c != null) c.DebugFinish();
            if (!z.cleared)
            {
                z.waveIndex = Mathf.Max(z.waveIndex, z.waveOrder.Count - 1);       // no more waves
                z.pending = 0; z.generation++;                                       // cancel the pending spawn
                if (HasBoss(z)) z.bossDown = true;                                   // a boss that has not appeared yet also counts as defeated (the area does not restart)
                if (!fighting)
                {
                    z.started = true;
                    Flags.Set(z.id + "_started");
                    ClearZone(z);
                    RefreshStandIns();
                }
                // fight in progress: UpdateZone clears the area next frame (the boss really falls -> relic / heal as usual)
            }
            if (MapMechanics.Instance != null) MapMechanics.Instance.DebugSolveIn(z.rect);
            for (int i = 0; i < z.storyDone.Length; i++)
                foreach (var f in z.storyDone[i].Split(','))
                    if (f.Trim().Length > 0) Flags.Set(f.Trim());
            Hints.Show("[TEST] Zone passed: " + Title(z), 2f);
        }

        // ------------------------------------------------------------------ go to a BOSS, remove restraints, open gates, use an exit
        /// <summary>One boss fight of the map: an enemy boss (area with a `boss` spawn) or a mechanism boss (Absalom, Burlap's Final Exam).</summary>
        public struct DebugBossInfo
        {
            public string key, title, zoneId;
            public bool cleared;
        }

        /// <summary>The map's boss fights, in story order (`story` step), then Tiled order.</summary>
        public List<DebugBossInfo> DebugBosses()
        {
            var list = new List<DebugBossInfo>();
            if (layout == null) return list;
            var order = new List<EncounterZone>();
            foreach (var st in StorySteps()) if (!order.Contains(st.zone)) order.Add(st.zone);
            foreach (var id in layout.ZoneOrder) if (!order.Contains(layout.Zones[id])) order.Add(layout.Zones[id]);
            var arenas = MapMechanics.Instance != null ? MapMechanics.Instance.DebugBossArenas() : new List<Mechanism>();
            foreach (var z in order)
            {
                if (HasBoss(z))
                {
                    string names = "";
                    foreach (var pt in z.points)
                        if (pt.props != null && pt.props.GetBool(MapSchema.Prop.Boss, false))
                            names += (names.Length > 0 ? " + " : "") + (pt.enemy ?? "").Replace('_', ' ');
                    list.Add(new DebugBossInfo { key = z.id, title = names.Length > 0 ? names : Title(z), zoneId = z.id, cleared = z.cleared });
                }
                foreach (var m in arenas)
                    if (ZoneAt(m.Rect.center) == z)
                        list.Add(new DebugBossInfo { key = m.Name, title = m is AbsalomHallMechanism ? "Absalom (Hall of Naming)" : "Sgt. Burlap - Final Exam", zoneId = z.id, cleared = m.Done });
            }
            foreach (var m in arenas)                                                  // mechanism boss outside every area
                if (ZoneAt(m.Rect.center) == null) list.Add(new DebugBossInfo { key = m.Name, title = m.Name, zoneId = "", cleared = m.Done });
            return list;
        }

        /// <summary>(F4 / F10 BOSSES panel) Go to boss fight `key` (boss area id or boss mechanism name): set the flags of every earlier story step,
        /// open gates, remove the map's restraints (Ren gets his outfit, sword + flask + Mantras back), full HP / flask, put Ren at the arena entrance.
        /// Story scenes outside the boss area are skipped (not shown); the boss intro still plays (F11 to fast-forward).</summary>
        public bool DebugGoToBoss(string key)
        {
            if (layout == null || player == null || string.IsNullOrEmpty(key)) return false;
            var mech = MapMechanics.Instance != null ? MapMechanics.Instance.DebugFind(key) : null;
            EncounterZone zone = null;
            if (mech == null && !layout.Zones.TryGetValue(key, out zone)) { Hints.Show("[TEST] Boss not found in this map: " + key, 2f); return false; }
            if (mech != null) zone = ZoneAt(mech.Rect.center);
            if (zone != null) DebugJumpToZone(zone.id);                               // flags + gates of every earlier step, teleport into the area
            DebugLiftRestraints(false);
            if (mech != null)
            {
                foreach (var f in mech.Props.GetString("requires", "").Split(','))  // e.g. Final Exam needs drill_s1, drill_s2
                    if (f.Trim().Length > 0) Flags.Set(f.Trim());
                var r = mech.Rect;                                                    // stand inside the arena, near the bottom edge (the fight starts by itself)
                player.position = FindFreeSpot(new Vector2(r.center.x, r.yMin + Mathf.Min(1.2f, r.height * 0.25f)), r, player.position.z);
            }
            DebugSkipStoryOutside(zone != null ? zone.rect : mech.Rect);             // after the `requires` flags (their consequence scenes are skipped too)
            if (playerHealth != null) playerHealth.Heal(playerHealth.Max);
            var pc = player.GetComponent<PlayerCombat>();
            if (pc != null) pc.RefillPotions();
            Hints.Show("[TEST] Boss: " + key + (mech != null && mech is DrillYardMechanism ? "  (sound the horn to start)" : ""), 2.5f);
            return true;
        }

        /// <summary>(F6 / going to a boss) Remove the map's restraints: sword + flask + normal outfit back, Mantras unsealed.</summary>
        public string DebugLiftRestraints(bool hint = true)
        {
            var mr = GetComponent<MapRestraint>();
            string done = mr != null ? mr.DebugLift() : "";
            if (hint && done.Length > 0) Hints.Show("[TEST] Restraints lifted (" + done + "): blade, flasks, mantras, normal clothes", 2.5f);
            return done;
        }

        /// <summary>(F5) Open EVERY gate of the map (closed arena lock gates included).</summary>
        public int DebugOpenAllGates()
        {
            if (gateCtl == null) return 0;
            int n = 0;
            foreach (var g in gateCtl.All) { gateCtl.Open(g); n++; }
            return n;
        }

        /// <summary>(Ctrl+F4) GO THROUGH the nearest exit AT ONCE (sets its `requires` flags, clears its area if needed) - changes map right away.</summary>
        public void DebugUseExit()
        {
            MapTrigger best = null;
            float bd = float.MaxValue;
            foreach (var t in triggerHandler.All)
            {
                if (t.type != MapSchema.ObjectType.Exit) continue;
                float d = (t.rect.center - (Vector2)player.position).sqrMagnitude;
                if (d < bd) { bd = d; best = t; }
            }
            if (best == null) { Hints.Show("[TEST] This map has no exit", 1.5f); return; }
            foreach (var f in best.props.GetString(MapSchema.Prop.Requires, "").Split(','))
                if (f.Trim().Length > 0) Flags.Set(f.Trim());
            if (!string.IsNullOrEmpty(best.zoneId) && layout.Zones.TryGetValue(best.zoneId, out var z) && !z.cleared)
            {
                z.started = true; z.bossDown = true;
                ClearZone(z);
            }
            Hints.Show("[TEST] Taking exit: " + best.name, 1.5f);
            triggerHandler.ExitNow(best.name);
        }

        /// <summary>Teleport right next to the first exit (1.2 tiles below it) to test the map change.</summary>
        public void DebugGoToExit()
        {
            foreach (var t in triggerHandler.All)
            {
                if (t.type != MapSchema.ObjectType.Exit) continue;
                var c = t.rect.center + new Vector2(0f, -t.rect.height * 0.5f - tileSize * 1.2f);
                player.position = FindFreeSpot(c, new Rect(c.x - 3f, c.y - 3f, 6f, 6f), player.position.z);
                Hints.Show(triggerHandler.ExitOpen(t) ? "[TEST] At the exit - step in" : "[TEST] At the exit (still sealed - press F3 in the boss zone)", 2f);
                return;
            }
            Hints.Show("[TEST] This map has no exit", 1.5f);
        }

        /// <summary>Free spot near the centre (not overlapping solid colliders) so a teleport never traps the player in an obstacle.</summary>
        private Vector3 FindFreeSpot(Vector2 c, Rect r, float z)
        {
            for (float rad = 0f; rad < Mathf.Max(r.width, r.height); rad += 0.32f)
            {
                int steps = rad <= 0f ? 1 : Mathf.Max(8, Mathf.CeilToInt(rad * 12f));
                for (int s = 0; s < steps; s++)
                {
                    float a = s * Mathf.PI * 2f / steps;
                    var p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rad;
                    if (!r.Contains(p)) continue;
                    bool blocked = false;
                    foreach (var col in Physics2D.OverlapCircleAll(p, 0.3f))
                        if (!col.isTrigger && !col.transform.IsChildOf(player)) { blocked = true; break; }
                    if (!blocked) return new Vector3(p.x, p.y, z);
                }
            }
            return new Vector3(c.x, c.y, z);
        }
    }
}
#endif
