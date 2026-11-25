using System.Collections;
using System.Collections.Generic;
using SuperTiled2Unity;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// DIRECTOR OF ONE MAP - every map scene has exactly one director (reads the Tiled map through SuperTiled2Unity).
    /// Flow: enter an area -> spawn wave 1 -> no enemies left -> next wave -> no waves left -> open gates, set flag "&lt;zone&gt;_cleared".
    ///       Falling -> respawn at the nearest point, the area in progress restarts from scratch.
    /// Files of this class:
    ///   MapEncounterDirector.cs   Inspector, map startup (Start split into steps), Update, area / coordinate lookup, saving
    ///   .Zones.cs                 areas & enemy waves, spawning, stragglers, bosses, pre-placed actors (stand_in)
    ///   .Gates.cs                 key-locked doors, Ren speaking at locked doors, flags -> open gates
    ///   .Respawn.cs               death & respawn, area restart, clearing reset_on_death flags
    ///   .Stages.cs / .AutoRespawn.cs / .DevTools.cs   trials, silent respawn points, test keys
    /// Delegated to other classes: MapLayout (reads the map; layer / property names: MapSchema), WorldFlags (flags), GateController (gates),
    /// EnemySpawner (creates enemies), MapTriggerHandler (campfires, gravestones, pickups, exits), MapMechanics (mechanisms).
    /// </summary>
    public partial class MapEncounterDirector : MonoBehaviour
    {
        [System.Serializable]
        public class EnemyEntry
        {
            public string enemyName;
            public GameObject prefab;
        }

        public Transform mapRoot;
        public List<EnemyEntry> enemies = new List<EnemyEntry>();
        [Tooltip("Size of 1 map tile in Unity units (32 px / 100 PPU)")]
        public float tileSize = 0.32f;
        public float respawnDelay = 2.6f;   // long enough for the death animation (~1.6 s) + lying still
        [Tooltip("Areas with a lock gate (close_on_enter) only start once the player is at least this far inside the gate, so the gate does not close on top of the player")]
        public float lockClearance = 0.75f;
        [Tooltip("Tiles the player must walk into the area (from the area box edge) before the fight starts - standing in the doorway / at the gate / on a strip overlapping the next area does not trigger it. Small areas: at most 30% of the short side. Override per area with the entry_depth property.")]
        public float entryDepthTiles = 2f;
        public bool movePlayerToStart = true;

        public static MapEncounterDirector Instance { get; private set; }

        /// <summary>Map state flags (puzzles, gates, pickups, cleared areas...).</summary>
        public WorldFlags Flags { get; private set; }
        /// <summary>Area-based map fog (drawn by RequiemHUD on the minimap / large map). Null before Start.</summary>
        public MapFog Fog { get; private set; }
        /// <summary>Boss / mini-boss in the current fight (large HP bar on the HUD).</summary>
        public EnemyAI Boss { get; private set; }
        /// <summary>SECOND boss in the same area (duo fight, e.g. Brazen Reliquary + Hollow Champion). The HUD draws a second HP bar;
        /// BossIntro merges them into one intro. Main boss falls -> the partner becomes the main boss.</summary>
        public EnemyAI BossPartner { get; private set; }
        /// <summary>Boss just appeared (with the area hint, so BossIntro shows it after the title card).</summary>
        public event System.Action<EnemyAI, string> BossSpawned;
        public string CurrentZoneTitle { get; private set; }
        /// <summary>The player is standing in a rest point -> may swap skills.</summary>
        public bool AtCheckpoint => player != null && triggerHandler != null && triggerHandler.PlayerAtCheckpoint;
        /// <summary>In the death phase -> respawning (death screen).</summary>
        public bool Respawning => respawning;
        /// <summary>The map's flag group (map property `flag_group`, default = scene name). A main map + side cellar in the same group
        /// keep progress when moving back and forth (GameSession remembers flags when leaving a map).</summary>
        public string FlagGroup { get; private set; } = "";

        /// <summary>In combat - Ren is inside an area in combat with an aggressive boss / enemy, or an aggressive enemy is within 6 units.
        /// InteractionSystem uses it to lock Examine / Read (an accidental T press opening a reading panel mid-fight).</summary>
        public bool InCombat
        {
            get
            {
                if (combatFrame == Time.frameCount) return combatNow;
                combatFrame = Time.frameCount;
                combatNow = false;
                if (player == null || layout == null) return false;
                Vector2 p = player.position;
                foreach (var z in layout.Zones.Values)
                {
                    if (!z.started) continue;                       // cleared areas are still checked: enemies released later (cell lever) into a cleared area
                    bool inside = !z.cleared && z.rect.Contains(p);
                    foreach (var e in z.alive)
                    {
                        if (e == null || e.Health == null || e.Health.IsDead) continue;
                        if ((inside && (e.IsBoss || e.Aggro)) || (e.Aggro && ((Vector2)e.transform.position - p).sqrMagnitude < 36f))
                            return combatNow = true;
                    }
                }
                return false;
            }
        }
        private int combatFrame = -1;

        /// <summary>A mechanism TEMPORARILY closes / opens a gate by name (does not change the gate's "opened" state; reloading the map restores it)
        /// - e.g. the Drill Yard locks the player in during a test. Returns false if there is no gate with that name.</summary>
        public bool SetGateClosed(string gateName, bool closed)
        {
            if (gateCtl == null || string.IsNullOrEmpty(gateName)) return false;
            bool any = false;
            foreach (var g in gateCtl.All) if (g.name == gateName) { gateCtl.SetClosed(g, closed); any = true; }
            return any;
        }

        /// <summary>The quest target is inside the area Ren is fighting in -> the HUD hides the arrow / minimap target (already there).</summary>
        public bool TargetReachedInFight(Vector3 target)
        {
            if (player == null || !InCombat) return false;
            return TryGetZoneAt(player.position, out _, out var r) && r.Contains((Vector2)target);
        }
        private bool combatNow;
        public float RespawnDelay => respawnDelay;

        internal Transform Player => player;

        private MapLayout layout;
        private GateController gateCtl;
        private EnemySpawner spawner;
        private MapTriggerHandler triggerHandler;
        private Transform player;
        private Health playerHealth;
        private Vector3 respawnPoint;
        private bool respawning;

        /// <summary>An area was just RESTARTED (player fell / unfinished trial respawned) - passes the area box.
        /// Decorations changed by a boss (Isolde's broken / swapped coffins) listen to this event to rebuild themselves.</summary>
        public static event System.Action<Rect> ZoneReset;
        internal static void RaiseZoneReset(Rect r) => ZoneReset?.Invoke(r);

        private void Awake()
        {
            Instance = this;
            // area-based sound - Core.Sfx asks which area this point belongs to
            SoulRequiem.Core.Sfx.ZoneOf = p => TryGetZoneAt(p, out var zid, out _) ? zid : null;
            Flags = new WorldFlags(OnFlagSet);
            Hints.Clear();                   // hints from the previous scene do not carry over
            CutsceneState.Reset(); TutorialPrompt.Hide();
            KeyRing.Clear();                 // the inventory belongs to each map
        }

        private void OnDestroy()
        {
            if (Instance == this) { Instance = null; SoulRequiem.Core.Sfx.ZoneOf = null; }
        }

        private static string SceneName => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        // ================================================================== map startup (in exact order)
        private void Start()
        {
            if (!FindPlayerAndMap()) { enabled = false; return; }
            layout = MapLayout.Read(mapRoot);
            ReadMapProperties();
            EnsureMechanics();
            gateCtl = new GateController(layout.Gates);
            spawner = new EnemySpawner(enemies, tileSize);
            triggerHandler = new MapTriggerHandler(this, layout.Triggers);

            // Continue -> load the save (flags first, so gates / scenes / villagers / scroll skills start in the right state)
            bool loaded = SaveGame.TryTake(SceneName, out var save);
            bool restore = LoadFlags(loaded, save);
            triggerHandler.RegisterSigns();
            triggerHandler.RegisterPickups();       // pickups = come close and press T
            triggerHandler.RegisterExits();         // exits used with T (climbing a rope, climbing down a hole)
            triggerHandler.ApplyScrollLocks();      // skills inside unpicked scrolls -> not owned yet
            PrepareZones();
            if (restore) RestoreZonesFromFlags();
            ReequipWeapon();
            gateCtl.ApplyStartState();
            RefreshStandIns();                      // bosses / enemies standing in place before the fight (stand_in)
            OnFlagSet(false);                       // flags may already be set before Start (mechanisms starting early)
            PlacePlayer(loaded, save);
            if (restore) UndoZoneFlagsOnLoad();
            CreateFog();                            // AFTER loading flags: revealed areas show clearly at once

            // reaching a chapter entrance for the first time (including New Game into Map 1) -> autosave at once: Continue / Chapters always have a point to return to
            if (!loaded && SaveGame.IsNewChapter(SceneName)) SaveProgress();
            Debug.Log($"MapEncounterDirector: {layout.Zones.Count} areas, {layout.Gates.Count} gates, {layout.Triggers.Count} triggers.");
            foreach (var g in layout.Gates)
                if (g.closeOnEnter) Debug.Log($"MapEncounterDirector: lock gate '{g.name}' of {g.zoneId} at {g.bounds}");
        }

        private bool FindPlayerAndMap()
        {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc == null) { Debug.LogError("MapEncounterDirector: PlayerController not found."); return false; }
            player = pc.transform;
            playerHealth = pc.GetComponent<Health>();
            respawnPoint = player.position;
            if (mapRoot == null)
            {
                var map = FindFirstObjectByType<SuperMap>();
                if (map != null) mapRoot = map.transform;
            }
            if (mapRoot == null) { Debug.LogError("MapEncounterDirector: no map (SuperMap) in the scene."); return false; }
            return true;
        }

        /// <summary>SuperMap of the map (mapRoot may be the map itself, its parent or its child).</summary>
        private SuperMap FindSuperMap()
        {
            var sm = mapRoot.GetComponent<SuperMap>();
            if (sm == null) sm = mapRoot.GetComponentInParent<SuperMap>();
            if (sm == null) sm = mapRoot.GetComponentInChildren<SuperMap>();
            return sm;
        }

        /// <summary>Whole-map properties: flag_group (flag group), disarm_until / mantra_seal_until (map restraints - MapRestraint).</summary>
        private void ReadMapProperties()
        {
            var sm = FindSuperMap();
            var mp = sm != null ? sm.GetComponent<SuperCustomProperties>() : null;
            FlagGroup = mp != null ? mp.GetString(MapSchema.Prop.FlagGroup, SceneName) : SceneName;
            if (string.IsNullOrEmpty(FlagGroup)) FlagGroup = SceneName;
            string disarm = mp != null ? mp.GetString("disarm_until", "") : "", seal = mp != null ? mp.GetString("mantra_seal_until", "") : "";
            if ((disarm.Length > 0 || seal.Length > 0) && GetComponent<MapRestraint>() == null)
                gameObject.AddComponent<MapRestraint>().Setup(this, disarm, seal, mp.GetString("disarm_text", ""), mp.GetString("mantra_seal_text", ""));
        }

        /// <summary>Load flags: the save (Continue) or the remembered flags of the map group (returning to a visited map), learned books, the saved skill set.
        /// Returns true if progress was restored.</summary>
        private bool LoadFlags(bool loaded, SaveGame.Data save)
        {
            if (loaded) foreach (var f in save.flags) Flags.Set(f);
            var recalled = loaded ? null : GameSession.RecallGroup(FlagGroup);
            if (recalled != null) foreach (var f in recalled) Flags.Set(f);
            foreach (var b in GameSession.Books) Flags.Set(b);
            if (loaded && save.owned != null && save.owned.Length > 0)
            {
                GameSession.SetLoadout(save.owned, save.equipped);
                var lo = player.GetComponent<PlayerLoadout>();
                if (lo != null) { lo.owned.Clear(); for (int i = 0; i < PlayerLoadout.SlotCount; i++) lo.slots[i] = null; GameSession.ApplyLoadout(lo); }
            }
            return loaded || recalled != null;
        }

        /// <summary>Caves placed on the map from the start; wave order of each area.</summary>
        private void PrepareZones()
        {
            foreach (var z in layout.Zones.Values)
            {
                foreach (var p in z.points) if (p.IsCave) z.caves.Add(spawner.CreateCave(p));
                var waves = new SortedSet<int>();
                foreach (var p in z.points) if (!p.eventWave && !p.IsCave) waves.Add(p.wave);
                z.waveOrder.AddRange(waves);
            }
        }

        /// <summary>Restore progress: cleared areas do not respawn, pickups taken / campfires lit, unfinished trials respawn.</summary>
        private void RestoreZonesFromFlags()
        {
            foreach (var z in layout.Zones.Values)
                if (Flags.Has(z.id + "_cleared"))
                {
                    z.started = z.cleared = true;
                    foreach (var c in z.caves) if (c != null) c.DebugFinish();
                }
            triggerHandler.RestoreFromFlags();
            ApplyStagesOnLoad();
        }

        /// <summary>Re-equip the playthrough's katana (from the save / carried over from the previous map); lost -> back to the starting blade.</summary>
        private void ReequipWeapon()
        {
            var kit = player != null ? player.GetComponent<PlayerCombat>() : null;
            if (kit == null) return;
            var w = WeaponRing.Find(WeaponRing.Equipped);
            if (w != null && WeaponRing.Has(Flags, w.id)) kit.Equip(w);
            else if (!string.IsNullOrEmpty(WeaponRing.Equipped)) kit.Equip(null);
        }

        /// <summary>Place Ren: the player_start point -> the spot named by the previous map's exit (arrive_at) -> the saved spot. Loading a save also resets the flask.</summary>
        private void PlacePlayer(bool loaded, SaveGame.Data save)
        {
            if (triggerHandler.TryGetPlayerStart(out var start))
            {
                respawnPoint = new Vector3(start.x, start.y, player.position.z);
                if (movePlayerToStart) player.position = respawnPoint;
            }
            if (GameSession.TakeArrival(out var arriveTile) && !loaded)
            {
                respawnPoint = TileToWorld(arriveTile.x, arriveTile.y);
                player.position = respawnPoint;
            }
            if (!loaded) return;
            if (!save.noPoint)                                         // Chapters entering a chapter whose campfire is unlit -> keep the map entrance
            {
                respawnPoint = new Vector3(save.rx, save.ry, player.position.z);
                player.position = respawnPoint;
            }
            var kit = player.GetComponent<PlayerCombat>();
            if (kit != null)
            {
                kit.maxPotions = Mathf.Clamp(save.maxPotions, 1, PlayerCombat.MaxFlaskSlots);
                if (save.healPercent > 0f) kit.healPercent = save.healPercent;
                kit.RefillPotions();
                if (!string.IsNullOrEmpty(save.weapon)) WeaponRing.SetEquipped(save.weapon);
            }
            Hints.Show(string.IsNullOrEmpty(save.note) ? "Progress restored - you wake by the last fire you lit." : save.note, 3.5f);
            Debug.Log($"[Save] Loaded {save.flags.Length} flags, respawn at {respawnPoint}.");
        }

        /// <summary>Area-based map fog (minimap / large map).</summary>
        private void CreateFog()
        {
            var superMap = FindSuperMap();
            if (superMap != null && superMap.m_Width > 0 && superMap.m_Height > 0)
                Fog = new MapFog(layout, mapRoot, tileSize, superMap.m_Width, superMap.m_Height, Flags);
        }

        /// <summary>Add the map's components from the map content: mechanisms ("mechanics" layer), villagers ("npcs" layer), boss title cards,
        /// boss relics, arenas, story scenes, and the per-point scenes by Tiled point name (intro_cam_1, landing_point...).
        /// Adding a new point-based scene: add one line `if (HasPoint("name")) AddOnce&lt;Scene&gt;();`.</summary>
        private void EnsureMechanics()
        {
            bool mech = false, npcs = false;
            foreach (var t in mapRoot.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "mechanics" || t.name == MapMechanics.ArtLayer) mech = true;
                else if (t.name == "npcs") npcs = true;
            }
            if (mech && FindFirstObjectByType<MapMechanics>() == null) gameObject.AddComponent<MapMechanics>();
            AddOnce<BossIntro>();                                      // boss title card when a boss appears
            AddOnce<BossRelic>();                                      // relic dropped by a boss (relic_skill)
            AddOnce<ArenaSeal>();                                      // boss arena: blocks the exits (invisible)
            if (npcs && FindFirstObjectByType<VillagerDirector>() == null) gameObject.AddComponent<VillagerDirector>();
            AddOnce<StoryScenes>();                                    // story scenes written as Tiled data (type "story")
            if (HasPoint("intro_cam_1", true)) { AddOnce<OpeningCutscene>(); AddOnce<TutorialDirector>(); }   // Map 1 opening scene
            if (HasPoint("aftermath_door")) AddOnce<RaidAftermath>();  // scene after the raid
            if (HasPoint("landing_point")) AddOnce<LandingCutscene>(); // landing at the start of Map 2
            if (HasPoint("eye_glimpse")) AddOnce<EyeGlimpse>();        // first glimpse of the Eye (Cistern Gate)
            if (HasPoint("escape_surge")) AddOnce<EscapeSurge>();      // escape after defeating the Eye
            SetupRingPillars();
        }

        private void AddOnce<T>() where T : Component
        {
            if (GetComponent<T>() == null) gameObject.AddComponent<T>();
        }

        /// <summary>Does the map have a Tiled object named `tiledName` (orTransformName: GameObjects with the same name count too).</summary>
        private bool HasPoint(string tiledName, bool orTransformName = false)
        {
            foreach (var so in mapRoot.GetComponentsInChildren<SuperObject>(true))
                if (so.m_TiledName == tiledName) return true;
            if (orTransformName)
                foreach (var t in mapRoot.GetComponentsInChildren<Transform>(true))
                    if (t.name == tiledName) return true;
            return false;
        }

        /// <summary>Arena stone pillars (property pillar = true) -> RingPillar; pillar_visual = name of the pillar prop.</summary>
        private void SetupRingPillars()
        {
            foreach (var so in mapRoot.GetComponentsInChildren<SuperObject>(true))
            {
                var p = so.GetComponent<SuperCustomProperties>();
                if (p == null || !p.GetBool("pillar", false)) continue;
                if (so.GetComponent<RingPillar>() != null) continue;
                var rp = so.gameObject.AddComponent<RingPillar>();
                string vis = p.GetString("pillar_visual", "");
                if (!string.IsNullOrEmpty(vis))
                    foreach (var o in mapRoot.GetComponentsInChildren<SuperObject>(true))
                        if (o.m_TiledName == vis) { rp.visual = o.gameObject; break; }
            }
        }

        // ------------------------------------------------------------------ loop
        private void Update()
        {
            if (player == null) return;
            Vector2 pos = player.position;

            foreach (var z in layout.Zones.Values)
            {
                if (z.rect.Contains(pos))
                {
                    CurrentZoneTitle = z.title;
                    if (!z.started && ReadyToStart(z, pos)) StartZone(z);
                }
                if (z.started && !z.cleared) UpdateZone(z);
            }

            triggerHandler.Tick(pos);
            TickAutoRespawn(pos);                                        // entering an area -> record a silent respawn point
            TickGateLocks(pos);
            Fog?.Tick(player.position);

            if (playerHealth != null && playerHealth.IsDead && !respawning) StartCoroutine(Respawn());
        }

        // ================================================================== area lookup (ArenaSeal, area sound, HUD...)
        /// <summary>Area (id + box) containing point `p` - the smallest one when they overlap.</summary>
        internal bool TryGetZoneAt(Vector2 p, out string id, out Rect rect)
        {
            id = null; rect = default;
            if (layout == null) return false;
            float best = float.MaxValue;
            foreach (var z in layout.Zones.Values)
                if (z.rect.Contains(p) && z.rect.width * z.rect.height < best) { best = z.rect.width * z.rect.height; id = z.id; rect = z.rect; }
            return id != null;
        }

        /// <summary>Area with a gate that closes on entry (close_on_enter) - already a sealed arena.</summary>
        internal bool ZoneHasLockGate(string id)
        {
            if (gateCtl == null) return false;
            foreach (var g in gateCtl.All) if (g.zoneId == id && g.closeOnEnter) return true;
            return false;
        }

        internal bool ZoneCleared(string id) => layout != null && layout.Zones.TryGetValue(id, out var z) && z.cleared;

        // ------------------------------------------------------------------ coordinates
        /// <summary>A story scene leaves the map through a named exit (StoryScenes `exit` command).</summary>
        public void ExitNow(string exitName) { if (triggerHandler != null) triggerHandler.ExitNow(exitName); }

        /// <summary>Convert Tiled tile coordinates (x right, y down) to world coordinates.</summary>
        public Vector3 TileToWorld(float tx, float ty)
        {
            var p = mapRoot != null ? mapRoot.TransformPoint(new Vector3(tx * tileSize, -ty * tileSize, 0f))
                                    : new Vector3(tx * tileSize, -ty * tileSize, 0f);
            return new Vector3(p.x, p.y, player != null ? player.position.z : 0f);
        }

        // ================================================================== respawn point / save
        internal void SetRespawnPoint(Vector2 pos) => respawnPoint = new Vector3(pos.x, pos.y, player.position.z);
        /// <summary>Save progress (called when lighting / resting at a rest point).</summary>
        public void SaveProgress() => SaveGame.Save(this, respawnPoint);
    }
}
