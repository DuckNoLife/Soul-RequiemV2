using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// "Judgement Ray" — bộ Mantra Con Mắt, ô III: một tay bắn tia Ether xanh trong <see cref="duration"/> giây,
    /// tia xoay theo chuột (tay cầm: cần phải), Ren đứng yên. Tia xuyên quái, bị tường / đá chặn
    /// (trừ vật cản bao quanh chính con quái, vd hố của Plague Eye). Bấm lăn (Q) để huỷ sớm; hồi chiêu vẫn tính.
    /// 07/11 (§184, người dùng: "không tắt được chiêu"): BẤM LẠI phím chiêu (C) cũng tắt tia ngay (TryStop -> PlayerLoadout.TryCast);
    /// tắt sớm được hoàn <see cref="earlyRefund"/> × phần thời gian chưa dùng vào hồi chiêu. Trước đây bấm lại không có tác dụng
    /// (ô đang hồi + Ren đang bị khoá), chỉ Roll mới tắt được mà không có gợi ý nào.
    /// Tia chạm cơ quan như đòn chém (PlayerCombat.Struck): vd đèn Ether vỡ được bằng tia.
    /// </summary>
    [CreateAssetMenu(menuName = "Soul Requiem/Skill/Judgement Ray", fileName = "EyeRay")]
    public class JudgementRaySkill : SkillDefinition
    {
        // 11/11 (§228): thông số cho bảng Shrine.
        public override string Kind => "Channel - Beam";
        public override void Stats(System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>> o)
        {
            Stat(o, "Damage", dps.ToString("0") + " /s");
            Stat(o, "Duration", Secs(duration));
            Stat(o, "Range", Tiles(range));
            Stat(o, "Pierces", "every foe in line");
            Stat(o, "Stop early", "refund " + Pct(earlyRefund) + " of unused time");
        }

        public float duration = 5f;
        public float dps = 30f;
        [Tooltip("Giây giữa hai lần gây sát thương.")]
        public float tick = 0.15f;
        [Tooltip("Tầm tia (đơn vị Unity; 1 ô = 0,32).")]
        public float range = 8f;
        [Tooltip("Bề rộng HÌNH tia.")]
        public float width = 0.22f;
        [Tooltip("Bề rộng HITBOX tia (dò ở mặt đất, ngang chân quái). Xuyên mọi quái trên đường.")]
        public float hitWidth = 0.6f;
        [Tooltip("Tốc độ xoay tia theo chuột (độ / giây).")]
        public float turnRate = 220f;
        [Tooltip("Tắt tia sớm: hoàn (phần thời gian chưa bắn / duration) × hệ số này vào hồi chiêu.")]
        public float earlyRefund = 0.5f;
        [Tooltip("08/11 (§191): đang có màn Veil (Health.Guarded) thì vừa bắn vừa lướt với hệ số tốc độ này (0 = đứng yên).")]
        public float veiledMove = 0.4f;
        [NonSerialized] private bool firing, stopRequested;
        // 09/11 (§200): hình ray-hold hướng BẮC duỗi tay TRÁI (ra bên phải màn hình), còn TÂY-BẮC duỗi tay PHẢI (bên trái) -> quét tia
        // từ TB sang B thì tay phát tia nhảy sang bên kia người, tia bắn chéo qua đầu. Sửa: hướng Bắc mà tia lệch trái thì LẬT hình
        // (flipX quanh chân — áo, kiếm đối xứng) để tay duỗi đúng phía tia; đổi phía có vùng trễ quanh chính Bắc; điểm phát tia trượt mượt.
        [Tooltip("09/11: vùng trễ (độ) quanh chính Bắc trước khi đổi tay duỗi.")]
        public float northSwapDeadzone = 6f;
        [Tooltip("09/11: tốc độ trượt điểm phát tia khi đổi hình (1/giây, càng lớn càng nhanh).")]
        public float handBlend = 28f;
        [NonSerialized] private int northSide;                 // -1 tay bên trái (đã lật), +1 bên phải (hình gốc)
        [NonSerialized] private Vector2 handOffset;
        [NonSerialized] private bool handInit;
        [NonSerialized] private float firingEnd;
        [Tooltip("Hand position in each ray-hold sprite, in sprite pixels from the feet (x right, y up). " +
                 "Order: E, NE, N, NW, W, SW, S, SE. The ray starts here, so it leaves the outstretched hand.")]
        public Vector2[] handPixels =
        {
            // 09/11 (§202): Ren cầm katana tay PHẢI -> tia phát từ tay TRÁI duỗi thẳng (GamePackage/Ren/ray, khung 2-7), px từ chân
            // (§202b: pivot mới = tâm thân ở mặt đất)
            // 10/11 (§214): hoạt ảnh ray mới (lòng bàn tay trái đẩy thẳng ra trước) — đo tự động từ khung giữ
            new Vector2(38f, 65f), new Vector2(11f, 70f), new Vector2(-32f, 63f), new Vector2(-48f, 71f),
            new Vector2(-34f, 63f), new Vector2(-46f, 55f), new Vector2(37f, 62f), new Vector2(44f, 58f),
        };
        [Tooltip("Sprite pixels per Unity unit of the player art.")]
        public float spritePixelsPerUnit = 100f;

        private static readonly RaycastHit2D[] buf = new RaycastHit2D[48];
        private readonly List<KeyValuePair<EnemyAI, float>> enemies = new List<KeyValuePair<EnemyAI, float>>();
        private readonly List<KeyValuePair<Collider2D, float>> walls = new List<KeyValuePair<Collider2D, float>>();

        public override bool TryStop(PlayerCombat p)
        {
            if (!firing || Time.time >= firingEnd) return false;
            stopRequested = true;
            return true;
        }

        public override IEnumerator Cast(PlayerCombat p, Action cancel)
        {
            int id = p.BeginAction();
            Vector2 dir = Aim(p, p.Facing.sqrMagnitude > 0.01f ? p.Facing.normalized : Vector2.down);
            var glow = EtherVfx.Spawn("Judgement Ray", EtherVfx.Beam, p.transform.position, EtherVfx.Blue, p.VfxLayer, p.PlayerOrder + 3);
            var core = EtherVfx.Spawn("Core", EtherVfx.Beam, p.transform.position, EtherVfx.Core, p.VfxLayer, p.PlayerOrder + 4, glow.transform);
            core.transform.localScale = new Vector3(1f, 0.4f, 1f);
            var spark = EtherVfx.Spawn("Ray Spark", EtherVfx.Orb, p.transform.position, EtherVfx.Core, p.VfxLayer, p.PlayerOrder + 4);
            var handGlow = EtherVfx.Spawn("Ray Hand", EtherVfx.Orb, p.transform.position, EtherVfx.Blue, p.VfxLayer, p.PlayerOrder + 4);
            float end = Time.time + duration, nextTick = 0f;
            var done = new HashSet<Health>();
            p.RayPose(true, dir);                                // tư thế duỗi tay (nếu Animator có)
            northSide = dir.x < 0f ? -1 : 1; handInit = false;    // 09/11 (§200)
            firing = true; stopRequested = false; firingEnd = end;
            FirstTimeHints.Show("tut_ray_stop", $"Press {GameInput.KeyLabel(GameInput.SkillKey(setSlot >= 0 ? setSlot : 2))} again (or roll) to end the ray early.", 3.5f);

            while (Time.time < end && p.ActionAlive(id) && !stopRequested)   // lăn / choáng / gục / bấm lại -> tắt tia
            {
                p.LockActions(0.1f);                            // đứng yên trong lúc bắn
                p.LockedMoveMul = p.Health.Guarded ? veiledMove : 0f;   // 08/11 (§191): có Veil thì lướt chậm được
                dir = EtherVfx.RotateTowards(dir, Aim(p, dir), turnRate * Time.deltaTime);
                p.RayAim(dir);
                UpdateNorthFlip(p, dir);                          // 09/11 (§200)
                Vector2 origin = SmoothHand(p, dir);
                Vector2 ground = (Vector2)p.transform.position + dir * 0.12f;   // va chạm tính ở mặt đất (collider quái = vệt chân)
                float len = Scan(ground, dir);
                Place(glow, origin, dir, len);
                spark.transform.position = (Vector3)(origin + dir * len);
                spark.transform.localScale = Vector3.one * (0.7f + 0.15f * Mathf.Sin(Time.time * 35f));
                handGlow.transform.position = (Vector3)origin;
                handGlow.transform.localScale = Vector3.one * (0.8f + 0.1f * Mathf.Sin(Time.time * 25f));

                if (Time.time >= nextTick)
                {
                    nextTick = Time.time + tick;
                    done.Clear();
                    foreach (var e in enemies)
                    {
                        var ai = e.Key;
                        if (e.Value > len || ai == null || ai.Health == null || ai.Health.IsDead || done.Contains(ai.Health)) continue;
                        done.Add(ai.Health);
                        if (ai.Health.TakeDamage(dps * tick, dir, 0.15f) && UnityEngine.Random.value < 0.3f)
                            SpriteVfx.Spawn("hit", (Vector2)ai.transform.position + new Vector2(0f, 0.15f), 0.45f, p.VfxLayer, p.PlayerOrder + 5, dir.x < 0f);
                    }
                    PlayerCombat.RaiseStruck(ground + dir * len, 0.2f);
                }
                yield return null;
            }
            firing = false;
            p.LockedMoveMul = 0f;
            if (stopRequested || !p.ActionAlive(id))             // tắt sớm -> hoàn một phần hồi chiêu theo thời gian chưa bắn
                p.Loadout.Refund(this, earlyRefund * Mathf.Clamp01((end - Time.time) / Mathf.Max(0.01f, duration)));
            stopRequested = false;
            if (p.Sprite != null) p.Sprite.flipX = false;        // 09/11 (§200)
            p.RayPose(false, dir);
            if (glow != null) Destroy(glow.gameObject);
            if (spark != null) Destroy(spark.gameObject);
            if (handGlow != null) Destroy(handGlow.gameObject);
        }

        // Quét dọc tia: ghi lại quái + vật cản; trả về độ dài tia (tới vật cản gần nhất).
        // Vật cản bao quanh chính một con quái trên đường tia (vd hố Plague Eye) không chặn tia.
        private float Scan(Vector2 origin, Vector2 dir)
        {
            enemies.Clear(); walls.Clear();
            var filter = new ContactFilter2D { useTriggers = false, useLayerMask = false };
            // quái: quét rộng (hitWidth) -> xuyên mọi con trên đường tia
            int n = Physics2D.CircleCast(origin, hitWidth * 0.5f, dir, filter, buf, range);
            for (int i = 0; i < n; i++)
            {
                var ai = buf[i].collider.GetComponentInParent<EnemyAI>();
                if (ai != null) enemies.Add(new KeyValuePair<EnemyAI, float>(ai, buf[i].distance));
            }
            // tường / đá: quét mảnh theo đúng tâm tia (đá lệch sang bên không cắt ngắn tia)
            n = Physics2D.CircleCast(origin, 0.05f, dir, filter, buf, range);
            for (int i = 0; i < n; i++)
            {
                var c = buf[i].collider;
                if (c.GetComponentInParent<EnemyAI>() != null) continue;
                if (LineOfSight.IsSolid(c, false) && !c.OverlapPoint(origin)) walls.Add(new KeyValuePair<Collider2D, float>(c, buf[i].distance));
            }
            float len = range;
            foreach (var w in walls)
            {
                if (w.Value >= len) continue;
                bool enclosesEnemy = false;
                foreach (var e in enemies) if (e.Key != null && w.Key.OverlapPoint(e.Key.transform.position)) { enclosesEnemy = true; break; }
                if (!enclosesEnemy) len = w.Value;
            }
            return Mathf.Max(0.05f, len);
        }

        // Điểm phát tia = bàn tay trong hình ray-hold đang hiện. Animator (SimpleDirectional2D, 8 hướng) hiện hình của hướng
        // gần dir nhất -> lấy đúng hướng đó. Toạ độ tay: xem handPixels (Ren v2, 09/11 §201); nhân lossyScale.
        private Vector2 Hand(PlayerCombat p, Vector2 dir)
        {
            Vector2 feet = p.transform.position;
            if (handPixels == null || handPixels.Length < 8 || spritePixelsPerUnit <= 0f)
                return feet + new Vector2(0f, 0.2f) + dir * 0.12f;          // dữ liệu thiếu -> như cũ (giữa thân)
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;           // E = 0, ngược chiều kim đồng hồ: NE, N, NW, W, SW, S, SE
            int i = ((Mathf.RoundToInt(ang / 45f) % 8) + 8) % 8;
            Vector3 s = p.transform.lossyScale;
            Vector2 px = handPixels[i] / spritePixelsPerUnit;
            if (p.Sprite != null && p.Sprite.flipX) px.x = -px.x;          // 09/11 (§200): hình đang lật (Bắc, tay trái màn hình)
            return feet + new Vector2(px.x * Mathf.Abs(s.x), px.y * Mathf.Abs(s.y));
        }

        private static int DirIndex(Vector2 dir)
        {
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;           // E = 0, ngược chiều kim đồng hồ: NE, N, NW, W, SW, S, SE
            return ((Mathf.RoundToInt(ang / 45f) % 8) + 8) % 8;
        }

        /// <summary>09/11 (§200): hình hướng Bắc — tia lệch trái (qua vùng trễ) thì lật hình để tay duỗi bên trái.</summary>
        private void UpdateNorthFlip(PlayerCombat p, Vector2 dir)
        {
            float lean = Mathf.Sin(northSwapDeadzone * Mathf.Deg2Rad);
            if (dir.x < -lean) northSide = -1;
            else if (dir.x > lean) northSide = 1;
            // 09/11 (§202): hình Bắc duỗi tay TRÁI (bên trái màn hình) -> lật khi tia lệch PHẢI
            if (p.Sprite != null) p.Sprite.flipX = DirIndex(dir) == 2 && northSide > 0;
        }

        /// <summary>09/11 (§200): điểm phát tia trượt nhanh tới tay của hình đang hiện (không giật khi đổi hướng / đổi tay).</summary>
        private Vector2 SmoothHand(PlayerCombat p, Vector2 dir)
        {
            Vector2 feet = p.transform.position;
            Vector2 target = Hand(p, dir) - feet;
            if (!handInit) { handOffset = target; handInit = true; }
            else handOffset = Vector2.Lerp(handOffset, target, 1f - Mathf.Exp(-handBlend * Time.deltaTime));
            return feet + handOffset;
        }

        // Hướng ngắm: cần phải tay cầm (nếu đang gạt), không thì con trỏ chuột; không có gì -> giữ hướng cũ
        private Vector2 Aim(PlayerCombat p, Vector2 fallback)
        {
            var pad = Gamepad.current;
            if (pad != null)
            {
                Vector2 s = pad.rightStick.ReadValue();
                if (s.sqrMagnitude > 0.09f) return s.normalized;
            }
            var mouse = Mouse.current;
            var cam = Camera.main;
            if (mouse != null && cam != null)
            {
                Vector2 sp = mouse.position.ReadValue();
                Vector3 w = cam.ScreenToWorldPoint(new Vector3(sp.x, sp.y, Mathf.Abs(cam.transform.position.z)));
                Vector2 d = (Vector2)w - Hand(p, fallback);
                if (d.sqrMagnitude > 0.0025f) return d.normalized;
            }
            return fallback;
        }

        private void Place(SpriteRenderer r, Vector2 origin, Vector2 dir, float len)
        {
            r.transform.position = new Vector3(origin.x, origin.y, 0f);
            r.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            float pulse = 1f + 0.12f * Mathf.Sin(Time.time * 40f);
            r.transform.localScale = new Vector3(len / EtherVfx.BeamWidthUnits, width / EtherVfx.BeamHeightUnits * pulse, 1f);
        }
    }
}
