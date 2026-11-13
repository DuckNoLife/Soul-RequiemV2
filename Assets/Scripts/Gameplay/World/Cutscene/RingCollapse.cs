using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 02/11 (v2): cảnh SẬP HẦM cuối Map 1 (nhặt nanh Grok -> đất sụt -> rơi xuống Undercity). Chạy trước SceneTransition.Go.
    /// v1 dùng hố elip đen vẽ bằng code + vết nứt LineRenderer (nét vector mượt) -> lạc tông pixel art. v2 chỉ dùng hình CÙNG BỘ TILESET:
    ///   Resources/VFX/collapse/collapse_hole_1..4 (ghép từ hole-main.png của map: hố đất viền cỏ, lớn dần; _4 lòng hố sụt thành khoảng tối),
    ///   collapse_lip (mép cỏ phía trước, vẽ ĐÈ lên người chơi khi rơi), collapse_pebble_1..3 (sỏi của bộ props).
    /// Nhịp (~3.4 s): đất rung + sỏi rơi lộp độp; 0.5 s hố hé dưới chân Ren, lớn dần 3 nấc (mỗi nấc bụi đất bật + giật camera);
    /// cột đá còn đứng rung rồi lún, sụp trong bụi; 2.5 s lòng hố sụt thành khoảng tối, Ren tụt xuống sau mép cỏ (không thu nhỏ hình).
    /// Bật bằng thuộc tính Tiled `collapse = true` trên lối ra; `collapse_center` = tâm vòng đá (ô) cho sỏi / bụi.
    /// </summary>
    public static class RingCollapse
    {
        
        private class Pebble { public SpriteRenderer sr; public Vector3 from, to; public float t0, dur; }
        private class Puff { public SpriteRenderer sr; public float t0, fps; }

        // 02/11 (v3): bụi đất tự vẽ (collapse_dust_0..5, màu đất của tileset, tan bằng dither) thay cho m3_dash_dust / m3_break
        // (bộ VFX Map 3 vẽ kiểu vòng sáng vàng cam -> lạc tông, nhìn "giả")
        private static Sprite[] dustFrames;
        private static readonly List<Puff> puffs = new List<Puff>();

        private static void DustAt(Vector2 p, int layer, int order, float t, float fps = 14f)
        {
            if (dustFrames == null || dustFrames.Length == 0) return;
            var go = new GameObject("Collapse Dust");
            go.transform.position = new Vector3(Mathf.Round(p.x * 100f) / 100f, Mathf.Round(p.y * 100f) / 100f, 0f);   // khớp lưới điểm ảnh
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = dustFrames[0]; sr.flipX = Random.value < 0.5f;
            sr.sortingLayerID = layer; sr.sortingOrder = order;
            puffs.Add(new Puff { sr = sr, t0 = t, fps = fps });
        }

        private static void TickDust(float t)
        {
            for (int i = puffs.Count - 1; i >= 0; i--)
            {
                var p = puffs[i];
                if (p.sr == null) { puffs.RemoveAt(i); continue; }
                int f = (int)((t - p.t0) * p.fps);
                if (f >= dustFrames.Length) { Object.Destroy(p.sr.gameObject); puffs.RemoveAt(i); continue; }
                p.sr.sprite = dustFrames[f];
            }
        }

        public static IEnumerator Play(MapEncounterDirector dir, Vector2 centerTile)
        {
            var player = dir.Player;
            if (player == null) yield break;
            Vector2 ringCenter = float.IsNaN(centerTile.x) ? (Vector2)player.position : (Vector2)dir.TileToWorld(centerTile.x, centerTile.y);
            var psr = player.GetComponentInChildren<SpriteRenderer>();
            int layer = psr != null ? psr.sortingLayerID : 0, order = psr != null ? psr.sortingOrder : 2;

            // 3 nấc: hố nhỏ (4x5 ô) -> hố lớn (5x6) -> lòng hố sụt thành khoảng tối
            var holeSprites = new Sprite[3];
            for (int i = 0; i < 3; i++) holeSprites[i] = Resources.Load<Sprite>("VFX/collapse/collapse_hole_" + (i + 2));
            dustFrames = new Sprite[6];
            for (int i = 0; i < 6; i++) dustFrames[i] = Resources.Load<Sprite>("VFX/collapse/collapse_dust_" + i);
            if (System.Array.Exists(dustFrames, f => f == null)) dustFrames = null;
            puffs.Clear();
            var lipSprite = Resources.Load<Sprite>("VFX/collapse/collapse_lip");
            var pebbleSprites = new List<Sprite>();
            for (int i = 1; i <= 3; i++) { var s = Resources.Load<Sprite>("VFX/collapse/collapse_pebble_" + i); if (s != null) pebbleSprites.Add(s); }

            CutsceneState.Begin();
            var cam = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            float size = Camera.main != null ? Camera.main.orthographicSize : 3f;
            if (cam != null) cam.Focus(player.position + new Vector3(0f, 0.15f, 0f), size * 0.9f);

            // hố: tâm ảnh đặt sao cho chân Ren nằm giữa lòng hố (lòng hố = hàng 2-4 của khối 6 hàng -> lệch xuống 0.16 đv so với tâm ảnh)
            Vector3 feet = player.position;
            Vector3 holeCenter = feet + new Vector3(0f, 0.16f, 0f);
            var holeGo = new GameObject("Collapse Hole");
            holeGo.transform.position = holeCenter;
            var hole = holeGo.AddComponent<SpriteRenderer>();
            hole.sortingLayerID = layer; hole.sortingOrder = order - 1;
            hole.enabled = false;
            SpriteRenderer lip = null;

            var pillars = new List<(Transform t, Vector3 p0)>();
            foreach (var rp in RingPillar.All)
                if (rp != null && !rp.Broken && rp.visual != null && rp.visual.activeInHierarchy)
                    pillars.Add((rp.visual.transform, rp.visual.transform.position));

            Transform vis = psr != null ? psr.transform : player;
            Vector3 visPos0 = vis.localPosition;
            Color pc0 = psr != null ? psr.color : Color.white;

            var pebbles = new List<Pebble>();
            float[] stageAt = { 0.6f, 1.5f, 2.5f };
            int stage = -1;
            float nextKick = 0f, nextPebble = 0.1f, nextPillarDust = 1.0f;
            bool pillarsGone = false;
            const float total = 3.4f;

            for (float t = 0f; t < total; t += Time.deltaTime)
            {
                GameInput.Blocked = true;
                float k = t / total;

                // rung: giật nhỏ liên tục, mạnh dần
                if (t >= nextKick)
                {
                    nextKick = t + 0.07f;
                    CameraFollow.KickMain(Random.insideUnitCircle, Mathf.Lerp(0.012f, 0.05f, k), 0.12f);
                }

                // sỏi rơi từ trên xuống quanh vòng đá / quanh Ren, chạm đất bật bụi rồi lăn tắt
                if (t >= nextPebble && pebbleSprites.Count > 0 && t < total - 0.4f)
                {
                    nextPebble = t + Mathf.Lerp(0.13f, 0.045f, k);
                    Vector2 c = Random.value < 0.5f ? ringCenter : (Vector2)feet;
                    Vector3 to = c + Random.insideUnitCircle * 2.0f;
                    var go = new GameObject("Collapse Pebble");
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = pebbleSprites[Random.Range(0, pebbleSprites.Count)];
                    sr.flipX = Random.value < 0.5f;
                    sr.sortingLayerID = layer; sr.sortingOrder = order + 2;
                    var pb = new Pebble { sr = sr, to = to, from = to + new Vector3(Random.Range(-0.1f, 0.1f), 1.3f, 0f), t0 = t, dur = Random.Range(0.28f, 0.4f) };
                    go.transform.position = pb.from;
                    pebbles.Add(pb);
                }
                for (int i = pebbles.Count - 1; i >= 0; i--)
                {
                    var pb = pebbles[i];
                    if (pb.sr == null) { pebbles.RemoveAt(i); continue; }
                    float u = (t - pb.t0) / pb.dur;
                    if (u < 1f)
                        pb.sr.transform.position = Vector3.LerpUnclamped(pb.from, pb.to, u * u);      // rơi nhanh dần
                    else
                    {
                        if (pb.sr.sortingOrder != order - 2)
                        {
                            pb.sr.sortingOrder = order - 2;                                         // nằm trên mặt đất
                            DustAt(pb.to, layer, order + 1, t, 18f);
                        }
                        float fade = Mathf.Clamp01((t - pb.t0 - pb.dur) / 0.5f);
                        pb.sr.color = new Color(1f, 1f, 1f, 1f - fade);
                        if (fade >= 1f) { Object.Destroy(pb.sr.gameObject); pebbles.RemoveAt(i); }
                    }
                }

                // hố lớn dần theo 4 nấc
                while (stage + 1 < stageAt.Length && t >= stageAt[stage + 1])
                {
                    stage++;
                    if (holeSprites[stage] != null) { hole.sprite = holeSprites[stage]; hole.enabled = true; }
                    bool last = stage == stageAt.Length - 1;
                    float big = 0.7f + 0.15f * stage;
                    CameraFollow.KickMain(Vector2.down, last ? 0.13f : 0.06f, last ? 0.35f : 0.2f);
                    for (int d = 0; d < 5 + stage * 3; d++)                                    // bụi bật quanh mép hố
                    {
                        float a = Random.value * Mathf.PI * 2f;
                        Vector2 p = (Vector2)holeCenter + new Vector2(Mathf.Cos(a) * 0.7f, Mathf.Sin(a) * 0.55f - 0.1f) * big;
                        DustAt(p, layer, order + 1, t + Random.Range(-0.05f, 0f), Random.Range(11f, 16f));
                    }
                    if (last && lipSprite != null)
                    {
                        var lg = new GameObject("Collapse Lip");
                        lg.transform.position = holeCenter + new Vector3(0f, -0.8f, 0f);   // hàng dưới cùng của khối 6 hàng (1.92 đv)
                        lip = lg.AddComponent<SpriteRenderer>();
                        lip.sprite = lipSprite;
                        lip.sortingLayerID = layer; lip.sortingOrder = order + 1;      // mép cỏ trước che Ren khi tụt xuống
                    }
                }

                // cột đá: rung tại chỗ, lún dần, sụp trong bụi
                if (!pillarsGone && t >= 1.0f)
                {
                    float s = Mathf.Clamp01((t - 1.0f) / 1.6f);
                    foreach (var p in pillars)
                        if (p.t != null)
                            p.t.position = p.p0 + Vector3.down * (0.5f * s * s) + new Vector3(Random.Range(-0.012f, 0.012f), 0f, 0f);
                    if (t >= nextPillarDust)
                    {
                        nextPillarDust = t + 0.22f;
                        foreach (var p in pillars)
                        {
                            if (p.t == null) continue;
                            DustAt((Vector2)p.p0 + new Vector2(Random.Range(-0.2f, 0.2f), 0.02f), layer, order + 1, t);
                        }
                    }
                    if (s >= 1f)
                    {
                        pillarsGone = true;
                        foreach (var p in pillars)
                        {
                            if (p.t == null) continue;
                            for (int d = 0; d < 4; d++)
                                DustAt((Vector2)p.p0 + new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(0f, 0.4f)), layer, order + 1, t, 12f);
                            p.t.gameObject.SetActive(false);
                        }
                    }
                }

                // Ren tụt xuống lòng hố (sau mép cỏ phía trước), tối dần trong bóng tối
                if (t >= 2.6f)
                {
                    float f = Mathf.Clamp01((t - 2.6f) / 0.75f);
                    vis.localPosition = visPos0 + Vector3.down * (0.6f * f * f);
                    if (psr != null)
                    {
                        float dark = Mathf.Clamp01((f - 0.35f) / 0.65f);
                        psr.color = new Color(pc0.r * (1f - 0.85f * dark), pc0.g * (1f - 0.85f * dark), pc0.b * (1f - 0.85f * dark), pc0.a * (1f - dark));
                    }
                }
                TickDust(t);
                yield return null;
            }
            if (cam != null) cam.Release();
            // CutsceneState giữ nguyên: người gọi chuyển scene ngay sau đó (scene mới Reset); chuyển thất bại thì người gọi tự End
        }
    }
}
