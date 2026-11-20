using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 20/11 (§350): crystal_pillar — nguồn pha lê của The Reliquary Lists (Map 4). Thuộc tính `crystal`:
    /// crimson (lửa) | azure (băng) | verdant (sinh / độc) | gilded (thánh quang).
    /// 20/11 (§353, người dùng: thay cột bằng 4 KỆ chứa pha lê 4 màu có hình nguyên tố, kệ cần ánh sáng đúng màu pha lê):
    ///   Hình = KỆ ĐÁ teal khung đồng (PixelLab, _tools/m4duo_1119/s353/make_shelves.py): pha lê lớn tầng trên, mảnh nhỏ tầng dưới,
    ///   ĐĨA KHẮC ở chân kệ mang ký hiệu nguyên tố (ngọn lửa / bông tuyết / lá / mặt trời).
    ///   ÁNH SÁNG 3 lớp: (1) lớp tự sáng crystal_pillar_<c>_lit vẽ Unlit đè lên pha lê + ký hiệu (sáng trong bóng tối);
    ///   (2) quầng sáng mềm Unlit màu pha lê quanh pha lê; (3) Light2D màu pha lê hắt xuống sàn. Ba lớp cùng thở theo trạng thái.
    /// Ba trạng thái:
    ///   Dormant : sáng dịu, thở chậm.
    ///   Called  : Hollow Champion GỌI kệ này (sáng bừng, nhịp nhanh, vòng aura màu nguyên tố dưới chân) -> CUỘC ĐUA: Ren tới trước bấm T ("Attune")
    ///             -> Ren hấp thụ bùa (RelicBoons), Champion tới nơi tay trắng -> hụt hẫng; Champion tới trước -> HÚT 1,1 s (Ren dồn đủ sát thương thì cắt).
    ///   Spent   : pha lê cạn (xám, ký hiệu tắt, đèn tắt) — các kệ rèn lại khi cạn cả bốn.
    /// Va chạm đặc ở chân kệ. Trận đặt lại (ZoneReset) -> Dormant, bùa của Ren mất.
    /// </summary>
    public sealed class CrystalPillarMechanism : Mechanism
    {
        public enum State { Dormant, Called, Spent }
        public State Now { get; private set; } = State.Dormant;
        public string Crystal => Str("crystal", "crimson");
        public Vector2 Stand { get; private set; }
        public bool ClaimedByRen { get; private set; }
        public Color Tint => ColorOf(Crystal);
        /// <summary>Tâm pha lê lớn trên kệ (chỗ tia hút bắn ra) — 74% chiều cao ảnh kệ.</summary>
        public Vector2 Tip => new Vector2(Rect.center.x, Rect.yMin + 0.02f + Height * 0.74f);

        private SpriteRenderer sr, lit, halo;
        private Sprite full, spent;
        private Light2D glow;
        private SpriteVfx aura;
        private float Scale => Float("scale", 1f);   // 20/11 (§352): tỉ lệ 1:1 như tile (không mixel)
        private float Height => (full != null ? full.bounds.size.y : 0.78f) * Scale;
        private static Sprite haloSprite;

        public static Color ColorOf(string c) =>
            c == "crimson" ? new Color(1f, 0.35f, 0.25f) : c == "azure" ? new Color(0.4f, 0.78f, 1f) :
            c == "verdant" ? new Color(0.45f, 1f, 0.4f) : c == "gilded" ? new Color(1f, 0.85f, 0.35f) : new Color(0.72f, 0.42f, 1f);

        public override string DefaultVerb => "Attune";
        public override bool Usable => Now == State.Called;

        public override void Init()
        {
            full = WardenStatueMechanism.Load("VFX/props/crystal_pillar_" + Crystal);
            spent = WardenStatueMechanism.Load("VFX/props/crystal_pillar_" + Crystal + "_spent");
            sr = TrapArt.MakeRenderer(Ctx.Host.transform, "Crystal Shelf " + Name, new Vector3(Rect.center.x, Rect.yMin + 0.02f, 0f), Ctx.Player, 0);
            sr.sprite = full;
            sr.transform.localScale = new Vector3(Scale, Scale, 1f);
            // (1) lớp tự sáng: pha lê + ký hiệu, cùng gốc, vẽ ngay trên kệ
            var lg0 = new GameObject("CrystalLit " + Name);
            lg0.transform.SetParent(sr.transform, false);
            lit = lg0.AddComponent<SpriteRenderer>();
            lit.sprite = WardenStatueMechanism.Load("VFX/props/crystal_pillar_" + Crystal + "_lit");
            lit.spriteSortPoint = SpriteSortPoint.Pivot;
            lit.sortingLayerID = sr.sortingLayerID; lit.sortingOrder = sr.sortingOrder + 1;
            FxLighting.MakeUnlit(lit);
            // (2) quầng sáng mềm quanh pha lê
            var hg = new GameObject("CrystalHalo " + Name);
            hg.transform.SetParent(sr.transform, false);
            hg.transform.position = Tip;
            halo = hg.AddComponent<SpriteRenderer>();
            halo.sprite = HaloSprite;
            halo.sortingLayerID = sr.sortingLayerID; halo.sortingOrder = sr.sortingOrder + 2;
            FxLighting.MakeUnlit(halo);
            // (3) đèn màu pha lê hắt xuống sàn
            var lg = new GameObject("CrystalLight " + Name);
            lg.transform.SetParent(sr.transform, false);
            lg.transform.position = Tip;
            glow = lg.AddComponent<Light2D>();
            glow.lightType = Light2D.LightType.Point;
            glow.color = Tint;
            glow.pointLightInnerRadius = 0.15f;
            glow.pointLightOuterRadius = 2.2f;
            glow.falloffIntensity = 0.6f;
            var go = new GameObject("CrystalCollider " + Name);
            go.transform.SetParent(Ctx.Host.transform, true);
            go.transform.position = Rect.center;
            go.AddComponent<BoxCollider2D>().size = Rect.size;
            var arena = MapMechanics.Instance != null ? MapMechanics.Instance.MechRect("duo_arena") : null;
            bool south = arena.HasValue && arena.Value.center.y > Rect.center.y;
            Stand = new Vector2(Rect.center.x, south ? Rect.yMax + 0.2f : Rect.yMin - 0.2f);
            MapEncounterDirector.ZoneReset += OnZoneReset;
            SetDormant();
        }

        /// <summary>Quầng sáng tròn mềm (vẽ bằng code, 48 px) — nhuộm màu pha lê.</summary>
        private static Sprite HaloSprite
        {
            get
            {
                if (haloSprite != null) return haloSprite;
                const int n = 48;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float d = new Vector2(x - n / 2f + 0.5f, y - n / 2f + 0.5f).magnitude / (n / 2f);
                        float a = Mathf.Clamp01(1f - d);
                        a = Mathf.Round(a * a * 5f) / 5f;                  // 5 bậc — giữ chất pixel, không loang mịn
                        px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
                    }
                tex.SetPixels32(px); tex.Apply();
                haloSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
                return haloSprite;
            }
        }

        public override void Animate()
        {
            if (glow == null) return;
            float t = Time.time;
            if (Now == State.Called)
            {
                float k = 0.5f + 0.5f * Mathf.Sin(t * 9f);
                glow.intensity = 1.5f + 0.8f * k;
                SetHalo(0.42f + 0.18f * k, 1.25f + 0.15f * k);
                if (sr != null) sr.color = Color.Lerp(Color.white, Color.Lerp(Color.white, Tint, 0.35f), k);   // kệ ửng màu pha lê theo nhịp
            }
            else if (Now == State.Dormant)
            {
                float k = 0.5f + 0.5f * Mathf.Sin(t * 2f + Rect.x);
                glow.intensity = 0.85f + 0.2f * k;
                SetHalo(0.2f + 0.08f * k, 0.95f);
            }
        }

        private void SetHalo(float alpha, float size)
        {
            if (halo == null) return;
            halo.enabled = alpha > 0.01f;
            halo.color = new Color(Tint.r, Tint.g, Tint.b, alpha);
            halo.transform.localScale = new Vector3(size, size, 1f);
        }

        private void SetDormant()
        {
            Now = State.Dormant; ClaimedByRen = false;
            if (sr != null) { sr.sprite = full; sr.color = Color.white; }
            if (lit != null) { lit.enabled = true; lit.color = Color.white; }
            if (glow != null) { glow.enabled = true; glow.pointLightOuterRadius = 2.2f; }
            StopAura();
        }

        private void StopAura() { if (aura != null) { aura.Stop(); aura = null; } }

        /// <summary>Champion gọi pha lê: kệ sáng bừng, vòng aura nguyên tố dưới chân, bắt đầu cuộc đua.</summary>
        public void Call()
        {
            if (Now != State.Dormant) return;
            Now = State.Called; ClaimedByRen = false;
            if (glow != null) glow.pointLightOuterRadius = 3.2f;
            Sfx.Play("rack_unseal", Rect.center);
            StopAura();
            aura = SpriteVfx.Spawn("duo_aura_" + Crystal, new Vector2(Rect.center.x, Rect.yMin - 0.04f), 1.3f, sr != null ? sr.sortingLayerID : 0, (sr != null ? sr.sortingOrder : 0) - 1, false, 12f, false);
            if (aura != null) aura.Looping(9999f);
        }

        /// <summary>Ren bấm T: hấp thụ pha lê.</summary>
        public override void Use()
        {
            if (Now != State.Called) return;
            ClaimedByRen = true;
            Drain();
            Sfx.Play("relic_claim", Rect.center);
            var bv = SpriteVfx.Spawn("m3_break", Tip, 0.9f, sr != null ? sr.sortingLayerID : 0, (sr != null ? sr.sortingOrder : 0) + 3, false, 14f, false);
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
            if (lit != null) lit.enabled = false;
            if (glow != null) glow.enabled = false;
            SetHalo(0f, 1f);
            StopAura();
        }

        /// <summary>Rèn lại (khi cả bốn kệ đã cạn).</summary>
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
