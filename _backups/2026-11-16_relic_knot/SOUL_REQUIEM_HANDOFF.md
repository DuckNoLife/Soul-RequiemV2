# SOUL REQUIEM — Bản bàn giao (16/11/2026, 02:10)

> Dành cho một phiên / tài khoản Claude **hoàn toàn mới**. Đọc hết file này trước khi làm gì.
> Thay bản 05/11 (bản cũ sao lưu ở `_backups/2026-11-16_handoff/SOUL_REQUIEM_HANDOFF_2026-11-05.md`).
> Thứ tự đọc: **file này** → `ARCHITECTURE.md` (kiến trúc code) → `SOUL_REQUIEM_NOTES.md` (nhật ký, đọc **từ dưới lên**, mới nhất §233–§242)
> → `HANDOFF_REN_KATANA.md` nếu đụng tới nhân vật Ren / katana.
> Các link claude.ai/artifact trong NOTES thuộc tài khoản cũ, tài khoản mới **không mở được**. Mọi thứ cần biết về cốt truyện đã chép đủ vào mục 6–8 dưới đây.

---

## 1. Tin nhắn mở đầu gợi ý cho phiên mới

> Tôi đang làm game **Soul Requiem** (Unity 6 URP 2D, top-down action dark fantasy). Hãy đọc
> `C:\Users\Admin\Soul Requiem\SOUL_REQUIEM_HANDOFF.md`, rồi `ARCHITECTURE.md`, rồi các mục cuối (§233 trở đi) của
> `SOUL_REQUIEM_NOTES.md` trước khi làm gì. Làm đúng mục "Quy tắc làm việc". Trả lời tôi bằng tiếng Việt.

Phiên mới cần **liên kết với máy** (mở chat trong app Claude desktop) và quyền thư mục:
`C:\Users\Admin\Soul Requiem` (bắt buộc), `C:\Users\Admin\Desktop\Catalog` (công cụ map cũ),
`C:\Users\Admin\AppData\Local\Unity\Editor` (đọc `Editor.log` để xem lỗi biên dịch).
Qua cầu nối, thư mục project hiện ở `$HOME/mnt/Soul Requiem` trong `device_bash`.
PixelLab: dùng qua MCP PixelLab (tài khoản người dùng). Không đăng nhập / nhập mật khẩu hộ.

---

## 2. Quy tắc làm việc (BẮT BUỘC — người dùng đã yêu cầu)

**Ngôn ngữ và văn phong**
1. Trả lời **tiếng Việt**. Mọi chữ hiển thị trong game: **tiếng Anh**. Chú thích code: tiếng Việt.
2. Phân tích như chuyên gia (thiết kế game, kể chuyện), nhưng **hỏi trước khi làm thay đổi lớn** (2–4 phương án, đánh dấu Recommended). Việc nhỏ, rõ thì làm luôn.

**Cốt truyện và cutscene (người dùng rất khắt khe — 16/11)**
3. **Không spoil sự thật trước Map 4.** Sự thật (quý tộc giết The Ascended trong vụ Angel Bird, Darwin là kẻ phản bội, the Gift có nguồn gốc dối trá) chỉ lộ ở **Map 4**. Map 1–3 chỉ được gieo manh mối nhỏ.
4. **Không vẽ nhân vật giống Chúa Jesus.** The Ascended chỉ thể hiện bằng biểu tượng (chim trắng trong đĩa mặt trời, ánh sáng) hoặc bóng người trùm mũ nhìn từ sau, nhỏ.
5. Cutscene **không chia chương**, **không có dòng tiêu đề** kiểu "THE GIFT", "THE ORDER OF VAEL" trong hộp thoại. Kể liền mạch, giọng sử thi, chuyên nghiệp.
6. **Năm viết bằng chữ số** ("the Year 780", "Year 1176 of the Eternal King").
7. Không đưa hình ảnh "điên loạn / hỗn loạn" vào phần giới thiệu the Gift (người dùng: "không ai muốn bị điên cả"). Bản chính thống dân tin: the Gift = món quà thần thánh.
8. Tranh cutscene vẽ bằng PixelLab `create_image_pro` 256×144, `no_background=false`. Xem ảnh trước khi đưa vào game; sai ý (lính thừa, cảnh tối khi cần ngày…) thì vẽ lại.

