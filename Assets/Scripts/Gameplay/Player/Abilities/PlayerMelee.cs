using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// NORMAL ATTACK = 3-HIT COMBO: diagonal slash -> rising backhand -> vertical cleave.
    /// Ren's body uses the same 3 animations attack1/2/3 (8 directions, PixelLab skeleton) for EVERY weapon; per-hit numbers, blade sprite, trail colour
    /// come from the held <see cref="WeaponDefinition"/> (PlayerCombat.weapon). Pressing again during recovery / right after the cancel frame
    /// (comboWindow) chains the next hit; past the window it restarts from hit 1. The last hit adds finisherRecovery.
    /// No weapon: uses the fallback numbers in PlayerCombat (damage, range, arcDegrees, knockback) for all 3 hits.
    /// Calls: PlayerStrikes (damage + hit feel), WeaponTraits (passives), TurnedBlade, PlayerWeaponView.
    /// </summary>
    internal sealed class PlayerMelee
    {
        private const float SlashVfxFps = 20f;
        private const float SlashArtRange = 0.75f;
        private static readonly string[] SlashKeys = { "E", "NE", "N", "NW", "W", "SW", "S", "SE" };
        private static readonly Vector2[] Dir8 =
        {
            new Vector2(1, 0), new Vector2(0.7071f, 0.7071f), new Vector2(0, 1), new Vector2(-0.7071f, 0.7071f),
            new Vector2(-1, 0), new Vector2(-0.7071f, -0.7071f), new Vector2(0, -1), new Vector2(0.7071f, -0.7071f),
        };

        private readonly PlayerCombat owner;
        private readonly PlayerAnimation anim;
        private readonly PlayerActionState actions;
        private readonly PlayerStrikes strikes;

        public float NextAttack;
        private int lastStep;            // last hit used (0 = none)
        private float chainUntil;        // deadline to chain the next hit
        private int showId;              // action holding the weapon

        public PlayerMelee(PlayerCombat owner, PlayerAnimation anim, PlayerActionState actions, PlayerStrikes strikes)
        {
            this.owner = owner; this.anim = anim; this.actions = actions; this.strikes = strikes;
        }

        public bool Ready => Time.time >= NextAttack && !actions.IsLocked;
        /// <summary>Combo hit just used (1-3), 0 = chain window over.</summary>
        public int ComboStep => Time.time <= chainUntil ? lastStep : 0;

        /// <summary>Arc centre: the character's hip.</summary>
        public Vector2 Pivot => (Vector2)owner.transform.position + Vector2.up * owner.pivotHeight;

        private ComboStep Step(int i)
        {
            var w = owner.Weapon;
            if (w != null && w.combo != null && w.combo.Length > 0) return w.combo[Mathf.Clamp(i, 0, w.combo.Length - 1)];
            return new ComboStep { damage = owner.damage, range = owner.range, arcDegrees = owner.arcDegrees, knockback = owner.knockback, flipTrail = i == 1 };
        }

        private int StepCount => owner.Weapon != null && owner.Weapon.combo != null && owner.Weapon.combo.Length > 0 ? Mathf.Min(3, owner.Weapon.combo.Length) : 3;

        public IEnumerator Slash(Vector2 facing)
        {
            int id = actions.Begin();
            Vector2 d = Snap(facing);
            int step = Time.time <= chainUntil && lastStep < StepCount ? lastStep + 1 : 1;
            var s = Step(step - 1);
            lastStep = step;

            anim.Attack(step, d, s.fps / 20f);
            owner.SetFacing(d);
            WeaponTraits.OnSwingStart(owner);                                    // Iaijutsu
            var view = owner.WeaponView;
            if (view != null) { view.Show(true); showId = id; }

            float now = Time.time;
            actions.LockUntil = now + s.CancelTime;                               // stand still until the cancel frame
            bool last = step >= StepCount;
            NextAttack = now + s.CancelTime + (last ? s.Length - s.CancelTime + (owner.Weapon != null ? owner.Weapon.finisherRecovery : 0.2f) : 0f);
            chainUntil = last ? 0f : now + s.CancelTime + (owner.Weapon != null ? owner.Weapon.comboWindow : 0.3f);

            yield return Strike(id, step, d, s, last, 0f);
        }

        /// <summary>The part after the swing — VFX + damage + keep the weapon until the clip ends. <paramref name="elapsed"/> = clip seconds already
        /// played when called (the Iaijutsu hold freezes on the raised frame before slashing on).</summary>
        private IEnumerator Strike(int id, int step, Vector2 d, ComboStep s, bool last, float elapsed)
        {
            string style = StyleOf(owner.Weapon);
            // frame-synced VFX — starts exactly when frame (hitFrame-1) shows, 2 VFX frames per animation frame
            float hitIn = Mathf.Max(0f, s.HitTime - elapsed);
            float lead = owner.showVfx ? (style != null ? Mathf.Min(1f / Mathf.Max(1f, s.fps), hitIn) : Mathf.Min(1f / SlashVfxFps, hitIn)) : 0f;
            yield return new WaitForSeconds(hitIn - lead);
            SoulRequiem.Core.Sfx.Play(last ? "pl_swing_heavy" : "pl_swing", owner.transform.position);   // sound
            if (!actions.Alive(id)) { Hide(id); yield break; }                    // cancelled by Roll / stun / death
            if (owner.showVfx)
            {
                if (style != null) SpawnFrameVfx(style, step, d, s); else SpawnVfx(d, s);
                if (lead > 0f) yield return new WaitForSeconds(lead);
                if (!actions.Alive(id)) { Hide(id); yield break; }
            }
            Vector2 pivot = Pivot;
            PlayerCombat.RaiseStruck(pivot + d * s.range * 0.5f, s.range * 0.6f);
            TurnedBlade.TryParry(pivot, d, s.range);                             // Turned Blade book — slashing a bullet reflects it
            float dmg = s.damage * WeaponTraits.MeleeMul(owner, last);           // + the katana's passive
            int n = strikes.Damage(pivot, s.range, dmg, s.knockback, c => InArc(c, pivot, d, s), true, last);
            if (n > 0) { strikes.Impact(d, s.impact); WeaponTraits.AfterMeleeHit(owner, dmg * n, n); }
            WeaponTraits.AfterSwing(owner, pivot + d * s.range * 0.5f, s.range * 0.6f, n, last);
            PlayerCombat.RaiseMeleeSwung(owner, pivot, d, s.range, dmg, s.knockback);
            if (owner.debugHitbox) owner.StartCoroutine(DebugArc(pivot, d, s));

            // the weapon stays visible until the clip ends (unless the next hit / a roll started)
            yield return new WaitForSeconds(Mathf.Max(0f, s.Length - Mathf.Max(s.HitTime, elapsed)));
            Hide(id);
        }

        /// <summary>
        /// IAIJUTSU HOLD (swords with the Iaijutsu passive, first combo hit). Pressing attack starts hit 1 as usual; still HOLDING the key
        /// when the raised-sword frame arrives freezes Ren in that stance (hit 1 animation paused), a charge bar beside him fills over `window` seconds
        /// (from the press). The dash key turns the slash direction in place. RELEASE -> the animation continues and slashes: bar full = draw strike
        /// (IaiCharged: bonus damage, stuns normal enemies); not full = normal hit. A quick tap = a normal hit 1, no delay.
        /// A roll / stun cancels the hold.
        /// </summary>
        public IEnumerator IaiSlash(System.Func<bool> held)
        {
            int id = actions.Begin();
            Vector2 d = Snap(owner.Facing);
            var s = Step(0);
            lastStep = 1;
            bool last = StepCount <= 1;
            float speed = s.fps / 20f;
            anim.Attack(1, d, speed);
            owner.SetFacing(d);
            owner.IaiCharged = false; owner.IaiCharge = 0f; owner.IaiHolding = false; owner.IaiPower = 0f;
            var view = owner.WeaponView;
            if (view != null) { view.Show(true); showId = id; }

            // the bar is full after `full` seconds (max); only matters once held past `window`, stronger the longer until full
            float pressAt = Time.time, window = Mathf.Max(0.05f, WeaponTraits.IaiWindow(owner)), full = Mathf.Max(window + 0.05f, WeaponTraits.IaiFull(owner));
            float holdAt = s.HitTime * 0.55f;                                     // raised-sword frame, before the slash frame
            actions.LockUntil = pressAt + holdAt + 0.05f;
            while (Time.time - pressAt < holdAt)
            {
                if (!actions.Alive(id)) { Hide(id); yield break; }
                yield return null;
            }
            float elapsed = holdAt;
            if (held())
            {
                anim.SetAttackSpeed(0f);                                          // holding the stance
                owner.IaiHolding = true;
                while (held())
                {
                    if (!actions.Alive(id))
                    {
                        anim.SetAttackSpeed(speed);
                        owner.IaiHolding = false; owner.IaiCharge = 0f;
                        Hide(id); yield break;
                    }
                    actions.LockUntil = Time.time + 0.1f;
                    owner.IaiCharge = Mathf.Clamp01((Time.time - pressAt) / full);
                    Vector2 nd = Snap(owner.Facing);
                    if (nd != d) { d = nd; anim.SetAttackDir(d); owner.SetFacing(d); }
                    yield return null;
                }
                owner.IaiHolding = false;
                float heldFor = Time.time - pressAt;
                owner.IaiCharged = heldFor >= window;                                    // not past the threshold = normal hit
                owner.IaiPower = owner.IaiCharged ? Mathf.Clamp01((heldFor - window) / (full - window)) : 0f;   // max = full bar
                owner.IaiCharge = 0f;
                anim.SetAttackSpeed(speed);
                if (owner.IaiCharged) { var st = owner.GetComponent<IaiStance>(); if (st != null) st.Release(d, owner.IaiPower); }
            }
            float now = Time.time;
            float toCancel = Mathf.Max(0f, s.CancelTime - elapsed);
            actions.LockUntil = now + toCancel;
            NextAttack = now + toCancel + (last ? s.Length - s.CancelTime + (owner.Weapon != null ? owner.Weapon.finisherRecovery : 0.2f) : 0f);
            chainUntil = last ? 0f : now + toCancel + (owner.Weapon != null ? owner.Weapon.comboWindow : 0.3f);
            WeaponTraits.OnSwingStart(owner);                                     // record LastSwingTime (Iaijutsu does not recompute IaiCharged)
            yield return Strike(id, 1, d, s, last, elapsed);
        }

        private void Hide(int id)
        {
            if (showId != id) return;
            var view = owner.WeaponView;
            if (view != null) view.Show(false);
        }

        /// <summary>Cancel the combo (roll / heavy hit): the next press starts from hit 1 again, hides the weapon.</summary>
        public void ResetCombo()
        {
            lastStep = 0; chainUntil = 0f;
            var view = owner.WeaponView;
            if (view != null) view.Show(false);
        }

        public void PreloadVfx()
        {
            foreach (var k in SlashKeys) SpriteVfx.Load("slash_" + k);
            SpriteVfx.Load(ThrustVfx);
            string style = StyleOf(owner.Weapon);
            if (style != null)
                for (int n = 1; n <= 3; n++) foreach (var k in SlashKeys) SpriteVfx.Load(FrameVfxName(style, n, k));
        }

        // ------------------------------------------------------------------ FRAME-SYNCED slash VFX per sword
        private static string StyleOf(WeaponDefinition w) => w != null && !string.IsNullOrEmpty(w.slashStyle) ? w.slashStyle : null;
        private static string FrameVfxName(string style, int step, string key) => "slash3/" + style + "_" + step + "_" + key;

        /// <summary>The VFX image is drawn on the same canvas as Ren's sprite for that direction, pivot = sprite feet -> matches Ren's position + scale.
        /// Not tinted (colours are part of each sword's design).</summary>
        private void SpawnFrameVfx(string style, int step, Vector2 d, ComboStep s)
        {
            float a = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            int i = ((Mathf.RoundToInt(a / 45f) % 8) + 8) % 8;
            var body = owner.Sprite;
            Vector2 pos = body != null ? (Vector2)body.transform.position : (Vector2)owner.transform.position;
            float k = body != null ? Mathf.Abs(body.transform.lossyScale.x) : 1f;
            string name = FrameVfxName(style, Mathf.Clamp(step, 1, 3), SlashKeys[i]);
            if (SpriteVfx.Load(name).Length == 0) { SpawnVfx(d, s); return; }   // missing image -> fallback VFX
            SpriteVfx.Spawn(name, pos, k, owner.VfxLayer, owner.PlayerOrder + (d.y > 0.3f ? -1 : 1), false, 2f * Mathf.Max(1f, s.fps));
        }

        /// <summary>Closest of the 8 directions (the body has attack animations in all 8).</summary>
        public Vector2 Snap(Vector2 f)
        {
            if (f.sqrMagnitude < 0.0001f) return Vector2.down;
            Vector2 best = Dir8[0]; float bd = -2f;
            foreach (var v in Dir8)
            {
                float dd = Vector2.Dot(f.normalized, v);
                if (dd > bd + 1e-4f) { bd = dd; best = v; }
            }
            return best;
        }

        private bool InArc(Collider2D c, Vector2 pivot, Vector2 d, ComboStep s)
        {
            Vector2 near = c.ClosestPoint(pivot) - pivot;
            if (near.magnitude <= owner.innerRadius) return true;
            float half = s.arcDegrees * 0.5f;
            Vector2 mid = (Vector2)c.bounds.center - pivot;
            return Vector2.Angle(d, near) <= half || (mid.magnitude <= s.range && Vector2.Angle(d, mid) <= half);
        }

        private const string ThrustVfx = "m3_thrust_E";                   // straight thrust trail (drawn facing East, rotated to the thrust direction)

        private void SpawnVfx(Vector2 d, ComboStep s)
        {
            float a = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            if (s.thrust)
            {
                float k = owner.vfxScale * Mathf.Max(0.3f, s.range / SlashArtRange) * s.trailScale;
                var t = SpriteVfx.Spawn(ThrustVfx, Pivot + d * 0.08f, k, owner.VfxLayer, owner.PlayerOrder + (d.y > 0.3f ? -1 : 1), false, 18f);
                if (t == null) return;
                t.transform.rotation = Quaternion.Euler(0f, 0f, a);
                if (owner.Weapon != null) t.Tint(owner.Weapon.trailColor);
                return;
            }
            int i = ((Mathf.RoundToInt(a / 45f) % 8) + 8) % 8;
            float scale = owner.vfxScale * Mathf.Max(0.3f, s.range / SlashArtRange) * s.trailScale;
            var v = SpriteVfx.Spawn("slash_" + SlashKeys[i], Pivot, scale, owner.VfxLayer, owner.PlayerOrder + (d.y > 0.3f ? -1 : 1), false, SlashVfxFps);
            if (v == null) return;
            if (owner.Weapon != null) v.Tint(owner.Weapon.trailColor);
            if (s.flipTrail)                                                      // rising backhand: mirror the trail across the slash axis
            {                                                                     // (flip vertically then rotate 2a = reflection across the line at angle a)
                var sc = v.transform.localScale;
                v.transform.localScale = new Vector3(sc.x, -sc.y, sc.z);
                v.transform.rotation = Quaternion.Euler(0f, 0f, 2f * a);
            }
        }

        public Vector3[] ArcOutline(Vector2 pivot, Vector2 d) => ArcOutline(pivot, d, Step(0));

        private Vector3[] ArcOutline(Vector2 pivot, Vector2 d, ComboStep s)
        {
            const int seg = 24;
            var list = new List<Vector3>();
            float a0 = Mathf.Atan2(d.y, d.x) - s.arcDegrees * 0.5f * Mathf.Deg2Rad;
            float span = s.arcDegrees * Mathf.Deg2Rad;
            list.Add(pivot + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * owner.innerRadius);
            for (int k = 0; k <= seg; k++)
            {
                float a = a0 + span * k / seg;
                list.Add(pivot + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * s.range);
            }
            for (int k = seg; k >= 0; k--)
            {
                float a = a0 + span * k / seg;
                list.Add(pivot + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * owner.innerRadius);
            }
            return list.ToArray();
        }

        private IEnumerator DebugArc(Vector2 pivot, Vector2 d, ComboStep s)
        {
            var go = new GameObject("fx_hitbox");
            var lr = go.AddComponent<LineRenderer>();
            var pts = ArcOutline(pivot, d, s);
            lr.positionCount = pts.Length; lr.SetPositions(pts); lr.loop = true;
            lr.widthMultiplier = 0.015f;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = lr.endColor = new Color(1f, 0.25f, 0.25f, 0.9f);
            lr.sortingLayerID = owner.VfxLayer; lr.sortingOrder = owner.PlayerOrder + 5;
            yield return new WaitForSeconds(0.25f);
            Object.Destroy(lr.material);
            Object.Destroy(go);
        }
    }
}
