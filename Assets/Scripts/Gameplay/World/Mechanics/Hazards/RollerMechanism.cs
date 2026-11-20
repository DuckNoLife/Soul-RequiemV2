using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 08/11 (§197): roller — KHÚC GỖ GAI LĂN (Old Prison). Vùng object = làn lăn. Làn dọc (cao hơn rộng): nắp hầm ở đầu làn bật mở
    /// (báo trước ~0,65 s) rồi khúc gỗ ngang lăn xuống; làn ngang: khúc gỗ dọc lăn sang. Cán cả quái.
    ///   dir    down | up | left | right (mặc định theo hình làn: dọc = down, ngang = right)
    ///   trigger (tín hiệu, vd tấm sàn) và / hoặc period (giây; 0 = chỉ theo tín hiệu), offset
    ///   speed (ô / giây, mặc định 6), damage (người chơi, 22), enemy_damage (80), knock (5), rearm (giây sau mỗi lượt, 2)
    ///   disable_on, start_when, stop_when. Tránh: núp vào hốc tường dọc làn, hoặc chạy kịp ra khỏi làn.
    ///   17/11 (§288): `fit_lane` = true -> khúc gỗ phóng to cho KÍN bề ngang làn (vùng cán cũng kín làn) — trước đây gỗ chỉ dài ~3,75 ô,
    ///   làn 5 ô thì áp sát vách là thoát. `log_fill` = phần thân gỗ có hình trên chiều dài ảnh (trap_log_v: 120/160 = 0.75).
    /// </summary>
    public sealed class RollerMechanism : HazardMechanismBase
    {
        private Sprite[] hatch, log;
        private SpriteRenderer hatchSr, logSr;
        private TrapListener trigger, disabler;
        private float disabledUntil, startAt = -1f, readyAt, lastCycle = -1f;
        private const float HatchTime = 0.65f;
        private readonly HashSet<Health> hit = new HashSet<Health>();
        private bool playerHit;
        private float fitCross;                                           // 17/11 (§288): > 0 = vùng cán phủ kín bề ngang làn

        private Vector2 Dir
        {
            get
            {
                string d = Str("dir", Rect.height >= Rect.width ? "down" : "right");
                return d == "up" ? Vector2.up : d == "left" ? Vector2.left : d == "right" ? Vector2.right : Vector2.down;
            }
        }
        private bool Vertical => Mathf.Abs(Dir.y) > 0.5f;

        public override void Init()
        {
            log = TrapArt.Frames(Vertical ? "trap_log_h" : "trap_log_v");
            hatch = Vertical ? TrapArt.Frames("trap_hatch") : new Sprite[0];
            if (hatch.Length > 0)
            {
                hatchSr = TrapArt.MakeRenderer(Ctx.Host.transform, "Hatch " + Name, StartPos(), Ctx.Player, -6);
                hatchSr.sprite = hatch[0];
            }
            if (log.Length > 0)
            {
                logSr = TrapArt.MakeRenderer(Ctx.Host.transform, "Log " + Name, StartPos(), Ctx.Player, 0);
                logSr.sprite = log[0];
                logSr.enabled = false;
                if (Props.GetBool("fit_lane", false))                         // 17/11 (§288): gỗ kín làn
                {
                    float cross = Vertical ? Rect.width : Rect.height;
                    float art = (Vertical ? log[0].bounds.size.x : log[0].bounds.size.y) * Mathf.Clamp(Float("log_fill", 0.75f), 0.1f, 1f);
                    float k = cross / Mathf.Max(0.01f, art);
                    logSr.transform.localScale = new Vector3(logSr.transform.localScale.x * k, logSr.transform.localScale.y * k, 1f);
                    fitCross = cross;
                }
            }
            trigger = new TrapListener(Str("trigger", ""));
            disabler = new TrapListener(Str("disable_on", ""));
        }

        /// <summary>Tâm khúc gỗ lúc bắt đầu (sát mép đầu làn) / lúc kết thúc.</summary>
        private Vector3 StartPos()
        {
            Vector2 d = Dir, c = Rect.center;
            float half = Vertical ? Rect.height / 2f - 0.32f : Rect.width / 2f - 0.32f;
            return c - d * half;
        }
        private Vector3 EndPos()
        {
            Vector2 d = Dir, c = Rect.center;
            float half = Vertical ? Rect.height / 2f : Rect.width / 2f;
            return c + d * (half + 0.2f);
        }

        private bool Armed()
        {
            string w = Str("start_when", "");
            return string.IsNullOrEmpty(w) || Ctx.Flags.HasAll(w);
        }

        public override void Tick(Vector2 playerPos, bool inside)
        {
            if (disabler.Poll()) disabledUntil = Time.time + Float("disable_seconds", 6f);
            bool fired = trigger.Poll();
            float now = Time.time;
            if (!Armed() || now < disabledUntil) return;
            float period = Float("period", 0f);
            bool go = fired;
            if (period > 0f)
            {
                float c = Mathf.Floor((now + Float("offset", 0f)) / period);
                if (c != lastCycle) { if (lastCycle >= 0f) go = true; lastCycle = c; }
            }
            if (go && startAt < 0f && now >= readyAt) { startAt = now; hit.Clear(); playerHit = false; if (logSr != null) SoulRequiem.Core.Sfx.PlayAt("trap_roller", logSr.transform); }   // 17/11 (§294): âm
            if (startAt < 0f || logSr == null) return;

            float e = now - startAt - (hatch.Length > 0 ? HatchTime : 0.4f);
            if (e < 0f) return;
            float speed = Float("speed", 6f) * 0.32f;
            Vector3 a = StartPos(), b = EndPos();
            float len = Vector3.Distance(a, b);
            float t = e * speed / Mathf.Max(0.01f, len);
            if (t >= 1f)
            {
                logSr.enabled = false;
                startAt = -1f;
                readyAt = now + Float("rearm", 2f);
                return;
            }
            logSr.enabled = true;
            logSr.transform.position = Vector3.Lerp(a, b, t);
            logSr.sprite = log[(int)(e * 16f) % log.Length];

            // vùng cán: thân khúc gỗ (dẹt theo hướng lăn)
            var size = (Vector2)log[0].bounds.size;
            var box = Vertical ? new Vector2(size.x * 0.85f, 0.3f) : new Vector2(0.3f, size.y * 0.75f);
            if (fitCross > 0f) box = Vertical ? new Vector2(fitCross, 0.3f) : new Vector2(0.3f, fitCross);   // kín làn: áp sát vách vẫn dính
            var hitRect = new Rect((Vector2)logSr.transform.position - box / 2f, box);
            if (!playerHit && hitRect.Contains(playerPos))
            {
                playerHit = true;
                TrapArt.HurtPlayerCrush(Ctx, Float("damage", 22f), Dir, Float("knock", 5f));
            }
            // 20/11 (§353): khúc gỗ lăn KHÔNG cán quái — chỉ Ren dính.
        }

        public override void Animate()
        {
            if (hatchSr == null) return;
            int f = 0;
            if (startAt >= 0f) f = Mathf.Min(hatch.Length - 1, (int)((Time.time - startAt) / HatchTime * hatch.Length));
            hatchSr.sprite = hatch[f];
        }

        protected override void OnStopped() { if (logSr != null) logSr.enabled = false; }

        internal override void DebugSolve() { }
    }
}
