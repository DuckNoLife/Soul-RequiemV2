using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// breakable - wooden barrier: slash it `hits` times (default 3) -> flag `sets` (default &lt;name&gt;_broken) + text `text`.
    /// The gate with the same name in the gates layer shakes on every hit and opens with the flag. `hint` shows the first time Ren gets close.
    /// Own images, shared by every map:
    ///   `sprite`        = Resources image of the intact object (e.g. VFX/props/barricade_crates_0); a comma-separated list makes
    ///                     the copies cycle through them (less repetition). Image feet at the bottom edge, sorted by y on the character layer.
    ///   `sprite_broken` = debris image after breaking (also takes a list), lying on the floor below characters. Empty = disappears.
    ///   `sprite_count`  = number of image copies spread along the area's long side (vertical area: stacked bottom up; horizontal: spread left -> right); default 1.
    ///   `sprite_width`  = width of each copy (Unity units); default vertical area = area width ×1.1, horizontal = width / copies.
    ///   `broken_scale`  = debris width as a fraction of the intact copy (default 0.85).
    ///   `sprite_damaged` = progressively damaged images (light -> heavy, comma-separated): each slash swaps to a heavier one,
    ///                     only the last slash breaks it. E.g. the Old Prison wooden fence: fence_h_1,fence_h_2,fence_h_3 with hits = 4.
    ///   `debris`        = plank debris images (list) scattered on the floor when broken, instead of `sprite_broken`; `debris_count` (default 10),
    ///                     `debris_scale` (default 1 = original pixel size). Scatter positions are fixed by object name (identical on the next load).
    /// DEFAULT PLACEMENT: the hit area = a rectangle in `mechanics` (as before); EACH image piece = a tile object in the
    ///   `mechanics_art` layer (SR-mech-art tileset) with `art_for` = this object's name -> each piece is visible and draggable in Tiled. With pieces,
    ///   `sprite` / `sprite_count` / `sprite_width` are ignored; `sprite_damaged`, `debris`, `sprite_broken` are still set on the object.
    /// Every hit: the object shakes + wood splinters fly; breaking: dust + splinters burst, switch to the debris image. Already broken in the save -> debris shown at once.
    /// </summary>
    public sealed class BreakableMechanism : Mechanism
    {
        private int hits;
        private bool announced;
        private readonly List<SpriteRenderer> pieces = new List<SpriteRenderer>();
        private readonly List<float> widths = new List<float>();
        private Sprite[] whole, broken, damaged, debris;
        private int totalHits;
        private static Sprite chip;

        public override bool ReactsToStrike => true;

        public override void Init()
        {
            hits = totalHits = Mathf.Max(1, Int("hits", 3));
            whole = LoadAll(Str("sprite", ""));
            broken = LoadAll(Str("sprite_broken", ""));
            damaged = LoadAll(Str("sprite_damaged", ""));
            debris = LoadAll(Str("debris", ""));
            if (Art.Count > 0) BuildFromArt();
            else if (whole.Length > 0) Build();
            if (Ctx.Flags.Has(Str("sets", Name + "_broken"))) { Done = true; ShowBroken(); }
        }

        public override void OnFlagsChanged()
        {
            if (Done || !Ctx.Flags.Has(Str("sets", Name + "_broken"))) return;   // F5 / another mechanism set the flag
            Done = true;
            ShowBroken();
        }

        public override void Tick(Vector2 playerPos, bool inside)
        {
            if (announced || Done || Vector2.Distance(playerPos, Rect.center) >= 1.0f) return;
            announced = true;
            Hints.Show(Str("hint", "Rotten wood. A few strikes would break it."), 2.5f);
        }

        public override void OnStruck()
        {
            if (Done) return;
            // `requires` = flags needed before it can be slashed (e.g. the Map 4 winch chain needs the katana); missing -> `locked_text`
            if (!Ctx.Flags.HasAll(Str("requires", "")))
            {
                foreach (var p in pieces) if (p != null) Ctx.Run(Jiggle(p.transform, 0.1f, 0.015f));
                Hints.Show(Str("locked_text", "It will not break. Not like this."), 2.2f);
                return;
            }
            // `min_radius` > 0 = only heavy hits break it (Earthshatter ~1.1; a sword slash ~0.45). Light hits: small shake + `weak_hint`.
            float minR = Float("min_radius", 0f);
            if (minR > 0f && MapMechanics.StrikeRadius < minR)
            {
                foreach (var p in pieces) if (p != null) Ctx.Run(Jiggle(p.transform, 0.1f, 0.015f));
                Hints.Show(Str("weak_hint", "Your blade only scratches it. It needs a heavier blow."), 2.2f);
                return;
            }
            hits--;
            Ctx.Run(Ctx.ShakeGate(Name));
            foreach (var p in pieces) if (p != null) Ctx.Run(Jiggle(p.transform, 0.18f, 0.035f));
            Ctx.Run(Chips(6));
            string what = (Name + " " + Str("sprite", "")).ToLowerInvariant();
            bool stoneLike = minR > 0f || what.Contains("rubble") || what.Contains("stone") || what.Contains("rock") || what.Contains("slab"); // sound
            if (hits > 0) { ShowDamage(); if (!stoneLike) SfxHooks.MechanismEvent(this, "hit", "hit_wood"); return; }
            Done = true;
            SfxHooks.MechanismEvent(this, "break", stoneLike ? "break_stone" : "break_wood");
            Hints.Show(Str("text", "It breaks apart."), 2.5f);
            Ctx.Flags.Set(Str("sets", Name + "_broken"));
            var psr = PlayerSprite();
            if (psr != null) CutsceneFx.Burst(Ctx.Host, new Vector3(Rect.center.x, Rect.center.y, 0f), Rect.width * 0.4f, Rect.height * 0.3f, 5, 0, psr.sortingLayerID, psr.sortingOrder);
            Ctx.Run(Chips(16));
            CameraFollow.KickMain(Vector2.up, 0.05f, 0.18f);
            ShowBroken();
        }

        // ------------------------------------------------------------------ images
        private static Sprite[] LoadAll(string paths)
        {
            var list = new List<Sprite>();
            foreach (var raw in paths.Split(','))
            {
                string path = raw.Trim();
                if (path.Length == 0) continue;
                var sp = Resources.Load<Sprite>(path);
                if (sp != null) list.Add(sp);
                else Debug.LogWarning("[Breakable] Sprite not found: " + path);
            }
            return list.ToArray();
        }

        private SpriteRenderer PlayerSprite() =>
            Ctx.Director != null && Ctx.Director.Player != null ? Ctx.Director.Player.GetComponentInChildren<SpriteRenderer>() : null;

        private void Build()
        {
            int n = Mathf.Max(1, Int("sprite_count", 1));
            bool vertical = Rect.height > Rect.width * 1.2f;
            float w = Float("sprite_width", vertical ? Rect.width * 1.1f : Rect.width / n);
            var psr = PlayerSprite();
            for (int i = 0; i < n; i++)
            {
                var sr = new GameObject("Breakable " + Name + " " + i).AddComponent<SpriteRenderer>();
                sr.transform.SetParent(Ctx.Host.transform, true);
                var spr = whole[i % whole.Length];
                sr.sprite = spr;
                sr.spriteSortPoint = SpriteSortPoint.Pivot;
                if (psr != null) { sr.sortingLayerID = psr.sortingLayerID; sr.sortingOrder = psr.sortingOrder; sr.sharedMaterial = psr.sharedMaterial; }
                float s = w / Mathf.Max(0.01f, spr.bounds.size.x);
                sr.transform.localScale = new Vector3(s, s, 1f);
                // image feet: horizontal area -> spread evenly along the bottom edge; vertical area -> split evenly over the height (lowest copy at the bottom edge)
                float x = vertical ? Rect.center.x : Rect.xMin + Rect.width * (i + 0.5f) / n;
                float y = (vertical ? Rect.yMin + Rect.height * i / n : Rect.yMin) - spr.bounds.min.y * s;
                sr.transform.position = new Vector3(x, y, 0f);
                pieces.Add(sr);
                widths.Add(w);
            }
        }

        /// <summary>Each image piece = one `art_for` tile object (drawn to fit its frame as in Tiled).</summary>
        private void BuildFromArt()
        {
            var psr = PlayerSprite();
            for (int i = 0; i < Art.Count; i++)
            {
                var a = Art[i];
                var spr = a.Resolve("sprite");
                if (spr == null) continue;
                var sr = new GameObject("Breakable " + Name + " " + i).AddComponent<SpriteRenderer>();
                sr.transform.SetParent(Ctx.Host.transform, true);
                sr.spriteSortPoint = SpriteSortPoint.Pivot;
                if (psr != null) { sr.sortingLayerID = psr.sortingLayerID; sr.sortingOrder = psr.sortingOrder; sr.sharedMaterial = psr.sharedMaterial; }
                a.Apply(sr, spr);
                pieces.Add(sr);
                widths.Add(a.Box.width);
            }
        }

        /// <summary>Swap to the damaged image matching the hits taken (the last hit is reserved for breaking).</summary>
        private void ShowDamage()
        {
            if (damaged.Length == 0 || pieces.Count == 0) return;
            int taken = totalHits - hits;
            int idx = taken * damaged.Length / Mathf.Max(1, totalHits - 1) - 1;
            if (idx < 0) return;
            var spr = damaged[Mathf.Min(idx, damaged.Length - 1)];
            for (int i = 0; i < pieces.Count; i++)
            {
                var p = pieces[i];
                if (p == null || p.sprite == spr) continue;
                float s = widths[i] / Mathf.Max(0.01f, spr.bounds.size.x);
                float footY = p.transform.position.y + p.sprite.bounds.min.y * p.transform.localScale.y;
                p.sprite = spr;
                p.transform.localScale = new Vector3(s, s, 1f);
                p.transform.position = new Vector3(p.transform.position.x, footY - spr.bounds.min.y * s, 0f);
            }
        }

        /// <summary>Plank debris scattered on the floor (below characters); positions fixed by name so the next load is identical.</summary>
        private void ScatterDebris()
        {
            var psr = PlayerSprite();
            var rng = new System.Random(Name.GetHashCode());
            int n = Mathf.Max(1, Int("debris_count", 10));
            float k = Float("debris_scale", 1f);
            for (int i = 0; i < n; i++)
            {
                var sr = new GameObject("Debris " + Name + " " + i).AddComponent<SpriteRenderer>();
                sr.transform.SetParent(Ctx.Host.transform, true);
                sr.sprite = debris[rng.Next(debris.Length)];
                sr.flipX = rng.Next(2) == 0;
                if (psr != null) { sr.sortingLayerID = psr.sortingLayerID; sr.sortingOrder = psr.sortingOrder - 6; sr.sharedMaterial = psr.sharedMaterial; }
                float x = Rect.xMin + (float)rng.NextDouble() * Rect.width;
                float y = Rect.yMin - 0.06f + (float)rng.NextDouble() * (Rect.height > Rect.width * 1.2f ? Rect.height : Mathf.Min(Rect.height, 0.5f));   // vertical area: spread over the full height
                sr.transform.position = new Vector3(x, y, 0f);
                sr.transform.localScale = new Vector3(k, k, 1f);
            }
        }

        private void ShowBroken()
        {
            if (debris.Length > 0)
            {
                foreach (var p in pieces) if (p != null) p.gameObject.SetActive(false);
                ScatterDebris();
                return;
            }
            float k = Float("broken_scale", 0.85f);
            for (int i = 0; i < pieces.Count; i++)
            {
                var p = pieces[i];
                if (p == null) continue;
                if (broken.Length == 0) { p.gameObject.SetActive(false); continue; }
                var b = broken[i % broken.Length];
                float s = widths[i] * k / Mathf.Max(0.01f, b.bounds.size.x);
                p.sprite = b;
                p.transform.localScale = new Vector3(s, s, 1f);
                p.sortingOrder -= 6;                                             // debris lies below characters
            }
        }

        private static IEnumerator Jiggle(Transform t, float seconds, float amp)
        {
            if (t == null) yield break;
            Vector3 p0 = t.position;
            for (float e = 0f; e < seconds && t != null; e += Time.deltaTime)
            {
                t.position = p0 + new Vector3(Mathf.Sin(e * 90f) * amp * (1f - e / seconds), 0f, 0f);
                yield return null;
            }
            if (t != null) t.position = p0;
        }

        /// <summary>Wood splinter: a small brown bit flying in an arc, then lying still and fading.</summary>
        private IEnumerator Chips(int n)
        {
            if (chip == null)
            {
                var tex = new Texture2D(3, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                var c = new Color32(122, 86, 52, 255); var d = new Color32(84, 56, 34, 255);
                tex.SetPixels32(new[] { c, c, d, d, c, c }); tex.Apply();
                chip = Sprite.Create(tex, new Rect(0, 0, 3, 2), new Vector2(0.5f, 0.5f), 100f);
            }
            var psr = PlayerSprite();
            var list = new List<(SpriteRenderer sr, Vector2 from, Vector2 to, float arc)>();
            for (int i = 0; i < n; i++)
            {
                var sr = new GameObject("Wood Chip").AddComponent<SpriteRenderer>();
                sr.sprite = chip;
                if (psr != null) { sr.sortingLayerID = psr.sortingLayerID; sr.sortingOrder = psr.sortingOrder + 1; }
                Vector2 from = new Vector2(Random.Range(Rect.xMin, Rect.xMax), Random.Range(Rect.yMin, Rect.yMax));
                Vector2 to = from + Random.insideUnitCircle * 0.5f + Vector2.down * 0.15f;
                sr.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
                list.Add((sr, from, to, Random.Range(0.15f, 0.35f)));
            }
            const float dur = 0.35f;
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                float u = t / dur;
                foreach (var c in list)
                    if (c.sr != null) c.sr.transform.position = Vector2.Lerp(c.from, c.to, u) + Vector2.up * (c.arc * 4f * u * (1f - u));
                yield return null;
            }
            yield return new WaitForSeconds(0.8f);
            for (float t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                foreach (var c in list) if (c.sr != null) c.sr.color = new Color(1f, 1f, 1f, 1f - t / 0.5f);
                yield return null;
            }
            foreach (var c in list) if (c.sr != null) Object.Destroy(c.sr.gameObject);
        }
    }
}
