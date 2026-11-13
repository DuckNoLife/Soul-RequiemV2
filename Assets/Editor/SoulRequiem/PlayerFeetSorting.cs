// Soul Requiem — cho nhân vật xếp lớp theo CHÂN (giống đồ vật trên map và quái).
//
// Vì sao: map dùng Transparency Sort = Custom Axis (0,1,0); đồ vật (SuperTiled2Unity) và quái xếp theo chân ảnh.
// Sprite nhân vật được cắt sát hình (rect khác nhau từng khung) với pivot = Center của rect,
// nên Unity coi nhân vật "đứng" ở ngang bụng -> vật có chân nằm giữa bụng và bàn chân (khúc gỗ, dây cầu, cọc...)
// bị vẽ đè lên người. Pivot giữa rect còn làm hình nhân vật rung nhẹ giữa các khung (mỗi khung một tâm).
//
// Menu 1 "Xếp lớp nhân vật theo chân": lấy MỌI ảnh trong Animator của Player (hiện là bộ Rogue Traveler),
//   đặt pivot vào điểm bàn chân của khung (đo từ ảnh, chung cho cả thư mục), Sort Point = Pivot, Order = 2.
// Menu 2 "Collider nhân vật gọn ở chân": collider va chạm 0,6 x 0,35 ô ngay dưới bàn chân (chạy sau menu 1).
// Sau mỗi lệnh: Ctrl+S lưu scene.
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
            EditorUtility.DisplayDialog("Soul Requiem", "Hãy dừng Play Mode trước khi chạy lệnh này.", "OK");
            return true;
        }

        // 07/11: menu gom về SoulRequiemMenu.cs (tiếng Anh) — trước: [MenuItem("Soul Requiem/Xếp lớp nhân vật theo chân")]
        public static void Run()
        {
            if (Busy()) return;
            var pc = Object.FindFirstObjectByType<PlayerController>();
            if (pc == null) { EditorUtility.DisplayDialog("Soul Requiem", "Không thấy Player trong scene.", "OK"); return; }
            var sr = pc.GetComponentInChildren<SpriteRenderer>();
            var anim = pc.GetComponentInChildren<Animator>();

            // 1) Ảnh thật sự dùng: mọi sprite trong các clip của Animator + sprite đang gán
            var texPaths = new System.Collections.Generic.HashSet<string>();
            if (sr != null && sr.sprite != null) texPaths.Add(AssetDatabase.GetAssetPath(sr.sprite.texture));
            if (anim != null && anim.runtimeAnimatorController != null)
                foreach (var clip in anim.runtimeAnimatorController.animationClips)
                    foreach (var b in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                        foreach (var k in AnimationUtility.GetObjectReferenceCurve(clip, b))
                            if (k.value is Sprite sp) texPaths.Add(AssetDatabase.GetAssetPath(sp.texture));

            // 09/11 (§201): ảnh Ren v2 đã có pivot = chân THẬT theo tư thế đứng từng hướng (ren_import.py) và bảng neo vũ khí
            // tính theo đúng pivot đó -> KHÔNG ghi đè (giữa khung / hàng thấp nhất lệch vài px làm vũ khí rời tay).
            texPaths.RemoveWhere(p => p != null && p.Replace('\\', '/').StartsWith("Assets/GamePackage/Ren/"));

            // 2) Điểm chân theo từng thư mục (mỗi thư mục = 1 hướng của 1 animation, cùng cỡ khung):
            //    x = giữa khung, y = hàng thấp nhất có hình (giá trị gặp nhiều nhất trong thư mục)
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

            // 3) Player xếp cùng lớp với đồ vật, theo pivot (bàn chân)
            if (sr != null)
            {
                Undo.RecordObject(sr, "Feet sorting");
                sr.spriteSortPoint = SpriteSortPoint.Pivot;
                sr.sortingLayerName = "Default";
                sr.sortingOrder = 2;
                EditorSceneManager.MarkSceneDirty(pc.gameObject.scene);
            }
            Debug.Log($"[Soul Requiem] Xếp lớp theo chân: {texPaths.Count} ảnh trong Animator, {changed} ảnh đổi pivot.");
            EditorUtility.DisplayDialog("Soul Requiem",
                $"Ảnh nhân vật trong Animator: {texPaths.Count}, vừa chỉnh pivot: {changed} (0 = đã đúng).\n" +
                "Player: Order in Layer = 2, Sort Point = Pivot.\n\nTiếp theo chạy \"Collider nhân vật gọn ở chân\", rồi Ctrl+S.", "OK");
        }

        // 07/11: menu gom về SoulRequiemMenu.cs (tiếng Anh) — trước: [MenuItem("Soul Requiem/Collider nhân vật gọn ở chân")]
        public static void FeetCollider()
        {
            if (Busy()) return;
            var pc = Object.FindFirstObjectByType<PlayerController>();
            if (pc == null) { EditorUtility.DisplayDialog("Soul Requiem", "Không thấy Player trong scene.", "OK"); return; }
            var sr = pc.GetComponentInChildren<SpriteRenderer>();
            if (sr == null || sr.spriteSortPoint != SpriteSortPoint.Pivot)
            {
                EditorUtility.DisplayDialog("Soul Requiem", "Chạy \"Xếp lớp nhân vật theo chân\" trước.", "OK");
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
            cap.offset = new Vector2(0f, size.y * 0.5f);   // transform = bàn chân (pivot) -> collider nằm ngay trên đó
            cap.isTrigger = false;
            EditorSceneManager.MarkSceneDirty(pc.gameObject.scene);
            Debug.Log($"[Soul Requiem] Collider Player {size.x:0.###} x {size.y:0.###} (local) ở bàn chân.");
            EditorUtility.DisplayDialog("Soul Requiem", "Collider nhân vật đã thu về bàn chân (0,6 x 0,35 ô).\nNhấn Ctrl+S để lưu scene.", "OK");
        }
    }
}
