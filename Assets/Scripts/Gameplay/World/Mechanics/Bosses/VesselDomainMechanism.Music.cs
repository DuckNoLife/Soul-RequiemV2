using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// BACKGROUND MUSIC of The Crystal Vessel fight (track supplied by the user: "Psychronic — Reign of the Cyber King", 150 BPM, 3:03).
    /// Starts on the ROAR at the end of the awakening (or when a phase starts after re-entering the domain), runs through all 5 phases + LAST GRASP,
    /// quieter during phase breaks / when Ren is down, fades out when the Vessel dies. Loop: intro 0–12.2 s plays once, then 12.2 -> 181.2 s loops.
    /// Tiled properties on the vessel_domain object: `music` (id in Resources/Audio/Music, "" = no music), `music_volume` (0.55),
    /// `music_loop_start` (12.2), `music_loop_end` (181.2), `music_rest_level` (0.6 = level during breaks).
    /// Calls: Music.Play / Level (Core/Music.cs). Called by: Intro (MusicStart on the roar), PhaseLoop, Finale (MusicEnd), TickFight (TickMusic).
    /// </summary>
    public sealed partial class VesselDomainMechanism
    {
        private bool musicOn;

        private void MusicStart(float fadeIn)
        {
            string id = Str("music", "vessel_reign");
            if (string.IsNullOrEmpty(id) || Ctx.Flags.HasAll(SlainFlag)) return;
            Music.Play(id, fadeIn, Float("music_loop_start", 12.2f), Float("music_loop_end", 181.2f), Float("music_volume", 0.55f));
            Music.Level = 1f;
            musicOn = true;
        }

        private void MusicEnd(float fade)
        {
            if (!musicOn) return;
            musicOn = false;
            Music.Stop(fade);
            Music.Level = 1f;
        }

        /// <summary>Every frame: phase break / Ren down -> quieter; fighting -> full level.</summary>
        private void TickMusic()
        {
            if (!musicOn) return;
            Music.Level = actState == ActState.Playing ? 1f : Float("music_rest_level", 0.6f);
        }
    }
}
