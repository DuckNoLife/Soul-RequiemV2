using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// bell — quả chuông (nhóm `group`, nốt `note` 1..5): nhấn T -> ngân + loé sáng.
    /// Đánh đúng chuỗi `answer` ("3,5,2,1") -> cả nhóm Done, cờ `sets` (mặc định &lt;group&gt;_done) + chữ `text`.
    /// Sai một nốt -> chuỗi về đầu, thả quái `fail_spawn` vào khu `zone` trước mặt chuông (tối đa 1 con / 2.5 s).
    /// Chuông đã xong vẫn ngân được. 07/11: `requires` + `locked_text` (vd chuông một nốt: answer "1", rung sau khi dọn khu).
    /// </summary>
    public sealed class BellMechanism : Mechanism
    {
        private static readonly string[] BellNames = { "", "great", "low", "middle", "high", "small" };

        public override string DefaultVerb => "Ring";

        /// <summary>19/11 (§343): cờ `sets` bị gỡ (vd gục giữa trận Isolde) -> nhóm chuông rung lại được từ đầu.</summary>
        public override void OnFlagsReset()
        {
            string group = Str("group", "");
            if (!Done || group.Length == 0 || Ctx.Flags.HasAll(Str("sets", group + "_done"))) return;
            Done = false;
            Ctx.BellProgress[group] = 0;
        }
        public override bool Usable => true;

        public override void Use()
        {
            // 07/11: `requires` — chuông chưa rung được tới khi đủ cờ (vd dọn xong lũ hát quanh chuông); báo `locked_text`
            string req = Str("requires", "");
            if (req.Length > 0 && !Ctx.Flags.HasAll(req)) { Hints.Show(Str("locked_text", "The bell will not answer yet."), 2.5f); return; }
            int note = Int("note", 1);
            SfxHooks.MechanismEvent(this, "toll", "bell_toll"); // 19/11 (§335): âm
            Ctx.Run(Flash(Rect.center, note));
            string group = Str("group", "");
            if (string.IsNullOrEmpty(group) || Done) { Hints.Show("The bell tolls.", 1.2f); return; }
            var ans = Str("answer", "").Split(',');
            if (ans.Length == 0) return;
            Ctx.BellProgress.TryGetValue(group, out int step);
            string nm = note >= 1 && note < BellNames.Length ? BellNames[note] : "";
            if (ans[Mathf.Min(step, ans.Length - 1)].Trim() == note.ToString())
            {
                step++;
                if (step >= ans.Length)
                {
                    foreach (var m in Ctx.All) if (m is BellMechanism b && b.Str("group", "") == group) b.Done = true;
                    Ctx.BellProgress[group] = 0;
                    Hints.Show(Str("text", "The last note rings out. The bells fall silent."), 5f);
                    Ctx.Flags.Set(Str("sets", group + "_done"));
                    return;
                }
                Ctx.BellProgress[group] = step;
                Hints.Show($"The {nm} bell tolls. ({step}/{ans.Length})", 1.6f);
            }
            else
            {
                Ctx.BellProgress[group] = ans[0].Trim() == note.ToString() ? 1 : 0;
                Hints.Show($"The {nm} bell clangs out of tune. Something stirs in the rafters.", 2.5f);
                string fail = Str("fail_spawn", "");
                if (!string.IsNullOrEmpty(fail) && Time.time >= Ctx.NextBellSpawn)
                {
                    Ctx.NextBellSpawn = Time.time + 2.5f;
                    Ctx.Director.SpawnInZone(fail, FrontSpot(), Str("zone", ""));
                }
            }
        }

        /// <summary>Chỗ thả quái phía TRƯỚC chuông (phía người chơi, y thấp hơn) và không đè lên vật cản.</summary>
        private Vector2 FrontSpot()
        {
            var r = Rect;
            Vector2 c = r.center, pp = Ctx.Player != null ? (Vector2)Ctx.Player.position : c + Vector2.down;
            float side = pp.x >= c.x ? 1f : -1f;
            float below = r.height * 0.5f + 0.45f;
            var tries = new[]
            {
                new Vector2(c.x, r.yMin - 0.45f),
                new Vector2(c.x + side * 0.6f, r.yMin - 0.35f),
                new Vector2(c.x - side * 0.6f, r.yMin - 0.35f),
                new Vector2(c.x, c.y - below - 0.4f),
                Vector2.Lerp(c, pp, 0.5f),
            };
            var filter = new ContactFilter2D { useTriggers = false };
            var hits = new List<Collider2D>();
            foreach (var t in tries)
            {
                hits.Clear();
                Physics2D.OverlapCircle(t, 0.2f, filter, hits);
                bool blocked = false;
                foreach (var h in hits)
                    if (h != null && h.GetComponentInParent<PlayerController>() == null && h.GetComponentInParent<EnemyAI>() == null) { blocked = true; break; }
                if (!blocked) return t;
            }
            return Vector2.Lerp(c, pp, 0.6f);
        }

        private static IEnumerator Flash(Vector2 at, int note)
        {
            var go = new GameObject("BellFlash");
            go.transform.position = new Vector3(at.x, at.y, 0f);
            var l = go.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point;
            l.color = new Color(0.62f, 0.86f, 1f);
            float rad = Mathf.Lerp(1.6f, 0.9f, (note - 1) / 4f);
            l.pointLightOuterRadius = rad;
            l.pointLightInnerRadius = rad * 0.2f;
            for (float t = 0f; t < 0.8f; t += Time.deltaTime)
            {
                l.intensity = Mathf.Lerp(1.6f, 0f, t / 0.8f);
                yield return null;
            }
            Object.Destroy(go);
        }
    }
}
