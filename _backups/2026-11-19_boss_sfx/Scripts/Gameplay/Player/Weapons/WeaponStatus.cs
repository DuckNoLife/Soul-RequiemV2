namespace SoulRequiem.Gameplay
{
    /// <summary>16/11 (§275): hiệu ứng thanh katana gây lên quái khi chém trúng (WeaponTrait.status).</summary>
    public enum StatusKind
    {
        None,
        /// <summary>Chảy máu: mỗi tầng `statusPower` sát thương / giây trong `statusSeconds`, cộng dồn tối đa `statusStacks` (Crimson Oath).</summary>
        Bleed,
        /// <summary>Thiêu: `statusPower` sát thương / giây trong `statusSeconds` (lửa thánh quang vàng — Arthur's Accord).</summary>
        Burn,
        /// <summary>Rễ trói: chậm `statusPower` (0.4 = 40%) trong `statusSeconds`, nháy tím (Moonlit Edge — rễ của Specimen Zero).</summary>
        Bind,
        /// <summary>Băng giá: chậm `statusPower` trong `statusSeconds`, nháy xanh lạnh (Ironcrown — sắt phương bắc).</summary>
        Chill,
        /// <summary>Dấu khát máu: quái mang dấu chết trong `statusSeconds` thì Ren hồi máu theo Hunger (Eventide).</summary>
        Mark,
    }

    /// <summary>16/11 (§275): khi nào nhát chém gây hiệu ứng.</summary>
    public enum StatusTrigger
    {
        /// <summary>Mọi nhát trúng.</summary>
        EveryHit,
        /// <summary>Chỉ nhát cuối combo.</summary>
        Finisher,
        /// <summary>Chỉ khi nội tại đang bật (vd Vigil đã sáng).</summary>
        WhileActive,
    }
}
