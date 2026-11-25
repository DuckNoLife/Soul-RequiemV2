using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using SoulRequiem.Core;
using SoulRequiem.Gameplay;

namespace SoulRequiem.UI
{
    /// <summary>
    /// HUD — SURVIVAL + SKILLS + FLASKS: hud_vitals frame (HP bar, trail of HP just lost, poison), Roll / sword passive chips,
    /// 3 Mantra slots (cooldown ring, sealed), flasks (liquid by flask count, Hemorrhage), red vignette at low HP.
    /// </summary>
    public partial class RequiemHUD
    {
        /// <summary>Gold dot on the inventory when the number of picked items (picked_* flags) grew since the inventory was last opened.</summary>
        private void UpdateBagDot()
        {
            if (bagDot == null || Time.unscaledTime < bagCheckT) return;
            bagCheckT = Time.unscaledTime + 0.4f;
            var dir = MapEncounterDirector.Instance;
            if (dir == null || dir.Flags == null) return;
            int n = 0;
            foreach (var f in dir.Flags.All) if (f.StartsWith("picked_")) n++;
            if (bagSeenPicked < 0 || bagSeenOpens != RequiemMenus.InventoryOpens) { bagSeenPicked = n; bagSeenOpens = RequiemMenus.InventoryOpens; }
            bagDot.style.display = n > bagSeenPicked ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void BuildVignette()
        {
            vignette = new VisualElement { pickingMode = PickingMode.Ignore };
            vignette.AddToClassList("sr-fill");
            const int n = 64;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Clamp01((Mathf.Sqrt(dx * dx + dy * dy) - 0.55f) / 0.6f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(255 * d * d));
                }
            t.SetPixels32(px); t.Apply();
            vignette.style.backgroundImage = new StyleBackground(t);
            vignette.style.unityBackgroundImageTintColor = new Color(0.7f, 0.02f, 0.05f);
            vignette.style.opacity = 0f;
            fx.Add(vignette);
        }

