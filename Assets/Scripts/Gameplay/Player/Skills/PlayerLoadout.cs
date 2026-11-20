using System;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Bộ chiêu đang gắn của người chơi: SlotCount ô (phím Skill1/2/3), danh sách chiêu đã có (owned), hồi chiêu từng ô.
    ///   - Thư viện chiêu = mọi SkillDefinition trong Resources/Skills (không có asset nào -> tự tạo Ground Slam mặc định).
    ///   - Đổi chiêu: Equip(slot, skill) — chỉ cho phép khi đang nghỉ ở checkpoint (RequiemMenus kiểm tra).
    ///   - Qua map: GameSession giữ id chiêu đã có + bộ đang gắn.
    /// Tự gắn vào người chơi (PlayerCombat.Awake).
    /// </summary>
    public class PlayerLoadout : MonoBehaviour
    {
        public const int SlotCount = 3;

        public SkillDefinition[] slots = new SkillDefinition[SlotCount];
        public List<SkillDefinition> owned = new List<SkillDefinition>();

        private readonly float[] readyAt = new float[SlotCount];
        private PlayerCombat combat;
        private static SkillDefinition[] library;

        public event Action Changed;
        /// Ô vừa dùng chiêu (HUD làm hiệu ứng nhấn).
        public event Action<int> Cast;

        public static SkillDefinition[] Library
        {
            get
            {
                if (library != null && library.Length > 0) return library;
                library = Resources.LoadAll<SkillDefinition>("Skills");
                if (library == null || library.Length == 0)
                {
                    var g = ScriptableObject.CreateInstance<GroundSlamSkill>();
                    g.id = "ground_slam"; g.displayName = "Earthshatter"; g.iconName = "ic_skill_slam";
                    g.description = "Stamp the earth. A ring of force strikes and throws back every foe around you.";
                    library = new SkillDefinition[] { g };
                }
                return library;
            }
        }

        public static SkillDefinition Find(string id)
        {
            foreach (var s in Library) if (s != null && s.id == id) return s;
            return null;
        }

        /// <summary>Chiêu thuộc một bộ (setId khác rỗng).</summary>
        public static bool InSet(SkillDefinition s) => s != null && !string.IsNullOrEmpty(s.setId);

        /// <summary>Mọi chiêu trong thư viện cùng bộ với <paramref name="s"/>.</summary>
        public static List<SkillDefinition> SetOf(SkillDefinition s)
        {
            var list = new List<SkillDefinition>();
            if (!InSet(s)) return list;
            foreach (var x in Library) if (x != null && x.setId == s.setId) list.Add(x);
            return list;
        }

        /// <summary>04/11: chiêu đứng đầu bộ (setSlot nhỏ nhất) — giữ tên / mô tả của cả bộ.</summary>
        public static SkillDefinition SetLeader(SkillDefinition s)
        {
            SkillDefinition best = null;
            foreach (var m in SetOf(s)) if (best == null || m.setSlot < best.setSlot) best = m;
            return best;
        }

        /// <summary>04/11: tên hiển thị của bộ (setName của chiêu đầu bộ; trống thì "Mantra set").</summary>
        public static string SetName(SkillDefinition s)
        {
            var l = SetLeader(s);
            return l != null && !string.IsNullOrEmpty(l.setName) ? l.setName : "Mantra set";
        }

        public static PlayerLoadout Of(Component c)
        {
            var l = c.GetComponent<PlayerLoadout>();
            return l != null ? l : c.gameObject.AddComponent<PlayerLoadout>();
        }

        private void Awake()
        {
            combat = GetComponent<PlayerCombat>();
            if (owned.Count == 0)
                foreach (var s in Library) if (s != null && s.unlockedAtStart) owned.Add(s);
            bool any = false;
            foreach (var s in slots) if (s != null) any = true;
            if (!any && owned.Count > 0) slots[0] = owned[0];
        }

        private void Start() => GameSession.ApplyLoadout(this);

        public SkillDefinition SkillAt(int i) => i >= 0 && i < SlotCount ? slots[i] : null;
        public float CooldownLeft(int i) => SkillAt(i) != null ? Mathf.Max(0f, readyAt[i] - Time.time) : 0f;
        public float CooldownTotal(int i) => SkillAt(i) != null ? Mathf.Max(0.01f, SkillAt(i).cooldown * CdMul) : 1f;
        private float CdMul => WeaponTraits.CooldownMul(combat != null ? combat : GetComponent<PlayerCombat>());
        public bool Owns(SkillDefinition s) => s != null && owned.Contains(s);

        /// Bấm phím ô `i`: kiểm tra hồi chiêu / bị khoá rồi thi triển.
        public bool TryCast(int i)
        {
            var s = SkillAt(i);
            if (s != null && combat != null && s.TryStop(combat)) return true;   // 07/11: bấm lại = tắt chiêu đang duy trì
            if (s == null || combat == null || Time.time < readyAt[i] || combat.IsAttacking) return false;
            var st = GetComponent<PlayerStatus>();
            if (st != null && st.Silenced)
            {
                Hints.Show("Your mantras are sealed!", 1.2f);
                return false;
            }
            if (st != null && st.MantrasBound) { st.SayRestraint(st.MantraSealText); return false; }   // 17/11 (§306): phong theo map
            readyAt[i] = Time.time + s.cooldown * CdMul;                        // 16/11 (§257): nội tại Namebound
            int slot = i;
            combat.StartCoroutine(s.Cast(combat, () => readyAt[slot] = Time.time));
            Cast?.Invoke(i);
            SfxHooks.Mantra(s, combat.transform.position); // 19/11 (§335): âm
            return true;
        }

        /// Gắn chiêu vào ô (chiêu đã ở ô khác thì đổi chỗ). null = tháo.
        public void Equip(int slot, SkillDefinition s)
        {
            if (slot < 0 || slot >= SlotCount) return;
            if (s != null && !Owns(s)) return;
            // Bộ chiêu: lắp một chiêu = lắp cả bộ, mỗi chiêu vào đúng ô setSlot của nó (chiếm cả 3 ô)
            if (InSet(s))
            {
                for (int i = 0; i < SlotCount; i++) slots[i] = null;
                foreach (var m in SetOf(s))
                    if (Owns(m) && m.setSlot >= 0 && m.setSlot < SlotCount) slots[m.setSlot] = m;
                Changed?.Invoke();
                return;
            }
            // Ô đang thuộc một bộ: tháo cả bộ trước khi lắp chiêu lẻ / tháo
            if (InSet(slots[slot]))
            {
                string set = slots[slot].setId;
                for (int i = 0; i < SlotCount; i++) if (slots[i] != null && slots[i].setId == set) slots[i] = null;
            }
            int other = Array.IndexOf(slots, s);
            if (s != null && other >= 0 && other != slot)
            {
                slots[other] = slots[slot];
                (readyAt[other], readyAt[slot]) = (readyAt[slot], readyAt[other]);
            }
            slots[slot] = s;
            Changed?.Invoke();
        }

        /// <summary>07/11 (§183): hoàn <paramref name="fraction"/> thời gian hồi còn lại của chiêu <paramref name="s"/> (vd Riposte đỡ trúng).</summary>
        public void Refund(SkillDefinition s, float fraction)
        {
            int i = Array.IndexOf(slots, s);
            if (i < 0 || fraction <= 0f) return;
            float left = Mathf.Max(0f, readyAt[i] - Time.time);
            readyAt[i] = Time.time + left * (1f - Mathf.Clamp01(fraction));
        }

        public void Unlock(SkillDefinition s, bool autoEquip = true)
        {
            if (s == null) return;
            if (InSet(s))                                  // mở khoá một chiêu của bộ = mở cả bộ (và lắp luôn cả bộ)
            {
                bool added = false;
                foreach (var m in SetOf(s)) if (!owned.Contains(m)) { owned.Add(m); added = true; }
                if (!added) return;
                if (autoEquip) Equip(0, s); else Changed?.Invoke();
                return;
            }
            if (owned.Contains(s)) return;
            owned.Add(s);
            if (autoEquip)
                for (int i = 0; i < SlotCount; i++)
                    if (slots[i] == null) { slots[i] = s; break; }
            Changed?.Invoke();
        }

        public void Unlock(string id) => Unlock(Find(id));

        public void NotifyChanged() => Changed?.Invoke();
    }
}
