using System.Collections.Generic;
using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// flood — cống tràn: các lớp tile `layers` ("muc1,muc2,...") là các mức nước; chu kỳ
    /// idle -> rise -> hold -> fall (`idle_seconds`, `rise_seconds`, `hold_seconds`, `fall_seconds`).
    /// Ô gây rát của mức i: `cells_i` ("x,y;x,y" ô Tiled). Sủi bọt &lt;tên&gt;_warn `warn_seconds` trước khi tràn.
    /// </summary>
    public sealed class FloodMechanism : HazardMechanismBase
    {
        private GameObject[] stageLayers;
        private HashSet<Vector2Int>[] stageCells;

        public override void Init()
        {
            var names = Str("layers", "").Split(',');
            stageLayers = new GameObject[names.Length];
            stageCells = new HashSet<Vector2Int>[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                string ln = names[i].Trim();
                var tr = Ctx.FindMapChild(ln);
                stageLayers[i] = tr != null ? tr.gameObject : null;
                if (stageLayers[i] == null) Debug.LogWarning("[MapMechanics] Không thấy lớp " + ln);
                var set = new HashSet<Vector2Int>();
                foreach (var c in Str("cells_" + (i + 1), "").Split(';'))
                {
                    var p = c.Split(',');
                    if (p.Length == 2 && int.TryParse(p[0], out int x) && int.TryParse(p[1], out int y)) set.Add(new Vector2Int(x, y));
                }
                stageCells[i] = set;
            }
            Ctx.Show(Name + "_warn", false);
            SetStage(0);
        }

        protected override void OnStopped() => SetStage(0);

        private void SetStage(int stage)
        {
            phase = stage;
            if (stageLayers == null) return;
            for (int i = 0; i < stageLayers.Length; i++) if (stageLayers[i] != null) stageLayers[i].SetActive(i == stage);
        }

        public override void Tick(Vector2 playerPos, bool inside)
        {
            if (stageLayers == null || stageLayers.Length == 0) return;
            float idle = Float("idle_seconds", 2.5f), rise = Float("rise_seconds", 0.9f), hold = Float("hold_seconds", 1.8f),
                  fall = Float("fall_seconds", 0.9f), warn = Float("warn_seconds", 1f);
            int top = stageLayers.Length - 1;
            float t = Mathf.Repeat(Time.time + Float("offset", 0f), idle + rise + hold + fall);
            int stage = t < idle ? 0 : t < idle + rise ? Mathf.Min(1, top) : t < idle + rise + hold ? top : Mathf.Min(1, top);
            if (stage != phase) SetStage(stage);
            Ctx.Show(Name + "_warn", t >= idle - warn && t < idle);
            var hp = Ctx.PlayerHealth;
            if (hp == null || hp.IsDead || Time.time < nextTick) return;
            if (!stageCells[stage].Contains(Ctx.WorldToTile(Ctx.Player.position))) return;
            nextTick = Time.time + Ctx.DamageTick;
            Ctx.HurtPlayer(hp.Max * Float("dps_percent", 8f) / 100f * Ctx.DamageTick, Vector2.zero, 0f);
            if (!hp.Invulnerable)
                hp.ApplyPoison(Float("poison_percent", 3f) / 100f, Float("poison_seconds", 2f));
        }
    }
}
