using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 20/11 (§365): TRẠM 3 — SERGEANT SAYS (thay đấu bò + máy phóng §360, bị chê khó trúng). Burlap là CỖ MÁY HUẤN LUYỆN tự động:
    /// đọc lệnh từ thẻ đục lỗ (tiếng cuộn thẻ "reel_spin" -> "reel_stop"), hô lệnh, thỉnh thoảng lỗi lệnh.
    /// Sân có 5 Ô TẬP vẽ trên sàn (texture tạo bằng code): LEFT / RIGHT / FORWARD / BACK (mũi tên) + AT EASE (vòng tròn giữa sân).
    /// Luật (kiểu Simon Says quân sự) — CHỈ làm theo khi lệnh có "SERGEANT SAYS":
    ///   • "SERGEANT SAYS: LEFT!"  -> hết giờ phải đứng TRONG ô đó. Cả sân trừ ô đó ửng đỏ 0,4 s -> Burlap DẬM: ngoài ô = dính đòn (lăn không cứu).
    ///   • "LEFT!" (lệnh BẪY, thiếu "Sergeant says") -> phải ĐỨNG YÊN; nhúc nhích quá 0,55 đv -> bao cát rơi trúng đầu (bóng báo 0,35 s).
    ///   • "SERGEANT SAYS: HIT THE DIRT!" -> đúng lúc hết giờ phải đang LĂN (cả sân ửng đỏ, dậm).
    /// 10 lệnh, thời gian nhanh dần (2,6 -> 1,8 s), lệnh lỗi "SERGEANT SAYS: SERGEANT SAYS: SERG-". Đủ 10 lệnh -> máy QUÁ TẢI: bốc khói, kẹt răng cưa,
    /// lắp bắp rồi tự "cho qua". Hết tinh thần (5 ô) = trượt trạm.
    /// HUD: dòng LỆNH to giữa màn (DrillBoard.Command), vạch đếm ngược (TrackFrac), mục tiêu "ORDERS n / 10".
    /// </summary>
    public sealed partial class DrillYardMechanism
    {
        private enum Pad { Center, Left, Right, Forward, Back }
        private enum Kind { Move, Freeze, Dirt }

        private sealed class DrillOrder
        {
            public Kind Kind; public Pad Pad; public bool Real, Fixed; public string Text, Line;
        }

        private readonly Dictionary<Pad, SpriteRenderer> pads = new Dictionary<Pad, SpriteRenderer>();
        private readonly Dictionary<Pad, Vector2> padAt = new Dictionary<Pad, Vector2>();
        private readonly List<SpriteRenderer> danger = new List<SpriteRenderer>();
        private Sprite white;
        private Pad? lit;
        private bool padsOn, litDanger;
        private float padsAt;
        private int ordersDone;
        private const float PadHalf = 0.6f;
        private const int Orders = 10;

        // ------------------------------------------------------------------ ô tập trên sàn
        private void InitPads()
        {
            var c = A.center + Vector2.down * 0.1f;
            padAt[Pad.Center] = c;
            padAt[Pad.Left] = new Vector2(A.xMin + 1.15f, c.y);
            padAt[Pad.Right] = new Vector2(A.xMax - 1.15f, c.y);
            padAt[Pad.Forward] = new Vector2(c.x, A.yMax - 1.85f);
            padAt[Pad.Back] = new Vector2(c.x, A.yMin + 1.0f);
            foreach (var kv in padAt)
            {
                var sr = TrapArt.MakeRenderer(Ctx.Host.transform, "Drill Pad " + kv.Key, kv.Value, Ctx.Player, -8);
                FxLighting.MakeUnlit(sr);
                sr.sprite = PadSprite(kv.Key);
                sr.enabled = false;
                pads[kv.Key] = sr;
            }
            var t = Texture2D.whiteTexture;
            white = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), t.width);   // 1 đv mỗi cạnh
        }

        /// <summary>Ô tập 120 x 120 px: nền vàng rơm mờ, viền nét đứt, biểu tượng (mũi tên theo hướng / vòng tròn "at ease").</summary>
        private static Sprite PadSprite(Pad p)
        {
            const int N = 120;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "drill_pad" };
            var px = new Color32[N * N];
            var fill = new Color32(255, 214, 120, 30); var edge = new Color32(255, 222, 140, 210); var icon = new Color32(255, 222, 140, 150);
            for (int i = 0; i < px.Length; i++) px[i] = fill;
            for (int k = 0; k < N; k++)
            {
                if ((k / 8) % 2 != 0) continue;
                for (int t = 0; t < 3; t++) { px[t * N + k] = edge; px[(N - 1 - t) * N + k] = edge; px[k * N + t] = edge; px[k * N + N - 1 - t] = edge; }
            }
            int cx = N / 2, cy = N / 2;
            if (p == Pad.Center)
            {
                for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
                {
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    if (d > 22f && d < 27f) px[y * N + x] = icon;
                }
            }
            else
            {
                // mũi tên đặc: thân + đầu, vẽ hướng +x rồi xoay theo ô
                for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
                {
                    int u = x - cx, v = y - cy;                                  // hệ +x
                    int ru = u, rv = v;
                    switch (p)
                    {
                        case Pad.Left: ru = -u; rv = v; break;
                        case Pad.Forward: ru = v; rv = -u; break;                // hướng lên
                        case Pad.Back: ru = -v; rv = u; break;
                    }
                    bool shaft = ru >= -26 && ru <= 4 && Mathf.Abs(rv) <= 6;
                    bool head = ru > 4 && ru <= 26 && Mathf.Abs(rv) <= (26 - ru);
                    if (shaft || head) px[y * N + x] = icon;
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
        }

        private bool OnPad(Pad p, Vector2 feet) => Mathf.Abs(feet.x - padAt[p].x) <= PadHalf + 0.06f && Mathf.Abs(feet.y - padAt[p].y) <= PadHalf + 0.06f;

        private Pad? PadOf(Vector2 feet)
        {
            foreach (var kv in padAt) if (OnPad(kv.Key, feet)) return kv.Key;
            return null;
        }

        private void ShowPads(bool on)
        {
            padsOn = on; padsAt = Time.time; lit = null;
            foreach (var sr in pads.Values) sr.enabled = on;
            if (!on) Danger(null, false);
        }

        /// <summary>Mỗi khung: ô đang được gọi tên nhấp nháy, ô khác mờ; hiện dần khi mới bật.</summary>
        private void AnimatePads()
        {
            if (!padsOn) return;
            float fade = Mathf.Clamp01((Time.time - padsAt) / 0.6f);
            foreach (var kv in pads)
            {
                bool on = lit.HasValue && lit.Value == kv.Key;
                float a = on ? 0.75f + 0.25f * Mathf.Sin(Time.time * 12f) : 0.45f;
                kv.Value.color = new Color(1f, on ? 0.95f : 1f, on ? 0.7f : 1f, a * fade);
                float s = on ? 1f + 0.03f * Mathf.Sin(Time.time * 12f) : 1f;
                kv.Value.transform.localScale = new Vector3(s, s, 1f);
            }
            foreach (var d in danger) if (d != null && d.enabled) d.color = new Color(1f, 0.15f, 0.1f, 0.22f + 0.16f * Mathf.Sin(Time.time * 22f));
        }

        /// <summary>Phủ đỏ "vùng sẽ bị dậm": cả sân trừ ô `safe` (null = cả sân).</summary>
        private void Danger(Pad? safe, bool on)
        {
            foreach (var d in danger) if (d != null) d.enabled = false;
            if (!on) return;
            var rects = new List<Rect>();
            if (safe == null) rects.Add(A);
            else
            {
                var c = padAt[safe.Value];
                float l = c.x - PadHalf, r = c.x + PadHalf, b = c.y - PadHalf, t = c.y + PadHalf;
                rects.Add(Rect.MinMaxRect(A.xMin, t, A.xMax, A.yMax));        // trên
                rects.Add(Rect.MinMaxRect(A.xMin, A.yMin, A.xMax, b));        // dưới
                rects.Add(Rect.MinMaxRect(A.xMin, b, l, t));                  // trái
                rects.Add(Rect.MinMaxRect(r, b, A.xMax, t));                  // phải
            }
            for (int i = 0; i < rects.Count; i++)
            {
                var rc = rects[i];
                if (rc.width <= 0.01f || rc.height <= 0.01f) continue;
                while (danger.Count <= i)
                {
                    var sr = TrapArt.MakeRenderer(Ctx.Host.transform, "Drill Danger", A.center, Ctx.Player, -7);
                    FxLighting.MakeUnlit(sr);
                    sr.sprite = white;
                    danger.Add(sr);
                }
                var d = danger[i];
                d.enabled = true;
                d.transform.position = rc.center;
                d.transform.localScale = new Vector3(rc.width, rc.height, 1f);
            }
        }

        // ------------------------------------------------------------------ trạm 3
        private IEnumerator Inspection()
        {
            ordersDone = 0;
            DrillBoard.Goal = "ORDERS 0 / " + Orders; DrillBoard.GoalFrac = 0f;
            ShowPads(true);
            Sfx.Play("lift_grind", A.center, 0.6f);
            Hints.Show("SERGEANT SAYS: obey an order ONLY if it starts with \"SERGEANT SAYS\". Otherwise - FREEZE.", 6f);
            yield return Wait(1.6f);
            var script = Script();
            for (int i = 0; i < script.Count && !aborted && !stationFailed; i++)
            {
                float window = Mathf.Lerp(2.6f, 1.8f, i / (float)(script.Count - 1));
                yield return RunOrder(script[i], window);
                if (aborted || stationFailed) break;
                ordersDone++;
                DrillBoard.Goal = "ORDERS " + ordersDone + " / " + Orders; DrillBoard.GoalFrac = ordersDone / (float)Orders;
                yield return Wait(0.55f);
            }
            DrillBoard.Command = "";
            if (aborted || stationFailed) yield break;
            yield return Overload();
        }

        /// <summary>10 lệnh: kiểu cố định (dạy dần), hướng ngẫu nhiên (không trùng ô Ren đang đứng).</summary>
        private List<DrillOrder> Script()
        {
            var kinds = new (Kind k, bool real)[]
            {
                (Kind.Move, true), (Kind.Move, true), (Kind.Freeze, false), (Kind.Move, true), (Kind.Move, true),
                (Kind.Freeze, false), (Kind.Dirt, true), (Kind.Move, true), (Kind.Move, true), (Kind.Freeze, false),
            };
            var list = new List<DrillOrder>();
            foreach (var (k, real) in kinds) list.Add(new DrillOrder { Kind = k, Real = real });
            list[4].Pad = Pad.Center; list[4].Fixed = true;                 // "AT EASE" lần đầu dạy ô giữa
            list[8].Line = "SERGEANT SAYS: SERGEANT SAYS: SERG- SERG- *KZZT* ...";  // lỗi thẻ
            return list;
        }

        private static string Word(Pad p)
        {
            switch (p)
            {
                case Pad.Left: return "LEFT FLANK";
                case Pad.Right: return "RIGHT FLANK";
                case Pad.Forward: return "FORWARD";
                case Pad.Back: return "FALL BACK";
                default: return "AT EASE";
            }
        }

        private IEnumerator RunOrder(DrillOrder o, float window)
        {
            // chọn ô: khác ô Ren đang đứng (lệnh thật phải di chuyển; lệnh bẫy thì "mồi" một ô khác cho Ren muốn chạy)
            Pad? here = PadOf(PlayerFeet);
            if (o.Kind != Kind.Dirt && !(o.Fixed && here != o.Pad))
            {
                var pool = new List<Pad> { Pad.Left, Pad.Right, Pad.Forward, Pad.Back, Pad.Center };
                if (here.HasValue) pool.Remove(here.Value);
                o.Pad = pool[Random.Range(0, pool.Count)];
            }
            string word = o.Kind == Kind.Dirt ? "HIT THE DIRT" : Word(o.Pad);
            o.Text = (o.Real ? "SERGEANT SAYS: " : "") + word + "!";
            // đọc thẻ đục lỗ
            Sfx.Play("reel_spin", king.Pos, 0.6f);
            king.Play("idle", 10f);
            yield return Wait(0.45f);
            if (aborted || stationFailed) yield break;
            Sfx.Play("reel_stop", king.Pos, 0.7f);
            if (o.Line != null)
            {
                Bark(o.Line, 1.4f, true);
                Sfx.Play("lift_jam", king.Pos, 0.6f);
                king.Flash(new Color(0.6f, 0.9f, 1f), 0.4f);
                yield return Wait(1.0f);
                if (aborted || stationFailed) yield break;
            }
            Bark(o.Text, window + 0.4f, true);
            DrillBoard.Command = o.Text; DrillBoard.CommandTime = Time.time;
            lit = o.Kind == Kind.Dirt ? (Pad?)null : o.Pad;
            Vector2 start = PlayerFeet;
            // đếm ngược (thanh + trống)
            float t0 = Time.time, nextTick = 0f;
            while (Time.time - t0 < window && !aborted && !stationFailed)
            {
                float u = (Time.time - t0) / window;
                DrillBoard.TrackFrac = 1f - u;
                if (Time.time - t0 >= nextTick) { nextTick += 0.5f; Sfx.Play("footstep", king.Pos, 0.5f); }
                yield return null;
            }
            if (aborted || stationFailed) yield break;
            DrillBoard.TrackFrac = 0f;
            bool ok;
            switch (o.Kind)
            {
                case Kind.Move:
                    if (!o.Real) goto case Kind.Freeze;
                    Danger(o.Pad, true);
                    yield return Wait(0.4f);
                    yield return Stomp();
                    ok = OnPad(o.Pad, PlayerFeet);
                    if (!ok) Struck(king.Pos, 4f, null, false);
                    break;
                case Kind.Dirt:
                    Danger(null, true);
                    yield return Wait(0.4f);
                    yield return Stomp();
                    ok = Rolling;
                    if (!ok) Struck(king.Pos, 4f, null, false);
                    break;
                case Kind.Freeze:
                default:                                                     // lệnh bẫy: phải đứng yên
                    ok = (PlayerFeet - start).magnitude <= 0.55f;
                    if (!ok) yield return SackDrop();
                    break;
            }
            Danger(null, false);
            lit = null;
            DrillBoard.Command = "";
            if (aborted || stationFailed) yield break;
            if (ok)
            {
                DrillBoard.Say(o.Real ? "ORDER OBEYED!" : "GOOD - YOU FROZE!", Green);
                Sfx.Play("ui_confirm", PlayerFeet, 0.5f);
                if (Time.time >= nextLine && Random.value < 0.5f)
                    Bark(o.Real ? Pick("*CLICK* COMPLIANCE: ACCEPTABLE.", "Correct! ...Suspiciously correct.", "OBEDIENCE LEVEL: TOLERABLE.")
                                : Pick("Hm. You didn't move. NOBODY told you not to move! ...I did. Good.", "*WHIRR* TRAP CARD: INEFFECTIVE. Recalibrating.", "Clever recruit. I HATE clever recruits."), 2f, false);
            }
            else Bark(o.Kind == Kind.Freeze || !o.Real ? Pick("I did NOT say 'Sergeant says'! *THWACK*", "Did I say SERGEANT SAYS? DID I?!", "FREEZE means FREEZE, recruit!")
                     : o.Kind == Kind.Dirt ? Pick("I SAID DIRT! KISS IT!", "On your BELLY, recruit! ROLL!") : Pick("Wrong square! Can you READ ARROWS?!", "OUT OF FORMATION!", "That is NOT " + Word(o.Pad) + "!"), 2.2f, true);
        }

        /// <summary>Burlap dậm đất (đòn phạt cả vùng đỏ): hoạt ảnh nện + nứt + rung.</summary>
        private IEnumerator Stomp()
        {
            king.Play("dashend", 18f, false);
            yield return Wait(0.18f);
            Sfx.Play("duo_slam", king.Pos);
            CameraFollow.KickMain(Vector2.down, 0.09f, 0.3f);
            SpriteVfx.Spawn("duo_quake", king.Pos, 1.3f, Layer, Order - 1, false, 16f, false);
            Ctx.Run(IdleLater(0.6f));
        }

        private IEnumerator IdleLater(float s) { yield return new WaitForSeconds(s); if (king != null && phase == Phase.Running) king.Play("idle", 4f); }

        /// <summary>Phạt lệnh bẫy: bao cát rơi từ trên trời trúng đầu Ren (bóng báo 0,35 s).</summary>
        private IEnumerator SackDrop()
        {
            Vector2 at = PlayerFeet;
            var shadow = SpriteVfx.Spawn("drill/fx_arrow", at, 1.6f, Layer, Order - 1, false, 1f, false);
            if (shadow != null) { shadow.transform.rotation = Quaternion.Euler(0f, 0f, -90f); shadow.Tint(new Color(1f, 0.3f, 0.25f, 0.9f)); shadow.Looping(0.4f); }
            Sfx.Play("drill_whoosh", at, 0.8f);
            var sack = new DrillActor("sack", at, Ctx.Host.transform, Ctx.Player) { Harmless = true, FaceByVel = false };
            sack.Play("roll", 24f);
            actors.Add(sack);
            for (float t = 0f; t < 0.35f; t += Time.deltaTime) { sack.Pos = at; sack.Lift = Mathf.Lerp(2.2f, 0f, t / 0.35f); yield return null; }
            sack.Lift = 0f;
            Struck(at + Vector2.up * 0.4f, 3f, sack, false);
            Sfx.Play("heavy_land", at, 0.8f);
            SpriteVfx.Spawn("m3_dash_dust", at, 1f, Layer, Order - 1, false, 16f, false);
            yield return Wait(0.5f);
            Ctx.Run(Vanish(sack));
        }

        /// <summary>Đủ 10 lệnh: cỗ máy QUÁ TẢI — khói, kẹt bánh răng, lắp bắp, rồi tự cho qua.</summary>
        private IEnumerator Overload()
        {
            ShowPads(false);
            DrillBoard.Objective = "";
            Bark("SERGEANT SAYS... SERGEANT... SAYS... *KRRK* ...ERROR. ERROR. RECRUIT HAS FOLLOWED ALL ORDERS. THIS HAS NEVER HAPPENED.", 3.6f, true);
            Sfx.Play("lift_jam", king.Pos);
            for (int i = 0; i < 8 && !aborted; i++)
            {
                SpriteVfx.Spawn("drill/fx_snuff", king.Pos + new Vector2(Random.Range(-0.35f, 0.35f), Random.Range(0.4f, 1.0f)), 1.6f, Layer, Order + 3, false, 14f, false);
                king.Pos += Random.insideUnitCircle * 0.03f;
                king.Flash(new Color(1f, 0.6f, 0.3f), 0.2f);
                if (i % 3 == 0) Sfx.Play("steam_vent", king.Pos, 0.6f);
                yield return new WaitForSeconds(0.3f);
            }
            king.Play("dashend", 8f, false);
            Sfx.Play("heavy_land", king.Pos);
            CameraFollow.KickMain(Vector2.down, 0.08f, 0.3f);
            yield return new WaitForSeconds(0.8f);
            Bark("...REBOOTING. *ding* RECRUIT STATUS: PASSED. YARD: UNSEALED. ...Please don't tell the other dummies I overheated.", 4.4f, true);
            yield return new WaitForSeconds(2.2f);
        }

        private void ResetKing()
        {
            ShowPads(false);
            DrillBoard.Command = "";
            if (king == null) return;
            king.Pos = kingHome; king.Vel = Vector2.zero; king.Harmless = true; king.Play("idle", 4f); king.SetAlpha(1f); king.Lift = 0f;
            if (king.Sr != null) king.Sr.transform.rotation = Quaternion.identity;
        }

        private void SergeantGuide()
        {
            DrillBoard.Objective = "Obey ONLY orders that start with \"SERGEANT SAYS\" - stand on that square when the bar runs out. Any other order: don't move!";
            Guide(null);
            QuestGuide.Set(QuestTitle + " - Sergeant Says", "Obey the orders (" + ordersDone + "/" + Orders + ")", null);
        }
    }
}
