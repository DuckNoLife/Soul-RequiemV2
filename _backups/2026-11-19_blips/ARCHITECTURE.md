# Soul Requiem — Kiến trúc code

> Đọc file này trước khi sửa code. Quy tắc làm việc chung: `SOUL_REQUIEM_HANDOFF.md` mục 2.
> Cập nhật lần cuối: 04/11/2026 23:30 (2.6 Đấu trường boss, 2.7 Cảnh diễn, 2.8 Mang theo qua map / lưu game, 2.9 Túi đồ – chìa – rương – cửa khoá).

## 1. Các assembly (asmdef) và namespace

Mỗi thư mục lớn là **một assembly riêng**, có **namespace trùng tên**. Unity chỉ biên dịch lại assembly bị sửa
và những assembly phụ thuộc vào nó.

| Assembly / namespace | Thư mục | Chứa gì | Được dùng |
|---|---|---|---|
| `SoulRequiem.Core` | `Assets/Scripts/Core/` | Nền tảng không biết gì về gameplay: phím (`GameInput`), cài đặt (`GameSettings`), đọc thuộc tính Tiled (`TiledProps`), xử lý chữ (`TextUtil`), dòng gợi ý giữa màn hình (`Hints`), ánh sáng VFX (`FxLighting`) | (không gì của dự án) |
| `SoulRequiem.Gameplay` | `Assets/Scripts/Gameplay/` | Toàn bộ luật chơi | Core |
| `SoulRequiem.Visual` | `Assets/Scripts/Visual/` | Hiệu ứng nhìn thuần tuý: bóng đổ, chiều sâu, đèn quanh người chơi | Core, Gameplay |
| `SoulRequiem.UI` | `Assets/Scripts/UI/` | Giao diện UI Toolkit: HUD, menu, màn hình tiêu đề | Core, Gameplay |
| `SoulRequiem.DevTools` | `Assets/Scripts/DevTools/` | Phím test F1–F9 (F1 = bảng phím; Shift/Ctrl = biến thể), ghi log chẩn đoán. **Chỉ có trong Editor / Development Build** (`#if`) | tất cả |
| `SoulRequiem.EditorTools` (asm `SoulRequiem.Editor`) | `Assets/Editor/` | Menu "Soul Requiem ▸ …" dựng scene, quái, animator | tất cả |

### Luật phụ thuộc (asmdef bắt buộc — vi phạm là lỗi biên dịch)

```
DevTools ─┐
Editor ───┼──► UI ──────┐
          ├──► Visual ──┼──► Gameplay ──► Core ──► (Unity, Input System, URP, SuperTiled2Unity)
          └─────────────┘
```

- **Gameplay không bao giờ gọi UI hay Visual.** Gameplay cần báo cho UI → dùng event/trạng thái ở Core hoặc Gameplay,
  UI tự đọc (vd menu mở: `RequiemMenus` đặt `GameInput.Blocked`, gameplay chỉ đọc `GameInput.Blocked`).
- **Core không biết gameplay.** Thứ gì cần `Health`, `PlayerController`… thì thuộc Gameplay.
- Code dùng chung giữa nhiều assembly mà không phụ thuộc gameplay → đưa xuống Core.

## 2. Gameplay — bản đồ thư mục

| Thư mục | Nội dung |
|---|---|
| `Player/` | `PlayerController` (di chuyển, đọc `GameInput.Move`), `PlayerCombat` (thông số Inspector + nhận phím), `PlayerStatus` (độc/chậm/câm…) |
| `Player/Abilities/` | Tính năng chiến đấu, mỗi thứ một class: `PlayerMelee` (chém), `PlayerDodge` (lăn), `PlayerFlask` (bình máu), `PlayerStrikes` (sát thương, dừng hình, giật camera), `PlayerHurtReactions` (trúng đòn/choáng/gục), `PlayerAnimation` (tham số Animator), `PlayerActionState` (huỷ/khoá hành động) |
| `Player/Skills/` | Kỹ năng tháo lắp: `SkillDefinition` (ScriptableObject, `Resources/Skills/*.asset`), `PlayerLoadout` (3 ô), `GroundSlamSkill` |
| `Combat/` | `Health` (máu + event `Damaged/Died/Hit/Revived`), đạn (`Projectile` — có `Live`, `Reflect`), `TurnedBlade` (đỡ đạn), vùng độc, sóng chấn, đòn đập đất, `SpriteVfx` |
| `Enemies/` | `EnemyAI`, `IEnemySkills` (`MonsterSkills.cs`), kỹ năng quái thường, thanh máu, VFX quái, hang sinh quái |
| `Enemies/Bosses/` | Mỗi boss một file `<Tên>Skills.cs` (bộ chiêu riêng, `IEnemySkills`); `BossSkillsBase` = nền cho boss đấu trường (vùng đấu, strafe, hồi chiêu theo tên) |
| `Enemies/Data/` | `EnemyDefinition` (ScriptableObject) — **chỉ số quái**, xem mục 2.4 |
| `Enemies/Navigation/` | `EnemySteering` — quái né vật cản (quét 16 hướng), tách đàn, phát hiện kẹt. Tầm nhìn: `Combat/LineOfSight` |
| `Enemies/Toolkit/` | Dùng chung cho quái & boss: `BulletPatterns` (vòng / quạt đạn). Cùng loại: `EnemyAI.SortingLayer/SortingOrder/MatchSorting`, `CameraFollow.KickMain` |
| `World/Encounters/` | Trận đánh theo khu — xem mục 2.1, đấu trường boss mục 2.6 |
| `World/Mechanics/` | Cơ quan & câu đố — xem mục 2.2 |
| `World/Cutscene/` | Cảnh diễn & màn hướng dẫn — xem mục 2.7 |
| `World/` | `InteractionSystem` (phím T), `QuestTracker` (nhiệm vụ + minimap; đích có `check` -> danh sách con trên HUD), ánh sáng/không khí khu, `LightCuller` (tắt đèn map ngoài khung camera, 05/11) |
| `World/MapFog` | 05/11: sương mù bản đồ theo khu (dữ liệu; `MapEncounterDirector.Fog`). Thuộc tính khu `fog` = secret / off; cờ `map_seen_<khu>`. UI vẽ: `UI/MinimapFogView` |
| `Flow/` | Chuyển map (`SceneTransition`, `GameSession`), lưu game (`SaveGame`), lối ra, thành tựu |
| `Camera/` | `CameraFollow` |

