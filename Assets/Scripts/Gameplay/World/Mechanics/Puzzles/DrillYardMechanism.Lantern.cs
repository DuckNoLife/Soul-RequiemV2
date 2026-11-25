using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// STATION 2 — LANTERN RUN (yard 2 of Map4d_DrillYard). Lantern pedestal at `lantern_at`, rack at `rack_at` (LEFT edge).
    /// Ren walks onto the pedestal = picks up a lantern (warm light follows him). WHILE HOLDING A LANTERN REN CANNOT ROLL (PlayerStatus.SealRoll every frame).
    /// Low yard (10 tiles) -> 5 LANES of dummies running VERTICALLY (alternating up / down, wrapping top - bottom), narrow safe strips between lanes.
    /// One run: hang all 3 lanterns within 75 s, 3 lives. Touching a dummy = -1 life (a held lantern goes out and returns to the pedestal).
    /// Every lane keeps its own constant speed for the whole run. Time / lives out = replay the whole station.
    /// </summary>
    public sealed partial class DrillYardMechanism
    {
        private const float LanternTime = 75f;                 // whole station (3 lanterns)
        private const int Lanterns3 = 3;
        private Vector2 lanternAt, rackAt;
        private SpriteRenderer pedestal, rack, lantern;
        private Light2D lanternLight;
        private readonly List<SpriteRenderer> hung = new List<SpriteRenderer>();
        private Sprite lanternLit, lanternOut;
        private bool carrying, lanternHome = true, lanternOn;
        private int delivered;
        private PlayerStatus status;

        // redrawn rack (front view) scaled 1.25x; 3 hooks at -0.14 / 0 / +0.14 u from centre, hook bottoms 0.31 u high
        private const float RackScale = 1.25f, HungScale = 0.75f, CarryScale = 0.8f;
        private static readonly float[] HookX = { -0.1375f, 0.0f, 0.1375f };

        private void InitLantern()
        {
            lanternAt = Ctx.TilePoint(Str("lantern_at", ""), new Vector2(A.xMin + 0.55f, A.center.y));
            rackAt = Ctx.TilePoint(Str("rack_at", ""), new Vector2(A.xMax - 0.6f, A.center.y));
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

        /// <summary>Lantern back to the pedestal (unlit). clearRack = also remove hung lanterns (restarting station 2); after passing, hung lanterns stay for looks.</summary>
        private void ResetLantern(bool clearRack = false)
        {
            carrying = false; lanternHome = true; lanternOn = false;
            if (lantern != null) { lantern.transform.position = LanternRest; lantern.sprite = lanternOut; }
            if (!clearRack) return;
            foreach (var h in hung) if (h != null) Object.Destroy(h.gameObject);
            hung.Clear();
            delivered = 0;
        }

        private IEnumerator Lanterns()
        {
            ResetLantern(true);
            lanternOn = true; lantern.sprite = lanternLit;
            DrillBoard.Goal = "LANTERNS 0 / " + Lanterns3; DrillBoard.GoalFrac = 0f;
            // 5 VERTICAL lanes between pedestal and rack, ~0.95 u apart (narrow safe strips between lanes): telegraphed before the dummies rush in
            const int Lanes = 5;
            // inset 0.8 u INTO the yard along the pedestal -> rack axis
            float inward = rackAt.x >= lanternAt.x ? 1f : -1f;
            float x0 = lanternAt.x + 0.8f * inward, x1 = rackAt.x - 0.8f * inward;
            var laneX = new float[Lanes];
            for (int i = 0; i < Lanes; i++) laneX[i] = Mathf.Lerp(x0, x1, i / (float)(Lanes - 1));
            for (int i = 0; i < Lanes; i++)
            {
                int dir = (i % 2 == 0) ? -1 : 1;
                Vector2 wa = new Vector2(laneX[i], dir < 0 ? A.yMax - 0.3f : A.yMin + 0.2f), wb = new Vector2(laneX[i], dir < 0 ? A.yMin + 0.2f : A.yMax - 0.3f);
                DrillFx.Warn(Ctx.Host, wa, wb, 1.4f, new Vector2(0f, dir), Ctx.Player, 0.45f);
            }
            Sfx.Play("rf_war_drum", A.center, 0.6f);
            Tip("Walk onto the pedestal to take the lantern. Carry it to the rack. You can't roll while carrying it.", 4.5f);
            yield return Wait(1.4f);
            if (aborted || stationFailed) yield break;
            string[] kinds = { "straw", "pell", "sack", "straw", "pell" };
            float[] speeds = { 1.15f, 1.55f, 1.3f, 1.75f, 1.4f };
            float[] gaps = { 1.25f, 1.5f, 1.15f, 1.6f, 1.35f };
            var lanesActors = new List<(DrillActor a, int lane)>();
            float span = A.height + 1.2f;
            for (int i = 0; i < Lanes; i++)
            {
                int dir = (i % 2 == 0) ? -1 : 1;
                int n = Mathf.Max(2, Mathf.FloorToInt(span / gaps[i]));
                float off = Random.Range(0f, gaps[i]);
                for (int k = 0; k < n; k++)
                {
                    float y = A.yMin - 0.6f + Mathf.Repeat(off + k * span / n, span);
                    var a = Spinner(kinds[i], new Vector2(laneX[i], y), new Vector2(0f, speeds[i] * dir));
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
                // lanes wrap top - bottom
                foreach (var (a, lane) in lanesActors)
                {
                    if (!actors.Contains(a)) continue;
                    float v = speeds[lane] * Mathf.Sign(a.Vel.y);
                    a.Vel = new Vector2(0f, v);
                    if (a.Pos.y > A.yMax + 0.6f) a.Pos.y -= span;
                    else if (a.Pos.y < A.yMin - 0.6f) a.Pos.y += span;
                    if (a.Credited && (a.Body - PlayerFeet).magnitude > 0.8f) a.Credited = false;
                }
                LanternLogic();                                                        // no free-rolling sandbags on the last lantern
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
            HangLantern(delivered);
            delivered++;
            DrillBoard.Goal = "LANTERNS " + delivered + " / " + Lanterns3;
            DrillBoard.GoalFrac = delivered / (float)Lanterns3;
            DrillBoard.Say(delivered >= Lanterns3 ? "ALL LANTERNS HUNG!" : "LANTERN HUNG!", Green);
            Sfx.Play("relic_absorb", rackAt, 0.6f);
            SpriteVfx.Spawn("duo_light_orb", rackAt + Vector2.up * 0.3f, 0.9f, Layer, Order + 2, false, 16f, false);
            // a fresh lantern is lit on the pedestal for the next trip
            lanternHome = true; lantern.transform.position = LanternRest;
            if (delivered < Lanterns3) { lanternOn = true; lantern.sprite = lanternLit; Sfx.Play("trap_fire", LanternRest, 0.35f); }
            else { lanternOn = false; lantern.sprite = null; }
            if (delivered < Lanterns3) Bark(delivered == 1 ? "One lamp! Two to go - don't get cocky!" : "Two lamps! One more! Mind the hems, they're FRESHLY STARCHED!", 2.6f, true);
        }

        /// <summary>Lantern hanging on hook i of the rack (with light).</summary>
        private void HangLantern(int i)
        {
            i = Mathf.Clamp(i, 0, HookX.Length - 1);
            var sr = TrapArt.MakeRenderer(Ctx.Host.transform, "Drill Hung Lantern", rackAt + new Vector2(HookX[i], 0.31f - 0.31f * HungScale + 0.01f), Ctx.Player, 1);
            sr.sprite = lanternLit;
            sr.transform.localScale = new Vector3(HungScale, HungScale, 1f);
            var lg = new GameObject("light");
            lg.transform.SetParent(sr.transform, false);
            lg.transform.localPosition = new Vector3(0f, 0.14f, 0f);
            var l = lg.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point; l.color = new Color(1f, 0.72f, 0.38f); l.pointLightOuterRadius = 1.2f; l.intensity = 0.9f;
            hung.Add(sr);
        }

        /// <summary>Hit while holding a lantern: it goes out (smoke) and is relit back on the pedestal.</summary>
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
            if (phase == Phase.Running && station == 1 && delivered < Lanterns3)
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
                // lantern swinging at Ren's side
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
