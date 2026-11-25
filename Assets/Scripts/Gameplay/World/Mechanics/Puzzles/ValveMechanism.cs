using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// valve - rusty valve (Boiler Room): slashing / pressing T does nothing until the Ether Ram rams it
    /// (<see cref="MapMechanics.RamImpact"/>); then one use -> flag `sets` + text `text`.
    /// </summary>
    public sealed class ValveMechanism : Mechanism
    {
        private bool rusted = true;
        private bool announced;

        public override string DefaultVerb => "Turn";
        public override bool ReactsToStrike => true;

        public override void Tick(Vector2 playerPos, bool inside)
        {
            bool near = Vector2.Distance(playerPos, Rect.center) < 0.7f;
            if (near && !announced)
            {
                announced = true;
                Hints.Show(rusted ? "The valve is rusted solid. A blow far heavier than yours might crack it loose."
                                  : "The rust is cracked. The valve will turn now.", 3f);
            }
            if (!near) announced = false;
        }

        public override void OnStruck() => Turn();
        public override void Use() => Turn();

        private void Turn()
        {
            if (Done) return;
            if (rusted) { Hints.Show("Rusted solid. Your blade only rings off it.", 2f); Ctx.Run(Ctx.ShakeGate(Name)); return; }
            Done = true;
            Hints.Show(Str("text", "The valve turns."), 3f);
            Ctx.Flags.Set(Str("sets", Name));
        }

        /// <summary>The Ether Ram's ram (centre at, radius r) breaks the rust layer. Returns true if hit.</summary>
        internal bool TryCrackRust(Vector2 at, float r)
        {
            if (Done || !rusted || !Overlaps(at, r)) return false;
            rusted = false;
            Ctx.Run(Ctx.ShakeGate(Name));
            return true;
        }
    }
}
