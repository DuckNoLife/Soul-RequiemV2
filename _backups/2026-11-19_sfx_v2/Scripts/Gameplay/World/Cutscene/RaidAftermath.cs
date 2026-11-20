using System.Collections;
using SuperTiled2Unity;
using UnityEngine;
using UnityEngine.InputSystem;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 02/11: sau khi đuổi toán cướp (cờ zf_cleared): cửa nhà nông bật mở, ông Todd (trưởng làng) bước ra, đi tới chỗ Ren,
    /// cảm ơn và kể chuyện (bóng thoại), rồi chỉ tới hợp đồng trên bảng tin. Xong bật cờ todd_greeted, Todd về chỗ của mình.
    /// Dữ liệu Tiled: điểm "aftermath_door" (lớp triggers, type cutscene_point) = bậc cửa; prop "farm_door_open" = hình cửa mở;
    /// NPC Todd (lớp npcs, tên "todd") có show_when = todd_door (cảnh bật cờ này sau khi đặt Todd ở bậc cửa -> không hiện đột ngột).
    /// Câu thoại: thuộc tính line1..lineN trên điểm aftermath_door. Space / Enter: tua nhanh.
    /// </summary>
    public class RaidAftermath : MonoBehaviour
    {
        private bool skip;

        private IEnumerator Start()
        {
            yield return null;
            var dir = MapEncounterDirector.Instance;
            if (dir == null || dir.Flags == null || dir.Flags.HasAll("todd_greeted")) yield break;
            foreach (var so in dir.mapRoot.GetComponentsInChildren<SuperObject>(true))
                if (so.m_TiledName == "farm_door_open") so.gameObject.SetActive(false);   // cửa đóng cho tới lúc Todd ra
            while (!dir.Flags.HasAll("zf_cleared")) yield return new WaitForSeconds(0.25f);
            yield return new WaitForSeconds(1.6f);                       // lửa tắt, khói tan bớt

            SuperObject doorPt = null, doorOpen = null;
            foreach (var so in dir.mapRoot.GetComponentsInChildren<SuperObject>(true))
            {
                if (so.m_TiledName == "aftermath_door") doorPt = so;
                else if (so.m_TiledName == "farm_door_open") doorOpen = so;
            }
            Villager todd = null;
            foreach (var v in FindObjectsByType<Villager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (v.name == "NPC todd") { todd = v; break; }
            if (doorPt == null || todd == null || dir.Player == null) { dir.Flags.Set("todd_door"); dir.Flags.Set("todd_greeted"); yield break; }

            var props = doorPt.GetComponent<SuperCustomProperties>();
            Vector3 door = doorPt.transform.position;
            Vector3 home = todd.transform.position;                      // chỗ đứng thường ngày của Todd (vị trí trong Tiled)

            // 02/11 (chạy thử): chỉ diễn khi Ren đang ở gần nhà Todd (dọn khu bằng phím thử / bỏ đi xa thì đợi Ren quay lại)
            while (dir.Player != null && ((Vector2)(dir.Player.position - door)).magnitude > 4.5f) yield return new WaitForSeconds(0.25f);

            // cửa bật mở, Todd bước ra
            if (doorOpen != null) doorOpen.gameObject.SetActive(true);
            yield return new WaitForSeconds(0.5f);
            todd.transform.position = door;
            dir.Flags.Set("todd_door");                                  // VillagerDirector bật Todd tại bậc cửa
            yield return null;
            StartCoroutine(HoldInput(dir));
            CutsceneState.Begin();                                       // HUD mờ đi, không che bóng thoại

            var player = dir.Player;
            // 02/11: Todd đứng NGANG hàng với Ren (bên phía cửa nhà) để hai người nhìn thẳng mặt nhau theo chiều ngang —
            // trước đây Todd đứng chéo phía trên nên quay mặt xuống (S), còn Ren vẫn nhìn hướng cũ
            float side = door.x >= player.position.x ? 1f : -1f;
            Vector2 stand = (Vector2)player.position + new Vector2(side * 0.46f, 0f);
            var combat = player.GetComponent<PlayerCombat>();
            if (combat != null) combat.FacingOverride = new Vector2(side, 0f);   // Ren quay sang phía Todd đi tới
            todd.WalkTo(stand);
            float give = Time.time + 7f;
            while (!todd.Arrived && Time.time < give && !skip) yield return null;
            // 02/11 (chạy thử): đường bị chặn -> đặt Todd cạnh Ren, không để hai người nói chuyện cách nhau cả sân
            float miss = ((Vector2)todd.transform.position - stand).magnitude;
            if (miss > 0.35f && miss < 3f) todd.Warp(stand);
            if (doorOpen != null) doorOpen.gameObject.SetActive(false); // cửa khép lại sau lưng
            todd.FaceTo(player.position);

            // 02/11: camera lia vào giữa Todd và Ren (hơi nhích lên để bóng thoại nằm giữa màn hình) — trước đây camera vẫn bám Ren,
            // bóng thoại của Todd ở mép trên bị thanh máu / minimap che
            var cam = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            float oldSharp = cam != null ? cam.focusSharpness : 6f;
            if (cam != null)
            {
                cam.focusSharpness = 2.5f;
                cam.Focus((todd.transform.position + player.position) * 0.5f + new Vector3(0f, 0.35f, 0f), Camera.main.orthographicSize);
            }

            // 02/11: lời dài -> khung hội thoại dưới màn hình (tên "Todd", chữ chạy, [T] câu tiếp) thay bóng thoại trên đầu bị cắt chữ
            var lines = new System.Collections.Generic.List<string>();
            for (int i = 1; i <= 8; i++)
            {
                string line = props != null ? props.GetString("line" + i, "") : "";
                if (string.IsNullOrEmpty(line)) break;
                lines.Add(line);
            }
            if (lines.Count > 0)
            {
                DialogueBox.Open(todd.speaker.Length > 0 ? todd.speaker : "Todd", lines.ToArray());
                while (DialogueBox.Active)
                {
                    todd.FaceTo(player.position);
                    if (combat != null) combat.FacingOverride = (Vector2)(todd.transform.position - player.position);
                    yield return null;
                }
            }

            if (cam != null) { cam.Release(); cam.focusSharpness = 3f; }
            if (combat != null) combat.FacingOverride = Vector2.zero;
            dir.Flags.Set("todd_greeted");                              // HoldInput nhả điều khiển
            Hints.Show(props != null ? props.GetString("after_hint", "") : "", 4.5f);
            yield return new WaitForSeconds(0.8f);
            if (cam != null) cam.focusSharpness = oldSharp;
            todd.WalkTo(home);
            give = Time.time + 12f;
            while (!todd.Arrived && Time.time < give) yield return null;
            todd.EndScript();
        }

        private IEnumerator HoldInput(MapEncounterDirector dir)
        {
            while (!dir.Flags.HasAll("todd_greeted"))
            {
                GameInput.Blocked = true;
                // 11/11 (§229): cảnh cốt truyện KHÔNG bỏ qua được (chỉ bảng giới thiệu boss mới bỏ qua được)
                yield return null;
            }
            GameInput.Blocked = false;
            CutsceneState.End();
        }
    }
}
