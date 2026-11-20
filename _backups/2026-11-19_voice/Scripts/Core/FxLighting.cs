using UnityEngine;

namespace SoulRequiem.Core
{
    /// <summary>
    /// Đạn, vạch cảnh báo, hiệu ứng trúng đòn phải luôn đọc rõ dù đứng trong bóng tối:
    /// dùng vật liệu Sprite-Unlit (tự sáng, không bị Global Light 2D làm tối).
    /// Gọi FxLighting.MakeUnlit(renderer) ngay sau khi tạo SpriteRenderer / LineRenderer cho hiệu ứng nguy hiểm.
    /// </summary>
    public static class FxLighting
    {
        private static Material unlit;

        public static Material Unlit
        {
            get
            {
                if (unlit == null)
                {
                    var sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                    if (sh == null) sh = Shader.Find("Sprites/Default");
                    unlit = new Material(sh) { name = "SR Unlit FX" };
                }
                return unlit;
            }
        }

        public static void MakeUnlit(Renderer r)
        {
            if (r != null) r.sharedMaterial = Unlit;
        }
    }
}
