#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using SoulRequiem.Core;
using SoulRequiem.Gameplay;

namespace SoulRequiem.EditorTools
{
    /// <summary>
    /// Menu: Soul Requiem ▸ Dựng quái Map 2
    /// Quái cống ngầm Vael lấy từ gói EPIC RPG World – Sewers (Mutant Rat, Viper, Beholder) và Tiny Swords (Turtle).
    /// Nhện và goblin dùng lại prefab Map 1. Mở scene Map2_UndercityOfVael, gán toàn bộ vào EncounterDirector.
    ///   Plague_Rat     chuột đột biến, cào + độc (Sewers)
    ///   Vat_Born       chuột chui ra từ bình nuôi ở Lab (nhuộm xanh, nhanh, máu ít)
    ///   Bloated_Rat    chuột phình to x1.4, phun bóng độc (tầm xa) — tinh anh ở Feed Conduit
    ///   Channel_Viper  rắn sống ở bờ kênh Ichor, không rời bờ (dây xích ngắn)
    ///   Ichor_Snapper  rùa Tiny Swords: trâu, cắn lao; trúng đòn thì thụt mai (bất tử 2 giây)
    ///   Plague_Eye     boss Beholder ghim trên hố: loạt đạn mắt, tia quét 360° (núp sau cột), xoay quật khi bị áp sát
    ///                  (PlagueEyeSkills); cứ 20 giây gọi một đợt quái độc (Toxic Brood)
    ///   Bong bóng của Bloated Rat vỡ ra mây độc (PoisonCloud)
    /// Chạy lại bao nhiêu lần cũng được (ghi đè).
    /// </summary>
    public static class Map2EnemyBuilder
    {
        const string SW = "Assets/GamePackage/EPIC RPG World - Sewers V1.5/Characters/";
        const string TS = "Assets/GamePackage/Tiny Swords (Enemy Pack)/Enemy Pack/";
        const string OutRoot = "Assets/Enemies/Map2";
        const string Map1Prefabs = "Assets/Enemies/Map1/Prefabs/";
        const string Scene = "Assets/Scenes/Map2_UndercityOfVael.unity";
        static readonly string[] Reuse = { "Spider", "Goblin_Spearman", "Goblin_Torch", "Goblin_Hexer" };

        class Anim
        {
            public string state, file; public float fps; public bool loop; public int frameW; public float pivotY;
            public Anim(string state, string file, float fps, bool loop, int frameW = 0, float pivotY = -1f)
            { this.state = state; this.file = file; this.fps = fps; this.loop = loop; this.frameW = frameW; this.pivotY = pivotY; }
        }

        class Def
        {
            public string key, display;
            public float ppu = 100f, pivotX = 0.5f, pivotY;
            public Color tint = Color.white;
            public float scale = 1f;
            public Anim[] anims;
            public Vector2 collider;
            public float mass = 1f, hp;
            public bool immuneCC;
            public EnemyKind kind;
            public string projectile;            // "bubble" | "eye"
            public Action<EnemyAI> tune;
            public Action<GameObject> extra;     // thêm component riêng (vd. PlagueEyeSkills)
        }

