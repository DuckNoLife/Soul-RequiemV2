using System.Collections;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 04/11: chest — rương mở bằng phím T, có hoạt ảnh mở nắp (8 khung, Resources/VFX/chest/chest&lt;style&gt;_0..7) và phần thưởng.
    /// Thuộc tính Tiled (object lớp mechanics, phủ lên hình rương):
    ///   chest          tên object hình rương trên map (tile object). Bắt buộc.
    ///   style          bộ khung hoạt ảnh: 2 (gỗ, mặc định) / 3 (sắt)
    ///   requires_key   id chìa trong túi đồ (KeyRing). Không có chìa -> Ren nói `locked_say`.
    ///   requires       cờ phải có trước (vd records_open); chưa có -> `locked_say`.
    ///   key_id ...     rương CHỨA một chìa / đồ then chốt (định nghĩa như KeyRing): mở ra là vào túi đồ.
    ///   potions_max    + số bình máu tối đa (và đổ đầy thêm chừng đó bình). potions: thêm bình (không tăng tối đa).
    ///   skill_id       chiêu học được (để trống: phát triển sau).
    ///   sets           cờ bật khi mở. text: dòng chữ khi mở. say: Ren nói sau khi mở.
    /// Đã mở (cờ "&lt;tên&gt;_opened", lưu theo map) -> nạp lại map thì rương nằm ở khung mở sẵn.
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
            // đã mở (hoặc bản lưu cũ đã có cờ `sets` từ trước khi có rương) -> nằm mở sẵn, chìa bên trong đã ở trong túi
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
                SfxHooks.MechanismEvent(this, "locked", "door_locked"); // 19/11 (§335): âm
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

            // phần thưởng
            var combat = player != null ? player.GetComponent<PlayerCombat>() : null;
            int max = Int("potions_max", 0), add = Int("potions", 0);
            if (combat != null && max > 0) { combat.maxPotions += max; combat.AddPotion(max); }
            if (combat != null && add > 0) combat.AddPotion(add);
            string skill = Str("skill_id", "");
            if (skill.Length > 0 && player != null) PlayerLoadout.Of(player).Unlock(PlayerLoadout.Find(skill), false);
            string sets = Str("sets", "");
            if (sets.Length > 0) Ctx.Flags.Set(sets);
            string text = Str("text", "");
            string give = Str("key_id", "");
            if (give.Length > 0) KeyRing.Give(Ctx.Flags, give, text);
            else if (text.Length > 0) Hints.Show(text, Mathf.Max(4f, text.Length / 16f));
            else if (max > 0) Hints.Show($"Inside: a flask. You can now carry {(combat != null ? combat.maxPotions : 0)} flasks.", 4f);
            KeyRing.Say(player, Str("say", ""));
        }

        /// <summary>Hiện khung `f` của hoạt ảnh mở: hình rương trên map ẩn đi, một hình riêng đặt khớp mép dưới-trái của nó.</summary>
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
                if (orig == null) { Debug.LogWarning("[Chest] " + Name + ": không thấy hình rương '" + Str("chest", "") + "'"); return; }
                // góc dưới-trái khung ảnh của hình trên map (bản đã cắt 9 px lề dưới) — khung hoạt ảnh 64x64 có pivot đúng chỗ đó
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
