using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using SoulRequiem.Core;
using SoulRequiem.Gameplay;

namespace SoulRequiem.UI
{
    /// <summary>
    /// HUD — CUTSCENES: dialogue box (DialogueBox), subtitles, choice cards (ChoiceCard), tutorial pause screen (TutorialPrompt),
    /// film bars, hold-SPACE skip button.
    /// </summary>
    public partial class RequiemHUD
    {
        // ===================================================================================================== choice cards
        private const float ChoiceW = 400f, ChoiceH = 300f, ChoiceGap = 48f;

        private void BuildChoice()
        {
            choiceRoot = new VisualElement { pickingMode = PickingMode.Ignore };
            choiceRoot.style.position = Position.Absolute;
            choiceRoot.style.left = 0; choiceRoot.style.right = 0; choiceRoot.style.top = 0; choiceRoot.style.bottom = 0;
            hud.Add(choiceRoot);
            choiceShade = Fill(choiceRoot, new Color(0.02f, 0f, 0.02f, 0.62f));
            choiceTitle = RequiemUI.Text("", "sr-title", choiceRoot);
            choiceTitle.style.position = Position.Absolute;
            choiceTitle.style.fontSize = 46; choiceTitle.style.letterSpacing = 8;
            choiceTitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            choiceTitle.style.color = new Color(0.95f, 0.9f, 0.85f);
            choiceTitle.style.unityTextOutlineWidth = 2f; choiceTitle.style.unityTextOutlineColor = new Color(0.05f, 0.02f, 0.04f);
            choiceSub = RequiemUI.Text("", "sr-small", choiceRoot);
            choiceSub.style.position = Position.Absolute;
            choiceSub.style.fontSize = 19; choiceSub.style.unityTextAlign = TextAnchor.MiddleCenter;
            choiceSub.style.color = new Color(0.85f, 0.8f, 0.8f, 0.9f);
            choiceSub.style.whiteSpace = WhiteSpace.Normal;
            choiceFoot = RequiemUI.Text("", "sr-small", choiceRoot);
            choiceFoot.style.position = Position.Absolute;
            choiceFoot.style.fontSize = 18; choiceFoot.style.unityTextAlign = TextAnchor.MiddleCenter;
            choiceFoot.style.color = new Color(0.9f, 0.85f, 0.7f);
            choiceRoot.style.display = DisplayStyle.None;
        }

