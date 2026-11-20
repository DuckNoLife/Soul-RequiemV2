using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 20/11 (§356): drill_horn — TÙ VÀ ĐỒNG trên cọc ở sân tập: Ren nhấn T ("Sound the horn") -> bắt đầu / chơi lại minigame
    /// của DrillYardMechanism tên `yard` (mặc định "drill_yard"). Vẽ hình VFX/props/drill_horn (chân ảnh ở đáy vùng object) + va chạm nhỏ ở chân.
    /// §360: minigame có checkpoint theo trạm -> thổi lại thì chơi tiếp từ trạm đã trượt (nhắc "Station N").
    /// §363: thổi = KÈN HIỆU quân sự (âm `drill_horn`, PixelLab: 4 nốt kèn đồng vang sân đá, ngân dài) + tù và rung 1,4 s; minigame chờ 1,1 s cho tiếng kèn rồi mới vào trạm.
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
            Sfx.Play("drill_horn", Rect.center);                                     // §363: kèn hiệu
            if (y.Checkpoint > 0 || y.RoundCheckpoint > 0) Hints.Show("Resuming the drill at Station " + (y.Checkpoint + 1) + ", round " + (y.RoundCheckpoint + 1) + ".", 2.5f);   // §371
            Ctx.Run(Toot());
            y.Begin();
        }

        private System.Collections.IEnumerator Toot()
        {
            if (sr == null) yield break;
            const float Len = 1.4f;                                                    // §363: rung theo tiếng kèn (dài hơn)
            for (float t = 0f; t < Len; t += Time.deltaTime)
            {
                float k = Mathf.Sin(t * 40f) * 0.05f * (1f - t / Len);
                sr.transform.localScale = new Vector3(1f + k, 1f - k, 1f);
                yield return null;
            }
            sr.transform.localScale = Vector3.one;
        }

        internal override void DebugSolve() { }
    }
}
