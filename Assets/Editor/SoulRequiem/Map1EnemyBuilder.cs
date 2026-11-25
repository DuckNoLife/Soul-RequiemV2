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
using SuperTiled2Unity;
using SoulRequiem.Core;
using SoulRequiem.Gameplay;

namespace SoulRequiem.EditorTools
{
    /// <summary>
    /// Map 1 enemy builder. Menu: Soul Requiem ▸ Enemies ▸ Map 1 (declared in SoulRequiemMenu; also part of Build Map / Build Everything).
    /// 1. Re-slice the Tiny Swords Enemy Pack sheets on an even grid, character feet as pivot.
    /// 2. Create Animation Clips + Animator Controller (Idle / Move / Attack / Windup / Recovery / Death).
    /// 3. Create enemy prefabs (Rigidbody2D + Capsule at the feet + Health + EnemyAI), the Mage bullet prefab, the Cave prefab.
    /// 4. Attach to the open scene: EncounterDirector + HUD, Health + combat test kit for the Player.
    /// Safe to rerun any number of times (overwrites).
    /// Related: EnemyDefinitionTools.ApplyOrCreate (asset stats win), Map2/3/4EnemyBuilder (reuse Map 1 prefabs).
    /// </summary>
    public static class Map1EnemyBuilder
    {
        const string Pack = "Assets/GamePackage/Tiny Swords (Enemy Pack)/Enemy Pack/";
        const string OutRoot = "Assets/Enemies/Map1";
        const float CavePPU = 100f;      // cave ~1.5 x 1.2 units (~5 x 4 map tiles)

        class Anim
        {
            public string state, file;
            public float fps;
            public bool loop;
            public int frameW;           // 0 = square frame (width = height)
            public Anim(string state, string file, float fps, bool loop, int frameW = 0) { this.state = state; this.file = file; this.fps = fps; this.loop = loop; this.frameW = frameW; }
        }

        /// Extra effect sheets (not Animator states) that must be sliced before tune() fetches sprites.
        class Fx
        {
            public string file; public float ppu, pivotX, pivotY; public int frameW;
            public Fx(string file, float ppu, float pivotX, float pivotY, int frameW = 0) { this.file = file; this.ppu = ppu; this.pivotX = pivotX; this.pivotY = pivotY; this.frameW = frameW; }
        }

        /// Paths: "Assets/..." is used as is, anything else is relative to Tiny Swords Enemy Pack.
        static string P(string f) => f.StartsWith("Assets/") ? f : Pack + f;

        const string ERW = "Assets/GamePackage/ERW - Grass Land 2.0 v2.1/Characters/";
        const string SeerBolt = ERW + "orc mage/atk1 fx-energy ish fx-single.png";   // 12 frames 192x160: mud bolt falling, breaking into a dome
        const string SeerSpikes = ERW + "orc mage/atk1 fx-Spike fx-pack.png";        // 13 frames 192x160: rock spike cluster
        const string CannonDir = "Extra/Cannon/";
        const string HexBurst = "Extra/Hex Shaman Transformation Spell/Hex Shaman_Explosion Spell.png";   // 10 frames 192: pink ring -> explosion

        class Def
        {
            public string key, display;
            public float ppu, pivotY, pivotX = 0.5f;
            public Color tint = Color.white;
            public Fx[] fx;
            public Anim[] anims;
            public Vector2 collider;     // capsule size at the feet (Unity units)
            public float mass = 1f;
            public float hp;
            public bool immuneCC;
            public EnemyKind kind;
            public Action<EnemyAI> tune;
        }

