using System.Collections;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// tank — bể kính (Culture Hall): cóc Gorgemaw đập xuống gần (<see cref="MapMechanics.Shatter"/>) -> ẩn `obj`, hiện `broken`.
    /// 03/11: bể vỡ -> bật cờ `sets` (mặc định &lt;tên&gt;_broken) và Ichor tràn ra thành vũng `spill` (object Tiled, lan dần từ chân bể).
    /// Vùng rát đi kèm = cơ quan hazard có `start_when` = cờ trên (ẩn / vô hại tới khi bể vỡ). Vũng Ichor cũng là chỗ lũ cóc con chui lên
    /// ở pha 2 (<see cref="SpillPoint"/>). Nạp lại bản lưu đã có cờ: vỡ sẵn, vũng hiện sẵn.
    /// </summary>
    public sealed class TankMechanism : Mechanism
    {
        public override void Init()
        {
            Ctx.Show(Str("broken", ""), false);
            Ctx.Show(Str("spill", ""), false);
            OnFlagsChanged();
        }

        public override void OnFlagsChanged()
        {
            if (Done || !Ctx.Flags.Has(Flag)) return;
            Done = true;                                            // bản lưu / cờ bật từ nơi khác: vỡ sẵn, không hiệu ứng
            Ctx.Show(Str("obj", ""), false);
            Ctx.Show(Str("broken", ""), true);
            Ctx.Show(Str("spill", ""), true);
        }

        private string Flag => Str("sets", Name + "_broken");

        /// <summary>Tâm vũng Ichor (chỗ cóc con chui lên) — tâm hình `spill` nếu có, không thì tâm vùng bể.</summary>
        internal Vector2 SpillPoint
        {
            get
            {
                foreach (var go in Ctx.NamedObjects(Str("spill", "")))
                {
                    var r = go != null ? go.GetComponentInChildren<SpriteRenderer>(true) : null;
                    if (r != null) return r.bounds.center;
                }
                return Rect.center;
            }
        }

        /// <summary>Vỡ nếu cú đập (tâm at, bán kính r) chạm bể. Trả về true nếu vỡ.</summary>
        internal bool TryShatter(Vector2 at, float r)
        {
            if (Done || !Overlaps(at, r)) return false;
            Done = true;
            SfxHooks.MechanismEvent(this, "break", "glass_break"); // 19/11 (§335): âm
            Ctx.Show(Str("obj", ""), false);
            Ctx.Show(Str("broken", ""), true);
            Ctx.Run(Spill());
            return true;
        }

        /// <summary>Ichor tràn: vũng lan từ chân bể (co -> giãn), bật cờ khi đã loang đủ (vùng rát bắt đầu cắn).</summary>
        private IEnumerator Spill()
        {
            // tile object của Tiled có gốc ở góc dưới-trái -> phóng to quanh TÂM hình (bù vị trí), vũng loang đều ra mọi phía
            var list = Ctx.NamedObjects(Str("spill", ""));
            int n = list.Count;
            var scales = new Vector3[n]; var pos = new Vector3[n]; var off = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                if (list[i] == null) continue;
                list[i].SetActive(true);
                var tr = list[i].transform;
                scales[i] = tr.localScale; pos[i] = tr.position;
                var r = list[i].GetComponentInChildren<SpriteRenderer>(true);
                off[i] = r != null ? r.bounds.center - tr.position : Vector3.zero;
            }
            for (float t = 0f; t <= 0.7f; t += Time.deltaTime)
            {
                float k = Mathf.Lerp(0.2f, 1f, 1f - (1f - t / 0.7f) * (1f - t / 0.7f));
                for (int i = 0; i < n; i++)
                {
                    if (list[i] == null) continue;
                    var tr = list[i].transform;
                    tr.localScale = new Vector3(scales[i].x * k, scales[i].y * k, scales[i].z);
                    tr.position = pos[i] + off[i] * (1f - k);
                }
                if (t > 0.25f && !Ctx.Flags.Has(Flag)) Ctx.Flags.Set(Flag);
                yield return null;
            }
            for (int i = 0; i < n; i++)
                if (list[i] != null) { list[i].transform.localScale = scales[i]; list[i].transform.position = pos[i]; }
            if (!Ctx.Flags.Has(Flag)) Ctx.Flags.Set(Flag);
        }
    }
}
