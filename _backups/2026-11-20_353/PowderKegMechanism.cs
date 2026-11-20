using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 19/11 (§345): powder_keg — THÙNG THUỐC NỔ của The Warden Gallery (Map 4). Ai cũng có thể châm:
    ///   • Ren chém thùng -> ngòi cháy 1,1 s (kịp chạy) rồi nổ;
    ///   • đạn Grapeshot nổ sát thùng / quả cối của Brazen Reliquary rơi gần / Soulfire Slam của Hollow Champion -> ngòi ngắn;
    ///   • Champion có thể NHẤC thùng ném về phía Ren (Lift — thùng biến khỏi chỗ, HollowChampionSkills lo phần bay).
    /// Nổ: vòng báo đỏ suốt thời gian ngòi cháy, rồi sát thương vùng (Ren lăn né được), Champion trúng -> choáng + mất máu (KegHit),
    /// thùng khác gần đó nổ dây chuyền, tượng gần đó nứt, để lại vũng lửa ngắn. Trận đặt lại -> thùng về chỗ cũ.
    /// Thuộc tính: `sprite` (mặc định VFX/props/powder_keg), `damage` (24), `radius` (0,95 đv).
    /// </summary>
    public sealed class PowderKegMechanism : Mechanism
    {
        private SpriteRenderer sr;
        private BoxCollider2D col;
        private bool lit;
        private Coroutine fuseCo;
        private readonly List<Health> hits = new List<Health>();
        private static readonly Collider2D[] buf = new Collider2D[24];

        public override bool ReactsToStrike => true;
        /// <summary>Còn nguyên, chưa châm (Champion nhấc được).</summary>
        public bool Ready => !Done && !lit;
        public Vector2 Center => new Vector2(Rect.center.x, Rect.yMin + 0.12f);
        public Sprite Art => sr != null ? sr.sprite : null;
        private float Radius => Float("radius", 0.95f);

        public override void Init()
        {
            sr = TrapArt.MakeRenderer(Ctx.Host.transform, "Keg " + Name, new Vector3(Rect.center.x, Rect.yMin + 0.02f, 0f), Ctx.Player, 0);
            sr.sprite = WardenStatueMechanism.Load(Str("sprite", "VFX/props/powder_keg"));
            var go = new GameObject("KegCollider " + Name);
            go.transform.SetParent(Ctx.Host.transform, true);
            go.transform.position = Rect.center;
            col = go.AddComponent<BoxCollider2D>();
            col.size = Rect.size;
            // đạn thẳng va vào thùng -> nổ ngay tại thùng (Animate thấy đạn nổ sát -> châm ngòi ngắn)
            MapEncounterDirector.ZoneReset += OnZoneReset;
        }

        public override void OnStruck()
        {
            if (!Ready) return;
            Hints.Show("The fuse catches! Get clear!", 1.6f);
            Ignite(1.1f);
        }

        public override void Animate()
        {
            if (!Ready) return;
            Vector2 c = Center;
            foreach (var p in Projectile.Live)
                if (p != null && p.Exploding && ((Vector2)p.transform.position - c).sqrMagnitude < 0.38f * 0.38f) { Ignite(0.55f); return; }
        }

        /// <summary>Châm ngòi: `fuse` giây sau nổ (đang cháy rồi thì thôi).</summary>
        public void Ignite(float fuse)
        {
            if (!Ready) return;
            lit = true;
            fuseCo = Ctx.Host.StartCoroutine(Fuse(Mathf.Max(0.15f, fuse)));
        }

        /// <summary>Champion nhấc thùng đi ném: thùng biến khỏi chỗ (không nổ ở đây).</summary>
        public void Lift()
        {
            if (!Ready) return;
            Done = true;
            if (sr != null) sr.enabled = false;
            if (col != null) col.enabled = false;
        }

        private IEnumerator Fuse(float fuse)
        {
            Sfx.Play("keg_fuse", Center);
            var warn = GroundStrike.Spawn(Center, fuse, Radius, 0f, null, 0, 14f, sr != null ? sr.sortingOrder - 1 : 0, new Color(1f, 0.3f, 0.15f));
            warn.impactSfx = "none";
            for (float t = 0f; t < fuse; t += Time.deltaTime)
            {
                if (sr != null) sr.color = Mathf.Repeat(t * (4f + t * 10f), 1f) < 0.5f ? new Color(1f, 0.45f, 0.35f) : Color.white;
                yield return null;
            }
            Explode();
        }

        private void Explode()
        {
            Done = true; fuseCo = null;
            Vector2 c = Center;
            if (sr != null) { sr.color = Color.white; sr.enabled = false; }
            if (col != null) col.enabled = false;
            Blast(c, Radius, Float("damage", 24f), sr != null ? sr.sortingLayerID : 0, sr != null ? sr.sortingOrder : 0);
        }

        /// <summary>Vụ nổ thùng (dùng chung cho thùng do Champion ném): VFX, sát thương Ren + quái, dây chuyền, tượng nứt, vũng lửa.</summary>
        internal static void Blast(Vector2 c, float r, float damage, int layer, int order)
        {
            Sfx.Play("keg_blast", c);
            SpriteVfx.Spawn("duo_keg_blast", c + Vector2.up * 0.1f, 1.6f, layer, order + 2, Random.value < 0.5f, 16f, false);
            CameraFollow.KickMain(Vector2.down, 0.09f, 0.3f);
            var mm = MapMechanics.Instance;
            // Ren
            var pc = Object.FindFirstObjectByType<PlayerController>();
            if (pc != null)
            {
                Vector2 d = (Vector2)pc.transform.position - c;
                var ph = pc.GetComponent<Health>();
                if (ph != null && !ph.IsDead && d.magnitude <= r)
                {
                    Health.IncomingRanged = true;
                    try { ph.TakeDamage(damage, d.sqrMagnitude > 0.0001f ? d.normalized : Vector2.down, 3.6f); }
                    finally { Health.IncomingRanged = false; }
                }
            }
            // quái / boss
            int n = Physics2D.OverlapCircleNonAlloc(c, r, buf);
            var done = new HashSet<Health>();
            for (int i = 0; i < n; i++)
            {
                var h = buf[i] != null ? buf[i].GetComponentInParent<Health>() : null;
                if (h == null || h.IsPlayer || h.IsDead || !done.Add(h)) continue;
                h.TakeDamage(damage * 1.25f, ((Vector2)h.transform.position - c).normalized, 2.5f);
            }
            if (mm != null) { mm.IgniteKegs(c, r + 0.3f, 0.35f); mm.ChipStatues(c, r, 3); }
            BurnPatch.Spawn(c, "duo_soulfire", 0.75f, 3f, layer, order - 3, 0.36f, 4f, new Color(1f, 0.55f, 0.2f), new Color(1f, 0.6f, 0.3f));
        }

        private void OnZoneReset(Rect zone)
        {
            if (Ctx.Host == null) { MapEncounterDirector.ZoneReset -= OnZoneReset; return; }
            if (!zone.Contains(Rect.center)) return;
            if (fuseCo != null) { Ctx.Host.StopCoroutine(fuseCo); fuseCo = null; }
            Done = false; lit = false;
            if (sr != null) { sr.enabled = true; sr.color = Color.white; }
            if (col != null) col.enabled = true;
        }

        internal override void DebugSolve() { }
    }
}