        static readonly Def[] Defs =
        {
            new Def {
                key = "Lizard", display = "Spike Lizard", ppu = 200, pivotY = 0.24f, collider = new Vector2(0.30f, 0.12f), hp = 45,
                kind = EnemyKind.SpikeBurst,
                anims = new[] { new Anim("Idle", "Lizard/Lizard_Idle.png", 8, true), new Anim("Move", "Lizard/Lizard_Run.png", 10, true),
                                new Anim("Attack", "Lizard/Lizard_Attack.png", 12, false) },
                tune = e => { e.moveSpeed = 1.3f; e.damage = 8; e.windup = 0.45f; e.cooldown = 1.8f; e.knockback = 3.5f;
                              e.spikeCount = 10; e.spikeTriggerRange = 1.0f; e.projectileSpeed = 2.2f; e.projectileLifetime = 1.2f; }
            },
            new Def {
                key = "Spider", display = "Spider", ppu = 200, pivotY = 0.32f, collider = new Vector2(0.30f, 0.12f), hp = 25,
                kind = EnemyKind.Melee,
                anims = new[] { new Anim("Idle", "Spider/Spider_Idle.png", 8, true), new Anim("Move", "Spider/Spider_Run.png", 10, true),
                                new Anim("Attack", "Spider/Spider_Attack.png", 12, false) },
                tune = e => { e.moveSpeed = 1.9f; e.damage = 5; e.windup = 0.33f; e.cooldown = 1.0f; e.attackRange = 0.12f;
                              e.poisonPercent = 0.02f; e.poisonDuration = 5f; }
            },
            new Def {
                key = "Snake", display = "Snake", ppu = 200, pivotY = 0.39f, collider = new Vector2(0.30f, 0.10f), hp = 25,
                kind = EnemyKind.Melee,
                anims = new[] { new Anim("Idle", "Snake/Snake_Idle.png", 8, true), new Anim("Move", "Snake/Snake_Run.png", 10, true),
                                new Anim("Attack", "Snake/Snake_Attack.png", 12, false) },
                tune = e => { e.moveSpeed = 1.7f; e.damage = 5; e.windup = 0.3f; e.cooldown = 1.0f; e.attackRange = 0.12f;
                              e.poisonPercent = 0.02f; e.poisonDuration = 5f; }
            },
            new Def {
                key = "Spear_Goblin", display = "Goblin Warrior", ppu = 200, pivotY = 0.336f, collider = new Vector2(0.26f, 0.12f), hp = 55, mass = 2,
                kind = EnemyKind.Melee,
                anims = new[] { new Anim("Idle", "Spear Goblin/Spear Goblin_Idle.png", 8, true), new Anim("Move", "Spear Goblin/Spear Goblin_Run.png", 10, true),
                                new Anim("Attack", "Spear Goblin/Spear Goblin_Attack Fast.png", 12, false) },
                tune = e => { e.moveSpeed = 1.6f; e.damage = 12; e.windup = 0.33f; e.cooldown = 1.0f; e.attackRange = 0.2f; e.knockback = 1.5f; }
            },
            new Def {
                key = "Hex_Shaman", display = "Goblin Shaman", ppu = 200, pivotY = 0.32f, collider = new Vector2(0.26f, 0.12f), hp = 35,
                kind = EnemyKind.Ranged,
                anims = new[] { new Anim("Idle", "Hex Shaman/Hex Shaman_Idle.png", 8, true), new Anim("Move", "Hex Shaman/Hex Shaman_Run.png", 8, true),
                                new Anim("Attack", "Hex Shaman/Hex Shaman_Attack.png", 12, false) },
                tune = e => { e.moveSpeed = 1.2f; e.damage = 10; e.windup = 0.5f; e.cooldown = 1.6f; e.aggroRange = 3.8f; e.keepDistance = 1.8f;
                              e.projectileSpeed = 2.4f; e.projectileLifetime = 2.5f; }
            },
            new Def {
                key = "Minotaur", display = "Bull Warrior", ppu = 150, pivotY = 0.356f, collider = new Vector2(0.42f, 0.16f), hp = 150, mass = 6,
                kind = EnemyKind.Melee,
                anims = new[] { new Anim("Idle", "Minotaur/Minotaur_Idle.png", 10, true), new Anim("Move", "Minotaur/Minotaur_Walk.png", 8, true),
                                new Anim("Attack", "Minotaur/Minotaur_Attack.png", 10, false) },
                tune = e => { e.moveSpeed = 0.9f; e.damage = 18; e.windup = 0.7f; e.cooldown = 1.8f; e.attackRange = 0.3f; e.aggroRange = 3.5f;
                              e.stunSeconds = 1.5f; e.knockback = 2f; }
            },
            new Def {
                key = "Grok_Troll", display = "Grok, the Greystone Chieftain", ppu = 160, pivotY = 0.25f, collider = new Vector2(0.62f, 0.22f), hp = 600, mass = 30,
                immuneCC = true, kind = EnemyKind.Boss,
                anims = new[] { new Anim("Idle", "Troll/Troll_Idle.png", 10, true), new Anim("Move", "Troll/Troll_Walk.png", 10, true),
                                new Anim("Windup", "Troll/Troll_Windup.png", 8, true), new Anim("Attack", "Troll/Troll_Attack.png", 10, false),
                                new Anim("Recovery", "Troll/Troll_Recovery.png", 10, true), new Anim("Death", "Extra/Troll Dead/Troll_Dead.png", 10, false) },
                tune = e => { e.moveSpeed = 1.0f; e.damage = 25; e.windup = 0.9f; e.cooldown = 0.8f; e.attackRange = 1.0f; e.aggroRange = 4.5f;
                              e.chargeSpeed = 4.5f; e.chargeTime = 0.45f; e.recoveryTime = 1.6f; e.slamRadius = 0.35f; e.knockback = 3f;
                              e.phase2At = 0.5f; }
            },

            // ---------------- Map 1 enemies from the "Level Design" document (Tiny Swords goblins, Bear, Cannon) ----------------
            // ---------------- Greystone goblin tribe (Tiny Swords Enemy Pack, PPU 200 like the other TS enemies) ----------------
            // Spearman: fast stabs; every 3rd attack flashes orange then spins the spear (Attack Strong) — dash away
            new Def {
                key = "Goblin_Spearman", display = "Greystone Spearman", ppu = 200, pivotY = 0.336f,   // same pivot as Def Spear_Goblin (shared sheet)
                collider = new Vector2(0.26f, 0.12f), hp = 60, mass = 2, kind = EnemyKind.Melee,
                anims = new[] { new Anim("Idle", "Spear Goblin/Spear Goblin_Idle.png", 8, true), new Anim("Move", "Spear Goblin/Spear Goblin_Run.png", 10, true),
                                new Anim("Attack", "Spear Goblin/Spear Goblin_Attack Fast.png", 12, false),
                                new Anim("Spin", "Extra/Spear Goblin Strong Attack/Spear Goblin_Attack Strong.png", 12, true) },
                tune = e => { e.moveSpeed = 1.6f; e.damage = 11; e.windup = 0.25f; e.cooldown = 0.9f; e.attackRange = 0.22f; e.aggroRange = 3.4f;
                              e.knockback = 1.5f; e.special = EnemySpecial.Spin; e.specialEvery = 3; e.specialWindup = 0.6f;
                              e.spinTime = 0.67f; e.spinEndTime = 0.1f; e.spinRadius = 0.55f; e.spinTick = 0.22f; e.spinMoveFactor = 0.6f; }
            },
            // Torchbearer: runs fast, swings a torch in melee, low HP
            new Def {
                key = "Goblin_Torch", display = "Greystone Torchbearer", ppu = 200, pivotX = 0.44f, pivotY = 0.307f,
                collider = new Vector2(0.24f, 0.11f), hp = 40, mass = 1.5f, kind = EnemyKind.Melee,
                anims = new[] { new Anim("Idle", "Torch Goblin/Torch Goblin_Idle.png", 10, true), new Anim("Move", "Torch Goblin/Torch Goblin_Run.png", 12, true),
                                new Anim("Attack", "Torch Goblin/Torch Goblin_Attack.png", 14, false) },
                tune = e => { e.moveSpeed = 1.9f; e.damage = 9; e.windup = 0.2f; e.cooldown = 0.8f; e.attackRange = 0.16f; e.aggroRange = 3.6f;
                              e.knockback = 1f; }
            },
            // Hexer: shoots pink orbs from range; every 3rd cast transforms -> pink explosion where the player stands (warned by a ring)
            new Def {
                key = "Goblin_Hexer", display = "Greystone Hexer", ppu = 200, pivotY = 0.32f,       // same pivot as Def Hex_Shaman (shared sheet)
                collider = new Vector2(0.26f, 0.12f), hp = 40, mass = 1.5f, kind = EnemyKind.Ranged,
                anims = new[] { new Anim("Idle", "Hex Shaman/Hex Shaman_Idle.png", 8, true), new Anim("Move", "Hex Shaman/Hex Shaman_Run.png", 8, true),
                                new Anim("Attack", "Hex Shaman/Hex Shaman_Attack.png", 14, false),
                                new Anim("Cast", "Extra/Hex Shaman Transformation Spell/Hex Shaman_Transformation Spell.png", 14, false) },
                fx = new[] { new Fx(HexBurst, 200, 0.5f, 0.22f) },
                tune = e => { e.moveSpeed = 1.2f; e.damage = 10; e.windup = 0.45f; e.cooldown = 1.6f; e.aggroRange = 3.8f; e.keepDistance = 1.8f;
                              e.projectileSpeed = 2.4f; e.projectileLifetime = 2.5f;
                              e.special = EnemySpecial.GroundSpikes; e.specialEvery = 3; e.specialFrames = Sprites(HexBurst);
                              e.specialImpactFrame = 2; e.specialRadius = 0.45f; e.strikeFps = 14f;
                              e.strikeColor = new Color(1f, 0.55f, 0.9f, 1f); }
            },
            // Boar Rider: rides a boar, winds up then charges on sight (every 3.5 s), stabs when close
            new Def {
                key = "Goblin_PigRider", display = "Greystone Boar Rider", ppu = 200, pivotX = 0.49f, pivotY = 0.352f,
                collider = new Vector2(0.36f, 0.14f), hp = 90, mass = 6, kind = EnemyKind.Melee,
                anims = new[] { new Anim("Idle", "Extra/Pig Rider Spear Goblin/Pig Rider_Idle.png", 8, true),
                                new Anim("Move", "Extra/Pig Rider Spear Goblin/Pig Rider_Run.png", 10, true),
                                new Anim("Attack", "Extra/Pig Rider Spear Goblin/Pig Rider_Attack.png", 12, false) },
                tune = e => { e.moveSpeed = 1.5f; e.damage = 13; e.windup = 0.3f; e.cooldown = 1.1f; e.attackRange = 0.2f; e.aggroRange = 4.0f;
                              e.knockback = 2.5f; e.enrageAt = 1.0f; e.dashSpeed = 5f; e.dashTime = 0.42f; e.dashCooldown = 3.5f; e.leashRange = 6f; }
            },
            new Def {
                key = "Mad_Bear", display = "Mad Bear", ppu = 100, pivotY = 0.309f, collider = new Vector2(0.60f, 0.18f), hp = 220, mass = 10,
                kind = EnemyKind.Melee,
                anims = new[] { new Anim("Idle", "Bear/Bear_Idle.png", 8, true), new Anim("Move", "Bear/Bear_Run.png", 10, true),
                                new Anim("Attack", "Bear/Bear_Attack.png", 12, false) },
                // slow, 2 claw swipes in a row (frames 6 and 8), short charge below 50% HP
                tune = e => { e.moveSpeed = 0.8f; e.damage = 14; e.windup = 0.5f; e.cooldown = 1.4f; e.attackRange = 0.15f; e.aggroRange = 3.2f;
                              e.knockback = 3f; e.comboHits = 2; e.comboGap = 0.17f; e.enrageAt = 0.5f; e.dashSpeed = 4f; e.dashTime = 0.35f;
                              e.dashCooldown = 3.5f; e.leashRange = 4f; }
            },
            new Def {
                key = "Camp_Gunner", display = "Camp Gunner", ppu = 100, pivotY = 0.30f, collider = new Vector2(0.40f, 0.22f), hp = 60, mass = 50,
                immuneCC = true, kind = EnemyKind.Turret,
                anims = new[] { new Anim("Idle", CannonDir + "Cannon_Right.png", 1, true) },
                fx = new[] { new Fx(CannonDir + "Cannon_UpRight.png", 100, 0.5f, 0.30f), new Fx(CannonDir + "Cannon_Up.png", 100, 0.5f, 0.30f),
                             new Fx(CannonDir + "Cannon_DownRight.png", 100, 0.5f, 0.30f), new Fx(CannonDir + "Cannon_Down.png", 100, 0.5f, 0.30f),
                             new Fx(CannonDir + "Cannon_Ball.png", 130, 0.5f, 0.5f) },
                // 1 shell every 2.5 s; arcing 1 s flight, red shadow marks the landing spot
                tune = e => { e.damage = 14; e.cooldown = 2.5f; e.aggroRange = 4.5f; e.leashRange = 0f; e.knockback = 2f;
                              e.strikeWarn = 1.0f; e.strikeRadius = 0.36f; e.strikeImpactFrame = 0; e.strikeFps = 16f;
                              e.strikeColor = new Color(1f, 0.35f, 0.25f, 1f);
                              e.strikeFrames = Sprites(Pack + "Hex Shaman/Hex Shaman_Explosion.png");
                              e.shellSprite = Sprites(Pack + CannonDir + "Cannon_Ball.png").FirstOrDefault();
                              e.aimSprites = new[] { CannonDir + "Cannon_Right.png", CannonDir + "Cannon_UpRight.png", CannonDir + "Cannon_Up.png",
                                                     CannonDir + "Cannon_DownRight.png", CannonDir + "Cannon_Down.png" }
                                             .Select(p => Sprites(Pack + p).FirstOrDefault()).ToArray(); }
            },
            new Def {
                key = "Blightdrinker", display = "Blightdrinker", ppu = 150, pivotY = 0.356f, collider = new Vector2(0.42f, 0.16f), hp = 150, mass = 6,
                kind = EnemyKind.Melee,   // original sheet colours (no blue tint)
                anims = new[] { new Anim("Idle", "Minotaur/Minotaur_Idle.png", 10, true), new Anim("Move", "Minotaur/Minotaur_Walk.png", 8, true),
                                new Anim("Attack", "Minotaur/Minotaur_Attack.png", 10, false) },
                tune = e => { e.moveSpeed = 0.9f; e.damage = 18; e.windup = 0.7f; e.cooldown = 1.8f; e.attackRange = 0.3f; e.aggroRange = 3.5f;
                              e.stunSeconds = 1.5f; e.knockback = 2f; }
            },
        };

