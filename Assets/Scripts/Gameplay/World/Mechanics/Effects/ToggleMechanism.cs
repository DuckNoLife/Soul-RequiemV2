using System.Collections;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// toggle - once flag `when` is set: fade out then hide tile layers `hide_layers`, hide objects `hide_objects`, show objects `show_objects`.
    /// (e.g. the Ichor channel slowly draining after the flow is diverted.)
    /// </summary>
    public sealed class ToggleMechanism : Mechanism
    {
        public override void Init() => Ctx.Show(Str("show_objects", ""), false);

        public override void OnFlagsChanged()
        {
            if (Done || !Ctx.Flags.HasAll(Str("when", "-"))) return;
            Done = true;
            SfxHooks.Toggled(this);   // sound
            foreach (var ln in Str("hide_layers", "").Split(','))
                if (!string.IsNullOrEmpty(ln)) Ctx.Run(FadeLayer(ln.Trim(), 2.5f));
            Ctx.Show(Str("hide_objects", ""), false);
            Ctx.Show(Str("show_objects", ""), true);
        }

        private IEnumerator FadeLayer(string layerName, float seconds)
        {
            var t = Ctx.FindMapChild(layerName);
            if (t == null) { Debug.LogWarning("[MapMechanics] Layer not found " + layerName); yield break; }
            var maps = t.GetComponentsInChildren<UnityEngine.Tilemaps.Tilemap>(true);
            float t0 = 0f;
            while (t0 < seconds)
            {
                t0 += Time.deltaTime;
                foreach (var tm in maps) { var c = tm.color; c.a = 1f - t0 / seconds; tm.color = c; }
                yield return null;
            }
            t.gameObject.SetActive(false);
        }
    }
}
