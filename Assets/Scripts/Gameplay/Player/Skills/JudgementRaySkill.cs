using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// "Judgement Ray" — Mantras of the Eye set, slot III: one hand fires a blue Ether beam for <see cref="duration"/> seconds,
    /// the beam follows the mouse (gamepad: right stick), Ren stands still. The beam pierces enemies and is stopped by walls / rocks
    /// (except obstacles enclosing the enemy itself, e.g. the Plague Eye pit). Roll (Q) cancels early; the cooldown still counts.
    /// PRESSING the skill key (C) AGAIN also stops the beam at once (TryStop -> PlayerLoadout.TryCast);
    /// stopping early refunds <see cref="earlyRefund"/> × the unused time into the cooldown.
    /// The beam touches mechanisms like a slash (PlayerCombat.Struck): e.g. Ether lamps can be broken with the beam.
    /// </summary>
    [CreateAssetMenu(menuName = "Soul Requiem/Skill/Judgement Ray", fileName = "EyeRay")]
    public class JudgementRaySkill : SkillDefinition
    {
        // numbers for the Shrine panel.
        public override string Kind => "Channel - Beam";
        public override void Stats(System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>> o)
        {
            Stat(o, "Damage", dps.ToString("0") + " /s");
            Stat(o, "Duration", Secs(duration));
            Stat(o, "Range", Tiles(range));
            Stat(o, "Pierces", "every foe in line");
            Stat(o, "Stop early", "refund " + Pct(earlyRefund) + " of unused time");
        }

        public float duration = 5f;
        public float dps = 30f;
        [Tooltip("Seconds between two damage ticks.")]
        public float tick = 0.15f;
        [Tooltip("Beam range (Unity units; 1 tile = 0.32).")]
        public float range = 8f;
        [Tooltip("Width of the beam VISUAL.")]
        public float width = 0.22f;
        [Tooltip("Width of the beam HITBOX (checked at ground level, at the enemies' feet). Pierces every enemy on the way.")]
        public float hitWidth = 0.6f;
        [Tooltip("Beam turn speed following the mouse (degrees / second).")]
        public float turnRate = 220f;
        [Tooltip("Stopping early: refunds (unfired time / duration) × this factor into the cooldown.")]
        public float earlyRefund = 0.5f;
        [Tooltip("With the Veil active (Health.Guarded) Ren can glide while firing at this speed factor (0 = stand still).")]
        public float veiledMove = 0.4f;
        [NonSerialized] private bool firing, stopRequested;
        // the NORTH ray-hold sprite extends the LEFT hand (screen right), NORTH-WEST extends the RIGHT hand (screen left) -> sweeping the beam
        // from NW to N made the casting hand jump to the other side of the body and the beam cross over the head. Fix: facing North with the beam to the left, FLIP the sprite
        // (flipX around the feet — robe and sword are symmetric) so the hand reaches toward the beam; a dead zone around due North; the beam origin slides smoothly.
        [Tooltip("Dead zone (degrees) around due North before switching the extended hand.")]
        public float northSwapDeadzone = 6f;
        [Tooltip("Slide speed of the beam origin when the sprite changes (1/second, higher = faster).")]
        public float handBlend = 28f;
        [NonSerialized] private int northSide;                 // -1 hand on the left (flipped), +1 on the right (original sprite)
        [NonSerialized] private Vector2 handOffset;
        [NonSerialized] private bool handInit;
        [NonSerialized] private float firingEnd;
        [Tooltip("Hand position in each ray-hold sprite, in sprite pixels from the feet (x right, y up). " +
                 "Order: E, NE, N, NW, W, SW, S, SE. The ray starts here, so it leaves the outstretched hand.")]
        public Vector2[] handPixels =
        {
            // Ren holds the katana in his RIGHT hand -> the beam comes from the outstretched LEFT hand (GamePackage/Ren/ray, frames 2-7), px from the feet,
            // ray animation with the left palm pushed straight out — measured automatically from the hold frames
            new Vector2(38f, 65f), new Vector2(11f, 70f), new Vector2(-32f, 63f), new Vector2(-48f, 71f),
            new Vector2(-34f, 63f), new Vector2(-46f, 55f), new Vector2(37f, 62f), new Vector2(44f, 58f),
        };
        [Tooltip("Sprite pixels per Unity unit of the player art.")]
        public float spritePixelsPerUnit = 100f;

        private static readonly RaycastHit2D[] buf = new RaycastHit2D[48];
        private readonly List<KeyValuePair<EnemyAI, float>> enemies = new List<KeyValuePair<EnemyAI, float>>();
        private readonly List<KeyValuePair<Collider2D, float>> walls = new List<KeyValuePair<Collider2D, float>>();

        public override bool TryStop(PlayerCombat p)
        {
            if (!firing || Time.time >= firingEnd) return false;
            stopRequested = true;
            return true;
        }

        public override IEnumerator Cast(PlayerCombat p, Action cancel)
        {
            int id = p.BeginAction();
            Vector2 dir = Aim(p, p.Facing.sqrMagnitude > 0.01f ? p.Facing.normalized : Vector2.down);
            var glow = EtherVfx.Spawn("Judgement Ray", EtherVfx.Beam, p.transform.position, EtherVfx.Blue, p.VfxLayer, p.PlayerOrder + 3);
            var core = EtherVfx.Spawn("Core", EtherVfx.Beam, p.transform.position, EtherVfx.Core, p.VfxLayer, p.PlayerOrder + 4, glow.transform);
            core.transform.localScale = new Vector3(1f, 0.4f, 1f);
            var spark = EtherVfx.Spawn("Ray Spark", EtherVfx.Orb, p.transform.position, EtherVfx.Core, p.VfxLayer, p.PlayerOrder + 4);
            var handGlow = EtherVfx.Spawn("Ray Hand", EtherVfx.Orb, p.transform.position, EtherVfx.Blue, p.VfxLayer, p.PlayerOrder + 4);
            float end = Time.time + duration, nextTick = 0f;
            var done = new HashSet<Health>();
            p.RayPose(true, dir);                                // outstretched pose (if the Animator has it)
            northSide = dir.x < 0f ? -1 : 1; handInit = false;
            firing = true; stopRequested = false; firingEnd = end;
            FirstTimeHints.Show("tut_ray_stop", $"Press {GameInput.KeyLabel(GameInput.SkillKey(setSlot >= 0 ? setSlot : 2))} again (or roll) to end the ray early.", 3.5f);

            while (Time.time < end && p.ActionAlive(id) && !stopRequested)   // roll / stun / death / pressed again -> stop the beam
            {
                p.LockActions(0.1f);                            // stand still while firing
                p.LockedMoveMul = p.Health.Guarded ? veiledMove : 0f;   // with the Veil, slow gliding is allowed
                dir = EtherVfx.RotateTowards(dir, Aim(p, dir), turnRate * Time.deltaTime);
                p.RayAim(dir);
                UpdateNorthFlip(p, dir);
                Vector2 origin = SmoothHand(p, dir);
                Vector2 ground = (Vector2)p.transform.position + dir * 0.12f;   // collision at ground level (enemy collider = foot strip)
                float len = Scan(ground, dir);
                Place(glow, origin, dir, len);
                spark.transform.position = (Vector3)(origin + dir * len);
                spark.transform.localScale = Vector3.one * (0.7f + 0.15f * Mathf.Sin(Time.time * 35f));
                handGlow.transform.position = (Vector3)origin;
                handGlow.transform.localScale = Vector3.one * (0.8f + 0.1f * Mathf.Sin(Time.time * 25f));

                if (Time.time >= nextTick)
                {
                    nextTick = Time.time + tick;
                    done.Clear();
                    foreach (var e in enemies)
                    {
                        var ai = e.Key;
                        if (e.Value > len || ai == null || ai.Health == null || ai.Health.IsDead || done.Contains(ai.Health)) continue;
                        done.Add(ai.Health);
                        if (ai.Health.TakeDamage(dps * tick, dir, 0.15f) && UnityEngine.Random.value < 0.3f)
                            SpriteVfx.Spawn("hit", (Vector2)ai.transform.position + new Vector2(0f, 0.15f), 0.45f, p.VfxLayer, p.PlayerOrder + 5, dir.x < 0f);
                    }
                    PlayerCombat.RaiseStruck(ground + dir * len, 0.2f);
                }
                yield return null;
            }
            firing = false;
            p.LockedMoveMul = 0f;
            if (stopRequested || !p.ActionAlive(id))             // stopped early -> refund part of the cooldown by the unfired time
                p.Loadout.Refund(this, earlyRefund * Mathf.Clamp01((end - Time.time) / Mathf.Max(0.01f, duration)));
            stopRequested = false;
            if (p.Sprite != null) p.Sprite.flipX = false;
            p.RayPose(false, dir);
            if (glow != null) Destroy(glow.gameObject);
            if (spark != null) Destroy(spark.gameObject);
            if (handGlow != null) Destroy(handGlow.gameObject);
        }

        // Sweep along the beam: record enemies + obstacles; returns the beam length (to the nearest obstacle).
        // An obstacle enclosing an enemy on the beam path (e.g. the Plague Eye pit) does not stop the beam.
        private float Scan(Vector2 origin, Vector2 dir)
        {
            enemies.Clear(); walls.Clear();
            var filter = new ContactFilter2D { useTriggers = false, useLayerMask = false };
            // enemies: wide sweep (hitWidth) -> pierces every enemy on the beam
            int n = Physics2D.CircleCast(origin, hitWidth * 0.5f, dir, filter, buf, range);
            for (int i = 0; i < n; i++)
            {
                var ai = buf[i].collider.GetComponentInParent<EnemyAI>();
                if (ai != null) enemies.Add(new KeyValuePair<EnemyAI, float>(ai, buf[i].distance));
            }
            // walls / rocks: thin sweep along the beam centre (a rock off to the side does not cut the beam short)
            n = Physics2D.CircleCast(origin, 0.05f, dir, filter, buf, range);
            for (int i = 0; i < n; i++)
            {
                var c = buf[i].collider;
                if (c.GetComponentInParent<EnemyAI>() != null) continue;
                if (LineOfSight.IsSolid(c, false) && !c.OverlapPoint(origin)) walls.Add(new KeyValuePair<Collider2D, float>(c, buf[i].distance));
            }
            float len = range;
            foreach (var w in walls)
            {
                if (w.Value >= len) continue;
                bool enclosesEnemy = false;
                foreach (var e in enemies) if (e.Key != null && w.Key.OverlapPoint(e.Key.transform.position)) { enclosesEnemy = true; break; }
                if (!enclosesEnemy) len = w.Value;
            }
            return Mathf.Max(0.05f, len);
        }

        // Beam origin = the hand in the ray-hold sprite being shown. The Animator (SimpleDirectional2D, 8 directions) shows the direction
        // closest to dir -> use exactly that one. Hand coordinates: see handPixels; multiplied by lossyScale.
        private Vector2 Hand(PlayerCombat p, Vector2 dir)
        {
            Vector2 feet = p.transform.position;
            if (handPixels == null || handPixels.Length < 8 || spritePixelsPerUnit <= 0f)
                return feet + new Vector2(0f, 0.2f) + dir * 0.12f;          // missing data -> fallback (middle of the body)
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;           // E = 0, counter-clockwise: NE, N, NW, W, SW, S, SE
            int i = ((Mathf.RoundToInt(ang / 45f) % 8) + 8) % 8;
            Vector3 s = p.transform.lossyScale;
            Vector2 px = handPixels[i] / spritePixelsPerUnit;
            if (p.Sprite != null && p.Sprite.flipX) px.x = -px.x;          // sprite flipped (North, hand on the screen left)
            return feet + new Vector2(px.x * Mathf.Abs(s.x), px.y * Mathf.Abs(s.y));
        }

        private static int DirIndex(Vector2 dir)
        {
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;           // E = 0, counter-clockwise: NE, N, NW, W, SW, S, SE
            return ((Mathf.RoundToInt(ang / 45f) % 8) + 8) % 8;
        }

        /// <summary>North sprite — beam to the left (past the dead zone) flips the sprite so the hand reaches left.</summary>
        private void UpdateNorthFlip(PlayerCombat p, Vector2 dir)
        {
            float lean = Mathf.Sin(northSwapDeadzone * Mathf.Deg2Rad);
            if (dir.x < -lean) northSide = -1;
            else if (dir.x > lean) northSide = 1;
            // the North sprite extends the LEFT hand (screen left) -> flip when the beam goes RIGHT
            if (p.Sprite != null) p.Sprite.flipX = DirIndex(dir) == 2 && northSide > 0;
        }

        /// <summary>The beam origin slides quickly to the hand of the sprite shown (no jump when changing direction / hand).</summary>
        private Vector2 SmoothHand(PlayerCombat p, Vector2 dir)
        {
            Vector2 feet = p.transform.position;
            Vector2 target = Hand(p, dir) - feet;
            if (!handInit) { handOffset = target; handInit = true; }
            else handOffset = Vector2.Lerp(handOffset, target, 1f - Mathf.Exp(-handBlend * Time.deltaTime));
            return feet + handOffset;
        }

        // Aim direction: gamepad right stick (if pushed), otherwise the mouse cursor; neither -> keep the old direction
        private Vector2 Aim(PlayerCombat p, Vector2 fallback)
        {
            var pad = Gamepad.current;
            if (pad != null)
            {
                Vector2 s = pad.rightStick.ReadValue();
                if (s.sqrMagnitude > 0.09f) return s.normalized;
            }
            var mouse = Mouse.current;
            var cam = Camera.main;
            if (mouse != null && cam != null)
            {
                Vector2 sp = mouse.position.ReadValue();
                Vector3 w = cam.ScreenToWorldPoint(new Vector3(sp.x, sp.y, Mathf.Abs(cam.transform.position.z)));
                Vector2 d = (Vector2)w - Hand(p, fallback);
                if (d.sqrMagnitude > 0.0025f) return d.normalized;
            }
            return fallback;
        }

        private void Place(SpriteRenderer r, Vector2 origin, Vector2 dir, float len)
        {
            r.transform.position = new Vector3(origin.x, origin.y, 0f);
            r.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            float pulse = 1f + 0.12f * Mathf.Sin(Time.time * 40f);
            r.transform.localScale = new Vector3(len / EtherVfx.BeamWidthUnits, width / EtherVfx.BeamHeightUnits * pulse, 1f);
        }
    }
}
