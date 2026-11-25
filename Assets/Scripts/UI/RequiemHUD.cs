using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using SoulRequiem.Core;
using SoulRequiem.Gameplay;

namespace SoulRequiem.UI
{
    /// <summary>
    /// "Requiem Gothic" HUD (UI Toolkit). Laid out by the 4 corners, each corner one group of functions (1920x1080 reference frame):
    ///   Top left     : SURVIVAL — portrait medallion, HP bar (trail of HP just lost), small row under the bar: Attack (J) / Roll (Q) + status.
    ///   Bottom left  : SKILLS — 3 equippable skill slots from PlayerLoadout (cooldown sweep, empty slot, sealed).
    ///   Bottom right : FLASKS — glass flask, liquid draining with the flask count, count, key E; Hemorrhage crossed out.
    ///   Top right    : NAVIGATION — zone name, minimap, main quest.
    ///   Bottom centre: COMBAT — boss bar (name above) + hint strip; "rest at the rest point" prompt.
    ///   Full screen  : red vignette below 30% HP, quest target arrow, HP bars over enemies, full map (M).
    /// Image sizes: 1x or a fixed ratio of the source image so pixels stay crisp.
    /// Files of this class: RequiemHUD.cs (fields, Start builds the HUD, Update calls each part, shared draw helpers Box / Group)
    ///   .Vitals.cs (HP, skills, flasks, sword passive) · .Navigation.cs (minimap, quest, target arrow, full map, enemy HP bars)
    ///   .Boss.cs (boss bar, hints, boss name card) · .Cinematic.cs (dialogue, subtitles, choice cards, tutorial screen, skip) · .Drill.cs (phase / station panel)
    /// </summary>
    public partial class RequiemHUD : MonoBehaviour
    {
        private const float M = 32f;                      // screen margin
        private const float HpW = 278f;                   // width of the HP bar fill inside the hud_vitals frame
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
        private VisualElement bagDot, bagGlow;                                  // inventory in the medallion
        private int bagSeenOpens = -1, bagSeenPicked = -1;
        private float bagCheckT;
        private CooldownRing rollCd;
        private Label hpText;
        private float lag = 1f, lagHold;
        private readonly List<Badge> badges = new List<Badge>();
        private VisualElement statusRow;
        // PASSIVE CELL of the held katana: dim icon while waiting, bright + sword-colour outline when active,
        // build / drain ring for timed passives, stack count (Penance / Hunger), name shown 2.5 s on a sword change; threshold mark on the HP bar.
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
        private VisualElement questChecks;                                     // sub-task list of the quest step
        private readonly List<(VisualElement row, VisualElement box, Label text)> checkRows = new List<(VisualElement, VisualElement, Label)>();
        private VisualElement minimapRoot, mapWindow, mapImage, mapPlayer, mapTarget, mapTargetEdge, questPanel, mapHidden;
        private VisualElement bigMap, bigMapImage, bigMapPlayer, bigMapTarget;
        private Label bigMapTitle;
        private string lastQuest = "";

