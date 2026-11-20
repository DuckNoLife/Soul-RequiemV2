using System.Collections;
using System.Collections.Generic;
using SuperTiled2Unity;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Điều phối trận đánh của một map Tiled (SuperTiled2Unity). Mỗi scene map có đúng một director.
    ///
    /// Luồng: người chơi bước vào khu -> sinh đợt 1 -> hết quái -> đợt tiếp -> hết đợt -> mở cổng của khu,
    ///        bật cờ "&lt;khu&gt;_cleared". Gục -> hồi sinh ở điểm nghỉ gần nhất, đấu trường dở dang được mở lại.
    ///
    /// Director chỉ giữ LUỒNG KHU / ĐỢT và hồi sinh; phần việc khác giao cho:
    ///   <see cref="MapLayout"/>         đọc map Tiled (tên layer/thuộc tính: <see cref="MapSchema"/>)
    ///   <see cref="WorldFlags"/>        cờ trạng thái (public: <see cref="Flags"/>)
    ///   <see cref="GateController"/>    đóng / mở cổng
    ///   <see cref="EnemySpawner"/>      tạo quái từ tên
    ///   <see cref="MapTriggerHandler"/> điểm nghỉ, bia, vật nhặt, dịch chuyển, lối ra
    ///   MapEncounterDirector.DevTools.cs  lệnh test F2–F4 (chỉ Editor / Development Build)
    /// Gợi ý trên màn hình: <see cref="Hints"/> (Core).
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
        [Tooltip("Kích thước 1 ô map theo đơn vị Unity (32 px / 100 PPU)")]
        public float tileSize = 0.32f;
        public float respawnDelay = 2.6f;   // đủ cho hoạt ảnh gục (~1,6 s) + nằm yên
        [Tooltip("Khu có cổng khóa (close_on_enter) chỉ bắt đầu khi người chơi đã vào sâu cách cổng ít nhất khoảng này, để cổng không đóng đè lên người chơi")]
        public float lockClearance = 0.75f;
        [Tooltip("08/11: số ô phải đi vào trong khu (tính từ mép khung khu) thì trận mới bắt đầu — đứng ở cửa / sát cổng / dải chồng lên khu bên cạnh không kích hoạt. Khu nhỏ: tối đa 30% cạnh ngắn. Ghi đè từng khu bằng thuộc tính entry_depth.")]
        public float entryDepthTiles = 2f;
        public bool movePlayerToStart = true;

        public static MapEncounterDirector Instance { get; private set; }

        /// <summary>Cờ trạng thái của map (câu đố, cổng, nhặt đồ, khu đã dọn...).</summary>
        public WorldFlags Flags { get; private set; }
        /// <summary>05/11: sương bản đồ theo khu (RequiemHUD vẽ lên minimap / bản đồ lớn). Null khi chưa Start.</summary>
        public MapFog Fog { get; private set; }
        /// <summary>Boss / mini-boss đang đánh (thanh máu lớn trên HUD).</summary>
        public EnemyAI Boss { get; private set; }
        /// <summary>19/11 (§344): boss THỨ HAI cùng khu (song đấu, vd Brazen Reliquary + Hollow Champion). HUD vẽ thanh máu thứ hai;
        /// BossIntro gộp làm một màn giới thiệu. Boss chính gục -> boss phụ lên làm boss chính.</summary>
        public EnemyAI BossPartner { get; private set; }
        /// <summary>02/11: boss vừa xuất hiện (kèm gợi ý của khu, để BossIntro hiện sau bảng tên).</summary>
        public event System.Action<EnemyAI, string> BossSpawned;
        public string CurrentZoneTitle { get; private set; }
        /// <summary>Người chơi đang đứng trong điểm nghỉ -> được đổi chiêu.</summary>
        public bool AtCheckpoint => player != null && triggerHandler != null && triggerHandler.PlayerAtCheckpoint;
        /// <summary>Đang trong pha gục -> hồi sinh (màn chết).</summary>
        public bool Respawning => respawning;
        /// <summary>07/11: nhóm cờ của map (thuộc tính map `flag_group`, mặc định = tên scene). Map chính + hầm phụ cùng nhóm thì
        /// đi qua lại vẫn giữ tiến độ (GameSession nhớ cờ khi rời map).</summary>
        public string FlagGroup { get; private set; } = "";

        /// <summary>04/11: đang giao chiến — Ren đứng trong khu đang đánh còn boss / quái đã gây hấn, hoặc có quái gây hấn trong 6 đv.
        /// InteractionSystem dùng để khoá Examine / Read (người chơi bấm T nhầm mở bảng đọc giữa trận).</summary>
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
                    if (!z.started) continue;                       // khu đã dọn vẫn xét: quái thả thêm (cần gạt buồng giam) vào khu đã dọn
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

        /// <summary>04/11: đích nhiệm vụ nằm ngay trong khu Ren đang đánh -> HUD ẩn mũi tên / điểm đích trên minimap (đã tới nơi).</summary>
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

        /// <summary>19/11 (§343): một khu vừa được LÀM LẠI (người chơi gục / ải chưa xong hồi quái) — truyền khung khu.
        /// Đồ trang trí do boss làm thay đổi (quan tài Isolde vỡ / bị tráo chỗ) nghe sự kiện này để dựng lại.</summary>
        public static event System.Action<Rect> ZoneReset;
        internal static void RaiseZoneReset(Rect r) => ZoneReset?.Invoke(r);

        private void Awake()
        {
            Instance = this;
            // 19/11 (§343): âm theo khu — Core.Sfx hỏi điểm này thuộc khu nào
            SoulRequiem.Core.Sfx.ZoneOf = p => TryGetZoneAt(p, out var zid, out _) ? zid : null;
            Flags = new WorldFlags(OnFlagSet);
            Hints.Clear();                   // gợi ý của scene trước không mang sang
            CutsceneState.Reset(); TutorialPrompt.Hide();
            KeyRing.Clear();                 // 04/11: túi đồ là của từng map
        }

        private void OnDestroy()
        {
            if (Instance == this) { Instance = null; SoulRequiem.Core.Sfx.ZoneOf = null; }
        }

        private void Start()
        {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc == null) { Debug.LogError("MapEncounterDirector: không tìm thấy PlayerController."); enabled = false; return; }
            player = pc.transform;
            playerHealth = pc.GetComponent<Health>();
            respawnPoint = player.position;

            if (mapRoot == null)
            {
                var map = FindFirstObjectByType<SuperMap>();
                if (map != null) mapRoot = map.transform;
            }
            if (mapRoot == null) { Debug.LogError("MapEncounterDirector: chưa có map (SuperMap) trong scene."); enabled = false; return; }

            layout = MapLayout.Read(mapRoot);
            {
                // thuộc tính map nằm trên object có SuperMap (mapRoot có thể là chính nó, cha hoặc con của nó)
                var sm = mapRoot.GetComponent<SuperMap>();
                if (sm == null) sm = mapRoot.GetComponentInParent<SuperMap>();
                if (sm == null) sm = mapRoot.GetComponentInChildren<SuperMap>();
                var mp = sm != null ? sm.GetComponent<SuperCustomProperties>() : null;
                string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                FlagGroup = mp != null ? mp.GetString(MapSchema.Prop.FlagGroup, sceneName) : sceneName;
                if (string.IsNullOrEmpty(FlagGroup)) FlagGroup = sceneName;
                // 17/11 (§306): trói theo map (tước kiếm / phong Mantra tới khi đủ cờ) — xem MapRestraint
                string disarm = mp != null ? mp.GetString("disarm_until", "") : "", seal = mp != null ? mp.GetString("mantra_seal_until", "") : "";
                if ((disarm.Length > 0 || seal.Length > 0) && GetComponent<MapRestraint>() == null)
                    gameObject.AddComponent<MapRestraint>().Setup(this, disarm, seal, mp.GetString("disarm_text", ""), mp.GetString("mantra_seal_text", ""));
            }
            EnsureMechanics();
            gateCtl = new GateController(layout.Gates);
            spawner = new EnemySpawner(enemies, tileSize);
            triggerHandler = new MapTriggerHandler(this, layout.Triggers);
            // 02/11: Continue -> nạp bản lưu (cờ trước, để cổng / cảnh / dân làng / chiêu trong cuộn giấy khởi động đúng trạng thái)
            bool loaded = SaveGame.TryTake(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, out var save);
            if (loaded) foreach (var f in save.flags) Flags.Set(f);
            // 07/11: quay lại map đã đi (vd leo từ hầm lên map chính) -> nạp cờ đã nhớ của nhóm map
            var recalled = loaded ? null : GameSession.RecallGroup(FlagGroup);
            if (recalled != null) foreach (var f in recalled) Flags.Set(f);
            bool restore = loaded || recalled != null;
            foreach (var b in GameSession.Books) Flags.Set(b);          // 04/11: sách đã học ở map trước
            if (loaded && save.owned != null && save.owned.Length > 0)   // 04/11: bộ chiêu trong bản lưu
            {
                GameSession.SetLoadout(save.owned, save.equipped);
                var lo = player.GetComponent<PlayerLoadout>();
                if (lo != null) { lo.owned.Clear(); for (int i = 0; i < PlayerLoadout.SlotCount; i++) lo.slots[i] = null; GameSession.ApplyLoadout(lo); }
            }
            triggerHandler.RegisterSigns();
            triggerHandler.RegisterPickups();       // 18/11 (§326): đồ nhặt = lại gần nhấn T
            triggerHandler.RegisterExits();          // 08/11 (§190): lối ra nhấn T (leo dây, trèo xuống hố)
            triggerHandler.ApplyScrollLocks();       // 02/11: chiêu trong cuộn giấy chưa nhặt -> chưa có

            foreach (var z in layout.Zones.Values)
            {
                // Hang động có sẵn trên map ngay từ đầu (không đợi vào khu mới hiện ra)
                foreach (var p in z.points) if (p.IsCave) z.caves.Add(spawner.CreateCave(p));

                var waves = new SortedSet<int>();
                foreach (var p in z.points) if (!p.eventWave && !p.IsCave) waves.Add(p.wave);
                z.waveOrder.AddRange(waves);
            }
            if (restore)
            {
                foreach (var z in layout.Zones.Values)
                    if (Flags.Has(z.id + "_cleared"))
                    {
                        z.started = z.cleared = true;                  // khu đã dọn: không sinh quái lại
                        foreach (var c in z.caves) if (c != null) c.DebugFinish();
                    }
                triggerHandler.RestoreFromFlags();                 // đồ đã nhặt, lửa trại đã thắp
                ApplyStagesOnLoad();                               // 08/11: ải chưa qua -> khu của ải hồi quái (giữ cờ)
            }
            // 10/11 (§217): đeo lại thanh katana của lượt chơi (bản lưu / mang từ map trước); mất thanh thì về thanh mở màn
            {
                var kit2 = player != null ? player.GetComponent<PlayerCombat>() : null;
                if (kit2 != null)
                {
                    var w = WeaponRing.Find(WeaponRing.Equipped);
                    if (w != null && WeaponRing.Has(Flags, w.id)) kit2.Equip(w);
                    else if (!string.IsNullOrEmpty(WeaponRing.Equipped)) kit2.Equip(null);
                }
            }
            gateCtl.ApplyStartState();
            RefreshStandIns();                // 16/11 (§271): boss / quái đứng sẵn trước trận (stand_in)
            OnFlagSet(false);                // cờ có thể đã bật trước Start (cơ quan khởi động sớm)

            if (triggerHandler.TryGetPlayerStart(out var start))
            {
                respawnPoint = new Vector3(start.x, start.y, player.position.z);
                if (movePlayerToStart) player.position = respawnPoint;
            }
            // 07/11: lối ra của map trước chỉ định chỗ tới (arrive_at) — chỉ khi không nạp bản lưu
            if (GameSession.TakeArrival(out var arriveTile) && !loaded)
            {
                respawnPoint = TileToWorld(arriveTile.x, arriveTile.y);
                player.position = respawnPoint;
            }
            if (loaded)
            {
                respawnPoint = new Vector3(save.rx, save.ry, player.position.z);
                player.position = respawnPoint;
                var kit = player.GetComponent<PlayerCombat>();
                if (kit != null)
                {
                    kit.maxPotions = Mathf.Max(1, save.maxPotions);
                    if (save.healPercent > 0f) kit.healPercent = save.healPercent;
                    kit.RefillPotions();
                    if (!string.IsNullOrEmpty(save.weapon)) WeaponRing.SetEquipped(save.weapon);   // 10/11 (§217)
                }
                Hints.Show("Progress restored - you wake by the last fire you lit.", 3.5f);
                Debug.Log($"[Save] Đã nạp {save.flags.Length} cờ, hồi sinh tại {respawnPoint}.");
            }
            if (restore) UndoZoneFlagsOnLoad();   // 19/11 (§343)

            // 05/11: sương bản đồ — tạo SAU khi nạp cờ bản lưu (khu đã mở hiện rõ ngay)
            var superMap = mapRoot.GetComponent<SuperMap>();
            if (superMap == null) superMap = mapRoot.GetComponentInParent<SuperMap>();
            if (superMap == null) superMap = mapRoot.GetComponentInChildren<SuperMap>();
            if (superMap != null && superMap.m_Width > 0 && superMap.m_Height > 0)
                Fog = new MapFog(layout, mapRoot, tileSize, superMap.m_Width, superMap.m_Height, Flags);

            Debug.Log($"MapEncounterDirector: {layout.Zones.Count} khu, {layout.Gates.Count} cổng, {layout.Triggers.Count} trigger.");
            foreach (var g in layout.Gates)
                if (g.closeOnEnter) Debug.Log($"MapEncounterDirector: cổng khóa '{g.name}' của {g.zoneId} tại {g.bounds}");
        }

        /// <summary>01/11: map có lớp "mechanics" mà scene chưa gắn MapMechanics (vd Map 1 sau khi thêm cơ quan) -> tự gắn.</summary>
        private void EnsureMechanics()
        {
            bool mech = false, npcs = false;
            foreach (var t in mapRoot.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "mechanics" || t.name == MapMechanics.ArtLayer) mech = true;
                else if (t.name == "npcs") npcs = true;
            }
            if (mech && FindFirstObjectByType<MapMechanics>() == null) gameObject.AddComponent<MapMechanics>();
            // 02/11: bảng tên boss khi boss xuất hiện
            if (GetComponent<BossIntro>() == null) gameObject.AddComponent<BossIntro>();
            if (GetComponent<BossRelic>() == null) gameObject.AddComponent<BossRelic>();      // 04/11: cổ vật rơi từ boss (relic_skill)
            // 03/11: đấu trường boss — boss không ra khỏi khu; khu không có cổng tự đóng thì dựng màn chắn Ether ở các lối ra
            if (GetComponent<ArenaSeal>() == null) gameObject.AddComponent<ArenaSeal>();
            // 04/11: màn dừng hướng dẫn giữa đường (Turned Blade, chiêu mới)
            // 08/11: bỏ CombatTutorials (màn J / F) theo yêu cầu người dùng
            // 01/11: dân làng (lớp "npcs") -> VillagerDirector
            if (npcs && FindFirstObjectByType<VillagerDirector>() == null) gameObject.AddComponent<VillagerDirector>();
            // 16/11 (§237): cảnh cốt truyện viết bằng dữ liệu Tiled (object type "story")
            if (GetComponent<StoryScenes>() == null) gameObject.AddComponent<StoryScenes>();
            // 02/11: cảnh mở đầu (map có điểm "intro_cam_1")
            foreach (var t in mapRoot.GetComponentsInChildren<Transform>(true))
                if (t.name == "intro_cam_1" || (t.GetComponent<SuperTiled2Unity.SuperObject>() is var o && o != null && o.m_TiledName == "intro_cam_1"))
                {
                    if (GetComponent<OpeningCutscene>() == null) gameObject.AddComponent<OpeningCutscene>();
                    if (GetComponent<TutorialDirector>() == null) gameObject.AddComponent<TutorialDirector>();   // 02/11: HEAL / MANTRA
                    break;
                }
            // 02/11: cảnh sau trận cướp (map có điểm "aftermath_door")
            foreach (var so in mapRoot.GetComponentsInChildren<SuperTiled2Unity.SuperObject>(true))
                if (so.m_TiledName == "aftermath_door")
                {
                    if (GetComponent<RaidAftermath>() == null) gameObject.AddComponent<RaidAftermath>();
                    break;
                }
            // 03/11: cảnh tiếp đất đầu Map 2 (map có điểm "landing_point")
            foreach (var so in mapRoot.GetComponentsInChildren<SuperTiled2Unity.SuperObject>(true))
                if (so.m_TiledName == "landing_point")
                {
                    if (GetComponent<LandingCutscene>() == null) gameObject.AddComponent<LandingCutscene>();
                    break;
                }
            // 03/11: lần đầu thấy con Mắt ở Cistern Gate (map có điểm "eye_glimpse")
            foreach (var so in mapRoot.GetComponentsInChildren<SuperTiled2Unity.SuperObject>(true))
                if (so.m_TiledName == "eye_glimpse")
                {
                    if (GetComponent<EyeGlimpse>() == null) gameObject.AddComponent<EyeGlimpse>();
                    break;
                }
            // 03/11: cảnh thoát thân sau khi hạ con Mắt (map có điểm "escape_surge")
            foreach (var so in mapRoot.GetComponentsInChildren<SuperTiled2Unity.SuperObject>(true))
                if (so.m_TiledName == "escape_surge")
                {
                    if (GetComponent<EscapeSurge>() == null) gameObject.AddComponent<EscapeSurge>();
                    break;
                }
            // 02/11: cột đá đấu trường (thuộc tính pillar = true) -> RingPillar, pillar_visual = tên prop hình cột
            foreach (var so in mapRoot.GetComponentsInChildren<SuperTiled2Unity.SuperObject>(true))
            {
                var p = so.GetComponent<SuperTiled2Unity.SuperCustomProperties>();
                if (p == null || !p.GetBool("pillar", false)) continue;
                if (so.GetComponent<RingPillar>() != null) continue;
                var rp = so.gameObject.AddComponent<RingPillar>();
                string vis = p.GetString("pillar_visual", "");
                if (!string.IsNullOrEmpty(vis))
                    foreach (var o in mapRoot.GetComponentsInChildren<SuperTiled2Unity.SuperObject>(true))
                        if (o.m_TiledName == vis) { rp.visual = o.gameObject; break; }
            }
        }

        // ------------------------------------------------------------------ vòng lặp
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
            TickAutoRespawn(pos);                                        // 17/11 (§285): vào khu -> ghi điểm hồi sinh ngầm
            TickGateLocks(pos);
            Fog?.Tick(player.position);

            if (playerHealth != null && playerHealth.IsDead && !respawning) StartCoroutine(Respawn());
        }

        // ------------------------------------------------------------------ khu & đợt quái
        /// <summary>Khu đấu trường: chỉ khóa khi người chơi đã đi hẳn qua vạch cổng.
        /// (Lỗi cũ: cổng khóa nằm ngay mép khu, vừa chạm mép là cổng bật lên đè người chơi và đẩy ra ngoài.)</summary>
        private bool ReadyToStart(EncounterZone z, Vector2 pos)
        {
            if (!string.IsNullOrEmpty(z.startWhen) && !Flags.HasAll(z.startWhen)) return false;
            // 08/11 (người dùng: "chưa qua khu, chỉ đứng gần cổng mà quái đã sinh"): khung khu thường chồng 1 ô lên khu bên cạnh
            // (Map 3: Commons/Penitents' Garden x92–93, Commons/Avenue x43–44) và mép khung nằm ngay cửa vào -> đứng ở cửa đã tính là
            // "vào khu". Giờ phải vào sâu entryDepthTiles ô so với mọi mép khung mới bắt đầu trận.
            float depth = Mathf.Min((z.entryDepth >= 0f ? z.entryDepth : entryDepthTiles) * tileSize, 0.3f * Mathf.Min(z.rect.width, z.rect.height));
            if (pos.x < z.rect.xMin + depth || pos.x > z.rect.xMax - depth || pos.y < z.rect.yMin + depth || pos.y > z.rect.yMax - depth) return false;
            if (z.holdNearRespawn)
            {
                // 05/11: hồi sinh ở lửa trại nằm trong khu -> quái không ập ra ngay lúc vừa tỉnh dậy; rời lửa trại > 2 đv mới đánh lại
                if (((Vector2)respawnPoint - pos).magnitude < RespawnHoldDistance) return false;
                z.holdNearRespawn = false;
            }
            foreach (var g in gateCtl.All)
            {
                if (!g.closeOnEnter || g.zoneId != z.id) continue;
                if (DistanceToRect(g.bounds, pos) < lockClearance) return false;
                // 27/10: phải đứng CÙNG PHÍA cổng với tâm đấu trường (lỗi cũ: khu V11 phủ cả hành lang trước cổng ->
                // cổng khoá đóng sập trước mặt người chơi khi họ còn ở ngoài, không vào được boss).
                Vector2 gc = g.bounds.center, zc = z.rect.center;
                bool horizontal = g.bounds.width >= g.bounds.height;
                // 20/11: cổng cao-hẹp đặt trong tường NGANG (vd cổng vòm 2x4 ô ở hành lang Warden Gallery, Map 4) -> tâm khu lệch chủ yếu theo y:
                // xét phía theo y, nếu không Ren đứng nửa phải hành lang / phòng bị coi là "ngoài cổng" và trận không bắt đầu.
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

        /// <summary>11/11 (§229): bắt đầu khu ngay, quái đợt đầu sinh TỨC THÌ (không bụi báo hiệu) — cho cảnh diễn cần thấy quái của khu
        /// trước khi người chơi bước vào (cảnh mở đầu Map 1: goblin đốt nhà). false nếu khu không có / đã bắt đầu / đã dọn.</summary>
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
            z.bossDown = false;                          // 19/11 (§343)
            if (!string.IsNullOrEmpty(z.stage)) lastStage = z.stage;   // 08/11: ải đang đánh (chọn điểm hồi sinh khi gục)
            Flags.Set(z.id + "_started");                // cờ cho cơ quan (vd. ẩn Con Mắt "đang uống" khi trận đấu bắt đầu)
            // 02/11: khu có boss ở đợt đầu -> BossIntro hiện gợi ý sau bảng tên
            if (!string.IsNullOrEmpty(z.hint) && !(BossSpawned != null && BossInFirstWave(z))) Hints.Show(z.hint, 4f);
            gateCtl.SetArenaClosed(z.id, true);          // khóa đấu trường
            RefreshStandIns();                           // 16/11 (§271): bỏ diễn viên đứng sẵn — quái thật sinh đúng chỗ đó
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
            z.quietSince = -1f; z.stragglersCalled = false; z.fought = false;   // 19/11 (§340); 20/11
            z.waveIndex++;
            if (z.waveIndex >= z.waveOrder.Count) return;
            int w = z.waveOrder[z.waveIndex];
            // 02/11: trận boss sinh ngay (BossIntro cần thấy đủ boss + đệ ở khung sau); khu thường: từng con một, có báo hiệu
            bool instant = HasBossPoint(z) || forceInstant;
            int k = 0;
            foreach (var p in z.points)
            {
                if (p.eventWave || p.IsCave || p.wave != w) continue;
                if (SpawnSkipped(p)) continue;                           // 09/11 (§200): skip_when (vd mẫu vật đã thả sớm bằng chìa)
                if (instant) Spawn(z, p);
                else StartCoroutine(SpawnTelegraphed(z, p, k++ * StaggerGap + (p.props != null ? p.props.GetFloat("delay", 0f) : 0f)));
            }
        }

        // ---- 02/11: sinh quái có báo hiệu (bụi cuộn ~0.6 giây), lần lượt cách nhau, không mọc sát người chơi
        private const float StaggerGap = 0.5f, TelegraphTime = 0.6f, MinSpawnDistance = 1.5f;

        /// <summary>09/11 (§200): điểm sinh có `skip_when` = cờ -> không sinh nữa (đợt trống thì khu tự sang đợt sau / dọn xong).</summary>
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

        /// <summary>Điểm sinh cách người chơi &lt; 1.5 đv -> đẩy ra 1.5 đv (nếu chỗ mới không vướng va chạm), không thì giữ nguyên.</summary>
        private Vector3 SafeSpawnPos(Vector3 pos)
        {
            if (player == null) return pos;
            Vector2 d = (Vector2)(pos - player.position);
            if (d.sqrMagnitude >= MinSpawnDistance * MinSpawnDistance) return pos;
            Vector2 dir = d.sqrMagnitude > 0.0001f ? d.normalized : Random.insideUnitCircle.normalized;
            Vector2 cand = (Vector2)player.position + dir * MinSpawnDistance;
            foreach (var c in Physics2D.OverlapCircleAll(cand, 0.15f))
                if (c != null && !c.isTrigger && c.attachedRigidbody == null) return pos;   // vướng tường / vật cản -> giữ chỗ cũ
            return new Vector3(cand.x, cand.y, pos.z);
        }

        private void UpdateZone(EncounterZone z)
        {
            int before = z.alive.Count;
            z.alive.RemoveAll(e => e == null || e.Health.IsDead);
            z.killed += before - z.alive.Count;
            CheckStragglers(z);                                          // 19/11 (§340)
            if (z.alive.Count > 0 || z.pending > 0) return;
            if (z.waveIndex < z.waveOrder.Count - 1) { NextWave(z); return; }
            foreach (var c in z.caves) if (c != null && !c.Done) return;
            // 19/11 (§343): khu boss mà boss biến mất không gục (bị xoá giữa chừng) -> KHÔNG tính là thắng: làm lại khu
            if (HasBoss(z) && !z.bossDown)
            {
                Debug.LogWarning("[Map] Khu boss " + z.id + " hết quái nhưng boss chưa gục -> làm lại khu.");
                z.started = false; z.waveIndex = -1; z.killed = 0; z.pending = 0; z.generation++;
                if (gateCtl.HasArenaLock(z.id)) gateCtl.SetArenaClosed(z.id, false);
                RaiseZoneReset(z.rect);
                return;
            }
            ClearZone(z);
        }

        /// <summary>19/11 (§343): khu đang đánh dở (chưa dọn) có thuộc tính Tiled `reset_on_death` = "cờ,cờ" -> người chơi gục thì GỠ các cờ đó
        /// (vd Isolde: "west_bell_rung,isolde_summoned" — chuông Tây lại phải rung, cảnh Isolde kéo Ren xuống Bower chạy lại, rồi đánh lại từ đầu;
        /// trước đây chuông đã rung + cảnh đã chạy nên không còn đường quay lại trận). Cơ quan gắn với cờ tự mở lại (Mechanism.OnFlagsReset).</summary>
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

        /// <summary>19/11 (§343): nạp bản lưu: khu `reset_on_death` chưa dọn mà điểm hồi sinh nằm NGOÀI khu (thoát game giữa trận, bản lưu cũ)
        /// -> gỡ cờ như khi gục, để không kẹt ngoài đấu trường.</summary>
        private void UndoZoneFlagsOnLoad()
        {
            if (layout == null) return;
            foreach (var z in layout.Zones.Values)
                if (!z.cleared && z.resetOnDeath.Length > 0 && !z.rect.Contains((Vector2)respawnPoint)) UndoFlags(z);
        }

        // ---- 19/11 (§340): QUÁI SÓT. Lỗi gặp ở Map 3b The Drowned Muster: 2 Grave Worm đợt 2 sinh ở góc phòng tối, không bò, chỉ thấy Ren
        // trong ~3 đv -> đứng yên ở miệng hố như đồ trang trí; người chơi tưởng đã hạ hết, cửa không mở.
        // Luật chung mọi map: khu (không phải boss) còn ≤ 2 quái, hết lượt chờ sinh, Ren đứng trong khu và 4 giây không con nào giao chiến
        // -> chúng lao về phía Ren (Provoke: bỏ dây xích, sâu đào hầm tới), hiện chấm đỏ trên minimap, dòng nhắc một lần.
        private const float StragglerQuiet = 4f;
        private const int StragglerMax = 2;

        private void CheckStragglers(EncounterZone z)
        {
            if (z.stragglersCalled || z.pending > 0 || z.alive.Count == 0 || z.alive.Count > StragglerMax || HasBoss(z) || player == null || !z.rect.Contains((Vector2)player.position))
            { if (!z.stragglersCalled) z.quietSince = -1f; return; }
            foreach (var e in z.alive)
                if (e != null && (e.Aggro || e.Summoned)) { z.fought = true; z.quietSince = -1f; return; }
            // 20/11 (người dùng: goblin đầu game chạy cả quãng xa tới chỗ Ren thay vì đang đốt nhà): chỉ gọi quái sót khi đợt này ĐÃ giao chiến
            // (Ren đã đánh nhau trong khu rồi mới còn sót vài con trốn góc). Quái chưa từng gây hấn = đang làm việc của nó, chờ Ren tới gần.
            if (!z.fought) { z.quietSince = -1f; return; }
            if (z.quietSince < 0f) { z.quietSince = Time.time; return; }
            if (Time.time - z.quietSince < StragglerQuiet || CutsceneState.Active) return;
            z.stragglersCalled = true;
            foreach (var e in z.alive) if (e != null && !e.Health.IsDead) e.Provoke();
            Hints.Show("Something still stirs in the dark...", 3f);
        }

        /// <summary>19/11 (§340): quái sót đã được gọi (HUD vẽ chấm đỏ trên minimap).</summary>
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
            bool stageDone = CheckStageComplete(z);                      // 08/11: khu cuối của ải -> qua ải (tự hồi đầy + lưu)
            if (HasBoss(z) && !stageDone) RestoreAfterBoss();
        }

        private static bool HasBoss(EncounterZone z)
        {
            foreach (var p in z.points)
                if (p.props != null && p.props.GetBool(MapSchema.Prop.Boss, false)) return true;
            return false;
        }

        /// <summary>05/11 (người dùng): hạ boss / mini-boss (khu có spawn `boss = true`) -> đầy máu + nạp bình tới mức tối đa,
        /// để không phải quay về lửa trại giữa chừng. Khu thường không hồi (giữ áp lực bình máu). Bản lưu nạp khu đã dọn: không chạy.</summary>
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
            yield return new WaitForSeconds(2.6f);                       // sau "The gate is open" / màn cổ vật
            if (playerHealth != null && !playerHealth.IsDead) Hints.Show("Victory steadies you - health and flasks restored.", 3f);
        }

        private void Spawn(EncounterZone z, SpawnPoint p, Vector3? at = null)
        {
            if (p.IsCave) { z.caves.Add(spawner.CreateCave(p)); return; }

            var e = spawner.Create(p.enemy, at ?? p.pos);
            if (e == null) return;
            // quái bay (Plague Eye): ghim tại chỗ sinh (lơ lửng trên hố), chỉ nhích nhẹ
            if (e.floating) e.bounds = new Rect(p.pos.x - 0.55f, p.pos.y - 0.45f, 1.1f, 0.9f);
            z.alive.Add(e);
            if (p.props.GetBool(MapSchema.Prop.Provoke, false)) e.Provoke();   // 06/11: phục kích — chạy tới Ren ngay
            if (p.props.GetBool(MapSchema.Prop.Boss, false))
            {
                // 19/11 (§344): song đấu — boss chính còn sống trong cùng khu -> con này là boss phụ
                if (Boss != null && Boss != e && Boss.Health != null && !Boss.Health.IsDead && z.alive.Contains(Boss)) BossPartner = e;
                else { Boss = e; BossPartner = null; }
                e.Health.Died += _ =>
                {
                    if (BossPartner != null && BossPartner.Health != null && !BossPartner.Health.IsDead && e == Boss) { Boss = BossPartner; BossPartner = null; }
                    else if (e == BossPartner) BossPartner = null;
                    // §343: khu boss chỉ "dọn xong" khi MỌI boss của khu đã gục (z.alive vẫn giữ con còn lại)
                    z.bossDown = true;
                };
                e.Phase2Reached += _ =>
                {
                    foreach (var q in z.points) if (q.eventWave) Spawn(z, q);
                    // 16/11 (§269): bỏ thông báo thừa — hình / hoạt ảnh / bảng tên boss đã thể hiện
                };
                BossSpawned?.Invoke(e, z.waveIndex <= 0 ? z.hint : "");
            }
        }

        /// <summary>Boss triệu hồi thêm quái vào khu của nó (tính vào khu: khu chỉ dọn xong khi hết quái).</summary>
        public EnemyAI SpawnSummon(string enemyName, Vector2 pos, EnemyAI owner)
        {
            var e = spawner.Create(enemyName, AtPlayerDepth(pos), "quái triệu hồi");
            if (e == null) return null;
            e.Provoke();                                   // 06/11: quái boss gọi ra lao vào Ren ngay, không đứng chờ tới tầm nhìn
            foreach (var z in layout.Zones.Values)
                if (z.alive.Contains(owner)) { z.alive.Add(e); break; }
            return e;
        }

        /// <summary>Cơ quan thả quái vào một khu (vd. mở nhầm buồng giam). Khu chỉ dọn xong khi quái này chết.</summary>
        /// <summary>07/11: prefab quái theo tên (cảnh diễn dựng "diễn viên" không có não, vd Isolde ném Ren vào Bower).</summary>
        public GameObject EnemyPrefab(string enemyName) => spawner != null ? spawner.Prefab(enemyName) : null;

        public EnemyAI SpawnInZone(string enemyName, Vector2 pos, string zoneId)
        {
            var e = spawner.Create(enemyName, AtPlayerDepth(pos));
            if (e != null && !string.IsNullOrEmpty(zoneId) && layout.Zones.TryGetValue(zoneId, out var z)) z.alive.Add(e);
            return e;
        }

        private Vector3 AtPlayerDepth(Vector2 pos) => new Vector3(pos.x, pos.y, player != null ? player.position.z : 0f);

        /// <summary>02/11: khu đang đánh: đã hạ / tổng số quái (gồm đợt chưa ra và quái hang). false = khu chưa bắt đầu / đã xong / không có.</summary>
        public bool ZoneTally(string zoneId, out int slain, out int total)
        {
            slain = total = 0;
            if (layout == null || string.IsNullOrEmpty(zoneId) || !layout.Zones.TryGetValue(zoneId, out var z) || !z.started || z.cleared) return false;
            int alive = 0;
            foreach (var e in z.alive) if (e != null && !e.Health.IsDead) alive++;
            int dead = z.alive.Count - alive;                           // chết nhưng chưa dọn khỏi danh sách (khung này)
            int pending = 0;
            for (int i = z.waveIndex + 1; i < z.waveOrder.Count; i++)
                foreach (var p in z.points) if (!p.eventWave && !p.IsCave && p.wave == z.waveOrder[i] && !SpawnSkipped(p)) pending++;
            int caveSlain = 0, caveTotal = 0;
            foreach (var c in z.caves) if (c != null) { caveTotal += c.Total; caveSlain += c.Spawned - c.AliveCount; }
            slain = z.killed + dead + caveSlain;
            total = z.killed + z.alive.Count + z.pending + pending + caveTotal;
            return total > 0;
        }

        /// <summary>Khu đã dọn xong — hoặc không có khu tên đó (lối ra không gắn khu).</summary>
        internal bool IsZoneClearedOrMissing(string zoneId) => !layout.Zones.TryGetValue(zoneId, out var z) || z.cleared;

        // ------------------------------------------------------------------ 04/11: cửa khoá bằng chìa + Ren tự nói khi cửa khoá
        private readonly Dictionary<MapGate, float> gateBarkAt = new Dictionary<MapGate, float>();

        /// <summary>Ren tới sát một cửa còn đóng: có chìa (requires_key) -> dùng chìa, cửa mở; không có -> nói `locked_say`, quay lưng
        /// lùi lại một bước (thay cho biển "Examine" đứng trơ trọi cạnh cửa).</summary>
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
                    Flags.Set(g.name + "_unlocked");                  // -> OnFlagSet mở cổng
                    SfxHooks.KeyUsed(g.bounds.center); // 19/11 (§335): âm
                    Hints.Show($"Used: {KeyRing.NameOf(g.requiresKey)}. The lock turns.", 2.5f);
                    CameraFollow.KickMain(Vector2.down, 0.02f, 0.12f);
                    continue;
                }
                if (g.lockedSay.Length == 0) continue;
                if (gateBarkAt.TryGetValue(g, out float t) && Time.time < t) continue;
                gateBarkAt[g] = Time.time + 4f;
                SfxHooks.DoorLocked(g.bounds.center); // 19/11 (§335): âm
                StartCoroutine(GateBark(g, pos));
            }
        }

        private IEnumerator GateBark(MapGate g, Vector2 pos)
        {
            // 04/11 (người dùng): chỉ nói — không còn tự quay lưng / lùi lại
            KeyRing.Say(player, g.lockedSay, 2.6f);
            yield break;
        }

        // ------------------------------------------------------------------ cờ -> cổng
        /// <summary>Có cờ mới: mở các cổng unlock_when đã đủ cờ.</summary>
        private void OnFlagSet(bool announce)
        {
            if (gateCtl == null) return;                 // map chưa đọc xong (Start sẽ gọi lại)
            if (gateCtl.OpenUnlocked(Flags) && announce) Hints.Show("Somewhere, a way opens.", 2.5f);
        }

        // ------------------------------------------------------------------ 03/11: đấu trường boss (ArenaSeal)
        /// <summary>Khu (id + khung) chứa điểm `p` — khu nhỏ nhất nếu chồng nhau.</summary>
        internal bool TryGetZoneAt(Vector2 p, out string id, out Rect rect)
        {
            id = null; rect = default;
            if (layout == null) return false;
            float best = float.MaxValue;
            foreach (var z in layout.Zones.Values)
                if (z.rect.Contains(p) && z.rect.width * z.rect.height < best) { best = z.rect.width * z.rect.height; id = z.id; rect = z.rect; }
            return id != null;
        }

        /// <summary>Khu có cổng tự đóng khi vào (close_on_enter) — đã là đấu trường kín.</summary>
        internal bool ZoneHasLockGate(string id)
        {
            if (gateCtl == null) return false;
            foreach (var g in gateCtl.All) if (g.zoneId == id && g.closeOnEnter) return true;
            return false;
        }

        internal bool ZoneCleared(string id) => layout != null && layout.Zones.TryGetValue(id, out var z) && z.cleared;

        // ------------------------------------------------------------------ toạ độ
        /// <summary>Đổi toạ độ ô Tiled (x phải, y xuống) sang toạ độ thế giới.</summary>
        /// <summary>16/11 (§237): cảnh cốt truyện rời map qua lối ra theo tên (lệnh `exit` của StoryScenes).</summary>
        public void ExitNow(string exitName) { if (triggerHandler != null) triggerHandler.ExitNow(exitName); }

        public Vector3 TileToWorld(float tx, float ty)
        {
            var p = mapRoot != null ? mapRoot.TransformPoint(new Vector3(tx * tileSize, -ty * tileSize, 0f))
                                    : new Vector3(tx * tileSize, -ty * tileSize, 0f);
            return new Vector3(p.x, p.y, player != null ? player.position.z : 0f);
        }

        // ------------------------------------------------------------------ gục & hồi sinh
        internal void SetRespawnPoint(Vector2 pos) => respawnPoint = new Vector3(pos.x, pos.y, player.position.z);
        /// <summary>02/11: lưu tiến trình (gọi khi thắp / nghỉ ở điểm nghỉ).</summary>
        public void SaveProgress() => SaveGame.Save(this, respawnPoint);

        private IEnumerator Respawn()
        {
            respawning = true;
            Vector2 diedAt = player.position;
            // màn chết (RequiemMenus) hiện chữ YOU HAVE FALLEN; không hiện gợi ý trùng
            yield return new WaitForSeconds(respawnDelay);
            if (!ChooseStageRespawn(diedAt))                             // 08/11: gục trong ải chưa xong -> tỉnh ở đầu ải
                ChooseNearestRespawn(diedAt);                            // 17/11 (§285): còn lại -> điểm đã qua gần chỗ gục nhất
            player.position = respawnPoint;
            playerHealth.Revive();
            var kit = player.GetComponent<PlayerCombat>();
            if (kit != null) kit.RefillPotions();                       // 02/11: hồi sinh = đầy máu + nạp lại bình
            UndoZoneFlagsOnDeath();                                      // 19/11 (§343): vd chuông Tây -> phải rung lại, Isolde kéo xuống đánh lại
            ResetLockedZones();
            ResetOpenStages();                                           // 08/11: mọi ải chưa xong hồi quái
            SaveProgress();                                              // 19/11 (§343): lưu ngay trạng thái đã làm lại (thoát game giữa chừng không kẹt)
            respawning = false;
            FirstTimeHints.Show("tut_death", "You wake close to where you fell. Foes you had not finished have risen again.", 6f);
        }

        /// <summary>Người chơi gục và hồi sinh: mọi khu đang đánh dở được làm lại từ đầu — xoá quái còn lại, mở khoá đấu trường.
        /// 03/11 (chạy thử Map 2): trước đây chỉ khu có cổng khoá mới làm lại -> quái khu thường (Gallery of Arches)
        /// đuổi theo tới tận điểm hồi sinh, vừa sống lại đã bị vây.
        /// 05/11: bỏ ngoại lệ "khu chứa điểm hồi sinh" (lỗi: Lantern Knight giữ máu dở cạnh lửa trại Broken Bridge, hồi sinh giữa quái ở
        /// Overseer's Office / Ruins Path / Ossuary Tower). Khu đó giờ cũng làm lại, nhưng chờ Ren rời lửa trại vài bước mới đánh lại.</summary>
        private const float RespawnHoldDistance = 2f;

        private void ResetLockedZones()
        {
            foreach (var z in layout.Zones.Values)
            {
                if (!z.started || z.cleared) continue;
                if (z.caves.Count > 0) continue;                       // khu có hang sinh quái: hang không làm lại được -> giữ nguyên
                bool arena = gateCtl.HasArenaLock(z.id);
                foreach (var e in z.alive)
                    if (e != null) { if (e == Boss) Boss = null; if (e == BossPartner) BossPartner = null; Destroy(e.gameObject); }
                z.alive.Clear();
                if (arena) gateCtl.SetArenaClosed(z.id, false);
                z.started = false;
                z.quietSince = -1f; z.stragglersCalled = false; z.fought = false;   // 19/11 (§340); 20/11
                z.bossDown = false;                               // 19/11 (§343)
                RaiseZoneReset(z.rect);                           // 19/11 (§343)
                z.waveIndex = -1;
                z.killed = 0;
                z.pending = 0; z.generation++;
                z.holdNearRespawn = z.rect.Contains((Vector2)respawnPoint);
            }
            RefreshStandIns();                                     // 16/11 (§271): khu làm lại -> diễn viên đứng sẵn hiện lại
        }

        // ------------------------------------------------------------------ 16/11 (§271): diễn viên đứng sẵn (stand_in)
        /// <summary>Điểm sinh đợt đầu có thuộc tính `stand_in` = true: khi khu CHƯA bắt đầu / chưa dọn, đặt sẵn một "diễn viên" (chỉ hình + hoạt ảnh Idle,
        /// không AI / máu / va chạm) đúng chỗ đó — người chơi thấy boss từ xa (vd cảnh động đất nhìn về Stone Ring). Trận bắt đầu -> xoá, quái thật sinh đúng chỗ.</summary>
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
