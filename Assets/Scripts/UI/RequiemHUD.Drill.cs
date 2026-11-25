using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using SoulRequiem.Core;
using SoulRequiem.Gameplay;

namespace SoulRequiem.UI
{
    /// <summary>
    /// HUD — DrillBoard PANEL (Drill Yard, Absalom, The Registrar): station / phase name, objective, life pips, shouted lines.
    /// Reads: DrillBoard (written by DrillYardMechanism, AbsalomHallMechanism, RegistrarSkills).
    /// </summary>
    public partial class RequiemHUD
    {
        // ===================================================================================================== Drill Yard panel
        /// <summary>COMPACT panel 640 x 64 against the top edge (~6% of the screen height):
        /// row 1: "STN 2/3  LANTERN RUN" | OBJECTIVE ("LANTERNS 1 / 3") | 3 LIFE cells | clock;
        /// row 2: one short TASK line; row 3: progress / tracking bar (icons at both ends + a running icon) + a thin time bar.
        /// Event lines float right under the panel; results big in the centre (briefly); Final Exam questions at the BOTTOM edge (not covering the blackboard / Burlap).</summary>
        private const float DrillW = 640f, DrillBarX = 60f, DrillBarW = 520f;

        private void BuildDrill()
        {
            drillRoot = new VisualElement { pickingMode = PickingMode.Ignore };
            RequiemUI.Abs(drillRoot, 0, 8, DrillW, 96);
            hud.Add(drillRoot);
            Box(drillRoot, 0, 0, DrillW, 64, new Color(0.05f, 0.03f, 0.08f, 0.82f));
            var gold = new Color(0.75f, 0.56f, 0.25f, 0.9f);
            Box(drillRoot, 0, 0, DrillW, 1, gold); Box(drillRoot, 0, 63, DrillW, 1, gold); Box(drillRoot, 0, 0, 1, 64, gold); Box(drillRoot, DrillW - 1, 0, 1, 64, gold);
            drillTitle = RequiemUI.Text("", "sr-tiny", drillRoot);
            RequiemUI.Abs(drillTitle, 12, 3, 210, 24); drillTitle.style.unityTextAlign = TextAnchor.MiddleLeft;
            drillTitle.style.color = new Color(1f, 0.85f, 0.5f); drillTitle.style.fontSize = 14;
            drillGoal = RequiemUI.Text("", "sr-tiny", drillRoot);
            RequiemUI.Abs(drillGoal, 222, 2, 214, 26); drillGoal.style.unityTextAlign = TextAnchor.MiddleCenter;
            drillGoal.style.color = Color.white; drillGoal.style.fontSize = 19;
            var ml = drillPipLabel = RequiemUI.Text("LIVES", "sr-tiny", drillRoot);   // label follows DrillBoard.PipLabel
            RequiemUI.Abs(ml, 440, 3, 48, 24); ml.style.color = new Color(0.75f, 0.72f, 0.8f); ml.style.fontSize = 12; ml.style.unityTextAlign = TextAnchor.MiddleLeft;
            drillPips.Clear();
            for (int i = 0; i < 5; i++)
            {
                var pip = Box(drillRoot, 486 + i * 20, 8, 15, 15, new Color(0.15f, 0.1f, 0.12f));
                drillPips.Add(Box(pip, 2, 2, 11, 11, new Color(1f, 0.78f, 0.35f)));
            }
            drillClock = RequiemUI.Text("", "sr-tiny", drillRoot);
            RequiemUI.Abs(drillClock, 552, 2, 78, 26); drillClock.style.unityTextAlign = TextAnchor.MiddleRight;
            drillClock.style.color = new Color(0.7f, 0.85f, 1f); drillClock.style.fontSize = 19;
            drillObjective = RequiemUI.Text("", "sr-tiny", drillRoot);
            RequiemUI.Abs(drillObjective, 12, 28, DrillW - 24, 18); drillObjective.style.color = new Color(1f, 0.9f, 0.55f);
            drillObjective.style.unityTextAlign = TextAnchor.MiddleCenter; drillObjective.style.fontSize = 13;
            drillObjective.style.whiteSpace = WhiteSpace.NoWrap; drillObjective.style.overflow = Overflow.Hidden;
            // progress / tracking bar
            Box(drillRoot, DrillBarX - 1, 49, DrillBarW + 2, 8, new Color(0.45f, 0.33f, 0.16f, 0.9f));
            Box(drillRoot, DrillBarX, 50, DrillBarW, 6, new Color(0.11f, 0.06f, 0.1f));
            drillFill = RequiemUI.Img("bar_fill", DrillBarX, 50, DrillBarW, 6, drillRoot);
            drillTrackL = new VisualElement { pickingMode = PickingMode.Ignore }; drillRoot.Add(drillTrackL);
            drillTrackR = new VisualElement { pickingMode = PickingMode.Ignore }; drillRoot.Add(drillTrackR);
            drillTrackM = new VisualElement { pickingMode = PickingMode.Ignore }; drillRoot.Add(drillTrackM);
            drillTime = Box(drillRoot, DrillBarX, 58, DrillBarW, 2, new Color(0.6f, 0.75f, 1f, 0.8f));
            drillPop = RequiemUI.Text("", "sr-small", drillRoot);
            RequiemUI.Abs(drillPop, 0, 68, DrillW, 24); drillPop.style.unityTextAlign = TextAnchor.MiddleCenter; drillPop.style.fontSize = 17;
            drillRoot.style.display = DisplayStyle.None;
            drillResult = RequiemUI.Text("", "sr-title", hud);
            drillResult.style.position = Position.Absolute; drillResult.style.left = 0; drillResult.style.right = 0; drillResult.style.top = Length.Percent(40);
            drillResult.style.unityTextAlign = TextAnchor.MiddleCenter; drillResult.style.fontSize = 46;
            drillResult.style.display = DisplayStyle.None;
            drillCommand = RequiemUI.Text("", "sr-title", hud);
            drillCommand.style.position = Position.Absolute; drillCommand.style.left = 0; drillCommand.style.right = 0; drillCommand.style.top = Length.Percent(84);
            drillCommand.style.unityTextAlign = TextAnchor.MiddleCenter; drillCommand.style.fontSize = 34;
            drillCommand.style.color = new Color(1f, 0.93f, 0.7f);
            drillCommand.style.display = DisplayStyle.None;
        }