**Kỹ thuật**
9. **Sửa .tmx bằng script có kiểm md5** (người dùng sửa tay trong Tiled song song): kiểm md5 đầu vào (khác → dừng, đọc lại), sao lưu vào `_backups/<ngày>_<đợt>/`, giữ CRLF, sửa theo id/tên object, in md5 mới, ghi md5 vào NOTES. Thư viện: `_tools/lore_1116/patch_lib.py` (mục 5).
10. **Chỉ xoá đúng thứ người dùng yêu cầu.** Xoá qua cầu nối bị chặn → chuyển vào `_to_delete/<ngày>_<việc>/` và báo người dùng.
11. Ghi file qua `device_commit_files`: **luôn kiểm lại md5 / nội dung trên máy** — nó có lúc KHÔNG ghi đè file đã có (đã gặp 16/11 với PNG). Cách chắc ăn: commit sang tên tạm rồi `python3 -c "open(dst,'wb').write(open(src,'rb').read())"`. Ảnh mới: PNG trước, `.meta` sau.
12. Mỗi MonoBehaviour một file trùng tên class. Không dùng `GetComponent<T>() ?? …` với object Unity. Tính năng mới làm **chung cho mọi map**, cấu hình bằng thuộc tính Tiled.
13. **Kiểm thử trong Unity**: mặc định KHÔNG tự lái Unity bằng computer use (tốn token) — gửi **checklist test** cho người dùng. Chỉ thao tác Unity khi người dùng bảo. Kiểm biên dịch: `tr -d '\000' < Editor.log | grep "error CS"` + thời gian `Library/ScriptAssemblies/SoulRequiem.*.dll`. Unity chỉ biên dịch khi cửa sổ Unity được bấm vào (người dùng Ctrl+R).
14. Thiết kế phải hợp lý như ngoài đời: khoá nằm đúng cửa, ghi chú nằm trên đồ vật thật, không biển trơ trọi, không chắn lối, cơ chế nào cũng có báo trước + cách né. Trang trí theo CỤM kể chuyện, không rải ngẫu nhiên (người dùng sẽ gỡ hết).
15. Vật có hình trong map = **tile object** người dùng thấy và kéo được trong Tiled (lớp `mechanics_art`, tileset `SR-mech-art.tsx`), không phải hình chữ nhật + thuộc tính `sprite`.
16. Sau mỗi cột mốc: thêm mục vào `SOUL_REQUIEM_NOTES.md` (**tiếp theo là §244**) và báo cáo ngắn: đã làm gì, lỗi đã sửa, checklist test, việc còn lại.

---

## 3. Đường dẫn

| Thứ | Đường dẫn |
|---|---|
| Project Unity | `C:\Users\Admin\Soul Requiem` (Unity 6000.3.6f1, URP 2D). Code: `Assets/Scripts/` (Core, Gameplay, Visual, UI, DevTools) |
| Scene | `Assets/Scenes/Title.unity`, `Map1_GreystonePlains.unity`, `Map2_UndercityOfVael.unity`, `Map3_ValeOfGraves.unity`, `Map3b_KingsBarrow.unity`. **Không còn scene Map 4** (bỏ 15/11) |
| Map 1 .tmx | `Assets/GamePackage/ERW - Grass Land 2.0 v2.1/TiledMap Editor/Maps/map1_greystone_plains.tmx` — md5 **`0691f429bb1b212eec6e4e0fc8f92edc`** |
| Map 2 .tmx | `Assets/GamePackage/EPIC RPG World - Sewers V1.5/TiledMap Editor/Maps/map2_undercity_of_vael.tmx` — md5 **`8d0e0d06bec59f33a19c7ff93c12c5f2`** |
| Map 3 .tmx | `Assets/GamePackage/EPIC RPG World Pack - Cemetery V 1.6/TiledMap Editor/Maps/map3_vale_of_graves.tmx` — md5 **`b3a87fd34c7aac10b7c902837a64246f`** |
| Map 3b .tmx | cùng thư mục, `map3b_kings_barrow.tmx` — md5 **`9291ab1865dac614d98ab9f96c9b8002`** |
| Map 4 cũ (BỎ) | `…Crypt V.1.6/…/map4_catacombs.tmx` và `…Old Prison…/map4_catacombs.tmx` còn nằm đó nhưng không dùng; menu Build All đã bỏ Map 4 |
| Script sửa map theo lore mới | `_tools/lore_1116/` (`patch_lib.py`, `makemeta.py`, `wave_*.py`) |
| Sao lưu | `_backups/2026-11-16_*` (đợt lore), `Catalog/backups/` (cũ hơn) |
| Tranh cutscene | `Assets/Resources/Story/*.png` (256×144, meta pivot giữa) |
| NPC PixelLab | `Assets/Resources/NPCs/<id>/idle_S_00.png …` (canvas 144, chân ở hàng 120): `mb_todd`, `pell`, `aldous`, `goblin_child` |
| Nhiệm vụ / minimap | `Assets/Resources/Quests/<Scene>.json`, `Assets/Resources/Minimap/<Scene>.png` |
| Vũ khí | `Assets/Resources/Weapons/` (WeaponDefinition), `Weapons/Designs/` (bảng UV 64×32) |
| Mantra | `Assets/Resources/Skills/` |
| Lưu game | `Application.persistentDataPath/soulrequiem_save.json` |
| Lore gốc của người dùng | `Cottruyenmoi.docx` (người dùng giữ; xin gửi lại nếu cần chi tiết) |

---

## 4. Quy ước kỹ thuật

