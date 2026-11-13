// Soul Requiem — dựng Animator đầy đủ cho nhân vật Rogue Traveler.
// Menu: Soul Requiem ▸ Dựng Animator nhân vật (Idle, Run, Dash, Iaido)
//
// Lý do: Player.controller cũ (GamePackage/Idle/rotations) và các clip Idle/Run không còn trong project,
// Animator của Player trong scene đang trỏ vào controller bị mất. Lệnh này dựng lại toàn bộ từ ảnh trong
// Assets/GamePackage/Rogue Traveler:
//   Idle  : Blend Tree 8 hướng (breathing-idle, 4 khung, 0,225 s/khung = vòng thở 0,9 s, lặp)
//   Run   : Blend Tree 8 hướng (run, 9 khung, 12 fps, lặp) — state Run chạy x RunAnimSpeed để chân bớt trượt
//   Dash  : Blend Tree 4 hướng từ 4 clip *_Dashing có sẵn (ép chạy 1 lần) — Any State khi DashTrigger
//   Iaido : Blend Tree 4 hướng chính + các hướng chéo nào đã có thư mục iaido/<hướng> (vd. south-west), 14 khung
//           — Any State khi IaidoTrigger. Danh sách hướng được ghi vào PlayerCombat.directions để đòn chém
//           chỉ chọn hướng có animation (không trộn 2 clip sprite).
// Tham số: DirX, DirY, Speed (PlayerController ghi), AtkX, AtkY (hướng đòn chém, chỉ ghi lúc bắt đầu chém),
//          DashTrigger, IaidoTrigger (PlayerCombat ghi).
// Clip lưu ở Rogue Traveler/Animations. Chạy lại nhiều lần an toàn (ghi đè clip, dựng lại controller).
// Chạy khi KHÔNG ở Play Mode.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using SoulRequiem.Core;
using SoulRequiem.Gameplay;

namespace SoulRequiem.EditorTools
{
    public static class PlayerAnimatorBuilder
    {
        const string Root = "Assets/GamePackage/Rogue Traveler";
        const string OutDir = Root + "/Animations";
        const string CtrlPath = OutDir + "/Player.controller";

        struct Dir { public string folder, name; public Vector2 v; public Dir(string f, string n, float x, float y) { folder = f; name = n; v = new Vector2(x, y); } }
        static readonly Dir[] Dir8 =
        {
            new Dir("east", "East", 1, 0), new Dir("north-east", "NorthEast", 0.7071f, 0.7071f),
            new Dir("north", "North", 0, 1), new Dir("north-west", "NorthWest", -0.7071f, 0.7071f),
            new Dir("west", "West", -1, 0), new Dir("south-west", "SouthWest", -0.7071f, -0.7071f),
            new Dir("south", "South", 0, -1), new Dir("south-east", "SouthEast", 0.7071f, -0.7071f),
        };
        static readonly Dir[] Dir4 = { new Dir("east", "East", 1, 0), new Dir("north", "North", 0, 1), new Dir("west", "West", -1, 0), new Dir("south", "South", 0, -1) };