        /// <summary>Sprite icon (Resources) `h` px tall, feet at (cx, bottom) in the panel; empty = hidden.</summary>
        private void DrillIcon(VisualElement v, string path, float cx, float bottom, float h, float alpha = 1f)
        {
            var sp = DrillSprite(path);
            if (sp == null) { v.style.display = DisplayStyle.None; return; }
            v.style.display = DisplayStyle.Flex;
            if (v.userData as Sprite != sp) { v.style.backgroundImage = new StyleBackground(sp); v.userData = sp; }
            float w = h * sp.rect.width / Mathf.Max(1f, sp.rect.height);
            RequiemUI.Abs(v, Mathf.Round(cx - w * 0.5f), Mathf.Round(bottom - h), Mathf.Round(w), Mathf.Round(h));
            v.style.opacity = alpha;
        }

        private readonly Dictionary<string, Sprite> drillSprites = new Dictionary<string, Sprite>();

        private Sprite DrillSprite(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (drillSprites.TryGetValue(path, out var s)) return s;
            var all = Resources.LoadAll<Sprite>(path);
            s = all.Length > 0 ? all[0] : null;
            drillSprites[path] = s;
            return s;
        }

        private void UpdateDrill(float dt)
        {
            bool on = DrillBoard.Active;
            drillRoot.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            float since = Time.time - DrillBoard.ResultTime;
            bool res = since < 3.2f && DrillBoard.Result.Length > 0;
            drillResult.style.display = res ? DisplayStyle.Flex : DisplayStyle.None;
            if (res)
            {
                if (drillResult.text != DrillBoard.Result) drillResult.text = DrillBoard.Result;
                float a = since < 0.2f ? since / 0.2f : since > 2.4f ? Mathf.Clamp01((3.2f - since) / 0.8f) : 1f;
                var c = DrillBoard.ResultColor; drillResult.style.color = new Color(c.r, c.g, c.b, a);
                float sc = since < 0.25f ? 1.3f - 0.3f * since / 0.25f : 1f;
                drillResult.style.scale = new Scale(new Vector3(sc, sc, 1f));
            }
            // Final Exam question — big text at the BOTTOM edge, pops up when a new question appears
            bool cmd = on && !string.IsNullOrEmpty(DrillBoard.Command);
            drillCommand.style.display = cmd ? DisplayStyle.Flex : DisplayStyle.None;
            if (cmd)
            {
                if (drillCommand.text != DrillBoard.Command) drillCommand.text = DrillBoard.Command;
                float cs = Time.time - DrillBoard.CommandTime;
                float k = cs < 0.18f ? 1.3f - 0.3f * cs / 0.18f : 1f;
                drillCommand.style.scale = new Scale(new Vector3(k, k, 1f));
            }
            if (!on) return;
            drillRoot.style.left = Mathf.Round((PanelW - DrillW) * 0.5f);
            string title = DrillBoard.StationLabel + " " + DrillBoard.Station + "/" + DrillBoard.Stations + "  " + DrillBoard.Title;
            if (drillPipLabel.text != DrillBoard.PipLabel) drillPipLabel.text = DrillBoard.PipLabel;
            if (drillTitle.text != title) drillTitle.text = title;
            if (drillGoal.text != DrillBoard.Goal) drillGoal.text = DrillBoard.Goal;
            string clock = DrillBoard.TimeFrac >= 0f ? DrillBoard.TimeText : "";
            if (drillClock.text != clock) drillClock.text = clock;
            drillClock.style.color = DrillBoard.TimeFrac >= 0f && DrillBoard.TimeFrac < 0.2f
                ? new Color(1f, 0.45f, 0.4f, 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 10f)) : new Color(0.7f, 0.85f, 1f);
            // Burlap shouting during play -> replaces the task line for a moment (orange text), never opens the bottom dialogue box over the dodge space
            bool shout = Time.time < DrillBoard.ShoutUntil && !string.IsNullOrEmpty(DrillBoard.Shout);
            string obj = shout ? DrillBoard.ShoutName + ": " + DrillBoard.Shout : string.IsNullOrEmpty(DrillBoard.Objective) ? "" : "> " + DrillBoard.Objective;
            if (drillObjective.text != obj) drillObjective.text = obj;
            drillObjective.style.color = shout ? new Color(1f, 0.62f, 0.32f) : new Color(1f, 0.9f, 0.55f);
            // lives: remaining cells = gold (last one = red); a cell just lost = red shake, then dark
            float lost = Time.time - DrillBoard.MoraleLostAt;
            for (int i = 0; i < drillPips.Count; i++)
            {
                var p = drillPips[i];
                bool show = i < DrillBoard.MoraleMax;
                p.parent.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                if (!show) continue;
                bool full = i < DrillBoard.Morale;
                bool justLost = !full && i == DrillBoard.Morale && lost < 0.8f;
                p.style.backgroundColor = full ? (DrillBoard.Morale <= 1 ? new Color(1f, 0.45f, 0.35f) : new Color(1f, 0.78f, 0.35f))
                                         : justLost ? new Color(1f, 0.2f, 0.15f, 1f - lost / 0.8f) : new Color(0.25f, 0.16f, 0.18f);
                float sh = justLost ? Mathf.Sin(lost * 60f) * 3f * (1f - lost / 0.8f) : 0f;
                p.parent.style.translate = new Translate(sh, 0f);
            }
            // bar: with TrackFrac -> tracks distance (running icon); without -> objective progress
            bool track = DrillBoard.TrackFrac >= 0f;
            float frac = track ? DrillBoard.TrackFrac : DrillBoard.GoalFrac;
            drillFill.style.width = DrillBarW * Mathf.Clamp01(frac);
            drillFill.style.unityBackgroundImageTintColor = track ? new Color(0.95f, 0.72f, 0.3f)
                : frac >= 1f ? new Color(0.35f, 0.85f, 0.4f) : new Color(0.85f, 0.62f, 0.2f);
            if (track)
            {
                DrillIcon(drillTrackL, DrillBoard.TrackLeft, DrillBarX - 24f, 62f, 20f);
                DrillIcon(drillTrackR, DrillBoard.TrackRight, DrillBarX + DrillBarW + 24f, 62f, 22f);
                float bob = Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6f)) * 2f;
                DrillIcon(drillTrackM, DrillBoard.TrackMarker, DrillBarX + DrillBarW * Mathf.Clamp01(frac), 56f - bob, 16f);
            }
            else drillTrackL.style.display = drillTrackR.style.display = drillTrackM.style.display = DisplayStyle.None;
            drillTime.style.display = DrillBoard.TimeFrac >= 0f ? DisplayStyle.Flex : DisplayStyle.None;
            drillTime.style.width = DrillBarW * Mathf.Clamp01(DrillBoard.TimeFrac);
            float ps = Time.time - DrillBoard.PopTime;
            if (ps < 1.2f && DrillBoard.Pop.Length > 0)
            {
                if (drillPop.text != DrillBoard.Pop) drillPop.text = DrillBoard.Pop;
                var c = DrillBoard.PopColor; drillPop.style.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(1.4f - ps));
                drillPop.style.top = 68 + 6f * ps;
                drillPop.style.display = DisplayStyle.Flex;
            }
            else drillPop.style.display = DisplayStyle.None;
        }
    }
}
