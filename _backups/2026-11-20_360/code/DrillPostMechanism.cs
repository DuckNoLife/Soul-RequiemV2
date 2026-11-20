using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 20/11 (§356): drill_post — CỌC TẬP của The Tally Room (Map 4): một người nộm luyện tập đứng trên cọc (`kind` sack | straw | pell | bust —
    /// bust = tượng bán thân Trung sĩ Burlap, hình king thu nhỏ). Ren nhấn T ("Drill") để tập với nó theo THỨ TỰ BÀI TẬP:
    ///   nhóm `group`, số `note` (1..4), đáp án `answer` ("2,1,4,3", ghi ở mọi cọc trong nhóm). Đúng -> người nộm làm động tác, rune dưới chân sáng;
    ///   SAI -> người nộm QUẬT Ren (2 máu, đẩy lùi) + chuỗi về đầu. Đủ chuỗi -> cả nhóm chào, cờ `sets` (mặc định &lt;group&gt;_done).
    /// Gợi ý thứ tự nằm trong sổ tập (note) cạnh cửa. Va chạm nhỏ ở chân cọc.
    /// </summary>
    public sealed class DrillPostMechanism : Mechanism
    {
        private static readonly Dictionary<string, int> progress = new Dictionary<string, int>();
        private DrillActor dummy;
        private Light2D lamp;
        private bool lit;

        public override string DefaultVerb => "Drill";
        public override bool Usable => !Done;
        private string Group => Str("group", "tally");
        private string Kind => Str("kind", "sack");

        public override void Init()
        {
            string k = Kind == "bust" ? "king" : Kind;
            var foot = new Vector2(Rect.center.x, Rect.yMin + 0.02f);
            dummy = new DrillActor(k, foot, Ctx.Host.transform, Ctx.Player, Kind == "bust" ? 0.62f : 1.35f) { Harmless = true, FaceByVel = false };
            dummy.Sr.flipX = Props.GetBool("flip", false);
            dummy.Tick(0f);
            var go = new GameObject("DrillPostCollider " + Name);
            go.transform.SetParent(Ctx.Host.transform, true);
            go.transform.position = foot + Vector2.up * 0.07f;
            go.AddComponent<BoxCollider2D>().size = new Vector2(Kind == "pell" ? 0.36f : 0.28f, 0.12f);
            var lg = new GameObject("DrillPostLamp " + Name);
            lg.transform.SetParent(Ctx.Host.transform, true);
            lg.transform.position = foot;
            lamp = lg.AddComponent<Light2D>();
            lamp.lightType = Light2D.LightType.Point;
            lamp.color = new Color(1f, 0.8f, 0.45f);
            lamp.pointLightOuterRadius = 0.9f;
            lamp.intensity = 0f;
            OnFlagsChanged();
        }

        public override void OnFlagsChanged()
        {
            if (Done || !Ctx.Flags.HasAll(Str("sets", Group + "_done"))) return;
            Done = true; lit = true;
        }

        public override void Animate()
        {
            if (dummy != null) dummy.Tick(Time.deltaTime);
            if (lamp != null) lamp.intensity = Mathf.MoveTowards(lamp.intensity, lit ? 0.8f + 0.15f * Mathf.Sin(Time.time * 3f) : 0f, Time.deltaTime * 2f);
        }

        public override void Use()
        {
            if (Done) return;
            var ans = Str("answer", "").Split(',');
            if (ans.Length == 0) return;
            progress.TryGetValue(Group, out int step);
            int note = Int("note", 1);
            if (ans[Mathf.Min(step, ans.Length - 1)].Trim() == note.ToString())
            {
                step++;
                lit = true;
                Ctx.Run(Show());
                Sfx.Play("hit_wood", Rect.center, 0.7f);
                if (step >= ans.Length)
                {
                    progress[Group] = 0;
                    foreach (var m in Ctx.All) if (m is DrillPostMechanism p && p.Group == Group) { p.Done = true; p.lit = true; p.Ctx.Run(p.Show()); }
                    Sfx.Play("puzzle_solved", Rect.center);
                    Hints.Show(Str("text", "Every post bows stiffly. Somewhere west, a bolt slides back."), 4f);
                    Ctx.Flags.Set(Str("sets", Group + "_done"));
                    return;
                }
                progress[Group] = step;
                Hints.Show(Str("ok_" + step, "HUT! (" + step + "/" + ans.Length + ")"), 1.4f);
            }
            else
            {
                progress[Group] = 0;
                foreach (var m in Ctx.All) if (m is DrillPostMechanism p && p.Group == Group) p.lit = false;
                Ctx.Run(Wallop());
            }
        }

        /// <summary>Động tác mẫu của người nộm (xoay / bước) rồi về đứng nghiêm.</summary>
        private IEnumerator Show()
        {
            if (dummy == null) yield break;
            string a = Kind == "straw" || Kind == "pell" ? "spin" : "move";
            if (Kind == "bust") a = "move";
            dummy.Play(a, 16f);
            yield return new WaitForSeconds(0.7f);
            dummy.Play("idle", 4f);
        }

        /// <summary>Sai thứ tự: người nộm quật Ren.</summary>
        private IEnumerator Wallop()
        {
            if (dummy != null) dummy.Play(Kind == "straw" || Kind == "pell" ? "spin" : "move", 22f);
            Sfx.Play("duo_punch", Rect.center);
            Vector2 p = Ctx.Player != null ? (Vector2)Ctx.Player.position : Rect.center;
            Vector2 d = p - Rect.center;
            Ctx.HurtPlayer(2f, d.sqrMagnitude > 0.0001f ? d.normalized : Vector2.down, 4f);
            string[] lines = { "WRONG ORDER, RECRUIT! *WHAP*", "Out of turn! The ledger SAYS the order! *WHAP*", "That is NOT how we drill! *THWACK*" };
            Hints.Show(lines[Random.Range(0, lines.Length)], 2.2f);
            yield return new WaitForSeconds(0.6f);
            if (dummy != null) dummy.Play("idle", 4f);
        }

        internal override void DebugSolve()
        {
            if (Done) return;
            Ctx.Flags.Set(Str("sets", Group + "_done"));
        }
    }
}
