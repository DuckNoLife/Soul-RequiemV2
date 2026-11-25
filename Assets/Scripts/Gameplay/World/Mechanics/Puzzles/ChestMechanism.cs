using System.Collections;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// chest - a chest opened with the T key, with a lid opening animation (8 frames, Resources/VFX/chest/chest&lt;style&gt;_0..7) and a reward.
    /// Tiled properties (object in the mechanics layer, over the chest image):
    ///   chest          name of the chest image object on the map (tile object). Required.
    ///   style          animation frame set: 2 (wood, default) / 3 (iron)
    ///   requires_key   inventory key id (KeyRing). No key -> Ren says `locked_say`.
    ///   requires       flags needed first (e.g. records_open); missing -> `locked_say`.
    ///   key_id ...     the chest HOLDS a key / key item (defined like KeyRing): opening it puts it in the inventory.
    ///   potions_max    + max flask charges (and refills that many). potions: extra flasks (max not raised).
    ///   skill_id       skill learned (empty: future use). The skill scroll POPS out of the chest and flies into Ren (PickupPop) before it is learned;
    ///                  sets flag new_mantra; `skill_text` = line shown afterwards (default "A mantra scroll. Press {mantras} to attune it.").
    ///   sets           flag set on opening. text: line shown on opening. say: Ren's line after opening.
    /// Opened (flag "&lt;name&gt;_opened", saved per map) -> on map reload the chest sits on its open frame.
    /// </summary>
    public sealed class ChestMechanism : Mechanism
    {
        private SpriteRenderer shown;
        private Sprite[] frames;

        public override string DefaultVerb => "Open";
        private string OpenedFlag => Name + "_opened";

        public override void Init()
        {
            KeyRing.Register(Props);
            SyncFromSave();
        }

        public override void OnFlagsChanged() => SyncFromSave();

        private void SyncFromSave()
        {
            if (Done) return;
            string give = Str("key_id", "");
            string sets = Str("sets", "");
            // opened (or an old save already had flag `sets` before the chest existed) -> lies open, the key inside is already in the inventory
            if (Ctx.Flags.Has(OpenedFlag) || (sets.Length > 0 && Ctx.Flags.HasAll(sets)))
            {
                Done = true;
                ShowFrame(7);
                if (give.Length > 0 && !KeyRing.Has(Ctx.Flags, give)) Ctx.Flags.Set(KeyRing.Flag(give));
            }
        }

        public override void Use()
        {
            if (Done) return;
            var player = Ctx.Director != null ? Ctx.Director.Player : null;
            string key = Str("requires_key", ""), req = Str("requires", "");
            bool keyOk = key.Length == 0 || KeyRing.Has(Ctx.Flags, key);
            if (!keyOk || !Ctx.Flags.HasAll(req))
            {
                KeyRing.Say(player, Str("locked_say", "Locked tight. I need a key."));
                SfxHooks.MechanismEvent(this, "locked", "door_locked"); // sound
                return;
            }
            Done = true;
            SfxHooks.MechanismEvent(this, "open", "chest_open");
            Ctx.Flags.Set(OpenedFlag);
            if (key.Length > 0) Ctx.Flags.Set(KeyRing.UsedFlag(key));
            Ctx.Run(Open(player, key));
        }

        private IEnumerator Open(Transform player, string usedKey)
        {
            if (usedKey.Length > 0) Hints.Show($"Used: {KeyRing.NameOf(usedKey)}.", 1.6f);
            CameraFollow.KickMain(Vector2.down, 0.02f, 0.12f);
            for (int f = 0; f < 8; f++)
            {
                ShowFrame(f);
                yield return new WaitForSeconds(f == 0 ? 0.12f : 0.07f);
            }
            yield return new WaitForSeconds(0.25f);

            // reward
            var combat = player != null ? player.GetComponent<PlayerCombat>() : null;
            int max = Int("potions_max", 0), add = Int("potions", 0);
            if (combat != null && max > 0) combat.AddFlaskSlots(max);                              // capped at MaxFlaskSlots
            if (combat != null && add > 0) combat.AddPotion(add);
            // the skill scroll pops out of the chest, flies into Ren, then the skill is learned
            string skill = Str("skill_id", "");
            bool learned = false;
            if (skill.Length > 0 && player != null)
            {
                Vector2 lid = shown != null ? new Vector2(shown.bounds.center.x, shown.bounds.max.y - 0.15f) : (Vector2)player.position;
                var pop = PickupPop.Launch(lid, player, "skill_scroll", shown != null ? shown.sortingLayerID : 0, (shown != null ? shown.sortingOrder : 2) + 6);
                if (pop != null) yield return new WaitForSeconds(PickupPop.Duration);
                PlayerLoadout.Of(player).Unlock(PlayerLoadout.Find(skill), false);
                Ctx.Flags.Set("new_mantra");                                                       // new-skill flag (the old CombatTutorials try-out screen read it; that tutorial is removed)
                learned = true;
            }
            string sets = Str("sets", "");
            if (sets.Length > 0) Ctx.Flags.Set(sets);
            string text = Str("text", "");
            string give = Str("key_id", "");
            if (give.Length > 0) KeyRing.Give(Ctx.Flags, give, text);
            else if (text.Length > 0) Hints.Show(text, Mathf.Max(4f, text.Length / 16f));
            else if (max > 0) Hints.Show($"Inside: a flask. You can now carry {(combat != null ? combat.maxPotions : 0)} flasks.", 4f);
            KeyRing.Say(player, Str("say", ""));
            if (learned)
            {
                yield return new WaitForSeconds(2.8f);
                Hints.Show(Str("skill_text", "A mantra scroll. Press {mantras} to attune it."), 6f);
            }
        }

        /// <summary>Show frame `f` of the opening animation: the chest image on the map is hidden and a separate image is placed at its bottom-left edge.</summary>
        private void ShowFrame(int f)
        {
            if (frames == null)
            {
                string style = Str("style", "2");
                frames = new Sprite[8];
                for (int i = 0; i < 8; i++) frames[i] = Resources.Load<Sprite>($"VFX/chest/chest{style}_{i}");
            }
            if (frames[f] == null) return;
            if (shown == null)
            {
                SpriteRenderer orig = null;
                foreach (var go in Ctx.NamedObjects(Str("chest", "")))
                {
                    orig = go.GetComponentInChildren<SpriteRenderer>(true);
                    if (orig != null) break;
                }
                if (orig == null) { Debug.LogWarning("[Chest] " + Name + ": chest image not found '" + Str("chest", "") + "'"); return; }
                // bottom-left corner of the map image's frame (the version with the 9 px bottom margin cropped) - the 64x64 animation frames have their pivot exactly there
                Vector3 bl = orig.transform.position;
                if (orig.sprite != null)
                {
                    Vector2 pv = orig.sprite.pivot / orig.sprite.pixelsPerUnit;
                    bl = orig.transform.TransformPoint(new Vector3(-pv.x, -pv.y, 0f));
                }
                var g = new GameObject("Chest " + Name);
                g.transform.position = new Vector3(bl.x, bl.y, orig.transform.position.z);
                shown = g.AddComponent<SpriteRenderer>();
                shown.sortingLayerID = orig.sortingLayerID;
                shown.sortingOrder = orig.sortingOrder;
                shown.spriteSortPoint = orig.spriteSortPoint;
                shown.sharedMaterial = orig.sharedMaterial;
                shown.color = orig.color;
                orig.enabled = false;
            }
            shown.sprite = frames[f];
        }
    }
}