- Ô 32 px, **PPU 100 → 1 ô = 0,32 đv**. Toạ độ Tiled tính bằng ô (y hướng xuống). Tiled → Unity bằng **SuperTiled2Unity**.
- Layer logic trong Tiled để opacity 0.35, **không ẩn** (ẩn = ST2U bỏ qua). `unity:IsTrigger` đặt trên từng object.
- Thuộc tính .tmx giữ CRLF; xuống dòng trong giá trị thuộc tính viết literal `\n`. Kiểu `bool` / `float` phải khai `type=`.
- Phím: J/chuột trái chém, Q lăn, E bình máu, F/R/C Mantra I/II/III, **T tương tác**, M bản đồ, B túi đồ, Esc dừng.
- Phím test (DevCheats, chỉ Editor): F1 bảng, F2 bất tử, F3 hạ hết quái, F4 khu kế, F5 giải đố + mọi chìa, F6 mở Mantra, F7 boss −25%, F8 tốc độ, F9 tải lại, F10 đổi katana.
- Menu Unity: **Soul Requiem ▸ Scenes** (Build Map N Scene, Build All Map Scenes), **▸ Enemies**, **▸ Player**. Sửa .tmx xong người dùng chạy Build Scene.
- `MapEncounterDirector.EnsureMechanics()` tự gắn phần lớn hệ thống (BossIntro, BossRelic, ArenaSeal, VillagerDirector, **StoryScenes**…).

---

## 5. Công cụ sửa map hiện dùng (`_tools/lore_1116/`)

```python
import sys; sys.path.insert(0,'.')
from patch_lib import Doc
d = Doc('<đường dẫn .tmx>', '../../_backups/<ngày>_<đợt>')
assert d.md5_before == '<md5 hiện tại>', d.md5_before
d.set(oid, 'script', '...')                      # đặt thuộc tính object theo id
d.add('triggers', 'name', 'story', tx, ty, tw, th, {...})   # thêm object (toạ độ ô)
d.sub(...) / d.gsub(...)                          # thay chuỗi; d.src là nội dung XML
print(d.err, d.save())                            # save() sao lưu + ghi + trả md5 mới
```
- `makemeta.py <template.meta> center|feet <png…>`: tạo .meta cho sprite (template `Assets/Resources/NPCs/mb_todd/idle_S_00.png.meta`, hoặc `Resources/Story/epic_classes.png.meta` cho tranh).
- Map 2/3 cũ còn công cụ `Catalog/v2/` (minimap bake `Catalog/v2/tools/minimap_bake.py <Scene>`, `qa_prop_colliders.py`). **Không chạy generator Map 1/2/3** (ghi đè sửa tay).

---

## 6. CỐT TRUYỆN — LORE ĐÃ CHỐT (15–16/11/2026, thay mọi bản trước)

> Bản cũ (Choir of the Requiem, Naming = nối linh hồn với Vael, ánh tím, Ichor…) **đã bỏ**. Khi NOTES cũ lệch, theo mục này.

### 6.1 Hai thực thể và Ether
- **The Vael**: hỗn mang bất tử, là khái niệm (thù hận, cạnh tranh). Chỉ **khuếch đại** thứ đã có sẵn trong người, không tạo ra. Không bao giờ bị diệt ở bất kỳ kết nào.
- **Ellious = The Ascended**: lòng trắc ẩn, khái niệm đối cực của Vael. Chọn sinh ra làm người. **Thân xác chết, khái niệm thì không** → mọi nỗ lực hồi sinh Ngài bằng cách ghép xác đều sai từ gốc.
- **Ether chỉ có 3 màu** (đã chốt, bỏ tím hoàn toàn):

| Màu | Nguồn | Ai dùng | Tác dụng |
|---|---|---|---|
| **Xanh dương** | Ellious nguyên bản, lưu trong kinh cổ của Children of Light | **Ren (Mantra)**, tín đồ gốc Children of Light | Mạnh, hiếm, không biến đổi người dùng |
| **Đỏ** = Mantra của The Vael | Ether nhiễm Vael (máu thánh trong hồ Nova, dịch Vael trong xác thánh) | Hoàng tộc, quý tộc (nhà Vey, Darwin, the Haloed), Soul Requiem, Requiem Door, Specimen Zero | Bất tử nhưng phải hút máu; khuếch đại thù hận. Mantra đỏ trả giá bằng máu |
| **Xanh lá** | Plague Eye (mắt trái thánh, Malric hồi sinh) | Awakened, goblin, quái Undercity, quân hoàng gia | Biến đổi cơ thể, tăng thể lực; bản thô làm dại đi |

- **Lễ Awakening** (từ năm 780): trẻ đến tuổi uống Ether xanh lá — "the Gift". **Awakened** khoẻ hơn, đi lính / lao dịch, được vua bảo hộ. **Unawakened**: chưa uống, tầng đáy, không ai bảo vệ. Soul Requiem gọi cùng lễ đó là **Naming**, người chưa uống là **Nameless**. Plague Eye sai khiến được mọi người mang Ether xanh lá / đỏ → chỉ người Nameless mới lấy được nó.

