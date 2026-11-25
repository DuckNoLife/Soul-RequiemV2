#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using SoulRequiem.Core;
using SoulRequiem.Gameplay;

namespace SoulRequiem.DevTools
{
    /// <summary>
    /// Test keys for developers / testers. ONLY exists in the Unity Editor and "Development Build" —
    /// release builds do not compile this file, so players cannot press them by accident.
    /// Keys (the in-game list is <see cref="Help"/>, toggled with F1):
    ///   F1  this panel + test status (toggle)
    ///   F2  god mode: never die, HP + flasks always full                      · Shift: WALK THROUGH WALLS (Ren's body collider becomes a trigger)
    ///   F3  defeat every enemy (caves too)                                    · Shift: PASS the current zone (enemies + puzzles / minigames + story flags)
    ///                                                                         · Ctrl:  REPLAY the current zone (reload the map, then jump to the zone)
    ///   F4  GO TO the map's BOSS (press again = next boss; sets every earlier step's flags, lifts restraints — clothes, sword, flasks, Mantras back,
    ///       full HP, cutscenes skipped) · Shift: next story step · Ctrl+Shift: previous step · Ctrl: TAKE the nearest exit now (change map)
    ///   F5  solve every puzzle + every key + OPEN EVERY GATE of the map
    ///   F6  UNLOCK EVERYTHING: every Mantra, passive book, katana, key item + all flask slots, and lift the map's restraints
    ///   F7  current boss −25% HP · Shift: jump to phase 2 · mechanism bosses (Absalom / Vessel): pass the current phase · Ctrl: showcase skill
    ///   F8  game speed 1× → 0.5× → 0.25× → 2× (study skill telegraphs) · Shift: damage log in the Console ([DMG])
    ///   F9  reload from the save · Shift: die (test death / respawn) · Ctrl: RESTART THE WHOLE MAP (drops the map group's progress, keeps items / skills)
    ///   F10 PICKER: tabs ZONES (zones of this map) / BOSSES (every boss of every map) / MAPS (every scene in Build Settings).
    ///       ↑↓ select, Enter = go, Shift+Enter = replay from scratch, Tab switches tab, F10 / Backspace closes (not Esc: Esc opens Pause).
    ///   F11 SKIP the running cutscene (and chained ones) · Shift: ALWAYS skip cutscenes (toggle). Zone jumps / go-to-boss auto-skip for 8 s.
    /// Test keys work during cutscenes / dialogue too; they are only blocked while a menu is open, Ren is down, or the map is changing.
    /// While a cheat affects the numbers (god mode, walk through walls, speed ≠ 1×, damage log) -> a small "TEST" badge at the top of the screen.
    /// Attaches itself to every scene at runtime (no need to drop it in a scene). New key: add a branch in <see cref="Update"/> + a line in <see cref="Help"/>.
    /// </summary>
    public class DevCheats : MonoBehaviour
    {
        private static readonly float[] Speeds = { 1f, 0.5f, 0.25f, 2f };
        private static readonly string[] Help =
        {
            "F1   This panel",
            "F2   God mode (never die, HP + flasks full)     Shift: walk through walls",
            "F3   Defeat every enemy     Shift: PASS this zone     Ctrl: REPLAY this zone",
            "F4   GO TO BOSS (press again: next boss)     Shift: next story step     Ctrl+Shift: previous     Ctrl: TAKE EXIT now",
            "F5   Solve all puzzles + every key item + OPEN EVERY GATE",
            "F6   Unlock EVERYTHING (all 5 flask slots) + lift map restraints (normal clothes, blade, flasks, mantras)",
            "F7   Boss -25% HP     Shift: jump to phase 2     (Absalom: skip current phase)     Ctrl: Absalom showcase skill",
            "F8   Game speed 1x / 0.5x / 0.25x / 2x     Shift: damage log",
            "F9   Reload last save     Shift: die     Ctrl: RESTART this map from scratch",
            "F10  Picker: ZONES / BOSSES (every map) / MAPS     Enter: go   Shift+Enter: replay from scratch (story scenes skipped, boss intro kept)",
            "F11  SKIP the cutscene playing now     Shift: ALWAYS skip cutscenes (toggle)",
            "Stuck behind a wall?  Shift+F2 = walk through walls",
        };

