namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Tên layer / loại object / thuộc tính Tiled mà hệ thống trận đánh (MapEncounterDirector) đọc.
    /// Đây là "hợp đồng" giữa file .tmx và code: đổi tên ở Tiled thì phải đổi ở đây (và ngược lại).
    /// Thuộc tính của cơ quan / câu đố nằm ở MapMechanics.
    /// </summary>
    public static class MapSchema
    {
        /// <summary>Tên object layer trong Tiled.</summary>
        public static class Layer
        {
            public const string Zones = "zones";        // vùng từng khu
            public const string Spawns = "spawns";      // điểm sinh quái
            public const string Gates = "gates";        // cổng: collider + hình, cùng tên object
            public const string Triggers = "triggers";  // điểm kích hoạt khi bước vào
        }

        /// <summary>Loại (Type/Class) của object trong layer triggers — và loại "gate" trong layer gates.</summary>
        public static class ObjectType
        {
            public const string Gate = "gate";
            public const string PlayerStart = "player_start";
            public const string Checkpoint = "checkpoint";  // điểm nghỉ: hồi sinh ở đây, đổi chiêu
            public const string Sign = "sign";              // bia / biển / xác: nhấn T để đọc (hoặc tự hiện nếu auto)
            public const string Pickup = "pickup";          // vật nhặt: potion | flag | skill | skill_book
            public const string Portal = "portal";          // dịch chuyển trong cùng map
            public const string Exit = "exit";              // sang map khác
            public const string BossArena = "boss";         // 02/11: vùng trận boss (vd grok_fight) — BossIntro đọc lời thoại / toss_minions
            public const string TurnBack = "turn_back";     // 02/11: dải chặn mềm — chưa đủ cờ (requires) thì đẩy lùi + nhắc locked_text
        }

        /// <summary>Tên thuộc tính (Custom Properties).</summary>
        public static class Prop
        {
            // chung
            public const string ZoneId = "zone_id";
            public const string HintText = "hint_text";
            public const string Requires = "requires";        // danh sách cờ cần có
            public const string LockedText = "locked_text";   // chữ hiện khi chưa đủ cờ
            public const string Target = "target";

            // zones
            public const string Title = "title";
            public const string StartWhen = "start_when";     // khu chỉ bắt đầu khi đủ cờ
            public const string Fog = "fog";                  // 05/11: sương bản đồ — trống / secret / off (MapFog)
            // 07/11: đánh dấu cốt truyện (xem EncounterZone.story) — phím test F4 nhảy theo đây
            public const string Story = "story";              // số bước mạch chính, vd "2.2" hoặc nhiều bước "2.2;5" (khu dùng lại ở chương sau)
            public const string StoryDone = "story_done";     // cờ coi như xong khi qua bước đó, ngăn bằng ';' theo từng số của `story`
            public const string EntryDepth = "entry_depth";   // 08/11: số ô phải đi vào trong khu (tính từ mép) thì trận mới bắt đầu
            public const string Stage = "stage";              // 08/11: mã ải — trên khu (khu thuộc ải) và trên checkpoint (điểm hồi sinh của ải)
            public const string StageName = "stage_name";     // 08/11: tên ải (trên checkpoint của ải) — hiện khi qua ải
            public const string Optional = "optional";        // true = khu phụ / bí mật (không thuộc mạch chính)

            // spawns
            public const string Enemy = "enemy";
            public const string Wave = "wave";
            public const string SpawnWhen = "spawn_when";     // có giá trị = đợt sự kiện (boss gọi viện binh ở pha 2)
            public const string Provoke = "provoke";          // 06/11: true = quái sinh ra lao thẳng vào Ren (phục kích sinh ngoài tầm nhìn)
            public const string Boss = "boss";
            public const string PerWave = "per_wave";         // hang (Cave_Spawner)
            public const string Waves = "waves";
            public const string TriggerRadiusTiles = "trigger_radius_tiles";
            public const string Spawns = "spawns";            // "Spider|Snake"

            // gates
            public const string StartsClosed = "starts_closed";
            public const string CloseOnEnter = "close_on_enter";  // cổng khoá đấu trường
            public const string UnlockWhen = "unlock_when";       // mở khi đủ cờ

            // sign
            public const string Auto = "auto";
            public const string Verb = "verb";
            public const string Range = "range";
            public const string Interact = "interact";        // 08/11 (§190): exit — nhấn T mới đi (verb / range như biển)
            public const string HideWhen = "hide_when";

            // pickup
            public const string Item = "item";
            public const string Flag = "flag";
            public const string Hide = "hide";
            public const string SkillId = "skill_id";
            public const string Book = "book";

            // portal / exit
            public const string Shake = "shake";
            public const string DepartText = "depart_text";
            public const string OpenText = "open_text";
            // 07/11: lối ra sang map con (hầm, tầng dưới) và quay về
            public const string ArriveAt = "arrive_at";          // "x,y" (ô Tiled của map ĐÍCH): chỗ đứng khi tới (trống = player_start)
            public const string ArriveTitle = "arrive_title";    // tiêu đề hiện khi tới (trống = tiêu đề mặc định của map đích)
            public const string ArriveSubtitle = "arrive_subtitle";
            public const string Quake = "quake";                 // false = không rung đất lúc đi / lúc tới (leo dây, đi cầu thang)
            // 07/11: thuộc tính CỦA MAP (Map ▸ Map Properties)
            public const string FlagGroup = "flag_group";        // các map cùng nhóm dùng chung cờ khi đi qua lại (map chính + hầm phụ)
        }

        /// <summary>Tên quái đặc biệt: hang sinh quái (không phải EnemyAI).</summary>
        public const string CaveSpawner = "Cave_Spawner";
    }
}
