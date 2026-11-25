using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using SoulRequiem.Core;
using SoulRequiem.Gameplay;

namespace SoulRequiem.UI
{
    /// <summary>
    /// Out-of-combat screens (UI Toolkit, same Requiem image set):
    ///   - PAUSE (Esc): Resume / Settings / Quit to Title (confirmation). Stops time, blocks input.
    ///   - SETTINGS: Audio / Display / Gameplay / Controls (saved in PlayerPrefs, applied at once). Shared with the Title (BuildSettings).
    ///   - REST / SKILLS (T at a rest point): pick a skill slot, then a skill to equip; unequip skills.
    ///   - DEATH: "YOU HAVE FALLEN" overlay while down -> respawn at the last rest point.
    ///   - INVENTORY (B) + SWORD RACK, MANTRA PANEL (L anywhere).
    /// Esc inside a sub-menu = back. Opening a panel = stop time + block input (Push / Pop).
    /// Files of this class: RequiemMenus.cs (menu stack, keys, UI sounds, HUD buttons, death screen) · .Pause.cs · .Settings.cs
    ///   .Inventory.cs (inventory, key / note cards) · .Weapons.cs (sword rack, katana stats card) · .Rest.cs (Mantra / rest panel)
    ///   .Widgets.cs (shared UI build helpers: Txt, Row, KeyCap, ActionButton, Tab, bar-less scrolling...)
    /// </summary>
    public partial class RequiemMenus : MonoBehaviour
    {
        public static bool Open => instance != null && instance.stack.Count > 0;
        private static RequiemMenus instance;

        private readonly List<VisualElement> stack = new List<VisualElement>();
        private VisualElement layer, dim, death;
        private Label deathTitle, deathSub;
        private float deathT, savedScale = 1f;
        private Health hp;
        private PlayerLoadout loadout;

        private void Awake() => instance = this;

        private void Start()
        {
            var ui = RequiemUI.Instance;
            if (ui == null) { enabled = false; return; }
            layer = ui.Menu;
            dim = new VisualElement();
            dim.AddToClassList("sr-fill"); dim.AddToClassList("sr-dim");
            layer.Add(dim);
            // standard UI sounds —
            //   moving to another entry (keys / gamepad / HOVERING a button or row) = ui_move; pressing a CLICKABLE entry = ui_confirm (clicking empty background: silent);
            //   sliders / steppers tick by themselves (RequiemUI); switching section Q/E = page turn; the automatic focus when a panel opens / closes is silent.
            layer.RegisterCallback<FocusInEvent>(e =>
            {
                if (Time.unscaledTime >= quietFocusUntil && e.target is VisualElement v && v.focusable) SoulRequiem.Core.Sfx.Play2D("ui_move");
            }, TrickleDown.TrickleDown);
            layer.RegisterCallback<NavigationSubmitEvent>(_ => SoulRequiem.Core.Sfx.Play2D("ui_confirm"), TrickleDown.TrickleDown);
            layer.RegisterCallback<ClickEvent>(e =>
            {
                if (RequiemUI.Pressable(e.target as VisualElement, layer)) SoulRequiem.Core.Sfx.Play2D("ui_confirm");
            }, TrickleDown.TrickleDown);
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null) { hp = pc.GetComponent<Health>(); loadout = pc.GetComponent<PlayerLoadout>(); }
            BuildDeath(ui.Fx);
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
            if (stack.Count > 0) { Time.timeScale = savedScale; GameInput.Blocked = false; }
            GameInput.MenuOpen = false;
        }

