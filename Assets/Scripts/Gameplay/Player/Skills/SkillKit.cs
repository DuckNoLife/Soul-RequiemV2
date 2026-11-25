using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;
using UnityEngine.InputSystem;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Shared helpers for the equippable skills (Map 2 + the Vessel set).
    ///   AimDir / AimPoint : aim direction / point (gamepad right stick -> mouse -> facing direction).
    ///   WallDistance      : how far along a direction before hitting a wall / rock (ignores enemies, the player, trigger zones).
    ///   EnemiesIn         : living enemies in a circle (each once, not through walls).
    ///   Control           : crowd control — normal enemies are stunned / rooted; bosses (CC-immune) become "exposed" (take extra damage) instead.
    ///   Vulnerable        : exposed + a down arrow mark over the enemy (sk_vuln, tinted by source: Ichor green / Spent violet / Staggered gold).
    /// Balance rule (ARCHITECTURE 2.11): normal slashes ~48 DPS are the main damage source; crowd control on bosses always turns into
    /// +20–30% exposure for ≤ 4 s, no stacking (keeps the highest, extends the time).
    /// </summary>
    public static class SkillKit
    {
        public static readonly Color CorrodedTint = new Color(0.72f, 0.95f, 0.3f);
        public static readonly Color SpentTint = new Color(0.78f, 0.5f, 1f);
        public static readonly Color StaggeredTint = new Color(1f, 0.85f, 0.45f);

        // BOUND mark (roots around the feet, PixelLab sk_bound) — set by Grasping Roots, Hand of the Vessel hits Bound targets harder.
        // Not crowd control, so bosses get it too (the roots cannot hold a boss but can mark it).
        private static readonly Dictionary<Health, float> boundUntil = new Dictionary<Health, float>();
        private static readonly Dictionary<Health, SpriteVfx> boundFx = new Dictionary<Health, SpriteVfx>();

        public static void Bind(EnemyAI ai, float seconds, int layer, int order)
        {
            if (ai == null || ai.Health == null || ai.Health.IsDead || seconds <= 0f) return;
            var h = ai.Health;
            if (boundUntil.Count > 512) { boundUntil.Clear(); boundFx.Clear(); }
            float until = Time.time + seconds;
            boundUntil[h] = boundUntil.TryGetValue(h, out var u) ? Mathf.Max(u, until) : until;
            if (boundFx.TryGetValue(h, out var old) && old != null) { old.Extend(seconds); return; }
            float w = ai.BodyCollider != null ? ai.BodyCollider.bounds.size.x : 0.3f;
            float scale = Mathf.Clamp(w / 0.3f, 0.8f, 2.6f);              // root image 48 px wide = 0.48 u
            var v = SpriteVfx.SpawnAttached("sk_bound", ai.transform, scale, layer, order + 1, 22f / Mathf.Max(0.5f, seconds), seconds);
            if (v != null) boundFx[h] = v;
        }

        public static bool IsBound(EnemyAI ai) =>
            ai != null && ai.Health != null && boundUntil.TryGetValue(ai.Health, out var u) && Time.time < u;

        /// pull an enemy toward `to`, stopping `stopAt` u away (extra velocity v = sqrt(2·drag·distance)). Bosses (CC-immune) are not pulled.
        public static void Pull(EnemyAI ai, Vector2 to, float stopAt)
        {
            if (ai == null || ai.Health == null || ai.Health.IsDead || ai.Health.ImmuneToCrowdControl) return;
            Vector2 d = to - (Vector2)ai.transform.position;
            float dist = d.magnitude - stopAt;
            if (dist <= 0.02f) return;
            float v = Mathf.Sqrt(2f * Mathf.Max(1f, ai.Health.KnockbackDecay) * dist);
            ai.Health.AddImpulse(d.normalized * v);
        }

        private static readonly RaycastHit2D[] castBuf = new RaycastHit2D[32];
        private static readonly List<Collider2D> overlap = new List<Collider2D>();
        private static readonly Dictionary<Health, SpriteVfx> markers = new Dictionary<Health, SpriteVfx>();

        /// Aim direction from Ren's feet: gamepad right stick (if pushed) -> mouse cursor -> <paramref name="fallback"/>.
        public static Vector2 AimDir(PlayerCombat p, Vector2 fallback)
        {
            var pad = Gamepad.current;
            if (pad != null)
            {
                Vector2 s = pad.rightStick.ReadValue();
                if (s.sqrMagnitude > 0.09f) return s.normalized;
            }
            if (MouseWorld(out var w))
            {
                Vector2 d = w - ((Vector2)p.transform.position + new Vector2(0f, 0.2f));
                if (d.sqrMagnitude > 0.0025f) return d.normalized;
            }
            return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector2.down;
        }

        /// Aim point within <paramref name="maxRange"/>: mouse cursor (clamped to range) / right stick / ahead at 70% range.
        public static Vector2 AimPoint(PlayerCombat p, float maxRange)
        {
            Vector2 feet = p.transform.position;
            var pad = Gamepad.current;
            if (pad != null)
            {
                Vector2 s = pad.rightStick.ReadValue();
                if (s.sqrMagnitude > 0.09f) return feet + s.normalized * maxRange * Mathf.Clamp01(s.magnitude);
            }
            if (MouseWorld(out var w))
            {
                Vector2 d = w - feet;
                return feet + Vector2.ClampMagnitude(d, maxRange);
            }
            Vector2 f = p.Facing.sqrMagnitude > 0.01f ? p.Facing.normalized : Vector2.down;
            return feet + f * maxRange * 0.7f;
        }

        private static bool MouseWorld(out Vector2 w)
        {
            w = default;
            var mouse = Mouse.current;
            var cam = Camera.main;
            if (mouse == null || cam == null) return false;
            Vector2 sp = mouse.position.ReadValue();
            w = cam.ScreenToWorldPoint(new Vector3(sp.x, sp.y, Mathf.Abs(cam.transform.position.z)));
            return true;
        }

        /// Distance travelled (body centre, radius <paramref name="radius"/>) before touching a solid obstacle.
        public static float WallDistance(Vector2 from, Vector2 dir, float maxLen, float radius)
        {
            var filter = new ContactFilter2D { useTriggers = false, useLayerMask = false };
            int n = Physics2D.CircleCast(from, Mathf.Max(0.01f, radius), dir, filter, castBuf, maxLen);
            float len = maxLen;
            for (int i = 0; i < n; i++)
            {
                var c = castBuf[i].collider;
                if (c == null || c.GetComponentInParent<EnemyAI>() != null || c.GetComponentInParent<PlayerCombat>() != null) continue;
                if (!LineOfSight.IsSolid(c, false) || c.OverlapPoint(from)) continue;
                len = Mathf.Min(len, castBuf[i].distance);
            }
            return Mathf.Max(0f, len);
        }

        /// Push a landing point out of obstacles (stepping back toward <paramref name="back"/>).
        public static Vector2 FreeSpot(Vector2 at, Vector2 back)
        {
            for (int i = 0; i < 12; i++)
            {
                bool solid = false;
                foreach (var c in Physics2D.OverlapPointAll(at))
                    if (c != null && !c.isTrigger && c.GetComponentInParent<EnemyAI>() == null && c.GetComponentInParent<PlayerCombat>() == null && LineOfSight.IsSolid(c, false)) { solid = true; break; }
                if (!solid) return at;
                at = Vector2.MoveTowards(at, back, 0.1f);
            }
            return at;
        }

        /// Living enemies in a circle; <paramref name="losFrom"/> != null drops those hidden behind walls.
        public static List<EnemyAI> EnemiesIn(Vector2 c, float r, Vector2? losFrom = null)
        {
            var list = new List<EnemyAI>();
            overlap.Clear();
            Physics2D.OverlapCircle(c, r, new ContactFilter2D { useTriggers = true, useLayerMask = false }, overlap);
            foreach (var col in overlap)
            {
                var ai = col.GetComponentInParent<EnemyAI>();
                if (ai == null || ai.Health == null || ai.Health.IsDead || list.Contains(ai)) continue;
                if (losFrom.HasValue && !LineOfSight.CanSee(losFrom.Value, col)) continue;
                list.Add(ai);
            }
            return list;
        }

        /// Enemies on the segment from -> from + dir * len (width width), sorted by distance.
        public static List<KeyValuePair<EnemyAI, float>> EnemiesAlong(Vector2 from, Vector2 dir, float len, float width)
        {
            var list = new List<KeyValuePair<EnemyAI, float>>();
            var filter = new ContactFilter2D { useTriggers = true, useLayerMask = false };
            int n = Physics2D.CircleCast(from, width * 0.5f, dir, filter, castBuf, Mathf.Max(0.01f, len));
            for (int i = 0; i < n; i++)
            {
                var ai = castBuf[i].collider.GetComponentInParent<EnemyAI>();
                if (ai == null || ai.Health == null || ai.Health.IsDead) continue;
                bool dup = false;
                foreach (var kv in list) if (kv.Key == ai) { dup = true; break; }
                if (!dup) list.Add(new KeyValuePair<EnemyAI, float>(ai, castBuf[i].distance));
            }
            list.Sort((a, b) => a.Value.CompareTo(b.Value));
            return list;
        }

        /// Crowd control: normal enemies stunned <paramref name="stun"/> s; bosses (CC-immune) exposed by <paramref name="bossBonus"/> for <paramref name="bossSeconds"/> s.
        public static void Control(EnemyAI ai, float stun, float bossBonus, float bossSeconds, int layer, int order)
        {
            if (ai == null || ai.Health == null || ai.Health.IsDead) return;
            if (ai.Health.ImmuneToCrowdControl) { if (bossBonus > 0f) Vulnerable(ai, bossBonus, bossSeconds, StaggeredTint, layer, order); }
            else if (stun > 0f) ai.Health.Stun(stun);
        }

        /// Exposed: takes <paramref name="bonus"/> (0.2 = +20%) extra damage for <paramref name="seconds"/> s + a mark over the head.
        public static void Vulnerable(EnemyAI ai, float bonus, float seconds, Color tint, int layer, int order)
        {
            if (ai == null || ai.Health == null || ai.Health.IsDead || bonus <= 0f || seconds <= 0f) return;
            ai.Health.AddVulnerability(bonus, seconds);
            if (markers.TryGetValue(ai.Health, out var old) && old != null) { old.Extend(seconds).Tint(tint); return; }   // the Ichor pool calls this every tick: extend, do not restart
            var body = ai.GetComponentInChildren<SpriteRenderer>();
            Vector2 head = body != null ? new Vector2(body.bounds.center.x, body.bounds.max.y + 0.04f) : (Vector2)ai.transform.position + new Vector2(0f, 0.6f);
            var v = SpriteVfx.SpawnAttached("sk_vuln", ai.transform, 0.6f, layer, order + 6, 12f, seconds);
            if (v != null) { v.transform.position = new Vector3(head.x, head.y, 0f); v.Tint(tint); markers[ai.Health] = v; }
        }

        /// "hit" blood spray on the enemy body (like a slash).
        public static void HitFx(PlayerCombat p, EnemyAI ai, Vector2 dir)
        {
            if (p == null || ai == null || !p.showVfx) return;
            var body = ai.GetComponentInChildren<SpriteRenderer>();
            Vector2 at = body != null ? (Vector2)body.bounds.center : (Vector2)ai.transform.position + new Vector2(0f, 0.2f);
            SpriteVfx.Spawn("hit", at, p.vfxScale, p.VfxLayer, p.PlayerOrder + 2, dir.x < 0f);
        }

        /// Tint Ren's body while channelling (restores the original colour when done).
        public static System.Collections.IEnumerator TintWhile(SpriteRenderer sr, Color c, System.Func<bool> keep)
        {
            if (sr == null) yield break;
            Color c0 = sr.color;
            while (sr != null && keep())
            {
                float k = 0.55f + 0.45f * Mathf.Sin(Time.time * 18f);
                sr.color = Color.Lerp(new Color(1f, 1f, 1f, c0.a), new Color(c.r, c.g, c.b, c0.a), k);
                yield return null;
            }
            if (sr != null) sr.color = new Color(1f, 1f, 1f, sr.color.a);
        }

        /// Play a frame strip (Sprite[]) in place, then self-destroy (roots / hand of the Vessel set).
        public static System.Collections.IEnumerator PlayFrames(Sprite[] frames, Vector2 at, float scale, float fps, Color tint, int layer, int order, bool flip)
        {
            if (frames == null || frames.Length == 0) yield break;
            var go = new GameObject("Mantra FX");
            go.transform.position = new Vector3(at.x, at.y, 0f);
            go.transform.localScale = new Vector3(scale, scale, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            FxLighting.MakeUnlit(sr);
            sr.sortingLayerID = layer; sr.sortingOrder = order; sr.color = tint; sr.flipX = flip;
            for (float t = 0f; ; t += Time.deltaTime)
            {
                int i = (int)(t * fps);
                if (i >= frames.Length || sr == null) break;
                sr.sprite = frames[i];
                yield return null;
            }
            if (go != null) Object.Destroy(go);
        }

        private static readonly Dictionary<string, Sprite[]> frameCache = new Dictionary<string, Sprite[]>();

        /// Load frames Resources/&lt;prefix&gt;0..n-1 (e.g. "VFX/specimen/hand_").
        public static Sprite[] Frames(string prefix, int n)
        {
            if (frameCache.TryGetValue(prefix, out var f) && f != null) return f;
            var list = new List<Sprite>();
            for (int i = 0; i < n; i++) { var s = Resources.Load<Sprite>(prefix + i); if (s == null) break; list.Add(s); }
            f = list.ToArray();
            frameCache[prefix] = f;
            return f;
        }
    }
}
