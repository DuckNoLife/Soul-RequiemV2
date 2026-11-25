using UnityEngine;

namespace SoulRequiem.Core
{
    /// <summary>
    /// Bullets, warning lines and hit effects must stay readable even in darkness:
    /// use the Sprite-Unlit material (self-lit, not darkened by the Global Light 2D).
    /// Call FxLighting.MakeUnlit(renderer) right after creating the SpriteRenderer / LineRenderer of any dangerous effect.
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
