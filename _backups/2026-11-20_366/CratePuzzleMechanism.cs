using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 20/11 (§360): crate_puzzle — ĐẨY THÙNG LÊN TẤM ÉP (Sokoban nhỏ; The Quartermaster's Store, Map 4 — thay câu đố cọc tập §356).
    /// Vùng object = lưới ô (khớp ô Tiled). `layout` = các hàng ô từ TRÊN xuống, ngăn bằng '/', mỗi ký tự = một ô:
    ///   '#' kệ / chồng thùng cố định (tự dựng hình + va chạm)   '_' sàn   '-' sàn nhưng thùng KHÔNG vào được (lối vào chuồng)
    ///   '$' thùng   '.' tấm ép   '*' thùng nằm sẵn trên tấm ép   'o' ngoài lưới (bỏ qua)
    /// Đẩy: đi thẳng vào thùng (theo trục ngang / dọc) ~0,18 s -> thùng trượt 1 ô nếu ô kế tiếp trống (không kệ, không thùng, không vật cản vật lý).
    /// Không kéo được (Sokoban): đẩy vào góc là kẹt -> gạt cần `reset` (lever `fires` = tên tín hiệu) đưa thùng về chỗ cũ.
    /// Tấm ép có thùng = rune sáng vàng + đèn nhỏ; đủ thùng trên mọi tấm -> cờ `sets` (mặc định tally_done), thùng khoá lại.
    /// Lời giải được kiểm bằng bộ giải BFS (_tools/m4drill_1120/s360/sok.py) trước khi đặt vào map.
    /// </summary>
    public sealed class CratePuzzleMechanism : Mechanism
    {
        private sealed class Crate
        {
            public Vector2Int Cell, Start;
            public SpriteRenderer Sr;
            public Rigidbody2D Body;
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
            if (rows == 0 || cols == 0) { Debug.LogError("[crate_puzzle] " + Name + ": thiếu layout"); Done = true; return; }
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
                        sr.color = new Color(0.72f, 0.72f, 0.8f);                       // kệ tối hơn thùng đẩy được (thùng gỗ thông sáng màu)
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
                        go.AddComponent<BoxCollider2D>().size = new Vector2(T * 0.94f, T * 0.8f);
                        crates.Add(k);
                    }
                }
            reset = new TrapListener(Str("reset", Name + "_reset"));
            OnFlagsChanged();
        }

        // ------------------------------------------------------------------ toạ độ ô
        private Vector2 Center(Vector2Int c) => new Vector2(Rect.xMin + (c.x + 0.5f) * T, Rect.yMax - (c.y + 0.5f) * T);
        /// <summary>Chân hình (pivot đáy giữa) của vật đứng trong ô.</summary>
        private Vector2 Feet(Vector2Int c) => new Vector2(Rect.xMin + (c.x + 0.5f) * T, Rect.yMax - (c.y + 1f) * T + 0.02f);
        private Vector2Int CellOf(Vector2 p) => new Vector2Int(Mathf.FloorToInt((p.x - Rect.xMin) / T), Mathf.FloorToInt((Rect.yMax - p.y) / T));
        private bool InGrid(Vector2Int c) => c.x >= 0 && c.y >= 0 && c.x < cols && c.y < rows;

        private Crate CrateAt(Vector2Int c)
        {
            foreach (var k in crates) if (k.Cell == c) return k;
            return null;
        }

        // ------------------------------------------------------------------ cờ / nạp bản lưu
        public override void OnFlagsChanged()
        {
            if (Done || grid == null || !Ctx.Flags.HasAll(Sets)) return;
            // đã giải từ trước (nạp bản lưu): xếp thùng lên các tấm ép
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

        // ------------------------------------------------------------------ mỗi khung
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

        public override void Tick(Vector2 playerPos, bool inside)
        {
            if (grid == null || Done) return;
            if (inside && !hinted) { hinted = true; Hints.Show(Str("hint", "Push the crates onto the pressure plates. Walk into a crate to shove it. Stuck? Pull the reset lever."), 5f); }
            Vector2 mv = GameInput.Move;
            Vector2Int dir = Vector2Int.zero;
            if (mv.sqrMagnitude > 0.25f) dir = Mathf.Abs(mv.x) > Mathf.Abs(mv.y) ? new Vector2Int(mv.x > 0f ? 1 : -1, 0) : new Vector2Int(0, mv.y > 0f ? -1 : 1);   // ô Tiled: y xuống
            Crate target = null;
            if (dir != Vector2Int.zero)
            {
                var pc = CellOf(playerPos + Vector2.up * 0.05f);
                var c = CrateAt(pc + dir);
                // thẳng hàng với thùng (lệch trục vuông góc < 0,45 ô) và đã sát mặt thùng
                if (c != null && !c.Moving)
                {
                    Vector2 d = Center(c.Cell) - playerPos;
                    float along = dir.x != 0 ? Mathf.Abs(d.x) : Mathf.Abs(d.y), side = dir.x != 0 ? Mathf.Abs(d.y) : Mathf.Abs(d.x);
                    if (side < T * 0.45f && along < T * 1.05f) target = c;
                }
            }
            if (target == null || target != leaning || dir != leanDir) { leaning = target; leanDir = dir; leanTime = 0f; return; }
            leanTime += Time.deltaTime;
            if (leanTime < 0.18f) return;
            leanTime = -0.1f;                                                          // đẩy liên tục: nhịp 0,28 s / ô
            TryPush(target, dir);
        }

        private void TryPush(Crate k, Vector2Int dir)
        {
            var to = k.Cell + dir;
            bool ok = InGrid(to) && grid[to.x, to.y] != '#' && grid[to.x, to.y] != '-' && grid[to.x, to.y] != 'o' && CrateAt(to) == null && !Blocked(to, k);
            if (!ok)
            {
                k.Shake = 1f;
                if (Time.time >= nextThunk) { nextThunk = Time.time + 0.4f; Sfx.Play("hit_wood", Center(k.Cell), 0.35f); }
                if (InGrid(to) && grid[to.x, to.y] == '-' && Time.time >= nextHint) { nextHint = Time.time + 6f; Hints.Show("The crate won't fit through the gap between the shelves.", 2.5f); }
                return;
            }
            Ctx.Run(Slide(k, to));
        }

        /// <summary>Vật cản vật lý khác trong ô đích (đồ bày, cột...) — trừ chính thùng và người chơi.</summary>
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
            if (fx) Sfx.Play("lift_grind", Rect.center, 0.6f);                         // lời nhắc = `text` của cần gạt
            Refresh(false);
        }

        /// <summary>Cập nhật tấm ép (lún / sáng) + kiểm tra đã giải.</summary>
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
