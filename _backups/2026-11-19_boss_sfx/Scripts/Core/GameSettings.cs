using System;
using UnityEngine;

namespace SoulRequiem.Core
{
    /// <summary>
    /// Cài đặt người chơi (PlayerPrefs "set_*"). Nạp tự động lúc khởi động, Apply() áp lên Unity.
    ///   Âm thanh : Master (AudioListener.volume), Music, Sfx (lưu sẵn cho hệ âm thanh sau này).
    ///   Hình ảnh : độ phân giải, toàn màn hình, VSync.
    ///   Gameplay : cỡ HUD, số máu trên đầu quái, rung màn hình, viền đỏ khi máu thấp, giảm nhấp nháy.
    /// </summary>
    public static class GameSettings
    {
        public static float Master = 1f, Music = 0.8f, Sfx = 1f;
        public static bool Fullscreen = true, VSync = true;
        public static int ResolutionIndex = -1;            // -1 = độ phân giải hiện tại
        public static float HudScale = 1f;                 // 0.8 - 1.25
        public static bool EnemyHpNumbers = true;
        public static float ScreenShake = 1f;              // 0 - 1
        public static bool LowHpVignette = true;
        public static bool ReduceFlashing = false;

        public static event Action Changed;
        private static bool loaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot() { Load(); Apply(); }

        public static void Load()
        {
            loaded = true;
            Master = PlayerPrefs.GetFloat("set_master", 1f);
            Music = PlayerPrefs.GetFloat("set_music", 0.8f);
            Sfx = PlayerPrefs.GetFloat("set_sfx", 1f);
            Fullscreen = PlayerPrefs.GetInt("set_fullscreen", Screen.fullScreen ? 1 : 0) == 1;
            VSync = PlayerPrefs.GetInt("set_vsync", 1) == 1;
            ResolutionIndex = PlayerPrefs.GetInt("set_res", -1);
            HudScale = PlayerPrefs.GetFloat("set_hud", 1f);
            EnemyHpNumbers = PlayerPrefs.GetInt("set_enemyhp", 1) == 1;
            ScreenShake = PlayerPrefs.GetFloat("set_shake", 1f);
            LowHpVignette = PlayerPrefs.GetInt("set_vignette", 1) == 1;
            ReduceFlashing = PlayerPrefs.GetInt("set_noflash", 0) == 1;
        }

        public static void Save()
        {
            PlayerPrefs.SetFloat("set_master", Master);
            PlayerPrefs.SetFloat("set_music", Music);
            PlayerPrefs.SetFloat("set_sfx", Sfx);
            PlayerPrefs.SetInt("set_fullscreen", Fullscreen ? 1 : 0);
            PlayerPrefs.SetInt("set_vsync", VSync ? 1 : 0);
            PlayerPrefs.SetInt("set_res", ResolutionIndex);
            PlayerPrefs.SetFloat("set_hud", HudScale);
            PlayerPrefs.SetInt("set_enemyhp", EnemyHpNumbers ? 1 : 0);
            PlayerPrefs.SetFloat("set_shake", ScreenShake);
            PlayerPrefs.SetInt("set_vignette", LowHpVignette ? 1 : 0);
            PlayerPrefs.SetInt("set_noflash", ReduceFlashing ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// Áp cài đặt (gọi sau mỗi lần đổi). Độ phân giải chỉ đổi trong bản build (Editor bỏ qua).
        public static void Apply()
        {
            if (!loaded) Load();
            AudioListener.volume = Mathf.Clamp01(Master);
            QualitySettings.vSyncCount = VSync ? 1 : 0;
#if !UNITY_EDITOR
            var list = Screen.resolutions;
            if (ResolutionIndex >= 0 && ResolutionIndex < list.Length)
            {
                var r = list[ResolutionIndex];
                Screen.SetResolution(r.width, r.height, Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
            }
            else Screen.fullScreenMode = Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
#endif
            Changed?.Invoke();
        }

        public static void ResetDefaults()
        {
            Master = 1f; Music = 0.8f; Sfx = 1f; Fullscreen = true; VSync = true; ResolutionIndex = -1;
            HudScale = 1f; EnemyHpNumbers = true; ScreenShake = 1f; LowHpVignette = true; ReduceFlashing = false;
            Save(); Apply();
        }
    }
}
