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
    /// 17/11 (§307): Menu Soul Requiem ▸ Enemies ▸ Build Map 4 Enemies (BuildMap4 cũng tự gọi sau khi dựng scene).
    /// Quái The Catacombs of Naming (Map 4) theo bảng người dùng chốt (§296):
    ///   Bone_Jailer     = Tiny Swords Skull (PPU 200 như quái TS Map 1)           — 3 con ập vào khi Ren cầm lại katana (Cell Rows)
    ///   Choir_Cutthroat = Tiny Swords Thief                                       — Blood Pit, Scriptorium (+ The Registrar gọi viện)
    ///   Pit_Bat         = Tiny Swords Giant Bat (không có hoạt ảnh chết -> đứng hình rồi tan)
    ///   Lantern_Warden  = MEGAPACK Knight_LVL4 (chỉ Idle/Move/Dash/Dashend -> Dash = đòn húc LungeSkills; lửa tím đã đổi ĐỎ:
    ///                     Assets/Enemies/Map4/Sprites/Lantern_Warden, tô bằng script — giữ nguyên ảnh gốc của gói)
    ///   Hollow_Cantor   = MEGAPACK Sorcerer_LVL4 (không có hoạt ảnh đánh -> Dash_Begin làm động tác niệm, bắn đạn thánh ca Hymn_Bolt Map 3)
    ///   The_Registrar   = MEGAPACK GameMaster (miniboss giữ Ấn 1: đứng yên, ATK, Upgrade = pha 2, chết = tan biến) — RegistrarSkills
    ///   Lantern_Knight  = (bỏ ở §344 — trùng boss Map 3)
    ///   19/11 (§344) SONG ĐẤU giữ Ấn 2 (The Royal Reliquary): Brazen_Reliquary = MEGAPACK Mimic_LVL4 (pháo đồng) + Fire / Vent (PixelLab),
    ///                Hollow_Champion = MEGAPACK Knight_LVL4 bản gốc lửa tím + Slam / Rage (PixelLab). Ảnh đã dựng sẵn ở
    ///                Assets/Enemies/Map4/Sprites/{Reliquary_Engine,Hollow_Champion} (khung đệm chung cỡ để hoạt ảnh mới khớp chỗ đứng).
    ///                VFX duo_soul_slam / duo_soulfire / duo_muzzle (Resources/VFX), đạn Brass_Shell (trap_fireball).
    /// Mở scene Map4_CatacombsOfNaming, gán đúng danh sách quái Map 4 vào MapEncounterDirector. Chạy lại bao nhiêu lần cũng được.
    /// </summary>
    public static class Map4EnemyBuilder
    {
        const string OutRoot = "Assets/Enemies/Map4";
        const string Scene = "Assets/Scenes/Map4_CatacombsOfNaming.unity";
        const string Map3Prefabs = "Assets/Enemies/Map3/Prefabs/";
        static readonly string[] Reuse = { };   // 19/11 (§344): bỏ Lantern_Knight (trùng boss Map 3)
        const string ENGINE = OutRoot + "/Sprites/Reliquary_Engine/", CHAMP = OutRoot + "/Sprites/Hollow_Champion/";

        const string TSE = "Assets/GamePackage/Tiny Swords (Enemy Pack)/Enemy Pack/";
        const string MEGA = "Assets/GamePackage/CHARACTER MEGAPACK/";
        const string WARDEN = OutRoot + "/Sprites/Lantern_Warden/";

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
            public string projectile;            // "hymn"
            public Action<EnemyAI> tune;
            public Action<GameObject> extra;
        }

        // pivotY đo bằng script (đáy hình chân / bóng của khung Idle): Skull 0.323, Thief 0.312, Giant Bat 0.255, Knight 0.0, Sorcerer 0.0, GameMaster 0.128
        static Def[] Defs => new[]
        {
            new Def {
                key = "Bone_Jailer", display = "Bone Jailer", ppu = 200, pivotY = 0.323f, scale = 1.2f, collider = new Vector2(0.24f, 0.10f), hp = 70, mass = 2f,
                kind = EnemyKind.Melee,
                anims = new[] { new Anim("Idle", TSE + "Skull/Skull_Idle.png", 8, true, 192), new Anim("Move", TSE + "Skull/Skull_Run.png", 10, true, 192),
                                new Anim("Attack", TSE + "Skull/Skull_Attack.png", 12, false, 192) },
                tune = e => { e.moveSpeed = 1.25f; e.damage = 12; e.windup = 0.45f; e.cooldown = 1.2f; e.attackRange = 0.18f; e.aggroRange = 3.6f; e.knockback = 1.5f; }
            },
            new Def {
                key = "Choir_Cutthroat", display = "Choir Cutthroat", ppu = 200, pivotY = 0.312f, scale = 1.15f, collider = new Vector2(0.22f, 0.09f), hp = 50, mass = 1.5f,
                kind = EnemyKind.Melee,
                anims = new[] { new Anim("Idle", TSE + "Thief/Thief_Idle.png", 8, true, 192), new Anim("Move", TSE + "Thief/Thief_Run.png", 12, true, 192),
                                new Anim("Attack", TSE + "Thief/Thief_Attack.png", 14, false, 192) },
                tune = e => { e.moveSpeed = 1.9f; e.damage = 10; e.windup = 0.3f; e.cooldown = 0.9f; e.attackRange = 0.16f; e.aggroRange = 4.0f;
                              e.comboHits = 2; e.comboGap = 0.18f; e.poisonPercent = 0.015f; e.poisonDuration = 4f; }
            },
            new Def {
                key = "Pit_Bat", display = "Pit Bat", ppu = 200, pivotY = 0.255f, scale = 1.15f, collider = new Vector2(0.22f, 0.09f), hp = 45, mass = 1f,
                kind = EnemyKind.Melee,
                anims = new[] { new Anim("Idle", TSE + "Giant Bat/Giant Bat_Idle.png", 10, true, 192), new Anim("Move", TSE + "Giant Bat/Giant Bat_Move.png", 12, true, 192),
                                new Anim("Attack", TSE + "Giant Bat/Giant Bat_Attack.png", 16, false, 192) },
                tune = e => { e.moveSpeed = 2.1f; e.damage = 8; e.windup = 0.3f; e.cooldown = 1.0f; e.attackRange = 0.14f; e.aggroRange = 4.4f; }   // 18/11: bỏ floating — floating = GHIM tại chỗ sinh (kiểu Plague Eye), làm dơi đứng im không đuổi
            },
            new Def {
                key = "Lantern_Warden", display = "Lantern Warden", pivotY = 0.02f, scale = 0.95f, collider = new Vector2(0.30f, 0.12f), hp = 150, mass = 8f,
                kind = EnemyKind.Custom,
                anims = new[] { new Anim("Idle", WARDEN + "Idle_2x1.png", 4, true, 87), new Anim("Move", WARDEN + "Move_24x1.png", 18, true, 87),
                                new Anim("Prepare", WARDEN + "Idle_2x1.png", 10, true, 87), new Anim("Dash", WARDEN + "Dash_2x1.png", 12, true, 87),
                                new Anim("Recover", WARDEN + "Dashend_6x1.png", 12, false, 87) },
                tune = e => { e.moveSpeed = 0.9f; e.damage = 16; e.aggroRange = 3.6f; e.knockback = 2.5f; },
                extra = go => { var k = go.AddComponent<LungeSkills>(); k.meleeState = ""; k.lungeRange = 1.7f; k.lungeSpeed = 3.6f; k.prepTime = 0.55f; k.recoverTime = 0.7f; }
            },
            new Def {
                key = "Hollow_Cantor", display = "Hollow Cantor", pivotY = 0.0f, scale = 1.05f, collider = new Vector2(0.20f, 0.08f), hp = 60, mass = 1.5f,
                kind = EnemyKind.Ranged, projectile = "hymn",
                anims = new[] { new Anim("Idle", MEGA + "Sorcerer_LVL4/Idle_2x1.png", 4, true, 62), new Anim("Move", MEGA + "Sorcerer_LVL4/Move_24x1.png", 18, true, 62),
                                new Anim("Attack", MEGA + "Sorcerer_LVL4/Dash_Begin_4x1.png", 8, false, 62) },
                tune = e => { e.moveSpeed = 1.0f; e.damage = 9; e.windup = 0.5f; e.cooldown = 1.7f; e.aggroRange = 4.4f; e.keepDistance = 2.2f;
                              e.projectileSpeed = 2.3f; e.projectileLifetime = 2.8f; }
            },
            new Def {
                key = "The_Registrar", display = "The Registrar", pivotY = 0.128f, scale = 0.75f, collider = new Vector2(0.34f, 0.12f), hp = 480, mass = 60f,
                immuneCC = true, kind = EnemyKind.Custom, projectile = "hymn",
                anims = new[] { new Anim("Idle", MEGA + "GameMaster/Idle_48x1.png", 16, true, 173), new Anim("Attack", MEGA + "GameMaster/ATK_48x1.png", 40, false, 173),
                                new Anim("Upgrade", MEGA + "GameMaster/Upgrade_40x1.png", 16, false, 173), new Anim("Move", MEGA + "GameMaster/Idle_48x1.png", 16, true, 173) },
                tune = e => { e.treatAsBoss = true; e.phase2At = 0.5f; e.leashRange = 0f; e.moveSpeed = 0.8f;   /* 18/11 (§319): tạm cho đi chậm áp sát tới khi làm lại boss (§5) */ e.damage = 14; e.aggroRange = 6f; e.attackRange = 0.3f; e.cooldown = 0.5f; },
                extra = go => { var k = go.AddComponent<RegistrarSkills>(); k.arenaRadii = new Vector2(2.6f, 1.6f); k.meleeRange = 0.5f; k.preferredRange = 1.8f; k.upgradeSeconds = 40f / 16f; }
            },
        };

        // 19/11 (§344): song đấu Reliquary
        static Def[] DuoDefs => new[]
        {
            new Def {
                key = "Brazen_Reliquary", display = "The Brazen Reliquary", pivotY = 0.02f, scale = 1.35f, collider = new Vector2(0.62f, 0.22f), hp = 520, mass = 60f,
                immuneCC = true, kind = EnemyKind.Custom, projectile = "brass",
                anims = new[] { new Anim("Idle", ENGINE + "Idle_16x1.png", 12, true, 112), new Anim("Move", ENGINE + "Move_16x1.png", 14, true, 112),
                                new Anim("Prepare", ENGINE + "Dash_Begin_8x1.png", 12, false, 112), new Anim("Dash", ENGINE + "Dash_Full_14x1.png", 18, true, 112),
                                new Anim("Recover", ENGINE + "Dash_End_6x1.png", 12, false, 112), new Anim("Fire", ENGINE + "Fire_9x1.png", 14, false, 112),
                                new Anim("Vent", ENGINE + "Vent_9x1.png", 12, true, 112),
                                new Anim("Bite", ENGINE + "Bite_9x1.png", 18, false, 112) },          // 20/11 (§349): PixelLab — Maw Snap pha 2
                tune = e => { e.treatAsBoss = true; e.leashRange = 0f; e.moveSpeed = 0.55f; e.damage = 12; e.aggroRange = 6f; e.attackRange = 0.3f; e.cooldown = 0.5f; e.knockback = 1.2f; },
                extra = go => { var k = go.AddComponent<BrazenReliquarySkills>(); k.arenaRadii = new Vector2(2.1f, 1.0f); k.meleeRange = 0.45f; }
            },
            new Def {
                key = "Hollow_Champion", display = "The Hollow Champion", pivotY = 0.02f, scale = 1.35f, collider = new Vector2(0.42f, 0.16f), hp = 850,   /* 20/11 (§355): 560 -> 850 + Giáp hồn 200 */ mass = 60f,
                immuneCC = true, kind = EnemyKind.Custom,
                anims = new[] { new Anim("Idle", CHAMP + "Idle_2x1.png", 4, true, 96), new Anim("Move", CHAMP + "Move_24x1.png", 18, true, 96),
                                new Anim("Prepare", CHAMP + "Idle_2x1.png", 10, true, 96), new Anim("Dash", CHAMP + "Dash_2x1.png", 12, true, 96),
                                new Anim("Recover", CHAMP + "Dashend_6x1.png", 12, false, 96), new Anim("Slam", CHAMP + "Slam_9x1.png", 12, false, 96),
                                new Anim("Rage", CHAMP + "Rage_6x1.png", 10, false, 96) },
                tune = e => { e.treatAsBoss = true; e.leashRange = 0f; e.moveSpeed = 1.25f; e.damage = 16; e.aggroRange = 6f; e.attackRange = 0.25f; e.cooldown = 0.5f; e.knockback = 2.5f; },
                extra = go => { var k = go.AddComponent<HollowChampionSkills>(); k.arenaRadii = new Vector2(2.1f, 1.0f); k.meleeRange = 0.4f; FillChampionPalette(go); }
            },
        };

        // 20/11 (§350): bản đổi màu lửa phép của Hollow Champion (thư mục con theo màu, cắt y hệt bản gốc) -> ChampionPalette
        // 20/11 (§353): §350 thay ảnh trong LateUpdate không chạy trong game -> nay mỗi màu một AnimatorOverrideController
        //   (Animations/Hollow_Champion/Hollow_Champion_<c>.overrideController: clip Hollow_Champion_<c>_<state> dựng từ ảnh đã đổi màu).
        static readonly string[] ChampSheets = { "Idle_2x1.png", "Move_24x1.png", "Dash_2x1.png", "Dashend_6x1.png", "Slam_9x1.png", "Rage_6x1.png" };
        static readonly string[] CrystalColors = { "crimson", "azure", "verdant", "gilded" };

        static void FillChampionPalette(GameObject go)
        {
            var pal = go.GetComponent<ChampionPalette>();
            if (pal == null) pal = go.AddComponent<ChampionPalette>();
            var anim = go.GetComponent<Animator>();
            var baseCtrl = anim != null ? anim.runtimeAnimatorController as AnimatorController : null;
            if (baseCtrl == null) { Debug.LogWarning("[Map4] Champion chưa có AnimatorController — bỏ qua bản màu."); return; }
            pal.baseController = baseCtrl;
            var def = DuoDefs.First(x => x.key == "Hollow_Champion");
            string folder = $"{OutRoot}/Animations/Hollow_Champion";
            var vars = new List<ChampionPalette.Variant>();
            foreach (var c in CrystalColors)
            {
                string path = $"{folder}/Hollow_Champion_{c}.overrideController";
                var ov = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
                bool isNew = ov == null;
                if (isNew) ov = new AnimatorOverrideController(baseCtrl); else ov.runtimeAnimatorController = baseCtrl;
                var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                foreach (var a in def.anims)
                {
                    var src = baseCtrl.animationClips.FirstOrDefault(k => k != null && k.name == $"Hollow_Champion_{a.state}");
                    if (src == null) continue;
                    var frames = Sprites(a.file.Replace(CHAMP, CHAMP + c + "/"));
                    if (frames.Length == 0) { Debug.LogWarning($"[Map4] Thiếu ảnh Champion bản {c}: {a.file}"); continue; }
                    pairs.Add(new KeyValuePair<AnimationClip, AnimationClip>(src, Clip(folder, $"Hollow_Champion_{c}_{a.state}", frames, a.fps, a.loop)));
                }
                ov.ApplyOverrides(pairs);
                if (isNew) AssetDatabase.CreateAsset(ov, path); else EditorUtility.SetDirty(ov);
                vars.Add(new ChampionPalette.Variant { name = c, controller = ov });
            }
            pal.variants = vars.ToArray();
            AssetDatabase.SaveAssets();
        }

        static readonly (string file, int frameW, float px, float py)[] DuoVfx =
        {
            ("duo_soul_slam.png", 116, 0.5f, 0.5f), ("duo_soulfire.png", 54, 0.5f, 0.19f), ("duo_muzzle.png", 56, 0.41f, 0.53f),
            ("duo_keg_blast.png", 96, 0.5f, 0.5f),
            ("duo_ward.png", 136, 0.5f, 0.143f), ("duo_bolt.png", 96, 0.5f, 0.14f),                    // 20/11 (§349): khiên hồn Engine, tia sét ô quay
            // 20/11 (§350): lửa hồn / vòng nện theo màu pha lê
            ("duo_soul_slam_crimson.png", 116, 0.5f, 0.5f), ("duo_soul_slam_azure.png", 116, 0.5f, 0.5f), ("duo_soul_slam_verdant.png", 116, 0.5f, 0.5f), ("duo_soul_slam_gilded.png", 116, 0.5f, 0.5f),
            ("duo_soulfire_crimson.png", 54, 0.5f, 0.19f), ("duo_soulfire_azure.png", 54, 0.5f, 0.19f), ("duo_soulfire_verdant.png", 54, 0.5f, 0.19f), ("duo_soulfire_gilded.png", 54, 0.5f, 0.19f),
            // 20/11 (§353): VFX PixelLab mới của Champion (gốc = anchor trong file json của PixelLab)
            ("duo_quake.png", 112, 0.5f, 0.375f),                                                       // Grave Quake: nứt đá + bụi
            ("duo_fire_ring.png", 128, 0.5f, 0.3125f), ("duo_flame.png", 48, 0.5f, 0.2368f),             // crimson
            ("duo_frost_ward.png", 96, 0.5f, 0.5f), ("duo_ice_burst.png", 96, 0.5f, 0.2188f),            // azure
            ("duo_heal.png", 80, 0.5f, 0.1786f), ("duo_thorns.png", 96, 0.5f, 0.2188f),                  // verdant
            ("duo_smite.png", 96, 0.5f, 0.1406f), ("duo_light_orb.png", 32, 0.5f, 0.5f),                // gilded
            ("duo_aura_crimson.png", 96, 0.5f, 0.125f), ("duo_aura_azure.png", 96, 0.5f, 0.125f),        // vòng aura nguyên tố dưới chân
            ("duo_aura_verdant.png", 96, 0.5f, 0.125f), ("duo_aura_gilded.png", 96, 0.5f, 0.125f),
            // 20/11 (§356): người nộm MEGAPACK Dummy_LVL1-4 cho The Drill Yard (Resources/VFX/drill/, chân = đáy khung)
            ("drill/sack_idle.png", 34, 0.5f, 0f), ("drill/sack_move.png", 34, 0.5f, 0f), ("drill/sack_roll.png", 34, 0.5f, 0f), ("drill/sack_sleep.png", 34, 0.5f, 0f),
            ("drill/straw_idle.png", 33, 0.5f, 0f), ("drill/straw_move.png", 33, 0.5f, 0f), ("drill/straw_spin.png", 33, 0.5f, 0f),
            ("drill/pell_idle.png", 65, 0.5f, 0.0238f), ("drill/pell_move.png", 65, 0.5f, 0.0238f), ("drill/pell_spin.png", 65, 0.5f, 0.0238f), ("drill/pell_top.png", 65, 0.5f, 0.0238f),
            ("drill/king_idle.png", 74, 0.5f, 0f), ("drill/king_move.png", 74, 0.5f, 0f), ("drill/king_dash.png", 74, 0.5f, 0f), ("drill/king_dashend.png", 74, 0.5f, 0f),                                                    // 19/11 (§345): thùng thuốc nổ
            // 20/11 (§360): VFX PixelLab của Burlap's Proving Grounds (gốc = anchor trong json PixelLab) + mũi tên báo + tấm ép + bao cỏ
            ("drill/fx_hit.png", 72, 0.4861f, 0.6923f), ("drill/fx_whirl.png", 65, 0.4769f, 0.5f), ("drill/fx_hayburst.png", 89, 0.4944f, 0.6797f),
            ("drill/fx_snuff.png", 32, 0.5f, 0.5588f), ("drill/fx_arrow.png", 16, 0.5f, 0.5f), ("drill/fx_plate.png", 32, 0.5f, 0.5f), ("drill/fx_bale.png", 28, 0.5f, 0.5f),
        };

        static readonly string[] GalleryProps =
        {
            "warden_statue_0", "warden_statue_0_c1", "warden_statue_0_c2", "warden_statue_1", "warden_statue_1_c1", "warden_statue_1_c2",
            "warden_plinth_0", "warden_plinth_1", "powder_keg",
            // 20/11 (§349): giá vũ khí + vũ khí trên tay Champion + biểu tượng ô quay
            "relic_rack_empty", "relic_rack_halberd", "relic_rack_hammer", "relic_rack_flail", "relic_rack_axes",
            "relic_halberd", "relic_hammer", "relic_flail", "relic_axe",
            // 20/11 (§350): cột pha lê 4 màu (+ bản cạn), mảnh pha lê, cụm pha lê, bệ pháo, đồ trang trí phòng
            "crystal_shelf_crimson", "crystal_shelf_azure", "crystal_shelf_verdant", "crystal_shelf_gilded",
            "crystal_shelf_crimson_spent", "crystal_shelf_azure_spent", "crystal_shelf_verdant_spent", "crystal_shelf_gilded_spent",
            "crystal_shelf_crimson_lit", "crystal_shelf_azure_lit", "crystal_shelf_verdant_lit", "crystal_shelf_gilded_lit",   // 20/11 (§353): lớp tự sáng của kệ
            "crystal_shard_crimson", "crystal_shard_azure", "crystal_shard_verdant", "crystal_shard_gilded",
            "crystal_cluster_crimson", "crystal_cluster_azure", "crystal_cluster_verdant", "crystal_cluster_gilded",
            "engine_pedestal", "coin_heap", "wall_gear", "lists_banner", "soul_brazier",
            "drill_horn", "drill_ledger", "drill_keyped", "drill_keyring",   // 20/11 (§356): The Drill Yard
            "drill_lantern", "drill_lantern_out", "drill_launcher", "drill_launcher_empty", "drill_rack",                  // 20/11 (§360): Lantern Run + máy phóng bao cỏ
            "drill_crate", "drill_crate_stack", "drill_crate_stack2",                                // 20/11 (§360): kho quân nhu (đẩy thùng)
            "reel_frame", "reel_goblin", "reel_bomb", "reel_skull", "reel_coin", "reel_bolt", "reel_anvil", "reel_heart", "reel_bust",
        };

        public static void BuildAllMenu() => BuildAll();

        public static void BuildAll()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Thoát Play Mode trước."); return; }
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var d in Defs.Concat(DuoDefs))
                    foreach (var a in d.anims) Slice(a.file, d.ppu, a.pivotY >= 0f ? a.pivotY : d.pivotY, d.pivotX, a.frameW);
                foreach (var v in DuoVfx) Slice(VFX + v.file, 100f, v.py, v.px, v.frameW);   // 19/11 (§344)
                Slice(VFX + "m4_op_door_v.png", 100f, 0f, 0f, 96);                            // 20/11 (§356): cửa dọc (đóng / mở) cho gate_visual, gốc dưới-trái
                var champ = DuoDefs.First(x => x.key == "Hollow_Champion");                   // 20/11 (§350): bản đổi màu cắt y hệt bản gốc
                foreach (var c in CrystalColors)
                    foreach (var f in ChampSheets) Slice(CHAMP + c + "/" + f, champ.ppu, champ.pivotY, champ.pivotX, 96);
                foreach (var f in GalleryProps)                                               // 19/11 (§345): tượng / bệ / thùng — chân ảnh ở mép dưới
                    Slice(VFX + "props/" + f + ".png", 100f, 0f, 0.5f, -1);                   // -1 = cả ảnh là một khung
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.Refresh();
            Directory.CreateDirectory(OutRoot + "/Animations");
            Directory.CreateDirectory(OutRoot + "/Prefabs");
            AssetDatabase.Refresh();

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(Scene)) { Debug.LogError("Chưa có " + Scene + " (chạy menu Build Map 4 Scene trước)."); return; }
            var scene = EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            int order = PlayerSortingOrder();
            var hymn = AssetDatabase.LoadAssetAtPath<GameObject>(Map3Prefabs + "Hymn_Bolt.prefab");      // đạn thánh ca dựng ở Map 3
            if (hymn == null) Debug.LogWarning("Thiếu " + Map3Prefabs + "Hymn_Bolt.prefab (chạy Build Map 3 Enemies một lần).");
            var prefabs = new Dictionary<string, GameObject>();
            foreach (var d in Defs) prefabs[d.key] = BuildEnemy(d, order, d.projectile == "hymn" ? hymn : null);
            // 19/11 (§344): song đấu Reliquary + đạn đồng (quả cầu lửa của bẫy, nổ khi chạm)
            var brass = BuildProjectile("Brass_Shell", VFX + "trap_fireball.png", VFX + "trap_fireball_end.png", order + 1, 0.1f, Color.white, true, 0.8f);
            foreach (var d in DuoDefs) prefabs[d.key] = BuildEnemy(d, order, d.projectile == "brass" ? brass : null);
            foreach (var key in Reuse)
            {
                var p = AssetDatabase.LoadAssetAtPath<GameObject>(Map3Prefabs + key + ".prefab");
                if (p != null) prefabs[key] = p; else Debug.LogWarning("Thiếu prefab Map 3: " + key);
            }
            // 20/11 (§349): ô GOBLIN của Jackpot Engine bắn goblin cầm đuốc của Map 1
            var gob = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemies/Map1/Prefabs/Goblin_Torch.prefab");
            if (gob != null) prefabs["Goblin_Torch"] = gob; else Debug.LogWarning("Thiếu prefab Map 1: Goblin_Torch (chạy Build Map 1 Enemies).");
            var dir = UnityEngine.Object.FindFirstObjectByType<MapEncounterDirector>();
            if (dir == null) { Debug.LogError("Scene Map 4 chưa có MapEncounterDirector."); return; }
            Undo.RecordObject(dir, "Map 4 enemies");
            var list = new List<MapEncounterDirector.EnemyEntry>();          // Map 4 chỉ dùng quái của nó (scene chép từ Map 3)
            foreach (var kv in prefabs) list.Add(new MapEncounterDirector.EnemyEntry { enemyName = kv.Key, prefab = kv.Value });
            dir.enemies = list;
            EditorUtility.SetDirty(dir);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Soul Requiem] Đã dựng {Defs.Length} prefab quái Map 4 ({OutRoot}/Prefabs) + gán {prefabs.Count} loại vào Map4_CatacombsOfNaming.");
        }

        // ------------------------------------------------------------------ cắt sprite (giống Map3EnemyBuilder)
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
            importer.maxTextureSize = 8192;
            importer.GetSourceTextureWidthAndHeight(out int w, out int h);
            int frame = frameW > 0 ? frameW : frameW < 0 ? w : h, count = Mathf.Max(1, w / frame);
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
                col.size = d.collider / Mathf.Max(0.01f, d.scale);           // collider tính theo thế giới, không phóng theo scale
                col.offset = new Vector2(0f, col.size.y * 0.35f);
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
                if (VfxCfg.TryGetValue(d.key, out var vcfg)) vcfg(go.AddComponent<EnemyVfx>());
                return PrefabUtility.SaveAsPrefabAsset(go, $"{OutRoot}/Prefabs/{d.key}.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        // ---------------------------------------------------------------- VFX (dùng bộ m3_* đã có trong Resources/VFX)
        const string VFX = "Assets/Resources/VFX/";
        static readonly Dictionary<string, Action<EnemyVfx>> VfxCfg = new Dictionary<string, Action<EnemyVfx>>
        {
            ["Bone_Jailer"] = v => { v.swing = "m3_slash_steel"; v.windup = "m3_windup"; v.death = "m3_death_soul"; v.chestY = 0.25f; },
            ["Choir_Cutthroat"] = v => { v.swing = "m3_claw"; v.windup = "m3_windup"; v.death = "m3_death_soul"; v.chestY = 0.25f; },
            ["Pit_Bat"] = v => { v.swing = "m3_claw"; v.death = "m3_death_blood"; v.chestY = 0.3f; },
            ["Lantern_Warden"] = v => { v.windup = "m3_windup"; v.dash = "m3_dash_dust"; v.death = "m3_death_soul"; v.chestY = 0.35f; },
            ["Hollow_Cantor"] = v => { v.cast = "m3_cast_hymn"; v.death = "m3_death_soul"; v.chestY = 0.3f; },
            ["The_Registrar"] = v => { v.cast = "m3_cast_hymn"; v.death = "m3_death_soul"; v.chestY = 0.45f; },
            ["Brazen_Reliquary"] = v => { v.dash = "m3_dash_dust"; v.death = "m3_death_soul"; v.chestY = 0.35f; },   // 19/11 (§344)
            ["Hollow_Champion"] = v => { v.windup = "m3_windup"; v.dash = "m3_dash_dust"; v.death = "m3_death_soul"; v.chestY = 0.45f; },
        };

        static GameObject BuildProjectile(string name, string fly, string boom, int order, float radius, Color tint, bool spin, float scale = 1f)
        {
            var flyF = Sprites(fly);
            var boomF = Sprites(boom);
            var go = new GameObject(name);
            go.transform.localScale = new Vector3(scale, scale, 1f);
            try
            {
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = flyF.Length > 0 ? flyF[0] : null;
                sr.sortingOrder = order;
                sr.color = tint;
                var p = go.AddComponent<Projectile>();
                p.radius = radius;
                p.flyFrames = flyF;
                p.explodeFrames = boom == fly ? new Sprite[0] : boomF;      // mũi tên: ảnh vỡ riêng (VFX) nếu có
                p.fps = 12f;
                p.spinToDirection = spin;
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
