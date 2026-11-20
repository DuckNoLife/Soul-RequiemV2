using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 05/11: passage — lối đi tắt trong cùng map (thang bảo trì, nắp hầm, đường ống chui...): nhấn T -> tối dần -> Ren ra ở đầu bên kia.
    /// Lối tắt MỘT CHIỀU kiểu Souls = 2 object: đầu "bên trong" có `sets` (mở then), đầu "bên ngoài" có `requires` cùng cờ đó + `locked_say`.
    /// Thuộc tính Tiled (object lớp mechanics, vùng = chỗ đứng để tương tác):
    ///   to            "x,y" ô Tiled nơi Ren xuất hiện (thường cạnh đầu bên kia). Bắt buộc.
    ///   verb          chữ trên nhắc phím (mặc định "Climb")
    ///   requires      cờ cần có để dùng (vd boil_hatch_open). Chưa có -> Ren nói `locked_say`.
    ///   sets          cờ bật khi dùng (lần đầu = mở then cho đầu kia). first_text: dòng chữ lần đầu, text: các lần sau.
    ///   sprite        ảnh (Resources) đặt giữa mép dưới vùng, nằm dưới nhân vật; sprite_open: ảnh khi đã dùng được (để trống = như sprite).
    /// Đang giao chiến thì không dùng được (không cho chạy khỏi trận / đấu trường).
    /// CHỈ dùng cho lối đổi tầng thật (lên mặt đất, xuống hầm khác) — game top-down, trong cùng mặt sàn thì dùng cửa / cổng (NOTES §153).
    /// 06/11: có thể đặt bằng tile object (lớp `mechanics_art`): hình vẽ khít khung, vùng tương tác = khung.
    /// </summary>
    public sealed class PassageMechanism : Mechanism
    {
        private SpriteRenderer shown;
        private Sprite closedSprite, openSprite;

        public override string DefaultVerb => "Climb";

        private bool Open => Ctx.Flags.HasAll(Str("requires", ""));

        public override void Init()
        {
            var art = Art.Count > 0 ? Art[0] : null;
            closedSprite = art != null ? art.Resolve("sprite") : Load(Str("sprite", ""));
            openSprite = Load(Str("sprite_open", ""));
            if (closedSprite == null && openSprite == null) return;
            shown = new GameObject("Passage " + Name).AddComponent<SpriteRenderer>();
            shown.transform.SetParent(Ctx.Host.transform, false);
            shown.transform.position = new Vector3(Rect.center.x, Rect.yMin, 0f);
            if (art != null) art.Apply(shown, closedSprite != null ? closedSprite : openSprite);
            var psr = Ctx.Player != null ? Ctx.Player.GetComponentInChildren<SpriteRenderer>() : null;
            if (psr != null) { shown.sortingLayerID = psr.sortingLayerID; shown.sortingOrder = psr.sortingOrder - 1; shown.sharedMaterial = psr.sharedMaterial; }
            Refresh();
        }

        public override void OnFlagsChanged() => Refresh();

        private void Refresh()
        {
            if (shown == null) return;
            var s = Open && openSprite != null ? openSprite : closedSprite;
            if (s == null) s = openSprite;
            if (shown.sprite != s) shown.sprite = s;
        }

        private static Sprite Load(string path) => string.IsNullOrEmpty(path) ? null : Resources.Load<Sprite>(path);

        public override void Use()
        {
            var player = Ctx.Player;
            if (player == null || SceneTransition.Busy) return;
            if (Ctx.Director != null && Ctx.Director.InCombat) { KeyRing.Say(player, "Not now - not with them on me."); return; }
            if (!Open) { KeyRing.Say(player, Str("locked_say", "It won't budge. Something holds it from the other side.")); return; }
            string sets = Str("sets", "");
            bool first = sets.Length > 0 && !Ctx.Flags.HasAll(sets);
            if (sets.Length > 0) Ctx.Flags.Set(sets);
            string text = first ? Str("first_text", Str("text", "")) : Str("text", "");
            Vector3 to = Ctx.TilePoint(Str("to", ""), player.position);
            SceneTransition.Warp(player, new Vector3(to.x, to.y, player.position.z), text);
        }
    }
}
