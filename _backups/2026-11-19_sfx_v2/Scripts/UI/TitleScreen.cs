using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using SoulRequiem.Core;
using SoulRequiem.Gameplay;

namespace SoulRequiem.UI
{
    /// <summary>
    /// Màn hình tiêu đề (scene "Title", dựng bằng menu Soul Requiem ▸ Scenes ▸ Build Title Scene).
    /// 07/11 (v2, NOTES §182) — làm lại toàn bộ:
    ///   Nền: tranh PixelLab 480x270 (Ren đứng trên vách đá nhìn Vale of Graves dưới trăng máu), phóng nguyên lần (x4 ở 1080p), phủ kín
    ///   màn mọi tỉ lệ (cắt mép). Lớp động trên tranh, đặt theo toạ độ điểm ảnh của tranh: quầng trăng thở, cửa sổ nhà nguyện lập loè,
    ///   2 dải sương trôi ngược chiều, tàn lửa bay lên từ vách đá, bầy dơi thỉnh thoảng bay ngang trăng; dải tối bên trái + viền tối.
    ///   Khối tên game (huy hiệu PixelLab + SOUL REQUIEM + câu phụ + vạch) và menu chữ (TitleMenuItem: lửa hai bên mục đang chọn).
    ///   Mở màn: đen mờ dần, tên game hiện + trôi lên, mục menu hiện lần lượt. Chân trang: phiên bản (trái), phím (phải).
    ///   Continue = lửa trại đã lưu / map chơi gần nhất (PlayerPrefs "last_scene").
    /// </summary>
    public class TitleScreen : MonoBehaviour
    {
        [Tooltip("Scene bắt đầu khi chọn New Game.")]
        public string firstScene = "Map1_GreystonePlains";

        // toạ độ trong tranh nền (điểm ảnh của title_bg.png 480x270)
        private const float ArtW = 480f, ArtH = 270f;
        private static readonly Vector2 MoonAt = new Vector2(367f, 84f);
        private static readonly Vector2 WindowAt = new Vector2(152f, 194f);
        private const float FogTileW = 240f, FogTileH = 40f;

        private UIDocument doc;
        private VisualElement root, art, moonGlow, windowGlow, col, block, menu, settingsHost, blackout, settingsDim;
        private readonly VisualElement[] fogA = new VisualElement[3], fogB = new VisualElement[3];
        private readonly List<TitleMenuItem> items = new List<TitleMenuItem>();
        private float t, scale = 4f, lastW, lastH;

        private class Ember { public VisualElement v; public float x, y, vx, vy, life, age, size; }
        private readonly List<Ember> embers = new List<Ember>();
        private class Bat { public VisualElement v; public float x, y, vx, phase, bob; }
        private readonly List<Bat> bats = new List<Bat>();
        private float nextBats = 4f;
        private Texture2D[] batTex;

