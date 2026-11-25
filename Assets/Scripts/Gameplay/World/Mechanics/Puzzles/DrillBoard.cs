using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// HUD BOARD data shared by timed minigames / boss phases — static data for the HUD (RequiemHUD.Drill.cs: UpdateDrill) to draw.
    /// Writers: DrillYardMechanism (Burlap's Proving Grounds), AbsalomHallMechanism, RegistrarSkills (.Phases). The HUD only reads. Active = false -> hidden.
    /// Each station has a clear GOAL ("LANTERNS 1 / 3") + progress bar, a CLOCK (optional) and Morale = the station's LIVES (3, HUD shows "LIVES");
    /// out of lives = replay the station. Compact 640 x 64 board (RequiemHUD.BuildDrill). SceneMusic plays boss_burlap while Active in the Drill Yard.
    /// </summary>
    public static class DrillBoard
    {
        public static bool Active;
        /// <summary>Current station (1..Stations) + its name ("BLADE DRILL").</summary>
        public static int Station = 1, Stations = 3;
        public static string Title = "";
        /// <summary>Big goal line in the middle of the board ("SURVIVE", "LANTERNS 1 / 3", "BURLAP 2 / 3") + progress 0..1.</summary>
        public static string Goal = "";
        public static float GoalFrac;
        /// <summary>Clock: TimeFrac &lt; 0 = untimed station. TimeText = "0:42".</summary>
        public static float TimeFrac = -1f;
        public static string TimeText = "";
        /// <summary>Morale left / max; MoraleLostAt = when a pip was just lost (the HUD shakes it).</summary>
        public static int Morale = 3, MoraleMax = 3;
        public static float MoraleLostAt = -99f;
        /// <summary>OBJECTIVE LINE (what to do right now, full sentence) + TRACK BAR: TrackFrac &lt; 0 = use GoalFrac;
        /// with TrackLeft / TrackRight / TrackMarker (Resources sprite paths) -> the HUD draws end icons + a moving icon on the bar
        /// (Lantern Run: pedestal -> rack, the lantern moves with the carried distance).</summary>
        public static string Objective = "";
        public static float TrackFrac = -1f;
        public static string TrackLeft = "", TrackRight = "", TrackMarker = "";
        /// <summary>COMMAND / QUESTION being called — the HUD shows big text mid-screen; empty = hidden.</summary>
        public static string Command = "";
        public static float CommandTime = -99f;
        /// <summary>Event line that just happened ("CLEAN DODGE!") + its time (the HUD floats it up, then fades).</summary>
        public static string Pop = "";
        public static Color PopColor = Color.white;
        public static float PopTime = -99f;
        /// <summary>Big result mid-screen ("STATION CLEARED", "RANK A - PASSED") for a few seconds.</summary>
        public static string Result = "";
        public static Color ResultColor = Color.white;
        public static float ResultTime = -99f;

        /// <summary>Shout DURING LIVE PLAY — the HUD shows it instead of the objective line (one orange line) until ShoutUntil,
        /// instead of opening the bottom dialogue box over the dodge area.</summary>
        public static string Shout = "";
        /// <summary>Board labels — drill yard "STN" / "LIVES" / "BURLAP"; Absalom "PHASE" / "HALO" / "ABSALOM". Begin() resets the defaults.</summary>
        public static string StationLabel = "STN", PipLabel = "LIVES", ShoutName = "BURLAP";
        public static float ShoutUntil = -99f;

        public static void Say(string text, Color c) { Pop = text; PopColor = c; PopTime = Time.time; }
        public static void Yell(string text, float seconds) { Shout = text ?? ""; ShoutUntil = Time.time + seconds; }
        public static void Finish(string text, Color c) { Result = text; ResultColor = c; ResultTime = Time.time; }

        public static void LoseMorale()
        {
            Morale = Mathf.Max(0, Morale - 1);
            MoraleLostAt = Time.time;
        }

        /// <summary>Start a station: board shown, morale full.</summary>
        public static void Begin(int station, string title, int morale)
        {
            Active = true; Station = station; Title = title; Goal = ""; GoalFrac = 0f; TimeFrac = -1f; TimeText = "";
            Morale = MoraleMax = morale; MoraleLostAt = -99f; Pop = ""; PopTime = -99f;
            Objective = ""; TrackFrac = -1f; TrackLeft = TrackRight = TrackMarker = ""; Command = ""; Shout = ""; ShoutUntil = -99f;
            StationLabel = "STN"; PipLabel = "LIVES"; ShoutName = "BURLAP";
        }
    }
}
