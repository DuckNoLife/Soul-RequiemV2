using System.Collections;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Boss mercy sequence: the boss collapses at its last HP and BEGS, the player decides on a CHOICE CARD (ChoiceCard):
    ///   1. Collapse: flash + hit-stop, the boss kneels (Stagger held on its last frame), blood pool spreads, camera pushes in between Ren and the boss, letterbox.
    ///   2. Plea: 3 voiced lines, then the choice card states CLEARLY what is gained / lost.
    ///   3a. SPARE: Ren speaks, the boss answers, HANDS OVER an item (spareKey - the real image flies into Ren and into the inventory), flag `spareFlag` (Mercy), the boss turns into a swarm of bats and vanishes.
    ///   3b. KILL : the boss says a last line, Ren strikes (flash, hit-stop, blood), the boss dies (Death animation, area cleared), DROP (killReward - the real image
    ///              rises from the corpse, hovers, then flies into Ren's chest) = immediate reward, flag `slainFlag`.
    /// The default text is Isolde's (Map 3); other bosses override the fields right after BossMercy.Begin (before the next frame).
    /// Items: image Resources/VFX/props/pickup_&lt;id&gt;.png. The inventory item (spareKey) must be defined on the map (pickup item=key, key_id).
    /// </summary>
    public class BossMercy : MonoBehaviour
    {
        public string spareFlag = "mercy_3", slainFlag = "isolde_slain";

        [Header("Choice card")]
        public string cardTitle = "THE MATRIARCH KNEELS";
        public string cardSub = "Isolde Vey has no blood left to spend. What she is owed is yours to decide.";
        public string spareTitle = "SPARE HER", spareTagline = "Let the last Vey walk away.";
        public string[] spareGains = { "Mercy — one of seven. Mercies shape how this ends.", "The Vey Signet — opens the sealed crypt of House Vey.", "House Vey will owe you a debt." };
        public string[] spareLosses = { "The Vey Heartblood is lost." };
        public string killTitle = "END HER", killTagline = "Take what is left of the Vey.";
        public string[] killGains = { "Vey Heartblood — +1 flask, filled now." };
        public string[] killLosses = { "This mercy is lost forever.", "No Vey Signet." };

        [Header("Dialogue")]
        public string[] pleaLines =
        {
            "Wait... stay your blade.",
            "Six hundred years I kept this vale. I will not beg twice, Nameless.",
            "End me, and drink what is left of the Vey. Or let me go - and my House will owe you a debt.",
        };
        public string spareLine = "Go. Before I change my mind.";
        public string bossReply = "...You are a stranger thing than any Hierophant.";
        public string giftLine = "Take my signet. The Vey keep their debts - even to you.";
        public string killBossLine = "Then drink, Nameless. It will not make you one of us.";

        [Header("Rewards")]
        public string spareKey = "vey_signet";                            // inventory item id (KeyRing) - defined on the map
        public string spareKeyText = "Obtained: The Vey Signet. The crest of House Vey - it opens their sealed crypt.";
        public string killReward = "vey_heartblood";                      // image pickup_vey_heartblood; effect: +1 flask slot
        public string killRewardText = "Obtained: Vey Heartblood. Flask capacity +1.";
        public int killFlaskSlots = 1;
        public Color bloodColor = new Color(1f, 0.25f, 0.32f);
        public string spareVfx = "m3_intro_bats";

        private EnemyAI ai;
        private Health hp;
        private bool resolved;
        private CameraFollow cam;
        private float camSize, camSharp;

        public static BossMercy Begin(EnemyAI ai, string spareFlag, string slainFlag)
        {
            var m = ai.gameObject.AddComponent<BossMercy>();
            m.ai = ai; m.hp = ai.Health; m.spareFlag = spareFlag; m.slainFlag = slainFlag;
            return m;
        }

        private Transform Player => MapEncounterDirector.Instance != null ? MapEncounterDirector.Instance.Player : null;
        private Vector3 Chest => transform.position + new Vector3(0f, 0.45f, 0f);
        private int Layer { get { var sr = GetComponent<SpriteRenderer>(); return sr != null ? sr.sortingLayerID : 0; } }
        private int Order { get { var sr = GetComponent<SpriteRenderer>(); return sr != null ? sr.sortingOrder : 2; } }

        private IEnumerator Start()
        {
            if (ai == null) ai = GetComponent<EnemyAI>();
            if (hp == null) hp = GetComponent<Health>();
            ai.Held = true;
            ai.Desired = Vector2.zero;
            hp.Invulnerable = true;
            hp.Floor = 0f;
            hp.Died += OnDied;
            BossJuice.Clear(hp);

            // 1. COLLAPSE: flash + hit-stop + kneel, blood pool spreads
            CutsceneState.Begin();
            BossJuice.Impact(Chest, bloodColor, 1.2f, 3, Layer, Order + 2);
            Sfx.Play("body_fall", transform.position);
            ai.PlayState(ai.HasAnimState("Stagger") ? "Stagger" : "Idle");
            var pool = SpriteVfx.Spawn("m3_blood_pool", (Vector2)transform.position + Vector2.down * 0.05f, 0.8f, Layer, Order - 3, false, 8f, false);
            if (pool != null) pool.Looping(60f);
            var glow = BossJuice.Light(Chest, bloodColor, 1.6f, 0.9f, 0f, transform);
            foreach (var e in FindObjectsByType<EnemyAI>(FindObjectsSortMode.None))       // the bat swarm pulls back and does not bite while the player is choosing
                if (e != null && e != ai && Vector2.Distance(e.transform.position, transform.position) < 9f) e.Held = true;
            yield return new WaitForSeconds(0.6f);

            // camera pushes in between Ren and the boss
            cam = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            camSize = Camera.main != null ? Camera.main.orthographicSize : 3f;
            if (cam != null)
            {
                camSharp = cam.focusSharpness;
                cam.focusSharpness = 2.2f;
                Vector3 mid = Player != null ? Vector3.Lerp(Player.position, transform.position, 0.6f) : transform.position;
                cam.Focus(mid + new Vector3(0f, 0.25f, 0f), camSize * 0.78f);
            }
            if (Player != null) { var pc = Player.GetComponent<PlayerCombat>(); if (pc != null) pc.FacingOverride = (Vector2)(transform.position - Player.position); }
            yield return new WaitForSeconds(0.7f);

            // 2. PLEA
            string who = Subtitles.NameOf(transform);
            foreach (var line in pleaLines)
            {
                if (string.IsNullOrEmpty(line)) continue;
                float s = Mathf.Clamp(1.4f + line.Length * 0.045f, 2.2f, 4.6f);
                SpeechBubble.Show(transform.position + new Vector3(0f, 0.95f, 0f), line, s, new Color(1f, 0.55f, 0.6f), who);
                if (glow != null) glow.intensity = 1.2f;
                yield return new WaitForSeconds(s + 0.15f);
            }

            // 3. CHOICE CARD
            Sfx.Play2D("ui_open");
            int pick = -1;
            var opts = new[]
            {
                new ChoiceCard.Option { title = spareTitle, tagline = spareTagline, gains = spareGains, losses = spareLosses, color = new Color(0.75f, 0.9f, 1f) },
                new ChoiceCard.Option { title = killTitle, tagline = killTagline, gains = killGains, losses = killLosses, color = bloodColor },
            };
            yield return ChoiceCard.Ask(cardTitle, cardSub, opts, i => pick = i);
            if (glow != null) Destroy(glow.gameObject);
            if (pick == 0) yield return Spare(pool);
            else yield return Kill(pool);
        }

        private void Update()
        {
            if (resolved || ai == null) return;
            ai.Held = true;                                           // stay kneeling, do not let the boss Think move it
            ai.Desired = Vector2.zero;
        }

        private void OnDied(Health h)
        {
            var dir = MapEncounterDirector.Instance;
            if (dir != null && dir.Flags != null) dir.Flags.Set(slainFlag);
        }

        private void EndScene()
        {
            if (cam != null) { cam.focusSharpness = camSharp; cam.Release(); }
            if (Player != null) { var pc = Player.GetComponent<PlayerCombat>(); if (pc != null) pc.FacingOverride = Vector2.zero; }
            CutsceneState.End();
        }

        // ------------------------------------------------------------------ 3a. THA
        private IEnumerator Spare(SpriteVfx pool)
        {
            resolved = true;
            var dir = MapEncounterDirector.Instance;
            var player = Player;
            string who = Subtitles.NameOf(transform);
            if (player != null) SpeechBubble.Show(player.position + new Vector3(0f, 0.85f, 0f), spareLine, 2.6f, null, Subtitles.NameOf(player)).Follow(player);
            yield return new WaitForSeconds(2.7f);
            SpeechBubble.Show(transform.position + new Vector3(0f, 0.95f, 0f), bossReply, 3.2f, new Color(1f, 0.55f, 0.6f), who);
            yield return new WaitForSeconds(3.3f);
            if (!string.IsNullOrEmpty(giftLine))
            {
                SpeechBubble.Show(transform.position + new Vector3(0f, 0.95f, 0f), giftLine, 3.2f, new Color(1f, 0.55f, 0.6f), who);
                yield return new WaitForSeconds(1.2f);
            }
            if (dir != null && dir.Flags != null) dir.Flags.Set(spareFlag);
            // HAND OVER: the ring leaves her hand and flies into Ren
            if (!string.IsNullOrEmpty(spareKey))
            {
                yield return Fly(spareKey, Chest + new Vector3(0.15f, -0.1f, 0f), new Color(1f, 0.85f, 0.5f), false);
                if (dir != null && dir.Flags != null) KeyRing.Give(dir.Flags, spareKey, spareKeyText);
            }
            yield return new WaitForSeconds(1.6f);
            Hints.Show("Mercy shown. The Vale will remember it.", 3.5f);
            // turn into bats
            Sfx.Play("bat_swarm", transform.position);
            SpriteVfx.Spawn(spareVfx, transform.position + new Vector3(0f, 0.3f, 0f), 0.9f, Layer, Order + 2, false, 14f);
            BossJuice.Flash(Chest, bloodColor, 1.8f, 1.2f, 0.5f);
            foreach (var e in FindObjectsByType<EnemyAI>(FindObjectsSortMode.None))          // her minions (bats) dissolve with her
                if (e != null && e != ai && Vector2.Distance(e.transform.position, transform.position) < 9f) Destroy(e.gameObject);
            if (pool != null) pool.Stop();
            EndScene();
            hp.Died -= OnDied;
            Destroy(gameObject, 0.15f);                              // area enemy counter null = all gone -> area cleared, same as defeating the boss
        }

        // ------------------------------------------------------------------ 3b. KILL
        private IEnumerator Kill(SpriteVfx pool)
        {
            resolved = true;
            var player = Player;
            SpeechBubble.Show(transform.position + new Vector3(0f, 0.95f, 0f), killBossLine, 3f, new Color(1f, 0.55f, 0.6f), Subtitles.NameOf(transform));
            yield return new WaitForSeconds(3.1f);
            // Ren strikes: dash in close, white flash + hit-stop + blood
            if (player != null)
            {
                Vector2 side = player.position.x <= transform.position.x ? Vector2.left : Vector2.right;
                Vector2 to = (Vector2)transform.position + side * 0.45f;
                var rb = player.GetComponent<Rigidbody2D>();
                Vector2 from = player.position;
                for (float t = 0f; t < 0.14f; t += Time.deltaTime)
                {
                    Vector2 p = Vector2.Lerp(from, to, t / 0.14f);
                    if (rb != null) rb.position = p; else player.position = new Vector3(p.x, p.y, player.position.z);
                    yield return null;
                }
                var pc = player.GetComponent<PlayerCombat>();
                if (pc != null) pc.FacingOverride = (Vector2)(transform.position - player.position);
            }
            Sfx.Play("blood_splat", transform.position);
            BossJuice.Impact(Chest, Color.white, 1.4f, 3, Layer, Order + 3);
            BossJuice.Burst("m3x_blood_burst", transform.position + new Vector3(0f, 0.3f, 0f), 1.6f, Color.white, Layer, Order + 2, 14f);
            BossJuice.Stop(0.12f);
            hp.Invulnerable = false;
            hp.Floor = 0f;
            hp.TakeDamage(hp.Current + 10f);
            if (pool != null) pool.Looping(4f);
            yield return new WaitForSeconds(1.4f);
            // DROP: the blood vial rises from the corpse and flies into Ren's chest
            if (!string.IsNullOrEmpty(killReward))
            {
                yield return Fly(killReward, transform.position + new Vector3(0f, 0.2f, 0f), bloodColor, true);
                bool grew = killFlaskSlots <= 0;                                                       // already 5 slots -> just refill the flask
                if (player != null && killFlaskSlots > 0)
                {
                    var pc = player.GetComponent<PlayerCombat>();
                    if (pc != null) { int b0 = pc.maxPotions; pc.AddFlaskSlots(killFlaskSlots); grew = pc.maxPotions > b0; if (!grew) pc.RefillPotions(); }
                }
                var dir = MapEncounterDirector.Instance;
                if (dir != null && dir.Flags != null) dir.Flags.Set(killReward);
                Sfx.Play2D("pickup_item");
                Hints.Show(grew ? killRewardText : "Obtained: Vey Heartblood. Your flask belt is already full - it fills every flask.", 4.5f);
            }
            EndScene();
        }

        /// <summary>The real item (image pickup_&lt;id&gt;) rises at `from`, hovers and glows, then flies into Ren's chest (trail + light + glow ring).</summary>
        private IEnumerator Fly(string id, Vector3 from, Color col, bool rise)
        {
            var player = Player;
            var tex = Resources.Load<Texture2D>("VFX/props/pickup_" + id);
            var sprs = Resources.LoadAll<Sprite>("VFX/props/pickup_" + id);
            Sprite s = sprs.Length > 0 ? sprs[0] : tex != null ? Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f) : null;
            var go = new GameObject("Reward " + id);
            go.transform.position = from;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s;
            sr.sortingLayerID = Layer; sr.sortingOrder = Order + 8;
            go.transform.localScale = Vector3.one * 1.6f;                  // item drawn large and clear in the scene (14-16 px image)
            var l = BossJuice.Light(from, col, 1.3f, 0f, 0f, go.transform);
            Sfx.Play2D("relic_absorb");
            Vector3 up = from + (rise ? new Vector3(0f, 0.55f, 0f) : new Vector3(0f, 0.25f, 0f));
            for (float t = 0f; t < 1.1f; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / 1.1f);
                go.transform.position = Vector3.Lerp(from, up, k) + new Vector3(0f, 0.03f * Mathf.Sin(t * 6f), 0f);
                sr.color = new Color(1f, 1f, 1f, k);
                if (l != null) l.intensity = 1.6f * k;
                yield return null;
            }
            BossJuice.Burst("bfx_flash", up, 0.6f, Color.Lerp(col, Color.white, 0.5f), Layer, Order + 9, 20f);
            yield return new WaitForSeconds(0.7f);
            Vector3 a = go.transform.position;
            for (float t = 0f; t < 0.55f && player != null; t += Time.deltaTime)
            {
                float k = t / 0.55f;
                Vector3 b = player.position + new Vector3(0f, 0.35f, 0f);
                go.transform.position = Vector3.Lerp(a, b, k * k) + new Vector3(0f, 0.25f * Mathf.Sin(k * Mathf.PI), 0f);
                go.transform.localScale = Vector3.one * Mathf.Lerp(1.6f, 0.6f, k);
                yield return null;
            }
            if (player != null)
            {
                BossJuice.Impact(player.position + new Vector3(0f, 0.35f, 0f), col, 0.8f, 1, Layer, Order + 9);   // reaches Ren's chest: flash
            }
            Destroy(go);
        }
    }
}
