using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// drifting_pages - PAGES DRIFTING in the void around The Margin (instead of static pages).
    /// Object area = drift area (usually the whole map). Each page:
    ///   drifts slowly (light wind diagonally upward), bobs, tilts back and forth, now and then FLIPS (squashed horizontally as it turns - the back is darker),
    ///   has DEPTH: far pages are darker + fainter + slower + shift less with the camera (parallax) -> a layered floating space.
    ///   Lives 9-20 s: fades in, fades out, respawns elsewhere. Never drifts into walkable space (`avoid`): touching it fades the page early.
    ///   Plus small purple ink motes rising and twinkling (`motes`).
    /// Properties: count (22), motes (26), sprites ("margin_page,margin_page2,margin_page3" in Resources/VFX/props),
    ///   avoid = "x,y,w,h;x,y,w,h" (Tiled tiles) - walkable areas pages stay out of.
    /// Pixels stay 1:1 (no scaling); only squashed horizontally when flipping.
    /// </summary>
    public sealed class DriftingPagesMechanism : Mechanism
    {
        private class Page
        {
            public SpriteRenderer r;
            public Vector2 at, vel;
            public float depth, born, life, bobAmp, bobFreq, tilt, tiltFreq, phase, flipAt, flipDur;
            public bool dying; public float dieAt;
        }
        private class Mote { public SpriteRenderer r; public Vector2 at; public float speed, born, life, sway; }

        private readonly List<Page> pages = new List<Page>();
        private readonly List<Mote> motes = new List<Mote>();
        private readonly List<Rect> avoid = new List<Rect>();
        private Sprite[] sprites;
        private Sprite dot;
        private Transform root;
        private Rect area;
        private Vector3 camStart;
        private bool hasCam;

        public override void Init()
        {
            area = Rect;
            var list = new List<Sprite>();
            foreach (var n in Str("sprites", "margin_page,margin_page2,margin_page3").Split(','))
            {
                var s = MarginArt.Prop(n.Trim());
                if (s != null) list.Add(s);
            }
            sprites = list.ToArray();
            foreach (var part in Str("avoid", "").Split(';'))
            {
                var v = part.Split(',');
                if (v.Length != 4) continue;
                if (!float.TryParse(v[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) ||
                    !float.TryParse(v[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) ||
                    !float.TryParse(v[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float w) ||
                    !float.TryParse(v[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float h)) continue;
                Vector3 a = Ctx.Director.TileToWorld(x, y), b = Ctx.Director.TileToWorld(x + w, y + h);
                var rr = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
                avoid.Add(new Rect(rr.x - 0.25f, rr.y - 0.25f, rr.width + 0.5f, rr.height + 0.5f));   // leave room for the torn page edge
            }
            root = new GameObject("Drifting Pages " + Name).transform;
            root.SetParent(Ctx.Host.transform, false);
            dot = MakeDot();
            if (sprites.Length > 0)
            {
                int n = Mathf.Max(0, Int("count", 22));
                for (int i = 0; i < n; i++) pages.Add(NewPage(true));
            }
            int m = Mathf.Max(0, Int("motes", 26));
            for (int i = 0; i < m; i++) motes.Add(NewMote(true));
        }

        // ------------------------------------------------------------------ trang
        private Page NewPage(bool anyAge)
        {
            var p = new Page();
            var go = new GameObject("page");
            go.transform.SetParent(root, false);
            p.r = go.AddComponent<SpriteRenderer>();
            Respawn(p, anyAge);
            return p;
        }

        private void Respawn(Page p, bool anyAge)
        {
            p.r.sprite = sprites[Random.Range(0, sprites.Length)];
            p.r.flipX = Random.value < 0.5f;
            p.depth = Random.value;                                         // 0 near, 1 far
            p.at = FreePoint();
            float spd = Mathf.Lerp(0.16f, 0.05f, p.depth);
            p.vel = new Vector2(Random.Range(-1f, 1f), Random.Range(0.15f, 0.7f)).normalized * spd;
            p.life = Random.Range(9f, 20f);
            p.born = Time.time - (anyAge ? Random.Range(0f, p.life * 0.8f) : 0f);
            p.bobAmp = Random.Range(0.02f, 0.07f); p.bobFreq = Random.Range(0.5f, 1.2f);
            p.tilt = Random.Range(6f, 16f); p.tiltFreq = Random.Range(0.25f, 0.6f);
            p.phase = Random.Range(0f, 10f);
            p.flipAt = Time.time + Random.Range(2f, 9f); p.flipDur = Random.Range(0.9f, 1.6f);
            p.dying = false;
            // near pages draw over far pages (all below props / characters)
            p.r.sortingOrder = -16 + Mathf.RoundToInt((1f - p.depth) * 4f);
        }

        private Vector2 FreePoint()
        {
            for (int k = 0; k < 30; k++)
            {
                var v = new Vector2(Random.Range(area.xMin, area.xMax), Random.Range(area.yMin, area.yMax));
                if (!InAvoid(v)) return v;
            }
            return new Vector2(area.xMin + 0.3f, area.yMax - 0.3f);
        }

        private bool InAvoid(Vector2 v)
        {
            foreach (var a in avoid) if (a.Contains(v)) return true;
            return false;
        }

        public override void Animate()
        {
            if (root == null) return;
            float dt = Time.deltaTime, now = Time.time;
            var cam = Camera.main;
            Vector3 cp = cam != null ? cam.transform.position : Vector3.zero;
            if (!hasCam && cam != null) { camStart = cp; hasCam = true; }
            Vector2 camDelta = hasCam ? (Vector2)(cp - camStart) : Vector2.zero;

            foreach (var p in pages)
            {
                if (p.r == null) continue;
                p.at += p.vel * dt;
                float age = now - p.born;
                // left the area / touched walkable space / end of life -> fade
                if (!p.dying && (age > p.life || !area.Contains(p.at) || InAvoid(p.at))) { p.dying = true; p.dieAt = now; }
                float a = Mathf.Clamp01(age / 1.5f);
                if (p.dying) { a *= 1f - Mathf.Clamp01((now - p.dieAt) / 1.2f); if (now - p.dieAt > 1.2f) { Respawn(p, false); continue; } }
                // parallax: far pages follow the camera more (as if lying deep behind)
                Vector2 parallax = camDelta * Mathf.Lerp(0.05f, 0.45f, p.depth);
                float bob = Mathf.Sin((now + p.phase) * p.bobFreq * Mathf.PI * 2f) * p.bobAmp;
                p.r.transform.position = new Vector3(p.at.x + parallax.x, p.at.y + bob + parallax.y, 0f);
                float rot = Mathf.Sin((now + p.phase) * p.tiltFreq * Mathf.PI * 2f) * p.tilt;
                // flip: squash horizontally through 0 (the back is darker)
                float sx = 1f; bool back = false;
                if (now >= p.flipAt)
                {
                    float u = (now - p.flipAt) / p.flipDur;
                    if (u >= 1f) { p.flipAt = now + Random.Range(4f, 11f); p.flipDur = Random.Range(0.9f, 1.6f); p.r.flipX = !p.r.flipX; }
                    else { float c = Mathf.Cos(u * Mathf.PI * 2f); sx = Mathf.Max(0.06f, Mathf.Abs(c)); back = u > 0.25f && u < 0.75f; rot += Mathf.Sin(u * Mathf.PI) * 20f; }
                }
                p.r.transform.rotation = Quaternion.Euler(0f, 0f, rot);
                p.r.transform.localScale = new Vector3(sx, 1f, 1f);
                float shade = Mathf.Lerp(1f, 0.42f, p.depth) * (back ? 0.62f : 1f);
                p.r.color = new Color(shade, shade * 0.97f, shade * 1.04f, a * Mathf.Lerp(0.95f, 0.55f, p.depth));
            }

            foreach (var m in motes)
            {
                if (m.r == null) continue;
                float age = now - m.born;
                if (age > m.life || InAvoid(m.at)) { ResetMote(m, false); continue; }
                m.at += new Vector2(Mathf.Sin((now + m.sway) * 1.3f) * 0.05f, m.speed) * dt;
                m.r.transform.position = new Vector3(m.at.x + camDelta.x * 0.2f, m.at.y + camDelta.y * 0.2f, 0f);
                float tw = 0.55f + 0.45f * Mathf.Sin((now + m.sway) * 5f);
                float fade = Mathf.Clamp01(age / 1f) * Mathf.Clamp01((m.life - age) / 1f);
                m.r.color = new Color(0.72f, 0.5f, 1f, 0.7f * tw * fade);
            }
        }

        // ------------------------------------------------------------------ rising ink motes
        private Mote NewMote(bool anyAge)
        {
            var m = new Mote();
            var go = new GameObject("ink mote");
            go.transform.SetParent(root, false);
            m.r = go.AddComponent<SpriteRenderer>();
            m.r.sprite = dot;
            m.r.sortingOrder = -15;
            FxLighting.MakeUnlit(m.r);
            ResetMote(m, anyAge);
            return m;
        }

        private void ResetMote(Mote m, bool anyAge)
        {
            m.at = FreePoint();
            m.speed = Random.Range(0.04f, 0.12f);
            m.life = Random.Range(4f, 9f);
            m.born = Time.time - (anyAge ? Random.Range(0f, m.life) : 0f);
            m.sway = Random.Range(0f, 10f);
        }

        private static Sprite MakeDot()
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "margin_mote" };
            tex.SetPixels32(new[] { new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 160), new Color32(255, 255, 255, 160), new Color32(255, 255, 255, 90) });
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