        private void BuildVitals()
        {
            // ONE solid PixelLab frame `hud_vitals` 458x256: a large round socket (inventory) forged together with the HP bar,
            // a small round socket (Mantras) hanging below on a gold hook. The insides of the 3 sockets are cut out transparent, content is drawn UNDER the frame:
            //   HP bar  x157..434 (w 278), y91..120 · large socket x38..128, y61..151 · small socket x134..186, y173..225 (coordinates in the frame)
            var g = Group(hud, "vitals");
            g.style.left = M - 10; g.style.top = 10;
            g.style.width = 458; g.style.height = 256;
            // --- HP bar (under the frame)
            Box(g, 157, 91, HpW, 30, new Color(0.08f, 0.04f, 0.08f));
            hpLag = Box(g, 157, 91, HpW, 30, Lag);
            hpFill = RequiemUI.Img("bar_fill", 157, 91, HpW, 30, g);
            hpFill.style.unityBackgroundImageTintColor = HpRed;
            hpSurf = Box(g, 157, 91, HpW, 3, new Color(1f, 0.55f, 0.55f, 0.7f));
            // --- large socket: inventory
            Round(Box(g, 38, 61, 90, 90, new Color(0.07f, 0.04f, 0.09f)), 45f);
            bagGlow = Round(Box(g, 38, 61, 90, 90, new Color(0.62f, 0.36f, 0.18f, 0f)), 45f);
            portraitImg = RequiemUI.Img("ic_bag", 50, 71, 64, 64, g);
            // --- small socket: Mantras
            Round(Box(g, 134, 173, 52, 52, new Color(0.05f, 0.05f, 0.11f)), 26f);
            var tomeGlow = Round(Box(g, 134, 173, 52, 52, new Color(0.35f, 0.55f, 0.95f, 0f)), 26f);
            var tomeIcon = RequiemUI.Img("ic_tome", 142, 180, 36, 36, g);
            // --- the solid frame (top of the image layers)
            RequiemUI.Img("hud_vitals", 0, 0, 458, 256, g);
            hpText = RequiemUI.Text("", "sr-hp-text", g);
            RequiemUI.Abs(hpText, 157, 89, HpW, 32);
            // passive HP threshold mark (Blood Oath / Last Stand) on the HP bar
            traitHpTick = Box(g, 157, 87, 2, 38, new Color(1f, 0.85f, 0.45f, 0.9f));
            traitHpTick.style.display = DisplayStyle.None;
            // "new item" dot + key cap + click area
            bagDot = Round(Box(g, 118, 58, 14, 14, new Color(0.94f, 0.77f, 0.38f)), 7f);
            bagDot.style.borderTopWidth = bagDot.style.borderBottomWidth = bagDot.style.borderLeftWidth = bagDot.style.borderRightWidth = 2f;
            bagDot.style.borderTopColor = bagDot.style.borderBottomColor = bagDot.style.borderLeftColor = bagDot.style.borderRightColor = new Color(0.27f, 0.14f, 0.05f);
            bagDot.style.display = DisplayStyle.None;
            var bagKey = RequiemUI.Img("keycap", 70, 158, 26, 24, g);
            RequiemUI.Abs(RequiemUI.Text(GameInput.Label(GameAction.Inventory), "sr-key-small", bagKey), 0, 0, 26, 22);
            var tomeKey = RequiemUI.Img("keycap", 186, 206, 24, 22, g);
            RequiemUI.Abs(RequiemUI.Text(GameInput.Label(GameAction.Mantras), "sr-key-small", tomeKey), 0, 0, 24, 20);
            var bagHit = Round(Box(g, 38, 61, 90, 90, new Color(0f, 0f, 0f, 0f)), 45f);
            bagHit.pickingMode = PickingMode.Position;
            HudClick(bagHit, RequiemMenus.ToggleInventory, on =>
            {
                bagGlow.style.backgroundColor = new Color(0.62f, 0.36f, 0.18f, on ? 0.45f : 0f);
                portraitImg.style.scale = new Scale(Vector3.one * (on ? 1.08f : 1f));
            });
            var tomeHit = Round(Box(g, 132, 171, 56, 56, new Color(0f, 0f, 0f, 0f)), 28f);
            tomeHit.pickingMode = PickingMode.Position;
            HudClick(tomeHit, RequiemMenus.ToggleMantras, on =>
            {
                tomeGlow.style.backgroundColor = new Color(0.35f, 0.55f, 0.95f, on ? 0.4f : 0f);
                tomeIcon.style.scale = new Scale(Vector3.one * (on ? 1.1f : 1f));
            });

            // row of SQUARE cells under the bar (actions / status): Attack (J) / Roll (Q) / passive | effects
            statusRow = Group(g, "row");
            statusRow.style.left = 212; statusRow.style.top = 134;
            Chip(statusRow, 0, "ic_attack", GameInput.Label(GameAction.Attack), out _);
            rollRing = Chip(statusRow, 52, "ic_roll", GameInput.Label(GameAction.Roll), out rollCd);
            BuildTraitChip(statusRow, 104);
            Box(statusRow, 160, 4, 2, 34, new Color(RequiemUI.Gold.r, RequiemUI.Gold.g, RequiemUI.Gold.b, 0.6f));
            for (int i = 0; i < 6; i++)
            {
                var b = new Badge { root = Group(statusRow, "badge") };
                b.root.style.left = 174 + i * 100; b.root.style.top = 0;
                RequiemUI.Img("chk_off", 0, 0, 40, 40, b.root);
                b.icon = RequiemUI.Img("ic_slow", 4, 4, 32, 32, b.root);
                b.time = RequiemUI.Text("", "sr-small", b.root);
                RequiemUI.Abs(b.time, 46, 8);
                b.root.style.display = DisplayStyle.None;
                badges.Add(b);
            }
        }