        const string RAT = SW + "Mutant Rat/mutant rat - ";
        const string VIPER = SW + "Viper/viper-";
        const string EYE = SW + "Beholder/beholder-";
        const string BEAM = SW + "Beholder/beam-impact-loop.png";
        const string FX = OutRoot + "/FX/";
        const string BEAM_SRC = SW + "Beholder/beam-loop-4 frames.png";
        const string BEAM_LOOP = FX + "eye-beam-loop.png";             // bản sao: ảnh gốc đang dùng trong tileset của map, không cắt lại ảnh gốc
        const string SPIN_FX = SW + "Beholder/beholder-atk3-loop-FX.png";
        const string CLOUD = SW + "Mutant Rat/poison cloud-";
        // Map 2 v2 (Malric's Warren): quái CHARACTER MEGAPACK
        const string MEGA = "Assets/GamePackage/CHARACTER MEGAPACK/";
        const string TOAD = MEGA + "FrogBoss/";
        const string HOP = MEGA + "FrogMonster/";
        const string RAM = MEGA + "RhinoMonster_09_Radioactive/";
        const string BARREL = MEGA + "Goblin_Barrel_01 (Green Skinned)/Goblin_Barrel_01_";
        const string MIMIC = MEGA + "Mimic_LVL2/";
        static GameObject toadGlob;                       // đạn nhớt của Gorgemaw (dựng trước khi dựng quái)
        static GameObject eyeBolt;                        // đạn mắt (Plague Eye) — Specimen Zero dùng lại, nhuộm tím
        const string CRAWL = "Assets/GamePackage/CRAWLING DEPTHS/CRAWLING DEPTHS/Creatures/";
        // bản sao trong FX: ảnh gốc của CRAWLING DEPTHS đang dùng trong tileset map, không đổi chế độ import của ảnh gốc
        const string SZ_DORMANT = FX + "specimen-zero-dormant.png";
        const string SZ_AWAKE = FX + "specimen-zero-awaken.png";
        const string SZ_TENT = FX + "specimen-zero-tentacle.png";
        // chiêu mini-boss (22/10): giọt/cục nhớt Ichor của cóc, toé khi rơi, đá trần rơi khi Ram đâm tường
        const string GLOB = FX + "ichor-glob.png";
        const string GLOB_POP = FX + "ichor-glob-pop.png";
        const string SPLASH = FX + "ichor-splash.png";          // bản sao 'water ripples by impact-opaque' (ảnh gốc nằm trong tileset map)
        const string RUBBLE = FX + "rubble-chunk.png";

        static Anim[] RatAnims(string attack, float atkFps = 14f) => new[] {
            new Anim("Idle", RAT + "idle.png", 10, true, 128), new Anim("Move", RAT + "walk.png", 12, true, 128),
            new Anim("Attack", RAT + attack, atkFps, false, 128), new Anim("Death", RAT + "death.png", 12, false, 128) };
        // 06/11 (người dùng: chuột cận chiến không cầm độc, phải khác hẳn chuột ném độc): bản "clean" — mụn / cầu độc xanh
        // đã đổi sang vết loét nâu + móng tối (Catalog/v2/tools/rat_clean_sheets.py). Chuột ném độc (Bloated) giữ bản gốc.
        static Anim[] RatCleanAnims(float atkFps = 14f) => new[] {
            new Anim("Idle", RAT + "clean idle.png", 10, true, 128), new Anim("Move", RAT + "clean walk.png", 12, true, 128),
            new Anim("Attack", RAT + "clean attack2.png", atkFps, false, 128), new Anim("Death", RAT + "clean death.png", 12, false, 128) };

