using System.Collections;
using System.Collections.Generic;
using SuperTiled2Unity;
using UnityEngine;
using UnityEngine.InputSystem;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Boss title card (Enter the Gungeon / Hollow Knight style) when a boss appears.
    /// First meeting (flag "bossintro_&lt;name&gt;", kept for the whole playthrough): locks controls, the boss stands still, the camera pans to the boss,
    /// the boss roars (Windup animation + screen shake), the title card shows ~3 seconds, camera and control return, then the area hint shows.
    /// Meeting again (after falling): a short locked scene without dialogue / entrance (see below). Space / Enter: skip.
    /// The card is drawn by the HUD (RequiemHUD), which reads the static fields CardName / CardSub / CardStart / CardLength.
    /// Name comes from displayName: "Grok, the Greystone Chieftain" -> GROK + THE GREYSTONE CHIEFTAIN (split at "—" or ", ").
    /// </summary>
    public class BossIntro : MonoBehaviour
    {
        public static string CardName = "", CardSub = "";
        public static float CardStart = -99f, CardLength = 0f;
        /// <summary>Portrait (the boss's current sprite) + band colour of the Soul Knight style intro card.</summary>
        public static Sprite CardSprite;
        /// <summary>Name of a custom portrait image in Resources/UI/Requiem (property `portrait` on the boss object), empty = use the sprite.</summary>
        public static string CardPortrait = "";
        public static Color CardColor = new Color(0.47f, 0.36f, 0.16f);
        /// <summary>Title card currently showing (the HUD hides the boss HP bar meanwhile).</summary>
        public static bool CardActive => Time.time - CardStart < CardLength;

        private MapEncounterDirector dir;
        private bool skip;

        private void Awake()
        {
            dir = GetComponent<MapEncounterDirector>();
            if (dir != null) dir.BossSpawned += OnBossSpawned;
            CardStart = -99f; CardLength = 0f;
        }

        private void OnDestroy()
        {
            if (dir != null) dir.BossSpawned -= OnBossSpawned;
        }

        public static void SplitName(string full, out string name, out string sub)
        {
            full = full ?? "";
            int cut = full.IndexOf('—');
            int len = 1;
            if (cut < 0) { cut = full.IndexOf(", "); len = 2; }
            if (cut > 0) { name = full.Substring(0, cut).Trim(); sub = full.Substring(cut + len).Trim(); }
            else { name = full.Trim(); sub = ""; }
        }

        private void OnBossSpawned(EnemyAI boss, string zoneHint)
        {
            if (boss == null) return;
            if (dir != null && dir.BossPartner == boss) return;          // duo fight - the partner boss shares the main boss's intro
            StartCoroutine(Run(boss, zoneHint));
        }

        private IEnumerator Run(EnemyAI boss, string zoneHint)
        {
            SplitName(boss.displayName, out var name, out var sub);
            string flag = "bossintro_" + name.ToLowerInvariant().Replace(' ', '_');
            bool first = dir.Flags != null && !dir.Flags.HasAll(flag);
            var cam = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;

            // properties on the Tiled "boss" object (e.g. grok_fight): intro_line1..N (the boss speaks before roaring),
            // toss_minions (knocks away the minions standing nearby, stunned until the boss is at half HP), wake_line (line that wakes the minions)
            var props = BossProps(boss);
            // custom card text (card_name / card_sub), entrance (entrance = burst | leap | wake - see Entrance)
            string entrance = props != null ? props.GetString("entrance", "") : "";
            string eflag = props != null ? props.GetString("entrance_flag", "") : "";
            if (props != null)
            {
                string cn = props.GetString("card_name", ""), cs = props.GetString("card_sub", "");
                if (cn.Length > 0) name = cn;                      // bossintro_* flags still use the original name
                if (cs.Length > 0) sub = cs;
            }
            var hidden = new List<SpriteRenderer>();                // burst / leap: the boss stays hidden until it bursts out
            if (first && cam != null && dir.Player != null && (entrance == "burst" || entrance == "leap" || entrance == "mist" || entrance == "rise"))
                foreach (var r in boss.GetComponentsInChildren<SpriteRenderer>())
                    if (r.enabled) { r.enabled = false; hidden.Add(r); }
            var dead = new List<EnemyAI>();                         // minion stun stars from the previous attempt (removed when the arena resets)
            foreach (var kv in stars) if (kv.Key == null) { if (kv.Value != null) kv.Value.Stop(); dead.Add(kv.Key); }
            foreach (var k in dead) stars.Remove(k);
            boss.Held = true;                                       // hold from the first frame (minions spawn in the same wave, checked on the next frame)
            yield return null;
            // duo fight - the partner boss (same wave, next frame) stands still for the whole intro and is released together with the main boss
            var partner = dir.BossPartner != null && dir.BossPartner != boss ? dir.BossPartner : null;
            if (partner != null) partner.Held = true;
            var minions = new List<EnemyAI>();
            if (props != null && props.GetBool("toss_minions", false))
                foreach (var e in FindObjectsByType<EnemyAI>(FindObjectsSortMode.None))
                    if (e != null && e != boss && e.Health != null && !e.Health.IsDead &&
                        ((Vector2)(e.transform.position - boss.transform.position)).sqrMagnitude < 16f) { e.Held = true; minions.Add(e); }
            if (minions.Count > 0) HookWake(boss, minions, props);

            // MEETING AGAIN (scene already seen / fell and came back) is also a short locked scene: no dialogue / entrance,
            // but the wind-up, slam and minion toss + title card still play, player and enemies stand still, Space/Enter skips.
            // (Showing only the card let the boss and minions move at once and Grok's minions were already lying down without the toss.)
            bool replay = !first;
            if (cam == null || dir.Player == null)
            {
                if (eflag.Length > 0 && dir.Flags != null) dir.Flags.Set(eflag);
                foreach (var m in minions) StartCoroutine(Toss(boss, m, 0f));    // meeting again: minions already lying stunned
                SetPortrait(boss, props);
                ShowCard(name, sub, 2.2f);
                if (!string.IsNullOrEmpty(zoneHint)) Hints.Show(zoneHint, 4f);
                boss.Held = false;
                if (partner != null) partner.Held = false;
                yield break;
            }

            if (first) dir.Flags.Set(flag);
            else if (eflag.Length > 0 && dir.Flags != null) dir.Flags.Set(eflag);
            skip = false;
            var ph = dir.Player.GetComponent<Health>();            // some bosses' own skill sets (Update / coroutines) keep running: do not let the player be hit while locked
            if (ph != null) ph.Invulnerable = true;
            var pc = dir.Player.GetComponent<PlayerCombat>();
            if (pc != null) pc.LookAt(boss.transform.position - dir.Player.position, 12f);   // Ren faces the boss for the whole scene
            bool holding = true;
            StartCoroutine(HoldInput(() => holding));
            CutsceneState.Begin();

            float size = Camera.main.orthographicSize;
            float oldSharp = cam.focusSharpness;
            cam.focusSharpness = 3f;
            if (first && entrance.Length > 0) yield return Entrance(boss, props, entrance, eflag, cam, size, hidden);
            foreach (var r in hidden) if (r != null) r.enabled = true;
            // camera fully on the boss (boss centred) during the complaint + minion toss
            Vector3 at = boss.transform.position + Vector3.up * 0.45f;
            cam.Focus(at, size);                                    // keep the frame size: resizing makes the pixel art scale out of step
            yield return Wait(0.8f);

            // boss speaks: dialogue box at the bottom of the screen (boss name, auto-advance; T/mouse = next line, Space = skip scene)
            var talk = new List<string>();
            for (int i = 1; i <= 6 && props != null; i++)
            {
                string line = props.GetString("intro_line" + i, "");
                if (string.IsNullOrEmpty(line)) break;
                talk.Add(line);
            }
            if (first && talk.Count > 0 && !skip)
            {
                DialogueBox.Open(name, talk.ToArray(), null, 1.4f);
                while (DialogueBox.Active && !skip) yield return null;
                if (DialogueBox.Active) DialogueBox.Close();
            }

            // wind-up + slam + shake ONLY when the boss has minions to toss (Grok) or `intro_slam` = true; otherwise every boss swung a basic
            // attack mid-scene and players thought they were hit before the fight began. Other bosses play their own pose `intro_anim`
            // (Vow, Toll, Cast, Prepare...), with a light shake when `intro_roar` = true.
            bool slam = minions.Count > 0 || (props != null && props.GetBool("intro_slam", false));
            if (slam)
            {
                // wind up ONCE, then slam (Windup is a 0.625 s looping clip: looping it for the whole scene made the boss twitch); on the slam the minions fly
                float windT = Clip(boss, "Windup", 0.625f), slamT = Clip(boss, "Attack", 0.6f);
                if (!skip && boss.HasAnimState("Windup")) { boss.PlayState("Windup"); yield return Wait(windT); }
                if (!skip && boss.HasAnimState("Attack")) boss.PlayState("Attack");
                foreach (var m in minions) StartCoroutine(Toss(boss, m, skip ? 0f : 0.45f));
                if (!skip) StartCoroutine(Rumble(0.45f, 0.07f));
                yield return Wait(slamT);
                if (boss != null && !boss.Health.IsDead) boss.PlayState("Idle");
                yield return Wait(0.35f);                           // minions just flew off -> the title card lands as the accent
            }
            else
            {
                string ia = props != null ? props.GetString("intro_anim", "") : "";
                if (!skip && ia.Length > 0 && boss.HasAnimState(ia))
                {
                    boss.PlayState(ia);
                    if (props.GetBool("intro_roar", false)) StartCoroutine(Rumble(0.4f, 0.05f));
                    var bsr = boss.GetComponentInChildren<SpriteRenderer>(true);
                    if (bsr != null) PlayVfx(props, "intro_vfx", boss.transform.position, bsr.sortingLayerID, bsr.sortingOrder - 1, BossScale(bsr));   // e.g. Morrow's bell toll
                    yield return Wait(Clip(boss, ia, 0.8f));
                    if (boss != null && !boss.Health.IsDead) boss.PlayState("Idle");
                }
                yield return Wait(0.25f);
            }
            SetPortrait(boss, props);
            ShowCard(name, sub, replay ? 2.2f : 2.8f);
            yield return Wait(replay ? 1.8f : 2.6f);
            if (skip && CardActive) CardLength = Mathf.Min(CardLength, Time.time - CardStart + 0.5f);   // skipped: fade out at once

            cam.Release();
            StartCoroutine(FadeFlares(1.2f));                       // lights that flared during the scene settle down
            yield return new WaitForSeconds(0.35f);
            cam.focusSharpness = oldSharp;
            holding = false;
            CutsceneState.End();
            if (pc != null) pc.StopLook();
            if (ph != null) ph.Invulnerable = false;
            if (boss != null && !boss.Health.IsDead) boss.PlayState("Idle");
            if (!string.IsNullOrEmpty(zoneHint)) Hints.Show(zoneHint, 4.5f);
            yield return new WaitForSeconds(0.4f);                  // one beat for the player to get ready before the boss charges
            if (boss != null) boss.Held = false;
            if (partner != null) partner.Held = false;
        }

        // ---- boss entrance (properties on the Tiled "boss" object)
        //   burst : thumping on `entrance_shake` (boiler, door...), then breaks out and charges from `entrance_from` to its spot (animation entrance_anim, default ChargeRun)
        //   leap  : `entrance_shake` (breeding tank) shakes, flag `entrance_flag` turns on (toggle swaps the occupied tank for an empty one), the boss leaps out in an arc
        //   wake  : the boss sleeps in place: 3 heartbeats (`wake_color` glow + shake) growing stronger, then it roars
        private IEnumerator Entrance(EnemyAI boss, SuperCustomProperties p, string kind, string eflag, CameraFollow cam, float size, List<SpriteRenderer> hidden)
        {
            if (boss == null || p == null) yield break;
            Vector3 home = boss.transform.position;
            Vector3 from = CutsceneFx.TileProp(dir, p, "entrance_from", home);
            var shake = CutsceneFx.Find(dir.mapRoot, p.GetString("entrance_shake", ""));
            var sr0 = boss.GetComponentInChildren<SpriteRenderer>(true);
            int layer = sr0 != null ? sr0.sortingLayerID : 0, order = sr0 != null ? sr0.sortingOrder : 2;

            // ---- per-boss entrances (instead of the shared "ground shakes in place")
            //   mist     : appears from swirling blood mist (Isolde) - `mist_color`
            //   rise     : rises from the ground, growing from the feet, cracking earth with flying gravel (Ent)
            //   approach : `entrance_lights` flare one by one, the boss walks from `entrance_from` to its spot (Lantern Knight)
            //   turn     : the boss prays with its back turned (facing north), `entrance_lights` flare one by one, then it turns to face Ren (Morrow)
            if (kind == "mist")
            {
                Color mc = ColorUtility.TryParseHtmlString(p.GetString("mist_color", "#ff3a6a"), out var mcc) ? mcc : new Color(1f, 0.25f, 0.4f);
                var glow = Flare(home + Vector3.up * 0.4f, mc, 0f, 3.2f);
                cam.Focus(home + Vector3.up * 0.6f, size);
                yield return Wait(0.5f);
                for (int i = 0; i < 12 && !skip; i++)                      // mist swirl shrinks toward the boss spot
                {
                    float a = i * 0.9f, rr = Mathf.Lerp(1.3f, 0.25f, i / 11f);
                    StartCoroutine(CutsceneFx.Puff(home + new Vector3(Mathf.Cos(a) * rr, Mathf.Sin(a) * rr * 0.55f + 0.2f, 0f), layer, order + 1, 16f));
                    glow.intensity = Mathf.Lerp(0.4f, 2.4f, i / 11f);
                    if (i == 6) PlayVfx(p, "entrance_vfx", home, layer, order + 1, BossScale(sr0));   // bats + blood mist (PixelLab) burst exactly as she appears
                    if (i % 4 == 0) CameraFollow.KickMain(Random.insideUnitCircle.normalized, 0.02f, 0.15f);
                    yield return Wait(0.11f);
                }
                var cols = new List<Color>();
                foreach (var r in hidden) if (r != null) { cols.Add(r.color); r.color = new Color(r.color.r, r.color.g, r.color.b, 0f); r.enabled = true; }
                for (float t = 0f; t < 0.7f; t += Time.deltaTime)
                {
                    float u = t / 0.7f;
                    for (int i = 0; i < hidden.Count; i++) if (hidden[i] != null) { var c0 = cols[i]; hidden[i].color = new Color(c0.r, c0.g, c0.b, c0.a * u); }
                    boss.TeleportTo(home + Vector3.up * 0.3f * (1f - u * u));
                    yield return null;
                }
                for (int i = 0; i < hidden.Count; i++) if (hidden[i] != null) hidden[i].color = cols[i];
                hidden.Clear();
                boss.TeleportTo(home);
                CameraFollow.KickMain(Vector2.down, 0.06f, 0.3f);
                CutsceneFx.Burst(this, home, 0.6f, 0.22f, 7, 0, layer, order);
                glow.intensity = 1.4f;
                if (eflag.Length > 0) dir.Flags.Set(eflag);
                string ea = p.GetString("entrance_anim", "");
                if (ea.Length > 0 && boss.HasAnimState(ea)) { boss.PlayState(ea); yield return Wait(Clip(boss, ea, 0.7f)); boss.PlayState("Idle"); }
                yield break;
            }
            if (kind == "rise")
            {
                var body = boss.Body != null ? boss.Body.transform : boss.transform;
                Vector3 s0 = body.localScale;
                body.localScale = new Vector3(s0.x, s0.y * 0.05f, s0.z);
                foreach (var r in hidden) if (r != null) r.enabled = true;
                hidden.Clear();
                cam.Focus(home + Vector3.up * 0.6f, size);
                yield return Wait(0.6f);
                float[] steps = { 0.3f, 0.65f, 1f };
                for (int i = 0; i < steps.Length; i++)
                {
                    CameraFollow.KickMain(Vector2.down, 0.05f + 0.035f * i, 0.3f);
                    if (i == 0) PlayVfx(p, "entrance_vfx", home, layer, order + 1, BossScale(sr0));   // thorn roots + cracked earth (PixelLab)
                    CutsceneFx.Burst(this, home, 0.5f + 0.15f * i, 0.18f, 4 + 2 * i, 3 + 2 * i, layer, order + 1);
                    float h0 = body.localScale.y / Mathf.Max(0.0001f, s0.y);
                    for (float t = 0f; t < 0.32f; t += Time.deltaTime)
                    {
                        float u = 1f - (1f - t / 0.32f) * (1f - t / 0.32f);
                        body.localScale = new Vector3(s0.x, s0.y * Mathf.Lerp(h0, steps[i], u), s0.z);
                        yield return null;
                    }
                    if (!skip) yield return Wait(i < 2 ? 0.45f : 0.2f);
                }
                body.localScale = s0;
                if (eflag.Length > 0) dir.Flags.Set(eflag);
                yield break;
            }
            if (kind == "approach" || kind == "turn")
            {
                Color lc = ColorUtility.TryParseHtmlString(p.GetString("lights_color", "#ffd27a"), out var lcc) ? lcc : new Color(1f, 0.82f, 0.48f);
                if (kind == "turn" && boss.HasAnimState("Idle_N")) boss.PlayState("Idle_N");
                if (kind == "approach") boss.TeleportTo(from);
                cam.Focus((kind == "approach" ? Vector3.Lerp(from, home, 0.5f) : home) + Vector3.up * 0.5f, size);
                yield return Wait(0.6f);
                foreach (var n in p.GetString("entrance_lights", "").Split(','))  // lights flare one by one
                {
                    if (skip) break;
                    var t = CutsceneFx.Find(dir.mapRoot, n.Trim());
                    if (t == null) continue;
                    var col = t.GetComponent<Collider2D>();
                    Vector3 lp = col != null ? (Vector3)col.bounds.center : t.position;
                    StartCoroutine(FlareUp(Flare(lp, lc, 0f, 2.4f), 2.6f, 1.1f));
                    var lsr = t.GetComponentInChildren<SpriteRenderer>();
                    PlayVfx(p, "light_vfx", lp, lsr != null ? lsr.sortingLayerID : layer, (lsr != null ? lsr.sortingOrder : order) + 5, 0.6f);   // fire flares at the lamp head
                    CameraFollow.KickMain(Vector2.down, 0.025f, 0.15f);
                    yield return Wait(0.42f);
                }
                if (kind == "turn")
                {
                    yield return Wait(0.3f);
                    boss.PlayState("Idle");                                  // turn back to face Ren
                    yield return Wait(0.35f);
                }
                else
                {
                    float spd = p.GetFloat("entrance_speed", 1.1f), dist = Vector3.Distance(from, home);
                    float walkT = Mathf.Max(0.4f, dist / Mathf.Max(0.2f, spd)), nextStep = 0f;
                    if (boss.HasAnimState("Move")) boss.PlayState("Move");
                    for (float t = 0f; t < walkT && !skip; t += Time.deltaTime)
                    {
                        boss.TeleportTo(Vector3.Lerp(from, home, t / walkT));
                        if (t >= nextStep) { nextStep = t + 0.5f; CameraFollow.KickMain(Vector2.down, 0.02f, 0.12f); StartCoroutine(CutsceneFx.Puff(boss.transform.position, layer, order - 1, 16f)); }
                        cam.Focus(boss.transform.position + Vector3.up * 0.45f, size);
                        yield return null;
                    }
                    boss.TeleportTo(home);
                    boss.PlayState("Idle");
                }
                if (eflag.Length > 0) dir.Flags.Set(eflag);
                yield break;
            }

            if (kind == "wake")
            {
                Color wc = ColorUtility.TryParseHtmlString(p.GetString("wake_color", "#b48cff"), out var c) ? c : new Color(0.7f, 0.55f, 1f);
                var go = new GameObject("Boss Wake Glow");
                go.transform.position = Head(boss, -0.5f);
                var glow = go.AddComponent<UnityEngine.Rendering.Universal.Light2D>();
                glow.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Point;
                glow.pointLightOuterRadius = 3f; glow.pointLightInnerRadius = 0.3f;
                glow.color = wc; glow.intensity = 0f;
                cam.Focus(home + Vector3.up * 0.6f, size);
                yield return Wait(0.9f);
                for (int beat = 0; beat < 3 && !skip; beat++)
                {
                    float peak = 1.3f + beat * 1.0f;
                    CameraFollow.KickMain(Vector2.down, 0.03f + beat * 0.025f, 0.25f);
                    StartCoroutine(CutsceneFx.Puff(home + new Vector3(Random.Range(-0.5f, 0.5f), 0.05f, 0f), layer, order + 1));
                    for (float t = 0f; t < 0.75f - beat * 0.15f && !skip; t += Time.deltaTime)
                    {
                        float u = t / (0.75f - beat * 0.15f);
                        glow.intensity = peak * (u < 0.2f ? u / 0.2f : 1f - (u - 0.2f) / 0.8f);
                        yield return null;
                    }
                }
                if (eflag.Length > 0) dir.Flags.Set(eflag);
                if (boss.HasAnimState("Awake")) boss.PlayState("Awake");
                glow.intensity = 3f;
                CameraFollow.KickMain(Vector2.down, 0.12f, 0.4f);
                CutsceneFx.Burst(this, home, 0.6f, 0.22f, 8, 0, layer, order);
                for (float t = 0f; t < 0.8f; t += Time.deltaTime) { glow.intensity = Mathf.Lerp(3f, 0f, t / 0.8f); yield return null; }
                Destroy(go);
                yield break;
            }

            // burst / leap: camera looks at where the boss is about to burst out
            cam.Focus(Vector3.Lerp(from, home, 0.4f) + Vector3.up * 0.3f, size);
            boss.TeleportTo(from);
            yield return Wait(0.8f);
            int thuds = kind == "burst" ? 3 : 2;
            for (int i = 0; i < thuds && !skip; i++)
            {
                CameraFollow.KickMain(Vector2.down, 0.035f + 0.02f * i, 0.22f);
                StartCoroutine(CutsceneFx.Jiggle(shake, 0.22f, 0.025f + 0.012f * i));
                StartCoroutine(CutsceneFx.Puff(from + new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(-0.1f, 0.15f), 0f), layer, order + 1));
                yield return Wait(kind == "burst" ? 0.55f - 0.1f * i : 0.65f);
            }
            if (eflag.Length > 0) dir.Flags.Set(eflag);
            CutsceneFx.Toggle(dir.mapRoot, p.GetString("entrance_hide", ""), false);
            CutsceneFx.Toggle(dir.mapRoot, p.GetString("entrance_show", ""), true);
            CameraFollow.KickMain(Vector2.down, 0.15f, 0.45f);
            CutsceneFx.Burst(this, from, 0.55f, 0.22f, 9, 7, layer, order);
            foreach (var r in hidden) if (r != null) r.enabled = true;
            cam.Focus(home + Vector3.up * 0.45f, size);

            string anim = p.GetString("entrance_anim", kind == "burst" ? "ChargeRun" : "Move");
            if (boss.HasAnimState(anim)) boss.PlayState(anim);
            float dur = p.GetFloat("entrance_time", kind == "burst" ? 0.45f : 0.7f), arc = kind == "leap" ? p.GetFloat("entrance_arc", 0.9f) : 0f;
            float nextTrail = 0f;
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                float u = t / dur;
                boss.TeleportTo(Vector3.Lerp(from, home, kind == "burst" ? 1f - (1f - u) * (1f - u) : u) + Vector3.up * (arc * 4f * u * (1f - u)));
                if (kind == "burst" && t >= nextTrail)                    // dust kicked up by the hooves
                {
                    nextTrail = t + 0.07f;
                    StartCoroutine(CutsceneFx.Puff(boss.transform.position, layer, order - 1, 16f));
                }
                yield return null;
            }
            boss.TeleportTo(home);
            CameraFollow.KickMain(Vector2.down, kind == "leap" ? 0.12f : 0.07f, 0.3f);
            CutsceneFx.Burst(this, home, 0.5f, 0.18f, kind == "leap" ? 8 : 5, kind == "leap" ? 4 : 0, layer, order);
            if (boss.HasAnimState("Idle")) boss.PlayState("Idle");
            yield return Wait(0.4f);
        }

        // ---- PixelLab effects in the scene (Resources/VFX/<name>, see SpriteVfx). Name comes from property `key` on the boss object
        //   (empty / "none" = do not play); `<key>_scale` overrides the scale, `<key>_fps` overrides the frame rate (default 14).
        private static void PlayVfx(SuperCustomProperties p, string key, Vector3 at, int layer, int order, float defScale)
        {
            if (p == null) return;
            string n = p.GetString(key, "");
            if (n.Length == 0 || n == "none") return;
            SpriteVfx.Spawn(n, at, p.GetFloat(key + "_scale", defScale), layer, order, false, p.GetFloat(key + "_fps", 14f));
        }

        /// effect scale from the boss's real size (effects are drawn around the original sprite, 1 pixel = 1 pixel, PPU 100).
        private static float BossScale(SpriteRenderer sr)
        {
            if (sr == null || sr.sprite == null) return 1f;
            return Mathf.Abs(sr.transform.lossyScale.y) * 100f / Mathf.Max(1f, sr.sprite.pixelsPerUnit);
        }

        private static readonly Color BossTalk = new Color(1f, 0.45f, 0.35f);

        // ---- lights flaring during the scene (fade out when the scene ends)
        private readonly List<UnityEngine.Rendering.Universal.Light2D> flares = new List<UnityEngine.Rendering.Universal.Light2D>();

        private UnityEngine.Rendering.Universal.Light2D Flare(Vector3 at, Color c, float intensity, float radius)
        {
            var go = new GameObject("Boss Intro Flare");
            go.transform.position = at;
            var l = go.AddComponent<UnityEngine.Rendering.Universal.Light2D>();
            l.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Point;
            l.color = c; l.intensity = intensity;
            l.pointLightOuterRadius = radius; l.pointLightInnerRadius = radius * 0.12f;
            flares.Add(l);
            return l;
        }

        private static IEnumerator FlareUp(UnityEngine.Rendering.Universal.Light2D l, float peak, float settle)
        {
            for (float t = 0f; t < 0.35f && l != null; t += Time.deltaTime)
            {
                float u = t / 0.35f;
                l.intensity = u < 0.4f ? peak * u / 0.4f : Mathf.Lerp(peak, settle, (u - 0.4f) / 0.6f);
                yield return null;
            }
            if (l != null) l.intensity = settle;
        }

        private IEnumerator FadeFlares(float seconds)
        {
            var list = new List<UnityEngine.Rendering.Universal.Light2D>(flares);
            flares.Clear();
            var start = new List<float>();
            foreach (var l in list) start.Add(l != null ? l.intensity : 0f);
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                for (int i = 0; i < list.Count; i++) if (list[i] != null) list[i].intensity = start[i] * (1f - t / seconds);
                yield return null;
            }
            foreach (var l in list) if (l != null) Destroy(l.gameObject);
        }

        // ---- card image: cropped to the drawn part (sprite Tight mesh) -> large, centred, no transparent margin
        private static readonly Dictionary<Sprite, Sprite> trimmed = new Dictionary<Sprite, Sprite>();

        public static Sprite Trim(Sprite s)
        {
            if (s == null) return null;
            if (trimmed.TryGetValue(s, out var t) && t != null) return t;
            var v = s.vertices;
            if (v == null || v.Length < 3 || s.packed) { trimmed[s] = s; return s; }
            float ppu = s.pixelsPerUnit;
            Vector2 piv = s.pivot;                                       // pivot in pixels inside the rect
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (var p in v)
            {
                float x = p.x * ppu + piv.x, y = p.y * ppu + piv.y;
                minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y); maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y);
            }
            var r = s.rect;
            minX = Mathf.Clamp(Mathf.Floor(minX), 0f, r.width); minY = Mathf.Clamp(Mathf.Floor(minY), 0f, r.height);
            maxX = Mathf.Clamp(Mathf.Ceil(maxX), 0f, r.width); maxY = Mathf.Clamp(Mathf.Ceil(maxY), 0f, r.height);
            if (maxX - minX < 2f || maxY - minY < 2f || (maxX - minX >= r.width - 1f && maxY - minY >= r.height - 1f)) { trimmed[s] = s; return s; }
            var cut = new Rect(r.x + minX, r.y + minY, maxX - minX, maxY - minY);
            var ns = Sprite.Create(s.texture, cut, new Vector2(0.5f, 0f), ppu, 0, SpriteMeshType.FullRect);
            ns.name = s.name + "_card";
            trimmed[s] = ns;
            return ns;
        }

        /// <summary>Tiled object of type "boss" nearest to the boss (e.g. grok_fight) - holds the dialogue / scene options.</summary>
        private SuperCustomProperties BossProps(EnemyAI boss) => PropsFor(dir, boss);

        /// <summary>Properties of the "boss" object (triggers layer) nearest to the boss. Public so BossRelic can share it.</summary>
        public static SuperCustomProperties PropsFor(MapEncounterDirector dir, EnemyAI boss)
        {
            if (dir == null || dir.mapRoot == null || boss == null) return null;
            SuperCustomProperties best = null; float bd = 400f;
            foreach (var so in dir.mapRoot.GetComponentsInChildren<SuperObject>(true))
            {
                if (so.m_Type != MapSchema.ObjectType.BossArena) continue;
                var col = so.GetComponent<Collider2D>();
                Vector2 c = col != null ? (Vector2)col.bounds.center : (Vector2)so.transform.position;
                float d = (c - (Vector2)boss.transform.position).sqrMagnitude;
                if (d < bd) { bd = d; best = so.GetComponent<SuperCustomProperties>(); }
            }
            return best;
        }

        private static Vector3 Head(EnemyAI e, float above)
        {
            var sr = e.GetComponentInChildren<SpriteRenderer>();
            float top = sr != null ? sr.bounds.max.y : e.transform.position.y + 0.8f;
            return new Vector3(e.transform.position.x, top + above, e.transform.position.z);
        }

        // ---- minions knocked away by the boss lie stunned (stars spinning overhead) until the boss is at half HP
        private readonly Dictionary<EnemyAI, SpriteVfx> stars = new Dictionary<EnemyAI, SpriteVfx>();

        private IEnumerator Toss(EnemyAI boss, EnemyAI m, float flight)
        {
            if (m == null || boss == null) yield break;
            m.Health.Invulnerable = true;                           // cannot be hit while lying stunned (until Grok wakes them)
            float side = m.transform.position.x >= boss.transform.position.x ? 1f : -1f;
            Vector2 from = m.transform.position, to = from + new Vector2(side * 0.75f, -0.45f);
            float t = 0f;
            while (flight > 0f && t < flight && m != null && !m.Health.IsDead)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / flight);
                Vector2 p = Vector2.Lerp(from, to, u) + Vector2.up * (Mathf.Sin(u * Mathf.PI) * 0.32f);
                m.TeleportTo(p);
                m.transform.rotation = Quaternion.Euler(0f, 0f, -side * 720f * u);
                yield return null;
            }
            if (m == null || m.Health.IsDead) yield break;
            m.TeleportTo(to);
            m.transform.rotation = Quaternion.Euler(0f, 0f, -side * 90f);   // sprawled out, head pointing away from the boss
            if (m.HasAnimState("Idle")) m.PlayState("Idle");
            var an = m.GetComponentInChildren<Animator>();
            if (an != null) an.speed = 0f;                          // unconscious - frozen frame (no breathing)
            var sr = m.GetComponentInChildren<SpriteRenderer>();
            var v = SpriteVfx.Spawn("stun_stars", to + new Vector2(side * 0.42f, 0.2f), 0.6f,
                                    sr != null ? sr.sortingLayerID : 0, (sr != null ? sr.sortingOrder : 2) + 3, false, 14f);
            if (v != null) { v.Looping(99999f); stars[m] = v; }
        }

        private void HookWake(EnemyAI boss, List<EnemyAI> minions, SuperCustomProperties props)
        {
            bool woke = false;
            void Wake()
            {
                if (woke) return;
                woke = true;
                StartCoroutine(WakeRoutine(boss, minions, props));
            }
            boss.Phase2Reached += _ => Wake();
            boss.Health.Died += _ => Wake();
        }

        private IEnumerator WakeRoutine(EnemyAI boss, List<EnemyAI> minions, SuperCustomProperties props)
        {
            string line = props != null ? props.GetString("wake_line", "") : "";
            if (!string.IsNullOrEmpty(line) && boss != null && !boss.Health.IsDead)
                SpeechBubble.Show(Head(boss, 0.18f), line, 2.4f, BossTalk).Follow(boss.transform);
            yield return new WaitForSeconds(0.6f);
            foreach (var m in minions)
            {
                if (stars.TryGetValue(m, out var v) && v != null) v.Stop();
                if (m == null || m.Health.IsDead) continue;
                StartCoroutine(GetUp(m));
            }
            // no extra hint - visuals / animation / boss title already tell the player
        }

        private static IEnumerator GetUp(EnemyAI m)
        {
            Vector2 p0 = m.transform.position;
            float z0 = m.transform.eulerAngles.z;
            for (float t = 0f; t < 0.3f && m != null; t += Time.deltaTime)
            {
                float u = t / 0.3f;
                m.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(z0, 0f, u));
                m.TeleportTo(p0 + Vector2.up * (Mathf.Sin(u * Mathf.PI) * 0.12f));
                yield return null;
            }
            if (m == null) yield break;
            m.transform.rotation = Quaternion.identity;
            m.TeleportTo(p0);
            var an = m.GetComponentInChildren<Animator>();
            if (an != null) an.speed = 1f;
            m.Health.Invulnerable = false;
            m.Held = false;
        }

        /// <summary>Clip length of the state (if found in the Animator), otherwise the default value.</summary>
        private static float Clip(EnemyAI e, string state, float fallback)
        {
            var an = e != null ? e.GetComponentInChildren<Animator>() : null;
            if (an == null || an.runtimeAnimatorController == null) return fallback;
            foreach (var c in an.runtimeAnimatorController.animationClips)
                if (c != null && c.name.EndsWith("_" + state)) return Mathf.Clamp(c.length, 0.2f, 1.5f);
            return fallback;
        }

        private static void SetPortrait(EnemyAI boss, SuperCustomProperties props)
        {
            CardSprite = boss != null ? Trim(boss.PortraitSprite()) : null; // facing-down image, cropped tight (large + centred)
            CardPortrait = props != null ? props.GetString("portrait", "") : "";
            CardColor = new Color(0.47f, 0.36f, 0.16f);
            string hex = props != null ? props.GetString("card_color", "") : "";
            if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out var c)) CardColor = c;
        }

        private static void ShowCard(string name, string sub, float seconds)
        {
            CardName = name; CardSub = sub;
            CardStart = Time.time; CardLength = seconds;
            SoulRequiem.Core.Sfx.Play2D("boss_intro");   // sound
        }

        /// <summary>Keep controls locked for the whole scene (closing the Pause menu releases Blocked -> lock again every frame); Space/Enter = skip.</summary>
        private IEnumerator HoldInput(System.Func<bool> active)
        {
            while (active())
            {
                GameInput.Blocked = true;
                var kb = Keyboard.current;
                if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)) skip = true;
                yield return null;
            }
            GameInput.Blocked = false;
        }

        /// <summary>Screen shake: a chain of short jolts changing direction (the roar).</summary>
        private IEnumerator Rumble(float seconds, float amp)
        {
            float end = Time.time + seconds;
            int i = 0;
            while (Time.time < end)
            {
                float a = i * 2.39996f;                               // golden angle: jolt directions never repeat
                CameraFollow.KickMain(new Vector2(Mathf.Cos(a), Mathf.Sin(a)), amp, 0.08f);
                i++;
                yield return new WaitForSeconds(0.05f);
            }
        }

        private IEnumerator Wait(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end && !skip) yield return null;
        }
    }
}
