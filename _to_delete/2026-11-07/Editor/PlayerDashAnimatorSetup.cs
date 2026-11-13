// Soul Requiem — gắn animation Dash (lướt/roll) vào Animator của Player.
// Menu: Soul Requiem ▸ Gắn animation Dash vào Animator
//
// Animator hiện có 4 state rời North/East/South/West_Dashing nhưng KHÔNG có tham số, KHÔNG có đường chuyển,
// nên SetTrigger("DashTrigger") trong PlayerCombat không làm gì. Lệnh này:
//   1. Thêm trigger "DashTrigger" (giữ DirX, DirY, Speed).
//   2. Tạo state "Dash" = Blend Tree 2D (DirX, DirY) từ 4 clip *_Dashing: N(0,1) E(1,0) S(0,-1) W(-1,0).
//   3. Any State -> Dash khi DashTrigger (không chờ, chuyển ngay, không tự chuyển lại chính nó).
//   4. Dash -> Idle khi clip chạy hết (exit time 1). Idle tự sang Run nếu đang giữ phím di chuyển (Speed > 0.1).
//   5. Xoá 4 state rời *_Dashing (đã gộp vào Dash). Chạy lại nhiều lần vẫn an toàn.
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using SoulRequiem.Core;
using SoulRequiem.Gameplay;

namespace SoulRequiem.EditorTools
{
    public static class PlayerDashAnimatorSetup
    {
        const string Trigger = "DashTrigger";

        // 07/11: menu gom về SoulRequiemMenu.cs (tiếng Anh) — trước: [MenuItem("Soul Requiem/Gắn animation Dash vào Animator")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Soul Requiem", "Hãy dừng Play Mode trước khi chạy lệnh này.", "OK");
                return;
            }
            var pc = Object.FindFirstObjectByType<PlayerController>();
            var animator = pc != null ? pc.GetComponentInChildren<Animator>() : null;
            var ctrl = animator != null ? animator.runtimeAnimatorController as AnimatorController : null;
            if (ctrl == null)
            {
                EditorUtility.DisplayDialog("Soul Requiem", "Không tìm thấy Animator Controller của Player trong scene.", "OK");
                return;
            }
            Undo.RegisterCompleteObjectUndo(ctrl, "Setup Dash");

            // 1. tham số
            if (!ctrl.parameters.Any(p => p.name == Trigger))
                ctrl.AddParameter(Trigger, AnimatorControllerParameterType.Trigger);
            foreach (var f in new[] { "DirX", "DirY" })
                if (!ctrl.parameters.Any(p => p.name == f)) ctrl.AddParameter(f, AnimatorControllerParameterType.Float);

            var sm = ctrl.layers[0].stateMachine;

            // 2. lấy 4 clip lướt: từ các state *_Dashing có sẵn, hoặc tìm theo tên trong project
            AnimationClip Find(string dir)
            {
                var st = sm.states.Select(c => c.state).FirstOrDefault(s => s.name == dir + "_Dashing");
                if (st != null && st.motion is AnimationClip c0) return c0;
                var guid = AssetDatabase.FindAssets(dir + "_Dashing t:AnimationClip").FirstOrDefault();
                return guid == null ? null : AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guid));
            }
            var n = Find("North"); var e = Find("East"); var s2 = Find("South"); var w = Find("West");
            if (n == null || e == null || s2 == null || w == null)
            {
                EditorUtility.DisplayDialog("Soul Requiem", "Thiếu clip: cần đủ North/East/South/West_Dashing.", "OK");
                return;
            }

            // state Dash (tạo mới hoặc dùng lại)
            var dash = sm.states.Select(c => c.state).FirstOrDefault(s => s.name == "Dash");
            if (dash == null) dash = sm.AddState("Dash", new Vector3(560, 420, 0));
            var tree = new BlendTree
            {
                name = "Dash Blend Tree",
                blendType = BlendTreeType.SimpleDirectional2D,
                blendParameter = "DirX",
                blendParameterY = "DirY",
                hideFlags = HideFlags.HideInHierarchy
            };
            AssetDatabase.AddObjectToAsset(tree, ctrl);
            tree.AddChild(n, new Vector2(0, 1));
            tree.AddChild(e, new Vector2(1, 0));
            tree.AddChild(s2, new Vector2(0, -1));
            tree.AddChild(w, new Vector2(-1, 0));
            if (dash.motion is BlendTree old && old != tree && AssetDatabase.GetAssetPath(old) == AssetDatabase.GetAssetPath(ctrl))
                Object.DestroyImmediate(old, true);
            dash.motion = tree;
            dash.writeDefaultValues = true;

            // 3. Any State -> Dash
            foreach (var t in sm.anyStateTransitions.Where(t => t.destinationState == dash).ToArray())
                sm.RemoveAnyStateTransition(t);
            var any = sm.AddAnyStateTransition(dash);
            any.AddCondition(AnimatorConditionMode.If, 0, Trigger);
            any.hasExitTime = false;
            any.duration = 0f;
            any.canTransitionToSelf = false;

            // 4. Dash -> Idle khi chạy hết clip
            var idle = sm.defaultState;
            foreach (var t in dash.transitions.ToArray()) dash.RemoveTransition(t);
            var back = dash.AddTransition(idle);
            back.hasExitTime = true;
            back.exitTime = 1f;
            back.duration = 0.05f;

            // 5. bỏ 4 state rời
            foreach (var child in sm.states.Select(c => c.state).Where(s => s.name.EndsWith("_Dashing")).ToArray())
                sm.RemoveState(child);

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            float len = new[] { n, e, s2, w }.Max(c => c.length);
            var kit = pc.GetComponent<PlayerCombat>();
            string note = kit != null && len > kit.rollIFrames + 0.05f
                ? $"\n\nLưu ý: clip Dash dài {len:0.00}s, i-frame Roll đang {kit.rollIFrames:0.00}s — có thể tăng rollIFrames cho khớp."
                : "";
            Debug.Log($"[Soul Requiem] Đã gắn Dash vào '{ctrl.name}': trigger {Trigger}, Blend Tree 4 hướng, clip {len:0.00}s.");
            EditorUtility.DisplayDialog("Soul Requiem",
                $"Đã gắn animation Dash vào Animator '{ctrl.name}'.\nAny State → Dash (DashTrigger) → Idle." + note, "OK");
        }
    }
}
