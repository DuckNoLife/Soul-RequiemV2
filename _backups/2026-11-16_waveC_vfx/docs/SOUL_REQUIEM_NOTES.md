# SOUL REQUIEM — Ghi chú dự án cho Claude

> Đọc file này TRƯỚC khi làm bất cứ việc gì trong dự án. Cập nhật lại sau mỗi cột mốc lớn.
> Cập nhật lần cuối: 30/09/2026 (Map 1 + hệ thống quái Map 1 hoàn thành).

## 1. Tổng quan
- Game: **Soul Requiem** — top-down action / bullet hell, dark fantasy (cảm hứng Blasphemous).
  Cốt lõi: né đạn bằng **Roll (Q)**, đánh thường combo 3 nhịp (J / chuột trái), kỹ năng AOE (F, hồi 5s),
  bơm máu (E, khựng 0,5s). Kịch bản chi tiết nằm trong `Logbook_Soul_Requiem.docx` (người dùng gửi kèm khi cần).
- 4 map: 1 Thảo Nguyên (Grassland, boss Grok – Tù trưởng Orc) → 2 Cống ngầm (Sewers, boss Mắt Bệnh Dịch)
  → 3 Nghĩa trang (boss Pháp Sư Hắc Ám) → 4 Hầm Mộ / Địa Ngục (Chúa Quỷ).
- Công cụ người dùng: **Unity 6 (6000.3.6f1, URP 2D)**, **Tiled**, **PixelLab**, **Aseprite**, VS Code.
- Người dùng nói tiếng Việt. **Mọi chữ hiển thị trong game phải bằng tiếng Anh**
  (tên khu, gợi ý, HUD, tên quái). Chú thích code / menu editor có thể tiếng Việt.

## 2. Đường dẫn (máy Windows của người dùng)
| Thứ | Đường dẫn |
|---|---|
| Project Unity | `C:\Users\Admin\Soul Requiem` |
| Bộ công cụ dựng map (Python) | `C:\Users\Admin\Desktop\Catalog` |
| Asset gốc | `Assets/GamePackage/` (ERW - Grass Land 2.0 v2.1, Tiny Swords (Enemy Pack), Enemy_Animations_Set, …) |
| Map 1 (.tmx) | `Assets/GamePackage/ERW - Grass Land 2.0 v2.1/TiledMap Editor/Maps/map1_grassland.tmx` |
| Scene chính | `Assets/Scenes/SampleScene.unity` |
| Script game | `Assets/Scripts/` (Combat/, Enemies/, UI/, CameraFollow.cs, PlayerController.cs) |
| Tool editor | `Assets/Editor/SoulRequiem/Map1EnemyBuilder.cs` → menu **Soul Requiem ▸ Dựng quái Map 1** |
| Prefab / animation quái | `Assets/Enemies/Map1/Prefabs`, `Assets/Enemies/Map1/Animations/<Quái>/` |
| Animator nhân vật | `Assets/GamePackage/Rogue Traveler/Animations/Player.controller` (dựng bằng menu **Soul Requiem ▸ Dựng Animator nhân vật (Idle, Run, Dash, Iaido)**; bản cũ ở Idle/rotations đã mất) |
| Bản sao lưu | `Desktop/Catalog/backups/` |

Cloud session truy cập qua cầu nối thiết bị: thư mục hiện ở `$HOME/mnt/Soul Requiem`, `$HOME/mnt/Catalog`, `$HOME/mnt/GamePackage`.

## 3. Quy ước kỹ thuật
- Ô map **32 px**, **PPU 100** → 1 ô = **0,32 đơn vị Unity**. Map 1 = 64×48 ô.
- Import Tiled bằng **SuperTiled2Unity** (Packages/SuperTiled). Map là prefab sinh từ .tmx, kéo vào scene.
  - Layer có `visible="0"` sẽ **bị bỏ qua khi import** → layer logic (zones, triggers, blockers…) để
    **opacity 0.35**, KHÔNG ẩn (tắt mắt) trong Tiled.
  - `unity:IsTrigger` phải đặt **trên từng object**, đặt ở layer không ăn.
  - .tmx importer: `sortingMode: 1` (Custom Sort Axis). Renderer2D: Transparency Sort Mode = Custom Axis (0,1,0).
- Sorting: tile layer bắt đầu -10, bóng cây -5, props 0, props front 1. Player & quái: **Order in Layer 2**,
  quái dùng Sprite Sort Point = Pivot (pivot ở chân).
- Input: **Unity Input System** (`Keyboard.current`, `Mouse.current`), không dùng Input cũ.
- Player: Rigidbody2D + CapsuleCollider2D, `PlayerController` (moveSpeed 4, MovePosition), tag `Player`.
- Camera: `CameraFollow.cs` — bám player, kẹp trong biên tilemap, pixel snap với PPU 100.
- Unity không tự thấy file sửa từ bên ngoài → sau khi ghi file phải **Ctrl+R** trong Unity (Assets ▸ Refresh).
  Kiểm tra đã biên dịch: thời gian `Library/ScriptAssemblies/Assembly-CSharp.dll`.

## 4. Quy trình dựng map (layout JSON → Tiled → Unity)
1. Viết/sửa bố cục: `Catalog/layouts/<map>.json` — gồm `zones` (id, name=title tiếng Anh, rect [x,y,w,h] theo ô, hint),
   `paths`, `water`, `stone`, `gravel`, `dirt`, `fences`, `bridges`, `props` (tên file sprite + ô), `spawns`
   (enemy, at, wave, zone, boss, spawn_when…), `triggers` (player_start, checkpoint, sign, pickup, spawner_zone,
   boss, exit, gate…), `solid` (vùng chặn thêm), `check` (start, goal, gate_order, stages).
2. Dựng: trong `Desktop/Catalog`
   ```
   python build_layout_map.py layouts/map1_grassland.json --pack "C:\Users\Admin\Soul Requiem\Assets\GamePackage\ERW - Grass Land 2.0 v2.1" --out "<pack>\TiledMap Editor\Maps\map1_grassland.tmx" --preview-out previews
   ```
   Script tự kiểm tra: mô phỏng đi bộ qua từng stage (cổng mở dần), báo lỗ hở/đường rò, trigger không tới được,
   quái kẹt trong vật cản. Xem `previews/<map>_report.txt` và `_debug.png`. **Không để ảnh preview trong Assets.**
3. Mở .tmx trong Tiled để xem/chỉnh tay (Reload nếu đang mở). Unity tự import lại prefab map.
4. Các lớp object sinh ra: zones, spawns, triggers, gates (collider `gate` + hình `gate_visual` cùng tên),
   obstacle colliders, water colliders (layer Water), blockers (rừng/biên), map bounds.
5. Sửa nhỏ trực tiếp vào .tmx thì **sửa luôn file layout JSON** cho đồng bộ (lần dựng sau không mất).

## 5. Hệ thống quái & trận đánh (Map 1)
- `Combat/Health.cs` — máu, độc (%/giây, không cộng dồn), choáng, đẩy lùi (vận tốc phụ), Invulnerable (i-frame Roll).
- `Combat/Projectile.cs` — đạn tự kiểm tra va chạm (OverlapCircle), bỏ qua trigger/quái/layer Water.
- `Combat/PlayerCombatPrototype.cs` — BỘ TEST TẠM (thay bằng combat thật ở Sprint 1):
  J/chuột trái đánh, Q roll, E bình máu (35%, khựng 0,5s), F AOE; **F1 bất tử, F2 hạ hết quái, F3 sang khu kế**.
- `Enemies/EnemyAI.cs` — kiểu Melee / Ranged / SpikeBurst / Boss. `attackRange` & `slamRadius` là **khoảng hở giữa
  collider** (không phải khoảng cách tâm). `leashRange` 5: đuổi xa điểm sinh thì quay về hồi máu (boss không).
  Animator cần state tên: Idle, Move, Attack (+ Windup, Recovery, Death cho boss).
- `Enemies/CaveSpawner.cs` — hang gọi Nhện/Rắn theo đợt khi người chơi tới gần.
- `Enemies/MapEncounterDirector.cs` — đọc object layer của map: vào khu → sinh đợt → hết quái → đợt kế → mở cổng.
  Cổng `close_on_enter` (đấu trường) chỉ khóa khi người chơi đã vào sâu ≥ `lockClearance` (0,75) khỏi cổng;
  chết & hồi sinh ngoài đấu trường → mở khóa, reset trận. Hang động được đặt sẵn từ đầu.
- `UI/SimpleCombatHUD.cs` — HUD OnGUI tạm (máu, bình, hồi chiêu, tên khu, gợi ý, thanh máu boss).
- `Map1EnemyBuilder` (menu editor): cắt lại sheet Tiny Swords theo lưới (khung vuông = chiều cao ảnh, pivot ở chân),
  tạo clip (1 key/khung, KHÔNG thêm key cuối), controller, prefab, gắn Director + HUD vào scene. Chạy lại được.
  **Chạy khi KHÔNG ở Play Mode.**

| Quái (key spawn) | Asset Tiny Swords | Kiểu | HP | Ghi chú |
|---|---|---|---|---|
| Lizard – Spike Lizard | Lizard | SpikeBurst | 45 | gai tỏa tròn, đẩy lùi |
| Spider / Snake | Spider, Snake | Melee | 25 | độc 2%/s × 5s |
| Spear_Goblin – Orc Warrior | Spear Goblin | Melee | 55 | |
| Hex_Shaman – Orc Shaman | Hex Shaman | Ranged | 35 | đạn Hex_Shaman_Projectile |
| Minotaur – Bull Warrior | Minotaur | Melee | 150 | choáng 1,5s, chậm |
| Grok_Troll – Grok, the Orc Chieftain | Troll (+ Extra/Troll Dead) | Boss | 600 | miễn khống chế; gồng → lao → khựng 1,6s; 50% máu gọi 2 Orc Warrior |
| Cave_Spawner | Extra/Cave (PPU 100) | Hang | – | 2 đợt × 3 con |

Bố cục Map 1: z0 Grassland Gate (lửa trại, player_start) → z1 Lizard Meadow → z2 Mysterious Cave (2 bình máu)
→ z3 Orc Outpost → z4 Ruins Gate → z5 Grok's Arena → lối ra `to_map2` (map2_sewers).

## 6. Lỗi đã gặp — đừng lặp lại
- Layer logic bị ẩn trong Tiled → ST2U bỏ qua → mất zone/trigger. (Dùng opacity, không ẩn.)
- Collider zone/trigger không phải trigger → đẩy player văng lúc Play. (IsTrigger từng object.)
- Tường vô hình quanh map → thêm hàng bụi/đá 2 lớp dọc vùng chặn cho người chơi thấy.
- Cổng khóa đấu trường nằm ở mép khu → khóa đè lên người chơi, bị nhốt ngoài. (Đã sửa bằng lockClearance.)
- Quái "chạy tại chỗ" do đo khoảng cách tâm-tâm trong khi collider chặn. (Dùng Collider2D.Distance.)
- Clip thêm key cuối → khung cuối nhân đôi, giật mỗi vòng lặp.
- `GetComponent<T>() ?? ...` sai với object Unity (fake null) → luôn kiểm tra `== null`.
- Builder chạy trong Play Mode → lỗi MarkSceneDirty.
- Camera bị gắn nhầm lên map1_grassland; pixel snap đặt 32 thay vì 100.
- device_commit_files đôi khi không ghi đè → luôn grep kiểm tra file trên máy sau khi ghi.
- Cửa sổ Task Manager / Unity Hub hay che Unity khi điều khiển máy → nhờ người dùng đóng.

## 7. Việc còn lại / ý tưởng
- Sprint 1: hệ combat thật (combo 3 nhịp có animation, Roll có animation, kỹ năng) thay `PlayerCombatPrototype`.
- UI thật (thay HUD OnGUI), âm thanh.
- Cân chỉnh độ khó Map 1 (chỉ số trong `Map1EnemyBuilder.Defs` rồi chạy lại menu, hoặc sửa thẳng prefab).
- Map 2 Sewers: bẫy vũng độc, thùng nổ, chuột bầy, rắn phóng xạ, boss Mắt Bệnh Dịch — dựng theo quy trình mục 4.

## 8. Map 1 bản 2 (Catalog/v2) — quy tắc ghép chi tiết (đã sửa theo góp ý)
- Cặp vật đối xứng (ngà voi, cột cổng): tác giả ERW đã vẽ sẵn bản trái/phải (bone - big 1|2, 3|4, 5|6;
  gate - left|right section). Dùng `Kit.mirror_pair(left, cx, y, gap, right=...)` — KHÔNG lật (flip)
  ảnh vốn đã là bản đối xứng, nếu không hai vật sẽ quay cùng chiều.
- Cầu dây: dùng `Kit.rope_bridge(y, x_near)` — tự đo dải nước ở 3 hàng cầu, ván gác lên bờ 1 ô mỗi bên.
  Hàng y là dây lan can sau; mặt ván ở hàng y+1..y+2 (chỉ 2 hàng đó đi được).
- Cầu đá ngang: ảnh 4 ô cao; hàng 1 lan can sau, hàng 2-3 mặt cầu (đi được), hàng 4 lan can trước + vòm.
  Thân 5 ô phải trùng đúng dải nước, mố có cỏ ở hai đầu.
- Bậc đá dọc 96 px: đặt tâm ở x.5 để khít 3 cột ô (collider khớp hình). Cổng, ngà, đường đất dùng chung trục.
- Cổng dọc (chắn lối ngang): hình là dãy rào chữ X `barricade - spikes - 3/4`, không dùng cọc chồng cùng cột
  (nhìn như một cây cột). Rect cổng phải phủ trọn số nguyên hàng ô — khe nửa ô là lỗ lọt.
- Tường gỗ trại Orc: `horizontal2` có mặt sàn rộng 106 px (x 5..110) → bước 106, không phải 109 (hở khe 3 px).
- Vật có chân nằm trong vùng mặt tường cao (hàng giữa đỉnh lan can và chân tường) sẽ trông như đứng TRÊN tường.
- Soát chi tiết: `python qa_crops.py <tmx> ten:x,y,w,h ...` → ảnh độ phân giải gốc trong Catalog/v2/out/qa.
- Đẩy file từ cloud xuống máy: device_commit_files có thể gửi BẢN CŨ nếu đường dẫn nguồn đã từng gửi →
  luôn copy ra tên file mới (vd mapkit_9.py) rồi commit vào Catalog/v2/_in, sau đó `cp -f` + kiểm md5.

## 9. Thứ tự vẽ & collider trong Unity (map1_grassland_v2)
- Importer .tmx PHẢI để Sorting Mode = Custom Sort Axis (`sortingMode: 1` trong .tmx.meta). Chế độ Stacked
  cộng +1 thứ tự cho MỖI đồ vật -> hàng trăm vật đè lên nhân vật (order 2).
- Renderer2D đã đặt Transparency Sort = Custom Axis (0,1,0). Thứ tự: nền -20..-10, bóng cây -6,
  "ground decals" -5 (vòng rune, cầu, bậc, tế đàn, hoá thạch, hoa cỏ, lửa trại), "props"/"props front"/"gates" = 2
  (bằng nhân vật/quái -> xếp theo chân ảnh). Danh sách vật sát đất: Kit.GROUND_RX trong mapkit.py.
- Collider: vật đặt bằng put(solid=...) / ("auto", h) / solid_rect() thành collider đúng kích thước (obstacle),
  KHÔNG làm tròn ra ô nữa. Ô chặn (blocker) chỉ còn: lòng rừng (co vào 1 ô), nước, vách, tường/tháp, lan can cầu.
  Không còn "tường tàng hình" theo khung zone — muốn chặn đường tắt thì đặt vật cản thật (rừng, nước, rào).
- Nhân vật PHẢI xếp theo chân như đồ vật: chạy menu **Soul Requiem ▸ Xếp lớp nhân vật theo chân**
  (Assets/Editor/SoulRequiem/PlayerFeetSorting.cs): pivot ảnh PlayerV1 -> bàn chân (29 px từ đáy khung 112),
  Sort Point = Pivot, collider dời lên bù. Triệu chứng nếu chưa chạy: dây cầu/cọc/rào vẽ đè lên người.
- Cầu dây: collider = 2 thanh lan can mỏng (10 px) chạy suốt cầu kể cả đoạn cọc néo; mặt ván đi được là dải
  px [y*32+36, y*32+84]. Collider nhân vật nên gọn ở chân (menu "Collider nhân vật gọn ở chân") — thân cao 1,25 ô
  sẽ kẹt ở lối hẹp. Tự kiểm tra bỏ qua viền collider mỏng < 12 px khi tính ô chặn.
- map1_v2.py KHÔNG ghi đè .tmx đã sửa tay trong Tiled (so md5 với Catalog/v2/out/<map>.gen.md5); muốn sinh lại phải
  chuyển chỉnh sửa vào script rồi chạy với --force. Bản người dùng chỉnh collider (02/10) sao lưu ở Catalog/backups.
- ẢNH NHÂN VẬT THẬT SỰ DÙNG: bộ **GamePackage/Rogue Traveler** (breathing-idle/run/dash, 104 ảnh trong Animator),
  KHÔNG phải PlayerV1 (sprite gán sẵn trong scene chỉ là ảnh tĩnh, Animator ghi đè). Luôn kiểm tra clip của Animator.
  Pivot các khung đặt ở bàn chân (điểm giữa khung, hàng thấp nhất có hình — chung cho cả thư mục), sửa .meta 02/10;
  bản cũ: Catalog/backups/RogueTraveler_meta. Menu "Xếp lớp nhân vật theo chân" tự lấy ảnh từ Animator (chạy lại khi
  thêm animation mới), "Collider nhân vật gọn ở chân" = capsule ngang 0,6 x 0,35 ô ngay dưới chân. Đã kiểm tra trong Play.
- Ảnh đồ vật gói ERW có 13-29 px trong suốt dưới chân -> Unity xếp lớp sai (vật đè lên người đứng trước nó).
  Catalog/v2/trim_props.py tạo bản cắt đáy (Props/SR-trimmed + Tilesets/SR-props-trimmed.tsx) và đổi gid trong .tmx
  (lớp props/props front/gates), hình hiển thị không dịch. Kit.write tự chạy; map sửa tay: `python trim_props.py <tmx>`
  (chạy lại an toàn). Thêm vật mới trong Tiled -> chạy lại lệnh này.
- Chiều sâu 2D (Assets/Scripts/Visual/DepthEffects.cs, tự chạy khi vào scene — RuntimeInitializeOnLoadMethod):
  làm mờ vật đứng (Order 2, cao > 0,55 đv) khi nó ở TRƯỚC và che >= 25% thân nhân vật (alpha 0,3);
  bóng elip dưới chân Player + mọi EnemyAI (order -4: trên lớp mặt đất -5, dưới vật đứng). Chỉnh thông số trên
  object "DepthEffects" khi Play. Đã kiểm tra trong Play 02/10 (đi vào rừng: tán cây mờ, thấy người).

## 10. Map1 v2 — dốc cỏ, cây thông, sinh vật động (02/10)
- Bậc đá Z2->Z3 đã bỏ, thay bằng dốc cỏ tự nhiên: `k.cliff_gap(79, 82)` (khe 3 ô giữa hai khối vách, mép tròn;
  vạch đá mỏng phía trên mặt vách tự xoá sau automap). Cổng z2 = rào ngang dốc ở hàng floor(rim)+1.
- Cây đỏ (color scheme 5) đã bỏ -> thông `k.tree(x, y, "pine")` (tileset SR-pines.tsx, pine-tree_0/1/2/5/6/7/8 có bóng sẵn).
- Ảnh động: `k.put_anim(rel, fw, fh, x, y, layer, flip, ms)` tạo tileset SR-anim-*.tsx có <animation> (ST2U tự chạy).
  Lớp "ambient fx" (Order 10, trên mọi thứ): bướm, muỗi, ruồi, hạt bụi, gió, khói. Đã đặt: lửa+khói 3 lửa trại,
  đèn treo Z0, cờ trại Orc, bát máu, 9 bướm, 5 muỗi, 4 ruồi, 8 hạt bụi, 5 gió, 6 chim, 5 vịt (gợn nước), 6 ếch.
  Sinh vật dùng RNG riêng (arng) -> thêm/bớt không xáo bố cục cây/đồ.
- Ảnh động rộng > 2048 px (bướm 3600, gió 14976...) phải đặt maxTextureSize 16384 trong .meta, nếu không Unity
  thu nhỏ ảnh -> ST2U báo lỗi tileset và hiện ô caro. Đã sửa 17 file.
- Chỉnh tay collider trong Tiled: `python capture_overrides.py <map người dùng> <map sinh lại> maps/map1_v2_overrides.json`
  -> map1_v2.py tự áp lại sau mỗi lần dựng (Kit.apply_overrides). Dựng đè map đã sửa tay: `--force`.

## 11. Kiến trúc học từ mockup của tác giả (đo bằng template matching trên Mockups/*.gif, 02/10)
- Script đo: khớp mẫu từng ảnh prop với mockup gif (masked SSD qua FFT) -> ra toạ độ chính xác tác giả đặt.
- Nút giao tường gỗ (mockup6): tường ngang chạy LIỀN qua nút; lối đi dọc rộng 128 px (x0 = cx*32-64):
  up (x0, yb-128), down (x0, yb+32), left (x0-32, yb+32), right (x0+96, yb+32); lối đi dọc 'vertical - on grass'
  chân tại yb-192 trở lên. Đầu lối đi kết thúc: 'vertical - end2' (cọc nhọn). Kit.fort_junction / fort_walkway.
- Cổng tường: 2 cột gai left/right, ngà nhỏ bone-big-5 (trái) / 6 (phải) sát trong cột, chân cao hơn chân cột 75 px.
- Tháp canh 'watchtower - front' (128x224, có thang) đứng cạnh lối đi gỗ. Đã thay 2 tháp bát giác bằng nút chữ T.
- Đã bỏ cây thông (dùng lại scheme 1-4). Lửa trại: lửa động đặt cao hơn chân vòng đá 1,05 ô (giữa vòng).
- Đồ đã dời chỗ mà có collider chỉnh tay trong overrides.json -> xoá mục đó khỏi json, kẻo collider cũ còn ở chỗ cũ.
- Dash (02/10): 4 clip Rogue Traveler/*_Dashing. PlayerCombatPrototype.Roll() đặt DirX/DirY + SetTrigger("DashTrigger")
  (chỉ khi Animator có trigger đó). Menu "Gắn animation Dash vào Animator" tạo state Dash (Blend Tree 2D 4 hướng),
  Any State -> Dash (DashTrigger), Dash -> Idle khi hết clip; xoá 4 state rời. Bản cũ: Catalog/backups/Player_before_dash.controller.

## 12. Combat Iaido + Animator mới (05/10)
- Ảnh iaido: `GamePackage/Rogue Traveler/iaido/{east,west,south,north}/frame_000-013.png`, 140x140, pivot tùy chỉnh (alignment 9)
  neo ở pixel (70, 19 tính từ đáy) = chân. Thời lượng khung (ms): 110,110,150,70,40,70,90,90,90,90,90,110,110,140
  (tổng 1,35 s; khung chém = khung 5 lúc 0,48 s). Không lật gương cả khung (tay/kiếm sai logic). Script dựng: container scratchpad (south/north).
- `Assets/Editor/SoulRequiem/PlayerAnimatorBuilder.cs`: XOÁ và dựng lại `Rogue Traveler/Animations/Player.controller`, tạo clip
  Idle_/Run_<8 hướng>, Iaido_<4 hướng>, dùng lại <Dir>_Dashing.anim; gán cho Player. Chạy lại an toàn (ngoài Play Mode), xong Ctrl+S.
  Tham số: DirX, DirY, Speed, AtkX, AtkY, DashTrigger, IaidoTrigger. State: Idle(mặc định)/Run/Dash (DirX/DirY), Iaido (AtkX/AtkY).
  AnyState→Iaido (IaidoTrigger), Iaido→Idle hết clip, Iaido→Run khi Speed>0,1 sau 0,64 s.
- PlayerCombatPrototype: nhóm Iaido (useIaido, iaidoDamage 40, iaidoHitTime 0.48, iaidoLockTime 0.64, iaidoReach 0.45,
  iaidoRadius 0.6, iaidoKnockback 2.5). Chỉ dùng Iaido khi Animator có IaidoTrigger, không thì đòn Attack cũ. Roll huỷ đòn.
  PlayerController khoá di chuyển khi `combat.IsAttacking`.
- Clip cũ East_Slash1/West_Slash1.anim để nguyên, không dùng. Bản sao lưu trước khi sửa: `Downloads/soul_requiem_work/backup_before_iaido/`.
- Đã test Play: Iaido 4 hướng, chạy, Dash, 0 lỗi Console. CHƯA test trúng quái thật (cần đứng cạnh quái ở khu có quái).
- (06/10) Thêm Iaido **south-west**: `iaido/south-west/frame_000-013.png` (cùng nhịp, cùng pivot (70,19)). Dựng tay từ breathing-idle
  SW (bỏ tay phải/tay trái/bao/chuôi gốc, vẽ lại tay phải có khớp, xoay tay trái + vẽ lại bao kiếm). Script: container scratchpad sw/build_sw.py.
  Menu Dựng Animator giờ tự thêm hướng chéo nào đã có thư mục iaido/<hướng> và ghi danh sách vào
  `PlayerCombatPrototype.iaidoDirs`; đòn chém khoá vào hướng gần nhất trong danh sách (không trộn 2 clip). Hiện có: E, N, W, S, SW.
  Bản sao lưu 2 script trước khi sửa: `Downloads/soul_requiem_work/backup_before_sw/`.
- (06/10) Đủ **8 hướng Iaido**: thêm north-west, north-east, south-east (cùng nhịp, pivot (70,19)). Dựng bằng script chung
  container scratchpad `sw/iaido_lib.py` + `dir_<hướng>.py` (tách tay phải/tay trái/bao/chuôi khỏi breathing-idle của hướng đó,
  vẽ lại; hướng quay lưng vẽ cẳng tay + kiếm + vệt chém SAU thân, chỉ cánh tay trên đè lên lưng).
- **Idle SE của Pixellab đeo kiếm hông PHẢI** (ảnh như SW lật gương) -> đã sửa 4 khung `breathing-idle/south-east`: bỏ đoạn bao
  dưới tay phải, tay trái xoay về hông trái giữ miệng bao, vẽ chuôi chĩa ra trước. Meta rect/pivot đã cập nhật (neo (56,7)).
  Bản gốc: `Downloads/soul_requiem_work/backup_idle_se/`. (Ghi chú cũ sai: run SE thật ra đeo kiếm đúng ở hông trái/phía sau.)
- Khi chép nhiều file qua device_commit_files: chia ≤14 file/lần (1 lần 37 file bị timeout giữa chừng).

## 13. Map 1 "Greystone Plains" dựng lại + quái ERW (08/10)
- Map mới: `.../TiledMap Editor/Maps/map1_greystone.tmx` (128 x 88 ô), sinh bằng `Catalog/v2/maps/map1_greystone.py --force`
  (bản cũ của script: `maps/map1_greystone_v1_backup.py`). SampleScene đã trỏ sang prefab map1_greystone
  (bản scene cũ: `Logs/claude_preview/SampleScene_before_greystone.unity`). KHÔNG dùng cấu trúc map1_grassland_v2 nữa.
- Bố cục: dòng nước là trục. Hồ Stone Ring -> thác (tileset động `SR-waterfall.tsx`, ghép từ waterfall-spritesheet,
  thêm 1 hàng giữa cho vách 3 ô) -> suối -> ao trang trại. Đi ngược dòng: Farm -> Brookside -> cầu dây -> Meadow ->
  khe hẹp -> Hollow Caves (+ Old Copper Mine phía tây) -> dốc -> Greystone Camp (+ Healer's Tent) -> Ruins Path
  (cổng đổ, quảng trường có tường + cột mốc rune, bãi đào Orc) -> cầu đá MỘT nhịp qua eo 5 ô -> Stone Ring (mũi đất,
  6 cột đá đứng thành vòng, tế đàn ở bắc). Không còn ngà trong đấu trường (người dùng thấy vô lý).
- Quy tắc người dùng đã yêu cầu: đồ vật phải có lý do + đặt theo cụm bám vào vật lớn; KHÔNG để vật có va chạm trên
  lối đi (script tự kiểm tra, in "LỐI ĐI: ĐẠT"); không lặp ngà; ray mỏ dọc trông như cái thang -> không dùng;
  lều có sừng chỉ dùng ở đất Orc; cột đèn "no lamp" trông như giá treo cổ -> không dùng.
- Ảnh tĩnh mới trong `Props/Static props/SR-extra/`: bù nhìn (khung 0 của training dummy), rương gỗ (chest no horns),
  thác 3 ô. Ruộng dùng wangset `fertilized soil -standard` (vẽ thành tile object trong ground decals); cây trồng đặt
  ở lớp props (nếu để ground decals sẽ bị đất che vì Tiled xếp object theo y).
- Quái mới (Map1EnemyBuilder + EnemyAI + GroundStrike.cs): Greystone_Warrior/_B (ERW orc melee, xoay vòng mỗi 3 đòn),
  Greystone_Seer/_B (ERW orc mage, tia bùn rơi + gai đá, quật gậy khi bị áp sát), Mad_Bear (cào 2 nhát, lao khi
  <50%), Camp_Gunner (Turret, 5 hướng, đạn vòng cung + vòng đỏ báo điểm rơi), Blightdrinker (Minotaur nhuộm xanh).
  Sprite ERW: PPU 100, khung 256, pivot chân (0.475/0.402 warrior, 0.506/0.406 mage). Fx của mage: khung 192x160.
  => PHẢI chạy menu "Soul Requiem ▸ Dựng quái Map 1" sau mỗi lần thêm/sửa Def quái (đã chạy 08/10: 15 prefab).
     Triệu chứng nếu quên: quái key mới KHÔNG spawn, Console cảnh báo "Chưa có prefab cho quái '<key>'".
- F3 (debug sang khu kế) giờ đi theo thứ tự object id trong Tiled và tìm chỗ trống không kẹt collider.
- Bug công cụ: device_commit_files có lúc gửi bản cũ -> luôn đặt tên file mới (vd *_v3d.py) và kiểm md5 trên máy.
- Còn thiếu: visual bùn xanh Ichor; Grok va cột đá chưa có code (cột chỉ là vật cản); thú cảnh chưa bay đi khi lại gần.

## 14. Bộ tộc Greystone = goblin Tiny Swords (08/10)
- Người dùng muốn dùng goblin Tiny Swords thay Orc ERW. Quái ERW (Greystone_Warrior/_B, Greystone_Seer/_B) đã xoá khỏi
  builder (prefab cũ còn nằm trong Assets/Enemies/Map1/Prefabs nhưng không dùng).
- Key mới (Map1EnemyBuilder, PPU 200 như mọi quái TS):
  - Goblin_Spearman (Spear Goblin, Attack Fast + Extra/Spear Goblin Strong Attack làm đòn xoay mỗi 3 đòn, HP 60)
  - Goblin_Torch (Torch Goblin, cận chiến nhanh, HP 40)
  - Goblin_Hexer (Hex Shaman: cầu hồng; mỗi 3 lần dùng state "Cast" = Transformation Spell + GroundStrike
    "Hex Shaman_Explosion Spell" ở chỗ người chơi, HP 40)
  - Goblin_PigRider (Pig Rider Spear Goblin: enrageAt = 1 -> luôn có thể gồng rồi lao, 3,5 s/lần, HP 90)
- Spear_Goblin/Hex_Shaman (Def cũ của map v2) dùng CHUNG sheet với Goblin_Spearman/Goblin_Hexer -> pivot phải giống
  nhau (0.5/0.336 và 0.5/0.32), nếu không lần cắt sau ghi đè lần trước.
- EnemyAI: Ranged có state "Cast" thì đòn đặc biệt chơi "Cast"; màu vòng báo đòn đặc biệt = strikeColor.
- Đã chạy menu dựng quái + Play thử trại Orc: đủ Spearman/Torch/Hexer/Gunner, đợt 2 có Boar Rider, Console 0 lỗi.
- ĐÃ CHỐT: cốt truyện gọi là bộ tộc goblin Greystone (không còn "Orc"). Đã đổi: biển báo/sổ ghi trong map1_greystone.py
  ("A goblin war totem...", "The goblins are digging..."), display Def cũ (Goblin Warrior/Goblin Shaman), tài liệu
  "Thiết Kế Màn Chơi" (rev 53). Chữ "orc" chỉ còn trong tên asset (orc-tents...) — giữ nguyên.

## 15. PlayerCombatPrototype gọn lại (08/10)
- Bỏ hệ "đánh thường" (combo 3 nhịp: attackDamage/attackRange/attackInterval/comboResetTime/thirdHitKnockback) và cờ
  useIaido. Chỉ còn MỘT đòn chém: header "Attack (J / Left Click)", field directions, damage, hitTime, lockTime,
  reach, radius, knockback (có [FormerlySerializedAs("iaido...")] nên giá trị cũ giữ nguyên).
- Coroutine Slash(): có animation Iaido thì bật trigger; không có thì vẫn gây sát thương sau hitTime + vẽ vòng tầm.
- Header inspector tiếng Anh: Roll (Q), Healing Potion (E), Area Skill (F), có tooltip.
- PlayerAnimatorBuilder ghi hướng vào FindProperty("directions") (trước là "iaidoDirs").
- Bản sao lưu: Logs/claude_preview/backup_player/. Đã Play thử: J chém, hạ được Lizard, Console 0 lỗi.

## 16. Quái đứng hình (08/10) — Animator "Missing (Runtime Animator Controller)"
- Triệu chứng: mọi quái chỉ hiện khung Idle_0, không đổi hoạt ảnh; Inspector của quái/prefab: Controller = Missing.
- Nguyên nhân: bản builder cũ XOÁ rồi TẠO LẠI file .controller khi Unity đang mở; prefab (và bản prefab trong bộ nhớ
  Unity) giữ tham chiếu hỏng. File trên đĩa đúng GUID nhưng Unity vẫn báo Missing tới khi gán lại + import lại.
- Đã sửa: builder giờ giữ nguyên file controller (chỉ dọn state). Thêm menu **Soul Requiem ▸ Kiểm tra & sửa Animator quái**
  (EnemyAnimatorDoctor.cs): gán lại Animations/<key>/<key>.controller cho mọi prefab, lưu, ForceUpdate import;
  báo cáo Logs/claude_preview/animator_doctor.txt. Đã chạy: 17 prefab, Play thử Lizard Idle/Move/Attack chạy đúng.
- Logger chẩn đoán AnimDebugLogger chỉ bật khi có Logs/claude_preview/animlog.on (đã đổi thành animlog.off).

## 17. Iaido south-east làm lại + idle SE (09/10)
- Hướng NW/NE/SE bản 06/10 đã bị XOÁ theo yêu cầu (tay biến dạng). SE làm lại thủ công (không PixelLab) theo style SW;
  NW, NE CHƯA làm lại. Animator hiện có Iaido 6 hướng: E, N, W, S, SW, SE.
- `iaido/south-east/frame_000-013.png` (140x140, pivot (70,19 từ đáy), cùng nhịp ms như các hướng khác) + metas mới.
  `breathing-idle/south-east/frame_000-003.png` thay bằng bản kiếm ở hông trái (pivot (56,7 từ đáy)); gốc ở
  `Downloads/soul_requiem_work/backup_idle_se/`.
- Quy tắc đã chốt với người dùng (dùng cho NW/NE):
  - Cổ tay thẳng: cẳng tay, chuôi, lưỡi một đường; đòn vung do vai + khuỷu. Khuỷu tay phải KHÔNG BAO GIỜ cong lên
    (nằm trên/dưới đường vai-nắm tay; script assert từng khung).
  - Mắt nhìn theo lưỡi kiếm. Tay trái xách vỏ ngang hông. Chém ngang đúng chiều (từ bên trái nhân vật sang phải).
  - Chân: chân phải bước lên (~6 px) làm trụ, chân trái đẩy nhẹ về sau (~3 px); thân dồn 1-2 px, hông hạ 2-3 px.
    Không lao dài, không ghép chân từ khung chạy.
  - Không xoá vạt áo haori khi bỏ tay áo; đường lưng chữ A (không gờ "cong mông"); tay áo dày (bán kính ~4 px + túi rủ).
  - Kiểm pixel: không hàng trống ngang ở chân (inverse mapping khi kéo chân), không mất pixel so với khung Unity.
- Script dựng (container, không có trên máy): dir_se.py + iaido_lib.py + legs_se.py.
- Đã kiểm trong Unity: menu dựng Animator báo Iaido 6 hướng, Ctrl+S, Play 0 lỗi; xem clip Iaido_SouthEast/Idle_SouthEast
  bằng Animation preview: khung đúng, chân trên collider. CHƯA bấm chém SE thật trong Play (công cụ điều khiển máy không
  giữ được 2 phím cùng lúc) — người dùng tự thử: giữ S+D rồi J.
- Còn lại: dash chéo; kỹ năng F, uống thuốc E, trúng đòn, chết.

## 17. Map 2 v2.1 "Undercity of Vael" — dựng lại theo thiết kế hub + 3 khu (15/10)
- Generator: `Catalog/v2/maps/map2_sewers.py` (bản v1 lưu ở `Catalog/backups/map2_sewers_v1.py`; sewerkit cũ ở
  `Catalog/backups/sewerkit_v4.py`). Chạy: `python maps/map2_sewers.py --force` trong Catalog/v2. Self-check: ĐẠT hết (17 stage).
- Bố cục: Z0 Collapse -> Z1 Overflow Gallery -> Cistern Ring (hub quanh bể). A Spillways (tây: thác, cần gạt, bàn đạp+Shellback),
  B Undercroft (đông: thùng Ichor, phà thùng, bom Bloatfish), C Lower Works (đông nam, cần xả B: Dark Aqueduct, Flooded Archive
  câu đố rune, Malric's Lab). Pump Works cần 3 Ấn (ổ ở đoạn nam hub) -> pipegrid -> cistern_gate -> boss Plague Eye -> exit to_map3.
- Hub ngập: lớp tile `flood A`, `flood B`, `flood cistern` + cổng vô hình flood_w1/w2 (sluice_a_drained), flood_e1/e2
  (sluice_b_drained). Unity cần: khi cờ bật -> ẩn lớp flood tương ứng + mở cổng.
- Cờ (unlock_when) mới cần code đặt: a1_cleared, a2_nest_flushed, a3_plate_held, sluice_a_drained, a_shortcut_open, b1_cleared,
  burrow/ferry/vault_rubble_destroyed, sluice_b_drained, b_shortcut_open, litany_solved, seals_3, pipe_level1, plague_eye_defeated.
- Lớp `mechanics` (89 object): lever, pressure_plate, cascade, current, rubble (needs=explosion), ichor_barrel (pushable),
  rune_tile + rune_sequence (order 3,1,4,2), torch (reveals), darkzone, seal_socket, pipegrid, spikes/pit/flame_jet, spawner.
- Pickup: sluice_seal_a/b/c; 5 sách kỹ năng (item=skill_book, book=still_water/turned_blade/purifier/thrown_steel/chain) —
  sprite tạm `SR book` + `SR lectern`/`SR book altar`, thay bằng asset thật sau.
- Ảnh cơ quan mới cắt từ Crypt/Crawling Depths/Terrain2 vào `Props/SR-extra` (SR lever, SR plate, SR rune *, SR seal A/B/C,
  SR sluice gate, SR sealed door, SR rubble, SR passage, SR spike/pit trap, SR flame statue...). Cổng visual mới: none/rubble/sluice.
- Key quái mới CHƯA có prefab (Director coi như khu đã dọn): Plague_Rat, Gutter_Rat, Channel_Viper, Sewer_Bat, Bloatfish, Shellback,
  Ossuary_Skull(_Guard), Ichor_Runner, Cistern_Shark, Sludge_Worm, Bone_Gnawer, Vat_Born, Plague_Eye. Dùng lại: Goblin_Spearman/
  Hexer/Torch, Spider, Blightdrinker.
- Lỗi bridge commit gặp lại: ghi đè cùng đường dẫn outputs -> commit nhận nội dung CŨ. Mỗi lần gửi phải đặt tên file outputs MỚI.
- (15/10) Trang trí Map 2 theo mockup tác giả: lớp `streams ichor`, `flood A ichor`, `flood B ichor`, `flood cistern ichor`
  = nước Ichor mờ 63% phủ lên sàn cuội (hướng dẫn của gói). build_layout_map.LAYER_OPACITY + inspect_tiled hỗ trợ opacity.
  Khi xả khu: Unity phải ẩn CẢ `flood X` lẫn `flood X ichor`. Mặt tường: đuốc, ống nhỏ Ichor + vũng (SR ichor puddle WxH),
  ống sắt ngang; mạng nhện góc; thùng/ván/xương/kiếm gãy/gián; cột có đuốc ở B1; song sắt trần qua kênh. Self-check vẫn ĐẠT.
- (15/10) Map 2 v2.3: phòng boss làm lại theo mockup Beholder — bỏ hào/lớp flood cistern; 4 cột = khối tường 2 ô
  (WALL_PILLARS, khoét góc sàn trước build_terrain) có đuốc; hố tròn "SR boss pit 8x6" (vẽ thủ tục, hazard boss_pit, không chặn);
  miệng cống lớn tường bắc = exit to_map3; tiền sảnh hẹp (50,61,4,7) chứa cistern_arena_lock. Pump Works: bỏ dòng chảy dưới lưới
  ống, nền = dải gạch tối, Ichor chỉ ở cửa vào máy + cống xả. Quy ước: Ichor chỉ dùng làm tín hiệu (nguy hiểm/ngập),
  NO_ICHOR cho cistern/c2/sealed/trial/vault. Cột tường thêm ở B1 và Lab. Backup: Catalog/backups/*_v22.py.

## 18. Chuyển map Map 1 -> Map 2 (15/10)
- Scripts mới: `Scripts/Core/GameSession.cs` (mang % máu + bình máu sang scene mới, tối thiểu 35% máu),
  `Scripts/Core/SceneTransition.cs` (khoá điều khiển, rung camera, fade đen, LoadSceneAsync, fade vào + tiêu đề vùng;
  bảng target->scene: map1_greystone=SampleScene, map2_sewers=Map2_Sewers, map3_vale=Map3_Vale),
  `Scripts/Core/ExitBeacon.cs` (đèn Light2D nhấp nháy ở lối ra khi mở).
- MapEncounterDirector: trigger exit chỉ mở khi khu của nó đã dọn xong (z5 = hạ Grok); chưa mở -> "The way is sealed...".
  Dọn xong khu có exit -> hint "The altar cracks open..." + ExitBeacon. Thuộc tính tuỳ chọn trên exit: open_text, depart_text,
  locked_text. Scene đích chưa có trong Build Settings -> báo "not built yet", không lỗi.
- Health.SetCurrent, PlayerCombatPrototype.SetPotions; phím test **F4** = dịch tới cạnh lối ra.
- Map2SceneBuilder thêm SampleScene + Map2_Sewers vào Build Settings (đã chạy 15/10).
- Đã test Play: F1, F4 (vào Stone Ring, Grok xuất hiện), F2 x3, F4, đi lên -> fade -> Map2_Sewers, đứng dưới miệng hầm,
  tiêu đề "The Undercity of Vael", máu mang theo đúng. Console: chỉ cảnh báo thiếu prefab quái Map 2 (dự kiến).
- LỖI ĐÃ SỬA: tileset Sewers 2080 px > maxTextureSize 2048 -> sàn Terrain2 hiện hồng/nhiễu trong Unity. Đã đặt 4096,
  không nén, Point filter cho Tilesets + SR-extra. Thuộc tính object tên "tag" bị ST2U hiểu là Unity Tag -> dùng "group".

## 19. Map 2 v3 — phác thảo làm lại (15/10, CHỜ DUYỆT)
- Người dùng chê v2.3: đồ lệch lưới, sông Ichor chắn lối, cầu lệch, quái trong phòng câu đố, thảm lưới sọc vô nghĩa,
  thùng trồng cây lạc đề, khối void vuông cắt tường, đuốc giữa sàn, không Y-sort.
- Mockup gốc của gói: `Assets/GamePackage/EPIC RPG World - Sewers V1.5/Mockups/` (mockup-1..4, Secret Lab, viper, fog).
  Quy tắc rút ra: Catalog/previews/map2_v3_style_rules.png. Phác thảo: Catalog/previews/map2_v3_sketch.png
  (nguồn toạ độ: Catalog/v2/maps/map2_v3_sketch.py, dict L = toạ độ phòng).
- v3: 8 phòng + 3 bí mật. R1 Hatch → R2 Gallery → R3 Channel Crossing → R4 Sluice Hall (hub an toàn, checkpoint)
  → W R5 Flooded Works (van W) / E R6 Lab + R6b annex câu đố ống 3x3 (van E) → 2 đèn → cổng chính → R7 Drowned Saint
  → R8 The Eye Below (Beholder) → lối ra Map 3 ở miệng cống bắc.
- Bẫy chỉ còn T1 Ichor sâu (chặn), T2 Ichor tràn (sát thương), T3 ống phun theo nhịp, T4 cống hơi độc, T5 cổng song sắt,
  T6 vật cản gỗ phá được. Bỏ: rune, ấn ×3, thùng đẩy, phà, bàn đạp.
- Quái: Mutant Rat (Plague Rat/Vat-born/Bloated elite), Viper (chỉ trong kênh), Beholder (Plague Eye), goblin Tiny Swords
  (chỉ ở R5, lỗ đào). Asset pack có sẵn: gate1-3 metal bar (on water), Improvised bridge, submerged pipes, laboratory *,
  light through a hatch, poison cloud, thin fog, tiny rat.

## 20. Map 2 v4 — mạch Ichor là xương sống (15/10, CHỜ DUYỆT, chưa dựng)
- Người dùng chê v3: câu đố ống không có vào/ra, cột khuyết góc (cột khoét tường), phòng vũng R5 tệ, kênh 1 ô "bước qua
  được", phòng boss xấu + sai chỗ, không thấy Ichor chảy từ đâu tới đâu. .tmx trên máy hiện vẫn là v3.
- v4 (Catalog/previews/map2_v4_sketch.png): Nguồn = ống khổng lồ vỡ ở R3 -> bể -> thân kênh qua hub R4 -> phòng ống R5b
  (kín) -> A nuôi bể boss R8 / B cống xả ra sông. R5a phòng điều khiển: cần gạt (thiếu tay quay, lấy ở Lab R6) -> camera lia
  + phóng vào R5b, bấm chuột xoay ống, ống nối với VÀO sáng xanh. Thắng: VÀO->B, A cạn, bể R8 rút, cửa boss mở.
- Cột: dùng cột tròn Terrain2 (cột 60 hàng 3-5; cột 63 mũ vuông), KHÔNG khoét tường. Kênh chặn đường rộng 3-5 ô, bờ gạch.
- Quái thêm (Tiny Swords): Spider (prefab Map1 có sẵn, PPU 200) ở chỗ khô/mạng nhện (R2, S2, R5a);
  Turtle = "Ichor Snapper" (Turtle_Idle/Walk/Attack 320px, Extra/Turtle Guard In/Out = thụt mai bất tử) ở R3, Feed Conduit,
  pha 2 boss. Cần thêm kiểu đòn EnemySpecial "Shell" (trúng đòn trước mặt / sau mỗi N đòn -> thụt mai 2s, Invulnerable).
- Wall-1 của kit: mặt tường bắc chỉ 2 hàng (top, top+1), sàn từ top+2. wall-1-2 KHÔNG cho mặt 3 hàng.
- Mã Unity soạn dở (chưa chép vào project): scratchpad unity/MapMechanics.cs, sửa MapEncounterDirector (cờ, cổng theo cờ,
  portal), PlayerCombatPrototype.Struck, EnemyAI.treatAsBoss, Map2SceneBuilder (đèn theo cờ). Map2 .tmx.meta sortingMode=0
  -> phải đổi 1 (Custom Sort Axis) để hết lỗi thùng vẽ đè người.

## 21. Map 2 v5 — Ichor kiểu map mẫu + câu đố ống 5x4 (15/10, ĐỢT 1 XONG, chờ duyệt)
- Generator: `Catalog/v2/maps/map2_sewers.py` (bản v5), `sewerkit.py`, `pipe_art.py`, `pipe_puzzle.py` (bản cũ: maps/_in/map2_v4_e_backup.py, sewerkit_v4a.py).
- **Ichor KHÔNG còn kênh bờ gạch (k.water)**. Mọi hồ/kênh = `k.curb_water(x,y,w,h, tag="")` (viền đá vát wall-3-water + nước water-wall-3),
  rộng >= 5 ô. Cho vùng ăn vào khối tường >= 1-3 hàng để Ichor chạm thẳng chân tường: tường giáp Ichor tự đổi sang bộ
  `wall-1-water` (cùng bố cục 240 ô với wall-1, chân tường hắt sáng xanh). Khối tường giữa hồ = đóng góc `k.M["floor"]` (cách mép >= 2 ô).
- Viền đá vát "mọc" thêm 1 hàng phía trên mặt nước (phối cảnh) -> lối đi cạnh kênh mất 2 hàng; đừng để hồ chạm hành lang khác (góc trên-trái hồ R3 phải lùi x>=50).
- Kênh A: `tag="A"` -> lớp `curb A` + `curb water A` + bản khô `curb A dry` (bộ wall-3 không nước, cùng chỉ số). Unity toggle ẩn "curb water A,curb A".
- Đồ chìm: "bones_N - transparency - frame_0", "wooden chests - transparency - topdown_N frame_0" (có gợn sóng 8 khung). Bọt: Props/bubble1-3.png (32x32x8).
  KHÔNG dùng "pipes - sewage water splash and ripples..." (vẽ ra ô vuông sáng).
- Câu đố ống: lưới 5x4, ô 2x2 ô map, góc (54.5,71) — lệch nửa ô để cột VÀO thẳng tâm miệng ống tường. Ống ghép từ ống kim loại của gói
  (atlas-props.png y1280-1440 khô, +192 px = bộ viền Ichor). Sprite "SR pipe2 [bolt ]{cell|wet} NN" + "SR pipe2 valve ...".
  Luật: tới B (1,3,S), không tới A (4,1,E), KHÔNG RÒ; ô (2,0),(2,2) bắt bu-lông. Lời giải duy nhất, tối thiểu 12 lần xoay (seed 4).
  Lời giải (test): phải-click (0,1),(1,1),(3,1),(0,2); trái x2 (1,2); trái (3,2); trái x2 (1,3); trái (2,3); trái x2 (3,3).
- MapMechanics: click trái/phải xoay, R reset, ô bu-lông rung, đèn xanh ô ướt, tia rò nhân bản từ 'leakpal_fx', ống cố định `pipefix_{IN|A|B}_{mask}_{k}`.
- R8: hố tròn có song (giant pipe-sheet2) giữa phòng, 4 cột đuốc, Con Mắt spawn (94.5,46). Kênh A đổ xuống cống tròn cuối Feed Conduit (bể ngầm).
- Đã test Play: đi qua console, giải 12 lần xoay -> ichor_diverted -> kênh A cạn, hố tắt sáng, cổng boss mở. Console không lỗi đỏ.
- ĐỢT 2 (chưa làm): AI Con Mắt cố định trên hố (bắn mắt tỏa tròn, tia laser quét 360°, xoay cận chiến), chuột phun bóng -> mây độc.
- (15/10 chiều) Sửa theo góp ý:
  - BỎ cống hơi độc (mây độc = chiêu riêng của chuột). Feed Conduit có 2 CỐNG TRÀN (mech 'flood'): sàn lưới sắt trên Ichor
    ('flood base' + 3 lớp 'flood <tên> <i>' lưới có lỗ to dần), ống tường đổ xuống miệng cống tròn; Unity hiện 1 mức/lúc
    (idle 2.6s -> dâng 0.9 -> ngập 1.8 -> rút 0.9), ô rát = props cells_i. Dừng khi ichor_diverted.
  - Phòng ống: bỏ miệng ống lớn; ống cấp chạy từ đỉnh tường xuống (cùng cỡ ống lưới). Cuối thân kênh ở hub có song sắt (gate1-metal bar-on water).
  - Boss: HÀO Ichor quanh đảo (kênh A rẽ bắc vào R8). Collider hào = nhóm 'acid A colliders' (tên acid_A), toggle ẩn khi ichor_diverted.
    Bỏ boss_gate + lối nhìn 'view'. Khu r8 = vùng hào+đảo (87,38,15,17): bước xuống lòng hào cạn là đánh. Director bật cờ <zone>_started;
    ảnh động 'boss_idle_fx' (Con Mắt đang uống) ẩn khi r8_started.
  - CHƯA test trong Unity bản này (Unity đã đóng lúc 15:09).
- (15/10 15:20) Boss to x3 (Map2EnemyBuilder Plague_Eye scale=3 — cần chạy lại "Dựng quái Map 2"). R8 nới (81,29,29,32);
  hào (83,34,24,24), đảo (87,38,16,16), hố giant pipe-sheet2 x3 ở (95,46), 4 cột "SR pillar thin" x2.5 (đỉnh tròn như mockup)
  ở (89.4/100.6, 43.0/52.6) có collider 2.1x1 ô -> đạn nổ khi chạm (nấp được). Object Tiled phóng to bằng w/h (hàm big()).

## 22. Map 2 v5k — đấu trường mở + chìa khoá + cốt truyện goblin (15/10 tối)
- Đấu trường R8 (77,27,33,34): KHÔNG hào, KHÔNG bẫy. Con Mắt x3 trên hố x3 (93,40.5). Bể Ichor (87,54,13,8) sát tường nam (kênh A
  rộng 3 ô chui cống vòm tường bắc Feed -> song sắt -> bể). Máy chuyển hoá = "Secret Lab - capsules and pipes" x2 LẬT ngang
  + "active capsule liquid-no creature" (anim đè khít) ở góc đông-nam (105.5,60.2) — lối vào tây (e1) để trống.
  Mech mới: 'beam' (kéo dài ảnh động mẫu 'beampal' = beam-loop của Beholder, nhuộm xanh, đèn dọc tia) máy->bể->boss;
  'shield' (quả cầu sinh bằng code quanh boss). Tắt khi ichor_diverted. Khu r8 có start_when=ichor_diverted (Director).
  Boss bay ghim tại chỗ sinh (bounds ±0.25 ô).
- Bỏ mọi cống tràn/ống phun. Feed: sàn lưới sắt trên Ichor TĨNH (k.flood với bán kính ~0 = lớp lưới kín + 'flood base').
- Văn phòng đốc công (99,64,10,13) qua cầu ván trên đoạn kênh dọc: hòm sắt -> cờ pump_key. Cổng 'ctrl_door' (c5) mở khi pump_key.
  Chuỗi: Lab (tay quay) + Văn phòng (chìa) -> Phòng điều khiển -> câu đố ống -> đấu trường.
- Cốt truyện: hang ổ thí nghiệm của goblin (Arch-Alchemist Malric) sau khi thế giới đổi thay; nhốt "god-spawn" (Con Mắt),
  nuôi bằng Ichor để uống lấy sức mạnh ngang các Thần Tối Cao; bị diệt gần hết vì phạm thượng. Chữ trong game đã đổi theo.
- CHƯA test Unity bản này (Unity bị treo khi nạp map lúc 15:55).

## 23. Test Unity Map 2 v5m (15/10/2026)
- Đã chơi thử trọn tuyến: Hatch → R3 → hub → lab → Feed (lưới trên Ichor) → Office (nhặt `pump_key` ở rương) → Pump Control (giải 12 click) → Cistern (bong bóng + 2 tia trước khi giải; sau khi giải: bong bóng vỡ, tia tắt, bể rút, boss xuất hiện ×3, đánh trúng được). Console: 0 lỗi, 0 cảnh báo.
- Sửa trong lần test (generator v5m md5 43255b93…, MapMechanics u53 md5 15dfce4f…):
  - Tia `beam`: sprite mẫu `beampal` (ô Tiled) có pivot góc dưới-trái → tia lệch nửa chiều dài. Giờ sprite nằm ở con `beam_sprite`, lùi `-bounds.center`.
  - `shield` có CircleCollider2D (0.94 bán kính) chặn người chơi bước vào hố; tắt collider khi vỡ.
  - Toggle `ichor_divert` ẩn thêm lớp `flood base` → lưới ở Feed hết Ichor sau khi chuyển dòng.
  - Gợi ý khu feed: "The grates hold - the Ichor below feeds the cistern".
- Self-check generator báo "KẸT: Plague_Eye" = báo nhầm (boss lơ lửng, bị ghim trên hố có solid). Nên cho self-check bỏ qua spawn `boss: True`.
- Ghi chú test: F5 qua công cụ điều khiển máy không tới được Unity (bấm ra đòn đánh) → giải câu đố bằng tay. Kéo khung panel trong lúc Play làm lệch toạ độ chuột của Game view → không kéo layout khi đang Play.
- Còn lại: AI boss Plague Eye (3 chiêu: loạt đạn mắt toả tròn, tia quét 360° bị cột chặn, xoay cận chiến), chiêu bong bóng độc của chuột; rương ở Office chưa có hoạt ảnh mở; Office/ctrl hơi tối.

## 24. Boss Plague Eye + chuột độc + đánh bóng Map 2 (15/10/2026 tối)
- Script mới: `Scripts/Enemies/PlagueEyeSkills.cs` (bộ chiêu boss), `Scripts/Combat/PoisonCloud.cs` (mây độc). Sửa: EnemyAI (kind `Beholder` + hàm public cho bộ chiêu), Projectile (Ignore() + mây độc khi vỡ), PlayerCombatPrototype (sự kiện `SkillCast`, F6 = F5 giải mọi câu đố — F5 không tới được Unity qua công cụ điều khiển), CameraFollow (`SetZoom`/`ClearZoom`: lùi camera mà vẫn theo nhân vật), MapMechanics (bong bóng sắp xếp chiều sâu theo người chơi), Map2EnemyBuilder. Bản cũ: `Catalog/v2/maps/_in/u59_backup/`.
- Plague Eye (kind Beholder, ghim trên hố): 
  1. Eye Volley (atk2): 2 vòng 14 đạn mắt (pha 2: 3 vòng xoáy, 18 đạn). Đạn bỏ qua khối rắn của hố (Projectile.Ignore), vỡ khi chạm cột/tường.
  2. Death Ray (atk1): vạch đỏ báo 1,3 s rồi tia quét 360° (80°/s, 4,2 s; pha 2: 2 tia đối nhau, nhanh hơn). Tia dừng ở cột/tường (Raycast) -> núp sau cột. Tia = bản sao `Assets/Enemies/Map2/FX/eye-beam-loop.png` (KHÔNG cắt lại ảnh gốc vì tileset của map dùng nó).
  3. Spin (atk3): người chơi trong 2,3 đv quanh hố -> vòng đỏ 0,8 s, xoay 1,6 s (bán kính 2 đv, chỉ cú đầu đẩy mạnh), rồi choáng 1,4 s (cửa sổ phản công).
  - Đấu trường: camera lùi x1.45 khi boss bắt đầu, trả lại khi chết.
- Bloated Rat: bong bóng (5 khung bay / 4 khung vỡ) vỡ ra mây độc 4 s (popping 15 / loop 8 / fading 12 khung 200x110). Có sách Purifier (cờ book_purifier) thì kỹ năng F xoá mây.
- Map: hòm Office mở khi nhặt chìa (toggle pump_chest -> pump_chest_open), đèn key_glow tắt; thêm 2 đèn Office + 2 đèn Pump Control (scene: 46 đèn), thêm bàn ở Office (dời Spider sang (101,72)). Biển r8_shield đổi lời (đúng cả trước/sau khi chuyển dòng) — generator v5n3 ĐÃ chép nhưng CHƯA chạy lại (người dùng đang Play): chạy `python maps/map2_sewers.py` rồi Ctrl+R.
- Đã test: F6, hòm mở, Pump Control/Office sáng hơn, vòng cảnh báo Spin, vạch đỏ + tia quét, camera lùi/trả, pha 2 gọi quái, hạ boss -> r8_cleared. Console 0 lỗi lúc dựng. Chưa nhìn kỹ: tia bị cột chặn, loạt đạn mắt, mây độc của Bloated Rat.
- (19:40) Đạn mắt gốc chỉ 24 px nên gần như không thấy -> PlagueEyeSkills phóng đạn x2.4 (hitbox theo), tô sáng, vẽ trên thân boss.
- Bỏ quân tiếp viện pha 2 (đã xoá 2 spawn `spawn_when: boss_phase2` khỏi generator v5o). Thay bằng chiêu 4 "Toxic Brood": cứ 20 giây
  (đếm từ lúc boss bắt đầu đánh) boss gồng, hiện chữ "The Plague Eye spawns its brood!", mây độc phồng lên ở các điểm quanh hố
  (bỏ điểm vướng vật cản) rồi chui ra 2 Plague Rat + 1 Bloated Rat; tối đa 6 con còn sống. Boss chết -> lũ được gọi chết theo.
  Director có hàm mới `SpawnSummon(enemy, pos, owner)` (thêm quái vào khu của boss). Hint "calls for reinforcements" chỉ hiện khi khu còn spawn pha 2.
- Đã test: thấy loạt đạn mắt, triệu hồi đúng, mây độc, tia quét; Console 0 lỗi / 0 cảnh báo.

## 25. Lỗi cầu ván Feed -> Office không đi được (15/10 tối)
- Nguyên nhân: collider riêng của kênh A (`acid A colliders`, Unity chỉ tắt khi ichor_diverted) KHÔNG trừ các ô `walk_rect` của cầu
  -> trước khi chuyển dòng, cầu (x91-95, y67-69) bị chặn. Self-check không bắt được vì nó coi kênh A là đi được.
  Sửa trong `sewerkit.py` (v5e, md5 6da48695…): `block_a = self.block_A - self.walk`.
- Người dùng lại sửa .tmx trong Tiled (lưu 19:48): chạy trim_props.py (60 đồ vật đổi sang SR-props-trimmed.tsx) và kéo hố / Con Mắt /
  khiên / đèn hố / trigger trận boss / khu r8 / collider bể. Bản gốc lưu ở `maps/_in/map2_sewers_USER_EDIT_2048.tmx`.
  Generator v5p2 (md5 4f0e92d3…) giờ tự áp: mục 15 `USER_MOVES` (toạ độ px y hệt bản Tiled) + tự chạy `trim_props.py` sau khi dựng.
  So với bản của người dùng: chỉ khác collider kênh A ở cầu (đúng chủ đích).
- LƯU Ý: chạy generator với `--out` khác vẫn ghi đè các SR-anim-*.tsx trong project (nội dung không đổi) — tránh dùng --out để thử.
- Muốn sửa map trong Tiled: sửa xong báo lại để chép vào USER_MOVES, nếu không lần dựng sau (--force) sẽ mất.

## 18. Iaido đủ 8 hướng (16/10)
- Thêm `iaido/north-west` và `iaido/north-east` (14 khung, 140x140, neo (70,19 từ đáy), cùng nhịp ms). Menu
  "Dựng Animator nhân vật" báo Iaido 8 hướng; đã chạy và Ctrl+S ở CẢ HAI scene Map1_GrassLands và Map2_Sewers
  (danh sách `directions` của PlayerCombatPrototype nằm trong từng scene -> scene mới phải chạy lại menu).
- Đã xem clip Iaido_NorthWest / Iaido_NorthEast bằng Animation preview (khung đúng, chân trên collider); Play 0 lỗi.
- Bài học khi dựng hướng quay lưng (NW/NE), dùng cho các animation sau:
  - Tay phía xa/gần trong idle là một dải nằm ngoài đường may tối của áo: chỉ bỏ đúng dải đó (và bàn tay), rồi
    đắp lưng áo bằng vải phía TRONG thân (tránh đường may), không cắt cả mảng -> thân không bị hẹp/mất nửa.
  - Bao kiếm: dùng lại pixel gốc (không vẽ ống), tách thành lớp riêng treo theo hông (dịch theo dy khi hạ hông),
    loại khỏi vùng kéo chân và không cho "dồn thân" kéo xiên.
  - Chân: chân gần camera chỉ dịch ~3-4 px, dồn vào đúng khung chém; độ nghiêng trải từ hông tới đế (không gãy
    khúc); giữ vạt áo liền khi hai chân dang; bàn chân luôn ở hàng 120 (không nhích lên/xuống).
  - Chi phía xa camera rút ngắn theo phối cảnh (tay 11+10, kiếm 17-18 thay vì 15+13, 21).
- Script dựng (container): dir_nw2.py, dir_ne2.py (+ iaido_lib.py).

## 26. Boss lệch lên + hố song gãy + bản vá chỉnh tay (17/10)
- Boss lệch lên: điểm sinh `Plague_Eye` là CHÂN boss (pivot sprite 0.48/0.135, x3), thân nằm cao hơn ~2,7 ô. Bản Tiled (15/10) kéo điểm sinh
  vào tâm hố nên boss nổi lên ~3 ô. Giờ điểm sinh = chân Con Mắt "đang uống" người dùng đã căn: ô (93.72, 41.62) = px (2999, 1331.8).
  PlagueEyeSkills có `groundOffset` (0, 0.6): tâm vòng xoay / gốc tia / điểm gọi quái = tâm hố, không phải chân sprite.
- Hố: `pit_grate` (giant pipe-sheet2, song nguyên) + `pit_broken` (giant pipe-sheet3, song bị bẻ cong); toggle `pit_break` khi ichor_diverted.
- Chỉnh tay Tiled (15–16/10: tô lại tường ở R3/hub/feed, cột to collider hơn, collider hố mới, dời trigger trận boss, xoá 3 đồ vật, cầu feed...)
  giờ lưu ở `Catalog/v2/maps/map2_user_patch.json`, generator tự áp sau khi dựng (+ tự chạy trim_props.py). Công cụ: `Catalog/v2/tmx_userpatch.py`.
  Khi người dùng sửa Tiled lần nữa: (1) chép bản Tiled ra _in/, (2) dựng bản thô vào thư mục tạm có `TiledMap Editor/Tilesets/SR-props-trimmed.tsx`
  + symlink `Props` (xem lệnh trong lịch sử 17/10), `--raw --out`, (3) `tmx_userpatch.py capture raw user patch.json`, (4) bỏ các mục do
  đánh số tự động (pillar_/torch_ trong mechanics) và những gì generator đã đổi chủ đích, rồi `--force`.
- Tên đèn tự đánh số (pillar_47.., torch_51..) đã lệch 1 vì thêm mech pit_break -> PHẢI chạy lại "Dựng scene Map 2".
- Bản Tiled 16/10 lưu ở `maps/_in/map2_sewers_before_v5q.tmx`. Generator v5r md5 7528cfb3…, PlagueEyeSkills md5 a1b704d2….

## 27. Tia máy, Con Mắt sau kính Ichor, đáy kênh cạn, nhịp gọi quái (17/10 sáng sớm)
- Tia `beam_converter`: gốc = điểm người dùng tự đặt trong Tiled (ô 103.15, 54.23 — ống vào bình thứ nhất); trước đây kéo object mech không có tác dụng
  vì tia đọc thuộc tính `src`. `beam_feed` dst theo Con Mắt người dùng căn (93.96, 40.67). Ống ngang của bình nằm ở y ảnh 96 -> ô 54.89 nếu muốn hạ gốc tia đúng thân ống.
- Shield có `veil="boss_idle_fx"`: vật trong bong bóng ám xanh, trong mờ ~50%, gợn ngang (MapMechanics, trả lại khi bong bóng vỡ). Lõi bong bóng đặc hơn (0.18).
- Kênh A khi cạn: lớp mới `A floor` (giữa `curb A dry` và `curb A`) = đá cuội Terrain cột 26-28 hàng 2-4 (khối 3x3 "ground tiles designed for water on top"),
  chỉ ở ô ngập trọn 4 góc. Collider `acid_A` KHÔNG còn tắt khi chuyển dòng (kênh cạn là hố sâu). sewerkit v5f md5 d78459f8….
- Toxic Brood: 30 giây/lần và CHỈ khi đợt trước bị hạ hết (`BroodCleared()`).
- Bản vá chỉnh tay đã chụp lại (17/10 02:08: collider hố, 2 collider cầu trên/dưới ván, tia/đèn máy): `maps/map2_user_patch.json` (bản cũ `_in/map2_user_patch_v1.json`).
  Bản Tiled 17/10 lưu ở `maps/_in/map2_sewers_before_v5s.tmx`. Generator v5s2 md5 c0f1aae7…. Công cụ xem trước: `Catalog/v2/tmx_render.py`.
- Đã test trong Unity: Con Mắt mờ xanh trong bong bóng, tia máy chạm bình, kênh cạn thành đá cuội và không bước xuống được, gọi quái lần đầu ~30 s,
  vòng xoay giờ quanh tâm hố.
- (02:40) Người dùng chỉ rõ: tia máy gắn vào ĐẦU ống ngang ở mép bể -> PIPE_OUT = (100.2, 54.9) (ảnh lật x2, px 7,96). Bỏ 2 mục dời
  beam_converter / converter_glow khỏi bản vá (đèn giờ ở miệng ống). Generator v5s3 md5 348c0cf4….

## 28. Map 2 v6 – thiết kế lại toàn bộ (ĐỀ XUẤT, chờ duyệt)
- Tài liệu: tab "Redesign v6 – full map" trong doc https://claude.ai/code/artifact/9a0b13b6-0113-4ae4-8125-fa828876d2eb
- Layout 128x108, hub Sluice Hall + 3 cánh: Giả kim (Lab → Crank), Sinh học (Culture Hall, mini-boss Gorgemaw = FrogBoss → pump_key), Kỹ thuật (Boiler Room, mini-boss Ichor Ram = RhinoMonster_09_Radioactive → steam_pressure). Console Pump Control cần cả 3 flag.
- Quái mới đề xuất: Skinned Hopper (FrogMonster 48x48), Bloat Fly (Monsterfly_01 85x99), Barrel Goblin (Goblin_Barrel 38x26), Mimic (Mimic_LVL1-4 29x38). Boss ẩn tuỳ chọn Specimen Zero (CRAWLING DEPTHS Eldritch Entity 160x208, tím xám → chỉ dùng trong hầm kín).
- Bản vá Tiled hiện tại (map2_user_patch.json) KHÔNG áp được lên layout mới.
- Câu hỏi chờ: file mới map2_warren.tmx hay ghi đè; làm Vault ngay hay sau; loại phần thưởng cho phòng tuỳ chọn.
- Contact sheet quái: Catalog/v2/out/monsters/mega.png, crawl.png

## 29. Map 2 v2 – ĐÃ DỰNG map2_sewers_v2.tmx (17/10)
- Generator: Catalog/v2/maps/map2_sewers_v2.py (chạy: `python maps/map2_sewers_v2.py --force` trong Catalog/v2). Map cũ map2_sewers.tmx KHÔNG đổi.
- 128x108. Lõi cũ dời +14 ô sang đông bằng `at(14,0)` (mọi toạ độ trong khối lõi viết theo bản cũ). Cánh mới viết toạ độ tuyệt đối (`at(0,0)`).
- Bản vá Tiled riêng: maps/map2_v2_user_patch.json (tự tạo lần đầu từ map2_user_patch.json, dời +448 px). Áp 67 ô/16 dời/3 xoá/1 thêm, khớp hết.
- Stamp: Catalog/v2/out/map2_sewers_v2.tmx.gen.md5 (kiểm như map cũ trước khi dựng lại).
- Console cần `lever_handle,pump_key,valve_1,valve_2,valve_3`; đèn khe sock_crank/sock_key/sock_pressure (lit_when). Cửa Pump Control mở sẵn (bỏ ctrl_door).
- pump_key: pickup trong Culture Hall có `requires=cult_cleared` (MapEncounterDirector: pickup hỗ trợ requires/locked_text — mới), hình chìa (Props/SR-extra2/SR pump key.png, vẽ thủ tục) hiện bằng toggle when cult_cleared.
- Van gỉ Boiler: tạm là `breakable` (chém 4 lần) -> cờ valve_n. Sau này Ichor Ram mới phá được.
- Quái chưa có prefab (Director chỉ cảnh báo): Brood_Toad, Ichor_Ram, Skinned_Hopper, Barrel_Goblin, Mimic. Có quái cũ thế chỗ trong Culture Hall / Boiler.
- Cổng chờ code: records_gate (records_open – khoá số), cell_1..6 (cell_n_open – cần gạt). vault_gate mở khi ichor_diverted (Vault chỉ trang trí, chưa có boss).
- mapkit.anim(): đã escape đường dẫn ảnh trong .tsx (gói "Structures & Details" có dấu &).
- Unity: menu mới "Soul Requiem ▸ Dựng scene Map 2 v2 (Malric's Warren)" -> Assets/Scenes/Map2_Sewers_v2.unity (sao từ Map2_Sewers.unity). Map2SceneBuilder.cs đã tách BuildScene().
- CHƯA test trong Unity (17/10 03:45 Unity editor đang đóng).

## 30. Map 2 v2 THAY Map 2 + cơ chế mới (17/10 ~04:40) — đã test trong Unity
- Map2SceneBuilder: menu "Dựng scene Map 2 (Sewers)" giờ dựng map2_sewers_v2.tmx vào Map2_Sewers.unity (tên object vẫn map2_sewers).
  Bản cũ: menu "Dựng scene Map 2 (bản cũ)". (Đã bỏ scene Map2_Sewers_v2 riêng.)
- EnemyAI: EnemyKind.Custom + interface IEnemySkills (Scripts/Enemies/MonsterSkills.cs). PlagueEyeSkills cũng implement IEnemySkills.
  Helper công khai: Desired, GapToPlayer(), HitPlayerNow(), FaceX(), Rb, BodyCollider, Airborne (bỏ qua FixedUpdate khi nhảy).
- MonsterSkills.cs: LungeSkills (Skinned_Hopper, Barrel_Goblin, Mimic), ToadSkills (Brood_Toad: Hop/Leap Slam/Burp/Brood pha 2),
  RamSkills (Ichor_Ram: vạch cảnh báo -> lao, đụng tường -> MapMechanics.RamImpact + choáng; vệt mây độc; pha 2 lao 2 lần).
- Map2EnemyBuilder: 5 Def mới từ CHARACTER MEGAPACK (FrogBoss x2.1, FrogMonster x1.6, Rhino_09 x2, Goblin_Barrel_01 x1.8, Mimic_LVL2 x1.8)
  + đạn Toad_Glob. Pivot chân ~0.02 (Rhino 0.111).
- MapMechanics mới: lever (cần gạt buồng giam; sai -> SpawnInZone), dial (khoá số nhóm, mỗi nhát chỉ xoay vòng gần nhất),
  valve (gỉ tới khi RamImpact), tank (Shatter khi cóc đập). MapEncounterDirector.SpawnInZone mới.
- Mã khoá Records Vault: 3-7-4 (ghi chú: nhật ký Lab "I - three", sổ cái Office "II - seven", biểu đồ Specimen "III - four").
- Buồng đúng: số 4 (có bình máu). Barrel_Goblin vào trận khi Ram còn 50% (spawn_when=phase2).
- Bể nuôi cấy lớn ở Culture Hall đặt lớp "ground decals" (ảnh động không được trim_props cắt đáy -> ở props sẽ che người chơi).
- Test: F3 qua các khu, cóc nhảy đập/đánh dấu bóng đổ OK, Ram lao + vạch cảnh báo + choáng OK, Console 0 lỗi.
  CHƯA test tay: cần gạt buồng giam, khoá số, vặn van sau khi Ram phá gỉ, vỡ bể kính, nhặt chìa khoá, cả map chơi liền mạch tới Map 3.

## 31. Sửa lỗi đồ vật đè lên nhân vật ở Map 2 v2 (17/10 12:30) — đã kiểm trong Unity
- Nguyên nhân 1 (chính): map2_sewers_v2.tmx.meta do Unity tạo lần đầu có `sortingMode: 0` (Stacked) -> mỗi object +1 thứ tự,
  đè lên nhân vật. Đã đổi `sortingMode: 1`. map2_sewers_v2.py giờ tự sửa .meta mỗi lần dựng (MỌI .tmx mới đều phải kiểm).
- Nguyên nhân 2: ảnh ĐỘNG (tileset SR-anim-*) không được trim_props cắt dải trong suốt dưới chân -> điểm xếp lớp thấp hơn
  chân thật (máy hơi nước 41 px, đuốc 16 px, Con Mắt 13 px...). Công cụ mới `Catalog/v2/trim_anims.py`: cắt dải đó
  -> Props/SR-trimmed-anim/*.png + tileset SR-animT-*.tsx, dời y/height object để hình không đổi. Generator chạy nó SAU bản vá.
- Lưu ý regex: .tmx sau khi tmx_userpatch ghi lại có dạng `<tileset ... />` (có dấu cách) — trim_props.py (regex `"/>`)
  vì thế KHÔNG làm gì ở lần chạy lại; ảnh tĩnh vẫn đúng vì đã cắt từ trước. trim_anims.py chấp nhận cả hai dạng.
- Bể nuôi cấy lớn (Culture Hall) + máy chuyển hoá (Cistern): base + bình Ichor đặt lớp "ground decals" (dựa sát tường,
  người chơi không đứng sau) để bình luôn vẽ trên thân máy.
- Kiểm: đặt Player sau/trước bình thí nghiệm và máy hơi nước ở Lab -> vật che đúng khi đứng sau, nhân vật đè đúng khi đứng trước.

## 32. Map 2 v2: cửa khoá phòng bơm + bàn điều khiển báo thiếu gì (17/10 13:10)
- Pump Control lại KHOÁ: gate `ctrl_door` (song sắt, hành lang hub -> Pump Control) mở bằng cờ `pump_key`
  (chìa PUMP ROOM của đốc công: Gorgemaw nuốt, rơi ra khi dọn Culture Hall). Lối tắt Culture Hall -> Pump Control
  (`cult_short`) cũng mở bằng `pump_key`. Biển "hub_south" ở cửa: PUMP ROOM - KEY HELD BY THE OVERSEER.
- Bàn điều khiển chỉ cần 2 thứ: `lever_handle` (tay quay, Lab) + `valve_1..3` (áp suất, Boiler Room). Đèn khe: sock_crank, sock_pressure.
- MapMechanics console: prop `needs` = "cờ,cờ=mô tả|..." -> khi chém/đứng gần mà chưa đủ, báo đúng thứ CÒN THIẾU
  ("The console will not wake. Still missing: ..."). Đã thử trong Unity.
- Self-check: thêm ctrl_door vào gate_order, khu ctrl ở stage riêng.

## 33. Map 2 v2: tiến trình mới + khoá ký tự Vael + boss ẩn Specimen Zero + thành tựu (17/10 18:30) — đã test trong Unity
**Tiến trình (thay mục 32, bỏ hẳn 3 van gỉ):**
- Lab (r6): nhặt tay quay `lever_handle`. Bàn điều khiển Pump Control CHỈ cần tay quay (`CONSOLE_NEEDS = "lever_handle"`).
- Specimen Cells: cửa Culture Hall `cult_door` (song sắt, đèn `cult_door_lamp`) KHOÁ bằng cờ `vat_key`.
  Rương báo cáo (3.4, 68.4) ngay trước cửa: note `incident_report` (đặt cờ `report_read`, rương chuyển sang mở) -> chìa ở chỗ Brakk, Boiler Room.
- Boiler Room: hạ Ichor Ram (+ đợt quái còn lại) -> `boil_cleared` -> chìa `vat_key_item` hiện trên xác Brakk (111, 90.4).
- Culture Hall: hạ cóc Gorgemaw -> `pump_key` (17, 88.6) -> mở `ctrl_door` (cửa phòng bơm, đèn `ctrl_door_lamp`) + lối tắt `cult_short`.
- Ô nhặt chìa (vat/pump/vault) nới thành 2.4 x 2.4 ô cho dễ nhặt.

**Khoá ký tự Vael (Records Vault, mech "cipher"):**
- Bảng lớn OnGUI hiện khi đứng ở vùng khoá; 3 ô ký tự, bấm mũi tên lên/xuống; `MapMechanics.PanelOpen` chặn chuột tấn công.
- Số Vael hệ 8, ký tự lấy từ Crypt `Props/ground-runes.png` (`glyphpal_0..7`, GLYPH_TILE trong generator).
  Đáp án "3,7,4": tờ r6_journal (Seal I = 3), office_ledger (Seal II = 7), spec_chart (Seal III = 4). Mỗi tờ vẽ ký tự của chữ số đó.
- Giải xong -> `records_open` -> phòng hồ sơ: chìa `vault_key` + hồ sơ `rec_dossier` "TOP SECRET - Project ORIGIN".
- Note/cipher là mech "note"/"cipher" (panel to giữa màn hình, tự đóng khi rời vùng).

**Specimen Zero (boss ẩn, Containment Vault, cổng `vault_gate` mở bằng `vault_key`):**
- Script `Assets/Scripts/Enemies/SpecimenZeroSkills.cs`: đứng yên (floating), xúc tu dưới chân người chơi (GroundStrike,
  `bodyScale` 1.5) + vòng đạn tím từ mắt (Eye_Bolt tô tím, x2, bỏ qua collider của chính nó).
  Xích `leash` 6.5: người chơi ra khỏi phòng thì ngừng đánh. Đứng sau thân -> thân mờ 45%.
  Bỏ viên đạn sinh ngay trên người chơi (trước đó cả vòng trúng cùng 1 khung = chết xuyên God mode).
- Chết -> cờ `specimen_zero_slain` + `Achievements.Unlock("breaker_of_seals", "Breaker of Seals")`.
- `Assets/Scripts/Core/Achievements.cs` (bản tạm): PlayerPrefs `ach_<id>`, hint "ACHIEVEMENT UNLOCKED: ...", event `Unlocked`.
  Lưu ý: đã test nên máy này đang có `ach_breaker_of_seals = 1` (xoá PlayerPrefs nếu muốn thử lại "lần đầu").

**LỖI QUAN TRỌNG đã sửa:** LungeSkills/ToadSkills/RamSkills/SpecimenZeroSkills từng nằm chung `MonsterSkills.cs`
-> Unity KHÔNG gắn được script vào prefab (`m_Script: {fileID: 0}`) -> mọi trường gán trong builder (khung xúc tu,
globPrefab của cóc, mây độc của Ram...) bị mất, quái chạy bằng giá trị mặc định. Đã tách mỗi class MonoBehaviour ra file
cùng tên; `MonsterSkills.cs` giờ chỉ còn interface `IEnemySkills`. QUY TẮC: mỗi MonoBehaviour một file trùng tên class.
Sau khi tách phải chạy lại "Dựng quái Map 2" (đã chạy; prefab giờ có m_Script đúng).

**Khác:**
- `MapMechanics.InitBeam`: tắt renderer của mẫu `beampal` (trước đó hiện ở góc map).
- F3 không quay vòng: tới khu cuối (vault) thì dừng. Bị đưa về The Hatch = người chơi đã chết (respawn).
- Test F3 bỏ qua khu thì quái các khu trước vẫn đuổi theo xuyên tường (cóc nhảy, Ram lao) — chỉ là hệ quả của F3.
- Generator báo "KẸT: Plague_Eye" — Con Mắt lơ lửng trong khối rắn của hố, như cũ, bỏ qua.
- CHƯA test liền mạch bằng đi bộ thật: toàn bộ chuỗi Lab -> rương báo cáo -> Boiler -> Culture Hall -> Pump Control -> cipher -> Vault.

## 34. Dọn map cũ (21/10)
- ĐÃ XOÁ khỏi Assets: map1_grassland.tmx, map1_grassland_v2.tmx, map1_grassland_v3.tmx, map2_sewers.tmx (+ .meta).
  Bản sao lưu: `Catalog/backups/old_maps_2026-10-21/` (kèm Map2SceneBuilder.cs.bak). Muốn dùng lại: chép cả .tmx lẫn .meta về chỗ cũ.
- Bỏ menu "Dựng scene Map 2 (bản cũ)" và hằng MapPath trong Map2SceneBuilder.cs.
- Map đang dùng: map1_greystone.tmx (Map1_GrassLands.unity), map2_sewers_v2.tmx (Map2_Sewers.unity). Map 3, Map 4 chưa có.
- Lưu ý: generator cũ (map1_v2.py, map1_v3.py, map2_sewers.py) chạy lại sẽ sinh lại các file này — đừng chạy.
- Đích Map 3 trong lối ra Map 2 vẫn là map3_vale (đổi khi làm Map 3).

## 35. Đổi tên map theo tên vùng trong game (21/10)
| Map | File Tiled | Scene | Tên đích (target) | Tiêu đề trong game |
|---|---|---|---|---|
| 1 | `map1_greystone_plains.tmx` | `Map1_GreystonePlains.unity` | `map1_greystone_plains` | Greystone Plains – The road home |
| 2 | `map2_undercity_of_vael.tmx` | `Map2_UndercityOfVael.unity` | `map2_undercity_of_vael` | The Undercity of Vael – Beneath the Stone Ring |
- Đổi bằng cách đổi tên cả file lẫn .meta (giữ GUID) lúc Unity đóng. Đã sửa: Build Settings, SceneTransition (giữ tên đích cũ
  map1_greystone/map2_sewers làm bí danh), Map2SceneBuilder (SrcScene giờ là Map1_GreystonePlains — SampleScene không còn),
  Map2EnemyBuilder, lối ra Map 1 trong .tmx, tên object map trong 2 scene, --out/preview của generator, stamp out/<tên mới>.tmx.gen.md5.
- Generator vẫn tên cũ: `maps/map1_greystone.py`, `maps/map2_sewers_v2.py` (ghi ra file tên mới). Cả 2 .tmx đã sửa tay -> dựng lại cần --force.
- Mọi mục trước đây nhắc map1_greystone.tmx / map2_sewers_v2.tmx / Map1_GrassLands / Map2_Sewers / SampleScene = các tên mới ở bảng trên.
- Sao lưu trước khi đổi: `Catalog/backups/rename_maps_2026-10-21/`.

## 36. Map 2: cổng vòm cống + bỏ cổng nhà tù (22/10) — CHƯA test trong Unity
- .tmx là bản gốc (sửa thẳng, KHÔNG chạy lại map2_sewers_v2.py --force: sẽ mất các sửa này). Sao lưu trước khi sửa: `Catalog/backups/map2_gates_2026-10-22/`.
- Ảnh mới (Props/SR-extra): `SR sewer arch dry.png` (vòm vuông 4x3 ô = Terrain id 2680-2683/2745-2748/2810-2813, đã đổi vệt Ichor xanh ở chân về màu gạch),
  `SR portcullis closed.png` (song sắt kín lòng vòm, lòng vòm px 27..100, màu lấy từ gate1). Tileset `tilesets/SR-gates.tsx` (firstgid 14233 trong map2).
- 5 cổng: r2_gate, r3_gate, ctrl_door, records_gate, lối ra Culture Hall (cult_door + cult_lock CHUNG 1 vòm ở (6,77)).
  Mỗi cổng: bỏ bo tròn 2 đầu tường ở 3 hàng mặt tường; vòm = tile object `<gate>_arch` lớp props (đáy = chân tường);
  2 collider chân vòm `<gate>_arch_L/R` (obstacle colliders); vùng chặn cổng dời xuống hàng ngay trên chân vòm, rộng 74 px; hình cổng = SR portcullis.
- Gói Sewers KHÔNG có song sắt kín: gate1/2/3 chỉ là khung (giữa trống) và nằm cách sàn 12 px. Khối vòm trong tileset là bản kênh nước (chân có Ichor).
- Cửa Crypt (doors2) rộng ~55 px + khung = 3 ô; khe tường Map 2 = 4 ô -> không dùng (hở). Cửa tường Crypt có gạch xanh lam -> lệch màu.
- Nhà tù (Specimen Cells): XOÁ cổng cell_1..6. Câu đố giữ: cần gạt sai -> quái nhảy ra; `cell_4_potion` cần cờ `cell_4_open`
  (locked_text "The chest is chained shut. The chain runs to Lever 4."). Lời cần gạt đổi: không còn nhắc song sắt.
- Chưa làm: boil_lock (giữa hành lang, không có tường ngang) và các cổng tường dọc vẫn song sắt cũ. Lối ra Culture Hall: tường phải thấp hơn trái 1 hàng (thiết kế cũ).
- Công cụ: script áp ở container (Catalog/v2/tools_gates/: apply_gates.py, make_assets.py, gate_render.py), ảnh xem trước Catalog/previews/gates_map2/ (after/ = sau khi sửa).

## 37. Map 2: dọn dẹp trang trí (bước 1 phương án trang trí, 22/10) — đã test trong Unity
- Phương án đầy đủ theo từng khu: tab "Trang trí theo khu (22/10)" trong doc Map 2 (claude.ai artifact 9a0b13b6…).
- Sao lưu trước khi sửa: `Catalog/backups/map2_cleanup_2026-10-22/`. Công cụ: `Catalog/v2/tools_decor/`
  (`render_full.py` vẽ .tmx gồm cả object + xuất danh sách object; `apply_cleanup.py` + `cleanup.json` = danh sách đã áp).
- Đã xoá 131 object trang trí không mục đích: mạng nhện dập khuôn (giữ ở The Hatch + Forgotten Store), vụn đá/xương/ván
  rải giữa sàn, rương đóng giả đồ nhặt, thùng gỗ lẻ (giữ cụm góc TN Ichor Well, góc TN Sluice Hall, Forgotten Store).
  Giữ: xác Brakk (Boiler), đống đổ nát dưới miệng hầm (The Hatch), mọi object có tên (cơ chế/chìa/palette).
- Culture Hall: 8 vũng neon -> 3 vũng sát chân bể (dời id 377/406/407, xoá 5 cái).
- Boiler Room: xoá dải lưới thừa lớp `grates` ô (94–115, 87–89).
- **LỖI ĐÃ SỬA (có từ trước, không do dọn dẹp):** .tmx có 7 lớp logic bị ẩn `visible="0"` (zones, spawns, triggers,
  obstacle colliders, water colliders, blockers, mechanics) -> ST2U bỏ qua -> trong game 0 khu, 0 trigger, 0 cơ quan,
  người chơi không về điểm xuất phát. Đã bật lại (giữ opacity 0.35). Trong Tiled muốn ẩn thì bấm con mắt rồi BẬT LẠI trước khi lưu.
- RamSkills: vạch cảnh báo lao (RamChargeLine) còn kẹt trên sàn nếu Ram chết giữa lúc gồng -> giờ tự xoá khi Ram chết/tắt.
- Bước tiếp theo của phương án: Ichor + sàn theo vùng, rồi ánh sáng theo vùng, rồi điểm nhấn từng khu.

## 38. Đổi bộ chiêu 2 mini-boss Map 2 (22/10) — đã test trong Unity
Nguyên nhân chiêu "lạ": sau khi tách file (mục 33) prefab mới nhận đúng dữ liệu builder gán — cóc bắn đạn bằng ảnh
BONG BÓNG của chuột đột biến (Toad_Glob = rat bubble), Ram rải MÂY ĐỘC của chuột dọc đường lao.
- **Gorgemaw (ToadSkills.cs)**: Hop (đáp đất văng 6 giọt Ichor + gợn sóng) · Tongue (gần ≤ 2,3: vạch hồng bám rồi khoá,
  lưỡi phóng/thu) · Spit (xa > 1,6: ợ, phun 3/5 cục nhớt bay vòng cung, vòng cảnh báo xanh, rơi toé) · Leap Slam (giữ) ·
  Brood pha 2 (giữ). Bỏ Burp mây độc.
- **Ichor Ram (RamSkills.cs)**: bỏ vệt mây độc. Đâm tường -> đá trần rơi 3 (pha 2: 5) tảng: vòng cảnh báo vàng đất,
  tảng đá rơi thẳng từ trên, để lại đá trên sàn 2,5 s.
- FX mới (tự vẽ, bảng màu Ichor/đá của gói Sewers) trong `Assets/Enemies/Map2/FX/`: `ichor-glob.png` (4×14 px, PPU 70),
  `ichor-glob-pop.png` (4×16), `rubble-chunk.png` (3×14, PPU 50), `ichor-splash.png` (bản sao "water ripples by impact").
- GroundStrike: thêm `bodyColor`, `shellScale`, `leaveSprite/leaveSeconds` (mảnh để lại, dùng `FadeAndDie.cs` — file riêng).
- **Health (người chơi): `hitIFrames` = 0,25 s** — sau mỗi đòn trúng bỏ qua đòn mới 0,25 s. Trước đó cả vòng đạn sinh ra
  trên người (cóc đáp trúng người, Specimen Zero) trúng cùng 1 khung = chết tức thì xuyên God mode.
- MapEncounterDirector.DebugNextZone: map 0 khu (lớp logic bị ẩn) -> báo thay vì lỗi IndexOutOfRange.
- Khi test F3 bỏ qua khu, cóc/Ram của khu trước vẫn đuổi theo sang khu khác — chỉ do F3, chơi thật cửa khoá giữ lại.
- **Tiled lưu đè lúc 02:37 (22/10)** làm mất dọn dẹp + ẩn lại lớp logic -> đã áp lại lên bản người dùng lưu
  (giữ chỉnh sửa của người dùng). Bản người dùng lưu: `Catalog/backups/map2_cleanup_2026-10-22/map2_user_save_0237.tmx`.

## 39. Gorgemaw: bỏ hẳn giọt/cục nhớt "bong bóng độc", làm lại lưỡi ếch (22/10) — đã test trong Unity
- Theo yêu cầu người dùng: bỏ mọi đạn nhớt xanh của cóc (vòng giọt khi nhảy/nhảy đập, chiêu Spit).
  Thay bằng **Shockwave** (`Scripts/Enemies/Shockwave.cs`): vòng sóng dẹt nở ra từ chỗ tiếp đất, trúng 1 lần khi mép
  vòng đi qua người chơi (lăn để né). Nhảy thường: bán kính 1,1 (pha 2: 1,5). Nhảy đập: 2,6, pha 2 thêm sóng thứ hai 3,4 sau 0,35 s.
- **Lưỡi (Tongue)**: phồng họng (anim Burp) + vệt nhắm mờ và chấm đích nhấp nháy, khoá hướng 0,15 s trước khi phóng;
  lưỡi là LineRenderer thuôn dần (texture lát cắt 8 px hồng/viền đỏ sẫm, shader Sprite-Lit-Default), đầu lưỡi tròn;
  phóng ra 0,13 s (nhanh rồi chậm, đoạn đầu uốn cong rồi duỗi), giữ 0,08 s, thu về 0,2 s; trúng thì quất + KÉO người chơi
  về phía miệng. Tầm `tongueRange` 2,6, độ dày 0,16.
- Trường globPrefab/globShell vẫn giữ trong ToadSkills (không dùng) để builder và prefab không lỗi.
- Culture Hall: 3 decal bong bóng động (id 379/405/408) còn nằm giữa sàn sau khi dời vũng -> dời vào trong 3 vũng.

## 40. Tờ ghi chú khoá ký tự: chỉ rõ ký tự nào ở ô nào (22/10) — đã test trong Unity
- Bỏ lối "số Vael hệ 8" khỏi lời văn: 3 tờ (r6_journal, office_ledger, spec_chart) giờ nói thẳng "wheel I/II/III của khoá
  Records Vault" và vẽ SƠ ĐỒ KHOÁ 3 ô (I, II, III): ô của tờ đó có ký tự, 2 ô kia là "?".
- Thuộc tính mới của note: `seal = "ô:ký tự[:số ô]"` (vd `1:3`). MapMechanics.DrawNote ưu tiên `seal`, vẫn hiểu `glyphs` cũ.
  Đáp án/perm của khoá không đổi (3,7,4). Hint của khoá: chỉ ra 3 nơi có tờ ghi chú.
- Sửa thẳng trong .tmx (sao lưu `Catalog/backups/map2_cleanup_2026-10-22/map2_before_sealnotes.tmx`). Generator map2_sewers_v2.py
  vẫn còn lời văn cũ — không chạy lại generator.


## 41. Map 2: trang trí bước 2–6 (Ichor, sàn, ánh sáng, điểm nhấn, đồ kể chuyện) (22/10) — đã test trong Unity
- Công cụ: `Catalog/v2/tools_decor2/` — `build.py` (toàn bộ thay đổi, chạy trên bản sao lưu `Catalog/backups/map2_decor2_2026-10-22/map2_before_decor2.tmx`),
  `dk.py` (thêm object/tileset, ảnh tự trim), `own_art.py` (vật tự vẽ), `metas.py` (.meta ảnh: Point, không nén, PPU 100, GUID cố định theo tên).
  .tmx vẫn là bản gốc; KHÔNG chạy lại build.py lên .tmx đã sửa tay (nó đọc base.tmx = bản trước bước này).
- Tileset mới: `tilesets/SR-decor2.tsx` (collection, firstgid 14235, ảnh trong `Props/SR-decor2/`, mọi ảnh đã cắt sát viền -> xếp lớp theo chân đúng)
  + 7 tileset động `tilesets/SR-animD-*.tsx` (firstgid từ 14700; ảnh `Props/SR-decor2-anim/`): nến Crypt, sương đất x2, sương nước (không dùng), bắn nước, gợn, khói Volcano.
- Ichor: bỏ ô 53 (nước đục chói) ở lớp `curb`/`curb A`; `curb water`, `curb water A`, `flood base` nhuộm `tintcolor=#4d7a60` (ST2U hỗ trợ). Trong game hồ/kênh thành nước tối;
  chỉ vòi xả, ống vỡ, bể nuôi còn xanh chói. FadeLayer (toggle ichor_divert) giữ màu nhuộm, chỉ đổi alpha.
- Sàn: cánh tây Malric (x1–33, y1–55) + văn phòng: sàn gạch xếp Terrain2 157/222/223/287 (KHÔNG dùng 288: có điểm trong suốt). Lưới kim loại kiểu 2 (lớp grates) quanh bàn điều khiển Pump Control.
- Ánh sáng: Map2SceneBuilder đọc thêm `intensity` và `inner` của object light. 23 đèn nền `fill_*` (bán kính lớn, cường độ 0.2–0.38) theo bảng màu vùng
  (cống bắc xanh xám, cánh tây vàng nến, giam/nuôi xanh vàng, khu máy cam, Cistern xanh Ichor, thánh địa xanh lam/tím). 13 đuốc chuyển sang tắt (gid 14168) + bỏ đèn tương ứng;
  hub mỗi ngả 1 màu (tây ấm, nam cam, đông xanh). PHẢI chạy "Dựng scene Map 2 (Sewers)" sau khi sửa đèn trong .tmx.
- Điểm nhấn: Gallery of Arches 4 vòm (Terrain2 ô 244-248 + lòng tối, 2 vòm có song, 2 vòm có vòi xả); tường sập nửa tây; bàn ghép đồ (sách, nến, cốc... ghép thẳng vào ảnh bàn
  để không bị bàn che khi xếp lớp); biển số buồng 1–6 (Specimen Cells); cọc neo + xích đứt G-7, xác người chăn (Culture Hall); biển INFLOW / TO DRAIN / TO CISTERN (Pump Gallery);
  biển "< MALRIC", "PUMP ROOM", "KEEP OUT"; giá móc chìa thiếu 1 chìa (Office); vạch đếm tổ Brakk, than, xẻng, khói nồi hơi (Boiler); vòng ấn Vael quanh hố (Cistern) và trong Records Vault;
  sàn đá tím CRAWLING DEPTHS + ấn tím + xích (Containment Vault); tượng, quan tài, nến, cờ (Saint's Reliquary).
- LỖI CŨ ĐÃ SỬA: (1) 23 collider "vô hình" còn lại sau bước dọn dẹp (rương/thùng đã xoá) -> đã xoá. (2) 4 bể kính Culture Hall chưa từng có hình: thêm hình bể
  (`tank_*`, anim capsule 14080) + phục hồi `tank_*_broken` (vũng đen + bể vỡ, ẩn tới khi cóc đập). Bước dọn dẹp trước đã lỡ xoá 4 vũng `tank_*_broken` (id 399/400/417/418).
- Không mua gói Old Prison (cần người dùng mua); xích/biển/máu/than... tự vẽ trong own_art.py.
- File thừa không xoá được (chưa có quyền xoá): `Soul Requiem/_to_delete/SR-decor2/` (16 ảnh glyph cũ) — xoá tay được.

- 22/10 07:30 — SỬA LỖI VẬT GẮN TƯỜNG BỊ CHE: object trong lớp `ground decals` (SortingOrder -5) nằm trên mặt tường bị lớp tường che trong Unity
  (miệng ống của vòi xả Gallery of Arches biến mất, dòng xả như phun từ tường trơn). Đã chuyển 84 object gắn tường (miệng ống/lưới Terrain2, ống tường,
  mạng nhện, graffiti, mắt nhỏ ở Vault) sang lớp `props` (giữ toạ độ; đáy object ở chân tường nên luôn sau nhân vật). Công cụ: `Catalog/v2/tools_decor2/fix_wall_decals.py`.
  QUY TẮC: vật gắn trên tường đặt ở lớp `props`, KHÔNG đặt ở `ground decals`.
- Lớp `A floor` (đáy xanh Feed Conduit / bồn Cistern) nhuộm `#5f7d68` cho đồng bộ với nước Ichor tối.
- Áp lên bản người dùng lưu lúc 07:17 (đã bỏ 4 vòm, song, thùng Ichor... — giữ nguyên lựa chọn đó). Sao lưu: `Catalog/backups/map2_decor2_2026-10-22/map2_user_save_0717.tmx`.

- 22/10 07:55 — sửa tiếp theo phản hồi:
  * THỨ TỰ VẼ: ST2U đánh số thứ tự vẽ tăng dần theo lớp tile -> chèn lớp mới (curb surface) làm các lớp sau bị đẩy lên, nước Feed Conduit đè lên cầu (decal -5).
    Nay MỌI lớp tile có `unity:SortingOrder` tường minh: channel bed -22 ... curb water A -6 (theo thứ tự trong file), walls -4, wall details -3;
    ground decals -5 nằm giữa (trên nước/sàn, dưới tường). Thêm lớp tile mới thì phải đặt SortingOrder cho nó. Công cụ: tools_decor2/fix_order.py.
  * Lớp object mới `wall props` (SortingOrder -2: trên tường, dưới người chơi): 4 bể nuôi cấy lớn Culture Hall (id 370-373) — trước đây bị tường che mất nắp.
  * Bể kính Culture Hall: vũng đen dưới bể (id 1280/1282/1284/1286) luôn hiện; chỉ `tank_*` (bể nguyên) ẩn và `tank_*_broken` (bể vỡ) hiện khi cóc đập.
  * Màu nước Ichor sáng lại: curb surface #b4e0b8, curb water #a6d4ae, A floor #9cbfa4 (bản trước quá tối dưới Global Light 0.42).
  * ToadSkills: nhảy thường (Hop) KHÔNG còn sóng xung kích, chỉ gợn nước; sóng chỉ có ở Leap Slam (+ sóng thứ 2 ở pha 2).
  * Độc ở Culture Hall: vũng Ichor chỉ là trang trí, không gây độc; độc đến từ đòn của Skinned Hopper / Vat-born (poisonPercent 2-3%).
- 22/10 08:00 — Bể kính Culture Hall: bể nguyên = bình nuôi cấy lớn có Ichor sủi bọt (cắt bình phải của "Secret Lab - capsules only"
  + lớp chất lỏng động, x2; tileset SR-animD-culture_tank, firstgid 14445), bể vỡ = bình nứt rỗng (SR-animD-culture_tank_broken, 14453), đứng giữa vũng đen.
  Vũng Ichor Culture Hall (3 vũng dưới bể tường bắc) giờ GÂY HẠI: mech hazard `cult_acid_0..2` (dps 2%/s + độc 3% trong 3 s, `shape=ellipse`).
  MapMechanics hazard: thêm `poison_percent`, `poison_seconds`, `shape=ellipse` (chỉ tính elip nội tiếp khung). Công cụ: tools_decor2/fix_tanks.py.
- 22/10 08:10 — 3 tờ ghi chú khoá Records Vault (r6_journal, office_ledger, spec_chart) chỉ xuất hiện khi dọn sạch phòng chứa nó
  (note prop `show_when` = r6_cleared / office_cleared / spec_cleared). Mỗi tờ có bục đọc sách `<note>_item` (tileset SR-animD-lectern_book, firstgid 14454)
  + vòng sáng vàng nhấp nháy trên sàn, 3 đốm lấp lánh trên sách và Light2D vàng — tắt sau khi đọc. Khi hiện: hint
  "Something lies where the fighting was. A faint glow marks it." Code: MapMechanics CreateNoteMarker/AnimateNoteMarker (tự vẽ sprite, không cần ảnh).
  CHƯA test trong Unity (Unity đang thu nhỏ lúc sửa).

## 42. VFX đòn đánh (PixelLab VFX Workbench) + chỉnh nhịp Idle/Run/Dash (23/10) — đã test trong Unity
- Ảnh VFX: `Assets/Resources/VFX/` (dải ngang, meta cắt sẵn `<tên>_0..n`, pivot = điểm neo, Point, không nén, PPU 100):
  `slash_E/SE/S/NE/N` (vệt chém ngang 112x64, 4 khung, neo (56,32); hướng phía tây = lật X),
  `hit` (63x55, 8 khung, neo (31,21): chớp + tia lửa + máu sẫm), `ground_impact` (148x60, 8 khung, neo (74,30): vòng sóng + nứt đất xanh thép).
  Bản gốc trên pixellab.ai (My effects): "Iaido Slash", "Iaido Hit", "Soul Ground Impact", "Iaido Slash (8 dir work)" (bản làm 8 hướng, không có nhân vật).
  Mạng chặn api.pixellab.ai -> lấy khung từ ảnh trả về của lệnh export; vệt chém dựng KHÔNG có sprite để khỏi phải tách nhân vật.
  slash_trail: facing 0=E, 45=SE, 90=S, -45=NE, -90=N (độ, y xuống); flatten 0.4, sweep 1.4, thickness 1.2, size 1.25; retime 5 khung rồi fps 16.
- `Scripts/Combat/SpriteVfx.cs`: SpriteVfx.Spawn(tên, vị trí, scale, sortingLayer, order, flipX, fps=16) – Resources.LoadAll, tự huỷ.
- PlayerCombatPrototype (header "Effects (PixelLab VFX)": showVfx, slashVfxLead 0.12, vfxScale 0.7, groundImpactScale 1.4):
  chém J -> vệt chém bắt đầu hitTime-0.12s (khung sáng nhất trùng khung trúng), tâm 50 px trên chân lệch theo hướng; chém lên
  (N/NE/NW) vẽ sau nhân vật (order-1), còn lại order+1. Trúng quái -> `hit` ở bounds.center collider quái (order+2, lật theo hướng).
  F -> `ground_impact` ở chân (order-1, dưới nhân vật) thay vòng tròn debug. Vòng tròn debug chỉ còn khi showVfx tắt.
- PlayerAnimatorBuilder: Idle 0,225 s/khung (vòng thở 0,9 s), state Run speed 1,8 (RunAnimSpeed; 2,4 mới khớp hẳn tốc độ đi
  moveSpeed 2 nhưng quá gấp), 4 clip *_Dashing ép Loop Time = tắt. Đã chạy menu ở Map2 + Ctrl+S (GUID controller giữ nguyên nên Map1 vẫn trỏ đúng).
- Test (Map2, Time Scale tạm 0.1 để chụp, đã trả về 1): F hiện vòng sóng + nứt; J hướng E/S có vệt chém; trúng 3 quái hiện hit. Console chỉ có lỗi
  editor "Screen position out of view frustum" khi bấm Pause (không phải code game).
- Còn lại: pivot Dash nhảy (-7..+11 px) và chân Run lún 3 px ở NE/SE/SW chưa sửa; Dash mới 4 hướng; East/West iaido chân cao hơn idle 1 px.
- Bản sao lưu: Logs/claude_preview/backup_vfx/.

## 43. Vệt chém khớp hitbox ("kiếm khí") + cảm giác lực (23/10 chiều) — đã test trong Unity
- Phân tích: hitbox chém = vòng tròn tâm chân + hướng*reach(0.45), bán kính radius(0.8) -> mép xa 1,25 đơn vị
  (~1,8 lần chiều cao nhân vật 0,68). Vệt cũ chỉ ôm sát thân (~0,25 đơn vị) nên trông "chém gần".
  Collider quái chỉ là vệt chân -> hitbox là vùng trên mặt đất.
  hitTime trong CẢ HAI scene đang là 0.8 trong khi khung chém của animation Iaido là 0.48 s (khung 5) -> vệt + sát thương
  trễ ~0,3 s sau nhát chém. Đã đặt hitTime = 0.48 ở Map1 (sửa file) và Map2 (Inspector + Ctrl+S).
- VFX mới `Resources/VFX/slash_<E|NE|N|NW|W|SW|S|SE>.png` (lưới 3x2 ô 344x344, 5 khung dùng, neo (172,172) = chân):
  dựng recipe riêng trong VFX Workbench (effect "Iaido Reach Slash"): lưỡi chính arc profile slash bán kính 129 px, dày 22,
  quét 120° (start = hướng-60°, chiều kim đồng hồ ảnh = tay trái -> tay phải nhân vật); sóng ngoài bán kính 157 px dày 8
  quét 80° trễ 1 khung; tia lửa bắn ra ở 124 px theo hướng chém. 20 fps, khung sáng nhất = khung 2.
  Hướng góc ảnh (y xuống): E 0, SE 45, S 90, SW 135, W 180, NW -135, N -90, NE -45. Vẽ không squash (khớp vòng tròn hitbox).
- PlayerCombatPrototype: vệt đặt ở chân + 10 px, scale = vfxScale * (reach+radius)/1,25 (đổi tầm thì vệt tự giãn theo);
  bắt đầu 1 khung (0,05 s) trước khi trúng; order +1. Trúng >=1 quái: hit-stop `hitStop` 0,06 s (timeScale 0.05, bỏ qua
  nếu timeScale đang <1) + `cameraKick` 0,05 đơn vị theo hướng chém (CameraFollow.Kick). VFX hit đặt ở thân quái
  (lerp từ collider chân lên tâm sprite 60%). `debugHitbox` vẽ vùng trúng thật; chọn Player trong Scene thấy gizmo 8 hitbox.
  Bỏ field slashVfxLead.
- Ảnh phân tích trước/sau: container scratchpad vfx2/analysis.png (script vfx2/analyze.py). Bản sao lưu: Logs/claude_preview/backup_vfx2/.

## 44. Hitbox chém thu nhỏ thành hình quạt + vệt chém v3 "Iaido Blade Arc" (23/10 15:00) — biên dịch OK, CHƯA chơi thử
- Lưỡi kiếm trong khung trúng (frame_005) chỉ vươn 0,30-0,46 đơn vị tính từ hông; hitbox cũ (tâm chân+0,45, bán kính 0,8)
  vươn 1,25 = gấp ~3 lần lưỡi kiếm, diện tích ~2,0 đv². Người dùng thấy "hitbox to quá, đòn chém xấu".
- PlayerCombatPrototype: BỎ reach/radius. Thay bằng hình quạt: `range` 0.75, `arcDegrees` 140, `innerRadius` 0.3 (sát người
  trúng mọi góc), `pivotHeight` 0.2 (tâm quạt ở hông). Quái trúng nếu điểm gần nhất hoặc tâm collider nằm trong quạt.
  Diện tích ~0,75 đv² (-63%). Struck (cơ quan map) nhận vòng tâm pivot+d*range*0.5, bán kính range*0.6.
  debugHitbox vẽ viền quạt bằng LineRenderer 0,25 s; gizmo vẽ quạt 8 hướng.
- VFX slash_<8 hướng>.png thay bằng bản v3 (lưới 3x2 ô 240x240, 4 khung dùng, neo (120,120) = tâm quạt):
  lưỡi bán kính 89 px dày 13, sóng 103 px dày 5, quét 140°, bỏ tia lửa, ramp ít trắng hơn. Scale = vfxScale*range/0.75.
  Chém lên (d.y>0.3) vẽ sau nhân vật. Effect PixelLab: "Iaido Blade Arc".
- Ảnh so sánh: container scratchpad vfx2/analysis3.png (script vfx2/analyze3.py). Sao lưu: Logs/claude_preview/backup_vfx3/.
- Đề xuất UX chưa làm (chờ người dùng chọn): xem phần trả lời 23/10 15:00 (tốc độ chạy, Roll, input buffer, giữ hướng chéo,
  chuột không ngắm, camera PPU 32 lệch PPU 100, nhịp ra đòn 0,48 s).

## 45. Hoạt ảnh phụ của Rogue Traveler: trúng đòn, dậm chân (F), gục, đứng dậy, trúng nặng, choáng, uống thuốc (24/10)
- Thư mục ảnh mới trong `GamePackage/Rogue Traveler/` (mỗi khung 1 PNG + meta, pivot = chân CỐ ĐỊNH theo idle cùng hướng):
  `hurt/` 8 hướng 3 khung 112x112 (dựng bằng script từ idle khung 0: ngả người ngược hướng nhìn 6 px, gối chùng 3 px,
  khung 1 nháy sáng giữ viền tối) · `stomp/` 8 hướng 8 khung 140x140 (PixelLab v3; W, SW làm lại bằng pixminimax vì v3
  xoay mặt về camera; NE làm lại "stomp_fix") · `death/` S,E,N,W 10 khung · `getting-up/south` 4 khung (mẫu getting-up,
  thứ tự 2,1,3,4) · `stagger/` S,E,N,W 6 khung · `stunned/` S,E,N,W 4 khung lặp · `drinking/` S,E,N,W 6 khung (mẫu drinking).
  Các khung stomp/stunned/stagger được "dán chân": hàng thấp nhất kéo về mặt đất của khung tham chiếu (PixelLab hay để lơ lửng).
  Script: container scratchpad `anim/animlib.py` (split_preview: tách ảnh xem trước của pixelart_workbench join — mạng chặn
  tải file PixelLab, nên lấy khung từ ảnh trả về; nền #1f252c bị khoá màu), `hurt/hurt.py`.
- PlayerAnimatorBuilder: tự thêm state nếu có thư mục: Hurt (HurtTrigger), Skill (SkillTrigger, StompMs khung 4 = 0,31 s),
  Death (bool Dead, Any State đứng đầu, mọi Any State khác thêm IfNot Dead), Revive (ReviveTrigger), Stagger
  (StaggerTrigger -> Stunned nếu bool Stunned), Stunned (lặp 0,15 s/khung), Drink (DrinkTrigger).
- Health: event Hit (chỉ TakeDamage, không tính độc), Revived; StunTimeLeft. SpriteVfx.SpawnAttached (lặp, bám theo vật).
- PlayerCombatPrototype: trúng nhẹ -> Hurt (không khi đang chém/dậm/lộn); knockback >= heavyHitKnockback (3) -> Stagger,
  huỷ đòn, khoá staggerLock 0,45 s; bị choáng (không phải khựng uống thuốc) -> Stagger + Stunned + VFX `stun_stars`
  (Resources/VFX, 12 khung, bám đầu); F = coroutine: hoạt ảnh -> 0,31 s mới gây sát thương + nứt đất + giật camera xuống,
  lộn trước khi chạm đất thì huỷ và hoàn hồi chiêu; E -> DrinkTrigger; gục -> Dead=true; Revive -> Dead=false + ReviveTrigger
  (khoá 0,75 s). J/F không bấm được khi đang khoá. Phím test **F8** = tự gục.
- respawnDelay 1.5 -> 2.6 (code + 2 scene).
- ĐÃ TEST (Map2): dậm chân hướng S + nứt đất, gục -> nằm -> đứng dậy, trúng nhẹ (khung nháy hồng/trắng). Console 0 lỗi.
  (24/10 02:35) Đã biên dịch + chạy menu dựng Animator (Hurt 8, Stomp 8, Death 4, Revive 1, Stagger 4, Stunned 4, Drink 4) +
  Ctrl+S Map2; test uống thuốc E: hoạt ảnh uống + VFX hồi máu, máu 76 -> 100, Console 0 lỗi.
  CHƯA TEST trong game: Stagger / Stunned (cần đòn knockback >= 3 hoặc quái gây choáng như Minotaur, Blightdrinker ở Map 1).
- VFX hồi máu "Ronin Heal" (PixelLab, tự dựng layer): vòng sáng xanh lan dưới chân + đốm sáng bay lên + dấu cộng quanh thân.
  Tách 2 tấm theo hàng 88: `Resources/VFX/heal_back.png` (vòng, order -1) và `heal_front.png` (dấu cộng, order +1),
  13 khung 76x121, neo chân (38,104), 14 fps; bắt đầu 0,15 s sau khi bấm E, bám theo nhân vật (SpriteVfx.Follow).
- Bản sao lưu: Logs/claude_preview/backup_anim/.

## 46. Sửa uống thuốc (thấy rõ bình) + mắt trợn khi gục (24/10 03:15) — đã test trong Unity
- `drinking/` S,E,N,W làm lại: PixelLab v3 "drink_potion" (bình thuỷ tinh tròn, nước đỏ, nút bần), 9 khung 140x140:
  rút bình -> đưa lên -> ngửa cổ uống -> hạ bình. Thứ tự khung: S [1,2,3,4,5,5,4,3,8] (bỏ 6-7 vì bình thành vòng đen),
  E [1,2,3,4,5,6,7,3,2], N [1,2,3,4,5,6,7,3,8], W [1,2,3,4,5,6,7,8,2]. Hướng S bình chỉ là quầng đỏ -> vẽ đè bình 11x14 px
  (nút bần, cổ thuỷ tinh, thân đỏ, điểm sáng) vào tâm quầng đỏ ở khung 2-8 (script anim/res5).
  DrinkMs = 70,70,70,90,90,90,90,80,70 (0,72 s); Drink -> Run nếu bấm đi sau 0,5 s. VFX hồi máu bắt đầu 0,22 s (khung ngửa cổ).
  Bản cũ: Logs/claude_preview/backup_drink_v1/.
- `death/` mắt mở trừng trừng (PixelLab) -> nhắm mắt: East khung 5-6 lim dim, 7-9 nhắm; South & West khung 7-9 nhắm
  (thay lòng trắng + con ngươi bằng màu hốc mắt, vẽ nét mí). Script anim/fix_eyes2.py <hướng> <khung> <khung lim dim>.
  Bản cũ: Logs/claude_preview/backup_death_eyes/.
- Ghi file đè trên máy: phiên này không được xoá -> giải nén vào thư mục tạm rồi `cat src > dst` (tar không ghi đè được).

## §42 Ánh sáng Map 2 – làm lại (24/10/2026)
Công cụ: `Catalog/v2/tools_decor2/fix_lights.py` (sửa tmx), `light_sim.py` / `light_rooms.py` (mô phỏng sáng, ảnh từng phòng).
Sao lưu: `Catalog/backups/map2_lights_2026-10-24/` (tmx trước khi sửa + mọi .cs/.prefab đã vá).
- **Quy tắc:** mọi đèn phải có vật phát sáng nhìn thấy được (đuốc, nến, bể, axit, giếng trời). Không dùng đèn phủ nền `fill_*` nữa.
  119 đèn → 109; bỏ 23 fill + ~20 đèn không nguồn/trùng; đuốc 5 ô → ~3.8 ô.
- **Thuộc tính đèn mới (lớp mechanics, type light):** `anim` = flicker | pulse, `anim_amount`, `anim_speed`; `shape=area`
  + object chữ nhật/đa giác = Freeform light theo hình (mặt axit), `falloff` (ô); `falloff_strength`. Builder gắn `LightAnimator`.
  Đèn có `lit_when`/`off_when` mờ dần (LightAnimator.SetOn) thay vì tắt phụt. Axit A (Feed, hồ Cistern, cổng) `off_when=ichor_diverted`.
- **Ánh nền theo khu:** thuộc tính `ambient`, `ambient_color`, `ambient_fight` trên object lớp `zones` → `ZoneAmbience` (gắn cạnh
  Director) đổi Global Light khi vào khu, tối lại khi đang đánh boss (vault, r8). Hành lang: 0.36 lạnh.
- **Soul Lantern** (`Scripts/Visual/SoulLantern.cs`): tự gắn vào Player khi nạp scene; lõi 1.1u/0.75 + quầng 2.4u/0.22, lam nhạt;
  tự tắt khi ánh nền sáng (Map 1); máu <30% ngả đỏ + nhịp tim; trúng đòn chớp đỏ; lướt/tung chiêu lóe sáng.
- **Hiệu ứng nguy hiểm tự sáng:** `FxLighting.MakeUnlit(renderer)` cho đạn (Projectile), vạch/vòng cảnh báo (GroundStrike, Shockwave,
  Ram, Toad aim/mark, PlagueEye beam), mây độc, SpriteVfx. 4 prefab đạn dùng Sprite-Unlit-Default (guid 9dfc825aed78fcd4ba02077103263b40).
  Hiệu ứng nguy hiểm mới phải gọi MakeUnlit; thân quái/nhân vật giữ Sprite-Lit.
- Hình đuốc/nến dùng tileset sẵn có: đuốc 13959 (SR-animT-props-torch1-lit), nến 14367 (SR-animD-crypt_candle). Old Prison chỉ có nến
  tĩnh không lửa – chưa dùng.
- Chỉnh độ sáng: ánh nền từng khu trong Tiled (zones), đèn riêng trong mechanics, đèn nhân vật trong component SoulLantern.

## 48. CỐT TRUYỆN CHÍNH THỨC – đã chốt 25/10/2026
Tài liệu đầy đủ: https://claude.ai/artifact/TGgU4u8hS94w1yRdznwzTN ("Soul Requiem – Cốt truyện chính thức"). Bỏ logbook cũ (em gái, Pháp Sư Hắc Ám, Chúa Quỷ) và bản "Nước Đắng" (Elowen, the Order).
- Ren: lính đánh thuê không người thân, chưa qua lễ Naming (Nameless). Tobin chỉ là người thuê việc ở Millbrook.
- Vael = hỗn mang (the Unformed), hiện thân của khái niệm thế giới, KHÔNG thể bị đánh bại; thành hình theo cảm xúc con người.
  Hình 1 The Saint (Eldritch Entity, bị giết 1000 năm trước, xác = nguồn Ichor); hình 2 sắp thành = quỷ Volcano giant (the Remade God).
- Giáo hội: the Choir of the Requiem (GameMaster = Hierophant Absalom, Sorcerer = ca viên/Precentor Morrow, Knight = Lantern Knight,
  Tiny Swords phái đen = quân Black Lantern, Old Prison Assassin = Warden Hesk/Blind Confessor). Requiem ru hỗn mang + át Wall of Voices.
- Naming nối linh hồn người với Vael → chết thì mắc trên Wall of Voices (tileset tormented souls Volcano).
- Luật loài: "Ichor ban đúng điều được cầu" → goblin (nông nô), orc (lính), vampire (quý tộc), dwarf, cyclops, undead, mimic, dummy...
- Map 2 = Project ORIGIN (300 năm trước, Absalom + goblin Malric) thất bại: Specimen Zero không trí khôn, Plague Eye điều khiển goblin → the Drowning.
  Malric còn sống, bị giam ở Map 4. Chữ trong game Map 1–2 gần như giữ nguyên; xem bảng "Áp vào game" trong tài liệu.
- Boss cuối không giết được: thắng bằng cách vỡ hình hài (3 pha: hình hài hồi sinh → phá 3 neo → đập tim). 3 kết: Requiem / The Face / Silence.

## 49. THIẾT KẾ MAP 3 – The Vale of Graves (25/10/2026) — mới là thiết kế, CHƯA dựng
Tài liệu: https://claude.ai/artifact/LmdKL4p2u3v7kCjXFnKqWt ("Map 3 – The Vale of Graves: thiết kế màn chơi").
- Lòng chảo có hub: V0–V2 đi thẳng → V3 Commons (hub) → 3 nhánh làm im 3 chuông theo thứ tự tùy ý (V5 Tây, V7 Đông, V9 Nam),
  mỗi chuông mở lối tắt về V3 → V10 nhà nguyện (cần 3 chuông, câu đố kính màu) → V11 boss Precentor Morrow → V12 Requiem Door sang Map 4.
  Bí mật: X1 Field of the Unnamed, X2 King's Barrow, X3 Matriarch (boss tùy chọn). Bảng toạ độ ô (map 136×108) ở mục Kỹ thuật.
- Luật mới: quái chết trên Ichor sống lại sau 3 s, tối đa 2 lần (Ichor Ren xả xuống cống ở Map 2). Đòn F đốt khô Ichor.
- 3 phe đánh nhau: Ca đoàn / The Risen / nhà Vey. Độ khó tăng theo số chuông đã im (cờ bell_west/east/south_silenced).
- Câu đố: P2 gia phả Vey (cipher), P3 câu kết Requiem (mech mới `bell`), P4 đèn Ichor (breakable), P5 kính màu (pipegrid),
  P6 mộ câu đố (xẻng của Sexton), P7 quạ. Gợi ý 3 tầng + dấu Gaze làm la bàn.
- Việc kỹ thuật lớn nhất/rủi ro nhất: AI quái phải nhắm Transform mục tiêu bất kỳ (phe phái), không chỉ tag Player.

## 50. Map 3 ĐÃ DỰNG trong Tiled – map3_vale_of_graves.tmx (25/10/2026) — scene Unity xem §51
- Generator: `Catalog/v2/maps/map3_vale.py` + bộ dựng gói Cemetery `Catalog/v2/cemkit.py`. Chạy trong Catalog/v2:
  `python maps/map3_vale.py --force`. Ra: `Assets/GamePackage/EPIC RPG World Pack - Cemetery V 1.6/TiledMap Editor/Maps/map3_vale_of_graves.tmx`
  (136×108). Stamp: out/map3_vale_of_graves.tmx.gen.md5. Self-check: ĐẠT hết (11 stage cổng). Sao lưu: Catalog/backups/map3_2026-10-25/.
- (25/10 sáng, theo góp ý) BỎ suối/thác/đầm Ichor. Ichor chỉ còn ở chỗ có luật chơi: vệt rỉ dưới cống V0, vũng mộ V1 (dạy hồi sinh),
  rãnh dấu Naming ở đấu trường boss V11. Đường đi: 1 trục đá Bắc–Nam (V0→V12) + 3 nhánh đá (Tây→đại lộ V4, Đông→bậc V7,
  Đông Nam→mép ruộng sụt; đường "lún mất" trong V8, đoạn cuối tới cầu V9) + lối tắt thang máy. Bỏ đồ rải ngẫu nhiên không mục đích
  (mộ/cây/đá rải ở Commons, 2 nhà đổ nát ở V8, sọ rải khắp V7, quạ thường, mảng cỏ trang trí). Mộ tập thể V8 = hàng gò đất.
- Cờ chuông tạm: Tây v5b_cleared, Đông v7_cleared, Nam v9_cleared (chờ cơ chế chuông riêng). Lối ra to_map4 (target map4_catacombs).
- Lỗi cầu nối gặp lại: commit file mới tên vẫn có lúc nhận bản CŨ → luôn md5sum sau commit, sai thì commit lại với tên khác.
- (25/10 ~11:00) Map 3 v8 – DỰNG LẠI theo kiến trúc (góp ý: cổng đè lên mái hầm, Ichor xấu, khung chuông chắp vá):
  * Chất liệu: đất đỏ = lối đi; cỏ tím grass1 = mọi sân đi được; lá đỏ = vườn V6 + sân X3; gạch chỉ ở mặt bệ V0/V7 và sân đại lăng V11.
    KHÔNG dùng lớp "stone/grass detail" (tạo viền xanh vuông vức xấu).
  * Mỗi khu có biên + cổng NẰM TRÊN biên: rào sắt hoa văn (màu 2) quanh Commons, rào cao (màu 3) quanh nhà nguyện, tường đá
    quanh khuôn viên Vey (V5 | tường trong y57 | V5b | chung tường y70 | X3). Cổng: thuộc tính `fence_color` -> hình đóng cùng kiểu rào
    (cemkit._fence_piece). Cổng chính dùng arch(): 2 cột cách tâm ±1.53 ô + "gateway arc" gác lên đỉnh cột (lòng 2 ô).
  * Kết giới nhà nguyện = visual none + đèn tím + hạt (chapel_barrier_fx), không phải rào. X3 vào thẳng qua cổng tường nam V5b
    (x3_gate y70 + x3_lock y71 cùng ô cửa; zone x3 bắt đầu y73).
  * Asset mới (cemkit): "SR belfry 1/5" = 2 cột "pillar - 4" của gói + xà gỗ màu cột đèn + chuông xanh ngọc màu bia mộ;
    Ichor tileset v6 tối, viền xanh đen. Nhà nguyện = sprite "chapel" nguyên vẹn của gói; hầm khoá = sprite hầm + "crypt - N - fence".
  * V7: 5 cơ quan `bell` (note 1..5, answer 3,5,2,1, sets amen_rung) – Unity CHƯA có loại mech này. Đèn Ichor V9 = breakable (ichor_lamp_N_broken).
  * cemkit.deadwood(canopy=2): không trồng cây sát dưới vùng đi được (tán che lối/tường) + avoid các khuôn viên.

## 51. Map 3 TRONG UNITY – Map3_ValeOfGraves.unity (25/10/2026 ~20:15) — đã chơi thử
- Scene: menu **Soul Requiem ▸ Dựng scene Map 3 (Vale of Graves)** (trong Map2SceneBuilder.cs: BuildMap3). Sao từ scene Map 2,
  thay SuperMap, ánh nền 0.42 màu trăng (0.70,0.78,0.95), đèn từ lớp mechanics, thêm vào Build Settings.
  FixPixelTextures: ảnh gói Cemetery (TiledMap Editor/Tilesets/Props) -> Point, không nén, tối đa 8192, không mipmap.
  Lần đầu mất ~45 phút (944 ảnh Props import từng cái + reimport tmx sau mỗi ảnh); đã sửa bọc StartAssetEditing, giờ ảnh đã đúng nên bỏ qua.
- Quái: menu **Soul Requiem ▸ Dựng quái Map 3** (Assets/Editor/SoulRequiem/Map3EnemyBuilder.cs) -> Assets/Enemies/Map3/Prefabs,
  gán 20 loại vào EncounterDirector (Spider dùng lại prefab Map 1). Collider trong Def tính theo THẾ GIỚI (chia cho scale).
  Black Lantern = Tiny Swords đen, ppu 200 (Lancer 300) + scale 1.5 (scale 1 nhỏ bằng nửa người chơi). Megapack: tên tệp có hậu tố
  khung (Move_24x1.png) -> Pick() tìm theo tiền tố. Đạn: BL_Arrow, Blood_Arrow (mũi tên nhuộm đỏ), Hymn_Bolt (đạn mắt Map 2 nhuộm xanh).
  BL_Monk = HealerSkills (Scripts/Enemies/HealerSkills.cs: hồi 25% máu đồng minh yếu nhất trong 3.2u mỗi 3.5 s, lùi khi bị áp sát).
  Lantern_Knight, Ent_2/3, Dummy = LungeSkills. Boss Matriarch_Isolde / Precentor_Morrow = Ranged + treatAsBoss (thanh máu boss chạy).
- MapMechanics: loại `bell` (RingBell): đánh đúng chuỗi `answer` -> cờ `sets`; sai -> reset + thả `fail_spawn` (≤1 con/2.5 s). Đèn chớp BellFlash.
  ĐÃ TEST: chuỗi 3,5,2,1 ở V7 -> "The Amen rings out..." + cờ amen_rung.
- SceneTransition: map3_vale / map3_vale_of_graves -> Map3_ValeOfGraves, tiêu đề "The Vale of Graves" / "Where the Named are buried".
- Đã chơi thử (F1/F2/F3): V0 -> V1 đi bộ thật xuống bậc, mọi khu tới V11 sinh quái đúng, boss bar Isolde + Morrow, Console 0 lỗi 0 cảnh báo.
  CHƯA thử: chuyển Map 2 -> Map 3 bằng đi bộ (scene đã trong Build Settings), quân tiếp viện phase 2, cổng đi bộ từng khu, lối ra to_map4 (chưa có Map 4).
- Sao lưu trước khi vá: Catalog/backups/map3_unity_2026-10-25/ (MapMechanics.cs, SceneTransition.cs, Map2SceneBuilder.cs).
  Script vá: Catalog/v2/tools/patch_mech_bell.py, patch_scene_map3.py, patch_transition_map3.py.
- CHƯA LÀM (hệ thống trong thiết kế §49): Ichor hồi sinh quái, phe phái (quái đánh nhau), hố sụt V8, Gaze, nhật ký/gợi ý 3 tầng,
  chiêu riêng cho 2 boss (giờ chỉ bắn + gọi viện phase 2). Thân chuông không có va chạm (người chơi đi xuyên giá chuông).

## 52. Quái người Map 3 THAY bằng PixelLab cùng style người chơi (25/10/2026 ~21:45)
- Lý do: Tiny Swords + Megapack là chibi, thân rộng 0.6–0.96 lần chiều cao (người chơi 0.28); phóng lên bằng người chơi thì "mập".
  Enemy_Animations_Set/Megapack cao 16–20 px, phóng 3–5 lần -> lộ pixel. Mốc thế giới: người chơi cao ~65 px map (2 ô), cửa nhà nguyện ~68 px,
  cột đèn 81 px, cột rào 55 px. Ảnh so sánh: Catalog/v2/out/qa/size_lineup_*.png.
- PixelLab (pro, style_character_id = Rogue Traveler 7db1a01f…, khung 112; boss 128), hoạt ảnh CHỈ hướng east (EnemyAI tự lật):
  BL_Warrior 7ae90874, BL_Lancer 2acd01a6, BL_Archer a244750e, BL_Monk 20bfc0d9, Choir_Novice c001042b (bản 2: mặt nạ sứ, mũ trùm;
  bản 1 giống Ren quá -> đã xoá), Lantern_Knight 556b80dd, Vampire 43fa18ea, Vampire_Archer d578bc7b, Precentor_Morrow 1abdae47, Matriarch_Isolde 453cae32.
  State: Idle (breathing-idle), Move (walking), Attack (v3 tuỳ chỉnh), Death (falling-back-death), Monk Heal, Lantern Knight Dash.
- Nhập: `python tools/pixellab_import.py [key…]` (Catalog/v2) -> Assets/Enemies/Map3/Sprites/<Key>/<Key>_<State>.png (dải ngang, ô đều)
  + <Key>_meta.txt "cellW cellH pivotY" (chân theo ảnh xoay east). Xem trước: tools/pixellab_preview.py. Đo cỡ: tools/measure_sizes.py, measure_body.py, size_lineup.py.
- Map3EnemyBuilder: Defs giờ là property; PL(key, (state,fps,loop)…) đọc sprite PixelLab. Scale = 0.7 như người chơi (Lantern Knight 0.77, boss 0.8)
  -> cùng mật độ pixel. Quái EPIC RPG (cùng hoạ sĩ với map) giữ, chỉnh vừa: Undead 1.25, Skeleton 1.3, Giant_Bat 1.7; quái Megapack
  độ phân giải thấp còn lại (Ent_1/2 1.4, Ent_3 1.2, Dummy 1.4) để nhỏ cho bớt lộ pixel — nên thay nốt khi có thời gian.
- Chi phí PixelLab ~260 lượt (còn ~4.650/5.000 tới 10/11).
- CẦN LÀM: chạy menu Dựng quái Map 3 rồi chơi thử (chưa chạy vì máy đang mở game khác).

## 53. Soát + sửa hoạt ảnh quái PixelLab Map 3 (25/10/2026 ~23:00) — QUY TRÌNH CHUẨN
- Lỗi gốc đã tìm ra: (1) ảnh xoay east của BL_Warrior bản 1 có 2 khiên, kiếm chỉ thò mẩu -> tạo lại base BL_Warrior v2 cf99c88b
  (tay phải 1 kiếm, tay trái 1 khiên; base cũ 7ae90874 còn trên PixelLab, CHƯA xoá). (2) template idle/walk/death áp khung xương chung
  -> mất/hiện đồ cầm tay, ép quần lên váy/áo choàng. (3) v3 tự mô tả vẽ lại từng khung -> sai tay cầm, lật gậy, tự thêm VFX khi lời
  mô tả có "slash/pulse/wave/spray".
- Cách làm chuẩn từ giờ: hoạt ảnh custom dùng mode **pixminimax** (1 lượt/hoạt ảnh, giữ đồ + màu đúng bảng màu), lời mô tả:
  "fixed side view facing right" + tay nào cầm gì "in every frame" + pha động tác thật (chuẩn bị → ra đòn → theo đà → thu) +
  "Draw only the character: no smear, no trails, no dust, no visual effects". VFX làm riêng ở phần VFX.
- Tên hoạt ảnh trên PixelLab dạng <anim>_vN; pixellab_import.py lấy N lớn nhất cho mỗi state. Tái lập toàn bộ:
  `PL_OUT=<tạm> bash tools/map3_enemy_sprites.sh` (Catalog/v2) = nhập + pl_clean (khoá bảng màu ảnh xoay: xoá điểm lạ như máu/tia;
  điểm lạ trong thân thì tô lại) + pl_frames (bỏ khung smear vệt chém: Warrior/Lancer/Vey Kin/Isolde/Morrow đòn đánh, Huntress chết)
  + Lantern Knight Dash = chu kỳ bước (bản chạy luôn có bụi). LUÔN soi bằng tools/pl_row.py / pixellab_inspect.py trước khi chép vào Assets.
- Bản đã duyệt: Warrior idle/walk/attack(v6)/death v5; Lancer idle/walk/attack/death v5; Archer idle/attack/death v5 (+walk v1);
  Monk heal/death v5; Choir attack/death v5; Lantern Knight idle v5, walk v3, death v5 (attack v1 giữ); Vey Kin attack v6;
  Huntress idle/walk/attack/death v5; Morrow attack v3 (pixminimax, 2 tay nắm gậy, chuông luôn ở đầu), death v6; Isolde attack/death v5.
- Sprite cũ sao lưu: Catalog/backups/map3_unity_2026-10-25/sprites_pixellab_v1/. fps/windup trong Map3EnemyBuilder chỉnh khớp khung ra đòn
  (Archer Attack 18 fps, Huntress 16, Isolde 14, Morrow 12, Vey Kin 13, Choir windup 0.4).
- CẦN LÀM: chạy menu Dựng quái Map 3 + chơi thử (chưa chạy: máy đang mở game khác).


## 54. Quái PixelLab Map 3 có hướng LÊN/XUỐNG + quy tắc KHÔNG VFX (26/10/2026)
- Chỉ làm S (xuống) + N (lên) cho Idle/Move/Attack của 10 quái PixelLab; ngang = east (lật cho west). Death luôn ngang; Monk Heal chỉ ngang.
- File: <Key>_<State>.png (ngang), <Key>_<State>_S.png, <Key>_<State>_N.png; <Key>_meta.txt = "cw ch pyE pyS pyN" (pivot chân theo hướng).
  Map3EnemyBuilder.PL() tự thêm state "<State>_S/_N" nếu file có.
- EnemyAI.cs: hasDirs khi có Idle_S/Move_S/Idle_N/Move_N. Chọn hướng theo trục trội, trễ 1.3× (chống giật); hướng KHOÁ trong lúc ra đòn;
  bản dọc không lật flipX; thiếu bản dọc của 1 động tác → dùng bản ngang, quay mặt về mục tiêu. Bản cũ: backups/.../EnemyAI_before_dirs.cs.
- QUY TẮC KHÔNG VFX trong hoạt ảnh nhân vật: không vệt chém, bụi, máu, tia, KHÔNG đạn/mũi tên bay. Cung/nỏ (Archer, Huntress): mũi tên biến
  mất lúc nhả dây — tools/pl_keepmain.py giữ mảng liền lớn nhất, xoá vật tách rời. Mũi tên/VFX làm sau ở phần VFX (đạn là prefab riêng).
- Bị loại (để trong <Key>/_rejected/, AI dùng bản ngang): Attack_N của Vampire (Vey Kin), Matriarch_Isolde, Precentor_Morrow
  (bản lưng cứ quay mặt ra camera/nhoè mặt). Isolde Idle_N/Move_N còn chút sương đỏ ở gấu váy (thiết kế nhân vật) — chấp nhận.
- pixellab_import.py: regex tên "^(.*)_v(\d+)(?:_\d+)?$" (PixelLab tự thêm _2/_3 khi trùng tên).
- Sprite trước khi chép bản có hướng: Catalog/backups/map3_unity_2026-10-25/sprites_pixellab_v2/. Tái lập: PL_OUT=<tạm> bash tools/map3_enemy_sprites.sh.
- Đã dùng ~60 lượt PixelLab cho phần hướng. Warrior base cũ 7ae90874 vẫn còn trên PixelLab (chỉ xoá khi người dùng bảo).
- Soát 26/10 12:15: Morrow Attack_S khung 6-11 mất gậy → giữ tư thế lắc chuông rồi hạ về (đã sửa trong map3_enemy_sprites.sh);
  Isolde Idle_N/Move_N xoá đốm sương rời (pl_keepmain), viền sương dính gấu váy vẫn còn (nhẹ). Vật rời trong Death = vũ khí rơi (đúng).
- ĐÃ CHẠY Dựng quái Map 3 (19 prefab, 20 loại) + chơi thử khu v2 (Monk/Archer/Lancer) và v4 (Vey Kin/Huntress): quái quay lưng khi
  đi lên, quay mặt khi xuống; số khung clip khớp sprite; Console 0 lỗi 0 cảnh báo. Lantern_Knight_Recover.anim là file thừa cũ (không dùng).

## 55. Thiết kế VFX PixelLab + bộ chiêu theo lore (26/10/2026) — mới là thiết kế, CHƯA code
Tài liệu: https://claude.ai/artifact/KMxSk5QbCfwV2BaPNGWd1Z ("Soul Requiem – VFX PixelLab & bộ chiêu theo lore").
- PixelLab vfx_workbench: miễn phí, dựng hiệu ứng từ lớp tham số (arc/ring/beam/glyphs/particles tự vẽ/remap/warp/light),
  xuất GIF + spritesheet + JSON, --game-pack = ZIP mỗi phần một clip. Thử: "Morrow - Toll of Hymns (test)" effect e822ff5e-2015-4ed1-806f-4d81367e87d7.
- Hiện trạng: đạn mắt (Plague Eye/Hymn_Bolt/Specimen), mũi tên (BL/Blood_Arrow), GroundStrike, LungeSkills (7 quái) dùng chung, chỉ đổi màu.
- Hướng: 4 nhịp cảnh báo (báo → khoá trắng 0,15 s → đánh → dư âm), bảng màu/hình theo phe, mỗi quái 1 chiêu ký tên,
  Ren có né hoàn hảo + Iai Flash + Gaze Sight, boss dùng Posture/Break (đánh thường 10%). Morrow 3 pha (đèn/rãnh Naming/chuỗi Amen 3-5-2-1),
  Isolde 3 pha (vũng máu/3 quan tài/Blood Heart).
- Chờ người dùng chốt: 10% hay 0% khi chưa Break, tạo lại boss khung 176 px?, số bình máu, bắt đầu từ Morrow hay Isolde.

## 56. Đấu trường Morrow mở rộng + 6 cột tế (26/10/2026 ~13:50) — map ĐÃ dựng, code boss CHƯA
- Map 3 giờ 136x126 ô (thêm 18 hàng phía nam). V11 = hành lang (66,81,4,10) + elip tâm (68,100) 28x20 ô; zone v11 (52,81,32,32),
  v12 (60,113,16,12). Cổng v11_lock y87 (close_on_enter), vòm y88. Requiem Door dời xuống y120.6 (exit trig y117.4), 2 đại lăng
  crypt-7/8 hai bên lối. Rãnh Naming = khung Ichor 1 ô rect(62,96,12,8)-(63,97,10,6) (vòng elip mảnh bị tile vẽ đứt khúc như bụi cây).
- 6 cột tế "SR ritual pillar" (PixelLab object e6c50980 + state Blood caa94a72 + Broken 222e7a4c; 38x89 px) ở Props/SR-extra,
  thêm CUỐI danh sách ảnh trong cemkit._make_extra_tilesets. Trên elip 21x14 ô, góc -60/0/60/120/180/240. Mỗi cột: object
  morrow_pillar_N (+ _blood, _broken bị mech toggle 'morrow_pillar_states' ẩn lúc vào), đèn morrow_pillar_N_light (#6aa8ff),
  mech 'ritual_pillar' (hits=3, index=N) — MapMechanics CHƯA xử lý loại này: để code boss (MorrowSkills) làm.
- Undead pha 2 sinh ở 4 cụm mộ quanh mép; Morrow ở bệ tế (68,99.4); trigger boss rect (56,92,24,16).
- Đã chạy: generator --force (md5 khớp trước đó), self-check ĐẠT hết, Dựng scene Map 3 (49 đèn), chơi thử tới V11: cột hiện đúng,
  _blood/_broken ẩn, Console 0 lỗi. Sao lưu: Catalog/backups/map3_arena_2026-10-26/.
- Thiết kế boss bản 2 (cột hút máu, 8 chiêu, 3 pha, Blood Pact, Hymn Ward 25%) + VFX thử "Morrow - Blood Drain (test)"
  (effect 46d9c343-a60d-40ee-9165-fbedfda87edf): tài liệu https://claude.ai/artifact/KMxSk5QbCfwV2BaPNGWd1Z mục "Morrow bản 2".
- CẦN LÀM: MorrowSkills.cs (chu kỳ cột, dải máu, Break, 8 chiêu, camera SetZoom 1.4), xử lý mech ritual_pillar, VFX còn lại.

## 57. VFX cho quái Map 3 (26/10/2026 ~15:30) — ĐÃ gắn + chơi thử
- 22 hiệu ứng PixelLab VFX Workbench (miễn phí, lưu trong My effects tên m3_*) -> Assets/Resources/VFX/m3_*.png, phát bằng SpriteVfx.
  Vệt đánh có hướng neo ở NGỰC: m3_slash_steel_E/S/N (Warrior, Lantern Knight, Skeleton), m3_thrust_E/S/N (Lancer), m3_claw_E/S/N
  (Vey Kin, Giant Bat). _E lật X khi quay trái; _N = _S lật dọc quanh ngực (vẽ SAU thân); thrust/claw S = xoay 90° bản E (không vỡ pixel).
  Neo ở CHÂN: m3_death_soul (ca đoàn, Risen), m3_death_blood (nhà Vey), m3_ground_burst (Undead/Skeleton lúc sinh, Grave Worm lúc trồi),
  m3_dash_dust (lao: LK, Ent 2/3, Effigy), m3_heal_lantern (Monk hồi máu, bám theo đồng đội). Tâm: m3_windup (loé báo đòn),
  m3_cast_gold (Monk), m3_cast_hymn (Choir, Morrow), m3_hurt (trên Ren khi đòn cận chiến trúng).
  Đạn: Hymn_Bolt = m3_hymn_orb (cầu + nốt nhạc, scale 0.8) vỡ m3_hymn_pop; Blood_Arrow vỡ m3_blood_splash; BL_Arrow vỡ m3_arrow_hit.
- Code: Scripts/Enemies/EnemyVfx.cs (tên ảnh theo từng việc, scale 0.7, chestY). Hook: EnemyAI (Start->Spawned, MeleeAttack->Windup+Swing,
  HitPlayer/Hurt->HurtPlayer, RangedAttack->Cast, OnDied->Death; public Vfx, DirToPlayer), LungeSkills (Windup/Swing/Dash),
  HealerSkills (Cast/HealOn). Map3EnemyBuilder: bảng VfxCfg theo key; BuildProjectile(scale) + ảnh vỡ cho mũi tên khi boom != fly.
  Bản cũ: Catalog/backups/map3_vfx_2026-10-26/.
- Công cụ (Catalog/v2/tools): pl_vfx.py <tên> <image_id> <slug> <neo_x> <neo_y> [rotS] [flipY] (tải export -> dải ngang + pivot,
  ghi manifest), vfx_meta.py <thư mục VFX> vfx_m3_manifest.json (tạo .meta Sprite Multiple từ mẫu hit.png.meta, giữ guid).
  Quy trình: dựng trong workbench CÓ sprite quái để canh cỡ -> remove subject -> export -> pl_vfx.py -> chép png -> vfx_meta.py.
- Đã chơi thử (Pause + Step khung): vệt đâm Lancer đúng hướng, vòng hồi máu vàng, chớp trúng đòn trên Ren, máu văng, linh hồn bay khi chết.
  Console 0 lỗi 0 cảnh báo. m3_ground_burst đã làm sáng lại (bản đầu chìm vào nền đất).
- Chưa: VFX chiêu boss Morrow (chờ code MorrowSkills), cảnh báo trên mặt đất cho đòn quái thường (Telegraph).

## 58. Tương tác phím T + hố rơi thành hầm riêng + nới V9 + sửa Lantern Knight (26/10/2026 ~17:20)
Sao lưu trước khi sửa: Catalog/backups/map3_interact_2026-10-26/ (thư mục con lk/ = ảnh Attack_S/N gốc + Map3EnemyBuilder.cs).
- **Phím T (MapMechanics)**: bell / lever / dial / valve / console có đèn Light2D vàng ấm (1,.86,.55): mờ khi trong 3.2 đv,
  sáng nhịp khi trong 0.85 đv (đang chọn). Hiện "[T] <verb>" (prop `verb`, mặc định Ring/Pull/Use/Turn). T = gọi OnStruck vào
  đúng cơ quan đó (bán kính 0.02). Chém (J / chuột trái) vẫn kích được như cũ.
- **Dịch chuyển có màn đen**: SceneTransition.Warp(player, pos, text, shake) — mờ 0.5s (rung), đặt rb+transform,
  CameraFollow.SnapToTarget() (hàm mới), chờ 0.4s, hiện chữ, sáng lại 0.7s. Director: trigger `portal` dùng Warp (+ prop `shake`).
- **Hố V8 (barrow_drop)** giờ rơi xuống **X2 King's Barrow** — phòng kín RIÊNG ở góc dưới trái map (zone x2 = 4,105,20,15),
  không nhìn thấy map chính. Thoát: kéo dây `x2_rope_lever` (T, verb "Pull rope") -> cờ x2_rope -> portal `x2_climb` đưa lên
  Sinking Fields (118.5,66.5). Chưa kéo dây: "The rope is out of reach. Pull it down first." ĐÃ chơi thử đủ chuỗi này.
  (Lần trước thấy cờ x2_rope tự bật là do bấm F5 test, không phải lỗi.)
- **self_check (build_layout_map.py)** đi theo trigger portal như cạnh 1 chiều -> phòng kín vẫn qua kiểm tra "tới được".
- **V9 Broken Bridge**: bờ đông nới thành rect [104,84,22,18], zone v9 = (88,80,40,26), quái giãn ra.
- **Lantern Knight lỗi hoạt ảnh** (ảnh PixelLab lỗi, không phải code):
  Attack_S khung 6-13 tụt chân 23px rồi trồi lại (trông như trượt/nhảy) -> căn chân y=119, bớt khung hồi (14 -> 10 khung).
  Attack_N khung 8-13 bị co nhỏ ~15% và mất kiếm -> thay bằng hồi về khung 2,1 (13 -> 9 khung).
  Builder: LK windup 0.4 (prefab attackTime tự lấy theo độ dài clip = 0.75) để đòn không bị cắt giữa chừng rồi giật về Idle.
  Đã chơi thử ở V9: đòn ngang + vệt chém bình thường, Console 0 lỗi.
- Còn: Dash của LK = bản sao Move (lao trông như chạy nhanh); thanh máu Isolde còn hiện khi F3 nhảy qua đấu trường của bà
  (do test bỏ qua, đánh thật không gặp). MorrowSkills.cs vẫn chưa code (chờ người dùng).

## 59. Lantern Knight bản Chỉ huy + bộ chiêu miniboss (LK, Ancient Ent) + V9 bố cục lại (26/10/2026 ~18:00)
Sao lưu: Catalog/backups/map3_v9_miniboss_2026-10-26/ (map3_vale.py, Shockwave, Health, ZoneAmbience, EnemyVfx, MapMechanics,
PlayerCombatPrototype, Map3EnemyBuilder). Sprite LK bản 1 chuyển vào Sprites/Lantern_Knight/_v1~ (Unity bỏ qua thư mục đuôi ~).
- **LK bản 2** = PixelLab state "Commander" 9a1c182f (cùng nhóm 556b80dd): mũ miện bạc, giáp gai, áo choàng xám tro có huy hiệu
  đèn lồng đen (ngực + lưng), đại kiếm, KHÔNG cầm đèn. Khung 176x176, chân y=156 (pivot 0.1136), meta "176 176 0.1136 ...".
  Hướng ngang = ảnh west lật thành east. Clip: Idle/Move/Attack(chém ngang)/Dash(lao, vòng 4-5 khung)/Slam(bổ đất)/Vow(cắm kiếm thề)/Death,
  mỗi cái có _S/_N (Vow ngang dùng ảnh mặt trước). Đã bỏ 2 khung cleave west dính vệt chém xanh (luật không VFX trong hoạt ảnh).
  Công cụ dựng: scratchpad lkraw/build.py (chân = hàng thấp nhất có >=18 điểm ảnh liền, tránh mũi kiếm).
- **LanternKnightSkills.cs** (thay LungeSkills): Cleave (quạt 130°, pha 2 chém 2 nhát) · Charge (vạch lam 0,7 s, pha 2 lao 3 lần) ·
  Judgement (vòng đỏ chỗ bổ + Shockwave có 2 KHE, pha 2 thêm vòng thứ 2 lệch 90°) · Vow (thề 2 s; đánh trúng 3 lần = vỡ, choáng 2,2 s;
  không vỡ = đồng đội trong 3,6 đv được VowWard: nhận 30% sát thương trong 6 s) · Lightless khi vào pha 2 (ZoneAmbience.DimMul=0.45,
  trả về 1 khi chết). hp 520, treatAsBoss (thanh máu + pha 2), immuneCC.
- **AncientEntSkills.cs** (Ent_3, scale 1.6, hp 420, treatAsBoss): Root Line (rễ gai nối nhau, pha 2 ba đường) · Ichor Volley (4-6 cục
  bay vòng cung vào vòng cảnh báo) · Bramble Ring (Shockwave đỏ 2 khe + rễ mọc theo vòng) · Grasp (rễ dưới chân, giữ 0,8 s) ·
  Communion (<50% máu, tối đa 2 lần: uống Ichor 3 s hồi 24%; 4 đòn = giật đứt, choáng 2 s) · pha 2 gọi 2 Ent_1.
- VFX mới (PixelLab VFX Workbench, Resources/VFX, manifest Catalog/v2/tools/vfx_m3_manifest2.json): m3_lk_cleave_E/S/N, m3_lk_slam,
  m3_lk_sigil (vòng), m3_lk_ward (vòng), m3_lk_lightless, m3_root_spike, m3_ichor_glob, m3_sap_communion.
- Code dùng chung: Shockwave.gapAngles/gapWidth (vòng có khe, đứng trong khe không trúng); Health.DamageTakenMul;
  ZoneAmbience.DimMul; MapMechanics breakable đọc prop `hint`; **phím test F7 = trừ 25% máu boss đang đánh**.
- Map: spawn LK (v5b, v9) và Ent_3 (v6) có boss=True (thanh máu). V9 thêm 2 BL_Archer spawn_when=phase2 (ra từ chân tháp).
  **V9 bờ đông** = mỏm elip (116,92.5, rx 10.5, ry 8) + lối từ đầu cầu [104,88,4,5]; tháp chuông dời ra lưng mỏm (116, 87.4) trên bệ đá
  [112,85,8,3]; 3 đèn Ichor tam giác (109.6,92.6) (122.4,91.6) (116,98.6) có hint mới; cột gãy, vòm đổ, đá, cọc sọ ở vành; sân giữa để trống.
  Self-check ĐẠT hết. Đã chơi thử: LK thề -> khiên trên Lancer/Monk, chém + vệt, bổ + sóng có khe, F7 -> pha 2 tối lại + cung thủ ra;
  Ent: rễ, vòng rễ, mưa Ichor, cây con, uống Ichor. Console 0 lỗi.
- Còn: LK Dash_S/_N hơi quay ngang (PixelLab); chưa đánh thật không bất tử để cân máu/sát thương.

## 60. Lantern Knight — làm lại hoạt ảnh theo soát xét chuyên môn (26/10/2026 ~19:00)
Soát xét trước đó: đòn ngang quay lưng/chém ra sau, Slam ngang quay mặt vào camera, bản _N vẽ kiếm phía camera (sai chiều sâu),
Move/Dash không có chu kỳ chân + giật vòng lặp, tia lấp lánh dính trong khung, nhát chém 2 lệch khung chạm.
- Tạo lại (PixelLab 9a1c182f): Move ngang = pixminimax walk (kiếm cố định), Move_S/_N = skeleton-v3 walking-8-frames;
  Dash ngang/_S = pixminimax sprint (bỏ khung tham chiếu), Dash_N = walk skeleton chạy 12 fps;
  Attack ngang = cleave_w2 SẮP LẠI khung [0,5,6,7,8,9,1,10] (kéo kiếm ra sau -> vung qua -> chạm trước mặt);
  Attack_S = cleave_s2 [0,2,3,4,4,5,6,8,10]; Attack_N = cleave_n2 (kiếm khuất sau thân lúc chém = đúng chiều sâu);
  Slam ngang = slam_w2 [0,1,2,2,3,5,7,8,9,10] (bỏ khung vệt + khung tia); Slam_N = slam_n2[0-4] + vow_n2[5-8,3] + slam_n2[0];
  Vow_N = vow_n2 (kiếm khuất). Mới: ChargePrep (+_S/_N, 7 khung 10 fps = 0,7 s gồng lao), Stagger (+_S/_N, quỳ gối).
  Mọi khung qua clean(): xoá điểm xanh dương + cụm sáng rời <= 12 điểm ảnh. Dựng bằng scratchpad lkraw/build2.py.
- Nhịp khớp code: khung CHẠM của mọi Attack = khung 5 @12 fps -> cleaveWindup 5/12, cleaveRecover 0.35;
  mọi Slam bổ ở khung 5 @8 fps -> slamWindup 5/8. Nhát 2 (pha 2) dùng EnemyAI.PlayStateFrom("Attack", giây) vào thẳng giữa đoạn gồng.
  Charge: ChargePrep trong lúc vạch báo; Vow vỡ: Stagger rồi Idle.
- EnemyAI thêm PlayStateFrom(state, seconds). Builder LK: thêm state ChargePrep/Stagger.
- CHƯA chạy "Dựng quái Map 3" + chơi thử (máy đang mở Roblox, không điều khiển). Còn chấp nhận: Vow ngang dùng ảnh mặt trước
  (nghi lễ), Death quay mặt vào camera rồi ngã, Dash_S thân hơi chéo 3/4, Stagger_N quỳ nhẹ.
- (19:10) Người dùng tự tạo thêm trên PixelLab: 5 bản cmd_charge_px north, 2 bản cmd_stagger north; đã xoá state cũ cầm đèn (556b80dd)
  và nhóm cmd_slam_n2 (Slam_N trong project vẫn còn vì đã ghép sẵn). Chọn: Dash_N = charge_px north 9aa09c9e khung 2-7
  (lưng thuần, kiếm cố định, chân chạy); Stagger_N = stagger north 70a58d4a bỏ khung 5 (quỳ + cúi rõ nhất). Dựng: lkraw/build3.py.
- (19:09) ĐÃ chạy "Dựng quái Map 3": controller LK có đủ 25 state (Idle/Move/Attack/Dash/ChargePrep/Slam/Vow/Stagger x3 hướng + Death),
  số khung đúng (Attack 8, Move 8, Slam 10, Dash_N 6, Stagger_N 10, ChargePrep 7); prefab cleaveWindup 0.4167, cleaveRecover 0.35,
  slamWindup 0.625. Biên dịch không lỗi. Người dùng tự chơi thử. (Lantern_Knight_Recover.anim là file cũ thừa, vô hại.)

## 61. Boss Morrow + Isolde v2: bỏ AI "bắn xa lùi thẳng", bộ chiêu cận chiến + môi trường (26/10/2026 ~22:30)
- NGUYÊN NHÂN cũ: cả 2 boss là EnemyKind.Ranged -> khi người chơi lại gần thì `desired = -toPlayer` (lùi thẳng), không cận chiến,
  không biết đấu trường -> lùi vào góc/tường, đứng kẹt, người chơi đứng sát chém mãi. Không có cơ chế môi trường.
- MỚI: `Scripts/Enemies/BossSkillsBase.cs` (abstract, IEnemySkills): elip đấu trường (arenaRadii/arenaOffset từ chỗ sinh),
  Stalk = đi vòng quanh người chơi ở preferredRange + kéo về tâm (KHÔNG lùi thẳng); 3 bộ đếm chống kẹt (sát mép > 1,2 s,
  bị áp sát > 2,2 s, muốn đi mà không nhích > 0,7 s) -> Reposition() (dịch chuyển tới FarPoint); Break = quỳ Stagger + x2 sát thương;
  Fade / SetHidden (ẩn + tắt collider + bất tử); cooldown theo key (Ready/Use, CooldownMul).
- `MorrowSkills.cs`: pha >65% / 65-30% / <30%. Bell Sweep (cận chiến 180°, pha 2+ nối Toll gõ đất), Toll of Hymns (1/2/3 vòng 4 khe),
  Choir Lances (cột sáng m3_choir_lance rơi theo Ren), Dirge Notes (quạt 5 đạn), Raise the Named (Undead ở cụm mộ), Vespers Step
  (thoát góc / pha 2+ hiện sau lưng Ren rồi quét; bóng mờ nổ vòng nhỏ), Requiem Chorus pha 3 (3 tia quay 6 s, cột tế che).
  Cột tế morrow_pillar_i: chu kỳ hoá máu (_blood, dây máu tới Morrow) -> chém đèn 3 nhát = cột vỡ (_broken) + Break 3,2 s;
  không kịp = hồi 10% + Blood Pact (chiêu nhanh x0,88/tầng). Pha 3: mọi cột còn lại đỏ cùng lúc.
- `IsoldeSkills.cs`: pha >60% / 60-25% / <25%. Blood Lash (cào 160°, pha 2+ hai nhát), Bat Dive (vạch đỏ -> hoá dơi lao xuyên Ren,
  hiện cạnh rồi cào; batFrames = Bat-walk), Blood Volley 3/5/7, Crimson Rain (giọt máu rơi, 3 giọt đầu để lại vũng m3_blood_pool),
  Feast (Mist Step tới vũng xa Ren nhất, quỳ uống 2,6 s hồi 3%/s; 4 nhát = Break), Mist Step (Reposition + bắn), Night Flight (pha 2+:
  trốn vào 1 trong 3 quan tài isolde_coffin_i; quan tài thật rỉ máu; chém đúng = _broken + Break 3,5 s; sai = _open + 2 Bat;
  hết 7 s = phá nắp, vòng máu 3 khe, hồi 6%; mỗi quan tài dùng 1 lần), Sanguine Tide pha 3 (3 vòng 3 khe + vũng máu phun gai).
  Chiêu F (PlayerCombatPrototype.SkillCast) đốt khô vũng máu. Vào pha 3: hút mọi vũng còn lại, mỗi vũng +2%.
- SpriteVfx: thêm `Looping(seconds)`. VFX mới (vfx_m3_manifest3.json, PixelLab "My effects"): m3_choir_lance, m3_vespers,
  m3_blood_mist, m3_break, m3_blood_pool.
- Sprite mới (scratchpad boss2/build.py: căn khung 0 vào <Key>_Rotation*.png bằng so khớp alpha, sai lệch 0):
  Morrow Cast/Toll/Stagger x E,S,N (PixelLab 1abdae47 nhóm m_cast/m_toll/m_stagger; Toll_S bỏ chớp trắng; Cast_N/Toll_N chỉ giơ gậy
  vì gậy đổi tay). Isolde Cast E,S,N + Stagger E,S + Drink E,S (453cae32 nhóm i_cast/i_stagger/i_drink; Cast_E chỉ khung 0-2 vì
  quay mặt + VFX nướng sẵn; Stagger_N quay ngang nên bỏ -> EnemyAI dùng bản ngang).
- Map: 3 quan tài "SR vey coffin" (+ open, broken; PixelLab object b44d3060 / fd6c5714 / ce838354) ở X3 (9.6,82.4) (23.4,82.4)
  (16.5,76.4); toggle isolde_coffin_states ẩn _open/_broken lúc đầu; note "Three Coffins". cemkit thêm ảnh CUỐI danh sách SR-extra.
  Đã dựng lại map (md5 trùng stamp trước khi dựng), tự kiểm tra ĐẠT hết.
- Builder: Morrow/Isolde -> EnemyKind.Custom + MorrowSkills/IsoldeSkills; phase2At Morrow 0.65, Isolde 0.6 (khớp pha 2 của code
  để Undead/Bat spawn_when=phase2 ra đúng lúc); VfxCfg thêm swing (Morrow m3_slash_steel, Isolde m3_claw).
- Sao lưu: Catalog/backups/map3_bosses_2026-10-26/ (EnemyAI, GroundStrike, MapMechanics, MapEncounterDirector, map3_vale.py,
  cemkit.py, Map3EnemyBuilder.cs).
- CHƯA chạy "Dựng quái Map 3" + chơi thử (máy đang mở Roblox, không điều khiển). Test: F3 tới V11/X3, F7 -25% máu boss để xem pha.

## 62. Boss v2.1 — theo phản hồi chơi thử 23:00 (26/10/2026)
- Người dùng: Requiem Chorus (3 thanh trắng quay từ Morrow) thừa + xấu; muốn chiêu BẮN TỪ CỘT; mọi chiêu boss phải dùng VFX PixelLab;
  Isolde bắn tên máu không hợp ma cà rồng.
- Morrow: BỎ Requiem Chorus (+ thanh trắng MakeBar). MỚI Lantern Litany: 2 (pha 1) / 3 (pha 2) cột còn nguyên tụ sáng
  (m3_lantern_charge, đèn Light2D x1,8) + vạch báo m3_ray_warn 0,85 s -> bắn tia m3_lantern_ray dài 7,5 về phía Ren
  (trúng khi cách tia < 0,2, lăn né được). Cột vỡ không bắn. Pha 3 Requiem = về bệ tế + mọi cột còn lại bắn 3 loạt (loạt 2-3 lệch ngẫu nhiên).
  Blood Pact: dây đỏ -> dòng giọt m3_blood_orb chảy từ đèn về Morrow (FlyMote).
- Isolde: BỎ Blood Volley (tên máu). MỚI Children of the Night (3/5/7 dơi Bat-walk bay quạt rồi rẽ đuổi Ren 120°/s trong 1,1 s,
  chém trúng là dơi nổ m3_blood_splash) và Sanguine Siphon (vạch báo đỏ 0,7 s -> dòng giọt máu từ Ren về bà 2,8 s, mỗi 0,5 s
  trừ 30% sát thương + bà hồi 1%; đứt khi xa > 3,8 hoặc QUAN TÀI chắn giữa). Bat Dive vạch báo = m3_ray_warn nhuộm đỏ.
  Crimson Rain giọt rơi = m3_blood_orb.
- Shockwave: thêm `crest` (Sprite[] mảnh sóng PixelLab, mũi hướng +x) — xếp mảnh quanh elip thay vòng vẽ tay, khe = bỏ mảnh.
  BossSkillsBase.Wave(at, r, dur, dmg, "m3_wave_hymn"|"m3_wave_blood"). LK/Ent vẫn dùng vòng cũ.
- BossSkillsBase: bỏ MakeBar/Stretch/PixelSprite; thêm Wave, Ray/PlaceRay (ảnh tia nằm ngang gốc trái, kéo dãn theo chiều dài),
  FlyMote, SegDist, Dir. SpriteVfx thêm Tint(color).
- VFX mới (vfx_m3_manifest4.json): m3_lantern_ray (71x20), m3_ray_warn (62x4, trắng để nhuộm), m3_lantern_charge,
  m3_wave_hymn, m3_wave_blood (12x20), m3_blood_orb (12x12). Công thức lưu trong PixelLab "My effects".
- CHƯA dựng lại prefab/chơi thử sau v2.1 (máy mở Roblox). Cần chạy "Dựng quái Map 3".

## 63. Cân bằng boss v2.2 + hiệu ứng trạng thái người chơi (27/10/2026 ~01:00)
- Phản hồi: sóng xung kích to/rộng mà còn mất máu; boss gọi quái quá nhiều; không có nhịp dừng.
- NGUYÊN TẮC: đòn rộng/khó né (vòng sóng, mưa máu) KHÔNG mất máu mà gây trạng thái theo lore; đòn có báo rõ, né được
  (cận chiến, tia, cột sáng, Bat Dive, gai vũng máu) mới mất máu.
- MỚI `Scripts/Combat/PlayerStatus.cs` (PlayerController.Awake tự gắn): Slow (SpeedMul), Silence (khoá F), Hemorrhage/BlockHeal
  (không uống bình), Daze (Health.Stun + stun_stars). Lăn (Invulnerable) / F1 bất tử thì miễn. VFX PixelLab: m3_st_slow (chân),
  m3_st_silence (vòng ấn chú, +0.32), m3_st_bleed (máu nhỏ giọt, +0.55). Gọi lại khi đang có thì chỉ kéo dài (không tạo lại).
  PlayerController nhân SpeedMul; PlayerCombatPrototype: F bị Silenced -> "Your skill is sealed!", bình máu bị HealBlocked -> không mất bình.
- GroundStrike/Shockwave: `onHit(Health)`; damage 0 = chỉ đẩy lùi (AddImpulse) + onHit, lăn né được. Shockwave không trúng (lăn/miễn)
  thì không đánh dấu hitDone (sóng vẫn có thể trúng sau).
- BossSkillsBase: NHỊP NGHỈ chung sau mỗi chiêu Gcd() = 1,7 / 1,35 / 1,05 s (pha 1/2/3); ReadyG() cho mọi chiêu trừ cận chiến;
  trong nhịp nghỉ boss chỉ đi vòng hoặc cận chiến. PS = PlayerStatus của người chơi.
- Morrow: Toll vòng 1/2/2 (không mất máu, Silence 3,5 s); Toll Strike nối Bell Sweep -> Daze 0,5 s; Echo -> Slow 35% 1,5 s;
  vòng chuyển pha chỉ đẩy. Raise CHỈ từ pha 2, hồi 26 s, 2 con/lần, tối đa 3 Risen sống; BỎ đợt Undead x4 spawn_when=phase2 trên map.
  Hồi chiêu: sweep 2, toll 9, lances 10 (3/4/5 cột, x0,8), dirge 6 (2 loạt chỉ pha 3), litany 11 (x0,8), requiem 22 (x0,55/tia),
  vespers 10. Cột hoá máu 18 s (x0,85 / x0,75), mỗi lần 1 cột, hồi 8%; Blood Pact tối đa 3 tầng (x0,9/tầng).
- Isolde: Crimson Rain 4/5/6 giọt KHÔNG mất máu -> Hemorrhage 6 s (2 giọt đầu để vũng, tối đa 4 vũng); đứng trong vũng = chậm 25%;
  Sanguine Tide 2 vòng -> Slow 45% 2,5 s (gai từ vũng vẫn x0,8, báo 0,75 s); phá nắp quan tài -> Slow 40% 2 s; vòng pha chỉ đẩy.
  Dơi 3/4/5 (x0,5), Mist Step thả 2. Chém sai quan tài -> 1 Bat (trước 2); map pha 2: 2 Bat (trước 3).
  Hồi chiêu: lash 1,8, dive 8, bats 7, siphon 16, rain 10, feast 16, flight 28, tide 12.
- Map dựng lại (md5 khớp stamp), tự kiểm tra ĐẠT hết. CHƯA dựng quái/chơi thử — cần "Dựng quái Map 3" + "Dựng scene Map 3".

## 64. Chỉnh boss theo yêu cầu 27/10 01:46
- Morrow: BỎ Bell Sweep (pháp sư không cận chiến). Bị áp sát -> TollStrike (anim Toll, vòng nhỏ m3_wave_hymn: Daze 0,5 s,
  đẩy 3, không mất máu, hồi 4 s) rồi Vespers Step đi xa; pinnedTimeMax 1,4. Vespers tấn công cũng kết thúc bằng TollStrike.
  Đèn cột: pillarHits 1 (1 nhát vỡ), vỡ đèn -> Break 3,5 s.
- Isolde: Blood Lash hồi 15 s (chỉ khi bị áp sát). Night Flight = trò TRÁO QUAN TÀI: dơi bay vào đúng 1 quan tài (máu rỉ 1 s) ->
  tráo chỗ 4 lượt x0,5 s (pha 3: 6 lượt x0,36 s; dời cả base/_open/_broken theo cung) -> 5 s để chém 1 quan tài
  (chỉ nhận đòn khi guessOpen). Đúng: _broken + Break 3 s. Sai/hết giờ: quan tài sai bật nắp, bà phá nắp, hồi 8%, sóng làm chậm.
  Sau đó cả 3 quan tài niêm; 20 s sau chiêu Rite of Repose dựng lại (vị trí giữ theo lần tráo), Night Flight sẵn lại sau 6 s.
- Lantern Knight: BỎ chém thường khỏi vòng chiêu (chỉ Charge / Judgement / Vow / Lightless); đứng gần (<1,4) mà chưa có chiêu thì
  thủ thế bước vòng chậm.
- MỚI `Scripts/Enemies/EnemyHealthBar.cs`: thanh máu 30x4 px trên đầu quái thường (không boss/treatAsBoss), hiện khi mất máu,
  vệt trễ sáng; độ cao lấy từ lưới Tight của sprite Idle. EnemyAI.Start gọi EnemyHealthBar.Attach(this) (áp dụng mọi map).
- LK PixelLab: tư thế Commander cũ vẫn chìa nắm đấm trống (di sản cầm đèn). Đã tạo state "Commander Two-Hand"
  49d0004b-eb7e-4948-ad53-d5f54250b3dd (hai tay cầm chuôi kiếm). Đã xếp hàng c2_idle, c2_walk (pixminimax), c2_cleave, c2_death,
  c2_slam, c2_charge_prep (W/S/N). Còn thiếu: dash, vow, stagger; chưa QA/dựng sheet. Tạm dừng theo yêu cầu người dùng.

## 65. Lantern Knight bản 3 — bỏ tư thế cầm đèn (27/10 ~02:20)
- Gốc lỗi: state PixelLab "Commander" (9a1c182f) vẫn chìa nắm đấm trống (di sản cầm đèn) -> mọi hoạt ảnh cmd_* mang theo.
- State mới "Commander Two-Hand" 49d0004b-eb7e-4948-ad53-d5f54250b3dd (hai tay cầm chuôi đại kiếm). Nhóm dùng:
  c2_idle (west 11f 9bd7527e, south, north), c2_walk (pixminimax), c2_dash (west/south; Dash_N = walk north),
  ChargePrep: side = dash_west [0,1,1,2,2,2,2] (bản v3 & pixminimax west đều quay người -> bỏ), S/N = c2_charge_prep;
  Slam: west = c2_slam_w (pixminimax) [0,1,2,3,5,7,8,9,10,11,12], S = c2_slam south [0,1,2,2,3,3,4,5,8,10,12],
  N = c2_slam_n [0,1,2,3,4,6,7,8,9,10,12] -> mọi hướng chạm đất ở khung 6 (slamWindup = 6/8);
  c2_vow W/S/N, c2_stagger W/S/N, c2_death west. Dựng: scratchpad lkraw/build4.py (ô 176, chân 20 px, lật west -> east).
- Bỏ c2_charge_prep west, c2_slam west/north bản v3 (lỗi). Không có Attack (LK không còn chém thường); Attack cũ chuyển vào
  Sprites/Lantern_Knight_v2~ (cùng bản sao toàn bộ sheet v2). Builder: bỏ ("Attack"...) khỏi PL LK.
- CẦN chạy "Dựng quái Map 3" để cắt lại sheet.

## 66. 27/10 ~02:30
- Morrow: không còn đòn thường nào — bỏ Toll Strike cận chiến, Dirge Notes và Vespers tấn công khỏi Think. Bị áp sát 0,8 s ->
  Vespers Step dịch đi (repositionCooldown 2,5). Chiêu còn lại: Toll of Hymns, Choir Lances, Lantern Litany, Raise, Requiem, Vespers.
- Isolde: đòn cào -> chiêu Sanguine Kiss (hồi 15 s, 1 nhát x0,55, trúng thì bà hồi 4% + dòng giọt máu). Bat Dive không reset hồi chiêu.
- X3: bỏ hầm mộ cuối sân (anim crypt + solid), thay quan tài thứ 4 isolde_coffin_3 (16.5,87); coffinCount 4; note "Four Coffins".
  arenaRadii (3.0,2.2), offset (0,0.1). Map dựng lại, ĐẠT hết.
- Thanh máu quái: EnemyHealthBar chỉ còn là danh sách (All + HeadWorld); SimpleCombatHUD.DrawEnemyBars vẽ thanh 46x5 + chữ
  "máu/tối đa" trên đầu MỌI quái thường còn sống trong màn hình (luôn hiện). Thanh boss cao 20 px + chữ "máu / tối đa".

## 67. LK bản 3.1 — soát xét chuyên môn + sửa (27/10 ~03:45)
- Lỗi tìm thấy ở bản 3: Stagger bên quay ngược người; Stagger_N và Slam_N đổi từ nhìn sau sang nhìn ngang; Death quay mặt ra camera;
  Move bên lê chân; Dash_N dùng tạm Walk; Idle bên áo choàng bay mạnh + vòng lặp giật; ChargePrep_N kiếm đổi bên.
- Tạo lại (pixminimax, ép "strict side view facing left / back view in every frame"): c3_stagger_w, c3_stagger_n, c3_slam_n,
  c3_dash_n, c3_walk_w, c3_death_w (xoá vệt máu đỏ nướng sẵn). Dựng: lkraw/build5.py -> outputs/lk4.
- Ghép: Idle bên = idleW1 [0,1,2,2,1,0] (thở nhẹ); Move bên = c3_walk; Dash bên = dash_west 3..8, Dash_S 2..8 (vòng lặp liền);
  Dash_N = c3_dash_n; ChargePrep_N = [0,1,2,3,3,3,3]; Slam_N = c3_slam_n [0,2,3,4,5,7,8,...] (chạm đất khung 6);
  Stagger/Stagger_N/Death = bản c3. Còn chấp nhận: Slam_S mũi kiếm cắm đất chạm mép dưới, chưa có khung bổ xuống giữa.

## 68. 27/10 ~04:00 — sửa lỗi biên dịch + LK Judgement + Isolde "quan tài là mạng"
- QUY ƯỚC MỚI: mọi việc chơi thử / bấm menu trong Unity -> NGƯỜI DÙNG TỰ LÀM; Claude chỉ ghi rõ cần làm gì. Không điều khiển Unity nữa.
- Lỗi CS0103 "EnemyHealthBar does not exist": Unity không nạp EnemyHealthBar.cs (không có trong Library/Bee/.../Assembly-CSharp.rsp).
  Sửa: thay .meta bằng guid mới + MonoImporter đầy đủ, chạm file -> biên dịch sạch (0 lỗi). Đã chạy Dựng scene + Dựng quái Map 3 lúc 03:38.
  Còn 1 cảnh báo khi Play "The referenced script (Unknown) on this Behaviour is missing!" — chưa xác định nguồn.
- Health: thêm `Floor` (máu không xuống dưới mức này; 0 = bình thường).
- LK Judgement: waveRadius 3,2 -> 1,8, sóng nhanh hơn (x0,75), vỏ m3_wave_hymn; lưỡi kiếm + sóng KHÔNG sát thương, chỉ Daze 0,9 / 0,7 s + đẩy nhẹ.
- Isolde: quan tài = mạng. Còn quan tài thì hp.Floor = 5% (không chết, có gợi ý). Chém 3 nhát phá 1 quan tài (ngoài lúc tráo).
  Vỡ quan tài -> bà hồi healStep x số quan tài đã vỡ (3/6/9/12%, cấp số cộng) + hồi liên tục regenStep 0,2%/s x số đã vỡ.
  Rite of Repose: mỗi 30 s (cố định) dựng lại MỘT quan tài vỡ (xa Ren nhất). Night Flight chỉ dùng quan tài còn nguyên (>=2);
  đoán đúng = quan tài đó vỡ (không hồi) + Break 3 s; đoán sai = nắp bật rồi đóng lại, bà hồi 8%. Thanh boss ghi "Coffins n/4".

## 69. X2 King's Barrow = đáy hố thật (27/10 ~04:25)
- Trước: sân đất đỏ có rào cột đá + cọc sọ, rừng cây chết bao quanh (giống mặt đất).
- Nay: vách đá wall-1 bốn phía (+ wall2 phía bắc), sàn đá lát (stone) + vệt đất sụt; ngoài hầm ĐEN ĐẶC bằng 4 tấm "SR void N/S/W/E"
  (PNG đen, lớp props front) chừa đúng hầm x5-23, y104-119; dưới đó còn hole + rocky. Deadwood tránh rect [0,95,31,31].
- Đồ hầm mộ gói Crypt chép vào Props/SR-extra tên "SR crypt *" (32 ảnh; cemkit nạp CUỐI danh sách): quan tài vua mở + nắp lệch,
  2 tượng, 2 chân nến (đèn ấm), 2 giá đuốc trên vách bắc, xương rải, bình gốm, thánh giá; đá rơi dưới chỗ dây. Ánh sáng nền x2 0,2 #8a96c0.
- Người dùng đã xoá LK ở V5b trong Tiled (04:12) -> bỏ spawn đó khỏi generator. Bản tmx người dùng lưu ở backups/.../map3_vale_of_graves_user_0412.tmx.
- Map dựng lại, tự kiểm tra ĐẠT hết. Cần "Dựng scene Map 3".

## §70 HUD "Slate & Gold" (27/10)
- `Scripts/UI/SimpleCombatHUD.cs` viết lại (vẫn OnGUI). Ảnh ở `Assets/Resources/UI/hud_*.png`, bake bằng `Catalog/v2/tools/ui_bake.py`
  (nguồn: Tiny Swords UI — SpecialPaper/BigBar/SmallBar/SmallRibbons/Icons, đổi màu gỗ → xám đá + vàng; 2D Pixel Dungeon flasks; VFX ground_impact/stun_stars; Rogue Traveler south idle làm chân dung).
  Script bake đọc từ thư mục upload của container — chạy lại thì sửa biến `U` thành `Assets/` của project.
- Bố cục: trên trái khung người chơi (chân dung, thanh máu có vệt máu vừa mất, nhấp nháy khi <30%, biểu tượng trạng thái + giây);
  dưới trái ô J/Q/F/E (che tối theo hồi chiêu, loé vàng khi xong, F "SEALED" khi Silence, E dấu X khi Hemorrhage, số bình);
  trên phải tên khu trên ruy băng xám; dưới giữa thanh Boss + tên trên ruy băng máu, phần sau "—" của displayName hiện góc phải (Coffins n/4);
  gợi ý trên khung tối mờ dần; quái thường thanh nhỏ khung vàng + số (tắt bằng `enemyHpNumbers`).
- Thông số cắt lát (BigCap/SmallCap/RibCap/Inset/PanelBorder) trong HUD phải khớp ảnh bake. Thiếu ảnh → vẽ khối màu + cảnh báo Console.
- PlayerStatus thêm `SlowLeft/SilenceLeft/HealBlockLeft`.
- Lưu ý commit: đẩy .meta trước .png khi Unity đang mở → Unity xoá meta "mồ côi". Đẩy png trước rồi meta sau.
- Bản cũ: `Catalog/backups/ui_2026-10-27/SimpleCombatHUD.cs`.

## §71 Dời hầm X2 King's Barrow ra xa (27/10 05:00)
- map3_vale.py: thêm `X2Y = 46`; map cao 126 -> 172 ô. Mọi toạ độ X2 (OPEN, địa hình hố, đồ hầm, đèn, dây, portal x2_climb,
  zone x2, 4 tấm SR void, deadwood avoid) cộng X2Y (hàm `Y()` trong mục 15). Portal barrow_drop target = 15.0,160.5.
- Chỗ cũ góc tây nam (x0-40, y90-126) giờ là rừng cây chết bình thường; hầm + khối đen nằm ở y136-172, cách X3 ~47 ô -> không khu nào thấy.
- Đã so cấu trúc .tmx trước khi --force: 3511 object trùng khớp bản dựng (chỉ khác do trim_props) -> không có sửa tay.
- Dựng lại: TỔNG ĐẠT hết. Bản cũ: Catalog/backups/map3_x2move_2026-10-27/. Cần "Dựng scene Map 3" trong Unity.

## §72 Judgement của Lantern Knight dùng VFX ground_impact (27/10 06:15)
- LanternKnightSkills.JudgementWave(at): SpriteVfx "ground_impact" (như chiêu F của Ren) scale = waveRadius/0.71 (vòng lớn nhất khung 4 = 142 px),
  fps `waveFps` = 10; kèm Shockwave ẩn (`hidden = true`, `squash = 0.45`, band 0.22, thời gian 4.2/fps) chỉ để tính trúng -> Daze 0.7 s, không sát thương.
  Bỏ khe hở (vòng kín) vì VFX là vòng liền; pha 2 vẫn 2 sóng cách 0,45 s. waveTime/gapWidth còn trong Inspector nhưng không dùng.
- Shockwave thêm `hidden` (không vẽ) và `squash` (độ dẹt vòng, mặc định 0.6 như cũ).
- Báo lỗi "shockwave bị mặt nước che": chưa tìm ra lớp nước nào vẽ trên order 1 (tile layer đều âm, props 2) -> chờ người dùng chỉ chỗ.
- Bản cũ: Catalog/backups/lk_wave_2026-10-27/.

## §73 Theo dõi nhiệm vụ chính + minimap (27/10 06:24)
- `Scripts/Map/QuestTracker.cs` (tự tạo khi scene có MapEncounterDirector): đọc `Resources/Quests/<Scene>.json`
  (steps[] {title, done "a,b|c", targets[] {label,x,y (ô Tiled), done, pre/preLabel/preX/preY}}, hidden[] vùng bí mật, view = khung bản đồ lớn).
  Bước hiện tại = bước đầu chưa xong; đích = đích chưa xong GẦN NHẤT (pre chưa có thì chỉ tới chỗ mở khoá trước). Cờ lấy từ MapEncounterDirector.
- Đã viết nhiệm vụ cho 3 map: Map1 (zf->z0->...->z5 Grok->lối xuống), Map2 (r2->r3->r4->{crank, Vat key, Pump key}->ichor_diverted->r8->outflow),
  Map3 (Lychgate->3 chuông [Tây cần vey_open]->rose window->Mausoleum->Requiem Door). Đổi cờ/vị trí thì sửa JSON, không cần build.
- Minimap: `Resources/Minimap/<Scene>.png` bake bằng `Catalog/v2/tools/minimap_bake.py [Scene]` (4 px/ô, chỉnh sáng theo GRADE; vùng hidden xoá trong suốt).
  Map3 ẩn hàng y>=128 (hầm X2). Map dựng lại thì bake lại minimap.
- HUD: minimap 170 px dưới tên khu (34 ô ngang, mũi tên người chơi theo hướng nhìn, hình thoi = đích, ra ngoài khung = mũi tên mép),
  khung MAIN QUEST dưới minimap (loé "QUEST UPDATED" 3 s khi qua bước), mũi tên nhấp nhô trên đích (hud_wp_0..3 từ 2D Pixel Dungeon arrow),
  đích ngoài màn hình -> hud_pointer ở mép + số mét. Phím M = bản đồ lớn. Trong vùng hidden: minimap "???", không chỉ đường.

## §74 Sửa lỗi không vào được đấu trường Morrow (27/10 06:50)
- Nguyên nhân: zone v11 = (52,81,32,32) phủ cả hành lang y81-86 PHÍA TRƯỚC cổng khoá v11_lock (y87). Người chơi bước vào hành lang
  -> khu bắt đầu -> cổng khoá đóng sập trước mặt -> kẹt ngoài.
- Sửa dữ liệu: v11 = (52,89,32,24) (bắt đầu sau cổng). Đã so cấu trúc tmx (trùng 5245 object) rồi --force; TỔNG ĐẠT hết.
- Sửa code (chống tái phát mọi map): MapEncounterDirector.ReadyToStart chỉ bắt đầu khu có cổng khoá khi người chơi ở CÙNG PHÍA cổng với tâm khu.
- Lưu ý: chạy generator sang --out khác vẫn ghi đè stamp out/<tên>.gen.md5 -> sao lưu/khôi phục stamp khi so sánh.

## §75 HUD: nhiệm vụ sang trái + phím bấm rõ (27/10 15:33)
- Khung MAIN QUEST: thử chuyển sang cột trái, người dùng muốn giữ chỗ cũ -> lại nằm dưới minimap (cột phải, rộng 250).
- Ô hành động: tên ATTACK/ROLL/SKILL/POTION phía trên (SEALED / NO HEAL màu đỏ khi bị khoá), nắp phím J/Q/F/E nổi giữa mép dưới ô
  (nền tối, viền vàng, chữ trắng 14), số bình "xN" trong góc trên phải ô; thanh nâng lên 12 px, khoảng cách ô 12 px.

## §76 Soát chi tiết phi logic Map 3 (27/10 15:54)
- V1: bỏ bia "tombstone - tall - 5" + ụ đất "tombstone - dirt - 2" giữa vũng Ichor (74,19-20) — vũng giữ nguyên (luật chơi hồi sinh).
- Bậc thang V0 (67,8), V7 (117,13), (111,13): bỏ ô bậc phẳng (k.stairs(..., tiles=False)), đặt ảnh "SR stairs wide"
  (cắt Tilesets/stairs.png của gói, 128x85: trụ gạch + bậc) ở lớp ground decals, tâm x+1, chân y+3. cemkit nạp "SR stairs *" CUỐI SR-extra.
- V8: bỏ 8 "bia nghiêng" (khung bia rỗng trên miệng hố, trái với bảng "No time for stones") -> sọ + đá vụn ở mép hố (giữ số lần gọi rng).
- Người dùng sửa tay trong Tiled (X1: collider "tree 1" + đèn x1_glow) -> generator mục USER_EDITS áp lại sau mỗi lần dựng.
- Chưa sửa (ghi lại): ô Ichor (SR-ichor) nhìn như bụi cỏ xanh; ụ đất trước 2 hầm mở ở V4 (cố ý: ma cà rồng cạy hầm).
- Dựng lại TỔNG ĐẠT hết; minimap Map 3 bake lại. Bản trước: Catalog/backups/map3_x2move_2026-10-27/*_pre_audit.py.

## §77 Số bình máu (28/10)
- Bỏ huy hiệu đỏ đè lên góc ô POTION. Số bình giờ là "×N" chữ trắng 18 viền đen 4 phía, đứng NGOÀI bên phải ô (giữa chiều cao); hết bình -> chữ đỏ nhạt, icon xám.

## §78 Bố cục HUD theo nhóm chức năng (28/10)
- Cụm SINH TỒN (trên trái): khung máu + ô BÌNH MÁU riêng gắn ngay bên phải (DrawFlask: số bình góc trên phải viền đen,
  phím E, loé khi uống, hết bình icon xám/số đỏ, Hemorrhage: icon gạch chéo + viền đỏ nhấp nháy + giây) + hàng trạng thái.
- Cụm CHIẾN ĐẤU (dưới trái): chỉ 3 ô ATTACK J / ROLL Q / SKILL F (hàm KeyCap dùng chung).
- Cụm ĐỊNH HƯỚNG (trên phải): tên khu, minimap, nhiệm vụ. Cụm TRẬN ĐẤU (dưới giữa): thanh boss + gợi ý.
- Dưới 30% máu: viền đỏ quanh màn hình (texture tự sinh) đập theo nhịp tim, mạnh dần khi máu càng thấp.
- Bản trước: scratchpad hud_before_flask.cs / backups/ui_2026-10-27/.

## §79 Giao diện mới "Requiem Gothic" (UI Toolkit) + chiêu tháo lắp + menu (28/10)
- Thay HUD OnGUI (SimpleCombatHUD bị tắt tự động; file vẫn giữ để tham khảo).
- Bộ ảnh `Assets/Resources/UI/Requiem/` (PixelLab create_ui_asset + vẽ tay, bảng màu đá tro/vàng xỉn/đỏ máu): skill_socket, roll_socket,
  medallion (đã khoét lỗ chân dung), hp_frame/boss_frame (đã khoét rãnh), flask_back/liquid/front (chất lỏng cắt theo số bình),
  minimap_frame, panel_dark/stone, button, chk_on/off, track, btn_round, pill, keycap, hint_band/line, ic_* (16 icon PixelLab + ic_slot_empty).
  Phông pixel `Fonts/RequiemTitle.ttf` (16px), `RequiemBody.ttf` (12px) — chỉ ASCII; RequiemUI.Clean() đổi —, …, ’, ×, > ...
  Nguồn & script cắt ảnh: scratchpad kit/ (proc.py) — số đo rãnh/lỗ trong meta.json.
- Code: `Scripts/UI/Requiem/` RequiemUI (khởi động + điều khiển SRButton/SRSlider/SRToggle/SRStepper/CooldownRing),
  RequiemHUD, RequiemMenus (Pause Esc / Settings / Rest T ở điểm nghỉ / màn chết), TitleScreen; `Requiem.uss`, `RequiemTheme.tss`.
  Bố cục (1920x1080): trên trái máu + hàng nhỏ Đánh J / Lăn Q + trạng thái; dưới trái 3 ổ chiêu; dưới phải bình máu; trên phải tên khu,
  minimap, nhiệm vụ; dưới giữa thanh boss (tự thu nhỏ màn hẹp) + gợi ý; viền đỏ máu thấp; mũi tên nhiệm vụ; thanh máu quái; bản đồ lớn M.
- `Scripts/Core/GameInput.cs`: bảng phím (Attack J, Roll Q, Heal E, Skill F/R/C, Interact T, Map M, Pause Esc), GameInput.Blocked khi menu mở.
  `GameSettings.cs`: PlayerPrefs set_* (âm lượng, độ phân giải, fullscreen, vsync, cỡ HUD, số máu quái, rung màn hình, cảnh báo máu, giảm nhấp nháy).
  CameraFollow.Kick nhân GameSettings.ScreenShake.
- Chiêu tháo lắp: `Scripts/Combat/Skills/` SkillDefinition (ScriptableObject), GroundSlamSkill ("Earthshatter" = chiêu F cũ),
  PlayerLoadout (3 ô, hồi chiêu, Equip/Unlock, sự kiện). Asset `Resources/Skills/GroundSlam.asset`. GameSession mang bộ chiêu qua map.
  Trigger pickup item=skill + skill_id mở khoá chiêu. Checkpoint: hint "press T to rest"; MapEncounterDirector.AtCheckpoint / Respawning.
- Title: menu Soul Requiem ▸ Dựng scene Title (tạo Assets/Scenes/Title.unity, đặt đầu Build Settings). Continue = PlayerPrefs last_scene.
- Còn lại: bảng cơ quan (khoá chữ, ống) của MapMechanics vẫn OnGUI cũ; đổi phím (rebinding) chưa làm (Controls chỉ hiển thị).
- Bản cũ các script đã sửa: Catalog/backups/ui_v2_2026-10-28/.

## §80 Thanh máu quái rõ hơn (28/10 01:23)
- RequiemHUD: thanh 84x14 (nền mực, viền đen 2px, gạch vàng trên), đỏ đậm có vệt sáng, vệt máu vừa mất (giữ 0,4 s), số máu 18px trắng viền đen ở trên.
- EnemyHealthBar: lấy sprite thân = EnemyAI.Body (trước đây GetComponentInChildren có thể trúng bóng đổ/VFX con -> thanh nằm giữa người); đỉnh đầu tính theo bounds khung hình hiện tại, làm mượt.

## §81 Phông chữ UI bị mờ (28/10)
- Nguyên nhân chính: theme mặc định (`unity-theme://default`) đặt `-unity-font-definition` (Inter) → `-unity-font` trong Requiem.uss bị bỏ qua, chữ ra phông mặc định mảnh. Cộng thêm cỡ 15–20 px tham chiếu bị thu nhỏ trong Game view nhỏ, viền SDF 1 px làm nét mảnh, Game view Scale > 1x làm nhòe.
- Sửa: `RequiemUI.ApplyFont()` gán `style.unityFontDefinition = FontDefinition.FromFont(...)` cho mọi chữ tạo qua `RequiemUI.Text` (sr-title / sr-cd / sr-count dùng RequiemTitle, còn lại RequiemBody). Mọi chữ UI phải đi qua `RequiemUI.Text`.
- USS: tăng cỡ (thân 24, small 22, tiny 19, HP 24, zone 38, boss 38, nút 27), bỏ outline, dùng bóng đổ cứng `2px 2px 0 rgb(8,4,12)` (tiêu đề 3–6 px), màu chữ sáng hơn rgb(244,234,212). Cỡ tối thiểu: 19 px tham chiếu.
- Font .meta: fontRenderingMode 2 (Hinted Raster).
- Backup: Catalog/backups/ui_v2_2026-10-28/RequiemUI.cs, Requiem.uss.
- Dải gợi ý / nhắc "Rest" bị lệch phải: `left:50% + translate:-50%` với phần tử absolute không đặt width → Yoga bóp bề rộng còn (cha - left) = nửa màn, câu dài tràn sang phải. Quy ước: canh giữa bằng hàng phủ `left:0; right:0; align-items:center` + hộp con relative (`.sr-hint` > `.sr-hint-band`, `.sr-prompt-row` > `.sr-prompt`). Không dùng left:50%+translate cho khối co theo nội dung.

## §82 Tương tác bằng T + làm sáng vật tương tác (28/10)
- `Scripts/Map/InteractionSystem.cs` (mới): `Interactable` {rect, anchor, verb/verbNow, available, use, clue, range 0.55}. `InteractionSystem.Add()` tự tạo GO.
  Focus = vật gần nhất trong range (ưu tiên gần tâm). Light2D vàng: focus 1.4 thở, trong 3,2 đv 0.6 (đã xem 0.3). Manh mối chưa xem: sao vàng 9px lấp lánh thấy từ 8 đv. Nhấn Interact -> use().
- HUD: `.sr-iprompt` "[T] Read/Examine/Talk/Ring/Pull/Use/Close" nổi trên anchor. Rest prompt + T-rest (RequiemMenus) chỉ khi Focus == null.
- Director: trigger "sign" KHÔNG tự hiện nữa, đăng ký vào InteractionSystem (RegisterSigns). Tự hiện chỉ khi: thuộc tính `auto=true`, chữ bắt đầu "Press ", hoặc tên trong AutoSignNames (r1_hatch, r2_spout_hint, stair_sign). Động từ: thuộc tính `verb`, hoặc theo tên (sexton/seer=Talk; poster/note/ledger/grave/vey_/exit/hub_south/door_hint/notice=Read; còn lại Examine).
- MapMechanics: note/cipher mở bằng T (T lần nữa / Esc / đi xa >1,1 đv để đóng); PanelOpen gồm cả note + frame vừa đóng (Esc không mở Pause). Chuông, cần gạt, vòng số, bàn điều khiển: CHỈ T (chém không còn tác dụng). Đòn chém: chỉ van + vật cản gỗ (`Activate`). Đã bỏ glow/prompt OnGUI cũ.
- Chuông sai nốt: dơi thả ở `FrontSpot` (dưới/phía trước chuông, chỗ không có collider đặc) thay cho y+0,9 sau chuông.
- Việc còn lại: một số bia/manh mối đặt ở vùng không có prop phù hợp -> đã có đốm sáng đánh dấu, nhưng nên bổ sung prop (map3_vale.py; Map2 chỉ sửa tay, không --force).

## §83 Map 3: soát lore, ghi chú, câu đố Vey + cửa sổ hoa hồng (28/10)
- Phân tích: khoá Vey ở cửa hầm nhưng mở cổng khác; manh mối rải dọc V4 lẫn trận đánh; thiếu mắt xích Isolde; nhà nguyện là "cần gạt"
  giả mở cổng rào cách 7 ô; nhiều ghi chú lặp/đặt sai. Người dùng chọn: khoá trên cổng + khu mộ gia tộc; câu đố kính thật + đi xuyên nhà nguyện;
  nghĩa trang ca đoàn + lối tắt.
- P2 The Vey Gate: cipher `vey_lock` (22,55,4,2) NGAY TRÊN cổng vey_inner (23-24,57), đèn tím `vey_gate_lock` tắt khi vey_open.
  Khu mộ gia tộc nửa đông V5 (y50.6, đặt LỘN thứ tự): Lucan(son of Casimir, last) / Aurel(first) / quách Isolde (SR crypt coffin - vertical - 1:
  "daughter of Aurel... no date of death") / Casimir(son of Isolde); Marek (người hầu, mồi giả) ở góc (28.4,54.6). Bia V4 giữ làm mộ thường (không đọc).
  Bỏ vey_mural. Zone v5 = "House Vey".
- P5 The Rose Window: mech mới `rosewindow` (MapMechanics): bảng 2x2 mảnh kính, click xoay 90° (chuột phải ngược), mảnh đúng có viền vàng;
  `picture`/`truth` = tên tranh (object ẩn `picpal_<tên>`), `start` "1,2,3,1", `sets` window_solved, `solved_hint`. Giải -> tranh sự thật hiện dần.
  Ảnh: SR-extra/SR rose night|gold|truth.png (PixelLab create_image_pro 128 + đổi màu), SR chapel closed/open.png (cửa gỗ cài then / cửa mở ánh hầm + kính đỏ).
  toggle `chapel_door_state` đổi closed->open. Portal `chapel_stair` (cửa nhà nguyện) -> (68,84.9) trước hầm `crypt - 5` (68,83.4) ngoài rào nam;
  portal `tunnel_up` ngược lại (lối tắt). Đã BỎ cổng altar_gate + lever altar_lever + biển chapel_door; rào nam V10 kín.
  Book of Hours (note) có `picture=rose_gold` (DrawNote vẽ tranh). Bảng OnGUI (note/cipher/window) dùng phông RequiemBody/Title.
- Nghĩa trang ca đoàn (V10 đông): giữ 2 hàng mộ (y66, 69.4), chòi "little house - 2" (77.6,76.4), điểm nghỉ rest_chapel (74,77.4), bình máu choir_flask.
- Ghi chú bỏ: outfall_sign, stair_sign, unnamed_stones (gộp vào unnamed_rubbing "Field of the Unnamed" ở gốc cây), vey_coffins, chapel_door.
  sexton -> `sexton_door_note` (giấy ghim cửa chòi, mẹo P1). naming_register dời vào sân nhà nguyện (56.6,74.2).
- Generator: GHOST[0]=True -> put() không đặt nhưng vẫn rút rng (bỏ đồ không xáo phần sau). Ghim rng sau địa hình:
  out/map3_rng_after_terrain.pkl (lấy từ bản 27/10) -> đổi OPEN/PATHS không xê dịch đồ ngẫu nhiên. Xoá pkl nếu muốn ngẫu nhiên lại.
  cemkit SR-extra thêm CUỐI: "SR chapel *", "SR rose *". Self-check bỏ altar_gate, stage cuối ["v10","v11","v12","exit"]. TỔNG ĐẠT hết.
- Quest Map3: pre West Bell = "The Vey Gate" (24,56); bước Morrow pre v11_started -> "Stair beneath the Chapel" (61.5,74.5). Minimap bake lại.
- Bản trước: Catalog/backups/map3_lore_2026-10-28/ (py, cemkit, tmx, stamp, quest json); MapMechanics.pre_rose.cs ở backups/ui_v2_2026-10-28/.
- CẦN trong Unity: Dựng scene Map 3 (đèn mới) -> Dựng quái Map 3 -> chơi thử.
- (28/10 v2, theo góp ý "nhà chắn lối vào phi logic") Bỏ hầm crypt - 5 chắn hành lang + portal tunnel_up. Thay bằng:
  * Miệng giếng thang chìm dưới đất "SR cellar stair" (64x60, vẽ tay, SR-extra; cemkit glob "SR cellar *" CUỐI) ở BÊN HÔNG sân nhỏ trước cổng
    đại lăng (OPEN thêm rect 62,81,12,5); portal chapel_stair -> (64,85.3) ngay trước miệng giếng.
  * Cổng nam sân nhà nguyện trở lại (gate `mausoleum_gate`, rào màu 3) nhưng CÀI THEN PHÍA NGOÀI: lever `mausoleum_bar` (phía nam, verb
    "Lift the bar", range 0.2) -> cờ mausoleum_gate_open = lối tắt về điểm nghỉ ca đoàn. Biển `barred_gate` phía trong (hide_when cờ đó).
  * Code: Interactable `range` đọc từ thuộc tính `range` (mech + sign); sign hỗ trợ `hide_when`. Self-check thêm stage mausoleum_gate: ĐẠT hết.
- (28/10 03:3x) VẬT MANG CHỮ cho ghi chú Map 3 — ảnh SR-extra "SR note *" (cemkit glob CUỐI): paper/scrap/letter (giấy Old Prison màu 2, tối bớt),
  book open/closed (Crypt), lectern (Crypt book support 3 + sách mở), barrel (thùng Old Prison + lệnh trên nắp), board (Grass Land sign 10, gỗ xám).
  Đặt theo logic (đã soát lại sau góp ý "cố tình, không tự nhiên"):
  * sexton_door_note: giấy ghim GIỮA cánh cửa chòi. bl_orders: trên nắp thùng tiếp tế cạnh chòi gác (không dán tường như chòi Sexton).
  * naming_register: KHÔNG bục ngoài trời -> 3 trang xé gió lùa từ cửa nhà nguyện rải về phía tây. book_of_hours: hài cốt người hành hương
    gục bên bể Naming + sách mở rơi khỏi tay. sermon: bục giảng (ngoài trời, hợp) trước tượng.
  * peer_diary: sổ rơi cạnh đống đất trước hầm bị cạy (V4). hymn_sheet: xác ca viên + tờ thánh ca (V7). soldier_letter: xác lính dưới cọc sọ
    ở đầu cầu V9 + thư. unnamed_rubbing: mảnh giấy ở chân bia phía nam vòng X1. mass_grave: bảng gỗ cắm vội.
  * Mọi đồ ghi chú dùng occ=False (wrapper put() có tham số occ) để không đổi lưới chiếm chỗ -> rừng cây không xê dịch.

## §84 Đổi quy tắc làm việc với Unity + báo cáo (29/10)
- Thay quy ước ở §(dòng "QUY ƯỚC MỚI… Không điều khiển Unity nữa"): test đơn giản -> người dùng tự test theo hướng dẫn từng bước của Claude;
  Claude chỉ thao tác Unity (computer use) khi đã hỏi và được đồng ý.
- Sau mỗi đợt: báo cáo Nhiệm vụ đã làm / Lỗi đã sửa / Cần test + hướng dẫn / Còn lại. Chi tiết: HANDOFF mục 2, quy tắc 2 và 14.

## §85 Minimap: hết viền đen ở rìa map (30/10)
- Triệu chứng: Map 1 (Millbrook Farm, góc đông nam) / Map 2 gần mép -> minimap hiện mảng đen bên phải/dưới.
- Nguyên nhân: minimap luôn đặt người chơi ở TÂM khung (34 ô); đứng cách mép < 17 ô thì khung nhìn ra ngoài ảnh -> lộ nền khung (0.04,0.02,0.06). Ảnh PNG không lỗi.
- Sửa: RequiemHUD.cs — kẹp vị trí ảnh (MinimapOffset) để ảnh luôn phủ kín khung; ở rìa ảnh đứng yên, mũi tên người chơi rời tâm; điểm nhiệm vụ / mũi tên mép tính lại theo vị trí ảnh. Áp dụng cho mọi map.
- Sao lưu: Catalog/backups/minimap_2026-10-30/RequiemHUD.cs
- Ghi nhận (chưa sửa): ảnh minimap Map 2 có vài vật lạc ở góc trên trái (ô ~0–2, 0–6) — có thể là object/tile thừa ở gốc .tmx.

## §86 Tái cấu trúc — Giai đoạn 0 (30/10)
Kế hoạch 7 giai đoạn (0–6): xem phân tích 30/10 (namespace/asmdef, tách MapEncounterDirector, MapMechanics theo loại, BossBase, EnemyDefinition SO, tài liệu + test).
- DevTools/DevCheats.cs (MỚI): phím test F1–F8 chuyển khỏi PlayerCombatPrototype. Bọc `#if UNITY_EDITOR || DEVELOPMENT_BUILD` -> bản Release không có. Tự gắn bằng RuntimeInitializeOnLoadMethod, không cần kéo vào scene.
  PlayerCombatPrototype.GodMode giờ là property get/set.
- Core/TiledProps.cs (MỚI): extension GetString/GetInt/GetFloat/GetBool cho SuperCustomProperties. Str/Int/Float/Bool trong MapEncounterDirector, MapMechanics, ZoneAmbience, Map2SceneBuilder chỉ còn gọi về đây (hằng số tên thuộc tính để tới GĐ 2–3 khi tách file).
- Gỡ HUD cũ SimpleCombatHUD: xoá component khỏi 3 scene Map (YAML), bỏ khỏi Map1EnemyBuilder + RequiemUI; file chuyển ra _to_delete/legacy_2026-10-30/.
- Sao lưu: Catalog/backups/refactor_p0_2026-10-30/ (kèm scenes/).
- Còn: Assets/_Recovery/ có 6 scene khôi phục cũ (chờ người dùng quyết).

## §87 Tái cấu trúc — Giai đoạn 1: thư mục + namespace + asmdef (30/10)
- 6 asmdef: SoulRequiem.Core / Gameplay / Visual / UI / DevTools / Editor; namespace SoulRequiem.<tên> (Editor: SoulRequiem.EditorTools). Sơ đồ + luật phụ thuộc: ARCHITECTURE.md (MỚI, ở gốc project).
- Di chuyển .cs KÈM .meta (giữ GUID -> scene/prefab không mất script). Bảng cũ->mới: Catalog/backups/refactor_p1_2026-10-30/ (+ tar toàn bộ Scripts/Editor trước khi đổi). Thư mục rỗng cũ -> _to_delete/empty_folders_2026-10-30/.
- Gỡ vòng phụ thuộc: FxLighting -> Core; SceneTransition/GameSession/Achievements/ExitBeacon -> Gameplay/Flow; RequiemUI.Clean -> Core/TextUtil.Clean (RequiemUI.Clean vẫn còn, gọi về); InteractionSystem đọc GameInput.Blocked thay RequiemMenus.Open.
- AnimDebugLogger bọc #if UNITY_EDITOR || DEVELOPMENT_BUILD.
- Sửa: RequiemMenus.Update bỏ qua khi layer == null (domain reload lúc đang Play) -> hết NullReferenceException khi bấm Esc.

## §88 Tái cấu trúc — Giai đoạn 2: tách MapEncounterDirector (30/10)
- MapEncounterDirector (711 dòng) -> thư mục Gameplay/World/Encounters/: MapEncounterDirector (luồng khu/đợt + hồi sinh, ~290 dòng), MapSchema (hằng số layer/type/thuộc tính Tiled), MapLayout (đọc map), WorldFlags, GateController, EnemySpawner, MapTriggerHandler, MapEncounterDirector.DevTools.cs (partial, #if).
  Component trong scene GIỮ NGUYÊN (file .cs + .meta chuyển cùng, field Inspector không đổi) -> không phải dựng lại scene.
- Core/Hints.cs (MỚI): Hints.Show/Clear/Text/Until/Visible thay director.ShowHint/HintText/HintUntil/ClearHint. Director.Awake gọi Hints.Clear().
- Cờ: dir.HasFlag/HasFlags/SetFlag(x,false)/FlagSet -> dir.Flags.Has/HasAll/Set(x)/Changed. Thứ tự giữ nguyên: Changed (cơ quan) trước, mở cổng sau.
- 72 chỗ gọi ở 15 file đã đổi. Sao lưu: Catalog/backups/refactor_p2_before.tar.

## §89 Tái cấu trúc — Giai đoạn 3: MapMechanics theo loại + bảng UI Toolkit (30/10)
- Trước đó: GĐ0–2 đã biên dịch được (6 assembly). Sửa: asmdef UI + Editor thiếu Unity.InputSystem (CS0012 ở RequiemHUD/RequiemMenus).
- MapMechanics (1.564 dòng) -> Gameplay/World/Mechanics/: MapMechanics (host ~200 dòng, .meta cũ giữ GUID), MapMechanics.DevTools.cs (F5), Mechanism (nền), MechanismContext, MechanismRegistry (Type lạ -> cảnh báo), 19 class theo loại trong Hazards/ Effects/ Puzzles/, PipeNetwork (logic ống thuần), NoteMarker.
  ritual_pillar (Map 3) đăng ký là PassiveMechanism (chỉ là mốc cho boss Morrow).
- Bảng ghi chú / khoá ký tự / cửa sổ hoa hồng: OnGUI -> UI Toolkit. Dữ liệu: Mechanics/Panels/MechanismPanels.cs (Gameplay); vẽ: UI/MechanismPanelView.cs (RequiemUI tự gắn). MapMechanics.PanelOpen = MechanismPanels.BlocksInput.
- File cũ: _to_delete/legacy_2026-10-30/MapMechanics_monolith.cs (+ bản sao Catalog/backups/MapMechanics_old_2026-10-30.cs). Tar trước GĐ3: Catalog/backups/refactor_p3_before.tar.

## §90 Tái cấu trúc — Giai đoạn 4: người chơi + nhập liệu (30/10)
- Đổi tên PlayerCombatPrototype -> PlayerCombat (file .cs mới + .meta CŨ -> GUID giữ, scene giữ thông số). 19 file đổi theo.
- Tách tính năng ra Gameplay/Player/Abilities/: PlayerMelee, PlayerDodge, PlayerFlask, PlayerStrikes, PlayerHurtReactions, PlayerAnimation, PlayerActionState (class C# thường, PlayerCombat tạo trong Awake). PlayerCombat còn ~200 dòng: thông số + nhận phím + API cho SkillDefinition.
- Bỏ code chết: coroutine Skill() cũ + các trường "Area Skill (F)" (skillDamage, skillRadius, skillCooldown, skillKnockback, skillImpactTime, skillLockTime, groundImpactScale) — kỹ năng F đã chuyển sang GroundSlam.asset từ trước. SkillCooldownLeft bỏ (không ai dùng).
- GameInput.Move: W A S D / mũi tên / cần trái / D-pad; PlayerController + PlayerCombat đọc từ đây. Tay cầm cho mọi GameAction (PadButton).
- File cũ: _to_delete/legacy_2026-10-30/PlayerCombatPrototype_old.cs. Tar trước GĐ4: Catalog/backups/refactor_p4_before.tar.

## §91 Tái cấu trúc — Giai đoạn 5 (gọn): EnemyDefinition + công cụ boss chung (30/10)
- Xác nhận: GĐ3 + GĐ4 biên dịch được lúc 12:44 (6 assembly, không lỗi).
- 5a: Gameplay/Enemies/Data/EnemyDefinition.cs (ScriptableObject: maxHp, immuneToCrowdControl, mass + EnemyStats = mọi chỉ số số/enum của EnemyAI, trừ attackTime/attack2Time).
  EnemyAI.definition: Awake gọi ApplyTo (JsonUtility chép theo tên field) + giữ attackTime ≥ windup + 0.1. Health thêm getter ImmuneToCrowdControl.
  Editor/SoulRequiem/EnemyDefinitionTools.cs: menu "Soul Requiem ▸ Quái ▸ Tạo EnemyDefinition từ prefab" (tạo asset từ prefab hiện có, giữ chỉnh tay) + ApplyOrCreate gọi trong 3 builder (asset thắng code).
- 5b (không viết lại AI boss): Enemies/Toolkit/BulletPatterns (Ring/Fan/Direction); CameraFollow.KickMain; EnemyAI.SortingLayer/SortingOrder/MatchSorting.
  Dùng ở: EnemyAI (gai), PlagueEye + SpecimenZero (vòng đạn, lớp vẽ đạn), BossSkillsBase/LanternKnight (giật camera), AncientEnt/LanternKnight/BossSkillsBase (lớp vẽ).
- Quyết định (người dùng chọn 30/10): KHÔNG ép 9 boss kế thừa BossSkillsBase (rủi ro viết lại hành vi đã cân bằng).
- Tar trước GĐ5: Catalog/backups/refactor_p5_before.tar.

## §92 Lantern Knight: soát animation + dọn đồ không dùng (31/10)
- Soát sprite PixelLab (Map3/Sprites/Lantern_Knight, khung 176 px, chân 0.1136): Slam chạm đất khung 7 = slamWindup 6/8 ✓, ChargePrep 7 khung/10 fps = 0,7 s ✓.
  Lỗi hình cần vẽ lại trong PixelLab: Death (khung 3–8 quay mặt ra trước), Stagger_S (khung 6+ quay nghiêng trái), Vow_N (khung 4–9 thấy tay + chuôi kiếm xuyên qua lưng, mất huy hiệu),
  Slam_N (khung 7–11 đổi dáng), ChargePrep_S (kiếm đổi bên -> nhảy tay sang Dash_S), ChargePrep_N (gần như không có dấu hiệu). Số khung lệch hướng: Idle 6/9/9, Dash 6/7/8.
- Code: xoá Cleave() + trường cleave* (không còn gọi từ 27/10). Bước vòng quanh Ren: EnemyAI.LookAtPlayer (mặt luôn hướng Ren) + EnemyAI.MoveAnimSpeed (nhịp chân theo tốc độ 45%, hết trượt).
  Builder Map 3: bỏ k.cleave*, bỏ v.swing m3_lk_cleave, sửa ghi chú khung.
- XOÁ (người dùng yêu cầu, có sao lưu Catalog/backups/unused_anims_2026-10-31/unused_anims.tar): clip Lantern_Knight_Attack/_N/_S, Lantern_Knight_Recover, Plague_Eye_Cast;
  thư mục sprite cũ Map3/Sprites/Lantern_Knight/_v1~ và Map3/Sprites/Lantern_Knight_v2~; VFX Resources/VFX/m3_lk_cleave_E/N/S.
  (Catalog/v2/tools/vfx_m3_manifest2.json vẫn liệt kê m3_lk_cleave — đừng import lại.)
- 31/10 (tiếp): Vow hướng N dùng hình Vow_S (người dùng chọn). Builder Map 3: hàm Borrow(list, "Vow_N", "Vow_S"). Đã xoá Lantern_Knight_Vow_N.png (sao lưu trong unused_anims.tar).

## §93 AI quái: né vật cản + tầm nhìn + chống trap (31/10)
- Người dùng chọn: "Né vật cản + chống trap" (không A*), bị trap thì "Rút lui & hồi máu".
- MỚI Combat/LineOfSight.cs (IsSolid / Blocked / CanSee / Clearance; nước chặn đi không chặn nhìn), Enemies/Navigation/EnemySteering.cs (16 hướng, LookAhead 0,7, tách đàn 0,4, kẹt 0,5 s -> đổi phía).
- PlayerStrikes.Damage: bỏ quái bị vật cản che (tầm nhìn tính từ CHÂN người chơi).
- EnemyAI: Chase + quay về nhà dùng steering; aggro cần thấy người chơi (hoặc d <= 35% aggroRange); HitPlayer cần tầm nhìn;
  CheckTrapped (Melee/SpikeBurst, không boss/bay): bị đánh trong 2,5 s + 2 s không giảm khoảng cách -> StartReturning (hồi 50%/s như dây xích) + reaggroGuard 4 s.
  Quay về nhà quá 6 s chưa tới -> lấy chỗ đang đứng làm nhà.
- Sao lưu: Catalog/backups/ai_nav_before_2026-10-31.tar.


## §94 Viết lại cốt truyện: giáo hội thành ma giáo + động cơ cá nhân cho Ren (31/10)
- Lý do (phân tích): bản cũ thiếu "mong muốn" của Ren → đánh goblin/Grok vô cớ (Map 1), Map 2 lẽ ra là "thoát ra" nhưng giao việc cứu thế giới;
  lore dùng làm động cơ; phản diện vắng mặt tới cuối Map 3; tên "The Silencing" lộ đáp án.
- Người dùng chốt: Ichor→**Ether**; Skill→**Mantra** (chỉ chữ hiển thị); The Saint→**The Ascended** (Vael là **nam** → "ngài");
  The Silencing→**The Requiem**; kẻ giết thánh = quý tộc thời xưa (sợ mất quyền); Project ORIGIN→**The Vessel**; boss Map 1 giữ **Grok**;
  người thuê = **Todd** (thay Tobin). Ren = thợ săn tiền thưởng, hợp đồng giết Grok (KHÔNG do Morrow thuê).
- Map 2: hạ Plague Eye → cổ vật Con Mắt nhập vào Ren, cho bộ Mantra mới. Giáo hội có tiên tri, săn Ren để lấy mắt cho lễ hồi sinh; Ren chỉ muốn về nhà lĩnh thưởng.
- Map 3: hạ Morrow → trước khi chết niệm chú ném Ren qua cổng không gian sang Map 4.
- Vá lỗ hổng (người dùng đồng ý): (1) tiên tri "Con Mắt được giải thoát bởi kẻ không có tên" — Plague Eye khống chế linh hồn có Tên nên giáo hội
  không tự lấy được; (2) Con Mắt hoà vào linh hồn Ren → phải tách bằng nghi lễ → Morrow gửi Ren CÒN SỐNG tới nơi làm lễ;
  (3) 3 chuông cộng hưởng Con Mắt (làm lộ Ren) → lý do làm im; (4) Mantra = câu kinh cổ của giáo hội thời chính đạo.
- Bộ Mantra Con Mắt (người dùng thiết kế): I 5 cầu xanh bám mục tiêu gây Burn; II khiên bất tử 3 s; III laser một tay theo chuột 5 s, đứng yên.
  Đề xuất tên: Seeker Embers / Veil of the Ascended / Judgement Ray. Cần thêm trạng thái Burn vào Health.
  Câu hỏi mở: lắp theo bộ (chiếm 3 ô) hay lẻ; "chỉ định" = gần nhất hay theo chuột; Q huỷ laser; tay cầm ngắm cần phải; sprite niệm chú; màu xanh = màu Ether chung.
- Tên tạm chờ duyệt: *Church of Mercy* (giáo hội trước khi biến chất).
- Đã ghi: HANDOFF mục 6 (thay toàn bộ). Chưa đổi chữ trong game. Sao lưu: Catalog/backups/story_2026-10-31/.

## §95 Map 1: phân tích tutorial + bố cục + kế hoạch sửa (31/10) — TẠM DỪNG, chờ mua gói Village
- **Bước 1 ĐÃ LÀM (chờ người dùng test):** token phím trong gợi ý. `Core/GameInput.ExpandKeys` thay {attack} {roll} {heal}
  {skill1..3} {interact} {map} {pause} {move}; `Core/Hints.Show` tự gọi. `MapTriggerHandler.cs:143` "press E" -> "press {heal}".
  Test: Ctrl+R, Console sạch; Play Map 1, F3 tới Hollow Caves, nhặt bình -> "(press E to drink)". Sao lưu: Catalog/backups/map1_rework_2026-10-31/step1/.
  Lưu ý: biển có chữ bắt đầu "Press " tự hiện không cần T (MapTriggerHandler.AutoSign) -> giữ tiền tố "Press " khi viết lại.
- **Map 1 .tmx ĐÃ SỬA TAY** (md5 fa27… ≠ stamp 2ee3…): KHÔNG chạy map1_greystone.py nữa. Mọi sửa = sửa thẳng lớp logic trong .tmx
  (zones/spawns/triggers/gates/blockers/props) bằng script, sao lưu trước, xuất ảnh QA ngoài Assets.
- **Lỗi tutorial tìm thấy:** (1) khu zf không có quái -> "zf_cleared" bật ngay, nhiệm vụ bù nhìn tự xong, bù nhìn là obstacle không chém được
  -> thao tác chém không được dạy; (2) Greystone Camp 10 quái/5 loại + dạy F cùng lúc (mật độ 46 ô/quái, khu khác 100–200);
  (3) gợi ý Ruins "strike from behind" — game KHÔNG có cơ chế đánh sau lưng; (4) gợi ý ghi cứng phím; chưa dạy: nghỉ/chết-hồi sinh,
  tín hiệu cam (đòn mạnh) / vòng đỏ (điểm rơi), trạng thái xấu; green_tonic không giải thích; tờ Tobin nói "sói" nhưng map không có sói.
- **Bố cục (đo collider):** đường chữ S tốt, giữ. Rừng chết x0–95,y66–88 (~20% map). Ruins Path lớn nhất (1041 ô đi được, 5 quái).
  "Hollow Caves" là bãi cỏ có hố, không có hang. Đồng cỏ->hang->mỏ ~110 ô cỏ giống nhau. Dải đi được ngoài khu: mép trái sau Healer's Tent
  (x2–3,y4–28), dải y64–66 (chờ người dùng xác nhận có tới được không). Đấu trường Grok nhỏ, 6 cột chưa có code va chạm.
  Ảnh QA: Catalog/v2/out/qa/map1_layout_overlay.png.
- **Kế hoạch (theo khu, mỗi bước người dùng test):**
  2 Millbrook -> **làng** (dùng gói "Epic RPG World – The Village" V1.5, người dùng sẽ mua, giải nén vào Desktop\Catalog\assets\Village):
    2a nhập gói (PNG trước .meta sau, tileset Tiled), 2b phát quang rừng chết x≈70–95,y66–88 (xoá cây/collider, tô nền) + mở rộng zf,
    2c 4–6 nhà dựng sẵn + **cối xay nước trên suối** (Mill-brook) + quảng trường giếng + bảng tin; nhà = object lớp props, collider chân tường,
    2d toán cướp 2 Goblin_Torch đốt một nhà (dạy chém), hợp đồng Todd trên bảng tin (bản nháp: "Hunter — the purse is yours the day you bring
    me proof Grok is dead… Fifty silver, as agreed. — Todd, Millbrook"), bỏ bước bù nhìn; tuỳ chọn NPC Todd nói chuyện bằng T.
  3 Brookside+Meadow: bỏ cổng khoá khu hoang dã, dấu vết toán cướp, giải thích điểm nghỉ lần đầu, {roll}, chặn dải đất thừa.
  4 Caves+Camp: dạy F ở hang (đàn quái hang sinh), Camp 10 -> ~7 quái, đổi tên Hollow Caves (đề xuất "The Burrows") hoặc thêm cửa hang.
  5 Ruins+Stone Ring: gợi ý "phản đòn khi nó khựng"; lấp chỗ trống Ruins bằng dấu vết; Grok lao vào cột -> choáng; kết màn nhặt nanh Grok
    -> Stone Ring sụp -> rơi xuống Map 2 (bỏ nhiệm vụ "Descend" tự nguyện).
  6 Gợi ý lần đầu (chết, đòn cam, vòng đỏ, độc) + gợi ý thích ứng (trúng gai 3 lần -> nhắc lăn).
- **Câu hỏi còn mở:** số tiền hợp đồng; Goblin_Torch sẵn hay tạo Goblin_Raider yếu; bỏ hẳn bù nhìn; Hollow Caves đổi tên hay thêm hang;
  cơ chế Grok-cột; giữ rừng chết hay phát quang làm làng (đã nghiêng về làng); NPC dân làng.

## §96 Map 2: sửa lỗi mất cơ quan + thêm điểm nghỉ (31/10) — Bước A, B (chờ test)
- Phân tích Map 2 (31/10): boss dùng môi trường tốt (Ram/tường, Toad/bể, Eye/rút bể); lỗi: chỉ 1 điểm nghỉ cho map 128x108,
  hub thực chất tuyến tính (Boiler -> Culture -> Pump; Lab tuỳ lúc), 12/46 quái là Spider, mục tiêu chưa theo mạch "thoát ra".
  Kế hoạch: A khôi phục cơ quan, B điểm nghỉ, C viết lại chữ "thoát ra" + đổi tên Ether/The Vessel/Ascended, D thay bớt nhện,
  E cổ vật Con Mắt -> bộ Mantra (sau). Đề xuất màu: Ether xanh lá (thánh huyết tha hoá), Mantra xanh dương (đã thanh lọc qua Ren).
- **A. Nguyên nhân:** đợt chỉnh đèn 24/10 vô tình xoá khỏi .tmx: 6 cần gạt buồng giam (props `cell_lever_1..6` + mechanics lever +
  đèn `cell_lever_glow_*`), tờ `incident_report` (manh mối "chìa ở chỗ Brakk, Boiler Room") và toggle `report_chest_opened`.
  Hệ quả: gợi ý Specimen Cells trỏ vào cần gạt không tồn tại, rương `cell_4_potion` khoá vĩnh viễn, cửa Culture Hall không có manh mối.
  **Đã chép nguyên văn 20 object** từ `Catalog/backups/map2_lights_2026-10-24/map2_before_lights.tmx` (tileset giống hệt, id không trùng).
- **B. Điểm nghỉ:** `hub_rest` trước đây vô hình (đèn xanh cũng mất 24/10). Mỗi điểm nghỉ giờ = 3 nến `crypt_candle` (gid 14367, không collider)
  + đèn xanh `<tên>_light` (#9fd0ff r4) + đèn nến `<tên>_flame`. Thêm 4 trigger checkpoint: `rest_cistern` (84.5,58.4, r4, trước Plague Eye),
  `rest_cells` (6.5,67.4, spec, trước Brood Toad), `rest_office` (114.5,75.5, office, trước Ichor Ram), `rest_reliquary` (49.5,92.4, s3, trước Specimen Zero).
  Vị trí chọn theo đường đi thật (BFS trên collider); nến kiểm tra đều trên sàn.
- Sao lưu: `Catalog/backups/map2_fix_2026-10-31/before_A.tmx`, `before_B.tmx`. Ảnh QA: `Catalog/v2/out/qa/m2_rest_*.png`, `m2_lever_cells.png`.
- Sửa thẳng .tmx bằng script chèn văn bản (không chạy generator, không ET.write lại cả file).
- **Cần chạy:** Soul Requiem ▸ Dựng scene Map 2 (Sewers) — đèn Light2D lấy từ lớp mechanics.

## §97 Sửa: đánh Plague Eye không mất máu (01/11)
- Triệu chứng: chém / kỹ năng trúng Plague Eye nhưng thanh máu boss không giảm.
- Nguyên nhân: §93 (31/10) bắt đòn người chơi kiểm tra tầm nhìn (`PlayerStrikes` -> `LineOfSight.CanSee`). Hố của boss là collider
  `obstacle` hình chữ nhật 8.2x6.9 ô (`pit`, id 808, lớp obstacle colliders) BAO QUANH boss -> tia từ người chơi luôn chạm thành hố -> huỷ đòn.
- Sửa: `Combat/LineOfSight.Blocked` bỏ qua vật cản chứa một trong hai đầu mút (`OverlapPoint(from) || OverlapPoint(to)`). Áp dụng mọi map.
  Không sửa .tmx. Sao lưu: Catalog/backups/map2_fix_2026-10-31/LineOfSight_before_pitfix.cs. ARCHITECTURE 2.5 đã ghi thêm.

## §98 Map 2 Bước C (chữ "thoát ra" + đổi tên) và Bước D (bớt nhện) (01/11) — chờ test
- **Màu Ether (người dùng chốt):** Ether thuần khiết = XANH DƯƠNG (Mantra của Ren); Ether bị hắc hoá = XANH LÁ (toàn bộ Ether ở Map 2/3).
  Chữ gọi là "tainted Ether" / "green Ether". Đèn + axit xanh lá giữ nguyên.
- **C — .tmx (22 chỗ):** Ichor -> (tainted) Ether; "Saint's Reliquary" -> "Reliquary of the Ascended"; Vael priesthood/seal -> Choir;
  "god-spawn" -> the Eye. Mạch thoát: r1_hatch (trục rơi sập, phải tìm lối khác), r3_well (gió lạnh từ phía đông),
  hub_boss_door (lối thoát duy nhất là outflow sau hố), gallery_pipes solve_text, to_map3 open/depart_text.
  Hồ sơ rec_dossier viết lại = "SEALED - The Vessel": Absalom mang thánh tích con mắt của The Ascended; Specimen Zero = thân vô chủ;
  cấy Mắt làm trí -> cắt ra nuôi Ether -> hát điều khiển goblin; Mắt huỷ linh hồn có Tên (gài tiên tri + Ren miễn nhiễm).
  r6_journal: "The Eye drinks and grows…". Sách purifier: "The Purifier's Rite" (chữ "skill" giữ tới khi đổi UI Skill->Mantra).
- **C — khác:** Quest Map 2: Find a way out of the Undercity / Cross the Ether Well - follow the cold air east / Reach the Sluice Hall /
  Get the pumps running / Drain the cistern at the Pump Control / Get past the Plague Eye / Climb out through the outflow (cờ giữ nguyên).
  Tên hiển thị: Ether Snapper, The Ether Ram (asset EnemyDefinition + Map2EnemyBuilder + RamSkills hint); PipeGridMechanism 2 gợi ý.
  KHOÁ nội bộ giữ nguyên: Ichor_Snapper, Ichor_Ram, cờ ichor_diverted…
- **D:** Spider 14 -> 8. id 646,647 (Gallery đợt 2) -> Plague_Rat; 653,654 (Pump Control) -> Vat_Born, 655 -> Skinned_Hopper;
  660 (Office) -> Plague_Rat. Giữ nhện ở Forgotten Store, Archive, Gallery đợt 1.
- Generator map2_sewers_v2.py KHÔNG đồng bộ các sửa đổi 31/10–01/11 (không bao giờ chạy lại cho Map 2).
- Sao lưu: Catalog/backups/map2_fix_2026-10-31/ (before_C.tmx, before_D.tmx, C_code/).
- Còn lại Map 2: E — cổ vật Con Mắt sau boss -> bộ Mantra (cần code Mantra); lời thì thầm của Plague Eye; Map 3 còn chữ Ichor/Saint (đổi qua generator Map 3).

## §99 Đổi chữ UI Skill -> Mantra (01/11) — chờ test
- Chỉ chữ hiển thị (class/khoá giữ nguyên: SkillDefinition, PlayerLoadout, GameAction.Skill1..3, skill_id, "skill_book", ảnh skill_socket).
- GameInput.ActionName: Mantra I/II/III. RequiemMenus: nút "Mantras" / "Mantras  (rest at a shrine)", "Choose a socket, then the mantra…",
  "You know no mantras yet.", "Remove mantra". TitleScreen: "F/R/C mantras". MapTriggerHandler: "A new mantra…", "You found a book of mantras.".
  PlayerStatus/PlayerLoadout/MorrowSkills: câm -> "mantras are sealed / fall silent". SkillDefinition.displayName mặc định "Mantra".
  Map 2 .tmx book_purifier: "The Purifier's Rite. Earthshatter now also clears poison clouds."
- CHƯA đổi: Map 1 .tmx ("area skill" ở z3 — Map 1 đang tạm dừng, sẽ viết lại khi làm lại Map 1), Map 3 King's Censer (đổi cùng đợt chữ Map 3).
- Sao lưu: Catalog/backups/mantra_ui_2026-11-01/.

## §100 Map 3: đổi chữ theo cốt truyện mới (01/11) — chờ test
- .tmx Map 3 LỆCH stamp (tmx sửa 30/10 12:54, stamp 27/10 68b0810…) — không rõ thay đổi 30/10 là gì, không có bản sao lưu trùng lần dựng cuối.
  => KHÔNG chạy generator. Sửa thẳng chữ trong .tmx (7 chỗ, theo id) + sửa y hệt 6 chuỗi trong maps/map3_vale.py. Stamp GIỮ NGUYÊN
  (để lần sau chạy generator vẫn bị chặn -> nhớ đối chiếu cấu trúc trước khi --force).
- Đổi: kings_relic "Earthshatter now burns away a wider pool of tainted Ether."; naming_font "The tainted Ether…"; ichor_lamp_0..2
  "green, tainted Ether"; rose_window "The Ascended is not rising. He is held down - and the hands on him wear the rings of the great Houses."
  (lộ sự thật quý tộc giết thánh, nối House Vey/Avenue of Peers); book_of_hours "The Glory of the Ascended… kneel around him".
- Đề xuất chờ duyệt (phụ thuộc Bước E Con Mắt): v3 chuông cộng hưởng Con Mắt; bl_orders thêm "điều giáo hội chờ 300 năm";
  to_map4 open/depart_text = Morrow hấp hối mở cửa, ánh tím kéo Ren đi (lâu dài: tự chuyển map khi Morrow chết).
- Sao lưu: Catalog/backups/map3_text_2026-11-01/ (tmx, map3_vale.py, stamp).
- (01/11, đã duyệt + áp) v3: "Three bells toll over the Vale - and the Eye inside you answers each one. Silence them."; bl_orders thêm
  "He carries what the Choir has waited three hundred years for."; to_map4 open "With his last breath, Morrow tears the Requiem Door open…",
  depart "The violet light seizes the Eye inside you - and you with it." (tmx + map3_vale.py; stamp giữ nguyên).

## §101 Bộ Mantra Con Mắt (01/11) — chờ test
- Người dùng chốt: CẢ BỘ chiếm 3 ô (I=F, II=R, III=C); cầu bám quái GẦN NHẤT; Q huỷ laser; hoạt ảnh tạm (không phát Stomp, chỉ hiệu ứng).
- Code: `Player/Skills/SeekerEmbersSkill.cs` (5 cầu, dmg 6 + Burn 5/s x3s, tầm 4.5, CD 6), `VeilSkill.cs` (khiên 3s, CD 20),
  `JudgementRaySkill.cs` (laser 5s, 30 dps tick 0.15, tầm 5, xoay 220°/s theo chuột / cần phải tay cầm, đứng yên, Q huỷ, CD 25;
  tia bị tường chặn trừ vật cản bao quanh quái (hố Plague Eye); tia gọi Struck -> phá được đèn/cơ quan).
  `Combat/EtherVfx.cs` (sprite vẽ bằng code: cầu/vòng/tia, unlit), `Combat/HomingOrb.cs` (MonoBehaviour cầu đuổi; đụng tường thì tan).
  `Health`: `Shield(giây)` (Invulnerable = cờ Roll HOẶC còn khiên -> lăn không tắt khiên), `ApplyBurn(dps, giây)` (tick 0.5s, ánh xanh), Revive xoá cả hai.
  `SkillDefinition`: thêm `setId`, `setSlot`. `PlayerLoadout`: `InSet/SetOf`; Equip chiêu của bộ = lắp cả bộ vào đúng ô; lắp chiêu lẻ vào ô của bộ = tháo cả bộ;
  Unlock 1 chiêu của bộ = mở cả bộ + tự lắp. GameSession lưu theo id từng ô -> qua map giữ nguyên.
- Asset: `Resources/Skills/EyeOrbs|EyeVeil|EyeRay.asset` (id eye_orbs/eye_veil/eye_ray, setId eye, setSlot 0/1/2, chỉnh chỉ số ở Inspector).
  Icon tự vẽ: `Resources/UI/Requiem/ic_mantra_embers|veil|ray.png` (+meta chép từ ic_skill_slam, GUID mới). Meta .cs tự tạo GUID.
- Map 2 .tmx: trigger `eye_relic` (pickup, item=skill, skill_id=eye_orbs, requires=r8_cleared, flag=eye_relic_taken) ở mép BẮC hố (106.9,34.0);
  đèn `eye_relic_glow` #8fd0ff (lit_when r8_cleared, off_when eye_relic_taken). MapTriggerHandler: pickup "skill" giờ đặt cờ `flag` nếu có.
- Phím test F10: mở + lắp bộ Con Mắt (DevCheats).
- Chưa có: sprite cổ vật trên sàn (chỉ có đèn), hoạt ảnh giơ tay của Ren, tự chuyển Map 4 khi Morrow chết.
- Sao lưu: Catalog/backups/eye_mantra_2026-11-01/.
- (01/11, sửa theo góp ý) Khiên: tự đo SpriteRenderer của Ren lúc gọi -> tâm giữa thân, đường kính = chiều cao x ringScale (1.3); không gắn làm con
  (theo vị trí mỗi khung). Laser: va chạm dò ở MẶT ĐẤT (collider quái = vệt chân; trước đây dò ngang tay nên bắn ngang hụt), hitbox quái
  quét rộng hitWidth 0.6 (xuyên mọi con), tường quét mảnh 0.05 theo tâm; tầm 8, hình rộng 0.22.
  Seeker Embers ĐỔI CƠ CHẾ: `Combat/EmberOrbit.cs` (MonoBehaviour gắn vào người chơi) — tối đa 5 cầu bay vòng quanh chân 10 s (vẽ cao 0.2),
  chạm quái -> 8 sát thương con đó + nổ r0.6: 6 sát thương + Burn 4/s x3s cho quái thấy được; mỗi cầu nổ 1 lần; dùng lại = bù đủ 5 + làm mới 10 s
  (không chồng); 1 s cuối nhấp nháy; nửa sau vòng vẽ sau lưng. CD 6 -> 12 (dài hơn thời gian cầu). `HomingOrb.cs` giờ không dùng (giữ lại).
  Cách hiểu "ember 10 giây / tối đa 5" = các CẦU (chờ người dùng xác nhận; nếu là hiệu ứng cháy trên quái thì đổi burnTime).


## §102 Nhân vật khựng khi đánh: sửa chuyển Animator sang Run (01/11) — chờ test
- Nguyên nhân chính: `Iaido -> Run` dùng Has Exit Time (0,4706 = 0,64 s của clip 1,36 s). Exit Time chỉ kiểm tra ĐÚNG 1 khung;
  khung đó code còn khoá (Speed = 0) -> lỡ -> hết khoá nhân vật đã đi nhưng hình vẫn tra kiếm tới hết clip (~0,7 s), rồi Idle, rồi Run.
- `Editor/SoulRequiem/PlayerAnimatorBuilder.cs`: Iaido / Drink / Stagger -> Run bỏ Exit Time, điều kiện `Speed > 0.1` (+ `!Dead`, Stagger thêm `!Stunned`).
  Hurt -> Run giữ Exit Time 1 (đòn nhẹ không khoá chân, bỏ Exit Time thì clip Hurt bị cắt ngay khi đang chạy).
  Thêm Dash -> Run (Exit Time 1, ưu tiên hơn Dash -> Idle). Bỏ hằng `IaidoCancelAt` — thời gian khoá do `PlayerCombat.lockTime` quyết định.
- `Player/Abilities/PlayerAnimation.cs`: `HoldStill()` đặt Speed = 0 ngay khung phát Iaido / Drink / Stagger (chặn nhảy sang Run khung đầu
  khi PlayerController chạy trước PlayerCombat).
- Cần chạy lại menu "Soul Requiem ▸ Dựng Animator nhân vật". Bản cũ: `Logs/claude_preview/_backup_2026-11-01/`.
- Còn lại từ phân tích (chưa làm): đệm phím chém/lăn; camera `pixelsPerUnit` 32 (ảnh PPU 100); `CameraFollow.cs` dòng 105 chú thích nuốt
  `kickDur/kickTime` (giật camera không chạy); Skill/Cast -> Run cũng dùng Exit Time (cùng loại lỗi); nạp sẵn VFX.

## §103 Animation bộ Mantra Con Mắt (01/11) — chờ test
- PixelLab state Rogue Traveler (7db1a01f…): 3 animation custom V3, 8 hướng × 9 khung (khung 0 = khung tham chiếu), 140x140 (hướng north 136x136 như các bộ cũ).
  cast-embers = "casting a spell while keeping the same facing…", cast-veil = "casting a protective ward…",
  ray-hold = "The character plants his feet then thrusts his right…" (bản cũ "firing a beam…" đã xoá trên PixelLab).
- Đã kiểm từng hướng: lỗi xoay mặt về camera (veil E/NE/W, ray NW/W) đã tạo lại; veil East còn hơi xoay ở vài khung (chấp nhận tạm).
- Nhập bằng `Catalog/import_mantra_anims.py <zip>` (--dry để kiểm): giải nén ra Catalog/_dev, nhận diện theo tên, kiểm 8 hướng × 9 khung,
  chép vào `Assets/GamePackage/Rogue Traveler/cast-embers|cast-veil|ray-hold/<hướng>/frame_000..008.png` + .meta theo mẫu
  (Sprite Multiple, Point, không nén, PPU 100, pivot chân); đổi khung lóe vàng ray-hold/south/frame_005 sang xanh Ether (210 px).
- Ảnh xem nhanh: Catalog/previews/anim_<tên>.png. Zip gốc: Downloads/Main_Ronin_Style_Guide_v2.zip.
- Rác PixelLab còn lại: "drinking" (6 khung, đã thay bằng drink_potion) – người dùng tự xoá.
- Cần chạy trong Unity: Soul Requiem ▸ Dựng Animator nhân vật → Xếp lớp nhân vật theo chân → Ctrl+S.
- Cùng loại lỗi §102: CastEmbers/CastVeil -> Run bỏ Exit Time (điều kiện Speed > 0.1, !Dead); `PlayerAnimation.CastEmbers/CastVeil` gọi HoldStill().
  Bản cũ: Catalog/backups/eye_mantra_2026-11-01/anim2/.


## §104 Chiến đấu mượt hơn: đệm phím, huỷ đòn sớm, camera, giật camera, nạp sẵn VFX (01/11) — chờ test
Tiếp §102 (phân tích "khựng khi đánh nhau"). Sao lưu bản trước: `Logs/claude_preview/_backup_2026-11-01b/`.
- **Đệm phím** (`PlayerCombat.Update`, field mới `inputBuffer` = 0,2 s, mục Input): bấm Chém / Lăn lúc chưa làm được thì nhớ 0,2 s,
  làm ngay khi được. Lăn ưu tiên (xoá chém đang nhớ). Mở menu thì bỏ phím đã nhớ. Đặt 0 = như cũ (chỉ khung bấm).
- **Huỷ đòn sớm** (`PlayerMelee.Slash`, field mới `moveCancelAfterHit` = 0,08 s): khoá chân = min(lockTime, hitTime + 0,08) = 0,56 s
  (trước 0,64). Nhịp chém vẫn theo `lockTime` (0,64 s / nhát). Animator Iaido -> Run không Exit Time nên tự khớp.
- **Camera** (`CameraFollow`): field `pixelsPerUnit` (32 trong 3 scene, sai so với ảnh PPU 100 -> nền nhảy ~8 px màn hình mỗi bước)
  đổi tên thành `snapPixelsPerUnit` = 100 để bỏ giá trị cũ đã lưu, không phải sửa file scene.
- **Giật camera** (`CameraFollow.Kick`): `kickDur` / `kickTime` từng nằm trong chú thích -> chưa bao giờ chạy. Đã tách dòng.
  Từ giờ có giật: chém trúng (cameraKick 0,05), dậm đất (x1,4), Lantern Knight (0,05–0,06). Tắt bằng Settings ▸ Screen shake.
- **Skill (dậm đất) -> Run** (`PlayerAnimatorBuilder`): bỏ Exit Time như Iaido; `PlayerAnimation.Stomp` gọi `HoldStill()`.
  (Cast Embers / Veil đã được sửa cùng kiểu trước đó.) Cần chạy lại menu "Dựng Animator nhân vật".
- **Nạp sẵn VFX** (`PlayerCombat.Start` + `PlayerMelee.PreloadVfx`): slash_* (8 hướng), hit, stun_stars, heal_*, ground_impact.
- Chưa làm: va chạm Dynamic–Dynamic người chơi (nặng 1) với quái (2–50) có thể rung khi ép vào quái — chờ người chơi thử báo lại.

## §104 Map 1 làng Millbrook — Bước 1: asset (01/11) — chờ test
- Gói "Epic RPG World - The Village V2.2" đã có trong Assets/GamePackage (người dùng nhập 31/10, Unity tạo meta); bản giải nén gốc: Catalog/assets/Village.
- Bố cục đã chốt (ảnh Catalog/previews/village/map1_village_plan_v2.png): làng x56–128, y62–88; phát quang rừng chết x56–95;
  đường chính bắc–nam x103–106 (lối vào nam = nơi Ren tới → lối ra bắc Brookside); phố đông–tây y75–77, cầu đá trên phố qua suối (x91–96);
  cối xay nước thượng nguồn cầu (x84–91,y64–72) + hồ xay (giữ); quảng trường x98–112,y70–75 (giếng, bảng tin, sạp); tây: 3 nhà, nhà nông + kho,
  nhà bị đốt (2 Goblin_Torch), lò rèn, ruộng rìa nam cạnh suối; đông: nhà Todd (trưởng làng), quán trọ + chuồng, 1 nhà; dân làng đi lại (hệ NPC mới).
  Bỏ ruộng cũ x96–106. Chốt: quái cướp = Goblin_Torch có sẵn; hợp đồng = thư trên bảng tin; tiền thưởng "A hundred silver".
- Script `Catalog/village_step1.py` (không ghi đè file có sẵn): cắt 13 nhà vào `Village V2.2/SR-buildings/vh_*.png` (+ .meta Point, không nén),
  tạo tileset `Tilesets/SR-village-buildings.tsx` (collection), `SR-anim-village-watermill.tsx` (bánh xe 192x224, 9 khung),
  `SR-anim-village-chimney-dark-smoke.tsx` (khói đen 64x64, 24 khung, cho nhà cháy). Đồ lặt vặt dùng thẳng `Village V2.2/TiledMap Editor/tilesets/props(as sprites).tsx`.
  vh_stone_cottage là bản "không mái" (nóc xanh đen) — không dùng. Ảnh xem: Catalog/_dev/village/crops_final.png.
- **Bước 2 ĐÃ LÀM (chờ test)** — `Catalog/v2/village_step2.py` (kiểm md5 trước, giữ CRLF, chỉ thay CSV lớp + xoá/chèn object theo id):
  md5 map1 fa27… -> **985fac23e25ef0faa32c499389808183**; sao lưu `Catalog/backups/map1_village_2026-11-01/step2/`.
  Dựng lại mặt nạ góc wang từ tile sẵn có (100% khớp) rồi chỉ đổi ô có khoá góc thay đổi (water 89, dirt 354, dirt2 132, shade 573 ô).
  - Phát quang x57–96, chân vật y66.5–85.5 (giữ hàng cây bắc y≤66 — ngăn đi tắt sang Thornback Meadow —, rừng tây x<57, hàng cây nam y>85.5):
    xoá 200 cây + 173 bóng cây + hoa/cỏ; xoá ruộng cũ x96–110,y72–85 (90 ô đất bón, rào, cà rốt, 2 bù nhìn + collider).
  - Suối mới từ đáy hồ xuống mép nam: x96–98 (y72–76), x95–97 (y77–82), x94–96 (y83–87); va chạm nước từng hàng, **bỏ trống y78–80 chờ cầu** (bước 3).
  - Đường chính x103–105, y67–87 (nối lối đi cũ phía bắc, không đụng cổng gỗ y≈65); phố y78–80, x57–105 (không cắt sân nhà Todd).
  - Blockers: bỏ 3557/3558/3563/3564/3567/3568/3570 (rừng x0–95) + 3572–3575 (rào ruộng); thêm x0–57 (y67–70, y75–88), x40–57 (y70–75), hàng cây nam x57–96 y86–88.
  - Còn chờ: biển zf_fields ("radishes wilted") và nhiệm vụ bù nhìn trỏ vào ruộng đã xoá -> sửa ở bước 4.
- **Bước 2b (thay bước 2, chờ test)** — người dùng muốn sông to hơn + đường tự nhiên hơn. `Catalog/v2/village_step2b.py` dựng lại TỪ BẢN GỐC
  (fa27…, kiểm file hiện tại = 985fac… trước khi ghi), hình theo đường tâm + nửa bề rộng + nhiễu fbm (`poly_mask`, `blob`); `--dry --preview`
  xuất tmx xem thử ra Catalog/_dev rồi `tmx_render.py`. md5 map1 mới: **93526696e04f54f8a42af6e63f56caa6**
  (đã gỡ tay 3 va chạm nước trùng ở ao x34–41,y72–74 do lỗi lọc — script đã sửa lọc x≥85). Sao lưu bản bước 2: backups/…/step2b/.
  - Sông: rộng 5–6 ô (bằng suối thượng nguồn x86–92), uốn chữ S từ đáy hồ (99,69) -> (96,79.5) -> (93,84.7) -> mép nam (92,89);
    **thắt 4 ô, bờ thẳng (không nhiễu) quanh y79.5 = chỗ bắc cầu**; va chạm nước bỏ trống y78–80 chờ cầu.
  - Đường chính cong nhẹ rộng ~3 ô (103.6,67.5)->(105.3,74.5)->(104.6,79.5)->(105.6,84)->(104.8,89.5); phố uốn theo địa hình, rộng 3 ô
    hẹp dần còn ~2.5 ô ở cuối làng phía tây (58.8,78.2); sân quảng trường hình bầu dục tâm (104.4,77) bán kính 4.6×3.4 tại ngã ba.
- **Bước 2c (chờ test)** — suối Brookside (dưới thác -> hồ, y44–67) nới từ ~4 lên 6–7 ô (`Catalog/v2/village_step2c.py`, chỉ thêm nước,
  giữ nguyên thác x84–88 và đoạn cầu gỗ y49.5–54.5). 61 ô nước mới, 30 va chạm nước bù (bỏ hàng cầu y51–52); xoá 16 hoa/cỏ/đá + 2 ếch nằm
  trong nước mới; dời biển chỉ đường (prop 2144 + collider 3098) sang bờ đông 2 ô. md5 map1: **bbbd0f150a4c02829900c843261a3066**;
  sao lưu bản 2b: backups/…/step2c/. Ảnh so sánh trước/sau: Catalog/previews/village/step2c_compare.png.
- **Bước 2d (chờ test)** — thiết kế lại cả dòng sông chân thác -> mép nam (`Catalog/v2/village_step2d.py`): một đường tâm, nửa bề rộng đổi dần
  (vũng chân thác 4.6 -> sông 3.9–4.2 -> hồ 4.6 -> 3.1–3.4 ở cầu đá làng -> 4.4 hạ lưu), bỏ 2 chỗ thắt (y50 dưới vũng thác còn 5 ô; y76–80 còn 5 ô).
  Bờ thẳng (không nhiễu) quanh cầu gỗ y52 và cầu đá y79.5. Chỉ thêm nước; giữ mọi vật có tên "bridge" + nhóm gates; xoá 2 cây + 2 bóng,
  1 đá (+collider 3099), 1 ếch nằm trong nước mới. Va chạm nước bù: bỏ hàng cầu gỗ y51–52 (x82–95) và hàng cầu đá y78–80.
  md5 map1: bbbd0f… -> **381d205f5c8ae84cddfeff5c3c17d690**; sao lưu backups/…/step2d/. Ảnh so sánh: Catalog/previews/village/s2d_compare.png.
- **Bước 2e (chờ test)** — làm lại TOÀN BỘ dòng sông (`Catalog/v2/village_step2e.py`), md5 map1 381d205f… -> **d22371b5834ccbf62946bf5a55250cd4**,
  sao lưu backups/…/step2e/. Ảnh ghép có vật: Catalog/previews/village/s2e_final.png.
  - Cửa sông trên cao nguyên y35–41 nới 5 -> 7 ô; **thác nước 5 -> 7 cột (x83–89)**: xoá 25 decal SR-waterfall cũ, đặt 35 decal mới
    (cột trái id 0/24/48/72/96, giữa 1/25/…, phải 2/26/…; đáy hàng y=40..44).
  - Vùng x78–106, y42–88 vẽ lại từ đầu (xoá + vẽ): vũng chân thác hình bầu dục (86.6,46.4) 5.3×3.5 thay khối hộp; sông 9–10 ô; hồ bầu dục
    (96.9,67.6) 5.4×3.6; thu dần còn 7–8 ô ở cầu đá (y79.5, bờ thẳng); hạ lưu 9–10 ô. Bờ thẳng quanh cầu gỗ y52.
  - Va chạm nước vùng đó dựng lại toàn bộ (xoá 111 cũ, thêm 51 gộp theo hàng; bỏ hàng cầu gỗ y51–52 x82–95, hàng cầu đá y78–80, chân thác y41–42).
  - Xoá 7 cây (+6 collider khớp đúng tên) đứng trong/sát nước MỚI trên cao nguyên + 1 cây bờ hồ; giữ vịt trên hồ, giữ mọi vật "bridge",
    giữ ambient fx. Rải 11 bụi sậy (vegetation 1–9) / đá nhỏ dọc bờ (tránh cầu, đường, mép quảng trường).
- **Bước 3 ĐÃ LÀM (chờ test)** — `Catalog/v2/village_step3.py`, md5 map1 d22371b5… -> **d4263b5e2a88def392d26d306bb46a56**, sao lưu backups/…/step3/.
  Ảnh ghép (có collider đỏ/xanh): Catalog/previews/village/s3_final.png. Công cụ mới: `Catalog/v2/compose_preview.py` (ghép tile + vật theo Y).
  - Thêm 4 tileset vào map: SR-village-buildings (11749), Village props(as sprites) (11762), SR-anim-village-watermill (12034),
    SR-anim-village-chimney-dark-smoke (12043).
  - Phát quang thêm x36–57 (chân vật y66.5–85.5) + bỏ hàng cây sát mép nam x38–92 (che ruộng); blockers: 3580/3582 thu còn x0–36,
    3583 (hàng cây nam) x36–96, xoá 3581. Phố kéo dài tới x40.5 + lối vào ruộng x75.
  - Dãy bắc phố (chân y76.8): **nhà nông + kho** (47.9, cạnh ao x34–41 — nhà sẽ bị toán cướp đốt ở bước 4), **lò rèn** (64.2),
    **cối xay** (85.0) + **bánh xe quay** trên sông (khung trái 90.6, đáy 77.7); rơm, bao thóc, thùng thóc.
  - **Cầu đá** trên phố x92–98 (lối vào trái + thân 5 ô + lối vào phải, đáy ảnh y81.25) + 2 collider tường cầu.
  - Quảng trường: giếng (101.2,74.2), bảng tin questboard (108.6,74) + giấy, 2 đèn, sạp hàng (bàn + thùng + bao) x99–102,y84–86.
    Giếng cũ sân Todd (prop 2758 + collider 3078) + xô bỏ; biển zf_well (3017) dời theo giếng mới.
  - **Quán trọ** (vh_thatch_stone_house) chân (113.2,87.7) phía nam sân Todd; thùng + ghế.
  - 3 ruộng rìa nam có rào + cổng + đất bón wang ("fertilized soil -standard") + hoa màu: lúa mì x41–55, cà rốt x58–72,
    bắp cải x77–90 (cạnh sông, bù nhìn). Collider: chân nhà (95% rộng × 4.2–5.2 ô), rào (chừa cổng), giếng, bảng tin, đèn, sạp.
  - Bỏ nhà ván (plank house) vì không đủ chỗ; còn chờ bước 4: nhà bị đốt + toán cướp, nội dung bảng tin Todd, dời player_start xuống lối vào nam,
    mở rộng zone zf sang tây, sửa biển zf_fields/nhiệm vụ bù nhìn.
- **Bước 3b (chờ test)** — `Catalog/v2/village_step3b.py`, md5 map1 d4263b5e… -> **4843958577d3a7b36cb230ed4d76335d**; sao lưu backups/…/step3b/ (cả tsx bánh xe).
  - Lỗi animation bánh xe: tấm watermill_with-fx_simpler.png chỉ có **7 khung** (2 ô cuối trống) -> `SR-anim-village-watermill.tsx` còn 7 khung.
  - Ruộng bắp cải lấn ra bờ sông -> làm lại x76–86 (cổng x77–78 cạnh lối vào), chừa dải bờ cỏ + bụi/cây ven sông.
  - Trồng 27 cây/bụi (+19 bóng, 23 collider gốc 29×16 như cây cũ): sau nhà nông + quanh ao, khe nhà nông/lò rèn, sau lò rèn, khe lò rèn/cối xay,
    sau cối xay, bờ tây dưới cầu, bụi thấp cuối ruộng, bờ đông cạnh quảng trường. Ảnh: Catalog/previews/village/s3b_preview.png.


## §105 Judgement Ray: tia phát ra từ bàn tay (01/11) — chờ test
- Trước: điểm phát = chân + (0, 0.2) + hướng x 0.12 -> tia ra từ giữa người (ngang chuôi kiếm).
- `Player/Skills/JudgementRaySkill.cs`: `Hand()` lấy vị trí bàn tay theo hình ray-hold đang hiện (8 hướng, chọn hướng gần nhất
  giống Animator SimpleDirectional2D). Bảng `handPixels` (pixel ảnh tính từ chân, thứ tự E, NE, N, NW, W, SW, S, SE) đo trên
  `Rogue Traveler/ray-hold/<hướng>/frame_005..008` (140x140, chân ở (69, 23) từ dưới), nhân lossyScale (0.7).
  Chỉnh được trong asset `Resources/Skills/EyeRay.asset`. Hitbox tia vẫn quét ở mặt đất (không đổi).
- **Bước 4 ĐÃ LÀM (chờ test)** — `Catalog/v2/village_step4.py` + 2 lần vá tay (IsTrigger cho lớp mechanics; phóng to lửa ×2.2 / khói ×2.25),
  md5 map1 4843958… -> **5fd3a06d806ef12e79a99e0fe5ed30b1**; sao lưu backups/…/step4/ (cả 2 file .cs + quest json).
  - Nhà nông bị đốt: 5 `raid_fire` (campfire1 fire, 352px) + 2 `raid_smoke` (khói đen) trong "props front"; `raid_smoulder` (khói trắng) ẩn.
    Lớp mới **"mechanics"** (id 25) có `raid_fire_out` (toggle, when=zf_cleared: ẩn lửa + khói đen, hiện khói trắng).
  - `MapEncounterDirector.EnsureMechanics()`: map có lớp "mechanics" mà scene chưa có MapMechanics -> tự AddComponent (Map 1 không cần sửa scene).
  - `MapTriggerHandler.RegisterSigns`: biển có property `flag` -> bật cờ khi đọc bằng T.
  - 2 Goblin_Torch (zf, đợt 1) tại (45.6,79) & (50.8,79.4) trước nhà cháy -> zf_cleared. (Máu 40, chưa có chỉnh máu riêng theo điểm sinh.)
  - Bảng tin: biển `notice_todd_contract` (107.6,73.4) flag `todd_contract`, nội dung hợp đồng (WANTED – GROK, 100 silver, nanh làm bằng chứng, hỏi Todd).
  - Khu zf = cả làng x36–128,y64–88, title "Millbrook", hint "Raiders in the village! {move} to move, {attack} to strike."
  - player_start (104,85.6) + zf_home (102.9,84.8) ở lối vào nam; `tobin_note` -> `todd_note` (thư Todd: điều khoản ở bảng tin, đang chữa cháy);
    zf_fields dời tới ruộng bắp cải (78.6,80.4) "cabbages wilted…"; zf_exit "North road: Brookside Trail, then Greystone land…".
  - Quest Map1: bước 1 "Drive the raiders out of Millbrook" (zf_cleared, mục tiêu nhà cháy), **chèn** bước 2 "Read Todd's contract on the notice board"
    (todd_contract), bước 3 đổi tên "Take the north road toward Greystone land" (z0_cleared).
  - Còn: bước 5 dân làng (NPC); phần còn lại của §95 (Brookside/Meadow/Caves/Camp/Ruins/Stone Ring).
- **Sửa thác (01/11, chờ test)** — ô cỏ vuông lộ ra hai bên thác: (1) mép nước hai bên thác lởm chởm (ô góc chéo) -> `Catalog/v2/village_fix_fall.py`
  ép bờ thẳng đứng: góc nước x83..90 cho hàng góc y38..45 (ô mép bờ x82 / x90 thẳng), +4 va chạm nước; (2) cột mép của ảnh thác
  ("waterfall - 3 wide": cột trái/phải có đá + góc cỏ) đặt lệch vào trong -> thác giờ **9 cột x82–90** (cột mép đè lên ô bờ x82/x90, 7 cột giữa x83–89),
  đáy hàng y=40..44. md5 map1 5fd3a06d… -> **6d91863328b18c7c6780e65c5721faed**; sao lưu backups/…/fix_fall/.
- **Bước 5 ĐÃ LÀM (chờ test)** — dân làng.
  - Ảnh: `Catalog/village_npcs.py` ghép lớp (thân + quần áo) từ gói Village -> `Assets/Resources/NPCs/<id>/idle_NN.png, walk_NN.png`
    (166 ảnh, meta Point, không nén, **PPU 44** = cao ~1 ô ngang tầm Ren, pivot ở chân). id: vf1 vf2 vf3 vm1 vm2 vm3 todd (idle 10 + walk 10, nhìn ngang),
    vendor (no_carpet idle 8), smith (idle 8), fisher (đang câu 10, khung 192).
  - Code mới `Scripts/Gameplay/World/Villagers/`: `Villager.cs` (đi lang thang trong vùng, tránh collider bằng CircleCast, nghỉ 1.5–4.5 s,
    lật ảnh theo hướng; T = nói lần lượt từng câu, quay mặt về người chơi; Interactable bám theo NPC), `VillagerDirector.cs` (đọc lớp "npcs":
    kind / walk / lines '|' / show_when / hide_when / face_left / speed; tắt collider vùng; ẩn hiện theo cờ). `MapEncounterDirector.EnsureMechanics`
    gắn thêm VillagerDirector khi map có lớp "npcs". Sắp lớp vẽ theo SpriteRenderer của Player (Sort Point = Pivot).
  - Map 1 (`Catalog/v2/village_step5.py`, md5 6d918633… -> **d5e1a91b6e793723857f9da60f398559**, sao lưu backups/…/step5/): lớp "npcs" (id 26), 12 người:
    luôn có: 2 người đi quanh quảng trường, khách trọ sân quán, thợ xay đứng ở cối xay, người bán ở sạp, ông câu cá bờ đông hồ;
    lúc bị cướp (hide_when zf_cleared): 2 người nấp đầu cầu phía đông; sau khi hết cướp (show_when zf_cleared): Todd đứng trước nhà cháy,
    thợ rèn trước lò rèn, 2 người đi lại phố tây / sân nhà nông.
  - 01/11 (theo yêu cầu): dân làng **chặn người chơi** — `Villager.Init` thêm Rigidbody2D Kinematic (interpolate) + CapsuleCollider2D ngang 0.42×0.22
    ở chân (offset y 0.1); đi lại chuyển sang FixedUpdate + `rb.MovePosition`; CircleCast bỏ qua thân mình (attachedRigidbody), coi người chơi /
    dân làng khác / quái là vật cản -> đứng chờ rồi chọn hướng khác. Bản cũ: backups/…/step5/Villager.v1.cs.
- **Bước 5b (01/11, xong)** — người dùng: dân làng quá to + kẹt nhau, sạp hàng sai, muốn nhân vật PixelLab cùng style Ren.
  - Nguyên nhân to: map PPU 100 (1 ô = 0.32 đv), Ren = ảnh 112 px PPU 100 × scale 0.7 (~0.66 đv ≈ 2 ô); ảnh dân làng gói Village (PPU 44) cao ~1.1 đv.
  - Sạp: `table--1` của gói Village chỉ là KHĂN TRẢI BÀN (không có bàn) -> bỏ; thay **xe hàng rong có mui** (SR-props-trimmed "carriage - complete",
    chân (101.1,86.7)) + thùng + bao cạnh xe, collider xe 3.8×1.2; dời người bán (99.7,84.1), 2 người nấp (100.1,79.7) / (101.8,80.6).
    md5 map1 d5e1a91b… -> **910117fa87c653e2ce706caf40bcbd09**; sao lưu backups/…/step5b/.
  - Code: `Villager` đọc ảnh theo hướng (idle_E/S/N, walk_E/S/N; west = lật east), collider/bán kính tính theo đơn vị thế giới (capsule 0.30×0.14,
    CircleCast r 0.11), tốc độ 0.55; `VillagerDirector` không còn làm con EncounterDirector, scale mỗi NPC = property `scale` (mặc định 0.7 như Ren),
    `face` E/W/S/N. Bản cũ: backups/…/step5/Villager.v2.cs, VillagerDirector.v1.cs.
  - CHỜ: tạo 7 nhân vật PixelLab (style Rogue Traveler, 112 px) + idle/walk S/E/N, rồi nhập Resources/NPCs/mb_* (PPU 100) và đổi `kind` trong lớp npcs.
    Ảnh gói Village ở Resources/NPCs/{vf1..todd,vendor,smith,fisher} sẽ thành thừa (cần người dùng cho phép xoá).
- **Bước 5c (01/11)** — 7 dân làng PixelLab (Pro, Low Top-Down, style ref "Main Ronin (Style Guide v2)" = Rogue Traveler 63×99):
  woman 440ae790, man e5882143, todd 8f2f8c21, farmhand f656a17d, smith 14eec055, merchant 1a5b3fb0, fisher c08f44c0.
  Hoạt ảnh template Skeleton v3: Breathing Idle (4 frame) + Walking (6 frames), hướng S/E/N (west = lật E). Nút "rocket" trên thẻ hoạt ảnh = tạo
  thêm 1 hướng; tối đa 10 job chạy cùng lúc. Export ZIP (khung 144×144, chân y≈123) -> `Catalog/village_npcs_pl.py <id> <zip>`
  -> Resources/NPCs/mb_<id>/{idle,walk}_{S,E,N}_NN.png, PPU 100, pivot chân chung (cùng tỉ lệ điểm ảnh với Ren khi scale 0.7).
  - farmhand THIẾU walk_S (PixelLab lỗi 500 ảnh rotations/south.png khi gửi job) -> `Villager.Set` lấy walk_E và lật theo hướng x khi thiếu ảnh dọc.
  - Map: `v2/village_step5c.py` đổi kind 12 NPC sang mb_* (vendor/inn_guest = merchant, miller/farm_hand = farmhand, huddle_b/square_man = man…),
    face_left -> face W. md5 910117fa… -> **3f195cdcc41173cdd174f08720f21679**; sao lưu backups/…/step5c/.
  - Đã chuyển (người dùng bảo xoá, 01/11) -> _to_delete/NPCs_village_old_2026-11-01/: Resources/NPCs/{vf1,vf2,vf3,vm1,vm2,vm3,todd,vendor,smith,fisher} (ảnh gói Village cũ).
  - Người dùng chê 4 người (farmhand/todd/man/woman) giống mặt nhau (style ref chép mặt Ren + prompt chỉ tả "grim") -> tạo lại với mô tả mặt riêng:
    farmhand 5ea1142a, todd f73210f1, man e66065ad, woman cfc665e2 (bản cũ còn trên PixelLab, người dùng tự xoá). `village_npcs_pl.py --replace` giữ GUID .meta.
    Đã làm idle+walk S/E/N cho 4 bản mới và thay vào mb_farmhand/todd/man/woman (--replace; farmhand giờ có đủ walk_S). Thêm despeckle (xoá cụm <8 điểm rời).
  - Lái buôn: bản có gậy bị thừa tay (S) + gậy lúc có lúc không / cầm nhầm tay (E) -> tạo lại KHÔNG gậy, hai tay nắm quai ba lô (b653347b),
    thay vào mb_merchant (--replace). Bài học: tránh vật cầm tay dài với PixelLab; West = lật East nên vật cầm tay luôn đổi tay khi đi sang trái.

## §106 Map 1 hoàn thiện (02/11) — chờ test
Người dùng chốt: dựng cửa hang thật; Camp còn ~7 quái; Grok lao vào cột thì choáng; nhặt nanh Grok -> nền sập -> rơi xuống Map 2.
Script `Catalog/v2/map1_lib.py` (Tmx: kiểm md5, giữ CRLF, sửa object/prop/CSV theo id) + map1_stepA/B/B2/B3/C.py; sao lưu `Catalog/backups/map1_complete_2026-11-02/<bước>/`.
md5 map1: 3f195cdc… -> A ac57a3fc… -> B 8ede8e0e… -> B2 8587c812… -> B3 9103a02b… -> C **4e0e46233512d36a460a1d825d59839e**.
- **A Brookside/Meadow:** bỏ cổng z0_gate (3039–3042, thú hoang không ép giết hết); gợi ý dùng token {roll} {heal} {skill1} (z1, z2, z3, z1_roll_hint,
  z2_heal_hint, z3_skill_hint); Ruins bỏ "strike from behind" -> "roll qua đòn nặng, đánh khi nó hồi"; dấu vết toán cướp trên đường bắc
  (bao thóc rách + thùng vỡ + biển z0_tracks, đọc bằng T). Code: lần đầu tới điểm nghỉ giải thích hồi sinh + nghỉ gắn mantra (cờ tut_shrine).
  Dải y64–66 đã kiểm: bị blocker y66 chặn, không đi tắt được -> giữ.
- **B Hollow Caves:** vách đá cao 6 ô (lớp cliffs x23–37, y35–40; blocker 3510 -> y35.6 cao 2.4); cửa hang sâu ảnh mới
  `Props/SR-trimmed/cave entrance - deep.png` (bỏ cỏ trên nắp, tối miệng hang) qua tileset mới `SR-cave.tsx` (firstgid 12067); 2 đuốc;
  biển z2_cave_mouth (T) + z2_skill_hint tự hiện "Press {skill1}..." trước miệng hang; Cave_Spawner (2993) dời ra miệng hang (30.6,41.75).
  Greystone Camp bỏ 3004 Torch, 3005 Hexer, 3002 Gunner -> còn 7.
- **C Stone Ring:** 6 cột tên ring_pillar_1..6, collider cột nới 1.4×0.8 ô + thuộc tính `pillar`/`pillar_visual`.
  Code: `Enemies/RingPillar.cs` (mới), `MapEncounterDirector.EnsureMechanics` gắn RingPillar; `EnemyAI.BossCombo` đang lao chạm cột -> cột vỡ (ẩn hình,
  tắt collider), boss choáng `pillarStun` 3 giây + gợi ý. Nanh Grok: prop grok_tusk_item hiện khi z5_cleared (toggle grok_tusk_show),
  pickup grok_tusk (item flag, requires z5_cleared, then_exit to_map2) -> 1.8 giây sau đi qua lối ra; to_map2 cần cờ grok_tusk (ExitOpen đọc `requires`,
  RevealExits bỏ qua lối ra chưa đủ cờ). Pickup có locked_text rỗng thì im lặng.
- **D:** `World/FirstTimeHints.cs` (mới, nhớ bằng cờ map): tut_death (sau hồi sinh), tut_glow (quái nhấp nháy gồng đòn ở gần), tut_circle
  (GroundStrike.Spawn), tut_poison (Health.ApplyPoison của người chơi). Thuốc xanh (green_tonic) trước đây nhặt không có tác dụng -> giải độc
  (`Health.CurePoison`). Nhiệm vụ: bước 3 xong khi vào Meadow (z1_started), "Clear the Hollow Caves" chỉ tới miệng hang, bước cuối
  "Take Grok's tusk as proof for Todd" (grok_tusk). Bản cũ các file .cs + quest json: backups/…/stepA, stepC, stepD.
- Chưa làm: lấp chỗ trống Ruins bằng dấu vết; NPC Todd nhận nanh (sang Map 2 luôn).
- **C2 (02/11, người dùng: cột đều + tỉ lệ vàng):** `v2/map1_stepC2.py` xếp 6 cột thành lục giác trên elip tâm (111.0,20.5),
  Rx 6 ô, Ry = Rx/φ ≈ 3.7 ô (góc 0/60/…/300°): cặp nhỏ ôm bậc đàn (108,17.3)/(114,17.3), cặp ngang (105,20.5)/(117,20.5), cặp dưới
  (108,23.7)/(114,23.7); collider đi theo. 2 goblin giáo dời ra (107,21.7)/(115,21.7). md5 4e0e4623… -> **297ea4bbfa80550aae0930532f212b3d**
  (lưu ý: backups/…/stepC2 giữ bản trung gian aeb01a7b; bản trước C2 = chạy lại stepC trên backups/…/stepC).

## §107 Map 1 sửa nhịp tutorial theo phân tích từng khu (02/11) — chờ test
Script `Catalog/v2/map1_tutA..D.py`, sao lưu backups/map1_complete_2026-11-02/tutA..tutD. md5 297ea4bb… -> A e5f4b781… -> B b67c9862… -> C 6099c181… -> D **285f04677785c357f698f2e5c1f95353**.
- **A Brookside:** thằn lằn gai 2986 ở đường ra làng (103,61) -> đứng một mình trên đường SAU điểm nghỉ (98.0,52.3); biển tự hiện mới
  z0_roll_hint (99.4,55.4) dạy {roll} trước khi gặp nó; biển cũ ở cầu (3022) thành lời nhắc đọc bằng T.
- **B Camp:** Goblin_Hexer 3000 + Camp_Gunner 3001 sang đợt 2 (đợt 1 = 2 giáo + 2 đuốc; đợt 2 = phù thuỷ + súng + cưỡi heo).
- **C Millbrook:** Goblin_Torch 4310 = đợt 2 (2 tên cướp ra lần lượt); biển Camp 3030 sửa khớp đợt mới.
- **D Hollow Caves:** bỏ Snake 2994 đứng lẻ (rắn vẫn chui ra từ hang); bình máu z2_potion_1 ra trước miệng hang (29.6,44.2).
  Code MapTriggerHandler: vật nhặt potion/flask_upgrade/green_tonic có đốm sáng ấm nhấp nháy (ExitBeacon), tắt khi nhặt (áp dụng mọi map);
  `flask_upgrade` trước đây nhặt không có tác dụng -> healPercent +0.15 (tối đa 0.8) + 1 bình.
- **Sửa vách Hollow Caves (02/11, người dùng: vách 6 ô quá cao, thiếu tự nhiên)** — `v2/map1_caveFix.py`, md5 285f0467… -> **10dfbd30f6883403f902600e0f2b51db**.
  Trả lớp cliffs x16–44, y32–42 về bản trước bước B (vách 3 ô như mọi vách khác; ảnh cửa hang cao đúng 3 ô nên chiếm gần hết mặt đá);
  blocker 3510 về (23,38,15×1). Cửa hang x27 (lỗ ~28–31), đá phải 2002/3115 lùi sang x33.2; bỏ 2 cột đuốc (hang thú hoang);
  Cave_Spawner x29.5, biển miệng hang x27.4, bình máu x29.0. Phá mép thẳng: bụi/đá/cây khô trên mép (cave_lip_*, không collider).
  Bài học: bộ tile vách ERW chỉ thiết kế cho vách 3 ô — không chồng tầng.
- **Cổng qua màn + rương Old Copper Mine (02/11)** — `v2/map1_gatesChest.py`, md5 10dfbd30… -> **900fdb225bb821be4053466c068dfc37**.
  Cột cọc dọc (ảnh 64×94) trước đây chồng từ đáy rect lên nên trồi 1.7 ô lên cổng trại / cắm xuống rừng: xếp lại cho đỉnh/đáy khớp khe đường
  (z1 49.4–54.2, z3 15.4–20.2, z4/z5 14.6–17.95). z2 (khe vách x38–41): 2 dàm cọc ngang căn theo phần hình (x22..81 của ảnh 96) phủ đúng 38–41,
  đứng ở chân vách y41.1; collider cổng 3052 lên y40.2. `compose_preview.py` giờ vẽ cả lớp gates.
  Rương: lời nhắn của Hob (thợ mỏ Millbrook) trên cọc gỗ ở lối vào bãi mỏ (z2_hob_note) -> động cơ đánh gấu; z2b_flask dời ra trước rương,
  requires z2b_cleared (locked_text gấu canh), flag z2b_flask_taken -> toggle z2b_chest_toggle đổi rương đóng (2205 z2b_chest_closed) sang mở
  (ảnh mới `Props/SR-trimmed/chest - wooden open.png` = khung cuối chests-opening-no horns, gid 12068 trong SR-cave.tsx). Code: flask_upgrade bật `flag`.
- **Cổng Meadow -> Caves thêm hàng cọc tĩnh (02/11)** — `v2/map1_z1fence.py`, md5 900fdb22… -> **f8bf23869335602535d14bab283f025f**.
  Dải cỏ trên đường (y46.5–50) nhìn như đi vòng được (thật ra blocker 3517 chặn) -> 5 cọc dọc z1_palisade_1..5 (lớp props, x48, đỉnh 46.6 đáy 50.5)
  nối từ bìa rừng xuống đỉnh cột cổng; không thuộc nhóm cổng nên vẫn đứng sau khi cổng mở (khớp blocker).
- **Làng sau chơi thử 02/11 03:40** — `v2/map1_villageFix2.py`, md5 e0e6397c… (bản người dùng lưu lại trong Tiled: chỉ đổi định dạng số,
  đã so object/layer = f8bf…) -> **3080ff4f3dd079d3a3ae0776d30b0127**; sao lưu backups/…/villageFix2.
  - Cầu đá gãy: thân cầu 5 ô có sẵn 2 đầu riêng + 2 mẩu "entrance" thấp hơn -> 3 khúc rời. Ảnh mới `Props/SR-trimmed/stone bridge - long 7.png`
    (đầu trái + 2 đoạn giữa + đầu phải, 224px = 7 ô, gid 12069) đặt x92–99; bỏ 3946/3948; collider lan can x92.3 rộng 6.4 ô.
  - Dân làng: mỗi thiết kế chỉ xuất hiện 1 lần cùng lúc. Bỏ inn_guest / miller / west_woman. Trước khi đuổi cướp: 2 người nấp ở hiên nhà Todd
    (116,78)/(118.4,78.2), lái buôn đứng TRƯỚC xe hàng (101.4,87.2 hướng S), ông chài trên bờ nhìn ra nước (102.5,68.4 hướng W).
    Sau khi đuổi cướp: phụ nữ quanh giếng (vùng 98.8–102.8, 74.8–77.2), đàn ông cạnh bảng tin (106.2–110.2, 75–77.6) — vùng riêng, không đẻ chồng;
    cậu làm thuê trong ruộng lúa mì (42.2–54.2, 82.4–84.6); Todd, thợ rèn giữ chỗ.
  - Dấu vết trận cướp: ảnh nhà cháy `Village V2.2/SR-buildings/vh_farmhouse_barn_burnt.png` (gid 12070; mái + chái sạm vệt khói dọc, lỗ thủng,
    kính vỡ, hoa cháy). raid_fire_out: ẩn raid_fire, raid_smoke, farmhouse_intact (3779) -> hiện farmhouse_burnt, 2 raid_smoulder (khói trắng), raid_debris (4 khúc gỗ cháy ngoài phố).
  - Lưu ý: SR-cave.tsx giờ 4 tile (cửa hang, rương mở, cầu dài, nhà cháy). Tuyệt đối không ghi tmx tạm vào Assets (lỡ tạo _preview_after_raid.tmx -> đã chuyển ra Soul Requiem/_to_delete/).

## §108 Cảnh mở đầu Map 1 + dạy dần (02/11) — chờ test
- Code mới: `World/Cutscene/OpeningCutscene.cs` (tự gắn vào MapEncounterDirector khi map có điểm `intro_cam_1`), `World/Cutscene/SpeechBubble.cs`
  (bóng thoại thế giới: TextMesh phông RequiemBody + nền tối, sortingOrder 32000+, nảy khi hiện, mờ dần). `CameraFollow.focusSharpness` (mặc định 6;
  cảnh lia 1.4, trả về 2.2). Sao lưu CameraFollow/MapEncounterDirector cũ: backups/map1_complete_2026-11-02/intro/.
- Trình tự: khoá điều khiển (giữ GameInput.Blocked mỗi khung, Space/Enter bỏ qua) -> chú thích "Millbrook, the morning the raiders came."
  -> lia tới nhà nông đang cháy: goblin "Burn it! Burn it all!", tiếng kêu "Help! The farm is burning!", goblin "Tell your headman - Grok sends his regards!"
  -> lia tới hiên nhà Todd: 2 dân nấp "Raiders! They came down the north road!" / "You there, with the sword - please, help us!" -> trả camera về Ren
  -> cờ intro_done, nhả điều khiển -> dạy dần: {move} + theo dấu nhiệm vụ; tới gần tên cướp đầu (≤2.2 đv) -> {attack}; tên đầu gục -> nhắc tên thứ hai (đợt 2).
- Map: `v2/map1_intro.py`, md5 3080ff4f… -> **81b80eef4391924217301f20539f45f6**: 7 điểm type `cutscene_point` (intro_cam_1/2, intro_say_1..5:
  shot, delay, color goblin|villager, text) trong lớp triggers; gợi ý vào khu zf để trống (tránh chữ chồng lúc mở màn).
- WorldFlags không lưu giữa các lần chạy -> cảnh chạy mỗi lần nạp scene Map 1 (không chạy lại khi chết/hồi sinh).
- **Sau trận cướp: Todd mở cửa bước ra (02/11 04:12)** — code mới `World/Cutscene/RaidAftermath.cs` (tự gắn khi map có điểm `aftermath_door`);
  `Villager` thêm đi theo kịch bản (WalkTo / FaceTo / EndScript / Arrived; bị chặn >2 giây coi như tới). Khi zf_cleared: chờ 1.6 giây ->
  hiện prop `farm_door_open` (ảnh mới Village V2.2/SR-buildings/vh_farmhouse_door_open.png, gid 12071) -> đặt Todd ở bậc cửa rồi bật cờ todd_door
  (Todd show_when = todd_door nên không hiện đột ngột) -> khoá điều khiển, Todd đi tới cạnh Ren, đóng cửa, nói line1..line5 bằng bóng thoại
  (Space tua) -> cờ todd_greeted, gợi ý after_hint (bảng tin) -> Todd về chỗ cũ. Thợ rèn + cậu làm thuê bỏ show_when (luôn có mặt).
  Ảnh nhà cháy làm lại (`v2/make_burnt_house.py`): bỏ vệt sọc khắp mặt; 2 lỗ thủng mái + 1 lỗ mái chái (lộ kèo, viền than, than hồng),
  muội bốc lên từ cửa sổ/cửa, kính vỡ, hoa cháy; 2 raid_smoulder dời lên đúng 2 lỗ mái. Bản v1: backups/…/villageFix2/vh_farmhouse_barn_burnt_v1.png.
  Map: `v2/map1_aftermath.py` -> md5 **a033562eb6c0edeabd735e9199d53448**.
- **Dân làng nam/nữ làm lại lần 3 (02/11 04:10)** — nam giống Ren (tóc đen, râu, cau mày) -> man 015a5089: người đẫy, mặt tròn đỏ, cạo râu,
  mũ len xanh, áo vàng mù tạt + tạp dề da, mặt lo lắng; nữ nhắm mắt khi thở + mặt tươi lúc kêu cứu -> woman ad260437: mặt sợ hãi, mắt mở to, không cười.
  PixelLab "Breathing Idle" hay vẽ NHẮM MẮT / thêm "hơi thở" rời: `village_npcs_pl.py` thêm `--fix-eyes=S` (dán đầu từ ảnh xoay vào khung idle,
  căn ±3 px; `E:1` = lấy đầu từ khung idle 1), `--keep-main=idle_E` (chỉ giữ mảng liền lớn nhất), `--despeckle=N`, `--sheet=ảnh soát`.
  Đã nhập: man `--fix-eyes=S`, woman `--keep-main=idle_E` (--replace, giữ GUID). Bản cũ trên PixelLab: e66065ad (man), cfc665e2 (woman) — người dùng tự xoá.

## §109 Bảng tên boss khi boss xuất hiện (02/11) — chờ test
- Người dùng chọn kiểu 1 (bảng tên kiểu Enter the Gungeon / Hollow Knight). Dùng chung cho MỌI boss (spawn có `boss = true`), không cần sửa map.
- Code mới `World/Cutscene/BossIntro.cs` (director tự gắn trong EnsureMechanics). Director thêm event `BossSpawned(EnemyAI, gợi ý khu)`;
  khu có boss ở đợt đầu thì gợi ý của khu (vd z5 "Grok charges blindly...") để BossIntro hiện SAU bảng tên.
- Lần đầu gặp (cờ `bossintro_<tên>`, giữ trong lượt chơi): khoá điều khiển + người chơi bất tử tạm, boss `Held` (EnemyAI mới: bộ não đứng yên),
  camera lia 75% về phía boss (thu khung 0.9) -> 0.9 s boss gồng (Windup) + rung màn hình 0.55 s -> bảng tên 3 s -> trả camera, điều khiển,
  hiện gợi ý khu, 0.4 s sau boss mới được đánh. Space/Enter: bỏ qua. Gặp lại sau khi gục: chỉ bảng tên 2.2 s, không khoá.
- Bảng tên do RequiemHUD vẽ (BuildBossCard/UpdateBossCard): dải tối mờ hai mép ở 20% chiều cao màn hình, 2 vạch vàng chạy từ giữa ra,
  TÊN 112px (Bone, bóng đỏ thẫm) phóng to + giãn chữ rồi dồn lại, loé trắng; biệt danh vàng hiện sau 0.45 s. Thanh máu boss ẩn khi bảng tên đang hiện.
  Tên tách từ displayName ở "—" hoặc ", ": "Grok, the Greystone Chieftain" -> GROK / THE GREYSTONE CHIEFTAIN.
- Chưa có âm thanh (dự án chưa có hệ thống âm thanh). Backup: Catalog/backups/boss_intro_2026-11-02/ (EnemyAI, MapEncounterDirector, RequiemHUD).
- **Sửa 02/11 07:50 (người dùng báo 2 lỗi):**
  1. Không qua được cầu z4 -> z5 dù cổng đã mở: 2 ô va chạm nước `3682` (y512-544) và `3683` (y544-576) trong lớp water colliders
     phủ đúng làn qua cầu (giữa z4_gate x3104 và z5_arena_lock x3296) — có từ trước đợt 02/11. Đã bỏ (`v2/map1_bridgeFix.py`),
     md5 **520b4740e144594201e495e824319d8b**. Quét mọi cổng của Map 1: chỉ cầu này bị chặn.
  2. Cảnh boss giật: (a) Grok_Troll_Windup là clip LẶP 5 khung 0.625 s, để chạy suốt ~3 s nên boss giật lại từ đầu mỗi 0.6 s;
     (b) CameraFollow lúc lia (Focus) không làm tròn điểm ảnh, tắt PixelPerfect và thu khung 0.9 -> ảnh pixel rung rinh;
     (c) rung màn hình không chạy lúc đang lia mà dồn lại, giật một cái khi camera vừa về tới Ren; SmoothDamp còn vận tốc cũ.
     Sửa: gồng 1 lần (độ dài clip) -> Attack (nện) + rung + bảng tên -> Idle; giữ nguyên cỡ khung; CameraFollow lia trên vị trí thô
     `focusRaw` rồi Snap + cộng rung, xoá vận tốc khi về xong (cảnh mở đầu cũng được hưởng). Backup: boss_intro_2026-11-02/ (CameraFollow.cs, BossIntro_v1.cs.txt).

## §110 Sửa 4 lỗi Map 1 (02/11 08:xx) — chờ test
1. **Vật trang trí vẽ đè lên nhân vật** (đống gỗ trước nhà Todd): sắp xếp theo trục Y dùng pivot = ĐÁY ảnh (SuperTiled2Unity),
   nhưng vài ảnh có lề trong suốt dưới đáy (logs_5 21px, lamp-post1 17px...) -> nhân vật đứng trên lề đó bị coi là "phía sau".
   Quét lớp props (`v2/props_padding_scan.py`): 9 vật lề >= 8px. Cắt lề đáy thành bản sao `ERW/Props/SR-trimmed/village - <tên> - trim.png`
   (.meta chép từ ảnh gốc, GUID mới), tileset mới `Tilesets/SR-props-trim.tsx` (firstgid 12072), đổi gid + nâng đáy object
   (hình nằm nguyên chỗ). Chừa giấy dán bảng tin (questboard_papers_1: phải vẽ đè lên bảng).
2. **Tua hội thoại bị chồng chữ**: SpeechBubble.Show xoá bóng cũ ở gần (cùng người nói, |dx|<1.2, |dy|<0.6); bóng ở xa vẫn giữ.
3. **Minimap cũ** (bake 26/10, trước khi làm làng): chạy lại `v2/tools/minimap_bake.py Map1_GreystonePlains`. Ảnh cũ: backups/fixes4_2026-11-02/.
   Minimap vẽ mọi vật (kể cả lửa đốt nhà) -> làng hiện lúc đang cháy. Mỗi lần sửa map nhớ bake lại.
4. **Chưa đọc hợp đồng vẫn ra khỏi làng**: loại trigger mới `turn_back` (MapSchema + MapTriggerHandler.OnTurnBack): chưa đủ cờ `requires`
   thì đẩy người chơi ra khỏi dải về phía vừa đi tới + nhắc `locked_text` (tối đa 1 lần/3 giây). Map: `village_north_lock`
   (x100-119, y64.3-65.3, ngang cổng vòm gỗ, từ bờ sông tới hàng cây), requires todd_contract. Lưu ý: F2/F3 (DevCheats) mở cổng các khu.
   Map: `v2/map1_fixes4.py` -> md5 **209a443679f60b1bb8ffa06095a84010**.
- **Sửa ngay sau (08:20): sinh sai chỗ** — `map1_fixes4.py` truyền kích thước theo px vào `rect_obj` (hàm này nhân 32) -> dải
  village_north_lock thành 19456x1024 px, phủ cả điểm xuất phát; vừa vào game OnTurnBack đẩy Ren ra mép tây bản đồ (x = 0, rừng
  dưới rương z2b). Sửa map `v2/map1_fixes4b.py` (608x32 px) -> md5 **590679b3fe80e6ef91fe915cf30aa3dd**. Code chặn thêm: chỉ dời
  theo cạnh mỏng, giữ toạ độ kia; bỏ qua khi chưa có vị trí khung trước hoặc dải dày > 0.7 đơn vị.

## §111 Hướng dẫn "dừng chờ phím", goblin quát doạ, Todd lia camera, dân làng đi về (02/11 09:xx) — chờ test
- **Màn dừng hướng dẫn** (`TutorialPrompt` + `CutsceneState` trong `Cutscene/CutsceneState.cs`; HUD vẽ ở RequiemHUD.BuildTutorialPrompt):
  timeScale = 0, phủ tối, chữ ATTACK / DODGE, ô phím viền vàng nhấp nháy (thời gian thật), chờ `GameInput.PressedRaw` (bỏ qua Blocked,
  Attack nhận cả chuột trái) rồi `PlayerCombat.QueueAction(a, hướng)` làm luôn đòn/lăn. Menu Pause mở giữa chừng: không nhận phím
  (`GameInput.MenuOpen`, RequiemMenus đặt); đóng menu thì dừng lại. Dải gợi ý ẩn khi màn hướng dẫn đang hiện.
- **OpeningCutscene.Tutorial viết lại**: cả 2 Goblin_Torch cùng đợt 1 (4310 wave 2 -> 1; cảnh mở đầu thấy 2 tên thì vào trận cũng 2 tên).
  Tới gần (3.1 đv) -> giữ cả hai (`Held`), tên gần nhất quát "Heh! Fresh meat for the fire!" (khoá 2 giây) rồi lao vào ->
  cách 0.62 đv: DỪNG [J / LEFT CLICK] ATTACK -> lần gồng đòn kế tiếp (`EnemyAI.MeleeWindup`, event mới): DỪNG [Q] DODGE (lăn ra xa) ->
  tên đầu gục (hoặc 8 giây) -> tên thứ hai quát "You'll burn with the rest of 'em!" rồi lao vào.
- **HUD mờ khi diễn cảnh** (CutsceneState.Begin/End): cảnh mở đầu, Todd, giới thiệu boss. Giữ dải gợi ý, bảng tên boss, màn hướng dẫn.
- **Todd**: camera lia vào giữa Todd và Ren (+0.35 đv lên trên) lúc nói; lời rút còn 3 câu ngắn (line4/line5 để trống).
- **Thợ rèn + cậu làm thuê**: show_when zf_cleared + `enter_from` (thuộc tính NPC mới, "x,y;x,y" ô Tiled: điểm xuất hiện rồi các điểm đi qua,
  cuối cùng về chỗ cũ; chỉ khi hiện ra GIỮA lúc chơi). Thợ rèn đi từ mép đông (67.5,78.8), cậu làm thuê từ mép tây (36.2,79) vào ruộng qua khe rào (48.9).
- **Gỗ đổ có va chạm**: 4 ô `raid_debris` trong obstacle colliders (cùng tên -> ẩn/hiện cùng hình qua toggle raid_fire_out), tính từ phần ảnh có màu.
- **Lối ra làng**: turn_back nay cho Ren tự nhủ bằng bóng thoại (thuộc tính `say`): "Not yet... I should check that wanted notice in the square first."
  Ghi log `[TurnBack] ... Cờ đang có: ...` mỗi lần chặn — dùng để tra nếu đọc bảng rồi vẫn bị chặn (chưa tái hiện được: đọc bảng bật cờ
  todd_contract, dải chỉ chặn khi thiếu cờ đó; không có va chạm nào khác ở cổng vòm).
- Map: `v2/map1_tutorial.py` -> md5 **da3c0f9bb9bbdf7e81f96c8b60389e66**. Code backup: Catalog/backups/tutorial_2026-11-02/.
- **09:05**: người dùng tự sửa map trong Tiled (bỏ cây 4213 + bóng 4300 cạnh sông, biển 2279 + va chạm 3082 cạnh cổng vòm, dời 2943)
  -> md5 mới **b7f356a3d9816bc3395552d3f7a4bb66** (đã giữ nguyên). Todd/Ren nhìn nhau: Todd đứng NGANG hàng Ren (phía cửa, cách 0.46 đv),
  Ren quay về phía Todd qua `PlayerCombat.FacingOverride` suốt lúc nói, trả lại khi xong.
- Cổng vòm: Editor.log lượt chơi gần nhất có todd_contract rồi z0_started, không có dòng [TurnBack]; dải turn_back là trigger
  (lớp triggers có unity:IsTrigger), không có va chạm/tile va chạm nào khác trên đường qua cổng (đã quét). Chờ người dùng thử lại.
- **10:05 — ĐÃ TÌM RA lỗi cổng vòm** (xem trực tiếp trong Unity): BoxCollider2D của village_north_lock có Is Trigger TẮT ->
  tường vô hình; Ren đứng kẹt ở y -21.013 (đúng mép dưới dải). Thuộc tính unity:IsTrigger đặt ở LỚP triggers KHÔNG được
  SuperTiled2Unity áp cho object — mỗi object phải tự có. Sửa: `v2/map1_gateTrigger.py` thêm unity:IsTrigger = true cho 4419
  -> md5 **8719085bfac59e3c43a94348e0321867**; MapLayout ép mọi Collider2D của lớp triggers thành trigger (phòng lần sau quên).
  Lưu ý cho script: rect_obj trong lớp triggers/zones/npcs/mechanics phải kèm "unity:IsTrigger".

## §112 Quay mặt khi nói, goblin bước lại gần, HEAL/MANTRA, cuộn chiêu F, Grok đánh bay đệ (02/11 10:xx) — chờ test
- **Quay mặt khi nói**: `PlayerCombat.LookAt(dir, giây)` / `StopLook()` / `LookDir` (PlayerController dùng khi không có FacingOverride;
  người chơi tự đi thì thôi). Villager.Say: NPC quay về Ren, Ren quay về NPC, NPC đứng yên hết thời gian câu nói. Cảnh goblin, Grok cũng dùng.
- **Goblin đầu tiên**: tới 3.1 đv -> giữ cả hai, khoá điều khiển, HUD mờ; tên gần nhất bước lại tới 1.35 đv (tối đa 2.5 giây) rồi mới quát.
- **Màn dừng dùng chung**: `TutorialPrompt.Run(combat, action, title, keys, sub, dir)` (CutsceneState.cs). QueueAction thêm Heal (uống)
  và Skill1 (thi triển ô 1). **TutorialDirector** (tự gắn cùng OpeningCutscene): HEAL lần đầu máu ≤ 55% còn bình; MANTRA lần đầu có quái
  trong 1.6 đv sau khi có cờ learned_ground_slam. DODGE [Q] vẫn ở trận cướp (lần gồng đòn đầu tiên).
- **Chiêu F**: item mới `skill_scroll` (skill_id, potions, flag, learn_flag): học chiêu (lắp ô trống đầu = F) + thêm bình.
  `MapTriggerHandler.ApplyScrollLocks()`: lượt mới (không mang loadout từ map trước) mà chưa nhặt cuộn -> bỏ chiêu đó khỏi owned/slots.
  Rương của Hob (z2b_flask): skill_scroll ground_slam (Earthshatter) + 1 bình, cờ learned_ground_slam (bỏ nâng cấp bình đồng cũ);
  sửa thư của Hob; gợi ý z2_skill_hint, khu z3, z3_skill_hint không còn bảo bấm {skill1} trước khi có chiêu.
- **Grok**: 2 Goblin_Spearman (3012/3013) cùng đợt 1 với Grok (bỏ spawn_when). Object boss `grok_fight` có thuộc tính mới
  toss_minions, intro_line1..N, wake_line. BossIntro: giữ boss + đệ (trong 4 đv) -> Grok chửi 2 câu -> gồng, nện: đệ bay (cung + xoay 2 vòng,
  0.75 đv ngang + 0.45 đv xuống), nằm nghiêng 90°, sao choáng `stun_stars` -> bảng tên. Boss xuống nửa máu (Phase2Reached) hoặc chết:
  câu wake_line, sao tắt, đệ bật dậy và đánh. Gặp lại sau khi gục: đệ nằm choáng sẵn, không diễn.
- Map: `v2/map1_bossMinions.py` -> md5 **6a30267dded87e280071c1deffa3a8b7**. Backup code: Catalog/backups/tutorial2_2026-11-02/.

## §113 Khung hội thoại, DODGE ở khu thằn lằn, HEAL 40%, nhiệm vụ rương Hob (02/11 11:xx) — chờ test
- **Khung hội thoại** (`Cutscene/DialogueBox.cs` tĩnh + `DialogueRunner.cs`; HUD BuildDialogue/UpdateDialogue): giữa đáy màn hình, nền tối viền vàng,
  tên người nói (vàng), chữ chạy 55 ký tự/giây, "[T] next / [T] close" nhấp nháy. T/Space/Enter/chuột trái: hiện hết câu -> câu sau -> đóng.
  Khoá di chuyển khi mở; phím T vừa mở khung không tính. Dải gợi ý + nhắc tương tác ẩn khi khung mở.
  Dùng cho: NPC (Villager.Say mở TẤT CẢ lines một lượt, hai bên quay mặt nhau tới khi đóng; tên = thuộc tính `speaker`, mặc định tên Tiled
  viết hoa) và bảng/thư đọc bằng T (RegisterSigns: trang ngăn bằng '|', thuộc tính `speaker` tuỳ chọn). Cảnh diễn vẫn dùng bóng thoại.
- **DODGE [Q]** chuyển khỏi trận cướp sang khu thằn lằn (TutorialDirector.dodgeZone = z0): event mới `EnemyAI.AttackStarted`
  (cận chiến, nổ gai, bắn); lần đầu một con trong 2.6 đv bắt đầu gồng -> dừng khung sau, lăn ra xa. **HEAL [E]** khi máu ≤ 40% (healAt).
- **Bỏ gợi ý tutorial cũ ở dải đáy**: tut_glow (EnemyAI), "Press {move}…", "Good! Roll…", "second raider… roll…", "Picked up a potion (press…)";
  map: gợi ý khu z1 ({roll}), z2 ({heal}), z4 (glow/roll) để trống; xoá biển tự hiện z2_heal_hint (3024), z0_roll_hint (4389);
  z3_skill_hint thành chữ khắc trên khiên; chữ rương Hob bỏ "press {skill1}". Còn giữ: tut_shrine, tut_death, tut_circle, tut_poison.
- **Nhiệm vụ rương Hob**: bước mới sau "Clear the Hollow Caves": "Win back Hob's chest from the bear in the old copper mine"
  (done z2b_flask_taken, mũi tên tới (13.2, 58.8)). Lý do: thư của Hob (cuộn chiêu + bình) + dải turn_back `z2_north_lock` trước cổng bắc
  Hollow Caves (requires z2b_flask_taken): Ren tự nhủ "That camp's crawling with hexers. Hob's mantra scroll first - the old mine is west."
- NPC speaker: Villager ×2, Peddler, Old Fisherman, Frightened Woman/Man, Todd, Smith, Farmhand.
- Map: `v2/map1_dialogue.py` -> md5 **f2f9f951b04cc89d6fda7097aa111f8d**. Code backup: Catalog/backups/dialogue_2026-11-02/.

## §114 Sửa 5 lỗi (02/11 16:xx) — chờ test
1. **Đỏ mãi sau khi lăn**: PlayerDodge chụp màu lúc bắt đầu lăn (đang nháy đỏ vì trúng đòn) rồi trả lại màu đó -> nay chỉ trả độ trong suốt.
2. **DODGE hiện khi gai chưa bay ra**: event mới `EnemyAI.ProjectilesFired` (sau khi nổ gai / bắn); TutorialDirector dừng lúc đó,
   lăn XUYÊN qua vòng gai (chéo 40° về phía con thằn lằn) — lăn ra xa thì gai bay cùng chiều, hết bất tử vẫn trúng.
3. **MANTRA chỉ khi bị bao vây** (≥ 3 quái trong 2 đv, hoặc 2 quái hai phía lệch > 100°): "You're surrounded! Earthshatter strikes…".
   **Bảng nhiệm vụ đếm quái**: bước có cờ `<khu>_cleared` -> dòng vàng "Foes slain: x / y" khi khu đang đánh
   (`MapEncounterDirector.ZoneTally`: đã hạ + còn sống + đợt chưa ra + quái hang (CaveSpawner.Spawned/Total); `QuestTracker.StepDone`).
4. **Đệ của Grok nằm choáng vẫn bị đánh / vẫn thở**: lúc bị hất: Health.Invulnerable = true, Animator.speed = 0 (đứng hình); dậy thì trả lại.
5. **Rương của Hob** dời lên 4 ô ra bãi đất trước miệng mỏ (hình đóng/mở, va chạm, vùng nhặt, toggle): `v2/map1_chestMove.py`
   -> md5 **abee0d8e8ccb0c2c2cb79a17f3e1ba34**. Bake lại minimap. Code backup: Catalog/backups/fix5_2026-11-02/.
- **16:35** rương Hob dời lần 2 (đang nằm giữa lối đi): vào góc thợ mỏ, ngay dưới bàn cuốc/xẻng cạnh miệng mỏ (dịch -10.8, -4.44 ô)
  `v2/map1_chestMove2.py` -> md5 **ea3fb3fb056b41671beacdfb4006cc83**; bake lại minimap. Ảnh phân tích tuyến đường: Catalog/previews/map1_route_analysis.png
  (`v2/route_overlay.py`: khu, cổng, điểm nghỉ, quái, đích nhiệm vụ).

## §115 Cân bằng Map 1 cho người mới (02/11 16:50) — chờ test
1. **Điểm nghỉ doanh trại** `z3_fire` dời khỏi giữa trại ra lối đi giữa cổng bắc Hollow Caves và cổng trại (trigger x34.5–41, y36–37.6,
   phủ cả lối đi, zone_id rỗng) + lửa trại mới (vòng đá, lửa, khói, va chạm `camp_approach_*`). Lửa trong trại giữ làm cảnh.
2. **Bình máu tối đa**: `PlayerCombat.maxPotions = 3` (vào map đủ 3), `RefillPotions()`; hồi sinh và nghỉ bên lửa (RequiemMenus, nhấn T)
   = đầy máu + hết độc + nạp bình tới tối đa. skill_scroll (rương Hob) +1 bình tối đa. HUD bình tính theo maxPotions.
3. **Meadow** đợt 2: 4 -> 3 thằn lằn (xoá 2992).
4. **Doanh trại** 3 đợt: (Torch×2 + Spearman 2996) / (Spearman 2997 + Hexer) / (PigRider + Gunner).
5. **Ruins Path**: 2 Spearman đợt 1 có `delay = 4` giây -> Blightdrinker ra một mình trước.
6. **Sinh quái có báo hiệu**: NextWave (trừ khu có boss — BossIntro cần đủ boss + đệ ngay) -> `SpawnTelegraphed`: lần lượt cách 0.5 giây
   (+ thuộc tính spawn `delay`), bụi `m3_dash_dust` 0.6 giây rồi mới ra; cách người chơi < 1.5 đv thì đẩy ra 1.5 đv (nếu chỗ mới không vướng).
   `EncounterZone.pending` (chưa tính dọn xong khi còn quái chờ), `generation` (khu làm lại huỷ lượt chờ); ZoneTally cộng pending.
- Map: `v2/map1_balance.py` -> md5 **52fc8afa2bba6298aacf87867b7400e1**; bake lại minimap + ảnh phân tích. Code backup: Catalog/backups/balance_2026-11-02/.

## §116 Cốt truyện Map 1 sửa lại: thuế thóc của Grok (02/11 17:00) — chờ test
- Lỗi: đọc bảng tin (cờ todd_contract) TRƯỚC khi cứu làng là mở được lối ra bắc -> người chơi đi Meadow khi nhiệm vụ còn "Drive the raiders out".
- Cốt truyện mới: Ren là kiếm khách đi ngang qua (không còn "hunter"/tiền thưởng). Mỗi kỳ trăng mới, lính thu thuế của Grok xuống
  lấy thóc của Millbrook; thiếu bao nào là đốt kho. Sáng nay thiếu 3 bao -> đốt trại nhà Harlan. Ren đuổi chúng đi; Todd (trưởng làng)
  ra cảm ơn, kể chuyện bị bóc lột và nhờ Ren diệt Grok, mang nanh về làm bằng chứng để làng được tự do.
- Nhiệm vụ: bỏ "Read Todd's contract…", thay bằng "Hear out Todd, the village headman" (done todd_greeted, đích cửa nhà Todd);
  bước cuối "Take Grok's tusk - proof for Todd that Millbrook is free". Lối ra bắc `village_north_lock` requires **todd_greeted**,
  Ren tự nhủ "Not now... these people need help first."
- Viết lại chữ: intro_say_1/3, 3 câu Todd sau trận + after_hint, bảng tin (lệnh thu thuế bằng sơn đỏ + sổ thóc của Todd, speaker
  "Notice board", chỉ để đọc), giấy trên cửa nhà Todd, biển lối bắc, dấu vết z0, lời square_woman / vendor / huddle_b / todd / smith /
  farm_hand, câu đầu của Grok, chữ nhặt nanh; chú thích mở đầu (OpeningCutscene) "Millbrook. New moon - and Grok's tithe-collectors have come."
- Map: `v2/map1_story.py` -> md5 **2ac64542566d2a90327cf05c25c9b420**.

## §117 Bớt ghi chú/biển đọc thừa ở Map 1 (02/11 17:05)
- Xoá 8 biển: todd_note, zf_well, zf_fields, zf_exit (biển ngay cổng vòm, nhắc [T] Read gây rối chỗ chặn), z1_roll_hint (đã có DODGE),
  z2_skill_hint, z3_skill_hint (trùng gợi ý khu z3), z4_ledger (trùng z4_dig + bà thầy bói). Bỏ gợi ý khu z0 "Follow the stream to its source".
- Còn 8 biển, mỗi cái một ý: notice_todd_contract (lệnh thu thuế), z0_tracks (dấu vết lũ cướp), z0_falls (nước xanh), z1_border (totem
  biên giới), z2_cave_mouth (hang ổ quái), z2_hob_note (rương Hob), z3b_seer (lời bà thầy bói - gốc truyện), z4_dig (rãnh đào tới vòng đá).
- Map: `v2/map1_notesTrim.py` -> md5 **568803275e3c9a2902e8654f95648285**.

## §118 Mũi tên rương, rào cổng bắc Hollow Caves, hội thoại dài vào khung, bảng tên boss (02/11 17:35) — chờ test
- Nhiệm vụ rương Hob: đích (4.4, 50.4), nhãn "Hob's chest" (trước trỏ chỗ rương cũ).
- Cổng z2_gate (rào cọc bắc Hollow Caves): unlock_when **z2_cleared,z2b_flask_taken** — rào còn đứng tới khi lấy rương
  (trước mở ngay khi dọn hang, chỉ còn dải chặn vô hình). `v2/map1_gateChest.py` -> md5 **f5fcf117d834a7b4091dafd6370d51ef**.
- Lời Todd sau trận: khung hội thoại (tên Todd, [T] câu tiếp) thay bóng thoại. Lời Grok mở màn: khung hội thoại tự sang câu
  (`DialogueBox.Open(..., autoSeconds)` mới; T/chuột = câu tiếp, Space = bỏ cảnh). wake_line giữa trận vẫn là bóng thoại.
- SpeechBubble tự xuống dòng ~30 ký tự/dòng.
- Bảng tên boss: xuống 1/3 dưới màn hình (top 64%), dải nền nhạt hơn (0.7); camera lúc giới thiệu lệch xuống 0.55 đv -> boss ở nửa trên.
- Code backup: Catalog/backups/ui_2026-11-02/.

## §119 Goblin bỏ chạy, dấu hiệu điểm nghỉ, bóng thoại không che đầu, bảng boss kiểu Soul Knight (02/11 18:xx) — chờ test
- **Trận cướp = đuổi đi**: OpeningCutscene đặt Health.Floor = 1 cho 2 Goblin_Torch (không chết). Còn 1 máu -> Held + bất tử, quát
  ("Ow-ow-OW! I'm telling Grok!" / tên cuối "You'll regret this! Grok will hear of it!" + khoá điều khiển 1.1 giây, HUD mờ, Ren nhìn theo),
  chạy về bìa rừng phía tây (33, 79), mờ dần rồi biến mất -> khu zf tính là dọn xong -> Todd ra. HUD: "Foes defeated: x / y".
- **Điểm nghỉ dễ nhận ra**: `World/ShrineMarker.cs` (MapTriggerHandler tạo cho mỗi checkpoint): vòng phù văn sáng dưới đất (m3_lk_sigil lặp),
  Light2D xanh lam, ngọn lửa của điểm nghỉ (tile object có hoạt ảnh trong 1.6 đv) đổi màu: chưa thắp = tàn xám, đã thắp = lửa linh hồn xanh.
  Bước vào lần đầu: Kindle (bùng sáng m3_heal_lantern nhuộm xanh) + gợi ý "Shrine kindled… Press T to rest - health and flasks restored."
- **Bóng thoại** mọc LÊN từ điểm neo (nền + chữ dời lên nửa chiều cao) -> không che đầu người nói.
- **Giới thiệu boss**: camera đưa hẳn về boss (boss + 0.45 đv lên) lúc Grok phàn nàn + đánh bay đệ; sau đó bảng kiểu Soul Knight
  (RequiemHUD BuildBossCard v2): nền tối 35%, dải màu nghiêng -6° (viền tối trên/dưới) trượt vào từ trái, tên lớn 132px trắng viền tối,
  biệt danh vàng nhạt, chân dung = sprite hiện tại của boss phóng to (nét pixel) trượt vào từ phải; 2.8 giây rồi trượt ra; boss mới được đánh.
  Màu dải: thuộc tính `card_color` (hex) trên object boss, mặc định nâu ô liu (0.47, 0.36, 0.16).
- Code backup: Catalog/backups/polish_2026-11-02/.

## §120 Còn 3 lửa trại hồi sinh, lửa trại tự sáng, nông dân ra đồng, goblin chạy về cầu, bảng boss v3 (02/11 19:xx) — đã test trong Unity
- **Điểm hồi sinh**: map1_shrines3.py bỏ zf_home (3015), z3_fire (3031) và lửa trại trước doanh trại (4425–4428). Còn 3 điểm chia đều theo
  quãng đường: z0_fire (~29 ô từ chỗ xuất phát), z2_fire (~87 ô), z4_fire (~165 ô). Chết trong làng -> về điểm xuất phát.
- **ShrineMarker v2**: bỏ vòng ấn dưới đất. Lửa trại tự nổi bật: đèn Light2D ấm nhấp nháy, ngọn lửa sáng hơn, tia lửa bay lên;
  khi đã thắp -> chuyển xanh linh hồn + bùng sáng. v1 lưu tại Catalog/backups/polish_2026-11-02/ShrineMarker_v1.cs.txt.
- **Nông dân**: enter_from "39.8,78.6;48.9,79.2;48.9,82.2" -> đi từ góc nhà ra nông trại sau khi goblin bỏ chạy.
- **Goblin bỏ chạy** (OpeningCutscene.Flee): chạy về phía cầu đông TileToWorld(91.0,79.4) — hướng doanh trại Grok — 4.5 giây, mờ dần 1 giây cuối.
- **Bảng boss v3** (RequiemHUD.UpdateBossCard): nền tối 55%, dải nghiêng -5° cao 26% màn hình (viền mực + vạch sáng), tên 180px viền trắng
  + bóng, biệt danh nằm dưới tên trong dải (vị trí tính theo độ nghiêng `yAt(x)`), chân dung riêng Resources/UI/Requiem/boss_grok.png
  (Catalog/v2/tools/boss_portrait.py: frame 0 Troll_Idle, bỏ bóng đổ bán trong suốt, viền trắng 3px) cao 2.15× dải, đầu vượt khỏi dải,
  có bóng tối lệch + nhịp thở; chớp trắng 0.26 s. Thuộc tính boss: `portrait` = boss_grok, `card_color` = #b5532a (map1_bossCard.py).
- Màn cắt cảnh có letterbox (2 dải đen 9%) ; thanh máu quái, mũi tên chỉ đường ẩn trong cắt cảnh; quái đang ngất/bỏ chạy (Held + Invulnerable)
  không hiện thanh máu.
- Phím thử: F1 bất tử, F3 ×9 (sau khi bỏ qua mở đầu) tới The Stone Ring.
- Minimap đã nướng lại. md5 map1 hiện tại: 6472fc346cefa5c01f14429bbf81ef06.

## §121 Hồi chiêu đánh thường, HEAL sau Hollow Caves, bớt thằn lằn z0/z1 (02/11 20:xx) — chờ test
- **Đánh thường**: PlayerCombat.attackCooldown = 0.5 s, tính từ khung trúng đòn -> nhát kế tiếp sớm nhất sau hitTime + 0.5 ≈ 0.98 s
  (trước đây 0.6 s tính từ lúc bấm). Lăn huỷ nhát chém không xoá hồi chiêu. Muốn nhanh hơn: giảm attackCooldown trên Player trong scene.
- **HEAL tutorial** (TutorialDirector): bỏ mốc ≤ 40% máu. Giờ chỉ hiện sau `z2_cleared` (khu nhện + rắn Hollow Caves), khi đã yên
  (không quái trong 7 đv, không mất máu 1.2 s), đang bị thương và còn bình.
- **Thằn lằn** (map1_lizards.py, md5 6472fc34… -> fc3d416102555c4c2e51ffb42e3c031e): z0 còn 1 con (2986 tại 98.5,51.6 — giữa lối đất
  giữa lửa trại và đầu cầu, để học né 1-1); z1 còn 3: đợt 1 = 2988 (75,52) trên đường sau cầu; đợt 2 = 2991 (64,49.4) mép trên đường
  + 2989 (59,54.6) mép dưới, ra trễ 1 s. Bỏ 2984, 2985, 2987, 2990. Ảnh kiểm tra: Catalog/previews/z0z1_spawns2.png.
- Backup code: Catalog/backups/polish_2026-11-02/{TutorialDirector,PlayerCombat,PlayerMelee}_v1.cs.txt.

## §122 Lan can cầu che nhân vật, bỏ cọc thừa ở cổng Meadow, Stone Ring còn 3 cột (02/11 20:xx) — chờ test
- Bản map trước khi sửa đã được lưu lại bằng Tiled (chỉ đổi thứ tự thuộc tính) -> md5 nền 8ae0df93…; sau map1_bridgesPillars.py: 278cce275ca7792164436f2e0a7e87d9.
- **Cầu**: lớp mới `bridge front` (unity:SortingOrder 3, trên nhân vật = 2): dây trước cầu gỗ (2929–2939, chuyển từ props front) + ảnh bờ
  trước cầu đá (Catalog/v2/tools/bridge_front.py tách từ ảnh gốc -> Props/SR-trimmed/*- front.png, tile 7–10 trong SR-props-trim.tsx;
  đặt trùng 1078–1080 Stone Ring và 3947 cầu làng). Thu hẹp dải đi: nước 3406 y 18 -> 17.45, tường 4129 80.12 -> 79.7, dây 3071 52.625 -> 52.3.
- **Cổng Meadow -> Caves**: bỏ hàng cọc tĩnh z1_palisade_1..5 (đứng lại sau khi cổng mở, trông như cổng chưa mở); thay bằng bụi cây
  z1_hedge_1..3 (blocker 3517 vẫn chặn).
- **Stone Ring**: còn 3 cột lớn, tam giác ngược đối xứng qua trục bàn thờ x=111: ring_pillar_1 (tâm 106.5, đáy 17.6), ring_pillar_3 (115.5, 17.6),
  ring_pillar_5 (111, 24.6); collider 3180/3178/3177 đi theo. Bỏ ring_pillar_2/4/6 + collider 3179/3175/3176. Boss chỉ còn tối đa 3 lần choáng.
- Minimap nướng lại.

## §123 Lửa trại: một lời nhắc, chỉ đường tới lửa trại đầu, biểu tượng trên bản đồ, thắp từ mọi hướng (02/11 21:xx) — chờ test
- **Một lời nhắc**: bỏ Hints "Shrine - press T to rest". Chỉ còn ô HUD `[T] Rest at the campfire` (chữ lấy từ thuộc tính `rest_noun`
  của checkpoint, mặc định "shrine" cho Map 2/3). Lần đầu thắp: dòng giải thích hiện ngay dưới ô đó 7 giây (ShrineMarker.Notice), không còn gợi ý thứ hai.
- **Chỉ đường**: QuestTracker có `detours` trong quest JSON. Map 1: sau `todd_greeted`, mũi tên / minimap / dòng phụ nhiệm vụ chỉ tới lửa trại
  Brookside (105.8, 56.6) "Campfire - rest here to set your respawn" cho tới khi có `tut_shrine` (đã thắp) hoặc `z1_started` (đã đi tiếp).
- **Bản đồ**: biểu tượng lửa trại (Resources/UI/Requiem/map_campfire.png 16x16, tô màu khi vẽ) trên minimap và bản đồ lớn (M):
  cam = chưa thắp, xanh = đã thắp. Nguồn: ShrineMarker.All.
- **Vùng thắp** (map1_campfires.py, md5 278cce27… -> 595e2527902b7f8362f2f051951cc570): rect 4.2 x 3.4 ô đặt tâm trên collider đống lửa
  (trước đây chỉ có dải 3 x 1.4 ô ở phía nam). Điểm hồi sinh = chỗ người chơi đứng lúc bước vào (không phải tâm vùng — giữa đống lửa có collider).
  ShrineMarker chỉ lấy lửa / khói nằm trong vùng (0.4 đv) và đặt đèn ở tâm vùng.
- Minimap nướng lại.

## §124 Mũi tên sau Brookside, bóng thoại bám theo người nói (02/11 21:xx) — chờ test
- Quest bước "Take the north road" (done z1_started): trước `z0_cleared` chỉ Brookside Trail (100,54); sau đó chỉ đầu cầu phía tây
  "Bridge to Thornback Meadow" (82.5,52) — trước đây vẫn chỉ ngược về (100,54) khi đã đánh xong thằn lằn và đang qua cầu.
- SpeechBubble.Follow(transform): bóng thoại bám người nói (LateUpdate, giữ độ lệch lúc nói). Dùng cho goblin doạ / bỏ chạy (OpeningCutscene),
  câu gọi đệ của Grok (BossIntro), câu tự nhủ của Ren ở dải turn_back (MapTriggerHandler). Bóng cũ: Catalog/backups/polish_2026-11-02/SpeechBubble_v2.cs.txt.

## §125 Đợt nâng Map 1 lên 8+: chạy thử, cân bằng, lưu game, sắp lại quái trại, khép truyện, góc bí mật (02/11 22:xx)
Chạy thử trong Unity (mở đầu -> Todd -> Brookside -> cầu dây -> Stone Ring) và sửa các lỗi tìm thấy:
- **Bài ATTACK trượt / bỏ qua**: (1) bỏ qua cảnh nhanh thì quái của khu chưa kịp ra (sinh có báo trước) -> cả bài hướng dẫn bị bỏ,
  goblin chết thay vì bỏ chạy. OpeningCutscene chờ quân ra (tối đa 8 s). (2) Nhát chém 25 không hạ được Torchbearer 40 máu ->
  goblin không bỏ chạy, người chơi mất ~30% máu ngay bài đầu. Giữ goblin + người chơi bất tử 0.7 s sau khi bấm.
- **Cân bằng sau hồi chiêu 0.5 s** (nhịp chém ~1 s): sát thương chém Map 1 25 -> 30 (bằng Map 2/3); Goblin_Torch 40 -> 30 máu (quái lót, 1 nhát).
  Số nhát: Torch 1, Lizard/Spearman/Hexer/Gunner 2, PigRider 3, Blightdrinker 5, Gấu 8, Grok 20.
- Goblin bỏ chạy 2.8 -> 1.7 đv/s (người chơi 2), 3.8 s. Tên thứ hai tự chạy lại gần (1.5 đv) rồi mới quát (trước đây quát ngoài màn hình rồi đứng im).
- **Todd nói chuyện từ bậc cửa** (cách Ren cả sân): Villager.Blocked coi chạm-lúc-bắt-đầu (khoảng cách 0) là chặn kể cả khi đi RA -> sửa;
  RaidAftermath đặt Todd cạnh Ren nếu vẫn chưa tới. Thợ rèn đi vào theo mép trên đường (map1_smithPath) — không chen vào Ren.
- **Kẹt ở góc cầu làng**: bỏ collider cột đèn 4132, thêm 2 tam giác polygon 4436/4437 ở 2 đầu phía bắc cầu (trượt xuống mặt cầu).
- QuestTracker chỉ được tạo ở scene đầu tiên của phiên -> tải lại / sang map / Continue mất minimap + nhiệm vụ. Giờ tạo lại mỗi lần nạp scene.
- Nhãn điểm ghé: "Brookside campfire" (nhãn cũ bị cắt).
**Lưu game** (Gameplay/Flow/SaveGame.cs, file persistentDataPath/soulrequiem_save.json): lưu khi thắp lửa trại và khi nghỉ (T).
  Nội dung: mọi cờ (khu đã dọn, nhiệm vụ, hướng dẫn đã xem, `picked_<trigger>` đồ đã nhặt, `lit_<checkpoint>` lửa đã thắp), điểm hồi sinh,
  bình tối đa, healPercent. Title: Continue -> RequestLoad (nếu bản lưu cùng scene), New Game -> xoá. Director.Start nạp cờ trước, đánh dấu
  khu đã dọn (hang: DebugFinish), RestoreFromFlags, đặt người chơi ở lửa trại. Phím thử F10 = tải lại map từ bản lưu.
**Greystone Camp** (map1_campWaves): mỗi đợt 1 loại mới — 1: Spearman+Torch, 2: Hexer+Spearman, 3: Gunner+Torch, 4: PigRider một mình.
**Truyện**: nhặt nanh Grok -> Ren: "Hold on, Millbrook. I'll bring it home." (thuộc tính `say` của pickup), depart_text nhắc nanh vẫn trong tay
  -> móc truyện cho Map 2/3 (tìm đường về Millbrook — CHƯA làm phần trả). Chữ dùng chung theo ShrineMarker.Noun (campfire/shrine): màn chết,
  gợi ý chết lần đầu, menu nghỉ ("REST BY THE CAMPFIRE"), nút Mantras.
**HUD**: ô chiêu trống ẩn khi không còn chiêu chưa lắp; ô hiện ra (loé) khi học chiêu.
**Góc bí mật** (map1_secrets): z0_secret_potion (108.4,48 — hốc sau cây lớn đông-bắc lửa trại Brookside, +1 bình);
  z4_secret_flask (52.8,12 — sau tường đổ tây-bắc Ruins Path, flask_upgrade: +15% hồi mỗi lần uống, +1 bình). Chưa kiểm tra tận nơi trong Unity.
Map md5 sau đợt này: d324926aebed46e31c0c35f4989b55e8 (người dùng có sửa tay trong Tiled giữa chừng: xoá raid_debris, vendor, chỉnh lửa trại).

## §126 Chốt cốt truyện cuối Map 1 (02/11 23:xx)
- Người dùng chốt: vòng đá sụp chỉ là TAI NẠN; goblin không biết gì về bí mật lớn của thế giới; nanh Grok hết là mục tiêu quan trọng
  sau khi rơi xuống — nhiệm vụ chính ở Map 2 là THOÁT THÂN (quest Map 2 bước 1 đã là "Find a way out of the Undercity").
- map1_tuskAccident.py (md5 ac83acff… -> mới): bỏ câu hứa "I'll bring it home"; nhặt nanh -> Ren: "What - the ground's giving way!";
  depart_text: "The ground gives way - you fall into the dark beneath the ring!". Thay cho phần "móc truyện trả nanh" ghi ở §125.
- KHÔNG làm: sổ thu thuế / lời trăng trối của Grok về "the Eye" (đề xuất bị bỏ).
- Lệch tài liệu: cốt truyện chính thức ghi người thuê là Tobin (giếng nước đắng); game hiện dùng Todd + thuế lương thực — chưa chốt.

## §127 Cảnh sập hầm cuối Map 1 (02/11 23:xx) — chờ test
- Gameplay/World/Cutscene/RingCollapse.cs (lớp tĩnh, coroutine chạy trên director), ~3.1 s trước SceneTransition.Go:
  rung camera tăng dần + thanh điện ảnh; bụi (m3_dash_dust) / đá vụn (m3_break) rơi quanh tâm; 6 vết nứt (LineRenderer gấp khúc,
  nhánh đầu chạy về phía Ren) dài dần, đầu nứt bật đất (m3_ground_burst); cột đá còn đứng lún xuống + mờ đi; 2.2 s: miệng hố đen
  (ảnh tạo bằng code) mở dưới chân Ren, giật camera mạnh, Ren tụt xuống (thu nhỏ, tối dần). Sau đó SceneTransition rung + tối như cũ.
- MapTriggerHandler.CollapseThenExit: nhặt đồ có then_exit -> nếu lối ra có `collapse = true` thì chờ 0.6 s rồi chạy cảnh, tâm `collapse_center` (ô).
- map1_collapseFx.py: to_map2 collapse = true, collapse_center = "111,19.8" (tâm vòng ấn). md5 -> ef13903da515733217be704ab25a2164.

## §128 Sập hầm v3 dùng hình cùng tileset, nanh Grok tự vẽ, Todd không còn bay tới Stone Ring (02/11 23:xx) — đã xem trong Unity
- Đánh giá v1 (đã chạy thử): hố elip đen vẽ bằng code (mép mờ, phối cảnh sai), vết nứt LineRenderer (nét vector mượt),
  m3_break / m3_dash_dust của Map 3 hiện thành đĩa vàng cam, Ren bị thu nhỏ (vỡ lưới pixel), cột mờ alpha -> nhìn "giả".
- v3 (RingCollapse.cs): chỉ dùng hình cùng bộ tileset map 1 — Resources/VFX/collapse/ (tools/collapse_sprites.py từ
  Tilesets/"hole- transparency.png", khối hố làm sẵn 5x6 ô): collapse_hole_2 (4x5) -> _3 (5x6) -> _4 (lòng hố sụt thành khoảng tối,
  dither 2x2 theo màu đất), collapse_lip (mép cỏ trước, vẽ đè Ren khi rơi), sỏi rơi (rocks color scheme 1 - 27/28/29),
  bụi đất tự vẽ collapse_dust_0..5 (36x22, màu đất, tan bằng dither). Ren tụt xuống sau mép cỏ + tối dần (không thu nhỏ).
  Nhịp: 0.6 s hố hé, 1.5 s hố lớn, 2.5 s sụt tối, cột lún từ 1.0 s và sụp trong bụi ở 2.6 s, tổng 3.4 s rồi SceneTransition.
- Nanh Grok: trước dùng "bone - small - 14" (khúc xương chân) -> tools/tusk_sprite.py vẽ nanh cong (viền mực Tiny Swords, gốc gãy,
  bóng đổ) Props/SR-trimmed/grok tusk.png, tile 11 của SR-props-trim.tsx; map1_tuskSprite (4386 gid 12083, 30x18).
- RaidAftermath: chỉ diễn khi Ren ở gần nhà Todd (≤ 4.5 đv); chỉ đặt Todd cạnh Ren khi lệch < 3 đv
  (lỗi thấy khi chạy thử: dọn làng bằng phím thử khi đang ở xa -> Todd bị dịch chuyển tới Stone Ring).

## §129 Map 2 – Giai đoạn 0: chạy thử + sửa lỗi nền (03/11 02:xx)
Chạy thử trong Unity: Hatch -> Gallery -> Ether Well -> Sluice Hall -> Records Vault -> Archive -> Lab -> Specimen Cells ->
Culture Hall (Gorgemaw) -> Pump Control -> Feed Conduit -> Office -> Boiler Room (Ether Ram) -> Cistern Gate -> Eye's Cistern.
Lỗi đã sửa:
- **Bình máu 30** khi vào thẳng Map 2/Map 3 (giá trị thử trong scene; Continue cũng nạp lại 30): scene Map2/Map3 `potions` 30 -> 3.
- **Trận đầu sau khi rơi xuống quá gắt** (Gallery: 5 con lao vào cùng lúc, ~22 sát thương/giây, chết sau ~4 s): chia 3 đợt
  (chuột + 2 nhện / chuột + nhện / 2 chuột) — map2_phase0.py sửa thẳng .tmx (md5 ffedabee… -> 788cea4e…; KHÔNG chạy generator).
- **Hồi sinh bị quái đuổi tới tận chỗ**: ResetLockedZones chỉ làm lại khu có cổng khoá -> quái khu thường đi theo. Giờ mọi khu đang
  đánh dở (không chứa điểm hồi sinh, không có hang sinh quái) được làm lại: xoá quái, đặt lại đợt.
- **Lời dẫn rơi xuống hiện lại mỗi lần hồi sinh**: biển tự hiện (AutoSign) chỉ hiện 1 lần (cờ `seen_<tên>`).
  Chữ: "The altar gives way" -> "The stones give way…" (khớp cảnh sập hầm Map 1).
- **Điểm nghỉ Sluice Hall không nghỉ được**: biển hub_south cạnh nến giành phím [T] (ô nhắc nghỉ bị ẩn). InteractionSystem bỏ tiêu điểm
  khi đang đứng trong điểm nghỉ -> [T] = nghỉ.
- **Bảng tên boss**: tên dài (GORGEMAW, THE ETHER RAM) bị chân dung che -> cỡ chữ theo độ dài (≤6: 180, ≤9: 140, còn lại 108),
  chân dung luôn đứng sau tên (đo bề rộng chữ).
Ghi nhận cho giai đoạn sau: chân dung boss Map 2 còn nhỏ, màu dải mặc định; điểm nghỉ = cụm nến trên sàn, khó nhận ra;
Feed Conduit sàn Ichor xanh chói; chưa có cảnh mở đầu / Con Mắt / thoát ra.

## §130 Map 2 – Giai đoạn 1: các cảnh diễn (03/11 03:xx) — đã chạy thử từng cảnh trong Unity
Mọi sửa .tmx bằng script riêng có kiểm md5 + sao lưu (Catalog/backups/map2_2026-11-03/<bước>), KHÔNG chạy generator.
Chuỗi md5 Map 2: 788cea4e… -> landing 6c236c54… -> eyeGlimpse 10633ea2… -> bossIntros 2568b312… -> escape c88e0467…
-> drainFocus **6f66d882…** (hiện tại).
- **Tiếp đất** (LandingCutscene.cs, điểm `landing_point`): Ren rơi từ miệng hầm (sỏi rơi theo) -> bụi + giật camera -> ngẩng lên,
  trần rung -> tự lăn né xuống nam -> đống đất đá (Resources/VFX/landing/landing_rubble, có va chạm) sụp bịt miệng hầm, cột sáng
  `hatch_beam` + đèn `hatch_shaft` mờ đi -> "The way up... gone." / "Then I find another way out." -> gợi ý. Cờ `landed`;
  điểm hồi sinh dời ra chỗ Ren sau khi lăn. Continue / bản lưu cũ / Ren không ở miệng hầm: đặt sẵn đống đá, không diễn.
  Đá Map 1 nhuộm xám lạnh (dưới ánh xanh của hầm không còn vàng chói). Biển r1_hatch bị tắt (`mute_sign`).
- **Thấy con Mắt lần đầu** (EyeGlimpse.cs, điểm `eye_glimpse` ở Cistern Gate): camera lia sang hố, con Mắt trong khiên Ichor bừng
  sáng + đất rung -> "...What is that thing?" -> gợi ý lối ra duy nhất nằm sau nó. Cờ `eye_glimpsed`; không diễn khi đã `ichor_diverted`.
- **Màn xuất hiện boss** (BossIntro.Entrance + CutsceneFx.cs dùng chung): thuộc tính trên object `boss` của Tiled —
  `entrance` = burst | leap | wake, `entrance_from`, `entrance_shake`, `entrance_flag`, `entrance_hide/show`, `card_name`, `card_sub`,
  `portrait`, `card_color`, `wake_color`.
  · Gorgemaw (cult_fight): bồn nuôi `cult_capsule_full` rung, cờ `gorgemaw_out` -> toggle `gorgemaw_capsule` đổi sang bồn rỗng
    `cult_capsule_empty`, cóc nhảy vòng cung xuống sàn.
  · Ether Ram (boil_fight): 3 cú nện sau nồi hơi `boil_engine` (rung), phá ra, lao (ChargeRun) kéo bụi; thẻ "ETHER RAM / Terror of the Boiler Room".
  · Specimen Zero (vault_fight): 3 nhịp tim ánh tím mạnh dần rồi thức (Awake); thẻ "The Sleeper in the Vault".
  · Plague Eye (plague_eye_fight): thức dậy ánh xanh; thẻ "PLAGUE EYE / Malric's Great Experiment".
  Chân dung: tools/boss_portraits_m2.py -> Resources/UI/Requiem/boss_{ram,toad,specimen,eye}.png (+ .meta chép từ boss_grok).
  HUD: chân dung chừa đủ nửa ảnh + 18 px sau tên (Plague Eye che chữ E cuối).
- **Xả bể** (PipeGridMechanism.Solved): máy bơm gầm -> camera lia chậm tới hố (`after_focus` 107.8,41.8) -> ĐÚNG lúc tới nơi mới bật
  `ichor_diverted` (khiên vỡ, dòng cấp / ánh xanh tắt) + loé xanh, bụi, rung + `after_text`. F5 (test) cũng chạy cảnh này.
- **Thoát thân** (EscapeSurge.cs, điểm `escape_surge`): hạ con Mắt -> hố trào Ether (rung, loé, bụi) -> lia tới ống thoát
  (`exit_at`) -> "Up. Now - before this place drowns." -> gợi ý lấy lõi Mắt rồi leo ra. Sau đó hố phập phồng, đất rung lâu lâu tới khi
  rời map. Cờ `escape_seen`.
Còn lại (giai đoạn 2–3): hướng dẫn Ether spout / đỡ đòn (Turned Blade) / lắp chiêu khi nghỉ; khung hội thoại cho chữ dài; điểm nghỉ dễ
thấy; cân bằng; kiểm tra lưu trên Map 2. Bước cuối nhiệm vụ "Climb out through the outflow" vẫn done = never (rời map là xong).

## §131 Map 2 – sửa theo ảnh chạy thử của người dùng + hoàn thiện (03–04/11) — đã chạy thử trong Unity
Chuỗi md5 Map 2 (script riêng, kiểm md5 + sao lưu Catalog/backups/map2_2026-11-0x/<bước>, KHÔNG chạy generator):
830fabc7… -> (người dùng sửa trong Tiled: bỏ vũng Ichor + hazard cult_acid_0/1/2 dưới bồn nuôi, dời 175 và gorgemaw_capsule) 7e01834e…
-> polish4 98021f97… -> polish4b 262ec811… -> hubRest **9b6d92a1…** (hiện tại).
- **Tay quay của Malric** (map2_crankLever.py): vùng nhặt lệch ~6 ô so với hình -> dời đúng lên hình `lever_handle_item`; mốc nhiệm vụ
  (13.6, 37.2); pickup `marker = true` -> vòng sáng + đốm lấp lánh (NoteMarker.CreateAt + MarkerPulse) tới khi nhặt.
- **Cần gạt bơm**: console `marker_item = pump_lever`, `marker_off = ichor_diverted`; đèn sock_crank dời từ máy hơi sang cần gạt.
- **Đấu trường boss** (ArenaSeal.cs — QUY TẮC CHUNG, xem ARCHITECTURE §2.6): boss bị kẹp trong khung khu; khu không có cổng
  close_on_enter -> 4 tấm chắn dày 0,6 đv + màn sáng Ether ở khe hở tường + người chơi bị kẹp trong khung; boss gục / khu dọn / gục -> tan.
  Chạy thử: lần 1 EdgeCollider mỏng bị Ram ép xuyên qua + khe hở chạm góc khu bị bỏ sót -> sửa (tấm dày, chốt khe cuối cạnh).
  Ether Ram giờ không ra khỏi Boiler Room; Ren cũng không ra được tới khi hạ Ram. Map 1 / Grok, Gorgemaw, Plague Eye có cổng khoá -> chỉ kẹp boss.
  **Vẽ khu boss mới: khung khu = đúng sàn phòng đấu.**
- **Bể kính Gorgemaw** (map2_toadTanks.py, TankMechanism, HazardMechanism `start_when`): bỏ 4 decal nước đen; bể vỡ -> cờ `<bể>_broken`,
  vũng Ichor `<bể>_acid_fx` loang từ chân bể, vùng rát `<bể>_acid` (3%/s + độc) + đèn xanh. Pha 2 (≤50% máu): cóc nhảy vọt ra giữa khu,
  vỡ cả 4 bể, cóc con chui lên từ các vũng (mỗi vũng một con), các lần gọi sau cũng từ vũng. (ToadSkills.PhaseLeap / Brood / Emerge)
- **Turned Blade** (Combat/TurnedBlade.cs, Projectile.Reflect / Live): trước đây sách chỉ bật cờ, KHÔNG có tác dụng. Giờ chém trúng đạn
  (hoặc đạn bay vào hình quạt trong 0,2 s sau khung chém) -> đạn dội ngược, nhanh ×1,6, sát thương ×2, chỉ trúng quái.
- **Hướng dẫn giữa đường** (CombatTutorials.cs, mọi map): TURN THE BLADE (dừng đúng lúc đạn sắp trúng, tính cả 0,48 s vung kiếm);
  NEW MANTRA (nhặt chiêu -> dừng, bấm phím chiêu 1 thử ngay, nhắc đổi chiêu ở điểm nghỉ). Vòi Ether: giữ biển r2_spout_hint.
- **Điểm nghỉ dễ thấy**: checkpoint `marker = true` (cả 5 điểm Map 2) -> vòng sáng tới khi thắp. Vùng nghỉ nới 0,25 đv; hub_rest phủ chân nến,
  biển hub_south `range 0.3` (trước đây đứng sát nến bấm T lại đọc biển).
- **Feed Conduit bớt chói**: lớp "flood base" #6e9a78, "curb water A" #6a9276, đèn acid_feed / acid_cistern 0,45 -> 0,25.
- **Gợi ý chữ dài**: Hints tự kéo dài ≥ 16 ký tự/giây.
- **Mang theo qua map + lưu**: sách `book_*` vào GameSession.Books (sang Map 3 còn Purifier / Turned Blade); SaveGame lưu bộ chiêu
  (owned / equipped) -> Continue không mất chiêu; New Game -> GameSession.NewGame() xoá đồ mang theo của lượt trước.
- Đã chạy thử: tay quay (nhặt đúng chỗ), vòng sáng cần gạt / điểm nghỉ, màn chắn Ram (chặn được, tan khi Ram gục), cóc con chui từ vũng,
  xả bể (F6), Plague Eye + thoát thân, cổ vật -> NEW MANTRA (F), Turned Blade (dội đạn Specimen Zero), nghỉ -> lưu -> F10 nạp lại
  (về đúng điểm nghỉ, nến đã thắp, không diễn lại cảnh tiếp đất).
Còn lại: cân bằng chi tiết (chơi tay một lượt không F-key).

## §132 Gorgemaw (Brood Toad) – sửa cơ chế theo phản hồi người chơi (04/11) — CHƯA chạy thử (người dùng tự test)
Phàn nàn: (1) đang diễn cảnh vẫn bị quái đánh; (2) vừa qua cổng có con ếch lao vào; (3) vũng acid nhả ếch gần như vô hạn.
Nguyên nhân:
- BossIntro bật `Invulnerable` cho Ren nhưng PlayerDodge tắt cờ khi lăn xong -> lăn vào phòng là mất bất tử giữa cảnh.
  Độc / cháy gọi ApplyDamage thẳng (không qua Invulnerable). Quái thường không bị Held -> vẫn đuổi đánh lúc cảnh chạy.
- Skinned_Hopper đợt 1 đặt ở (10,86) ngay dưới cult_door.
- ToadSkills: pha 2 gọi Brood mỗi `broodEvery` 12 s (tối đa 4 con sống) và từ 03/11 mọi lần đều chui từ vũng -> giết xong lại ra.
Sửa (chung cho mọi map):
- **Health**: người chơi (`isPlayer`) tự bất tử khi `CutsceneState.Active` (`CutsceneSafe`, không phụ thuộc cờ của Roll); nhịp độc / cháy bị bỏ qua.
- **EnemyAI**: mọi quái đứng im + Idle khi `CutsceneState.Active`, trừ con có `CutsceneActor = true` (OpeningCutscene bật cho tên cướp đầu).
Sửa riêng Gorgemaw:
- **Brood một lần**: chỉ sau cú nhảy vào pha 2 — mỗi vũng một con, lần lượt cách 0,45 s, gợi ý "Hoppers crawl out of the spilled Ichor!".
  Không còn đợt lặp (`broodEvery` giữ cho prefab, không dùng). Gục boss -> cóc con chết theo (như cũ).
- **Cửa sổ trừng phạt pha 2** (pha 2 hết bể để đập): Leap Slam đáp trong `sinkRadius` 1,1 quanh vũng Ichor -> cóc lún `sinkTime` 1,8 s.
- **Map**: bỏ hopper id 681 (10,86) — `Catalog/v2/map2_toadRework.py`, backup `backups/map2_2026-11-04/toadRework`. md5 mới fdf0c073ea11b939f6771c3981bdbace.
  Pha 1 = Gorgemaw + 1 hopper (24,90).
Backup code: `Catalog/backups/polish_2026-11-04/toad_rework/` (Health, EnemyAI, ToadSkills, OpeningCutscene).

## §133 Map 2 – sửa theo ảnh chạy thử đợt 2 (04/11 chiều) — CHƯA chạy thử (người dùng tự test)
- **Kết giới lệch** (ArenaSeal): dò khe hở thêm sâu 0,4 đv hai phía mép (collider tường sát mép mỏng hơn hình -> màn lấn chân tường);
  hai đầu khe bám lưới ô 0,32 (`AddRun`).
- **Vòng điểm nghỉ lệch nến**: vòng đặt dưới chân cụm nến thật (`ShrineMarker.TryFlameBounds`), không theo tâm vùng nghỉ đã nới.
- **Giữa trận không đọc biển**: `MapEncounterDirector.InCombat` (trong khu đang đánh còn boss / quái gây hấn, hoặc quái gây hấn trong 6 đv);
  `Interactable.calmOnly` = biển + ghi chú / bảng giải đố (Read / Examine). Cần gạt, van, tay quay không bị khoá.
- **Cắt 9 biển trang trí** (người dùng chọn): r1_hatch, hub_channel, hub_boss_door, ctrl_hint, feed_drain, arch_sign, cult_sign, boil_sign, vault_sign.
  r8_shield `hide_when ichor_diverted`. Script `Catalog/v2/map2_trimSigns.py`.
- **Plague Eye gọi thêm chuột cận chiến**: pha 1 `summonWave` = Plague Rat x2 + Vat-born; pha 2 `summonWaveP2` = Plague Rat x2 + Vat-born + Bloated Rat
  (sửa cả prefab Plague_Eye). Vẫn chỉ gọi đợt mới khi đợt trước chết hết.
- **Cổ vật Con Mắt rơi từ boss** (`World/Cutscene/BossRelic.cs`, director tự gắn): Plague Eye gục 1,4 s -> lõi mắt (vẽ bằng code, quầng + đèn xanh)
  trồi lên, lơ lửng, bay vòng cung vào ngực Ren (camera bám), loé sáng + 2 vòng Ether, Ren ánh xanh -> mở + TỰ LẮP bộ Con Mắt, cờ eye_relic_taken
  + new_mantra -> màn "NEW MANTRAS - MANTRAS OF THE EYE" (nhắc F / R / C). Rồi mới tới cảnh thoát thân (escape_surge `when` = r8_cleared,eye_relic_taken;
  EscapeSurge chờ cả TutorialPrompt). Bỏ pickup eye_relic + đèn eye_relic_glow ở mép hố. Bản lưu cũ đã hạ Plague Eye mà chưa nhận -> trao khi nạp map.
  Thuộc tính trên plague_eye_fight: relic_skill/flag/color/text/say/hint. Script `map2_eyeRelic.py`.
- **Bảng nghỉ hiện bộ chiêu thành MỘT thẻ** (3 biểu tượng, tên bộ, phím + hồi chiêu từng chiêu; chọn = lắp cả bộ). SkillDefinition thêm `setName`,
  `setDescription` (EyeOrbs.asset: "Mantras of the Eye"). PlayerLoadout `SetLeader` / `SetName`.
- **Specimen Cells thiết kế lại – "Hồ sơ mã số"** (`map2_specCells.py`): bỏ 4 quái đi lang thang; bảng hồ sơ đọc được ngay, liệt kê theo MÃ
  (G-2, G-5, V-3, V-4 hiền "Odo", V-7, V-9); cần gạt chỉ đọc mã trên cửa buồng (buồng 1 V-7, 2 G-2, 3 V-3, 4 V-4, 5 V-9, 6 G-5).
  Buồng hung: đèn đỏ chập chờn + 2 đốm mắt đỏ (`cell_eye_N_k`, tắt khi buồng mở); buồng 4: xanh dịu. Kéo đúng buồng 4 -> rương bình máu (như cũ).
- md5 Map 2 sau đợt này: 07285a31a6b7def6a60b7a02e2172293. Backup code: `Catalog/backups/polish_2026-11-04/batch2/`; map: `backups/map2_2026-11-04/<bước>`.
- (04/11 17:xx, người dùng) Bỏ màn dừng hướng dẫn NEW MANTRAS sau cảnh cổ vật: BossRelic không bật cờ new_mantra nữa, chỉ còn dòng gợi ý relic_hint.

## §134 Map 2 – sắp xếp quái theo cốt truyện + sửa đạo cụ phòng giam (04/11 tối) — CHƯA chạy thử
Lý do (ảnh Tiled của người dùng): phòng giam đặt bình VỠ + xương mà cần gạt lại thả quái sống; Map 2 dùng lại goblin Greystone của Map 1
(trái với biển "no goblin is left"), nhện rải khắp nơi.
Nguyên tắc: Map 2 chỉ có 2 "hệ sinh thái" — thú cống biến dị vì Ether đen (Plague / Bloated Rat, Channel Viper, Ichor Snapper) và mẫu vật của
Malric (Vat-born, Skinned Hopper, Mimic, các boss). Người dùng chọn: bỏ SẠCH goblin; giữ nhện chỉ ở Forgotten Store (kho bí mật).
- r2 Gallery: chỉ chuột (đợt 1: 2, đợt 2: 3). arch: 2 chuột + Mimic (đợt 2), bỏ Hexer. r6 Lab: đợt 1 3 Vat-born, đợt 2 Vat-born + 2 Skinned Hopper.
  office: nhện -> chuột. boil: bỏ 2 Barrel Goblin (trận Ether Ram chỉ còn boss). Còn lại giữ nguyên.
- Phòng giam: 5 buồng hung = bình nguyên `cell_N_tank` (capsule 32x96) + mẫu vật sống `cell_N_specimen` (tile động 32x64, lệch lên 6 px);
  kéo cần -> toggle `cell_N_shatter` (when cell_N_open): ẩn bình + mẫu vật, hiện `cell_N_tank_broken`; quái lao ra ở chân bình
  ("The tank's seals blow - glass bursts..."). Bỏ xương ở 5 buồng hung + 2 đốm mắt đỏ (§133); đèn buồng hung trở lại xanh dịch nuôi.
  Buồng 4 giữ bộ xương Odo + rương.
- `InCombat`: quái thả vào khu đã dọn (cần gạt) cũng tính là đang giao chiến.
- Script `Catalog/v2/map2_ecology.py`, backup `backups/map2_2026-11-04/ecology`. md5 Map 2: 170beaa23d0baf8d5f759e456eab6fc7.

## §135 Túi đồ + chìa khoá + rương + cửa tự nói, bỏ cần gạt phòng giam, Specimen Zero làm lại (04/11 tối) — CHƯA chạy thử
Người dùng duyệt: bỏ hết cần gạt phòng giam; mọi chìa Map 2 vào túi đồ; Odo's Key giấu ở Forgotten Store; Specimen Zero bộ chiêu mới + PixelLab vẽ rễ.
- **Lưỡi cóc**: không gây sát thương, dính trúng thì GIẬT Ren về sát miệng (AddImpulse v = sqrt(2·20·d)), lưỡi bám theo Ren lúc kéo; lăn né được.
- **Pump Control** không còn quái (`map2_ctrlCalm.py`).
- **Túi đồ** (`World/KeyRing.cs`, static): đồ định nghĩa bằng thuộc tính Tiled `key_id/key_name/key_desc/key_opens/key_icon`
  (pickup `item = key` hoặc rương chứa chìa). Có = cờ `key_<id>`, đã dùng = `key_<id>_used`. Menu Pause ▸ **Inventory** (RequiemMenus.BuildInventory):
  icon (Resources/UI/Requiem/key_pump|vat|seal|odo, item_crank), tên, "Opens: …", mô tả, "Used" mờ. KeyRing.Clear() ở Awake của director.
  Bản lưu cũ: pickup chìa đã nhặt (picked_*) -> tự thêm vào túi; rương có `sets` đã có cờ -> nằm mở sẵn + chìa vào túi.
- **Cửa khoá bằng chìa** (`MapGate.requiresKey/lockedSay`, `MapEncounterDirector.TickGateLocks`): cổng `requires_key` (không ghi unlock_when ->
  tự dùng cờ `<cổng>_unlocked`) chỉ mở khi Ren tới sát cửa (0,4 đv) và có chìa ("Used: … The lock turns."). Cửa còn khoá + `locked_say` ->
  Ren nói (bóng thoại), quay lưng, lùi một bước (hồi 4 s). Thay cho các biển "Examine" cạnh cửa.
- **Rương** (`Mechanics/Puzzles/ChestMechanism.cs`, type `chest`, verb Open): `chest` (tên hình rương), `style` 2/3 (khung Resources/VFX/chest/chest<style>_0..7,
  64x64, pivot (0, 9/64) khớp bản đã cắt 9 px lề dưới), `requires_key`, `requires`, `locked_say`, `potions_max`, `potions`, `skill_id` (để dành), `key_*`
  (rương chứa chìa), `sets`, `text`, `say`. Đã mở = cờ `<tên>_opened`.
- **Map** (`map2_keysChests.py`, md5 e3277e8f811fb6be58d609b37518015a): bỏ 6 cần gạt + đèn + toggle vỡ bình (bình mẫu vật giữ nguyên) + cell_4_potion;
  odo_chest (requires_key odo_key, +1 bình tối đa); Odo's Key (hình chìa + vòng sáng) ở Forgotten Store (27,36.6); bảng hồ sơ gợi ý chỗ giấu chìa.
  Chìa cũ -> item=key: pump (ctrl_door + cult_short), vat (cult_door), crank (vẫn bật cờ lever_handle cho bàn điều khiển); Records Vault: rương
  records_chest_lock (style 3) chứa Choir Vault Key (vault_gate), bỏ pickup + hình chìa cũ. locked_say thêm cho s2_barricade, s3_door, records_gate.
  Bỏ biển cult_door_hint, hub_south, vault_gate_hint, hub_west, r3_well, ctrl_console. Cờ nhiệm vụ cũ (pump_key, vat_key, lever_handle, vault_key) vẫn bật.
- **Specimen Zero** viết lại (`SpecimenZeroSkills.cs`): bỏ Volley (cầu tím). Grasping Roots (vết nứt -> rễ trồi nối đuôi, chậm 50% 1,4 s; pha 2: 3 vết xoè),
  Tentacle Lash (làn ngang sáng 0,9 s -> xúc tu trồi quét ngang phòng, đẩy lùi), Vessel's Gaze (khe mắt nạp 1,1 s, tia quét 70°/100°),
  Requiem Wail (rung, đỉnh gào mà không lăn -> khoá mantra 2,5/3,5 s; 3/4 vòng sóng), pha 2: xích đứt + 4 mắt tường (khoá vị trí Ren -> tia đâm).
  Hình rễ: Resources/VFX/specimen/roots_* (CHƯA có — chờ PixelLab; tạm dùng khung xúc tu nhuộm nâu).
- Backup code: Catalog/backups/polish_2026-11-04/batch3/.
- (04/11 20:xx) **Rễ Specimen Zero từ PixelLab**: Create image Pro 128x128 (10 lượt) -> 4 kiểu rễ thịt tím có gai trồi khỏi nền đá nứt
  (ảnh gốc 2x2: Catalog/v2/art/pixellab_roots_source.png). Mỗi kiểu tách lớp rễ / lớp đá vụn theo dòng "mặt đất" rồi dựng 8 khung
  (đá vụn -> rễ trồi 62%/28% -> vút cao +8% -> đứng x2 -> lún 32%/70%, đá mờ dần): Resources/VFX/specimen/roots{a..d}_0..7.png,
  PPU 180, pivot giữa đống đá vụn. SpecimenZeroSkills.LoadRoots nạp 4 bộ, mỗi gốc rễ chọn ngẫu nhiên một bộ (scale 0,85–1,05); khung trúng = 2.


## §136 Mạch truyện Map 2: tờ giấy ở Cistern Gate + đích nhiệm vụ phát sáng / nhấp nháy (04/11 tối) — CHƯA chạy thử
- Mạch: thoát ra -> Sluice Hall -> nhiệm vụ "Find the way out - east of the Sluice Hall" (Cistern Gate, cảnh EyeGlimpse thấy con Mắt)
  -> "Search the Cistern Gate" -> nhặt + đọc **A Sluice-keeper's Last Page** (người giữ cống goblin, quá khứ gần: lối ra là ống thoát trên hố;
  con Mắt bất khả xâm phạm khi bể của bộ chuyển đổi đầy; đổi dòng ở Pump Control; khoá phòng bơm: chìa trong bụng cóc, chìa vat hall ở Brakk,
  tay quay ở lab) -> "Open the Pump Room - turn the flow" -> ... Bước cũ của bản lưu cũ tự qua (done gồm pump_key|ichor_diverted).
- NoteMechanism thêm `sprite` (Resources, ảnh tờ giấy đặt mép dưới vùng), `marker` (vòng sáng tới khi đọc), `pickup` (đọc xong là nhặt:
  hình biến mất, không đọc lại tại chỗ) + `key_*` -> tờ giấy vào túi đồ (Inventory, icon item_note, mô tả tóm tắt việc phải làm).
  Ảnh: Resources/VFX/props/note_paper.png (chép từ Cemetery SR note paper), Resources/UI/Requiem/item_note.png.
- Map (`map2_cisternNote.py`, md5 dd207507e6557b201a9ac3a98ed6ba5b): note `cistern_note` (88.1–88.9, 54.5–55.3) phía tây cổng r8_lock;
  after_hint của eye_glimpse nhắc tờ giấy; bỏ biển r8_shield (tờ giấy đã kể).
- HUD (RequiemHUD): đích nhiệm vụ dễ thấy hơn — minimap: hình thoi 12 px nhấp nháy + quầng vàng phập phồng + vòng "ping" lan ra mỗi 1,2 s;
  mũi tên mép minimap 24 px nhấp nháy + quầng; bản đồ lớn: thoi 16 px + quầng + ping; trên màn hình: quầng dưới cờ đích, mũi tên mép màn hình
  40 px + quầng + nhấp nháy. Bật "Reduce flashing" -> không nhấp nháy / không ping, quầng đứng yên.

## §137 Sửa theo ảnh chạy thử (04/11 ~21h) — CHƯA chạy thử
- Tờ giấy `cistern_note` dời vào trong Eye's Cistern: (121–121.75, 50.2–50.9), giữa cột phải và cụm bình của bộ chuyển đổi; nhiệm vụ đổi thành
  "Search the cistern" (đích 121.4, 50.6); after_hint của eye_glimpse: "Something pale lies by the old converter, east of the pit."
- 5 bình mẫu vật phòng giam: chuyển từ lớp ground decals sang **props** (che đúng chiều sâu) + va chạm `cell_N_tank_col` (26x16 px ở chân bình,
  lớp obstacle colliders, collider_kind prop). Script `map2_noteTanks.py`, md5 a0da2db6be3d17ad7625de835fd09c4c.
- **Phím B mở / đóng túi đồ** (tay cầm LT): `GameAction.Inventory` (thêm CUỐI enum, mặc định Key.B, token {inventory}); RequiemMenus đọc
  PressedRaw để đóng được khi đang mở; không mở khi đang diễn cảnh / bảng đọc / hội thoại / bàn điều khiển. Nút trong Pause ghi kèm phím.
- Đích nhiệm vụ **bớt chói**: quầng mờ (0,18–0,32), ping chậm 1,6 s và mờ (≤0,45), nhấp nháy dịu (sin, 1,5 Hz); bỏ quầng quanh cờ đích và
  mũi tên mép màn hình (mũi tên 36 px, phập phồng nhẹ).

## §138 Sửa theo ảnh chạy thử (04/11 ~21h30) — CHƯA chạy thử
- Cửa khoá: bỏ quay lưng + lùi; chỉ còn bóng thoại `locked_say`.
- Tay quay: cần gạt `pump_lever` ẩn tới khi lắp tay quay (ConsoleMechanism `fit_object`/`fit_item`/`fit_text`/`fit_flag` = pump_console_fitted;
  lần đầu bấm T khi có cờ lever_handle: hiện cần gạt, tay quay trong túi đồ thành "used", rồi vào bàn điều khiển). Bản lưu đã đổi dòng -> hiện sẵn.
- HUD: Ren đang đánh trong khu chứa đích nhiệm vụ (`MapEncounterDirector.TargetReachedInFight`) -> ẩn mũi tên + điểm đích minimap.
- **Túi đồ dạng ô** (RequiemMenus.BuildInventory, rộng 1560): lọc All / Keys / Tools / Documents / Weapons (kèm số lượng); lưới 5x4 ô 132 px
  (nền skill_socket, ô trống mờ, ô chọn viền vàng), bấm / chạm ô -> bảng thông tin: icon 112, tên, loại (+ "used"), "Opens:", mô tả,
  PROPERTIES (bảng chỉ số `key_stats` "Nhãn:Giá trị;..." cho vũ khí sau này), WRITTEN ON IT (toàn văn tài liệu). Nút Close to (B).
  KeyItem thêm `kind` (key_kind; có `text` -> Document), `text`, `stats`. Tay quay key_kind = Tool.
- **Containment Vault nới rộng** (`map2_vaultWards.py` + `map2_vaultFloor.py`, md5 7637207137cfd366715dfa5beb49663c): mọi lớp tile cột 55–85
  hàng 101–107 = hàng (r-5) -> sàn 89–105, tường đáy 106–107; khu vault + vault_fight cao 608 px; sàn tím (decal 1389) nối thêm bản sao
  vault_floor_ext lệch xuống 157 px. Boss (chân 98.9) giờ ở giữa phòng. Minimap đã bake lại (tools/minimap_bake.py Map2_UndercityOfVael).
- **3 trụ phong ấn** sz_ward_1..3 (chân y 103.3, x 63 / 70 / 77): hình SR pillar thin 52x110 + va chạm 44x26 + điểm ritual_pillar.
  Requiem Wail: báo trước 2,2 s ("shelter behind the glowing ward!"), một trụ ngẫu nhiên bừng sáng xanh + dải sáng vùng an toàn phía sau
  (xa boss, dài 1,35, rộng 0,8); 3/4 vòng sóng (Shockwave chỉ để vẽ, damage 0) — SpecimenZeroSkills.RingHits tự xét trúng: lăn (bất tử)
  hoặc núp sau trụ sáng (Sheltered: dọc -0,12..1,4, ngang < 0,42) thì không trúng; trúng vòng đầu -> khoá mantra.

## §139 Màn chắn theo ô tile + sóng gào có khe, bỏ trụ phong ấn (04/11 ~22h15) — CHƯA chạy thử
- ArenaSeal: dò khe hở theo Ô của các lớp tilemap có va chạm (Collider2D trên tilemap hoặc con của nó) — ô ngoài + ô trong mép đều không có
  tile tường -> màn phủ trọn ô đó (khớp hình vẽ; trước đây dò collider mỏng nên màn hở một đoạn ở cửa Containment Vault).
  Không tìm thấy lớp tường nào -> dò collider như cũ.
- Bỏ 3 trụ sz_ward (`map2_removeWards.py`, md5 ade88be7b19bd19ece0bc1b3cbef37a4; minimap bake lại).
- Requiem Wail làm lại: báo trước 1,6 s bằng 2 dải sáng xanh từ chân boss theo hướng 2 KHE đối diện (64°), khe đầu lệch ~90° so với chỗ Ren
  (phải di chuyển); 3 vòng (pha 2: 4, khe xoay 30° mỗi vòng, dải sáng xoay theo); Shockwave gapAngles (khe có vạch sáng ở mép) — đứng trong
  khe hoặc lăn xuyên sóng thì không trúng; trúng vòng đầu -> khoá mantra.

## §140 Containment Vault: sửa blocker, trang trí nửa dưới, khắc chế tiếng gào (04/11 ~23h) — CHƯA chạy thử
- Blocker 872 (0,101 128x7 ô) chặn ngang nửa dưới phòng vừa nới -> tách 3: trái (0–58, 101–108), phải (82–128, 101–108), đáy phòng (58–82, 106–108).
- Trang trí (`map2_vaultDecor.py`, md5 12cbe0beec2c6f953c4126c4c021e5f0, minimap bake lại): vòng phong ấn 7 ký tự Choir (vault_seal_0..6) hình
  bán nguyệt quanh chân boss; 2 tượng + 2 xúc tu đối xứng ở 2 góc dưới; 4 mắt nhỏ; sương đáy phòng; 2 đèn tím (vault_glow_sw/se).
- Requiem Wail thêm cách khắc chế thứ hai: trong 1,6 s boss hít vào, dồn ≥5% máu tối đa (~2 nhát chém) -> tiếng gào nghẹn, boss khựng 1,8 s,
  không có sóng. Vẫn còn khe trong sóng + lăn xuyên sóng.

## §141 Trụ phong ấn PixelLab + chiêu Hand of the Vessel + trang trí lại vault (04/11 ~23h45) — CHƯA chạy thử
- Người dùng: dải sáng xanh của Requiem Wail "phèn"; muốn núp sau props hợp chủ đề / chữ sáng lên; thêm chiêu hay hơn (PixelLab).
- Ảnh PixelLab (Create image Pro 128, nguồn ở Catalog/v2/art/pixellab_ward_stones_source.png, _ward_sigil_source, _vessel_hand_source):
  - Resources/VFX/specimen/ward_stone0..3 (+ _lit): 4 trụ phong ấn khác kiểu; bản tắt = rune khắc chìm, bản sáng = rune vàng (đổi màu từ xanh).
    PPU 140, pivot chân trụ.
  - ward_sigil: vòng ấn vàng (chỉ giữ nét vàng, nền trong suốt). hand_0..7: bàn tay xác trồi lên (8 khung: nứt sàn -> trồi -> tóm -> lún),
    dựng bằng cách cắt lớp tay / lớp vết nứt ở dòng 92 rồi dịch lớp tay. PPU 128, pivot (0.5, 0.18).
- MỚI `SpritePropMechanism` (type Tiled `sprite_prop`, Mechanics/Effects): vật có ảnh Resources, chân chặn (BoxCollider2D), sắp theo Y cùng
  lớp nhân vật, decal vòng ấn trên sàn (decal_at/size/squash/alpha), đèn Light2D tắt sẵn, SetLit(bool), tra theo nhóm `Group("sz_ward")`.
- `Shockwave`: thêm `gapFrom` (khe chỉ mở khi vòng nở quá bán kính này — bóng che phía SAU vật) và `onSheltered` (gọi khi vòng đi qua Ren đang
  được che). InGap dùng `squash` thay cho 0,6 cố định.
- Requiem Wail bản 3: bỏ dải xanh + khe xoay. Boss hít vào 2,2 s (pha 2: 1,9 s): MỘT trong 2 trụ gần Ren nhất bừng sáng (rune vàng, đèn nến,
  vòng ấn sau trụ sáng dần). Vòng sóng phủ cả phòng (bán kính tính theo góc xa nhất), vỡ ra khi qua trụ sáng -> đứng sau trụ (vòng ấn) an toàn;
  lần đầu được che có dòng "The ward holds...". Pha 2: 4 vòng, sau vòng 2 trụ khác bừng sáng, có 1,4 s để chạy sang. Giữ: lăn xuyên sóng, trúng
  vòng đầu -> khoá mantra, dồn 5% máu lúc hít vào -> nghẹn + khựng 1,8 s.
- Chiêu mới Hand of the Vessel (hồi 8 s, sau Gaze trong thứ tự ưu tiên): bóng tối loang dưới chân Ren bám theo 0,9 s rồi khoá, GroundStrike
  0,45 s + bàn tay vọt lên (r 0,42, 110% sát thương, hất 3,5). Pha 2: 3 bàn tay nối tiếp (bám 0,45 s mỗi tay).
- Map (`map2_wardStones.py` md5 54b24679c97bda098d1cba336b5fc024 -> `map2_vaultDressing.py` md5 3f4856d1a5ca9e612cd081d039c662c0, minimap
  bake lại): bỏ 7 ký tự xanh vault_seal_0..6 + 2 xúc tu góc dưới; 4 trụ sz_ward_nw/ne/sw/se (60.4,95.4)/(79.6,95.4)/(64.6,103.2)/(75.4,103.2),
  vòng ấn cách chân 1,3 ô trên tia boss->trụ; nến hầm mộ hai bên mỗi trụ + đèn nến ấm nhẹ (sz_ward_*_candles); "người gác ấn ngã xuống":
  2 bộ xương + kiếm gãy sát tường dưới, xương rải, 4 vệt máu. Bỏ vũng ichor xanh/đá rêu (lạc tông).
- Code sao lưu: Catalog/backups/polish_2026-11-04/batch4/. Chưa biên dịch lại (Gameplay.dll 16:11 UTC) — mở Unity để kiểm tra.

## §142 Sắp lại phím test F1–F9 cho người test (04/11 ~23h55) — CHƯA chạy thử
- Vấn đề cũ: F5 = F6 (trùng), F10 bấm một lần làm 2 việc (tải lại bản lưu + mở Mantra Con Mắt), không có bảng nhắc phím, thiếu làm chậm game,
  vào thẳng pha 2 boss, nhận chìa, lùi khu; không biết đang bật cheat khi chụp ảnh / đo số liệu.
- `DevTools/DevCheats.cs` viết lại, nhóm theo việc: F1 bảng phím + trạng thái (khu, máu boss, bình) · F2 bất tử (máu + bình luôn đầy) ·
  F3 hạ hết quái · F4 khu kế / Shift khu trước / Ctrl lối ra · F5 giải câu đố + nhận mọi chìa / đồ then chốt của map (KeyRing.Known) ·
  F6 mở mọi Mantra (PlayerLoadout.Library) + đầy bình · F7 boss −25% / Shift vào pha 2 (49%) · F8 tốc độ 1×→0.5×→0.25×→2× / Shift ghi sát
  thương [DMG] · F9 tải lại bản lưu (dùng được cả lúc đang gục) / Shift tự gục. Bỏ F10.
- Tốc độ test được giữ khi menu / hit-stop / cảnh diễn trả timeScale về 1 (chỉ đặt lại khi timeScale đúng 1; không đụng lúc dừng 0).
- Nhãn "TEST · GOD · 0.5x · DMG LOG" ở mép trên khi còn cheat ảnh hưởng số liệu. Bảng F1 vẽ bằng OnGUI, co theo chiều cao màn hình.
- `MapEncounterDirector.DevTools`: thêm `DebugStepZone(int step)` (DebugNextZone giữ làm lối tắt). Chú thích phím ở PlayerCombat / Health / MapMechanics
  sửa theo. Sao lưu: Catalog/backups/polish_2026-11-04/batch5_devkeys/.

## §143 Map 2: sửa theo bản đánh giá (05/11 ~0h–1h) — CHƯA chạy thử
Bản đánh giá tĩnh Map 2 (04/11 khuya): 7,2/10 — điểm trừ: giữa game dễ lạc, giao tranh thưa, ichor chói/phẳng, phòng lớn trống,
quay lại xa, 120 đèn. Sáu việc người dùng chọn:
1. **Nhiệm vụ con**: `QuestTracker.Target.check` (dòng ngắn) -> `Checks` (done / active / locked); `RequiemHUD.UpdateChecks` vẽ ô tích bằng
   viền USS (không phụ thuộc phông có ☐), dòng đang được mũi tên chỉ sáng lên, dòng thiếu `pre` hiện "- locked". USS `.sr-quest-check*`.
   Quest JSON bước "Open the Pump Room - find what it takes": Malric's crank - the Laboratory / Vat Hall key - Brakk, Boiler Room /
   Pump Room key - Culture Hall (mũi tên vẫn chỉ món gần nhất còn thiếu; chìa Pump chưa có chìa Vat thì chỉ sang Boiler Room).
2. **Trận theo cốt truyện** (`map2_ambushes.py`, md5 7d33b9b88ff8ffad4292dd9138fd738d): khu phục kích RIÊNG chồng lên phòng (không gắn
   start_when vào khu phòng vì cờ `<khu>_started` của phòng là điều kiện nhiệm vụ; khu không có `ambient` -> giữ ánh nền phòng):
   - `spec_breakout` (Specimen Cells, start_when cult_cleared): hạ Gorgemaw -> buồng giam nứt, 2 đợt Skinned Hopper + Vat-born khi quay về.
   - `r4_swarm` (Sluice Hall đông-bắc 76–88 x 48–56, start_when eye_glimpsed): 2 đợt chuột + Bloated Rat — chừa lửa trại rest_cistern ra ngoài
     khu (gục thì không hồi sinh giữa trận đang làm lại).
   - Feed Conduit: đợt 2 thêm 2 Plague Rat, đợt 3 mới 2 Channel Viper (ambush water).
   - Sửa gợi ý cũ của Specimen Cells "Read the chart before pulling a lever" (cần gạt đã bỏ) -> rỗng.
3. **Ichor dịu màu** (`tools/ichor_calm_tileset.py` -> tileset mới `SR-ichor-calm` 8x19, firstgid 14455; `map2_ichorCalm.py`, md5
   0e1fdcc5fce5ba930b8dddc5e30fe6fb): 17 tile nước lime (8 khung) -> giảm bão hoà 0,55 / sáng 0,72 (chọn sau khi so 5 mức có nhân tint lớp),
   giữ mép cong, pixel sát mép trong suốt thành viền bọt; 532 ô cách bờ ≥ 2 -> biến thể "vùng sâu" tối hơn; mặt nền curb surface dịu màu;
   hoạt ảnh chậm 130 ms. Thêm 51 gợn sóng / bong bóng + 10 dải sương; trên bể "A" (Cistern + Feed) đặt tên feed_fx để ẩn khi xả bể.
   Không sửa asset gốc của gói Sewers. Vũng acid quanh bể vỡ Culture Hall giữ màu nổi (chỉ hiện khi bể vỡ — vùng nguy hiểm cần dễ đọc).
4. **Trang trí kể chuyện** (`map2_roomDressing.py`, md5 217987c16717fdbe64339c36ca17ca65): Boiler Room (góc làm việc của Brakk, cọc buộc
   Ram + xích đứt, xương, than cạnh lò, thùng), Eye's Cistern (xích Malric trói con Mắt từ 4 cột, xác "the Drowning" sát bể, dấu tay bò ra
   khỏi ichor, bàn khám + biểu đồ, bình vỡ, xương chuột cạnh bồn), Culture Hall (thùng thức ăn, bàn mổ + mô thịt, biểu đồ, thùng gỗ, vết máu,
   kiếm gãy + sổ tay). Đồ đứng có vật cản chân (obstacle prop). Giữ khoảng giữa phòng cho boss.
5. **Lối tắt một chiều** — MỚI `PassageMechanism` (type `passage`, Mechanics/Puzzles): `to`, `verb`, `requires`, `sets`, `locked_say`,
   `first_text`/`text`, `sprite`/`sprite_open`; dùng `SceneTransition.Warp` (tối dần -> dời người chơi + camera); đang giao chiến thì không
   dùng. `map2_boilShortcut.py` (md5 761e668038320c8169674c6eca7ed450): thang bảo trì tường bắc Boiler Room (boil_ladder, sets
   boil_hatch_open) <-> nắp hầm góc đông-nam Sluice Hall cạnh lửa trại rest_cistern (hub_hatch, requires boil_hatch_open, "Bolted from below.").
   Ảnh PixelLab: Resources/VFX/props/hatch_round (PPU 190), maint_ladder (PPU 140, tách phần thang khỏi khối tường), nguồn ở Catalog/v2/art.
6. **Đèn** — MỚI `LightCuller` (Gameplay/World, tự tạo khi có director): cứ 0,2 s tắt đèn điểm của map ("Light ...") ngoài khung camera
   (+ bán kính + 1 đv lề), bật lại khi vào khung. Đèn có LightAnimator: `SetCulled` (giữ trạng thái cờ, không chạy hoạt ảnh khi bị cắt);
   đèn thường: bộ cắt nhớ trạng thái, chỉ bật lại đèn do nó tắt (Morrow Map 3 chỉnh thẳng đèn trụ). Bảng F1 (DevCheats) có dòng
   FPS / ms / tệ nhất 2 s / "Map lights on: a/b". Renderer2D đã để Light Render Texture Scale 0,5 (hợp mobile).
   `LightAnimator.Lamp` (đổi từ tên Light để không trùng kiểu UnityEngine.Light).
- Sao lưu code: Catalog/backups/polish_2026-11-05/batch1/; map: Catalog/backups/map2_2026-11-05/<bước>/. Minimap đã bake lại.

## §144 Sửa theo ảnh chạy thử Containment Vault + ichor + gỡ trang trí (05/11 ~2h–3h) — CHƯA chạy thử
- **Mắt tường (ảnh "laser từ chấm đỏ không biết là gì")**: 4 prop thật `sz_walleye_1..4` (sprite_prop, `map2_wallEyes.py`) trên mặt tường bắc
  vault (x 59.3 / 67.2 / 72.8 / 80.7, hàng 87.6). Ảnh PixelLab: Resources/VFX/specimen/walleye_open (mắt mở), walleye_closed (hốc tối có khe đỏ —
  dựng từ ảnh mở để khớp hình), PPU 170, pivot giữa. Pha 2: xích đứt -> lần lượt mở to (loé đỏ). Mỗi phát: vòng đỏ bám rồi khoá dưới chân Ren
  + vạch nhắm mảnh mắt -> vòng (khoá thì nhấp nháy), mắt nạp sáng; tia đỏ (EtherVfx.Beam) lõi trắng, nổ vòng + vết cháy mờ 1,6 s.
  Không có prop -> chấm đỏ cũ. Boss chết -> mắt nhắm lại (litWards).
- **Requiem Wail bản 4 (ảnh "cột chưa có tác dụng, chưa báo trước, không biết trốn cột nào")**: hít vào 2,6 s (pha 2: 2,2 s);
  RUNE riêng của trụ sáng (Resources/VFX/specimen/ward_rune0..3, tách từ ảnh trụ; thuộc tính Tiled `icon` mới của sprite_prop) hiện to dưới chân
  boss + vệt 14 chấm vàng chạy từ boss tới vòng ấn; VÒNG ĐỎ bám dưới chân Ren, đứng trong vòng ấn (bán kính 0,42 đv, đo dẹt) -> chuyển vàng.
  Sóng: an toàn = trong vòng ấn (`Shockwave.shelter` mới — Func<Vector2,bool>), ngoài = trúng; lăn vẫn né; vòng đầu khoá mantra.
  `wailing`: mắt tường chờ, đang nhắm thì huỷ phát — không chiêu nào chen khi Ren phải đứng trong vòng. Map không có trụ -> không gào.
  Bỏ khe sóng/bóng che (Shadow). Vòng ấn lúc sáng 0,35 -> 1; đèn trụ tới 2,2.
- **Ichor (ảnh "dòng ichor không xuất hiện")**: nguyên nhân = tôi giảm độ sáng 0,72 rồi tint lớp (#6a9276 ở bể A) + ánh nền khu ~0,45 làm tối
  thêm 2 lần -> lớp nước gần như vô hình. `tools/ichor_calm_tileset.py`: bão hoà 0,62, GIỮ độ sáng 1,0, +20 alpha; vùng sâu 0,84; mặt nền 0,7.
  Mô phỏng có tint + ánh nền 0,5: sáng ngang bản gốc, bớt chói (out/preview/ichor_ingame_sim.png). Không đổi map.
- **Gỡ trang trí (ảnh "đưa linh tinh, không phân tích")**: `map2_undoDecor.py` (md5 0d4d881265ac23aebe77861f0b6de40a) gỡ id 1647–1707 (gợn
  sóng / bong bóng / sương rải ngẫu nhiên) + 1708–1756 (đồ + vật cản 3 phòng). Giữ màu ichor, trụ, mắt tường, lối tắt, trận phục kích.
  Bài học: đặt đồ theo CỤM kể chuyện bám kiến trúc, cùng mật độ pixel với tile (vẽ PixelLab ở 64x64 thay vì 128 thu nhỏ), cùng bảng màu
  tileset, chừa đường đi / đường lao boss, có ảnh mockup trên map thật để người dùng duyệt trước khi ghi .tmx.
- Mockup Boiler Room (chờ duyệt): thang bảo trì vẽ tay theo bảng màu tường (maint_ladder2, đủ cao mặt tường, lỗ hầm ở đỉnh) dời sang đoạn
  tường trống x 93.35; tấm neo + xích đứt + cùm gãy (PixelLab 64x64, tách khỏi phiến đá) chỗ Ram xuất hiện; thùng gỗ cạnh xác Brakk;
  than nhỏ ở đúng cửa lò 3 máy.
- Sao lưu code: Catalog/backups/polish_2026-11-05/batch2/.

## §145 Cập nhật file bàn giao theo §143–§144 + cốt truyện hiện hành (05/11 ~2h45)
- SOUL_REQUIEM_HANDOFF.md (md5 a851395438851e228a0a4b3ac5ee0a5c; bản cũ ở Catalog/backups/handoff_2026-11-05/): mục 6 viết lại đầy đủ (thế giới, tiên tri,
  mạch Map 1–4, kết game, câu hỏi mở: động cơ Ren sau Map 1, tên Church of Mercy); mục 7/8/9/10 thêm việc §143–§144 (phục kích, nhiệm vụ con,
  ichor, lối tắt, LightCuller, mắt tường, Requiem Wail bản 4, gỡ trang trí, mockup Boiler Room chờ duyệt) và bài học tint ichor / trang trí.
- Tài liệu cốt truyện gốc (artifact 25/10) + §48 là bản cũ; khi lệch theo HANDOFF mục 6.

## §146 Specimen Zero làm lại theo phân tích thiết kế boss (05/11 ~3h–3h30) — CHƯA chạy thử
Ảnh chạy thử: "mắt tường tấn công chưa có VFX; bỏ cơ chế đánh boss lúc nộ chiêu là ngắt chiêu (quá dễ)". Phân tích (người dùng duyệt 4 phương án Recommended):
(1) dồn 5% máu (~2 nhát) lúc Requiem Wail hít vào -> nghẹn + khựng 1,8 s = đòn đặc trưng thành cửa sổ sát thương lớn nhất, trụ phong ấn vô nghĩa;
(2) boss đứng yên, không đòn nào phạt người đứng sát -> "ôm boss + lăn" là tối ưu; (3) mắt tường: tia 0,25 s mảnh, đỏ trên viền đỏ màn hình
lúc thấp máu, vòng đỏ trùng nghĩa với vòng đỏ của Wail, 4 mắt tự bắn ngẫu nhiên (~1 phát/1,4 s chen mọi đòn) -> mất máu "không rõ từ đâu".
- **Bỏ ngắt chiêu** Wail. Thay bằng **Spent**: gào xong boss gục mặt (state Idle = khung ngủ), sáng nhạt 2,5 s, `Health.DamageTakenMul` = 1,5
  ("The wail leaves it spent - Specimen Zero slumps. Strike now!"). Trường Inspector `spentSeconds`, `spentDamageMul`.
- **Writhing Bed** (chống áp sát): `Update` đếm thời gian Ren trong elip `bedRadius` 1,2 quanh chân boss (y/0,6; rời ra giảm x2; không đếm lúc gào/Spent).
  ≥ `bedHugSeconds` 2,5 s và hết hồi (7 s x Pace) -> đĩa tím loang + viền co lại 0,8 s ("The roots around its base writhe - back away!")
  -> 3 vòng rễ (4/7/11 gốc, r 0,3/0,65/1 x 1,35) bung từ trong ra cách 0,09 s, 80% sát thương, hất 4. Ưu tiên ngay sau xích đứt, trước Wail.
- **Mắt tường -> đòn The Watchers** (pha 2, hồi 8 s, ưu tiên sau Wail): mắt không còn tự bắn. Xích đứt -> 4 mắt mở lần lượt trái -> phải.
  Đòn: bắt đầu từ mắt XA Ren nhất, mỗi 0,55 s một mắt: dấu ngắm hình mắt (PixelLab) bám chân Ren 0,9/0,6 s rồi khoá 0,35 s (nhấp nháy, vạch nhắm dày),
  loé nạp ở mắt, tia: viền tối + đoạn tia PixelLab lặp Tiled + lõi trắng, 0,4 s; trúng nếu Ren trong 0,34 đv chỗ khoá (55% sát thương);
  nổ PixelLab 6 khung + vết cháy mờ 1,6 s. Xong loạt nghỉ 0,7 s. Thiếu ảnh -> vẽ bằng code như cũ.
- Ảnh PixelLab (Create image Pro 64x64 -> lưới 4x4, tải về Downloads rồi cắt): Resources/VFX/specimen/eyefx_reticle, eyefx_scorch,
  eyefx_charge_0..5, eyefx_burst_0..5 (khung cuối mờ dần), eyefx_beam_0..3 (56x32, pivot (0,0.5), Full Rect cho Tiled). PPU 100.
  Nguồn: Catalog/v2/art/pixellab_eyefx_{reticle,scorch,charge,burst,beam}_source.png.
- Code: chỉ `Enemies/Bosses/SpecimenZeroSkills.cs` (md5 111c235847bcee296ea4dabcc19ec092). Sao lưu: Catalog/backups/polish_2026-11-05/batch3_szRework/.

## §147 Specimen Zero: bộ chọn chiêu mới — hết spam Requiem Wail (05/11 ~3h40) — CHƯA chạy thử
- Ảnh chạy thử: "boss spam shockwave liên tục, mắt tường không ra chiêu". Nguyên nhân: Act chọn theo THỨ TỰ ƯU TIÊN cố định và hồi chiêu đặt lúc
  BẮT ĐẦU đòn. Wail dài ~11 s (hít 2,2 + 4 vòng + đổi trụ + Spent 2,5) > hồi chiêu pha 2 (14 x 0,72 ≈ 10 s) -> vừa xong đã sẵn, lại đứng đầu
  -> gào liên tục; The Watchers / Gaze / Hand không tới lượt.
- Sửa (`PickAttack` / `AfterAttack`): hồi chiêu tính từ lúc đòn KẾT THÚC (Wail 16 s / pha 2 13 s; Watchers 7; Gaze 9; Hand 8; Lash 6; Roots 3,2 — trừ
  Wail/Watchers đều x Pace); bốc ngẫu nhiên có trọng số trong các đòn đã sẵn (Watchers 3, Gaze 2, Hand 2, Lash 1,5, Roots 1), KHÔNG lặp đòn vừa dùng
  (trừ Roots làm đòn lót); Wail chỉ ra khi đã có ≥ 4 (pha 2: ≥ 3) đòn khác xen giữa, đủ điều kiện thì luôn được chọn. Writhing Bed vẫn ưu tiên khi Ren ôm boss.
  Nghỉ giữa 2 đòn 0,65 s (pha 2: 0,45). Wail pha 2: 3 vòng (trước 4), đổi trụ sau vòng 1.
- md5 SpecimenZeroSkills.cs: fdd65333b1fb5cff1de8a591f4621192 (biên dịch sạch 05/11 03:37). Sao lưu: batch3_szRework/SpecimenZeroSkills_before_scheduler.cs.

## §148 Kế hoạch thiết kế Map 4 – The Catacombs of Naming (05/11 ~4h) — CHỜ NGƯỜI DÙNG CHỐT
Tài liệu: https://claude.ai/artifact/6QfPwrPpEcgZxMes4SwX1u ("Soul Requiem – Map 4: The Catacombs of Naming (kế hoạch thiết kế)").
- Vai trò: điểm xoay giữa game — Ren thắng trận nhưng mất Con Mắt; rời map với mục tiêu riêng đánh the Remade God ở Map 5 (The Forge, gói Volcano).
- Đề xuất twist (chưa chốt): Tên của Ren bị Choir cắt bỏ khi còn bé ("Candidate VII", nhà Corwen tạm); lời tiên tri do Absalom dựng nên.
- 3 tầng: Gaol of Confession (Old Prison, Mantra bị phong ấn, mini-boss Warden Hesk – nghe tiếng động) -> Ossuary of Candidates (Crypt, hub
  Stair of Bones + 3 nhánh tuỳ thứ tự: Archive of Names / Candidates' Wards / Drowned Ossuary, boss tuỳ chọn Ossuary Maw) -> Hall of Naming
  (boss Candidate VI = bản sao Ren, cảnh The Severing, Malric hy sinh). Câu đố P1–P6, bẫy (khúc gỗ lăn, gai, pháo lửa, tên, nắp quan tài, Ether dâng).
- Hệ thống mới dự kiến: NoiseSystem, phong ấn Mantra theo cờ, Mechanism `trap`, MirrorSkills, lựa chọn có hậu quả (cờ name_reclaimed,
  parents_heard, candidate_vi_freed mang qua map). Ước lượng 14–18 phiên, 7 giai đoạn.

## §149 Minimap: sương mù theo khu + "??" (05/11 ~8h30–9h) — CHƯA chạy thử
Người dùng chọn (4 phương án Recommended trừ kiểu sương): hành lang gộp vào khu gần nhất · mở = bước vào + bản đồ nhặt được ·
đích nhiệm vụ vẫn hiện trên sương · vân sương PixelLab.
- MỚI `Gameplay/World/MapFog.cs` (class thường, MapEncounterDirector tạo cuối Start sau khi nạp cờ, `dir.Fog`, `Fog.Tick` trong Update):
  vùng = khu lớp `zones` GỘP theo tên hiển thị (spec_breakout/r4_swarm chung vùng với phòng); ô ngoài khu thuộc vùng không bí mật gần nhất;
  2 khu khác tên chồng nhau -> ô sáng nếu một trong hai đã mở. Thuộc tính Tiled `fog` trên khu: trống / `secret` (sương không "??", không nhận
  hành lang, chỉ mở khi bước hẳn vào khung khu) / `off` (luôn rõ). Mở = cờ `map_seen_<id khu đầu>` (vào bản lưu); bản lưu cũ: `<id>_started|_cleared`
  tính là đã mở. Bản đồ nhặt được: note/đồ có `sets = map_seen_r6,map_seen_spec`. Kích thước lấy từ SuperMap.m_Width/m_Height.
- MỚI `UI/MinimapFogView.cs`: một ảnh sương dùng chung (ô x ppt px), vân `Resources/UI/Requiem/minimap_fog.png` (PixelLab 64x64 lưới 4x4 ->
  ghép 4x4 ô mây có viền mềm + làm liền mạch bằng cuộn nửa ảnh, 200x200, **Read/Write bật** trong meta; thiếu -> vân Perlin bằng code), mép mềm
  nội suy giữa ô, vẽ lại khi Fog.Version đổi; ảnh cũ mờ dần 0,6 s trên ảnh mới; "??" ở tâm khu lớn nhất của vùng chưa mở (không cho khu bí mật);
  bản đồ lớn hiện thêm tên khu đã mở. RequiemHUD: minimap + bản đồ lớn (bigMapImage overflow hidden); lửa trại trong sương bị ẩn; đích nhiệm vụ giữ.
  USS: .sr-fog-q, .sr-fog-q-big, .sr-fog-name. Nguồn ảnh: Catalog/v2/art/pixellab_minimap_fog_source.png.
- Khu bí mật: script `Catalog/v2/maps_fogSecret.py` (fog=secret): Map 3 x1/x2/x3 -> md5 6213eaa4d1f3d76834d0ac60102dffd5 (đã ghi).
  Map 2 s2/s3/vault -> đã ghi 51af5276… NHƯNG người dùng lưu .tmx trong Tiled lúc 08:45 (đang sửa tay) nên bản ghi bị đè: md5 hiện 3991b6fb…,
  chưa có fog=secret -> chạy lại script khi người dùng xong Tiled (đổi EXPECT) hoặc người dùng tự thêm thuộc tính trong Tiled.
- Biên dịch sạch 05/11 08:45. Sao lưu code: Catalog/backups/polish_2026-11-05/batch4_minimapFog/; map: backups/map{2,3}_2026-11-05/fogSecret/.

## §150 Hạ boss = hồi đầy máu + bình; chết thì mọi khu dở dang làm lại (05/11 ~10h50) — CHƯA chạy thử, CHƯA biên dịch
Phân tích trước khi sửa (luật cũ): gục -> hồi sinh ở lửa trại gần nhất, đầy máu + bình; khu đã dọn giữ nguyên; khu đang đánh làm lại từ đợt 1
(boss xuất hiện lại đầy máu); ngoại lệ: khu có hang sinh quái, và KHU CHỨA LỬA TRẠI hồi sinh (lỗi: Map 3 rest_bridge nằm trong Broken Bridge ->
Lantern Knight giữ máu dở cạnh chỗ hồi sinh; Map 2 rest_office trong Overseer's Office, Map 1 z4_fire trong Ruins Path, Map 3 rest_ossuary
trong Ossuary Tower -> hồi sinh giữa quái chưa dọn). Người dùng chọn (Recommended):
- `ClearZone`: khu có spawn `boss = true` dọn xong -> `RestoreAfterBoss`: Heal(Max) + RefillPotions (tới maxPotions), 2,6 s sau
  "Victory steadies you - health and flasks restored.". Khu thường không hồi. Nạp bản lưu không kích hoạt.
- `ResetLockedZones`: bỏ ngoại lệ khu chứa điểm hồi sinh; khu đó bật `holdNearRespawn` -> `ReadyToStart` chờ Ren rời lửa trại > 2 đv
  (`RespawnHoldDistance`) mới đánh lại (quái không ập ra lúc vừa tỉnh). Giữ ngoại lệ khu có hang sinh quái.
- Sao lưu: Catalog/backups/polish_2026-11-05/batch5_bossHeal/.

## §151 Minimap sương bản 2: cụm mây tự nhiên thay cho cắt thẳng (05/11 ~11h) — CHƯA chạy thử, CHƯA biên dịch
- Người dùng (ảnh mẫu): sương cắt thẳng/chéo nhìn xấu, muốn từng vùng là một cụm mây viền tròn tự nhiên có "??".
- `UI/MinimapFogView.cs` viết lại phần vẽ (vẫn 1 ảnh, 4 px mỗi ô, lọc Bilinear, vẽ lại khi Fog.Version đổi):
  lớp lót = ô chưa mở co vào 1 ô, màu nửa tối của búi mây; rải "búi mây" PixelLab `Resources/UI/Requiem/fogpuff_0..13` (64x64, Read/Write bật)
  theo vùng: viền giáp vùng đã mở = lưới 2,4 ô, bán kính 2,4–3,4 ô (tràn ra ngoài -> viền tròn); ruột = lưới so le 3,6 ô, bán kính 3–5,2;
  ranh giữa 2 vùng chưa mở: bán kính bị chặn bởi dSeam + nhiễu 0,5–2,3 ô -> khe tối lượn sóng, nhìn ra từng cụm; mỗi vùng tông sáng ±9%;
  dán theo y (dưới đè trên) + bóng đổ lệch xuống. Ngẫu nhiên theo id vùng -> hình cụm cố định. Mở vùng: ảnh cũ mờ 0,8 s + 4–16 búi mây
  bay toả từ tâm vùng rồi tan (1 s). `MapFog`: thêm `RegionAt(x,y)`, `Region.Areas`.
- Ảnh nguồn: Catalog/v2/art/pixellab_fogpuff_source.png (bỏ ô hình tim + lưỡi liềm). Ảnh mô phỏng trên minimap Map 1 trước khi viết code.
  `minimap_fog.png` (vân sương bản 1) không còn dùng — giữ lại, chưa xoá.
- Sao lưu bản 1: Catalog/backups/polish_2026-11-05/batch4_minimapFog/{MapFog_v1.cs, MinimapFogView_v1.cs}.
- (05/11 11h30, bản 3 — người dùng: "mây dày đặc quá, muốn tự nhiên như ảnh mẫu"): mỗi vùng = một cụm vài đám mây LỚN: R = căn(số ô)/3,2
  (3,2–7,5 ô); mây lớn đặt cách ~1,05R, chỉ ở ô cách mép ≥ 0,45R và cách vùng khác ≥ 0,55R; lấp trống bằng mây r = 2,4–0,6R (không lấp
  sát ranh vùng khác, < 2 ô) -> khe giữa các cụm; bỏ lớp lót đặc, thay bằng màn tối mỏng (35% màu mây, alpha 150–175) trên mọi ô chưa mở.
  Bóng đổ nhẹ hơn. Mô phỏng: Catalog/v2/out/preview không lưu (ảnh gửi trong chat). Sao lưu bản 2: batch4_minimapFog/MinimapFogView_v2.cs.
- (05/11 13h) Dấu "??" trên mây: màu gần đen rgb(10,6,16), đậm, viền sáng 1 px (USS .sr-fog-q 24px, bản đồ lớn 34px) — người dùng: khó nhận biết.
- (05/11 13h10) "??" vẫn khó thấy (khung game nhỏ -> chữ ~13 px, chữ trơn chìm vào mây nhiều tông): đổi sang NHÃN NỀN — bảng bo tròn màu giấy da rgb(236,222,188), viền nâu đậm, chữ "??" đen đậm (minimap 46x28, bản đồ lớn 68x40). Tương phản sáng-trên-tối ổn định bất kể nền mây.

## §152 (06/11) — Ếch pha 2, rào lối bí mật, Ram: chuồng nhốt + van xả nước (bỏ cột)

**Yêu cầu:** (1) rào chặn lối bí mật s2/s3 đang dùng tile cây cầu → làm rào thật; (2) phòng mini-boss Ram sơ sài, máy hơi không còn
liên quan cốt truyện → thiết kế lại theo ngoại hình + chiêu của Ram; (3) ếch con pha 2 đứng im chờ người chơi, nhịp đánh ếch chưa chuyên nghiệp.
Người dùng sau đó: đồ PixelLab lệch style / sai góc top-down; bỏ 4 cột (map 1–3 đã có quá nhiều boss dùng cột); chuồng ván gỗ không giữ nổi Ram;
cổng chuồng sai hướng.

**Ếch (ToadSkills / EnemyAI / Director)** — đã compile sạch 06/11 08h35:
- `EnemyAI.Provoke(summoned)`: quái triệu hồi (ếch con, SpawnSummon) bật aggro ngay, bỏ leash, không bị CheckTrapped kéo về.
- Toad: chọn đòn có trọng số, không lặp liên tiếp; Tongue trúng → Belly Flop (cảnh báo 0,55 s); Leap Slam hụt → thở dốc 1,2 s (cửa sổ phản đòn);
  pha 2 khi hết ếch con → nổi điên (Hop nhanh, hồi Slam ×0,55, thở dốc 0,8 s). Ếch con chui lên xong gọi Provoke.

**Rào lối bí mật s2 / s3** (dùng chung mọi map):
- Asset: rào gỗ pack **EPIC Old Prison** (cùng hoạ sĩ với tileset Sewers) — 4 trạng thái hư dần: `VFX/props/fence_h_0..3` (ngang), `fence_v_0..3` (dọc);
  ván vụn pack Sewers `plank_0..5`. PNG ghi trước, .meta sau (PPU 100, chân ảnh giữa mép dưới).
- `BreakableMechanism`: thêm `sprite_damaged` (ảnh hư dần theo từng nhát, nhát cuối mới vỡ) + `debris` / `debris_count` / `debris_scale`
  (ván vụn rải trên sàn, vị trí cố định theo tên). s2: fence_v, 2 bản, hits 4; s3: fence_h, 2 bản rộng 0,76, hits 4.
- Thùng PixelLab `barricade_crates_*` / `barricade_debris_*` (lệch style) không còn dùng — giữ file, chưa xoá.

**Ram — "Chuồng của Ram" + Van xả nước** (thay 4 cột `ram_pillar`):
- Lý do (người dùng): cột đã dùng ở boss map 1, 2, 3. Cơ chế mới phải khác loại: công cụ người chơi CHỦ ĐỘNG + khắc chế đúng nguyên tố (Ether độc).
- `SluiceMechanism` (type `sluice`, mới, Puzzles/): chém hoặc nhấn T vào van → nước lạnh chảy dọc làn `lane_a`–`lane_b` (ô Tiled) `flow_seconds` 3 s,
  nạp lại `cooldown` 10 s. Nước tràn từ tường xuống trong 0,35 s, rút 0,4 s. Đèn xanh nhấp nháy khi sẵn sàng, tắt khi đang nạp.
  API tĩnh: `Wet(p)`, `CrossesFlow(a,b)`, `AnyFlowing`. Hình: `sluice_stream_0..7` / `sluice_splash_0..7` / `sluice_ripple_0..7` (cắt từ pack
  Sewers, nhuộm xanh nước sạch); mặt nước vẽ bằng code (4 khung, lặp dọc, bọt hai mép). Nước sạch: không làm Ren mất máu.
- `RamSkills` viết lại:
  - Lao vào / bước vào làn nước → trượt + Ether trên da tắt (hơi nước bốc) → choáng `quenchStun` 2,5 s, nhận ×1,5 sát thương.
    Đâm tường chỉ choáng `wallStunTime` 1,2 s (van là công cụ chính).
  - Nước rửa trôi vệt độc. Ram đi bộ qua vệt độc của chính nó → hút lại; đủ 8 đốm → **quá tải** 6 s (lao ×1,25 nhanh, gồng ×0,7, quầng xanh dưới chân).
  - Pha 2 (Stampede): không gồng khi nước đang chắn giữa nó và Ren, thấy nước bật lúc đang gồng thì huỷ, không tự bước vào nước
    → Ren phải mở van đúng lúc nó đã lao đi. Bị dập nước giữa chuỗi Stampede → hết chuỗi.
  - Chỉ dẫn: tên boss → sau 2,7 s "Strike a sluice valve on the north wall as it charges..."; lần đầu đâm tường / quá tải / dập nước / pha 2
    đều có câu riêng; 25 s chưa dập được Ram → nhắc lại cách thắng (tối đa 25 s/lần).
- `RamPillarMechanism.cs` (+ .meta) chuyển vào `_to_delete/2026-11-06/` (dòng `ram_pillar` trong Registry + `RamPillarImpact` đã bỏ).
- `SpritePropMechanism`: thêm `flat = true` (xương, vòng neo nằm bẹt → luôn dưới nhân vật).

**Phòng (Boiler Room)** — script `Catalog/v2/map2_ramPen_barricades.py` (8c80af29… → f6a90489…) + `map2_ramPen_fix1.py` (→ **10c82a37…**),
sao lưu `Catalog/backups/polish_2026-11-06/batch2_ramPen/`, bake lại minimap:
- Bỏ 3 máy hơi + khối chặn + đèn lửa + hơi nước. Thêm 2 van `sluice_w` (96.5) / `sluice_e` (112.5) ngay bánh van có sẵn trên ống tường bắc.
- Chuồng trái của Ram: tấm rào **cột đá + song sắt** (PixelLab, ảnh map làm style mẫu, thu 1/2 cho khớp cỡ gạch), cổng giữa bị húc toang (vẽ lại từ
  chính tấm rào làm ảnh tham chiếu — cùng góc nhìn chính diện), vách đá bên, máng ăn đá đựng ichor xanh, vòng neo xích đứt, xương.
  Các tấm ghép chồng 1 cột (12 px) để không lộ 2 cột sát nhau.
- Chuồng phải = kho thức ăn (cửa còn khoá xích): thùng phi, thùng gỗ. Xương người trông chuồng bị xích trên tường bắc (Old Prison).
- Hành lang vào (bắc-đông): thùng + **Brakk's Tally Sheet** (note, marker) — dạy cơ chế bằng lời Brakk.
- Boss intro: `entrance_from` = cổng chuồng (92,96.3), bỏ `entrance_shake` (máy hơi). `hint_text` khu: "Lure its charge across a channel, then strike the valve".
- Ảnh: `Catalog/v2/art/pixellab_ram_*.png`; mockup `ram_room_mock_v3.png` (đã gửi trong chat).

**Chưa compile:** BreakableMechanism (sprite_damaged/debris), SluiceMechanism, RamSkills mới, MapMechanics/Registry, SpritePropMechanism —
Editor chưa mở lại từ 08h48. **Còn lại:** Map 2 load chậm (chờ `[LoadProfile]`, xoá `loadprof.on` sau); Map 4 quyết định trong doc.

## §153 Gỡ lối tắt thang Boiler Room <-> nắp hầm Sluice Hall (06/11 ~11h15) — chờ test
- Người dùng (ảnh chạy thử): leo thang ở tường bắc Boiler Room lại trồi ra nắp hầm giữa sàn Sluice Hall — vô lý với game TOP-DOWN:
  hai phòng cùng một mặt sàn, thang chỉ hợp khi thật sự đổi tầng (lên mặt đất, xuống tầng hầm khác). Lỗi thiết kế của §143 (việc 5).
- `Catalog/v2/map2_removeLadder.py`: gỡ id 1757 `boil_ladder` + 1758 `hub_hatch` (type passage); cờ `boil_hatch_open` không còn ai dùng.
  md5 Map 2 10c82a37… -> **57b077c5d697a3c98410d202c49aaf2e**; sao lưu `Catalog/backups/map2_2026-11-06/removeLadder/`. Minimap đã nướng lại.
- GIỮ: code `PassageMechanism` (dùng chung, chỉ dùng cho thang/cửa đổi tầng thật), ảnh `Resources/VFX/props/maint_ladder`, `hatch_round` (không xoá).
- Bài học: top-down = mọi phòng cùng mặt phẳng trừ khi map nói rõ có tầng khác. Lối tắt trong cùng tầng phải là cửa / cổng / rào
  mở từ một phía, không phải thang hay nắp hầm.

## §154 Hình cơ quan = tile object nhìn thấy trong Tiled — MẶC ĐỊNH TỪ NAY (06/11 ~15h) — chờ biên dịch + test
- Người dùng: trong Tiled chỉ thấy khung rỗng của cơ quan, không thấy props (rào, thùng, xương phòng Ram, trụ phong ấn, mắt tường...).
  Nguyên nhân: `sprite_prop` / `note` / `breakable` đặt bằng hình chữ nhật + thuộc tính `sprite` (chữ) -> Tiled không vẽ được ảnh.
  Người dùng chọn phương án A và yêu cầu ghi làm mặc định (HANDOFF quy tắc 14, ARCHITECTURE 2.2).
- **Code** (Gameplay):
  - MỚI `World/Mechanics/MechanismArt.cs` (không phải MonoBehaviour): đọc tile object (khung = bounds renderer ST2U, lật ngang), tắt ảnh ST2U,
    `Resolve("sprite")` (Resources, không có -> ảnh tile), `PivotFor` / `ScaleFor` / `Apply` (vẽ khít khung như trong Tiled).
  - `Mechanism.cs`: `Art` (danh sách hình) + `AddArt`.
  - `MapMechanics.ReadMap`: đọc thêm lớp `mechanics_art` (`ArtLayer`); tile object không collider -> Rect = khung; object có `art_for`
    -> gắn vào cơ quan cùng tên sau khi đọc hết (không thấy -> cảnh báo). `MapEncounterDirector.EnsureMechanics` nhận cả lớp mới.
  - `SpritePropMechanism`: có hình -> ảnh / chân / tỉ lệ / lật theo khung; vòng ấn giữ đúng `decal_size` khi vật bị kéo to.
  - `NoteMechanism`, `PassageMechanism`: tờ giấy / hình lối đi theo khung. `BreakableMechanism`: `BuildFromArt` (mỗi mảnh `art_for` một hình;
    hư hại dần / ván vụn như cũ). Cách cũ (rect + `sprite`) vẫn chạy cho map chưa chuyển.
- **Tileset** `EPIC RPG World - Sewers V1.5/TiledMap Editor/tilesets/SR-mech-art.tsx` (32 tile, firstgid 14607, .meta tự ghi PPU 100):
  thùng/hòm/rào chuồng/máng/vòng neo/xương/xác xích/trụ phong ấn 0–3/mắt tường/ghi chú/thang/nắp hầm/mảnh rào. Mỗi tile ghi sẵn class +
  `sprite` + mặc định (`collider`, `flat`, `sprite_lit`). Không đưa vào: khung hoạt ảnh, `_lit`, ảnh hư hại 1–3, ván vụn (code tự đổi).
  Ảnh PPU 140/170/190 (trụ, mắt, thang, nắp hầm): bản xem trước thu nhỏ đúng tỉ lệ ở `.../Sewers V1.5/Props/SR-mech-art/` (chỉ cho Tiled).
- **Map 2** (`Catalog/v2/map2_mechArt.py` + `map2_mechArt_fix1.py` khung số lẻ cho ảnh PPU ≠ 100): 30 `sprite_prop` + 2 `note` có tờ giấy
  chuyển sang lớp mới `mechanics_art` (id 37, trước `zones`), giữ id / tên / type / mọi thuộc tính khác, bỏ `sprite` `sprite_lit` `unity:IsTrigger`
  (tile lo). `s2_barricade` / `s3_door`: vùng chém giữ nguyên, bỏ `sprite` `sprite_count` `sprite_width`, thêm 4 mảnh `s2_barricade_art1/2`,
  `s3_door_art1/2` (id 1788–1791). Kiểm tự động: 32/32 chân vật mới trùng chân cũ (sai số < 0,02 px), thuộc tính khác không đổi.
  md5 57b077c5… -> a57ba3a9… -> **54a96593eae4f631cd4ca358cc336c1c**; sao lưu `Catalog/backups/map2_2026-11-06/mechArt/`, `.../mechArt_fix1/`.
  Minimap đã nướng lại. `compose_preview.py` vẽ thêm lớp `mechanics_art`. Map 1 / Map 3 không có cơ quan dùng ảnh rời -> không đổi.
- Lưu ý khi chỉnh tay: kéo vật mới từ tileset vào lớp `mechanics_art` là thành cơ quan ngay (đặt tên nếu code cần tìm, vd `group`);
  mảnh rào phải có `art_for`. Đừng đặt tile SR-mech-art vào lớp `props` (lớp đó chỉ là hình tĩnh, mất collider / sáng).

## §155 Sửa sau chạy thử: nước van lệch, Ram làm lại, Plague Eye (06/11 ~15h) — chờ biên dịch + test
- **Lỗi biên dịch §154**: `MechanismArt.cs` thiếu `using SoulRequiem.Core` (extension `GetString`) -> 3 lỗi CS1061, Unity chạy code cũ. Đã thêm.
- **Người dùng sửa tay Map 2 trong Tiled** (14h47): xoá 2 tượng lửa phòng Ram (props 558/559 + collider 859/860) và `pen_skel_e` (1783);
  Tiled sắp lại thứ tự thuộc tính zone s2/s3/vault (fog=secret vẫn còn). md5 4dedfbe3… — giữ nguyên.
- **Nước van lệch lên trên** (ảnh 1): ảnh `sluice_stream` cao 2 ô, chân đặt ở mép làn -> trồi quá ống ~1 ô.
  `SluiceMechanism`: thuộc tính mới `outlet` "x,y" (miệng ống); ảnh dòng nước cắt giữ phần trên vừa khoảng miệng ống -> mép làn (`CropTop`).
  Map 2: `Catalog/v2/map2_sluiceOutlet.py` — `sluice_w` outlet 96.5,84.95, `sluice_e` 112.5,84.95. md5 -> **6fe28eb8a465d0a91c37d67a3a0579cc**,
  sao lưu `Catalog/backups/map2_2026-11-06/sluiceOutlet/`. Minimap nướng lại.
- **Không có hướng dẫn dùng van** (ảnh 2): `SluiceMechanism.Alert` (static) — Ram bật khi vào trận, tắt khi chết: van sẵn sàng có mũi tên xanh
  nhấp nhô trên miệng ống + quầng sáng mạnh, nhấp nháy nhanh. Câu chỉ dẫn mới "Its Ether hide turns steel. Strike a glowing valve...".
- **Ram viết lại** (`RamSkills.cs`, bản cũ sao lưu `Catalog/backups/code_2026-11-06/ram_plague/`), theo thiết kế người dùng:
  - Da Ether: mặc định nhận 50% sát thương (`armorDamageMul`). Dính nước -> choáng 2,5 s (pha 2: 2 s, nhận ×1,5) rồi **Ướt sũng 10 s**:
    mất giảm sát thương + chậm 20%, giọt nước rơi khỏi thân. Chém lúc chưa ướt lần đầu -> gợi ý "Your blade barely bites...".
  - Pha 1: đi 2,3 đv/s (Ren 2,0; EnemyDefinition + Map2EnemyBuilder), áp sát thì **húc** (gồng 0,4 s, lao 0,7 đv, 18 sát thương),
    thỉnh thoảng (5–8 s, khi Ren cách ≥ 1,2) gồng **lao** có nhắm: đâm tường choáng 1,2 s + đá rơi.
  - Pha 2 **Rampage**: lao liên tục 4,2 đv/s, đâm tường -> đá rơi (2 tảng, cách một lần có 1 tảng nhắm chỗ Ren), bật ra theo phản xạ ±35°,
    35% bật về phía Ren ±20°; kẹt góc 3 lần -> lao về giữa phòng. Tường không làm choáng — chỉ nước dừng được. Mỗi 10 s thở dốc 1,4 s.
    Bỏ "pha 2 né nước" cũ (Ram giờ lao mù -> Ren canh làn nước rồi mở van).
  - **Lỗi kẹt tường**: khi đi Ram dừng sát Ren rồi húc (không ủi vật lý); lúc húc / lao / Rampage `Physics2D.IgnoreCollision` với Ren
    (gây sát thương + đẩy rồi lao xuyên qua), trả lại khi xong / chết / tắt.
  - Giữ: vệt độc khi lao (nước rửa trôi), hút vệt -> quá tải, hơi nước khi bị dập.
- **Plague Eye** (`PlagueEyeSkills.cs` + prefab `Plague_Eye`): 
  - Laser không mất máu: kiểm trúng 0,25 s/lần mà tia quét 80°/s lướt qua người ~0,13 s -> lọt. Nay kiểm theo vùng quét mỗi khung
    (`RayTouchesPlayer`, xét chân + thân), mỗi tia trúng lại sau `rayTick`. `rayDamage` 9 -> 12.
  - Xoay spam: ưu tiên mới triệu hồi > Ray > Spin > Volley; Spin cần Ren đứng sát liên tục `spinCommit` 0,8 s, không 2 lần liền, hồi 5 -> 10 s.

## §156 Chuột cận chiến / phục kích Cistern / Ram bật tường + VFX pixel (06/11 ~16h) — chờ biên dịch + Dựng quái Map 2 + test
- §154–§155 đã biên dịch sạch 15:12 (Map 2 nạp 183 cơ quan, không cảnh báo hình).
- **Chuột** (người dùng: cận chiến không có độc, phân biệt rõ với chuột ném độc):
  - `Catalog/v2/tools/rat_clean_sheets.py` -> bộ hình `Mutant Rat/mutant rat - clean {idle,walk,attack2,hurt,death}.png` (+ .meta GUID mới):
    khối xanh độc liền >= 20 điểm ảnh (cầu độc trên tay) -> màu vuốt tối của thân; đốm nhỏ (mụn mủ) -> dải ô-liu xám. Bản gốc giữ nguyên.
  - `Map2EnemyBuilder`: `RatCleanAnims()` cho Plague_Rat + Vat_Born; Vat_Born nhợt hồng (tint 1/0.8/0.76), scale 0.85; Bloated_Rat bỏ tint đỏ
    (giữ xanh độc sáng = con ném độc). `poisonPercent` Plague_Rat / Vat_Born = 0 (builder + EnemyDefinition asset).
- **Phục kích sau eye_glimpse** (`r4_swarm`): 7 điểm sinh dời từ hành lang Sluice Hall vào trong Cistern (x 92–98, y 50–58.5, sàn trống đã kiểm),
  chữ "Rats pour out of the cistern...". Thuộc tính mới chung cho điểm sinh **`provoke = true`** (MapSchema.Prop.Provoke, Director.Spawn gọi
  EnemyAI.Provoke) — quái lao vào Ren ngay dù sinh ngoài tầm phát hiện. Script `map2_swarmInCistern.py` (6fe28eb8 -> 76505eb3) +
  `map2_swarmProvoke.py` (-> **4d61ddb4eb99c51939c9fe20f2db62d6**), sao lưu `Catalog/backups/map2_2026-11-06/swarm*/`. Minimap nướng lại.
- **Ram**:
  - Tốc độ đi trả về 0,7 (người dùng: nhanh hơn Ren rất khó chịu) — asset + builder.
  - Bật tường pha 2 viết lại: dò trước bằng CircleCast cỡ thân (`FreeDistance`, chỉ tính mặt chắn ngược hướng đi), bật đúng lúc chạm;
    `ChooseBounce`: thử hướng về Ren (35%) rồi phản xạ lệch ngẫu nhiên ±0/20/40/60°, chỉ nhận hướng còn >= `rampageMinRun` 1,2 đv đường trống
    và không cắm vào mặt vừa đâm; góc chết -> quét 16 hướng lấy hướng thoáng nhất. Dò kẹt (Stalled) giữ làm dự phòng. Bỏ "về giữa phòng".
  - VFX mới `Enemies/Bosses/RamVfx.cs` (static, ảnh vẽ bằng code kiểu pixel art cùng cỡ điểm ảnh Ram ×2 = PPU 50, viền tối, 3 sắc xanh):
    vũng Ether độc (3 dạng, bọt sủi 2 khung, mờ theo 3 bậc), vòng chấm quay + hạt phóng xạ vuông khi quá tải, bụi gót khi lao,
    hơi nước pixel khi bị dập, giọt nước pixel khi ướt. Bỏ hết đốm tròn nhoè `EtherVfx.Orb`. `trailEvery` 0,09 -> 0,12.
- Người dùng cần chạy **Soul Requiem ▸ Dựng quái Map 2** (ngoài Play Mode) để chuột nhận bộ hình sạch / màu mới, rồi **Dựng scene Map 2**.

## §157 Ram: sửa pha 2 đứng bật tại chỗ + trả VFX cũ (06/11 ~16h10) — chờ biên dịch + test
- Người dùng (ảnh): pha 2 Ram không lao, đá rơi khắp phòng; muốn VFX đẹp như cũ.
- Nguyên nhân: `FreeDistance` (§156) tính cả lần CircleCast "chạm ở khoảng cách 0" (khối dò bắt đầu chồng lên vật rắn: sát tường / cổng)
  với pháp tuyến = -hướng đi -> mỗi khung đều "đâm tường" -> bật tại chỗ + gọi đá rơi liên tục.
  Sửa: bỏ qua hit `distance <= 0.001` (vật đang chạm thật đã có `Stalled()` dự phòng); hai lần đâm cách nhau < 0,4 s thì không gọi thêm đá.
- VFX: trả lại hình cũ (đốm Ether mềm `EtherVfx.Orb` cho vệt độc, quầng quá tải, hơi nước, giọt nước; bỏ bụi gót; `trailEvery` 0,09).
  `RamVfx.cs` (+ .meta) chuyển vào `_to_delete/2026-11-06/` (không còn dùng).

## §158 Ram pha 2 vẫn quay tại chỗ (06/11 ~16h15) — chờ biên dịch + test
- Ảnh người dùng: Ram quay tại chỗ giữa sàn, sát Ren (bản §157 đã biên dịch 16:05). Prefab Ram chỉ có 1 CapsuleCollider2D; đá vụn / vũng độc
  không có collider -> nghi khối dò coi collider của Ren là tường (lọc bằng `Health.IsPlayer` có thể lọt).
- `RamSkills.Solid`: chỉ vật TĨNH là tường — bỏ mọi collider có Rigidbody động (Ren, quái, vật đẩy) và mọi collider thuộc cây object của Ren.
- Chẩn đoán (chỉ Editor): bật dồn dập (< 0,4 s) -> `Debug.Log("[Ram] bật dồn dập: <tên vật> (lớp ...) tại ...")`. Còn lỗi thì đọc Editor.log.

## §159 Ram quay tại chỗ — nguyên nhân thật: chồng lên tấm chắn đấu trường (06/11 ~16h25) — chờ biên dịch + test
- Log chẩn đoán §158: "[Ram] bật dồn dập: Arena Seal boil". ArenaSeal xích chân boss cách mép khu 0,3 đv, thân Ram rộng 0,84 đv -> thân đè lên
  tấm chắn vô hình. Bản §157 bỏ qua mọi hit "khoảng cách 0" -> hướng đâm vào tấm chắn bị coi là trống -> Ram chọn đúng hướng đó, bị chặn, lặp lại.
- `FreeDistance`: hit chồng lấn -> pháp tuyến thật = từ `ClosestPoint` trên vật về tâm khối dò (tâm lọt hẳn vào vật -> hướng về giữa khu);
  đi vào vật = chặn ngay, đi ra = bỏ qua. Khung xích đấu trường (zone rect − 0,34 đv, lấy bằng `TryGetZoneAt` lúc vào trận) cũng tính là tường.
- Log chẩn đoán giữ lại (chỉ Editor), thêm trường hợp "mép khung đấu trường".

## §160 Ram pha 2 quay tại chỗ — nguyên nhân thật: so sánh số thực (06/11 ~20h30) — ĐÃ CHẠY THỬ TRONG UNITY, đạt
- Chạy thử trong Unity (người dùng yêu cầu): log chẩn đoán cho thấy Ram ở giữa phòng, khung đấu trường đúng (28.8,-32, 9.6x4.48),
  KHÔNG có collider hay mép khung nào chặn, nhưng vẫn "đâm" mỗi khung.
- Nguyên nhân: `FreeDistance(dir, speed*dt+0.06) < speed*dt+0.06` — biểu thức tính 2 lần, lệch số lẻ cuối (độ chính xác float của JIT)
  -> đường trống bị coi là đâm -> bật tại chỗ. Các sửa §157–§159 đều đúng nhưng không chạm gốc lỗi.
- Sửa: tính `look` một lần, so `< look - 0.001`; `ChooseBounce` so `>= rampageMinRun - 0.001`. Gỡ log chẩn đoán chi tiết (giữ dòng
  "[Ram] bật dồn dập" chỉ trong Editor).
- Kết quả chạy thử: pha 2 Ram lao chéo khắp Boiler Room, bật tường đổi hướng, vệt độc kéo dài; 0 dòng "bật dồn dập"; không exception.
- Ghi chú phím test: F4 nhảy theo id khu, khu phục kích (spec_breakout, r4_swarm, id lớn) chồng lên phòng làm thứ tự nhảy rối;
  đường tới Ram: F4 x8 -> Shift+F4 x2 (vault) -> F3 -> Shift+F4 x4 (Cistern Gate) -> Shift+F4 (Boiler Room). F7 cần bấm vào khung Game trước.

## §161 Map 2: đánh dấu khu theo cốt truyện, chặn đi tắt, F4 theo truyện (07/11 ~0h30) — chờ biên dịch + test
- Phân tích: từ hub Sluice Hall (r4) có 4 lối — Đông (e1 -> r8, mạch chính), Tây (hành lang x17–33 y50–52 -> Lab, Archive, Specimen, Culture),
  Đông-nam (x84–88 y62 -> Feed -> Office -> Boiler/Ram), Nam (Pump Control, khoá chìa Pump). Tây + Đông-nam không bị chặn -> người chơi lấy
  tay quay / đánh Ram trước khi thấy con Mắt và đọc trang nhật ký (truyện bị đảo). F4 nhảy theo id khu, khu phục kích làm rối, không bật cờ.
- Người dùng chọn (3 phương án Recommended): dải chặn mềm · mở khi đọc trang nhật ký · F4 theo chương + bật cờ.
- **Code**: `WorldFlags.HasAll` hỗ trợ `|` (HOẶC). `MapSchema.Prop.Story/StoryDone/Optional`; `EncounterZone.story[] / storyDone[] / optional`
  (MapLayout.ReadStory). `MapEncounterDirector.DevTools`: `StorySteps()` + `DebugStepZone` nhảy theo bước, nhớ `storyCursor` (khu dùng lại
  nhiều bước), bật cờ + mở cổng các bước trước; map chưa đánh dấu -> `StepZoneByTiledOrder` (bỏ khu optional). Bảng F1: "F4 Next story step".
  Sao lưu `Catalog/backups/code_2026-11-07/storyZones/`.
- **Map 2** (`Catalog/v2/map2_storyZones.py`, md5 4d61ddb4… -> **6062261600aaea27ecfdd2bb92557752**, sao lưu `backups/map2_2026-11-07/storyZones/`):
  | Bước | Khu | story_done |
  |---|---|---|
  | 1 / 1.1 / 1.2 | r1 Hatch / r2 Gallery / r3 Ether Well | landed / r2_cleared / r3_cleared |
  | 2 / 2.1 / 2.2 | r4 Sluice Hall / e1 Cistern Gate / r8 (đọc trang) | r4_started / eye_glimpsed / cistern_note_read,key_cistern_note |
  | 3 / 3.1 / 3.2 | r6 Lab (tay quay) / boil (Ram, chìa Vat) / cult (Gorgemaw, chìa Pump) | lever_handle,key_crank / boil_cleared,vat_key,key_vat / cult_cleared,pump_key,key_pump |
  | 4 / 5 | ctrl Pump Control / r8 Plague Eye | pump_console_fitted,key_crank_used,ichor_diverted / r8_cleared |
  optional: rec, arch, spec, feed, office, s2, s3, vault. Không đánh dấu: spec_breakout, r4_swarm.
  2 dải `turn_back`: `hub_west_lock` (x32.4–33, y49.8–53.2), `hub_southeast_lock` (x83.8–88.2, y63.2–63.8);
  requires `cistern_note_read|lever_handle|vat_key|pump_key|ichor_diverted`; Ren: "No - the cold air comes from the east. That's my way out."
  Minimap nướng lại.

## §162 Đánh dấu cốt truyện cho Map 1 + Map 3, chặn bỏ qua chuông ở Map 3 (07/11 ~0h45) — chờ test
- Script `Catalog/v2/maps_storyZones_m1m3.py` (cùng chuẩn §161). Sao lưu `Catalog/backups/map{1,3}_2026-11-07/storyZones/`. Minimap 2 map nướng lại.
- **Map 1** (md5 7e4033d3… -> **5c25f201688be5b0cdf41eaa8cb4acdd**): mạch thẳng, đã có cổng + 2 dải turn_back (làng: todd_greeted; bắc Hollow Caves:
  z2b_flask_taken) -> chỉ đánh dấu. zf 1 (zf_cleared,todd_greeted) · z0 2 · z1 3 · z2 4 · z2b 5 (z2b_flask_taken,learned_ground_slam) · z3 6 · z4 7 ·
  z5 8 (grok_defeated,z5_cleared). optional: z3b Healer's Tent.
- **Map 3** (md5 6213eaa4… -> **c21867e3b659d705f77e42799cf3335e**): v0 1 · v1 1.1 · v2 1.2 · v3 Commons 2 · 3 chuông thứ tự tuỳ ý v5b 3 (v5b_cleared),
  v7 3.1 (amen_rung), v9 3.2 (v9_cleared) · v10 Chapel 4 (window_solved) · v11 Morrow 5 · v12 6. optional: x1, x2, x3. Đường đi v4/v5/v6/v8 không đánh dấu
  (F4 bỏ qua, nhảy thẳng tới khu có chuông).
- **Lỗ hổng Map 3**: cửa sổ hoa hồng không yêu cầu gì -> bỏ qua cả 3 chuông, xuống thẳng Morrow. Thêm `chapel_door_lock` (turn_back, x65.6–70.4,
  y62–62.6, cửa duy nhất của nhà nguyện) requires `v5b_cleared|window_solved,amen_rung|window_solved,v9_cleared|window_solved` (bản lưu đã giải cửa sổ
  thì cho qua); Ren: "The bells still toll over the Vale - and the Eye answers every one. Silence them first."

## §163 Dải chặn turn_back bị lăn (Q) xuyên qua (07/11 ~0h50) — chờ biên dịch + test
- Ảnh người dùng: dải `hub_southeast_lock` (Map 2) chặn khi đi bộ nhưng lăn qua được. Nguyên nhân: dải dày 0,6 ô (0,19 đv), lăn đi xa hơn bề dày
  trong một khung -> không khung nào đứng TRONG dải; Tick chỉ xét `rect.Contains`.
- `MapTriggerHandler.Tick`: dải turn_back xét thêm đoạn di chuyển khung trước -> khung này cắt dải (`SegmentHitsRect`, Liang–Barsky) = bước vào;
  bỏ qua bước nhảy ≥ 1 đv (dịch chuyển, hồi sinh, cảnh diễn). Áp dụng mọi map (Map 1 làng / mỏ, Map 2 hub, Map 3 nhà nguyện).
- (07/11 ~1h05, người dùng: vẫn lăn qua được) Log có [TurnBack] -> dải CÓ bắt, nhưng đẩy sai phía: sau khi đẩy về, `lastPos` cuối Tick vẫn = vị trí
  đầu khung (bên kia dải) -> khung sau đoạn di chuyển cắt dải theo chiều ngược -> đẩy theo lastPos sang bên kia. Sửa: `MapTrigger.safeSide`
  (phía người chơi đứng lần cuối khi ở hẳn ngoài dải) -> luôn đẩy về phía đó; OnTurnBack đặt `lastPos = p`; cuối Tick `lastPos` = vị trí thật
  của người chơi sau khi xử lý trigger.

## §164 Map 1: khôi phục lái buôn (Peddler, mb_merchant) (07/11 ~1h20) — chờ test
- Người dùng: Map 1 thiếu nhân vật lái buôn. NPC `vendor` bị xoá khi người dùng sửa tay trong Tiled 02/11 (§125); xe hàng rong (prop 4374 +
  collider 4377 "merchant wagon") vẫn còn.
- `Catalog/v2/map1_restoreVendor.py`: chép nguyên object id 4366 từ `backups/map1_complete_2026-11-02/campfires/` vào lớp npcs — (101.2, 86.9)
  trước xe hàng, nhìn S, đứng yên, speaker "Peddler", 2 câu (thuế bột của Grok / thằn lằn gai ở Meadow). Không khôi phục prop 4214 cạnh đó.
  md5 Map 1 5c25f201… -> **420dde937235d7a8883bc882ff9d6cf5**; sao lưu `backups/map1_2026-11-07/restoreVendor/`. Minimap nướng lại.

## §165 Chống bị quái nặng đè kẹt (Grok ép Ren vào góc Stone Ring) (07/11 ~1h40) — chờ biên dịch + test
- Ảnh người dùng: Ren kẹt ở góc tây-nam Stone Ring (nước phía tây x<107, dải va chạm hàng cây phía nam y≥27), Grok đè sát. Hai thân Dynamic,
  Grok nặng hơn nhiều + AI cứ áp sát -> Ren bị ép vào vật tĩnh, bấm đi không thoát (lỗi "va chạm Dynamic–Dynamic" còn treo trong HANDOFF 9C).
- `PlayerController` (chung mọi map / quái / boss): mỗi FixedUpdate so bước muốn đi với quãng đã đi; muốn đi mà nhích < 25% suốt 0,25 s và có
  collider quái (EnemyAI) trong 0,3 đv -> `Physics2D.IgnoreCollision` Ren <-> collider đó (Ren lách qua), ghi log "[Unstuck]"; bật lại khi hai bên
  cách > 0,15 đv và đã qua 0,6 s. Đòn đánh không đổi (quái tính trúng theo khoảng cách). Không đổi map.
- Sao lưu: `Catalog/backups/code_2026-11-07/unstuck/`.

## §166 Tách hầm King's Barrow (X2) khỏi Map 3 thành map riêng (07/11 ~2h30) — chờ biên dịch + dựng scene + test
- Người dùng (ảnh Scene view): phòng mộ bí mật X2 nằm ở góc tây-nam Map 3, map phải kéo dài thêm 46 hàng rừng (~1.500 props thừa) chỉ để
  chứa nó -> chọn "Tách thành map riêng (Recommended)".
- **Map mới** `map3b_kings_barrow.tmx` (cùng thư mục Maps của Cemetery, md5 **0fb257e19481ecd9e76488bfa4e2852e**): 18 x 15 ô = cột 5–22,
  hàng 150–164 cũ (sát tường phòng mộ; ngoài là nền camera đen). Giữ id object cũ, dời (-5, -150) ô; collider cắt theo khung; map_bound mới;
  thêm `player_start` (id 5251, ô 10,10.5). `x2_climb`: portal -> **exit** (target `map3_vale_of_graves`, `arrive_at 118.5,66.5`,
  `arrive_title The Sinking Fields`, `quake false`, requires `x2_rope`, bỏ zone_id).
- **Map 3** cắt 172 -> **126 hàng** (mép nam hàng 120–125 vốn là rừng chặn kín): bỏ 1.371 props + mọi object dưới hàng 126, cắt blocker 4967,
  map_bound đáy/hai bên. `barrow_drop` (4509): portal -> **exit** (target `map3b_kings_barrow`, `arrive_at 10,10.5`, bỏ zone_id v8 để không
  đòi dọn khu / không bật đèn hiệu lộ bí mật). md5 c21867e3… -> **cb1aad654baeb2e16253373dad78a4c0**.
- Cả hai map: thuộc tính MAP `flag_group = vale_of_graves`. Script `Catalog/v2/map3_barrowSplit.py`; sao lưu `backups/map3_2026-11-07/barrowSplit/`.
- **Code chung (mọi map)**:
  - `GameSession`: rời map -> nhớ toàn bộ cờ theo `flag_group` (`RecallGroup`); `SetArrival/TakeArrival` (chỗ tới). `NewGame()` +
    `ForgetMaps()` (Continue) xoá.
  - `MapEncounterDirector.FlagGroup` (thuộc tính map, mặc định tên scene). Start: không nạp bản lưu mà nhóm có cờ đã nhớ -> đặt cờ, khu
    `<id>_cleared` coi như xong, `RestoreFromFlags` (y như nạp bản lưu). Có `arrive_at` -> đứng + hồi sinh ở đó thay player_start.
  - `MapTriggerHandler.OnExit` đọc `arrive_at` / `arrive_title` / `arrive_subtitle` / `quake`; `SceneTransition.Go(..., quake, title, subtitle)`
    (quake false: không rung lúc đi / lúc tới). SceneTransition thêm `map3b_kings_barrow` -> `Map3b_KingsBarrow` ("King's Barrow",
    "Beneath the Sinking Fields").
  - `TitleScreen` Continue: luôn về scene của bản lưu (lửa trại cuối). Trước đây last_scene khác scene bản lưu -> vào map đó trắng tay
    (thoát game trong hầm rồi Continue sẽ mất cờ Map 3).
- Editor: menu **Soul Requiem ▸ Dựng scene Map 3b (King's Barrow)** (chép từ scene Map 3, ánh nền 0.25 xanh, nền camera đen, thêm Build Settings).
- Nhiệm vụ: `Resources/Quests/Map3b_KingsBarrow.json` (dọn hầm -> kéo dây -> leo lên); Map 3 bỏ dải ẩn, view 136x126. Minimap 2 map nướng lại
  (`minimap_bake.py` thêm Map3b, bỏ vùng ẩn Map 3). Script `Catalog/v2/map3b_quests.py`.
- Còn lại: khu chưa dọn xong khi rời map sẽ làm lại từ đầu khi quay về (giống nạp bản lưu). Cờ `f_upgrade_burn` (King's Censer) vẫn chưa có code đọc.
- Sao lưu code: `Catalog/backups/code_2026-11-07/barrow_split/`.

## §167 Map 3: dọn cây thò ra ngoài mép map (07/11 ~2h15) — chờ người dùng xem lại
- Ảnh người dùng (Scene view, góc tây-nam Map 3): sau khi cắt còn 126 hàng, hàng cây viền nam + viền cũ ba phía thò ra ngoài khung map.
  Đếm: 543 tile object lớp `props` vượt mép (N 112, W 107, E 156, S 190), 4 cái nằm hẳn ngoài.
- `Catalog/v2/map3_trimEdges.py`: nằm hẳn ngoài -> xoá; thò một phần -> đẩy vào vừa khít mép (giữ độ dày rừng viền, xa nhất ~6 ô);
  nếu đẩy vào làm gốc cây (1/4 dưới) đè thêm ô đi được -> xoá thay vì đẩy. Kết quả: đẩy 512, xoá 31, còn thò ra 0. Không động lớp khác.
  md5 cb1aad65… -> **e0845f92e0c16ae27edd0877684c0b0d**; sao lưu `backups/map3_2026-11-07/trimEdges/`. Minimap nướng lại.
- Map 1 cũng có 455 object vượt mép (tree shadows 172, props 279, decals 4; 34 nằm hẳn ngoài) — chưa đụng, chờ người dùng quyết.

## §168 Map 3: vực chia đôi Sinking Fields + bỏ dịch chuyển cầu thang nhà nguyện (07/11 ~2h45) — chờ dựng lại scene + test
- Người dùng: lối vào khu cầu Broken Bridge vô lý, phân tích lại toàn map. Phân tích (lưới ô đi được + ảnh render):
  vực ichor mọc giữa cánh đồng (đầu vực y70) -> đi vòng chữ U qua đầu vực rồi qua cầu, trong khi chỗ chuông chỉ cách cánh đồng 4 ô rừng;
  cầu thang dưới nhà nguyện dịch chuyển ~12 ô cùng mặt đất (như thang Map 2, luật 15); bố cục quá ô vuông (thẩm mỹ, chưa sửa).
  Người dùng chọn 2 phương án Recommended.
- **Vực chia đôi đồng**: holes x99–104 kéo từ y75 lên y48 (miệng vực 2 hàng như đầu vực cũ, thân chép hàng 76–83), một collider
  nước (99,48)-(105,75) thay 15 collider hố nhỏ; bỏ hố nhỏ x96–98 y65–68 (chặn lối mép vực) và phần hố x102–105 bị vực đè.
  - Bờ tây = lối chính: Commons -> mép vực x96–98 -> hành lang -> cầu -> chuông. Bờ đông (đồng + hố xuống hầm) vào từ Penitents' Garden
    qua lối mới x129–131 y47–51 (cỏ tím nối vòng đường quanh vườn; blocker 5035 chừa lối + blocker mới x132–135).
  - Kiểm tra BFS: bờ đông không qua lối vườn thì không sang được bờ tây / chuông; bờ tây không qua cầu thì không tới chuông.
  - Khu v8 chỉ còn bờ đông (x105–131); v9 chỉ từ sau cầu (105,84,23x18). Quái 4473/4476 (đứng trên đường vực) -> (108,58)/(109,64).
    Bảng "Mass grave" + bảng ghim + va chạm dời sang bờ tây (-4 ô). Bỏ 9 decal/prop nhỏ nằm trên đường vực / lối mới.
- **Nhà nguyện**: bỏ portal `chapel_stair`, then cổng `mausoleum_bar`, toggle cửa mở `chapel_door_state` + prop `chapel_open`, 2 đèn chỉ lối
  (`chapel_stair_glow`, `cellar_stair_glow`). `mausoleum_gate` unlock_when `window_solved|mausoleum_gate_open` (bản lưu cũ đã nhấc then vẫn mở).
  Bảng `barred_gate` đổi lời (ấn ánh tím, ẩn khi giải cửa sổ); `rose_window.solved_hint` không còn nói cầu thang. Cầu thang hầm (cellar stair)
  ngoài sân giữ làm trang trí. Nhiệm vụ: điểm phụ "Stair beneath the Chapel" -> "The mausoleum gate" (68, 80.5).
- Script `Catalog/v2/map3_riftChapel.py`; md5 e0845f92… -> **3ae173089628f98e49c5f5410ede6d63**; sao lưu `backups/map3_2026-11-07/riftChapel/`.
  Minimap nướng lại. Cần chạy lại menu Dựng scene Map 3 (đèn đuốc dựng lúc build: 2 đèn đã bỏ vẫn còn trong scene).

## §169 Map 3: hai cây cầu qua vực + đường chính bờ tây, bỏ dải cây chắn đường (07/11 ~3h00) — chờ dựng lại scene + test
- Người dùng: lối vào khu không theo tuyến đường chính, thiếu chuyên nghiệp; vực dài -> cần **2 cây cầu** đặt như người dân ngoài đời sẽ bắc;
  bỏ dải cây x89–95 y57–80 cho đỡ vướng. (Phương án dời vực sang đông đã phác ở plan1 bị thay bằng hướng này.)
- Logic: đường ngang Commons (y53–55) vốn chạy thẳng tới mép vực -> **cầu bắc "xe tang"** nối tiếp con đường đó sang Sinking Fields
  (ruộng chôn người nghèo, bảng "Mass grave" dời ra ngay đầu cầu phía đông). Cầu nam Broken Bridge giữ nguyên, dẫn tới chuông. Giữa hai
  đầu cầu: đường đất dọc bờ tây (x91–93, cách mép vực 5 ô), rẽ nhánh xuống bãi lửa trại; mép vực để đất trần. Ngã ba ở đầu cầu bắc.
- Thực hiện (`Catalog/v2/map3_bridgesRoad.py`, md5 3ae17308… -> **e47abc0931a80710492ff62641a22b88**, sao lưu `backups/map3_2026-11-07/bridgesRoad/`):
  - Bỏ blocker 4997, 5049 + 34 cây / decal trong dải. Bờ tây x84–98 y52–103 vẽ lại cỏ tím (grass1) theo bộ wang "grass1 to any
    terrain(transparency)" (đỉnh = đất -> đường); xoá rocky / dirt2 / grass2 / leaves trong vùng + cột x99 (vành vực nằm trên đất).
  - Cầu bắc: chép nguyên cầu nam (2 đầu cầu + 4 nhịp, ground decals) + 3 nến + 3 đèn (`cart_bridge_candle_*`) lên 36 hàng (y52–57);
    collider vực tách khe hàng 53–55. Lấp hố nhỏ x105–107 y53–56 (holes + 3 collider) ở đầu cầu phía đông.
  - Bỏ cổng tắt `s_short` (5 object): làn cạnh nhà nguyện giờ thông ra bờ tây, cổng sẽ đứng trơ giữa bãi.
  - BFS: Commons -> cánh đồng chỉ qua cầu bắc (hoặc lối vườn); Commons -> chuông chỉ qua cầu nam. Minimap nướng lại.

## §170 Map 3: vực tự nhiên + bờ tây làm lại cho hợp phong cách chung (07/11 ~3h10) — chờ dựng lại scene + test
- Người dùng: vực thiếu tự nhiên, quá dài; đường không nhất quán; style lệch tổng thể. (Giữa chừng: "khu này nên có cây giống các khu khác"
  — làn cạnh rào nhà nguyện sau khi bỏ dải cây §169 thành bãi cỏ trơn.)
- Phân tích: vực là cột thẳng 6 ô rộng đều từ rào vườn tới đáy map; bờ tây là mảng tím phẳng + 2 đường song song (làn cũ cạnh rào nhà
  nguyện x86 và đường mới x92); mép vực bằng phẳng; không cây, không đèn, khác hẳn Commons.
- `Catalog/v2/map3_riftNatural.py` (md5 e47abc09… -> **20d733f4c12c7a9d9d8f1fdcd7df3a8c**, sao lưu `backups/map3_2026-11-07/riftNatural/`):
  - Vực vẽ lại bằng bộ wang "hole-transparency" (đỉnh, góc TL/TR/BR/BL): tâm uốn (x102 ở 2 cầu -> x107 ở y73 -> x101.6 phía nam),
    rộng 5 ô ở cầu, ~7 ô giữa, nhiễu mép mịn; vách trên 2 hàng (vành 3472–3484 + vách +16); đuôi nam thon dần, khuất dưới 3 cây +
    blocker (99,101)-(106,111); hố nhỏ của cánh đồng chạm vực được gộp. Collider sinh lại từ hình vực (ô ≥ 2 góc là hố), chừa mặt 2 cầu
    (27 rect). Bỏ dòng ichor ở đáy vực (thử: xanh chói, lệch phong cách — ichor trong map chỉ dùng điểm xuyết ở Outfall).
  - Bờ tây: cỏ tím (grass1) vẽ tới sát vành vực (vành đất trần 1 ô); làn cũ cạnh rào nhà nguyện thành cỏ -> chỉ còn MỘT đường (uốn theo
    vực, 3 đỉnh rộng như đường Commons); 6 mảng đất loang như Commons.
  - Cây: 7 cây lề so le giữa rào nhà nguyện và đường (tree - darker 1/5, tree 1; có collider gốc), 2 cây chết giữa đường và mép vực,
    5 cây bẻ viền thẳng của cánh đồng. Đèn đường (lamp post scheme 1 + đèn) ở ngã ba đầu cầu bắc và chỗ rẽ xuống cầu nam.
    Hàng rào gỗ gãy đoạn dọc mép vực phía tây (fences 3569 + collider).
  - BFS: vào cánh đồng chỉ qua cầu bắc (hoặc lối vườn), tới chuông chỉ qua cầu nam. Minimap nướng lại.

## §171 Map 3: dải rừng dày giữa rào nhà nguyện và đường bờ tây (07/11 ~3h20) — chờ test
- Người dùng (ảnh mẫu tán cây dày): khu giữa rào nhà nguyện và đường (x85–89, y62–81) phải là rừng dày như viền rừng các khu khác,
  không phải cây lẻ (§170).
- `Catalog/v2/map3_verseForest.py` (md5 20d733f4… -> **37f41e4b202015291e56fec7e0acd2c1**, sao lưu `backups/map3_2026-11-07/verseForest/`):
  bỏ 7 cây lẻ §170 + collider gốc; chép nguyên ô rừng dày nhất của map (viền tây bắc, gốc (2,5), 5 x 20 ô, 52 cây, giữ thứ tự vẽ) vào dải;
  thêm blocker (85,62)-(90,82) như mọi dải rừng (rừng = blocker + cây). Đường bờ tây không bị chắn (BFS như §170). Minimap nướng lại.
- (§166, 07/11 ~3h30) Lỗi biên dịch CS0165 `ax`/`ay` chưa gán ở `MapTriggerHandler.OnExit` (biến `out` trong chuỗi `&&` gán vào bool rồi dùng
  ở biểu thức khác -> trình biên dịch không chắc đã gán). Sửa: khai báo `float ax = 0f, ay = 0f;` trước, `out ax` / `out ay`.

## §172 Map 3: chân cầu (vòm) không nằm trên vực (07/11 ~3h50) — chờ test
- Ảnh người dùng (chạy thử): trụ vòm trái của cầu đứng trên đất, dưới vòm lộ đất hai bên, lòng vực lệch phải.
- Nguyên nhân: sprite cầu ghép 12 ô (đầu trái 4 + 4 nhịp + đầu phải 4) có vòm: trụ trái ở +2.3–3.0 ô, trụ phải +9.0–9.6 ô, lòng vòm
  +3.0–9.0 ô -> với đầu cầu ở x95, lòng vòm là x98–104. Vực §170 ở chỗ cầu chỉ x99.5–104.5 (đỉnh 100..104). Mặt cầu (+0.6–3.1 ô) chỉ
  2 hàng; cầu bắc chép từ cầu nam lệch 1 hàng so với đường Commons (54–55).
- Sửa (chạy lại chuỗi từ bản trước §170: `map3_riftNatural.py` + `map3_verseForest.py`, cả hai nhận `EXPECT` qua biến môi trường):
  vực ở 2 cầu = đỉnh 98..104 (trụ vòm đứng đúng trên vành), đường tâm CP chỉnh theo; cầu bắc (6 mảnh + 3 nến + 3 đèn) xuống 1 hàng;
  khe va chạm mặt cầu = hàng 54–55 (bắc) và 89–90 (nam); nến giữa cầu không bị coi là đồ vật trong vực.
- md5 37f41e4b… -> **81abb54afcd12149c714d5e0c68d1913**; sao lưu `backups/map3_2026-11-07/bridgeFit/`. BFS như §170. Minimap nướng lại.

## §173 Map 3: Requiem Door quay đúng hướng đường đi, bỏ lối hẹp phía Nam + 2 nhà mái vòm (07/11 ~10h50) — chờ dựng lại scene + test
- Người dùng: khu cuối Map 3 thừa cổng dịch chuyển (2 nhà mái vòm hai bên lối, cửa vòm tối không dẫn đi đâu) và cổng Requiem Door
  quay mặt về Nam, ngược đường đi (đường từ Forecourt đi xuống từ phía Bắc -> Ren chạm cửa từ sau mái).
- Phân tích: mọi hình nhà / cổng trong tileset vẽ mặt tiền hướng Nam (về phía camera) -> lối vào nhà phải tới từ phía Nam. Trigger
  `to_map4` cũ nằm trên phần thân nhà, collider ở bậc cửa -> chỉ chạm được từ phía Bắc (sau mái). Người dùng chọn: dời cửa lên đầu Forecourt.
- `Catalog/v2/map3_requiemDoor.py` (md5 bbe79ccc… [người dùng sửa tay sau §172] -> **b622175573226f076c80e8c012222e35**,
  sao lưu `backups/map3_2026-11-07/requiemDoor/`):
  - Nhà cửa (2683) ở góc đông bắc sân (góc trái x72.6, đáy y93), cạnh cổng vào; bậc cửa xuống mặt lát hàng 92. Va chạm: thân + mái
    (72.75,85.4 4.7x4.2) + 2 trụ hiên; bậc đi được; trigger `to_map4` trong lòng cửa (74.0–76.2, 89.6–91.4) -> Ren đi LÊN vào cửa.
    Blocker rừng 5092/5095 khoét lòng cửa + bậc. Đèn tím theo cửa. Bỏ 11 cây / bia đè lên nhà mới + collider bia 4867.
  - Bỏ 2 nhà mái vòm (2400, 2401).
  - Lối hẹp + phòng cửa cũ (x60–76, y109–121): ghép nguyên ô rừng bên trái cùng kích thước (x40–56: mọi lớp tile + 48 cây / decal),
    blocker phủ kín.
  - Khu v12 (story 6) = trước bậc cửa (73,91.5 4.2x2). Nhiệm vụ: đích "The Requiem Door" -> (75.1, 92.5). Minimap nướng lại.

## §174 Map 3: sân lát đá quanh lăng Requiem Door, đường đi xuống phía Nam làm lại (07/11 ~11h00) — chờ dựng lại scene + test
- Người dùng bỏ phương án §173 (cửa ở đầu Forecourt): "tạo lại đường đi xuống dưới nhưng chuyên nghiệp hơn".
- Thiết kế: mọi hình nhà trong tileset quay mặt về Nam -> lối từ Forecourt đi xuống tới SAU lưng lăng, nên làm sân lát đá bao quanh
  lăng như sân mộ thật: lối lát đá từ Forecourt xuống -> sau lăng -> 2 hành lang hai bên (5 ô) -> sân trước cửa ở phía Nam; Ren vòng ra
  trước, đi LÊN bậc vào cửa. 2 đèn trụ (ánh tím nhạt) hai bên sân trước cửa.
- `Catalog/v2/map3_requiemCourt.py`, chạy trên bản trước §173 (md5 bbe79ccc…; bản §173 b6221755… lưu ở `backups/map3_2026-11-07/requiemCourt/`):
  - Lát `stone2` theo bộ wang "stone ground2 to any terrain (transparency)" (cùng loại lối cũ), sân x61–75 y110–123 bỏ góc, nối lối cũ hàng 108–109.
  - Lăng (2683) giữ cột x, đáy y119.5. Va chạm: thân + mái (4.7x4.2), 2 trụ hiên; bậc cửa đi được; trigger `to_map4` trong lòng cửa
    (66.8–69.0, 116.1–117.9). Bỏ collider bậc cửa cũ 4870 (dành cho lối từ phía Bắc). Đèn tím theo lăng.
  - Blocker trong khung x58–78 y108–126 sinh lại từ mặt lát (ô >= 3 góc lát = đi được), phần blocker cũ ngoài khung giữ nguyên.
  - Bỏ 2 nhà mái vòm (2400, 2401), đồ vật / collider nằm trên sân mới, cây rừng sát mép Nam có tán che sân trước cửa (tổng 52).
  - Khu v12 (story 6) = sân trước cửa (61,119.6 15x3.4). Nhiệm vụ: đích "The Requiem Door" -> (67.9, 120.8). BFS: Forecourt -> trước cửa / bậc cửa OK.
- md5 Map 3 -> **0c3719cf1b7ca43bec203c5a1bf0b3b9**. Minimap nướng lại.

## §175 Map 3: bỏ cầu thang hầm sân nhà nguyện; cổng lăng có ấn ánh sáng tím, giải cửa sổ thì tự mở (07/11 ~11h05) — chờ test
- Người dùng: bỏ cầu thang (cellar stair) ở sân sau cổng nhà nguyện; giải cửa sổ hoa hồng thì cổng tự mở, ban đầu cổng có ánh sáng ma
  thuật khoá lại.
- `Catalog/v2/map3_mausoleumWard.py` (md5 0c3719cf… -> **ee7dba052d19595067d2bd8d2470adf6**, sao lưu `backups/map3_2026-11-07/mausoleumWard/`):
  - Bỏ prop 1688 (cellar stair) + collider 4836.
  - Ấn trên cổng `mausoleum_gate`: tile object hiệu ứng `mausoleum_gate_fx` (gid 4805, cùng loại ấn cổng nhà nguyện) ở lớp `ambient fx`
    + đèn tím `mausoleum_gate_glow` (off_when `window_solved|mausoleum_gate_open`) + toggle `mausoleum_ward_state` (when cùng cờ,
    hide_objects ấn). Cổng mở bằng `unlock_when` đã có từ §168; bảng `barred_gate` + `solved_hint` cửa sổ đã nói về ấn.
  - Lỗi cùng loại phát hiện kèm: ấn cổng nhà nguyện `chapel_barrier_fx` không có gì tắt -> mở cổng (đủ 3 chuông) rồi vẫn còn. Thêm
    toggle `chapel_ward_state` (when `v5b_cleared,amen_rung,v9_cleared`, hide_objects ấn). Đèn `chapel_barrier_glow` đã có off_when.
- Quy ước (ghi lại): ấn / hiệu ứng khoá cổng đặt ở `ambient fx` (luôn vẽ trên cùng) + toggle `hide_objects` theo đúng điều kiện mở cổng.

## §176 Map 3: gộp 2 cổng trước Forecourt thành một, 2 đèn trước lăng đối xứng (07/11 ~11h15) — chờ dựng lại scene + test
- Người dùng: sân nhỏ giữa cổng rào nhà nguyện (`mausoleum_gate`, y80) và cổng vòm Forecourt (`v11_lock`, y87) để làm gì, sao 2 cổng?
  (Sân từng chứa cầu thang hầm + then cổng, đã bỏ ở §168/§175 -> thành khoảng trống vô dụng.) 2 đèn trước lăng cùng một hình (lồng
  đèn bên trái) -> lệch, không đối xứng.
- `Catalog/v2/map3_forecourtGate.py` (md5 ee7dba05… -> **4f5169bc1bda69131ad9dfef1b6e04ac**; sao lưu `backups/map3_2026-11-07/forecourtGate/`):
  - Một ngưỡng duy nhất: vòm (props front 4407) + 2 trụ (1753, 1754) + va chạm trụ dời lên 7 hàng, đứng trên rào nhà nguyện, trùm cổng
    `mausoleum_gate` (ấn tím, mở khi giải cửa sổ — §175). Bỏ cổng `v11_lock` (4555–4557): Morrow là boss -> ArenaSeal tự dựng màn chắn
    quanh khu v11 (ARCHITECTURE 2.6), không cần cổng khoá đấu trường.
  - Hai bên sân cũ -> rừng (blocker (62,81) và (70,81) 4x5 + 4 cây), còn lối 4 ô thẳng xuống Forecourt.
  - Đèn trái trước lăng (5411) lật ngang (gid | 0x80000000), dời x để cột đèn giữ chỗ; ánh `requiem_lamp_0` đối xứng qua cột.

## §177 Map 3: rung chuông Tây / Nam thật, Isolde kéo Ren xuống Bower, mở màn cho mọi boss Map 3, ảnh bảng tên boss nhìn xuống (07/11 ~11h40) — chờ biên dịch + test
- Người dùng: chuông Tây (v5b) và chuông Nam (v9) có hình nhưng không rung được — dọn quái là xong nhiệm vụ, trong khi cốt truyện là
  dọn xong vẫn phải rung chuông (như Ossuary). Khu chuông Tây quá dễ, muốn thêm quái vampire; rung chuông Tây xong -> cảnh Nữ vương
  vampire nói "kẻ nào dám xâm phạm phần mộ gia tộc", hút Ren vào khu của bà, khoá cổng, bắt đánh. Lantern Knight (miniboss chuông Nam)
  cần cảnh mở màn chuyên nghiệp; mọi miniboss cần cảnh. Bảng giới thiệu boss phải dùng ảnh nhìn xuống (South), không theo hướng boss.
- Code (sao lưu `Catalog/backups/code_2026-11-07/bells_bosses/`):
  - `EnemyAI.PortraitSprite()`: Animator nhảy tạm sang `Idle_S` (không có thì `Idle`), `Update(0)` lấy sprite rồi trả state cũ.
    `BossIntro.SetPortrait` dùng hàm này (trước: `sr.sprite` = hướng boss đang quay).
  - `BellMechanism`: `requires` + `locked_text` (chuông chưa rung được tới khi đủ cờ). Chuông một nốt = `answer "1"`.
  - Mới `SummonMechanism` (type `summon`): khi đủ `when` và chưa có `sets`: đợi `delay`, khoá điều khiển + bất tử, quầng sáng `color` +
    rung, hội thoại `speaker` line1..8 (tự sang câu `auto` s), bật `sets`, `SceneTransition.Warp` tới `target` ("x,y" ô) với `warp_text`,
    `shake`. Đăng ký trong MechanismRegistry.
- Map (`Catalog/v2/map3_bellsBosses.py`, md5 4f5169bc… -> **f3e162cef5cf2fb4da2997f9debc2e9b**; sao lưu `backups/map3_2026-11-07/bellsBosses/`):
  - Cờ hoàn thành chuông: **Tây = `x3_cleared`** (rung -> Isolde -> hạ), Ossuary = `amen_rung`, **Nam = `south_bell_rung`**. Đổi ở cổng / đèn /
    toggle ấn nhà nguyện, dải `chapel_door_lock`, đèn chuông ở Commons, lửa xanh tại chuông, nhiệm vụ bước 2 (JSON).
  - `bell_west` (9,61.8) requires `v5b_cleared` -> `west_bell_rung`; `bell_south` (114.8,86.2) requires `v9_cleared` -> `south_bell_rung`.
  - `isolde_summon`: when `west_bell_rung` -> 3 câu của Isolde -> kéo Ren tới Bower (17.5,77); khu x3 tự bắt đầu, `x3_lock` đóng, Isolde xuất hiện.
    `x3_gate` mở khi `west_bell_rung` (gục thì đi bộ xuống đánh lại). x3: bỏ optional / fog secret, story 3.05, story_done `x3_cleared`.
  - Khu chuông Tây: đợt 1 thêm 2 Choir Novice (4 kẻ hát quanh chuông) + 2 Vey Kin; đợt 2 (provoke) 2 Vey Kin + 2 Vey Huntress.
    v5b story_done `v5b_cleared,west_bell_rung`; v9 story_done `v9_cleared,south_bell_rung`; gợi ý khu nói phải rung chuông.
  - Object `boss` mới: `isolde_fight` (wake đỏ, ISOLDE VEY / THE VEY MATRIARCH, 3 câu), `lantern_fight` (wake vàng, THE LANTERN KNIGHT /
    WARDEN OF THE BROKEN BRIDGE, 3 câu), `ent_fight` (wake đỏ gỗ, 2 câu); `morrow_fight` thêm wake tím, PRECENTOR OF THE CHOIR, 3 câu.

## §178 Menu Unity "Soul Requiem" gom lại, tiếng Anh, bỏ lệnh thừa, thêm lệnh thiếu (07/11 ~11h55) — chờ biên dịch
- Người dùng: chỉnh menu cho tối ưu, bỏ thừa, thêm thiếu, chuyển sang tiếng Anh.
- Mới `Assets/Editor/SoulRequiem/SoulRequiemMenu.cs` — MỘT chỗ khai báo menu (có thứ tự + vạch ngăn nhóm); các builder bỏ `[MenuItem]`
  (để lại dòng chú thích). Thêm lệnh menu mới -> thêm ở file này.
  - **Scenes**: Build Title Scene · Build Map 2 Scene (Undercity of Vael) [trước ghi "Sewers", tên cũ] · Build Map 3 Scene (Vale of Graves) ·
    Build Map 3b Scene (King's Barrow) · **Build All Map Scenes** (mới: Map 2 -> Map 3 -> Map 3b đúng thứ tự chép scene).
  - **Enemies**: Build Map 1/2/3 Enemies · **Build All Enemies** (mới) · Create Missing Enemy Definitions · Check and Repair Enemy Animators.
    Build Map 1 Enemies giờ tự mở scene Map 1 trước (builder gắn director / HUD vào scene ĐANG MỞ — chạy nhầm scene trước đây là hỏng).
    Check and Repair Enemy Animators: trước chỉ quét Map 1, giờ mọi `Assets/Enemies/<MapN>/Prefabs`; hộp thoại tiếng Anh.
  - **Player** (scene đang mở): **Rebuild Player (Open Scene)** (mới: Animator -> Sort by Feet -> Fit Collider đúng thứ tự) + 3 bước riêng.
  - Bỏ: "Gắn animation Dash vào Animator" (PlayerDashAnimatorSetup) — Build Player Animator đã dựng state Dash (Blend Tree 4 hướng,
    Any State khi DashTrigger); chạy lệnh cũ sau đó còn xoá / dựng trùng state. File chuyển `Soul Requiem/_to_delete/2026-11-07/Editor/`
    (bản chép thừa ở `Catalog/_to_delete/2026-11-07/Editor/` do lệnh mv sang thư mục khác không xoá được nguồn).
- `EnemyDefinitionTools.CreateFromPrefabs` private -> internal (menu gọi). Sao lưu `Catalog/backups/code_2026-11-07/editor_menu/`.

## §179 Map 3: không rơi xuống hố King's Barrow được (07/11 ~12h05) — chờ test
- Ảnh người dùng (chạy thử): đứng mép hố ở Sinking Fields chỉ có "Examine", không xuống được.
- Nguyên nhân: trigger `barrow_drop` (121.2–124.6, 68.6–69.5) nằm TRỌN trong va chạm của chính cái hố (water 4908 x122–124 y68,
  4912 x121–125 y69) -> chân Ren không bao giờ vào được khung trigger (lỗi có từ hồi còn là portal, §166 chỉ đổi loại).
- `Catalog/v2/map3_barrowDropFix.py` (md5 f3e162ce… -> **60d26c6fa26887030c033b8bdd507c36**; sao lưu `backups/map3_2026-11-07/barrowDropFix/`):
  trigger thành dải mép hố ngoài va chạm ở 3 phía đi tới được: Bắc (121.6,67.45 3x0.75), Tây `barrow_drop_w` (120.2,69 0.8x2.6),
  Nam `barrow_drop_s` (121.6,73 2.6x0.6) — cùng thuộc tính (exit -> map3b, arrive_at 10,10.5). Bảng `barrow_glint` lùi lên (65.6–67.4):
  đọc trước, bước thêm tới mép mới rơi.
- Quy tắc (ghi lại): trigger đặt cạnh vật cản (hố, tường) phải nằm NGOÀI collider — kiểm tra bằng lưới ô đi được trước khi giao.

## §180 Map 3: Isolde ném Ren vào đấu trường, mỗi boss một màn chào riêng, bỏ đòn đánh thường trong cảnh, ảnh bảng tên to và giữa (07/11 ~12h30) — chờ biên dịch + test
- Người dùng (3 ảnh chạy thử):
  1. Sau hội thoại của Isolde phải có cảnh bà ta hiện ra SAU LƯNG Ren và ném Ren vào đấu trường.
  2. Ảnh boss trên bảng tên bé và lệch tâm.
  3. Boss vung một đòn đánh thường giữa cảnh giới thiệu. Boss nào cũng chỉ "động đất tại chỗ" -> mỗi boss cần màn chào riêng.
- Nguyên nhân:
  - (2) Ảnh lấy thẳng từ sprite Idle_S. Khung sprite dư rất nhiều ô trong suốt, nên phần hình thật nhỏ và lệch trong khung.
  - (3) BossIntro luôn chạy Windup + Attack + rung cho MỌI boss. Đoạn này vốn chỉ để Grok đánh bay đệ.
- Code:
  - `SummonMechanism`, có thuộc tính `actor` (tên quái):
    - Dựng "diễn viên" chỉ gồm SpriteRenderer + Animator chép từ prefab. Không có EnemyAI / Health nên không đánh và không bị đánh.
    - Diễn viên hiện ra từ sương ở `actor_behind` đv phía Bắc Ren, rồi Ren quay lại nhìn.
    - Diễn viên chạy `actor_anim_appear` (Cast) kèm bong bóng `throw_line`, rồi `actor_anim_throw` (Attack).
    - Ren bay vòng cung tới `target` (`throw_time`, `throw_arc`): tắt rigidbody, xoay người; diễn viên tan dần.
    - Ren tiếp đất: rung, bụi, `PlayerCombat.PlayStagger()` (mới).
    - Không có `actor` thì vẫn tối màn dịch chuyển như cũ.
    - Mới `MapEncounterDirector.EnemyPrefab(name)`.
  - `BossIntro`:
    - Gồng + nện + rung CHỈ chạy khi boss có đệ để đánh bay, hoặc `intro_slam = true`.
    - Boss khác diễn động tác riêng `intro_anim` (Cast / Vow / Prepare / Toll); `intro_roar = true` thì rung nhẹ theo.
  - Màn xuất hiện mới (`entrance`):
    - `mist`: sương `mist_color` xoáy co về chỗ đứng, boss hiện dần rồi hạ xuống.
    - `rise`: boss trồi lên từ lòng đất, thân cao dần qua 3 nhịp (0.3 / 0.65 / 1) kèm rung và đất văng.
    - `approach`: đèn `entrance_lights` bùng lên lần lượt (`lights_color`), boss bước từ `entrance_from` tới chỗ đứng với tốc độ `entrance_speed`.
    - `turn`: boss quay lưng (Idle_N), đèn bùng lên, rồi quay lại nhìn Ren.
    - Đèn bùng trong cảnh dịu dần sau khi camera trả lại.
  - `BossIntro.Trim(sprite)` cắt ảnh theo khung hình thật (mesh Tight), nên ảnh bảng tên to và giữa đúng tâm.
    - Có cache.
    - `EnemyAI.PortraitSprite()` lấy khung Idle_S.
  - Biên dịch lần 1 lỗi CS0136 (`from`, `dur` trùng tên với biến ngoài) -> đổi tên thành `h0`, `walkT`. Đã biên dịch sạch lúc 05:10.
- Map (`Catalog/v2/map3_bossEntrances.py`, chạy với `EXPECT=db0139e8…` vì người dùng vừa sửa trong Tiled; db0139e8… -> **b2d7cf34d262d370ba0e65039c594dc1**):

  | Object | Thuộc tính |
  |---|---|
  | `isolde_summon` | actor Matriarch_Isolde, behind 0.55, Cast/Attack, throw_line "Down you go, little thief.", 1.15 s, cung 1.5 |
  | `isolde_fight` | mist (#ff3a6a), Cast |
  | `lantern_fight` | approach từ 116,88.6, tốc độ 0.9, 3 đèn ichor + lửa chuông Nam (#ffd27a), Vow |
  | `ent_fight` | rise, Prepare |
  | `morrow_fight` | turn, 4 cột nghi lễ (#6aa8ff), Toll |

## §181 PixelLab cho màn giới thiệu boss và cảnh diễn Map 3: tranh bảng tên + 4 hiệu ứng (07/11 ~12h50) — chờ biên dịch + test
- Người dùng: được dùng PixelLab cho màn giới thiệu boss và cảnh diễn để chuyên nghiệp hơn.
- Tranh bảng tên (`create_image_pro` 136x168, có tham chiếu là sprite Idle_S của chính boss; 4 ứng viên một lần, tổng 60 lượt tạo):
  - Ảnh: `Resources/UI/Requiem/boss_isolde.png` (giơ hai tay vuốt), `boss_lantern` (rút kiếm, đèn lồng sáng), `boss_morrow` (gậy chuông, giơ tay),
    `boss_ent` (rễ vươn, sáp xanh).
  - Ảnh nhìn South, cắt sát và có viền trắng 3 px như tranh Map 1/2.
  - Dựng lại bằng `python3 Catalog/v2/tools/boss_portraits_m3.py` (chạy từ ~/mnt). Ảnh gốc PixelLab ở `Catalog/v2/tools/pl_src/m3_portraits/`.
  - Ảnh Ent rất đáng giá: sprite Ent_3 chỉ cao 47 px, nên lúc trước bảng tên phóng to một hình mờ.
- Hiệu ứng (PixelLab VFX Workbench, không tốn lượt; đã lưu ở My effects):
  - Đều nằm trong `Resources/VFX/`. Nhập bằng `pl_vfx.py` với tuỳ chọn mới `dropLast`: bỏ khung cuối, nơi workbench vẽ lại nhân vật mẫu.
  - .meta tạo bằng `vfx_meta.py` theo `vfx_m3_manifest6.json`.

  | Tên | Nội dung | Khung |
  |---|---|---|
  | `m3_intro_bats` | sương máu gom về, nổ thành bầy dơi tung ra | 15 khung 174x162, neo ở chân |
  | `m3_intro_roots` | đất nứt, rễ gai đâm lên, sỏi và bụi | 15 khung 112x83 |
  | `m3_intro_toll` | chớp xanh ở quả chuông + 3 vòng sóng xanh lan trên đất | 15 khung 210x136 |
  | `m3_intro_ignite` | lửa hổ phách bùng + tia lửa nguội dần | 12 khung 49x45 |

- Code (phát hiệu ứng theo thuộc tính Tiled; tên rỗng hoặc `none` thì không phát; `<key>_scale` / `<key>_fps` ghi đè):
  - `BossIntro`:
    - `entrance_vfx`: mist phát ở nhịp xoáy thứ 7, nên dơi nổ đúng lúc hiện hình; rise phát ở nhịp trồi đầu.
    - `light_vfx`: phát ở từng đèn khi đèn bùng; tỉ lệ mặc định 0.6.
    - `intro_vfx`: phát cùng `intro_anim`, ngay dưới chân boss.
    - Tỉ lệ mặc định = cỡ thật của boss (`BossScale`: lossyScale × 100 / PPU).
  - `SummonMechanism`:
    - `actor_vfx` phát khi diễn viên hiện hình, chờ `actor_vfx_lead` (0.45 s) rồi mới hiện dần; phát lại khi ném (tan thành bầy dơi).
    - `land_vfx` phát khi Ren tiếp đất.
  - Sao lưu ở `Catalog/backups/code_2026-11-07/intro_vfx/`.
- Map (`Catalog/v2/map3_introVfx.py`, md5 b2d7cf34… -> **ed30dd0da9709af2af154161e6a02040**; sao lưu ở `backups/map3_2026-11-07/introVfx/`; không đổi ô nên không cần bake minimap):

  | Object | Thuộc tính |
  |---|---|
  | `isolde_fight` | portrait boss_isolde, entrance_vfx m3_intro_bats |
  | `lantern_fight` | portrait boss_lantern, light_vfx m3_intro_ignite |
  | `ent_fight` | portrait boss_ent, entrance_vfx m3_intro_roots |
  | `morrow_fight` | portrait boss_morrow, intro_vfx m3_intro_toll |
  | `isolde_summon` | actor_vfx m3_intro_bats, land_vfx ground_impact (0.6) |

- PixelLab: còn ~2 840 lượt tạo (làm mới 10/11).

## §182 Màn tiêu đề làm lại toàn bộ: tranh PixelLab, lớp động, logo huy hiệu, menu chữ có lửa (07/11 ~17h20) — chờ biên dịch + test
- Người dùng (ảnh màn tiêu đề): giao diện và nền cần chuyên nghiệp hơn, được dùng PixelLab.
  - Chọn cảnh "Ren nhìn Vale of Graves".
  - Chọn làm lại toàn bộ (logo, nút, chân trang, mở màn).
- Vấn đề cũ:
  - Nền là minimap Map 3 trôi chậm: trông như bản đồ chứ không phải tranh, ảnh mờ và rối.
  - Lúc nền trôi bị hở dải đen bên phải.
  - Không có điểm nhấn, nút khung dày, chân trang liệt kê cả dãy phím điều khiển.
- PixelLab (60 lượt):
  - Tranh chủ đạo 480x270, phóng x4 = 1920x1080:
    - Ren đứng quay lưng trên vách đá cỏ đỏ, nhìn xuống thung lũng mộ sương phủ.
    - Bên trái là nhà nguyện cửa sổ sáng và triền bia mộ; xa xa là nhà thờ đổ nát có tháp chuông đứng trước trăng máu.
    - Lần đầu vẽ 640x360 thì tranh chỉ nằm giữa, viền phẳng -> dùng phần tranh làm tham chiếu, vẽ lại 480x270 phủ kín mép.
  - Huy hiệu 64x64, chọn 1 trong 16 mẫu: chuông đồng, trăng lưỡi liềm đỏ, hai kiếm chéo, khung vòm gothic.
  - Lửa con trỏ menu (VFX Workbench, 8 khung 16x26, lặp; không tốn lượt).
- Ảnh vẽ bằng code (`Catalog/v2/tools/title_assets.py`): dải sương liền mạch 240x40, dơi 2 khung 9x5, quầng sáng tròn, dải tối trái, viền tối.
  - Mọi ảnh ở `Resources/UI/Requiem/title_*.png`.
  - Dựng lại bằng `python3 Catalog/v2/tools/title_assets.py` (chạy từ ~/mnt). Nguồn PixelLab ở `Catalog/v2/tools/pl_src/title/`.
- Code:
  - `TitleScreen.cs` viết lại: nền tranh phủ kín mọi tỉ lệ (cắt mép, đúng x4 ở 16:9). Các lớp động đặt theo toạ độ điểm ảnh của tranh và
    bước theo điểm ảnh tranh (không trượt nửa điểm ảnh):

    | Lớp động | Chi tiết |
    |---|---|
    | Quầng trăng | thở chậm, tâm (367,84) |
    | Cửa sổ nhà nguyện | lập loè như nến, tâm (152,194) |
    | Sương A | y 124, trôi phải |
    | Sương B | y 232, trôi trái, nhanh hơn |
    | Tàn lửa | 26 hạt bay lên từ vách đá, gió thổi về trái |
    | Bầy dơi | 2–4 con, 9–16 s một lần, bay ngang trời |
    | Che tối | dải tối bên trái + viền tối |

  - Khối giữa-trái gồm huy hiệu x3, SOUL REQUIEM 100px, câu phụ, vạch.
  - Mở màn: màn đen mờ dần 1.6 s, khối tên hiện và trôi lên, mục menu hiện lần lượt.
  - Mới `TitleMenuItem.cs` (VisualElement), mục menu chữ trơn không khung:
    - Đang chọn: chữ vàng phóng 1.06, hai ngọn lửa lệch nhịp hai bên, dải sáng đỏ mờ phía sau.
    - Rê chuột = chọn; Enter / click = bấm.
  - Chân trang: `v0.3` bên trái, "Enter Confirm · W / S Select" bên phải. Bỏ dãy phím điều khiển.
  - Settings mở trên nền tối 72%; Esc / Back quay về đúng mục Settings.
  - Hành vi Continue / New Game / Quit giữ nguyên.
- USS: thêm nhóm `.sr-tm-*`. Lớp `.sr-title-*` cũ không dùng nữa, chưa xoá.
- Sao lưu: `Catalog/backups/code_2026-11-07/title_v2/` (TitleScreen.cs, Requiem.uss cũ).

## §183 Map 2: 3 chiêu lẻ nhặt ở khu phụ, bộ "Mantras of the Vessel" khi hạ Specimen Zero, mục Passives, cân bằng chiêu (07/11 ~19h40) — chờ biên dịch + test
- Người dùng: thiết kế thêm chiêu nhặt ở Map 2 và bộ chiêu khi hạ boss ẩn, dùng PixelLab, cân bằng như chuyên gia.
  - Duyệt bản thiết kế, gồm 3 chiêu lẻ và bộ Vessel.
  - Chọn thêm mục Passive cho 2 sách.
- Phân tích trước khi làm:
  - Chém thường: 30 sát thương mỗi nhát, khoảng 0,62 s một nhát (≈ 48 DPS), Ren 100 HP.
  - Chỉ có MỘT chiêu lẻ (Earthshatter), nên lấy bộ Mắt rồi thì không còn gì để phối.
  - Specimen Zero (1.100 HP, khó nhất Map 2) hạ xong chỉ có thành tựu.
  - Hai sách chỉ là cờ ẩn.
- Quy tắc cân bằng: xem ARCHITECTURE 2.11.
- Chiêu lẻ (cuộn chiêu PixelLab trên bàn hoặc sàn, đèn vàng thở, nhặt thì cuộn và đèn tắt theo cờ `scroll_<id>_taken`):

  | Chiêu | Nơi nhặt | Hiệu ứng | Hồi |
  |---|---|---|---|
  | **Severing Draw** (`sever_draw`) | Archive (arch) | lướt 1,6 đv (5 ô) theo hướng ngắm, bất tử lúc lướt, 32 mỗi quái trên đường, khựng 0,3 s cuối; tường chặn | 6 s |
  | **Ichor Flask** (`ichor_flask`) | Feeding Hall (feed) | ném ≤ 2,6 đv: vỡ 15 (r 0,6), vũng 4 s gây 6/s + Corroded +20% (còn 1,5 s sau khi ra); Ichor_*, Vat_Born, Brood_Toad kháng Corroded | 12 s |
  | **Riposte** (`riposte`) | Warden's Office (office) | thế đỡ 0,45 s, chặn đòn đầu, phản 60 + choáng 1,2 s (boss: Staggered +25% trong 2 s), hoàn 50% hồi, bất tử 0,3 s; đỡ hụt khựng 0,35 s | 8 s |

- Bộ Vessel (cổ vật Specimen Zero: `relic_skill vessel_roots`, màu tím; bản lưu cũ đã hạ boss -> trao luôn):

  | Ô | Chiêu | Hiệu ứng | Hồi |
  |---|---|---|---|
  | I | Grasping Roots | dậm, rễ thịt trồi nối đuôi 2,4 đv: 24 + trói 1,4 s (boss chỉ nhận sát thương) | 9 s |
  | II | Hand of the Vessel | bóng bám quái gần điểm ngắm 0,5 s (khoá 0,12 s trước), bàn tay tóm: 75 + choáng 1,5 s (boss: Staggered +25% / 2 s) | 15 s |
  | III | Requiem Wail | niệm 0,8 s (lăn = huỷ, hoàn hồi), sóng r 1,6: 45 + Spent +30% / 4 s (cả boss), hồi 3 HP mỗi quái (≤ 15) | 30 s |

  - Rễ và bàn tay dùng lại tranh PixelLab của chính Specimen Zero.
- So sánh hai bộ chiêu, sát thương lên boss trong 30 s:
  - Bộ Mắt ≈ 355, kèm 4,5 s bất tử. Đánh xa, an toàn.
  - Bộ Vessel ≈ 330, kèm hồi máu và khống chế bầy. Đánh gần–trung, rủi ro hơn.
- Passives: mới `PassiveCatalog`. Bảng nghỉ (Mantras) có mục "PASSIVES - always active" cuối danh sách, hiện 2 sách đã học (Turned Blade,
  Purifier's Rite) với icon PixelLab mới. Không chiếm ô.
- Code mới:
  - `Player/Skills/SkillKit.cs`:
    - Ngắm: AimDir, AimPoint.
    - Dò: WallDistance, FreeSpot, EnemiesIn, EnemiesAlong.
    - Hiệu ứng lên quái: Control (choáng, hoặc boss -> lộ sơ hở), Vulnerable (+ dấu mũi tên trên đầu, nhuộm theo nguồn), HitFx.
    - Diễn: TintWhile, PlayFrames, Frames.
  - 6 lớp chiêu (SeveringDrawSkill, IchorFlaskSkill, RiposteSkill, GraspingRootsSkill, VesselHandSkill, RequiemWailSkill), `PassiveCatalog.cs`.
- Code sửa:
  - `Health`: ParryWindow / EndParry / sự kiện Parried (chặn đòn đầu); AddVulnerability / VulnBonus (nhân vào sát thương nhận, không cộng
    dồn, lấy mức cao nhất); Revive xoá cả hai.
  - `PlayerCombat`: PlayIaido, PlayDash, SetFacing.
  - `PlayerLoadout.Refund`.
  - `SpriteVfx.Extend`.
  - `MapTriggerHandler` (item=skill có `hide`).
  - `RequiemMenus.AddPassives`.
- Asset:
  - `Resources/Skills/{SeveringDraw,IchorFlask,Riposte,VesselRoots,VesselHand,VesselWail}.asset`, sinh bằng
    `Catalog/v2/tools/skills_m2/make_assets.py` (bảng số liệu ở đầu file; guid lớp đọc từ .cs.meta).
  - Code vá bằng `tools/skills_m2/patch_code.py` (chạy lại không vá trùng). Sao lưu ở `Catalog/backups/code_2026-11-07/skills_m2/`.
- PixelLab:
  - 8 icon 64x64, mỗi icon chọn 1 trong 16 mẫu, theo kiểu icon Earthshatter: `ic_mantra_draw/flask/riposte`, `ic_vessel_roots/hand/wail`,
    `ic_passive_turned_blade/purifier`.
  - Cuộn chiêu 22x26 (chọn 1 trong 64).
  - 6 hiệu ứng VFX Workbench (Resources/VFX, manifest `tools/skills_m2/vfx_skills_manifest.json`): `sk_draw` (vệt rút kiếm, neo trái, kéo
    giãn theo quãng lướt), `sk_ichor_splash`, `sk_ichor_pool` (lặp), `sk_riposte`, `sk_wail`, `sk_vuln` (dấu lộ sơ hở, trắng để nhuộm).
  - `sk_vial` (lọ Ichor 7x10) vẽ bằng code.
  - Tốn 100 lượt.
- Map 2 (`Catalog/v2/map2_mantraScrolls.py`, md5 60622616… -> **a708d78248215d62b5ebb698b3e96077**; sao lưu `backups/map2_2026-11-07/mantraScrolls/`):
  - Tileset mới `SR-animT-sr-extra2-sr-mantra-scroll` (firstgid 14639).
  - Mỗi cuộn gồm: hình (mechanics_art), pickup item=skill (triggers), đèn `off_when`, toggle ẩn hình.
  - `vault_fight` thêm relic_*.
  - Đã bake minimap.

## §184 Bộ Mắt: Judgement Ray tắt được bằng bấm lại, tăng sát thương tia; Seeker Embers giảm sát thương, tăng hồi (07/11 ~23h05) — chờ biên dịch + test
- Người dùng: chiêu C (tia laser) của bộ Mắt không tắt được. Muốn tăng sát thương tia, giảm sát thương và tăng hồi chiêu F (Seeker Embers).
- Nguyên nhân "không tắt được":
  - Bấm lại C không có tác dụng: ô đang hồi (đã tính lúc thi triển) và Ren đang bị khoá trong lúc bắn, nên `TryCast` bỏ qua.
  - Chỉ Roll mới tắt được tia, mà không có gợi ý nào.
- Sửa:
  - `SkillDefinition.TryStop` (ảo, mặc định false); `PlayerLoadout.TryCast` gọi trước khi xét hồi chiêu và khoá.
  - `JudgementRaySkill.TryStop`: đang bắn thì dừng ngay.
  - Tắt sớm (bấm lại hoặc lăn) được hoàn `earlyRefund` 0,5 × phần thời gian chưa bắn vào hồi chiêu.
  - Gợi ý một lần (`tut_ray_stop`): "Press C again (or roll) to end the ray early."
  - Mô tả chiêu trong bảng nghỉ cập nhật theo.
- Số liệu (`Resources/Skills`):

  | Chiêu | Thay đổi |
  |---|---|
  | EyeRay | dps 30 -> **45** (225 mỗi lần bắn đủ 5 s) |
  | EyeOrbs | hồi 12 -> **15** s; hitDamage 8 -> **6**, blastDamage 6 -> **5**, burnDps 4 -> **3** (mỗi cầu 26 -> 20) |

- Cân bằng lại, sát thương lên boss trong 30 s:
  - Bộ Mắt ≈ 400 (Embers ~130 + Ray ~270), kèm Veil. Phần lớn nằm ở tia, đứng yên 5 s, nên rủi ro cao hơn trước.
  - Bộ Vessel ≈ 330.
- Sao lưu: `Catalog/backups/code_2026-11-07/eye_tune/`.

## §185 Map 3: phân tích điểm nghỉ, thêm rest_vey (House Vey) và rest_fields (Sinking Fields) (07/11 ~23h20) — chờ dựng lại scene + test
- Người dùng: phân tích và thêm điểm hồi sinh (set spawn) phù hợp cho Map 3.
- Hiện trạng: 6 điểm nghỉ.
  - Outfall (xuất phát), Commons (ngã ba), Field of the Unnamed (khu phụ), Ossuary Tower, rest_bridge (đầu cầu nam, trước Lantern Knight),
    Chapel (trước Forecourt / Morrow).
  - Khoảng cách từ điểm nghỉ gần nhất tới boss: Ent ~15 ô (Commons), Lantern ~14 ô (rest_bridge), Morrow ~15 ô (Chapel). Các tuyến này đủ.
- Thiếu:
  - **Cánh Tây** (Avenue of Peers -> House Vey -> West Bell -> trận Isolde): gục ở Isolde phải đi bộ từ Commons ~70 ô qua 3 khu.
  - **Sinking Fields** (khu phụ qua cầu bắc, lối xuống King's Barrow): ~30 ô từ Commons, không có điểm nghỉ.
    - Ghi chú: Fields không nối thẳng xuống Broken Bridge (blocker y 80–85). Lantern Knight đi đường cầu nam, đã có rest_bridge.
- Thêm (`Catalog/v2/map3_restPoints.py`, md5 480850da… (người dùng sửa trong Tiled) -> **64eb7067bcba5f817c215d4cbf10f0b9**;
  sao lưu `backups/map3_2026-11-07/restPoints/`):
  - Bộ dụng cụ giống hệt rest_commons: bàn thờ gid 4494, 2 nến gid 4893, 2 va chạm nến, đèn torch flicker #ffd9a0 r 4.5.
  - Script kiểm tra không đè lên va chạm sẵn có.

  | Điểm nghỉ | Vị trí | Khu | Vai trò |
  |---|---|---|---|
  | `rest_vey` | ô (17.6, 53.4) | v5 | sân House Vey, bên đường từ cổng bắc xuống cổng West Bell. Gục ở trận Isolde -> tỉnh ở đây, đi ~20 ô qua West Bell (đã dọn) xuống x3_gate |
  | `rest_fields` | ô (105.6, 57.0) | v8 | đầu cầu bắc phía đông. Khám phá Fields + lối xuống King's Barrow |

  - Đã bake minimap.

## §186 Hệ thống ẢI (stage): gục thì ải chưa xong hồi quái, qua ải thì hồi đầy + khoá; Map 3 chia 5 ải chính + 1 ải phụ (08/11 ~02h40) — chờ biên dịch + dựng lại scene + test
- Người dùng: thua boss thì phải đánh lại toàn bộ quái trước đó. Một ải = nhóm khu của một nhiệm vụ, mỗi ải một miniboss và một điểm spawn.
  Qua ải thì đầy máu.
  - Duyệt: bỏ / dời điểm nghỉ giữa ải.
  - Gục thì MỌI ải chưa xong hồi quái.
- Hệ thống (chung mọi map, dữ liệu Tiled) — mới `MapEncounterDirector.Stages.cs`:
  - Thuộc tính `stage` trên khu = mã ải. Trên checkpoint: `stage` (điểm hồi sinh của ải, đặt ở đầu ải) + `stage_name`.
  - Gục:
    - Tỉnh ở điểm hồi sinh của ải đang đánh, nếu đã thắp. Ải được xác định bằng khu chứa chỗ gục, không có thì ải vào gần nhất.
    - Mọi ải chưa xong hồi quái: Rearm từng khu, kể cả khu đã dọn.
  - Giữ cờ `<khu>_cleared`: cổng tắt, câu đố, đồ nhặt, chuông vẫn giữ.
  - Qua ải (mọi khu CÓ QUÁI của ải đã dọn): cờ `stage_<mã>_done`, đầy máu + bình, tự lưu, dòng "<tên ải> - trial complete...".
    Thay cho hồi máu sau boss khi boss là khu cuối của ải.
  - Nạp bản lưu: ải chưa xong cũng hồi quái (ApplyStagesOnLoad).
  - Khu có hang sinh quái không làm lại được: giữ nguyên.
  - Gợi ý lần chết đầu (`tut_death`) viết lại theo luật ải.
- Sửa kèm:
  - `GateController.RearmArena`: trước đây cổng khoá đấu trường đã "opened" thì `OpenForClearedZone` bỏ qua, nên đánh lại xong cổng kẹt đóng.
  - `MapLayout` (EncounterZone.stage), `MapSchema` (Stage, StageName), `MapTriggerHandler` (TryStageSpawn, StageName).
  - Vá bằng `Catalog/v2/tools/stages/patch_stages.py`; sao lưu ở `Catalog/backups/code_2026-11-08/stages/`.
- Map 3 (`Catalog/v2/tools/stages/map3_stages.py`, md5 64eb7067… -> **b201d5636988c9f716fae5c57a10fa59**; sao lưu
  `backups/map3_2026-11-08/stages/`; đã bake minimap):

  | Ải | Khu (số quái) | Kết ải | Hồi sinh |
  |---|---|---|---|
  | descent - The Descent | v0, v1 (5), v2 (4), v3 Commons (2) | dọn Commons | rest_outfall |
  | west - The West Bell | v4 (10), v5 (3), v5b (10), x3 | Isolde | **rest_west** (mới, cạnh đường cái cửa Avenue, ô 46.2,41) |
  | ossuary - The Ossuary Bell | v6 (6), v7 (7) | Ent + tháp | rest_commons |
  | south - The South Bell | v9 (8) | Lantern Knight | rest_bridge |
  | requiem - The Requiem | v10, v11, v12 | Morrow | rest_chapel |
  | fields - The Sinking Fields (phụ) | v8 (8) | dọn Fields | rest_fields |

  - Bỏ `rest_ossuary` (giữa ải Ossuary, gồm checkpoint, bàn thờ, nến, va chạm, đèn).
  - `rest_vey` (§185) dời thành `rest_west` ở đầu ải.
  - `rest_unnamed` (khu bí mật x1, không quái) giữ, không thuộc ải.

## §187 Chia ải cho Map 1 và Map 2 (08/11 ~03h10) — chờ biên dịch + dựng lại scene + test
- Người dùng: thiết kế ải cho cả Map 1 và Map 2, theo luật §186.
- Code: checkpoint dùng chung nhiều ải — `stage = "a,b"`, `stage_name = "a=Tên A;b=Tên B"`.
  - `MapTriggerHandler.HasStage`, `StageName`; vá bằng `tools/stages/patch_stages2.py`.
  - Thư viện script mới `Catalog/v2/tools/stages/stagekit.py`: md5, sao lưu, giữ xuống dòng, kiểm tra va chạm, thêm / xoá object.
- **Map 1** (`tools/stages/map1_stages.py`, md5 420dde93… -> **7d2dc225a7b92ded2ac80bb08be9a64a**; sao lưu `backups/map1_2026-11-08/stages/`):

  | Ải | Khu (quái) | Kết ải | Hồi sinh |
  |---|---|---|---|
  | (không) | zf Millbrook (2 Goblin Torch) — mở đầu trong làng, dọn một lần là xong | | |
  | brook - Brookside Trail | z0 (1), z1 (3 Lizard) | dọn Meadow | z0_fire |
  | mine - The Hollow Caves | z2 (hang sinh quái, không làm lại), z2b | **Mad Bear** | z2_fire |
  | camp - Greystone Camp | z3 (7 Goblin) | dọn trại | **camp_fire** (mới: ngoài cổng trại, tây đường đất, ô 34,33.6) |
  | ring - The Stone Ring | z4 (5), z5 | **Grok** | **ring_fire** (mới: trong trại, cạnh cổng z3_gate ra Ruins Path, ô 39.8,19.6) |

  - Bỏ `z4_fire`: nằm giữa ải, ngay trước Grok (checkpoint, lửa, va chạm, khúc gỗ).
  - Bộ lửa trại mới giống hệt z0_fire.
- **Map 2** (`tools/stages/map2_stages.py`, md5 a708d782… -> **dfdf66dca6762349b5926f3907c71420**; sao lưu `backups/map2_2026-11-08/stages/`):

  | Ải | Khu | Kết ải | Hồi sinh |
  |---|---|---|---|
  | descent - Into the Undercity | r1, r2 (5), r3 (5) | dọn Ether Well | **rest_hatch** (mới: góc dưới phải phòng Hatch, bộ nến như rest_cistern) |
  | lab - Malric's Laboratory | r6 (6) | dọn phòng thí nghiệm | hub_rest |
  | boiler - The Boiler Room | boil | **Ichor Ram** | rest_cistern |
  | culture - The Culture Hall | spec, cult | **Brood Toad** | rest_cells |
  | cistern - The Eye's Cistern | ctrl, e1, r8 | **Plague Eye** | rest_cistern (dùng chung với boiler) |
  | vault - The Containment Vault (ẩn) | vault | **Specimen Zero** | rest_reliquary |

  - Không thuộc ải: hub r4; khu phụ rec, arch, s2, s3, feed, office; sự kiện spec_breakout (sau Brood Toad), r4_swarm.
  - rest_office (phòng phụ) giữ làm chỗ nghỉ / lưu.
- Đã bake minimap Map 1, Map 2.

## §188 Sửa: đứng gần cổng / cửa khu bên cạnh (chưa vào, chưa tới nhiệm vụ) mà quái đã sinh (08/11 ~08h45) — đã biên dịch, chờ test
- Người dùng: đứng gần cổng khu khác, chưa qua khu đó và chưa tới nhiệm vụ của khu, mà quái đã sinh.
- Nguyên nhân: trận bắt đầu ngay khi chân Ren vừa lọt vào khung khu (`z.rect.Contains`). Mép khung thường nằm ngay cửa vào, và nhiều khung
  chồng 1 ô lên khu bên cạnh, nên đứng ở cửa đã tính là "vào khu".
  - Map 3: Commons/Penitents' Garden chồng nhau ở x92–93, y31–49 (đứng ở lối vào vườn là Ent và cả vườn sinh quái).
  - Map 3: Commons/Avenue of Peers chồng nhau ở x43–44.
  - (Map 2 r4/r4_swarm, spec/spec_breakout chồng là cố ý — khu sự kiện có start_when.)
- Sửa (chung mọi map): `ReadyToStart` đòi Ren vào sâu `entryDepthTiles` (mặc định 2 ô) so với MỌI mép khung.
  - Khu nhỏ: tối đa 30% cạnh ngắn.
  - Ghi đè từng khu bằng thuộc tính Tiled `entry_depth` (ô).
  - Dải chồng giữa hai khu (1 ô) giờ không kích hoạt khu nào.
- File: `MapEncounterDirector.cs` (entryDepthTiles + kiểm tra), `MapLayout.cs` (EncounterZone.entryDepth), `MapSchema.cs` (EntryDepth).
  Sao lưu ở `Catalog/backups/code_2026-11-08/entry_depth/`.
- Không sửa map. Không cần dựng lại scene, trừ khi director trong scene đã lưu giá trị cũ (trường mới tự lấy 2).

## §189 Sửa: Plague Eye ít máu (pha 2) chỉ spam Death Ray (08/11 ~08h50) — đã biên dịch, chờ test
- Người dùng: boss Plague Eye khi ít máu chỉ bắn laser liên tục.
- Nguyên nhân: hồi chiêu Ray được đặt ở ĐẦU tia (`nextRay = now + cd`). Pha 2 hồi = 9 x 0,7 = 6,3 s, nhưng một lần tia đã dài ~6,05 s
  (gồng 1,3 + quét 4,2 + tắt 0,25 + nghỉ 0,3), cộng nghỉ lượt 0,6 s -> tia vừa tắt là hồi xong. Act ưu tiên Ray hơn Spin / Volley
  -> laser nối laser, Volley / Spin gần như không còn.
- Sửa (`PlagueEyeSkills.cs`):
  - Hồi tính từ lúc tia TẮT: đặt lại `nextRay = now + cd` sau `Cleanup`. Đầu Ray đặt tạm `now + gồng + quét + cd` (phòng tia bị ngắt).
  - Pha 2 hồi = `rayCooldown` x `rayCooldownP2` (mới, 0,8 -> 7,2 s sau khi tia tắt; trước là 0,7 tính từ đầu tia).
  - `minActsBetweenRays` (mới, 1): giữa 2 lần tia luôn có ít nhất 1 chiêu khác (Volley / Spin / triệu hồi).
  - Pha 1 cũng hưởng: hồi 9 s tính từ lúc tia tắt (trước ~3 s sau tia).
- Sao lưu `Catalog/backups/code_2026-11-08/plague_ray/`; script `Catalog/v2/tools/bosses/plague_ray_fix.py`.
- Không sửa prefab (trường mới dùng giá trị mặc định). Không cần dựng lại scene.

## §190 Unity sập (artifact hỏng); leo dây / trèo xuống hố bằng T; Grave Worm đào hầm phục kích; thanh máu quái sát đầu (08/11 ~11h40) — đã biên dịch (04:38 UTC, không lỗi), chờ dựng quái Map 3 + test
- **Unity sập khi mở** (Editor.log 11h24): "# Fatal Error! The file 'Library/Artifacts/b1/b1057d0016b703605785c3e240f88cbf' is corrupted!"
  (SuperTiled2Unity TiledAssetDependencies nạp SuperAsset lúc mở scene khôi phục `_Recovery/0 (9).unity`). Dời file đó sang
  `_to_delete/2026-11-08/Library_Artifacts_corrupt/` (cache, Unity tự nhập lại) -> Unity mở lại bình thường (Title, không lỗi CS).
  Còn: lỗi mạng của gói `com.unity.ai.assistant` (SuperProxyClient / Curl error 35) — không phải lỗi project. `_Recovery/0 (1..10).unity`
  là cảnh khôi phục sau các lần sập — chưa xoá (chờ người dùng).
- **Ảnh 2 — King's Barrow bấm T không leo lên, phải đi một lúc**: `x2_climb` là exit tự đi khi BƯỚC VÀO, nằm ngay dưới cần kéo dây
  `x2_rope_lever`. Lại gần dây -> đã đứng trong x2_climb (chưa có cờ -> "The rope is out of reach") -> nhấn T kéo dây (cờ x2_rope) nhưng
  không có lần "bước vào" mới -> phải đi ra rồi đi vào lại.
- **Ảnh 3 — muốn nhấn T để trèo xuống hố**, không lỡ chân rơi: 3 dải `barrow_drop / _w / _s` cũng là exit tự đi khi bước vào.
- Sửa (chung mọi map): thuộc tính exit **`interact`** (bool) -> không tự đi khi bước vào; `MapTriggerHandler.RegisterExits` đăng ký với
  InteractionSystem: `[T] <verb>` (mặc định "Enter"), tầm `range` (0,55). Chưa đủ cờ / khu chưa dọn -> ẩn (vật mở khoá gần đó nhận T),
  `show_locked` = true thì vẫn hiện và nhắc locked_text. Director gọi sau RegisterSigns. Exit không có `interact` giữ như cũ.
  Script `Catalog/v2/tools/exits/patch_interact_exits.py`, sao lưu `backups/code_2026-11-08/interact_exits/`.
- Map: `Catalog/v2/tools/exits/map3_interactExits.py` (sao lưu `backups/map3_2026-11-08/interactExits/`):
  - Map 3b `x2_climb`: interact, verb "Climb up". md5 0fb257e1… -> **95cf1c7dc0a4bd4bb8dfdf034ffe6325**.
  - Map 3 `barrow_drop`, `_w`, `_s`: interact, verb "Climb down", depart_text "You lower yourself over the broken edge and drop into the dark."
    md5 b201d563… -> **f6c05cbb8a32aa74b56f13c3c44b88a3**. Minimap 2 map nướng lại.
- **Ảnh 1 / 3 — quái bất động = Grave Worm**: bộ hình Crypt không có hoạt ảnh bò (Move mượn Idle, tốc độ 0,45) -> đuổi mà trượt như tượng;
  thấy Ren bên kia hố nứt (nước không chặn tầm nhìn nhưng chặn đi) thì đứng im hẳn. Thanh máu 90/90 lơ lửng giữa miệng hố là của con sâu
  bên dưới (đỉnh đầu tính bằng bounds cả khung 128 px).
- Người dùng chọn "Đào hầm phục kích (Recommended)". **`WormSkills`** (IEnemySkills, kind Custom):
  - Ren trong tầm cắn: Attack như cũ (appear-atk: chui nhanh -> bật lên cắn, gồng 0,8 s).
  - Ngoài tầm (<= 4,5 đv, chỗ trồi trong 90% leash quanh hang): Retreat (chui xuống 0,6 s) -> tắt va chạm + thân + thanh máu -> đi ngầm
    1,5 đv/s (0,45–2,2 s), bụi `m3_dash_dust` nổi mỗi 0,22 s (không nổi trên hố) và bám theo Ren tới 60% đường -> trồi DƯỚI CHÂN Ren
    (chỗ đứng được gần nhất) -> đất rung 0,73 s (khung 0–15 của Attack, chưa đánh được) -> trồi, cắn trúng nếu khoảng hở <= 0,37.
  - Cắn xong đứng `cooldown` 1,4 s (cửa sổ đánh trả), đào lại sau 2,5 s. Counter: thấy vệt bụi / đất rung thì lăn hoặc bước ra.
  - Builder Map 3: thêm hoạt ảnh **Retreat** (retreat-32 frames, 20 fps), kind Custom, AddComponent<WormSkills>, aggroRange 3,2.
    `Definitions/Grave_Worm.asset`: kind 0 -> 6, aggroRange 2,6 -> 3,2 (asset thắng code). Prefab cũ chưa dựng lại: không có WormSkills ->
    Custom tự đuổi trượt như cũ (không lỗi).
- **Thanh máu quái thường** (`EnemyHealthBar`, chung): đỉnh đầu theo lưới Tight của khung hiện tại (đỉnh cao nhất của sprite.vertices, có
  cache) thay vì bounds cả khung -> thanh sát đầu (sprite khung lớn như Grave Worm không còn thanh lơ lửng).
- Script `Catalog/v2/tools/enemies/patch_worm.py`, sao lưu `backups/code_2026-11-08/worm/`. File mới `Scripts/Gameplay/Enemies/WormSkills.cs`.
- Người dùng cần: **Soul Requiem ▸ Enemies ▸ Build Map 3 Enemies**, rồi **Build Map 3 Scene + Map 3b Scene** (đọc lại .tmx).

## §191 Gỡ chiêu trùng của Ren — mỗi chiêu một vai, PixelLab hiệu ứng + icon mới (08/11 ~12h50) — đã biên dịch (05:59 UTC), chờ test
- Người dùng: phân tích loại bỏ chiêu thừa, tránh chiêu trùng như chuyên gia (được dùng PixelLab). Chọn "Làm lại, giữ đủ 10 chiêu (Recommended)".
- Phân tích (10 chiêu chủ động + 2 nội tại, theo vai sát thương / khống chế / phòng thủ / di chuyển / lộ sơ hở) — 4 chỗ trùng nặng + 1 nhẹ:
  1. Lộ sơ hở có 4 nguồn không cộng dồn: Flask Corroded 20%, Riposte + Hand Staggered 25% (boss), Wail Spent 30%. Trong bộ Vessel
     Hand + Wail cùng làm -> Hand mất tác dụng.
  2. Requiem Wail ≈ Earthshatter mạnh hơn (vòng quanh người + đẩy văng).
  3. Veil bất tử 3 s ≈ cú lăn dài 6 lần; xoá cái giá đứng yên của Judgement Ray.
  4. Grasping Roots trùng vai khống chế với Hand, yếu nhất, vô dụng với boss.
  5. (nhẹ) Riposte và Turned Blade cùng "đỡ đúng nhịp" với đạn.
- Sửa (code + asset, `Catalog/v2/tools/skills_m2/rework191/patch_rework.py` + `assets_rework.py`, sao lưu `backups/code_2026-11-08/rework191/`):
  - **Riposte**: bỏ Staggered lên boss -> phản đòn ×1,5 lên kẻ miễn khống chế (90). Thế đỡ CHỈ chặn đòn cận chiến:
    `Health.IncomingRanged` (Projectile bật quanh TakeDamage lên người chơi).
  - **Grasping Roots**: 24 -> 30 sát thương; mọi quái trúng (cả boss) mang dấu **Bound** 2 s (`SkillKit.Bind`, ảnh `sk_bound` quấn chân,
    cỡ theo collider quái); quái thường vẫn bị trói 1,4 s. Icon mới `ic_vessel_roots_bind`.
  - **Hand of the Vessel**: bỏ Staggered lên boss -> mục tiêu Bound ăn +50% (75 -> 112). Combo Roots -> Hand.
  - **Requiem Wail**: bỏ đẩy văng -> **KÉO** mọi quái trong 2 đv về cách chân Ren 0,45 (`SkillKit.Pull`: v = căn(2·hãm·quãng), boss không
    bị kéo) + 45 + Spent + hút máu. Xoáy hồn `sk_wail_pull` thu dần về chân trong 0,8 s niệm (lăn huỷ thì tắt xoáy). Icon `ic_vessel_wail_pull`.
  - **Veil of the Ascended**: bỏ bất tử -> 4 s đòn trúng còn 50% (`Health.Guard(mul, s)`, `Guarded`) + Judgement Ray cho lướt 40% tốc độ khi có
    màn (`JudgementRaySkill.veiledMove` -> `PlayerCombat.LockedMoveMul`, PlayerController nhân input khi đang khoá). Hồi 20 -> 16 s.
    Ảnh màn Ether `sk_veil` (sau lưng Ren, lặp); thiếu ảnh thì vòng EtherVfx cũ. Icon `ic_mantra_veil_ascend`.
  - Bộ Vessel: "kéo — trói — nghiền", không khiên. Mô tả (tiếng Anh) 6 asset cập nhật; setDescription Vessel mới.
  - Giữ nguyên: Earthshatter, Severing Draw, Ichor Flask, Seeker Embers, Judgement Ray (chỉ thêm lướt khi có Veil), 2 nội tại.
- PixelLab (My effects): sk_wail_pull 538e8cec…, sk_bound e592c31e…, sk_veil 0127bb3d…; ảnh dải ở `Resources/VFX`, meta bằng `vfx_meta.py`
  với `tools/skills_m2/vfx_manifest_191.json` (sk_wail_pull 4114 px -> maxTextureSize 8192). Icon create_image_pro 64x64 (style = icon
  §183), chọn wail #1 / roots #10 / veil #2, nền trắng ngoài khung -> trong suốt (4..60 như icon cũ). Icon cũ giữ nguyên (không xoá).
- ARCHITECTURE 2.11: thêm bảng vai "mỗi chiêu một vai".

## §192 Map 4 – The Catacombs of Naming: tài liệu thiết kế + BẢN ĐỒ THÔ (08/11 ~13h20) — chờ biên dịch + dựng scene + chạy thử
- Người dùng: phân tích làm Map 4 như chuyên gia, tạo khung và thiết kế. Chốt: "Giữ §148, cập nhật", "Mất Mắt, nhận bộ mới",
  "1 map, hub + 3 nhánh", "Tài liệu + bản đồ thô". (Tài liệu §148 cũ 6QfPwrPpEcgZxMes4SwX1u không đọc được nữa — viết lại.)
- **Tài liệu (Claude Docs):** https://claude.ai/artifact/AanV3WpvUS4FwNsFEZiGyi — tóm tắt, cốt truyện (Candidate VII, Malric, Absalom,
  The Severing, cờ name_reclaimed / parents_heard / candidate_vi_freed), sơ đồ luồng đi, bảng khu + ải, 5 hệ thống mới (phong ấn
  Mantra, NoiseSystem, trap, MirrorSkills, lựa chọn), quái + 6 boss (Hesk, Archivist, Ward Mother, Drowned Cantor, Ossuary Maw,
  Candidate VI + bảng nhái chiêu), câu đố P1–P6, bộ Mantras of the Name (Name-Step / Echo of Seven / Unspoken), hình ảnh, lộ trình 8 bước.
- **Bản đồ thô** `Old Prison V1.7.1/TiledMap Editor New/Maps/map4_catacombs.tmx` (112x112, md5 **7ddaf2fcea3138009621af9146ed1ac3**),
  sinh bằng `Catalog/v2/maps/map4_greybox.py` (--force khi chạy lại, tự sao lưu `backups/map4_<ngày>/greybox/`):
  - Lát 4 ô nền: Old Prison đá (Gaol), Crypt xanh ngọc (Ossuary), Crypt tím (Hall), ô tối cho chỗ không đi được / vũng Ether.
  - 11 khu (g1 g2 a1 a2 b1 b2 c1 c2 h1 h2 x1), 6 ải (cells, warden, archive, wards, drowned, naming), 7 checkpoint (6 đầu ải + hub),
    12 cổng (2 kéo cần: cell7_lock, loose_bones; 5 khoá đấu trường; seal_gate cần a2/b2/c2_cleared), 9 biển, 43 quái Map 3 dùng tạm
    (thuộc tính `placeholder_for` = quái thật), lối ra `to_map5` (map5_forge, interact "Descend", cần h2_cleared).
  - Thuộc tính map: flag_group catacombs_of_naming, greybox, sealed_slots 3, unseal_flags (chưa có code đọc).
  - Tự kiểm tra `Catalog/v2/maps/map4_check.py`: ĐẠT (8.084 ô đi được, mọi khu / quái / trigger tới được từ Cell VII, không trigger
    nằm trong va chạm, quái đều trong khu). Ảnh `Catalog/previews/map4_greybox.png`.
- Nhiệm vụ `Resources/Quests/Map4_CatacombsOfNaming.json` (7 bước, `map4_quests.py`); minimap đã nướng (minimap_bake thêm Map 4).
- Code (`Catalog/v2/maps/map4_patch_code.py`, sao lưu `backups/code_2026-11-08/map4/`): SceneTransition `map4_catacombs` ->
  `Map4_CatacombsOfNaming` + tiêu đề; `Map2SceneBuilder.BuildMap4` (chép scene Map 3, sửa import ảnh 2 gói, nền 0,5, camera đen);
  menu **Soul Requiem ▸ Scenes ▸ Build Map 4 Scene (Catacombs - greybox)** + trong Build All.
- Người dùng cần: Build Map 4 Scene (sau khi đã có scene Map 3), rồi đi từ Requiem Door hoặc mở thẳng scene Map 4 để chạy thử luồng đi.

## §193 Map 4: phân tích quái vật (08/11 ~14h10) — tài liệu, chưa code
- Người dùng: phân tích quái vật và những thứ liên quan để tạo Map 4. Kết quả: tab **Quái vật** trong tài liệu Map 4
  (https://claude.ai/artifact/AanV3WpvUS4FwNsFEZiGyi#9eb12f82-b3e7).
- Kiểm kê hình (đếm khung thật): Old Prison — Skeleton 1 (kiếm / kiếm gỉ / có khiên: đỡ 8, rơi khiên 21), Mage Skeleton (niệm 3 đoạn,
  bụi 16, đạn 11 + vỡ 12; khung 220x220), Assassin (ma cà rồng dao chặt: chạy 13, nhảy chém 25; khung ~110x96); Crypt — Skeleton,
  Spider, Big Worm 1/2; **Crawling Depths** — Amalgam (6 khung 96x64), Eye (28, 32x32), Mouth (18, 64x64), Tentacle (6, 48x64),
  Eldritch Entity (2 ảnh đứng 160x208). Loại: 2D Pixel Dungeon (chỉ idle 16 px), Tiny Swords Enemy (chibi, chỉ dự phòng).
- Dàn quái: 12 quái thường, 4 phe theo giác quan (Choir nhìn / xác giữ ngục nghe / Hollow canh chỗ / sâu bọ phục kích).
  Mới: Shieldbearer (GuardSkills chặn phía trước), Silencer (khoá 1 ô chiêu 6 s — dùng API phong ấn Mantra), Amalgam (vồ giữ, chết tách
  2 Remnant), Wall Eye (canh hình nón, rú gọi quái), Mouth Trap (bẫy sàn). Tái dụng: Melee, Ranged, LungeSkills (Ghoul Turnkey),
  WormSkills (Tentacle lặn Ether), HealerSkills, quân Choir + sâu bọ Map 3.
- Boss: chỉ Hesk, Ward Mother, Drowned Cantor cần PixelLab (~350 lượt cả chân dung); Archivist = Mage Skeleton x1,6 đổi màu;
  Ossuary Maw = Mouth + Eye; Candidate VI = đổi màu bộ hình Ren.
- Phân bố theo ải có "điểm đe doạ" (cells 8, warden 12, archive 22, wards 24, drowned 21, naming 22); độ khó theo số neo đã phá =
  thêm quái qua `spawn_when` (cờ seals_1 / seals_2), không tăng máu.
- Thứ tự làm đề xuất: NoiseSystem + phong ấn Mantra (+ Health chặn theo hướng) -> Map4EnemyBuilder (6 quái Old Prison / Crypt) ->
  5 quái Crawling Depths + Silencer -> Hesk -> 3 boss nhánh -> Candidate VI + Ossuary Maw.

## §194 Map 4 dựng bằng art gói Crypt (08/11 ~14h50) — chờ biên dịch + Build Map 4 Scene + chạy thử
- Người dùng: "bắt đầu toạ map đi, thiết kế chuẩn style của crypt". Thay bản đồ thô §192 bằng map thật cùng bố cục / logic.
- Bộ dựng mới `Catalog/v2/cryptkit.py` (`CryptKit(Kit)`, giống `CemKit`): nền ground1 + mảng ground2, đất xương (ô ít xương có trọng số cao
  hơn; mặt nạ loang ra 1 ô để mép chuyển nằm dưới tường), bệ viền hoa văn (`slope-transparency`), thảm tím (`path-transparency`),
  vũng Ether hữu cơ (SR-ichor của gói Cemetery), tường `wall-111` + 2 luật automap của tác giả, cửa trang trí 3x3 dán lên mặt tường
  (`STAMPS`: song sắt, vòm, cửa lớn, cửa sổ), lan can `balluster3` theo ô kề + luật dead-ends, đuốc / nến động (dải ảnh -> SR-anim, có đèn).
  Quy tắc tường: khối = ô tường có 2 ô dưới cũng là tường -> 2 hàng tường sát trên sàn thành mặt gạch. Phòng chồng dọc cách >= 4 hàng,
  vách đứng >= 2 cột (`check_walls()` báo chỗ sai); vách mỏng / cột 2x2 của bản thô đổi thành lan can, tượng, giá sách...
- Dữ liệu bố cục tách ra `Catalog/v2/maps/map4_layout.py`; dựng: `python3 Catalog/v2/maps/map4_crypt.py [--force]` (có file -> phải --force,
  sao lưu `Catalog/backups/map4_<ngày>/crypt/`). Đổi so với §192: cell7 lùi 1 cột; a1/a2/b1/b2 cao 15, c1 13, c2 15, x1 8 (khe 4 hàng);
  cổng a2/b2/c2_arena dời theo hành lang mới (hàng 55 / 55 / 91).
- Trang trí theo khu: Cell Block (vách buồng = lan can, cửa song sắt trên tường, giường, chỗ gác giữa phòng), Warden's Round (6 tượng + đèn
  nến, bệ đấu Hesk, xà lim Malric bằng lan can), hub (vòng thảm quanh hố sập, 2 tượng), Archive (dãy bục giảng + ghế, sách), Scriptorium,
  Wards (buồng lan can + quan tài dọc làm giường), Ward Mother (tượng, quan tài), Bone Sluice / Drowned Ossuary (đất xương, vũng Ether,
  quan tài mở nắp), Antechamber (3 thánh giá neo xiềng), Hall of Naming (bàn thờ = tượng Absalom + sổ tên, ghế hai bên, 4 tượng), Maw.
  Cổng đóng = hàng bẫy gai. 54 đèn (đuốc / nến) + đèn khu + đèn checkpoint.
- File: `Assets/GamePackage/EPIC RPG World Pack - Crypt V.1.6/TiledMap Editor/Maps/map4_catacombs.tmx` (+ .meta sortingMode 1),
  md5 `dbbfe69e97078ea1cd8f47783e6d2191`; tileset mới trong gói Crypt: SR-anim(T)-animated-torch/candle-burning.tsx, SR-props-trimmed.tsx,
  ảnh cắt `Props/SR-trimmed*/`. Bản thô §192 (gói Old Prison) giữ làm tham chiếu, không còn dùng.
- Code: `Map2SceneBuilder.Map4Path` -> gói Crypt, `FixPixelTextures(Crypt/Props)`, nền 0,5 -> 0,3; menu "Build Map 4 Scene (Catacombs)";
  `minimap_bake` + `map4_check.py` (nhận đường dẫn) trỏ map mới (script `Catalog/v2/tools/map4/patch_crypt194.py`).
- Kiểm tra: `map4_check.py` ĐẠT (7.670 ô tới được, mọi khu / quái / trigger tới được); minimap đã nướng lại; ảnh `Catalog/previews/map4_crypt.png`.

## §195 Map 4: phân tích style + kho asset Old Prison / Crypt cho bẫy và cơ chế (08/11 ~15h40) — tài liệu, chưa code
- Người dùng gửi 2 trang itch.io (Old Prison, Crypt): phân tích style trước khi làm, map cần khu kích hoạt bẫy và nhiều cơ chế,
  tận dụng tối đa asset. Kết quả: tab **Asset & bẫy** trong tài liệu Map 4 (https://claude.ai/artifact/AanV3WpvUS4FwNsFEZiGyi#ec955162-3fdb).
- Style (trang + mockup trong gói): Old Prison = đá xám-xanh, lửa ma XANH LAM, vực đỏ đầy sọ, xà lim tường mỏng (autotile kiểu cạnh),
  phòng nhỏ có chức năng; Crypt = đá teal, nến vàng thành cụm, bệ nổi viền hoa văn + lan can + bậc, bố cục đối xứng nghi lễ.
  Map §194 lệch: phòng quá to / trống, thiếu bệ + lan can + bậc, đồ rải đều, khu ngục dùng đá Crypt.
- Kho bẫy (đếm khung): gai CR 9 khung x4 màu + gai / vũng máu autotile OP (tileset động), sàn sập CR (1x1 19, 2x2 21 khung, dải 8),
  lưỡi cưa CR 4 khung + rãnh, lưỡi xoay OP 2 khung 128 + ray 16 mảnh, khúc gỗ lăn OP (nắp 9 + lăn 11, ngang / dọc), máy bắn tên CR
  (11+4+6+5), tượng mặt phun lửa CR (6/9/5), lỗ tường lửa / băng CR (x3 mỗi loại), đạn fireball / ice / flame-energy + vỡ,
  switch sàn / tường CR 4 khung, gear switch OP 12+11, cầu treo OP 15 khung, cửa CR 5 khung x gỗ / sắt (9 khung + dịch chuyển 16),
  boss door 40, prison gate OP 18, cell gate OP 23, bàn lật OP (7-8 khung + 3 mức vỡ), rương đá 16 / gỗ 11, gián 8 khung.
- Đối chiếu code: đã có spout / hazard / vent / lever / breakable / chest / passage / toggle / gate theo cờ / Projectile;
  thiếu vùng kích bẫy (`plate`) và bẫy chuyển động.
- Đề xuất 17 cơ chế quanh hệ `plate` (xung cờ `fires`, bẫy nghe `trigger`; quái cũng kích -> bẫy thành vũ khí của Ren),
  phân bổ theo khu, làm 3 đợt (nền: plate, gai, cổng hoạt ảnh, cần gạt; chủ động: launcher, cover, roller, blade;
  địa hình: crumble + ô an toàn, cầu treo, quan tài, rune). Đuốc tắt để sau cùng NoiseSystem.
- Ảnh tham khảo: `Catalog/_preview_m4/style/` (op_a/op_b/cr_a/cr_b = mockup, traps_strips.png = dải bẫy).
- Người dùng đã chốt (cả 4 theo đề xuất): khu ngục art Old Prison + hầm mộ Crypt; đủ 3 đợt bẫy; dựng lại bố cục v2 theo style
  mockup (giữ luồng đi / ải / cổng / cờ §192); tấm sàn pha trộn (đầu map hiện rõ, về sau ẩn có dấu hiệu). Bước tiếp: đợt 1 + prisonkit.

## §196 Map 4 đợt 1 bẫy: vùng kích bẫy, gai hoạt ảnh, cổng hoạt ảnh, cần quay bánh răng (08/11 ~16h05) — đã biên dịch (09:33 UTC, không lỗi), chờ test
- Người dùng: "làm đợt 1" (theo tab Asset & bẫy §195).
- Ảnh: `Catalog/v2/tools/traps196/slice_traps.py` cắt 15 dải khung vào `Resources/VFX/` (Sprite Multiple, pivot dưới-trái, cắt chung
  dải trống, maxTextureSize 8192): trap_spike1-4 (9 khung), trap_plate_h / _v / _wall (4), trap_gear (12), gate_metal_h (8) / _v (7),
  gate_wood_h (9) / _v (8), gate_cell1 (23), gate_prison (18), gate_boss (40). Khung 0 (đóng) cho Tiled: `<Crypt>/Props/SR-traps/` +
  tileset `SR-traps.tsx`.
- Code mới (Scripts/Gameplay/World):
  - `Mechanics/TrapSignals.cs`: xung tín hiệu không lưu (`fires` -> `trigger` / `disable_on`); bẫy HỎI bằng `TrapListener.Poll()`
    (không đăng ký sự kiện tĩnh -> không rò khi đổi scene).
  - `Mechanics/TrapArt.cs`: nạp khung theo số đuôi, SpriteRenderer cùng lớp nhân vật, tìm quái trong vùng (ContactFilter2D),
    gây sát thương bẫy cho người chơi như đòn xa (Riposte không đỡ, Veil vẫn giảm).
  - `Hazards/PlateMechanism.cs` (`plate`): by any / player / enemy, delay, rearm, once, sets, start_when, sprite (trống = tấm ẩn).
  - `Hazards/SpikeMechanism.cs` (`spikes`): lát ô 32 px, mode cycle / step / trigger, wave_step + wave_axis (sóng), damage,
    enemy_damage, knock, style 1-4, disable_on + disable_seconds, start_when, stop_when. Trúng cả quái.
  - `Encounters/GateAnimator.cs`: gate_visual có `anim` -> chạy khung đóng / mở (khớp mép trên-trái vì trim_props xén chân hình);
    1 giây đầu sau khi nạp scene đặt ngay. Sửa `GateController.SetClosed` (hình có hoạt ảnh luôn hiện) + `MapLayout` (gắn GateAnimator).
  - `LeverMechanism`: `sprite` (trap_gear / trap_plate_wall), `fires`, `rearm` (gạt lại được, cần quay về), có `fires` mà không ghi
    `sets` thì không bật cờ. Registry: `plate`, `spikes`. Sao lưu: `Catalog/backups/196/`.
- Map 4 (`map4_layout.py` thêm `GATE_ART` + `TRAPS`; `cryptkit` thêm tileset SR-traps; `map4_crypt.py` đặt hình cổng + cơ quan):
  - Cổng hoạt ảnh: cell7_door (cửa gỗ OP), g1_door / g2_arena (2 cửa sắt dọc chồng), g2_stairs (2 cổng đầu lâu OP), a2 / b2 / c2_arena,
    h1_door, h2_arena (2 cửa sắt ngang), seal_gate (2 cửa boss), x1_door (cửa xà lim kéo lên). Cổng khác (malric_door) vẫn hàng gai.
  - Bẫy: Cell Block dạy (tấm sàn hiện rõ g1_plate -> sóng gai g1_wave; dải gai quét g1_sweep); Warden's Round 2 sân gai nhịp + cần
    quay bánh răng g2_gear tắt gai 10 s (nạp 20 s); Archive 2 ô gai giẫm ẩn giữa kệ (xương làm dấu hiệu); Ward Mother dải gai quét;
    Antechamber 2 tấm sàn ẩn -> gai bật cả vùng 6x3 sau 0,5 s.
  - md5 `58916edf99c8325a3d41bb7d96a50ab7`, map4_check ĐẠT (7.670 ô tới được), minimap nướng lại.
- Đổi kế hoạch: bộ dựng ngục `prisonkit` (art Old Prison) làm CÙNG bố cục v2 — làm bây giờ trên bố cục cũ sẽ phải dựng khu ngục 2 lần.

## §197 Map 4 đợt 2 bẫy: máy bắn, bàn lật, khúc gỗ lăn, lưỡi cưa / lưỡi xoay (08/11 ~16h45) — chờ biên dịch + Build Map 4 Scene + test
- Người dùng: "làm tiếp đợt 2".
- Ảnh: `Catalog/v2/tools/traps196/slice_traps2.py` -> 27 dải trong `Resources/VFX/` (pivot giữa cho đạn / lưỡi, giữa-dưới cho vật đứng):
  trap_arrow_prep (11) / _launch (4) / _fly (6, xoay 90° cho mũi tên chỉ sang phải) / _stick (5), trap_face_fire (9), trap_hole_fire (9),
  trap_hole_ice (11), trap_fireball (6) + _end (10), trap_ice (12) + _end (8), trap_table_up (8) + _break (3), trap_hatch (9),
  trap_log_h (11, 192x96) / _v (11, 96x160), trap_saw (4) / _v (3), trap_saw_trail (_v), trap_blades (2, 128x128), trap_rail_lr/tb/l/r/t/b.
- Code mới:
  - `Hazards/LauncherMechanism.cs` (`launcher`): kind arrow / fire / fire_hole / ice, dir, mode cycle (period, offset) | trigger,
    warn_seconds, speed (ô/giây), damage, range, burst + burst_gap, burn_dps / burn_seconds, chill / chill_seconds, disable_on, start_when.
    Đặt vùng = ô mặt tường; đạn ra từ mép vùng. Đạn trúng cả quái.
  - `Puzzles/CoverMechanism.cs` (`cover`): bàn đứng = va chạm thấp (đạn bay qua, `TrapLowCover.cs`); T "Flip" -> chắn đạn; `hits` (6)
    phát thì vỡ 3 mức rồi hết chặn (đếm đạn nổ trong vùng qua `Projectile.Live`).
  - `Hazards/RollerMechanism.cs` (`roller`): làn dọc = nắp hầm mở 0,65 s rồi gỗ ngang lăn; làn ngang = gỗ dọc; trigger và/hoặc period,
    speed, damage (22), enemy_damage (80), knock, rearm. Mỗi lượt trúng mỗi mục tiêu một lần.
  - `Hazards/BladeMechanism.cs` (`blade`): kind saw (rãnh sàn) | blades (ray sắt OP tự vẽ theo ô); đi lại giữa hai đầu (SmoothStep),
    pause, offset, damage, enemy_damage, knock, disable_on.
  - `Projectile`: `hitsEnemies`, `burnDps/burnSeconds`, `chillMul/chillSeconds`; bỏ qua collider có `TrapLowCover`.
    `Health.Chill(mul, s)` + `MoveMul`; `PlayerController` nhân input với `MoveMul`. Quái chưa bị chậm (chỉ mất máu / cháy).
  - Registry: launcher, cover, roller, blade. Sao lưu `Catalog/backups/197/`.
- Map 4 (thêm vào `map4_layout.TRAPS`; torch chừa ô máy bắn; `map4_check.py` bỏ qua máy bắn trong tường):
  Cell Block: tấm sàn hiện rõ g1_log_plate -> gỗ lăn làn tây (14..19, hàng 13-29) (tấm sàn sóng gai dời sang x24 / gai x25-26);
  Warden's Round: 4 lưỡi xoay chạy ray mép bắc / nam, cần bánh răng tắt cả gai lẫn lưỡi 10 s; Archive: 3 lỗ bắn tên bắn dọc 3 khe
  giữa kệ (so le 0,9 s) + 2 bàn đọc lật được; Bone Sluice: 2 lưỡi cưa (ngang hàng 78, dọc x34); Drowned Ossuary: 2 ống băng;
  Antechamber: 3 đầu tượng phun lửa so le + 2 bàn lật; Hall of Naming: lưỡi xoay ngang hàng 101; Maw: gỗ lăn ngang mỗi 6 s.
  md5 `7ac6104fffe3ec0077e0d74039141329`, map4_check ĐẠT (7.670 ô), minimap nướng lại.

## §198 Map 4: khu ngục bằng art Old Prison (prisonkit) + sửa lỗi biên dịch đợt 2 (08/11 ~19h40) — chờ biên dịch + Build Map 4 Scene + test
- Người dùng: "Tiếp tục làm bước tiếp theo" (bố cục v2 + prisonkit).
- Lỗi biên dịch §197: `LauncherMechanism.cs(63)` gọi `Props.GetBool` thiếu `using SoulRequiem.Core` (Editor.log: Tundra build failed)
  -> đã thêm. (Đợt 1 §196 đã biên dịch 09:33 UTC không lỗi.)
- Học cách tác giả ghép (`Old Prison example map.tmx`): sàn đi được = BỆ `wall-1` (mép bệ là vách 3 ô rơi xuống vực đỏ), tường phòng =
  khối `wall-2` đứng trên bệ, xà lim = tường mỏng: tường ngang cao 3 ô ghép từ các CỘT 3 ô của Tileset-Terrain-old prison
  (đầu cột hàng 14 / 17, mỗi ô dưới +65), cột dọc 1 ô (cột 30-31 hàng 17-20), song sắt 2 ô (hàng 17 cột 17-18). Wangset cạnh
  'prison walls' chỉ là ô đánh dấu cho luật automap (thử ra luật chỉ dựng được mối chữ T) -> dựng thẳng bằng cột ô.
- `Catalog/v2/prisonkit.py`: `attach(k)` (thêm tileset OP terrain / wall-1 / wall-2 / atlas-props-sprites, PropBook, chỉ mục ảnh,
  thứ tự lớp op platform / op dirt / op blood ... op walls / op cells), `h_wall` (tường ngang 3 ô có cửa / song sắt / trụ), `v_post`,
  `build_gaol(k)` (bệ phủ hàng < 35, bùn đất + vũng máu, wall-2 + luật tác giả, xoá lớp Crypt ở hàng ngục), `flame` (lửa ma xanh
  `Props/magic flame.png` 8 khung + đèn #7fa8ff), `pick` (tên ảnh OP theo tiền tố). Thử nghiệm: `Catalog/v2/prison/optest.py`.
- Map 4 (hàng < 35 = ngục): Cell Block = 5 buồng (cột dọc x20/27/34/41 + tường ngang hàng 11-13, cửa 2 ô, song sắt x25-26 / 39-40;
  bỏ lan can Crypt), ổ rơm, xô, xích treo; phòng gác 2 bàn + ghế + giấy tờ; kho đông thùng, hòm, đống vàng. Warden's Round: vũng máu
  méo giữa sân, 6 máy tra tấn (máy chém / iron maiden / giá) + chân nến OP thay tượng, 2 lồng treo, xà lim Malric = cột + tường ngang
  hàng 11-13, cửa 3 ô `malric_door` (104,11,3,3) hình cửa sắt hoạt ảnh. Cell 7: ổ rơm, xô, xích. Tường bắc khu ngục: lửa ma xanh + xích
  thay đuốc / cờ Crypt. Bẫy g1 dời xuống dưới tường buồng (roller 14,14,6,16; g1_wave 25,14,2,7; g1_sweep 29,14,12,2).
- `Map2SceneBuilder.BuildMap4`: thêm `FixPixelTextures(Map4Prison + "/Props")`.
- md5 `2ff75a1819cacea6bf694f0b151f08d4`, map4_check ĐẠT (7.604 ô), minimap nướng lại. Sao lưu `Catalog/backups/198/`.
- Còn của bố cục v2: phía hầm mộ (bệ nổi + lan can + bậc thang, hốc tránh bẫy, đồ dồn cụm), chia Warden's Round (sân rất rộng).

## §199 Bỏ màn hướng dẫn phản đòn (J) và dùng chiêu (F) (08/11 ~22h05) — đã biên dịch (15:06 UTC), chưa Play thử
- Lỗi người dùng báo: đánh boss, đạn bay tới là game dừng hiện TURN THE BLADE [J]; nhặt chiêu mới nào cũng dừng hiện NEW MANTRA [F].
  Nguyên nhân: `CombatTutorials` (MapEncounterDirector tự gắn) nhớ cờ `tut_parry` / `tut_mantra` THEO TỪNG MAP -> map nào cũng hiện lại.
- Đã bỏ: `CombatTutorials` thành lớp rỗng, MapEncounterDirector KHÔNG gắn nữa; `TutorialDirector` (Map 1) bỏ luôn màn MANTRA [F] khi bị bao vây.
  Giữ: DODGE [Q], HEAL [E], các màn ATTACK/DODGE của cảnh mở đầu, dòng chữ "Turned! ..." sau khi dội đạn thành công (TurnedBlade.cs).
- Cờ `new_mantra` vẫn được MapTriggerHandler bật nhưng không còn ai đọc (vô hại). Bản cũ: `Logs/claude_preview/backup_tutorials/`.

## §200 Sửa theo 12 ảnh: điểm hồi sinh Map 1/2, hồi sinh tại chỗ khi qua ải, cuộn chiêu trong sách, bỏ Riposte, giấy trên thùng, giá chìa + buồng mẫu vật, rương Odo vào túi đồ, laser đổi tay (09/11 ~01h45) — đã biên dịch (18:42 UTC, Console 0 lỗi, vào Play Map 3 sạch); CHƯA chơi thử Map 1/2 và laser
- **Luật mới (code, mọi map)**: qua ải -> `AnchorStageRespawn` đặt điểm hồi sinh tại chỗ Ren đứng yên ổn trong phòng vừa dọn (không mất máu 0,8 s,
  tối đa chờ 8 s) + lưu game; dòng "… trial complete … if you fall, you rise here." Gục ở ải sau: ải đó có lửa trại đã thắp -> về đó, không thì về chỗ này.
  `StageName` tìm cả trong stage_name kiểu "a=Tên;b=Tên" của checkpoint khác (ải không có lửa trại riêng).
- **Map 1** (`Catalog/v2/tools/fix_1109/map1_respawns.py`, md5 7d2dc225… -> **366b8e701ae94fd0078e5107a6a94975**, sao lưu `backups/map1_2026-11-09/respawns/`):
  bỏ camp_fire (ảnh 1, sát z2_fire) và ring_fire (ảnh 2, dày); khôi phục nguyên bộ z4_fire (lửa trại Ruins Path, ảnh 3) từ bản trước §187.
  Ải: brook z0,z1 -> z0_fire | mine z2,z2b -> z2_fire | camp z3 -> z2_fire (stage "mine,camp") | **ruins z4 (ải mới)** -> hồi sinh tại chỗ qua camp |
  ring z5 (Grok) -> z4_fire. Còn 3 lửa trại: Brookside, Hollow Caves, Ruins Path. Minimap bake lại.
- **Map 2** (`fix_1109/map2_fixes.py`, md5 dfdf66dc… -> **b1f3d2bd9802f1a773620138c2f28627**, sao lưu `backups/map2_2026-11-09/fixes/`; minimap bake lại):
  - Bỏ rest_hatch (ảnh 4): cột sáng nắp hầm = điểm hồi sinh mặc định; tên ải descent ghi trên hub_rest.
  - Cuộn chiêu (ảnh 5-6): bỏ 3 cuộn trên sàn / bàn + vòng vàng. Severing Draw = sách hồng trên kệ TRÁI, Ichor Flask = sách nâu trên kệ PHẢI của
    Forgotten Store (sách Turned Blade ở giữa). Sách ở `props front`, đèn vàng thở nhỏ, pickup đứng trước kệ (không vòng vàng). Cờ giữ nguyên.
  - Riposte (ảnh 7) bỏ hẳn: `Riposte.asset` + `RiposteSkill.cs` dời sang `_to_delete/2026-11-09/riposte/`. (tools/skills_m2/make_assets.py còn
    bảng Riposte — đừng chạy lại mà không bỏ dòng đó.) Health.ParryWindow vẫn còn, không ai gọi.
  - Giấy (ảnh 8): boil_brakk_note đặt lên nắp boil_note_crate; cistern_note thêm thùng `cistern_note_crate` bên dưới. Bỏ marker (dùng đốm).
    NoteMechanism mới `sort_at` "x,y" (SortingGroup ở chân thùng) để giấy vẽ đè thùng mà vẫn bị Ren che khi đứng trước.
  - Giá chìa (ảnh 9): `office_keyrack` (note, verb Take, pickup) -> **Cell Keys** (key_cells, icon `UI/Requiem/key_cells.png`), giá đổi sang hình rỗng
    (`office_keyrack_empty`, tileset mới `SR-animT-sr-extra2-sr-keyrack-empty.tsx` firstgid 14640). Specimen Cells: 5 khoá `cell_N_lock` (lever,
    verb Unlock, requires_key cell_keys): V-7 Vat_Born, G-2 Skinned_Hopper, V-3 Vat_Born, V-9 Vat_Born, G-5 Skinned_Hopper -> bình vỡ
    (`cell_N_tank_broken` gid 12566, toggle `cell_N_shatter`), đèn tắt, quái lao ra. Spawn spec_breakout tương ứng có `skip_when cell_N_open`.
    Code: LeverMechanism `requires_key`/`locked_say` + Done khi nạp bản lưu; director `skip_when` cho điểm sinh.
  - Rương Odo (ảnh 10): thêm key_* -> "Odo's Tonic" (Tool, icon `item_flask.png`, Flask capacity +1) vào túi đồ; vẫn +1 bình tối đa.
  - NoteMechanism: nhặt đồ có key_* -> "Obtained: … [Inventory]".
- **Laser (ảnh 11-12)**: hình ray-hold Bắc duỗi tay TRÁI (bên phải màn hình), Tây-Bắc tay PHẢI -> quét TB->B tay phát tia nhảy sang bên kia.
  `JudgementRaySkill`: hướng Bắc mà tia lệch trái (vùng trễ `northSwapDeadzone` 6°) thì flipX hình (quanh chân, pivot 0.4926) + đảo x tay;
  điểm phát tia trượt mượt (`handBlend` 28). RayPose(false) luôn trả flipX.
- Sao lưu code: `Catalog/backups/code_2026-11-09/fix_1109/`; script vá `Catalog/v2/tools/fix_1109/patch_code.py`.
- Chưa test: đi Map 1/2 thật (lửa trại, qua ải, giá chìa, mở bình, sách kệ, giấy trên thùng), laser (cần lắp bộ Mắt ở điểm nghỉ).
  Scene Map 3 đang mở có thay đổi chưa lưu (dấu *) — không đụng tới.

## §201 — 09/11: Ren v2 — thân tay không + lớp vũ khí, combo 3 nhát, bỏ Iaido
- **Vì sao**: phân tích animation cũ (Rogue Traveler): Iaido chém sai hướng (N/NW/NE đâm sang phải, S chém trái), chậm 1,35 s (trúng ở 0,48 s),
  tra kiếm mỗi nhát; Cast Veil mỗi hướng một phép khác; Dash 4 hướng x 4 khung; Death luôn ngã sang phải màn hình; nhiều động tác chỉ 4 hướng;
  kiếm vẽ dính vào mọi khung -> không đổi được vũ khí.
- **Thân mới**: PixelLab nhân vật `b16a3c0d-8854-47da-8522-979ab14fb3a7`, state "Ren Unarmed" (112 px, low top-down). 16 động tác x 8 hướng
  (skeleton-v3: 1 chuyển động 3D chiếu ra 8 hướng -> đồng nhất): idle(6), run, roll, attack1/2/3 (tư thế 3D tự viết: chém chéo / hất ngược /
  bổ dọc), hurt(6), stagger, stunned, death(10), getup, drink, cast_embers (đã xoá tia lửa vẽ dính), cast_veil, ray (2 khung giơ + 6 lặp), stomp.
  Ảnh: `Assets/GamePackage/Ren/<động tác>/<hướng>/<động tác>_<HƯỚNG>_<nn>.png`, pivot = chân thật theo tư thế đứng từng hướng.
  Nhập lại: tải zip -> `Catalog/_dev/ren2/x`, chạy `Catalog/v2/tools/ren2/ren_import.py <thư mục>` (giữ guid).
- **Vũ khí = lớp riêng** (`Scripts/Gameplay/Player/Weapons/`): `WeaponDefinition` (ScriptableObject, `Resources/Weapons/*.asset`: hình lưỡi chĩa +X,
  pivot ở tay cầm; số liệu từng nhát combo; màu vệt chém) + `PlayerWeaponView` (SpriteRenderer con, mỗi khung tra
  `Resources/Weapons/ren_weapon_anchors.json` theo TÊN sprite: chỗ cầm, góc, co ngắn `s`, trước/sau thân). Chỉ hiện lúc chém.
  Bảng neo sinh bằng `tools/ren2/anchors2.py` (cổ tay + vector lưỡi 3D trong `attacks.py` chiếu qua `proj.py`); xem thử `compose.py`.
  Có 2 vũ khí: **Katana** (16/16/26, nhanh — mặc định) và **Butcher Cleaver** (22/22/38, chậm, đẩy lùi mạnh, vệt cam).
  Đổi: gán asset vào `PlayerCombat.weapon` hoặc gọi `PlayerCombat.Equip(w)`. Vũ khí mới = 1 PNG + 1 asset, không vẽ lại thân.
- **Combo** (`PlayerMelee` viết lại): nhát 1-3 nối trong `comboWindow` sau khung huỷ; lăn huỷ combo; tốc độ clip theo `fps` từng nhát (AtkSpeed).
  `PlayerAnimation.Attack(step, dir, speed)` thay Iaido; Severing Draw dùng `PlaySlashPose`. Iaido bỏ hẳn.
- **Animator**: `Assets/GamePackage/Ren/Animations/Ren.controller` — menu Soul Requiem ▸ Player ▸ Build Ren Animator (v2) (Rebuild Player cũng dùng).
  Đã gán cho Player ở Map1/Map2/Map3/Map3b (Map3 qua menu; 3 scene kia sửa guid controller trong file .unity, bản cũ ở
  `Catalog/backups/code_2026-11-09/ren2/scenes/`). Controller cũ Rogue Traveler còn nguyên (menu "legacy Iaido").
- `PlayerFeetSorting` (Sort Player by Feet) BỎ QUA ảnh trong `GamePackage/Ren/` — trước đó nó ghi đè pivot về giữa khung làm vũ khí rời tay.
- Laser: `JudgementRaySkill.handPixels` đo lại trên khung ray mới (E,NE,N,NW,W,SW,S,SE). Lật hình hướng Bắc vẫn giữ (thân không kiếm, đối xứng).
- Sao lưu code: `Catalog/backups/code_2026-11-09/ren2/`. Đã play-test Map 3: chạy, combo nhát 1->2, katana gắn tay, vệt chém; 0 lỗi biên dịch.
  Chưa test kỹ: nhát 3, Cleaver, laser với hình mới, chết/hồi sinh. Unity tắt ngang giữa chừng lúc 07:4x — thay đổi chưa lưu cũ của scene Map 3 đã mất.

## §202 — 09/11: Ren CẦM KATANA THẬT + đổi thiết kế katana / bao kiếm (UV-map)
- **Vì sao bỏ lớp kiếm ghép §201**: đo 192 khung chém — chỗ cầm lệch tay > 4 px ở 109 khung (TB ~10 px nhát 1/3), thân vẽ để đấm tay không
  (không tay thứ 2 trên chuôi), chuôi đè lên nắm tay, ~nửa số khung kiếm khuất / co ngắn, lưỡi 2 px viền cứng xoay bằng code -> "dính vào tay".
- **Thân mới**: PixelLab `f8705d6f-5133-4c6f-b1a4-db65b1cb77dc` (state "Ren Katana Saya v2", 128 px): katana cầm tay phải, BAO KIẾM rỗng ở hông trái
  mọi hướng. Vẽ bằng palette có **màu khoá** chỉ kiếm/bao dùng: lưỡi #5c6478 #9aaccb #f0f6ff, tsuba #9a7430 #e0bc58, chuôi #1d2c6e #3c56b4,
  bao #1f5a38 #4f9a62 (+ mắt #e2d6cc). 16 động tác x 8 hướng: combo 2 tay = attack1 kesa (chéo) / attack2 hất ngược / attack3 quét ngang
  (tư thế 3D: `Catalog/v2/tools/ren2/katana_clips.py` — khuỷu tay đặt NGƯỢC hướng lưỡi để lưỡi đúng hướng, giơ kiếm cạnh đầu không che mặt);
  idle/run/roll/hurt/stagger/stunned/death/getup; drink + cast_embers + ray dùng TAY TRÁI (tay phải giữ kiếm); cast_veil; stomp = cắm kiếm xuống đất.
- **UV-map katana**: `ren_import.py` (gọi `katana_uv.bake`) với mỗi khung ghi ảnh phụ `GamePackage/Ren/_uv/.../<tên>_kuv.png` (Sprite Secondary
  Texture `_KatanaUV`, linear, point): phần (lưỡi/tsuba/chuôi/bao), vị trí dọc, hàng ngang, bóng. Shader `Assets/Shaders/Sprite-Lit-Katana.shader`
  (material `Resources/Weapons/KatanaBody.mat`, PlayerWeaponView gán lúc chạy) tra **bảng thiết kế 64x32** của thanh đang cầm:
  hàng 0-7 lưỡi (gáy->cạnh: hamon, chữ khắc), 8-15 tsuba (tâm->vành, theo góc: khoét/hoa văn), 16-23 chuôi (quấn ito / hình thoi), 24-29 sơn bao,
  30 dây sageo, 31 đồng bịt bao. Alpha < 255 = phát sáng (nhân `glow`, loé khi vung `swingGlowBoost`).
- **Thanh katana** = `Resources/Weapons/<id>.asset` (WeaponDefinition): `design` (bảng 64x32, sinh từ `designs.json` bằng `katana_design.py`
  hoặc vẽ tay), `showcase` (ảnh trưng bày 256x96 — PixelLab, `Resources/Weapons/Showcase/`), `glowTint`, vệt chém, số liệu combo.
  Có 3: Ronin's Katana (mặc định), Moonlit Edge (nhanh, hamon xanh phát sáng), Crimson Oath (nặng, rồng khắc vàng). Thêm thanh mới = 1 bảng
  thiết kế + 1 ảnh trưng bày + 1 asset, KHÔNG vẽ lại animation. Đổi: `PlayerCombat.Equip(asset)`; test: **F10** (DevCheats) vòng qua các thanh.
- Laser: tay TRÁI (`JudgementRaySkill.handPixels` đo lại); hình Bắc lật khi tia lệch PHẢI.
- Đã kiểm trong Map 3: thân mới + đổi 3 thanh bằng F10 + combo; 0 lỗi. Lưu ý commit file qua bridge: cùng stagedPath bị lưu đệm -> đặt tên mới.
- Chưa: vài hướng PixelLab vẽ kém (NW hay khuất kiếm, attack3 E có khung 2 lưỡi) — vẽ lại riêng hướng đó nếu thấy chướng; UI túi đồ chưa hiển thị
  ảnh trưng bày (`WeaponDefinition.showcase` đã sẵn); các nguồn nhặt katana trong map chưa có.
- **§202b (09/11) — lệch trọng tâm**: pivot cũ = giữa 6 hàng điểm ảnh thấp nhất của tư thế đứng -> với Ren cầm katana, mũi kiếm / đầu bao chạm
  hàng đó: hướng Nam pivot ở x=9 (thân ở x=61), Đông-Nam 79, Tây-Nam 44 ... -> đổi hướng là nhân vật nhảy ngang tới 50 px.
  Sửa: `Catalog/v2/tools/ren2/feet.py` — bỏ màu khoá kiếm/bao + viền của chúng, lấy cụm thân lớn nhất: pivot x = TÂM THÂN, y = đáy chân
  (8 hướng nay x 60.9-62.4 trên canvas 128). Nhập lại toàn bộ (giữ guid). `JudgementRaySkill.handPixels` đo lại theo pivot mới.
  Kiểm: chạy vòng Đ-N-T-B trong Map 3, thân giữ đúng tâm; ảnh so sánh Catalog… (chay_vong_tron.gif).
- **09/11 15:40**: bàn giao đầy đủ ở `HANDOFF_REN_KATANA.md` (gốc project). Việc dở ưu tiên 1: sửa lưỡi kiếm ở 3 nhát chém (xem mục 3 file đó). Script soát lỗi `Catalog/v2/tools/ren2/qa/` đang trỏ đường dẫn container (/tmp/rk2, /home/claude/ren/uv) — sửa sang `~/mnt/Catalog/_dev/ren2/k` + `~/mnt/Catalog/v2/tools/ren2` khi chạy trên máy.

## §203 — 09/11: Sửa 3 nhát chém của Ren — vẽ lại katana bằng code theo đường chém 3D + vệt chém (smear) (09/11 ~16h20) — đã nhập vào Unity, CHƯA Play thử
- **Vì sao**: soát tay từng khung (192 khung) thấy lỗi nhiều hơn bản QA tự động: ngoài 62 khung bị cờ, phần lớn các ô còn lại lưỡi
  đứng yên (dựng đứng / kẹt sau đầu) suốt nhát chém, không đọc ra đường chém. PixelLab (skeleton-v3) bỏ qua hướng lưỡi trong tư thế 3D.
- **Cách sửa (khung 0-6 của cả 24 ô; khung 7 = về thế thủ, giữ nguyên PixelLab)**: `Catalog/v2/tools/ren2/fix_atk/`
  - `strip.py`: gỡ lưỡi + chuôi + tsuba PixelLab (màu khoá + màu xám / mắt dính lưỡi + viền), mòn gai mỏng, vá lỗ bằng màu lân cận,
    bỏ mảnh rời nhỏ (< 120 px, trừ bao). Đoạn "que" vẽ bằng màu da / bao cầm như kiếm: xoá theo đoạn thẳng (`grips.py` ERASE).
  - `grips.py`: điểm cầm (tay) từng khung, đọc tay trên bảng soi `rv/s_<nhát>_<hướng>.png` (ứng viên cụm da đánh số).
    Bao kiếm bị giơ / nhân đôi / cầm như vũ khí: thay bằng bao của khung tham chiếu, dời theo hông (`SAYA`).
  - `build.py`: hướng lưỡi từng khung = vector 3D (phải, trước, lên) `B3` theo nhịp mới (0-2 vung, 3-4 chém, 5 dư lực, 6 thu),
    chiếu xuống 8 hướng (low top-down: lên x1.107, bắc x0.339) -> góc + độ co ngắn (dài gốc 50 px), sau thân nếu chĩa ra xa camera
    hoặc giơ sau đầu lúc vung. Cạnh lưỡi quay theo chiều chém.
  - `katdraw.py`: vẽ lưỡi (3 px, màu khoá lưỡi, cong nhẹ, mũi kissaki, habaki đồng), tsuba, chuôi quấn; tay vẽ đè lên chuôi;
    vệt chém hình lưỡi liềm (3 dải trắng-xanh, bán trong suốt) ở khung 3 và 4, nội suy 3D giữa hai khung.
  - Màu khoá -> `katana_uv.bake` tự tách UV: hoa văn katana (Moonlit / Crimson) vẫn áp lên lưỡi mới (đã thử Crimson).
- Nhập: `ren_import.py ~/mnt/Catalog/_dev/ren2/k_fix` (chỉ 3 nhát) — giữ guid 192 sprite + 192 ảnh _KatanaUV, pivot không đổi,
  không cần dựng lại Animator. Sao lưu bản cũ: `Catalog/backups/ren_atk_2026-11-09/` (PNG + meta + _uv). `hitFrame` giữ 3 (khớp vệt chém khung 3).
- QA (`qa/atk_qa.py`, `REN_SRC=<thư mục>`): còn 28 cờ, đều là chủ ý: lưỡi co ngắn khi chĩa vào / ra xa camera, lưỡi khuất sau lưng
  ở hướng N/NE/NW (vệt chém vẫn thấy), lưỡi dựng ở khung 3 (giữa nhát chém từ trên xuống). Ảnh so sánh trước/sau: `Catalog/_dev/ren2/fix/*.gif`.
- Còn: vệt chém trong sprite màu trắng-xanh cố định (chưa nhuộm theo `trailColor` của từng thanh); có thể hơi trùng với VFX vệt chém lúc chạy
  (PlayerMelee) -> nếu thấy dày quá thì giảm `trailScale` hoặc bỏ VFX ở nhát nào.
- Sửa công cụ: `qa/atk_qa.py`, `qa/atksheet.py`, `qa/defect_map.py` trỏ đường dẫn máy (`REN_SRC`, `QA_OUT`).
- **§203b (09/11 ~17h00) — soát lại từng khung (24 ô × 8 khung, phóng to) và sửa các hướng sai**:
  - Nhát 1 (kesa): khung chém 3 đổi tư thế lưỡi (vung chéo từ vai, không dựng đứng che mặt) -> S/SE/SW/W đọc ra đường chéo;
    E/W khung 0-2: lưỡi giơ sau đầu chĩa ngược ra sau (trước đó lưỡi chọc xuyên mặt / mũi kiếm nhú như sừng) — `THETA_OVR`.
  - Nhát 2 (gyaku-kesa): khung 3 cũ thu lưỡi thành 3 px (chĩa thẳng vào camera) -> vector mới lệch ngang, đủ dài; Bắc khung 2
    điểm cầm nhảy lên đỉnh đầu -> giữ tay khung 1; Tây khung 3 tay cầm trước mặt; NW khung 4-5 tay đúng chỗ.
  - Nhát 3 (yoko): khung 5 bớt ngang; Tây khung 0/2 lưỡi ra sau lưng thay vì xuyên mặt.
  - Vệt chém: bỏ 1/3 đầu đường vung (không còn thành vòng tròn quanh người ở SE/NE); đè lên thân thì mờ 60% để vẫn thấy mặt.
  - Gỡ kiếm: vá lỗ theo hướng (chép điểm ảnh gần nhất theo trục ngắn nhất) -> hết vệt nâu / sẫm trên mặt và tay; xoá thêm 3 "que" màu da
    (N nhát 1 khung 5, NW nhát 1 khung 5, S nhát 2 khung 6) theo toạ độ khung (`ERASE_F`); bao bị nhân đôi ở Đông nhát 2/3 -> giữ
    thanh gần bao khung 0, xoá thanh kia (`DEDUP`); mảnh xanh lẻ < 30 px (dấu bao cũ) -> màu thân.
  - Chuôi + tay vẽ TRƯỚC thân khi tay cầm nhìn thấy (trước chỉ lưỡi mới được trước/sau theo độ sâu -> chuôi chui sau tay).
  - Canvas 3 nhát nới thành 224x224 (đầu mũi kiếm / vệt không bị cắt mép); pivot tính lại -> lệch so với chân cũ 0 px, giữ 384 guid.
  - QA còn 23 cờ, đều có chủ ý (lưỡi sau lưng ở N/NE/NW, giơ sau đầu E/W khung 2, khung 7 PixelLab gốc). GIF: `Catalog/_dev/ren2/fix/attack*_v2.gif`.
- **§203c (09/11 ~16h50) — người dùng: hướng N và nhiều hướng "sai hết tư thế và cách tấn công"**. Phân tích: lỗi GỐC ở THÂN PixelLab
  (đứng yên, 2 tay co trước ngực, không lấy đà / xoay vai / bước vào / duỗi tay theo đường cắt; hướng lưng tay bị che) — vá lưỡi bằng code
  (§203, §203b) không cứu được. Vệt chém hình quạt đặc trông như lưỡi hái. Quyết định (người dùng chọn A): tạo lại THÂN 3 nhát trên PixelLab
  đủ 8 hướng với tư thế mới `Catalog/v2/tools/ren2/katana_clips_v3.py` (4 nhịp, lấy đà tay trên đầu, khung chém lưỡi có thành phần dọc,
  `check` độ dài chiếu ≥ 0.45). Vệt chém mới: lưỡi liềm mảnh bám mũi kiếm (`fix_atk/katdraw.py`).
  **Bài học chống tái phạm: `LESSONS_ANIM.md` ở gốc project — đọc trước khi làm mọi animation tấn công.**

## §204 — 09/11 tối: LÀM LẠI THÂN 3 nhát chém bằng PixelLab (khung khoá + V3 nội suy) — BẢN NHÁP, CHỜ NGƯỜI DÙNG DUYỆT GIF, CHƯA NHẬP UNITY
- Truy cập PixelLab: trình duyệt trong app Claude (người dùng đăng nhập), gọi API web bằng phiên đăng nhập của trang
  (`/generate-character-state/background`, `/animate-with-text-v3/character/background`, `/characters/<id>`), tải ảnh về Downloads
  (file `.tmp` theo kích thước) -> `Catalog/_dev/ren2/pl/`. Lượt: ~3607 -> ~4000/5000.
- Có một phiên khác (17h-19h) đã thử V3 + tạo 3 state "katana windup stance / before / end of a diagonal do" — dùng lại "windup stance" (jōdan).
- Khung khoá (state 176px, bảng màu khoá "Ren + katana + saya keys"), ID ở `Catalog/v2/tools/ren2/v3/ids_pixellab.md`:
  guard176, jōdan (giơ kiếm qua đầu), k1end (cuối nhát chém xuống, kiếm thấp phía phải-trước), k2end (giơ cao),
  k3start (kiếm ở hông phải chĩa ra sau), k3end + k3end_b (cuối quét ngang; chọn theo hướng).
- Clip (V3 trên Ren Katana Saya v2 f8705d6f, tên animation `k1a_/k1b_/k2A_/k3a_/k3b_<hướng>` + bản làm lại `k1b2_/k1b3_/k2B_`):
  - Nhát 1: thủ thế -> giơ qua đầu (k1a, 4 khung) -> chém xuống (k1b, 6 khung). Nhát 2: k1end -> chém hất lên tới giơ cao (8 khung).
    Nhát 3: giơ cao -> hạ về hông phải (k3a) -> quét ngang (k3b). Combo nối liền: cuối nhát trước = đầu nhát sau.
  - Chọn + sắp 8 khung / hướng ở `v3/picks.json` (khung 3 = trúng đòn); `build_atk.py` canh thân (khung thủ thế trùng tư thế đứng 128 -> lệch 0),
    canvas 224; `finish.py` bỏ mảnh FX rời + vẽ vệt chém đồng nhất khung 3-4. Kết quả: `Catalog/_dev/ren2/k_v3_import/` (192 khung + rotations).
- Còn yếu: NW nhát 1 khung 4-7 lưỡi khuất sau thân (đúng hình học nhưng khó đọc); vài khung còn bụi/vệt PixelLab dính mũi kiếm (S nhát 1 khung 4).
- Khi người dùng duyệt: `python3 ren_import.py ~/mnt/Catalog/_dev/ren2/k_v3_import` (giữ guid; sao lưu trước như §203) + test trong Unity.
- Bài học mới: LESSONS_ANIM.md mục 9-12.
- **§204b (09/11 ~22h) — người dùng: "hướng này khi chém lại quay về hướng ngược lại"**. Gốc lỗi: KHUNG KHOÁ PixelLab tạo với mô tả xoay thân
  đã xoay cả người ở vài hướng (k2end W nhìn sang phải, k3end_b W nhìn sang phải, k1end SW nhìn phải-trước); V3 nội suy tới đó -> Ren quay
  ngược giữa đòn (W nhát 2-3, SW nhát 1-2-3, E cuối nhát 2). Sửa: đối chiếu từng khung khoá với tư thế đứng (REF) -> jōdan đúng cả 8 hướng
  dùng làm cuối nhát 2 / đầu nhát 3 cho W, SW, E; khung khoá mới k1endC ("keeps facing exactly the same direction") đúng cả 8 hướng -> SW nhát 1-2;
  W nhát 3 kết ở k3end (bản a). SW nhát 3 giữ khung b4 (b5-b6 V3 tự quay). GIF: `Catalog/_dev/ren2/fix/attack*_v3b.gif`; ảnh nhập
  `Catalog/_dev/ren2/k_v3_import/` (đã cập nhật). Bài học: LESSONS_ANIM.md mục 13. Vẫn CHỜ DUYỆT, chưa nhập Unity.

## §204c — 09/11 tối: SOÁT CHUYÊN GIA 24 Ô × 8 KHUNG bản v3b -> bản v3c (CHỜ DUYỆT GIF, CHƯA NHẬP UNITY)
- Soát bằng bảng `q_<huong>.png` (REF + 3 nhát × 8 khung, khung 3 tô nền). Lỗi tìm được và cách sửa:
  - Khung trúng (3) chưa phải lúc lưỡi cắt qua: SE/N/NW/W nhát 1; E/SW/W nhát 2; E/NW nhát 3 -> sắp lại khung (`picks.json`).
  - Nối combo giật nhát 2 -> nhát 3 (nhát 2 kết giơ cao, khung 0 nhát 3 hạ thấp): SE/NE/N/NW -> khung 0 nhát 3 lấy khung chuyển tiếp hợp lý.
  - Lưỡi đi tới–lui–tới / lật phía: E nhát 3 khung 1, NE nhát 3 khung 1 (2 lưỡi) + khung 7 (lưỡi lật trái), W nhát 1 khung 2 -> bỏ khung; W nhát 1 đổi sang đoạn k1b2; SW nhát 2 đổi k2JC3.
  - FX PixelLab dính liền lưỡi (vệt trắng chân E nhát 1, bụi mũi S nhát 1, tia N) -> công cụ mới `fxclean2.py` (dò lưỡi từ chuôi/tay, giữ ±2px, xoá nhánh; khớp <30% thì bỏ qua).
  - SW nhát 3 khung 4-7: bao kiếm nằm ngang + 2 vòng vàng "D" trên đai -> tạo lại đoạn V3 k3bS2/k3bS3 (k3start->k3end, mô tả giữ bao kiếm ở hông) , dùng k3bS3.
  - SE nhát 1 khung 3-5 bao kiếm sang trái (hông xoay): thử k1bC4/C5 (jōdan->k1endC) nhưng khung cuối lệch với đầu nhát 2 -> GIỮ bản cũ (chấp nhận như xoay hông), ghi lại để người dùng quyết.
- Kết quả: `Catalog/_dev/ren2/k_v3c_import/` (200 png, sẵn `ren_import.py`), GIF `Catalog/_dev/ren2/fix/atk*_v3c.gif`, `combo_v3c.gif`; công cụ + picks: `Catalog/v2/tools/ren2/v3/v3c/`. Bản v3b giữ nguyên ở `k_v3_import/`.
- Bài học mới: LESSONS_ANIM.md #14–#18.
- Unity vẫn đang là bản §203b. Chỉ nhập khi người dùng nói "nhập": `python3 ren_import.py ~/mnt/Catalog/_dev/ren2/k_v3c_import` (giữ guid, hitFrame vẫn 3).

## §204d — 09/11 22:35: sửa SE quay mặt 45° + N cuối nhát 3 (bản v3d, CHỜ DUYỆT, CHƯA NHẬP UNITY)
- Người dùng báo SE vẫn quay mặt lệch 45°. Soát hàng đầu-mặt SE: nhát 1 khung 3-5 nhìn thẳng camera (S); nhát 3 khung 0-3 quay nghiêng sang E (khung khoá k3start SE vốn quay sang E).
- Sửa SE cả combo bằng PixelLab (mỗi đoạn 2 phương án): nhát 1 = k1a + **k1bC4** (jōdan->k1endC); nhát 2 = **k2C4** (k1endC->k2end); nhát 3 = **k3E3** (k2end->k3end thẳng, bỏ k3start). Mặt giữ SE ở cả 24 khung. Còn lại: bao kiếm SE nhát 1 khung 4-7 nằm bên trái (khung khoá k1endC vẽ vậy), mặt vẫn đúng.
- N nhát 3 khung 4-7: tay cầm chuôi duỗi sang phải, lưỡi vòng ra sau lưng sang trái (người dùng gửi ảnh) -> giữ khung k3b 4 (kiếm thu về bên trái, đúng hướng).
- Kết quả: `Catalog/_dev/ren2/k_v3d_import/` (sẵn ren_import), GIF `fix/SE_v3d.gif`, `N_v3d.gif`, `combo_v3d.gif`; picks `v2/tools/ren2/v3/v3c/picks_v3d.json`. Bài học #19.
- 09/11 22:40: người dùng nói "nhập" -> sao lưu attack1-3 + _uv cũ (bản §203b) vào `Catalog/backups/ren_atk_2026-11-09/pre_v3d/`, chạy `ren_import.py k_v3d_import`: 192 khung, guid giữ nguyên, hitFrame vẫn 3. ĐÃ NHẬP v3d VÀO UNITY — chờ người dùng test.

## §204e — 09/11 23:20: soát ĐẦU-MẶT từng khung 8 hướng (bản v3e, CHỜ DUYỆT, CHƯA NHẬP — Unity đang là v3d)
- Bảng đầu-mặt (`heads.py`, 4x, cạnh REF) phát hiện mặt lệch 30-45°: S nhát 2 khung 0-6 (quay sang phải), SW nhát 1 khung 3-5 + nhát 2 khung 0-5 (quay về chính diện), W nhát 2 + nhát 3 (3/4 thay vì nghiêng), SE nhát 3 (chính diện thay vì 3/4). N/NE/NW/E đạt.
- Sửa bằng PixelLab (mỗi đoạn 2-3 phương án, mô tả giữ mặt): S nhát 2 = k2F3; SW nhát 1 = k1bC3 (bỏ khung quay lưng), nhát 2 = k2G3 (khung đầu = khung cuối thật của nhát 1); W nhát 2 = k2F5, nhát 3 = k3F3; SE nhát 3 = k3F3. Thêm `sayafix.gold_specks` (chấm vàng trên mắt).
- Sau sửa: cả 192 khung mặt đúng hướng REF (bảng `fix/mat_8huong_v3e.png`). Gói nhập: `Catalog/_dev/ren2/k_v3e_import/`. Bài học #20-#21.
- 09/11 23:20: người dùng nói "nhập" -> sao lưu bản v3d (attack1-3 + _uv) vào `Catalog/backups/ren_atk_2026-11-09/pre_v3e/`, chạy `ren_import.py k_v3e_import`: 192 khung, guid giữ nguyên (0 khác), 84 ảnh thay đổi, 0 meta rỗng. ĐÃ NHẬP v3e VÀO UNITY — chờ người dùng test.

## §205 — 10/11 00:00: CHẠY KIẾM TRONG BAO + trạng thái kiếm (rút / tra) — LESSONS #22-#30
- Phân tích run cũ (`Catalog/v2/tools/ren2/run/run_qa.py`): move `run` PixelLab vung tay kiểu nước rút -> katana quay ~150°/chu kỳ, mũi cao hơn đầu 5 hướng, S/NW mất lưỡi, đầu nhấp 4-7 px.
- Người dùng: "chạy là phải cho kiếm vào bao". State mới `Ren Katana Sheathed` 74eb2346-46cd-439a-9871-6327fa0d662c (ép bảng màu khoá -> UV vẫn đổi design chuôi/bao, đã thử 3 katana).
- `run` = nhóm `run_sheath_1` (c4fbd4b3…): skeleton-v3 `run` energy 0.8 + lean 10 + head -6 + tay trái đè bao mọi khung. ĐÃ NHẬP `Ren/run` (8 khung, guid giữ). Sao lưu cũ: `Catalog/backups/ren_run_2026-11-10/pre_sheath/`.
- `idle_sheathed` (nhóm idle_sheath ff5bcc54…, E ping-pong 3 khung, SE/NE/NW bù 2-3 px) + `sheathe` (V3 từ rotations cầm kiếm -> rotations trong bao, snap bảng màu, đặt lại canvas 192; SE=b, W=c, SW=c, còn lại a). Gói: `Catalog/_dev/ren2/sheath205_import_v2/` (`ren_import.py <gói> --state=Ren_Katana_Sheathed`). CHƯA NHẬP.
- Người dùng bắt lỗi tư thế GỐC: SW cầm nhầm tay (chuôi sát miệng bao như 2 kiếm), NW mất kiếm (cả idle cầm kiếm hiện tại). State mới (cc3b1378 / 00b5982d) vẫn sai -> sửa tay bằng tay: SW = xoá kiếm cũ + dán kiếm/nắm tay từ rotations S (dịch +7,-6); NW = inpaint vùng tay phải (e6e9a921). Ảnh: `Catalog/_dev/ren2/pose_SW_fix.png`, `pose_NW_fix.png`.
  - idle cầm kiếm SW/NW mới (`sheath/idlefix.py`, thở 1 px) -> gói `Catalog/_dev/ren2/idlefix205/` (`ren_import.py <gói>`, state mặc định).
  - sheathe SW = `sheathe_SW_fix2`, NW = `sheathe_NW_fix1` (khung đầu = tư thế đã sửa). Gói cuối: `Catalog/_dev/ren2/sheath205_v3/` (`--state=Ren_Katana_Sheathed`). GIF duyệt: `tra_kiem_8huong_v2.gif`.
  - 10/11 00:50: ĐÃ NHẬP sheath205_v3 (idle_sheathed + sheathe, 16 nhóm) + idlefix205 (idle SW/NW, guid giữ). Sao lưu idle SW/NW cũ: `Catalog/backups/ren_run_2026-11-10/pre_idlefix/`. Người dùng đã chạy builder TRƯỚC khi nhập (báo thiếu ảnh = code biên dịch OK) -> chạy lại builder.
  - (đã sửa §205d, xem dưới) attack SW/NW đầu/cuối theo tư thế mới.
- Code (CHƯA biên dịch thử — người dùng tự mở Unity):
  - `PlayerAnimation`: bool `Sheathed`; `Engage()` ở Attack/Stomp/Cast/Ray/Hurt/Stagger giữ kiếm `DrawnHold`=3 s; `Tick()` gọi đầu `PlayerCombat.Update`.
  - `RenAnimatorBuilder`: state `IdleSheathed` (mặc định) + `Sheathe` (Idle --Sheathed--> Sheathe -> IdleSheathed; Run luôn trong bao; Run dừng -> Idle/IdleSheathed theo Sheathed); `RunAnimSpeed` 1.3 -> 1.55 (bước ngắn hơn ~18%); **dựng lại controller TẠI CHỖ (`Clear`) -> giữ guid**, Map1/2/3/3b không mất liên kết.
  - `ren_import.py`: thêm tên `idle_sheathed`, `sheathe`.
- Việc người dùng: mở Unity -> menu Soul Requiem ▸ Player ▸ Build Ren Animator (v2) -> Ctrl+S -> thử (vào màn kiếm trong bao; chém rút; 3 s tra kiếm; bước chạy có trượt không).
- 10/11 00:58 (§205b): người dùng báo idle_sheathed E tay đung đưa -> làm lại idle_sheathed 8 hướng = rotations trong bao + thở 1 px (`sheath/pack_v4.py`), ĐÃ NHẬP (guid giữ). Bài học #31.
- 10/11 01:05 (§205c): soát lại từ file Unity (`Catalog/_dev/ren2/chk205/unity_ren_205.gif`, căn theo pivot meta): idle CẦM KIẾM cũ cũng lắc (E có vệt hơi thở trắng cạnh miệng + tay/kiếm xê dịch tới 1400 px, N/NE/SE/W/S kiếm lắc nhẹ) -> làm lại idle 8 hướng = rotations (SW/NW tư thế đã sửa) + thở 1 px (`sheath/idlefix_v2.py`), ĐÃ NHẬP (guid giữ, 6 khung). Sao lưu: `Catalog/backups/ren_run_2026-11-10/pre_idle_drawn_v2/`. Kiểm: idle + idle_sheathed lệch ngoài phần thở = 0.
- 10/11 01:20 (§205d): sửa đầu/cuối đòn chém theo tư thế cầm kiếm mới (`sheath/atk_splice.py`, V3 4 khung giữa tư thế mới và khung đòn cũ, chỉ lấy 1-2 khung): attack1 SW k0 = tư thế mới, k1 = V3 (giơ kiếm), k7 = tư thế mới; attack2 SW k0 = tư thế mới; attack1 NW k6 = V3 (hạ kiếm, lưỡi hiện), k7 = tư thế mới; attack2 NW k0 = tư thế mới, k1-k2 = V3. ĐÃ NHẬP (guid + pivot giữ). Sao lưu: `Catalog/backups/ren_run_2026-11-10/pre_atkfix/`. GIF: `Catalog/_dev/ren2/chk205/chem_SW_NW_sua.gif`. attack3 không đổi (bắt đầu từ thế giơ cao của nhát 2).

## §205e — 10/11 01:20: GIẢM TỐC ĐỘ ĐÁNH (người dùng: "đánh nhanh quá")
- Phân tích (Katana cũ): clip 8 khung @20 fps = 0,40 s; trúng ở 0,15 s; huỷ / nối nhát ở khung 5 = 0,25 s -> bấm liên tục ra 4 nhát/giây, cả combo 3 nhát + nghỉ chỉ 1,12 s; nhát bị cắt ở 5/8 nên không thấy phần vung theo.
- Sửa (chỉ số liệu, không đổi ảnh): fps ×0,65, cancelFrame +1 (5->6, nhát 3: 6->7), finisherRecovery +~0,12, comboWindow +0,02; sát thương ×1,6 để giữ DPS (~50, cũ ~52).
  | Katana | fps nhát 1/2/3 | huỷ | dmg | nghỉ cuối | combo |
  |---|---|---|---|---|---|
  | Ronin's | 13/13/12 | 6/6/7 | 26/26/42 | 0,30 | 1,9 s (0,46 s/nhát) |
  | Moonlit (nhanh) | 15/15/14 | 6/6/7 | 22/22/37 | 0,25 | 1,6 s |
  | Crimson (nặng) | 12/12/10 | 6/6/7 | 30/30/50 | 0,36 | 2,2 s |
- File: `Resources/Weapons/Katana*.asset`, mặc định trong `WeaponDefinition.cs`. Sao lưu: `Catalog/backups/ren_run_2026-11-10/weapons_pre_speed/`. Chỉnh tiếp: chỉ cần sửa `fps` từng nhát trong asset (Inspector) — clip, khung trúng, khoá di chuyển, vệt chém tự theo.
- 10/11 01:25: người dùng chọn GIỮ NGUYÊN sát thương (16/16/26, Moonlit 14/14/23, Crimson 19/19/31) -> chỉ đổi tốc độ; DPS giảm ~52 -> ~31 (Ronin's), quái/boss lâu chết hơn ~1,7 lần.

## §206 — 10/11 01:35: SOÁT CHUYÊN GIA 3 ĐÒN × 8 HƯỚNG × 8 KHUNG (bản đang trong Unity, sau §205d) — người dùng: "sai rất nhiều từ tư thế đến cách tấn công"
Bảng: `Catalog/_dev/ren2/chk205/phan_tich_attack1..3.png` (cột 0 = idle, căn pivot).
- Đường cắt không phải MỘT đòn 3D: mỗi hướng chém một kiểu (nhát 1 S/SW chém thẳng xuống giữa 2 chân, SE chém về phía SAU-trái, N/NE chém dọc bên hông; nhát 2 S/E/N không có đường hất lên ở phía trước, SE/N chém ngang ra sau; nhát 3 độ cao quét lúc ngang đầu (N/NW) lúc sát chân (E/NE/SW)).
- Đường mũi kiếm đi tới-lui (nhát 1 SE k2 lưỡi lộn ngược; NE/N k1 giơ sau đầu -> k2 dựng đứng trước -> k3 chém).
- Tấn: ngồi xổm dạng chân rộng kiểu sumo (nhát 1 S/SW/W k3-k7, nhát 2 k0-k3, nhát 3 k1-k7) -> trông buồn cười, khác hẳn idle -> giật khi về idle.
- Khung đứng im lặp lại: nhát 2 k5-k7 giữ thế giơ cao, nhát 3 k4-k7 giữ thế "chĩa kiếm như ngắm súng" (không phải vung theo của quét ngang) -> sau khi giảm fps (§205e) thành đứng đơ 0,25-0,33 s.
- Khung trúng (k3) nhát 2 S/E/N chưa thấy lưỡi đi lên (hất ở k4). Vệt chém lệch đường lưỡi (nhát 1 NE k4, W k4 hình chữ L; nhát 3 SE k3 vệt ngang nhưng lưỡi chĩa xuống).
- Nối combo: nhát 2 k7 (giơ cao) -> nhát 3 k0 lệch ở NE/NW/W; cuối nhát 2 / nhát 3 về idle không có thu kiếm.
- Hướng mặt: nhát 1 W k1 quay mặt về camera; nhát 1 S k1 tay che mặt.
- Gốc: thân làm bằng V3 nội suy từng hướng riêng + vá nhiều lần -> 8 hướng = 8 cách hiểu. Đề xuất: làm lại cả 3 nhát bằng skeleton-v3 (MỘT chuyển động 3D chiếu ra 8 hướng, giữ bảng màu): move `slash` / `rising_cut` / `sweep` + pose tay trái cầm chuôi, thử 3 hướng trước. CHỜ NGƯỜI DÙNG QUYẾT.
- 10/11 01:50 (§206b): THỬ skeleton-v3 cho 3 nhát (nhóm atk4_1 / atk4_2 / atk4_3, rồi atk4_1b chỉnh khuỷu tay; E/SE/S) -> KHÔNG ĐẠT: thân chuyển động đúng 1 đường 3D, nhưng LƯỠI KIẾM do mô hình vẽ theo cẳng tay nên không xoay theo nhát chém (khung trúng lưỡi dựng đứng / chĩa vào camera), mất lưỡi ở khung vung mạnh (E k4-k5), 2 lưỡi (E k1), tay 2 bên che mặt thành mảng đen khi giơ cao. Ảnh: `thu_skeleton_nhat1.png`. Kết luận: skeleton-v3 không điều khiển được góc lưỡi -> không dùng một mình cho đòn chém. Hướng đề xuất: THÂN skeleton-v3 tay không + KATANA vẽ bằng code theo cùng đường 3D (góc lưỡi chính xác, 8 hướng đồng nhất, UV design vẫn dùng được, nắm tay đè lên chuôi). Chờ người dùng quyết.
- 10/11 02:30 (§206c): người dùng chọn hướng 1 (thân skeleton-v3 tay không + katana vẽ code). State `Ren Empty Hands` 252b957a-266f-4cbe-901c-8306899cbec7 (bảng màu khoá, KHÔNG có màu lưỡi; NE/N vẫn còn lưỡi màu nhạt -> phải xoá bằng code). Thân nhát 1 = nhóm `body_a1` 5bf2bb37 (E/SE/S). Công cụ (container, chưa chép máy): `/home/claude/ren/atk5/` katana3d.py (chiếu 3D [phải,trước,lên] -> màn hình, CU 0.90 CV 0.55, vẽ lưỡi 3 màu + tsuba + chuôi), hands.py (đoán nắm tay phải từ toạ độ 3D, S=120 px/đv, hông = chân-26, bù +4,-5; bỏ cụm da trong vùng mặt), compose.py (lưỡi sau thân khi độ sâu >0.7 hoặc khung lấy đà; vệt chém = vùng giữa cung mũi kiếm nội suy cầu 3D; khung nghỉ k7 lấy hướng lưỡi 2D từ idle), a1b.json (tư thế), blade_a1.json (hướng lưỡi 8 khung). Bản thử: `thu_nhat1_moi.gif`. Chờ người dùng duyệt kiểu trước khi làm 3 nhát × 8 hướng.

## §204f — 10/11 02:20 (phiên song song với §205-§206): chọn lại khung đòn chém theo nhịp game thật (bản v3f, CHƯA NHẬP)
- Làm trên nền v3e (KHÔNG gồm sửa SW/NW của §205d). Gói chờ: `Catalog/_dev/ren2/k_v3f_import/` (+ `fix/v3f_anims.zip`, GIF `fix/combo_v3f_a.gif`, `combo_v3f_b.gif`, `cmp_v3e_v3f.gif`, bảng `fix/mat_8huong_v3f.png`). Công cụ: `Catalog/v2/tools/ren2/v3/v3f/`.
- Nội dung: khung 3 = lưỡi cắt qua mục tiêu ở mọi hướng (S/NE nhát 2, SE nhát 1 đổi khung); khung cuối thấy được khi nối (cancelFrame-1 = k5 nhát 1/2) liền với k0 nhát sau; khoá độ dài lưỡi <= 1,15 x trung vị từng hướng (`bladelen.py`, thế giơ cao trước dài 61-65 px); xoá kiếm "ma" SW nhát 2 k4-k5; bỏ vệt chém rời NE nhát 1 k4; W nhát 3 k2 bỏ khung bao đen.
- CHƯA làm: thu kiếm cuối nhát 3 (cần PixelLab; phiên này bị đăng xuất). Nếu dùng hướng §206c (làm lại toàn bộ) thì bỏ v3f.

## §204g — 10/11 03:05: LÀM LẠI CUỐI NHÁT 3 (vung theo + thu kiếm về thế thủ) — bản v3g, CHỜ DUYỆT, CHƯA NHẬP
- Người dùng: "không ai cầm kiếm như này, không ai kết thúc đòn chém ở tư thế này" (cuối nhát 3 = thế chĩa kiếm ngang như ngắm súng, N cầm sai tay) -> chọn phương án B.
- Nhát 3 mới: k0-k3 giữ (lấy đà, cắt qua); k4 = khung khoá VUNG THEO mới (state 176 từ f8705d6f, bảng màu khoá): S/E/NE = 32c44d26 (A), N = 168baec2 (C), NW/W/SW = b74d8cd6 (B), SE = 2b2678b5 (E, giữ mặt 3/4); k5-k6 = V3 thu kiếm (vung theo -> thế thủ guard176 72433f1e; SW/NW -> tư thế mới §205 gửi dạng base64); k7 = đúng tư thế đứng (rotations / pose SW,NW §205) -> về idle không giật. Bỏ hẳn khung k3end cũ.
- Gộp sẵn sửa SW/NW của §205d (attack1 SW k0,k1,k7; attack2 SW k0; attack1 NW k6,k7; attack2 NW k0-k2) lên v3f/v3g (`merge205.py`).
- Soát: bảng đầu-mặt 8 hướng đạt; cách cầm (2 tay trên chuôi, lưỡi ra từ tsuba) đạt; nhát 3 hết khung đứng im.
- Gói: `Catalog/_dev/ren2/k_v3g_import/`. GIF: `fix/nhat3_cu_moi_a.gif`, `_b.gif`, `fix/combo_v3g_a.gif`, `_b.gif`; bảng `fix/mat_8huong_v3g.png`. Công cụ: `Catalog/v2/tools/ren2/v3/v3g/`.
- Nhập (khi người dùng nói "nhập"): sao lưu attack1-3 hiện tại -> `cd Catalog/v2/tools/ren2 && python3 ren_import.py ~/mnt/Catalog/_dev/ren2/k_v3g_import`.

## §207 — 10/11 03:50: LÀM LẠI NHÁT 1 (kesa-giri) + NHÁT 2 (kiri-age) theo kiếm thuật thật — bản v3h, CHỜ DUYỆT, CHƯA NHẬP
- Người dùng: nhát 1-2 sai động tác thật (N kiếm thõng dọc hông, kiếm gác sau gáy, W kiếm dựng trước mặt). Gốc: khung khoá cũ — nhát 1 kết thúc thấp bên PHẢI (k1endC) nên đường chém đi dọc bên hông, không cắt qua trước mặt.
- Nhát 1: k0-k1 giơ kiếm 2 tay lên vai phải (V3 thế thủ -> hassō 2cfb1cd1), k2 hassō, k3 = vung theo hông trái (khung k4 nhát 3) + VỆT CHÉM CÓ HƯỚNG đi qua trước mặt (`smearvia.py`), k4-k5 giữ, k6-k7 thu kiếm (= k5-k6 nhát 3, về thế thủ).
- Nhát 2: k0 vung theo trái, k1-k2 hạ kiếm thấp giữa (lấy đà), k3 = hassō + vệt chém hất chéo lên qua trước mặt, k4-k7 hassō -> nhát 3 k0 giơ cao.
- HẠN CHẾ: PixelLab HẾT LƯỢT (5048/5000, credits 0) ngay khi đang tạo các khung giữa nhát chém -> k3 nhát 1-2 dùng "khung vệt chém" (tư thế cuối + vệt cung) thay cho khung lưỡi đang ở giữa đường. Khi có lượt: V3 hassō -> vung theo (nhát 1) và vung theo -> hassō (nhát 2), thay k3 + thêm khung giữa. Khung khoá kesaMid/kesaEnd/kiriMid đã thử (ids_pixellab.md) KHÔNG dùng được: PixelLab vẽ kiếm thấp bên phải như thế thủ.
- Gói: `Catalog/_dev/ren2/k_v3h_import/` (gồm cả nhát 3 v3g). GIF `fix/combo_v3h_a.gif`, `_b.gif`; bảng `fix/mat_8huong_v3h.png`. Công cụ `Catalog/v2/tools/ren2/v3/v3h/`.

## §207b — 10/11 11:30: nhát 1-2 có KHUNG GIỮA NHÁT CHÉM thật (PixelLab sang kỳ mới) — bản v3i, CHỜ DUYỆT, CHƯA NHẬP
- V3 (mỗi hướng 2 phương án + SE thêm 2): nhát 1 = hassō 2cfb1cd1 -> vung theo trái (khung khoá nhát 3); nhát 2 = vung theo trái -> hassō. Chọn khung theo hướng (`k12_cfg.json`), bỏ khung 2 lưỡi / hiệu ứng / mặt lệch (SE nhát 2 làm lại với câu khoá mặt 3/4).
- Nhát 1: k0-k1 giơ kiếm, k2 hassō (S: bắt đầu hạ giữa), k3 = lưỡi CẮT QUA trước mặt (khung giữa thật) + vệt chém có hướng, k4 tiếp, k5 vung theo hông trái, k6-k7 thu kiếm.
- Nhát 2: k0-k2 hạ thấp / quét thấp qua trước (lấy đà), k3 = lưỡi hất lên giữa đường + vệt chém, k4-k5 lên vai phải, k6-k7 giữ -> nhát 3 k0 giơ cao.
- Soát mặt 8 hướng đạt. Dùng ~120/5000 lượt kỳ mới; generations_left_today = 0 (giới hạn ngày).
- Gói `Catalog/_dev/ren2/k_v3i_import/` (cả nhát 3 v3g). GIF `fix/combo_v3i_a.gif`, `_b.gif`; bảng `fix/mat_8huong_v3i.png`. Công cụ `Catalog/v2/tools/ren2/v3/v3i/`.

## §208 (10/11) Ren katana combo v3j — khung trúng PHÍA TRƯỚC MẶT (chưa nhập)
- Phản hồi: đòn 2 đưa tay ra sau rồi chém sau lưng; trọng tâm đòn lúc dưới chân, lúc ngang, lúc ra sau.
- Đo (aimchk.py) v3i: khung trúng lệch 70–176° so với hướng mặt ở atk1 S/SE/NW/N, atk2 S/SE/E/NW/SW, atk3 SE/SW; đòn 2 giữ hasso (lưỡi chĩa ra sau) f4–f7.
- Sửa: 3 khung khoá mới "strike" (8ef00a66 / d8506772 / b89b7583) + "risefin" 84523e8e (kiếm giơ cao phía trước). Đòn 1: gió→hasso→STRIKE→vung theo thấp phía bên kia→rest. Đòn 2: nạp ở hông→STRIKE→risefin (S/N: hasso)→thu kiếm V3. Đòn 3: STRIKE ở f3, giữ phần còn lại v3g.
- Sau sửa: khung trúng 0–12° lệch, mũi kiếm +62..+87px phía trước, cả 8 hướng × 3 đòn.
- Gói: Catalog/_dev/ren2/k_v3j_import (201 file); GIF/so sánh: _dev/ren2/fix/*v3j*; tool: Catalog/v2/tools/ren2/v3/v3j. Chờ duyệt, nhập khi user nói "nhập".

## §209 (10/11 ~12:20) ĐÃ NHẬP v3j + VFX chém mới theo từng thanh kiếm
- Nhập: attack1-3 = v3j bản sạch (k_v3j2_import: KHÔNG còn vệt chém xám vẽ trong sprite khung 3 — VFX game vẽ thay). Sao lưu trước khi nhập: Catalog/_dev/ren2/backup_pre_v3j_0458 (sprite + _uv + PlayerMelee.cs, WeaponDefinition.cs, 3 file Katana*.asset).
- VFX mới: Resources/VFX/slash3/<kiểu>_<nhát 1-3>_<HƯỚNG>.png (72 dải, mỗi khung 1 sprite). Làm bằng PixelLab VFX Creator (engine recipe chạy trong trình duyệt, không tốn lượt): lớp "arc" profile "slash" có points = ĐƯỜNG MŨI KIẾM đo từ chính khung 2→3→4 của hoạt ảnh (vfxgeo.py), + lõi sáng + hạt (Moonlit: lấp lánh, Crimson: giọt máu).
- Khớp khung: canvas VFX 224 = canvas sprite Ren, pivot VFX = pivot chân sprite cùng hướng → đặt tại SpriteRenderer của Ren, cùng tỉ lệ. Bắt đầu lúc hiện khung 2, 2 khung VFX / 1 khung hoạt ảnh (fps VFX = 2×fps nhát), đầu vệt chạm MŨI KIẾM đúng khung trúng 3, tan dần khung 4-5.
- Kiểu: Katana.asset slashStyle=ronin (thép trắng-xanh), Katana_Moonlit=moonlit (xanh trăng, vệt kép + lấp lánh), Katana_Crimson=crimson (đỏ máu + giọt máu). WeaponDefinition.slashStyle trống → VFX cũ slash_*.
- Code: PlayerMelee.SpawnFrameVfx / StyleOf / FrameVfxName; PreloadVfx nạp thêm 24 dải của thanh đang cầm.
- Công cụ: Catalog/v2/tools/ren2/v3/v3j/tools_vfx209.zip (vfxgeo.py, vfx_recipes.js, vfxunity.py, vfxgif.py). GIF: _dev/ren2/fix/vfx_*.gif

## §210 (10/11 12:40) Lướt (Dash/Roll) — kiếm TRONG BAO — ĐÃ NHẬP 12:50 (roll 8 hướng + code PlayerAnimation.Dash, RenAnimatorBuilder); cần chạy lại menu dựng Animator
- Người dùng: "chiêu dash đang cầm kiếm rồi dash". Roll cũ: lộn nhào với lưỡi kiếm trần trên tay (state cầm kiếm), nhiều khung nhoè.
- Mới: V3 từ state Ren Katana Sheathed 74eb2346 (rotations -> rotations), 2 bản thử (lộn / lướt), chọn bản LƯỚT: chùi người lao về trước, tay trái giữ bao kiếm, không thấy lưỡi; khung cuối khớp tư thế đứng trong bao (lệch 0-4 px). Khung lấy V3 [2,3,4,5,5,6,7,8] (N: [2,3,4,4,4,6,7,8], bỏ khung có đốm xanh).
- Gói: Catalog/_dev/ren2/dash210_import (nhập: `ren_import.py <gói> --state=Ren_Katana_Sheathed`). Sao lưu roll cũ + code: _dev/ren2/backup_pre_dash210.
- Code chờ nhập (đã viết sẵn ở backup_pre_dash210/patched, CHƯA chép vào dự án): PlayerAnimation.Dash đặt Sheathed=true (lướt xong về IdleSheathed, chém lại thì tự rút); RenAnimatorBuilder: Dash -> IdleSheathed khi Sheathed. Sau khi nhập phải chạy lại menu dựng Animator.

## §211 (10/11 13:10) LỘN VỀ TRƯỚC thật (thay bản "lướt" §210 — người dùng: "cần roll về phía trước mà sao lại ngồi") — ĐÃ NHẬP 13:13 (8 hướng roll, guid giữ; sao lưu _dev/ren2/backup_pre_roll211)
- Nguyên nhân §210: V3 khung đầu = khung cuối = đứng -> chỉ khom/ngồi. Sửa: 3 khung khoá state (từ Ren Katana Sheathed): lao người dive17 a6d9c1e9, cuộn tròn tuck3 8dac21c4, quỳ đứng dậy rise3 17f38d8c; V3 nối 3 đoạn dive->tuck, tuck->rise, rise->đứng. (Thử V3 tự do không khung cuối: hỏng, bỏ.)
- 8 khung: lao người, chúi xuống, cuộn tròn, CUỘN QUA VAI (quả cầu xoay 90° theo chiều lăn ở hướng ngang/chéo; S/N cầu lăn tới/xa 6 px), bung ra, quỳ, đứng dậy, đứng (khớp tư thế đứng trong bao 0-3 px).
- Gói: Catalog/_dev/ren2/roll211_import (`ren_import.py <gói> --state=Ren_Katana_Sheathed`). Code §210 (Dash -> Sheathed, về IdleSheathed) vẫn dùng được.

## §212 (10/11 16:20) UỐNG BÌNH MÁU làm lại — ĐÃ NHẬP 16:22 (8 hướng drink + PlayerFlask, PlayerAnimation, RenAnimatorBuilder); cần chạy lại Build Ren Animator (v2)
- Lỗi bản cũ (soát 8 hướng): uống tay trái, tay phải cầm KIẾM TRẦN kiểu cũ (lưỡi bạc, khác thiết kế katana hiện tại); chai đỏ chỉ là đốm vài px, hiện ra từ không khí; N/NE/NW gần như đứng im (không thấy giơ chai); đầu không ngửa; clip 0,7 s nhưng khoá chỉ 0,5 s (đi được khi tay còn đang hạ chai -> cắt cụt); máu cộng NGAY lúc bấm, VFX 0,22 s sau — không khớp động tác.
- Mới: kiếm TRONG BAO (state 74eb2346), khung khoá: cầm chai thuỷ tinh đỏ trước ngực hold21 ab7a9c74, ngửa cổ uống drink21 00179329 (bảng màu khoá + 5 sắc đỏ). V3 3 đoạn: đứng->rút chai, chai->môi, uống->cất chai->đứng. 8 khung, DrinkMs 50,60,50,90,90,60,50,50 = 0,5 s = healLock; khung cuối = tư thế đứng (0-4 px).
- Code chờ nhập (_dev/ren2/backup_pre_drink212/patched): PlayerFlask hồi máu + VFX ở GulpTime 0,16 s (khung 3 ngửa cổ); PlayerAnimation.Drink tra kiếm (Sheathed=true); RenAnimatorBuilder DrinkMs mới + Drink -> IdleSheathed.
- Gói: Catalog/_dev/ren2/drink212_import (`--state=Ren_Katana_Sheathed`). Sao lưu drink cũ + code: _dev/ren2/backup_pre_drink212.

## §213 (10/11 16:40) SOÁT 4 HOẠT ẢNH CAST + làm lại SHOCKWAVE (stomp) — ĐÃ NHẬP 17:05
- Người dùng: "kiếm không cắm xuống đất ở trung tâm mà cắm sang bên cạnh" + "tránh động tác thừa khiến nhân vật ngu ngốc".
- stomp cũ: kiếm 1 tay cắm cạnh hông (S lệch ~15 px sang phải chân), N/NW không thấy cắm, SW/NE vung kiếm qua lại; clip 0,75 s > khoá 0,5 s (bị cắt); sóng xung kích VFX ở giữa chân, không ở chỗ kiếm.
- Mới: 4 pha gọn — giơ kiếm thẳng đứng 2 tay (raise19 6453aad3) -> quỳ 1 gối CẮM KIẾM THẲNG ĐỨNG ngay trước thân (plant19 812df0a5; S mũi kiếm lệch chân 5 px) -> giữ -> rút kiếm đứng lên. V3 3 đoạn, loại các khung V3 có vệt trắng / vòng sáng / vung thừa. StompMs 60,90,100,40,80,50,40,40 = 0,5 s; chạm đất khung 3 ở 0,25 s (GroundSlam.asset impactTime 0,31 -> 0,25). VFX ground_impact đặt ở MŨI KIẾM (GroundSlamSkill.BladeTip, bảng px 8 hướng); vùng sát thương giữ ở giữa người.
- Gói: Catalog/_dev/ren2/stomp213_import (state mặc định Ren_Katana_Saya_v2). Code chờ: _dev/ren2/backup_pre_stomp213/patched.
- Còn lại (chưa sửa): cast_embers (kiếm trần bạc 1 tay; E còn quả cầu xanh + tia vẽ dính; N/NW không thấy động tác), cast_veil (vung kiếm qua đầu; S khung 6 mất lưỡi; NW giơ BAO KIẾM thay vì kiếm), ray (gần như đứng im, tay giơ thấp, S gần như không thấy tay).

## §214 (10/11 17:00) Làm lại 3 cast còn lại: cast_embers, cast_veil, ray — ĐÃ NHẬP 17:05 (cần chạy lại Build Ren Animator (v2))
- Nguyên tắc người dùng: không động tác thừa. Mỗi chiêu: lấy thế -> ra chiêu (khung 3) -> giữ -> thu về đúng tư thế đứng; thu về = đảo ngược chính các khung ra chiêu.
- Khoá (base f8705d6f): palm23 425a0298 (tay TRÁI đẩy thẳng lòng bàn tay mở ra trước, tay phải giữ kiếm thấp), ward23 429ebeee (2 tay dựng kiếm thẳng đứng trước mặt). SW/NW đầu-cuối = pose §205 (URL khung đầu hoạt ảnh cũ trên PixelLab); NW palm = V3 tự do từ pose (khoá NW của base không có lưỡi).
- cast_embers (Seeker Embers, Ichor Flask, Vessel Hand): 0,35 s, lòng bàn tay ra ở 0,2 s. cast_veil (Veil, Wail): 0,3 s, kiếm dựng ở 0,13 s. ray: 2 khung giơ tay (60+80 ms) + 6 khung giữ tĩnh (tia tự chuyển động).
- Code chờ (_dev/ren2/backup_pre_cast214/patched): RenAnimatorBuilder CastEmbersMs/CastVeilMs; JudgementRaySkill.handPixels mới (đo tự động từ khung giữ); EyeOrbs.asset spawnDelay 0,3 -> 0,2.
- Gói: Catalog/_dev/ren2/cast214_import (state mặc định). Sao lưu cũ: _dev/ren2/backup_pre_cast214.


## §215 — 10/11 ~17h45: Map 2 — "vùng trigger lỗi khiến khu vực đấy quái không spawn" -> khung r4_swarm lệch chỗ điểm sinh — ĐÃ SỬA tmx, CHƯA Play thử
- Map 2 trước khi sửa: md5 **a41637efc16b93bbf9656eba9d6bdea4** (người dùng lưu trong Tiled 10/11 17:25; so với bản sau §200 b1f3d2bd — dựng lại
  đúng md5 bằng cách chạy lại fix_1109/map2_fixes.py lên bản sao lưu, lưu ở `Catalog/_dev/map2spawn/post200_b1f3d2bd.tmx`). Thay đổi của người dùng:
  dời ~30 điểm sinh (r2, r3, arch, r6, feed, office, s2, r4_swarm), xoá spawn 682 (Skinned_Hopper của cult -> cult chỉ còn Brood_Toad), xoá toggle
  `gorgemaw_capsule` (bồn nuôi KHÔNG còn đổi sang hình rỗng sau khi Gorgemaw nhảy ra), dời 2 chân nến (433/434 + vật cản 830/831).
- Soát toàn bộ: 20 khu đều có lõi tới được (BFS lưới 1/4 ô từ player_start, mở mọi cổng trừ gallery_window), mọi spawn nằm trong khu của nó và tới được,
  27 prefab đủ, mọi cờ start_when (eye_glimpsed — EyeGlimpse.cs, ichor_diverted, cult_cleared) đều có nguồn bật, cổng close_on_enter (r8_lock, cult_lock)
  đúng phía. Khu không có quái theo thiết kế: r1, r4 (hub), rec, spec (trước cult), ctrl, e1, s3; r8 chỉ đánh sau ichor_diverted.
- **Gốc lỗi**: 7 điểm sinh r4_swarm bị dời vào sâu Eye's Cistern (quanh hố, x100-108 y44-49) nhưng KHUNG kích hoạt r4_swarm vẫn ở Sluice Hall (x76-88
  y48-56). Vào bể chứa -> không gì xảy ra; quay lại hall -> khu bắt đầu nhưng chuột sinh cách 19-27 ô (ngoài màn hình), đường đi 26-34 ô vòng hố ->
  AI kẹt (noProgress) quay về chỗ sinh -> không bao giờ tới, khu không dọn xong được. (Bản §200 cũng đã xa 11-17 ô.)
- **Sửa** (`Catalog/v2/tools/fix_1110/map2_swarm.py`, md5 a41637ef… -> **d3107f556aaca4f96d9b6b65e6c60090**, sao lưu `Catalog/backups/map2_2026-11-10/swarm/`):
  khung r4_swarm -> x91.5-113 y43.5-56 (ô) = nửa trái bể chứa ngay sau cổng r8_lock. Sau cảnh nhìn thấy Con Mắt, Ren bước vào ~3 ô là đàn chuột quanh
  hố lao ra (cách 10-16 ô, trên màn hình). Khung KHÔNG chứa Plague Eye (107.7,41.6) -> TryGetZoneAt (khu nhỏ nhất) vẫn là r8 cho ArenaSeal/boss.
  title "The Eye's Cistern", hint mới, không ambient (ZoneAmbience vẫn dùng của r8). Điểm sinh giữ nguyên chỗ người dùng đặt. Minimap không cần nướng lại.
- Bài học: điểm sinh có thể nằm ngoài khung khu (director không kiểm), nên khi dời spawn phải xem khung `zones` cùng zone_id; kiểm khoảng cách đường đi
  từ lõi khung tới spawn (≤ ~12 ô, trên màn hình). Khung mới không được chứa chỗ boss của khu khác (TryGetZoneAt chọn khu NHỎ nhất).

- **§215b (10/11 18h10, người dùng)**: khôi phục toggle `gorgemaw_capsule` (id 1514, nguyên văn bản §200) -> bồn nuôi lại đổi đầy -> rỗng khi Gorgemaw nhảy ra
  (`Catalog/v2/tools/fix_1110/map2_capsule.py`, md5 d3107f55… -> **6b3d02cf4b6b6d3085857b9185dd3aba**, sao lưu `Catalog/backups/map2_2026-11-10/capsule/`).
  Spawn 682 (Skinned_Hopper lẻ loi ở pha 1 trận Gorgemaw) GIỮ XOÁ theo ý người dùng: cult chỉ còn Brood_Toad; cóc con chỉ ra ở pha 2 (ToadSkills.Brood, tối đa 4).


## §216 — 10/11 18h20: chỉ số mặc định của Ren theo ảnh người dùng + lỗi Console "(GetStatus) Cannot get non-existing progress id 12."
- PlayerCombat.cs (mặc định code) + PlayerCombat trong 4 scene (Map1, Map2, Map3, Map3b): directions 8 hướng, damage 35, hitTime 0.48, lockTime 0.6,
  attackCooldown 0.5, moveCancelAfterHit 0.08, range 0.75, arc 140, innerRadius 0.3, pivotHeight 0.2, knockback 2, inputBuffer 0.2, heavyHitKnockback 3,
  staggerLock 0.45, roll 6 / i-frame 0.5 / cooldown 1.5, potions 3, healPercent 0.5 (Map3 trước là 1, các map khác 0.35), healLock 0.5.
  Sao lưu: `Catalog/backups/player_stats_2026-11-10/`. Lưu ý: damage/range/arc/knockback ở đây chỉ dùng khi KHÔNG cầm vũ khí — có katana thì lấy combo
  của WeaponDefinition (Katana 16/16/26). Bản lưu cũ có healPercent (0.35) sẽ ghi đè khi Continue. Bình đồng (flask_upgrade): +0.15, tối đa 0.8.
- Lỗi Console "(GetStatus) Cannot get non-existing progress id N": lỗi nội bộ Unity Editor (Progress API của cửa sổ Background Tasks, issue UUM-121640 /
  UUM-18620), dự án không có script nào gọi UnityEditor.Progress. Vô hại: Clear Console; còn lặp lại thì khởi động lại Unity (+ Hub).


## §218 — 10/11 ~22h: 3 thanh KATANA — cốt truyện, nội tại, nơi giấu, bảng vũ khí trong túi đồ — CHƯA biên dịch / CHƯA Play thử
**Phân tích bản cũ**: 3 asset `Resources/Weapons/*.asset` đã có art (design 64x32 + showcase 256x96) nhưng **Crimson Oath và Moonlit Edge
không nhặt được ở đâu trong game**, không có nội tại, không mang qua map, không lưu trong bản lưu, túi đồ có tab "Weapons" nhưng luôn rỗng.
DPS gần bằng nhau sẵn (Ronin 58 dmg/1,89 s ≈ 31; Moonlit 51/1,62 ≈ 31,5; Crimson 69/2,16 ≈ 32) -> người dùng chọn **giữ ngang sức, khác lối đánh**;
không đổi số liệu combo, chỉ thêm NỘI TẠI.

**Nội tại (`WeaponTrait`, chỉ tác động lên đòn CHÉM, Mantra không đổi)**
| Thanh | Nội tại | Hiệu ứng |
|---|---|---|
| Ronin's Katana | Counterstroke | nhát chém ĐẦU trong 1,2 s sau khi lăn: **+50%** (một lần mỗi cú lăn) |
| Moonlit Edge | Vigil | 5 s không trúng đòn -> lưỡi kiếm bừng sáng (`PlayerWeaponView.TraitGlow` 1,75): **+15%**; trúng đòn là tắt |
| Crimson Oath | Blood Oath | mỗi nhát trúng hồi **6%** sát thương gây ra; máu **< 40%** thì **+25%** |

**Nơi giấu (người dùng chọn)**
- **Moonlit Edge** — Map 2, khu bí mật `s3` *Reliquary of the Ascended* (sau rào gỗ `s3_door`). Bệ đá + vải chàm ở góc đông-bắc (ô 51,3 / 88,1),
  đèn vigil xanh không bao giờ tắt treo trên. Bia `reliquary_relic` (cơ quan `note`, verb "Take the blade") -> nhặt kiếm, kiếm biến mất khỏi bệ.
- **Crimson Oath** — Map 3b *King's Barrow*, nằm trên nắp quan tài Vua cuối cùng của Vael (ô 8,75 / 6,05). Bia `barrow_oath` (note, "Take the blade").
  **Bỏ biển `king_bones` (id 4511)**: câu của nó ("He drank the most, and he drowned the first.") thành dòng mở đầu của bia -> chỉ còn MỘT chỗ tương tác.

**Code mới**: `Player/Weapons/WeaponTrait.cs` (enum + số liệu + `WeaponTraits.MeleeMul/AfterMeleeHit/Active`),
`Player/Weapons/WeaponRing.cs` (sở hữu bằng cờ `weapon_<id>`, thanh đang đeo, tóm tắt chỉ số cho UI).
**Vá** (`Catalog/v2/tools/katana217/patch_code.py`, `patch_ui.py`, `patch_assets.py`): WeaponDefinition (+epithet/lore/foundAt/trait);
PlayerCombat (LastRollTime / CounterReady / LastHurtTime qua `Health.Damaged`, ánh kiếm nội tại, Equip nhớ thanh đang đeo);
PlayerWeaponView (+TraitGlow); PlayerDodge (mở cửa sổ đòn phản); PlayerMelee (nhân sát thương + hút máu);
MapTriggerHandler (`item = weapon`, `weapon_id`); NoteMechanism (`weapon_id` -> WeaponRing.Give);
GameSession (cờ `weapon_` đi theo map như `book_`; NewGame reset); SaveGame (`weapon` = id thanh đang đeo);
MapEncounterDirector (nạp xong thì đeo lại thanh của lượt chơi).

**Túi đồ (RequiemMenus)**: ô katana đứng TRƯỚC đồ then chốt, chấm vàng ở ô thanh đang đeo; bảng riêng `WeaponDetail`:
tranh trưng bày 256x96 (x1,25) trong khung vàng -> tên + huy hiệu "AT YOUR HIP" + epithet -> 4 thanh chỉ số (CUT / SPEED / REACH / IMPACT,
so với thanh cao nhất trong game) -> "THE THREE CUTS" (từng nhát: tên, sát thương, thời lượng) -> khối NỘI TẠI viền vàng ->
"THE TELLING" (cốt truyện) + "Found: …" -> nút **Draw this blade**. Bảng đồ then chốt cũ tách ra `KeyDetail` (không đổi).

**Art mới** (vẽ bằng code từ `ren2/designs.json` để khớp đúng bảng màu của từng thanh — không dùng PixelLab):
`Catalog/v2/tools/katana217/icons.py` -> `Resources/UI/Requiem/weapon_katana|katana_moonlit|katana_crimson.png` (64x64, độ dốc 2:1 cho nét liền);
`relics.py` -> `Props/SR-relics/relic_plinth.png` (48x24 bệ đá + vải), `relic_moonlit.png` (48x14 kiếm nằm ngang),
`relic_crimson.png` (16x56 kiếm nằm dọc) + 3 tileset `SR-relic-*.tsx`.

**md5**: Map 2 6b3d02cf… -> **965533efb035cd4708495084c4a4de6e** (tileset mới firstgid 14641 bệ, 14642 kiếm);
Map 3b 95cf1c7d… -> **280578cff633c3fc2a0991927ea2bfb4** (firstgid 4987). Sao lưu `Catalog/backups/katana217_2026-11-10/` (pre = code + asset, map2/, map3b/).
**Chưa làm**: chưa biên dịch, chưa Play. Minimap không cần nướng lại (không đổi tường).


## §219 — 11/11 03h40: THIẾT KẾ LẠI TÚI ĐỒ (người dùng: "thiết kế inventory này quá tệ") — CHƯA biên dịch / Play
**Đánh giá bản §218 (ảnh người dùng)**: (1) TRÀN KHUNG — lưới thò ra mép trái, thẻ chi tiết thò ra mép phải. Gốc: hàng thân co theo nội dung
(khung `sr-panel` align-items center) nên chữ dài không xuống dòng đo ra bề ngang rất lớn. (2) 20 ô trống ornate cho 1 món -> nhiễu.
(3) 5 tab to (240 px) kể cả tab 0 món, "Documents 0" bị cắt chữ. (4) Ảnh kiếm bé xíu giữa dải đen trống. (5) Thanh chỉ số chuẩn hoá theo
thanh mạnh nhất -> thanh nào cũng gần đầy, không nói lên gì; nhãn và số cách nhau cả bề ngang. (6) Phần nội tại/cốt truyện bị đẩy xuống
dưới thanh cuộn. (7) Nút "At your hip" mờ nhưng trông như nút chính + trùng nhãn "AT YOUR HIP". (8) Không so sánh được với kiếm đang đeo.
**Bản mới** (`Catalog/v2/tools/katana217/inv219.py`, sao lưu `Catalog/backups/inv219_2026-11-11/RequiemMenus.cs`):
- Khung 1600 (maxWidth 96%), thân cao cố định 660, `alignSelf Stretch`; thẻ phải `flexBasis 0` -> không bao giờ tràn, không nhảy kích thước khi đổi món.
- Tab chữ gọn, gạch chân vàng, ẨN tab rỗng (Documents -> "Notes"). Lưới 4 cột x ô 104, chỉ đủ hàng cho số món (tối thiểu 2, tối đa 5),
  ô phẳng tối viền mảnh; đang chọn = viền vàng + nền ấm; đang đeo = hình thoi vàng. Tên món đang chọn dưới lưới.
- Thẻ katana: ảnh 256x96 x2 (bội số nguyên) tràn mép trên, nền ánh màu vệt chém của thanh; tên 40 px + epithet; bên phải hành động chính
  "Draw this blade" (hoặc nhãn EQUIPPED). Hai cột: ATTRIBUTES (DAMAGE / SPEED / REACH / STAGGER — 10 nấc so với mốc CỐ ĐỊNH 80 / 2,2 / 1,2 / 5,
  số + chênh lệch xanh/đỏ "vs <kiếm đang đeo>") + THE THREE CUTS | khối INNATE + THE TELLING (chỉ phần cốt truyện cuộn).
- Thẻ đồ then chốt cùng ngôn ngữ: icon 128 trên dải tối, tên + nhãn loại (USED), OPENS / mô tả / PROPERTIES / WRITTEN ON IT.
- Điều khiển: rê chuột hoặc phím mũi tên = xem (focus = chọn, không dựng lại lưới nên không mất focus); Enter trên ô katana = đeo;
  chân bảng có dòng gợi ý phím + nút Close.
- Ảnh mô phỏng đúng đơn vị UI + phông thật: outputs `inv219_mock.png` (script /home/claude/mock/inv219.py, không nằm trong dự án).

- **§220 (11/11 03h45)**: F6 (DevCheats) = MỞ KHOÁ TẤT CẢ: mọi Mantra, sách nội tại (book_turned_blade, book_purifier, f_upgrade_burn), mọi katana (cờ weapon_<id>, kèm nội tại riêng của từng thanh), đồ then chốt của map + đầy bình. Không giải câu đố (F5). Sao lưu `Catalog/backups/inv219_2026-11-11/DevCheats.cs`.


## §221 — 11/11 ~04h: TÚI ĐỒ lần 2 (người dùng: ghét thanh cuộn kiểu Windows, nền tím + phối màu khó nhìn, chữ thừa, không muốn tab All)
**Đánh giá bản §219 qua 4 ảnh**: (1) ScrollView hiện thanh cuộn mặc định của Unity (nút mũi tên) -> trông như phần mềm văn phòng.
(2) Dòng nhắc "Arrows Browse" lơ lửng trên nền đá. (3) Nền `panel_stone` tím có mảng khối sáng + thẻ đen = hai bảng màu đánh nhau.
(4) Tab "All" trộn kiếm với chìa / giấy. (5) Thừa: tiêu đề danh sách "WEAPONS 3" trùng tab, tên món dưới lưới trùng tên trên thẻ.
(6) Lưới icon không cho đọc tên nếu không rê chuột; ghi chép nào cũng cùng một icon tờ giấy.
**Bản mới** (`Catalog/v2/tools/katana217/inv221.py`; sao lưu bản §219 `Catalog/backups/inv219_2026-11-11/RequiemMenus_v219.cs`):
- 4 mục riêng WEAPONS / KEYS / TOOLS / NOTES (bỏ All), có icon mục; mục rỗng mờ nhưng GIỮ CHỖ (thanh mục không xê dịch); Q / E hoặc LB / RB đổi mục.
- Trái: DANH SÁCH (ô icon 72 + tên + một dòng phụ: nội tại của kiếm / "Opens ..." của chìa / "Used"), dòng chọn = nền ấm + vạch vàng trái,
  kiếm đang đeo = chữ EQUIPPED. Bỏ lưới ô trống.
- Thẻ katana kiểu bảng thông số 2 cột: trái (ảnh 256x96 x2, tên, epithet, nút [Enter] DRAW THIS BLADE / nhãn EQUIPPED, ATTRIBUTES vs kiếm đang đeo,
  THE THREE CUTS) | phải (khối INNATE + toàn bộ THE TELLING — đủ chỗ, không cần cuộn). Thẻ chìa/dụng cụ: ô icon 140 + tên + nhãn loại.
  Thẻ ghi chép: trang đọc (tiêu đề + chữ, khổ chữ tối đa 860 cho dễ đọc), không icon to.
- Chữ dài: ScrollView ẨN thanh cuộn (cả hai chiều), cuộn bằng bánh xe chuột / cần phải tay cầm; dải mờ ở đáy chỉ hiện khi còn chữ bên dưới.
- Chân bảng = nhắc phím keycap bấm được ([Enter] Draw this blade, [B] Close) — bỏ nút Close to và dòng chữ lơ lửng.
- Một bảng màu: nền mực gần đen, mặt thẻ tối hơn một bậc, đường kẻ ĐỒNG ĐỎ (khớp art), vàng chỉ cho thứ đang chọn / tiêu đề / số liệu.
**Art PixelLab** (UI Creator, `POST https://api.pixellab.ai/ui-assets/generate` với pieces {kind rounded_rect, label, x,y,w,h,radius},
poll `/background-jobs/<id>` -> `last_response.image.base64` PNG; tốn 10 lượt/ảnh). Ảnh gốc `Catalog/_dev/inv221/A.png` (592x448):
khung cửa sổ đồng có hoa văn góc, thanh trang trí, ô vật phẩm, 4 icon mục. Cắt bằng `/home/claude/inv/cut.py` (không trong dự án):
PixelLab tự vẽ THANH CUỘN vào cạnh phải khung -> lật nửa trái sang; cạnh có đinh tán -> dựng lại 9-slice với cạnh là 1 hàng sạch lặp lại;
lòng khung đổ màu đặc (14,10,17); icon tách khỏi ô viền + bỏ mẩu góc. Ra `Catalog/_dev/inv221/`: inv_frame 112 (slice 44), inv_divider 120x36
(slice 44 ngang), inv_slot 48 (slice 16), cat_weapon/key/tool/note.
- Code đọc `Resources/UI/Requiem/inv/<tên>` — THIẾU ảnh thì tự vẽ phẳng bằng màu (không vỡ). CHƯA chép art vào Resources: chờ người dùng "nhập".
- Mô phỏng đúng đơn vị UI + phông thật + art: outputs `inv221_mock.png`.


## §222 — 11/11 ~04h15: tab WEAPONS = GIÁ KIẾM (người dùng thích tranh trưng bày) + thông số bên dưới; 3 mục — CHƯA biên dịch / Play
- Mục: WEAPONS / KEY ITEMS (chìa + dụng cụ giải đố gộp) / NOTES. `InvEntry.Kind` = Weapon | KeyItem | Document.
- WEAPONS: hàng 3 ô tranh trưng bày 256x96 x2 (màn hẹp < 540 -> x1,5) + tên + nội tại; chọn = viền vàng + nền ấm, ô khác tranh mờ 70%;
  EQUIPPED ở góc ô. Bên dưới: bảng 3 cột — DANH TÍNH (tên, epithet, [Enter] DRAW THIS BLADE / EQUIPPED, khối INNATE có số liệu tô vàng
  bằng rich text) | SỨC CHIẾN (DAMAGE / SPEED / REACH / STAGGER vs kiếm đang đeo + bảng THE THREE CUTS: CUT / DMG / TIME + tổng chuỗi) |
  THE TELLING (toàn bộ cốt truyện). KEY ITEMS / NOTES giữ danh sách trái + thẻ phải (§221).
- Rich text: `RequiemUI.Text` chạy TextUtil.Clean (đổi < > # thành ký tự khác) -> gán `label.text` trực tiếp sau khi Clean nội dung.
- Script `Catalog/v2/tools/katana217/inv222.py`; sao lưu bản §221 `Catalog/backups/inv219_2026-11-11/RequiemMenus_v221.cs`. Mô phỏng `inv222_mock.png`.
- Art PixelLab §221 vẫn ở `Catalog/_dev/inv221/` chờ "nhập".


## §223 — 11/11 ~04h55: GIÁ KIẾM khi có NHIỀU katana (người dùng hỏi) — CHƯA biên dịch / Play
- Trước: chia đều bề ngang cho mọi thanh -> thanh thứ 4 trở đi làm ô co hẹp, tranh bị cắt.
- Nay (`Catalog/v2/tools/katana217/inv223.py`, sao lưu `Catalog/backups/inv219_2026-11-11/RequiemMenus_v222.cs`): giá kiếm là các TRANG 3 ô
  (ô rộng cố định = 1/3 khung, tranh x2; ô hẹp < 520 -> x1,5). 1-2 thanh: căn giữa. > 3 thanh: mũi tên trái/phải hai bên (vẽ bằng viền xoay 45°)
  + vạch trang bên dưới (trang đang xem = vạch dài vàng); lật trang bằng mũi tên, bánh xe chuột, hoặc đi phím / tay cầm sang thanh ở trang khác
  (tự lật, trượt 0,22 s). Trang cuối luôn đủ 3 ô (lùi lại). Mở túi: nhảy tới trang có thanh đang chọn. Không thanh cuộn.
- THÊM MỘT KATANA MỚI (không cần sửa UI):
  1. `Resources/Weapons/<Tên>.asset` (Create ▸ Soul Requiem ▸ Weapon): id, displayName, epithet, lore, foundAt, combo, trait, slashStyle.
  2. Art: bảng thiết kế 64x32 (`ren2/designs.json` + `katana_design.py`), tranh trưng bày 256x96 `Weapons/Showcase/<Tên>.png`,
     icon `UI/Requiem/weapon_<id>.png` (`katana217/icons.py`), VFX chém `VFX/slash3/<style>_*` nếu có style riêng.
  3. Đặt trên map: cơ quan `note` + `pickup: true` + `weapon_id: <id>` (mẫu: `katana217/map2_relic.py`) hoặc pickup `item = weapon`.
  4. Nội tại MỚI (không phải Counterstroke / Vigil / BloodOath) = thêm một giá trị WeaponTraitKind + nhánh trong WeaponTraits.
  WeaponRing tự thấy mọi asset trong Resources/Weapons; giá kiếm tự xếp theo (thanh mở màn trước, rồi theo tên).


## §224 — 11/11 ~05h05: chuẩn hoá tranh trưng bày katana theo MẪU Crimson Oath (người dùng)
- MẪU: hàng trên = BAO KIẾM RỖNG, miệng bao bên PHẢI, cong võng xuống như lưỡi; hàng dưới = LƯỠI ĐÃ RÚT, mũi bên TRÁI, chuôi bên PHẢI.
- Ronin's Katana: bao cong NGƯỢC lưỡi -> lật dọc; cắt đoạn quấn hạt + nắp đen ở đầu phải (trông như chuôi thứ hai), thêm vành miệng bao sắt.
- Moonlit Edge: hàng dưới là cả thanh kiếm TRONG BAO (2 cây kiếm) + cả bộ ngược chiều -> cắt chuôi + chắn tay, thêm vành bạc miệng bao,
  lật ngang bao và lưỡi, bao lên trên.
- Ghi đè `Resources/Weapons/Showcase/Katana.png`, `Katana_Moonlit.png` (giữ .meta / guid). Sao lưu `Catalog/backups/showcase224_2026-11-11/`.
  Cách làm: `Catalog/v2/tools/katana217/showcase_fix.md`. Katana mới sau này phải vẽ theo đúng mẫu này.


## §225 — 11/11 ~05h15: BAO KIẾM dùng chung KHUÔN Crimson (bản §224 vá pixel chưa đạt)
- Phân tích bản §224: bao Ronin dày ~10 px (Crimson ~20), đuôi NHỌN như lưỡi; bao Moonlit ngắn ~150 px (Crimson ~220); bao không cong theo
  lưỡi; hai hàng mỗi thanh lệch cao độ -> ba ô giá kiếm không đồng bộ.
- Sửa: lấy NGUYÊN bao của Crimson Oath (hình, độ cong, nắp đuôi, vành miệng, nút dây sageo, đai) làm khuôn, phân nhóm điểm ảnh
  (viền / sơn mài + bóng sáng / phụ kiện vàng / dây-đai) theo sắc độ, tô lại theo bảng màu thiết kế:
  Ronin = sơn mài đen + vân đá, sắt, dây gỉ; Moonlit = sơn mài xanh đêm + đốm sao, bạc, dây xanh nhạt. Lưỡi giữ nguyên, canh cùng cao độ mẫu.
- Script `Catalog/v2/tools/katana217/saya_template.py` (đường dẫn container trong script — chạy lại cần sửa SRC/OUT).
  Ghi đè `Resources/Weapons/Showcase/Katana.png`, `Katana_Moonlit.png` (giữ .meta). Bản gốc trước §224: `Catalog/backups/showcase224_2026-11-11/`.
- Katana MỚI: chạy cùng script với bảng màu mới -> bao tự đúng mẫu; chỉ cần vẽ lưỡi đúng chiều (mũi trái, chuôi phải).

- **§225b (11/11 05h10)**: đoạn ĐAI QUẤN + dây gần miệng bao bị loang (gộp chung nhóm "dây" + kéo giãn độ sáng; điểm tối trong đai giữ màu xanh tím của Crimson). Sửa trong `saya_template.py`: tách ĐAI (trong thân bao, nới 2,5 px vì đai nhô cao hơn sơn) khỏi SỢI DÂY + NÚT (trên thân); đai tô dải tối gom 3 sắc độ, điểm tối nhất trong đai cũng tô lại. Đai phải TƯƠNG PHẢN với sơn mài như mẫu Crimson: Ronin (sơn đen) = đỏ huyết #2a100e-#7a3024; Moonlit (sơn xanh đêm) = gần đen #05070f-#1a2238. Ghi đè lại 2 tranh.

- **§225c (11/11 05h10, người dùng: "Moonlit dây phải màu xanh")**: đai quấn Moonlit đổi từ gần đen sang XANH của dây (#2c4874-#9cc4ec, 3 sắc độ), sợi dây + nút nâng sáng (#5078aa-#a8d0f4). Nguyên tắc: đai / dây mang MÀU DÂY của thiết kế từng thanh (bảng `saya.cord`), không bê nguyên màu đen của Crimson. Ghi đè Katana_Moonlit.png.

- **§221 NHẬP (11/11 05h06, người dùng "nhập")**: chép art PixelLab từ `Catalog/_dev/inv221/` vào `Resources/UI/Requiem/inv/` (inv_frame 112 slice 44, inv_divider 120x36 slice 44 ngang, inv_slot 48 slice 16, cat_weapon / cat_key / cat_note / cat_tool). Unity tự nhập ngay: filterMode 0 (Point), không mipmap, không nén. Túi đồ giờ dùng khung đồng + thanh trang trí + ô icon + icon mục thay cho khung phẳng. cat_tool chưa dùng (Key Items dùng icon chìa).

## §226 — 11/11: dọn animation rác (Unity + PixelLab)
- Cách xác định "rác": (1) Unity: đồ thị GUID từ .unity/.prefab/.controller/.asset/.anim (bỏ Assets/_Recovery); controller không ai trỏ = rác, .anim chỉ nằm trong controller rác = rác.
  (2) PixelLab: so bóng (alpha bbox + mặt nạ 16x16) từng khung với 1.120 khung Ren trong game cùng hướng; animation không có khung nào lệch ≤6 bit = rác.
  Script: ~/refs2.py, ~/gsig.py, ~/match.py (scratch device_bash); danh sách `Downloads/pl_sigs_1111.json`, `Downloads/pl_delete_1111.json`.
- Unity -> `_to_delete/2026-11-11/`: toàn bộ Rogue Traveler (Player.controller, 95 .anim, 655 PNG) TRỪ `breathing-idle/south/frame_000.png` (4 scene còn trỏ làm sprite mặc định của Player);
  Greystone_Seer/_B, Greystone_Warrior/_B (4 controller, 24 .anim, prefab, definition — bỏ từ §14). Còn 422 .anim, 46 controller.
  PlayerAnimatorBuilder.cs (menu legacy Iaido) vẫn giữ để không vỡ SoulRequiemMenu, nhưng KHÔNG chạy được nữa (không còn khung RT).
- PixelLab: xoá 228 animation (f8705d6f 133, Ren Katana Sheathed 13, khung khoá 4, Rogue Traveler 78) + 58 nhân vật (Ren Unarmed, Katana Key, Katana Saya cũ, Ren Katana,
  Sword Ref, Empty Hands, Hand Fix A/B, 50 nhân vật khung khoá 0 animation). Giữ: f8705d6f (182 anim), 74eb2346 (101), 5 khung khoá có anim dùng trong attack, NPC/quái "Idle", Commander, Rogue Traveler (0 anim, mẫu style).

## §227 — 11/11 ~13h: bục sách thiếu khối chặn; Moonlit Edge rơi ra khi hạ Specimen Zero (bỏ bệ ở Reliquary)
**1. Bục sách xuyên qua được** (ảnh người dùng): 3 bục `lectern_book` của Map 2 — `r6_journal_item` (ô 10,3/47), `office_ledger_item` (120,1/72,5),
`spec_chart_item` (5,3/63) — không có obstacle. **Gốc lỗi**: 3 bục thêm ở §200 bằng script chỉ ghi tile object; khối chặn của vật cũ do
bước sinh collider làm trước đó nên bục mới không có. **Sửa**: obstacle 32x16 ở chân bục (như `SR lectern` 820), **đặt cùng tên `<note>_item`**
-> bục chỉ hiện khi dọn xong phòng (show_when) thì khối chặn cũng bật / tắt theo (`Ctx.Show` bật mọi object cùng tên).
**Phòng lại**: luật mới HANDOFF mục 6 + công cụ `Catalog/v2/tools/qa_prop_colliders.py` (chỉ đọc): liệt kê vật "cứng" (tên có lectern / table /
barrel / crate / chest / statue / pillar..., cao >= 20 px, hoặc >= 60% chỗ dùng cùng hình đã có khối chặn) mà chân không đè khối chặn nào;
bỏ qua lớp wall props / mechanics_art / props front và vật ngoài map. Hiện còn 66 nghi vấn (Map 2: 21, Map 3: 23, Map 4: 9, Map 1: 13) —
nhiều cái có chủ ý (bể nuôi có khối chặn riêng của TankMechanism, rương đặt trên kệ...) -> chờ người dùng soát.
**2. Moonlit Edge** (người dùng: bỏ bệ kiếm, đánh xong boss phòng bí mật mới rơi ra kiếm, ngay trong phòng):
- Map 2 bỏ bệ `reliquary_plinth` 1839 + khối chặn 1840 + đèn kiếm 1843. Giữ đèn vigil `reliquary_lamp` + chân nến (lời kể: đèn vẫn cháy trên chỗ trống).
- Kiếm (note 1841 `reliquary_relic`, giữ id / `sets moonlit_taken` / `weapon_id katana_moonlit`) chuyển vào Containment Vault, chân tượng
  trước chỗ Specimen Zero (ô 70 / 100,2): `show_when vault_cleared`, `show_delay 2.4`, `drop`, `show_hint`, `shown_sets vault_relic_shown`.
  Đèn mới `vault_relic_glow` (lit_when vault_relic_shown, off_when moonlit_taken). Lời bia + `Katana_Moonlit.asset` (lore, foundAt) viết lại:
  lính canh của Malric tìm thấy kiếm trên xác các tu sĩ, thấy nó "giữ yên thứ nó chém" với sinh vật Ether -> đóng vào ngực Kẻ Ngủ.
- `NoteMechanism` (dùng chung): có `show_when` thì HÌNH cũng ẩn tới khi có cờ (trước chỉ ẩn `<tên>_item`); thuộc tính mới `show_delay`
  (đợi + đợi cảnh diễn cổ vật xong), `drop` (rơi từ cao 0,9 đơn vị, nảy, rung máy quay), `show_hint`, `shown_sets`. Nạp bản lưu: hiện ngay, không chữ.
- Bản lưu đã nhặt kiếm ở bệ cũ: có `moonlit_taken` -> kiếm không hiện lại.
**md5 Map 2**: 965533ef… -> **9a3332a0ae475cd919f43d12829f41ce** (`Catalog/v2/map2_moonlitDrop_lecternCols.py`); sao lưu `Catalog/backups/moonlitDrop_2026-11-11/`.
Minimap đã nướng lại. **Chưa biên dịch** (NoteMechanism).

## §227 — 11/11: nhát 2 (attack2) hướng South làm lại — ĐÃ NHẬP
- Lỗi cũ: khung 1-2 quay mặt đi; khung trúng (3) lưỡi cắm xuống chân; nhảy thẳng từ giữa lên vai không có khung vung; 3 khung lấy đà đứng im.
- Phương án A (người dùng chọn): thấp PHẢI màn hình (hông trái Ren) -> vòng qua trước -> cao TRÁI = đi ngược đường kesa nhát 1. Nối combo ở khung 6 nhát 1 (cancelFrame 6) -> khung 0 nhát 2.
- Nguồn: PixelLab V3 `a2s227_B` (f8705d6f, id cf328f9e…, south, 6 khung, khung đầu = 2:1 sửa mặt, khung cuối = 2:4, target_palette khoá). Đã xoá bản thử A/C.
  Thứ tự: f0..f6 = V3 0..6, f7 = f6. Sửa miệng khung 1-3 (chép vùng miệng khung 0), viền đen mép ngoài, tsuba vẽ lại sát tay (khung 1-3), bỏ điểm lẻ, bake UV.
- Script: `Catalog/v2/tools/ren2/v3/v3k/a2s_pl.py` (ghép + sửa miệng), `a2s_quality.py` (viền/tsuba/UV). Gói: `Catalog/_dev/ren2/a2s227/`. Sao lưu cũ: `Catalog/backups/a2s227_2026-11-11/`.
- Giữ nguyên .meta (guid, pivot) -> không cần dựng lại Animator. hitFrame 3 = lưỡi quét qua trước người sang thấp trái.
- Các bản thử bằng code (v3k, v3k2, v3k3) bị loại: ảnh mờ (vệt bán trong suốt + GIF dither), kiếm vẽ code gãy giữa các khung.
- §227e (11/11 14:25) — ĐÃ NHẬP: nhát 3 South GIỮ NGUYÊN động tác (người dùng chọn), chỉ sửa mặt quay 3/4 ở khung 4-5: ghép đầu nhìn thẳng lấy từ khung 7 nhát 3
  (`Catalog/v2/tools/ren2/v3/v3k/a3s_head.py`, căn đỉnh tóc + tâm ngang; chỉ đổi hàng 67-104). Ảnh _kuv không đổi (vùng đầu không có điểm katana). Sao lưu: `Catalog/backups/a3s227_2026-11-11/`.
  Bản làm lại nhát 3 bằng PixelLab (yoko/rec) bị loại theo ý người dùng, đã xoá trên PixelLab. Còn giữ `a1s227_kesa_2` (bản làm lại nhát 1, CHỜ người dùng quyết).
- §227g (11/11 14:36) — ĐÃ NHẬP: nhát 3 South khung 2 và 5 cầm 1 tay -> thay bằng khung PixelLab V3 hai tay (giữ nguyên các khung khác):
  khung 2 = `a3s227_x1` khung 1 (3:1 -> 3:3), khung 5 = `a3s227_y1` khung 1 (3:4 -> 3:6). Viền/tsuba/điểm lẻ + bake UV. Script `v3/v3k/a3s_hands.py`.
  Sao lưu: `Catalog/backups/a3s227g_2026-11-11/`. Khung 6-7 (thu kiếm về thế nghỉ) vẫn 1 tay theo tư thế đứng cũ.

## §228 — 11/11: làm lại bảng Shrine of Rest (mantra) theo ngôn ngữ túi đồ
- Người dùng: thanh cuộn mặc định thiếu chuyên nghiệp, nền tím khó nhìn, bộ chiêu phải ở mục riêng, mỗi chiêu cần đủ thông tin.
- Code (ĐÃ GHI, chưa biên dịch thử trong Unity): `RequiemMenus.cs` thay toàn bộ khối REST/SKILLS (SetCard, AddPassives, FillSocket bị thay):
  khung `inv/inv_frame` 1840x1000, tiêu đề + dòng phụ, 3 mục Q/E: Mantras | Mantra Sets | Passives (icon `inv/cat_mantra|cat_set|cat_passive`, thiếu ảnh thì bỏ icon);
  cột SOCKETS (3 ô, phím F/R/C, tên + loại + hồi chiêu, ô chọn viền vàng) | danh sách (không thanh cuộn) | thẻ chi tiết: icon 128, tên, LOẠI - COOLDOWN,
  nút "Attune to F" / chip ATTUNED, EFFECT (mô tả) | DETAILS (bảng nhãn/giá trị). Bộ chiêu: THE SET + từng chiêu (phím, icon, loại, hiệu ứng, bảng thông số).
  Nhắc chân: Enter Attune, Del/Backspace (tay cầm X) Clear socket, Esc Leave. Update gọi InvTick khi bảng nghỉ trên cùng (Q/E, cần phải cuộn).
- `SkillDefinition`: `virtual string Kind`, `virtual void Stats(list)`, tiện ích Tiles (1 ô = 0,32 đv) / Secs / Pct; 9 chiêu override Kind + Stats (giá trị THẬT của asset).
  Script vá: `Catalog/v2/tools/shrine228/patch_skills.py`, khối UI `rest_ui.cs.txt`. Sao lưu: `Catalog/backups/shrine228_2026-11-11/`.
- Art PixelLab (ui-assets, 2 lần x 10 lượt): `Catalog/_dev/shrine228/` icons_raw/icon2_raw -> cat_mantra (lửa Ether trên lòng bàn tay; bản đầu là chữ "R" -> bỏ vì trùng phím R),
  cat_set (3 cầu nối chỉ vàng), cat_passive (sách mở). Tách bằng `cut.py`. Mô phỏng: `mock_shrine228.png`. CHỜ "nhập" để chép vào Resources/UI/Requiem/inv/.

## §229 — 11/11 ~16h: cảnh mở đầu Map 1 không thấy goblin, cảnh Grok, bỏ qua cảnh, bình máu tối đa mất, Earthshatter 10 s — CHƯA biên dịch / Play
Sao lưu code + asset trước khi sửa: `Catalog/backups/cutscene229_2026-11-11/`.
- **Cảnh mở đầu chỉ thấy chữ, không có goblin đốt nhà**: goblin là quái của khu `zf`, sinh khi khu BẮT ĐẦU. Từ 08/11 khu chỉ bắt đầu khi Ren vào sâu
  `entryDepth` (3 ô) so với mép khung; player_start Map 1 chỉ cách mép dưới khung zf 2,4 ô và Ren bị khoá đứng yên suốt cảnh -> khu chưa bắt đầu,
  camera lia tới trang trại trống. Sửa: `MapEncounterDirector.ForceStartZone(id)` (đợt đầu sinh TỨC THÌ, không bụi báo hiệu);
  `OpeningCutscene` gọi cho khu chứa điểm `intro_cam_1` (`TryGetZoneAt`, không viết cứng "zf") trước khi lia camera. Quái đứng yên vì CutsceneState.
- **Bỏ qua cảnh**: bỏ Space/Enter ở MỌI cảnh cốt truyện (OpeningCutscene, RaidAftermath, LandingCutscene, BossRelic, EscapeSurge, EyeGlimpse) + bỏ chữ
  "(Space to skip)". Chỉ `BossIntro` (giới thiệu boss) còn bỏ qua được. Hội thoại (DialogueRunner) vẫn Space/Enter = câu tiếp.
- **Cảnh Grok đánh bay 2 goblin không hiện / gặp lại boss vẫn đi lại được**: lần gặp lại (cờ `bossintro_<tên>` đã có — đã xem / gục rồi quay lại / bản lưu)
  chỉ hiện bảng tên, đệ nằm choáng sẵn (không có cú đánh bay), boss + Ren cử động ngay. Sửa `BossIntro.Run`: gặp lại cũng là cảnh ngắn có khoá
  (khoá phím, CutsceneState -> quái đứng yên, Ren bất tử, camera về boss): không hội thoại / màn xuất hiện, vẫn gồng-nện-đánh bay đệ + bảng tên 2,2 s;
  Space/Enter bỏ qua. Lần đầu không đổi.
- **Bình máu tối đa (rương +1) mất khi sang map / chết ở map sau**: `GameSession` chỉ mang theo SỐ bình đang có, không mang `maxPotions` -> scene mới về 3,
  hồi sinh nạp lại 3, bản lưu ở map sau ghi 3. Sửa: `GameSession.MaxPotions` (Capture / ApplyTo / NewGame). Bản lưu đã ghi 3 ở map sau thì không tự khôi phục.
- **Earthshatter (chiêu dậm sóng xung kích)**: `Resources/Skills/GroundSlam.asset` cooldown 5 -> **10**.

## §229 — 11/11 ~22h: Crimson Oath trong hầm King's Barrow — kiếm mới + nguyên nhân không thấy kiếm — CHƯA dựng scene / Play
- **Người dùng**: "chưa có asset kiếm hiện trong hầm".
- **Nguyên nhân 1**: `Scenes/Map3b_KingsBarrow.unity` dựng 10/11 11:18, TRƯỚC §218 (thêm `barrow_oath` + kiếm vào .tmx) -> scene không có bia lẫn kiếm.
  Sửa .tmx xong mà không chạy "Dựng scene" thì game vẫn chạy bản cũ (Map 2 cũng vậy: chưa có `reliquary_relic`).
- **Nguyên nhân 2**: sprite cũ 16x56 vẽ code, đứng thẳng, trông như cây gậy, cao gần bằng nắp quan tài.
- **Mới**: `Props/SR-relics/relic_crimson.png` 36x56 (giữ .meta) — katana trần nằm chéo trên nắp (chuôi dưới-trái, mũi trên-phải), lưỡi cong nhẹ,
  hamon choji đỏ, rồng vàng chấm trên thân, tsuba vàng sukashi, chuôi quấn đỏ-đen + menuki/kashira vàng, bóng đổ xuống nắp. Màu từ `ren2/designs.json` Katana_Crimson.
  Script: `Catalog/v2/tools/katana217/oath_sprite.py <designs.json> <out.png>`.
- `SR-relic-crimson.tsx` 16x56 -> 36x56; object `barrow_oath` x 280 -> 270, w 16 -> 36 (tâm giữ 288, đáy 193.6).
  md5 map3b 280578cf… -> **74f3f5aa4eb43e023f7232060234576c**. Sao lưu: `Catalog/backups/oath229_2026-11-11/` (png, tsx, tmx).
- Cần: Soul Requiem ▸ Dựng scene Map 3b (King's Barrow) (hoặc Build All Map Scenes để Map 2 có luôn Moonlit Edge).

## §230 — 12/11: SOÁT + CHỈNH COLLIDER VẬT Map 1 / 2 / 3 / 3b (KHÔNG đụng Map 4) — ĐÃ GHI .tmx, CHƯA dựng scene / Play
- Luật (áp cho mọi map sau): collider = DẤU CHÂN vật (góc 3/4), không phải bóng hình. Vật tròn -> **elip** trong Tiled (phím C; ST2U: elip
  -> PolygonCollider2D 32 cạnh, rộng = cao -> CircleCollider2D). Khúc gỗ nằm / cụm thùng, bao -> **viên thuốc** (polygon). Vật vuông (nhà, hòm,
  bàn, kệ, rào, bia mộ, xe, cổng) giữ chữ nhật. Code game chỉ dùng `Collider2D` chung (LineOfSight, RingPillar...) nên không cần sửa code.
- Công cụ `Catalog/v2/tools/colliders/`: `collider_shapes.py` (chạy thử -> out/, `--apply` = sao lưu + ghi; chạy lại an toàn),
  `sheet.py` (ảnh collider theo nhóm), `fit.py` (đo lệch chân hình / mồ côi), `near.py` (đo khung đặc của vật quanh 1 collider),
  `compare.py` (đỏ cũ / xanh mới). Ảnh: `Catalog/previews/colliders/` (cmp*.png). Sao lưu: `Catalog/backups/colliders_2026-11-12/`.
- Kết quả: Map 1: 265 elip + 8 viên thuốc; Map 2: 30 elip; Map 3: 75 elip; Map 3b: 4 elip. Sửa riêng:
  Map 1 xoá 2 collider mồ côi (#3100 đá bên suối, #3139 rào gai chắn nửa lối đất), cột đèn #4133 lệch 14 px, bao #4138 lệch 13 px,
  quán trọ #4136 thòng 32 px xuống cỏ -> polygon theo chân tường + bậc cửa, 4 lửa trại -> elip ôm vòng đá;
  Map 2 cột #784 lệch 7 px, bể culture vat #852/#853 và Secret Lab #840: 1 khung phủ khe -> elip riêng từng bể/bình (id mới 1848-1851, cùng tên).
- Kiểm tra rò (raster 1 px, chân Ren viên thuốc 19,2 x 11,2 px): bo góc 7 cây sát mép tây Map 1 (dải rừng x64-128) mở khe chéo vào rừng
  -> giữ chữ nhật (KEEP_RECT). Sau đó: 0 rò, 0 lối bị chặn, 0 điểm sinh / trigger / điểm hồi sinh lửa trại bị đè.
- Lỗi CÓ SẴN (không do đợt này): Map 3 checkpoint #4499 `rest_unnamed` — điểm hồi sinh (tâm x, đáy-10 px) nằm trong vật cản.
- md5: map1 0a49ec62…, map2 91b19ff9…, map3 8e9f025b…, map3b e2eaf06a….
- CẦN: Unity Ctrl+R (Map 1 dùng thẳng prefab map -> tự cập nhật) rồi **Soul Requiem ▸ Scenes ▸ Build Map 2 / Map 3 / Map 3b Scene** (hoặc Build All Map Scenes);
  scene Map 2/3/3b giữ bản map cũ tới khi dựng lại. Play thử đi vòng quanh cây/đá/altar.
- Nếu Tiled đang mở map1: chọn Reload khi Tiled hỏi, ĐỪNG lưu đè bản cũ.

## §230b — 12/11 (lần 2, người dùng: "nhiều collider bị lệch"): CĂN COLLIDER THEO ĐIỂM ẢNH THẬT — ĐÃ GHI .tmx, CHƯA dựng scene / Play
- GỐC LỖI: bước sinh collider cũ đặt khung theo KHUNG ẢNH (giữa canvas + đáy canvas), không theo điểm ảnh -> cây có thân lệch giữa ảnh
  (~1/3 số cây Map 1 lệch ~9,5 px; cây chết Map 3 lệch 25-48 px), giá treo đèn #3077 lệch 14 px, cọc biển #3098 9 px, đá 3-7 px,
  đầu lâu cắm cọc Map 3 ~8 px, cột #784 Map 2 7 px; nhiều khung RỘNG hơn chân vật (bàn 109 -> 87, kệ, chòi canh, giếng...).
- Công cụ mới `Catalog/v2/tools/colliders/collider_fit.py` (+ `footprint.py`): luôn đọc BẢN GỐC `Catalog/backups/colliders_2026-11-12/*.tmx`,
  ghép collider <-> đúng tile object (cùng tên ảnh), đọc alpha (bỏ mảnh rời nhỏ):
  CÂY -> tìm THÂN (cột tô kín >= 60% ngay trên rễ) đặt elip dưới thân; vật khác -> dải chân (cao = độ sâu collider cũ): căn giữa, đáy = đáy hình,
  rộng = min(rộng cũ, rộng chân); lều orc #3145 + đống gỗ "wood log 27" -> polygon bao lồi chân. Nhà/cầu/rào/cổng/vòm/cơ quan lớn: giữ khung.
  Lệch quá lớn (>24 px, cây >40 px) thì bỏ qua + ghi log. `collider_shapes.py` (lần 1) giờ chỉ là thư viện luật hình — ĐỪNG chạy lại.
  Log từng object: `tools/colliders/out/fit_log_applied.txt`. Ảnh đỏ cũ / xanh mới: `Catalog/previews/colliders/v2*.png`.
  Bản sau lần 1 lưu ở `Catalog/backups/colliders_2026-11-12/after_v1/`.
- Số liệu: Map 1 299 object (270 elip, 7 viên thuốc, 4 polygon, 16 chữ nhật căn lại, 2 xoá), Map 2 67, Map 3 87, Map 3b 4; 254 object được dời/thu hẹp.
- Kiểm tra rò (so với bản GỐC): 0 rò, 0 lối bị chặn; checkpoint z2_fire: điểm hồi sinh (đáy khung -10 px) vẫn trống.
- md5: map1 ffc7b6ec…, map2 383ca021…, map3 9938bbc6…, map3b 5b37cfdd….
- CẦN như §230: Ctrl+R, Build Map 2 / 3 / 3b Scene, Play thử. Tiled đang mở map -> Reload.

## §230c — 12/11: Map 3 lăng mộ mái nhọn THIẾU collider (người dùng) — ĐÃ GHI .tmx, CHƯA dựng scene
- Lăng `crypt 1` (#1357, x272-432 y1562-1779, khu v5) không có khối chặn nào -> thêm `crypt 1` #5464 rect (284,1715,140,64) = thân + 2 bệ cột.
- Chân rào sắt trước 2 lăng `crypt 4 and 6 fence` (#1152/#1153) thò dưới khung lăng 14 px -> thêm #5465/#5466 (48x14).
- Thêm qua bảng `ADD` trong `collider_fit.py` (chạy lại vẫn giữ). Kiểm tra rò: 0. md5 map3 a850d618….
- Công cụ soát vật lớn thiếu khối chặn: `Catalog/v2/tools/colliders/missing.py <map.tmx>` (chỉ đọc; nhiều mục là cố ý: cơ quan tự tạo collider, nến, cây chồng ẩn/hiện).

## §231 — 12/11: LÀM MỜ VẬT CHE (DepthEffects) theo ĐIỂM ẢNH thật — ĐÃ GHI code, CHƯA biên dịch / Play
- Người dùng: đứng TRƯỚC / cạnh lều orc mà lều vẫn mờ. Gốc lỗi: `Scripts/Visual/DepthEffects.cs` so KHUNG CHỮ NHẬT của sprite với khung
  nhân vật (vùng trong suốt của ảnh lều / cây cũng tính là che) + so "trước/sau" bằng `transform.y` (không theo luật xếp lớp thật).
- Mới: vật chỉ mờ khi (a) vật VẼ SAU nhân vật đúng luật Unity (sorting layer -> order -> điểm xếp Pivot/Center theo trục Y) VÀ
  (b) >= 30% điểm ảnh ĐẶC của thân Ren (lưới 12x18 trên khung ảnh, chỉ lấy điểm có alpha) nằm dưới điểm ảnh ĐẶC của vật.
  Hiện lại khi < 12% (trễ, không nhấp nháy). Kênh alpha đọc 1 lần / ảnh (ảnh không bật Read/Write -> Blit + ReadPixels), lưu cache.
  Quét lại danh sách vật mỗi 4 s (vật hiện/ẩn theo cơ quan); vật bị ẩn mà đang mờ được trả độ đục.
- Thông số (Inspector object DepthEffects khi Play): enterCoverage 0.3, exitCoverage 0.12, fadedAlpha 0.35, fadeSpeed 8, minOccluderHeight 0.45.
- Sao lưu bản cũ: `Catalog/backups/depthfade_2026-11-12/DepthEffects.cs`.

## §231b — 12/11: sửa §231 "không hoạt động" — ĐÃ BIÊN DỊCH + PLAY THỬ (Map 1)
- Gốc lỗi: bản §231 dùng `Bounds.Intersects` -> xét cả trục Z; Ren z = 1, vật ST2U z = 0 -> không bao giờ "chạm" -> không bao giờ mờ.
  (Log chẩn đoán xác nhận: z vật 0.00 / người 1.00.) Sửa: chỉ so trên mặt phẳng XY.
- Thêm: gộp HỢP mọi vật che (rừng nhiều tán, mỗi tán che một ít) — Ren bị che tổng >= 30% thì mờ mọi vật góp >= 5% (`partCoverage`); hiện lại < 12%.
- `debugLog` (Inspector DepthEffects khi Play) in mỗi 0,5 s: tên vật, vẽ đè?, % che, sortY + dòng "TỔNG bị che x% -> MỜ/rõ" (đọc ở Editor.log).
- Đã thử Play 12/11 17:37: rừng phía nam lối đi (cạnh rào gai z1_gate, x~15, y~-17): TỔNG 51% -> MỜ, tán cây mờ; đứng trước vật: vẽ đè=False -> không mờ.
  Console 0 lỗi. Assembly SoulRequiem.Visual.dll 10:35:47 UTC.

## §231c — 12/11: đứng TRƯỚC nhà mà nhà vẫn mờ -> sửa ĐƯỜNG XẾP LỚP vật (DepthEffects) — ĐÃ BIÊN DỊCH + PLAY THỬ
- Gốc lỗi THẬT (không phải logic mờ): ST2U xếp tile object theo ĐÁY KHUNG ẢNH. Quán trọ: chân tường y 2774, bậc cửa nhô tới 2806
  = đáy khung -> Ren đứng sát chân tường (chân 2774..2806) bị Unity vẽ SAU nhà -> làm mờ bật lên (đúng logic nhưng xếp lớp sai).
  Trước đây collider cũ thòng 32 px xuống cỏ để che lỗi này; §230 thu collider về chân tường nên lộ ra.
  Tương tự: cây thông / cây trụi có 6-12 px trong suốt dưới gốc -> đứng sát trước gốc cũng bị vẽ sau cây.
- Sửa (runtime, `DepthEffects.FixSortLine`, chạy khi quét map): mỗi tile object ST2U (gốc -> "(Pivot)" -> "(CF)" -> renderer):
  đo chân thật từ alpha (đáy đặc thấp nhất; vật rộng >= 64 cột không phải cây/bụi: nếu >= 50% cột có đáy trên cùng một đường +-3 px
  cao hơn đáy thấp nhất >= 4 px -> lấy đường đó = chân tường, bỏ bậc cửa/hiên). Dời "(Pivot)" lên d px, "(CF)" xuống d px (hình đứng yên),
  gắn SortingGroup (Default, order 2) vào "(Pivot)" -> vật xếp theo chân thật. Không đổi sprite / cây object (ẩn-hiện theo tên vẫn đúng).
  Tắt bằng `fixSortLines` (Inspector DepthEffects). `DrawsOverPlayer`/`SortY` đọc SortingGroup.
- Đã thử Play 12/11 17:46: áp sát chân tường quán trọ -> Ren vẽ TRƯỚC, nhà KHÔNG mờ; đi ra sau mái -> nhà mờ. Console 0 lỗi mới.
- Lưu ý: script nào đổi sortingOrder của vật map lúc chạy sẽ không còn tác dụng với vật đã gắn SortingGroup (hiện chưa thấy script nào).

## §232 — 13/11: "đánh hiện trúng mà không mất máu" (Greystone Spearman) — ĐÃ SỬA, ĐÃ PLAY THỬ
- Tái hiện được (log chẩn đoán): Goblin_Spearman trúng nhát 1 (60 -> 44) rồi mọi nhát sau landed=False, invul=True, held=False.
- GỐC LỖI: enum `EnemySpecial` bị CHÈN `Shell` vào giữa khi làm Map 2 (None, Shell, Spin, GroundSpikes). Unity lưu enum trong prefab
  bằng SỐ -> prefab Map 1 dựng trước đó đọc lệch: Goblin_Spearman (special 1 = Spin) thành SHELL = rùa của Map 2: trúng đòn là bất tử
  `shellTime` 2 s, hồi 3,5 s; Goblin_Hexer (Greystone Seer, 2 = GroundSpikes) thành Spin (xoay giáo thay vì gai đá).
  Hit VFX + dừng hình vẫn chạy vì PlayerStrikes bắn máu bất kể TakeDamage trả false.
- Sửa: `EnemySpecial` ghi số cố định None=0, Spin=1, GroundSpikes=2, Shell=3 (LUẬT: giá trị mới thêm Ở CUỐI, không chèn/đổi thứ tự —
  cũng áp cho mọi enum khác được lưu trong prefab/asset). Ichor_Snapper.prefab special 1 -> 3.
  `PlayerStrikes.Damage`: chỉ bắn máu / tính "trúng" (dừng hình, giật camera, nội tại kiếm) khi TakeDamage thật sự trả true;
  bật `debugHitbox` trên PlayerCombat thì Console in "[Strike] ... chạm nhưng KHÔNG nhận đòn (bất tử=..., chết=...)".
- Play thử 13/11 15:21 (z4 Ruins Path, 2 Spearman): máu giảm mỗi nhát, chết sau ~4 nhát. Console 0 lỗi.
- Sao lưu: `Catalog/backups/melee_2026-11-13/` (PlayerStrikes.cs, EnemyAI.cs, Ichor_Snapper.prefab).
- Còn cần kiểm: Greystone Seer (Hexer) giờ lại mọc gai đá dưới chân (đúng thiết kế) — chưa đánh thử; Ichor Snapper Map 2 vẫn thụt mai — chưa đánh thử.

## §233 — 15/11: LORE MỚI (file Cottruyenmoi.docx của người dùng) — bản đánh giá + ý tưởng Map 1–5 — CHỜ NGƯỜI DÙNG CHỐT, CHƯA SỬA GÌ TRONG GAME
- Người dùng gửi lore mới (lịch sử thế giới tới năm 780; hiện tại = 1176): Vael (hỗn mang) và Ellious = The Ascended (trắc ẩn) là HAI thực thể;
  vụ giết thánh = "Angel Bird" (1 TCN); Darwin (cựu tông đồ) giữ TIM, làm vua bất tử; hồ Nova / The Faint Lake -> ma cà rồng (nhà Vey, Aurel đời đầu);
  Luke + Emma mang đi mắt trái + 1 cánh tay; giáo phái = **Soul Requiem** (tiền thân Children of Light); Absalom ghép Specimen Zero (thiếu tim + mắt trái,
  hồi sinh mỗi 100 năm); **Malric là NGƯỜI (hậu duệ Luke), mất năm 776**, tạo Plague Eye từ mắt trái, câu đố ống = phong ấn của ông;
  780 Isolde Vey quản Ether xanh lá; thế giới chia Awakened / Unawakened (lễ The Awakening — chưa định nghĩa).
- Tài liệu đề xuất (Claude Doc): https://claude.ai/artifact/GNHDUwzU3J5kCeRvZR56Zx ("Soul Requiem – Đánh giá lore mới và ý tưởng Map 1–5").
  Ý chính: giữ bố cục Map 1–4, chủ yếu đổi chữ; chủ đề "ai cũng giữ một phần của Ngài" (Vey = máu, Malric = mắt, Absalom = tay trái, Darwin = tim, Ren = mắt);
  1176 = kỳ Specimen Zero trở lại; Ren = Unawakened / Candidate VII, từ Map 3 muốn gỡ Con Mắt; Map 2 xả bể = phá ấn Malric; NPC mới Pell (goblin còn trí người)
  thay Malric ở Map 4; Map 5 = The Faint Lake (gói Volcano), trùm cuối Darwin -> the Remade God; 3 kết Face / Silence / Requiem (trả tim, an táng);
  7 việc tử tế (mercy_1..3, name_reclaimed, parents_heard, candidate_vi_freed, mercy_5).
- 10 câu cần người dùng chốt nằm cuối tài liệu (Awakening là gì, tên giáo phái trong game, Ren là ai, Pell hay Malric, tay trái của Emma, trùm cuối,
  vị trí Map 5, đổi tên Borrow/Darwin, sửa mốc năm, Specimen Zero tuỳ chọn hay bắt buộc). HANDOFF mục 6 CHƯA đổi — chỉ đổi sau khi chốt.

## §234 — 15/11 23h: CHỐT 3 MÀU ETHER + BỎ MAP 4 CŨ
- **Màu Ether (người dùng chốt): chỉ 3 màu, BỎ TÍM.** Xanh dương = Ellious / Mantra của Ren. **Đỏ = Mantra của The Vael**: Ether nhiễm Vael,
  quý tộc + hoàng tộc dùng (nhà Vey, Darwin), kèm nghi lễ Soul Requiem, Requiem Door, Specimen Zero. Xanh lá = Plague Eye (goblin, Awakened).
  Cần đổi tím -> đỏ trong game (chưa làm): ánh Requiem Door Map 3, nhịp tim / ánh thức dậy Specimen Zero Map 2, mọi đèn/VFX tím khác.
- **Bỏ Map 4 cũ** (người dùng: cấu trúc lộn xộn, chưa xếp quái). Đã DỜI (không xoá) vào `_to_delete/2026-11-15_map4_old/` (giữ đường dẫn, kèm .meta):
  2 file map4_catacombs.tmx (Crypt + Old Prison), Scenes/Map4_CatacombsOfNaming.unity, Resources/Quests + Minimap Map4_CatacombsOfNaming.
  Unity đang mở lúc dời -> Unity sẽ tự bỏ khỏi AssetDatabase. GIỮ code bẫy/cơ quan, prisonkit/cryptkit (Catalog), gói hình.
  Hệ quả: Requiem Door (to_map4 -> map4_catacombs) Map 3 trỏ vào scene không còn; SceneTransition / Build Settings / menu Build Map 4 Scene còn tham chiếu.
  Map 4 mới sẽ thiết kế lại từ đầu theo lore mới (tài liệu GNHDUwzU3J5kCeRvZR56Zx mục Map 4). Tài liệu Map 4 cũ (AanV3WpvUS4FwNsFEZiGyi) và §148/§192–§198 = LỖI THỜI.

## §235 — 16/11 00h: KẾ HOẠCH SỬA MAP 1–3 THEO LORE MỚI — CHỜ NGƯỜI DÙNG CHỐT, CHƯA SỬA GAME
- Lore hoàn chỉnh (người dùng đã đọc, sửa 2 chỗ: Ether xanh dương MẠNH + do tín đồ gốc Children of Light dùng): https://claude.ai/artifact/CWFzGYRNCmYt15ffNcq7Ev
- Kế hoạch (đã soát từng zone / note / trigger trong 4 .tmx, có id vật thể): https://claude.ai/artifact/LzyfqZNMu1NwEJLH1p12ke
- Nguyên tắc: (1) chuỗi nhân quả 1176: ca viên Soul Requiem ("the hooded one", z3b_seer) xúi goblin đào Stone Ring (nắp ấn nhà Vey 780) -> Ether rỉ
  (giếng đắng, Grok biến dạng) -> Con Mắt thức ("Day thirty-one") -> đội thợ chết -> địa chấn sập vòng đá -> Ren phá ấn -> Ether tràn Vale.
  (2) Di tích ba vương quốc CHỈ nằm dưới đất trong map con (như King's Barrow), mặt đất = kỷ nguyên Vael. (3) Thưởng theo thời đại:
  ba vương quốc = katana + sách kiếm pháp (không Ether); Children of Light = Mantra xanh dương; Malric/goblin = xanh lá; quý tộc/giáo phái = đỏ (trả bằng máu).
- Map 2 tách 2 lớp: phòng thí nghiệm Malric (757–776: Lab, Archive, Records, Cells, Reliquary, Vault) / Crown Ether-works (780 -> tháng trước:
  Sluice, Pump Control, Feed, Boiler, Office, Vat Hall; Grisk, Brakk là thợ Awakened của nhà Vey). Pell = "Malric's clerk" trong office_ledger.
- Đổi tên khu: z3b Healer's Tent -> The Seer's Tent; z4 Ruins Path -> Cloister Ruins; cult Culture Hall -> The Vat Hall; v11 Mausoleum Forecourt ->
  The Precentor's Court; Pump Room Key -> Pump Control Key; Ichor Flask đổi tên.
- Hầm mới: 1b The Hermit's Hollow (nền nứt Old Copper Mine, phá bằng Earthshatter; katana Stillwater + Mantra Canticle of Stillness);
  2b The Kroven Deep (nắp hầm Forgotten Store; dùng lại code bẫy Map 4 cũ; katana Unbowed + sách The Broken Chain);
  3b King's Barrow = Borrow, thêm buồng The Red Field (Vael); The First Cup (cạnh Bower, mở bằng Vey Signet; katana Nightvow);
  3c The Hall of Three Crowns (đặt 3 kiếm vương quốc -> katana Concord).
- Mantra mới: Canticle of Stillness (M1b), Ether Amber (M2 Archive), Dirge (M3 Lychgate, đỏ), Amen (M3 Ossuary), Blood Tithe (di vật Isolde, mất nếu tha).
- NPC: Pell (M2 Lab), Aldous (M3 Commons). mercy_1 đổi: để lại green tonic [3033] cho goblin con (không thể mang thóc về vì rơi xuống Map 2).
- Thứ tự: A sửa chữ (32 đoạn) -> B đổi tên -> C tím sang đỏ -> D NPC + cutscene (16 mới, 7 sửa) -> E hầm ngầm + katana + Mantra. 9 câu chờ chốt cuối tài liệu.

## §236 — 16/11 00h30: NGƯỜI DÙNG CHỐT CẢ 9 CÂU (§235) + LÀM ĐỢT A, B, C — ĐÃ GHI FILE, CHƯA DỰNG SCENE / PLAY
- Chốt: chia Map 2 hai lớp; kẻ trùm mũ = Soul Requiem; mercy_1 = để lại tonic; Crimson Oath = kiếm Borrow, sáu kiếm thề -> Nightvow; làm cả 4 hầm;
  Mantra đỏ trả bằng máu + tha Isolde mất Blood Tithe; 4 tên khu mới; Pell + Aldous vẽ PixelLab; CÓ boss bí mật trong hầm.
- Script: `_tools/lore_1116/patch_lib.py` (sửa thuộc tính theo id object, giữ CRLF, tự sao lưu) + `wave_abc.py` (kiểm md5 trước khi ghi).
  Sao lưu bản trước: `_backups/2026-11-16_lore_wave_ABC/` (4 tmx, quest, asset, SpecimenZeroSkills.cs, Specimen_Zero.prefab).
- md5: map1 05df6db5… -> **5de4afa88affd2a64e859afc3ef0d9e2**; map2 383ca021… -> **bb0f16677c53c0491b7efe33abf07756**;
  map3 a089de15… -> **8bf61208361a3efc6a86699832f9ba92**; map3b 5b37cfdd… -> **9291ab1865dac614d98ab9f96c9b8002**. Cả 4 parse XML được.
- Đợt A (chữ): Todd/dân làng/bảng thông báo (thêm cáo thị the Gift)/thư Hob/bà đồng/rãnh đào/bình đồng/Grok intro_line3/nanh Grok (M1);
  rec_dossier, Journal of Malric, sổ cái (old clerk = Pell), trang người giữ cống (kẻ cầm đèn đỏ), báo cáo Crown Ether-works, bia Moonlit,
  di vật Specimen Zero (rễ đỏ), di vật Plague Eye, cuộn Blight Flask (M2); lệnh truy nã, lệnh BL (four hundred), bia Aurel, khoá Vey,
  Sermon of the Gift, Naming Register, Font of the Gift, ward đỏ, chuông "seven hundred years", Isolde/Morrow intro, Requiem Door, rose window (M3);
  bia barrow_oath = "The Purified King" (M3b). Asset: Katana (Corwen trao), Katana_Moonlit, Katana_Crimson (kiếm Borrow), IchorFlask -> "Blight Flask"
  (id skill giữ ichor_flask).
- Đợt B (tên): z3b The Seer's Tent, z4 Cloister Ruins, cult The Vat Hall (mọi "Culture Hall" -> "Vat Hall"), v11 The Precentor's Court;
  Pump Room Key -> Pump Control Key (mọi PUMP ROOM/pump room); quest JSON 3 map theo.
- Đợt C (một phần): đèn tím -> đỏ (#e05a64 / #c0303c): M2 id 1040,1086-1088,1225,1388,1465-1468,1599,1600, vault ambient #c8949c,
  vault_fight relic/wake/card; M3 5180,5189,5249,5418, v12 ambient #e0a8b0. SpecimenZeroSkills.cs tint + làn + bàn tay -> đỏ; prefab tint sửa tay.
  CÒN: ảnh VFX tím (Vessel mantras, rễ/bàn tay PixelLab), SkillKit.SpentTint (màu trạng thái, giữ).
- CẦN: Ctrl+R trong Unity, chạy Build All Map Scenes (Map 1, 2, 3, 3b), chơi thử đọc chữ. Đợt D (NPC + cutscene) và E (hầm) chưa làm.

## §237 — 16/11 01h: ĐỢT D — NPC + CẢNH CỐT TRUYỆN (StoryScenes) — ĐÃ BIÊN DỊCH (0 lỗi), ĐÃ DỰNG SCENE, MỚI CHƠI THỬ CẢNH MỞ ĐẦU
- MỚI `Gameplay/World/Cutscene/StoryScenes.cs` (director tự gắn): cảnh viết bằng dữ liệu Tiled, object type **story** trong lớp triggers.
  Thuộc tính trigger (enter | interact | flag | start), when, unless (có MỘT cờ là chặn), once, repeat, radius (đv), verb, range, script.
  Lệnh script (mỗi dòng, trong Tiled dùng \n): cam X,Y [zoom] · camren · wait · say NGƯỜI: a|b · narrate a|b · bubble ren|npc|X,Y: câu ·
  hint · shake BIÊN GIÂY · beat X,Y #màu N · glow X,Y #màu R GIÂY · show/hide tên · set cờ · face X,Y · walk NPC X,Y · fade out|in GIÂY ·
  slide Resources/ĐƯỜNG: lời|lời · slide end · exit TÊN_LỐI_RA. Kèm lời thì thầm của Con Mắt khi nghỉ ở điểm hồi sinh (eye_relic_taken, chưa eye_severed).
  StoryScenes.Busy: OpeningCutscene chờ cảnh "start" (tranh mở đầu) xong mới chạy.
- MỚI `Enemies/Bosses/BossMercy.cs`: boss quỳ, chém = kết liễu (cờ slainFlag), [T] "Spare her" = tha (spareFlag, tan thành dơi, Destroy -> khu dọn).
  IsoldeSkills: hết quan tài vẫn giữ sàn máu 5% -> chạm sàn thì StopAllCoroutines + BossMercy.Begin(ai,"mercy_3","isolde_slain").
- MapTriggerHandler: `ExitNow(tên)` + pickup có `unless`. Director: `ExitNow`, thêm StoryScenes. Menu Build All Map Scenes BỎ Map 4
  (lần chạy đầu đã dựng lại Map4 scene từ bản cũ -> đã dời vào _to_delete/2026-11-15_map4_old/Assets/Scenes/*.rebuilt_1116.unity).
- Ảnh: `Resources/Story/angel_bird_1..4.png` (PixelLab 256x144, meta tâm giữa); NPC `Resources/NPCs/pell|aldous|goblin_child`
  (PixelLab v3 Pell 66154674…, Aldous cd3bff08…, Goblin Child 585454ff…; breathing-idle + walking-6-frames S/E/N, đặt lên canvas 144 chân dòng 120
  như Todd; meta chép từ mb_todd). Scale Tiled: pell 0.52, aldous 0.66, goblin_child 0.6. Công cụ `_tools/lore_1116/makemeta.py`, `wave_d_data.py`.
- Dữ liệu (md5 cuối: map1 9d66595b…, map2 8d0e0d06…, map3 b3a87fd3…): Map 1 prologue (start), tremor_caves (z2_cleared), tremor_ruins (z4_started),
  ring_look (enter 94,12 4x10), give_tonic (interact, mercy_1; pickup z3b_tonic unless mercy_1) + NPC goblin_child (9,25).
  Map 2 thêm lớp npcs: Pell (10,37; show r6_cleared, hide r8_cleared), pell_meet, pell_page (mercy_2), pickup malric_page (Records Vault 12,7, cờ malric_page),
  hub_grate (51,56), pump_seal (console, lever_handle). Map 3 thêm lớp npcs: Aldous (86,37) + aldous_field (50,67, x1_wall_broken), outfall (start, kể
  giáo phái bắt Pell), commons_bell (v3_started), aldous_meet, aldous_field, morrow_door (v11_cleared -> exit to_map4, map 4 chưa có thì báo chưa xây).
- ĐÃ THỬ: biên dịch 0 lỗi; Play Map 1: tranh Angel Bird + lời kể -> cảnh cướp làng chạy tiếp, Console 0 lỗi.
  CHƯA THỬ: các cảnh còn lại, vị trí NPC có bị kẹt / che không, Isolde quỳ + tha, Morrow tự chuyển map.

## §238 — 16/11 01h20: CẢNH MỞ ĐẦU LÀM LẠI (người dùng: không spoil, giới thiệu thế giới hiện tại + hoàn cảnh Ren) — CHƯA CHƠI THỬ BẢN MỚI
- Người dùng: sự thật Angel Bird CHỈ lộ ở Map 4 -> các cảnh sự thật để sau. Đã GỠ cảnh `angel_bird_truth` (Map 3) và bia `luke_diary` (Map 2)
  vừa thêm (khôi phục đúng md5 map2 8d0e0d06…, map3 b3a87fd3…). Ảnh `Resources/Story/angel_bird_1..4` giữ lại cho Map 4
  (angel_bird_3 cũ vẽ cầu thang, không phải hồ -> bản hồ mới ở PixelLab job 6dd87fa7…, chưa tải).
- Prologue Map 1 (object `prologue`, md5 map1 -> **1d2b804ccc7a04b5b6a33df4f05695bf**): 7 tranh `Resources/Story/prologue_1..7` (PixelLab 256x144):
  1 kinh đô + Eternal King · 2 truyền thuyết chính thống (kính màu: hiệp sĩ Darwin chém vua Thrag, biểu tượng CHIM TRẮNG trong mặt trời —
  người dùng cấm vẽ hình người kiểu Chúa Jesus) · 3 lễ Awakening (the Gift) · 4 Unawakened + goblin trên đồi · 5 Soul Requiem bị truy lùng ·
  6 Ren lang bạt trên đồng Greystone, đất rung (người dùng bỏ tranh rời nhà Corwen) · 7 BAN NGÀY, khói trên Millbrook. Không nói gì về sự thật.

## §239 — 16/11 01h40: CẢNH MỞ ĐẦU BẢN SỬ THI (7 chương) — map1 md5 dfa6948d80e3a756b7040df9338e2c10 — CHƯA CHƠI THỬ
- Người dùng: bản §238 thiếu cao trào, thiếu chiến tranh ba nước + công thống nhất của Darwin, thiếu nguồn gốc (giả) của the Gift, năm viết bằng chữ.
- Mạch mới (lời kể CHÍNH THỐNG của vương quốc, không lộ sự thật): I The Three Crowns (epic_three_crowns) · II The War (34 Before the Crown,
  600,000 chết, epic_war) · III The Butcher-King Borrow (epic_butcher_king) · IV The Ascended lật Borrow năm 7 Before the Crown (epic_ascended, hình
  nhỏ đội mũ trùm nhìn từ sau, KHÔNG vẽ mặt) · V The Crown: Thrag giết vị thánh, Darwin chém Thrag + thống nhất ba nước thành Vael (prologue_2 kính màu,
  chim trắng) · VI The Eternal King 1,176 năm (prologue_1) · VII The Gift: năm 776 cơn điên từ lòng đất, 4 năm sau vua mở "Tears of the Ascended"
  (LỜI NÓI DỐI: thật ra là Ether của Plague Eye) (epic_madness, prologue_3) · Unawakened + goblin "bị cơn điên bắt trước khi kịp uống" theo lời giáo sĩ
  (prologue_4) · Soul Requiem (prologue_5) · YEAR 1176: Ren (prologue_6) · khói trên Millbrook, ban ngày (prologue_7).
- Lịch trong game: "Before the Crown" / "Year N of the Eternal King" (năm 1 = năm Darwin lên ngôi = năm Angel Bird). Số năm viết bằng chữ số.

## §240 — 16/11 01h50: CẢNH MỞ ĐẦU BẢN 3 (người dùng: bỏ ba quốc gia, quá dài) — map1 md5 4936d05fcb7e3b0dbcd4d44abbad5587 — CHƯA CHƠI THỬ
- Lộ trình người dùng chốt: vương quốc tồn tại bao lâu -> chiến công của ai -> the Gift từ bao giờ -> các tầng lớp -> hoàn cảnh Ren.
- 8 tranh: prologue_1 (Kingdom of Vael 1,176 năm, Eternal King) · prologue_2 (Ascended chấm dứt chiến tranh, bị Thrag giết, Darwin báo thù + thống nhất,
  được ban bất tử) · epic_madness (776 cơn điên, 780 Tears of the Ascended) · prologue_3 (Awakening) · epic_classes MỚI (The Order of Vael: Great Houses
  được "trời ban" không già / Awakened được vương triều bảo hộ) · prologue_4 (Unawakened + goblin theo lời giáo sĩ) · prologue_6 (YEAR 1176, Ren) · prologue_7.
- Bỏ khỏi mở đầu: epic_three_crowns, epic_war, epic_butcher_king, epic_ascended (giữ ảnh trong Resources/Story để dùng về sau), prologue_5 (Soul Requiem).

## §241 (16/11) Prologue Map 1 — "The Gift" không còn hình ảnh điên loạn
- Người dùng: không cho "điên" vào phần mở đầu. Món quà kể theo bản chính thống: năm 780 ánh sáng xanh trào lên từ đất thánh nơi Ascended từng khóc cho dân → Vua tuyên bố đó là Tears of the Ascended (xanh như cánh đồng mọc lên nơi Ngài đi qua) → ai uống cũng khỏe, người bệnh đứng dậy.
- Slide mới: `Resources/Story/epic_gift_light.png` (cột sáng xanh giữa thánh đường, dân quỳ, chim trắng), `epic_gift_strong.png` (dân làng/lính khỏe mạnh, gân xanh mờ). Meta center.
- Bỏ khỏi prologue: epic_madness, prologue_3, epic_black_year/epic_first_cup (job PixelLab bbc6735a, d4f8b779 bỏ). Goblin: "those who drank from wild springs instead of the King's cup. So the priests say."
- StoryScenes.Say: "~" = xuống dòng trong cùng trang (tiêu đề chương + câu đầu). RequiemHUD: ẩn mũi tên/khoảng cách đích ("57m") khi CutsceneState.Active.
- map1 md5 sau sửa: 3e504492bb17d608468e67ec88902c1f. Backup: _backups/2026-11-16_waveD5_gift, _waveD6_gift2.
- (16/11 tiếp) epic_gift_strong thay bằng cảnh dựng nhà: dân nâng xà gỗ, ôm đá, vác cối xay, không có lính. Tranh dự phòng: epic_gift_harvest.png (gặt lúa). Câu thoại: "One pair of hands could lift what four had carried, and men worked from dawn to dark without tiring." map1 md5 3ed6a5b9efc0988b7bad335bf637fcd5.
- Lưu ý: device_commit_files có lúc KHÔNG ghi đè PNG đã có. Cách xử lý: commit sang tên tạm rồi ghi đè bằng python (open().write()).
- (16/11) Bỏ toàn bộ tiêu đề chương (THE KINGDOM OF VAEL, THE GIFT, THE ORDER OF VAEL, YEAR 1176…) khỏi hộp lời dẫn của prologue. Người dùng: cutscene KHÔNG chia chương, không viết tiêu đề trong hộp thoại. Năm 1176 đưa vào câu ("It is the Year 1176 of the Eternal King."). map1 md5 0691f429bb1b212eec6e4e0fc8f92edc.

## §242 — 16/11 02h10: VIẾT LẠI SOUL_REQUIEM_HANDOFF.md cho tài khoản Claude mới
- Bản bàn giao mới chép đủ lore đã chốt (3 màu Ether, biên niên, ai giữ phần xác thánh, thế lực, nhân vật), mạch 5 map, Seven Mercies, 3 kết,
  cảnh mở đầu Map 1 hiện tại (từng câu), hệ StoryScenes / BossMercy, md5 các map, việc còn lại (đợt C còn VFX tím, đợt E, Map 4, Map 5), quy tắc cutscene của người dùng.
- Bản cũ 05/11: `_backups/2026-11-16_handoff/SOUL_REQUIEM_HANDOFF_2026-11-05.md`. Mục NOTES tiếp theo: §243.
