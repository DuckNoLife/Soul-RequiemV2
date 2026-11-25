using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Map change: lock input -> camera shake + departure text -> fade to black -> load the target scene ->
    /// place the player at player_start (done by MapEncounterDirector) -> apply carried HP / flasks -> fade in + region title.
    /// Created on demand (no need to put it in a scene). Drawn with OnGUI so it is always above the HUD.
    /// Called by: MapTriggerHandler (exits), DevCheats, VesselDomainMechanism / AbsalomHallMechanism (portal), Title / Chapters.
    /// </summary>
    [DefaultExecutionOrder(200)]   // after CameraFollow (100) so the shake is added to the camera position
    public class SceneTransition : MonoBehaviour
    {
        // target name in Tiled (property 'target' of an exit trigger) -> scene name in Build Settings
        private static readonly Dictionary<string, string> SceneOf = new Dictionary<string, string>
        {
            { "map1_greystone_plains", "Map1_GreystonePlains" },
            { "map2_undercity_of_vael", "Map2_UndercityOfVael" },
            // old target names — kept so old maps / scenes still transition
            { "map1_greystone", "Map1_GreystonePlains" },
            { "map2_sewers", "Map2_UndercityOfVael" },
            { "map3_vale_of_graves", "Map3_ValeOfGraves" },
            { "map3_vale", "Map3_ValeOfGraves" },
            { "map1b_hermits_hollow", "Map1b_HermitsHollow" },  // Oathbreaker secret area under the Old Copper Mine
            { "map2b_kroven_deep", "Map2b_KrovenDeep" },        // Ironcrown secret area under the Forgotten Store
            { "map3b_kings_barrow", "Map3b_KingsBarrow" },      // X2 secret crypt split off Map 3
            { "map4_catacombs", "Map4_CatacombsOfNaming" },     // Map 4 — entered through the Requiem Door of Map 3
            { "map4m_margin", "Map4m_TheMargin" },              // The Margin — The Registrar's own arena
            { "map4d_drill", "Map4d_DrillYard" },               // Burlap's Proving Grounds — 3 drill yards split off Map 4
            { "map4n_unwritten", "Map4n_TheUnwritten" },        // domain pha 3 Absalom — The Crystal Vessel
        };
        // title shown on arrival
        private static readonly Dictionary<string, string[]> TitleOf = new Dictionary<string, string[]>
        {
            { "map1_greystone_plains", new[] { "Greystone Plains", "The road home" } },
            { "map2_undercity_of_vael", new[] { "The Undercity of Vael", "Beneath the Stone Ring" } },
            { "map1_greystone", new[] { "Greystone Plains", "The road home" } },
            { "map2_sewers", new[] { "The Undercity of Vael", "Beneath the Stone Ring" } },
            { "map3_vale_of_graves", new[] { "The Vale of Graves", "Where the Named are buried" } },
            { "map3_vale", new[] { "The Vale of Graves", "Where the Named are buried" } },
            { "map1b_hermits_hollow", new[] { "Hermit's Hollow", "Beneath the Old Copper Mine" } },
            { "map2b_kroven_deep", new[] { "The Kroven Deep", "Beneath the Records Vault" } },   // the way down moved to the Records Vault
            { "map3b_kings_barrow", new[] { "King's Barrow", "Beneath the Sinking Fields" } },
            { "map4_catacombs", new[] { "The Catacombs of Naming", "Beneath the Requiem Door" } },
            { "map4m_margin", new[] { "The Margin", "A page of your own" } },
            { "map4d_drill", new[] { "Burlap's Proving Grounds", "The Drill Yard" } },
            { "map4n_unwritten", new[] { "The Unwritten", "Where the Vessel waits" } },
        };

        public static bool Busy { get; private set; }
        private static SceneTransition inst;

        private float fade;            // 0 = transparent, 1 = black
        private float shake;           // shake amplitude (Unity units)
        private string title, subtitle;
        private bool quake = true;     // false = quiet map change (climbing a rope), no ground shake
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

        /// <summary>Is there a scene for this target in Build Settings.</summary>
        public static bool CanGo(string target) => Application.CanStreamedLevelBeLoaded(SceneName(target));

        public static string SceneName(string target)
        {
            if (string.IsNullOrEmpty(target)) return "";
            return SceneOf.TryGetValue(target, out var s) ? s : target;
        }

        /// <summary>Start a map change. departText: text shown when leaving.
        /// quake = false -> no shake; empty arriveTitle/Subtitle -> the target's default title.</summary>
        public static void Go(Transform player, string target, string departText, string fromMap,
                              bool quake = true, string arriveTitle = "", string arriveSubtitle = "")
        {
            if (Busy) return;
            Get().StartCoroutine(inst.Run(player, target, departText, fromMap, quake, arriveTitle, arriveSubtitle));
        }

        /// <summary>Move within the same map (falling into a pit, climbing a rope...): fade to black -> move player + camera -> fade in.
        /// shakeAmt > 0: shake while fading (ground collapsing).</summary>
        public static void Warp(Transform player, Vector3 pos, string text, float shakeAmt = 0f)
        {
            if (Busy || player == null) return;
            Get().StartCoroutine(inst.RunWarp(player, pos, text, shakeAmt));
        }

        private IEnumerator RunWarp(Transform player, Vector3 pos, string text, float shakeAmt)
        {
            Busy = true;
            SetControl(player, false);
            Sfx.Play2D("teleport_dark", 0.7f);                                     // sound
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

            // the ground shakes, then collapses: shake rising over 1.2 s, fade to black over 1 s
            if (quake) Sfx.Play2D("quake_rumble", 0.9f);                           // sound
            if (quake)
                for (float t = 0f; t < 1.2f; t += Time.unscaledDeltaTime) { shake = Mathf.Lerp(0.02f, 0.09f, t / 1.2f); yield return null; }
            else
                yield return new WaitForSecondsRealtime(0.6f);                      // time to read the departure text
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime) { fade = t; shake = quake ? 0.09f * (1f - t) : 0f; yield return null; }
            fade = 1f; shake = 0f;

            string[] tt = TitleOf.TryGetValue(target, out var v) ? (string[])v.Clone() : new[] { scene, "" };
            if (!string.IsNullOrEmpty(arriveTitle)) { tt[0] = arriveTitle; tt[1] = arriveSubtitle ?? ""; }
            GameSession.Capture(player != null ? player.gameObject : null, fromMap, tt[0], tt[1]);
            var op = SceneManager.LoadSceneAsync(scene);
            while (op != null && !op.isDone) yield return null;
            // the rest happens in OnSceneLoaded
        }

        private void OnSceneLoaded(Scene s, LoadSceneMode mode)
        {
            if (!Busy) return;
            StartCoroutine(Arrive());
        }

        private IEnumerator Arrive()
        {
            yield return null;                 // wait for MapEncounterDirector.Start() to place the player at player_start
            yield return null;
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null)
            {
                GameSession.ApplyTo(pc.gameObject);
                SetControl(pc.transform, false);
            }
            title = GameSession.ArrivalTitle;
            subtitle = GameSession.ArrivalSubtitle;
            float land = quake ? 0.07f : 0f;                                         // landing (rope climb: no shake)
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
            // IMGUI changes label text colour on hover / active when the mouse passes over (the title would dim then brighten)
            // -> use the SAME colour for every state.
            SetColor(st, new Color(0, 0, 0, col.a * 0.8f));
            GUI.Label(new Rect(r.x + 2 * s, r.y + 2 * s, r.width, r.height), text, st);
            SetColor(st, col);
            GUI.Label(r, text, st);
        }

        private static void SetColor(GUIStyle st, Color c)
        {
            st.normal.textColor = st.hover.textColor = st.active.textColor = st.focused.textColor = c;
            st.onNormal.textColor = st.onHover.textColor = st.onActive.textColor = st.onFocused.textColor = c;
        }
    }
}
