using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 01/11: dân làng. Đi lang thang trong vùng `area`, dừng nghỉ; hoặc đứng yên (`walker` = false).
    /// Ảnh theo hướng (PixelLab, cùng style Ren): "idle_E/S/N", "walk_E/S/N" (west = lật east). Thiếu hướng nào -> dùng hướng có sẵn.
    /// Có thân va chạm (Rigidbody2D Kinematic + capsule ở chân): người chơi không đi xuyên; đi bằng MovePosition.
    /// Có câu nói: lại gần nhấn Interact (T) -> quay về người chơi, hiện lần lượt từng câu.
    /// Do <see cref="VillagerDirector"/> tạo từ lớp "npcs" của Tiled.
    /// </summary>
    public sealed class Villager : MonoBehaviour
    {
        public Dictionary<string, Sprite[]> anims = new Dictionary<string, Sprite[]>();
        public Rect area;
        public bool walker = true;
        public float speed = 0.55f;
        public float fps = 8f;
        public string[] lines;
        /// <summary>02/11: tên hiện trên khung hội thoại.</summary>
        public string speaker = "";
        /// <summary>Hướng nhìn ban đầu: (1,0) east, (-1,0) west, (0,-1) south, (0,1) north.</summary>
        public Vector2 facing = Vector2.down;

        private SpriteRenderer sr;
        private Rigidbody2D rb;
        private Vector2 target;
        private float wait, frameT;
        private int frame;
        private bool moving;
        private Interactable talk;
        private readonly RaycastHit2D[] hits = new RaycastHit2D[8];
        private ContactFilter2D filter;

        private const float Radius = 0.11f;          // đơn vị thế giới (1 ô map = 0.32)
        private const float TalkHeight = 0.78f;

        public void Init(SpriteRenderer reference)
        {
            sr = gameObject.AddComponent<SpriteRenderer>();
            if (reference != null)
            {
                sr.sortingLayerID = reference.sortingLayerID;
                sr.sortingOrder = reference.sortingOrder;
            }
            sr.spriteSortPoint = SpriteSortPoint.Pivot;

            rb = gameObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            var col = gameObject.AddComponent<CapsuleCollider2D>();
            col.direction = CapsuleDirection2D.Horizontal;
            float s = Mathf.Max(0.01f, transform.lossyScale.x);
            col.size = new Vector2(0.30f, 0.14f) / s;              // ~ bề ngang bàn chân, theo đơn vị thế giới
            col.offset = new Vector2(0f, 0.05f) / s;

            filter = new ContactFilter2D();
            filter.NoFilter();
            filter.useTriggers = false;
            frame = Random.Range(0, 8);
            wait = Random.Range(0.3f, 2.5f);
            target = transform.position;
            Apply();

            if (lines != null && lines.Length > 0)
            {
                talk = InteractionSystem.Add(new Interactable
                {
                    id = "npc_" + name, verb = "Talk", clue = false, range = 0.35f,
                    rect = Box(), anchor = (Vector2)transform.position + new Vector2(0f, TalkHeight),
                    available = () => isActiveAndEnabled,
                    use = Say
                });
            }
        }

        private Rect Box() => new Rect((Vector2)transform.position - new Vector2(0.18f, 0.02f), new Vector2(0.36f, 0.62f));

        private void Say()
        {
            if (lines == null || lines.Length == 0 || DialogueBox.Active) return;
            // 02/11: khung hội thoại (tên + chữ chạy, [T] câu tiếp) thay dòng chữ mờ ở đáy; hai người quay mặt vào nhau tới khi đóng
            wait = 9999f;                                       // đứng yên suốt cuộc nói chuyện
            moving = false;
            var p = FindFirstObjectByType<PlayerController>();
            PlayerCombat pc = null;
            if (p != null)
            {
                Vector2 d = (Vector2)p.transform.position - (Vector2)transform.position;
                if (d.sqrMagnitude > 0.0001f) facing = d.normalized;
                pc = p.GetComponent<PlayerCombat>();
                if (pc != null) pc.LookAt(-d, 600f);
            }
            DialogueBox.Open(speaker, lines, () =>
            {
                wait = Random.Range(1f, 2.5f);
                if (pc != null) pc.StopLook();
            });
        }

        private void Update()
        {
            Animate(Time.deltaTime);
            if (talk != null)
            {
                talk.rect = Box();
                talk.anchor = (Vector2)transform.position + new Vector2(0f, TalkHeight);
                if (talk.glow != null) talk.glow.transform.position = transform.position + new Vector3(0f, 0.3f, 0f);
            }
        }

        // ---- 02/11: đi theo kịch bản (cảnh cắt): WalkTo -> tới nơi (hoặc bị chặn quá 2 giây) thì đứng; EndScript -> đi lang thang lại
        private bool scripted;
        private Vector2 scriptTarget;
        private float stuckFor;
        public bool Scripted => scripted;
        public bool Arrived { get; private set; }

        public void WalkTo(Vector2 p)
        {
            scripted = true; Arrived = false; stuckFor = 0f;
            scriptTarget = p; wait = 0f;
        }

        /// <summary>02/11: đặt tức thì tới `p` (vd bước ra từ cửa nhà / đi vào từ mép làng).</summary>
        public void Warp(Vector2 p)
        {
            transform.position = new Vector3(p.x, p.y, transform.position.z);
            if (rb != null) rb.position = p;
        }

        public void FaceTo(Vector2 worldPoint)
        {
            Vector2 d = worldPoint - (Vector2)transform.position;
            if (d.sqrMagnitude > 0.0001f) facing = d.normalized;
            moving = false;
        }

        public void EndScript()
        {
            scripted = false; moving = false;
            target = transform.position; wait = Random.Range(1f, 2.5f);
        }

        private void ScriptStep(float dt)
        {
            if (Arrived) { moving = false; return; }
            Vector2 p = rb.position, d = scriptTarget - p;
            float dist = d.magnitude;
            if (dist < 0.03f) { Arrived = true; moving = false; return; }
            Vector2 dir = d / dist;
            float step = Mathf.Min(dist, speed * dt);
            if (Blocked(p, dir, step + 0.04f))
            {
                moving = false; stuckFor += dt;
                if (stuckFor > 2f) Arrived = true;                // bị chặn (vd người chơi đứng đó) -> coi như tới
                return;
            }
            stuckFor = 0f;
            rb.MovePosition(p + dir * step);
            moving = true;
            facing = dir;
        }

        private void FixedUpdate()
        {
            if (rb == null) return;
            if (scripted) ScriptStep(Time.fixedDeltaTime);
            else if (walker) Wander(Time.fixedDeltaTime);
        }

        private void Wander(float dt)
        {
            if (wait > 0f) { wait -= dt; moving = false; if (wait <= 0f) PickTarget(); return; }
            Vector2 p = rb.position, d = target - p;
            float dist = d.magnitude;
            if (dist < 0.03f) { moving = false; wait = Random.Range(1.5f, 4.5f); return; }
            Vector2 dir = d / dist;
            float step = Mathf.Min(dist, speed * dt);
            if (Blocked(p, dir, step + 0.04f)) { moving = false; wait = Random.Range(0.4f, 1.2f); return; }
            rb.MovePosition(p + dir * step);
            moving = true;
            facing = dir;
        }

        private bool Blocked(Vector2 from, Vector2 dir, float len)
        {
            int n = Physics2D.CircleCast(from, Radius, dir, filter, hits, len);
            for (int i = 0; i < n; i++)
            {
                var c = hits[i].collider;
                if (c == null || c.isTrigger || c.attachedRigidbody == rb) continue;   // bỏ qua thân mình
                // 18/11 (§319): đi theo cảnh cắt thì KHÔNG coi người chơi / dân khác là vật chặn — trước đây Ren đứng trước cửa ngục
                // -> Pell bị chặn, đứng im 2 giây rồi "coi như tới": cả cảnh Pell không bước đi (không có hoạt ảnh đi).
                if (scripted && (c.GetComponentInParent<PlayerController>() != null || c.GetComponentInParent<Villager>() != null)) continue;
                // 02/11 (chạy thử): đang đứng LẤN vào collider (vd Todd bước ra ở bậc cửa, chân chạm tường nhà) -> CircleCast báo chạm ở
                // khoảng cách 0 dù đang đi RA xa -> kẹt, cả cảnh nói chuyện diễn ra khi Todd vẫn đứng ở cửa. Chỉ coi là chặn khi đi VÀO vật.
                if (hits[i].distance <= 0f)
                {
                    Vector2 away = from - c.ClosestPoint(from);
                    if (away.sqrMagnitude < 1e-6f || Vector2.Dot(away, dir) > 0f) continue;
                }
                return true;                                                         // tường, cây, nước, người chơi, dân làng khác
            }
            return false;
        }

        private void PickTarget()
        {
            for (int k = 0; k < 10; k++)
            {
                var t = new Vector2(Random.Range(area.xMin, area.xMax), Random.Range(area.yMin, area.yMax));
                var c = Physics2D.OverlapCircle(t, Radius + 0.04f);
                if (c != null && !c.isTrigger && c.attachedRigidbody != rb) continue;
                target = t;
                return;
            }
            target = transform.position;
        }

        /// <summary>Hướng -> hậu tố ảnh + lật.</summary>
        private string Dir(out bool flip)
        {
            flip = false;
            if (Mathf.Abs(facing.x) >= Mathf.Abs(facing.y) * 0.8f) { flip = facing.x < 0f; return "E"; }
            return facing.y > 0f ? "N" : "S";
        }

        /// <summary>Bộ ảnh theo hướng; thiếu hướng đó thì lấy hướng khác (used = hướng thực dùng).</summary>
        private Sprite[] Set(string anim, string dir, out string used)
        {
            used = dir;
            if (anims.TryGetValue(anim + "_" + dir, out var s) && s.Length > 0) return s;
            foreach (var d in new[] { "E", "S", "N" })
                if (anims.TryGetValue(anim + "_" + d, out s) && s.Length > 0) { used = d; return s; }
            return null;
        }

        private void Apply()
        {
            string dir = Dir(out bool flip);
            string used = dir;
            var set = (moving ? Set("walk", dir, out used) : null) ?? Set("idle", dir, out used);
            if (set == null) return;
            if (used == "E" && dir != "E") flip = facing.x < 0f;   // thiếu ảnh dọc -> đi ngang theo phía đang hướng
            sr.flipX = flip;
            sr.sprite = set[frame % set.Length];
        }

        private void Animate(float dt)
        {
            frameT += dt;
            if (frameT >= 1f / fps) { frameT = 0f; frame++; }
            Apply();
        }

        private void OnDisable() { moving = false; }
    }
}