### 2.1 World/Encounters — trận đánh theo khu

`MapEncounterDirector` (component duy nhất trong scene, giữ nguyên các field Inspector) chỉ điều phối **luồng khu / đợt quái và hồi sinh**.
Phần việc khác là các class C# thường, director tạo trong `Start()`:

| Class | Việc |
|---|---|
| `MapSchema` | **Hợp đồng với Tiled**: tên layer (`zones/spawns/gates/triggers`), loại object, tên thuộc tính. Đổi tên ở Tiled → đổi ở đây |
| `MapLayout` | Đọc map Tiled thành dữ liệu: `EncounterZone`, `SpawnPoint`, `MapGate`, `MapTrigger` |
| `WorldFlags` | Cờ trạng thái (`Has`, `HasAll("a,b")`, `Set`, event `Changed`). Truy cập: `MapEncounterDirector.Instance.Flags` |
| `GateController` | Đóng/mở cổng: khoá đấu trường, `unlock_when`, mở khi dọn khu |
| `EnemySpawner` | Tạo quái từ tên theo bảng prefab, đặt hang sinh quái |
| `MapTriggerHandler` | Trigger: điểm nghỉ (17/11 §285: KHÔNG còn dấu; checkpoint không có lửa / nến thật = điểm hồi sinh ẩn, không ô Rest; `ShowCheckpointMarkers = false`), bia/biển (đăng ký phím T), vật nhặt (`marker` = đốm lấp lánh, §286 không còn vòng vàng), dịch chuyển, lối ra |
| `MapEncounterDirector.AutoRespawn.cs` | 17/11 (§285): vào một khu (khu nhỏ nhất chứa Ren) -> ghi điểm hồi sinh + lưu game (trừ khu đấu trường có cổng khoá, khu đang bị vây). Gục -> ải chưa xong ưu tiên đầu ải (`ChooseStageRespawn` trả bool), còn lại tỉnh ở điểm đã ghi GẦN CHỖ GỤC NHẤT |
| `ArenaSeal` | Đấu trường boss — mục 2.6 |
| `MapEncounterDirector.DevTools.cs` | `partial` chứa lệnh test F2–F4, chỉ biên dịch trong Editor / Development Build |

**Mạch chính theo khu — BẮT BUỘC cho mọi map (07/11, NOTES §161):**
- Thuộc tính khu `story` = số bước mạch chính (vd `1.1`; khu dùng lại ở chương sau ghi nhiều số `2.2;5`), `story_done` = cờ coi như xong
  của từng bước (ngăn `;` theo từng số, trong mỗi bước ngăn `,`; tính cả cờ chìa `key_<id>`). Khu phụ / bí mật: `optional = true`.
  Khu phục kích (chồng lên phòng) không đánh dấu. Đọc vào `EncounterZone.story / storyDone / optional` (MapLayout).
- **Chặn đi tắt:** dải `turn_back` (lớp triggers, mỏng ≤ 0,7 đv, `requires`, `say`) ở cửa nhánh chưa tới lượt theo truyện.
- **Điều kiện cờ** (`requires`, `start_when`, `unlock_when`… mọi chỗ dùng `WorldFlags.HasAll`): `,` = VÀ, `|` = HOẶC (vd `note_read|vat_key`
  — để bản lưu cũ đã đi tắt không bị kẹt sau dải chặn mới).
- **F4 (test):** nhảy theo bước `story` (Shift lùi), tới nơi bật cờ `story_done` của mọi bước trước + mở cổng các khu đó. Map chưa đánh dấu
  -> nhảy khu theo thứ tự tạo trong Tiled (bỏ khu `optional`).

Giao tiếp giữa các hệ thống:
- **Hiện chữ cho người chơi:** `Hints.Show("…", giây)` — không gọi HUD. Chữ dài tự kéo dài thời gian (≥ 16 ký tự/giây).
- **Báo "đã xong việc gì":** `Flags.Set("ten_co")` — cổng, câu đố, nhiệm vụ tự phản ứng theo cờ.

### 2.2 World/Mechanics — cơ quan & câu đố

`MapMechanics` (component trong scene) chỉ **đọc map và điều phối**; mỗi loại cơ quan (Type của object trong layer
`mechanics` ở Tiled) là **một class** kế thừa `Mechanism`:

