using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// crate_puzzle - PUSH CRATES ONTO PRESSURE PLATES.
    /// Object area = tile grid (matching Tiled tiles). `layout` = tile rows from the TOP down, separated by '/', one character per tile:
    ///   '#' fixed shelf / crate stack (image + collider built automatically)   '_' floor   '-' floor that crates CANNOT enter (pen entrance)
    ///   '$' crate   '.' pressure plate   '*' crate already on a plate   'o' outside the grid (ignored)
    /// Pushing: walk straight into a crate (along an axis) for ~0.18 s -> it slides 1 tile if the next tile is free (no shelf, no crate, no physics obstacle).
    /// No pulling (Sokoban): pushing into a corner gets it stuck -> pull lever `reset` (lever `fires` = signal name) to return the crates.
    /// A plate with a crate = glowing gold rune + small light; every plate covered -> flag `sets` (default tally_done), crates lock in place.
    /// The solution is checked with a BFS solver (_tools/m4drill_1120/s360/sok.py) before placing it on the map.
    /// </summary>
    public sealed class CratePuzzleMechanism : Mechanism
    {
        private sealed class Crate
        {
            public Vector2Int Cell, Start;
            public SpriteRenderer Sr;
            public Rigidbody2D Body;
            public BoxCollider2D Box;                     // collider box (used for "Ren pressing against the crate face" with real physics)
            public bool Moving;
            public float Shake;
        }

        private sealed class Plate
        {
            public Vector2Int Cell;
            public SpriteRenderer Sr;
            public Light2D Glow;
            public bool Covered;
        }

        private int cols, rows;
        private float T;
        private char[,] grid;
        private readonly List<Crate> crates = new List<Crate>();
        private readonly List<Plate> plates = new List<Plate>();
        private Sprite[] plateFrames;
        private TrapListener reset;
        private Crate leaning;
        private Vector2Int leanDir;
        private float leanTime, nextThunk, nextHint;
        private bool hinted;

        private string Sets => Str("sets", "tally_done");

        public override void Init()
        {
            var lines = Str("layout", "").Split('/');
            rows = lines.Length;
            cols = 0;
            foreach (var l in lines) cols = Mathf.Max(cols, l.Length);
            if (rows == 0 || cols == 0) { Debug.LogError("[crate_puzzle] " + Name + ": missing layout"); Done = true; return; }
            T = Rect.width / cols;
            grid = new char[cols, rows];
            plateFrames = SpriteVfx.Load("drill/fx_plate");
            var stackA = WardenStatueMechanism.Load("VFX/props/drill_crate_stack");
            var stackB = WardenStatueMechanism.Load("VFX/props/drill_crate_stack2");
            var crateSprite = WardenStatueMechanism.Load("VFX/props/drill_crate");
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < cols; x++)
                {
                    char c = x < lines[y].Length ? lines[y][x] : 'o';
                    var cell = new Vector2Int(x, y);
                    grid[x, y] = c == '$' ? '_' : c == '*' ? '.' : c;
                    if (c == '#')
                    {
                        var sr = TrapArt.MakeRenderer(Ctx.Host.transform, "Store Shelf", Feet(cell), Ctx.Player, 0);
                        sr.sprite = ((x * 7 + y * 3) % 3 == 0) ? stackB : stackA;
                        sr.color = new Color(0.72f, 0.72f, 0.8f);                       // shelves darker than pushable crates (light pine crates)
                        var go = new GameObject("Store Shelf Collider");
                        go.transform.SetParent(Ctx.Host.transform, true);
                        go.transform.position = Center(cell);
                        go.AddComponent<BoxCollider2D>().size = new Vector2(T, T);
                    }
                    if (c == '.' || c == '*')
                    {
                        var p = new Plate { Cell = cell };
                        p.Sr = TrapArt.MakeRenderer(Ctx.Host.transform, "Store Plate", Center(cell), Ctx.Player, -6);
                        FxLighting.MakeUnlit(p.Sr);
                        if (plateFrames.Length > 0) p.Sr.sprite = plateFrames[0];
                        var lg = new GameObject("glow");
                        lg.transform.SetParent(p.Sr.transform, false);
                        p.Glow = lg.AddComponent<Light2D>();
                        p.Glow.lightType = Light2D.LightType.Point;
                        p.Glow.color = new Color(1f, 0.78f, 0.4f);
                        p.Glow.pointLightOuterRadius = 0.7f;
                        p.Glow.intensity = 0f;
                        plates.Add(p);
                    }
                    if (c == '$' || c == '*')
                    {
                        var k = new Crate { Cell = cell, Start = cell };
                        k.Sr = TrapArt.MakeRenderer(Ctx.Host.transform, "Store Crate", Feet(cell), Ctx.Player, 0);
                        k.Sr.sprite = crateSprite;
                        var go = new GameObject("Store Crate Body");
                        go.transform.SetParent(Ctx.Host.transform, true);
                        go.transform.position = Center(cell);
                        k.Body = go.AddComponent<Rigidbody2D>();
                        k.Body.bodyType = RigidbodyType2D.Kinematic;
                        k.Body.interpolation = RigidbodyInterpolation2D.Interpolate;
                        k.Box = go.AddComponent<BoxCollider2D>();
                        k.Box.size = new Vector2(T * 0.94f, T * 0.8f);
                        crates.Add(k);
                    }
                }
            reset = new TrapListener(Str("reset", Name + "_reset"));
            OnFlagsChanged();
        }

        // ------------------------------------------------------------------ tile coordinates
        private Vector2 Center(Vector2Int c) => new Vector2(Rect.xMin + (c.x + 0.5f) * T, Rect.yMax - (c.y + 0.5f) * T);
        /// <summary>Image feet (bottom-centre pivot) of an object standing in the tile.</summary>
        private Vector2 Feet(Vector2Int c) => new Vector2(Rect.xMin + (c.x + 0.5f) * T, Rect.yMax - (c.y + 1f) * T + 0.02f);
        private Vector2Int CellOf(Vector2 p) => new Vector2Int(Mathf.FloorToInt((p.x - Rect.xMin) / T), Mathf.FloorToInt((Rect.yMax - p.y) / T));
        private bool InGrid(Vector2Int c) => c.x >= 0 && c.y >= 0 && c.x < cols && c.y < rows;

        private Crate CrateAt(Vector2Int c)
        {
            foreach (var k in crates) if (k.Cell == c) return k;
            return null;
        }

        // ------------------------------------------------------------------ flags / loading a save
        public override void OnFlagsChanged()
        {
            if (Done || grid == null || !Ctx.Flags.HasAll(Sets)) return;
            // already solved (loading a save): put the crates on the plates
            for (int i = 0; i < crates.Count && i < plates.Count; i++) Place(crates[i], plates[i].Cell);
            Done = true;
            Refresh(false);
        }

        public override void OnFlagsReset()
        {
            if (!Done || Ctx.Flags.HasAll(Sets)) return;
            Done = false;
            ResetCrates(false);
        }

        // ------------------------------------------------------------------ every frame
        public override void Animate()
        {
            if (grid == null) return;
            float dt = Time.deltaTime;
            foreach (var k in crates)
            {
                if (k.Sr == null) continue;
                Vector2 basePos = k.Body != null ? k.Body.position : Center(k.Cell);
                float sx = k.Shake > 0f ? Mathf.Sin(Time.time * 80f) * 0.015f * k.Shake : 0f;
                k.Shake = Mathf.MoveTowards(k.Shake, 0f, dt * 4f);
                k.Sr.transform.position = basePos + new Vector2(sx, -T * 0.5f + 0.02f);
            }
            if (!Done && reset.Poll()) ResetCrates(true);
        }

        /// <summary>
        /// "PUSH" IS DETECTED BY PHYSICAL CONTACT (detecting by the FOOT tile + centre offset &lt; 0.45 tiles failed: Ren's collider is at his feet while the body is drawn higher,
        /// so Ren was blocked by the crate but could not push it - he had to shuffle his feet to the crate middle; sideways pushes failed most because the detection strip
        /// sat far below the collision strip).
        ///   • Pushing works when Ren's collider box PRESSES against the crate face in the input direction (gap &lt; 0.06 units) and both boxes overlap on the perpendicular axis
        ///     by at least 25% - exactly when the player sees Ren blocked by the crate.
        ///   • Pressing 0.12 s makes the crate shake as a warning, then slide 1 tile; holding the key = continuous pushing (0.26 s per tile).
        ///   • ROW ASSIST: while pushing, Ren is pulled gently onto the crate's row / column (Zelda boulder style) -> pushing several tiles in a row never slips off the crate.
        /// </summary>
        public override void Tick(Vector2 playerPos, bool inside)
        {
            if (grid == null || Done) return;
            if (inside && !hinted) { hinted = true; Hints.Show(Str("hint", "Push the crates onto the pressure plates. Walk into a crate to shove it. Stuck? Pull the reset lever."), 5f); }
            Vector2 mv = GameInput.Move;
            Vector2Int dir = Vector2Int.zero;
            if (mv.sqrMagnitude > 0.25f) dir = Mathf.Abs(mv.x) > Mathf.Abs(mv.y) ? new Vector2Int(mv.x > 0f ? 1 : -1, 0) : new Vector2Int(0, mv.y > 0f ? -1 : 1);   // Tiled tiles: y points down
            Crate target = dir != Vector2Int.zero ? Touching(dir) : null;
            if (target == null || target != leaning || dir != leanDir) { leaning = target; leanDir = dir; leanTime = 0f; return; }
            leanTime += Time.deltaTime;
            if (leanTime > 0f && leanTime < 0.12f) target.Shake = Mathf.Max(target.Shake, 0.35f);    // wind-up: the crate shakes slightly before sliding
            if (leanTime < 0.12f) return;
            leanTime = -0.14f;                                                         // continuous push: 0.26 s per tile
            if (TryPush(target, dir)) Ctx.Run(Align(target, dir));
        }

        private Collider2D playerBox;

        /// <summary>The crate Ren is pressing against along `dir` (Tiled tiles), picking the one with the most overlap.</summary>
        private Crate Touching(Vector2Int dir)
        {
            if (playerBox == null && Ctx.Player != null)
                foreach (var c in Ctx.Player.GetComponents<Collider2D>()) if (!c.isTrigger) { playerBox = c; break; }
            if (playerBox == null) return null;
            Bounds pb = playerBox.bounds;
            Vector2 w = new Vector2(dir.x, -dir.y);                                    // world direction (y up)
            Crate best = null; float bestOv = 0f;
            foreach (var k in crates)
            {
                if (k.Moving || k.Box == null) continue;
                Bounds cb = k.Box.bounds;
                float gap, ov, need;
                if (w.x != 0f)
                {
                    gap = w.x > 0f ? cb.min.x - pb.max.x : pb.min.x - cb.max.x;
                    ov = Mathf.Min(pb.max.y, cb.max.y) - Mathf.Max(pb.min.y, cb.min.y);
                    need = 0.25f * Mathf.Min(pb.size.y, cb.size.y);
                }
                else
                {
                    gap = w.y > 0f ? cb.min.y - pb.max.y : pb.min.y - cb.max.y;
                    ov = Mathf.Min(pb.max.x, cb.max.x) - Mathf.Max(pb.min.x, cb.min.x);
                    need = 0.25f * Mathf.Min(pb.size.x, cb.size.x);
                }
                if (gap > 0.06f || gap < -0.12f || ov < need) continue;
                if (ov > bestOv) { bestOv = ov; best = k; }
            }
            return best;
        }

        /// <summary>Row assist: while the crate slides, pull Ren along the perpendicular axis onto the crate's row / column (at most 0.2 units).</summary>
        private IEnumerator Align(Crate k, Vector2Int dir)
        {
            var rb = Ctx.Player != null ? Ctx.Player.GetComponent<Rigidbody2D>() : null;
            if (rb == null || playerBox == null) yield break;
            for (float t = 0f; t < 0.16f; t += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();
                Vector2 c = Center(k.Cell);
                Vector2 p = rb.position;
                // centre of Ren's foot box lined up with the crate centre on the perpendicular axis
                Vector2 off = (Vector2)playerBox.bounds.center - p;
                float err = dir.x != 0 ? (c.y - (p.y + off.y)) : (c.x - (p.x + off.x));
                float step = Mathf.Clamp(err, -0.2f, 0.2f) * 0.35f;
                if (Mathf.Abs(err) < 0.01f) yield break;
                // set rb.position directly AFTER the physics step (MovePosition would be overwritten by PlayerController.FixedUpdate in the same step)
                rb.position = dir.x != 0 ? p + new Vector2(0f, step) : p + new Vector2(step, 0f);
            }
        }

        private bool TryPush(Crate k, Vector2Int dir)
        {
            var to = k.Cell + dir;
            bool ok = InGrid(to) && grid[to.x, to.y] != '#' && grid[to.x, to.y] != '-' && grid[to.x, to.y] != 'o' && CrateAt(to) == null && !Blocked(to, k);
            if (!ok)
            {
                k.Shake = 1f;
                if (Time.time >= nextThunk) { nextThunk = Time.time + 0.4f; Sfx.Play("hit_wood", Center(k.Cell), 0.35f); }
                if (InGrid(to) && grid[to.x, to.y] == '-' && Time.time >= nextHint) { nextHint = Time.time + 6f; Hints.Show("The crate won't fit through the gap between the shelves.", 2.5f); }
                return false;
            }
            Ctx.Run(Slide(k, to));
            return true;
        }

        /// <summary>Other physics obstacles in the destination tile (props, pillars...) - except the crate itself and the player.</summary>
        private bool Blocked(Vector2Int c, Crate self)
        {
            var hits = Physics2D.OverlapBoxAll(Center(c), new Vector2(T * 0.6f, T * 0.6f), 0f);
            foreach (var h in hits)
            {
                if (h == null || h.isTrigger) continue;
                if (self.Body != null && h.attachedRigidbody == self.Body) continue;
                if (Ctx.Player != null && h.transform.IsChildOf(Ctx.Player)) continue;
                return true;
            }
            return false;
        }

        private IEnumerator Slide(Crate k, Vector2Int to)
        {
            k.Moving = true;
            Vector2 a = Center(k.Cell), b = Center(to);
            k.Cell = to;
            Sfx.Play("lift_grind", a, 0.45f);
            for (float t = 0f; t < 0.16f; t += Time.deltaTime)
            {
                if (k.Body != null) k.Body.MovePosition(Vector2.Lerp(a, b, t / 0.16f));
                yield return null;
            }
            if (k.Body != null) k.Body.MovePosition(b);
            yield return new WaitForFixedUpdate();
            k.Moving = false;
            SpriteVfx.Spawn("m3_dash_dust", b - new Vector2(0f, T * 0.45f), 0.6f, k.Sr.sortingLayerID, k.Sr.sortingOrder - 1, false, 18f, false);
            Refresh(true);
        }

        private void Place(Crate k, Vector2Int c)
        {
            k.Cell = c;
            if (k.Body != null) { k.Body.position = Center(c); k.Body.transform.position = Center(c); }
        }

        private void ResetCrates(bool fx)
        {
            foreach (var k in crates)
            {
                if (fx && k.Cell != k.Start) SpriteVfx.Spawn("drill/fx_snuff", Center(k.Cell), 1.2f, k.Sr.sortingLayerID, k.Sr.sortingOrder + 1, false, 14f, false);
                Place(k, k.Start);
                if (fx) SpriteVfx.Spawn("drill/fx_snuff", Center(k.Start), 1.2f, k.Sr.sortingLayerID, k.Sr.sortingOrder + 1, false, 14f, false);
            }
            leaning = null;
            if (fx) Sfx.Play("lift_grind", Rect.center, 0.6f);                         // reminder = the lever's `text`
            Refresh(false);
        }

        /// <summary>Update the pressure plates (sink / glow) + check whether solved.</summary>
        private void Refresh(bool sound)
        {
            int covered = 0;
            foreach (var p in plates)
            {
                bool on = CrateAt(p.Cell) != null;
                if (on && !p.Covered && sound) { Sfx.Play("trap_plate", Center(p.Cell), 0.8f); SpriteVfx.Spawn("duo_light_orb", Center(p.Cell), 0.6f, p.Sr.sortingLayerID, p.Sr.sortingOrder + 8, false, 18f, false); }
                p.Covered = on;
                if (plateFrames.Length >= 3) p.Sr.sprite = plateFrames[on ? 2 : 0];
                if (p.Glow != null) p.Glow.intensity = on ? 0.9f : 0f;
                if (on) covered++;
            }
            foreach (var k in crates) if (k.Sr != null) k.Sr.color = plates.Exists(p => p.Cell == k.Cell) ? new Color(1f, 0.93f, 0.8f) : Color.white;
            if (Done || covered < plates.Count || plates.Count == 0) return;
            Done = true;
            Sfx.Play("puzzle_solved", Rect.center);
            CameraFollow.KickMain(Vector2.down, 0.04f, 0.25f);
            Hints.Show(Str("text", "The last plate sinks with a clunk. Somewhere west, a heavy bolt slides back."), 4.5f);
            Ctx.Flags.Set(Sets);
        }

        internal override void DebugSolve()
        {
            if (Done) return;
            Ctx.Flags.Set(Sets);
        }
    }
}
