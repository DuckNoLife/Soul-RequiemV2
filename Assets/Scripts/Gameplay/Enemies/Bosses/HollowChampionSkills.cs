using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// THE HOLLOW CHAMPION - the "flesh and bone" half of the Reliquary Wardens duo fight (Map 4; MEGAPACK Knight_LVL4 sprite, PURPLE spell fire).
    /// "CRYSTAL RACKS": he is what raises the turret pedestal (soul tether to the Brazen Reliquary) -> he must be defeated first.
    ///   RACK RACE: after a while empty-handed he CALLS a crystal rack (the rack lights up, element aura ring, 1 s roar) and runs to it; the Engine fires mortars to block Ren.
    ///     Ren gets there first and presses T (Attune) -> Ren absorbs the charm, he arrives empty-handed -> dismayed for 2.3 s (x2 damage).
    ///     He gets there first -> DRAINS the crystal for 1.1 s (crystal shards fly into his chest); the drain always succeeds.
    ///   All 4 racks used -> they re-forge 15 s later. When he falls, any still-lit rack is open for Ren to absorb (used against the Engine).
    ///   SOUL ARMOR (Health.Barrier): 200 armor absorbs damage before HP - armor breaks -> stunned 2.6 s (x2 damage); it ONLY refills when he
    ///     drains a crystal -> winning the rack race keeps him exposed. The HUD draws the armor layer (crystal colour) over the HP bar.
    ///   Movement: races to racks fast (x1.75 / x1.9), PATHS AROUND the turret pedestal in the middle (corners of the expanded pedestal box) + unsticks (side-steps).
    ///   LOOK: after a drain the hood / soul smoke / fist gems take the crystal colour (ChampionPalette, swaps AnimatorOverrideController)
    ///     + element AURA RING under the feet (duo_aura_<c>: glow ring + rising particles) + Light2D in the crystal colour.
    ///   EMPTY-HANDED (purple): Gauntlet Rush (dash punch, 1/2 hits) / Grave Quake (ground slam: rock cracks + duo_quake dust, shock ring with a gap in phase 2).
    ///   CRIMSON - fire, CLOSE RANGE: Inferno Ring (fire ring bursts around him, leaves 6 flames on the ring edge; phase 2 adds a second ring with gaps)
    ///                          / Flame Lunge (long dash thrust, duo_flame trail burns along the path; phase 2 dashes twice).
    ///   AZURE - ice, DEFENSIVE: Glacial Ward (DEFENSE BUFF: 6 s ice shield, -55% damage taken; Ren dealing 5% of his HP breaks it -> he is stunned;
    ///                          shield still up when time runs out -> ICE BURST around him, slows on hit) / Ice Spikes (row of duo_ice_burst spikes running at Ren; phase 2: 3 rows in a fan).
    ///   VERDANT - life, HEALING: Verdant Bloom (plants a CRYSTAL SPROUT behind him: duo_heal light pillar heals 2% HP / 0.6 s for 6 s - HIT THE SPROUT 3 TIMES to uproot it,
    ///                          which stuns him; only used under 85% HP) / Thorn Snare (duo_thorns bushes grow in a ring around Ren: poison + slowing root).
    ///   GILDED - holy light, RANGED: Judgement (3/5 gold duo_smite bolts chasing where Ren stands) / Radiant Halo (4/6 duo_light_orb orbs circle him
    ///                          then launch at Ren one by one - can be slashed).
    ///   Enemies do NOT damage each other: the Engine's mortars do not hurt the Champion.
    /// Class files: HollowChampionSkills.cs (settings, setup, skill choice, helpers) · HollowChampionSkills.Attacks.cs (each skill as a coroutine).
    /// </summary>
    public sealed partial class HollowChampionSkills : BossSkillsBase
    {
        public float rushSpeed = 4.0f, rushDamage = 18f, slamDamage = 20f, slamRadius = 1.7f;
        public float armorMax = 200f;                    // Soul armor
        private BrazenReliquarySkills partner;
        private bool enraged, released, wardOn, bloomOn;
        private SpriteVfx rageAura, elemAura, wardBubble, bloomPillar;
        private GameObject bloomSeed;
        private System.Action<Vector2, float> bloomStruck;
        private Light2D elemLight;
        private float baseSpeed, nextCall, rearmAt = -1f;
        private string element = "";                    // "" = original purple fire
        private int uses;
        private ChampionPalette palette;
        private static readonly Color Soul = new Color(0.72f, 0.42f, 1f);

        private bool PartnerAlive => partner != null && partner.Alive;
        // hp is only set on the first Act tick -> if the partner asks earlier, fetch Health directly
        internal bool Alive { get { if (hp == null) hp = GetComponent<Health>(); return hp != null && !hp.IsDead; } }
        internal float HpPercent => Alive ? hp.Percent : 0f;
        /// <summary>Current spell fire colour (the Engine's soul tether follows it).</summary>
        internal Color Tint => element.Length > 0 ? CrystalPillarMechanism.ColorOf(element) : Soul;
        private Color Dim => new Color(Tint.r, Tint.g, Tint.b, 0.9f);
        private static MapMechanics MM => MapMechanics.Instance;
        private Vector2 Chest => Pos + Vector2.up * 0.55f;

        private static string Pretty(string c) =>
            c == "crimson" ? "Crimson crystal" : c == "azure" ? "Azure crystal" : c == "verdant" ? "Verdant crystal" : c == "gilded" ? "Gilded crystal" : c;

        protected override void OnEngage()
        {
            preferredRange = 1.1f;
            pinnedTimeMax = 2.4f; repositionCooldown = 3f;
            Use("rush", 1.2f); Use("slam", 3f);
            partner = FindFirstObjectByType<BrazenReliquarySkills>();
            var mm = MM;
            var arena = mm != null ? mm.MechRect("duo_arena") : null;
            if (arena.HasValue) { center = arena.Value.center; arenaRadii = arena.Value.size * 0.5f; }
            else if (partner != null) center = (Pos + (Vector2)partner.transform.position) * 0.5f;
            baseSpeed = ai.moveSpeed;
            nextCall = Time.time + 3.5f;
            palette = GetComponent<ChampionPalette>();
            hp.Died += _ => OnFell();
            hp.BarrierColor = Soul;
            hp.SetBarrier(armorMax, armorMax);
            hp.BarrierBroken += OnArmorBroken;
        }

        protected override int PhaseFor(float p)
        {
            if (engaged && partner != null && !partner.Alive) return 3;
            return p <= 0.5f ? 2 : 1;
        }
        protected override float CooldownMul() => phase == 3 ? 0.7f : phase == 2 ? 0.85f : 1f;

        /// <summary>Soul armor breaks: soul shard burst, stunned 2.6 s (x2 damage). Only draining a crystal restores the armor.</summary>
        private void OnArmorBroken(Health h)
        {
            if (!Alive) return;
            Sfx.Play("ward_break", Pos);
            var bv = SpriteVfx.Spawn("m3_break", Chest, 1.2f, Layer, Order + 3, false, 14f, false);
            if (bv != null) bv.Tint(Tint);
            Kick(Vector2.down, 0.06f, 0.25f);
            StartCoroutine(BreakRoutine(2.6f, "SOUL ARMOR SHATTERED - he is exposed! Keep him from the crystals!"));
        }

        private void OnFell()
        {
            if (hp != null) hp.SetBarrier(0f, 0f);
            SetElement("");
            EndWard();
            EndBloom();
            if (released) return;
            released = true;
            var mm = MM;
            if (mm == null) return;
            bool any = false;
            foreach (var c in mm.Crystals()) if (c.Now == CrystalPillarMechanism.State.Dormant) { c.Release(); any = true; }
            if (any) Hints.Show("The crystals blaze free - attune to what remains [T]!", 3f);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (rageAura != null) rageAura.Stop();
            if (elemAura != null) elemAura.Stop();
            EndWard();
            EndBloom();
        }

        // ------------------------------------------------------------------ update: racks re-forging
        protected override void Update()
        {
            base.Update();
            if (!engaged || !Alive) return;
            var mm = MM;
            if (mm == null) return;
            var all = mm.Crystals();
            if (all.Count == 0) return;
            foreach (var c in all) if (c.Now != CrystalPillarMechanism.State.Spent) { rearmAt = -1f; return; }
            if (rearmAt < 0f) { rearmAt = Time.time + 15f; return; }
            if (Time.time < rearmAt) return;
            rearmAt = -1f;
            foreach (var c in all) c.Rearm();
            Hint("The crystals slowly glow back to life.", 2.4f);
        }

        // ------------------------------------------------------------------ minigame: race + crystal drain
        private CrystalPillarMechanism PickCrystal()
        {
            var mm = MM;
            if (mm == null) return null;
            CrystalPillarMechanism best = null; float bd = float.MaxValue;
            foreach (var c in mm.Crystals())
            {
                if (c.Now != CrystalPillarMechanism.State.Dormant) continue;
                float d = (c.Stand - Pos).sqrMagnitude + Random.Range(0f, 1.5f);
                if (d < bd) { bd = d; best = c; }
            }
            return best;
        }

        /// <summary>Element burst visual on the spot (when a drain finishes) - visual only.</summary>
        private void ElementBurst(Vector2 at)
        {
            string n = element == "crimson" ? "duo_fire_ring" : element == "azure" ? "duo_ice_burst" : element == "verdant" ? "duo_thorns" : element == "gilded" ? "duo_smite" : null;
            if (n != null) SpriteVfx.Spawn(n, at, element == "crimson" ? 1.2f : 1f, Layer, Order + (element == "crimson" ? -1 : 1), Random.value < 0.5f, 16f, false);
        }

        private void SetElement(string e)
        {
            element = e ?? "";
            if (palette != null) palette.Set(element);
            if (elemAura != null) { elemAura.Stop(); elemAura = null; }
            if (rageAura != null) rageAura.gameObject.SetActive(element.Length == 0);   // the phase 3 purple soul fire gives way to the element aura
            if (element.Length > 0)
            {
                elemAura = SpriteVfx.Spawn("duo_aura_" + element, Pos + Vector2.down * 0.02f, 1.2f, Layer, Order - 1, false, 12f, false);
                if (elemAura != null) { elemAura.Looping(9999f); elemAura.Follow(transform); }
            }
            if (elemLight == null && element.Length > 0)
            {
                var lg = new GameObject("Element Light");
                lg.transform.SetParent(transform, false);
                lg.transform.localPosition = new Vector3(0f, 0.35f, 0f);
                elemLight = lg.AddComponent<Light2D>();
                elemLight.lightType = Light2D.LightType.Point;
                elemLight.pointLightInnerRadius = 0.1f;
                elemLight.pointLightOuterRadius = 1.7f;
                elemLight.falloffIntensity = 0.6f;
                elemLight.intensity = 0.9f;
            }
            if (elemLight != null) { elemLight.enabled = element.Length > 0; elemLight.color = Tint; }
            if (hp != null) hp.BarrierColor = Tint;                          // HUD soul armor takes the crystal colour
        }

        private void SpendElement()
        {
            if (--uses > 0) return;
            Hint("The " + Pretty(element).ToLowerInvariant() + " power fades from him.", 2f);
            SetElement("");
            nextCall = Time.time + (phase == 1 ? 6f : 4.5f);
        }

        /// <summary>Next waypoint toward `target` - if the straight line crosses the pedestal box (expanded by 0.45 units), go to the box corner
        /// that gives the shortest total path with neither leg crossing the box; no pedestal / no crossing -> target itself.</summary>
        private Vector2 Waypoint(Vector2 target)
        {
            var col = partner != null ? partner.PedestalCollider : null;
            if (col == null) return target;
            var b = col.bounds;
            Vector2 mn = (Vector2)b.min - new Vector2(0.45f, 0.45f), mx = (Vector2)b.max + new Vector2(0.45f, 0.45f);
            Vector2 innerMn = mn + new Vector2(0.08f, 0.08f), innerMx = mx - new Vector2(0.08f, 0.08f);
            if (!SegHitsBox(Pos, target, innerMn, innerMx)) return target;
            bool inside = Pos.x > innerMn.x && Pos.x < innerMx.x && Pos.y > innerMn.y && Pos.y < innerMx.y;   // right next to the pedestal base
            Vector2[] corners = { mn, new Vector2(mx.x, mn.y), mx, new Vector2(mn.x, mx.y) };
            Vector2 best = target; float bd = float.MaxValue;
            foreach (var k in corners)
            {
                if ((k - Pos).sqrMagnitude < 0.04f) continue;               // already reached this corner
                if (!inside && SegHitsBox(Pos, k, innerMn, innerMx)) continue;
                float d = (k - Pos).magnitude + (target - k).magnitude + (SegHitsBox(k, target, innerMn, innerMx) ? 1.5f : 0f);
                if (d < bd) { bd = d; best = k; }
            }
            return best;
        }

        /// <summary>Does segment ab cross the rectangle [mn, mx] (Liang-Barsky).</summary>
        private static bool SegHitsBox(Vector2 a, Vector2 b, Vector2 mn, Vector2 mx)
        {
            float t0 = 0f, t1 = 1f; Vector2 d = b - a;
            for (int i = 0; i < 2; i++)
            {
                float p0 = i == 0 ? d.x : d.y, a0 = i == 0 ? a.x : a.y, lo = i == 0 ? mn.x : mn.y, hi = i == 0 ? mx.x : mx.y;
                if (Mathf.Abs(p0) < 1e-6f) { if (a0 < lo || a0 > hi) return false; continue; }
                float u0 = (lo - a0) / p0, u1 = (hi - a0) / p0;
                if (u0 > u1) { float tmp = u0; u0 = u1; u1 = tmp; }
                t0 = Mathf.Max(t0, u0); t1 = Mathf.Min(t1, u1);
                if (t0 > t1) return false;
            }
            return true;
        }

        /// <summary>Ren absorbs the gold crystal: 2 shards fly into him.</summary>
        internal void TakeHatchets(Vector2 from, string sprite)
        {
            var art = WardenStatueMechanism.Load(sprite);
            for (int i = 0; i < 2; i++)
            {
                var b = DuoBullet.Spawn(art, from, new Vector2(i == 0 ? -2.5f : 2.5f, 2f), 0f, 0.2f, 3f, Layer, Order + 2, 1.2f);
                b.spinDeg = 900f; b.stopOnWalls = false;
                b.hitEnemy = transform;
                bool first = i == 0;
                b.onHitEnemy = _ =>
                {
                    if (!Alive) return;
                    hp.TakeDamage(hp.Max * 0.07f);
                    Sfx.Play("duo_punch", Pos);
                    SpriteVfx.Spawn("hit", Pos + Vector2.up * 0.5f, 0.9f, Layer, Order + 2, false);
                    if (first && !Broken) StartCoroutine(BreakRoutine(0.9f, "Radiant shards pierce the Champion!"));
                };
            }
        }

        private static void Chill(Health h) { var s = h != null ? PlayerStatus.Of(h) : null; if (s != null) s.Slow(0.55f, 1.6f, "Chilled!"); }
        private static void Snare(Health h)
        {
            if (h == null || !h.IsPlayer) return;
            h.ApplyPoison(0.02f, 3f);
            var s = PlayerStatus.Of(h);
            if (s != null) s.Slow(0.35f, 1.2f, "Snared by thorns!");
        }

        /// <summary>Turn off the ice shield (remove damage reduction + bubble visual).</summary>
        private void EndWard()
        {
            if (!wardOn && wardBubble == null) return;
            wardOn = false;
            if (hp != null) hp.Guard(1f, 0f);                            // guardMul = 1 -> no more damage reduction
            if (wardBubble != null) { wardBubble.Stop(); wardBubble = null; }
        }

        private void EndBloom()
        {
            if (bloomStruck != null) { PlayerCombat.Struck -= bloomStruck; bloomStruck = null; }
            if (bloomPillar != null) { bloomPillar.Stop(); bloomPillar = null; }
            if (bloomSeed != null) { Destroy(bloomSeed); bloomSeed = null; }
            bloomOn = false;
        }
    }
}
