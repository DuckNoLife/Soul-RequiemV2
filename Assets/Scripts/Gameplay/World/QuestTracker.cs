using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Tracks the map's MAIN QUEST + minimap data (drawn by RequiemHUD).
    ///
    /// Data: Resources/Quests/&lt;scene name&gt;.json (coordinates in Tiled TILES, x right / y down), image: Resources/Minimap/&lt;scene name&gt;.png
    /// (baked with Catalog/v2/tools/minimap_bake.py, ppt px per tile).
    ///   steps[]   : steps in order. Current step = the first unfinished one (`done` = flag list "a,b", each item may be "x|y").
    ///   targets[] : places to reach for the step. Several targets (e.g. 3 bells) -> shows progress n/3 and points to the nearest UNFINISHED target.
    ///               `pre` = flag needed first (e.g. unlock the Vey crypt before the West Bell) -> until set, points to preX/preY.
    ///               `check` = short line for the target in the SUB-LIST (the quest panel shows a checkbox for every target with `check`):
    ///               done = filled box, the target the arrow points to = bright, a target missing its `pre` condition = "locked".
    ///   hidden[]  : secret areas (e.g. crypt X2) - left blank on the minimap; standing inside darkens the minimap and gives no directions.
    ///   view      : frame of the large map (M key), default the whole map.
    /// Flags are held by MapEncounterDirector (&lt;zone&gt;_started, &lt;zone&gt;_cleared, mechanism flags, pickups...).
    /// Created automatically when the scene has a MapEncounterDirector.
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
            public string check = "";                 // line in the sub-list (empty = not shown)
        }

        /// <summary>One line of the sub-list (quest panel).</summary>
        public struct Check { public string text; public bool done, active, locked; }
        [Serializable] public class Step
        {
            public string title = "";
            public string done = "";
            public Target[] targets = new Target[0];
        }
        [Serializable] public class Area { public float x, y, w, h; public string label = ""; }
        /// <summary>Temporary waypoint (e.g. the first campfire): once flags `when` are set and flag `done` is not, the arrow / minimap / sub-line
        /// point here instead of the current step's target (the quest title stays the same).</summary>
        [Serializable] public class Detour { public string label = "", when = "", done = ""; public float x, y; }
        [Serializable] public class Data
        {
            public float ppt = 4f;
            public Step[] steps = new Step[0];
            public Area[] hidden = new Area[0];
            public Area view;
            public Detour[] detours = new Detour[0];
            /// <summary>"side" = side quest (secret map: explored through villager / note clues) -> the panel says SIDE QUEST.</summary>
            public string kind = "main";
        }

        public static QuestTracker Instance { get; private set; }

        public Data data;
        public Texture2D Minimap { get; private set; }
        public float Ppt => data != null && data.ppt > 0f ? data.ppt : 4f;
        public Vector2 MapTiles => Minimap != null ? new Vector2(Minimap.width / Ppt, Minimap.height / Ppt) : Vector2.zero;

        // ---- state for the HUD ----
        public bool HasQuest => data != null && data.steps != null && data.steps.Length > 0;
        /// <summary>Side quest (kind = side).</summary>
        public bool IsSide => data != null && string.Equals(data.kind, "side", StringComparison.OrdinalIgnoreCase);
        public int StepIndex { get; private set; } = -1;
        /// <summary>Completion flag of the current step (e.g. "z3_cleared") - the HUD counts the area's enemies.</summary>
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
        /// <summary>Sub-list of the current step (empty if no target has `check`).</summary>
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
            // recreated every time a scene with a MapEncounterDirector loads (creating it only in the session's FIRST scene lost the
            // minimap + quest panel after reloading a map / moving to another map / Continue).
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
                catch (Exception e) { Debug.LogWarning($"[Quest] Failed to read Quests/{scene}.json: {e.Message}"); }
            }
            else Debug.LogWarning($"[Quest] No Resources/Quests/{scene}.json - quest tracking off.");
            if (data == null) data = new Data();
            Minimap = Resources.Load<Texture2D>("Minimap/" + scene);
            if (Minimap != null) { Minimap.filterMode = FilterMode.Point; Minimap.wrapMode = TextureWrapMode.Clamp; }
            else Debug.LogWarning($"[Quest] No Resources/Minimap/{scene}.png - minimap hidden.");
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

        // ------------------------------------------------------------------ coordinates
        public Vector2 WorldToTile(Vector3 w)
        {
            if (dir == null || dir.mapRoot == null) return new Vector2(w.x / 0.32f, -w.y / 0.32f);
            var l = dir.mapRoot.InverseTransformPoint(w);
            return new Vector2(l.x / dir.tileSize, -l.y / dir.tileSize);
        }

        public Vector3 TileToWorld(Vector2 t) => dir != null ? dir.TileToWorld(t.x, t.y) : new Vector3(t.x * 0.32f, -t.y * 0.32f, 0f);

        public Vector2 PlayerTile => player != null ? WorldToTile(player.position) : Vector2.zero;
        public bool HasPlayer => player != null;

        /// Secret area containing this tile (null if none).
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

        // ------------------------------------------------------------------ flags
        /// "a,b" = all required; each item "x|y" = any one of the flags. Empty / "never" = never done.
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
                if (StepIndex >= 0 && idx > StepIndex) { StepChangedAt = Time.time; SoulRequiem.Core.Sfx.Play2D("quest_update"); }   // sound
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
                        // a target whose pre is not met makes the arrow point at the prerequisite -> do not highlight the "locked" line
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
            // a running mechanism's temporary objective (minigame) replaces the current step on the panel + the target arrow
            if (QuestGuide.Active)
            {
                Title = QuestGuide.Title; Label = QuestGuide.Label; Checks.Clear();
                Checks.AddRange(QuestGuide.Checks);                                 // mechanism guidance lines (e.g. the Absalom fight)
                DoneCount = QuestGuide.Done; TotalCount = QuestGuide.Total > 1 ? QuestGuide.Total : 0;
                HasTarget = QuestGuide.HasTarget;
                if (HasTarget) { TargetWorld = QuestGuide.World; TargetTile = WorldToTile(QuestGuide.World); }
            }
        }
    }
}