| Thư mục | Loại (Tiled Type → class) |
|---|---|
| `Hazards/` | `spout`, `vent`, `hazard` (`start_when` / `stop_when`), `flood` (nền chung `HazardMechanismBase`: dừng khi `stop_when`) |
| `Effects/` | `beam`, `shield` (nền `FxMechanism`: tắt khi `off_when`), `light`, `toggle`, `tank` (vỡ → cờ `<tên>_broken`, vũng `spill` loang ra), **`sprite_prop`** (vật có ảnh Resources + chân chặn + vòng ấn trên sàn + đèn; code bật bằng `SetLit`, tra theo `SpritePropMechanism.Group(group)`) |
| `Puzzles/PassageMechanism` | **`passage`** (05/11): lối tắt / thang / nắp hầm trong cùng map — `to`, `requires`, `sets`, `locked_say`, `sprite`; dùng `SceneTransition.Warp`. Lối tắt một chiều = 2 object: đầu trong `sets` cờ, đầu ngoài `requires` cờ đó |
| `Puzzles/` | `breakable`, `lever`, `dial`, `valve`, `bell`, `console` (`marker_item`/`marker_off`) + `pipegrid` (logic thuần `PipeNetwork`), `note` (+`NoteMarker`, `MarkerPulse`), `cipher`, `rosewindow`, **`sluice`** (06/11: van xả nước — `lane_a`/`lane_b`, `flow_seconds`, `cooldown`; quái hỏi `SluiceMechanism.Wet`) |
| `Panels/` | `MechanismPanels` + model `NotePanel` / `CipherPanel` / `RoseWindowPanel` — dữ liệu bảng giữa màn hình |

- `MechanismRegistry`: bảng Type → class. **Type lạ → cảnh báo trong Console** (bắt lỗi gõ sai trong Tiled).
- `MechanismContext`: mọi thứ cơ quan cần (cờ, người chơi, object theo tên, đèn, ảnh ký tự/tranh/ống, toạ độ ô, coroutine).
- Vòng đời một cơ quan: `Init` → `OnFlagsChanged` → mỗi khung `Animate` + `Tick` (khi chưa `Done`) → `OnStruck` (chém) / `Use` (phím T).
- Bảng ghi chú / khoá / cửa sổ hoa hồng: gameplay chỉ giữ **dữ liệu** (`MechanismPanels.Current`);
  `UI/MechanismPanelView` vẽ bằng UI Toolkit (khung đá giống menu) và gọi ngược `Turn` / `Rotate`.
- **Công tắc / đồ giải đố phải nhìn ra được**: vật nhỏ (cần gạt, tay quay, chìa khoá) luôn có `NoteMarker`
  (console `marker_item`, pickup `marker = true`) hoặc đèn nhấp nháy. Vùng nhặt (trigger) phải trùm đúng lên hình món đồ.
  17/11 (§286, người dùng): `NoteMarker.ShowRing = false` — KHÔNG vẽ vòng vàng dưới chân; chỉ đốm lấp lánh + Light2D nhẹ. Đừng bật lại.
- **Bẫy cơ khí (§284)**: gai / trụ lăn / lưỡi cưa / phụt nước gọi `TrapArt.HurtPlayerCrush` (đặt `Health.TrapIgnoresRoll`) -> lăn KHÔNG né được; mũi tên máy bắn vẫn né được.
- **Khiên quái (§281/§284)**: `Health.BlockCheck` (Func<Vector2,bool>) + event `Blocked`, `Health.LastBlockedAmount` (sát thương vừa bị chặn). Dùng: `LastGuardSkills` (Map 2b).

**Thêm một loại cơ quan mới:** tạo `Puzzles/XyzMechanism.cs : Mechanism` (ghi chú `<summary>` liệt kê thuộc tính Tiled),
override những hàm cần, rồi thêm một dòng `{ "xyz", () => new XyzMechanism() }` vào `MechanismRegistry`. Không sửa `MapMechanics`.

**Hình của cơ quan trong Tiled — MẶC ĐỊNH TỪ 06/11 (NOTES §154):** mọi cơ quan có hình (`sprite_prop`, `note` có tờ giấy,
`passage`, mảnh hình của `breakable`) đặt bằng **tile object** trong lớp **`mechanics_art`** (opacity 1, nhìn thấy), kéo từ tileset
**`SR-mech-art.tsx`** của map — KHÔNG dùng hình chữ nhật trống + thuộc tính `sprite` nữa (Tiled chỉ vẽ khung rỗng, không chỉnh tay được).
- `MapMechanics.ReadMap` đọc lớp `mechanics` và `mechanics_art` như nhau; tile object -> `MechanismArt` (khung = khung tile object,
  ảnh SuperTiled2Unity dựng sẵn bị tắt, cơ quan tự vẽ). Danh sách hình: `Mechanism.Art`.
- **Tile trong tileset ghi sẵn** class (vd `sprite_prop`) + `sprite` (đường dẫn Resources) + mặc định `sprite_lit` / `collider` / `flat`;
  object kế thừa, chỉ ghi thứ khác mặc định. Đổi tile = đổi hình trong game. Lúc chạy ảnh vẫn nạp từ Resources (giữ pivot / PPU / ảnh trạng thái).
- Hình vẽ **khít khung tile object**: kéo to / lật ngang trong Tiled = to / lật trong game (collider + đèn to theo). Chân vật = pivot ảnh.
- Cơ quan có **vùng logic khác hình** (breakable: vùng chém to, nhiều mảnh): vùng = hình chữ nhật trong `mechanics`; mỗi mảnh hình =
  tile object trong `mechanics_art` có **`art_for` = tên cơ quan**. Ảnh trạng thái (`sprite_damaged`, `debris`) vẫn ghi trên vùng.
- Ảnh PPU ≠ 100: tileset dùng bản xem trước đã thu đúng tỉ lệ (`Props/SR-mech-art/`, chỉ cho Tiled) để thấy đúng cỡ.
- **Thêm ảnh mới**: PNG vào `Resources/VFX/...` (PNG trước, .meta sau) rồi thêm một `<tile>` vào CUỐI `SR-mech-art.tsx`
  (không xê dịch id). Map mới: tạo `SR-mech-art.tsx` riêng cho map đó theo mẫu `Catalog/v2/map2_mechArt.py`.


