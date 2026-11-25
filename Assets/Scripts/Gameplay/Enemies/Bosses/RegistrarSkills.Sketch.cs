using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// The Registrar's DRAW / WRITE moves.
    ///   Summon    — he SKETCHES 2 monsters on the page (the pen line drawing in = warning of where they appear); finished sketches become REAL
    ///               enemies from the asset pack: Skeleton (Crypt, Map 3 prefab reused) and Mage_Skeleton (Old Prison).
    ///               Phase I: 2 skeletons, II: skeleton + mage, III: 2 mages. Still alive at phase end -> he strikes them out (ink cross) and they melt.
    ///   QuillStab — he writes on the page; a giant quill hovers above Ren (top-down: higher = shifted up the screen; the SHADOW + RING on the
    ///               floor = where it will land), follows Ren slower than running speed, then LOCKS (red ring flash) -> lifts for momentum ->
    ///               stabs through the paper. 3 stabs, not fast; in phase III each stab adds a second quill at a random spot. Leaves a hole (paper_hole).
    /// Art: Resources/VFX/props/ink/summon_skeleton_sketch_0..5, summon_mage_sketch_0..5, quill_drop, paper_hole (_tools/s447/bake447.py).
    /// Calls: MapEncounterDirector.SpawnSummon (real enemies). Prefabs built by Editor/SoulRequiem/Map4EnemyBuilder.
    /// </summary>
    public sealed partial class RegistrarSkills
    {
        private const int SketchFrames = 6;
        private static readonly Color PaperBit = new Color(0.9f, 0.85f, 0.72f);
        private static bool taughtStab;
        private readonly List<EnemyAI> summons = new List<EnemyAI>();

        // ================================================================== basics
        private static Sprite InkArt(string name) => MarginArt.Prop("ink/" + name);
        private static Sprite SketchArt(string name, int f) => MarginArt.Prop("ink/" + name + "_sketch_" + Mathf.Clamp(f, 0, SketchFrames - 1));

        /// <summary>Place the child sprite so the parent's origin = the FEET, with the feet at `pivotFrac` of the frame height from the bottom (independent of the .meta pivot).</summary>
        private static void PlaceFeet(SpriteRenderer r, float pivotFrac)
        {
            if (r == null || r.sprite == null) return;
            Bounds b = r.sprite.bounds;
            float sx = Mathf.Abs(r.transform.localScale.x), sy = Mathf.Abs(r.transform.localScale.y);
            r.transform.localPosition = new Vector3(-b.center.x * sx, -(b.min.y + b.size.y * pivotFrac) * sy, 0f);
        }

        /// <summary>Hand-drawn "line boil": every 0.12 s the sprite squashes / stretches slightly.</summary>
        private static void Boil(SpriteRenderer body, ref float timer, float dt, float baseScale = 1f)
        {
            if (body == null) return;
            timer -= dt;
            if (timer > 0f) return;
            timer = 0.12f;
            body.transform.localScale = new Vector3(baseScale * Random.Range(0.97f, 1.03f), baseScale * Random.Range(0.97f, 1.03f), 1f);
        }

        /// <summary>Draw in front of / behind Ren by feet position (feet lower than Ren = in front).</summary>
        private void DepthSort(SpriteRenderer body, float footY)
        {
            if (body != null) body.sortingOrder = pOrder + (footY < PlayerFeet.y ? 1 : -1);
        }

        /// <summary>Random point in the playable area at least `minDist` from Ren (14 tries; otherwise the farthest point found).</summary>
        private Vector2 FarSpot(float minDist)
        {
            Vector2 best = RandomIn(safe, 0.3f);
            float bestD = -1f;
            for (int i = 0; i < 14; i++)
            {
                Vector2 p = RandomIn(safe, 0.3f);
                float d = (p - PlayerFeet).magnitude;
                if (d >= minDist) return p;
                if (d > bestD) { bestD = d; best = p; }
            }
            return best;
        }

        /// <summary>Thin streak (stretched white pixel) from a to b.</summary>
        private SpriteRenderer Streak(Vector2 a, Vector2 b, float width, Color c, int order)
        {
            var r = Make("ink streak", MarginArt.Pixel(), (a + b) * 0.5f, order);
            Vector2 d = b - a;
            r.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            r.transform.localScale = new Vector3(d.magnitude * 100f, width * 100f, 1f);
            r.color = c;
            return r;
        }

        private static Vector2 Rotate(Vector2 v, float deg)
        {
            float r = deg * Mathf.Deg2Rad, cs = Mathf.Cos(r), sn = Mathf.Sin(r);
            return new Vector2(v.x * cs - v.y * sn, v.x * sn + v.y * cs);
        }

        /// <summary>One pen stroke drawn from a to b over `draw` seconds (after `delay`), held `hold` seconds, then faded. Lives under root (gone on page turn).</summary>
        private IEnumerator Stroke(Vector2 a, Vector2 b, float width, Color c, int order, float delay, float draw, float hold)
        {
            yield return W(delay);
            if (Halt) yield break;
            var r = Streak(a, a, width, c, order);
            Vector2 d = b - a;
            float len = d.magnitude;
            r.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            for (float t = 0f; t < draw && !Halt && r != null; t += Dt)
            {
                float u = t / draw;
                r.transform.position = (Vector3)(a + d * (u * 0.5f));
                r.transform.localScale = new Vector3(len * u * 100f, width * 100f, 1f);
                yield return null;
            }
            if (r == null) yield break;
            r.transform.position = (Vector3)((a + b) * 0.5f);
            r.transform.localScale = new Vector3(len * 100f, width * 100f, 1f);
            yield return W(hold);
            for (float t = 0f; t < 0.3f && !Halt && r != null; t += Dt)
            {
                r.color = new Color(c.r, c.g, c.b, c.a * (1f - t / 0.3f));
                yield return null;
            }
            if (r != null) Destroy(r.gameObject);
        }

        // ================================================================== SUMMON — sketch 2 monsters, finished sketches become real
        private int AliveSummons()
        {
            summons.RemoveAll(e => e == null || e.Health == null || e.Health.IsDead);
            return summons.Count;
        }

        /// <summary>Sketch 2 monsters (a, b) at two spots far from Ren and from each other; the second starts 0.3 s later.</summary>
        private IEnumerator Summon(string a, string b)
        {
            if (MapEncounterDirector.Instance == null) yield break;
            busyAnim = true;
            ai.PlayState("Attack");
            Sfx.Play("registrar_quill", Pos, 0.8f);
            Vector2 p0 = FarSpot(1.5f), p1 = FarSpot(1.5f);
            for (int i = 0; i < 10 && (p1 - p0).magnitude < 1.1f; i++) p1 = FarSpot(1.5f);
            float draw = Warn(1.6f);
            yield return Par(DrawSummon(a, p0, draw), DelayThen(0.3f, DrawSummon(b, p1, draw)));
            if (ai != null) ai.PlayState("Idle");
            busyAnim = false;
        }

        /// <summary>
        /// The sketch draws in (6 frames, 75 % of the time), blinks for the rest (about to become real) with a violet ring under its feet, then an ink
        /// splash -> MapEncounterDirector.SpawnSummon (real enemy, charges Ren at once). Sketch size / feet match the real enemy so the finished
        /// drawing lines up with it.
        /// </summary>
        private IEnumerator DrawSummon(string key, Vector2 at, float seconds)
        {
            bool mage = key == "Mage_Skeleton";
            string art = mage ? "summon_mage" : "summon_skeleton";
            float pivot = mage ? 0.327f : 0.273f, scale = mage ? 1.15f : 1.3f;   // matches the prefab's pivotY / scale (Map4EnemyBuilder / Map3EnemyBuilder)
            var holder = new GameObject("summon sketch");
            if (root != null) holder.transform.SetParent(root, false);
            holder.transform.position = at;
            var body = Make("sketch art", SketchArt(art, 0), at, 0);
            body.transform.SetParent(holder.transform, false);
            body.transform.localScale = new Vector3(scale, scale, 1f);
            PlaceFeet(body, pivot);
            var ring = Make("summon mark", MarginArt.Ring(), at, -5);
            ring.transform.SetParent(holder.transform, true);
            ring.transform.localScale = new Vector3(1.5f, 0.8f, 1f);
            SpriteVfx.Spawn("m3_vespers", at + Vector2.up * 0.25f, 0.6f, Layer, pOrder + 2, false, 18f);
            float boil = 0f;
            bool rifted = false;
            for (float t = 0f; t < seconds && !Halt && body != null; t += Dt)
            {
                float k = t / Mathf.Max(0.01f, seconds);
                var s = SketchArt(art, Mathf.FloorToInt(Mathf.Clamp01(k / 0.75f) * (SketchFrames - 1) + 0.0001f));
                if (s != null) body.sprite = s;
                body.color = new Color(1f, 1f, 1f, k > 0.75f && Mathf.Repeat(t * 12f, 1f) < 0.5f ? 0.45f : 1f);
                Boil(body, ref boil, Dt, scale);
                PlaceFeet(body, pivot);
                DepthSort(body, at.y);
                if (ring != null) ring.color = new Color(0.75f, 0.5f, 1f, 0.25f + 0.4f * k);
                if (!rifted && seconds - t <= 0.65f)                                // PixelLab ink rift
                {
                    rifted = true;
                    SpriteVfx.Spawn("reg_ink_rift", at, 0.9f, Layer, pOrder - 2, false, 10f, false);
                }
                yield return null;
            }
            if (holder != null) Destroy(holder);
            if (Halt || collapsed || !phaseRunning) yield break;
            var fx = SpriteVfx.Spawn("reg_ink_splash", at + Vector2.up * 0.15f, 0.7f, Layer, pOrder + 2, false, 20f, false);
            Sfx.Play("ink_splash", at, 0.6f);
            var dir = MapEncounterDirector.Instance;
            var e = dir != null ? dir.SpawnSummon(key, at, ai) : null;
            if (e != null) summons.Add(e);
            else Debug.LogWarning("[Registrar] Could not summon '" + key + "' — run Build Map 4 Enemies, then Build Map 4 Margin Scene.");
        }

        /// <summary>Phase end: he strikes out every surviving monster (3 diagonal ink strokes) and they melt.</summary>
        private IEnumerator ScribbleOut()
        {
            if (AliveSummons() == 0) yield break;
            Sfx.Play("registrar_quill", Pos, 0.8f);
            foreach (var e in summons)
            {
                if (e == null) continue;
                Vector2 at = (Vector2)e.transform.position + Vector2.up * 0.1f;
                for (int i = 0; i < 3; i++)
                    StartCoroutine(Stroke(at + new Vector2(-0.3f, 0.02f + i * 0.13f), at + new Vector2(0.3f, 0.16f + i * 0.13f), 0.035f,
                                          new Color(0.18f, 0.1f, 0.3f, 0.95f), 8, i * 0.1f, 0.1f, 0.35f));
            }
            yield return W(0.5f);
            foreach (var e in summons)
            {
                if (e == null || e.Health == null || e.Health.IsDead) continue;
                var fx = SpriteVfx.Spawn("reg_ink_splash", (Vector2)e.transform.position + Vector2.up * 0.2f, 0.8f, Layer, pOrder + 3, false, 18f, false);
                e.Health.Invulnerable = false;
                e.Health.TakeDamage(99999f);
            }
            summons.Clear();
            yield return W(0.3f);
        }

        // ================================================================== QUILL STAB — a quill stabs through the page
        /// <param name="stabs">number of stabs (3)</param>
        /// <param name="perStab">quills per stab (1; phase III = 2: a second quill at a random spot)</param>
        private IEnumerator QuillStab(int stabs, int perStab)
        {
            busyAnim = true;
            ai.PlayState("Attack");
            Sfx.Play("registrar_quill", Pos, 0.9f);
            if (!taughtStab) { taughtStab = true; Hints.Show("The quill falls where its shadow settles. Keep moving.", 3f); }
            for (int i = 0; i < stabs && !Halt; i++)
            {
                var jobs = new IEnumerator[Mathf.Max(1, perStab)];
                jobs[0] = OneStab(true, Warn(0.75f));                               // faster lock (1.15 -> 0.75 s)
                for (int j = 1; j < jobs.Length; j++) jobs[j] = OneStab(false, Warn(0.75f));
                yield return Par(jobs);
                if (i < stabs - 1) yield return W(0.15f);
            }
            if (ai != null) ai.PlayState("Idle");
            busyAnim = false;
        }

        /// <summary>One quill: follow (slower than Ren runs) -> lock + wind-up -> stab. Pulling back runs in the background so the next stab starts at once.</summary>
        private IEnumerator OneStab(bool onRen, float follow)
        {
            const float H = 2.3f;                                                // hover height (top-down: shifted up the screen)
            // extra quill lands on a RANDOM spot of the page (does not track Ren or stand next to the main quill)
            Vector2 target = onRen ? ClampSafe(PlayerFeet) : RandomIn(safe, 0.35f);
            float chase = part == 0 ? 1.9f : part == 1 ? 2.2f : 2.5f;         // units / second — Ren runs faster, so running always escapes
            var mark = Make("stab mark", MarginArt.Ring(), target, -4);
            mark.color = new Color(0.75f, 0.5f, 1f, 0f);
            var shadow = Make("stab shadow", DrillChalk.Shadow(), target, -5);
            shadow.color = new Color(1f, 1f, 1f, 0f);
            var quill = new GameObject("stab quill");
            if (root != null) quill.transform.SetParent(root, false);
            quill.transform.position = target + Vector2.up * H;
            var qr = Make("quill art", InkArt("quill_drop"), target, 9);
            qr.transform.SetParent(quill.transform, false);
            qr.transform.localScale = new Vector3(1.1f, 1.1f, 1f);
            if (qr.sprite != null)
            {
                Bounds b = qr.sprite.bounds;
                qr.transform.localPosition = new Vector3(-b.center.x * 1.1f, -b.min.y * 1.1f, 0f);   // object origin = quill nib
            }
            qr.color = new Color(1f, 1f, 1f, 0f);

            // 1) follow Ren
            for (float t = 0f; t < follow && !Halt; t += Dt)
            {
                if (mark == null || shadow == null || quill == null) yield break;   // page turned mid-move
                float u = t / follow;
                if (onRen) target = Vector2.MoveTowards(target, ClampSafe(PlayerFeet), chase * Dt);
                mark.transform.position = target;
                mark.transform.localScale = Vector3.one * (1.6f + Mathf.Sin(t * 14f) * 0.06f);
                mark.color = new Color(0.75f, 0.5f, 1f, 0.2f + 0.45f * u);
                shadow.transform.position = target;
                float sk = 0.35f + 0.45f * u;
                shadow.transform.localScale = new Vector3(sk, sk, 1f);
                shadow.color = new Color(1f, 1f, 1f, 0.6f * u);
                quill.transform.position = target + Vector2.up * (H + Mathf.Sin(t * 5f) * 0.06f);
                quill.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 3.5f) * 5f);
                qr.color = new Color(1f, 1f, 1f, Mathf.Clamp01(u * 2.5f));
                yield return null;
            }
            if (Halt || mark == null || quill == null) yield break;

            // 2) lock: red ring flash, the quill lifts higher for momentum
            Sfx.Play("reg_tome_snap", target, 0.5f);
            float lockT = Warn(0.28f);                                           // shorter lock
            for (float t = 0f; t < lockT && !Halt && quill != null; t += Dt)
            {
                float u = t / lockT;
                if (mark != null) mark.color = Mathf.Repeat(t * 10f, 1f) < 0.5f ? new Color(1f, 0.4f, 0.55f, 0.9f) : new Color(1f, 0.9f, 0.95f, 0.9f);
                quill.transform.position = target + Vector2.up * (H + 0.35f * u * (2f - u));
                quill.transform.rotation = Quaternion.identity;
                yield return null;
            }
            if (Halt || quill == null) yield break;

            // 3) stab down
            Sfx.Play("drill_whoosh", target, 0.45f);
            const float Drop = 0.1f;
            for (float t = 0f; t < Drop && !Halt && quill != null; t += Dt)
            {
                float u = t / Drop;
                quill.transform.position = target + Vector2.up * Mathf.Lerp(H + 0.35f, 0f, u * u);
                yield return null;
            }
            if (Halt || quill == null) yield break;
            quill.transform.position = target;
            if (mark != null) Destroy(mark.gameObject);
            if (shadow != null) Destroy(shadow.gameObject);
            DepthSort(qr, target.y - 0.02f);
            Sfx.Play("spear_burst", target, 0.8f);
            Sfx.Play("ink_splash", target, 0.5f);
            Kick(Vector2.down, 0.06f, 0.18f);
            var fx = SpriteVfx.Spawn("reg_ink_splash", target, 0.7f, Layer, pOrder + 3, false, 20f, false);
            SpriteVfx.Spawn("reg_paper_dust", target, 0.7f, Layer, pOrder - 1, false, 14f, false);
            StartCoroutine(PaperHole(target));
            StartCoroutine(Shreds(target));
            Vector2 d = PlayerFeet - target;
            if (d.magnitude < 0.3f) HurtPlayer(stabDamage, d.sqrMagnitude > 0.0001f ? d.normalized : Vector2.down, 3f);
            RunBg(PullOut(quill, qr, target));
            yield return W(0.12f);
        }

        /// <summary>The planted quill shakes for 0.3 s, then pulls out and fades.</summary>
        private IEnumerator PullOut(GameObject quill, SpriteRenderer qr, Vector2 at)
        {
            for (float t = 0f; t < 0.3f && !Halt && quill != null; t += Dt)
            {
                quill.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 60f) * 6f * (1f - t / 0.3f));
                yield return null;
            }
            for (float t = 0f; t < 0.28f && !Halt && quill != null; t += Dt)
            {
                float u = t / 0.28f;
                quill.transform.rotation = Quaternion.identity;
                quill.transform.position = at + Vector2.up * (2.3f * u * u);
                if (qr != null) { qr.color = new Color(1f, 1f, 1f, 1f - u); qr.sortingOrder = pOrder + 9; }
                yield return null;
            }
            if (quill != null) Destroy(quill);
        }

        /// <summary>Hole in the page: pops in, stays 6 s, fades (gone with root on page turn).</summary>
        private IEnumerator PaperHole(Vector2 at)
        {
            var r = Make("paper hole", InkArt("paper_hole"), at, -6);
            for (float t = 0f; t < 0.12f && r != null; t += Dt)
            {
                float k = Mathf.Lerp(1.35f, 1f, t / 0.12f);
                r.transform.localScale = new Vector3(k, k * 0.8f, 1f);
                yield return null;
            }
            if (r != null) r.transform.localScale = new Vector3(1f, 0.8f, 1f);
            for (float t = 0f; t < 6f && !Halt && r != null; t += Dt) yield return null;
            for (float t = 0f; t < 1f && r != null; t += Dt)
            {
                r.color = new Color(1f, 1f, 1f, 1f - t);
                yield return null;
            }
            if (r != null) Destroy(r.gameObject);
        }

        /// <summary>Paper scraps + ink drops burst from the stab point.</summary>
        private IEnumerator Shreds(Vector2 at)
        {
            const int N = 7;
            var bits = new SpriteRenderer[N];
            var vel = new Vector2[N];
            for (int i = 0; i < N; i++)
            {
                bits[i] = Make("paper shred", MarginArt.Pixel(), at + Vector2.up * 0.05f, 4);
                bits[i].color = i % 3 == 0 ? MarginArt.Ink : PaperBit;
                bits[i].transform.localScale = new Vector3(Random.Range(3f, 5f), Random.Range(2f, 3f), 1f);
                vel[i] = Rotate(Vector2.right, Random.Range(0f, 360f)) * Random.Range(0.5f, 1.2f) + Vector2.up * 0.9f;
            }
            const float Life = 0.55f;
            for (float t = 0f; t < Life && !Halt; t += Dt)
            {
                float dt = Dt;
                for (int i = 0; i < N; i++)
                {
                    if (bits[i] == null) continue;
                    vel[i].y -= 4.5f * dt;
                    bits[i].transform.position += (Vector3)(vel[i] * dt);
                    bits[i].transform.Rotate(0f, 0f, 540f * dt);
                    var c = bits[i].color; c.a = t < Life * 0.6f ? 1f : 1f - (t - Life * 0.6f) / (Life * 0.4f); bits[i].color = c;
                }
                yield return null;
            }
            for (int i = 0; i < N; i++) if (bits[i] != null) Destroy(bits[i].gameObject);
        }
    }
}
