using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// One TRAINING DUMMY of Burlap's Proving Grounds (not an enemy: no HP, no AI — DrillYardMechanism drives it).
    /// Art = MEGAPACK Dummy_LVL1-4 copied to Resources/VFX/drill/&lt;kind&gt;_&lt;anim&gt; (sack | straw | pell | king; idle / move / roll / spin / top / dash...).
    /// A "dangerous" dummy must LOOK dangerous —
    ///   Whirl(true)  = whirlwind ring (PixelLab VFX drill/fx_whirl) spinning around the feet, looping, bobbing with the spin;
    ///   Flash(c, s)  = colour flash (hit / struck by a hay sack); fades at the yard edge (entering / leaving never pops in mid-yard).
    /// Moves by velocity `Vel` (or set Pos directly), sorted by feet like characters. `Harmless` = touching does not count.
    /// </summary>
    internal sealed class DrillActor
    {
        public readonly string Kind;
        public readonly SpriteRenderer Sr;
        public Vector2 Pos, Vel;
        public float Radius = 0.2f, Scale = 1.35f;
        public bool Harmless, FaceByVel = true;
        /// <summary>"Clean dodge" already counted for this pass (the formation has passed the player).</summary>
        public bool Credited;
        /// <summary>Yard-edge alpha (set every frame by DrillYard) multiplied by its own alpha.</summary>
        public float EdgeAlpha = 1f;
        /// <summary>Lifts the sprite off its feet ("HUT!" hop in the muster cutscene) — Pos / collision unchanged.</summary>
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
        /// <summary>One-shot animation has finished.</summary>
        public bool AnimDone => !loop && frames != null && (Time.time - animStart) * fps >= frames.Length;
        /// <summary>Body centre for collision (a bit above the feet).</summary>
        public Vector2 Body => Pos + Vector2.up * 0.1f * Scale;

        /// <summary>Whirlwind ring under the feet on / off (spinning dummy = can hurt).</summary>
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
            whirl.transform.localScale = new Vector3(size, size, 1f) / Mathf.Max(0.01f, Scale);       // world size = size (1 = 0.65 u ring)
        }

        /// <summary>Spinning (whirl on) — DrillYard plays the looping spin sound from the one nearest Ren.</summary>
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
                // whirl: frames 1..9 of the round slash looped (first / last frames are the open / close) — 20 fps
                int n = Mathf.Max(1, whirlFrames.Length - 3);
                int k = 1 + (int)((Time.time - whirlStart) * 20f) % n;
                whirl.sprite = whirlFrames[Mathf.Min(k, whirlFrames.Length - 1)];
                whirl.sortingLayerID = Sr.sortingLayerID;
                whirl.sortingOrder = Sr.sortingOrder + 1;
                whirl.color = new Color(1f, 1f, 1f, 0.85f * a);
                whirl.flipX = Sr.flipX;
                // slight bob: the body "bumps" with the spin
                float s = 1f + 0.05f * Mathf.Sin((Time.time - whirlStart) * 31f);
                Sr.transform.localScale = new Vector3(Scale * s, Scale / s, 1f);
            }
        }

        public void SetAlpha(float a) { alpha = Mathf.Clamp01(a); }

        public void Destroy() { if (Sr != null) Object.Destroy(Sr.gameObject); }
    }
}
