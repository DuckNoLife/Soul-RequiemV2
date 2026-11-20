using System.Collections;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 08/11 (§196): hình cổng CÓ HOẠT ẢNH (cửa xà lim kéo lên, cổng đầu lâu, cửa sắt / gỗ, cửa boss) thay cho kiểu cũ bật / tắt hình.
    /// Gắn vào object `gate_visual` có thuộc tính `anim` = tên dải khung trong Resources/VFX (khung 0 = ĐÓNG, khung cuối = MỞ;
    /// cắt bằng Catalog/v2/tools/traps196/slice_traps.py), `anim_fps` (mặc định 14). Tile object trong Tiled dùng ảnh khung 0
    /// (tileset SR-traps.tsx) nên đặt đâu thấy đúng đó. GateController gọi <see cref="SetClosed"/>; trong 1 giây đầu sau khi nạp scene thì đặt ngay, không chạy.
    /// </summary>
    public sealed class GateAnimator : MonoBehaviour
    {
        private Sprite[] frames;
        private float fps = 14f;
        private SpriteRenderer shown;
        private bool initialized;
        private int current;
        private Coroutine running;

        public void Setup(string anim, float framesPerSecond)
        {
            frames = TrapArt.Frames(anim);
            fps = Mathf.Max(1f, framesPerSecond);
        }

        private bool Prepare()
        {
            if (shown != null) return true;
            if (frames == null || frames.Length == 0) return false;
            var orig = GetComponentInChildren<SpriteRenderer>(true);
            if (orig == null) return false;
            // khớp theo MÉP TRÊN-TRÁI hình trên map (trim_props có thể xén dải trống dưới chân hình trong Tiled, mép trên thì giữ nguyên);
            // khung hoạt ảnh có pivot dưới-trái -> đặt chân khung = mép trên - chiều cao khung
            Vector3 bl = orig.transform.position;
            if (orig.sprite != null)
            {
                Vector2 pv = orig.sprite.pivot / orig.sprite.pixelsPerUnit;
                float hOrig = orig.sprite.rect.height / orig.sprite.pixelsPerUnit;
                float hFrame = frames[0].rect.height / frames[0].pixelsPerUnit;
                bl = orig.transform.TransformPoint(new Vector3(-pv.x, -pv.y + hOrig - hFrame, 0f));
            }
            var g = new GameObject("GateAnim");
            g.transform.SetParent(transform, true);
            g.transform.position = new Vector3(bl.x, bl.y, orig.transform.position.z);
            shown = g.AddComponent<SpriteRenderer>();
            shown.sortingLayerID = orig.sortingLayerID;
            shown.sortingOrder = orig.sortingOrder;
            shown.spriteSortPoint = orig.spriteSortPoint;
            shown.sharedMaterial = orig.sharedMaterial;
            shown.color = orig.color;
            shown.flipX = orig.flipX;
            orig.enabled = false;
            shown.sprite = frames[0];
            return true;
        }

        public void SetClosed(bool closed)
        {
            if (!Prepare()) { gameObject.SetActive(closed); return; }
            int target = closed ? 0 : frames.Length - 1;
            // lúc nạp map (trạng thái ban đầu / bản lưu) thì đặt ngay; về sau mới chạy hoạt ảnh
            if ((!initialized && Time.timeSinceLevelLoad < 1f) || !isActiveAndEnabled)
            {
                initialized = true;
                current = target;
                shown.sprite = frames[current];
                return;
            }
            if (running != null) StopCoroutine(running);
            running = StartCoroutine(Play(target));
        }

        private IEnumerator Play(int target)
        {
            float step = 1f / fps;
            while (current != target)
            {
                current += target > current ? 1 : -1;
                shown.sprite = frames[current];
                yield return new WaitForSeconds(step);
            }
            running = null;
        }
    }
}