        /// <summary>Passive cell next to Attack / Roll — glow behind, icon ic_trait_&lt;kind&gt;, build / drain ring, stack count, name below.</summary>
        private void BuildTraitChip(VisualElement parent, float x)
        {
            traitChip = Group(parent, "trait");
            traitChip.style.left = x; traitChip.style.top = 0; traitChip.style.width = 40; traitChip.style.height = 40;
            traitGlow = Box(traitChip, -3, -3, 46, 46, new Color(1f, 1f, 1f, 0f));
            RequiemUI.Img("chk_off", 0, 0, 40, 40, traitChip);
            var wrap = Group(traitChip, "ic");
            RequiemUI.Abs(wrap, 4, 4, 32, 32);
            wrap.style.overflow = Overflow.Hidden;
            traitIcon = RequiemUI.Img("ic_attack", 0, 0, 32, 32, wrap);
            traitRing = new CooldownRing();
            RequiemUI.Abs(traitRing, 0, 0, 32, 32);
            wrap.Add(traitRing);
            traitCount = RequiemUI.Text("", "sr-key-small", traitChip);
            RequiemUI.Abs(traitCount, 22, 22, 20, 18);
            traitLabel = RequiemUI.Text("", "sr-tiny sr-gold", traitChip);
            RequiemUI.Abs(traitLabel, -80, 42, 200, 24);
            traitLabel.style.unityTextAlign = TextAnchor.UpperCenter;
            traitLabel.style.whiteSpace = WhiteSpace.NoWrap;
            traitLabel.style.opacity = 0f;
            traitChip.style.display = DisplayStyle.None;
        }

        /// Small chips (Attack / Roll): frame 40, icon 32, small key cap in the bottom-right corner.
        private VisualElement Chip(VisualElement parent, float x, string icon, string key, out CooldownRing ring)
        {
            var c = Group(parent, "chip");
            c.style.left = x; c.style.top = 0; c.style.width = 40; c.style.height = 40;
            RequiemUI.Img("chk_off", 0, 0, 40, 40, c);
            var iconWrap = Group(c, "ic");
            RequiemUI.Abs(iconWrap, 4, 4, 32, 32);
            iconWrap.style.overflow = Overflow.Hidden;
            RequiemUI.Img(icon, 0, 0, 32, 32, iconWrap);
            ring = new CooldownRing();
            RequiemUI.Abs(ring, 0, 0, 32, 32);
            iconWrap.Add(ring);
            var k = RequiemUI.Img("keycap", 24, 22, 20, 19, c);
            var l = RequiemUI.Text(key, "sr-key-small", k);
            RequiemUI.Abs(l, 0, 0, 20, 17);
            return c;
        }

        private void BuildSkills()
        {
            var g = Group(hud, "skills");
            g.style.left = M; g.style.bottom = M + 16;
            g.style.width = 3 * 113 + 2 * 12; g.style.height = 114;
            for (int i = 0; i < sockets.Length; i++)
            {
                var s = new Socket { root = Group(g, "socket" + i) };
                s.root.style.left = i * 125; s.root.style.top = 0; s.root.style.width = 113; s.root.style.height = 114;
                RequiemUI.Img("skill_socket", 0, 0, 113, 114, s.root);          // frame first, icon over the socket
                s.glow = Box(s.root, 19, 19, 73, 68, new Color(1f, 1f, 1f, 0f));
                var inner = Group(s.root, "inner");
                RequiemUI.Abs(inner, 19, 19, 73, 68);
                inner.style.overflow = Overflow.Hidden;
                s.icon = RequiemUI.Img("ic_slot_empty", 4, 2, 64, 64, inner);
                s.ring = new CooldownRing();
                RequiemUI.Abs(s.ring, 0, 0, 73, 68);
                inner.Add(s.ring);
                s.seal = RequiemUI.Img("ic_silence", 12, 8, 48, 48, inner);
                s.seal.style.display = DisplayStyle.None;
                s.cd = RequiemUI.Text("", "sr-cd", s.root);
                RequiemUI.Abs(s.cd, 19, 19, 73, 68);
                s.key = RequiemUI.Text(GameInput.KeyLabel(GameInput.SkillKey(i)), "sr-key", RequiemUI.Img("keycap", 56 - 16, 114 - 14, 32, 30, s.root));
                RequiemUI.Abs(s.key, 0, 0, 32, 27);
                sockets[i] = s;
            }
        }

