using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// AREAS & ENEMY WAVES of MapEncounterDirector:
    ///   ReadyToStart (must walk entryDepthTiles tiles in, on the right side of a lock gate) -> StartZone -> NextWave (spawns with a dust telegraph, bosses spawn at once)
    ///   UpdateZone (no enemies left -> next wave / ClearZone). Boss areas: Boss / BossPartner, boss defeated -> full HP + flask (RestoreAfterBoss).
    ///   Stragglers (CheckStragglers): ≤ 2 left, no combat for 4 s -> they rush toward Ren. Pre-placed actors: RefreshStandIns.
    /// </summary>
    public partial class MapEncounterDirector
    {
        // ------------------------------------------------------------------ areas & enemy waves
        /// <summary>Arena area: only locks once the player has fully crossed the gate line
        /// (a lock gate right on the area edge would pop up on top of the player at the first touch and push him out).</summary>
        private bool ReadyToStart(EncounterZone z, Vector2 pos)
        {
            if (!string.IsNullOrEmpty(z.startWhen) && !Flags.HasAll(z.startWhen)) return false;
            // area boxes often overlap the neighbouring area by 1 tile
            // (Map 3: Commons/Penitents' Garden x92-93, Commons/Avenue x43-44) and the box edge sits right at the entrance -> standing in the doorway counted as
            // "inside". The fight only starts once the player is entryDepthTiles tiles inside from every box edge.
            float depth = Mathf.Min((z.entryDepth >= 0f ? z.entryDepth : entryDepthTiles) * tileSize, 0.3f * Mathf.Min(z.rect.width, z.rect.height));
            if (pos.x < z.rect.xMin + depth || pos.x > z.rect.xMax - depth || pos.y < z.rect.yMin + depth || pos.y > z.rect.yMax - depth) return false;
            if (z.holdNearRespawn)
            {
                // respawning at a campfire inside the area -> enemies do not burst out the moment he wakes; the fight resumes after leaving the campfire by > 2 units
                if (((Vector2)respawnPoint - pos).magnitude < RespawnHoldDistance) return false;
                z.holdNearRespawn = false;
            }
            foreach (var g in gateCtl.All)
            {
                if (!g.closeOnEnter || g.zoneId != z.id) continue;
                if (DistanceToRect(g.bounds, pos) < lockClearance) return false;
                // must stand on the SAME SIDE of the gate as the arena centre (area V11 also covered the corridor in front of the gate ->
                // the lock gate slammed shut in front of the player while still outside, so the boss could not be reached).
                Vector2 gc = g.bounds.center, zc = z.rect.center;
                bool horizontal = g.bounds.width >= g.bounds.height;
                // a tall narrow gate set in a HORIZONTAL wall (e.g. the 2x4 tile arch in the Warden Gallery corridor, Map 4) -> the area centre is offset mostly in y:
                // check the side by y, otherwise Ren standing in the right half of the corridor / room counts as "outside the gate" and the fight never starts.
                if (!horizontal && Mathf.Abs(zc.y - gc.y) > Mathf.Abs(zc.x - gc.x) * 2f) horizontal = true;
                float side = horizontal ? (pos.y - gc.y) * (zc.y - gc.y) : (pos.x - gc.x) * (zc.x - gc.x);
                if (side < 0f) return false;
            }
            return true;
        }

        private static float DistanceToRect(Rect r, Vector2 p)
        {
            float dx = Mathf.Max(r.xMin - p.x, 0f, p.x - r.xMax);
            float dy = Mathf.Max(r.yMin - p.y, 0f, p.y - r.yMax);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>Start the area now, first wave spawns INSTANTLY (no dust telegraph) - for cutscenes that need to show the area's enemies
        /// before the player walks in (Map 1 opening: goblins burning a house). false if the area does not exist / already started / already cleared.</summary>
        public bool ForceStartZone(string id)
        {
            if (layout == null || string.IsNullOrEmpty(id) || !layout.Zones.TryGetValue(id, out var z) || z.started || z.cleared) return false;
            forceInstant = true;
            try { StartZone(z); } finally { forceInstant = false; }
            return true;
        }
        private bool forceInstant;

        private void StartZone(EncounterZone z)
        {
            z.started = true;
            z.bossDown = false;
            if (!string.IsNullOrEmpty(z.stage)) lastStage = z.stage;   // trial in progress (used to pick the respawn point on death)
            Flags.Set(z.id + "_started");                // flag for mechanisms (e.g. hide the "drinking" Eye when the fight starts)
            // area with a boss in the first wave -> BossIntro shows the hint after the title card
            if (!string.IsNullOrEmpty(z.hint) && !(BossSpawned != null && BossInFirstWave(z))) Hints.Show(z.hint, 4f);
            gateCtl.SetArenaClosed(z.id, true);          // lock the arena
            RefreshStandIns();                           // remove the pre-placed actors - the real enemies spawn in the same spots
            NextWave(z);
        }

        private static bool BossInFirstWave(EncounterZone z)
        {
            if (z.waveOrder.Count == 0) return false;
            int w = z.waveOrder[0];
            foreach (var p in z.points)
                if (!p.eventWave && !p.IsCave && p.wave == w && p.props != null && p.props.GetBool(MapSchema.Prop.Boss, false)) return true;
            return false;
        }

        private void NextWave(EncounterZone z)
        {
            z.quietSince = -1f; z.stragglersCalled = false; z.fought = false;
            z.waveIndex++;
            if (z.waveIndex >= z.waveOrder.Count) return;
            int w = z.waveOrder[z.waveIndex];
            // boss fights spawn at once (BossIntro needs the boss + minions on the next frame); normal areas: one by one, with a telegraph
            bool instant = HasBossPoint(z) || forceInstant;
            int k = 0;
            foreach (var p in z.points)
            {
                if (p.eventWave || p.IsCave || p.wave != w) continue;
                if (SpawnSkipped(p)) continue;                           // skip_when (e.g. a specimen already released early with a key)
                if (instant) Spawn(z, p);
                else StartCoroutine(SpawnTelegraphed(z, p, k++ * StaggerGap + (p.props != null ? p.props.GetFloat("delay", 0f) : 0f)));
            }
        }

        // ---- telegraphed spawning (rolling dust ~0.6 seconds), staggered, never right next to the player
        private const float StaggerGap = 0.5f, TelegraphTime = 0.6f, MinSpawnDistance = 1.5f;

        /// <summary>Spawn point with `skip_when` = flag -> no longer spawns (an empty wave makes the area move to the next wave / clear by itself).</summary>
        private bool SpawnSkipped(SpawnPoint p)
        {
            string s = p.props != null ? p.props.GetString("skip_when", "") : "";
            return s.Length > 0 && Flags != null && Flags.HasAll(s);
        }

        private static bool HasBossPoint(EncounterZone z)
        {
            foreach (var p in z.points) if (p.props != null && p.props.GetBool(MapSchema.Prop.Boss, false)) return true;
            return false;
        }

        private IEnumerator SpawnTelegraphed(EncounterZone z, SpawnPoint p, float delay)
        {
            z.pending++;
            int gen = z.generation;
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (gen != z.generation) yield break;
            Vector3 pos = SafeSpawnPos(p.pos);
            var sr = player != null ? player.GetComponent<SpriteRenderer>() : null;
            var fx = SpriteVfx.Spawn("m3_dash_dust", pos, 0.9f, sr != null ? sr.sortingLayerID : 0, 1, false, 12f);
            if (fx != null) fx.Looping(TelegraphTime);
            yield return new WaitForSeconds(TelegraphTime);
            if (fx != null) fx.Stop();
            if (gen != z.generation) yield break;
            z.pending--;
            Spawn(z, p, pos);
        }

        /// <summary>Spawn point closer than 1.5 units to the player -> pushed out to 1.5 units (if the new spot is free of colliders), otherwise kept.</summary>
        private Vector3 SafeSpawnPos(Vector3 pos)
        {
            if (player == null) return pos;
            Vector2 d = (Vector2)(pos - player.position);
            if (d.sqrMagnitude >= MinSpawnDistance * MinSpawnDistance) return pos;
            Vector2 dir = d.sqrMagnitude > 0.0001f ? d.normalized : Random.insideUnitCircle.normalized;
            Vector2 cand = (Vector2)player.position + dir * MinSpawnDistance;
            foreach (var c in Physics2D.OverlapCircleAll(cand, 0.15f))
                if (c != null && !c.isTrigger && c.attachedRigidbody == null) return pos;   // blocked by a wall / obstacle -> keep the old spot
            return new Vector3(cand.x, cand.y, pos.z);
        }

        private void UpdateZone(EncounterZone z)
        {
            int before = z.alive.Count;
            z.alive.RemoveAll(e => e == null || e.Health.IsDead);
            z.killed += before - z.alive.Count;
            CheckStragglers(z);
            if (z.alive.Count > 0 || z.pending > 0) return;
            if (z.waveIndex < z.waveOrder.Count - 1) { NextWave(z); return; }
            foreach (var c in z.caves) if (c != null && !c.Done) return;
            // boss area where the boss vanished without falling (removed midway) -> NOT a win: restart the area
            if (HasBoss(z) && !z.bossDown)
            {
                Debug.LogWarning("[Map] Khu boss " + z.id + " has no enemies left but the boss has not fallen -> restarting the area.");
                z.started = false; z.waveIndex = -1; z.killed = 0; z.pending = 0; z.generation++;
                if (gateCtl.HasArenaLock(z.id)) gateCtl.SetArenaClosed(z.id, false);
                RaiseZoneReset(z.rect);
                return;
            }
            ClearZone(z);
        }

        // ---- STRAGGLERS. Without this, 2 second-wave Grave Worms in Map 3b The Drowned Muster spawned in a dark corner, never crawled and only saw Ren
        // within ~3 units -> they sat at their pit like decorations; the player thought everything was dead and the door never opened.
        // Rule for every map: a (non-boss) area with ≤ 2 enemies left, no pending spawns, Ren inside the area and no enemy in combat for 4 seconds
        // -> they rush toward Ren (Provoke: leash removed, worms tunnel over), red dots on the minimap, one reminder line.
        private const float StragglerQuiet = 4f;
        private const int StragglerMax = 2;

        private void CheckStragglers(EncounterZone z)
        {
            // Ren standing at the door (right at the area edge, 1-2 tiles outside the box) still counts as "inside" -> stragglers are still called
            if (z.stragglersCalled || z.pending > 0 || z.alive.Count == 0 || z.alive.Count > StragglerMax || HasBoss(z) || player == null || DistanceToRect(z.rect, player.position) > 1.6f)
            { if (!z.stragglersCalled) z.quietSince = -1f; return; }
            foreach (var e in z.alive)
                if (e != null && (e.Aggro || e.Summoned)) { z.fought = true; z.quietSince = -1f; return; }
            // only call stragglers once this wave HAS fought
            // (Ren already fought in the area and a few are left hiding in corners). Enemies that never turned aggressive are busy with their own thing, waiting for Ren to come close.
            // NextWave resets `fought` = false, and the 2 second-wave Grave Worms never turned aggressive
            // -> they would never be called. Having defeated at least one enemy in the area = has fought.
            if (!z.fought && z.killed == 0) { z.quietSince = -1f; return; }
            if (z.quietSince < 0f) { z.quietSince = Time.time; return; }
            if (Time.time - z.quietSince < StragglerQuiet || CutsceneState.Active) return;
            z.stragglersCalled = true;
            foreach (var e in z.alive) if (e != null && !e.Health.IsDead) e.Provoke();
            Hints.Show("Something still stirs in the dark...", 3f);
        }

        /// <summary>Stragglers have been called (the HUD draws red dots on the minimap).</summary>
        public static IEnumerable<EnemyAI> Stragglers()
        {
            var d = Instance;
            if (d == null || d.layout == null) yield break;
            foreach (var z in d.layout.Zones.Values)
            {
                if (!z.stragglersCalled || z.cleared) continue;
                foreach (var e in z.alive) if (e != null && e.Health != null && !e.Health.IsDead) yield return e;
            }
        }

        private void ClearZone(EncounterZone z)
        {
            z.cleared = true;
            bool opened = gateCtl.OpenForClearedZone(z.id);
            Flags.Set(z.id + "_cleared");
            if (opened) Hints.Show("The gate is open", 2.5f);
            triggerHandler.RevealExits(z.id);
            if (Boss != null && Boss.Health.IsDead) Boss = null;
            bool stageDone = CheckStageComplete(z);                      // last area of the trial -> trial passed (auto full heal + save)
            if (HasBoss(z) && !stageDone) RestoreAfterBoss();
        }

        private static bool HasBoss(EncounterZone z)
        {
            foreach (var p in z.points)
                if (p.props != null && p.props.GetBool(MapSchema.Prop.Boss, false)) return true;
            return false;
        }

        /// <summary>Boss / mini-boss defeated (area with a `boss = true` spawn) -> full HP + flask refilled to the max,
        /// so the player does not have to walk back to a campfire midway. Normal areas do not heal (keeps the flask pressure). Not run when a save loads an already cleared area.</summary>
        private void RestoreAfterBoss()
        {
            if (playerHealth == null || playerHealth.IsDead) return;
            playerHealth.Heal(playerHealth.Max);
            var kit = player != null ? player.GetComponent<PlayerCombat>() : null;
            if (kit != null) kit.RefillPotions();
            StartCoroutine(RestoreHint());
        }

        private IEnumerator RestoreHint()
        {
            yield return new WaitForSeconds(2.6f);                       // after "The gate is open" / the relic scene
            if (playerHealth != null && !playerHealth.IsDead) Hints.Show("Victory steadies you - health and flasks restored.", 3f);
        }

        private void Spawn(EncounterZone z, SpawnPoint p, Vector3? at = null)
        {
            if (p.IsCave) { z.caves.Add(spawner.CreateCave(p)); return; }

            var e = spawner.Create(p.enemy, at ?? p.pos);
            if (e == null) return;
            // flying enemies (Plague Eye): pinned at the spawn spot (floating above the pit), only drift slightly
            if (e.floating) e.bounds = new Rect(p.pos.x - 0.55f, p.pos.y - 0.45f, 1.1f, 0.9f);
            z.alive.Add(e);
            if (p.props.GetBool(MapSchema.Prop.Provoke, false)) e.Provoke();   // ambush - runs at Ren at once
            if (p.props.GetBool(MapSchema.Prop.Boss, false))
            {
                // duo fight - the main boss is still alive in the same area -> this one is the partner boss
                if (Boss != null && Boss != e && Boss.Health != null && !Boss.Health.IsDead && z.alive.Contains(Boss)) BossPartner = e;
                else { Boss = e; BossPartner = null; }
                e.Health.Died += _ =>
                {
                    if (BossPartner != null && BossPartner.Health != null && !BossPartner.Health.IsDead && e == Boss) { Boss = BossPartner; BossPartner = null; }
                    else if (e == BossPartner) BossPartner = null;
                    // a boss area is only "cleared" once EVERY boss of the area has fallen (z.alive still holds the remaining one)
                    z.bossDown = true;
                };
                e.Phase2Reached += _ =>
                {
                    foreach (var q in z.points) if (q.eventWave) Spawn(z, q);
                    // no extra hint - visuals / animation / boss title already tell the player
                };
                BossSpawned?.Invoke(e, z.waveIndex <= 0 ? z.hint : "");
            }
        }

        /// <summary>A boss summons extra enemies into its area (counted in the area: the area is only cleared when no enemies remain).</summary>
        public EnemyAI SpawnSummon(string enemyName, Vector2 pos, EnemyAI owner)
        {
            var e = spawner.Create(enemyName, AtPlayerDepth(pos), "summoned enemy");
            if (e == null) return null;
            e.Provoke();                                   // enemies called by a boss rush Ren at once, instead of waiting until he is in sight
            foreach (var z in layout.Zones.Values)
                if (z.alive.Contains(owner)) { z.alive.Add(e); break; }
            return e;
        }

        /// <summary>Enemy prefab by name (cutscenes build brainless "actors", e.g. Isolde throwing Ren into the Bower).</summary>
        public GameObject EnemyPrefab(string enemyName) => spawner != null ? spawner.Prefab(enemyName) : null;

        /// <summary>A mechanism releases enemies into an area (e.g. opening the wrong cell). The area is only cleared when they die.</summary>
        public EnemyAI SpawnInZone(string enemyName, Vector2 pos, string zoneId)
        {
            var e = spawner.Create(enemyName, AtPlayerDepth(pos));
            if (e != null && !string.IsNullOrEmpty(zoneId) && layout.Zones.TryGetValue(zoneId, out var z)) z.alive.Add(e);
            return e;
        }

        private Vector3 AtPlayerDepth(Vector2 pos) => new Vector3(pos.x, pos.y, player != null ? player.position.z : 0f);

        /// <summary>Area in combat: defeated / total enemies (including waves not yet out and cave enemies). false = area not started / done / missing.</summary>
        public bool ZoneTally(string zoneId, out int slain, out int total)
        {
            slain = total = 0;
            if (layout == null || string.IsNullOrEmpty(zoneId) || !layout.Zones.TryGetValue(zoneId, out var z) || !z.started || z.cleared) return false;
            int alive = 0;
            foreach (var e in z.alive) if (e != null && !e.Health.IsDead) alive++;
            int dead = z.alive.Count - alive;                           // dead but not yet removed from the list (this frame)
            int pending = 0;
            for (int i = z.waveIndex + 1; i < z.waveOrder.Count; i++)
                foreach (var p in z.points) if (!p.eventWave && !p.IsCave && p.wave == z.waveOrder[i] && !SpawnSkipped(p)) pending++;
            int caveSlain = 0, caveTotal = 0;
            foreach (var c in z.caves) if (c != null) { caveTotal += c.Total; caveSlain += c.Spawned - c.AliveCount; }
            slain = z.killed + dead + caveSlain;
            total = z.killed + z.alive.Count + z.pending + pending + caveTotal;
            return total > 0;
        }

        /// <summary>Area already cleared - or no area with that name (an exit not tied to an area).</summary>
        internal bool IsZoneClearedOrMissing(string zoneId) => !layout.Zones.TryGetValue(zoneId, out var z) || z.cleared;

        // ------------------------------------------------------------------ pre-placed actors (stand_in)
        /// <summary>A first-wave spawn point with property `stand_in` = true: while the area has NOT started / is not cleared, an "actor" (sprite + Idle animation only,
        /// no AI / HP / collider) is placed right there - the player sees the boss from afar (e.g. the earthquake scene looking at the Stone Ring). Fight starts -> removed, the real enemy spawns in the same spot.</summary>
        private void RefreshStandIns()
        {
            if (layout == null) return;
            foreach (var z in layout.Zones.Values)
            {
                bool show = !z.started && !z.cleared;
                if (!show) { foreach (var g in z.standIns) if (g != null) Destroy(g); z.standIns.Clear(); continue; }
                if (z.standIns.Count > 0) continue;
                int first = z.waveOrder.Count > 0 ? z.waveOrder[0] : 1;
                foreach (var p in z.points)
                {
                    if (p.eventWave || p.IsCave || p.wave != first || p.props == null || !p.props.GetBool("stand_in", false)) continue;
                    var g = MakeStandIn(p.enemy, p.pos);
                    if (g != null) z.standIns.Add(g);
                }
            }
        }

        private GameObject MakeStandIn(string enemy, Vector3 at)
        {
            var prefab = spawner != null ? spawner.Prefab(enemy) : null;
            if (prefab == null) return null;
            var srcSr = prefab.GetComponentInChildren<SpriteRenderer>(true);
            var srcAn = prefab.GetComponentInChildren<Animator>(true);
            if (srcSr == null) return null;
            var go = new GameObject("StandIn " + enemy);
            go.transform.position = at;
            go.transform.localScale = srcSr.transform.lossyScale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = srcSr.sprite; sr.sharedMaterial = srcSr.sharedMaterial;
            sr.sortingLayerID = srcSr.sortingLayerID; sr.sortingOrder = srcSr.sortingOrder; sr.spriteSortPoint = srcSr.spriteSortPoint;
            if (srcAn != null && srcAn.runtimeAnimatorController != null)
            {
                var an = go.AddComponent<Animator>();
                an.runtimeAnimatorController = srcAn.runtimeAnimatorController;
                foreach (var st in new[] { "Idle_S", "Idle" })
                    if (an.HasState(0, Animator.StringToHash(st))) { an.Play(Animator.StringToHash(st), 0, Random.value); break; }
            }
            return go;
        }
    }
}
