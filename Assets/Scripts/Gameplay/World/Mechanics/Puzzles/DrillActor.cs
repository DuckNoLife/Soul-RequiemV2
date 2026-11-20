using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 20/11 (§356) → (§360): một NGƯỜI NỘM của Burlap's Proving Grounds (không phải quái: không máu, không AI — DrillYardMechanism điều khiển).
    /// Hình = bộ MEGAPACK Dummy_LVL1-4 chép sang Resources/VFX/drill/&lt;kind&gt;_&lt;anim&gt; (sack | straw | pell | king; idle / move / roll / spin / top / dash...).
    /// §360: người nộm "nguy hiểm" phải TRÔNG nguy hiểm —
    ///   Whirl(true)  = vòng gió xoáy (VFX PixelLab drill/fx_whirl) quay quanh chân, lặp liên tục, nảy nhẹ theo nhịp xoay;
    ///   Flash(c, s)  = loé màu (trúng đòn / bị bao cỏ đập); Fade theo mép sân (vào / ra sân không "bật" ra giữa chừng).
    /// Di chuyển theo vận tốc `Vel` (hoặc đặt thẳng Pos), sắp lớp theo chân như nhân vật. `Harmless` = chạm không tính.
    /// </summary>
    internal sealed class DrillActor
    {
        public readonly string Kind;
        public readonly SpriteRenderer Sr;
        public Vector2 Pos, Vel;
        public float Radius = 0.2f, Scale = 1.35f;
        public bool Harmless, FaceByVel = true;
        /// <summary>Đã tính "né sạch" cho lượt này (đội hình đã đi qua người chơi).</summary>
        public bool Credited;
        /// <summary>Độ trong suốt do mép sân (DrillYard đặt mỗi khung) nhân với độ trong suốt riêng.</summary>
        public float EdgeAlpha = 1f;
        /// <summary>20/11 (§361): nhấc hình lên khỏi chân (nhảy "HUT!" trong cảnh duyệt binh) — không đổi Pos / va chạm.</summary>
        public float Lift;
        private Sprite[] frames;
        private float fps = 12f, animStart, alpha = 1f, flashUntil, flashLen;
        private Color flashColor = Color.white;
        private bool loop = true;
        private string anim = "";
        private SpriteRenderer whirl;
        private Sprite[] whirlFrames;
        private float whirlStart;
        private static readonly Dictionary<string, Sprite[]> cache = new Dictionary<string, Sprite[]>();

        public DrillActor(string kind, Vector2 pos, Transform parent, Transform player, float scale = 1.35f)
        {
            Kind = kind; Pos = pos; Scale = scale;
            Sr = TrapArt.MakeRenderer(parent, "Drill " + kind, pos, player, 0);
            Sr.transform.localScale = new Vector3(scale, scale, 1f);
            Play("idle", 4f);
        }

        public static Sprite[] Frames(string kind, string anim)
        {
            string key = kind + "_" + anim;
            if (cache.TryGetValue(key, out var f) && f != null && f.Length > 0) return f;
            f = SpriteVfx.Load("drill/" + key);
            cache[key] = f;
            return f;
        }

        public void Play(string name, float framesPerSecond, bool looping = true)
        {
            if (name == anim && looping == loop) { fps = framesPerSecond; return; }
            var f = Frames(Kind, name);
            if (f.Length == 0) return;
            anim = name; frames = f; fps = framesPerSecond; loop = looping; animStart = Time.time;
            Sr.sprite = frames[0];
        }

        public string Anim => anim;
        /// <summary>Hoạt ảnh một lần đã chạy hết.</summary>
        public bool AnimDone => !loop && frames != null && (Time.time - animStart) * fps >= frames.Length;
        /// <summary>Tâm thân để tính va chạm (cao hơn chân một chút).</summary>
        public Vector2 Body => Pos + Vector2.up * 0.1f * Scale;

        /// <summary>Bật / tắt vòng gió xoáy dưới chân (người nộm đang quay = có thể gây đau).</summary>
        public void Whirl(bool on, float size = 1f)
        {
            if (!on) { if (whirl != null) whirl.enabled = false; return; }
            if (whirl == null)
            {
                whirlFrames = SpriteVfx.Load("drill/fx_whirl");
                if (whirlFrames.Length == 0) return;
                var go = new GameObject("whirl");
                go.transform.SetParent(Sr.transform, false);
                whirl = go.AddComponent<SpriteRenderer>();
                whirl.spriteSortPoint = SpriteSortPoint.Pivot;
                FxLighting.MakeUnlit(whirl);
                whirlStart = Time.time - Random.Range(0f, 0.6f);
            }
            whirl.enabled = true;
            whirl.transform.localPosition = new Vector3(0f, 0.09f, 0f);
            whirl.transform.localScale = new Vector3(size, size, 1f) / Mathf.Max(0.01f, Scale);       // cỡ thế giới = size (1 = vòng 0,65 đv)
        }

        /// <summary>§364: đang quay (có vòng gió) — DrillYard phát tiếng quay lặp theo con gần Ren nhất.</summary>
        public bool Spinning => whirl != null && whirl.enabled;

        public void Flash(Color c, float seconds) { flashColor = c; flashLen = Mathf.Max(0.01f, seconds); flashUntil = Time.time + seconds; }

        public void Tick(float dt)
        {
            Pos += Vel * dt;
            if (Sr == null) return;
            Sr.transform.position = Pos + Vector2.up * Lift;
            if (FaceByVel && Mathf.Abs(Vel.x) > 0.05f) Sr.flipX = Vel.x < 0f;
            if (frames != null && frames.Length > 0)
            {
                int i = (int)((Time.time - animStart) * fps);
                Sr.sprite = frames[loop ? i % frames.Length : Mathf.Min(i, frames.Length - 1)];
            }
            float a = alpha * EdgeAlpha;
            var col = Color.white;
            if (Time.time < flashUntil) col = Color.Lerp(Color.white, flashColor, (flashUntil - Time.time) / flashLen);
            col.a = a;
            Sr.color = col;
            if (whirl != null && whirl.enabled && whirlFrames.Length > 0)
            {
                // vòng xoáy: khung 1..9 của nhát chém tròn lặp liên tục (khung đầu / cuối là lúc mở / tắt) — 20 khung/giây
                int n = Mathf.Max(1, whirlFrames.Length - 3);
                int k = 1 + (int)((Time.time - whirlStart) * 20f) % n;
                whirl.sprite = whirlFrames[Mathf.Min(k, whirlFrames.Length - 1)];
                whirl.sortingLayerID = Sr.sortingLayerID;
                whirl.sortingOrder = Sr.sortingOrder + 1;
                whirl.color = new Color(1f, 1f, 1f, 0.85f * a);
                whirl.flipX = Sr.flipX;
                // nảy nhẹ: thân xoay thì hơi "dập" theo nhịp
                float s = 1f + 0.05f * Mathf.Sin((Time.time - whirlStart) * 31f);
                Sr.transform.localScale = new Vector3(Scale * s, Scale / s, 1f);
            }
        }

        public void SetAlpha(float a) { alpha = Mathf.Clamp01(a); }

        public void Destroy() { if (Sr != null) Object.Destroy(Sr.gameObject); }
    }
}
