using System.Collections;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 17/11 (§307): THE REGISTRAR — người giữ Sổ Tên của the Choir (Map 4, Scriptorium; hình MEGAPACK GameMaster: chỉ có Idle / ATK / Upgrade).
    /// Đứng yên trên bệ cạnh Sổ Tên (không có hoạt ảnh đi), mọi đòn là phép:
    ///   Struck Names : ATK — gạch tên Ren: 2 (pha 2: 3) vạch đá rơi đúng chỗ Ren đứng, cách nhau 0,32 s (GroundStrike, có báo trước).
    ///   Ledger Bolts : ATK — quạt 3 (pha 2: 5) đạn thánh ca hướng về Ren.
    ///   Bị áp sát lâu: sóng đẩy lùi (không mất máu) "Stand back from the Register."
    ///   Pha 2 (≤ 50%): Upgrade (bất tử khi diễn), gọi 2 Choir Cutthroat, nhịp nhanh hơn.
    ///   Chết: không có hoạt ảnh chết -> EnemyAI đứng hình rồi tan biến.
    /// </summary>
    public sealed class RegistrarSkills : BossSkillsBase
    {
        public string summon = "Choir_Cutthroat";
        public float upgradeSeconds = 2.6f;
        public float boltSpeed = 2.4f;
        private static readonly Color Ink = new Color(0.75f, 0.85f, 1f);

        protected override void OnEngage()
        {
            pinnedTimeMax = 1.6f; repositionCooldown = 3.5f;
            Use("strike", 1.5f); Use("ledger", 0.8f);
        }

        protected override int PhaseFor(float hpPercent) => hpPercent <= 0.5f ? 2 : 1;

        protected override IEnumerator Think()
        {
            ai.Desired = Vector2.zero;                                  // không bao giờ rời bệ
            if (Broken) { yield return null; yield break; }
            ai.FaceX((PlayerPos - Pos).x);
            if (ReadyG("strike")) { yield return StruckNames(); yield break; }
            if (ReadyG("ledger")) { yield return LedgerBolts(); yield break; }
            yield return ai.WaitFor(0.35f);
        }

        // bị áp sát lâu: đẩy lùi bằng sóng mực (không mất máu)
        protected override IEnumerator Reposition()
        {
            busy = true;
            ai.Desired = Vector2.zero;
            ai.PlayState("Attack");
            yield return Telegraph(0.35f, Ink);
            if (hp.IsDead) { busy = false; yield break; }
            var w = Wave(Pos, 1.7f, 0.5f, 0f, "m3_wave_hymn");
            w.knockback = 3.6f;
            Hint("\"Stand back from the Register.\"", 1.8f);
            yield return ai.WaitFor(0.6f);
            ai.PlayState("Idle");
            busy = false;
        }

        protected override IEnumerator OnPhase(int p)
        {
            if (p != 2) yield break;
            hp.Invulnerable = true;
            ai.Desired = Vector2.zero;
            ai.PlayState("Upgrade");
            Hint("\"Every name in this book was struck by my hand. Yours will be the last.\"", 3f);
            yield return ai.WaitFor(upgradeSeconds * 0.5f);
            var dirr = MapEncounterDirector.Instance;
            for (int i = 0; i < 2 && dirr != null; i++)
            {
                Vector2 at = ClampArena(Pos + new Vector2(i == 0 ? -1.1f : 1.1f, -0.7f), 0.9f);
                SpriteVfx.Spawn("m3_vespers", at, 0.6f, Layer, Order, false, 14f);
                dirr.SpawnSummon(summon, at, ai);
            }
            yield return ai.WaitFor(upgradeSeconds * 0.5f);
            hp.Invulnerable = false;
            ai.PlayState("Idle");
        }

        private IEnumerator StruckNames()
        {
            SfxHooks.BossMove(this, "StruckNames"); // 19/11 (§335): âm
            Use("strike", phase == 1 ? 5.5f : 4.2f);
            ai.PlayState("Attack");
            if (ai.Vfx != null) ai.Vfx.Cast();
            yield return Telegraph(0.35f, Ink);
            var frames = SpriteVfx.Load("m3_choir_lance");
            int n = phase == 1 ? 2 : 3;
            for (int i = 0; i < n && !hp.IsDead && ai.Player != null; i++)
            {
                var g = GroundStrike.Spawn(PlayerPos, 0.8f, 0.32f, ai.damage, frames, 2, 16f, Order + 1, Ink);
                g.bodyScale = 0.7f;
                yield return ai.WaitFor(0.32f);
            }
            yield return ai.WaitFor(0.3f);
            ai.PlayState("Idle");
        }

        private IEnumerator LedgerBolts()
        {
            SfxHooks.BossMove(this, "LedgerBolts"); // 19/11 (§335): âm
            Use("ledger", phase == 1 ? 3.2f : 2.4f);
            ai.PlayState("Attack");
            if (ai.Vfx != null) ai.Vfx.Cast();
            yield return ai.WaitFor(0.45f);
            if (hp.IsDead || ai.Player == null) yield break;
            Vector2 aim = (PlayerPos - Pos).normalized;
            float baseA = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            int half = phase == 1 ? 1 : 2;
            for (int k = -half; k <= half; k++)
            {
                float a = (baseA + k * 14f) * Mathf.Deg2Rad;
                ai.ShootProjectile(new Vector2(Mathf.Cos(a), Mathf.Sin(a)), boltSpeed, ai.damage * 0.6f, 3.2f);
            }
            yield return ai.WaitFor(0.4f);
            ai.PlayState("Idle");
        }
    }
}
