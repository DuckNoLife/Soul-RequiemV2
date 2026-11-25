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
    /// SETTINGS (shared by Pause and Title): section list on the left, rows on the right, explanation line at the bottom.
    /// Adding a setting: a field in Core/GameSettings + one Row(...) line in the matching tab below.
    /// </summary>
    public partial class RequiemMenus
    {
        // ------------------------------------------------------------------ SETTINGS (shared with the Title)
        // PC-only layout. Left: vertical section list (text, gold mark on the open section);
        // right: rows (name left, control right); hovering / selecting a row shows its explanation at the bottom; footer: small "Restore defaults" text
        // (hard to click by mistake) + [Esc] Back. Shared by the Title (settingsHost) and Pause.
        private static Label setDesc;

        public static VisualElement BuildSettings(Action back)
        {
            var p = new VisualElement(); p.AddToClassList("sr-set");
            RequiemUI.Text("SETTINGS", "sr-title sr-set-title", p);
            var hr = new VisualElement(); hr.AddToClassList("sr-set-hr"); p.Add(hr);
            var body = new VisualElement(); body.AddToClassList("sr-set-body"); p.Add(body);
            var cats = new VisualElement(); cats.AddToClassList("sr-set-cats"); body.Add(cats);
            var right = new VisualElement(); right.AddToClassList("sr-set-right"); body.Add(right);
            var rows = new VisualElement(); rows.AddToClassList("sr-set-rows"); right.Add(rows);
            setDesc = RequiemUI.Text("", "sr-set-desc", right);
            string[] names = { "Audio", "Display", "Gameplay", "Controls" };
            var catEls = new List<VisualElement>();
            Action<int> show = null;
            show = i =>
            {
                rows.Clear();
                setDesc.text = "";
                for (int k = 0; k < catEls.Count; k++) catEls[k].EnableInClassList("sr-set-cat-on", k == i);
                if (i == 0) AudioTab(rows);
                else if (i == 1) DisplayTab(rows);
                else if (i == 2) GameplayTab(rows);
                else ControlsTab(rows);
            };
            for (int i = 0; i < names.Length; i++)
            {
                int idx = i;
                var c = new VisualElement(); c.AddToClassList("sr-set-cat");
                c.focusable = true; c.tabIndex = 0;
                var bar = new VisualElement { pickingMode = PickingMode.Ignore }; bar.AddToClassList("sr-set-cat-bar"); c.Add(bar);
                RequiemUI.Text(names[i], "sr-title sr-set-cat-label", c);
                c.AddManipulator(new Clickable(() => show(idx)));
                c.RegisterCallback<NavigationSubmitEvent>(_ => show(idx));
                c.RegisterCallback<PointerEnterEvent>(_ => c.Focus());
                cats.Add(c); catEls.Add(c);
            }
            show(0);
            var foot = new VisualElement(); foot.AddToClassList("sr-set-foot"); p.Add(foot);
            var reset = new VisualElement(); reset.AddToClassList("sr-set-link"); reset.focusable = true; reset.tabIndex = 0;
            RequiemUI.Text("Restore defaults", "sr-set-link-label", reset);
            reset.AddManipulator(new Clickable(() => { GameSettings.ResetDefaults(); show(0); }));
            reset.RegisterCallback<NavigationSubmitEvent>(_ => { GameSettings.ResetDefaults(); show(0); });
            foot.Add(reset);
            var bk = new VisualElement(); bk.AddToClassList("sr-set-back"); bk.focusable = true; bk.tabIndex = 0;
            var cap = new VisualElement { pickingMode = PickingMode.Ignore }; cap.AddToClassList("sr-set-cap");
            var kt = RequiemUI.Tex("keycap"); if (kt != null) cap.style.backgroundImage = new StyleBackground(kt);
            RequiemUI.Text(GameInput.Label(GameAction.Pause), "sr-set-cap-label", cap);
            bk.Add(cap);
            RequiemUI.Text("Back", "sr-set-link-label", bk);
            bk.AddManipulator(new Clickable(() => back?.Invoke()));
            bk.RegisterCallback<NavigationSubmitEvent>(_ => back?.Invoke());
            foot.Add(bk);
            return p;
        }

        private static void Changed() { GameSettings.Save(); GameSettings.Apply(); }

        private static void AudioTab(VisualElement b)
        {
            Row(b, "Master volume", new SRSlider(GameSettings.Master, v => { GameSettings.Master = v; Changed(); }), "Overall volume of the game.");
            Row(b, "Music", new SRSlider(GameSettings.Music, v => { GameSettings.Music = v; Changed(); }), "Volume of the background music.");
            Row(b, "Sound effects", new SRSlider(GameSettings.Sfx, v => { GameSettings.Sfx = v; Changed(); }), "Volume of combat, footsteps and interface sounds.");
        }

        private static void DisplayTab(VisualElement b)
        {
            var res = Screen.resolutions.Select(r => $"{r.width} x {r.height}").Distinct().ToList();
            var resIdx = new List<int>();
            var all = Screen.resolutions;
            foreach (var label in res) resIdx.Add(Array.FindLastIndex(all, r => $"{r.width} x {r.height}" == label));
            if (res.Count == 0) { res.Add($"{Screen.width} x {Screen.height}"); resIdx.Add(-1); }
            int cur = Mathf.Max(0, resIdx.IndexOf(GameSettings.ResolutionIndex));
            if (GameSettings.ResolutionIndex < 0) cur = Mathf.Max(0, res.IndexOf($"{Screen.currentResolution.width} x {Screen.currentResolution.height}"));
            Row(b, "Resolution", new SRStepper(res.ToArray(), cur, i => { GameSettings.ResolutionIndex = resIdx[i]; Changed(); }), "Screen resolution. Applies immediately.");
            Row(b, "Fullscreen", new SRToggle(GameSettings.Fullscreen, v => { GameSettings.Fullscreen = v; Changed(); }), "Play in fullscreen or in a window.");
            Row(b, "V-Sync", new SRToggle(GameSettings.VSync, v => { GameSettings.VSync = v; Changed(); }), "Match the frame rate to your monitor to stop screen tearing.");
            float[] scales = { 0.8f, 0.9f, 1f, 1.1f, 1.25f };
            int si = Array.FindIndex(scales, x => Mathf.Abs(x - GameSettings.HudScale) < 0.01f);
            Row(b, "Interface size", new SRStepper(scales.Select(x => Mathf.RoundToInt(x * 100) + "%").ToArray(), si < 0 ? 2 : si,
                i => { GameSettings.HudScale = scales[i]; Changed(); }), "Size of the HUD and menus.");
        }

        private static void GameplayTab(VisualElement b)
        {
            Row(b, "Enemy health numbers", new SRToggle(GameSettings.EnemyHpNumbers, v => { GameSettings.EnemyHpNumbers = v; Changed(); }), "Show numbers on enemy health bars.");
            Row(b, "Screen shake", new SRSlider(GameSettings.ScreenShake, v => { GameSettings.ScreenShake = v; Changed(); }), "Strength of the camera shake on heavy hits.");
            Row(b, "Low health warning", new SRToggle(GameSettings.LowHpVignette, v => { GameSettings.LowHpVignette = v; Changed(); }), "Red edges around the screen when your health is low.");
            Row(b, "Reduce flashing", new SRToggle(GameSettings.ReduceFlashing, v => { GameSettings.ReduceFlashing = v; Changed(); }), "Tone down flashes and blinking effects.");
        }

        private static void ControlsTab(VisualElement b)
        {
            foreach (GameAction a in Enum.GetValues(typeof(GameAction)))
            {
                var k = new VisualElement(); k.AddToClassList("sr-keycap-big");
                var kt = RequiemUI.Tex("keycap");
                if (kt != null) k.style.backgroundImage = new StyleBackground(kt);
                RequiemUI.Text(GameInput.Label(a), "sr-key", k);
                var r = Row(b, GameInput.ActionName(a), k);
                r.AddToClassList("sr-row-compact");
            }
            RequiemUI.Text("Move: W A S D or arrow keys     Attack: also the left mouse button", "sr-set-note", b);
        }
    }
}
