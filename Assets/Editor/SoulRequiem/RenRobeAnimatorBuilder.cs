// Soul Requiem — Ren in the "acolyte robe" (Map 4, sword taken) — natural motions, no VFX:
//   breathing idle (robe/idle 6) · walk (robe/walk 8) · dodge roll (robe/roll 8) · hurt (robe/hurt 6) · stagger back (robe/stagger 8)
//   · stunned (robe/stunned 6, loop) · fall (robe/death 10) · get up (robe/getup 8). No flask drinking: the flask is taken with the sword.
// FALL / LYING = the GET-UP set PLAYED BACKWARDS (kneel -> sit -> lie back), the lying frame = EXACTLY the first get-up frame.
//   The robe/death set (PixelLab v3) is unusable: the lying frame is cut off at the image edge and lies face down / sideways while getup starts
//   face up -> a visible jump in the Map 4 prison wake-up scene (story commands `ren fall` -> `ren getup`) and on respawn. Left in the folder, NOT used.
// Builds an AnimatorOverrideController from Ren.controller. Robe clips are FITTED TO THE ORIGINAL CLIP LENGTH (same frame count -> reuse the
// original timings, otherwise spread evenly) so states exit exactly when the code expects. Looping clips (Idle/Run/Stunned) keep their own timing.
// Every other state (attacks, skills, drinking…) -> breathing idle (never shows Ren holding the sword). Saved to Resources/Player/RenRobe.overrideController;
// MapRestraint switches to it while PlayerStatus.Disarmed (Map 4: until flag m4_katana) and back afterwards.
// Menu: Soul Requiem ▸ Player ▸ Ren Robe Animator (Map 4). Run AFTER Ren Animator (v2), outside Play Mode.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SoulRequiem.EditorTools
{
    public static class RenRobeAnimatorBuilder
    {
        const string RobeRoot = "Assets/GamePackage/Ren/robe";
        const string ClipDir = RobeRoot + "/Animations";
        const string BaseCtrl = "Assets/GamePackage/Ren/Animations/Ren.controller";
        const string OutPath = "Assets/Resources/Player/RenRobe.overrideController";
        const float IdleFrame = 0.38f, WalkFrame = 0.1f, StunFrame = 0.15f;   // slow breathing (0.2 -> 0.38 s/frame, ~2.3 s per breath)
        /// <summary>true = the robe fall clip is robe/getup reversed (matches the first get-up frame exactly).</summary>
        const bool DeathFromGetup = true;
        /// <summary>Fall timing (seconds / frame, in reversed frame order): standing -> quick kneel, sitting -> slowing lie-back like someone exhausted.</summary>
        static readonly float[] LieTimes = { 0.05f, 0.06f, 0.08f, 0.09f, 0.11f, 0.13f, 0.16f, 0.2f };

        // direction names in Ren.controller clip names -> image folders. Longer names first (NorthEast before East).
        static readonly (string name, string folder)[] Dirs =
        {
            ("NorthEast", "north-east"), ("NorthWest", "north-west"), ("SouthEast", "south-east"), ("SouthWest", "south-west"),
            ("East", "east"), ("North", "north"), ("West", "west"), ("South", "south"),
        };
        /// <summary>Original clip -> robe action (by clip name prefix in RenAnimatorBuilder).</summary>
        static string AnimFor(string clip)
        {
            if (clip.StartsWith("Run_")) return "walk";
            if (clip.StartsWith("Roll_") || clip.StartsWith("Dash")) return "roll";
            if (clip.StartsWith("Stunned_")) return "stunned";
            if (clip.StartsWith("Hurt_")) return "hurt";
            if (clip.StartsWith("Stagger_")) return "stagger";
            if (clip.StartsWith("Death_")) return "death";
            if (clip.StartsWith("Revive_")) return "getup";
            return "idle";
        }

        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { EditorUtility.DisplayDialog("Soul Requiem", "Stop Play Mode first.", "OK"); return; }
            var baseCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(BaseCtrl);
            if (baseCtrl == null) { EditorUtility.DisplayDialog("Soul Requiem", "No Ren.controller yet - run Soul Requiem > Player > Ren Animator (v2) first.", "OK"); return; }
            EnsureFolder(ClipDir); EnsureFolder("Assets/Resources/Player");

            var missing = new List<string>();
            var sets = new Dictionary<string, Dictionary<string, Sprite[]>>();
            foreach (var anim in new[] { "idle", "walk", "roll", "hurt", "stagger", "stunned", "death", "getup" })
            {
                sets[anim] = new Dictionary<string, Sprite[]>();
                foreach (var d in Dirs)
                {
                    var f = Frames($"{RobeRoot}/{anim}/{d.folder}");
                    if (f.Length == 0) missing.Add(anim + "/" + d.folder); else sets[anim][d.folder] = f;
                }
            }
            // fall = get-up played backwards
            if (DeathFromGetup)
            {
                sets["death"] = new Dictionary<string, Sprite[]>();
                foreach (var kv in sets["getup"]) sets["death"][kv.Key] = kv.Value.Reverse().ToArray();
                missing.RemoveAll(m => m.StartsWith("death/"));
            }
            if (!sets["idle"].ContainsKey("south")) { EditorUtility.DisplayDialog("Soul Requiem", "Missing image robe/idle/south.", "OK"); return; }

            var oc = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(OutPath);
            bool isNew = oc == null;
            if (isNew) oc = new AnimatorOverrideController();
            oc.runtimeAnimatorController = baseCtrl;
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            oc.GetOverrides(pairs);
            var made = new Dictionary<string, AnimationClip>();
            var count = new Dictionary<string, int>();
            for (int i = 0; i < pairs.Count; i++)
            {
                var orig = pairs[i].Key;
                string folder = DirOf(orig.name);
                string anim = AnimFor(orig.name);
                var set = sets.ContainsKey(anim) && sets[anim].Count > 0 ? sets[anim] : sets["idle"];
                if (!set.TryGetValue(folder, out var spr)) spr = set.TryGetValue("south", out var s0) ? s0 : sets["idle"]["south"];
                string clipName = "Robe" + orig.name;
                if (!made.TryGetValue(clipName, out var clip)) made[clipName] = clip = Build(spr, orig, clipName, anim);
                pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(orig, clip);
                count[anim] = count.TryGetValue(anim, out var c) ? c + 1 : 1;
            }
            oc.ApplyOverrides(pairs);
            if (isNew) AssetDatabase.CreateAsset(oc, OutPath); else EditorUtility.SetDirty(oc);
            AssetDatabase.SaveAssets();

            string msg = $"RenRobe: {pairs.Count} clips replaced (" + string.Join(", ", count.Select(kv => $"{kv.Key} {kv.Value}")) + $"). Saved {OutPath}.";
            if (missing.Count > 0) msg += "\nMissing (using south instead): " + string.Join(", ", missing);
            Debug.Log(msg);
            EditorUtility.DisplayDialog("Soul Requiem", msg, "OK");
        }

        static string DirOf(string clipName)
        {
            foreach (var d in Dirs) if (clipName.EndsWith(d.name)) return d.folder;
            return "south";
        }

        static Sprite[] Frames(string folder)
        {
            if (!Directory.Exists(folder)) return new Sprite[0];
            return Directory.GetFiles(folder, "*.png").Select(f => f.Replace('\\', '/')).OrderBy(f => f)
                .Select(f => AssetDatabase.LoadAssetAtPath<Sprite>(f)).Where(s => s != null).ToArray();
        }

        /// <summary>Robe clip replacing an original clip. Looping original (Idle, Run, Stunned…) -> loops at the robe's own timing.
        /// One-shot original -> stretched to the exact original length; same frame count reuses the original timings (keeps the fall timing / last-frame hold).</summary>
        static AnimationClip Build(Sprite[] s, AnimationClip orig, string clipName, string anim)
        {
            var origSet = AnimationUtility.GetAnimationClipSettings(orig);
            bool loop = origSet.loopTime;
            var keys = new List<ObjectReferenceKeyframe>();
            if (loop)
            {
                float frame = anim == "walk" ? WalkFrame : anim == "stunned" ? StunFrame : IdleFrame;
                for (int i = 0; i < s.Length; i++) keys.Add(new ObjectReferenceKeyframe { time = i * frame, value = s[i] });
                keys.Add(new ObjectReferenceKeyframe { time = s.Length * frame, value = s[0] });   // close the loop
            }
            else if (anim == "death" && DeathFromGetup)
            {
                float t = 0f;
                for (int i = 0; i < s.Length; i++) { keys.Add(new ObjectReferenceKeyframe { time = t, value = s[i] }); t += LieTimes[Mathf.Min(i, LieTimes.Length - 1)]; }
                keys.Add(new ObjectReferenceKeyframe { time = Mathf.Max(t, orig.length), value = s[s.Length - 1] });   // lie still until the original clip ends
            }
            else
            {
                float len = Mathf.Max(orig.length, 0.05f);
                var ok = OrigKeys(orig);
                if (ok != null && (ok.Length == s.Length || ok.Length == s.Length + 1))
                    for (int i = 0; i < s.Length; i++) keys.Add(new ObjectReferenceKeyframe { time = ok[i].time, value = s[i] });
                else
                    for (int i = 0; i < s.Length; i++) keys.Add(new ObjectReferenceKeyframe { time = len * i / s.Length, value = s[i] });
                keys.Add(new ObjectReferenceKeyframe { time = len, value = s[s.Length - 1] });           // hold the last frame until the original clip ends
            }
            string path = $"{ClipDir}/{clipName}.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            bool isNew = clip == null;
            if (isNew) clip = new AnimationClip();
            clip.ClearCurves();
            clip.frameRate = 100f;
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys.ToArray());
            var st = AnimationUtility.GetAnimationClipSettings(clip); st.loopTime = loop; AnimationUtility.SetAnimationClipSettings(clip, st);
            if (isNew) AssetDatabase.CreateAsset(clip, path); else EditorUtility.SetDirty(clip);
            return clip;
        }

        static ObjectReferenceKeyframe[] OrigKeys(AnimationClip orig)
        {
            foreach (var b in AnimationUtility.GetObjectReferenceCurveBindings(orig))
                if (b.propertyName == "m_Sprite") return AnimationUtility.GetObjectReferenceCurve(orig, b);
            return null;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
