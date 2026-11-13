using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using SuperTiled2Unity;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Những gì một cơ quan cần từ thế giới xung quanh: cờ, người chơi, object theo tên Tiled, đèn,
    /// bảng ảnh (ký tự Vael, tranh, sprite ống), đổi toạ độ ô, chạy coroutine...
    /// MapMechanics tạo một context cho cả map và đưa cho mọi cơ quan.
    /// </summary>
    public sealed class MechanismContext
    {
        public MapMechanics Host { get; }
        public MapEncounterDirector Director { get; }
        public WorldFlags Flags => Director.Flags;
        public Transform Player { get; }
        public Health PlayerHealth { get; }
        public CameraFollow Camera { get; }
        /// <summary>Nhịp gây sát thương của vùng nguy hiểm (giây) — Inspector của MapMechanics.</summary>
        public float DamageTick => Host.tick;

        internal readonly List<Mechanism> All = new List<Mechanism>();
        internal readonly Dictionary<string, List<GameObject>> Named = new Dictionary<string, List<GameObject>>();
        internal readonly Dictionary<string, Light2D> Lights = new Dictionary<string, Light2D>();
        internal readonly Dictionary<string, Sprite> PipeSprites = new Dictionary<string, Sprite>();
        internal readonly Dictionary<int, Sprite> Glyphs = new Dictionary<int, Sprite>();
        internal readonly Dictionary<string, Sprite> Pictures = new Dictionary<string, Sprite>();

        // trạng thái dùng chung giữa các cơ quan cùng loại
        internal readonly Dictionary<string, int> BellProgress = new Dictionary<string, int>();
        internal float NextBellSpawn;
        internal SpriteRenderer BeamTemplate;
        internal GameObject LeakTemplate;
        /// <summary>Bàn điều khiển đang dùng (camera đã lia sang phòng ống).</summary>
        internal ConsoleMechanism ActiveConsole;

        internal MechanismContext(MapMechanics host, MapEncounterDirector director, Transform player, Health playerHealth, CameraFollow camera)
        {
            Host = host; Director = director; Player = player; PlayerHealth = playerHealth; Camera = camera;
        }

        // ------------------------------------------------------------------ object theo tên Tiled
        public List<GameObject> NamedObjects(string name) => Named.TryGetValue(name, out var l) ? l : new List<GameObject>();

        /// <summary>Bật / tắt mọi object có tên trong danh sách "a,b,c".</summary>
        public void Show(string names, bool on)
        {
            if (string.IsNullOrEmpty(names)) return;
            foreach (var n in names.Split(','))
                if (Named.TryGetValue(n.Trim(), out var list)) foreach (var go in list) if (go != null) go.SetActive(on);
        }

        public Light2D Light(string name) => Lights.TryGetValue(name, out var l) ? l : null;
        public Sprite Glyph(int digit) => Glyphs.TryGetValue(digit, out var s) ? s : null;
        public Sprite Picture(string name) => Pictures.TryGetValue(name ?? "", out var s) ? s : null;
        public Sprite PipeSprite(string key) => PipeSprites.TryGetValue(key, out var s) ? s : null;

        /// <summary>Lớp (Transform) của map theo tên — vd lớp tile để làm mờ.</summary>
        public Transform FindMapChild(string name)
        {
            foreach (var tr in Director.mapRoot.GetComponentsInChildren<Transform>(true))
                if (tr.name == name) return tr;
            return null;
        }

        // ------------------------------------------------------------------ toạ độ
        /// <summary>"x,y" (ô Tiled) -> toạ độ thế giới; sai định dạng -> fallback.</summary>
        public Vector3 TilePoint(string s, Vector2 fallback)
        {
            var p = (s ?? "").Split(',');
            if (p.Length == 2 && float.TryParse(p[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float tx) &&
                float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float ty))
                return Director.TileToWorld(tx, ty);
            return fallback;
        }

        public Vector2Int WorldToTile(Vector3 p)
        {
            var lp = Director.mapRoot.InverseTransformPoint(p);
            return new Vector2Int(Mathf.FloorToInt(lp.x / Director.tileSize), Mathf.FloorToInt(-lp.y / Director.tileSize));
        }

        // ------------------------------------------------------------------ người chơi
        public void HurtPlayer(float amount, Vector2 dir, float knock)
        {
            if (PlayerHealth == null || PlayerHealth.IsDead) return;
            PlayerHealth.TakeDamage(amount, dir, knock);
        }

        /// <summary>Bật / tắt điều khiển nhân vật (khi đứng ở bàn điều khiển).</summary>
        internal void SetPlayerControl(bool on)
        {
            MapMechanics.ConsoleActive = !on;
            var pc = Player.GetComponent<PlayerController>();
            if (pc != null) pc.enabled = on;
            var anim = Player.GetComponent<Animator>();
            if (!on && anim != null) anim.SetFloat("Speed", 0f);
            var rb = Player.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = Vector2.zero;
        }

        // ------------------------------------------------------------------ coroutine / hiệu ứng nhỏ
        public Coroutine Run(IEnumerator routine) => Host.StartCoroutine(routine);

        /// <summary>Rung các phần của cổng (layer gates) cùng tên — phản hồi khi chém vật cản / van.</summary>
        public IEnumerator ShakeGate(string gateName)
        {
            var parts = new List<Transform>();
            foreach (var so in Director.mapRoot.GetComponentsInChildren<SuperObject>())
                if (so.m_TiledName == gateName && so.transform.parent != null && so.transform.parent.name == MapSchema.Layer.Gates) parts.Add(so.transform);
            var start = new List<Vector3>();
            foreach (var p in parts) start.Add(p.localPosition);
            for (int i = 0; i < 6; i++)
            {
                for (int k = 0; k < parts.Count; k++) if (parts[k] != null) parts[k].localPosition = start[k] + (Vector3)(Random.insideUnitCircle * 0.03f);
                yield return new WaitForSeconds(0.03f);
            }
            for (int k = 0; k < parts.Count; k++) if (parts[k] != null) parts[k].localPosition = start[k];
        }

        /// <summary>Màu "#rrggbb" + alpha; sai định dạng -> xanh Ichor.</summary>
        public static Color ParseColor(string hex, float alpha)
        {
            if (!ColorUtility.TryParseHtmlString(hex, out var c)) c = new Color(0.55f, 1f, 0.35f);
            c.a = alpha;
            return c;
        }
    }
}
