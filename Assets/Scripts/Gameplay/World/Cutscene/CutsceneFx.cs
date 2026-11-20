using System.Collections;
using System.Collections.Generic;
using SuperTiled2Unity;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 03/11: hiệu ứng nhỏ dùng chung cho các cảnh diễn (không phải MonoBehaviour — chạy bằng StartCoroutine của cảnh gọi).
    ///   Puff   : một cụm bụi đá (Resources/VFX/landing/landing_dust_0..5, màu đá xám lạnh của hầm) tan dần.
    ///   Debris : một viên sỏi (landing_pebble_1..5) văng vòng cung rồi nằm lại, mờ dần.
    ///   Jiggle : rung một object (cửa, nồi hơi, bồn) tại chỗ.
    ///   Toggle : bật / tắt các object Tiled theo tên (phân tách bằng dấu phẩy).
    /// </summary>
    public static class CutsceneFx
    {
        private static Sprite[] dust;
        private static readonly List<Sprite> pebbles = new List<Sprite>();

        private static bool Load()
        {
            if (dust == null)
            {
                var d = new Sprite[6];
                for (int i = 0; i < 6; i++) d[i] = Resources.Load<Sprite>("VFX/landing/landing_dust_" + i);
                dust = System.Array.Exists(d, s => s == null) ? new Sprite[0] : d;
                pebbles.Clear();
                for (int i = 1; i <= 5; i++) { var s = Resources.Load<Sprite>("VFX/landing/landing_pebble_" + i); if (s != null) pebbles.Add(s); }
            }
            return dust.Length > 0;
        }

        private static Vector3 Snap(Vector3 p) => new Vector3(Mathf.Round(p.x * 100f) / 100f, Mathf.Round(p.y * 100f) / 100f, p.z);

        public static IEnumerator Puff(Vector3 p, int layer, int order, float fps = 14f, float delay = 0f)
        {
            if (!Load()) yield break;
            if (delay > 0f) yield return new WaitForSeconds(delay);
            var sr = new GameObject("Cutscene Dust").AddComponent<SpriteRenderer>();
            sr.transform.position = Snap(p);
            sr.flipX = Random.value < 0.5f;
            sr.sortingLayerID = layer; sr.sortingOrder = order;
            for (float t = 0f; sr != null; t += Time.deltaTime)
            {
                int f = (int)(t * fps);
                if (f >= dust.Length) break;
                sr.sprite = dust[f];
                yield return null;
            }
            if (sr != null) Object.Destroy(sr.gameObject);
        }

        public static IEnumerator Debris(Vector3 from, Vector3 to, float arc, float dur, int layer, int order)
        {
            if (!Load() || pebbles.Count == 0) yield break;
            var sr = new GameObject("Cutscene Debris").AddComponent<SpriteRenderer>();
            SoulRequiem.Core.Sfx.Play2D("rock_fall", 0.6f); // 19/11 (§337): âm
            sr.sprite = pebbles[Random.Range(0, pebbles.Count)];
            sr.flipX = Random.value < 0.5f;
            sr.color = new Color(0.8f, 0.82f, 0.8f);
            sr.sortingLayerID = layer; sr.sortingOrder = order;
            for (float t = 0f; t < dur && sr != null; t += Time.deltaTime)
            {
                float u = t / dur;
                sr.transform.position = Snap(Vector3.Lerp(from, to, u) + Vector3.up * (arc * 4f * u * (1f - u)));
                yield return null;
            }
            if (sr == null) yield break;
            sr.transform.position = Snap(to);
            sr.sortingOrder = order - 4;                                 // nằm trên mặt đất
            yield return new WaitForSeconds(1.4f);
            for (float t = 0f; t < 0.6f && sr != null; t += Time.deltaTime)
            {
                sr.color = new Color(0.8f, 0.82f, 0.8f, 1f - t / 0.6f);
                yield return null;
            }
            if (sr != null) Object.Destroy(sr.gameObject);
        }

        /// <summary>Bụi bật thành vòng elip quanh `c` + đá văng ra (cú nện, cửa vỡ, tiếp đất).</summary>
        public static void Burst(MonoBehaviour host, Vector3 c, float rx, float ry, int puffs, int debris, int layer, int order)
        {
            for (int i = 0; i < puffs; i++)
            {
                float a = i / (float)puffs * Mathf.PI * 2f + Random.Range(-0.25f, 0.25f);
                host.StartCoroutine(Puff(c + new Vector3(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry, 0f), layer, order + 1, Random.Range(10f, 15f), Random.Range(0f, 0.1f)));
            }
            for (int i = 0; i < debris; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                var to = c + new Vector3(Mathf.Cos(a) * Random.Range(rx, rx * 1.8f), Mathf.Sin(a) * Random.Range(ry, ry * 1.8f), 0f);
                host.StartCoroutine(Debris(c, to, Random.Range(0.15f, 0.35f), Random.Range(0.25f, 0.4f), layer, order + 2));
            }
        }

        public static IEnumerator Jiggle(Transform t, float seconds, float amp)
        {
            if (t == null) yield break;
            Vector3 p0 = t.position;
            for (float e = 0f; e < seconds && t != null; e += Time.deltaTime)
            {
                float k = 1f - e / seconds;
                t.position = p0 + new Vector3(Random.Range(-amp, amp) * k, Random.Range(-amp, amp) * 0.4f * k, 0f);
                yield return null;
            }
            if (t != null) t.position = p0;
        }

        public static Transform Find(Transform mapRoot, string name)
        {
            if (mapRoot == null || string.IsNullOrEmpty(name)) return null;
            foreach (var so in mapRoot.GetComponentsInChildren<SuperObject>(true))
                if (so.m_TiledName == name) return so.transform;
            return null;
        }

        public static void Toggle(Transform mapRoot, string names, bool show)
        {
            if (mapRoot == null || string.IsNullOrEmpty(names)) return;
            var set = new HashSet<string>();
            foreach (var n in names.Split(',')) if (n.Trim().Length > 0) set.Add(n.Trim());
            foreach (var so in mapRoot.GetComponentsInChildren<SuperObject>(true))
                if (set.Contains(so.m_TiledName)) so.gameObject.SetActive(show);
        }

        /// <summary>Thuộc tính "x,y" (toạ độ ô Tiled) -> toạ độ thế giới; không có / sai thì trả `fallback`.</summary>
        public static Vector3 TileProp(MapEncounterDirector dir, SuperCustomProperties p, string key, Vector3 fallback)
        {
            if (p == null || dir == null) return fallback;
            var xy = p.GetString(key, "").Split(',');
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            var st = System.Globalization.NumberStyles.Float;
            if (xy.Length == 2 && float.TryParse(xy[0], st, ci, out float x) && float.TryParse(xy[1], st, ci, out float y))
            {
                var w = dir.TileToWorld(x, y);
                return new Vector3(w.x, w.y, fallback.z);
            }
            return fallback;
        }
    }
}
