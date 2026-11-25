using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// note - a note / record sheet: press T -> large reading panel in the centre of the screen (`title`, `text`, picture `picture`,
    /// Vael glyphs `glyphs` "digit:caption;...", or a lock diagram `seal` "slot:glyph[:slot count]"). First read -> flag `sets`.
    /// `requires`: only readable with the flag. `show_when`: only appears (together with object &lt;name&gt;_item) with the flag, with a gold glow ring until read.
    /// Walking farther than <see cref="MapMechanics.CloseRange"/> closes the panel.
    /// `sprite` = paper image (Resources, e.g. VFX/props/note_paper) placed at the middle of the area's bottom edge; `marker = true` = glow ring until read;
    /// `pickup = true` = reading picks the paper up (the image disappears, and stays gone on map reload once flag `sets` is set).
    /// By default the paper is placed as a tile object (`mechanics_art` layer, SR-mech-art tileset) - reading area = the tile object frame.
    /// `sort_at` = "x,y" (Tiled tile) - feet used for sorting (paper lying ON a crate / table: use the crate feet, nudged 0.05 tiles down
    /// so it draws over the crate yet is still covered by Ren standing in front). Picking up items with key_* -> line "Obtained: ... [Inventory]".
    /// With `show_when` the image (tile art / sprite) is hidden too until the flag is set (not only object &lt;name&gt;_item).
    ///   `show_delay` (seconds) = wait before showing, and wait for cutscenes (a boss relic...) to finish; `drop = true` = the object FALLS in (from above,
    ///   small bounce, shake) instead of popping in; `show_hint` = line shown on appearing; `shown_sets` = flag set once shown (e.g. a light turning on with it).
    ///   Loading a save with the flag already set -> shown at once, no animation, no text.
    /// </summary>
    public sealed class NoteMechanism : Mechanism
    {
        private GameObject marker;
        private SpriteRenderer paper;
        private bool shown, read, synced, revealing;

        public override string DefaultVerb => "Read";
        public override bool Clue => marker == null;          // notes with their own glow ring (show_when) need no sparkle

        public override bool Usable
        {
            get
            {
                if (marker != null && !shown) return false;    // room not cleared -> not shown yet
                if (read && Props.GetBool("pickup", false) && !PanelIsOpen) return false;   // picked up: re-read it in the inventory
                return Ctx.Flags.HasAll(Str("requires", ""));
            }
        }

        public override void Init()
        {
            if (!string.IsNullOrEmpty(Str("show_when", "")) || Props.GetBool("marker", false)) marker = NoteMarker.Create(Name, Rect, Ctx);
            var art = Art.Count > 0 ? Art[0] : null;
            string spr = Str("sprite", "");
            if (spr.Length > 0 || art != null)
            {
                var sp = art != null ? art.Resolve("sprite") : Resources.Load<Sprite>(spr);
                if (sp != null)
                {
                    paper = new GameObject("Note " + Name).AddComponent<SpriteRenderer>();
                    paper.sprite = sp;
                    paper.transform.position = new Vector3(Rect.center.x, Rect.yMin + 0.06f, 0f);
                    if (art != null) art.Apply(paper, sp);                       // exactly where it is drawn in Tiled
                    var psr = Ctx.Director != null && Ctx.Director.Player != null ? Ctx.Director.Player.GetComponentInChildren<SpriteRenderer>() : null;
                    if (psr != null) { paper.sortingLayerID = psr.sortingLayerID; paper.sortingOrder = psr.sortingOrder - 1; }
                    string sortAt = Str("sort_at", "");                     // lying on a crate -> sorted by the crate feet
                    if (sortAt.Length > 0)
                    {
                        Vector3 foot = Ctx.TilePoint(sortAt, paper.transform.position);
                        var g = new GameObject("NoteSort " + Name);
                        g.transform.position = new Vector3(foot.x, foot.y, 0f);
                        var grp = g.AddComponent<UnityEngine.Rendering.SortingGroup>();
                        if (psr != null) { grp.sortingLayerID = psr.sortingLayerID; grp.sortingOrder = psr.sortingOrder; }
                        paper.transform.SetParent(g.transform, true);
                        paper.sortingOrder = 0;
                    }
                }
            }
            if (marker != null && paper != null && !string.IsNullOrEmpty(Str("show_when", ""))) paper.gameObject.SetActive(false);   // not time yet
            KeyRing.Register(Props);                                     // picked-up paper shows in the inventory (key_id ...)
            string sets = Str("sets", "");
            if (Props.GetBool("pickup", false) && sets.Length > 0 && Ctx.Flags.HasAll(sets))   // save: already picked up
            {
                read = true;
                if (paper != null) paper.gameObject.SetActive(false);
            }
        }

        public override void OnFlagsChanged()
        {
            if (marker == null) return;
            bool on = Ctx.Flags.HasAll(Str("show_when", ""));
            bool first = !synced;                                            // first sync after Init = loading a save: no animation
            synced = true;
            if (revealing) return;
            if (on && !shown && !first)
            {
                if (Float("show_delay", 0f) > 0f || Props.GetBool("drop", false)) { revealing = true; Ctx.Run(Reveal()); return; }
                Hints.Show(ShowHint, 3f);
            }
            SetShown(on);
        }

        private string ShowHint => Str("show_hint", "Something lies where the fighting was. A faint glow marks it.");

        private void SetShown(bool on)
        {
            shown = on;
            Ctx.Show(Name + "_item", on);
            marker.SetActive(on && !read);
            if (paper != null) paper.gameObject.SetActive(on && !(read && Props.GetBool("pickup", false)));
            string flag = Str("shown_sets", "");
            if (on && flag.Length > 0 && !Ctx.Flags.HasAll(flag)) Ctx.Flags.Set(flag);
        }

        /// <summary>Show after `show_delay` + after cutscenes; `drop` = falls from above, small bounce, camera shake.</summary>
        private System.Collections.IEnumerator Reveal()
        {
            float delay = Float("show_delay", 0f);
            if (delay > 0f) yield return new WaitForSeconds(delay);
            while (CutsceneState.Active) yield return new WaitForSeconds(0.2f);
            revealing = false;
            SetShown(true);
            Hints.Show(ShowHint, 3.5f);
            if (paper == null || !Props.GetBool("drop", false) || !paper.gameObject.activeSelf) yield break;
            var tr = paper.transform;
            Vector3 rest = tr.position, top = rest + Vector3.up * 0.9f;
            for (float t = 0f; t < 0.38f && tr != null; t += Time.deltaTime)
            {
                float u = t / 0.38f;
                tr.position = Vector3.Lerp(top, rest, u * u);                    // falls faster and faster
                paper.color = new Color(1f, 1f, 1f, Mathf.Clamp01(u * 3f));
                yield return null;
            }
            if (tr == null) yield break;
            paper.color = Color.white;
            CameraFollow.KickMain(Vector2.down, 0.03f, 0.15f);
            for (float t = 0f; t < 0.18f && tr != null; t += Time.deltaTime)  // small bounce
            {
                float u = t / 0.18f;
                tr.position = rest + Vector3.up * (0.07f * 4f * u * (1f - u));
                yield return null;
            }
            if (tr != null) tr.position = rest;
        }

        public override void Animate()
        {
            if (marker != null && marker.activeSelf) NoteMarker.Animate(marker);
        }

        public override bool KeepPanelOpen(Vector2 playerPos) => Usable && DistanceTo(playerPos) <= MapMechanics.CloseRange;

        public override void Use()
        {
            if (PanelIsOpen) { MechanismPanels.Close(); return; }
            var panel = BuildPanel();
            if (read) { MechanismPanels.Open(panel); return; }
            read = true;
            if (marker != null) marker.SetActive(false);
            if (paper != null && Props.GetBool("pickup", false)) paper.gameObject.SetActive(false);
            // received items are printed at the PANEL BOTTOM (the panel is open), no extra line at the bottom of the screen
            string got = TextUtil.Clean(Str("key_got", ""));
            string item = Str("key_id", "");
            if (item.Length > 0 && !KeyRing.Has(Ctx.Flags, item))
            {
                KeyRing.Give(Ctx.Flags, item, got, false);
                panel.Obtained = got.Length > 0 ? got : $"Obtained: {KeyRing.NameOf(item)}.";
            }
            string wep = Str("weapon_id", "");                                                                        // the relic = a katana
            if (wep.Length > 0 && !Ctx.Flags.Has(WeaponRing.Flag(wep)))
            {
                WeaponRing.Give(Ctx.Flags, wep, got, false);
                var w = WeaponRing.Find(wep);
                panel.Obtained = got.Length > 0 ? got : $"Obtained: {(w != null ? w.displayName : wep)}.";
            }
            string sets = Str("sets", "");
            // notes with an attached cutscene (`cutscene_only` = true) -> the first time NO text panel opens, only the flag is set so the scene runs;
            // reading it later (interacting again / inventory) opens the panel as usual.
            if (Props.GetBool("cutscene_only", false))
            {
                if (!string.IsNullOrEmpty(sets)) Ctx.Flags.Set(sets);
                return;
            }
            MechanismPanels.Open(panel);
            if (!string.IsNullOrEmpty(sets)) Ctx.Flags.Set(sets);
        }

        private NotePanel BuildPanel()
        {
            var p = new NotePanel
            {
                Owner = this,
                Title = TextUtil.Clean(Str("title", "")),
                Body = TextUtil.Clean(Str("text", "").Replace("\\n", "\n")),
                Picture = Ctx.Picture(Str("picture", "-")),
            };
            foreach (var part in Str("glyphs", "").Split(';'))
            {
                int c = part.IndexOf(':');
                if (c > 0 && int.TryParse(part.Substring(0, c), out int d)) p.Glyphs.Add((Ctx.Glyph(d), part.Substring(c + 1)));
            }
            // "seal" = "slot:glyph[:slot count]" (e.g. "1:3" = slot I of the letter lock must be glyph 3)
            var sp = Str("seal", "").Split(':');
            if (sp.Length >= 2 && int.TryParse(sp[0], out int slot) && int.TryParse(sp[1], out int glyph))
            {
                p.SealSlot = slot - 1;
                p.SealGlyph = Ctx.Glyph(glyph);
                p.SealSlots = sp.Length >= 3 && int.TryParse(sp[2], out int n) ? Mathf.Clamp(n, 1, 6) : 3;
            }
            return p;
        }
    }
}
