# SOUL REQUIEM — Bản bàn giao tiến trình (05/11/2026, 02:45)

> Dùng file này để tiếp tục dự án với một tài khoản / phiên AI mới. **Thay bản 04/11 23:50** (sao lưu: `Catalog/backups/handoff_2026-11-05/`).
> Thứ tự đọc của phiên mới: file này → `ARCHITECTURE.md` → `SOUL_REQUIEM_NOTES.md` (nhật ký chi tiết, **mới nhất §129–§144**,
> đọc từ dưới lên). §104 bị trùng số ("Chiến đấu mượt hơn" và "Map 1 làng Millbrook"). Đầu NOTES có vài đường dẫn cũ — đường dẫn đúng ở mục 3.

---

## 1. Tin nhắn mở đầu gợi ý cho phiên mới

> Tôi đang làm game **Soul Requiem** (Unity 6 URP 2D, top-down dark fantasy). Hãy đọc
> `C:\Users\Admin\Soul Requiem\SOUL_REQUIEM_HANDOFF.md`, rồi `ARCHITECTURE.md`, rồi các mục §129–§144 của
> `SOUL_REQUIEM_NOTES.md` trước khi làm gì. Làm đúng mục "Quy tắc làm việc" trong file bàn giao. Trả lời tôi bằng tiếng Việt.

Phiên mới cần **liên kết với máy** (mở chat trong app Claude desktop) và quyền các thư mục:
`C:\Users\Admin\Soul Requiem`, `C:\Users\Admin\Desktop\Catalog` (+ `C:\Users\Admin\AppData\Local\Unity\Editor` để đọc `Editor.log`).
PixelLab: dùng tài khoản của người dùng qua trình duyệt trong app (người dùng tự đăng nhập; AI KHÔNG nhập mật khẩu).

---

## 2. Quy tắc làm việc (người dùng đã yêu cầu — BẮT BUỘC)

1. **Trả lời tiếng Việt.** Mọi chữ hiển thị trong game: **tiếng Anh**. Chú thích code: tiếng Việt.
2. **KHÔNG tự chạy thử trong Unity** (computer use tốn token — quy tắc 04/11). Thay vào đó gửi **checklist test** từng bước
   (mở gì, làm gì, kết quả đúng trông thế nào). Chỉ thao tác Unity khi người dùng chủ động yêu cầu.
   Được phép kiểm tra biên dịch: thời gian file `Library/ScriptAssemblies/SoulRequiem.*.dll` + tìm `error CS` trong `Editor.log`
   (`tr -d '\000' < Editor.log | grep "error CS"`). Unity chỉ biên dịch khi người dùng bấm vào cửa sổ Unity.
3. **Phân tích kiểu chuyên gia rồi HỎI trước khi làm thay đổi lớn** (đưa 2–4 phương án, đánh dấu Recommended). Việc nhỏ, rõ ràng thì làm luôn.
4. **Chỉ xoá đúng thứ người dùng yêu cầu.** Không xoá asset PixelLab nếu không được bảo.
5. **Người dùng sửa tay .tmx trong Tiled song song.** Mọi sửa .tmx = **script riêng trong `Catalog/v2/`** có: kiểm md5 đầu vào
   (khác -> dừng, đọc lại), sao lưu vào `Catalog/backups/<đợt>/<bước>/`, giữ CRLF, sửa theo id/tên, kiểm XML, in md5 mới.
   Ghi md5 mới vào NOTES. **TUYỆT ĐỐI không chạy `map2_sewers_v2.py --force`, không chạy generator Map 1** (Map 3: xem mục 5).
6. Sau mỗi lần sửa map: **nướng lại minimap** `python3 Catalog/v2/tools/minimap_bake.py <Scene>`
   Script THÊM VẬT vào map phải thêm **khối chặn** (lớp `obstacle colliders`, `collider_kind = prop`; vật chỉ hiện theo cờ thì đặt khối chặn
   CÙNG TÊN để bật / tắt theo) trong cùng script, rồi chạy `python3 Catalog/v2/tools/qa_prop_colliders.py` (§227).
   (ảnh toàn map dùng để xem trước: `Catalog/v2/out/minimap/<Scene>_full.png`, 32 px/ô).
7. Không ghi file test vào `Assets`. Ảnh preview để ngoài (`Catalog/v2/out/preview`, `Catalog/previews`).
8. Ghi file qua cầu nối: kiểm lại md5 sau khi ghi. Ảnh mới: **PNG trước, .meta sau**.
9. **Mỗi MonoBehaviour một file trùng tên class.** Không dùng `GetComponent<T>() ?? …`.
   Tránh đặt tên method trùng tên kiểu (vd method `Rect()`/`Strip()` trong class có dùng kiểu `Rect`/`Strip` -> lỗi biên dịch).
