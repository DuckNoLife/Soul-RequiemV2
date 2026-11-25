using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// ENERGY AURA enveloping a character (the Registrar lifting Ren by magic) — covers the whole body, unlike a round bubble.
    ///   - A bright rim hugging the character's shape: copies the character's current sprite every frame into 16 tinted copies offset 1–3 px around the body,
    ///     drawn BEHIND it (not scaled -> keeps 1:1 pixels), pulsing brightness.
    ///   - The body is tinted with the energy colour; energy wisps / motes rise around it; a violet light follows.
    /// Usage: var a = EnergyAura.Attach(player, colour); ... a.Stop() (fades over 0.25 s then self-destroys, restores the body colour).
    /// </summary>
    public sealed class EnergyAura : MonoBehaviour
    {
        private SpriteRenderer body;
        private Color bodyColor;
        private Color color;
        private readonly List<SpriteRenderer> rim = new List<SpriteRenderer>();
        private readonly List<Vector2> rimOff = new List<Vector2>();
        private readonly List<SpriteRenderer> wisps = new List<SpriteRenderer>();
        private readonly List<Vector3> wispState = new List<Vector3>();   // x = horizontal offset, y = progress 0..1, z = speed
        private Light2D glow;
        private float strength, target = 1f, born;
        private bool stopping;
        private static Sprite dot;

        public static EnergyAura Attach(Transform who, Color c)
        {
            if (who == null) return null;
            var sr = who.GetComponentInChildren<SpriteRenderer>();
            if (sr == null) return null;
            var go = new GameObject("Energy Aura");
            go.transform.SetParent(sr.transform, false);
            var a = go.AddComponent<EnergyAura>();
            a.Setup(sr, c);
            return a;
        }

        private void Setup(SpriteRenderer sr, Color c)
        {
            body = sr; bodyColor = sr.color; color = c; born = Time.time;
            // inner ring 8 directions offset 1 px (strong), outer ring 8 directions offset 3 px (faint) -> thick, soft glowing rim
            for (int ring = 0; ring < 2; ring++)
                for (int k = 0; k < 8; k++)
                {
                    float ang = k * Mathf.PI / 4f + (ring == 1 ? Mathf.PI / 8f : 0f);
                    float px = ring == 0 ? 0.01f : 0.03f;
                    var go = new GameObject("aura rim");
                    go.transform.SetParent(transform, false);
                    var r = go.AddComponent<SpriteRenderer>();
                    FxLighting.MakeUnlit(r);
                    rim.Add(r);
                    rimOff.Add(new Vector2(Mathf.Round(Mathf.Cos(ang)) * px, Mathf.Round(Mathf.Sin(ang)) * px));
                }
            if (dot == null)
            {
                var tex = new Texture2D(2, 3, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "aura_wisp" };
                tex.SetPixels32(new[] { new Color32(255, 255, 255, 200), new Color32(255, 255, 255, 200), new Color32(255, 255, 255, 255),
                                        new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 120), new Color32(255, 255, 255, 120) });
                tex.Apply(false, true);
                dot = Sprite.Create(tex, new Rect(0, 0, 2, 3), new Vector2(0.5f, 0.5f), 100f);
            }
            for (int i = 0; i < 14; i++)
            {
                var go = new GameObject("aura wisp");
                go.transform.SetParent(transform, false);
                var r = go.AddComponent<SpriteRenderer>();
                r.sprite = dot;
                FxLighting.MakeUnlit(r);
                wisps.Add(r);
                wispState.Add(new Vector3(Random.Range(-1f, 1f), Random.value, Random.Range(0.8f, 1.6f)));
            }
            var lg = new GameObject("aura light");
            lg.transform.SetParent(transform, false);
            lg.transform.localPosition = new Vector3(0f, 0.25f, 0f);
            glow = lg.AddComponent<Light2D>();
            glow.lightType = Light2D.LightType.Point;
            glow.color = c; glow.intensity = 0f;
            glow.pointLightOuterRadius = 1.3f; glow.pointLightInnerRadius = 0.2f;
            LateUpdate();
        }

        /// <summary>Fade out then self-destroy (restores the body colour).</summary>
        public void Stop()
        {
            if (this == null || stopping) return;
            stopping = true; target = 0f;
        }

        private void LateUpdate()
        {
            if (body == null) { Destroy(gameObject); return; }
            float dt = Time.deltaTime;
            strength = Mathf.MoveTowards(strength, target, dt / (stopping ? 0.25f : 0.3f));
            if (stopping && strength <= 0f) { body.color = bodyColor; Destroy(gameObject); return; }
            float t = Time.time - born;
            float pulse = 0.75f + 0.25f * Mathf.Sin(t * 14f);
            // body tinted with the energy colour
            body.color = Color.Lerp(bodyColor, new Color(Mathf.Lerp(bodyColor.r, color.r, 0.55f), Mathf.Lerp(bodyColor.g, color.g, 0.55f),
                                                         Mathf.Lerp(bodyColor.b, color.b, 0.55f), bodyColor.a), strength);
            // rim hugging the character shape
            for (int i = 0; i < rim.Count; i++)
            {
                var r = rim[i];
                r.sprite = body.sprite;
                r.flipX = body.flipX; r.flipY = body.flipY;
                r.sortingLayerID = body.sortingLayerID;
                r.sortingOrder = body.sortingOrder - 1;
                r.transform.localPosition = rimOff[i] + new Vector2(0f, Mathf.Sin(t * 9f + i) * 0.004f);
                float a = (i < 8 ? 0.85f : 0.4f) * pulse * strength;
                r.color = new Color(Mathf.Min(1f, color.r + 0.15f), Mathf.Min(1f, color.g + 0.1f), 1f, a);
            }
            // energy motes rising around the body (inside the character's frame)
            var b = body.bounds;
            for (int i = 0; i < wisps.Count; i++)
            {
                var s = wispState[i];
                s.y += dt * s.z * 0.9f;
                if (s.y > 1f) { s.y = 0f; s.x = Random.Range(-1f, 1f); s.z = Random.Range(0.8f, 1.6f); }
                wispState[i] = s;
                var r = wisps[i];
                float halfW = b.extents.x * 0.85f;
                r.transform.position = new Vector3(b.center.x + s.x * halfW + Mathf.Sin(t * 6f + i) * 0.02f, b.min.y + s.y * b.size.y * 1.05f, 0f);
                r.sortingLayerID = body.sortingLayerID;
                r.sortingOrder = body.sortingOrder + (i % 2 == 0 ? 1 : -1);
                float fade = Mathf.Sin(s.y * Mathf.PI);
                r.color = new Color(Mathf.Min(1f, color.r + 0.25f), Mathf.Min(1f, color.g + 0.25f), 1f, 0.9f * fade * strength);
            }
            if (glow != null) glow.intensity = 1.3f * pulse * strength;
        }

        private void OnDestroy()
        {
            if (body != null) body.color = bodyColor;
        }
    }
}
