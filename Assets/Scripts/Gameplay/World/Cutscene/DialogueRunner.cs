using UnityEngine;
using UnityEngine.InputSystem;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>Runs the dialogue box (<see cref="DialogueBox"/>): typewriter text in real time, next/skip keys, control lock.</summary>
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
            GameInput.Blocked = true;                               // stand still while listening (closing the Pause menu releases Blocked -> lock again)
            if (GameInput.MenuOpen) return;
            // cutscene skipping (DevCheats F11) -> flip pages at once, one page per frame (the scene's onClose still runs)
            if (CutsceneSkip.Now) { DialogueBox.Shown = DialogueBox.Line.Length; acc = 0f; DialogueBox.Next(); return; }

            if (!DialogueBox.LineDone)
            {
                acc += Time.unscaledDeltaTime * CharsPerSecond;
                int add = Mathf.FloorToInt(acc);
                if (add > 0) { acc -= add; DialogueBox.Shown = Mathf.Min(DialogueBox.Line.Length, DialogueBox.Shown + add); }
            }

            // auto-advance mode (cutscenes)
            if (DialogueBox.AutoSeconds > 0f && DialogueBox.LineDone)
            {
                autoT += Time.unscaledDeltaTime;
                if (autoT >= DialogueBox.AutoSeconds) { autoT = 0f; DialogueBox.Next(); return; }
            }
            else autoT = 0f;
            if (Time.frameCount == DialogueBox.OpenedFrame) return;   // the T press that opened the box does not count as "next"
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            bool next = GameInput.PressedRaw(GameAction.Interact) ||
                        (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) ||
                        (mouse != null && mouse.leftButton.wasPressedThisFrame);
            if (next) { acc = 0f; DialogueBox.Next(); }
        }
    }
}
