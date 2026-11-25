namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Tiled layer / object type / property names read by the fight system (MapEncounterDirector).
    /// This is the "contract" between the .tmx files and the code: renaming something in Tiled means renaming it here (and vice versa).
    /// Mechanism / puzzle properties live in MapMechanics.
    /// </summary>
    public static class MapSchema
    {
        /// <summary>Object layer names in Tiled.</summary>
        public static class Layer
        {
            public const string Zones = "zones";        // area boxes
            public const string Spawns = "spawns";      // enemy spawn points
            public const string Gates = "gates";        // gates: collider + image, same object name
            public const string Triggers = "triggers";  // trigger points fired on entering
        }

        /// <summary>Type (Type/Class) of objects in the triggers layer - and type "gate" in the gates layer.</summary>
        public static class ObjectType
        {
            public const string Gate = "gate";
            public const string PlayerStart = "player_start";
            public const string Checkpoint = "checkpoint";  // rest point: respawn here, swap skills
            public const string Sign = "sign";              // gravestone / sign / corpse: press T to read (or shown automatically if auto)
            public const string Pickup = "pickup";          // pickup: potion | flag | skill | skill_book
            public const string Portal = "portal";          // teleport within the same map
            public const string Exit = "exit";              // to another map
            public const string BossArena = "boss";         // boss fight area (e.g. grok_fight) - BossIntro reads the dialogue / toss_minions
            public const string TurnBack = "turn_back";     // soft blocking strip - flags (requires) missing -> pushes back + shows locked_text
        }

        /// <summary>Property names (Custom Properties).</summary>
        public static class Prop
        {
            // chung
            public const string ZoneId = "zone_id";
            public const string HintText = "hint_text";
            public const string Requires = "requires";        // list of required flags
            public const string LockedText = "locked_text";   // text shown while flags are missing
            public const string Target = "target";

            // zones
            public const string Title = "title";
            public const string StartWhen = "start_when";     // the area only starts once the flags are set
            public const string Fog = "fog";                  // map fog - empty / secret / off (MapFog)
            // story marks (see EncounterZone.story) - the F4 test key jumps by these
            public const string Story = "story";              // main story step number, e.g. "2.2" or several steps "2.2;5" (area reused in a later chapter)
            public const string StoryDone = "story_done";     // flags treated as done once that step is passed, separated by ';' per `story` number
            public const string EntryDepth = "entry_depth";   // tiles to walk into the area (from the edge) before the fight starts
            public const string Stage = "stage";              // trial code - on areas (area belongs to the trial) and on checkpoints (the trial's respawn point)
            public const string StageName = "stage_name";     // trial name (on the trial's checkpoint) - shown when the trial is passed
            public const string Optional = "optional";        // true = side / secret area (not on the main story)

            // spawns
            public const string Enemy = "enemy";
            public const string Wave = "wave";
            public const string SpawnWhen = "spawn_when";     // any value = event wave (boss calls reinforcements in phase 2)
            public const string Provoke = "provoke";          // true = spawned enemies rush straight at Ren (ambush spawned out of sight)
            public const string Boss = "boss";
            public const string PerWave = "per_wave";         // hang (Cave_Spawner)
            public const string Waves = "waves";
            public const string TriggerRadiusTiles = "trigger_radius_tiles";
            public const string Spawns = "spawns";            // "Spider|Snake"

            // gates
            public const string StartsClosed = "starts_closed";
            public const string CloseOnEnter = "close_on_enter";  // arena lock gate
            public const string UnlockWhen = "unlock_when";       // opens once the flags are set

            // sign
            public const string Auto = "auto";
            public const string Verb = "verb";
            public const string Range = "range";
            public const string Interact = "interact";        // exit - only leaves on pressing T (verb / range like signs)
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
            // exit to a sub-map (cellar, lower floor) and back
            public const string ArriveAt = "arrive_at";          // "x,y" (Tiled tile of the DESTINATION map): where to stand on arrival (empty = player_start)
            public const string ArriveTitle = "arrive_title";    // title shown on arrival (empty = the destination map's default title)
            public const string ArriveSubtitle = "arrive_subtitle";
            public const string Quake = "quake";                 // false = no ground shake on leaving / arriving (climbing a rope, taking stairs)
            // properties OF THE MAP (Map ▸ Map Properties)
            public const string FlagGroup = "flag_group";        // maps in the same group share flags when moving back and forth (main map + side cellar)
        }

        /// <summary>Special enemy name: enemy spawning cave (not an EnemyAI).</summary>
        public const string CaveSpawner = "Cave_Spawner";
    }
}
