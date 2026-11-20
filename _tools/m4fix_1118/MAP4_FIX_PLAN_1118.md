# Map 4 — Kế hoạch sửa 10 lỗi (18/11, 01:30) — BẢNG TRẠNG THÁI cập nhật 18/11 14:40

## Trạng thái (đọc trước)
Map 4 md5 hiện tại **`a7b2895f444c1aa3ba7919c105e26dfb`** (NOTES §324). Map cao 148 hàng (§318 chèn 24 hàng). Toạ độ trong các mục dưới là
TRƯỚC khi chèn: cột 46–65 có y ≥ 26, và các cột khác có y ≥ 22, nay phải cộng 24.

| Mục | Trạng thái | Đã làm (NOTES) | Còn lại |
|---|---|---|---|
| §1 Ren đè cổng | Đã sửa, chờ test | Lintel 4 cổng (§317) + vòm lối đi `lintel_arch_warden`; bịt 3 vòm cụt bằng `arch_seal` (§321); DepthEffects không làm mờ cổng / lintel (§319) | Cửa Crypt + cổng sọ chưa có lintel; nếu còn lỗi thì xin ảnh + vị trí |
| §2 Pell không đi | Đã sửa, chờ test | Pell có walk E/N/S; Villager đi theo cảnh bỏ qua người chơi, đường ra qua giữa cổng (§319) | — |
| §3 Quái đứng im | Một phần, chờ test | Pit_Bat floating = 0 (sửa thẳng prefab + builder); Registrar moveSpeed 0.8 tạm (§319) | Quái khác: kiểm zone start / stand_in / leash |
| §4 Bảng chữ trong cutscene | Xong | `cutscene_only` cho 5 note (§317); bỏ cảnh lantern_line "Across the pit" (§319) | — |
| §5 Boss Registrar | **CHƯA LÀM** | — | Chờ duyệt bố cục 2 phòng bẫy + lượt bẫy + thoại (thiết kế ở dưới) |
| §6 Hai VFX choáng | Xong | Bỏ sao trong `PlayerStatus.Daze` (§317) | — |
| §7 Cầu mờ | Xong | Alpha ván cầu (§317) | — |
| §8 Hố gai đi vào được | Xong | Blocker phủ hố (§316) | — |
| §9 Penitent's Walk | Xong, chờ test | +24 hàng, 3 đoạn bẫy (§318); máy bắn áp tường (§319) | — |
| §10 Blade Yard | Xong, chờ test | Người dùng tự vẽ lưới 6x4; 10 bẫy khớp lưới + IsTrigger (§319, §321); rãnh cưa gọn (§321) | — |
| Thêm: lối tây trước khi cứu Pell | Xong, chờ test | Rào `west_barricade`, mở bằng `west_barricade_down` trong cảnh pell_freed (§321) | — |
| Thêm: áo tế không lăn | Xong, chờ test | §320 | — |

Việc sau §5: boss Hall of Naming (Candidate VI + The Severing + Pell chết), khu bí mật Namesake, rồi Map 5. Thứ tự đầy đủ ở `SOUL_REQUIEM_ROADMAP.md`.

---


> Phiên sau: đọc file này trước. Trạng thái tới §314 của NOTES:
> - Đã sửa lỗi lớp trùng tên "gates (1)" khiến 11 cổng Map 4 không bao giờ mở. Mọi lỗi gặp TRƯỚC bản sửa đó cần test lại.
> - Map 4 md5 `f8fe5f1aaf272963d45acc0aad34c61d`.
> - Công cụ trong `_tools/m4fix_1118/`:
>   - `render_m4.py`: vẽ .tmx kèm lớp logic; có `--only=lớp`, `--noobj`.
>   - `reach_m4.py`: chạy BFS theo từng giai đoạn cờ.
>   - `patch_*.py`: các patch, có kiểm md5 và sao lưu.
>
> Quy ước: sửa .tmx bằng script, kiểm md5 trước khi ghi, sao lưu vào `_backups/<ngày>_<việc>/`.
> **`range` của vùng interact tính bằng đơn vị thế giới: 1 ô = 0,32.**