10. **Thiết kế phải hợp lý như ngoài đời**: khoá nằm đúng cửa nó mở; ghi chú nằm trên đồ vật thật; không biển / ghi chú thừa đứng trơ trọi;
    không đặt vật chắn lối đi; quái phải hợp cốt truyện từng khu; cơ chế nào cũng phải có cách khắc chế rõ ràng (báo trước + cách né).
11. Sau mỗi cột mốc: thêm mục mới vào `SOUL_REQUIEM_NOTES.md` (**tiếp theo là §228**) + báo cáo trong chat:
    việc đã làm, lỗi đã sửa (triệu chứng → nguyên nhân → cách sửa), **checklist test**, việc còn lại.
12. Code sạch, chuyên nghiệp, dễ cho dev mới: theo đúng luật assembly trong `ARCHITECTURE.md`; tính năng mới làm **chung cho mọi map**,
    cấu hình bằng thuộc tính Tiled, không viết cứng theo một map.
13. Trình duyệt: không đăng nhập / nhập mật khẩu hộ; tải file phải xin phép; không xoá vĩnh viễn.
14. **Người dùng phải THẤY và chỉnh tay được mọi vật trong Tiled** (06/11, NOTES §154): vật / cơ quan có hình = **tile object** trong lớp
    `mechanics_art` kéo từ tileset `SR-mech-art.tsx` — KHÔNG đặt hình chữ nhật trống + thuộc tính `sprite`. Chi tiết: ARCHITECTURE 2.2.
15. **Game top-down**: mọi phòng cùng một mặt sàn trừ khi map nói rõ có tầng khác. Không dùng thang / nắp hầm cho lối tắt cùng tầng (NOTES §153).
16. **Mọi khu phải đánh dấu cốt truyện** (07/11, NOTES §161): khu mạch chính có `story` + `story_done`, khu phụ `optional = true`;
    nhánh chưa tới lượt chặn bằng `turn_back`. F4 dựa vào đây để test (ARCHITECTURE 2.1).

---

## 3. Đường dẫn