        public static void BuildAll()
        {
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var d in Defs)
                {
                    foreach (var a in d.anims) Slice(P(a.file), d.ppu, d.pivotY, d.pivotX, a.frameW);
                    if (d.fx != null) foreach (var x in d.fx) Slice(P(x.file), x.ppu, x.pivotY, x.pivotX, x.frameW);
                }
                Slice(Pack + "Hex Shaman/Hex Shaman_Projectile.png", 200, 0.5f);
                Slice(Pack + "Hex Shaman/Hex Shaman_Explosion.png", 200, 0.5f);
                Slice(Pack + "Extra/Cave/Cave_Idle.png", CavePPU, 0.14f);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            AssetDatabase.Refresh();

            Directory.CreateDirectory(OutRoot + "/Animations");
            Directory.CreateDirectory(OutRoot + "/Prefabs");
            AssetDatabase.Refresh();

            int order = PlayerSortingOrder();
            var projectile = BuildProjectile(order + 1);
            var prefabs = new Dictionary<string, GameObject>();
            foreach (var d in Defs) prefabs[d.key] = BuildEnemy(d, order, (d.key == "Hex_Shaman" || d.key == "Goblin_Hexer") ? projectile : null);
            prefabs["Cave_Spawner"] = BuildCave(order);

            SetupScene(prefabs);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Soul Requiem] Built {prefabs.Count} Map 1 enemy prefabs in {OutRoot}/Prefabs and attached them to the scene.");
        }

        // ------------------------------------------------------------------ sprite slicing
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
                    name = n,
                    rect = new Rect(i * frame, 0, frame, h),
                    alignment = SpriteAlignment.Custom,
                    pivot = new Vector2(pivotX, pivotY),
                    border = Vector4.zero,
                    spriteID = old.TryGetValue(n, out var id) ? id : GUID.Generate()
                });
            }
            dp.SetSpriteRects(rects.ToArray());

            var nameIds = dp.GetDataProvider<ISpriteNameFileIdDataProvider>();
            if (nameIds != null)
                nameIds.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)).ToList());

            dp.Apply();
            importer.SaveAndReimport();
        }

        static Sprite[] Sprites(string path) =>
            AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s => s.rect.x).ToArray();

        // ------------------------------------------------------------------ animation
        static AnimationClip Clip(string folder, string name, Sprite[] frames, float fps, bool loop)
        {
            string p = $"{folder}/{name}.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(p);
            bool isNew = clip == null;
            if (isNew) clip = new AnimationClip();
            clip.frameRate = fps;

            var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
            // One key per frame; Unity already holds the last frame for 1/fps, so NO extra key (avoids a doubled last frame that stutters)
            var keys = new ObjectReferenceKeyframe[frames.Length];
            for (int i = 0; i < frames.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = frames[i] };
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            if (isNew) AssetDatabase.CreateAsset(clip, p);
            else EditorUtility.SetDirty(clip);
            return clip;
        }

        static AnimatorController Controller(string folder, string name, List<(string state, AnimationClip clip)> states)
        {
            string p = $"{folder}/{name}.controller";
            // Do NOT delete and recreate the file: prefabs / scenes pointing at the old controller would go "Missing" -> frozen enemies.
            // Keep the file (keeps its GUID), only clear the old states and rebuild.
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(p);
            if (ctrl == null)
            {
                if (File.Exists(p)) AssetDatabase.DeleteAsset(p);       // corrupt file, cannot be loaded
                ctrl = AnimatorController.CreateAnimatorControllerAtPath(p);
            }
            if (ctrl.layers.Length == 0) ctrl.AddLayer("Base Layer");
            var sm = ctrl.layers[0].stateMachine;
            foreach (var cs in sm.states.ToArray()) sm.RemoveState(cs.state);
            foreach (var sub in sm.stateMachines.ToArray()) sm.RemoveStateMachine(sub.stateMachine);
            foreach (var tr in sm.anyStateTransitions.ToArray()) sm.RemoveAnyStateTransition(tr);
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

        // ------------------------------------------------------------------ prefab
        static GameObject BuildEnemy(Def d, int order, GameObject projectile)
        {
            string animFolder = $"{OutRoot}/Animations/{d.key}";
            Directory.CreateDirectory(animFolder);
            AssetDatabase.Refresh();

            var states = new List<(string, AnimationClip)>();
            float attackLen = 0.6f, attack2Len = 0.6f;
            Sprite first = null;
            bool turret = d.kind == EnemyKind.Turret;
            foreach (var a in d.anims)
            {
                var frames = Sprites(P(a.file));
                if (frames.Length == 0) { Debug.LogWarning("Could not slice " + a.file); continue; }
                if (a.state == "Idle") first = frames[0];
                if (turret) continue;                         // the cannon turret swaps sprites by direction, no Animator
                var clip = Clip(animFolder, $"{d.key}_{a.state}", frames, a.fps, a.loop);
                if (a.state == "Attack") attackLen = frames.Length / a.fps;
                if (a.state == "Attack2") attack2Len = frames.Length / a.fps;
                states.Add((a.state, clip));
            }
            var ctrl = turret ? null : Controller(animFolder, d.key, states);

            var go = new GameObject(d.key);
            try
            {
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = first;
                sr.sortingOrder = order;
                sr.spriteSortPoint = SpriteSortPoint.Pivot;
                sr.color = d.tint;

                if (ctrl != null)
                {
                    var anim = go.AddComponent<Animator>();
                    anim.runtimeAnimatorController = ctrl;
                }

                var rb = go.AddComponent<Rigidbody2D>();
                rb.bodyType = turret ? RigidbodyType2D.Kinematic : RigidbodyType2D.Dynamic;
                rb.gravityScale = 0f;
                rb.freezeRotation = true;
                rb.mass = d.mass;
                rb.linearDamping = 5f;
                rb.interpolation = RigidbodyInterpolation2D.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

                if (turret)
                {
                    var box = go.AddComponent<BoxCollider2D>();
                    box.size = d.collider;
                    box.offset = new Vector2(0f, d.collider.y * 0.5f);
                }
                else
                {
                    var col = go.AddComponent<CapsuleCollider2D>();
                    col.direction = CapsuleDirection2D.Horizontal;
                    col.size = d.collider;
                    col.offset = new Vector2(0f, d.collider.y * 0.35f);
                }

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
                ai.attack2Time = attack2Len;
                if (projectile != null) ai.projectilePrefab = projectile;
                EnemyDefinitionTools.ApplyOrCreate(go, d.key, OutRoot);            // asset Definitions/<key> wins over the values in code
                ai.attackTime = Mathf.Max(attackLen, ai.windup + 0.1f);

                return PrefabUtility.SaveAsPrefabAsset(go, $"{OutRoot}/Prefabs/{d.key}.prefab");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        static GameObject BuildProjectile(int order)
        {
            var fly = Sprites(Pack + "Hex Shaman/Hex Shaman_Projectile.png");
            var boom = Sprites(Pack + "Hex Shaman/Hex Shaman_Explosion.png");
            var go = new GameObject("Hex_Shaman_Projectile");
            try
            {
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = fly.Length > 0 ? fly[0] : null;
                sr.sortingOrder = order;
                var p = go.AddComponent<Projectile>();
                p.radius = 0.1f;
                p.flyFrames = fly;
                p.explodeFrames = boom;
                p.fps = 14f;
                p.spinToDirection = false;
                return PrefabUtility.SaveAsPrefabAsset(go, $"{OutRoot}/Prefabs/Hex_Shaman_Projectile.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        static GameObject BuildCave(int order)
        {
            string animFolder = $"{OutRoot}/Animations/Cave";
            Directory.CreateDirectory(animFolder);
            AssetDatabase.Refresh();
            var frames = Sprites(Pack + "Extra/Cave/Cave_Idle.png");
            var clip = Clip(animFolder, "Cave_Idle", frames, 8, true);
            var ctrl = Controller(animFolder, "Cave", new List<(string, AnimationClip)> { ("Idle", clip) });

            var go = new GameObject("Cave_Spawner");
            try
            {
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = frames.Length > 0 ? frames[0] : null;
                sr.sortingOrder = order;
                sr.spriteSortPoint = SpriteSortPoint.Pivot;
                go.AddComponent<Animator>().runtimeAnimatorController = ctrl;
                var box = go.AddComponent<BoxCollider2D>();          // the cave base blocks the path, cannot be walked through
                box.size = new Vector2(1.3f, 0.3f);
                box.offset = new Vector2(0f, 0.12f);
                var cave = go.AddComponent<CaveSpawner>();
                cave.perWave = 3;
                cave.waves = 2;
                cave.triggerRadius = 7 * 0.32f;
                cave.spawnRadius = 0.45f;
                cave.spawnOffset = new Vector2(0f, -0.3f);
                return PrefabUtility.SaveAsPrefabAsset(go, $"{OutRoot}/Prefabs/Cave_Spawner.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        // ------------------------------------------------------------------ scene
        static int PlayerSortingOrder()
        {
            var pc = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            var sr = pc != null ? pc.GetComponentInChildren<SpriteRenderer>() : null;
            return sr != null ? sr.sortingOrder : 2;
        }

        static void SetupScene(Dictionary<string, GameObject> prefabs)
        {
            var scene = EditorSceneManager.GetActiveScene();
            var pc = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            if (pc == null) { Debug.LogError("The scene has no Player (PlayerController)."); return; }

            // Player
            pc.gameObject.tag = "Player";
            var hp = pc.GetComponent<Health>();
            if (hp == null) hp = Undo.AddComponent<Health>(pc.gameObject);
            var so = new SerializedObject(hp);
            so.FindProperty("maxHp").floatValue = 100f;
            so.FindProperty("isPlayer").boolValue = true;
            so.FindProperty("immuneToCrowdControl").boolValue = false;
            so.ApplyModifiedProperties();
            if (pc.GetComponent<PlayerCombat>() == null) Undo.AddComponent<PlayerCombat>(pc.gameObject);

            // Map: remove a stray Camera (if one was attached to the map)
            var map = UnityEngine.Object.FindFirstObjectByType<SuperMap>();
            if (map != null)
            {
                foreach (var c in map.GetComponents<Component>())
                    if (c != null && c.GetType().Name == "UniversalAdditionalCameraData") Undo.DestroyObjectImmediate(c);
                var cam = map.GetComponent<Camera>();
                if (cam != null) { Undo.DestroyObjectImmediate(cam); Debug.Log("[Soul Requiem] Removed a stray Camera from the map."); }
            }

            // Director + HUD
            var director = UnityEngine.Object.FindFirstObjectByType<MapEncounterDirector>();
            if (director == null)
            {
                var go = new GameObject("EncounterDirector");
                Undo.RegisterCreatedObjectUndo(go, "Create EncounterDirector");
                director = go.AddComponent<MapEncounterDirector>();
            }
            Undo.RecordObject(director, "Setup director");
            director.mapRoot = map != null ? map.transform : null;
            director.tileSize = 0.32f;
            director.enemies = prefabs.Select(kv => new MapEncounterDirector.EnemyEntry { enemyName = kv.Key, prefab = kv.Value }).ToList();
            EditorUtility.SetDirty(director);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
#endif
