#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.InputSystem;
using SoulRequiem.Core;
using SoulRequiem.Gameplay;

namespace SoulRequiem.DevTools
{
    /// <summary>
    /// Phím test cho người làm game / người test. CHỈ tồn tại trong Unity Editor và bản "Development Build" —
    /// bản phát hành (Release) không biên dịch file này, người chơi không bấm nhầm được.
    ///
    /// 04/11 (sắp lại theo nhóm việc của người test; F10 cũ gộp vào F6 / F9):
    ///   F1  bảng phím + trạng thái test (bật/tắt)
    ///   F2  bất tử: không gục, máu + bình luôn đầy                F3  hạ hết quái (cả hang ra quái)
    ///   F4  sang bước cốt truyện kế (khu có `story`; bật cờ các bước trước) · Shift: lùi · Ctrl: tới lối ra của map
    ///   F5  giải mọi câu đố / vật cản + nhận mọi chìa, đồ then chốt của map
    ///   F6  MỞ KHOÁ TẤT CẢ (11/11 §220): mọi Mantra, sách nội tại (Turned Blade, Purifier), mọi thanh katana, đồ then chốt của map + đầy bình
    ///   F7  boss đang đánh −25% máu · Shift: nhảy thẳng vào pha 2 (49% máu)
    ///   F8  tốc độ game 1× → 0.5× → 0.25× → 2× (soi báo trước của chiêu) · Shift: ghi sát thương vào Console ([DMG])
    ///   F9  tải lại từ bản lưu (lửa trại gần nhất) · Shift: tự gục (xem chết / hồi sinh)
    ///   F10 đổi thanh katana (vòng qua mọi Resources/Weapons/*.asset) — 09/11 §202
    /// Khi còn cheat ảnh hưởng số liệu (bất tử, tốc độ ≠ 1×, ghi sát thương) -> nhãn "TEST" nhỏ ở mép trên màn hình.
    /// Tự gắn vào mỗi scene khi chạy (không cần kéo vào scene). Thêm phím mới: thêm một nhánh trong <see cref="Update"/> + một dòng trong <see cref="Help"/>.
    /// </summary>
    public class DevCheats : MonoBehaviour
    {
        private static readonly float[] Speeds = { 1f, 0.5f, 0.25f, 2f };
        private static readonly string[] Help =
        {
            "F1   This panel",
            "F2   God mode (never die, HP + flasks always full)",
            "F3   Defeat every enemy (caves finish spawning)",
            "F4   Next story step   Shift: previous   Ctrl: map exit",
            "F5   Solve all puzzles + get every key item of the map",
            "F6   Unlock EVERYTHING: Mantras, passives, katanas, key items",
            "F7   Boss -25% HP   Shift: jump to phase 2",
            "F8   Game speed 1x / 0.5x / 0.25x / 2x   Shift: damage log",
            "F9   Reload last save   Shift: die (test death / respawn)",
            "F10  Next katana (look + stats)",
        };

        private PlayerCombat player;
        private static bool logDamage;
        private bool showHelp;
        private int speedIndex;
        private GUIStyle boxStyle, badgeStyle;
        private float frameMs = 16.7f, worstMs;                 // 05/11: đo hiệu năng cho bảng F1
        private float worstReset;

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
            if (kb.f1Key.wasPressedThisFrame) showHelp = !showHelp;           // bảng phím xem được cả lúc menu mở
            float ms = Time.unscaledDeltaTime * 1000f;
            frameMs = Mathf.Lerp(frameMs, ms, 0.05f);
            if (Time.unscaledTime > worstReset) { worstReset = Time.unscaledTime + 2f; worstMs = 0f; }
            worstMs = Mathf.Max(worstMs, ms);
            KeepSpeed();

            if (player == null) player = FindFirstObjectByType<PlayerCombat>();   // scene mới -> tìm lại
            if (player == null || player.Health == null) return;
            if (player.GodMode) { player.Health.Heal(player.Health.Max); player.RefillPotions(); }

            // F9 chạy được cả lúc đang gục (thử lại nhanh)
            if (kb.f9Key.wasPressedThisFrame && !shift) { ReloadSave(); return; }
            if (GameInput.Blocked || player.Health.IsDead) return;                       // menu đang mở / đang gục

            var dir = MapEncounterDirector.Instance;
            if (kb.f2Key.wasPressedThisFrame)
            {
                player.GodMode = !player.GodMode;
                Hints.Show(player.GodMode ? "[TEST] God mode: ON" : "[TEST] God mode: OFF", 1.5f);
            }
            if (dir != null && kb.f3Key.wasPressedThisFrame) dir.DebugKillAll();
            if (dir != null && kb.f4Key.wasPressedThisFrame)
            {
                if (ctrl) dir.DebugGoToExit();
                else dir.DebugStepZone(shift ? -1 : 1);
            }
            if (kb.f5Key.wasPressedThisFrame) SolveAndGiveKeys(dir);
            if (kb.f6Key.wasPressedThisFrame) UnlockEverything(dir);
            if (dir != null && kb.f7Key.wasPressedThisFrame) HurtBoss(dir, shift);
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
            if (kb.f10Key.wasPressedThisFrame) CycleKatana();
        }

        /// <summary>(F10) 09/11 §202: đổi sang thanh katana kế tiếp (thử ngoại hình / số liệu từng thanh).</summary>
        private void CycleKatana()
        {
            var all = Resources.LoadAll<WeaponDefinition>("Weapons");
            if (all == null || all.Length == 0) return;
            System.Array.Sort(all, (a, b) => string.CompareOrdinal(a.name, b.name));
            int i = System.Array.IndexOf(all, player.Weapon);
            var w = all[(i + 1) % all.Length];
            player.Equip(w);
            Hints.Show("[TEST] Katana: " + w.displayName, 1.5f);
        }

