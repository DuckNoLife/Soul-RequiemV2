using System.Collections.Generic;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 19/11 (§345): API cho song đấu Reliquary Wardens (The Warden Gallery, Map 4): mốc theo tên (duo_gallery / duo_landing / duo_arena),
    /// tượng che chắn (warden_statue) và thùng thuốc nổ (powder_keg).
    /// </summary>
    public partial class MapMechanics
    {
        /// <summary>Khung (đơn vị Unity) của cơ quan / mốc trong lớp mechanics theo tên Tiled; null nếu không có.</summary>
        public Rect? MechRect(string name)
        {
            foreach (var m in mechs) if (m.Name == name) return m.Rect;
            return null;
        }

        public IEnumerable<T> MechsOf<T>() where T : Mechanism
        {
            foreach (var m in mechs) if (m is T t) yield return t;
        }

        /// <summary>20/11 (§349): mọi giá vũ khí (relic_rack) của map.</summary>
        public List<RelicRackMechanism> Racks()
        {
            var l = new List<RelicRackMechanism>();
            foreach (var m in mechs) if (m is RelicRackMechanism r) l.Add(r);
            return l;
        }

        /// <summary>20/11 (§350): mọi cột pha lê (crystal_pillar) của map.</summary>
        public List<CrystalPillarMechanism> Crystals()
        {
            var l = new List<CrystalPillarMechanism>();
            foreach (var m in mechs) if (m is CrystalPillarMechanism c) l.Add(c);
            return l;
        }

        /// <summary>Châm mọi thùng còn nguyên trong bán kính r (cối rơi, Slam, nổ dây chuyền).</summary>
        public int IgniteKegs(Vector2 at, float r, float fuse)
        {
            int n = 0;
            foreach (var m in mechs)
                if (m is PowderKegMechanism k && k.Ready && (k.Center - at).sqrMagnitude <= r * r) { k.Ignite(fuse); n++; }
            return n;
        }

        /// <summary>Tượng đứng trong bán kính r nứt thêm `chips` nấc.</summary>
        public void ChipStatues(Vector2 at, float r, int chips)
        {
            foreach (var m in mechs)
                if (m is WardenStatueMechanism s && s.Standing && m.Overlaps(at, r)) s.Chip(chips);
        }

        /// <summary>Tượng còn đứng mà vòng tròn (at, r) chạm vào (Champion lao trúng).</summary>
        public WardenStatueMechanism StatueHit(Vector2 at, float r)
        {
            foreach (var m in mechs)
                if (m is WardenStatueMechanism s && s.Standing && m.Overlaps(at, r)) return s;
            return null;
        }

        /// <summary>Đường bắn thẳng a -> b có bị tượng còn đứng chắn không (khe hở `pad` quanh chân tượng).</summary>
        public bool StatueBlocks(Vector2 a, Vector2 b, float pad = 0.12f)
        {
            foreach (var m in mechs)
            {
                if (!(m is WardenStatueMechanism s) || !s.Standing) continue;
                var r = m.Rect;
                var box = new Rect(r.xMin - pad, r.yMin - pad, r.width + pad * 2f, r.height + pad * 2f);
                for (int i = 1; i < 16; i++) if (box.Contains(Vector2.Lerp(a, b, i / 16f))) return true;
            }
            return false;
        }

        /// <summary>Thùng còn nguyên gần `from` nhất trong bán kính maxR (Champion đi nhặt).</summary>
        public PowderKegMechanism NearestKeg(Vector2 from, float maxR)
        {
            PowderKegMechanism best = null; float bd = maxR * maxR;
            foreach (var m in mechs)
                if (m is PowderKegMechanism k && k.Ready)
                {
                    float d = (k.Center - from).sqrMagnitude;
                    if (d < bd) { bd = d; best = k; }
                }
            return best;
        }
    }
}
