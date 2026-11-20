using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Chuyển map: khoá điều khiển -> rung camera + lời dẫn -> mờ dần sang đen -> tải scene đích ->
    /// đặt người chơi ở player_start (MapEncounterDirector lo) -> áp máu/bình máu mang theo -> hiện dần + tiêu đề vùng.
    /// Tự tạo khi cần (không phải đặt vào scene). Vẽ bằng OnGUI để luôn nằm trên HUD.
    /// </summary>
    [DefaultExecutionOrder(200)]   // sau CameraFollow (100) để cộng độ rung vào vị trí camera
    public class SceneTransition : MonoBehaviour
    {
        // tên đích trong Tiled (thuộc tính 'target' của trigger exit) -> tên scene trong Build Settings
        private static readonly Dictionary<string, string> SceneOf = new Dictionary<string, string>
        {
            { "map1_greystone_plains", "Map1_GreystonePlains" },
            { "map2_undercity_of_vael", "Map2_UndercityOfVael" },
            // tên đích cũ (21/10 đổi tên) — giữ để map/scene cũ vẫn chuyển được
            { "map1_greystone", "Map1_GreystonePlains" },
            { "map2_sewers", "Map2_UndercityOfVael" },
            { "map3_vale_of_graves", "Map3_ValeOfGraves" },
            { "map3_vale", "Map3_ValeOfGraves" },
            { "map1b_hermits_hollow", "Map1b_HermitsHollow" },  // 16/11 (§261): khu bí mật Oathbreaker dưới Old Copper Mine
            { "map3b_kings_barrow", "Map3b_KingsBarrow" },      // 07/11: hầm bí mật X2 tách khỏi Map 3
            { "map4_catacombs", "Map4_CatacombsOfNaming" },     // 08/11 (§192): Map 4 (bản đồ thô) — Requiem Door của Map 3 dẫn vào
        };
        // tiêu đề hiện khi tới nơi
        private static readonly Dictionary<string, string[]> TitleOf = new Dictionary<string, string[]>
        {
            { "map1_greystone_plains", new[] { "Greystone Plains", "The road home" } },
            { "map2_undercity_of_vael", new[] { "The Undercity of Vael", "Beneath the Stone Ring" } },
            { "map1_greystone", new[] { "Greystone Plains", "The road home" } },
            { "map2_sewers", new[] { "The Undercity of Vael", "Beneath the Stone Ring" } },
            { "map3_vale_of_graves", new[] { "The Vale of Graves", "Where the Named are buried" } },
            { "map3_vale", new[] { "The Vale of Graves", "Where the Named are buried" } },
            { "map1b_hermits_hollow", new[] { "Hermit's Hollow", "Beneath the Old Copper Mine" } },
            { "map3b_kings_barrow", new[] { "King's Barrow", "Beneath the Sinking Fields" } },
            { "map4_catacombs", new[] { "The Catacombs of Naming", "Beneath the Requiem Door" } },
        };

        public static bool Busy { get; private set; }
        private static SceneTransition inst;

        private float fade;            // 0 = trong suốt, 1 = đen
        private float shake;           // biên độ rung (đơn vị Unity)
        private string title, subtitle;
        private bool quake = true;     // 07/11: false = chuyển map êm (leo dây), không rung đất
        private float titleUntil;
        private GUIStyle titleStyle, subStyle;

        private static SceneTransition Get()
        {
            if (inst != null) return inst;
            var go = new GameObject("[SceneTransition]");
            DontDestroyOnLoad(go);
            inst = go.AddComponent<SceneTransition>();
            SceneManager.sceneLoaded += inst.OnSceneLoaded;
            return inst;
        }

        /// <summary>Có scene cho đích này trong Build Settings chưa.</summary>
        public static bool CanGo(string target) => Application.CanStreamedLevelBeLoaded(SceneName(target));

        public static string SceneName(string target)
        {
            if (string.IsNullOrEmpty(target)) return "";
            return SceneOf.TryGetValue(target, out var s) ? s : target;
        }

        /// <summary>Bắt đầu chuyển map. departText: lời dẫn hiện lúc rời đi.
        /// 07/11: quake = false -> không rung; arriveTitle/Subtitle trống -> tiêu đề mặc định của đích.</summary>
        public static void Go(Transform player, string target, string departText, string fromMap,
                              bool quake = true, string arriveTitle = "", string arriveSubtitle = "")
        {
            if (Busy) return;
            Get().StartCoroutine(inst.Run(player, target, departText, fromMap, quake, arriveTitle, arriveSubtitle));
        }

        /// <summary>Dịch chuyển trong cùng map (rơi xuống hố, leo dây...): tối dần -> dời người chơi + camera -> sáng lại.
        /// shakeAmt > 0: rung khi tối dần (đất sụt).</summary>
        public static void Warp(Transform player, Vector3 pos, string text, float shakeAmt = 0f)
        {
            if (Busy || player == null) return;
            Get().StartCoroutine(inst.RunWarp(player, pos, text, shakeAmt));
        }

        private IEnumerator RunWarp(Transform player, Vector3 pos, string text, float shakeAmt)
        {
            Busy = true;
            SetControl(player, false);
            for (float t = 0f; t < 0.5f; t += Time.unscaledDeltaTime) { fade = t / 0.5f; shake = shakeAmt * (t / 0.5f); yield return null; }
            fade = 1f; shake = 0f;
            var rb = player.GetComponent<Rigidbody2D>();
            if (rb != null) { rb.position = pos; rb.linearVelocity = Vector2.zero; }
            player.position = pos;
            var cam = FindFirstObjectByType<CameraFollow>();
            if (cam != null) cam.SnapToTarget();
            yield return new WaitForSecondsRealtime(0.4f);
            if (cam != null) cam.SnapToTarget();
            Hints.Show(text, 3f);
            for (float t = 0f; t < 0.7f; t += Time.unscaledDeltaTime) { fade = 1f - t / 0.7f; yield return null; }
            fade = 0f;
            SetControl(player, true);
            Busy = false;
        }

        private IEnumerator Run(Transform player, string target, string departText, string fromMap,
                                bool quakeOn, string arriveTitle, string arriveSubtitle)
        {
            Busy = true;
            quake = quakeOn;
            string scene = SceneName(target);
            SetControl(player, false);
            Hints.Show(departText, 3f);

            // mặt đất rung, rồi sập: rung tăng dần 1.2 s, đen dần 1 s
            if (quake)
                for (float t = 0f; t < 1.2f; t += Time.unscaledDeltaTime) { shake = Mathf.Lerp(0.02f, 0.09f, t / 1.2f); yield return null; }
            else
                yield return new WaitForSecondsRealtime(0.6f);                      // kịp đọc lời dẫn
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime) { fade = t; shake = quake ? 0.09f * (1f - t) : 0f; yield return null; }
            fade = 1f; shake = 0f;

            string[] tt = TitleOf.TryGetValue(target, out var v) ? (string[])v.Clone() : new[] { scene, "" };
            if (!string.IsNullOrEmpty(arriveTitle)) { tt[0] = arriveTitle; tt[1] = arriveSubtitle ?? ""; }
            GameSession.Capture(player != null ? player.gameObject : null, fromMap, tt[0], tt[1]);
            var op = SceneManager.LoadSceneAsync(scene);
            while (op != null && !op.isDone) yield return null;
            // phần còn lại ở OnSceneLoaded
        }

        private void OnSceneLoaded(Scene s, LoadSceneMode mode)
        {
            if (!Busy) return;
            StartCoroutine(Arrive());
        }

        private IEnumerator Arrive()
        {
            yield return null;                 // đợi Start() của MapEncounterDirector đặt người chơi ở player_start
            yield return null;
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null)
            {
                GameSession.ApplyTo(pc.gameObject);
                SetControl(pc.transform, false);
            }
            title = GameSession.ArrivalTitle;
            subtitle = GameSession.ArrivalSubtitle;
            float land = quake ? 0.07f : 0f;                                         // tiếp đất (leo dây: không rung)
            shake = land;
            for (float t = 0f; t < 1.2f; t += Time.unscaledDeltaTime)
            {
                fade = 1f - t / 1.2f;
                shake = land * (1f - t / 0.5f);
                if (shake < 0f) shake = 0f;
                yield return null;
            }
            fade = 0f; shake = 0f;
            titleUntil = Time.unscaledTime + 3.5f;
            if (pc != null) SetControl(pc.transform, true);
            Busy = false;
        }

        private static void SetControl(Transform player, bool on)
        {
            if (player == null) return;
            var pc = player.GetComponent<PlayerController>();
            if (pc != null) pc.enabled = on;
            var kit = player.GetComponent<PlayerCombat>();
            if (kit != null) kit.enabled = on;
            var rb = player.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = Vector2.zero;
            var hp = player.GetComponent<Health>();
            if (hp != null) hp.Invulnerable = !on;
            var anim = player.GetComponent<Animator>();
            if (anim != null && !on) anim.SetFloat("Speed", 0f);
        }

        private void LateUpdate()
        {
            if (shake <= 0f) return;
            var cam = Camera.main;
            if (cam == null) return;
            cam.transform.position += (Vector3)(Random.insideUnitCircle * shake);
        }

        private void OnGUI()
        {
            GUI.depth = -1000;
            if (fade > 0.001f)
            {
                var c = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, Mathf.Clamp01(fade));
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = c;
            }
            float left = titleUntil - Time.unscaledTime;
            if (!string.IsNullOrEmpty(title) && (left > 0f || fade > 0.2f))
            {
                float s = Screen.height / 720f;
                titleStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                subStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Italic };
                titleStyle.fontSize = Mathf.RoundToInt(40 * s);
                subStyle.fontSize = Mathf.RoundToInt(20 * s);
                float a = fade > 0.2f ? 1f : Mathf.Clamp01(left / 1f);
                var r = new Rect(0, Screen.height * 0.30f, Screen.width, 60 * s);
                var r2 = new Rect(0, Screen.height * 0.30f + 52 * s, Screen.width, 34 * s);
                DrawText(r, title, titleStyle, new Color(0.86f, 0.95f, 0.78f, a), s);
                if (!string.IsNullOrEmpty(subtitle)) DrawText(r2, subtitle, subStyle, new Color(0.75f, 0.8f, 0.85f, a), s);
            }
        }

        private static void DrawText(Rect r, string text, GUIStyle st, Color col, float s)
        {
            var old = st.normal.textColor;
            st.normal.textColor = new Color(0, 0, 0, col.a * 0.8f);
            GUI.Label(new Rect(r.x + 2 * s, r.y + 2 * s, r.width, r.height), text, st);
            st.normal.textColor = col;
            GUI.Label(r, text, st);
            st.normal.textColor = old;
        }
    }
}
