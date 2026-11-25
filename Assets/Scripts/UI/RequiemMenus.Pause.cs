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
    /// PAUSE MENU (Esc): Resume / Settings / Quit to Title (asks for confirmation before leaving).
    /// </summary>
    public partial class RequiemMenus
    {
        // ------------------------------------------------------------------ PAUSE
        // PC-only layout: no Inventory / Mantras entries (keys B / L + HUD buttons exist) and no Quit Game (quit from the Title).
        // 3 text entries + a flame like the title screen, a left column on a dark strip, the game still visible behind.
        // Also shows: chapter + region, current quest, a reminder that saving happens at campfires. Quit to Title asks again.
        private VisualElement BuildPause()
        {
            var root = new VisualElement();
            root.AddToClassList("sr-fill"); root.AddToClassList("sr-pz");
            root.RegisterCallback<AttachToPanelEvent>(_ => { if (dim != null) dim.style.opacity = 0.5f; });   // lighter dark overlay: the game stays visible behind
            root.RegisterCallback<DetachFromPanelEvent>(_ => { if (dim != null) dim.style.opacity = 1f; });
            RequiemUI.Deco("title_shade", "sr-pz-shade", root);
            var col = new VisualElement(); col.AddToClassList("sr-pz-col"); root.Add(col);
            var dir = MapEncounterDirector.Instance;
            var ch = ChapterBook.Get(ChapterBook.Of(SceneManager.GetActiveScene().name));
            if (ch != null) RequiemUI.Text($"CHAPTER {ch.roman}", "sr-pz-kicker", col);
            RequiemUI.Text("PAUSED", "sr-title sr-pz-title", col);
            string zone = dir != null ? RequiemUI.Clean(dir.CurrentZoneTitle ?? "") : "";
            if (zone.Length > 0) RequiemUI.Text(zone, "sr-pz-zone", col);
            RequiemUI.Deco("hint_line", "sr-rule sr-pz-rule", col);
            var menu = new VisualElement(); menu.AddToClassList("sr-pz-menu"); col.Add(menu);
            menu.Add(new TitleMenuItem("Resume", Pop) { AutoTick = true });
            menu.Add(new TitleMenuItem("Settings", () => Push(Center(BuildSettings(Pop)))) { AutoTick = true });
            if (Application.CanStreamedLevelBeLoaded("Title"))
                menu.Add(new TitleMenuItem("Quit to Title", () => Push(BuildQuitConfirm())) { AutoTick = true });

            var q = QuestTracker.Instance;
            if (q != null && q.HasQuest)
            {
                var obj = new VisualElement(); obj.AddToClassList("sr-pz-obj"); col.Add(obj);
                RequiemUI.Text(q.IsSide ? "SIDE QUEST" : "CURRENT OBJECTIVE", "sr-pz-kicker", obj);
                RequiemUI.Text(q.AllDone ? "The way onward is open." : RequiemUI.Clean(q.Title), "sr-pz-objtitle", obj);
                if (!q.AllDone && q.HasTarget && !string.IsNullOrEmpty(q.Label)) RequiemUI.Text(RequiemUI.Clean(q.Label), "sr-pz-objsub", obj);
            }
            RequiemUI.Text("Progress is saved when you rest by a fire.", "sr-pz-foot", col);
            return root;
        }

        /// <summary>Confirm before returning to the title screen (everything since the last rest is lost). "Stay" is selected by default.</summary>
        private VisualElement BuildQuitConfirm()
        {
            var c = new VisualElement();
            c.AddToClassList("sr-fill"); c.AddToClassList("sr-center-box"); c.AddToClassList("sr-pz-ask-bg");
            var box = new VisualElement(); box.AddToClassList("sr-pz-ask"); c.Add(box);
            RequiemUI.Text("Return to title?", "sr-title sr-pz-ask-title", box);
            RequiemUI.Text("Anything since your last rest will be lost.", "sr-pz-ask-sub", box);
            var row = new VisualElement(); row.AddToClassList("sr-pz-ask-row"); box.Add(row);
            row.Add(new TitleMenuItem("Stay", Pop) { AutoTick = true });
            row.Add(new TitleMenuItem("Return", () => { CloseAll(); SceneManager.LoadScene("Title"); }) { AutoTick = true });
            return c;
        }

        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
