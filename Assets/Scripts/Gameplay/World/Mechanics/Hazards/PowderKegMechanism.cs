using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// powder_keg - POWDER KEG of The Warden Gallery (Map 4). Anything can light it:
    ///   • Ren slashes the keg -> the fuse burns 1.1 s (time to run), then it explodes;
    ///   • a Grapeshot bursting next to it / a Brazen Reliquary mortar landing near it / the Hollow Champion's Soulfire Slam -> short fuse;
    ///   • the Champion can LIFT the keg and throw it at Ren (Lift - the keg leaves its spot, HollowChampionSkills handles the flight).
    /// Explosion: red warning ring for the whole fuse, then area damage to Ren (can be rolled through); traps never hurt enemies or bosses,
    /// nearby kegs chain-explode, nearby statues crack, a short fire pool is left behind. Fight reset -> kegs return to their spots.
    /// Properties: `sprite` (default VFX/props/powder_keg), `damage` (24), `radius` (0.95 units).
    /// </summary>
    public sealed class PowderKegMechanism : Mechanism
    {
        private SpriteRenderer sr;
        private BoxCollider2D col;
        private bool lit;
        private Coroutine fuseCo;
        private readonly List<Health> hits = new List<Health>();

        public override bool ReactsToStrike => true;
        /// <summary>Intact and unlit (the Champion can lift it).</summary>
        public bool Ready => !Done && !lit;
        public Vector2 Center => new Vector2(Rect.center.x, Rect.yMin + 0.12f);
        public new Sprite Art => sr != null ? sr.sprite : null;
        private float Radius => Float("radius", 0.95f);

        public override void Init()
        {
            sr = TrapArt.MakeRenderer(Ctx.Host.transform, "Keg " + Name, new Vector3(Rect.center.x, Rect.yMin + 0.02f, 0f), Ctx.Player, 0);
            sr.sprite = WardenStatueMechanism.Load(Str("sprite", "VFX/props/powder_keg"));
            var go = new GameObject("KegCollider " + Name);
            go.transform.SetParent(Ctx.Host.transform, true);
            go.transform.position = Rect.center;
            col = go.AddComponent<BoxCollider2D>();
            col.size = Rect.size;
            // a straight bullet hitting the keg -> bursts right at the keg (Animate sees a bullet burst nearby -> lights a short fuse)
            MapEncounterDirector.ZoneReset += OnZoneReset;
        }

        public override void OnStruck()
        {
            if (!Ready) return;
            Hints.Show("The fuse catches! Get clear!", 1.6f);
            Ignite(1.1f);
        }

        public override void Animate()
        {
            if (!Ready) return;
            Vector2 c = Center;
            foreach (var p in Projectile.Live)
                if (p != null && p.Exploding && ((Vector2)p.transform.position - c).sqrMagnitude < 0.38f * 0.38f) { Ignite(0.55f); return; }
        }

        /// <summary>Light the fuse: explodes `fuse` seconds later (does nothing if already burning).</summary>
        public void Ignite(float fuse)
        {
            if (!Ready) return;
            lit = true;
            fuseCo = Ctx.Host.StartCoroutine(Fuse(Mathf.Max(0.15f, fuse)));
        }

        /// <summary>The Champion lifts the keg to throw it: the keg leaves its spot (no explosion here).</summary>
        public void Lift()
        {
            if (!Ready) return;
            Done = true;
            if (sr != null) sr.enabled = false;
            if (col != null) col.enabled = false;
        }

        private IEnumerator Fuse(float fuse)
        {
            Sfx.Play("keg_fuse", Center);
            var warn = GroundStrike.Spawn(Center, fuse, Radius, 0f, null, 0, 14f, sr != null ? sr.sortingOrder - 1 : 0, new Color(1f, 0.3f, 0.15f));
            warn.impactSfx = "none";
            for (float t = 0f; t < fuse; t += Time.deltaTime)
            {
                if (sr != null) sr.color = Mathf.Repeat(t * (4f + t * 10f), 1f) < 0.5f ? new Color(1f, 0.45f, 0.35f) : Color.white;
                yield return null;
            }
            Explode();
        }

        private void Explode()
        {
            Done = true; fuseCo = null;
            Vector2 c = Center;
            if (sr != null) { sr.color = Color.white; sr.enabled = false; }
            if (col != null) col.enabled = false;
            Blast(c, Radius, Float("damage", 24f), sr != null ? sr.sortingLayerID : 0, sr != null ? sr.sortingOrder : 0);
        }

        /// <summary>Keg explosion (shared with kegs thrown by the Champion): VFX, damage to Ren, chain reaction, cracked statues, fire pool.</summary>
        internal static void Blast(Vector2 c, float r, float damage, int layer, int order)
        {
            Sfx.Play("keg_blast", c);
            SpriteVfx.Spawn("duo_keg_blast", c + Vector2.up * 0.1f, 1.6f, layer, order + 2, Random.value < 0.5f, 16f, false);
            CameraFollow.KickMain(Vector2.down, 0.09f, 0.3f);
            var mm = MapMechanics.Instance;
            // Ren
            var pc = Object.FindFirstObjectByType<PlayerController>();
            if (pc != null)
            {
                Vector2 d = (Vector2)pc.transform.position - c;
                var ph = pc.GetComponent<Health>();
                if (ph != null && !ph.IsDead && d.magnitude <= r)
                {
                    Health.IncomingRanged = true;
                    try { ph.TakeDamage(damage, d.sqrMagnitude > 0.0001f ? d.normalized : Vector2.down, 3.6f); }
                    finally { Health.IncomingRanged = false; }
                }
            }
            // an exploding keg is a TRAP -> deals NO damage to enemies / bosses (only Ren, chained kegs, cracked statues).
            if (mm != null) { mm.IgniteKegs(c, r + 0.3f, 0.35f); mm.ChipStatues(c, r, 3); }
            BurnPatch.Spawn(c, "duo_soulfire", 0.75f, 3f, layer, order - 3, 0.36f, 4f, new Color(1f, 0.55f, 0.2f), new Color(1f, 0.6f, 0.3f));
        }

        private void OnZoneReset(Rect zone)
        {
            if (Ctx.Host == null) { MapEncounterDirector.ZoneReset -= OnZoneReset; return; }
            if (!zone.Contains(Rect.center)) return;
            if (fuseCo != null) { Ctx.Host.StopCoroutine(fuseCo); fuseCo = null; }
            Done = false; lit = false;
            if (sr != null) { sr.enabled = true; sr.color = Color.white; }
            if (col != null) col.enabled = true;
        }

        internal override void DebugSolve() { }
    }
}
