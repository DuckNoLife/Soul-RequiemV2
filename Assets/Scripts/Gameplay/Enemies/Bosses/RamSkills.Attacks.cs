using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// SKILLS of RamSkills (one coroutine per skill). Settings / skill choice: RamSkills.cs.
    /// </summary>
    public partial class RamSkills
    {
        public IEnumerator Act(EnemyAI owner)
        {
            ai = owner;
            if (!engaged) Engage();
            if (ai.Player == null) yield break;
            Vector2 pos = transform.position, pp = ai.Player.position;

            if (SluiceMechanism.Wet(pos)) { yield return Quench(); yield break; }       // stepped into water

            if (ai.InPhase2) { yield return Rampage(); yield break; }

            float dist = (pp - pos).magnitude;
            if (Time.time >= nextCharge && dist >= chargeMinDistance)
            {
                yield return Charge();
                nextCharge = Time.time + Random.Range(chargeEveryMin, chargeEveryMax);
                yield break;
            }
            float gap = ai.GapToPlayer();
            if (gap <= goreRange && Time.time >= nextGore)
            {
                yield return Gore();
                nextGore = Time.time + goreCooldown;
                yield break;
            }
            // walk: faster than Ren; stop when right next to him (do not bulldoze Ren into walls)
            Vector2 to = pp - pos;
            Vector2 step = gap <= 0.1f ? Vector2.zero : to.normalized * ai.moveSpeed * SpeedMul;
            ai.FaceX(to.x);
            ai.Desired = step;
            ai.PlayState(step == Vector2.zero ? "Idle" : "Move", false);
            Absorb(pos);
            Nudge();
        }

        // ------------------------------------------------------------------ hints
        private IEnumerator Tutorial()
        {
            yield return new WaitForSeconds(2.7f);
            if (ai != null && !ai.Health.IsDead)
                Hints.Show("Its Ether hide turns steel. Strike a glowing valve on the north wall - cold water breaks it.", 5f);
        }

        // ------------------------------------------------------------------ phase 1: butt
        private IEnumerator Gore()
        {
            ai.Desired = Vector2.zero;
            ai.PlayState("ChargeBegin");
            ai.FaceX(ai.Player.position.x - transform.position.x);
            yield return ai.Flash(Windup(goreWindup), new Color(1f, 0.45f, 0.35f));
            if (ai.Health.IsDead || ai.Player == null) yield break;
            Vector2 dir = ((Vector2)ai.Player.position - (Vector2)transform.position).normalized;
            ai.FaceX(dir.x);
            ai.PlayState("ChargeRun");
            IgnorePlayer(true);
            bool hit = false;
            for (float t = 0f; t < goreTime && !ai.Health.IsDead; t += Time.deltaTime)
            {
                ai.Desired = dir * goreSpeed * SpeedMul;
                if (!hit && ai.GapToPlayer() <= 0.08f) { ai.Hurt(ai.damage, transform.position, 3f); hit = true; }
                yield return null;
                if (SluiceMechanism.Wet(transform.position)) { ai.Desired = Vector2.zero; IgnorePlayer(false); yield return Quench(dir); yield break; }
            }
            ai.Desired = Vector2.zero;
            IgnorePlayer(false);
            ai.PlayState("ChargeRecover");
            yield return ai.WaitFor(0.35f);
            ai.PlayState("Idle");
        }

        // ------------------------------------------------------------------ phase 1: aimed charge
        private IEnumerator Charge()
        {
            SfxHooks.BossMove(this, "Charge"); // sound
            ai.Desired = Vector2.zero;
            ai.PlayState("ChargeBegin");
            float t = 0f, w = Windup(windupTime), speed = chargeSpeed * SpeedMul;
            Vector2 dir = ai.Player != null ? ((Vector2)ai.Player.position - (Vector2)transform.position).normalized : Vector2.right;
            // shared warning lane, as wide as the ram body; reaches the first obstacle (it charges until it hits the wall)
            var tg = DashTelegraph.Spawn(transform.position, dir, speed * maxRun, DashTelegraph.WidthFor(ai.BodyCollider, 0.75f), RamWarn, w, ai.Body != null ? ai.Body.sortingLayerID : 0, ai.Body != null ? ai.Body.sortingOrder - 1 : 1);
            while (t < w && !ai.Health.IsDead)
            {
                if (ai.Player != null && t < w * 0.75f)
                    dir = ((Vector2)ai.Player.position - (Vector2)transform.position).normalized;
                ai.FaceX(dir.x);
                tg.Aim(transform.position, dir, DashTelegraph.Reach(transform.position, dir, speed * maxRun, 0.3f, ai.BodyCollider, ai.Player));
                if (SluiceMechanism.Wet(transform.position)) break;           // water reached its feet during the wind-up
                t += Time.deltaTime;
                yield return null;
            }
            tg.Release();
            if (ai.Health.IsDead) yield break;
            if (SluiceMechanism.Wet(transform.position)) { yield return Quench(); yield break; }

            ai.PlayState("ChargeRun");
            IgnorePlayer(true);
            bool hitPlayer = false, wall = false, wet = false;
            float run = 0f, stall = 0f;
            Vector2 last = transform.position;
            while (run < maxRun && !ai.Health.IsDead)
            {
                ai.Desired = dir * speed;
                if (Time.time >= nextTrail) { nextTrail = Time.time + trailEvery; DropTrail(transform.position); }
                if (!hitPlayer && ai.GapToPlayer() <= 0.08f) { ai.Hurt(ai.damage, transform.position, 4f); hitPlayer = true; }
                run += Time.deltaTime;
                yield return null;
                if (SluiceMechanism.Wet(transform.position)) { wet = true; break; }
                if (Stalled(ref last, ref stall, run, speed)) { wall = true; break; }
            }
            ai.Desired = Vector2.zero;
            IgnorePlayer(false);
            if (wet) { yield return Quench(dir); yield break; }
            if (wall)
            {
                Vector2 at = (Vector2)transform.position + dir * 0.35f;
                Impact(at, dir, rubbleCount, true);
                if (!wallHinted) { wallHinted = true; Hints.Show("The wall barely slows it. Use the water.", 2.5f); }
                ai.PlayState("ChargeRecover");
                yield return ai.WaitFor(0.3f);
                ai.PlayState("Hit");
                yield return ai.Flash(wallStunTime, new Color(1f, 1f, 0.55f));
            }
            else
            {
                ai.PlayState("ChargeRecover");
                yield return ai.WaitFor(0.5f);
            }
            ai.PlayState("Idle");
        }

        // ------------------------------------------------------------------ phase 2: nonstop charges, bouncing off walls like a spring
        private IEnumerator Rampage()
        {
            SfxHooks.BossMove(this, "Rampage"); // sound
            if (!rampHinted)
            {
                rampHinted = true;
                Hints.Show("The Ram goes berserk and will not stop! Only the water can bring it down.", 4f);
            }
            // short opening wind-up, facing Ren
            ai.Desired = Vector2.zero;
            ai.PlayState("ChargeBegin");
            Vector2 dir = ai.Player != null ? ((Vector2)ai.Player.position - (Vector2)transform.position).normalized : Vector2.right;
            float rw = Windup(0.55f);
            var tg = DashTelegraph.Spawn(transform.position, dir, DashTelegraph.Reach(transform.position, dir, 3.5f, 0.3f, ai.BodyCollider, ai.Player),
                                         DashTelegraph.WidthFor(ai.BodyCollider, 0.75f), RamWarn, rw, ai.Body != null ? ai.Body.sortingLayerID : 0, ai.Body != null ? ai.Body.sortingOrder - 1 : 1);
            for (float t = 0f; t < rw && !ai.Health.IsDead; t += Time.deltaTime)
            {
                ai.FaceX(dir.x);
                yield return null;
            }
            tg.Release();
            if (ai.Health.IsDead) yield break;

            IgnorePlayer(true);
            ai.PlayState("ChargeRun");
            float breathAt = Time.time + rampageBreathEvery, run = 0f, stall = 0f;
            Vector2 last = transform.position;
            int bounces = 0;
            while (!ai.Health.IsDead)
            {
                float speed = rampageSpeed * SpeedMul;
                // probe ahead with a circle the size of the Ram body -> bounce exactly when touching the wall (no waiting to get stuck)
                // compute the probe distance ONCE and compare with a tolerance - computing it twice differed in the last float digits,
                // so "clear path" counted as a wall hit every frame and the Ram spun in place in the middle of the room.
                float look = speed * Time.deltaTime + 0.06f;
                bool hitAhead = FreeDistance(dir, look, out Vector2 n, out Vector2 at) < look - 0.001f;
                bool hitWall = hitAhead;
                if (!hitAhead)
                {
                    ai.Desired = dir * speed;
                    ai.FaceX(dir.x);
                    if (Time.time >= nextTrail) { nextTrail = Time.time + trailEvery; DropTrail(transform.position); }
                        if (Time.time >= nextBodyHit && ai.GapToPlayer() <= 0.08f) { ai.Hurt(ai.damage, transform.position, 3.5f); nextBodyHit = Time.time + 0.6f; }
                    run += Time.deltaTime;
                    yield return null;
                    if (SluiceMechanism.Wet(transform.position)) { ai.Desired = Vector2.zero; IgnorePlayer(false); yield return Quench(dir); yield break; }
                    // fallback: blocked by something the probe cannot see (dynamic objects...) -> treat as a wall, normal = against the charge
                    if (Stalled(ref last, ref stall, run, speed)) { hitWall = true; n = -dir; at = (Vector2)transform.position + dir * 0.3f; }
                }
                if (!hitWall) continue;

                // wall hit: rocks fall, short hitch, pick a new bounce direction with clear path
                bool rocks = Time.time - lastBounceAt >= 0.4f;               // rapid repeated hits (tight corner): no extra rocks
#if UNITY_EDITOR
                if (!rocks) Debug.Log("[Ram] rapid bounces: " + (!hitAhead ? "stuck (Stalled)" : lastWall != null ? lastWall.name + " (layer " + LayerMask.LayerToName(lastWall.gameObject.layer) + ")" : "arena box edge")
                                      + " at " + (Vector2)transform.position + " dir " + dir + " khung " + (hasArena ? arena.ToString() : "none")
                                      + " body r=" + CastRadius.ToString("0.00") + " centre " + CastOrigin);
#endif
                lastBounceAt = Time.time;
                if (rocks) Impact(at, dir, rampageRubble, (bounces++ % 2) == 0);
                else { var mm = MapMechanics.Instance; if (mm != null) mm.RamImpact(at, impactRadius); }
                ai.Desired = Vector2.zero;
                ai.PlayState("ChargeRecover");
                yield return ai.WaitFor(0.18f);
                if (ai.Health.IsDead) break;
                dir = ChooseBounce(dir, n);
                ai.FaceX(dir.x);
                run = 0f; stall = 0f; last = transform.position;

                if (Time.time >= breathAt)
                {
                    // panting: rest window (Ether hide still on unless soaked)
                    ai.PlayState("Hit");
                    yield return ai.Flash(rampageBreath, new Color(1f, 1f, 0.6f));
                    breathAt = Time.time + rampageBreathEvery;
                    if (ai.Health.IsDead) break;
                    if (ai.Player != null) dir = ChooseBounce(((Vector2)ai.Player.position - (Vector2)transform.position).normalized, Vector2.zero);
                    ai.PlayState("ChargeBegin");
                    yield return ai.WaitFor(Windup(0.35f));
                }
                ai.PlayState("ChargeRun");
            }
            ai.Desired = Vector2.zero;
            IgnorePlayer(false);
        }

        // ------------------------------------------------------------------ quenched by water
        private IEnumerator Quench(Vector2 slide = default)
        {
            stunnedByWater = true;
            ai.Desired = slide * 0.6f;                                        // slides a little further on the wet floor, then buckles
            lastQuench = Time.time;
            overchargedUntil = 0f;
            absorbed = 0;
            StartCoroutine(Steam());
            CameraFollow.KickMain(slide == default ? Vector2.down : slide, 0.1f, 0.3f);
            if (!quenchHinted) { quenchHinted = true; Hints.Show("Steam hisses off its hide - the Ram is down! Strike now!", 3f); }
            ai.PlayState("ChargeRecover");
            yield return ai.WaitFor(0.2f);
            ai.Desired = Vector2.zero;
            ai.PlayState("Hit");
            yield return ai.Flash(ai.InPhase2 ? quenchStunP2 : quenchStun, new Color(0.7f, 0.9f, 1f));
            stunnedByWater = false;
            soakedUntil = Time.time + soakedTime;
            if (!soakHinted) { soakHinted = true; Hints.Show("Soaked through - its hide can't shield it for a while!", 3f); }
            ai.PlayState("Idle");
        }

        /// <summary>White steam rising from the Ram body.</summary>
        private IEnumerator Steam()
        {
            SfxHooks.BossMove(this, "Steam"); // sound
            int layer = ai.Body != null ? ai.Body.sortingLayerID : 0, order = ai.Body != null ? ai.Body.sortingOrder + 2 : 3;
            var puffs = new List<(SpriteRenderer sr, Vector2 from, Vector2 vel, float born)>();
            float k0 = 0.22f / Mathf.Max(0.01f, EtherVfx.Orb.bounds.size.x);
            for (float t = 0f; t < 1.6f; t += Time.deltaTime)
            {
                if (t < 0.9f && puffs.Count < 18 && Random.value < 0.5f)
                {
                    Vector2 p = (Vector2)transform.position + new Vector2(Random.Range(-0.25f, 0.25f), Random.Range(0.05f, 0.3f));
                    var sr = EtherVfx.Spawn("RamSteam", EtherVfx.Orb, p, new Color(0.92f, 0.96f, 1f, 0.7f), layer, order);
                    puffs.Add((sr, p, new Vector2(Random.Range(-0.15f, 0.15f), Random.Range(0.35f, 0.6f)), Time.time));
                }
                foreach (var q in puffs)
                {
                    if (q.sr == null) continue;
                    float age = Time.time - q.born, u = Mathf.Clamp01(age / 0.8f);
                    q.sr.transform.position = q.from + q.vel * age;
                    float k = k0 * (1f + u * 1.6f);
                    q.sr.transform.localScale = new Vector3(k, k, 1f);
                    q.sr.color = new Color(0.92f, 0.96f, 1f, 0.7f * (1f - u));
                }
                yield return null;
            }
            foreach (var q in puffs) if (q.sr != null) Destroy(q.sr.gameObject);
        }

        private static IEnumerator Fall(SpriteRenderer sr, Vector2 from)
        {
            for (float t = 0f; t < 0.4f && sr != null; t += Time.deltaTime)
            {
                sr.transform.position = from + Vector2.down * (0.9f * t);
                sr.color = new Color(0.6f, 0.85f, 1f, 0.85f * (1f - t / 0.4f));
                yield return null;
            }
            if (sr != null) Destroy(sr.gameObject);
        }
    }
}
