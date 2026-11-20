# SOUL REQUIEM — Bản bàn giao (17/11/2026, 17:10 — Map 4: bố cục + trang trí xong, .tmx đã ghi; bước 3 chưa làm; nội dung tới §301)

> Dành cho một phiên / tài khoản Claude **hoàn toàn mới**. Đọc hết file này trước khi làm gì.
> Bản trước sao lưu ở `_backups/2026-11-17_handoff_map4/` (bản 17/11 14:40), `_backups/2026-11-16_handoff/` (bản 05/11 và bản 16/11 02:10) và `_backups/2026-11-17_entrance_r5/` (bản 17/11 01:40).
> Thứ tự đọc: **file này** (đặc biệt mục 10 "Trạng thái & việc còn lại" và mục 11 "Lỗi hay gặp") → `ARCHITECTURE.md` (kiến trúc code)
> → `SOUL_REQUIEM_NOTES.md` (nhật ký, đọc **từ dưới lên**; phiên 16/11 là §243–§275, phiên 16/11 tối – 17/11 sáng là §276–§286)
> → `HANDOFF_REN_KATANA.md` nếu đụng tới nhân vật Ren / katana.
> → `SOUL_REQUIEM_KATANAS.md`: bộ katana đã chốt 16/11 (tên, lore, nội tại, nơi nhặt, màu) — nguồn sự thật cho mọi katana.
> Các link claude.ai/artifact trong NOTES thuộc tài khoản cũ, tài khoản mới **không mở được**. Mọi thứ cần biết về cốt truyện đã chép đủ vào mục 6–8 dưới đây.

---

## 1. Tin nhắn mở đầu gợi ý cho phiên mới

> Tôi đang làm game **Soul Requiem** (Unity 6 URP 2D, top-down action dark fantasy). Hãy đọc
> `C:\Users\Admin\Soul Requiem\SOUL_REQUIEM_HANDOFF.md`, rồi `ARCHITECTURE.md`, rồi các mục cuối của
> `SOUL_REQUIEM_NOTES.md` (nhất là §295–§301 = Map 4 đang làm, rồi §276–§294) và `SOUL_REQUIEM_KATANAS.md` trước khi làm gì. Làm đúng mục "Quy tắc làm việc". Trả lời tôi bằng tiếng Việt.

Phiên mới cần **liên kết với máy** (mở chat trong app Claude desktop) và quyền thư mục:
`C:\Users\Admin\Soul Requiem` (bắt buộc), `C:\Users\Admin\Desktop\Catalog` (công cụ map cũ),
`C:\Users\Admin\AppData\Local\Unity\Editor` (đọc `Editor.log` để xem lỗi biên dịch).
Qua cầu nối, thư mục project hiện ở `$HOME/mnt/Soul Requiem` trong `device_bash`.
PixelLab: dùng qua MCP PixelLab (tài khoản người dùng; phiên 16–17/11 đã kết nối, còn ~2.800 lượt tới 10/12). Không đăng nhập / nhập mật khẩu hộ.
Tài sản PixelLab đang dùng: nhân vật **The Last Guard v2 (A)** `a3d65a38-4d9a-4bd4-b316-43c042a73159` (pha 1, khiên + giáo) + trạng thái **Last Stand** `c6c3511e-2105-42a4-b622-9fbd45f537c9` (pha 2, giáo hai tay);
style chung = Lantern Knight `49d0004b`. Hiệu ứng trong My effects: LG Whirl / LG Slam / LG Thrust / LG Block Spark / LG Ether Aura / LG Dust / LG Guard Break / LG Stun (vfx_workbench, miễn phí).
**Cảnh báo 17/11:** trong `_tools/kroven_2b/prev/` từng xuất hiện ảnh render do một phiên KHÁC tạo cùng lúc -> nếu chạy nhiều phiên song song, luôn kiểm md5 trước khi sửa .tmx (script patch đã assert md5).

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
15b. **Map dựng bằng script phải qua kiểm tra trước khi gửi ảnh** (bài học Map 4, §300): (1) đi thử theo từng giai đoạn mở cổng, không đường tắt;
   (2) kiểm chân MỌI đồ vật theo loại ô thật (sàn / mặt bệ / lan can / mặt tường / đá) = 0 lỗi; (3) cửa khớp đúng loại tường theo quy tắc §298;
   (4) xem từng khu ở tỉ lệ thật đặt cạnh ảnh mẫu của gói. Không đặt đồ bằng toạ độ ước lượng.
15. Vật có hình trong map = **tile object** người dùng thấy và kéo được trong Tiled (lớp `mechanics_art`, tileset `SR-mech-art.tsx`), không phải hình chữ nhật + thuộc tính `sprite`.
16. Sau mỗi cột mốc: thêm mục vào `SOUL_REQUIEM_NOTES.md` (**tiếp theo là §305**) và báo cáo ngắn: đã làm gì, lỗi đã sửa, checklist test, việc còn lại.
17. **Animation nhân vật nhiều hướng: đọc `_tools/ANIMATION_RULES.md` TRƯỚC khi tạo / nhập** (§292). Vũ khí chĩa theo hướng nhìn
    (S/N không bao giờ chĩa ngang), khiên che trước thân khi thủ / lao / đẩy, thân không xoay giữa clip, động tác đúng vật lý,
    khớp khung ra đòn với code. Soát TỪNG khung cả 3 hướng bằng sheet so sánh; có khung sai thì không nhập.

---

## 3. Đường dẫn

| Thứ | Đường dẫn |
|---|---|
| Project Unity | `C:\Users\Admin\Soul Requiem` (Unity 6000.3.6f1, URP 2D). Code: `Assets/Scripts/` (Core, Gameplay, Visual, UI, DevTools) |
| Scene | `Assets/Scenes/Title.unity`, `Map1_GreystonePlains.unity`, `Map2_UndercityOfVael.unity`, `Map3_ValeOfGraves.unity`, `Map3b_KingsBarrow.unity`, `Map1b_HermitsHollow.unity` (menu Build Map 1b Scene). **Không còn scene Map 4** (bỏ 15/11) |
| Map 1 .tmx | `Assets/GamePackage/ERW - Grass Land 2.0 v2.1/TiledMap Editor/Maps/map1_greystone_plains.tmx` — md5 **`faada035cb5b86d4c7cbcd051e076fc5`** (§303: 7 cây làng -> ellipse) |
| Map 2 .tmx | `Assets/GamePackage/EPIC RPG World - Sewers V1.5/TiledMap Editor/Maps/map2_undercity_of_vael.tmx` — md5 **`741bdd0e688e64bf0db7a3514d58a1d3`** (17/11 §295: người dùng sửa tay trong Tiled — dời đuốc torch_36 / torch_91, prop 500, 1232; xoá nến candle_91_402 + prop 1471; trước đó fc40540b… 17/11 §291: bỏ cảnh hub_grate lia camera vào lối bí mật Specimen Zero; 17/11 §286: lối xuống Kroven = cầu thang chìm + song sắt khoá vương miện ở Records Vault; bỏ nắp hầm Forgotten Store) |
| Map 3 .tmx | `Assets/GamePackage/EPIC RPG World Pack - Cemetery V 1.6/TiledMap Editor/Maps/map3_vale_of_graves.tmx` — md5 **`4cb9c3ea07a439c7eb72dcbe9330bead`** (§303: 2 trụ bát giác -> ellipse) |
| Map 3b .tmx | cùng thư mục, `map3b_kings_barrow.tmx` — md5 **`f0202c8aac1aa388449178c7f53f0939`** |
| Map 2b .tmx | thư mục Map 2, `map2b_kroven_deep.tmx` (SINH bằng `_tools/kroven_2b/gen_map.py`) — md5 **`4d7e20cb9072cf00f601f0250e0283a6`** (17/11 §295: người dùng dời 6 đuốc vách + đèn trong Tiled; `tmxio.USER_POS` áp lại khi sinh, sinh lại khớp từng object; trước đó 4740a1fc… 17/11 §285: Rolling Hall ngang + hốc 3x3, dải gai, cầu thang đá; .meta sortingMode 1). Scene `Map2b_KrovenDeep.unity` (menu Build Map 2b Scene). Boss The Last Guard v3: PixelLab a3d65a38 (pha 1) + c6c3511e (pha 2), xuất bằng `_tools/kroven_2b/import_last_guard.py` |
| Map 1b .tmx | thư mục Map 1, `map1b_hermits_hollow.tmx` (sinh bằng `_tools/lore_1116/hollow_map.py`) — md5 **`5961b7411676507160e80cd348738ac8`** (§303 collider tròn) |
| Map 4 cũ (BỎ) | `…Crypt V.1.6/…/map4_catacombs.tmx` và `…Old Prison…/map4_catacombs.tmx` còn nằm đó nhưng không dùng; menu Build All đã bỏ Map 4 |
| Map 4 .tmx (MỚI 17/11) | `…Crypt V.1.6/TiledMap Editor/Maps/map4_catacombs_of_naming.tmx` — SINH bằng `_tools/map4_1117/write_real.py` (+ trim_props) — md5 **`dde60fc1e5b0aeaf46bb70356768a23a`** (§304 logic phần 1); scene TẠM: menu Build Map 4 Scene (chép Map 3) |
| Script sửa map theo lore mới | `_tools/lore_1116/` (`patch_lib.py`, `makemeta.py`, `wave_*.py`) |
| Sao lưu | `_backups/2026-11-16_*` (đợt lore), `Catalog/backups/` (cũ hơn) |
| Tranh cutscene | `Assets/Resources/Story/*.png` (256×144, meta pivot giữa) |
| NPC PixelLab | `Assets/Resources/NPCs/<id>/idle_S_00.png …` (canvas 144, chân ở hàng 120): `mb_todd`, `pell`, `aldous`, `goblin_child` |
| Nhiệm vụ / minimap | `Assets/Resources/Quests/<Scene>.json`, `Assets/Resources/Minimap/<Scene>.png` |
| Vũ khí | `Assets/Resources/Weapons/` (WeaponDefinition), `Weapons/Designs/` (bảng UV 64×32) |
| Mantra | `Assets/Resources/Skills/` |
| Lưu game | `Application.persistentDataPath/soulrequiem_save.json` |
| Lore gốc của người dùng | `Cottruyenmoi.docx` (người dùng giữ; xin gửi lại nếu cần chi tiết) |
| Lore đã chốt (Word) | `SOUL_REQUIEM_LORE_2026-11-16.docx` (bản chờ chốt 15/11 đã cập nhật theo chốt 16/11; cuối file: thay đổi + câu hỏi mở). Bản markdown cũng lưu trong Project claude.ai `claude/lore_ban_chot_16-11.md` |

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
| **Xanh dương** | Ellious nguyên bản, lưu trong kinh cổ của Children of Light; **xác thánh khi tự do** | **Ren (Mantra)**, tín đồ gốc Children of Light | Mạnh, hiếm, không biến đổi người dùng |
| **Đỏ** = Mantra của The Vael | Ether nhiễm Vael (máu thánh trong hồ Nova, dịch Vael trong xác thánh) | Hoàng tộc, quý tộc (nhà Vey, Darwin, the Haloed), Soul Requiem, Requiem Door, Specimen Zero | Bất tử nhưng phải hút máu; khuếch đại thù hận. Mantra đỏ trả giá bằng máu |
| **Xanh lá** | Plague Eye (mắt trái thánh, Malric hồi sinh) | Awakened, goblin, quái Undercity, quân hoàng gia | Biến đổi cơ thể, tăng thể lực; bản thô làm dại đi |