        /// Thời lượng từng khung Iaido (ms) — khớp bản GIF đã duyệt. Khung 5 (index 5) là khung trúng đòn.
        public static readonly int[] IaidoMs = { 110, 110, 150, 70, 40, 70, 90, 90, 90, 90, 90, 110, 110, 140 };
        /// Hoạt ảnh phụ — chỉ dựng khi đã có thư mục ảnh <Root>/<thư mục>/<hướng>/frame_*.png (thiếu hướng nào thì Blend Tree
        /// lấy hướng gần nhất). Thời lượng từng khung (ms):
        public static readonly int[] HurtMs = { 60, 80, 80 };                                   // trúng nhẹ: khung 1 nháy sáng
        public static readonly int[] StompMs = { 80, 100, 130, 60, 120, 100, 90, 80 };         // chiêu F: khung 4 chạm đất ở 0,31 s
        public static readonly int[] DeathMs = { 90, 100, 110, 140, 200, 250, 120, 80, 90, 400 }; // gục: quỳ rồi đổ, khung cuối giữ
        public static readonly int[] ReviveMs = { 250, 150, 150, 200 };                         // đứng dậy ở checkpoint (hướng Nam)
        public static readonly int[] StaggerMs = { 70, 100, 120, 90, 90, 90 };                 // trúng nặng: giật, lùi, gập, đứng lại
        public static readonly int[] DrinkMs = { 70, 70, 70, 90, 90, 90, 90, 80, 70 };          // uống thuốc: rút bình, đưa lên, ngửa cổ uống (khung 4 ~0,21 s), hạ bình
        // Mantra Con Mắt (PixelLab V3, 9 khung = khung đứng + 8): gọi cầu, chắp tay niệm khiên
        public static readonly int[] CastEmbersMs = { 60, 70, 80, 90, 100, 90, 90, 80, 90 };
        public static readonly int[] CastVeilMs = { 60, 70, 80, 90, 110, 100, 90, 80, 90 };
        // Tia (ray-hold): các khung đầu = giơ tay (chạy 1 lần), phần còn lại = giữ tay (lặp tới khi tắt tia)
        public static readonly int[] RayStartMs = { 50, 60, 70, 70, 80 };
        const float RayLoopFrame = 0.1f;
        const float StunFrame = 0.15f;                                                          // choáng: vòng lặp lảo đảo
        const float IdleFrame = 0.225f;
        /// Bước chân trong clip Run ~0,63 đơn vị/vòng (0,75 s) trong khi Player đi moveSpeed 2 = 1,5 đơn vị/vòng.
        /// Chạy clip nhanh hơn để chân bớt trượt (2,4 là khớp hẳn nhưng nhìn quá gấp).
        const float RunAnimSpeed = 1.8f;
        const float RunFrame = 1f / 12f;
        // 01/11/2026: Iaido / Drink / Stagger -> Run KHÔNG dùng Exit Time. Code (PlayerController) giữ Speed = 0 suốt lúc khoá
        // (PlayerCombat.lockTime, healLock, staggerLock) nên chuyển sang Run đúng khung hết khoá. Exit Time chỉ được kiểm tra
        // ĐÚNG 1 khung -> hay bị lỡ -> nhân vật trượt trong tư thế tra kiếm tới hết clip (lỗi "khựng sau khi chém").
        // Hurt / Dash không khoá di chuyển -> vẫn chờ hết clip (bỏ Exit Time thì clip bị cắt ngay khi đang chạy).

        // 07/11: menu gom về SoulRequiemMenu.cs (tiếng Anh) — trước: [MenuItem("Soul Requiem/Dựng Animator nhân vật (Idle, Run, Dash, Iaido)")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Soul Requiem", "Hãy dừng Play Mode trước khi chạy lệnh này.", "OK");
                return;
            }
            var pc = Object.FindFirstObjectByType<PlayerController>();
            var animator = pc != null ? pc.GetComponent<Animator>() : null;
            if (animator == null)
            {
                EditorUtility.DisplayDialog("Soul Requiem", "Không tìm thấy Player (PlayerController + Animator) trong scene đang mở.", "OK");
                return;
            }
            if (!AssetDatabase.IsValidFolder(OutDir)) AssetDatabase.CreateFolder(Root, "Animations");