        private void BuildFlask()
        {
            flaskRoot = Group(hud, "flask");
            flaskRoot.style.right = M; flaskRoot.style.bottom = M + 16;
            flaskRoot.style.width = 96; flaskRoot.style.height = 197;
            RequiemUI.Img("flask_back", 0, 0, 96, 197, flaskRoot);
            flaskLiquidClip = Group(flaskRoot, "liquid");
            RequiemUI.Abs(flaskLiquidClip, 0, FlaskLiqY0, 96, FlaskLiqY1 - FlaskLiqY0);
            flaskLiquidClip.style.overflow = Overflow.Hidden;
            flaskLiquid = RequiemUI.Img("flask_liquid", 0, -FlaskLiqY0, 96, 197, flaskLiquidClip);
            flaskSurface = Box(flaskRoot, 12, FlaskLiqY0, 72, 2, new Color(0.95f, 0.45f, 0.5f, 0.9f));
            RequiemUI.Img("flask_front", 0, 0, 96, 197, flaskRoot);
            flaskFlash = Box(flaskRoot, 6, 96, 84, 96, new Color(1f, 0.6f, 0.4f, 0f));
            flaskFlash.style.borderTopLeftRadius = flaskFlash.style.borderTopRightRadius =
                flaskFlash.style.borderBottomLeftRadius = flaskFlash.style.borderBottomRightRadius = 44;
            flaskSeal = RequiemUI.Img("ic_bleed", 24, 116, 48, 48, flaskRoot);
            flaskSeal.style.display = DisplayStyle.None;
            flaskTimer = RequiemUI.Text("", "sr-small sr-warn", flaskRoot);
            RequiemUI.Abs(flaskTimer, -70, 160);
            // flask count badge left of the flask neck
            var pill = new VisualElement { pickingMode = PickingMode.Ignore };
            RequiemUI.Abs(pill, -58, 22, 72, 34);
            RequiemUI.Slice(pill, "pill", 10, 8, 10, 8);
            flaskRoot.Add(pill);
            flaskCount = RequiemUI.Text("", "sr-count", pill);
            RequiemUI.Abs(flaskCount, 0, 0, 72, 31);
            var k = RequiemUI.Img("keycap", 48 - 16, 197 - 14, 32, 30, flaskRoot);
            var l = RequiemUI.Text(GameInput.Label(GameAction.Heal), "sr-key", k);
            RequiemUI.Abs(l, 0, 0, 32, 27);
        }

        private void UpdateVitals(float dt)
        {
            if (hp == null) return;
            float pct = hp.Percent;
            if (pct >= lag) { lag = pct; lagHold = 0.45f; }
            else if (lagHold > 0f) lagHold -= dt;
            else lag = Mathf.MoveTowards(lag, pct, dt * 0.6f);
            hpFill.style.width = HpW * pct;
            hpSurf.style.width = HpW * pct;
            hpLag.style.width = HpW * lag;
            Color c = hp.IsPoisoned ? HpPoison : HpRed;
            if (pct < 0.3f && !hp.IsDead && !GameSettings.ReduceFlashing)
                c = Color.Lerp(c, new Color(1f, 0.5f, 0.45f), 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 9f));
            hpFill.style.unityBackgroundImageTintColor = c;
            string t = $"{Mathf.CeilToInt(hp.Current)} / {hp.Max:0}";
            if (hpText.text != t) hpText.text = t;
            portraitImg.style.unityBackgroundImageTintColor = hp.IsDead ? new Color(0.4f, 0.4f, 0.4f) :
                                                              hp.IsStunned ? new Color(1f, 0.92f, 0.6f) : Color.white;
            UpdateBagDot();
            // Roll: cooldown ring
            if (kit != null) rollCd.Progress = kit.rollCooldown > 0f ? kit.RollCooldownLeft / kit.rollCooldown : 0f;
            // prison clothes cannot roll -> the Roll cell dims
            if (rollRing != null) rollRing.style.opacity = status != null && (status.Disarmed || status.RollSealed) ? 0.3f : 1f;   // + roll lock

