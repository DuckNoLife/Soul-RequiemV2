using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 20/11 (§360): TRẠM 3 — THE SERGEANT (đấu bò tót, không dùng kiếm) + MÁY PHÓNG BAO CỎ.
    /// Máy phóng (`launcher_1`, `launcher_2` = "x,y,hướng,tín_hiệu"): hình PixelLab, va chạm ở chân; LÀN BẮN = dải ngang trước miệng máy
    /// tới hết sân. Cần gạt riêng (lever, `fires` = tín_hiệu, `rearm`) -> máy giật, bao cỏ bay dọc làn (8,5 đv/s), vỡ ở tường bên kia.
    /// Burlap: nhìn Ren -> VẠCH BÁO (mũi tên tới chỗ dừng) -> LAO. Dừng ở chỗ Ren đứng (+ trượt 0,6 đv) -> CHOÁNG 1,9 s;
    /// đâm tường / máy phóng -> CHOÁNG 2,8 s. Đang choáng trong làn bắn -> mũi tên của làn đó sáng xanh + nhắc "PULL THE LEVER".
    /// Bao cỏ trúng hắn khi đang LAO hoặc CHOÁNG trong làn = 1 phát (đứng ngắm thì hắn BẮT được — phải dụ trước); đủ 3 phát = thắng.
    /// Bao cỏ trúng Ren (đứng trong làn) = -1 tinh thần.
    /// Sau phát 1: mỗi lần lao xong dậm đất ra sóng có 3 khe. Sau phát 2: thêm 2 làn lính rơm xoay chạy ngang (không trùng làn bắn).
    /// </summary>
    public sealed partial class DrillYardMechanism
    {
        private sealed class Launcher
        {
            public Vector2 Feet, Muzzle;
            public int Dir;
            public TrapListener Signal;
            public SpriteRenderer Sr;
            public float Kick, EmptyUntil;
            public float LaneY => Muzzle.y;
            /// <summary>Gầu ném (bao cỏ nằm sẵn trong gầu, phía SAU máy) — điểm bao cỏ bắt đầu bay.</summary>
            public Vector2 Bucket => Feet + new Vector2(-0.2f * Dir, 0.42f);
            /// <summary>§362: vạch làn bắn trên sàn + cần gạt của máy (cần gần nhất, để chỉ đường).</summary>
            public SpriteRenderer Lane;
            public Mechanism Lever;
            public bool KingIn;
            public GameObject Blocker;                     // §364: va chạm chân máy (tắt khi máy được cất đi)
            public float Drop;                             // §364: 1 -> 0 khi máy được kéo ra (rơi xuống, nảy)
        }

        private sealed class Bale
        {
            public SpriteRenderer Sr;
            public Vector2 Pos, Vel;
            public Launcher From;
            public bool Spent;
            public float Age;
        }

        private readonly List<Launcher> launchers = new List<Launcher>();
        private readonly List<Bale> bales = new List<Bale>();
        private int kingHp;
        private bool kingStunned;
        private float kingStunUntil, laneHintAt;
        private SpriteVfx kingStars;
        private const float LaneHalf = 0.36f;
        /// <summary>§362: dải CHÂN hợp lệ của làn bắn (vẽ trên sàn đúng bằng dải này): tâm = LaneY - 0,1, nửa bề rộng 0,46 đv.</summary>
        private const float LaneFeet = 0.46f;

        private Sprite launcherFull, launcherEmpty;

        private void InitLaunchers()
        {
            launcherFull = WardenStatueMechanism.Load("VFX/props/drill_launcher");
            launcherEmpty = WardenStatueMechanism.Load("VFX/props/drill_launcher_empty");
            for (int i = 1; i <= 4; i++)
            {
                var p = Str("launcher_" + i, "").Split(',');
                if (p.Length < 4) continue;
                var feet = (Vector2)Ctx.TilePoint(p[0].Trim() + "," + p[1].Trim(), A.center);
                int dir = p[2].Trim().StartsWith("-") ? -1 : 1;
                var L = new Launcher { Feet = feet, Dir = dir, Signal = new TrapListener(p[3].Trim()) };
                L.Muzzle = feet + new Vector2(0.3f * dir, 0.2f);
                L.Sr = Prop("Drill Hay Launcher " + i, feet, "VFX/props/drill_launcher", 0);
                L.Sr.flipX = dir < 0;                              // §360b: hình gốc (nhìn ngang, đúng chiều) quay mặt sang PHẢI (đông)
                L.Blocker = AddBlocker(feet, new Vector2(0.44f, 0.16f));
                launchers.Add(L);
                BuildLane(L);                                      // §362: vạch làn bắn trên sàn
            }
            SetArmory(false, false);                               // §364: máy phóng + cần + làn chỉ có mặt ở TRẠM 3
        }

        private void AnimateLaunchers(float dt)
        {
            foreach (var L in launchers)
            {
                if (L.Signal.Poll()) Fire(L);
                L.Kick = Mathf.MoveTowards(L.Kick, 0f, dt * 4f);
                // gầu rỗng trong lúc nạp lại (cần gạt rearm 2,2 s) -> có bao cỏ lại
                var want = Time.time < L.EmptyUntil ? launcherEmpty : launcherFull;
                if (want != null && L.Sr.sprite != want) L.Sr.sprite = want;
                float k = Mathf.Sin(L.Kick * Mathf.PI * 3f) * L.Kick * 0.12f;
                L.Sr.transform.localScale = new Vector3(1f + k, 1f - k, 1f);
                if (L.Drop > 0f)                                   // §364: máy được thả xuống sân (rơi từ trên, chạm đất)
                {
                    L.Drop = Mathf.MoveTowards(L.Drop, 0f, dt / 0.3f);
                    L.Sr.transform.position = L.Feet + Vector2.up * (L.Drop * L.Drop * 1.2f);
                    if (L.Drop <= 0f) { L.Sr.transform.position = L.Feet; L.Kick = 0.8f; }
                }
                AnimateLane(L);
            }
            for (int i = bales.Count - 1; i >= 0; i--)
            {
                var b = bales[i];
                b.Age += dt;
                b.Pos += b.Vel * dt;
                if (b.Sr != null)
                {
                    // 0,3 s đầu: bao cỏ vọt khỏi gầu thành vòng cung rồi hạ xuống ngang làn bắn (Pos = vết trên làn)
                    float u = Mathf.Clamp01(b.Age / 0.3f);
                    float lift = Mathf.Lerp(b.From.Bucket.y - b.From.Muzzle.y, 0f, u) + Mathf.Sin(u * Mathf.PI) * 0.28f;
                    b.Sr.transform.position = b.Pos + Vector2.up * lift;
                    b.Sr.transform.rotation = Quaternion.Euler(0f, 0f, -Mathf.Sign(b.Vel.x) * b.Age * 720f);
                }
                if (!b.Spent && b.Age > 0.12f) BaleHits(b);
                if (b.Spent || b.Pos.x < A.xMin + 0.1f || b.Pos.x > A.xMax - 0.1f)
                {
                    if (!b.Spent) { SpriteVfx.Spawn("drill/fx_hayburst", b.Pos, 0.55f, Layer, Order + 2, false, 18f, false); Sfx.Play("break_wood", b.Pos, 0.5f); }
                    if (b.Sr != null) Object.Destroy(b.Sr.gameObject);
                    bales.RemoveAt(i);
                }
            }
            if (phase == Phase.Running && station == 2 && kingStunned && Time.time >= laneHintAt)
            {
                var L = LaneOf(king.Pos);
                if (L != null)
                {
                    laneHintAt = Time.time + 1.2f;
                    DrillFx.Warn(Ctx.Host, L.Muzzle + new Vector2(0.4f * L.Dir, -0.2f), new Vector2(L.Dir > 0 ? A.xMax - 0.3f : A.xMin + 0.3f, L.Muzzle.y - 0.2f),
                                 1.1f, new Vector2(L.Dir, 0f), Ctx.Player, 0.5f, 0f, 1f);
                    DrillBoard.Say("HE'S IN THE LANE - PULL THE LEVER!", Green);
                }
            }
        }

        /// <summary>Máy phóng gần nhất có làn chứa điểm p (theo chân).</summary>
        private Launcher LaneOf(Vector2 p)
        {
            foreach (var L in launchers)
                if (Mathf.Abs(p.y - (L.LaneY - 0.1f)) < LaneFeet && (p.x - L.Muzzle.x) * L.Dir > -0.1f) return L;
            return null;
        }

        private void Fire(Launcher L)
        {
            if (!armory || Time.time < L.EmptyUntil) return;
            L.Kick = 1f;
            L.EmptyUntil = Time.time + 2f;
            Sfx.Play("goblin_launch", L.Muzzle);
            CameraFollow.KickMain(new Vector2(-L.Dir, 0f), 0.04f, 0.15f);
            SpriteVfx.Spawn("m3_dash_dust", L.Feet, 0.9f, Layer, Order - 1, L.Dir < 0, 16f, false);
            var start = new Vector2(L.Bucket.x, L.Muzzle.y);
            var sr = TrapArt.MakeRenderer(Ctx.Host.transform, "Drill Hay Bale", L.Bucket, Ctx.Player, 2);
            var bf = SpriteVfx.Load("drill/fx_bale");
            if (bf.Length > 0) sr.sprite = bf[0];
            bales.Add(new Bale { Sr = sr, Pos = start, Vel = new Vector2(8.5f * L.Dir, 0f), From = L });
        }

        private void BaleHits(Bale b)
        {
            // Burlap (chỉ khi đang đấu): thân lớn — xét theo chân trong làn + bao cỏ đi ngang qua thân
            if (phase == Phase.Running && station == 2 && kingHp > 0 && Mathf.Abs(king.Pos.y - (b.From.LaneY - 0.1f)) < LaneFeet + 0.03f && Mathf.Abs(king.Pos.x - b.Pos.x) < 0.38f)
            {
                b.Spent = true;
                if (kingStunned || !king.Harmless) { Ctx.Run(KingHit(b)); return; }
                // đang đứng ngắm: hắn BẮT được bao cỏ — phải dụ hắn lao rồi choáng đã
                SpriteVfx.Spawn("drill/fx_hayburst", b.Pos, 0.5f, Layer, Order + 2, false, 18f, false);
                Sfx.Play("parry", b.Pos, 0.6f);
                DrillBoard.Say("CAUGHT! STUN HIM FIRST", Red);
                Bark(Pick("HA! Caught it! Hay is for HORSES, recruit!", "Nice try! I SEE you, you know! I have a FACE!"), 2.2f, true);
                return;
            }
            // Ren đứng trong làn lúc bao cỏ bay qua
            if (phase == Phase.Running && (PlayerFeet + Vector2.up * 0.12f - b.Pos).magnitude < 0.26f)
            {
                b.Spent = true;
                SpriteVfx.Spawn("drill/fx_hayburst", b.Pos, 0.55f, Layer, Order + 2, false, 18f, false);
                Struck(b.Pos - b.Vel.normalized * 0.3f, 3f, null);
                if (Time.time >= nextLine) Bark("FRIENDLY HAY! Stand CLEAR of the lane when you pull, genius!", 2.4f, true);
            }
        }

        // ------------------------------------------------------------------ trận đấu
        private IEnumerator Bout()
        {
            kingHp = 3;
            DrillBoard.Goal = "HIT BURLAP 0 / 3"; DrillBoard.GoalFrac = 0f;
            yield return DeployArmory();                           // §364: kéo máy phóng ra sân
            // dạy luật: chỉ làn bắn của từng máy
            foreach (var L in launchers)
                DrillFx.Warn(Ctx.Host, L.Muzzle + new Vector2(0.4f * L.Dir, -0.2f), new Vector2(L.Dir > 0 ? A.xMax - 0.3f : A.xMin + 0.3f, L.Muzzle.y - 0.2f),
                             2.4f, new Vector2(L.Dir, 0f), Ctx.Player, 0.5f, 0f, 1f);
            Hints.Show("The painted strips are the launchers' firing lanes. Stand in one so Burlap charges into it, dodge, then pull that launcher's lever (T).", 6f);
            yield return Wait(1.6f);
            if (aborted || stationFailed) yield break;
            king.Harmless = true;
            int charges = 0;
            Coroutine sweep = null;
            while (kingHp > 0 && !aborted && !stationFailed)
            {
                if (kingHp == 1 && sweep == null) { sweep = Ctx.Run(Sweepers()); routines.Add(sweep); }
                yield return Charge(charges++);
                if (aborted || stationFailed || kingHp <= 0) break;
            }
            if (kingHp <= 0 && !aborted && !stationFailed)
            {
                king.Harmless = true; king.Vel = Vector2.zero;
                StopStars();
                foreach (var a in actors) Ctx.Run(Vanish(a));
                king.Play("dashend", 12f, false);
                Sfx.Play("heavy_land", king.Pos);
                CameraFollow.KickMain(Vector2.down, 0.1f, 0.35f);
                Bark("...THREE. Three hay bales. To the FACE. ...I yield, recruit. Officially. With DIGNITY.", 4f, true);
                yield return Wait(3.2f);
            }
        }

        /// <summary>Một lượt lao: nhắm -> báo -> lao -> choáng (đâm tường / máy thì choáng lâu hơn).</summary>
        private IEnumerator Charge(int n)
        {
            float anger = 3 - kingHp;                             // 0, 1, 2
            float warn = Mathf.Lerp(1.05f, 0.75f, anger / 2f), speed = 6.2f + 1.4f * anger;
            king.Play("idle", 10f);
            yield return Wait(n == 0 ? 0.2f : 0.55f);
            if (aborted || stationFailed) yield break;
            Vector2 from = king.Pos, target = PlayerFeet;
            Vector2 dir = (target - from).sqrMagnitude > 0.01f ? (target - from).normalized : Vector2.down;
            float want = (target - from).magnitude + 0.6f;
            // đường lao: dừng sớm ở tường sân / máy phóng (BONK)
            bool bonk = false;
            float dist = 0f, stepLen = 0.05f;
            while (dist < want)
            {
                var q = from + dir * (dist + stepLen);
                if (q.x < A.xMin + 0.3f || q.x > A.xMax - 0.3f || q.y < A.yMin + 0.15f || q.y > A.yMax - 0.45f || HitsLauncher(q)) { bonk = true; break; }
                dist += stepLen;
            }
            Vector2 stop = from + dir * dist;
            king.Sr.flipX = dir.x < 0f;
            Bark(Pick("CHAAARGE!", "INSPECTION!", "HOLD STILL!", "RAAAH! REGULATIONS!"), 1f, true);
            DrillFx.Warn(Ctx.Host, from + dir * 0.35f, stop, warn, dir, Ctx.Player, 0.34f);
            // gồng: rung tại chỗ
            float t0 = Time.time;
            while (Time.time - t0 < warn && !aborted && !stationFailed)
            {
                king.Pos = from + Random.insideUnitCircle * 0.02f;
                yield return null;
            }
            king.Pos = from;
            if (aborted || stationFailed) yield break;
            king.Harmless = false; king.Credited = false;
            king.Play("dash", 18f);
            Sfx.Play("ram_charge", king.Pos);
            float dur = Mathf.Max(0.05f, dist / speed), t = 0f, dustAt = 0f;
            while (t < dur && !aborted && !stationFailed)
            {
                t += Time.deltaTime;
                king.Pos = Vector2.Lerp(from, stop, t / dur);
                king.Sr.transform.rotation = Quaternion.Euler(0f, 0f, -8f * Mathf.Sign(dir.x));
                if (t >= dustAt) { dustAt = t + 0.07f; SpriteVfx.Spawn("m3_dash_dust", king.Pos, 1.1f, Layer, king.Sr.sortingOrder - 1, dir.x < 0f, 18f, false); }
                if (king.Credited && (king.Body - PlayerFeet).magnitude > 1f) king.Credited = false;
                yield return null;
            }
            king.Sr.transform.rotation = Quaternion.identity;
            king.Harmless = true;
            if (aborted || stationFailed) yield break;
            float stun;
            if (bonk)
            {
                Sfx.Play("lg_wall_crash", king.Pos);
                CameraFollow.KickMain(dir, 0.12f, 0.3f);
                SpriteVfx.Spawn("duo_quake", king.Pos, 0.9f, Layer, Order - 1, false, 16f, false);
                Bark(Pick("BONK. ...I meant to do that.", "Who PUT that there?!", "Ow. Ow. Structural OW."), 2f, true);
                stun = 2.8f;
            }
            else
            {
                Sfx.Play("heavy_land", king.Pos, 0.6f);
                stun = 1.9f;
            }
            yield return Stunned(stun);
            if (aborted || stationFailed || kingHp <= 0) yield break;
            if (kingHp <= 2) yield return Stomp();
        }

        private bool HitsLauncher(Vector2 q)
        {
            foreach (var L in launchers) if (Mathf.Abs(q.x - L.Feet.x) < 0.42f && q.y > L.Feet.y - 0.2f && q.y < L.Feet.y + 0.35f) return true;
            return false;
        }

        private IEnumerator Stunned(float seconds)
        {
            kingStunned = true; laneHintAt = 0f;
            kingStunUntil = Time.time + seconds;
            king.Play("idle", 3f);
            StopStars();
            kingStars = SpriteVfx.SpawnAttached("stun_stars", king.Sr.transform, 0.9f, Layer, Order + 3, 12f, seconds);
            if (kingStars != null) kingStars.transform.position = king.Pos + Vector2.up * 1.0f;
            while (Time.time < kingStunUntil && !aborted && !stationFailed) yield return null;
            kingStunned = false;
            StopStars();
        }

        private void StopStars() { if (kingStars != null) kingStars.Stop(); kingStars = null; }

        /// <summary>Bao cỏ đập trúng Burlap: tia rơm lớn, khựng, rung, hắn bị hất dọc làn, hết choáng ngay (tức giận).</summary>
        private IEnumerator KingHit(Bale b)
        {
            kingHp--;
            int got = 3 - kingHp;
            DrillBoard.Goal = "HIT BURLAP " + got + " / 3"; DrillBoard.GoalFrac = got / 3f;
            DrillBoard.Say(kingHp > 0 ? "DIRECT HIT!" : "SERGEANT DOWN!", Gold);
            SpriteVfx.Spawn("drill/fx_hayburst", king.Body + Vector2.up * 0.2f, 1f, Layer, Order + 3, false, 18f, false);
            Sfx.Play("blunt_strike", king.Pos);
            Sfx.Play("break_wood", king.Pos, 0.7f);
            king.Flash(new Color(1f, 0.85f, 0.4f), 0.35f);
            DrillFx.HitStop(Ctx.Host, 0.11f);
            CameraFollow.KickMain(b.Vel.normalized, 0.14f, 0.35f);
            if (kingHp > 0) Bark(kingHp == 2 ? "HAY?! In MY yard?! ...Fine. Now I'm ANGRY. Angry is FASTER." : "Two! TWO! Battalion - REINFORCE ME!", 3f, true);
            kingStunUntil = Mathf.Min(kingStunUntil, Time.time + 0.5f);
            Vector2 from = king.Pos, to = from + new Vector2(Mathf.Sign(b.Vel.x) * 0.7f, 0f);
            to.x = Mathf.Clamp(to.x, A.xMin + 0.4f, A.xMax - 0.4f);
            for (float t = 0f; t < 0.3f; t += Time.deltaTime) { king.Pos = Vector2.Lerp(from, to, 1f - (1f - t / 0.3f) * (1f - t / 0.3f)); yield return null; }
        }

        /// <summary>Dậm đất: sóng chấn động có 3 khe (khe là chỗ né / lăn xuyên được).</summary>
        private IEnumerator Stomp()
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
            w.onHit = h => Struck(king.Pos, 4f, null);
            yield return Wait(0.6f);
            king.Play("idle", 4f);
        }

        /// <summary>Sau phát 2: hai làn lính rơm xoay chạy ngang sân (tránh làn bắn của máy phóng).</summary>
        private IEnumerator Sweepers()
        {
            var ys = new List<float>();
            for (float y = A.yMin + 1.0f; y < A.yMax - 1.0f && ys.Count < 2; y += 1.6f)
            {
                bool clash = false;
                foreach (var L in launchers) if (Mathf.Abs(y - L.LaneY) < 0.8f) clash = true;
                if (!clash) ys.Add(y);
            }
            foreach (var y in ys) DrillFx.Warn(Ctx.Host, new Vector2(A.xMin + 0.3f, y), new Vector2(A.xMax - 0.3f, y), 1.2f, Vector2.right, Ctx.Player, 0.5f);
            yield return Wait(1.2f);
            float span = A.width + 1.2f;
            var list = new List<DrillActor>();
            for (int i = 0; i < ys.Count; i++)
                for (int k = 0; k < 3; k++)
                    list.Add(Spinner("straw", new Vector2(A.xMin - 0.6f + k * span / 3f, ys[i]), new Vector2((i % 2 == 0 ? 1.2f : -1.2f), 0f)));
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

        // ------------------------------------------------------------------ §364: kho máy phóng (chỉ trạm 3)
        private bool armory;
        private float armoryAt;

        /// <summary>Hiện / cất 2 máy phóng + cần gạt + vạch làn. fx = khói + tiếng kéo máy.</summary>
        private void SetArmory(bool on, bool fx)
        {
            armory = on; armoryAt = Time.time;
            foreach (var L in launchers)
            {
                if (L.Sr != null) L.Sr.enabled = on;
                if (L.Blocker != null) L.Blocker.SetActive(on);
                if (L.Lever is LeverMechanism lv) lv.Hidden = !on;
                if (L.Lane != null && !on) L.Lane.enabled = false;
                L.Drop = on && fx ? 1f : 0f;
                if (!on && L.Sr != null) L.Sr.transform.position = L.Feet;
                if (fx)
                {
                    SpriteVfx.Spawn("drill/fx_snuff", L.Feet + Vector2.up * 0.15f, 2.2f, Layer, Order + 3, false, 14f, false);
                    SpriteVfx.Spawn("m3_dash_dust", L.Feet, 1.2f, Layer, Order - 1, L.Dir < 0, 16f, false);
                    if (L.Lever != null) SpriteVfx.Spawn("drill/fx_snuff", L.Lever.Rect.center, 1.4f, Layer, Order + 3, false, 14f, false);
                }
            }
            if (fx) Sfx.Play("drill_deploy", A.center, on ? 0.9f : 0.6f);
        }

        private IEnumerator DeployArmory()
        {
            if (armory) yield break;
            Bark(Pick("Corporal! HAUL OUT THE LAUNCHERS! ...What do you mean, 'why'? For DECORATION!", "Bring out the hay launchers! They're purely CEREMONIAL!"), 2.6f, true);
            yield return Wait(0.6f);
            SetArmory(true, true);
            CameraFollow.KickMain(Vector2.down, 0.06f, 0.25f);
            yield return Wait(0.5f);
        }

        private void ResetKing()
        {
            if (armory) SetArmory(false, true);                    // §364: hết trạm 3 (thắng / trượt / bỏ dở) -> cất máy phóng
            if (king == null) return;
            StopStars();
            kingStunned = false;
            king.Pos = kingHome; king.Vel = Vector2.zero; king.Harmless = true; king.Play("idle", 4f); king.SetAlpha(1f);
            if (king.Sr != null) king.Sr.transform.rotation = Quaternion.identity;
        }
    }
}
