using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// SKILLS of SpecimenZeroSkills (one coroutine per skill). Settings / skill choice: SpecimenZeroSkills.cs.
    /// </summary>
    public partial class SpecimenZeroSkills
    {
        public IEnumerator Act(EnemyAI owner)
        {
            ai = owner;
            ai.Desired = Vector2.zero;
            if (!engaged)
            {
                engaged = true;
                var dir = MapEncounterDirector.Instance;
                hasRoom = dir != null && dir.TryGetZoneAt(transform.position, out _, out room);
                LoadRoots();
                LoadHand();
                ai.PlayState("Awake");
                // no extra hint - visuals / animation / boss title already tell the player
                yield return ai.Flash(1.4f, tint);
                nextRoots = Time.time + 0.6f;
                nextLash = Time.time + 4f;
                nextGaze = Time.time + 7f;
                nextWail = Time.time + 11f;
                nextHand = Time.time + 5.5f;
                nextBed = Time.time + 3f;
            }
            if (ai.Player == null) yield break;
            // chained: only attacks while the player is inside the Containment Vault
            if (((Vector2)ai.Player.position - Base).magnitude > leash) { yield return ai.WaitFor(0.5f); yield break; }

            if (P2 && !chainsBroken) { chainsBroken = true; yield return ChainsBreak(); }
            else if (closeT >= bedHugSeconds && Time.time >= nextBed) yield return Bed();
            else
            {
                string pick = PickAttack();
                if (pick == null) { yield return ai.WaitFor(0.3f); yield break; }
                switch (pick)
                {
                    case "wail": yield return Wail(); break;
                    case "watch": yield return Watchers(); break;
                    case "gaze": yield return Gaze(); break;
                    case "hand": yield return Hand(); break;
                    case "lash": yield return Lash(); break;
                    default: yield return Roots(); break;
                }
                AfterAttack(pick);
            }
            yield return ai.WaitFor(P2 ? 0.45f : 0.65f);
        }

        // ------------------------------------------------------------------ 1. Grasping Roots
        private IEnumerator Roots()
        {
            SfxHooks.BossMove(this, "Roots"); // sound
            nextRoots = Time.time + 3.4f * Pace;
            if (rootSets.Count == 0) yield break;
            Vector2 target = ai.Player.position;
            Vector2 start = Base + Vector2.up * 0.15f;
            Vector2 main = target - start;
            float len = Mathf.Clamp(main.magnitude + 0.6f, 1.5f, 6f);
            var dirs = new List<Vector2> { main.normalized };
            if (P2) { dirs.Add(EtherVfx.Rotate(main.normalized, 28f)); dirs.Add(EtherVfx.Rotate(main.normalized, -28f)); }

            // warning crack
            var cracks = new List<SpriteRenderer>();
            foreach (var d in dirs) cracks.Add(Line(start, start + d * len, 0.05f, new Color(0.55f, 0.25f, 0.75f, 0f), Order - 3));
            ai.PlayState("Awake");
            for (float t = 0f; t < 0.7f; t += Time.deltaTime)
            {
                foreach (var c in cracks) if (c != null) c.color = new Color(0.55f, 0.25f, 0.75f, 0.25f + 0.55f * (t / 0.7f) * (0.7f + 0.3f * Mathf.Sin(t * 30f)));
                yield return null;
            }
            foreach (var c in cracks) if (c != null) Destroy(c.gameObject);
            // roots burst one after another along the crack
            const float step = 0.38f;
            int n = Mathf.CeilToInt(len / step);
            for (int i = 1; i <= n && !ai.Health.IsDead; i++)
            {
                foreach (var d in dirs)
                {
                    Vector2 p = start + d * (i * step);
                    if (hasRoom && !room.Contains(p)) continue;
                    var fr = rootSets[Random.Range(0, rootSets.Count)];
                    bool fallback = fr == tentacleFrames;
                    var g = GroundStrike.Spawn(p, 0.12f, 0.24f, ai.damage * 0.7f, fr, Mathf.Min(2, fr.Length - 1), 16f, Order, tint);
                    g.impactSfx = "root_spike"; // sound
                    g.bodyScale = fallback ? 0.9f : Random.Range(0.85f, 1.05f);
                    g.bodyColor = fallback ? rootTint : Color.white;
                    g.onHit = h => { var st = h != null ? h.GetComponent<PlayerStatus>() : null; if (st != null) st.Slow(0.5f, 1.4f, "Roots grip your legs!"); };
                }
                if (i % 3 == 0) CameraFollow.KickMain(Random.insideUnitCircle, 0.015f, 0.08f);
                yield return new WaitForSeconds(0.05f);
            }
            yield return ai.WaitFor(0.4f);
        }

        // ------------------------------------------------------------------ 2. Tentacle Lash
        private IEnumerator Lash()
        {
            SfxHooks.BossMove(this, "Lash"); // sound
            nextLash = Time.time + 6.5f * Pace;
            if (tentacleFrames == null || tentacleFrames.Length == 0 || !hasRoom) yield break;
            float y = Mathf.Clamp(ai.Player.position.y, room.yMin + 0.3f, room.yMax - 0.3f);
            bool fromLeft = ai.Player.position.x > room.center.x;          // sweep from the side far from Ren toward Ren
            float x0 = fromLeft ? room.xMin + 0.2f : room.xMax - 0.2f, x1 = fromLeft ? room.xMax - 0.2f : room.xMin + 0.2f;
            var lane = Bar(new Vector2((x0 + x1) * 0.5f, y), new Vector2(Mathf.Abs(x1 - x0), 0.5f), new Color(0.7f, 0.3f, 1f, 0f), Order - 3);
            // no extra hint - visuals / animation / boss title already tell the player
            for (float t = 0f; t < 0.9f; t += Time.deltaTime)
            {
                lane.color = new Color(0.7f, 0.3f, 1f, 0.12f + 0.18f * Mathf.Abs(Mathf.Sin(t * 9f)));
                yield return null;
            }
            Destroy(lane.gameObject);
            int n = Mathf.CeilToInt(Mathf.Abs(x1 - x0) / 0.42f);
            for (int i = 0; i <= n && !ai.Health.IsDead; i++)
            {
                Vector2 p = new Vector2(Mathf.Lerp(x0, x1, i / (float)n), y + (i % 2 == 0 ? 0.08f : -0.08f));
                var g = GroundStrike.Spawn(p, 0.05f, 0.3f, ai.damage * 0.9f, tentacleFrames, Mathf.Min(2, tentacleFrames.Length - 1), 18f, Order, tint);
                g.impactSfx = "flesh_slam"; // sound
                g.bodyScale = 1.6f; g.bodyColor = tint; g.knockback = 3f;
                yield return new WaitForSeconds(P2 ? 0.035f : 0.05f);
            }
            yield return ai.WaitFor(0.5f);
        }

        // ------------------------------------------------------------------ 3. Vessel's Gaze
        private IEnumerator Gaze()
        {
            SfxHooks.BossMove(this, "Gaze"); // sound
            nextGaze = Time.time + 9f * Pace;
            Vector2 eye = Eye;
            var glowGo = new GameObject("SZ Eye Glow"); fx.Add(glowGo);
            glowGo.transform.position = eye;
            var glow = glowGo.AddComponent<Light2D>();
            glow.lightType = Light2D.LightType.Point; glow.color = tint; glow.pointLightOuterRadius = 2.2f; glow.pointLightInnerRadius = 0.1f;
            var aim = Line(eye, eye, 0.025f, new Color(tint.r, tint.g, tint.b, 0.35f), Order + 2);
            // no extra hint - visuals / animation / boss title already tell the player
            // charge: light rises, aim line follows Ren
            Vector2 toP = Vector2.down;
            for (float t = 0f; t < 1.1f && !ai.Health.IsDead; t += Time.deltaTime)
            {
                if (ai.Player != null) toP = ((Vector2)ai.Player.position + Vector2.up * 0.25f - eye).normalized;
                Place(aim, eye, toP, 9f, 0.025f);
                glow.intensity = 3f * (t / 1.1f);
                yield return null;
            }
            Destroy(aim.gameObject);
            // sweep: a 70° arc (phase 2: 100°) through where Ren stood at lock time
            float arc = P2 ? 100f : 70f, dur = P2 ? 1.3f : 1.6f, sideSign = Random.value < 0.5f ? 1f : -1f;
            var beam = EtherVfx.Spawn("SZ Gaze", EtherVfx.Beam, eye, tint, ai.Body != null ? ai.Body.sortingLayerID : 0, Order + 3);
            fx.Add(beam.gameObject);
            float nextTick = 0f;
            for (float t = 0f; t < dur && !ai.Health.IsDead; t += Time.deltaTime)
            {
                float a = Mathf.Lerp(-arc * 0.5f, arc * 0.5f, t / dur) * sideSign;
                Vector2 d = EtherVfx.Rotate(toP, a);
                float reach = 9f;
                beam.transform.position = eye;
                beam.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                beam.transform.localScale = new Vector3(reach / EtherVfx.BeamWidthUnits, (0.22f + 0.04f * Mathf.Sin(t * 40f)) / EtherVfx.BeamHeightUnits, 1f);
                glow.intensity = 3f + Mathf.Sin(t * 30f);
                SoulRequiem.Core.Sfx.Loop(beam, "eye_beam", transform); // sound: the sweeping beam hums while firing
                if (t >= nextTick && ai.Player != null)
                {
                    Vector2 pp = (Vector2)ai.Player.position + Vector2.up * 0.25f;
                    if (DistToSeg(pp, eye, eye + d * reach) < 0.2f) { nextTick = t + 0.25f; ai.Hurt(ai.damage * 0.45f, eye, 1.2f); }
                }
                if (Random.value < 0.25f) CameraFollow.KickMain(Random.insideUnitCircle, 0.012f, 0.06f);
                yield return null;
            }
            Destroy(beam.gameObject);
            Destroy(glowGo);
            yield return ai.WaitFor(0.5f);
        }

        private IEnumerator Wail()
        {
            SfxHooks.BossMove(this, "Wail"); // sound
            nextWail = Time.time + 14f * Pace;
            var wards = SpritePropMechanism.Group(wardGroup);
            var ward = PickWard(wards, null);
            if (ward == null) yield break;                       // map without pillars: skip this skill (no way to take cover)
            wailing = true;
            try
            {
                Vector2 c = Base + Vector2.up * 0.1f;
                float cover = CoverRadius(c), speed = 3.4f;
                float warn = P2 ? 2.2f : 2.6f;
                Hints.Show("Specimen Zero draws a rattling breath - its rune flares on a ward-stone. Get inside that ward's circle!", warn + 0.8f);
                for (float t = 0f; t < warn && !ai.Health.IsDead; t += Time.deltaTime)
                {
                    float k = t / warn;
                    GlowWard(ward, k, t);
                    ShowRune(ward, c, k, t);
                    ShowTrail(c, ward.DecalAt, t);
                    ShowFootRing(ward, k, t);
                    if (t > warn - 0.5f && Random.value < 0.35f) CameraFollow.KickMain(Random.insideUnitCircle, 0.04f, 0.1f);
                    yield return null;
                }
                if (ai.Health.IsDead) yield break;
                yield return ai.Flash(0.2f, new Color(1f, 0.55f, 0.85f));
                SoulRequiem.Core.Sfx.Play("sz_wail", transform.position); // sound
                int rings = 3;                                        // phase 2: still 3 rings but the pillar switches midway (4 rings made the attack too long)
                for (int i = 0; i < rings && !ai.Health.IsDead; i++)
                {
                    if (P2 && i == 1)
                    {
                        var next = PickWard(wards, ward);
                        if (next != null)
                        {
                            Hints.Show("Another ward wakes - move!", 1.6f);
                            StartCoroutine(DimWard(ward, 0.9f));
                            ward = next;
                            for (float t = 0f; t < 1.4f && !ai.Health.IsDead; t += Time.deltaTime)
                            {
                                GlowWard(ward, t / 1.4f, t); ShowRune(ward, c, 1f, t); ShowTrail(c, ward.DecalAt, t); ShowFootRing(ward, 1f, t);
                                yield return null;
                            }
                        }
                    }
                    var w = Shockwave.Spawn(c, cover, cover / speed, ai.damage * 0.6f, Order - 1, new Color(tint.r, tint.g, tint.b, 0.85f));
                    w.knockback = 2f;
                    var wd = ward;
                    w.shelter = p => InSigil(wd, p);
                    w.onSheltered = () => { if (this != null && isActiveAndEnabled) StartCoroutine(Sheltered(wd)); };
                    if (i == 0)
                        w.onHit = h => { var st = h != null ? h.GetComponent<PlayerStatus>() : null; if (st != null) st.Silence(P2 ? 3.5f : 2.5f, "The wail drowns out your mantras!"); };
                    float hold = P2 ? 0.75f : 0.6f;
                    for (float t = 0f; t < hold && !ai.Health.IsDead; t += Time.deltaTime)
                    {
                        GlowWard(ward, 1f, Time.time); ShowRune(ward, c, 1f, Time.time); ShowFootRing(ward, 1f, Time.time);
                        yield return null;
                    }
                }
                for (float t = 0f; t < cover / speed && !ai.Health.IsDead; t += Time.deltaTime)
                {
                    GlowWard(ward, 1f, Time.time); ShowFootRing(ward, 1f, Time.time);
                    yield return null;
                }
                ClearWailFx();
                StartCoroutine(DimWard(ward, 0.6f));
                yield return Spent();
            }
            finally
            {
                wailing = false;
                ClearWailFx();
            }
        }

        private IEnumerator DimWard(SpritePropMechanism ward, float dur)
        {
            if (ward == null || ward.Body == null) yield break;
            float i0 = ward.Glow.intensity, a0 = ward.Decal != null ? ward.Decal.color.a : 0f;
            for (float t = 0f; t < dur && ward.Body != null; t += Time.deltaTime)
            {
                float k = t / dur;
                ward.Glow.intensity = Mathf.Lerp(i0, 0f, k);
                if (ward.Decal != null) ward.Decal.color = new Color(1f, 1f, 1f, Mathf.Lerp(a0, ward.DecalAlpha, k));
                yield return null;
            }
            ward.SetLit(false);
            litWards.Remove(ward);
        }

        /// <summary>A wave just passed while Ren stood in the seal ring: the ring flashes, the light jumps.</summary>
        private IEnumerator Sheltered(SpritePropMechanism ward)
        {
            if (ward == null || ward.Body == null) yield break;
            if (!shelterTold) { shelterTold = true; Hints.Show("The ward holds - the wail breaks around the stone.", 2.2f); }
            CameraFollow.KickMain(Vector2.up, 0.015f, 0.08f);
            Vector3 s0 = ward.Decal != null ? ward.Decal.transform.localScale : Vector3.one;
            for (float t = 0f; t < 0.3f && ward.Body != null; t += Time.deltaTime)
            {
                float k = 1f - t / 0.3f;
                ward.Glow.intensity = 2.2f + 2.5f * k;
                if (ward.Decal != null) { ward.Decal.color = new Color(1f, 1f, 1f, 1f); ward.Decal.transform.localScale = s0 * (1f + 0.12f * k); }
                yield return null;
            }
            if (ward.Decal != null) ward.Decal.transform.localScale = s0;
        }

        // ------------------------------------------------------------------ 5. Hand of the Vessel
        // a shadow spreads under Ren and follows him (0.9 s), locks (warning ring shrinks), then a corpse hand bursts up to grab.
        // Phase 2: 3 hands in a row, each aimed where Ren stands at that moment - keep running or roll at the right time.
        private IEnumerator Hand()
        {
            SfxHooks.BossMove(this, "Hand"); // sound
            nextHand = Time.time + 8f * Pace;
            if (handFrames == null || handFrames.Length == 0) yield break;
            int n = P2 ? 3 : 1;
            Hints.Show("The floor beneath you darkens - something reaches up!", 1.8f);
            ai.PlayState("Awake");
            for (int k = 0; k < n && !ai.Health.IsDead && ai.Player != null; k++)
            {
                Vector2 mark = ai.Player.position;
                var shadow = EtherVfx.Spawn("SZ Hand Shadow", Blob, mark, new Color(0.07f, 0.02f, 0.1f, 0f), ai.Body != null ? ai.Body.sortingLayerID : 0, Order - 3);
                fx.Add(shadow.gameObject);
                float track = k == 0 ? 0.9f : 0.45f;
                for (float t = 0f; t < track && ai.Player != null && !ai.Health.IsDead; t += Time.deltaTime)
                {
                    mark = Vector2.Lerp(mark, ai.Player.position, Time.deltaTime * 7f);
                    float g = t / track;
                    shadow.transform.position = mark;
                    shadow.transform.localScale = new Vector3(1f, 0.6f, 1f) * Mathf.Lerp(0.35f, 0.95f, g);
                    shadow.color = new Color(0.07f, 0.02f, 0.1f, 0.75f * g);
                    yield return null;
                }
                if (shadow != null) Destroy(shadow.gameObject);
                var strike = GroundStrike.Spawn(mark, 0.45f, 0.42f, ai.damage * 1.1f, handFrames, 3, 14f, Order, new Color(0.75f, 0.3f, 1f));
                strike.impactSfx = "sz_hand"; // sound
                strike.knockback = 3.5f;
                strike.onHit = h => CameraFollow.KickMain(Vector2.up, 0.05f, 0.15f);
                yield return new WaitForSeconds(P2 ? 0.35f : 0.2f);
            }
            yield return ai.WaitFor(0.7f);
        }

        // ------------------------------------------------------------------ 6. Spent - exhausted after Requiem Wail
        // Reward for hiding at the right pillar: the boss hangs its face (sleep frame "Idle"), dims, takes x1.5 damage for 2.5 s.
        private IEnumerator Spent()
        {
            if (ai.Health.IsDead) yield break;
            Hints.Show("The wail leaves it spent - Specimen Zero slumps. Strike now!", 2.4f);
            CameraFollow.KickMain(Vector2.down, 0.05f, 0.25f);
            ai.PlayState("Idle");
            ai.Health.DamageTakenMul = spentDamageMul;
            try { yield return ai.Flash(spentSeconds, new Color(1f, 0.93f, 0.68f)); }
            finally { if (ai != null && ai.Health != null) ai.Health.DamageTakenMul = 1f; }
            if (!ai.Health.IsDead) ai.PlayState("Awake");
            yield return ai.WaitFor(0.3f);
        }

        // ------------------------------------------------------------------ 7. Writhing Bed - anti-hugging
        // A spreading purple disc + glowing rim shrinks around the boss feet (0.8 s) -> 3 root rings burst from the inside out and launch (roll to dodge).
        private IEnumerator Bed()
        {
            nextBed = Time.time + 7f * Pace;
            closeT = 0f;
            if (rootSets.Count == 0) yield break;
            Vector2 c = Base + Vector2.up * 0.1f;
            float r = bedRadius + 0.15f;
            int layer = ai.Body != null ? ai.Body.sortingLayerID : 0;
            Hints.Show("The roots around its base writhe - back away!", 1.6f);
            ai.PlayState("Awake");
            var disk = EtherVfx.Spawn("SZ Bed Warn", Blob, c, new Color(0.45f, 0.15f, 0.6f, 0f), layer, Order - 4);
            fx.Add(disk.gameObject);
            disk.transform.localScale = new Vector3(r * 2f, r * 2f * 0.6f, 1f);
            var edge = EtherVfx.Spawn("SZ Bed Edge", EtherVfx.Ring, c, new Color(0.8f, 0.4f, 1f, 0f), layer, Order - 3);
            fx.Add(edge.gameObject);
            float ringK = r * 2f / Mathf.Max(0.01f, EtherVfx.Ring.bounds.size.x);
            const float warn = 0.8f;
            for (float t = 0f; t < warn && !ai.Health.IsDead; t += Time.deltaTime)
            {
                float k = t / warn;
                disk.color = new Color(0.45f, 0.15f, 0.6f, (0.15f + 0.4f * k) * (0.75f + 0.25f * Mathf.Sin(t * 26f)));
                edge.color = new Color(0.8f, 0.4f, 1f, 0.4f + 0.6f * k);
                float s = ringK * Mathf.Lerp(1.15f, 1f, k);
                edge.transform.localScale = new Vector3(s, s * 0.6f, 1f);
                if (t > warn - 0.3f && Random.value < 0.4f) CameraFollow.KickMain(Random.insideUnitCircle, 0.025f, 0.08f);
                yield return null;
            }
            Destroy(disk.gameObject);
            Destroy(edge.gameObject);
            if (ai.Health.IsDead) yield break;
            // 3 root rings in a row from the inside out: anyone in the disc gets swept by one ring
            float[] rings = { 0.3f, 0.65f, 1f };
            int[] counts = { 4, 7, 11 };
            for (int i = 0; i < rings.Length && !ai.Health.IsDead; i++)
            {
                float rr = r * rings[i], a0 = Random.value * 360f;
                for (int j = 0; j < counts[i]; j++)
                {
                    float a = (a0 + 360f * j / counts[i]) * Mathf.Deg2Rad;
                    Vector2 p = c + new Vector2(Mathf.Cos(a) * rr, Mathf.Sin(a) * rr * 0.6f);
                    var fr = rootSets[Random.Range(0, rootSets.Count)];
                    var g = GroundStrike.Spawn(p, 0.02f, 0.3f, ai.damage * 0.8f, fr, Mathf.Min(2, fr.Length - 1), 18f, Order, tint);
                    g.impactSfx = "root_spike"; // sound
                    g.bodyScale = Random.Range(0.9f, 1.1f);
                    g.bodyColor = fr == tentacleFrames ? rootTint : Color.white;
                    g.knockback = 4f;
                }
                CameraFollow.KickMain(Vector2.up, 0.04f, 0.12f);
                yield return new WaitForSeconds(0.09f);
            }
            yield return ai.WaitFor(0.5f);
        }

        private IEnumerator ChainsBreak()
        {
            SfxHooks.BossMove(this, "ChainsBreak"); // sound
            // no extra hint - visuals / animation / boss title already tell the player
            for (float t = 0f; t < 1.2f; t += 0.07f) { CameraFollow.KickMain(Random.insideUnitCircle, 0.09f, 0.12f); yield return new WaitForSeconds(0.07f); }
            yield return ai.Flash(0.6f, tint);
            LoadEyeFx();
            watchEyes.Clear();
            var eyes = SpritePropMechanism.Group(wallEyeGroup);
            if (eyes.Count > 0)
            {
                // no extra hint - visuals / animation / boss title already tell the player
                eyes.Sort((x, y) => x.Foot.x.CompareTo(y.Foot.x));
                for (int i = 0; i < eyes.Count; i++) StartCoroutine(OpenEye(eyes[i], eyes[i].Foot, 0.4f + i * 0.35f));
            }
            else if (hasRoom)
            {
                var spots = new[]
                {
                    new Vector2(room.xMin + 0.25f, room.yMax - 0.35f), new Vector2(room.xMax - 0.25f, room.yMax - 0.35f),
                    new Vector2(room.xMin + 0.25f, room.center.y), new Vector2(room.xMax - 0.25f, room.center.y),
                };
                for (int i = 0; i < spots.Length; i++) StartCoroutine(OpenEye(null, spots[i], i * 0.35f));
            }
            nextWatch = Time.time + 3.5f;
        }

        private IEnumerator OpenEye(SpritePropMechanism prop, Vector2 at, float delay)
        {
            SfxHooks.BossMove(this, "OpenEye"); // sound
            yield return new WaitForSeconds(delay);
            if (ai == null || ai.Health.IsDead) yield break;
            var e = new WatchEye { at = at };
            if (prop != null && prop.Body != null)
            {
                prop.SetLit(true);
                if (!litWards.Contains(prop)) litWards.Add(prop);
                e.light = prop.Glow;
                e.light.color = new Color(1f, 0.22f, 0.3f);
                e.idle = 0.8f;
                for (float t = 0f; t < 0.5f; t += Time.deltaTime) { e.light.intensity = 3f * (1f - t / 0.5f) + e.idle; yield return null; }
            }
            else
            {
                var own = new GameObject("SZ Wall Eye"); fx.Add(own);
                own.transform.position = at;
                e.light = own.AddComponent<Light2D>();
                e.light.lightType = Light2D.LightType.Point; e.light.color = new Color(1f, 0.25f, 0.45f);
                e.light.pointLightOuterRadius = 0.7f; e.light.pointLightInnerRadius = 0.02f; e.light.intensity = 0f;
                var e1 = Bar(at + new Vector2(-0.05f, 0f), new Vector2(0.04f, 0.03f), new Color(1f, 0.3f, 0.5f, 0f), Order + 4, own.transform);
                var e2 = Bar(at + new Vector2(0.05f, 0f), new Vector2(0.04f, 0.03f), new Color(1f, 0.3f, 0.5f, 0f), Order + 4, own.transform);
                e.idle = 1.2f;
                for (float t = 0f; t < 1f; t += Time.deltaTime) { e.light.intensity = e.idle * t; e1.color = e2.color = new Color(1f, 0.3f, 0.5f, t); yield return null; }
            }
            e.light.intensity = e.idle;
            watchEyes.Add(e);
        }

        private IEnumerator Watchers()
        {
            nextWatch = Time.time + 8f;
            if (ai.Player == null) yield break;
            LoadEyeFx();
            var order = new List<WatchEye>(watchEyes);
            Vector2 p = ai.Player.position;
            order.Sort((x, y) => Mathf.Abs(y.at.x - p.x).CompareTo(Mathf.Abs(x.at.x - p.x)));
            Hints.Show("The wall-eyes fix on you, one after another - keep moving!", 2.2f);
            int running = 0;
            for (int i = 0; i < order.Count && !ai.Health.IsDead; i++)
            {
                running++;
                StartCoroutine(WatcherShot(order[i], i == 0 ? 0.9f : 0.6f, () => running--));
                yield return new WaitForSeconds(WatchGap);
            }
            for (float t = 0f; running > 0 && t < 4f && !ai.Health.IsDead; t += Time.deltaTime) yield return null;
            yield return ai.WaitFor(0.7f);                                             // rest beat after the volley
        }

        private IEnumerator WatcherShot(WatchEye e, float track, System.Action done)
        {
            SfxHooks.BossMove(this, "WatcherShot"); // sound
            var temp = new List<SpriteRenderer>();
            try
            {
                if (ai.Player == null) yield break;
                int layer = ai.Body != null ? ai.Body.sortingLayerID : 0;
                Vector2 at = e.at, mark = ai.Player.position;
                var crimson = new Color(1f, 0.16f, 0.24f);

                // target mark under Ren's feet (PixelLab image; missing -> deep red ring in code)
                var ret = EtherVfx.Spawn("SZ Eye Reticle", eyeReticle != null ? eyeReticle : EtherVfx.Ring, mark,
                                         eyeReticle != null ? Color.white : crimson, layer, Order - 3);
                temp.Add(ret);
                float retK = 0.85f / Mathf.Max(0.01f, ret.sprite.bounds.size.x);
                var aim = Line(at, mark, 0.02f, new Color(crimson.r, crimson.g, crimson.b, 0.3f), Order + 3);
                temp.Add(aim);
                SpriteRenderer charge = null;
                float chargeK = 1f;
                if (eyeCharge != null)
                {
                    charge = EtherVfx.Spawn("SZ Eye Charge", eyeCharge[0], at, Color.white, layer, Order + 6);
                    temp.Add(charge);
                    chargeK = 0.75f / Mathf.Max(0.01f, eyeCharge[0].bounds.size.x);
                    charge.transform.localScale = Vector3.one * chargeK;
                }
                float total = track + WatchLock;
                for (float t = 0f; t < total && ai.Player != null && !ai.Health.IsDead; t += Time.deltaTime)
                {
                    bool locked = t >= track;
                    if (!locked) mark = Vector2.Lerp(mark, ai.Player.position, Time.deltaTime * 8f);
                    float s = retK * (locked ? 1f + 0.1f * Mathf.Sin(t * 45f) : Mathf.Lerp(1.4f, 1f, t / track));
                    ret.transform.position = mark;
                    ret.transform.localScale = new Vector3(s, s * 0.62f, 1f);
                    float blink = locked ? 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(t * 28f)) : 0.75f;
                    ret.color = eyeReticle != null ? new Color(1f, 1f, 1f, blink) : new Color(crimson.r, crimson.g, crimson.b, blink);
                    Vector2 d = mark - at;
                    Place(aim, at, d.normalized, d.magnitude, locked ? 0.035f : 0.018f);
                    aim.color = new Color(crimson.r, crimson.g, crimson.b, locked ? 0.35f + 0.55f * Mathf.Abs(Mathf.Sin(t * 30f)) : 0.28f);
                    if (charge != null) charge.sprite = eyeCharge[Mathf.Min(eyeCharge.Length - 1, (int)(t / total * eyeCharge.Length))];
                    if (e.light != null) e.light.intensity = e.idle + 3f * t / total;
                    yield return null;
                }
                foreach (var r in temp) if (r != null) Destroy(r.gameObject);
                temp.Clear();
                if (ai.Player == null || ai.Health.IsDead) yield break;

                // strike: dark outline (readable over red) + beam body + white core
                Vector2 dir = mark - at; float len = dir.magnitude;
                if (((Vector2)ai.Player.position - mark).magnitude < WatchHitRadius) ai.Hurt(ai.damage * 0.55f, mark, 1f);
                CameraFollow.KickMain(dir.normalized, 0.04f, 0.12f);
                StartCoroutine(Scorch(mark, layer));
                var outline = Line(at, mark, 0.3f, new Color(0.16f, 0f, 0.04f, 0.85f), Order + 3);
                temp.Add(outline);
                SpriteRenderer beam;
                float bw, bh;
                if (eyeBeam != null)
                {
                    beam = EtherVfx.Spawn("SZ Eye Beam", eyeBeam[0], at, Color.white, layer, Order + 4);
                    beam.drawMode = SpriteDrawMode.Tiled;                          // tile the PixelLab beam segment along the length (meta: Full Rect)
                    beam.tileMode = SpriteTileMode.Continuous;
                    bw = eyeBeam[0].bounds.size.x; bh = eyeBeam[0].bounds.size.y;
                }
                else
                {
                    beam = EtherVfx.Spawn("SZ Eye Beam", EtherVfx.Beam, at, crimson, layer, Order + 4);
                    bw = EtherVfx.BeamWidthUnits; bh = EtherVfx.BeamHeightUnits;
                }
                temp.Add(beam);
                beam.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                var core = Line(at, mark, 0.06f, Color.white, Order + 5);
                temp.Add(core);
                const float dur = 0.4f;
                for (float t = 0f; t < dur; t += Time.deltaTime)
                {
                    float k = 1f - t / dur;
                    float thick = 0.12f + 0.2f * k;
                    if (eyeBeam != null)
                    {
                        beam.sprite = eyeBeam[(int)(t * 16f) % eyeBeam.Length];
                        beam.size = new Vector2(len, bh);
                        beam.transform.localScale = new Vector3(1f, thick / bh, 1f);
                    }
                    else beam.transform.localScale = new Vector3(len / bw, thick / bh, 1f);
                    beam.color = eyeBeam != null ? new Color(1f, 1f, 1f, Mathf.Clamp01(k * 1.4f)) : new Color(crimson.r, crimson.g, crimson.b, k);
                    Place(outline, at, dir.normalized, len, thick + 0.1f);
                    outline.color = new Color(0.16f, 0f, 0.04f, 0.85f * k);
                    Place(core, at, dir.normalized, len, 0.02f + 0.05f * k);
                    core.color = new Color(1f, 1f, 1f, k);
                    if (e.light != null) e.light.intensity = e.idle + 4f * k;
                    yield return null;
                }
                if (e.light != null) e.light.intensity = e.idle;
            }
            finally
            {
                foreach (var r in temp) if (r != null) Destroy(r.gameObject);
                done?.Invoke();
            }
        }

        /// <summary>Beam impact: burst (PixelLab eyefx_burst, missing -> growing ring) + scorch (eyefx_scorch, missing -> dark spot) fading over 1.6 s.</summary>
        private IEnumerator Scorch(Vector2 at, int layer)
        {
            SfxHooks.BossMove(this, "Scorch"); // sound
            var mark = EtherVfx.Spawn("SZ Eye Scorch", eyeScorch != null ? eyeScorch : Blob, at,
                                      eyeScorch != null ? new Color(1f, 1f, 1f, 0.9f) : new Color(0.25f, 0.02f, 0.05f, 0.6f), layer, Order - 4);
            fx.Add(mark.gameObject);
            float mk = 0.7f / Mathf.Max(0.01f, mark.sprite.bounds.size.x);
            mark.transform.localScale = new Vector3(mk, mk * 0.6f, 1f);
            Color m0 = mark.color;
            SpriteRenderer burst;
            if (eyeBurst != null)
            {
                burst = EtherVfx.Spawn("SZ Eye Burst", eyeBurst[0], at, Color.white, layer, Order + 5);
                float bk = 1.1f / Mathf.Max(0.01f, eyeBurst[0].bounds.size.x);
                burst.transform.localScale = Vector3.one * bk;
            }
            else burst = EtherVfx.Spawn("SZ Eye Burst", EtherVfx.Ring, at, new Color(1f, 0.4f, 0.35f, 1f), layer, Order - 2);
            fx.Add(burst.gameObject);
            const float burstT = 0.4f;
            for (float t = 0f; t < 1.6f; t += Time.deltaTime)
            {
                if (burst != null)
                {
                    float k = Mathf.Clamp01(t / burstT);
                    if (eyeBurst != null) burst.sprite = eyeBurst[Mathf.Min(eyeBurst.Length - 1, (int)(k * eyeBurst.Length))];
                    else
                    {
                        burst.transform.localScale = new Vector3(1f, 0.6f, 1f) * Mathf.Lerp(0.2f, 0.75f, k);
                        burst.color = new Color(1f, 0.4f, 0.35f, 1f - k);
                    }
                    if (k >= 1f) { Destroy(burst.gameObject); burst = null; }
                }
                if (mark != null) mark.color = new Color(m0.r, m0.g, m0.b, m0.a * (1f - t / 1.6f));
                yield return null;
            }
            if (burst != null) Destroy(burst.gameObject);
            if (mark != null) Destroy(mark.gameObject);
        }
    }
}