        private void Start()
        {
            Time.timeScale = 1f;
            GameInput.Blocked = false;
            doc = RequiemUI.CreateDocument(gameObject, out _, 0);
            root = doc.rootVisualElement;
            root.pickingMode = PickingMode.Position;
            root.style.backgroundColor = new Color(0.03f, 0.015f, 0.05f);

            // ---- tranh nền + lớp động (cùng hệ toạ độ tranh)
            art = new VisualElement { pickingMode = PickingMode.Ignore };
            art.style.position = Position.Absolute;
            root.Add(art);
            var bg = Layer(art, "title_bg", true);
            bg.style.left = 0; bg.style.top = 0; bg.style.right = 0; bg.style.bottom = 0;
            moonGlow = Layer(art, "title_glow", false);
            moonGlow.style.unityBackgroundImageTintColor = new Color(1f, 0.25f, 0.32f);
            windowGlow = Layer(art, "title_glow", false);
            windowGlow.style.unityBackgroundImageTintColor = new Color(1f, 0.72f, 0.36f);
            for (int i = 0; i < 3; i++) { fogA[i] = Layer(art, "title_fog", true); fogA[i].style.opacity = 0.45f; }
            for (int i = 0; i < 3; i++) { fogB[i] = Layer(art, "title_fog", true); fogB[i].style.opacity = 0.75f; }
            batTex = new[] { RequiemUI.Tex("title_bat_0"), RequiemUI.Tex("title_bat_1") };
            for (int i = 0; i < 26; i++) embers.Add(NewEmber(true));

            // dải tối bên trái (chữ đọc rõ) + viền tối
            var shade = Layer(root, "title_shade", false);
            shade.style.left = 0; shade.style.top = 0; shade.style.bottom = 0; shade.style.width = Length.Percent(70);
            var vig = Layer(root, "title_vignette", false);
            vig.style.left = 0; vig.style.top = 0; vig.style.right = 0; vig.style.bottom = 0;

            // ---- khối tên game + menu
            col = new VisualElement();
            col.AddToClassList("sr-tm-col");
            root.Add(col);
            block = new VisualElement { pickingMode = PickingMode.Ignore };
            block.AddToClassList("sr-tm-block");
            col.Add(block);
            RequiemUI.Deco("title_emblem", "sr-tm-emblem", block);
            RequiemUI.Text("SOUL REQUIEM", "sr-title sr-game-title sr-tm-title", block);
            RequiemUI.Text("Every hymn has an end.", "sr-game-sub sr-tm-sub", block);
            RequiemUI.Deco("hint_line", "sr-rule sr-tm-rule", block);

            menu = new VisualElement(); menu.AddToClassList("sr-tm-menu");
            col.Add(menu);
            string last = PlayerPrefs.GetString("last_scene", "");
            if (!string.IsNullOrEmpty(last) && Application.CanStreamedLevelBeLoaded(last))
                AddItem("Continue", () =>
                {
                    // 07/11: luôn tiếp tục từ lửa trại đã lưu. Thoát game ở map chưa có lửa trại (vd hầm King's Barrow, hoặc vừa sang
                    // map mới) -> về lửa trại cuối ở map trước, thay vì vào map đó trắng tay (mất cờ / tiến độ của nhóm map)
                    string saved = SaveGame.SavedScene;
                    string go = !string.IsNullOrEmpty(saved) && Application.CanStreamedLevelBeLoaded(saved) ? saved : last;
                    if (go == saved) SaveGame.RequestLoad();                      // 02/11: tiếp tục từ lửa trại đã lưu
                    GameSession.ForgetMaps();                                     // 07/11: tiến độ nhóm map cũ trong phiên không còn đúng
                    SceneManager.LoadScene(go);
                });
            AddItem("New Game", () =>
            {
                PlayerPrefs.DeleteKey("last_scene");
                SaveGame.Delete();                                              // 02/11: chơi mới = xoá bản lưu
                GameSession.NewGame();                                          // 04/11: xoá chiêu / sách mang theo của lượt trước
                if (Application.CanStreamedLevelBeLoaded(firstScene)) SceneManager.LoadScene(firstScene);
                else Debug.LogError($"[Title] Scene '{firstScene}' chưa có trong Build Settings.");
            });
            AddItem("Settings", OpenSettings);
            AddItem("Quit", RequiemMenus.Quit);

            RequiemUI.Text("v0.3", "sr-tiny sr-tm-version", root);
            RequiemUI.Text("Enter  Confirm      W / S  Select", "sr-tiny sr-tm-keys", root);

            settingsDim = new VisualElement { pickingMode = PickingMode.Ignore };
            settingsDim.AddToClassList("sr-fill"); settingsDim.AddToClassList("sr-tm-dim");
            settingsDim.style.display = DisplayStyle.None;
            root.Add(settingsDim);
            settingsHost = new VisualElement();
            settingsHost.AddToClassList("sr-fill"); settingsHost.AddToClassList("sr-center-box");
            settingsHost.style.display = DisplayStyle.None;
            root.Add(settingsHost);

            blackout = new VisualElement { pickingMode = PickingMode.Ignore };
            blackout.AddToClassList("sr-fill");
            blackout.style.backgroundColor = Color.black;
            root.Add(blackout);

            menu.schedule.Execute(() => items[0].Focus());
        }

        private void AddItem(string text, System.Action click)
        {
            var it = new TitleMenuItem(text, click) { Appear = 0f };
            items.Add(it);
            menu.Add(it);
        }

