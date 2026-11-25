using System.Collections;
using System.Collections.Generic;
using SuperTiled2Unity;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// THE REGISTRAR — keeper of the Choir's Book of Names (Map 4). A SURVIVAL fight: "the author makes the player's life hard".
    ///   Setup: clear the Scriptorium -> the Registrar appears, points, and lifts Ren into THE MARGIN (own scene Map4m_TheMargin):
    ///          one ledger page floating in the void.
    ///   Rules: he CANNOT be hit. Ren must survive 3 TIMED PHASES (First Draft / Revisions / Final Draft, 45 / 50 / 55 s);
    ///          HUD board "PHASE n/3" + "PAGE x%" bar like the Vessel fight. The boss HP bar = what is left of the ledger.
    ///          After phase III the ink runs dry, he collapses before the lectern -> ONE finishing blow (invulnerability off, 1 HP left).
    ///   Dying mid-fight -> replay from the START of the current phase (ResumeChapter = phase * 3).
    ///   Moves (all telegraphed, all dodgeable; picked at random per phase — see .Phases.cs):
    ///     Ledger Maws   — books fly to the page edge, open their paper-tooth maws and spit a violet beam: aimed / per lane / sweeping (roll through).
    ///     Summon        — he SKETCHES 2 monsters on the page; finished sketches become real enemies (Skeleton / Mage_Skeleton). Struck out at phase end.
    ///     Quill Stab    — a giant floating quill; its shadow follows Ren, locks, then the quill stabs through the paper — 3 times.
    ///     Cranes        — books at the page edge release paper cranes that home in briefly then veer off; can be cut down.
    ///     Saws          — sketches 2 saw blades -> real spinning blades that aim, charge Ren and bounce off the page edges.
    ///     Eraser        — an upright eraser scrubs zig-zag across Ren's row 3 times; it also erases his own monsters / cranes / ink pools.
    ///     Page Fold     — a crease appears, Ren's half darkens, the other half flips over and slams down (phase III folds twice).
    ///     Tease         — he melts into ink, reappears mid-page to taunt Ren, glides around, then returns to the lectern.
    ///     Ink Pots      — ink pots lobbed in an arc onto a target ring; they burst into slowing puddles.
    ///     The Grip      — a violet sigil tracks Ren then locks: still inside -> lifted and thrown to the page edge; the floor shadow marks the
    ///                     landing, ROLL as you touch down (white ring flash) = safe landing, otherwise take damage.
    ///     Erasure       — tears off the page margin (phase II / III): that strip becomes void.
    ///     Written Backwards — inverts movement keys for a few seconds (phase III).
    ///   Files: RegistrarSkills.cs (lifecycle, camera, finale), .Phases.cs (3 phases, move picker, HUD board), .Attacks.cs (older moves),
    ///          .Grip.cs (lift and throw), .Sketch.cs (Summon, Quill Stab), .Craft.cs (Cranes, Saws, Eraser), .Fold.cs, .Tease.cs.
    ///   Called by: EnemyAI (Kind = Custom) via IEnemySkills. Related: MapEncounterDirector (spawn), SceneMusic (boss_registrar), DrillBoard (HUD).
    /// </summary>
    public sealed partial class RegistrarSkills : BossSkillsBase
    {
        public float upgradeSeconds = 2.5f;          // (kept for the old builder) length of the Upgrade animation
        [Header("The Margin")]
        public float repelRadius = 1.05f;            // Ren inside this radius (from his chest) -> gets grabbed and thrown
        public float repelForce = 10f;
        public float beamDamage = 14f, stabDamage = 16f, potDamage = 10f, spikeDamage = 20f, eraseDamage = 6f;

        /// <summary>Chapter to start from when the fight restarts (start of the unfinished phase). Reset to 0 when he is defeated.</summary>
        public static int ResumeChapter;

        private static readonly string[] ActTitle = { "FIRST DRAFT", "REVISIONS", "FINAL DRAFT" };

        private Transform root;                       // everything this fight creates (cleared when the boss is gone / Ren dies)
        private Rect page, play, safe;                // page (world) / the part Ren plays on (minus the lectern strip) / what is left after the margin is erased
        private Vector2 home;
        private Health pHp;
        private PlayerCombat pCombat;
        private PlayerController pCtrl;
        private CameraFollow cam;
        private int pOrder;
        private int chapter;
        private bool bookStarted, collapsed, finished, camHeld;
        private float nextRepel, nextHitBark;
        private int pending;
        private static bool taughtGrip, taughtSweep;

        // ------------------------------------------------------------------ lifecycle
        private void Start()
        {
            var e = GetComponent<EnemyAI>();
            if (e != null) { e.aggroRange = 14f; e.leashRange = 0f; e.moveSpeed = 0f; }   // sees Ren anywhere on the page, never walks
        }

        protected override void OnEngage()
        {
            edgeTimeMax = pinnedTimeMax = stuckTimeMax = 9999f;   // no anti-stuck logic from the shared base
            home = Pos;
            hp.Invulnerable = true;
            ai.HideBossBar = true;                                   // only the PHASE / PAGE board at the top is used
            pOrder = Order;
            cam = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            root = new GameObject("[Registrar Margin]").transform;
            FindPage();
            chapter = Mathf.Clamp(ResumeChapter / 3, 0, 2) * 3;              // start of the unfinished phase (ResumeChapter = phase * 3)
            StartCoroutine(Book());
        }

        /// <summary>The page = object "margin_page" (mechanics layer in Tiled). Missing -> use arenaRadii around the spawn point.</summary>
        private void FindPage()
        {
            page = new Rect(home.x - 3.2f, home.y - 3.45f, 6.4f, 4.15f);
            var dir = MapEncounterDirector.Instance;
            if (dir != null && dir.mapRoot != null)
                foreach (var so in dir.mapRoot.GetComponentsInChildren<SuperObject>(true))
                {
                    if (so.m_TiledName != "margin_page") continue;
                    Vector3 a = dir.TileToWorld(so.m_X / 32f, so.m_Y / 32f), b = dir.TileToWorld((so.m_X + so.m_Width) / 32f, (so.m_Y + so.m_Height) / 32f);
                    page = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
                    break;
                }
            // the top strip (ledger lectern, candles) is not playable
            float top = Mathf.Min(page.yMax - 0.2f, home.y - 0.25f);
            play = Rect.MinMaxRect(page.xMin + 0.18f, page.yMin + 0.16f, page.xMax - 0.18f, top);
            safe = play;
        }

        protected override int PhaseFor(float hpPercent) => 1;       // cooldown handled by Book()
        protected override IEnumerator Reposition() { yield break; }

        protected override IEnumerator Think()
        {
            ai.Desired = Vector2.zero;
            if (!busyAnim) ai.PlayState("Idle", false);
            ai.FaceX((PlayerPos - Pos).x);
            yield return ai.WaitFor(0.2f);
        }
        private bool busyAnim;

        protected override void Update()
        {
            if (ai == null || !engaged || hp == null) return;
            if (hp.IsDead) { ReleaseCamera(); return; }
            if (!bookStarted) return;
            HoldCamera();
            ProximityGrab();                                         // replaces Repel (knock-away)
            TickPhase();                                             // phase timer + HUD board
        }

        private void OnDestroy() => CleanUp();
        protected override void OnDisable() { base.OnDisable(); CleanUp(); }

        private void CleanUp()
        {
            if (PlayerFlask.Stolen == (System.Action<PlayerCombat>)OnFlaskStolen) PlayerFlask.Stolen = null;
            EndGrip();
            if (GameInput.InvertedUntil > 0f) GameInput.InvertedUntil = 0f;
            ReleaseCamera();
            HideBoard();
            if (root != null) Destroy(root.gameObject);
            root = null;
        }

        // ------------------------------------------------------------------ time pauses during cutscenes
        private bool Halt => hp == null || hp.IsDead || ai == null || ai.Player == null || (pHp != null && pHp.IsDead);
        private static bool Frozen => CutsceneState.Active || SceneTransition.Busy || DialogueBox.Active;
        private static float Dt => Frozen ? 0f : Time.deltaTime;

        private IEnumerator W(float s)
        {
            float t = 0f;
            while (t < s && !Halt) { t += Dt; yield return null; }
        }

        /// <summary>Run several moves in parallel and wait for all of them.</summary>
        /// Waits only for ITS OWN jobs (private counter). Waiting on the shared `pending` made nested Par calls (Par(Crows(...)) where Crows
        /// calls Par again) wait for themselves -> stuck forever, the chapter / phase never advanced.
        private IEnumerator Par(params IEnumerator[] jobs)
        {
            var left = new int[1];
            foreach (var j in jobs) if (j != null) { left[0]++; pending++; StartCoroutine(TrackLocal(j, left)); }
            while (left[0] > 0 && !Halt) yield return null;
        }
        private IEnumerator TrackLocal(IEnumerator j, int[] left)
        {
            try { yield return j; }
            finally { left[0]--; pending = Mathf.Max(0, pending - 1); }
        }
        private IEnumerator Track(IEnumerator j) { try { yield return j; } finally { pending = Mathf.Max(0, pending - 1); } }
        /// <summary>Run in the background (no wait) — still counted so the chapter waits for it to finish.</summary>
        private void RunBg(IEnumerator j) { pending++; StartCoroutine(Track(j)); }
        private IEnumerator Drain() { while (pending > 0 && !Halt) yield return null; }

        private IEnumerator ActCard(int a)
        {
            busyAnim = true;
            ai.PlayState("Upgrade");
            Sfx.Play("cs_page", Pos, 0.9f);
            SpriteVfx.Spawn("m3_cast_hymn", Pos + Vector2.up * 1.2f, 1.4f, Layer, Order + 2, false, 14f);
            DrillBoard.Finish(ActTitle[a], MarginArt.Violet);
            string line = a == 0 ? Pick("Let us begin with a rough draft of you.", "Chapter One. The Candidate arrives, unwelcome.")
                        : a == 1 ? Pick("First drafts are always kind. Revisions are not.", "Too many survivors on that page. Let me revise.")
                        : Pick("Enough drafts. This one is final.", "The last act. I always know how it ends.");
            Say(line, 3f);
            yield return W(upgradeSeconds);
            ai.PlayState("Idle");
            busyAnim = false;
        }

        /// <summary>Turn the page: a dark band sweeps across, clears everything on the floor, erased margins come back; sometimes he moves along the top edge.</summary>
        private IEnumerator PageTurn()
        {
            ClearHazards();
            Sfx.Play("cs_page", page.center, 1f);
            var band = Make("page turn", MarginArt.Pixel(), new Vector2(page.xMax, page.center.y), 30);
            band.color = new Color(0.04f, 0.03f, 0.07f, 0.85f);
            band.transform.localScale = new Vector3(70f, (page.height + 1.2f) * 100f, 1f);
            float t = 0f;
            while (t < 0.7f && !Halt)
            {
                t += Dt;
                float u = t / 0.7f;
                band.transform.position = new Vector3(Mathf.Lerp(page.xMax + 0.4f, page.xMin - 0.4f, u * u * (3f - 2f * u)), page.center.y, 0f);
                yield return null;
            }
            if (band != null) Destroy(band.gameObject);
            safe = play;
            if (Random.value < 0.5f) yield return Blink();
            yield return W(0.5f);
        }

        /// <summary>Melt into ink and reappear elsewhere along the top edge (behind / beside the lectern).</summary>
        private IEnumerator Blink()
        {
            Vector2 to = home + new Vector2(Random.Range(-1, 2) * 1.6f, 0f);
            if ((to - Pos).sqrMagnitude < 0.04f) yield break;
            SpriteVfx.Spawn("m3_vespers", Pos + Vector2.up * 0.6f, 1.1f, Layer, Order + 1, false, 16f);
            Sfx.Play("teleport_dark", Pos, 0.7f);
            yield return Fade(0f, 0.25f);
            ai.TeleportTo(to);
            SpriteVfx.Spawn("m3_vespers", Pos + Vector2.up * 0.6f, 1.1f, Layer, Order + 1, false, 16f);
            yield return Fade(1f, 0.2f);
        }

        // ----------------------------------------------------------------- stolen flask
        /// <summary>Ren drinks a flask in the Margin: the flask is spent but Ren does NOT heal; ink flies from Ren to him and the current phase
        /// gets `flaskPenalty` seconds longer (the PAGE bar moves back). Once he has collapsed (finale) it only steals.
        /// Hooked through PlayerFlask.Stolen.</summary>
        private void OnFlaskStolen(PlayerCombat who)
        {
            if (hp == null || hp.IsDead) return;
            Vector2 from = who != null ? (Vector2)who.transform.position + Vector2.up * 0.3f : PlayerBody;
            Sfx.Play("life_drain", from, 0.9f);
            for (int i = 0; i < 5; i++) StartCoroutine(FlyMote("m3_hymn_orb", from + Random.insideUnitCircle * 0.2f, transform, Vector2.up * 1.2f, 0.55f + i * 0.08f, 0.5f));
            var fx = SpriteVfx.Spawn("heal_back", Pos, 1.6f, Layer, Order - 1, false, 14f);
            if (fx != null) fx.Tint(new Color(0.75f, 0.5f, 1f));
            if (collapsed)
            {
                return;
            }
            PhasePenalty();
        }

        // ------------------------------------------------------------------ camera
        /// <summary>Camera size for the Margin fight (much closer than the old whole-page framing, ~2.5).</summary>
        private const float MarginCamSize = 1.75f;

        /// <summary>
        /// Camera follows Ren closely and leans slightly toward the page centre (so attacks coming from the middle are visible),
        /// never more than 0.35 u past the page edge; the top edge reaches where he stands. Called every frame (Focus target updates continuously).
        /// </summary>
        private void HoldCamera()
        {
            if (cam == null || collapsed && finished) return;
            if (Frozen) return;
            float aspect = Camera.main != null ? Camera.main.aspect : 16f / 9f;
            float size = MarginCamSize, halfW = size * aspect;
            Vector2 p = PlayerFeet;
            float x = Mathf.Lerp(page.center.x, p.x, 0.6f);
            float y = Mathf.Lerp(page.center.y, p.y, 0.75f) + 0.25f;
            float x0 = page.xMin - 0.35f + halfW, x1 = page.xMax + 0.35f - halfW;
            x = x0 > x1 ? page.center.x : Mathf.Clamp(x, x0, x1);
            float top = Mathf.Max(page.yMax, Pos.y + 2.3f) + 0.15f, bottom = page.yMin - 0.35f;
            float y0 = bottom + size, y1 = top - size;
            y = y0 > y1 ? (top + bottom) * 0.5f : Mathf.Clamp(y, y0, y1);
            cam.Focus(new Vector3(x, y, 0f), size);
            camHeld = true;
        }

        private void ReleaseCamera()
        {
            if (!camHeld) return;
            camHeld = false;
            if (cam != null) cam.Release();
        }

        // ------------------------------------------------------------------ finale: ink runs dry, he collapses -> one blow
        private IEnumerator Collapse()
        {
            collapsed = true;
            HideBoard();
            ClearHazards();
            GameInput.InvertedUntil = 0f;
            busyAnim = true;
            ai.PlayState("Attack");
            Say("No. No, I have not finished your—", 2.4f);
            yield return W(1.2f);
            Sfx.Play("glass_break", Pos + Vector2.up * 1.2f, 0.9f);
            Sfx.Play("registrar_quill", Pos, 1f);
            SpriteVfx.Spawn("m3_break", Pos + Vector2.up * 1.3f, 1.2f, Layer, Order + 3, false, 14f);
            Kick(Vector2.down, 0.08f, 0.3f);
            yield return W(0.6f);
            // slides off the lectern and kneels in front of it
            Vector2 from = Pos, to = new Vector2(Pos.x, Mathf.Max(play.yMin + 0.6f, Pos.y - 0.95f));
            if (ai.Body != null) ai.Body.color = new Color(0.62f, 0.56f, 0.72f, 1f);
            for (float t = 0f; t < 0.5f && !Halt; t += Dt)
            {
                ai.TeleportTo(Vector2.Lerp(from, to, t / 0.5f));
                yield return null;
            }
            ai.TeleportTo(to);
            Sfx.Play("body_fall", to, 0.9f);
            SpriteVfx.Spawn("m3_dash_dust", to, 1.2f, Layer, Order - 1, false, 16f, false);
            ai.PlayState("Idle");
            hp.Invulnerable = false;
            hp.DamageTakenMul = 1f;
            hp.SetCurrent(1f);
            ResumeChapter = 0;
            Hints.Show("The ink has run dry. Strike him from his own book.", 6f);
            Say("...the ink... it was never... enough...", 3f);
            while (!Halt) yield return null;                         // no repeated nagging line (fewer speech bubbles)
            finished = true;
        }

        // ------------------------------------------------------------------ helpers
        private Vector2 PlayerFeet => PlayerPos;
        private Vector2 PlayerBody => PlayerPos + new Vector2(0f, 0.14f);
        private Vector2 Hand => Pos + new Vector2(0f, 1.5f);

        private static string Pick(params string[] s) => s[Random.Range(0, s.Length)];

        private void Say(string text, float seconds)
        {
            SpeechBubble.Show((Vector3)(Pos + Vector2.up * 2.45f), text, seconds, new Color(0.82f, 0.72f, 1f), "The Registrar");
        }

        /// <summary>Damage Ren (projectiles / spells ignore blocking; trap = rolling does not avoid it either).</summary>
        private bool HurtPlayer(float dmg, Vector2 dir, float knock, bool trap = false)
        {
            if (pHp == null || pHp.IsDead || gripping || collapsed) return false;
            bool hit;
            Health.IncomingRanged = true; Health.TrapIgnoresRoll = trap;
            try { hit = pHp.TakeDamage(dmg, dir, knock); }
            finally { Health.IncomingRanged = false; Health.TrapIgnoresRoll = false; }
            if (hit && Time.time >= nextHitBark && Random.value < 0.45f)
            {
                nextHitBark = Time.time + 4f;
            }
            return hit;
        }

        /// <summary>New unlit sprite under root. orderOffset is relative to Ren's sorting order (negative = under the feet, positive = above the head).</summary>
        private SpriteRenderer Make(string name, Sprite s, Vector2 at, int orderOffset) => Make(name, s, at, orderOffset, false);

        /// <summary>Like Make but LIT by the scene (paper, torn edges — so they match the darkness of the page background).</summary>
        private SpriteRenderer MakeLit(string name, Sprite s, Vector2 at, int orderOffset) => Make(name, s, at, orderOffset, true);

        private SpriteRenderer Make(string name, Sprite s, Vector2 at, int orderOffset, bool lit)
        {
            var go = new GameObject(name);
            if (root != null) go.transform.SetParent(root, false);
            go.transform.position = new Vector3(at.x, at.y, 0f);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = s;
            r.sortingLayerID = Layer;
            r.sortingOrder = pOrder + orderOffset;
            if (!lit) FxLighting.MakeUnlit(r);
            return r;
        }

        /// <summary>Clear everything on the page (page turn / end of fight). In-flight moves end on their own because their objects are gone.</summary>
        private void ClearHazards()
        {
            if (root != null)
                for (int i = root.childCount - 1; i >= 0; i--) Destroy(root.GetChild(i).gameObject);
            puddles.Clear();
            erased.Clear();
            safe = play;
        }

        private Vector2 RandomIn(Rect r, float inset) =>
            new Vector2(Random.Range(r.xMin + inset, r.xMax - inset), Random.Range(r.yMin + inset, r.yMax - inset));

        private Vector2 ClampSafe(Vector2 p, float inset = 0.2f) =>
            new Vector2(Mathf.Clamp(p.x, safe.xMin + inset, safe.xMax - inset), Mathf.Clamp(p.y, safe.yMin + inset, safe.yMax - inset));
    }
}
