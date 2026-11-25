using System.Collections;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Grave Worm - a TUNNELLING AMBUSH worm. The Crypt sprite set has no crawl animation, so it never walks on the surface.
    ///   - Ren within bite range: burrow quickly and pop up to bite on the spot (Attack = appear-atk).
    ///   - Ren out of range: burrow (Retreat, frames 0-11) -> travel underground, a dust trail puffs along the path (visible; no dust
    ///     under pits) -> arrive UNDER Ren's feet (nearest standable spot) -> the ground shakes for `emergeWarn` seconds (Attack frames 0-15,
    ///     cannot hit yet) -> burst up and bite. Rolling / stepping off the shaking ground dodges it.
    ///   - After the bite it stays above ground for `cooldown` seconds (punish window); burrows again after `burrowCooldown` seconds.
    ///   - Underground: collider + body + HP bar off (cannot be hit). Only surfaces within ~90% of `leashRange` around its cave.
    /// Counter: watch for the dust trail + shaking ground and roll away; hit it right after a bite; or stay away from the cave.
    /// </summary>
    public class WormSkills : MonoBehaviour, IEnemySkills
    {
        public string burrowState = "Retreat";
        [Tooltip("Burrow time (Retreat at 20 fps: frames 0-11)")] public float burrowTime = 0.6f;
        [Tooltip("Underground travel speed (units/second)")] public float tunnelSpeed = 1.5f;
        public float minTunnel = 0.45f, maxTunnel = 2.2f;
        [Tooltip("Ground shake before surfacing (Attack at 22 fps: frames 0-15)")] public float emergeWarn = 0.73f;
        public float burrowCooldown = 2.5f;
        [Tooltip("Do not tunnel after Ren when he is farther than this (units)")] public float maxChase = 4.5f;
        [Tooltip("Surfacing bite: hits when the gap <= attackRange + this value (misses if Ren has rolled / stepped away)")] public float emergeReach = 0.05f;
        public string trailVfx = "m3_dash_dust";
        public float trailEvery = 0.22f, trailScale = 0.6f;

        private EnemyAI self;
        private EnemyHealthBar bar;
        private Vector2 home;
        private bool hasHome;
        private float nextBurrow;

        private void Awake()
        {
            self = GetComponent<EnemyAI>();
            var h = GetComponent<Health>();
            if (h != null) h.Died += _ => { if (self != null && self.Body != null) self.Body.enabled = true; };   // dying underground (Burn) still leaves a visible corpse
        }

        public IEnumerator Act(EnemyAI ai)
        {
            if (ai.Player == null) yield break;
            if (!hasHome) { home = transform.position; hasHome = true; nextBurrow = Time.time + 0.6f; }
            if (ai.GapToPlayer() <= ai.attackRange) { yield return Bite(ai, false); yield break; }

            Vector2 p = ai.Player.position;
            // summoned (leftover / called enemy) -> tunnel after Ren however far (at most maxTunnel seconds per tick, continues next tick)
            bool reachable = ai.Summoned || ((p - home).magnitude <= Leash(ai) + 0.6f && (p - (Vector2)transform.position).magnitude <= maxChase);
            if (Time.time >= nextBurrow && reachable) { yield return Tunnel(ai); yield break; }

            ai.Desired = Vector2.zero;            // no crawling: wait where it surfaced
            ai.PlayState("Idle", false);
            yield return ai.WaitFor(0.15f);
        }

        private float Leash(EnemyAI ai) => ai.leashRange > 0f ? ai.leashRange * 0.9f : 99f;

        // ------------------------------------------------------------------ bite (Attack: ground shakes -> surface -> bite)
        private IEnumerator Bite(EnemyAI ai, bool emerging)
        {
            SfxHooks.BossMove(this, "Bite"); // sound
            ai.Desired = Vector2.zero;
            ai.FaceX(ai.DirToPlayer.x);
            ai.PlayState("Attack");
            if (ai.Vfx != null) ai.Vfx.Windup(ai.DirToPlayer);
            if (emerging)
            {
                if (ai.Body != null) ai.Body.enabled = true;           // ground shake visible (first frames of Attack)
                yield return ai.WaitFor(emergeWarn);
                if (ai.Health.IsDead) yield break;
                SetUnder(ai, false);                                   // surfaced: can be hit
                yield return ai.WaitFor(Mathf.Max(0f, ai.windup - emergeWarn));
            }
            else yield return ai.WaitFor(ai.windup);
            if (ai.Health.IsDead) yield break;
            if (ai.Vfx != null) ai.Vfx.Swing(ai.DirToPlayer);
            float reach = emerging ? ai.attackRange + emergeReach : ai.attackRange + 0.2f;
            if (ai.GapToPlayer() <= reach) ai.HitPlayerNow(ai.damage);
            yield return ai.WaitFor(Mathf.Max(0f, ai.attackTime - ai.windup));
            ai.PlayState("Idle");
            yield return ai.WaitFor(ai.cooldown);
        }

        // ------------------------------------------------------------------ tunnelling
        private IEnumerator Tunnel(EnemyAI ai)
        {
            SfxHooks.BossMove(this, "Tunnel"); // sound
            nextBurrow = Time.time + burrowCooldown;
            ai.Desired = Vector2.zero;
            if (!ai.HasAnimState(burrowState)) { yield return Bite(ai, false); yield break; }   // old prefab not rebuilt yet
            ai.PlayState(burrowState);
            yield return ai.WaitFor(burrowTime);
            if (ai.Health.IsDead || ai.Player == null) yield break;
            SetUnder(ai, true);
            Puff(ai, transform.position);

            Vector2 from = transform.position;
            Vector2 dest = Destination(ai);
            float time = Mathf.Clamp(Vector2.Distance(from, dest) / tunnelSpeed, minTunnel, maxTunnel);
            float t = 0f, nextPuff = trailEvery;
            while (t < time && !ai.Health.IsDead)
            {
                if (t < time * 0.6f && ai.Player != null) dest = Destination(ai);   // follow Ren for 60% of the way, then lock the surfacing spot
                Vector2 at = Vector2.Lerp(from, dest, t / time);
                ai.TeleportTo(at);
                if (t >= nextPuff) { nextPuff = t + trailEvery; if (Free(at)) Puff(ai, at); }
                t += Time.deltaTime;
                yield return null;
            }
            if (ai.Health.IsDead) yield break;
            ai.TeleportTo(dest);
            yield return Bite(ai, true);
        }

        /// Surfacing spot: under Ren's feet (kept inside the cave area), avoiding obstacles / water (pits) - tries a ring around if Ren's feet are blocked.
        private Vector2 Destination(EnemyAI ai)
        {
            Vector2 p = ai.Player.position;
            Vector2 fromHome = p - home;
            float leash = Leash(ai);
            if (fromHome.magnitude > leash) p = home + fromHome.normalized * leash;
            if (Free(p)) return p;
            for (int ring = 1; ring <= 4; ring++)
                for (int k = 0; k < 8; k++)
                {
                    float a = (k * 45f + ring * 22.5f) * Mathf.Deg2Rad;
                    Vector2 c = p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.16f * ring;
                    if (Free(c)) return c;
                }
            return transform.position;
        }

        private bool Free(Vector2 p)
        {
            foreach (var c in Physics2D.OverlapCircleAll(p, 0.14f))
                if (c != null && (self == null || c != self.BodyCollider) && LineOfSight.IsSolid(c, true)) return false;
            return true;
        }

        private void Puff(EnemyAI ai, Vector2 at)
        {
            if (!string.IsNullOrEmpty(trailVfx)) SpriteVfx.Spawn(trailVfx, at, trailScale, ai.SortingLayer, ai.SortingOrder - 1, false, 14f);
        }

        private void SetUnder(EnemyAI ai, bool under)
        {
            if (ai.BodyCollider != null) ai.BodyCollider.enabled = !under;
            if (ai.Body != null) ai.Body.enabled = !under;
            if (bar == null) bar = GetComponent<EnemyHealthBar>();
            if (bar != null) bar.enabled = !under;
        }

        private void OnDisable()
        {
            if (self != null && !self.Health.IsDead && self.BodyCollider != null) self.BodyCollider.enabled = true;
        }
    }
}
