using System.Collections.Generic;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// API for the Reliquary Wardens duo fight (The Warden Gallery, Map 4): named markers (duo_gallery / duo_landing / duo_arena),
    /// cover statues (warden_statue) and powder kegs (powder_keg).
    /// </summary>
    public partial class MapMechanics
    {
        /// <summary>Box (Unity units) of a mechanism / marker in the mechanics layer by Tiled name; null if missing.</summary>
        public Rect? MechRect(string name)
        {
            foreach (var m in mechs) if (m.Name == name) return m.Rect;
            return null;
        }

        public IEnumerable<T> MechsOf<T>() where T : Mechanism
        {
            foreach (var m in mechs) if (m is T t) yield return t;
        }

        /// <summary>Every weapon rack (relic_rack) of the map.</summary>
        public List<RelicRackMechanism> Racks()
        {
            var l = new List<RelicRackMechanism>();
            foreach (var m in mechs) if (m is RelicRackMechanism r) l.Add(r);
            return l;
        }

        /// <summary>Every crystal pillar (crystal_pillar) of the map.</summary>
        public List<CrystalPillarMechanism> Crystals()
        {
            var l = new List<CrystalPillarMechanism>();
            foreach (var m in mechs) if (m is CrystalPillarMechanism c) l.Add(c);
            return l;
        }

        /// <summary>Light every intact keg within radius r (mortars, Slam, chain explosions).</summary>
        public int IgniteKegs(Vector2 at, float r, float fuse)
        {
            int n = 0;
            foreach (var m in mechs)
                if (m is PowderKegMechanism k && k.Ready && (k.Center - at).sqrMagnitude <= r * r) { k.Ignite(fuse); n++; }
            return n;
        }

        /// <summary>Standing statues within radius r crack `chips` more steps.</summary>
        public void ChipStatues(Vector2 at, float r, int chips)
        {
            foreach (var m in mechs)
                if (m is WardenStatueMechanism s && s.Standing && m.Overlaps(at, r)) s.Chip(chips);
        }

        /// <summary>A standing statue touched by circle (at, r) (Champion charge hit).</summary>
        public WardenStatueMechanism StatueHit(Vector2 at, float r)
        {
            foreach (var m in mechs)
                if (m is WardenStatueMechanism s && s.Standing && m.Overlaps(at, r)) return s;
            return null;
        }

        /// <summary>Is the straight shot a -> b blocked by a standing statue (gap `pad` around the statue base).</summary>
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

        /// <summary>Intact keg nearest to `from` within radius maxR (the Champion goes to pick it up).</summary>
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
