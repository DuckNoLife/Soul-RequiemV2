namespace SoulRequiem.Core
{
    /// <summary>
    /// BACKGROUND MUSIC — one track at a time, file Resources/Audio/Music/&lt;id&gt;. The actual player: <see cref="MusicRunner"/>.
    ///   Music.Play(id, fadeIn, loopStart, loopEnd, gain)  play / keep a track (calling again with the same id while playing does nothing)
    ///   Music.Stop(fade)                                   fade out
    ///   Music.Level  0..1   set by the FIGHT (e.g. the Vessel between phases)
    ///   Music.Duck   0..1   set by SceneMusic per situation (dialogue / cutscene = 0, menu 0.5, down 0.35)
    /// Final volume = GameSettings.Music × gain × Level × Duck. Which track plays in which scene: Gameplay/Flow/SceneMusic.cs.
    /// </summary>
    public static class Music
    {
        public const string Folder = "Audio/Music";

        public static void Play(string id, float fadeIn = 2f, float loopStart = 0f, float loopEnd = 0f, float gain = 0.55f)
            => MusicRunner.Get()?.Play(id, fadeIn, loopStart, loopEnd, gain);

        public static void Stop(float fade = 2f) => MusicRunner.Get()?.Stop(fade);

        public static float Level
        {
            get { var r = MusicRunner.Get(); return r != null ? r.LevelGoal : 1f; }
            set { var r = MusicRunner.Get(); if (r != null) r.LevelGoal = UnityEngine.Mathf.Clamp01(value); }
        }

        public static float Duck
        {
            get { var r = MusicRunner.Get(); return r != null ? r.DuckGoal : 1f; }
            set { var r = MusicRunner.Get(); if (r != null) r.DuckGoal = UnityEngine.Mathf.Clamp01(value); }
        }

        /// <summary>id of the playing track (null = silent / fading out).</summary>
        public static string Current { get { var r = MusicRunner.Get(); return r != null ? r.CurrentId : null; } }
    }
}
