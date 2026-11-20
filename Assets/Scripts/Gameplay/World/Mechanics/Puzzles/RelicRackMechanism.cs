using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 20/11 (§349): relic_rack — GIÁ VŨ KHÍ của The Reliquary Lists (Map 4, song đấu Reliquary Wardens). Thuộc tính `weapon`:
    /// halberd | hammer | flail | axes. Ba trạng thái:
    ///   Sealed : vũ khí bị xích hồn (tối màu) — không ai lấy được.
    ///   Called : Hollow Champion GỌI vũ khí này (giá sáng tím, lửa hồn dưới chân) -> CUỘC ĐUA: Ren tới trước bấm T ("Claim") -> cướp bùa
    ///            (RelicBoons) và Champion tới nơi tay không thì hụt hẫng; Champion tới trước -> hắn cầm vũ khí, đổi bộ chiêu.
    ///   Empty  : giá trống (hết lượt cho tới khi các giá rèn lại).
    /// Va chạm đặc ở chân giá. Trận đặt lại (ZoneReset) -> Sealed.
    /// </summary>
    public sealed class RelicRackMechanism : Mechanism
    {
        public enum State { Sealed, Called, Empty }
        public State Now { get; private set; } = State.Sealed;
        public string Weapon => Str("weapon", "halberd");
        /// <summary>Chỗ đứng để lấy vũ khí: phía sân (giá sát tường bắc -> đứng dưới; sát tường nam -> đứng trên).</summary>
        public Vector2 Stand { get; private set; }
        /// <summary>Ren vừa cướp được (Champion đọc để biết mình hụt).</summary>
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

        /// <summary>Champion gọi vũ khí: mở xích, bắt đầu cuộc đua.</summary>
        public void Call()
        {
            if (Now != State.Sealed) return;
            Now = State.Called; ClaimedByRen = false;
            if (sr != null) sr.color = Color.white;
            Sfx.Play("rack_unseal", Rect.center);
            glow = SpriteVfx.Spawn("duo_soulfire", new Vector2(Rect.center.x, Rect.yMin), 0.9f, sr != null ? sr.sortingLayerID : 0, (sr != null ? sr.sortingOrder : 0) - 1, false, 12f, false);
            if (glow != null) glow.Looping(9999f);
        }

        /// <summary>Ren bấm T.</summary>
        public override void Use()
        {
            if (Now != State.Called) return;
            ClaimedByRen = true;
            Empty();
            Sfx.Play("relic_claim", Rect.center);
            SpriteVfx.Spawn("m3_break", Rect.center + Vector2.up * 0.5f, 0.8f, sr != null ? sr.sortingLayerID : 0, (sr != null ? sr.sortingOrder : 0) + 2, false, 14f, false);
            RelicBoons.Grant(Weapon, Rect.center + Vector2.up * 0.4f);
        }

        /// <summary>Champion tới nơi trước: lấy vũ khí. true nếu lấy được.</summary>
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

        /// <summary>Các giá rèn lại relic (khoá xích như ban đầu).</summary>
        public void Rearm() { if (Now == State.Empty) Seal(); }

        /// <summary>Champion gục: giá còn xích mở hết cho Ren tự lấy.</summary>
        public void Release() { if (Now == State.Sealed) Call(); }

        private void OnZoneReset(Rect zone)
        {
            if (Ctx.Host == null) { MapEncounterDirector.ZoneReset -= OnZoneReset; return; }
            if (zone.Contains(Rect.center)) { Seal(); RelicBoons.Clear(); }   // Ren gục giữa trận: bùa cũng mất
        }

        internal override void DebugSolve() { }
    }
}
