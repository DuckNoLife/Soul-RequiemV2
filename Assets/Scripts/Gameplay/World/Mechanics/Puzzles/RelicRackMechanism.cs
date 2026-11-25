using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// relic_rack - WEAPON RACK of The Reliquary Lists (Map 4, Reliquary Wardens duo fight). Property `weapon`:
    /// halberd | hammer | flail | axes. Three states:
    ///   Sealed : the weapon is bound by soul chains (dark) - nobody can take it.
    ///   Called : the Hollow Champion CALLS this weapon (rack glows purple, soul fire at its base) -> RACE: Ren gets there first and presses T ("Claim") -> steals the charm
    ///            (RelicBoons) and the Champion arrives empty-handed, dismayed; the Champion gets there first -> he takes the weapon and changes skill set.
    ///   Empty  : empty rack (out of turns until the racks re-forge).
    /// Solid collider at the rack base. Fight reset (ZoneReset) -> Sealed.
    /// </summary>
    public sealed class RelicRackMechanism : Mechanism
    {
        public enum State { Sealed, Called, Empty }
        public State Now { get; private set; } = State.Sealed;
        public string Weapon => Str("weapon", "halberd");
        /// <summary>Where to stand to take the weapon: the field side (rack on the north wall -> stand below; on the south wall -> stand above).</summary>
        public Vector2 Stand { get; private set; }
        /// <summary>Ren just stole it (the Champion reads this to know he missed).</summary>
        public bool ClaimedByRen { get; private set; }

        private SpriteRenderer sr;
        private Sprite full, empty;
        private BoxCollider2D col;
        private SpriteVfx glow;

        public override string DefaultVerb => "Claim";
        public override bool Usable => Now == State.Called;

        public override void Init()
        {
            full = WardenStatueMechanism.Load("VFX/props/relic_rack_" + Weapon);
            empty = WardenStatueMechanism.Load("VFX/props/relic_rack_empty");
            sr = TrapArt.MakeRenderer(Ctx.Host.transform, "Rack " + Name, new Vector3(Rect.center.x, Rect.yMin + 0.02f, 0f), Ctx.Player, 0);
            sr.sprite = full;
            var go = new GameObject("RackCollider " + Name);
            go.transform.SetParent(Ctx.Host.transform, true);
            go.transform.position = Rect.center;
            col = go.AddComponent<BoxCollider2D>();
            col.size = Rect.size;
            var arena = MapMechanics.Instance != null ? MapMechanics.Instance.MechRect("duo_arena") : null;
            float toward = arena.HasValue && arena.Value.center.y > Rect.center.y ? 1f : -1f;
            Stand = new Vector2(Rect.center.x, toward > 0f ? Rect.yMax + 0.2f : Rect.yMin - 0.2f);
            MapEncounterDirector.ZoneReset += OnZoneReset;
            Seal();
        }

        public override void Animate()
        {
            if (sr == null || Now != State.Called) return;
            float k = 0.5f + 0.5f * Mathf.Sin(Time.time * 9f);
            sr.color = Color.Lerp(Color.white, new Color(0.85f, 0.6f, 1f), k);
        }

        private void Seal()
        {
            Now = State.Sealed; ClaimedByRen = false;
            if (sr != null) { sr.sprite = full; sr.color = new Color(0.5f, 0.45f, 0.62f); }
            if (glow != null) { glow.Stop(); glow = null; }
        }

        /// <summary>The Champion calls the weapon: chains open, the race begins.</summary>
        public void Call()
        {
            if (Now != State.Sealed) return;
            Now = State.Called; ClaimedByRen = false;
            if (sr != null) sr.color = Color.white;
            Sfx.Play("rack_unseal", Rect.center);
            glow = SpriteVfx.Spawn("duo_soulfire", new Vector2(Rect.center.x, Rect.yMin), 0.9f, sr != null ? sr.sortingLayerID : 0, (sr != null ? sr.sortingOrder : 0) - 1, false, 12f, false);
            if (glow != null) glow.Looping(9999f);
        }

        /// <summary>Ren presses T.</summary>
        public override void Use()
        {
            if (Now != State.Called) return;
            ClaimedByRen = true;
            Empty();
            Sfx.Play("relic_claim", Rect.center);
            SpriteVfx.Spawn("m3_break", Rect.center + Vector2.up * 0.5f, 0.8f, sr != null ? sr.sortingLayerID : 0, (sr != null ? sr.sortingOrder : 0) + 2, false, 14f, false);
            RelicBoons.Grant(Weapon, Rect.center + Vector2.up * 0.4f);
        }

        /// <summary>The Champion arrived first: takes the weapon. true if taken.</summary>
        public bool TakeByChampion()
        {
            if (Now != State.Called) return false;
            Empty();
            return true;
        }

        private void Empty()
        {
            Now = State.Empty;
            if (sr != null) { sr.sprite = empty; sr.color = Color.white; }
            if (glow != null) { glow.Stop(); glow = null; }
        }

        /// <summary>The racks re-forge their relics (chained again as at the start).</summary>
        public void Rearm() { if (Now == State.Empty) Seal(); }

        /// <summary>The Champion falls: every still-chained rack opens for Ren to take.</summary>
        public void Release() { if (Now == State.Sealed) Call(); }

        private void OnZoneReset(Rect zone)
        {
            if (Ctx.Host == null) { MapEncounterDirector.ZoneReset -= OnZoneReset; return; }
            if (zone.Contains(Rect.center)) { Seal(); RelicBoons.Clear(); }   // Ren falls mid-fight: the charm is lost too
        }

        internal override void DebugSolve() { }
    }
}
