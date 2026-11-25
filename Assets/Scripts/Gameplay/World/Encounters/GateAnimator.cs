using System.Collections;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// ANIMATED gate image (cell door sliding up, skull gate, iron / wooden door, boss door) instead of just showing / hiding a sprite.
    /// Added to a `gate_visual` object with property `anim` = frame strip name in Resources/VFX (frame 0 = CLOSED, last frame = OPEN;
    /// sliced with Catalog/v2/tools/traps196/slice_traps.py), `anim_fps` (default 14). The Tiled tile object uses the frame 0 image
    /// (tileset SR-traps.tsx) so what you place is what you see. GateController calls <see cref="SetClosed"/>; during the first second after the scene loads it snaps instead of animating.
    /// </summary>
    public sealed class GateAnimator : MonoBehaviour
    {
        private Sprite[] frames;
        private float fps = 14f;
        private SpriteRenderer shown;
        private bool initialized;
        private int current;
        private Coroutine running;
        private string animName = ""; // sound

        public void Setup(string anim, float framesPerSecond)
        {
            frames = TrapArt.Frames(anim);
            animName = anim; // sound
            fps = Mathf.Max(1f, framesPerSecond);
        }

        private bool Prepare()
        {
            if (shown != null) return true;
            if (frames == null || frames.Length == 0) return false;
            var orig = GetComponentInChildren<SpriteRenderer>(true);
            if (orig == null) return false;
            // aligned by the TOP-LEFT edge of the map image (trim_props may crop the empty strip under the image in Tiled, the top edge stays);
            // animation frames have a bottom-left pivot -> frame base = top edge - frame height
            Vector3 bl = orig.transform.position;
            if (orig.sprite != null)
            {
                Vector2 pv = orig.sprite.pivot / orig.sprite.pixelsPerUnit;
                float hOrig = orig.sprite.rect.height / orig.sprite.pixelsPerUnit;
                float hFrame = frames[0].rect.height / frames[0].pixelsPerUnit;
                bl = orig.transform.TransformPoint(new Vector3(-pv.x, -pv.y + hOrig - hFrame, 0f));
            }
            var g = new GameObject("GateAnim");
            g.transform.SetParent(transform, true);
            g.transform.position = new Vector3(bl.x, bl.y, orig.transform.position.z);
            shown = g.AddComponent<SpriteRenderer>();
            shown.sortingLayerID = orig.sortingLayerID;
            shown.sortingOrder = orig.sortingOrder;
            shown.spriteSortPoint = orig.spriteSortPoint;
            shown.sharedMaterial = orig.sharedMaterial;
            shown.color = orig.color;
            shown.flipX = orig.flipX;
            orig.enabled = false;
            shown.sprite = frames[0];
            return true;
        }

        public void SetClosed(bool closed)
        {
            if (!Prepare()) { gameObject.SetActive(closed); return; }
            int target = closed ? 0 : frames.Length - 1;
            // on map load (initial state / save) snap at once; animate only afterwards
            if ((!initialized && Time.timeSinceLevelLoad < 1f) || !isActiveAndEnabled)
            {
                initialized = true;
                current = target;
                shown.sprite = frames[current];
                return;
            }
            if (running != null) StopCoroutine(running);
            if (target != current) SfxHooks.Gate(animName, closed, transform.position); // sound
            running = StartCoroutine(Play(target));
        }

        private IEnumerator Play(int target)
        {
            float step = 1f / fps;
            while (current != target)
            {
                current += target > current ? 1 : -1;
                shown.sprite = frames[current];
                yield return new WaitForSeconds(step);
            }
            running = null;
        }
    }
}