            var missing = new List<string>();
            // ---- clip
            var idle = new Dictionary<string, AnimationClip>();
            var run = new Dictionary<string, AnimationClip>();
            foreach (var d in Dir8)
            {
                idle[d.folder] = BuildUniform($"{Root}/breathing-idle/{d.folder}", "Idle_" + d.name, IdleFrame, missing);
                run[d.folder] = BuildUniform($"{Root}/run/{d.folder}", "Run_" + d.name, RunFrame, missing);
            }
            var dash = new Dictionary<string, AnimationClip>();
            var iaido = new Dictionary<string, AnimationClip>();
            foreach (var d in Dir4)
            {
                dash[d.folder] = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{Root}/{d.name}_Dashing.anim");
                if (dash[d.folder] == null) missing.Add($"{d.name}_Dashing.anim");
                else
                {
                    // Dash là đòn lướt chạy 1 lần (clip gốc để Loop Time = bật)
                    var ds = AnimationUtility.GetAnimationClipSettings(dash[d.folder]);
                    if (ds.loopTime) { ds.loopTime = false; AnimationUtility.SetAnimationClipSettings(dash[d.folder], ds); EditorUtility.SetDirty(dash[d.folder]); }
                }
                iaido[d.folder] = BuildTimed($"{Root}/iaido/{d.folder}", "Iaido_" + d.name, IaidoMs, missing);
            }
            // hướng chéo: chỉ thêm khi đã có ảnh
            var iaidoDirs = Dir4.ToList();
            foreach (var d in Dir8)
            {
                if (iaido.ContainsKey(d.folder) || !Directory.Exists($"{Root}/iaido/{d.folder}")) continue;
                iaido[d.folder] = BuildTimed($"{Root}/iaido/{d.folder}", "Iaido_" + d.name, IaidoMs, missing);
                iaidoDirs.Add(d);
            }
            // ---- hoạt ảnh phụ (có thư mục nào dựng thư mục đó)
            var hurt = OptionalSet("hurt", "Hurt_", HurtMs, missing);
            var stomp = OptionalSet("stomp", "Stomp_", StompMs, missing);
            var death = OptionalSet("death", "Death_", DeathMs, missing);
            var revive = OptionalSet("getting-up", "Revive_", ReviveMs, missing);
            var stagger = OptionalSet("stagger", "Stagger_", StaggerMs, missing);
            var drink = OptionalSet("drinking", "Drink_", DrinkMs, missing);
            var castE = OptionalSet("cast-embers", "CastEmbers_", CastEmbersMs, missing);
            var castV = OptionalSet("cast-veil", "CastVeil_", CastVeilMs, missing);
            var rayStart = new Dictionary<string, AnimationClip>();
            var rayLoop = new Dictionary<string, AnimationClip>();
            foreach (var d in Dir8)
            {
                string rp = $"{Root}/ray-hold/{d.folder}";
                if (!Directory.Exists(rp)) continue;
                var fr = LoadFrames(rp);
                if (fr.Length < RayStartMs.Length + 1 || fr.Any(x => x == null)) { missing.Add($"{rp} (cần ≥ {RayStartMs.Length + 1} khung, có {fr.Length})"); continue; }
                rayStart[d.folder] = TimedFrom(fr.Take(RayStartMs.Length).ToArray(), "RayStart_" + d.name, RayStartMs);
                var loopKeys = fr.Skip(RayStartMs.Length).Select((sp, i) => new ObjectReferenceKeyframe { time = i * RayLoopFrame, value = sp }).ToArray();
                rayLoop[d.folder] = Save("RayLoop_" + d.name, loopKeys, 1f / RayLoopFrame, true);
            }
            var stun = new Dictionary<string, AnimationClip>();
            foreach (var d in Dir8)
                if (Directory.Exists($"{Root}/stunned/{d.folder}"))
                { var c = BuildUniform($"{Root}/stunned/{d.folder}", "Stunned_" + d.name, StunFrame, missing); if (c != null) stun[d.folder] = c; }
            if (missing.Count > 0)
            {
                EditorUtility.DisplayDialog("Soul Requiem", "Thiếu ảnh/clip:\n" + string.Join("\n", missing.Take(12)), "OK");
                return;
            }

