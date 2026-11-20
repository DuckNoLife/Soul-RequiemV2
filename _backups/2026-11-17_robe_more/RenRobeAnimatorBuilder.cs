// Soul Requiem — 17/11: Ren "áo tế" (Map 4, bị tước kiếm) — chỉ 2 động tác đơn giản, không VFX:
//   đứng thở (robe/idle, 6 khung) + đi bộ (robe/walk, template PixelLab walking-8-frames).
// Dựng AnimatorOverrideController từ Ren.controller: Run / Dash / Roll -> đi bộ; MỌI trạng thái khác -> đứng thở
// (để không lộ hình Ren cầm kiếm khi bị đánh / chết). Lưu vào Resources/Player/RenRobe.overrideController
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
        const float IdleFrame = 0.2f, WalkFrame = 0.1f;

        // tên hướng trong tên clip của Ren.controller -> thư mục ảnh. Tên dài xét trước (NorthEast trước East).
        static readonly (string name, string folder)[] Dirs =
        {
            ("NorthEast", "north-east"), ("NorthWest", "north-west"), ("SouthEast", "south-east"), ("SouthWest", "south-west"),
            ("East", "east"), ("North", "north"), ("West", "west"), ("South", "south"),
        };
        static readonly string[] MovePrefixes = { "Run", "Dash", "Roll" };

        [MenuItem("Soul Requiem/Player/Build Ren Robe Animator (Map 4)", false, 323)]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { EditorUtility.DisplayDialog("Soul Requiem", "Hãy dừng Play Mode trước.", "OK"); return; }
            var baseCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(BaseCtrl);
            if (baseCtrl == null) { EditorUtility.DisplayDialog("Soul Requiem", "Chưa có Ren.controller — chạy Build Ren Animator (v2) trước.", "OK"); return; }
            EnsureFolder(ClipDir); EnsureFolder("Assets/Resources/Player");

            var missing = new List<string>();
            var idle = new Dictionary<string, AnimationClip>();
            var walk = new Dictionary<string, AnimationClip>();
            foreach (var d in Dirs)
            {
                var si = Frames($"{RobeRoot}/idle/{d.folder}");
                var sw = Frames($"{RobeRoot}/walk/{d.folder}");
                if (si.Length == 0) missing.Add("idle/" + d.folder); else idle[d.folder] = Loop(si, "RobeIdle_" + d.name, IdleFrame);
                if (sw.Length == 0) missing.Add("walk/" + d.folder); else walk[d.folder] = Loop(sw, "RobeWalk_" + d.name, WalkFrame);
            }
            if (!idle.ContainsKey("south")) { EditorUtility.DisplayDialog("Soul Requiem", "Thiếu ảnh robe/idle/south.", "OK"); return; }

            var oc = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(OutPath);
            bool isNew = oc == null;
            if (isNew) oc = new AnimatorOverrideController();
            oc.runtimeAnimatorController = baseCtrl;
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            oc.GetOverrides(pairs);
            int nWalk = 0, nIdle = 0;
            for (int i = 0; i < pairs.Count; i++)
            {
                var orig = pairs[i].Key;
                string folder = DirOf(orig.name);
                bool move = MovePrefixes.Any(p => orig.name.StartsWith(p));
                var set = move ? walk : idle;
                if (!set.TryGetValue(folder, out var clip)) clip = set.TryGetValue("south", out var s) ? s : idle["south"];
                pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(orig, clip);
                if (move) nWalk++; else nIdle++;
            }
            oc.ApplyOverrides(pairs);
            if (isNew) AssetDatabase.CreateAsset(oc, OutPath); else EditorUtility.SetDirty(oc);
            AssetDatabase.SaveAssets();

            string msg = $"RenRobe: {pairs.Count} clip được thay ({nWalk} -> đi bộ, {nIdle} -> đứng thở). Lưu {OutPath}.";
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

        static AnimationClip Loop(Sprite[] s, string clipName, float frame)
        {
            var keys = s.Select((sp, i) => new ObjectReferenceKeyframe { time = i * frame, value = sp }).ToList();
            keys.Add(new ObjectReferenceKeyframe { time = s.Length * frame, value = s[0] });   // khép vòng cho khung cuối đủ thời lượng
            string path = $"{ClipDir}/{clipName}.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            bool isNew = clip == null;
            if (isNew) clip = new AnimationClip();
            clip.ClearCurves();
            clip.frameRate = 1f / frame;
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys.ToArray());
            var st = AnimationUtility.GetAnimationClipSettings(clip); st.loopTime = true; AnimationUtility.SetAnimationClipSettings(clip, st);
            if (isNew) AssetDatabase.CreateAsset(clip, path); else EditorUtility.SetDirty(clip);
            return clip;
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
