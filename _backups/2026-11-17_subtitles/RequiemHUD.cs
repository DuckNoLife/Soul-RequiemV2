using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using SoulRequiem.Core;
using SoulRequiem.Gameplay;

namespace SoulRequiem.UI
{
    /// <summary>
    /// HUD "Requiem Gothic" (UI Toolkit). Bố cục theo 4 góc, mỗi góc một nhóm chức năng (khung tham chiếu 1920x1080):
    ///   Trên trái  : SINH TỒN — mề đay chân dung, thanh máu (vệt máu vừa mất), hàng nhỏ dưới thanh: Đánh (J) / Lăn (Q) + trạng thái.
    ///   Dưới trái  : CHIÊU — 3 ổ chiêu tháo lắp theo PlayerLoadout (vòng quét hồi chiêu, ô trống, bị phong ấn).
    ///   Dưới phải  : BÌNH MÁU — bình thủy tinh, chất lỏng rút theo số bình, số bình, phím E; Hemorrhage gạch chéo.
    ///   Trên phải  : ĐỊNH HƯỚNG — tên khu, minimap, nhiệm vụ chính.
    ///   Dưới giữa  : TRẬN ĐẤU — thanh boss (tên ở trên) + dải gợi ý; nhắc "nghỉ ở điểm nghỉ".
    ///   Toàn màn hình: viền đỏ khi máu dưới 30%, mũi tên đích nhiệm vụ, thanh máu trên đầu quái, bản đồ lớn (M).
    /// Kích thước ảnh: 1x hoặc tỉ lệ cố định của ảnh gốc để điểm ảnh sắc.
    /// </summary>
    public class RequiemHUD : MonoBehaviour
    {
        private const float M = 32f;                      // lề màn hình
        private static readonly Color HpRed = new Color(0.76f, 0.13f, 0.19f);
        private static readonly Color HpPoison = new Color(0.36f, 0.7f, 0.24f);
        private static readonly Color Lag = new Color(0.94f, 0.84f, 0.63f);
        private static readonly Color BossRed = new Color(0.6f, 0.06f, 0.16f);
        private static readonly Color MobRed = new Color(0.86f, 0.14f, 0.18f);

        private Health hp;
        private PlayerCombat kit;
        private PlayerLoadout loadout;
        private PlayerStatus status;
        private VisualElement root, hud, fx;

        // vitals
        private VisualElement hpFill, hpLag, hpSurf, portraitImg, rollRing;
        private CooldownRing rollCd;
        private Label hpText;
        private float lag = 1f, lagHold;
        private readonly List<Badge> badges = new List<Badge>();
        private VisualElement statusRow;
        // 16/11 (§279): Ô NỘI TẠI của thanh katana đang cầm (thay thước §275): icon mờ khi chờ, sáng + viền màu kiếm khi bật,
        // vòng tích / cạn cho nội tại có thời gian, số tầng (Penance / Hunger), tên hiện 2,5 s khi đổi kiếm; vạch ngưỡng trên thanh máu.
        private VisualElement traitChip, traitGlow, traitIcon, traitHpTick;
        private CooldownRing traitRing;
        private Label traitLabel, traitCount;
        private WeaponTraitKind traitShown = WeaponTraitKind.None;
        private float traitNameT, traitFlash;

        // skills
        private readonly Socket[] sockets = new Socket[PlayerLoadout.SlotCount];

        // flask
        private VisualElement flaskLiquidClip, flaskLiquid, flaskSurface, flaskFlash, flaskSeal, flaskRoot;
        private Label flaskCount, flaskTimer;
        private int lastPotions = -1, potionCap = 3;
        private float flaskFlashT;
        private const float FlaskLiqY0 = 100f, FlaskLiqY1 = 196f;

        // navigation
        private Label zoneLabel, questHead, questTitle, questTarget, questDist, questKills;
        private VisualElement questChecks;                                     // 05/11: danh sách con của bước nhiệm vụ
        private readonly List<(VisualElement row, VisualElement box, Label text)> checkRows = new List<(VisualElement, VisualElement, Label)>();
        private VisualElement minimapRoot, mapWindow, mapImage, mapPlayer, mapTarget, mapTargetEdge, questPanel, mapHidden;
        private VisualElement bigMap, bigMapImage, bigMapPlayer, bigMapTarget;
        private Label bigMapTitle;
        private string lastQuest = "";

