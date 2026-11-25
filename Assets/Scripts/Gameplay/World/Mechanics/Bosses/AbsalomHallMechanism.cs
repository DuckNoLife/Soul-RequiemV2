using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// absalom_hall — FINAL BOSS of Map 4, fought in the Hall of Naming (design doc: "Hall of Naming — Absalom & The Crystal Vessel").
    /// Absalom never takes damage (the Ascended's left hand shields him — blue halo); Ren clears each phase through its own mechanic:
    ///   Phase I  THE CONGREGATION (FlockPhase, .Congregation.cs) — 3 waves of cultists step out of the side alcoves (`alcoves`);
    ///            one cultist per wave is blessed (golden ward, breaks after 3 hits). Absalom only summons and blesses, he does not attack.
    ///   Phase II THE HIEROPHANT (LampPhase, .Lamps.cs) — break the 4 Votive Lamps (tile objects with `role` lamp); a lamp only takes hits
    ///            while its tether is dim, i.e. right after Absalom casts that lamp's element.
    /// After phase II -> PortalPull (.Portal.cs) drags Ren through a violet portal into THE UNWRITTEN (own scene, VesselDomainMechanism = last phase).
    /// The Still Hour (.TimeStop.cs): 1 s warning -> greyscale freeze, Ren frozen, Absalom places bullets with path markers, 3-2-1 countdown -> all resume.
    /// Open hall with a large floor seal (abs_floor_seal, flares during time stop); Absalom hovers in the upper half of the hall.
    /// Skill files: .Skills (shared kit: Cast, hazards, warning rings), .Light, .Fire, .Frost, .Storm, .Umbral, .Signature (big moves),
    /// .Intro (opening cutscene), .Guide (HUD hints).
    /// Checkpoint: flag absalom_pN when phase N is cleared; on death the fight resets, Ren wakes at full HP and entering the hall replays the CURRENT phase.
    /// Tiled properties: home ("x,y" tile — spot before the altar), dais ("x0,y0,x1,y1" tiles — his movement area), alcoves ("x,y;x,y…" cultist spawns),
    /// seal_gate (gate locked behind Ren, default hall_door), cam_size. Object box = hall floor (the camera stays inside it).
    /// Created from the Tiled mechanism object (Mechanism base). Calls: DrillBoard (HUD board), SceneMusic (via Fighting), PlayerCombat (Struck, flasks).
    /// </summary>
    public sealed partial class AbsalomHallMechanism : Mechanism
    {
        private enum State { Waiting, Intro, Fight, Rest }
        private State stateRaw = State.Waiting;
        /// <summary>Every state change also updates SceneMusic (Fight = Absalom boss track).</summary>
        private State state { get => stateRaw; set { stateRaw = value; InFight = value == State.Fight; } }
        /// <summary>True while Absalom is fighting in the Hall of Naming (read by SceneMusic; reset to false on scene load).</summary>
        internal static bool InFight;
        private AbsalomActor abs;
        private Vector2 home;
        private Rect dais;
        private Coroutine main;
        private readonly List<Coroutine> routines = new List<Coroutine>();
        private bool aborted, sceneOwned, camOwned, sealedIn, frozenRen;
        private float readyAt, nextLine;
        /// <summary>Height forced by a skill (&lt; 0 = free hover), extra height while charging, and whether he is up in the sky (off screen).</summary>
        private float liftOverride = -1f, liftBoost;
        private bool airHidden;
        private int curPhase;
        private const int FlockPhase = 1, LampPhase = 2;                              // phase I = cultists, phase II = lamps
        private int layer, order;
        private PlayerCombat combat;

        public const string MetFlag = "absalom_met";
        /// <summary>Number of phases fought in this hall (I + II); the last phase lives in The Unwritten.</summary>
        private const int BuiltPhases = 2;
        private static readonly string[] PhaseNames = { "THE CONGREGATION", "THE HIEROPHANT", "THE LITANY OF NAMES", "THE ASCENSION" };
        private static readonly string[] ShortNames = { "CONGREGATION", "HIEROPHANT", "LITANY", "ASCENSION" };
        private static readonly string[] Roman = { "I", "II", "III", "IV" };
        private static readonly Color Gold = new Color(1f, 0.85f, 0.45f), Azure = new Color(0.6f, 0.82f, 1f),
                                      Red = new Color(1f, 0.4f, 0.35f), Green = new Color(0.55f, 1f, 0.6f);

        private Rect A => Rect;
        private int Layer => layer;
        private int Order => order;
        private Vector2 PlayerFeet => Ctx.Player != null ? (Vector2)Ctx.Player.position : A.center;
        private static string PhaseFlag(int p) => "absalom_p" + p;

        private int NextPhase()
        {
            for (int p = 1; p <= 4; p++) if (!Ctx.Flags.HasAll(PhaseFlag(p))) return p;
            return 5;
        }

        public override void Init()
        {
            var psr = Ctx.Player != null ? Ctx.Player.GetComponentInChildren<SpriteRenderer>() : null;
            layer = psr != null ? psr.sortingLayerID : 0;
            order = psr != null ? psr.sortingOrder : 2;
            home = Ctx.TilePoint(Str("home", ""), A.center);
            dais = TileRect(Str("dais", ""), new Rect(home.x - 1.4f, home.y, 2.8f, 1.8f));
            abs = new AbsalomActor(home, Ctx.Host.transform, Ctx.Player) { Alpha = 0f };
            InitSeal();
            abs.Face(Vector2.up);                                                      // back turned, facing the altar — until Ren walks in
            abs.Size = SmallSize;                                                      // Ren-sized until the transformation
            combat = Ctx.Player != null ? Ctx.Player.GetComponent<PlayerCombat>() : null;
            InitLamps();
            InitAlcoves();
            PlayerCombat.Struck += OnStruck;
        }

        /// <summary>"x0,y0,x1,y1" (Tiled tiles) -> world rect.</summary>
        private Rect TileRect(string s, Rect fallback)
        {
            var p = s.Split(',');
            if (p.Length != 4) return fallback;
            Vector2 a = Ctx.TilePoint(p[0].Trim() + "," + p[1].Trim(), fallback.min), b = Ctx.TilePoint(p[2].Trim() + "," + p[3].Trim(), fallback.max);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        // ------------------------------------------------------------------ per frame
        public override void Animate()
        {
            float dt = Time.deltaTime;
            Vector2 feet = PlayerFeet;
            bool inside = A.Contains(feet);
            bool near = new Rect(A.xMin - 1f, A.yMin - 3f, A.width + 2f, A.height + 3f).Contains(feet);
            if (abs != null)
            {
                abs.Alpha = airHidden ? 0f : Mathf.MoveTowards(abs.Alpha, near || state != State.Waiting ? 1f : 0f, dt * 2f);
                // Real hover once the fight starts — hover / cast frames, 0.42 u high (+ liftBoost while charging big moves), gentle bob; floor shadow follows height.
                // Skills can drive the height directly (Ascension Dive) through liftOverride.
                bool fly = state == State.Fight || (state == State.Intro && !introGround);   // stands on the ground in the intro until the transformation
                abs.Airborne = fly;
                if (liftOverride >= 0f) abs.Lift = liftOverride;
                else
                {
                    float lift = fly ? 0.42f + liftBoost + 0.05f * Mathf.Sin(Time.time * 2.1f) : 0f;
                    abs.Lift = Mathf.MoveTowards(abs.Lift, lift, dt * (fly ? 1.2f : 0.8f));
                }
                AnimateSize(dt);                                                        // Ren-sized outside the fight, boss-sized inside it
                abs.Tick();
            }
            AnimateLamps(dt);
            AnimateSeal(dt);
            UpdateGuide();                                                              // HUD quest board for the fight
            if (SceneTransition.Busy) return;
            var hp = Ctx.PlayerHealth;
            if (state == State.Intro || state == State.Fight)
            {
                if (hp == null || hp.IsDead) { Abort(); return; }
                if (!new Rect(A.xMin - 1.5f, A.yMin - 2.5f, A.width + 3f, A.height + 4f).Contains(feet)) { Abort(); return; }   // teleported out (test key)
                if (!camDrive) ArenaCam(true);                                          // the transformation scene drives the camera itself
                TrackStill(dt, feet);
                TickHazards(dt);
                PullRen(dt);
                TrackRenVelocity(dt);                                                   // for lead shots / grabs
                return;
            }
            ArenaCam(false);
            if (state == State.Waiting && inside && feet.y > A.yMin + 0.5f && hp != null && !hp.IsDead && Time.time >= readyAt
                && !CutsceneState.Active && !DialogueBox.Active && Ctx.Director != null && !Ctx.Director.Respawning
                && NextPhase() <= BuiltPhases + 1)                                       // phase II cleared but the domain is not -> portal scene
                main = Ctx.Run(Encounter());
        }

        // ------------------------------------------------------------------ fight
        private IEnumerator Encounter()
        {
            aborted = false;
            state = State.Intro;
            Seal(true);
            int p = NextPhase();
            bool toDomain = p > BuiltPhases;                                           // back in the hall after phase II -> straight to the portal scene
            yield return null;
            if (!toDomain)
            {
                if (!Ctx.Flags.HasAll(MetFlag)) yield return IntroScene();
                else yield return ShortIntro();
            }
            if (aborted) yield break;
            Ctx.Flags.Set(MetFlag);
            if (toDomain) { yield return PortalPull(true); yield break; }
            state = State.Fight;
            while (!aborted && p <= BuiltPhases)
            {
                BeginBoard(p);
                DrillBoard.Finish(Roman[p - 1] + ". " + PhaseNames[p - 1], Gold);
                Sfx.Play("bell_toll", A.center, 0.8f);
                yield return Wait(1.8f);
                if (aborted) yield break;
                curPhase = p;
                skipPhase = false;
                if (p == FlockPhase) yield return RunCongregationPhase();               // phase I = cultists (.Congregation.cs)
                else if (p == LampPhase) yield return RunLampPhase();                   // phase II = lamps (.Lamps.cs)
                if (aborted) yield break;
                ClearField();
                Ctx.Flags.Set(PhaseFlag(p));
                yield return HaloBreak(p);
                p++;
            }
            if (aborted) yield break;
            yield return PortalPull(false);                                            // after phase II -> the violet portal pulls Ren into The Unwritten
        }

        // IntroScene / ShortIntro / Ascension -> AbsalomHallMechanism.Intro.cs (transformation cutscene).

        /// <summary>Phase cleared: the halo loses a piece (HUD HALO) and Absalom says one line.</summary>
        private IEnumerator HaloBreak(int p)
        {
            DrillBoard.Morale = Mathf.Max(0, 4 - p);
            DrillBoard.MoraleLostAt = Time.time;
            DrillBoard.Finish("THE HALO CRACKS", Azure);
            abs.Flash(Azure, 0.8f);
            Sfx.Play("ward_break", abs.Pos, 0.9f);
            Sfx.Play("glass_break", abs.Pos, 0.6f);
            SpriteVfx.Spawn("duo_ice_burst", abs.Body, 0.9f, Layer, Order + 3, false, 16f, false);
            CameraFollow.KickMain(Vector2.down, 0.08f, 0.3f);
            Bark(p == FlockPhase ? "The faithful fall. Then the lamps will answer for them." : "...Enough. Let the Litany weigh what is left of you.", 3f);
            RefillFlasks();
            yield return Wait(3f);
        }

        /// <summary>Each cleared phase refills the flasks up to Ren's current slots (3 + slots found, max PlayerCombat.MaxFlaskSlots).</summary>
        private void RefillFlasks()
        {
            if (combat == null) return;
            int before = combat.Potions;
            combat.RefillPotions();
            int got = combat.Potions - before;
            Hints.Show(got > 0 ? $"The halo's light fills your flasks: {combat.Potions}/{combat.maxPotions}." : $"Flasks full: {combat.Potions}/{combat.maxPotions}.", 3f);
            if (Ctx.Player != null)
            {
                Sfx.Play("mantra_ichorflask", Ctx.Player.position, 0.55f);
                SpriteVfx.Spawn("duo_ice_burst", (Vector2)Ctx.Player.position + Vector2.up * 0.35f, 0.45f, Layer, Order + 3, false, 16f, false);
            }
        }

        /// <summary>Phase not implemented in this hall — open the gate and say so clearly.</summary>
        private IEnumerator NotBuiltYet()
        {
            state = State.Rest;
            DrillBoard.Objective = "To be continued - the Litany of Names arrives in the next build.";
            Hints.Show("[WIP] Absalom phases 3-5 are the next milestone. Phases 1-2 are done.", 6f);
            yield return Wait(4f);
            DrillBoard.Active = false;
            Seal(false);
            ArenaCam(false);
            yield return Glide(home, 1.4f);
            abs.Face(Vector2.up);
        }

        private void BeginBoard(int p)
        {
            DrillBoard.Begin(p, ShortNames[p - 1], 4);                                  // compact HUD board (640 px): short name
            DrillBoard.Stations = 4;
            DrillBoard.StationLabel = "PHASE";
            DrillBoard.PipLabel = "HALO";
            DrillBoard.ShoutName = "ABSALOM";
            DrillBoard.MoraleMax = 4;
            DrillBoard.Morale = 4 - (p - 1);
        }

        // ------------------------------------------------------------------ reset (death / leaving the hall)
        private void Abort()
        {
            if (state != State.Intro && state != State.Fight) return;
            aborted = true;
            ClearGuide();
            if (main != null) Ctx.Host.StopCoroutine(main);
            main = null;
            StopRoutines();
            ClearField();
            if (sceneOwned) EndScene();
            if (curPhase == LampPhase || NextPhase() == LampPhase) ResetLamps();       // replaying the lamp phase: rebuild the lamps
            DrillBoard.Active = false;
            Seal(false);
            ArenaCam(false);
            abs.Pos = home; abs.Lift = 0f;
            liftOverride = -1f; liftBoost = 0f; airHidden = false;
            introGround = false; sizeDrive = false; camDrive = false; abs.Size = SmallSize;               // back to human size, praying with his back turned like the start
            abs.Airborne = false;
            abs.Play("idle", 5f);
            abs.Face(Vector2.up);
            state = State.Waiting;
            readyAt = Time.time + 3f;
        }

        /// <summary>Clear the arena between phases / on reset: bullets, cultists, time stop, HUD board.</summary>
        private void ClearField()
        {
            StopRoutines();
            HallBullet.ClearAll();
            ClearHazards();
            DespawnCultists();
            RestoreStop();
            DrillBoard.Command = "";
        }

        private void StartR(IEnumerator r) => routines.Add(Ctx.Run(r));

        private void StopRoutines()
        {
            foreach (var c in routines) if (c != null) Ctx.Host.StopCoroutine(c);
            routines.Clear();
        }

        private IEnumerator Wait(float s)
        {
            float end = Time.time + s;
            while (Time.time < end && !aborted) yield return null;
        }

        // ------------------------------------------------------------------ scenes / lines
        private void BeginScene()
        {
            if (sceneOwned) return;
            sceneOwned = true;
            CutsceneState.Begin();
            GameInput.Blocked = true;
            Ctx.SetPlayerControl(false);
        }

        private void EndScene()
        {
            if (!sceneOwned) return;
            sceneOwned = false;
            if (DialogueBox.Active) DialogueBox.Close();
            CutsceneState.End();
            GameInput.Blocked = false;
            if (Ctx.Player != null) Ctx.SetPlayerControl(true);
        }

        private IEnumerator Say(string who, params string[] lines)
        {
            DialogueBox.Open(who, lines, null, 2.4f);
            while (DialogueBox.Active && !aborted) yield return null;
            if (DialogueBox.Active) DialogueBox.Close();
        }

        /// <summary>Absalom speaks: during the fight -> ONE LINE on the HUD board; outside it -> subtitle box.</summary>
        private void Bark(string text, float seconds)
        {
            nextLine = Time.time + seconds * 0.8f;
            if (state == State.Fight && DrillBoard.Active) { DrillBoard.Yell(text, Mathf.Clamp(seconds, 1.8f, 3.4f)); return; }
            var b = SpeechBubble.Show(abs.Pos + Vector2.up * 1.3f, text, seconds, Azure, "Absalom");
            if (b != null && abs.Sr != null) b.Follow(abs.Sr.transform);
        }

        private static string Pick(params string[] s) => s[Random.Range(0, s.Length)];

        // ------------------------------------------------------------------ gate + camera
        private void Seal(bool on)
        {
            if (sealedIn == on || Ctx.Director == null) return;
            if (Ctx.Director.SetGateClosed(Str("seal_gate", "hall_door"), on)) sealedIn = on;
        }

        private const float HudBand = 0.075f;

        /// <summary>Arena camera: must show where bullets come from (required by the time stop).</summary>
        private void ArenaCam(bool on)
        {
            var cf = Ctx.Camera != null ? Ctx.Camera : (Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null);
            if (cf == null) return;
            if (!on)
            {
                if (camOwned) { camOwned = false; cf.Release(); }
                return;
            }
            var cam = Camera.main;
            float aspect = cam != null ? Mathf.Max(0.5f, cam.aspect) : 16f / 9f;
            float top = A.yMax + 0.45f, bottom = A.yMin - 0.3f;
            float size = Mathf.Max(Float("cam_size", 2.6f), (top - bottom) / (2f - 2f * HudBand), (A.width + 0.4f) * 0.5f / aspect);
            // Close frame FOLLOWS REN, clamped inside the hall:
            //   phase I (few bullets) -> `cam_near_p1` (2.1 u), centred on Ren;
            //   other phases          -> `cam_near` (2.9 u), nudged up toward Absalom.
            // Zooms out to the whole hall only during the TIME STOP (the player must see where every bullet comes from).
            bool tight = state == State.Fight && curPhase == FlockPhase;
            float near = Mathf.Min(size, tight ? Float("cam_near_p1", 2.1f) : Float("cam_near", 2.9f));
            if (!TimeStop.Active && near < size)
            {
                Vector2 p = PlayerFeet + Vector2.up * (tight ? 0.25f : 0.5f);
                if (abs != null) p = Vector2.Lerp(p, abs.Pos, tight ? 0.08f : 0.18f);
                float hw = near * aspect, hh = near;
                float lx = A.xMin - 0.3f + hw, rx = A.xMax + 0.3f - hw, ly = bottom + hh, ty = top + 0.4f - hh;
                var g = new Vector2(lx > rx ? A.center.x : Mathf.Clamp(p.x, lx, rx), ly > ty ? (bottom + top) * 0.5f : Mathf.Clamp(p.y, ly, ty));
                cf.Focus(g, near);
                camOwned = true;
                return;
            }
            cf.Focus(new Vector2(A.center.x, bottom + size), size);
            camOwned = true;
        }

        // ------------------------------------------------------------------ Ren's sword hits
        private void OnStruck(Vector2 c, float r)
        {
            if (Ctx == null || Ctx.Host == null) { PlayerCombat.Struck -= OnStruck; return; }
            if (state != State.Fight) return;
            if (abs != null && !airHidden && abs.Alpha > 0.5f && (abs.Body - c).sqrMagnitude <= (r + 0.45f) * (r + 0.45f))
            {
                WardSpark(abs.Body);
                abs.Flash(Azure, 0.25f);
                FirstTimeHints.Show("tut_absalom_ward", "Your blade cannot reach Absalom. Break what holds him up.", 4.5f);
            }
            StruckLamps(c, r);
            StruckBlessed(c, r);
        }

        private void WardSpark(Vector2 at)
        {
            SpriteVfx.Spawn("hit", at, 0.7f, Layer, Order + 3, false, 18f, false);
            Sfx.Play("hit_block", at, 0.6f);
        }

        // ------------------------------------------------------------------ standing-still tracker (stillFor; the Judgement punish that read it is currently unused)
        private Vector2 stillAnchor;
        private float stillFor;

        private void TrackStill(float dt, Vector2 feet)
        {
            if ((feet - stillAnchor).sqrMagnitude > 0.18f * 0.18f) { stillAnchor = feet; stillFor = 0f; }
            else stillFor += dt;
        }

        // ------------------------------------------------------------------ floor seal — SUMMON CIRCLE
        private SpriteVfx seal;
        private GameObject sealLight;
        private float sealGlow, sealTarget, sealCastUntil, sealCastLen = 1f;
        private Color sealCastColor = Color.white;
        private static readonly Color SealIdle = new Color(0.55f, 0.78f, 1f);

        /// <summary>Large seal in the middle of the hall (replaces the old statue). The WHITE-GREY sprite abs_floor_seal_w is tinted at runtime —
        /// idle: faint pale blue; fight: bright blue, slowly BREATHING (always visible); while he CASTS: switches to the SCHOOL COLOUR, flares and
        /// sends out one wave ring of the seal shape (SealCast); time stop: blazing white. Layer -5 = below hazard markers (-4) so it never hides them.</summary>
        private void InitSeal()
        {
            seal = SpriteVfx.Spawn("abs_floor_seal_w", A.center, 2f, 0, -5, false, 6f, false);
            if (seal == null) seal = SpriteVfx.Spawn("abs_floor_seal", A.center, 2f, 0, -5, false, 6f, false);
            if (seal != null) { seal.Looping(1e7f); seal.Tint(new Color(1f, 1f, 1f, 0f)); }
            sealLight = GlowAt(A.center, SealIdle, 3.2f, 0f, 0f);
        }

        private void SealGlow(float k) => sealTarget = Mathf.Clamp01(k);

        /// <summary>He casts school `c` -> the summon circle changes colour and flares for `seconds`, with a seal-shaped wave ring.</summary>
        private void SealCast(Color c, float seconds)
        {
            sealCastColor = c;
            sealCastLen = Mathf.Max(0.3f, seconds);
            sealCastUntil = Time.time + sealCastLen;
            Ctx.Run(SealWave(c));
            Sfx.Play("seal_pulse", A.center, 0.6f);                                // sound
        }

        private IEnumerator SealWave(Color c)
        {
            var w = SpriteVfx.Spawn("abs_floor_seal_w", A.center, 2f, 0, -5, false, 10f, false);
            if (w == null) yield break;
            w.Looping(1f);
            for (float t = 0f; t < 0.7f && w != null; t += Time.deltaTime)
            {
                float u = t / 0.7f;
                w.transform.localScale = Vector3.one * 2f * Mathf.Lerp(1f, 1.32f, 1f - (1f - u) * (1f - u));
                w.Tint(new Color(Mathf.Lerp(1f, c.r, u), Mathf.Lerp(1f, c.g, u), Mathf.Lerp(1f, c.b, u), 0.75f * (1f - u)));
                yield return null;
            }
            if (w != null) w.Stop();
        }

        private void AnimateSeal(float dt)
        {
            bool fight = state == State.Fight || state == State.Intro;
            sealGlow = Mathf.MoveTowards(sealGlow, sealTarget, dt * 1.5f);
            float cast = Time.time < sealCastUntil ? Mathf.Sin(Mathf.Clamp01(1f - (sealCastUntil - Time.time) / sealCastLen) * Mathf.PI) : 0f;
            float breathe = 0.5f + 0.5f * Mathf.Sin(Time.time * 1.7f);
            Color col = Color.Lerp(SealIdle, sealCastColor, Mathf.Clamp01(cast * 1.6f));
            col = Color.Lerp(col, Color.white, sealGlow);
            float a = (fight ? 0.32f + 0.1f * breathe : 0.16f + 0.05f * breathe) + 0.45f * cast + 0.5f * sealGlow;
            if (seal != null) seal.Tint(new Color(col.r, col.g, col.b, Mathf.Clamp01(a)));
            if (sealLight != null)
            {
                var l = sealLight.GetComponent<Light2D>();
                if (l != null)
                {
                    l.color = col;
                    l.intensity = (fight ? 0.45f + 0.15f * breathe : 0.2f) + 1.3f * cast + 1.1f * sealGlow;
                }
            }
        }

        // ------------------------------------------------------------------ test keys
        private bool skipPhase;

        /// <summary>Clear the current phase NOW (lamp phase: break every lamp; congregation phase: kill every cultist, skip remaining waves).</summary>
        internal bool DebugSkipPhase()
        {
            if (state != State.Fight) return false;
            skipPhase = true;
            if (curPhase == LampPhase) foreach (var l in lamps) if (!l.Broken) BreakLamp(l, false);
            if (curPhase == FlockPhase) foreach (var e in cultists) if (e != null && e.Health != null && !e.Health.IsDead) e.Health.TakeDamage(99999f);
            Hints.Show("[TEST] Absalom: skipping phase " + curPhase, 1.5f);
            return true;
        }

        /// <summary>(Ctrl+F7): cast the next sample skill NOW to review its visuals. All 13 school skills —
        /// Light (Sunlance, Halo Ring, Sunfall, Ascension Dive) -> Fire (Pyre Lines, Cinder Rain) -> Frost (Rime Thorns, Glacial Volley)
        /// Storm (Tempest Lattice, Thunderclap) -> Umbral (Umbral Orbs, Abyssal Well) -> fly around the hall (flight animation) -> Judgement Ray,
        /// Arcane Blaster, Void Cannon, Eclipse, Grasp of the Damned, Starfall, The Still Hour -> loop.</summary>
        private int showcase;

        internal bool DebugShowcase()
        {
            if (state != State.Fight) return false;
            string[] names = { "SUNLANCE", "HALO RING", "SUNFALL", "CATACLYSM DIVE", "PYRE LINES", "CINDER RAIN", "RIME THORNS",
                               "GLACIAL VOLLEY", "TEMPEST LATTICE", "THUNDERCLAP", "UMBRAL ORBS", "ABYSSAL WELL", "FLIGHT (glide across the hall)",
                               "JUDGEMENT RAY", "ARCANE BLASTER", "VOID CANNON", "ECLIPSE", "GRASP OF THE DAMNED", "STARFALL", "THE STILL HOUR" };
            int k = showcase++ % names.Length;
            IEnumerator r;
            switch (k)
            {
                case 0: r = Sunlance(2); break;
                case 1: r = HaloRing(2); break;
                case 2: r = ShowSunfall(); break;
                case 3: r = AscensionDive(2); break;
                case 4: r = PyreLines(2); break;
                case 5: r = CinderRain(2); break;
                case 6: r = RimeThorns(2); break;
                case 7: r = GlacialVolley(2); break;
                case 8: r = TempestLattice(2); break;
                case 9: r = Thunderclap(2); break;
                case 10: r = UmbralOrbs(2); break;
                case 11: r = AbyssalWell(2); break;
                case 12: r = ShowFlight(); break;
                case 13: r = JudgementRay(2); break;
                case 14: r = ArcaneBlaster((School)Random.Range(0, 5), 2); break;
                case 15: r = VoidCannon(2); break;
                case 16: r = Eclipse(2); break;
                case 17: r = GraspOfTheDamned(2); break;
                case 18: r = AscensionDive(2, true); break;
                default: r = TimeStopRoutine(StopPattern.Daggers); break;
            }
            if (k == names.Length - 1) Ctx.Run(r);                                     // The Still Hour stops other skills itself -> runs outside the routines list
            else StartR(r);
            Hints.Show("[TEST] Showcase " + (k + 1) + "/" + names.Length + ": " + names[k] + "   (Ctrl+F7 again: next)", 2f);
            return true;
        }

        private IEnumerator ShowSunfall()
        {
            yield return Cast(School.Light, "SUNFALL", 0.4f, "bell_toll");
            yield return Sunfall(3, 0.55f, 0.55f, 9f);
            abs.Play("idle", 5f);
        }

        /// <summary>Fly a loop through the 4 corners of the dais and back — preview of the 3-direction flight animation + afterimage trail.</summary>
        private IEnumerator ShowFlight()
        {
            Vector2 back = abs.Pos;
            yield return Glide(new Vector2(dais.xMin, dais.yMax), 2.6f);
            yield return Glide(new Vector2(dais.xMax, dais.yMax), 2.6f);
            yield return Glide(new Vector2(dais.xMax, dais.yMin), 2.6f);
            yield return Glide(new Vector2(dais.xMin, dais.yMin), 2.6f);
            yield return Glide(back, 2.6f);
        }

        /// <summary>F5 / Shift+F3 (test keys): mark the implemented phases as cleared.</summary>
        internal override void DebugSolve()
        {
            Ctx.Flags.Set(MetFlag);
            for (int p = 1; p <= BuiltPhases; p++) Ctx.Flags.Set(PhaseFlag(p));
            Abort();
            state = State.Rest;
            foreach (var l in lamps) BreakLamp(l, false);
        }
    }
}
