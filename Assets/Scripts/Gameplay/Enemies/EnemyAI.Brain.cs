using System;
using System.Collections;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// ENEMY BRAIN (Brain loop): guard -> sees Ren (SeesPlayer, aggroRange) -> chase (Chase, EnemySteering avoids obstacles)
    /// -> attack when in range (Attacks) -> lost sight / trapped (CheckTrapped) -> return to spawn (StartReturning).
    /// </summary>
    public partial class EnemyAI
    {
        // ------------------------------------------------------------------ brain
        private IEnumerator Brain()
        {
            yield return new WaitForSeconds(UnityEngine.Random.Range(0.1f, 0.5f));   // offset the timing between enemies
            while (!Health.IsDead)
            {
                if (inShell || Held) { desired = Vector2.zero; yield return null; continue; }
                if (CutsceneState.Active && !CutsceneActor) { desired = Vector2.zero; Play("Idle"); yield return null; continue; }
                if (player == null || Health.IsStunned || (playerHealth != null && playerHealth.IsDead))
                {
                    desired = Vector2.zero;
                    Play("Idle");
                    yield return null;
                    continue;
                }

                Vector2 toPlayer = (Vector2)(player.position - transform.position);
                float d = toPlayer.magnitude;
                float gap = Gap();
                if (kind != EnemyKind.Turret) Face(toPlayer.x);

                // Leash: chased too far from spawn -> return; trapped -> return too (not bosses: the arena is locked)
                if (!IsBoss && kind != EnemyKind.Turret)
                {
                    Vector2 fromHome = (Vector2)transform.position - home;
                    if (leashRange > 0f && aggro && fromHome.magnitude > leashRange) StartReturning();
                    if (returning)
                    {
                        // arrived — or never arrives (old spot blocked): stop and make this spot "home"
                        if (fromHome.magnitude < 0.15f || Time.time - returningSince > 6f)
                        {
                            returning = false; desired = Vector2.zero; Play("Idle");
                            if (fromHome.magnitude >= 0.15f) home = transform.position;
                        }
                        else
                        {
                            desired = steering != null ? steering.Steer(home, moveSpeed * 1.3f) : -fromHome.normalized * moveSpeed * 1.3f;
                            Face(-fromHome.x); Play("Move");
                        }
                        Health.Heal(Health.Max * 0.5f * Time.deltaTime);
                        yield return null;
                        continue;
                    }
                }

                if (!aggro)
                {
                    bool playerNearHome = leashRange <= 0f || IsBoss ||
                                          ((Vector2)player.position - home).magnitude <= leashRange;
                    // must SEE the player (not through rocks / walls) — unless very close
                    bool notices = d <= aggroRange && playerNearHome && (SeesPlayer() || d <= aggroRange * 0.35f);
                    // just retreated after being trapped: only re-aggro when the player really closes in
                    if (notices && Time.time < reaggroGuardUntil && gap > attackRange + 0.3f) notices = false;
                    if (notices) { aggro = true; reachGap = float.MaxValue; noProgressT = 0f; SfxHooks.EnemyAlert(this); } // sound
                    else { desired = Vector2.zero; Play("Idle"); yield return null; continue; }
                }

                if (CheckTrapped(gap)) { StartReturning(); reaggroGuardUntil = Time.time + 4f; yield return null; continue; }

                switch (kind)
                {
                    case EnemyKind.Melee:
                        if (CanDash(d, gap)) yield return Dash(toPlayer);
                        else if (gap > attackRange) Chase(toPlayer);
                        else
                        {
                            attackCount++;
                            if (special == EnemySpecial.Spin && specialEvery > 0 && attackCount % specialEvery == 0)
                                yield return SpinAttack();
                            else
                                yield return MeleeAttack("Attack", damage, attackTime, windup);
                        }
                        break;

                    case EnemyKind.SpikeBurst:
                        if (d > spikeTriggerRange) Chase(toPlayer);
                        else yield return SpikeAttack();
                        break;

                    case EnemyKind.Ranged:
                        if (closeRange > 0f && gap <= closeRange && HasState("Attack2"))
                            yield return MeleeAttack("Attack2", damage * 0.7f, attack2Time, windup * 0.8f);
                        else if (d > aggroRange) Chase(toPlayer);
                        else if (d < keepDistance * 0.7f) { desired = -toPlayer.normalized * moveSpeed; Play("Move"); }
                        else yield return RangedAttack();
                        break;

                    case EnemyKind.Boss:
                        if (gap > attackRange) Chase(toPlayer);
                        else yield return BossCombo();
                        break;

                    case EnemyKind.Turret:
                        yield return TurretTick(toPlayer, d);
                        break;

                    case EnemyKind.Custom:
                        if (skills == null) skills = GetComponent<IEnemySkills>();
                        if (skills != null) yield return skills.Act(this);
                        else Chase(toPlayer);
                        break;

                    case EnemyKind.Beholder:
                        desired = Vector2.zero;
                        if (skills == null) skills = GetComponent<IEnemySkills>();
                        if (skills != null) yield return skills.Act(this);
                        else yield return RangedAttack();
                        break;
                }
                yield return null;
            }
        }

        private void Chase(Vector2 toPlayer)
        {
            // avoid obstacles instead of running straight into rocks (EnemySteering)
            desired = steering != null && player != null ? steering.Steer(player.position, moveSpeed) : toPlayer.normalized * moveSpeed;
            Play(desired.sqrMagnitude > 0.0001f ? "Move" : "Idle");
        }

        private void StartReturning()
        {
            aggro = false;
            returning = true;
            returningSince = Time.time;
            reachGap = float.MaxValue;
            noProgressT = 0f;
        }

        /// <summary>The player is in line of sight (not blocked by obstacles). Checked 5 times / second.</summary>
        private bool SeesPlayer()
        {
            if (Time.time < sightCheckAt) return seesPlayer;
            sightCheckAt = Time.time + 0.2f;
            Vector2 eye = bodyCol != null ? (Vector2)bodyCol.bounds.center : (Vector2)transform.position;
            seesPlayer = LineOfSight.CanSee(eye, playerCol);
            return seesPlayer;
        }

        /// <summary>
        /// "Trapped": a melee enemy was just hit but could not close the distance for TrapNoProgressSeconds seconds
        /// (stuck behind a rock, across water...). It then returns to its spot and heals — the player has to fight it properly.
        /// </summary>
        private bool CheckTrapped(float gap)
        {
            bool melee = kind == EnemyKind.Melee || kind == EnemyKind.SpikeBurst;
            if (!melee || IsBoss || floating || Summoned || gap <= attackRange) { noProgressT = 0f; reachGap = gap; return false; }
            if (Time.time >= reachCheckAt)
            {
                reachCheckAt = Time.time + 0.5f;
                noProgressT = gap > reachGap - 0.05f ? noProgressT + 0.5f : 0f;   // no closer than half a second ago
                reachGap = gap;
            }
            return noProgressT >= TrapNoProgressSeconds && Time.time - lastDamagedAt < TrapRecentHitSeconds;
        }
    }
}
