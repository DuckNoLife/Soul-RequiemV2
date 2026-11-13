using UnityEngine;
using UnityEngine.InputSystem;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>02/11: chạy khung hội thoại (<see cref="DialogueBox"/>): chữ chạy theo thời gian thật, nhận phím tiếp/bỏ qua, khoá điều khiển.</summary>
    public class DialogueRunner : MonoBehaviour
    {
        private const float CharsPerSecond = 55f;
        private static DialogueRunner instance;
        private float acc, autoT;
        private bool blocking;

        public static void Ensure()
        {
            if (instance != null) return;
            instance = new GameObject("Dialogue Runner").AddComponent<DialogueRunner>();
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
            if (blocking) GameInput.Blocked = false;
            DialogueBox.Close();
        }

        private void Update()
        {
            if (!DialogueBox.Active)
            {
                if (blocking) { blocking = false; GameInput.Blocked = false; }
                return;
            }
            blocking = true;
            GameInput.Blocked = true;                               // đứng yên nghe nói (đóng menu Pause nhả Blocked -> khoá lại)
            if (GameInput.MenuOpen) return;

            if (!DialogueBox.LineDone)
            {
                acc += Time.unscaledDeltaTime * CharsPerSecond;
                int add = Mathf.FloorToInt(acc);
                if (add > 0) { acc -= add; DialogueBox.Shown = Mathf.Min(DialogueBox.Line.Length, DialogueBox.Shown + add); }
            }

            // 02/11: chế độ tự sang câu (cảnh diễn)
            if (DialogueBox.AutoSeconds > 0f && DialogueBox.LineDone)
            {
                autoT += Time.unscaledDeltaTime;
                if (autoT >= DialogueBox.AutoSeconds) { autoT = 0f; DialogueBox.Next(); return; }
            }
            else autoT = 0f;
            if (Time.frameCount == DialogueBox.OpenedFrame) return;   // phím T vừa mở khung: không tính là "tiếp"
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            bool next = GameInput.PressedRaw(GameAction.Interact) ||
                        (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) ||
                        (mouse != null && mouse.leftButton.wasPressedThisFrame);
            if (next) { acc = 0f; DialogueBox.Next(); }
        }
    }
}
