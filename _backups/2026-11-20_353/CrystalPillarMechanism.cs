using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 20/11 (§350): crystal_pillar — CỘT PHA LÊ của The Reliquary Lists (Map 4), thay giá vũ khí §349. Thuộc tính `crystal`:
    /// crimson (lửa) | azure (băng) | verdant (độc) | gilded (thánh quang). Ba trạng thái:
    ///   Dormant : pha lê sáng mờ, đèn nhỏ màu của nó — chưa ai chạm được.
    ///   Called  : Hollow Champion GỌI pha lê này (đèn bừng, lửa màu dưới chân, nhịp sáng) -> CUỘC ĐUA: Ren tới trước bấm T ("Attune") -> hấp thụ bùa
    ///             (RelicBoons) và Champion tới nơi tay trắng -> hụt hẫng; Champion tới trước -> HÚT pha lê 1,1 s (Ren dồn đủ sát thương thì cắt
    ///             được), rồi lửa phép trên người hắn đổi sang màu pha lê + bộ chiêu của nguyên tố đó.
    ///   Spent   : pha lê cạn (xám tối, tắt đèn) — các cột rèn lại khi cạn cả bốn.
    /// Va chạm đặc ở chân cột. Trận đặt lại (ZoneReset) -> Dormant, bùa của Ren mất.
    /// </summary>
    public sealed class CrystalPillarMechanism : Mechanism
    {
        public enum State { Dormant, Called, Spent }
        public State Now { get; private set; } = State.Dormant;
        public string Crystal => Str("crystal", "crimson");
        public Vector2 Stand { get; private set; }
        public bool ClaimedByRen { get; private set; }
        public Color Tint => ColorOf(Crystal);
        /// <summary>Đỉnh pha lê (chỗ tia hút bắn ra).</summary>
        public Vector2 Tip => new Vector2(Rect.center.x, Rect.yMin + 0.9f * Scale);

        private SpriteRenderer sr;
        private Sprite full, spent;
        private Light2D glow;
        private SpriteVfx flame;
        private float Scale => Float("scale", 1f);   // 20/11 (§352): tỉ lệ 1:1 như tile (không mixel)

        public static Color ColorOf(string c) =>
            c == "crimson" ? new Color(1f, 0.35f, 0.25f) : c == "azure" ? new Color(0.4f, 0.78f, 1f) :
            c == "verdant" ? new Color(0.45f, 1f, 0.4f) : c == "gilded" ? new Color(1f, 0.85f, 0.35f) : new Color(0.72f, 0.42f, 1f);

        public override string DefaultVerb => "Attune";
        public override bool Usable => Now == State.Called;

        public override void Init()
        {
            full = WardenStatueMechanism.Load("VFX/props/crystal_pillar_" + Crystal);
            spent = WardenStatueMechanism.Load("VFX/props/crystal_pillar_" + Crystal + "_spent");
            sr = TrapArt.MakeRenderer(Ctx.Host.transform, "Crystal " + Name, new Vector3(Rect.center.x, Rect.yMin + 0.02f, 0f), Ctx.Player, 0);
            sr.sprite = full;
            sr.transform.localScale = new Vector3(Scale, Scale, 1f);
            var go = new GameObject("CrystalCollider " + Name);
            go.transform.SetParent(Ctx.Host.transform, true);
            go.transform.position = Rect.center;
            go.AddComponent<BoxCollider2D>().size = Rect.size;
            var lg = new GameObject("CrystalLight " + Name);
            lg.transform.SetParent(sr.transform, false);
            lg.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            glow = lg.AddComponent<Light2D>();
            glow.lightType = Light2D.LightType.Point;
            glow.color = Tint;
            glow.pointLightInnerRadius = 0.05f;
            glow.pointLightOuterRadius = 1.8f;
            var arena = MapMechanics.Instance != null ? MapMechanics.Instance.MechRect("duo_arena") : null;
            bool south = arena.HasValue && arena.Value.center.y > Rect.center.y;
            Stand = new Vector2(Rect.center.x, south ? Rect.yMax + 0.2f : Rect.yMin - 0.2f);
            MapEncounterDirector.ZoneReset += OnZoneReset;
            SetDormant();
        }

        public override void Animate()
        {
            if (glow == null) return;
            if (Now == State.Called)
            {
                float k = 0.5f + 0.5f * Mathf.Sin(Time.time * 9f);
                glow.intensity = 1.1f + 0.6f * k;
                if (sr != null) sr.color = Color.Lerp(Color.white, Color.Lerp(Color.white, Tint, 0.5f), k);
            }
            else if (Now == State.Dormant) glow.intensity = 0.45f + 0.08f * Mathf.Sin(Time.time * 2f + Rect.x);
        }

        private void SetDormant()
        {
            Now = State.Dormant; ClaimedByRen = false;
            if (sr != null) { sr.sprite = full; sr.color = new Color(0.8f, 0.8f, 0.85f); }
            if (glow != null) { glow.enabled = true; glow.pointLightOuterRadius = 1.6f; }
            if (flame != null) { flame.Stop(); flame = null; }
        }

        /// <summary>Champion gọi pha lê: bừng sáng, bắt đầu cuộc đua.</summary>
        public void Call()
        {
            if (Now != State.Dormant) return;
            Now = State.Called; ClaimedByRen = false;
            if (sr != null) sr.color = Color.white;
            if (glow != null) glow.pointLightOuterRadius = 2.6f;
            Sfx.Play("rack_unseal", Rect.center);
            flame = SpriteVfx.Spawn("duo_soulfire_" + Crystal, new Vector2(Rect.center.x, Rect.yMin), 1.0f, sr != null ? sr.sortingLayerID : 0, (sr != null ? sr.sortingOrder : 0) - 1, false, 12f, false);
            if (flame != null) flame.Looping(9999f);
        }

        /// <summary>Ren bấm T: hấp thụ pha lê.</summary>
        public override void Use()
        {
            if (Now != State.Called) return;
            ClaimedByRen = true;
            Drain();
            Sfx.Play("relic_claim", Rect.center);
            var bv = SpriteVfx.Spawn("m3_break", Tip, 0.9f, sr != null ? sr.sortingLayerID : 0, (sr != null ? sr.sortingOrder : 0) + 2, false, 14f, false);
            if (bv != null) bv.Tint(Tint);
            RelicBoons.Grant(Crystal, Tip);
        }

        /// <summary>Champion tới trước: bắt đầu hút (pha lê cạn). true nếu được.</summary>
        public bool TakeByChampion()
        {
            if (Now != State.Called) return false;
            Drain();
            return true;
        }

        private void Drain()
        {
            Now = State.Spent;
            if (sr != null) { sr.sprite = spent; sr.color = Color.white; }
            if (glow != null) glow.enabled = false;
            if (flame != null) { flame.Stop(); flame = null; }
        }

        /// <summary>Rèn lại (khi cả bốn cột đã cạn).</summary>
        public void Rearm() { if (Now == State.Spent) SetDormant(); }

        /// <summary>Champion gục: pha lê còn lại mở cho Ren tự hấp thụ.</summary>
        public void Release() { if (Now == State.Dormant) Call(); }

        private void OnZoneReset(Rect zone)
        {
            if (Ctx.Host == null) { MapEncounterDirector.ZoneReset -= OnZoneReset; return; }
            if (zone.Contains(Rect.center)) { SetDormant(); RelicBoons.Clear(); }
        }

        internal override void DebugSolve() { }
    }
}
