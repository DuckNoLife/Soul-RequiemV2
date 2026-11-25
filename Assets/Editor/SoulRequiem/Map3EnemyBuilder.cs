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
    /// Map 3 enemy builder. Menu: Soul Requiem ▸ Enemies ▸ Map 3 (declared in SoulRequiemMenu).
    /// The Vale of Graves (Map 3) enemies, following the story's character table:
    ///   The Risen    : Undead (Cemetery), Skeleton + Grave_Worm (Crypt), Spider (reuses the Map 1 prefab)
    ///   House Vey    : Vampire (Enemy_Animations_Set), Vampire_Archer (MEGAPACK), Bat / Giant_Bat (Cemetery),
    ///                  optional boss Matriarch_Isolde (scaled-up Vampire Archer)
    ///   The Choir    : Black Lantern = black Tiny Swords faction (BL_Lancer, BL_Warrior, BL_Archer, BL_Monk healer),
    ///                  Choir_Novice (Sorcerer LVL1), Lantern_Knight (Knight LVL2, pounce), boss Precentor_Morrow (Sorcerer LVL4)
    ///   Penitent Garden: Ent_1..4 (Ent LVL1-4), Dummy (Dummy LVL1). Mini-boss Ent_3 (Ancient Ent) GROWS THROUGH 4 FORMS —
    ///                  controllers Ent_3_F1..F4 (EntForms) assigned to AncientEntSkills.forms; Ent_1 gets extra Prepare/Dash/Recover/Grow states to slice the LVL1 sheet.
    /// Opens scene Map3_ValeOfGraves and assigns everything to the EncounterDirector. Safe to rerun any number of times.
    /// Related: Map4EnemyBuilder reuses Hymn_Bolt and other Map 3 prefabs.
    /// </summary>
    public static class Map3EnemyBuilder
    {
        const string OutRoot = "Assets/Enemies/Map3";
        const string Scene = "Assets/Scenes/Map3_ValeOfGraves.unity";
        const string Map1Prefabs = "Assets/Enemies/Map1/Prefabs/";
        static readonly string[] Reuse = { "Spider" };

        const string CEM = "Assets/GamePackage/EPIC RPG World Pack - Cemetery V 1.6/Characters/";
        const string CRYPT = "Assets/GamePackage/EPIC RPG World Pack - Crypt V.1.6/Characters/";
        const string EAS = "Assets/GamePackage/Enemy_Animations_Set/Enemy_Animations_Set/enemies-";
        const string TSB = "Assets/GamePackage/Tiny Swords (Free Pack)/Tiny Swords (Free Pack)/Units/Black Units/";
        const string MEGA = "Assets/GamePackage/CHARACTER MEGAPACK/";
        const string EYE = "Assets/GamePackage/EPIC RPG World - Sewers V1.5/Characters/Beholder/";
        const string ARROW = TSB + "Archer/Arrow.png";

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
            public string projectile;            // "arrow" | "blood" | "hymn"
            public Action<EnemyAI> tune;
            public Action<GameObject> extra;
        }

        static Anim[] Mega(string folder, int fw, string atk = null, float atkFps = 12f, bool lunge = false)
        {
            var l = new List<Anim> { new Anim("Idle", folder + Pick(folder, "Idle_"), 4, true, fw), new Anim("Move", folder + Pick(folder, "Move_"), 18, true, fw) };
            if (atk != null) l.Add(new Anim("Attack", folder + Pick(folder, atk), atkFps, false, fw));
            if (lunge)
            {
                l.Add(new Anim("Prepare", folder + Pick(folder, "Dash_Begin_"), 10, false, fw));
                l.Add(new Anim("Dash", folder + Pick(folder, "Dash_Full_"), 16, true, fw));
                l.Add(new Anim("Recover", folder + Pick(folder, "Dash_End_"), 12, false, fw));
            }
            return l.ToArray();
        }

        /// MEGAPACK file names carry a frame-count suffix (e.g. "Move_24x1.png"): look them up by prefix.
        static string Pick(string folder, string prefix)
        {
            var dirPath = folder.TrimEnd('/');
            if (!Directory.Exists(dirPath)) return prefix + ".png";
            var f = Directory.GetFiles(dirPath, prefix + "*.png").Select(Path.GetFileName).OrderBy(n => n.Length).FirstOrDefault();
            return f ?? prefix + ".png";
        }

        static Def[] Defs => new[]          // property: re-read the PixelLab sprites on every build
        {
            // ---------------------------------------------------------------- The Risen
            new Def {
                key = "Undead", scale = 1.25f, display = "Risen Dead", pivotX = 0.49f, pivotY = 0.354f, collider = new Vector2(0.22f, 0.09f), hp = 55, mass = 2f,
                kind = EnemyKind.Melee,
                anims = new[] { new Anim("Idle", CEM + "undead/Undead-idle animation.png", 8, true, 96), new Anim("Move", CEM + "undead/Undead-walk animation.png", 8, true, 96),
                                new Anim("Attack", CEM + "undead/Undead-atk animation.png", 16, false, 96), new Anim("Death", CEM + "undead/Undead-death animation.png", 12, false, 96) },
                tune = e => { e.moveSpeed = 0.95f; e.damage = 9; e.windup = 0.55f; e.cooldown = 1.2f; e.attackRange = 0.14f; e.aggroRange = 3.4f; }
            },
            new Def {
                key = "Skeleton", scale = 1.3f, display = "Ossuary Skeleton", pivotY = 0.273f, collider = new Vector2(0.22f, 0.09f), hp = 70, mass = 2f,
                kind = EnemyKind.Melee,
                anims = new[] { new Anim("Idle", CRYPT + "Skeleton/skeleton-variation1-idle.png", 8, true, 128), new Anim("Move", CRYPT + "Skeleton/skeleton-variation1-walk.png", 10, true, 128),
                                new Anim("Attack", CRYPT + "Skeleton/skeleton-variation1-attack.png", 16, false, 128), new Anim("Death", CRYPT + "Skeleton/skeleton-variation1-death.png", 12, false, 128) },
                tune = e => { e.moveSpeed = 1.1f; e.damage = 11; e.windup = 0.5f; e.cooldown = 1.2f; e.attackRange = 0.16f; e.aggroRange = 3.4f; }
            },
            new Def {
                key = "Grave_Worm", scale = 1.25f, display = "Grave Worm", pivotY = 0.234f, collider = new Vector2(0.30f, 0.12f), hp = 90, mass = 20f,
                // no crawl animation -> burrowing ambush (WormSkills): Retreat = dig down, Attack = ground shakes -> emerge -> bite
                kind = EnemyKind.Custom,
                anims = new[] { new Anim("Idle", CRYPT + "Big Worm 1/Big worm - 1-idle-8 frames.png", 8, true, 128), new Anim("Move", CRYPT + "Big Worm 1/Big worm - 1-idle-8 frames.png", 8, true, 128),
                                new Anim("Attack", CRYPT + "Big Worm 1/Big worm - 1-appear-atk-29 frames.png", 22, false, 128), new Anim("Death", CRYPT + "Big Worm 1/Big worm - 1-death-12 frames.png", 12, false, 128),
                                new Anim("Retreat", CRYPT + "Big Worm 1/Big worm - 1-retreat-32 frames.png", 20, false, 128) },
                tune = e => { e.moveSpeed = 0.45f; e.damage = 13; e.windup = 0.8f; e.cooldown = 1.4f; e.attackRange = 0.32f; e.aggroRange = 3.2f; e.knockback = 2f; },
                extra = go => go.AddComponent<WormSkills>()
            },
            // ---------------------------------------------------------------- House Vey
            new Def {
                key = "Bat", scale = 1.0f, display = "Bat", pivotX = 0.51f, pivotY = 0.385f, collider = new Vector2(0.18f, 0.08f), hp = 25, mass = 0.6f,
                kind = EnemyKind.Melee,
                anims = new[] { new Anim("Idle", CEM + "bat/Bat-Idle animation.png", 10, true, 96), new Anim("Move", CEM + "bat/Bat-walk animation.png", 12, true, 96),
                                new Anim("Attack", CEM + "bat/Bat-atk animation.png", 18, false, 96), new Anim("Death", CEM + "bat/Bat-death animation.png", 14, false, 96) },
                tune = e => { e.moveSpeed = 1.9f; e.damage = 6; e.windup = 0.3f; e.cooldown = 1.0f; e.attackRange = 0.12f; e.aggroRange = 3.8f; }
            },
            new Def {
                key = "Giant_Bat", display = "Giant Bat", pivotX = 0.51f, pivotY = 0.385f, collider = new Vector2(0.20f, 0.09f), hp = 110, mass = 3f,
                scale = 1.7f, kind = EnemyKind.Melee,
                anims = new[] { new Anim("Idle", CEM + "bat/Bat2-Idle animation.png", 10, true, 96), new Anim("Move", CEM + "bat/Bat2-walk animation.png", 12, true, 96),
                                new Anim("Attack", CEM + "bat/Bat2-atk animation.png", 18, false, 96), new Anim("Death", CEM + "bat/Bat2-death animation.png", 14, false, 96) },
                tune = e => { e.moveSpeed = 1.5f; e.damage = 12; e.windup = 0.45f; e.cooldown = 1.1f; e.attackRange = 0.16f; e.aggroRange = 4.2f; e.knockback = 1.5f; }
            },
            new Def {
                key = "Vampire", display = "Vey Kin", scale = 0.7f, collider = new Vector2(0.20f, 0.08f), hp = 80, mass = 2f,
                kind = EnemyKind.Melee, anims = PL("Vampire", ("Idle", 6, true), ("Move", 11, true), ("Attack", 13, false), ("Death", 10, false)),
                tune = e => { e.moveSpeed = 1.6f; e.damage = 12; e.windup = 0.4f; e.cooldown = 1.0f; e.attackRange = 0.14f; e.aggroRange = 3.6f; }
            },
            new Def {
                key = "Vampire_Archer", display = "Vey Huntress", scale = 0.7f, collider = new Vector2(0.20f, 0.08f), hp = 60, mass = 1.5f,
                kind = EnemyKind.Ranged, projectile = "blood", anims = PL("Vampire_Archer", ("Idle", 6, true), ("Move", 11, true), ("Attack", 16, false), ("Death", 11, false)),
                tune = e => { e.moveSpeed = 1.2f; e.damage = 10; e.windup = 0.5f; e.cooldown = 1.4f; e.aggroRange = 4.2f; e.keepDistance = 1.8f;
                              e.projectileSpeed = 3.2f; e.projectileLifetime = 2f; }
            },
            new Def {
                key = "Matriarch_Isolde", display = "Isolde, the Vey Matriarch", scale = 0.8f, collider = new Vector2(0.30f, 0.11f), hp = 650, mass = 20f,
                immuneCC = true, kind = EnemyKind.Custom, projectile = "blood",
                // boss: IsoldeSkills (elliptical arena, melee Blood Lash, Bat Dive, Crimson Rain -> blood pools, Feast,
                // Night Flight hiding in 1 of 3 coffins isolde_coffin_*, phase 3 Sanguine Tide). Cast/Drink/Stagger from PixelLab.
                anims = PL("Matriarch_Isolde", ("Idle", 6, true), ("Move", 10, true), ("Attack", 14, false), ("Cast", 12, false),
                           ("Drink", 8, false), ("Stagger", 8, false), ("Death", 11, false)),
                tune = e => { e.treatAsBoss = true; e.leashRange = 0f; e.moveSpeed = 1.25f; e.damage = 13; e.windup = 0.4f; e.cooldown = 0.5f;
                              e.aggroRange = 6f; e.attackRange = 0.3f; e.projectileSpeed = 3.6f; e.projectileLifetime = 2.4f; e.phase2At = 0.6f; },
                extra = go => { var k = go.AddComponent<IsoldeSkills>(); k.arenaRadii = new Vector2(3.0f, 2.2f); k.arenaOffset = new Vector2(0f, 0.1f);
                                k.meleeRange = 0.5f; k.preferredRange = 1.6f; k.batFrames = Sprites(CEM + "bat/Bat-walk animation.png"); }
            },
            // ---------------------------------------------------------------- The Choir
            new Def {
                key = "BL_Lancer", display = "Black Lantern Lancer", scale = 0.7f, collider = new Vector2(0.22f, 0.09f), hp = 90, mass = 4f,
                kind = EnemyKind.Melee, anims = PL("BL_Lancer", ("Idle", 6, true), ("Move", 11, true), ("Attack", 13, false), ("Death", 11, false)),
                tune = e => { e.moveSpeed = 1.0f; e.damage = 12; e.windup = 0.5f; e.cooldown = 1.3f; e.attackRange = 0.30f; e.aggroRange = 3.6f; e.knockback = 1.5f; }
            },
            new Def {
                key = "BL_Warrior", display = "Black Lantern Warrior", scale = 0.7f, collider = new Vector2(0.22f, 0.09f), hp = 80, mass = 3f,
                kind = EnemyKind.Melee, anims = PL("BL_Warrior", ("Idle", 6, true), ("Move", 11, true), ("Attack", 15, false), ("Death", 11, false)),
                tune = e => { e.moveSpeed = 1.2f; e.damage = 12; e.windup = 0.4f; e.cooldown = 1.1f; e.attackRange = 0.18f; e.aggroRange = 3.6f; }
            },
            new Def {
                key = "BL_Archer", display = "Black Lantern Archer", scale = 0.7f, collider = new Vector2(0.20f, 0.08f), hp = 45, mass = 1.5f,
                kind = EnemyKind.Ranged, projectile = "arrow", anims = PL("BL_Archer", ("Idle", 6, true), ("Move", 10, true), ("Attack", 18, false), ("Death", 11, false)),
                tune = e => { e.moveSpeed = 1.1f; e.damage = 9; e.windup = 0.6f; e.cooldown = 1.6f; e.aggroRange = 4.4f; e.keepDistance = 2.0f;
                              e.projectileSpeed = 3.4f; e.projectileLifetime = 2f; }
            },
            new Def {
                key = "BL_Monk", display = "Black Lantern Monk", scale = 0.7f, collider = new Vector2(0.20f, 0.08f), hp = 50, mass = 1.5f,
                kind = EnemyKind.Custom, anims = PL("BL_Monk", ("Idle", 6, true), ("Move", 10, true), ("Heal", 10, false), ("Death", 11, false)),
                tune = e => { e.moveSpeed = 1.0f; e.damage = 0; e.aggroRange = 4.5f; },
                extra = go => go.AddComponent<HealerSkills>()
            },
            new Def {
                key = "Choir_Novice", display = "Choir Novice", scale = 0.7f, collider = new Vector2(0.18f, 0.08f), hp = 45, mass = 1.5f,
                kind = EnemyKind.Ranged, projectile = "hymn", anims = PL("Choir_Novice", ("Idle", 6, true), ("Move", 10, true), ("Attack", 10, false), ("Death", 11, false)),
                tune = e => { e.moveSpeed = 1.0f; e.damage = 8; e.windup = 0.4f; e.cooldown = 1.8f; e.aggroRange = 4f; e.keepDistance = 2.2f;
                              e.projectileSpeed = 2.2f; e.projectileLifetime = 2.6f; }
            },
            new Def {
                key = "Lantern_Knight", display = "Lantern Knight", scale = 0.77f, collider = new Vector2(0.28f, 0.11f), hp = 520, mass = 14f,
                immuneCC = true, kind = EnemyKind.Custom,
                // PixelLab 9a1c182f "Commander": commander armour, black-lantern crest cloak, greatsword. One animation per move.
                // Frame timing matches the code: Slam hits the ground on frame 7 (6/8 s), ChargePrep 7 frames = 0.7 s charge wind-up.
                // State "Commander Two-Hand" 49d0004b: both hands on the hilt, no lantern pose. No Attack state (no normal slash).
                // Vow_N (PixelLab) was drawn wrong (hand + hilt through the back) -> the N direction reuses Vow_S.
                anims = Borrow(PL("Lantern_Knight", ("Idle", 8, true), ("Move", 10, true), ("Dash", 12, true),
                           ("ChargePrep", 10, false), ("Slam", 8, false), ("Vow", 8, false), ("Stagger", 8, false), ("Death", 10, false)),
                           "Vow_N", "Vow_S"),
                tune = e => { e.treatAsBoss = true; e.phase2At = 0.5f; e.leashRange = 0f; e.moveSpeed = 0.85f; e.damage = 16; e.aggroRange = 4.2f;
                              e.attackRange = 0.35f; e.cooldown = 0.5f; e.knockback = 2.5f; },
                extra = go => { var k = go.AddComponent<LanternKnightSkills>(); k.slamWindup = 6f / 8f; }   // Slam: the sword hits the ground on frame 7 (6/8 s)
            },
            new Def {
                key = "Precentor_Morrow", display = "Precentor Morrow", scale = 0.8f, collider = new Vector2(0.30f, 0.11f), hp = 1000, mass = 40f,
                immuneCC = true, kind = EnemyKind.Custom, projectile = "hymn",
                // boss: MorrowSkills (blood-draining altar pillars -> breaking a lantern = Break, melee Bell Sweep, Toll of Hymns, Choir Lances,
                // Raise the Named at the grave clusters, Vespers Step corner escape, phase 3 Requiem Chorus behind pillars). Cast/Toll/Stagger from PixelLab.
                anims = PL("Precentor_Morrow", ("Idle", 6, true), ("Move", 9, true), ("Attack", 12, false), ("Cast", 12, false),
                           ("Toll", 10, false), ("Stagger", 8, false), ("Death", 11, false)),
                tune = e => { e.treatAsBoss = true; e.leashRange = 0f; e.moveSpeed = 0.85f; e.damage = 14; e.windup = 0.55f; e.cooldown = 0.5f;
                              e.aggroRange = 7f; e.attackRange = 0.35f; e.projectileSpeed = 2.4f; e.projectileLifetime = 3.6f; e.phase2At = 0.65f; },
                extra = go => { var k = go.AddComponent<MorrowSkills>(); k.arenaRadii = new Vector2(3.2f, 2.1f); k.arenaOffset = new Vector2(0f, -0.19f);
                                k.meleeRange = 0.55f; k.preferredRange = 1.9f; }
            },
            // ---------------------------------------------------------------- Penitent Garden
            new Def {
                key = "Ent_1", display = "Penitent Ent", pivotY = 0.058f, collider = new Vector2(0.30f, 0.12f), hp = 120, mass = 8f,
                scale = 1.4f, kind = EnemyKind.Melee,
                anims = Mega(MEGA + "Ent_LVL1/", 49, "Fire_", 12f).Concat(new[] {                 // extra sheets for form 1 of the Ancient Ent
                    new Anim("Prepare", MEGA + "Ent_LVL1/" + Pick(MEGA + "Ent_LVL1/", "Dash_Full2_"), 12, false, 49),
                    new Anim("Dash", MEGA + "Ent_LVL1/" + Pick(MEGA + "Ent_LVL1/", "Dash_Full_"), 14, true, 49),
                    new Anim("Recover", MEGA + "Ent_LVL1/" + Pick(MEGA + "Ent_LVL1/", "Special_End_"), 12, false, 49),
                    new Anim("Grow", MEGA + "Ent_LVL1/" + Pick(MEGA + "Ent_LVL1/", "Special_Full_"), 40, false, 49) }).ToArray(),
                tune = e => { e.moveSpeed = 0.8f; e.damage = 14; e.windup = 0.6f; e.cooldown = 1.3f; e.attackRange = 0.2f; e.aggroRange = 2.2f; e.knockback = 2f; }
            },
            new Def {
                key = "Ent_2", display = "Elder Ent", pivotX = 0.45f, pivotY = 0.022f, collider = new Vector2(0.32f, 0.12f), hp = 150, mass = 10f,
                scale = 1.4f, kind = EnemyKind.Custom, anims = Mega(MEGA + "Ent_LVL2/", 69, null, 12f, true),
                tune = e => { e.moveSpeed = 0.8f; e.damage = 15; e.aggroRange = 2.4f; },
                extra = go => { var k = go.AddComponent<LungeSkills>(); k.meleeState = ""; k.lungeRange = 1.5f; k.lungeSpeed = 2.8f; k.prepTime = 0.6f; }
            },
            new Def {
                key = "Ent_3", display = "Ancient Ent", pivotX = 0.45f, pivotY = 0.018f, collider = new Vector2(0.44f, 0.16f), hp = 420, mass = 30f,
                immuneCC = true, scale = 1.6f, kind = EnemyKind.Custom, anims = Mega(MEGA + "Ent_LVL3/", 86, null, 12f, true),
                tune = e => { e.treatAsBoss = true; e.phase2At = 0.5f; e.leashRange = 0f; e.moveSpeed = 0.6f; e.damage = 16; e.aggroRange = 3.4f; e.knockback = 2.5f; },
                extra = go => { var k = go.AddComponent<AncientEntSkills>(); k.forms = EntForms(); }   // 4 forms
            },
            // Ent LVL4 (giant tree head) — form 4 of the Ancient Ent; also usable on its own as an elite
            new Def {
                key = "Ent_4", display = "Grove Colossus", pivotX = 0.51f, pivotY = 0.01f, collider = new Vector2(0.5f, 0.18f), hp = 260, mass = 30f,
                immuneCC = true, scale = 1.75f, kind = EnemyKind.Custom, anims = Mega(MEGA + "Ent_LVL4/", 59, null, 12f, true),
                tune = e => { e.moveSpeed = 0.55f; e.damage = 18; e.aggroRange = 3f; e.knockback = 2.5f; },
                extra = go => { var k = go.AddComponent<LungeSkills>(); k.meleeState = ""; k.lungeRange = 1.6f; k.lungeSpeed = 2.4f; k.prepTime = 0.8f; }
            },
            new Def {
                key = "Dummy", display = "Penitent Effigy", pivotX = 0.40f, pivotY = 0.02f, collider = new Vector2(0.18f, 0.08f), hp = 60, mass = 2f,
                scale = 1.4f, kind = EnemyKind.Custom, anims = Mega(MEGA + "Dummy_LVL1/", 34, null, 12f, true),
                tune = e => { e.moveSpeed = 1.1f; e.damage = 9; e.aggroRange = 3f; },
                extra = go => { var k = go.AddComponent<LungeSkills>(); k.meleeState = ""; k.lungeRange = 1.4f; k.lungeSpeed = 3.4f; }
            },
        };


        // ------------------------------------------------------------------ PixelLab sprites (Catalog/v2/tools/pixellab_import.py)
        // Sprites/<Key>/<Key>_<State>.png = horizontal strip facing east, even cells; <Key>_meta.txt = "cellW cellH pivotY" (feet, facing east).
        const string PLRoot = OutRoot + "/Sprites/";
        /// <summary>4 AnimatorControllers for the 4 Ancient Ent forms (same state names: Idle / Move / Prepare / Dash / Recover; F1 adds Grow, Attack).
        /// Uses the sprites sliced by Def Ent_1 / Ent_2 / Ent_3 / Ent_4 (pivot per Def).</summary>
        static RuntimeAnimatorController[] EntForms()
        {
            string folder = $"{OutRoot}/Animations/Ent_3";
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            string L1 = MEGA + "Ent_LVL1/", L2 = MEGA + "Ent_LVL2/", L3 = MEGA + "Ent_LVL3/", L4 = MEGA + "Ent_LVL4/";
            var spec = new[]
            {
                new[] { ("Idle", L1, "Idle_", 10f, true), ("Move", L1, "Move_", 18f, true), ("Prepare", L1, "Dash_Full2_", 12f, false),
                        ("Dash", L1, "Dash_Full_", 14f, true), ("Recover", L1, "Special_End_", 12f, false), ("Grow", L1, "Special_Full_", 40f, false),
                        ("Attack", L1, "Fire_", 12f, false) },
                new[] { ("Idle", L2, "Idle_", 4f, true), ("Move", L2, "Move_", 18f, true), ("Prepare", L2, "Dash_Begin_", 10f, false),
                        ("Dash", L2, "Dash_Full_", 16f, true), ("Recover", L2, "Dash_End_", 12f, false) },
                new[] { ("Idle", L3, "Idle_", 4f, true), ("Move", L3, "Move_", 18f, true), ("Prepare", L3, "Dash_Begin_", 10f, false),
                        ("Dash", L3, "Dash_Full_", 16f, true), ("Recover", L3, "Dash_End_", 12f, false) },
                new[] { ("Idle", L4, "Idle_", 4f, true), ("Move", L4, "Move_", 20f, true), ("Prepare", L4, "Dash_Begin_", 24f, false),
                        ("Dash", L4, "Dash_Full_", 36f, true), ("Recover", L4, "Dash_End_", 14f, false) },
            };
            var res = new RuntimeAnimatorController[4];
            for (int f = 0; f < 4; f++)
            {
                var states = new List<(string, AnimationClip)>();
                foreach (var (st, dir, pre, fps, loop) in spec[f])
                {
                    var frames = Sprites(dir + Pick(dir, pre));
                    if (frames.Length == 0) { Debug.LogWarning($"[Ent forms] Could not slice {dir}{pre}*"); continue; }
                    states.Add((st, Clip(folder, $"Ent_3_F{f + 1}_{st}", frames, fps, loop)));
                }
                res[f] = Controller(folder, $"Ent_3_F{f + 1}", states);
            }
            return res;
        }

        static Anim[] PL(string key, params (string state, float fps, bool loop)[] st)
        {
            string dir = PLRoot + key + "/";
            int w = 0; float py = 0.06f, pyS = 0.06f, pyN = 0.06f;
            var metaPath = dir + key + "_meta.txt";
            if (File.Exists(metaPath))
            {
                var t = File.ReadAllText(metaPath).Trim().Split(' ');
                var ci = System.Globalization.CultureInfo.InvariantCulture;
                w = int.Parse(t[0]); py = float.Parse(t[2], ci);
                pyS = t.Length > 3 ? float.Parse(t[3], ci) : py;
                pyN = t.Length > 4 ? float.Parse(t[4], ci) : py;
            }
            var list = new List<Anim>();
            foreach (var x in st)
                foreach (var (suf, pv) in new[] { ("", py), ("_S", pyS), ("_N", pyN) })
                {
                    string f = dir + key + "_" + x.state + suf + ".png";      // _S faces down, _N faces up (EnemyAI picks automatically)
                    if (File.Exists(f)) list.Add(new Anim(x.state + suf, f, x.fps, x.loop, w, pv));
                }
            return list.ToArray();
        }

        /// <summary>
        /// Use the frames of state <paramref name="source"/> for state <paramref name="target"/> (e.g. a badly drawn N direction borrows S).
        /// Replaces any existing target clip.
        /// </summary>
        static Anim[] Borrow(Anim[] list, string target, string source)
        {
            var src = list.FirstOrDefault(a => a.state == source);
            if (src == null) { Debug.LogWarning($"[Soul Requiem] Borrow: no state {source}."); return list; }
            var l = list.Where(a => a.state != target).ToList();
            l.Add(new Anim(target, src.file, src.fps, src.loop, src.frameW, src.pivotY));
            return l.ToArray();
        }


        public static void BuildAll()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Exit Play Mode first."); return; }
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var d in Defs)
                    foreach (var a in d.anims) Slice(a.file, d.ppu, a.pivotY >= 0f ? a.pivotY : d.pivotY, d.pivotX, a.frameW);
                Slice(ARROW, 200, 0.5f, 0.5f, 64);
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.Refresh();
            Directory.CreateDirectory(OutRoot + "/Animations");
            Directory.CreateDirectory(OutRoot + "/Prefabs");
            AssetDatabase.Refresh();

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(Scene)) { Debug.LogError("Missing " + Scene + " (run Soul Requiem > Scenes > Map 3 first)."); return; }
            var scene = EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            int order = PlayerSortingOrder();
            var arrow = BuildProjectile("BL_Arrow", ARROW, VFX + "m3_arrow_hit.png", order + 1, 0.07f, Color.white, true);
            var blood = BuildProjectile("Blood_Arrow", ARROW, VFX + "m3_blood_splash.png", order + 1, 0.07f, new Color(1f, 0.55f, 0.6f), true);
            var hymn = BuildProjectile("Hymn_Bolt", VFX + "m3_hymn_orb.png", VFX + "m3_hymn_pop.png", order + 1, 0.09f, Color.white, false, 0.8f);
            var prefabs = new Dictionary<string, GameObject>();
            foreach (var d in Defs)
                prefabs[d.key] = BuildEnemy(d, order, d.projectile == "arrow" ? arrow : d.projectile == "blood" ? blood : d.projectile == "hymn" ? hymn : null);
            foreach (var key in Reuse)
            {
                var p = AssetDatabase.LoadAssetAtPath<GameObject>(Map1Prefabs + key + ".prefab");
                if (p != null) prefabs[key] = p; else Debug.LogWarning("Missing Map 1 prefab: " + key);
            }

            var dir = UnityEngine.Object.FindFirstObjectByType<MapEncounterDirector>();
            if (dir == null) { Debug.LogError("Scene Map 3 has no MapEncounterDirector."); return; }
            Undo.RecordObject(dir, "Map 3 enemies");
            var list = new List<MapEncounterDirector.EnemyEntry>();          // Map 3 only uses its own enemies (the scene is copied from Map 2)
            foreach (var kv in prefabs) list.Add(new MapEncounterDirector.EnemyEntry { enemyName = kv.Key, prefab = kv.Value });
            dir.enemies = list;
            EditorUtility.SetDirty(dir);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Soul Requiem] Built {Defs.Length} Map 3 enemy prefabs ({OutRoot}/Prefabs) + assigned {prefabs.Count} types to Map3_ValeOfGraves.");
        }

        // ------------------------------------------------------------------ sprite slicing (same as Map2EnemyBuilder)
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

        // ---------------------------------------------------------------- VFX (PixelLab VFX Workbench, Assets/Resources/VFX/m3_*)
        const string VFX = "Assets/Resources/VFX/";
        static readonly Dictionary<string, Action<EnemyVfx>> VfxCfg = new Dictionary<string, Action<EnemyVfx>>
        {
            // the Choir / Black Lantern: blue-silver steel, golden lantern light, ice-blue hymns; death = souls rising (Wall of Voices)
            ["BL_Warrior"] = v => { v.swing = "m3_slash_steel"; v.windup = "m3_windup"; v.death = "m3_death_soul"; },
            ["BL_Lancer"] = v => { v.swing = "m3_thrust"; v.windup = "m3_windup"; v.death = "m3_death_soul"; },
            ["BL_Archer"] = v => { v.death = "m3_death_soul"; },
            ["BL_Monk"] = v => { v.cast = "m3_cast_gold"; v.heal = "m3_heal_lantern"; v.death = "m3_death_soul"; },
            ["Choir_Novice"] = v => { v.cast = "m3_cast_hymn"; v.death = "m3_death_soul"; },
            ["Lantern_Knight"] = v => { v.windup = "m3_windup"; v.dash = "m3_dash_dust"; v.death = "m3_death_soul"; v.scale = 0.77f; v.chestY = 0.40f; },
            ["Precentor_Morrow"] = v => { v.swing = "m3_slash_steel"; v.windup = "m3_windup"; v.cast = "m3_cast_hymn"; v.death = "m3_death_soul"; v.chestY = 0.42f; },
            // House Vey: dark crimson blood
            ["Vampire"] = v => { v.swing = "m3_claw"; v.windup = "m3_windup"; v.death = "m3_death_blood"; },
            ["Vampire_Archer"] = v => { v.death = "m3_death_blood"; },
            ["Matriarch_Isolde"] = v => { v.swing = "m3_claw"; v.windup = "m3_windup"; v.death = "m3_death_blood"; v.chestY = 0.42f; },
            ["Bat"] = v => { v.death = "m3_death_blood"; v.chestY = 0.2f; },
            ["Giant_Bat"] = v => { v.swing = "m3_claw"; v.death = "m3_death_blood"; v.chestY = 0.3f; },
            // the Risen: rise out of the grave soil
            ["Undead"] = v => { v.windup = "m3_windup"; v.spawn = "m3_ground_burst"; v.death = "m3_death_soul"; v.chestY = 0.3f; },
            ["Skeleton"] = v => { v.swing = "m3_slash_steel"; v.windup = "m3_windup"; v.spawn = "m3_ground_burst"; v.death = "m3_death_soul"; v.chestY = 0.3f; },
            ["Grave_Worm"] = v => { v.windup = "m3_ground_burst"; v.windupAtFeet = true; v.spawn = "m3_ground_burst"; },
            // Penitent Garden: dust when charging
            ["Ent_1"] = v => { v.windup = "m3_windup"; v.chestY = 0.35f; },
            ["Ent_2"] = v => { v.dash = "m3_dash_dust"; v.windup = "m3_windup"; v.chestY = 0.4f; },
            ["Ent_3"] = v => { v.dash = "m3_dash_dust"; v.windup = "m3_windup"; v.chestY = 0.4f; },
            ["Ent_4"] = v => { v.dash = "m3_dash_dust"; v.windup = "m3_windup"; v.chestY = 0.5f; },
            ["Dummy"] = v => { v.dash = "m3_dash_dust"; v.chestY = 0.3f; },
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