        // boss + hint
        private VisualElement bossRoot, bossFill, bossLag, bossSurf;
        private Label bossName, bossSub;
        private float bossLagV = 1f, bossLagHold;
        private Health lastBoss;
        private VisualElement hintRoot, restPrompt;
        private Label restSub, restText;
        // 02/11: biểu tượng lửa trại nghỉ trên minimap / bản đồ lớn (cam = chưa thắp, xanh = đã thắp)
        private VisualElement mapFireLayer, bigFireLayer;
        // 05/11: sương mù bản đồ theo khu (MapFog) — "??" trên khu chưa mở
        private MinimapFogView miniFog, bigFog;
        private readonly List<VisualElement> mapFires = new List<VisualElement>(), bigFires = new List<VisualElement>();
        private static readonly Color FireUnlit = new Color(1f, 0.62f, 0.25f), FireLit = new Color(0.5f, 0.88f, 1f);
        // 02/11: bảng tên boss (BossIntro)
        private VisualElement cardRoot, cardBand, cardShade, cardTilt, cardPortrait, cardPortraitShadow, cardFlash, cardStripTop, cardStripBot, cardHiTop, cardHiBot;
        private VisualElement lbTop, lbBot;          // 02/11: dải đen điện ảnh khi diễn cảnh
        private Label cardName, cardSub;
        // 02/11: màn dừng hướng dẫn (TutorialPrompt) + mờ HUD khi diễn cảnh (CutsceneState)
        private VisualElement promptRoot, promptKey;
        private Label promptTitle, promptKeyText, promptSub;
        private float cineK = 1f;
        // 02/11: khung hội thoại (DialogueBox)
        private VisualElement dlgRoot;
        private Label dlgName, dlgText, dlgNext;
        private Label hintText;

        // world-space
        private VisualElement waypoint, edgePointer, vignette;
        // 04/11: đích nhiệm vụ dễ thấy hơn — quầng sáng phập phồng + vòng "ping" lan ra + nhấp nháy (minimap, bản đồ lớn, mũi tên mép màn hình)
        private VisualElement mapTargetGlow, mapTargetPing, mapEdgeGlow, bigMapGlow, bigMapPing, wpGlow, edgeGlow;
        private static Texture2D glowTex, ringTex;
        // nhắc tương tác "[T] Read" nổi trên vật được chọn (InteractionSystem.Focus)
        private VisualElement iPrompt;
        private Label iKey, iVerb;
        private Label wpDist;
        private readonly List<VisualElement> wpFrames = new List<VisualElement>();
        private readonly List<EnemyBar> enemyBars = new List<EnemyBar>();

        private class Badge { public VisualElement root, icon; public Label time; }
        private class Socket
        {
            public VisualElement root, icon, seal, glow; public CooldownRing ring; public Label cd, key; public SkillDefinition shown;
            public float wasLeft, flash;
        }
        private class EnemyBar { public VisualElement root, fill, lag, shine; public Label num; }
        private readonly Dictionary<Health, Vector2> enemyLag = new Dictionary<Health, Vector2>();   // (vệt máu, giây giữ)

        private void Start()
        {
            var ui = RequiemUI.Instance;
            if (ui == null) { enabled = false; return; }
            root = ui.Root; hud = ui.Hud; fx = ui.Fx;
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null)
            {
                hp = pc.GetComponent<Health>();
                kit = pc.GetComponent<PlayerCombat>();
                loadout = pc.GetComponent<PlayerLoadout>();
                status = pc.GetComponent<PlayerStatus>();
                if (loadout != null) loadout.Cast += OnCast;
            }
            BuildVignette();
            BuildVitals();
            BuildSkills();
            BuildFlask();
            BuildNavigation();
            BuildBossAndHint();
            BuildBossCard();
            BuildLetterbox();
            BuildTutorialPrompt();
            BuildDialogue();
            BuildWaypoint();
            BuildInteractPrompt();
            BuildBigMap();
            if (kit != null) potionCap = Mathf.Max(kit.maxPotions, kit.Potions);
        }

        private void OnDestroy() { if (loadout != null) loadout.Cast -= OnCast; }

        // =====================================================================================================  dựng
        private VisualElement Box(VisualElement parent, float x, float y, float w, float h, Color c)
        {
            var v = new VisualElement { pickingMode = PickingMode.Ignore };
            RequiemUI.Abs(v, x, y, w, h);
            v.style.backgroundColor = c;
            parent.Add(v);
            return v;
        }

