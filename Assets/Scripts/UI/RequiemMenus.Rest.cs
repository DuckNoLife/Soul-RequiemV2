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
    /// MANTRA / REST PANEL (T at a campfire, L anywhere): 3 sections Mantras / Mantra Sets / Passives, skill slot column on the left, list in the middle,
    /// detail card on the right. Equips / unequips skills through PlayerLoadout.
    /// </summary>
    public partial class RequiemMenus
    {
        // ------------------------------------------------------------------ REST / SKILLS
        // same visual language as the inventory: PixelLab frames, no default scroll bars,
        // 3 sections Q/E (Mantras | Mantra Sets | Passives), skill slot column on the left, list in the middle, full detail card on the right.
        private int selSlot;
        private VisualElement restPanel;
        private string restTab = "Mantra", restSel = "";
        private Action restClear;
        private static readonly string[] RestTabs = { "Mantra", "Set", "Passive" };
        private static string RestTabLabel(string t) => t == "Set" ? "Mantra Sets" : t == "Passive" ? "Passives" : "Mantras";
        private static string RestTabIcon(string t) => "inv/cat_" + (t == "Set" ? "set" : t == "Passive" ? "passive" : "mantra");
        private static string SlotKey(int i) => GameInput.KeyLabel(GameInput.SkillKey(i));

        private sealed class RestEntry
        {
            public SkillDefinition skill;
            public List<SkillDefinition> set;
            public PassiveCatalog.Entry passive;
            public bool isPassive;
            public string Id => isPassive ? "p:" + passive.flag : set != null ? "s:" + set[0].setId : "m:" + skill.id;
            public string Name => isPassive ? passive.name : set != null ? PlayerLoadout.SetName(set[0]) : skill.displayName;
            public Texture2D Icon => isPassive ? RequiemUI.Tex(passive.icon) : set != null ? set[0].Icon : skill.Icon;
        }

        /// <param name="atRest"> true = opened while resting at a campfire (title REST…, already healed); false = key L anywhere.</param>
        private VisualElement BuildRest(bool atRest)
        {
            var dir = MapEncounterDirector.Instance;
            if (Array.IndexOf(RestTabs, restTab) < 0) restTab = "Mantra";
            var p = new VisualElement();
            p.style.width = InvW; p.style.maxWidth = Length.Percent(96);
            p.style.height = InvH; p.style.maxHeight = Length.Percent(96);
            if (!Art(p, ArtFrame, 44, 44, 44, 44)) { p.style.backgroundColor = InvBg; Border(p, 2, InvFrame); }
            Pad(p, 58, 46);

            var head = Row(p); head.style.alignItems = Align.Center; head.style.flexShrink = 0;
            var tcol = new VisualElement(); tcol.style.flexGrow = 1; head.Add(tcol);
            string ttl = !atRest ? "MANTRAS" : ShrineMarker.Noun == "shrine" ? "SHRINE OF REST" : "REST BY THE " + ShrineMarker.Noun.ToUpperInvariant();
            Txt(ttl, "sr-title", tcol, RequiemUI.Gold, 40).style.letterSpacing = 3;
            Txt(atRest ? "Health and flasks restored - progress saved. Drag a mantra onto a socket, or pick EQUIP TO below its name."
                       : "Drag a mantra onto a socket, or pick EQUIP TO below its name. Drag a socket back to the list to clear it.", "sr-tiny", tcol, InvMuted, 17).style.marginTop = 2;
            var tabsWrap = Row(head); tabsWrap.style.alignItems = Align.Center;
            KeyCap("Q", tabsWrap).style.marginRight = 12;
            var tabs = Row(tabsWrap);
            KeyCap("E", tabsWrap).style.marginLeft = 12;
            var rule = new VisualElement(); rule.style.flexShrink = 0; rule.style.marginTop = 12;
            if (Art(rule, ArtDivider, 44, 0, 44, 0)) rule.style.height = 36; else { rule.style.height = 1; rule.style.backgroundColor = InvLine; }
            p.Add(rule);

            var body = Row(p); body.style.flexGrow = 1; body.style.flexShrink = 1; body.style.marginTop = 22;
            var foot = Row(p); foot.style.flexShrink = 0; foot.style.marginTop = 18;
            foot.style.justifyContent = Justify.FlexEnd; foot.style.alignItems = Align.Center; foot.style.height = 40;

            Action rebuild = null;
            Action<RestEntry> attune = e =>
            {
                if (loadout == null || e == null || e.isPassive) return;
                if (e.set != null) loadout.Equip(0, PlayerLoadout.SetLeader(e.set[0]) ?? e.set[0]);
                else loadout.Equip(selSlot, e.skill);
                // no extra message — the visuals already show the change (skill panel updated)
                rebuild();
            };
            restClear = () => { if (loadout != null && loadout.SkillAt(selSlot) != null) { loadout.Equip(selSlot, null); rebuild(); } };
            // equip into a CHOSEN slot (EQUIP TO F/R/C buttons, drag and drop). A skill already in another slot swaps places (PlayerLoadout.Equip).
            Action<RestEntry, int> attuneAt = (e, slot) =>
            {
                if (loadout == null || e == null || e.isPassive) return;
                if (e.set != null) { attune(e); return; }
                loadout.Equip(slot, e.skill);
                selSlot = slot;
                rebuild();
            };
            // drag-and-drop target slots: glowing outline (1 = can drop, 2 = hovered)
            Action<VisualElement, int> mark = (el, st) =>
            {
                var g = el.Q<VisualElement>("drop-glow");
                if (g == null) return;
                g.style.display = st == 0 ? DisplayStyle.None : DisplayStyle.Flex;
                var c = st == 2 ? RequiemUI.Gold : new Color(RequiemUI.Gold.r, RequiemUI.Gold.g, RequiemUI.Gold.b, 0.45f);
                g.style.borderTopColor = g.style.borderBottomColor = g.style.borderLeftColor = g.style.borderRightColor = c;
                g.style.backgroundColor = st == 2 ? new Color(RequiemUI.Gold.r, RequiemUI.Gold.g, RequiemUI.Gold.b, 0.14f) : new Color(0f, 0f, 0f, 0f);
            };
            Func<VisualElement, VisualElement> glowOf = el =>
            {
                var g = new VisualElement { name = "drop-glow", pickingMode = PickingMode.Ignore };
                g.style.position = Position.Absolute; g.style.left = g.style.right = g.style.top = g.style.bottom = 0;
                g.style.borderTopWidth = g.style.borderBottomWidth = g.style.borderLeftWidth = g.style.borderRightWidth = 2;
                g.style.display = DisplayStyle.None;
                el.Add(g);
                return el;
            };

            rebuild = () =>
            {
                var flags = dir != null ? dir.Flags : null;
                var all = new List<RestEntry>();
                var seen = new HashSet<string>();
                if (loadout != null)
                    foreach (var sk in loadout.owned)
                    {
                        if (PlayerLoadout.InSet(sk))
                        {
                            if (!seen.Add(sk.setId)) continue;
                            var mem = PlayerLoadout.SetOf(sk); mem.Sort((a, b) => a.setSlot.CompareTo(b.setSlot));
                            all.Add(new RestEntry { set = mem });
                        }
                        else all.Add(new RestEntry { skill = sk });
                    }
                foreach (var pe in PassiveCatalog.Learned(flags)) all.Add(new RestEntry { passive = pe, isPassive = true });
                Func<RestEntry, string> kindOf = e => e.isPassive ? "Passive" : e.set != null ? "Set" : "Mantra";

                tabs.Clear();
                foreach (var t in RestTabs)
                {
                    string tab = t;
                    tabs.Add(Tab(RestTabLabel(tab), RestTabIcon(tab), all.Count(x => kindOf(x) == tab), restTab == tab, () => { restTab = tab; rebuild(); }));
                }
                var shown = all.Where(x => kindOf(x) == restTab).ToList();
                if (shown.Count > 0 && shown.All(x => x.Id != restSel)) restSel = shown[0].Id;
                body.Clear();

                // ---- skill slot column
                var sockCol = new VisualElement(); sockCol.style.width = 330; sockCol.style.flexShrink = 0;
                body.Add(sockCol);
                Header("SOCKETS", sockCol);
                VisualElement focusSock = null;
                var sockEls = new List<VisualElement>();
                VisualElement listDrop = null;
                for (int i = 0; i < PlayerLoadout.SlotCount; i++)
                {
                    int idx = i;
                    var sk = loadout != null ? loadout.SkillAt(i) : null;
                    var r = new VisualElement { focusable = true, tabIndex = 0 };
                    r.style.flexDirection = FlexDirection.Row; r.style.alignItems = Align.Center; r.style.flexShrink = 0;
                    r.style.height = 108; r.style.marginBottom = 8; r.style.paddingLeft = 12; r.style.paddingRight = 12;
                    r.style.borderLeftWidth = 4;
                    StyleRow(r, i == selSlot);
                    var bx = SlotBox(sk != null ? sk.Icon : RequiemUI.Tex("ic_slot_empty"), 88, 64, sk != null ? 1f : 0.5f);
                    r.Add(bx);
                    var kc = KeyCap(SlotKey(i), bx); kc.style.position = Position.Absolute; kc.style.right = -8; kc.style.bottom = -8;
                    var c = new VisualElement { pickingMode = PickingMode.Ignore }; c.style.marginLeft = 18; c.style.flexShrink = 1; c.style.overflow = Overflow.Hidden;
                    r.Add(c);
                    var nm = Txt(sk != null ? sk.displayName : "Empty", "sr-small", c, sk != null ? RequiemUI.Bone : InvFaint); NoWrap(nm);
                    var sub = Txt(sk != null ? $"{sk.Kind}  -  {sk.cooldown:0.#}s" : "No mantra attuned", "sr-tiny", c, InvMuted, 16); NoWrap(sub); sub.style.marginTop = 2;
                    glowOf(r);
                    sockEls.Add(r);
                    if (sk != null && loadout != null)
                    {
                        // drag a slot onto another = swap; drag back to the list = unequip
                        var skIcon = sk.Icon; var self = r;
                        r.AddManipulator(new DragToSlot(() => skIcon, () => { selSlot = idx; rebuild(); },
                            to =>
                            {
                                if (to == null) return;
                                int j = sockEls.IndexOf(to);
                                var moving = loadout.SkillAt(idx);
                                if (j >= 0 && j != idx && moving != null) { loadout.Equip(j, moving); selSlot = j; rebuild(); }
                                else if (to == listDrop) { loadout.Equip(idx, null); rebuild(); }
                            },
                            () => { var l = new List<VisualElement>(); foreach (var se in sockEls) if (se != self) l.Add(se); if (listDrop != null) l.Add(listDrop); return l; },
                            () => p, mark));
                    }
                    else r.AddManipulator(new Clickable(() => { selSlot = idx; rebuild(); }));
                    r.RegisterCallback<NavigationSubmitEvent>(_ => { selSlot = idx; rebuild(); });
                    sockCol.Add(r);
                    if (i == selSlot) focusSock = r;
                }
                SkillDefinition s0 = loadout != null ? loadout.SkillAt(0) : null;
                if (s0 != null && PlayerLoadout.InSet(s0))
                {
                    var note = Txt($"Set attuned: {PlayerLoadout.SetName(s0)}. Attuning any single mantra removes the whole set.", "sr-tiny", sockCol, InvMuted, 16);
                    note.style.whiteSpace = WhiteSpace.Normal; note.style.marginTop = 6;
                }

                // ---- list
                var listWrap = new VisualElement(); listWrap.style.width = ListW; listWrap.style.flexShrink = 0; listWrap.style.marginLeft = 28;
                body.Add(listWrap);
                listDrop = glowOf(listWrap);                                      // drop a slot here = unequip
                Header(RestTabLabel(restTab).ToUpperInvariant(), listWrap);
                var list = QuietScroll(listWrap);
                var card = new VisualElement();
                card.style.flexGrow = 1; card.style.flexShrink = 1; card.style.flexBasis = 0; card.style.marginLeft = 28;
                card.style.backgroundColor = InvSurface; Border(card, 1, InvLine); card.style.overflow = Overflow.Hidden;
                body.Add(card);

                var rows = new List<KeyValuePair<RestEntry, VisualElement>>();
                Action<string> select = null;
                select = id =>
                {
                    restSel = id;
                    foreach (var kv in rows) StyleRow(kv.Value, kv.Key.Id == id);
                    var cur = shown.FirstOrDefault(x => x.Id == id);
                    card.Clear(); invReader = null; foot.Clear();
                    if (cur == null)
                    {
                        var em = Txt(restTab == "Passive" ? "No passive gifts learned yet." : restTab == "Set" ? "No mantra sets yet." : "You know no single mantras yet.", "sr-small", card, InvFaint);
                        em.style.alignSelf = Align.Center; em.style.marginTop = 160;
                    }
                    else RestDetail(card, cur, attune, attuneAt);
                    if (cur != null && !cur.isPassive) Prompt("Enter", cur.set != null ? "Attune set" : $"Attune to {SlotKey(selSlot)}", foot, () => attune(cur));
                    if (loadout != null && loadout.SkillAt(selSlot) != null) Prompt("Del", $"Clear socket {SlotKey(selSlot)}", foot, restClear);
                    Prompt("Esc", "Leave", foot, Pop);
                };
                VisualElement focusRow = null;
                foreach (var e in shown)
                {
                    var en = e;
                    var r = new VisualElement { focusable = true, tabIndex = 0 };
                    r.style.flexDirection = FlexDirection.Row; r.style.alignItems = Align.Center; r.style.flexShrink = 0;
                    r.style.height = 84; r.style.marginBottom = 4; r.style.paddingLeft = 12; r.style.paddingRight = 14;
                    r.style.borderLeftWidth = 4; r.style.borderLeftColor = Clear0;
                    if (en.set != null)
                    {
                        var stack3 = new VisualElement { pickingMode = PickingMode.Ignore }; stack3.style.width = 72; stack3.style.height = 72; stack3.style.flexShrink = 0;
                        if (!Art(stack3, ArtSlot, 16, 16, 16, 16)) stack3.style.backgroundColor = new Color(0.028f, 0.02f, 0.036f, 1f);
                        for (int k = 0; k < en.set.Count && k < 3; k++)
                        {
                            var ic = new VisualElement { pickingMode = PickingMode.Ignore };
                            ic.style.position = Position.Absolute; ic.style.width = 34; ic.style.height = 34;
                            ic.style.left = k == 0 ? 6 : k == 1 ? 32 : 19; ic.style.top = k == 2 ? 32 : 6;
                            if (en.set[k].Icon != null) ic.style.backgroundImage = new StyleBackground(en.set[k].Icon);
                            stack3.Add(ic);
                        }
                        r.Add(stack3);
                    }
                    else r.Add(SlotBox(en.Icon, 72, 56));
                    var c = new VisualElement { pickingMode = PickingMode.Ignore };
                    c.style.flexGrow = 1; c.style.flexShrink = 1; c.style.marginLeft = 16; c.style.overflow = Overflow.Hidden;
                    r.Add(c);
                    var nm = Txt(en.Name, "sr-small", c, RequiemUI.Bone); NoWrap(nm);
                    string sub = en.isPassive ? "Always active" : en.set != null ? $"{en.set.Count} mantras  -  takes every socket" : $"{en.skill.Kind}  -  {en.skill.cooldown:0.#}s";
                    var sl = Txt(sub, "sr-tiny", c, InvMuted, 16); NoWrap(sl); sl.style.marginTop = 2;
                    string badge = null;
                    if (loadout != null && !en.isPassive)
                    {
                        if (en.set != null) { if (en.set.All(m => Array.IndexOf(loadout.slots, m) >= 0)) badge = "SET"; }
                        else { int at = Array.IndexOf(loadout.slots, en.skill); if (at >= 0) badge = SlotKey(at); }
                    }
                    if (badge != null) { var kb = KeyCap(badge, r); kb.style.marginLeft = 10; kb.style.borderLeftColor = kb.style.borderRightColor = kb.style.borderTopColor = kb.style.borderBottomColor = RequiemUI.Gold; }
                    list.Add(r);
                    rows.Add(new KeyValuePair<RestEntry, VisualElement>(en, r));
                    string id = en.Id; var rv = r;
                    if (id == restSel) focusRow = r;
                    r.RegisterCallback<PointerEnterEvent>(_ => rv.Focus());
                    r.RegisterCallback<FocusInEvent>(_ => { if (restSel != id) select(id); });
                    if (!en.isPassive && loadout != null)
                        r.AddManipulator(new DragToSlot(() => en.Icon, () => { rv.Focus(); if (restSel != id) select(id); },
                            to => { int j = sockEls.IndexOf(to); if (j >= 0) attuneAt(en, j); },
                            () => sockEls, () => p, mark));                                  // drag a skill onto a slot
                    else r.AddManipulator(new Clickable(() => { rv.Focus(); if (restSel != id) select(id); }));
                    r.RegisterCallback<NavigationSubmitEvent>(_ => attune(en));
                }
                select(shown.Count > 0 ? restSel : "");
                var f = focusRow ?? focusSock;
                if (f != null) f.schedule.Execute(() => f.Focus());
            };
            invCycle = d =>
            {
                int k = Array.IndexOf(RestTabs, restTab);
                restTab = RestTabs[(k + d + RestTabs.Length) % RestTabs.Length];
                rebuild();
            };
            rebuild();
            restPanel = Center(p);
            return restPanel;
        }

        /// <summary>Detail card: header (large icon, name, kind, cooldown, equip buttons) | EFFECT | STATS; skill sets: each skill in the set.</summary>
        private void RestDetail(VisualElement card, RestEntry e, Action<RestEntry> attune, Action<RestEntry, int> attuneAt)
        {
            card.style.flexDirection = FlexDirection.Column;
            var top = Row(card); top.style.alignItems = Align.Center; top.style.flexShrink = 0; Pad(top, 36, 28);
            top.style.borderBottomWidth = 1; top.style.borderBottomColor = InvLine;
            top.Add(SlotBox(e.Icon, 128, 96));
            var tc = new VisualElement(); tc.style.marginLeft = 28; tc.style.flexGrow = 1; tc.style.flexShrink = 1; top.Add(tc);
            Txt(e.Name, "sr-title", tc, RequiemUI.Bone, 36);
            string kind = e.isPassive ? "PASSIVE GIFT  -  always active, takes no socket"
                : e.set != null ? $"MANTRA SET  -  {e.set.Count} mantras, takes every socket" : (e.skill.Kind.ToUpperInvariant() + $"  -  COOLDOWN {e.skill.cooldown:0.#}s");
            var kl = Txt(kind, "sr-tiny", tc, RequiemUI.Gold, 17); kl.style.letterSpacing = 2; kl.style.marginTop = 4;
            var act = Row(tc); act.style.marginTop = 14; act.style.height = 50; act.style.alignItems = Align.Center;
            if (!e.isPassive && loadout != null)
            {
                bool on;
                if (e.set != null) on = e.set.All(m => Array.IndexOf(loadout.slots, m) >= 0);
                else on = loadout.SkillAt(selSlot) == e.skill;
                if (e.set != null)
                {
                    if (on) Chip("ATTUNED  -  ALL SOCKETS", RequiemUI.Gold, act);
                    else act.Add(ActionButton("Attune set", "Enter", () => attune(e)));
                }
                else
                {
                    // EQUIP TO [F] [R] [C] — each button shows the skill currently in that slot; the slot already holding this skill has a gold outline.
                    var lbl = Txt("EQUIP TO", "sr-tiny", act, InvMuted, 16); lbl.style.marginRight = 12; lbl.style.letterSpacing = 2;
                    for (int i = 0; i < PlayerLoadout.SlotCount; i++)
                    {
                        int slot = i;
                        var occ = loadout.SkillAt(i);
                        bool here = occ == e.skill;
                        var b = new VisualElement();
                        b.style.flexDirection = FlexDirection.Row; b.style.alignItems = Align.Center; b.style.height = 44; b.style.marginRight = 10;
                        Pad(b, 10, 4); Border(b, 2, here ? RequiemUI.Gold : InvFrame);
                        var idle = here ? new Color(0.22f, 0.16f, 0.09f, 1f) : new Color(0.15f, 0.11f, 0.062f, 1f);
                        b.style.backgroundColor = idle;
                        KeyCap(SlotKey(i), b);
                        var occIcon = new VisualElement { pickingMode = PickingMode.Ignore };
                        occIcon.style.width = 28; occIcon.style.height = 28; occIcon.style.marginLeft = 8;
                        if (occ != null && occ.Icon != null) occIcon.style.backgroundImage = new StyleBackground(occ.Icon);
                        else { occIcon.style.opacity = 0.35f; var ei = RequiemUI.Tex("ic_slot_empty"); if (ei != null) occIcon.style.backgroundImage = new StyleBackground(ei); }
                        b.Add(occIcon);
                        if (here) { var tk = Txt("ON", "sr-tiny", b, RequiemUI.Gold, 15); tk.style.marginLeft = 8; }
                        if (!here)
                        {
                            b.AddManipulator(new Clickable(() => attuneAt(e, slot)));
                            b.RegisterCallback<PointerEnterEvent>(_ => { Border(b, 2, RequiemUI.Gold); b.style.backgroundColor = new Color(0.22f, 0.16f, 0.09f, 1f); });
                            b.RegisterCallback<PointerLeaveEvent>(_ => { Border(b, 2, InvFrame); b.style.backgroundColor = idle; });
                        }
                        act.Add(b);
                    }
                }
            }
            else if (e.isPassive) Chip("ALWAYS ACTIVE", RequiemUI.Gold, act);

            var sc = Reader(card);
            sc.contentContainer.style.paddingLeft = sc.contentContainer.style.paddingRight = 36;
            sc.contentContainer.style.paddingTop = 24; sc.contentContainer.style.paddingBottom = 28;
            if (e.isPassive)
            {
                Header("EFFECT", sc);
                var d = Txt("", "sr-card-desc", sc, InvBody, 21); d.enableRichText = true; d.text = Rich(e.passive.description);
                return;
            }
            if (e.set == null) { SkillBlock(sc, e.skill, false); return; }
            var lead = PlayerLoadout.SetLeader(e.set[0]);
            string lore = lead != null && !string.IsNullOrEmpty(lead.setDescription) ? lead.setDescription : "";
            if (lore.Length > 0)
            {
                Header("THE SET", sc);
                Txt(lore, "sr-card-desc", sc, InvBody, 20).style.marginBottom = 10;
            }
            foreach (var m in e.set)
            {
                Hairline(sc, 14, 18);
                SkillBlock(sc, m, true);
            }
        }

        /// <summary>One skill: (in a set: key + icon + name) | EFFECT (description) | STATS (label / value table).</summary>
        private void SkillBlock(VisualElement parent, SkillDefinition s, bool member)
        {
            if (member)
            {
                var h = Row(parent); h.style.alignItems = Align.Center; h.style.marginBottom = 12;
                if (s.setSlot >= 0 && s.setSlot < PlayerLoadout.SlotCount) KeyCap(SlotKey(s.setSlot), h).style.marginRight = 14;
                h.Add(SlotBox(s.Icon, 60, 46));
                var hc = new VisualElement(); hc.style.marginLeft = 14; h.Add(hc);
                Txt(s.displayName, "sr-small", hc, RequiemUI.Bone, 26);
                var k = Txt($"{s.Kind.ToUpperInvariant()}  -  COOLDOWN {s.cooldown:0.#}s", "sr-tiny", hc, RequiemUI.Gold, 15); k.style.letterSpacing = 2;
            }
            var cols = Row(parent); cols.style.alignItems = Align.FlexStart;
            var c1 = new VisualElement(); c1.style.flexGrow = 1.25f; c1.style.flexBasis = 0; c1.style.paddingRight = 28; cols.Add(c1);
            var c2 = new VisualElement(); c2.style.flexGrow = 1f; c2.style.flexBasis = 0; c2.style.paddingLeft = 28;
            c2.style.borderLeftWidth = 1; c2.style.borderLeftColor = InvLine; cols.Add(c2);
            Header("EFFECT", c1);
            var d = Txt("", "sr-card-desc", c1, InvBody, 21); d.enableRichText = true; d.text = Rich(s.description);
            Header("DETAILS", c2);
            var st = new List<KeyValuePair<string, string>>();
            st.Add(new KeyValuePair<string, string>("Cooldown", $"{s.cooldown:0.#}s"));
            s.Stats(st);
            foreach (var kv in st)
            {
                var r = Row(c2); r.style.minHeight = 30; r.style.alignItems = Align.Center; r.style.flexShrink = 0;
                r.style.borderBottomWidth = 1; r.style.borderBottomColor = new Color(InvLine.r, InvLine.g, InvLine.b, 0.45f);
                var l = Txt(kv.Key, "sr-tiny", r, InvMuted, 17); l.style.width = 170; l.style.flexShrink = 0;
                var v = Txt("", "sr-tiny", r, RequiemUI.Bone, 17); v.enableRichText = true; v.text = Rich(kv.Value);
                v.style.flexGrow = 1; v.style.whiteSpace = WhiteSpace.Normal; v.style.unityTextAlign = TextAnchor.MiddleRight;
            }
        }
    }
}
