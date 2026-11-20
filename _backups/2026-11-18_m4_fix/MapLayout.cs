using System.Collections.Generic;
using SuperTiled2Unity;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>Điểm sinh quái (layer spawns).</summary>
    internal sealed class SpawnPoint
    {
        public string enemy;
        public int wave;
        public bool eventWave;            // chỉ sinh khi boss vào pha 2
        public Vector3 pos;
        public SuperCustomProperties props;
        public bool IsCave => enemy == MapSchema.CaveSpawner;
    }

    /// <summary>Một khu (layer zones) + trạng thái trận đánh trong khu.</summary>
    internal sealed class EncounterZone
    {
        public string id, title, hint, startWhen = "";
        public string fog = "";           // 05/11: sương bản đồ (MapFog): "" / "secret" / "off"
        /// <summary>07/11: các bước mạch chính khu này đảm nhận (thuộc tính Tiled `story`, vd "2.2;5") — rỗng = không thuộc mạch chính.</summary>
        public float[] story = new float[0];
        /// <summary>Cờ coi như xong của từng bước trong `story` (thuộc tính `story_done`, ngăn ';').</summary>
        public string[] storyDone = new string[0];
        public bool optional;             // 07/11: khu phụ / bí mật (thuộc tính `optional`)
        /// <summary>08/11: mã ải (thuộc tính `stage`) — khu của ải chưa qua thì hồi quái khi gục (MapEncounterDirector.Stages).</summary>
        public string stage = "";
        /// <summary>08/11: thuộc tính `entry_depth` (ô) — đi sâu vào khu bao nhiêu ô thì trận mới bắt đầu; &lt; 0 = mặc định của director.</summary>
        public float entryDepth = -1f;
        public Rect rect;
        public int order = int.MaxValue;  // id object trong Tiled = thứ tự tạo khu (dùng cho F3)
        public readonly List<SpawnPoint> points = new List<SpawnPoint>();
        public readonly List<int> waveOrder = new List<int>();
        public int waveIndex = -1;
        public bool started, cleared;
        public bool holdNearRespawn;      // 05/11: khu vừa làm lại có chứa điểm hồi sinh -> chỉ bắt đầu lại khi Ren rời lửa trại vài bước
        public int killed;                // 02/11: quái của khu đã hạ (bảng nhiệm vụ)
        public int pending;               // 02/11: quái đã hẹn sinh (đang báo hiệu / chờ) — chưa tính là dọn xong
        public int generation;            // 02/11: tăng khi khu làm lại -> huỷ các lượt sinh đang chờ
        public readonly List<EnemyAI> alive = new List<EnemyAI>();
        public readonly List<CaveSpawner> caves = new List<CaveSpawner>();
        /// <summary>16/11 (§271): "diễn viên" đứng sẵn của điểm sinh có `stand_in` (vd Grok ở Stone Ring) — hiện khi khu chưa đánh, xoá lúc trận bắt đầu.</summary>
        public readonly List<GameObject> standIns = new List<GameObject>();
    }

    /// <summary>Một cổng (các object cùng tên trong layer gates).</summary>
    internal sealed class MapGate
    {
        public string name, zoneId, unlockWhen = "";
        /// <summary>04/11: chìa trong túi đồ để mở (KeyRing) — mở khi Ren tới sát cửa và có chìa; câu Ren nói khi cửa còn khoá.</summary>
        public string requiresKey = "", lockedSay = "";
        public bool startsClosed = true, closeOnEnter, opened;
        public Rect bounds;               // vùng collider (lấy lúc đọc map, trước khi cổng bị tắt)
        public readonly List<GameObject> parts = new List<GameObject>();
    }

    /// <summary>Một điểm kích hoạt (layer triggers).</summary>
    internal sealed class MapTrigger
    {
        public string type, name, zoneId;
        /// <summary>07/11: dải turn_back — phía (dấu theo trục mỏng: -1 / +1) người chơi đứng lần cuối khi còn ở hẳn ngoài dải. 0 = chưa biết.</summary>
        public float safeSide;
        public Rect rect;
        public SuperCustomProperties props;
        public bool fired, inside;
    }

    /// <summary>
    /// Toàn bộ dữ liệu trận đánh đọc từ map Tiled (SuperTiled2Unity). Tạo bằng <see cref="Read"/>.
    /// </summary>
    internal sealed class MapLayout
    {
        public readonly Dictionary<string, EncounterZone> Zones = new Dictionary<string, EncounterZone>();
        public readonly List<string> ZoneOrder = new List<string>();   // thứ tự gặp trong file
        public readonly List<MapGate> Gates = new List<MapGate>();
        public readonly List<MapTrigger> Triggers = new List<MapTrigger>();

        public static MapLayout Read(Transform mapRoot)
        {
            var map = new MapLayout();
            foreach (var so in mapRoot.GetComponentsInChildren<SuperObject>(true))
            {
                string layer = so.transform.parent != null ? so.transform.parent.name : "";
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
                        // 08/11 (§196): hình cổng có hoạt ảnh (thuộc tính `anim` = dải khung Resources/VFX)
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
                        // 02/11: lớp triggers chỉ là vùng kích hoạt — object thiếu unity:IsTrigger thì SuperTiled2Unity tạo va chạm ĐẶC
                        // (lỗi dải village_north_lock thành tường vô hình). Ép mọi va chạm của lớp này thành trigger.
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
                    Debug.LogWarning("[MapLayout] khu " + z.id + ": story '" + parts[i] + "' không phải số");
                    continue;
                }
                steps.Add(v);
                flags.Add(i < doneParts.Length ? doneParts[i].Trim() : "");
            }
            z.story = steps.ToArray();
            z.storyDone = flags.ToArray();
        }

        /// <summary>Hình chữ nhật thế giới của object: theo collider, không có thì là một điểm.</summary>
        public static Rect RectOf(SuperObject so)
        {
            var col = so.GetComponent<Collider2D>();
            if (col != null) { var b = col.bounds; return new Rect(b.min.x, b.min.y, b.size.x, b.size.y); }
            return new Rect(so.transform.position.x, so.transform.position.y, 0.01f, 0.01f);
        }
    }
}