        private VisualElement Group(VisualElement parent, string name)
        {
            var g = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            g.style.position = Position.Absolute;
            parent.Add(g);
            return g;
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
            var g = Group(hud, "vitals");
            g.style.left = M; g.style.top = 22;
            // mề đay 154x166, lỗ tròn x29-124, y41-136
            Box(g, 29, 41, 96, 96, new Color(0.08f, 0.05f, 0.11f));
            portraitImg = RequiemUI.Img("hud_portrait", 33, 43, 88, 88, g);
            RequiemUI.Img("medallion", 0, 0, 154, 166, g);
            // thanh máu 9 lát 440x60, rãnh x44..396, y16..43
            var bar = Group(g, "hp");
            bar.style.left = 148; bar.style.top = 52; bar.style.width = 440; bar.style.height = 60;
            Box(bar, 44, 16, 352, 27, new Color(0.11f, 0.06f, 0.1f));
            hpLag = Box(bar, 44, 16, 352, 27, Lag);
            hpFill = RequiemUI.Img("bar_fill", 44, 16, 352, 27, bar);
            hpFill.style.unityBackgroundImageTintColor = HpRed;
            hpSurf = Box(bar, 44, 16, 352, 3, new Color(1f, 0.55f, 0.55f, 0.7f));
            var frame = new VisualElement { pickingMode = PickingMode.Ignore };
            RequiemUI.Abs(frame, 0, 0, 440, 60);
            RequiemUI.Slice(frame, "hp_frame", 44, 16, 44, 17);
            bar.Add(frame);
            hpText = RequiemUI.Text("", "sr-hp-text", bar);
            RequiemUI.Abs(hpText, 44, 14, 352, 30);

            // 16/11 (§279): vạch ngưỡng máu của nội tại (Blood Oath / Last Stand) trên thanh máu
            traitHpTick = Box(bar, 44, 12, 2, 35, new Color(1f, 0.85f, 0.45f, 0.9f));
            traitHpTick.style.display = DisplayStyle.None;

            // hàng nhỏ dưới thanh: Đánh (J) / Lăn (Q) | trạng thái
            statusRow = Group(g, "row");
            statusRow.style.left = 188; statusRow.style.top = 118;
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

        /// <summary>16/11 (§279): ô nội tại cạnh Đánh / Lăn — viền sáng phía sau, icon ic_trait_&lt;kiểu&gt;, vòng tích / cạn, số tầng, tên bên dưới.</summary>
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

        /// Chip nhỏ (Đánh / Lăn): khung 40, icon 32, nắp phím nhỏ ở góc dưới phải.
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
                RequiemUI.Img("skill_socket", 0, 0, 113, 114, s.root);          // khung trước, icon nằm trên lòng ổ
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
            // bảng số bình bên trái cổ bình
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
            // 02/11: số quái của khu đang đánh (đã hạ / tổng)
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

        private void BuildBossAndHint()
        {
            bossRoot = Group(hud, "boss");
            bossRoot.style.bottom = M; bossRoot.style.width = 671; bossRoot.style.height = 80;
            Box(bossRoot, 47, 42, 577, 19, new Color(0.11f, 0.06f, 0.1f));
            bossLag = Box(bossRoot, 47, 42, 577, 19, Lag);
            bossFill = RequiemUI.Img("bar_fill", 47, 42, 577, 19, bossRoot);
            bossFill.style.unityBackgroundImageTintColor = BossRed;
            bossSurf = Box(bossRoot, 47, 42, 577, 3, new Color(0.9f, 0.35f, 0.42f, 0.8f));
            RequiemUI.Img("boss_frame", 0, 0, 671, 80, bossRoot);
            bossName = RequiemUI.Text("", "sr-title sr-boss-name", bossRoot);
            RequiemUI.Abs(bossName, -200, -44, 1071, 40);
            bossSub = RequiemUI.Text("", "sr-small sr-gold sr-right", bossRoot);
            RequiemUI.Abs(bossSub, 380, 2, 240, 24);
            bossRoot.style.display = DisplayStyle.None;

            // hàng phủ cả bề ngang, dải chữ canh giữa bên trong (left:50% + translate làm dải bị bóp còn nửa màn -> lệch phải)
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
            // 02/11: lần đầu thắp lửa trại: dòng giải thích ngay dưới ô nhắc (thay cho gợi ý thứ hai ở dưới màn hình)
            restSub = RequiemUI.Text("", "sr-prompt-text", restPrompt);
            restSub.style.fontSize = 19; restSub.style.marginTop = 3;
            restSub.style.paddingLeft = 14; restSub.style.paddingRight = 14; restSub.style.paddingTop = 3; restSub.style.paddingBottom = 4;
            restSub.style.backgroundColor = new Color(0.055f, 0.03f, 0.086f, 0.7f);
            restSub.style.color = new Color(0.78f, 0.92f, 1f);
            restSub.style.display = DisplayStyle.None;
            restPrompt.style.display = DisplayStyle.None;
        }

        private void BuildInteractPrompt()
        {
            // hàng rộng 480 canh giữa quanh điểm neo (tránh left:50%+translate bóp bề rộng)
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
            fx.Insert(fx.IndexOf(waypoint), wpGlow);                     // quầng nằm dưới hình đích
            edgePointer = RequiemUI.Img("hud_pointer", 0, 0, 36, 36, fx);
            wpDist = RequiemUI.Text("", "sr-small sr-gold", fx);
            wpDist.style.position = Position.Absolute;
            waypoint.style.display = edgePointer.style.display = wpDist.style.display = DisplayStyle.None;
            wpGlow.style.display = edgeGlow.style.display = DisplayStyle.None;
        }

        // ---- 04/11: quầng sáng / vòng ping cho đích nhiệm vụ
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

        /// <summary>Đặt quầng (tâm tại `c`) + phập phồng; `ping` = vòng lan rộng rồi tắt, lặp mỗi 1,2 s.</summary>
        private static void Pulse(VisualElement glow, VisualElement ping, Vector2 c, float baseSize, float phase = 0f)
        {
            bool calm = GameSettings.ReduceFlashing;
            float t = Time.unscaledTime + phase;
            if (glow != null)
            {
                glow.style.display = DisplayStyle.Flex;
                glow.style.left = c.x - baseSize / 2f; glow.style.top = c.y - baseSize / 2f;
                // 04/11 (người dùng: bớt chói): quầng mờ, phập phồng nhẹ
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

        /// <summary>Nhấp nháy (bỏ qua khi bật Reduce flashing).</summary>
        private static void Blink(VisualElement v, float hz = 1.5f)
        {
            if (v == null) return;
            v.style.opacity = GameSettings.ReduceFlashing ? 1f : 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * hz * Mathf.PI * 2f);   // nhấp nháy dịu
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

        // =====================================================================================================  cập nhật
        private void Update()
        {
            if (root == null || root.panel == null) return;
            float dt = Time.unscaledDeltaTime;
            var dir = MapEncounterDirector.Instance;
            var q = QuestTracker.Instance;
            bool menu = RequiemMenus.Open;
            hud.style.display = menu ? DisplayStyle.None : DisplayStyle.Flex;

            UpdateVitals(dt);
            UpdateSkills(dt);
            UpdateFlask(dt);
            UpdateNavigation(q, dir);
            float bossTop = UpdateBoss(dir, dt);
            UpdateBossCard();
            UpdateTutorialPrompt();
            UpdateDialogue();
            UpdateCinematic(dt);
            UpdateHint(dir, bossTop);
            UpdateWorld(q, menu);
            UpdateInteractPrompt(menu || MapMechanics.ConsoleActive || DialogueBox.Active);
            UpdateBigMap(q, dir);
        }

        private float PanelW => root.resolvedStyle.width > 1 ? root.resolvedStyle.width : 1920f;
        private float PanelH => root.resolvedStyle.height > 1 ? root.resolvedStyle.height : 1080f;

        private void UpdateVitals(float dt)
        {
            if (hp == null) return;
            float pct = hp.Percent;
            if (pct >= lag) { lag = pct; lagHold = 0.45f; }
            else if (lagHold > 0f) lagHold -= dt;
            else lag = Mathf.MoveTowards(lag, pct, dt * 0.6f);
            hpFill.style.width = 352f * pct;
            hpSurf.style.width = 352f * pct;
            hpLag.style.width = 352f * lag;
            Color c = hp.IsPoisoned ? HpPoison : HpRed;
            if (pct < 0.3f && !hp.IsDead && !GameSettings.ReduceFlashing)
                c = Color.Lerp(c, new Color(1f, 0.5f, 0.45f), 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 9f));
            hpFill.style.unityBackgroundImageTintColor = c;
            string t = $"{Mathf.CeilToInt(hp.Current)} / {hp.Max:0}";
            if (hpText.text != t) hpText.text = t;
            portraitImg.style.unityBackgroundImageTintColor = hp.IsDead ? new Color(0.4f, 0.4f, 0.4f) :
                                                              hp.IsStunned ? new Color(1f, 0.92f, 0.6f) : Color.white;
            // Lăn: vòng hồi
            if (kit != null) rollCd.Progress = kit.rollCooldown > 0f ? kit.RollCooldownLeft / kit.rollCooldown : 0f;

            // trạng thái
            int n = 0;
            if (kit != null && kit.GodMode) SetBadge(n++, "ic_god", "GOD");
            if (hp.IsStunned) SetBadge(n++, "ic_stun", "");
            if (hp.IsPoisoned) SetBadge(n++, "ic_poison", "");
            if (status == null && hp != null) status = hp.GetComponent<PlayerStatus>();
            if (status != null)
            {
                if (status.SlowLeft > 0f) SetBadge(n++, "ic_slow", Secs(status.SlowLeft));
                if (status.SilenceLeft > 0f) SetBadge(n++, "ic_silence", Secs(status.SilenceLeft));
                if (status.HealBlockLeft > 0f) SetBadge(n++, "ic_bleed", Secs(status.HealBlockLeft));
            }
            for (int i = n; i < badges.Count; i++) badges[i].root.style.display = DisplayStyle.None;

            UpdateTraitGauge();

            // viền đỏ máu thấp
            float k = (!hp.IsDead && pct < 0.3f && GameSettings.LowHpVignette) ? 1f - pct / 0.3f : 0f;
            float beat = GameSettings.ReduceFlashing ? 0.8f : 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * (3f + 3f * k)));
            vignette.style.opacity = k > 0f ? (0.3f + 0.5f * k) * beat : 0f;
        }

