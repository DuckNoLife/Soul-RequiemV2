using System.Collections;
using System.Collections.Generic;
using SuperTiled2Unity;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// BOSS ARENA - shared rules for every boss fight (without them the Ether Ram charged out of the Boiler Room all the way into the Overseer's Office,
    /// because the area had no locking gate and the AI had no range limit).
    ///   1. Leash: the boss is always kept inside its area box (every kind of movement - walking, charging, leaping, being pushed).
    ///   2. Ether barrier: an area WITHOUT a self-closing gate (close_on_enter) -> when the boss appears, invisible walls go up around the area box
    ///      (4 thick BoxCollider2D slabs - block both the player and enemies; the player is also held inside the box like the boss), plus a rippling light curtain at the gaps of the real walls (entrances).
    ///      Boss down / area cleared / player down (area resets, boss removed) -> the barrier dissolves.
    ///   Turn it off for one area: property `arena_seal = false` on the area object (Tiled). Colour: `seal_color` (default pale soul blue).
    ///   By DEFAULT the light curtain at the gaps is NOT drawn
    ///   (the walls still block, invisible, silent, no shake). To show it for one area: `seal_visible = true` on the area object.
    /// Gaps are probed with Physics2D: a point on the area edge that lies in no static collider (wall, water, obstacle) = a passage.
    /// </summary>
    public class ArenaSeal : MonoBehaviour
    {
        private class Strip { public SpriteRenderer sr; public Texture2D tex; public bool horizontal; public int w, h; public Light2D light; }
        private class Arena
        {
            public EnemyAI boss; public string zone; public Rect rect;
            public GameObject root; public readonly List<Strip> strips = new List<Strip>();
            public float alpha, target; public bool dropping, sealedIn;
        }

        private MapEncounterDirector dir;
        private readonly List<Arena> arenas = new List<Arena>();
        private float nextRepaint;
        private bool showSeal;                    // does the area being built draw the light curtain (default no)

        private void Awake()
        {
            dir = GetComponent<MapEncounterDirector>();
            if (dir != null) dir.BossSpawned += OnBossSpawned;
        }

        private void OnDestroy()
        {
            if (dir != null) dir.BossSpawned -= OnBossSpawned;
        }

        private void OnBossSpawned(EnemyAI boss, string hint)
        {
            if (boss == null || !dir.TryGetZoneAt(boss.transform.position, out var id, out var rect))
            {
                Debug.LogWarning($"[Arena] No area found containing boss {(boss != null ? boss.name : "null")} at {(boss != null ? (Vector2)boss.transform.position : Vector2.zero)}");
                return;
            }
            foreach (var a in arenas) if (a.boss == boss) return;
            var arena = new Arena { boss = boss, zone = id, rect = rect };
            arenas.Add(arena);
            var zp = ZoneProps(id);
            bool seal = !dir.ZoneHasLockGate(id) && (zp == null || zp.GetBool("arena_seal", true));
            if (!seal) return;
            Color col = new Color(0.62f, 0.9f, 1f);
            if (zp != null && ColorUtility.TryParseHtmlString(zp.GetString("seal_color", ""), out var c)) col = c;
            showSeal = zp != null && zp.GetBool("seal_visible", false);
            Raise(arena, col);
        }

        private SuperCustomProperties ZoneProps(string id)
        {
            if (dir.mapRoot == null) return null;
            foreach (var so in dir.mapRoot.GetComponentsInChildren<SuperObject>(true))
                if (so.m_Type == "zone")
                {
                    var p = so.GetComponent<SuperCustomProperties>();
                    if (p != null && p.GetString("zone_id", so.m_TiledName) == id) return p;
                }
            return null;
        }

        // ------------------------------------------------------------------ build the barrier
        private void Raise(Arena a, Color col)
        {
            var r = a.rect;
            a.root = new GameObject("Arena Seal " + a.zone);
            if (showSeal) Sfx.Play2D("arena_seal", 0.8f);                                        // sound
            // player right at / on the area edge when the boss appears -> push inside so he is not stuck outside the invisible wall
            var pl = dir.Player;
            if (pl != null)
            {
                var inner = new Rect(r.xMin + 0.3f, r.yMin + 0.3f, r.width - 0.6f, r.height - 0.6f);
                Vector2 p = pl.position;
                if (!inner.Contains(p))
                {
                    var q = new Vector2(Mathf.Clamp(p.x, inner.xMin, inner.xMax), Mathf.Clamp(p.y, inner.yMin, inner.yMax));
                    var rb = pl.GetComponent<Rigidbody2D>();
                    if (rb != null) rb.position = q;
                    pl.position = new Vector3(q.x, q.y, pl.position.z);
                }
            }
            // a thin EdgeCollider let the Ether Ram squeeze the player through -> 4 THICK slabs (0.6 units) right outside the area edge
            const float T = 0.6f;
            var walls = new[]
            {
                new Rect(r.xMin - T, r.yMax, r.width + 2f * T, T),      // top
                new Rect(r.xMin - T, r.yMin - T, r.width + 2f * T, T),  // bottom
                new Rect(r.xMin - T, r.yMin, T, r.height),               // left
                new Rect(r.xMax, r.yMin, T, r.height),                   // right
            };
            foreach (var w in walls)
            {
                var box = a.root.AddComponent<BoxCollider2D>();
                box.offset = w.center; box.size = w.size;
                box.enabled = false;                                     // enabled after probing the gaps
            }

            var psr = pl != null ? pl.GetComponentInChildren<SpriteRenderer>() : null;
            int layer = psr != null ? psr.sortingLayerID : 0, order = psr != null ? psr.sortingOrder : 2;
            // probe gaps on the 4 sides (the arena's own slabs are not enabled yet while probing)
            Openings(a, new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), true, col, layer, order);   // bottom side
            Openings(a, new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, r.yMax), true, col, layer, order);   // top side
            Openings(a, new Vector2(r.xMin, r.yMin), new Vector2(r.xMin, r.yMax), false, col, layer, order);  // left side
            Openings(a, new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), false, col, layer, order);  // right side
            foreach (var b in a.root.GetComponents<BoxCollider2D>()) b.enabled = true;
            a.alpha = 0f; a.target = 1f;
            a.sealedIn = true;
            if (a.strips.Count > 0)
            {
                CameraFollow.KickMain(Vector2.down, 0.04f, 0.25f);
                StartCoroutine(SealHint());
            }
        }

        private IEnumerator SealHint()
        {
            yield return new WaitForSeconds(0.2f);
            while (CutsceneState.Active) yield return null;            // after the boss title card
            // no extra hint - visuals / animation / boss title already tell the player
        }

        private readonly List<Collider2D> hits = new List<Collider2D>();

        private bool Blocked(Vector2 p)
        {
            var f = ContactFilter2D.noFilter;
            f.useTriggers = false;
            hits.Clear();
            Physics2D.OverlapPoint(p, f, hits);
            foreach (var c in hits)
            {
                if (c == null) continue;
                var rb = c.attachedRigidbody;
                if (rb != null && rb.bodyType != RigidbodyType2D.Static) continue;   // player / enemies / dynamic objects
                return true;
            }
            return false;
        }


        // probe gaps by the TILES of the colliding wall layers (exactly as drawn),
        // not by colliders (wall colliders are thinner / shorter than the art). No wall layer found -> fall back to collider probing.
        private readonly List<UnityEngine.Tilemaps.Tilemap> solidMaps = new List<UnityEngine.Tilemaps.Tilemap>();
        private bool solidLoaded;

        private void LoadSolidMaps()
        {
            solidLoaded = true;
            if (dir == null || dir.mapRoot == null) return;
            foreach (var tm in dir.mapRoot.GetComponentsInChildren<UnityEngine.Tilemaps.Tilemap>(true))
                if (tm.GetComponent<Collider2D>() != null || tm.GetComponentInChildren<Collider2D>(true) != null) solidMaps.Add(tm);
        }

        private bool SolidTile(Vector2 p)
        {
            foreach (var tm in solidMaps)
                if (tm != null && tm.HasTile(tm.WorldToCell(new Vector3(p.x, p.y, tm.transform.position.z)))) return true;
            return false;
        }

        private void Openings(Arena a, Vector2 from, Vector2 to, bool horizontal, Color col, int layer, int order)
        {
            if (!solidLoaded) LoadSolidMaps();
            float len = (to - from).magnitude;
            Vector2 dirv = (to - from).normalized, nrm = horizontal ? Vector2.up : Vector2.right;
            if (solidMaps.Count > 0)
            {
                float cs = solidMaps[0].layoutGrid != null ? Mathf.Abs(solidMaps[0].layoutGrid.cellSize.x) : 0.32f;
                int n = Mathf.RoundToInt(len / cs);
                int k0 = -1;
                for (int k = 0; k <= n; k++)
                {
                    bool open = false;
                    if (k < n)
                    {
                        Vector2 pt = from + dirv * ((k + 0.5f) * cs);
                        open = !SolidTile(pt + nrm * cs * 0.5f) && !SolidTile(pt - nrm * cs * 0.5f);
                    }
                    if (open && k0 < 0) k0 = k;
                    if (!open && k0 >= 0)
                    {
                        AddStrip(a, from + dirv * (k0 * cs), from + dirv * Mathf.Min(k * cs, len), horizontal, col, layer, order);
                        k0 = -1;
                    }
                }
                return;
            }
            const float step = 0.08f;
            float runStart = -1f;
            for (float d = 0f; d <= len + step * 0.5f; d += step)
            {
                Vector2 pt = from + dirv * d;
                bool open = d <= len && !Blocked(pt + nrm * 0.1f) && !Blocked(pt - nrm * 0.1f)
                            && !Blocked(pt + nrm * 0.4f) && !Blocked(pt - nrm * 0.4f);
                if (open && runStart < 0f) runStart = d;
                if ((!open || d > len) && runStart >= 0f)
                {
                    float runEnd = Mathf.Min(d, len);
                    if (runEnd - runStart >= 0.24f) AddRun(a, from, dirv, runStart, runEnd, horizontal, col, layer, order);
                    runStart = -1f;
                }
            }
            if (runStart >= 0f && len - runStart >= 0.24f) AddRun(a, from, dirv, runStart, len, horizontal, col, layer, order);
        }

        /// <summary>Both gap ends snap to the tile grid (0.32 units) - passage edges in the map always line up with tile edges, so the curtain is never half a tile off.</summary>
        private void AddRun(Arena a, Vector2 from, Vector2 dirv, float s0, float s1, bool horizontal, Color col, int layer, int order)
        {
            const float tile = 0.32f;
            float c0 = horizontal ? from.x : from.y;                   // coordinate along the side (dirv is always +x or +y)
            float g0 = Mathf.Round((c0 + s0) / tile) * tile - c0, g1 = Mathf.Round((c0 + s1) / tile) * tile - c0;
            if (Mathf.Abs(g0 - s0) <= 0.12f) s0 = g0;
            if (Mathf.Abs(g1 - s1) <= 0.12f) s1 = g1;
            if (s1 - s0 < 0.24f) return;
            AddStrip(a, from + dirv * s0, from + dirv * s1, horizontal, col, layer, order);
        }

        private void AddStrip(Arena a, Vector2 p0, Vector2 p1, bool horizontal, Color col, int layer, int order)
        {
            if (!showSeal) return;                                   // invisible blocking is enough
            float len = (p1 - p0).magnitude;
            var s = new Strip { horizontal = horizontal };
            // horizontal side: a standing curtain (0.5 units tall) on the edge; vertical side (seen from above): a thin strip along the edge
            s.w = horizontal ? Mathf.Max(4, Mathf.RoundToInt(len * 100f)) : 14;
            s.h = horizontal ? 50 : Mathf.Max(4, Mathf.RoundToInt(len * 100f));
            s.tex = new Texture2D(s.w, s.h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var go = new GameObject("Seal Strip");
            go.transform.SetParent(a.root.transform, false);
            Vector2 mid = (p0 + p1) * 0.5f;
            go.transform.position = horizontal ? new Vector3(mid.x, p0.y, 0f) : new Vector3(p0.x, mid.y, 0f);
            s.sr = go.AddComponent<SpriteRenderer>();
            s.sr.sprite = Sprite.Create(s.tex, new Rect(0, 0, s.w, s.h), horizontal ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 0.5f), 100f);
            s.sr.color = col;
            s.sr.sortingLayerID = layer; s.sr.sortingOrder = order;
            s.sr.spriteSortPoint = SpriteSortPoint.Pivot;             // the top edge curtain stands behind Ren, the bottom edge one in front of him
            var lgo = new GameObject("Seal Light");
            lgo.transform.SetParent(go.transform, false);
            lgo.transform.localPosition = horizontal ? new Vector3(0f, 0.2f, 0f) : Vector3.zero;
            s.light = lgo.AddComponent<Light2D>();
            s.light.lightType = Light2D.LightType.Point;
            s.light.color = col;
            s.light.pointLightOuterRadius = Mathf.Clamp(len * 0.7f, 0.9f, 2.4f);
            s.light.pointLightInnerRadius = 0.1f;
            s.light.intensity = 0f;
            a.strips.Add(s);
            Paint(s, 0f);
        }

        /// <summary>Draw the curtain: vertical light strands (horizontal for vertical sides) rippling over time, fading toward the top / both ends.</summary>
        private static void Paint(Strip s, float t)
        {
            var px = new Color32[s.w * s.h];
            int along = s.horizontal ? s.w : s.h, across = s.horizontal ? s.h : s.w;
            for (int i = 0; i < along; i++)
            {
                // light strands: noise by position + time, grouped in 2 px clusters for a pixel look
                int k = i / 2;
                float n = Mathf.PerlinNoise(k * 0.37f, t * 1.6f);
                float strand = Mathf.Clamp01((n - 0.35f) * 2.2f);
                float edgeFade = Mathf.Clamp01(Mathf.Min(i, along - 1 - i) / 6f);
                for (int j = 0; j < across; j++)
                {
                    float a;
                    if (s.horizontal)
                    {
                        float v = j / (float)(across - 1);                         // 0 = curtain base, 1 = top
                        float wave = Mathf.Sin(i * 0.21f + t * 5f + j * 0.08f) * 0.12f;
                        a = (1f - v + wave) * (0.25f + 0.75f * strand);
                        if (j < 2) a = Mathf.Max(a, 0.9f);                       // light line at the curtain base
                    }
                    else
                    {
                        float u = Mathf.Abs(j - (across - 1) * 0.5f) / ((across - 1) * 0.5f);   // 0 = middle of the strip
                        a = (1f - u) * (0.35f + 0.65f * strand);
                        if (u < 0.2f) a = Mathf.Max(a, 0.85f);
                    }
                    a *= edgeFade;
                    // quantized to 4 levels (pixel art)
                    a = Mathf.Round(Mathf.Clamp01(a) * 4f) / 4f;
                    byte b = (byte)(a * 255f);
                    int x = s.horizontal ? i : j, y = s.horizontal ? j : i;
                    px[y * s.w + x] = new Color32(255, 255, 255, b);
                }
            }
            s.tex.SetPixels32(px);
            s.tex.Apply(false);
        }

        // ------------------------------------------------------------------ boss leash + barrier dissolve
        private void FixedUpdate()
        {
            for (int i = arenas.Count - 1; i >= 0; i--)
            {
                var a = arenas[i];
                bool over = a.boss == null || a.boss.Health == null || a.boss.Health.IsDead || dir.ZoneCleared(a.zone);
                if (over) { if (!a.dropping) Drop(a); continue; }
                // the player cannot be knocked out through the barrier either (boss charging / ramming him into the wall)
                if (a.sealedIn && dir.Player != null)
                {
                    var prb = dir.Player.GetComponent<Rigidbody2D>();
                    Vector2 pp = prb != null ? prb.position : (Vector2)dir.Player.position;
                    const float pm = 0.12f;
                    var pq = new Vector2(Mathf.Clamp(pp.x, a.rect.xMin + pm, a.rect.xMax - pm), Mathf.Clamp(pp.y, a.rect.yMin + pm, a.rect.yMax - pm));
                    if ((pq - pp).sqrMagnitude > 0.000001f && prb != null) prb.position = pq;
                }
                var rb = a.boss.GetComponent<Rigidbody2D>();
                Vector2 p = rb != null ? rb.position : (Vector2)a.boss.transform.position;
                const float m = 0.3f;
                var q = new Vector2(Mathf.Clamp(p.x, a.rect.xMin + m, a.rect.xMax - m), Mathf.Clamp(p.y, a.rect.yMin + m, a.rect.yMax - m));
                if ((q - p).sqrMagnitude > 0.000001f)
                {
                    if (rb != null)
                    {
                        rb.position = q;
                        var v = rb.linearVelocity;
                        if ((q.x > p.x && v.x < 0f) || (q.x < p.x && v.x > 0f)) v.x = 0f;   // drop the outward velocity
                        if ((q.y > p.y && v.y < 0f) || (q.y < p.y && v.y > 0f)) v.y = 0f;
                        rb.linearVelocity = v;
                    }
                    else a.boss.transform.position = new Vector3(q.x, q.y, a.boss.transform.position.z);
                }
            }
        }

        private void Drop(Arena a)
        {
            a.dropping = true;
            a.target = 0f;
            Sfx.Play2D("arena_unseal", 0.7f);                                      // sound
            if (a.root != null)
            {
                foreach (var b in a.root.GetComponents<BoxCollider2D>()) b.enabled = false;
                // no extra hint - visuals / animation / boss title already tell the player
            }
        }

        private void Update()
        {
            bool repaint = Time.time >= nextRepaint;
            if (repaint) nextRepaint = Time.time + 0.09f;
            for (int i = arenas.Count - 1; i >= 0; i--)
            {
                var a = arenas[i];
                a.alpha = Mathf.MoveTowards(a.alpha, a.target, Time.deltaTime / (a.dropping ? 0.7f : 0.45f));
                foreach (var s in a.strips)
                {
                    if (s.sr == null) continue;
                    var c = s.sr.color; c.a = a.alpha * (0.8f + 0.2f * Mathf.Sin(Time.time * 3f)); s.sr.color = c;
                    if (s.light != null) s.light.intensity = a.alpha * (0.55f + 0.2f * Mathf.Sin(Time.time * 2.3f));
                    if (repaint && a.alpha > 0.01f) Paint(s, Time.time);
                }
                if (a.dropping && a.alpha <= 0f)
                {
                    foreach (var s in a.strips) if (s.tex != null) Destroy(s.tex);
                    if (a.root != null) Destroy(a.root);
                    arenas.RemoveAt(i);
                }
            }
        }
    }
}
