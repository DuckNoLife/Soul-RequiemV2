using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// One-shot sprite effect that destroys itself (VFX made with the PixelLab VFX Workbench).
    /// Images in Assets/Resources/VFX/&lt;name&gt;.png, pre-sliced into frames &lt;name&gt;_0, _1, ... (pivot = the effect's anchor point).
    ///   slash_E / slash_SE / slash_S / slash_NE / slash_N : Iaido slash trails (west = flipped X)
    ///   hit            : hit on an enemy (flash + sparks + blood)
    ///   ground_impact  : F ground slam (wave ring + cracks)
    /// Also: Looping / SpawnAttached (follow an object), Tint, sound per effect (VfxSfx table).
    /// </summary>
    public class SpriteVfx : MonoBehaviour
    {
        private static readonly Dictionary<string, Sprite[]> cache = new Dictionary<string, Sprite[]>();

        private Sprite[] frames;
        private float frameTime;
        private float t;
        private SpriteRenderer sr;
        private bool loop;
        private float endAt;

        /// Load the frames of an effect (by the number at the end of the name). Returns an empty array if the image is missing.
        public static Sprite[] Load(string name)
        {
            if (cache.TryGetValue(name, out var s)) return s;
            var all = Resources.LoadAll<Sprite>("VFX/" + name);
            var list = new List<Sprite>(all);
            list.Sort((a, b) => Index(a.name).CompareTo(Index(b.name)));
            s = list.ToArray();
            if (s.Length == 0) Debug.LogWarning($"[VFX] Not found: Resources/VFX/{name}.png");
            cache[name] = s;
            return s;
        }

        private static int Index(string n)
        {
            int u = n.LastIndexOf('_');
            return u >= 0 && int.TryParse(n.Substring(u + 1), out int i) ? i : 0;
        }

        /// Spawn the effect at a position (the image pivot lands exactly on pos). scale = ratio to 100 px / unit.
        /// sfx = false -> decorative constantly looping effects (rest point sparks...) do NOT play the VfxSfx sound.
        public static SpriteVfx Spawn(string name, Vector2 pos, float scale, int sortingLayerId, int sortingOrder,
                                      bool flipX = false, float fps = 16f, bool sfx = true)
        {
            var f = Load(name);
            if (f.Length == 0) return null;
            if (sfx) SfxHooks.Vfx(name, pos);   // sound
            var go = new GameObject("vfx_" + name);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            go.transform.localScale = new Vector3(scale, scale, 1f);
            var r = go.AddComponent<SpriteRenderer>();
            FxLighting.MakeUnlit(r);
            r.sortingLayerID = sortingLayerId;
            r.sortingOrder = sortingOrder;
            r.flipX = flipX;
            r.sprite = f[0];
            var v = go.AddComponent<SpriteVfx>();
            v.frames = f;
            v.frameTime = 1f / Mathf.Max(1f, fps);
            v.sr = r;
            return v;
        }

        /// Looping effect attached to an object (e.g. stun stars over a head) for `seconds`.
        public static SpriteVfx SpawnAttached(string name, Transform parent, float scale, int sortingLayerId, int sortingOrder,
                                              float fps, float seconds)
        {
            var v = Spawn(name, parent.position, scale, sortingLayerId, sortingOrder, false, fps);
            if (v == null) return null;
            v.transform.SetParent(parent, true);
            v.loop = true;
            v.endAt = Time.time + seconds;
            return v;
        }

        public void Stop() { if (this != null) Destroy(gameObject); }

        /// Tint the effect (e.g. white warning line -> blood red).
        public SpriteVfx Tint(Color c) { if (sr != null) sr.color = c; return this; }

        /// Switch an effect (spawned in place) to looping for `seconds`, e.g. a blood pool on the ground.
        public SpriteVfx Looping(float seconds) { loop = true; endAt = Time.time + seconds; return this; }

        /// the looping effect lasts at least `seconds` more (the exposed mark is refreshed every Ichor pool tick).
        public SpriteVfx Extend(float seconds) { if (loop) endAt = Mathf.Max(endAt, Time.time + seconds); return this; }

        /// Follow an object (still plays once), e.g. a heal effect following the player.
        public SpriteVfx Follow(Transform parent) { transform.SetParent(parent, true); return this; }

        private void Update()
        {
            t += Time.deltaTime;
            int i = Mathf.FloorToInt(t / frameTime);
            if (loop)
            {
                if (Time.time >= endAt) { Destroy(gameObject); return; }
                i %= frames.Length;
            }
            else if (i >= frames.Length) { Destroy(gameObject); return; }
            sr.sprite = frames[i];
        }
    }
}
