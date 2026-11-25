using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace SoulRequiem.Core
{
    /// <summary>Player actions. Add new actions at the END (saved keys are stored by name).</summary>
    public enum GameAction { Attack, Roll, Heal, Skill1, Skill2, Skill3, Interact, Map, Pause, Inventory, Mantras }

    /// <summary>
    /// CENTRAL KEY TABLE — every script reads keys / movement through here, never Keyboard.current directly.
    ///   Change default keys    : table <see cref="DefaultKeys"/>.
    ///   Change gamepad buttons : <see cref="PadButton"/>.
    ///   Key tokens in text     : <see cref="ExpandKeys"/> ({roll} -> "Q"...).
    ///   Block input            : Blocked (menu open), Reading (reading a panel), InvertedUntil (inverted movement — Red Field skill).
    /// Keys rebound by the player are saved in PlayerPrefs "key_&lt;Action&gt;".
    /// </summary>
    public static class GameInput
    {
        // ================================================================== keyboard keys
        private static readonly Dictionary<GameAction, Key> DefaultKeys = new Dictionary<GameAction, Key>
        {
            { GameAction.Attack, Key.J },    { GameAction.Roll, Key.Q },      { GameAction.Heal, Key.E },
            { GameAction.Skill1, Key.F },    { GameAction.Skill2, Key.R },    { GameAction.Skill3, Key.C },
            { GameAction.Interact, Key.T },  { GameAction.Map, Key.M },       { GameAction.Pause, Key.Escape },
            { GameAction.Inventory, Key.B }, { GameAction.Mantras, Key.L },
        };
        private static Dictionary<GameAction, Key> keys;
        public static event Action BindingsChanged;

        /// <summary>Currently bound key (loads PlayerPrefs on first use).</summary>
        private static Dictionary<GameAction, Key> Keys
        {
            get
            {
                if (keys != null) return keys;
                keys = new Dictionary<GameAction, Key>(DefaultKeys);
                foreach (GameAction a in Enum.GetValues(typeof(GameAction)))
                {
                    string saved = PlayerPrefs.GetString("key_" + a, "");
                    if (!string.IsNullOrEmpty(saved) && Enum.TryParse(saved, out Key k) && k != Key.None) keys[a] = k;
                }
                return keys;
            }
        }

        public static Key KeyOf(GameAction a) => Keys.TryGetValue(a, out var k) ? k : Key.None;
        public static GameAction SkillAction(int slot) => slot == 0 ? GameAction.Skill1 : slot == 1 ? GameAction.Skill2 : GameAction.Skill3;
        public static Key SkillKey(int slot) => KeyOf(SkillAction(slot));

        public static void Rebind(GameAction a, Key k)
        {
            Keys[a] = k;
            PlayerPrefs.SetString("key_" + a, k.ToString());
            PlayerPrefs.Save();
            BindingsChanged?.Invoke();
        }

        public static void ResetBindings()
        {
            keys = new Dictionary<GameAction, Key>(DefaultKeys);
            foreach (GameAction a in Enum.GetValues(typeof(GameAction))) PlayerPrefs.DeleteKey("key_" + a);
            PlayerPrefs.Save();
            BindingsChanged?.Invoke();
        }

        // ================================================================== display text
        /// <summary>Short keycap label: "J", "ESC", "SPC"...</summary>
        public static string Label(GameAction a) => KeyLabel(KeyOf(a));

        public static string KeyLabel(Key k)
        {
            switch (k)
            {
                case Key.Escape: return "ESC";
                case Key.Space: return "SPC";
                case Key.LeftShift: case Key.RightShift: return "SHF";
                case Key.LeftCtrl: case Key.RightCtrl: return "CTL";
                case Key.Tab: return "TAB";
                case Key.Enter: return "ENT";
                case Key.None: return "-";
            }
            string s = k.ToString();
            if (s.StartsWith("Digit")) return s.Substring(5);
            return s.Length > 3 ? s.Substring(0, 3).ToUpperInvariant() : s.ToUpperInvariant();
        }

        /// <summary>Token -> action, used in text written in Tiled / code ("Press {roll} to roll").</summary>
        private static readonly Dictionary<string, GameAction> Tokens = new Dictionary<string, GameAction>
        {
            { "{roll}", GameAction.Roll }, { "{heal}", GameAction.Heal }, { "{skill1}", GameAction.Skill1 },
            { "{skill2}", GameAction.Skill2 }, { "{skill3}", GameAction.Skill3 }, { "{interact}", GameAction.Interact },
            { "{map}", GameAction.Map }, { "{inventory}", GameAction.Inventory }, { "{mantras}", GameAction.Mantras },
            { "{pause}", GameAction.Pause },
        };

        /// <summary>Replace key tokens with the bound keys. {attack} = "J / Left Click", {move} = "WASD". Unknown tokens stay as they are.</summary>
        public static string ExpandKeys(string s)
        {
            if (string.IsNullOrEmpty(s) || s.IndexOf('{') < 0) return s;
            s = s.Replace("{attack}", Label(GameAction.Attack) + " / Left Click").Replace("{move}", "WASD");
            foreach (var t in Tokens) s = s.Replace(t.Key, Label(t.Value));
            return s;
        }

        /// <summary>Action name in the Controls table (Settings).</summary>
        public static string ActionName(GameAction a)
        {
            switch (a)
            {
                case GameAction.Heal: return "Drink flask";
                case GameAction.Skill1: return "Mantra I";
                case GameAction.Skill2: return "Mantra II";
                case GameAction.Skill3: return "Mantra III";
                case GameAction.Interact: return "Interact / Rest";
                default: return a.ToString();
            }
        }

        // ================================================================== input blocking state
        /// <summary>A menu is open: the character ignores keys (except Pause to close the menu).</summary>
        public static bool Blocked { get; set; }
        /// <summary>A menu panel (Pause / inventory / rest) is open — set by RequiemMenus.</summary>
        public static bool MenuOpen { get; set; }
        /// <summary>Reading a panel (note, cipher lock): stand still, only Interact / Pause / Map are accepted.</summary>
        public static bool Reading { get; set; }
        /// <summary>The mouse is over a clickable HUD button -> left click does not count as an attack.</summary>
        public static bool MouseOverUI;
        /// <summary>Until this Time.time, movement keys are INVERTED (The Red Field's blood pool skill). 0 = normal.</summary>
        public static float InvertedUntil;
        public static bool Inverted => InvertedUntil > 0f && Time.time < InvertedUntil;

        private static bool ReadingBlocks(GameAction a) => Reading && a != GameAction.Interact && a != GameAction.Pause && a != GameAction.Map;

        // ================================================================== reading buttons
        private static KeyControl KeyControlOf(GameAction a)
        {
            var kb = Keyboard.current;
            var k = KeyOf(a);
            return kb == null || k == Key.None ? null : kb[k];
        }

        /// <summary>Fixed gamepad buttons (by position): West attack, East roll, North flask, South interact,
        /// RB / LB / RT = Mantra I / II / III, Select map, Start pause, LT inventory, R3 Mantra panel.</summary>
        private static ButtonControl PadButton(GameAction a)
        {
            var pad = Gamepad.current;
            if (pad == null) return null;
            switch (a)
            {
                case GameAction.Attack: return pad.buttonWest;
                case GameAction.Roll: return pad.buttonEast;
                case GameAction.Heal: return pad.buttonNorth;
                case GameAction.Interact: return pad.buttonSouth;
                case GameAction.Skill1: return pad.rightShoulder;
                case GameAction.Skill2: return pad.leftShoulder;
                case GameAction.Skill3: return pad.rightTrigger;
                case GameAction.Map: return pad.selectButton;
                case GameAction.Pause: return pad.startButton;
                case GameAction.Inventory: return pad.leftTrigger;
                case GameAction.Mantras: return pad.rightStickButton;
            }
            return null;
        }

        private static bool WasPressed(GameAction a)
        {
            var k = KeyControlOf(a);
            if (k != null && k.wasPressedThisFrame) return true;
            var b = PadButton(a);
            return b != null && b.wasPressedThisFrame;
        }

        /// <summary>Pressed this frame, IGNORING Blocked / Reading (tutorial screens waiting for an exact key). Attack also accepts left click.</summary>
        public static bool PressedRaw(GameAction a)
        {
            if (WasPressed(a)) return true;
            if (a != GameAction.Attack) return false;
            var m = Mouse.current;
            return m != null && m.leftButton.wasPressedThisFrame && !MouseOverUI;
        }

        /// <summary>Pressed this frame (menu open -> only Pause; reading a panel -> only Interact / Pause / Map).</summary>
        public static bool Pressed(GameAction a)
        {
            if (Blocked && a != GameAction.Pause) return false;
            if (ReadingBlocks(a)) return false;
            return WasPressed(a);
        }

        /// <summary>Key / button is held.</summary>
        public static bool Held(GameAction a)
        {
            if (Blocked || ReadingBlocks(a)) return false;
            var k = KeyControlOf(a);
            if (k != null && k.isPressed) return true;
            var b = PadButton(a);
            return b != null && b.isPressed;
        }

        // ================================================================== movement
        private const float StickDeadZone = 0.2f;

        /// <summary>Movement direction: WASD / arrows (each axis -1, 0, 1), otherwise the left stick / D-pad. Blocked -> (0, 0).</summary>
        public static Vector2 Move => Inverted ? -RawMove : RawMove;

        private static Vector2 RawMove
        {
            get
            {
                if (Blocked || Reading) return Vector2.zero;
                var v = Vector2.zero;
                var kb = Keyboard.current;
                if (kb != null)
                {
                    if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) v.x -= 1f;
                    if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) v.x += 1f;
                    if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v.y -= 1f;
                    if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v.y += 1f;
                }
                if (v != Vector2.zero) return v;
                var pad = Gamepad.current;
                if (pad == null) return Vector2.zero;
                var stick = pad.leftStick.ReadValue();
                return stick.magnitude >= StickDeadZone ? Vector2.ClampMagnitude(stick, 1f) : pad.dpad.ReadValue();
            }
        }
    }
}