**Khu phục kích theo cốt truyện (05/11):** muốn một trận chỉ xảy ra sau sự kiện (vd sau khi hạ boss / thấy con Mắt) mà phòng đó đã là
khu của nhiệm vụ -> vẽ **khu riêng** chồng lên một phần phòng, `start_when = <cờ>`, spawn `zone_id = <khu riêng>`, KHÔNG đặt `ambient`
(giữ ánh nền phòng). Không gắn start_when vào khu phòng (cờ `<phòng>_started` thường là điều kiện nhiệm vụ). Chừa lửa trại ra ngoài khu.

### 2.3 Nhập liệu

- **Mọi phím / nút đọc qua `Core/GameInput`**: `Pressed(GameAction.X)`, `Held(...)`, `Move` (W A S D / mũi tên / cần trái / D-pad).
  Không viết `Keyboard.current.jKey` trong gameplay (ngoại lệ có lý do: chuột trái để chém, phím trong bảng điều khiển ống, Space/Enter tua cảnh).
- Tay cầm: X chém, B lăn, Y bình máu, A tương tác, RB/LB/RT kỹ năng I/II/III, Select bản đồ, Start tạm dừng.
- `GameInput.Blocked = true` khi menu mở / cảnh diễn → mọi hành động (trừ Pause) và di chuyển trả về "không bấm".

### 2.4 Chỉ số quái — EnemyDefinition

- Mỗi loại quái có asset `Assets/Enemies/<MapN>/Definitions/<Tên>.asset` (máu, khối lượng, tốc độ, sát thương, tầm, đòn đặc biệt...).
- Prefab quái trỏ tới asset qua `EnemyAI.definition`; khi quái sinh ra, **asset ghi đè chỉ số trong prefab** → cân bằng game
  chỉ cần sửa asset trong Inspector, không sửa code, không cần dựng lại quái.
- Hình / hoạt ảnh / collider / thông số riêng của bộ chiêu boss (`*Skills`) vẫn ở prefab (do "Dựng quái Map N" tạo).
- "Dựng quái Map N": asset có sẵn thì asset thắng giá trị trong code builder; chưa có thì tạo từ code.
- Quái mới: thêm `Def` trong builder → chạy "Dựng quái Map N" (tự tạo asset) → chỉnh asset.

### 2.5 Tầm nhìn & né vật cản (31/10)

- `LineOfSight`: vật cản = collider không phải trigger, không thuộc nhân vật (có `Health`) hay đạn. **Nước chặn đi, không chặn tầm nhìn.**
- Đòn của người chơi (`PlayerStrikes.Damage`) và đòn cận chiến của quái (`EnemyAI.HitPlayer`) **không xuyên vật cản**.
  (Chiêu riêng của boss gọi `EnemyAI.Hurt` — chưa kiểm tra tầm nhìn, vì đấu trường boss trống.)
- Quái phải **thấy** người chơi mới gây hấn (trừ khi đứng rất sát hoặc bị đánh trúng).
- Quái đuổi bằng `EnemySteering` (đường thẳng thông thì đi thẳng, bị chắn thì vòng). Bị kẹt 0,5 s → đổi phía bám mép.
- Quái cận chiến thường bị **trap** (vừa bị đánh + 2 s không lại gần được) → rút về chỗ cũ, hồi máu, 4 s không gây hấn lại trừ khi người chơi áp sát.
  Boss, quái bay, quái tầm xa, quái `Custom` không dùng cơ chế này.

### 2.6 Đấu trường boss — QUY TẮC BẮT BUỘC (04/11)

> Lý do: chạy thử Map 2, Ether Ram lao khỏi Boiler Room sang tận Overseer's Office — khu boss không có cổng khoá và AI không bị giới hạn phạm vi.

`World/Encounters/ArenaSeal.cs` (director tự gắn) áp dụng cho **mọi** spawn có `boss = true`, mọi map:

1. **Xích boss:** mỗi FixedUpdate boss bị kẹp trong khung **khu (zone) chứa nó** (cách mép 0,3 đv) — đúng với mọi kiểu di chuyển
   (đi, lao, nhảy, bị đẩy, `TeleportTo`). Không cần sửa bộ chiêu boss.
2. **Màn chắn Ether:** khu boss **không** có cổng `close_on_enter` → khi boss xuất hiện, dựng 4 tấm chắn **dày 0,6 đv** ngay ngoài mép khu
   (BoxCollider2D — không dùng EdgeCollider mỏng: boss húc ép người chơi xuyên qua) + màn sáng gợn sóng ở đúng các khe hở của tường thật
   (dò bằng `Physics2D.OverlapPoint` hai phía mép). Người chơi cũng bị kẹp trong khung khi màn chắn đang dựng.
   Boss gục / khu dọn xong / người chơi gục (khu làm lại) → màn tan.
   Khe hở dò theo **ô tile của các lớp tilemap có va chạm** (ô ngoài + ô trong mép đều không có tile tường) — khớp hình vẽ;
   không tìm thấy lớp tường thì quay về dò collider.
3. Khu có cổng `close_on_enter` (vd `r8_lock`, `cult_lock`, `z5_arena_lock`) → chỉ xích boss, không dựng màn.

Khi vẽ khu boss mới trong Tiled:
- **Khung khu = đúng sàn phòng đấu** (mép khu trùng mép tường). Không vẽ khu lấn sang hành lang / phòng bên — boss sẽ được đi tới đó.
- Phòng có nhiều lối vào: để màn chắn tự dựng, hoặc đặt cổng `close_on_enter` nếu muốn cổng thật.
- Tắt màn chắn cho một khu: thuộc tính `arena_seal = false` trên object khu. Đổi màu: `seal_color`.
- Màn xuất hiện của boss: thuộc tính trên object `boss` (lớp triggers) — `entrance` = burst | leap | wake | mist | rise | approach | turn,
  `card_name`, `card_sub`, `portrait` (tranh riêng `Resources/UI/Requiem/<tên>`), `card_color` (BossIntro).
  - mist: `mist_color`. approach: `entrance_from`, `entrance_speed`. approach/turn: `entrance_lights` (tên object, phẩy) + `lights_color`.
  - **Mỗi boss một màn chào riêng (07/11)**: KHÔNG dùng đòn đánh thường làm màn chào. Gồng + nện chỉ chạy khi có đệ để đánh bay hoặc `intro_slam`;
    boss khác diễn `intro_anim` (+ `intro_roar`).
  - Hiệu ứng PixelLab trong cảnh: `entrance_vfx` / `light_vfx` / `intro_vfx` = tên ảnh `Resources/VFX/<tên>` (SpriteVfx), `<key>_scale`, `<key>_fps`.
