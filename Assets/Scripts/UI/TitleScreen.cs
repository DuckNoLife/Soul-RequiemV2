using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using SoulRequiem.Core;
using SoulRequiem.Gameplay;

namespace SoulRequiem.UI
{
    /// <summary>
    /// Title screen (scene "Title", built by the menu Soul Requiem ▸ Build Map ▸ Title Screen, TitleSceneBuilder).
    ///   Background: a 480x270 PixelLab painting (Ren standing on a cliff over the Vale of Graves under a blood moon), integer-scaled (x4 at 1080p), covering
    ///   the screen at any aspect ratio (edges cropped). Animated layers over the painting, placed in painting pixel coordinates: breathing moon halo, flickering chapel window,
    ///   2 mist bands drifting in opposite directions, embers rising from the cliff, a flock of bats now and then crossing the moon; dark strip on the left + vignette.
    ///   Title block (PixelLab crest + SOUL REQUIEM + subtitle + rule) and text menu (TitleMenuItem: flame beside the selected entry).
    ///   Opening: black fades out, the title appears + drifts up, menu entries appear one by one. Footer: version (left), keys (right).
    ///   Continue ONLY shows with a real save (SaveGame, a separate save file for builds); CHAPTERS returns to reached chapters to look for secrets,
    ///   keeping progress / swords / skills; New Game asks again when a save exists; transitions fade to black.
    /// </summary>
    public class TitleScreen : MonoBehaviour
    {
        [Tooltip("Scene started by New Game.")]
        public string firstScene = "Map1_GreystonePlains";

        // coordinates in the background painting (pixels of title_bg.png 480x270)
        private const float ArtW = 480f, ArtH = 270f;
        private static readonly Vector2 MoonAt = new Vector2(367f, 84f);
        private static readonly Vector2 WindowAt = new Vector2(152f, 194f);
        private const float FogTileW = 240f, FogTileH = 40f;

        private UIDocument doc;
        private VisualElement root, art, moonGlow, windowGlow, col, block, menu, settingsHost, blackout, settingsDim;
        private readonly VisualElement[] fogA = new VisualElement[3], fogB = new VisualElement[3];
        private readonly List<TitleMenuItem> items = new List<TitleMenuItem>();
        private float t, scale = 4f, lastW, lastH;

        private const string KeysText = "";                     // PC only — no "Enter Confirm / W S Select"
        private SaveGame.Data save;                              // save read once when the screen opens (null = never saved)
        private string continueNote = "";
        private Label note, keys, chNum, chTitle, chTag, chInfo;
        private TitleMenuItem itContinue, itChapters, itNew, confirmNo, chTravel, chBack;
        private int lastCard;
        private VisualElement chap, confirm;
        private readonly List<TitleChapterCard> rows = new List<TitleChapterCard>();
        private readonly List<TitleMenuItem> extra = new List<TitleMenuItem>();
        private HashSet<string> allFlags = new HashSet<string>();
        private bool leaving;
        private float leaveAt, leaveFrom;
        private System.Action leaveDo;

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

            // ---- background painting + animated layers (same painting coordinates)
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

            // dark strip on the left (readable text) + vignette
            var shade = Layer(root, "title_shade", false);
            shade.style.left = 0; shade.style.top = 0; shade.style.bottom = 0; shade.style.width = Length.Percent(70);
            var vig = Layer(root, "title_vignette", false);
            vig.style.left = 0; vig.style.top = 0; vig.style.right = 0; vig.style.bottom = 0;

            // ---- title block + menu
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
            // Continue only with a valid save whose scene is still in Build Settings (PlayerPrefs "last_scene" and a save file shared with the Editor
            // made Continue appear for players with nothing saved, dropping them mid-map empty-handed).
            save = SaveGame.Peek();
            if (save != null && !Application.CanStreamedLevelBeLoaded(save.scene)) save = null;
            if (save != null)
            {
                continueNote = ContinueNote();
                itContinue = AddItem("Continue", Continue, continueNote);          // sub-line = chapter / region to enter
                bool many = false;                                                   // Chapters only appears once Chapter II is reached
                foreach (var c in SaveGame.ReachedChapters(save)) if (c >= 2) many = true;
                if (many) itChapters = AddItem("Chapters", OpenChapters);
            }
            itNew = AddItem("New Game", () => { if (save != null) OpenConfirm(); else StartNewGame(); });
            AddItem("Settings", OpenSettings);
            AddItem("Quit", RequiemMenus.Quit);

            RequiemUI.Text("v0.3", "sr-tiny sr-tm-version", root);
            keys = RequiemUI.Text(KeysText, "sr-tiny sr-tm-keys", root);

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