- **Quy tắc màu của xác thánh (chốt 16/11)**: xác thánh mang Ether thuần của Ascended → khi **tự do** luôn sáng **xanh dương** (có chút hào quang), không bao giờ đổi màu. Chỉ khi bị gắn vào một **vật chủ mang tính xấu** nó mới nhiễm màu của vật chủ (Plague Eye → xanh lá, giáo phái / quý tộc / Specimen Zero → đỏ); **ra khỏi vật chủ là trở lại nguyên bản xanh dương**. Mọi cổ vật rơi từ boss: `relic_color` = `#8fd0ff`.
- **Màu Ether (lore) ≠ màu chiêu (hình ảnh)** — chốt 16/11 04h: màu Ether là năng lượng được *miêu tả* trong truyện; hình chiêu không bắt buộc cùng màu (như mana màu xanh nhưng phép có thể màu khác). Specimen Zero: năng lượng là Ether đỏ (Vael), nhưng **chiêu giữ màu tím gốc**. **Chiêu Ren lấy từ boss nào thì dùng chung màu / hình chiêu của boss đó** (bộ Vessel = tím như Specimen Zero) cho đỡ lạc quẻ. Đèn / ánh nền vault đã đổi đỏ ở đợt C vẫn giữ. Cổ vật rơi (xác thánh tự do) vẫn sáng xanh dương. **Katana** rơi / nằm trong khu boss nào thì tông màu theo chiêu boss đó (Moonlit Edge = tím như Specimen Zero, §250).
- **Lễ Awakening** (từ năm 780): trẻ đến tuổi uống Ether xanh lá — "the Gift". **Awakened** khoẻ hơn, đi lính / lao dịch, được vua bảo hộ. **Unawakened**: chưa uống, tầng đáy, không ai bảo vệ. Soul Requiem gọi cùng lễ đó là **Naming**, người chưa uống là **Nameless**. Plague Eye sai khiến được mọi người mang Ether xanh lá / đỏ → chỉ người Nameless mới lấy được nó.

### 6.2 Biên niên (lịch tính từ Angel Bird: 1 TCN = năm Ngài chết; hiện tại **năm 1176**)
- Thời sơ khai: ba vương quốc **Arden, Miravel, Kroven**; Miravel dưới vua **Arthur**.
- 34 TCN Arthur mất → nội chiến. 32 TCN **Borrow** thống nhất Miravel. 31 TCN vua **Nolan** (Kroven) tử trận. 30 TCN trận Miravel–Arden 600.000 người chết → **The Vael thức dậy**, Borrow hoá tàn bạo.
- Thu 29 TCN **Ellious sinh ra làm người** ở phía Tây Arden. Vua **Lucas** (Arden) truy lùng. 15 TCN Lucas tử trận, Borrow chiếm thế giới.
- 7 TCN dân gọi Ngài là **The Ascended**; Ngài lật đổ Borrow, Borrow tự vẫn ở **Sinking Fields** (xác không tìm thấy). 6 TCN lập **Children of Light**. 5 TCN **Thrag** (con Lucas) lên ngôi.
- **1 TCN — Angel Bird**: **Darwin** (tông đồ + quý tộc thân cận Thrag) gieo sợ hãi; Thrag và quý tộc (có **Aurel Vey**) hành hình The Ascended. Máu chảy xuống **hồ Nova** → ai uống thành ma cà rồng. Đàn chim trắng bay lên lúc Ngài tắt thở. Darwin "trừng phạt" Thrag, được tôn làm vua; vương quốc đổi tên **Vael**. **Luke** và **Emma** biết sự thật, mang **mắt trái** và **tay trái** của xác thánh trốn đi.
- 1–476: Children of Light thờ xác thánh trong hầm mộ (sau là Catacombs of Naming), dần cuồng tín thành **Soul Requiem**. Thế kỷ đầu: hoàng gia thu những gì còn lại của xác về **Royal Reliquary** (kho thánh tích hoàng gia); **Darwin hợp nhất trái tim** → bất tử, không cần hút máu; mỗi 100 năm nâng một hoàng tộc lên (**the Haloed**, nay 11 người). Nhà Vey uống hồ Nova (nay giấu trong hầm mộ hoàng gia, gọi **the Faint Lake**).
- ~476 **Hierophant Absalom** (dòng Emma, tay trái thánh ghép trên vai), qua giáo đồ cài trong hoàng tộc, trộm từ Royal Reliquary **cột sống, đôi tai, mắt phải**. Darwin biết nhưng **cố ý để mất** (giáo phái sẽ làm việc nguy hiểm và gom xác hộ hắn). Absalom lấy cột sống làm khung, đắp thịt bằng Ether đỏ. **676**: thiếu tim + hai mắt → **Specimen Zero** (xác mù, rỗng, săn bằng rung chấn; bị hạ thì 100 năm sau trở lại — trừ khi cột sống bị rút ra). Đôi tai về sau ghép cho ca trưởng the Choir; mắt phải cất trong Catacombs để ghép cho Candidate.
- 724 **Malric** (hậu duệ Luke) tìm thấy mắt trái. 745 đọc nhật ký Luke. 757 bắt đầu thí nghiệm dưới lòng đất (Undercity). 763 gặp Absalom. 766 hợp tác, hiến tế người cho Specimen Zero. 769 vỏ Plague Eye. 770 Specimen Zero thức sớm, bị ru ngủ lại. 771 **Plague Eye** sống, tiết Ether xanh lá (chuột đột biến, ếch khổng lồ, người hoá goblin; vài con giữ trí người).
- **776**: Specimen Zero thức hoàn toàn, tàn sát (**the Drowning**). Malric dẫn năng lượng nó phong ấn Plague Eye (hệ thống bơm + ống — chính là câu đố ống Map 2). Mùa đông Malric chết. Absalom thoát.
- **780**: hoàng gia phái **Isolde Vey** quản Ether xanh lá (bản Malric tinh chế), truy nã Absalom. Ether ban cho dân như **the Gift** → lễ Awakening.
- 780–1176: goblin, quái biến dị thành chuyện thường. Soul Requiem thành tà giáo ở vùng mộ địa hoàng gia, lập quân **Black Lantern** và hàng giáo sĩ **the Choir**. Specimen Zero trở lại 876, 976, 1076. Giáo phái bắt trẻ trước Awakening nuôi "rỗng" để lấy Con Mắt: **Candidate I–VI đều chết**.
- ~1156 **Candidate VII** ra đời, thất lạc, được **nhà Corwen** nuôi → **Ren**.
- **1176**: kỳ trở lại thứ năm của Specimen Zero. Đất rung khắp Greystone. Game bắt đầu.