        /// Một lớp ảnh tuyệt đối. Ảnh pixel (crisp) giữ lọc Point; quầng sáng / bóng tối mềm dùng Bilinear.
        private static VisualElement Layer(VisualElement parent, string tex, bool crisp)
        {
            var v = new VisualElement { pickingMode = PickingMode.Ignore };
            v.style.position = Position.Absolute;
            var tx = RequiemUI.Tex(tex);
            if (tx != null)
            {
                if (!crisp) tx.filterMode = FilterMode.Bilinear;
                v.style.backgroundImage = new StyleBackground(tx);
                v.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));   // giãn đúng khung
            }
            parent.Add(v);
            return v;
        }

        // đặt một phần tử theo toạ độ tranh (tâm cx, cy; cỡ w x h điểm ảnh tranh)
        private void Place(VisualElement v, float cx, float cy, float w, float h)
        {
            v.style.left = Mathf.Round((cx - w * 0.5f) * scale); v.style.top = Mathf.Round((cy - h * 0.5f) * scale);
            v.style.width = Mathf.Round(w * scale); v.style.height = Mathf.Round(h * scale);
        }

        private void Layout(float w, float h)
        {
            scale = Mathf.Max(w / ArtW, h / ArtH);                     // phủ kín, cắt mép (ưu tiên giữ nguyên lần khi đúng 16:9)
            float aw = ArtW * scale, ah = ArtH * scale;
            art.style.left = Mathf.Round((w - aw) * 0.5f); art.style.top = Mathf.Round((h - ah) * 0.5f);
            art.style.width = aw; art.style.height = ah;
            Place(moonGlow, MoonAt.x, MoonAt.y, 240f, 240f);
            Place(windowGlow, WindowAt.x, WindowAt.y, 46f, 34f);
            for (int i = 0; i < 3; i++)
            {
                fogA[i].style.width = FogTileW * scale; fogA[i].style.height = FogTileH * scale;
                fogB[i].style.width = FogTileW * scale; fogB[i].style.height = FogTileH * scale;
            }
        }

        private Ember NewEmber(bool anywhere)
        {
            var e = new Ember { v = new VisualElement { pickingMode = PickingMode.Ignore } };
            e.v.style.position = Position.Absolute;
            art.Add(e.v);
            Respawn(e, anywhere);
            return e;
        }

        private static readonly Color[] EmberCols = { new Color(1f, 0.42f, 0.3f), new Color(1f, 0.66f, 0.32f), new Color(0.86f, 0.2f, 0.28f), new Color(1f, 0.85f, 0.55f) };

        private void Respawn(Ember e, bool anywhere)
        {
            e.x = Random.Range(250f, 480f);                              // vách đá + thung lũng bên phải, gió thổi về trái
            e.y = anywhere ? Random.Range(60f, 270f) : Random.Range(262f, 280f);
            e.vx = Random.Range(-9f, -3f); e.vy = Random.Range(-14f, -6f);
            e.life = Random.Range(6f, 12f); e.age = anywhere ? Random.Range(0f, e.life) : 0f;
            e.size = Random.value < 0.8f ? 1f : 2f;
            e.v.style.backgroundColor = EmberCols[Random.Range(0, EmberCols.Length)];
        }

        private void SpawnBats()
        {
            bool fromLeft = Random.value < 0.4f;
            int n = Random.Range(2, 5);
            float y0 = Random.Range(52f, 110f);
            for (int i = 0; i < n; i++)
            {
                var b = new Bat { v = new VisualElement { pickingMode = PickingMode.Ignore } };
                b.v.style.position = Position.Absolute;
                art.Insert(art.IndexOf(fogA[0]), b.v);                    // dưới lớp sương
                b.x = fromLeft ? -12f - i * Random.Range(8f, 16f) : ArtW + 12f + i * Random.Range(8f, 16f);
                b.y = y0 + Random.Range(-10f, 10f);
                b.vx = (fromLeft ? 1f : -1f) * Random.Range(34f, 46f);
                b.phase = Random.value; b.bob = Random.Range(2f, 5f);
                bats.Add(b);
            }
            nextBats = t + Random.Range(9f, 16f);
        }

        private void OpenSettings()
        {
            col.style.display = DisplayStyle.None;
            settingsDim.style.display = DisplayStyle.Flex;
            settingsHost.Clear();
            settingsHost.Add(RequiemMenus.BuildSettings(CloseSettings));
            settingsHost.style.display = DisplayStyle.Flex;
            settingsHost.schedule.Execute(() => settingsHost.Q<SRButton>()?.Focus());
        }

        private void CloseSettings()
        {
            settingsHost.style.display = DisplayStyle.None;
            settingsHost.Clear();
            settingsDim.style.display = DisplayStyle.None;
            col.style.display = DisplayStyle.Flex;
            menu.schedule.Execute(() => items[items.Count - 2].Focus());   // về lại "Settings" (luôn đứng trước Quit)
        }

        private void Update()
        {
            if (settingsHost != null && settingsHost.style.display == DisplayStyle.Flex && GameInput.Pressed(GameAction.Pause)) CloseSettings();
            if (art == null) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            t += dt;

            float w = root.resolvedStyle.width, h = root.resolvedStyle.height;
            if (!float.IsNaN(w) && w > 1f && (w != lastW || h != lastH)) { lastW = w; lastH = h; Layout(w, h); }

            // mở màn
            blackout.style.opacity = 1f - Mathf.SmoothStep(0f, 1f, t / 1.6f);
            if (t > 2f && blackout.style.display != DisplayStyle.None) blackout.style.display = DisplayStyle.None;
            float bk = Mathf.SmoothStep(0f, 1f, (t - 0.5f) / 1.1f);
            block.style.opacity = bk;
            block.style.translate = new Translate(0f, (1f - bk) * 24f);
            for (int i = 0; i < items.Count; i++)
            {
                items[i].Appear = Mathf.SmoothStep(0f, 1f, (t - 1.25f - i * 0.12f) / 0.45f);
                items[i].Tick(t);
            }

            // trăng thở + cửa sổ lập loè (2 sóng lệch nhịp như ngọn nến)
            moonGlow.style.opacity = 0.32f + 0.08f * Mathf.Sin(t * 0.7f);
            windowGlow.style.opacity = 0.42f + 0.12f * Mathf.Sin(t * 7.3f) * Mathf.Sin(t * 2.1f + 1f) + 0.05f * Mathf.Sin(t * 13f);

            // sương: A trôi chậm sang phải ở giữa thung lũng, B nhanh hơn sang trái ở chân tranh; bước theo điểm ảnh tranh
            float oa = Mathf.Repeat(t * 3f, FogTileW), ob = Mathf.Repeat(-t * 6f, FogTileW);
            for (int i = 0; i < 3; i++)
            {
                fogA[i].style.left = Mathf.Round((oa + (i - 1) * FogTileW)) * scale; fogA[i].style.top = Mathf.Round(124f * scale);
                fogB[i].style.left = Mathf.Round((ob + (i - 1) * FogTileW)) * scale; fogB[i].style.top = Mathf.Round(232f * scale);
            }

            // tàn lửa
            foreach (var e in embers)
            {
                e.age += dt;
                e.x += (e.vx + Mathf.Sin(t * 1.3f + e.y * 0.05f) * 4f) * dt; e.y += e.vy * dt;
                if (e.age >= e.life || e.y < 30f || e.x < -4f) { Respawn(e, false); }
                float u = e.age / e.life, a = u < 0.15f ? u / 0.15f : 1f - (u - 0.15f) / 0.85f;
                e.v.style.opacity = a * (0.6f + 0.4f * Mathf.Sin(t * 9f + e.x));
                e.v.style.left = Mathf.Round(e.x) * scale; e.v.style.top = Mathf.Round(e.y) * scale;
                e.v.style.width = e.size * scale; e.v.style.height = e.size * scale;
            }

            // dơi bay ngang trời (đập cánh 2 khung)
            if (t >= nextBats && t > 3f) SpawnBats();
            for (int i = bats.Count - 1; i >= 0; i--)
            {
                var b = bats[i];
                b.x += b.vx * dt;
                float y = b.y + Mathf.Sin((t + b.phase * 3f) * 2.4f) * b.bob;
                if (b.x < -40f || b.x > ArtW + 40f) { b.v.RemoveFromHierarchy(); bats.RemoveAt(i); continue; }
                var tx = batTex[(int)((t + b.phase) * 9f) % 2];
                if (tx != null && b.v.style.backgroundImage.value.texture != tx) b.v.style.backgroundImage = new StyleBackground(tx);
                b.v.style.left = Mathf.Round(b.x) * scale; b.v.style.top = Mathf.Round(y) * scale;
                b.v.style.width = 9f * scale; b.v.style.height = 5f * scale;
                b.v.style.scale = new Scale(new Vector3(b.vx < 0f ? -1f : 1f, 1f, 1f));
            }
        }
    }
}
