using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Table "Tiled Type" -> mechanism class. Adding a new kind: one line here.
    /// A Type missing from the table -> a Console warning (usually a typo in Tiled) and it is treated as a PassiveMechanism.
    /// </summary>
    internal static class MechanismRegistry
    {
        private static readonly Dictionary<string, Func<Mechanism>> Factories = new Dictionary<string, Func<Mechanism>>
        {
            // danger zones
            { "spout", () => new SpoutMechanism() },
            { "vent", () => new VentMechanism() },
            { "hazard", () => new HazardMechanism() },
            { "plate", () => new PlateMechanism() },             // trap trigger zone (pressure plate) -> signal pulse
            { "spikes", () => new SpikeMechanism() },            // animated floor spike tiles (rhythm / step / signal)
            { "launcher", () => new LauncherMechanism() },       // wall-mounted arrow / fire / ice shooters
            { "stillness", () => new StillnessMechanism() },     // motion sensor -> signal pulse (The Still Walk)
            { "cover", () => new CoverMechanism() },             // flipped table used as a bullet shield
            { "roller", () => new RollerMechanism() },           // spiked log rolling down a lane
            { "ranks", () => new RankMechanism() },              // marching ghost soldier ranks (Marching Hall, Map 3b)
            { "pit", () => new PitTrapMechanism() },             // collapsing floor (Map 4, in front of the Reliquary store door)
            { "blade", () => new BladeMechanism() },
            { "warden_statue", () => new WardenStatueMechanism() },   // Warden Gallery cover statues (Champion charge hit -> shatter, stun)
            { "powder_keg", () => new PowderKegMechanism() },         // powder keg (slash / bullet / mortar / thrown by the Champion)             // saw / spinning blade running on a track
            { "relic_rack", () => new RelicRackMechanism() },         // weapon rack - weapon race minigame (Reliquary Lists)
            { "crystal_pillar", () => new CrystalPillarMechanism() }, // 4-colour crystal pillars (replacing weapon racks) - the Champion drains them to change element
            { "drill_yard", () => new DrillYardMechanism() },         // dummy dodging minigame (The Drill Yard, Map 4)
            { "drill_horn", () => new DrillHornMechanism() },         // horn that starts / restarts the minigame
            { "absalom_hall", () => new AbsalomHallMechanism() },     // Map 4 final boss - Absalom, phases 1-4 in the Hall of Naming
            { "crate_puzzle", () => new CratePuzzleMechanism() },     // pushing crates onto pressure plates (Quartermaster's Store) - replaces drill_post
            { "flood", () => new FloodMechanism() },
            // flag-driven effects
            { "beam", () => new BeamMechanism() },
            { "shield", () => new ShieldMechanism() },
            { "light", () => new LightMechanism() },
            { "toggle", () => new ToggleMechanism() },
            { "tank", () => new TankMechanism() },
            { "sprite_prop", () => new SpritePropMechanism() },   // decoration with its own image + blocking base + light (seal pillars)
            // puzzles / interactive objects
            { "breakable", () => new BreakableMechanism() },
            { "sluice", () => new SluiceMechanism() },           // sluice valve - slash the valve, water floods down a lane (quenches the Ether Ram)
            { "lever", () => new LeverMechanism() },
            { "dial", () => new DialMechanism() },
            { "valve", () => new ValveMechanism() },
            { "bell", () => new BellMechanism() },
            { "registrar_summons", () => new RegistrarSummonsMechanism() },   // the Registrar pulls Ren into The Margin (Scriptorium)
            { "drifting_pages", () => new DriftingPagesMechanism() },         // pages drifting in the void (The Margin)
            { "void_tower", () => new WatchTowerMechanism() },                // watch tower + purple eye casting a light cone onto the island (The Unwritten)
            { "void_isle", () => new VoidIsleMechanism() },                   // floating rock islands / elemental crystals with depth
            { "vessel_domain", () => new VesselDomainMechanism() },           // The Crystal Vessel - domain fight (rig, breathing, awakening scene, phases)
            { "void_decor", () => new VoidDecorMechanism() },                 // void background - nebulae, stars, giant sigil ring, drafts
            { "ambush", () => new AmbushMechanism() },                        // troops encircling Ren (the Black Lantern at The Lychgate)
            { "summon", () => new SummonMechanism() },          // pulled / dragged away scene (dialogue + teleport) once the flags are set
            { "console", () => new ConsoleMechanism() },
            { "pipegrid", () => new PipeGridMechanism() },
            { "note", () => new NoteMechanism() },
            { "chest", () => new ChestMechanism() },             // chest with an opening animation, key lock, reward
            { "passage", () => new PassageMechanism() },         // shortcut / ladder / trapdoor (teleport within the map, with a one-way bolt)
            { "cipher", () => new CipherMechanism() },
            { "rosewindow", () => new RoseWindowMechanism() },
            // marker only (bosses read it by name)
            { "ritual_pillar", () => new PassiveMechanism() },
        };

        private static readonly HashSet<string> warned = new HashSet<string>();

        public static IEnumerable<string> KnownTypes => Factories.Keys;

        public static Mechanism Create(string type, string objectName)
        {
            if (type != null && Factories.TryGetValue(type, out var make)) return make();
            if (warned.Add(type ?? ""))
                Debug.LogWarning($"[MapMechanics] Type '{type}' (object '{objectName}') is not in MechanismRegistry - typo in Tiled? Behaviour skipped.");
            return new PassiveMechanism();
        }
    }
}
