using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 07/11 (§183) "Grasping Roots" — bộ "Mantras of the Vessel" (cổ vật Specimen Zero), ô I. Ren dậm đất: rễ thịt tím (chính tranh
    /// rễ PixelLab của Specimen Zero, Resources/VFX/specimen/roots{a..d}_*) trồi nối đuôi dọc một đường dài <see cref="length"/> đv theo
    /// hướng ngắm (tường chặn). Mỗi quái trên đường: <see cref="damage"/> + trói (choáng) <see cref="rootTime"/> s. Boss chỉ nhận sát thương.
    /// 08/11 (§191, gỡ trùng): trước đây chỉ trói (trùng vai khống chế với Hand) và vô dụng với boss. Nay là chiêu MỞ COMBO: mọi quái
    /// trúng rễ (cả boss) mang dấu BOUND <see cref="boundTime"/> s (SkillKit.Bind, PixelLab sk_bound) — Hand of the Vessel đánh mạnh
    /// hơn lên mục tiêu Bound. Quái thường vẫn bị trói <see cref="rootTime"/> s. Sát thương 24 -> 30 (asset).
    /// Cân bằng: 30 / 9 s ≈ 3,3 DPS mỗi con + trói cả hàng + mở combo Hand (+50% của 75).
    /// </summary>
    [CreateAssetMenu(menuName = "Soul Requiem/Skill/Grasping Roots", fileName = "VesselRoots")]
    public class GraspingRootsSkill : SkillDefinition
    {
        // 11/11 (§228): thông số cho bảng Shrine.
        public override string Kind => "Line - Control";
        public override void Stats(System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>> o)
        {
            Stat(o, "Damage", damage.ToString("0"));
            Stat(o, "Length", Tiles(length));
            Stat(o, "Rooted", Secs(rootTime));
            Stat(o, "Bound", Secs(boundTime));
        }

        public float length = 2.4f;
        public float spacing = 0.36f;
        public float stepDelay = 0.06f;
        [Tooltip("Giây từ lúc bấm tới khi rễ đầu trồi (lúc chân dậm). Lăn trước lúc đó = huỷ, hoàn hồi chiêu.")]
        public float startDelay = 0.28f;
        public float damage = 24f;
        public float hitRadius = 0.36f;
        public float rootTime = 1.4f;
        [Tooltip("08/11 (§191): thời gian dấu Bound (cả boss).")]
        public float boundTime = 2f;
        public float castLock = 0.5f;
        public float rootScale = 0.75f;
        public Color rootTint = new Color(0.82f, 0.93f, 1f); // 16/11: chiêu của Ren = xanh lam Ascended (Ren chưa nhiễm gì); boss giữ đỏ

        public override IEnumerator Cast(PlayerCombat p, Action cancel)
        {
            int id = p.BeginAction();
            Vector2 dir = SkillKit.AimDir(p, p.Facing.sqrMagnitude > 0.01f ? p.Facing : Vector2.down);
            p.SetFacing(dir);
            p.LockActions(castLock);
            if (p.PlaySkillAnimation() && startDelay > 0f)
            {
                yield return new WaitForSeconds(startDelay);
                if (!p.ActionAlive(id)) { cancel?.Invoke(); yield break; }
            }
            Vector2 feet = p.transform.position;
            float len = SkillKit.WallDistance(feet, dir, length, 0.08f);
            int layer = p.VfxLayer, order = p.PlayerOrder;
            var kinds = new[] { SkillKit.Frames("VFX/specimen/blue/rootsa_", 8), SkillKit.Frames("VFX/specimen/blue/rootsb_", 8),
                                SkillKit.Frames("VFX/specimen/blue/rootsc_", 8), SkillKit.Frames("VFX/specimen/blue/rootsd_", 8) };
            var done = new HashSet<EnemyAI>();
            Vector2 side = new Vector2(-dir.y, dir.x);
            int k = 0;
            p.KickCamera(Vector2.down, 0.7f, 0.15f);
            for (float d = spacing * 0.9f; d <= len + 0.01f; d += spacing, k++)
            {
                Vector2 at = feet + dir * d + side * ((k % 2 == 0 ? 1f : -1f) * 0.06f);
                var fr = kinds[k % kinds.Length];
                if (fr.Length > 0) p.StartCoroutine(SkillKit.PlayFrames(fr, at, rootScale, 16f, rootTint, layer, order + 1, k % 2 == 1));
                foreach (var ai in SkillKit.EnemiesIn(at, hitRadius))
                {
                    if (!done.Add(ai)) continue;
                    if (!ai.Health.TakeDamage(damage, dir, 0.3f)) continue;
                    SkillKit.HitFx(p, ai, dir);
                    SkillKit.Control(ai, rootTime, 0f, 0f, layer, order);
                    SkillKit.Bind(ai, boundTime, layer, order);
                }
                PlayerCombat.RaiseStruck(at, hitRadius);
                yield return new WaitForSeconds(stepDelay);
            }
            if (done.Count > 0) p.HitStopNow();
        }
    }
}