        private PlayerCombat player;
        private static bool logDamage;
        private bool showHelp;
        private int speedIndex;
        private GUIStyle boxStyle, badgeStyle, rowStyle;
        private float frameMs = 16.7f, worstMs;                 // performance numbers for the F1 panel
        private float worstReset;

        // walk through walls
        private bool ghost;
        private readonly List<Collider2D> ghostCols = new List<Collider2D>();
        private PlayerCombat ghostOwner;

        // zone / map picker (F10)
        private bool picker;
        private int pickTab, pickIndex;
        private readonly List<MapEncounterDirector.DebugZoneInfo> pickZones = new List<MapEncounterDirector.DebugZoneInfo>();
        private readonly List<string> pickMaps = new List<string>();
        /// <summary>BOSSES tab — (scene, key, name) of every boss in every map (key = boss zone id or the boss mechanism name in Tiled).</summary>
        private static readonly string[][] AllBosses =
        {
            new[] { "Map1_GreystonePlains", "z5", "Grok" },
            new[] { "Map2_UndercityOfVael", "cult", "Brood Toad (mini)" },
            new[] { "Map2_UndercityOfVael", "boil", "Ichor Ram (mini)" },
            new[] { "Map2_UndercityOfVael", "r8", "Plague Eye" },
            new[] { "Map2_UndercityOfVael", "vault", "Specimen Zero" },
            new[] { "Map2b_KrovenDeep", "g4", "The Last Guard" },
            new[] { "Map3_ValeOfGraves", "x3", "Matriarch Isolde" },
            new[] { "Map3_ValeOfGraves", "v6", "Ent (mini)" },
            new[] { "Map3_ValeOfGraves", "v9", "Lantern Knight" },
            new[] { "Map3_ValeOfGraves", "v11", "Precentor Morrow" },
            new[] { "Map3b_KingsBarrow", "x4", "The Red Field" },
            new[] { "Map4m_TheMargin", "c2r", "The Registrar" },
            new[] { "Map4_CatacombsOfNaming", "c4b", "Brazen Reliquary + Hollow Champion" },
            new[] { "Map4d_DrillYard", "drill_exam", "Sgt. Burlap - Final Exam" },
            new[] { "Map4_CatacombsOfNaming", "absalom_hall", "Absalom (Hall of Naming)" },
        };
        private int bossCursor = -1;
        private float bossCursorAt;
        private bool skipScaled;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var go = new GameObject("[DevCheats]");
            DontDestroyOnLoad(go);
            go.AddComponent<DevCheats>();
        }

        private float Speed => Speeds[speedIndex];

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            bool shift = kb.shiftKey.isPressed, ctrl = kb.ctrlKey.isPressed;
            if (kb.f1Key.wasPressedThisFrame) showHelp = !showHelp;           // the key panel can be shown even while a menu is open
            float ms = Time.unscaledDeltaTime * 1000f;
            frameMs = Mathf.Lerp(frameMs, ms, 0.05f);
            if (Time.unscaledTime > worstReset) { worstReset = Time.unscaledTime + 2f; worstMs = 0f; }
            worstMs = Mathf.Max(worstMs, ms);
            KeepSpeed();

            if (player == null) player = FindFirstObjectByType<PlayerCombat>();   // new scene -> find again
            if (ghostOwner != player) { ghost = false; ghostCols.Clear(); ghostOwner = player; }   // new Ren (map change) -> walk-through-walls off
            if (player == null || player.Health == null) { if (picker) ClosePicker(); return; }
            if (player.GodMode) { player.Health.Heal(player.Health.Max); player.RefillPotions(); }

            if (picker) { PickerInput(kb, shift); return; }                     // picker open -> only the picker's keys are handled

            // F11 skip cutscene — works even while a cutscene locks the controls
            if (kb.f11Key.wasPressedThisFrame)
            {
                if (shift) { CutsceneSkip.Always = !CutsceneSkip.Always; Hints.Show(CutsceneSkip.Always ? "[TEST] Always skip cutscenes: ON" : "[TEST] Always skip cutscenes: OFF", 1.5f); }
                else { CutsceneSkip.For(1.5f); if (!CutsceneSkip.Playing) Hints.Show("[TEST] No cutscene playing (Shift+F11: always skip)", 1.5f); }
            }
            TickSkip();

