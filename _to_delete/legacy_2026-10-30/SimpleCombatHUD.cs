using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// HUD chiến đấu (OnGUI) — giao diện "Slate & Gold" dựng từ asset có sẵn (bake sẵn vào Resources/UI/hud_*.png):
///   Tiny Swords UI (SpecialPaper → khung tối viền vàng + ô hành động, BigBar/SmallBar → thanh máu, Ribbons → dải tên, Icons),
///   2D Pixel Dungeon (bình máu), VFX của dự án (ground_impact cho kỹ năng F, stun_stars), Rogue Traveler (chân dung).
///
///   Góc trên trái : khung người chơi — chân dung, thanh máu (có vệt "máu vừa mất"), hàng biểu tượng trạng thái + giây còn lại.
///   Cạnh khung máu: Ô BÌNH MÁU riêng (số bình, phím E, loé khi uống, gạch chéo khi Hemorrhage).
///   Dưới 30% máu  : viền đỏ quanh màn hình đập theo nhịp tim.
///   Góc dưới trái : 3 ô chiến đấu — tên (ATTACK/ROLL/SKILL) trên ô, NẮP PHÍM J/Q/F/E nổi ở mép dưới, che tối theo hồi chiêu, loé vàng khi sẵn sàng,
///                   F bị khoá (Silence) / E bị chặn (Hemorrhage) hiện dấu X; số bình máu.
///   Trên phải     : tên khu trên dải ruy băng xám.
///   Dưới giữa     : thanh Boss lớn + tên trên dải ruy băng máu; phần sau "—" của tên (vd. "Coffins 3/4") hiện ở góc phải.
///   Giữa dưới     : gợi ý trên khung tối, mờ dần khi sắp hết.
///   Trên đầu quái : thanh nhỏ khung vàng + "máu/tối đa".
///   Dưới tên khu  : MINIMAP (QuestTracker: ảnh Resources/Minimap) xoay quanh người chơi — mũi tên = vị trí + hướng nhìn,
///                   hình thoi vàng = đích nhiệm vụ (ra ngoài khung thì mũi tên ở mép). Vùng bí mật (hidden) không hiện.
///                   Phím M: bản đồ lớn.
///   Dưới minimap  : khung NHIỆM VỤ CHÍNH (bước hiện tại, tiến độ n/N, đích kế tiếp + khoảng cách), loé khi đổi bước.
///   Trong màn hình: mũi tên nhấp nhô trên đích nhiệm vụ; đích ngoài màn hình -> mũi tên vàng ở mép chỉ hướng + số mét (1 ô = 1 m).
/// Thiếu ảnh nào thì vẽ khối màu thay thế (không lỗi).
///
/// Mọi kích thước nhân theo s = chiều cao màn hình / 720.
/// </summary>
public class SimpleCombatHUD : MonoBehaviour
{
    public Health player;
    public PlayerCombatPrototype kit;
    [Tooltip("Cỡ chữ chính ở màn hình cao 720 px (tự co giãn theo màn hình).")]
    public int fontSize = 18;
    [Tooltip("Cỡ chữ nhỏ nhất (px) để chữ vẫn đọc được trên cửa sổ rất nhỏ.")]
    public int minFontSize = 11;
    [Tooltip("Hiện chỉ số máu trên đầu quái thường.")]
    public bool enemyHpNumbers = true;

    // ---- ảnh (Resources/UI/hud_*) ----
    private Texture2D white, barBig, barSmall, barFill, panel, slot, ribBlood, ribSlate, portrait;
    private Texture2D pointer;
    private readonly Texture2D[] wp = new Texture2D[4];
    private GUIStyle questTitle, questHead;
    [Tooltip("Số ô hiển thị theo chiều ngang của minimap.")]
    public float minimapTiles = 34f;
    private Texture2D icAttack, icRoll, icSkill, icPotion, icPoison, icGod, icStun, icSlow, icSilence, icNoHeal;
    // thông số cắt lát (px ảnh nguồn) — khớp với tools/ui_bake.py
    private const float BigCap = 24f, SmallCap = 15f, RibCap = 31f;
    private static readonly float[] BigInset = { 11, 11, 11, 17 };    // trái, phải, trên, dưới
    private static readonly float[] SmallInset = { 10, 10, 8, 5 };
    private static readonly float[] PanelBorder = { 27, 22, 27, 21 }; // trái, trên, phải, dưới

    private static readonly Color Gold = new Color(0.93f, 0.78f, 0.42f);
    private static readonly Color Bone = new Color(0.93f, 0.9f, 0.82f);
    private static readonly Color HpRed = new Color(0.86f, 0.13f, 0.16f);
    private static readonly Color PoisonGreen = new Color(0.42f, 0.78f, 0.28f);
    private static readonly Color BossRed = new Color(0.62f, 0.06f, 0.14f);
    private static readonly Color MobRed = new Color(0.82f, 0.16f, 0.18f);
    private static readonly Color LagColor = new Color(1f, 0.86f, 0.62f, 0.85f);

    private GUIStyle label, center, small, smallCenter, right, tiny, tinyLeft, keyStyle, keyNum, keyCap, bigNum, potionNum, flaskNum;
    private float builtFor = -1f;
    private PlayerStatus status;

    // vệt máu vừa mất
    private float playerLag = 1f, playerLagHold, bossLag = 1f, bossLagHold;
    private Health lastBoss;
    // loé khi hồi chiêu xong
    private float rollWasLeft, skillWasLeft, rollFlash, skillFlash, flaskFlash;
    private int lastPotions = -1;