Thứ tự làm, xếp theo mức chặn tiến trình:

| Ưu tiên | Mục | Vì sao |
|---|---|---|
| 1 | §3 Quái đứng im, §8 Ren lọt xuống hố gai | Làm hỏng combat và độ khó của toàn map |
| 2 | §1 Ren vẽ đè lên cổng, §7 Cầu mờ, §6 Hai VFX choáng | Lỗi hiển thị, sửa nhanh |
| 3 | §2 Pell không có animation đi, §4 Bảng chữ trong cutscene | Chất lượng cảnh diễn |
| 4 | §10 Blade Yard, §9 Penitent's Walk | Thiết kế lại thử thách |
| 5 | §5 Boss The Registrar | Hệ thống mới, việc lớn nhất |

---

## §1 Cổng: Ren vẽ đè lên cổng (ảnh 1)

**Triệu chứng.** Ren đứng trong ô giam, phía bắc bức tường có vòm. Thân Ren lại vẽ đè lên mặt tường và vòm cổng. Đúng ra Ren phải bị tường che.

**Nguyên nhân.**
- Mặt tường nằm ở lớp ô `op_cells`, với `unity:SortingOrder 0`. Tilemap vẽ cả lớp ở một mức sorting, không sắp theo trục y từng ô.
- Ren nằm ở mức sorting cao hơn, nên luôn được vẽ trên.
- Hình cổng (lớp `gates`, order 2) được GateAnimator chép đúng mức sorting đó, nên cũng không sắp theo y.

**Cách sửa** (chọn A):
- **A. Recommended.** Cho các tilemap có mặt tường đứng sắp theo y giống nhân vật:
  - Đặt `TilemapRenderer.mode = Individual`.
  - Đặt sorting layer và order bằng của Player.
  - Kiểm tra Transparency Sort Axis (0,1,0) trong Graphics / URP 2D Renderer.
  - Làm trong builder scene Map 4, sau khi nhập map. Trước đó xem Map 2b và Map 3 xử lý tường thế nào để làm giống.
- B. Viết script tách các ô mặt tường quanh cổng và cửa (cột cổng ±2 ô) thành vật thể có gid trong lớp `props`, để chúng tự sắp theo y. Rủi ro: nhiều vật, khó bảo trì.
- Cổng (GateAnimator): `spriteSortPoint = Pivot`, pivot đặt ở chân tường, cùng sorting layer với Player.

**Kiểm tra.**
- Ren đứng phía bắc cổng: bị tường và cổng che.
- Ren đứng phía nam cổng: vẽ đè lên tường.
- Kiểm cả 11 cổng của Map 4 và các cổng Map 2–3, tránh làm hỏng chỗ khác.

## §2 Pell không có animation di chuyển (ảnh 2)

**Nguyên nhân.** Lệnh `walk` trong StoryScenes chỉ dời vị trí NPC. Pell là Villager, chỉ có hình đứng hoặc idle. Trên PixelLab, Pell (`66154674-dfd5-4c92-aac1-798ba653e77f`, 112 px) đã có 6 animation, cần kiểm có walk chưa.

**Cách sửa.**
1. PixelLab: nếu Pell chưa có walk 8 hướng thì sinh bằng v3. Prompt theo kiểu đã duyệt:
   - *"walks at a slow, careful pace … Same body proportions throughout. Calm face, small realistic motion, no effects."*
   - Mỗi hướng ghi rõ hướng nhìn.
   - Bỏ khung tham chiếu 0.
   - Soát từng khung theo `_tools/ANIMATION_RULES.md`.