        /// <summary>Rebuild the cards every time ChoiceCard opens (number of choices / gain-loss lines change).</summary>
        private void RebuildChoice()
        {
            choiceBuiltAt = ChoiceCard.OpenedAt;
            foreach (var c in choiceCards) c.RemoveFromHierarchy();
            choiceCards.Clear(); choiceNames.Clear();
            var opts = ChoiceCard.Options;
            for (int i = 0; i < opts.Length; i++)
            {
                int idx = i;
                var o = opts[i];
                var card = new VisualElement { pickingMode = PickingMode.Position };
                card.style.position = Position.Absolute;
                card.style.width = ChoiceW; card.style.height = ChoiceH;
                card.style.backgroundColor = new Color(0.06f, 0.035f, 0.06f, 0.94f);
                card.style.borderLeftWidth = card.style.borderRightWidth = card.style.borderTopWidth = card.style.borderBottomWidth = 3;
                card.style.paddingLeft = card.style.paddingRight = 22; card.style.paddingTop = 18;
                card.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));
                card.RegisterCallback<PointerEnterEvent>(_ => ChoiceCard.Hover(idx));
                card.RegisterCallback<PointerDownEvent>(_ => ChoiceCard.Click(idx));
                var band = new VisualElement { pickingMode = PickingMode.Ignore };     // school-colour strip at the top of the card
                band.style.height = 4; band.style.marginBottom = 12; band.style.backgroundColor = o.color;
                card.Add(band);
                var name = RequiemUI.Text(o.title ?? "", "sr-title", card);
                name.style.fontSize = 34; name.style.letterSpacing = 4; name.style.color = o.color;
                name.style.unityTextOutlineWidth = 1.5f; name.style.unityTextOutlineColor = new Color(0.03f, 0.01f, 0.02f);
                choiceNames.Add(name);
                var tag = RequiemUI.Text(o.tagline ?? "", "sr-small", card);
                tag.style.fontSize = 17; tag.style.unityFontStyleAndWeight = FontStyle.Italic; tag.style.marginBottom = 14;
                tag.style.color = new Color(0.85f, 0.82f, 0.8f); tag.style.whiteSpace = WhiteSpace.Normal;
                foreach (var g in o.gains ?? new string[0]) ChoiceLine(card, "+  " + g, new Color(0.72f, 1f, 0.72f));
                foreach (var l in o.losses ?? new string[0]) ChoiceLine(card, "−  " + l, new Color(1f, 0.55f, 0.55f));
                choiceRoot.Add(card);
                choiceCards.Add(card);
            }
            choiceFoot.BringToFront();
        }

        private static void ChoiceLine(VisualElement card, string text, Color c)
        {
            var l = RequiemUI.Text(text, "sr-small", card);
            l.style.fontSize = 17; l.style.color = c; l.style.marginBottom = 7;
            l.style.whiteSpace = WhiteSpace.Normal;
        }

        private void UpdateChoice(float dt)
        {
            bool on = ChoiceCard.Active;
            choiceK = Mathf.MoveTowards(choiceK, on ? 1f : 0f, dt * (on ? 4f : 2.5f));
            choiceRoot.style.display = choiceK > 0.001f ? DisplayStyle.Flex : DisplayStyle.None;
            if (choiceK <= 0.001f) return;
            if (!Mathf.Approximately(choiceBuiltAt, ChoiceCard.OpenedAt)) RebuildChoice();
            choiceRoot.style.opacity = choiceK;
            float w = PanelW, h = PanelH;
            int n = choiceCards.Count;
            float rowW = n * ChoiceW + Mathf.Max(0, n - 1) * ChoiceGap;
            float top = h * 0.5f - ChoiceH * 0.5f + 30f;
            RequiemUI.Abs(choiceTitle, 0, top - 150f, w, 60);
            RequiemUI.Abs(choiceSub, w * 0.2f, top - 86f, w * 0.6f, 56);
            choiceTitle.text = RequiemUI.Clean(ChoiceCard.Title);
            choiceSub.text = RequiemUI.Clean(ChoiceCard.Subtitle);
            float since = Time.unscaledTime - ChoiceCard.ChosenAt;
            for (int i = 0; i < n; i++)
            {
                var card = choiceCards[i];
                var o = ChoiceCard.Options.Length > i ? ChoiceCard.Options[i] : null;
                if (o == null) continue;
                bool sel = i == ChoiceCard.Selected;
                bool picked = !on && i == ChoiceCard.Chosen;
                card.style.left = w * 0.5f - rowW * 0.5f + i * (ChoiceW + ChoiceGap);
                card.style.top = top + (sel ? -6f : 0f);
                float pulse = sel ? 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 5f) : 0.35f;
                Color b = picked ? Color.white : Color.Lerp(new Color(0.3f, 0.26f, 0.3f), o.color, sel ? pulse : 0.15f);
                card.style.borderLeftColor = card.style.borderRightColor = card.style.borderTopColor = card.style.borderBottomColor = b;
                float sc = picked ? 1.08f - 0.04f * Mathf.Clamp01(since / 0.35f) : sel ? 1.04f : 0.96f;
                card.style.scale = new Scale(new Vector3(sc, sc, 1f));
                card.style.opacity = !on && !picked ? 0.25f : sel || picked ? 1f : 0.62f;
                if (i < choiceNames.Count) choiceNames[i].text = (sel && on ? "▶ " : "") + (o.title ?? "");
            }
            RequiemUI.Abs(choiceFoot, 0, top + ChoiceH + 34f, w, 30);
            choiceFoot.text = ChoiceCard.CanConfirm || !on
                ? $"[A] [D]  choose        [{GameInput.Label(GameAction.Interact)}]  confirm        this cannot be undone"
                : "...";
        }

        private static float EaseOut(float u) { u = Mathf.Clamp01(u); return 1f - (1f - u) * (1f - u) * (1f - u); }

        /// <summary>Tutorial pause screen — dark overlay, large action name, blinking gold-framed key cap, explanation line.</summary>
        private void BuildTutorialPrompt()
        {
            promptRoot = new VisualElement { pickingMode = PickingMode.Ignore };
            promptRoot.style.position = Position.Absolute;
            promptRoot.style.left = 0; promptRoot.style.right = 0; promptRoot.style.top = 0; promptRoot.style.bottom = 0;
            promptRoot.style.backgroundColor = new Color(RequiemUI.Ink.r, RequiemUI.Ink.g, RequiemUI.Ink.b, 0.42f);
            promptRoot.style.alignItems = Align.Center;
            promptRoot.style.justifyContent = Justify.FlexEnd;
            promptRoot.style.paddingBottom = Length.Percent(14);
            hud.Add(promptRoot);

            promptTitle = RequiemUI.Text("", "sr-title", promptRoot);
            promptTitle.style.fontSize = 64; promptTitle.style.color = RequiemUI.Gold;
            promptTitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            promptTitle.style.letterSpacing = 10;

            promptKey = new VisualElement { pickingMode = PickingMode.Ignore };
            promptKey.style.marginTop = 14; promptKey.style.marginBottom = 14;
            promptKey.style.paddingLeft = 34; promptKey.style.paddingRight = 34; promptKey.style.paddingTop = 8; promptKey.style.paddingBottom = 10;
            promptKey.style.backgroundColor = new Color(0.1f, 0.06f, 0.1f, 0.92f);
            promptKey.style.borderTopWidth = 4f; promptKey.style.borderBottomWidth = 4f;
            promptKey.style.borderLeftWidth = 4f; promptKey.style.borderRightWidth = 4f;
            promptKey.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));
            promptRoot.Add(promptKey);
            promptKeyText = RequiemUI.Text("", "sr-title", promptKey);
            promptKeyText.style.fontSize = 54; promptKeyText.style.color = Color.white;
            promptKeyText.style.unityTextAlign = TextAnchor.MiddleCenter;

            promptSub = RequiemUI.Text("", "", promptRoot);
            promptSub.style.fontSize = 28; promptSub.style.color = RequiemUI.Bone;
            promptSub.style.unityTextAlign = TextAnchor.MiddleCenter;

            promptRoot.style.display = DisplayStyle.None;
        }

        private void UpdateTutorialPrompt()
        {
            bool on = TutorialPrompt.Active;
            promptRoot.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            if (!on) return;
            string ti = RequiemUI.Clean(TutorialPrompt.Title), k = RequiemUI.Clean(TutorialPrompt.Keys), sb = RequiemUI.Clean(TutorialPrompt.Sub);
            if (promptTitle.text != ti) promptTitle.text = ti;
            if (promptKeyText.text != k) promptKeyText.text = k;
            if (promptSub.text != sb) promptSub.text = sb;
            // blinks on real time (the game is paused): gold <-> white outline, key cap pulsing
            float u = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 7f);
            Color edge = Color.Lerp(RequiemUI.Gold, Color.white, u);
            promptKey.style.borderTopColor = edge; promptKey.style.borderBottomColor = edge;
            promptKey.style.borderLeftColor = edge; promptKey.style.borderRightColor = edge;
            float sc = 1f + 0.08f * u;
            promptKey.style.scale = new Scale(new Vector3(sc, sc, 1f));
            promptKeyText.style.color = Color.Lerp(Color.white, RequiemUI.Gold, u);
        }

        /// <summary>Cutscene running -> fade the HUD (except the hint strip, boss name card, tutorial screen) so it does not cover speech bubbles.</summary>
        /// <summary>2 black bars top / bottom (cinema style) slide in during cutscenes — a clear "watching a scene" signal, the HUD hides.</summary>
        private void BuildLetterbox()
        {
            lbTop = Fill(hud, Color.black); lbTop.style.bottom = StyleKeyword.Auto;
            lbBot = Fill(hud, Color.black); lbBot.style.top = StyleKeyword.Auto;
            BuildSkip();
        }

        /// <summary>Player skip button — bottom right, inside the lower black bar: gold-framed key cap + "Hold to skip",
        /// a thin gold line below fills while holding (CutsceneSkipper.Hold01). While skipping: "Skipping…".</summary>
        private void BuildSkip()
        {
            skipRoot = new VisualElement { pickingMode = PickingMode.Ignore };
            skipRoot.style.position = Position.Absolute;
            skipRoot.style.right = M; skipRoot.style.bottom = 22;
            skipRoot.style.flexDirection = FlexDirection.Column;
            skipRoot.style.alignItems = Align.FlexEnd;
            hud.Add(skipRoot);
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            skipRoot.Add(row);
            var cap = new VisualElement { pickingMode = PickingMode.Ignore };
            cap.style.paddingLeft = 10; cap.style.paddingRight = 10; cap.style.paddingTop = 2; cap.style.paddingBottom = 3;
            cap.style.marginRight = 10;
            cap.style.backgroundColor = new Color(0.1f, 0.06f, 0.1f, 0.9f);
            Color edge = new Color(RequiemUI.Gold.r, RequiemUI.Gold.g, RequiemUI.Gold.b, 0.9f);
            cap.style.borderTopWidth = 2; cap.style.borderBottomWidth = 2; cap.style.borderLeftWidth = 2; cap.style.borderRightWidth = 2;
            cap.style.borderTopColor = edge; cap.style.borderBottomColor = edge; cap.style.borderLeftColor = edge; cap.style.borderRightColor = edge;
            row.Add(cap);
            skipKey = RequiemUI.Text("SPACE", "sr-key", cap);
            skipKey.style.position = Position.Relative;
            skipKey.style.fontSize = 18; skipKey.style.color = Color.white;
            skipKey.style.unityTextAlign = TextAnchor.MiddleCenter;
            skipText = RequiemUI.Text("Hold to skip", "sr-small", row);
            skipText.style.position = Position.Relative;
            skipText.style.fontSize = 20; skipText.style.color = RequiemUI.Bone;
            var track = new VisualElement { pickingMode = PickingMode.Ignore };
            track.style.marginTop = 6; track.style.height = 3; track.style.width = 200;
            track.style.backgroundColor = new Color(1f, 1f, 1f, 0.14f);
            skipRoot.Add(track);
            skipFill = new VisualElement { pickingMode = PickingMode.Ignore };
            skipFill.style.position = Position.Absolute; skipFill.style.left = 0; skipFill.style.top = 0; skipFill.style.bottom = 0;
            skipFill.style.width = 0;
            skipFill.style.backgroundColor = RequiemUI.Gold;
            track.Add(skipFill);
            skipRoot.style.opacity = 0f;
            skipRoot.style.display = DisplayStyle.None;
        }

        private void UpdateSkip(float dt)
        {
            if (skipRoot == null) return;
            bool skipping = CutsceneSkip.Now && CutsceneSkipper.Cutscene;
            bool show = CutsceneSkipper.Offer || skipping;
            skipK = Mathf.MoveTowards(skipK, show ? 1f : 0f, dt * (show ? 3f : 6f));
            skipRoot.style.display = skipK > 0.01f ? DisplayStyle.Flex : DisplayStyle.None;
            if (skipK <= 0.01f) return;
            float h = skipping ? 1f : CutsceneSkipper.Hold01;
            skipRoot.style.opacity = skipK * (skipping ? 0.7f : h > 0.001f ? 1f : 0.6f);   // idle: dim, does not pull the eye away from the scene
            string key = CutsceneSkipper.PadConnected ? "SPACE / Y" : "SPACE";
            if (skipKey.text != key) skipKey.text = key;
            string t = skipping ? "Skipping..." : "Hold to skip";
            if (skipText.text != t) skipText.text = t;
            skipFill.style.width = Mathf.Round(200f * h);
        }

        private void UpdateCinematic(float dt)
        {
            cineK = Mathf.MoveTowards(cineK, CutsceneState.Active ? 0f : 1f, dt * 4f);
            foreach (var c in hud.Children())
            {
                if (c == hintRoot || c == cardRoot || c == promptRoot || c == dlgRoot || c == lbTop || c == lbBot || c == choiceRoot || c == skipRoot) continue;
                c.style.opacity = cineK;
            }
            if (waypoint != null) waypoint.style.opacity = cineK;
            if (edgePointer != null) edgePointer.style.opacity = cineK;
            if (iPrompt != null) iPrompt.style.opacity = cineK;
            foreach (var b in enemyBars) if (b != null && b.root != null) b.root.style.opacity = cineK;   // enemy HP bars never cover the scene
            float bar = Mathf.Round(PanelH * 0.09f * EaseOut(1f - cineK));
            lbTop.style.height = bar; lbBot.style.height = bar;
            lbTop.style.display = lbBot.style.display = bar > 0.5f ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>Dialogue box (DialogueBox) — bottom centre of the screen, dark fill with gold border, speaker name, typing text, blinking [T].</summary>
        private void BuildDialogue()
        {
            dlgRoot = new VisualElement { pickingMode = PickingMode.Ignore };
            dlgRoot.style.position = Position.Absolute;
            dlgRoot.style.bottom = M + 6;
            dlgRoot.style.minHeight = 150;
            dlgRoot.style.paddingLeft = 34; dlgRoot.style.paddingRight = 34; dlgRoot.style.paddingTop = 22; dlgRoot.style.paddingBottom = 34;
            dlgRoot.style.backgroundColor = new Color(RequiemUI.Ink.r, RequiemUI.Ink.g, RequiemUI.Ink.b, 0.93f);
            dlgRoot.style.borderTopWidth = 3; dlgRoot.style.borderBottomWidth = 3; dlgRoot.style.borderLeftWidth = 3; dlgRoot.style.borderRightWidth = 3;
            Color edge = new Color(RequiemUI.Gold.r, RequiemUI.Gold.g, RequiemUI.Gold.b, 0.85f);
            dlgRoot.style.borderTopColor = edge; dlgRoot.style.borderBottomColor = edge; dlgRoot.style.borderLeftColor = edge; dlgRoot.style.borderRightColor = edge;
            hud.Add(dlgRoot);

            dlgName = RequiemUI.Text("", "sr-title", dlgRoot);
            dlgName.style.fontSize = 30; dlgName.style.color = RequiemUI.Gold;
            dlgName.style.marginBottom = 8;

            dlgText = RequiemUI.Text("", "", dlgRoot);
            dlgText.style.fontSize = 29; dlgText.style.color = RequiemUI.Bone;
            dlgText.style.whiteSpace = WhiteSpace.Normal;
            dlgText.style.unityTextAlign = TextAnchor.UpperLeft;

            dlgNext = RequiemUI.Text("", "sr-small", dlgRoot);
            dlgNext.style.position = Position.Absolute;
            dlgNext.style.right = 18; dlgNext.style.bottom = 8;
            dlgNext.style.color = RequiemUI.Gold;

            dlgRoot.style.display = DisplayStyle.None;
        }

        private void UpdateDialogue()
        {
            bool on = DialogueBox.Active;
            bool sub = !on && Subtitles.Visible;             // short lines (formerly speech bubbles) -> same box, controls not blocked
            dlgRoot.style.display = on || sub ? DisplayStyle.Flex : DisplayStyle.None;
            if (sub) { UpdateSubtitle(); return; }
            dlgRoot.style.opacity = 1f;
            dlgName.style.color = RequiemUI.Gold;
            if (!on) return;
            float w = Mathf.Min(1040f, PanelW - 2f * M - 40f);
            dlgRoot.style.width = w;
            dlgRoot.style.left = (PanelW - w) / 2f;
            string nm = RequiemUI.Clean(DialogueBox.Speaker);
            if (dlgName.text != nm) dlgName.text = nm;
            dlgName.style.display = nm.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            string line = DialogueBox.Line;
            string shown = RequiemUI.Clean(line.Substring(0, Mathf.Clamp(DialogueBox.Shown, 0, line.Length)));
            if (dlgText.text != shown) dlgText.text = shown;
            string key = GameInput.Label(GameAction.Interact);
            string next = DialogueBox.LineDone ? (DialogueBox.HasMore ? $"[{key}] next" : $"[{key}] close") : "";
            if (dlgNext.text != next) dlgNext.text = next;
            dlgNext.style.opacity = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3.5f));
        }

        /// <summary>Subtitles inside the dialogue box: name + fast typing text, no [key] next, fade in / out.</summary>
        private void UpdateSubtitle()
        {
            float w = Mathf.Min(1040f, PanelW - 2f * M - 40f);
            dlgRoot.style.width = w;
            dlgRoot.style.left = (PanelW - w) / 2f;
            float t = Time.time;
            dlgRoot.style.opacity = Mathf.Min(Mathf.Clamp01((t - Subtitles.Born) / 0.12f), Mathf.Clamp01((Subtitles.Until - t) / 0.35f));
            string nm = RequiemUI.Clean(Subtitles.Speaker);
            if (dlgName.text != nm) dlgName.text = nm;
            dlgName.style.display = nm.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            dlgName.style.color = Subtitles.NameColor.a > 0f ? Subtitles.NameColor : RequiemUI.Gold;
            string line = Subtitles.Text;
            int n = Mathf.Clamp(Mathf.FloorToInt((t - Subtitles.Born) * 70f), 0, line.Length);   // typing ~70 characters / second
            string shown = RequiemUI.Clean(line.Substring(0, n));
            if (dlgText.text != shown) dlgText.text = shown;
            if (dlgNext.text != "") dlgNext.text = "";
        }
    }
}