| Thứ | Đường dẫn |
|---|---|
| Project Unity | `C:\Users\Admin\Soul Requiem` (Unity 6000.3.6f1). Code: `Assets/Scripts/` (Core, Gameplay, Visual, UI, DevTools) — **đọc ARCHITECTURE.md** |
| Công cụ map (Python) | `C:\Users\Admin\Desktop\Catalog\v2\` — script sửa Map 2: `map2_<việc>.py` (mỗi đợt một file, có md5 đầu vào) |
| Sao lưu | `Catalog/backups/` — Map 2: `map2_2026-11-0x/<bước>/`; code: `polish_2026-11-03/`, `polish_2026-11-04/{toad_rework,batch2..batch5_devkeys}/`, `polish_2026-11-05/{batch1,batch2}/`; Map 2 05/11: `map2_2026-11-05/<bước>/`; file bàn giao: `handoff_2026-11-05/` |
| Scene | `Assets/Scenes/Title.unity`, `Map1_GreystonePlains.unity`, `Map2_UndercityOfVael.unity`, `Map3_ValeOfGraves.unity`, `Map3b_KingsBarrow.unity` (hầm X2, §166) |
| Map 1 .tmx | `Assets/GamePackage/ERW - Grass Land 2.0 v2.1/TiledMap Editor/Maps/map1_greystone_plains.tmx` — md5 hiện tại `7d2dc225a7b92ded2ac80bb08be9a64a` |
| Map 2 .tmx | `Assets/GamePackage/EPIC RPG World - Sewers V1.5/TiledMap Editor/Maps/map2_undercity_of_vael.tmx` — md5 hiện tại **`dfdf66dca6762349b5926f3907c71420`** |
| Map 3 .tmx | `Assets/GamePackage/EPIC RPG World Pack - Cemetery V 1.6/TiledMap Editor/Maps/map3_vale_of_graves.tmx` — md5 hiện tại `f6c05cbb8a32aa74b56f13c3c44b88a3` (136x126, §166–190); hầm `map3b_kings_barrow.tmx` md5 `95cf1c7dc0a4bd4bb8dfdf034ffe6325` (§190) |
| Map 4 .tmx (bản đồ thô, không còn dùng) | `Assets/GamePackage/EPIC RPG World Pack - Old Prison V1.7.1/TiledMap Editor New/Maps/map4_catacombs.tmx` — md5 `7ddaf2fcea3138009621af9146ed1ac3` (§192, sinh bằng `Catalog/v2/maps/map4_greybox.py`); thiết kế: https://claude.ai/artifact/AanV3WpvUS4FwNsFEZiGyi |
| Map 4 .tmx (art Crypt) | `Assets/GamePackage/EPIC RPG World Pack - Crypt V.1.6/TiledMap Editor/Maps/map4_catacombs.tmx` — md5 `2ff75a1819cacea6bf694f0b151f08d4` (§194 + bẫy đợt 1-2 §196-197 + khu ngục Old Prison §198, sinh bằng `Catalog/v2/maps/map4_crypt.py` + `cryptkit.py`, bố cục `maps/map4_layout.py`); scene Map 4 dùng file này |
| Nhiệm vụ / minimap | `Assets/Resources/Quests/<Scene>.json`, `Assets/Resources/Minimap/<Scene>.png` |
| Giao diện | code `Assets/Scripts/UI/` (RequiemHUD, RequiemMenus, RequiemUI); ảnh + uss `Assets/Resources/UI/Requiem/` (chân dung `boss_*.png`, icon `ic_mantra_*`, `key_*`, `item_*`) |
| VFX nạp lúc chạy | `Assets/Resources/VFX/` — `landing/`, `collapse/`, `chest/` (rương mở 8 khung), `specimen/` (rễ PixelLab `roots{a..d}_0..7`, trụ phong ấn `ward_stone0..3(_lit)`, `ward_sigil`, bàn tay `hand_0..7`, rune trụ `ward_rune0..3`, mắt tường `walleye_open/closed`), `props/` (`note_paper`, `hatch_round`, `maint_ladder`) |
| Kỹ năng (Mantra) | `Assets/Resources/Skills/` GroundSlam, EyeOrbs/EyeVeil/EyeRay (bộ "Mantras of the Eye") |
| Chỉ số quái / prefab | `Assets/Enemies/<MapN>/Definitions/*.asset`, `Assets/Enemies/<MapN>/Prefabs/` |
| Ảnh gốc PixelLab đã dùng | `Catalog/v2/art/` (vd `pixellab_roots_source.png`) |
| File lưu game | `Application.persistentDataPath/soulrequiem_save.json` |
| Nhật ký | `C:\Users\Admin\Soul Requiem\SOUL_REQUIEM_NOTES.md` |

---

## 4. Quy ước kỹ thuật

- Ô 32 px, **PPU 100 → 1 ô = 0,32 đv**. Toạ độ Tiled theo ô (y hướng xuống); tile object có gốc **dưới-trái**; thế giới y = −ô y × 0,32.
- Tiled → Unity bằng **SuperTiled2Unity**. Layer logic opacity 0.35, KHÔNG ẩn. `unity:IsTrigger` đặt trên **từng object**.
  Lớp object: `zones`, `spawns`, `gates`, `triggers`, `mechanics`, `obstacle colliders` (type obstacle, `collider_kind`), `blockers`,
  `ground decals` (dưới nhân vật), `props` (sắp theo chân), `props front`, `ambient fx`,
  **`mechanics_art`** (06/11: hình của cơ quan — tile object từ `SR-mech-art.tsx`, opacity 1; mảnh của cơ quan khác ghi `art_for`).
- Thuộc tính Tiled đọc bằng `TiledProps.GetString/GetBool/GetFloat/GetInt` (namespace `SoulRequiem.Core`).
- Phím (Core/GameInput): J/chuột trái chém, Q lăn, E bình máu, F/R/C Mantra I/II/III, T tương tác/nghỉ, M bản đồ, **B túi đồ**, Esc tạm dừng.
  Tay cầm: X chém, B lăn, Y bình, A tương tác, RB/LB/RT Mantra, Select bản đồ, Start dừng, **LT túi đồ**. Token chữ: `{attack} … {inventory}`.
- Phím test (DevCheats, chỉ Editor/Dev build, sắp lại 04/11 — NOTES §142): **F1 bảng phím + trạng thái**, F2 bất tử, F3 hạ hết quái,
  F4 khu kế (Shift: khu trước, Ctrl: lối ra), F5 giải câu đố + nhận mọi chìa, F6 mở mọi Mantra + đầy bình, F7 boss −25% (Shift: vào pha 2),
  F8 tốc độ 1×/0.5×/0.25×/2× (Shift: ghi sát thương), F9 tải lại bản lưu (Shift: tự gục). Có cheat ảnh hưởng số liệu -> nhãn TEST ở mép trên.
- Menu Unity (07/11, tiếng Anh, gom trong `Assets/Editor/SoulRequiem/SoulRequiemMenu.cs`): **Soul Requiem ▸ Scenes** (Build Title / Map 2 / Map 3 / Map 3b Scene, Build All Map Scenes), **▸ Enemies** (Build Map 1/2/3 Enemies, Build All Enemies, Create Missing Enemy Definitions, Check and Repair Enemy Animators), **▸ Player** (Rebuild Player (Open Scene) = Animator + Sort by Feet + Fit Collider, và từng bước riêng). Chạy ngoài Play Mode. Ghi chú cũ trong NOTES dùng tên menu tiếng Việt trước đó.
  Sửa map xong người dùng phải chạy **Dựng scene Map N**. Chỉ số quái: sửa asset EnemyDefinition (không cần dựng lại).
- Hầu hết hệ thống **tự gắn** bởi `MapEncounterDirector.EnsureMechanics()` (BossIntro, BossRelic, ArenaSeal, CombatTutorials, cảnh diễn theo điểm Tiled…).

---

## 5. Quy trình sửa map an toàn

- **Map 2** (đang làm): mỗi việc một script `Catalog/v2/map2_<việc>.py` theo mẫu (xem `map2_vaultDecor.py`, `map2_keysChests.py`):
  `EXPECT = "<md5 hiện tại>"` → sửa chuỗi XML theo id/tên → `ET.fromstring` kiểm → sao lưu → ghi → in md5 mới → NOTES → bake minimap.
  Chuỗi md5 04/11: 9b6d92a1 → fdf0c073 (toadRework) → 01045056 (trimSigns) → 11bccce9 (eyeRelic) → 07285a31 (specCells) → 170beaa2 (ecology)
  → 9a6599fe (ctrlCalm) → e3277e8f (keysChests) → dd207507 (cisternNote) → a0da2db6 (noteTanks) → 0adf0f8d (vaultWards) → 7637207 (vaultFloor)
  → ade88be7 (removeWards) → 12cbe0be (vaultDecor) → 54b24679 (wardStones)
  → 3f4856d1 (vaultDressing) → 7d33b9b8 (ambushes) → 0e1fdcc5 (ichorCalm) → 217987c1 (roomDressing)
  → 761e6680 (boilShortcut) → 26941db6 (wallEyes) → **0d4d8812 (undoDecor, hiện tại)**.
- **Map 1**: sửa bằng script dùng `Catalog/v2/map1_lib.py` (lớp `Tmx`). md5 đã lệch so với NOTES §128 → người dùng đã sửa trong Tiled; đối chiếu trước.
- **Map 3**: .tmx lệch stamp generator từ 30/10 → sửa thẳng .tmx bằng script (như Map 2), KHÔNG chạy generator nếu chưa đối chiếu.

---

## 6. Cốt truyện (bản hiện hành — đối chiếu NOTES §94, §100, §116, §126, §136; cập nhật 05/11)

> Tên chữ hiển thị: Ichor→**Ether**, The Saint→**The Ascended**, The Silencing→**The Requiem**, Project ORIGIN→**The Vessel**, Skill→**Mantra**,
> người thuê Tobin→**Todd**. Màu: Ether thuần = **xanh dương** (Mantra của Ren); Ether hắc hoá = **xanh lá**.
> Tài liệu "Soul Requiem – Cốt truyện chính thức" (artifact 25/10) và NOTES §48 là bản CŨ (Tobin, Ichor, The Saint…) — khi lệch, theo mục này.

**Thế giới**
- **Vael** = hỗn mang bất tử, thành hình theo cảm xúc người (Vael là nam — "ngài"). Hình hài đầu = **The Ascended** (giáo hội thờ).
- **The Requiem** (1000 năm trước): quý tộc giết The Ascended vì sợ mất quyền. Tinh chất = **Ether** — ban điều được cầu nhưng méo mó
  (nông nô → goblin, lính → orc, quý tộc → vampire…).
- **Naming** nối linh hồn với Vael → chết thì mắc trên **Wall of Voices**. Ren chưa qua Naming (**Nameless**).
- Giáo hội biến chất thành ma giáo **Choir of the Requiem**: Hierophant **Absalom**, Precentor **Morrow**, **Lantern Knight**, quân **Black Lantern**.
  Mantra = câu kinh cổ của giáo hội thời chính đạo (tên tạm thời chính đạo: *Church of Mercy* — chưa duyệt).
- **The Vessel** (300 năm trước, goblin **Malric** + Absalom) thất bại: **Specimen Zero** vô tri; thánh tích Con Mắt thành **Plague Eye**, điều khiển goblin → **the Drowning**.
- **Tiên tri**: *"Con Mắt được giải thoát bởi kẻ không có tên."* Plague Eye khống chế linh hồn có Tên nên giáo hội không tự lấy được → cần một kẻ Nameless.

**Mạch truyện theo map**
- **Map 1 Greystone Plains (xong)**: Ren là kiếm khách đi ngang qua. Mỗi kỳ trăng mới lính thu thuế của tù trưởng goblin **Grok** xuống lấy thóc ở Millbrook;
  thiếu 3 bao → đốt trại nhà Harlan. Ren đuổi chúng; trưởng làng **Todd** nhờ diệt Grok, mang nanh làm bằng chứng. Hạ Grok ở Stone Ring →
  vòng đá sập do **tai nạn** → rơi xuống lòng đất. Goblin không biết gì về bí mật thế giới; nanh Grok hết quan trọng sau khi rơi.
- **Map 2 Undercity of Vael** (mục tiêu: **thoát ra**): tiếp đất, đường lên bị lấp → Cistern Gate thấy con Mắt chặn lối ra duy nhất (ống thoát trên hố)
  → trang nhật ký người giữ cống (Eye's Cistern): con Mắt bất khả xâm phạm khi bể đầy, phải đổi dòng ở Pump Control (chìa trong bụng cóc Gorgemaw,
  chìa Vat Hall ở Brakk – Boiler Room, tay quay ở lab Malric) → xả bể → hạ **Plague Eye** → **lõi Mắt nhập vào Ren** (bộ "Mantras of the Eye")
  → hố trào Ether → leo ra ống thoát. Goblin trong hầm đã chết hết. Tuỳ chọn: Specimen Zero (Containment Vault).
- **Map 3 Vale of Graves**: Ether tràn xuống thung lũng, xác chết đứng dậy. Giáo hội phong toả, săn "kẻ mang Con Mắt" (Con Mắt đã hoà vào linh hồn Ren
  → phải tách bằng nghi lễ, nên cần Ren **còn sống**). **3 chuông cộng hưởng Con Mắt** (làm lộ Ren) → làm im 3 chuông → cửa sổ hoa hồng
  (sự thật: tay đè The Ascended đeo nhẫn các Đại gia tộc — nối House Vey / Avenue of Peers) → hạ **Morrow**; hấp hối, Morrow xé mở **Requiem Door**,
  ánh tím kéo Ren (cùng Con Mắt) sang Map 4.
- **Map 4 Catacombs of Naming** (§192 tài liệu + bản đồ thô; §194 đã dựng bằng art Crypt; §195 phân tích asset + 17 cơ chế bẫy; §196 đợt 1: plate, spikes, cổng hoạt ảnh, cần quay; §197 đợt 2: launcher, cover, roller, blade; §198 khu ngục art Old Prison (prisonkit) — tiếp: bố cục v2 phía hầm mộ, đợt 3): Gaol → hub Stair of Bones + 3 nhánh → Hall of Naming; Ren là Candidate VII; The Severing tách Con Mắt, Malric hy sinh, nhận bộ Mantras of the Name.
- **Kết game** (ý tưởng): hình hài thứ hai của Vael = **the Remade God**. Boss cuối không giết được — thắng bằng cách vỡ hình hài
  (3 pha: hình hài hồi sinh → phá 3 neo → đập tim). 3 kết: **Requiem / The Face / Silence**.

**Câu hỏi mở về truyện (chưa chốt)**
- Động cơ của Ren sau Map 1: đã bỏ móc "mang nanh về Millbrook" (§126); Map 2 = thoát thân, Map 3 = bị săn — Ren chưa có mục tiêu chủ động
  (vd muốn tách Con Mắt ra / về nhà / trả thù). Cần chốt trước khi thiết kế Map 4.
- Tên giáo hội thời chính đạo (*Church of Mercy*?). Thiết kế Map 4 + điều kiện 3 kết.

---

## 7. Trạng thái từng map

### Map 1 – Greystone Plains — xong, người dùng đã chạy thử phần lớn (NOTES §106–§128)
Làng Millbrook, mở màn cướp làng, Todd, 3 lửa trại (lưu game), Hollow Caves, Old Copper Mine (Earthshatter), Greystone Camp, Stone Ring (Grok + 3 cột) → sập hầm → Map 2.

### Map 2 – The Undercity of Vael — đang hoàn thiện, nhiều thay đổi 03–04/11 **CHƯA được người dùng chạy thử hết** (§132–§144)
Đường đi: Hatch (tiếp đất) → Gallery of Arches → Ether Well → **Sluice Hall (hub)** → Cistern Gate (thấy con Mắt) → Eye's Cistern (trang nhật ký)
→ lấy đồ: Malric's Laboratory (tay quay), Boiler Room (mini-boss **Ether Ram** → Vat Hall Key), Specimen Cells → Culture Hall (mini-boss **Gorgemaw** → Pump Room Key)
→ Pump Control (lắp tay quay, câu đố ống, xả bể) → **Plague Eye** → lõi Mắt → thoát.
Tuỳ chọn / bí mật: Malric's Archive → Records Vault (khoá ký tự 3-7-4, mã ở các ghi chú) → rương Choir Vault Key → Containment Vault (boss ẩn **Specimen Zero**);
Forgotten Store (sau rào gỗ ở Ether Well: sách Turned Blade + **Odo's Key**); Reliquary (sách Purifier); Overseer's Office.
Quái theo cốt truyện: thú cống (Plague/Bloated Rat, Channel Viper, Ichor Snapper) + mẫu vật của Malric (Vat-born, Skinned Hopper, Mimic). Nhện chỉ ở Forgotten Store.
Điểm chính đã làm 03–04/11:
- Cảnh diễn: tiếp đất, thấy con Mắt, màn xuất hiện 4 boss (bảng tên + chân dung), xả bể, cổ vật nhập vào Ren (BossRelic), thoát thân.
- An toàn khi diễn cảnh: Ren bất tử + mọi quái đứng im (chung mọi map).
- Đấu trường boss (ArenaSeal): boss bị kẹp trong khu; khu không có cổng thì dựng màn chắn Ether ở khe tường (dò theo ô tile).
- Gorgemaw: lưỡi giật Ren lại gần (không sát thương), vỡ bể kính → vũng acid, pha 2 nhảy ra giữa làm vỡ 4 bể, cóc con chui từ vũng **một lần**.
- Plague Eye: gọi chuột cận chiến + đánh xa; hạ xong lõi Mắt rơi ra.
- Specimen Zero (làm lại): Grasping Roots (rễ PixelLab), Tentacle Lash, Vessel's Gaze (tia quét), Requiem Wail (một trong 4 **trụ phong ấn PixelLab** bừng sáng — núp sau trụ,
  trên vòng ấn vàng; dồn 5% máu khi nó hít vào → nghẹn), **Hand of the Vessel** (bàn tay xác trồi lên tóm), pha 2 xích đứt + mắt tường.
  Phòng nới rộng 5 hàng, boss ở giữa; trang trí "người gác ấn ngã xuống" (xương, kiếm gãy, nến quanh trụ).
- Túi đồ (B): chìa / dụng cụ / tài liệu; cửa khoá chỉ mở khi tới đúng cửa và có chìa; cửa khoá thì Ren tự nói; rương có hoạt ảnh mở.
- Phòng giam: bình mẫu vật sống (có va chạm), không còn cần gạt; rương của Odo khoá bằng Odo's Key (+1 bình máu tối đa, ô chiêu để dành).
- Cắt biển thừa; giữa trận không đọc biển/ghi chú; đích nhiệm vụ trên minimap có quầng/ping nhẹ, tự ẩn khi đang đánh trong khu đích.
- 05/11 (§143, theo bản đánh giá tĩnh 7,2/10): nhiệm vụ con có ô tích (bước "Open the Pump Room"); 2 trận phục kích theo cốt truyện
  (`spec_breakout` sau Gorgemaw, `r4_swarm` sau khi thấy con Mắt) + Feed Conduit thêm đợt; ichor dịu màu (tileset `SR-ichor-calm`);
  lối tắt một chiều thang Boiler Room ↔ nắp hầm Sluice Hall (`passage`); `LightCuller` tắt đèn ngoài camera.
- 05/11 (§146): Specimen Zero làm lại — bỏ ngắt chiêu Wail (thay bằng Spent 2,5 s nhận +50% sát thương), đòn chống áp sát Writhing Bed,
  mắt tường thành đòn có lịch The Watchers (loạt bắn đuổi, VFX PixelLab `eyefx_*`).
- 05/11 (§144): 4 mắt tường thật cho pha 2 Specimen Zero; Requiem Wail bản 4 (rune trụ sáng hiện dưới chân boss + vệt chấm dẫn tới vòng ấn,
  vòng dưới chân Ren đổi đỏ → vàng khi an toàn); ichor sửa lại độ sáng (bản §143 gần như vô hình); **gỡ toàn bộ trang trí rải ngẫu nhiên** của §143
  (id 1647–1756) — trang trí lại theo cụm, có mockup cho người dùng duyệt trước.

### Map 3 – The Vale of Graves — đã dựng, chờ chơi thử (NOTES §83, §100)
V0 Outfall → V3 The Commons (hub) → nhánh House Vey (Isolde), Ossuary Tower (chuông), Sinking Fields, Broken Bridge (Lantern Knight) → Chapel of Naming → V11 Morrow → V12 Requiem Door.
**Chưa áp** các hệ thống mới của Map 2 (túi đồ/chìa, cửa tự nói, cổ vật boss…) — xem mục 9.

---

## 8. Hệ thống chính (chi tiết: ARCHITECTURE.md)

| Hệ thống | File |
|---|---|
| Người chơi | `Gameplay/Player/` PlayerController, PlayerCombat, `Abilities/*` (PlayerDodge, PlayerMelee…), PlayerStatus |
| Máu / trạng thái | `Combat/Health.cs` (độc, cháy, khiên, `Floor`, `CutsceneSafe`) |
| Mantra + bộ chiêu | `Player/Skills/` SkillDefinition (`setId/setSlot/setName/setDescription`), PlayerLoadout (`SetOf/SetLeader/SetName`) |
| Quái + boss | `Enemies/EnemyAI.cs` (`Held`, `CutsceneActor`, `Aggro`), `Enemies/Bosses/*Skills.cs` (Toad, Ram, PlagueEye, SpecimenZero…) |
| Khu / đợt / cổng / trigger | `World/Encounters/` MapEncounterDirector (`InCombat`, `TickGateLocks`, `TargetReachedInFight`), MapLayout (MapGate `requiresKey/lockedSay`), GateController, MapTriggerHandler, **ArenaSeal** |
| Đèn | `World/LightCuller.cs` (tắt đèn map ngoài khung camera, 0,2 s/lần), `World/LightAnimator.cs` (`Lamp`, `SetCulled`) |
| Cơ quan | `World/Mechanics/` — mỗi loại một class: lever, valve, console (`fit_object`), pipegrid, note (`sprite/marker/pickup/key_*`), **chest**, **sprite_prop**, **passage** (lối tắt / thang), tank, hazard, light, toggle… |
| Túi đồ / chìa | `World/KeyRing.cs` (KeyItem: id, name, kind, desc, opens, icon, text, stats); UI `RequiemMenus.BuildInventory` |
| Cảnh diễn | `World/Cutscene/` CutsceneState, BossIntro, **BossRelic**, LandingCutscene, EyeGlimpse, EscapeSurge, CombatTutorials, CutsceneFx, DialogueBox, SpeechBubble |
| Tương tác / nhiệm vụ | `World/InteractionSystem.cs` (`calmOnly`), `World/QuestTracker.cs` (`Target.Checks` — nhiệm vụ con) + `Resources/Quests/*.json` |
| Sóng chấn | `Combat/Shockwave` (`gapAngles`, `gapFrom`, `shelter` = Func<Vector2,bool> vùng an toàn, `onSheltered`) |
| Lưu / chuyển map | `Flow/SaveGame.cs` (cờ, điểm hồi sinh, bình, bộ chiêu), `Flow/GameSession.cs` (sách `book_*`, bộ chiêu mang qua map) |
| Giao diện | `UI/RequiemHUD.cs` (minimap, mũi tên đích có quầng/ping, bảng tên boss…), `UI/RequiemMenus.cs` (Pause, Inventory, Rest/Mantras, Settings), `UI/RequiemUI.cs` |

---

## 9. Việc còn dở / dự định tiếp theo

**A. Chờ người dùng chạy thử (ưu tiên cao, Map 2)** — checklist chi tiết ở cuối mỗi mục NOTES §132–§141:
- [ ] Biên dịch sạch: DLL `SoulRequiem.*` biên dịch lần cuối 05/11 02:37 (giờ VN) — kiểm `error CS` trong `Editor.log` (cần quyền thư mục `AppData\Local\Unity\Editor`).
- [ ] Chạy **Dựng scene Map 2** rồi chơi trọn Map 2 từ đầu: mạch truyện trang nhật ký, chìa/túi đồ/cửa, rương Odo + Records Vault, lắp tay quay,
      Gorgemaw, Ether Ram, Plague Eye + cổ vật, Specimen Zero (Requiem Wail bản 4: đứng trong vòng ấn của trụ sáng; mắt tường pha 2; bàn tay; đánh chặn; rễ PixelLab vừa kích thước?), màn chắn cửa các phòng boss.
- [ ] Thử các việc §143–§144: ô tích nhiệm vụ con, 2 trận phục kích, màu ichor mới (thấy rõ nhưng không chói), lối tắt thang Boiler Room → nắp hầm Sluice Hall, FPS trên bảng F1.
- [ ] **Chờ duyệt mockup trang trí Boiler Room** (§144: thang bảo trì vẽ lại `maint_ladder2` ở x 93.35, tấm neo + xích đứt chỗ Ram, thùng cạnh xác Brakk, than ở cửa lò). Duyệt xong mới ghi .tmx; các phòng khác (Eye's Cistern, Culture Hall) làm theo cùng cách.
- [ ] Cân bằng Map 2 sau khi chơi tay (máu/sát thương boss, ngưỡng đánh chặn 5%, số quái từng khu).

**B. Dự định gần (người dùng đã nhắc)**
- [ ] **Vũ khí**: túi đồ đã có loại "Weapon" + bảng PROPERTIES (`key_kind = Weapon`, `key_stats = "Nhãn:Giá trị;…"`) — cần thiết kế hệ thống trang bị vũ khí + nội tại.
- [ ] **Chiêu thưởng trong rương của Odo** (`skill_id` đang để trống) — thiết kế một Mantra mới.
- [ ] Giao diện túi đồ cho **mobile** đã làm kiểu ô to; cần thêm điều khiển cảm ứng cho cả game nếu nhắm mobile.
- [ ] Áp hệ thống Map 2 sang **Map 3**: chìa/túi đồ (cổng Vey, chìa…), cửa tự nói thay biển, `calmOnly`, ArenaSeal, BossRelic (nếu boss Map 3 rơi đồ), cắt biển thừa,
      sắp quái theo cốt truyện; rồi chạy thử Map 3. Map 3: tự chuyển sang Map 4 khi Morrow chết; cân bằng Morrow / Isolde / Lantern Knight.
- [ ] **Cốt truyện**: chốt động cơ của Ren sau Map 1 + tên giáo hội thời chính đạo (mục 6, "Câu hỏi mở").
- [ ] **Map 4 Catacombs**: thiết kế (nơi làm lễ hồi sinh; tách Con Mắt khỏi linh hồn Ren; Malric bị giam ở đây).

**C. Tồn đọng**
- [ ] Hệ thống âm thanh (chưa có gì). Giao diện đổi phím trong Settings (hiện chỉ xem).
- [ ] Sprite lõi Con Mắt trên sàn đang vẽ bằng code (BossRelic); có thể làm bằng PixelLab cho đẹp hơn.
- [ ] Trụ phong ấn phòng boss ẩn (§141) đã có tác dụng rõ (núp khi gào); chờ người dùng xem kích thước / vị trí có chắn lối không.
- [ ] Còn treo: va chạm Dynamic–Dynamic người chơi/quái có thể rung.
- [ ] Rác chờ người dùng quyết: `Soul Requiem/_to_delete/`, `Assets/_Recovery/`.

---

## 10. Lỗi hay gặp — đừng lặp lại

- Layer logic ẩn trong Tiled → ST2U bỏ qua (dùng opacity). `unity:IsTrigger` phải đặt từng object.
- Cổng khoá đấu trường đặt đè hành lang → khoá người chơi ở ngoài. Khu boss: **khung khu = đúng sàn phòng**.
- **Nới phòng phải sửa cả blocker** (lớp `blockers`): blocker cũ ở mép phòng sẽ chắn ngang phần mới (lỗi Containment Vault 04/11).
- Màn chắn / va chạm dò theo collider dễ lệch hình → dò theo ô tile của lớp tường.
- Ảnh có lề trong suốt dưới đáy → vật đè nhân vật (cắt lề). Ảnh .meta tới trước PNG → Unity xoá meta.
- Animator: Exit Time từ hành động sang Run → khựng.
- Vật trang trí đặt ở `ground decals` luôn vẽ dưới nhân vật → vật đứng (bình, cột) phải ở `props` + có va chạm ở chân.
- Cơ chế không có cách khắc chế (sóng phủ cả phòng, quái spawn vô hạn, quái đánh lúc diễn cảnh) → người dùng sẽ phàn nàn: luôn có báo trước + cách né / phá.
- Đồ vật phải hợp logic: chìa/cần gạt không được "có sẵn" khi người chơi còn đang giữ nó trong túi; cần gạt không đặt giữa sàn.
- PixelLab Create image Pro trả **lưới 2x2 biến thể** (4 ảnh trong 1); hoạt ảnh có thể dựng từ ảnh tĩnh bằng tách lớp + dịch (xem NOTES §135).
- Hot reload làm style OnGUI null; RequiemMenus khi domain reload → kiểm tra null.
- Đổi màu tile: tính cả **tint lớp + ánh nền khu** (nhân dồn) — giảm độ sáng tile rồi tint thêm làm nước gần như vô hình (ichor §143 → §144).
  Mô phỏng ảnh trong game (`out/preview/ichor_ingame_sim.png`) trước khi áp.
- **Trang trí rải ngẫu nhiên / "đưa linh tinh" → người dùng gỡ hết.** Đặt đồ theo CỤM kể chuyện bám kiến trúc, cùng mật độ pixel với tile
  (PixelLab 64x64, không thu nhỏ từ 128), cùng bảng màu tileset, chừa đường đi / đường lao boss, **mockup trên map thật cho người dùng duyệt trước**.


## 09/11 15:40 — ĐỌC TRƯỚC: HANDOFF_REN_KATANA.md
Tình hình Ren cầm katana + hệ đổi katana + VIỆC DỞ (sửa đòn chém: 44 khung lỗi nặng, kế hoạch vẽ lưỡi bằng code + vệt chém) nằm trong `HANDOFF_REN_KATANA.md` ở gốc project.
