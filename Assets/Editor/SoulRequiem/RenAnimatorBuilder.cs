// Soul Requiem — builds the Animator for Ren v2 (PixelLab unarmed body "Ren Unarmed", 8 directions for every action,
// the weapon is a separate layer — PlayerWeaponView). Menu: Soul Requiem ▸ Player ▸ Ren Animator (v2) (declared in SoulRequiemMenu).
// Images: Assets/GamePackage/Ren/<action>/<direction>/<action>_<DIRECTION>_<nn>.png (imported by Catalog/v2/tools/ren2/ren_import.py,
// pivot = feet of the standing pose in the same direction). Sprite names are UNIQUE -> the weapon anchor table looks them up by name.
// Run OUTSIDE Play Mode, then Ctrl+S the scene. Related: RenRobeAnimatorBuilder (Map 4 override), PlayerAnimation (parameters).
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using SoulRequiem.Gameplay;

namespace SoulRequiem.EditorTools
{
    public static class RenAnimatorBuilder
    {
        const string Root = "Assets/GamePackage/Ren";
        const string OutDir = Root + "/Animations";
        const string CtrlPath = OutDir + "/Ren.controller";

        struct Dir { public string folder, name; public Vector2 v; public Dir(string f, string n, float x, float y) { folder = f; name = n; v = new Vector2(x, y); } }
        static readonly Dir[] Dir8 =
        {
            new Dir("east", "East", 1, 0), new Dir("north-east", "NorthEast", 0.7071f, 0.7071f),
            new Dir("north", "North", 0, 1), new Dir("north-west", "NorthWest", -0.7071f, 0.7071f),
            new Dir("west", "West", -1, 0), new Dir("south-west", "SouthWest", -0.7071f, -0.7071f),
            new Dir("south", "South", 0, -1), new Dir("south-east", "SouthEast", 0.7071f, -0.7071f),
        };

        // ---- timing (ms / frame). The hit / impact frame is noted right next to it — the code waits for exactly that moment.
        const float IdleFrame = 0.2f, RunFrame = 1f / 14f, StunFrame = 0.13f, RayLoopFrame = 0.1f, AttackFrame = 1f / 20f;
        static readonly int[] RollMs = { 40, 45, 45, 50, 50, 55, 60, 60 };                 // 0.4 s; invulnerable rollIFrames 0.3 s
        static readonly int[] HurtMs = { 50, 60, 60, 60, 60, 70 };
        static readonly int[] StaggerMs = { 60, 70, 80, 80, 70, 60, 60, 70 };
        static readonly int[] DeathMs = { 80, 80, 90, 100, 110, 120, 140, 160, 200, 400 };  // last frame held (lying down)
        static readonly int[] ReviveMs = { 120, 120, 110, 100, 100, 90, 90, 90 };
        // 0.5 s = PlayerCombat.healLock. Frame 3 (head back, flask at the lips) starts at 160 ms = PlayerFlask.GulpTime (heal + VFX).
        static readonly int[] DrinkMs = { 50, 60, 50, 90, 90, 60, 50, 50 };
        // 0.5 s = GroundSlam.lockTime (not cancelled by moving). Frame 3 = sword hits the ground at 0.25 s = GroundSlam.impactTime.
        static readonly int[] StompMs = { 60, 90, 100, 40, 80, 50, 40, 40 };
        // 0.35 s = castLock (Seeker Embers / Ichor 0.35; Vessel Hand 0.4). Frame 3 = palm pushes out at 0.2 s (spawnDelay / throwDelay / castDelay ~0.2).
        static readonly int[] CastEmbersMs = { 40, 60, 100, 60, 40, 20, 15, 15 };
        // 0.3 s = Veil.castLock. Frame 3 = sword held upright in front at 0.13 s.
        static readonly int[] CastVeilMs = { 30, 40, 60, 70, 40, 25, 20, 15 };
        static readonly int[] RayStartMs = { 60, 80 };                                      // 2 frames raising the hand, the rest loops
        // running with the sword sheathed (shorter strides) -> faster timing so the feet do not slide
        const float RunAnimSpeed = 1.55f;                                                   // match the footsteps to moveSpeed
        const float SheatheFrame = 1f / 12f;                                                // sheathe 8 frames = 0.67 s

        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { EditorUtility.DisplayDialog("Soul Requiem", "Stop Play Mode first.", "OK"); return; }
            var pc = Object.FindFirstObjectByType<PlayerController>();
            var animator = pc != null ? pc.GetComponent<Animator>() : null;
            if (animator == null) { EditorUtility.DisplayDialog("Soul Requiem", "No Player (PlayerController + Animator) found in the scene.", "OK"); return; }
            if (!AssetDatabase.IsValidFolder(OutDir)) AssetDatabase.CreateFolder(Root, "Animations");