### 6.3 Xác thánh — ai giữ phần nào (xương sống của game: "ai cũng giữ một phần của Ngài và gọi đó là chính nghĩa")
| Phần | Người giữ 1176 | Lý do họ nói | Map |
|---|---|---|---|
| Máu (hồ Nova) — *không phải bộ phận rời* | Đại gia tộc, nhà Vey, Isolde | "Chúng ta xứng đáng đứng trên" | 3 |
| Mắt trái | Luke → Malric → **Plague Eye** → Ren | "Mọi người phải bình đẳng" | 2 (rơi khi hạ Plague Eye) |
| Cột sống | **Specimen Zero** (khung do Absalom dựng) | không còn ý chí | 2 (tuỳ chọn; rút ra = Specimen Zero chết hẳn) |
| Đôi tai | **Precentor Morrow** (ghép từ Absalom) | "Ta nghe được Ngài" | 3 (nghe Con Mắt qua 3 chuông) |
| Mắt phải | **Candidate VI** (giáo phái ghép vào vỏ rỗng để "thấy" Con Mắt) | không còn gì | 4 (tha thì nó tự trao: *"It was never mine."*) |
| Tay trái | Emma → **Absalom** (ghép trên vai, giữ hắn sống) | "Ta là bàn tay sẽ nâng Ngài dậy" | 4 (lộ diện), 5 (boss) |
| **Tim** | **Darwin** | "Chỉ ta đủ sức giữ trật tự" | 5 |