### 6.2 Biên niên (lịch tính từ Angel Bird: 1 TCN = năm Ngài chết; hiện tại **năm 1176**)
- Thời sơ khai: ba vương quốc **Arden, Miravel, Kroven**; Miravel dưới vua **Arthur**.
- 34 TCN Arthur mất → nội chiến. 32 TCN **Borrow** thống nhất Miravel. 31 TCN vua **Nolan** (Kroven) tử trận. 30 TCN trận Miravel–Arden 600.000 người chết → **The Vael thức dậy**, Borrow hoá tàn bạo.
- Thu 29 TCN **Ellious sinh ra làm người** ở phía Tây Arden. Vua **Lucas** (Arden) truy lùng. 15 TCN Lucas tử trận, Borrow chiếm thế giới.
- 7 TCN dân gọi Ngài là **The Ascended**; Ngài lật đổ Borrow, Borrow tự vẫn ở **Sinking Fields** (xác không tìm thấy). 6 TCN lập **Children of Light**. 5 TCN **Thrag** (con Lucas) lên ngôi.
- **1 TCN — Angel Bird**: **Darwin** (tông đồ + quý tộc thân cận Thrag) gieo sợ hãi; Thrag và quý tộc (có **Aurel Vey**) hành hình The Ascended. Máu chảy xuống **hồ Nova** → ai uống thành ma cà rồng. Đàn chim trắng bay lên lúc Ngài tắt thở. Darwin "trừng phạt" Thrag, được tôn làm vua; vương quốc đổi tên **Vael**. **Luke** và **Emma** biết sự thật, mang **mắt trái** và **tay trái** của xác thánh trốn đi.
- 1–476: Children of Light thờ xác thánh trong hầm mộ (sau là Catacombs of Naming), dần cuồng tín thành **Soul Requiem**. Thế kỷ đầu: hoàng gia thu xác về kho chia phần; **Darwin hợp nhất trái tim** → bất tử, không cần hút máu; mỗi 100 năm nâng một hoàng tộc lên (**the Haloed**, nay 11 người). Nhà Vey uống hồ Nova (nay giấu trong hầm mộ hoàng gia, gọi **the Faint Lake**).
- ~476 **Hierophant Absalom** (dòng Emma, tay trái thánh ghép trên vai) dùng Ether đỏ dựng thân xác mới, trộm chân, tay phải, mắt phải. **676**: thiếu tim + mắt trái → **Specimen Zero** (xác rỗng, bị hạ thì 100 năm sau trở lại).
- 724 **Malric** (hậu duệ Luke) tìm thấy mắt trái. 745 đọc nhật ký Luke. 757 bắt đầu thí nghiệm dưới lòng đất (Undercity). 763 gặp Absalom. 766 hợp tác, hiến tế người cho Specimen Zero. 769 vỏ Plague Eye. 770 Specimen Zero thức sớm, bị ru ngủ lại. 771 **Plague Eye** sống, tiết Ether xanh lá (chuột đột biến, ếch khổng lồ, người hoá goblin; vài con giữ trí người).
- **776**: Specimen Zero thức hoàn toàn, tàn sát (**the Drowning**). Malric dẫn năng lượng nó phong ấn Plague Eye (hệ thống bơm + ống — chính là câu đố ống Map 2). Mùa đông Malric chết. Absalom thoát.
- **780**: hoàng gia phái **Isolde Vey** quản Ether xanh lá (bản Malric tinh chế), truy nã Absalom. Ether ban cho dân như **the Gift** → lễ Awakening.
- 780–1176: goblin, quái biến dị thành chuyện thường. Soul Requiem thành tà giáo ở vùng mộ địa hoàng gia, lập quân **Black Lantern** và hàng giáo sĩ **the Choir**. Specimen Zero trở lại 876, 976, 1076. Giáo phái bắt trẻ trước Awakening nuôi "rỗng" để lấy Con Mắt: **Candidate I–VI đều chết**.
- ~1156 **Candidate VII** ra đời, thất lạc, được **nhà Corwen** nuôi → **Ren**.
- **1176**: kỳ trở lại thứ năm của Specimen Zero. Đất rung khắp Greystone. Game bắt đầu.

### 6.3 Xác thánh — ai giữ phần nào (xương sống của game: "ai cũng giữ một phần của Ngài và gọi đó là chính nghĩa")
| Phần | Người giữ 1176 | Lý do họ nói | Map |
|---|---|---|---|
| Máu (hồ Nova) | Đại gia tộc, nhà Vey | "Chúng ta xứng đáng đứng trên" | 3 |
| Mắt trái | Luke → Malric → Plague Eye → **Ren** | "Mọi người phải bình đẳng" | 2–4 |
| Tay trái | Emma → Absalom | "Ta là bàn tay sẽ nâng Ngài dậy" | 4–5 |
| Chân, tay phải, mắt phải | Specimen Zero | không còn ý chí | 2, 5 |
| **Tim** | **Darwin** | "Chỉ ta đủ sức giữ trật tự" | 5 |

