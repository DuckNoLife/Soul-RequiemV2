using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using SuperTiled2Unity;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 16/11 (§237): cảnh cốt truyện viết bằng dữ liệu Tiled, không cần một class C# cho mỗi cảnh.
    ///
    /// Đặt một object trong lớp "triggers", type = "story". Thuộc tính:
    ///   trigger = enter (mặc định: Ren bước vào hình chữ nhật / bán kính `radius`) | interact (nhấn T, chữ `verb`, `range`)
    ///             | flag (tự chạy khi đủ cờ `when`) | start (chạy ngay khi vào map).
    ///   when    = cờ cần có ("a,b", "a|b" = một trong hai). unless = cờ chặn (có MỘT trong các cờ "a,b" là không chạy).
    ///   once    = cờ đánh dấu đã diễn (mặc định "scene_&lt;tên object&gt;"); repeat = true -> diễn lại mỗi lần (interact).
    ///   script  = các bước, mỗi dòng một lệnh (trong Tiled dùng \n):
    ///     cam X,Y [zoom]        lia camera tới ô (X,Y) (zoom nhân cỡ khung, mặc định 1)
    ///     camren                trả camera về Ren
    ///     wait S                chờ S giây
    ///     say NGƯỜI: câu|câu    khung hội thoại, chờ người chơi đọc xong ('|' = trang mới)
    ///     narrate câu|câu       khung hội thoại không tên, tự sang trang
    ///     bubble AI: câu        bóng thoại trên đầu (AI = ren | tên NPC trong lớp npcs | X,Y), chờ đọc xong
    ///     hint câu              dòng gợi ý dưới màn hình
    ///     shake BIÊN GIÂY       rung camera
    ///     beat X,Y #màu N       N nhịp tim: quầng sáng đập + rung nhẹ tại ô (X,Y)
    ///     glow X,Y #màu R GIÂY  quầng sáng (không chờ)
    ///     show tên,tên / hide tên,tên   bật / tắt object Tiled
    ///     set cờ                bật cờ
    ///     face X,Y              Ren quay mặt về ô đó
    ///     walk NPC X,Y          NPC đi tới ô, chờ tới nơi
    ///     fade out GIÂY / fade in GIÂY  màn đen
    ///     slide ẢNH: câu|câu    tranh toàn màn hình (Resources/ẢNH) + lời kể; slide end = tắt tranh
    ///     exit TÊN              rời map qua lối ra TÊN (lớp triggers, type exit)
    ///   18/11 (§327) thêm:
    ///     ren TƯ_THẾ            Ren diễn: draw / sheathe / stagger / fall (ngã gục, nằm yên) / getup
    ///     ren warp X,Y          đặt Ren tới ô (X,Y) — dùng lúc màn đen (cắt cảnh)
    ///     warp NPC X,Y          đặt NPC tới ô (X,Y)
    ///     look NPC X,Y          NPC quay mặt về ô
    ///     play NPC ẢNH [hold] [nowait] [fps=N]   NPC diễn bộ ảnh ẢNH_&lt;hướng&gt; một lần (hold = giữ khung cuối; nowait = không chờ)
    /// Cảnh cốt truyện không bỏ qua được (luật §229); khung hội thoại vẫn bấm để sang trang.
    /// Thêm: nghỉ ở điểm hồi sinh khi Con Mắt còn trong người -> một câu thì thầm (whispers).
    /// </summary>
    public class StoryScenes : MonoBehaviour
    {
        private class Scene
        {
            public string name, trigger, when, unless, once, script, verb;
            public bool repeat;
            public Rect rect;
            public float radius, range;
            public Interactable it;
        }

        public static bool Busy { get; private set; }
        public static StoryScenes Instance { get; private set; }

        private readonly List<Scene> scenes = new List<Scene>();
        private readonly Queue<Scene> queue = new Queue<Scene>();
        private MapEncounterDirector dir;
        private bool playing;
        private SpriteRenderer overlay, slide;
        private bool wasAtCheckpoint;
        private int whisperIndex;

        private static readonly string[] Whispers =
        {
            "...you could keep me. They would kneel to you.",
            "...the bells are calling us. Why do you not answer?",
            "...he gave everything away. Look where it got him.",
            "...you have no name. I could give you one.",
            "...every one of them took something. Why not you?",
            "...rest, then. I am not tired. I am never tired.",
        };

        private void Awake() { Instance = this; }
        private void OnDestroy() { if (Instance == this) { Instance = null; Busy = false; } }

        private IEnumerator Start()
        {
            yield return null;
            dir = MapEncounterDirector.Instance;
            if (dir == null || dir.mapRoot == null || dir.Flags == null) yield break;

            foreach (var so in dir.mapRoot.GetComponentsInChildren<SuperObject>(true))
            {
                if (so.m_Type != "story") continue;
                var p = so.GetComponent<SuperCustomProperties>();
                if (p == null) continue;
                var s = new Scene
                {
                    name = so.m_TiledName,
                    trigger = p.GetString("trigger", "enter").ToLowerInvariant(),
                    when = p.GetString("when", ""), unless = p.GetString("unless", ""),
                    once = p.GetString("once", "scene_" + so.m_TiledName),
                    script = p.GetString("script", ""), verb = p.GetString("verb", "Talk"),
                    repeat = p.GetBool("repeat", false),
                    radius = p.GetFloat("radius", 0f), range = p.GetFloat("range", 0.45f),
                    rect = RectOf(so),
                };
                foreach (var c in so.GetComponentsInChildren<Collider2D>(true)) c.enabled = false;
                scenes.Add(s);
                if (s.trigger == "interact")
                {
                    var sc = s;
                    s.it = InteractionSystem.Add(new Interactable
                    {
                        id = "story_" + s.name, verb = s.verb, clue = false, range = s.range, rect = s.rect,
                        anchor = new Vector2(s.rect.center.x, s.rect.yMax + 0.15f),
                        available = () => Ready(sc) && !playing,
                        use = () => Enqueue(sc),
                    });
                }
            }
            // cảnh "start" chiếm khoá ngay khung này để các cảnh mở đầu khác (OpeningCutscene) chờ
            foreach (var s in scenes) if (s.trigger == "start" && Ready(s)) { Enqueue(s); Busy = true; }
            Debug.Log($"[Story] {scenes.Count} cảnh trong map.");
        }

        private static Rect RectOf(SuperObject so)
        {
            var col = so.GetComponent<Collider2D>();
            if (col != null) return new Rect(col.bounds.min, col.bounds.size);
            return new Rect((Vector2)so.transform.position - Vector2.one * 0.08f, Vector2.one * 0.16f);
        }

        private bool Ready(Scene s)
        {
            var f = dir.Flags;
            if (!s.repeat && f.Has(s.once)) return false;
            if (!f.HasAll(s.when)) return false;
            if (s.unless.Length > 0)
                foreach (var u in s.unless.Split(',')) if (u.Trim().Length > 0 && f.HasAll(u.Trim())) return false;
            return true;
        }

        private void Enqueue(Scene s)
        {
            if (queue.Contains(s)) return;
            if (!s.repeat) dir.Flags.Set(s.once);
            queue.Enqueue(s);
        }

        private bool Calm => !CutsceneState.Active && !SceneTransition.Busy && !dir.Respawning && !TutorialPrompt.Active && !DialogueBox.Active;

        private void Update()
        {
            if (dir == null || dir.Flags == null) return;
            var player = dir.Player;
            if (!playing && queue.Count > 0 && (Calm || Busy)) { StartCoroutine(Play(queue.Dequeue())); return; }
            if (playing || player == null) return;

            Vector2 pos = player.position;
            foreach (var s in scenes)
            {
                if (s.trigger == "interact" || s.trigger == "start" || !Ready(s)) continue;
                bool go = s.trigger == "flag" ||
                          (s.trigger == "enter" && (s.radius > 0f ? (pos - s.rect.center).magnitude <= s.radius : s.rect.Contains(pos)));
                if (go && Calm && !dir.InCombat) { Enqueue(s); break; }
            }
            TickWhisper();
        }

        private void TickWhisper()
        {
            bool at = dir.AtCheckpoint;
            if (at && !wasAtCheckpoint && dir.Flags.Has("eye_relic_taken") && !dir.Flags.Has("eye_severed") && dir.Player != null)
            {
                SpeechBubble.Show(dir.Player.position + new Vector3(0f, 0.95f, 0f), Whispers[whisperIndex % Whispers.Length], 3.4f,
                                  new Color(0.62f, 1f, 0.6f), "The Eye").Follow(dir.Player);   // 17/11: phụ đề ghi rõ Con Mắt nói
                whisperIndex++;
            }
            wasAtCheckpoint = at;
        }

        // ------------------------------------------------------------------ chạy kịch bản
        private IEnumerator Play(Scene s)
        {
            playing = true; Busy = true;
            CutsceneState.Begin();
            var cam = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            float baseSize = Camera.main != null ? Camera.main.orthographicSize : 3f;
            float oldSharp = cam != null ? cam.focusSharpness : 6f;
            if (cam != null) cam.focusSharpness = 1.8f;
            var combat = dir.Player != null ? dir.Player.GetComponent<PlayerCombat>() : null;
            Debug.Log($"[Story] Cảnh '{s.name}'");

            string text = (s.script ?? "").Replace("\\n", "\n");
            foreach (var raw in text.Split('\n'))
            {
                GameInput.Blocked = true;
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                int sp = line.IndexOf(' ');
                string cmd = (sp < 0 ? line : line.Substring(0, sp)).ToLowerInvariant();
                string arg = sp < 0 ? "" : line.Substring(sp + 1).Trim();
                IEnumerator step = null;
                switch (cmd)
                {
                    case "cam":
                    {
                        var a = arg.Split(' ');
                        float zoom = a.Length > 1 ? F(a[1], 1f) : 1f;
                        if (cam != null && TryTile(a[0], out var w)) cam.Focus(w, baseSize * zoom);
                        break;
                    }
                    case "camren": if (cam != null) cam.Release(); break;
                    case "wait": step = Wait(F(arg, 1f)); break;
                    case "say": step = Say(arg, 0f); break;
                    case "narrate": step = Say(":" + arg, 3.4f); break;
                    case "bubble": step = Bubble(arg); break;
                    case "hint": Hints.Show(arg, 5f); break;
                    case "sfx": SoulRequiem.Core.Sfx.Play2D(arg.Trim()); break; // 19/11 (§335): âm — lệnh mới: sfx <id>
                    case "shake":
                    {
                        var a = arg.Split(' ');
                        SoulRequiem.Core.Sfx.Play2D("quake_rumble"); // 19/11 (§335): âm
                        step = Shake(a.Length > 0 ? F(a[0], 0.03f) : 0.03f, a.Length > 1 ? F(a[1], 0.6f) : 0.6f);
                        break;
                    }
                    case "beat": step = Beat(arg); break;
                    case "glow": StartCoroutine(Glow(arg)); break;
                    case "show": CutsceneFx.Toggle(dir.mapRoot, arg, true); break;
                    case "hide": CutsceneFx.Toggle(dir.mapRoot, arg, false); break;
                    case "set": foreach (var f in arg.Split(',')) dir.Flags.Set(f.Trim()); break;
                    case "face":
                        if (combat != null && dir.Player != null && TryTile(arg, out var fw)) combat.FacingOverride = (Vector2)(fw - dir.Player.position);
                        break;
                    case "walk": step = Walk(arg); break;
                    case "ren": step = RenCmd(arg, combat); break;
                    case "warp":
                    {
                        var a = arg.Split(' ');
                        var v = a.Length > 1 ? Npc(a[0]) : null;
                        if (v != null && TryTile(a[1], out var to)) v.Warp(to);
                        break;
                    }
                    case "look":
                    {
                        var a = arg.Split(' ');
                        var v = a.Length > 1 ? Npc(a[0]) : null;
                        if (v != null && TryTile(a[1], out var to)) v.FaceTo(to);
                        break;
                    }
                    case "play": step = PlayNpc(arg); break;
                    case "fade": step = Fade(arg); break;
                    case "slide": step = Slide(arg); break;
                    case "exit":
                        CleanUp(cam, oldSharp, combat);
                        dir.ExitNow(arg);
                        yield break;
                    default: Debug.LogWarning($"[Story] '{s.name}': lệnh lạ '{cmd}'"); break;
                }
                if (step != null) yield return step;
            }
            CleanUp(cam, oldSharp, combat);
        }

        private void CleanUp(CameraFollow cam, float oldSharp, PlayerCombat combat)
        {
            if (cam != null) { cam.Release(); cam.focusSharpness = oldSharp; }
            if (combat != null) combat.FacingOverride = Vector2.zero;
            SoulRequiem.Core.Sfx.Bed(null); // 19/11 (§336): hết cảnh / bị bỏ qua -> tắt nền dẫn chuyện
            if (slide != null) Destroy(slide.gameObject);
            if (overlay != null && overlay.color.a <= 0.01f) Destroy(overlay.gameObject);
            GameInput.Blocked = false;
            CutsceneState.End();
            playing = false;
            Busy = queue.Count > 0;
        }

        private IEnumerator Wait(float s) { for (float t = 0f; t < s; t += Time.deltaTime) yield return null; }

        private IEnumerator Say(string arg, float auto)
        {
            int c = arg.IndexOf(':');
            string who = c >= 0 ? arg.Substring(0, c).Trim() : "";
            string body = (c >= 0 ? arg.Substring(c + 1) : arg).Replace(" ~ ", "\n").Replace("~", "\n");   // 16/11: "~" = xuống dòng trong một trang (tiêu đề chương)
            bool closed = false;
            DialogueBox.Open(who, DialogueBox.Pages(body), () => closed = true, auto);
            while (!closed) { GameInput.Blocked = true; yield return null; }
            yield return null;
        }

        private IEnumerator Bubble(string arg)
        {
            int c = arg.IndexOf(':');
            if (c < 0) yield break;
            string who = arg.Substring(0, c).Trim();
            string body = arg.Substring(c + 1).Trim();
            Transform follow = null;
            Vector3 at;
            if (who.ToLowerInvariant() == "ren" && dir.Player != null) { follow = dir.Player; at = follow.position; }
            else if (TryTile(who, out var w)) at = w;
            else
            {
                var go = GameObject.Find("NPC " + who);
                if (go == null) { Debug.LogWarning($"[Story] không thấy NPC '{who}'"); yield break; }
                follow = go.transform; at = follow.position;
            }
            float secs = Mathf.Clamp(1.4f + body.Length * 0.045f, 2.2f, 6f);
            var b = SpeechBubble.Show(at + new Vector3(0f, 0.85f, 0f), body, secs);
            if (follow != null && b != null) b.Follow(follow);
            yield return Wait(secs);
        }

        private IEnumerator Shake(float amp, float secs)
        {
            float next = 0f;
            for (float t = 0f; t < secs; t += Time.deltaTime)
            {
                if (t >= next) { next = t + 0.09f; CameraFollow.KickMain(Random.insideUnitCircle, amp * (1f - t / secs), 0.14f); }
                yield return null;
            }
        }

        private IEnumerator Beat(string arg)
        {
            var a = arg.Split(' ');
            if (a.Length < 1 || !TryTile(a[0], out var at)) yield break;
            Color col = a.Length > 1 && ColorUtility.TryParseHtmlString(a[1], out var cc) ? cc : new Color(1f, 0.3f, 0.35f);
            int n = a.Length > 2 ? Mathf.Max(1, (int)F(a[2], 3f)) : 3;
            var l = MakeLight(at, col, 2.8f);
            for (int i = 0; i < n; i++)
            {
                for (float t = 0f; t < 0.85f; t += Time.deltaTime)
                {
                    float k = t < 0.12f ? t / 0.12f : Mathf.Clamp01(1f - (t - 0.12f) / 0.6f);
                    l.intensity = 2.2f * k;
                    yield return null;
                }
                CameraFollow.KickMain(Random.insideUnitCircle, 0.02f, 0.12f);
            }
            Destroy(l.gameObject);
        }

        private IEnumerator Glow(string arg)
        {
            var a = arg.Split(' ');
            if (a.Length < 1 || !TryTile(a[0], out var at)) yield break;
            Color col = a.Length > 1 && ColorUtility.TryParseHtmlString(a[1], out var cc) ? cc : Color.white;
            float r = a.Length > 2 ? F(a[2], 2f) : 2f, secs = a.Length > 3 ? F(a[3], 3f) : 3f;
            var l = MakeLight(at, col, r);
            for (float t = 0f; t < secs && l != null; t += Time.deltaTime)
            {
                l.intensity = 1.6f * Mathf.Clamp01(t / 0.4f) * Mathf.Clamp01((secs - t) / 0.6f);
                yield return null;
            }
            if (l != null) Destroy(l.gameObject);
        }

        private static Light2D MakeLight(Vector3 at, Color col, float radius)
        {
            var go = new GameObject("Story Glow");
            go.transform.position = at;
            var l = go.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point;
            l.pointLightOuterRadius = radius; l.pointLightInnerRadius = 0.2f;
            l.color = col; l.intensity = 0f;
            return l;
        }

        // ---- 18/11 (§327): lệnh diễn cho Ren / NPC
        private static Villager Npc(string who)
        {
            var go = GameObject.Find("NPC " + who);
            var v = go != null ? go.GetComponent<Villager>() : null;
            if (v == null) Debug.LogWarning($"[Story] không thấy NPC '{who}' (đã hiện chưa? xem show_when)");
            return v;
        }

        private IEnumerator RenCmd(string arg, PlayerCombat combat)
        {
            var a = arg.Split(' ');
            if (a.Length == 0 || dir.Player == null) yield break;
            if (a[0].ToLowerInvariant() == "warp")
            {
                if (a.Length < 2 || !TryTile(a[1], out var to)) yield break;
                var rb = dir.Player.GetComponent<Rigidbody2D>();
                dir.Player.position = new Vector3(to.x, to.y, dir.Player.position.z);
                if (rb != null) { rb.position = to; rb.linearVelocity = Vector2.zero; }
                yield break;
            }
            if (combat != null) combat.CutscenePose(a[0]);
        }

        private IEnumerator PlayNpc(string arg)
        {
            var a = arg.Split(' ');
            if (a.Length < 2) yield break;
            var v = Npc(a[0]);
            if (v == null) yield break;
            bool hold = false, nowait = false; float fps = 0f;
            for (int i = 2; i < a.Length; i++)
            {
                string o = a[i].ToLowerInvariant();
                if (o == "hold") hold = true;
                else if (o == "nowait") nowait = true;
                else if (o.StartsWith("fps=")) fps = F(o.Substring(4), 0f);
            }
            float secs = v.Play(a[1], hold, fps);
            if (!nowait && secs > 0f) yield return Wait(secs);
        }

        private IEnumerator Walk(string arg)
        {
            var a = arg.Split(' ');
            if (a.Length < 2) yield break;
            var go = GameObject.Find("NPC " + a[0]);
            var v = go != null ? go.GetComponent<Villager>() : null;
            if (v == null || !TryTile(a[1], out var to)) yield break;
            v.WalkTo(to);
            float give = Time.time + Vector2.Distance(v.transform.position, to) / Mathf.Max(0.1f, v.speed) + 3f;
            while (v != null && !v.Arrived && Time.time < give) yield return null;
            if (v != null) v.EndScript();
        }

        // ---- màn đen / tranh toàn màn hình (SpriteRenderer phủ khung camera, trên mọi lớp)
        private SpriteRenderer Cover(string name, int orderBoost)
        {
            var go = new GameObject(name);
            var sr = go.AddComponent<SpriteRenderer>();
            var layers = SortingLayer.layers;
            sr.sortingLayerID = layers[layers.Length - 1].id;
            sr.sortingOrder = 30000 + orderBoost;
            return sr;
        }

        private void FitToCamera(SpriteRenderer sr, bool cover)
        {
            var c = Camera.main;
            if (c == null || sr == null || sr.sprite == null) return;
            sr.transform.position = new Vector3(c.transform.position.x, c.transform.position.y, 0f);
            float h = c.orthographicSize * 2f, w = h * c.aspect;
            var b = sr.sprite.bounds.size;
            float k = cover ? Mathf.Max(w / b.x, h / b.y) : Mathf.Min(w / b.x, h / b.y);
            sr.transform.localScale = Vector3.one * k;
        }

        private void LateUpdate()
        {
            if (overlay != null) FitToCamera(overlay, true);
            if (slide != null) FitToCamera(slide, false);
        }

        private static Sprite white;
        private static Sprite White()
        {
            if (white != null) return white;
            var t = new Texture2D(4, 4); var px = new Color[16];
            for (int i = 0; i < 16; i++) px[i] = Color.white;
            t.SetPixels(px); t.Apply();
            white = Sprite.Create(t, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            return white;
        }

        private IEnumerator Fade(string arg)
        {
            var a = arg.Split(' ');
            bool outw = a.Length > 0 && a[0].ToLowerInvariant() == "out";
            float secs = a.Length > 1 ? F(a[1], 1f) : 1f;
            if (overlay == null) { overlay = Cover("Story Fade", 0); overlay.sprite = White(); overlay.color = new Color(0f, 0f, 0f, outw ? 0f : 1f); }
            FitToCamera(overlay, true);
            float from = overlay.color.a, to = outw ? 1f : 0f;
            for (float t = 0f; t < secs; t += Time.deltaTime)
            {
                overlay.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, t / secs));
                yield return null;
            }
            overlay.color = new Color(0f, 0f, 0f, to);
            if (!outw) { Destroy(overlay.gameObject); overlay = null; }
        }

        private IEnumerator Slide(string arg)
        {
            if (arg.Trim().ToLowerInvariant() == "end")
            {
                SoulRequiem.Core.Sfx.Bed(null); // 19/11 (§336)
                if (slide != null) yield return FadeSprite(slide, 1f, 0f, 0.8f);
                if (slide != null) Destroy(slide.gameObject);
                slide = null;
                yield break;
            }
            int c = arg.IndexOf(':');
            string path = (c >= 0 ? arg.Substring(0, c) : arg).Trim();
            string words = c >= 0 ? arg.Substring(c + 1).Trim() : "";
            var sp = Resources.Load<Sprite>(path);
            if (sp == null)
            {
                var tex = Resources.Load<Texture2D>(path);
                if (tex != null) { tex.filterMode = FilterMode.Point; sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f); }
            }
            if (sp != null)
            {
                if (overlay == null) { overlay = Cover("Story Fade", 0); overlay.sprite = White(); overlay.color = Color.black; }
                var old = slide;
                slide = Cover("Story Slide", 1);
                slide.sprite = sp; slide.color = new Color(1f, 1f, 1f, 0f);
                FitToCamera(slide, false);
                if (old != null) yield return FadeSprite(old, 1f, 0f, 0.6f);
                if (old != null) Destroy(old.gameObject);
                SoulRequiem.Core.Sfx.Play2D("cs_slide"); // 19/11 (§336): tranh mới hiện + nền dẫn chuyện
                SoulRequiem.Core.Sfx.Bed("cs_story_bed");
                yield return FadeSprite(slide, 0f, 1f, 1.0f);
            }
            else Debug.LogWarning($"[Story] thiếu tranh Resources/{path}");
            if (words.Length > 0) yield return Say(":" + words, 3.6f);
            else yield return Wait(2.5f);
        }

        private static IEnumerator FadeSprite(SpriteRenderer sr, float a0, float a1, float secs)
        {
            for (float t = 0f; t < secs && sr != null; t += Time.deltaTime)
            {
                sr.color = new Color(1f, 1f, 1f, Mathf.Lerp(a0, a1, t / secs));
                yield return null;
            }
            if (sr != null) sr.color = new Color(1f, 1f, 1f, a1);
        }

        // ---- tiện ích
        private static float F(string s, float d) =>
            float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : d;

        private bool TryTile(string s, out Vector3 w)
        {
            w = Vector3.zero;
            var xy = s.Split(',');
            if (xy.Length != 2) return false;
            if (!float.TryParse(xy[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) ||
                !float.TryParse(xy[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y)) return false;
            w = dir.TileToWorld(x, y);
            return true;
        }
    }
}