2. Nhập vào `Assets/GamePackage/NPC/Pell/{idle,walk}/<hướng>/`.
3. Viết `NpcAnimatorBuilder`: BlendTree 8 hướng, tham số DirX, DirY, Speed.
4. Villager hoặc lệnh `walk` của StoryScenes đặt Speed > 0 và hướng mỗi khung khi NPC di chuyển, đặt Speed = 0 khi tới nơi.
5. Áp dụng chung cho NPC khác: Aldous, Goblin Child.

## §3 Quái đứng im, không đuổi theo Ren (ảnh 3, Pit Bat cạnh cổng sọ)

**Nghi vấn.** Kiểm theo thứ tự:
1. Các cổng khoá đấu trường và zone trước bản sửa §314 không chạy, nên zone không "start" và AI không nhận lệnh. Cần test lại sau bản sửa.
2. Thuộc tính điểm sinh `stand_in`, `provoke`, `spawn_when` trong `_tools/map4_1117/enemies.py`: quái được dựng sẵn làm vật trang trí chờ zone start.
3. Zone p7 có `start_when = scene_lantern_line`. Nếu Ren vào zone trước khi cảnh lantern_line diễn, thì sau khi cờ bật zone có được start lại không? Xem `MapEncounterDirector.Stages.cs`.
4. Prefab Pit_Bat (Map4EnemyBuilder):
   - Có EnemyAI, Rigidbody2D, tốc độ khác 0 chưa?
   - Quái bay có cần NavMesh hay đường đi riêng không?
   - So với Giant Bat ở Map 3.

**Kiểm tra.** Dùng Console và DevTools (`MapEncounterDirector.DevTools.cs`) xem trạng thái zone p7. Đánh trúng quái thì quái phải aggro.

## §4 Cutscene đang diễn thì bảng chữ hiện lên (ảnh 4: Chronicle of the First Keepers và cảnh Angel Bird)

**Mong muốn của người dùng.** Cảnh kiểu này chỉ diễn cutscene, bỏ bảng chữ. Tài liệu vẫn vào túi đồ để đọc lại sau.

**Cách sửa.**
- Ghi chú (NoteMechanism) nào có cảnh story gắn cờ đọc (`angel_bird` có `when = chronicle_read`) thì khi Ren nhặt: không mở bảng chữ. Chỉ bật cờ đọc, thêm tài liệu vào KeyRing và chạy cảnh.
- Cách làm: thêm thuộc tính Tiled `cutscene_only = true` trên note, hoặc tự phát hiện có story `trigger=flag` với `when` trùng cờ của note.
- Rà toàn game các cặp note và cảnh tương tự: `vii_read`, `letters_read`, `prophecy_seen`, `inventory_seen` ở Map 4, cùng các map khác.

**Kiểm tra.** Nhặt note: cảnh diễn ngay, không có bảng chữ. Mở túi đồ: đọc lại được nội dung.

## §5 Mini boss The Registrar: thiết kế lại hoàn toàn (ảnh 5)

**Hiện trạng.** Boss đứng yên trên bệ, chỉ bắn (`RegistrarSkills`), và quá nhỏ.

**Yêu cầu của người dùng.**
- Boss to gấp **3 lần Ren**.
- Bỏ kiểu bắn chưởng thường. Thay bằng dịch chuyển Ren vào **phòng bẫy**.

### Cấu trúc trận

| Pha | Nơi | Diễn biến | Kết thúc |
|---|---|---|---|
| 1 | Phòng bẫy A ("The Margin") | Boss "đọc tên": bẫy chạy theo lượt như màn obby (lưỡi cưa, giáo phóng, gai trồi, con lăn, tấm ép). Mỗi lượt có báo trước ≥ 0,6 s và luôn có làn an toàn. Sau 3–4 lượt boss "mệt" khoảng 4 s: hạ xuống giữa phòng, không bất tử. | Hết 1/3 máu thì về pha 2 |
| 2 | Phòng bẫy B ("The Index"), khó hơn | Lượt dài hơn, hai loại bẫy chồng nhau, làn an toàn đổi chỗ, cửa sổ đánh ngắn hơn. | Hết 2/3 máu thì về Scriptorium |
| 3 | Scriptorium (Map 4) | Gọi nhiều đợt quái (Cutthroat, Cantor). Boss bắn chưởng khó né: quạt đạn, vòng xoáy, mưa tên đá, đều có báo trước. | Hạ boss hẳn |