Tim giữ cả thân xác khỏi bị Vael nhiễm; thiếu tim mọi thí nghiệm hỏng. Không ai giết được Darwin; chỉ có thể lấy lại trái tim khỏi ngực hắn. **Darwin tự viết lời tiên tri** ("Con Mắt được giải thoát bởi kẻ không có tên") và cố ý để Soul Requiem sống để gom xác + tìm Nameless hộ mình. Giáo phái không biết cho tới Map 4.

### 6.4 Thế lực 1176
- **Vương triều Vael**: Darwin (Eternal King), 11 Haloed, Đại gia tộc (nhà Vey). Quái: Vey Kin, Huntress, Isolde.
- **Soul Requiem**: Hierophant Absalom; the Choir (Precentor Morrow, Choir Novice); quân Black Lantern (Warrior/Lancer/Archer/Monk); Lantern Knight. Màu đỏ. Trong Map 1–3 chỉ gọi là "the hooded one" / giáo phái.
- **The Risen**: xác chết Vale of Graves đứng dậy vì Ether tràn. Xanh lá.
- **Unawakened**: dân thường như Millbrook (Todd).
- **Goblin**: con cháu người uống Ether thô trước 780. Bộ tộc Greystone, thờ "the Eye Below". Pell.

### 6.5 Nhân vật
- **Ren**: kiếm khách ~20 tuổi, Unawakened, thật ra là Candidate VII; nhà Corwen nuôi, trao Ronin's Katana. Muốn: Map 1 giúp người → Map 2 thoát → Map 3 gỡ Con Mắt → Map 4 sống → Map 5 trả lại.
- **Todd** (trưởng làng Millbrook), **Grok** (tù trưởng goblin, dâng thóc cho "the Eye Below").
- **Pell**: goblin còn trí người, học trò út của Malric, sống hơn 400 năm. Từng cầm dao trong vụ hiến tế 766. Giữ lời hứa với thầy: không để ai phá ấn. Chết ở Map 4 (đỡ tia cho Ren).
- **Aldous**: NPC Map 3 (Vale of Graves).
- **Isolde Vey**: con gái Aurel Vey, ma cà rồng, quản Ether xanh lá từ 780. Muốn Con Mắt để đổi lấy việc được Darwin "nâng". Có thể tha (Map 3).
- **Precentor Morrow**: chỉ huy cuộc săn Ren ở Map 3, muốn bắt Ren sống.
- **Candidate VI**: vỏ rỗng hứng Con Mắt ở Map 4, có thể tha.
- **Absalom**: lãnh đạo Soul Requiem hơn 700 năm, thật lòng yêu vị thần; giấc mộng hắn tin là lời Ngài thật ra đến từ dịch Vael.
- **Darwin**: kẻ phản bội, vua bất tử mang tim thánh. Muốn đủ xác để thành thần, vì "thần không cần ai tha thứ".
- Chỉ hiện qua ghi chép: Arthur, Borrow, Nolan, Lucas, Thrag (bia mộ, biên niên Map 3–4); Luke, Emma (nhật ký Luke Map 2); Aurel Vey (gia phả Map 3); Malric (nhật ký Map 2); Odo, Brakk (nạn nhân Undercity).

---

## 7. MẠCH TRUYỆN 5 MAP

| Map | Kẻ giữ | Ren muốn | Sự thật lộ ra | Ren rời map với |
|---|---|---|---|---|
| 1 Greystone Plains | nạn nhân the Gift | giúp một làng | goblin từng là người | cú rơi xuống lòng đất |
| 2 Undercity of Vael | Malric (mắt) | thoát ra | muốn thoát phải phá phong ấn Malric chết để dựng | Con Mắt trong người, và một lỗi lớn |
| 3 Vale of Graves | nhà Vey (máu) | gỡ Con Mắt | ai cũng muốn Con Mắt, không ai muốn cứu Ren | bị ném sống tới nơi làm lễ |
| 4 Catacombs of Naming | Absalom (tay) | sống sót | Ren là Candidate VII; giáo phái là công cụ của vua; Darwin là kẻ phản bội | mất Con Mắt, có lại tên, mất Pell |
| 5 The Faint Lake | Darwin (tim) | chặn lại, rồi trả lại | không thể hồi sinh lòng trắc ẩn bằng cách ghép xác | một trong ba kết |

