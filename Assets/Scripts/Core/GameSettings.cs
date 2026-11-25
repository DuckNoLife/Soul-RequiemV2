using System;
using UnityEngine;

namespace SoulRequiem.Core
{
    /// <summary>
    /// PLAYER SETTINGS (Settings panel). Saved in PlayerPrefs "set_*", loaded at startup, Apply() pushes them to Unity.
    ///   Audio    : Master (AudioListener.volume), Music (MusicRunner), Sfx (SfxRunner).
    ///   Video    : resolution (builds only), fullscreen, VSync.
    ///   Gameplay : HUD size, damage numbers over enemies, screen shake, red low-HP vignette, reduced flashing.
    /// Adding a setting: add the field + default here, read / write it in Load / Save, add a row in RequiemMenus (Settings tabs).
    /// </summary>
    public static class GameSettings
    {
        // Default values (reused by ResetDefaults)
        private const float DefMaster = 1f, DefMusic = 0.8f, DefSfx = 1f, DefHud = 1f, DefShake = 1f;

        public static float Master = DefMaster, Music = DefMusic, Sfx = DefSfx;
        public static bool Fullscreen = true, VSync = true;
        public static int ResolutionIndex = -1;            // -1 = current resolution
        public static float HudScale = DefHud;             // 0.8 - 1.25
        public static bool EnemyHpNumbers = true;
        public static float ScreenShake = DefShake;        // 0 - 1
        public static bool LowHpVignette = true;
        public static bool ReduceFlashing = false;

        public static event Action Changed;
        private static bool loaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot() { Load(); Apply(); }

        public static void Load()
        {
            loaded = true;
            Master = PlayerPrefs.GetFloat("set_master", DefMaster);
            Music = PlayerPrefs.GetFloat("set_music", DefMusic);
            Sfx = PlayerPrefs.GetFloat("set_sfx", DefSfx);
            Fullscreen = PlayerPrefs.GetInt("set_fullscreen", Screen.fullScreen ? 1 : 0) == 1;
            VSync = PlayerPrefs.GetInt("set_vsync", 1) == 1;
            ResolutionIndex = PlayerPrefs.GetInt("set_res", -1);
            HudScale = PlayerPrefs.GetFloat("set_hud", DefHud);
            EnemyHpNumbers = PlayerPrefs.GetInt("set_enemyhp", 1) == 1;
            ScreenShake = PlayerPrefs.GetFloat("set_shake", DefShake);
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

        /// <summary>Apply the settings (call after every change). Resolution only changes in builds (ignored in the Editor).</summary>
        public static void Apply()
        {
            if (!loaded) Load();
            AudioListener.volume = Mathf.Clamp01(Master);
            QualitySettings.vSyncCount = VSync ? 1 : 0;
#if !UNITY_EDITOR
            var list = Screen.resolutions;
            var mode = Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            if (ResolutionIndex >= 0 && ResolutionIndex < list.Length) Screen.SetResolution(list[ResolutionIndex].width, list[ResolutionIndex].height, mode);
            else Screen.fullScreenMode = mode;
#endif
            Changed?.Invoke();
        }

        public static void ResetDefaults()
        {
            Master = DefMaster; Music = DefMusic; Sfx = DefSfx; Fullscreen = true; VSync = true; ResolutionIndex = -1;
            HudScale = DefHud; EnemyHpNumbers = true; ScreenShake = DefShake; LowHpVignette = true; ReduceFlashing = false;
            Save(); Apply();
        }
    }
}
