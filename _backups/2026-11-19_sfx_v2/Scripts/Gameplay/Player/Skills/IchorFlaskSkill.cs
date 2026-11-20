using System;
using System.Collections;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 07/11 (§183) "Ichor Flask" — chiêu lẻ, nhặt ở Feeding Hall (Map 2). Ren ném một lọ Ichor theo vòng cung tới điểm ngắm
    /// (≤ <see cref="range"/> đv; ném qua đầu vật cản, điểm đáp bị đẩy ra khỏi tường). Lọ vỡ: <see cref="splashDamage"/> quanh chỗ vỡ,
    /// để lại vũng <see cref="poolTime"/> s: quái đứng trong vũng mất <see cref="poolDps"/>/s và bị CORRODED (+<see cref="corrode"/> sát thương
    /// nhận, còn <see cref="corrodeLinger"/> s sau khi ra khỏi vũng). Quái sinh từ Ichor (tên chứa một chuỗi trong <see cref="resistant"/>)
    /// vẫn mất máu nhưng KHÔNG bị Corroded — khắc chế tự nhiên ở Map 2.
    /// Cân bằng (1 mục tiêu đứng yên): 15 + 24 + 20% × 48 DPS × 4 s ≈ 77 / 12 s ≈ 6,4 DPS — mạnh lên bầy và khi phối với chém thường.
    /// </summary>
    [CreateAssetMenu(menuName = "Soul Requiem/Skill/Ichor Flask", fileName = "IchorFlask")]
    public class IchorFlaskSkill : SkillDefinition
    {
        // 11/11 (§228): thông số cho bảng Shrine.
        public override string Kind => "Thrown - Area";
        public override void Stats(System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>> o)
        {
            Stat(o, "Throw range", Tiles(range));
            Stat(o, "Burst damage", splashDamage.ToString("0"));
            Stat(o, "Pool", poolDps.ToString("0.#") + " /s for " + Secs(poolTime));
            Stat(o, "Pool radius", Tiles(poolRadius));
            Stat(o, "Corroded", "+" + Pct(corrode) + " damage taken");
        }

        public float range = 2.6f;
        public float flight = 0.45f;
        public float arc = 0.5f;
        public float splashRadius = 0.6f;
        public float splashDamage = 15f;
        public float poolRadius = 0.6f;
        public float poolTime = 4f;
        public float poolDps = 6f;
        public float tick = 0.5f;
        [Tooltip("Sát thương nhận thêm khi Corroded (0.2 = +20%).")]
        public float corrode = 0.2f;
        public float corrodeLinger = 1.5f;
        public float castLock = 0.35f;
        [Tooltip("Giây từ lúc bấm tới lúc lọ rời tay. Lăn trước lúc đó = huỷ, hoàn hồi chiêu.")]
        public float throwDelay = 0.22f;
        public string[] resistant = { "Ichor", "Vat_Born", "Brood_Toad" };
        public string splashVfx = "sk_ichor_splash", poolVfx = "sk_ichor_pool";
        [Tooltip("Tỉ lệ ảnh vũng (ảnh rộng ~1,09 đv ở tỉ lệ 1).")]
        public float poolScale = 1.1f;

        private static Sprite vial;

        public override IEnumerator Cast(PlayerCombat p, Action cancel)
        {
            int id = p.BeginAction();
            p.LockActions(castLock);
            Vector2 aim = SkillKit.AimPoint(p, range);
            Vector2 dir0 = aim - (Vector2)p.transform.position;
            if (dir0.sqrMagnitude > 0.0001f) p.SetFacing(dir0);
            if (p.PlayEmbersCast() && throwDelay > 0f)
            {
                yield return new WaitForSeconds(throwDelay);
                if (!p.ActionAlive(id)) { cancel?.Invoke(); yield break; }
            }
            Vector2 from = (Vector2)p.transform.position + new Vector2(0f, 0.3f);
            Vector2 to = SkillKit.FreeSpot(aim, p.transform.position);
            int layer = p.VfxLayer, order = p.PlayerOrder;

            // lọ bay vòng cung, xoay tròn
            if (vial == null) vial = Resources.Load<Sprite>("VFX/sk_vial");
            var go = new GameObject("Ichor Vial");
            var sr = go.AddComponent<SpriteRenderer>();
            FxLighting.MakeUnlit(sr);
            sr.sprite = vial; sr.sortingLayerID = layer; sr.sortingOrder = order + 3;
            for (float t = 0f; t < flight; t += Time.deltaTime)
            {
                float u = t / flight;
                go.transform.position = (Vector3)(Vector2.Lerp(from, to, u) + Vector2.up * arc * 4f * u * (1f - u));
                go.transform.rotation = Quaternion.Euler(0f, 0f, -720f * u);
                yield return null;
            }
            Destroy(go);

            // vỡ
            if (p.showVfx) SpriteVfx.Spawn(splashVfx, to, 1f, layer, order + 2, UnityEngine.Random.value < 0.5f, 15f);
            foreach (var ai in SkillKit.EnemiesIn(to, splashRadius))
                if (ai.Health.TakeDamage(splashDamage, (Vector2)ai.transform.position - to, 0.6f)) SkillKit.HitFx(p, ai, (Vector2)ai.transform.position - to);
            PlayerCombat.RaiseStruck(to, splashRadius);
            p.KickCamera(Vector2.down, 0.4f, 0.1f);

            // vũng
            SpriteVfx pool = p.showVfx ? SpriteVfx.Spawn(poolVfx, to, poolScale, layer, order - 1, false, 10f) : null;
            if (pool != null) pool.Looping(poolTime).Tint(new Color(1f, 1f, 1f, 0.85f));
            float end = Time.time + poolTime;
            while (Time.time < end)
            {
                foreach (var ai in SkillKit.EnemiesIn(to, poolRadius))
                {
                    ai.Health.TakeDamage(poolDps * tick, Vector2.zero, 0f);
                    if (!Resists(ai)) SkillKit.Vulnerable(ai, corrode, tick + corrodeLinger, SkillKit.CorrodedTint, layer, order);
                }
                yield return new WaitForSeconds(tick);
            }
        }

        private bool Resists(EnemyAI ai)
        {
            if (resistant == null) return false;
            string n = ai.definition != null ? ai.definition.name : ai.name;
            foreach (var r in resistant) if (!string.IsNullOrEmpty(r) && (n.Contains(r) || ai.name.Contains(r))) return true;
            return false;
        }
    }
}
