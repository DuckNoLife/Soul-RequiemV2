// Soul Requiem — make the player sort by the FEET (like map props and enemies).
//
// Why: the map uses Transparency Sort = Custom Axis (0,1,0); props (SuperTiled2Unity) and enemies sort by the image's feet.
// The player sprites are trimmed tightly (a different rect per frame) with pivot = rect Center,
// so Unity thinks the player "stands" at belly height -> props whose feet lie between belly and feet (logs, bridge ropes, stakes...)
// get drawn over the body. A centre pivot also makes the sprite jitter slightly between frames (each frame has its own centre).
//
// Menu "Soul Requiem ▸ Player ▸ Sort Player by Feet (Sprite Pivots)": takes EVERY image in the Player's Animator,
//   sets the pivot at the frame's foot point (measured from the image, shared per folder), Sort Point = Pivot, Order = 2.
// Menu "Soul Requiem ▸ Player ▸ Fit Player Collider to Feet": 0.6 x 0.35 tile collision collider right under the feet (run after the first one).
// After each command: Ctrl+S to save the scene. Both are declared in SoulRequiemMenu (Player ▸ Rebuild Player runs them in order).
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using SoulRequiem.Core;
using SoulRequiem.Gameplay;

namespace SoulRequiem.EditorTools
{
    public static class PlayerFeetSorting
    {

