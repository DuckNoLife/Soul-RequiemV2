using System.Collections;
using System.Collections.Generic;
using SuperTiled2Unity;
using UnityEngine;
using UnityEngine.InputSystem;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Map 1 opening scene (runs only once, flag "intro_done"): locks controls, pans the camera to a burning house (goblins set fire, villagers cry for help),
    /// then to Todd's porch (villagers hiding and begging), returns the camera to Ren, then teaches step by step: walk -> slash when near the first raider -> the second one.
    /// Tiled data (triggers layer, type "cutscene_point"): intro_cam_1, intro_cam_2 (pan points);
    /// intro_say_N (shot = 1|2, text, delay in seconds after reaching the shot, color = goblin|villager). Cannot be skipped.
    /// </summary>
    public class OpeningCutscene : MonoBehaviour
    {
        private const string DoneFlag = "intro_done";
        private bool skip;

        private IEnumerator Start()
        {
            yield return null;                                       // wait for the director to finish building the map / flags
            yield return null;                                       // so StoryScenes gets the "start" scene (opening slides) in time
            while (StoryScenes.Busy) yield return null;
            var dir = MapEncounterDirector.Instance;
            if (dir == null || dir.Flags == null || dir.Flags.HasAll(DoneFlag) || dir.Player == null) yield break;
            if (StoryScenes.DevSkipIntros) { dir.Flags.Set(DoneFlag); yield break; }      // area replay test -> skip the map opening scene

            var cam = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            Transform shot1 = Find("intro_cam_1"), shot2 = Find("intro_cam_2");
            if (cam == null || shot1 == null) { dir.Flags.Set(DoneFlag); yield break; }

            float size = Camera.main.orthographicSize;
            float oldSharp = cam.focusSharpness;
            StartCoroutine(HoldInput());
            CutsceneState.Begin();

            // the area holding the camera pan point (Map 1: zf, goblins burning the house) starts AT ONCE - if it only started once Ren walked into
            // the area box, the locked Ren at the area edge meant the camera panned to text with no goblins.
            if (dir.TryGetZoneAt(shot1.position, out var shotZone, out _)) dir.ForceStartZone(shotZone);
            Hints.Show("Millbrook. New moon - and Grok's tithe-collectors have come.", 3.2f);
            Sfx.Play2D("cs_slide");          // narration sound opening the scene (narration -> page-turn sound played by DialogueVoice)
            Sfx.Bed("cs_story_bed");         // narration bed during the camera pans, fades out when the camera returns to Ren
            yield return Wait(1.2f);

            cam.focusSharpness = 1.4f;
            if (!skip) yield return Shot(cam, shot1, size, 1);
            if (!skip && shot2 != null) yield return Shot(cam, shot2, size, 2);

            cam.focusSharpness = 2.2f;
            cam.Release();
            Sfx.Bed(null);
            if (!skip) yield return Wait(1.6f);
            cam.focusSharpness = oldSharp;

            dir.Flags.Set(DoneFlag);                                 // HoldInput releases itself once the flag is set
            yield return null;
            StartCoroutine(Tutorial(dir));
        }

        private IEnumerator Shot(CameraFollow cam, Transform at, float size, int shot)
        {
            cam.Focus(at.position, size);
            yield return Wait(2.4f);
            if (skip) yield break;
            float last = 0f;
            foreach (var so in GetComponentsInMap())
            {
                if (!so.m_TiledName.StartsWith("intro_say_")) continue;
                var p = so.GetComponent<SuperCustomProperties>();
                if (p == null || p.GetInt("shot", 1) != shot) continue;
                float delay = p.GetFloat("delay", 0f);
                bool goblin = p.GetString("color", "villager") == "goblin";
                // blip voice - Tiled property `voice` (e.g. "woman", "Goblin"), missing: goblin / villager (alternating female - male by index)
                int.TryParse(so.m_TiledName.Substring("intro_say_".Length), out int no);
                string voice = p.GetString("voice", goblin ? "goblin" : no % 2 == 0 ? "woman" : "villager");
                StartCoroutine(SayLater(so.transform.position, p.GetString("text", "..."), delay, goblin, voice));
                last = Mathf.Max(last, delay);
            }
            yield return Wait(last + 2.6f);
        }

        private IEnumerator SayLater(Vector3 at, string text, float delay, bool goblin, string voice)
        {
            yield return Wait(delay);
            if (skip) yield break;
            SpeechBubble.Show(at, text, 2.6f, goblin ? new Color(1f, 0.55f, 0.35f) : new Color(1f, 0.95f, 0.85f), null, voice);
        }

        /// <summary>Keep controls locked for the whole scene (closing the Pause menu releases Blocked -> lock again every frame); Space/Enter = skip.</summary>
        private IEnumerator HoldInput()
        {
            var dir = MapEncounterDirector.Instance;
            while (dir != null && !dir.Flags.HasAll(DoneFlag))
            {
                GameInput.Blocked = true;
                // story scenes CANNOT be skipped (only the boss intro card can)
                yield return null;
            }
            GameInput.Blocked = false;
            CutsceneState.End();
        }

        private static readonly Color GoblinTalk = new Color(1f, 0.55f, 0.35f);

        /// <summary>Step-by-step teaching after the scene, "freeze the game and wait for the right key" style:
        /// walk to the farm -> the nearest raider shouts a threat (controls locked ~2 seconds) then charges -> right next to Ren: FREEZE, blink
        /// [J / left mouse] ATTACK -> the first time it winds up an attack: FREEZE, blink [Q] DODGE -> the second raider (waiting from the start, same wave) shouts and charges.</summary>
        private IEnumerator Tutorial(MapEncounterDirector dir)
        {
            Hints.Show("Drive the raiders out! Follow the marker west to the burning farm.", 5f);
            var player = dir.Player;
            var combat = player.GetComponent<PlayerCombat>();
            var farm = Find("intro_cam_1");
            Vector2 farmPos = farm != null ? (Vector2)farm.position : (Vector2)player.position;

            // this fight's raiders: living enemies around the farm
            // the area's enemies spawn with a "telegraph" (dust + 0.5 s delay each) -> after a fast skip NOBODY is there yet,
            // which skipped the whole tutorial (no ATTACK screen, goblins dying instead of fleeing). Wait until they are all out (at most 8 s).
            var raiders = new List<EnemyAI>();
            float waitUntil = Time.time + 8f, settle = -1f;
            while (Time.time < waitUntil)
            {
                raiders.Clear();
                foreach (var e in FindObjectsByType<EnemyAI>(FindObjectsSortMode.None))
                    if (e != null && e.Health != null && !e.Health.IsDead && ((Vector2)e.transform.position - farmPos).sqrMagnitude < 49f) raiders.Add(e);
                if (raiders.Count > 0 && settle < 0f) settle = Time.time + 1.2f;      // the first one is out: wait a bit longer for the rest
                if (settle > 0f && Time.time >= settle) break;
                yield return null;
            }
            if (raiders.Count == 0) yield break;
            // "drive them off" story - raiders do not die: at 1 HP they flee, threatening to tell Grok (the last one gets a short scene)
            foreach (var e in raiders) e.Health.Floor = 1f;
            StartCoroutine(WatchFlee(raiders, player, combat));

            // 1) wait for the player to get near the first raider
            EnemyAI first = null;
            float giveUp = Time.time + 180f;
            while (first == null && Time.time < giveUp && !dir.Flags.HasAll("zf_cleared"))
            {
                float best = 3.1f * 3.1f;
                foreach (var e in raiders)
                {
                    if (!Alive(e)) continue;
                    float d = ((Vector2)(e.transform.position - player.position)).sqrMagnitude;
                    if (d < best) { best = d; first = e; }
                }
                yield return null;
            }
            if (first == null) yield break;
            foreach (var e in raiders) if (Alive(e)) e.Held = true;

            // 2) the raider steps closer (~1.35 units, both in frame, speech bubble not cut off), then shouts, then charges
            first.LookAtPlayer = true;
            first.CutsceneActor = true;                              // other enemies stand still during the scene, only this one may step forward
            CutsceneState.Begin();
            float stepEnd = Time.time + 2.5f;
            first.Held = false;
            while (Alive(first) && Time.time < stepEnd &&
                   ((Vector2)(first.transform.position - player.position)).magnitude > 1.35f)
            {
                GameInput.Blocked = true;
                if (combat != null) combat.LookAt(first.transform.position - player.position, 0.5f);
                yield return null;
            }
            if (Alive(first)) first.Held = true;
            if (combat != null && first != null) combat.LookAt(first.transform.position - player.position, 2.5f);
            if (first != null)
                SpeechBubble.Show(first.transform.position + new Vector3(0f, 0.75f, 0f), "Heh! Fresh meat for the fire!", 2f, GoblinTalk).Follow(first.transform);
            yield return Block(2f);
            CutsceneState.End();
            if (first != null) { first.Held = false; first.CutsceneActor = false; first.Provoke(); }   // charge at once, no waiting

            // 3) ATTACK: freeze when it reaches the player
            while (Alive(first) && ((Vector2)(first.transform.position - player.position)).magnitude > 0.62f && !dir.Flags.HasAll("zf_cleared"))
                yield return null;
            if (Alive(first))
            {
                // the goblin's torch swing (0.2 s) is faster than the slash (0.48 s to the hit frame) and knocks the player back ->
                // the tutorial's first slash often MISSED. Keep the goblin still + the player unhittable while the first slash
                // plays out -> the lesson "strike before it can attack" always succeeds.
                first.Held = true;
                yield return Prompt(combat, GameAction.Attack, "ATTACK",
                    $"{GameInput.Label(GameAction.Attack)}  /  LEFT CLICK", "Strike the raider before it strikes you.",
                    first.transform.position - player.position);
                var php = player.GetComponent<Health>();
                if (php != null) php.Invulnerable = true;
                yield return new WaitForSeconds(0.7f);
                if (php != null) php.Invulnerable = false;
                if (Alive(first) && first.Health.Current > first.Health.Floor + 0.01f) first.Held = false;   // a real miss (rare): keep fighting as normal
            }

            // 4) the DODGE [Q] screen moved to the lizard area (TutorialDirector)

            // 5) second raider: wait for the first to fall (or a while), shout, then charge
            float next = Time.time + 8f;
            while (Alive(first) && Time.time < next) yield return null;
            yield return new WaitForSeconds(0.6f);
            foreach (var e in raiders)
            {
                if (e == first || !Alive(e) || !e.Held) continue;
                // the second raider waits off screen -> it would shout unseen and then stand still (out of detection range).
                // Let it run close (~1.5 units) first, then shout + charge.
                if (e.HasAnimState("Move")) e.PlayState("Move");
                for (float t = 0f; t < 4f && Alive(e) && player != null; t += Time.deltaTime)
                {
                    Vector2 to = (Vector2)(player.position - e.transform.position);
                    if (to.magnitude <= 1.5f) break;
                    e.FaceX(to.x);
                    e.TeleportTo(Vector2.MoveTowards(e.transform.position, player.position, 1.9f * Time.deltaTime));
                    yield return null;
                }
                if (!Alive(e)) continue;
                if (e.HasAnimState("Idle")) e.PlayState("Idle");
                e.LookAtPlayer = true;
                if (combat != null) combat.LookAt(e.transform.position - player.position, 1.2f);
                SpeechBubble.Show(e.transform.position + new Vector3(0f, 0.75f, 0f), "You'll burn with the rest of 'em!", 2f, GoblinTalk).Follow(e.transform);
                yield return new WaitForSeconds(0.9f);
                e.Held = false;
                // only releasing Held left the second raider waiting for Ren: it had been pulled past its leash from home,
                // so EnemyAI did not "detect" Ren (outside its home area) / or walked home. Provoke = aggressive at once, ignoring the leash.
                e.Provoke();
            }
            foreach (var e in raiders) if (e != null) { e.Held = false; if (Alive(e) && e.Health.Current > e.Health.Floor + 0.01f) e.Provoke(); }   // safety net: nobody stays held / frozen forever
        }

        private static bool Alive(EnemyAI e) => e != null && e.Health != null && !e.Health.IsDead;

        private IEnumerator WatchFlee(List<EnemyAI> raiders, Transform player, PlayerCombat combat)
        {
            var fleeing = new HashSet<EnemyAI>();
            while (true)
            {
                int left = 0;
                foreach (var e in raiders) if (e != null && !fleeing.Contains(e)) left++;
                if (left == 0) yield break;
                foreach (var e in raiders)
                {
                    if (e == null || fleeing.Contains(e) || e.Health.Current > e.Health.Floor + 0.01f) continue;
                    fleeing.Add(e);
                    StartCoroutine(Flee(e, left == 1, player, combat));
                    left--;
                }
                yield return null;
            }
        }

        private IEnumerator Flee(EnemyAI e, bool last, Transform player, PlayerCombat combat)
        {
            e.Held = true;
            e.Health.Invulnerable = true;
            e.LookAtPlayer = false;
            var dir = MapEncounterDirector.Instance;
            string line = last ? "You'll regret this! Grok will hear of it!" : "Ow-ow-OW! I'm telling Grok!";
            SpeechBubble.Show(e.transform.position + new Vector3(0f, 0.75f, 0f), line, 2.4f, GoblinTalk).Follow(e.transform);   // text follows the goblin
            if (last)
            {
                CutsceneState.Begin();                               // last one: lock controls for a beat, Ren watches it go
                if (combat != null) combat.LookAt(e.transform.position - player.position, 3f);
                yield return Block(1.1f);
            }
            else yield return new WaitForSeconds(0.6f);
            if (e == null) { if (last) CutsceneState.End(); yield break; }

            // runs along the village road EAST to the bridge (the way to Grok's camp), fades out and vanishes -
            // the area counts as cleared once they are all gone
            Vector2 target = dir != null ? (Vector2)dir.TileToWorld(91.0f, 79.4f) : (Vector2)e.transform.position + Vector2.right * 10f;
            if (e.HasAnimState("Move")) e.PlayState("Move");
            e.FaceX(target.x - e.transform.position.x);
            var sr = e.GetComponentInChildren<SpriteRenderer>();
            Color c0 = sr != null ? sr.color : Color.white;
            const float runFor = 3.8f;
            for (float t = 0f; t < runFor && e != null; t += Time.deltaTime)
            {
                e.TeleportTo(Vector2.MoveTowards(e.transform.position, target, 1.7f * Time.deltaTime));   // 1.7 units/s (player 2): still visible running away, line readable in time
                if (sr != null && t > runFor - 1f) sr.color = new Color(c0.r, c0.g, c0.b, c0.a * Mathf.Clamp01((runFor - t) / 1f));
                if (last && t > 0.8f && CutsceneState.Active) { CutsceneState.End(); last = false; }
                yield return null;
            }
            if (last) CutsceneState.End();
            if (e != null) Destroy(e.gameObject);
        }

        /// <summary>Lock controls for `seconds` seconds (closing the Pause menu releases Blocked -> lock again every frame).</summary>
        private static IEnumerator Block(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end) { GameInput.Blocked = true; yield return null; }
            GameInput.Blocked = false;
        }

        private static IEnumerator Prompt(PlayerCombat combat, GameAction a, string title, string keys, string sub, Vector2 dir)
            => TutorialPrompt.Run(combat, a, title, keys, sub, dir);

        private IEnumerator Wait(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end && !skip) yield return null;
        }

        private Transform Find(string tiledName)
        {
            foreach (var so in GetComponentsInMap()) if (so.m_TiledName == tiledName) return so.transform;
            return null;
        }

        private SuperObject[] cache;
        private SuperObject[] GetComponentsInMap()
        {
            if (cache == null)
            {
                var dir = MapEncounterDirector.Instance;
                cache = dir != null && dir.mapRoot != null ? dir.mapRoot.GetComponentsInChildren<SuperObject>(true) : new SuperObject[0];
            }
            return cache;
        }
    }
}