**Kỹ thuật.**
- **Phòng bẫy:** làm khu riêng trong cùng .tmx Map 4, ngoài vùng chơi (mở rộng bản đồ, ví dụ thêm cột ở mép trái). Mỗi phòng có `zone` riêng, `player_start` riêng (điểm dịch chuyển) và bộ bẫy có id.
- **Dịch chuyển:** component mới `TrialTeleport` làm mờ màn hình → đặt Ren → máy quay theo → mở màn. Chết trong phòng bẫy thì làm lại từ đầu pha đó, máu boss giữ nguyên.
- **`TrapSequencer`** (component mới, mỗi file một MonoBehaviour):
  - Danh sách lượt, mỗi lượt gồm id các bẫy kèm độ trễ.
  - Đọc từ thuộc tính Tiled (ví dụ `seq_1 = "saw_a,spk_b@0.4|launch_c@1.0"`).
  - Bật các bẫy có sẵn (`RollerMechanism`, `BladeMechanism`, `LauncherMechanism`, `SpikeMechanism`, `PlateMechanism`) qua TrapSignals.
- **`RegistrarSkills`:** state machine Intro → Trial(n) → Exhausted → (đủ ngưỡng máu thì) Teleport → … → Final. Ngoài trạng thái Exhausted thì bất tử, có hiệu ứng khiên chữ.
- **Kích cỡ:** scale nguyên 3 lần (filter Point) cho sprite GameMaster. Nếu thô quá thì sinh model PixelLab mới khoảng 192–256 px. Sửa collider, thanh máu boss, `BossIntro` theo cỡ mới.
- **Cần người dùng duyệt trước khi dựng:**
  - Bố cục 2 phòng bẫy (vẽ bằng `render_m4.py`).
  - Danh sách lượt bẫy.
  - Thoại boss: tiếng Anh, không spoil.

## §6 Ren có 2 VFX choáng trên đầu, một cao một thấp (ảnh 6)

**Nghi vấn.** Hai hệ thống cùng vẽ:
- `Health.Stun` sinh sao choáng.
- Biểu tượng trạng thái (StatusIcons / PlayerStatus) hoặc VFX "LG Stun" của boss.

Hai cái có độ cao khác nhau. Có thể một cái gắn theo chiều cao sprite, cái kia cố định, nên lệch thêm khi đổi sang bộ áo tế.

**Cách sửa.**
- Tìm mọi chỗ sinh VFX choáng (grep `stun`, `Stun(`, `stars`).
- Giữ **một** nguồn duy nhất: component trên Player, có cờ "đang hiện", gọi lại thì gia hạn chứ không sinh thêm.
- Neo theo đỉnh sprite hiện tại.

## §7 Cây cầu bị mờ (ảnh 7)

**Nghi vấn.** Ảnh khung `Resources/VFX/m4_op_bridge_v.png` có import sai: filter Bilinear, có nén, có mipmap, hoặc PPU khác 100. Cũng có thể GateAnimator kéo giãn (khung khác cỡ hình gốc trên map), hoặc hình cầu có alpha thấp (lớp `gates` id 21).

**Cách sửa.**
- Đặt meta: `filterMode 0`, `textureCompression 0`, `mipmaps 0`, PPU 100.
- Kiểm tất cả `m4_op_*` và `m4_cr_*` cùng lúc.
- Kiểm opacity của lớp và vật thể trong .tmx.

## §8 Ren đi vào được hố gai (ảnh 8)

