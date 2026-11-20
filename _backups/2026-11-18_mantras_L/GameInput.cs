using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace SoulRequiem.Core
{
    /// <summary>Các hành động người chơi (phím gán được, sau này đổi phím / tay cầm chỉ cần sửa ở đây).</summary>
    public enum GameAction { Attack, Roll, Heal, Skill1, Skill2, Skill3, Interact, Map, Pause, Inventory }   // 04/11: Inventory (thêm CUỐI để giữ phím đã lưu)

    /// <summary>
    /// Bảng phím trung tâm. Mọi script đọc phím qua đây (không gõ thẳng Keyboard.current.jKey...) để:
    ///   - HUD hiện đúng ký hiệu phím đang gán (ô chiêu, bình máu...),
    ///   - khi menu mở (Pause, Settings, tháo lắp chiêu) -> chặn điều khiển nhân vật (Blocked),
    ///   - đổi phím: lưu PlayerPrefs "key_&lt;Action&gt;";
    ///   - tay cầm: mỗi hành động có một nút cố định (PadButton), di chuyển bằng cần trái / D-pad (Move).
    /// </summary>
    public static class GameInput
    {
        private static readonly Dictionary<GameAction, Key> Defaults = new Dictionary<GameAction, Key>
        {
            { GameAction.Attack, Key.J }, { GameAction.Roll, Key.Q }, { GameAction.Heal, Key.E },
            { GameAction.Skill1, Key.F }, { GameAction.Skill2, Key.R }, { GameAction.Skill3, Key.C },
            { GameAction.Interact, Key.T }, { GameAction.Map, Key.M }, { GameAction.Pause, Key.Escape },
            { GameAction.Inventory, Key.B },
        };
        private static Dictionary<GameAction, Key> keys;

        /// Menu đang mở: nhân vật không nhận phím (trừ Pause để đóng menu).
        public static bool Blocked { get; set; }

        /// <summary>16/11 (§270): đang ĐỌC một bảng (ghi chú, khoá ký tự...): đứng yên, không chém / lăn / hồi máu / dùng chiêu;
        /// chỉ nhận Interact (T, đóng bảng), Pause (Esc) và Map. Đóng bảng mới đi lại được.</summary>
        public static bool Reading { get; set; }

        private static bool ReadingBlocks(GameAction a) => Reading && a != GameAction.Interact && a != GameAction.Pause && a != GameAction.Map;

        public static event Action BindingsChanged;

        private static Dictionary<GameAction, Key> Keys
        {
            get
            {
                if (keys != null) return keys;
                keys = new Dictionary<GameAction, Key>(Defaults);
                foreach (GameAction a in Enum.GetValues(typeof(GameAction)))
                {
                    string s = PlayerPrefs.GetString("key_" + a, "");
                    if (!string.IsNullOrEmpty(s) && Enum.TryParse(s, out Key k) && k != Key.None) keys[a] = k;
                }
                return keys;
            }
        }

        public static Key KeyOf(GameAction a) => Keys.TryGetValue(a, out var k) ? k : Key.None;

        public static Key SkillKey(int slot) => KeyOf(slot == 0 ? GameAction.Skill1 : slot == 1 ? GameAction.Skill2 : GameAction.Skill3);
        public static GameAction SkillAction(int slot) => slot == 0 ? GameAction.Skill1 : slot == 1 ? GameAction.Skill2 : GameAction.Skill3;

        /// Ký hiệu ngắn hiện trên nắp phím: "J", "ESC", "SPC"...
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

        /// <summary>
        /// Thay token phím trong chữ hiển thị bằng phím đang gán, để gợi ý viết trong Tiled không ghi cứng phím:
        /// {attack} {roll} {heal} {skill1} {skill2} {skill3} {interact} {map} {pause} {move}.
        /// Vd "Press {roll} to roll" -> "Press Q to roll". Token lạ giữ nguyên.
        /// </summary>
        public static string ExpandKeys(string s)
        {
            if (string.IsNullOrEmpty(s) || s.IndexOf('{') < 0) return s;
            return s.Replace("{attack}", Label(GameAction.Attack) + " / Left Click")
                    .Replace("{roll}", Label(GameAction.Roll))
                    .Replace("{heal}", Label(GameAction.Heal))
                    .Replace("{skill1}", Label(GameAction.Skill1))
                    .Replace("{skill2}", Label(GameAction.Skill2))
                    .Replace("{skill3}", Label(GameAction.Skill3))
                    .Replace("{interact}", Label(GameAction.Interact))
                    .Replace("{map}", Label(GameAction.Map))
                    .Replace("{inventory}", Label(GameAction.Inventory))
                    .Replace("{pause}", Label(GameAction.Pause))
                    .Replace("{move}", "WASD");
        }

        public static string ActionName(GameAction a)
        {
            switch (a)
            {
                case GameAction.Attack: return "Attack";
                case GameAction.Roll: return "Roll";
                case GameAction.Heal: return "Drink flask";
                case GameAction.Skill1: return "Mantra I";
                case GameAction.Skill2: return "Mantra II";
                case GameAction.Skill3: return "Mantra III";
                case GameAction.Interact: return "Interact / Rest";
                case GameAction.Map: return "Map";
                case GameAction.Pause: return "Pause";
                case GameAction.Inventory: return "Inventory";
            }
            return a.ToString();
        }

        private static KeyControl Control(GameAction a)
        {
            var kb = Keyboard.current;
            var k = KeyOf(a);
            if (kb == null || k == Key.None) return null;
            return kb[k];
        }

        // ------------------------------------------------------------------ tay cầm (cố định, chưa đổi được)
        /// <summary>
        /// Nút tay cầm của từng hành động (theo vị trí, không theo chữ): Tây = X/□ chém, Đông = B/○ lăn, Bắc = Y/△ bình máu,
        /// Nam = A/✕ tương tác, RB / LB / RT = kỹ năng I / II / III, Select = bản đồ, Start = tạm dừng.
        /// </summary>
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
                case GameAction.Inventory: return pad.leftTrigger;     // 04/11: LT
            }
            return null;
        }

        /// Bấm trong khung hình này — bàn phím hoặc tay cầm (bị chặn khi menu mở, trừ Pause).
        /// <summary>02/11: có menu (Pause / nghỉ) đang mở — RequiemMenus đặt.</summary>
        public static bool MenuOpen { get; set; }

        /// <summary>02/11: bấm phím hành động khung này, BỎ QUA Blocked (màn dừng hướng dẫn chờ đúng phím). Attack nhận cả chuột trái.</summary>
        public static bool PressedRaw(GameAction a)
        {
            var c = Control(a);
            if (c != null && c.wasPressedThisFrame) return true;
            var b = PadButton(a);
            if (b != null && b.wasPressedThisFrame) return true;
            if (a == GameAction.Attack) { var m = Mouse.current; if (m != null && m.leftButton.wasPressedThisFrame) return true; }
            return false;
        }

        public static bool Pressed(GameAction a)
        {
            if (Blocked && a != GameAction.Pause) return false;
            if (ReadingBlocks(a)) return false;                                // 16/11 (§270)
            var c = Control(a);
            if (c != null && c.wasPressedThisFrame) return true;
            var b = PadButton(a);
            return b != null && b.wasPressedThisFrame;
        }

        public static bool Held(GameAction a)
        {
            if (Blocked || ReadingBlocks(a)) return false;
            var c = Control(a);
            if (c != null && c.isPressed) return true;
            var b = PadButton(a);
            return b != null && b.isPressed;
        }

        // ------------------------------------------------------------------ di chuyển
        private const float StickDeadZone = 0.2f;

        /// <summary>
        /// Hướng di chuyển: W A S D / phím mũi tên (mỗi trục -1, 0, 1), hoặc cần trái / D-pad của tay cầm.
        /// Menu đang mở (<see cref="Blocked"/>) -> (0, 0). MỌI script đọc di chuyển qua đây.
        /// </summary>
        public static Vector2 Move
        {
            get
            {
                if (Blocked || Reading) return Vector2.zero;                       // 16/11 (§270): đang đọc bảng -> đứng yên
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
                if (stick.magnitude >= StickDeadZone) return Vector2.ClampMagnitude(stick, 1f);
                return pad.dpad.ReadValue();
            }
        }

        public static void Rebind(GameAction a, Key k)
        {
            Keys[a] = k;
            PlayerPrefs.SetString("key_" + a, k.ToString());
            PlayerPrefs.Save();
            BindingsChanged?.Invoke();
        }

        public static void ResetBindings()
        {
            keys = new Dictionary<GameAction, Key>(Defaults);
            foreach (GameAction a in Enum.GetValues(typeof(GameAction))) PlayerPrefs.DeleteKey("key_" + a);
            PlayerPrefs.Save();
            BindingsChanged?.Invoke();
        }
    }
}