**Map 1** (đã có): Ren qua Millbrook (làng Unawakened). Mỗi kỳ trăng mới lính thu thuế của Grok xuống lấy thóc; thiếu 3 bao → đốt trại nhà Harlan. Todd nhờ diệt Grok. Trong trại goblin: truyền miệng "trước nước xanh họ là người"; thóc ném xuống hố Stone Ring dâng "the Eye Below". Đất rung từng đợt. Chuỗi nhân quả: ca viên Soul Requiem ("the hooded one") xúi goblin đào Stone Ring (nắp ấn nhà Vey 780) → Ether rỉ → địa chấn → vòng đá sập khi Ren hạ Grok → rơi xuống Map 2.
**Map 2** (đã có): tiếp đất trong lab Malric. Lối ra duy nhất là ống thoát trên hố Plague Eye, không chạm được khi bể đầy. Gặp **Pell** (giữ tay quay, cảnh báo bơm cạn = phá ấn). Lời Malric ở Pump Control: *"Do not turn it. I am asking you as a dead man."* Ren vẫn quay. Hạ Plague Eye → Con Mắt bám vào Ren (Nameless) → bộ **Mantras of the Eye** → Ether trào lên mặt đất. Sau lưng, giáo phái xuống lấy Specimen Zero và bắt Pell. Tuỳ chọn: Specimen Zero (Containment Vault) → Moonlit Edge.
**Map 3** (đã có): Ether tràn thung lũng, xác đứng dậy — lỗi của Ren. Soul Requiem phong toả, 3 chuông cộng hưởng Con Mắt. House Vey: Isolde muốn mổ Con Mắt khỏi xác Ren; tha hoặc giết. Sinking Fields → King's Barrow (Map 3b, mộ Borrow, Crimson Oath). Chapel of Naming. Hạ Morrow → hấp hối mở Requiem Door → Ren bị kéo đi (scene hiện tự rời map qua exit `to_map4`).
**Map 4** (CHƯA LÀM, thiết kế lại từ đầu): Ren tỉnh trong ngục, Mantra bị phong; cứu Pell, Pell dẫn đường → thấy Black Lantern khiêng Specimen Zero → sổ gốc: Ren là Candidate VII, thư mẹ xin con về → biên niên ghi tên kẻ phản bội bị xé trong nhật ký Luke: **Darwin** → bản gốc lời tiên tri có dấu ấn hoàng gia → Hall of Naming: Absalom xuất hiện lần đầu, Candidate VI dùng chính chiêu của Ren → **The Severing**: Con Mắt bị xé khỏi Ren, Pell chết che chắn, Ren nhận **Mantras of the Name** → Absalom mang Con Mắt đi: *"Only the heart remains. The King will bring it himself."* → lối ra mở sẵn nhưng Ren đi xuống theo. **Các cutscene sự thật (tranh Angel Bird `Resources/Story/angel_bird_1..4`, nhật ký Luke) để dành cho Map 4.**
**Map 5** (CHƯA LÀM): the Faint Lake trong lòng núi lửa (gói Volcano). The Pilgrim Stair (giáo phái vs lính hoàng gia) → Crypt of the Haloed (tha Isolde thì bà mở lối tắt) → The Forge (Wall of Voices; hạ Absalom; Darwin giật tay Absalom, mang thân xác xuống hồ) → trận cuối 3 pha: **The Haloed King** (kiếm sĩ) → **The Remade God** (dùng lại chiêu của Plague Eye, Specimen Zero, Isolde) → **The Return** (phá 3 neo, nói tên thật để lấy tim; tha Absalom thì hắn giữ một neo).

**Seven Mercies** (tuỳ chọn, không thưởng ngay, không nhiệm vụ nhắc): `mercy_1` Map 1 để lại tonic cho goblin con (đã làm) · `mercy_2` Map 2 đưa Pell trang nhật ký cuối của Malric (đã làm) · `mercy_3` Map 3 tha Isolde (đã làm, `isolde_slain` nếu giết) · `name_reclaimed` Map 4 đọc hết sổ tên · `parents_heard` Map 4 phòng thư của mẹ · `candidate_vi_freed` Map 4 · `mercy_5` Map 5 tha Absalom.

**Ba kết** (Ren cầm trái tim trên bãi hành hình):
- **The Face** (luôn có): đặt tim vào ngực mình → vua bất tử mới; cảnh cuối Millbrook nhận the Gift từ tay Ren.
- **Silence** (luôn có): dìm tim và xác xuống hồ → Ether tắt, không còn bất tử, cũng không còn phép lạ.
- **Requiem** (kết thật, cần `name_reclaimed` + ≥ 5/7 mercy): đặt tim về thân xác, đọc Mantra như lời cầu hồn để **an táng** → thân xác tan thành ánh xanh dương, linh hồn trên Wall of Voices được thả. Vael vẫn còn ở cả 3 kết.

**Câu hỏi còn mở**: giữ tên **Borrow** (vần với Morrow) hay đổi? Giữ tên **Darwin** hay đổi (vd Darven)? Người dùng chưa trả lời.

---

## 8. CẢNH MỞ ĐẦU MAP 1 (bản hiện tại, người dùng duyệt từng câu 16/11)

Story `prologue` (trigger `start`, `unless intro_done`) trong map1. Lộ trình người dùng chốt: Vael tồn tại bao lâu → công lao của ai (Darwin) → dân nhận the Gift từ bao giờ → các giai cấp → hoàn cảnh Ren. **Không** có chiến tranh ba nước (quá dài), không cảnh nông trại, Ren tới Millbrook **ban ngày**.

