using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using SoulRequiem.Core;
using SoulRequiem.Gameplay;

namespace SoulRequiem.UI
{
    /// <summary>
    /// HUD — BOSS FIGHT: boss HP bar (1 boss or a 2-boss duo, hidden when boss.HideBossBar), cast bar, barrier,
    /// boss name card on appearance (BossIntro), hint strip (Hints).
    /// Reads: BossJuice (cast bar), Health (HP / barrier), BossIntro, Hints.
    /// </summary>
    public partial class RequiemHUD
    {
        private void BuildBossAndHint()
        {
            bossRoot = Group(hud, "boss");
            bossRoot.style.bottom = M; bossRoot.style.width = 671; bossRoot.style.height = 80;
            Box(bossRoot, 47, 42, 577, 19, new Color(0.11f, 0.06f, 0.1f));
            bossLag = Box(bossRoot, 47, 42, 577, 19, Lag);
            bossFill = RequiemUI.Img("bar_fill", 47, 42, 577, 19, bossRoot);
            bossFill.style.unityBackgroundImageTintColor = BossRed;
            bossSurf = Box(bossRoot, 47, 42, 577, 3, new Color(0.9f, 0.35f, 0.42f, 0.8f));
            bossHit = Box(bossRoot, 47, 42, 577, 19, Color.white);                 // white flash when hit
            bossHit.style.opacity = 0f;
            // BARRIER — 7 px strip over the top half of the HP bar fill (colour from Health.BarrierColor), split into 4 cells, flashes when it absorbs a hit
            armorBack = Box(bossRoot, 47, 42, 577, 7, new Color(0.08f, 0.04f, 0.12f, 0.75f));
            armorFill = Box(bossRoot, 47, 42, 577, 7, new Color(0.62f, 0.38f, 0.95f));
            armorGloss = Box(bossRoot, 47, 42, 577, 1, new Color(1f, 1f, 1f, 0.55f));
            for (int i = 1; i < 4; i++) armorTicks.Add(Box(bossRoot, 47 + 577f * i / 4f - 1f, 42, 2, 7, new Color(0.05f, 0.02f, 0.08f, 0.9f)));
            armorFlash = Box(bossRoot, 47, 42, 577, 19, Color.white);
            armorFlash.style.opacity = 0f;
            RequiemUI.Img("boss_frame", 0, 0, 671, 80, bossRoot);
            bossName = RequiemUI.Text("", "sr-title sr-boss-name", bossRoot);
            RequiemUI.Abs(bossName, -200, -44, 1071, 40);
            bossSub = RequiemUI.Text("", "sr-small sr-gold sr-right", bossRoot);
            RequiemUI.Abs(bossSub, 380, 2, 240, 24);
            armorIcon = RequiemUI.Img("ic_soul_armor", 52, 6, 22, 26, bossRoot);
            armorLabel = RequiemUI.Text("", "sr-small sr-gold", bossRoot);
            RequiemUI.Abs(armorLabel, 80, 4, 220, 24);
            // SECONDARY BOSS BAR — violet metal frame with gold trim matching the boss frame (duo_bar_frame 420x32, fill 326x11 at 47,11),
            // delayed HP trail, gloss, name on the left + "SEALED" with a padlock on the right while the secondary boss is invulnerable (e.g. the cannon on its pedestal)
            bossP = new VisualElement { pickingMode = PickingMode.Ignore };
            bossP.style.position = Position.Absolute; bossP.style.left = 125; bossP.style.top = -100; bossP.style.width = 420; bossP.style.height = 50;
            bossRoot.Add(bossP);
            bossPName = RequiemUI.Text("", "sr-small sr-gold", bossP);
            RequiemUI.Abs(bossPName, 47, 0, 200, 18);
            bossPSeal = RequiemUI.Text("SEALED", "sr-small sr-gold sr-right", bossP);
            RequiemUI.Abs(bossPSeal, 213, 0, 140, 18);
            bossPLock = RequiemUI.Img("ic_lock", 356, 1, 18, 22, bossP);
            Box(bossP, 47, 29, 326, 11, new Color(0.11f, 0.06f, 0.1f));
            bossPLag = Box(bossP, 47, 29, 326, 11, Lag);
            bossPFill = RequiemUI.Img("bar_fill", 47, 29, 326, 11, bossP);
            bossPGloss = Box(bossP, 47, 29, 326, 2, new Color(1f, 1f, 1f, 0.35f));
            RequiemUI.Img("duo_bar_frame", 0, 18, 420, 32, bossP);
            bossP.style.display = DisplayStyle.None;

            // cast bar — above the boss name (duo: above the secondary boss bar too)
            castRoot = new VisualElement { pickingMode = PickingMode.Ignore };
            castRoot.style.position = Position.Absolute; castRoot.style.left = 135; castRoot.style.top = -86;
            castRoot.style.width = 400; castRoot.style.height = 34;
            castRoot.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));
            bossRoot.Add(castRoot);
            castName = RequiemUI.Text("", "sr-small", castRoot);
            RequiemUI.Abs(castName, 0, 0, 400, 20);
            castName.style.unityTextAlign = TextAnchor.MiddleCenter; castName.style.fontSize = 16;
            castName.style.unityFontStyleAndWeight = FontStyle.Bold;
            Box(castRoot, 48, 21, 304, 10, new Color(0.02f, 0.01f, 0.03f, 0.85f));     // dark outline
            Box(castRoot, 50, 23, 300, 6, new Color(0.12f, 0.08f, 0.14f, 0.95f));      // fill
            castFill = Box(castRoot, 50, 23, 0, 6, Color.white);
            castTip = Box(castRoot, 50, 21, 2, 10, Color.white);                          // bright bar tip
            castFlash = Box(castRoot, 48, 21, 304, 10, Color.white);
            castFlash.style.opacity = 0f;
            castRoot.style.display = DisplayStyle.None;
            bossRoot.style.display = DisplayStyle.None;

