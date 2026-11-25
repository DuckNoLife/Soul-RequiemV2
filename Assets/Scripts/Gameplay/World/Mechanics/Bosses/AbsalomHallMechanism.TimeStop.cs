using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// THE STILL HOUR — Absalom's 5 s TIME STOP: a bullet-READING puzzle, not an ambush:
    ///   1) 1 s warning: he raises a hand, bell + clock-face sound, the floor seal flares; existing fire zones / black holes / lightning rods fade;
    ///   2) 5 s freeze: greyscale screen (TimeStop.Grey), Ren / enemies / old bullets frozen (Ren invulnerable), Absalom moves around placing still bullets
    ///      with arrow TRAILS; the last 3 seconds count 3-2-1 WHILE STILL FROZEN;
    ///   3) time out = EVERYTHING RESUMES AT ONCE — colour returns, Ren moves, bullets fly, enemies resume.
    /// Fairness rules: no bullet within 1.0 u of Ren, always ≥ 2 walkable gaps, at most once per 25 s, no other skill runs during the stop.
    /// Layout:
    ///   • Absalom FLIES (fly animation + afterimage) around Ren planting big 135 px LIGHT LANCES (abs_still_lance) in mid-air, tips pointing at Ren,
    ///     each appearing with a flash + light; a solid GOLD LINE links each lance -> Ren's spot (readable even in greyscale).
    ///     StopPattern.Ring (lamp phase): one ring, radius 2.3 (10 lances, one gap 2 lances wide).
    ///     StopPattern.Daggers (Ctrl+F7 showcase): adds a second ring, radius 3.0, offset half a slot (same gap).
    ///   • 3 GOLD SIGILS under Ren + both sides: 0.55 s after time resumes a LIGHT PILLAR falls (forces Ren to move).
    ///   • Time out: lances fly at 8.5 u/s, thaw only 0.12 s (so walking alone is not enough). 1 s later: VOLLEY 2 — 5 lances (Daggers: 6) drop straight
    ///     down from the top edge of the hall in columns (0.6 s warning lines, one column left empty).
    /// Called by: RunLampPhase (.Lamps.cs, from 2 broken lamps) and the Ctrl+F7 showcase. Calls: TimeStop (global freeze), HallBullet.held / Release.
    /// </summary>
    public sealed partial class AbsalomHallMechanism
    {
        private enum StopPattern { Ring, Daggers }
        private struct StopShot { public Vector2 pos, vel; public Color tint; public float scale; }
        private float lastStop = -99f, greyW;
        private Coroutine greyCo;

        private IEnumerator TimeStopRoutine(StopPattern pat)
        {
            // 1) warning
            abs.Face(Vector2.down);
            abs.Play("strike", 6f, false);
            Sfx.Play("bell_toll", abs.Pos, 1f);
            Sfx.Play("dial_turn", abs.Pos, 0.9f);
            SpriteVfx.Spawn("duo_aura_gilded", abs.Pos + Vector2.up * 0.55f, 1.8f, Layer, Order - 1, false, 8f, false);
            abs.Flash(Gold, 1f);
            Bark("Be still.", 1.4f);
            SealGlow(1f);
            StopRoutines();                                                           // no other skill runs in parallel during the time stop
            ClearHazards();
            yield return Wait(1f);
            if (aborted) yield break;
            // 2) freeze
            TimeStop.Begin();
            FreezeRen(true);
            GreyTo(1f, 0.3f);
            DrillBoard.Finish("TIME STANDS STILL", Gold);
            FirstTimeHints.Show("tut_timestop", "Time is frozen. Read where every light will fly - when the colour returns, everything moves at once.", 6f);
            Sfx.Play("bell_toll", A.center, 0.6f);
            stopCenter = PlayerFeet;
            var plan = PlanStop(pat, stopCenter);
            // 3 sigils under Ren (+ both sides) — light pillars fall 0.55 s after time resumes
            var sigils = new List<Vector2> { stopCenter };
            for (int k = 0; k < 2; k++) sigils.Add(InArena(stopCenter + Rot(Vector2.right, Random.Range(0f, 360f)) * Random.Range(0.8f, 1.2f), 0.4f));
            foreach (var sp in sigils) { var m = RingMark(sp, 0.5f, 0.05f, new Color(1f, 0.85f, 0.4f, 0.9f), 0f); stopLines.Add(m); }
            var placed = new List<HallBullet>();
            const float Dur = 5f;
            float t0 = Time.time;
            int idx = 0, shown = -1;
            abs.Play("walk", 10f);                                                    // flying -> fly animation + afterimage
            while (!aborted && Time.time - t0 < Dur)
            {
                float u = Mathf.Clamp01((Time.time - t0) / (Dur - 0.8f));
                int want = Mathf.Min(plan.Count, Mathf.CeilToInt(u * plan.Count));
                while (idx < want)
                {
                    var s = plan[idx++];
                    // big self-lit light lance, flashes on appearing; solid gold line shows its flight path
                    var b = BigShot("abs_still_lance", s.pos, s.vel, Sun, 1f, 11f, 0.16f, 3f, "abs_ice_shatter", 1f, new Color(1f, 0.9f, 0.55f));
                    b.held = true;
                    b.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(s.vel.y, s.vel.x) * Mathf.Rad2Deg);
                    placed.Add(b);
                    var line = MakeLine("Still Hour Path");
                    line.sortingOrder = Order - 1; line.enabled = true;
                    line.color = new Color(1f, 0.85f, 0.4f, 0.55f);
                    Stretch(line, s.pos, s.pos + s.vel.normalized * Mathf.Min(6f, (stopCenter - s.pos).magnitude + 1.2f), 0.035f);
                    stopLines.Add(line);
                    Fx("abs_arcane_hit", s.pos, 0.8f, Order + 4, 20f, new Color(1f, 0.92f, 0.6f));
                    Sfx.Play("hymn_pop", s.pos, 0.35f);
                }
                // Absalom moves around Ren to the bullet just placed (only he can move)
                if (idx > 0)
                {
                    Vector2 go = plan[idx - 1].pos + Vector2.down * 0.15f;
                    abs.Face(go - abs.Pos);
                    abs.Pos = Vector2.MoveTowards(abs.Pos, go, 6f * Time.deltaTime);
                }
                int left = Mathf.CeilToInt(Dur - (Time.time - t0));
                if (left != shown)
                {
                    shown = left;
                    DrillBoard.Command = left <= 3 ? left.ToString() : "TIME STOPPED";                 // 3-2-1 counted while still frozen
                    DrillBoard.CommandTime = Time.time;
                    Sfx.Play(left <= 3 ? "footstep" : "dial_turn", A.center, left <= 3 ? 0.8f : 0.5f);
                }
                yield return null;
            }
            if (aborted) yield break;
            // 3) time resumes — EVERYTHING AT ONCE (Ren, enemies, bullets)
            Sfx.Play("teleport_dark", abs.Pos, 0.5f);
            abs.Pos = RandomDaisPoint();                                              // he is already back in place (blink)
            abs.Play("idle", 5f);
            abs.Face(PlayerFeet - abs.Pos);
            TimeStop.End();
            FreezeRen(false);
            GreyTo(0f, 0.45f);
            SealGlow(0f);
            foreach (var b in placed) if (b != null) b.Release(0.12f);                 // thaw 0.12 s — lances launch almost immediately
            DrillBoard.Command = "";
            Sfx.Play("shockwave", PlayerFeet, 0.7f);
            Sfx.Play("mantra_eyeray", stopCenter, 0.8f);
            Shake(2);
            FlashLight(stopCenter, Sun, 4f, 2f, 0.4f);
            ClearStopLines();
            var fall = SpriteVfx.Load("abs_sunfall");
            foreach (var sp in sigils)
            {
                var g = GroundStrike.Spawn(sp, 0.55f, 0.5f, 11f, fall, 1, 16f, Order + 1, Gold);
                if (g != null) { g.impactSfx = "bolt_strike"; g.bodyScale = 0.5f / 0.62f; }
                Ctx.Run(SunfallBurst(sp, 0.55f + 1f / 16f, 0.5f));
            }
            lastStop = Time.time;
            yield return Wait(1.0f);
            if (aborted) yield break;
            yield return LanceRainColumns(pat == StopPattern.Daggers ? 6 : 5);
            yield return Wait(0.6f);
        }

        private Vector2 stopCenter;
        private readonly List<SpriteRenderer> stopLines = new List<SpriteRenderer>();

        private void ClearStopLines()
        {
            foreach (var l in stopLines) if (l != null) Object.Destroy(l.gameObject);
            stopLines.Clear();
        }

        /// <summary>Still Hour VOLLEY 2 — `n` lances drop straight from the top edge of the hall in evenly spaced columns (one empty column near Ren), 0.6 s warning lines.</summary>
        private IEnumerator LanceRainColumns(int n)
        {
            float w = A.width - 0.8f, step = w / n;
            int gap = Mathf.Clamp(Mathf.FloorToInt((PlayerFeet.x - (A.xMin + 0.4f)) / step) + (Random.value < 0.5f ? -1 : 1), 0, n - 1);
            var cols = new List<Vector2>();
            var tels = new List<DashTelegraph>();
            for (int i = 0; i < n; i++)
            {
                if (i == gap) continue;
                Vector2 top = new Vector2(A.xMin + 0.4f + step * (i + 0.5f), A.yMax - 0.1f);
                cols.Add(top);
                tels.Add(DashTelegraph.Spawn(top, Vector2.down, A.height, 0.3f, Sun, 0.6f, Layer, Order - 2));
            }
            Sfx.Play("bell_toll", A.center, 0.6f);
            yield return Wait(0.6f);
            foreach (var t in tels) if (t != null) t.Release();
            if (aborted) yield break;
            foreach (var top in cols)
            {
                BigShot("abs_still_lance", top, Vector2.down * 9f, Sun, 1f, 11f, 0.16f, 1.2f, "abs_ice_shatter", 1f, new Color(1f, 0.9f, 0.55f));
                Muzzle(top, Vector2.down, Sun, 1.4f);
            }
            Shake(1);
        }

        /// <summary>Place bullets: one ring (Ring) or two rings (Daggers) around Ren, pointing at the centre; skip bullets that land in walls / outside the hall.</summary>
        private List<StopShot> PlanStop(StopPattern pat, Vector2 c)
        {
            var list = new List<StopShot>();
            int n = 10;                                                               // 10 big lances per ring, one gap 2 lances wide
            int g1 = Random.Range(0, n), g2 = g1;
            float[] radii = pat == StopPattern.Ring ? new[] { 2.3f } : new[] { 2.3f, 3.0f };
            for (int ring = 0; ring < radii.Length; ring++)
                for (int i = 0; i < n; i++)
                {
                    if (i == g1 || i == (g1 + 1) % n || i == g2 || i == (g2 + 1) % n) continue;     // 2 khe
                    float ang = (i + (ring == 1 ? 0.5f : 0f)) * 360f / n;
                    if (ring == 1 && (i == (g1 + n - 1) % n || i == (g2 + n - 1) % n)) continue;     // outer ring keeps the gap aligned
                    Vector2 d = new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad) * 0.85f);
                    Vector2 p = c + d * radii[ring];
                    if ((p - c).magnitude < 1.0f || !Free(p)) continue;
                    var s = new StopShot
                    {
                        pos = p, vel = (c - p).normalized * (8.5f + ring * 0.8f),
                        tint = pat == StopPattern.Ring ? Sun : new Color(1f, 0.97f, 0.85f), scale = pat == StopPattern.Ring ? 0.55f : 0.5f,
                    };
                    list.Add(s);
                }
            return list;
        }

        /// <summary>Point is inside the hall and not blocked by solids (pillars, walls).</summary>
        private bool Free(Vector2 p)
        {
            if (!new Rect(A.xMin + 0.15f, A.yMin + 0.1f, A.width - 0.3f, A.height - 0.25f).Contains(p)) return false;
            foreach (var col in Physics2D.OverlapCircleAll(p, 0.08f))
                if (col != null && !col.isTrigger && (Ctx.Player == null || !col.transform.IsChildOf(Ctx.Player))) return false;
            return true;
        }

        /// <summary>Lock / unlock Ren while time is stopped (frozen + invulnerable).</summary>
        private void FreezeRen(bool on)
        {
            if (frozenRen == on || Ctx.Player == null) return;
            frozenRen = on;
            GameInput.Blocked = on;
            Ctx.SetPlayerControl(!on);
            if (Ctx.PlayerHealth != null) Ctx.PlayerHealth.Invulnerable = on;
        }

        private void GreyTo(float target, float seconds)
        {
            if (greyCo != null) Ctx.Host.StopCoroutine(greyCo);
            greyCo = Ctx.Run(GreyRoutine(target, seconds));
        }

        private IEnumerator GreyRoutine(float target, float seconds)
        {
            float from = greyW;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                greyW = Mathf.Lerp(from, target, t / seconds);
                TimeStop.Grey(greyW);
                yield return null;
            }
            greyW = target;
            TimeStop.Grey(greyW);
            greyCo = null;
        }

        /// <summary>Return everything to normal (death / phase cleared during the time stop).</summary>
        private void RestoreStop()
        {
            if (TimeStop.Active) TimeStop.End();
            ClearStopLines();
            if (greyCo != null) { Ctx.Host.StopCoroutine(greyCo); greyCo = null; }
            greyW = 0f;
            TimeStop.Grey(0f);
            FreezeRen(false);
            DrillBoard.Command = "";
            SealGlow(0f);
        }
    }
}