            var missing = new List<string>();
            var idle = Loop("idle", "Idle_", IdleFrame, missing);
            var run = Loop("run", "Run_", RunFrame, missing);
            var idleS = Loop("idle_sheathed", "IdleSheathed_", IdleFrame, missing);        // standing, sword sheathed
            var sheathe = Loop("sheathe", "Sheathe_", SheatheFrame, missing, false);      // sheathe the sword
            var roll = Timed("roll", "Roll_", RollMs, missing);
            var atk = new[] { Loop("attack1", "Attack1_", AttackFrame, missing, false), Loop("attack2", "Attack2_", AttackFrame, missing, false), Loop("attack3", "Attack3_", AttackFrame, missing, false) };
            var hurt = Timed("hurt", "Hurt_", HurtMs, missing);
            var stagger = Timed("stagger", "Stagger_", StaggerMs, missing);
            var stun = Loop("stunned", "Stunned_", StunFrame, missing);
            var death = Timed("death", "Death_", DeathMs, missing);
            var revive = Timed("getup", "Revive_", ReviveMs, missing);
            var drink = Timed("drink", "Drink_", DrinkMs, missing);
            var stomp = Timed("stomp", "Stomp_", StompMs, missing);
            var castE = Timed("cast_embers", "CastEmbers_", CastEmbersMs, missing);
            var castV = Timed("cast_veil", "CastVeil_", CastVeilMs, missing);
            var rayStart = new Dictionary<string, AnimationClip>(); var rayLoop = new Dictionary<string, AnimationClip>();
            foreach (var d in Dir8)
            {
                var fr = Frames($"{Root}/ray/{d.folder}");
                if (fr.Length < RayStartMs.Length + 2) { missing.Add($"ray/{d.folder} ({fr.Length} khung)"); continue; }
                rayStart[d.folder] = TimedFrom(fr.Take(RayStartMs.Length).ToArray(), "RayStart_" + d.name, RayStartMs);
                var keys = fr.Skip(RayStartMs.Length).Select((sp, i) => new ObjectReferenceKeyframe { time = i * RayLoopFrame, value = sp }).ToArray();
                rayLoop[d.folder] = Save("RayLoop_" + d.name, keys, 1f / RayLoopFrame, true);
            }
            if (missing.Count > 0) { EditorUtility.DisplayDialog("Soul Requiem", "Missing images:\n" + string.Join("\n", missing.Take(14)), "OK"); return; }

            var old = animator.runtimeAnimatorController;
            // REBUILD IN PLACE (keep the guid) — deleting and recreating the file broke the controller links in Map1/2/3/3b
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(CtrlPath);
            if (ctrl == null) ctrl = AnimatorController.CreateAnimatorControllerAtPath(CtrlPath);
            else Clear(ctrl);
            foreach (var f in new[] { "DirX", "DirY", "Speed", "AtkX", "AtkY", "AtkSpeed" }) ctrl.AddParameter(f, AnimatorControllerParameterType.Float);
            foreach (var t in new[] { "DashTrigger", "Attack1Trigger", "Attack2Trigger", "Attack3Trigger", "HurtTrigger", "SkillTrigger", "CastEmbersTrigger",
                                      "CastVeilTrigger", "RayTrigger", "ReviveTrigger", "StaggerTrigger", "DrinkTrigger" })
                ctrl.AddParameter(t, AnimatorControllerParameterType.Trigger);
            foreach (var b in new[] { "Ray", "Dead", "Stunned", "Sheathed" }) ctrl.AddParameter(b, AnimatorControllerParameterType.Bool);
            var ps = ctrl.parameters;
            foreach (var q in ps) { if (q.name == "DirY" || q.name == "AtkY") q.defaultFloat = -1f; if (q.name == "AtkSpeed") q.defaultFloat = 1f; if (q.name == "Sheathed") q.defaultBool = true; }
            ctrl.parameters = ps;

