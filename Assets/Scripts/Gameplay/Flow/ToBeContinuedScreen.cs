using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// END-OF-CHAPTER screen after The Crystal Vessel fight (Absalom fled through the portal).
    /// Fade to black 1.5 s -> "MAP 5 COMING SOON!" types in letter by letter + a violet light line spreads ->
    /// after 3 s the "Back to home" button appears (mouse click or gamepad A) -> fade out -> scene "Title".
    /// Drawn with OnGUI (always above the HUD). Created on demand by <see cref="Show"/>. (The old class name is kept so callers need no change.)
    /// Called by: VesselDomainMechanism (EnterPortal, ShowComingSoon).
    /// </summary>
    public sealed class ToBeContinuedScreen : MonoBehaviour
    {
        private const string Title = "MAP 5 COMING SOON!";
        private const string ButtonText = "Back to home";
        private const float ButtonAt = 3.2f;
        private static ToBeContinuedScreen live;
        private Action onLeave;
        private float t0, leaveAt = -1f;
        private Font titleFont, bodyFont;
        private GUIStyle titleStyle, buttonStyle;

        public static bool Active => live != null;

        /// <summary>Show the screen. onLeave runs when the player presses Back to home (before the scene change).</summary>
        public static void Show(Action onLeave)
        {
            if (live != null) return;
            var g = new GameObject("Chapter Coming Soon");
            DontDestroyOnLoad(g);
            live = g.AddComponent<ToBeContinuedScreen>();
            live.onLeave = onLeave;
            live.t0 = Time.unscaledTime;
            GameInput.Blocked = true;
            Cursor.visible = true;                                                       // show the cursor to click the button
            Cursor.lockState = CursorLockMode.None;
            Sfx.Play2D("bell_toll");
        }

        private void Awake()
        {
            titleFont = Resources.Load<Font>("UI/Requiem/Fonts/RequiemTitle");
            bodyFont = Resources.Load<Font>("UI/Requiem/Fonts/RequiemBody");
        }

        private void Update()
        {
            float t = Time.unscaledTime - t0;
            if (leaveAt < 0f && t > ButtonAt + 0.4f && ConfirmPressed()) Press();
            if (leaveAt > 0f && Time.unscaledTime - leaveAt > 1.2f) Leave();
        }

        /// <summary>Only the on-screen button (mouse) or gamepad A — the keyboard does not leave.</summary>
        private static bool ConfirmPressed()
        {
            var gp = Gamepad.current;
            return gp != null && gp.buttonSouth.wasPressedThisFrame;
        }

        private void Press()
        {
            if (leaveAt > 0f) return;
            leaveAt = Time.unscaledTime;
            Sfx.Play2D("ui_confirm");
        }

        private void Leave()
        {
            var cb = onLeave; onLeave = null;
            live = null;
            try { cb?.Invoke(); } catch (Exception e) { Debug.LogException(e); }
            GameInput.Blocked = false;
            if (Application.CanStreamedLevelBeLoaded("Title")) SceneManager.LoadScene("Title");
            Destroy(gameObject);
        }

        private void OnGUI()
        {
            GUI.depth = -2000;
            float t = Time.unscaledTime - t0, s = Screen.height / 720f;
            float black = Mathf.Clamp01(t / 1.5f);
            float leave = leaveAt > 0f ? Mathf.Clamp01((Time.unscaledTime - leaveAt) / 1.1f) : 0f;
            var c0 = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, Mathf.Max(black, leave));
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

            titleStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Overflow };
            titleStyle.font = bodyFont;                                                   // the RequiemTitle font cannot be drawn in OnGUI (text disappears) -> bold body font
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.fontSize = Mathf.RoundToInt(46 * s);
            float fade = 1f - leave;
            float y = Screen.height * 0.38f;

            // title text: types in letter by letter from 1.4 s
            int shown = Mathf.Clamp(Mathf.FloorToInt((t - 1.4f) / 0.06f), 0, Title.Length);
            if (shown > 0)
            {
                string txt = Title.Substring(0, shown);
                var r = new Rect(0, y - 50 * s, Screen.width, 100 * s);
                Label(new Rect(r.x + 3 * s, r.y + 3 * s, r.width, r.height), txt, titleStyle, new Color(0.15f, 0.02f, 0.25f, fade));
                Label(r, txt, titleStyle, new Color(0.95f, 0.9f, 1f, fade));
            }
            // violet light line spreading to both sides
            float line = Mathf.Clamp01((t - 2.6f) / 0.8f);
            if (line > 0f)
            {
                float w = Screen.width * 0.36f * line * line * (3f - 2f * line);
                GUI.color = new Color(0.72f, 0.45f, 1f, 0.85f * fade);
                GUI.DrawTexture(new Rect(Screen.width * 0.5f - w, y + 52 * s, w * 2f, Mathf.Max(1f, 2f * s)), Texture2D.whiteTexture);
                GUI.color = new Color(1f, 1f, 1f, 0.9f * fade);
                GUI.DrawTexture(new Rect(Screen.width * 0.5f - w * 0.35f, y + 52 * s, w * 0.7f, Mathf.Max(1f, 1f * s)), Texture2D.whiteTexture);
            }
            // Back to home button
            float b = Mathf.Clamp01((t - ButtonAt) / 0.6f) * fade;
            if (b > 0f)
            {
                buttonStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Overflow };
                buttonStyle.font = bodyFont;
                buttonStyle.fontSize = Mathf.RoundToInt(22 * s);
                float bw = 280 * s, bh = 52 * s;
                var br = new Rect(Screen.width * 0.5f - bw * 0.5f, Screen.height * 0.62f, bw, bh);
                bool hover = br.Contains(Event.current.mousePosition);
                float pulse = 0.5f + 0.5f * Mathf.Sin((t - ButtonAt) * 3f);
                // frame: dark violet fill + gold border (Requiem menu style)
                GUI.color = new Color(0.12f, 0.06f, 0.18f, 0.92f * b);
                GUI.DrawTexture(br, Texture2D.whiteTexture);
                Color edge = hover ? new Color(1f, 0.85f, 0.45f, b) : new Color(0.85f, 0.68f, 0.35f, (0.55f + 0.35f * pulse) * b);
                float e = Mathf.Max(1f, 2f * s);
                GUI.color = edge;
                GUI.DrawTexture(new Rect(br.x, br.y, br.width, e), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(br.x, br.yMax - e, br.width, e), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(br.x, br.y, e, br.height), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(br.xMax - e, br.y, e, br.height), Texture2D.whiteTexture);
                Label(br, ButtonText, buttonStyle, hover ? new Color(1f, 0.95f, 0.8f, b) : new Color(0.92f, 0.88f, 1f, b));
                if (leaveAt < 0f && b >= 0.6f && Event.current.type == EventType.MouseDown && Event.current.button == 0 && hover)
                {
                    Event.current.Use();
                    Press();
                }
            }
            GUI.color = c0;
        }

        /// <summary>GUI.color MULTIPLIES the text colour — if it still holds the black of the overlay just drawn,
        /// the text and the "Back to home" button turn black on black. Draw text with a white GUI.color, then restore it.</summary>
        private static void Label(Rect r, string text, GUIStyle st, Color col)
        {
            var keep = GUI.color;
            GUI.color = Color.white;
            st.normal.textColor = st.hover.textColor = st.active.textColor = st.focused.textColor = col;
            GUI.Label(r, text, st);
            GUI.color = keep;
        }
    }
}
