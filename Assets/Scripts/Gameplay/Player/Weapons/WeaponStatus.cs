namespace SoulRequiem.Gameplay
{
    /// <summary>Effect a katana applies to an enemy on hit (WeaponTrait.status).</summary>
    public enum StatusKind
    {
        None,
        /// <summary>Bleed: each stack deals `statusPower` damage / second for `statusSeconds`, stacking up to `statusStacks` (Crimson Oath).</summary>
        Bleed,
        /// <summary>Burn: `statusPower` damage / second for `statusSeconds` (gold holy fire — Arthur's Accord).</summary>
        Burn,
        /// <summary>Binding roots: slow by `statusPower` (0.4 = 40%) for `statusSeconds`, violet flash (Moonlit Edge — Specimen Zero's roots).</summary>
        Bind,
        /// <summary>Frost: slow by `statusPower` for `statusSeconds`, icy blue flash (Ironcrown — northern iron).</summary>
        Chill,
        /// <summary>Blood-hunger mark: a marked enemy dying within `statusSeconds` heals Ren through Hunger (Eventide).</summary>
        Mark,
    }

    /// <summary>When a slash applies the effect.</summary>
    public enum StatusTrigger
    {
        /// <summary>Every hit.</summary>
        EveryHit,
        /// <summary>Only the last combo hit.</summary>
        Finisher,
        /// <summary>Only while the passive is active (e.g. Vigil lit).</summary>
        WhileActive,
    }
}
