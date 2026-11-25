using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// FIRE PATCH left on the floor (Reliquary duo): the brass mortar leaves orange fire, the Hollow Champion leaves violet soul fire.
    /// Loops the `vfx` animation for `seconds`; Ren inside the radius -> loses `damage` every `tick` seconds + a light Burn. Rolling does not avoid it
    /// (it is burning ground, not an attack) but stepping out does. Fades over the last two seconds (shows it is ending).
    /// </summary>
    public sealed class BurnPatch : MonoBehaviour
    {
        public float radius = 0.34f, damage = 4f, tick = 0.45f, burnDps = 2f, burnSeconds = 1.5f;
        public Color burnTint = new Color(1f, 0.55f, 0.2f);
        private SpriteVfx fx;
        private float until, nextTick;
        private Health player;

        public static BurnPatch Spawn(Vector2 at, string vfx, float scale, float seconds, int layer, int order, float radius, float damage, Color tint, Color? vfxTint = null)
        {
            var go = new GameObject("BurnPatch");
            go.transform.position = at;
            var p = go.AddComponent<BurnPatch>();
            p.radius = radius; p.damage = damage; p.burnTint = tint;
            p.until = Time.time + seconds;
            p.nextTick = Time.time + 0.25f;
            p.fx = SpriteVfx.Spawn(vfx, at, scale, layer, order, Random.value < 0.5f, 12f);
            if (p.fx != null) { p.fx.Looping(seconds); if (vfxTint.HasValue) p.fx.Tint(vfxTint.Value); }
            var pc = Object.FindFirstObjectByType<PlayerController>();
            if (pc != null) p.player = pc.GetComponent<Health>();
            return p;
        }

        private void Update()
        {
            if (Time.time >= until) { Destroy(gameObject); return; }
            if (fx != null)
            {
                var sr = fx.GetComponent<SpriteRenderer>();
                if (sr != null) { var c = sr.color; c.a = Mathf.Clamp01((until - Time.time) / 2f); sr.color = c; }
            }
            if (Time.time < nextTick || player == null || player.IsDead) return;
            nextTick = Time.time + tick;
            if (((Vector2)player.transform.position - (Vector2)transform.position).sqrMagnitude > radius * radius) return;
            Health.TrapIgnoresRoll = true;                              // burning ground: rolling gives no immunity
            bool hurt = player.TakeDamage(damage);
            Health.TrapIgnoresRoll = false;
            if (hurt && burnDps > 0f) player.ApplyBurn(burnDps, burnSeconds, burnTint);
        }

        private void OnDestroy()
        {
            if (fx != null) fx.Stop();
        }
    }
}
