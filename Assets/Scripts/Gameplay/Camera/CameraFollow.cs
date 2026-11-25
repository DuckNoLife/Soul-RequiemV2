using UnityEngine;
using UnityEngine.Tilemaps;
using SoulRequiem.Core;
using SuperTiled2Unity;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// On the Main Camera: the camera follows the player but NEVER shows outside the map.
    /// - Target: the player (left empty -> finds the object with PlayerController).
    /// - Map Root: the map object (e.g. grassland/map1_grassland). Left empty -> uses
    ///   every Tilemap with tiles in the scene.
    /// - When the player walks near the map edge the camera stops at the edge; the player moves off screen centre.
    /// - If the map is smaller than the view along one axis, the camera centres on the map along that axis.
    /// Also: Focus (pan to a point), zoom-out for arenas, kicks / shake (Kick, KickMain), GameplaySize.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField, Min(0f)] private float smoothTime = 0f;
        [SerializeField] private bool pixelSnap = true;
        // renamed from pixelsPerUnit (the old value 32 saved in the scenes was wrong — images use PPU 100 -> the camera jumped 3 image pixels per step,
        // ~8 screen px, background jittering against the player). Renamed so the stored 32 in 3 scenes is dropped.
        [Tooltip("Camera position is rounded to 1 / this (Unity units). Match the sprites' Pixels Per Unit (100).")]
        [SerializeField, Min(1f)] private float snapPixelsPerUnit = 100f;

        [Header("Map bounds")]
        [SerializeField] private bool clampToMap = true;
        [SerializeField] private Transform mapRoot;
        [Tooltip("Shrink the visible camera area (Unity units), to leave out map edges with cut-off props.")]
        [SerializeField, Min(0f)] private float inset = 0f;

        private Camera cam;
        private Vector3 velocity;

        // ---- pan the camera to a point (pipe room control panel, warnings...) then back to the player
        private bool focusing, returning;
        private Vector3 focusPoint;
        private Vector3 focusRaw;                                    // pan position before pixel rounding
        private float focusSize, baseSize = -1f;
        private Behaviour pixelPerfect;              // URP Pixel Perfect Camera (if present) locks the size -> temporarily disabled while zooming
        public bool IsFocusing => focusing;
        [Tooltip("Speed when panning to a Focus point / back (higher = faster). The opening cutscene lowers it to ~1.6 for a slow pan.")]
        public float focusSharpness = 6f;
        /// <summary>Frame size during gameplay. Scenes still at the default size 2
        /// are switched to this value in Awake (before any Start, so cutscenes / bosses read the new size). 1.6 = 20 % closer.</summary>
        public static float GameplaySize = 1.6f;

        // ---- zoom out (boss arenas): still follows the player, frame `zoomMul` times larger
        private float zoomMul = 1f;
        private bool zooming;

        /// <summary>Scale the frame up `mul` times (e.g. 1.45 = wider view) while still following the player.</summary>
        public void SetZoom(float mul)
        {
            if (cam == null) cam = GetComponent<Camera>();
            if (!focusing && !returning && !zooming) baseSize = cam.orthographicSize;
            zoomMul = Mathf.Max(0.1f, mul);
            zooming = true;
            FindPixelPerfect();
            if (pixelPerfect != null) pixelPerfect.enabled = false;
        }

        /// <summary>Restore the frame size.</summary>
        public void ClearZoom() { zoomMul = 1f; }

        // ---- FOLLOW the player closely without clamping to map bounds (minigames zoom out near the map edge: clamping pushed the player off to one side)
        private bool freeFollow;
        private float freeBlend;                                     // 0 = normal clamping, 1 = no clamping; smooth 0.6 s transition
        public void SetFreeFollow(bool on) { freeFollow = on; }

        private void FindPixelPerfect()
        {
            if (pixelPerfect != null) return;
            foreach (var b in GetComponents<Behaviour>())
                if (b != null && b.GetType().Name.Contains("PixelPerfectCamera")) pixelPerfect = b;
        }

        /// <summary>Pan smoothly to `point` (world coordinates) and change the frame size (orthographicSize = size).</summary>
        public void Focus(Vector3 point, float size)
        {
            if (cam == null) cam = GetComponent<Camera>();
            if (!focusing && !returning && !zooming) baseSize = cam.orthographicSize;
            if (!focusing && !returning) focusRaw = transform.position;
            focusing = true;
            returning = false;
            focusPoint = new Vector3(point.x, point.y, transform.position.z);
            focusSize = size;
            if (pixelPerfect == null)
                foreach (var b in GetComponents<Behaviour>())
                    if (b != null && b.GetType().Name.Contains("PixelPerfectCamera")) pixelPerfect = b;
            if (pixelPerfect != null) pixelPerfect.enabled = false;
        }

        /// <summary>Return the camera to following the player.</summary>
        public void Release()
        {
            if (!focusing) return;
            focusing = false;
            returning = true;
        }
        private Bounds mapBounds;
        private bool hasBounds;
        // map property `camera_clamp` = both (default) | y | x | none. Maps of separate rooms floating on black (King's Barrow)
        // use "y": horizontally the camera follows Ren — clamping to the widest boss arena pushed narrow rooms off to one side.
        private bool clampX = true, clampY = true;

        // ---- short directional camera kick (hits landing): push out then spring back, on unscaled time
        private Vector2 kickDir;
        private float kickAmp, kickTime, kickDur;

        /// <summary>Kick the camera `amplitude` units along `dir`, decaying over `duration` seconds (unscaled time).</summary>
        /// <summary>Kick the main camera (if it has CameraFollow) — for heavy enemy / boss hits.</summary>
        public static void KickMain(Vector2 dir, float amplitude, float duration)
        {
            var cam = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            if (cam != null) cam.Kick(dir, amplitude, duration);
        }

        public void Kick(Vector2 dir, float amplitude, float duration)
        {
            kickDir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.zero;
            kickAmp = amplitude * GameSettings.ScreenShake;          // Screen shake setting (0 = off)
            kickDur = Mathf.Max(0.01f, duration);                    // keep these 2 lines as code: commented out, camera kicks stop working
            kickTime = 0f;
        }

        private Vector3 KickOffset()
        {
            if (kickTime >= kickDur || kickAmp <= 0f) return Vector3.zero;
            kickTime += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(kickTime / kickDur);
            float k = (1f - u) * (1f - u) * Mathf.Cos(u * Mathf.PI * 3f);   // push out, bounce back once, settle
            return (Vector3)(kickDir * kickAmp * k);
        }

        private void Awake()
        {
            cam = GetComponent<Camera>();
            // only scenes with a player (not the title screen) still at the default size 2 (scenes that set their own size keep it)
            if (cam.orthographic && Mathf.Abs(cam.orthographicSize - 2f) < 0.01f && (target != null || FindFirstObjectByType<PlayerController>() != null))
                cam.orthographicSize = GameplaySize;
        }

        private void Start()
        {
            if (target == null)
            {
                var player = FindFirstObjectByType<PlayerController>();
                if (player != null) target = player.transform;
            }
            if (target == null)
            {
                Debug.LogWarning("CameraFollow: no Target. Drag the Player into the Target slot on the Main Camera.");
                enabled = false;
                return;
            }

            RefreshBounds();
            transform.position = Snap(Clamp(Goal()));
        }

        /// <summary>Jump instantly to the player (teleport inside the map: no pan across other zones).</summary>
        public void SnapToTarget()
        {
            if (target == null) return;
            transform.position = Snap(Clamp(Goal()));
        }

        /// <summary>Call again after a map change (e.g. moving to Map 2).</summary>
        public void RefreshBounds()
        {
            hasBounds = false;
            var maps = mapRoot != null
                ? mapRoot.GetComponentsInChildren<Tilemap>()
                : FindObjectsByType<Tilemap>(FindObjectsSortMode.None);

            foreach (var tm in maps)
            {
                if (tm.GetUsedTilesCount() == 0) continue;      // skip empty tilemaps (e.g. old Grid/Ground)
                tm.CompressBounds();
                var lb = tm.localBounds;
                var b = new Bounds(tm.transform.TransformPoint(lb.center),
                                   Vector3.Scale(lb.size, tm.transform.lossyScale));
                if (!hasBounds) { mapBounds = b; hasBounds = true; }
                else mapBounds.Encapsulate(b);
            }

            clampX = clampY = true;
            var sm = mapRoot != null ? mapRoot.GetComponentInChildren<SuperMap>() : FindFirstObjectByType<SuperMap>();
            var mp = sm != null ? sm.GetComponent<SuperCustomProperties>() : null;
            string mode = mp != null ? mp.GetString("camera_clamp", "both").Trim().ToLowerInvariant() : "both";
            clampX = mode == "both" || mode == "x";
            clampY = mode == "both" || mode == "y";

            if (!hasBounds && clampToMap)
                Debug.LogWarning("CameraFollow: no Tilemap with tiles found to bound the camera.");
        }

        private void LateUpdate()
        {
            freeBlend = Mathf.MoveTowards(freeBlend, freeFollow ? 1f : 0f, Time.unscaledDeltaTime / 0.6f);
            if (focusing || returning)
            {
                float k = 1f - Mathf.Exp(-focusSharpness * Time.unscaledDeltaTime);
                Vector3 g = focusing ? focusPoint : Clamp(Goal());
                float s = focusing ? focusSize : baseSize * zoomMul;
                // pan on the raw position, then round to pixels + add shake (without rounding while panning, pixel art
                // shimmered, and shake piled up then jerked once the camera reached the player)
                focusRaw = Vector3.Lerp(focusRaw, g, k);
                cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, s, k);
                transform.position = Snap(focusRaw + KickOffset());
                if (returning && (focusRaw - g).sqrMagnitude < 0.0004f && Mathf.Abs(cam.orthographicSize - s) < 0.01f)
                {
                    returning = false;
                    cam.orthographicSize = s;
                    velocity = Vector3.zero;                         // SmoothDamp does not carry velocity from before the cutscene
                    if (pixelPerfect != null && !zooming) pixelPerfect.enabled = true;
                }
                return;
            }
            if (zooming)
            {
                float target = baseSize * zoomMul;
                float kz = 1f - Mathf.Exp(-3f * Time.unscaledDeltaTime);
                cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, target, kz);
                if (zoomMul == 1f && Mathf.Abs(cam.orthographicSize - baseSize) < 0.01f)
                {
                    cam.orthographicSize = baseSize;
                    zooming = false;
                    if (pixelPerfect != null) pixelPerfect.enabled = true;
                }
            }
            Vector3 goal = Clamp(Goal());
            Vector3 pos = smoothTime > 0f
                ? Vector3.SmoothDamp(transform.position, goal, ref velocity, smoothTime)
                : goal;
            transform.position = Snap(Clamp(pos) + KickOffset());
        }

        private Vector3 Goal() =>
            new Vector3(target.position.x, target.position.y, transform.position.z);

        private Vector3 Clamp(Vector3 p)
        {
            if (!clampToMap || !hasBounds || !cam.orthographic) return p;
            Vector3 raw = p;

            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            Vector3 min = mapBounds.min + new Vector3(inset, inset, 0f);
            Vector3 max = mapBounds.max - new Vector3(inset, inset, 0f);

            if (clampX) p.x = (max.x - min.x) <= 2f * halfW ? (min.x + max.x) * 0.5f : Mathf.Clamp(p.x, min.x + halfW, max.x - halfW);
            if (clampY) p.y = (max.y - min.y) <= 2f * halfH ? (min.y + max.y) * 0.5f : Mathf.Clamp(p.y, min.y + halfH, max.y - halfH);
            if (freeBlend > 0f) p = Vector3.Lerp(p, raw, freeBlend);
            return p;
        }

        private Vector3 Snap(Vector3 p)
        {
            if (!pixelSnap) return p;
            p.x = Mathf.Round(p.x * snapPixelsPerUnit) / snapPixelsPerUnit;
            p.y = Mathf.Round(p.y * snapPixelsPerUnit) / snapPixelsPerUnit;
            return p;
        }

        private void OnDrawGizmosSelected()
        {
            if (!hasBounds) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(mapBounds.center, mapBounds.size);
        }
    }
}
