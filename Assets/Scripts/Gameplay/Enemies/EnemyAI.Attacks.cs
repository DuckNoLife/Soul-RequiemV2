using System;
using System.Collections;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// NORMAL ENEMY ATTACKS (per EnemyKind / EnemySpecial in the Inspector / EnemyDefinition): MeleeAttack, SpinAttack, Dash,
    /// SpikeAttack (rock spikes), RangedAttack + TurretTick (shots / arcing shells), BossCombo (Grok). Damage: HitPlayer, SpawnProjectile.
    /// </summary>
    public partial class EnemyAI
    {
        // ------------------------------------------------------------------ attacks
        private IEnumerator MeleeAttack(string state, float dmg, float totalTime, float hitAt)
        {
            desired = Vector2.zero;
            Play(state, true);
            if (vfx != null) vfx.Windup(DirToPlayer);
            MeleeWindup?.Invoke(this);
            AttackStarted?.Invoke(this);
            yield return Wait(hitAt);
            if (Health.IsDead) yield break;
            if (vfx != null) vfx.Swing(DirToPlayer);
            int hits = Mathf.Max(1, comboHits);
            for (int i = 0; i < hits; i++)
            {
                if (i > 0) { yield return Wait(comboGap); if (Health.IsDead) yield break; }
                if (Gap() <= attackRange + 0.2f) HitPlayer(dmg);
            }
            yield return Wait(Mathf.Max(0f, totalTime - hitAt - comboGap * (hits - 1)));
            Play("Idle");
            yield return Wait(cooldown);
        }

        /// Greystone Warrior — attack 2: wind-up (orange flashes) then spin, sliding slowly toward the player.
        private IEnumerator SpinAttack()
        {
            desired = Vector2.zero;
            Play("Windup", true);
            yield return Telegraph(specialWindup, new Color(1f, 0.6f, 0.35f));
            if (Health.IsDead) yield break;
            Play("Spin", true);
            float t = 0f, nextTick = 0f;
            while (t < spinTime && !Health.IsDead && player != null)
            {
                Vector2 to = (Vector2)(player.position - transform.position);
                desired = to.normalized * moveSpeed * spinMoveFactor;
                if (t >= nextTick)
                {
                    nextTick += spinTick;
                    if (to.magnitude <= spinRadius) HitPlayer(damage * 0.6f);
                }
                t += Time.deltaTime;
                yield return null;
            }
            desired = Vector2.zero;
            Play("SpinEnd", true);
            yield return Wait(spinEndTime);
            Play("Idle");
            yield return Wait(cooldown * 1.3f);   // stagger after the spin: counter window
        }

        private bool CanDash(float d, float gap) =>
            enrageAt > 0f && Health.Percent <= enrageAt && Time.time >= nextDash && gap > attackRange + 0.3f && d < 3f;

        /// Mad Bear below 50% HP: short wind-up then a straight charge.
        private IEnumerator Dash(Vector2 toPlayer)
        {
            nextDash = Time.time + dashCooldown;
            desired = Vector2.zero;
            Play("Windup", true);
            // warning lane (DashTelegraph) + charge distance to Ren's spot + 0.8 u (2..3.8 u)
            Vector2 dir = ((Vector2)(player.position - transform.position)).normalized;
            float reach = DashTelegraph.Reach(transform.position, dir, DashTelegraph.WantReach(transform.position, player.position, 0.8f, 2f, 3.8f), 0.25f, bodyCol, player);
            var tg = DashTelegraph.Spawn(transform.position, dir, reach, DashTelegraph.WidthFor(bodyCol, 0.6f), new Color(1f, 0.4f, 0.35f), 0.6f, SortingLayer, SortingOrder - 1);
            yield return Telegraph(0.6f, new Color(1f, 0.4f, 0.4f));
            tg.Release();
            if (Health.IsDead || player == null) yield break;
            Face(dir.x);
            Play("Move", true);
            bool hit = false;
            float t = 0f, runTime = reach / Mathf.Max(0.1f, dashSpeed);
            while (t < runTime && !Health.IsDead)
            {
                desired = dir * dashSpeed;
                if (!hit && Gap() <= 0.1f) { HitPlayer(damage * 1.2f); hit = true; }
                t += Time.deltaTime;
                yield return null;
            }
            desired = Vector2.zero;
            Play("Idle");
            yield return Wait(0.5f);
        }

        private IEnumerator SpikeAttack()
        {
            desired = Vector2.zero;
            Play("Attack", true);
            AttackStarted?.Invoke(this);
            yield return Telegraph(windup, new Color(1f, 0.95f, 0.5f));
            if (Health.IsDead) yield break;
            float offset = BulletPatterns.RandomRingOffset(spikeCount);
            foreach (var dir in BulletPatterns.Ring(spikeCount, offset)) SpawnProjectile(dir, true);
            ProjectilesFired?.Invoke(this);
            yield return Wait(Mathf.Max(0f, attackTime - windup));
            Play("Idle");
            yield return Wait(cooldown);
        }

        private IEnumerator RangedAttack()
        {
            desired = Vector2.zero;
            attackCount++;
            bool spec = special == EnemySpecial.GroundSpikes && specialEvery > 0 && attackCount % specialEvery == 0
                        && specialFrames != null && specialFrames.Length > 0;
            Play(spec && HasState("Cast") ? "Cast" : "Attack", true);
            if (vfx != null) vfx.Cast();
            AttackStarted?.Invoke(this);
            yield return Wait(windup);
            if (Health.IsDead || player == null) yield break;

            int order = sr != null ? sr.sortingOrder : 2;
            if (spec)
            {
                var g = GroundStrike.Spawn(player.position, 0.65f, specialRadius, damage * 1.2f, specialFrames,
                                           specialImpactFrame, strikeFps, order, strikeColor);
                g.knockback = 2.5f;
            }
            else if (strikeFrames != null && strikeFrames.Length > 0)
            {
                GroundStrike.Spawn(player.position, strikeWarn, strikeRadius, damage, strikeFrames,
                                   strikeImpactFrame, strikeFps, order, strikeColor);
            }
            else { SpawnProjectile((Vector2)(player.position - transform.position), false); ProjectilesFired?.Invoke(this); }

            yield return Wait(Mathf.Max(0f, attackTime - windup));
            Play("Idle");
            // strafe a little during the cooldown so it is harder to close in
            Vector2 side = Vector2.Perpendicular((Vector2)(player.position - transform.position)).normalized;
            if (UnityEngine.Random.value < 0.5f) side = -side;
            desired = side * moveSpeed * 0.6f;
            Play("Move");
            yield return Wait(cooldown);
            desired = Vector2.zero;
        }

        /// Camp Gunner: turn the barrel toward the player; every `cooldown` seconds fire one arcing shell at where the player stands.
        private IEnumerator TurretTick(Vector2 toPlayer, float d)
        {
            desired = Vector2.zero;
            Aim(toPlayer);
            if (d > aggroRange * 1.25f) { aggro = false; yield break; }
            if (Time.time < nextShot) yield break;
            nextShot = Time.time + cooldown;
            yield return Telegraph(0.25f, new Color(1f, 0.7f, 0.4f));
            if (Health.IsDead || player == null) yield break;
            int order = sr != null ? sr.sortingOrder : 2;
            var g = GroundStrike.Spawn(player.position, strikeWarn, strikeRadius, damage, strikeFrames,
                                       strikeImpactFrame, strikeFps, order, strikeColor);
            g.shell = shellSprite;
            g.shellFrom = (Vector2)transform.position + Vector2.up * 0.2f;
            g.knockback = knockback;
        }

        private void Aim(Vector2 v)
        {
            if (aimSprites == null || aimSprites.Length < 5 || sr == null) return;
            float a = Mathf.Atan2(v.y, Mathf.Abs(v.x)) * Mathf.Rad2Deg;
            int i = a > 67.5f ? 2 : a > 22.5f ? 1 : a > -22.5f ? 0 : a > -67.5f ? 3 : 4;
            if (aimSprites[i] != null) sr.sprite = aimSprites[i];
            sr.flipX = v.x < 0f && i != 2 && i != 4;
        }

        private IEnumerator BossCombo()
        {
            // 1. Wind-up — clear warning
            desired = Vector2.zero;
            Play("Windup");
            // the warning lane follows Ren for the first 75% of the wind-up, then locks;
            // charge distance = exactly to Ren's spot (1.6..4 u), then slam
            Vector2 dir = ((Vector2)(player.position - transform.position)).normalized;
            float reach = 2f;
            DashTelegraph tg = null;
            StartCoroutine(Telegraph(windup, new Color(1f, 0.6f, 0.4f)));
            for (float w = 0f; w < windup && !Health.IsDead; w += Time.deltaTime)
            {
                if (player != null && (w < windup * 0.75f || tg == null))
                {
                    dir = ((Vector2)(player.position - transform.position)).normalized;
                    reach = DashTelegraph.Reach(transform.position, dir, DashTelegraph.WantReach(transform.position, player.position, 0.1f, 1.6f, 4f), 0.25f, bodyCol, player);
                }
                if (tg == null) tg = DashTelegraph.Spawn(transform.position, dir, reach, DashTelegraph.WidthFor(bodyCol, 0.7f), new Color(1f, 0.55f, 0.35f), windup, SortingLayer, SortingOrder - 1);
                else tg.Aim(transform.position, dir, reach);
                Face(dir.x);
                yield return null;
            }
            if (tg != null) tg.Release();
            if (Health.IsDead || player == null) yield break;

            // 2. Charge to where the player stood when the wind-up ended, slam on arrival
            Face(dir.x);
            Play("Attack");
            bool hitDuringCharge = false, hitPillar = false;
            float t = 0f, runTime = (reach + 0.3f) / Mathf.Max(0.1f, chargeSpeed);   // +0.3: still crashes into a stone pillar at the end of the lane (RingPillar)
            while (t < runTime && !Health.IsDead)
            {
                desired = dir * chargeSpeed;
                // crashed into a stone pillar -> the pillar breaks, long stagger (the player lures the boss into pillars)
                var pillar = bodyCol != null ? RingPillar.Touching(bodyCol.bounds, 0.06f) : null;
                if (pillar != null) { pillar.Break(); hitPillar = true; break; }
                if (!hitDuringCharge && Gap() <= 0.1f)
                {
                    HitPlayer(damage * 0.8f);
                    hitDuringCharge = true;
                }
                t += Time.deltaTime;
                yield return null;
            }
            desired = Vector2.zero;
            if (hitPillar)
            {
                Play("Recovery");
                Hints.Show($"{displayName} smashed into the standing stone - strike now!", 2.5f);
                yield return Wait(pillarStun);
                Play("Idle");
                yield return Wait(cooldown);
                yield break;
            }
            SoulRequiem.Core.Sfx.Play("boss_slam", transform.position); // sound: slam at the end of the charge
            if (!hitDuringCharge && Gap() <= slamRadius) HitPlayer(damage);
            yield return Wait(Mathf.Max(0f, attackTime - runTime));

            // 3. Stagger — counter window from the logbook
            Play("Recovery");
            yield return Wait(recoveryTime);
            Play("Idle");
            yield return Wait(cooldown);
        }

        // ------------------------------------------------------------------ helpers
        private void HitPlayer(float amount)
        {
            if (playerHealth == null) return;
            // melee hits do not pass through rocks / walls
            Vector2 from = bodyCol != null ? (Vector2)bodyCol.bounds.center : (Vector2)transform.position;
            if (!LineOfSight.CanSee(from, playerCol)) return;
            Vector2 dir = (Vector2)(player.position - transform.position);
            if (!playerHealth.TakeDamage(amount, dir, knockback)) return;
            if (vfx != null) vfx.HurtPlayer(player.position);
            if (poisonPercent > 0f) playerHealth.ApplyPoison(poisonPercent, poisonDuration);
            if (stunSeconds > 0f) playerHealth.Stun(stunSeconds);
        }

        private void SpawnProjectile(Vector2 dir, bool spike)
        {
            GameObject go;
            if (projectilePrefab != null) go = Instantiate(projectilePrefab, transform.position + Vector3.up * 0.15f, Quaternion.identity);
            else
            {
                go = new GameObject(spike ? "Spike" : "Projectile");
                go.transform.position = transform.position + Vector3.up * 0.15f;
                var s = go.AddComponent<SpriteRenderer>();
                s.sprite = Projectile.SpikeSprite;
                s.sortingOrder = 1;
                go.AddComponent<Projectile>();
            }
            var p = go.GetComponent<Projectile>();
            p.lifetime = projectileLifetime;
            p.knockback = knockback;
            p.poisonPercent = poisonPercent;
            p.poisonDuration = poisonDuration;
            p.Launch(dir, projectileSpeed, damage);
        }
    }
}
