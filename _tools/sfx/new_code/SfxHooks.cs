using System;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 17/11 (§294): chỗ chọn âm cho các sự kiện chung của gameplay (gọi từ Health, SpriteVfx, MapMechanics...),
    /// để code từng hệ chỉ cần một dòng. Danh sách âm: _tools/sfx/sfx_manifest.json.
    /// Cơ quan trên map có thể ghi đè âm bằng thuộc tính Tiled `sfx` (id âm, hoặc "none" để im).
    /// </summary>
    public static class SfxHooks
    {
        /// <summary>Tên quái mặc giáp / xương / thân cứng -> tiếng kim loại thay vì tiếng chém thịt.</summary>
        private static readonly string[] Armored =
            { "Guard", "Knight", "Skeleton", "Undead", "BL_", "Minotaur", "Ram", "Ent", "Vat_Born", "Mimic" };

        /// <summary>VFX (tiền tố tên) -> âm đi kèm. Khớp theo tiền tố để gồm cả hậu tố hướng (_E/_S/_N) và khung.</summary>
        private static readonly KeyValuePair<string, string>[] VfxSfx =
        {
            new KeyValuePair<string, string>("lg_slam", "boss_slam"),
            new KeyValuePair<string, string>("m3_lk_slam", "boss_slam"),
            new KeyValuePair<string, string>("ground_impact", "boss_slam"),
            new KeyValuePair<string, string>("m3_ground_burst", "boss_slam"),
            new KeyValuePair<string, string>("lg_break", "lg_shield_break"),
            new KeyValuePair<string, string>("lg_thrust", "lg_spear_thrust"),
            new KeyValuePair<string, string>("lg_whirl_front", "lg_whirl"),
            new KeyValuePair<string, string>("lg_stun_front", "lg_wall_crash"),
            new KeyValuePair<string, string>("lg_spark", "hit_block"),
            new KeyValuePair<string, string>("m3_blood_splash", "st_bleed"),
            new KeyValuePair<string, string>("m3_root_spike", "st_bind"),
            new KeyValuePair<string, string>("sk_bound", "st_bind"),
            new KeyValuePair<string, string>("sk_vuln", "st_mark"),
        };

        /// <summary>Loại cơ quan (Tiled Type) -> âm khi nhấn T.</summary>
        private static readonly Dictionary<string, string> UseSfx = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "lever", "lever" }, { "valve", "lever" }, { "console", "lever" }, { "sluice", "lever" },
            { "cipher", "dial_turn" }, { "dial", "dial_turn" }, { "rosewindow", "dial_turn" },
            { "passage", "stone_door" }, { "cover", "stone_door" },
            { "chest", "pickup_item" }, { "note", "pickup_note" },
        };

        // ------------------------------------------------------------------ máu
        public static void Hit(Health h)
        {
            if (h == null) return;
            Vector2 p = h.transform.position;
            if (h.IsPlayer) Sfx.Play("pl_hurt", p);
            else Sfx.Play(IsArmored(h) ? "hit_armor" : "hit_flesh", p);
        }

        public static void Died(Health h)
        {
            if (h == null) return;
            if (h.IsPlayer) { Sfx.Play2D("pl_death"); return; }
            var ai = h.GetComponent<EnemyAI>();
            Sfx.Play(ai != null && ai.IsBoss ? "boss_death" : "en_death", h.transform.position);
        }

        public static void Blocked(Health h) { if (h != null) Sfx.Play("hit_block", h.transform.position); }

        public static void Parried(Health h) { if (h != null) Sfx.Play("parry", h.transform.position); }

        private static bool IsArmored(Health h)
        {
            string n = h.gameObject.name;
            foreach (var k in Armored)
                if (n.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        // ------------------------------------------------------------------ VFX / đạn
        public static void Vfx(string name, Vector2 pos)
        {
            if (string.IsNullOrEmpty(name)) return;
            foreach (var kv in VfxSfx)
                if (name.StartsWith(kv.Key, StringComparison.Ordinal)) { Sfx.Play(kv.Value, pos); return; }
        }

        /// <summary>Đạn bắn ra: tên có arrow / bolt / spike / spear -> tiếng dây cung, còn lại -> tiếng phép.</summary>
        public static void Launched(Component projectile)
        {
            if (projectile == null) return;
            string n = projectile.gameObject.name.ToLowerInvariant();
            bool physical = n.Contains("arrow") || n.Contains("bolt") || n.Contains("spike") || n.Contains("spear") || n.Contains("dart");
            Sfx.Play(physical ? "arrow_shot" : "proj_magic", projectile.transform.position);
        }

        // ------------------------------------------------------------------ cơ quan trên map
        public static void MechanismUsed(Mechanism m)
        {
            if (m == null) return;
            string id = m.Props != null ? m.Props.GetString("sfx", "") : "";
            if (id == "none") return;
            if (string.IsNullOrEmpty(id) && !UseSfx.TryGetValue(m.Type ?? "", out id)) return;
            Sfx.Play(id, m.Rect.center);
        }

        /// <summary>Toggle bật (ẩn / hiện vật khi đủ cờ): mặc định kêu tiếng cổng sắt nếu vật bị ẩn là song sắt / cổng / cửa.</summary>
        public static void Toggled(Mechanism m)
        {
            if (m == null || m.Props == null) return;
            string id = m.Props.GetString("sfx", "");
            if (id == "none") return;
            if (string.IsNullOrEmpty(id))
            {
                string what = (m.Name + " " + m.Props.GetString("hide_objects", "")).ToLowerInvariant();
                if (what.Contains("grate") || what.Contains("gate") || what.Contains("door") || what.Contains("bars")) id = "gate_open";
                else return;
            }
            Sfx.Play(id, m.Rect.center);
        }
    }
}