            // UI sounds — move (keyboard / mouse / gamepad) and press; locked chapter cards play a locked-door sound
            root.RegisterCallback<FocusInEvent>(e =>
            {
                var v = e.target as VisualElement;
                if (leaving || t < 0.6f || v == null || !v.focusable) return;
                Sfx.Play2D("ui_move");
            }, TrickleDown.TrickleDown);
            root.RegisterCallback<NavigationSubmitEvent>(_ => UiPress(), TrickleDown.TrickleDown);
            root.RegisterCallback<ClickEvent>(e => { if (RequiemUI.Pressable(e.target as VisualElement, root)) UiPress(); }, TrickleDown.TrickleDown);   // empty background is silent
        }

        private void UiPress()
        {
            if (leaving) return;
            bool locked = focusController?.focusedElement is TitleChapterCard c && !c.Open;
            Sfx.Play2D(locked ? "door_locked" : "ui_confirm");
        }

        private TitleMenuItem AddItem(string text, System.Action click, string sub = null)
        {
            var it = new TitleMenuItem(text, click, sub) { Appear = 0f };
            items.Add(it);
            menu.Add(it);
            return it;
        }

        /// One absolute image layer. Pixel images (crisp) keep Point filtering; soft glows / shadows use Bilinear.
        private static VisualElement Layer(VisualElement parent, string tex, bool crisp)
        {
            var v = new VisualElement { pickingMode = PickingMode.Ignore };
            v.style.position = Position.Absolute;
            var tx = RequiemUI.Tex(tex);
            if (tx != null)
            {
                if (!crisp) tx.filterMode = FilterMode.Bilinear;
                v.style.backgroundImage = new StyleBackground(tx);
                v.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));   // stretched to the frame
            }
            parent.Add(v);
            return v;
        }

        // place an element in painting coordinates (centre cx, cy; size w x h painting pixels)
        private void Place(VisualElement v, float cx, float cy, float w, float h)
        {
            v.style.left = Mathf.Round((cx - w * 0.5f) * scale); v.style.top = Mathf.Round((cy - h * 0.5f) * scale);
            v.style.width = Mathf.Round(w * scale); v.style.height = Mathf.Round(h * scale);
        }

        private void Layout(float w, float h)
        {
            scale = Mathf.Max(w / ArtW, h / ArtH);                     // cover, crop edges (keeps integer scale at exactly 16:9)
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
            e.x = Random.Range(250f, 480f);                              // cliff + valley on the right, wind blowing left
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
                art.Insert(art.IndexOf(fogA[0]), b.v);                    // below the mist layer
                b.x = fromLeft ? -12f - i * Random.Range(8f, 16f) : ArtW + 12f + i * Random.Range(8f, 16f);
                b.y = y0 + Random.Range(-10f, 10f);
                b.vx = (fromLeft ? 1f : -1f) * Random.Range(34f, 46f);
                b.phase = Random.value; b.bob = Random.Range(2f, 5f);
                bats.Add(b);
            }
            nextBats = t + Random.Range(9f, 16f);
        }

        // ================================================================== Continue / New Game / Chapters

        /// <summary>Fade to black over 0.45 s, then run `act` (once; blocks repeated presses).</summary>
        private void Leave(System.Action act)
        {
            if (leaving) return;
            leaving = true;
            leaveAt = t;
            Sfx.Play2D("ui_begin");                                                // enter-game sound
            leaveFrom = blackout.style.display == DisplayStyle.None ? 0f : blackout.resolvedStyle.opacity;
            leaveDo = act;
            blackout.style.display = DisplayStyle.Flex;
            blackout.pickingMode = PickingMode.Position;                    // block the mouse during the fade
        }

        private void Continue()
        {
            if (leaving || save == null) return;
            var s = save;
            Leave(() =>
            {
                GameSession.NewGame();                    // static state from the previous run in this session (back to Title) must not leak into the save
                GameSession.RestoreGroups(s.groups);      // progress of other map groups (exits back and forth / Chapters)
                SaveGame.RequestLoad();
                SceneManager.LoadScene(s.scene);
            });
        }

        private void StartNewGame()
        {
            if (leaving) return;
            Leave(() =>
            {
                PlayerPrefs.DeleteKey("last_scene");
                SaveGame.Delete();                                              // new game = delete the save
                GameSession.NewGame();                                          // clear skills / books carried from the previous run
                if (Application.CanStreamedLevelBeLoaded(firstScene)) SceneManager.LoadScene(firstScene);
                else Debug.LogError($"[Title] Scene '{firstScene}' is not in Build Settings.");
            });
        }

        private string ContinueNote()
        {
            if (save == null) return "";
            var ch = ChapterBook.Get(ChapterBook.Of(save.scene));
            string where = ChapterBook.AreaName(save.scene);
            string head = ch != null ? $"Chapter {ch.roman}  -  {where}" : where;
            return head;
        }

        private TitleMenuItem Extra(string text, System.Action click, VisualElement parent)
        {
            var it = new TitleMenuItem(text, click) { Appear = 1f };
            extra.Add(it);
            parent.Add(it);
            return it;
        }

        private void ShowOverlay(VisualElement panel)
        {
            col.style.display = DisplayStyle.None;
            settingsDim.style.display = DisplayStyle.Flex;
            panel.style.display = DisplayStyle.Flex;
            keys.text = RequiemUI.Clean("Esc  Back");
        }

        private void HideOverlay(VisualElement panel, TitleMenuItem refocus)
        {
            panel.style.display = DisplayStyle.None;
            settingsDim.style.display = DisplayStyle.None;
            col.style.display = DisplayStyle.Flex;
            keys.text = RequiemUI.Clean(KeysText);
            if (refocus != null) menu.schedule.Execute(() => refocus.Focus());
        }

        // ---- New Game with an existing save: ask again ("Return" selected by default)
        private void OpenConfirm()
        {
            if (leaving) return;
            if (confirm == null)
            {
                confirm = new VisualElement();
                confirm.AddToClassList("sr-fill"); confirm.AddToClassList("sr-center-box");
                root.Insert(root.IndexOf(blackout), confirm);
                RequiemUI.Text("Begin a new journey?", "sr-title sr-tm-ask", confirm);
                RequiemUI.Text("Your saved journey - every chapter, blade and mantra - will be lost.", "sr-tm-asksub", confirm);
                var m = new VisualElement();
                m.AddToClassList("sr-tm-menu");
                confirm.Add(m);
                confirmNo = Extra("Return", CloseConfirm, m);
                Extra("Begin anew", () => { if (leaving) return; StartNewGame(); }, m);
            }
            ShowOverlay(confirm);
            confirm.schedule.Execute(() => confirmNo.Focus());
        }

        private void CloseConfirm() { if (!leaving && confirm != null) HideOverlay(confirm, itNew); }

        // ---- Chapters
        private void OpenChapters()
        {
            if (leaving || save == null) return;
            if (chap == null) BuildChapters();
            ShowOverlay(chap);
            int cur = Mathf.Clamp(ChapterBook.Of(save.scene) - 1, 0, rows.Count - 1);
            chap.schedule.Execute(() => { rows[cur].Focus(); ShowDetail(cur); });
        }

        private void CloseChapters() { if (!leaving && chap != null) HideOverlay(chap, itChapters); }

        // 2 x 2 grid of chapter cards (512x288 images = x2 source pixels, crisp)
        // + detail column on the right (chapter number, name, tagline, where it continues, hidden blades, Travel / Back buttons). Chapter V left out — add it back when its map exists.
        private void BuildChapters()
        {
            chap = new VisualElement();
            chap.AddToClassList("sr-ch2-panel");
            chap.style.display = DisplayStyle.None;
            root.Insert(root.IndexOf(blackout), chap);

            var head = new VisualElement { pickingMode = PickingMode.Ignore }; head.AddToClassList("sr-ch2-head"); chap.Add(head);
            RequiemUI.Text("CHAPTERS", "sr-title sr-ch2-headtext", head);
            RequiemUI.Text("Return to a land you have walked. Your blades, mantras and flasks travel with you.", "sr-ch2-sub", head);
            RequiemUI.Deco("hint_line", "sr-rule sr-ch2-rule", head);

            var body = new VisualElement(); body.AddToClassList("sr-ch2-body"); chap.Add(body);
            var grid = new VisualElement(); grid.AddToClassList("sr-ch2-grid"); body.Add(grid);
            var reached = SaveGame.ReachedChapters(save);
            allFlags = SaveGame.AllFlags(save);
            VisualElement line = null;
            foreach (var ch in ChapterBook.All)
            {
                if (rows.Count % 2 == 0) { line = new VisualElement(); line.AddToClassList("sr-ch2-line"); grid.Add(line); }
                bool open = ch.Playable && reached.Contains(ch.n);
                int n = ch.n, idx = rows.Count;
                var card = new TitleChapterCard(ch, open, CardStatus(ch, open), ChapterBook.Of(save.scene) == ch.n, () => TravelTo(n));
                card.RegisterCallback<FocusInEvent>(_ => { lastCard = idx; ShowDetail(idx); });
                card.RegisterCallback<NavigationMoveEvent>(e => GridMove(idx, e));
                rows.Add(card);
                line.Add(card);
            }

            var det = new VisualElement(); det.AddToClassList("sr-ch2-detail"); body.Add(det);
            chNum = RequiemUI.Text("", "sr-title sr-ch2-num", det);
            chTitle = RequiemUI.Text("", "sr-title sr-ch2-title", det);
            chTag = RequiemUI.Text("", "sr-ch2-tag", det);
            RequiemUI.Deco("hint_line", "sr-rule sr-ch2-rule2", det);
            chInfo = RequiemUI.Text("", "sr-ch2-info", det);
            var acts = new VisualElement(); acts.AddToClassList("sr-ch2-acts"); det.Add(acts);
            chTravel = Extra("Travel", () => TravelTo(rows[Mathf.Clamp(lastCard, 0, rows.Count - 1)].Chapter.n), acts);
            chBack = Extra("Back", CloseChapters, acts);
            chTravel.RegisterCallback<NavigationMoveEvent>(e => SideMove(e, true));
            chBack.RegisterCallback<NavigationMoveEvent>(e => SideMove(e, false));
        }

        /// <summary>Navigate the 2-column grid with W/A/S/D / gamepad; leaving the right column -> the Travel button.</summary>
        private void GridMove(int i, NavigationMoveEvent e)
        {
            int n = rows.Count, to = -1;
            switch (e.direction)
            {
                case NavigationMoveEvent.Direction.Left: if (i % 2 == 1) to = i - 1; break;
                case NavigationMoveEvent.Direction.Right: to = i % 2 == 0 && i + 1 < n ? i + 1 : -2; break;
                case NavigationMoveEvent.Direction.Up: if (i >= 2) to = i - 2; break;
                case NavigationMoveEvent.Direction.Down: to = i + 2 < n ? i + 2 : -2; break;
                default: return;
            }
            if (to >= 0) rows[to].Focus();
            else if (to == -2) (rows[i].Open ? chTravel : chBack).Focus();
            e.StopPropagation();
            focusController?.IgnoreEvent(e);
        }

        private void SideMove(NavigationMoveEvent e, bool travel)
        {
            switch (e.direction)
            {
                case NavigationMoveEvent.Direction.Left: rows[Mathf.Clamp(lastCard, 0, rows.Count - 1)].Focus(); break;
                case NavigationMoveEvent.Direction.Up: if (travel) rows[Mathf.Clamp(lastCard, 0, rows.Count - 1)].Focus(); else chTravel.Focus(); break;
                case NavigationMoveEvent.Direction.Down: if (travel) chBack.Focus(); break;
                default: return;
            }
            e.StopPropagation();
            focusController?.IgnoreEvent(e);
        }

        private FocusController focusController => root != null ? root.focusController : null;

        private int BladesFound(ChapterBook.Chapter ch)
        {
            int k = 0;
            foreach (var b in ch.blades) if (allFlags.Contains(WeaponRing.Flag(b))) k++;
            return k;
        }

        private string CardStatus(ChapterBook.Chapter ch, bool open)
        {
            if (!open) return "Continue the story to unlock";                       // reveal nothing about chapters not yet reached
            return ch.blades.Length > 0 ? $"Secrets  {BladesFound(ch)} / {ch.blades.Length}" : "Explored";   // "Secrets", never says swords
        }

        private void ShowDetail(int i)
        {
            if (i < 0 || i >= rows.Count) return;
            var card = rows[i];
            var ch = card.Chapter;
            chNum.text = $"CHAPTER {ch.roman}";
            chTitle.text = RequiemUI.Clean(card.Open ? ch.name : "???");
            chTravel.style.display = card.Open ? DisplayStyle.Flex : DisplayStyle.None;
            if (card.Open)
            {
                chTag.text = RequiemUI.Clean(ch.tagline);
                var g = SaveGame.GroupFor(save, ch.n);
                string where = g != null && g.hasPoint ? $"Resume at the last fire you lit:\n{ChapterBook.AreaName(g.scene)}"
                                                       : "Resume at the start of the chapter.";
                string blades;
                if (ch.blades.Length == 0) blades = "";
                else
                {
                    int f = BladesFound(ch);
                    blades = f >= ch.blades.Length ? $"Secrets  {f} / {ch.blades.Length}  -  all found"
                                                   : $"Secrets  {f} / {ch.blades.Length}  -  something still waits here";
                }
                string here = ChapterBook.Of(save.scene) == ch.n ? "\n\nYour journey rests here." : "";
                chInfo.text = RequiemUI.Clean(where + (blades.Length > 0 ? "\n\n" + blades : "") + here);
            }
            else
            {
                chTag.text = "This land has not yet been reached.";
                chInfo.text = "";
            }
        }

        private void TravelTo(int n)
        {
            if (leaving || save == null) return;
            var d = SaveGame.BuildChapterLoad(n);
            if (d == null || !Application.CanStreamedLevelBeLoaded(d.scene))
            {
                Debug.LogWarning($"[Title] Could not enter chapter {n} (scene '{d?.scene}').");
                return;
            }
            var groups = save.groups;
            Leave(() =>
            {
                GameSession.NewGame();
                GameSession.RestoreGroups(groups);        // other chapters keep their progress when reached through exits
                SaveGame.RequestLoad(d);
                SceneManager.LoadScene(d.scene);
            });
        }

        /// <summary>Caption under the menu for the selected entry (Continue: chapter / region / save time).</summary>
        private void UpdateNote()
        {
            if (note == null) return;
            string s = "";
            if (itContinue != null && itContinue.Selected) s = continueNote;
            else if (itChapters != null && itChapters.Selected) s = "Return to a land you have walked.";
            else if (itNew != null && itNew.Selected) s = save != null ? "Begin anew. Your saved journey will be lost." : "Every hymn has a first verse.";
            s = RequiemUI.Clean(s);
            if (note.text != s) note.text = s;
            note.style.opacity = items.Count > 0 ? items[items.Count - 1].Appear : 1f;
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
            menu.schedule.Execute(() => items[items.Count - 2].Focus());   // back to "Settings" (always right before Quit)
        }

        private void Update()
        {
            if (!leaving && GameInput.Pressed(GameAction.Pause))                 // Esc closes the open layer
            {
                if (settingsHost != null && settingsHost.style.display == DisplayStyle.Flex) CloseSettings();
                else if (chap != null && chap.style.display == DisplayStyle.Flex) CloseChapters();
                else if (confirm != null && confirm.style.display == DisplayStyle.Flex) CloseConfirm();
            }
            if (art == null) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            t += dt;

            float w = root.resolvedStyle.width, h = root.resolvedStyle.height;
            if (!float.IsNaN(w) && w > 1f && (w != lastW || h != lastH)) { lastW = w; lastH = h; Layout(w, h); }

            // opening
            if (leaving)                                                         // fade to black before loading the scene
            {
                float k = Mathf.Clamp01((t - leaveAt) / 0.45f);
                blackout.style.opacity = Mathf.Lerp(leaveFrom, 1f, k);
                if (k >= 1f && leaveDo != null) { var go = leaveDo; leaveDo = null; go(); }
            }
            else
            {
                blackout.style.opacity = 1f - Mathf.SmoothStep(0f, 1f, t / 1.6f);
                if (t > 2f && blackout.style.display != DisplayStyle.None) blackout.style.display = DisplayStyle.None;
            }
            float bk = Mathf.SmoothStep(0f, 1f, (t - 0.5f) / 1.1f);
            block.style.opacity = bk;
            block.style.translate = new Translate(0f, (1f - bk) * 24f);
            for (int i = 0; i < items.Count; i++)
            {
                items[i].Appear = Mathf.SmoothStep(0f, 1f, (t - 1.25f - i * 0.12f) / 0.45f);
                items[i].Tick(t);
            }
            foreach (var e in extra) e.Tick(t);
            foreach (var r in rows) r.Tick(t);
            UpdateNote();

            // breathing moon + flickering window (2 out-of-phase waves like a candle)
            moonGlow.style.opacity = 0.32f + 0.08f * Mathf.Sin(t * 0.7f);
            windowGlow.style.opacity = 0.42f + 0.12f * Mathf.Sin(t * 7.3f) * Mathf.Sin(t * 2.1f + 1f) + 0.05f * Mathf.Sin(t * 13f);

            // mist: A drifts slowly right across the middle of the valley, B faster left at the bottom of the painting; steps in painting pixels
            float oa = Mathf.Repeat(t * 3f, FogTileW), ob = Mathf.Repeat(-t * 6f, FogTileW);
            for (int i = 0; i < 3; i++)
            {
                fogA[i].style.left = Mathf.Round((oa + (i - 1) * FogTileW)) * scale; fogA[i].style.top = Mathf.Round(124f * scale);
                fogB[i].style.left = Mathf.Round((ob + (i - 1) * FogTileW)) * scale; fogB[i].style.top = Mathf.Round(232f * scale);
            }

            // embers
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

            // bats crossing the sky (2-frame wing flap)
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