            // ---- controller (dựng mới hoàn toàn)
            var old = animator.runtimeAnimatorController;
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(CtrlPath) != null) AssetDatabase.DeleteAsset(CtrlPath);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(CtrlPath);
            foreach (var f in new[] { "DirX", "DirY", "Speed", "AtkX", "AtkY" }) ctrl.AddParameter(f, AnimatorControllerParameterType.Float);
            ctrl.AddParameter("DashTrigger", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("IaidoTrigger", AnimatorControllerParameterType.Trigger);
            if (hurt.Count > 0) ctrl.AddParameter("HurtTrigger", AnimatorControllerParameterType.Trigger);
            if (stomp.Count > 0) ctrl.AddParameter("SkillTrigger", AnimatorControllerParameterType.Trigger);
            if (castE.Count > 0) ctrl.AddParameter("CastEmbersTrigger", AnimatorControllerParameterType.Trigger);
            if (castV.Count > 0) ctrl.AddParameter("CastVeilTrigger", AnimatorControllerParameterType.Trigger);
            if (rayStart.Count > 0) { ctrl.AddParameter("RayTrigger", AnimatorControllerParameterType.Trigger); ctrl.AddParameter("Ray", AnimatorControllerParameterType.Bool); }
            if (death.Count > 0) { ctrl.AddParameter("Dead", AnimatorControllerParameterType.Bool); ctrl.AddParameter("ReviveTrigger", AnimatorControllerParameterType.Trigger); }
            if (stagger.Count > 0) ctrl.AddParameter("StaggerTrigger", AnimatorControllerParameterType.Trigger);
            if (stun.Count > 0) ctrl.AddParameter("Stunned", AnimatorControllerParameterType.Bool);
            if (drink.Count > 0) ctrl.AddParameter("DrinkTrigger", AnimatorControllerParameterType.Trigger);
            var p = ctrl.parameters; foreach (var q in p) if (q.name == "DirY") q.defaultFloat = -1f; ctrl.parameters = p;   // mặc định nhìn xuống
            var p2 = ctrl.parameters; foreach (var q in p2) if (q.name == "AtkY") q.defaultFloat = -1f; ctrl.parameters = p2;

            var sm = ctrl.layers[0].stateMachine;
            var sIdle = sm.AddState("Idle", new Vector3(300, 120, 0));
            sIdle.motion = Tree(ctrl, "Idle Tree", "DirX", "DirY", Dir8.Select(d => (idle[d.folder], d.v)));
            sm.defaultState = sIdle;
            var sRun = sm.AddState("Run", new Vector3(600, 120, 0));
            sRun.motion = Tree(ctrl, "Run Tree", "DirX", "DirY", Dir8.Select(d => (run[d.folder], d.v)));
            sRun.speed = RunAnimSpeed;
            var sDash = sm.AddState("Dash", new Vector3(300, 320, 0));
            sDash.motion = Tree(ctrl, "Dash Tree", "DirX", "DirY", Dir4.Select(d => (dash[d.folder], d.v)));
            var sIaido = sm.AddState("Iaido", new Vector3(600, 320, 0));
            sIaido.motion = Tree(ctrl, "Iaido Tree", "AtkX", "AtkY", iaidoDirs.Select(d => (iaido[d.folder], d.v)));
            foreach (var s in new[] { sIdle, sRun, sDash, sIaido }) s.writeDefaultValues = true;

            // Idle <-> Run
            Instant(sIdle.AddTransition(sRun)).AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            Instant(sRun.AddTransition(sIdle)).AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            // Dash
            var aDash = Instant(sm.AddAnyStateTransition(sDash));
            aDash.AddCondition(AnimatorConditionMode.If, 0, "DashTrigger");
            aDash.canTransitionToSelf = false;
            var dBack = sDash.AddTransition(sIdle); dBack.hasExitTime = true; dBack.exitTime = 1f; dBack.hasFixedDuration = true; dBack.duration = 0f;
            // lăn xong mà vẫn giữ phím đi -> sang Run luôn (không chớp 1 khung Idle)
            var dRun = sDash.AddTransition(sRun); dRun.hasExitTime = true; dRun.exitTime = 1f; dRun.hasFixedDuration = true; dRun.duration = 0f;
            dRun.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            sDash.transitions = new[] { dRun, dBack };
            // Iaido
            var aIaido = Instant(sm.AddAnyStateTransition(sIaido));
            aIaido.AddCondition(AnimatorConditionMode.If, 0, "IaidoTrigger");
            aIaido.canTransitionToSelf = true;
            var iBack = sIaido.AddTransition(sIdle); iBack.hasExitTime = true; iBack.exitTime = 1f; iBack.hasFixedDuration = true; iBack.duration = 0f;
            // ---- Gục (ưu tiên cao nhất: thêm trước) / Đứng dậy / Chiêu F / Trúng đòn
            if (death.Count > 0)
            {
                var sDeath = sm.AddState("Death", new Vector3(900, 120, 0));
                sDeath.motion = Tree(ctrl, "Death Tree", "DirX", "DirY", death.Select(k => (k.Value, DirOf(k.Key))));
                sDeath.writeDefaultValues = true;
                var aDeath = Instant(sm.AddAnyStateTransition(sDeath));
                aDeath.AddCondition(AnimatorConditionMode.If, 0, "Dead");
                aDeath.canTransitionToSelf = false;
                // đưa transition Gục lên đầu danh sách Any State
                var list = sm.anyStateTransitions.ToList(); list.Remove(aDeath); list.Insert(0, aDeath); sm.anyStateTransitions = list.ToArray();
                var back = Instant(sDeath.AddTransition(sIdle)); back.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
                if (revive.Count > 0)
                {
                    var sRev = sm.AddState("Revive", new Vector3(900, 320, 0));
                    sRev.motion = revive.Count == 1 ? (Motion)revive.Values.First() : Tree(ctrl, "Revive Tree", "DirX", "DirY", revive.Select(k => (k.Value, DirOf(k.Key))));
                    sRev.writeDefaultValues = true;
                    var toRev = Instant(sDeath.AddTransition(sRev));
                    toRev.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
                    toRev.AddCondition(AnimatorConditionMode.If, 0, "ReviveTrigger");
                    sDeath.transitions = new[] { toRev, back };          // ưu tiên đứng dậy
                    var rEnd = sRev.AddTransition(sIdle); rEnd.hasExitTime = true; rEnd.exitTime = 1f; rEnd.hasFixedDuration = true; rEnd.duration = 0f;
                }
                // mọi Any State khác không chạy khi đã gục
                foreach (var t in sm.anyStateTransitions) if (t != aDeath) t.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
            }
            if (stomp.Count > 0)
            {
                var sSkill = sm.AddState("Skill", new Vector3(600, 520, 0));
                sSkill.motion = Tree(ctrl, "Skill Tree", "DirX", "DirY", stomp.Select(k => (k.Value, DirOf(k.Key))));
                sSkill.writeDefaultValues = true;
                var a = Instant(sm.AddAnyStateTransition(sSkill));
                a.AddCondition(AnimatorConditionMode.If, 0, "SkillTrigger");
                if (death.Count > 0) a.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
                var e = sSkill.AddTransition(sIdle); e.hasExitTime = true; e.exitTime = 1f; e.hasFixedDuration = true; e.duration = 0f;
                // hết khoá chân của chiêu (GroundSlamSkill.lockTime) mà bấm đi -> Run ngay, không Exit Time (§102, §104)
                var c = Instant(sSkill.AddTransition(sRun));
                c.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
                if (death.Count > 0) c.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
            }
            // ---- Mantra Con Mắt
            AddCast(ctrl, sm, "CastEmbers", castE, "CastEmbersTrigger", CastEmbersMs, sIdle, sRun, death.Count > 0, new Vector3(600, 600, 0));
            AddCast(ctrl, sm, "CastVeil", castV, "CastVeilTrigger", CastVeilMs, sIdle, sRun, death.Count > 0, new Vector3(600, 680, 0));
            if (rayStart.Count > 0)
            {
                var sRS = sm.AddState("RayStart", new Vector3(900, 600, 0));
                sRS.motion = Tree(ctrl, "RayStart Tree", "DirX", "DirY", rayStart.Select(k => (k.Value, DirOf(k.Key))));
                sRS.writeDefaultValues = true;
                var sRL = sm.AddState("RayLoop", new Vector3(900, 680, 0));
                sRL.motion = Tree(ctrl, "RayLoop Tree", "DirX", "DirY", rayLoop.Select(k => (k.Value, DirOf(k.Key))));
                sRL.writeDefaultValues = true;
                var a = Instant(sm.AddAnyStateTransition(sRS));
                a.AddCondition(AnimatorConditionMode.If, 0, "RayTrigger");
                if (death.Count > 0) a.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
                var hold = sRS.AddTransition(sRL); hold.hasExitTime = true; hold.exitTime = 1f; hold.hasFixedDuration = true; hold.duration = 0f;
                Instant(sRS.AddTransition(sIdle)).AddCondition(AnimatorConditionMode.IfNot, 0, "Ray");
                Instant(sRL.AddTransition(sIdle)).AddCondition(AnimatorConditionMode.IfNot, 0, "Ray");
            }
            AnimatorState sStun = null;
            if (stun.Count > 0)
            {
                sStun = sm.AddState("Stunned", new Vector3(900, 520, 0));
                sStun.motion = Tree(ctrl, "Stunned Tree", "DirX", "DirY", stun.Select(k => (k.Value, DirOf(k.Key))));
                sStun.writeDefaultValues = true;
                Instant(sStun.AddTransition(sIdle)).AddCondition(AnimatorConditionMode.IfNot, 0, "Stunned");
            }
            if (stagger.Count > 0)
            {
                var sSt = sm.AddState("Stagger", new Vector3(900, 720, 0));
                sSt.motion = Tree(ctrl, "Stagger Tree", "DirX", "DirY", stagger.Select(k => (k.Value, DirOf(k.Key))));
                sSt.writeDefaultValues = true;
                var a = Instant(sm.AddAnyStateTransition(sSt));
                a.AddCondition(AnimatorConditionMode.If, 0, "StaggerTrigger");
                if (death.Count > 0) a.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
                a.canTransitionToSelf = true;
                var toIdle = sSt.AddTransition(sIdle); toIdle.hasExitTime = true; toIdle.exitTime = 1f; toIdle.hasFixedDuration = true; toIdle.duration = 0f;
                // hết staggerLock (Speed thôi bị giữ 0) mà người chơi bấm đi -> sang Run ngay
                var toRun = Instant(sSt.AddTransition(sRun));
                toRun.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
                if (death.Count > 0) toRun.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
                if (sStun != null)
                {
                    var toStun = sSt.AddTransition(sStun); toStun.hasExitTime = true; toStun.exitTime = 0.5f; toStun.hasFixedDuration = true; toStun.duration = 0f;
                    toStun.AddCondition(AnimatorConditionMode.If, 0, "Stunned");
                    toRun.AddCondition(AnimatorConditionMode.IfNot, 0, "Stunned");
                    sSt.transitions = new[] { toStun, toRun, toIdle };
                }
                else sSt.transitions = new[] { toRun, toIdle };
            }
            if (drink.Count > 0)
            {
                var sDr = sm.AddState("Drink", new Vector3(600, 720, 0));
                sDr.motion = Tree(ctrl, "Drink Tree", "DirX", "DirY", drink.Select(k => (k.Value, DirOf(k.Key))));
                sDr.writeDefaultValues = true;
                var a = Instant(sm.AddAnyStateTransition(sDr));
                a.AddCondition(AnimatorConditionMode.If, 0, "DrinkTrigger");
                if (death.Count > 0) a.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
                var e = sDr.AddTransition(sIdle); e.hasExitTime = true; e.exitTime = 1f; e.hasFixedDuration = true; e.duration = 0f;
                // hết khựng (healLock) mà người chơi bấm đi thì bỏ phần cất bình — Speed bị giữ 0 lúc khựng nên không cần Exit Time
                var dr = Instant(sDr.AddTransition(sRun));
                dr.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
                if (death.Count > 0) dr.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
            }
            if (hurt.Count > 0)
            {
                var sHurt = sm.AddState("Hurt", new Vector3(300, 520, 0));
                sHurt.motion = Tree(ctrl, "Hurt Tree", "DirX", "DirY", hurt.Select(k => (k.Value, DirOf(k.Key))));
                sHurt.writeDefaultValues = true;
                var a = Instant(sm.AddAnyStateTransition(sHurt));
                a.AddCondition(AnimatorConditionMode.If, 0, "HurtTrigger");
                if (death.Count > 0) a.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
                a.canTransitionToSelf = true;
                var e = sHurt.AddTransition(sIdle); e.hasExitTime = true; e.exitTime = 1f; e.hasFixedDuration = true; e.duration = 0f;
                var r = sHurt.AddTransition(sRun); r.hasExitTime = true; r.exitTime = 1f; r.hasFixedDuration = true; r.duration = 0f;
                r.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
                sHurt.transitions = new[] { r, e };
            }

            // hết lockTime (Speed thôi bị giữ 0) mà người chơi bấm đi -> bỏ phần tra kiếm, sang Run ngay khung đó
            var iCancel = Instant(sIaido.AddTransition(sRun));
            iCancel.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            if (death.Count > 0) iCancel.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();

            // ---- gán vào Player
            Undo.RecordObject(animator, "Assign Player Animator");
            animator.runtimeAnimatorController = ctrl;
            EditorUtility.SetDirty(animator);
            // hướng đòn Iaido có animation -> PlayerCombat.directions
            var combat = animator.GetComponent<PlayerCombat>();
            if (combat != null)
            {
                var so = new SerializedObject(combat);
                var arr = so.FindProperty("directions");
                if (arr != null)
                {
                    arr.arraySize = iaidoDirs.Count;
                    for (int i = 0; i < iaidoDirs.Count; i++) arr.GetArrayElementAtIndex(i).vector2Value = iaidoDirs[i].v;
                    so.ApplyModifiedProperties();
                }
            }
            EditorSceneManager.MarkSceneDirty(animator.gameObject.scene);

            string oldInfo = old == null ? "(trước đó: controller bị mất / trống)" : $"(trước đó: {AssetDatabase.GetAssetPath(old)})";
            Debug.Log($"[Soul Requiem] Đã dựng {CtrlPath}: Idle/Run 8 hướng, Dash 4 hướng, Iaido {iaidoDirs.Count} hướng ({string.Join(", ", iaidoDirs.Select(d => d.name))}); Hurt {hurt.Count}, Stomp {stomp.Count}, Death {death.Count}, Revive {revive.Count}, Stagger {stagger.Count}, Stunned {stun.Count}, Drink {drink.Count} hướng; gán cho '{animator.name}' {oldInfo}.");
            EditorUtility.DisplayDialog("Soul Requiem",
                $"Đã dựng Animator nhân vật và gán cho '{animator.name}'.\nIaido: {string.Join(", ", iaidoDirs.Select(d => d.name))}\n\nIdle ⇄ Run (Speed)\nAny State → Dash (DashTrigger) → Idle\n" +
                $"Any State → Iaido (IaidoTrigger) → Idle, hết khoá chém mà bấm di chuyển thì sang Run ngay\n\n{oldInfo}\nNhớ Ctrl+S để lưu scene.", "OK");
        }

        static Vector2 DirOf(string folder) => Dir8.First(d => d.folder == folder).v;

        /// Bộ clip của một hoạt ảnh phụ: mọi hướng có thư mục ảnh. Thư mục không tồn tại -> rỗng (bỏ qua).
        static Dictionary<string, AnimationClip> OptionalSet(string folder, string prefix, int[] ms, List<string> missing)
        {
            var res = new Dictionary<string, AnimationClip>();
            foreach (var d in Dir8)
            {
                string path = $"{Root}/{folder}/{d.folder}";
                if (!Directory.Exists(path)) continue;
                var c = BuildTimed(path, prefix + d.name, ms, missing);
                if (c != null) res[d.folder] = c;
            }
            return res;
        }

        static AnimatorStateTransition Instant(AnimatorStateTransition t)
        {
            t.hasExitTime = false; t.hasFixedDuration = true; t.duration = 0f; t.offset = 0f;
            return t;
        }

        static BlendTree Tree(AnimatorController ctrl, string name, string px, string py, IEnumerable<(AnimationClip clip, Vector2 pos)> kids)
        {
            var t = new BlendTree
            {
                name = name,
                blendType = BlendTreeType.SimpleDirectional2D,
                blendParameter = px,
                blendParameterY = py,
                useAutomaticThresholds = false,
                hideFlags = HideFlags.HideInHierarchy
            };
            AssetDatabase.AddObjectToAsset(t, ctrl);
            foreach (var k in kids) t.AddChild(k.clip, k.pos);
            return t;
        }

        static Sprite[] LoadFrames(string folder)
        {
            if (!Directory.Exists(folder)) return new Sprite[0];
            return Directory.GetFiles(folder, "frame_*.png").Select(f => f.Replace('\\', '/')).OrderBy(f => f)
                .Select(f => AssetDatabase.LoadAllAssetsAtPath(f).OfType<Sprite>().FirstOrDefault()).ToArray();
        }

        /// Clip lặp, mỗi khung bằng nhau. 1 key/khung, KHÔNG thêm key cuối (Unity tự cộng 1 khung vào độ dài clip).
        static AnimationClip BuildUniform(string folder, string clipName, float frame, List<string> missing)
        {
            var s = LoadFrames(folder);
            if (s.Length == 0 || s.Any(x => x == null)) { missing.Add(folder); return null; }
            var keys = s.Select((sp, i) => new ObjectReferenceKeyframe { time = i * frame, value = sp }).ToArray();
            return Save(clipName, keys, 1f / frame, true);
        }

        /// Clip chạy 1 lần, thời lượng từng khung riêng. Khung cuối được giữ đủ thời lượng bằng 1 key giữ.
        static AnimationClip BuildTimed(string folder, string clipName, int[] ms, List<string> missing)
        {
            var s = LoadFrames(folder);
            if (s.Length != ms.Length || s.Any(x => x == null)) { missing.Add($"{folder} (cần {ms.Length} khung, có {s.Length})"); return null; }
            const float rate = 100f;                        // 10 ms / mẫu: khớp đúng mọi thời lượng
            var keys = new List<ObjectReferenceKeyframe>();
            float t = 0f;
            for (int i = 0; i < s.Length; i++) { keys.Add(new ObjectReferenceKeyframe { time = t, value = s[i] }); t += ms[i] / 1000f; }
            keys.Add(new ObjectReferenceKeyframe { time = t - 1f / rate, value = s[s.Length - 1] });   // độ dài clip = t
            return Save(clipName, keys.ToArray(), rate, false);
        }

        /// Trạng thái chiêu chạy 1 lần (như Stomp): Any State --trigger--> state -> hết clip về Idle; đang đi thì sang Run sớm.
        static void AddCast(AnimatorController ctrl, AnimatorStateMachine sm, string name, Dictionary<string, AnimationClip> set, string trigger,
                            int[] ms, AnimatorState idle, AnimatorState run, bool hasDeath, Vector3 pos)
        {
            if (set.Count == 0) return;
            var s = sm.AddState(name, pos);
            s.motion = Tree(ctrl, name + " Tree", "DirX", "DirY", set.Select(k => (k.Value, DirOf(k.Key))));
            s.writeDefaultValues = true;
            var a = Instant(sm.AddAnyStateTransition(s));
            a.AddCondition(AnimatorConditionMode.If, 0, trigger);
            if (hasDeath) a.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
            var e = s.AddTransition(idle); e.hasExitTime = true; e.exitTime = 1f; e.hasFixedDuration = true; e.duration = 0f;
            // Sang Run ngay khi hết khoá chân (PlayerController giữ Speed = 0 trong lúc khoá) — không Exit Time (xem §102).
            var c = Instant(s.AddTransition(run));
            c.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            if (hasDeath) c.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
        }

        /// Clip chạy 1 lần từ mảng sprite có sẵn (dùng cho phần đầu của ray-hold).
        static AnimationClip TimedFrom(Sprite[] s, string clipName, int[] ms)
        {
            const float rate = 100f;
            var keys = new List<ObjectReferenceKeyframe>();
            float t = 0f;
            for (int i = 0; i < s.Length; i++) { keys.Add(new ObjectReferenceKeyframe { time = t, value = s[i] }); t += ms[i] / 1000f; }
            keys.Add(new ObjectReferenceKeyframe { time = t - 1f / rate, value = s[s.Length - 1] });
            return Save(clipName, keys.ToArray(), rate, false);
        }

        static AnimationClip Save(string clipName, ObjectReferenceKeyframe[] keys, float rate, bool loop)
        {
            string path = $"{OutDir}/{clipName}.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            bool isNew = clip == null;
            if (isNew) clip = new AnimationClip();
            clip.ClearCurves();
            clip.frameRate = rate;
            var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
            var st = AnimationUtility.GetAnimationClipSettings(clip);
            st.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, st);
            if (isNew) AssetDatabase.CreateAsset(clip, path); else EditorUtility.SetDirty(clip);
            return clip;
        }
    }
}
