using System.Collections.Generic;
using SuperTiled2Unity;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>Enemy spawn point (spawns layer).</summary>
    internal sealed class SpawnPoint
    {
        public string enemy;
        public int wave;
        public bool eventWave;            // only spawns when the boss enters phase 2
        public Vector3 pos;
        public SuperCustomProperties props;
        public bool IsCave => enemy == MapSchema.CaveSpawner;
    }

    /// <summary>One area (zones layer) + the state of the fight inside it.</summary>
    internal sealed class EncounterZone
    {
        public string id, title, hint, startWhen = "";
        public string fog = "";           // map fog (MapFog): "" / "secret" / "off"
        /// <summary>Main story steps this area covers (Tiled property `story`, e.g. "2.2;5") - empty = not on the main story.</summary>
        public float[] story = new float[0];
        /// <summary>Flags treated as done for each step in `story` (property `story_done`, separated by ';').</summary>
        public string[] storyDone = new string[0];
        public bool optional;             // side / secret area (property `optional`)
        /// <summary>Trial code (property `stage`) - areas of an unfinished trial respawn their enemies on death (MapEncounterDirector.Stages).</summary>
        public string stage = "";
        /// <summary>Property `entry_depth` (tiles) - how many tiles into the area before the fight starts; &lt; 0 = the director default.</summary>
        public float entryDepth = -1f;
        public Rect rect;
        public int order = int.MaxValue;  // Tiled object id = area creation order (used by F3)
        public readonly List<SpawnPoint> points = new List<SpawnPoint>();
        public readonly List<int> waveOrder = new List<int>();
        public int waveIndex = -1;
        public bool started, cleared;
        public bool holdNearRespawn;      // a just-restarted area containing the respawn point -> only restarts once Ren has walked a few steps from the campfire
        public int killed;                // area enemies defeated (quest panel)
        public int pending;               // enemies scheduled to spawn (telegraphing / waiting) - the area is not cleared yet
        public int generation;            // incremented when the area restarts -> cancels pending spawns
        public readonly List<EnemyAI> alive = new List<EnemyAI>();
        public float quietSince = -1f;    // start time of "≤ 2 enemies left and none in combat"
        public bool stragglersCalled;     // this wave's stragglers have been called toward Ren + shown on the minimap
        public bool fought;               // an enemy of this wave has fought -> only then may stragglers be called (enemies "acting" like the house-burning goblins are not)
        public bool bossDown;             // the area's boss has really fallen (Health.Died)
        public string resetOnDeath = "";  // property `reset_on_death` - flags cleared when the player falls mid-fight
        public readonly List<CaveSpawner> caves = new List<CaveSpawner>();
        /// <summary>Pre-placed "actor" of a spawn point with `stand_in` (e.g. Grok at the Stone Ring) - shown while the area has not been fought, removed when the fight starts.</summary>
        public readonly List<GameObject> standIns = new List<GameObject>();
    }

    /// <summary>One gate (objects with the same name in the gates layer).</summary>
    internal sealed class MapGate
    {
        public string name, zoneId, unlockWhen = "";
        /// <summary>Inventory key needed to open it (KeyRing) - opens when Ren is right at the door with the key; line Ren says while the door is still locked.</summary>
        public string requiresKey = "", lockedSay = "";
        public bool startsClosed = true, closeOnEnter, opened;
        public Rect bounds;               // collider area (taken while reading the map, before the gate gets disabled)
        public readonly List<GameObject> parts = new List<GameObject>();
    }

    /// <summary>One trigger point (triggers layer).</summary>
    internal sealed class MapTrigger
    {
        public string type, name, zoneId;
        /// <summary>turn_back strip - side (sign along the thin axis: -1 / +1) the player was last on while fully outside the strip. 0 = unknown.</summary>
        public float safeSide;
        public Rect rect;
        public SuperCustomProperties props;
        public bool fired, inside;
    }

    /// <summary>
    /// All fight data read from the Tiled map (SuperTiled2Unity). Created with <see cref="Read"/>.
    /// </summary>
    internal sealed class MapLayout
    {
        public readonly Dictionary<string, EncounterZone> Zones = new Dictionary<string, EncounterZone>();
        public readonly List<string> ZoneOrder = new List<string>();   // order as found in the file
        public readonly List<MapGate> Gates = new List<MapGate>();
        public readonly List<MapTrigger> Triggers = new List<MapTrigger>();

        public static MapLayout Read(Transform mapRoot)
        {
            var map = new MapLayout();
            foreach (var so in mapRoot.GetComponentsInChildren<SuperObject>(true))
            {
                string layer = so.transform.parent != null ? so.transform.parent.name : "";
                // a map with 2 Tiled layers of the same name (Map 4: "gates" x2 - one for the bridge lying on the ground, different sorting) -> SuperTiled2Unity
                // names the second layer "gates (1)", which used to make MapLayout skip the whole layer (11 Map 4 gates never opened). Strip the " (n)" suffix.
                int sfx = layer.LastIndexOf(" (");
                if (sfx > 0 && layer.EndsWith(")") && int.TryParse(layer.Substring(sfx + 2, layer.Length - sfx - 3), out _)) layer = layer.Substring(0, sfx);
                var props = so.GetComponent<SuperCustomProperties>();
                switch (layer)
                {
                    case MapSchema.Layer.Zones:
                    {
                        var z = map.GetZone(props.GetString(MapSchema.Prop.ZoneId, so.m_TiledName));
                        z.title = props.GetString(MapSchema.Prop.Title, z.id);
                        z.hint = props.GetString(MapSchema.Prop.HintText, "");
                        z.startWhen = props.GetString(MapSchema.Prop.StartWhen, "");
                        z.fog = props.GetString(MapSchema.Prop.Fog, "");
                        z.optional = props.GetBool(MapSchema.Prop.Optional, false);
                        z.stage = props.GetString(MapSchema.Prop.Stage, "").Trim();
                        z.entryDepth = props.GetFloat(MapSchema.Prop.EntryDepth, -1f);
                        z.resetOnDeath = props.GetString("reset_on_death", "").Trim();
                        ReadStory(z, props.GetString(MapSchema.Prop.Story, ""), props.GetString(MapSchema.Prop.StoryDone, ""));
                        z.rect = RectOf(so);
                        z.order = so.m_Id;
                        break;
                    }
                    case MapSchema.Layer.Spawns:
                    {
                        var z = map.GetZone(props.GetString(MapSchema.Prop.ZoneId, ""));
                        z.points.Add(new SpawnPoint
                        {
                            enemy = props.GetString(MapSchema.Prop.Enemy, so.m_TiledName),
                            wave = props.GetInt(MapSchema.Prop.Wave, 1),
                            eventWave = !string.IsNullOrEmpty(props.GetString(MapSchema.Prop.SpawnWhen, "")),
                            pos = so.transform.position,
                            props = props
                        });
                        break;
                    }
                    case MapSchema.Layer.Gates:
                    {
                        var g = map.Gates.Find(x => x.name == so.m_TiledName);
                        if (g == null) { g = new MapGate { name = so.m_TiledName }; map.Gates.Add(g); }
                        g.parts.Add(so.gameObject);
                        // animated gate image (property `anim` = Resources/VFX frame strip)
                        if (so.m_Type == "gate_visual" && props.GetString("anim", "").Length > 0)
                            so.gameObject.AddComponent<GateAnimator>().Setup(props.GetString("anim", ""), props.GetFloat("anim_fps", 14f));
                        if (so.m_Type == MapSchema.ObjectType.Gate)
                        {
                            g.bounds = RectOf(so);
                            g.zoneId = props.GetString(MapSchema.Prop.ZoneId, "");
                            g.startsClosed = props.GetBool(MapSchema.Prop.StartsClosed, true);
                            g.closeOnEnter = props.GetBool(MapSchema.Prop.CloseOnEnter, false);
                            g.unlockWhen = props.GetString(MapSchema.Prop.UnlockWhen, "");
                            g.requiresKey = props.GetString("requires_key", "");
                            g.lockedSay = props.GetString("locked_say", "");
                            if (g.requiresKey.Length > 0 && g.unlockWhen.Length == 0) g.unlockWhen = g.name + "_unlocked";
                        }
                        break;
                    }
                    case MapSchema.Layer.Triggers:
                        // the triggers layer is only trigger areas - objects without unity:IsTrigger get SOLID colliders from SuperTiled2Unity
                        // (the village_north_lock strip became an invisible wall). Force every collider of this layer to be a trigger.
                        foreach (var c in so.GetComponentsInChildren<Collider2D>(true)) c.isTrigger = true;
                        map.Triggers.Add(new MapTrigger
                        {
                            type = so.m_Type, name = so.m_TiledName, zoneId = props.GetString(MapSchema.Prop.ZoneId, ""),
                            rect = RectOf(so), props = props
                        });
                        break;
                }
            }
            return map;
        }

        private EncounterZone GetZone(string id)
        {
            if (!Zones.TryGetValue(id, out var z)) { z = new EncounterZone { id = id }; Zones[id] = z; ZoneOrder.Add(id); }
            return z;
        }

        private static void ReadStory(EncounterZone z, string story, string done)
        {
            if (string.IsNullOrWhiteSpace(story)) return;
            var parts = story.Split(';');
            var doneParts = done.Split(';');
            var steps = new List<float>();
            var flags = new List<string>();
            for (int i = 0; i < parts.Length; i++)
            {
                if (!float.TryParse(parts[i].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v))
                {
                    Debug.LogWarning("[MapLayout] area " + z.id + ": story '" + parts[i] + "' is not a number");
                    continue;
                }
                steps.Add(v);
                flags.Add(i < doneParts.Length ? doneParts[i].Trim() : "");
            }
            z.story = steps.ToArray();
            z.storyDone = flags.ToArray();
        }

        /// <summary>World rectangle of the object: from its collider, otherwise a point.</summary>
        public static Rect RectOf(SuperObject so)
        {
            var col = so.GetComponent<Collider2D>();
            if (col != null) { var b = col.bounds; return new Rect(b.min.x, b.min.y, b.size.x, b.size.y); }
            return new Rect(so.transform.position.x, so.transform.position.y, 0.01f, 0.01f);
        }
    }
}
