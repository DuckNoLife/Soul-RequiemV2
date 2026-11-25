using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// TURNED BLADE — the book "Book of the Turned Blade" (flag book_turned_blade, Forgotten Store, Map 2).
    /// A normal slash (PlayerMelee) hitting a bullet in flight within slash range -> the bullet reflects BACK the way
    /// it came, faster, twice as strong, only hitting enemies. Flash + light camera kick + very short hit-stop.
    /// The book flag carries across maps (GameSession.Books) and is saved.
    /// </summary>
    public static class TurnedBlade
    {
        public const string Flag = "book_turned_blade";

        public static bool Known
        {
            get { var d = MapEncounterDirector.Instance; return d != null && d.Flags != null && d.Flags.Has(Flag); }
        }

        // the slash lands after 0.48 s -> bullets arrive during that time; keep a "parry window" for `Window` seconds after the slash frame,
        // bullets entering the arc during the window are reflected too (Projectile.Update calls Catch) — forgiving, no frame-perfect timing needed
        public const float Window = 0.2f;
        private static float openUntil = -1f;
        private static Vector2 wPivot, wFacing;
        private static float wRange;

        /// <summary>Reflect every bullet in the slash arc (centre `pivot`, direction `facing`, range `range`) and open the parry window. Returns the number reflected now.</summary>
        public static int TryParry(Vector2 pivot, Vector2 facing, float range)
        {
            if (!Known) return 0;
            wPivot = pivot; wFacing = facing; wRange = range; openUntil = Time.time + Window;
            int n = 0;
            for (int i = Projectile.Live.Count - 1; i >= 0; i--)
                if (Catch(Projectile.Live[i], false)) n++;
            if (n > 0) Feedback(facing);
            return n;
        }

        /// <summary>Bullet `p` is inside the arc of the last slash (still within the parry window) -> reflect it.</summary>
        public static bool Catch(Projectile p, bool feedback = true)
        {
            if (p == null || p.Reflected || p.Exploding || Time.time > openUntil) return false;
            Vector2 v = (Vector2)p.transform.position - wPivot;
            if (v.magnitude > wRange + 0.3f) return false;
            if (v.sqrMagnitude > 0.0025f && Vector2.Angle(wFacing, v) > 90f) return false;
            p.Reflect(-p.Direction);
            SpriteVfx.Spawn("hit", p.transform.position, 0.6f, 0, 50, false, 20f);
            if (feedback) Feedback(wFacing);
            return true;
        }

        private static void Feedback(Vector2 facing)
        {
            CameraFollow.KickMain(facing, 0.05f, 0.15f);
            var d = MapEncounterDirector.Instance;
            if (d != null && d.Flags != null && !d.Flags.Has("tut_parry_done"))
            {
                d.Flags.Set("tut_parry_done");
                Hints.Show("Turned! A bolt struck back flies true - and hits twice as hard.", 3f);
            }
        }
    }
}
