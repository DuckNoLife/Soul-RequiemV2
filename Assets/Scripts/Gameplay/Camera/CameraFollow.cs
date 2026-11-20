using UnityEngine;
using UnityEngine.Tilemaps;
using SoulRequiem.Core;
using SuperTiled2Unity;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// Gắn vào Main Camera: camera đi theo nhân vật nhưng KHÔNG BAO GIỜ nhìn ra ngoài map.
    /// - Target: nhân vật (để trống thì tự tìm object có PlayerController).
    /// - Map Root: object map (vd grassland/map1_grassland). Để trống thì tự lấy
    ///   mọi Tilemap có tile trong scene.
    /// - Khi nhân vật đi sát mép map, camera dừng lại ở mép; nhân vật lệch khỏi tâm màn hình.
    /// - Nếu map nhỏ hơn khung hình theo một chiều, camera đứng giữa map theo chiều đó.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField, Min(0f)] private float smoothTime = 0f;
        [SerializeField] private bool pixelSnap = true;
        // 01/11/2026: đổi tên từ pixelsPerUnit (giá trị cũ 32 trong scene sai — ảnh dùng PPU 100 -> camera nhảy 3 pixel ảnh mỗi bước,
        // ~8 px màn hình, nền giật so với nhân vật). Đổi tên để bỏ giá trị 32 đã lưu trong 3 scene.
        [Tooltip("Camera position is rounded to 1 / this (Unity units). Match the sprites' Pixels Per Unit (100).")]
        [SerializeField, Min(1f)] private float snapPixelsPerUnit = 100f;

        [Header("Giới hạn trong map")]
        [SerializeField] private bool clampToMap = true;
        [SerializeField] private Transform mapRoot;
        [Tooltip("Thu hẹp vùng camera được thấy (đơn vị Unity), để chừa mép map nếu mép có đồ vật cắt dở.")]
        [SerializeField, Min(0f)] private float inset = 0f;

        private Camera cam;
        private Vector3 velocity;

        // ---- lia camera tới một điểm (bảng điều khiển phòng ống, cảnh báo...) rồi trả về nhân vật
        private bool focusing, returning;
        private Vector3 focusPoint;
        private Vector3 focusRaw;                                    // vị trí lia chưa làm tròn điểm ảnh (02/11)
        private float focusSize, baseSize = -1f;
        private Behaviour pixelPerfect;              // URP Pixel Perfect Camera (nếu có) khoá kích thước -> tạm tắt khi phóng
        public bool IsFocusing => focusing;
        [Tooltip("02/11: độ nhanh khi lia tới điểm Focus / trả về (lớn = nhanh). Cảnh mở đầu hạ xuống ~1.6 cho lia chậm.")]
        public float focusSharpness = 6f;

        // ---- lùi camera (đấu trường boss): vẫn theo nhân vật, khung to hơn `zoomMul` lần
        private float zoomMul = 1f;
        private bool zooming;

        /// <summary>Phóng khung hình lên `mul` lần (vd 1.45 = thấy rộng hơn) mà vẫn theo nhân vật.</summary>
        public void SetZoom(float mul)
        {
            if (cam == null) cam = GetComponent<Camera>();
            if (!focusing && !returning && !zooming) baseSize = cam.orthographicSize;
            zoomMul = Mathf.Max(0.1f, mul);
            zooming = true;
            FindPixelPerfect();
            if (pixelPerfect != null) pixelPerfect.enabled = false;
        }

        /// <summary>Trả khung hình về như cũ.</summary>
        public void ClearZoom() { zoomMul = 1f; }

        // ---- 20/11 (§371): THEO SÁT nhân vật, không kẹp biên map (minigame lùi camera ở khu sát mép map: kẹp biên đẩy nhân vật lệch sang một bên)
        private bool freeFollow;
        private float freeBlend;                                     // 0 = kẹp biên như thường, 1 = bỏ kẹp; chuyển mượt 0,6 s
        public void SetFreeFollow(bool on) { freeFollow = on; }

        private void FindPixelPerfect()
        {
            if (pixelPerfect != null) return;
            foreach (var b in GetComponents<Behaviour>())
                if (b != null && b.GetType().Name.Contains("PixelPerfectCamera")) pixelPerfect = b;
        }

        /// <summary>Lia mượt tới `point` (toạ độ thế giới) và đổi kích thước khung (orthographicSize = size).</summary>
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

        /// <summary>Trả camera về theo nhân vật.</summary>
        public void Release()
        {
            if (!focusing) return;
            focusing = false;
            returning = true;
        }
        private Bounds mapBounds;
        private bool hasBounds;
        // 19/11 (§332): thuộc tính map `camera_clamp` = both (mặc định) | y | x | none. Map phòng rời trôi giữa nền đen (King's Barrow)
        // dùng "y": chiều ngang đi theo Ren — trước đây camera kẹp theo sân boss rộng nhất nên phòng hẹp bị đẩy lệch sang một bên.
        private bool clampX = true, clampY = true;

        // ---- giật camera ngắn theo một hướng (đòn chém trúng): đẩy ra rồi lò xo về, theo thời gian thật
        private Vector2 kickDir;
        private float kickAmp, kickTime, kickDur;

        /// <summary>Giật camera `amplitude` đơn vị theo `dir` rồi tắt dần trong `duration` giây (thời gian thật).</summary>
        /// <summary>Giật camera chính (nếu có CameraFollow) — dùng cho đòn nặng của quái / boss.</summary>
        public static void KickMain(Vector2 dir, float amplitude, float duration)
        {
            var cam = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            if (cam != null) cam.Kick(dir, amplitude, duration);
        }

        public void Kick(Vector2 dir, float amplitude, float duration)
        {
            kickDir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.zero;
            kickAmp = amplitude * GameSettings.ScreenShake;          // cài đặt Screen shake (0 = tắt)
            kickDur = Mathf.Max(0.01f, duration);                    // 01/11: 2 lệnh này từng nằm trong chú thích -> giật camera không chạy
            kickTime = 0f;
        }

        private Vector3 KickOffset()
        {
            if (kickTime >= kickDur || kickAmp <= 0f) return Vector3.zero;
            kickTime += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(kickTime / kickDur);
            float k = (1f - u) * (1f - u) * Mathf.Cos(u * Mathf.PI * 3f);   // đẩy ra, nảy ngược 1 lần, tắt
            return (Vector3)(kickDir * kickAmp * k);
        }

        private void Awake()
        {
            cam = GetComponent<Camera>();
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
                Debug.LogWarning("CameraFollow: chưa có Target. Kéo Player vào ô Target trên Main Camera.");
                enabled = false;
                return;
            }

            RefreshBounds();
            transform.position = Snap(Clamp(Goal()));
        }

        /// <summary>Nhảy tức thì tới người chơi (dịch chuyển trong map: không lia ngang qua các khu khác).</summary>
        public void SnapToTarget()
        {
            if (target == null) return;
            transform.position = Snap(Clamp(Goal()));
        }

        /// <summary>Gọi lại khi đổi map (vd chuyển sang Map 2).</summary>
        public void RefreshBounds()
        {
            hasBounds = false;
            var maps = mapRoot != null
                ? mapRoot.GetComponentsInChildren<Tilemap>()
                : FindObjectsByType<Tilemap>(FindObjectsSortMode.None);

            foreach (var tm in maps)
            {
                if (tm.GetUsedTilesCount() == 0) continue;      // bỏ qua tilemap rỗng (vd Grid/Ground cũ)
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
                Debug.LogWarning("CameraFollow: không tìm thấy Tilemap nào có tile để giới hạn camera.");
        }

        private void LateUpdate()
        {
            freeBlend = Mathf.MoveTowards(freeBlend, freeFollow ? 1f : 0f, Time.unscaledDeltaTime / 0.6f);   // §371
            if (focusing || returning)
            {
                float k = 1f - Mathf.Exp(-focusSharpness * Time.unscaledDeltaTime);
                Vector3 g = focusing ? focusPoint : Clamp(Goal());
                float s = focusing ? focusSize : baseSize * zoomMul;
                // 02/11: lia trên vị trí thô rồi làm tròn theo điểm ảnh + cộng rung (trước đây lúc lia không làm tròn -> ảnh pixel
                // rung rinh; rung màn hình bị dồn lại và giật một cái khi camera vừa về tới người chơi)
                focusRaw = Vector3.Lerp(focusRaw, g, k);
                cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, s, k);
                transform.position = Snap(focusRaw + KickOffset());
                if (returning && (focusRaw - g).sqrMagnitude < 0.0004f && Mathf.Abs(cam.orthographicSize - s) < 0.01f)
                {
                    returning = false;
                    cam.orthographicSize = s;
                    velocity = Vector3.zero;                         // SmoothDamp không mang vận tốc cũ từ trước cảnh
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
            if (freeBlend > 0f) p = Vector3.Lerp(p, raw, freeBlend);           // §371
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
