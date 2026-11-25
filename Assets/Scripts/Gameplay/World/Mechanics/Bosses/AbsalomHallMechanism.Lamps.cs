using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// PHASE II — THE HIEROPHANT (LampPhase). 4 Votive Lamps (tile objects with `art_for` absalom_hall, `role` lamp); EACH LAMP HOLDS
    /// ONE ELEMENT (top-left Fire, top-right Frost, bottom-left Storm, bottom-right Umbral; lamp + tether use the school colour). Absalom can only cast
    /// the schools whose lamps are STILL LIT — breaking a lamp takes that school away (the player chooses which school to remove first).
    /// Loop (RunLampPhase): he casts a school-X skill -> LAMP X OPENS for ExposeTime (tether dims, lamp blinks in the school colour, arrow points at it)
    /// -> meanwhile he keeps pressing with PressureSkill. Other lamps stay shielded (hits = sparks). Each lamp takes LampHp hits; damage regrows after
    /// LampRegen s without hits. From 2 broken lamps -> THE STILL HOUR once (then every remaining lamp opens). All 4 broken -> phase cleared.
    /// Called by: AbsalomHallMechanism.Encounter (p == LampPhase), OnStruck (sword hits -> StruckLamps). Related: .Guide.cs (lamp hints), .Intro.cs (lamps ignite).
    /// </summary>
    public sealed partial class AbsalomHallMechanism
    {
        private sealed class Lamp
        {
            public Vector2 Feet;
            public School Element;
            public SpriteRenderer Sr, Tether, Marker;
            public Light2D Light;
            public int Hp = LampHp;
            public bool Broken;
            public float LastHit = -99f, ShakeUntil, ExposedUntil;
            public GameObject Blocker;
        }

        private readonly List<Lamp> lamps = new List<Lamp>();
        private Sprite lampOk, lampBroken, pixel, arrowSprite;
        private bool stopUsedP1;
        private const int LampHp = 3;
        private const float LampRegen = 6f, ExposeTime = 3.5f;                       // regrow after 6 s unhit; open 3.5 s per cast

        private void InitLamps()
        {
            lampOk = WardenStatueMechanism.Load("VFX/props/hall_votive_lamp");
            lampBroken = WardenStatueMechanism.Load("VFX/props/hall_votive_lamp_broken");
            var arrow = SpriteVfx.Load("drill/fx_arrow");
            if (arrow.Length > 0) arrowSprite = arrow[arrow.Length - 1];
            var at = new List<Vector2>();
            foreach (var a in Art)
                if (a != null && a.Props != null && a.Props.GetString("role", "") == "lamp") at.Add(new Vector2(a.Box.center.x, a.Box.yMin));
            if (at.Count == 0)
                foreach (var part in Str("lamps", "").Split(';'))
                    if (part.Trim().Length > 0) at.Add(Ctx.TilePoint(part.Trim(), A.center));
            if (at.Count == 0)
            {
                at.Add(new Vector2(A.xMin + 2f, A.yMax - 1.2f)); at.Add(new Vector2(A.xMax - 2f, A.yMax - 1.2f));
                at.Add(new Vector2(A.xMin + 2f, A.yMin + 1f)); at.Add(new Vector2(A.xMax - 2f, A.yMin + 1f));
            }
            // assign schools by position — top-left Fire, top-right Frost, bottom-left Storm, bottom-right Umbral
            float midY = 0f; foreach (var p in at) midY += p.y; midY /= at.Count;
            float midX = 0f; foreach (var p in at) midX += p.x; midX /= at.Count;
            bool passed = Ctx.Flags.HasAll(PhaseFlag(LampPhase));
            var used = new HashSet<School>();
            foreach (var p in at)
            {
                School e = p.y >= midY ? (p.x <= midX ? School.Fire : School.Frost) : (p.x <= midX ? School.Storm : School.Umbral);
                if (used.Contains(e)) foreach (var alt in new[] { School.Fire, School.Frost, School.Storm, School.Umbral }) if (!used.Contains(alt)) { e = alt; break; }
                used.Add(e);
                Color ec = SchoolColor(e);
                var l = new Lamp { Feet = p, Element = e };
                l.Sr = TrapArt.MakeRenderer(Ctx.Host.transform, "Votive Lamp (" + e + ")", p, Ctx.Player, 0);
                l.Sr.sprite = lampOk;
                var lg = new GameObject("Votive Light");
                lg.transform.SetParent(l.Sr.transform, false);
                lg.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                l.Light = lg.AddComponent<Light2D>();
                l.Light.lightType = Light2D.LightType.Point;
                l.Light.color = Color.Lerp(ec, Color.white, 0.2f);
                l.Light.pointLightOuterRadius = 1.5f; l.Light.pointLightInnerRadius = 0.1f;
                l.Light.intensity = 0.7f;
                l.Tether = MakeLine("Votive Tether");
                l.Marker = MakeMarker();
                l.Blocker = new GameObject("Votive Lamp Collider");
                l.Blocker.transform.SetParent(Ctx.Host.transform, true);
                l.Blocker.transform.position = p + Vector2.up * 0.05f;
                l.Blocker.AddComponent<BoxCollider2D>().size = new Vector2(0.16f, 0.1f);
                lamps.Add(l);
                if (passed) BreakLamp(l, false);
            }
        }

        private SpriteRenderer MakeMarker()
        {
            var go = new GameObject("Votive Marker");
            go.transform.SetParent(Ctx.Host.transform, true);
            go.transform.rotation = Quaternion.Euler(0f, 0f, -90f);                       // arrow pointing down
            var sr = go.AddComponent<SpriteRenderer>();
            FxLighting.MakeUnlit(sr);
            sr.sprite = arrowSprite;
            sr.sortingLayerID = Layer; sr.sortingOrder = Order + 12;
            sr.enabled = false;
            return sr;
        }

        /// <summary>Thin light strip (1 px stretched) — lamp tether -> Absalom, blessing beam, lightning…</summary>
        private SpriteRenderer MakeLine(string name)
        {
            if (pixel == null)
            {
                var tex = new Texture2D(1, 1) { filterMode = FilterMode.Point };
                tex.SetPixel(0, 0, Color.white); tex.Apply();
                pixel = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0f, 0.5f), 1f);
            }
            var go = new GameObject(name);
            go.transform.SetParent(Ctx.Host.transform, true);
            var sr = go.AddComponent<SpriteRenderer>();
            FxLighting.MakeUnlit(sr);
            sr.sprite = pixel;
            sr.sortingLayerID = Layer; sr.sortingOrder = Order - 3;
            sr.enabled = false;
            return sr;
        }

        private static void Stretch(SpriteRenderer sr, Vector2 a, Vector2 b, float width)
        {
            Vector2 d = b - a;
            sr.transform.position = a;
            sr.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            sr.transform.localScale = new Vector3(d.magnitude, width, 1f);
        }

        private int BrokenCount() { int n = 0; foreach (var l in lamps) if (l.Broken) n++; return n; }

        private Lamp LampOf(School s) { foreach (var l in lamps) if (l.Element == s) return l; return null; }

        private void ResetLamps()
        {
            foreach (var l in lamps)
            {
                l.Broken = false; l.Hp = LampHp; l.LastHit = -99f; l.ExposedUntil = 0f;
                if (l.Sr != null) { l.Sr.sprite = lampOk; l.Sr.color = Color.white; }
                if (l.Light != null) l.Light.enabled = true;
            }
        }

        private void BreakLamp(Lamp l, bool fx)
        {
            l.Broken = true; l.Hp = 0; l.ExposedUntil = 0f;
            if (l.Sr != null) { l.Sr.sprite = lampBroken != null ? lampBroken : lampOk; l.Sr.color = lampBroken != null ? Color.white : new Color(0.4f, 0.4f, 0.45f); }
            if (l.Light != null) l.Light.enabled = false;
            if (l.Tether != null) l.Tether.enabled = false;
            if (l.Marker != null) l.Marker.enabled = false;
            if (!fx) return;
            Vector2 head = l.Feet + Vector2.up * 0.5f;
            Color ec = SchoolColor(l.Element);
            Sfx.Play("glass_break", head, 1f);
            Sfx.Play("ward_break", head, 0.7f);
            var burst = SpriteVfx.Spawn("abs_charge", head, 1.4f, Layer, Order + 3, false, 30f, false);
            if (burst != null) burst.Tint(ec);
            SpriteVfx.Spawn("duo_ice_burst", head, 0.6f, Layer, Order + 3, false, 16f, false);
            CameraFollow.KickMain(Vector2.down, 0.06f, 0.22f);
            abs.Flash(ec, 0.6f);
            int n = BrokenCount();
            DrillBoard.Say(SchoolName(l.Element) + " IS GONE FROM HIM", ec);
            DrillBoard.Goal = "LAMPS " + n + " / " + lamps.Count;
            DrillBoard.GoalFrac = n / (float)Mathf.Max(1, lamps.Count);
            if (n < lamps.Count)
                Bark(n == 1 ? "A lamp. There are others." : n == 2 ? "Hm. Then let us stop pretending time is yours." : "One remains. Choose carefully.", 2.4f);
        }

        /// <summary>Sword hit on a lamp: lamp OPEN -> loses 1 hit; lamp shielded -> sparks + reminder.</summary>
        private void StruckLamps(Vector2 c, float r)
        {
            if (curPhase != LampPhase) return;
            foreach (var l in lamps)
            {
                if (l.Broken) continue;
                Vector2 body = l.Feet + Vector2.up * 0.3f;
                if ((body - c).sqrMagnitude > (r + 0.3f) * (r + 0.3f)) continue;
                if (Time.time < l.ExposedUntil)
                {
                    l.Hp--; l.LastHit = Time.time; l.ShakeUntil = Time.time + 0.2f;
                    Sfx.Play("glass_break", body, 0.45f);
                    SpriteVfx.Spawn("hit", body, 0.6f, Layer, Order + 3, false, 18f, false);
                    if (l.Hp <= 0) BreakLamp(l, true);
                    else DrillBoard.Say("LAMP " + (LampHp - l.Hp) + " / " + LampHp, SchoolColor(l.Element));
                }
                else
                {
                    WardSpark(body);
                    FirstTimeHints.Show("tut_votive", "A lamp is shielded while its thread burns bright. When Absalom casts its element, THAT lamp opens - strike it then.", 5.5f);
                }
                return;
            }
        }

        private void AnimateLamps(float dt)
        {
            bool fight1 = state == State.Fight && curPhase == LampPhase;
            foreach (var l in lamps)
            {
                if (l.Sr == null) continue;
                Vector2 pos = l.Feet;
                if (Time.time < l.ShakeUntil) pos += new Vector2(Random.Range(-0.02f, 0.02f), 0f);
                l.Sr.transform.position = pos;
                if (l.Broken) continue;
                bool open = fight1 && Time.time < l.ExposedUntil;
                Color ec = SchoolColor(l.Element);
                // lamp left alone too long -> burns back to full
                if (fight1 && l.Hp < LampHp && Time.time - l.LastHit > LampRegen && !open)
                {
                    l.Hp = LampHp;
                    Sfx.Play("hymn_pop", l.Feet, 0.5f);
                    DrillBoard.Say("THE " + SchoolName(l.Element) + " LAMP REKINDLES", Red);
                }
                float k = l.Hp / (float)LampHp;
                Color body = Color.Lerp(Color.white, ec, 0.35f);
                if (open) body = Color.Lerp(body, ec * 1.2f, 0.5f + 0.5f * Mathf.Sin(Time.time * 14f));
                l.Sr.color = Color.Lerp(new Color(0.7f, 0.6f, 0.65f), body, k);
                if (l.Light != null) l.Light.intensity = (0.35f + 0.45f * k) * (open ? 1.4f : 1f) + 0.05f * Mathf.Sin(Time.time * 7f);
                if (l.Tether != null)
                {
                    l.Tether.enabled = fight1 && abs != null;
                    if (l.Tether.enabled)
                    {
                        Stretch(l.Tether, l.Feet + Vector2.up * 0.52f, abs.Body + Vector2.up * 0.1f, open ? 0.015f : 0.035f);
                        float a = open ? 0.12f + 0.1f * Mathf.Sin(Time.time * 18f) : 0.65f + 0.15f * Mathf.Sin(Time.time * 5f + l.Feet.x);
                        l.Tether.color = new Color(ec.r, ec.g, ec.b, a);
                    }
                }
                if (l.Marker != null)
                {
                    l.Marker.enabled = open && arrowSprite != null;
                    if (l.Marker.enabled)
                    {
                        float bob = Mathf.Abs(Mathf.Sin(Time.time * 5f)) * 0.08f;
                        l.Marker.transform.position = l.Feet + Vector2.up * (0.95f + bob);
                        l.Marker.color = ec;
                        float s = 1.2f + 0.1f * Mathf.Sin(Time.time * 9f);
                        l.Marker.transform.localScale = new Vector3(s, s, 1f);
                    }
                }
            }
        }

        // ------------------------------------------------------------------ PHA 1
        private School lastElem = School.Light;

        /// <summary>Next school: pick a lamp still lit (avoid repeating the last school if there is a choice).</summary>
        private School NextElement()
        {
            var live = new List<School>();
            foreach (var l in lamps) if (!l.Broken) live.Add(l.Element);
            if (live.Count == 0) return School.Light;
            if (live.Count > 1) live.Remove(lastElem);
            lastElem = live[Random.Range(0, live.Count)];
            return lastElem;
        }

        /// <summary>One skill of the school (random 1 of 2).</summary>
        private IEnumerator ElementSkill(School s, int power)
        {
            bool a = Random.value < 0.5f;
            elemNext = s; elemNextAt = Time.time;                                       // HUD board: a lamp is about to open
            switch (s)
            {
                case School.Fire: yield return a ? PyreLines(power) : CinderRain(power); break;
                case School.Frost: yield return a ? RimeThorns(power) : GlacialVolley(power); break;
                case School.Storm: yield return a ? TempestLattice(power) : Thunderclap(power); break;
                case School.Umbral: yield return a ? UmbralOrbs(power) : AbyssalWell(power); break;
                default: yield return LightSkill(power); break;
            }
        }

        /// <summary>"Pressure" skill while a lamp is open — stronger with the number of broken lamps (tier). Never repeats the last skill.</summary>
        private int lastPressure = -1;

        private IEnumerator PressureSkill(School e, int tier)
        {
            int k, pool = tier <= 0 ? 2 : tier == 1 ? 4 : 7;
            do k = Random.Range(0, pool); while (k == lastPressure && pool > 1);
            lastPressure = k;
            switch (k)
            {
                case 0: yield return LightSkill(1); break;
                case 1: yield return ArcaneBlaster(e, tier >= 2 ? 2 : 1); break;
                case 2: yield return GraspOfTheDamned(tier >= 2 ? 2 : 1); break;
                case 3: yield return VoidCannon(tier >= 2 ? 2 : 1); break;
                case 4: yield return JudgementRay(tier >= 3 ? 2 : 1); break;
                case 5: yield return Eclipse(tier >= 3 ? 2 : 1); break;
                default: yield return AscensionDive(2, true); break;                      // Starfall + Cataclysm
            }
        }

        /// <summary>Lamp phase loop (phase II). Beat: (glide) -> school skill + (tier >= 1) Sunfall layered on top -> that school's lamp opens for ExposeTime,
        /// and while it is open he presses nonstop with PressureSkill (no idle gaps). Tier = lamps broken:
        ///   0: light Light skills / Arcane Blaster · 1: + Grasp, Void Cannon, school skill with Sunfall · 2: + Judgement Ray, Eclipse, Starfall; THE STILL HOUR (once),
        ///   then every lamp opens but he fires a Judgement Ray at once · 3 (last lamp): strong versions, shortest rests.</summary>
        private IEnumerator RunLampPhase()
        {
            ResetLamps();
            stopUsedP1 = false;
            DrillBoard.Goal = "LAMPS 0 / " + lamps.Count; DrillBoard.GoalFrac = 0f;
            DrillBoard.Objective = "Each lamp holds one element. When Absalom casts it, that lamp opens - break all 4.";
            abs.Face(PlayerFeet - abs.Pos);
            Bark("Show me what the Eye made of you.", 2.6f);
            yield return Wait(1.4f);
            while (!aborted && BrokenCount() < lamps.Count)
            {
                int tier = BrokenCount();
                if (tier >= 2 && !stopUsedP1 && Time.time - lastStop > 25f)
                {
                    stopUsedP1 = true;
                    yield return TimeStopRoutine(StopPattern.Ring);
                    if (aborted) yield break;
                    foreach (var l in lamps) if (!l.Broken) l.ExposedUntil = Time.time + ExposeTime + 1.5f;
                    DrillBoard.Objective = "Every lamp is open - strike now!";
                    yield return JudgementRay(2);                                       // no free window — the lamps open but the sweeping ray must still be dodged
                    if (aborted) yield break;
                    continue;
                }
                if (Random.value < 0.6f) yield return Glide(RandomDaisPoint(), 2.6f);
                if (aborted) yield break;
                School e = NextElement();
                if (tier >= 1) StartR(Sunfall(tier >= 2 ? 3 : 2, 0.5f, 0.55f, 9f));     // layered hazards
                yield return ElementSkill(e, 2);
                if (aborted) yield break;
                var lamp = LampOf(e);
                if (lamp != null && !lamp.Broken)
                {
                    lamp.ExposedUntil = Time.time + ExposeTime;
                    elemNextAt = -99f;                                                  // lamp is open -> stop the "opening soon" hint
                    DrillBoard.Objective = "The " + SchoolName(e) + " lamp is open - strike it!";
                    Sfx.Play("tremor_soft", lamp.Feet, 0.5f);
                    float until = lamp.ExposedUntil;
                    yield return Wait(0.25f);
                    if (aborted) yield break;
                    while (!aborted && Time.time < until && !lamp.Broken)
                    {
                        yield return PressureSkill(e, BrokenCount());                     // keep pressing until the lamp closes / breaks
                        if (aborted) yield break;
                    }
                    DrillBoard.Objective = "Each lamp holds one element. When Absalom casts it, that lamp opens.";
                }
                yield return Wait(tier >= 3 ? 0.15f : Random.Range(0.25f, 0.5f));
            }
        }
    }
}