        static readonly Def[] Defs =
        {
            new Def {
                key = "Plague_Rat", display = "Plague Rat", pivotX = 0.53f, pivotY = 0.354f, collider = new Vector2(0.30f, 0.12f), hp = 45, mass = 1.5f,
                kind = EnemyKind.Melee, anims = RatCleanAnims(),
                tune = e => { e.moveSpeed = 1.5f; e.damage = 8; e.windup = 0.35f; e.cooldown = 1.1f; e.attackRange = 0.12f; e.aggroRange = 3.2f;
                              e.poisonPercent = 0f; e.knockback = 1f; }                 // 06/11: đánh thường không gây độc
            },
            new Def {
                key = "Vat_Born", display = "Vat-born", pivotX = 0.53f, pivotY = 0.354f, collider = new Vector2(0.28f, 0.11f), hp = 30, mass = 1f,
                // 06/11: con non từ bể nuôi — da nhợt hồng, nhỏ hơn, không độc (trước: nhuộm xanh, trông như chuột độc)
                scale = 0.85f, tint = new Color(1f, 0.8f, 0.76f), kind = EnemyKind.Melee, anims = RatCleanAnims(16f),
                tune = e => { e.moveSpeed = 1.95f; e.damage = 7; e.windup = 0.28f; e.cooldown = 0.9f; e.attackRange = 0.12f; e.aggroRange = 3.6f;
                              e.poisonPercent = 0f; }
            },
            new Def {
                key = "Bloated_Rat", display = "Bloated Rat", pivotX = 0.53f, pivotY = 0.354f, collider = new Vector2(0.30f, 0.12f), hp = 170, mass = 5f,
                // 06/11: giữ màu gốc (trước nhuộm đỏ làm nhạt màu xanh độc) -> mụn + cầu độc xanh sáng rõ: đây là con ném độc
                scale = 1.4f, kind = EnemyKind.Ranged, projectile = "bubble", anims = RatAnims("attack1.png"),
                tune = e => { e.moveSpeed = 0.9f; e.damage = 12; e.windup = 0.7f; e.cooldown = 1.6f; e.aggroRange = 4.0f; e.keepDistance = 1.6f;
                              e.projectileSpeed = 1.9f; e.projectileLifetime = 2.8f; e.poisonPercent = 0.03f; e.poisonDuration = 4f; e.closeRange = 0f; }
            },
            new Def {
                key = "Channel_Viper", display = "Channel Viper", pivotY = 0.438f, collider = new Vector2(0.26f, 0.10f), hp = 40, mass = 1.5f,
                kind = EnemyKind.Melee,
                anims = new[] { new Anim("Idle", VIPER + "idle.png", 10, true), new Anim("Move", VIPER + "walk.png", 10, true),
                                new Anim("Attack", VIPER + "attack.png", 14, false), new Anim("Death", VIPER + "death.png", 12, false) },
                tune = e => { e.moveSpeed = 1.2f; e.damage = 10; e.windup = 0.3f; e.cooldown = 1.2f; e.attackRange = 0.2f; e.aggroRange = 2.4f;
                              e.leashRange = 1.6f; e.poisonPercent = 0.02f; e.poisonDuration = 4f; }
            },
            new Def {
                key = "Ichor_Snapper", display = "Ether Snapper", ppu = 200, pivotX = 0.52f, pivotY = 0.35f, collider = new Vector2(0.50f, 0.16f),
                hp = 140, mass = 8f, tint = new Color(0.86f, 1f, 0.86f), kind = EnemyKind.Melee,
                anims = new[] { new Anim("Idle", TS + "Turtle/Turtle_Idle.png", 10, true), new Anim("Move", TS + "Turtle/Turtle_Walk.png", 9, true),
                                new Anim("Attack", TS + "Turtle/Turtle_Attack.png", 12, false),
                                new Anim("GuardIn", TS + "Extra/Turtle Guard/Turtle_Guard_In.png", 14, false),
                                new Anim("GuardOut", TS + "Extra/Turtle Guard/Turtle_Guard_Out.png", 12, false) },
                tune = e => { e.moveSpeed = 0.8f; e.damage = 16; e.windup = 0.5f; e.cooldown = 1.5f; e.attackRange = 0.2f; e.aggroRange = 3.0f;
                              e.knockback = 2.5f; e.special = EnemySpecial.Shell; e.shellTime = 2.0f; e.shellCooldown = 3.5f; }
            },
            new Def {
                key = "Plague_Eye", display = "The Plague Eye", pivotX = 0.48f, pivotY = 0.135f, collider = new Vector2(0.62f, 0.30f), hp = 900, mass = 50f,
                scale = 3f,                                   // to gấp 3 (như mockup: Con Mắt chiếm gần trọn miệng hố)
                immuneCC = true, kind = EnemyKind.Beholder, projectile = "eye",
                anims = new[] { new Anim("Idle", EYE + "walk-front view.png", 12, true, 128), new Anim("Move", EYE + "walk-front view.png", 12, true, 128),
                                new Anim("Attack", EYE + "atk2-front view-antecipation.png", 8, false, 128),
                                new Anim("VolleyWindup", EYE + "atk2-front view-antecipation.png", 8, false, 128),
                                new Anim("VolleyLoop", EYE + "atk2-front view-loop.png", 8, true, 128),
                                new Anim("VolleyEnd", EYE + "atk2-front view-finishing.png", 8, false, 128),
                                new Anim("BeamWindup", EYE + "atk1-front view-antecipation.png", 13, false, 128),
                                new Anim("BeamLoop", EYE + "atk1-front view-loop.png", 10, true, 128),
                                new Anim("BeamEnd", EYE + "atk1-front view- finishing atk1 animation.png", 12, false, 128),
                                new Anim("SpinStart", EYE + "atk3-spin start.png", 8, false, 128),
                                new Anim("Spin", EYE + "atk3-spin loop.png", 16, true, 128),
                                new Anim("SpinEnd", EYE + "atk3-finishing the spin.png", 8, false, 128),
                                new Anim("Death", EYE + "death.png", 10, false, 128, 0.27f) },
                tune = e => { e.treatAsBoss = true; e.floating = true; e.leashRange = 0f; e.moveSpeed = 0f; e.damage = 14; e.windup = 0.5f;
                              e.cooldown = 1.1f; e.aggroRange = 12f; e.keepDistance = 1.3f; e.projectileSpeed = 2.0f; e.projectileLifetime = 4.5f;
                              e.phase2At = 0.5f; },
                extra = go => {
                    var k = go.AddComponent<PlagueEyeSkills>();
                    k.beamFrames = Sprites(BEAM_LOOP);
                    k.beamImpactFrames = Sprites(BEAM);
                    k.spinFxFrames = Sprites(SPIN_FX);
                    k.eyeOffset = new Vector2(0f, 0.77f);
                    k.cloudPop = Sprites(CLOUD + "popping up.png");
                    k.cloudLoop = Sprites(CLOUD + "loop.png");
                    k.cloudFade = Sprites(CLOUD + "fading.png");
                }
            },
            // ---------------------------------------------------------------- Map 2 v2
            new Def {
                key = "Specimen_Zero", display = "Specimen Zero", pivotY = 0.034f, collider = new Vector2(0.9f, 0.25f), hp = 1100, mass = 999f,
                scale = 1.6f, immuneCC = true, kind = EnemyKind.Custom,
                anims = new[] { new Anim("Idle", SZ_DORMANT, 2, true, 160), new Anim("Move", SZ_AWAKE, 2, true, 160),
                                new Anim("Awake", SZ_AWAKE, 2, true, 160) },
                tune = e => { e.treatAsBoss = true; e.floating = true; e.leashRange = 0f; e.moveSpeed = 0f; e.damage = 16; e.aggroRange = 4.5f; e.phase2At = 0.5f; },
                extra = go => { var k = go.AddComponent<SpecimenZeroSkills>(); k.boltPrefab = eyeBolt; k.tentacleFrames = Sprites(SZ_TENT); }
            },
            new Def {
                key = "Skinned_Hopper", display = "Skinned Hopper", pivotY = 0.02f, collider = new Vector2(0.16f, 0.07f), hp = 35, mass = 1f,
                scale = 1.6f, kind = EnemyKind.Custom,
                anims = new[] { new Anim("Idle", HOP + "Idle_2x1.png", 4, true, 48), new Anim("Move", HOP + "Jump_Full_22x1.png", 22, true, 48),
                                new Anim("Prepare", HOP + "Prepare_4x1.png", 12, false, 48), new Anim("Dash", HOP + "Jump_14x1.png", 26, false, 48),
                                new Anim("Recover", HOP + "Recover_4x1.png", 10, false, 48) },
                tune = e => { e.moveSpeed = 1.1f; e.damage = 8; e.windup = 0.3f; e.cooldown = 1f; e.attackRange = 0.1f; e.aggroRange = 3.6f;
                              e.leashRange = 4f; e.poisonPercent = 0.02f; e.poisonDuration = 3f; },
                extra = go => { var k = go.AddComponent<LungeSkills>(); k.prepState = "Prepare"; k.lungeState = "Dash"; k.endState = "Recover";
                                k.meleeState = ""; k.lungeRange = 1.3f; k.lungeSpeed = 3.0f; k.lungeTime = 0.5f; k.prepTime = 0.3f; }
            },
            new Def {
                key = "Barrel_Goblin", display = "Barrel Goblin", pivotY = 0.02f, collider = new Vector2(0.2f, 0.08f), hp = 60, mass = 2f,
                scale = 1.8f, kind = EnemyKind.Custom,
                anims = new[] { new Anim("Idle", BARREL + "Idle_1x1.png", 4, true, 38), new Anim("Move", BARREL + "Move_10x1.png", 12, true, 38),
                                new Anim("Prepare", BARREL + "Prepare_5x1.png", 12, false, 38), new Anim("Dash", BARREL + "Dash_1x1.png", 4, true, 38, 0.15f),
                                new Anim("Attack", BARREL + "ATK_5x1.png", 14, false, 38) },
                tune = e => { e.moveSpeed = 1.0f; e.damage = 10; e.windup = 0.25f; e.cooldown = 0.9f; e.attackRange = 0.12f; e.aggroRange = 5f;
                              e.leashRange = 0f; e.knockback = 1.5f; },
                extra = go => { var k = go.AddComponent<LungeSkills>(); k.prepState = "Prepare"; k.lungeState = "Dash"; k.endState = "Idle";
                                k.meleeState = "Attack"; k.lungeRange = 2.0f; k.lungeSpeed = 3.4f; k.lungeTime = 0.6f; k.prepTime = 0.45f; k.lungePush = 3f; }
            },
            new Def {
                key = "Mimic", display = "Mimic", pivotY = 0.02f, collider = new Vector2(0.16f, 0.07f), hp = 80, mass = 3f,
                scale = 1.8f, kind = EnemyKind.Custom,
                anims = new[] { new Anim("Idle", MIMIC + "Idle_2x1.png", 3, true, 29), new Anim("Move", MIMIC + "Move_24x1.png", 22, true, 29),
                                new Anim("Prepare", MIMIC + "Dash_Begin_4x1.png", 12, false, 29), new Anim("Dash", MIMIC + "Dash_Full_10x1.png", 18, true, 29),
                                new Anim("Recover", MIMIC + "Dash_End_6x1.png", 12, false, 29) },
                tune = e => { e.moveSpeed = 1.3f; e.damage = 14; e.windup = 0.3f; e.cooldown = 1f; e.attackRange = 0.1f; e.aggroRange = 1.1f;
                              e.leashRange = 3f; },
                extra = go => { var k = go.AddComponent<LungeSkills>(); k.prepState = "Prepare"; k.lungeState = "Dash"; k.endState = "Recover";
                                k.meleeState = ""; k.lungeRange = 1.6f; k.lungeSpeed = 3.6f; k.lungeTime = 0.5f; k.prepTime = 0.3f; k.lungeCooldown = 1.2f; }
            },
            new Def {
                key = "Brood_Toad", display = "Gorgemaw, the Brood Toad", pivotY = 0.02f, collider = new Vector2(0.62f, 0.2f), hp = 700, mass = 40f,
                scale = 2.1f, immuneCC = true, kind = EnemyKind.Custom,
                anims = new[] { new Anim("Idle", TOAD + "Idle_2x1.png", 3, true, 116), new Anim("Move", TOAD + "Move_Full_22x1.png", 26, true, 116),
                                new Anim("JumpPrepare", TOAD + "Jump_Prepare_12x1.png", 20, false, 116, 0.03f),
                                new Anim("JumpRecover", TOAD + "Jump_Recover_6x1.png", 8, false, 116, 0.0f),
                                new Anim("Burp", TOAD + "Burp_4x1.png", 8, true, 116) },
                tune = e => { e.treatAsBoss = true; e.leashRange = 0f; e.moveSpeed = 1.2f; e.damage = 14; e.aggroRange = 5f; e.phase2At = 0.5f;
                              e.knockback = 2f; },
                extra = go => { var k = go.AddComponent<ToadSkills>(); k.globPrefab = toadGlob;
                                var g = Sprites(GLOB); k.globShell = g.Length > 0 ? g[0] : null; k.splashFrames = Sprites(SPLASH); }
            },
            new Def {
                key = "Ichor_Ram", display = "The Ether Ram", pivotY = 0.111f, collider = new Vector2(0.42f, 0.12f), hp = 600, mass = 30f,
                scale = 2f, immuneCC = true, kind = EnemyKind.Custom,
                anims = new[] { new Anim("Idle", RAM + "Idle_1x1.png", 4, true, 78), new Anim("Move", RAM + "Move_8x1.png", 10, true, 78),
                                new Anim("ChargeBegin", RAM + "Charge_Begin_24x1.png", 30, false, 78),
                                new Anim("ChargeRun", RAM + "Charge_RunLoop_6x1.png", 16, true, 78),
                                new Anim("ChargeRecover", RAM + "Charge_Recover_6x1.png", 12, false, 78),
                                new Anim("Hit", RAM + "Hit_2x1.png", 6, true, 78) },
                tune = e => { e.treatAsBoss = true; e.leashRange = 0f; e.moveSpeed = 0.7f; e.damage = 18;   // 06/11: thử 2,3 -> người dùng thấy khó chịu, trả về 0,7
                               e.aggroRange = 5f; e.phase2At = 0.5f; },
                extra = go => { var k = go.AddComponent<RamSkills>(); k.rubbleSprites = Sprites(RUBBLE); }
            },
        };

