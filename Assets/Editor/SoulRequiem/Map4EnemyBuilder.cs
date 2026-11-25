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
    /// Map 4 enemy builder. Menu: Soul Requiem ▸ Enemies ▸ Map 4 (declared in SoulRequiemMenu; Build Map ▸ Map 4 runs it after the scene).
    /// The Catacombs of Naming (Map 4) enemies:
    ///   Bone_Jailer     = Tiny Swords Skull (PPU 200 like the Map 1 TS enemies)      — 3 rush in when Ren takes his katana back (Cell Rows)
    ///   Choir_Cutthroat = Tiny Swords Thief                                           — Blood Pit, Scriptorium (+ The Registrar's reinforcements)
    ///   Pit_Bat         = Tiny Swords Giant Bat (no death animation -> freezes then fades)
    ///   Lantern_Warden  = MEGAPACK Knight_LVL4 (only Idle/Move/Dash/Dashend -> Dash = LungeSkills charge; the violet fire is recoloured RED:
    ///                     Assets/Enemies/Map4/Sprites/Lantern_Warden, painted by script — the pack's original images are untouched)
    ///   Hollow_Cantor   = MEGAPACK Sorcerer_LVL4 (no attack animation -> Dash_Begin is the cast pose, fires the Map 3 Hymn_Bolt)
    ///   The_Registrar   = MEGAPACK GameMaster (Seal 1 boss) — RegistrarSkills
    ///   DUO guarding Seal 2 (The Royal Reliquary): Brazen_Reliquary = MEGAPACK Mimic_LVL4 (brass cannon) + Fire / Vent (PixelLab),
    ///                Hollow_Champion = MEGAPACK Knight_LVL4 original violet fire + Slam / Rage (PixelLab). Prebuilt images in
    ///                Assets/Enemies/Map4/Sprites/{Reliquary_Engine,Hollow_Champion} (shared padded frames so new animations line up).
    ///                VFX duo_soul_slam / duo_soulfire / duo_muzzle (Resources/VFX), bullet Brass_Shell (trap_fireball).
    ///   Also: elemental Acolytes, Mage_Skeleton (The Margin), Cinder_Imp (The Unwritten), Drill Yard dummies and props.
    /// Opens scene Map4_CatacombsOfNaming and assigns exactly the Map 4 enemy list to the MapEncounterDirector. Safe to rerun any number of times.
    /// Needs: Map 1 prefabs (Goblin_Torch), Map 3 prefabs (Hymn_Bolt, Skeleton), the Map 4 scene.
    /// </summary>
    public static class Map4EnemyBuilder
    {
        const string OutRoot = "Assets/Enemies/Map4";
        const string Scene = "Assets/Scenes/Map4_CatacombsOfNaming.unity";
        const string Map3Prefabs = "Assets/Enemies/Map3/Prefabs/";
        static readonly string[] Reuse = { "Skeleton" };   // no Lantern_Knight (same as the Map 3 boss); Skeleton (Crypt) is drawn by The Registrar
        const string ENGINE = OutRoot + "/Sprites/Reliquary_Engine/", CHAMP = OutRoot + "/Sprites/Hollow_Champion/";

        const string TSE = "Assets/GamePackage/Tiny Swords (Enemy Pack)/Enemy Pack/";
        const string MEGA = "Assets/GamePackage/CHARACTER MEGAPACK/";
        const string OLDP = "Assets/GamePackage/EPIC RPG World Pack - Old Prison V1.7.1/Characters/Mage Skeleton/";
        const string OLDP_NS = OLDP + "Mage Skeleton no shield/";
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

        // pivotY measured by script (bottom of the feet / shadow in the Idle frame): Skull 0.323, Thief 0.312, Giant Bat 0.255, Knight 0.0, Sorcerer 0.0, GameMaster 0.128
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
                tune = e => { e.moveSpeed = 2.1f; e.damage = 8; e.windup = 0.3f; e.cooldown = 1.0f; e.attackRange = 0.14f; e.aggroRange = 4.4f; }   // not floating — floating = PINNED at its spawn point (like Plague Eye), which made the bat stand still instead of chasing
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
                key = "The_Registrar", display = "The Registrar", pivotY = 0.128f, scale = 1.5f, collider = new Vector2(0.68f, 0.24f), hp = 900, mass = 60f,   /* 2x size; HP = the "pages" bar (survival fight) */
                immuneCC = true, kind = EnemyKind.Custom, projectile = "hymn",
                anims = new[] { new Anim("Idle", MEGA + "GameMaster/Idle_48x1.png", 16, true, 173), new Anim("Attack", MEGA + "GameMaster/ATK_48x1.png", 40, false, 173),
                                new Anim("Upgrade", MEGA + "GameMaster/Upgrade_40x1.png", 16, false, 173), new Anim("Move", MEGA + "GameMaster/Idle_48x1.png", 16, true, 173) },
                tune = e => { e.treatAsBoss = true; e.phase2At = 0f; e.leashRange = 0f; e.moveSpeed = 0f;   /* stands behind the lectern, does not walk */ e.damage = 14; e.aggroRange = 14f; e.attackRange = 0.3f; e.cooldown = 0.5f; },
                extra = go => { var k = go.AddComponent<RegistrarSkills>(); k.arenaRadii = new Vector2(3.2f, 2.1f); k.upgradeSeconds = 40f / 16f; }   // The Margin fight
            },
            // ELEMENTAL ACOLYTES "The Choir of the Named" — PixelLab 35d3ff2b (1 teal-hood original -> 4 recolours,
            // _tools/s390/build_acolytes.py), 3 directions (Idle/Move/Attack + _S/_N, Death side only), frame 192x176 feet at (96,156) -> pivotY 20/176.
            Acolyte("Acolyte_Fire", "Pyre Acolyte", AcolyteElement.Fire, 70),
            Acolyte("Acolyte_Frost", "Rime Acolyte", AcolyteElement.Frost, 75),
            Acolyte("Acolyte_Storm", "Storm Acolyte", AcolyteElement.Storm, 65),
            Acolyte("Acolyte_Umbral", "Umbral Acolyte", AcolyteElement.Umbral, 70),
            // MAGE SKELETON (Old Prison pack, shieldless version, frame 220x220, feet 0.327) — DRAWN by The Registrar in The Margin.
            // Fires violet Mage_Orb bullets.
            new Def {
                key = "Mage_Skeleton", display = "Mage Skeleton", pivotY = 0.327f, scale = 1.15f, collider = new Vector2(0.2f, 0.08f), hp = 40, mass = 1.5f,
                kind = EnemyKind.Ranged, projectile = "orb",
                anims = new[] { new Anim("Idle", OLDP_NS + "Mage Skeleton - animations-idle.png", 8, true, 220),
                                new Anim("Move", OLDP_NS + "Mage Skeleton - animations-walk.png", 10, true, 220),
                                new Anim("Attack", OLDP_NS + "Mage Skeleton - animations-atk1-loopable.png", 12, false, 220),
                                new Anim("Death", OLDP_NS + "Mage Skeleton - animations-death.png", 14, false, 220) },
                tune = e => { e.moveSpeed = 0.8f; e.damage = 8; e.windup = 0.6f; e.cooldown = 2.0f; e.aggroRange = 9f; e.leashRange = 0f; e.keepDistance = 2.0f;
                              e.projectileSpeed = 2.0f; e.projectileLifetime = 3f; }
            },
            // CINDER IMP — Volcano pack imp (with shadow, frame 192x160, feet = shadow bottom 0.331), dropped by the Vessel in Phase 2 CINDER (domain The Unwritten).
            // Images laid out by _tools/s418/make_cinder.py -> Assets/Enemies/Map4/Sprites/Cinder_Imp/.
            new Def {
                key = "Cinder_Imp", display = "Cinder Imp", pivotY = 0.3312f, scale = 1.0f, collider = new Vector2(0.2f, 0.08f), hp = 26, mass = 1f,
                kind = EnemyKind.Melee,
                anims = new[] { new Anim("Idle", IMP + "Idle_9x1.png", 10, true, 192), new Anim("Move", IMP + "Move_6x1.png", 12, true, 192),
                                new Anim("Attack", IMP + "Attack_13x1.png", 18, false, 192), new Anim("Death", IMP + "Death_29x1.png", 24, false, 192) },
                tune = e => { e.moveSpeed = 1.8f; e.damage = 8; e.windup = 0.35f; e.cooldown = 1.1f; e.attackRange = 0.16f; e.aggroRange = 9f; e.leashRange = 0f; }
            },
        };
        const string IMP = OutRoot + "/Sprites/Cinder_Imp/";

        const string ACOS = OutRoot + "/Sprites/";

        static Def Acolyte(string key, string display, AcolyteElement el, float hp)
        {
            string p = ACOS + key + "/";
            return new Def {
                key = key, display = display, pivotY = 20f / 176f, scale = 0.72f,   /* 0.72: body ~0.76 u ≈ 1.1x Ren (0.68) */ collider = new Vector2(0.22f, 0.09f), hp = hp, mass = 1.5f,
                kind = EnemyKind.Custom,
                anims = new[] {
                    new Anim("Idle", p + "Idle_5x1.png", 6, true, 192), new Anim("Idle_S", p + "Idle_S_5x1.png", 6, true, 192), new Anim("Idle_N", p + "Idle_N_5x1.png", 6, true, 192),
                    new Anim("Move", p + "Move_6x1.png", 10, true, 192), new Anim("Move_S", p + "Move_S_8x1.png", 10, true, 192), new Anim("Move_N", p + "Move_N_8x1.png", 10, true, 192),
                    new Anim("Attack", p + "Attack_9x1.png", 14, false, 192), new Anim("Attack_S", p + "Attack_S_9x1.png", 14, false, 192), new Anim("Attack_N", p + "Attack_N_9x1.png", 14, false, 192),
                    new Anim("Death", p + "Death_9x1.png", 10, false, 192) },
                tune = e => { e.moveSpeed = 1.1f; e.damage = 9; e.windup = 0.5f; e.cooldown = 1.0f; e.aggroRange = 4.8f; e.keepDistance = 2.2f; e.attackRange = 0.3f; },
                extra = go => { var k = go.AddComponent<AcolyteSkills>(); k.element = el; }
            };
        }

        // Reliquary duo
        static Def[] DuoDefs => new[]
        {
            new Def {
                key = "Brazen_Reliquary", display = "The Brazen Reliquary", pivotY = 0.02f, scale = 1.35f, collider = new Vector2(0.62f, 0.22f), hp = 520, mass = 60f,
                immuneCC = true, kind = EnemyKind.Custom, projectile = "brass",
                anims = new[] { new Anim("Idle", ENGINE + "Idle_16x1.png", 12, true, 112), new Anim("Move", ENGINE + "Move_16x1.png", 14, true, 112),
                                new Anim("Prepare", ENGINE + "Dash_Begin_8x1.png", 12, false, 112), new Anim("Dash", ENGINE + "Dash_Full_14x1.png", 18, true, 112),
                                new Anim("Recover", ENGINE + "Dash_End_6x1.png", 12, false, 112), new Anim("Fire", ENGINE + "Fire_9x1.png", 14, false, 112),
                                new Anim("Vent", ENGINE + "Vent_9x1.png", 12, true, 112),
                                new Anim("Bite", ENGINE + "Bite_9x1.png", 18, false, 112) },          // PixelLab — Maw Snap pha 2
                tune = e => { e.treatAsBoss = true; e.leashRange = 0f; e.moveSpeed = 0.55f; e.damage = 12; e.aggroRange = 6f; e.attackRange = 0.3f; e.cooldown = 0.5f; e.knockback = 1.2f; },
                extra = go => { var k = go.AddComponent<BrazenReliquarySkills>(); k.arenaRadii = new Vector2(2.1f, 1.0f); k.meleeRange = 0.45f; }
            },
            new Def {
                key = "Hollow_Champion", display = "The Hollow Champion", pivotY = 0.02f, scale = 1.35f, collider = new Vector2(0.42f, 0.16f), hp = 850,   /* 850 HP + Soul Armour 200 */ mass = 60f,
                immuneCC = true, kind = EnemyKind.Custom,
                anims = new[] { new Anim("Idle", CHAMP + "Idle_2x1.png", 4, true, 96), new Anim("Move", CHAMP + "Move_24x1.png", 18, true, 96),
                                new Anim("Prepare", CHAMP + "Idle_2x1.png", 10, true, 96), new Anim("Dash", CHAMP + "Dash_2x1.png", 12, true, 96),
                                new Anim("Recover", CHAMP + "Dashend_6x1.png", 12, false, 96), new Anim("Slam", CHAMP + "Slam_9x1.png", 12, false, 96),
                                new Anim("Rage", CHAMP + "Rage_6x1.png", 10, false, 96) },
                tune = e => { e.treatAsBoss = true; e.leashRange = 0f; e.moveSpeed = 1.25f; e.damage = 16; e.aggroRange = 6f; e.attackRange = 0.25f; e.cooldown = 0.5f; e.knockback = 2.5f; },
                extra = go => { var k = go.AddComponent<HollowChampionSkills>(); k.arenaRadii = new Vector2(2.1f, 1.0f); k.meleeRange = 0.4f; FillChampionPalette(go); }
            },
        };

        // Hollow Champion spell-fire recolours (subfolder per colour, sliced exactly like the original) -> ChampionPalette.
        // Swapping sprites in LateUpdate did not work in game -> one AnimatorOverrideController per colour
        //   (Animations/Hollow_Champion/Hollow_Champion_<c>.overrideController: clips Hollow_Champion_<c>_<state> built from the recoloured images).
        static readonly string[] ChampSheets = { "Idle_2x1.png", "Move_24x1.png", "Dash_2x1.png", "Dashend_6x1.png", "Slam_9x1.png", "Rage_6x1.png" };
        static readonly string[] CrystalColors = { "crimson", "azure", "verdant", "gilded" };

        static void FillChampionPalette(GameObject go)
        {
            var pal = go.GetComponent<ChampionPalette>();
            if (pal == null) pal = go.AddComponent<ChampionPalette>();
            var anim = go.GetComponent<Animator>();
            var baseCtrl = anim != null ? anim.runtimeAnimatorController as AnimatorController : null;
            if (baseCtrl == null) { Debug.LogWarning("[Map4] Champion has no AnimatorController - skipping the colour variants."); return; }
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
                    if (frames.Length == 0) { Debug.LogWarning($"[Map4] Missing Champion image for colour {c}: {a.file}"); continue; }
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
            ("duo_ward.png", 136, 0.5f, 0.143f), ("duo_bolt.png", 96, 0.5f, 0.14f),                    // Engine soul shield, slot-reel lightning
            // soul fire / slam ring in the crystal colour
            ("duo_soul_slam_crimson.png", 116, 0.5f, 0.5f), ("duo_soul_slam_azure.png", 116, 0.5f, 0.5f), ("duo_soul_slam_verdant.png", 116, 0.5f, 0.5f), ("duo_soul_slam_gilded.png", 116, 0.5f, 0.5f),
            ("duo_soulfire_crimson.png", 54, 0.5f, 0.19f), ("duo_soulfire_azure.png", 54, 0.5f, 0.19f), ("duo_soulfire_verdant.png", 54, 0.5f, 0.19f), ("duo_soulfire_gilded.png", 54, 0.5f, 0.19f),
            // new PixelLab Champion VFX (origin = anchor in the PixelLab json)
            ("duo_quake.png", 112, 0.5f, 0.375f),                                                       // Grave Quake: cracked stone + dust
            ("duo_fire_ring.png", 128, 0.5f, 0.3125f), ("duo_flame.png", 48, 0.5f, 0.2368f),             // crimson
            ("duo_frost_ward.png", 96, 0.5f, 0.5f), ("duo_ice_burst.png", 96, 0.5f, 0.2188f),            // azure
            ("duo_heal.png", 80, 0.5f, 0.1786f), ("duo_thorns.png", 96, 0.5f, 0.2188f),                  // verdant
            ("duo_smite.png", 96, 0.5f, 0.1406f), ("duo_light_orb.png", 32, 0.5f, 0.5f),                // gilded
            ("duo_aura_crimson.png", 96, 0.5f, 0.125f), ("duo_aura_azure.png", 96, 0.5f, 0.125f),        // element aura ring under the feet
            ("duo_aura_verdant.png", 96, 0.5f, 0.125f), ("duo_aura_gilded.png", 96, 0.5f, 0.125f),
            // MEGAPACK Dummy_LVL1-4 training dummies for The Drill Yard (Resources/VFX/drill/, feet = frame bottom)
            ("drill/sack_idle.png", 34, 0.5f, 0f), ("drill/sack_move.png", 34, 0.5f, 0f), ("drill/sack_roll.png", 34, 0.5f, 0f), ("drill/sack_sleep.png", 34, 0.5f, 0f),
            ("drill/straw_idle.png", 33, 0.5f, 0f), ("drill/straw_move.png", 33, 0.5f, 0f), ("drill/straw_spin.png", 33, 0.5f, 0f),
            ("drill/pell_idle.png", 65, 0.5f, 0.0238f), ("drill/pell_move.png", 65, 0.5f, 0.0238f), ("drill/pell_spin.png", 65, 0.5f, 0.0238f), ("drill/pell_top.png", 65, 0.5f, 0.0238f),
            ("drill/king_idle.png", 74, 0.5f, 0f), ("drill/king_move.png", 74, 0.5f, 0f), ("drill/king_dash.png", 74, 0.5f, 0f), ("drill/king_dashend.png", 74, 0.5f, 0f),                                                    // powder keg
            // PixelLab VFX of Burlap's Proving Grounds (origin = anchor in the PixelLab json) + warning arrows + pressure plates + straw sacks
            ("drill/fx_hit.png", 72, 0.4861f, 0.6923f), ("drill/fx_whirl.png", 65, 0.4769f, 0.5f), ("drill/fx_hayburst.png", 89, 0.4944f, 0.6797f),
            ("drill/fx_snuff.png", 32, 0.5f, 0.5588f), ("drill/fx_arrow.png", 16, 0.5f, 0.5f), ("drill/fx_plate.png", 32, 0.5f, 0.5f), ("drill/fx_bale.png", 28, 0.5f, 0.5f), ("drill/fx_trapdoor.png", 60, 0.5f, 0.5f),   // / trapdoor 6 frames 60 px
        };

        static readonly string[] GalleryProps =
        {
            "warden_statue_0", "warden_statue_0_c1", "warden_statue_0_c2", "warden_statue_1", "warden_statue_1_c1", "warden_statue_1_c2",
            "warden_plinth_0", "warden_plinth_1", "powder_keg",
            // weapon rack + weapons in the Champion's hand + slot-reel icons
            "relic_rack_empty", "relic_rack_halberd", "relic_rack_hammer", "relic_rack_flail", "relic_rack_axes",
            "relic_halberd", "relic_hammer", "relic_flail", "relic_axe",
            // 4-colour crystal pillars (+ drained version), crystal shards, crystal clusters, cannon base, room decor
            "crystal_shelf_crimson", "crystal_shelf_azure", "crystal_shelf_verdant", "crystal_shelf_gilded",
            "crystal_shelf_crimson_spent", "crystal_shelf_azure_spent", "crystal_shelf_verdant_spent", "crystal_shelf_gilded_spent",
            "crystal_shelf_crimson_lit", "crystal_shelf_azure_lit", "crystal_shelf_verdant_lit", "crystal_shelf_gilded_lit",   // shelf self-lit layer
            "crystal_shard_crimson", "crystal_shard_azure", "crystal_shard_verdant", "crystal_shard_gilded",
            "crystal_cluster_crimson", "crystal_cluster_azure", "crystal_cluster_verdant", "crystal_cluster_gilded",
            "engine_pedestal", "coin_heap", "wall_gear", "lists_banner", "soul_brazier",
            "drill_horn", "drill_ledger", "drill_keyped", "drill_keyring",   // The Drill Yard
            "drill_lantern", "drill_lantern_out", "drill_launcher", "drill_launcher_empty", "drill_rack",                  // Lantern Run + straw sack launcher
            "drill_crate", "drill_crate_stack", "drill_crate_stack2",                                // quartermaster's store (crate pushing)
            "drill_board", "drill_mortar", "drill_haystack", "drill_king_legs", "drill_king_crown",   // Final Exam (blackboard, straw mortar, straw piles, Burlap's feet + crown)
            "drill_ammo_crate", "drill_hay_burnt",                                                    // Live Fire (straw ammo crate, straw piles burning to ash)
            "margin_lectern", "margin_ledgers", "margin_quill", "margin_inkwell", "margin_puddle", "margin_page", "margin_page2", "margin_page3", "margin_rule", "margin_inkspike",      // The Margin (props on the page)
            "pickup_potion", "pickup_flask_upgrade", "pickup_green_tonic", "pickup_skill_scroll",   // pickups lying on the floor
            "margin_tome", "margin_tome_open", "margin_tome_open2", "margin_inkpot", "margin_quill_giant",                                  // gaping ledgers, ink pots (Registrar); giant quill instead of the bookshelf
            "reel_frame", "reel_goblin", "reel_bomb", "reel_skull", "reel_coin", "reel_bolt", "reel_anvil", "reel_heart", "reel_bust",
        };


        public static void BuildAll()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Exit Play Mode first."); return; }
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var d in Defs.Concat(DuoDefs))
                    foreach (var a in d.anims) Slice(a.file, d.ppu, a.pivotY >= 0f ? a.pivotY : d.pivotY, d.pivotX, a.frameW);
                foreach (var v in DuoVfx) Slice(VFX + v.file, 100f, v.py, v.px, v.frameW);
                Slice(VFX + "m4_op_door_v.png", 100f, 0f, 0f, 96);                            // vertical door (closed / open) for gate_visual, origin bottom-left
                Slice(OLDP + "Mage Skeleton -projectile-loop.png", 100f, 0.5f, 0.5f, 92);      // Mage_Orb bullet
                Slice(OLDP + "Mage Skeleton -projectile-destroy.png", 100f, 0.5f, 0.5f, 92);
                var champ = DuoDefs.First(x => x.key == "Hollow_Champion");                   // recolours sliced exactly like the original
                foreach (var c in CrystalColors)
                    foreach (var f in ChampSheets) Slice(CHAMP + c + "/" + f, champ.ppu, champ.pivotY, champ.pivotX, 96);
                foreach (var f in GalleryProps)                                               // statues / pedestals / crates — image feet at the bottom edge
                    Slice(VFX + "props/" + f + ".png", 100f, 0f, 0.5f, -1);                   // -1 = the whole image is one frame
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.Refresh();
            Directory.CreateDirectory(OutRoot + "/Animations");
            Directory.CreateDirectory(OutRoot + "/Prefabs");
            AssetDatabase.Refresh();

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(Scene)) { Debug.LogError("Missing " + Scene + " (run Soul Requiem > Scenes > Map 4 first)."); return; }
            var scene = EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            int order = PlayerSortingOrder();
            var hymn = AssetDatabase.LoadAssetAtPath<GameObject>(Map3Prefabs + "Hymn_Bolt.prefab");      // hymn bullet built in Map 3
            if (hymn == null) Debug.LogWarning("Missing " + Map3Prefabs + "Hymn_Bolt.prefab (run Soul Requiem > Enemies > Map 3 once).");
            var prefabs = new Dictionary<string, GameObject>();
            // Mage Skeleton violet orb
            var orb = BuildProjectile("Mage_Orb", OLDP + "Mage Skeleton -projectile-loop.png", OLDP + "Mage Skeleton -projectile-destroy.png", order + 1, 0.09f, Color.white, false, 0.7f);
            foreach (var d in Defs) prefabs[d.key] = BuildEnemy(d, order, d.projectile == "hymn" ? hymn : d.projectile == "orb" ? orb : null);
            // Reliquary duo + brass shell (the trap fireball, bursts on contact)
            var brass = BuildProjectile("Brass_Shell", VFX + "trap_fireball.png", VFX + "trap_fireball_end.png", order + 1, 0.1f, Color.white, true, 0.8f);
            foreach (var d in DuoDefs) prefabs[d.key] = BuildEnemy(d, order, d.projectile == "brass" ? brass : null);
            foreach (var key in Reuse)
            {
                var p = AssetDatabase.LoadAssetAtPath<GameObject>(Map3Prefabs + key + ".prefab");
                if (p != null) prefabs[key] = p; else Debug.LogWarning("Missing Map 3 prefab: " + key);
            }
            // the Jackpot Engine GOBLIN slot fires Map 1 torch goblins
            var gob = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemies/Map1/Prefabs/Goblin_Torch.prefab");
            if (gob != null) prefabs["Goblin_Torch"] = gob; else Debug.LogWarning("Missing Map 1 prefab: Goblin_Torch (run Soul Requiem > Enemies > Map 1).");
            var dir = UnityEngine.Object.FindFirstObjectByType<MapEncounterDirector>();
            if (dir == null) { Debug.LogError("Scene Map 4 has no MapEncounterDirector."); return; }
            Undo.RecordObject(dir, "Map 4 enemies");
            var list = new List<MapEncounterDirector.EnemyEntry>();          // Map 4 only uses its own enemies (the scene is copied from Map 3)
            foreach (var kv in prefabs) list.Add(new MapEncounterDirector.EnemyEntry { enemyName = kv.Key, prefab = kv.Value });
            dir.enemies = list;
            EditorUtility.SetDirty(dir);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Soul Requiem] Built {Defs.Length} Map 4 enemy prefabs ({OutRoot}/Prefabs) + assigned {prefabs.Count} types to Map4_CatacombsOfNaming.");
        }

        // ------------------------------------------------------------------ sprite slicing (same as Map3EnemyBuilder)
        static void Slice(string path, float ppu, float pivotY, float pivotX = 0.5f, int frameW = 0)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) { Debug.LogWarning("Not found: " + path); return; }
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
                if (frames.Length == 0) { Debug.LogWarning("Could not slice " + a.file); continue; }
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
                col.size = d.collider / Mathf.Max(0.01f, d.scale);           // collider in world units, not scaled with the transform
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
                EnemyDefinitionTools.ApplyOrCreate(go, d.key, OutRoot);            // asset Definitions/<key> wins over the values in code
                ai.attackTime = Mathf.Max(attackLen, ai.windup + 0.1f);
                if (VfxCfg.TryGetValue(d.key, out var vcfg)) vcfg(go.AddComponent<EnemyVfx>());
                return PrefabUtility.SaveAsPrefabAsset(go, $"{OutRoot}/Prefabs/{d.key}.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        // ---------------------------------------------------------------- VFX (uses the m3_* set already in Resources/VFX)
        const string VFX = "Assets/Resources/VFX/";
        static readonly Dictionary<string, Action<EnemyVfx>> VfxCfg = new Dictionary<string, Action<EnemyVfx>>
        {
            ["Bone_Jailer"] = v => { v.swing = "m3_slash_steel"; v.windup = "m3_windup"; v.death = "m3_death_soul"; v.chestY = 0.25f; },
            ["Choir_Cutthroat"] = v => { v.swing = "m3_claw"; v.windup = "m3_windup"; v.death = "m3_death_soul"; v.chestY = 0.25f; },
            ["Pit_Bat"] = v => { v.swing = "m3_claw"; v.death = "m3_death_blood"; v.chestY = 0.3f; },
            ["Lantern_Warden"] = v => { v.windup = "m3_windup"; v.dash = "m3_dash_dust"; v.death = "m3_death_soul"; v.chestY = 0.35f; },
            ["Hollow_Cantor"] = v => { v.cast = "m3_cast_hymn"; v.death = "m3_death_soul"; v.chestY = 0.3f; },
            ["Mage_Skeleton"] = v => { v.cast = "m3_cast_hymn"; v.death = "m3_death_soul"; v.chestY = 0.3f; },
            ["The_Registrar"] = v => { v.cast = "m3_cast_hymn"; v.death = "m3_death_soul"; v.chestY = 0.45f; },
            ["Brazen_Reliquary"] = v => { v.dash = "m3_dash_dust"; v.death = "m3_death_soul"; v.chestY = 0.35f; },
            ["Hollow_Champion"] = v => { v.windup = "m3_windup"; v.dash = "m3_dash_dust"; v.death = "m3_death_soul"; v.chestY = 0.45f; },
            ["Acolyte_Fire"] = v => { v.death = "m3_death_soul"; v.chestY = 0.32f; },
            ["Acolyte_Frost"] = v => { v.death = "m3_death_soul"; v.chestY = 0.32f; },
            ["Acolyte_Storm"] = v => { v.death = "m3_death_soul"; v.chestY = 0.32f; },
            ["Acolyte_Umbral"] = v => { v.death = "m3_death_soul"; v.chestY = 0.32f; },
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
                p.explodeFrames = boom == fly ? new Sprite[0] : boomF;      // arrows: own break sprite (VFX) if available
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
