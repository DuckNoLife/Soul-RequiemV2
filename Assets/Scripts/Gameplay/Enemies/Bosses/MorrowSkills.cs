using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// PRECENTOR MORROW - Map 3 boss (area V11). Choirmaster of the church, bell staff. The fight is a hymn; the arena is the instrument.
    /// EVERY effect is a PixelLab image (Resources/VFX): m3_lantern_ray beam, m3_ray_warn warning line, m3_lantern_charge glow,
    /// m3_wave_hymn wave pieces, m3_choir_lance light pillar, m3_vespers mist, m3_blood_orb blood drop, m3_break Break.
    ///   ARENA - 6 altar pillars (morrow_pillar_0..5), each with a holy lantern:
    ///   • Lantern Litany: lanterns on 2/3 pillars charge (0.85 s warning line), then FIRE holy beams across the arena toward Ren.
    ///     Broken pillars cannot fire -> breaking pillars both causes a Break and weakens this skill.
    ///   • Blood Pact: periodically one pillar turns BLOOD RED and blood streams toward Morrow. Break its lantern before the drain ends
    ///     -> the pillar breaks for good + Morrow BREAKS (kneels, x2 damage). Drain completes -> heals 10% + faster skills.
    ///   • Requiem (phase 3): Morrow returns to the altar and EVERY remaining lantern fires 3 crossing beam volleys.
    ///   • Graves around the edge: Raise the Named calls up Risen Dead.
    ///   OTHER SKILLS (he is a caster with no basic melee)
    ///   • Bell Toll (when pressed close): short bell wave, light damage + 0.5 s daze + knockback, then Vespers Step away.
    ///   • Toll of Hymns: 1/2/2 ring waves with 4 gaps, gaps rotate each ring; no damage, seals Ren's F skill.
    ///   • Choir Lances: 4/5/6 light pillars falling one after another where Ren is standing. • Dirge Notes: fan of 5 notes (phase 2+, or 2+ pillars broken).
    ///   • Vespers Step: dissolves into mist, reappears far from Ren (corner escape) or behind Ren and tolls the bell (phase 3).
    ///   DIFFICULTY:
    ///   • Shackling Canticle: 3/4/5 golden hymn notes circle Morrow, then chase Ren one by one. A hit = the note bursts into a CHORD ZONE lasting 0.6 s;
    ///     standing in it when invulnerability ends = ROLL (Q) LOCKED for 5 s (golden staff under the feet, HUD roll slot dims + countdown). Slash notes / outrun them.
    ///   • Antiphon: while Ren's Q is locked Morrow skips his rest and answers at once with Choir Lances (dense, fast) or Lantern Litany.
    ///   • Choir Lances LEAD Ren's running direction; phase 3 adds diagonal pillars on both sides.
    ///   • Each broken pillar: skills 7% faster + extra Dirge Notes volleys (breaking every pillar does not leave Morrow without attacks).
    ///   • Blood lanterns get tougher: phase 1/2/3 needs 1/2/3 hits.
    /// Class files: MorrowSkills.cs (settings, setup, skill choice, helpers) · MorrowSkills.Attacks.cs (each skill as a coroutine).
    /// </summary>
    public partial class MorrowSkills : BossSkillsBase
    {
        [Header("Altar pillars")]
        public int pillarCount = 6;
        public float pillarEvery = 18f, drainTime = 6.5f, drainHeal = 0.08f;
        public int pillarHits = 1;                  // 1 hit breaks a lantern
        [Header("Risen Dead")]
        public int maxRisen = 3;                    // at most 3 Risen alive at once
        public Vector2 graveRadii = new Vector2(3.52f, 2.37f);
        public float[] graveAngles = { -25f, 30f, 150f, 205f };

        private class Pillar
        {
            public int index; public string name; public Vector2 pos, lamp; public bool broken, blood; public int hits;
            public Light2D light; public float drainEnd;
        }
        private readonly List<Pillar> pillars = new List<Pillar>();
        private readonly List<EnemyAI> risen = new List<EnemyAI>();
        private int pactStacks;
        private float nextPillar;
        // Q-lock notes + Ren velocity tracking (leading Lances)
        [Header("Shackling Canticle")]
        public float shackleSeconds = 5f, noteSpeed = 1.45f, noteTurn = 110f, noteLife = 5.5f;
        private sealed class Note { public Transform t; public SpriteVfx fx; public bool dead; }
        private readonly List<Note> notes = new List<Note>();
        private Vector2 renVel, renLast;
        private bool renSeen;
        private SpriteVfx shackleFx;
        private static readonly Color Gold = new Color(1f, 0.85f, 0.45f, 1f);
        private int BrokenPillars => pillars.FindAll(p => p.broken).Count;
        private bool RenShackled { get { var s = PS; return s != null && s.RollSealed; } }
        private static readonly Color Ice = new Color(0.65f, 0.82f, 1f, 1f);
        private static readonly Color Blood = new Color(0.9f, 0.12f, 0.15f, 1f);

        protected override int PhaseFor(float p) => p > 0.65f ? 1 : p > 0.30f ? 2 : 3;
        protected override float CooldownMul() => Mathf.Pow(0.9f, Mathf.Min(pactStacks, 3)) * (phase == 3 ? 0.85f : 1f)
                                                   * Mathf.Max(0.65f, 1f - 0.07f * BrokenPillars);                     // pillars lost -> sings faster

        /// Ren's velocity (smoothed) for leading Choir Lances + the golden staff that follows his feet while roll is locked.
        protected override void Update()
        {
            base.Update();
            if (ai == null || ai.Player == null) return;
            Vector2 p = PlayerPos;
            if (renSeen && Time.deltaTime > 0f) renVel = Vector2.Lerp(renVel, (p - renLast) / Time.deltaTime, Mathf.Clamp01(Time.deltaTime * 8f));
            renLast = p; renSeen = true;
            bool sh = RenShackled && hp != null && !hp.IsDead;
            if (sh && shackleFx == null)
            {
                shackleFx = SpriteVfx.Spawn("m3x_shackle", p, 0.75f, Layer, Order - 1, false, 10f, false);
                if (shackleFx != null) shackleFx.Looping(99f);
            }
            if (shackleFx != null)
            {
                if (!sh) { shackleFx.Stop(); shackleFx = null; }
                else shackleFx.transform.position = p + Vector2.up * 0.02f;
            }
        }

        // ------------------------------------------------------------------ start
        protected override void OnEngage()
        {
            Hint("\"You did us a kindness in the cistern. Let me return it.\"", 3.5f);
            var mm = MapMechanics.Instance;
            for (int i = 0; i < pillarCount && mm != null; i++)
            {
                string n = "morrow_pillar_" + i;
                var gos = mm.Named(n);
                if (gos.Count == 0) continue;
                var r = gos[0].GetComponentInChildren<SpriteRenderer>();
                Vector2 foot = r != null ? new Vector2(r.bounds.center.x, r.bounds.min.y + 0.12f) : (Vector2)gos[0].transform.position;
                Vector2 lamp = r != null ? new Vector2(r.bounds.center.x, r.bounds.min.y + r.bounds.size.y * 0.62f) : foot + Vector2.up * 0.5f;
                pillars.Add(new Pillar { index = i, name = n, pos = foot, lamp = lamp, light = mm.LightNamed(n + "_light") });
            }
            PlayerCombat.Struck += OnStruck;
            pinnedTimeMax = 0.8f; repositionCooldown = 2.5f;                        // caster: teleports away when pressed for too long
            nextPillar = Time.time + 6f;
            Use("toll", 4f); Use("lances", 6f); Use("dirge", 2f); Use("raise", 999f); Use("vespers", 8f); Use("litany", 7f); Use("requiem", 4f);
            Use("canticle", 9f); Use("dirge", 8f);
            StartCoroutine(PillarCycle());
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            PlayerCombat.Struck -= OnStruck;
            foreach (var n in notes) if (n.t != null) Destroy(n.t.gameObject);
            notes.Clear();
            if (shackleFx != null) { shackleFx.Stop(); shackleFx = null; }
        }

        // ------------------------------------------------------------------ Lantern Litany: holy beams fired from the altar pillar lanterns
        private const float RayLen = 7.5f, RayHalfWidth = 0.2f, ChargeTime = 0.85f;
        private float rayDamageMul = 0.8f;

        private static Vector2 Orbit(int i, int n, float t, float r)
        {
            float a = (i * 360f / n + t * 220f) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r * 0.6f + 0.1f);
        }

        private void KillNotes(List<Note> list)
        {
            foreach (var n in list) { n.dead = true; if (n.t != null) Destroy(n.t.gameObject); notes.Remove(n); }
        }

        /// Player slash (Struck event): hits the lantern of a red pillar.
        private void OnStruck(Vector2 c, float r)
        {
            // slash a hymn note apart (prevents the Q lock)
            foreach (var n in notes)
            {
                if (n.dead || n.t == null || ((Vector2)n.t.position - c).magnitude > r + 0.28f) continue;
                n.dead = true;
                BossJuice.Burst("bfx_flash", n.t.position, 0.5f, Gold, Layer, Order + 3, 22f);
                BossJuice.Flash(n.t.position, Gold, 0.9f, 1f, 0.2f);
            }
            foreach (var p in pillars)
            {
                if (!p.blood || p.broken) continue;
                if ((p.lamp - c).magnitude <= r + 0.35f || (p.pos - c).magnitude <= r + 0.3f)
                {
                    p.hits++;
                    SpriteVfx.Spawn("m3_hurt", p.lamp, 0.6f, Layer, Order + 2, false, 18f);
                    int need = Mathf.Max(pillarHits, phase);                    // phase 1/2/3 needs 1/2/3 hits
                    if (p.hits >= need) p.broken = true;
                    else Hint("The lantern cracks... " + (need - p.hits) + " more!", 1f);
                    break;
                }
            }
        }
    }
}
