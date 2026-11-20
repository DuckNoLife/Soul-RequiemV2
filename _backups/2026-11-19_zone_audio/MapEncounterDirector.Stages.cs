using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 08/11 (NOTES §186) — ẢI (stage): nhóm khu của một nhiệm vụ, kết thúc bằng miniboss / mục tiêu của nhiệm vụ.
    /// Dữ liệu Tiled:
    ///   khu (zone)        : `stage` = mã ải (vd "west"). Khu không ghi = như cũ (dọn một lần là xong vĩnh viễn).
    ///   điểm nghỉ (checkpoint): `stage` = mã ải mà điểm này là ĐIỂM HỒI SINH (đặt ở đầu ải), `stage_name` = tên hiện khi qua ải.
    /// Luật:
    ///   • Gục khi ải CHƯA xong: tỉnh ở điểm hồi sinh của ải đang đánh (nếu đã thắp; chưa thắp -> lửa trại cuối như cũ),
    ///     và MỌI ải chưa xong hồi quái — mọi khu của ải đó đánh lại từ đầu.
    ///     Cờ &lt;khu&gt;_cleared KHÔNG bị xoá: cổng tắt đã mở, câu đố, đồ đã nhặt, chuông đã rung vẫn giữ (kiểu Souls).
    ///   • Qua ải (mọi khu CÓ QUÁI của ải đã dọn): cờ stage_&lt;mã&gt;_done -> ải khoá vĩnh viễn, đầy máu + đầy bình, tự lưu,
    ///     dòng "&lt;tên ải&gt; - trial complete".
    ///   • Nạp bản lưu / quay lại map: ải chưa xong cũng hồi quái (cờ _cleared của khu trong ải bị bỏ qua khi dựng khu).
    /// Khu có hang sinh quái (cave) không làm lại được -> giữ nguyên.
    /// </summary>
    public partial class MapEncounterDirector
    {
        private string lastStage = "";

        public static string StageDoneFlag(string stage) => "stage_" + stage + "_done";

        /// <summary>Ải đã qua (cờ stage_&lt;mã&gt;_done).</summary>
        public bool StageDone(string stage) => !string.IsNullOrEmpty(stage) && Flags != null && Flags.Has(StageDoneFlag(stage));

        /// <summary>Khu thuộc một ải chưa qua.</summary>
        private bool InOpenStage(EncounterZone z) => !string.IsNullOrEmpty(z.stage) && !StageDone(z.stage);

        /// <summary>Gọi sau khi khôi phục khu từ cờ (Start): khu đã dọn nhưng ải chưa xong -> hồi quái.</summary>
        private void ApplyStagesOnLoad()
        {
            foreach (var z in layout.Zones.Values)
                if (z.cleared && InOpenStage(z) && z.caves.Count == 0) Rearm(z);
        }

        /// <summary>Làm lại một khu từ đầu: xoá quái còn sống, mở khoá đấu trường, bỏ trạng thái đã dọn (giữ cờ).</summary>
        private void Rearm(EncounterZone z)
        {
            foreach (var e in z.alive)
                if (e != null) { if (e == Boss) Boss = null; Destroy(e.gameObject); }
            z.alive.Clear();
            gateCtl.RearmArena(z.id);
            z.started = z.cleared = false;
            z.quietSince = -1f; z.stragglersCalled = false;   // 19/11 (§340)
            z.waveIndex = -1;
            z.killed = 0;
            z.pending = 0; z.generation++;
            z.holdNearRespawn = z.rect.Contains((Vector2)respawnPoint);
        }

        /// <summary>Gục: mọi ải chưa xong hồi quái.</summary>
        private void ResetOpenStages()
        {
            foreach (var z in layout.Zones.Values)
                if (InOpenStage(z) && z.caves.Count == 0 && (z.started || z.cleared)) Rearm(z);
        }

        /// <summary>Ải người chơi đang đánh lúc gục: khu (thuộc ải chưa xong) chứa chỗ gục, không thì ải vừa vào gần nhất.</summary>
        private string DeathStage(Vector2 at)
        {
            foreach (var z in layout.Zones.Values)
                if (InOpenStage(z) && z.rect.Contains(at)) return z.stage;
            return !string.IsNullOrEmpty(lastStage) && !StageDone(lastStage) ? lastStage : "";
        }

        /// <summary>Gục trong ải chưa xong -> điểm hồi sinh của ải (nếu đã thắp).</summary>
        private bool ChooseStageRespawn(Vector2 diedAt)
        {
            string st = DeathStage(diedAt);
            if (st.Length == 0) return false;
            if (triggerHandler.TryStageSpawn(st, out var pos)) { SetRespawnPoint(pos); return true; }
            return false;
        }

        /// <summary>Khu vừa dọn: nếu là khu cuối cùng (có quái) của ải -> qua ải. Trả true nếu vừa qua ải.</summary>
        private bool CheckStageComplete(EncounterZone z)
        {
            if (!InOpenStage(z)) return false;
            foreach (var o in layout.Zones.Values)
                if (o.stage == z.stage && o.points.Count > 0 && !o.cleared) return false;
            CompleteStage(z.stage);
            return true;
        }

        private void CompleteStage(string stage)
        {
            Flags.Set(StageDoneFlag(stage));
            if (lastStage == stage) lastStage = "";
            if (playerHealth != null && !playerHealth.IsDead)
            {
                playerHealth.Heal(playerHealth.Max);
                var kit = player != null ? player.GetComponent<PlayerCombat>() : null;
                if (kit != null) kit.RefillPotions();
            }
            SaveProgress();
            StartCoroutine(StageHint(triggerHandler.StageName(stage)));
            StartCoroutine(AnchorStageRespawn());                        // 09/11 (§200): hồi sinh tại phòng vừa qua ải
        }

        /// <summary>09/11 (§200, người dùng): qua ải thì điểm hồi sinh chuyển về ngay phòng vừa dọn — khỏi phải đi ngược về lửa trại.
        /// Chọn chỗ Ren đứng yên ổn (không mất máu 0,8 s, không diễn cảnh) trong 8 s sau khi qua ải; không có thì lấy chỗ lúc qua ải.
        /// Gục ở ải sau: ải đó có lửa trại đã thắp -> về lửa trại đó (ChooseStageRespawn), không thì về chỗ này.</summary>
        private IEnumerator AnchorStageRespawn()
        {
            if (player == null || playerHealth == null || playerHealth.IsDead) yield break;
            Vector2 at = player.position;
            float hp = playerHealth.Current, calmSince = Time.time, until = Time.time + 8f;
            while (Time.time < until)
            {
                if (playerHealth.IsDead || respawning) yield break;
                if (playerHealth.Current < hp - 0.01f) calmSince = Time.time;
                hp = playerHealth.Current;
                if (Time.time - calmSince >= 0.8f && !CutsceneState.Active) { at = player.position; break; }
                yield return null;
            }
            SetRespawnPoint(at);
            SaveProgress();
        }

        private IEnumerator StageHint(string name)
        {
            yield return new WaitForSeconds(2.6f);                       // sau "The gate is open" / màn cổ vật
            while (CutsceneState.Active) yield return null;
            Hints.Show($"{name} - trial complete. Your strength and flasks are restored - if you fall, you rise here.", 5f);   // 09/11 (§200)
        }
    }
}
