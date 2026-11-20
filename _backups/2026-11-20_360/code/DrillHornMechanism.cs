using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 20/11 (§356): drill_horn — TÙ VÀ ĐỒNG trên cọc ở sân tập: Ren nhấn T ("Sound the horn") -> bắt đầu / chơi lại minigame
    /// của DrillYardMechanism tên `yard` (mặc định "drill_yard"). Vẽ hình VFX/props/drill_horn (chân ảnh ở đáy vùng object) + va chạm nhỏ ở chân.
    /// </summary>
    public sealed class DrillHornMechanism : Mechanism
    {
        private DrillYardMechanism yard;
        private SpriteRenderer sr;

        public override string DefaultVerb => "Sound the horn";
        public override bool Usable => Yard != null && (Yard.Ready || Yard.Passed);

        private DrillYardMechanism Yard
        {
            get
            {
                if (yard != null) return yard;
                string n = Str("yard", "drill_yard");
                foreach (var m in Ctx.All) if (m is DrillYardMechanism d && d.Name == n) { yard = d; break; }
                return yard;
            }
        }

        public override void Init()
        {
            sr = TrapArt.MakeRenderer(Ctx.Host.transform, "Drill Horn", new Vector3(Rect.center.x, Rect.yMin, 0f), Ctx.Player, 0);
            sr.sprite = WardenStatueMechanism.Load("VFX/props/drill_horn");
            var go = new GameObject("DrillHornCollider");
            go.transform.SetParent(Ctx.Host.transform, true);
            go.transform.position = new Vector3(Rect.center.x, Rect.yMin + 0.06f, 0f);
            go.AddComponent<BoxCollider2D>().size = new Vector2(0.22f, 0.12f);
        }

        public override void Use()
        {
            var y = Yard;
            if (y == null) return;
            if (y.Passed) { Hints.Show("The horn is silent. The Sergeant already signed your papers.", 2f); return; }
            if (!y.Ready) return;
            Sfx.Play("rf_war_drum", Rect.center);
            Ctx.Run(Toot());
            y.Begin();
        }

        private System.Collections.IEnumerator Toot()
        {
            if (sr == null) yield break;
            for (float t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                float k = Mathf.Sin(t * 40f) * 0.05f * (1f - t / 0.5f);
                sr.transform.localScale = new Vector3(1f + k, 1f - k, 1f);
                yield return null;
            }
            sr.transform.localScale = Vector3.one;
        }

        internal override void DebugSolve() { }
    }
}
