using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// bell - a bell (group `group`, note `note` 1..5): press T -> rings + flashes.
    /// Playing the sequence `answer` ("3,5,2,1") correctly -> the whole group is Done, flag `sets` (default &lt;group&gt;_done) + text `text`.
    /// A wrong note -> the sequence restarts and `fail_spawn` enemies are released into area `zone` in front of the bell (at most 1 per 2.5 s).
    /// Finished bells still ring. `requires` + `locked_text` (e.g. a single-note bell: answer "1", rung after clearing the area).
    /// </summary>
    public sealed class BellMechanism : Mechanism
    {
        private static readonly string[] BellNames = { "", "great", "low", "middle", "high", "small" };

        public override string DefaultVerb => "Ring";

        /// <summary>Flag `sets` cleared (e.g. falling mid-fight against Isolde) -> the bell group can be rung again from the start.</summary>
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
            // `requires` - the bell cannot be rung until the flags are set (e.g. the singers around the bell are cleared); shows `locked_text`
            string req = Str("requires", "");
            if (req.Length > 0 && !Ctx.Flags.HasAll(req)) { Hints.Show(Str("locked_text", "The bell will not answer yet."), 2.5f); return; }
            int note = Int("note", 1);
            SfxHooks.MechanismEvent(this, "toll", "bell_toll"); // sound
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

        /// <summary>Spawn spot IN FRONT of the bell (player side, lower y) and not overlapping obstacles.</summary>
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
