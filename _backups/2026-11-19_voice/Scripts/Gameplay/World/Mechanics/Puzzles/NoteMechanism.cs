using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// note — tờ ghi chú / hồ sơ: nhấn T -> bảng đọc lớn giữa màn hình (`title`, `text`, tranh `picture`,
    /// ký tự Vael `glyphs` "chữ số:chú thích;...", hoặc sơ đồ khoá `seal` "ô:ký tự[:số ô]"). Đọc lần đầu -> cờ `sets`.
    /// `requires`: chỉ đọc được khi có cờ. `show_when`: chỉ xuất hiện (cùng object &lt;tên&gt;_item) khi có cờ, kèm vòng sáng vàng tới khi đọc.
    /// Đi xa hơn <see cref="MapMechanics.CloseRange"/> thì bảng tự đóng.
    /// 04/11: `sprite` = ảnh tờ giấy (Resources, vd VFX/props/note_paper) đặt giữa mép dưới vùng; `marker = true` = vòng sáng tới khi đọc;
    /// `pickup = true` = đọc xong thì nhặt luôn tờ giấy (hình biến mất, nạp lại map vẫn mất nếu đã có cờ `sets`).
    /// 06/11: mặc định đặt tờ giấy bằng tile object (lớp `mechanics_art`, tileset SR-mech-art) — vùng đọc = khung tile object.
    /// 09/11 (§200): `sort_at` = "x,y" (ô Tiled) — chân để sắp lớp vẽ (tờ giấy đặt TRÊN thùng / bàn: lấy chân thùng, nhích xuống 0,05 ô
    /// để vẽ đè lên thùng mà vẫn bị Ren che khi Ren đứng trước). Nhặt đồ có key_* -> dòng "Obtained: ... [Inventory]".
    /// 11/11: có `show_when` thì hình (tile art / sprite) cũng ẩn tới khi có cờ (trước đây chỉ ẩn object &lt;tên&gt;_item).
    ///   `show_delay` (giây) = đợi rồi mới hiện, và đợi cảnh diễn (cổ vật của boss...) xong; `drop = true` = vật RƠI xuống (từ trên cao,
    ///   nảy nhẹ, rung) thay vì hiện phụt; `show_hint` = dòng chữ khi hiện; `shown_sets` = cờ bật khi đã hiện (vd đèn sáng theo).
    ///   Nạp bản lưu mà cờ đã có sẵn -> hiện ngay, không diễn, không hiện chữ.
    /// </summary>
    public sealed class NoteMechanism : Mechanism
    {
        private GameObject marker;
        private SpriteRenderer paper;
        private bool shown, read, synced, revealing;

        public override string DefaultVerb => "Read";
        public override bool Clue => marker == null;          // ghi chú có vòng sáng riêng (show_when) thì khỏi đốm

        public override bool Usable
        {
            get
            {
                if (marker != null && !shown) return false;    // chưa dọn phòng -> chưa xuất hiện
                if (read && Props.GetBool("pickup", false) && !PanelIsOpen) return false;   // đã nhặt: đọc lại trong túi đồ
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
                    if (art != null) art.Apply(paper, sp);                       // đúng chỗ vẽ trong Tiled
                    var psr = Ctx.Director != null && Ctx.Director.Player != null ? Ctx.Director.Player.GetComponentInChildren<SpriteRenderer>() : null;
                    if (psr != null) { paper.sortingLayerID = psr.sortingLayerID; paper.sortingOrder = psr.sortingOrder - 1; }
                    string sortAt = Str("sort_at", "");                     // 09/11 (§200): nằm trên thùng -> sắp theo chân thùng
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
            if (marker != null && paper != null && !string.IsNullOrEmpty(Str("show_when", ""))) paper.gameObject.SetActive(false);   // 11/11: chưa tới lúc
            KeyRing.Register(Props);                                     // tờ giấy nhặt được hiện trong túi đồ (key_id ...)
            string sets = Str("sets", "");
            if (Props.GetBool("pickup", false) && sets.Length > 0 && Ctx.Flags.HasAll(sets))   // bản lưu: đã nhặt
            {
                read = true;
                if (paper != null) paper.gameObject.SetActive(false);
            }
        }

        public override void OnFlagsChanged()
        {
            if (marker == null) return;
            bool on = Ctx.Flags.HasAll(Str("show_when", ""));
            bool first = !synced;                                            // lần đồng bộ đầu sau Init = nạp bản lưu: không diễn
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

        /// <summary>11/11: hiện sau `show_delay` + sau cảnh diễn; `drop` = rơi từ trên xuống, nảy nhẹ, rung máy quay.</summary>
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
                tr.position = Vector3.Lerp(top, rest, u * u);                    // rơi nhanh dần
                paper.color = new Color(1f, 1f, 1f, Mathf.Clamp01(u * 3f));
                yield return null;
            }
            if (tr == null) yield break;
            paper.color = Color.white;
            CameraFollow.KickMain(Vector2.down, 0.03f, 0.15f);
            for (float t = 0f; t < 0.18f && tr != null; t += Time.deltaTime)  // nảy nhẹ
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
            // 16/11 (§269): đồ nhận được ghi ở CHÂN BẢNG (bảng đang mở), không hiện thêm dòng chữ dưới màn hình (người dùng: thừa)
            string got = TextUtil.Clean(Str("key_got", ""));
            string item = Str("key_id", "");
            if (item.Length > 0 && !KeyRing.Has(Ctx.Flags, item))                                                      // 09/11 (§200)
            {
                KeyRing.Give(Ctx.Flags, item, got, false);
                panel.Obtained = got.Length > 0 ? got : $"Obtained: {KeyRing.NameOf(item)}.";
            }
            string wep = Str("weapon_id", "");                                                                        // 10/11 (§217): di vật = một thanh katana
            if (wep.Length > 0 && !Ctx.Flags.Has(WeaponRing.Flag(wep)))
            {
                WeaponRing.Give(Ctx.Flags, wep, got, false);
                var w = WeaponRing.Find(wep);
                panel.Obtained = got.Length > 0 ? got : $"Obtained: {(w != null ? w.displayName : wep)}.";
            }
            string sets = Str("sets", "");
            // 18/11 (người dùng): ghi chú có cảnh diễn đi kèm (`cutscene_only` = true) -> lần đầu KHÔNG mở bảng chữ, chỉ bật cờ để cảnh chạy;
            // đọc lại sau (tương tác lần nữa / túi đồ) vẫn mở bảng như thường.
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
            // "seal" = "ô:ký tự[:số ô]" (vd "1:3" = ô I của khoá ký tự phải là ký tự 3)
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