- **Chiêu phủ cả phòng phải có chỗ núp rõ ràng** (04/11, Specimen Zero): dùng `sprite_prop` (vd trụ phong ấn nhóm `sz_ward`) + `Shockwave`
  `gapAngles` / `gapWidth` / **`gapFrom`** (khe chỉ mở sau bán kính tới vật → vệt bóng che PHÍA SAU vật) + `onSheltered` (phản hồi khi được che).
  Báo trước bằng chính vật đó sáng lên (ảnh `sprite_lit`, Light2D, vòng ấn `decal`) — không vẽ dải màu trơ trọi trên sàn.
- **Không đặt quái đệ sát cửa vào khu boss** (người chơi vừa bước qua cổng đã bị lao vào). Đệ nên đứng phía xa / cạnh boss, hoặc
  do bộ chiêu boss gọi ra **một lần** ở mốc pha (vd Gorgemaw: cóc con chui từ vũng Ichor khi vào pha 2) — không gọi lặp vô hạn.

- **Bảng tên boss** (07/11): ảnh luôn là dáng nhìn xuống — `EnemyAI.PortraitSprite()` (Idle_S, không có thì Idle ngang); mọi boss nên có
  object `boss` (lớp triggers, khung = đấu trường) với `entrance`, `intro_line1..`, `card_name/card_sub/card_color`.
- **Chuông cần rung sau khi dọn khu** (07/11): `bell` + `requires` (cờ dọn khu) + `locked_text`; chuông một nốt = `answer "1"`, `sets` = cờ hoàn thành.
- **Cảnh bị kéo / hút đi** (07/11): cơ quan `summon` (`when`, `sets` chạy một lần, `speaker` + line1..8, `target`, `warp_text`, `color`,
  `shake`, `delay`, `auto`) — dời Ren vào đấu trường có cổng `close_on_enter` -> khu tự bắt đầu, boss xuất hiện (BossIntro).
  - Có `actor` (tên quái): boss hiện ra sau lưng Ren (`actor_behind`) và ném Ren bay vòng cung tới `target`.
    - Thuộc tính: `actor_anim_appear`, `actor_anim_throw`, `throw_line`, `throw_time`, `throw_arc`.
    - Hiệu ứng: `actor_vfx` (+ `actor_vfx_lead`), `land_vfx`.

### 2.7 Cảnh diễn & hướng dẫn (World/Cutscene)

- Mọi cảnh: `CutsceneState.Begin/End` (HUD mờ + letterbox), giữ `GameInput.Blocked` mỗi khung, Space/Enter tua nhanh.
- **An toàn khi diễn cảnh (04/11, tự động, mọi map):** trong lúc `CutsceneState.Active` người chơi **luôn bất tử** (`Health.CutsceneSafe`
  — không phụ thuộc cờ `Invulnerable`, vì Roll tắt cờ đó khi lăn xong) và nhịp độc / cháy bị bỏ qua; **mọi quái đứng im** (Idle, không đi,
  không đánh). Quái cần "đóng vai" trong cảnh (bước tới, quát) → cảnh bật `enemy.CutsceneActor = true` rồi tắt khi xong.
  Không cần tự bật `Invulnerable` cho Ren trong cảnh mới.
- Cảnh gắn theo dữ liệu Tiled: director `EnsureMechanics` thấy điểm tên X (lớp triggers, type `cutscene_point`) thì gắn component tương ứng
  (`intro_cam_1` → OpeningCutscene, `aftermath_door` → RaidAftermath, `landing_point` → LandingCutscene, `eye_glimpse` → EyeGlimpse,
  `escape_surge` → EscapeSurge). Lời thoại / hint là thuộc tính trên điểm đó, không viết cứng trong code.
- **Cổ vật rơi từ boss (04/11):** `BossRelic` (director tự gắn) — object `boss` có `relic_skill` → boss gục thì cổ vật trồi lên, bay vào Ren,
  mở + lắp chiêu / bộ chiêu, bật `new_mantra` (màn NEW MANTRA). Thuộc tính: `relic_flag`, `relic_color`, `relic_text`, `relic_say`, `relic_hint`.
  Bản lưu đã hạ boss mà chưa có cờ cổ vật → trao luôn khi nạp map.
- **Bộ chiêu (set):** các `SkillDefinition` cùng `setId` lắp / tháo cùng nhau (chiếm 3 ô); `setName` / `setDescription` điền ở chiêu `setSlot` 0.
  Bảng nghỉ hiện cả bộ thành một thẻ (`RequiemMenus.SetCard`).
- **Đang giao chiến** (`MapEncounterDirector.InCombat`): Examine / Read (biển, ghi chú, bảng giải đố — `Interactable.calmOnly`) bị khoá;
  cần gạt / van / tay quay vẫn dùng được.
- Hiệu ứng dùng chung: `CutsceneFx` (bụi, sỏi văng, rung object, bật/tắt object Tiled, đọc toạ độ ô).
- Màn dừng chờ phím: `TutorialPrompt.Run(...)`. Hướng dẫn giữa đường dùng chung mọi map: `CombatTutorials` (Turned Blade, chiêu mới).