        /// <summary>16/11 (§275): màu thước theo kiểu nội tại (theo màu thanh kiếm / boss nguồn gốc).</summary>
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

        /// <summary>16/11 (§279): ô nội tại. Chỉ nội tại có thời gian (Counterstroke / Vigil / Accord) mới có vòng; ngưỡng máu vẽ trên thanh máu.</summary>
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
            if (kind != traitShown)                                   // đổi kiếm: icon mới, ô loé, tên hiện 2,5 s
            {
                traitShown = kind;
                var tex = RequiemUI.Tex("ic_trait_" + kind.ToString().ToLowerInvariant());
                if (tex == null) tex = RequiemUI.Tex("ic_attack");
                if (tex != null) traitIcon.style.backgroundImage = new StyleBackground(tex);
                traitLabel.text = WeaponTraits.Title(kit).ToUpperInvariant();
                traitNameT = 2.5f; traitFlash = 1f;
            }
            var col = TraitColor(kind);

            // vòng: phần tối = phần chưa tích / đã cạn
            bool timed = kind == WeaponTraitKind.Counterstroke || kind == WeaponTraitKind.Vigil || kind == WeaponTraitKind.Accord;
            traitRing.Progress = timed && (active || kind == WeaponTraitKind.Vigil) ? 1f - Mathf.Clamp01(fill) : 0f;