        private void Update()
        {
            // Unity reloading code during Play (domain reload) -> the (non-serialized) UI references become null
            // and Start() does not run again. Skip instead of throwing NullReferenceException on every Esc press; stopping and restarting Play fixes it.
            // To avoid it entirely: Edit > Preferences > General > Script Changes While Playing = Recompile After Finished Playing.
            if (layer == null) return;
            bool dead = hp != null && hp.IsDead;
            var dir = MapEncounterDirector.Instance;
            // key B (gamepad LT) opens / closes the inventory (ignores Blocked so it can close while open)
            if (GameInput.PressedRaw(GameAction.Inventory) && !dead && !MapMechanics.ConsoleActive && !MapMechanics.PanelOpen
                && !CutsceneState.Active && !TutorialPrompt.Active && !DialogueBox.Active)
            {
                if (stack.Count == 0 && !GameInput.Blocked) { Push(BuildInventory()); return; }
                if (stack.Count > 0 && inventoryPanel != null && stack[stack.Count - 1] == inventoryPanel) { Pop(); return; }
            }
            // key L (gamepad R3) opens / closes the Mantra panel ANYWHERE (no rest point needed).
            // Opening it stops time like the inventory. Not during cutscenes / dialogue / reading a panel; Mantras sealed by the map -> a message.
            if (GameInput.PressedRaw(GameAction.Mantras) && !dead && !MapMechanics.ConsoleActive && !MapMechanics.PanelOpen
                && !CutsceneState.Active && !TutorialPrompt.Active && !DialogueBox.Active)
            {
                if (stack.Count == 0 && !GameInput.Blocked) { OpenMantras(); return; }
                if (stack.Count > 0 && restPanel != null && stack[stack.Count - 1] == restPanel) { Pop(); return; }
            }
            if (stack.Count > 0 && inventoryPanel != null && stack[stack.Count - 1] == inventoryPanel) InvTick();
            if (stack.Count > 0 && restPanel != null && stack[stack.Count - 1] == restPanel)
            {
                InvTick();
                var kb2 = UnityEngine.InputSystem.Keyboard.current; var gp2 = UnityEngine.InputSystem.Gamepad.current;
                if ((kb2 != null && (kb2.deleteKey.wasPressedThisFrame || kb2.backspaceKey.wasPressedThisFrame)) || (gp2 != null && gp2.buttonWest.wasPressedThisFrame)) restClear?.Invoke();
            }
            if (GameInput.Pressed(GameAction.Pause))
            {
                if (stack.Count > 0) Pop();
                else if (!dead && !MapMechanics.ConsoleActive && !MapMechanics.PanelOpen) Push(BuildPause());
            }
            else if (stack.Count == 0 && !dead && dir != null && dir.AtCheckpoint && InteractionSystem.Focus == null && GameInput.Pressed(GameAction.Interact))
            {
                // resting at a campfire = full HP, poison cured, flasks refilled to max (Souls style: dying only costs the walk back, never resources)
                if (hp != null) { hp.SetCurrent(hp.Max); hp.CurePoison(); }
                var kit = hp != null ? hp.GetComponent<PlayerCombat>() : null;
                if (kit != null) kit.RefillPotions();
                dir.SaveProgress();                                   // resting = save the game
                Hints.Show("You rest by the fire. Health and flasks restored - progress saved.", 3f);
                pushSfx = "rest"; // sound — resting at a campfire / real candle
                Push(BuildRest(true));
            }
            UpdateDeath(dead);
        }

        // ------------------------------------------------------------------ menu stack
        private float quietFocusUntil;   // automatic focus when a panel opens / closes does not play ui_move

        private void Push(VisualElement panel)
        {
            quietFocusUntil = Time.unscaledTime + 0.25f;
            if (stack.Count == 0)
            {
                savedScale = Time.timeScale > 0f ? Time.timeScale : 1f;
                Time.timeScale = 0f;
                GameInput.Blocked = true;
                layer.style.display = DisplayStyle.Flex;
            }
            else stack[stack.Count - 1].style.display = DisplayStyle.None;
            stack.Add(panel);
            GameInput.MenuOpen = true;
            layer.Add(panel);
            FocusFirst(panel);
            SoulRequiem.Core.Sfx.Play2D(pushSfx ?? (stack.Count <= 1 ? "ui_open" : "ui_confirm"));   // sound
            pushSfx = null;
        }

        private string pushSfx;   // custom sound for the panel about to Push (null = default)

        private void Pop()
        {
            if (stack.Count == 0) return;
            SoulRequiem.Core.Sfx.Play2D("ui_back");   // sound
            quietFocusUntil = Time.unscaledTime + 0.25f;
            var top = stack[stack.Count - 1];
            stack.RemoveAt(stack.Count - 1);
            top.RemoveFromHierarchy();
            if (stack.Count > 0)
            {
                var prev = stack[stack.Count - 1];
                prev.style.display = DisplayStyle.Flex;
                FocusFirst(prev);
                return;
            }
            layer.style.display = DisplayStyle.None;
            Time.timeScale = savedScale;
            GameInput.Blocked = false;
            GameInput.MenuOpen = false;
        }

