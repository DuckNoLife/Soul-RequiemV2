using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 20/11 (§356): một NGƯỜI NỘM của minigame The Drill Yard (không phải quái: không máu, không AI — DrillYardMechanism điều khiển).
    /// Hình = bộ MEGAPACK Dummy_LVL1-4 chép sang Resources/VFX/drill/&lt;kind&gt;_&lt;anim&gt; (sack | straw | pell | king; idle / move / roll / spin / top / dash...).
    /// Di chuyển theo vận tốc `Vel` (hoặc vị trí đặt thẳng), tự quay mặt theo hướng đi, sắp lớp theo chân như nhân vật.
    /// `Row` = mã hàng (hàng / cột / vòng) để tính điểm "đi qua khe"; `Harmless` = chạm không tính (đang chào, đang đứng chỉ huy).
    /// </summary>
    internal sealed class DrillActor
    {
        public readonly string Kind;
        public readonly SpriteRenderer Sr;
        public Vector2 Pos, Vel;
        public float Radius = 0.16f, Scale = 1.35f;
        public int Row = -1;
        public bool Harmless, Credited;
        public bool FaceByVel = true;
        private Sprite[] frames;
        private float fps = 12f, animStart;
        private bool loop = true;
        private string anim = "";
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
            if (name == anim && looping == loop) return;
            var f = Frames(Kind, name);
            if (f.Length == 0) return;
            anim = name; frames = f; fps = framesPerSecond; loop = looping; animStart = Time.time;
            Sr.sprite = frames[0];
        }

        /// <summary>Hoạt ảnh một lần đã chạy hết.</summary>
        public bool AnimDone => !loop && frames != null && (Time.time - animStart) * fps >= frames.Length;
        /// <summary>Tâm thân để tính va chạm (cao hơn chân một chút).</summary>
        public Vector2 Body => Pos + Vector2.up * 0.1f * Scale;

        public void Tick(float dt)
        {
            Pos += Vel * dt;
            if (Sr == null) return;
            Sr.transform.position = Pos;
            if (FaceByVel && Mathf.Abs(Vel.x) > 0.05f) Sr.flipX = Vel.x < 0f;
            if (frames == null || frames.Length == 0) return;
            int i = (int)((Time.time - animStart) * fps);
            Sr.sprite = frames[loop ? i % frames.Length : Mathf.Min(i, frames.Length - 1)];
        }

        public void SetAlpha(float a) { if (Sr != null) { var c = Sr.color; c.a = a; Sr.color = c; } }

        public void Destroy() { if (Sr != null) Object.Destroy(Sr.gameObject); }
    }
}
