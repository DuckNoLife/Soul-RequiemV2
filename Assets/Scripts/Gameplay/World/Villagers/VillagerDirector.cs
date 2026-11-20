using System.Collections;
using System.Collections.Generic;
using System.Linq;
using SuperTiled2Unity;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 01/11: đọc lớp "npcs" của Tiled, tạo <see cref="Villager"/>. Mỗi object hình chữ nhật = vùng đi lại (hoặc chỗ đứng).
    /// Thuộc tính: kind (thư mục Resources/NPCs/&lt;kind&gt;: idle_E_NN, idle_S_NN, idle_N_NN, walk_E/S/N_NN; bản cũ idle_NN = east),
    /// walk (bool, mặc định true), lines (các câu, ngăn bằng '|'), show_when / hide_when (cờ thế giới),
    /// face (E/W/S/N, mặc định S; face_left cũ = W), speed (đv/giây, mặc định 0.55), scale (mặc định 0.7 = như người chơi).
    /// MapEncounterDirector tự gắn component này khi map có lớp "npcs".
    /// </summary>
    public sealed class VillagerDirector : MonoBehaviour
    {
        private struct Entry { public Villager v; public string showWhen, hideWhen; public Vector2[] enterPath; public Vector2 home, face;
                               public string[] baseLines; public List<AltLines> alt; }
        /// <summary>16/11 (§260): một bộ lời thay thế theo cờ (Tiled `lines_N_when` / `lines_N_unless` / `lines_N`).</summary>
        private class AltLines { public string when = "", unless = ""; public string[] lines; }
        private bool started;
        private readonly List<Entry> all = new List<Entry>();
        private static readonly Dictionary<string, Dictionary<string, Sprite[]>> cache = new Dictionary<string, Dictionary<string, Sprite[]>>();

        private IEnumerator Start()
        {
            yield return null;                                       // đợi Director đọc map
            var dir = MapEncounterDirector.Instance;
            if (dir == null || dir.mapRoot == null) yield break;
            var pc = FindFirstObjectByType<PlayerController>();
            var reference = pc != null ? pc.GetComponent<SpriteRenderer>() : null;

            foreach (var so in dir.mapRoot.GetComponentsInChildren<SuperObject>(true))
            {
                if (so.transform.parent == null || so.transform.parent.name != "npcs") continue;
                var p = so.GetComponent<SuperCustomProperties>();
                string kind = p.GetString("kind", "");
                var art = Load(kind);
                if (art.Count == 0) { Debug.LogWarning($"[Villagers] Thiếu ảnh Resources/NPCs/{kind} ({so.m_TiledName})"); continue; }

                var col = so.GetComponent<Collider2D>();
                Rect area = col != null ? new Rect(col.bounds.min, col.bounds.size) : new Rect(so.transform.position, Vector2.one * 0.2f);
                foreach (var c in so.GetComponentsInChildren<Collider2D>(true)) c.enabled = false;   // vùng đi lại không chặn ai

                var go = new GameObject("NPC " + (string.IsNullOrEmpty(so.m_TiledName) ? kind : so.m_TiledName));
                go.transform.localScale = Vector3.one * p.GetFloat("scale", 0.7f);   // không làm con: tỉ lệ đúng thế giới
                go.transform.position = new Vector3(area.center.x, area.walkable() ? area.center.y : area.yMin, reference != null ? reference.transform.position.z : 0f);
                var v = go.AddComponent<Villager>();
                v.anims = art; v.area = area;
                v.walker = p.GetBool("walk", true) && art.Keys.Any(k => k.StartsWith("walk_"));
                v.speed = p.GetFloat("speed", 0.55f);
                string face = p.GetBool("face_left", false) ? "W" : p.GetString("face", "S").ToUpperInvariant();
                v.facing = face == "E" ? Vector2.right : face == "W" ? Vector2.left : face == "N" ? Vector2.up : Vector2.down;
                string lines = p.GetString("lines", "");
                v.lines = string.IsNullOrEmpty(lines) ? null : lines.Split('|').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
                v.speaker = p.GetString("speaker", Pretty(so.m_TiledName));
                v.Init(reference);
                var ent = new Entry { v = v, showWhen = p.GetString("show_when", ""), hideWhen = p.GetString("hide_when", ""),
                                    enterPath = ParsePath(dir, p.GetString("enter_from", "")), home = go.transform.position, face = v.facing,
                                    alt = new List<AltLines>() };
                // 16/11 (§260): lời theo cờ — `lines_N_when` (+ `lines_N_unless`) + `lines_N`, N = 1..6; bộ có N lớn nhất đủ điều kiện thắng
                ent.baseLines = v.lines;
                for (int n = 1; n <= 6; n++)
                {
                    string ln = p.GetString("lines_" + n, "");
                    if (ln.Length == 0) continue;
                    ent.alt.Add(new AltLines { when = p.GetString("lines_" + n + "_when", ""), unless = p.GetString("lines_" + n + "_unless", ""),
                                               lines = ln.Split('|').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray() });
                }
                all.Add(ent);
            }
            dir.Flags.Changed += _ => Refresh(dir.Flags);
            Refresh(dir.Flags);
            started = true;
            Debug.Log($"[Villagers] {all.Count} dân làng.");
        }

        private void Refresh(WorldFlags flags)
        {
            foreach (var e in all)
            {
                if (e.v == null) continue;
                if (e.alt.Count > 0)                                                   // 16/11 (§260): chọn bộ lời theo cờ
                {
                    string[] pick = e.baseLines;
                    foreach (var a in e.alt)
                        if ((a.when.Length == 0 || flags.HasAll(a.when)) && (a.unless.Length == 0 || !a.unless.Split(',').Any(f => f.Trim().Length > 0 && flags.Has(f.Trim())))) pick = a.lines;
                    e.v.lines = pick;
                }
                bool on = (string.IsNullOrEmpty(e.showWhen) || flags.HasAll(e.showWhen)) &&
                          (string.IsNullOrEmpty(e.hideWhen) || !flags.HasAll(e.hideWhen));
                if (e.v.gameObject.activeSelf == on) continue;
                e.v.gameObject.SetActive(on);
                // 02/11: hiện ra giữa lúc chơi + có `enter_from` -> đi vào từ đó (cửa nhà, mép làng) thay vì bật ra tại chỗ
                if (on && started && e.enterPath != null) StartCoroutine(Enter(e));
            }
        }

        /// <summary>Tên Tiled -> tên hiện: "farm_hand" -> "Farm Hand" (map đặt `speaker` để đổi).</summary>
        private static string Pretty(string n)
        {
            if (string.IsNullOrEmpty(n)) return "";
            var parts = n.Split('_');
            for (int i = 0; i < parts.Length; i++)
                if (parts[i].Length > 0) parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1);
            return string.Join(" ", parts);
        }

        /// <summary>"x,y;x,y;..." (ô Tiled): điểm xuất hiện rồi các điểm đi qua; cuối cùng về chỗ đứng của mình.</summary>
        private static Vector2[] ParsePath(MapEncounterDirector dir, string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            var pts = new List<Vector2>();
            foreach (var part in s.Split(';'))
            {
                var xy = part.Split(',');
                if (xy.Length == 2 &&
                    float.TryParse(xy[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x) &&
                    float.TryParse(xy[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y))
                    pts.Add(dir.TileToWorld(x, y));
            }
            return pts.Count > 0 ? pts.ToArray() : null;
        }

        private IEnumerator Enter(Entry e)
        {
            var v = e.v;
            yield return null;                                       // sau SetActive (Villager đã chạy lại)
            if (v == null) yield break;
            v.Warp(e.enterPath[0]);
            for (int i = 1; i <= e.enterPath.Length; i++)
            {
                Vector2 to = i < e.enterPath.Length ? e.enterPath[i] : e.home;
                v.WalkTo(to);
                float give = Time.time + Vector2.Distance(v.transform.position, to) / Mathf.Max(0.1f, v.speed) + 3f;
                while (v != null && v.gameObject.activeInHierarchy && !v.Arrived && Time.time < give) yield return null;
                if (v == null) yield break;
            }
            v.EndScript();
            v.facing = e.face;
        }

        /// <summary>Resources/NPCs/&lt;kind&gt;: "idle_E_03" -> khoá "idle_E"; tên cũ "idle_03" -> "idle_E".</summary>
        private static Dictionary<string, Sprite[]> Load(string kind)
        {
            if (cache.TryGetValue(kind, out var a)) return a;
            a = new Dictionary<string, Sprite[]>();
            var groups = new Dictionary<string, List<Sprite>>();
            foreach (var sp in Resources.LoadAll<Sprite>("NPCs/" + kind))
            {
                var parts = sp.name.Split('_');
                if (parts.Length < 2) continue;
                string key = parts.Length >= 3 ? parts[0] + "_" + parts[1] : parts[0] + "_E";
                if (!groups.TryGetValue(key, out var l)) groups[key] = l = new List<Sprite>();
                l.Add(sp);
            }
            foreach (var kv in groups) a[kv.Key] = kv.Value.OrderBy(x => x.name).ToArray();
            cache[kind] = a;
            return a;
        }
    }

    internal static class RectExt
    {
        /// <summary>Vùng đủ rộng để đi lại (&gt; ~1 ô map = 0.32 đv) -> đặt ở giữa; vùng nhỏ = chỗ đứng -> đặt ở đáy vùng (chân).</summary>
        public static bool walkable(this Rect r) => r.width > 0.35f || r.height > 0.35f;
    }
}
