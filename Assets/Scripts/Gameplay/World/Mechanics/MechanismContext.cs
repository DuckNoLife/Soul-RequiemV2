using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using SuperTiled2Unity;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// What a mechanism needs from the world around it: flags, the player, objects by Tiled name, lights,
    /// image palettes (Vael glyphs, pictures, pipe sprites), tile coordinate conversion, running coroutines...
    /// MapMechanics creates one context for the whole map and gives it to every mechanism.
    /// </summary>
    public sealed class MechanismContext
    {
        public MapMechanics Host { get; }
        public MapEncounterDirector Director { get; }
        public WorldFlags Flags => Director.Flags;
        public Transform Player { get; }
        public Health PlayerHealth { get; }
        public CameraFollow Camera { get; }
        /// <summary>Damage tick of danger zones (seconds) - MapMechanics Inspector.</summary>
        public float DamageTick => Host.tick;

        internal readonly List<Mechanism> All = new List<Mechanism>();
        internal readonly Dictionary<string, List<GameObject>> Named = new Dictionary<string, List<GameObject>>();
        internal readonly Dictionary<string, Light2D> Lights = new Dictionary<string, Light2D>();
        internal readonly Dictionary<string, Sprite> PipeSprites = new Dictionary<string, Sprite>();
        internal readonly Dictionary<int, Sprite> Glyphs = new Dictionary<int, Sprite>();
        internal readonly Dictionary<string, Sprite> Pictures = new Dictionary<string, Sprite>();

        // state shared between mechanisms of the same kind
        internal readonly Dictionary<string, int> BellProgress = new Dictionary<string, int>();
        internal float NextBellSpawn;
        internal SpriteRenderer BeamTemplate;
        internal GameObject LeakTemplate;
        /// <summary>Console in use (camera panned to the pipe room).</summary>
        internal ConsoleMechanism ActiveConsole;

        internal MechanismContext(MapMechanics host, MapEncounterDirector director, Transform player, Health playerHealth, CameraFollow camera)
        {
            Host = host; Director = director; Player = player; PlayerHealth = playerHealth; Camera = camera;
        }

        // ------------------------------------------------------------------ objects by Tiled name
        public List<GameObject> NamedObjects(string name) => Named.TryGetValue(name, out var l) ? l : new List<GameObject>();

        /// <summary>Show / hide every object named in the list "a,b,c".</summary>
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

        /// <summary>Map layer (Transform) by name - e.g. a tile layer to fade.</summary>
        public Transform FindMapChild(string name)
        {
            foreach (var tr in Director.mapRoot.GetComponentsInChildren<Transform>(true))
                if (tr.name == name) return tr;
            return null;
        }

        // ------------------------------------------------------------------ coordinates
        /// <summary>"x,y" (Tiled tile) -> world coordinates; bad format -> fallback.</summary>
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

        // ------------------------------------------------------------------ player
        public void HurtPlayer(float amount, Vector2 dir, float knock)
        {
            if (PlayerHealth == null || PlayerHealth.IsDead) return;
            PlayerHealth.TakeDamage(amount, dir, knock);
        }

        /// <summary>Turn character control on / off (while standing at a console).</summary>
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

        // ------------------------------------------------------------------ coroutines / small effects
        public Coroutine Run(IEnumerator routine) => Host.StartCoroutine(routine);

        /// <summary>Shake the gate parts (gates layer) with the same name - feedback when slashing a barrier / valve.</summary>
        public IEnumerator ShakeGate(string gateName)
        {
            var parts = new List<Transform>();
            foreach (var so in Director.mapRoot.GetComponentsInChildren<SuperObject>())
                if (so.m_TiledName == gateName && so.transform.parent != null && (so.transform.parent.name == MapSchema.Layer.Gates || so.transform.parent.name.StartsWith(MapSchema.Layer.Gates + " (")))   /* also the duplicate-named layer "gates (1)" */ parts.Add(so.transform);
            var start = new List<Vector3>();
            foreach (var p in parts) start.Add(p.localPosition);
            for (int i = 0; i < 6; i++)
            {
                for (int k = 0; k < parts.Count; k++) if (parts[k] != null) parts[k].localPosition = start[k] + (Vector3)(Random.insideUnitCircle * 0.03f);
                yield return new WaitForSeconds(0.03f);
            }
            for (int k = 0; k < parts.Count; k++) if (parts[k] != null) parts[k].localPosition = start[k];
        }

        /// <summary>Colour "#rrggbb" + alpha; bad format -> Ichor blue.</summary>
        public static Color ParseColor(string hex, float alpha)
        {
            if (!ColorUtility.TryParseHtmlString(hex, out var c)) c = new Color(0.55f, 1f, 0.35f);
            c.a = alpha;
            return c;
        }
    }
}