    private void Start()
    {
        if (player == null)
        {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null) { player = pc.GetComponent<Health>(); kit = pc.GetComponent<PlayerCombatPrototype>(); }
        }
        white = Texture2D.whiteTexture;
        barBig = T("bar_big"); barSmall = T("bar_small"); barFill = T("bar_fill"); panel = T("panel"); slot = T("slot");
        ribBlood = T("ribbon_blood"); ribSlate = T("ribbon_slate"); portrait = T("portrait");
        icAttack = T("ic_attack"); icRoll = T("ic_roll"); icSkill = T("ic_skill"); icPotion = T("ic_potion");
        icPoison = T("ic_poison"); icGod = T("ic_god"); icStun = T("ic_stun"); icSlow = T("ic_slow");
        icSilence = T("ic_silence"); icNoHeal = T("ic_noheal");
        pointer = T("pointer");
        for (int i = 0; i < wp.Length; i++) wp[i] = T("wp_" + i);
    }

    private static Texture2D T(string name)
    {
        var t = Resources.Load<Texture2D>("UI/hud_" + name);
        if (t == null) Debug.LogWarning($"[HUD] Missing Resources/UI/hud_{name}.png — drawing a plain box instead.");
        else { t.filterMode = FilterMode.Point; t.wrapMode = TextureWrapMode.Clamp; }
        return t;
    }

    private void BuildStyles(float s)
    {
        builtFor = s;
        int big = Mathf.Max(minFontSize + 2, Mathf.RoundToInt(fontSize * s));
        int sm = Mathf.Max(minFontSize, Mathf.RoundToInt((fontSize - 4) * s));
        label = new GUIStyle(GUI.skin.label)
        {
            fontSize = big, fontStyle = FontStyle.Bold, wordWrap = false, clipping = TextClipping.Overflow,
            alignment = TextAnchor.MiddleLeft, padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0)
        };
        label.normal.textColor = Bone;
        center = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter };
        small = new GUIStyle(label) { fontSize = sm };
        smallCenter = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter };
        right = new GUIStyle(small) { alignment = TextAnchor.MiddleRight };
        tiny = new GUIStyle(label) { fontSize = Mathf.Max(9, Mathf.RoundToInt(11 * s)), alignment = TextAnchor.MiddleCenter };
        keyStyle = new GUIStyle(tiny) { fontSize = Mathf.Max(9, Mathf.RoundToInt(12 * s)), alignment = TextAnchor.LowerRight };
        keyStyle.normal.textColor = Gold;
        tinyLeft = new GUIStyle(tiny) { alignment = TextAnchor.MiddleLeft };
        keyNum = new GUIStyle(tiny) { fontSize = keyStyle.fontSize, alignment = TextAnchor.MiddleRight };
        flaskNum = new GUIStyle(label) { fontSize = Mathf.Max(12, Mathf.RoundToInt(17 * s)), alignment = TextAnchor.UpperRight };
        potionNum = new GUIStyle(label) { fontSize = Mathf.Max(12, Mathf.RoundToInt(18 * s)), alignment = TextAnchor.MiddleLeft };
        keyCap = new GUIStyle(label) { fontSize = Mathf.Max(10, Mathf.RoundToInt(14 * s)), alignment = TextAnchor.MiddleCenter };
        questTitle = new GUIStyle(small) { wordWrap = true, alignment = TextAnchor.UpperLeft, clipping = TextClipping.Clip };
        questHead = new GUIStyle(tiny) { alignment = TextAnchor.MiddleLeft };
        bigNum = new GUIStyle(label) { fontSize = Mathf.Max(minFontSize, Mathf.RoundToInt(15 * s)), alignment = TextAnchor.MiddleCenter };
    }

    private void OnGUI()
    {
        float s = Mathf.Max(0.35f, Screen.height / 720f);
        // kiểm cả style mới nhất: sửa code khi đang Play, Unity giữ lại style cũ nhưng style mới thêm vẫn null -> dựng lại
        if (label == null || keyCap == null || questTitle == null || potionNum == null || flaskNum == null || !Mathf.Approximately(s, builtFor)) BuildStyles(s);
        bool repaint = Event.current.type == EventType.Repaint;
        if (repaint) Tick();

        float pad = 14 * s;
        var dir = MapEncounterDirector.Instance;

        DrawLowHpVignette();
        DrawEnemyBars(s);
        float actionRight = pad;
        if (player != null) DrawPlayerFrame(pad, s);
        if (kit != null) actionRight = DrawActionBar(pad, s);

        if (dir == null) return;

        if (!string.IsNullOrEmpty(dir.CurrentZoneTitle))
        {
            float h = 30 * s, tw = small.CalcSize(new GUIContent(dir.CurrentZoneTitle)).x;
            float cap = RibCap * h / 30f;
            var r = new Rect(Screen.width - pad - tw - 2 * cap - 8 * s, pad, tw + 2 * cap + 8 * s, h);
            Slice3(r, ribSlate, RibCap, RibCap, new Color(0.15f, 0.17f, 0.22f));
            Shadow(new Rect(r.x, r.y - 2 * s, r.width, r.height), dir.CurrentZoneTitle, smallCenter, s, Bone);
        }

        DrawGuide(pad, s);

        var boss = dir.Boss;
        float bossTop = Screen.height;
        if (boss != null && boss.Health != null && !boss.Health.IsDead)
            bossTop = DrawBoss(boss, pad, actionRight, s);

        if (Time.time < dir.HintUntil && !string.IsNullOrEmpty(dir.HintText))
        {
            float a = Mathf.Clamp01((dir.HintUntil - Time.time) / 0.35f);
            var size = label.CalcSize(new GUIContent(dir.HintText));
            float w = Mathf.Min(Screen.width - 2 * pad, size.x + 44 * s), h = size.y + 18 * s;
            float y = Mathf.Min(Screen.height - 120 * s, bossTop - 26 * s) - h;
            var r = new Rect((Screen.width - w) / 2f, y, w, h);
            var old = GUI.color; GUI.color = new Color(1, 1, 1, 0.92f * a);
            Slice9(r, panel, PanelBorder, 0.5f * s, new Color(0.1f, 0.1f, 0.14f));
            GUI.color = new Color(1, 1, 1, a);
            Shadow(r, dir.HintText, center, s, Bone);
            GUI.color = old;
        }
    }

    // ---------------- Viền đỏ khi máu thấp ----------------
    private Texture2D vignette;

    /// Dưới 30% máu: viền đỏ mờ quanh mép màn hình đập theo nhịp tim (mạnh dần khi máu càng thấp) — cảnh báo
    /// ngoại vi, không cần nhìn lên góc trái.
    private void DrawLowHpVignette()
    {
        if (player == null || player.IsDead) return;
        float pct = player.Percent;
        if (pct >= 0.3f) return;
        if (vignette == null)
        {
            const int n = 64;
            vignette = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Clamp01((Mathf.Sqrt(dx * dx + dy * dy) - 0.55f) / 0.6f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(255 * d * d));
                }
            vignette.SetPixels32(px);
            vignette.Apply();
        }
        float k = 1f - pct / 0.3f;                                  // 0 ở 30% -> 1 khi gần chết
        float beat = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * (3f + 3f * k)));
        var old = GUI.color;
        GUI.color = new Color(0.7f, 0.02f, 0.05f, (0.25f + 0.45f * k) * beat);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), vignette);
        GUI.color = old;
    }

    // ---------------- Người chơi ----------------

    private void DrawPlayerFrame(float pad, float s)
    {
        var P = new Rect(pad, pad, 330 * s, 84 * s);
        Slice9(P, panel, PanelBorder, 0.6f * s, new Color(0.1f, 0.1f, 0.14f));

        // chân dung
        var pr = new Rect(P.x + 12 * s, P.y + 12 * s, 60 * s, 60 * s);
        Box(pr, new Color(0.05f, 0.05f, 0.08f, 0.85f));
        if (portrait != null)
        {
            var old = GUI.color;
            if (player.IsDead) GUI.color = new Color(0.4f, 0.4f, 0.4f);
            else if (player.IsStunned) GUI.color = new Color(1f, 0.92f, 0.6f);
            GUI.DrawTexture(new Rect(pr.x + 2 * s, pr.y + 2 * s, pr.width - 4 * s, pr.height - 4 * s), portrait, ScaleMode.ScaleToFit);
            GUI.color = old;
        }
        Outline(pr, Gold, Mathf.Max(1f, Mathf.Round(2 * s)));

        // thanh máu
        float x = pr.xMax + 10 * s;
        var bar = new Rect(x, P.y + 12 * s, P.xMax - 12 * s - x, 32 * s);
        float pct = player.Percent;
        Color fill = player.IsPoisoned ? PoisonGreen : HpRed;
        if (pct < 0.3f && !player.IsDead) fill = Color.Lerp(fill, new Color(1f, 0.55f, 0.45f), 0.5f + 0.5f * Mathf.Sin(Time.time * 9f));
        FrameBar(bar, barBig, BigCap, BigInset, pct, playerLag, fill, s);
        Shadow(new Rect(bar.x, bar.y - 1 * s, bar.width, bar.height),
               $"{Mathf.CeilToInt(player.Current)} / {player.Max:0}", smallCenter, s, Bone);

        // trạng thái
        float bx = x + 2 * s, by = bar.yMax + 6 * s, bs = 26 * s;
        if (status == null) status = player.GetComponent<PlayerStatus>();
        if (kit != null && kit.GodMode) bx = Badge(bx, by, bs, icGod, "GOD", s, Gold);
        if (player.IsStunned) bx = Badge(bx, by, bs, icStun, "", s, Gold);
        if (player.IsPoisoned) bx = Badge(bx, by, bs, icPoison, "", s, PoisonGreen);
        if (status != null)
        {
            if (status.SlowLeft > 0f) bx = Badge(bx, by, bs, icSlow, Secs(status.SlowLeft), s, new Color(0.55f, 0.75f, 1f));
            if (status.SilenceLeft > 0f) bx = Badge(bx, by, bs, icSilence, Secs(status.SilenceLeft), s, new Color(0.75f, 0.6f, 1f));
            if (status.HealBlockLeft > 0f) bx = Badge(bx, by, bs, icNoHeal, Secs(status.HealBlockLeft), s, new Color(1f, 0.4f, 0.4f));
        }
        if (kit != null) DrawFlask(new Rect(P.xMax + 8 * s, P.y + 6 * s, 62 * s, 62 * s), s);
    }

    /// BÌNH MÁU — ô riêng gắn ngay bên phải khung máu (tài nguyên hồi máu đi cùng thanh máu, kiểu Estus của Souls),
    /// tách khỏi hàng chiêu chiến đấu. Số bình to viền đen ở góc trên phải, nắp phím E ở mép dưới.
    ///   uống: loé đỏ-vàng | hết bình: icon xám, số đỏ | Hemorrhage: icon gạch chéo, viền đỏ nhấp nháy + giây còn lại.
    private void DrawFlask(Rect r, float s)
    {
        bool blocked = status != null && status.HealBlocked, empty = kit.Potions <= 0;
        if (slot != null) GUI.DrawTexture(r, slot);
        else Slice9(r, panel, PanelBorder, 0.45f * s, new Color(0.1f, 0.1f, 0.14f));
        var inner = new Rect(r.x + 9 * s, r.y + 8 * s, r.width - 18 * s, r.height - 18 * s);
        var old = GUI.color;
        var icon = blocked ? icNoHeal : icPotion;
        if (icon != null)
        {
            GUI.color = empty && !blocked ? new Color(0.4f, 0.4f, 0.4f, 1f) : Color.white;
            GUI.DrawTexture(inner, icon, ScaleMode.ScaleToFit);
        }
        if (flaskFlash > 0f)
        {
            GUI.color = new Color(1f, 0.55f, 0.35f, 0.6f * flaskFlash);
            GUI.DrawTexture(new Rect(r.x + 4 * s, r.y + 4 * s, r.width - 8 * s, r.height - 8 * s), white);
        }
        GUI.color = old;
        if (blocked)
        {
            float a = 0.6f + 0.4f * Mathf.Sin(Time.time * 8f);
            Outline(r, new Color(1f, 0.3f, 0.3f, a), Mathf.Max(1f, Mathf.Round(2 * s)));
            Shadow(new Rect(r.x, r.yMax + 12 * s, r.width, 14 * s), Secs(status.HealBlockLeft), tiny, s, new Color(1f, 0.45f, 0.45f));
        }
        // số bình: góc trên phải, trong ô
        OutlinedLabel(new Rect(r.x + 6 * s, r.y + 3 * s, r.width - 10 * s, 20 * s), kit.Potions.ToString(), flaskNum, s,
                      empty ? new Color(1f, 0.4f, 0.4f) : Color.white);
        KeyCap(r, "E", s);
    }

    /// Nắp phím nổi ở giữa mép dưới một ô.
    private void KeyCap(Rect r, string key, float s)
    {
        float kw = 24 * s, kh = 20 * s;
        var cap = new Rect(r.center.x - kw / 2f, r.yMax - kh / 2f, kw, kh);
        float o = Mathf.Max(1f, Mathf.Round(1 * s));
        Box(new Rect(cap.x - o, cap.y - o, cap.width + 2 * o, cap.height + 2 * o + o), new Color(0.03f, 0.03f, 0.05f, 1f));
        Box(cap, new Color(0.16f, 0.16f, 0.21f, 1f));
        Box(new Rect(cap.x, cap.yMax - 3 * s, cap.width, 3 * s), new Color(0.09f, 0.09f, 0.13f, 1f));
        Outline(cap, Gold, o);
        Shadow(new Rect(cap.x, cap.y - 1 * s, cap.width, cap.height - 2 * s), key, keyCap, s, Color.white);
    }

    private float Badge(float x, float y, float size, Texture2D icon, string text, float s, Color accent)
    {
        var r = new Rect(x, y, size, size);
        Box(r, new Color(0.04f, 0.04f, 0.07f, 0.85f));
        Outline(r, accent, Mathf.Max(1f, Mathf.Round(1 * s)));
        if (icon != null) GUI.DrawTexture(new Rect(r.x + 1 * s, r.y + 1 * s, r.width - 2 * s, r.height - 2 * s), icon, ScaleMode.ScaleToFit);
        float w = size;
        if (!string.IsNullOrEmpty(text))
        {
            float tw = tiny.CalcSize(new GUIContent(text)).x;
            Shadow(new Rect(r.xMax + 3 * s, r.y, tw, size), text, tinyLeft, s, accent);
            w += tw + 3 * s;
        }
        return x + w + 6 * s;
    }

    private static string Secs(float t) => t >= 10f ? $"{t:0}s" : $"{t:0.0}s";

    // ---------------- Ô hành động ----------------

    private float DrawActionBar(float pad, float s)
    {
        float size = 52 * s, gap = 12 * s;
        float y = Screen.height - pad - size - 12 * s, x = pad;          // chừa chỗ cho phím bấm lồi dưới đáy ô
        bool silenced = status != null && status.Silenced;

        Slot(new Rect(x, y, size, size), icAttack, "J", "ATTACK", 0f, 1f, 0f, false, false, s); x += size + gap;
        Slot(new Rect(x, y, size, size), icRoll, "Q", "ROLL", kit.RollCooldownLeft, kit.rollCooldown, rollFlash, false, false, s); x += size + gap;
        Slot(new Rect(x, y, size, size), silenced ? icSilence : icSkill, "F", silenced ? "SEALED" : "SKILL",
             kit.SkillCooldownLeft, kit.skillCooldown, skillFlash, silenced, silenced, s);
        x += size;
        return x;
    }

    private void Slot(Rect r, Texture2D icon, string key, string name, float left, float total, float flash, bool dim, bool warn, float s)
    {
        if (slot != null) GUI.DrawTexture(r, slot);
        else Slice9(r, panel, PanelBorder, 0.42f * s, new Color(0.1f, 0.1f, 0.14f));
        var inner = new Rect(r.x + 9 * s, r.y + 8 * s, r.width - 18 * s, r.height - 18 * s);
        var old = GUI.color;
        if (icon != null)
        {
            GUI.color = dim ? new Color(0.45f, 0.45f, 0.45f, 1f) : Color.white;
            GUI.DrawTexture(inner, icon, ScaleMode.ScaleToFit);
        }
        if (left > 0f && total > 0f)
        {
            float f = Mathf.Clamp01(left / total);
            var cover = new Rect(inner.x - 2 * s, inner.y - 2 * s, inner.width + 4 * s, (inner.height + 4 * s) * f);
            GUI.color = new Color(0f, 0f, 0f, 0.68f); GUI.DrawTexture(cover, white);
            GUI.color = old;
            Shadow(inner, left >= 1f ? $"{left:0}" : $"{left:0.0}", bigNum, s, Bone);
        }
        if (flash > 0f)
        {
            GUI.color = new Color(Gold.r, Gold.g, Gold.b, 0.55f * flash);
            GUI.DrawTexture(new Rect(r.x + 4 * s, r.y + 4 * s, r.width - 8 * s, r.height - 8 * s), white);
        }
        GUI.color = old;
        // tên hành động phía trên ô
        Shadow(new Rect(r.x - 12 * s, r.y - 16 * s, r.width + 24 * s, 14 * s), name, tiny, s,
               warn ? new Color(1f, 0.45f, 0.45f) : new Color(0.85f, 0.82f, 0.72f));
        // PHÍM BẤM: nắp phím nổi ở giữa mép dưới ô (nền tối, viền vàng, chữ to sáng) -> nhìn là biết bấm nút nào
        KeyCap(r, key, s);
    }

    // ---------------- Boss ----------------

    private float DrawBoss(EnemyAI boss, float pad, float actionRight, float s)
    {
        float side = Mathf.Max(pad, actionRight + 16 * s);
        float w = Mathf.Min(600 * s, Screen.width - 2 * side);
        var bar = new Rect((Screen.width - w) / 2f, Screen.height - pad - 34 * s, w, 34 * s);
        if (boss.Health != lastBoss) { lastBoss = boss.Health; bossLag = boss.Health.Percent; }
        FrameBar(bar, barBig, BigCap, BigInset, boss.Health.Percent, bossLag, BossRed, s);
        Shadow(new Rect(bar.x, bar.y - 1 * s, bar.width, bar.height),
               $"{Mathf.CeilToInt(boss.Health.Current)} / {boss.Health.Max:0}", smallCenter, s, Bone);

        // tên: "Tên  —  phụ đề" → ruy băng + phụ đề bên phải
        string name = boss.displayName ?? "", sub = null;
        int cut = name.IndexOf('—');
        if (cut >= 0) { sub = name.Substring(cut + 1).Trim(); name = name.Substring(0, cut).Trim(); }
        float rh = 32 * s, cap = RibCap * rh / 30f;
        float tw = label.CalcSize(new GUIContent(name)).x;
        var rib = new Rect((Screen.width - tw) / 2f - cap - 6 * s, bar.y - rh + 4 * s, tw + 2 * cap + 12 * s, rh);
        Slice3(rib, ribBlood, RibCap, RibCap, new Color(0.35f, 0.05f, 0.1f));
        Shadow(new Rect(rib.x, rib.y - 2 * s, rib.width, rib.height), name, center, s, Bone);
        if (!string.IsNullOrEmpty(sub))
            Shadow(new Rect(bar.x, rib.y, bar.width - 8 * s, rh - 4 * s), sub, right, s, Gold);
        return rib.y;
    }

    // ---------------- Minimap + nhiệm vụ chính ----------------

    private void DrawGuide(float pad, float s)
    {
        var q = QuestTracker.Instance;
        if (q == null || !q.HasPlayer) return;
        Vector2 me = q.PlayerTile;
        bool inHidden = q.HiddenAt(me) != null;
        if (!inHidden && q.HasTarget && !q.BigMap) DrawWaypoint(q, me, s);
        float y = pad + 38 * s;
        if (q.Minimap != null)
        {
            float size = 170 * s;
            var M = new Rect(Screen.width - pad - size, y, size, size);
            DrawMinimap(q, M, me, inHidden, s);
            y = M.yMax + 8 * s;
        }
        // khung nhiệm vụ: cột phải, ngay dưới minimap, canh lề phải
        if (q.HasQuest) DrawQuestPanel(q, Screen.width - pad - 250 * s, y, 250 * s, me, inHidden, s);
        if (q.BigMap && q.Minimap != null) DrawBigMap(q, me, inHidden, s);
    }

    private void DrawMinimap(QuestTracker q, Rect M, Vector2 me, bool inHidden, float s)
    {
        float b = Mathf.Max(1f, Mathf.Round(2 * s));
        Box(new Rect(M.x - b, M.y - b, M.width + 2 * b, M.height + 2 * b), new Color(0.03f, 0.03f, 0.05f, 0.95f));
        Box(M, new Color(0.02f, 0.02f, 0.03f, 1f));
        if (!inHidden)
        {
            float k = M.width / Mathf.Max(8f, minimapTiles);          // px mỗi ô
            var map = q.MapTiles;
            GUI.BeginGroup(M);
            var c = new Vector2(M.width / 2f, M.height / 2f);
            GUI.DrawTexture(new Rect(c.x - me.x * k, c.y - me.y * k, map.x * k, map.y * k), q.Minimap);
            if (q.HasTarget)
            {
                Vector2 d = (q.TargetTile - me) * k;
                float edge = 8 * s;
                Vector2 tp = c + d;
                if (tp.x >= edge && tp.x <= M.width - edge && tp.y >= edge && tp.y <= M.height - edge)
                    Diamond(tp, 5 * s, s);
                else
                {
                    float t = Mathf.Min((c.x - edge) / Mathf.Max(0.001f, Mathf.Abs(d.x)), (c.y - edge) / Mathf.Max(0.001f, Mathf.Abs(d.y)));
                    Pointer(c + d * t, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, 12 * s, Color.white);
                }
            }
            Vector2 f = kit != null ? kit.Facing : Vector2.down;
            Pointer(c, Mathf.Atan2(-f.y, f.x) * Mathf.Rad2Deg, 13 * s, new Color(0.75f, 0.95f, 1f));
            GUI.EndGroup();
        }
        else Shadow(M, "???", center, s, new Color(0.55f, 0.55f, 0.65f));
        Outline(M, Gold, Mathf.Max(1f, Mathf.Round(1 * s)));
        Shadow(new Rect(M.x, M.yMax - 16 * s, M.width - 5 * s, 14 * s), "M", keyStyle, s, Gold);
    }

    private void DrawQuestPanel(QuestTracker q, float left, float y, float w, Vector2 me, bool inHidden, float s)
    {
        float padIn = 12 * s;
        string title = q.AllDone ? "The way onward is open." : q.Title;
        if (q.TotalCount > 0) title += $"  ({q.DoneCount}/{q.TotalCount})";
        float th = questTitle.CalcHeight(new GUIContent(title), w - 2 * padIn);
        string sub = "";
        if (inHidden) sub = "Find a way back up.";
        else if (!q.AllDone && q.HasTarget)
            sub = $"> {q.Label}   {Vector2.Distance(me, q.TargetTile):0}m";
        float headH = 16 * s, subH = sub.Length > 0 ? 18 * s : 0f;
        var r = new Rect(left, y, w, padIn + headH + th + subH + padIn * 0.8f);
        Slice9(r, panel, PanelBorder, 0.5f * s, new Color(0.1f, 0.1f, 0.14f));
        float since = Time.time - q.StepChangedAt;
        bool fresh = since < 3f;
        if (fresh)
        {
            float a = 0.35f * (1f - since / 3f) * (0.6f + 0.4f * Mathf.Sin(since * 10f));
            Box(new Rect(r.x + 4 * s, r.y + 4 * s, r.width - 8 * s, r.height - 8 * s), new Color(Gold.r, Gold.g, Gold.b, a));
        }
        Shadow(new Rect(r.x + padIn, r.y + padIn - 2 * s, w - 2 * padIn, headH), fresh ? "QUEST UPDATED" : "MAIN QUEST", questHead, s, Gold);
        Shadow(new Rect(r.x + padIn, r.y + padIn + headH, w - 2 * padIn, th), title, questTitle, s, Bone);
        if (subH > 0f)
            Shadow(new Rect(r.x + padIn, r.y + padIn + headH + th, w - 2 * padIn, subH), sub, questHead, s,
                   inHidden ? new Color(0.7f, 0.7f, 0.8f) : new Color(0.85f, 0.82f, 0.7f));
    }

    /// Đích nhiệm vụ trên màn hình: mũi tên nhấp nhô trên đích; ngoài màn hình thì mũi tên vàng ở mép + số mét.
    private void DrawWaypoint(QuestTracker q, Vector2 me, float s)
    {
        var cam = Camera.main;
        if (cam == null) return;
        float dist = Vector2.Distance(me, q.TargetTile);
        if (dist < 2f) return;
        Vector3 sp = cam.WorldToScreenPoint(q.TargetWorld);
        var g = new Vector2(sp.x, Screen.height - sp.y);
        float m = 48 * s;
        bool on = sp.z > 0f && g.x >= m && g.x <= Screen.width - m && g.y >= m && g.y <= Screen.height - m;
        if (on)
        {
            var tex = wp[Mathf.FloorToInt(Time.time * 6f) % wp.Length];
            float sz = 30 * s;
            if (tex != null) GUI.DrawTexture(new Rect(g.x - sz / 2f, g.y - sz - 18 * s, sz, sz), tex);
            else Diamond(new Vector2(g.x, g.y - 24 * s), 7 * s, s);
            return;
        }
        var c = new Vector2(Screen.width / 2f, Screen.height / 2f);
        Vector2 d = g - c;
        if (sp.z < 0f) d = -d;
        float t = Mathf.Min((c.x - m) / Mathf.Max(0.001f, Mathf.Abs(d.x)), (c.y - m) / Mathf.Max(0.001f, Mathf.Abs(d.y)));
        Vector2 e = c + d * t;
        float pulse = 1f + 0.12f * Mathf.Sin(Time.time * 5f);
        Pointer(e, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, 30 * s * pulse, Color.white);
        Vector2 inward = (c - e).normalized * 26 * s;
        Shadow(new Rect(e.x + inward.x - 40 * s, e.y + inward.y - 9 * s, 80 * s, 18 * s), $"{dist:0}m", tiny, s, Gold);
    }

    private void DrawBigMap(QuestTracker q, Vector2 me, bool inHidden, float s)
    {
        Box(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.72f));
        var v = q.ViewTiles;
        float k = Mathf.Min(Screen.width * 0.82f / v.width, Screen.height * 0.78f / v.height);
        var R = new Rect((Screen.width - v.width * k) / 2f, (Screen.height - v.height * k) / 2f + 6 * s, v.width * k, v.height * k);
        float b = Mathf.Max(2f, Mathf.Round(3 * s));
        Box(new Rect(R.x - b, R.y - b, R.width + 2 * b, R.height + 2 * b), new Color(0.03f, 0.03f, 0.05f, 1f));
        float tw = q.Minimap.width, thh = q.Minimap.height, ppt = q.Ppt;
        GUI.DrawTextureWithTexCoords(R, q.Minimap,
            new Rect(v.x * ppt / tw, 1f - (v.y + v.height) * ppt / thh, v.width * ppt / tw, v.height * ppt / thh));
        Outline(R, Gold, Mathf.Max(1f, Mathf.Round(2 * s)));
        if (q.HasTarget && !inHidden)
        {
            Vector2 tp = R.position + (q.TargetTile - v.position) * k;
            if (R.Contains(tp)) Diamond(tp, 7 * s, s);
        }
        if (!inHidden)
        {
            Vector2 pp = R.position + (me - v.position) * k;
            Vector2 f = kit != null ? kit.Facing : Vector2.down;
            if (R.Contains(pp)) Pointer(pp, Mathf.Atan2(-f.y, f.x) * Mathf.Rad2Deg, 18 * s, new Color(0.75f, 0.95f, 1f));
        }
        var dir = MapEncounterDirector.Instance;
        string head = dir != null && !string.IsNullOrEmpty(dir.CurrentZoneTitle) && !inHidden ? dir.CurrentZoneTitle : "Unknown depths";
        Shadow(new Rect(R.x, R.y - 30 * s, R.width, 26 * s), head, center, s, Bone);
        Shadow(new Rect(R.x, R.yMax + 6 * s, R.width, 18 * s), "Press M to close", tiny, s, Gold);
    }

    /// Mũi tên vàng (ảnh hud_pointer chỉ sang phải) xoay `angle` độ (theo chiều kim đồng hồ trên màn hình).
    private void Pointer(Vector2 at, float angle, float size, Color tint)
    {
        var m = GUI.matrix;
        var old = GUI.color;
        GUIUtility.RotateAroundPivot(angle, at);
        GUI.color = new Color(tint.r * old.r, tint.g * old.g, tint.b * old.b, tint.a * old.a);
        if (pointer != null) GUI.DrawTexture(new Rect(at.x - size / 2f, at.y - size / 2f, size, size), pointer);
        else GUI.DrawTexture(new Rect(at.x - size / 2f, at.y - size / 6f, size, size / 3f), white);
        GUI.color = old;
        GUI.matrix = m;
    }

    /// Hình thoi vàng viền tối (đích nhiệm vụ trên minimap).
    private void Diamond(Vector2 at, float r, float s)
    {
        var m = GUI.matrix;
        GUIUtility.RotateAroundPivot(45f, at);
        float o = Mathf.Max(1f, Mathf.Round(1.5f * s));
        Box(new Rect(at.x - r - o, at.y - r - o, 2 * (r + o), 2 * (r + o)), new Color(0.09f, 0.11f, 0.18f, 1f));
        float p = 0.75f + 0.25f * Mathf.Sin(Time.time * 6f);
        Box(new Rect(at.x - r, at.y - r, 2 * r, 2 * r), new Color(Gold.r, Gold.g * p + (1 - p) * 0.5f, Gold.b, 1f));
        GUI.matrix = m;
    }

    // ---------------- Quái thường ----------------

    private void DrawEnemyBars(float s)
    {
        var cam = Camera.main;
        if (cam == null) return;
        float w = 50 * s, h = Mathf.Max(6f, 10 * s);
        foreach (var e in EnemyHealthBar.All)
        {
            if (e == null || e.Health == null || e.Health.IsDead || !e.isActiveAndEnabled) continue;
            Vector3 sp = cam.WorldToScreenPoint(e.HeadWorld);
            if (sp.z < 0f || sp.x < -w || sp.x > Screen.width + w || sp.y < -20 || sp.y > Screen.height + 40) continue;
            var r = new Rect(Mathf.Round(sp.x - w / 2f), Mathf.Round(Screen.height - sp.y - h), w, h);
            FrameBar(r, barSmall, SmallCap, SmallInset, e.Health.Percent, e.Health.Percent, MobRed, s);
            if (!enemyHpNumbers) continue;
            float th = tiny.fontSize * 1.3f;
            Shadow(new Rect(r.x - 20 * s, r.y - th, w + 40 * s, th),
                   $"{Mathf.CeilToInt(e.Health.Current)}/{e.Health.Max:0}", tiny, s, Bone);
        }
    }

    // ---------------- Cập nhật hiệu ứng ----------------

    private void Tick()
    {
        float dt = Time.unscaledDeltaTime;
        if (player != null) Lag(ref playerLag, ref playerLagHold, player.Percent, dt);
        var dir = MapEncounterDirector.Instance;
        if (dir != null && dir.Boss != null && dir.Boss.Health != null) Lag(ref bossLag, ref bossLagHold, dir.Boss.Health.Percent, dt);
        if (kit != null)
        {
            float r = kit.RollCooldownLeft, k = kit.SkillCooldownLeft;
            if (rollWasLeft > 0f && r <= 0f) rollFlash = 1f;
            if (skillWasLeft > 0f && k <= 0f) skillFlash = 1f;
            rollWasLeft = r; skillWasLeft = k;
            if (lastPotions >= 0 && kit.Potions < lastPotions) flaskFlash = 1f;
            lastPotions = kit.Potions;
        }
        flaskFlash = Mathf.Max(0f, flaskFlash - dt * 2.5f);
        rollFlash = Mathf.Max(0f, rollFlash - dt * 4f);
        skillFlash = Mathf.Max(0f, skillFlash - dt * 3f);
    }

    /// Vệt máu vừa mất: giữ 0,45 s rồi trượt xuống; hồi máu thì theo ngay.
    private static void Lag(ref float lag, ref float hold, float pct, float dt)
    {
        if (pct >= lag) { lag = pct; hold = 0.45f; return; }
        if (hold > 0f) { hold -= dt; return; }
        lag = Mathf.MoveTowards(lag, pct, dt * 0.6f);
    }

    // ---------------- Vẽ ----------------

    /// Khung 3 lát (đầu trái, giữa kéo giãn, đầu phải) + phần lấp đầy trong rãnh.
    private void FrameBar(Rect r, Texture2D frame, float cap, float[] inset, float pct, float lag, Color fill, float s)
    {
        float k = frame != null ? r.height / frame.height : 1f;
        Rect inner = frame != null
            ? new Rect(r.x + inset[0] * k, r.y + inset[2] * k, r.width - (inset[0] + inset[1]) * k, r.height - (inset[2] + inset[3]) * k)
            : new Rect(r.x + 2 * s, r.y + 2 * s, r.width - 4 * s, r.height - 4 * s);
        Slice3(r, frame, cap, cap, new Color(0.05f, 0.05f, 0.08f, 0.9f));
        if (frame == null) Box(inner, new Color(0.16f, 0.08f, 0.1f));
        var tex = barFill != null ? barFill : white;
        var old = GUI.color;
        if (lag > pct)
        {
            GUI.color = LagColor;
            GUI.DrawTexture(new Rect(inner.x, inner.y, inner.width * Mathf.Clamp01(lag), inner.height), tex);
        }
        GUI.color = fill;
        GUI.DrawTexture(new Rect(inner.x, inner.y, inner.width * Mathf.Clamp01(pct), inner.height), tex);
        GUI.color = old;
    }

    /// Ảnh 3 lát theo chiều ngang; đầu giữ đúng tỉ lệ theo chiều cao rect.
    private void Slice3(Rect r, Texture2D t, float l, float rr, Color fallback)
    {
        if (t == null) { Box(r, fallback); return; }
        float k = r.height / t.height;
        float L = l * k, R = rr * k, mid = r.width - L - R;
        if (mid < 0f) { L = R = r.width / 2f; mid = 0f; }
        Part(new Rect(r.x, r.y, L, r.height), t, 0, 0, l, t.height);
        if (mid > 0f) Part(new Rect(r.x + L, r.y, mid, r.height), t, l, 0, t.width - l - rr, t.height);
        Part(new Rect(r.x + L + mid, r.y, R, r.height), t, t.width - rr, 0, rr, t.height);
    }

    /// Ảnh 9 lát; b = {trái, trên, phải, dưới} theo px ảnh, k = tỉ lệ vẽ viền.
    private void Slice9(Rect r, Texture2D t, float[] b, float k, Color fallback)
    {
        if (t == null) { Box(r, fallback); return; }
        float l = b[0] * k, tp = b[1] * k, rr = b[2] * k, bt = b[3] * k;
        float sx = Mathf.Min(1f, r.width / (l + rr)), sy = Mathf.Min(1f, r.height / (tp + bt));
        l *= sx; rr *= sx; tp *= sy; bt *= sy;
        float mw = r.width - l - rr, mh = r.height - tp - bt;
        float sw = t.width - b[0] - b[2], sh = t.height - b[1] - b[3];
        float[] dx = { r.x, r.x + l, r.x + l + mw }, dw = { l, mw, rr };
        float[] dy = { r.y, r.y + tp, r.y + tp + mh }, dh = { tp, mh, bt };
        float[] ux = { 0, b[0], t.width - b[2] }, uw = { b[0], sw, b[2] };
        float[] uy = { 0, b[1], t.height - b[3] }, uh = { b[1], sh, b[3] };
        for (int j = 0; j < 3; j++)
            for (int i = 0; i < 3; i++)
                if (dw[i] > 0f && dh[j] > 0f) Part(new Rect(dx[i], dy[j], dw[i], dh[j]), t, ux[i], uy[j], uw[i], uh[j]);
    }

    /// Vẽ một vùng ảnh (toạ độ px, gốc trên trái) vào rect màn hình.
    private static void Part(Rect dst, Texture2D t, float sx, float sy, float sw, float sh)
    {
        GUI.DrawTextureWithTexCoords(dst, t, new Rect(sx / t.width, 1f - (sy + sh) / t.height, sw / t.width, sh / t.height));
    }

    private void Box(Rect r, Color c)
    {
        var old = GUI.color; GUI.color = new Color(c.r * old.r, c.g * old.g, c.b * old.b, c.a * old.a);
        GUI.DrawTexture(r, white); GUI.color = old;
    }

    private void Outline(Rect r, Color c, float t)
    {
        Box(new Rect(r.x, r.y, r.width, t), c); Box(new Rect(r.x, r.yMax - t, r.width, t), c);
        Box(new Rect(r.x, r.y, t, r.height), c); Box(new Rect(r.xMax - t, r.y, t, r.height), c);
    }

    /// Chữ viền đen 4 phía (đọc rõ trên mọi nền), dùng cho số đếm.
    private static void OutlinedLabel(Rect r, string text, GUIStyle style, float s, Color color)
    {
        float o = Mathf.Max(1f, Mathf.Round(1.5f * s));
        var c = style.normal.textColor;
        style.normal.textColor = new Color(0.02f, 0.02f, 0.04f, 1f);
        GUI.Label(new Rect(r.x - o, r.y, r.width, r.height), text, style);
        GUI.Label(new Rect(r.x + o, r.y, r.width, r.height), text, style);
        GUI.Label(new Rect(r.x, r.y - o, r.width, r.height), text, style);
        GUI.Label(new Rect(r.x, r.y + o, r.width, r.height), text, style);
        GUI.Label(new Rect(r.x + o, r.y + o, r.width, r.height), text, style);
        style.normal.textColor = color;
        GUI.Label(r, text, style);
        style.normal.textColor = c;
    }

    private static void Shadow(Rect r, string text, GUIStyle style, float s, Color color)
    {
        if (style == null) style = GUI.skin.label;
        float o = Mathf.Max(1f, Mathf.Round(1.5f * s));
        var c = style.normal.textColor;
        style.normal.textColor = new Color(0, 0, 0, 0.85f);
        GUI.Label(new Rect(r.x + o, r.y + o, r.width, r.height), text, style);
        style.normal.textColor = color;
        GUI.Label(r, text, style);
        style.normal.textColor = c;
    }
}