        static bool Busy()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode) return false;
            EditorUtility.DisplayDialog("Soul Requiem", "Stop Play Mode before running this command.", "OK");
            return true;
        }

        public static void Run()
        {
            if (Busy()) return;
            var pc = Object.FindFirstObjectByType<PlayerController>();
            if (pc == null) { EditorUtility.DisplayDialog("Soul Requiem", "No Player found in the scene.", "OK"); return; }
            var sr = pc.GetComponentInChildren<SpriteRenderer>();
            var anim = pc.GetComponentInChildren<Animator>();

            // 1) Images actually used: every sprite in the Animator's clips + the currently assigned sprite
            var texPaths = new System.Collections.Generic.HashSet<string>();
            if (sr != null && sr.sprite != null) texPaths.Add(AssetDatabase.GetAssetPath(sr.sprite.texture));
            if (anim != null && anim.runtimeAnimatorController != null)
                foreach (var clip in anim.runtimeAnimatorController.animationClips)
                    foreach (var b in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                        foreach (var k in AnimationUtility.GetObjectReferenceCurve(clip, b))
                            if (k.value is Sprite sp) texPaths.Add(AssetDatabase.GetAssetPath(sp.texture));

            // Ren v2 images already have pivot = REAL feet for each direction's standing pose (ren_import.py), and the weapon anchor table
            // is computed from that pivot -> do NOT overwrite (frame centre / lowest row is a few px off and the weapon leaves the hand).
            texPaths.RemoveWhere(p => p != null && p.Replace('\\', '/').StartsWith("Assets/GamePackage/Ren/"));

            // 2) Foot point per folder (each folder = 1 direction of 1 animation, same frame size):
            //    x = frame centre, y = lowest row with pixels (most common value in the folder)
            var byFolder = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>();
            foreach (var p in texPaths)
            {
                var d = System.IO.Path.GetDirectoryName(p);
                if (!byFolder.TryGetValue(d, out var l)) byFolder[d] = l = new System.Collections.Generic.List<string>();
                l.Add(p);
            }
            int changed = 0;
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            foreach (var kv in byFolder)
            {
                var feetCount = new System.Collections.Generic.Dictionary<int, int>();
                int W = 0;
                foreach (var p in kv.Value)
                {
                    var t = new Texture2D(2, 2);
                    t.LoadImage(System.IO.File.ReadAllBytes(p));
                    W = t.width;
                    int feet = 0;
                    for (int y = 0; y < t.height && feet == 0; y++)
                        for (int x = 0; x < t.width; x++)
                            if (t.GetPixel(x, y).a > 0.5f) { feet = y; break; }
                    feetCount[feet] = feetCount.TryGetValue(feet, out var c) ? c + 1 : 1;
                    Object.DestroyImmediate(t);
                }
                int fy = 0, best = -1;
                foreach (var f in feetCount) if (f.Value > best) { best = f.Value; fy = f.Key; }
                var feetPx = new Vector2(W / 2f, fy);
                foreach (var p in kv.Value)
                {
                    var ti = AssetImporter.GetAtPath(p) as TextureImporter;
                    if (ti == null) continue;
                    var dp = factory.GetSpriteEditorDataProviderFromObject(ti);
                    dp.InitSpriteEditorDataProvider();
                    var rects = dp.GetSpriteRects();
                    bool dirty = false;
                    foreach (var r in rects)
                    {
                        var want = new Vector2((feetPx.x - r.rect.x) / r.rect.width, (feetPx.y - r.rect.y) / r.rect.height);
                        if (r.alignment == SpriteAlignment.Custom && (r.pivot - want).sqrMagnitude < 1e-6f) continue;
                        r.alignment = SpriteAlignment.Custom;
                        r.pivot = want;
                        dirty = true;
                    }
                    if (!dirty) continue;
                    dp.SetSpriteRects(rects);
                    dp.Apply();
                    ti.SaveAndReimport();
                    changed++;
                }
            }

            // 3) Player sorts on the same layer as props, by pivot (feet)
            if (sr != null)
            {
                Undo.RecordObject(sr, "Feet sorting");
                sr.spriteSortPoint = SpriteSortPoint.Pivot;
                sr.sortingLayerName = "Default";
                sr.sortingOrder = 2;
                EditorSceneManager.MarkSceneDirty(pc.gameObject.scene);
            }
            Debug.Log($"[Soul Requiem] Feet sorting: {texPaths.Count} images in the Animator, {changed} pivots changed.");
            EditorUtility.DisplayDialog("Soul Requiem",
                $"Player images in the Animator: {texPaths.Count}, pivots adjusted: {changed} (0 = already correct).\n" +
                "Player: Order in Layer = 2, Sort Point = Pivot.\n\nNext run \"Fit Player Collider to Feet\", then Ctrl+S.", "OK");
        }

        public static void FeetCollider()
        {
            if (Busy()) return;
            var pc = Object.FindFirstObjectByType<PlayerController>();
            if (pc == null) { EditorUtility.DisplayDialog("Soul Requiem", "No Player found in the scene.", "OK"); return; }
            var sr = pc.GetComponentInChildren<SpriteRenderer>();
            if (sr == null || sr.spriteSortPoint != SpriteSortPoint.Pivot)
            {
                EditorUtility.DisplayDialog("Soul Requiem", "Run \"Sort Player by Feet (Sprite Pivots)\" first.", "OK");
                return;
            }
            var s = pc.transform.lossyScale;
            const float tile = 0.32f;   // 32 px, PPU 100
            var size = new Vector2(0.6f * tile / Mathf.Abs(s.x), 0.35f * tile / Mathf.Abs(s.y));
            var cap = pc.GetComponent<CapsuleCollider2D>();
            if (cap == null) cap = Undo.AddComponent<CapsuleCollider2D>(pc.gameObject);
            Undo.RecordObject(cap, "Feet collider");
            cap.direction = CapsuleDirection2D.Horizontal;
            cap.size = size;
            cap.offset = new Vector2(0f, size.y * 0.5f);   // transform = feet (pivot) -> the collider sits right on it
            cap.isTrigger = false;
            EditorSceneManager.MarkSceneDirty(pc.gameObject.scene);
            Debug.Log($"[Soul Requiem] Player collider {size.x:0.###} x {size.y:0.###} (local) at the feet.");
            EditorUtility.DisplayDialog("Soul Requiem", "Player collider moved to the feet (0.6 x 0.35 tiles).\nPress Ctrl+S to save the scene.", "OK");
        }
    }
}