            var sm = ctrl.layers[0].stateMachine;
            var sIdle = State(ctrl, sm, "Idle", idle, "DirX", "DirY", new Vector3(300, 120));
            var sRun = State(ctrl, sm, "Run", run, "DirX", "DirY", new Vector3(600, 120)); sRun.speed = RunAnimSpeed;
            Instant(sIdle.AddTransition(sRun)).AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            // sword sheathed. Idle (sword drawn, in combat) -- after DrawnHold seconds --> Sheathe -> IdleSheathed.
            // Running always has the sword sheathed; stopping returns to Idle or IdleSheathed depending on Sheathed.
            var sIdleS = State(ctrl, sm, "IdleSheathed", idleS, "DirX", "DirY", new Vector3(300, 40));
            var sSheathe = State(ctrl, sm, "Sheathe", sheathe, "DirX", "DirY", new Vector3(60, 80));
            sm.defaultState = sIdleS;
            Instant(sIdle.AddTransition(sSheathe)).AddCondition(AnimatorConditionMode.If, 0, "Sheathed");
            End(sSheathe, sIdleS);
            Instant(sSheathe.AddTransition(sRun)).AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            Instant(sSheathe.AddTransition(sIdle)).AddCondition(AnimatorConditionMode.IfNot, 0, "Sheathed");
            Instant(sIdleS.AddTransition(sRun)).AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            Instant(sIdleS.AddTransition(sIdle)).AddCondition(AnimatorConditionMode.IfNot, 0, "Sheathed");
            var runIdle = Instant(sRun.AddTransition(sIdle)); runIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed"); runIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "Sheathed");
            var runIdleS = Instant(sRun.AddTransition(sIdleS)); runIdleS.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed"); runIdleS.AddCondition(AnimatorConditionMode.If, 0, "Sheathed");

            // Death: highest-priority Any State
            var sDeath = State(ctrl, sm, "Death", death, "DirX", "DirY", new Vector3(900, 120));
            var aDeath = Instant(sm.AddAnyStateTransition(sDeath)); aDeath.AddCondition(AnimatorConditionMode.If, 0, "Dead"); aDeath.canTransitionToSelf = false;
            var sRev = State(ctrl, sm, "Revive", revive, "DirX", "DirY", new Vector3(900, 320));
            var toRev = Instant(sDeath.AddTransition(sRev)); toRev.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead"); toRev.AddCondition(AnimatorConditionMode.If, 0, "ReviveTrigger");
            var back = Instant(sDeath.AddTransition(sIdle)); back.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
            sDeath.transitions = new[] { toRev, back };
            End(sRev, sIdle);

            // Roll
            var sRoll = State(ctrl, sm, "Dash", roll, "DirX", "DirY", new Vector3(300, 320));
            Any(sm, sRoll, "DashTrigger", false);
            var rRun = End(sRoll, sRun); rRun.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            // the dash animation is drawn with the sword SHEATHED -> ends in IdleSheathed (PlayerAnimation.Dash sets Sheathed = true)
            var rIdleS = End(sRoll, sIdleS); rIdleS.AddCondition(AnimatorConditionMode.If, 0, "Sheathed");
            var rIdle = End(sRoll, sIdle);
            sRoll.transitions = new[] { rRun, rIdleS, rIdle };

            // 3-hit combo — clip speed follows the weapon (AtkSpeed)
            for (int i = 0; i < 3; i++)
            {
                var s = State(ctrl, sm, "Attack" + (i + 1), atk[i], "AtkX", "AtkY", new Vector3(600, 320 + 70 * i));
                s.speedParameterActive = true; s.speedParameter = "AtkSpeed";
                Any(sm, s, "Attack" + (i + 1) + "Trigger", true);
                End(s, sIdle);
                Instant(s.AddTransition(sRun)).AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");   // attack lock ended and the player moves
            }

