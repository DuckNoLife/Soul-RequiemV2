using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>Older Registrar moves inside The Margin (Ledger Maws, Ink Pots, Erasure, Written Backwards). Phase flow and picker: .Phases.cs.</summary>
    public sealed partial class RegistrarSkills
    {
        private class Puddle { public Vector2 at; public float until; public SpriteRenderer r; }
        private readonly List<Puddle> puddles = new List<Puddle>();
        private readonly List<Rect> erased = new List<Rect>();
        private float nextBurn;

        private int ActNo => chapter / 3;
        /// <summary>Telegraph time shrinks with the act (act 1 easy to read, act 3 harsh).</summary>
        private float Warn(float s) => s * (ActNo == 0 ? 1f : ActNo == 1 ? 0.86f : 0.74f);

        private IEnumerator DelayThen(float s, IEnumerator then) { yield return W(s); if (!Halt) yield return then; }

        // ================================================================== floor: slowing ink pools, erased margins burn
        private void LateUpdate()
        {
            if (!engaged || Halt || Frozen || gripping || pHp == null) return;
            Vector2 f = PlayerFeet;
            for (int i = puddles.Count - 1; i >= 0; i--)
            {
                var p = puddles[i];
                if (p.r == null) { puddles.RemoveAt(i); continue; }
                float left = p.until - Time.time;
                if (left <= 0f) { Destroy(p.r.gameObject); puddles.RemoveAt(i); continue; }
                if (left < 0.6f) { var c = p.r.color; c.a = left / 0.6f; p.r.color = c; }
                Vector2 d = f - p.at;
                if ((d.x * d.x) / (0.34f * 0.34f) + (d.y * d.y) / (0.22f * 0.22f) < 1f) pHp.Chill(0.5f, 0.25f);
            }
            if (Time.time < nextBurn) return;
            nextBurn = Time.time + 0.2f;
            for (int i = 0; i < erased.Count; i++)                      // slipped into the void (collider miss) -> put back on the edge, no damage
            {
                var r = erased[i];
                if (!r.Contains(f)) continue;
                int side = Mathf.Abs(r.center.x - page.xMin) < 0.9f && r.height > r.width ? 0 : Mathf.Abs(r.center.x - page.xMax) < 0.9f && r.height > r.width ? 1 : 2;
                PushOutOf(r, side, false);
                break;
            }
        }

        // ================================================================== positions
        /// <summary>Point on the page edge: 0 left, 1 right, 2 bottom, 3 top of the playable area. k = 0..1 along that edge.</summary>
        private Vector2 EdgeSpot(int side, float k)
        {
            k = Mathf.Clamp01(k);
            switch (side)
            {
                case 0: return new Vector2(page.xMin + 0.12f, Mathf.Lerp(play.yMin + 0.35f, play.yMax - 0.3f, k));
                case 1: return new Vector2(page.xMax - 0.12f, Mathf.Lerp(play.yMin + 0.35f, play.yMax - 0.3f, k));
                case 2: return new Vector2(Mathf.Lerp(page.xMin + 0.5f, page.xMax - 0.5f, k), page.yMin + 0.2f);
                default: return new Vector2(Mathf.Lerp(page.xMin + 0.5f, page.xMax - 0.5f, k), play.yMax + 0.1f);
            }
        }

        private float LaneY(int k, int n) => Mathf.Lerp(play.yMin, play.yMax, (k + 0.5f) / n);
        private float NearestLane(int n)
        {
            int k = Mathf.Clamp(Mathf.FloorToInt((PlayerFeet.y - play.yMin) / Mathf.Max(0.01f, play.height) * n), 0, n - 1);
            return LaneY(k, n);
        }

        private Vector2 Aimed(Vector2 origin) => (PlayerBody - origin).normalized;

        /// <summary>Beam length from o along d to the end of the page (+ a little into the void).</summary>
        private float RayLen(Vector2 o, Vector2 d)
        {
            var r = new Rect(page.xMin - 0.5f, page.yMin - 0.5f, page.width + 1f, page.height + 1.4f);
            float tx = d.x > 0.0001f ? (r.xMax - o.x) / d.x : d.x < -0.0001f ? (r.xMin - o.x) / d.x : 99f;
            float ty = d.y > 0.0001f ? (r.yMax - o.y) / d.y : d.y < -0.0001f ? (r.yMin - o.y) / d.y : 99f;
            return Mathf.Clamp(Mathf.Min(tx, ty), 0.5f, 14f);
        }

        private static void PlaceBeam(SpriteRenderer r, Vector2 a, Vector2 dir, float len, float width)
        {
            if (r == null) return;
            r.transform.position = new Vector3(a.x, a.y, 0f);
            r.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            r.transform.localScale = new Vector3(len / 0.04f, Mathf.Max(0.01f, width) / 0.24f, 1f);
        }

        private Light2D Glow(Transform parent, Color c, float radius, float intensity)
        {
            var go = new GameObject("glow");
            go.transform.SetParent(parent, false);
            var l = go.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point;
            l.color = c; l.intensity = intensity;
            l.pointLightOuterRadius = radius; l.pointLightInnerRadius = radius * 0.2f;
            return l;
        }

        // ================================================================== LEDGER MAWS — books open their maws and spit violet beams
        /// <param name="origins">book mouths (beam origins)</param>
        /// <param name="aim">fire direction per origin — re-evaluated until 65% of the telegraph, then LOCKED</param>
        private IEnumerator MawVolley(Vector2[] origins, System.Func<Vector2, Vector2> aim, float warn, float fire = 0.42f, float width = 0.36f)
        {
            int n = origins.Length;
            var book = new SpriteRenderer[n]; var line = new SpriteRenderer[n]; var ray = new SpriteRenderer[n];
            var dir = new Vector2[n];
            Sprite closed = MarginArt.Prop("margin_tome"), open = MarginArt.Prop(Random.value < 0.5f ? "margin_tome_open" : "margin_tome_open2");
            busyAnim = true;
            ai.PlayState("Attack");
            for (int i = 0; i < n; i++)
            {
                book[i] = Make("maw", closed, origins[i] - new Vector2(0f, 0.2f), 6);
                book[i].transform.localScale = Vector3.zero;
                SpriteVfx.Spawn("m3_vespers", origins[i], 0.55f, Layer, pOrder + 7, false, 18f);
                dir[i] = aim(origins[i]);
            }
            Sfx.Play("teleport_dark", origins[0], 0.5f);
            // appear (bounce)
            for (float t = 0f; t < 0.22f && !Halt; t += Dt)
            {
                float k = t / 0.22f, s = k < 0.7f ? k / 0.7f * 1.15f : Mathf.Lerp(1.15f, 1f, (k - 0.7f) / 0.3f);
                foreach (var b in book) if (b != null) b.transform.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            // open maw + warning line (tracks Ren, then locks)
            Sfx.Play("reg_tome_snap", origins[0], 0.8f);
            Sfx.Play("eye_beam_charge", origins[0], 0.55f);
            for (int i = 0; i < n; i++)
            {
                if (book[i] == null) continue;
                book[i].sprite = open;
                book[i].transform.localScale = Vector3.one;
                line[i] = Make("maw warn", MarginArt.Beam(), origins[i], 5);
                Glow(book[i].transform, MarginArt.Violet, 1.1f, 1.1f).transform.localPosition = new Vector3(0f, 0.2f, 0f);
            }
            for (float t = 0f; t < warn && !Halt; t += Dt)
            {
                bool track = t < warn * 0.65f;
                float a = track ? 0.32f : (Mathf.Repeat(t * 14f, 1f) < 0.5f ? 0.85f : 0.4f);
                for (int i = 0; i < n; i++)
                {
                    if (line[i] == null) continue;
                    if (track) dir[i] = aim(origins[i]);
                    PlaceBeam(line[i], origins[i], dir[i], RayLen(origins[i], dir[i]), track ? 0.05f : 0.08f);
                    line[i].color = new Color(0.8f, 0.55f, 1f, a);
                    if (book[i] != null) book[i].transform.position = origins[i] - new Vector2(0f, 0.2f) + Vector2.up * Mathf.Sin(t * 9f + i) * 0.02f;
                }
                yield return null;
            }
            foreach (var l in line) if (l != null) Destroy(l.gameObject);
            if (Halt) yield break;
            // fire
            Sfx.Play("eye_beam", origins[0], 0.85f);
            Kick(-dir[0], 0.04f, 0.18f);
            for (int i = 0; i < n; i++) ray[i] = Make("maw beam", MarginArt.Beam(), origins[i], 8);
            bool hit = false;
            for (float t = 0f; t < fire + 0.15f && !Halt; t += Dt)
            {
                float k = t < 0.06f ? t / 0.06f : t > fire ? 1f - (t - fire) / 0.15f : 1f + Mathf.Sin(t * 50f) * 0.08f;
                float w = width * Mathf.Clamp01(k);
                for (int i = 0; i < n; i++)
                {
                    float len = RayLen(origins[i], dir[i]);
                    PlaceBeam(ray[i], origins[i], dir[i], len, w);
                    if (!hit && t < fire && SegDist(PlayerBody, origins[i], origins[i] + dir[i] * len) < width * 0.5f + 0.08f)
                        hit = HurtPlayer(beamDamage, Vector2.Perpendicular(dir[i]) * Mathf.Sign(Vector2.Dot(PlayerBody - origins[i], Vector2.Perpendicular(dir[i]))), 2.5f);
                }
                yield return null;
            }
            foreach (var r in ray) if (r != null) Destroy(r.gameObject);
            // close the book, fade
            for (int i = 0; i < n; i++) if (book[i] != null) book[i].sprite = closed;
            for (float t = 0f; t < 0.25f && !Halt; t += Dt)
            {
                foreach (var b in book) if (b != null) b.color = new Color(1f, 1f, 1f, 1f - t / 0.25f);
                yield return null;
            }
            foreach (var b in book) if (b != null) Destroy(b.gameObject);
            ai.PlayState("Idle");
            busyAnim = false;
        }

        /// <summary>HORIZONTAL lane beams from the left / right edge: 4 lanes, fire 3, leave 1 free (random).</summary>
        private IEnumerator LanesH(float warn)
        {
            int free = Random.Range(0, 4);
            bool fromLeft = Random.value < 0.5f;
            var o = new List<Vector2>();
            for (int k = 0; k < 4; k++) if (k != free) o.Add(new Vector2(fromLeft ? page.xMin + 0.12f : page.xMax - 0.12f, LaneY(k, 4)));
            Vector2 d = fromLeft ? Vector2.right : Vector2.left;
            yield return MawVolley(o.ToArray(), _ => d, warn, 0.45f, 0.42f);
        }

        /// <summary>VERTICAL lane beams from the bottom edge: 6 lanes, fire 4, leave 2 adjacent lanes free.</summary>
        private IEnumerator LanesV(float warn)
        {
            int free = Random.Range(0, 5);
            var o = new List<Vector2>();
            for (int k = 0; k < 6; k++)
                if (k != free && k != free + 1) o.Add(new Vector2(Mathf.Lerp(safe.xMin, safe.xMax, (k + 0.5f) / 6f), page.yMin + 0.2f));
            yield return MawVolley(o.ToArray(), _ => Vector2.up, warn, 0.45f, 0.42f);
        }

        /// <summary>SWEEP: one book on the top edge sweeps a beam down the whole page — no cover, Ren must ROLL through it.</summary>
        private IEnumerator Sweep(bool leftToRight, bool teach)
        {
            if (teach)
            {
                taughtSweep = true;
                Hints.Show("Nowhere to hide from this one. ROLL [" + GameInput.Label(GameAction.Roll) + "] straight through the beam!", 5f);
                yield return W(1.2f);
            }
            Vector2 o = new Vector2(Mathf.Clamp(PlayerFeet.x, page.xMin + 1f, page.xMax - 1f) * 0.3f + page.center.x * 0.7f, play.yMax + 0.15f);
            float a0 = leftToRight ? -165f : -15f, a1 = leftToRight ? -15f : -165f;
            var book = Make("sweep maw", MarginArt.Prop("margin_tome_open"), o - new Vector2(0f, 0.2f), 6);
            SpriteVfx.Spawn("m3_vespers", o, 0.6f, Layer, pOrder + 7, false, 18f);
            Glow(book.transform, MarginArt.Violet, 1.4f, 1.2f);
            var line = Make("sweep warn", MarginArt.Beam(), o, 5);
            busyAnim = true; ai.PlayState("Attack");
            Sfx.Play("reg_tome_snap", o, 0.9f);
            Sfx.Play("eye_beam_charge", o, 0.6f);
            float warn = Warn(1.1f);
            for (float t = 0f; t < warn && !Halt; t += Dt)
            {
                Vector2 d = Dir(a0);
                PlaceBeam(line, o, d, RayLen(o, d), 0.07f);
                line.color = new Color(0.8f, 0.55f, 1f, Mathf.Repeat(t * 10f, 1f) < 0.5f ? 0.8f : 0.35f);
                yield return null;
            }
            if (line != null) Destroy(line.gameObject);
            if (Halt) yield break;
            var ray = Make("sweep beam", MarginArt.Beam(), o, 8);
            Sfx.Play("eye_beam", o, 0.9f);
            float dur = ActNo >= 2 ? 2.1f : 2.5f;
            bool hit = false;
            for (float t = 0f; t < dur && !Halt; t += Dt)
            {
                float u = t / dur;
                Vector2 d = Dir(Mathf.Lerp(a0, a1, u * u * (3f - 2f * u)));
                float len = RayLen(o, d);
                PlaceBeam(ray, o, d, len, 0.3f * (1f + Mathf.Sin(t * 45f) * 0.08f));
                if (!hit && SegDist(PlayerBody, o, o + d * len) < 0.15f + 0.08f)
                    hit = HurtPlayer(beamDamage, Vector2.Perpendicular(d) * (leftToRight ? -1f : 1f), 3f);
                yield return null;
            }
            if (ray != null) Destroy(ray.gameObject);
            if (book != null) Destroy(book.gameObject);
            ai.PlayState("Idle"); busyAnim = false;
        }

        // ================================================================== INK POTS — lobbed ink pots burst into slowing puddles
        private IEnumerator InkRain(int count, float interval)
        {
            busyAnim = true; ai.PlayState("Attack");
            for (int i = 0; i < count && !Halt; i++)
            {
                Vector2 target = i == 0 || Random.value < 0.4f ? ClampSafe(PlayerFeet + Random.insideUnitCircle * 0.25f) : RandomIn(safe, 0.3f);
                RunBg(InkPot(target, 0.95f));
                yield return W(interval);
            }
            ai.PlayState("Idle"); busyAnim = false;
        }

        private IEnumerator InkPot(Vector2 target, float flight)
        {
            var mark = Make("pot mark", MarginArt.Ring(), target, -4);
            mark.color = new Color(0.75f, 0.5f, 1f, 0.6f);
            var shadow = Make("pot shadow", DrillChalk.Shadow(), target, -5);
            var pot = Make("ink pot", MarginArt.Prop("margin_inkpot"), Hand, 9);
            Vector2 from = Hand;
            Sfx.Play("drill_whoosh", from, 0.5f);
            for (float t = 0f; t < flight && !Halt; t += Dt)
            {
                float u = t / flight;
                Vector2 g = Vector2.Lerp(from, target, u);
                pot.transform.position = (Vector3)(g + Vector2.up * (Mathf.Sin(u * Mathf.PI) * 1.4f));
                pot.transform.rotation = Quaternion.Euler(0f, 0f, t * 540f);
                float k = 0.35f + 0.65f * u;
                shadow.transform.localScale = new Vector3(k, k, 1f);
                mark.transform.localScale = Vector3.one * (1.1f - 0.25f * u + Mathf.Sin(t * 20f) * 0.04f);
                mark.color = new Color(0.75f, 0.5f, 1f, 0.35f + 0.5f * u);
                yield return null;
            }
            if (pot != null) Destroy(pot.gameObject);
            if (shadow != null) Destroy(shadow.gameObject);
            if (mark != null) Destroy(mark.gameObject);
            if (Halt) yield break;
            Sfx.Play("glass_break", target, 0.75f);
            Sfx.Play("ink_splash", target, 0.8f);
            var fx = SpriteVfx.Spawn("reg_ink_splash", target, 0.8f, Layer, pOrder + 3, false, 18f);
            if ((PlayerFeet - target).magnitude < 0.38f) HurtPlayer(potDamage, (PlayerFeet - target).normalized, 2f);
            var r = Make("ink puddle", MarginArt.Prop("margin_puddle"), target - new Vector2(0f, 0.2f), -6);
            puddles.Add(new Puddle { at = target, until = Time.time + 4f, r = r });
        }

        // ================================================================== ERASURE — TEAR OFF A PAGE MARGIN
        /// <summary>
        /// Telegraph: the margin strip blinks + a CRACK runs along the edge about to tear -> TEAR: the strip breaks off and drifts into the void,
        /// that area becomes void, the remaining edge gets a TORN EDGE (ragged paper fibres + abyss shadow) and a wall (BoxCollider2D) so Ren
        /// stands at the edge and cannot walk off. Ren still inside the strip when it tears: knocked up onto the edge (takes `eraseDamage`).
        /// Page turn -> margins come back (everything under root is cleared).
        /// </summary>
        /// <param name="side">0 left, 1 right, 2 bottom</param>
        private IEnumerator Erase(int side, float thick, float warn)
        {
            Rect band = side == 0 ? new Rect(page.xMin - 0.3f, page.yMin - 0.3f, thick + 0.3f, play.yMax - page.yMin + 0.3f)
                      : side == 1 ? new Rect(page.xMax - thick, page.yMin - 0.3f, thick + 0.3f, play.yMax - page.yMin + 0.3f)
                      : new Rect(page.xMin - 0.3f, page.yMin - 0.3f, page.width + 0.6f, thick + 0.3f);
            // torn edge: the line between what remains and what was torn off.
            // The torn strip is 0.3 u wider than the page to cover the edge fully, but the TORN EDGE only runs along remaining paper —
            // it never sticks out into the void or crosses a margin torn earlier (e.g. tearing the bottom after the left / right).
            float px0 = page.xMin, px1 = page.xMax, py0 = page.yMin;
            foreach (var cut in erased)
            {
                if (cut.width > cut.height) py0 = Mathf.Max(py0, cut.yMax);              // bottom margin already torn
                else if (cut.center.x < page.center.x) px0 = Mathf.Max(px0, cut.xMax);  // left margin
                else px1 = Mathf.Min(px1, cut.xMin);                                      // right margin
            }
            Vector2 e0 = side == 0 ? new Vector2(band.xMax, py0) : side == 1 ? new Vector2(band.xMin, py0) : new Vector2(px0, band.yMax);
            Vector2 e1 = side == 2 ? new Vector2(px1, band.yMax) : new Vector2(e0.x, band.yMax);
            Rect paperCut = side == 0 ? Rect.MinMaxRect(page.xMin, py0, band.xMax, band.yMax)       // the actual paper that is torn off
                          : side == 1 ? Rect.MinMaxRect(band.xMin, py0, page.xMax, band.yMax)
                          : Rect.MinMaxRect(px0, page.yMin, px1, band.yMax);
            Vector2 outward = side == 0 ? Vector2.left : side == 1 ? Vector2.right : Vector2.down;
            var flash = Make("tear warn", MarginArt.Pixel(), band.center, -4);
            flash.transform.localScale = new Vector3(band.width * 100f, band.height * 100f, 1f);
            var crack = Make("tear crack", MarginArt.Pixel(), e0, -3);
            Sfx.Play("reg_erase", band.center, 0.85f);
            float len = (e1 - e0).magnitude;
            for (float t = 0f; t < warn && !Halt; t += Dt)
            {
                float u = Mathf.Clamp01(t / (warn * 0.85f));
                flash.color = new Color(0.7f, 0.45f, 1f, Mathf.Repeat(t * 8f, 1f) < 0.5f ? 0.28f : 0.1f);
                // crack running along the edge (slightly jagged)
                Vector2 mid = Vector2.Lerp(e0, e1, u * 0.5f);
                crack.transform.position = (Vector3)(mid + outward * (Mathf.Sin(t * 30f) * 0.008f));
                crack.transform.localScale = side == 2 ? new Vector3(len * u * 100f, 2.5f, 1f) : new Vector3(2.5f, len * u * 100f, 1f);
                crack.color = new Color(0.05f, 0.02f, 0.09f, 0.95f);
                yield return null;
            }
            if (flash != null) Destroy(flash.gameObject);
            if (crack != null) Destroy(crack.gameObject);
            if (Halt) yield break;
            // TEAR
            Sfx.Play("cs_page", band.center, 1f);
            Sfx.Play("reg_erase", band.center, 0.7f);
            Kick(outward, 0.05f, 0.2f);
            var hole = Make("tear hole", MarginArt.Pixel(), band.center, -9);
            hole.transform.localScale = new Vector3(band.width * 100f, band.height * 100f, 1f);
            hole.color = new Color(0.02f, 0.015f, 0.04f, 0.96f);
            var piece = MakeLit("torn piece", MarginArt.Pixel(), paperCut.center, -8);
            piece.transform.localScale = new Vector3(paperCut.width * 100f, paperCut.height * 100f, 1f);
            piece.color = new Color(0.4f, 0.35f, 0.3f, 1f);
            var lip = MakeLit("torn piece lip", MarginArt.TornEdge(Mathf.CeilToInt(len * 100f), 373 + side), Vector2.zero, -7);
            var edge = MakeLit("torn edge", MarginArt.TornEdge(Mathf.CeilToInt(len * 100f), 911 + side), Vector2.zero, -6);
            float rot = side == 0 ? -90f : side == 1 ? 90f : 0f;                // paper fibres hanging toward the void
            Vector2 edgeMid = (e0 + e1) * 0.5f;
            edge.transform.position = edgeMid; edge.transform.rotation = Quaternion.Euler(0f, 0f, rot);
            lip.transform.rotation = Quaternion.Euler(0f, 0f, rot + 180f);  // torn edge of the loose strip (facing inward)
            // block the abyss edge
            var wall = new GameObject("tear wall");
            if (root != null) wall.transform.SetParent(root, false);
            wall.transform.position = band.center;
            var box = wall.AddComponent<BoxCollider2D>();
            box.size = band.size;
            erased.Add(band);
            if (side == 0) safe.xMin = Mathf.Max(safe.xMin, band.xMax + 0.05f);
            else if (side == 1) safe.xMax = Mathf.Min(safe.xMax, band.xMin - 0.05f);
            else safe.yMin = Mathf.Max(safe.yMin, band.yMax + 0.05f);
            PushOutOf(band, side, true);
            // the loose strip drifts into the void, turning slightly, fading
            for (float t = 0f; t < 0.9f && !Halt; t += Dt)
            {
                float u = t / 0.9f;
                Vector2 off = outward * (u * u * 0.9f) + Vector2.down * (u * 0.25f);
                float a = 1f - u;
                if (piece != null)
                {
                    piece.transform.position = (Vector3)(paperCut.center + off);
                    piece.transform.rotation = Quaternion.Euler(0f, 0f, (side == 1 ? -1f : 1f) * u * 9f);
                    piece.color = new Color(0.4f * a + 0.05f, 0.35f * a + 0.04f, 0.3f * a + 0.06f, a);
                }
                if (lip != null)
                {
                    lip.transform.position = (Vector3)(edgeMid + off);
                    lip.color = new Color(1f, 1f, 1f, a);
                }
                yield return null;
            }
            if (piece != null) Destroy(piece.gameObject);
            if (lip != null) Destroy(lip.gameObject);
        }

        /// <summary>Ren inside a torn margin -> placed back on the page edge (never falls). hurt = right at the tear (takes damage).</summary>
        private void PushOutOf(Rect band, int side, bool hurt)
        {
            Vector2 f = PlayerFeet;
            if (!band.Contains(f) || ai == null || ai.Player == null) return;
            Vector2 to = side == 0 ? new Vector2(band.xMax + 0.16f, f.y) : side == 1 ? new Vector2(band.xMin - 0.16f, f.y) : new Vector2(f.x, band.yMax + 0.16f);
            var rb = ai.Player.GetComponent<Rigidbody2D>();
            if (rb != null) { rb.position = to; rb.linearVelocity = Vector2.zero; }
            ai.Player.position = new Vector3(to.x, to.y, ai.Player.position.z);
            SpriteVfx.Spawn("m3_dash_dust", to, 1f, Layer, pOrder - 1, false, 16f, false);
            if (hurt)
            {
                Vector2 inward = side == 0 ? Vector2.right : side == 1 ? Vector2.left : Vector2.up;
                HurtPlayer(eraseDamage, inward, 2f, true);
            }
        }

        // ================================================================== WRITTEN BACKWARDS — inverted movement keys (GameInput.InvertedUntil)
        private IEnumerator Backwards(float seconds)
        {
            DrillBoard.Finish("REVISED", new Color(1f, 0.55f, 0.75f));
            Hints.Show("Your steps are written backwards! Every direction is reversed.", Mathf.Min(seconds, 4f));
            Sfx.Play("registrar_quill", PlayerFeet, 0.8f);
            SpriteVfx.Spawn("rf_confuse_ring", PlayerFeet, 0.9f, Layer, pOrder + 4, false, 14f);
            float t = 0f;
            while (t < seconds && !Halt)
            {
                if (!Frozen) { t += Time.deltaTime; GameInput.InvertedUntil = Time.time + 0.15f; }
                yield return null;
            }
            GameInput.InvertedUntil = 0f;
        }
    }
}
