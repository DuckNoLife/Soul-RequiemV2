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
    /// INVENTORY (B): 3 sections Weapons / Key Items / Notes (Q / E switch section), list on the left + detail card on the right.
    /// Items come from WeaponRing (katanas) and KeyRing (keys, tools, notes). Frame images: Resources/UI/Requiem/inv/*.
    /// </summary>
    public partial class RequiemMenus
    {
        // ------------------------------------------------------------------ INVENTORY
        //   • Separate sections (Q / E or LB / RB to switch; empty sections are dimmed but keep their place -> the section bar never shifts). No "All" tab.
        //   • Left: a LIST (icon + name + one sub-line) — names readable without hovering. Right: the detail card.
        //   • One palette: near-black ink background, card face one step darker, thin bronze lines; GOLD only for the selection / titles / numbers.
        //   • Long text scrolls with the mouse wheel / right stick, NO scroll bar — only a faint strip at the bottom saying "more below".
        //   • Footer = clickable key hints (keycaps): [Enter] Draw this blade · [B] Close.
        private VisualElement inventoryPanel;
        private string invSel = "", invTab = "Weapon";
        private Action<int> invCycle;
        private ScrollView invReader;
        // 3 sections — keys + puzzle tools merged into KEY ITEMS
        private static readonly string[] InvTabs = { "Weapon", "KeyItem", "Document" };
        private static string TabLabel(string t) => t == "KeyItem" ? "Key Items" : t == "Document" ? "Notes" : "Weapons";
        private static string KindLabel(string k) => k == "Weapon" ? "Katana" : k == "Document" ? "Note" : k;
        private static string EmptyText(string t) => t == "KeyItem" ? "Nothing that opens or works anything yet." : t == "Document" ? "Nothing written down yet." : "No blades.";

        private const float InvW = 1840f, InvH = 1000f, ListW = 460f;
        private static readonly Color InvBg = new Color(0.050f, 0.036f, 0.064f, 0.97f);
        private static readonly Color InvSurface = new Color(0.074f, 0.054f, 0.092f, 1f);
        private static readonly Color InvRowSel = new Color(0.135f, 0.098f, 0.066f, 1f);
        private static readonly Color InvLine = new Color(0.27f, 0.155f, 0.135f, 1f);    // dark red bronze
        private static readonly Color InvFrame = new Color(0.47f, 0.286f, 0.224f, 1f);   // red bronze
        private const string ArtFrame = "inv/inv_frame", ArtDivider = "inv/inv_divider", ArtSlot = "inv/inv_slot";
        private static string CatIcon(string tab) => "inv/cat_" + (tab == "Weapon" ? "weapon" : tab == "KeyItem" ? "key" : "note");
        /// <summary>Use the UI image when present (9-slice), otherwise draw flat colour — the layout never breaks before art is imported.</summary>
        private static bool Art(VisualElement v, string tex, int l, int t, int r, int b)
        {
            var tx = RequiemUI.Tex(tex);
            if (tx == null) return false;
            v.style.backgroundImage = new StyleBackground(tx);
            v.style.unitySliceLeft = l; v.style.unitySliceTop = t; v.style.unitySliceRight = r; v.style.unitySliceBottom = b;
            return true;
        }
        private static readonly Color InvMuted = new Color(0.62f, 0.58f, 0.51f);
        private static readonly Color InvFaint = new Color(0.38f, 0.35f, 0.31f);
        private static readonly Color InvBody = new Color(0.80f, 0.75f, 0.65f);
        private static readonly Color InvGood = new Color(0.56f, 0.86f, 0.50f), InvBad = new Color(0.93f, 0.46f, 0.40f);
        private static readonly Color Clear0 = new Color(0f, 0f, 0f, 0f);

        private sealed class InvEntry
        {
            public KeyItem key;
            public WeaponDefinition weapon;
            public string Id => weapon != null ? weapon.id : key.id;
            public string Kind => weapon != null ? "Weapon" : key.kind == "Document" ? "Document" : "KeyItem";
            public string Name => weapon != null ? weapon.displayName : key.name;
            public Texture2D Icon => weapon != null
                ? RequiemUI.Tex("weapon_" + weapon.id)
                : KeyIcon(key);
            public string Sub
            {
                get
                {
                    if (weapon != null)
                        return weapon.trait != null && weapon.trait.Any
                            ? "Innate - " + (string.IsNullOrEmpty(weapon.trait.title) ? weapon.trait.kind.ToString() : weapon.trait.title) : "Katana";
                    return key.kind == "Document" ? "" : (key.opens ?? "");
                }
            }
        }

        /// <summary>Item image in the inventory. When Tiled has no `key_icon` (or names a missing image) the slot would be empty,
        /// so a default image per kind is used: documents = item_note, tools = item_crank, keys / seals = key_seal.</summary>
        private static Texture2D KeyIcon(KeyItem k)
        {
            if (k == null) return null;
            var t = string.IsNullOrEmpty(k.icon) ? null : RequiemUI.Tex(k.icon);
            if (t != null) return t;
            string kind = k.kind ?? "";
            return RequiemUI.Tex(kind == "Document" || !string.IsNullOrEmpty(k.text) ? "item_note" : kind == "Tool" ? "item_crank" : "key_seal");
        }

        /// <summary>Q / E (LB / RB) switch section, right stick scrolls text — called every frame while the inventory is open.</summary>
        private void InvTick()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            var gp = UnityEngine.InputSystem.Gamepad.current;
            int d = 0;
            if (kb != null) { if (kb.qKey.wasPressedThisFrame) d = -1; else if (kb.eKey.wasPressedThisFrame) d = 1; }
            if (gp != null) { if (gp.leftShoulder.wasPressedThisFrame) d = -1; else if (gp.rightShoulder.wasPressedThisFrame) d = 1; }
            if (d != 0 && invCycle != null) { invCycle(d); SoulRequiem.Core.Sfx.Play2D("cs_page", 0.6f); }   // switching section = page turn
            if (invReader != null && gp != null)
            {
                float y = gp.rightStick.ReadValue().y;
                if (Mathf.Abs(y) > 0.2f) invReader.scrollOffset += new Vector2(0f, -y * 900f * Time.unscaledDeltaTime);
            }
        }

        private static VisualElement ListRow(InvEntry it, bool equipped, bool used)
        {
            var r = new VisualElement { focusable = true, tabIndex = 0 };
            r.style.flexDirection = FlexDirection.Row; r.style.alignItems = Align.Center; r.style.flexShrink = 0;
            r.style.height = 84; r.style.marginBottom = 4; r.style.paddingLeft = 12; r.style.paddingRight = 14;
            r.style.borderLeftWidth = 4; r.style.borderLeftColor = Clear0;
            var box = new VisualElement { pickingMode = PickingMode.Ignore };
            box.style.width = 72; box.style.height = 72; box.style.flexShrink = 0;
            box.style.alignItems = Align.Center; box.style.justifyContent = Justify.Center;
            if (!Art(box, ArtSlot, 16, 16, 16, 16)) { box.style.backgroundColor = new Color(0.028f, 0.02f, 0.036f, 1f); Border(box, 1, InvLine); }
            var ic = new VisualElement { pickingMode = PickingMode.Ignore };
            ic.style.width = 56; ic.style.height = 56;
            var tex = it.Icon;
            if (tex != null) ic.style.backgroundImage = new StyleBackground(tex);
            if (used) ic.style.opacity = 0.45f;
            box.Add(ic); r.Add(box);
            var col = new VisualElement { pickingMode = PickingMode.Ignore };
            col.style.flexGrow = 1; col.style.flexShrink = 1; col.style.marginLeft = 16; col.style.overflow = Overflow.Hidden;
            r.Add(col);
            var nm = Txt(it.Name, "sr-small", col, used ? InvMuted : RequiemUI.Bone); nm.pickingMode = PickingMode.Ignore; NoWrap(nm);
            string sub = used ? "Used" : it.Sub;
            if (!string.IsNullOrEmpty(sub))
            {
                var sl = Txt(sub, "sr-tiny", col, InvMuted, 17); sl.pickingMode = PickingMode.Ignore; NoWrap(sl); sl.style.marginTop = 2;
            }
            if (equipped)
            {
                var e = Txt("EQUIPPED", "sr-tiny", r, RequiemUI.Gold, 15);
                e.style.letterSpacing = 2; e.style.marginLeft = 10; e.style.flexShrink = 0; e.pickingMode = PickingMode.Ignore;
            }
            return r;
        }
        private static void StyleRow(VisualElement r, bool sel)
        {
            r.style.backgroundColor = sel ? InvRowSel : Clear0;
            r.style.borderLeftColor = sel ? RequiemUI.Gold : Clear0;
        }

        private VisualElement BuildInventory()
        {
            pushSfx = "bag_open"; // sound — Push plays a leather bag sound instead of the book sound
            InventoryOpens++;                                       // the HUD clears the "new item" dot
            var dir = MapEncounterDirector.Instance;
            var flags = dir != null ? dir.Flags : null;
            if (Array.IndexOf(InvTabs, invTab) < 0) invTab = "Weapon";

            var p = new VisualElement();
            p.style.width = InvW; p.style.maxWidth = Length.Percent(96);
            p.style.height = InvH; p.style.maxHeight = Length.Percent(96);
            if (!Art(p, ArtFrame, 44, 44, 44, 44)) { p.style.backgroundColor = InvBg; Border(p, 2, InvFrame); }
            Pad(p, 58, 46);

            // header: title on the left | sections on the right (Q ‹ … › E)
            var head = Row(p); head.style.alignItems = Align.Center; head.style.flexShrink = 0;
            var title = Txt("INVENTORY", "sr-title", head, RequiemUI.Gold, 40); title.style.flexGrow = 1; title.style.letterSpacing = 3;
            var tabsWrap = Row(head); tabsWrap.style.alignItems = Align.Center;
            KeyCap("Q", tabsWrap).style.marginRight = 12;
            var tabs = Row(tabsWrap);
            KeyCap("E", tabsWrap).style.marginLeft = 12;
            var rule = new VisualElement(); rule.style.flexShrink = 0; rule.style.marginTop = 12;
            if (Art(rule, ArtDivider, 44, 0, 44, 0)) rule.style.height = 36;
            else { rule.style.height = 1; rule.style.backgroundColor = InvLine; }
            p.Add(rule);

            var body = new VisualElement(); body.style.flexGrow = 1; body.style.flexShrink = 1; body.style.marginTop = 22;
            p.Add(body);
            VisualElement card = null;

            var foot = Row(p); foot.style.flexShrink = 0; foot.style.marginTop = 18;
            foot.style.justifyContent = Justify.FlexEnd; foot.style.alignItems = Align.Center; foot.style.height = 40;

            var shown = new List<InvEntry>();
            var items = new List<KeyValuePair<InvEntry, Action<bool>>>();      // item + how "selected" is drawn
            Action rebuild = null;
            Action<string> select = null;
            Action<WeaponDefinition> equip = w =>
            {
                var kit = hp != null ? hp.GetComponent<PlayerCombat>() : null;
                if (kit == null || w == null || kit.Weapon == w) return;
                kit.Equip(w);
                // no extra message — the visuals already show the change (sword panel updated)
                rebuild();
            };

            select = id =>
            {
                invSel = id;
                foreach (var kv in items) kv.Value(kv.Key.Id == id);
                var cur = shown.FirstOrDefault(k => k.Id == id);
                card.Clear(); card.style.flexDirection = FlexDirection.Column; invReader = null;
                foot.Clear();
                bool canEquip = cur != null && cur.weapon != null && EquippedWeapon() != cur.weapon;
                if (canEquip) { var cw = cur.weapon; Prompt("Enter", "Draw this blade", foot, () => equip(cw)); }
                Prompt(GameInput.Label(GameAction.Inventory), "Close", foot, Pop);
                if (cur == null)
                {
                    var e = Txt(EmptyText(invTab), "sr-small", card, InvFaint);
                    e.style.alignSelf = Align.Center; e.style.marginTop = 160;
                    return;
                }
                if (cur.weapon != null) WeaponDetail(card, cur.weapon, equip);
                else if (cur.Kind == "Document") NoteCard(card, cur.key);
                else KeyCard(card, cur.key, flags);
            };

            rebuild = () =>
            {
                var owned = new List<InvEntry>();
                foreach (var w in WeaponRing.Owned(flags)) owned.Add(new InvEntry { weapon = w });
                if (flags != null) foreach (var k in KeyRing.Owned(flags)) owned.Add(new InvEntry { key = k });

                tabs.Clear();
                foreach (var t in InvTabs)
                {
                    string tab = t;
                    tabs.Add(Tab(TabLabel(tab), CatIcon(tab), owned.Count(k => k.Kind == tab), invTab == tab, () => { invTab = tab; rebuild(); }));
                }
                shown.Clear();
                shown.AddRange(owned.Where(k => k.Kind == invTab));
                if (shown.Count > 0 && shown.All(k => k.Id != invSel)) invSel = shown[0].Id;

                body.Clear(); items.Clear();
                bool rack = invTab == "Weapon";
                body.style.flexDirection = rack ? FlexDirection.Column : FlexDirection.Row;
                // sword rack = pages of 3 slots. More than 3 swords -> arrows + page dots; selecting a sword on another page turns the page.
                const float Gap = 24f; const int PerPage = 3;
                VisualElement list;
                var cards = new List<VisualElement>();
                int page = 0, pages = Mathf.Max(1, Mathf.CeilToInt(shown.Count / (float)PerPage));
                float cardW = 0f;
                Action<int> setPage = null;
                if (rack)
                {
                    var rackRoot = new VisualElement(); rackRoot.style.flexShrink = 0;
                    body.Add(rackRoot);
                    var line = Row(rackRoot); line.style.height = 276; line.style.flexShrink = 0;
                    bool paged = shown.Count > PerPage;
                    VisualElement chevL = null, chevR = null, dots = null;
                    if (paged) chevL = Chevron(line, true, () => setPage(page - 1));
                    var view = new VisualElement(); view.style.flexGrow = 1; view.style.overflow = Overflow.Hidden;
                    line.Add(view);
                    if (paged) chevR = Chevron(line, false, () => setPage(page + 1));
                    var strip = new VisualElement();
                    strip.style.position = Position.Absolute; strip.style.top = 0; strip.style.bottom = 0; strip.style.left = 0;
                    strip.style.flexDirection = FlexDirection.Row;
                    strip.style.transitionProperty = new List<StylePropertyName> { new StylePropertyName("left") };
                    strip.style.transitionDuration = new List<TimeValue> { new TimeValue(0.22f, TimeUnit.Second) };
                    strip.style.transitionTimingFunction = new List<EasingFunction> { new EasingFunction(EasingMode.EaseOutCubic) };
                    view.Add(strip);
                    if (paged) { dots = Row(rackRoot); dots.style.justifyContent = Justify.Center; dots.style.marginTop = 12; dots.style.flexShrink = 0; }
                    setPage = pg =>
                    {
                        page = Mathf.Clamp(pg, 0, pages - 1);
                        if (chevL != null) chevL.style.opacity = page > 0 ? 1f : 0.2f;
                        if (chevR != null) chevR.style.opacity = page < pages - 1 ? 1f : 0.2f;
                        if (dots != null)
                        {
                            dots.Clear();
                            for (int k = 0; k < pages; k++)
                            {
                                var dt = new VisualElement();
                                dt.style.width = k == page ? 26 : 10; dt.style.height = 6; dt.style.marginLeft = dt.style.marginRight = 4;
                                dt.style.backgroundColor = k == page ? RequiemUI.Gold : InvLine;
                                dots.Add(dt);
                            }
                        }
                        float vw = view.layout.width;
                        if (float.IsNaN(vw) || vw <= 0f || cardW <= 0f) return;
                        int n = shown.Count;
                        float total = n * cardW + Mathf.Max(0, n - 1) * Gap;
                        // the last page always has 3 slots (shifted back so it is not empty); 1-2 swords are centred
                        int first = Mathf.Clamp(page * PerPage, 0, Mathf.Max(0, n - PerPage));
                        strip.style.left = n <= PerPage ? (vw - total) / 2f : -first * (cardW + Gap);
                    };
                    view.RegisterCallback<GeometryChangedEvent>(e =>
                    {
                        cardW = Mathf.Floor((e.newRect.width - (PerPage - 1) * Gap) / PerPage);
                        foreach (var c in cards) c.style.width = cardW;
                        setPage(page);
                    });
                    view.RegisterCallback<WheelEvent>(e =>
                    {
                        if (!paged) return;
                        setPage(page + (e.delta.y > 0f ? 1 : -1));
                        e.StopPropagation();
                    });
                    list = strip;
                }
                else
                {
                    list = new VisualElement(); list.style.flexShrink = 0; list.style.width = ListW;
                    body.Add(list);
                }
                card = new VisualElement();
                card.style.flexGrow = 1; card.style.flexShrink = 1; card.style.flexBasis = 0;
                if (rack) card.style.marginTop = 20; else card.style.marginLeft = 28;
                card.style.backgroundColor = InvSurface; Border(card, 1, InvLine); card.style.overflow = Overflow.Hidden;
                body.Add(card);

                var eqW = EquippedWeapon();
                VisualElement focusMe = null;
                int selIndex = 0;
                for (int i = 0; i < shown.Count; i++)
                {
                    var it = shown[i];
                    VisualElement el; Action<bool> sty;
                    if (rack)
                    {
                        var rw = it.weapon;
                        el = RackCard(rw, rw == eqW, () => equip(rw), out sty);
                        el.RegisterCallback<ClickEvent>(e => { if (e.clickCount >= 2) equip(rw); });   // double click = equip
                        el.style.flexGrow = 0; el.style.flexShrink = 0; el.style.flexBasis = StyleKeyword.Auto;
                        if (cardW > 0f) el.style.width = cardW;
                        if (i < shown.Count - 1) el.style.marginRight = Gap;
                        cards.Add(el);
                        int pi = i / PerPage;
                        el.RegisterCallback<FocusInEvent>(_ => { if (pi != page) setPage(pi); });   // key / gamepad to the next page
                    }
                    else
                    {
                        bool used = it.key != null && KeyRing.Used(flags, it.key.id);
                        el = ListRow(it, false, used);
                        var er = el; sty = on => StyleRow(er, on);
                    }
                    list.Add(el);
                    items.Add(new KeyValuePair<InvEntry, Action<bool>>(it, sty));
                    string id = it.Id; var entry = it; var rv = el;
                    if (id == invSel) { focusMe = el; selIndex = i; }
                    el.RegisterCallback<PointerEnterEvent>(_ => rv.Focus());                  // hover = preview
                    el.RegisterCallback<FocusInEvent>(_ => { if (invSel != id) select(id); });
                    el.AddManipulator(new Clickable(() => { rv.Focus(); if (invSel != id) select(id); }));
                    el.RegisterCallback<NavigationSubmitEvent>(_ => { if (entry.weapon != null) equip(entry.weapon); });
                }
                if (setPage != null) setPage(selIndex / PerPage);                             // opening the inventory: the page holding the selected sword
                select(shown.Count > 0 ? invSel : "");
                if (focusMe != null) { var f = focusMe; f.schedule.Execute(() => f.Focus()); }
            };
            invCycle = d =>
            {
                int k = Array.IndexOf(InvTabs, invTab);
                invTab = InvTabs[(k + d + InvTabs.Length) % InvTabs.Length];
                rebuild();
            };
            rebuild();

            inventoryPanel = Center(p);
            return inventoryPanel;
        }

        private WeaponDefinition EquippedWeapon()
        {
            var kit = hp != null ? hp.GetComponent<PlayerCombat>() : null;
            return kit != null ? kit.Weapon : null;
        }

        // ------------------------------------------------------------------ key / tool card
        private void KeyCard(VisualElement card, KeyItem cur, WorldFlags flags)
        {
            var top = Row(card); top.style.alignItems = Align.Center; top.style.flexShrink = 0; Pad(top, 40, 30);
            var box = new VisualElement { pickingMode = PickingMode.Ignore };
            box.style.width = 140; box.style.height = 140; box.style.flexShrink = 0;
            box.style.alignItems = Align.Center; box.style.justifyContent = Justify.Center;
            box.style.backgroundColor = new Color(0.028f, 0.02f, 0.036f, 1f); Border(box, 1, InvLine);
            var big = new VisualElement { pickingMode = PickingMode.Ignore };
            big.style.width = 112; big.style.height = 112;
            var bt = KeyIcon(cur);   // default image when missing
            if (bt != null) big.style.backgroundImage = new StyleBackground(bt);
            box.Add(big); top.Add(box);
            var col = new VisualElement(); col.style.flexGrow = 1; col.style.flexShrink = 1; col.style.marginLeft = 28;
            top.Add(col);
            var nm = Txt(cur.name, "sr-title", col, RequiemUI.Bone, 38); nm.style.whiteSpace = WhiteSpace.Normal;
            bool used = KeyRing.Used(flags, cur.id);
            Chip((KindLabel(cur.kind) + (used ? "  -  USED" : "")).ToUpperInvariant(), used ? InvMuted : RequiemUI.Gold, col).style.marginTop = 10;

            var inner = new VisualElement(); inner.style.flexGrow = 1; inner.style.flexShrink = 1; Pad(inner, 40, 0);
            inner.style.paddingBottom = 28;
            card.Add(inner);
            Hairline(inner, 0, 22);
            var sc = Reader(inner);
            sc.style.maxWidth = 860;
            if (!string.IsNullOrEmpty(cur.opens))
            {
                Header("OPENS", sc);
                Txt(cur.opens, "sr-card-desc", sc, RequiemUI.Bone).style.marginBottom = 18;
            }
            if (!string.IsNullOrEmpty(cur.desc)) Txt(cur.desc, "sr-card-desc", sc, InvBody);
            if (cur.stats.Count > 0)
            {
                Header("PROPERTIES", sc, 20);
                foreach (var kv in cur.stats)
                {
                    var r = Row(sc); r.style.justifyContent = Justify.SpaceBetween; r.style.marginBottom = 2;
                    Txt(kv.Key, "sr-card-desc", r, InvMuted);
                    Txt(kv.Value, "sr-card-desc", r, RequiemUI.Gold);
                }
            }
            if (!string.IsNullOrEmpty(cur.text))
            {
                Header("WRITTEN ON IT", sc, 20);
                Txt(cur.text, "sr-card-desc", sc, InvBody);
            }
        }

        // ------------------------------------------------------------------ note card: reading page (no big icon — every sheet of paper looks the same)
        private void NoteCard(VisualElement card, KeyItem cur)
        {
            var inner = new VisualElement(); inner.style.flexGrow = 1; inner.style.flexShrink = 1;
            Pad(inner, 56, 40);
            card.Add(inner);
            Txt("NOTE", "sr-tiny", inner, InvMuted, 16).style.letterSpacing = 3;
            var nm = Txt(cur.name, "sr-title", inner, RequiemUI.Bone, 38); nm.style.whiteSpace = WhiteSpace.Normal; nm.style.marginTop = 4;
            if (!string.IsNullOrEmpty(cur.desc))
            {
                var dsc = Txt(cur.desc, "sr-tiny", inner, RequiemUI.Gold); dsc.style.whiteSpace = WhiteSpace.Normal; dsc.style.marginTop = 6;
            }
            Hairline(inner, 20, 24);
            var sc = Reader(inner);
            sc.style.maxWidth = 860;
            string body = !string.IsNullOrEmpty(cur.text) ? cur.text : cur.desc;
            if (!string.IsNullOrEmpty(body)) Txt(body, "sr-card-desc", sc, InvBody, 22);
            if (!string.IsNullOrEmpty(cur.opens)) Txt("Concerns: " + cur.opens, "sr-tiny", sc, InvFaint, 17).style.marginTop = 16;
        }
    }
}
