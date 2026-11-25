using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// pipegrid - pipe grid `cols` x `rows` (objects pipe_&lt;col&gt;_&lt;row&gt;), initial masks `masks`, bolted cells `bolts` ("x,y;x,y").
    /// Operated through <see cref="ConsoleMechanism"/>. Win: Ichor from `src` reaches `dst`, does NOT reach `cut`, does NOT leak (every wet pipe end
    /// connects to another pipe) -> flag `sets` (default &lt;name&gt;_solved), text `solve_text`, camera pans to `after_focus`.
    /// Sprites come from the hidden palette pipepal_{cell|wet|bolt_cell|bolt_wet}_NN. Fixed pipes outside the grid pipefix_{IN|A|B}_{NN}_{k}:
    /// A gets wet when reaching cut, B when reaching dst. Leaking pipe ends spray Ichor (clones of object 'leakpal_fx').
    /// </summary>
    public sealed class PipeGridMechanism : Mechanism
    {
        private int cols, rows;
        private int[] masks, startMasks;
        private bool[] bolted;
        private SpriteRenderer[] cells;
        private Light2D[] glows;
        private readonly List<(SpriteRenderer sr, string group, int mask)> fixedPipes = new List<(SpriteRenderer, string, int)>();
        private readonly List<GameObject> leakFx = new List<GameObject>();
        private float inputReady;
        private ConsoleMechanism controlledBy;

        private struct State { public HashSet<int> wet; public bool toDst, toCut; public int leaks; }

        public override void Init()
        {
            cols = Int("cols", 4);
            rows = Int("rows", 3);
            int n = cols * rows;
            var ms = Str("masks", "").Split(',');
            masks = new int[n];
            bolted = new bool[n];
            cells = new SpriteRenderer[n];
            glows = new Light2D[n];
            for (int i = 0; i < n && i < ms.Length; i++) int.TryParse(ms[i], out masks[i]);
            startMasks = (int[])masks.Clone();
            foreach (var b in Str("bolts", "").Split(';'))
            {
                var p = b.Split(',');
                if (p.Length == 2 && int.TryParse(p[0], out int bx) && int.TryParse(p[1], out int by) && bx < cols && by < rows)
                    bolted[by * cols + bx] = true;
            }
            for (int cy = 0; cy < rows; cy++)
                for (int cx = 0; cx < cols; cx++)
                {
                    int i = cy * cols + cx;
                    if (!Ctx.Named.TryGetValue($"pipe_{cx}_{cy}", out var list) || list.Count == 0) continue;
                    cells[i] = list[0].GetComponentInChildren<SpriteRenderer>(true);
                    // soft blue light for cells carrying Ichor
                    var go = new GameObject($"glow_{cx}_{cy}");
                    go.transform.position = (Vector3)CellRect(cx, cy).center;
                    go.transform.SetParent(Ctx.Host.transform, true);
                    var l = go.AddComponent<Light2D>();
                    l.lightType = Light2D.LightType.Point;
                    l.pointLightOuterRadius = CellRect(cx, cy).width * 0.85f;
                    l.pointLightInnerRadius = 0.02f;
                    l.intensity = 0.55f;
                    l.color = new Color(0.55f, 1f, 0.35f);
                    l.enabled = false;
                    glows[i] = l;
                }
            foreach (var kv in Ctx.Named)
            {
                if (!kv.Key.StartsWith("pipefix_")) continue;                 // pipefix_{GROUP}_{MM}_{k}
                var p = kv.Key.Split('_');
                if (p.Length < 3 || !int.TryParse(p[2], out int mask)) continue;
                foreach (var go in kv.Value)
                {
                    var sr = go.GetComponentInChildren<SpriteRenderer>(true);
                    if (sr != null) fixedPipes.Add((sr, p[1], mask));
                }
            }
            if (Ctx.Named.TryGetValue("leakpal_fx", out var tpl) && tpl.Count > 0) { Ctx.LeakTemplate = tpl[0]; Ctx.LeakTemplate.SetActive(false); }
            Repaint();
        }

        // ------------------------------------------------------------------ operating (from ConsoleMechanism)
        internal void BeginControl(ConsoleMechanism console)
        {
            controlledBy = console;
            inputReady = Time.unscaledTime + 0.35f;      // the press that opened the panel does not count as a rotate click
        }

        internal void ControlUpdate(Keyboard kb, Mouse mouse)
        {
            if (Done || Time.unscaledTime < inputReady) return;
            if (kb != null && kb.rKey.wasPressedThisFrame) { ResetGrid(); return; }
            var cam = UnityEngine.Camera.main;
            if (mouse == null || cam == null) return;
            bool cw = mouse.leftButton.wasPressedThisFrame, ccw = mouse.rightButton.wasPressedThisFrame;
            if (!cw && !ccw) return;
            Vector3 wp = cam.ScreenToWorldPoint(mouse.position.ReadValue());
            for (int cy = 0; cy < rows; cy++)
                for (int cx = 0; cx < cols; cx++)
                    if (CellRect(cx, cy).Contains((Vector2)wp)) { RotateCell(cy * cols + cx, cw); return; }
        }

        private Rect CellRect(int cx, int cy)
        {
            float w = Rect.width / cols, h = Rect.height / rows;
            return new Rect(Rect.xMin + cx * w, Rect.yMax - (cy + 1) * h, w, h);
        }

        private void RotateCell(int i, bool clockwise)
        {
            if (bolted[i])
            {
                Hints.Show("This joint is bolted fast.", 1.5f);
                if (cells[i] != null) Ctx.Run(Nudge(cells[i].transform));
                return;
            }
            masks[i] = clockwise ? PipeNetwork.RotateCW(masks[i]) : PipeNetwork.RotateCCW(masks[i]);
            var st = Repaint();
            if (st.toDst && !st.toCut && st.leaks == 0)
            {
                Done = true;
                Ctx.Run(Solved());
            }
            else if (st.toDst && st.toCut) Hints.Show("The Ether reaches the drain - but it still feeds the cistern. Cut the east line.", 3f);
            else if (st.toDst && st.leaks > 0) Hints.Show("The Ether reaches the drain, but spills from an open joint. Seal every end.", 3f);
        }

        private void ResetGrid()
        {
            masks = (int[])startMasks.Clone();
            Repaint();
            Hints.Show("The valves clank back to where they were.", 1.5f);
        }

        private static IEnumerator Nudge(Transform t)
        {
            var p0 = t.localPosition;
            for (int k = 0; k < 6; k++) { t.localPosition = p0 + (Vector3)(Random.insideUnitCircle * 0.02f); yield return new WaitForSeconds(0.03f); }
            t.localPosition = p0;
        }

        // F5 (test) also plays the tank drain scene instead of setting the flag at once
        internal override void DebugSolve()
        {
            if (Done) return;
            Done = true;
            Ctx.Run(Solved());
        }

        // the pump roars -> the camera pans slowly to the Eye's pit -> flag `sets` turns on EXACTLY when the camera arrives
        // (shield breaks, supply stream / blue glow go out, the Ichor surface drains - the off_when / toggle mechanisms) + blue flash, dust, shake -> `after_text`.
        // Setting the flag right on solving meant everything was already off when the camera arrived, and the player saw nothing happen.
        private IEnumerator Solved()
        {
            SfxHooks.MechanismEvent(this, "solved", "puzzle_solved"); // sound
            Hints.Show(Str("solve_text", "The pipes connect."), 4f);
            string flag = Str("sets", Name + "_solved");
            string after = Str("after_focus", "");
            bool scene = !string.IsNullOrEmpty(after) && Ctx.Camera != null;
            if (!scene) Ctx.Flags.Set(flag);
            for (float t = 0f; t < 1.4f; t += 0.12f)                      // pump roars, light shake growing
            {
                CameraFollow.KickMain(Random.insideUnitCircle, 0.012f + t * 0.012f, 0.12f);
                yield return new WaitForSeconds(0.12f);
            }
            if (scene)
            {
                Vector3 pit = Ctx.TilePoint(after, Rect.center);
                float size = UnityEngine.Camera.main != null ? UnityEngine.Camera.main.orthographicSize : 3f;
                float sharp = Ctx.Camera.focusSharpness;
                Ctx.Camera.focusSharpness = Float("after_pan", 1.3f);
                Ctx.Camera.Focus(pit, size);
                yield return new WaitForSeconds(Float("after_wait", 2.6f));

                Ctx.Flags.Set(flag);
                var dir = Ctx.Director;
                var psr = dir != null && dir.Player != null ? dir.Player.GetComponentInChildren<SpriteRenderer>() : null;
                int layer = psr != null ? psr.sortingLayerID : 0, order = psr != null ? psr.sortingOrder : 2;
                var go = new GameObject("Drain Flash");
                go.transform.position = pit;
                var flash = go.AddComponent<Light2D>();
                flash.lightType = Light2D.LightType.Point;
                flash.pointLightOuterRadius = 6f; flash.pointLightInnerRadius = 0.5f;
                flash.color = new Color(0.55f, 1f, 0.35f);
                if (dir != null) CutsceneFx.Burst(dir, pit + new Vector3(0f, -0.35f, 0f), 1.5f, 0.6f, 12, 6, layer, order);
                Hints.Show(Str("after_text", "Far off, the pit goes dark. The Eye shrieks."), 3.5f);
                float nextKick = 0f;
                for (float t = 0f; t < 1.8f; t += Time.deltaTime)
                {
                    flash.intensity = Mathf.Lerp(2.4f, 0f, t / 1.8f);
                    if (t >= nextKick && t < 1.2f)
                    {
                        nextKick = t + 0.08f;
                        CameraFollow.KickMain(Random.insideUnitCircle, Mathf.Lerp(0.07f, 0.01f, t / 1.2f), 0.12f);
                    }
                    yield return null;
                }
                Object.Destroy(go);
                yield return new WaitForSeconds(0.9f);
                Ctx.Camera.focusSharpness = sharp;
            }
            if (controlledBy != null) controlledBy.Exit();
            else if (scene) Ctx.Camera.Release();                        // solved with F5 (not through the console): return the camera ourselves
            Hints.Show("The cistern gate grinds open.", 3f);
        }

        // ------------------------------------------------------------------ redraw by the current flow
        /// <summary>Repaint dry/wet pipes, lights, fixed pipes and leak sprays from the current flow.</summary>
        private State Repaint()
        {
            var st = new State { wet = new HashSet<int>() };
            var ends = new List<(int cell, int k)>();
            if (PipeNetwork.ParseEnd(Str("src", "1,0,N"), out int sx, out int sy, out int sb))
                PipeNetwork.Flow(masks, cols, rows, sx, sy, sb, st.wet, ends);
            PipeNetwork.ParseEnd(Str("dst", "2,2,S"), out int dx, out int dy, out int db);
            PipeNetwork.ParseEnd(Str("cut", "3,1,E"), out int cx, out int cy, out int cb);
            var leakPos = new List<Vector2>();
            foreach (var (cell, k) in ends)
            {
                int x = cell % cols, y = cell / cols;
                if (x == dx && y == dy && PipeNetwork.Bit[k] == db) { st.toDst = true; continue; }
                if (x == cx && y == cy && PipeNetwork.Bit[k] == cb) { st.toCut = true; continue; }
                if (x == sx && y == sy && PipeNetwork.Bit[k] == sb) continue;
                st.leaks++;
                var r = CellRect(x, y);
                leakPos.Add(r.center + new Vector2(PipeNetwork.DX[k] * r.width * 0.45f, -PipeNetwork.DY[k] * r.height * 0.45f));
            }
            for (int i = 0; i < masks.Length; i++)
            {
                bool w = st.wet.Contains(i);
                var sr = cells[i];
                if (sr != null)
                {
                    var sp = Ctx.PipeSprite((bolted[i] ? "bolt_" : "") + (w ? "wet_" : "cell_") + masks[i].ToString("00"));
                    if (sp != null) sr.sprite = sp;
                }
                if (glows[i] != null) glows[i].enabled = w;
            }
            foreach (var (sr, group, mask) in fixedPipes)
            {
                if (group == "IN") continue;
                bool w = (group == "A" && st.toCut) || (group == "B" && st.toDst);
                var sp = Ctx.PipeSprite((w ? "wet_" : "cell_") + mask.ToString("00"));
                if (sp != null) sr.sprite = sp;
            }
            UpdateLeaks(leakPos);
            return st;
        }

        /// <summary>Leak spray: reuse / clone the 'leakpal_fx' template.</summary>
        private void UpdateLeaks(List<Vector2> leakPos)
        {
            var template = Ctx.LeakTemplate;
            if (template == null) return;
            while (leakFx.Count < leakPos.Count)
            {
                var c = Object.Instantiate(template, template.transform.parent);
                c.name = "leak_fx";
                leakFx.Add(c);
            }
            for (int i = 0; i < leakFx.Count; i++)
            {
                bool on = i < leakPos.Count;
                if (on)
                {
                    var pos = leakPos[i];
                    // the template is placed by its sprite feet: offset so the image centre lands on the pipe end
                    var sr = leakFx[i].GetComponentInChildren<SpriteRenderer>(true);
                    Vector3 off = sr != null ? leakFx[i].transform.position - sr.bounds.center : Vector3.zero;
                    leakFx[i].transform.position = new Vector3(pos.x, pos.y, 0f) + off;
                }
                leakFx[i].SetActive(on);
            }
        }
    }
}
