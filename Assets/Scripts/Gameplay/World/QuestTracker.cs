using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Theo dõi NHIỆM VỤ CHÍNH của map + dữ liệu minimap (RequiemHUD vẽ).
    ///
    /// Dữ liệu: Resources/Quests/&lt;tên scene&gt;.json (toạ độ theo Ô Tiled, x phải / y xuống), ảnh: Resources/Minimap/&lt;tên scene&gt;.png
    /// (bake bằng Catalog/v2/tools/minimap_bake.py, ppt px mỗi ô).
    ///   steps[]   : các bước theo thứ tự. Bước hiện tại = bước đầu tiên chưa xong (`done` = danh sách cờ "a,b", mỗi mục có thể "x|y").
    ///   targets[] : nơi cần tới của bước. Nhiều đích (vd 3 chuông) -> hiện tiến độ n/3 và chỉ tới đích CHƯA XONG gần nhất.
    ///               `pre` = cờ cần có trước (vd mở khoá hầm Vey trước khi tới Chuông Tây) -> khi chưa có, chỉ tới preX/preY.
    ///               05/11: `check` = dòng ngắn của đích trong DANH SÁCH CON (bảng nhiệm vụ hiện ô tích cho từng đích có `check`):
    ///               đã xong = ô đặc, đích mũi tên đang chỉ = sáng, đích còn thiếu điều kiện `pre` = "locked".
    ///   hidden[]  : vùng bí mật (vd hầm X2) — minimap để trống, đứng trong đó thì minimap tối và không chỉ đường.
    ///   view      : khung của bản đồ lớn (phím M), mặc định cả map.
    /// Cờ do MapEncounterDirector giữ (&lt;khu&gt;_started, &lt;khu&gt;_cleared, cờ cơ quan, nhặt đồ...).
    /// Tự tạo khi scene có MapEncounterDirector.
    /// </summary>
    public class QuestTracker : MonoBehaviour
    {
        [Serializable] public class Target
        {
            public string label = "";
            public float x, y;
            public string done = "";
            public string pre = "", preLabel = "";
            public float preX, preY;
            public string check = "";                 // 05/11: dòng trong danh sách con (để trống = không hiện)
        }

        /// <summary>05/11: một dòng của danh sách con (bảng nhiệm vụ).</summary>
        public struct Check { public string text; public bool done, active, locked; }
        [Serializable] public class Step
        {
            public string title = "";
            public string done = "";
            public Target[] targets = new Target[0];
        }
        [Serializable] public class Area { public float x, y, w, h; public string label = ""; }
        /// <summary>02/11: điểm ghé tạm (vd lửa trại đầu tiên): khi đủ cờ `when` mà chưa có cờ `done`, mũi tên / minimap / dòng phụ
        /// chỉ tới đây thay cho mục tiêu của bước hiện tại (tiêu đề nhiệm vụ giữ nguyên).</summary>
        [Serializable] public class Detour { public string label = "", when = "", done = ""; public float x, y; }
        [Serializable] public class Data
        {
            public float ppt = 4f;
            public Step[] steps = new Step[0];
            public Area[] hidden = new Area[0];
            public Area view;
            public Detour[] detours = new Detour[0];
            /// <summary>16/11 (§267): "side" = nhiệm vụ phụ (map bí mật: khám phá theo manh mối dân làng / ghi chú) -> bảng ghi SIDE QUEST.</summary>
            public string kind = "main";
        }

        public static QuestTracker Instance { get; private set; }

        public Data data;
        public Texture2D Minimap { get; private set; }
        public float Ppt => data != null && data.ppt > 0f ? data.ppt : 4f;
        public Vector2 MapTiles => Minimap != null ? new Vector2(Minimap.width / Ppt, Minimap.height / Ppt) : Vector2.zero;

        // ---- trạng thái cho HUD ----
        public bool HasQuest => data != null && data.steps != null && data.steps.Length > 0;
        /// <summary>16/11 (§267): nhiệm vụ phụ (kind = side).</summary>
        public bool IsSide => data != null && string.Equals(data.kind, "side", StringComparison.OrdinalIgnoreCase);
        public int StepIndex { get; private set; } = -1;
        /// <summary>02/11: cờ hoàn thành của bước hiện tại (vd "z3_cleared") — HUD đếm quái của khu.</summary>
        public string StepDone => data != null && data.steps != null && StepIndex >= 0 && StepIndex < data.steps.Length ? data.steps[StepIndex].done : "";
        public string Title { get; private set; } = "";
        public string Label { get; private set; } = "";
        public int DoneCount { get; private set; }
        public int TotalCount { get; private set; }
        public bool HasTarget { get; private set; }
        public Vector2 TargetTile { get; private set; }
        public Vector3 TargetWorld { get; private set; }
        public float StepChangedAt { get; private set; } = -99f;
        public bool AllDone { get; private set; }
        /// <summary>05/11: danh sách con của bước hiện tại (rỗng nếu không đích nào có `check`).</summary>
        public readonly List<Check> Checks = new List<Check>();
        public bool BigMap { get; set; }

        private MapEncounterDirector dir;
        private Transform player;
        private float nextEval;

        private static bool hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { if (hooked) SceneManager.sceneLoaded -= OnSceneLoaded; hooked = false; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            // 02/11 (chạy thử): trước đây chỉ tạo ở scene ĐẦU TIÊN của phiên chơi -> tải lại map / sang map khác / Continue thì
            // mất minimap + bảng nhiệm vụ. Giờ tạo lại mỗi khi một scene có MapEncounterDirector được nạp.
            if (!hooked) { SceneManager.sceneLoaded += OnSceneLoaded; hooked = true; }
            Ensure();
        }

        private static void OnSceneLoaded(Scene s, LoadSceneMode m) => Ensure();

        private static void Ensure()
        {
            if (FindFirstObjectByType<MapEncounterDirector>() != null && FindFirstObjectByType<QuestTracker>() == null)
                new GameObject("QuestTracker").AddComponent<QuestTracker>();
        }

        private void Awake()
        {
            Instance = this;
            string scene = SceneManager.GetActiveScene().name;
            var ta = Resources.Load<TextAsset>("Quests/" + scene);
            if (ta != null)
            {
                try { data = JsonUtility.FromJson<Data>(ta.text); }
                catch (Exception e) { Debug.LogWarning($"[Quest] Lỗi đọc Quests/{scene}.json: {e.Message}"); }
            }
            else Debug.LogWarning($"[Quest] Không có Resources/Quests/{scene}.json — không theo dõi nhiệm vụ.");
            if (data == null) data = new Data();
            Minimap = Resources.Load<Texture2D>("Minimap/" + scene);
            if (Minimap != null) { Minimap.filterMode = FilterMode.Point; Minimap.wrapMode = TextureWrapMode.Clamp; }
            else Debug.LogWarning($"[Quest] Không có Resources/Minimap/{scene}.png — ẩn minimap.");
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Update()
        {
            if (dir == null) dir = MapEncounterDirector.Instance;
            if (player == null)
            {
                var pc = FindFirstObjectByType<PlayerController>();
                if (pc != null) player = pc.transform;
            }
            var kb = Keyboard.current;
            if (GameInput.Pressed(GameAction.Map) && Minimap != null) BigMap = !BigMap;
            if (Time.time >= nextEval) { nextEval = Time.time + 0.25f; Evaluate(); }
        }

        // ------------------------------------------------------------------ toạ độ
        public Vector2 WorldToTile(Vector3 w)
        {
            if (dir == null || dir.mapRoot == null) return new Vector2(w.x / 0.32f, -w.y / 0.32f);
            var l = dir.mapRoot.InverseTransformPoint(w);
            return new Vector2(l.x / dir.tileSize, -l.y / dir.tileSize);
        }

        public Vector3 TileToWorld(Vector2 t) => dir != null ? dir.TileToWorld(t.x, t.y) : new Vector3(t.x * 0.32f, -t.y * 0.32f, 0f);

        public Vector2 PlayerTile => player != null ? WorldToTile(player.position) : Vector2.zero;
        public bool HasPlayer => player != null;

        /// Vùng bí mật chứa ô này (null nếu không).
        public Area HiddenAt(Vector2 tile)
        {
            if (data.hidden == null) return null;
            foreach (var a in data.hidden)
                if (tile.x >= a.x && tile.x < a.x + a.w && tile.y >= a.y && tile.y < a.y + a.h) return a;
            return null;
        }

        public Rect ViewTiles
        {
            get
            {
                var v = data.view;
                if (v != null && v.w > 0f && v.h > 0f) return new Rect(v.x, v.y, v.w, v.h);
                var m = MapTiles; return new Rect(0, 0, m.x, m.y);
            }
        }

        // ------------------------------------------------------------------ cờ
        /// "a,b" = cần đủ; mỗi mục "x|y" = một trong các cờ. Rỗng / "never" = chưa bao giờ xong.
        private bool Flags(string list)
        {
            if (dir == null || string.IsNullOrEmpty(list)) return false;
            foreach (var part in list.Split(','))
            {
                bool any = false;
                foreach (var alt in part.Split('|'))
                    if (dir.Flags.Has(alt.Trim())) { any = true; break; }
                if (!any) return false;
            }
            return true;
        }

        private void Evaluate()
        {
            if (!HasQuest || dir == null) { HasTarget = false; return; }
            int idx = data.steps.Length;
            for (int i = 0; i < data.steps.Length; i++)
                if (!Flags(data.steps[i].done)) { idx = i; break; }

            if (idx != StepIndex)
            {
                if (StepIndex >= 0 && idx > StepIndex) { StepChangedAt = Time.time; SoulRequiem.Core.Sfx.Play2D("quest_update"); }   // 17/11 (§294): âm
                StepIndex = idx;
            }
            AllDone = idx >= data.steps.Length;
            Checks.Clear();
            if (AllDone) { Title = ""; Label = ""; HasTarget = false; DoneCount = TotalCount = 0; return; }

            var st = data.steps[idx];
            Title = st.title;
            int done = 0, total = st.targets != null ? st.targets.Length : 0;
            float best = float.MaxValue;
            HasTarget = false;
            Vector2 me = PlayerTile;
            int activeCheck = -1;
            if (st.targets != null)
                foreach (var t in st.targets)
                {
                    string tdone = string.IsNullOrEmpty(t.done) ? st.done : t.done;
                    bool tDone = Flags(tdone);
                    bool usePre = !tDone && !string.IsNullOrEmpty(t.pre) && !Flags(t.pre);
                    if (!string.IsNullOrEmpty(t.check)) Checks.Add(new Check { text = t.check, done = tDone, locked = usePre });
                    if (tDone) { done++; continue; }
                    var p = usePre ? new Vector2(t.preX, t.preY) : new Vector2(t.x, t.y);
                    float d = (p - me).sqrMagnitude;
                    if (d < best)
                    {
                        best = d;
                        HasTarget = true;
                        TargetTile = p;
                        Label = usePre && !string.IsNullOrEmpty(t.preLabel) ? t.preLabel : t.label;
                        // đích có pre chưa đủ thì mũi tên thực ra chỉ tới món điều kiện -> không tô sáng dòng "locked"
                        activeCheck = !string.IsNullOrEmpty(t.check) && !usePre ? Checks.Count - 1 : -1;
                    }
                }
            if (activeCheck >= 0) { var c = Checks[activeCheck]; c.active = true; Checks[activeCheck] = c; }
            DoneCount = done;
            TotalCount = total > 1 ? total : 0;
            if (data.detours != null)
                foreach (var dt in data.detours)
                    if (Flags(dt.when) && !Flags(dt.done))
                    {
                        HasTarget = true; TargetTile = new Vector2(dt.x, dt.y); Label = dt.label;
                        break;
                    }
            if (HasTarget) TargetWorld = TileToWorld(TargetTile);
            // 20/11 (§362): mục tiêu tạm của một cơ quan đang diễn ra (minigame) thay bước hiện tại trên bảng + mũi tên đích
            if (QuestGuide.Active)
            {
                Title = QuestGuide.Title; Label = QuestGuide.Label; Checks.Clear();
                DoneCount = QuestGuide.Done; TotalCount = QuestGuide.Total > 1 ? QuestGuide.Total : 0;
                HasTarget = QuestGuide.HasTarget;
                if (HasTarget) { TargetWorld = QuestGuide.World; TargetTile = WorldToTile(QuestGuide.World); }
            }
        }
    }
}
