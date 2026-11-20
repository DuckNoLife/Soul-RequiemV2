using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 20/11 (§368): TRẠM 3 — FINAL EXAM, pha 2: LIVE FIRE — hạ Burlap bằng PHÁO CỐI RƠM (thay máy phóng bắn thẳng §360 bị chê "khó trúng").
    /// Sau bài thi, Burlap đá văng bảng đen, gọi 2 PHÁO CỐI (`mortar_at`, mặc định ô 8.5,136.6 / 16.5,136.6) + 3 ĐỐNG RƠM (`hay_at`) rơi xuống sân.
    /// Vòng lặp: Burlap nhắm -> vạch báo -> LAO THẲNG tới khi đụng vật cản:
    ///   • ĐỐNG RƠM  -> cắm đầu vào rơm, chỉ còn đôi chân giãy (props/drill_king_legs), vương miện văng ra đất — KẸT 4,2 / 3,6 / 3,0 s.
    ///   • TƯỜNG / PHÁO -> "BONK", sao choáng 2,4 / 2,1 / 1,8 s.
    ///   • Lao hụt giữa sân (không đụng gì — hiếm) -> không kẹt.
    /// Đang kẹt: pháo còn đạn sáng lên + mũi tên chỉ; đứng cạnh pháo bấm T -> đạn rơm bay VÒNG CUNG (1 s), vòng ngắm đỏ BÁM THEO Burlap -> chắc chắn rơi
    /// trúng hắn. Rơi lúc hắn đang kẹt = TRÚNG (nổ rơm, khựng hình, hắn bị hất văng xoay vòng); hắn không kẹt = hắn BẮT / gạt bay ("Hay is for HORSES!").
    /// Ren đứng trong vòng nổ = dính đạn của mình (-1 mạng, lăn né được). Pháo nạp lại 2,4 s.
    /// Trúng 3 phát = Burlap gục, bốc khói, "REBOOTING... PASSED". Từ phát 1: ra khỏi rơm là dậm sóng có khe; còn 1 máu: 2 làn lính rơm xoay.
    /// </summary>
    public sealed partial class DrillYardMechanism
    {
        private sealed class Mortar
        {
            public Vector2 Feet;
            public SpriteRenderer Sr, Shell;
            public GameObject Blocker;
            public bool Loaded = true;
            public float ReloadAt, Kick, Drop;
            public Vector2 Muzzle => Feet + Vector2.up * 0.54f;            // §369: miệng cối ~54 px trên chân
        }

        private sealed class Haystack
        {
            public Vector2 Feet;
            public SpriteRenderer Sr;
            public GameObject Blocker;
            public float Shake, Drop;
        }

        private readonly List<Mortar> mortars = new List<Mortar>();
        private readonly List<Haystack> hays = new List<Haystack>();
        private readonly List<GameObject> shellFx = new List<GameObject>();
        private SpriteRenderer legs, crown;
        private bool artilleryOn, kingStuck, kingHitNow, crownOff;
        private int kingHp = 3;
        private float stuckUntil, crownAt;
        private Vector2 crownFrom, crownTo;
        private Haystack stuckIn;
        private SpriteVfx kingStars;
        private Coroutine sweepers;
        private Sprite[] baleFrames;
        private const float MortarScale = 1f, HayScale = 1f, KingPropScale = 1.35f;   // §369: pháo / rơm 1:1 như đồ của gói; chân / vương miện theo Burlap (x1,35)

        // ------------------------------------------------------------------ dựng
        private void InitArtillery()
        {
            baleFrames = SpriteVfx.Load("drill/fx_bale");
            int i = 0;
            foreach (var part in Str("mortar_at", "8.5,136.6;16.5,136.6").Split(';'))
            {
                if (part.Trim().Length == 0) continue;
                var m = new Mortar { Feet = Ctx.TilePoint(part.Trim(), A.center) };
                m.Sr = Prop("Drill Mortar " + i, m.Feet, "VFX/props/drill_mortar", 0);
                m.Sr.transform.localScale = new Vector3(MortarScale, MortarScale, 1f);
                m.Sr.enabled = false;
                m.Shell = TrapArt.MakeRenderer(m.Sr.transform, "Drill Mortar Shell " + i, m.Feet, Ctx.Player, 1);
                m.Shell.transform.localPosition = new Vector3(0f, 0.55f, 0f);
                m.Shell.transform.localScale = new Vector3(0.75f, 0.75f, 1f);
                if (baleFrames.Length > 0) m.Shell.sprite = baleFrames[0];
                m.Shell.enabled = false;
                m.Blocker = AddBlocker(m.Feet, new Vector2(0.46f, 0.18f));
                m.Blocker.SetActive(false);
                var mm = m;
                InteractionSystem.Add(new Interactable
                {
                    id = Name + "_mortar_" + i, verb = "Fire", clue = false, calmOnly = false, range = 0.55f,
                    rect = new Rect(m.Feet.x - 0.3f, m.Feet.y, 0.6f, 0.55f),
                    anchor = m.Feet + Vector2.up * 0.8f,
                    available = () => artilleryOn && mm.Loaded && phase == Phase.Running && !inScene,
                    use = () => Fire(mm),
                });
                mortars.Add(m);
                i++;
            }
            i = 0;
            foreach (var part in Str("hay_at", "5,140.4;20.4,130.2;4.6,127.8").Split(';'))
            {
                if (part.Trim().Length == 0) continue;
                var h = new Haystack { Feet = Ctx.TilePoint(part.Trim(), A.center) };
                h.Sr = Prop("Drill Haystack " + i++, h.Feet, "VFX/props/drill_haystack", 0);
                h.Sr.transform.localScale = new Vector3(HayScale, HayScale, 1f);
                h.Sr.enabled = false;
                h.Blocker = AddBlocker(h.Feet, new Vector2(0.6f, 0.2f));
                h.Blocker.SetActive(false);
                hays.Add(h);
            }
            legs = Prop("Drill Burlap Legs", kingHome, "VFX/props/drill_king_legs", 0);
            legs.transform.localScale = new Vector3(KingPropScale, KingPropScale, 1f);
            legs.enabled = false;
            crown = Prop("Drill Burlap Crown", kingHome, "VFX/props/drill_king_crown", 1);
            crown.transform.localScale = new Vector3(KingPropScale, KingPropScale, 1f);
            crown.enabled = false;
        }

        private void ShowArtillery(bool on, bool fx)
        {
            artilleryOn = on;
            foreach (var m in mortars)
            {
                m.Sr.enabled = on; m.Shell.enabled = on; m.Blocker.SetActive(on);
                m.Loaded = true; m.Kick = 0f; m.Drop = on && fx ? 1f : 0f;
                m.Sr.transform.position = m.Feet; m.Sr.color = Color.white;
                if (fx) SpriteVfx.Spawn("drill/fx_snuff", m.Feet + Vector2.up * 0.2f, 2f, Layer, Order + 3, false, 14f, false);
            }
            for (int i = 0; i < hays.Count; i++)
            {
                var h = hays[i];
                h.Sr.enabled = on; h.Blocker.SetActive(on); h.Shake = 0f;
                h.Drop = on && fx ? 1f + i * 0.35f : 0f;
                h.Sr.transform.position = h.Feet;
            }
            if (!on)
            {
                legs.enabled = false; crown.enabled = false; crownOff = false;
                foreach (var g in shellFx) if (g != null) Object.Destroy(g);
                shellFx.Clear();
            }
        }

        private void ResetArtillery()
        {
            if (sweepers != null) { Ctx.Host.StopCoroutine(sweepers); sweepers = null; }
            StopStars();
            kingStuck = false; kingHitNow = false; stuckIn = null; kingHp = 3;
            ShowArtillery(false, false);
        }

        // ------------------------------------------------------------------ mỗi khung
        private void AnimateArtillery(float dt)
        {
            if (!artilleryOn && !crownOff) return;
            foreach (var m in mortars)
            {
                if (!m.Loaded && Time.time >= m.ReloadAt && artilleryOn)
                {
                    m.Loaded = true; m.Kick = 0.5f;
                    Sfx.Play("hit_wood", m.Feet, 0.35f);
                }
                m.Shell.enabled = artilleryOn && m.Loaded;
                m.Kick = Mathf.MoveTowards(m.Kick, 0f, dt * 4f);
                float k = Mathf.Sin(m.Kick * Mathf.PI * 3f) * m.Kick * 0.14f;
                m.Sr.transform.localScale = new Vector3(MortarScale * (1f + k), MortarScale * (1f - k), 1f);
                bool call = kingStuck && m.Loaded;                                         // đang kẹt -> pháo còn đạn sáng nhấp nháy
                m.Sr.color = call ? Color.Lerp(Color.white, new Color(1f, 0.85f, 0.4f), 0.5f + 0.5f * Mathf.Sin(Time.time * 14f)) : Color.white;
                if (m.Drop > 0f)
                {
                    m.Drop = Mathf.MoveTowards(m.Drop, 0f, dt / 0.3f);
                    m.Sr.transform.position = m.Feet + Vector2.up * (m.Drop * m.Drop * 1.6f);
                    if (m.Drop <= 0f) { m.Sr.transform.position = m.Feet; m.Kick = 1f; Sfx.Play("heavy_land", m.Feet, 0.6f); SpriteVfx.Spawn("m3_dash_dust", m.Feet, 1.1f, Layer, Order - 1, false, 16f, false); }
                }
            }
            foreach (var h in hays)
            {
                if (h.Drop > 0f)
                {
                    float before = h.Drop;
                    h.Drop = Mathf.MoveTowards(h.Drop, 0f, dt / 0.3f);
                    float u = Mathf.Min(1f, h.Drop);
                    h.Sr.transform.position = h.Feet + Vector2.up * (u * u * 1.8f);
                    h.Sr.enabled = artilleryOn && h.Drop <= 1f;
                    if (before > 0f && h.Drop <= 0f) { h.Sr.transform.position = h.Feet; h.Shake = 1f; Sfx.Play("drill_whoosh", h.Feet, 0.5f); SpriteVfx.Spawn("drill/fx_hayburst", h.Feet + Vector2.up * 0.2f, 0.7f, Layer, Order + 2, false, 18f, false); }
                }
                float shake = Mathf.Max(h.Shake, stuckIn == h && kingStuck ? 0.35f : 0f);
                h.Shake = Mathf.MoveTowards(h.Shake, 0f, dt * 2.5f);
                float sq = Mathf.Sin(Time.time * 30f) * shake * 0.08f;
                h.Sr.transform.localScale = new Vector3(HayScale * (1f + sq), HayScale * (1f - sq), 1f);
            }
            // đôi chân giãy trong đống rơm
            if (legs.enabled && stuckIn != null)
            {
                legs.transform.position = stuckIn.Feet + new Vector2(0f, 0.36f + Mathf.Abs(Mathf.Sin(Time.time * 9f)) * 0.03f);   // §369: rơm cao 0,52 đv
                legs.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 16f) * 12f);
            }
            // vương miện văng ra / bay về
            if (crown.enabled)
            {
                float u = Mathf.Clamp01((Time.time - crownAt) / 0.45f);
                Vector2 to = crownOff ? crownTo : king.Pos + Vector2.up * 0.95f;
                Vector2 p = Vector2.Lerp(crownFrom, to, u) + Vector2.up * Mathf.Sin(u * Mathf.PI) * 0.6f;
                crown.transform.position = p;
                crown.transform.rotation = Quaternion.Euler(0f, 0f, crownOff ? Mathf.Lerp(0f, 200f, u) : Mathf.Lerp(200f, 360f, u));
                if (!crownOff && u >= 1f) crown.enabled = false;
            }
        }

        // ------------------------------------------------------------------ chuyển pha
        private IEnumerator ExamToArtillery()
        {
            DrillBoard.Command = "";
            yield return Wait(1.0f);
            Bark("SIX FOR SIX?! *KRRK* EXAM PROTOCOL: EXHAUSTED. ...SWITCHING TO PROGRAM FOUR: LIVE FIRE!", 3.2f, true);
            Sfx.Play("lift_jam", king.Pos, 0.8f);
            king.Flash(new Color(1f, 0.4f, 0.3f), 0.6f);
            yield return Wait(1.6f);
            if (aborted || stationFailed) yield break;
            // đá văng bảng đen
            yield return WalkKing(boardAt + Vector2.left * 0.75f, 2f);
            king.Sr.flipX = false;
            king.Play("dashend", 16f, false);
            yield return Wait(0.2f);
            Sfx.Play("break_wood", boardAt, 0.9f);
            Sfx.Play("duo_punch", boardAt, 0.6f);
            CameraFollow.KickMain(Vector2.right, 0.08f, 0.25f);
            Ctx.Run(KickBoard());
            foreach (var d in doors) d.Open = false;
            Sfx.Play("door_wood", A.center, 0.6f);
            Bark("CLASS. DISMISSED.", 1.6f, true);
            yield return Wait(1.0f);
            ShowExam(false);
            if (aborted || stationFailed) yield break;
            yield return WalkKing(lectern, 2f);
            Bark("CORPORAL! HAUL OUT THE MORTARS! ...And the HAY. Lots of hay. For... DECORATION.", 3f, true);
            yield return Wait(0.8f);
            Sfx.Play("drill_deploy", A.center, 0.9f);
            ShowArtillery(true, true);
            CameraFollow.KickMain(Vector2.down, 0.07f, 0.3f);
            kingHp = 3;
            DrillBoard.Title = "FINAL EXAM - LIVE FIRE";
            DrillBoard.Goal = "HIT BURLAP 0 / 3"; DrillBoard.GoalFrac = 0f;
            yield return Wait(1.2f);
            Hints.Show("Bait Burlap into a HAYSTACK (or a wall), dodge his charge. While he's STUCK, run to a mortar and press T - the shell homes in on him.", 8f);
            Bark("Rules: I CHARGE. You get FLATTENED. And do NOT stand in front of my hay. ...That is NOT a hint.", 3.6f, true);
            yield return Wait(2.4f);
        }

        private IEnumerator KickBoard()
        {
            if (board == null) yield break;
            Vector2 from = board.transform.position;
            for (float t = 0f; t < 0.7f; t += Time.deltaTime)
            {
                float u = t / 0.7f;
                board.transform.position = from + new Vector2(1.6f * u, Mathf.Sin(u * Mathf.PI) * 1.4f + u * 0.4f);
                board.transform.rotation = Quaternion.Euler(0f, 0f, -540f * u);
                board.color = new Color(1f, 1f, 1f, 1f - u * u);
                yield return null;
            }
            SpriteVfx.Spawn("drill/fx_hayburst", (Vector2)board.transform.position, 0.6f, Layer, Order + 2, false, 18f, false);
            ShowBoard(false);
        }

        // ------------------------------------------------------------------ trận
        private IEnumerator Artillery()
        {
            while (kingHp > 0 && !aborted && !stationFailed)
            {
                if (kingHp == 1 && sweepers == null) { sweepers = Ctx.Run(Sweepers()); routines.Add(sweepers); }
                yield return BullRun();
            }
            if (aborted || stationFailed || kingHp > 0) yield break;
            // gục: bốc khói, khởi động lại
            foreach (var a in actors) Ctx.Run(Vanish(a));
            king.Play("dashend", 8f, false);
            Bark("...THREE. Three hay shells. To the FACE. *KRRK* SYSTEM... YIELDS.", 3f, true);
            Sfx.Play("lift_jam", king.Pos);
            for (int i = 0; i < 8 && !aborted; i++)
            {
                SpriteVfx.Spawn("drill/fx_snuff", king.Pos + new Vector2(Random.Range(-0.35f, 0.35f), Random.Range(0.4f, 1.0f)), 1.6f, Layer, Order + 3, false, 14f, false);
                king.Pos += Random.insideUnitCircle * 0.03f;
                king.Flash(new Color(1f, 0.6f, 0.3f), 0.2f);
                if (i % 3 == 0) Sfx.Play("steam_vent", king.Pos, 0.6f);
                yield return new WaitForSeconds(0.3f);
            }
            yield return new WaitForSeconds(0.6f);
            Bark("...REBOOTING. *ding* RECRUIT STATUS: PASSED. YARD: UNSEALED. ...Please don't tell the other dummies about the hay.", 4.4f, true);
            yield return new WaitForSeconds(2.4f);
        }

        /// <summary>Một lượt: nhắm -> vạch báo -> lao tới khi đụng rơm / tường / pháo -> kẹt (có thể bị bắn) -> ra (dậm sóng nếu đã bị thương).</summary>
        private IEnumerator BullRun()
        {
            float anger = 3 - kingHp;                                                     // 0, 1, 2
            float warn = Mathf.Lerp(1.0f, 0.7f, anger / 2f), speed = 6.4f + 1.5f * anger;
            king.Play("idle", 10f);
            yield return Wait(0.55f);
            if (aborted || stationFailed) yield break;
            Vector2 from = king.Pos;
            Vector2 dir = (PlayerFeet - from).sqrMagnitude > 0.01f ? (PlayerFeet - from).normalized : Vector2.down;
            int kind = 0;                                                                // 0 hụt, 1 tường, 2 rơm, 3 pháo
            Haystack hay = null; Mortar mort = null;
            float dist = 0f;
            while (dist < 30f)
            {
                var q = from + dir * (dist + 0.05f);
                if (!Inside(q)) { kind = 1; break; }
                if ((hay = HayAt(q)) != null) { kind = 2; break; }
                if ((mort = MortarAt(q)) != null) { kind = 3; break; }
                dist += 0.05f;
            }
            Vector2 stop = from + dir * dist;
            king.Sr.flipX = dir.x < 0f;
            Bark(Pick("CHAAARGE!", "INSPECTION!", "HOLD STILL!", "RAAAH! REGULATIONS!", "FOR THE WARDERS!"), 1f, true);
            DrillFx.Warn(Ctx.Host, from + dir * 0.35f, stop, warn, dir, Ctx.Player, 0.34f);
            for (float t = 0f; t < warn && !aborted && !stationFailed; t += Time.deltaTime) { king.Pos = from + Random.insideUnitCircle * 0.02f; yield return null; }
            king.Pos = from;
            if (aborted || stationFailed) yield break;
            king.Harmless = false; king.Credited = false;
            king.Play("dash", 18f);
            Sfx.Play("ram_charge", king.Pos);
            float dur = Mathf.Max(0.05f, dist / speed), dustAt = 0f;
            for (float t = 0f; t < dur && !aborted && !stationFailed; t += Time.deltaTime)
            {
                king.Pos = Vector2.Lerp(from, stop, t / dur);
                king.Sr.transform.rotation = Quaternion.Euler(0f, 0f, -8f * Mathf.Sign(dir.x));
                if (t >= dustAt) { dustAt = t + 0.07f; SpriteVfx.Spawn("m3_dash_dust", king.Pos, 1.1f, Layer, king.Sr.sortingOrder - 1, dir.x < 0f, 18f, false); }
                if (king.Credited && (king.Body - PlayerFeet).magnitude > 1f) king.Credited = false;
                yield return null;
            }
            king.Pos = stop;
            king.Sr.transform.rotation = Quaternion.identity;
            king.Harmless = true;
            if (aborted || stationFailed) yield break;
            bool hit;
            if (kind == 2) { yield return StuckInHay(hay, dir, Mathf.Lerp(4.2f, 3.0f, anger / 2f)); hit = lastStuckHit; }
            else if (kind == 1 || kind == 3) { yield return Dizzy(mort, dir, Mathf.Lerp(2.4f, 1.8f, anger / 2f)); hit = lastStuckHit; }
            else { Sfx.Play("heavy_land", king.Pos, 0.6f); yield return Wait(0.6f); hit = false; }
            if (aborted || stationFailed || kingHp <= 0) yield break;
            if (kingHp <= 2 && !hit) yield return ShockStomp();
            else if (hit) yield return Wait(0.4f);
        }

        private bool lastStuckHit;

        private Haystack HayAt(Vector2 q)
        {
            foreach (var h in hays)
                if (h.Sr.enabled && Mathf.Abs(q.x - h.Feet.x) < 0.42f && q.y > h.Feet.y - 0.15f && q.y < h.Feet.y + 0.45f) return h;
            return null;
        }

        private Mortar MortarAt(Vector2 q)
        {
            foreach (var m in mortars)
                if (m.Sr.enabled && Mathf.Abs(q.x - m.Feet.x) < 0.32f && q.y > m.Feet.y - 0.15f && q.y < m.Feet.y + 0.5f) return m;
            return null;
        }

        /// <summary>Cắm đầu vào đống rơm: thân ẩn, chỉ còn đôi chân giãy, vương miện văng ra; chờ hết giờ hoặc bị bắn trúng.</summary>
        private IEnumerator StuckInHay(Haystack h, Vector2 dir, float seconds)
        {
            lastStuckHit = false;
            kingStuck = true; kingHitNow = false; stuckIn = h;
            stuckUntil = Time.time + seconds;
            Sfx.Play("heavy_land", h.Feet, 0.8f);
            Sfx.Play("drill_whoosh", h.Feet, 0.7f);
            SpriteVfx.Spawn("drill/fx_hayburst", h.Feet + Vector2.up * 0.3f, 1.1f, Layer, Order + 2, false, 18f, false);
            CameraFollow.KickMain(dir, 0.1f, 0.3f);
            h.Shake = 1f;
            king.SetAlpha(0f);
            legs.enabled = true;
            crown.enabled = true; crownOff = true; crownAt = Time.time;
            crownFrom = h.Feet + Vector2.up * 0.5f;
            crownTo = h.Feet + new Vector2(dir.x >= 0f ? 0.75f : -0.75f, -0.3f);
            Bark(Pick("MMMPHF! *muffled* I MEANT TO DO THAT!", "MMF?! WHO TURNED OFF THE LIGHTS?!", "*muffled screaming in military*"), 2.4f, true);
            DrillBoard.Say("HE'S STUCK! FIRE A MORTAR (T)!", Green);
            while (Time.time < stuckUntil && !kingHitNow && !aborted && !stationFailed) yield return null;
            kingStuck = false;
            legs.enabled = false;
            if (aborted || stationFailed) yield break;
            if (kingHitNow) { lastStuckHit = true; yield return Launched(h.Feet + Vector2.up * 0.2f); stuckIn = null; yield break; }
            // tự chui ra
            stuckIn = null;
            king.Pos = h.Feet + Vector2.down * 0.35f;
            king.SetAlpha(1f);
            king.Play("dashend", 14f, false);
            h.Shake = 1f;
            SpriteVfx.Spawn("drill/fx_hayburst", h.Feet + Vector2.up * 0.3f, 0.9f, Layer, Order + 2, false, 18f, false);
            Sfx.Play("drill_whoosh", h.Feet, 0.6f);
            CrownHome();
            Bark(Pick("PFAH! HAY IN MY GEARS!", "...Nobody saw that.", "TOO SLOW, recruit! I've eaten worse hay!"), 2f, true);
            yield return Wait(0.5f);
        }

        /// <summary>Đâm tường / pháo: BONK, sao choáng, nghiêng người; chờ hết giờ hoặc bị bắn trúng.</summary>
        private IEnumerator Dizzy(Mortar m, Vector2 dir, float seconds)
        {
            lastStuckHit = false;
            kingStuck = true; kingHitNow = false;
            stuckUntil = Time.time + seconds;
            Sfx.Play("lg_wall_crash", king.Pos);
            CameraFollow.KickMain(dir, 0.12f, 0.3f);
            SpriteVfx.Spawn("duo_quake", king.Pos, 0.9f, Layer, Order - 1, false, 16f, false);
            if (m != null) { m.Kick = 1f; Sfx.Play("hit_wood", m.Feet, 0.6f); }
            Bark(Pick("BONK. ...I meant to do that.", "Who PUT that there?!", "Ow. Ow. Structural OW."), 2f, true);
            DrillBoard.Say("HE'S DIZZY! FIRE A MORTAR (T)!", Green);
            king.Play("idle", 3f);
            StopStars();
            kingStars = SpriteVfx.SpawnAttached("stun_stars", king.Sr.transform, 0.9f, Layer, Order + 3, 12f, seconds);
            if (kingStars != null) kingStars.transform.position = king.Pos + Vector2.up * 1.0f;
            while (Time.time < stuckUntil && !kingHitNow && !aborted && !stationFailed)
            {
                king.Sr.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 6f) * 10f);
                yield return null;
            }
            kingStuck = false;
            king.Sr.transform.rotation = Quaternion.identity;
            StopStars();
            if (aborted || stationFailed) yield break;
            if (kingHitNow) { lastStuckHit = true; yield return Launched(king.Pos); }
        }

        private void StopStars() { if (kingStars != null) kingStars.Stop(); kingStars = null; }

        private void CrownHome()
        {
            if (!crownOff) return;
            crownOff = false; crownAt = Time.time;
            crownFrom = crown.transform.position;
        }

        /// <summary>Trúng đạn: Burlap bị hất văng xoay 2 vòng, rơi xuống phía giữa sân.</summary>
        private IEnumerator Launched(Vector2 origin)
        {
            legs.enabled = false;
            king.SetAlpha(1f);
            king.Pos = origin;
            Vector2 to = origin + (A.center - origin).normalized * 1.4f;
            to = new Vector2(Mathf.Clamp(to.x, A.xMin + 0.6f, A.xMax - 0.6f), Mathf.Clamp(to.y, A.yMin + 0.5f, A.yMax - 0.9f));
            king.Play("dashend", 10f, false);
            for (float t = 0f; t < 0.6f; t += Time.deltaTime)
            {
                float u = t / 0.6f;
                king.Pos = Vector2.Lerp(origin, to, u);
                king.Lift = Mathf.Sin(u * Mathf.PI) * 1.3f;
                if (king.Sr != null) king.Sr.transform.rotation = Quaternion.Euler(0f, 0f, -720f * u * Mathf.Sign(to.x - origin.x + 0.001f));
                yield return null;
            }
            king.Lift = 0f;
            king.Pos = to;
            if (king.Sr != null) king.Sr.transform.rotation = Quaternion.identity;
            Sfx.Play("heavy_land", to);
            SpriteVfx.Spawn("m3_dash_dust", to, 1.3f, Layer, Order - 1, false, 16f, false);
            CameraFollow.KickMain(Vector2.down, 0.1f, 0.3f);
            CrownHome();
            if (kingHp > 0)
                Bark(kingHp == 2 ? "HAY?! From ABOVE?! ...Fine. Now I'm ANGRY. Angry is FASTER." : "TWO?! Battalion - REINFORCE ME! And somebody find my CROWN!", 3f, true);
            yield return Wait(0.6f);
        }

        // ------------------------------------------------------------------ pháo cối
        private void Fire(Mortar m)
        {
            if (!artilleryOn || !m.Loaded || phase != Phase.Running) return;
            m.Loaded = false; m.ReloadAt = Time.time + 2.4f; m.Kick = 1f;
            Sfx.Play("duo_mortar", m.Muzzle);
            CameraFollow.KickMain(Vector2.down, 0.05f, 0.2f);
            var smoke = SpriteVfx.Spawn("drill/fx_snuff", m.Muzzle + Vector2.up * 0.1f, 1.6f, Layer, Order + 3, false, 16f, false);
            if (smoke != null) smoke.Tint(new Color(1f, 0.95f, 0.85f, 0.9f));
            SpriteVfx.Spawn("m3_dash_dust", m.Feet, 1f, Layer, Order - 1, false, 16f, false);
            Ctx.Run(Shell(m));
        }

        /// <summary>Đạn rơm bay vòng cung 1 s; vòng ngắm đỏ + bóng đạn bám theo Burlap; rơi xuống: trúng / bị bắt / dính chính mình.</summary>
        private IEnumerator Shell(Mortar m)
        {
            Vector2 from = m.Muzzle;
            var sr = TrapArt.MakeRenderer(Ctx.Host.transform, "Drill Mortar Round", from, Ctx.Player, 4);
            if (baleFrames.Length > 0) sr.sprite = baleFrames[0];
            sr.transform.localScale = new Vector3(1.3f, 1.3f, 1f);
            var sh = TrapArt.MakeRenderer(Ctx.Host.transform, "Drill Round Shadow", from, Ctx.Player, -6);
            sh.sprite = DrillChalk.Shadow();
            var ret = TrapArt.MakeRenderer(Ctx.Host.transform, "Drill Reticle", king.Pos, Ctx.Player, -5);
            ret.sprite = DrillChalk.Reticle();
            FxLighting.MakeUnlit(ret);
            shellFx.Add(sr.gameObject); shellFx.Add(sh.gameObject); shellFx.Add(ret.gameObject);
            const float Dur = 1.0f, Peak = 2.4f;
            bool whistled = false;
            Vector2 target = king.Pos;
            for (float t = 0f; t < Dur; t += Time.deltaTime)
            {
                if (!artilleryOn || sr == null) { Kill(sr, sh, ret); yield break; }
                float u = t / Dur;
                target = Vector2.Lerp(target, king.Pos, 0.35f);                          // bám theo Burlap
                Vector2 ground = Vector2.Lerp(from, target, u);
                float h = 4f * Peak * u * (1f - u);
                sr.transform.position = ground + Vector2.up * h;
                sr.transform.rotation = Quaternion.Euler(0f, 0f, -u * 900f);
                sh.transform.position = ground;
                float ss = Mathf.Lerp(0.6f, 1.4f, u);
                sh.transform.localScale = new Vector3(ss, ss, 1f);
                ret.transform.position = target;
                float rs = Mathf.Lerp(1.7f, 0.95f, u) * (1f + 0.06f * Mathf.Sin(Time.time * 25f));
                ret.transform.localScale = new Vector3(rs, rs * 0.6f, 1f);
                ret.transform.rotation = Quaternion.identity;
                if (!whistled && u > 0.3f) { whistled = true; Sfx.Play("drill_shell_whistle", target, 0.8f); }
                yield return null;
            }
            Kill(sr, sh, ret);
            Land(target);
        }

        private void Kill(params SpriteRenderer[] list)
        {
            foreach (var s in list) if (s != null) { shellFx.Remove(s.gameObject); Object.Destroy(s.gameObject); }
        }

        private void Land(Vector2 at)
        {
            Sfx.Play("duo_shell_land", at);
            SpriteVfx.Spawn("drill/fx_hayburst", at + Vector2.up * 0.2f, 1.2f, Layer, Order + 3, false, 18f, false);
            CameraFollow.KickMain(Vector2.down, 0.08f, 0.25f);
            if (phase == Phase.Running && (PlayerFeet - at).magnitude < 0.5f)
            {
                Struck(at + Vector2.up * 0.3f, 3f, null);
                if (!stationFailed) Bark("FRIENDLY HAY! Stand CLEAR of the target, genius!", 2.2f, true);
            }
            if (!artilleryOn || kingHp <= 0 || phase != Phase.Running) return;
            if ((king.Pos - at).magnitude > 1.2f) return;
            if (kingStuck && !kingHitNow)
            {
                kingHitNow = true;
                kingHp--;
                int got = 3 - kingHp;
                DrillBoard.Goal = "HIT BURLAP " + got + " / 3"; DrillBoard.GoalFrac = got / 3f;
                DrillBoard.Say(kingHp > 0 ? "DIRECT HIT!" : "SERGEANT DOWN!", Gold);
                SpriteVfx.Spawn("drill/fx_hayburst", king.Pos + Vector2.up * 0.5f, 1.8f, Layer, Order + 3, false, 18f, false);
                Sfx.Play("blunt_strike", king.Pos);
                Sfx.Play("break_wood", king.Pos, 0.7f);
                king.Flash(new Color(1f, 0.85f, 0.4f), 0.35f);
                DrillFx.HitStop(Ctx.Host, 0.12f);
                CameraFollow.KickMain(Vector2.up, 0.14f, 0.35f);
                if (stuckIn != null) stuckIn.Shake = 1.5f;
                return;
            }
            // không kẹt: hắn gạt bay đạn
            king.Play("dashend", 18f, false);
            king.Flash(new Color(0.7f, 0.9f, 1f), 0.25f);
            Sfx.Play("parry", king.Pos, 0.7f);
            DrillBoard.Say("SWATTED! WAIT TILL HE'S STUCK", Red);
            Bark(Pick("HA! Hay is for HORSES, recruit!", "Swatted! I have HANDS, you know! Big puffy ones!", "Not while I'm LOOKING!"), 2.2f, true);
            Ctx.Run(IdleLater(0.5f));
        }

        private IEnumerator IdleLater(float s) { yield return new WaitForSeconds(s); if (king != null && phase == Phase.Running && !kingStuck) king.Play("idle", 4f); }

        /// <summary>Dậm đất: sóng chấn động có 3 khe (né vào khe / lăn xuyên).</summary>
        private IEnumerator ShockStomp()
        {
            Bark(Pick("STOMP DRILL! Find the gaps!", "Feel that? That's DISCIPLINE!"), 1.4f, true);
            king.Play("dashend", 14f, false);
            yield return Wait(0.6f);
            if (aborted || stationFailed) yield break;
            Sfx.Play("duo_slam", king.Pos);
            CameraFollow.KickMain(Vector2.down, 0.07f, 0.3f);
            SpriteVfx.Spawn("duo_quake", king.Pos, 1.4f, Layer, Order - 1, false, 14f, false);
            var w = Shockwave.Spawn(king.Pos, Mathf.Max(A.width, A.height) * 0.95f, 1.6f, 0f, Order - 1, new Color(1f, 0.8f, 0.5f, 0.95f));
            w.knockback = 0f;
            float g = Random.Range(0f, 120f); w.gapAngles = new[] { g, g + 120f, g + 240f }; w.gapWidth = 38f;
            w.onHit = hp => Struck(king.Pos, 4f, null);
            yield return Wait(0.6f);
            king.Play("idle", 4f);
        }

        /// <summary>Còn 1 máu: hai làn lính rơm xoay chạy ngang sân (`sweep_y`, ô Tiled).</summary>
        private IEnumerator Sweepers()
        {
            var ys = new List<float>();
            foreach (var part in Str("sweep_y", "125.4;133.8").Split(';'))
                if (part.Trim().Length > 0) ys.Add(Ctx.TilePoint("12," + part.Trim(), A.center).y);
            foreach (var y in ys) DrillFx.Warn(Ctx.Host, new Vector2(A.xMin + 0.3f, y), new Vector2(A.xMax - 0.3f, y), 1.2f, Vector2.right, Ctx.Player, 0.5f);
            yield return Wait(1.2f);
            float span = A.width + 1.2f;
            var list = new List<DrillActor>();
            for (int i = 0; i < ys.Count; i++)
                for (int k = 0; k < 3; k++)
                    list.Add(Spinner("straw", new Vector2(A.xMin - 0.6f + k * span / 3f, ys[i]), new Vector2(i % 2 == 0 ? 1.2f : -1.2f, 0f)));
            while (!aborted && !stationFailed && kingHp > 0)
            {
                foreach (var a in list)
                {
                    if (!actors.Contains(a)) continue;
                    if (a.Pos.x > A.xMax + 0.6f) a.Pos.x -= span; else if (a.Pos.x < A.xMin - 0.6f) a.Pos.x += span;
                    if (a.Credited && (a.Body - PlayerFeet).magnitude > 0.8f) a.Credited = false;
                }
                yield return null;
            }
        }

        // ------------------------------------------------------------------ chỉ dẫn
        private void ArtilleryGuide()
        {
            int got = 3 - kingHp;
            if (kingStuck)
            {
                Mortar best = null; float bd = float.MaxValue;
                foreach (var m in mortars)
                {
                    if (!m.Loaded) continue;
                    float d = (m.Feet - PlayerFeet).sqrMagnitude;
                    if (d < bd) { bd = d; best = m; }
                }
                DrillBoard.Objective = "Burlap is STUCK! Run to a glowing mortar and press T - the shell homes in on him.";
                Vector2? target = best != null ? best.Feet + Vector2.up * 0.75f : (Vector2?)null;
                Guide(target);
                QuestGuide.Set(QuestTitle + " - Live Fire", "Fire a mortar at Burlap!", target, got, 3);
                return;
            }
            DrillBoard.Objective = "Stand in front of a HAYSTACK and dodge his charge - he gets stuck. Shells fired while he's free get swatted away!";
            Guide(null);
            QuestGuide.Set(QuestTitle + " - Live Fire", "Bait Burlap into a haystack", null, got, 3);
        }
    }
}
