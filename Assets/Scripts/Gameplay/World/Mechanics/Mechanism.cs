using SuperTiled2Unity;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Base class of ONE map mechanism (one object in the Tiled "mechanics" layer).
    /// Each kind (Tiled Type) is a subclass, registered in <see cref="MechanismRegistry"/>.
    ///
    /// Lifecycle (called by MapMechanics):
    ///   Init()            once after reading the map
    ///   OnFlagsChanged()  whenever a new flag is set (and once after Init)
    ///   Animate()         every frame, even when Done (effects, glow rings)
    ///   Tick(pos, inside) every frame while NOT Done (inside = the player is inside the object area)
    ///   OnStruck()        the player slashes it (only when ReactsToStrike)
    ///   Use()             the player presses T (only when DefaultVerb != null)
    ///
    /// Adding a new mechanism kind: create a subclass + one line in MechanismRegistry. MapMechanics needs no change.
    /// </summary>
    public abstract class Mechanism
    {
        public string Type { get; private set; }
        public string Name { get; private set; }
        public Rect Rect { get; private set; }
        public SuperCustomProperties Props { get; private set; }
        /// <summary>Finished (solved / destroyed / stopped). Done mechanisms are no longer Ticked.</summary>
        public bool Done { get; internal set; }
        /// <summary>Images placed in Tiled as tile objects (<see cref="MechanismArt"/>): the mechanism's own object first (if it is a
        /// tile object), then the pieces with `art_for` = mechanism name. Empty = old-style mechanism (rectangle + `sprite`).</summary>
        public System.Collections.Generic.IReadOnlyList<MechanismArt> Art => art;
        private readonly System.Collections.Generic.List<MechanismArt> art = new System.Collections.Generic.List<MechanismArt>();

        protected MechanismContext Ctx { get; private set; }

        internal void Bind(string type, string name, Rect rect, SuperCustomProperties props, MechanismContext ctx)
        {
            Type = type; Name = name; Rect = rect; Props = props; Ctx = ctx;
        }

        internal void AddArt(MechanismArt a) { if (a != null) art.Add(a); }

        public virtual void Init() { }
        public virtual void OnFlagsChanged() { }
        /// <summary>A flag was just CLEARED (player fell mid-fight with `reset_on_death`) - a mechanism finished because of that flag reopens.</summary>
        public virtual void OnFlagsReset() { }
        public virtual void Animate() { }
        public virtual void Tick(Vector2 playerPos, bool inside) { }

        // ---- slashes
        public virtual bool ReactsToStrike => false;
        public virtual void OnStruck() { }

        // ---- T key (InteractionSystem)
        /// <summary>Key prompt text ("Ring", "Read"...). null = not usable with T. Tiled can override it with property `verb`.</summary>
        public virtual string DefaultVerb => null;
        /// <summary>Story object: sparkles until first viewed.</summary>
        public virtual bool Clue => false;
        /// <summary>Can T be pressed right now.</summary>
        public virtual bool Usable => !Done;
        public virtual void Use() { }

        // ---- centre-screen panel (notes, letter locks, rose window)
        /// <summary>This mechanism's panel is open (the key prompt becomes "Close").</summary>
        public bool PanelIsOpen => MechanismPanels.Current != null && MechanismPanels.Current.Owner == this;
        /// <summary>Is the open panel still valid (e.g. closes when the player walks away).</summary>
        public virtual bool KeepPanelOpen(Vector2 playerPos) => true;

        // ---- test tools (F5)
        /// <summary>Treat as solved: set flag `sets`. Default for every kind with a sets property.</summary>
        internal virtual void DebugSolve()
        {
            string sets = Str("sets", "");
            if (string.IsNullOrEmpty(sets) || Done) return;
            Done = true;
            Ctx.Flags.Set(sets);
        }

        // ---- reading Tiled properties
        protected string Str(string key, string fallback) => Props.GetString(key, fallback);
        protected int Int(string key, int fallback) => Props.GetInt(key, fallback);
        protected float Float(string key, float fallback) => Props.GetFloat(key, fallback);

        /// <summary>Distance from the point to the mechanism area edge (0 if inside).</summary>
        protected float DistanceTo(Vector2 p)
        {
            var r = Rect;
            float dx = Mathf.Max(r.xMin - p.x, 0f, p.x - r.xMax);
            float dy = Mathf.Max(r.yMin - p.y, 0f, p.y - r.yMax);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>The mechanism area touches the circle (centre c, radius r).</summary>
        internal bool Overlaps(Vector2 c, float r)
        {
            var rc = Rect;
            float dx = Mathf.Max(rc.xMin - c.x, 0f, c.x - rc.xMax);
            float dy = Mathf.Max(rc.yMin - c.y, 0f, c.y - rc.yMax);
            return dx * dx + dy * dy <= r * r;
        }
    }

    /// <summary>Kind with no behaviour of its own (e.g. ritual_pillar: just a position marker for a boss).</summary>
    public sealed class PassiveMechanism : Mechanism { }
}
