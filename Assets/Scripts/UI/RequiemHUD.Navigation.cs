using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using SoulRequiem.Core;
using SoulRequiem.Gameplay;

namespace SoulRequiem.UI
{
    /// <summary>
    /// HUD — NAVIGATION: zone name, minimap + fog, main quest + task list (QuestTracker / QuestGuide), target arrow + ping halo,
    /// full map (M), interaction prompt, HP bars over enemies + dots for stray enemies.
    /// </summary>
    public partial class RequiemHUD
    {
        private void BuildNavigation()
        {
            zoneLabel = RequiemUI.Text("", "sr-title sr-zone", hud);
            zoneLabel.style.position = Position.Absolute; zoneLabel.style.right = M; zoneLabel.style.top = 18;

            minimapRoot = Group(hud, "minimap");
            minimapRoot.style.right = M; minimapRoot.style.top = 70; minimapRoot.style.width = 275; minimapRoot.style.height = 275;
            mapWindow = Group(minimapRoot, "window");
            RequiemUI.Abs(mapWindow, 27, 27, 221, 221);
            mapWindow.style.overflow = Overflow.Hidden;
            mapWindow.style.backgroundColor = new Color(0.04f, 0.02f, 0.06f);
            mapImage = new VisualElement { pickingMode = PickingMode.Ignore };
            mapImage.style.position = Position.Absolute;
            mapWindow.Add(mapImage);
            miniFog = new MinimapFogView(mapWindow, mapWindow.IndexOf(mapImage) + 1, "sr-fog-q", 46f, 28f);
            mapFireLayer = new VisualElement { pickingMode = PickingMode.Ignore };
            mapFireLayer.style.position = Position.Absolute; mapFireLayer.style.left = 0; mapFireLayer.style.top = 0;
            mapWindow.Add(mapFireLayer);
            mapTargetGlow = Glow(mapWindow, 44f, false);
            mapTargetPing = Glow(mapWindow, 40f, true);
            mapTarget = Diamond(mapWindow, 12);
            mapPlayer = RequiemUI.Img("hud_pointer", 110 - 11, 110 - 11, 22, 22, mapWindow);
            mapHidden = RequiemUI.Text("???", "sr-title sr-center", mapWindow);
            RequiemUI.Abs(mapHidden, 0, 0, 221, 221);
            var frame = new VisualElement { pickingMode = PickingMode.Ignore };
            RequiemUI.Abs(frame, 0, 0, 275, 275);
            var ft = RequiemUI.Tex("minimap_frame");
            if (ft != null) frame.style.backgroundImage = new StyleBackground(ft);
            minimapRoot.Add(frame);
            mapEdgeGlow = Glow(minimapRoot, 40f, false);
            mapTargetEdge = RequiemUI.Img("hud_pointer", 0, 0, 24, 24, minimapRoot);
            var mk = RequiemUI.Img("keycap", 275 - 40, 275 - 30, 28, 26, minimapRoot);
            var ml = RequiemUI.Text(GameInput.Label(GameAction.Map), "sr-key-small", mk);
            RequiemUI.Abs(ml, 0, 0, 28, 23);

            questPanel = new VisualElement { pickingMode = PickingMode.Ignore };
            questPanel.AddToClassList("sr-quest");
            RequiemUI.Slice(questPanel, "panel_dark", 14, 14, 14, 14);
            questPanel.style.position = Position.Absolute;
            questPanel.style.right = M; questPanel.style.top = 70 + 275 + 8; questPanel.style.width = 275;
            hud.Add(questPanel);
            questHead = RequiemUI.Text("MAIN QUEST", "sr-title sr-quest-head", questPanel);
            questTitle = RequiemUI.Text("", "sr-quest-title", questPanel);
            questChecks = new VisualElement { pickingMode = PickingMode.Ignore };
            questChecks.AddToClassList("sr-quest-checks");
            questPanel.Add(questChecks);
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("sr-quest-row");
            questPanel.Add(row);
            var arrow = RequiemUI.Img("hud_pointer", 0, 0, 14, 14, null);
            arrow.style.position = Position.Relative; arrow.style.marginRight = 6; arrow.style.flexShrink = 0;
            row.Add(arrow);
            questTarget = RequiemUI.Text("", "sr-quest-target", row);
            questDist = RequiemUI.Text("", "sr-quest-dist", row);
            // enemy count of the zone in combat (defeated / total)
            questKills = RequiemUI.Text("", "sr-quest-target", questPanel);
            questKills.style.color = RequiemUI.Gold;
            questKills.style.marginTop = 4;
        }

