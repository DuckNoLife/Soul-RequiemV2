using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// PHASE I — THE CONGREGATION (FlockPhase). Absalom hovers above the hall "so the faithful may witness"; 3 waves of cultists step out of
    /// the alcoves in the two side walls (`alcoves`). Each wave he BLESSES one cultist (golden aura: immune to damage, the ward breaks after 3 hits).
    /// Each wave sings one element (WaveSchools): wave 1 FIRE, wave 2 FROST + STORM, wave 3 UMBRAL + STORM — the elemental acolytes cast it.
    /// Absalom himself does not attack in this phase; the danger comes from the cultists. Clearing all waves -> PhaseCleared -> phase II (.Lamps.cs).
    /// Called by: AbsalomHallMechanism.Encounter (p == FlockPhase). Calls: MapEncounterDirector.SpawnInZone (cultists), DrillBoard (wave board).
    /// </summary>
    public sealed partial class AbsalomHallMechanism
    {
        private readonly List<Vector2> alcoves = new List<Vector2>();
        private readonly List<EnemyAI> cultists = new List<EnemyAI>();
        private readonly Dictionary<EnemyAI, int> blessHits = new Dictionary<EnemyAI, int>();
        private readonly Dictionary<EnemyAI, SpriteVfx> blessAura = new Dictionary<EnemyAI, SpriteVfx>();
        private const int BlessHits = 3;

        /// <summary>The song (element) of each wave.</summary>
        private static readonly School[][] WaveSchools =
        {
            new[] { School.Fire },
            new[] { School.Frost, School.Storm },
            new[] { School.Umbral, School.Storm },
        };

        /// <summary>Each wave has ELEMENTAL ACOLYTES singing that wave's song (Fire / Frost + Storm / Umbral + Storm) + melee guards in front.
        /// If "Build Map 4 Enemies" has not been run, SpawnWave falls back to Choir_Cutthroat.</summary>
        private static readonly string[][] Waves =
        {
            new[] { "Choir_Cutthroat", "Acolyte_Fire", "Choir_Cutthroat", "Acolyte_Fire" },
            new[] { "Choir_Cutthroat", "Acolyte_Frost", "Bone_Jailer", "Acolyte_Storm" },
            new[] { "Choir_Cutthroat", "Acolyte_Umbral", "Bone_Jailer", "Acolyte_Storm", "Acolyte_Umbral" },
        };

        private void InitAlcoves()
        {
            foreach (var part in Str("alcoves", "").Split(';'))
                if (part.Trim().Length > 0) alcoves.Add(Ctx.TilePoint(part.Trim(), A.center));
            if (alcoves.Count == 0)
            {
                alcoves.Add(new Vector2(A.xMin + 0.3f, A.center.y + 1f)); alcoves.Add(new Vector2(A.xMin + 0.3f, A.center.y - 1f));
                alcoves.Add(new Vector2(A.xMax - 0.3f, A.center.y + 1f)); alcoves.Add(new Vector2(A.xMax - 0.3f, A.center.y - 1f));
            }
        }

        private int AliveCultists()
        {
            int n = 0;
            foreach (var e in cultists) if (e != null && e.Health != null && !e.Health.IsDead) n++;
            return n;
        }

        private void DespawnCultists()
        {
            foreach (var a in blessAura.Values) if (a != null) a.Stop();
            blessAura.Clear(); blessHits.Clear();
            foreach (var e in cultists) if (e != null && e.Health != null && !e.Health.IsDead) Object.Destroy(e.gameObject);
            cultists.Clear();
        }

        // ------------------------------------------------------------------ CONGREGATION PHASE
        /// <summary>Absalom ONLY summons 3 waves and blesses one cultist per wave —
        /// no school skills, no Sunfall / Arcane Blaster / Grasp / Void Cannon, no big moves, no time stop, no standing-still punish.
        /// The danger in this phase comes from the cultists (elemental Acolytes, Choir_Cutthroat, Bone_Jailer).</summary>
        private IEnumerator RunCongregationPhase()
        {
            DrillBoard.Goal = "WAVE 1 / 3"; DrillBoard.GoalFrac = 0f;
            waveNow = -1;                                                               // HUD board per wave
            DrillBoard.Objective = "Defeat the congregation. The blessed one (golden aura) needs 3 hits to break its ward.";
            ClearHazards();
            yield return Glide(home, 2f);
            abs.Face(Vector2.down);
            Bark("Witness, faithful. Witness the seventh.", 2.6f);
            yield return Wait(1.6f);
            for (int w = 0; w < Waves.Length && !aborted && !skipPhase; w++)
            {
                waveNow = w;
                DrillBoard.Goal = "WAVE " + (w + 1) + " / " + Waves.Length;
                DrillBoard.GoalFrac = w / (float)Waves.Length;
                var song = WaveSchools[Mathf.Min(w, WaveSchools.Length - 1)];
                Color sc = SchoolColor(song[0]);
                string names = SchoolName(song[0]) + (song.Length > 1 ? " AND " + SchoolName(song[1]) : "");
                DrillBoard.Finish("THE CHOIR SINGS " + names, sc);
                abs.Flash(sc, 1f);
                yield return SpawnWave(w);
                if (aborted) yield break;
                // he only watches — wait until every cultist falls (the board counts those left)
                while (!aborted && !skipPhase && AliveCultists() > 0)
                {
                    int left = AliveCultists();
                    DrillBoard.Objective = left + (left == 1 ? " faithful remains" : " faithful remain") + (blessHits.Count > 0 ? " - break the golden ward first (3 hits)." : ".");
                    yield return null;
                }
                if (aborted) yield break;
                StopRoutines();
                ClearHazards();
                DrillBoard.Say("WAVE " + (w + 1) + " CLEARED", Green);
                DrillBoard.GoalFrac = (w + 1) / (float)Waves.Length;
                yield return Wait(1.8f);
            }
        }

        private IEnumerator SpawnWave(int w)
        {
            var names = Waves[w];
            Bark(w == 0 ? "Sing, faithful. Sing the names of the six who came before."
                 : w == 1 ? "Candidate One. Candidate Two... every one of them knelt on this floor."
                 : "And now - the seventh name.", 2.8f);
            Sfx.Play("v_chant_cast", A.center, 0.7f);
            var spawned = new List<EnemyAI>();
            for (int i = 0; i < names.Length && !aborted; i++)
            {
                Vector2 at = alcoves[(i + w) % alcoves.Count];
                at += new Vector2(at.x < A.center.x ? 0.25f : -0.25f, Random.Range(-0.15f, 0.15f));     // step out of the alcove
                SpriteVfx.Spawn("duo_soulfire_crimson", at, 0.7f, Layer, Order + 1, false, 14f, false);
                Sfx.Play("teleport_dark", at, 0.6f);
                yield return Wait(0.3f);
                if (aborted) yield break;
                var e = Ctx.Director.SpawnInZone(names[i], at, null);
                if (e == null && names[i] != "Choir_Cutthroat") e = Ctx.Director.SpawnInZone("Choir_Cutthroat", at, null);
                if (e == null) continue;
                e.Provoke();
                cultists.Add(e);
                spawned.Add(e);
            }
            if (aborted || spawned.Count == 0) yield break;
            yield return Wait(0.5f);
            var pick = spawned[Random.Range(0, spawned.Count)];
            if (pick != null && pick.Health != null && !pick.Health.IsDead) yield return Bless(pick);
        }

        /// <summary>Blessing: golden beam from Absalom to the cultist -> aura, immune to damage until struck 3 times (OnStruck -> StruckBlessed counts the hits).</summary>
        private IEnumerator Bless(EnemyAI e)
        {
            abs.Face((Vector2)e.transform.position - abs.Pos);
            abs.Play("strike", 10f, false);
            var beam = MakeLine("Bless Beam");
            beam.enabled = true;
            Sfx.Play("relic_absorb", e.transform.position, 0.7f);
            for (float t = 0f; t < 0.6f && !aborted && e != null; t += Time.deltaTime)
            {
                Stretch(beam, abs.Body + Vector2.up * 0.15f, (Vector2)e.transform.position + Vector2.up * 0.3f, 0.05f);
                beam.color = new Color(1f, 0.9f, 0.55f, 0.8f * (1f - t / 0.6f) + 0.2f);
                yield return null;
            }
            Object.Destroy(beam.gameObject);
            abs.Play("idle", 5f);
            if (aborted || e == null || e.Health == null || e.Health.IsDead) yield break;
            e.Health.DamageTakenMul = 0f;
            blessHits[e] = BlessHits;
            var aura = SpriteVfx.SpawnAttached("duo_aura_gilded", e.transform, 0.75f, Layer, Order - 1, 10f, 999f);
            if (aura != null) blessAura[e] = aura;
            FirstTimeHints.Show("tut_bless", "A blessed foe shrugs off damage. Strike it 3 times to shatter the golden ward.", 4.5f);
        }

        private void StruckBlessed(Vector2 c, float r)
        {
            if (blessHits.Count == 0) return;
            EnemyAI hitOne = null;
            foreach (var kv in blessHits)
            {
                var e = kv.Key;
                if (e == null || e.Health == null || e.Health.IsDead) continue;
                Vector2 body = (Vector2)e.transform.position + Vector2.up * 0.25f;
                if ((body - c).sqrMagnitude <= (r + 0.35f) * (r + 0.35f)) { hitOne = e; break; }
            }
            if (hitOne == null) return;
            int left = blessHits[hitOne] - 1;
            WardSpark((Vector2)hitOne.transform.position + Vector2.up * 0.3f);
            if (left > 0) { blessHits[hitOne] = left; DrillBoard.Say("WARD " + (BlessHits - left) + " / " + BlessHits, Gold); return; }
            blessHits.Remove(hitOne);
            hitOne.Health.DamageTakenMul = 1f;
            if (blessAura.TryGetValue(hitOne, out var a) && a != null) a.Stop();
            blessAura.Remove(hitOne);
            Sfx.Play("ward_break", hitOne.transform.position, 0.9f);
            SpriteVfx.Spawn("duo_ice_burst", (Vector2)hitOne.transform.position + Vector2.up * 0.3f, 0.6f, Layer, Order + 3, false, 16f, false);
            DrillBoard.Say("WARD BROKEN", Gold);
        }

        /// <summary>Standing still too long -> a judgement pillar falls exactly on Ren (bigger, clearly telegraphed). Currently not called by any phase.</summary>
        private IEnumerator Judgement()
        {
            Bark(Pick("Do not hide from judgement.", "Stillness is not prayer, child.", "Move, or be weighed."), 2f);
            yield return Sunfall(1, 0f, 0.62f, 12f);
        }
    }
}
