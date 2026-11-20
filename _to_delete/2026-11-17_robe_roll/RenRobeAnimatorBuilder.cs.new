// Soul Requiem — 17/11: Ren "áo tế" (Map 4, bị tước kiếm) — động tác tự nhiên, không VFX:
//   đứng thở (robe/idle 6) · đi bộ (robe/walk 8) · lăn né (robe/roll 8) · bị đánh (robe/hurt 6) · choáng lùi (robe/stagger 8)
//   · choáng đứng (robe/stunned 6, lặp) · ngã (robe/death 10) · đứng dậy (robe/getup 8). Không có uống bình: bình bị thu cùng kiếm.
// Dựng AnimatorOverrideController từ Ren.controller. Clip áo tế được CĂN THEO ĐỘ DÀI clip gốc (cùng số khung -> dùng đúng mốc
// thời gian gốc, khác -> rải đều) để trạng thái thoát đúng lúc code chờ. Clip lặp (Idle/Run/Stunned) giữ nhịp riêng.
// Mọi trạng thái khác (chém, chiêu, uống…) -> đứng thở (không lộ hình Ren cầm kiếm). Lưu vào Resources/Player/RenRobe.overrideController
// -> MapRestraint tự đổi sang khi PlayerStatus.Disarmed (Map 4: tới khi có cờ m4_katana) và trả lại khi hết.
// Menu: Soul Requiem ▸ Player ▸ Build Ren Robe Animator (Map 4). Chạy SAU Build Ren Animator (v2), khi không Play.
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
        const float IdleFrame = 0.2f, WalkFrame = 0.1f, StunFrame = 0.15f;

        // tên hướng trong tên clip của Ren.controller -> thư mục ảnh. Tên dài xét trước (NorthEast trước East).
        static readonly (string name, string folder)[] Dirs =
        {
            ("NorthEast", "north-east"), ("NorthWest", "north-west"), ("SouthEast", "south-east"), ("SouthWest", "south-west"),
            ("East", "east"), ("North", "north"), ("West", "west"), ("South", "south"),
        };
        /// <summary>Clip gốc -> động tác áo tế (theo tiền tố tên clip trong RenAnimatorBuilder).</summary>
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

        [MenuItem("Soul Requiem/Player/Build Ren Robe Animator (Map 4)", false, 323)]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { EditorUtility.DisplayDialog("Soul Requiem", "Hãy dừng Play Mode trước.", "OK"); return; }
            var baseCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(BaseCtrl);
            if (baseCtrl == null) { EditorUtility.DisplayDialog("Soul Requiem", "Chưa có Ren.controller — chạy Build Ren Animator (v2) trước.", "OK"); return; }
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
            if (!sets["idle"].ContainsKey("south")) { EditorUtility.DisplayDialog("Soul Requiem", "Thiếu ảnh robe/idle/south.", "OK"); return; }

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

            string msg = $"RenRobe: {pairs.Count} clip được thay (" + string.Join(", ", count.Select(kv => $"{kv.Key} {kv.Value}")) + $"). Lưu {OutPath}.";
            if (missing.Count > 0) msg += "\nThiếu (dùng hướng south thay): " + string.Join(", ", missing);
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

        /// <summary>Clip áo tế thay cho clip gốc. Gốc lặp (Idle, Run, Stunned…) -> lặp theo nhịp riêng của áo tế.
        /// Gốc một lần -> kéo cho vừa đúng độ dài gốc; cùng số khung thì dùng lại mốc thời gian gốc (giữ nhịp ngã / giữ khung cuối).</summary>
        static AnimationClip Build(Sprite[] s, AnimationClip orig, string clipName, string anim)
        {
            var origSet = AnimationUtility.GetAnimationClipSettings(orig);
            bool loop = origSet.loopTime;
            var keys = new List<ObjectReferenceKeyframe>();
            if (loop)
            {
                float frame = anim == "walk" ? WalkFrame : anim == "stunned" ? StunFrame : IdleFrame;
                for (int i = 0; i < s.Length; i++) keys.Add(new ObjectReferenceKeyframe { time = i * frame, value = s[i] });
                keys.Add(new ObjectReferenceKeyframe { time = s.Length * frame, value = s[0] });   // khép vòng
            }
            else
            {
                float len = Mathf.Max(orig.length, 0.05f);
                var ok = OrigKeys(orig);
                if (ok != null && (ok.Length == s.Length || ok.Length == s.Length + 1))
                    for (int i = 0; i < s.Length; i++) keys.Add(new ObjectReferenceKeyframe { time = ok[i].time, value = s[i] });
                else
                    for (int i = 0; i < s.Length; i++) keys.Add(new ObjectReferenceKeyframe { time = len * i / s.Length, value = s[i] });
                keys.Add(new ObjectReferenceKeyframe { time = len, value = s[s.Length - 1] });           // giữ khung cuối tới hết clip gốc
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