            // số tầng
            string count = "";
            if (kind == WeaponTraitKind.Penance && kit.PenanceStacks > 0) count = kit.PenanceStacks.ToString();
            else if (kind == WeaponTraitKind.Hunger && EnemyStatus.MarkedAlive > 0) count = EnemyStatus.MarkedAlive.ToString();
            if (traitCount.text != count) traitCount.text = count;

            // mờ khi chờ, sáng + viền màu kiếm khi bật
            traitIcon.style.unityBackgroundImageTintColor = active ? Color.white : new Color(0.42f, 0.4f, 0.46f, 1f);
            float pulse = active ? (GameSettings.ReduceFlashing ? 0.35f : 0.32f + 0.18f * Mathf.Sin(Time.unscaledTime * 6f)) : 0f;
            traitFlash = Mathf.Max(0f, traitFlash - dt * 1.6f);
            float a = Mathf.Max(pulse, GameSettings.ReduceFlashing ? 0f : traitFlash * 0.8f);
            traitGlow.style.backgroundColor = new Color(col.r, col.g, col.b, a);

            // tên nội tại khi vừa đổi kiếm
            traitNameT = Mathf.Max(0f, traitNameT - dt);
            traitLabel.style.opacity = Mathf.Clamp01(traitNameT / 0.6f);
            traitLabel.style.color = new StyleColor(Color.Lerp(col, Color.white, 0.35f));

            // vạch ngưỡng máu
            float thr = WeaponTraits.HpThreshold(kit);
            if (thr > 0f && thr < 1f)
            {
                traitHpTick.style.display = DisplayStyle.Flex;
                traitHpTick.style.left = 44f + 352f * thr - 1f;
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
            if (slot >= 0 && slot < sockets.Length) sockets[slot].flash = -0.6f;     // nháy tối khi vừa dùng
        }

