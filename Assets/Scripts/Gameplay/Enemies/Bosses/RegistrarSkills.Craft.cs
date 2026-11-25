using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Three "desk tools" moves of The Registrar.
    ///   Cranes  — books open at the left / right / bottom edge and release PAPER CRANES one by one: each crane CHASES Ren briefly (limited turn rate),
    ///             then VEERS off and flies straight away. Cranes are folded by him -> Ren can cut them (InkCreature).
    ///             3 / 3 / 4 cranes per phase, slow, chase only 0.6–0.8 s, turn 90°/s — tuned down because they were too hard to dodge.
    ///             Art: ink/paper_crane_0..7 (PixelLab, flapping loop).
    ///   Saws    — he SKETCHES 2 saw blades (sketch from the "spinning blades" trap of the Old Prison pack); finished sketches become real spinning
    ///             blades, a red aim line points at Ren, then they CHARGE and bounce off the page edge 1 / 2 / 3 times (by phase) and fade.
    ///   Eraser  — an UPRIGHT eraser, its LOWER end pressed on the paper, scrubs back and forth along 3 zig-zag lines through Ren's row;
    ///             pink crumbs fly off (PixelLab VFX reg_eraser_crumbs).
    ///             It erases what HE drew too: summoned monsters die, cranes tear, ink pools vanish, holes close.
    /// </summary>
    public sealed partial class RegistrarSkills
    {
        [Header("Cranes / Saws / Eraser")]
        public float craneDamage = 8f, sawDamage = 12f, eraserDamage = 12f;

        private const int CraneFrames = 8;                    // paper_crane_0..7 (PixelLab animate, seamless flapping loop)
        private static bool taughtCranes, taughtEraser;

        private static Sprite CraneArt(int f) => MarginArt.Prop("ink/paper_crane_" + ((f % CraneFrames) + CraneFrames) % CraneFrames);

        // ================================================================== CRANES — paper cranes chase, then veer off
        private IEnumerator Cranes()
        {
            int books = part >= 2 ? 4 : 3;                              // 3 / 3 / 4 books, one crane each
            busyAnim = true;
            ai.PlayState("Attack");
            Sfx.Play("cs_page", Pos, 0.8f);
            if (!taughtCranes) { taughtCranes = true; Hints.Show("Paper cranes hunt you for a moment, then veer off. Sidestep late — or cut them down.", 3.2f); }
            var spots = new List<Vector2>();
            for (int i = 0; i < books; i++) spots.Add(CraneBookSpot(spots));
            for (int i = 0; i < books && !Halt; i++)
            {
                RunBg(CraneBook(spots[i], Warn(0.8f)));
                yield return W(part == 0 ? 0.95f : part == 1 ? 0.85f : 0.75f);
            }
            if (ai != null) ai.PlayState("Idle");
            busyAnim = false;
        }

        /// <summary>Book spot: left / right / bottom edge of the playable page, ≥ 1.6 u from Ren and ≥ 0.9 u from other books.</summary>
        private Vector2 CraneBookSpot(List<Vector2> taken)
        {
            Vector2 best = new Vector2(safe.xMin + 0.15f, safe.center.y);
            float bestScore = -1f;
            for (int k = 0; k < 20; k++)
            {
                int side = Random.Range(0, 3);
                float u = Random.Range(0.12f, 0.88f);
                Vector2 p = side == 0 ? new Vector2(safe.xMin + 0.15f, Mathf.Lerp(safe.yMin, safe.yMax, u))
                          : side == 1 ? new Vector2(safe.xMax - 0.15f, Mathf.Lerp(safe.yMin, safe.yMax, u))
                          : new Vector2(Mathf.Lerp(safe.xMin, safe.xMax, u), safe.yMin + 0.15f);
                float near = 99f;
                foreach (var q in taken) near = Mathf.Min(near, (q - p).magnitude);
                float score = Mathf.Min((p - PlayerFeet).magnitude, 2.5f) + Mathf.Min(near, 1.5f);
                if ((p - PlayerFeet).magnitude >= 1.6f && near >= 0.9f) return p;
                if (score > bestScore) { bestScore = score; best = p; }
            }
            return best;
        }

        /// <summary>One book: pops out (blinking) -> opens -> releases 1 crane -> closes.</summary>
        private IEnumerator CraneBook(Vector2 at, float warn)
        {
            var bk = Make("crane book", MarginArt.Prop("margin_tome"), at - new Vector2(0f, 0.08f), 5);
            bk.flipX = at.x > safe.center.x;
            bk.transform.localScale = Vector3.zero;
            for (float t = 0f; t < warn && !Halt && bk != null; t += Dt)
            {
                float u = t / warn, pop = Mathf.Min(1f, u * 4f);
                bk.transform.localScale = Vector3.one * pop;
                bk.color = Mathf.Repeat(t * (5f + 8f * u), 1f) < 0.5f ? Color.white : new Color(0.85f, 0.7f, 1f, 1f);
                yield return null;
            }
            if (Halt || bk == null) { if (bk != null) Destroy(bk.gameObject); yield break; }
            bk.sprite = MarginArt.Prop("margin_tome_open");
            bk.color = Color.white;
            SpriteVfx.Spawn("m3_vespers", at + Vector2.up * 0.1f, 0.45f, Layer, pOrder + 6, false, 20f, false);
            Sfx.Play("cs_page", at, 0.6f);
            float speed = part == 0 ? 1.5f : part == 1 ? 1.7f : 1.9f;              // slow on purpose so cranes can be dodged
            RunBg(Crane(at + Vector2.up * 0.15f, speed));
            yield return W(0.6f);
            for (float t = 0f; t < 0.25f && bk != null; t += Dt) { bk.transform.localScale = Vector3.one * (1f - t / 0.25f); yield return null; }
            if (bk != null) Destroy(bk.gameObject);
        }

        /// <summary>
        /// One crane: flies toward Ren turning at most 90°/s for 0.6–0.8 s, then swerves 35–60° to one side and speeds up in a straight line until it
        /// leaves the page (VEER). Has a floor shadow. Touching Ren = damage (the crane tears); cut = tears into paper scraps.
        /// </summary>
        private IEnumerator Crane(Vector2 from, float speed)
        {
            if (Halt) yield break;
            var go = new GameObject("paper crane");
            if (root != null) go.transform.SetParent(root, false);
            go.transform.position = from;
            var body = Make("crane art", CraneArt(0), from, 6);
            body.transform.SetParent(go.transform, false);
            if (body.sprite != null)
            {
                Bounds b = body.sprite.bounds;
                body.transform.localPosition = new Vector3(-b.center.x, -b.center.y, 0f);
            }
            var shadow = Make("crane shadow", DrillChalk.Shadow(), from - new Vector2(0f, 0.3f), -5);
            shadow.color = new Color(1f, 1f, 1f, 0.35f);
            shadow.transform.localScale = new Vector3(0.45f, 0.45f, 1f);
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.16f;
            var cr = go.AddComponent<InkCreature>();
            Vector2 dir = (PlayerBody - from).sqrMagnitude > 0.0001f ? (PlayerBody - from).normalized : Vector2.right;
            float hunt = Random.Range(0.6f, 0.8f), veer = (Random.value < 0.5f ? 1f : -1f) * Random.Range(35f, 60f);
            float t = 0f, nextHit = 0f, sp = speed;
            bool veered = false;
            int frame = Random.Range(0, CraneFrames);
            Rect outR = new Rect(page.xMin - 0.6f, page.yMin - 0.6f, page.width + 1.2f, page.height + 1.2f);
            while (!Halt && go != null && cr.Alive && t < 7f)
            {
                float dt = Dt;
                t += dt;
                if (t < hunt)
                {
                    Vector2 want = (PlayerBody - (Vector2)go.transform.position);
                    if (want.sqrMagnitude > 0.0001f)
                    {
                        float a = Vector2.SignedAngle(dir, want.normalized);
                        dir = Rotate(dir, Mathf.Clamp(a, -90f * dt, 90f * dt));
                    }
                }
                else if (!veered)
                {
                    veered = true;
                    dir = Rotate(dir, veer);
                    Sfx.Play("cs_page", go.transform.position, 0.25f);
                }
                if (veered) sp = Mathf.MoveTowards(sp, speed * 1.4f, 2f * dt);
                Vector2 p = (Vector2)go.transform.position + dir * sp * dt;
                go.transform.position = p;
                if (shadow != null) shadow.transform.position = p - new Vector2(0f, 0.3f);
                // sprite: flipped by horizontal direction, tilted by vertical direction (max 35°)
                if (body != null)
                {
                    body.flipX = dir.x < 0f;
                    float tilt = Mathf.Clamp(Mathf.Atan2(dir.y, Mathf.Abs(dir.x)) * Mathf.Rad2Deg, -35f, 35f);
                    body.transform.localRotation = Quaternion.Euler(0f, 0f, dir.x < 0f ? -tilt : tilt);
                    var s = CraneArt((frame + Mathf.FloorToInt(t * 14f)) % CraneFrames);
                    if (s != null) body.sprite = s;
                    body.color = new Color(1f, 1f, 1f, Mathf.Clamp01(t / 0.2f));
                    DepthSort(body, p.y - 0.3f);
                    body.sortingOrder += 6;
                }
                if (t >= nextHit && (PlayerBody - p).magnitude < 0.2f && HurtPlayer(craneDamage, dir, 2.5f))
                {
                    nextHit = t + 0.6f;
                    cr.Alive = false;                                   // hitting Ren tears the crane immediately
                }
                if (!outR.Contains(p)) break;
                yield return null;
            }
            if (shadow != null) Destroy(shadow.gameObject);
            if (go == null) yield break;
            if (!cr.Alive) PaperBurst(go.transform.position, cr.WasStruck);
            Destroy(go);
        }

        /// <summary>Crane torn: paper scraps + small ink splash.</summary>
        private void PaperBurst(Vector2 at, bool struck)
        {
            StartCoroutine(Shreds(at));
            SpriteVfx.Spawn("reg_ink_splash", at, struck ? 0.55f : 0.4f, Layer, pOrder + 6, false, 22f, false);
            Sfx.Play("cs_page", at, 0.35f);
        }

        // ================================================================== SAWS — sketch 2 saw blades, then launch them at Ren
        private IEnumerator Saws()
        {
            busyAnim = true;
            ai.PlayState("Attack");
            Sfx.Play("registrar_quill", Pos, 0.8f);
            Vector2 p0 = FarSpot(1.6f), p1 = FarSpot(1.6f);
            for (int i = 0; i < 10 && (p1 - p0).magnitude < 1.2f; i++) p1 = FarSpot(1.6f);
            int bounces = part + 1;                                     // 1 / 2 / 3 bounces
            float sketch = Warn(1.3f);
            yield return Par(Saw(p0, sketch, 0f, bounces), Saw(p1, sketch, 0.55f, bounces));
            if (ai != null) ai.PlayState("Idle");
            busyAnim = false;
        }

        /// <summary>Sketch (6 line frames, line boil) -> real spinning blade -> aim line at Ren -> charge, bounce off the remaining page edges -> fade.</summary>
        private IEnumerator Saw(Vector2 at, float sketch, float launchDelay, int bounces)
        {
            var go = new GameObject("saw");
            if (root != null) go.transform.SetParent(root, false);
            go.transform.position = at;
            var body = Make("saw art", SketchArt("saw_blade", 0), at, 5);
            body.transform.SetParent(go.transform, false);
            var shadow = Make("saw shadow", DrillChalk.Shadow(), at - new Vector2(0f, 0.2f), -5);
            shadow.color = new Color(1f, 1f, 1f, 0f);
            shadow.transform.SetParent(go.transform, true);
            Sfx.Play("registrar_quill", at, 0.5f);
            float boil = 0f;
            // 1) sketch
            for (float t = 0f; t < sketch && !Halt && body != null; t += Dt)
            {
                float k = t / sketch;
                var s = SketchArt("saw_blade", Mathf.FloorToInt(Mathf.Clamp01(k / 0.75f) * (SketchFrames - 1) + 0.0001f));
                if (s != null) body.sprite = s;
                body.color = new Color(1f, 1f, 1f, k > 0.75f && Mathf.Repeat(t * 12f, 1f) < 0.5f ? 0.45f : 1f);
                Boil(body, ref boil, Dt, 1f);
                yield return null;
            }
            if (Halt || body == null) yield break;
            // 2) becomes real: spinning, whirring
            body.sprite = InkArt("saw_blade");
            body.color = Color.white;
            body.transform.localScale = Vector3.one;
            if (shadow != null) shadow.color = new Color(1f, 1f, 1f, 0.55f);
            SpriteVfx.Spawn("reg_ink_splash", at, 0.6f, Layer, pOrder + 6, false, 20f, false);
            Sfx.Play("spear_burst", at, 0.5f);
            float spin = 0f, rate = 240f;
            float wait = 0.35f + launchDelay;
            for (float t = 0f; t < wait && !Halt && body != null; t += Dt)
            {
                rate = Mathf.Min(1080f, rate + 1800f * Dt);
                spin += rate * Dt;
                body.transform.localRotation = Quaternion.Euler(0f, 0f, -spin);
                yield return null;
            }
            if (Halt || body == null) yield break;
            // 3) aim line (direction locks at 70 %)
            Vector2 dir = (PlayerFeet - at).sqrMagnitude > 0.0001f ? (PlayerFeet - at).normalized : Vector2.down;
            var aimLine = Streak(at, at + dir * 1.4f, 0.05f, new Color(1f, 0.55f, 0.6f, 0f), -4);
            float aimT = Warn(0.55f);
            for (float t = 0f; t < aimT && !Halt && body != null && aimLine != null; t += Dt)
            {
                if (t < aimT * 0.7f && (PlayerFeet - at).sqrMagnitude > 0.0001f) dir = (PlayerFeet - at).normalized;
                Vector2 b2 = at + dir * 1.4f;
                aimLine.transform.position = (Vector3)((at + b2) * 0.5f);
                aimLine.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                aimLine.color = new Color(1f, 0.55f, 0.6f, 0.25f + 0.5f * (t / aimT) * (Mathf.Repeat(t * 10f, 1f) < 0.5f ? 1f : 0.6f));
                spin += rate * Dt;
                body.transform.localRotation = Quaternion.Euler(0f, 0f, -spin);
                yield return null;
            }
            if (aimLine != null) Destroy(aimLine.gameObject);
            if (Halt || body == null || go == null) yield break;
            // 4) charge + bounce
            Sfx.Play("drill_whoosh", at, 0.6f);
            float speed = part >= 2 ? 3.9f : 3.4f, life = 0f, nextHit = 0f, trail = 0f;
            int left = bounces;
            Vector2 p = at;
            const float R = 0.22f;
            while (!Halt && go != null && body != null && life < 6f)
            {
                float dt = Dt;
                life += dt;
                p += dir * speed * dt;
                bool hitEdge = false;
                if (p.x < safe.xMin + R) { p.x = safe.xMin + R; dir.x = Mathf.Abs(dir.x); hitEdge = true; }
                if (p.x > safe.xMax - R) { p.x = safe.xMax - R; dir.x = -Mathf.Abs(dir.x); hitEdge = true; }
                if (p.y < safe.yMin + R) { p.y = safe.yMin + R; dir.y = Mathf.Abs(dir.y); hitEdge = true; }
                if (p.y > safe.yMax - R) { p.y = safe.yMax - R; dir.y = -Mathf.Abs(dir.y); hitEdge = true; }
                if (hitEdge)
                {
                    left--;
                    Sfx.Play("glass_break", p, 0.25f);
                    SpriteVfx.Spawn("reg_paper_dust", p, 0.5f, Layer, pOrder - 1, false, 16f, false);
                    Kick(-dir, 0.03f, 0.1f);
                    if (left < 0) break;
                }
                go.transform.position = p;
                spin += rate * dt;
                body.transform.localRotation = Quaternion.Euler(0f, 0f, -spin);
                DepthSort(body, p.y - 0.2f);
                trail -= dt;
                if (trail <= 0f) { trail = 0.05f; StartCoroutine(Stroke(p - dir * 0.05f, p - dir * 0.2f, 0.03f, new Color(0.7f, 0.75f, 0.95f, 0.5f), -5, 0f, 0.01f, 0.05f)); }
                if (life >= nextHit && (PlayerBody - p).magnitude < R + 0.06f && HurtPlayer(sawDamage, dir, 3f)) nextHit = life + 0.6f;
                yield return null;
            }
            if (go == null) yield break;
            // 5) dissolve back into a sketch, then gone
            if (shadow != null) Destroy(shadow.gameObject);
            for (float t = 0f; t < 0.4f && !Halt && body != null; t += Dt)
            {
                float u = t / 0.4f;
                var s = SketchArt("saw_blade", Mathf.RoundToInt((1f - u) * (SketchFrames - 1)));
                if (s != null) body.sprite = s;
                body.color = new Color(1f, 1f, 1f, 1f - u * u);
                yield return null;
            }
            if (go != null) Destroy(go);
        }

        // ================================================================== ERASER — upright eraser scrubs zig-zag 3 passes
        private const float EraserHalf = 0.3f;                 // half width of the scrub strip (hit area around the eraser tip)
        private const float EraserScale = 1.6f;

        private IEnumerator EraserPass()
        {
            busyAnim = true;
            ai.PlayState("Attack");
            if (!taughtEraser) { taughtEraser = true; Hints.Show("The eraser wipes away everything he wrote — even his own creatures.", 3.2f); }
            // eraser: object origin = the TIP pressed on the paper; the child sprite is placed so its bottom (worn end) sits on the origin
            var er = new GameObject("eraser");
            if (root != null) er.transform.SetParent(root, false);
            var art = Make("eraser art", InkArt("eraser_up"), Vector2.zero, 7);
            art.transform.SetParent(er.transform, false);
            art.transform.localScale = Vector3.one * EraserScale;
            if (art.sprite != null)
            {
                Bounds b = art.sprite.bounds;
                art.transform.localPosition = new Vector3(-b.center.x * EraserScale, -b.min.y * EraserScale, 0f);
            }
            var shadow = Make("eraser shadow", DrillChalk.Shadow(), Vector2.zero, -5);
            shadow.transform.SetParent(er.transform, false);
            shadow.transform.localScale = new Vector3(0.9f, 0.7f, 1f);
            shadow.color = new Color(1f, 1f, 1f, 0.5f);
            er.SetActive(false);
            bool fromLeft = Random.value < 0.5f;
            for (int i = 0; i < 3 && !Halt && er != null; i++)
            {
                // diagonal lines through Ren's current row, alternating up / down = zig-zag
                float yMid = Mathf.Clamp(PlayerFeet.y, safe.yMin + 0.4f, safe.yMax - 0.4f);
                float tilt = (i % 2 == 0 ? 1f : -1f) * Mathf.Min(0.7f, safe.height * 0.22f);
                Vector2 a = new Vector2(fromLeft ? safe.xMin - 0.3f : safe.xMax + 0.3f, Mathf.Clamp(yMid - tilt, safe.yMin + 0.25f, safe.yMax - 0.25f));
                Vector2 b = new Vector2(fromLeft ? safe.xMax + 0.3f : safe.xMin - 0.3f, Mathf.Clamp(yMid + tilt, safe.yMin + 0.25f, safe.yMax - 0.25f));
                yield return EraserStroke(er.transform, art, a, b, Warn(i == 0 ? 1f : 0.8f));
                fromLeft = !fromLeft;
            }
            if (er != null)
            {
                for (float t = 0f; t < 0.25f && art != null; t += Dt) { art.color = new Color(1f, 1f, 1f, 1f - t / 0.25f); yield return null; }
                if (er != null) Destroy(er);
            }
            if (ai != null) ai.PlayState("Idle");
            busyAnim = false;
        }

        /// <summary>
        /// One pass: a pink dashed warning line (blinking faster and faster), then the eraser drops at the line start and SCRUBS BACK AND FORTH
        /// (0.12 u amplitude, 13 times/s) while sliding along it; the tip throws crumbs (reg_eraser_crumbs). Hit = the tip reaches Ren's feet.
        /// </summary>
        private IEnumerator EraserStroke(Transform er, SpriteRenderer art, Vector2 a, Vector2 b, float warn)
        {
            Vector2 along = (b - a).normalized;
            float len = (b - a).magnitude;
            // warning: pink dashes along the scrub line
            var dashes = new List<SpriteRenderer>();
            for (float s = 0.1f; s < len - 0.1f; s += 0.28f)
                dashes.Add(Streak(a + along * s, a + along * Mathf.Min(len, s + 0.16f), 0.04f, new Color(1f, 0.6f, 0.82f, 0f), -4));
            for (float t = 0f; t < warn && !Halt; t += Dt)
            {
                float u = t / warn, blink = Mathf.Repeat(t * (5f + 9f * u), 1f) < 0.5f ? 1f : 0.45f;
                foreach (var d in dashes) if (d != null) d.color = new Color(1f, 0.6f, 0.82f, (0.3f + 0.6f * u) * blink);
                yield return null;
            }
            foreach (var d in dashes) if (d != null) Destroy(d.gameObject);
            if (Halt || er == null) yield break;
            // drop down
            er.gameObject.SetActive(true);
            er.position = a;
            for (float t = 0f; t < 0.12f && art != null; t += Dt)
            {
                art.transform.localScale = Vector3.one * EraserScale * Mathf.Lerp(1.25f, 1f, t / 0.12f);
                art.color = new Color(1f, 1f, 1f, t / 0.12f);
                yield return null;
            }
            if (art != null) { art.transform.localScale = Vector3.one * EraserScale; art.color = Color.white; }
            Sfx.Play("reg_erase", a, 0.8f);
            float speed = part >= 2 ? 3.6f : 3f, dur = len / speed;
            float crumb = 0f, nextHit = 0f, rub = 0f;
            for (float t = 0f; t < dur && !Halt && er != null; t += Dt)
            {
                float dt = Dt;
                Vector2 tip = Vector2.Lerp(a, b, t / dur);
                rub += dt * 13f * Mathf.PI * 2f;
                float scrub = Mathf.Sin(rub) * 0.12f;                                    // scrub back and forth along the line
                Vector2 pos = tip + along * scrub;
                er.position = pos;
                if (art != null) art.transform.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Cos(rub) * 7f * Mathf.Sign(along.x == 0f ? 1f : along.x));
                crumb -= dt;
                if (crumb <= 0f)
                {
                    crumb = 0.08f;
                    SpriteVfx.Spawn("reg_eraser_crumbs", pos + Random.insideUnitCircle * 0.06f, 1f, Layer, pOrder + 1, Random.value < 0.5f, 16f, false);
                }
                if (t >= nextHit && (PlayerFeet - pos).magnitude < EraserHalf + 0.05f && HurtPlayer(eraserDamage, Rotate(along, 90f), 3.5f))
                    nextHit = t + 0.6f;
                WipeAround(pos, EraserHalf + 0.15f);
                yield return null;
            }
            // lift up
            for (float t = 0f; t < 0.12f && art != null; t += Dt) { art.color = new Color(1f, 1f, 1f, 1f - t / 0.12f); yield return null; }
            if (er != null) er.gameObject.SetActive(false);
            yield return W(0.15f);
        }

        /// <summary>Erase what he drew around p: summoned monsters (die), paper cranes (tear), ink pools, paper holes.</summary>
        private void WipeAround(Vector2 p, float r)
        {
            for (int i = summons.Count - 1; i >= 0; i--)
            {
                var e = summons[i];
                if (e == null || e.Health == null || e.Health.IsDead) continue;
                if (((Vector2)e.transform.position - p).magnitude > r + 0.1f) continue;
                var fx = SpriteVfx.Spawn("reg_ink_splash", (Vector2)e.transform.position + Vector2.up * 0.2f, 0.8f, Layer, pOrder + 3, false, 18f, false);
                if (fx != null) fx.Tint(new Color(1f, 0.7f, 0.85f));
                e.Health.Invulnerable = false;
                e.Health.TakeDamage(99999f);
            }
            for (int i = puddles.Count - 1; i >= 0; i--)
            {
                var pd = puddles[i];
                if (pd.r == null || (pd.at - p).magnitude > r + 0.2f) continue;
                Destroy(pd.r.gameObject);
                puddles.RemoveAt(i);
            }
            if (root == null) return;
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                var c = root.GetChild(i);
                if (((Vector2)c.position - p).magnitude > r) continue;
                if (c.name == "paper hole") { Destroy(c.gameObject); continue; }
                var cr = c.GetComponent<InkCreature>();
                if (cr != null && cr.Alive) cr.Alive = false;          // paper cranes tear themselves inside their own loop
            }
        }
    }
}
