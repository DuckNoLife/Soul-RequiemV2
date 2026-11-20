using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 20/11 (§360): TRẠM 2 — LANTERN RUN (kiểu Frogger). Bệ đèn ở `lantern_at` (ô Tiled), giá treo đèn ở `rack_at`.
    /// Ren bước tới bệ = tự cầm đèn (đèn lồng PixelLab, có ánh sáng ấm theo người). ĐANG CẦM ĐÈN THÌ KHÔNG LĂN ĐƯỢC
    /// (PlayerStatus.SealRoll mỗi khung — HUD hiện SEALED). Tới giá = treo đèn (lên 1 / 3, các làn chạy nhanh hơn).
    /// 6 làn người nộm xoay chạy ngược chiều nhau quấn quanh sân; chạm = đèn tắt (khói), đèn về bệ, -1 tinh thần. 3 đèn trong 75 s.
    /// </summary>
    public sealed partial class DrillYardMechanism
    {
        private const float LanternTime = 75f;
        private const int Lanterns3 = 3;
        private Vector2 lanternAt, rackAt;
        private SpriteRenderer pedestal, rack, lantern;
        private Light2D lanternLight;
        private readonly List<SpriteRenderer> hung = new List<SpriteRenderer>();
        private Sprite lanternLit, lanternOut;
        private bool carrying, lanternHome = true, lanternOn;
        private int delivered;
        private float laneSpeedMul = 1f;
        private PlayerStatus status;

        // §360b: giá đèn vẽ lại (nhìn thẳng) phóng 1,25 lần; 3 móc cách tâm -0,14 / 0 / +0,14 đv, đáy móc cao 0,31 đv
        private const float RackScale = 1.25f, HungScale = 0.75f, CarryScale = 0.8f;
        private static readonly float[] HookX = { -0.1375f, 0.0f, 0.1375f };

        private void InitLantern()
        {
            lanternAt = Ctx.TilePoint(Str("lantern_at", ""), new Vector2(A.xMax - 1.6f, A.yMin + 0.6f));
            rackAt = Ctx.TilePoint(Str("rack_at", ""), new Vector2(A.xMin + 1.6f, A.yMax - 0.5f));
            pedestal = Prop("Drill Lantern Pedestal", lanternAt, "VFX/props/drill_keyped", 0);
            rack = Prop("Drill Lantern Rack", rackAt, "VFX/props/drill_rack", 0);
            rack.transform.localScale = new Vector3(RackScale, RackScale, 1f);
            lanternLit = WardenStatueMechanism.Load("VFX/props/drill_lantern");
            lanternOut = WardenStatueMechanism.Load("VFX/props/drill_lantern_out");
            lantern = TrapArt.MakeRenderer(Ctx.Host.transform, "Drill Lantern", LanternRest, Ctx.Player, 1);
            lantern.sprite = lanternOut;
            var lg = new GameObject("Drill Lantern Light");
            lg.transform.SetParent(lantern.transform, false);
            lg.transform.localPosition = new Vector3(0f, 0.14f, 0f);
            lanternLight = lg.AddComponent<Light2D>();
            lanternLight.lightType = Light2D.LightType.Point;
            lanternLight.color = new Color(1f, 0.72f, 0.38f);
            lanternLight.pointLightOuterRadius = 1.3f;
            lanternLight.intensity = 0f;
            AddBlocker(pedestal.transform.position, new Vector2(0.24f, 0.12f));
            AddBlocker(rack.transform.position, new Vector2(0.14f, 0.1f));
            status = Ctx.Player != null ? PlayerStatus.Of(Ctx.Player) : null;
        }

        private Vector2 LanternRest => lanternAt + Vector2.up * 0.24f;

        private SpriteRenderer Prop(string name, Vector2 feet, string sprite, int order)
        {
            var sr = TrapArt.MakeRenderer(Ctx.Host.transform, name, feet, Ctx.Player, order);
            sr.sprite = WardenStatueMechanism.Load(sprite);
            return sr;
        }

        private GameObject AddBlocker(Vector2 feet, Vector2 size)
        {
            var go = new GameObject("Drill Prop Collider");
            go.transform.SetParent(Ctx.Host.transform, true);
            go.transform.position = feet + Vector2.up * (size.y * 0.5f);
            go.AddComponent<BoxCollider2D>().size = size;
            return go;
        }

        /// <summary>Đèn về bệ (tắt). clearRack = gỡ cả đèn đã treo (bắt đầu lại trạm 2); qua trạm thì đèn treo ở lại cho đẹp.</summary>
        private void ResetLantern(bool clearRack = false)
        {
            carrying = false; lanternHome = true; lanternOn = false;
            if (lantern != null) { lantern.transform.position = LanternRest; lantern.sprite = lanternOut; }
            if (!clearRack) return;
            foreach (var h in hung) if (h != null) Object.Destroy(h.gameObject);
            hung.Clear();
            delivered = 0; laneSpeedMul = 1f;
        }

        private IEnumerator Lanterns()
        {
            ResetLantern(true);
            lanternOn = true; lantern.sprite = lanternLit;
            DrillBoard.Goal = "LANTERNS 0 / " + Lanterns3; DrillBoard.GoalFrac = 0f;
            // 6 làn giữa giá đèn (bắc) và bệ đèn (nam): báo trước cả 6 làn rồi người nộm mới ùa vào
            int lanes = 6;
            float yTop = rackAt.y - 0.55f, yBot = lanternAt.y + 0.5f;
            var laneY = new float[lanes];
            for (int i = 0; i < lanes; i++) laneY[i] = Mathf.Lerp(yTop, yBot, (i + 0.5f) / lanes);
            for (int i = 0; i < lanes; i++)
            {
                int dir = (i % 2 == 0) ? 1 : -1;
                DrillFx.Warn(Ctx.Host, new Vector2(A.xMin + 0.3f, laneY[i]), new Vector2(A.xMax - 0.3f, laneY[i]), 1.4f, new Vector2(dir, 0f), Ctx.Player, 0.5f);
            }
            Sfx.Play("rf_war_drum", A.center, 0.6f);
            Hints.Show("Walk onto the pedestal to take the lantern. Carry it to the rack. You can't roll while carrying it.", 4.5f);
            yield return Wait(1.4f);
            if (aborted || stationFailed) yield break;
            string[] kinds = { "straw", "sack", "pell", "straw", "pell", "sack" };
            float[] speeds = { 1.05f, 1.6f, 0.85f, 1.35f, 1.15f, 1.85f };
            float[] gaps = { 1.55f, 2.1f, 1.9f, 1.5f, 1.8f, 2.4f };
            var lanesActors = new List<(DrillActor a, int lane)>();
            float span = A.width + 1.2f;
            for (int i = 0; i < lanes; i++)
            {
                int dir = (i % 2 == 0) ? 1 : -1;
                int n = Mathf.Max(2, Mathf.FloorToInt(span / gaps[i]));
                float off = Random.Range(0f, gaps[i]);
                for (int k = 0; k < n; k++)
                {
                    float x = A.xMin - 0.6f + Mathf.Repeat(off + k * span / n, span);
                    var a = Spinner(kinds[i], new Vector2(x, laneY[i]), new Vector2(speeds[i] * dir, 0f));
                    lanesActors.Add((a, i));
                }
            }
            Sfx.Play("lg_whirl", A.center, 0.8f);
            float t0 = Time.time;
            while (!aborted && !stationFailed)
            {
                float left = LanternTime - (Time.time - t0);
                DrillBoard.TimeFrac = Mathf.Clamp01(left / LanternTime);
                DrillBoard.TimeText = Clock(Mathf.Max(0f, left));
                if (left <= 0f) { FailStation("TOO SLOW! The lamps went cold waiting for you!"); break; }
                // làn quấn quanh sân (ra mép này vào mép kia)
                foreach (var (a, lane) in lanesActors)
                {
                    if (!actors.Contains(a)) continue;
                    float v = speeds[lane] * laneSpeedMul * Mathf.Sign(a.Vel.x);
                    a.Vel = new Vector2(v, 0f);
                    if (a.Pos.x > A.xMax + 0.6f) a.Pos.x -= span;
                    else if (a.Pos.x < A.xMin - 0.6f) a.Pos.x += span;
                    if (a.Credited && (a.Body - PlayerFeet).magnitude > 0.8f) a.Credited = false;
                }
                LanternLogic();
                if (delivered >= Lanterns3) break;
                yield return null;
            }
            foreach (var (a, _) in lanesActors) Ctx.Run(Vanish(a));
        }

        private void LanternLogic()
        {
            Vector2 p = PlayerFeet;
            if (!carrying && lanternHome && lanternOn && (p - lanternAt).magnitude < 0.42f)
            {
                carrying = true; lanternHome = false;
                Sfx.Play("pickup_item", p, 0.7f);
                DrillBoard.Say("LANTERN TAKEN - NO ROLLING!", Sky);
            }
            if (carrying)
            {
                if (status != null) status.SealRoll(0.25f, null);
                if ((p - rackAt).magnitude < 0.55f) Deliver();
            }
        }

        private void Deliver()
        {
            carrying = false;
            int i = Mathf.Min(delivered, HookX.Length - 1);
            var sr = TrapArt.MakeRenderer(Ctx.Host.transform, "Drill Hung Lantern", rackAt + new Vector2(HookX[i], 0.31f - 0.31f * HungScale + 0.01f), Ctx.Player, 1);
            sr.sprite = lanternLit;
            sr.transform.localScale = new Vector3(HungScale, HungScale, 1f);
            var lg = new GameObject("light");
            lg.transform.SetParent(sr.transform, false);
            lg.transform.localPosition = new Vector3(0f, 0.14f, 0f);
            var l = lg.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point; l.color = new Color(1f, 0.72f, 0.38f); l.pointLightOuterRadius = 1.2f; l.intensity = 0.9f;
            hung.Add(sr);
            delivered++;
            laneSpeedMul = 1f + 0.14f * delivered;
            DrillBoard.Goal = "LANTERNS " + delivered + " / " + Lanterns3;
            DrillBoard.GoalFrac = delivered / (float)Lanterns3;
            DrillBoard.Say("LANTERN HUNG!", Green);
            Sfx.Play("relic_absorb", rackAt, 0.6f);
            SpriteVfx.Spawn("duo_light_orb", rackAt + Vector2.up * 0.3f, 0.9f, Layer, Order + 2, false, 16f, false);
            if (delivered < Lanterns3)
            {
                lanternHome = true; lanternOn = true;
                lantern.sprite = lanternLit;
                lantern.transform.position = LanternRest;
                Bark(delivered == 1 ? "ONE! Don't get cocky - my boys just got FASTER!" : "TWO! One more! Mind the hems, they're FRESHLY STARCHED!", 2.6f, true);
            }
            else lantern.sprite = null;
        }

        /// <summary>Dính đòn khi đang cầm đèn: đèn tắt (khói), về bệ thắp lại.</summary>
        private void Snuff(Vector2 at)
        {
            carrying = false;
            SpriteVfx.Spawn("drill/fx_snuff", lantern.transform.position + Vector3.up * 0.18f, 1f, Layer, Order + 3, false, 14f, false);
            Sfx.Play("gas_hiss", at, 0.6f);
            lantern.sprite = lanternOut;
            DrillBoard.Say("LIGHT OUT! BACK TO THE PEDESTAL", Red);
            Ctx.Run(Relight());
        }

        private IEnumerator Relight()
        {
            lanternOn = false;
            var from = (Vector2)lantern.transform.position;
            for (float t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                float u = t / 0.5f;
                lantern.transform.position = Vector2.Lerp(from, LanternRest, u) + Vector2.up * Mathf.Sin(u * Mathf.PI) * 0.6f;
                yield return null;
            }
            lantern.transform.position = LanternRest;
            lanternHome = true;
            if (phase == Phase.Running && station == 1)
            {
                yield return new WaitForSeconds(0.4f);
                lanternOn = true; lantern.sprite = lanternLit;
                Sfx.Play("trap_fire", LanternRest, 0.35f);
            }
        }

        private void AnimateLantern(float dt)
        {
            if (lantern == null) return;
            if (carrying)
            {
                // đèn lồng đung đưa bên tay Ren
                var pr = Ctx.Player != null ? Ctx.Player.GetComponentInChildren<SpriteRenderer>() : null;
                float side = pr != null && pr.flipX ? -1f : 1f;
                Vector2 hand = PlayerFeet + new Vector2(0.13f * side, 0.12f + 0.02f * Mathf.Sin(Time.time * 9f));
                lantern.transform.position = Vector2.Lerp(lantern.transform.position, hand, 1f - Mathf.Exp(-25f * dt));
                lantern.transform.rotation = Quaternion.Euler(0f, 0f, 6f * Mathf.Sin(Time.time * 7f));
                lantern.transform.localScale = new Vector3(CarryScale, CarryScale, 1f);
            }
            else { lantern.transform.rotation = Quaternion.identity; lantern.transform.localScale = Vector3.one; }
            bool lit = lantern.sprite == lanternLit && lantern.sprite != null;
            if (lanternLight != null) lanternLight.intensity = Mathf.MoveTowards(lanternLight.intensity, lit ? 0.85f + 0.1f * Mathf.Sin(Time.time * 11f) : 0f, dt * 4f);
        }
    }
}
