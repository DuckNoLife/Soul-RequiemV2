using System.Collections;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// ANCIENT ENT - miniboss of the Penitent Grove. GROWS THROUGH 4 FORMS (MEGAPACK Ent_LVL1..4) - the tree drinks grave sap (Ichor) and grows.
    /// FOUR LIVES instead of HP % thresholds - each form has its own HP bar (formHpMul × asset maxHp). Draining a bar
    /// (stops at 1 HP, no death) -> it roots to drink sap, the bar refills and it TRANSFORMS into the next form; only form 4 really dies. Bar name shows "(n/4)".
    /// Invulnerable ~1.9 s while transforming, with a knockback wave. Each form adds exactly ONE new mechanic
    /// and keeps one old one -> the player learns step by step and difficulty rises evenly (Map 3 miniboss: easier than the Map 3 bosses, harder than elites).
    ///   FORM 1 - GRAVE SAPLING (100-75%): small sapling, runs fast.
    ///     Root Scuttle : 0.55 s warning lane, then a dash (0.6x damage). Seed Spit: 3 sap seeds arcing down onto warning rings.
    ///   FORM 2 - PENITENT STUMP (75-50%): tough, slow stump.
    ///     Stump Slam   : dash in, then STOMP - shock ring r 1.3 (0.7x, knockback). Root Line: one row of thorn roots. Seed Spit with 4 seeds.
    ///   FORM 3 - ELDER ENT (50-25%): the classic Ancient Ent skill set.
    ///     Root Fan (3 rows), Ichor Volley 5, Bramble Ring (root ring with 2 gaps), Grasp (point blank), Communion (heal, once, 4 hits to interrupt),
    ///     summons 2 Penitent Ent saplings on entering the form.
    ///   FORM 4 - HEART OF THE GROVE (25-0%): giant tree head, slow but hits hard, fastest skill rhythm, no healing.
    ///     Grove Hammer : 1 s arm swing, then 3 slams stepping toward Ren (r 0.5, 1x). Double Bramble Ring (2 rings, offset gaps). Root Fan + Ichor Volley 6. Grasp.
    /// Animation: one AnimatorController per form (Ent_3_F1..F4, same state names Idle/Move/Prepare/Dash/Recover; F1 also has Grow), built by Map3EnemyBuilder.
    /// </summary>
    public class AncientEntSkills : MonoBehaviour, IEnemySkills
    {
        public float lineWarn = 0.6f, lineStep = 0.42f, lineDelay = 0.07f, spikeRadius = 0.26f;
        public int lineCount = 7;
        public float volleyWarn = 0.85f, ringWarn = 0.9f, ringRadius = 2.6f, ringTime = 1.5f;
        public float graspWarn = 0.6f, graspStun = 0.8f;
        public float healPerSecond = 0.05f, healTime = 3f;
        public int healBreakHits = 4;
        public float vfxScale = 0.7f;
        public float skillEvery = 2.6f;

        [Header("4 forms (assigned by the builder)")]
        public RuntimeAnimatorController[] forms;
        public float[] formScales = { 1.8f, 1.9f, 1.7f, 1.75f };
        public Vector2[] formColliders = { new Vector2(0.24f, 0.1f), new Vector2(0.34f, 0.12f), new Vector2(0.44f, 0.16f), new Vector2(0.52f, 0.18f) };
        public float[] formSpeeds = { 1.15f, 0.7f, 0.6f, 0.5f };
        public float[] formSkillEvery = { 2.2f, 2.4f, 2.3f, 1.9f };
        [Tooltip("HP per life = multiplier × asset maxHp (420 -> 97 / 122 / 151 / 185)")]
        public float[] formHpMul = { 0.23f, 0.29f, 0.36f, 0.44f };
        private static readonly string[] FormTitle = { "Grave Sapling", "Penitent Stump", "Elder Ent", "Heart of the Grove" };

        private EnemyAI ai;
        private Animator anim;
        private CapsuleCollider2D col;
        private string baseName;
        private bool engaged, summoned, growing;
        private int form = -1;
        private float nextSkill, nextGrasp;
        private int heals, hitsTaken, rot;
        private static readonly Color Root = new Color(0.85f, 0.3f, 0.25f, 1f);
        private static readonly Color Ichor = new Color(0.45f, 1f, 0.45f, 1f);
        private static readonly Color Sap = new Color(0.6f, 1f, 0.5f, 1f);           // glowing living sap

        private int Layer => ai.SortingLayer;
        private int Order => ai.SortingOrder;
        private Vector2 Pos => transform.position;
        private float Scale => formScales != null && form >= 0 && form < formScales.Length ? formScales[form] : 1.6f;
        /// vfx scaled by the current form size (form 3 = 1)
        private float FxK => Scale / 1.7f * (form == 3 ? 1.2f : form <= 1 ? 0.75f : 1f);

        private void Awake()
        {
            var h = GetComponent<Health>();
            if (h != null) h.Damaged += (_, __) => hitsTaken++;
            anim = GetComponent<Animator>();
            col = GetComponent<CapsuleCollider2D>();
        }

        private void Start()
        {
            var e = GetComponent<EnemyAI>();
            if (e != null && string.IsNullOrEmpty(baseName)) baseName = e.displayName;
            var h = GetComponent<Health>();
            if (h != null) baseHp = h.Max;                                  // asset maxHp (already applied by EnemyAI.Awake via Configure)
            ApplyForm(0, e);                                                // appears as a sapling
            if (HasForms && h != null) { h.Configure(FormHp(0), false, h.ImmuneToCrowdControl); h.Floor = 1f; }   // life 1/4
            if (HasForms && e != null) e.phase2At = 0f;                    // no shared "half HP" threshold - each life is its own threshold
        }

        private float baseHp;
        /// max HP of life `f`.
        private float FormHp(int f) => Mathf.Max(20f, Mathf.Round((baseHp > 0f ? baseHp : 420f) *
                                         (formHpMul != null && f >= 0 && f < formHpMul.Length ? formHpMul[f] : 0.25f)));

        private bool HasForms => forms != null && forms.Length >= 4 && forms[0] != null;

        /// Switch form: controller + size + collider + speed + name on the HP bar.
        private void ApplyForm(int f, EnemyAI e)
        {
            if (!HasForms) { form = 2; return; }                             // old prefab (not rebuilt yet): keep the form 3 skill set
            form = Mathf.Clamp(f, 0, 3);
            if (anim != null && forms[form] != null) anim.runtimeAnimatorController = forms[form];
            transform.localScale = Vector3.one * Scale;
            if (col != null && formColliders != null && form < formColliders.Length)
            {
                col.size = formColliders[form] / Mathf.Max(0.01f, Scale);
                col.offset = new Vector2(0f, col.size.y * 0.35f);
            }
            if (e != null)
            {
                if (formSpeeds != null && form < formSpeeds.Length) e.moveSpeed = formSpeeds[form];
                if (!string.IsNullOrEmpty(baseName)) e.displayName = baseName + "  —  " + FormTitle[form] + "  (" + (form + 1) + "/4)";
                e.PlayState("Idle");
            }
        }

        /// current life used up (HP hit the 1 HP floor) -> transform. Form 4 has no floor: real death.
        private bool LifeSpent => HasForms && form >= 0 && form < 3 && ai.Health.Current <= ai.Health.Floor + 0.01f;

        public IEnumerator Act(EnemyAI owner)
        {
            ai = owner;
            if (ai.Player == null) yield break;
            if (!engaged) { engaged = true; nextSkill = Time.time + 1.2f; if (form < 0) ApplyForm(0, ai); }
            if (growing) yield break;

            if (LifeSpent) { yield return Grow(form + 1); yield break; }     // one life gone -> next form (one form at a time)

            if (form == 2 && !summoned && (HasForms || ai.InPhase2)) { yield return Summon(); yield break; }   // old prefab: summon saplings at 50%
            if (form == 2 && heals < 1 && ai.Health.Percent < (HasForms ? 0.45f : 0.42f) && !LifeSpent) { yield return Communion(); yield break; }

            Vector2 to = (Vector2)ai.Player.position - Pos;
            float d = to.magnitude;
            ai.FaceX(to.x);
            if (form >= 2 && ai.GapToPlayer() < 0.45f && Time.time >= nextGrasp) { yield return Grasp(); yield break; }
            if (Time.time >= nextSkill && d < 5f)
            {
                yield return PickSkill(d);
                nextSkill = Time.time + (formSkillEvery != null && form >= 0 && form < formSkillEvery.Length ? formSkillEvery[form] : skillEvery);
                yield break;
            }
            // keep distance: the sapling circles at dash range, later forms stand at spit range
            float far = form == 0 ? 1.8f : form == 3 ? 1.6f : 2.2f, near = form == 0 ? 0.9f : form == 3 ? 0.7f : 1.2f;
            ai.Desired = d > far ? to.normalized * ai.moveSpeed : d < near ? -to.normalized * ai.moveSpeed * 0.6f : Vector2.zero;
            ai.PlayState(ai.Desired.sqrMagnitude > 0.001f ? "Move" : "Idle", false);
        }

        private IEnumerator PickSkill(float d)
        {
            int r = rot++;
            switch (form)
            {
                case 0:
                    if (r % 2 == 0 && d < 2.6f) yield return Scuttle(); else yield return SeedSpit(3);
                    break;
                case 1:
                    if (r % 3 == 0) yield return StumpSlam();
                    else if (r % 3 == 1) yield return RootLine(false);
                    else yield return SeedSpit(4);
                    break;
                case 3:
                    if (r % 4 == 0) yield return GroveHammer();
                    else if (r % 4 == 1) yield return RootLine(true);
                    else if (r % 4 == 2) yield return BrambleRing(2);
                    else yield return Volley(6);
                    break;
                default:
                    if (r % 3 == 0) yield return RootLine(true);
                    else if (r % 3 == 1) yield return Volley(5);
                    else yield return BrambleRing(1);
                    break;
            }
        }

        // ----------------------------------------------------------------- TRANSFORM
        private static readonly string[] GrowName = { "", "Root Takes Hold", "The Stump Splits", "Heart of the Grove" };
        private static readonly string[] GrowLine =
        {
            "",
            "The sapling drinks from the graves - and swells.",
            "Bark splits. Something older wakes inside the stump.",
            "The grove has one heart, and it is awake.",
        };

        private IEnumerator Grow(int to)
        {
            growing = true;
            var h = ai.Health;
            h.Invulnerable = true;
            // new life - the HP bar resizes, drops to 1, then REFILLS gradually while the tree drinks sap
            float newMax = FormHp(to);
            h.Configure(newMax, false, h.ImmuneToCrowdControl);
            h.Floor = 1f;
            h.SetCurrent(1f);
            ai.Desired = Vector2.zero;
            BossJuice.Cast(h, GrowName[to], Sap, 1.5f);
            Hints.Show(GrowLine[to], 2.4f);
            // 1. root and drink sap: form 1 plays "Grow" (turns green), later forms hold Prepare; sap gathers + light grows, roots rise around the feet
            ai.PlayState(ai.HasAnimState("Grow") ? "Grow" : "Prepare");
            var fx = SpriteVfx.Spawn("abs_charge2", Pos + Vector2.up * 0.3f * FxK, 1.1f, Layer, Order + 2, false, 12f / 1.4f, false);
            if (fx != null) { fx.Tint(Sap); fx.Follow(transform); }
            var glow = BossJuice.Light(Pos + Vector2.up * 0.3f, Sap, 1.4f, 0.3f, 0f, transform);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f * Mathf.Deg2Rad;
                BossJuice.Burst("m3x_root_erupt", Pos + new Vector2(Mathf.Cos(a) * 0.9f, Mathf.Sin(a) * 0.55f), 0.6f, Color.white, Layer, Order - 1, 18f);
            }
            float t = 0f;
            while (t < 1.4f && !h.IsDead)
            {
                if (glow != null) { glow.intensity = Mathf.Lerp(0.3f, 2f, t / 1.4f); glow.pointLightOuterRadius = Mathf.Lerp(1.4f, 2.4f, t / 1.4f); }
                h.SetCurrent(Mathf.Lerp(1f, newMax, Mathf.SmoothStep(0f, 1f, t / 1.4f)));   // HP bar refills
                t += Time.deltaTime;
                yield return null;
            }
            h.SetCurrent(newMax);
            h.Floor = to >= 3 ? 0f : 1f;                                     // only the last life can die
            if (glow != null) Destroy(glow.gameObject);
            // 2. BURST: switch form, swell from the old size to the new one (overshoot 8% then settle), wave pushes Ren out of the new body
            float s0 = Scale;
            ApplyForm(to, ai);
            float s1 = Scale;
            BossJuice.Fire(h);
            BossJuice.Impact(Pos + Vector2.up * 0.35f * FxK, Sap, 1.3f + 0.25f * to, 3, Layer, Order + 2);
            BossJuice.Burst("m3x_ichor_burst", Pos, 1.4f + 0.3f * to, Color.white, Layer, Order - 1, 14f);
            var w = Shockwave.Spawn(Pos, 1.4f + 0.2f * to, 0.5f, 0f, Order - 1, Sap);
            w.knockback = 3f;
            w.onHit = _ => { };
            for (float k = 0f; k < 0.35f; k += Time.deltaTime)
            {
                float e = k / 0.35f;
                transform.localScale = Vector3.one * Mathf.Lerp(s0, s1, e) * (1f + 0.08f * Mathf.Sin(e * Mathf.PI));
                yield return null;
            }
            transform.localScale = Vector3.one * s1;
            ai.PlayState("Idle");
            yield return ai.WaitFor(0.4f);
            h.Invulnerable = false;
            growing = false;
            nextSkill = Time.time + 0.9f;
            nextGrasp = Time.time + 2f;
        }

        // ------------------------------------------------------------------ shared wind-up
        /// wind-up = body flash + SKILL NAME on the HUD + green sap glowing brighter on the body (Light2D), flash at the end.
        private IEnumerator Gather(float seconds, string skill = null, Color? col = null)
        {
            ai.Desired = Vector2.zero;
            ai.PlayState("Prepare");
            Color c = col ?? Sap;
            if (!string.IsNullOrEmpty(skill)) BossJuice.Cast(ai.Health, skill, c, seconds);
            Vector2 chest = Pos + Vector2.up * 0.55f * FxK;
            var fx = SpriteVfx.Spawn("abs_charge2", chest, 0.9f * FxK, Layer, Order + 2, false, 12f / Mathf.Max(0.3f, seconds), false);
            if (fx != null) { fx.Tint(c); fx.Follow(transform); }
            var glow = BossJuice.Light(chest, c, 1.6f, 0.2f, 0f, transform);
            BossJuice.Run(RampGlow(glow, seconds));
            yield return ai.Flash(seconds, new Color(0.6f, 1f, 0.6f));
            if (!string.IsNullOrEmpty(skill)) BossJuice.Fire(ai.Health);
        }

        private static IEnumerator RampGlow(UnityEngine.Rendering.Universal.Light2D l, float seconds)
        {
            for (float t = 0f; t < seconds && l != null; t += Time.deltaTime) { l.intensity = Mathf.Lerp(0.2f, 1.5f, t / seconds); yield return null; }
            for (float t = 0f; t < 0.25f && l != null; t += Time.deltaTime) { l.intensity = 2f * (1f - t / 0.25f); yield return null; }
            if (l != null) Destroy(l.gameObject);
        }

        private IEnumerator Release(float hold)
        {
            ai.PlayState("Dash");
            yield return ai.WaitFor(hold);
            ai.PlayState("Recover");
            yield return ai.WaitFor(0.45f);
            ai.PlayState("Idle");
        }

        // ----------------------------------------------------------------- FORM 1: Root Scuttle
        private IEnumerator Scuttle()
        {
            SfxHooks.BossMove(this, "RootLine");
            ai.Desired = Vector2.zero;
            ai.PlayState("Prepare");
            Vector2 dir = ((Vector2)ai.Player.position - Pos).normalized;
            float width = DashTelegraph.WidthFor(ai.BodyCollider, 0.5f);
            float reach = DashTelegraph.Reach(Pos, dir, DashTelegraph.WantReach(Pos, ai.Player.position, 0.6f, 1.2f, 2.6f), 0.2f, ai.BodyCollider, ai.Player);
            BossJuice.Cast(ai.Health, "Root Scuttle", Root, 0.55f);
            var tg = DashTelegraph.Spawn(Pos, dir, reach, width, Root, 0.55f, Layer, Order - 1);
            yield return ai.WaitFor(0.55f);
            if (tg != null) tg.Release();
            if (ai.Health.IsDead) yield break;
            BossJuice.Fire(ai.Health);
            BossJuice.Burst("m3_dash_dust", Pos, 0.8f, Color.white, Layer, Order - 1, 16f);
            ai.PlayState("Dash");
            bool hit = false;
            const float speed = 3.6f;
            for (float t = 0f; t < reach / speed && !ai.Health.IsDead; t += Time.deltaTime)
            {
                ai.Desired = dir * speed;
                if (!hit && ai.GapToPlayer() <= 0.08f)
                {
                    hit = true;
                    ai.Hurt(ai.damage * 0.6f, Pos, 1.5f);
                    BossJuice.Impact((Vector2)ai.Player.position + Vector2.up * 0.3f, Root, 0.6f, 1, Layer, Order + 2);
                }
                yield return null;
            }
            ai.Desired = Vector2.zero;
            ai.PlayState("Recover");
            yield return ai.WaitFor(0.5f);
            ai.PlayState("Idle");
        }

        // ----------------------------------------------------------------- FORM 1-2: Seed Spit (small volley, small seeds)
        private IEnumerator SeedSpit(int n)
        {
            SfxHooks.BossMove(this, "Volley");
            yield return Gather(0.5f, "Seed Spit", Ichor);
            if (ai.Health.IsDead || ai.Player == null) yield break;
            var glob = SpriteVfx.Load("m3_ichor_glob");
            var splash = SpriteVfx.Load("m3x_ichor_burst");
            Vector2 target = ai.Player.position, mouth = Pos + Vector2.up * 0.25f * FxK;
            BossJuice.Flash(mouth, Ichor, 1.2f, 1.1f, 0.25f);
            for (int i = 0; i < n; i++)
            {
                Vector2 p = i == 0 ? target : target + Random.insideUnitCircle * 1.1f;
                var g = GroundStrike.Spawn(p, 0.8f + i * 0.1f, 0.26f, ai.damage * 0.6f, splash, 1, 14f, Order, Ichor);
                g.impactSfx = "acid_splash";
                g.bodyScale = 0.26f / 0.4f;
                g.impactLight = 0.8f;
                if (glob.Length > 0) { g.shell = glob[0]; g.shellFrom = mouth; g.shellArc = 0.7f; g.shellScale = 0.7f; g.shellFaceMotion = true; g.shellLight = 0.6f; }
            }
            yield return Release(0.35f);
        }

        // ----------------------------------------------------------------- FORM 2: Stump Slam (dash + stomp wave)
        private IEnumerator StumpSlam()
        {
            SfxHooks.BossMove(this, "BrambleRing");
            ai.Desired = Vector2.zero;
            ai.PlayState("Prepare");
            Vector2 dir = ((Vector2)ai.Player.position - Pos).normalized;
            float width = DashTelegraph.WidthFor(ai.BodyCollider, 0.6f);
            float reach = DashTelegraph.Reach(Pos, dir, DashTelegraph.WantReach(Pos, ai.Player.position, 0.2f, 0.8f, 2.2f), 0.25f, ai.BodyCollider, ai.Player);
            BossJuice.Cast(ai.Health, "Stump Slam", Root, 0.7f);
            var tg = DashTelegraph.Spawn(Pos, dir, reach, width, Root, 0.7f, Layer, Order - 1);
            yield return ai.WaitFor(0.7f);
            if (tg != null) tg.Release();
            if (ai.Health.IsDead) yield break;
            ai.PlayState("Dash");
            const float speed = 3f;
            for (float t = 0f; t < reach / speed && !ai.Health.IsDead; t += Time.deltaTime) { ai.Desired = dir * speed; yield return null; }
            ai.Desired = Vector2.zero;
            if (ai.Health.IsDead) yield break;
            // STOMP: damaging ring wave (can be rolled through) + green ground cracks
            BossJuice.Fire(ai.Health);
            ai.PlayState("Recover");
            BossJuice.Burst("m3x_lk_quake", Pos, 0.9f, new Color(0.75f, 1f, 0.7f), Layer, Order - 2, 14f);
            BossJuice.Impact(Pos + Vector2.up * 0.2f, Sap, 1f, 2, Layer, Order + 2);
            var w = Shockwave.Spawn(Pos, 1.3f, 0.55f, ai.damage * 0.7f, Order - 1, Root);
            w.knockback = 2.5f;
            yield return ai.WaitFor(0.6f);
            ai.PlayState("Idle");
        }

        // ------------------------------------------------------------------ Root Line / Root Fan
        private IEnumerator RootLine(bool fan)
        {
            SfxHooks.BossMove(this, "RootLine"); // sound
            yield return Gather(lineWarn, fan ? "Root Fan" : "Root Line", Root);
            if (ai.Health.IsDead || ai.Player == null) yield break;
            Vector2 dir = ((Vector2)ai.Player.position - Pos).normalized;
            float[] spread = fan ? new[] { -26f, 0f, 26f } : new[] { 0f };
            var frames = SpriteVfx.Load("m3x_root_erupt");                   // wooden roots break the ground, glowing sap cracks
            bool big = frames.Length > 0;
            if (!big) frames = SpriteVfx.Load("m3_root_spike");
            BossJuice.Shake(1);
            foreach (var a in spread)
            {
                Vector2 dd = Quaternion.Euler(0, 0, a) * dir;
                for (int i = 0; i < lineCount; i++)
                {
                    Vector2 p = Pos + dd * (0.45f + i * lineStep);
                    var g = GroundStrike.Spawn(p, 0.35f + i * lineDelay, spikeRadius, ai.damage, frames, 2, big ? 18f : 14f, Order, Root);
                    g.impactSfx = "root_spike"; // sound
                    g.bodyScale = big ? vfxScale * 0.95f : vfxScale;
                    g.knockback = 1.5f;
                    g.impactLight = 0.7f;
                    if (i == lineCount - 1 && a == 0f) g.impactShake = 1;
                }
            }
            yield return Release(0.5f);
        }

        // ------------------------------------------------------------------ Ichor Volley
        private IEnumerator Volley(int n)
        {
            SfxHooks.BossMove(this, "Volley");
            yield return Gather(0.45f, "Ichor Volley", Ichor);
            if (ai.Health.IsDead || ai.Player == null) yield break;
            var glob = SpriteVfx.Load("m3_ichor_glob");
            var splash = SpriteVfx.Load("m3x_ichor_burst");                   // glowing green sap splash
            bool fresh = splash.Length > 0;
            if (!fresh) splash = SpriteVfx.Load("m3_ground_burst");
            Vector2 target = ai.Player.position;
            Vector2 mouth = Pos + Vector2.up * 0.35f * FxK;
            BossJuice.Flash(mouth, Ichor, 1.6f, 1.3f, 0.3f);
            for (int i = 0; i < n; i++)
            {
                Vector2 p = i == 0 ? target : target + Random.insideUnitCircle * 1.3f;
                var g = GroundStrike.Spawn(p, volleyWarn + i * 0.1f, 0.32f, ai.damage * 0.8f, splash, 1, 14f, Order, Ichor);
                g.impactSfx = "acid_splash"; // sound
                g.bodyScale = fresh ? 0.32f / 0.4f : vfxScale;
                g.impactLight = 1f;
                if (glob.Length > 0)
                {
                    g.shell = glob[0]; g.shellFrom = mouth; g.shellArc = 0.9f; g.shellScale = vfxScale * 1.3f;
                    g.shellFaceMotion = true; g.shellLight = 0.7f;
                }
            }
            yield return Release(0.6f);
        }

        // ------------------------------------------------------------------ Bramble Ring (form 4: 2 rings)
        private IEnumerator BrambleRing(int rings)
        {
            SfxHooks.BossMove(this, "BrambleRing"); // sound
            yield return Gather(ringWarn, rings > 1 ? "Bramble Crown" : "Bramble Ring", Root);
            if (ai.Health.IsDead) yield break;
            float g0 = Random.Range(0f, 360f);
            for (int k = 0; k < rings && !ai.Health.IsDead; k++)
            {
                float g = g0 + k * 70f;                                      // second ring gap offset by 70°: the player must move
                BossJuice.Impact(Pos + Vector2.up * 0.2f, Root, 1.2f, 2, Layer, Order + 2);
                var w = Shockwave.Spawn(Pos, ringRadius, ringTime, ai.damage * 0.9f, Order - 1, Root);
                w.gapAngles = new[] { g, g + 180f };
                w.gapWidth = 50f;
                StartCoroutine(RingSpikes(g));
                if (k < rings - 1) yield return ai.WaitFor(0.75f);
            }
            yield return Release(0.6f);
        }

        /// Thorn roots rising along the ring wave (visual only: damage comes from Shockwave).
        private IEnumerator RingSpikes(float g0)
        {
            float[] radii = { 0.9f, 1.7f, 2.5f };
            foreach (var r in radii)
            {
                float k = 1f - Mathf.Sqrt(Mathf.Max(0f, 1f - r / ringRadius));
                yield return new WaitForSeconds(Mathf.Max(0f, k * ringTime - (r == radii[0] ? 0f : 0.05f)));
                int n = Mathf.RoundToInt(5 + r * 4);
                for (int i = 0; i < n; i++)
                {
                    float a = i * 360f / n;
                    if (Mathf.Abs(Mathf.DeltaAngle(a, g0)) < 30f || Mathf.Abs(Mathf.DeltaAngle(a, g0 + 180f)) < 30f) continue;
                    Vector2 p = Pos + new Vector2(Mathf.Cos(a * Mathf.Deg2Rad) * r, Mathf.Sin(a * Mathf.Deg2Rad) * r * 0.6f);
                    SpriteVfx.Spawn("m3_root_spike", p, vfxScale * 0.8f, Layer, Order, false, 18f);
                }
                BossJuice.Flash(Pos, Root, r + 0.8f, 0.9f, 0.3f);
                if (ai == null || ai.Health.IsDead) yield break;
            }
        }

        // ----------------------------------------------------------------- FORM 4: Grove Hammer (3 slams stepping forward)
        private IEnumerator GroveHammer()
        {
            SfxHooks.BossMove(this, "BrambleRing");
            ai.Desired = Vector2.zero;
            ai.PlayState("Prepare");                                         // LVL4: vung hai tay (24 khung)
            BossJuice.Cast(ai.Health, "Grove Hammer", Sap, 1f);
            Vector2 dir = ((Vector2)ai.Player.position - Pos).normalized;
            var frames = SpriteVfx.Load("m3x_lk_quake");
            var glow = BossJuice.Light(Pos + Vector2.up * 0.7f, Sap, 1.6f, 0.3f, 0f, transform);
            // 3 warning rings placed up front, bursting one after another toward Ren (the slam path can be read in advance)
            for (int i = 0; i < 3; i++)
            {
                Vector2 p = Pos + dir * (0.7f + i * 0.65f);
                var g = GroundStrike.Spawn(p, 1f + i * 0.22f, 0.5f, ai.damage, frames, 1, 14f, Order, new Color(0.7f, 1f, 0.45f));
                g.impactSfx = "ground_strike";
                g.bodyScale = 0.55f;
                g.knockback = 2f;
                g.impactLight = 1.3f; g.impactShake = 2;
            }
            for (float t = 0f; t < 1f && !ai.Health.IsDead; t += Time.deltaTime)
            {
                if (glow != null) glow.intensity = Mathf.Lerp(0.3f, 1.8f, t);
                yield return null;
            }
            if (glow != null) Destroy(glow.gameObject);
            if (ai.Health.IsDead) yield break;
            BossJuice.Fire(ai.Health);
            ai.PlayState("Dash");                                            // LVL4: repeated slams
            yield return ai.WaitFor(0.75f);
            ai.PlayState("Recover");
            yield return ai.WaitFor(0.5f);
            ai.PlayState("Idle");
        }

        // ------------------------------------------------------------------ Grasp
        private IEnumerator Grasp()
        {
            SfxHooks.BossMove(this, "Grasp"); // sound
            nextGrasp = Time.time + (form == 3 ? 3.2f : 4f);
            ai.Desired = Vector2.zero;
            ai.PlayState("Prepare");
            BossJuice.Cast(ai.Health, "Grasp", Root, graspWarn);
            var rf = SpriteVfx.Load("m3x_root_erupt");
            var g = GroundStrike.Spawn(ai.Player.position, graspWarn, 0.36f, ai.damage * 0.6f, rf.Length > 0 ? rf : SpriteVfx.Load("m3_root_spike"), 2, 18f,
                                       Order + 1, Root);
            g.impactSfx = "root_spike"; // sound
            g.bodyScale = rf.Length > 0 ? vfxScale * 1.05f : vfxScale;
            g.stunSeconds = graspStun;
            g.impactShake = 1;
            g.onHit = _ => BossJuice.Stop(0.05f);
            yield return ai.WaitFor(graspWarn);
            BossJuice.Fire(ai.Health);
            yield return Release(0.3f);
        }

        // ------------------------------------------------------------------ Communion (form 3, once)
        private IEnumerator Communion()
        {
            SfxHooks.BossMove(this, "Communion"); // sound
            heals++;
            ai.Desired = Vector2.zero;
            ai.PlayState("Prepare");
            Hints.Show("The Ancient Ent drinks from the graves. Tear it loose!", 2.5f);
            BossJuice.Cast(ai.Health, "Communion", Sap, healTime);
            var sapGlow = BossJuice.Light(Pos, Sap, 1.8f, 1.1f, 0f, transform);
            var pool = SpriteVfx.SpawnAttached("m3_sap_communion", transform, vfxScale, Layer, Order - 1, 12f, healTime);
            hitsTaken = 0;
            float t = 0f;
            bool broken = false;
            while (t < healTime && !ai.Health.IsDead)
            {
                if (hitsTaken >= healBreakHits) { broken = true; break; }
                // each form has its own HP bar -> heal at most 60% of this life's bar
                if (ai.Health.Percent < (HasForms ? 0.6f : 0.49f)) ai.Health.Heal(ai.Health.Max * healPerSecond * Time.deltaTime);
                t += Time.deltaTime;
                yield return null;
            }
            if (pool != null) pool.Stop();
            if (sapGlow != null) Destroy(sapGlow.gameObject);
            if (ai.Health.IsDead) yield break;
            ai.PlayState("Recover");
            if (broken)
            {
                BossJuice.Clear(ai.Health);
                BossJuice.Burst("m3x_ichor_burst", Pos, 1.4f, Color.white, Layer, Order + 2, 16f);
                BossJuice.Impact(Pos + Vector2.up * 0.4f, Sap, 1f, 2, Layer, Order + 2);
                BossJuice.Stop(0.06f);
                yield return ai.Flash(2f, new Color(1f, 1f, 0.55f));
            }
            else { BossJuice.Fire(ai.Health); yield return ai.WaitFor(0.5f); }
            ai.PlayState("Idle");
        }

        // ------------------------------------------------------------------ entering form 3: summon 2 saplings
        private IEnumerator Summon()
        {
            SfxHooks.BossMove(this, "Summon"); // sound
            summoned = true;
            yield return Gather(0.7f, "Call of the Grove", Sap);
            var dirr = MapEncounterDirector.Instance;
            for (int i = 0; i < 2 && dirr != null; i++)
            {
                Vector2 p = Pos + new Vector2(i == 0 ? -1.1f : 1.1f, -0.3f);
                SpriteVfx.Spawn("m3_root_spike", p, vfxScale, Layer, Order, false, 14f);
                BossJuice.Burst("m3x_root_erupt", p, vfxScale * 1.2f, Color.white, Layer, Order - 1, 18f);
                BossJuice.Flash(p, Sap, 1.4f, 1.2f, 0.4f);
                dirr.SpawnSummon("Ent_1", p, ai);
            }
            yield return Release(0.5f);
        }

        private void OnDisable()
        {
            if (ai != null) { BossJuice.Clear(ai.Health); if (growing && ai.Health != null) ai.Health.Invulnerable = false; }
        }
    }
}