| Tranh (`Resources/Story/`) | Lời dẫn (mỗi `|` là một trang) |
|---|---|
| prologue_1 | For 1,176 years, one kingdom has stood where three once warred - and one king has ruled it for every one of those years. \| He does not age. He does not die. His people call him the Eternal King. |
| prologue_2 (chim trắng trong đĩa mặt trời) | Every child knows how it began. A holy man, the Ascended, ended the age of war - and was murdered by Thrag, a jealous king. \| His most faithful apostle, Darwin, cut the murderer down and bound the warring crowns into one realm. Heaven blessed him with a life without end. |
| epic_gift_light (cột sáng xanh ở thánh đường, dân quỳ) | In the Year 780, a light rose from the holy ground where the Ascended once wept for His people - green as the fields that grew wherever He walked. \| The King proclaimed it the Tears of the Ascended: His last gift, kept sealed since His death for the day His people would need it. |
| epic_gift_strong (dân dựng nhà, nâng xà, ôm đá — không lính) | All who drank grew strong. The sick rose from their beds. One pair of hands could lift what four had carried, and men worked from dawn to dark without tiring. \| Since that day, every child of age drinks the Tears at the Awakening. |
| epic_classes | Above all stand the great Houses, blessed by Heaven at the King's side. They do not grow old. \| Below them, the Awakened: soldiers and labourers, strong with the Gift and protected by the Crown. |
| prologue_4 | Last come the Unawakened - those who never drank. No soldier marches to save their villages. \| And in the hills, the goblins: those who drank from wild springs instead of the King's cup. So the priests say. |
| prologue_6 | It is the Year 1176 of the Eternal King. Ren is a wandering swordsman. He has never taken the Gift. \| He draws his blade for those no soldier will defend. It pays poorly. He keeps doing it. \| This year the ground itself has begun to tremble across the Greystone Plains. No one can say why. |
| prologue_7 (khói trên Millbrook, ban ngày) | On the morning of the new moon, smoke rises over the village of Millbrook. Ren quickens his pace. |

"Lời nói dối" về the Gift được che bằng bản sử chính thống: ánh sáng từ đất thánh, "nước mắt của Ngài", xanh như cánh đồng nơi Ngài đi qua, lợi ích có thật. Chỉ nguồn gốc là giả → cú lật ở Map 4.
Tranh dự phòng chưa dùng: `epic_gift_harvest.png` (gặt lúa), `epic_madness`, `epic_three_crowns`, `epic_war`, `epic_butcher_king`, `epic_ascended`, `prologue_3/5`, `angel_bird_1..4` (để Map 4).

---

## 9. HỆ THỐNG MỚI (16/11) — cảnh cốt truyện bằng dữ liệu Tiled

**StoryScenes** (`Assets/Scripts/Gameplay/World/Cutscene/StoryScenes.cs`): object `type="story"` trong lớp `triggers`.
- Thuộc tính: `trigger` = `enter` | `interact` | `flag` | `start`; `when` (cờ cần có), `unless` (có cờ nào thì bỏ), `once`, `repeat`, `radius`, `verb` (chữ nút T), `range`, `script`.
- Lệnh script (mỗi dòng một lệnh, ngăn bằng `\n`): `cam X,Y` · `camren` · `wait s` · `say WHO: trang1|trang2` · `narrate: …` · `bubble ren|npc|X,Y: text` · `hint text` · `shake` · `beat` · `glow` · `show/hide <tên>` · `set <cờ>` · `face` · `walk` · `fade out|in s` · `slide Resources/PATH: trang|trang` (tranh toàn màn hình) · `slide end` · `exit <tên exit>`.
- `~` trong lời thoại = xuống dòng trong cùng trang (hiện KHÔNG dùng, người dùng không muốn tiêu đề).
- `StoryScenes.Busy` → OpeningCutscene chờ. Khi đang mang Con Mắt (`eye_relic_taken` và chưa `eye_severed`) Con Mắt thì thầm ở điểm nghỉ.
- HUD: mũi tên + "57m" khoảng cách đích ẩn khi `CutsceneState.Active`.

**BossMercy** (`Enemies/Bosses/BossMercy.cs`): `BossMercy.Begin(ai, spareFlag, slainFlag)` — boss quỳ ở ngưỡng máu; chém = kết liễu (slainFlag); [T] "Spare her" = tha (spareFlag), boss hoá dơi biến mất, khu được dọn. Đang dùng cho Isolde (`mercy_3` / `isolde_slain`).

**Pickup `unless`**: pickup có `unless` → nếu cờ có thì coi như đã nhặt (dùng cho tonic `mercy_1`).

**Cảnh đã đặt**: Map 1 `prologue`, `tremor_caves`, `tremor_ruins`, `ring_look`, `give_tonic`, NPC `goblin_child` · Map 2 NPC Pell, `pell_meet`, `pell_page` (mercy_2), `hub_grate`, `pump_seal`, pickup `malric_page` · Map 3 NPC Aldous ×2, `outfall`, `commons_bell`, `aldous_meet`, `aldous_field`, `morrow_door` (→ exit `to_map4`).

