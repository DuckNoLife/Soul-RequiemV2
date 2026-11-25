using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Status effects bosses put on Ren (instead of damage for wide, hard-to-dodge attacks):
    ///   Slow       – walks slower (PlayerController multiplies SpeedMul).        VFX m3_st_slow (arrows sinking under the feet)
    ///   Silence    – F skills locked (PlayerCombat checks Silenced).              VFX m3_st_silence (seal ring around the body)
    ///   Hemorrhage – cannot drink flasks (HealBlocked).                           VFX m3_st_bleed (dripping blood)
    ///   Daze       – stunned for one beat (Health.Stun).                          VFX stun_stars
    /// A roll (invulnerable) dodges them: no status while Health.Invulnerable. Test god mode (F2) ignores them too.
    /// Added to the player automatically when needed (PlayerStatus.Of). Cleared on respawn.
    /// Also: roll lock, map restraints (disarmed / Mantras sealed / flasks taken), ice floor, wind.
    /// </summary>
    public class PlayerStatus : MonoBehaviour
    {
        private Health hp;
        private PlayerCombat combat;
        private float slowUntil, slowMul = 1f, silenceUntil, healBlockUntil, rollSealUntil, iceUntil;
        private SpriteVfx slowFx, silenceFx, bleedFx;
        private static float nextHint;

        public float SpeedMul => Time.time < slowUntil ? slowMul : 1f;
        public bool Silenced => Time.time < silenceUntil;
        /// <summary>ROLL LOCK (Dash) — the Jackpot Engine's ghost skull touched Ren. The HUD dims the Roll slot + shows a countdown icon.</summary>
        public bool RollSealed => Time.time < rollSealUntil;
        public float RollSealLeft => Mathf.Max(0f, rollSealUntil - Time.time);
        /// <summary>MAP restraints (MapRestraint sets them every frame from flags): disarmed -> no slashing; Mantras sealed -> no skills.
        /// Message text comes from the map properties `disarm_text` / `mantra_seal_text`.</summary>
        public bool Disarmed { get; set; }
        public bool MantrasBound { get; set; }
        public string DisarmText { get; set; } = "Your blade is gone.";
        public string MantraSealText { get; set; } = "Your mantras are sealed.";
        /// <summary>Flasks taken together with the sword (Map 4) -> cannot drink until the sword is back.</summary>
        public bool FlaskTaken { get; set; }
        public string FlaskText { get; set; } = "Your flask is gone. The warders took it.";
        private float nextRestraintHint;
        /// <summary>Show the restraint message (at least 1.5 s apart).</summary>
        public void SayRestraint(string text)
        {
            if (Time.time < nextRestraintHint || string.IsNullOrEmpty(text)) return;
            nextRestraintHint = Time.time + 1.5f;
            Hints.Show(text, 1.6f);
        }
        public bool HealBlocked => Time.time < healBlockUntil;
        /// <summary>Standing on the ICE FLOOR (Phase 3 of The Crystal Vessel) — PlayerController adds inertia (slippery).</summary>
        public bool OnIce => Time.time < iceUntil;
        private Vector2 windVel; private float windUntil;
        /// <summary>WIND pushing Ren (Whiteout blizzard) — PlayerController adds it straight to the velocity until it expires.</summary>
        public Vector2 WindVel => Time.time < windUntil && hp != null && !hp.IsDead ? windVel : Vector2.zero;
        public void Push(Vector2 velocity, float seconds) { windVel = velocity; windUntil = Time.time + seconds; }
        /// <summary>Keep the ice floor state for `seconds` more (called every frame while there is ice). Not blocked by rolls / invulnerability: it is the floor, not an attack.</summary>
        public void Ice(float seconds)
        {
            if (hp == null || hp.IsDead) return;
            iceUntil = Mathf.Max(iceUntil, Time.time + seconds);
        }
        /// Seconds left for each effect (the HUD shows an icon + countdown).
        public float SlowLeft => Mathf.Max(0f, slowUntil - Time.time);
        public float SilenceLeft => Mathf.Max(0f, silenceUntil - Time.time);
        public float HealBlockLeft => Mathf.Max(0f, healBlockUntil - Time.time);

        public static PlayerStatus Of(Component player)
        {
            if (player == null) return null;
            var s = player.GetComponent<PlayerStatus>();
            if (s == null) s = player.gameObject.AddComponent<PlayerStatus>();
            return s;
        }

        private void Awake()
        {
            hp = GetComponent<Health>();
            combat = GetComponent<PlayerCombat>();
            if (hp != null) hp.Revived += _ => ClearAll();
        }

        private bool Blocked => hp == null || hp.IsDead || hp.Invulnerable || (combat != null && combat.GodMode);

        public void Slow(float mul, float seconds, string hint = null)
        {
            if (Blocked) return;
            if (Time.time >= slowUntil || mul < slowMul) slowMul = mul;
            slowUntil = Mathf.Max(slowUntil, Time.time + seconds);
            slowFx = Keep(slowFx, "m3_st_slow", 0f, slowUntil);
            Say(hint);
        }

        public void Silence(float seconds, string hint = "Silenced: your mantras are sealed!")
        {
            if (Blocked) return;
            silenceUntil = Mathf.Max(silenceUntil, Time.time + seconds);
            silenceFx = Keep(silenceFx, "m3_st_silence", 0.32f, silenceUntil);
            Say(hint);
        }

        /// <summary>Lock the roll for `seconds` (no stacking: keeps the furthest end time).</summary>
        public void SealRoll(float seconds, string hint = "Your dash is sealed!")
        {
            if (Blocked) return;
            rollSealUntil = Mathf.Max(rollSealUntil, Time.time + seconds);
            Say(hint);
        }

        public void BlockHeal(float seconds, string hint = "Hemorrhage: potions will not close your wounds!")
        {
            if (Blocked) return;
            healBlockUntil = Mathf.Max(healBlockUntil, Time.time + seconds);
            bleedFx = Keep(bleedFx, "m3_st_bleed", 0.55f, healBlockUntil);
            Say(hint);
        }

        public void Daze(float seconds, string hint = null)
        {
            if (Blocked) return;
            hp.Stun(seconds);
            // no stun stars spawned here — PlayerHurtReactions.UpdateStun spawns them when Health is stunned (avoids 2 rings of stars at different heights)
            Say(hint);
        }

        public void ClearAll()
        {
            slowUntil = silenceUntil = healBlockUntil = rollSealUntil = iceUntil = 0f;
            slowMul = 1f;
            foreach (var f in new[] { slowFx, silenceFx, bleedFx }) if (f != null) f.Stop();
            slowFx = silenceFx = bleedFx = null;
        }

        private int Layer { get { var r = GetComponent<SpriteRenderer>(); return r != null ? r.sortingLayerID : 0; } }
        private int Order { get { var r = GetComponent<SpriteRenderer>(); return r != null ? r.sortingOrder : 2; } }

        /// Keep a looping VFX attached to the player until `until` (create it if missing / expired).
        private SpriteVfx Keep(SpriteVfx fx, string name, float yOffset, float until)
        {
            if (fx != null) { fx.Looping(until - Time.time); return fx; }   // already shown: just extend it
            var v = SpriteVfx.SpawnAttached(name, transform, 0.7f, Layer, Order + 1, 12f, until - Time.time);
            if (v != null) v.transform.localPosition = new Vector3(0f, yOffset, 0f);
            return v;
        }

        private static void Say(string hint)
        {
            if (string.IsNullOrEmpty(hint) || Time.time < nextHint) return;
            Hints.Show(hint, 1.8f);
            nextHint = Time.time + 4f;
        }
    }
}