        private void CloseAll() { while (stack.Count > 0) Pop(); }

        private static void FocusFirst(VisualElement panel)
        {
            panel.schedule.Execute(() =>
            {
                var f = panel.Query<VisualElement>().Where(v => v.focusable && v.enabledInHierarchy && v.resolvedStyle.display != DisplayStyle.None).First();
                f?.Focus();
            });
        }

        // ------------------------------------------------------------------ khung chung
        public static VisualElement Panel(string title, string subtitle, float width)
        {
            var p = new VisualElement();
            p.AddToClassList("sr-panel");
            RequiemUI.Slice(p, "panel_stone", 16, 18, 16, 16);
            p.style.width = width;
            var t = RequiemUI.Text(title, "sr-title sr-panel-title", p);
            if (!string.IsNullOrEmpty(subtitle)) RequiemUI.Text(subtitle, "sr-panel-sub", p);
            RequiemUI.Deco("hint_line", "sr-rule", p);
            return p;
        }

        private static VisualElement Center(VisualElement panel)
        {
            var c = new VisualElement();
            c.AddToClassList("sr-fill"); c.AddToClassList("sr-center-box");
            c.Add(panel);
            return c;
        }

        /// <summary>Open the Mantra panel without a rest point (key L / Mantras button). Mantras sealed -> message only.</summary>
        private bool OpenMantras()
        {
            var st = hp != null ? PlayerStatus.Of(hp) : null;
            if (st != null && st.MantrasBound) { Hints.Show(st.MantraSealText, 2.5f); return false; }
            Push(BuildRest(false));
            return true;
        }

        // ----------------------------------------------------------------- HUD buttons (mouse) open / close the Inventory and Mantra panel
        /// <summary>Number of times the inventory was opened (the HUD clears the "new item" dot when it changes).</summary>
        public static int InventoryOpens { get; private set; }

        public static void ToggleInventory() { if (instance != null) instance.ToggleInv(); }
        public static void ToggleMantras() { if (instance != null) instance.ToggleMantra(); }

        private bool CanOpenFromHud => !(hp != null && hp.IsDead) && !MapMechanics.ConsoleActive && !MapMechanics.PanelOpen
                                       && !CutsceneState.Active && !TutorialPrompt.Active && !DialogueBox.Active;

        private void ToggleInv()
        {
            if (stack.Count > 0 && inventoryPanel != null && stack[stack.Count - 1] == inventoryPanel) { Pop(); return; }
            if (stack.Count == 0 && !GameInput.Blocked && CanOpenFromHud) Push(BuildInventory());
        }

        private void ToggleMantra()
        {
            if (stack.Count > 0 && restPanel != null && stack[stack.Count - 1] == restPanel) { Pop(); return; }
            if (stack.Count == 0 && !GameInput.Blocked && CanOpenFromHud) OpenMantras();
        }

        // ------------------------------------------------------------------ DEATH
        private void BuildDeath(VisualElement fx)
        {
            death = new VisualElement { pickingMode = PickingMode.Ignore };
            death.AddToClassList("sr-fill"); death.AddToClassList("sr-death");
            deathTitle = RequiemUI.Text("YOU HAVE FALLEN", "sr-title sr-death-title", death);
            deathSub = RequiemUI.Text("The bells toll. You rise again at the last shrine...", "sr-death-sub", death);
            death.style.opacity = 0f;
            death.style.display = DisplayStyle.None;
            fx.Add(death);
        }

        private void UpdateDeath(bool dead)
        {
            if (death == null) return;
            string sub = $"The bells toll. You rise again at the last {ShrineMarker.Noun}...";
            if (deathSub.text != sub) deathSub.text = sub;
            deathT = Mathf.MoveTowards(deathT, dead ? 1f : 0f, Time.unscaledDeltaTime * (dead ? 1.4f : 3f));
            death.style.display = deathT > 0.001f ? DisplayStyle.Flex : DisplayStyle.None;
            death.style.opacity = deathT;
            deathTitle.style.scale = new Scale(new Vector3(1f + 0.06f * (1f - deathT), 1f + 0.06f * (1f - deathT), 1f));
            if (dead && stack.Count > 0) CloseAll();
        }
    }
}
