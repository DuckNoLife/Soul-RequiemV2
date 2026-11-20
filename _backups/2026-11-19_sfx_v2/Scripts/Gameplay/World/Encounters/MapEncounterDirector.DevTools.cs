#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Lệnh test của director (gọi từ DevTools/DevCheats: F3 hạ hết quái, F4 / Shift+F4 / Ctrl+F4 dịch chuyển).
    /// Phần "partial" này chỉ được biên dịch trong Editor / Development Build — bản phát hành không có.
    /// </summary>
    public partial class MapEncounterDirector
    {
        /// <summary>(F3) Giết mọi quái đang sống, hang coi như ra hết quái — kiểm tra luồng đợt / mở cổng.</summary>
        public void DebugKillAll()
        {
            foreach (var e in FindObjectsByType<EnemyAI>(FindObjectsSortMode.None))
                if (e != null && !e.Health.IsDead) e.Health.TakeDamage(99999f);
            foreach (var z in layout.Zones.Values)
                foreach (var c in z.caves) if (c != null) c.DebugFinish();
            Hints.Show("[TEST] All enemies defeated", 1.5f);
        }

        /// <summary>(F4) Sang bước mạch chính kế tiếp (khu có thuộc tính `story`); map chưa đánh dấu -> khu kế theo thứ tự Tiled.</summary>
        public void DebugNextZone() => DebugStepZone(1);

        private int storyCursor = -1;                       // bước F4 vừa nhảy tới (khu dùng lại nhiều bước, vd r8 = 2.2 và 5)

        private struct StoryStep { public EncounterZone zone; public float order; public string done; }

        /// <summary>07/11: các bước mạch chính sắp theo số `story` (bỏ khu phục kích / khu phụ — không có `story`).</summary>
        private List<StoryStep> StorySteps()
        {
            var list = new List<StoryStep>();
            foreach (var z in layout.Zones.Values)
                for (int i = 0; i < z.story.Length; i++)
                    list.Add(new StoryStep { zone = z, order = z.story[i], done = i < z.storyDone.Length ? z.storyDone[i] : "" });
            list.Sort((a, b) => a.order.CompareTo(b.order));
            return list;
        }

        /// <summary>(F4 / Shift+F4) Nhảy `step` bước mạch chính. Tới nơi thì bật cờ "đã xong" của MỌI bước trước đó (cửa, chìa, nhiệm vụ,
        /// boss khớp như người chơi đã đi thật) và mở cổng của các khu đó. Map chưa có `story` -> nhảy khu theo thứ tự Tiled như cũ.</summary>
        public void DebugStepZone(int step)
        {
            var steps = StorySteps();
            if (steps.Count == 0) { StepZoneByTiledOrder(step); return; }
            Vector2 p = player.position;
            int cur = storyCursor >= 0 && storyCursor < steps.Count && steps[storyCursor].zone.rect.Contains(p) ? storyCursor : -1;
            if (cur < 0) for (int i = 0; i < steps.Count; i++) if (steps[i].zone.rect.Contains(p)) { cur = i; break; }
            int next = Mathf.Clamp(cur < 0 ? (step > 0 ? 0 : steps.Count - 1) : cur + step, 0, steps.Count - 1);
            storyCursor = next;
            for (int i = 0; i < next; i++)
            {
                foreach (var f in steps[i].done.Split(','))
                    if (f.Trim().Length > 0) Flags.Set(f.Trim());
                foreach (var g in gateCtl.All) if (g.zoneId == steps[i].zone.id && !g.closeOnEnter) gateCtl.Open(g);
            }
            var r = steps[next].zone.rect;
            player.position = FindFreeSpot(r.center, r, player.position.z);
            Hints.Show($"[TEST] Story {steps[next].order:0.0#} - {steps[next].zone.title}  ({next + 1}/{steps.Count})", 2f);
        }

        /// <summary>Map chưa đánh dấu cốt truyện: nhảy khu theo thứ tự tạo trong Tiled (bỏ khu phụ), mở cổng các khu đứng trước.</summary>
        private void StepZoneByTiledOrder(int step)
        {
            var zones = layout.Zones;
            var ids = new List<string>();
            foreach (var id in layout.ZoneOrder) if (!zones[id].optional) ids.Add(id);
            ids.Sort((a, b) => zones[a].order.CompareTo(zones[b].order));
            if (ids.Count == 0) { Hints.Show("[TEST] Map has no zones (logic layers hidden in Tiled?)", 2.5f); return; }
            int cur = -1;
            for (int i = 0; i < ids.Count; i++) if (zones[ids[i]].rect.Contains((Vector2)player.position)) cur = i;
            int next = Mathf.Clamp(cur < 0 && step < 0 ? 0 : cur + step, 0, ids.Count - 1);
            for (int i = 0; i < next; i++)
                foreach (var g in gateCtl.All) if (g.zoneId == ids[i] && !g.closeOnEnter) gateCtl.Open(g);
            var r = zones[ids[next]].rect;
            player.position = FindFreeSpot(r.center, r, player.position.z);
            Hints.Show($"[TEST] Teleported to {zones[ids[next]].title}", 1.5f);
        }

        /// <summary>(Ctrl+F4) Dịch chuyển tới ngay cạnh lối ra đầu tiên (phía dưới 1.2 ô) để thử chuyển map.</summary>
        public void DebugGoToExit()
        {
            foreach (var t in triggerHandler.All)
            {
                if (t.type != MapSchema.ObjectType.Exit) continue;
                var c = t.rect.center + new Vector2(0f, -t.rect.height * 0.5f - tileSize * 1.2f);
                player.position = FindFreeSpot(c, new Rect(c.x - 3f, c.y - 3f, 6f, 6f), player.position.z);
                Hints.Show(triggerHandler.ExitOpen(t) ? "[TEST] At the exit - step in" : "[TEST] At the exit (still sealed - press F3 in the boss zone)", 2f);
                return;
            }
            Hints.Show("[TEST] This map has no exit", 1.5f);
        }

        /// <summary>Chỗ trống gần tâm (không đè collider rắn) để dịch chuyển không nhốt người chơi trong vật cản.</summary>
        private Vector3 FindFreeSpot(Vector2 c, Rect r, float z)
        {
            for (float rad = 0f; rad < Mathf.Max(r.width, r.height); rad += 0.32f)
            {
                int steps = rad <= 0f ? 1 : Mathf.Max(8, Mathf.CeilToInt(rad * 12f));
                for (int s = 0; s < steps; s++)
                {
                    float a = s * Mathf.PI * 2f / steps;
                    var p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rad;
                    if (!r.Contains(p)) continue;
                    bool blocked = false;
                    foreach (var col in Physics2D.OverlapCircleAll(p, 0.3f))
                        if (!col.isTrigger && !col.transform.IsChildOf(player)) { blocked = true; break; }
                    if (!blocked) return new Vector3(p.x, p.y, z);
                }
            }
            return new Vector3(c.x, c.y, z);
        }
    }
}
#endif
