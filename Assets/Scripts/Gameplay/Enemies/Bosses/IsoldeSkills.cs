using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// MATRIARCH ISOLDE - Map 3 boss (The Matriarch's Bower). Mother of the Vey clan, asleep for a thousand years under the old crypt. Blood is her power:
    ///   ARENA
    ///   • Vey stone coffins (isolde_coffin_0..N) = HER LIVES: while any coffin is intact she cannot die (HP stops at undyingFloor).
    ///     Ren breaks a coffin with coffinHits slashes; each broken coffin heals her (healStep x coffins broken) and raises her regen.
    ///     Rite of Repose rebuilds ONE broken coffin every restoreEvery seconds. All coffins broken + HP at the floor -> she kneels (BossMercy).
    ///   • Night Flight (shell game): she turns into bats and hides in ONE intact coffin, the coffins swap places, Ren has guessTime seconds to strike one.
    ///     Right -> her coffin breaks and she is dragged out (BREAK, x2 damage). Wrong / too slow -> she bursts out (blood wave) and heals wrongHeal.
    ///   • Blood pools: every Crimson Rain drop leaves a pool. She glides to a pool to drink (Feast, heals) - 4 hits pull her
    ///     away from the feast (BREAK). Ren's F skill burns pools dry. Phase 3: every remaining pool SPRAYS BLOOD SPIKES.
    ///   SKILLS
    ///   • Blood Lash (melee): 160° claw swipe, two swipes from phase 2.
    ///   • Sanguine Kiss (when pressed close): light swipe that drains HP on hit.
    ///   • Bat Dive: red warning lane -> turns into a bat swarm that flies through Ren, reappears next to him and claws.
    ///   • Children of the Night: releases 3/5/7 bats that arc around and chase Ren (a slash pops them).
    ///   • Sanguine Siphon: 2.8 s blood tether between Ren and her that drains Ren to heal her. Break it: run farther than 3.8 or HIDE BEHIND A COFFIN.
    ///   • Crimson Rain: blood drops fall from the sky onto red rings around Ren and leave blood pools.
    ///   • Mist Step: dissolves into blood mist and reappears far from Ren (corner escape / when pressed), then releases 3 bats.
    ///   • Sanguine Tide (phase 3): 3 rings of blood waves with 3 gaps, slowly rotating; pools spray spikes at the same time.
    /// Every effect is a PixelLab image (m3_blood_mist, m3_blood_pool, m3_blood_orb, m3_wave_blood, m3_ray_warn, m3_break...).
    /// Class files: IsoldeSkills.cs (settings, setup, skill choice, helpers) · IsoldeSkills.Attacks.cs (each skill as a coroutine).
    /// </summary>
    public partial class IsoldeSkills : BossSkillsBase
    {
        [Header("Coffins")]
        public int coffinCount = 4;                 // a 4th coffin instead of the crypt
        public float flightTime = 7f, flightHeal = 0.06f;
        [Header("Blood pools")]
        public int maxPools = 4;
        public float poolLife = 24f, poolRadius = 0.34f, feastPerSecond = 0.03f, feastTime = 2.6f;
        public int feastBreakHits = 4;
        [Header("Bats (set by the builder)")]
        public Sprite[] batFrames;
        [Header("Siphon")]
        public float siphonRange = 3.8f, siphonTime = 2.8f;
        [Header("Lifesteal (toned down)")]
        [Tooltip("Multiplies every LIFESTEAL: Sanguine Kiss, Sanguine Siphon, Feast, phase 3 pool drinking")] public float lifestealMul = 0.5f;
        [Tooltip("Multiplies the HP she drinks when a coffin breaks")] public float coffinDrinkMul = 0.65f;

        private class Bat { public Transform t; public bool dead; }
        private readonly List<Bat> bats = new List<Bat>();

        private class Coffin
        {
            public string name; public Vector2 pos; public bool used;
            public int hits; public bool broken;                              // coffins must be destroyed
            public readonly List<GameObject> gos = new List<GameObject>();     // base + _open + _broken (moved together when swapping)
        }
        private bool guessOpen;                    // waiting for Ren to pick a coffin
        [Header("Coffin shell game")]
        public float wrongHeal = 0.08f, guessTime = 5f;
        [Header("Coffins = her lives")]
        [Tooltip("Slashes needed to break one coffin")] public int coffinHits = 3;
        [Tooltip("Rite of Repose rebuilds ONE coffin every this many seconds")] public float restoreEvery = 30f;
        [Tooltip("Heal when a coffin breaks = healStep x coffins broken (arithmetic: 3%, 6%, 9%, 12%)")] public float healStep = 0.03f;
        [Tooltip("Regen per second = regenStep x coffins broken")] public float regenStep = 0.002f;
        [Tooltip("While coffins remain, HP never drops below this")] public float undyingFloor = 0.05f;
        private string baseName;
        private float nextFloorHint;
        private BossMercy mercy;
        private int Intact => coffins.FindAll(c => !c.broken).Count;
        private int Destroyed => coffins.Count - Intact;
        private class Pool { public Vector2 pos; public SpriteVfx vfx; public float until; }
        private readonly List<Coffin> coffins = new List<Coffin>();
        private readonly List<Pool> pools = new List<Pool>();
        private Coffin hideIn;
        private Coffin struckCoffin;
        private int hitsTaken;
        private float nextBurnHint;
        private static readonly Color Blood = new Color(0.92f, 0.12f, 0.2f, 1f);
        private static readonly Color DarkBlood = new Color(0.55f, 0.05f, 0.1f, 1f);

        protected override int PhaseFor(float p) => p > 0.6f ? 1 : p > 0.25f ? 2 : 3;
        protected override float CooldownMul() => phase == 3 ? 0.75f : 1f;

        private void Awake()
        {
            var h = GetComponent<Health>();
            if (h != null) h.Damaged += (_, __) => hitsTaken++;
        }

        // ------------------------------------------------------------------ restore coffins when the fight restarts
        // If Ren falls mid-fight after breaking coffins, the zone resets and the new Isolde treats every coffin as intact, but the SPRITES would still
        // show broken / open / swapped coffins and the player could not tell which is real. So we remember every coffin sprite's original position (first seen)
        // and, when the coffin zone resets (MapEncounterDirector.ZoneReset) and when the fight starts, move them back, show the intact sprite, hide _open / _broken.
        private const int MaxCoffins = 8;
        private static readonly Dictionary<GameObject, Vector3> coffinHome = new Dictionary<GameObject, Vector3>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void HookReset()
        {
            coffinHome.Clear();
            MapEncounterDirector.ZoneReset -= OnZoneReset;
            MapEncounterDirector.ZoneReset += OnZoneReset;
        }

        private static void OnZoneReset(Rect zone)
        {
            var mm = MapMechanics.Instance;
            if (mm == null) return;
            bool mine = false;
            for (int i = 0; i < MaxCoffins && !mine; i++)
                foreach (var g in mm.Named("isolde_coffin_" + i))
                    if (g != null && zone.Contains((Vector2)(coffinHome.TryGetValue(g, out var h) ? h : g.transform.position))) { mine = true; break; }
            if (mine) RestoreCoffins(mm);
        }

        /// <summary>Every coffin back at its original spot, intact.</summary>
        private static void RestoreCoffins(MapMechanics mm)
        {
            for (int i = 0; i < MaxCoffins; i++)
            {
                string n = "isolde_coffin_" + i;
                foreach (var suf in new[] { "", "_open", "_broken" })
                    foreach (var g in mm.Named(n + suf))
                    {
                        if (g == null) continue;
                        if (!coffinHome.ContainsKey(g)) coffinHome[g] = g.transform.position;   // first time: not swapped yet
                        else g.transform.position = coffinHome[g];
                    }
                mm.SetVisible(n + "_open", false);
                mm.SetVisible(n + "_broken", false);
                mm.SetVisible(n, true);
            }
        }

        // ------------------------------------------------------------------ start
        protected override void OnEngage()
        {
            Hint("\"Who knocks upon the Matriarch's door?\"", 3.5f);
            var mm = MapMechanics.Instance;
            if (mm != null) RestoreCoffins(mm);                                  // new fight = intact coffins in their original spots
            for (int i = 0; i < coffinCount && mm != null; i++)
            {
                string n = "isolde_coffin_" + i;
                var gos = mm.Named(n);
                if (gos.Count == 0) continue;
                var r = gos[0].GetComponentInChildren<SpriteRenderer>();
                Vector2 c = r != null ? (Vector2)r.bounds.center : (Vector2)gos[0].transform.position;
                var cf = new Coffin { name = n, pos = c };
                foreach (var suf in new[] { "", "_open", "_broken" }) cf.gos.AddRange(mm.Named(n + suf));
                coffins.Add(cf);
            }
            PlayerCombat.Struck += OnStruck;
            baseName = ai.displayName;
            ready["rite"] = Time.time + restoreEvery;
            UpdateCoffinState();
            PlayerCombat.SkillCast += OnSkillCast;
            Use("bats", 2f); Use("siphon", 10f); Use("rain", 4f); Use("dive", 6f); Use("feast", 12f); Use("flight", 999f); Use("tide", 999f);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            PlayerCombat.Struck -= OnStruck;
            PlayerCombat.SkillCast -= OnSkillCast;
            foreach (var p in pools) if (p.vfx != null) p.vfx.Stop();
            pools.Clear();
            foreach (var b in bats) if (b.t != null) Destroy(b.t.gameObject);
            bats.Clear();
        }

        /// A coffin (open / broken ones included) between a and b cuts the siphon tether.
        private bool CoffinBlocks(Vector2 a, Vector2 b)
        {
            foreach (var c in coffins) if (SegDist(c.pos, a, b) < 0.42f) return true;
            return false;
        }

        private void DropBlood(Vector2 p, float warn, bool leavePool)
        {
            var glob = SpriteVfx.Load("m3x_blood_bolt");                  // falling blood drop = glowing blood dart with a tail (rotated along the fall)
            // the blood rain covers a wide area -> no HP loss, it causes HEMORRHAGE instead: no flask drinking for 6 s
            var g = GroundStrike.Spawn(p, warn, poolRadius, 0f, SpriteVfx.Load("m3x_blood_burst"), 1, 16f, Order, Blood);
            g.impactSfx = "blood_splat"; // sound
            g.onHit = _ => { var s = PS; if (s != null) s.BlockHeal(6f); };
            g.bodyScale = poolRadius / 0.4f;                              // m3x_blood_burst splash ring ~0.4 units at scale 1 = matches the hit radius
            g.impactLight = 1.1f;
            if (glob.Length > 0)
            {
                g.shell = glob[0]; g.shellFrom = p + new Vector2(0.35f, 2.6f); g.shellArc = 0f; g.shellScale = 0.55f;
                g.shellFaceMotion = true; g.shellLight = 0.8f;
            }
            if (leavePool) StartCoroutine(PoolAfter(p, warn));
        }

        /// Standing in a blood pool: sticky feet, 25% slower (a reason to burn pools with the F skill).
        protected override void Update()
        {
            base.Update();
            if (ai == null || hp == null || hp.IsDead || !engaged) return;
            // all coffins broken + HP at the floor -> Isolde kneels; slash again = finish her, [T] = spare (BossMercy)
            if (mercy == null && Intact == 0 && coffins.Count > 0 && hp.Current <= hp.Floor + 0.5f)
            {
                StopAllCoroutines();
                mercy = BossMercy.Begin(ai, "mercy_3", "isolde_slain");
                enabled = false;
                return;
            }
            if (Destroyed > 0 && !Broken) hp.Heal(hp.Max * regenStep * Destroyed * Time.deltaTime);   // constant regen, grows with the number of broken coffins
            if (Intact > 0 && hp.Current <= hp.Floor + 0.5f && Time.time >= nextFloorHint)
            {
                Hint("She cannot die while her coffins stand. Break them!", 2.5f);
                nextFloorHint = Time.time + 8f;
            }
            if (ai.Player == null || pools.Count == 0) return;
            foreach (var p in pools)
                if ((PlayerPos - p.pos).magnitude <= poolRadius + 0.05f) { var s = PS; if (s != null) s.Slow(0.75f, 0.3f); break; }
        }

        private void TickPools()
        {
            for (int i = pools.Count - 1; i >= 0; i--)
                if (Time.time >= pools[i].until || pools[i].vfx == null) { if (pools[i].vfx != null) pools[i].vfx.Stop(); pools.RemoveAt(i); }
        }

        /// Ren's F skill (SkillCast) burns blood pools dry inside its blast area.
        private void OnSkillCast(Vector2 c, float r)
        {
            int burned = 0;
            for (int i = pools.Count - 1; i >= 0; i--)
            {
                if ((pools[i].pos - c).magnitude > r + 0.35f) continue;
                SpriteVfx.Spawn("m3_blood_mist", pools[i].pos, 0.4f, Layer, Order, false, 18f);
                if (pools[i].vfx != null) pools[i].vfx.Stop();
                pools.RemoveAt(i);
                burned++;
            }
            if (burned > 0 && Time.time >= nextBurnHint) { Hint("The spilled blood boils away.", 1.6f); nextBurnHint = Time.time + 6f; }
        }

        /// Break a coffin: show _broken, blood splash; she heals by an arithmetic step (healStep x coffins broken).
        private void BreakCoffin(Coffin c, bool heal)
        {
            if (c.broken) return;
            c.broken = true;
            var mm = MapMechanics.Instance;
            if (mm != null) { mm.SetVisible(c.name, false); mm.SetVisible(c.name + "_open", false); mm.SetVisible(c.name + "_broken", true); }
            SpriteVfx.Spawn("m3_blood_mist", c.pos, 0.7f, Layer, Order + 2, false, 16f);
            BossJuice.Burst("m3x_blood_burst", c.pos, 1.3f, Color.white, Layer, Order + 2, 16f);
            BossJuice.Flash(c.pos, Blood, 1.8f, 1.5f, 0.4f);
            BossJuice.Shake(2);
            BossJuice.Stop(0.05f);
            UpdateCoffinState();
            int left = Intact;
            if (heal && !hp.IsDead)
            {
                float amt = healStep * Destroyed * coffinDrinkMul;              // 3/6/9/12% -> ~2/4/6/8%
                hp.Heal(hp.Max * amt);
                for (int k = 0; k < 5; k++) StartCoroutine(FlyMote("m3x_blood_bolt", c.pos + Random.insideUnitCircle * 0.2f, transform, Vector2.up * 0.45f, 0.5f + k * 0.06f, 0.34f));
                Hint(left > 0 ? $"A coffin shatters. She drinks its blood (+{amt * 100f:0}%). {left} remain." : "The last coffin falls. Now she can be brought to her knees!", 2.5f);
            }
            else if (left == 0) Hint("The last coffin falls. Now she can be brought to her knees!", 2.5f);
            if (Ready("rite")) ready["rite"] = Time.time + restoreEvery;       // start the 30 s count from the first broken coffin
        }

        /// Her lives: while coffins remain she cannot die (HP stops at undyingFloor). The HP bar name shows the coffin count.
        private void UpdateCoffinState()
        {
            int left = Intact;
            hp.Floor = (left > 0 || mercy == null) ? hp.Max * undyingFloor : 0f;   // no coffins left but still at the floor -> kneel, wait for Ren's choice
            if (!string.IsNullOrEmpty(baseName)) ai.displayName = left > 0 ? $"{baseName}  —  Coffins {left}/{coffins.Count}" : baseName;
        }

        // ------------------------------------------------------------------ Ren's slash hitting a coffin
        private void OnStruck(Vector2 c, float r)
        {
            foreach (var b in bats) if (!b.dead && b.t != null && ((Vector2)b.t.position - c).magnitude <= r + 0.2f) b.dead = true;   // slash bats
            if (guessOpen)
            {
                if (hideIn == null || struckCoffin != null) return;
                foreach (var cf in coffins)
                    if (!cf.broken && (cf.pos - c).magnitude <= r + 0.4f) { struckCoffin = cf; SpriteVfx.Spawn("m3_hurt", cf.pos, 0.6f, Layer, Order + 2, false, 18f); break; }
                return;
            }
            if (hideIn != null) return;                                        // swapping: not counted yet
            foreach (var cf in coffins)                                        // normal: slash to break the coffin
            {
                if (cf.broken || (cf.pos - c).magnitude > r + 0.4f) continue;
                cf.hits++;
                SpriteVfx.Spawn("m3_hurt", cf.pos, 0.6f, Layer, Order + 2, false, 18f);
                if (cf.hits >= coffinHits) BreakCoffin(cf, true);
                break;
            }
        }
    }
}
