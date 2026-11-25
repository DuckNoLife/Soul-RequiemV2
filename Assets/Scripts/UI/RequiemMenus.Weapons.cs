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
    /// SWORD RACK in the inventory: a card per katana (stats compared with the equipped sword, passive, special effects), equip button.
    /// </summary>
    public partial class RequiemMenus
    {
        // ------------------------------------------------------------------ SWORD RACK + stats panel
        /// <summary>Colour the numbers in a passive rule gold (+15%, 1.2s ...). Clean the text FIRST, then add colour tags (Clean strips &lt; &gt; #).</summary>
        private static string Rich(string s)
        {
            s = TextUtil.Clean(s ?? "");
            return System.Text.RegularExpressions.Regex.Replace(s, @"(\+?\d+(?:\.\d+)?%|\d+(?:\.\d+)?s\b)", "<color=#EDC770>$1</color>");
        }

        private static string TraitTitle(WeaponDefinition w) =>
            w.trait == null || !w.trait.Any ? "" : string.IsNullOrEmpty(w.trait.title) ? w.trait.kind.ToString() : w.trait.title;

        /// <summary>Sword rack page arrow (drawn with 2 border edges of a square rotated 45°, no glyphs).</summary>
        private static VisualElement Chevron(VisualElement parent, bool left, Action click)
        {
            var b = new VisualElement();
            b.style.width = 44; b.style.flexShrink = 0; b.style.alignItems = Align.Center; b.style.justifyContent = Justify.Center;
            if (left) b.style.marginRight = 12; else b.style.marginLeft = 12;
            var a = new VisualElement { pickingMode = PickingMode.Ignore };
            a.style.width = 16; a.style.height = 16;
            if (left) { a.style.borderLeftWidth = 3; a.style.borderBottomWidth = 3; }
            else { a.style.borderRightWidth = 3; a.style.borderTopWidth = 3; }
            a.style.borderLeftColor = a.style.borderBottomColor = a.style.borderRightColor = a.style.borderTopColor = RequiemUI.Gold;
            a.style.rotate = new Rotate(new Angle(45f, AngleUnit.Degree));
            b.Add(a);
            b.AddManipulator(new Clickable(click));
            b.RegisterCallback<PointerEnterEvent>(_ => b.style.backgroundColor = InvRowSel);
            b.RegisterCallback<PointerLeaveEvent>(_ => b.style.backgroundColor = Clear0);
            parent.Add(b);
            return b;
        }

        /// <summary>One sword rack slot: showcase image (sword + scabbard) + name + passive. Selected = gold outline, other slots' images dim.</summary>
        /// <param name="onEquip"> the selected slot (not equipped) shows an EQUIP button right under the sword image — one click equips, no Enter needed.</param>
        private static VisualElement RackCard(WeaponDefinition w, bool equipped, Action onEquip, out Action<bool> style)
        {
            var c = new VisualElement { focusable = true, tabIndex = 0 };
            c.style.flexGrow = 1; c.style.flexShrink = 1; c.style.flexBasis = 0; c.style.height = 276;
            c.style.alignItems = Align.Center; c.style.overflow = Overflow.Hidden;
            var tint = new Color(w.trailColor.r, w.trailColor.g, w.trailColor.b, 1f);
            var art = new VisualElement { pickingMode = PickingMode.Ignore };
            art.style.alignSelf = Align.Stretch; art.style.height = 200; art.style.flexShrink = 0;
            art.style.alignItems = Align.Center; art.style.justifyContent = Justify.Center;
            art.style.backgroundColor = Color.Lerp(new Color(0.025f, 0.018f, 0.032f, 1f), tint, 0.10f);
            art.style.borderBottomWidth = 1; art.style.borderBottomColor = InvLine;
            var pic = new VisualElement { pickingMode = PickingMode.Ignore };
            pic.style.width = 512; pic.style.height = 192;                        // 256x96 x2
            var tex = w.showcase != null ? w.showcase.texture : null;
            if (tex != null) { tex.filterMode = FilterMode.Point; pic.style.backgroundImage = new StyleBackground(tex); }
            art.Add(pic);
            c.Add(art);
            // narrow screens (slot < 540): image x1.5 so it does not overflow
            c.RegisterCallback<GeometryChangedEvent>(e =>
            {
                bool big = e.newRect.width >= 520f;
                pic.style.width = big ? 512 : 384; pic.style.height = big ? 192 : 144;
            });
            var nm = Txt(w.displayName, "sr-small", c, RequiemUI.Bone, 24); nm.style.marginTop = 12;
            string tt = TraitTitle(w);
            var sub = Txt(tt.Length > 0 ? "Innate - " + tt : "Katana", "sr-tiny", c, InvMuted, 17); sub.style.marginTop = 2;
            if (equipped)
            {
                var chip = Chip("EQUIPPED", RequiemUI.Gold, c);
                chip.style.position = Position.Absolute; chip.style.top = 10; chip.style.right = 10;
                chip.style.backgroundColor = new Color(0.05f, 0.035f, 0.06f, 0.9f);
            }
            // EQUIP button at the bottom centre of the sword image, only on the SELECTED, not-equipped slot
            VisualElement eq = null;
            if (!equipped && onEquip != null)
            {
                eq = new VisualElement { pickingMode = PickingMode.Ignore };                 // centred row at the bottom of the image (image 200 tall)
                eq.style.position = Position.Absolute; eq.style.left = 0; eq.style.right = 0; eq.style.top = 146;
                eq.style.flexDirection = FlexDirection.Row; eq.style.justifyContent = Justify.Center;
                var btn = ActionButton("Equip", "Enter", onEquip);
                btn.focusable = false;
                btn.style.backgroundColor = new Color(0.15f, 0.11f, 0.062f, 0.97f);
                btn.RegisterCallback<PointerDownEvent>(e => e.StopPropagation());
                eq.Add(btn);
                eq.style.display = DisplayStyle.None;
                c.Add(eq);
            }
            style = sel =>
            {
                Border(c, 2, sel ? RequiemUI.Gold : InvLine);
                c.style.backgroundColor = sel ? InvRowSel : InvSurface;
                pic.style.opacity = sel ? 1f : 0.7f;
                nm.style.color = sel ? RequiemUI.Gold : RequiemUI.Bone;
                if (eq != null) eq.style.display = sel ? DisplayStyle.Flex : DisplayStyle.None;
            };
            style(false);
            return c;
        }

        /// <summary>Stat row: label | 10 pips (FIXED scale) | number | difference from the equipped sword.</summary>
        /// <summary>The sword's special effects (passive) as "NAME — number" lines for the SPECIAL panel.
        /// New damage kinds (burn / bleed…) need a new branch here.</summary>
        private static List<KeyValuePair<string, string>> SpecialEffects(WeaponDefinition w)
        {
            var o = new List<KeyValuePair<string, string>>();
            var t = w != null ? w.trait : null;
            if (t == null || !t.Any) return o;
            float dps = WeaponRing.Dps(w);
            string P(float f) => Mathf.RoundToInt(f * 100f) + "%";
            void Add(string k, string v) => o.Add(new KeyValuePair<string, string>(k, v));
            switch (t.kind)
            {
                case WeaponTraitKind.Counterstroke: Add("FIRST CUT AFTER A ROLL", "+" + P(t.bonus)); break;
                case WeaponTraitKind.Vigil: Add("DPS WHILE UNTOUCHED", (dps * (1f + t.bonus)).ToString("0")); break;
                case WeaponTraitKind.BloodOath:
                    Add("LIFESTEAL", P(t.leech) + " of damage");
                    Add("DPS BELOW " + P(t.threshold) + " HEALTH", (dps * (1f + t.bonus)).ToString("0")); break;
                case WeaponTraitKind.Iaijutsu: Add("DRAW CUT", "+" + P(t.bonus) + " to +" + P(Mathf.Max(t.bonus, t.bonusMax)) + ", stun " + t.threshold.ToString("0.#") + "s"); break;   // rises with the hold time
                case WeaponTraitKind.LastStand: Add("DAMAGE TAKEN BELOW " + P(t.threshold), "-" + P(t.bonus)); break;
                case WeaponTraitKind.Hunger:
                    Add("HEAL ON KILL", P(t.leech) + " max health");
                    Add("FLASK HEALING", "-" + P(t.bonus)); break;
                case WeaponTraitKind.Accord:
                    Add("FINISHER", "+" + P(t.bonus));
                    Add("GUARD AFTER FINISHER", "-" + P(t.leech) + " for " + t.window.ToString("0.#") + "s"); break;
                case WeaponTraitKind.Namebound: Add("MANTRA COOLDOWN", "-" + P(t.bonus)); break;
                case WeaponTraitKind.Penance: Add("PER HIT TAKEN", "+" + P(t.bonus) + " (max " + Mathf.Max(1, Mathf.RoundToInt(t.threshold * 10f)) + ")"); break;
            }
            // effect on enemies
            string when = t.statusOn == StatusTrigger.Finisher ? " (finisher)" : t.statusOn == StatusTrigger.WhileActive ? " (while lit)" : "";
            switch (t.status)
            {
                case StatusKind.Bleed: Add("BLEED" + when, t.statusPower.ToString("0.#") + "/s per stack, " + t.statusSeconds.ToString("0.#") + "s, max " + t.statusStacks); break;
                case StatusKind.Burn: Add("HOLY FIRE" + when, t.statusPower.ToString("0.#") + "/s for " + t.statusSeconds.ToString("0.#") + "s"); break;
                case StatusKind.Bind: Add("ROOT BIND" + when, "slow " + P(t.statusPower) + " for " + t.statusSeconds.ToString("0.#") + "s"); break;
                case StatusKind.Chill: Add("FROST" + when, "slow " + P(t.statusPower) + " for " + t.statusSeconds.ToString("0.#") + "s"); break;
                case StatusKind.Mark: Add("BLOOD MARK" + when, t.statusSeconds.ToString("0.#") + "s - marked kills heal"); break;
            }
            return o;
        }

        private static void StatRow(VisualElement parent, string label, float v, float cap, string shown, float? other, string fmt)
        {
            var row = Row(parent); row.style.alignItems = Align.Center; row.style.height = 30; row.style.flexShrink = 0;
            var l = Txt(label, "sr-tiny", row, InvMuted); l.style.width = 136; l.style.flexShrink = 0; l.style.letterSpacing = 1;
            var pips = Row(row); pips.style.flexShrink = 0;
            int n = Mathf.Clamp(Mathf.RoundToInt(v / Mathf.Max(0.0001f, cap) * 10f), 1, 10);
            for (int i = 0; i < 10; i++)
            {
                var pip = new VisualElement();
                pip.style.width = 13; pip.style.height = 10; pip.style.marginRight = 3;
                pip.style.backgroundColor = i < n ? RequiemUI.Gold : new Color(0.19f, 0.15f, 0.12f);
                pips.Add(pip);
            }
            var val = Txt(shown, "sr-tiny", row, RequiemUI.Bone); val.style.width = 84; val.style.flexShrink = 0;
            val.style.unityTextAlign = TextAnchor.MiddleRight; val.style.marginLeft = 6;
            if (!other.HasValue) return;
            float d = v - other.Value;
            if (Mathf.Abs(d) <= 0.004f) return;
            var dl = Txt((d > 0f ? "+" : "-") + Mathf.Abs(d).ToString(fmt), "sr-tiny", row, d > 0f ? InvGood : InvBad);
            dl.style.marginLeft = 12;
        }

        /// <summary>Stats panel under the sword rack, 3 columns: IDENTITY (name, actions, passive) | COMBAT (stats, the three cuts) | THE TELLING.</summary>
        private void WeaponDetail(VisualElement card, WeaponDefinition w, Action<WeaponDefinition> equip)
        {
            var eq = EquippedWeapon();
            bool equipped = eq == w;
            var cmp = !equipped && eq != null ? eq : null;
            card.style.flexDirection = FlexDirection.Row;
            Func<float, bool, VisualElement> col = (grow, sep) =>
            {
                var v = new VisualElement(); v.style.flexGrow = grow; v.style.flexShrink = 1; v.style.flexBasis = 0;
                Pad(v, 32, 26);
                if (sep) { v.style.borderLeftWidth = 1; v.style.borderLeftColor = InvLine; }
                card.Add(v);
                return v;
            };

            // --- 1. identity
            var c1 = col(1f, false);
            Txt(w.displayName, "sr-title", c1, RequiemUI.Bone, 36);
            if (!string.IsNullOrEmpty(w.epithet))
            {
                var ep = Txt(w.epithet, "sr-tiny", c1, RequiemUI.Gold); ep.style.whiteSpace = WhiteSpace.Normal; ep.style.marginTop = 4;
            }
            var act = new VisualElement(); act.style.marginTop = 14; act.style.height = 50; act.style.flexShrink = 0;
            c1.Add(act);
            if (equipped) Chip("EQUIPPED", RequiemUI.Gold, act).style.marginTop = 10;
            else act.Add(ActionButton("Draw this blade", "Enter", () => equip(w)));
            string tt = TraitTitle(w);
            if (tt.Length > 0)
            {
                var box = new VisualElement(); box.style.flexShrink = 0; box.style.marginTop = 18;
                Pad(box, 18, 14);
                box.style.backgroundColor = new Color(0.115f, 0.082f, 0.048f, 1f);
                box.style.borderLeftWidth = 3; box.style.borderLeftColor = RequiemUI.Gold;
                Txt("INNATE", "sr-tiny", box, InvMuted, 16).style.letterSpacing = 3;
                Txt(tt, "sr-title", box, RequiemUI.Gold, 26).style.marginTop = 2;
                var td = Txt("", "sr-card-desc", box, RequiemUI.Bone);
                td.enableRichText = true; td.text = Rich(w.trait.Describe()); td.style.marginTop = 8;
                c1.Add(box);
            }

            // --- 2. combat
            var c2 = col(1f, true);
            Header(cmp != null ? $"ATTRIBUTES  -  vs {cmp.displayName}" : "ATTRIBUTES", c2);
            // damage = ONE DPS number (players do not need per-hit damage)
            StatRow(c2, "DPS", WeaponRing.Dps(w), 45f, WeaponRing.Dps(w).ToString("0"),
                cmp != null ? WeaponRing.Dps(cmp) : (float?)null, "0");
            StatRow(c2, "SPEED", WeaponRing.Rate(w), 2.2f, WeaponRing.Rate(w).ToString("0.0") + "/s",
                cmp != null ? WeaponRing.Rate(cmp) : (float?)null, "0.0");
            StatRow(c2, "REACH", WeaponRing.Reach(w), 1.2f, WeaponRing.Reach(w).ToString("0.00"),
                cmp != null ? WeaponRing.Reach(cmp) : (float?)null, "0.00");
            StatRow(c2, "STAGGER", WeaponRing.Impact(w), 5f, WeaponRing.Impact(w).ToString("0.0"),
                cmp != null ? WeaponRing.Impact(cmp) : (float?)null, "0.0");
            // no "THE THREE CUTS" table — swords with special effects list the effect + number directly
            var fx = SpecialEffects(w);
            if (fx.Count > 0)
            {
                Header("SPECIAL", c2, 16);
                foreach (var kv in fx)
                {
                    var r = Row(c2); r.style.height = 26; r.style.flexShrink = 0; r.style.alignItems = Align.Center;
                    var l = Txt(kv.Key, "sr-tiny", r, InvMuted); l.style.flexGrow = 1; l.style.letterSpacing = 1;
                    var v = Txt(kv.Value, "sr-tiny", r, RequiemUI.Gold); v.style.unityTextAlign = TextAnchor.MiddleRight;
                }
            }

            // --- 3. the telling
            var c3 = col(1.15f, true);
            Header("ORIGIN", c3);                                              // section title
            var sc = Reader(c3);
            string lore = !string.IsNullOrEmpty(w.lore) ? w.lore : w.description;
            if (!string.IsNullOrEmpty(lore)) Txt(lore, "sr-card-desc", sc, InvBody, 20);
            if (!string.IsNullOrEmpty(w.foundAt)) Txt("Found: " + w.foundAt, "sr-tiny", sc, InvFaint, 17).style.marginTop = 14;
        }
    }
}
