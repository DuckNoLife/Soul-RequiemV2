using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Hiệu ứng sprite chạy một lần rồi tự huỷ (VFX làm bằng PixelLab VFX Workbench).
    /// Ảnh nằm ở Assets/Resources/VFX/&lt;tên&gt;.png, cắt sẵn thành các khung &lt;tên&gt;_0, _1, ... (pivot = điểm neo của hiệu ứng).
    ///   slash_E / slash_SE / slash_S / slash_NE / slash_N : vệt chém Iaido (hướng phía tây = lật X)
    ///   hit            : trúng đòn trên quái (chớp sáng + tia lửa + máu)
    ///   ground_impact  : chiêu F đập đất (vòng sóng + nứt đất)
    /// </summary>
    public class SpriteVfx : MonoBehaviour
    {
        private static readonly Dictionary<string, Sprite[]> cache = new Dictionary<string, Sprite[]>();

        private Sprite[] frames;
        private float frameTime;
        private float t;
        private SpriteRenderer sr;
        private bool loop;
        private float endAt;

        /// Nạp các khung của một hiệu ứng (theo thứ tự số ở cuối tên). Trả về mảng rỗng nếu thiếu ảnh.
        public static Sprite[] Load(string name)
        {
            if (cache.TryGetValue(name, out var s)) return s;
            var all = Resources.LoadAll<Sprite>("VFX/" + name);
            var list = new List<Sprite>(all);
            list.Sort((a, b) => Index(a.name).CompareTo(Index(b.name)));
            s = list.ToArray();
            if (s.Length == 0) Debug.LogWarning($"[VFX] Không tìm thấy Resources/VFX/{name}.png");
            cache[name] = s;
            return s;
        }

        private static int Index(string n)
        {
            int u = n.LastIndexOf('_');
            return u >= 0 && int.TryParse(n.Substring(u + 1), out int i) ? i : 0;
        }

        /// Tạo hiệu ứng tại vị trí (pivot của ảnh đặt đúng vào pos). scale = tỉ lệ so với 100 px / đơn vị.
        public static SpriteVfx Spawn(string name, Vector2 pos, float scale, int sortingLayerId, int sortingOrder,
                                      bool flipX = false, float fps = 16f)
        {
            var f = Load(name);
            if (f.Length == 0) return null;
            SfxHooks.Vfx(name, pos);   // 17/11 (§294): âm
            var go = new GameObject("vfx_" + name);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            go.transform.localScale = new Vector3(scale, scale, 1f);
            var r = go.AddComponent<SpriteRenderer>();
            FxLighting.MakeUnlit(r);
            r.sortingLayerID = sortingLayerId;
            r.sortingOrder = sortingOrder;
            r.flipX = flipX;
            r.sprite = f[0];
            var v = go.AddComponent<SpriteVfx>();
            v.frames = f;
            v.frameTime = 1f / Mathf.Max(1f, fps);
            v.sr = r;
            return v;
        }

        /// Hiệu ứng lặp gắn theo một vật (vd sao choáng trên đầu nhân vật) trong `seconds` giây.
        public static SpriteVfx SpawnAttached(string name, Transform parent, float scale, int sortingLayerId, int sortingOrder,
                                              float fps, float seconds)
        {
            var v = Spawn(name, parent.position, scale, sortingLayerId, sortingOrder, false, fps);
            if (v == null) return null;
            v.transform.SetParent(parent, true);
            v.loop = true;
            v.endAt = Time.time + seconds;
            return v;
        }

        public void Stop() { if (this != null) Destroy(gameObject); }

        /// Nhuộm màu hiệu ứng (vd. vạch cảnh báo trắng -> đỏ máu).
        public SpriteVfx Tint(Color c) { if (sr != null) sr.color = c; return this; }

        /// Chuyển hiệu ứng (đã Spawn tại chỗ) sang lặp trong `seconds` giây, vd vũng máu trên đất.
        public SpriteVfx Looping(float seconds) { loop = true; endAt = Time.time + seconds; return this; }

        /// 07/11: hiệu ứng lặp còn ít nhất `seconds` giây nữa (dấu lộ sơ hở được làm mới mỗi nhịp vũng Ichor).
        public SpriteVfx Extend(float seconds) { if (loop) endAt = Mathf.Max(endAt, Time.time + seconds); return this; }

        /// Bám theo một vật (vẫn chạy 1 lần), vd hiệu ứng hồi máu đi theo nhân vật.
        public SpriteVfx Follow(Transform parent) { transform.SetParent(parent, true); return this; }

        private void Update()
        {
            t += Time.deltaTime;
            int i = Mathf.FloorToInt(t / frameTime);
            if (loop)
            {
                if (Time.time >= endAt) { Destroy(gameObject); return; }
                i %= frames.Length;
            }
            else if (i >= frames.Length) { Destroy(gameObject); return; }
            sr.sprite = frames[i];
        }
    }
}
