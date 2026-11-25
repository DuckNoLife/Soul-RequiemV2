using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Rat poison cloud (a burst bubble): swells -> hangs for `seconds` -> dissolves.
    /// The player inside the cloud (ellipse, radius `radius`) gets poisoned (Health.ApplyPoison — no stacking).
    /// With the book "Vael Purifier's Rite" learned (flag book_purifier): the area skill (F) touching the cloud dissolves it at once.
    /// </summary>
    public class PoisonCloud : MonoBehaviour
    {
        public Sprite[] pop, loop, fade;
        public float seconds = 4f;
        public float radius = 0.42f;
        public float poison = 0.03f;
        public float poisonDuration = 3f;

        private SpriteRenderer sr;
        private float t0, fadeAt = -1f, nextCheck;
        private static Health playerHp;

        const float PopFps = 20f, LoopFps = 10f, FadeFps = 16f;

        public static PoisonCloud Spawn(Vector2 at, Sprite[] pop, Sprite[] loop, Sprite[] fade, float seconds, float radius,
                                        float poison, float poisonDuration, int order)
        {
            var go = new GameObject("PoisonCloud");
            SoulRequiem.Core.Sfx.Play("gas_hiss", at); // sound
            go.transform.position = at;
            var c = go.AddComponent<PoisonCloud>();
            c.pop = pop; c.loop = loop; c.fade = fade;
            c.seconds = seconds; c.radius = radius; c.poison = poison; c.poisonDuration = poisonDuration;
            c.sr = go.AddComponent<SpriteRenderer>();
            FxLighting.MakeUnlit(c.sr);
            c.sr.sortingOrder = order;
            return c;
        }

        private void OnEnable() => PlayerCombat.SkillCast += OnSkill;
        private void OnDisable() => PlayerCombat.SkillCast -= OnSkill;

        private void Start()
        {
            t0 = Time.time;
            if (playerHp == null)
            {
                var pc = FindFirstObjectByType<PlayerController>();
                if (pc != null) playerHp = pc.GetComponent<Health>();
            }
        }

        private float PopLen => pop != null ? pop.Length / PopFps : 0f;

        private void Update()
        {
            float t = Time.time - t0;
            if (fadeAt < 0f && t >= PopLen + seconds) fadeAt = Time.time;

            if (fadeAt >= 0f)
            {
                int f = Mathf.FloorToInt((Time.time - fadeAt) * FadeFps);
                if (fade == null || f >= fade.Length) { Destroy(gameObject); return; }
                sr.sprite = fade[f];
                return;
            }
            if (t < PopLen && pop != null && pop.Length > 0) sr.sprite = pop[Mathf.Min(pop.Length - 1, Mathf.FloorToInt(t * PopFps))];
            else if (loop != null && loop.Length > 0) sr.sprite = loop[Mathf.FloorToInt((t - PopLen) * LoopFps) % loop.Length];

            // poison: only once the cloud is more than half swollen
            if (Time.time < nextCheck || t < PopLen * 0.5f || playerHp == null || playerHp.IsDead) return;
            nextCheck = Time.time + 0.25f;
            Vector2 d = (Vector2)playerHp.transform.position - (Vector2)transform.position;
            if ((d.x * d.x) / (radius * radius) + (d.y * d.y) / (radius * radius * 0.45f) <= 1f)
                playerHp.ApplyPoison(poison, poisonDuration);
        }

        private void OnSkill(Vector2 center, float r)
        {
            if (fadeAt >= 0f) return;
            var dir = MapEncounterDirector.Instance;
            if (dir == null || !dir.Flags.Has("book_purifier")) return;
            if (((Vector2)transform.position - center).magnitude > r + radius) return;
            fadeAt = Time.time;
        }
    }
}
