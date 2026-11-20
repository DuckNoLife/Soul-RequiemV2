using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 20/11 (§360): hiệu ứng dùng chung của Burlap's Proving Grounds —
    ///   Warn()    VẠCH BÁO: hàng mũi tên đỏ (drill/fx_arrow) dọc một đoạn, chớp nhanh dần rồi tắt đúng lúc đòn tới. Mọi đòn trong
    ///             minigame đều báo trước (người chơi trúng đòn = lỗi của mình, không phải "bị chơi xấu").
    ///   Impact()  cảm giác BỊ ĐÁNH: tia rơm + sao đánh (drill/fx_hit), khựng hình 0,06 s, rung màn theo hướng đòn.
    ///   HitStop() khựng hình (như PlayerStrikes: timeScale 0.05 rồi trả về, không đè lên lúc tạm dừng game).
    /// </summary>
    internal static class DrillFx
    {
        private static bool stopping;
        private static float stopAt;

        /// <summary>Hàng mũi tên từ `from` tới `to` (chỉ theo hướng đi), sống `seconds` giây. `gapFrom..gapTo` (0..1 dọc đoạn) = khe an toàn
        /// (mũi tên ở đó màu sáng, nhỏ) — để trống thì cả đoạn đỏ.</summary>
        public static void Warn(MonoBehaviour host, Vector2 from, Vector2 to, float seconds, Vector2 pointDir, Transform player,
                                float spacing = 0.34f, float gapFrom = -1f, float gapTo = -1f)
        {
            var frames = SpriteVfx.Load("drill/fx_arrow");
            if (frames.Length == 0 || host == null) return;
            var list = new List<SpriteRenderer>();
            float len = (to - from).magnitude;
            int n = Mathf.Max(1, Mathf.FloorToInt(len / spacing) + 1);
            float ang = Mathf.Atan2(pointDir.y, pointDir.x) * Mathf.Rad2Deg;
            for (int i = 0; i < n; i++)
            {
                float u = n == 1 ? 0.5f : i / (float)(n - 1);
                bool gap = gapFrom >= 0f && u >= gapFrom && u <= gapTo;
                var sr = TrapArt.MakeRenderer(host.transform, "DrillWarn", Vector2.Lerp(from, to, u), player, -5);
                FxLighting.MakeUnlit(sr);
                sr.sprite = frames[0];
                sr.transform.rotation = Quaternion.Euler(0f, 0f, ang);
                sr.transform.localScale = gap ? new Vector3(0.55f, 0.55f, 1f) : Vector3.one;
                sr.color = gap ? new Color(0.55f, 1f, 0.6f, 0.0f) : new Color(1f, 1f, 1f, 0f);
                list.Add(sr);
            }
            host.StartCoroutine(Blink(list, frames, seconds));
        }

        private static IEnumerator Blink(List<SpriteRenderer> list, Sprite[] frames, float seconds)
        {
            float t0 = Time.time;
            while (Time.time - t0 < seconds)
            {
                float u = (Time.time - t0) / seconds;
                float hz = Mathf.Lerp(3f, 11f, u);                                      // chớp nhanh dần
                bool on = Mathf.Repeat((Time.time - t0) * hz, 1f) < 0.6f;
                float a = Mathf.Clamp01(u * 6f);                                         // hiện dần 0,15 s đầu
                for (int i = 0; i < list.Count; i++)
                {
                    var sr = list[i];
                    if (sr == null) continue;
                    var c = sr.color; c.a = (on ? 0.95f : 0.45f) * a; sr.color = c;
                    if (frames.Length > 1) sr.sprite = frames[on ? 1 : 0];
                    // sóng chạy dọc hàng mũi tên (chỉ hướng)
                    float k = 1f + 0.12f * Mathf.Sin(Time.time * 14f - i * 0.9f);
                    if (sr.transform.localScale.x > 0.7f) sr.transform.localScale = new Vector3(k, k, 1f);
                }
                yield return null;
            }
            foreach (var sr in list) if (sr != null) Object.Destroy(sr.gameObject);
        }

        /// <summary>Đòn trúng người chơi: tia rơm + khựng hình + rung màn (hướng = từ vật đánh tới người chơi).</summary>
        public static void Impact(MonoBehaviour host, Vector2 at, Vector2 dir, int layer, int order, float shake = 0.09f)
        {
            SpriteVfx.Spawn("drill/fx_hit", at + Vector2.up * 0.15f, 1f, layer, order + 2, dir.x < 0f, 22f, false);
            CameraFollow.KickMain(dir, shake, 0.22f);
            HitStop(host, 0.07f);
        }

        public static void HitStop(MonoBehaviour host, float seconds)
        {
            if (stopping && Time.unscaledTime - stopAt > 1f) stopping = false;                 // host bị huỷ giữa chừng (đổi scene)
            if (host == null || stopping || Time.timeScale < 0.99f) return;
            host.StartCoroutine(Stop(seconds));
        }

        private static IEnumerator Stop(float seconds)
        {
            stopping = true; stopAt = Time.unscaledTime;
            Time.timeScale = 0.05f;
            yield return new WaitForSecondsRealtime(seconds);
            if (Mathf.Approximately(Time.timeScale, 0.05f)) Time.timeScale = 1f;
            stopping = false;
        }
    }
}