### 2.8 Mang theo qua map & lưu game (04/11)

- `GameSession`: máu, bình, bộ chiêu, **sách đã học (cờ `book_*`)** mang qua map. `GameSession.NewGame()` khi chọn New Game.
- `SaveGame` (một ô lưu JSON): cờ của map, điểm hồi sinh, bình, **bộ chiêu**. Lưu khi thắp / nghỉ ở điểm nghỉ và (17/11 §285) mỗi lần vào khu mới (điểm hồi sinh tự động).
- Cờ là của **từng map**; thứ gì phải sống qua map (sách, chiêu) → thêm vào `GameSession` + `SaveGame`, đừng chỉ đặt cờ.
- **Map con / hầm phụ (07/11, §166)**: khu bí mật cần nhiều chỗ thì làm **map riêng**, đừng kéo dài map chính. Hai map đặt cùng thuộc tính MAP
  `flag_group` (Map ▸ Map Properties) → rời map nào cũng nhớ toàn bộ cờ của nhóm, quay lại thì khu đã dọn / đồ đã nhặt giữ nguyên.
  Nối bằng trigger `exit`: `target` (tên map đích), `arrive_at` "x,y" (ô của map ĐÍCH, trống = player_start), `arrive_title` / `arrive_subtitle`
  (trống = tiêu đề mặc định), `quake` false (leo dây, cầu thang: không rung). Lối ra bí mật: không đặt `zone_id` (sẽ đòi dọn khu + bật đèn hiệu).
  Tên đích mới → thêm vào `SceneOf` / `TitleOf` của `SceneTransition` + menu dựng scene + Build Settings.

### 2.9 Túi đồ, chìa khoá, rương, cửa khoá (04/11)

- `World/KeyRing.cs` (static, của từng map — `Clear()` ở Awake của director): đồ then chốt định nghĩa bằng thuộc tính Tiled
  `key_id`, `key_name`, `key_desc`, `key_opens`, `key_icon` (ảnh trong `Resources/UI/Requiem`), `key_kind` (Key / Tool / Document / Weapon),
  `key_stats` ("Nhãn:Giá trị;…" — cho vũ khí). Có đồ = cờ `key_<id>`; đã dùng = `key_<id>_used`.
  Nguồn: pickup `item = key`, rương (`chest`) có `key_id`, ghi chú (`note`) có `key_id` + `pickup = true` (tài liệu, đọc lại trong túi).
- **Cửa khoá bằng chìa**: object cổng có `requires_key` (không ghi `unlock_when` → tự dùng cờ `<cổng>_unlocked`) — chỉ mở khi Ren tới sát cửa
  và có chìa. `locked_say` = câu Ren nói khi cửa còn khoá (thay biển "Examine" cạnh cửa). Logic: `MapEncounterDirector.TickGateLocks`.
- **Rương** (`Mechanics/Puzzles/ChestMechanism.cs`, type `chest`): `chest` (tên hình rương), `style` (khung `Resources/VFX/chest/chest<style>_0..7`),
  `requires_key`, `requires`, `locked_say`, phần thưởng `potions_max`, `potions`, `skill_id`, `key_*`, `sets`, `text`, `say`. Đã mở = `<tên>_opened`.
- **Bàn điều khiển cần lắp đồ**: console `fit_object` (hình bộ phận, ẩn tới khi lắp), `fit_item` (đồ trong túi → used), `fit_text`, `fit_flag`.
- **Giao diện**: phím **B** (tay cầm LT) / Pause ▸ Inventory → `RequiemMenus.BuildInventory`: lọc theo loại, lưới 5×4 ô 132 px,
  bấm ô → bảng thông tin (Opens, mô tả, PROPERTIES, toàn văn tài liệu). Thêm loại đồ mới chỉ cần dữ liệu Tiled.
- **Đang giao chiến** (`MapEncounterDirector.InCombat`) khoá Examine / Read (`Interactable.calmOnly`); HUD ẩn mũi tên + điểm đích minimap
  khi đích nằm trong khu đang đánh (`TargetReachedInFight`).

### 2.10 Màn tiêu đề (07/11, §182)

- `UI/TitleScreen.cs` + `UI/TitleMenuItem.cs`; ảnh `Resources/UI/Requiem/title_*.png` (dựng bằng `Catalog/v2/tools/title_assets.py`).
- Tranh nền 480x270 phóng nguyên lần, phủ kín màn (cắt mép). Lớp động đặt theo toạ độ điểm ảnh của tranh (`MoonAt`, `WindowAt`, hàng sương).
  Đổi tranh -> sửa các hằng này.
- Thêm mục menu: `AddItem(text, action)` (mục "Settings" phải đứng ngay trước "Quit": CloseSettings đưa focus về `items.Count - 2`).

### 2.11 Chiêu tháo lắp — CÂN BẰNG (07/11, §183)

- **Mốc**: chém thường 30 / ~0,62 s ≈ **48 DPS**, Ren 100 HP. Chém thường luôn là nguồn sát thương chính.
  Chiêu dùng liên tục không vượt mốc này; bùng nổ chỉ trong khoảnh khắc.
- **Ngân sách**: 6–12 sát thương mỗi giây hồi chiêu; chiêu diện rộng tính trên MỖI quái trúng. Khống chế quái thường tính thêm ~40% giá trị.
- **Boss miễn khống chế** -> chiêu khống chế đổi thành **lộ sơ hở** (`Health.AddVulnerability`, qua `SkillKit.Control`): +20–30% sát thương
  nhận trong ≤ 4 s, KHÔNG cộng dồn (lấy mức cao nhất, gia hạn). Dấu `sk_vuln` trên đầu, nhuộm màu theo nguồn:
  - xanh = Corroded,
  - tím = Spent,
  - vàng = Staggered.
