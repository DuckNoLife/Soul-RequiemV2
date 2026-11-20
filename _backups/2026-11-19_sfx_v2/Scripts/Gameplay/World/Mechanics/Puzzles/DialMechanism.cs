using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// dial — một vòng số của khoá (nhóm `group`, thứ tự `index`, bắt đầu `start`): mỗi lần nhấn T +1 (0-9).
    /// Mọi vòng trong nhóm đúng `answer` -> cả nhóm Done, cờ `sets`, chữ `text`.
    /// </summary>
    public sealed class DialMechanism : Mechanism
    {
        private int value;
        private bool announced;

        public override string DefaultVerb => "Turn";
        private string Group => Str("group", "");

        public override void Init() => value = Int("start", 0);

        public override void Tick(Vector2 playerPos, bool inside)
        {
            bool near = Vector2.Distance(playerPos, Rect.center) < 0.7f;
            if (near && !announced) { announced = true; Hints.Show("A brass dial. " + DialText(), 3f); }
            if (!near) announced = false;
        }

        public override void Use()
        {
            if (Done) return;
            value = (value + 1) % 10;
            var group = GroupMembers();
            if (group.TrueForAll(g => g.value == g.Int("answer", 0)))
            {
                foreach (var g in group) g.Done = true;
                Hints.Show(Str("text", "The dials click into place."), 3f);
                Ctx.Flags.Set(Str("sets", Name));
            }
            else Hints.Show(DialText(), 1.5f);
        }

        private List<DialMechanism> GroupMembers()
        {
            var list = new List<DialMechanism>();
            foreach (var m in Ctx.All) if (m is DialMechanism d && d.Group == Group) list.Add(d);
            return list;
        }

        private string DialText()
        {
            var group = GroupMembers();
            group.Sort((a, b) => a.Int("index", 0).CompareTo(b.Int("index", 0)));
            return "The dials read " + string.Join(" - ", group.ConvertAll(g => g.value.ToString())) + ".";
        }
    }
}
