using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// THE BRAZEN RELIQUARY - the "brass" half of the Reliquary Wardens duo fight (Map 4; MEGAPACK Mimic_LVL4 sprite: a brass cannon with an eye,
    /// Fire / Vent / Bite animations made with PixelLab). Fight design "THE RELIQUARY LISTS":
    ///   PHASE 1 "ON THE TURRET" (while the Hollow Champion is alive): no shield.
    ///     The Engine sits on a HIGH TURRET PEDESTAL in the middle of the field (marker duo_core, image engine_pedestal) - Ren cannot reach it (hitting the base: sparks + hint),
    ///     and the Champion's fire-coloured soul tether links to it (it is what raises the pedestal). A harassment turret: Grapeshot, Mortar, Turret Sweep (Champion under 50% HP),
    ///     INTERDICTION - when the Champion calls a weapon rack, mortars BLOCK Ren's path to the rack (ties into the race minigame).
    ///     Overheats after 4 skills -> silent for 2.4 s (breathing room).
    ///   PHASE TRANSITION (comic): the Champion falls -> the pedestal lowers halfway and JAMS, the cannon looks around "...?", hops twice to push it down -> the pedestal collapses,
    ///     the cannon faceplants, coughs smoke, coins scatter; the reel pops up "INSERT SOUL." -> one shot, then the fight resumes.
    ///   PHASE 2 "JACKPOT ENGINE": off the pedestal, walks + MELEE (Maw Snap bite, Brass Ram charge, muzzle blast when pinned).
    ///     A REEL (JackpotReel) grows on its head - every 8.5 s it spins while the Engine stands and sprays coin rings; the symbol it stops on is the skill fired:
    ///       GOBLIN  - launches 3-4 torch goblins (Goblin_Torch from Map 1) that arc down around Ren.
    ///       BOMB    - 6-8 bombs drop around Ren, fuses burn, then they explode in turn.
    ///       SKULL   - 6-8 ghost skulls burst out and slowly chase Ren (can be slashed).
    ///       COIN    - coin spiral with 3-4 arms for 2.2 s.
    ///       BOLT    - 6-8 lightning bolts fall chasing Ren (leading his movement).
    ///       ANVIL   - a shadow follows Ren's feet for 1.4 s, then locks, an iron anvil falls + shock wave.
    ///       HEART   - the Engine heals 7% HP + overdrive (faster skills for 8 s).
    ///       BUST    - backfire inside: loses 6% HP, stunned 3.2 s (x2 damage).
    ///     REEL MINIGAME: hitting the Engine while the reel spins -> the reel JOLTS one extra step. The strip is fixed and the last 3 steps are slow -> count steps to push
    ///     the reel into BUST and away from HEART. HP ≤ 45%: DOUBLE DOWN - after each spin (except BUST) it spins again, faster.
    /// Class files: BrazenReliquarySkills.cs (settings, setup, skill choice, helpers) · BrazenReliquarySkills.Attacks.cs (each skill as a coroutine).
    /// </summary>
    public sealed partial class BrazenReliquarySkills : BossSkillsBase
    {
        public float shellSpeed = 2.7f, grapeDamage = 10f, mortarDamage = 16f, ramDamage = 22f, ramSpeed = 4.2f, biteDamage = 22f;
        public int heatMax = 4;
        private int heat;
        private bool venting, unbound, transforming, cleaned, reelHinted, interdictPending;
        private float nextWardHint, nextReel, nextJolt, overclockUntil;
        private Vector2 home, interdictAt;
        private HollowChampionSkills partner;
        private SpriteVfx tether;
        private SpriteRenderer pedestal;                 // high turret pedestal in the middle of the field
        private SpriteMask pedMask;
        private BoxCollider2D pedCol;
        /// <summary>Collider of the pedestal base (the Champion paths around it). null once the pedestal has collapsed.</summary>
        internal Collider2D PedestalCollider => pedCol != null && pedCol.enabled ? pedCol : null;
        private Collider2D[] bodyCols;
        private Vector2 baseHome;                        // pedestal base
        private bool lowering;
        private int raisedOrder;
        private const float Lift = 1.22f, PedScale = 1f;   // crypt stone tower 128x134, 1:1 scale (no mixels); top surface 1.22 units high
        private JackpotReel reel;
        private readonly List<EnemyAI> goblins = new List<EnemyAI>();
        private static readonly Color Brass = new Color(1f, 0.6f, 0.25f), Soul = new Color(0.72f, 0.42f, 1f), Molten = new Color(1f, 0.55f, 0.45f);

        private bool PartnerAlive => partner != null && partner.Alive;
        // hp is only set on the first Act tick -> if the partner asks earlier, fetch Health directly
        internal bool Alive { get { if (hp == null) hp = GetComponent<Health>(); return hp != null && !hp.IsDead; } }
        private static MapMechanics MM => MapMechanics.Instance;

        protected override void OnEngage()
        {
            preferredRange = 1.4f;
            repositionCooldown = 3f;
            Use("grape", 1.5f); Use("mortar", 3.2f); Use("ram", 5f); Use("sweep", 6f);
            partner = FindFirstObjectByType<HollowChampionSkills>();
            var mm = MM;
            var arena = mm != null ? mm.MechRect("duo_arena") : null;
            if (arena.HasValue) { center = arena.Value.center; arenaRadii = arena.Value.size * 0.5f; }
            var core = mm != null ? mm.MechRect("duo_core") : null;
            baseHome = core.HasValue ? new Vector2(core.Value.center.x, core.Value.yMin + 0.1f) : Pos;
            home = baseHome;
            PlayerCombat.Struck += OnStruck;
            if (PartnerAlive) RaisePedestal();
            else { unbound = true; ai.TeleportTo(home); AttachReel(); }
        }

        private static Sprite whiteSprite;
        private static Sprite White()
        {
            if (whiteSprite != null) return whiteSprite;
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var px = new Color[16]; for (int i = 0; i < 16; i++) px[i] = Color.white;
            tex.SetPixels(px); tex.Apply();
            return whiteSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>Build the high pedestal at duo_core, put the Engine on top, turn off its body collider (nobody can reach it), tether to the Champion.</summary>
        private void RaisePedestal()
        {
            edgeLimit = 99f; pinnedTimeMax = 99f;
            var go = new GameObject("Engine Pedestal");
            go.transform.position = baseHome;
            go.transform.localScale = new Vector3(PedScale, PedScale, 1f);
            pedestal = go.AddComponent<SpriteRenderer>();
            pedestal.sprite = WardenStatueMechanism.Load("VFX/props/engine_pedestal");
            pedestal.spriteSortPoint = SpriteSortPoint.Pivot;
            if (ai.Body != null) { pedestal.sortingLayerID = ai.Body.sortingLayerID; pedestal.sortingOrder = ai.Body.sortingOrder; pedestal.sharedMaterial = ai.Body.sharedMaterial; }
            pedestal.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;   // while lowering: the part below the floor is clipped
            var mg = new GameObject("Pedestal Mask");
            mg.transform.position = baseHome + Vector2.up * 1.48f;
            mg.transform.localScale = new Vector3(75f, 75f, 1f);              // 4 px @100 -> 3 x 3 units, bottom edge = floor
            pedMask = mg.AddComponent<SpriteMask>();
            pedMask.sprite = White();
            var cg = new GameObject("Pedestal Collider");
            cg.transform.position = baseHome + Vector2.up * 0.16f;
            pedCol = cg.AddComponent<BoxCollider2D>();
            pedCol.size = new Vector2(1.1f, 0.32f);
            bodyCols = GetComponentsInChildren<Collider2D>();
            foreach (var c in bodyCols) if (c != null) c.enabled = false;
            if (ai.Body != null) { raisedOrder = 2; ai.Body.sortingOrder += raisedOrder; }
            home = baseHome + Vector2.up * Lift;
            ai.TeleportTo(home);
            tether = Ray("m3_ray_warn", Pos, Pos + Vector2.right, 0.62f, 0.8f, Order - 3, 12f, 9999f);
            if (tether != null) tether.Tint(new Color(0.75f, 0.45f, 1f, 0.55f));
        }

        private void AttachReel()
        {
            if (reel == null) reel = JackpotReel.Attach(transform, 1.0f, Layer, Order + 6);
            edgeLimit = 0.86f; pinnedTimeMax = 1.2f;
            ai.moveSpeed = Mathf.Max(ai.moveSpeed, 1.1f);                // off the pedestal: walks, closes in to bite
            nextReel = Time.time + 2.5f;
        }

        protected override int PhaseFor(float p)
        {
            if (!engaged || partner == null || partner.Alive) return 1;
            return p <= 0.45f ? 3 : 2;
        }
        protected override float CooldownMul()
        {
            // phase 2 bullets were too dense -> longer cooldowns (phase 2 x1.25, phase 3 x1.05)
            float m = phase == 3 ? 1.05f : phase == 2 ? 1.25f : PartnerAlive && partner.HpPercent <= 0.5f ? 0.85f : 1f;
            return Time.time < overclockUntil ? m * 0.75f : m;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            PlayerCombat.Struck -= OnStruck;
            if (tether != null) tether.Stop();
            if (pedestal != null) Destroy(pedestal.gameObject);
            if (pedMask != null) Destroy(pedMask.gameObject);
            if (pedCol != null) Destroy(pedCol.gameObject);
            Cleanup();
        }

        private void Cleanup()
        {
            if (cleaned) return;
            cleaned = true;
            foreach (var g in goblins) if (g != null && g.Health != null && !g.Health.IsDead) g.Health.TakeDamage(99999f);
            goblins.Clear();
            DuoBullet.ClearAll();
        }

        protected override void Update()
        {
            base.Update();
            if (hp == null || !engaged) return;
            if (hp.IsDead) { Cleanup(); return; }
            if (!unbound || transforming)
            {
                hp.DamageTakenMul = 0f;                                   // on the high pedestal: nobody can reach it
                if (!unbound && !lowering)
                {
                    if (((Vector2)transform.position - home).sqrMagnitude > 0.0004f) ai.TeleportTo(home);   // pinned on top of the pedestal
                    if (tether != null && partner != null)
                    {
                        PlaceRay(tether, baseHome + Vector2.up * 0.9f, (Vector2)partner.transform.position + Vector2.up * 0.6f, 0.62f, 0.8f);
                        var c = partner.Tint; tether.Tint(new Color(c.r, c.g, c.b, 0.55f));
                    }
                }
                return;
            }
            hp.DamageTakenMul = Broken ? 2f : venting ? 1.75f : 1f;   // BUST (BreakRoutine) = x2
        }

        /// <summary>Ren hits it: phase 1 -> shield throws sparks; phase 2 while the reel spins -> the reel jolts one step.</summary>
        private void OnStruck(Vector2 c, float r)
        {
            if (this == null || hp == null || hp.IsDead || !engaged) return;
            if (!unbound)
            {
                if ((c - (baseHome + Vector2.up * 0.3f)).magnitude > r + 0.75f) return;
                SpriteVfx.Spawn("hit", baseHome + Vector2.up * 0.4f + Random.insideUnitCircle * 0.3f, 0.7f, Layer, Order + 4, false, 18f, false);
                Sfx.Play("break_stone", baseHome, 0.35f);
                if (Time.time >= nextWardHint) { nextWardHint = Time.time + 8f; Hint("The Engine sits out of reach atop its turret. Bring down the Champion who holds it up!", 2.6f); }
                return;
            }
            if ((c - (Pos + Vector2.up * 0.35f)).magnitude > r + 0.65f) return;
            if (reel != null && reel.Spinning && Time.time >= nextJolt) { nextJolt = Time.time + 0.15f; reel.Jolt(); }
        }

        /// <summary>The Champion called a weapon rack: next tick fires mortars to block Ren's path to the rack.</summary>
        internal void Interdict(Vector2 target)
        {
            if (!Alive || unbound) return;
            interdictPending = true; interdictAt = target;
        }

        private Vector2 MuzzlePos => Pos + new Vector2((ai.Body != null && ai.Body.flipX ? -1f : 1f) * 0.38f, 0.32f);
        private void Muzzle(float scale) => SpriteVfx.Spawn("duo_muzzle", MuzzlePos, scale, Layer, Order + 2, ai.Body != null && ai.Body.flipX, 15f);

        private Vector2 PlayerVel()
        {
            var prb = ai.Player != null ? ai.Player.GetComponent<Rigidbody2D>() : null;
            return prb != null ? prb.linearVelocity : Vector2.zero;
        }

        private void Shell(Vector2 at, float warn, float radius, float dmg, float burnSeconds, Color c)
        {
            var g = GroundStrike.Spawn(at, warn, radius, dmg, SpriteVfx.Load("trap_fireball_end"), 0, 14f, Order + 1, c);
            g.impactSfx = "duo_shell_land";
            g.bodyScale = 0.9f;
            var fly = SpriteVfx.Load("trap_fireball");
            if (fly.Length > 0) { g.shell = fly[0]; g.shellFrom = MuzzlePos + Vector2.up * 0.2f; g.shellArc = 1.4f; }
            StartCoroutine(AfterShell(at, warn, burnSeconds));
        }

        // ------------------------------------------------------------------ phase change: PEDESTAL DROP SCENE (comic)
        private void PlaceLowered(float drop)
        {
            if (pedestal != null) pedestal.transform.position = baseHome + Vector2.down * drop;
            ai.TeleportTo(baseHome + Vector2.up * Mathf.Max(0f, Lift - drop));
        }

        private Sprite Icon(string n) => WardenStatueMechanism.Load("VFX/props/reel_" + n);

        private void CoinRing(int n, float speed)
        {
            var coin = Icon("coin");
            float a0 = Random.Range(0f, 360f);
            for (int i = 0; i < n; i++)
            {
                var b = DuoBullet.Spawn(coin, Pos + Vector2.up * 0.4f, Dir(a0 + i * 360f / n) * speed, 7f, 0.12f, 3.5f, Layer, Order + 2, 0.4f);
                b.spinDeg = 360f;
            }
            Sfx.Play("coin_spray", Pos, 0.5f);
        }

        private static void SkullCurse(Health h)
        {
            var st = h != null ? PlayerStatus.Of(h) : null;
            if (st == null) return;
            st.Silence(10f, "The skull's curse seals your mantras and your dash!");
            st.SealRoll(10f, null);
            Sfx.Play("skull_wail", h.transform.position, 0.6f);
        }

        private static Sprite shadowSprite;
        private static Sprite Shadow()
        {
            if (shadowSprite != null) return shadowSprite;
            var tex = new Texture2D(48, 24, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int y = 0; y < 24; y++)
                for (int x = 0; x < 48; x++)
                {
                    float dx = (x - 23.5f) / 24f, dy = (y - 11.5f) / 12f, r = dx * dx + dy * dy;
                    tex.SetPixel(x, y, r < 1f ? new Color(0f, 0f, 0f, 0.55f * (1f - r * 0.5f)) : Color.clear);
                }
            tex.Apply();
            return shadowSprite = Sprite.Create(tex, new Rect(0, 0, 48, 24), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