            OneShot(ctrl, sm, "Skill", stomp, "SkillTrigger", sIdle, sRun, new Vector3(600, 560));
            OneShot(ctrl, sm, "CastEmbers", castE, "CastEmbersTrigger", sIdle, sRun, new Vector3(600, 630));
            OneShot(ctrl, sm, "CastVeil", castV, "CastVeilTrigger", sIdle, sRun, new Vector3(600, 700));
            // drinking with the sword sheathed -> ends in IdleSheathed (PlayerAnimation.Drink sets Sheathed = true)
            var sDrink = State(ctrl, sm, "Drink", drink, "DirX", "DirY", new Vector3(600, 770));
            Any(sm, sDrink, "DrinkTrigger", false);
            Instant(sDrink.AddTransition(sRun)).AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            End(sDrink, sIdleS).AddCondition(AnimatorConditionMode.If, 0, "Sheathed");
            End(sDrink, sIdle);

            var sRS = State(ctrl, sm, "RayStart", rayStart, "DirX", "DirY", new Vector3(900, 600));
            var sRL = State(ctrl, sm, "RayLoop", rayLoop, "DirX", "DirY", new Vector3(900, 680));
            Any(sm, sRS, "RayTrigger", false);
            End(sRS, sRL);
            Instant(sRS.AddTransition(sIdle)).AddCondition(AnimatorConditionMode.IfNot, 0, "Ray");
            Instant(sRL.AddTransition(sIdle)).AddCondition(AnimatorConditionMode.IfNot, 0, "Ray");

            var sStun = State(ctrl, sm, "Stunned", stun, "DirX", "DirY", new Vector3(900, 520));
            Instant(sStun.AddTransition(sIdle)).AddCondition(AnimatorConditionMode.IfNot, 0, "Stunned");
            var sSt = State(ctrl, sm, "Stagger", stagger, "DirX", "DirY", new Vector3(900, 760));
            Any(sm, sSt, "StaggerTrigger", true);
            var toStun = sSt.AddTransition(sStun); toStun.hasExitTime = true; toStun.exitTime = 0.5f; toStun.hasFixedDuration = true; toStun.duration = 0f;
            toStun.AddCondition(AnimatorConditionMode.If, 0, "Stunned");
            var stRun = Instant(sSt.AddTransition(sRun)); stRun.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed"); stRun.AddCondition(AnimatorConditionMode.IfNot, 0, "Stunned");
            var stIdle = End(sSt, sIdle);
            sSt.transitions = new[] { toStun, stRun, stIdle };

            var sHurt = State(ctrl, sm, "Hurt", hurt, "DirX", "DirY", new Vector3(300, 520));
            Any(sm, sHurt, "HurtTrigger", true);
            var hRun = End(sHurt, sRun); hRun.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            var hIdle = End(sHurt, sIdle);
            sHurt.transitions = new[] { hRun, hIdle };

            // every Any State (except Death) is blocked once dead; Death is first in the list
            foreach (var t in sm.anyStateTransitions) if (t != aDeath) t.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
            var list = sm.anyStateTransitions.ToList(); list.Remove(aDeath); list.Insert(0, aDeath); sm.anyStateTransitions = list.ToArray();
            foreach (var st in sm.states) st.state.writeDefaultValues = true;

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            Undo.RecordObject(animator, "Assign Ren Animator");
            animator.runtimeAnimatorController = ctrl;
            EditorUtility.SetDirty(animator);
            EditorSceneManager.MarkSceneDirty(animator.gameObject.scene);
            string oldInfo = old == null ? "(before: empty)" : $"(before: {AssetDatabase.GetAssetPath(old)})";
            Debug.Log($"[Soul Requiem] Built {CtrlPath} (Ren v2, 8 directions for every action, 3-hit combo) and assigned it to '{animator.name}' {oldInfo}.");
            EditorUtility.DisplayDialog("Soul Requiem", $"Built Ren v2 and assigned it to '{animator.name}'.\n{oldInfo}\nRemember Ctrl+S to save the scene.", "OK");
        }

