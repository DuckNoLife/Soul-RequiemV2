using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    public enum AcolyteElement { Fire, Frost, Storm, Umbral }

    /// <summary>
    /// Absalom's elemental acolytes, "The Choir of the Named" (Map 4). All share the church uniform (crimson hood, black-red robe);
    /// the SCARF and CENSER FLAME carry the element colour (PixelLab 35d3ff2b, 4 recolours of the teal original). Each acolyte has 1 element, 3 skills:
    ///   Pyre (Fire)     - Ember Volley (3 fire bolts in a fan) · Brand Circle (warning ring under Ren -> fire pool for 3 s)
    ///                     · Martyr's Pyre (HP &lt; 30%: stands still, self-immolates, red ring around itself for 1.3 s, then EXPLODES - run away).
    ///   Rime (Ice)      - Frost Lance (0.45 s aim line -> very fast ice spear) · Frost Nova (Ren close: ring of ice spikes, hit = slowed 1.5 s)
    ///                     · Rime Ward (30-point ice shield on the nearest ally - blocks ~2 hits; shields itself if nobody is near).
    ///   Storm (Thunder) - Chain Spark (fast bolt that SPLITS in two on hit / at max range) · Lightning Rod (plants a rod near Ren, 1.2 s later a 1-unit lightning ring)
    ///                     · Blink (Ren close: teleports far away, leaves a lightning rod at the old spot).
    ///   Umbral (Shadow) - Shade Orb (homing dark orb, bursts into shards up close) · Grasp (2 tentacle pools that root for 0.5 s)
    ///                     · Soul Tithe (HP &lt; 70% and an ally nearby: 2 s tether that drains the ally to heal itself - HIT the acolyte to break it).
    /// Caster movement: keeps 1.9-3.0 units away, strafes sideways and flips direction every 1.2-2 s; every attack has a telegraph line / ring.
    /// The uniform is shared, so players read the acolyte by COLOUR: scarf + flame + body light (Light2D) all use the element colour.
    /// </summary>
    public class AcolyteSkills : MonoBehaviour, IEnemySkills
    {
        public AcolyteElement element = AcolyteElement.Fire;
        public float preferMin = 1.9f, preferMax = 3.0f;

        private float nextPrimary, nextSecondary, nextSpecial, nextWard, strafeFlipAt;
        private float strafeSign = 1f;
        private bool martyrDone;
        private Light2D aura;
        private static Sprite pixel;

        public static readonly Color FireCol = new Color(1f, 0.55f, 0.28f), FrostCol = new Color(0.55f, 0.88f, 1f),
                                     StormCol = new Color(0.8f, 0.76f, 1f), UmbralCol = new Color(0.66f, 0.42f, 0.98f);

        public Color Col => element == AcolyteElement.Fire ? FireCol : element == AcolyteElement.Frost ? FrostCol
                          : element == AcolyteElement.Storm ? StormCol : UmbralCol;

        private void Start()
        {
            nextPrimary = Time.time + Random.Range(0.8f, 1.6f);
            nextSecondary = Time.time + Random.Range(3f, 4.5f);
            nextSpecial = Time.time + Random.Range(4f, 6f);
            nextWard = Time.time + Random.Range(6f, 9f);
            // element light around the body (readable from far away, even in dark corridors)
            var go = new GameObject("Acolyte Aura");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            aura = go.AddComponent<Light2D>();
            aura.lightType = Light2D.LightType.Point;
            aura.color = Col;
            aura.pointLightOuterRadius = 1.1f; aura.pointLightInnerRadius = 0.15f;
            aura.intensity = 0.7f;
        }

        private void Update()
        {
            if (aura == null) return;
            var hp = GetComponent<Health>();
            aura.enabled = hp == null || !hp.IsDead;
            aura.intensity = 0.6f + 0.15f * Mathf.Sin(Time.time * 3f + transform.position.x);
        }

        // ------------------------------------------------------------------ brain tick
        public IEnumerator Act(EnemyAI ai)
        {
            if (ai.Player == null) yield break;
            Vector2 me = transform.position, to = (Vector2)ai.Player.position - me;
            float d = to.magnitude;
            var hp = ai.Health;

            // special skill (conditional)
            switch (element)
            {
                case AcolyteElement.Fire:
                    if (!martyrDone && hp.Percent < 0.3f) { martyrDone = true; yield return MartyrsPyre(ai); yield break; }
                    break;
                case AcolyteElement.Frost:
                    if (d < 1.3f && Time.time >= nextSpecial) { nextSpecial = Time.time + 5f; yield return FrostNova(ai); yield break; }
                    break;
                case AcolyteElement.Storm:
                    if (d < 1.2f && Time.time >= nextSpecial) { nextSpecial = Time.time + 4f; yield return Blink(ai); yield break; }
                    break;
                case AcolyteElement.Umbral:
                    if (hp.Percent < 0.7f && Time.time >= nextSpecial) { var ally = NearestAlly(3.5f); if (ally != null) { nextSpecial = Time.time + 8f; yield return SoulTithe(ai, ally); yield break; } }
                    break;
            }
            if (element == AcolyteElement.Frost && Time.time >= nextWard)
            {
                var ally = NearestAlly(4f);
                nextWard = Time.time + 9f;
                yield return RimeWard(ai, ally != null ? ally : ai);
                yield break;
            }
            // secondary skill (area control)
            if (Time.time >= nextSecondary && d < 5f)
            {
                nextSecondary = Time.time + Random.Range(5f, 6.5f);
                switch (element)
                {
                    case AcolyteElement.Fire: yield return BrandCircle(ai); break;
                    case AcolyteElement.Frost: yield return FrostLance(ai, true); break;
                    case AcolyteElement.Storm: yield return LightningRod(ai, (Vector2)ai.Player.position + Random.insideUnitCircle * 0.4f, 1.2f); break;
                    default: yield return Grasp(ai); break;
                }
                yield break;
            }
            // main skill (ranged)
            if (Time.time >= nextPrimary && d < 5.5f)
            {
                nextPrimary = Time.time + Random.Range(2.2f, 3f);
                switch (element)
                {
                    case AcolyteElement.Fire: yield return EmberVolley(ai); break;
                    case AcolyteElement.Frost: yield return FrostLance(ai, false); break;
                    case AcolyteElement.Storm: yield return ChainSpark(ai); break;
                    default: yield return ShadeOrb(ai); break;
                }
                yield break;
            }
            // caster movement: keep range + strafe
            if (Time.time >= strafeFlipAt) { strafeFlipAt = Time.time + Random.Range(1.2f, 2f); strafeSign = -strafeSign; }
            Vector2 dir = to / Mathf.Max(0.001f, d), side = new Vector2(-dir.y, dir.x) * strafeSign;
            Vector2 want = d > preferMax ? dir : d < preferMin ? -dir + side * 0.4f : side * 0.6f;
            ai.Desired = want.normalized * ai.moveSpeed;
            ai.PlayState("Move", false);
        }

        // ------------------------------------------------------------------ shared helpers
        private Vector2 Hand(EnemyAI ai) => (Vector2)transform.position + Vector2.up * 0.36f + new Vector2(Mathf.Sign(ai.DirToPlayer.x) * 0.11f, 0f);   // scaled by 0.72

        private static IEnumerator Wait(float s)
        {
            for (float t = 0f; t < s; t += Time.deltaTime) { if (TimeStop.Active) t -= Time.deltaTime; yield return null; }
        }

        /// <summary>Wind-up: stop, face Ren, play the cast pose (Attack), gather element particles at the censer and flash the body.</summary>
        private IEnumerator Cast(EnemyAI ai, float windup)
        {
            ai.Desired = Vector2.zero;
            ai.FaceX(ai.DirToPlayer.x);
            ai.PlayState("Attack");
            var fx = SpriteVfx.Spawn("abs_charge2", Hand(ai), 0.7f, ai.SortingLayer, ai.SortingOrder + 3, false, 12f / Mathf.Max(0.3f, windup), false);
            if (fx != null) fx.Tint(Col);
            if (aura != null) aura.intensity = 1.6f;
            yield return ai.Flash(windup, Col);
        }

        private SpriteVfx Fx(EnemyAI ai, string sheet, Vector2 at, float scale, float fps, Color tint, int orderOff = 3)
        {
            var fx = SpriteVfx.Spawn(sheet, at, scale, ai.SortingLayer, ai.SortingOrder + orderOff, false, fps, false);
            if (fx != null) fx.Tint(tint);
            return fx;
        }

        private HallBullet Bolt(EnemyAI ai, string sheet, Vector2 at, Vector2 vel, float dmg, float radius, float life, Color tint)
        {
            var b = HallBullet.Spawn(SpriteVfx.Load(sheet), at, vel, dmg, radius, life, tint, 1f, ai.SortingLayer, ai.SortingOrder + 3);
            if (b != null) { b.popFx = "abs_arcane_hit"; b.popScale = 0.45f; b.popTint = Col; b.Glow(Col, 0.7f, 0.8f); }
            return b;
        }

        private GameObject Glow(Vector2 at, float radius, float intensity, float seconds)
        {
            var go = new GameObject("Acolyte Glow");
            go.transform.position = at;
            var l = go.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point; l.color = Col;
            l.pointLightOuterRadius = radius; l.pointLightInnerRadius = radius * 0.15f; l.intensity = intensity;
            if (seconds > 0f) Destroy(go, seconds);
            return go;
        }

        private EnemyAI NearestAlly(float range)
        {
            EnemyAI best = null; float bd = range * range;
            foreach (var e in FindObjectsByType<EnemyAI>(FindObjectsSortMode.None))
            {
                if (e == null || e.gameObject == gameObject || e.Health == null || e.Health.IsDead) continue;
                float dd = ((Vector2)(e.transform.position - transform.position)).sqrMagnitude;
                if (dd < bd) { bd = dd; best = e; }
            }
            return best;
        }

        private SpriteRenderer Line(EnemyAI ai)
        {
            if (pixel == null)
            {
                var tex = new Texture2D(1, 1) { filterMode = FilterMode.Point };
                tex.SetPixel(0, 0, Color.white); tex.Apply();
                pixel = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0f, 0.5f), 1f);
            }
            var go = new GameObject("Acolyte Tether");
            var sr = go.AddComponent<SpriteRenderer>();
            FxLighting.MakeUnlit(sr);
            sr.sprite = pixel;
            sr.sortingLayerID = ai.SortingLayer; sr.sortingOrder = ai.SortingOrder + 2;
            return sr;
        }

        private static void Stretch(SpriteRenderer sr, Vector2 a, Vector2 b, float w)
        {
            Vector2 d = b - a;
            sr.transform.position = a;
            sr.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            sr.transform.localScale = new Vector3(d.magnitude, w, 1f);
        }

        private void EndCast(EnemyAI ai)
        {
            ai.PlayState("Idle");
            if (aura != null) aura.intensity = 0.7f;
        }

        // ------------------------------------------------------------------ PYRE (Fire)
        private IEnumerator EmberVolley(EnemyAI ai)
        {
            yield return Cast(ai, 0.5f);
            if (ai.Health.IsDead || ai.Player == null) yield break;
            Vector2 from = Hand(ai), aim = ((Vector2)ai.Player.position + Vector2.up * 0.18f - from).normalized;
            Sfx.Play("trap_fire", from, 0.6f);
            var mz = Fx(ai, "abs_muzzle", from, 1f, 18f, FireCol);
            if (mz != null) mz.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);
            for (int i = -1; i <= 1; i++) Bolt(ai, "abs_blaster", from, Quaternion.Euler(0f, 0f, i * 16f) * aim * 4.2f, 8f, 0.11f, 2.2f, FireCol);
            yield return Wait(0.35f);
            EndCast(ai);
        }

        private IEnumerator BrandCircle(EnemyAI ai)
        {
            yield return Cast(ai, 0.4f);
            if (ai.Health.IsDead || ai.Player == null) yield break;
            Vector2 at = ai.Player.position;
            var g = GroundStrike.Spawn(at, 0.9f, 0.45f, 8f, null, 0, 14f, ai.SortingOrder + 1, FireCol);
            if (g != null) g.impactSfx = "keg_blast";
            EndCast(ai);
            StartCoroutine(BurnPool(at, 0.9f, 3f));
            yield return Wait(0.3f);
        }

        private IEnumerator BurnPool(Vector2 at, float delay, float seconds)
        {
            yield return Wait(delay);
            var pool = SpriteVfx.Spawn("abs_cinder_pool", at, 1f, 0, -3, false, 10f, false);
            if (pool != null) pool.Looping(seconds);
            var gl = Glow(at, 1.4f, 1f, seconds);
            Shake(1);
            var php = FindPlayerHealth();
            float next = 0f;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                if (TimeStop.Active) { t -= Time.deltaTime; yield return null; continue; }
                if (php != null && !php.IsDead && Time.time >= next && ((Vector2)php.transform.position - at).sqrMagnitude <= 0.4f * 0.4f)
                    if (php.TakeDamage(5f, Vector2.zero, 0f)) next = Time.time + 0.5f;
                yield return null;
            }
        }

        private IEnumerator MartyrsPyre(EnemyAI ai)
        {
            // stop, catch fire, red warning ring around itself for 1.3 s -> EXPLODE (dies with it)
            ai.Desired = Vector2.zero;
            ai.PlayState("Attack");
            Sfx.Play("trap_fire", transform.position, 1f);
            Vector2 at = transform.position;
            var g = GroundStrike.Spawn(at, 1.3f, 0.95f, 16f, null, 0, 14f, ai.SortingOrder + 1, new Color(1f, 0.3f, 0.15f));
            if (g != null) g.impactSfx = "keg_blast";
            var burn = SpriteVfx.SpawnAttached("abs_cinder_pool", transform, 1f, ai.SortingLayer, ai.SortingOrder + 1, 12f, 1.3f);
            if (burn != null) burn.Tint(Color.white);
            if (aura != null) aura.pointLightOuterRadius = 2f;
            yield return ai.Flash(1.3f, new Color(1f, 0.35f, 0.15f));
            Fx(ai, "abs_quake", at, 0.7f, 16f, new Color(1f, 0.6f, 0.35f), -1);
            Fx(ai, "abs_arcane_hit", at + Vector2.up * 0.3f, 1.8f, 16f, FireCol, 4);
            Glow(at, 3f, 2.4f, 0.4f);
            Shake(2);
            StartCoroutine(BurnPool(at, 0.05f, 2.5f));
            if (!ai.Health.IsDead) ai.Health.TakeDamage(99999f);
        }

        // ------------------------------------------------------------------ RIME (Ice)
        private IEnumerator FrostLance(EnemyAI ai, bool twin)
        {
            yield return Cast(ai, 0.35f);
            int n = twin ? 2 : 1;
            for (int k = 0; k < n; k++)
            {
                if (ai.Health.IsDead || ai.Player == null) yield break;
                Vector2 from = Hand(ai), aim = ((Vector2)ai.Player.position + Vector2.up * 0.18f - from).normalized;
                var tel = DashTelegraph.Spawn(from, aim, 7f, 0.14f, FrostCol, 0.45f, ai.SortingLayer, ai.SortingOrder - 2);
                yield return Wait(0.45f);
                if (tel != null) tel.Release();
                if (ai.Health.IsDead) yield break;
                var b = Bolt(ai, "abs_ice_lance2", from, aim * 8.5f, 9f, 0.12f, 1.3f, Color.white);
                if (b != null) { b.popFx = "abs_ice_shatter"; b.popScale = 0.8f; b.popTint = Color.white; }
                Fx(ai, "abs_ice_shatter", from, 0.6f, 22f, Color.white, 4);
                Sfx.Play("ice_shatter", from, 0.4f);
                yield return Wait(0.25f);
            }
            EndCast(ai);
        }

        private IEnumerator FrostNova(EnemyAI ai)
        {
            yield return Cast(ai, 0.45f);
            if (ai.Health.IsDead) yield break;
            Vector2 c = transform.position;
            var spike = SpriteVfx.Load("abs_rime");
            for (int i = 0; i < 9; i++)
            {
                Vector2 at = c + (Vector2)(Quaternion.Euler(0f, 0f, i * 40f) * Vector2.right) * 0.85f;
                var g = GroundStrike.Spawn(at, 0.25f, 0.34f, 8f, spike, 2, 16f, ai.SortingOrder + 1, FrostCol);
                if (g != null) { g.bodyScale = 0.7f; g.impactSfx = i == 0 ? "ice_shatter" : "none"; g.onHit = h => h.Chill(0.5f, 1.5f); }
            }
            Fx(ai, "abs_ice_shatter", c + Vector2.up * 0.3f, 1.8f, 18f, Color.white, 4);
            Glow(c, 2f, 1.8f, 0.4f);
            Shake(1);
            yield return Wait(0.5f);
            EndCast(ai);
        }

        private IEnumerator RimeWard(EnemyAI ai, EnemyAI target)
        {
            yield return Cast(ai, 0.5f);
            if (ai.Health.IsDead || target == null || target.Health == null || target.Health.IsDead) yield break;
            target.Health.SetBarrier(30f, 30f);
            Fx(ai, "abs_ice_shatter", (Vector2)target.transform.position + Vector2.up * 0.3f, 1.3f, 18f, Color.white, 4);
            var shell = SpriteVfx.SpawnAttached("abs_crackle", target.transform, 1.1f, ai.SortingLayer, ai.SortingOrder + 4, 12f, 8f);
            if (shell != null) shell.Tint(FrostCol);
            Sfx.Play("st_chill", target.transform.position, 0.6f);
            // short glowing line from the caster to the shielded ally
            var ln = Line(ai);
            for (float t = 0f; t < 0.4f && target != null; t += Time.deltaTime)
            {
                Stretch(ln, Hand(ai), (Vector2)target.transform.position + Vector2.up * 0.35f, 0.04f);
                ln.color = new Color(FrostCol.r, FrostCol.g, FrostCol.b, 1f - t / 0.4f);
                yield return null;
            }
            Destroy(ln.gameObject);
            EndCast(ai);
        }

        // ------------------------------------------------------------------ STORM (Thunder)
        private IEnumerator ChainSpark(EnemyAI ai)
        {
            yield return Cast(ai, 0.4f);
            if (ai.Health.IsDead || ai.Player == null) yield break;
            Vector2 from = Hand(ai), aim = ((Vector2)ai.Player.position + Vector2.up * 0.18f - from).normalized;
            var b = Bolt(ai, "abs_blaster", from, aim * 7f, 8f, 0.12f, 0.75f, StormCol);
            if (b != null) { b.expireOnPop = true; b.onExpire = Split; }
            Fx(ai, "abs_zap2", from, 0.7f, 20f, Color.white, 4);
            Sfx.Play("bolt_strike", from, 0.4f);
            yield return Wait(0.3f);
            EndCast(ai);
        }

        /// <summary>Bolt that splits in two (±50°) on hit / at max range; the smaller pieces do not split again.</summary>
        private void Split(HallBullet b)
        {
            if (b == null) return;
            Vector2 p = b.transform.position, v = b.velocity.sqrMagnitude > 0.01f ? b.velocity.normalized : Vector2.right;
            var fx = SpriteVfx.Spawn("abs_zap2", p, 0.8f, b.Sr != null ? b.Sr.sortingLayerID : 0, b.Sr != null ? b.Sr.sortingOrder + 1 : 5, false, 20f, false);
            if (fx != null) fx.Tint(Color.white);
            for (int s = -1; s <= 1; s += 2)
            {
                var c = HallBullet.Spawn(SpriteVfx.Load("abs_blaster"), p, (Vector2)(Quaternion.Euler(0f, 0f, s * 50f) * v) * 6f, 6f, 0.1f, 0.6f, StormCol, 0.8f,
                                         b.Sr != null ? b.Sr.sortingLayerID : 0, b.Sr != null ? b.Sr.sortingOrder : 5);
                if (c != null) { c.popFx = "abs_zap2"; c.popScale = 0.5f; c.popTint = Color.white; }
            }
        }

        private IEnumerator LightningRod(EnemyAI ai, Vector2 at, float warn)
        {
            yield return Cast(ai, 0.35f);
            if (ai.Health.IsDead) yield break;
            PlantRod(ai, at, warn);
            EndCast(ai);
            yield return Wait(0.3f);
        }

        private void PlantRod(EnemyAI ai, Vector2 at, float warn)
        {
            var rod = new GameObject("Acolyte Storm Rod");
            rod.transform.position = at;
            var sr = rod.AddComponent<SpriteRenderer>();
            var f = SpriteVfx.Load("abs_storm_rod");
            if (f.Length > 0) sr.sprite = f[0];
            sr.sortingLayerID = ai.SortingLayer; sr.sortingOrder = ai.SortingOrder;
            sr.spriteSortPoint = SpriteSortPoint.Pivot;
            var arcs = SpriteVfx.Spawn("abs_crackle", at + Vector2.up * 0.3f, 1f, ai.SortingLayer, ai.SortingOrder + 2, false, 14f, false);
            if (arcs != null) { arcs.Looping(warn + 0.3f); arcs.transform.SetParent(rod.transform, true); }
            var g = GroundStrike.Spawn(at, warn, 1.0f, 10f, null, 0, 14f, ai.SortingOrder + 1, StormCol);
            if (g != null) g.impactSfx = "bolt_strike";
            Fx(ai, "abs_zap2", at + Vector2.up * 0.2f, 0.9f, 18f, Color.white, 4);
            StartCoroutine(RodStrike(ai, rod, at, warn));
        }

        private IEnumerator RodStrike(EnemyAI ai, GameObject rod, Vector2 at, float warn)
        {
            yield return Wait(Mathf.Max(0f, warn - 0.12f));
            var sb = SpriteVfx.Spawn("abs_sky_bolt", at, 0.9f, ai != null ? ai.SortingLayer : 0, ai != null ? ai.SortingOrder + 1 : 3, false, 28f, false);
            if (sb != null) { var sr = sb.GetComponent<SpriteRenderer>(); if (sr != null) sr.spriteSortPoint = SpriteSortPoint.Pivot; }
            yield return Wait(0.12f);
            var z = SpriteVfx.Spawn("abs_zap2", at + Vector2.up * 0.2f, 1.4f, ai != null ? ai.SortingLayer : 0, ai != null ? ai.SortingOrder + 3 : 5, false, 18f, false);
            if (z != null) z.Tint(Color.white);
            var gl = Glow(at, 2.4f, 2f, 0.3f);
            Shake(1);
            if (rod != null) Destroy(rod, 0.1f);
        }

        private IEnumerator Blink(EnemyAI ai)
        {
            if (ai.Player == null) yield break;
            Vector2 me = transform.position, away = (me - (Vector2)ai.Player.position).normalized;
            Vector2 best = me; float bestScore = -1f;
            for (int i = 0; i < 12; i++)
            {
                Vector2 d = Quaternion.Euler(0f, 0f, i * 30f) * away;
                Vector2 p = me + d * 2.6f;
                if (Blocked(me, p)) continue;
                float score = ((Vector2)ai.Player.position - p).magnitude;
                if (score > bestScore) { bestScore = score; best = p; }
            }
            if (bestScore < 0f) yield break;
            Fx(ai, "abs_zap2", me + Vector2.up * 0.3f, 1.1f, 20f, Color.white, 4);
            Glow(me, 1.8f, 1.6f, 0.25f);
            Sfx.Play("teleport_dark", me, 0.6f);
            PlantRod(ai, me, 0.9f);                                                    // leave a lightning rod at the old spot
            ai.TeleportTo(best);
            Fx(ai, "abs_zap2", best + Vector2.up * 0.3f, 1.1f, 20f, Color.white, 4);
            yield return Wait(0.35f);
        }

        /// <summary>True if the path me -> p, or standing at p, hits a solid collider (ignores triggers, itself and Ren).</summary>
        private bool Blocked(Vector2 me, Vector2 p)
        {
            foreach (var h in Physics2D.LinecastAll(me, p))
                if (h.collider != null && !h.collider.isTrigger && !h.collider.transform.IsChildOf(transform) && h.collider.GetComponentInParent<PlayerController>() == null) return true;
            foreach (var c in Physics2D.OverlapCircleAll(p, 0.22f))
                if (c != null && !c.isTrigger && !c.transform.IsChildOf(transform) && c.GetComponentInParent<PlayerController>() == null) return true;
            return false;
        }

        // ------------------------------------------------------------------ UMBRAL (Shadow)
        private IEnumerator ShadeOrb(EnemyAI ai)
        {
            yield return Cast(ai, 0.55f);
            if (ai.Health.IsDead || ai.Player == null) yield break;
            Vector2 from = Hand(ai), aim = ((Vector2)ai.Player.position - from).normalized;
            var b = HallBullet.Spawn(SpriteVfx.Load("abs_void_orb"), from, aim * 1.3f, 9f, 0.16f, 5f, Color.white, 0.85f, ai.SortingLayer, ai.SortingOrder + 3);
            if (b != null)
            {
                b.homing = 80f; b.accel = 0.25f; b.fuseRadius = 0.45f; b.expireOnPop = true; b.popFx = "none";
                b.Glow(UmbralCol, 0.8f, 0.9f);
                b.onExpire = OrbBurst;
            }
            Fx(ai, "abs_void_burst", from, 0.7f, 18f, Color.white, 4);
            Sfx.Play("teleport_dark", from, 0.4f);
            yield return Wait(0.3f);
            EndCast(ai);
        }

        private static void OrbBurst(HallBullet b)
        {
            if (b == null) return;
            Vector2 p = b.transform.position;
            int layer = b.Sr != null ? b.Sr.sortingLayerID : 0, order = b.Sr != null ? b.Sr.sortingOrder : 5;
            SpriteVfx.Spawn("abs_void_burst", p, 1.1f, layer, order + 1, false, 16f, false);
            Sfx.Play("teleport_dark", p, 0.35f);
            float a0 = Random.Range(0f, 60f);
            for (int k = 0; k < 6; k++)
            {
                var s = HallBullet.Spawn(SpriteVfx.Load("abs_void_shard"), p, (Vector2)(Quaternion.Euler(0f, 0f, a0 + k * 60f) * Vector2.right) * 1.9f,
                                         7f, 0.1f, 2.5f, Color.white, 1f, layer, order);
                if (s != null) { s.popFx = "abs_void_burst"; s.popScale = 0.4f; }
            }
        }

        private IEnumerator Grasp(EnemyAI ai)
        {
            yield return Cast(ai, 0.45f);
            var frames = SpriteVfx.Load("abs_grasp");
            for (int k = 0; k < 2; k++)
            {
                if (ai.Health.IsDead || ai.Player == null) yield break;
                Vector2 at = ai.Player.position;
                var g = GroundStrike.Spawn(at, 0.5f, 0.42f, 8f, frames, 4, 14f, ai.SortingOrder + 1, UmbralCol);
                if (g != null) { g.stunSeconds = 0.5f; g.impactSfx = "root_spike"; }
                yield return Wait(0.5f);
            }
            EndCast(ai);
        }

        private IEnumerator SoulTithe(EnemyAI ai, EnemyAI ally)
        {
            yield return Cast(ai, 0.35f);
            if (ai.Health.IsDead || ally == null) yield break;
            ai.PlayState("Attack");
            var ln = Line(ai);
            var ln2 = Line(ai);
            float hp0 = ai.Health.Current, next = 0f;
            Sfx.Play("life_drain", transform.position, 0.7f);
            for (float t = 0f; t < 2f; t += Time.deltaTime)
            {
                if (TimeStop.Active) { t -= Time.deltaTime; yield return null; continue; }
                if (ai.Health.IsDead || ally == null || ally.Health == null || ally.Health.IsDead) break;
                if (ai.Health.Current < hp0 - 0.5f)                                    // hit -> tether breaks
                {
                    Fx(ai, "abs_void_burst", Hand(ai), 0.8f, 18f, Color.white, 4);
                    DrillBoardSafeHint();
                    break;
                }
                Vector2 a = (Vector2)ally.transform.position + Vector2.up * 0.35f, b = Hand(ai);
                Stretch(ln, a, b, 0.09f + 0.03f * Mathf.Sin(Time.time * 20f));
                Stretch(ln2, a, b, 0.03f);
                ln.color = new Color(UmbralCol.r, UmbralCol.g, UmbralCol.b, 0.55f); ln2.color = Color.white;
                if (Time.time >= next)
                {
                    next = Time.time + 0.25f;
                    ally.Health.TakeDamage(4f);
                    ai.Health.Heal(4f);
                    hp0 = ai.Health.Current;
                    var m = HallBullet.Spawn(SpriteVfx.Load("abs_void_shard"), a, (b - a) * 2.5f, 0f, 0f, 0.4f, Color.white, 0.8f, ai.SortingLayer, ai.SortingOrder + 3);
                    if (m != null) { m.harmless = true; m.stopOnWalls = false; m.popFx = "none"; }
                }
                yield return null;
            }
            if (ln != null) Destroy(ln.gameObject);
            if (ln2 != null) Destroy(ln2.gameObject);
            EndCast(ai);
        }

        private static void DrillBoardSafeHint()
        {
            FirstTimeHints.Show("tut_soul_tithe", "Strike the Umbral acolyte to cut its tether.", 4f);
        }

        // ------------------------------------------------------------------ utilities
        private static Health playerHp;

        private static Health FindPlayerHealth()
        {
            if (playerHp == null)
            {
                var pc = FindFirstObjectByType<PlayerController>();
                if (pc != null) playerHp = pc.GetComponent<Health>();
            }
            return playerHp;
        }

        private static void Shake(int tier)
        {
            CameraFollow.KickMain(Vector2.down, tier >= 2 ? 0.08f : 0.04f, tier >= 2 ? 0.28f : 0.16f);
        }
    }
}
