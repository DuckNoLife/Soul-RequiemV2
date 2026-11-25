using System.Collections;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// REEL on top of the Brazen Reliquary (phase 2 "Jackpot Engine"). A brass-framed glass window where symbols scroll past on a
    /// FIXED strip (Strip) - spins fast, then slows; the symbol it stops on is the skill the Engine fires. Ren hitting the Engine while it spins -> the reel JOLTS
    /// one extra step: a skilled player counts steps to push it into BUST (Engine backfires, stunned) instead of HEART (Engine heals).
    /// </summary>
    public sealed class JackpotReel : MonoBehaviour
    {
        public static readonly string[] Strip = { "goblin", "bomb", "skull", "coin", "bolt", "anvil", "heart", "bust" };
        public const int Goblin = 0, Bomb = 1, Skull = 2, Coin = 3, Bolt = 4, Anvil = 5, Heart = 6, Bust = 7;

        private SpriteRenderer frame, icon;
        private Sprite[] icons;
        private int index;
        private float punch;
        public bool Spinning { get; private set; }
        public int Current => index;
        public static string Name(int i) => Strip[((i % Strip.Length) + Strip.Length) % Strip.Length];

        public static JackpotReel Attach(Transform owner, float height, int layer, int order)
        {
            var go = new GameObject("Jackpot Reel");
            go.transform.SetParent(owner, false);
            go.transform.localPosition = new Vector3(0f, height, 0f);
            var r = go.AddComponent<JackpotReel>();
            r.frame = go.AddComponent<SpriteRenderer>();
            r.frame.sprite = WardenStatueMechanism.Load("VFX/props/reel_frame");
            r.frame.sortingLayerID = layer; r.frame.sortingOrder = order;
            var ig = new GameObject("Icon");
            ig.transform.SetParent(go.transform, false);
            r.icon = ig.AddComponent<SpriteRenderer>();
            r.icon.sortingLayerID = layer; r.icon.sortingOrder = order + 1;
            r.icons = new Sprite[Strip.Length];
            for (int i = 0; i < Strip.Length; i++) r.icons[i] = WardenStatueMechanism.Load("VFX/props/reel_" + Strip[i]);
            r.index = Random.Range(0, Strip.Length);
            r.Show();
            return r;
        }

        private void Show()
        {
            if (icon != null) icon.sprite = icons[index];
            punch = 1f;
        }

        private void Update()
        {
            // stays upright even when the boss flips; bounces slightly on each step
            punch = Mathf.MoveTowards(punch, 0f, Time.deltaTime * 6f);
            float s = 1f + punch * 0.12f;
            transform.localScale = new Vector3(s, s, 1f);
            if (transform.parent != null && transform.parent.lossyScale.x < 0f) transform.localScale = new Vector3(-s, s, 1f);
        }

        /// <summary>Spin `steps` steps, slowing down (last step 0.5 s) - the player can read the last 3 steps.</summary>
        public IEnumerator Spin(int steps)
        {
            Spinning = true;
            Sfx.Play("reel_spin", transform.position, 0.8f);
            for (int k = 0; k < steps; k++)
            {
                int left = steps - k;
                float wait = left > 4 ? 0.07f : left == 4 ? 0.2f : left == 3 ? 0.3f : left == 2 ? 0.42f : 0.55f;
                yield return new WaitForSeconds(wait);
                index = (index + 1) % Strip.Length;
                Show();
            }
            Spinning = false;
            Sfx.Play("reel_stop", transform.position);
            punch = 2f;
            if (frame != null) StartCoroutine(Blink());
        }

        /// <summary>Ren hits it while spinning: jolt one extra step.</summary>
        public void Jolt()
        {
            if (!Spinning) return;
            index = (index + 1) % Strip.Length;
            Show();
            punch = 2.2f;
            Sfx.Play("reel_jolt", transform.position);
        }

        private IEnumerator Blink()
        {
            for (int i = 0; i < 4 && frame != null; i++)
            {
                frame.color = i % 2 == 0 ? new Color(1f, 0.9f, 0.5f) : Color.white;
                yield return new WaitForSeconds(0.08f);
            }
            if (frame != null) frame.color = Color.white;
        }
    }
}