Chốt 16/11 (lấy cảm hứng thánh tích; tên trong game **the Ascended's Remains**, từng món *Relic of the Ascended* — không dùng tên/hình theo Steel Ball Run): **6 bộ phận + máu**. Đã BỎ chân và tay phải. Chiêu "rễ" và "bàn tay khổng lồ" của Specimen Zero là thịt Vael đắp quanh cột sống, không phải tay thánh. Specimen Zero không còn mắt → chiêu tia mắt (`eyefx_*`) cần đổi hình (tia từ đốt sống) hoặc chuyển sang Candidate VI — **chưa làm**.
**Mỗi boss mang bộ phận thánh rơi ra bộ phận đó**, Ren nhận bộ Mantra tương ứng. **The Severing (Map 4) xé MỌI bộ phận Ren đang mang** (mắt trái, cột sống, đôi tai, mắt phải) → về tay Absalom; Ren chỉ còn **Mantras of the Name** (phải là bộ mạnh nhất). Bỏ qua Specimen Zero thì cột sống vẫn trong thân nó, giáo phái khiêng đi — kết quả như nhau.
Đề xuất cơ chế chung **Grace** (chưa làm): boss mang bộ phận thánh, khi thấp máu bộ phận loé xanh dương, hồi máu / khiên MỘT lần ("lòng trắc ẩn của Ngài che chở cả kẻ trộm xác Ngài"); Ren (Nameless) phá được. Cấu hình bằng thuộc tính Tiled.

**Mục đích của Darwin — kẻ gom xác thật sự.** Darwin muốn **đủ xác thánh để trở thành chúa**: ghép đủ bộ phận rồi tự nhập vào thân xác cùng trái tim (pha 2 *The Remade God*), "vì thần không cần ai tha thứ". Từ năm 1 hắn thiếu mắt trái + tay trái (Luke, Emma mang đi); năm 476 để giáo phái lấy thêm ba phần. Hắn không tự ra tay mà **gom bằng tay người khác**: viết lời tiên tri để giáo phái săn Nameless (chỉ Nameless lấy được Plague Eye); để Soul Requiem sống và dựng thân xác hộ; treo cho Isolde lời hứa "nâng" để bà săn Con Mắt; chờ mọi phần dồn về một chỗ rồi ra tay ở The Forge. Ren, không hề biết, là người khuân xác cho hắn suốt Map 2–4.
- Manh mối không spoil Map 1–3: lệnh hoàng gia chỉ đòi "the Eye, unharmed"; thư Isolde nhắc "the King's collection"; ở King's Barrow / Chapel có hộp thánh tích hoàng gia **rỗng**, nhãn khắc tên bộ phận.
- Lộ ở Map 4: sổ kiểm kê Royal Reliquary — mỗi ô trống có bút tích Darwin *"Let them carry it."*; bản gốc lời tiên tri có dấu ấn hoàng gia.

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
- **Precentor Morrow**: ca trưởng the Choir, mang **đôi tai thánh** (nghe Con Mắt qua 3 chuông). Chỉ huy cuộc săn Ren ở Map 3, muốn bắt Ren sống. Hạ hắn → rơi đôi tai.
- **Candidate VI**: vỏ rỗng hứng Con Mắt ở Map 4, mang **mắt phải** thánh; dùng chính chiêu của Ren. Tha → nó tự trao mắt phải (`candidate_vi_freed`); giết → rơi mắt phải.
- **The Registrar**: người giữ Sổ Tên của the Choir (hình Megapack **GameMaster** — KHÔNG còn là Absalom, đổi 17/11 §296); miniboss giữ Ấn 1 ở Scriptorium of Names, Map 4. Absalom cần thiết kế riêng sau.
- **Absalom**: lãnh đạo Soul Requiem hơn 700 năm, thật lòng yêu vị thần; giấc mộng hắn tin là lời Ngài thật ra đến từ dịch Vael.
- **Darwin**: kẻ phản bội, vua bất tử mang tim thánh, **kẻ gom xác thật sự**: muốn thu đủ xác thánh để tự nhập vào và trở thành chúa, vì "thần không cần ai tha thứ". Luôn để người khác làm thay (giáo phái, Isolde, Ren).
- Chỉ hiện qua ghi chép: Arthur, Borrow, Nolan, Lucas, Thrag (bia mộ, biên niên Map 3–4); Luke, Emma (nhật ký Luke Map 2); Aurel Vey (gia phả Map 3); Malric (nhật ký Map 2); Odo, Brakk (nạn nhân Undercity).

---

## 7. MẠCH TRUYỆN 5 MAP

| Map | Kẻ giữ | Ren muốn | Sự thật lộ ra | Ren rời map với |
|---|---|---|---|---|
| 1 Greystone Plains | nạn nhân the Gift | giúp một làng | goblin từng là người | cú rơi xuống lòng đất |
| 2 Undercity of Vael | Plague Eye (mắt trái), Specimen Zero (cột sống) | thoát ra | muốn thoát phải phá phong ấn Malric chết để dựng | Con Mắt trong người, và một lỗi lớn |
| 3 Vale of Graves | nhà Vey (máu), Morrow (tai) | gỡ Con Mắt | ai cũng muốn Con Mắt, không ai muốn cứu Ren | bị ném sống tới nơi làm lễ |
| 4 Catacombs of Naming | Candidate VI (mắt phải), Absalom (tay) | sống sót | Ren là Candidate VII; giáo phái là công cụ của vua; Darwin là kẻ phản bội | mất mọi bộ phận thánh, có lại tên, mất Pell |
| 5 The Faint Lake | Darwin (tim) | chặn lại, rồi trả lại | không thể hồi sinh lòng trắc ẩn bằng cách ghép xác | một trong ba kết |

**Map 1** (đã có): Ren qua Millbrook (làng Unawakened). Mỗi kỳ trăng mới lính thu thuế của Grok xuống lấy thóc; thiếu 3 bao → đốt trại nhà Harlan. Todd nhờ diệt Grok. Trong trại goblin: truyền miệng "trước nước xanh họ là người"; thóc ném xuống hố Stone Ring dâng "the Eye Below". Đất rung từng đợt. Chuỗi nhân quả: ca viên Soul Requiem ("the hooded one") xúi goblin đào Stone Ring (nắp ấn nhà Vey 780) → Ether rỉ → địa chấn → vòng đá sập khi Ren hạ Grok → rơi xuống Map 2.
**Map 2** (đã có): tiếp đất trong lab Malric. Lối ra duy nhất là ống thoát trên hố Plague Eye, không chạm được khi bể đầy. Gặp **Pell** (giữ tay quay, cảnh báo bơm cạn = phá ấn). Lời Malric ở Pump Control: *"Do not turn it. I am asking you as a dead man."* Ren vẫn quay. Hạ Plague Eye → rơi **mắt trái**, Con Mắt bám vào Ren (Nameless) → bộ **Mantras of the Eye** → Ether trào lên mặt đất. Sau lưng, giáo phái xuống lấy Specimen Zero và bắt Pell. Tuỳ chọn: Specimen Zero (Containment Vault) → rơi **cột sống** (bộ Mantras of the Vessel) + Moonlit Edge; Specimen Zero chết hẳn.
**Map 3** (đã có): Ether tràn thung lũng, xác đứng dậy — lỗi của Ren. Soul Requiem phong toả, 3 chuông cộng hưởng Con Mắt. House Vey: Isolde muốn mổ Con Mắt khỏi xác Ren; tha hoặc giết. Sinking Fields → King's Barrow (Map 3b, mộ Borrow, Crimson Oath). Chapel of Naming. Hạ Morrow → rơi **đôi tai** (bộ **Mantras of the Ear**: Afterecho / Discord / Forewarning, xanh băng) → hấp hối mở Requiem Door → Ren bị kéo đi (scene hiện tự rời map qua exit `to_map4`).
**Map 4** (ĐANG LÀM 17/11 — bản đồ xong, chưa có cơ chế/quái/scene; xem mục 10 "MAP 4"): Ren tỉnh trong ngục, Mantra bị phong; cứu Pell, Pell dẫn đường → thấy Black Lantern khiêng Specimen Zero → sổ gốc: Ren là Candidate VII, thư mẹ xin con về → biên niên ghi tên kẻ phản bội bị xé trong nhật ký Luke: **Darwin** → bản gốc lời tiên tri có dấu ấn hoàng gia → sổ kiểm kê Royal Reliquary (*"Let them carry it."*) → Hall of Naming: Absalom xuất hiện lần đầu, Candidate VI (mang **mắt phải**) dùng chính chiêu của Ren → **The Severing**: MỌI bộ phận thánh bị xé khỏi Ren, Pell chết che chắn, Ren nhận **Mantras of the Name** → Absalom mang tất cả đi: *"Only the heart remains. The King will bring it himself."* → lối ra mở sẵn nhưng Ren đi xuống theo. **Các cutscene sự thật (tranh Angel Bird `Resources/Story/angel_bird_1..4`, nhật ký Luke) để dành cho Map 4.**
**Map 5** (CHƯA LÀM): the Faint Lake trong lòng núi lửa (gói Volcano). The Pilgrim Stair (giáo phái vs lính hoàng gia) → Crypt of the Haloed (tha Isolde thì bà mở lối tắt) → The Forge (Wall of Voices; hạ Absalom; Darwin giật tay Absalom, mang thân xác xuống hồ) → trận cuối 3 pha: **The Haloed King** (kiếm sĩ) → **The Remade God** (Darwin nhập vào thân xác đủ bộ phận — mục đích cả đời hắn; dùng lại chiêu của từng bộ phận: mắt trái / Plague Eye, cột sống / Specimen Zero, đôi tai / Morrow, mắt phải / Candidate VI, tay trái / Absalom; máu / Isolde) → **The Return** (phá 3 neo, nói tên thật để lấy tim; tha Absalom thì hắn giữ một neo).

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

**BossRelic `relic_sprite`** (16/11, §244): hình cổ vật rơi từ boss — `eye` (mặc định) hoặc đường dẫn Resources; `relic_beat` = đập như tim. Specimen Zero dùng `VFX/specimen/relic_spine` (cột sống, `relic_beat=false`) — KHÔNG được rơi con mắt. `relic_size` = cạnh dài nhất (đv). Bộ thánh tích vẽ sẵn ở `Resources/VFX/relics/` (eye_left — Plague Eye đang dùng, eye_right, ears, arm_left, heart); nguồn `_tools/lore_1116/relic_dry.py` (`python3 relic_dry.py v3` = tờ xem trước; kiểu khô quắt như gỗ mục, không dây thần kinh, không rễ, không vân màu, hào quang xanh dương; tay trái nằm ngang, cong ở khuỷu, lộ xương — người dùng chốt).

**Mantras of the Ear** (16/11, §252): Afterecho / Discord / Forewarning (setId `ear`), rơi từ Morrow (cờ `relic_ear`). Hook `PlayerCombat.MeleeSwung`, `EnemyAI.Retarget`. Công cụ xem map: `_tools/lore_1116/render_tmx.py`.

**Lời NPC theo cờ** (16/11, §260): villager `lines_N_when` / `lines_N_unless` / `lines_N` (N 1..6, N lớn nhất đủ điều kiện thắng). Pell (Map 2) dùng lines_1..3.

**Pickup `unless`**: pickup có `unless` → nếu cờ có thì coi như đã nhặt (dùng cho tonic `mercy_1`).

**Hệ thống thêm chiều 16/11 (NOTES §261–§275)**
- `stillness` (Hazards/StillnessMechanism): vùng cảm biến — Ren ĐANG BƯỚC trong vùng -> phát xung `fires` cho bẫy `trigger` (đứng yên = im). Dùng ở The Still Walk (1b).
- Launcher: tên bắn ra bỏ qua tường chứa miệng bắn (trước đó bẫy "hỏng"); `art` = hậu tố hình hốc bắn (`mine` = hốc đá nâu); tên bắn dọc cắm dọc (Projectile.keepRotationOnEnd).
- Điểm sinh `stand_in` = true: khi khu chưa đánh, director đặt "diễn viên" (hình + Idle, không AI) đúng chỗ -> thấy boss từ xa (Grok ở Stone Ring). Trận bắt đầu thì thay bằng quái thật.
- Quest JSON `"kind": "side"` -> bảng ghi SIDE QUEST (map bí mật, bước viết theo manh mối dân làng / ghi chú). Khu map bí mật `fog = off` (không mây che minimap).
- Đọc bảng cơ quan (ghi chú / khoá ký tự…): `GameInput.Reading` -> Ren đứng yên, quay mặt về vật đang đọc, chỉ T / Esc / M; đồ nhận được (key_got) in ở chân bảng, không hiện dòng chữ dưới màn hình.
- Nền bảng UI `panel_stone` / `panel_small` vẽ lại (nền mực đen phẳng, viền thép + chỉ vàng) bằng `_tools/lore_1116/panel_forge.py`. KHÔNG dùng lại nền đá tím loang.
- Bỏ các thông báo thừa (chỉ tả lại điều đã thấy / trùng bảng tên boss) — danh sách §269. Giữ thông báo dạy cơ chế.
- Iaijutsu (Oathbreaker) = ĐÒN GIỮ: giữ J / chuột trái -> nhát 1 dừng ở khung vung kiếm, thanh nộ cạnh người (IaiStance); giữ ≥ 0,5 s = +60% + choáng 0,8 s; tăng dần tới +100% khi đầy ở 1,5 s; dưới 0,5 s = nhát thường.
- Hiệu ứng nội tại lên quái (EnemyStatus + WeaponTrait.status…): Bleed / Burn / Bind / Chill / Mark; thước nội tại trên HUD dưới thanh máu. Bảng ở SOUL_REQUIEM_KATANAS.md.
- Hình kiếm nằm đất: `_tools/lore_1116/katana_ground.py <key> <out> sheathed` (kiếm TRONG VỎ, 64x14) — quy ước cho mọi kiếm nhặt.
- Map 1b sinh bằng script: `hollow_tiles.py` (tileset SR-mine) + `hollow_map.py` (map) — sửa 1b thì sửa script rồi chạy lại, bake lại minimap (§266).

**Cảnh đã đặt**: Map 1 `prologue`, `tremor_caves`, `tremor_ruins`, `ring_look`, `give_tonic`, NPC `goblin_child` · Map 2 NPC Pell, `pell_meet`, `pell_page` (mercy_2), `hub_grate`, `pump_seal`, pickup `malric_page` · Map 3 NPC Aldous ×2, `outfall`, `commons_bell`, `aldous_meet`, `aldous_field`, `morrow_door` (→ exit `to_map4`).

---

## 10. TRẠNG THÁI & VIỆC CÒN LẠI (cập nhật 17/11 17:10)

### MAP 4 — The Catacombs of Naming (việc ĐANG LÀM — đọc kỹ khối này trước khi tiếp tục)
**Người dùng đã chốt (17/11):** cấu trúc Hub + 2 cánh; mở đầu Mantra bị phong + mất katana (lấy lại ở phòng lính gác); Pell xuất hiện ở điểm cố định;
đợt này làm Map + bẫy + cốt truyện, **boss Candidate VI + The Severing để đợt sau**. Phong cách bám sát mockup 2 gói (rafaelmatos.itch.io Crypt + Old Prison):
47 ảnh mẫu đã tải về `_tools/map4_1117/web/` (bảng xem `sheet_00..11.png`; cr_* = Crypt, op_* = Old Prison) — luôn đặt ảnh map cạnh ảnh mẫu khi duyệt.
- Ren thoát xà lim nhờ **RUNG CHẤN** (đoàn Black Lantern khiêng Specimen Zero đi ngang phía trên, cổng gỉ trật ray kẹt nửa chừng).
- Xà lim Pell: tời bánh răng đứng trên SÀN trước xà lim, **quấn xích** — phải có katana chém đứt xích mới quay được.
- GameMaster (Megapack) = **The Registrar** (miniboss giữ Ấn 1, Scriptorium) — KHÔNG còn là Absalom (Absalom cần thiết kế hình riêng sau).
- Cổng 2 ấn = cửa boss (khung 4x3, 128x96, lùi 16 px để tâm = lối 3 ô) trên mặt gạch bắc Ossuary (sau bệ thờ, 2 ổ ấn trên bệ).
- Quái: BỎ Undead / Bat / Skeleton 1 / Mage Skeleton. Dùng Tiny Swords Enemy Pack: **Bone Jailer = Skull** (+ `Extra/Skull Guard` đỡ khiên), **Choir Cutthroat = Thief**,
  **Pit Bat = Giant Bat** (PPU 200 như Map 1, không có hoạt ảnh chết -> VFX chết sẵn có); Megapack **Lantern Warden = Knight_LVL4** (chỉ Idle/Move/Dash/Dashend; lửa tím -> ĐỎ),
  **Hollow Cantor = Sorcerer_LVL4** (Dash = dịch chuyển, phép = VFX); Black Lantern (Map 3) đi đoàn khiêng Specimen Zero (chỉ diễn). Lantern Knight giữ Ấn 2 (Reliquary).
  Phân bố: Oubliette + nửa đầu Cell Rows không quái; cầm katana -> 3 Bone Jailer ập vào; Blood Pit: Pit Bat + Cutthroat; Ossuary an toàn (điểm nghỉ);
  Scriptorium: Cantor + Cutthroat + The Registrar; Reliquary: Warden + Bone Jailer + Lantern Knight; Penitent's Walk chỉ bẫy; Hall: boss đợt sau.

**File & trạng thái**
- `.tmx`: `Assets/GamePackage/EPIC RPG World Pack - Crypt V.1.6/TiledMap Editor/Maps/map4_catacombs_of_naming.tmx` (+ .meta sortingMode 1), 114x124 ô, 980 object,
  md5 **`dde60fc1e5b0aeaf46bb70356768a23a`** (§304). Ảnh cửa/cơ quan: `<Crypt>/Props/SR-m4-art/` + tileset `<Crypt>/TiledMap Editor/Tilesets/SR-m4-art.tsx`.
  Đã có: địa hình 2 gói, cửa/cổng (hình), trang trí, đèn (`mechanics` type light), khối chặn địa hình (`blockers`), biên map, khối chặn đồ vật.
  ĐÃ CÓ (§304, phần 1): 14 khu, player_start, 6 điểm nghỉ, 11 cổng logic + hình có hoạt ảnh (`Resources/VFX/m4_*`), tời/xích/chìa/katana/2 ấn + ổ ấn,
  cảnh nối TẠM (m4_wake, oub_bar, pell_skull_stub, walk_top), lối ra to_map5 — nguồn: `_tools/map4_1117/logic.py`, chuỗi cờ ghi ở đầu file.
  CHƯA có: spawns / bẫy / story thật / quest / minimap / scene riêng. **Chưa có phản hồi người dùng về file trong Tiled/Unity** (đã nhờ: mở Tiled xem đủ tileset; Ctrl+R xem Console).
- Map SINH BẰNG SCRIPT. Người dùng sửa tay trong Tiled -> phải ghi lại (so md5 trước khi sinh lại; chưa có cơ chế USER_POS cho Map 4 — nếu người dùng sửa thì thêm như 2b).

**Bố cục (toạ độ ô; ngục ở NAM, hầm mộ ở BẮC -> mọi cửa khoá trên MẶT TƯỜNG BẮC, đi lên để vào)**
Oubliette (x100..111 y109..119; xà lim Ren x105..111 y109..112, cổng trong vòm x105..110 hàng 113..115) -> cửa gỗ (vòm x101..103) -> hành lang Cell Rows (y101..105;
5 xà lim x76/83/90/97/104 y88..100, cổng trong vòm 6 ô; x83/x97 cổng mở = chỗ núp; x90 = Pell; tời Pell x95 y101) -> phòng tra tấn x77..86 y109..119 (vòm mở; móc chìa `key_hook`)
/ phòng gác x88..98 y109..119 (cửa gỗ khoá, chân dung vua, katana) -> Rolling Hall x69..74 y79..117 (miệng hầm tối đầu bắc, vực cuối nam) -> sân lưỡi xoay x34..64 y83..102
(hố gai sàn, ray lưỡi xoay) -> cầu treo DỌC x53..56 y79..82 (tời x56.6 y82) -> sân trước cổng x45..67 y67..70 + đường đá hẹp x52..60 y71..78 giữa hố xương ->
CỔNG ĐẦU LÂU x55..57 trong tường chắn y64..66 (Pell mở) -> lối x54..58 -> **Ossuary** x38..73 y48..61 (hub, điểm nghỉ; bệ thờ x49..63, bậc x54..58, cổng 2 ấn x55..57 mặt gạch 45..47)
-> cánh tây **Scriptorium** x4..33 y48..60 (bệ tây bắc: Sổ Tên + `seal1_pedestal`; cửa gỗ x18..21 -> phòng thư của mẹ x10..27 y26..39, `mother_letters`)
/ cánh đông **Reliquary** x78..107 y48..60 (bệ đông bắc: `seal2_pedestal`; 12 bệ thánh tích rỗng + tấm áp lực; cửa sắt x91..94 -> kho kiểm kê x84..101 y26..39, `inventory_book`;
sàn sập trước cửa kho) -> qua cổng 2 ấn -> **Penitent's Walk** x48..63 y26..42 (bệ cao: lửa/lỗ bắn trên mặt bệ hàng 32..34; cưa hàng 36/39; gai 41..42) -> cửa sắt x54..57 ->
**Hall of Naming** x40..73 y4..20 (4 trụ wall-3, nền xương, bệ giữa có viền) -> cửa chuyển màn (metal frame 5) x55..58 mặt gạch 1..3 -> Map 5.
Danh sách vùng bẫy dự kiến (chỉ để xem trước) = `TRAPS` trong `_tools/map4_1117/decor2.py`; cửa = `DOORS` trong `gen_frame2.py` + xà lim tự sinh.

**Công cụ `_tools/map4_1117/` (chạy trong device_bash, `cd` vào thư mục này)**
- `ck.py` (Crypt: vách wall-1 + luật tác giả, bệ wall-2 + lan can luật 2 lớp, trụ wall-3 `pillar`, hốc `niche`, khuôn từ Sample Map `stamp`, `passage`, `band` căn tâm, nền xương đảo mặt nạ)
  · `opk.py` (Old Prison: sàn bệ, tường dày, tường mỏng xà lim `cell/hwall(arch6=, hole=, door=)`, vũng máu, hố gai `spikes`) · `gen_frame2.py` (KHUNG v2 — nguồn sự thật bố cục)
  · `doors.py` (vị trí khớp tường + anim cổng) · `decor2.py` (trang trí) · **`logic.py`** (khu, cổng, cơ quan, cờ — §304) · `gate_strips.py` (dải hoạt ảnh cổng) · `logic_view.py` · `m4lib.py` / `m4_setup.py` (tileset + object) · `preview3.py` (ảnh xem trước `ref/v3_full.png`)
  · **`check.py`** (đi thử 10 giai đoạn mở cổng + chân cửa trên sàn) · **`audit.py`** (kiểm chân MỌI đồ vật theo loại ô) · **`write_real.py`** (ghi .tmx thật; tự chặn nếu audit còn lỗi)
  · sau `write_real.py` chạy `python3 ~/mnt/Catalog/v2/trim_props.py "<tmx>"` rồi ghi md5 vào mục 3 + NOTES.
- Bản xem trước ghi vào bản sao **`/tmp/gp/`** (thư mục tạm trên máy, mất khi khởi động lại): thư mục con là symlink tới `Assets/GamePackage/*`, riêng
  `/tmp/gp/EPIC RPG World Pack - Crypt V.1.6/TiledMap Editor/Maps/` và `/tmp/gp/EPIC RPG World Pack - Old Prison V1.7.1/TiledMap Editor New/` là thư mục THẬT (con là symlink)
  để file thử không rơi vào Assets. Mất thì dựng lại như vậy trước khi chạy preview (layers_view có assert chặn ghi vào máy người dùng).
- Bỏ, chỉ để tham khảo: `gen_frame.py`, `decor_prison.py`, `decor_crypt.py`, `preview.py`, `preview2.py` (bố cục v1 bị chê).

**QUY TẮC THIẾT KẾ đã rút ra (Map 4, NOTES §298 / §300) — áp cho mọi map dựng bằng script**
- Collider (§303): vật CHÂN TRÒN (thùng, xô, bình, chân nến, giá đuốc, bao tải, cây, đống đá, cột tròn/bát giác) = `<ellipse/>`;
  vật đáy vuông hoặc sát vách cần chặn kín phía sau = chữ nhật. Vật sinh trong code: `collider_shape` round -> CapsuleCollider2D.
- Old Prison cửa gỗ ngang: vòm 4 ô (cột tileset 40..43), 2 ô giữa đi được, cánh tâm x+2 (§303).
- Cửa: Crypt `door frame 1-5` (4x3) / `Boss door` (4x3 — dải 30 khung 128x96, §303) chỉ trên MẶT GẠCH BẮC; `doors2` chỉ trong TƯỜNG MỎNG giữa 2 phòng sát nhau; Old Prison cổng xà lim chỉ trong
  ĐOẠN VÒM 6 ô (tường liền, dầm + bánh răng nằm trên đỉnh tường), cửa gỗ trong vòm 3 ô của tường phòng, cổng đầu lâu trong tường chắn chạy hết bề ngang.
  Bậc "Dungeon exit to Ancient Ruins" = đi LÊN ra mặt đất (không dùng cho đi xuống); đi xuống = hầm bậc chìm trong sàn (Terrain hàng 32..34) hoặc cửa chuyển màn.
- Bẫy gắn vào kiến trúc thật: lỗ bắn / tượng mặt trên MẶT BỆ hoặc mặt gạch, cưa chạy rãnh sàn, gỗ lăn ra từ HỐC TỐI, lưỡi xoay trên ray sàn. Vải "tissue" là vải phủ mộ, không treo tường.
- Bệ `plat(y,h)`: mặt bệ y..y+h-2, LAN CAN hàng y+h-1, mặt gạch 3 hàng tiếp; bậc `stair(x, y+h)`; bệ có đặt đồ: h ≥ 5. Thảm / viền căn theo TÂM (`band`), tâm bậc = x+2.5.
- Trụ = wall-3 (`pillar`), không dùng wall-1. Wangset 'ground1 to dirt' màu 1 là ground1 (phải đảo). Ô chặn / mặt tường LẤY TỪ LỚP ĐÃ DỰNG, không đoán.
- Quy trình bắt buộc trước khi gửi ảnh: `check.py` ĐẠT -> `audit.py` = 0 lỗi -> xem từng khu tỉ lệ thật CẠNH ảnh mẫu (`ref/cmp2_*.png`). Không gửi ảnh thu nhỏ toàn map làm bằng chứng.

**BƯỚC 3 — việc tiếp theo (người dùng đã bảo "tiếp tục"; làm theo thứ tự, báo cáo + checklist test sau mỗi phần)**
1. ✅ XONG §304 (chờ người dùng test) — Logic trong .tmx (sửa `decor2.py`/`write_real.py`, sinh lại, check + audit): zones (fog/ambient từng khu), player_start (trong xà lim Ren), checkpoints (Ossuary + trước mỗi trận),
   cổng logic (`gate` + `gate_visual` có `anim`: dùng dải `Resources/VFX/gate_cell1 / gate_prison / gate_wood_h / gate_boss / gate_metal_h` — chú ý ảnh SR-traps là bản đã cắt; GateAnimator khớp mép trên-trái),
   tời (`lever` sprite trap_gear, `fires`/`sets`), chìa (`pickup` key), cổng 2 ấn (`unlock_when` 2 cờ ấn), exit sang Map 5, exit/arrive từ Map 3 (`to_map4` của Map 3 phải trỏ tới scene mới).
2. Bẫy bằng cơ chế có sẵn: `roller` (Rolling Hall, dir down), `blade` kind blades (sân lưỡi xoay), `spikes` (hố gai, Walk), `blade` kind saw (Walk), `launcher` arrow/fire/ice (Reliquary/Walk/Scriptorium),
   `plate` (Reliquary). `stop_when` cờ qua khu. CODE MỚI: cầu treo hạ (dải `hanging bridge-lifting/dropping-vertical`, mở va chạm khi hạ), sàn sập (`pit trap` dải Crypt), ổ đặt ấn, rung chấn mở cổng Ren.
3. Cốt truyện (StoryScenes `type="story"`, chữ tiếng Anh, không spoil sai chỗ): tỉnh dậy + rung chấn; Pell (gặp, chém xích, đi theo tới cổng đầu lâu, mở cổng); đoàn Black Lantern khiêng Specimen Zero
   trên gờ xa x0..29 y68..73; ghi chép: sổ gốc Candidate VII (Scriptorium), thư mẹ (`parents_heard`), sổ tên (`name_reclaimed`), biên niên Luke có tên **Darwin**, lời tiên tri gốc có dấu ấn hoàng gia,
   sổ kiểm kê *"Let them carry it."*; tranh `Resources/Story/angel_bird_1..4` dùng ở Map 4. Quest `Resources/Quests/Map4_CatacombsOfNaming.json`. Minimap bake.
4. Quái: `Map4EnemyBuilder` (Tiny Swords + Megapack như trên; The Registrar dùng Idle/ATK/Upgrade), spawns theo khu. Scene: menu **Soul Requiem ▸ Scenes ▸ Build Map 4 Scene** (viết mới;
   KHÔNG dùng `BuildMap4()` cũ, không thêm vào Build All khi chưa test).
5. Đợt sau: boss Candidate VI + The Severing (Hall of Naming), khu bí mật Namesake.
Người dùng tự test trong Unity (Claude không điều khiển Unity) — gửi checklist.

**Soát khi mở tài khoản mới (17/11 13:45, §295)**
- Biên dịch: lần build cuối trong Editor.log THÀNH CÔNG (gồm code âm thanh §294); 2 lỗi cũ (PlayerMelee.Release, RollerMechanism.GetBool) đã hết. Người dùng đã dựng lại các scene và vào Play sau đó; log không có lỗi game (chỉ nhiễu gói Unity AI Assistant "NoSubscription").
- Người dùng đã sửa tay Map 2 và Map 2b trong Tiled sau phiên trước -> md5 mới ở mục 3. **Map 2b sinh bằng script**: chỉnh tay phải ghi vào `_tools/kroven_2b/tmxio.py` `USER_POS` (đã ghi 6 đuốc + 6 đèn), nếu không `gen_map.py` sẽ xoá mất. Trước khi chạy `gen_map.py` luôn so md5 file hiện tại với md5 ở mục 3; khác -> so từng object với bản sinh ra thư mục tạm (§295 có cách làm).
- Tài khoản mới: MCP PixelLab có sẵn. **ElevenLabs MCP CHƯA kết nối** ở tài khoản này -> việc âm thanh §294 (chạy nốt node, tải file) cần người dùng thêm lại connector ElevenLabs + mở quyền tên miền `elevenlabsusercontent.com`.
- Chưa có phản hồi test của người dùng cho §284–§294 (2b, The Last Guard, hồi sinh tự động, âm thanh).


**Đã làm 16/11 tối – 17/11 sáng (NOTES §276–§286) — TẤT CẢ CHƯA BIÊN DỊCH / CHƯA CHƠI THỬ từ §284 trở đi**
- §277–§280: biểu tượng trạng thái, ô nội tại HUD (người dùng test OK §280).
- **Map 2b The Kroven Deep** (Ironcrown) — dựng §281, làm lại §282–§286. Sinh toàn bộ bằng `_tools/kroven_2b/` (`grid.py` sơ đồ phòng, `objects.py` mọi object, `gen_tiles.py` vách theo
  bảng học từ Map 2 `walls_table.json`, `gen_water.py` (kênh nước, hiện TẮT: WET rỗng), `gen_map.py` ghi .tmx, `tmxio.py`). Sửa map = sửa script rồi `python3 gen_map.py`, render kiểm bằng
  `_tools/lore_1116/render_tmx.py <tmx> x y w h out.png [scale]` (SR_CLEAN=1 để bake minimap: ảnh /8 BOX -> `Resources/Minimap/Map2b_KrovenDeep.png`).
  Đường đi: Landing (cầu thang đá lên, vòm trong vách bắc x5..7) -> C1 -> **Gatehouse** (rèm tên vách bắc hàng giữa y29: cột chẵn theo nhịp, cột lẻ bắn loạt khi giẫm phiến đá khắc
  vương miện `trap_plate_kroven`) -> C2 -> **Rolling Hall** (R2 22..44 x 17..21, khúc gỗ lăn ĐÔNG->TÂY 2,4 s, 2 hốc núp 3x3 ở x28..30 / x34..36) -> C3 -> **The Spiked Walk**
  (R3 32..44 x 5..9, 3 dải gai Crypt kiểu 1, khe 1 ô để chờ) -> C4 (điểm nghỉ ẩn door_rest) -> **The Guard's Post** (boss, cổng post_lock close_on_enter) -> cổng rest_gate ->
  **Nolan's Rest** (R5 10..19 x 16..21: quan tài đứng + giá sách dựa vách, Ironcrown trước quan tài, sách The Rampart Forms). Quest `Resources/Quests/Map2b_KrovenDeep.json`.
  Lối vào từ Map 2 (§286): **Records Vault** — cầu thang chìm xuống sàn + song sắt khoá vương miện (cipher `crown_lock`, 3 huy hiệu trong ghi chép Malric ở Archive) x16.9..18.9 y6..8;
  mở khoá -> song sắt ẩn (toggle `kroven_grate_open`) -> exit `to_kroven`. Lối lên 2b về Records Vault (17.9, 8.55). Nắp hầm Forgotten Store ĐÃ BỎ.
  Luật bẫy mới: bẫy cơ khí trúng cả khi lăn (`TrapArt.HurtPlayerCrush`). Tileset riêng 2b xếp sau tileset cuối của Map 2 (gen_map tự tính).
- **Boss The Last Guard v3** (`LastGuardSkills.cs`, `Map2bBuilder.cs` dựng 2 controller `Last_Guard` + `Last_Guard_LS`): khiên chỉ chặn khi đang thủ; pha 1 Thrust (hụt -> giáo cắm sàn 1,3 s),
  Shield Charge (đâm vách -> choáng 2,2 s), Bash, húc ra sau, Guard Break (chặn đủ 120 sát thương -> vỡ thế); pha 2 (≤50%) ném khiên, aura rêu, Whirl / Triple Thrust / Leaping Strike.
  Hoạt ảnh xuất bằng `_tools/kroven_2b/import_last_guard.py` (ô 192, pivot .1875, scale prefab 1.35). Dải sửa hướng (§290/§292) dựng bằng `build_charge_n.py` + `build_lg_fixes.py` (nguồn `lg4_src/`); importer cũ đã chặn ghi đè (`CLIP_OVERRIDE`, `FIXED`). VFX riêng `Resources/VFX/lg_*` (chuyển bằng `_tools/kroven_2b/make_vfx.py`).
- **Hồi sinh tự động mọi map** (§285): không còn vòng sáng / lời nhắc thắp lửa; vào khu tự ghi điểm + lưu; gục tỉnh ở điểm gần chỗ gục nhất (ải chưa xong vẫn về đầu ải).
  Lửa trại / nến thật vẫn cho nghỉ (hồi máu, bình). **Vòng vàng dưới đồ nhặt / ghi chép đã tắt toàn game** (`NoteMarker.ShowRing = false`, §286), còn đốm lấp lánh.
- Lớp vẽ: `.tmx.meta` phải `sortingMode: 1` (đã sửa cho 2b).

**Người dùng cần làm ngay khi mở phiên mới**: Ctrl+R trong Unity -> đọc lỗi biên dịch (Editor.log) nếu có -> **Build Map 2 Scene** -> **Build Map 2b Scene** -> chơi thử theo checklist:
1. Mọi map: không còn vòng vàng (điểm lưu, đồ nhặt, ghi chép); chết giữa một khu -> tỉnh gần chỗ chết.
2. Map 2: Forgotten Store không còn nắp hầm; Records Vault có cầu thang + song sắt; giải khoá vương miện -> song sắt biến mất -> xuống 2b; từ 2b lên về đúng Records Vault.
3. 2b: Ren đi trước / sau hài cốt, quan tài, giá sách không bị che sai; Gatehouse (hốc bắn hàng giữa, phiến đá lún + loạt tên); Rolling Hall (lăn đông->tây, núp hốc an toàn, tường hốc liền mạch);
   Spiked Walk (gai lần lượt, lăn không né); boss pha 1 (giáo kẹt, đâm vách, vỡ khiên) + pha 2 (khiên nằm sàn, 3 chiêu có báo trước) + VFX đúng lớp; nhặt Ironcrown + sách.

**Lịch sử phản hồi của người dùng về 2b (đừng lặp lại)**: thang sắt + cột sáng xanh xấu; tấm sàn trông như cần gạt; hốc tường 3x2 vẽ lỗi; gai lướt Q là qua; gai hồng xấu; boss quay khiên mãi,
hoạt ảnh lỗi, nhạt; đồ che Ren; máy bắn tên thấp; làn lăn dọc "ngược" (muốn lăn ngang từ cuối hành lang về đầu, có hốc núp); kênh nước + ống phun xấu; hồ + đống tên ở đầu đông thừa;
VFX boss cũ (slash_*, m3_*) xấu; vòng vàng điểm lưu / đồ nhặt thiếu chuyên nghiệp; nắp hầm ở Forgotten Store -> dời sang Records Vault.

**Đã xong trong ngày 16/11 (chi tiết NOTES §243–§275)**
- Lore v16/11 (6 bộ phận xác thánh + máu, Darwin là kẻ gom xác để thành chúa) — đã cập nhật mục 6–7 và file Word của người dùng.
- 9 katana chốt tên / lore / nội tại / màu; vẽ lại từ đầu bằng `katana_forge.py` (mỗi thanh dáng, hamon, tsuba, quấn chuôi, vỏ riêng) + vệt chém riêng. Nguồn sự thật: `SOUL_REQUIEM_KATANAS.md`.
  (Tên cũ Stillwater / Unbowed / Nightvow / Concord đã BỎ -> Oathbreaker / Ironcrown / Eventide / Arthur's Accord.)
- Bảng kiếm: DPS + mục SPECIAL; "THE TELLING" -> "ORIGIN". Mantras of the Ear: Afterecho / Discord / Forewarning.
- Soát chữ Map 1–3b theo lore mới; Pell đổi lời theo tình huống.
- **Map 1b Hermit's Hollow** (Oathbreaker) dựng xong, người dùng đang test: lối vào = đống đá sập ở miệng Old Copper Mine (chỉ Earthshatter phá); Collapsed Gallery (6 nhện, 2 đợt) -> Green Seam
  (vũng Ether độc) -> The Still Walk (bẫy tên chỉ bắn khi Ren bước; phiến đá khắc vòng tròn = chỗ đứng an toàn) -> Hermit's Cell (hài cốt Merek, Oathbreaker trong vỏ trước mặt).
  KHÔNG có lối tắt / cửa phụ (người dùng cấm, §265). Side quest 5 bước theo manh mối. Minimap có, không sương.
- Iaijutsu đòn giữ; hiệu ứng nội tại lên quái + thước HUD (§271–§275, CHƯA biên dịch / CHƯA test phần §275).
- Grok đứng sẵn ở Stone Ring (stand_in). Bỏ thông báo thừa. Đọc bảng = đứng yên. Nền bảng UI mới.

**Đang chờ người dùng test** (chưa có phản hồi): §273–§275 (thanh nộ Iaijutsu mới, +60%→+100%, hiệu ứng Bleed/Bind/Chill/Mark/Holy fire, ô nội tại HUD §279 thay thước — người dùng test OK §280), Grok stand_in, đọc bảng đứng yên.

**Việc tiếp theo (kế hoạch người dùng đã duyệt — khu bí mật cho katana, `SOUL_REQUIEM_KATANAS.md` bảng "khu bí mật")**
1. ĐÃ DỰNG + LÀM LẠI (§281–§286, chờ test) — **Map 2b The Kroven Deep** (Ironcrown) — xem đầu mục 10. Còn lại sau test: sửa theo phản hồi; cân bằng boss (máu 620, sát thương trong LastGuardSkills).
2. **Map 3b mở rộng + boss bí mật The Red Field** (hồn 600.000 người chết, Vael đánh thức) mở lăng Borrow -> Crimson Oath. Dời lối xuống 3b ra mép đầm Sinking Fields, khuất sau cây chết; bỏ gợi ý "ánh vàng".
   Viết lại side quest 3b theo manh mối (hiện vẫn chữ cũ, chỉ đổi kind side).
3. **Map 3c Hall of Three Crowns** (dưới lăng Borrow): đặt đủ Oathbreaker + Crimson Oath + Ironcrown -> hầm Arthur mở -> Arthur's Accord (lấy lại được cả ba thanh).
4. **Map 3d The First Cup** (dưới hầm mộ nhà Vey, mở bằng Vey Signet): câu đố rót chén theo gia phả, Vey Kin / dơi; tha Isolde -> bà để lại thứ tự. Thưởng Eventide.
5. ĐÃ XONG §278 (chờ xem trong Unity): biểu tượng trạng thái `Resources/VFX/status_bleed|burn|bind|chill|mark` (dải khung nhỏ) + có thể icon nội tại HUD.
6. Map 4 — ĐANG LÀM (xem khối "MAP 4" đầu mục 10). Khu bí mật Namesake để sau; Map 5 sau đó (Absolution).
QUY TẮC cho mọi khu bí mật (bài học §265): vẽ đồ thị lối đi — phần thưởng CHỈ tới được qua thử thách; không thêm lối tắt / cửa phụ / dịch chuyển nếu người dùng không yêu cầu;
tính năng ngoài kế hoạch đã duyệt -> hỏi trước; bị chê tính năng nào -> mặc định bỏ. Game top-down: không dùng "Slide down / Climb" giữa hai phòng cùng mặt sàn.
Mỗi map bí mật: side quest (`kind side`) theo manh mối, `fog off`, minimap bake, mục NOTES + md5 HANDOFF + checklist test.

**Đã xong §290 (17/11): Charge hướng North của The Last Guard** vẽ lại bằng PixelLab MCP (nhóm lg3_charge_north_fix_c), md5 b6ba18ab…;
script `_tools/kroven_2b/build_charge_n.py`, import_last_guard.py có CLIP_OVERRIDE. Chờ người dùng test. Nhóm thử fix_a/b/d còn trên PixelLab (chưa xoá).
PixelLab MCP đã kết nối ở tài khoản này; tải trực tiếp từ backblaze/api.pixellab bị proxy chặn -> lấy pixel qua ảnh xem trước pixelart_workbench (xem NOTES §290).
**Đã xong §292 (17/11): sửa toàn bộ động tác sai hướng của The Last Guard** — Aim/Stuck/Pull N+S, Idle_S, Bash_N (mới), LS_Thrust S/N(mới)/E.
Người dùng cần **Build Map 2b Scene** (số khung một số dải đổi, Bash_N + LS_Thrust_N mới) rồi test. Nhóm thử PixelLab thừa (lg3_charge_north_fix_a/b/d,
lg4_* , ls4_thrust_s/n/e) vẫn còn trên PixelLab, chưa xoá.
**Đã xong §293 (17/11): pha 2 (Last Stand) sửa toàn bộ hướng S/N** (Idle/Move giáo dựng, Thrust S/N, Leap N đáp cắm giáo trước thân, khung cuối Whirl).
Người dùng cần **Build Map 2b Scene** rồi test pha 2. Các nhóm thử ls5_* thừa còn trên PixelLab.
**ĐANG LÀM §294 (17/11): âm thanh** — code xong (Sfx / SfxRunner / SfxHooks + 24 file gắn hook, chưa biên dịch). Bộ âm: `_tools/sfx/sfx_manifest.json`
(60 id, node trong flow ElevenLabs `EVLxBJiWqsWWZXQEeRK9`). Việc còn lại:
1. Chạy nốt các node chưa tạo (gói free ElevenLabs giới hạn lượt / phút -> mỗi lần 2–3 node, `creative_run_flow_nodes`, generations_count = takes).
2. Người dùng mở quyền mạng `elevenlabsusercontent.com` (claude.ai Settings > Capabilities). Rồi `creative_get_generations` -> urls.json
   -> `_tools/sfx/fetch_sfx.py` -> `Assets/Resources/Audio/SFX/*.ogg`.
3. Người dùng Ctrl+R biên dịch, chơi thử, chỉnh Gain / MinGap trong Sfx.cs theo cảm nhận.

**Quyết định còn chờ người dùng**
- (17/11) Bỏ luôn cả việc NGHỈ ở lửa trại / nến thật (hồi máu + bình) không? Hiện vẫn giữ. Bỏ cả đốm lấp lánh trên đồ nhặt không? Hiện vẫn giữ.
- (17/11) Map 2 minimap chưa bake lại sau khi dời lối xuống (thay đổi nhỏ, 2x2 ô).
- Lấy Oathbreaker xong, đi ngược qua The Still Walk: bẫy vẫn bắn hay tắt?
- Mantra "Canticle of Stillness" (thưởng 1b) — chưa thiết kế, cần duyệt (không trùng chiêu dậm chân).
- Xoá `Assets/Resources/VFX/specimen/blue/` (40 file, không dùng) và `relic_knot.png`? Xoá `_to_delete/2026-11-16_iai_frames/`?
- Đổi màu Mantras of the Eye theo Plague Eye? Giữ tên Borrow / Darwin? Chiêu tia mắt của Specimen Zero (nó đã mù)? Cơ chế Grace?
- Hình cũ chưa dùng trong tileset SR-mine: mine_door / mine_door_open / door_bar(_off) / chute_top (không đặt trên map; không xoá khi chưa được bảo).

**Tồn đọng cũ**: âm thanh (chưa có); giao diện đổi phím; cân bằng boss Map 2–3; rác `_to_delete/`, `Assets/_Recovery/` chờ người dùng quyết.

---

## 11. Lỗi hay gặp — đừng lặp lại
- (17/11, Map 4) Bản sao `/tmp/gp` dùng symlink: ghi file tạm vào thư mục symlink = ghi thẳng vào Assets (đã lỡ 1 lần, chuyển sang `_to_delete/2026-11-17_map4_tmp/`).
  Thư mục đích của file thử phải là thư mục THẬT trong /tmp; script có assert `'/mnt/' not in realpath`.
- (17/11, Map 4) Đặt đồ / cửa bằng toạ độ ước lượng là nguồn lệch số 1 — luôn chạy `audit.py` + `check.py`; xem ảnh tỉ lệ thật cạnh mẫu.
- (17/11) `device_bash` không xoá được file trong thư mục kết nối (rm bị chặn) -> dời vào `_to_delete/<ngày>_<việc>/`.
- (17/11) WebFetch trang itch.io không trả ảnh; lấy URL ảnh bằng trình duyệt tích hợp (javascript đọc `img.itch.zone`) rồi `curl` trong device_bash.

- **Lớp vẽ map mới (§285)**: .tmx.meta phải `sortingMode: 1` (Custom Sort Axis) như Map 2 — để mặc định 0 (Stacked) thì đồ vẽ đè lên Ren. **Hồi sinh**: không còn dấu điểm lưu; checkpoint không có lửa thật = điểm ẩn; vào khu tự ghi điểm, gục tỉnh ở điểm gần nhất.
- **Boss / bẫy (§284)**: khiên boss KHÔNG được giơ mãi — chỉ chặn khi đang thủ, mọi đòn phải để lộ cửa sổ (hụt = kẹt, đâm vách = choáng, nứt khiên). Bẫy cơ khí (gai, trụ lăn, cưa, phụt nước) dùng `TrapArt.HurtPlayerCrush` — lăn không né được.
  Hốc bắn đặt hàng GIỮA mặt vách; tấm sàn là phiến đá lún, không vẽ như cần gạt; đồ cao (quan tài, giá sách) dựa sát vách và khối chặn phủ từ chân vách tới đế để Ren không lọt ra sau bị che.
  Không đặt phòng sát nhau 1 ô (R5 x21 chạm R2 x22 -> mất vách). Hành lang tối thiểu 3 hàng (2 hàng -> bảng vách thiếu khoá, vẽ sai). Hoạt ảnh skeleton-v3 hay làm MẤT khiên/giáo khi tay vung lớn — duyệt từng khung, dùng v3 cho đòn có vũ khí dài.
- Layer logic ẩn trong Tiled → ST2U bỏ qua. `unity:IsTrigger` đặt từng object.
- Thuộc tính sai kiểu (bool viết thành string, scale không `type="float"`) → không đọc được.
- `gsub` phân biệt hoa thường → kiểm `count == 1` trước khi thay.
- **Build All Map Scenes từng dựng lại scene Map 4 cũ** → đã bỏ khỏi Build All; không gọi `BuildMap4()`.
- `device_bash` grep cả `Packages/` → timeout; thu hẹp phạm vi.
- `device_commit_files` có lúc không ghi đè → kiểm md5 / pixel sau khi ghi.
- **Nhân vật / boss mới phải đúng style game (§283)**: PixelLab PRO với style_character_id = Lantern Knight (49d0004b) + max_colors ~24; đo số màu (16–27) và chiều cao trong game so với Ren (104 px) trước khi đưa vào. Boss nên cao ~1,4–1,5 lần Ren. Khu boss nên có cổng close_on_enter thật thay vì để ArenaSeal tự dựng màn sóng.
- **Cổng phải theo mẫu Map 2 (§282)**: lỗ 4 ô trong vách bắc, vòm 14233 (props) + song sắt 14234 (gate_visual) + khối chặn gate + 2 chân vòm obstacle; mặt vách hai bên chạy thẳng. KHÔNG đặt cổng giữa hành lang trống. Không dùng đồ trang trí trông như cổng / cửa ở chỗ không dẫn đi đâu.
- Cổng đấu trường đè hành lang → khoá người chơi ở ngoài; khu boss: khung khu = đúng sàn phòng. Nới phòng phải sửa cả `blockers`.
- Ảnh có lề trong suốt ở đáy → vật đè nhân vật. `.meta` tới trước PNG → Unity xoá meta.
- Vật đứng phải ở lớp `props` + có khối chặn ở chân, không để ở `ground decals`.
- Cutscene: người dùng phản đối spoil, hình Jesus, cảnh đêm khi game là ban ngày, tiêu đề chương, chữ năm viết bằng chữ, cảnh điên loạn, lính trong tranh "dân khoẻ mạnh". Luôn xem tranh trước khi đưa vào.
- **Phiên 16/11 chiều — người dùng phê bình, đừng lặp lại:**
  - Đổi màu katana cũ thay vì vẽ mới -> bị chê "lười"; mỗi thanh phải dáng / hoạ tiết riêng (katana_forge).
  - Thêm lối tắt (máng "Slide down / Climb", rồi cửa cài then) vào khu thử thách -> phá thử thách. Không thêm lối tắt khi không được yêu cầu.
  - Đặt vật lạc tông (hang rêu xanh Cave_Spawner trong mỏ) -> bỏ. Vật Grass Land trong hang phải bỏ cỏ (strip_green trong hollow_tiles.py).
  - Hình kiếm nhặt phải là KIẾM TRONG VỎ đầy đủ (không chỉ cái vỏ, không kiếm rời vỏ).
  - Xuống dòng trong thuộc tính .tmx phải là literal `\n` (script ghi xuống dòng thật -> chữ dính một đoạn).
  - Thông báo dưới màn hình trùng với bảng đang mở / tả lại cái đã thấy -> thừa.
  - Thanh UI gắn làm con của nhân vật -> lệch tỉ lệ (không đầy khung). Vẽ theo đơn vị thế giới, không parent.
  - Nội tại phải NHÌN THẤY được (thanh / hiệu ứng), không chỉ "+x%".