        // boss + hint
        private VisualElement bossRoot, bossFill, bossLag, bossSurf;
        private Label bossName, bossSub;
        // duo — small HP bar of the secondary boss (MapEncounterDirector.BossPartner), above the main boss name
        private VisualElement bossP, bossPFill, bossPLag, bossPGloss, bossPLock;
        private Label bossPName, bossPSeal;
        private float bossPLagV = 1f;
        private Health lastPartner;
        // BARRIER (Health.Barrier, e.g. the Hollow Champion's Soul Armour) — barrier strip over the top half of the main boss HP bar + status label
        private VisualElement armorBack, armorFill, armorGloss, armorFlash, armorIcon;
        private readonly List<VisualElement> armorTicks = new List<VisualElement>();
        private Label armorLabel;
        private float armorShownV;
        // THE DRILL YARD minigame panel (DrillBoard)
        // 3-station panel — objective + clock + morale cells instead of score / combo
        private VisualElement drillRoot, drillFill, drillTime;
        private readonly List<VisualElement> drillPips = new List<VisualElement>();
        private Label drillTitle, drillGoal, drillClock, drillPop, drillResult, drillObjective, drillPipLabel;
        private VisualElement drillTrackL, drillTrackR, drillTrackM;       // tracking bar icons
        private Label drillCommand;                                         // big centre text for Sergeant Says commands
        private float bossLagV = 1f, bossLagHold;
        private Health lastBoss;
        // boss CAST BAR (BossJuice.Cast) — skill name in school colour + a bar filling over the wind-up, flashes on release;
        // the boss HP bar flashes white on every hit
        private VisualElement castRoot, castFill, castTip, castFlash, bossHit;
        private Label castName;
        private float bossPctSeen = 1f, bossHitT;
        private bool castShown;
        private VisualElement hintRoot, restPrompt;
        private Label restSub, restText;
        // rest campfire icons on the minimap / full map (orange = unlit, blue = lit)
        private VisualElement mapFireLayer, bigFireLayer;
        // map fog per zone (MapFog) — "??" over unexplored zones
        private MinimapFogView miniFog, bigFog;
        private readonly List<VisualElement> mapFires = new List<VisualElement>(), bigFires = new List<VisualElement>();
        private readonly List<VisualElement> mapFoes = new List<VisualElement>();   // red dots for stray enemies on the minimap
        private static readonly Color FireUnlit = new Color(1f, 0.62f, 0.25f), FireLit = new Color(0.5f, 0.88f, 1f);
        // boss name card (BossIntro)
        // CHOICE CARDS (ChoiceCard) — spare / kill a boss...
        private VisualElement choiceRoot, choiceShade;
        private Label choiceTitle, choiceSub, choiceFoot;
        private readonly List<VisualElement> choiceCards = new List<VisualElement>();
        private readonly List<Label> choiceNames = new List<Label>();
        private float choiceK, choiceBuiltAt = -1f;
        private VisualElement cardRoot, cardBand, cardShade, cardTilt, cardPortrait, cardPortraitShadow, cardFlash, cardStripTop, cardStripBot, cardHiTop, cardHiBot;
        private VisualElement lbTop, lbBot;          // cinematic black bars during cutscenes
        private VisualElement skipRoot, skipFill;    // "Hold SPACE to skip" prompt bottom right during cutscenes
        private Label skipKey, skipText;
        private float skipK;
        private Label cardName, cardSub;
        // tutorial pause screen (TutorialPrompt) + HUD fade during cutscenes (CutsceneState)
        private VisualElement promptRoot, promptKey;
        private Label promptTitle, promptKeyText, promptSub;
        private float cineK = 1f;
        // dialogue box (DialogueBox)
        private VisualElement dlgRoot;
        private Label dlgName, dlgText, dlgNext;
        private Label hintText;

        // world-space
        private VisualElement waypoint, edgePointer, vignette;
        // more visible quest target — pulsing glow halo + spreading "ping" ring + blinking (minimap, full map, screen-edge arrow)
        private VisualElement mapTargetGlow, mapTargetPing, mapEdgeGlow, bigMapGlow, bigMapPing, wpGlow, edgeGlow;
        private static Texture2D glowTex, ringTex;
        // interaction prompt "[T] Read" floating over the focused object (InteractionSystem.Focus)
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
        private readonly Dictionary<Health, Vector2> enemyLag = new Dictionary<Health, Vector2>();   // (HP trail, seconds held)

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
            BuildChoice();
            BuildDrill();
            BuildLetterbox();
            BuildTutorialPrompt();
            BuildDialogue();
            BuildWaypoint();
            BuildInteractPrompt();
            BuildBigMap();
            if (kit != null) potionCap = Mathf.Max(kit.maxPotions, kit.Potions);
        }

        private void OnDestroy() { if (loadout != null) loadout.Cast -= OnCast; GameInput.MouseOverUI = false; }

        /// <summary>Clickable HUD button — hover: glows + left click no longer counts as an attack; left press = clicks at once.</summary>
        private static void HudClick(VisualElement v, System.Action click, System.Action<bool> hover)
        {
            v.RegisterCallback<PointerEnterEvent>(_ => { GameInput.MouseOverUI = true; hover?.Invoke(true); });
            v.RegisterCallback<PointerLeaveEvent>(_ => { GameInput.MouseOverUI = false; hover?.Invoke(false); });
            v.RegisterCallback<PointerDownEvent>(e => { if (e.button == 0) { click?.Invoke(); e.StopPropagation(); } });
        }

        private static VisualElement Round(VisualElement v, float r)
        {
            v.style.borderTopLeftRadius = r; v.style.borderTopRightRadius = r; v.style.borderBottomLeftRadius = r; v.style.borderBottomRightRadius = r;
            return v;
        }

        // =====================================================================================================  build
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

        // =====================================================================================================  update
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
            UpdateDrill(dt);
            UpdateBossCard();
            UpdateTutorialPrompt();
            UpdateDialogue();
            UpdateChoice(dt);
            UpdateCinematic(dt);
            UpdateSkip(dt);
            UpdateHint(dir, bossTop);
            UpdateWorld(q, menu);
            UpdateInteractPrompt(menu || MapMechanics.ConsoleActive || DialogueBox.Active);
            UpdateBigMap(q, dir);
        }

        private float PanelW => root.resolvedStyle.width > 1 ? root.resolvedStyle.width : 1920f;
        private float PanelH => root.resolvedStyle.height > 1 ? root.resolvedStyle.height : 1080f;
    }
}
