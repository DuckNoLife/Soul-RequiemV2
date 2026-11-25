using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// ENDING after LAST GRASP:
    ///   1) FINAL GRIP: the Vessel's right hand grabs and LIFTS Ren, squeezing; Ren strikes 3 times -> the hand cracks then SHATTERS, Ren drops;
    ///      the Vessel roars, glowing cracks (relic blue) spread over chest / head / shoulders, light pours through them -> white flash -> VesselDies (.Finale.cs).
    ///   2) Absalom appears RIGHT BESIDE THE PORTAL (inside the close camera frame), speaks, opens the portal and rushes in.
    ///   3) The portal STAYS OPEN; Ren walks in himself (EnterPortal, .Finale.cs) -> end-of-chapter screen (ToBeContinuedScreen: MAP 5 COMING SOON!, Back to home).
    ///      Re-entering the domain after Ren went through -> ComingSoonAgain shows that screen directly.
    /// ClosePortal is currently unused (the portal must stay open for Ren).
    /// </summary>
    public sealed partial class VesselDomainMechanism
    {
        private readonly List<GameObject> crackFx = new List<GameObject>();

        private IEnumerator FinalGrip()
        {
            actState = ActState.Rest;
            lastGrasp = false; noLie = false;
            ClearField();
            busy = true;
            BeginScene();
            CamWide(30f);
            const int side = 1;
            var player = Ctx.Player;
            var hp = Ctx.PlayerHealth;
            DrillBoard.Yell("MINE!", 1.4f);
            Roar(1.2f);
            yield return Wait(0.5f);
            // hand grabs down
            Vector2 at = Feet;
            for (float t = 0f; t < 0.5f; t += Time.deltaTime) { rig.SetHand(side, at + Vector2.up * 1.6f, 14f, "hand_claw"); yield return null; }
            rig.SetHand(side, at, 60f, "hand_fist");
            Sfx.Play("reg_grip", at, 1f);
            BossJuice.Shake(2);
            grabbing = true;
            // lift, squeeze
            for (float t = 0f; t < 1.3f; t += Time.deltaTime)
            {
                float lift = Mathf.Clamp01(t / 0.7f) * 1.4f;
                rig.SetHand(side, at + new Vector2(Mathf.Sin(t * 30f) * 0.03f, 0.25f + lift), 30f, "hand_fist");
                HoldRen();
                if (t > 0.6f && hp != null && Mathf.Repeat(t, 0.35f) < Time.deltaTime) { hp.TintPulse(new Color(1f, 0.4f, 0.4f), 0.2f); Sfx.Play("reg_grip", at, 0.5f); }
                yield return null;
            }
            DrillBoard.Yell("You... are... MINE...", 1.4f);
            // Ren strikes 3 times, the hand cracks more each time
            var combat = player != null ? player.GetComponent<PlayerCombat>() : null;
            Vector2 hand = rig.HandWorld(side);
            for (int k = 0; k < 3; k++)
            {
                HoldRen();
                if (combat != null) combat.PlayCutsceneSlash(k % 2 == 0 ? new Vector2(1f, 0.4f) : new Vector2(-1f, 0.6f));
                yield return Wait(0.12f);
                hand = rig.HandWorld(side);
                SpriteVfx.Spawn("hit", hand + Random.insideUnitCircle * 0.25f, 0.9f, layer, order + 8, k % 2 == 1, 18f, false);
                Sfx.Play("pl_swing", hand, 0.9f);
                Sfx.Play("glass_break", hand, 0.4f + 0.2f * k);
                BossJuice.Flash(hand, RelicBlue, 1.6f + 0.4f * k, 2.5f, 0.15f);
                BossJuice.Shake(1 + k / 2);
                AddBodyCrack(hand, 0.35f + 0.1f * k, 3, rig.ArmPathTransforms(side)[0]);
                for (int m = 0; m < 8; m++) AddMote(hand, Random.insideUnitCircle.normalized * Random.Range(1.5f, 3.5f), new Color(0.8f, 0.92f, 1f, 1f), 0.4f, 0.045f, true);
                for (float t = 0f; t < 0.32f; t += Time.deltaTime) { HoldRen(); rig.SetHand(side, rig.HandWorld(side) + new Vector2(Mathf.Sin(t * 70f) * 0.04f, 0f), 30f, "hand_fist"); yield return null; }
            }
            // the hand SHATTERS
            hand = rig.HandWorld(side);
            Sfx.Play("glass_break", hand, 1f);
            Sfx.Play("ward_break", hand, 1f);
            SpriteVfx.Spawn("abs_void_burst", hand, 1.4f, layer, order + 8, false, 16f, false);
            for (int m = 0; m < 30; m++) AddMote(hand, Random.insideUnitCircle.normalized * Random.Range(2f, 6f), Random.value < 0.5f ? new Color(0.85f, 0.92f, 1f, 1f) : new Color(0.45f, 0.4f, 0.6f, 1f), Random.Range(0.4f, 0.8f), Random.Range(0.04f, 0.08f), true);
            BossJuice.Flash(hand, Color.white, 4f, 4f, 0.35f);
            BossJuice.Shake(3);
            grabbing = false;
            ReleaseGrab(false);
            if (player != null)
            {
                Vector2 land = ClampFloor(at, 0.4f);
                var rb = player.GetComponent<Rigidbody2D>();
                if (rb != null) { rb.position = land; rb.linearVelocity = Vector2.zero; }
                player.position = new Vector3(land.x, land.y, player.position.z);
            }
            rig.SetArmAwake(side, false);
            rig.RestHand(side, 3f);
            slumpTarget = 0.35f;
            DrillBoard.Yell("NO-", 1f);
            yield return Wait(0.6f);
            // cracks CRAWL from the broken hand back up the wrist -> elbow -> shoulder -> chest,
            // then split into 3 branches at once: up to the head, across to the other shoulder, down the ribs. Each segment is attached to its bone, so it follows the motion.
            Vector2[] seats = { new Vector2(0f, 0f), new Vector2(-35f, -150f), new Vector2(40f, -120f), new Vector2(-90f, -40f), new Vector2(95f, -30f), new Vector2(0f, -205f), new Vector2(-20f, 70f) };
            Sfx.Play("quake_rumble", chest, 1f);
            var torso = rig.TorsoTransform;
            var armPath = new List<(Vector2 p, Transform a)>();
            foreach (var tr in rig.ArmPathTransforms(side)) if (tr != null) armPath.Add((tr.position, tr));
            yield return CrawlCrack(armPath, Float("crack_speed", 2.6f), 0.085f, 0.06f);
            Vector2 sh = armPath.Count > 0 ? armPath[armPath.Count - 1].p : rig.PointWorld(new Vector2(95f, -30f));
            Vector2 c0 = rig.PointWorld(new Vector2(0f, -10f));
            yield return CrawlCrack(new List<(Vector2, Transform)> { (sh, torso), (Vector2.Lerp(sh, c0, 0.5f) + Vector2.up * 0.15f, torso), (c0, torso) }, 2.6f, 0.08f, 0.065f);
            rig.Tint = new Color(1.15f, 1.2f, 1.35f);
            BossJuice.Shake(2);
            var b1 = Ctx.Run(CrawlCrack(new List<(Vector2, Transform)> { (c0, torso), (rig.PointWorld(new Vector2(-15f, -90f)), torso), (rig.PointWorld(new Vector2(10f, -150f)), torso), (rig.PointWorld(new Vector2(-5f, -200f)), torso) }, 3f, 0.07f, 0.05f));
            var b2 = Ctx.Run(CrawlCrack(new List<(Vector2, Transform)> { (c0, torso), (rig.PointWorld(new Vector2(-50f, -25f)), torso), (rig.PointWorld(new Vector2(-95f, -35f)), torso) }, 3f, 0.065f, 0.05f));
            var b3 = Ctx.Run(CrawlCrack(new List<(Vector2, Transform)> { (c0, torso), (rig.PointWorld(new Vector2(15f, 40f)), torso), (rig.PointWorld(new Vector2(-10f, 85f)), torso) }, 3f, 0.065f, 0.05f));
            yield return Wait(1.3f);
            rig.Tint = new Color(1.3f, 1.35f, 1.6f);
            jawHold = 1f;
            DrillBoard.Yell("AAAAH—", 1.2f);
            for (float t = 0f; t < 1.1f; t += Time.deltaTime)
            {
                rig.Tint = Color.Lerp(Color.white, new Color(1.6f, 1.6f, 2f), 0.5f + 0.5f * Mathf.Sin(t * 35f));
                if (Random.value < 0.5f) AddMote(rig.PointWorld(seats[Random.Range(0, seats.Length)]), Random.insideUnitCircle * 2f + Vector2.up, new Color(0.75f, 0.9f, 1f, 1f), 0.6f, 0.05f, true);
                yield return null;
            }
            rig.Tint = Color.white;
            jawHold = -1f;
            Ctx.Run(FadeCracks(6f));
        }

        private static readonly Color CrackCore = new Color(0.92f, 1f, 1f, 1f), CrackMid = new Color(0.45f, 0.8f, 1f, 1f), CrackEdge = new Color(0.07f, 0.03f, 0.14f, 0.95f);

        /// <summary>One crack segment (dark rim + bright core) from a to b, parented to `anchor` (follows the bone), not part of fieldObjs.</summary>
        private void CrackSeg(Vector2 a, Vector2 b, float th, Transform anchor)
        {
            var g = new GameObject("vessel crack");
            var sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = BandSprite(CrackCore, CrackMid, CrackEdge);
            FxLighting.MakeUnlit(sr);
            sr.sortingLayerID = layer; sr.sortingOrder = order + 9;
            PlaceBand(sr, a, b, th, Color.white);
            if (anchor != null) g.transform.SetParent(anchor, true);
            crackFx.Add(g);
        }

        /// <summary>Small cracks radiating around p (a sword hit on the hand).</summary>
        private void AddBodyCrack(Vector2 p, float len, int segs, Transform anchor = null)
        {
            for (int branch = 0; branch < 2; branch++)
            {
                Vector2 a = p;
                float dir = Random.Range(0f, 360f) + branch * 180f;
                for (int s = 0; s < segs; s++)
                {
                    dir += Random.Range(-35f, 35f);
                    Vector2 b = a + Rot(Vector2.right, dir) * (len / segs) * Random.Range(0.7f, 1.2f);
                    CrackSeg(a, b, 0.06f * (1f - s * 0.2f), anchor);
                    a = b;
                }
            }
        }

        /// <summary>A crack CRAWLS along the path (p, anchor) at `speed` u/s — each 0.12 u step zig-zags slightly, thinning th0 -> th1,
        /// 22 % of steps fork a short branch; the crack tip has a light + falling rock dust + a cracking sound.</summary>
        private IEnumerator CrawlCrack(List<(Vector2 p, Transform a)> path, float speed, float th0, float th1)
        {
            if (path == null || path.Count < 2) yield break;
            float total = 0f;
            for (int i = 1; i < path.Count; i++) total += Vector2.Distance(path[i - 1].p, path[i].p);
            var head = BossJuice.Light(path[0].p, CrackMid, 1.2f, 1.8f, 0f);
            float done = 0f, nextSfx = 0f;
            Vector2 cur = path[0].p;
            for (int i = 1; i < path.Count; i++)
            {
                Vector2 a = path[i - 1].p, b = path[i].p;
                var anchor = path[i - 1].a != null ? path[i - 1].a : path[i].a;
                float L = Vector2.Distance(a, b);
                int steps = Mathf.Max(1, Mathf.CeilToInt(L / 0.12f));
                Vector2 dir = L > 0.001f ? (b - a) / L : Vector2.up, nrm = new Vector2(-dir.y, dir.x);
                for (int k = 1; k <= steps; k++)
                {
                    Vector2 nxt = Vector2.Lerp(a, b, k / (float)steps);
                    if (k < steps) nxt += nrm * Random.Range(-0.05f, 0.05f);
                    float prog = total > 0f ? done / total : 0f;
                    float th = Mathf.Lerp(th0, th1, prog);
                    CrackSeg(cur, nxt, th, anchor);
                    if (Random.value < 0.22f)
                    {
                        Vector2 bp = cur; float bd = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + (Random.value < 0.5f ? -1f : 1f) * Random.Range(35f, 70f);
                        for (int q = 0; q < 2; q++) { Vector2 bn = bp + Rot(Vector2.right, bd + Random.Range(-20f, 20f)) * Random.Range(0.07f, 0.12f); CrackSeg(bp, bn, th * 0.55f, anchor); bp = bn; }
                    }
                    if (head != null) head.transform.position = nxt;
                    if (Random.value < 0.5f) AddMote(nxt, new Vector2(Random.Range(-0.5f, 0.5f), Random.Range(-1.5f, -0.4f)), new Color(0.35f, 0.32f, 0.4f, 1f), 0.6f, 0.04f);
                    if (Random.value < 0.35f) AddMote(nxt, Random.insideUnitCircle * 1.2f, CrackCore, 0.3f, 0.03f, true);
                    if (Time.time >= nextSfx) { nextSfx = Time.time + 0.18f; Sfx.Play("glass_break", nxt, 0.25f); }
                    done += Vector2.Distance(cur, nxt);
                    cur = nxt;
                    yield return new WaitForSeconds(0.12f / Mathf.Max(0.2f, speed));
                }
            }
            if (head != null)
            {
                var hl = BossJuice.Light(cur, CrackMid, 1.4f, 1.6f, 0f);
                if (hl != null) crackFx.Add(hl.gameObject);
                Object.Destroy(head.gameObject);
            }
        }

        private IEnumerator FadeCracks(float seconds)
        {
            yield return new WaitForSeconds(1.5f);
            var list = new List<GameObject>(crackFx);
            crackFx.Clear();
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float a = 1f - t / seconds;
                foreach (var g in list)
                {
                    if (g == null) continue;
                    var sr = g.GetComponent<SpriteRenderer>();
                    if (sr != null) sr.color = new Color(1f, 1f, 1f, a);
                    var l = g.GetComponent<UnityEngine.Rendering.Universal.Light2D>();
                    if (l != null) l.intensity = 1.6f * a;
                }
                yield return null;
            }
            foreach (var g in list) if (g != null) Object.Destroy(g);
        }

        // ------------------------------------------------------------------ end-of-chapter screen (MAP 5 COMING SOON)
        private bool comingSoonShown;

        private void ShowComingSoon()
        {
            if (comingSoonShown || ToBeContinuedScreen.Active) return;
            comingSoonShown = true;
            BeginScene();
            ToBeContinuedScreen.Show(() => { endCam = false; busy = false; EndCutscene(); });
        }

        /// <summary>Re-entering the domain after the ending: show the end-of-chapter screen directly.</summary>
        private IEnumerator ComingSoonAgain()
        {
            yield return new WaitForSeconds(1f);
            ShowComingSoon();
        }

        /// <summary>Close the portal after Absalom rushes in. Currently unused.</summary>
        private IEnumerator ClosePortal(Vector2 gate)
        {
            Sfx.Play("teleport_dark", gate, 0.7f);
            Vector3 s0 = portalFx != null ? portalFx.transform.localScale : Vector3.one;
            for (float t = 0f; t < 0.9f; t += Time.deltaTime)
            {
                float u = t / 0.9f, k = 1f - u * u;
                if (portalFx != null) portalFx.transform.localScale = new Vector3(s0.x * k, s0.y * k, 1f);
                if (portalLight != null) portalLight.intensity = 1.4f * k;
                yield return null;
            }
            if (portalFx != null) portalFx.Stop();
            if (portalLight != null) Object.Destroy(portalLight.gameObject);
            portalOpen = false;
            BossJuice.Flash(gate, VesselViolet, 2f, 2f, 0.3f);
        }
    }
}
