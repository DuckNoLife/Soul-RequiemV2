using UnityEngine;
using UnityEngine.InputSystem;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// console - pipe room control lever: needs flags `requires` (missing -> listed by `needs` "flag=description|..." or `locked_text`).
    /// Press T -> the camera pans to pipe grid `grid` and character control locks; left/right mouse rotates pipes, R resets, Esc / Q exits.
    /// `prompt` = instruction line while operating.
    /// </summary>
    public sealed class ConsoleMechanism : Mechanism
    {
        private bool announced;
        private PipeGridMechanism grid;
        // glow ring + sparkle under the lever (`marker_item` = lever image name) until flag `marker_off` is set
        // (the 10x18 px lever blended into the pump machine and players did not see it was the puzzle switch)
        private GameObject marker;

        // while the crank is still in the inventory the lever must NOT already be on the machine. `fit_object` = lever image name:
        // hidden until Ren fits the crank (first T press with `requires` met) -> flag `fit_flag` (default <name>_fitted), saved per map.
        private string FitFlag => Str("fit_flag", Name + "_fitted");
        private bool Fitted => Str("fit_object", "").Length == 0 || Ctx.Flags.Has(FitFlag) || Ctx.Flags.HasAll(Str("marker_off", "-"));

        public override void Init()
        {
            string item = Str("marker_item", "");
            if (item.Length > 0) marker = NoteMarker.Create(Name, Rect, Ctx, item);
            OnFlagsChanged();
        }

        public override void OnFlagsChanged()
        {
            string fit = Str("fit_object", "");
            if (fit.Length > 0) Ctx.Show(fit, Fitted);
            if (marker == null) return;
            string off = Str("marker_off", "");
            marker.SetActive(off.Length == 0 || !Ctx.Flags.HasAll(off));
        }

        public override void Animate()
        {
            if (marker != null && marker.activeSelf) NoteMarker.Animate(marker);
        }

        public override string DefaultVerb => "Use";

        public override void Tick(Vector2 playerPos, bool inside)
        {
            if (inside && !announced)
            {
                announced = true;
                Hints.Show(Ctx.Flags.HasAll(Str("requires", "")) ? $"The pump lever. Press {GameInput.Label(GameAction.Interact)} to take the controls."
                                                                 : LockedText(), 4f);
            }
            if (!inside) announced = false;
        }

        public override void Use()
        {
            if (Done) return;
            if (!Ctx.Flags.HasAll(Str("requires", ""))) { Hints.Show(LockedText(), 4f); return; }
            if (!Fitted)
            {
                Ctx.Flags.Set(FitFlag);                                       // OnFlagsChanged shows the lever
                string item = Str("fit_item", "");
                if (item.Length > 0) Ctx.Flags.Set(KeyRing.UsedFlag(item));
                SfxHooks.MechanismEvent(this, "fit", "fit_part"); // sound
                Hints.Show(Str("fit_text", "You fit the crank onto the console. It locks into place."), 2.5f);
                CameraFollow.KickMain(Vector2.down, 0.02f, 0.12f);
            }
            Enter();
        }

        private void Enter()
        {
            grid = null;
            foreach (var m in Ctx.All) if (m is PipeGridMechanism g && g.Name == Str("grid", "")) { grid = g; break; }
            if (grid == null) { Debug.LogWarning("[MapMechanics] Pipe grid not found " + Str("grid", "")); return; }
            if (grid.Done) { Hints.Show("The flow is already turned to the drain.", 2.5f); return; }
            Ctx.ActiveConsole = this;
            grid.BeginControl(this);
            Ctx.SetPlayerControl(false);
            // view frame: the whole grid + supply pipe above + drain below; centre nudged down so the instruction line does not cover the last pipe row
            Vector3 focus = (Vector3)grid.Rect.center + new Vector3(0f, -0.22f, 0f);
            var cam = UnityEngine.Camera.main;
            float aspect = cam != null ? cam.aspect : 16f / 9f;
            float size = Mathf.Max(grid.Rect.height * 0.5f + 0.95f, grid.Rect.width * 0.5f / aspect + 0.5f);
            if (Ctx.Camera != null) Ctx.Camera.Focus(focus, size);
            Hints.Show(Str("prompt", "Click a pipe to turn it. Esc to step back."), 9999f);
        }

        /// <summary>Leave the console: return the camera and character control.</summary>
        internal void Exit()
        {
            if (Ctx.ActiveConsole == this) Ctx.ActiveConsole = null;
            grid = null;
            if (Ctx.Camera != null) Ctx.Camera.Release();
            Ctx.SetPlayerControl(true);
            Hints.Clear();
        }

        /// <summary>Called every frame while operating (instead of every mechanism's Tick).</summary>
        internal void ControlUpdate()
        {
            var kb = Keyboard.current;
            if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.qKey.wasPressedThisFrame)) { Exit(); return; }
            if (Ctx.PlayerHealth != null && Ctx.PlayerHealth.IsDead) { Exit(); return; }
            if (grid != null) grid.ControlUpdate(kb, Mouse.current);
        }

        /// <summary>Conditions not met: list exactly what is STILL MISSING per `needs`; without `needs` use `locked_text`.</summary>
        private string LockedText()
        {
            string needs = Str("needs", "");
            if (string.IsNullOrEmpty(needs)) return Str("locked_text", "The lever will not move.");
            var missing = new System.Collections.Generic.List<string>();
            foreach (var part in needs.Split('|'))
            {
                int eq = part.IndexOf('=');
                if (eq <= 0) continue;
                if (!Ctx.Flags.HasAll(part.Substring(0, eq))) missing.Add(part.Substring(eq + 1).Trim());
            }
            return missing.Count == 0 ? Str("locked_text", "The lever will not move.")
                                      : "The console will not wake. Still missing: " + string.Join("; ", missing) + ".";
        }
    }
}