        /// <summary>Giữ tốc độ test: các hệ thống khác (đóng menu, hết hit-stop, cảnh diễn) trả timeScale về 1 -> đặt lại.
        /// Không đụng khi game đang dừng (timeScale 0) hay đang hit-stop.</summary>
        private void KeepSpeed()
        {
            if (!Mathf.Approximately(Speed, 1f) && Mathf.Approximately(Time.timeScale, 1f)) Time.timeScale = Speed;
        }

        // ------------------------------------------------------------------ F5 / F6 / F7
        private static void SolveAndGiveKeys(MapEncounterDirector dir)
        {
            if (MapMechanics.Instance != null) MapMechanics.Instance.DebugSolveAll();
            if (dir == null || dir.Flags == null) return;
            int n = 0;
            foreach (var k in KeyRing.Known)
                if (!KeyRing.Has(dir.Flags, k.id)) { dir.Flags.Set(KeyRing.Flag(k.id)); n++; }
            if (n > 0) Hints.Show($"[TEST] Puzzles solved + {n} key item(s) added [Inventory]", 2f);
        }

        /// <summary>11/11 (§220): F6 — mở khoá TẤT CẢ đồ sở hữu được: Mantra, sách nội tại, katana (kèm nội tại riêng), đồ then chốt của map.
        /// Không giải câu đố / không mở cổng (việc của F5). Cờ book_ / weapon_ đi theo sang map sau (GameSession) như khi nhặt thật.</summary>
        private static readonly string[] PassiveFlags =
        {
            TurnedBlade.Flag,          // book_turned_blade: chém trúng đạn thì dội ngược
            "book_purifier",           // Earthshatter dọn mây độc
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
            player.RefillPotions();
            Hints.Show($"[TEST] Unlocked: {mantras} mantra(s), {passives} passive(s), {blades} katana(s), {keys} key item(s). Flasks refilled. [Inventory]", 3f);
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
        /// <summary>(Shift+F8) Ghi mọi lần quái / người chơi mất máu vào Console (kèm nơi gọi) — để soát sát thương.</summary>
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

        /// <summary>(F9) Tải lại map từ bản lưu — thử Continue mà không cần qua màn hình tiêu đề.</summary>
        private static void ReloadSave()
        {
            if (!SaveGame.Exists) { Hints.Show("[TEST] No save yet - rest at a bonfire first", 1.5f); return; }
            SaveGame.RequestLoad();
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }

        // ------------------------------------------------------------------ bảng phím + nhãn TEST
        private void OnGUI()
        {
            // style tạo lại sau hot reload (GUI.skin có thể null ngoài OnGUI)
            if (boxStyle == null || badgeStyle == null)
            {
                boxStyle = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 15, richText = true, padding = new RectOffset(12, 12, 10, 10) };
                boxStyle.normal.textColor = Color.white;
                badgeStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, fontSize = 13, richText = true };
            }
            float k = Screen.height / 1080f;
            var old = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(k, k, 1f));
            float w = Screen.width / k;

            bool god = player != null && player.GodMode;
            if (god || !Mathf.Approximately(Speed, 1f) || logDamage)
            {
                string badge = "<color=#ffcc66><b>TEST</b></color>" + (god ? "  GOD" : "") +
                               (!Mathf.Approximately(Speed, 1f) ? $"  {Speed:0.##}x" : "") + (logDamage ? "  DMG LOG" : "") + "   <color=#999999>F1 help</color>";
                GUI.Label(new Rect(0f, 6f, w, 24f), badge, badgeStyle);
            }
            if (showHelp)
            {
                var dir = MapEncounterDirector.Instance;
                var sb = new System.Text.StringBuilder("<b>TEST KEYS</b>  <color=#999999>(Editor / Development Build only)</color>\n\n");
                foreach (var line in Help) sb.Append(line).Append('\n');
                sb.Append("\n<b>State</b>\n");
                sb.Append($"God: {(god ? "ON" : "off")}    Speed: {Speed:0.##}x    Damage log: {(logDamage ? "ON" : "off")}\n");
                sb.Append($"FPS: {1000f / Mathf.Max(0.1f, frameMs):0}  ({frameMs:0.0} ms, worst {worstMs:0.0} ms / 2 s)    Map lights on: {LightCuller.Active}/{LightCuller.Total}\n");
                if (dir != null)
                {
                    sb.Append($"Zone: {(string.IsNullOrEmpty(dir.CurrentZoneTitle) ? "-" : dir.CurrentZoneTitle)}\n");
                    var bh = dir.Boss != null ? dir.Boss.Health : null;
                    if (bh != null && !bh.IsDead) sb.Append($"Boss: {dir.Boss.displayName}  {bh.Current:0}/{bh.Max:0}\n");
                }
                if (player != null) sb.Append($"Flasks: {player.Potions}/{player.maxPotions}");
                GUI.Box(new Rect(w - 600f, 40f, 580f, 350f), sb.ToString(), boxStyle);
            }
            GUI.matrix = old;
        }

        private void OnDestroy()
        {
            if (logDamage) { Health.AnyDamaged -= LogDamage; logDamage = false; }
            if (!Mathf.Approximately(Speed, 1f) && Mathf.Approximately(Time.timeScale, Speed)) Time.timeScale = 1f;
        }
    }
}
#endif
