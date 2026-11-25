using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// ABSALOM FIGHT GUIDE on the HUD quest board
    /// (QuestGuide — replaces the main quest step during the fight; target arrow + distance point at exactly what to hit). Updated 5 times / s:
    ///   • Phase II (lamps): title "lamps n/4"; target line = the OPEN lamp ("Strike the FIRE lamp - it is open!", arrow on the lamp)
    ///     or the lamp of the element he just cast ("He casts FROST - the FROST lamp opens next") or "Watch which element he casts".
    ///   • Phase I (congregation): title "wave n/3"; target line = the BLESSED cultist first (golden ring, 3 hits), then the nearest cultist.
    ///   • The Still Hour (time stop): read the lances, find the gap, stand in it before colour returns.
    ///   • Always: "Dodge <skill>: <how>" for the skill he just cast (6 s) + flasks left / max (clearing a phase refills them).
    /// Fight over / reset / left the hall -> hand the board back to the main quest.
    /// Called by: Animate (every frame) -> UpdateGuide. Calls: QuestGuide, DrillBoard.
    /// </summary>
    public sealed partial class AbsalomHallMechanism
    {
        private const string GuideTitle = "Absalom";
        private bool questOwned;
        private string curSkill = "";
        private float curSkillAt = -99f, nextGuide;
        private School elemNext = School.Light;                                         // lamp phase: element of the "lamp-opening" skill being cast (set by ElementSkill)
        private float elemNextAt = -99f;
        private int waveNow = -1;                                                       // current congregation wave (0..2), set by .Congregation.cs

        /// <summary>How to dodge each skill (in-game English text), matching the "Dodge:" notes at the top of each school file.</summary>
        private static readonly Dictionary<string, string> DodgeTips = new Dictionary<string, string>
        {
            { "SUNLANCE", "step out of the gold lines - stand in the gap between two" },
            { "HALO RING", "slip through the 3-orb gap, or roll through the ring" },
            { "SUNFALL", "leave the gold circle before the pillar lands" },
            { "JUDGEMENT RAY", "outrun the sweep, get behind him, or roll through the beam" },
            { "ARCANE BLASTER", "change direction between volleys - they lead your run" },
            { "PYRE LINES", "stand between the red lines - the fire walls stay 3 s" },
            { "CINDER RAIN", "leave each marked circle - the pools burn for 3 s" },
            { "RIME THORNS", "step sideways off the thorn path - don't run along it" },
            { "GLACIAL VOLLEY", "sidestep after every blue aim line" },
            { "TEMPEST LATTICE", "leave through the open side before the rods link" },
            { "THUNDERCLAP", "stand between two rings, or step into a ring that just struck" },
            { "UMBRAL ORBS", "pull the orbs away; when one bursts, hold still - the gap faces you" },
            { "ABYSSAL WELL", "walk against the pull and stay out of its heart" },
            { "VOID CANNON", "sidestep the moment the purple aim line flashes white" },
            { "ECLIPSE", "circle WITH the spiral and keep your distance; the last ring opens toward you" },
            { "GRASP OF THE DAMNED", "keep moving and turn often - the pools follow your steps" },
            { "STARFALL", "keep moving across the hall, then leave the gold circle when it locks" },
            { "CATACLYSM DIVE", "run out of the gold circle the moment it locks (flashes white)" },
        };

        /// <summary>Called by Cast() and by skills that skip Cast (Ascension Dive): remember the last skill so the board can teach its dodge.</summary>
        private void NoteSkill(School s, string skill)
        {
            if (string.IsNullOrEmpty(skill)) return;
            curSkill = skill; curSkillAt = Time.time;
        }

        private static string Pretty(string upper)
        {
            if (string.IsNullOrEmpty(upper)) return "";
            var w = upper.ToLowerInvariant().Split(' ');
            for (int i = 0; i < w.Length; i++)
            {
                if (w[i].Length == 0) continue;
                if (i == 0 || (w[i] != "of" && w[i] != "the")) w[i] = char.ToUpperInvariant(w[i][0]) + w[i].Substring(1);
            }
            return string.Join(" ", w);
        }

        private void ClearGuide()
        {
            if (questOwned) { QuestGuide.Clear(); questOwned = false; }
        }

        /// <summary>Every frame (Animate): only active during the fight; outside it, return the board to the main quest.</summary>
        private void UpdateGuide()
        {
            if (state != State.Fight) { ClearGuide(); return; }
            if (Time.unscaledTime < nextGuide) return;
            nextGuide = Time.unscaledTime + 0.2f;
            string tip = curSkill.Length > 0 && Time.time - curSkillAt < 6f && DodgeTips.TryGetValue(curSkill, out var t)
                ? "Dodge " + Pretty(curSkill) + ": " + t : "";
            string flasks = combat != null ? $"Flasks {combat.Potions}/{combat.maxPotions} - every phase refills them" : "";

            if (TimeStop.Active)
            {
                QuestGuide.Set(GuideTitle + " - The Still Hour", "Time is frozen - read where every lance will fly", null);
                QuestGuide.Check("Find the gap in the ring of lances", false, true);
                QuestGuide.Check("Stand in it before the colour returns");
                QuestGuide.Check("Then step off the gold seals under your feet");
                questOwned = true;
                return;
            }

            if (curPhase == LampPhase && !Ctx.Flags.HasAll(PhaseFlag(LampPhase)))
            {
                int broken = BrokenCount(), total = lamps.Count;
                Lamp open = null;
                foreach (var l in lamps) if (!l.Broken && Time.time < l.ExposedUntil) { open = l; break; }
                Lamp next = null;
                if (open == null && Time.time - elemNextAt < 6f && elemNext != School.Light)
                {
                    var c = LampOf(elemNext);
                    if (c != null && !c.Broken) next = c;
                }
                string label;
                Vector2? target = null;
                if (open != null)
                {
                    label = "Strike the " + SchoolName(open.Element) + " lamp - it is open! (" + (LampHp - open.Hp) + "/" + LampHp + " hits)";
                    target = open.Feet;
                }
                else if (next != null)
                {
                    label = "He casts " + SchoolName(elemNext) + " - the " + SchoolName(elemNext) + " lamp opens next";
                    target = next.Feet;
                }
                else label = "Watch which element he casts - its lamp will open";
                QuestGuide.Set(GuideTitle + " - II. The Hierophant (lamps)", label, target, broken, total);
                QuestGuide.Check("Watch his element (lamp colours: fire, frost, storm, shadow)", open != null, open == null);
                QuestGuide.Check("Hit the open lamp " + LampHp + " times before it closes (" + ExposeTime.ToString("0.#") + " s)", false, open != null);
                QuestGuide.Check("Your blade can't reach Absalom while lamps hold him up");
                if (tip.Length > 0) QuestGuide.Check(tip, false, true);
                QuestGuide.Check(flasks);
                questOwned = true;
                return;
            }

            if (curPhase == FlockPhase && !Ctx.Flags.HasAll(PhaseFlag(FlockPhase)))
            {
                int left = AliveCultists(), wave = Mathf.Clamp(waveNow, 0, Waves.Length - 1);
                EnemyAI blessed = null, nearest = null;
                float bd = float.MaxValue;
                foreach (var kv in blessHits) if (kv.Key != null && kv.Key.Health != null && !kv.Key.Health.IsDead) { blessed = kv.Key; break; }
                foreach (var e in cultists)
                {
                    if (e == null || e.Health == null || e.Health.IsDead) continue;
                    float d = ((Vector2)e.transform.position - PlayerFeet).sqrMagnitude;
                    if (d < bd) { bd = d; nearest = e; }
                }
                string label;
                Vector2? target = null;
                if (blessed != null)
                {
                    int hitsLeft = blessHits.TryGetValue(blessed, out var h) ? h : BlessHits;
                    label = "Break the golden ward first - " + (BlessHits - hitsLeft) + "/" + BlessHits + " hits";
                    target = (Vector2)blessed.transform.position;
                }
                else if (nearest != null)
                {
                    label = "Defeat the faithful - " + left + (left == 1 ? " remains" : " remain");
                    target = (Vector2)nearest.transform.position;
                }
                else label = waveNow < 0 ? "The choir gathers - get ready" : "The next wave is coming";
                QuestGuide.Set(GuideTitle + " - I. The Congregation (waves)", label, target, Mathf.Max(0, waveNow), Waves.Length);
                QuestGuide.Check("Wave " + (wave + 1) + " of " + Waves.Length + ": defeat every faithful" + (left > 0 ? " (" + left + " left)" : ""), false, true);
                QuestGuide.Check("Gold aura = blessed: 3 hits break its ward, then it falls like the rest", blessHits.Count == 0 && left > 0, blessed != null);
                if (tip.Length > 0) QuestGuide.Check(tip, false, true);
                QuestGuide.Check(flasks);
                questOwned = true;
                return;
            }

            // between two phases (halo cracking) / phase not implemented
            QuestGuide.Set(GuideTitle, curPhase == 0 ? "He rises - get ready, watch his hands" : "The halo cracks - catch your breath, the next phase begins", null);
            QuestGuide.Check(flasks);
            questOwned = true;
        }
    }
}
