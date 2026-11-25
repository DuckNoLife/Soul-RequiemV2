using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Blue Ether orb of the Mantra "Seeker Embers": spreads out a little, then homes in on its assigned enemy.
    /// Hit: damage + Burn. Touching an obstacle (wall, rock...) dissolves it. Time out or target lost -> flies straight then dissolves.
    /// </summary>
    public class HomingOrb : MonoBehaviour
    {
        public EnemyAI target;
        public float speed = 5f;
        public float turnRate = 720f;
        public float damage = 6f;
        public float burnDps = 5f;
        public float burnTime = 3f;
        public float life = 2.5f;
        [Tooltip("For the first seconds it only fans out, without homing.")]
        public float launch = 0.15f;

        private Vector2 dir = Vector2.down;
        private float born;
        private SpriteRenderer glow;
        private Collider2D targetCol;
        private SpriteRenderer targetBody;

        /// <summary>Create an orb at <paramref name="pos"/>, flying along <paramref name="direction"/>, homing on <paramref name="enemy"/> (null = straight).</summary>
        public static HomingOrb Spawn(Vector2 pos, Vector2 direction, EnemyAI enemy, int sortingLayer, int order)
        {
            var r = EtherVfx.Spawn("Ether Orb", EtherVfx.Orb, pos, EtherVfx.Blue, sortingLayer, order);
            SoulRequiem.Core.Sfx.Play("proj_magic", pos); // sound
            r.transform.localScale = Vector3.one * 0.6f;
            var core = EtherVfx.Spawn("Core", EtherVfx.Orb, pos, EtherVfx.Core, sortingLayer, order + 1, r.transform);
            core.transform.localScale = Vector3.one * 0.45f;
            var o = r.gameObject.AddComponent<HomingOrb>();
            o.glow = r;
            o.dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.down;
            o.SetTarget(enemy);
            return o;
        }

        private void SetTarget(EnemyAI enemy)
        {
            target = enemy;
            targetCol = enemy != null ? enemy.GetComponentInChildren<Collider2D>() : null;
            targetBody = enemy != null ? enemy.GetComponentInChildren<SpriteRenderer>() : null;
        }

        private void Start() => born = Time.time;

        private void Update()
        {
            float dt = Time.deltaTime;
            if (Time.time - born > life) { Destroy(gameObject); return; }
            if (target != null && (target.Health == null || target.Health.IsDead)) SetTarget(null);

            Vector2 pos = transform.position;
            if (target != null && Time.time - born > launch)
                dir = EtherVfx.RotateTowards(dir, TargetPoint() - pos, turnRate * dt);

            Vector2 next = pos + dir * speed * dt;
            if (LineOfSight.Blocked(pos, next)) { Destroy(gameObject); return; }     // hit a wall / rock -> dissolve
            transform.position = new Vector3(next.x, next.y, transform.position.z);

            if (target != null && Vector2.Distance(next, TargetPoint()) < 0.12f) { Hit(); return; }
            if (glow != null) glow.transform.localScale = Vector3.one * (0.6f + 0.07f * Mathf.Sin(Time.time * 30f));
        }

        // enemy body: the collider is only a foot strip -> lift to the middle of the sprite (like PlayerStrikes)
        private Vector2 TargetPoint()
        {
            Vector2 p = targetCol != null ? (Vector2)targetCol.bounds.center : (Vector2)target.transform.position;
            if (targetBody != null) p.y = Mathf.Lerp(p.y, targetBody.bounds.center.y, 0.6f);
            return p;
        }

        private void Hit()
        {
            var h = target.Health;
            if (h != null && !h.IsDead)
            {
                h.TakeDamage(damage, dir, 0.6f);
                h.ApplyBurn(burnDps, burnTime);
                if (glow != null) SpriteVfx.Spawn("hit", transform.position, 0.5f, glow.sortingLayerID, glow.sortingOrder + 2, dir.x < 0f);
            }
            Destroy(gameObject);
        }
    }
}