- **Mỗi chiêu có rủi ro**: đứng niệm (huỷ được bằng lăn -> `cancel()` hoàn hồi), báo trước, hoặc khựng cuối chiêu. Phía quái có khắc chế:
  - quái Ichor kháng Corroded,
  - quái chạy khỏi bóng Hand,
  - tường chặn lướt / rễ / sóng.
- **Bộ chiêu** (chiếm 3 ô) ≈ 3 chiêu lẻ tốt nhất + bản sắc riêng, không mạnh hơn hẳn. So sát thương lên boss trong 30 s:
  - Mắt ≈ 400 + giảm sát thương / lướt khi bắn tia (§184: tia mạnh hơn, Embers yếu hơn; §191: Veil bỏ bất tử),
  - Vessel ≈ 330 (+ combo Bound ~40) + hồi máu + gom / trói quái.
- Thêm chiêu mới: lớp kế thừa `SkillDefinition` (dùng `SkillKit`) + dòng trong `tools/skills_m2/make_assets.py` (hoặc tạo asset bằng menu Create)
  + icon 64x64 `Resources/UI/Requiem/ic_*`. Đặt ngoài map: pickup `item=skill`, `skill_id`, `flag`, `hide`; hoặc cổ vật boss `relic_skill`.
- **Chiêu duy trì** (tia...): override `SkillDefinition.TryStop` để bấm lại phím tắt được; tắt sớm hoàn một phần hồi chiêu.
- **Nội tại** (sách): cờ `book_*` + một dòng trong `PassiveCatalog` (hiện trong bảng nghỉ, mục Passives).
- **Mỗi chiêu một vai — không trùng (08/11, §191)**. Trước khi thêm chiêu, đối chiếu bảng vai:
  - **Lộ sơ hở**: mỗi nhóm chỉ MỘT nguồn (không cộng dồn nên nguồn thứ hai là thừa): chiêu lẻ = Ichor Flask (Corroded);
    bộ Vessel = Requiem Wail (Spent); bộ Mắt = không có. Riposte / Hand KHÔNG còn làm boss lộ sơ hở.
  - **Khống chế lên boss** không nhất thiết đổi thành lộ sơ hở: có thể đổi thành sát thương thêm (Riposte ×1,5 lên boss)
    hoặc dấu combo (Roots -> **Bound** `SkillKit.Bind`/`IsBound`, Hand +50% lên mục tiêu Bound).
  - **Vòng quanh người**: Earthshatter ĐẨY ra; Requiem Wail KÉO vào (`SkillKit.Pull`, boss không bị kéo).
  - **Phòng thủ**: lăn = bất tử ngắn; Riposte = đỡ đòn CẬN CHIẾN (đạn qua thế đỡ: `Health.IncomingRanged`); Turned Blade = dội đạn;
    Veil = giảm 50% sát thương 4 s (`Health.Guard`) + cho Judgement Ray lướt chậm (`PlayerCombat.LockedMoveMul`) — KHÔNG còn bất tử.
  - **Di chuyển**: lăn (né) ≠ Severing Draw (lướt TẤN CÔNG, bất tử chỉ trong lúc lướt).

### 2.12 Ải (stage) — lặp lại tới khi vượt qua (08/11, §186)

- Mỗi map chia ải theo nhiệm vụ: một ải gồm các khu dẫn tới MỘT miniboss / mục tiêu.
  - Khu: `stage = <mã>`.
  - Điểm hồi sinh: một checkpoint ở ĐẦU ải, `stage = <mã>`, `stage_name`.
  - Không đặt điểm nghỉ giữa ải: nó mâu thuẫn với luật đánh lại từ đầu ải.
- Gục: tỉnh ở đầu ải đang đánh. Mọi ải chưa xong hồi quái (Rearm: xoá quái, mở khoá đấu trường, bỏ trạng thái đã dọn, GIỮ cờ).
- Qua ải: `stage_<mã>_done`, đầy máu + bình, tự lưu. Khu không ghi `stage` vẫn như cũ (dọn một lần là xong).
- Code: `MapEncounterDirector.Stages.cs`; điểm hồi sinh ở `MapTriggerHandler.TryStageSpawn`.
- Đã chia ải: Map 1, Map 2 (§187), Map 3 (§186).
- Checkpoint dùng chung nhiều ải: `stage = "a,b"`, `stage_name = "a=Tên A;b=Tên B"`.
- Script chia ải dùng `Catalog/v2/tools/stages/stagekit.py`.

### 2.13 Bổ sung 17–18/11 (Map 4 — dùng chung mọi map)

- **Phụ đề** `World/Cutscene/Subtitles.cs` (static): mọi `SpeechBubble.Show` đẩy câu xuống khung hội thoại đáy màn hình (tên người nói + chữ chạy),
  không chặn điều khiển. `SpeechBubble.WorldBubbles = true` để quay lại bóng thoại trên đầu. HUD ẩn mũi tên nhiệm vụ khi có hội thoại / phụ đề.
- **Tước kiếm theo map** `World/MapRestraint.cs` (thuộc tính map): `PlayerStatus.Disarmed` -> đổi Animator sang `Resources/Player/RenRobe`
  (menu Player ▸ Build Ren Robe Animator), không chém (`PlayerCombat`), không lăn, `FlaskTaken` (không uống bình), `MantrasBound` (phong Mantra).
- **MapLayout**: tên lớp Tiled bỏ hậu tố " (n)" (ST2U đổi lớp trùng tên); `MechanismContext` nhận "gates (n)".
- **NoteMechanism** `cutscene_only`: lần đọc đầu chỉ bật cờ để cảnh story chạy, không mở bảng chữ.
- **LauncherMechanism** `MountOnWall`: `dir` right / left / up -> xoay hình (hình gốc miệng chĩa xuống), lưng áp mép vùng phía tường,
  miệng bắn dời theo hình; tắt bằng `rotate_art = false`. Vùng launcher trên tường tây: x = mép tường, rộng 1 ô.