            // F9 works even while down (quick retry)
            if (kb.f9Key.wasPressedThisFrame && !shift)
            {
                if (ctrl) RestartMap(null); else ReloadSave();
                return;
            }
            // only blocked while a menu (Pause / inventory…) is open, Ren is down or the map is changing — NOT during cutscenes / dialogue
            if (GameInput.MenuOpen || player.Health.IsDead || SceneTransition.Busy) return;

            var dir = MapEncounterDirector.Instance;
            if (kb.f2Key.wasPressedThisFrame)
            {
                if (shift) SetGhost(!ghost);
                else
                {
                    player.GodMode = !player.GodMode;
                    Hints.Show(player.GodMode ? "[TEST] God mode: ON" : "[TEST] God mode: OFF", 1.5f);
                }
            }
            if (dir != null && kb.f3Key.wasPressedThisFrame)
            {
                if (ctrl) ReplayZone(dir.DebugZoneHere());
                else if (shift) dir.DebugPassZone();
                else dir.DebugKillAll();
            }
            if (dir != null && kb.f4Key.wasPressedThisFrame)
            {
                if (ctrl && shift) { CutsceneSkip.For(8f); dir.DebugStepZone(-1); }
                else if (ctrl) dir.DebugUseExit();
                else if (shift) { CutsceneSkip.For(8f); dir.DebugStepZone(1); }
                else GoToNextBoss(dir);
            }
            if (kb.f5Key.wasPressedThisFrame) SolveAndGiveKeys(dir);
            if (kb.f6Key.wasPressedThisFrame) UnlockEverything(dir);
            if (kb.f7Key.wasPressedThisFrame && ctrl)                            // Ctrl+F7 = showcase skill of the mechanism boss (Absalom / Vessel)
            {
                if (MapMechanics.Instance == null || !MapMechanics.Instance.DebugBossShowcase()) Hints.Show("[TEST] Showcase: start the Absalom fight first (F4)", 1.5f);
            }
            else if (kb.f7Key.wasPressedThisFrame)
            {
                if (dir != null && dir.Boss != null && dir.Boss.Health != null && !dir.Boss.Health.IsDead) HurtBoss(dir, shift);
                else if (MapMechanics.Instance != null && MapMechanics.Instance.DebugSkipBossPhase()) { }
                else Hints.Show("[TEST] No boss fight right now (F4: go to boss)", 1.5f);
            }
            if (kb.f8Key.wasPressedThisFrame)
            {
                if (shift) ToggleDamageLog();
                else
                {
                    float old = Speed;
                    speedIndex = (speedIndex + 1) % Speeds.Length;
                    if (Mathf.Approximately(Time.timeScale, old)) Time.timeScale = Speed;
                    Hints.Show($"[TEST] Game speed: {Speed:0.##}x", 1.5f);
                }
            }
            if (kb.f9Key.wasPressedThisFrame && shift) { player.GodMode = false; player.Health.TakeDamage(99999f); }
            if (kb.f10Key.wasPressedThisFrame) OpenPicker(dir);
        }

        // ----------------------------------------------------------------- F11 skip cutscene, F4 go to boss
        /// <summary>Skipping and a cutscene is running -> timeScale 12× (extends the skip window so chained cutscenes are skipped too); no cutscene -> restore speed.</summary>
        private void TickSkip()
        {
            // pushing / restoring timeScale lives in CutsceneSkipper (players can skip too) — here we only read its state
            // so KeepSpeed does not fight over timeScale and the GUI badge is correct.
            bool was = skipScaled;
            skipScaled = CutsceneSkipper.Skipping;
            if (was && !skipScaled && Time.timeScale > 0f && !Mathf.Approximately(Speed, 1f)) Time.timeScale = Speed;
        }

        /// <summary>F4: go to the map's next boss (first press = first boss not yet defeated; pressing again within 15 s = the next one).</summary>
        private void GoToNextBoss(MapEncounterDirector dir)
        {
            var bosses = dir.DebugBosses();
            if (bosses.Count == 0) { Hints.Show("[TEST] No boss in this map - F10 > BOSSES lists every boss of every map", 2.5f); return; }
            int k;
            if (bossCursor >= 0 && Time.unscaledTime - bossCursorAt < 15f) k = (bossCursor + 1) % bosses.Count;
            else { k = 0; for (int i = 0; i < bosses.Count; i++) if (!bosses[i].cleared) { k = i; break; } }
            bossCursor = k; bossCursorAt = Time.unscaledTime;
            dir.DebugGoToBoss(bosses[k].key);                                       // no more skipping — other cutscenes are dropped, the boss intro still plays (F11 to skip it)
            Hints.Show($"[TEST] Boss {k + 1}/{bosses.Count}: {bosses[k].title}" + (bosses.Count > 1 ? "   (F4 again: next boss)" : ""), 3f);
        }

        /// <summary>BOSSES tab: boss in another map -> change map (keeping progress), then go to the boss once the map is built.</summary>
        private void GoToBossAnyMap(string scene, string key)
        {
            var dir = MapEncounterDirector.Instance;
            if (scene == SceneManager.GetActiveScene().name && dir != null) { dir.DebugGoToBoss(key); return; }
            if (player == null || SceneTransition.Busy) return;
            if (!Application.CanStreamedLevelBeLoaded(scene)) { Hints.Show("[TEST] Scene not in Build Settings: " + scene, 2f); return; }
            StoryScenes.DevSkipIntros = true;                                        // the newly loaded map does not play its opening / cell cutscene
            GoMap(player.transform, scene, false);
            StartCoroutine(BossAfterLoad(dir, key));
        }

        private IEnumerator BossAfterLoad(MapEncounterDirector old, string key)
        {
            float limit = Time.unscaledTime + 30f;
            MapEncounterDirector d = null;
            while (Time.unscaledTime < limit)
            {
                d = MapEncounterDirector.Instance;
                if (d != null && d != old && d.DebugReady && !SceneTransition.Busy) break;
                yield return null;
            }
            if (d == null || d == old || !d.DebugReady) { StoryScenes.DevSkipIntros = false; yield break; }
            yield return null;
            d.DebugGoToBoss(key);                                                    // drop cutscenes outside the boss zone; the boss intro still plays
            yield return EndSkipIntros();
        }

        /// <summary>Clear the skip-map-opening flag once the new map has passed the Start of its opening scenes (OpeningCutscene waits 2 frames + StoryScenes.Busy).</summary>
        private static IEnumerator EndSkipIntros()
        {
            for (float t = Time.unscaledTime + 2f; Time.unscaledTime < t;) yield return null;
            StoryScenes.DevSkipIntros = false;
        }

        /// <summary>Keep the test speed: other systems (closing menus, end of hit-stop, cutscenes) reset timeScale to 1 -> set it again.
        /// Never touches it while the game is paused (timeScale 0) or during hit-stop.</summary>
        private void KeepSpeed()
        {
            if (skipScaled) return;                                                    // skipping a cutscene
            if (!Mathf.Approximately(Speed, 1f) && Mathf.Approximately(Time.timeScale, 1f)) Time.timeScale = Speed;
        }

        // ------------------------------------------------------------------ Shift+F2: walk through walls
        /// <summary>Ren's body collision (non-trigger colliders) temporarily becomes a trigger -> walk through walls / gates / obstacles.
        /// Attacks, traps and trigger zones still work (they detect by area / trigger). Turning it off restores everything.</summary>
        private void SetGhost(bool on)
        {
            if (on == ghost || player == null) return;
            if (on)
            {
                ghostCols.Clear();
                foreach (var c in player.GetComponentsInChildren<Collider2D>(true))
                    if (c != null && !c.isTrigger) { c.isTrigger = true; ghostCols.Add(c); }
            }
            else
            {
                foreach (var c in ghostCols) if (c != null) c.isTrigger = false;
                ghostCols.Clear();
            }
            ghost = on;
            Hints.Show(on ? "[TEST] Walk through walls: ON" : "[TEST] Walk through walls: OFF", 1.5f);
        }

        // ------------------------------------------------------------------ replay map / zone (Ctrl+F9, Ctrl+F3, F10 Shift+Enter)
        /// <summary>Reload scene `scene` (null = current map) FROM SCRATCH — does not restore the map group's remembered flags, does not load the save;
        /// keeps items / skills / katanas / books (GameSession carries them like a door transition). zone != null -> on arrival jump to that zone like F4.</summary>
        private void RestartMap(string zone, string scene = null)
        {
            if (player == null || SceneTransition.Busy) return;
            string cur = SceneManager.GetActiveScene().name;
            if (string.IsNullOrEmpty(scene)) scene = cur;
            if (!Application.CanStreamedLevelBeLoaded(scene)) { Hints.Show("[TEST] Scene not in Build Settings: " + scene, 2f); return; }
            GameSession.FreshNextMap = true;
            StoryScenes.DevSkipIntros = !string.IsNullOrEmpty(zone);                  // replaying one zone -> no map opening / cell cutscene
            var old = MapEncounterDirector.Instance;
            SceneTransition.Go(player.transform, scene, string.IsNullOrEmpty(zone) ? "[TEST] Restarting " + scene + " from scratch..." : "[TEST] Replaying zone from scratch...", cur, false);
            if (!string.IsNullOrEmpty(zone)) StartCoroutine(JumpAfterLoad(old, zone));
        }

        private void ReplayZone(string zone)
        {
            if (string.IsNullOrEmpty(zone)) { Hints.Show("[TEST] Not inside a zone", 1.5f); return; }
            RestartMap(zone);
        }

        private IEnumerator JumpAfterLoad(MapEncounterDirector old, string zone)
        {
            float limit = Time.unscaledTime + 30f;
            MapEncounterDirector d = null;
            while (Time.unscaledTime < limit)
            {
                d = MapEncounterDirector.Instance;
                if (d != null && d != old && d.DebugReady && !SceneTransition.Busy) break;
                yield return null;
            }
            if (d == null || d == old || !d.DebugReady) { StoryScenes.DevSkipIntros = false; yield break; }
            yield return null;
            d.DebugJumpToZone(zone);                                                 // also drop cutscenes outside the zone (the zone's boss intro still plays)
            yield return EndSkipIntros();
        }

        private static void GoMap(Transform who, string scene, bool fresh)
        {
            if (who == null || SceneTransition.Busy) return;
            if (!Application.CanStreamedLevelBeLoaded(scene)) { Hints.Show("[TEST] Scene not in Build Settings: " + scene, 2f); return; }
            GameSession.FreshNextMap = fresh;
            SceneTransition.Go(who, scene, (fresh ? "[TEST] Starting " : "[TEST] Travelling to ") + scene + "...", SceneManager.GetActiveScene().name, false);
        }

        // ------------------------------------------------------------------ F10: zone / map picker
        private void OpenPicker(MapEncounterDirector dir)
        {
            if (GameInput.MenuOpen) return;                                        // can be opened during cutscenes / dialogue too
            pickZones.Clear();
            if (dir != null) pickZones.AddRange(dir.DebugZones());
            pickMaps.Clear();
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string n = System.IO.Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(i));
                if (!string.IsNullOrEmpty(n) && n != "Title") pickMaps.Add(n);
            }
            pickTab = pickZones.Count > 0 ? 0 : 1;
            pickIndex = 0;
            if (pickTab == 0) for (int i = 0; i < pickZones.Count; i++) if (pickZones[i].here) { pickIndex = i; break; }
            picker = true;
            GameInput.Blocked = true;                                             // Ren stands still while picking
        }

        private void ClosePicker()
        {
            if (!picker) return;
            picker = false;
            GameInput.Blocked = false;
        }

        private int PickCount => pickTab == 0 ? pickZones.Count : pickTab == 1 ? AllBosses.Length : pickMaps.Count;

        private void PickerInput(Keyboard kb, bool shift)
        {
            if (kb.f10Key.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame) { ClosePicker(); return; }
            if (kb.tabKey.wasPressedThisFrame)
            {
                pickTab = (pickTab + (shift ? 2 : 1)) % 3; pickIndex = 0;                 // ZONES -> BOSSES -> MAPS
                if (pickTab == 2) pickIndex = Mathf.Max(0, pickMaps.IndexOf(SceneManager.GetActiveScene().name));
                else if (pickTab == 0) { for (int i = 0; i < pickZones.Count; i++) if (pickZones[i].here) { pickIndex = i; break; } }
                else for (int i = 0; i < AllBosses.Length; i++) if (AllBosses[i][0] == SceneManager.GetActiveScene().name) { pickIndex = i; break; }
                return;
            }
            int n = PickCount;
            if (n == 0) return;
            if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) pickIndex = (pickIndex - 1 + n) % n;
            if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) pickIndex = (pickIndex + 1) % n;
            if (kb.pageUpKey.wasPressedThisFrame) pickIndex = Mathf.Max(0, pickIndex - 10);
            if (kb.pageDownKey.wasPressedThisFrame) pickIndex = Mathf.Min(n - 1, pickIndex + 10);
            pickIndex = Mathf.Clamp(pickIndex, 0, n - 1);
            if (!(kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) return;
            ClosePicker();
            if (pickTab == 0)
            {
                string id = pickZones[pickIndex].id;                                 // no skipping — cutscenes outside the zone are dropped, the zone / boss scenes still play
                if (shift) ReplayZone(id);
                else if (MapEncounterDirector.Instance != null) MapEncounterDirector.Instance.DebugJumpToZone(id);
            }
            else if (pickTab == 1) GoToBossAnyMap(AllBosses[pickIndex][0], AllBosses[pickIndex][1]);
            else GoMap(player != null ? player.transform : null, pickMaps[pickIndex], shift);
        }

        // ------------------------------------------------------------------ F5 / F6 / F7
        private static void SolveAndGiveKeys(MapEncounterDirector dir)
        {
            if (MapMechanics.Instance != null) MapMechanics.Instance.DebugSolveAll();
            if (dir == null || dir.Flags == null) return;
            int n = 0;
            foreach (var k in KeyRing.Known)
                if (!KeyRing.Has(dir.Flags, k.id)) { dir.Flags.Set(KeyRing.Flag(k.id)); n++; }
            int gates = dir.DebugOpenAllGates();                                      // open every gate of the map
            Hints.Show($"[TEST] Puzzles solved, {n} key item(s) added, {gates} gate(s) opened. Still blocked? Shift+F2 walks through walls.", 3f);
        }

        /// <summary>F6 — unlock EVERY ownable item: Mantras, passive books, katanas (with their own passives), the map's key items.
        /// Does not solve puzzles / open gates (that is F5). book_ / weapon_ flags carry over to the next map (GameSession) like real pickups.
        /// Switch katanas in the inventory (B).</summary>
        private static readonly string[] PassiveFlags =
        {
            TurnedBlade.Flag,          // book_turned_blade: hitting a bullet reflects it
            "book_purifier",           // Earthshatter clears poison clouds
            "f_upgrade_burn",          // King's Censer (Map 3b)
        };

        private void UnlockEverything(MapEncounterDirector dir)
        {
            int mantras = 0, passives = 0, blades = 0, keys = 0;
            var lo = player.GetComponent<PlayerLoadout>();
            if (lo != null)
                foreach (var s in PlayerLoadout.Library)
                    if (s != null && !lo.owned.Contains(s)) { lo.Unlock(s, false); mantras++; }
            var f = dir != null ? dir.Flags : null;
            if (f != null)
            {
                foreach (var pf in PassiveFlags) if (!f.Has(pf)) { f.Set(pf); passives++; }
                foreach (var w in WeaponRing.All)
                    if (w != null && !WeaponRing.Has(f, w.id)) { f.Set(WeaponRing.Flag(w.id)); blades++; }
                foreach (var k in KeyRing.Known)
                    if (!KeyRing.Has(f, k.id)) { f.Set(KeyRing.Flag(k.id)); keys++; }
            }
            string lifted = dir != null ? dir.DebugLiftRestraints(false) : "";      // Ren back in normal clothes + sword + flasks + Mantras
            // every flask slot found in Maps 1–4 (after lifting restraints — lifting them restores the old slot count)
            int slots = Mathf.Max(0, PlayerCombat.MaxFlaskSlots - player.maxPotions);
            player.maxPotions = Mathf.Max(player.maxPotions, PlayerCombat.MaxFlaskSlots);
            player.RefillPotions();
            Hints.Show($"[TEST] Unlocked: {mantras} mantra(s), {passives} passive(s), {blades} katana(s), {keys} key item(s), +{slots} flask slot(s) ({player.maxPotions} total)." +
                       (lifted.Length > 0 ? " Restraints lifted: normal clothes, blade, flasks, mantras." : "") + " Flasks refilled. [Inventory]", 3.5f);
        }

        private static void HurtBoss(MapEncounterDirector dir, bool toPhase2)
        {
            var bh = dir.Boss != null ? dir.Boss.Health : null;
            if (bh == null || bh.IsDead) { Hints.Show("[TEST] No boss in this fight", 1.5f); return; }
            if (toPhase2)
            {
                float target = bh.Max * 0.49f;
                if (bh.Current > target) bh.TakeDamage(bh.Current - target);
                Hints.Show("[TEST] Boss forced into phase 2", 1.5f);
            }
            else bh.TakeDamage(bh.Max * 0.25f);
        }

        // ------------------------------------------------------------------ F8 / F9
        /// <summary>(Shift+F8) Log every HP loss of enemies / the player to the Console (with the caller) — to audit damage.</summary>
        private static void ToggleDamageLog()
        {
            logDamage = !logDamage;
            if (logDamage) Health.AnyDamaged += LogDamage; else Health.AnyDamaged -= LogDamage;
            Hints.Show(logDamage ? "[TEST] Damage log: ON (Console)" : "[TEST] Damage log: OFF", 1.5f);
        }

        private static void LogDamage(Health h, float amount, float before)
        {
            var ai = h.GetComponent<EnemyAI>();
            string who = ai != null ? $"{ai.displayName} ({h.name})" : h.name;
            string def = ai != null && ai.definition != null ? ai.definition.name : "-";
            Debug.Log($"[DMG] {who}: -{amount:0.#}  {before:0.#} -> {h.Current:0.#} / {h.Max:0.#}  def={def}  frame={Time.frameCount}");
        }

        /// <summary>(F9) Reload the map from the save — test Continue without going through the title screen.</summary>
        private static void ReloadSave()
        {
            if (!SaveGame.Exists) { Hints.Show("[TEST] No save yet - rest at a bonfire first", 1.5f); return; }
            SaveGame.RequestLoad();
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        // ------------------------------------------------------------------ key panel + TEST badge + picker
        private void OnGUI()
        {
            // style rebuilt after hot reload (GUI.skin can be null outside OnGUI)
            if (boxStyle == null || badgeStyle == null || rowStyle == null)
            {
                boxStyle = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 15, richText = true, padding = new RectOffset(12, 12, 10, 10) };
                boxStyle.normal.textColor = Color.white;
                badgeStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, fontSize = 13, richText = true };
                rowStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, fontSize = 15, richText = true, clipping = TextClipping.Clip };
            }
            float k = Screen.height / 1080f;
            var old = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(k, k, 1f));
            float w = Screen.width / k;

            bool god = player != null && player.GodMode;
            if (god || ghost || !Mathf.Approximately(Speed, 1f) || logDamage || CutsceneSkip.Always)
            {
                string badge = "<color=#ffcc66><b>TEST</b></color>" + (god ? "  GOD" : "") + (ghost ? "  GHOST" : "") +
                               (!Mathf.Approximately(Speed, 1f) ? $"  {Speed:0.##}x" : "") + (logDamage ? "  DMG LOG" : "") +
                               (CutsceneSkip.Always ? "  SKIP CUTSCENES" : "") + "   <color=#999999>F1 help</color>";
                GUI.Label(new Rect(0f, 6f, w, 24f), badge, badgeStyle);
            }
            else if (player != null) GUI.Label(new Rect(0f, 6f, w, 24f), "<color=#777777>F1 test keys</color>", badgeStyle);   // always remind that test keys exist
            if (skipScaled) GUI.Label(new Rect(0f, 30f, w, 24f), "<color=#ffcc66><b>>> SKIPPING CUTSCENE >></b></color>", badgeStyle);
            if (showHelp)
            {
                var dir = MapEncounterDirector.Instance;
                var sb = new System.Text.StringBuilder("<b>TEST KEYS</b>  <color=#999999>(Editor / Development Build only)</color>\n\n");
                foreach (var line in Help) sb.Append(line).Append('\n');
                sb.Append("\n<b>State</b>\n");
                sb.Append($"God: {(god ? "ON" : "off")}    Walls: {(ghost ? "GHOST" : "solid")}    Speed: {Speed:0.##}x    Damage log: {(logDamage ? "ON" : "off")}\n");
                sb.Append($"FPS: {1000f / Mathf.Max(0.1f, frameMs):0}  ({frameMs:0.0} ms, worst {worstMs:0.0} ms / 2 s)    Map lights on: {LightCuller.Active}/{LightCuller.Total}\n");
                sb.Append($"Scene: {SceneManager.GetActiveScene().name}\n");
                if (dir != null)
                {
                    sb.Append($"Zone: {(string.IsNullOrEmpty(dir.CurrentZoneTitle) ? "-" : dir.CurrentZoneTitle)}  ({dir.DebugZoneHere()})\n");
                    var bh = dir.Boss != null ? dir.Boss.Health : null;
                    if (bh != null && !bh.IsDead) sb.Append($"Boss: {dir.Boss.displayName}  {bh.Current:0}/{bh.Max:0}\n");
                }
                if (player != null) sb.Append($"Flasks: {player.Potions}/{player.maxPotions}");
                GUI.Box(new Rect(w - 940f, 40f, 920f, 470f), sb.ToString(), boxStyle);
            }
            if (picker) DrawPicker();
            GUI.matrix = old;
        }

        /// <summary>Picker (F10) on the left edge — at most 20 rows, scrolls with the selected row.</summary>
        private void DrawPicker()
        {
            const float X = 20f, Y = 40f, W = 640f, Row = 26f;
            const int Visible = 20;
            int n = PickCount;
            float h = 96f + Visible * Row;
            GUI.Box(new Rect(X, Y, W, h), "", boxStyle);
            string head = (pickTab == 0 ? "<b>[ ZONES ]</b>   <color=#888888>bosses   maps</color>"
                           : pickTab == 1 ? "<color=#888888>zones</color>   <b>[ BOSSES ]</b>   <color=#888888>maps</color>"
                           : "<color=#888888>zones   bosses</color>   <b>[ MAPS ]</b>") + "     <color=#999999>Tab switch</color>";
            GUI.Label(new Rect(X + 14f, Y + 8f, W - 28f, Row), head, rowStyle);
            GUI.Label(new Rect(X + 14f, Y + 8f + Row, W - 28f, Row),
                      "<color=#999999>Up/Down select · Enter go · Shift+Enter replay from scratch · F10 / Backspace close</color>", rowStyle);
            if (n == 0)
            {
                GUI.Label(new Rect(X + 14f, Y + 20f + Row * 2f, W - 28f, Row), "<color=#ff8866>(empty)</color>", rowStyle);
                return;
            }
            int first = Mathf.Clamp(pickIndex - Visible / 2, 0, Mathf.Max(0, n - Visible));
            string scene = SceneManager.GetActiveScene().name;
            for (int r = 0; r < Visible && first + r < n; r++)
            {
                int i = first + r;
                bool sel = i == pickIndex;
                string text;
                if (pickTab == 0)
                {
                    var z = pickZones[i];
                    text = (z.here ? "<color=#66ccff>></color> " : "  ") + z.title + "  <color=#888888>" + z.id + (z.tag.Length > 0 ? " · " + z.tag : "") + "</color>"
                           + (z.cleared ? "  <color=#77dd77>cleared</color>" : "");
                }
                else if (pickTab == 1)
                {
                    var b = AllBosses[i];
                    text = (b[0] == scene ? "<color=#66ccff>></color> " : "  ") + b[2] + "  <color=#888888>" + b[0] + "</color>";
                }
                else
                {
                    string m = pickMaps[i];
                    text = (m == scene ? "<color=#66ccff>></color> " : "  ") + m;
                }
                if (sel) text = "<color=#ffcc66><b>" + StripColors(text) + "</b></color>";
                GUI.Label(new Rect(X + 14f, Y + 20f + Row * (2 + r), W - 28f, Row), text, rowStyle);
            }
            if (n > Visible)
                GUI.Label(new Rect(X + 14f, Y + h - Row - 6f, W - 28f, Row), $"<color=#888888>{pickIndex + 1} / {n}</color>", rowStyle);
        }

        private static string StripColors(string s) => System.Text.RegularExpressions.Regex.Replace(s, "<[^>]+>", "");

        private void OnDestroy()
        {
            if (logDamage) { Health.AnyDamaged -= LogDamage; logDamage = false; }
            if (picker) ClosePicker();
            if (!Mathf.Approximately(Speed, 1f) && Mathf.Approximately(Time.timeScale, Speed)) Time.timeScale = 1f;
        }
    }
}
#endif
