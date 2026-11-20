using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Nền cho hiệu ứng dựng bằng code (beam, shield): tắt dần khi có cờ `off_when`.
    /// </summary>
    public abstract class FxMechanism : Mechanism
    {
        protected Transform fx;
        protected SpriteRenderer fxSr;
        protected Vector3 fxScale;
        /// <summary>Vật nhìn qua lớp hiệu ứng (shield): trả lại màu / vị trí gốc khi hiệu ứng tắt.</summary>
        protected readonly List<(SpriteRenderer sr, Color col, Vector3 pos)> veil = new List<(SpriteRenderer, Color, Vector3)>();

        /// <summary>true = vỡ ra (phồng + mờ), false = co lại theo chiều dọc.</summary>
        protected abstract bool PopsWhenOff { get; }

        public override void OnFlagsChanged()
        {
            if (Done || fx == null) return;
            string off = Str("off_when", "");
            if (string.IsNullOrEmpty(off) || !Ctx.Flags.HasAll(off)) return;
            Done = true;
            Ctx.Run(FadeOut(PopsWhenOff));
        }

        private IEnumerator FadeOut(bool pop)
        {
            if (fx == null) yield break;
            foreach (var c2 in fx.GetComponentsInChildren<Collider2D>()) c2.enabled = false;
            foreach (var (vsr, col, pos) in veil)
                if (vsr != null) { vsr.color = col; vsr.transform.localPosition = pos; }
            var srs = fx.GetComponentsInChildren<SpriteRenderer>();
            var ls = fx.GetComponentsInChildren<Light2D>();
            float t0 = 0f, dur = pop ? 0.6f : 1.2f;
            var s0 = fx.localScale;
            while (t0 < dur)
            {
                t0 += Time.deltaTime;
                float k = t0 / dur;
                foreach (var sr in srs) { var c = sr.color; c.a = (1f - k) * (pop ? 1f : 0.85f); sr.color = c; }
                foreach (var l in ls) l.intensity *= 0.95f;
                if (pop) fx.localScale = s0 * (1f + 0.25f * k);
                else fx.localScale = new Vector3(s0.x, s0.y * (1f - k), 1f);
                yield return null;
            }
            fx.gameObject.SetActive(false);
        }
    }
}