        // ------------------------------------------------------------------ build
        /// <summary>Wipe the old controller's content but keep the file (guid) so the scenes still point at it.</summary>
        static void Clear(AnimatorController ctrl)
        {
            foreach (var p in ctrl.parameters) ctrl.RemoveParameter(p);
            while (ctrl.layers.Length > 1) ctrl.RemoveLayer(1);
            var sm = ctrl.layers[0].stateMachine;
            foreach (var t in sm.anyStateTransitions) sm.RemoveAnyStateTransition(t);
            foreach (var t in sm.entryTransitions) sm.RemoveEntryTransition(t);
            foreach (var c in sm.stateMachines) sm.RemoveStateMachine(c.stateMachine);
            foreach (var c in sm.states) sm.RemoveState(c.state);
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(ctrl)))
                if (o is BlendTree) Object.DestroyImmediate(o, true);
        }

        static AnimatorState State(AnimatorController ctrl, AnimatorStateMachine sm, string name, Dictionary<string, AnimationClip> set, string px, string py, Vector3 pos)
        {
            var s = sm.AddState(name, pos);
            var t = new BlendTree { name = name + " Tree", blendType = BlendTreeType.SimpleDirectional2D, blendParameter = px, blendParameterY = py,
                                    useAutomaticThresholds = false, hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(t, ctrl);
            foreach (var d in Dir8) if (set.TryGetValue(d.folder, out var c)) t.AddChild(c, d.v);
            s.motion = t;
            return s;
        }

        static AnimatorStateTransition Instant(AnimatorStateTransition t) { t.hasExitTime = false; t.hasFixedDuration = true; t.duration = 0f; t.offset = 0f; return t; }

        static AnimatorStateTransition End(AnimatorState from, AnimatorState to)
        {
            var e = from.AddTransition(to); e.hasExitTime = true; e.exitTime = 1f; e.hasFixedDuration = true; e.duration = 0f; return e;
        }

        static void Any(AnimatorStateMachine sm, AnimatorState s, string trigger, bool self)
        {
            var a = Instant(sm.AddAnyStateTransition(s)); a.AddCondition(AnimatorConditionMode.If, 0, trigger); a.canTransitionToSelf = self;
        }

        static void OneShot(AnimatorController ctrl, AnimatorStateMachine sm, string name, Dictionary<string, AnimationClip> set, string trigger,
                            AnimatorState idle, AnimatorState run, Vector3 pos)
        {
            var s = State(ctrl, sm, name, set, "DirX", "DirY", pos);
            Any(sm, s, trigger, false);
            End(s, idle);
            Instant(s.AddTransition(run)).AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");   // the code keeps Speed = 0 while locked
        }

        // ------------------------------------------------------------------ clip
        static Sprite[] Frames(string folder)
        {
            if (!Directory.Exists(folder)) return new Sprite[0];
            return Directory.GetFiles(folder, "*.png").Select(f => f.Replace('\\', '/')).OrderBy(f => f)
                .Select(f => AssetDatabase.LoadAssetAtPath<Sprite>(f)).Where(s => s != null).ToArray();
        }

        static Dictionary<string, AnimationClip> Loop(string folder, string prefix, float frame, List<string> missing, bool loop = true)
        {
            var res = new Dictionary<string, AnimationClip>();
            foreach (var d in Dir8)
            {
                var s = Frames($"{Root}/{folder}/{d.folder}");
                if (s.Length == 0) { missing.Add($"{folder}/{d.folder}"); continue; }
                var keys = s.Select((sp, i) => new ObjectReferenceKeyframe { time = i * frame, value = sp }).ToList();
                if (!loop) keys.Add(new ObjectReferenceKeyframe { time = s.Length * frame - 0.01f, value = s[s.Length - 1] });
                res[d.folder] = Save(prefix + d.name, keys.ToArray(), loop ? 1f / frame : 100f, loop);
            }
            return res;
        }

        static Dictionary<string, AnimationClip> Timed(string folder, string prefix, int[] ms, List<string> missing)
        {
            var res = new Dictionary<string, AnimationClip>();
            foreach (var d in Dir8)
            {
                var s = Frames($"{Root}/{folder}/{d.folder}");
                if (s.Length != ms.Length) { missing.Add($"{folder}/{d.folder} (needs {ms.Length}, has {s.Length})"); continue; }
                res[d.folder] = TimedFrom(s, prefix + d.name, ms);
            }
            return res;
        }

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
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
            var st = AnimationUtility.GetAnimationClipSettings(clip); st.loopTime = loop; AnimationUtility.SetAnimationClipSettings(clip, st);
            if (isNew) AssetDatabase.CreateAsset(clip, path); else EditorUtility.SetDirty(clip);
            return clip;
        }
    }
}