            // row spans the full width, the text strip is centred inside it (left:50% + translate squeezed the strip to half the screen -> shifted right)
            hintRoot = new VisualElement { pickingMode = PickingMode.Ignore };
            hintRoot.AddToClassList("sr-hint");
            hud.Add(hintRoot);
            var band = new VisualElement { pickingMode = PickingMode.Ignore };
            band.AddToClassList("sr-hint-band");
            var hb = RequiemUI.Tex("hint_band");
            if (hb != null) band.style.backgroundImage = new StyleBackground(hb);
            hintRoot.Add(band);
            var top = RequiemUI.Deco("hint_line", "sr-hint-line", band); top.style.top = 0;
            var bot = RequiemUI.Deco("hint_line", "sr-hint-line", band); bot.style.bottom = 0;
            hintText = RequiemUI.Text("", "sr-hint-text", band);
            hintRoot.style.display = DisplayStyle.None;

            restPrompt = new VisualElement { pickingMode = PickingMode.Ignore };
            restPrompt.AddToClassList("sr-prompt-row");
            hud.Add(restPrompt);
            var promptBox = new VisualElement { pickingMode = PickingMode.Ignore };
            promptBox.AddToClassList("sr-prompt");
            restPrompt.Add(promptBox);
            var rk = RequiemUI.Img("keycap", 0, 0, 32, 30, null);
            rk.style.position = Position.Relative; rk.style.marginRight = 10;
            var rkl = RequiemUI.Text(GameInput.Label(GameAction.Interact), "sr-key", rk);
            RequiemUI.Abs(rkl, 0, 0, 32, 27);
            promptBox.Add(rk);
            restText = RequiemUI.Text("Rest at the shrine", "sr-prompt-text", promptBox);
            // first campfire lit: explanation line right under the prompt (instead of a second hint at the bottom of the screen)
            restSub = RequiemUI.Text("", "sr-prompt-text", restPrompt);
            restSub.style.fontSize = 19; restSub.style.marginTop = 3;
            restSub.style.paddingLeft = 14; restSub.style.paddingRight = 14; restSub.style.paddingTop = 3; restSub.style.paddingBottom = 4;
            restSub.style.backgroundColor = new Color(0.055f, 0.03f, 0.086f, 0.7f);
            restSub.style.color = new Color(0.78f, 0.92f, 1f);
            restSub.style.display = DisplayStyle.None;
            restPrompt.style.display = DisplayStyle.None;
        }

        /// <summary>A "sealed" boss = currently takes no damage (DamageTakenMul 0 / invulnerable) — e.g. the Brazen Reliquary on its pedestal.</summary>
        private static bool Sealed(Health h) => h != null && (h.DamageTakenMul <= 0.001f || h.Invulnerable);

        private float UpdateBoss(MapEncounterDirector dir, float dt)
        {
            var boss = dir != null ? dir.Boss : null;
            bool show = boss != null && boss.Health != null && !boss.Health.IsDead && !BossIntro.CardActive && !boss.HideBossBar;
            bossRoot.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) return PanelH;
            // secondary boss of a duo. The one that CAN be hit goes on the big bar — main boss sealed but the secondary not -> swap
            var partner = dir.BossPartner;
            bool two = partner != null && partner.Health != null && !partner.Health.IsDead;
            EnemyAI main = boss, side = partner;
            if (two && Sealed(boss.Health) && !Sealed(partner.Health)) { main = partner; side = boss; }
            var mh = main.Health;
            if (mh != lastBoss) { lastBoss = mh; bossLagV = mh.Percent; armorShownV = mh.BarrierPercent; bossPctSeen = mh.Percent; }
            float pct = mh.Percent;
            if (pct < bossPctSeen - 0.0005f) bossHitT = 1f;                              // just lost HP -> white flash
            bossPctSeen = pct;
            bossHitT = Mathf.Max(0f, bossHitT - dt * 7f);
            bossHit.style.width = 577f * pct;
            bossHit.style.opacity = bossHitT * 0.75f;
            if (pct >= bossLagV) { bossLagV = pct; bossLagHold = 0.45f; }
            else if (bossLagHold > 0f) bossLagHold -= dt;
            else bossLagV = Mathf.MoveTowards(bossLagV, pct, dt * 0.5f);
            bossFill.style.width = 577f * pct; bossSurf.style.width = 577f * pct; bossLag.style.width = 577f * bossLagV;
            UpdateArmor(mh, dt);
            // centre of the screen, without covering the skill cluster on the left / flasks on the right (shrinks on narrow screens)
            const float bw = 671f;
            float w = PanelW, minLeft = M + 3 * 125 + 24, maxRight = w - M - 96 - 70;
            float scale = Mathf.Clamp((maxRight - minLeft) / bw, 0.55f, 1f);
            float sw = bw * scale;
            float vis = Mathf.Clamp((w - sw) / 2f, minLeft, Mathf.Max(minLeft, maxRight - sw));
            bossRoot.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(100));
            bossRoot.style.scale = new Scale(new Vector3(scale, scale, 1f));
            bossRoot.style.left = vis - (bw - sw) / 2f;
            string name = main.displayName ?? "", sub = "";
            int cut = name.IndexOf('—');
            if (cut >= 0) { sub = name.Substring(cut + 1).Trim(); name = name.Substring(0, cut).Trim(); }
            name = RequiemUI.Clean(name); sub = RequiemUI.Clean(sub);
            if (bossName.text != name) bossName.text = name;
            if (bossSub.text != sub) bossSub.text = sub;
            bossP.style.display = two ? DisplayStyle.Flex : DisplayStyle.None;
            if (two)
            {
                var ph = side.Health;
                if (ph != lastPartner) { lastPartner = ph; bossPLagV = ph.Percent; }
                float pp = ph.Percent;
                bossPLagV = pp >= bossPLagV ? pp : Mathf.MoveTowards(bossPLagV, pp, dt * 0.5f);
                bool sealedSide = Sealed(ph);
                bossPFill.style.width = 326f * pp; bossPGloss.style.width = 326f * pp; bossPLag.style.width = 326f * bossPLagV;
                bossPFill.style.unityBackgroundImageTintColor = sealedSide ? new Color(0.42f, 0.38f, 0.5f) : BossRed;   // sealed: violet grey
                bossPSeal.style.display = sealedSide ? DisplayStyle.Flex : DisplayStyle.None;
                bossPLock.style.display = sealedSide ? DisplayStyle.Flex : DisplayStyle.None;
                if (sealedSide) bossPLock.style.opacity = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 3f);
                string pn = side.displayName ?? "";
                int pc = pn.IndexOf('—');
                if (pc >= 0) pn = pn.Substring(0, pc).Trim();
                pn = RequiemUI.Clean(pn).ToUpperInvariant();
                if (bossPName.text != pn) bossPName.text = pn;
            }
            UpdateCast(two);
            return PanelH - M - 80f * scale - 48f - (two ? 56f * scale : 0f) - (castShown ? 40f * scale : 0f);
        }

        /// <summary>Cast bar (BossJuice). Winding up: skill name in school colour with a soft pulse + a filling bar, white bar tip.
        /// Just released: the whole block flashes white, grows 12% then fades over 0.55 s. Boss dead / cast cancelled -> hidden.</summary>
        private void UpdateCast(bool two)
        {
            float now = Time.time;
            var owner = BossJuice.CastOwner;
            bool alive = owner != null && !owner.IsDead;
            bool fired = BossJuice.CastFiredAt >= BossJuice.CastStart;
            float since = now - BossJuice.CastFiredAt;
            bool charging = alive && !fired && now < BossJuice.CastEnd + 0.15f;
            bool fading = alive && fired && since < 0.55f;
            castShown = charging || fading;
            castRoot.style.display = castShown ? DisplayStyle.Flex : DisplayStyle.None;
            if (!castShown) return;
            castRoot.style.top = two ? -150 : -86;
            var c = BossJuice.CastColor;
            string t = RequiemUI.Clean(BossJuice.CastName).ToUpperInvariant();
            if (castName.text != t) castName.text = t;
            float span = Mathf.Max(0.01f, BossJuice.CastEnd - BossJuice.CastStart);
            float k = charging ? Mathf.Clamp01((now - BossJuice.CastStart) / span) : 1f;
            castFill.style.width = 300f * k;
            castTip.style.left = 50f + 300f * k - 1f;
            castFill.style.backgroundColor = Color.Lerp(c, Color.white, 0.3f * k * k);
            if (charging)
            {
                castName.style.color = new Color(c.r, c.g, c.b, 0.82f + 0.18f * Mathf.Sin(now * 9f));
                castFlash.style.opacity = 0f;
                castRoot.style.opacity = Mathf.Clamp01((now - BossJuice.CastStart) / 0.12f);   // appear fast
                castRoot.style.scale = new Scale(Vector3.one);
            }
            else
            {
                float a = 1f - since / 0.55f;
                castName.style.color = Color.Lerp(Color.white, c, Mathf.Clamp01(since / 0.2f));
                castFlash.style.opacity = Mathf.Clamp01(1f - since / 0.25f) * 0.9f;
                castRoot.style.opacity = Mathf.Clamp01(a * 1.6f);
                float pop = 1f + 0.12f * Mathf.Clamp01(1f - since / 0.2f);
                castRoot.style.scale = new Scale(new Vector3(pop, pop, 1f));
            }
        }

        /// <summary>BARRIER strip on the main boss bar — glides to the real value, flashes white when the barrier absorbs a hit,
        /// label "SOUL ARMOR" (barrier colour) / "ARMOR SHATTERED" (blinking red) when broken. Boss without a barrier -> all hidden.</summary>
        private void UpdateArmor(Health h, float dt)
        {
            bool has = h.BarrierMax > 0f;
            var disp = has ? DisplayStyle.Flex : DisplayStyle.None;
            armorBack.style.display = disp; armorFill.style.display = disp; armorGloss.style.display = disp;
            armorFlash.style.display = disp; armorIcon.style.display = disp; armorLabel.style.display = disp;
            foreach (var t in armorTicks) t.style.display = disp;
            if (!has) return;
            float target = h.BarrierPercent;
            armorShownV = target > armorShownV ? Mathf.MoveTowards(armorShownV, target, dt * 1.6f)   // barrier refilling: rises gradually
                                               : Mathf.MoveTowards(armorShownV, target, dt * 4f);
            Color c = h.BarrierColor;
            armorFill.style.width = 577f * armorShownV;
            armorGloss.style.width = 577f * armorShownV;
            armorFill.style.backgroundColor = c;
            armorGloss.style.backgroundColor = new Color(Mathf.Lerp(c.r, 1f, 0.6f), Mathf.Lerp(c.g, 1f, 0.6f), Mathf.Lerp(c.b, 1f, 0.6f), 0.8f);
            float since = Time.time - h.BarrierHitTime;
            armorFlash.style.opacity = since < 0.18f ? 0.45f * (1f - since / 0.18f) : 0f;
            armorIcon.style.unityBackgroundImageTintColor = target > 0f ? c : new Color(0.35f, 0.32f, 0.38f);
            bool broken = target <= 0f;
            string txt = broken ? "ARMOR SHATTERED" : "SOUL ARMOR";
            if (armorLabel.text != txt) armorLabel.text = txt;
            armorLabel.style.color = broken ? new Color(1f, 0.35f, 0.3f, 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 6f))
                                            : new Color(Mathf.Lerp(c.r, 1f, 0.45f), Mathf.Lerp(c.g, 1f, 0.45f), Mathf.Lerp(c.b, 1f, 0.45f));
        }

        /// <summary>Boss name card: 55% dark backdrop, a -5° slanted band ~26% of the screen tall (thick dark edges
        /// top / bottom + light / dark lines at the coloured band's edges), boss name 180px white with a dark outline, epithet above it, the boss's own portrait
        /// (Resources/UI/Requiem/&lt;portrait&gt;, white outline) ~1.6x the band height, feet on the band's lower edge, head beyond the band;
        /// when the band lands: a 0.12 s white flash. Slides in 0.28 s, holds, slides out 0.32 s.</summary>
        private void BuildBossCard()
        {
            cardRoot = new VisualElement { pickingMode = PickingMode.Ignore };
            cardRoot.style.position = Position.Absolute;
            cardRoot.style.left = 0; cardRoot.style.right = 0; cardRoot.style.top = 0; cardRoot.style.bottom = 0;
            hud.Add(cardRoot);

            cardShade = Fill(cardRoot, new Color(0f, 0f, 0f, 0.55f));

            cardTilt = new VisualElement { pickingMode = PickingMode.Ignore };
            cardTilt.style.position = Position.Absolute;
            cardTilt.style.left = Length.Percent(-10); cardTilt.style.width = Length.Percent(120);
            cardTilt.style.rotate = new Rotate(-5f);
            cardTilt.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));
            cardRoot.Add(cardTilt);
            Color ink = new Color(0.05f, 0.03f, 0.06f, 0.97f);
            cardStripTop = Strip(cardTilt, ink);
            cardHiTop = Strip(cardTilt, Color.white);
            cardBand = Strip(cardTilt, Color.gray);
            cardHiBot = Strip(cardTilt, Color.black);
            cardStripBot = Strip(cardTilt, ink);

            cardSub = RequiemUI.Text("", "sr-title", cardRoot);
            cardSub.style.position = Position.Absolute;
            cardSub.style.fontSize = 34; cardSub.style.color = new Color(1f, 1f, 1f, 0.9f);
            cardSub.style.letterSpacing = 8;
            cardSub.style.unityTextOutlineWidth = 1.5f; cardSub.style.unityTextOutlineColor = ink;
            cardSub.style.rotate = new Rotate(-5f);
            cardSub.style.transformOrigin = new TransformOrigin(Length.Percent(0), Length.Percent(50));

            cardName = RequiemUI.Text("", "sr-title", cardRoot);
            cardName.style.position = Position.Absolute;
            cardName.style.fontSize = 180; cardName.style.color = Color.white;
            cardName.style.letterSpacing = 10;
            cardName.style.unityTextOutlineWidth = 4f;
            cardName.style.unityTextOutlineColor = ink;
            cardName.style.textShadow = new TextShadow { offset = new Vector2(9, 9), blurRadius = 0, color = new Color(0.05f, 0.03f, 0.06f, 0.85f) };
            cardName.style.rotate = new Rotate(-5f);
            cardName.style.transformOrigin = new TransformOrigin(Length.Percent(0), Length.Percent(50));

            // portrait shadow (same image, filled black) offset down-right -> depth like a Soul Knight card
            cardPortraitShadow = new VisualElement { pickingMode = PickingMode.Ignore };
            cardPortraitShadow.style.position = Position.Absolute;
            cardPortraitShadow.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            cardPortraitShadow.style.unityBackgroundImageTintColor = new Color(0.05f, 0.03f, 0.06f, 0.75f);
            cardRoot.Add(cardPortraitShadow);

            cardPortrait = new VisualElement { pickingMode = PickingMode.Ignore };
            cardPortrait.style.position = Position.Absolute;
            cardPortrait.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            cardRoot.Add(cardPortrait);

            cardFlash = Fill(cardRoot, new Color(1f, 1f, 1f, 0f));
            cardRoot.style.display = DisplayStyle.None;
        }

        private static VisualElement Fill(VisualElement parent, Color c)
        {
            var v = new VisualElement { pickingMode = PickingMode.Ignore };
            v.style.position = Position.Absolute;
            v.style.left = 0; v.style.right = 0; v.style.top = 0; v.style.bottom = 0;
            v.style.backgroundColor = c;
            parent.Add(v);
            return v;
        }

        private static VisualElement Strip(VisualElement parent, Color c)
        {
            var v = new VisualElement { pickingMode = PickingMode.Ignore };
            v.style.backgroundColor = c;
            parent.Add(v);
            return v;
        }

        private void UpdateBossCard()
        {
            float t = Time.time - BossIntro.CardStart, len = BossIntro.CardLength;
            bool show = t >= 0f && t < len;
            cardRoot.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) return;

            string nm = RequiemUI.Clean(BossIntro.CardName).ToUpperInvariant(), sb = RequiemUI.Clean(BossIntro.CardSub).ToUpperInvariant();
            if (cardName.text != nm)
            {
                cardName.text = nm;
                // long names (GORGEMAW, THE ETHER RAM) get covered by the portrait -> font size by length
                cardName.style.fontSize = nm.Length <= 6 ? 180 : nm.Length <= 9 ? 140 : 108;
            }
            if (cardSub.text != sb) cardSub.text = sb;
            Color band = BossIntro.CardColor;
            cardBand.style.backgroundColor = band;
            cardHiTop.style.backgroundColor = Color.Lerp(band, Color.white, 0.45f);
            cardHiBot.style.backgroundColor = Color.Lerp(band, Color.black, 0.45f);

            float w = PanelW, h = PanelH;
            float bandH = Mathf.Round(h * 0.26f), strip = Mathf.Round(bandH * 0.13f);
            cardStripTop.style.height = strip; cardHiTop.style.height = 5; cardBand.style.height = bandH - 10; cardHiBot.style.height = 5;
            cardStripBot.style.height = strip;
            float total = bandH + strip * 2f, cy = h * 0.52f;
            cardTilt.style.top = cy - total * 0.5f;

            float inK = EaseOut(t / 0.28f), outK = EaseOut((t - (len - 0.32f)) / 0.32f);
            float slide = (-1f + inK) * w * 1.2f + outK * w * 1.2f;               // px: in from the left, out to the right
            cardShade.style.opacity = Mathf.Min(Mathf.Clamp01(t / 0.2f), 1f - outK);
            cardTilt.style.translate = new Translate(slide, 0f);
            float flash = t > 0.26f ? Mathf.Clamp01(1f - (t - 0.26f) / 0.14f) : 0f;
            cardFlash.style.backgroundColor = new Color(1f, 1f, 1f, 0.55f * flash);

            // name + epithet: left side, middle of the band; the name "stamps" in (scaled up -> 1)
            float tan = Mathf.Tan(5f * Mathf.Deg2Rad);
            System.Func<float, float> yAt = x => cy + (w * 0.5f - x) * tan;      // band centre at x (band slanted -5°)
            float pop = Mathf.Lerp(1.35f, 1f, EaseOut((t - 0.18f) / 0.28f));
            // name: text centre slightly above the band centre (room for the epithet below); follows the slant at the left edge
            float nameX = w * 0.09f;
            cardName.style.left = nameX + slide; cardName.style.top = yAt(nameX) - 88f;
            cardName.style.scale = new Scale(new Vector3(pop, pop, 1f));
            cardName.style.opacity = Mathf.Clamp01((t - 0.12f) / 0.12f);
            float subX = w * 0.095f;
            cardSub.style.left = subX + slide * 1.1f; cardSub.style.top = yAt(subX) + 70f;   // epithet under the name, inside the band
            cardSub.style.opacity = Mathf.Clamp01((t - 0.4f) / 0.3f) * (1f - outK);

            // portrait: own image (Resources/UI/Requiem/<CardPortrait>) or the current sprite; feet on the band's lower edge, head beyond the band
            Texture2D tex = !string.IsNullOrEmpty(BossIntro.CardPortrait) ? RequiemUI.Tex(BossIntro.CardPortrait) : null;
            var sp = tex == null ? BossIntro.CardSprite : null;
            bool hasArt = tex != null || sp != null;
            cardPortrait.style.display = hasArt ? DisplayStyle.Flex : DisplayStyle.None;
            cardPortraitShadow.style.display = cardPortrait.style.display;
            if (hasArt)
            {
                if (tex != null && cardPortrait.style.backgroundImage.value.texture != tex) cardPortrait.style.backgroundImage = new StyleBackground(tex);
                else if (sp != null && cardPortrait.style.backgroundImage.value.sprite != sp) cardPortrait.style.backgroundImage = new StyleBackground(sp);
                float aw = tex != null ? tex.width : sp.rect.width, ah = tex != null ? tex.height : sp.rect.height;
                if (tex != null && cardPortraitShadow.style.backgroundImage.value.texture != tex) cardPortraitShadow.style.backgroundImage = new StyleBackground(tex);
                else if (sp != null && cardPortraitShadow.style.backgroundImage.value.sprite != sp) cardPortraitShadow.style.backgroundImage = new StyleBackground(sp);
                // large portrait (2.15x the band), feet on the band's lower edge at exactly that x (following the slant), head well beyond the band
                float ph = bandH * 2.15f, pw = ph * aw / Mathf.Max(1f, ah);
                float pin = EaseOut((t - 0.1f) / 0.32f);
                // the portrait stands behind the name: never over the text (measures the laid-out name width; estimated before layout)
                float nameW = cardName.resolvedStyle.width;
                if (float.IsNaN(nameW) || nameW <= 1f) nameW = nm.Length * cardName.resolvedStyle.fontSize * 0.7f;
                float pcx = Mathf.Max(w * 0.68f, w * 0.09f + nameW + pw * 0.5f + 18f);   // wide portraits (Plague Eye) covered the last letters -> leave half the image + a gap
                pcx = Mathf.Min(pcx, w - pw * 0.5f - 20f);
                float pl = pcx - pw * 0.5f + (1f - pin) * w * 0.6f + outK * w * 0.7f;
                float pt = yAt(pcx) + total * 0.5f - ph - strip * 0.2f;
                float breath = 1f + 0.015f * Mathf.Sin(t * 6f);                   // soft breathing so it does not look "pasted on"
                cardPortrait.style.width = pw; cardPortrait.style.height = ph;
                cardPortrait.style.left = pl; cardPortrait.style.top = pt;
                cardPortrait.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(100));
                cardPortrait.style.scale = new Scale(new Vector3(breath, breath, 1f));
                cardPortraitShadow.style.width = pw; cardPortraitShadow.style.height = ph;
                cardPortraitShadow.style.left = pl + 16f; cardPortraitShadow.style.top = pt + 10f;
            }
        }

        private void UpdateHint(MapEncounterDirector dir, float bossTop)
        {
            bool dead = hp != null && hp.IsDead;
            bool show = !dead && Hints.Visible && !TutorialPrompt.Active && !DialogueBox.Active;   // tutorial / dialogue screen: no overlapping text
            hintRoot.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            float baseY = Mathf.Min(PanelH * 0.8f, bossTop - 14f);
            if (Subtitles.Visible && !DialogueBox.Active && dlgRoot.layout.height > 1f)              // hints never cover the subtitle box
                baseY = Mathf.Min(baseY, PanelH - (M + 6) - dlgRoot.layout.height - 16f);
            if (show)
            {
                string t = RequiemUI.Clean(Hints.Text);
                if (hintText.text != t) hintText.text = t;
                hintRoot.style.opacity = Mathf.Clamp01((Hints.Until - Time.time) / 0.35f);
                hintRoot.style.top = baseY - 48f;
            }
            bool rest = dir != null && dir.AtCheckpoint && !dead && InteractionSystem.Focus == null;
            restPrompt.style.display = rest ? DisplayStyle.Flex : DisplayStyle.None;
            if (rest && restText.text != ShrineMarker.RestLabel) restText.text = ShrineMarker.RestLabel;
            bool note = rest && Time.time < ShrineMarker.NoticeUntil && !string.IsNullOrEmpty(ShrineMarker.Notice);
            restSub.style.display = note ? DisplayStyle.Flex : DisplayStyle.None;
            if (note && restSub.text != ShrineMarker.Notice) restSub.text = ShrineMarker.Notice;
            restPrompt.style.top = (show ? baseY - 48f : baseY) - 44f;
        }
    }
}
