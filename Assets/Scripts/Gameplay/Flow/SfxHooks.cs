using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// The ONE place that picks sounds for every gameplay event — each system's code only calls one line here.
    /// Sound list + volumes: _tools/sfx/manifest_v2.py -> Resources/Audio/sfx_mix.json (gain / gap), files in Resources/Audio/SFX.
    /// Enemies: each type (EnemyDefinition asset name) belongs to a VOICE FAMILY (table Voices). Per family:
    ///   v_&lt;family&gt;_alert on spotting Ren · v_&lt;family&gt;_attack on wind-up / attack · v_&lt;family&gt;_hurt when hit (some families only) · v_&lt;family&gt;_death on death.
    ///   Hit sounds follow the family's MATERIAL (flesh / armour / bone / wood / slime). Missing file -> silent (Sfx skips it).
    /// Bosses: signature skills call BossMove(this, "SkillName") at the start of the coroutine -> table BossMoves.
    /// Map mechanisms can override their sound with the Tiled property `sfx` (sound id, or "none" for silence).
    /// Called by: EnemyAI (alert / Play / hurt / death), boss skill files, player scripts, DialogueBox (voices), gates and mechanisms.
    /// </summary>
    public static class SfxHooks
    {
        private enum Mat { Flesh, Armor, Bone, Wood, Slime }

        private sealed class Voice
        {
            public readonly string family; public readonly Mat mat; public readonly bool claw, caster;
            public Voice(string f, Mat m, bool claw = false, bool caster = false) { family = f; mat = m; this.claw = claw; this.caster = caster; }
        }

        /// <summary>EnemyDefinition asset name (or prefab name) -> voice family. New enemy: add one row.</summary>
        private static readonly Dictionary<string, Voice> Voices = new Dictionary<string, Voice>(StringComparer.OrdinalIgnoreCase)
        {
            // Map 1 — goblins + wild beasts
            { "Goblin_Spearman", new Voice("goblin", Mat.Flesh) }, { "Goblin_Torch", new Voice("goblin", Mat.Flesh) },
            { "Goblin_Hexer", new Voice("goblin", Mat.Flesh, caster: true) }, { "Goblin_PigRider", new Voice("goblin", Mat.Flesh) },
            { "Spear_Goblin", new Voice("goblin", Mat.Flesh) }, { "Hex_Shaman", new Voice("goblin", Mat.Flesh, caster: true) },
            { "Camp_Gunner", new Voice("goblin", Mat.Flesh) }, { "Blightdrinker", new Voice("goblin", Mat.Flesh) },
            { "Barrel_Goblin", new Voice("goblin", Mat.Flesh) },
            { "Grok_Troll", new Voice("troll", Mat.Flesh) }, { "Minotaur", new Voice("troll", Mat.Flesh) },
            { "Mad_Bear", new Voice("bear", Mat.Flesh, claw: true) },
            { "Lizard", new Voice("reptile", Mat.Flesh, claw: true) }, { "Snake", new Voice("reptile", Mat.Flesh, claw: true) },
            { "Spider", new Voice("spider", Mat.Slime, claw: true) },
            // Map 2 — sewer beasts + Malric's specimens
            { "Plague_Rat", new Voice("rat", Mat.Flesh, claw: true) }, { "Bloated_Rat", new Voice("rat", Mat.Slime, claw: true) },
            { "Channel_Viper", new Voice("reptile", Mat.Flesh, claw: true) }, { "Ichor_Snapper", new Voice("reptile", Mat.Armor, claw: true) },
            { "Skinned_Hopper", new Voice("toad", Mat.Slime, claw: true) }, { "Brood_Toad", new Voice("toad", Mat.Slime, claw: true) },
            { "Vat_Born", new Voice("flesh", Mat.Slime, claw: true) }, { "Mimic", new Voice("flesh", Mat.Wood, claw: true) },
            { "Ichor_Ram", new Voice("ram", Mat.Armor, claw: true) }, { "Plague_Eye", new Voice("eye", Mat.Slime) },
            { "Specimen_Zero", new Voice("sz", Mat.Slime) },
            // Map 2b / 3 / 3b / 4
            { "Last_Guard", new Voice("cultist", Mat.Armor) },
            { "BL_Warrior", new Voice("cultist", Mat.Armor) }, { "BL_Lancer", new Voice("cultist", Mat.Armor) },
            { "BL_Archer", new Voice("cultist", Mat.Armor) }, { "BL_Monk", new Voice("cultist", Mat.Flesh, caster: true) },
            { "Choir_Novice", new Voice("cultist", Mat.Flesh, caster: true) }, { "Precentor_Morrow", new Voice("cultist", Mat.Flesh, caster: true) },
            { "Lantern_Knight", new Voice("cultist", Mat.Armor) },
            { "Bat", new Voice("bat", Mat.Flesh, claw: true) }, { "Giant_Bat", new Voice("bat", Mat.Flesh, claw: true) },
            { "Pit_Bat", new Voice("bat", Mat.Flesh, claw: true) },
            { "Undead", new Voice("undead", Mat.Bone) }, { "Skeleton", new Voice("undead", Mat.Bone) }, { "Mage_Skeleton", new Voice("undead", Mat.Bone, caster: true) }, { "Bone_Jailer", new Voice("undead", Mat.Bone) },
            { "Grave_Worm", new Voice("worm", Mat.Slime, claw: true) },
            { "Ent_1", new Voice("ent", Mat.Wood) }, { "Ent_2", new Voice("ent", Mat.Wood) }, { "Ent_3", new Voice("ent", Mat.Wood) },
            { "Dummy", new Voice("", Mat.Wood) },
            { "Vampire", new Voice("vamp", Mat.Flesh, claw: true) }, { "Vampire_Archer", new Voice("vamp", Mat.Flesh) },
            { "Matriarch_Isolde", new Voice("vamp", Mat.Flesh, claw: true, caster: true) },
            { "Red_Field", new Voice("ghost", Mat.Bone) },
            { "Choir_Cutthroat", new Voice("cultist", Mat.Flesh) }, { "Hollow_Cantor", new Voice("cultist", Mat.Flesh, caster: true) },
            { "Lantern_Warden", new Voice("cultist", Mat.Armor) }, { "The_Registrar", new Voice("cultist", Mat.Flesh, caster: true) },
            { "Brazen_Reliquary", new Voice("", Mat.Armor) }, { "Hollow_Champion", new Voice("troll", Mat.Armor) },   // Reliquary duo
            { "Acolyte_Fire", new Voice("cultist", Mat.Flesh, caster: true) }, { "Acolyte_Frost", new Voice("cultist", Mat.Flesh, caster: true) },
            { "Acolyte_Storm", new Voice("cultist", Mat.Flesh, caster: true) }, { "Acolyte_Umbral", new Voice("cultist", Mat.Flesh, caster: true) },
        };

        /// <summary>"Class.Skill" (boss skill coroutine) -> warning sound when the skill starts.</summary>
        private static readonly Dictionary<string, string> BossMoves = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "ToadSkills.Tongue", "toad_tongue" }, { "ToadSkills.Brood", "toad_burp" }, { "ToadSkills.SecondWave", "toad_burp" },
            { "RamSkills.Charge", "ram_charge" }, { "RamSkills.Rampage", "ram_charge" }, { "RamSkills.Steam", "steam_vent" },
            { "PlagueEyeSkills.Ray", "eye_beam_charge" }, { "PlagueEyeSkills.Summon", "v_eye_alert" },
            { "SpecimenZeroSkills.Wail", "sz_inhale" }, { "SpecimenZeroSkills.Lash", "sz_lash" },   // Hand: sound when the hand slams (GroundStrike)
            { "SpecimenZeroSkills.Roots", "st_bind" }, { "SpecimenZeroSkills.Gaze", "eye_beam_charge" },
            { "SpecimenZeroSkills.ChainsBreak", "chains_break" }, { "SpecimenZeroSkills.OpenEye", "v_sz_alert" },
            { "MorrowSkills.TollStrike", "bell_toll" }, { "MorrowSkills.TollOfHymns", "bell_toll" },
            { "MorrowSkills.ChoirLances", "v_chant_cast" }, { "MorrowSkills.DirgeNotes", "v_chant_cast" },
            { "MorrowSkills.Requiem", "v_chant_cast" }, { "MorrowSkills.RaiseTheNamed", "v_undead_alert" },
            { "IsoldeSkills.BatSwarm", "bat_swarm" }, { "IsoldeSkills.ChildrenOfTheNight", "bat_swarm" }, { "IsoldeSkills.BatDive", "bat_swarm" },
            { "IsoldeSkills.MistStep", "mist_step" }, { "IsoldeSkills.NightFlight", "mist_step" }, { "IsoldeSkills.BloodLash", "blood_whip" },
            { "IsoldeSkills.BatShot", "v_bat_attack" }, { "IsoldeSkills.SanguineSiphon", "life_drain" }, { "MorrowSkills.Drain", "life_drain" },
            { "ToadSkills.LeapSlam", "boss_leap" }, { "ToadSkills.BellyFlop", "boss_leap" }, { "ToadSkills.PhaseLeap", "boss_leap" },
            { "LastGuardSkills.Leap", "boss_leap" }, { "PlagueEyeSkills.Spin", "eye_spin" },
            { "IsoldeSkills.Feast", "life_drain" }, { "AncientEntSkills.Communion", "life_drain" }, { "LastGuardSkills.LastStand", "lk_vow" },
            { "SpecimenZeroSkills.Scorch", "st_burn" }, { "SpecimenZeroSkills.WatcherShot", "proj_magic" },
            { "LanternKnightSkills.Vow", "lk_vow" }, { "LanternKnightSkills.Judgement", "lk_vow" }, { "LanternKnightSkills.Lightless", "lk_vow" },
            { "LanternKnightSkills.Charge", "lg_charge" },
            { "LastGuardSkills.Charge", "lg_charge" }, { "LastGuardSkills.Bash", "lg_shield_bash" }, { "LastGuardSkills.GuardBroken", "lg_shield_break" },
            { "RedFieldSkills.OathBreaks", "oath_break" },
            { "RegistrarSkills.StruckNames", "registrar_quill" }, { "RegistrarSkills.LedgerBolts", "registrar_quill" },
            { "AncientEntSkills.RootLine", "st_bind" }, { "AncientEntSkills.BrambleRing", "st_bind" }, { "AncientEntSkills.Grasp", "st_bind" },
            { "AncientEntSkills.Summon", "v_ent_alert" },
            { "WormSkills.Tunnel", "worm_burrow" }, { "WormSkills.Bite", "v_worm_attack" },
        };

        /// <summary>Animation state = WIND-UP (telegraph) -> attack cry.</summary>
        private static readonly HashSet<string> WindupStates = new HashSet<string>(StringComparer.Ordinal)
        { "Windup", "Prepare", "JumpPrepare", "ChargeBegin", "VolleyWindup", "BeamWindup", "Aim", "SpinStart", "Vow", "Cast", "Burp", "Toll" };

        /// <summary>Animation state = SWING (attack) -> weapon / claw whoosh.</summary>
        private static readonly HashSet<string> StrikeStates = new HashSet<string>(StringComparer.Ordinal)
        { "Attack", "Attack2", "Slam", "Thrust", "Bash", "Whirl", "Spin", "Leap", "Dash" };

        /// <summary>VFX (name prefix) -> matching sound. Matched by prefix to include direction suffixes (_E/_S/_N) and frames.</summary>
        private static readonly KeyValuePair<string, string>[] VfxSfx =
        {
            new KeyValuePair<string, string>("lg_slam", "boss_slam"),
            new KeyValuePair<string, string>("m3_lk_slam", "boss_slam"),
            new KeyValuePair<string, string>("ground_impact", "boss_slam"),
            new KeyValuePair<string, string>("m3_ground_burst", "ground_strike"),
            new KeyValuePair<string, string>("lg_break", "lg_shield_break"),
            new KeyValuePair<string, string>("lg_thrust", "lg_spear_thrust"),
            new KeyValuePair<string, string>("lg_whirl_front", "lg_whirl"),
            new KeyValuePair<string, string>("lg_stun_front", "lg_wall_crash"),
            new KeyValuePair<string, string>("lg_spark", "hit_block"),
            new KeyValuePair<string, string>("m3_blood_splash", "st_bleed"),
            new KeyValuePair<string, string>("m3_root_spike", "root_spike"),
            new KeyValuePair<string, string>("m3_dash_dust", "pl_dodge"),
            new KeyValuePair<string, string>("m3_heal_lantern", "v_chant_cast"),
            new KeyValuePair<string, string>("sk_bound", "st_bind"),
            new KeyValuePair<string, string>("sk_vuln", "st_mark"),
            new KeyValuePair<string, string>("status_bleed", "st_bleed"),
            new KeyValuePair<string, string>("status_burn", "st_burn"),
            new KeyValuePair<string, string>("status_chill", "st_chill"),
            new KeyValuePair<string, string>("status_bind", "st_bind"),
            new KeyValuePair<string, string>("status_mark", "st_mark"),
            new KeyValuePair<string, string>("rf_blood_snare", "blood_splat"),
            // shared Map 3 boss skill effects
            new KeyValuePair<string, string>("m3_vespers", "teleport_dark"),
            new KeyValuePair<string, string>("m3_blood_mist", "mist_step"),
            new KeyValuePair<string, string>("m3_blood_pool", "blood_splat"),
            new KeyValuePair<string, string>("m3_hymn_pop", "hymn_pop"),
            new KeyValuePair<string, string>("m3_lantern_charge", "lk_vow"),
            new KeyValuePair<string, string>("m3_lk_sigil", "lk_vow"),
            new KeyValuePair<string, string>("m3_lk_lightless", "lk_vow"),
        };

        /// <summary>Mechanism type (Tiled Type) -> sound when pressing T.</summary>
        private static readonly Dictionary<string, string> UseSfx = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "lever", "lever" }, { "valve", "valve_turn" }, { "console", "lever" }, { "sluice", "valve_turn" },
            { "cipher", "dial_turn" }, { "dial", "dial_turn" }, { "rosewindow", "dial_turn" },
            { "passage", "passage" }, { "cover", "stone_door" }, { "note", "pickup_note" },
            // chest / bell / breakable / tank / puzzles: the sound plays inside the mechanism itself (exactly on open / break / solve, silent while locked)
        };

        private static readonly Dictionary<int, float> vocalAt = new Dictionary<int, float>();
        private static readonly Dictionary<int, KeyValuePair<string, float>> stateAt = new Dictionary<int, KeyValuePair<string, float>>();

        // ------------------------------------------------------------------ voice families
        private static Voice VoiceOf(EnemyAI ai)
        {
            if (ai == null) return null;
            string key = ai.definition != null ? ai.definition.name : ai.gameObject.name.Replace("(Clone)", "").Trim();
            if (Voices.TryGetValue(key, out var v)) return v;
            foreach (var kv in Voices)                                 // names with a suffix (Undead_3, Plague_Rat_12...)
                if (key.StartsWith(kv.Key, StringComparison.OrdinalIgnoreCase)) return kv.Value;
            return null;
        }

        private static string V(Voice v, string what) => v == null || v.family.Length == 0 ? "" : "v_" + v.family + "_" + what;

        /// <summary>Each enemy cries at least `gap` seconds apart (a pack of 6 does not scream all at once).</summary>
        private static bool VocalReady(EnemyAI ai, float gap)
        {
            int k = ai.GetInstanceID();
            float now = Time.time;
            if (vocalAt.TryGetValue(k, out float t) && now - t < gap) return false;
            if (vocalAt.Count > 512) vocalAt.Clear();
            vocalAt[k] = now;
            return true;
        }

        /// <summary>Enemy just spotted Ren (EnemyAI turned aggressive).</summary>
        public static void EnemyAlert(EnemyAI ai)
        {
            var v = VoiceOf(ai);
            if (v == null || !VocalReady(ai, 3f)) return;
            Sfx.Play(V(v, "alert"), ai.transform.position);
        }

        /// <summary>EnemyAI just started a new animation (called in EnemyAI.Play when the state really changes).</summary>
        public static void EnemyState(EnemyAI ai, string state)
        {
            if (ai == null || string.IsNullOrEmpty(state)) return;
            // skill sets replay the same state repeatedly (PlayState every frame / tick) -> only the first of the chain makes a sound
            int k = ai.GetInstanceID();
            float now = Time.time;
            bool repeat = stateAt.TryGetValue(k, out var last) && last.Key == state && now - last.Value < 0.35f;
            if (stateAt.Count > 512) stateAt.Clear();
            stateAt[k] = new KeyValuePair<string, float>(state, now);
            if (repeat) return;
            var v = VoiceOf(ai);
            Vector2 p = ai.transform.position;
            if (WindupStates.Contains(state))
            {
                if (ai.IsBoss && state == "BeamWindup") return;                       // already has its own sound in BossMoves
                string id = v != null && v.caster && (state == "Cast" || state == "Vow") ? "v_chant_cast" : V(v, "attack");
                if (state == "Toll") id = "bell_toll";
                if (VocalReady(ai, 0.6f)) Sfx.Play(id, p);
                return;
            }
            if (state == "BeamLoop") { Sfx.Play("eye_beam", p); return; }
            if (state == "Stuck") { Sfx.Play("lg_spear_stuck", p); return; }          // The Last Guard: spear planted in the floor
            if (!StrikeStates.Contains(state)) return;
            // Eye / Specimen Zero / ghost army swing no weapon -> no whoosh (their skills have their own sounds)
            if (v != null && (v.family == "eye" || v.family == "sz" || v.family == "ghost")) return;
            Sfx.Play(v != null && v.claw ? "en_claw" : "en_swing", p);
            // no separate wind-up state (simple enemies) -> cry now and then on the swing
            if (v != null && UnityEngine.Random.value < 0.55f && VocalReady(ai, 1.1f)) Sfx.Play(V(v, "attack"), p);
        }

        /// <summary>A boss signature skill starts (called on the first line of the skill coroutine).</summary>
        public static void BossMove(Component skills, string move)
        {
            if (skills == null) return;
            if (BossMoves.TryGetValue(skills.GetType().Name + "." + move, out string id)) Sfx.Play(id, skills.transform.position);
        }

        // ------------------------------------------------------------------ health
        public static void Hit(Health h)
        {
            if (h == null) return;
            Vector2 p = h.transform.position;
            if (h.IsPlayer) { Sfx.Play("pl_hurt", p); return; }
            var ai = h.GetComponent<EnemyAI>();
            var v = VoiceOf(ai);
            Mat m = v != null ? v.mat : Mat.Flesh;
            Sfx.Play(m == Mat.Armor ? "hit_armor" : m == Mat.Bone ? "hit_bone" : m == Mat.Wood ? "hit_wood" : m == Mat.Slime ? "hit_slime" : "hit_flesh", p);
            // pain cries: only families with a v_<family>_hurt file (goblin, troll, cultist), half the time, each enemy ≥ 0.7 s apart
            if (ai != null && !h.IsDead && UnityEngine.Random.value < 0.5f && Sfx.Has(V(v, "hurt")) && VocalReady(ai, 0.7f))
                Sfx.Play(V(v, "hurt"), p);
        }

        public static void Died(Health h)
        {
            if (h == null) return;
            if (h.IsPlayer) { Sfx.Play2D("pl_death"); return; }
            var ai = h.GetComponent<EnemyAI>();
            var v = VoiceOf(ai);
            Vector2 p = h.transform.position;
            string cry = V(v, "death");
            if (Sfx.Has(cry)) Sfx.Play(cry, p);
            if (ai != null && ai.IsBoss) Sfx.Play("boss_death", p);
            else if (v == null || (v.family != "undead" && v.family != "bat" && v.family != "ghost")) Sfx.Play("en_death", p);
        }

        public static void Blocked(Health h) { if (h != null) Sfx.Play("hit_block", h.transform.position); }

        public static void Parried(Health h) { if (h != null) Sfx.Play("parry", h.transform.position); }

        // ------------------------------------------------------------------ voices
        /// <summary>Speaker name (lowercase, partial match) -> blip voice (bl_*, made with _tools/sfx/make_blips.py). Order matters (goblin child before goblin). New speaker: add one row.</summary>
        private static readonly KeyValuePair<string, string>[] SpeechCast =
        {
            new KeyValuePair<string, string>("goblin child", "bl_goblin_child"),
            new KeyValuePair<string, string>("grok", "bl_grok"),
            new KeyValuePair<string, string>("todd", "bl_todd"),
            new KeyValuePair<string, string>("pell", "bl_pell"),
            new KeyValuePair<string, string>("aldous", "bl_aldous"),
            new KeyValuePair<string, string>("hooded", "bl_hooded"),
            new KeyValuePair<string, string>("absalom", "bl_hooded"),
            new KeyValuePair<string, string>("isolde", "bl_isolde"),
            new KeyValuePair<string, string>("morrow", "bl_morrow"),
            new KeyValuePair<string, string>("lantern knight", "bl_lantern_knight"),
            new KeyValuePair<string, string>("fisherman", "bl_elder"),
            new KeyValuePair<string, string>("woman", "bl_villager_f"),
            new KeyValuePair<string, string>("greystone", "bl_goblin"),
            new KeyValuePair<string, string>("goblin", "bl_goblin"),
            new KeyValuePair<string, string>("farmhand", "bl_villager_m"),
            new KeyValuePair<string, string>("smith", "bl_villager_m"),
            new KeyValuePair<string, string>("peddler", "bl_villager_m"),
            new KeyValuePair<string, string>("villager", "bl_villager_m"),
            new KeyValuePair<string, string>("man", "bl_villager_m"),
        };

        /// <summary>Written text (notice boards, inscriptions, letters, ledgers) -> narration ticks (bl_narrate) instead of a human voice.</summary>
        private static readonly string[] Written = { "board", "notice", "scratched", "carved", "letter", "note", "journal", "ledger", "book", "inscription", "page", "plaque", "sign" };

        /// <summary>Blip voice for a speaker. box = dialogue box (unnamed narration -> bl_narrate); unnamed subtitles -> silent.
        /// Named speakers without their own voice -> bl_default (Undertale style: everyone who speaks makes a sound).</summary>
        public static string BlipId(string speaker, bool box)
        {
            string s = (speaker ?? "").Trim().ToLowerInvariant();
            if (s.Length == 0) return box ? "bl_narrate" : null;
            if (s == "ren") return "bl_ren";
            foreach (var w in Written) if (s.Contains(w)) return "bl_narrate";
            foreach (var kv in SpeechCast) if (s.Contains(kv.Key)) return kv.Value;
            foreach (var word in s.Split(' ', ',')) if (word == "ent") return "bl_ent";   // Penitent / Elder / Ancient Ent ("ent" as a whole word)
            return "bl_default";
        }

        // ------------------------------------------------------------------ player
        /// <summary>Footsteps by ground: outdoor map (grass / dirt) or underground (stone).</summary>
        public static string FootstepId()
        {
            string s = SceneManager.GetActiveScene().name;
            return s == "Map1_GreystonePlains" || s == "Map3_ValeOfGraves" ? "footstep_grass" : "footstep";
        }

        /// <summary>Casting a Mantra: mantra_&lt;Skill asset name in lowercase&gt; (e.g. mantra_groundslam).</summary>
        public static void Mantra(ScriptableObject skill, Vector2 pos)
        {
            if (skill == null) return;
            Sfx.Play("mantra_" + skill.name.ToLowerInvariant(), pos);
        }

        // ------------------------------------------------------------------ VFX / bullets / area attacks
        public static void Vfx(string name, Vector2 pos)
        {
            if (string.IsNullOrEmpty(name)) return;
            foreach (var kv in VfxSfx)
                if (name.StartsWith(kv.Key, StringComparison.Ordinal)) { Sfx.Play(kv.Value, pos); return; }
        }

        /// <summary>Bullet fired: names with arrow / bolt / spike / spear -> bowstring sound, others -> spell sound.</summary>
        public static void Launched(Component projectile)
        {
            if (projectile == null) return;
            string n = projectile.gameObject.name.ToLowerInvariant();
            bool physical = n.Contains("arrow") || n.Contains("bolt") || n.Contains("spike") || n.Contains("spear") || n.Contains("dart");
            Sfx.Play(physical ? "arrow_shot" : "proj_magic", projectile.transform.position);
        }

        // ------------------------------------------------------------------ gates / doors
        /// <summary>An animated gate starts closing / opening. Material guessed from the frame strip name.</summary>
        public static void Gate(string anim, bool closed, Vector2 pos)
        {
            string a = (anim ?? "").ToLowerInvariant();
            string id;
            if (a.Contains("wood") || a.Contains("door_h") || a.Contains("door_v")) id = "door_wood";
            else if (a.Contains("tomb") || a.Contains("boss") || a.Contains("skull") || a.Contains("stone")) id = "stone_door";
            else id = closed ? "gate_close" : "gate_open";
            Sfx.Play(id, pos);
        }

        public static void DoorLocked(Vector2 pos) => Sfx.Play("door_locked", pos);

        public static void KeyUsed(Vector2 pos) => Sfx.Play("key_unlock", pos);

        // ------------------------------------------------------------------ map mechanisms
        public static void MechanismUsed(Mechanism m)
        {
            if (m == null) return;
            string id = m.Props != null ? m.Props.GetString("sfx", "") : "";
            if (id == "none") return;
            if (string.IsNullOrEmpty(id) && !UseSfx.TryGetValue(m.Type ?? "", out id)) return;
            Sfx.Play(id, m.Rect.center);
        }

        /// <summary>Free sound for a mechanism (break, puzzle solved...): property `sfx_&lt;event&gt;` on the object overrides it, "none" = silent.</summary>
        public static void MechanismEvent(Mechanism m, string evt, string fallback)
        {
            if (m == null) return;
            string id = m.Props != null ? m.Props.GetString("sfx_" + evt, "") : "";
            if (id == "none") return;
            Sfx.Play(string.IsNullOrEmpty(id) ? fallback : id, m.Rect.center);
        }

        /// <summary>Toggle on (hide / show an object when flags are met): by default the iron gate sound if the hidden object is bars / a gate / a door.</summary>
        public static void Toggled(Mechanism m)
        {
            if (m == null || m.Props == null) return;
            string id = m.Props.GetString("sfx", "");
            if (id == "none") return;
            if (string.IsNullOrEmpty(id))
            {
                string what = (m.Name + " " + m.Props.GetString("hide_objects", "")).ToLowerInvariant();
                if (what.Contains("grate") || what.Contains("gate") || what.Contains("bars")) id = "gate_open";
                else if (what.Contains("door")) id = "door_wood";
                else return;
            }
            Sfx.Play(id, m.Rect.center);
        }
    }
}
