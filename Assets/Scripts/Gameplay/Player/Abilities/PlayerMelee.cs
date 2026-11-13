using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 09/11 (§201): ĐÁNH THƯỜNG = COMBO 3 NHÁT (thay đòn rút kiếm Iaido): chém chéo -> chém hất ngược -> bổ dọc.
    /// Thân Ren dùng chung 3 hoạt ảnh attack1/2/3 (8 hướng, PixelLab skeleton) cho MỌI vũ khí; số liệu từng nhát, hình lưỡi, màu vệt
    /// lấy từ <see cref="WeaponDefinition"/> đang cầm (PlayerCombat.weapon). Bấm tiếp trong lúc thu đòn / ngay sau khung huỷ
    /// (comboWindow) thì nối nhát kế; quá cửa sổ thì lại từ nhát 1. Nhát cuối có thêm finisherRecovery.
    /// Không có vũ khí: dùng số liệu cũ trong PlayerCombat (damage, range, arcDegrees, knockback) cho cả 3 nhát.
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
        private int lastStep;            // nhát vừa ra (0 = chưa)
        private float chainUntil;        // hạn nối nhát kế
        private int showId;              // hành động đang cầm vũ khí

        public PlayerMelee(PlayerCombat owner, PlayerAnimation anim, PlayerActionState actions, PlayerStrikes strikes)
        {
            this.owner = owner; this.anim = anim; this.actions = actions; this.strikes = strikes;
        }

        public bool Ready => Time.time >= NextAttack && !actions.IsLocked;
        /// <summary>Nhát combo vừa ra (1-3), 0 = đã hết cửa sổ nối.</summary>
        public int ComboStep => Time.time <= chainUntil ? lastStep : 0;

        /// <summary>Tâm hình quạt: hông nhân vật.</summary>
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
            var view = owner.WeaponView;
            if (view != null) { view.Show(true); showId = id; }

            float now = Time.time;
            actions.LockUntil = now + s.CancelTime;                               // đứng yên tới khung huỷ
            bool last = step >= StepCount;
            NextAttack = now + s.CancelTime + (last ? s.Length - s.CancelTime + (owner.Weapon != null ? owner.Weapon.finisherRecovery : 0.2f) : 0f);
            chainUntil = last ? 0f : now + s.CancelTime + (owner.Weapon != null ? owner.Weapon.comboWindow : 0.3f);

            string style = StyleOf(owner.Weapon);
            // 10/11 (§209): VFX theo khung — bắt đầu đúng lúc khung (hitFrame-1) hiện, chạy 2 khung VFX / 1 khung hoạt ảnh
            float lead = owner.showVfx ? (style != null ? Mathf.Min(1f / Mathf.Max(1f, s.fps), s.HitTime) : Mathf.Min(1f / SlashVfxFps, s.HitTime)) : 0f;
            yield return new WaitForSeconds(s.HitTime - lead);
            if (!actions.Alive(id)) { Hide(id); yield break; }                    // bị Roll / choáng / gục huỷ
            if (owner.showVfx)
            {
                if (style != null) SpawnFrameVfx(style, step, d, s); else SpawnVfx(d, s);
                if (lead > 0f) yield return new WaitForSeconds(lead);
                if (!actions.Alive(id)) { Hide(id); yield break; }
            }
            Vector2 pivot = Pivot;
            PlayerCombat.RaiseStruck(pivot + d * s.range * 0.5f, s.range * 0.6f);
            TurnedBlade.TryParry(pivot, d, s.range);                             // sách Turned Blade — chém trúng đạn thì dội ngược
            float dmg = s.damage * WeaponTraits.MeleeMul(owner);                 // 10/11 (§217): nội tại của thanh katana
            int n = strikes.Damage(pivot, s.range, dmg, s.knockback, c => InArc(c, pivot, d, s));
            if (n > 0) { strikes.Impact(d, s.impact); WeaponTraits.AfterMeleeHit(owner, dmg * n, n); }
            if (owner.debugHitbox) owner.StartCoroutine(DebugArc(pivot, d, s));

            // vũ khí còn hiện tới hết clip (trừ khi đã sang nhát khác / lăn)
            yield return new WaitForSeconds(Mathf.Max(0f, s.Length - s.HitTime));
            Hide(id);
        }

        private void Hide(int id)
        {
            if (showId != id) return;
            var view = owner.WeaponView;
            if (view != null) view.Show(false);
        }

        /// <summary>Huỷ combo (lăn / trúng đòn nặng): lần bấm sau lại từ nhát 1, ẩn vũ khí.</summary>
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

        // ------------------------------------------------------------------ 10/11 (§209): VFX chém KHỚP KHUNG theo từng thanh kiếm
        private static string StyleOf(WeaponDefinition w) => w != null && !string.IsNullOrEmpty(w.slashStyle) ? w.slashStyle : null;
        private static string FrameVfxName(string style, int step, string key) => "slash3/" + style + "_" + step + "_" + key;

        /// <summary>Ảnh VFX vẽ trên canvas trùng canvas sprite Ren cùng hướng, pivot = chân sprite -> đặt đúng vị trí + tỉ lệ của sprite Ren.
        /// Không nhuộm (màu do thiết kế từng thanh kiếm).</summary>
        private void SpawnFrameVfx(string style, int step, Vector2 d, ComboStep s)
        {
            float a = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            int i = ((Mathf.RoundToInt(a / 45f) % 8) + 8) % 8;
            var body = owner.Sprite;
            Vector2 pos = body != null ? (Vector2)body.transform.position : (Vector2)owner.transform.position;
            float k = body != null ? Mathf.Abs(body.transform.lossyScale.x) : 1f;
            string name = FrameVfxName(style, Mathf.Clamp(step, 1, 3), SlashKeys[i]);
            if (SpriteVfx.Load(name).Length == 0) { SpawnVfx(d, s); return; }   // thiếu ảnh -> VFX cũ
            SpriteVfx.Spawn(name, pos, k, owner.VfxLayer, owner.PlayerOrder + (d.y > 0.3f ? -1 : 1), false, 2f * Mathf.Max(1f, s.fps));
        }

        /// <summary>Hướng gần nhất trong 8 hướng (thân có hoạt ảnh đòn đủ 8 hướng).</summary>
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

        private const string ThrustVfx = "m3_thrust_E";                   // 09/11 (§202): vệt đâm thẳng (vẽ hướng Đông, xoay theo hướng đâm)

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
            if (s.flipTrail)                                                      // nhát hất ngược: soi gương vệt qua trục hướng chém
            {                                                                     // (lật dọc rồi xoay 2a = phản xạ qua đường góc a)
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
