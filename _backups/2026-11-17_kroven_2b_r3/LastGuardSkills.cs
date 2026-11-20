using System.Collections;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 16/11 (§281) THE LAST GUARD — cận vệ cuối cùng của vua Nolan (Kroven), boss nhỏ Map 2b The Kroven Deep.
    /// Khiên + giáo. Mỗi chiêu có báo trước, mỗi cách phá có chỗ:
    ///   Shield Wall  : khiên luôn quay về phía Ren nhưng quay CHẬM (`turnRate` độ/giây). Đòn có hướng đánh vào mặt khiên
    ///                  (trong `blockArc` độ quanh hướng khiên) bị dội, không mất máu (Health.BlockCheck) -> lăn ra hông / sau lưng.
    ///                  Đang hồi đòn (sau Thrust / Bash) khiên hạ xuống: không chặn. Độc / cháy / chảy máu vẫn ăn.
    ///   Spear Thrust : vạch đỏ theo hướng đâm `thrustWarn` giây (hướng khoá ở 75% thời gian báo) rồi lao giáo dài.
    ///   Shield Bash  : Ren sát mặt khiên -> khựng `bashWarn` giây (nhấp nháy) rồi húc ngắn, đẩy lùi mạnh.
    ///   Last Stand   : pha 2 (≤ 50% máu, EnemyAI.phase2At) — cắm giáo, rêu Ether bùng xanh, BỎ KHIÊN (chém được mọi phía);
    ///                  thêm Sweep: vòng đỏ quanh hắn `sweepWarn` giây rồi quét giáo một vòng. Thrust nhanh hơn.
    /// Bẫy của The Kroven Deep tắt khi khu đấu được dọn (cờ &lt;khu&gt;_cleared, thuộc tính `stop_when` của bẫy) — không cần code ở đây.
    /// </summary>
    public class LastGuardSkills : MonoBehaviour, IEnemySkills
    {
        [Header("Shield Wall")]
        public float turnRate = 110f, blockArc = 140f;
        [Header("Spear Thrust")]
        public float thrustWarn = 0.65f, thrustSpeed = 6.5f, thrustTime = 0.28f, thrustLength = 2.7f, thrustEvery = 3.2f, thrustDamage = 18f;
        [Header("Shield Bash")]
        public float bashWarn = 0.4f, bashSpeed = 4.5f, bashTime = 0.16f, bashKnock = 5f, bashEvery = 2.6f, bashDamage = 12f;
        [Header("Last Stand / Sweep")]
        public float sweepWarn = 0.85f, sweepRadius = 1.6f, sweepEvery = 4.6f, sweepDamage = 20f;

        private EnemyAI ai;
        private Health hp;
        private bool engaged, lastStand, recovering;
        private float nextThrust, nextBash, nextSweep, blockedHintAt = -99f;
        private Vector2 guardDir = Vector2.down;
        private SpriteRenderer line;
        private static Sprite pixel;
        private static readonly Color Warn = new Color(1f, 0.32f, 0.26f, 1f);
        private static readonly Color Moss = new Color(0.45f, 1f, 0.55f, 1f);

        private int Layer => ai != null ? ai.SortingLayer : 0;
        private int Order => ai != null ? ai.SortingOrder : 2;

        private void Awake()
        {
            hp = GetComponent<Health>();
            if (hp != null)
            {
                hp.BlockCheck = Blocks;
                hp.Blocked += OnBlocked;
                hp.Died += _ => { hp.BlockCheck = null; KillLine(); };
            }
        }

        private void OnDisable() => KillLine();

        /// Đòn đánh vào mặt khiên? (`hit` = hướng đẩy của đòn, từ Ren tới hắn -> đòn tới từ phía -hit)
        private bool Blocks(Vector2 hit)
        {
            if (lastStand || recovering || hp == null || hp.IsDead) return false;
            Vector2 from = -hit.normalized;
            return Vector2.Angle(from, guardDir) <= blockArc * 0.5f;
        }

        private void OnBlocked(Vector2 hit)
        {
            Vector2 at = (Vector2)transform.position + guardDir * 0.22f + Vector2.up * 0.22f;
            SpriteVfx.Spawn("hit", at, 0.45f, Layer, Order + 1, false, 18f);
            CameraFollow.KickMain(hit, 0.02f, 0.08f);
            if (Time.time - blockedHintAt > 30f)
            {
                blockedHintAt = Time.time;
                Hints.Show("The shield turns your blade. Get to his side, or behind him.", 3f);
            }
        }

        private void Update()
        {
            if (ai == null || ai.Player == null || hp == null || hp.IsDead) return;
            // khiên quay chậm về phía Ren (pha 2 không còn khiên)
            Vector2 want = ai.DirToPlayer.sqrMagnitude > 0.0001f ? ai.DirToPlayer.normalized : guardDir;
            float rate = recovering ? turnRate * 0.35f : turnRate;
            float a = Vector2.SignedAngle(guardDir, want);
            float step = Mathf.Clamp(a, -rate * Time.deltaTime, rate * Time.deltaTime);
            guardDir = (Quaternion.Euler(0, 0, step) * guardDir).normalized;
            if (line != null && hp.IsDead) KillLine();
        }

        public IEnumerator Act(EnemyAI owner)
        {
            ai = owner;
            if (ai.Player == null) yield break;
            if (!engaged)
            {
                engaged = true;
                guardDir = ai.DirToPlayer.normalized;
                nextThrust = Time.time + 1.8f;
                nextBash = Time.time + 1.2f;
                nextSweep = Time.time + 2.5f;
            }
            if (ai.InPhase2 && !lastStand) { yield return LastStand(); yield break; }

            Vector2 to = ai.DirToPlayer;
            float d = to.magnitude, gap = ai.GapToPlayer();
            if (lastStand && Time.time >= nextSweep && d < sweepRadius + 0.4f) { yield return Sweep(); yield break; }
            if (!lastStand && Time.time >= nextBash && gap < 0.35f) { yield return Bash(); yield break; }
            if (Time.time >= nextThrust && d > 0.7f && d < thrustLength + 0.3f) { yield return Thrust(); yield break; }

            // tiến chậm, mặt (và khiên) luôn hướng về Ren; sát quá thì đứng thủ
            ai.LookAtPlayer = true;
            if (gap < 0.3f) { ai.Desired = Vector2.zero; ai.PlayState("Idle", false); yield break; }
            ai.MoveAnimSpeed = lastStand ? 1f : 0.75f;
            ai.Desired = to.normalized * ai.moveSpeed * (lastStand ? 1.15f : 0.75f);
            ai.PlayState("Move", false);
        }

        // ------------------------------------------------------------------ Spear Thrust
        private IEnumerator Thrust()
        {
            ai.Desired = Vector2.zero;
            ai.LookAtPlayer = false;
            float warn = lastStand ? thrustWarn * 0.75f : thrustWarn;
            Vector2 dir = ai.DirToPlayer.normalized;
            ai.FaceX(dir.x);
            ai.PlayState("Idle");
            var l = line = MakeLine(thrustLength);
            float t = 0f;
            while (t < warn && !hp.IsDead)
            {
                if (ai.Player != null && t < warn * 0.75f) dir = ai.DirToPlayer.normalized;
                ai.FaceX(dir.x);
                l.transform.position = transform.position;
                l.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                l.color = new Color(Warn.r, Warn.g, Warn.b, Mathf.Repeat(Time.time * 8f, 1f) < 0.5f ? 0.65f : 0.25f);
                t += Time.deltaTime;
                yield return null;
            }
            KillLine();
            if (hp.IsDead) yield break;
            ai.PlayState(ai.HasAnimState("Thrust") ? "Thrust" : "Idle");
            bool hit = false;
            t = 0f;
            while (t < thrustTime && !hp.IsDead)
            {
                ai.Desired = dir * thrustSpeed;
                if (!hit && ai.GapToPlayer() <= 0.25f) { ai.Hurt(thrustDamage, transform.position, 3f); hit = true; }
                t += Time.deltaTime;
                yield return null;
            }
            ai.Desired = Vector2.zero;
            recovering = true;                                   // giáo đã ra, khiên hạ: cửa sổ phản công
            yield return ai.WaitFor(lastStand ? 0.45f : 0.65f);
            recovering = false;
            ai.PlayState("Idle");
            nextThrust = Time.time + thrustEvery * (lastStand ? 0.7f : 1f);
        }

        // ------------------------------------------------------------------ Shield Bash
        private IEnumerator Bash()
        {
            ai.Desired = Vector2.zero;
            ai.LookAtPlayer = false;
            Vector2 dir = ai.DirToPlayer.normalized;
            ai.FaceX(dir.x);
            ai.PlayState(ai.HasAnimState("Bash") ? "Bash" : "Idle");
            yield return ai.Flash(bashWarn, new Color(1f, 0.85f, 0.6f));
            if (hp.IsDead) yield break;
            bool hit = false;
            float t = 0f;
            while (t < bashTime && !hp.IsDead)
            {
                ai.Desired = dir * bashSpeed;
                if (!hit && ai.GapToPlayer() <= 0.15f) { ai.Hurt(bashDamage, transform.position, bashKnock); hit = true; }
                t += Time.deltaTime;
                yield return null;
            }
            ai.Desired = Vector2.zero;
            if (hit) CameraFollow.KickMain(dir, 0.05f, 0.18f);
            recovering = true;
            yield return ai.WaitFor(0.45f);
            recovering = false;
            ai.PlayState("Idle");
            nextBash = Time.time + bashEvery;
        }

        // ------------------------------------------------------------------ Last Stand (vào pha 2)
        private IEnumerator LastStand()
        {
            lastStand = true;
            ai.Desired = Vector2.zero;
            ai.PlayState(ai.HasAnimState("Stagger") ? "Stagger" : "Idle");
            yield return ai.Flash(1.1f, Moss);
            if (hp.IsDead) yield break;
            CameraFollow.KickMain(Vector2.down, 0.05f, 0.3f);
            ai.PlayState("Idle");
            nextSweep = Time.time + 0.6f;
            nextThrust = Time.time + 2.2f;
        }

        // ------------------------------------------------------------------ Sweep (chỉ pha 2)
        private IEnumerator Sweep()
        {
            ai.Desired = Vector2.zero;
            ai.LookAtPlayer = true;
            ai.PlayState("Idle");
            var gs = GroundStrike.Spawn(transform.position, sweepWarn, sweepRadius, sweepDamage, null, 0, 14f, Order - 1, Warn);
            gs.knockback = 3f;
            yield return ai.WaitFor(Mathf.Max(0f, sweepWarn - 0.25f));
            if (hp.IsDead) yield break;
            ai.PlayState(ai.HasAnimState("Sweep") ? "Sweep" : "Idle");
            yield return ai.WaitFor(0.9f);
            ai.PlayState("Idle");
            nextSweep = Time.time + sweepEvery;
            yield return ai.WaitFor(0.25f);
        }

        // ------------------------------------------------------------------ tiện ích
        private void KillLine()
        {
            if (line != null) { Destroy(line.gameObject); line = null; }
        }

        private SpriteRenderer MakeLine(float length)
        {
            if (pixel == null)
            {
                var tex = new Texture2D(4, 4) { filterMode = FilterMode.Point };
                var px = new Color[16];
                for (int i = 0; i < 16; i++) px[i] = Color.white;
                tex.SetPixels(px);
                tex.Apply();
                pixel = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0f, 0.5f), 4f);
            }
            var go = new GameObject("LG_ThrustLine");
            var s = go.AddComponent<SpriteRenderer>();
            FxLighting.MakeUnlit(s);
            s.sprite = pixel;
            s.sortingLayerID = Layer;
            s.sortingOrder = Order - 1;
            go.transform.localScale = new Vector3(length, 0.12f, 1f);
            return s;
        }
    }
}
