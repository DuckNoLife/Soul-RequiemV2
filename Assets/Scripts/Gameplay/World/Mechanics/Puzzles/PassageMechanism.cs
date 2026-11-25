using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// passage - shortcut within the same map (maintenance ladder, trapdoor, crawl pipe...): press T -> fade out -> Ren comes out at the other end.
    /// ONE-WAY Souls-style shortcut = 2 objects: the "inside" end has `sets` (unbolts it), the "outside" end has `requires` with the same flag + `locked_say`.
    /// Tiled properties (object in the mechanics layer, area = where to stand to interact):
    ///   to            "x,y" Tiled tile where Ren appears (usually next to the other end). Required.
    ///   verb          key prompt text (default "Climb")
    ///   requires      flags needed to use it (e.g. boil_hatch_open). Missing -> Ren says `locked_say`.
    ///   sets          flag set on use (first use = unbolts the other end). first_text: line on first use, text: on later uses.
    ///   sprite        image (Resources) at the middle of the area's bottom edge, below characters; sprite_open: image once usable (empty = same as sprite).
    /// Not usable in combat (no running away from a fight / arena).
    /// ONLY for real level changes (up to the surface, down to another cellar) - the game is top-down, so on the same floor use doors / gates.
    /// Can be placed as a tile object (`mechanics_art` layer): the image fits the frame, interaction area = the frame.
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
