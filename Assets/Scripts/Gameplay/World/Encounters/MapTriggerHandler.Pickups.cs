using System.Collections.Generic;
using System.Globalization;
using SuperTiled2Unity;
using UnityEngine;
using UnityEngine.SceneManagement;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// PICKUPS (type pickup): come close and press T. Property `item` decides what happens (OnPickup): potion, key, flag, skill, skill_scroll,
    /// flask_upgrade, green_tonic, weapon... Items lying on the ground have an image (PickupItemVisual) + glow; items in a chest (their `flag` opens the chest)
    /// are not drawn in advance and pop out of the chest on pickup (PickupPop). Flask full -> flasks are not picked up.
    /// </summary>
    internal sealed partial class MapTriggerHandler
    {
        /// <summary>Skills inside a scroll (item skill_scroll) not yet picked up -> a new playthrough starts WITHOUT that skill
        /// (Map 1: Earthshatter / slot F learned from Hob's chest). Carried over from the previous map (GameSession) -> kept.</summary>
        public void ApplyScrollLocks()
        {
            if (GameSession.HasLoadout) return;
            var lo = director.Player != null ? director.Player.GetComponent<PlayerLoadout>() : null;
            if (lo == null) return;
            bool changed = false;
            foreach (var t in triggers)
            {
                if (t.type != MapSchema.ObjectType.Pickup || t.props.GetString(MapSchema.Prop.Item, "") != "skill_scroll") continue;
                if (director.Flags.HasAll(t.props.GetString(MapSchema.Prop.Flag, "?"))) continue;
                var s = PlayerLoadout.Find(t.props.GetString(MapSchema.Prop.SkillId, ""));
                if (s == null) continue;
                lo.owned.Remove(s);
                for (int i = 0; i < PlayerLoadout.SlotCount; i++) if (lo.slots[i] == s) lo.slots[i] = null;
                changed = true;
            }
            if (changed) lo.NotifyChanged();
        }

        /// <summary>EVERY pickup = come close and press T. Button text: `verb` (default "Take"), range `range`
        /// (world units, default 0.3 ≈ 1 tile, measured to the area edge - wall-mounted items have their area on the wall face, Ren standing at the foot of the wall can still reach).
        /// Picked up / blocked by `unless` -> the button disappears. `requires` not met -> pressing T shows `locked_text`.</summary>
        public void RegisterPickups()
        {
            InteractionSystem.Ensure();
            var flags = director.Flags;
            foreach (var t in triggers)
            {
                if (t.type != MapSchema.ObjectType.Pickup || t.props.GetBool("auto", false)) continue;
                var pick = t;
                var r = t.rect;
                string unlessFlags = t.props.GetString("unless", "");
                // items DROPPED BY BOSSES (keys, Grok's tusk, the Vey seal...) must not show the T button before they have dropped.
                // The button only shows when: the item image (`hide`) is visible on the map AND (`requires` met, or there is a `locked_text` - the item is visible
                // but still guarded, e.g. the Map 4 seal "Its keeper still stands guard", the flask in the bear cave: press T to read the hint).
                string requires = t.props.GetString(MapSchema.Prop.Requires, "");
                bool hasLocked = !string.IsNullOrEmpty(t.props.GetString(MapSchema.Prop.LockedText, ""));
                var visuals = VisualsOf(t.props.GetString(MapSchema.Prop.Hide, ""));
                // pickups placed ON a pedestal / shelf (the Map 4 seal on a stone pedestal) were hidden by the pedestal itself: same sortingOrder, and the item's image base sits higher than the pedestal base,
                // so Y sorting draws the item behind. The item image is always drawn one step above its layer.
                foreach (var v in visuals)
                    if (v != null) foreach (var sr in v.GetComponentsInChildren<SpriteRenderer>(true)) sr.sortingOrder += 1;
                InteractionSystem.Add(new Interactable
                {
                    id = "pickup_" + t.name, verb = t.props.GetString(MapSchema.Prop.Verb, "Take"), rect = r, clue = false,
                    range = t.props.GetFloat(MapSchema.Prop.Range, 0.3f),
                    anchor = new Vector2(r.center.x, Mathf.Min(r.yMax, r.center.y + 0.45f)),
                    available = () => !pick.fired && (unlessFlags.Length == 0 || !flags.HasAll(unlessFlags))
                                      && Shown(visuals) && (hasLocked || flags.HasAll(requires)),
                    use = () => OnPickup(pick),
                });
            }
        }

        /// <summary>Tiled object that is the item image (the pickup's `hide` property).</summary>
        private List<GameObject> VisualsOf(string names)
        {
            var list = new List<GameObject>();
            if (string.IsNullOrEmpty(names) || director.mapRoot == null) return list;
            var want = new HashSet<string>();
            foreach (var n in names.Split(',')) if (n.Trim().Length > 0) want.Add(n.Trim());
            foreach (var so in director.mapRoot.GetComponentsInChildren<SuperTiled2Unity.SuperObject>(true))
                if (want.Contains(so.m_TiledName)) list.Add(so.gameObject);
            return list;
        }

        /// <summary>The item image is showing (no image -> counts as showing).</summary>
        private static bool Shown(List<GameObject> visuals)
        {
            if (visuals.Count == 0) return true;
            foreach (var v in visuals) if (v != null && v.activeInHierarchy) return true;
            return false;
        }

        /// <summary>The item lies in a chest / box = its `flag` is the `when` of an object with show_objects / hide_objects
        /// (e.g. z2b_flask sets z2b_flask_taken -> z2b_chest_toggle swaps the closed chest image for the open one).</summary>
        private bool OpensContainer(MapTrigger t)
        {
            string flag = t.props.GetString(MapSchema.Prop.Flag, "");
            if (flag.Length == 0 || director.mapRoot == null) return false;
            if (openFlags == null)
            {
                openFlags = new HashSet<string>();
                foreach (var cp in director.mapRoot.GetComponentsInChildren<SuperCustomProperties>(true))
                {
                    if (cp.GetString("show_objects", "").Length == 0 && cp.GetString("hide_objects", "").Length == 0) continue;
                    foreach (var w in cp.GetString("when", "").Split(','))
                        if (w.Trim().Length > 0) openFlags.Add(w.Trim());
                }
            }
            return openFlags.Contains(flag);
        }

        /// <summary>The item image rises out of the chest, then flies into Ren.</summary>
        private void PopFromContainer(MapTrigger t, string item)
        {
            var player = director.Player;
            var bodySr = player != null ? player.GetComponentInChildren<SpriteRenderer>() : null;
            PickupPop.Launch(new Vector2(t.rect.center.x, t.rect.center.y + 0.15f), player, item,
                             bodySr != null ? bodySr.sortingLayerID : 0, (bodySr != null ? bodySr.sortingOrder : 2) + 6);
        }

        private void OnPickup(MapTrigger t)
        {
            if (t.fired) return;
            var flags = director.Flags;
            // `unless` = the item was used for something else (e.g. giving the potion to the goblin child) -> disappears, no longer pickable
            string unlessFlags = t.props.GetString("unless", "");
            if (unlessFlags.Length > 0 && flags.HasAll(unlessFlags))
            {
                t.fired = true;
                if (pickupGlow.TryGetValue(t, out var g0) && g0 != null) UnityEngine.Object.Destroy(g0.gameObject);
                if (pickupMarker.TryGetValue(t, out var m0) && m0 != null) UnityEngine.Object.Destroy(m0);
                return;
            }
            // flag-locked items (e.g. a key dropping once the mini-boss area is cleared): not pickable until the flags are set
            if (!flags.HasAll(t.props.GetString(MapSchema.Prop.Requires, "")))
            {
                string locked = t.props.GetString(MapSchema.Prop.LockedText, "");
                if (!string.IsNullOrEmpty(locked)) Hints.Show(locked, 2.5f);
                return;
            }
            // flask already full -> do not pick up the flask yet, leave it to come back for later
            if (t.props.GetString(MapSchema.Prop.Item, "potion") == "potion" && director.Player != null)
            {
                var pk = director.Player.GetComponent<PlayerCombat>();
                if (pk != null && pk.FlasksFull)
                {
                    if (Time.unscaledTime >= fullHintAt) { fullHintAt = Time.unscaledTime + 3f; Hints.Show("Your flasks are already full. Leave it for later.", 2.5f); }
                    return;
                }
            }
            t.fired = true;
            flags.Set("picked_" + t.name);                     // the save remembers picked-up items
            if (pickupGlow.TryGetValue(t, out var glowObj) && glowObj != null) UnityEngine.Object.Destroy(glowObj.gameObject);
            if (pickupMarker.TryGetValue(t, out var markObj) && markObj != null) UnityEngine.Object.Destroy(markObj);
            var player = director.Player;
            string item = t.props.GetString(MapSchema.Prop.Item, "potion");
            if (containerPickups.Contains(t)) PopFromContainer(t, item);   // item pops out of the chest just opened
            switch (item)
            {
                case "potion":
                    var kit = player.GetComponent<PlayerCombat>();
                    if (kit != null) { kit.AddPotion(1); Hints.Show("Picked up a potion.", 2.5f); SoulRequiem.Core.Sfx.Play2D("pickup_item"); }   // sound
                    break;
                case "key":   // keys / key items -> inventory (KeyRing). Doors only open when Ren reaches the right door (requires_key)
                    KeyRing.Give(flags, KeyRing.Register(t.props), t.props.GetString(MapSchema.Prop.HintText, ""));
                    string keyFlag = t.props.GetString(MapSchema.Prop.Flag, "");   // e.g. a crank: flag lever_handle for the console
                    if (!string.IsNullOrEmpty(keyFlag)) flags.Set(keyFlag);
                    string keyHide = t.props.GetString(MapSchema.Prop.Hide, "");
                    if (!string.IsNullOrEmpty(keyHide)) HideMapObject(keyHide);
                    KeyRing.Say(player, t.props.GetString("say", ""));
                    break;
                case "flag":
                    flags.Set(t.props.GetString(MapSchema.Prop.Flag, t.name));
                    Hints.Show(t.props.GetString(MapSchema.Prop.HintText, "You picked something up."), 4.5f);
                    string pickSay = t.props.GetString("say", "");     // Ren says a line on pickup (e.g. Grok's tusk: a promise to bring it to the village)
                    if (!string.IsNullOrEmpty(pickSay) && player != null)
                        SpeechBubble.Show(player.position + new Vector3(0f, 0.82f, 0f), pickSay, 3f).Follow(player);
                    string hide = t.props.GetString(MapSchema.Prop.Hide, "");
                    if (!string.IsNullOrEmpty(hide)) HideMapObject(hide);
                    // after pickup go straight through the exit (e.g. Grok's tusk -> the Stone Ring floor collapses -> fall into Map 2)
                    string thenExit = t.props.GetString("then_exit", "");
                    if (!string.IsNullOrEmpty(thenExit)) director.StartCoroutine(CollapseThenExit(thenExit));
                    break;
                case "skill":   // swappable skill book: skill_id = SkillDefinition id
                    var lo = player.GetComponent<PlayerLoadout>();
                    if (lo != null) lo.Unlock(t.props.GetString(MapSchema.Prop.SkillId, ""));
                    flags.Set("new_mantra");                                     // new-skill flag (the old CombatTutorials try-out screen read it; that tutorial is removed)
                    string skillHide = t.props.GetString(MapSchema.Prop.Hide, "");   // skill scroll (Map 2) -> hide the scroll image
                    if (!string.IsNullOrEmpty(skillHide)) HideMapObject(skillHide);
                    string skillFlag = t.props.GetString(MapSchema.Prop.Flag, "");   // e.g. the Eye relic -> turn off the light at the pickup spot
                    if (!string.IsNullOrEmpty(skillFlag)) flags.Set(skillFlag);
                    Hints.Show(t.props.GetString(MapSchema.Prop.HintText, "A new mantra. Press {mantras} to attune it."), 4.5f);
                    break;
                case "skill_scroll":   // skill scroll + 1 flask (Hob's chest): learn the skill, equip it in the first empty slot (F)
                    var slo = player.GetComponent<PlayerLoadout>();
                    if (slo != null) slo.Unlock(t.props.GetString(MapSchema.Prop.SkillId, ""));
                    var sck = player.GetComponent<PlayerCombat>();
                    if (sck != null) sck.AddFlaskSlots(t.props.GetInt("potions", 1));   // max flasks +1
                    string scrollFlag = t.props.GetString(MapSchema.Prop.Flag, "");
                    if (!string.IsNullOrEmpty(scrollFlag)) flags.Set(scrollFlag);
                    string learn = t.props.GetString("learn_flag", "");
                    if (!string.IsNullOrEmpty(learn)) flags.Set(learn);
                    Hints.Show(t.props.GetString(MapSchema.Prop.HintText, "A mantra scroll. You learned a new mantra."), 6f);
                    break;
                case "flask_upgrade":   // each drink heals an extra 15% HP + 1 flask charge
                    var ck = player.GetComponent<PlayerCombat>();
                    if (ck != null) { ck.healPercent = Mathf.Min(0.8f, ck.healPercent + 0.15f); ck.AddPotion(1); }
                    Hints.Show(t.props.GetString(MapSchema.Prop.HintText, "An old miner's copper flask. Every drink now heals more."), 4.5f);
                    string flaskFlag = t.props.GetString(MapSchema.Prop.Flag, "");   // e.g. z2b_flask_taken -> the chest opens
                    if (!string.IsNullOrEmpty(flaskFlag)) flags.Set(flaskFlag);
                    break;
                case "green_tonic":   // the healer's blue potion - cures poison
                    var hp = player.GetComponent<Health>();
                    if (hp != null) hp.CurePoison();
                    Hints.Show(t.props.GetString(MapSchema.Prop.HintText, "A green tonic. It burns the goblin poison out of your blood."), 4.5f);
                    break;
                case "weapon":   // picked up a katana -> weapon rack (inventory ▸ Weapons), NOT equipped automatically
                    WeaponRing.Give(flags, t.props.GetString("weapon_id", ""), t.props.GetString(MapSchema.Prop.HintText, ""));
                    string wepFlag = t.props.GetString(MapSchema.Prop.Flag, "");
                    if (!string.IsNullOrEmpty(wepFlag)) flags.Set(wepFlag);
                    string wepHide = t.props.GetString(MapSchema.Prop.Hide, "");
                    if (!string.IsNullOrEmpty(wepHide)) HideMapObject(wepHide);
                    KeyRing.Say(player, t.props.GetString("say", ""), 3.4f);
                    break;
                case "skill_book":
                    flags.Set("book_" + t.props.GetString(MapSchema.Prop.Book, t.name));
                    Hints.Show(t.props.GetString(MapSchema.Prop.HintText, "You found a book of mantras."), 5f);
                    HideMapObject(t.name + "_book");
                    break;
            }
        }

        private void HideMapObject(string tiledName)
        {
            foreach (var so in director.mapRoot.GetComponentsInChildren<SuperObject>(true))
                if (so.m_TiledName == tiledName) so.gameObject.SetActive(false);
        }
    }
}
