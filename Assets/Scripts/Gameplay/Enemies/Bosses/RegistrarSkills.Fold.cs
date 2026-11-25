using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// The Registrar's "paper" move.
    ///   Page Fold — (from phase II) a vertical / horizontal crease appears (offset toward Ren); the half WITH Ren darkens (shadow of the other half about to
    ///               fold over) and the far edge of the other half curls up; then that half flips over the crease and SLAMS onto the dark half:
    ///               still standing there = `foldDamage` (rolling does not avoid it). Stays 0.3 s, unfolds, leaves a faint crease mark.
    ///               Phase III folds TWICE (vertical then horizontal).
    /// </summary>
    public sealed partial class RegistrarSkills
    {
        [Header("Page Fold")]
        public float foldDamage = 18f;

        private static readonly Color PaperFace = new Color(0.93f, 0.89f, 0.78f);
        private static readonly Color PaperBack = new Color(0.8f, 0.74f, 0.7f);
        private static bool taughtFold;

        /// <summary>Solid rectangle (1 px sprite) covering exactly `r`.</summary>
        private SpriteRenderer Box(string name, Rect r, Color c, int order, bool lit = false)
        {
            var s = lit ? MakeLit(name, MarginArt.Pixel(), r.center, order) : Make(name, MarginArt.Pixel(), r.center, order);
            SetBox(s, r);
            s.color = c;
            return s;
        }

        private static void SetBox(SpriteRenderer s, Rect r)
        {
            if (s == null) return;
            s.transform.position = new Vector3(r.center.x, r.center.y, 0f);
            s.transform.localScale = new Vector3(Mathf.Max(0.0001f, r.width) * 100f, Mathf.Max(0.0001f, r.height) * 100f, 1f);
        }

        // ================================================================== PAGE FOLD
        private IEnumerator PageFolds()
        {
            busyAnim = true;
            ai.PlayState("Attack");
            Sfx.Play("cs_page", Pos, 0.8f);
            if (!taughtFold) { taughtFold = true; Hints.Show("He is folding the page. Get off the darkening half!", 3f); }
            bool vertical = Random.value < 0.6f;
            yield return Fold(vertical, Warn(1.5f));
            if (part >= 2 && !Halt)
            {
                yield return W(0.25f);
                yield return Fold(!vertical, Warn(1.25f));
            }
            if (ai != null) ai.PlayState("Idle");
            busyAnim = false;
        }

        /// <summary>
        /// One fold. vertical = VERTICAL crease (folds left / right). Half B = the half with Ren (gets covered), half A = the half that flips over.
        /// The crease sits between the page centre and Ren (40 %) -> half A is always longer than half B and covers it completely.
        /// </summary>
        private IEnumerator Fold(bool vertical, float warn)
        {
            Rect r = safe;
            float lo = vertical ? r.xMin : r.yMin, hi = vertical ? r.xMax : r.yMax;
            float me = vertical ? PlayerFeet.x : PlayerFeet.y, mid = (lo + hi) * 0.5f;
            float crease = Mathf.Clamp(Mathf.Lerp(mid, me, 0.4f), lo + 0.6f, hi - 0.6f);
            float sB = me > crease ? 1f : me < crease ? -1f : (Random.value < 0.5f ? 1f : -1f);
            float lenB = sB > 0f ? hi - crease : crease - lo, lenA = sB > 0f ? crease - lo : hi - crease;
            Rect Span(float a, float b) => vertical ? Rect.MinMaxRect(Mathf.Min(a, b), r.yMin, Mathf.Max(a, b), r.yMax)
                                                    : Rect.MinMaxRect(r.xMin, Mathf.Min(a, b), r.xMax, Mathf.Max(a, b));
            Rect dangerR = Span(crease, crease + sB * lenB);
            Rect flapHome = Span(crease, crease - sB * lenA);

            // telegraph: crease blinks, half B darkens, the far edge of half A brightens (curling up)
            Vector2 c0 = vertical ? new Vector2(crease, r.yMin) : new Vector2(r.xMin, crease);
            Vector2 c1 = vertical ? new Vector2(crease, r.yMax) : new Vector2(r.xMax, crease);
            var line = Streak(c0, c1, 0.035f, new Color(0.75f, 0.5f, 1f, 0f), -3);
            var shade = Box("fold shade", dangerR, new Color(0.05f, 0.03f, 0.1f, 0f), -4);
            float farA = crease - sB * lenA;
            Vector2 e0 = vertical ? new Vector2(farA, r.yMin) : new Vector2(r.xMin, farA);
            Vector2 e1 = vertical ? new Vector2(farA, r.yMax) : new Vector2(r.xMax, farA);
            var lip = Streak(e0, e1, 0.06f, new Color(1f, 0.97f, 0.88f, 0f), -3);
            Sfx.Play("reg_tome_snap", (c0 + c1) * 0.5f, 0.5f);
            for (float t = 0f; t < warn && !Halt; t += Dt)
            {
                float u = t / warn, blink = Mathf.Repeat(t * (6f + 8f * u), 1f) < 0.5f ? 1f : 0.55f;
                if (line != null) line.color = new Color(0.75f, 0.5f, 1f, (0.35f + 0.55f * u) * blink);
                if (shade != null) shade.color = new Color(0.05f, 0.03f, 0.1f, 0.38f * u * u);
                if (lip != null) lip.color = new Color(1f, 0.97f, 0.88f, 0.5f * u);
                yield return null;
            }
            if (lip != null) Destroy(lip.gameObject);
            if (Halt) { if (line != null) Destroy(line.gameObject); if (shade != null) Destroy(shade.gameObject); yield break; }

            // flip: θ 0 -> π. Free edge at crease - sB * lenA * cos θ; under half A a darker "back page" shows
            var under = Box("fold under", flapHome, new Color(0.02f, 0.01f, 0.05f, 0f), -5);
            var flap = Box("fold flap", flapHome, PaperFace, 8, true);
            Sfx.Play("drill_whoosh", (c0 + c1) * 0.5f, 0.6f);
            const float Flip = 0.42f;
            for (float t = 0f; t < Flip && !Halt && flap != null; t += Dt)
            {
                float u = t / Flip, th = Mathf.PI * u * u;
                float edge = crease - sB * lenA * Mathf.Cos(th);
                SetBox(flap, Span(crease, edge));
                bool back = th > Mathf.PI * 0.5f;
                float lightK = 0.75f + 0.25f * Mathf.Abs(Mathf.Cos(th));             // darker midway (standing upright)
                Color face = back ? PaperBack : PaperFace;
                flap.color = new Color(face.r * lightK, face.g * lightK, face.b * lightK, 1f);
                if (under != null) under.color = new Color(0.02f, 0.01f, 0.05f, 0.45f * Mathf.Min(1f, u * 2f));
                if (shade != null) shade.color = new Color(0.05f, 0.03f, 0.1f, 0.38f + 0.3f * u);
                yield return null;
            }
            if (Halt || flap == null) { CleanFold(line, shade, under, flap); yield break; }

            // slam down
            SetBox(flap, dangerR);
            flap.color = PaperBack;
            if (shade != null) Destroy(shade.gameObject);
            Sfx.Play("heavy_land", dangerR.center, 0.9f);
            Sfx.Play("cs_page", dangerR.center, 1f);
            Kick(vertical ? new Vector2(sB, 0f) : new Vector2(0f, sB), 0.1f, 0.3f);
            for (int i = 0; i < 5; i++)
            {
                float k = (i + 0.5f) / 5f;
                Vector2 at = vertical ? new Vector2(crease + sB * lenB * 0.5f, Mathf.Lerp(r.yMin, r.yMax, k))
                                      : new Vector2(Mathf.Lerp(r.xMin, r.xMax, k), crease + sB * lenB * 0.5f);
                SpriteVfx.Spawn("reg_paper_dust", at + Random.insideUnitCircle * 0.15f, 1f, Layer, pOrder + 9, false, 14f, false);
            }
            Rect hitR = new Rect(dangerR.xMin + 0.04f, dangerR.yMin + 0.04f, dangerR.width - 0.08f, dangerR.height - 0.08f);
            if (hitR.Contains(PlayerFeet))
            {
                Vector2 push = vertical ? new Vector2(-sB, 0f) : new Vector2(0f, -sB);
                HurtPlayer(foldDamage, push, 4f, true);
            }
            yield return W(0.3f);

            // unfold
            for (float t = 0f; t < 0.35f && !Halt && flap != null; t += Dt)
            {
                float u = t / 0.35f, th = Mathf.PI * (1f - u);
                float edge = crease - sB * lenA * Mathf.Cos(th);
                SetBox(flap, Span(crease, edge));
                Color face = th > Mathf.PI * 0.5f ? PaperBack : PaperFace;
                flap.color = new Color(face.r, face.g, face.b, 1f - u * u);
                if (under != null) under.color = new Color(0.02f, 0.01f, 0.05f, 0.45f * (1f - u));
                yield return null;
            }
            if (flap != null) Destroy(flap.gameObject);
            if (under != null) Destroy(under.gameObject);
            if (line != null) line.color = new Color(0.45f, 0.35f, 0.6f, 0.35f);          // crease mark stays until the page turns
        }

        private static void CleanFold(params SpriteRenderer[] rs)
        {
            foreach (var s in rs) if (s != null) Destroy(s.gameObject);
        }
    }
}
