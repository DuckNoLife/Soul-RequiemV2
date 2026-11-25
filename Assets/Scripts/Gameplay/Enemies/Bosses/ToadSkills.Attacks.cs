using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// SKILLS of ToadSkills (one coroutine per skill). Settings / skill choice: ToadSkills.cs.
    /// </summary>
    public partial class ToadSkills
    {
        public IEnumerator Act(EnemyAI owner)
        {
            ai = owner;
            if (!engaged)
            {
                engaged = true;
                nextSlam = Time.time + 4f;
                nextTongue = Time.time + 1.2f;
                // no extra hint - visuals / animation / boss title already tell the player
            }
            if (ai.Player == null) yield break;
            float d = ((Vector2)ai.Player.position - Mouth).magnitude;
            brood.RemoveAll(e => e == null || e.Health == null || e.Health.IsDead);
            // the toadlet brood comes out ONLY ONCE, right after the phase 2 leap - no repeating waves
            if (ai.InPhase2 && !phaseLeapDone) { phaseLeapDone = true; yield return PhaseLeap(); yield return Brood(); }
            else if (tongueHit) { tongueHit = false; lastMove = "flop"; yield return BellyFlop(); }
            else
            {
                if (ai.InPhase2 && phaseLeapDone && !enraged && brood.Count == 0)
                {
                    enraged = true;                                       // brood all dead
                    // no extra hint - visuals / animation / boss title already tell the player
                    yield return ai.Flash(0.5f, new Color(1f, 0.35f, 0.3f));
                }
                string pick = Pick(d);
                lastMove = pick;
                switch (pick)
                {
                    case "slam": yield return LeapSlam(); break;
                    case "tongue": yield return Tongue(); break;
                    case "back": yield return Hop(true); break;
                    default: yield return Hop(false); break;
                }
            }
            ai.PlayState("Idle");
            yield return ai.WaitFor(enraged ? 0.25f : ai.InPhase2 ? 0.35f : 0.6f);
        }

        // ------------------------------------------------------------------ hop
        private IEnumerator Hop(bool away)
        {
            int n = away ? Random.Range(1, 3) : Random.Range(2, 4);
            float ht = enraged ? hopTime * 0.75f : hopTime, hs = enraged ? hopSpeed * 1.25f : hopSpeed;
            for (int i = 0; i < n && !ai.Health.IsDead && ai.Player != null; i++)
            {
                Vector2 dir = ((Vector2)ai.Player.position - (Vector2)transform.position).normalized;
                if (away) dir = -dir;
                ai.FaceX(away ? -dir.x : dir.x);
                ai.PlayState("Move");
                float t = 0f;
                while (t < ht && !ai.Health.IsDead) { ai.Desired = dir * hs; t += Time.deltaTime; yield return null; }
                ai.Desired = Vector2.zero;
                Ripple(transform.position, 1.2f);   // a normal hop only ripples, NO shock wave (only Leap Slam has one)
                yield return ai.WaitFor(0.25f);
            }
        }

        // ------------------------------------------------------------------ tongue
        // Like a real frog: throat swells while aiming (faint line + target dot under the player), the tongue shoots out very fast, the first part
        // still curving before it straightens; the round tongue tip sticks on hit, lashes + pulls the player toward the mouth, then retracts.
        private IEnumerator Tongue()
        {
            SfxHooks.BossMove(this, "Tongue"); // sound
            nextTongue = Time.time + (ai.InPhase2 ? tongueEvery * 0.7f : tongueEvery);
            ai.Desired = Vector2.zero;
            ai.PlayState("Burp");                                    // open the mouth, swell the throat
            int order = ai.Body != null ? ai.Body.sortingOrder + 1 : 3;

            // aim: faint thin line from the mouth to the target + target dot; direction locks 0.15 s before launch
            var aim = MakeStrip("ToadTongueAim", order - 2);
            liveWarn = aim.gameObject;
            var mark = MakeTip("ToadTongueMark", order - 2);
            FxLighting.MakeUnlit(mark);
            Vector2 dir = Vector2.down;
            float reach = tongueRange;
            float warn = ai.InPhase2 ? 0.45f : 0.6f, t = 0f;
            while (t < warn && !ai.Health.IsDead && ai.Player != null)
            {
                if (t < warn - 0.15f)
                {
                    Vector2 to = (Vector2)ai.Player.position + Vector2.up * 0.2f - Mouth;
                    dir = to.normalized;
                    reach = Mathf.Clamp(to.magnitude + 0.35f, 0.8f, tongueRange);
                }
                ai.FaceX(dir.x);
                float pulse = 0.18f + 0.12f * Mathf.Sin(Time.time * 18f);
                Place(aim, Mouth, dir, reach, 0.035f);
                aim.color = new Color(tongueColor.r, tongueColor.g, tongueColor.b, pulse);
                mark.transform.position = Mouth + dir * reach;
                mark.transform.localScale = Vector3.one * (0.9f + 0.2f * Mathf.Sin(Time.time * 18f));
                mark.color = new Color(tongueColor.r, tongueColor.g, tongueColor.b, pulse + 0.15f);
                t += Time.deltaTime;
                yield return null;
            }
            Destroy(aim.gameObject);
            Destroy(mark.gameObject);
            liveWarn = null;
            if (ai.Health.IsDead) yield break;

            // launch
            var lr = MakeTongueLine(order);
            var tip = MakeTip("ToadTongueTip", order + 1);
            liveTongue = lr.gameObject;
            tip.transform.SetParent(lr.transform, true);
            tip.color = Color.white;
            float side = Random.value < 0.5f ? -1f : 1f;              // tongue curves to one side
            bool hit = false;
            const float outT = 0.13f, holdT = 0.08f, backT = 0.2f;
            for (t = 0f; t < outT + holdT + backT && !ai.Health.IsDead; t += Time.deltaTime)
            {
                float k, wave;
                if (t < outT) { float u = t / outT; k = 1f - (1f - u) * (1f - u) * (1f - u); wave = 1f - u; }       // out: fast then slow
                else if (t < outT + holdT) { k = 1f; wave = 0f; }
                else { float u = (t - outT - holdT) / backT; k = 1f - u * u; wave = 0.35f * Mathf.Sin(u * Mathf.PI); } // back: slow then fast
                float len = Mathf.Max(0.06f, reach * k);
                Vector2 a = Mouth, b = Mouth + dir * len;
                if (hit && ai.Player != null)   // tongue stuck on Ren -> the tip follows Ren while pulling back
                {
                    Vector2 grab = (Vector2)ai.Player.position + Vector2.up * 0.2f - a;
                    len = Mathf.Max(0.06f, grab.magnitude);
                    if (len > 0.01f) { dir = grab / len; ai.FaceX(dir.x); }
                    b = a + dir * len;
                }
                DrawTongue(lr, a, dir, len, side * wave * 0.22f);
                tip.transform.position = b;
                if (!hit && ai.Player != null && t < outT + holdT &&
                    SegmentDistance((Vector2)ai.Player.position + Vector2.up * 0.2f, a, b) <= tongueWidth + 0.1f)
                {
                    // the tongue deals NO damage - on hit it YANKS Ren up to the toad's mouth (can be rolled through)
                    var ph = ai.PlayerHealth;
                    if (ph != null && !ph.Invulnerable && !ph.IsDead)
                    {
                        hit = true;
                        tongueHit = true;                                // next tick = Belly Flop
                        Vector2 toMouth = Mouth + dir * 0.45f - ((Vector2)ai.Player.position + Vector2.up * 0.2f);
                        float dist = toMouth.magnitude;
                        // extra velocity decays at a steady 20 units/s² (Health.knockbackDecay) -> v = sqrt(2 * 20 * distance): stops right in front of the mouth
                        if (dist > 0.1f) ph.AddImpulse(toMouth / dist * Mathf.Sqrt(2f * 20f * dist));
                        CameraFollow.KickMain(-dir, 0.04f, 0.15f);
                    }
                }
                yield return null;
            }
            if (lr != null) Destroy(lr.gameObject);
            liveTongue = null;
            ai.PlayState("Idle");
            yield return ai.WaitFor(0.3f);
        }

        private IEnumerator SecondWave(Vector2 at)
        {
            SfxHooks.BossMove(this, "SecondWave"); // sound
            yield return new WaitForSeconds(0.35f);
            if (ai != null && !ai.Health.IsDead) Shockwave.Spawn(at, 3.4f, 0.8f, ai.damage * 0.7f, GroundOrder, waveColor);
        }

        // ------------------------------------------------------------------ leap slam
        private IEnumerator LeapSlam()
        {
            SfxHooks.BossMove(this, "LeapSlam"); // sound
            nextSlam = Time.time + (enraged ? slamEvery * 0.55f : ai.InPhase2 ? slamEvery * 0.7f : slamEvery);
            ai.Desired = Vector2.zero;
            ai.PlayState("JumpPrepare");
            yield return ai.Flash(0.6f, new Color(1f, 0.6f, 0.4f));
            if (ai.Health.IsDead || ai.Player == null) yield break;

            var sr = ai.Body;
            var col = ai.BodyCollider;
            ai.Airborne = true;
            if (col != null) col.enabled = false;
            Vector3 start = transform.position;
            float t = 0f;
            while (t < 0.35f)
            {
                t += Time.deltaTime;
                transform.position = start + Vector3.up * leapHeight * (t / 0.35f);
                if (sr != null) sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, 1f - t / 0.35f);
                yield return null;
            }
            var shadow = MakeShadow();
            t = 0f;
            Vector3 target = ai.Player != null ? ai.Player.position : start;
            while (t < trackTime + lockTime && !ai.Health.IsDead)
            {
                if (t < trackTime && ai.Player != null) target = Vector3.Lerp(target, ai.Player.position, Time.deltaTime * 6f);
                float k = t < trackTime ? t / trackTime : 1f;
                shadow.transform.position = new Vector3(target.x, target.y, shadow.transform.position.z);
                shadow.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 1f, k);
                var ssr = shadow.GetComponent<SpriteRenderer>();
                ssr.color = t < trackTime ? new Color(0f, 0f, 0f, 0.55f)
                                          : (Mathf.Repeat(Time.time * 10f, 1f) < 0.5f ? new Color(0.6f, 0f, 0f, 0.7f) : new Color(0f, 0f, 0f, 0.6f));
                t += Time.deltaTime;
                yield return null;
            }
            transform.position = new Vector3(target.x, target.y + leapHeight, start.z);
            t = 0f;
            while (t < 0.2f)
            {
                t += Time.deltaTime;
                transform.position = new Vector3(target.x, target.y + leapHeight * (1f - t / 0.2f), start.z);
                if (sr != null) sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, t / 0.2f);
                yield return null;
            }
            Destroy(shadow);
            liveShadow = null;
            transform.position = new Vector3(target.x, target.y, start.z);
            if (ai.Rb != null) ai.Rb.position = (Vector2)target;
            if (sr != null) sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, 1f);
            if (col != null) col.enabled = true;
            ai.Airborne = false;
            if (ai.Player != null && ((Vector2)ai.Player.position - (Vector2)target).magnitude <= slamRadius)
                ai.Hurt(ai.damage * 1.4f, target, 3.5f);
            Ripple(target, 2.4f);
            Shockwave.Spawn(target, 2.6f, 0.7f, ai.damage * 0.9f, GroundOrder, waveColor);
            if (ai.InPhase2) StartCoroutine(SecondWave(target));
            var mm = MapMechanics.Instance;
            if (mm != null && mm.Shatter(target, shatterRadius))
            {
                Hints.Show("The tank shatters - Gorgemaw is stunned!", 2.5f);
                ai.PlayState("JumpRecover");
                yield return ai.Flash(stunTime, new Color(1f, 1f, 0.55f));
            }
            else if (ai.InPhase2 && mm != null && mm.SpillPoints(target, sinkRadius).Count > 0)
            {
                // phase 2 has no tanks left to break - landing in an Ichor pool makes it sink and get stuck for a while (hint only shown the first time)
                if (!sinkHinted) { sinkHinted = true; Hints.Show("Gorgemaw sinks into the Ichor - strike now!", 2.5f); }
                Ripple(target, 1.8f);
                ai.PlayState("JumpRecover");
                yield return ai.Flash(sinkTime, new Color(0.7f, 1f, 0.5f));
            }
            else
            {
                // missed landing -> panting (clear counterattack window); recovers faster when enraged
                if (!windedHinted && ai.Player != null && ((Vector2)ai.Player.position - (Vector2)target).magnitude > slamRadius)
                { windedHinted = true; Hints.Show("Gorgemaw lands heavily - it's winded!", 2f); }
                ai.PlayState("JumpRecover");
                yield return ai.Flash(enraged ? 0.8f : 1.2f, new Color(0.75f, 0.75f, 0.8f));
            }
        }

        // ------------------------------------------------------------------ belly flop
        /// <summary>After the tongue pulls Ren close: a low hop crashing onto Ren (red ring 0.55 s, then locks). Hit: damage + knockback;
        /// rolling out of the ring dodges it. Short range - it only punishes a player who was just pulled in.</summary>
        private IEnumerator BellyFlop()
        {
            SfxHooks.BossMove(this, "BellyFlop"); // sound
            ai.Desired = Vector2.zero;
            if (ai.Player == null) yield break;
            Vector2 start = transform.position;
            Vector2 target = ai.Player.position;
            ai.FaceX(target.x - start.x);
            ai.PlayState("JumpPrepare");
            int order = ai.Body != null ? ai.Body.sortingOrder - 1 : 1;
            var g = GroundStrike.Spawn(target, 0.55f, slamRadius * 0.8f, ai.damage * 1.1f, null, 0, 14f, order, new Color(1f, 0.3f, 0.25f));
            g.impactSfx = "heavy_land"; // sound
            g.knockback = 3f;
            yield return ai.WaitFor(0.15f);
            if (ai.Health.IsDead) yield break;
            var sr = ai.Body;
            Transform vis = sr != null ? sr.transform : null;
            Vector3 v0 = vis != null && vis != transform ? vis.localPosition : Vector3.zero;
            var col = ai.BodyCollider;
            ai.Airborne = true;
            if (col != null) col.enabled = false;
            const float air = 0.4f;
            for (float t = 0f; t < air && !ai.Health.IsDead; t += Time.deltaTime)
            {
                float k = t / air;
                Vector2 p = Vector2.Lerp(start, target, k);
                transform.position = new Vector3(p.x, p.y, transform.position.z);
                if (ai.Rb != null) ai.Rb.position = p;
                if (vis != null && vis != transform) vis.localPosition = v0 + Vector3.up * (0.55f * Mathf.Sin(k * Mathf.PI));
                yield return null;
            }
            if (vis != null && vis != transform) vis.localPosition = v0;
            transform.position = new Vector3(target.x, target.y, transform.position.z);
            if (ai.Rb != null) ai.Rb.position = target;
            if (col != null) col.enabled = true;
            ai.Airborne = false;
            CameraFollow.KickMain(Vector2.down, 0.08f, 0.2f);
            SoulRequiem.Core.Sfx.Play("heavy_land", target); // sound
            Ripple(target, 1.8f);
            ai.PlayState("JumpRecover");
            yield return ai.WaitFor(0.7f);                                  // recovery after the flop: Ren can counterattack
        }

        /// <summary>Called exactly once (entering phase 2). Each Ichor pool releases one toadlet, pool by pool (at most broodCap);
        /// no pools (map without tanks) -> release 2 next to the toad.</summary>
        private IEnumerator Brood()
        {
            SfxHooks.BossMove(this, "Brood"); // sound
            ai.Desired = Vector2.zero;
            ai.PlayState("Burp");
            yield return ai.Flash(0.5f, new Color(0.8f, 1f, 0.6f));
            var dir = MapEncounterDirector.Instance;
            if (dir == null) yield break;
            // toadlets rise from the Ichor pools of the broken tanks (only released next to the toad when there are no pools)
            var mm = MapMechanics.Instance;
            var pools = mm != null ? mm.SpillPoints(transform.position, 12f) : new List<Vector2>();
            int room = Mathf.Max(0, broodCap - brood.Count);
            int n = Mathf.Min(room, pools.Count > 0 ? pools.Count : 2);
            for (int i = 0; i < pools.Count; i++)                     // shuffle the pool positions
            {
                int j = Random.Range(i, pools.Count);
                var tmp = pools[i]; pools[i] = pools[j]; pools[j] = tmp;
            }
            for (int i = 0; i < n; i++)
            {
                bool fromPool = pools.Count > 0;
                Vector2 at = fromPool ? pools[i % pools.Count] + Random.insideUnitCircle * 0.12f
                                      : (Vector2)transform.position + new Vector2(i % 2 == 0 ? -0.35f : 0.35f, -0.15f);
                var e = dir.SpawnSummon(broodEnemy, at, ai);
                if (e == null) continue;
                brood.Add(e);
                if (fromPool) StartCoroutine(Emerge(e, at, i * 0.45f));   // one pool at a time: the player gets to see them rise
            }
            // no extra hint - visuals / animation / boss title already tell the player
            yield return ai.WaitFor(0.5f);
        }

        /// <summary>A toadlet rises from an Ichor pool - ripple, fades in from below the surface, holds still for a beat before attacking.</summary>
        private IEnumerator Emerge(EnemyAI e, Vector2 at, float delay)
        {
            e.Held = true;
            var sr = e.Body;
            Transform vis = sr != null ? sr.transform : null;
            Vector3 p0 = vis != null ? vis.localPosition : Vector3.zero;
            Color c0 = sr != null ? sr.color : Color.white;
            if (sr != null) sr.color = new Color(c0.r, c0.g, c0.b, 0f);
            if (delay > 0f) yield return new WaitForSeconds(delay);
            Ripple(at, 1.1f);
            for (float t = 0f; t < 0.5f && e != null; t += Time.deltaTime)
            {
                float k = t / 0.5f;
                if (sr != null) sr.color = new Color(c0.r * Mathf.Lerp(0.4f, 1f, k), c0.g, c0.b * Mathf.Lerp(0.4f, 1f, k), k);
                if (vis != null && vis != e.transform) vis.localPosition = p0 + Vector3.down * (0.18f * (1f - k));
                yield return null;
            }
            if (e == null) yield break;
            if (sr != null) sr.color = c0;
            if (vis != null && vis != e.transform) vis.localPosition = p0;
            yield return new WaitForSeconds(0.3f);
            if (e != null) { e.Held = false; e.Provoke(); }
        }

        /// <summary>Entering phase 2 - leaps to the middle of the area (not following the player) and slams down: big shock wave, strong shake,
        /// breaks every glass tank in the area (Ichor spills out). Not stunned like a normal slam.</summary>
        private IEnumerator PhaseLeap()
        {
            SfxHooks.BossMove(this, "PhaseLeap"); // sound
            var dir = MapEncounterDirector.Instance;
            Vector2 center = transform.position;
            if (dir != null && dir.TryGetZoneAt(transform.position, out _, out var zr)) center = zr.center;
            nextSlam = Time.time + slamEvery;
            ai.Desired = Vector2.zero;
            // no extra hint - visuals / animation / boss title already tell the player
            ai.PlayState("JumpPrepare");
            yield return ai.Flash(0.8f, new Color(1f, 0.45f, 0.3f));
            if (ai.Health.IsDead) yield break;
            var sr = ai.Body;
            var col = ai.BodyCollider;
            ai.Airborne = true;
            if (col != null) col.enabled = false;
            Vector3 start = transform.position;
            float h = leapHeight * 1.4f;
            for (float t = 0f; t < 0.35f; t += Time.deltaTime)
            {
                transform.position = start + Vector3.up * h * (t / 0.35f);
                if (sr != null) sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, 1f - t / 0.35f);
                yield return null;
            }
            var shadow = MakeShadow();
            var ssr = shadow.GetComponent<SpriteRenderer>();
            for (float t = 0f; t < 0.9f && !ai.Health.IsDead; t += Time.deltaTime)
            {
                shadow.transform.position = new Vector3(center.x, center.y, shadow.transform.position.z);
                shadow.transform.localScale = Vector3.one * Mathf.Lerp(0.6f, 1.4f, t / 0.9f);
                ssr.color = Mathf.Repeat(Time.time * 10f, 1f) < 0.5f ? new Color(0.6f, 0f, 0f, 0.7f) : new Color(0f, 0f, 0f, 0.6f);
                yield return null;
            }
            for (float t = 0f; t < 0.18f; t += Time.deltaTime)
            {
                transform.position = new Vector3(center.x, center.y + h * (1f - t / 0.18f), start.z);
                if (sr != null) sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, t / 0.18f);
                yield return null;
            }
            Destroy(shadow);
            liveShadow = null;
            transform.position = new Vector3(center.x, center.y, start.z);
            if (ai.Rb != null) ai.Rb.position = center;
            if (sr != null) sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, 1f);
            if (col != null) col.enabled = true;
            ai.Airborne = false;
            if (ai.Player != null && ((Vector2)ai.Player.position - center).magnitude <= slamRadius * 1.3f)
                ai.Hurt(ai.damage * 1.4f, center, 4f);
            CameraFollow.KickMain(Vector2.down, 0.2f, 0.5f);
            SoulRequiem.Core.Sfx.Play("heavy_land", center); // sound
            SoulRequiem.Core.Sfx.Play("rock_fall", center);
            Ripple(center, 3.2f);
            Shockwave.Spawn(center, 3.6f, 0.8f, ai.damage * 0.9f, GroundOrder, waveColor);
            StartCoroutine(SecondWave(center));
            var mm = MapMechanics.Instance;
            if (mm != null) mm.Shatter(center, 7f);   // no extra hint - visuals / animation / boss title already tell the player
            ai.PlayState("JumpRecover");
            yield return ai.WaitFor(0.9f);
        }
    }
}