        private static VisualElement Diamond(VisualElement parent, float size)
        {
            var d = new VisualElement { pickingMode = PickingMode.Ignore };
            d.AddToClassList("sr-diamond");
            RequiemUI.Abs(d, 0, 0, size, size);
            d.style.rotate = new Rotate(45f);
            parent.Add(d);
            return d;
        }

        private void BuildInteractPrompt()
        {
            // 480-wide row centred on the anchor (avoids left:50%+translate squeezing the width)
            iPrompt = Group(fx, "interact");
            iPrompt.AddToClassList("sr-iprompt-row");
            var box = new VisualElement { pickingMode = PickingMode.Ignore };
            box.AddToClassList("sr-iprompt");
            iPrompt.Add(box);
            var cap = RequiemUI.Img("keycap", 0, 0, 32, 30, null);
            cap.style.position = Position.Relative; cap.style.marginRight = 8;
            iKey = RequiemUI.Text(GameInput.Label(GameAction.Interact), "sr-key", cap);
            RequiemUI.Abs(iKey, 0, 0, 32, 27);
            box.Add(cap);
            iVerb = RequiemUI.Text("Read", "sr-iprompt-text", box);
            iPrompt.style.display = DisplayStyle.None;
        }

        private void UpdateInteractPrompt(bool menu)
        {
            var f = InteractionSystem.Focus;
            var cam = Camera.main;
            if (menu || f == null || cam == null || root.panel == null) { iPrompt.style.display = DisplayStyle.None; return; }
            Vector2 sp = RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, new Vector3(f.anchor.x, f.anchor.y + 0.18f, 0f), cam);
            iPrompt.style.display = DisplayStyle.Flex;
            iPrompt.style.left = Mathf.Round(Mathf.Clamp(sp.x, 260f, PanelW - 260f) - 240f);
            iPrompt.style.top = Mathf.Round(Mathf.Clamp(sp.y - 46f, 90f, PanelH - 200f));
            string k = GameInput.Label(GameAction.Interact), v = RequiemUI.Clean(f.Verb);
            if (iKey.text != k) iKey.text = k;
            if (iVerb.text != v) iVerb.text = v;
        }

        private void BuildWaypoint()
        {
            waypoint = Group(fx, "waypoint");
            waypoint.style.width = 36; waypoint.style.height = 36;
            for (int i = 0; i < 4; i++)
            {
                var f = RequiemUI.Img("hud_wp_" + i, 0, 0, 36, 36, waypoint);
                f.style.display = i == 0 ? DisplayStyle.Flex : DisplayStyle.None;
                wpFrames.Add(f);
            }
            wpGlow = Glow(fx, 80f, false);
            edgeGlow = Glow(fx, 84f, false);
            fx.Insert(fx.IndexOf(waypoint), wpGlow);                     // halo sits under the target icon
            edgePointer = RequiemUI.Img("hud_pointer", 0, 0, 36, 36, fx);
            wpDist = RequiemUI.Text("", "sr-small sr-gold", fx);
            wpDist.style.position = Position.Absolute;
            waypoint.style.display = edgePointer.style.display = wpDist.style.display = DisplayStyle.None;
            wpGlow.style.display = edgeGlow.style.display = DisplayStyle.None;
        }

        // ---- glow halo / ping ring for the quest target
        private static Texture2D GlowTex => glowTex != null ? glowTex : (glowTex = Radial(48, (d) => Mathf.Pow(Mathf.Clamp01(1f - d), 2f)));
        private static Texture2D RingTex => ringTex != null ? ringTex : (ringTex = Radial(48, (d) => Mathf.Exp(-Mathf.Pow((d - 0.82f) / 0.08f, 2f))));

