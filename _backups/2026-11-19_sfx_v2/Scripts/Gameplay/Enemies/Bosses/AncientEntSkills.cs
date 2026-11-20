using System.Collections;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// ANCIENT ENT — miniboss Vườn Sám Hối (V6). Cây uống Ichor từ mộ: rễ đỏ là dấu hiệu. Hoạt ảnh gói MEGAPACK chỉ có
    /// Idle / Move / Prepare (ngực sáng xanh) / Dash (phun Ichor) / Recover, nên mọi chiêu đều "gồng ngực -> phun":
    ///   Root Line   : gồng 0,6 s rồi rễ gai trồi nối nhau theo đường thẳng về phía người chơi (pha 2: 3 đường hình quạt). Né ngang.
    ///   Ichor Volley: phun 4-6 cục Ichor bay vòng cung, rơi vào các vòng cảnh báo quanh người chơi. Đọc vòng trên đất.
    ///   Bramble Ring: gồng lâu 0,9 s, rồi một vòng rễ lan ra có 2 khe hở. Tìm khe hoặc lăn qua.
    ///   Grasp       : người chơi đứng sát -> rễ trồi dưới chân sau 0,6 s, trúng thì bị giữ chân 0,8 s.
    ///   Communion   : máu dưới 50%: cắm rễ xuống đất uống Ichor 3 s (hồi tới 24% máu). Đánh trúng 4 lần là giật đứt rễ, nó choáng 2 s.
    ///   Pha 2       : gọi 2 Penitent Ent con mọc lên từ rễ.
    /// </summary>
    public class AncientEntSkills : MonoBehaviour, IEnemySkills
    {
        public float lineWarn = 0.6f, lineStep = 0.42f, lineDelay = 0.07f, spikeRadius = 0.26f;
        public int lineCount = 7;
        public float volleyWarn = 0.85f, ringWarn = 0.9f, ringRadius = 2.6f, ringTime = 1.5f;
        public float graspWarn = 0.6f, graspStun = 0.8f;
        public float healPerSecond = 0.08f, healTime = 3f, healEvery = 12f;
        public int healBreakHits = 4;
        public float vfxScale = 0.7f;
        public float skillEvery = 2.6f;

        private EnemyAI ai;
        private bool engaged, summoned;
        private float nextSkill, nextHeal, nextGrasp;
        private int heals, hitsTaken, rot;
        private static readonly Color Root = new Color(0.85f, 0.3f, 0.25f, 1f);
        private static readonly Color Ichor = new Color(0.45f, 1f, 0.45f, 1f);

        private int Layer => ai.SortingLayer;
        private int Order => ai.SortingOrder;
        private Vector2 Pos => transform.position;

        private void Awake()
        {
            var h = GetComponent<Health>();
            if (h != null) h.Damaged += (_, __) => hitsTaken++;
        }

        public IEnumerator Act(EnemyAI owner)
        {
            ai = owner;
            if (ai.Player == null) yield break;
            if (!engaged) { engaged = true; nextSkill = Time.time + 1.2f; nextHeal = Time.time + 4f; }

            if (ai.InPhase2 && !summoned) { yield return Summon(); yield break; }
            if (ai.Health.Percent < 0.5f && heals < 2 && Time.time >= nextHeal) { yield return Communion(); yield break; }

            Vector2 to = (Vector2)ai.Player.position - Pos;
            float d = to.magnitude;
            ai.FaceX(to.x);
            if (ai.GapToPlayer() < 0.45f && Time.time >= nextGrasp) { yield return Grasp(); yield break; }
            if (Time.time >= nextSkill && d < 5f)
            {
                int pick = rot++ % 3;
                if (pick == 0) yield return RootLine();
                else if (pick == 1) yield return Volley();
                else yield return BrambleRing();
                nextSkill = Time.time + skillEvery * (ai.InPhase2 ? 0.7f : 1f);
                yield break;
            }
            // giữ khoảng cách vừa tầm phun, không áp sát
            ai.Desired = d > 2.2f ? to.normalized * ai.moveSpeed : d < 1.2f ? -to.normalized * ai.moveSpeed * 0.6f : Vector2.zero;
            ai.PlayState(ai.Desired.sqrMagnitude > 0.001f ? "Move" : "Idle", false);
        }

        private IEnumerator Gather(float seconds)
        {
            ai.Desired = Vector2.zero;
            ai.PlayState("Prepare");
            yield return ai.Flash(seconds, new Color(0.6f, 1f, 0.6f));
        }

        private IEnumerator Release(float hold)
        {
            ai.PlayState("Dash");
            yield return ai.WaitFor(hold);
            ai.PlayState("Recover");
            yield return ai.WaitFor(0.45f);
            ai.PlayState("Idle");
        }

        // ------------------------------------------------------------------ Root Line
        private IEnumerator RootLine()
        {
            yield return Gather(lineWarn);
            if (ai.Health.IsDead || ai.Player == null) yield break;
            Vector2 dir = ((Vector2)ai.Player.position - Pos).normalized;
            float[] fan = ai.InPhase2 ? new[] { -26f, 0f, 26f } : new[] { 0f };
            var frames = SpriteVfx.Load("m3_root_spike");
            foreach (var a in fan)
            {
                Vector2 dd = Quaternion.Euler(0, 0, a) * dir;
                for (int i = 0; i < lineCount; i++)
                {
                    Vector2 p = Pos + dd * (0.45f + i * lineStep);
                    var g = GroundStrike.Spawn(p, 0.35f + i * lineDelay, spikeRadius, ai.damage, frames, 2, 14f, Order, Root);
                    g.bodyScale = vfxScale;
                    g.knockback = 1.5f;
                }
            }
            yield return Release(0.5f);
        }

        // ------------------------------------------------------------------ Ichor Volley
        private IEnumerator Volley()
        {
            yield return Gather(0.45f);
            if (ai.Health.IsDead || ai.Player == null) yield break;
            var glob = SpriteVfx.Load("m3_ichor_glob");
            var splash = SpriteVfx.Load("m3_ground_burst");
            int n = ai.InPhase2 ? 6 : 4;
            Vector2 target = ai.Player.position;
            Vector2 mouth = Pos + Vector2.up * 0.35f;
            for (int i = 0; i < n; i++)
            {
                Vector2 p = i == 0 ? target : target + Random.insideUnitCircle * 1.3f;
                var g = GroundStrike.Spawn(p, volleyWarn + i * 0.1f, 0.32f, ai.damage * 0.8f, splash, 1, 14f, Order, Ichor);
                g.bodyScale = vfxScale;
                if (glob.Length > 0) { g.shell = glob[0]; g.shellFrom = mouth; g.shellArc = 0.9f; g.shellScale = vfxScale; }
            }
            yield return Release(0.6f);
        }

        // ------------------------------------------------------------------ Bramble Ring
        private IEnumerator BrambleRing()
        {
            yield return Gather(ringWarn);
            if (ai.Health.IsDead) yield break;
            float g0 = Random.Range(0f, 360f);
            var w = Shockwave.Spawn(Pos, ringRadius, ringTime, ai.damage * 0.9f, Order - 1, Root);
            w.gapAngles = new[] { g0, g0 + 180f };
            w.gapWidth = 50f;
            StartCoroutine(RingSpikes(g0));
            yield return Release(0.6f);
        }

        /// Rễ gai mọc theo vòng sóng (chỉ để nhìn: sát thương do Shockwave tính).
        private IEnumerator RingSpikes(float g0)
        {
            float[] radii = { 0.9f, 1.7f, 2.5f };
            foreach (var r in radii)
            {
                // thời điểm sóng đi qua bán kính r: e = 1-(1-k)^2 = r/R
                float k = 1f - Mathf.Sqrt(Mathf.Max(0f, 1f - r / ringRadius));
                yield return new WaitForSeconds(Mathf.Max(0f, k * ringTime - (r == radii[0] ? 0f : 0.05f)));
                int n = Mathf.RoundToInt(5 + r * 4);
                for (int i = 0; i < n; i++)
                {
                    float a = i * 360f / n;
                    if (Mathf.Abs(Mathf.DeltaAngle(a, g0)) < 30f || Mathf.Abs(Mathf.DeltaAngle(a, g0 + 180f)) < 30f) continue;
                    Vector2 p = Pos + new Vector2(Mathf.Cos(a * Mathf.Deg2Rad) * r, Mathf.Sin(a * Mathf.Deg2Rad) * r * 0.6f);
                    SpriteVfx.Spawn("m3_root_spike", p, vfxScale * 0.8f, Layer, Order, false, 18f);
                }
                if (ai == null || ai.Health.IsDead) yield break;
            }
        }

        // ------------------------------------------------------------------ Grasp
        private IEnumerator Grasp()
        {
            nextGrasp = Time.time + 4f;
            ai.Desired = Vector2.zero;
            ai.PlayState("Prepare");
            var g = GroundStrike.Spawn(ai.Player.position, graspWarn, 0.36f, ai.damage * 0.6f, SpriteVfx.Load("m3_root_spike"), 2, 14f,
                                       Order + 1, Root);
            g.bodyScale = vfxScale;
            g.stunSeconds = graspStun;
            yield return ai.WaitFor(graspWarn);
            yield return Release(0.3f);
        }

        // ------------------------------------------------------------------ Communion (hồi máu)
        private IEnumerator Communion()
        {
            heals++;
            nextHeal = Time.time + healEvery;
            ai.Desired = Vector2.zero;
            ai.PlayState("Prepare");
            Hints.Show("The Ancient Ent drinks from the graves. Tear it loose!", 2.5f);
            var pool = SpriteVfx.SpawnAttached("m3_sap_communion", transform, vfxScale, Layer, Order - 1, 12f, healTime);
            hitsTaken = 0;
            float t = 0f;
            bool broken = false;
            while (t < healTime && !ai.Health.IsDead)
            {
                if (hitsTaken >= healBreakHits) { broken = true; break; }
                ai.Health.Heal(ai.Health.Max * healPerSecond * Time.deltaTime);
                t += Time.deltaTime;
                yield return null;
            }
            if (pool != null) pool.Stop();
            if (ai.Health.IsDead) yield break;
            ai.PlayState("Recover");
            if (broken)
            {
                // 16/11 (§269): bỏ thông báo thừa — hình / hoạt ảnh / bảng tên boss đã thể hiện
                yield return ai.Flash(2f, new Color(1f, 1f, 0.55f));
            }
            else yield return ai.WaitFor(0.5f);
            ai.PlayState("Idle");
        }

        // ------------------------------------------------------------------ Pha 2: gọi cây con
        private IEnumerator Summon()
        {
            summoned = true;
            yield return Gather(0.7f);
            var dirr = MapEncounterDirector.Instance;
            for (int i = 0; i < 2 && dirr != null; i++)
            {
                Vector2 p = Pos + new Vector2(i == 0 ? -1.1f : 1.1f, -0.3f);
                SpriteVfx.Spawn("m3_root_spike", p, vfxScale, Layer, Order, false, 14f);
                dirr.SpawnSummon("Ent_1", p, ai);
            }
            // 16/11 (§269): bỏ thông báo thừa — hình / hoạt ảnh / bảng tên boss đã thể hiện
            yield return Release(0.5f);
        }
    }
}
