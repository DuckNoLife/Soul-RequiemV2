using System.Collections.Generic;
using System.Globalization;
using SuperTiled2Unity;
using UnityEngine;
using UnityEngine.SceneManagement;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// GRAVESTONES / SIGNS (type sign, checkpoints with text): come close and press T to read. Signs in AutoSignNames show automatically on entering.
    /// Where the "[T] Read" label goes: property `prompt_at` (PromptAt).
    /// </summary>
    internal sealed partial class MapTriggerHandler
    {
        // ------------------------------------------------------------------ gravestones / signs: press T to read
        private static bool AutoSign(MapTrigger t)
        {
            if (t.props.GetBool(MapSchema.Prop.Auto, false) || AutoSignNames.Contains(t.name)) return true;
            return t.props.GetString(MapSchema.Prop.HintText, "").StartsWith("Press ");
        }

        private static string SignVerb(MapTrigger t)
        {
            string v = t.props.GetString(MapSchema.Prop.Verb, "");
            if (!string.IsNullOrEmpty(v)) return v;
            string n = t.name ?? "";
            if (n.Contains("sexton") || n.Contains("seer")) return "Talk";
            foreach (var k in new[] { "poster", "note", "ledger", "grave", "vey_", "exit", "hub_south", "door_hint", "notice" })
                if (n.Contains(k)) return "Read";
            return "Examine";
        }

        /// <summary>`prompt_at` = "x,y" (Tiled tile) - top middle of the object image to read (computed from the image pixels by _tools/s399/prompt_at.py).
        /// When set, the key prompt goes right above it; otherwise the old spot (middle / top edge of the area).</summary>
        private Vector2 PromptAt(SuperCustomProperties props, Vector2 fallback)
        {
            string at = props != null ? props.GetString("prompt_at", "") : "";
            var p = at.Split(',');
            if (p.Length != 2 || !float.TryParse(p[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float tx)
                || !float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float ty)) return fallback;
            return (Vector2)director.TileToWorld(tx, ty) - Vector2.up * MapMechanics.PromptLift;
        }

        /// <summary>Register the (non-automatic) gravestones/signs with InteractionSystem so T reads them.</summary>
        public void RegisterSigns()
        {
            InteractionSystem.Ensure();
            foreach (var t in triggers)
            {
                if (t.type != MapSchema.ObjectType.Sign || AutoSign(t)) continue;
                string text = t.props.GetString(MapSchema.Prop.HintText, "");
                if (string.IsNullOrEmpty(text)) continue;
                var r = t.rect;
                string hideWhen = t.props.GetString(MapSchema.Prop.HideWhen, "");   // e.g. the "gate is barred" sign stops showing once the gate is open
                var flags = director.Flags;
                string verb = SignVerb(t);
                var anchor = new Vector2(r.center.x, Mathf.Min(r.yMax, r.center.y + 0.45f));
                anchor = PromptAt(t.props, anchor);                                   // text right above the object
                InteractionSystem.Add(new Interactable
                {
                    id = t.name, verb = verb, rect = r, range = t.props.GetFloat(MapSchema.Prop.Range, 0.55f), calmOnly = true,
                    anchor = anchor,
                    available = string.IsNullOrEmpty(hideWhen) ? (System.Func<bool>)null : () => !flags.HasAll(hideWhen),
                    use = () =>
                    {
                        // read boards / letters in the dialogue box (pages separated by '|'; `speaker` = title line) instead of a faint line
                        DialogueBox.Open(t.props.GetString("speaker", ""), DialogueBox.Pages(GameInput.ExpandKeys(text)));
                        // signs with `flag` -> set the flag when read (e.g. Todd's contract on the notice board = a quest step)
                        string readFlag = t.props.GetString(MapSchema.Prop.Flag, "");
                        if (!string.IsNullOrEmpty(readFlag) && !flags.HasAll(readFlag)) flags.Set(readFlag);
                    }
                });
            }
        }
    }
}