---

## 10. TRẠNG THÁI & VIỆC CÒN LẠI (theo thứ tự ưu tiên)

**Kế hoạch sửa Map 1–3 theo lore mới (người dùng đã chốt 9 câu, 16/11):**
- Đợt A (sửa chữ) + B (đổi tên khu): **xong**. Tên khu mới: Map 1 *The Seer's Tent*, *Cloister Ruins*; Map 2 *The Vat Hall* (+ chìa *Pump Control Key*); Map 3 *The Precentor's Court*, *King's Barrow*… (chi tiết: NOTES §235–§236, script `_tools/lore_1116/wave_abc.py`).
- Đợt C (tím → đỏ): **xong** (16/11 02h20, NOTES §243) — đèn, ánh nền, Specimen Zero, ảnh rễ / bàn tay / Requiem Wail / Bound / icon Vessel, tint trong code. Chưa chơi thử.
- Đợt D (NPC + cảnh): **xong code, biên dịch 0 lỗi, đã dựng scene**. Mới chơi thử cảnh mở đầu. **Chưa chơi thử**: vị trí NPC, tha Isolde, Morrow tự rời map, các cảnh Map 2–3.
- **Đợt E (chưa làm)** — hầm bí mật + thưởng:
  - Map 1: hầm **1b The Hermit's Hollow** → katana **Stillwater**, Mantra **Canticle of Stillness** (xanh dương).
  - Map 2: hầm **2b The Kroven Deep** (tái dùng code bẫy Map 4 cũ) → katana **Unbowed**, sách **The Broken Chain**; Mantra **Ether Amber** (xanh lá); đổi tên chiêu Ichor Flask → **Blight Flask**.
  - Map 3: **King's Barrow** + buồng **The Red Field** (**boss bí mật**); **The First Cup** → katana **Nightvow** (sáu kiếm thề; Crimson Oath = kiếm của Borrow); **3c The Hall of Three Crowns** → katana **Concord**; Mantra **Dirge**, **Amen** (xanh dương), **Blood Tithe** (đỏ — tha Isolde thì mất Blood Tithe).
  - Nguyên tắc: di tích ba vương quốc **chỉ nằm dưới lòng đất** (map con); mặt đất là kỷ nguyên Vael. Thưởng theo thời đại: ba vương quốc = katana + sách kiếm pháp; Children of Light = Mantra xanh dương; Malric / goblin = xanh lá; quý tộc / giáo phái = Mantra đỏ.
- **Map 4**: thiết kế lại từ đầu theo mục 7 (đưa phương án cho người dùng duyệt trước).
- **Map 5**: thiết kế đầy đủ sau Map 4.

**Tồn đọng cũ**: hệ thống âm thanh (chưa có); giao diện đổi phím; cân bằng boss Map 2–3; rác trong `_to_delete/`, `Assets/_Recovery/` chờ người dùng quyết.

**Checklist test cho người dùng (chưa chạy)**: Ctrl+R → Build All Map Scenes → chơi Map 1 từ đầu (cảnh mở đầu 8 tranh, không còn tiêu đề, không hiện "57m"; 2 lần rung đất; nhìn xuống hố; đưa tonic cho goblin con) → Map 2 (gặp Pell sau r6, trang Malric, lời Malric ở Pump Control) → Map 3 (Outfall, chuông, Aldous, Isolde quỳ: chém / T tha, Morrow gục → tự chuyển).

---

## 11. Lỗi hay gặp — đừng lặp lại

- Layer logic ẩn trong Tiled → ST2U bỏ qua. `unity:IsTrigger` đặt từng object.
- Thuộc tính sai kiểu (bool viết thành string, scale không `type="float"`) → không đọc được.
- `gsub` phân biệt hoa thường → kiểm `count == 1` trước khi thay.
- **Build All Map Scenes từng dựng lại scene Map 4 cũ** → đã bỏ khỏi Build All; không gọi `BuildMap4()`.
- `device_bash` grep cả `Packages/` → timeout; thu hẹp phạm vi.
- `device_commit_files` có lúc không ghi đè → kiểm md5 / pixel sau khi ghi.
- Cổng đấu trường đè hành lang → khoá người chơi ở ngoài; khu boss: khung khu = đúng sàn phòng. Nới phòng phải sửa cả `blockers`.
- Ảnh có lề trong suốt ở đáy → vật đè nhân vật. `.meta` tới trước PNG → Unity xoá meta.
- Vật đứng phải ở lớp `props` + có khối chặn ở chân, không để ở `ground decals`.
- Cutscene: người dùng phản đối spoil, hình Jesus, cảnh đêm khi game là ban ngày, tiêu đề chương, chữ năm viết bằng chữ, cảnh điên loạn, lính trong tranh "dân khoẻ mạnh". Luôn xem tranh trước khi đưa vào.