        // 07/11: menu gom về SoulRequiemMenu.cs (tiếng Anh) — trước: [MenuItem("Soul Requiem/Dựng quái Map 2")]
        public static void BuildAllMenu() => BuildAll();


        public static void BuildAll()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Thoát Play Mode trước."); return; }
            // Specimen Zero: sao ảnh CRAWLING DEPTHS vào FX trước khi cắt
            Directory.CreateDirectory(FX);
            foreach (var (src, dst) in new[] { (CRAWL + "Eldritch Entity Dormant.png", SZ_DORMANT), (CRAWL + "Eldritch Entity Awaken.png", SZ_AWAKE),
                                               (CRAWL + "Tentacle 1 Sheet.png", SZ_TENT) })
                if (!File.Exists(dst) && File.Exists(src)) { File.Copy(src, dst); AssetDatabase.ImportAsset(dst); }
            AssetDatabase.Refresh();
            try
            {
                AssetDatabase.StartAssetEditing();
                Slice(SZ_TENT, 100, 0.05f, 0.5f, 48);
                Slice(GLOB, 70, 0.5f, 0.5f, 14);
                Slice(GLOB_POP, 70, 0.5f, 0.5f, 16);
                Slice(SPLASH, 100, 0.5f, 0.5f, 32);
                Slice(RUBBLE, 50, 0.3f, 0.5f, 14);
                foreach (var d in Defs)
                    foreach (var a in d.anims) Slice(a.file, d.ppu, a.pivotY >= 0f ? a.pivotY : d.pivotY, d.pivotX, a.frameW);
                Slice(BEAM, 100, 0.25f, 0.5f, 61);
                Slice(RAT + "bubble-projectile.png", 100, 0.5f, 0.5f, 37);
                Slice(SW + "Beholder/eye-projectile-loop.png", 100, 0.5f, 0.5f, 24);
                Slice(SW + "Beholder/eye-projectile-impact.png", 100, 0.5f, 0.5f, 24);
                Slice(SPIN_FX, 100, 0.135f, 0.48f, 128);
                foreach (var c in new[] { "popping up", "loop", "fading" }) Slice(CLOUD + c + ".png", 100, 0.3f, 0.5f, 200);
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.Refresh();
            Directory.CreateDirectory(OutRoot + "/Animations");
            Directory.CreateDirectory(OutRoot + "/Prefabs");
            AssetDatabase.Refresh();

            // tia của Con Mắt: cắt bản sao (pivot trái-giữa để kéo dài từ con mắt)
            Directory.CreateDirectory(FX);
            if (!File.Exists(BEAM_LOOP)) { File.Copy(BEAM_SRC, BEAM_LOOP); AssetDatabase.ImportAsset(BEAM_LOOP); }
            Slice(BEAM_LOOP, 100, 0.5f, 0f, 32);
            AssetDatabase.Refresh();

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            int order = PlayerSortingOrder();
            var bubble = BuildProjectile("Rat_Bubble", RAT + "bubble-projectile.png", RAT + "bubble-projectile.png", order + 1, 0.1f,
                                         0, 5, 5, 4, p => {
                                             p.cloudPop = Sprites(CLOUD + "popping up.png");
                                             p.cloudLoop = Sprites(CLOUD + "loop.png");
                                             p.cloudFade = Sprites(CLOUD + "fading.png");
                                             p.cloudSeconds = 4f; p.cloudRadius = 0.42f; p.cloudPoison = 0.03f; p.cloudPoisonDuration = 3f;
                                         });
            toadGlob = BuildProjectile("Toad_Glob", GLOB, GLOB_POP, order + 1, 0.08f);
            var eye = eyeBolt = BuildProjectile("Eye_Bolt", SW + "Beholder/eye-projectile-loop.png", SW + "Beholder/eye-projectile-impact.png", order + 1, 0.09f);
            var prefabs = new Dictionary<string, GameObject>();
            foreach (var d in Defs) prefabs[d.key] = BuildEnemy(d, order, d.projectile == "bubble" ? bubble : d.projectile == "eye" ? eye : null);
            foreach (var key in Reuse)
            {
                var p = AssetDatabase.LoadAssetAtPath<GameObject>(Map1Prefabs + key + ".prefab");
                if (p != null) prefabs[key] = p; else Debug.LogWarning("Thiếu prefab Map 1: " + key);
            }

            var dir = UnityEngine.Object.FindFirstObjectByType<MapEncounterDirector>();
            if (dir == null) { Debug.LogError("Scene Map2_UndercityOfVael chưa có MapEncounterDirector (chạy menu Dựng scene Map 2 trước)."); return; }
            Undo.RecordObject(dir, "Map 2 enemies");
            var list = dir.enemies ?? new List<MapEncounterDirector.EnemyEntry>();
            foreach (var kv in prefabs)
            {
                var e = list.Find(x => x != null && string.Equals(x.enemyName, kv.Key, StringComparison.OrdinalIgnoreCase));
                if (e == null) list.Add(new MapEncounterDirector.EnemyEntry { enemyName = kv.Key, prefab = kv.Value });
                else e.prefab = kv.Value;
            }
            dir.enemies = list;
            EditorUtility.SetDirty(dir);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Soul Requiem] Đã dựng {Defs.Length} prefab quái Map 2 ({OutRoot}/Prefabs) + gán {prefabs.Count} loại vào Map2_UndercityOfVael.");
        }

