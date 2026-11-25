using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Shared TIME STOP (Absalom — Map 4 final boss; reusable for Map 5).
    /// Does NOT use Time.timeScale (the one stopping time must still move, UI clocks still run). Begin() freezes:
    ///   enemies (EnemyAI.Held + Animator.speed 0 + velocity 0), normal bullets (Projectile) and duo bullets (DuoBullet) — components disabled;
    ///   Absalom's bullets (HallBullet) read <see cref="Active"/> themselves. The player is locked separately by the calling mechanism (needs MechanismContext).
    /// Grey(w): the screen fades to greyscale (global URP Volume, ColorAdjustments saturation −85) — turns on the camera's renderPostProcessing when needed.
    /// Scene change mid-way -> everything is restored automatically.
    /// Called by: AbsalomHallMechanism.TimeStop.
    /// </summary>
    public static class TimeStop
    {
        public static bool Active { get; private set; }

        private static readonly List<EnemyAI> held = new List<EnemyAI>();
        private static readonly List<Animator> anims = new List<Animator>();
        private static readonly List<float> animSpeeds = new List<float>();
        private static readonly List<Behaviour> disabled = new List<Behaviour>();
        private static Volume volume;
        private static bool ppForced, ppWas;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene s, LoadSceneMode m)
        {
            Active = false;
            held.Clear(); anims.Clear(); animSpeeds.Clear(); disabled.Clear();
            volume = null; ppForced = false;
        }

        /// <summary>Freeze everything in the scene (except the caller — Absalom is not an EnemyAI).</summary>
        public static void Begin()
        {
            if (Active) return;
            Active = true;
            foreach (var e in Object.FindObjectsByType<EnemyAI>(FindObjectsSortMode.None))
            {
                if (e == null || e.Health == null || e.Health.IsDead) continue;
                if (!e.Held) { e.Held = true; held.Add(e); }
                var an = e.GetComponentInChildren<Animator>();
                if (an != null && an.speed > 0f) { anims.Add(an); animSpeeds.Add(an.speed); an.speed = 0f; }
                var rb = e.GetComponent<Rigidbody2D>();
                if (rb != null) rb.linearVelocity = Vector2.zero;
            }
            foreach (var p in new List<Projectile>(Projectile.Live))
                if (p != null && p.enabled) { p.enabled = false; disabled.Add(p); }
            foreach (var b in new List<DuoBullet>(DuoBullet.Live))
                if (b != null && b.enabled) { b.enabled = false; disabled.Add(b); }
        }

        /// <summary>Time runs again.</summary>
        public static void End()
        {
            if (!Active) return;
            Active = false;
            foreach (var e in held) if (e != null) e.Held = false;
            for (int i = 0; i < anims.Count; i++) if (anims[i] != null) anims[i].speed = animSpeeds[i];
            foreach (var b in disabled) if (b != null) b.enabled = true;
            held.Clear(); anims.Clear(); animSpeeds.Clear(); disabled.Clear();
        }

        /// <summary>Greyscale amount 0..1 (the caller interpolates it over time).</summary>
        public static void Grey(float w)
        {
            w = Mathf.Clamp01(w);
            var cam = Camera.main;
            if (w > 0.001f)
            {
                if (volume == null)
                {
                    var go = new GameObject("[TimeStop Volume]");
                    volume = go.AddComponent<Volume>();
                    volume.isGlobal = true;
                    volume.priority = 100f;
                    var prof = ScriptableObject.CreateInstance<VolumeProfile>();
                    var adj = prof.Add<ColorAdjustments>(true);
                    adj.saturation.Override(-85f);
                    adj.contrast.Override(10f);
                    adj.colorFilter.Override(new Color(0.92f, 0.94f, 1f));
                    volume.sharedProfile = prof;
                }
                if (!ppForced && cam != null)
                {
                    var d = cam.GetUniversalAdditionalCameraData();
                    ppWas = d.renderPostProcessing;
                    d.renderPostProcessing = true;
                    ppForced = true;
                }
            }
            if (volume != null) volume.weight = w;
            if (w <= 0.001f && ppForced)
            {
                if (cam != null) cam.GetUniversalAdditionalCameraData().renderPostProcessing = ppWas;
                ppForced = false;
            }
        }
    }
}
