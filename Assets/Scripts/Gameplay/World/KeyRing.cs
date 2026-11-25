using System.Collections.Generic;
using SuperTiled2Unity;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>One key item in the inventory (key, crank...). Defined by Tiled properties, see <see cref="KeyRing"/>.</summary>
    public sealed class KeyItem
    {
        public string id, name, desc, opens, icon;
        /// <summary>Kind (inventory filter): "Key", "Tool", "Document" - later "Weapon". Tiled property `key_kind`.</summary>
        public string kind = "Key";
        /// <summary>Full text (picked-up documents: re-readable in the inventory).</summary>
        public string text = "";
        /// <summary>Stats / passives table (future weapons): `key_stats` = "Label:Value;Label:Value".</summary>
        public readonly List<KeyValuePair<string, string>> stats = new List<KeyValuePair<string, string>>();
    }

    /// <summary>
    /// INVENTORY - keys and key items of the current map.
    ///   - Definition: a Tiled object with `key_id` + `key_name`, `key_desc`, `key_opens` (door / place it opens), `key_icon` (image in
    ///     Resources/UI/Requiem). Placed on a pickup `item = key` or on a chest (`chest`) holding the key.
    ///   - Owned = flag `key_&lt;id&gt;` (saved with the map flags). Used at least once = flag `key_&lt;id&gt;_used`.
    ///   - Key-locked doors: property `requires_key` on the gate object -> only opens when Ren is right at the door with the key
    ///     (MapEncounterDirector.TickGateLocks), never on pickup.
    ///   - UI: Pause menu ▸ Inventory (RequiemMenus).
    /// Items belong to each map: KeyRing.Clear() when a new map loads.
    /// </summary>
    public static class KeyRing
    {
        private static readonly List<KeyItem> known = new List<KeyItem>();

        public static IReadOnlyList<KeyItem> Known => known;
        public static void Clear() => known.Clear();
        public static string Flag(string id) => "key_" + id;
        public static string UsedFlag(string id) => "key_" + id + "_used";

        public static KeyItem Find(string id)
        {
            foreach (var k in known) if (k.id == id) return k;
            return null;
        }

        public static string NameOf(string id)
        {
            var k = Find(id);
            return k != null && !string.IsNullOrEmpty(k.name) ? k.name : id;
        }

        /// <summary>Read the item definition from Tiled properties. Returns the id (empty if the object defines no item).</summary>
        public static string Register(SuperCustomProperties p)
        {
            if (p == null) return "";
            string id = p.GetString("key_id", "");
            if (id.Length == 0) return "";
            var k = Find(id);
            if (k == null) { k = new KeyItem { id = id }; known.Add(k); }
            k.name = p.GetString("key_name", string.IsNullOrEmpty(k.name) ? id : k.name);
            k.desc = p.GetString("key_desc", k.desc ?? "");
            k.opens = p.GetString("key_opens", k.opens ?? "");
            k.icon = p.GetString("key_icon", k.icon ?? "");
            string body = p.GetString("text", "");
            if (body.Length > 0) k.text = TextUtil.Clean(body.Replace("\\n", "\n"));
            k.kind = p.GetString("key_kind", body.Length > 0 ? "Document" : k.kind);
            k.stats.Clear();
            foreach (var part in p.GetString("key_stats", "").Split(';'))
            {
                int c = part.IndexOf(':');
                if (c > 0) k.stats.Add(new KeyValuePair<string, string>(part.Substring(0, c).Trim(), part.Substring(c + 1).Trim()));
            }
            return id;
        }

        public static bool Has(WorldFlags f, string id) => f != null && !string.IsNullOrEmpty(id) && f.Has(Flag(id));
        public static bool Used(WorldFlags f, string id) => f != null && f.Has(UsedFlag(id));

        /// <summary>Items currently in the inventory (in map definition order).</summary>
        public static List<KeyItem> Owned(WorldFlags f)
        {
            var list = new List<KeyItem>();
            foreach (var k in known) if (Has(f, k.id)) list.Add(k);
            return list;
        }

        /// <summary>Give Ren an item: set the flag + a notice line (with custom text `text` if given).</summary>
        public static void Give(WorldFlags f, string id, string text, bool announce = true)   // announce = false -> the caller shows it
        {
            if (f == null || string.IsNullOrEmpty(id)) return;
            f.Set(Flag(id));
            { var ki = Find(id); SoulRequiem.Core.Sfx.Play2D(ki != null && ki.kind == "Document" ? "pickup_note" : "pickup_item"); }   // sound
            if (!announce) return;
            string msg = string.IsNullOrEmpty(text) ? $"Obtained: {NameOf(id)}." : text;
            Hints.Show(msg + "  [Inventory]", Mathf.Max(4f, msg.Length / 16f));
        }

        /// <summary>Ren says a line (locked door / chest...): speech above his head.</summary>
        public static void Say(Transform player, string line, float seconds = 2.4f)
        {
            if (player == null || string.IsNullOrEmpty(line)) return;
            SpeechBubble.Show(player.position + new Vector3(0f, 0.8f, 0f), line, seconds).Follow(player);
        }
    }
}