        // ------------------------------------------------------------------ cắt sprite (giống Map1EnemyBuilder)
        static void Slice(string path, float ppu, float pivotY, float pivotX = 0.5f, int frameW = 0)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) { Debug.LogWarning("Không tìm thấy " + path); return; }
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = ppu;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = 4096;
            importer.GetSourceTextureWidthAndHeight(out int w, out int h);
            int frame = frameW > 0 ? frameW : h, count = Mathf.Max(1, w / frame);
            string baseName = Path.GetFileNameWithoutExtension(path);
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var dp = factory.GetSpriteEditorDataProviderFromObject(importer);
            dp.InitSpriteEditorDataProvider();
            var old = new Dictionary<string, GUID>();
            foreach (var r in dp.GetSpriteRects()) if (!old.ContainsKey(r.name)) old[r.name] = r.spriteID;
            var rects = new List<SpriteRect>();
            for (int i = 0; i < count; i++)
            {
                string n = $"{baseName}_{i}";
                rects.Add(new SpriteRect
                {
                    name = n, rect = new Rect(i * frame, 0, frame, h), alignment = SpriteAlignment.Custom,
                    pivot = new Vector2(pivotX, pivotY), border = Vector4.zero,
                    spriteID = old.TryGetValue(n, out var id) ? id : GUID.Generate()
                });
            }
            dp.SetSpriteRects(rects.ToArray());
            var nameIds = dp.GetDataProvider<ISpriteNameFileIdDataProvider>();
            if (nameIds != null) nameIds.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)).ToList());
            dp.Apply();
            importer.SaveAndReimport();
        }

        static Sprite[] Sprites(string path) =>
            AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s => s.rect.x).ToArray();

        static AnimationClip Clip(string folder, string name, Sprite[] frames, float fps, bool loop)
        {
            string p = $"{folder}/{name}.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(p);
            bool isNew = clip == null;
            if (isNew) clip = new AnimationClip();
            clip.frameRate = fps;
            var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
            var keys = new ObjectReferenceKeyframe[frames.Length];
            for (int i = 0; i < frames.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = frames[i] };
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            if (isNew) AssetDatabase.CreateAsset(clip, p); else EditorUtility.SetDirty(clip);
            return clip;
        }

        static AnimatorController Controller(string folder, string name, List<(string state, AnimationClip clip)> states)
        {
            string p = $"{folder}/{name}.controller";
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(p);
            if (ctrl == null)
            {
                if (File.Exists(p)) AssetDatabase.DeleteAsset(p);
                ctrl = AnimatorController.CreateAnimatorControllerAtPath(p);
            }
            if (ctrl.layers.Length == 0) ctrl.AddLayer("Base Layer");
            var sm = ctrl.layers[0].stateMachine;
            foreach (var cs in sm.states.ToArray()) sm.RemoveState(cs.state);
            foreach (var (state, clip) in states)
            {
                var st = sm.AddState(state);
                st.motion = clip;
                st.writeDefaultValues = false;
                if (state == "Idle") sm.defaultState = st;
            }
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssetIfDirty(ctrl);
            return ctrl;
        }

        static GameObject BuildEnemy(Def d, int order, GameObject projectile)
        {
            string animFolder = $"{OutRoot}/Animations/{d.key}";
            Directory.CreateDirectory(animFolder);
            AssetDatabase.Refresh();
            var states = new List<(string, AnimationClip)>();
            float attackLen = 0.6f;
            Sprite first = null;
            foreach (var a in d.anims)
            {
                var frames = Sprites(a.file);
                if (frames.Length == 0) { Debug.LogWarning("Chưa cắt được " + a.file); continue; }
                if (a.state == "Idle") first = frames[0];
                var clip = Clip(animFolder, $"{d.key}_{a.state}", frames, a.fps, a.loop);
                if (a.state == "Attack") attackLen = frames.Length / a.fps;
                states.Add((a.state, clip));
            }
            var ctrl = Controller(animFolder, d.key, states);
            var go = new GameObject(d.key);
            try
            {
                go.transform.localScale = Vector3.one * d.scale;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = first;
                sr.sortingOrder = order;
                sr.spriteSortPoint = SpriteSortPoint.Pivot;
                sr.color = d.tint;
                go.AddComponent<Animator>().runtimeAnimatorController = ctrl;
                var rb = go.AddComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Dynamic;
                rb.gravityScale = 0f;
                rb.freezeRotation = true;
                rb.mass = d.mass;
                rb.linearDamping = 5f;
                rb.interpolation = RigidbodyInterpolation2D.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                var col = go.AddComponent<CapsuleCollider2D>();
                col.direction = CapsuleDirection2D.Horizontal;
                col.size = d.collider;
                col.offset = new Vector2(0f, d.collider.y * 0.35f);
                var hp = go.AddComponent<Health>();
                var so = new SerializedObject(hp);
                so.FindProperty("maxHp").floatValue = d.hp;
                so.FindProperty("isPlayer").boolValue = false;
                so.FindProperty("immuneToCrowdControl").boolValue = d.immuneCC;
                so.ApplyModifiedPropertiesWithoutUndo();
                var ai = go.AddComponent<EnemyAI>();
                ai.displayName = d.display;
                ai.kind = d.kind;
                d.tune?.Invoke(ai);
                ai.attackTime = Mathf.Max(attackLen, ai.windup + 0.1f);
                if (projectile != null) ai.projectilePrefab = projectile;
                d.extra?.Invoke(go);
                EnemyDefinitionTools.ApplyOrCreate(go, d.key, OutRoot);            // asset Definitions/<key> thắng giá trị trong code
                ai.attackTime = Mathf.Max(attackLen, ai.windup + 0.1f);
                return PrefabUtility.SaveAsPrefabAsset(go, $"{OutRoot}/Prefabs/{d.key}.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        static GameObject BuildProjectile(string name, string fly, string boom, int order, float radius,
                                          int flyFrom = 0, int flyCount = 0, int boomFrom = 0, int boomCount = 0, Action<Projectile> extra = null)
        {
            var flyF = Sprites(fly);
            var boomF = Sprites(boom);
            if (flyCount > 0) flyF = flyF.Skip(flyFrom).Take(flyCount).ToArray();       // bong bóng chuột: 5 khung bay, 4 khung vỡ
            if (boomCount > 0) boomF = boomF.Skip(boomFrom).Take(boomCount).ToArray();
            var go = new GameObject(name);
            try
            {
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = flyF.Length > 0 ? flyF[0] : null;
                sr.sortingOrder = order;
                var p = go.AddComponent<Projectile>();
                p.radius = radius;
                p.flyFrames = flyF;
                p.explodeFrames = boomF;
                p.fps = 12f;
                p.spinToDirection = false;
                extra?.Invoke(p);
                return PrefabUtility.SaveAsPrefabAsset(go, $"{OutRoot}/Prefabs/{name}.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        static int PlayerSortingOrder()
        {
            var pc = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            var sr = pc != null ? pc.GetComponentInChildren<SpriteRenderer>() : null;
            return sr != null ? sr.sortingOrder : 2;
        }
    }
}
#endif