        private void UpdateSkills(float dt)
        {
            bool silenced = status != null && status.Silenced;
            // 02/11: ô chiêu trống chỉ hiện khi người chơi còn chiêu chưa lắp (để biết có thể gắn ở lửa trại) — trước đây 3 ô trống
            // nằm suốt Map 1, trông như game thiếu nội dung. Ô vừa có chiêu thì loé sáng khi hiện ra.
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
                if (s.wasLeft > 0f && left <= 0f && sk != null) s.flash = 1f;           // vừa hồi xong: loé
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
                // Khung bám theo người chơi nhưng KHÔNG trượt ra ngoài mép ảnh -> hết viền đen khi đứng sát rìa map.
                // Ở rìa: ảnh đứng yên, mũi tên người chơi rời tâm và chạy về phía mép.
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
                // lửa trại nghỉ
                int nf = 0;
                if (!hidden)
                    foreach (var sh in ShrineMarker.All)
                    {
                        if (sh == null) continue;
                        if (fog != null && !fog.IsClear(q.WorldToTile(sh.transform.position))) continue;   // lửa trại trong sương: chưa biết
                        Vector2 fp = new Vector2(ox, oy) + q.WorldToTile(sh.transform.position) * k;
                        if (fp.x < 4f || fp.x > view - 4f || fp.y < 4f || fp.y > view - 4f) continue;
                        var ic = FireIcon(mapFires, mapFireLayer, nf++, 14f);
                        ic.style.left = fp.x - 7f; ic.style.top = fp.y - 9f;
                        ic.style.unityBackgroundImageTintColor = sh.Lit ? FireLit : FireUnlit;
                    }
                for (int i = nf; i < mapFires.Count; i++) mapFires[i].style.display = DisplayStyle.None;
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

            // nhiệm vụ
            questPanel.style.display = q.HasQuest ? DisplayStyle.Flex : DisplayStyle.None;
            questPanel.style.top = q.Minimap != null ? 70 + 275 + 8 : 70;
            if (!q.HasQuest) return;
            bool fresh = Time.time - q.StepChangedAt < 3f;
            string head = fresh ? "QUEST UPDATED" : q.IsSide ? "SIDE QUEST" : "MAIN QUEST";   // 16/11 (§267): map bí mật = nhiệm vụ phụ
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

            // 02/11: bước "dọn khu" -> hiện số quái đã hạ / tổng khi khu đang đánh
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

        /// <summary>05/11: vẽ danh sách con (ô tích vẽ bằng viền USS — không phụ thuộc phông có ký tự ☐/☑).
        /// Xong = ô đặc vàng + chữ mờ; đang được mũi tên chỉ = chữ sáng; còn thiếu điều kiện = "locked".</summary>
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

        private float UpdateBoss(MapEncounterDirector dir, float dt)
        {
            var boss = dir != null ? dir.Boss : null;
            bool show = boss != null && boss.Health != null && !boss.Health.IsDead && !BossIntro.CardActive;
            bossRoot.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) return PanelH;
            if (boss.Health != lastBoss) { lastBoss = boss.Health; bossLagV = boss.Health.Percent; }
            float pct = boss.Health.Percent;
            if (pct >= bossLagV) { bossLagV = pct; bossLagHold = 0.45f; }
            else if (bossLagHold > 0f) bossLagHold -= dt;
            else bossLagV = Mathf.MoveTowards(bossLagV, pct, dt * 0.5f);
            bossFill.style.width = 577f * pct; bossSurf.style.width = 577f * pct; bossLag.style.width = 577f * bossLagV;
            // giữa màn hình, nhưng không đè cụm chiêu bên trái / bình máu bên phải (màn hẹp thì thu nhỏ)
            const float bw = 671f;
            float w = PanelW, minLeft = M + 3 * 125 + 24, maxRight = w - M - 96 - 70;
            float scale = Mathf.Clamp((maxRight - minLeft) / bw, 0.55f, 1f);
            float sw = bw * scale;
            float vis = Mathf.Clamp((w - sw) / 2f, minLeft, Mathf.Max(minLeft, maxRight - sw));
            bossRoot.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(100));
            bossRoot.style.scale = new Scale(new Vector3(scale, scale, 1f));
            bossRoot.style.left = vis - (bw - sw) / 2f;
            string name = boss.displayName ?? "", sub = "";
            int cut = name.IndexOf('—');
            if (cut >= 0) { sub = name.Substring(cut + 1).Trim(); name = name.Substring(0, cut).Trim(); }
            name = RequiemUI.Clean(name); sub = RequiemUI.Clean(sub);
            if (bossName.text != name) bossName.text = name;
            if (bossSub.text != sub) bossSub.text = sub;
            return PanelH - M - 80f * scale - 48f;
        }

        /// <summary>02/11 (v3, kiểu Soul Knight, chỉnh sau khi xem trong Unity): nền tối 55%, dải nghiêng -5° cao ~26% màn hình (viền tối dày
        /// trên/dưới + vạch sáng/tối ở mép dải màu), tên boss 180px trắng viền tối, biệt danh phía trên, chân dung riêng của boss
        /// (Resources/UI/Requiem/&lt;portrait&gt;, viền trắng) to gấp ~1.6 chiều cao dải, chân đứng trên mép dưới dải, đầu vượt khỏi dải;
        /// lúc dải đáp: chớp trắng 0.12 giây. Trượt vào 0.28 giây, giữ, trượt ra 0.32 giây.</summary>
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

            // bóng chân dung (cùng ảnh, tô đen) lệch xuống-phải -> tạo chiều sâu như thẻ Soul Knight
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

        private static float EaseOut(float u) { u = Mathf.Clamp01(u); return 1f - (1f - u) * (1f - u) * (1f - u); }

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
                // 03/11 (chạy thử Map 2): tên dài (GORGEMAW, THE ETHER RAM) bị chân dung che -> cỡ chữ theo độ dài
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
            float slide = (-1f + inK) * w * 1.2f + outK * w * 1.2f;               // px: vào từ trái, ra bên phải
            cardShade.style.opacity = Mathf.Min(Mathf.Clamp01(t / 0.2f), 1f - outK);
            cardTilt.style.translate = new Translate(slide, 0f);
            float flash = t > 0.26f ? Mathf.Clamp01(1f - (t - 0.26f) / 0.14f) : 0f;
            cardFlash.style.backgroundColor = new Color(1f, 1f, 1f, 0.55f * flash);

            // tên + biệt danh: bên trái, giữa dải; tên "dập" vào (phóng to -> 1)
            float tan = Mathf.Tan(5f * Mathf.Deg2Rad);
            System.Func<float, float> yAt = x => cy + (w * 0.5f - x) * tan;      // tâm dải tại hoành độ x (dải nghiêng -5°)
            float pop = Mathf.Lerp(1.35f, 1f, EaseOut((t - 0.18f) / 0.28f));
            // tên: tâm chữ hơi trên tâm dải (chừa chỗ biệt danh bên dưới); bám độ nghiêng tại mép trái
            float nameX = w * 0.09f;
            cardName.style.left = nameX + slide; cardName.style.top = yAt(nameX) - 88f;
            cardName.style.scale = new Scale(new Vector3(pop, pop, 1f));
            cardName.style.opacity = Mathf.Clamp01((t - 0.12f) / 0.12f);
            float subX = w * 0.095f;
            cardSub.style.left = subX + slide * 1.1f; cardSub.style.top = yAt(subX) + 70f;   // biệt danh nằm dưới tên, trong dải
            cardSub.style.opacity = Mathf.Clamp01((t - 0.4f) / 0.3f) * (1f - outK);

            // chân dung: ảnh riêng (Resources/UI/Requiem/<CardPortrait>) hoặc sprite hiện tại; chân trên mép dưới dải, đầu vượt khỏi dải
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
                // chân dung lớn (2,15 lần dải), chân đặt trên mép dưới dải tại đúng hoành độ (theo độ nghiêng), đầu vượt hẳn khỏi dải
                float ph = bandH * 2.15f, pw = ph * aw / Mathf.Max(1f, ah);
                float pin = EaseOut((t - 0.1f) / 0.32f);
                // chân dung đứng sau tên: không đè lên chữ (đo bề rộng tên đã dàn trang; chưa có thì ước lượng)
                float nameW = cardName.resolvedStyle.width;
                if (float.IsNaN(nameW) || nameW <= 1f) nameW = nm.Length * cardName.resolvedStyle.fontSize * 0.7f;
                float pcx = Mathf.Max(w * 0.68f, w * 0.09f + nameW + pw * 0.5f + 18f);   // 03/11: chân dung rộng (Plague Eye) che chữ cuối -> chừa đủ nửa ảnh + khe
                pcx = Mathf.Min(pcx, w - pw * 0.5f - 20f);
                float pl = pcx - pw * 0.5f + (1f - pin) * w * 0.6f + outK * w * 0.7f;
                float pt = yAt(pcx) + total * 0.5f - ph - strip * 0.2f;
                float breath = 1f + 0.015f * Mathf.Sin(t * 6f);                   // nhịp thở nhẹ cho đỡ "dán ảnh"
                cardPortrait.style.width = pw; cardPortrait.style.height = ph;
                cardPortrait.style.left = pl; cardPortrait.style.top = pt;
                cardPortrait.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(100));
                cardPortrait.style.scale = new Scale(new Vector3(breath, breath, 1f));
                cardPortraitShadow.style.width = pw; cardPortraitShadow.style.height = ph;
                cardPortraitShadow.style.left = pl + 16f; cardPortraitShadow.style.top = pt + 10f;
            }
        }

        /// <summary>02/11: màn dừng hướng dẫn — phủ tối, tên hành động lớn, ô phím viền vàng nhấp nháy, dòng giải thích.</summary>
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
            // nhấp nháy theo thời gian thật (game đang dừng): viền vàng <-> trắng, ô phím phập phồng
            float u = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 7f);
            Color edge = Color.Lerp(RequiemUI.Gold, Color.white, u);
            promptKey.style.borderTopColor = edge; promptKey.style.borderBottomColor = edge;
            promptKey.style.borderLeftColor = edge; promptKey.style.borderRightColor = edge;
            float sc = 1f + 0.08f * u;
            promptKey.style.scale = new Scale(new Vector3(sc, sc, 1f));
            promptKeyText.style.color = Color.Lerp(Color.white, RequiemUI.Gold, u);
        }

        /// <summary>02/11: đang diễn cảnh -> mờ HUD (trừ dải gợi ý, bảng tên boss, màn hướng dẫn) để không che bóng thoại.</summary>
        /// <summary>02/11: 2 dải đen trên/dưới (kiểu điện ảnh) trượt vào khi diễn cảnh — dấu hiệu rõ "đang xem cảnh", HUD ẩn đi.</summary>
        private void BuildLetterbox()
        {
            lbTop = Fill(hud, Color.black); lbTop.style.bottom = StyleKeyword.Auto;
            lbBot = Fill(hud, Color.black); lbBot.style.top = StyleKeyword.Auto;
        }

        private void UpdateCinematic(float dt)
        {
            cineK = Mathf.MoveTowards(cineK, CutsceneState.Active ? 0f : 1f, dt * 4f);
            foreach (var c in hud.Children())
            {
                if (c == hintRoot || c == cardRoot || c == promptRoot || c == dlgRoot || c == lbTop || c == lbBot) continue;
                c.style.opacity = cineK;
            }
            if (waypoint != null) waypoint.style.opacity = cineK;
            if (edgePointer != null) edgePointer.style.opacity = cineK;
            if (iPrompt != null) iPrompt.style.opacity = cineK;
            foreach (var b in enemyBars) if (b != null && b.root != null) b.root.style.opacity = cineK;   // thanh máu quái không đè lên cảnh
            float bar = Mathf.Round(PanelH * 0.09f * EaseOut(1f - cineK));
            lbTop.style.height = bar; lbBot.style.height = bar;
            lbTop.style.display = lbBot.style.display = bar > 0.5f ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>02/11: khung hội thoại (DialogueBox) — giữa đáy màn hình, nền tối viền vàng, tên người nói, chữ chạy, [T] nhấp nháy.</summary>
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
            dlgRoot.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
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

        private void UpdateHint(MapEncounterDirector dir, float bossTop)
        {
            bool dead = hp != null && hp.IsDead;
            bool show = !dead && Hints.Visible && !TutorialPrompt.Active && !DialogueBox.Active;   // màn hướng dẫn / hội thoại: không chồng chữ
            hintRoot.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            float baseY = Mathf.Min(PanelH * 0.8f, bossTop - 14f);
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

        private void UpdateWorld(QuestTracker q, bool menu)
        {
            var cam = Camera.main;
            var panel = root.panel;
            // mũi tên đích nhiệm vụ
            bool wp = !menu && cam != null && q != null && q.HasPlayer && q.HasTarget && !q.BigMap && q.HiddenAt(q.PlayerTile) == null
                      && hp != null && !hp.IsDead && !CutsceneState.Active;   // 16/11: không hiện mũi tên đích khi đang diễn cảnh
            float dist = wp ? Vector2.Distance(q.PlayerTile, q.TargetTile) : 0f;
            if (wp && dist < 2f) wp = false;
            var med = MapEncounterDirector.Instance;
            if (wp && med != null && med.TargetReachedInFight(q.TargetWorld)) wp = false;   // 04/11: đã tới khu đang đánh
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
                    // (04/11: bỏ quầng quanh cờ đích trên màn hình — quá chói)
                }
                else
                {
                    Vector2 c = new Vector2(W / 2f, H / 2f), d = sp - c;
                    if (vp.z < 0f) d = -d;
                    float t = Mathf.Min((c.x - mg) / Mathf.Max(0.001f, Mathf.Abs(d.x)), (c.y - mg) / Mathf.Max(0.001f, Mathf.Abs(d.y)));
                    Vector2 e = c + d * t;
                    edgePointer.style.display = wpDist.style.display = DisplayStyle.Flex;
                    edgePointer.style.left = e.x - 18f; edgePointer.style.top = e.y - 18f;
                    Blink(edgePointer, 1.2f);                                // mũi tên mép màn hình: không quầng, nhấp nháy dịu
                    float pulse = GameSettings.ReduceFlashing ? 1f : 1f + 0.1f * Mathf.Sin(Time.unscaledTime * 5f);
                    edgePointer.style.scale = new Scale(new Vector3(pulse, pulse, 1f));
                    edgePointer.style.rotate = new Rotate(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                    Vector2 inward = (c - e).normalized * 38f;
                    wpDist.style.left = e.x + inward.x - 24f; wpDist.style.top = e.y + inward.y - 12f;
                }
            }

            // thanh máu quái thường
            int used = 0;
            if (!menu && cam != null)
                foreach (var e in EnemyHealthBar.All)
                {
                    if (e == null || e.Health == null || e.Health.IsDead || !e.isActiveAndEnabled) continue;
                    if (e.Health.Invulnerable && e.TryGetComponent<EnemyAI>(out var ai) && ai.Held) continue;   // đang ngất / bỏ chạy: không phải mục tiêu
                    Vector3 vp = cam.WorldToViewportPoint(e.HeadWorld);
                    if (vp.z < 0f || vp.x < -0.05f || vp.x > 1.05f || vp.y < -0.05f || vp.y > 1.1f) continue;
                    Vector2 sp = RuntimePanelUtils.CameraTransformWorldToPanel(panel, e.HeadWorld, cam);
                    var bar = Bar(used++);
                    bar.root.style.display = DisplayStyle.Flex;
                    bar.root.style.left = Mathf.Round(sp.x - EbW / 2f); bar.root.style.top = Mathf.Round(sp.y - EbH - 6f);
                    float pct = e.Health.Percent;
                    // vệt máu vừa mất (giữ 0,4 s rồi trượt)
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

        // thanh máu quái: 84x14 (khung tham chiếu) — nền mực, viền đen 2px, gạch vàng trên, đỏ có vệt sáng, vệt máu vừa mất; số máu ở trên
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

        /// <summary>Vị trí ảnh minimap trong khung: muốn đặt ở <paramref name="want"/> (người chơi ở tâm) nhưng kẹp để ảnh luôn phủ kín khung.
        /// Ảnh nhỏ hơn khung thì đặt giữa.</summary>
        private static VisualElement FireIcon(List<VisualElement> pool, VisualElement layer, int i, float size)
        {
            while (pool.Count <= i) pool.Add(RequiemUI.Img("map_campfire", 0, 0, size, size, layer));
            pool[i].style.display = DisplayStyle.Flex;
            return pool[i];
        }

        private static float MinimapOffset(float want, float img, float view)
            => img <= view ? (view - img) / 2f : Mathf.Clamp(want, view - img, 0f);

        private void UpdateBigMap(QuestTracker q, MapEncounterDirector dir)
        {
            bool show = q != null && q.BigMap && q.Minimap != null && !RequiemMenus.Open;
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
            // chỉ hiện phần "view" của ảnh: phóng ảnh và dịch ngược
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
