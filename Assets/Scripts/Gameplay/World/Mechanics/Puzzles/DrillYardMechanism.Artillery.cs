using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// STATION 3 — FINAL EXAM, part 2: LIVE FIRE.
    /// After the quiz Burlap kicks the board away and calls in 2 MORTARS (`role` mortar) and 2 FIRE-POT CRATES (`role` crate).
    /// FIRE POTS (resin pots with a burning fuse — they set hay alight, which reads naturally) instead of hay shells. HAY DROPS ONE PILE AT A TIME
    /// (only 1 pile in the yard, landing ≥ 1.8 u from both mortars — relaxed if cramped — away from crates, Ren / Burlap and the previous spot,
    /// 0.9 s shadow warning); a hit burns it -> ash fades, a new pile drops elsewhere 2.4 s later. Hitting hay while Burlap is NOT stuck = burns the
    /// bait (new pile elsewhere). Burlap diving into the same pile twice without being shot -> he tears it apart (moves it, so it never sits in a
    /// spot that is hard to shoot). Tile objects with `role` hay = preferred drop spots.
    /// Loop — "HAUL AMMO + TIME THE AIM":
    ///   1) A POT appears at one crate (random, blinking). Walk onto it = carry on the shoulder: SLOW (60%), CANNOT ROLL.
    ///      Hit while carrying = drop it (back to the crate) + lose 1 life.
    ///   2) At a mortar press T = LOAD. T again = AIM: the aim ring sweeps BACK AND FORTH along mortar -> Burlap (near - far, period 1.5 -> 1.0 s).
    ///      Walking > 1.1 u from the mortar = aim lost. T a third time = FIRE: the pot arcs for 1.2 s and lands EXACTLY where the ring was (no tracking).
    ///   3) It only HITS if it lands on Burlap while he is STUCK HEAD-FIRST IN HAY. Landing near him while he is free = he SWATS it away.
    ///      Off target = "WIDE!". Standing in the landing spot = hit by your own shot (-1 life). 1 s after each shot a new pot appears — only 1 in the yard.
    ///   4) Every hit SETS that hay pile ON FIRE (ash, then gone) -> a new pile drops elsewhere; 3 hits = win. Burlap speeds up; from hit 1 he does
    ///      FAKE charges (wind up then change direction), and leaving the hay without being shot -> a STOMP shockwave with gaps.
    ///   Burlap ramming a WALL only staggers 0.6 s (not a shooting chance); ramming a MORTAR knocks out the loaded pot.
    /// 3 hits = Burlap collapses, "REBOOTING... PASSED".
    /// </summary>
    public sealed partial class DrillYardMechanism
    {
        private sealed class Mortar
        {
            public Vector2 Feet;
            public SpriteRenderer Sr, Shell;
            public GameObject Blocker;
            public bool Loaded;
            public float Kick, Drop;
            public Vector2 Muzzle => Feet + Vector2.up * 0.54f;
        }

        private sealed class Haystack
        {
            public Vector2 Feet;
            public SpriteRenderer Sr;
            public GameObject Blocker;
            public float Shake, Drop;
            public bool Burned;
            public SpriteVfx Flame;
        }

        private sealed class Crate
        {
            public Vector2 Feet;
            public SpriteRenderer Sr;
            public GameObject Blocker;
            public float Drop;
        }

        private readonly List<Mortar> mortars = new List<Mortar>();
        private readonly List<Haystack> hays = new List<Haystack>();
        private readonly List<Crate> crates = new List<Crate>();
        private readonly List<GameObject> shellFx = new List<GameObject>();
        private readonly List<SpriteRenderer> aimDots = new List<SpriteRenderer>();
        private SpriteRenderer legs, crown, shellSr, reticle;
        private bool artilleryOn, kingStuck, kingHitNow, crownOff, carryingShell;
        private int kingHp = 3, shellCrate = -1, lastCrate = -1;
        private float stuckUntil, crownAt, shellAt = -1f, aimT0;
        private Vector2 crownFrom, crownTo, aimPoint;
        private Haystack stuckIn;
        private Mortar aimMortar;
        private SpriteVfx kingStars;
        private Sprite[] baleFrames;
        private Sprite burntHay, hayFresh, fireShell, fireCrate;
        // hay drops one pile at a time
        private readonly List<Vector2> haySpots = new List<Vector2>();
        private Haystack activeHay;
        private float hayNext = -1f;
        private bool hayDropping;
        private int hayStucks;
        private Vector2 lastHaySpot = new Vector2(-999f, -999f);
        private const float HayMortarGap = 1.8f, HayMortarGapMin = 1.2f;
        private PlayerStatus shellStatus;
        private const float MortarScale = 1f, HayScale = 1f, KingPropScale = 1.35f, ShellFlight = 1.2f, AimRange = 1.1f;

        // ------------------------------------------------------------------ build
        private void InitArtillery()
        {
            baleFrames = SpriteVfx.Load("drill/fx_bale");
            burntHay = WardenStatueMechanism.Load("VFX/props/drill_hay_burnt");
            fireShell = WardenStatueMechanism.Load("VFX/props/drill_fire_shell");        // fire pot (PixelLab 4e1b804b)
            fireCrate = WardenStatueMechanism.Load("VFX/props/drill_fire_crate");        // fire-pot crate (PixelLab df2e227e)
            shellStatus = Ctx.Player != null ? PlayerStatus.Of(Ctx.Player) : null;
            var mortarAt = Points("mortar", "mortar_at", new[] { new Vector2(A.xMin + 2.2f, A.yMin + 0.7f), new Vector2(A.xMax - 2.2f, A.yMin + 0.7f) });
            var hayAt = Points("hay", "hay_at", new[] { new Vector2(A.xMin + 1.2f, A.center.y + 0.6f), new Vector2(A.center.x, A.yMax - 1.0f), new Vector2(A.xMax - 1.2f, A.center.y + 0.6f) });
            var crateAt = Points("crate", "crate_at", new[] { new Vector2(A.xMin + 0.5f, A.yMin + 0.3f), new Vector2(A.xMax - 0.5f, A.yMin + 0.3f) });
            int i = 0;
            foreach (var feet in mortarAt)
            {
                var m = new Mortar { Feet = feet };
                m.Sr = Prop("Drill Mortar " + i, m.Feet, "VFX/props/drill_mortar", 0);
                m.Sr.transform.localScale = new Vector3(MortarScale, MortarScale, 1f);
                m.Sr.enabled = false;
                m.Shell = TrapArt.MakeRenderer(m.Sr.transform, "Drill Mortar Shell " + i, m.Feet, Ctx.Player, 1);
                m.Shell.transform.localPosition = new Vector3(0f, 0.55f, 0f);
                m.Shell.transform.localScale = new Vector3(0.75f, 0.75f, 1f);
                m.Shell.sprite = ShellSprite;
                m.Shell.enabled = false;
                m.Blocker = AddBlocker(m.Feet, new Vector2(0.46f, 0.18f));
                m.Blocker.SetActive(false);
                var mm = m;
                InteractionSystem.Add(new Interactable
                {
                    id = Name + "_mortar_" + i, verb = "Load fire pot", clue = false, calmOnly = false, range = 0.85f,
                    verbNow = () => aimMortar == mm ? "FIRE!" : mm.Loaded ? "Aim" : "Load fire pot",
                    rect = new Rect(m.Feet.x - 0.3f, m.Feet.y, 0.6f, 0.55f),
                    anchor = m.Feet + Vector2.up * 0.8f,
                    available = () => artilleryOn && phase == Phase.Running && !inScene && !frozen && (mm.Loaded || carryingShell),
                    use = () => UseMortar(mm),
                });
                mortars.Add(m);
                i++;
            }
            i = 0;
            haySpots.AddRange(hayAt);                                                  // preferred drop spots (tile objects with `role` hay)
            foreach (var feet in hayAt)
            {
                var h = new Haystack { Feet = feet };
                h.Sr = Prop("Drill Haystack " + i++, h.Feet, "VFX/props/drill_haystack", 0);
                h.Sr.transform.localScale = new Vector3(HayScale, HayScale, 1f);
                h.Sr.enabled = false;
                h.Blocker = AddBlocker(h.Feet, new Vector2(0.6f, 0.2f));
                h.Blocker.SetActive(false);
                hays.Add(h);
            }
            if (hays.Count > 0) hayFresh = hays[0].Sr.sprite;
            i = 0;
            foreach (var feet in crateAt)
            {
                var c = new Crate { Feet = feet };
                c.Sr = Prop("Drill Ammo Crate " + i++, c.Feet, "VFX/props/drill_ammo_crate", 0);
                if (fireCrate != null) c.Sr.sprite = fireCrate;                       // fire-pot crates
                c.Sr.enabled = false;
                c.Blocker = AddBlocker(c.Feet, new Vector2(0.36f, 0.16f));
                c.Blocker.SetActive(false);
                crates.Add(c);
            }
            shellSr = TrapArt.MakeRenderer(Ctx.Host.transform, "Drill Loose Shell", A.center, Ctx.Player, 3);
            shellSr.sprite = ShellSprite;
            FxLighting.MakeUnlit(shellSr);                                             // fire pot is self-lit
            shellSr.enabled = false;
            reticle = TrapArt.MakeRenderer(Ctx.Host.transform, "Drill Aim Reticle", A.center, Ctx.Player, -5);
            reticle.sprite = DrillChalk.Reticle();
            FxLighting.MakeUnlit(reticle);
            reticle.enabled = false;
            for (int k = 0; k < 7; k++)
            {
                var d = TrapArt.MakeRenderer(Ctx.Host.transform, "Drill Aim Dot", A.center, Ctx.Player, -6);
                d.sprite = DrillChalk.Shadow();
                d.transform.localScale = new Vector3(0.14f, 0.1f, 1f);
                FxLighting.MakeUnlit(d);
                d.enabled = false;
                aimDots.Add(d);
            }
            legs = Prop("Drill Burlap Legs", kingHome, "VFX/props/drill_king_legs", 0);
            legs.transform.localScale = new Vector3(KingPropScale, KingPropScale, 1f);
            legs.enabled = false;
            crown = Prop("Drill Burlap Crown", kingHome, "VFX/props/drill_king_crown", 1);
            crown.transform.localScale = new Vector3(KingPropScale, KingPropScale, 1f);
            crown.enabled = false;
        }

        /// <summary>Position from a Tiled tile object (`role`); missing -> from the "x,y;x,y" property (Tiled tiles); missing too -> default.</summary>
        private List<Vector2> Points(string role, string prop, Vector2[] fallback)
        {
            var list = new List<Vector2>();
            foreach (var a in ArtRole(role)) list.Add(ArtFeet(a));
            if (list.Count == 0)
                foreach (var part in Str(prop, "").Split(';'))
                    if (part.Trim().Length > 0) list.Add(Ctx.TilePoint(part.Trim(), A.center));
            if (list.Count == 0) list.AddRange(fallback);
            return list;
        }

        private void ShowArtillery(bool on, bool fx)
        {
            artilleryOn = on;
            foreach (var m in mortars)
            {
                m.Sr.enabled = on; m.Shell.enabled = false; m.Blocker.SetActive(on);
                m.Loaded = false; m.Kick = 0f; m.Drop = on && fx ? 1f : 0f;
                m.Sr.transform.position = m.Feet; m.Sr.color = Color.white;
                if (fx) SpriteVfx.Spawn("drill/fx_snuff", m.Feet + Vector2.up * 0.2f, 2f, Layer, Order + 3, false, 14f, false);
            }
            for (int i = 0; i < hays.Count; i++)
            {
                var h = hays[i];
                h.Burned = false;
                if (h.Flame != null) { h.Flame.Stop(); h.Flame = null; }
                if (hayFresh != null) h.Sr.sprite = hayFresh;
                h.Sr.color = Color.white;
                h.Sr.enabled = false; h.Blocker.SetActive(false); h.Shake = 0f;           // hay drops ONE PILE AT A TIME (HayDrop)
                h.Drop = 0f;
                h.Sr.transform.position = h.Feet;
            }
            activeHay = null; hayDropping = false; hayStucks = 0; lastHaySpot = new Vector2(-999f, -999f);
            hayNext = on ? Time.time + (fx ? 2.0f : 0.6f) : -1f;
            for (int i = 0; i < crates.Count; i++)
            {
                var c = crates[i];
                c.Sr.enabled = on; c.Blocker.SetActive(on);
                c.Drop = on && fx ? 1.2f + i * 0.3f : 0f;
                c.Sr.transform.position = c.Feet;
            }
            carryingShell = false; shellCrate = -1; shellAt = on ? Time.time + 2.2f : -1f;
            shellSr.enabled = false;
            CancelAim(null);
            if (!on)
            {
                legs.enabled = false; crown.enabled = false; crownOff = false;
                foreach (var g in shellFx) if (g != null) Object.Destroy(g);
                shellFx.Clear();
            }
        }

        private void ResetArtillery()
        {
            StopStars();
            kingStuck = false; kingHitNow = false; stuckIn = null; kingHp = 3; shellsInFlight = 0;
            ShowArtillery(false, false);
        }

        // ------------------------------------------------------------------ ammo: crate -> shoulder -> mortar
        private void SpawnShell()
        {
            if (crates.Count == 0) return;
            int k = Random.Range(0, crates.Count);
            if (crates.Count > 1 && k == lastCrate) k = (k + 1) % crates.Count;            // switch crates so Ren has to run
            shellCrate = lastCrate = k;
            shellAt = -1f;
            shellSr.enabled = true;
            Sfx.Play("trap_fire", crates[k].Feet, 0.35f);
            SpriteVfx.Spawn("duo_flame", crates[k].Feet + Vector2.up * 0.2f, 0.35f, Layer, Order + 2, false, 18f, false);
            DrillBoard.Say("FIRE POT READY!", Sky);
        }

        /// <summary>Hit while carrying: the pot drops and rolls back to the crate (available again 1 s later).</summary>
        private void DropShell(Vector2 at)
        {
            if (!carryingShell) return;
            carryingShell = false;
            shellSr.enabled = false;
            shellAt = Time.time + 1f;
            SpriteVfx.Spawn("duo_flame", at + Vector2.up * 0.1f, 0.45f, Layer, Order + 3, false, 18f, false);
            Sfx.Play("gas_hiss", at, 0.6f);
            DrillBoard.Say("FIRE POT DROPPED!", Red);
        }

        private void UseMortar(Mortar m)
        {
            if (!artilleryOn || phase != Phase.Running || frozen) return;
            if (aimMortar == m) { Fire(m); return; }
            if (m.Loaded) { StartAim(m); return; }
            if (!carryingShell) return;
            carryingShell = false;
            shellSr.enabled = false;
            m.Loaded = true; m.Kick = 0.6f;
            Sfx.Play("hit_wood", m.Feet, 0.7f);
            Sfx.Play("lift_jam", m.Feet, 0.4f);
            DrillBoard.Say("LOADED! PRESS T TO AIM", Gold);
        }

        private void StartAim(Mortar m)
        {
            aimMortar = m; aimT0 = Time.time;
            reticle.enabled = true;
            Sfx.Play("reel_spin", m.Feet, 0.5f);
        }

        private void CancelAim(string why)
        {
            if (aimMortar == null && (reticle == null || !reticle.enabled)) return;
            aimMortar = null;
            if (reticle != null) reticle.enabled = false;
            foreach (var d in aimDots) if (d != null) d.enabled = false;
            if (!string.IsNullOrEmpty(why)) DrillBoard.Say(why, Red);
        }

        /// <summary>Aim direction (mortar -> Burlap) + the farthest reach inside the yard along it.</summary>
        private void AimLine(Mortar m, out Vector2 dir, out float far)
        {
            Vector2 to = king.Pos - m.Feet;
            dir = to.sqrMagnitude > 0.01f ? to.normalized : Vector2.up;
            far = 0.7f;
            while (far < 9f && Inside(m.Feet + dir * (far + 0.05f))) far += 0.05f;
        }

        // ------------------------------------------------------------------ every frame
        private void AnimateArtillery(float dt)
        {
            if (!artilleryOn && !crownOff) return;
            // pot at the crate / on the shoulder
            if (artilleryOn && shellAt > 0f && Time.time >= shellAt && !carryingShell && shellCrate < 0 && !AnyLoaded) SpawnShell();
            if (artilleryOn && activeHay == null && !hayDropping && hayNext > 0f && Time.time >= hayNext && kingHp > 0) Ctx.Run(HayDrop());
            if (shellCrate >= 0 && shellCrate < crates.Count)
            {
                var c = crates[shellCrate];
                shellSr.transform.position = c.Feet + Vector2.up * (0.36f + 0.03f * Mathf.Sin(Time.time * 5f));
                shellSr.color = Color.Lerp(Color.white, new Color(1f, 0.9f, 0.5f), 0.5f + 0.5f * Mathf.Sin(Time.time * 9f));
                if (phase == Phase.Running && !frozen && (PlayerFeet - c.Feet).magnitude < 0.5f)
                {
                    carryingShell = true; shellCrate = -1;
                    Sfx.Play("pickup_item", PlayerFeet, 0.7f);
                    DrillBoard.Say("FIRE POT IN HAND - SLOW, NO ROLL!", Sky);
                }
            }
            if (carryingShell)
            {
                shellSr.enabled = true;
                shellSr.color = Color.Lerp(Color.white, new Color(1f, 0.8f, 0.55f), 0.5f + 0.5f * Mathf.Sin(Time.time * 14f));
                shellSr.transform.position = PlayerFeet + new Vector2(0f, 0.62f + 0.02f * Mathf.Sin(Time.time * 10f));
                if (shellStatus != null) { shellStatus.Slow(0.6f, 0.2f, null); shellStatus.SealRoll(0.25f, null); }
            }
            foreach (var m in mortars)
            {
                m.Shell.enabled = artilleryOn && m.Loaded;
                m.Kick = Mathf.MoveTowards(m.Kick, 0f, dt * 4f);
                float k = Mathf.Sin(m.Kick * Mathf.PI * 3f) * m.Kick * 0.14f;
                m.Sr.transform.localScale = new Vector3(MortarScale * (1f + k), MortarScale * (1f - k), 1f);
                bool call = artilleryOn && (carryingShell && !m.Loaded || m.Loaded && aimMortar != m);
                m.Sr.color = call ? Color.Lerp(Color.white, new Color(1f, 0.85f, 0.4f), 0.5f + 0.5f * Mathf.Sin(Time.time * 10f)) : Color.white;
                if (m.Drop > 0f)
                {
                    m.Drop = Mathf.MoveTowards(m.Drop, 0f, dt / 0.3f);
                    m.Sr.transform.position = m.Feet + Vector2.up * (m.Drop * m.Drop * 1.6f);
                    if (m.Drop <= 0f) { m.Sr.transform.position = m.Feet; m.Kick = 1f; Sfx.Play("heavy_land", m.Feet, 0.6f); SpriteVfx.Spawn("m3_dash_dust", m.Feet, 1.1f, Layer, Order - 1, false, 16f, false); }
                }
            }
            foreach (var c in crates)
            {
                if (c.Drop <= 0f) continue;
                float before = c.Drop;
                c.Drop = Mathf.MoveTowards(c.Drop, 0f, dt / 0.3f);
                float u = Mathf.Min(1f, c.Drop);
                c.Sr.transform.position = c.Feet + Vector2.up * (u * u * 1.6f);
                c.Sr.enabled = artilleryOn && c.Drop <= 1f;
                if (before > 0f && c.Drop <= 0f) { c.Sr.transform.position = c.Feet; Sfx.Play("break_wood", c.Feet, 0.4f); SpriteVfx.Spawn("m3_dash_dust", c.Feet, 0.9f, Layer, Order - 1, false, 16f, false); }
            }
            foreach (var h in hays)
            {
                if (h.Drop > 0f)
                {
                    float before = h.Drop;
                    h.Drop = Mathf.MoveTowards(h.Drop, 0f, dt / 0.3f);
                    float u = Mathf.Min(1f, h.Drop);
                    h.Sr.transform.position = h.Feet + Vector2.up * (u * u * 1.8f);
                    h.Sr.enabled = artilleryOn && h.Drop <= 1f;
                    if (before > 0f && h.Drop <= 0f) { h.Sr.transform.position = h.Feet; h.Shake = 1f; Sfx.Play("drill_whoosh", h.Feet, 0.5f); SpriteVfx.Spawn("drill/fx_hayburst", h.Feet + Vector2.up * 0.2f, 0.7f, Layer, Order + 2, false, 18f, false); }
                }
                float shake = Mathf.Max(h.Shake, stuckIn == h && kingStuck ? 0.35f : 0f);
                h.Shake = Mathf.MoveTowards(h.Shake, 0f, dt * 2.5f);
                float sq = Mathf.Sin(Time.time * 30f) * shake * 0.08f;
                h.Sr.transform.localScale = new Vector3(HayScale * (1f + sq), HayScale * (1f - sq), 1f);
            }
            // aim ring sweeps back and forth (near <-> far) along mortar -> Burlap
            if (aimMortar != null)
            {
                var m = aimMortar;
                if (!artilleryOn || !m.Loaded || phase != Phase.Running || frozen) CancelAim(null);
                else if ((PlayerFeet - m.Feet).magnitude > AimRange) CancelAim("AIM LOST - STAY AT THE MORTAR");
                else
                {
                    AimLine(m, out var dir, out float far);
                    float period = Mathf.Lerp(1.5f, 1.0f, (3 - kingHp) / 2f);
                    float near = 0.7f, w = 0.5f - 0.5f * Mathf.Cos((Time.time - aimT0) / period * Mathf.PI * 2f);
                    aimPoint = m.Feet + dir * Mathf.Lerp(near, Mathf.Max(near, far), w);
                    reticle.transform.position = aimPoint;
                    bool onHim = kingStuck && stuckIn != null && (aimPoint - HayTarget(stuckIn)).magnitude < 0.6f;
                    float rs = (onHim ? 1.1f : 0.95f) * (1f + 0.06f * Mathf.Sin(Time.time * 25f));
                    reticle.transform.localScale = new Vector3(rs, rs * 0.6f, 1f);
                    reticle.color = onHim ? new Color(0.5f, 1f, 0.55f) : new Color(1f, 0.55f, 0.45f);
                    for (int i = 0; i < aimDots.Count; i++)
                    {
                        aimDots[i].enabled = true;
                        aimDots[i].transform.position = Vector2.Lerp(m.Muzzle, aimPoint, (i + 1) / (float)(aimDots.Count + 1));
                        aimDots[i].color = new Color(1f, 1f, 1f, 0.45f);
                    }
                }
            }
            // legs kicking in the hay pile
            if (legs.enabled && stuckIn != null)
            {
                legs.transform.position = stuckIn.Feet + new Vector2(0f, 0.36f + Mathf.Abs(Mathf.Sin(Time.time * 9f)) * 0.03f);
                legs.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 16f) * 12f);
            }
            // crown knocked off / flying back
            if (crown.enabled)
            {
                float u = Mathf.Clamp01((Time.time - crownAt) / 0.45f);
                Vector2 to = crownOff ? crownTo : king.Pos + Vector2.up * 0.95f;
                Vector2 p = Vector2.Lerp(crownFrom, to, u) + Vector2.up * Mathf.Sin(u * Mathf.PI) * 0.6f;
                crown.transform.position = p;
                crown.transform.rotation = Quaternion.Euler(0f, 0f, crownOff ? Mathf.Lerp(0f, 200f, u) : Mathf.Lerp(200f, 360f, u));
                if (!crownOff && u >= 1f) crown.enabled = false;
            }
        }

        private bool AnyLoaded { get { foreach (var m in mortars) if (m.Loaded) return true; return false; } }

        private static Vector2 HayTarget(Haystack h) => h.Feet + Vector2.up * 0.2f;

        // ------------------------------------------------------------------ part transition
        private IEnumerator ExamToArtillery()
        {
            SetLive(false);                                                            // transition to Live Fire -> dialogue box
            DrillBoard.Command = "";
            yield return Wait(0.6f);
            Bark("EXAM PROTOCOL: EXHAUSTED. *KRRK* ...SWITCHING TO PROGRAM FOUR: LIVE FIRE!", 3.2f, true);
            Sfx.Play("lift_jam", king.Pos, 0.8f);
            king.Flash(new Color(1f, 0.4f, 0.3f), 0.6f);
            yield return Wait(1.6f);
            if (aborted || stationFailed) yield break;
            if (examShown) yield return KickBoardScene();
            if (aborted || stationFailed) yield break;
            yield return DeployArtillery();
        }

        private IEnumerator KickBoardScene()
        {
            // the board may sit left of the lectern (mirrored yard) -> stand on the yard-centre side of it and kick it outward
            float side = BoardSide;
            yield return WalkKing(boardAt - Vector2.right * (0.75f * side), 2f);
            king.Sr.flipX = side < 0f;
            king.Play("dashend", 16f, false);
            yield return Wait(0.2f);
            Sfx.Play("break_wood", boardAt, 0.9f);
            Sfx.Play("duo_punch", boardAt, 0.6f);
            CameraFollow.KickMain(Vector2.right * side, 0.08f, 0.25f);
            Ctx.Run(KickBoard());
            foreach (var d in doors) d.Open = false;
            Sfx.Play("door_wood", A.center, 0.6f);
            Bark("CLASS. DISMISSED.", 1.6f, true);
            yield return Wait(1.0f);
            ShowExam(false);
        }

        private IEnumerator DeployArtillery()
        {
            yield return WalkKing(lectern, 2f);
            Bark("CORPORAL! HAUL OUT THE MORTARS! And the FIRE POTS. Hay comes ONE bale at a time - I'm on a BUDGET.", 3.4f, true);
            yield return Wait(0.8f);
            Sfx.Play("drill_deploy", A.center, 0.9f);
            ShowArtillery(true, true);
            CameraFollow.KickMain(Vector2.down, 0.07f, 0.3f);
            kingHp = 3;
            DrillBoard.Title = "LIVE FIRE";
            DrillBoard.Goal = "HIT BURLAP 0 / 3"; DrillBoard.GoalFrac = 0f;
            yield return Wait(1.2f);
            Tip("Carry a fire pot from the glowing crate to a mortar (T = load, T = aim, T = fire). It only counts while Burlap is STUCK in the haystack.", 6f);
            Bark("Rules: I CHARGE. You get FLATTENED. And do NOT lure me into my own hay. ...That is NOT a hint.", 3.6f, true);
            yield return Wait(2.4f);
        }

        /// <summary>Board is right (+1) or left (-1) of the lectern.</summary>
        private float BoardSide => boardAt.x >= kingHome.x ? 1f : -1f;

        private IEnumerator KickBoard()
        {
            if (board == null) yield break;
            float side = BoardSide;
            Vector2 from = board.transform.position;
            for (float t = 0f; t < 0.7f; t += Time.deltaTime)
            {
                float u = t / 0.7f;
                board.transform.position = from + new Vector2(1.6f * u * side, Mathf.Sin(u * Mathf.PI) * 1.4f + u * 0.4f);
                board.transform.rotation = Quaternion.Euler(0f, 0f, -540f * u * side);
                board.color = new Color(1f, 1f, 1f, 1f - u * u);
                yield return null;
            }
            SpriteVfx.Spawn("drill/fx_hayburst", (Vector2)board.transform.position, 0.6f, Layer, Order + 2, false, 18f, false);
            ShowBoard(false);
        }

        // ------------------------------------------------------------------ fight
        private IEnumerator Artillery()
        {
            SetLive(true);
            while (kingHp > 0 && !aborted && !stationFailed)
                yield return BullRun();
            if (aborted || stationFailed || kingHp > 0) yield break;
            // collapse: smoke, reboot
            CancelAim(null);
            SetLive(false);                                                            // Burlap down -> no more danger, closing line goes to the dialogue box
            foreach (var a in actors) Ctx.Run(Vanish(a));
            king.Play("dashend", 8f, false);
            Bark("...THREE. Three fire pots. To the FACE. *KRRK* SYSTEM... YIELDS.", 3f, true);
            Sfx.Play("lift_jam", king.Pos);
            for (int i = 0; i < 8 && !aborted; i++)
            {
                SpriteVfx.Spawn("drill/fx_snuff", king.Pos + new Vector2(Random.Range(-0.35f, 0.35f), Random.Range(0.4f, 1.0f)), 1.6f, Layer, Order + 3, false, 14f, false);
                king.Pos += Random.insideUnitCircle * 0.03f;
                king.Flash(new Color(1f, 0.6f, 0.3f), 0.2f);
                if (i % 3 == 0) Sfx.Play("steam_vent", king.Pos, 0.6f);
                yield return new WaitForSeconds(0.3f);
            }
            yield return new WaitForSeconds(0.6f);
            Bark("...REBOOTING. *ding* RECRUIT STATUS: PASSED. YARD: UNSEALED. ...Please don't tell the other dummies about the hay.", 4.4f, true);
            yield return new WaitForSeconds(2.4f);
        }

        /// <summary>One turn: aim -> (fake charge) -> warning line -> charge until hay / wall / mortar -> stuck in hay (shooting chance) or stagger.</summary>
        private IEnumerator BullRun()
        {
            float anger = 3 - kingHp;                                                     // 0, 1, 2
            float warn = Mathf.Lerp(0.95f, 0.65f, anger / 2f), speed = 6.4f + 1.6f * anger;
            king.Play("idle", 10f);
            yield return Wait(Mathf.Lerp(0.8f, 0.45f, anger / 2f));
            if (aborted || stationFailed) yield break;
            Vector2 from = king.Pos;
            // from hit 1: 35% FAKE CHARGE — winds up toward Ren, stops, re-aims faster
            if (anger >= 1f && Random.value < 0.35f)
            {
                Vector2 fd = (PlayerFeet - from).sqrMagnitude > 0.01f ? (PlayerFeet - from).normalized : Vector2.down;
                king.Sr.flipX = fd.x < 0f;
                Bark(Pick("CHAAAR-", "INSPEC-", "HOLD STI-"), 0.8f, true);
                DrillFx.Warn(Ctx.Host, from + fd * 0.35f, from + fd * 2.2f, 0.55f, fd, Ctx.Player, 0.34f);
                for (float t = 0f; t < 0.55f && !aborted && !stationFailed; t += Time.deltaTime) { king.Pos = from + Random.insideUnitCircle * 0.02f; yield return null; }
                king.Pos = from;
                if (aborted || stationFailed) yield break;
                Bark(Pick("...PSYCH!", "Made you FLINCH!", "Hah! Tactical PAUSE!"), 1f, true);
                yield return Wait(0.25f);
                warn = 0.45f;
            }
            Vector2 dir = (PlayerFeet - from).sqrMagnitude > 0.01f ? (PlayerFeet - from).normalized : Vector2.down;
            int kind = 0;                                                                // 0 miss, 1 wall, 2 hay, 3 mortar
            Haystack hay = null; Mortar mort = null;
            float dist = 0f;
            while (dist < 30f)
            {
                var q = from + dir * (dist + 0.05f);
                if (!Inside(q)) { kind = 1; break; }
                if ((hay = HayAt(q)) != null) { kind = 2; break; }
                if ((mort = MortarAt(q)) != null) { kind = 3; break; }
                if (CrateAt(q)) { kind = 1; break; }                                    // ammo crates are obstacles too (he used to charge through and end up standing inside one)
                dist += 0.05f;
            }
            // charge path too short (hugging a wall / mortar / crate) -> charge toward the yard centre instead of ramming a corner forever
            if (kind != 2 && dist < 0.7f)
            {
                dir = (A.center - from).sqrMagnitude > 0.01f ? (A.center - from).normalized : Vector2.down;
                kind = 0; hay = null; mort = null; dist = 0f;
                while (dist < 30f)
                {
                    var q = from + dir * (dist + 0.05f);
                    if (!Inside(q)) { kind = 1; break; }
                    if ((hay = HayAt(q)) != null) { kind = 2; break; }
                    if ((mort = MortarAt(q)) != null) { kind = 3; break; }
                    if (CrateAt(q)) { kind = 1; break; }
                    dist += 0.05f;
                }
            }
            if (kind == 1 || kind == 3) dist = Mathf.Max(0f, dist - 0.12f);           // stop in front of walls / obstacles, do not sink into them
            Vector2 stop = from + dir * dist;
            king.Sr.flipX = dir.x < 0f;
            Bark(Pick("CHAAARGE!", "INSPECTION!", "HOLD STILL!", "RAAAH! REGULATIONS!", "FOR THE WARDERS!"), 1f, true);
            DrillFx.Warn(Ctx.Host, from + dir * 0.35f, stop, warn, dir, Ctx.Player, 0.34f);
            for (float t = 0f; t < warn && !aborted && !stationFailed; t += Time.deltaTime) { king.Pos = from + Random.insideUnitCircle * 0.02f; yield return null; }
            king.Pos = from;
            if (aborted || stationFailed) yield break;
            king.Harmless = false; king.Credited = false;
            king.Play("dash", 18f);
            Sfx.Play("ram_charge", king.Pos);
            float dur = Mathf.Max(0.05f, dist / speed), dustAt = 0f;
            for (float t = 0f; t < dur && !aborted && !stationFailed; t += Time.deltaTime)
            {
                king.Pos = Vector2.Lerp(from, stop, t / dur);
                king.Sr.transform.rotation = Quaternion.Euler(0f, 0f, -8f * Mathf.Sign(dir.x));
                if (t >= dustAt) { dustAt = t + 0.07f; SpriteVfx.Spawn("m3_dash_dust", king.Pos, 1.1f, Layer, king.Sr.sortingOrder - 1, dir.x < 0f, 18f, false); }
                if (king.Credited && (king.Body - PlayerFeet).magnitude > 1f) king.Credited = false;
                yield return null;
            }
            king.Pos = stop;
            king.Sr.transform.rotation = Quaternion.identity;
            king.Harmless = true;
            if (aborted || stationFailed) yield break;
            bool hit = false;
            // stuck time 5.0 / 4.4 / 3.8 s by anger level (generous so the mortar can be timed)
            if (kind == 2) { yield return StuckInHay(hay, dir, Mathf.Lerp(StuckCalm, StuckAngry, anger / 2f)); hit = lastStuckHit; }
            else if (kind == 1 || kind == 3) yield return Bonk(mort, dir);
            else { Sfx.Play("heavy_land", king.Pos, 0.6f); yield return Wait(0.5f); }
            if (aborted || stationFailed || kingHp <= 0) yield break;
            if (kind == 2 && kingHp <= 2 && !hit) yield return ShockStomp();
            else if (hit) yield return Wait(0.4f);
            if (!hit) yield return Recover();                                         // hugging a wall / corner / obstacle -> hop back toward the yard centre
        }

        private bool lastStuckHit;
        private const float StuckCalm = 5.0f, StuckAngry = 3.8f;

        /// <summary>Ammo crate (currently shown) at point q.</summary>
        private bool CrateAt(Vector2 q)
        {
            foreach (var c in crates)
                if (c.Sr.enabled && Mathf.Abs(q.x - c.Feet.x) < 0.28f && q.y > c.Feet.y - 0.15f && q.y < c.Feet.y + 0.4f) return true;
            return false;
        }

        /// <summary>Cramped spot — near the yard edge or near a mortar / crate / intact hay pile (easy to get stuck on collider corners).</summary>
        private bool Cramped(Vector2 p, float r = 0.7f)
        {
            if (p.x < A.xMin + 0.9f || p.x > A.xMax - 0.9f || p.y < A.yMin + 0.7f || p.y > A.yMax - 1.05f) return true;
            foreach (var m in mortars) if (m.Sr.enabled && (p - m.Feet).sqrMagnitude < r * r) return true;
            foreach (var c in crates) if (c.Sr.enabled && (p - c.Feet).sqrMagnitude < r * r) return true;
            foreach (var h in hays) if (h.Sr.enabled && !h.Burned && (p - h.Feet).sqrMagnitude < r * r) return true;
            return false;
        }

        /// <summary>After a charge (wall / mortar / crate, miss, or leaving the hay) while standing somewhere cramped -> hop back 0.5–1.3 u toward the
        /// yard centre to open ground, so the next charge never starts from a corner (he used to ram the same corner forever).</summary>
        private IEnumerator Recover()
        {
            if (aborted || stationFailed || kingHp <= 0) yield break;
            Vector2 p = king.Pos;
            if (!Cramped(p)) yield break;
            Vector2 d = (A.center - p).sqrMagnitude > 0.01f ? (A.center - p).normalized : Vector2.down;
            Vector2 to = p;
            for (float s = 1.3f; s >= 0.5f; s -= 0.2f)
            {
                var c = p + d * s;
                c = new Vector2(Mathf.Clamp(c.x, A.xMin + 0.9f, A.xMax - 0.9f), Mathf.Clamp(c.y, A.yMin + 0.7f, A.yMax - 1.05f));
                if (!Cramped(c, 0.55f)) { to = c; break; }
            }
            if ((to - p).sqrMagnitude < 0.01f)
                to = new Vector2(Mathf.Clamp(p.x + d.x, A.xMin + 0.9f, A.xMax - 0.9f), Mathf.Clamp(p.y + d.y, A.yMin + 0.7f, A.yMax - 1.05f));
            king.Play("move", 14f);
            king.Sr.flipX = to.x < p.x;
            Sfx.Play("footstep", p, 0.7f);
            for (float t = 0f; t < 0.35f && !aborted && !stationFailed; t += Time.deltaTime)
            {
                float u = t / 0.35f;
                king.Pos = Vector2.Lerp(p, to, u);
                king.Lift = Mathf.Sin(u * Mathf.PI) * 0.22f;
                yield return null;
            }
            king.Lift = 0f;
            king.Pos = to;
            Sfx.Play("heavy_land", to, 0.45f);
            SpriteVfx.Spawn("m3_dash_dust", to, 1f, Layer, Order - 1, false, 16f, false);
            king.Play("idle", 6f);
        }

        /// <summary>INTACT (unburnt) hay pile at point q.</summary>
        private Haystack HayAt(Vector2 q)
        {
            foreach (var h in hays)
                if (h.Sr.enabled && !h.Burned && Mathf.Abs(q.x - h.Feet.x) < 0.42f && q.y > h.Feet.y - 0.15f && q.y < h.Feet.y + 0.45f) return h;
            return null;
        }

        private Mortar MortarAt(Vector2 q)
        {
            foreach (var m in mortars)
                if (m.Sr.enabled && Mathf.Abs(q.x - m.Feet.x) < 0.32f && q.y > m.Feet.y - 0.15f && q.y < m.Feet.y + 0.5f) return m;
            return null;
        }

        /// <summary>Head-first into the hay: body hidden, only kicking legs, crown knocked off; wait for the timer or a hit.</summary>
        private IEnumerator StuckInHay(Haystack h, Vector2 dir, float seconds)
        {
            lastStuckHit = false;
            kingStuck = true; kingHitNow = false; stuckIn = h;
            stuckUntil = Time.time + seconds;
            king.Pos = h.Feet + Vector2.down * 0.05f;
            Sfx.Play("heavy_land", h.Feet, 0.8f);
            Sfx.Play("drill_whoosh", h.Feet, 0.7f);
            SpriteVfx.Spawn("drill/fx_hayburst", h.Feet + Vector2.up * 0.3f, 1.1f, Layer, Order + 2, false, 18f, false);
            CameraFollow.KickMain(dir, 0.1f, 0.3f);
            h.Shake = 1f;
            king.SetAlpha(0f);
            legs.enabled = true;
            crown.enabled = true; crownOff = true; crownAt = Time.time;
            crownFrom = h.Feet + Vector2.up * 0.5f;
            crownTo = h.Feet + new Vector2(dir.x >= 0f ? 0.75f : -0.75f, -0.3f);
            Bark(Pick("MMMPHF! *muffled* I MEANT TO DO THAT!", "MMF?! WHO TURNED OFF THE LIGHTS?!", "*muffled screaming in military*"), 2.4f, true);
            DrillBoard.Say("HE'S STUCK! FIRE!", Green);
            // a shot only counts if it LANDS while he is still stuck (Land decides) — no extension
            while (!kingHitNow && !aborted && !stationFailed && Time.time < stuckUntil) yield return null;
            kingStuck = false;
            legs.enabled = false;
            if (aborted || stationFailed) yield break;
            if (kingHitNow) { lastStuckHit = true; yield return Launched(h.Feet + Vector2.up * 0.2f); stuckIn = null; yield break; }
            stuckIn = null;
            king.Pos = h.Feet + Vector2.down * 0.35f;
            king.SetAlpha(1f);
            if (h == activeHay && ++hayStucks >= 2) ScatterHay(h);
            king.Play("dashend", 14f, false);
            h.Shake = 1f;
            SpriteVfx.Spawn("drill/fx_hayburst", h.Feet + Vector2.up * 0.3f, 0.9f, Layer, Order + 2, false, 18f, false);
            Sfx.Play("drill_whoosh", h.Feet, 0.6f);
            CrownHome();
            Bark(Pick("PFAH! HAY IN MY GEARS!", "...Nobody saw that.", "TOO SLOW, recruit! I've eaten worse hay!"), 2f, true);
            yield return Wait(0.5f);
        }

        /// <summary>Ramming a WALL / MORTAR only staggers 0.6 s (not a shooting chance). Ramming a loaded mortar knocks the pot out.</summary>
        private IEnumerator Bonk(Mortar m, Vector2 dir)
        {
            Sfx.Play("lg_wall_crash", king.Pos);
            CameraFollow.KickMain(dir, 0.1f, 0.25f);
            SpriteVfx.Spawn("duo_quake", king.Pos, 0.8f, Layer, Order - 1, false, 16f, false);
            if (m != null)
            {
                m.Kick = 1f; Sfx.Play("hit_wood", m.Feet, 0.6f);
                if (m.Loaded)
                {
                    m.Loaded = false;
                    if (aimMortar == m) CancelAim(null);
                    shellAt = Time.time + 1f;
                    SpriteVfx.Spawn("duo_flame", m.Muzzle, 0.45f, Layer, Order + 3, false, 18f, false);
                    Sfx.Play("gas_hiss", m.Muzzle, 0.6f);
                    DrillBoard.Say("HE SMASHED YOUR FIRE POT!", Red);
                    Bark("Loaded mortars are CONFISCATED!", 2f, true);
                }
            }
            else Bark(Pick("BONK. ...Walls don't count.", "Who PUT that there?!", "Ow. Structural OW. Still standing!"), 1.6f, true);
            king.Play("idle", 3f);
            for (float t = 0f; t < 0.6f && !aborted && !stationFailed; t += Time.deltaTime)
            {
                king.Sr.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 12f) * 8f * (1f - t / 0.6f));
                yield return null;
            }
            king.Sr.transform.rotation = Quaternion.identity;
        }

        private void StopStars() { if (kingStars != null) kingStars.Stop(); kingStars = null; }

        private void CrownHome()
        {
            if (!crownOff) return;
            crownOff = false; crownAt = Time.time;
            crownFrom = crown.transform.position;
        }

        /// <summary>Hit: Burlap is flung spinning twice and lands toward the yard centre.</summary>
        private IEnumerator Launched(Vector2 origin)
        {
            legs.enabled = false;
            king.SetAlpha(1f);
            king.Pos = origin;
            Vector2 to = origin + (A.center - origin).normalized * 1.4f;
            to = new Vector2(Mathf.Clamp(to.x, A.xMin + 0.6f, A.xMax - 0.6f), Mathf.Clamp(to.y, A.yMin + 0.5f, A.yMax - 0.9f));
            king.Play("dashend", 10f, false);
            for (float t = 0f; t < 0.6f; t += Time.deltaTime)
            {
                float u = t / 0.6f;
                king.Pos = Vector2.Lerp(origin, to, u);
                king.Lift = Mathf.Sin(u * Mathf.PI) * 1.3f;
                if (king.Sr != null) king.Sr.transform.rotation = Quaternion.Euler(0f, 0f, -720f * u * Mathf.Sign(to.x - origin.x + 0.001f));
                yield return null;
            }
            king.Lift = 0f;
            king.Pos = to;
            if (king.Sr != null) king.Sr.transform.rotation = Quaternion.identity;
            Sfx.Play("heavy_land", to);
            SpriteVfx.Spawn("m3_dash_dust", to, 1.3f, Layer, Order - 1, false, 16f, false);
            CameraFollow.KickMain(Vector2.down, 0.1f, 0.3f);
            CrownHome();
            if (kingHp > 0)
                Bark(kingHp == 2 ? "MY HAY?! Now I'm ANGRY. Angry is FASTER!" : "TWO?! No more Mister Nice Sergeant!", 3f, true);
            yield return Wait(0.6f);
        }

        // ------------------------------------------------------------------ mortar: fires exactly at the aim ring (no tracking)
        private int shellsInFlight;

        private void Fire(Mortar m)
        {
            if (!artilleryOn || !m.Loaded || phase != Phase.Running) return;
            Vector2 target = aimPoint;
            CancelAim(null);
            m.Loaded = false; m.Kick = 1f;
            shellAt = Time.time + 1f;                                                   // new pot at a crate 1 s later
            Sfx.Play("duo_mortar", m.Muzzle);
            CameraFollow.KickMain(Vector2.down, 0.05f, 0.2f);
            var smoke = SpriteVfx.Spawn("drill/fx_snuff", m.Muzzle + Vector2.up * 0.1f, 1.6f, Layer, Order + 3, false, 16f, false);
            if (smoke != null) smoke.Tint(new Color(1f, 0.95f, 0.85f, 0.9f));
            SpriteVfx.Spawn("m3_dash_dust", m.Feet, 1f, Layer, Order - 1, false, 16f, false);
            Ctx.Run(Shell(m, target));
        }

        /// <summary>The pot arcs for 1.2 s to a FIXED POINT; a red ring marks the landing (so Ren can avoid it).</summary>
        private IEnumerator Shell(Mortar m, Vector2 target)
        {
            Vector2 from = m.Muzzle;
            var sr = TrapArt.MakeRenderer(Ctx.Host.transform, "Drill Mortar Round", from, Ctx.Player, 4);
            sr.sprite = ShellSprite;
            FxLighting.MakeUnlit(sr);
            sr.transform.localScale = new Vector3(1.1f, 1.1f, 1f);
            float trailAt = 0f;
            var sh = TrapArt.MakeRenderer(Ctx.Host.transform, "Drill Round Shadow", from, Ctx.Player, -6);
            sh.sprite = DrillChalk.Shadow();
            var ret = TrapArt.MakeRenderer(Ctx.Host.transform, "Drill Impact Mark", target, Ctx.Player, -5);
            ret.sprite = DrillChalk.Reticle();
            ret.color = new Color(1f, 0.4f, 0.3f, 0.9f);
            FxLighting.MakeUnlit(ret);
            shellFx.Add(sr.gameObject); shellFx.Add(sh.gameObject); shellFx.Add(ret.gameObject);
            const float Peak = 2.4f;
            bool whistled = false;
            shellsInFlight++;
            for (float t = 0f; t < ShellFlight; t += Time.deltaTime)
            {
                if (!artilleryOn || sr == null) { shellsInFlight = Mathf.Max(0, shellsInFlight - 1); Kill(sr, sh, ret); yield break; }
                float u = t / ShellFlight;
                Vector2 ground = Vector2.Lerp(from, target, u);
                float h = 4f * Peak * u * (1f - u);
                sr.transform.position = ground + Vector2.up * h;
                sr.transform.rotation = Quaternion.Euler(0f, 0f, -u * 540f);
                if (t >= trailAt) { trailAt = t + 0.07f; SpriteVfx.Spawn("duo_flame", (Vector2)sr.transform.position, 0.22f, Layer, Order + 3, false, 22f, false); }   // fire trail
                sh.transform.position = ground;
                float ss = Mathf.Lerp(0.6f, 1.4f, u);
                sh.transform.localScale = new Vector3(ss, ss, 1f);
                float rs = Mathf.Lerp(1.5f, 0.9f, u) * (1f + 0.06f * Mathf.Sin(Time.time * 25f));
                ret.transform.localScale = new Vector3(rs, rs * 0.6f, 1f);
                if (!whistled && u > 0.3f) { whistled = true; Sfx.Play("drill_shell_whistle", target, 0.8f); }
                yield return null;
            }
            shellsInFlight = Mathf.Max(0, shellsInFlight - 1);
            Kill(sr, sh, ret);
            Land(target);
        }

        private void Kill(params SpriteRenderer[] list)
        {
            foreach (var s in list) if (s != null) { shellFx.Remove(s.gameObject); Object.Destroy(s.gameObject); }
        }

        private void Land(Vector2 at)
        {
            Sfx.Play("keg_blast", at, 0.7f);                                          // fire pot breaks = burst of flame
            SpriteVfx.Spawn("duo_keg_blast", at + Vector2.up * 0.1f, 0.55f, Layer, Order + 3, false, 20f, false);
            var lick = SpriteVfx.Spawn("duo_flame", at, 0.4f, Layer, Order + 1, false, 12f, false);
            if (lick != null) lick.Looping(1.2f);
            CameraFollow.KickMain(Vector2.down, 0.08f, 0.25f);
            if (phase == Phase.Running && !frozen && (PlayerFeet - at).magnitude < 0.5f)
            {
                Struck(at + Vector2.up * 0.3f, 3f, null);
                if (!stationFailed) Bark("FRIENDLY FIRE! Stand CLEAR of the target, genius!", 2.2f, true);
            }
            if (!artilleryOn || kingHp <= 0 || phase != Phase.Running) return;
            if (kingStuck && !kingHitNow && stuckIn != null && (at - HayTarget(stuckIn)).magnitude < 0.6f)
            {
                kingHitNow = true;
                kingHp--;
                int got = 3 - kingHp;
                DrillBoard.Goal = "HIT BURLAP " + got + " / 3"; DrillBoard.GoalFrac = got / 3f;
                DrillBoard.Say(kingHp > 0 ? "DIRECT HIT! THE HAY IS BURNING!" : "SERGEANT DOWN!", Gold);
                SpriteVfx.Spawn("drill/fx_hayburst", HayTarget(stuckIn) + Vector2.up * 0.3f, 1.8f, Layer, Order + 3, false, 18f, false);
                SpriteVfx.Spawn("duo_keg_blast", HayTarget(stuckIn) + Vector2.up * 0.2f, 0.8f, Layer, Order + 3, false, 20f, false);
                Sfx.Play("blunt_strike", king.Pos);
                Sfx.Play("break_wood", king.Pos, 0.7f);
                king.Flash(new Color(1f, 0.85f, 0.4f), 0.35f);
                DrillFx.HitStop(Ctx.Host, 0.12f);
                CameraFollow.KickMain(Vector2.up, 0.14f, 0.35f);
                Burn(stuckIn);
                return;
            }
            if (!kingStuck && (king.Pos - at).magnitude < 0.8f)
            {
                // he is free: swats the pot away
                king.Play("dashend", 18f, false);
                king.Flash(new Color(0.7f, 0.9f, 1f), 0.25f);
                Sfx.Play("parry", king.Pos, 0.7f);
                DrillBoard.Say("DEFLECTED! WAIT TILL HE'S STUCK", Red);
                Bark(Pick("HA! Swatted! I have HANDS, you know! Big puffy ones!", "Not while I'm LOOKING!", "Hot hot HOT - and BACK at you!"), 2.2f, true);
                Ctx.Run(IdleLater(0.5f));
                return;
            }
            // fire landing on hay while he is NOT stuck in it = burns the bait (new pile elsewhere after 2.4 s)
            if (activeHay != null && !activeHay.Burned && (at - HayTarget(activeHay)).magnitude < 0.6f)
            {
                DrillBoard.Say("YOU BURNED THE BAIT! WAIT TILL HE'S STUCK", Red);
                Burn(activeHay);
                if (Time.time >= nextLine) Bark(Pick("You torched my HAY! ...I'll get more. I ALWAYS get more.", "That was a perfectly good haystack!"), 2.2f, false);
                return;
            }
            DrillBoard.Say("WIDE!", Red);
            if (Time.time >= nextLine) Bark(Pick("Missed by a MILE! Well. A yard. MY yard.", "Is that a SHELL or a SUGGESTION?", "Aim for the LEGS, recruit! The kicking ones!"), 2f, false);
        }

        /// <summary>A hit burns the hay pile: flames for 2.5 s, then an ash heap (cannot get stuck in it, can walk through).</summary>
        private void Burn(Haystack h)
        {
            if (h == null || h.Burned) return;
            h.Burned = true;
            h.Shake = 1.5f;
            if (h == activeHay) { activeHay = null; hayNext = Time.time + 2.4f; }   // new pile drops elsewhere
            Sfx.Play("trap_fire", h.Feet, 0.9f);
            h.Flame = SpriteVfx.Spawn("duo_flame", h.Feet + Vector2.up * 0.05f, 1f, Layer, Order + 2, false, 12f, false);
            if (h.Flame != null) h.Flame.Looping(2.5f);
            Ctx.Run(Char(h));
        }

        private IEnumerator Char(Haystack h)
        {
            for (float t = 0f; t < 1.2f && h.Burned && artilleryOn; t += Time.deltaTime)
            {
                float u = t / 1.2f;
                h.Sr.color = Color.Lerp(Color.white, new Color(0.35f, 0.25f, 0.2f), u);
                yield return null;
            }
            if (!h.Burned || !artilleryOn) yield break;
            if (burntHay != null) { h.Sr.sprite = burntHay; h.Sr.color = Color.white; }
            h.Blocker.SetActive(false);
            // ash fades and disappears (the yard always has just 1 hay pile, no clutter)
            for (float t = 0f; t < 2.5f && h.Burned && artilleryOn; t += Time.deltaTime) yield return null;
            for (float t = 0f; t < 1f && h.Burned && artilleryOn; t += Time.deltaTime) { h.Sr.color = new Color(1f, 1f, 1f, 1f - t); yield return null; }
            if (h.Burned && h != activeHay) { h.Sr.enabled = false; h.Sr.color = Color.white; }
        }

        private Sprite ShellSprite => fireShell != null ? fireShell : (baleFrames != null && baleFrames.Length > 0 ? baleFrames[0] : null);

        // ----------------------------------------------------------------- HAY DROPS ONE PILE AT A TIME
        /// <summary>Pick a drop spot: inside the yard (minus margins), ≥ 1.8 u from both mortars (relaxed down to 1.2 if cramped), ≥ 1.1 from crates,
        /// ≥ 1.2 from Ren, ≥ 1.0 from Burlap, ≥ 1.6 from the previous spot, not on an ash heap. Prefer spots placed in Tiled (`role` hay) if valid.</summary>
        private Vector2 PickHaySpot()
        {
            var cand = new List<Vector2>();
            for (float x = A.xMin + 1.0f; x <= A.xMax - 1.0f; x += 0.3f)
                for (float y = A.yMin + 0.9f; y <= A.yMax - 0.95f; y += 0.3f) cand.Add(new Vector2(x, y));
            for (float gap = HayMortarGap; gap >= HayMortarGapMin - 0.001f; gap -= 0.2f)
            {
                var ok = new List<Vector2>();
                var preferred = new List<Vector2>();
                foreach (var c in haySpots) if (HaySpotOk(c, gap)) preferred.Add(c);
                foreach (var c in cand) if (HaySpotOk(c, gap)) ok.Add(c);
                if (preferred.Count > 0 && Random.value < 0.5f) return preferred[Random.Range(0, preferred.Count)];
                if (ok.Count > 0) return ok[Random.Range(0, ok.Count)];
                if (preferred.Count > 0) return preferred[Random.Range(0, preferred.Count)];
            }
            // no spot qualifies: the one farthest from the mortars
            Vector2 best = A.center; float bd = -1f;
            foreach (var c in cand)
            {
                float d = float.MaxValue;
                foreach (var m in mortars) d = Mathf.Min(d, (c - m.Feet).magnitude);
                if (d > bd) { bd = d; best = c; }
            }
            return best;
        }

        private bool HaySpotOk(Vector2 c, float mortarGap)
        {
            if (c.x < A.xMin + 0.95f || c.x > A.xMax - 0.95f || c.y < A.yMin + 0.85f || c.y > A.yMax - 0.9f) return false;
            foreach (var m in mortars) if ((c - m.Feet).magnitude < mortarGap) return false;
            foreach (var k in crates) if ((c - k.Feet).magnitude < 1.1f) return false;
            if ((c - PlayerFeet).magnitude < 1.2f) return false;
            if ((c - king.Pos).magnitude < 1.0f) return false;
            if ((c - lastHaySpot).magnitude < 1.6f) return false;
            foreach (var h in hays) if (h.Sr.enabled && h.Burned && (c - h.Feet).magnitude < 0.8f) return false;
            return true;
        }

        /// <summary>0.9 s shadow warning, then a hay pile drops on the chosen spot (reuses one of the yard's piles; a pile still burning to ash is left alone).</summary>
        private IEnumerator HayDrop()
        {
            hayDropping = true;
            Haystack h = null;
            foreach (var x in hays) if (!x.Sr.enabled) { h = x; break; }
            if (h == null) foreach (var x in hays) if (x.Burned) { h = x; break; }
            if (h == null) { hayDropping = false; hayNext = Time.time + 1f; yield break; }
            Vector2 spot = PickHaySpot();
            lastHaySpot = spot;
            var shadow = TrapArt.MakeRenderer(Ctx.Host.transform, "Drill Hay Shadow", spot, Ctx.Player, -6);
            shadow.sprite = DrillChalk.Shadow();
            FxLighting.MakeUnlit(shadow);
            shellFx.Add(shadow.gameObject);
            Sfx.Play("drill_whoosh", spot, 0.5f);
            DrillBoard.Say("FRESH HAY INCOMING!", Sky);
            for (float t = 0f; t < 0.9f && artilleryOn && phase == Phase.Running; t += Time.deltaTime)
            {
                float s = Mathf.Lerp(0.6f, 1.6f, t / 0.9f);
                shadow.transform.localScale = new Vector3(s, s * 0.7f, 1f);
                shadow.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.3f, 0.75f, t / 0.9f));
                yield return null;
            }
            shellFx.Remove(shadow.gameObject);
            Object.Destroy(shadow.gameObject);
            hayDropping = false;
            if (!artilleryOn || phase != Phase.Running || kingHp <= 0) yield break;
            h.Feet = spot;
            h.Burned = false;
            if (h.Flame != null) { h.Flame.Stop(); h.Flame = null; }
            if (hayFresh != null) h.Sr.sprite = hayFresh;
            h.Sr.color = Color.white;
            h.Shake = 0f;
            h.Drop = 1f;                                                              // AnimateArtillery handles the fall + dust
            h.Sr.enabled = true;
            h.Blocker.transform.position = spot + Vector2.up * 0.1f;
            h.Blocker.SetActive(true);
            activeHay = h;
            hayStucks = 0;
        }

        /// <summary>Burlap dives into the same pile twice without being shot -> he tears it apart (new pile elsewhere, away from hard-to-shoot spots).</summary>
        private void ScatterHay(Haystack h)
        {
            if (h == null || h.Burned || h != activeHay) return;
            SpriteVfx.Spawn("drill/fx_hayburst", h.Feet + Vector2.up * 0.3f, 1.6f, Layer, Order + 3, false, 18f, false);
            Sfx.Play("drill_whoosh", h.Feet, 0.8f);
            h.Sr.enabled = false;
            h.Blocker.SetActive(false);
            activeHay = null;
            hayNext = Time.time + 1.2f;
            DrillBoard.Say("HE TORE THE HAY APART!", Red);
            Bark(Pick("BAH! This hay is DEFECTIVE! Bring me ANOTHER!", "*rrrip* ...New hay. NOW.", "I am NOT falling for THAT one again!"), 2.2f, true);
        }

        private IEnumerator IdleLater(float s) { yield return new WaitForSeconds(s); if (king != null && phase == Phase.Running && !kingStuck) king.Play("idle", 4f); }

        /// <summary>Stomp: shockwave with 3 gaps (step into a gap / roll through).</summary>
        private IEnumerator ShockStomp()
        {
            Bark(Pick("STOMP DRILL! Find the gaps!", "Feel that? That's DISCIPLINE!"), 1.4f, true);
            king.Play("dashend", 14f, false);
            yield return Wait(0.6f);
            if (aborted || stationFailed) yield break;
            Sfx.Play("duo_slam", king.Pos);
            CameraFollow.KickMain(Vector2.down, 0.07f, 0.3f);
            SpriteVfx.Spawn("duo_quake", king.Pos, 1.4f, Layer, Order - 1, false, 14f, false);
            var w = Shockwave.Spawn(king.Pos, Mathf.Max(A.width, A.height) * 0.95f, 1.6f, 0f, Order - 1, new Color(1f, 0.8f, 0.5f, 0.95f));
            w.knockback = 0f;
            float g = Random.Range(0f, 120f); w.gapAngles = new[] { g, g + 120f, g + 240f }; w.gapWidth = 38f;
            w.onHit = hp => Struck(king.Pos, 4f, null);
            yield return Wait(0.6f);
            king.Play("idle", 4f);
        }

        // ------------------------------------------------------------------ guidance
        private void ArtilleryGuide()
        {
            int got = 3 - kingHp;
            Vector2? target = null;
            string obj, label;
            if (aimMortar != null)
            {
                obj = kingStuck ? "FIRE (T) when the reticle is on him!" : "Lure Burlap into the haystack - fire only counts while he's STUCK.";
                label = kingStuck ? "Fire!" : "Lure Burlap into the haystack";
            }
            else if (AnyLoaded)
            {
                Mortar lm = null; foreach (var m in mortars) if (m.Loaded) lm = m;
                obj = "Press T at the loaded mortar to aim.";
                label = "Aim the loaded mortar";
                if (lm != null) target = lm.Feet + Vector2.up * 0.75f;
            }
            else if (carryingShell)
            {
                Mortar best = null; float bd = float.MaxValue;
                foreach (var m in mortars) { float d = (m.Feet - PlayerFeet).sqrMagnitude; if (d < bd) { bd = d; best = m; } }
                obj = "Load the fire pot into a mortar (T). Slow, no rolling!";
                label = "Load a mortar";
                if (best != null) target = best.Feet + Vector2.up * 0.75f;
            }
            else if (shellCrate >= 0 && shellCrate < crates.Count)
            {
                obj = "Grab the fire pot from the glowing crate.";
                label = "Grab a fire pot";
                target = crates[shellCrate].Feet + Vector2.up * 0.7f;
            }
            else { obj = "A new fire pot is coming..."; label = "Wait for a fire pot"; }
            DrillBoard.Objective = obj;
            Guide(target);
            SetQuest(QuestTitle + " - Live Fire", label, target, got, 3);
        }
    }
}