**Nguyên nhân.** Các hố gai (lớp ô `op_spk`, Blade Yard và Bleeding Way) chỉ là hình, không có collider. Các ô viền đá quanh hố cũng không chặn.

**Cách sửa.**
- Script đọc các vùng liền nhau của lớp `op_spk` (cùng lớp `op_pit` nếu cần).
- Sinh object `blocker` phủ kín từng hố, mép trong lùi khoảng 0,25 ô cho khớp viền đá.
- Chạy lại `reach_m4.py` để chắc không chặn mất đường chính.

**Phương án phụ** (nếu người dùng muốn hố là bẫy): thay blocker bằng vùng `pit` gây sát thương và đẩy về chỗ an toàn gần nhất. Hỏi người dùng.

## §9 Bẫy trước khi đánh boss quá dễ, quá ngắn (ảnh 9, Penitent's Walk c6, y 21–43)

**Hướng sửa.** Kéo dài đường lên trước khi tới Hall of Naming:
- **Chèn N hàng** (ví dụ 24) tại y = 21, giữa c7 và c6, bằng script. Thêm hàng vào mọi lớp ô; mọi object có y ≥ 21×32 thì cộng thêm N×32. c7 Hall of Naming và lối ra Map 5 giữ nguyên vị trí.
- Thiết kế đường 3 đoạn, mỗi đoạn dạy một loại bẫy rồi kết hợp:
  1. **Sóng sắt trồi** theo nhịp (có sẵn, cảnh `walk_hint`): đứng chờ ở hốc an toàn, rồi bước theo sóng.
  2. **Hai làn lưỡi cưa ngược chiều** cộng **giáo phóng ngang** từ tường: canh nhịp, có hốc nghỉ giữa đường.
  3. **Tấm ép kích hoạt con lăn** từ trên dốc xuống, kết hợp sóng sắt. Cuối đoạn là `walk_top`.
- Có checkpoint giữa đoạn 2 và 3. Không có đường vòng: tường hai bên dùng blocker.
- Kiểm bằng `reach_m4.py` (vượt được) và chạy thử từng nhịp. Mỗi đoạn có ≥ 1 cửa sổ an toàn ≥ 0,8 s.

## §10 Blade Yard p6 không ép người chơi vượt bẫy (ảnh 10)

**Vấn đề.** Ren chỉ cần vòng qua lưỡi cưa rồi gạt cần là sang khu khác. Không có gì chặn, không có gì buộc phải đi qua bẫy.

**Thiết kế lại theo nguyên tắc** "mục tiêu nằm ở bên kia bẫy, không có đường vòng":
- **Dời tời cầu (`bridge_winch`)** ra cuối sân, phía bên kia các hàng lưỡi cưa. Cầu `pit_bridge` cũng vậy.
- **Rào hai bên** bằng tường và rào thấp (blocker), để mỗi hàng lưỡi cưa chạy suốt chiều ngang hành lang và không có khe để vòng.
- **Hố gai** (sau §8) làm vách mê cung: tạo các hành lang hẹp mà lưỡi cưa quét qua.
- **Thêm lớp áp lực:**
  - Tấm ép (`plate`) giữa đường bắn giáo từ tường đối diện.
  - Quái Pit Bat bay quấy khi đang canh nhịp.
- **Về lại:** gạt tời xong thì mở một lối tắt ngắn quay về Rolling Hall, để chết không phải đi lại cả đoạn. Thêm checkpoint `rest_pit` ở đầu sân.
- Vẽ bố cục bằng `render_m4.py` cho người dùng duyệt trước khi sửa .tmx.

---

**Ghi chú chung cho phiên sau.**
- Mỗi lần sửa .tmx: kiểm md5, chạy `reach_m4.py`, chạy `render_m4.py --logic` để soát.
- Ghi NOTES (tiếp theo là §323), cập nhật HANDOFF + bảng trạng thái đầu file này.
- Hỏi người dùng trước các việc lớn: §5, §9, §10.