            // status
            int n = 0;
            if (kit != null && kit.GodMode) SetBadge(n++, "ic_god", "GOD");
            if (hp.IsStunned) SetBadge(n++, "ic_stun", "");
            if (hp.IsPoisoned) SetBadge(n++, "ic_poison", "");
            if (status == null && hp != null) status = hp.GetComponent<PlayerStatus>();
            if (status != null)
            {
                if (status.SlowLeft > 0f) SetBadge(n++, "ic_slow", Secs(status.SlowLeft));
                if (status.SilenceLeft > 0f) SetBadge(n++, "ic_silence", Secs(status.SilenceLeft));
                if (status.RollSealLeft > 0f) SetBadge(n++, "ic_roll", Secs(status.RollSealLeft));   // roll lock
                if (status.HealBlockLeft > 0f) SetBadge(n++, "ic_bleed", Secs(status.HealBlockLeft));
            }
            for (int i = n; i < badges.Count; i++) badges[i].root.style.display = DisplayStyle.None;

            UpdateTraitGauge();

            // low HP red vignette
            float k = (!hp.IsDead && pct < 0.3f && GameSettings.LowHpVignette) ? 1f - pct / 0.3f : 0f;
            float beat = GameSettings.ReduceFlashing ? 0.8f : 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * (3f + 3f * k)));
            vignette.style.opacity = k > 0f ? (0.3f + 0.5f * k) * beat : 0f;
        }

        /// <summary>Gauge colour per passive kind (from the sword's / source boss's colour).</summary>
        private static Color TraitColor(WeaponTraitKind k)
        {
            switch (k)
            {
                case WeaponTraitKind.Counterstroke: return new Color(0.85f, 0.85f, 0.8f);
                case WeaponTraitKind.Vigil: return new Color(0.78f, 0.5f, 1f);
                case WeaponTraitKind.BloodOath: return new Color(0.9f, 0.2f, 0.22f);
                case WeaponTraitKind.Iaijutsu: return new Color(0.6f, 0.72f, 0.88f);
                case WeaponTraitKind.LastStand: return new Color(0.62f, 0.8f, 0.95f);
                case WeaponTraitKind.Hunger: return new Color(0.6f, 0.08f, 0.16f);
                case WeaponTraitKind.Accord: return new Color(1f, 0.84f, 0.42f);
                case WeaponTraitKind.Namebound: return new Color(0.55f, 0.75f, 1f);
                case WeaponTraitKind.Penance: return new Color(0.85f, 0.7f, 0.4f);
                default: return Color.white;
            }
        }

        /// <summary>Passive cell. Only timed passives (Counterstroke / Vigil / Accord) have a ring; HP thresholds are drawn on the HP bar.</summary>
        private void UpdateTraitGauge()
        {
            float dt = Time.unscaledDeltaTime;
            if (kit == null || !WeaponTraits.Gauge(kit, out float fill, out bool active, out float mark))
            {
                traitChip.style.display = DisplayStyle.None; traitHpTick.style.display = DisplayStyle.None;
                traitShown = WeaponTraitKind.None; return;
            }
            var kind = WeaponTraits.KindOf(kit);
            traitChip.style.display = DisplayStyle.Flex;
            if (kind != traitShown)                                   // sword changed: new icon, the cell flashes, the name shows for 2.5 s
            {
                traitShown = kind;
                var tex = RequiemUI.Tex("ic_trait_" + kind.ToString().ToLowerInvariant());
                if (tex == null) tex = RequiemUI.Tex("ic_attack");
                if (tex != null) traitIcon.style.backgroundImage = new StyleBackground(tex);
                traitLabel.text = WeaponTraits.Title(kit).ToUpperInvariant();
                traitNameT = 2.5f; traitFlash = 1f;
            }
            var col = TraitColor(kind);

            // ring: dark part = not built yet / drained
            bool timed = kind == WeaponTraitKind.Counterstroke || kind == WeaponTraitKind.Vigil || kind == WeaponTraitKind.Accord;
            traitRing.Progress = timed && (active || kind == WeaponTraitKind.Vigil) ? 1f - Mathf.Clamp01(fill) : 0f;

            // stack count
            string count = "";
            if (kind == WeaponTraitKind.Penance && kit.PenanceStacks > 0) count = kit.PenanceStacks.ToString();
            else if (kind == WeaponTraitKind.Hunger && EnemyStatus.MarkedAlive > 0) count = EnemyStatus.MarkedAlive.ToString();
            if (traitCount.text != count) traitCount.text = count;

            // dim while waiting, bright + sword-colour outline when active
            traitIcon.style.unityBackgroundImageTintColor = active ? Color.white : new Color(0.42f, 0.4f, 0.46f, 1f);
            float pulse = active ? (GameSettings.ReduceFlashing ? 0.35f : 0.32f + 0.18f * Mathf.Sin(Time.unscaledTime * 6f)) : 0f;
            traitFlash = Mathf.Max(0f, traitFlash - dt * 1.6f);
            float a = Mathf.Max(pulse, GameSettings.ReduceFlashing ? 0f : traitFlash * 0.8f);
            traitGlow.style.backgroundColor = new Color(col.r, col.g, col.b, a);

            // passive name right after a sword change
            traitNameT = Mathf.Max(0f, traitNameT - dt);
            traitLabel.style.opacity = Mathf.Clamp01(traitNameT / 0.6f);
            traitLabel.style.color = new StyleColor(Color.Lerp(col, Color.white, 0.35f));

            // HP threshold mark
            float thr = WeaponTraits.HpThreshold(kit);
            if (thr > 0f && thr < 1f)
            {
                traitHpTick.style.display = DisplayStyle.Flex;
                traitHpTick.style.left = 157f + HpW * thr - 1f;
                traitHpTick.style.backgroundColor = active ? Color.Lerp(col, Color.white, 0.3f) : new Color(1f, 0.85f, 0.45f, 0.75f);
            }
            else traitHpTick.style.display = DisplayStyle.None;
        }

        private void SetBadge(int i, string icon, string time)
        {
            if (i >= badges.Count) return;
            var b = badges[i];
            b.root.style.display = DisplayStyle.Flex;
            var t = RequiemUI.Tex(icon);
            if (t != null) b.icon.style.backgroundImage = new StyleBackground(t);
            if (b.time.text != time) b.time.text = time;
        }

        private static string Secs(float t) => t >= 10f ? $"{t:0}s" : $"{t:0.0}s";

        private void OnCast(int slot)
        {
            if (slot >= 0 && slot < sockets.Length) sockets[slot].flash = -0.6f;     // dark flash right after use
        }

        private void UpdateSkills(float dt)
        {
            bool silenced = status != null && status.Silenced;
            // empty skill slots only show while the player owns unequipped skills (so they know they can equip them at a campfire) — otherwise 3 empty slots
            // look like missing content. A slot that just got a skill flashes when it appears.
            int equipped = 0;
            for (int i = 0; i < sockets.Length; i++) if (loadout != null && loadout.SkillAt(i) != null) equipped++;
            bool spare = loadout != null && loadout.owned.Count > equipped;
            for (int i = 0; i < sockets.Length; i++)
            {
                var s = sockets[i];
                var sk = loadout != null ? loadout.SkillAt(i) : null;
                bool vis = sk != null || spare;
                var want = vis ? DisplayStyle.Flex : DisplayStyle.None;
                if (s.root.style.display.value != want || s.root.style.display.keyword != StyleKeyword.Undefined)
                {
                    if (vis && s.root.resolvedStyle.display == DisplayStyle.None) s.flash = 1f;
                    s.root.style.display = want;
                }
                if (sk != s.shown)
                {
                    s.shown = sk;
                    var t = sk != null ? sk.Icon : RequiemUI.Tex("ic_slot_empty");
                    s.icon.style.backgroundImage = t != null ? new StyleBackground(t) : new StyleBackground();
                }
                float left = loadout != null ? loadout.CooldownLeft(i) : 0f;
                float total = loadout != null ? loadout.CooldownTotal(i) : 1f;
                s.ring.Progress = sk != null ? left / total : 0f;
                string cd = sk != null && left > 0f ? (left >= 1f ? $"{left:0}" : $"{left:0.0}") : "";
                if (s.cd.text != cd) s.cd.text = cd;
                if (s.wasLeft > 0f && left <= 0f && sk != null) s.flash = 1f;           // cooldown just finished: flash
                s.wasLeft = left;
                s.seal.style.display = silenced && sk != null ? DisplayStyle.Flex : DisplayStyle.None;
                s.icon.style.unityBackgroundImageTintColor = silenced && sk != null ? new Color(0.5f, 0.35f, 0.6f) : Color.white;
                if (s.flash > 0f) s.flash = Mathf.Max(0f, s.flash - dt * 3f);
                else if (s.flash < 0f) s.flash = Mathf.Min(0f, s.flash + dt * 3f);
                Color acc = sk != null ? sk.accent : Color.white;
                s.glow.style.backgroundColor = s.flash > 0f ? new Color(acc.r, acc.g, acc.b, 0.55f * s.flash)
                                             : s.flash < 0f ? new Color(0f, 0f, 0f, -0.6f * s.flash) : new Color(0, 0, 0, 0);
            }
        }

        private void UpdateFlask(float dt)
        {
            if (kit == null) return;
            int p = kit.Potions;
            potionCap = Mathf.Max(potionCap, Mathf.Max(p, kit.maxPotions));
            if (lastPotions >= 0 && p < lastPotions) flaskFlashT = 1f;
            lastPotions = p;
            float level = p <= 0 ? 0f : Mathf.Clamp01(0.2f + 0.8f * p / Mathf.Max(1f, potionCap));
            float h = (FlaskLiqY1 - FlaskLiqY0) * level;
            float top = FlaskLiqY1 - h;
            flaskLiquidClip.style.top = top; flaskLiquidClip.style.height = h;
            flaskLiquid.style.top = -top;
            flaskSurface.style.top = top;
            flaskSurface.style.display = p > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            string c = p.ToString();
            if (flaskCount.text != c) flaskCount.text = c;
            flaskCount.EnableInClassList("sr-warn", p <= 0);
            bool blocked = status != null && status.HealBlocked;
            flaskSeal.style.display = blocked ? DisplayStyle.Flex : DisplayStyle.None;
            string tt = blocked ? Secs(status.HealBlockLeft) : "";
            if (flaskTimer.text != tt) flaskTimer.text = tt;
            flaskFlashT = Mathf.Max(0f, flaskFlashT - dt * 2.5f);
            float a = flaskFlashT * 0.55f;
            if (blocked && !GameSettings.ReduceFlashing) a = Mathf.Max(a, 0.25f + 0.2f * Mathf.Sin(Time.unscaledTime * 8f));
            flaskFlash.style.backgroundColor = blocked ? new Color(0.8f, 0.1f, 0.15f, a) : new Color(1f, 0.6f, 0.4f, a);
        }
    }
}