- **BladeMechanism**: rãnh cưa chia đều trong vùng bẫy (không thò ra ngoài ray).
- **DepthEffects** (làm mờ vật che Ren): bỏ qua cổng có GateAnimator và lintel (`lintel_*`).
- **Lintel** = ảnh cắt từ ô tường quanh cổng / vòm (props, chân = chân tường, lòng vòm trong suốt) để Ren đi qua khung cửa bị tường che;
  sinh bằng `_tools/m4fix_1118/m4_gate_lintels3.py` / `m4_fix_1402.py`, ảnh ở `<Crypt>/Props/SR-trimmed/lintel_*.png`, tileset `SR-props-trimmed.tsx`.
- **Villager**: đi theo cảnh (`WalkTo`) không coi người chơi / dân khác là vật chặn.
- **Gate rào ẩn khi mở**: mọi object trong lớp `gates` cùng tên với cổng là phần của cổng; không có GateAnimator thì `SetActive(closed)`.

### 2.14 Âm thanh (§294 + §335, 19/11)

- `Core/Sfx.cs` (API tĩnh: `Play`, `PlayAt`, `Play2D`, `Loop`, `Ambience`) + `Core/SfxRunner.cs` (24 kênh, độ to / lệch tai theo camera, im 0.8 s đầu scene).
  File: `Resources/Audio/SFX/<id>.wav` hoặc biến thể `<id>_1.._n` (chọn ngẫu nhiên). Thiếu file = im lặng, không lỗi.
- Độ to / khoảng cách tối thiểu từng id: `Resources/Audio/sfx_mix.json` (sinh từ `_tools/sfx/manifest_v2.py --mix`) — chỉnh ở đây, không sửa code.
  Mọi file chuẩn hoá cùng độ to (`process_sfx.py`); âm phát nhiều để gain thấp (chừa chỗ nhạc nền).
- **Gameplay chỉ gọi `Flow/SfxHooks.cs`** (một dòng mỗi chỗ): quái theo họ giọng (`Voices`: tên asset EnemyDefinition -> họ + chất liệu), `EnemyState` gọi từ `EnemyAI.Play`,
  chiêu boss `SfxHooks.BossMove(this, "TênChiêu")` ở dòng đầu coroutine + bảng `BossMoves`, cơ quan `MechanismEvent(this, sựkiện, mặcđịnh)` (Tiled `sfx_<sựkiện>` ghi đè, "none" = im).
- **Quái / boss mới**: thêm một dòng `Voices` (dùng họ có sẵn hoặc họ mới + tạo `v_<họ>_alert/attack/death` trong manifest_v2.py). Chiêu boss mới: thêm dòng BossMoves + gọi BossMove.
- StoryScenes: lệnh `sfx <id>` phát âm 2D trong cảnh; tranh (`slide`) tự bật âm dẫn chuyện (cs_slide + nền cs_story_bed).
- **Giọng lời thoại (§336)**: `World/Cutscene/DialogueVoice.cs` (tự chạy) đọc DialogueBox / Subtitles mỗi khung -> `Sfx.Speech(SfxHooks.SpeechId(người nói))` khi chữ còn chạy, null khi xong / bỏ qua. Người nói mới = một dòng `SpeechCast` + âm `vo_<tên>`.

## 3. Dữ liệu không nằm trong code

| Dữ liệu | Ở đâu |
|---|---|
| Map, khu, đợt quái, cổng, cơ quan, ghi chú, cảnh diễn | File Tiled `.tmx` (Custom Properties) — đọc bằng `TiledProps` |
| Nhiệm vụ | `Assets/Resources/Quests/<Scene>.json` |
| Minimap | `Assets/Resources/Minimap/<Scene>.png` (bake bằng `Catalog/v2/tools/minimap_bake.py`) |
| Kỹ năng người chơi | `Assets/Resources/Skills/*.asset` |
| Chỉ số quái | `Assets/Enemies/<MapN>/Definitions/*.asset` (EnemyDefinition) |
| Giao diện (màu, chữ, ảnh) | `Assets/Resources/UI/Requiem/` (`Requiem.uss` + PNG; chân dung boss `boss_*.png`) |
| VFX cảnh diễn | `Assets/Resources/VFX/collapse/`, `VFX/landing/` |

## 4. Quy ước viết code

- **Một MonoBehaviour một file**, tên file = tên class (nếu không, prefab/scene mất script).
- File mới: đặt đúng thư mục và khai báo `namespace` của assembly đó; thêm `using SoulRequiem.Core;` khi cần (vd `GetString` của `TiledProps`).
- Không đổi tên class MonoBehaviour / ScriptableObject đang dùng trong scene/prefab. Bắt buộc đổi → di chuyển
  file **kèm `.meta`** (giữ GUID) và dùng `[FormerlySerializedAs]` cho field đổi tên.
- Không dùng `GetComponent<T>() ?? …` với object Unity — so `== null`.
- Chữ hiển thị trong game: tiếng Anh. Chú thích code: tiếng Việt.
- Phím test mới → `DevTools/DevCheats.cs`, không viết vào code gameplay. Lệnh test cần truy cập nội bộ một class → để trong file `partial` `<Class>.DevTools.cs` bọc `#if UNITY_EDITOR || DEVELOPMENT_BUILD`.
  Không override / gọi từ code thường một hàm chỉ có trong `#if` — bản build phát hành sẽ lỗi biên dịch.
- Thuộc tính Tiled mới → khai báo hằng số (vd `MapSchema.Prop`), không gõ chuỗi rời rạc trong code.