        private static Texture2D Radial(int n, System.Func<float, float> a)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                    px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(a(Mathf.Sqrt(u * u + v * v)))));
                }
            t.SetPixels32(px); t.Apply();
            return t;
        }

        private static VisualElement Glow(VisualElement parent, float size, bool ring)
        {
            var g = new VisualElement { pickingMode = PickingMode.Ignore };
            g.style.position = Position.Absolute;
            g.style.width = size; g.style.height = size;
            g.style.backgroundImage = new StyleBackground(ring ? RingTex : GlowTex);
            g.style.unityBackgroundImageTintColor = new Color(1f, 0.8f, 0.35f, 1f);
            g.style.display = DisplayStyle.None;
            parent.Add(g);
            return g;
        }

        /// <summary>Place the halo (centre `c`) + pulse; `ping` = a ring that grows then fades, repeating every 1.2 s.</summary>
        private static void Pulse(VisualElement glow, VisualElement ping, Vector2 c, float baseSize, float phase = 0f)
        {
            bool calm = GameSettings.ReduceFlashing;
            float t = Time.unscaledTime + phase;
            if (glow != null)
            {
                glow.style.display = DisplayStyle.Flex;
                glow.style.left = c.x - baseSize / 2f; glow.style.top = c.y - baseSize / 2f;
                // faint halo, soft pulse
                float k = calm ? 0.25f : 0.18f + 0.14f * (0.5f + 0.5f * Mathf.Sin(t * 4f));
                glow.style.opacity = k;
                float sc = calm ? 1f : 0.95f + 0.08f * Mathf.Sin(t * 4f);
                glow.style.scale = new Scale(new Vector3(sc, sc, 1f));
            }
            if (ping != null)
            {
                if (calm) { ping.style.display = DisplayStyle.None; return; }
                float u = Mathf.Repeat(t / 1.6f, 1f);
                ping.style.display = DisplayStyle.Flex;
                float w = ping.resolvedStyle.width > 1f ? ping.resolvedStyle.width : 40f;
                ping.style.left = c.x - w / 2f; ping.style.top = c.y - w / 2f;
                float sc = Mathf.Lerp(0.3f, 1.7f, u);
                ping.style.scale = new Scale(new Vector3(sc, sc, 1f));
                ping.style.opacity = 0.45f * (1f - u);
            }
        }

        /// <summary>Blink (skipped when Reduce flashing is on).</summary>
        private static void Blink(VisualElement v, float hz = 1.5f)
        {
            if (v == null) return;
            v.style.opacity = GameSettings.ReduceFlashing ? 1f : 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * hz * Mathf.PI * 2f);   // gentle blink
        }

        private void BuildBigMap()
        {
            bigMap = new VisualElement { pickingMode = PickingMode.Ignore };
            bigMap.AddToClassList("sr-fill");
            bigMap.style.backgroundColor = new Color(0.02f, 0.01f, 0.04f, 0.82f);
            fx.Add(bigMap);
            bigMapImage = new VisualElement { pickingMode = PickingMode.Ignore };
            bigMapImage.AddToClassList("sr-bigmap");
            bigMap.Add(bigMapImage);
            bigMapImage.style.overflow = Overflow.Hidden;
            bigFog = new MinimapFogView(bigMapImage, 0, "sr-fog-q sr-fog-q-big", 68f, 40f, true, "sr-fog-name", 240f, 26f);
            bigFireLayer = new VisualElement { pickingMode = PickingMode.Ignore };
            bigFireLayer.style.position = Position.Absolute; bigFireLayer.style.left = 0; bigFireLayer.style.top = 0;
            bigMapImage.Add(bigFireLayer);
            bigMapGlow = Glow(bigMapImage, 64f, false);
            bigMapPing = Glow(bigMapImage, 60f, true);
            bigMapTarget = Diamond(bigMapImage, 16);
            bigMapPlayer = RequiemUI.Img("hud_pointer", 0, 0, 26, 26, bigMapImage);
            bigMapTitle = RequiemUI.Text("", "sr-title sr-bigmap-title", bigMap);
            var close = RequiemUI.Text($"Press {GameInput.Label(GameAction.Map)} to close", "sr-small sr-gold sr-bigmap-hint", bigMap);
            bigMap.style.display = DisplayStyle.None;
        }

        private void UpdateNavigation(QuestTracker q, MapEncounterDirector dir)
        {
            string zone = dir != null ? RequiemUI.Clean(dir.CurrentZoneTitle) : "";
            if (zoneLabel.text != zone) zoneLabel.text = zone;
            if (q == null || !q.HasPlayer) { minimapRoot.style.display = questPanel.style.display = DisplayStyle.None; return; }
            Vector2 me = q.PlayerTile;
            bool hidden = q.HiddenAt(me) != null;

            // minimap
            minimapRoot.style.display = q.Minimap != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (q.Minimap != null)
            {
                const float view = 221f, tiles = 34f;
                float k = view / tiles;
                var mt = q.MapTiles;
                if (mapImage.style.backgroundImage.value.texture != q.Minimap) mapImage.style.backgroundImage = new StyleBackground(q.Minimap);
                float iw = mt.x * k, ih = mt.y * k;
                mapImage.style.width = iw; mapImage.style.height = ih;
                // The view follows the player but NEVER slides past the image edge -> no black border when standing at the map edge.
                // At the edge: the image stays put, the player arrow leaves the centre and moves toward the edge.
                float ox = MinimapOffset(view / 2f - me.x * k, iw, view);
                float oy = MinimapOffset(view / 2f - me.y * k, ih, view);
                mapImage.style.left = ox; mapImage.style.top = oy;
                mapPlayer.style.left = ox + me.x * k - 11f; mapPlayer.style.top = oy + me.y * k - 11f;
                mapImage.style.display = mapPlayer.style.display = hidden ? DisplayStyle.None : DisplayStyle.Flex;
                var fog = dir != null ? dir.Fog : null;
                miniFog.Update(fog, Mathf.RoundToInt(q.Ppt), new Vector2(ox, oy), k, new Rect(0f, 0f, view, view), !hidden);
                mapHidden.style.display = hidden ? DisplayStyle.Flex : DisplayStyle.None;
                Vector2 f = kit != null ? kit.Facing : Vector2.down;
                mapPlayer.style.rotate = new Rotate(Mathf.Atan2(-f.y, f.x) * Mathf.Rad2Deg);
                // rest campfires
                int nf = 0;
                if (!hidden)
                    foreach (var sh in ShrineMarker.All)
                    {
                        if (sh == null) continue;
                        if (fog != null && !fog.IsClear(q.WorldToTile(sh.transform.position))) continue;   // campfire under fog: unknown
                        Vector2 fp = new Vector2(ox, oy) + q.WorldToTile(sh.transform.position) * k;
                        if (fp.x < 4f || fp.x > view - 4f || fp.y < 4f || fp.y > view - 4f) continue;
                        var ic = FireIcon(mapFires, mapFireLayer, nf++, 14f);
                        ic.style.left = fp.x - 7f; ic.style.top = fp.y - 9f;
                        ic.style.unityBackgroundImageTintColor = sh.Lit ? FireLit : FireUnlit;
                    }
                for (int i = nf; i < mapFires.Count; i++) mapFires[i].style.display = DisplayStyle.None;
                // stray enemies left in the zone (idle for a long time, door still shut) -> blinking red dot
                int nfo = 0;
                if (!hidden)
                    foreach (var foe in MapEncounterDirector.Stragglers())
                    {
                        Vector2 ep2 = new Vector2(ox, oy) + q.WorldToTile(foe.transform.position) * k;
                        if (ep2.x < 3f || ep2.x > view - 3f || ep2.y < 3f || ep2.y > view - 3f) continue;
                        var dot = FoeDot(nfo++);
                        dot.style.left = ep2.x - 4f; dot.style.top = ep2.y - 4f;
                        dot.style.opacity = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 5f));
                    }
                for (int i = nfo; i < mapFoes.Count; i++) mapFoes[i].style.display = DisplayStyle.None;
                mapTarget.style.display = mapTargetEdge.style.display = DisplayStyle.None;
                mapTargetGlow.style.display = mapTargetPing.style.display = mapEdgeGlow.style.display = DisplayStyle.None;
                bool arrived = MapEncounterDirector.Instance != null && MapEncounterDirector.Instance.TargetReachedInFight(q.TargetWorld);
                if (q.HasTarget && !hidden && !arrived)
                {
                    Vector2 c = new Vector2(view / 2f, view / 2f);
                    Vector2 tp = new Vector2(ox, oy) + q.TargetTile * k;
                    Vector2 d = tp - c;
                    float e = 10f;
                    if (tp.x >= e && tp.x <= view - e && tp.y >= e && tp.y <= view - e)
                    {
                        mapTarget.style.display = DisplayStyle.Flex;
                        mapTarget.style.left = tp.x - 6f; mapTarget.style.top = tp.y - 6f;
                        Pulse(mapTargetGlow, mapTargetPing, tp, 44f);
                        Blink(mapTarget);
                    }
                    else
                    {
                        float t = Mathf.Min((c.x - e) / Mathf.Max(0.001f, Mathf.Abs(d.x)), (c.y - e) / Mathf.Max(0.001f, Mathf.Abs(d.y)));
                        Vector2 ep = c + d * t + new Vector2(27f, 27f);
                        mapTargetEdge.style.display = DisplayStyle.Flex;
                        mapTargetEdge.style.left = ep.x - 12f; mapTargetEdge.style.top = ep.y - 12f;
                        Pulse(mapEdgeGlow, null, ep, 40f);
                        Blink(mapTargetEdge);
                        mapTargetEdge.style.rotate = new Rotate(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                    }
                }
            }

            // quest
            questPanel.style.display = q.HasQuest ? DisplayStyle.Flex : DisplayStyle.None;
            questPanel.style.top = q.Minimap != null ? 70 + 275 + 8 : 70;
            if (!q.HasQuest) return;
            bool fresh = Time.time - q.StepChangedAt < 3f;
            string head = fresh ? "QUEST UPDATED" : q.IsSide ? "SIDE QUEST" : "MAIN QUEST";   // secret map = side quest
            if (questHead.text != head) questHead.text = head;
            questPanel.EnableInClassList("sr-quest-fresh", fresh && !GameSettings.ReduceFlashing && Mathf.Repeat(Time.time, 0.5f) < 0.25f);
            string title = q.AllDone ? "The way onward is open." : q.Title + (q.TotalCount > 0 ? $" ({q.DoneCount}/{q.TotalCount})" : "");
            title = RequiemUI.Clean(title);
            if (title != lastQuest) { questTitle.text = title; lastQuest = title; }
            UpdateChecks(q);
            string target = hidden ? "Find a way back up." : (!q.AllDone && q.HasTarget ? RequiemUI.Clean(q.Label) : "");
            if (questTarget.text != target) questTarget.text = target;
            string dist = !hidden && !q.AllDone && q.HasTarget ? $"{Vector2.Distance(me, q.TargetTile):0}m" : "";
            if (questDist.text != dist) questDist.text = dist;
            questTarget.parent.style.display = target.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;

            // "clear the zone" step -> show defeated / total while the zone is in combat
            string kills = "";
            if (dir != null && !q.AllDone)
                foreach (var tok in q.StepDone.Split(',', '|'))
                {
                    string f = tok.Trim();
                    if (!f.EndsWith("_cleared")) continue;
                    if (dir.ZoneTally(f.Substring(0, f.Length - 8), out int slain, out int total)) { kills = $"Foes defeated: {slain} / {total}"; break; }
                }
            if (questKills.text != kills) questKills.text = kills;
            questKills.style.display = kills.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>Draw the sub-task list (check boxes drawn with USS borders — no dependence on the font having ☐/☑).
        /// Done = filled gold box + dimmed text; pointed at by the arrow = bright text; condition missing = "locked".</summary>
        private void UpdateChecks(QuestTracker q)
        {
            var list = q.AllDone ? null : q.Checks;
            int n = list != null ? list.Count : 0;
            questChecks.style.display = n > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            while (checkRows.Count < n)
            {
                var r = new VisualElement { pickingMode = PickingMode.Ignore };
                r.AddToClassList("sr-quest-check");
                var box = new VisualElement { pickingMode = PickingMode.Ignore };
                box.AddToClassList("sr-quest-box");
                r.Add(box);
                var t = RequiemUI.Text("", "sr-quest-check-text", r);
                questChecks.Add(r);
                checkRows.Add((r, box, t));
            }
            for (int i = 0; i < checkRows.Count; i++)
            {
                var (r, box, t) = checkRows[i];
                if (i >= n) { r.style.display = DisplayStyle.None; continue; }
                var c = list[i];
                r.style.display = DisplayStyle.Flex;
                string text = RequiemUI.Clean(c.text) + (c.locked && !c.done ? "  - locked" : "");
                if (t.text != text) t.text = text;
                box.EnableInClassList("sr-quest-box-done", c.done);
                r.EnableInClassList("sr-quest-check-done", c.done);
                r.EnableInClassList("sr-quest-check-active", c.active && !c.done);
                r.EnableInClassList("sr-quest-check-locked", c.locked && !c.done);
            }
        }

        private void UpdateWorld(QuestTracker q, bool menu)
        {
            var cam = Camera.main;
            var panel = root.panel;
            // quest target arrow
            bool wp = !menu && cam != null && q != null && q.HasPlayer && q.HasTarget && !q.BigMap && q.HiddenAt(q.PlayerTile) == null
                      && hp != null && !hp.IsDead && !CutsceneState.Active    // no target arrow during cutscenes
                      && !DialogueBox.Active && !Subtitles.Visible;           // dialogue box / subtitles showing -> hide the arrow + distance for readability
            float dist = wp ? Vector2.Distance(q.PlayerTile, q.TargetTile) : 0f;
            if (wp && dist < 2f) wp = false;
            var med = MapEncounterDirector.Instance;
            if (wp && med != null && med.TargetReachedInFight(q.TargetWorld)) wp = false;   // arrived at the zone in combat
            waypoint.style.display = edgePointer.style.display = wpDist.style.display = DisplayStyle.None;
            wpGlow.style.display = edgeGlow.style.display = DisplayStyle.None;
            if (wp)
            {
                Vector2 sp = RuntimePanelUtils.CameraTransformWorldToPanel(panel, q.TargetWorld, cam);
                Vector3 vp = cam.WorldToViewportPoint(q.TargetWorld);
                float mg = 64f, W = PanelW, H = PanelH;
                bool on = vp.z > 0f && sp.x >= mg && sp.x <= W - mg && sp.y >= mg && sp.y <= H - mg;
                string dt = $"{dist:0}m";
                if (wpDist.text != dt) wpDist.text = dt;
                if (on)
                {
                    int f = Mathf.FloorToInt(Time.unscaledTime * 6f) % wpFrames.Count;
                    for (int i = 0; i < wpFrames.Count; i++) wpFrames[i].style.display = i == f ? DisplayStyle.Flex : DisplayStyle.None;
                    waypoint.style.display = DisplayStyle.Flex;
                    waypoint.style.left = sp.x - 18f; waypoint.style.top = sp.y - 58f;
                    // (no halo around the on-screen target flag — too bright)
                }
                else
                {
                    Vector2 c = new Vector2(W / 2f, H / 2f), d = sp - c;
                    if (vp.z < 0f) d = -d;
                    float t = Mathf.Min((c.x - mg) / Mathf.Max(0.001f, Mathf.Abs(d.x)), (c.y - mg) / Mathf.Max(0.001f, Mathf.Abs(d.y)));
                    Vector2 e = c + d * t;
                    edgePointer.style.display = wpDist.style.display = DisplayStyle.Flex;
                    edgePointer.style.left = e.x - 18f; edgePointer.style.top = e.y - 18f;
                    Blink(edgePointer, 1.2f);                                // screen-edge arrow: no halo, gentle blink
                    float pulse = GameSettings.ReduceFlashing ? 1f : 1f + 0.1f * Mathf.Sin(Time.unscaledTime * 5f);
                    edgePointer.style.scale = new Scale(new Vector3(pulse, pulse, 1f));
                    edgePointer.style.rotate = new Rotate(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                    Vector2 inward = (c - e).normalized * 38f;
                    wpDist.style.left = e.x + inward.x - 24f; wpDist.style.top = e.y + inward.y - 12f;
                }
            }

            // normal enemy HP bars
            int used = 0;
            if (!menu && cam != null)
                foreach (var e in EnemyHealthBar.All)
                {
                    if (e == null || e.Health == null || e.Health.IsDead || !e.isActiveAndEnabled) continue;
                    if (e.Health.Invulnerable && e.TryGetComponent<EnemyAI>(out var ai) && ai.Held) continue;   // knocked out / fleeing: not a target
                    Vector3 vp = cam.WorldToViewportPoint(e.HeadWorld);
                    if (vp.z < 0f || vp.x < -0.05f || vp.x > 1.05f || vp.y < -0.05f || vp.y > 1.1f) continue;
                    Vector2 sp = RuntimePanelUtils.CameraTransformWorldToPanel(panel, e.HeadWorld, cam);
                    var bar = Bar(used++);
                    bar.root.style.display = DisplayStyle.Flex;
                    bar.root.style.left = Mathf.Round(sp.x - EbW / 2f); bar.root.style.top = Mathf.Round(sp.y - EbH - 6f);
                    float pct = e.Health.Percent;
                    // trail of HP just lost (held 0.4 s, then slides)
                    if (!enemyLag.TryGetValue(e.Health, out var lg)) lg = new Vector2(pct, 0f);
                    if (pct >= lg.x) lg = new Vector2(pct, 0.4f);
                    else if (lg.y > 0f) lg.y -= Time.unscaledDeltaTime;
                    else lg.x = Mathf.MoveTowards(lg.x, pct, Time.unscaledDeltaTime * 0.7f);
                    enemyLag[e.Health] = lg;
                    float inner = EbW - 4f;
                    bar.fill.style.width = inner * pct;
                    bar.shine.style.width = inner * pct;
                    bar.lag.style.width = inner * lg.x;
                    bar.num.style.display = GameSettings.EnemyHpNumbers ? DisplayStyle.Flex : DisplayStyle.None;
                    string n = $"{Mathf.CeilToInt(e.Health.Current)}/{e.Health.Max:0}";
                    if (bar.num.text != n) bar.num.text = n;
                }
            for (int i = used; i < enemyBars.Count; i++) enemyBars[i].root.style.display = DisplayStyle.None;
        }

        // enemy HP bar: 84x14 (reference frame) — ink background, 2px black outline, gold line on top, red with a highlight, trail of HP just lost; HP number above
        private const float EbW = 84f, EbH = 14f;

        private EnemyBar Bar(int i)
        {
            while (enemyBars.Count <= i)
            {
                var b = new EnemyBar { root = Group(fx, "ebar") };
                b.root.style.width = EbW; b.root.style.height = EbH;
                b.root.AddToClassList("sr-ebar");
                b.lag = Box(b.root, 0, 0, EbW - 4f, EbH - 4f, Lag);
                b.fill = RequiemUI.Img("bar_fill", 0, 0, EbW - 4f, EbH - 4f, b.root);
                b.fill.style.unityBackgroundImageTintColor = MobRed;
                b.shine = Box(b.root, 0, 0, EbW - 4f, 2f, new Color(1f, 0.6f, 0.6f, 0.85f));
                b.num = RequiemUI.Text("", "sr-ebar-num", b.root);
                RequiemUI.Abs(b.num, -38, -26, EbW + 72, 24);
                fx.Insert(0, b.root);
                enemyBars.Add(b);
            }
            return enemyBars[i];
        }

        /// <summary>Position of the minimap image in its frame: wants to be at <paramref name="want"/> (player centred) but clamped so the image always fills the frame.
        /// An image smaller than the frame is centred.</summary>
        private VisualElement FoeDot(int i)
        {
            while (mapFoes.Count <= i)
            {
                var d = new VisualElement { pickingMode = PickingMode.Ignore };
                d.style.position = Position.Absolute; d.style.width = 8; d.style.height = 8;
                d.style.backgroundColor = new Color(0.95f, 0.2f, 0.18f);
                d.style.borderTopLeftRadius = d.style.borderTopRightRadius = d.style.borderBottomLeftRadius = d.style.borderBottomRightRadius = 4;
                d.style.borderTopWidth = d.style.borderBottomWidth = d.style.borderLeftWidth = d.style.borderRightWidth = 1;
                d.style.borderTopColor = d.style.borderBottomColor = d.style.borderLeftColor = d.style.borderRightColor = new Color(0.15f, 0.02f, 0.02f);
                mapFireLayer.Add(d);
                mapFoes.Add(d);
            }
            mapFoes[i].style.display = DisplayStyle.Flex;
            return mapFoes[i];
        }

        private static VisualElement FireIcon(List<VisualElement> pool, VisualElement layer, int i, float size)
        {
            while (pool.Count <= i) pool.Add(RequiemUI.Img("map_campfire", 0, 0, size, size, layer));
            pool[i].style.display = DisplayStyle.Flex;
            return pool[i];
        }

        private static float MinimapOffset(float want, float img, float view)
            => img <= view ? (view - img) / 2f : Mathf.Clamp(want, view - img, 0f);

        private bool bigMapShown; // negative

        private void UpdateBigMap(QuestTracker q, MapEncounterDirector dir)
        {
            bool show = q != null && q.BigMap && q.Minimap != null && !RequiemMenus.Open;
            if (show != bigMapShown) { bigMapShown = show; SoulRequiem.Core.Sfx.Play2D(show ? "map_open" : "ui_back"); } // negative
            bigMap.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) return;
            var v = q.ViewTiles;
            float W = PanelW, H = PanelH;
            float k = Mathf.Min(W * 0.82f / v.width, H * 0.76f / v.height);
            float w = v.width * k, h = v.height * k;
            bigMapImage.style.left = (W - w) / 2f; bigMapImage.style.top = (H - h) / 2f + 10f;
            bigMapImage.style.width = w; bigMapImage.style.height = h;
            if (bigMapImage.style.backgroundImage.value.texture != q.Minimap) bigMapImage.style.backgroundImage = new StyleBackground(q.Minimap);
            float tw = q.Minimap.width, th = q.Minimap.height, ppt = q.Ppt;
            // only show the "view" part of the image: scale the image and shift it back
            bigMapImage.style.backgroundSize = new BackgroundSize(tw / ppt * k, th / ppt * k);
            bigMapImage.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Left, -v.x * k);
            bigMapImage.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Top, -v.y * k);
            Vector2 me = q.PlayerTile;
            bool hidden = q.HiddenAt(me) != null;
            var fog = dir != null ? dir.Fog : null;
            bigFog.Update(fog, Mathf.RoundToInt(ppt), -v.position * k, k, new Rect(0f, 0f, w, h), !hidden);
            Vector2 pp = (me - v.position) * k;
            bigMapPlayer.style.display = hidden ? DisplayStyle.None : DisplayStyle.Flex;
            bigMapPlayer.style.left = pp.x - 13f; bigMapPlayer.style.top = pp.y - 13f;
            Vector2 f = kit != null ? kit.Facing : Vector2.down;
            bigMapPlayer.style.rotate = new Rotate(Mathf.Atan2(-f.y, f.x) * Mathf.Rad2Deg);
            int bf = 0;
            foreach (var sh in ShrineMarker.All)
            {
                if (sh == null || hidden) continue;
                if (fog != null && !fog.IsClear(q.WorldToTile(sh.transform.position))) continue;
                Vector2 fp = (q.WorldToTile(sh.transform.position) - v.position) * k;
                var ic = FireIcon(bigFires, bigFireLayer, bf++, 22f);
                ic.style.left = fp.x - 11f; ic.style.top = fp.y - 14f;
                ic.style.unityBackgroundImageTintColor = sh.Lit ? FireLit : FireUnlit;
            }
            for (int i = bf; i < bigFires.Count; i++) bigFires[i].style.display = DisplayStyle.None;
            bigMapTarget.style.display = q.HasTarget && !hidden ? DisplayStyle.Flex : DisplayStyle.None;
            Vector2 tp = (q.TargetTile - v.position) * k;
            bigMapTarget.style.left = tp.x - 8f; bigMapTarget.style.top = tp.y - 8f;
            if (q.HasTarget && !hidden) { Pulse(bigMapGlow, bigMapPing, tp, 64f); Blink(bigMapTarget); }
            else bigMapGlow.style.display = bigMapPing.style.display = DisplayStyle.None;
            string head = hidden ? "Unknown depths" : RequiemUI.Clean(dir != null ? dir.CurrentZoneTitle : "");
            if (bigMapTitle.text != head) bigMapTitle.text = head;
            bigMapTitle.style.top = (H - h) / 2f - 40f;
        }
    }
}
