using System.Collections;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    ///   Tease          — a move in the pool (every phase): he melts into ink, reappears mid-page ≥ 1.8 u from Ren, says a short taunt on the HUD board,
    ///                    GLIDES to 2 other spots (leaving ink strokes), then melts back to the lectern. While he is mid-page, getting close = thrown.
    ///   ProximityGrab  — replaces the old knock-away: Ren inside `repelRadius` around his chest -> violet sigil flashes underfoot for 0.22 s
    ///                    (roll now = escape), then Ren is LIFTED + THROWN to the page edge (Toss(false), `tossDamage` on impact). 2.5 s cooldown.
    ///                    Off once he has collapsed (so Ren can finish him).
    /// </summary>
    public sealed partial class RegistrarSkills
    {
        [Header("Tease / touching him")]
        public float tossDamage = 8f;

        private static readonly string[] TeaseLines = { "Over here.", "Missed me?", "Tag. You're it.", "Too slow, Candidate.", "Closer... closer..." };
        private bool teasing;

        // ================================================================== get close -> lifted and thrown
        private void ProximityGrab()
        {
            if (collapsed || Halt || gripping || tossing || Frozen || pHp == null || Time.time < nextRepel) return;
            Vector2 c = Pos + Vector2.up * 0.2f;
            if ((PlayerPos - c).magnitude > repelRadius) return;
            nextRepel = Time.time + 2.5f;
            StartCoroutine(GrabToss());
        }

        private IEnumerator GrabToss()
        {
            if (ai != null && !busyAnim) ai.PlayState("Attack");
            Sfx.Play("reg_grip", PlayerFeet, 0.9f);
            var sig = Make("touch sigil", MarginArt.Sigil(), PlayerFeet, -3);
            for (float t = 0f; t < 0.22f && !Halt && sig != null; t += Dt)
            {
                sig.transform.position = PlayerFeet;
                float s = Mathf.Lerp(1.15f, 0.75f, t / 0.22f);
                sig.transform.localScale = new Vector3(s, s * 0.62f, 1f);
                sig.color = Mathf.Repeat(t * 20f, 1f) < 0.5f ? Color.white : new Color(0.85f, 0.6f, 1f, 1f);
                yield return null;
            }
            if (sig != null) Destroy(sig.gameObject);
            if (Halt || collapsed || RollProtected) { DrillBoard.Finish("SLIPPED FREE!", new Color(0.85f, 1f, 0.8f)); yield break; }
            if (Random.value < 0.5f) DrillBoard.Yell(Pick("No touching the author.", "Back to your page.", "Shoo."), 1.6f);
            yield return Toss(false);
            if (ai != null && !busyAnim) ai.PlayState("Idle");
        }

        // ================================================================== TEASE — wander around taunting Ren
        private IEnumerator Tease()
        {
            if (Halt || teasing) yield break;
            teasing = true;
            try
            {
                busyAnim = true;
                // 1) melt into ink, appear mid-page
                yield return InkHop(TeaseSpot(1.8f), true);
                if (Halt) yield break;
                DrillBoard.Yell(TeaseLines[Random.Range(0, TeaseLines.Length)], 1.8f);
                ai.PlayState("Idle");
                ai.FaceX((PlayerPos - Pos).x);
                yield return W(1.1f);
                // 2) glide to 2 other spots
                for (int i = 0; i < 2 && !Halt; i++)
                {
                    yield return Glide(TeaseSpot(1.4f), 0.55f);
                    ai.FaceX((PlayerPos - Pos).x);
                    yield return W(0.8f);
                }
                // 3) back to the lectern
                if (!Halt) yield return InkHop(home, false);
            }
            finally
            {
                teasing = false;
                busyAnim = false;
                if (ai != null) ai.PlayState("Idle");
            }
        }

        /// <summary>Taunt spot: inside the playable page, at least `minDist` from Ren, not hugging an edge.</summary>
        private Vector2 TeaseSpot(float minDist)
        {
            Rect r = new Rect(safe.xMin + 0.6f, safe.yMin + 0.4f, safe.width - 1.2f, safe.height - 0.9f);
            Vector2 best = r.center;
            float bestD = -1f;
            for (int i = 0; i < 16; i++)
            {
                Vector2 p = RandomIn(r, 0f);
                float d = (p - PlayerFeet).magnitude;
                if (d >= minDist && (p - Pos).magnitude > 0.8f) return p;
                if (d > bestD) { bestD = d; best = p; }
            }
            return best;
        }

        /// <summary>Melt into ink and reappear at `to` (ink splash + ink rift where he appears).</summary>
        private IEnumerator InkHop(Vector2 to, bool rift)
        {
            SpriteVfx.Spawn("m3_vespers", Pos + Vector2.up * 0.6f, 1.1f, Layer, Order + 1, false, 16f);
            Sfx.Play("teleport_dark", Pos, 0.7f);
            yield return Fade(0f, 0.25f);
            if (Halt) { yield return Fade(1f, 0.1f); yield break; }
            ai.TeleportTo(to);
            if (rift) SpriteVfx.Spawn("reg_ink_rift", to, 1.2f, Layer, Order - 1, false, 12f, false);
            SpriteVfx.Spawn("reg_ink_splash", to, 1.1f, Layer, Order + 1, false, 16f, false);
            yield return Fade(1f, 0.2f);
        }

        /// <summary>Glide smoothly to `to` in `dur` seconds, leaving faint ink strokes behind.</summary>
        private IEnumerator Glide(Vector2 to, float dur)
        {
            Vector2 from = Pos;
            Sfx.Play("drill_whoosh", from, 0.5f);
            ai.FaceX((to - from).x);
            float trail = 0f;
            for (float t = 0f; t < dur && !Halt; t += Dt)
            {
                float u = t / dur, e = u * u * (3f - 2f * u);
                Vector2 p = Vector2.Lerp(from, to, e) + Vector2.up * Mathf.Sin(u * Mathf.PI) * 0.12f;
                ai.TeleportTo(p);
                trail -= Dt;
                if (trail <= 0f)
                {
                    trail = 0.05f;
                    StartCoroutine(Stroke(p + Vector2.up * 0.05f, p + Vector2.up * 0.05f - (to - from).normalized * 0.25f, 0.05f,
                                          new Color(0.35f, 0.2f, 0.55f, 0.6f), -2, 0f, 0.02f, 0.15f));
                }
                yield return null;
            }
            if (!Halt) ai.TeleportTo(to);
        }
    }
}
