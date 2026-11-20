using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 19/11 (§345): warden_statue — TƯỢNG CÁC CHAMPION ĐỜI TRƯỚC trong The Warden Gallery (Map 4, song đấu Reliquary Wardens).
    /// Vùng object = chân bệ tượng (va chạm đặc: chặn bước + chặn đạn thẳng). Vai trò trong trận:
    ///   • Che chắn: Grapeshot của Brazen Reliquary (bắn từ bệ pháo) vỡ vào tượng -> Ren nấp sau tượng. Mỗi viên trúng / quả cối rơi sát
    ///     làm tượng nứt thêm (`hits`, mặc định 6; ảnh `sprite_damaged` nặng dần) -> hết `hits` thì vỡ, chỉ còn bệ thấp (đạn bay qua).
    ///   • Bẫy Champion: Hollow Champion lao (Rush / Beacon Charge) đâm trúng tượng -> tượng vỡ tung, hắn CHOÁNG (HollowChampionSkills).
    /// Ren chém tượng không vỡ (đá). Chết giữa trận / trận đặt lại -> tượng dựng lại nguyên vẹn (ZoneReset).
    /// Thuộc tính: `sprite`, `sprite_damaged` (danh sách), `sprite_broken` (bệ trống), `hits`.
    /// </summary>
    public sealed class WardenStatueMechanism : Mechanism
    {
        private SpriteRenderer sr;
        private BoxCollider2D col;
        private Sprite whole, broken;
        private readonly List<Sprite> damaged = new List<Sprite>();
        private readonly HashSet<Projectile> counted = new HashSet<Projectile>();
        private int taken;

        /// <summary>Còn đứng (chặn đạn).</summary>
        public bool Standing => !Done;
        public Vector2 Center => Rect.center;

        public override void Init()
        {
            whole = Load(Str("sprite", "VFX/props/warden_statue_0"));
            broken = Load(Str("sprite_broken", ""));
            foreach (var p in Str("sprite_damaged", "").Split(',')) { var s = Load(p.Trim()); if (s != null) damaged.Add(s); }
            sr = TrapArt.MakeRenderer(Ctx.Host.transform, "Statue " + Name, new Vector3(Rect.center.x, Rect.yMin + 0.02f, 0f), Ctx.Player, 0);
            sr.sprite = whole;
            var go = new GameObject("StatueCollider " + Name);
            go.transform.SetParent(Ctx.Host.transform, true);
            go.transform.position = Rect.center;
            col = go.AddComponent<BoxCollider2D>();
            col.size = Rect.size;
            MapEncounterDirector.ZoneReset += OnZoneReset;
        }

        internal static Sprite Load(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            var all = Resources.LoadAll<Sprite>(path);
            if (all.Length == 0) { Debug.LogWarning("[WardenStatue] Không thấy sprite: " + path); return null; }
            return all[0];
        }

        public override void Animate()
        {
            if (Done) return;
            // đạn nổ sát tượng = trúng tượng (giống bàn lật CoverMechanism)
            var area = new Rect(Rect.xMin - 0.1f, Rect.yMin - 0.1f, Rect.width + 0.2f, Rect.height + 0.3f);
            foreach (var p in Projectile.Live)
            {
                if (p == null || !p.Exploding || counted.Contains(p)) continue;
                if (!area.Contains((Vector2)p.transform.position)) continue;
                counted.Add(p);
                Chip(1);
            }
            counted.RemoveWhere(p => p == null);
        }

        /// <summary>Nứt thêm `n` nấc (đạn, cối rơi sát). Hết nấc -> vỡ.</summary>
        public void Chip(int n)
        {
            if (Done) return;
            taken += n;
            int hits = Mathf.Max(2, Int("hits", 6));
            if (taken >= hits) { Shatter(); return; }
            Ctx.Run(Jiggle(0.12f, 0.02f));
            if (damaged.Count > 0)
            {
                int idx = Mathf.Clamp(taken * (damaged.Count + 1) / hits - 1, -1, damaged.Count - 1);
                if (idx >= 0 && sr != null) sr.sprite = damaged[idx];
            }
            Sfx.Play("break_stone", Rect.center, 0.35f);
        }

        /// <summary>Vỡ tung (Champion lao trúng / hết nấc): bụi + mảnh đá, chỉ còn bệ thấp (đạn bay qua).</summary>
        public void Shatter()
        {
            if (Done) return;
            Done = true;
            Sfx.Play("statue_shatter", Rect.center);
            var psr = Ctx.Player != null ? Ctx.Player.GetComponentInChildren<SpriteRenderer>() : null;
            if (psr != null) CutsceneFx.Burst(Ctx.Host, new Vector3(Rect.center.x, Rect.center.y + 0.3f, 0f), 0.4f, 0.35f, 7, 0, psr.sortingLayerID, psr.sortingOrder + 1);
            SpriteVfx.Spawn("m3_ground_burst", Rect.center, 1.1f, sr != null ? sr.sortingLayerID : 0, (sr != null ? sr.sortingOrder : 0) + 1, false, 16f, false);
            CameraFollow.KickMain(Vector2.down, 0.06f, 0.22f);
            if (sr != null)
            {
                if (broken != null) sr.sprite = broken; else sr.enabled = false;
            }
            if (col != null && col.GetComponent<TrapLowCover>() == null) col.gameObject.AddComponent<TrapLowCover>();   // bệ thấp: đạn bay qua
        }

        private void Restore()
        {
            Done = false; taken = 0; counted.Clear();
            if (sr != null) { sr.enabled = true; sr.sprite = whole; }
            var low = col != null ? col.GetComponent<TrapLowCover>() : null;
            if (low != null) Object.Destroy(low);
        }

        private void OnZoneReset(Rect zone)
        {
            if (Ctx.Host == null) { MapEncounterDirector.ZoneReset -= OnZoneReset; return; }
            if (zone.Contains(Rect.center)) Restore();
        }

        private IEnumerator Jiggle(float seconds, float amp)
        {
            if (sr == null) yield break;
            var t = sr.transform; Vector3 p0 = new Vector3(Rect.center.x, Rect.yMin + 0.02f, 0f);
            for (float e = 0f; e < seconds && t != null; e += Time.deltaTime)
            {
                t.position = p0 + new Vector3(Mathf.Sin(e * 90f) * amp * (1f - e / seconds), 0f, 0f);
                yield return null;
            }
            if (t != null) t.position = p0;
        }

        internal override void DebugSolve() { }
    }
}
