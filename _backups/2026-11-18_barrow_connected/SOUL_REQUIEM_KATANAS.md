# SOUL REQUIEM — Bộ katana (chốt 16/11/2026, 04h55)

> Nguồn sự thật cho tên, lore, nội tại, nơi nhặt và màu của mọi katana. Chữ trong game: tiếng Anh.
> Liên quan: `SOUL_REQUIEM_HANDOFF.md` mục 6–7 (lore), NOTES §250–§251.

## Nguyên tắc
- **Lore phải hợp lý ngoài đời** (không có chuyện tù nhân tự rèn kiếm trong ngục…). Chỉ thanh gắn với sự kiện lớn mới có lore dài; thanh nhỏ một dòng là đủ.
- **Tên ngắn, có sức nặng**, không ghép chữ sáo rỗng.
- **Nội tại sinh ra từ câu chuyện** của chính thanh kiếm.
- **Màu kiếm theo màu chiêu của boss trong khu nơi nó nằm** (Moonlit Edge = tím như Specimen Zero). Khu không có boss: màu theo thời đại / chủ cũ.
- Kiếm thời ba vương quốc chỉ nằm **dưới lòng đất** (khu bí mật / map con). Không spoil trước Map 4: lore đọc ở Map 1–3 chỉ gợi, Map 4 mới vỡ nghĩa.
- Mỗi thanh cần: `Resources/Weapons/<Id>.asset` (WeaponDefinition), `Showcase/<Id>.png` 256x96, `Designs/<Id>_design.png` 64x32 (UV trên người Ren),
  hình nhặt dưới đất (tile object lớp `mechanics_art`), bộ vệt chém `VFX/slash3/<style>_*` (tuỳ chọn), `trait`.

## Bảng tổng
| # | Tên (id) | Nơi nhặt | Thời đại / chủ cũ | Lore | Nội tại | Màu | Trạng thái |
|---|---|---|---|---|---|---|---|
| 1 | **Ronin's Katana** (`katana`) | Có sẵn | 1176, nhà Corwen | 1 dòng | Counterstroke | Thép trơn, đen | Có trong game |
| 2 | **Oathbreaker** (`katana_oathbreaker`) | Map 1b Hermit's Hollow | Arden, 29–15 TCN, kiếm sư hoàng gia | Ngắn | Iaijutsu (mới) | Thép xám lam | Chưa làm (đợt E) |
| 3 | **Moonlit Edge** (`katana_moonlit`) | Map 2, Containment Vault (rơi từ Specimen Zero) | Children of Light, sau Angel Bird | Dài (đã có) | Vigil | **Tím** (theo Specimen Zero, §250) | Có trong game |
| 4 | **Ironcrown** (`katana_ironcrown`) | Map 2b Kroven Deep | Kroven, vua Nolan (mất 31 TCN) | Ngắn | Last Stand (mới) | Sắt lạnh, đồng cũ | Chưa làm (đợt E) |
| 5 | **Crimson Oath** (`katana_crimson`) | Map 3b King's Barrow | Miravel, vua Borrow | Dài (đã có) | Blood Oath | Đỏ, vàng | Có trong game |
| 6 | **Eventide** (`katana_eventide`) | Map 3, The First Cup (mở bằng Vey Signet) | Nhà Vey, Aurel Vey | Dài | Hunger (mới) | Đen, đỏ máu | Chưa làm (đợt E) |
| 7 | **Arthur's Accord** (`katana_accord`) | Map 3c Hall of Three Crowns | Thời vua Arthur, ba nước hoà ước | Dài | Accord (mới) | Thép sáng, vàng cũ | Chưa làm (đợt E) |
| 8 | **Namesake** (`katana_namesake`) — TẠM | Map 4 (khu bí mật chưa có) | Soul Requiem, Candidate VII | Ngắn | Namebound (mới) — tạm | Trắng ngà, xanh dương | Chờ thiết kế Map 4 |
| 9 | **Absolution** (`katana_absolution`) — TẠM | Map 5 (khu bí mật chưa có) | Darwin thời còn là tông đồ | Dài | Penance (mới) — tạm | Vàng nhạt, đỏ sẫm | Chờ thiết kế Map 5 |

## Từng thanh

### 1. Ronin's Katana — giữ
- Lore (1 dòng, giữ bản đang có trong asset là đủ): Corwen mua của một thợ rèn rong, trao cho Ren năm 12 tuổi kèm một luật: *draw it for other people*.
- Nội tại **Counterstroke** (đã có, `WeaponTraitKind.Counterstroke`): nhát đầu tiên trong **1,2 s** sau khi lăn gây **+50%** sát thương.

### 2. Oathbreaker — Map 1b Hermit's Hollow
- Lore (ngắn): kiếm của kiếm sư hoàng gia Arden — tên **Merek** (§261; 14 vạch đếm năm trên vách = 14 năm ẩn trong mỏ). Vua Lucas sai ông đi giết đứa trẻ trong lời tiên tri; ông tìm thấy đứa trẻ, không ra tay được, bỏ trốn,
  bị truy nã như kẻ phản thề và chết già trong mỏ đồng. Lời thề ông phá là lời thề với nhà vua — tên kiếm vừa là tội danh vừa là danh dự.
  Map 1 KHÔNG nói đứa trẻ là ai (Map 4 mới vỡ nghĩa).
- Nội tại **Iaijutsu** (§271 đổi thành ĐÒN GIỮ): giữ phím chém -> Ren đứng thế (dừng khung vung kiếm của nhát 1), qua mốc **0,5 s** = **+60%** + choáng, giữ tiếp tăng dần tới **+100%** khi thanh đầy ở **1,5 s** (§274); thả khi đầy -> đòn rút kiếm **+60%** sát thương, choáng quái thường 0,8 s (boss không choáng); thả sớm = nhát thường.
- Màu: thép xám lam (Arden).

### 3. Moonlit Edge — Map 2 (giữ tên, đã đổi màu tím)
- Lore: giữ bản đang có (rèn 15 đêm dưới ngọn đèn canh không được tắt; cắm vào ngực Specimen Zero để ru nó ngủ).
- Nội tại **Vigil** (đã có): **5 s** không trúng đòn → lưỡi sáng, **+15%** sát thương; trúng một đòn là tắt.
- Màu: tím (chiêu Specimen Zero).

### 4. Ironcrown — Map 2b Kroven Deep
- Lore (ngắn): vua Kroven được tấn phong bằng một thanh kiếm, không phải vương miện. Kinh đô thất thủ năm 31 TCN, vua Nolan tử trận; cận vệ mang kiếm
  xuống hầm sâu và giăng bẫy khắp lối vào để Borrow không bao giờ lấy được làm chiến lợi phẩm. (→ lý do Kroven Deep đầy bẫy; dùng lại code bẫy Map 4 cũ.)
- Kèm sách kiếm pháp **The Rampart Forms** (thay "The Broken Chain").
- Nội tại **Last Stand** (mới): máu dưới **50%** → nhận ít hơn **20%** sát thương và không bị đánh ngắt khi đang chém.
- Màu: sắt lạnh, đồng cũ.

### 5. Crimson Oath — Map 3b King's Barrow (giữ)
- Lore: giữ bản đang có (Borrow thề với vương quốc tan vỡ; Vael khuếch đại, kiếm "uống" phần còn lại của cuộc chiến; tay ông còn úp trên chuôi).
- Nội tại **Blood Oath** (đã có): hút **6%** sát thương gây ra; máu dưới **40%** → **+25%** sát thương.
- Bỏ ý "sáu kiếm thề" cũ.

### 6. Eventide — Map 3, The First Cup
- Lore (dài): kiếm Aurel Vey mang theo đêm ông uống chén nước hồ Nova đầu tiên; từ đêm đó nó chưa từng thấy ánh mặt trời.
  Map 3 chỉ đọc được "đêm của chén đầu tiên"; Map 4 mới hiểu đó là đêm sau khi Ngài bị giết.
- Nội tại **Hunger** (mới): mỗi đòn kết liễu hồi **8%** máu tối đa; bình máu hồi ít hơn **30%**.
- Màu: đen, đỏ máu (nhà Vey / Isolde).

### 7. Arthur's Accord — Map 3c Hall of Three Crowns
- Lore (dài): sảnh nơi ba vua thề hoà ước dưới thời Arthur; trên bàn đá còn ba giá kiếm trống. Đặt đủ **Oathbreaker (Arden), Crimson Oath (Miravel),
  Ironcrown (Kroven)** → hầm của Arthur mở, trong đó là kiếm của chính ông. KHÔNG rèn lại kiếm; **Ren lấy lại được cả ba thanh** sau khi hầm mở.
- Nội tại **Accord** (mới): nhát cuối combo **+40%** sát thương và giảm **15%** sát thương nhận vào trong **2 s**.
- Màu: thép sáng, vàng cũ.

### 8. Namesake — Map 4 (TẠM, chờ thiết kế khu bí mật Map 4)
- Ý tưởng: giáo phái rèn sẵn một thanh cho mỗi Candidate, khắc tên thật lên chuôi. Thanh của Candidate VII chưa từng được trao; nằm cạnh sổ tên gốc
  (nối với `name_reclaimed`). Chỉ cần lore ngắn.
- Nội tại **Namebound** (tạm): hồi chiêu Mantras of the Name giảm **15%**.
- Màu: trắng ngà, xanh dương (Ether nguyên bản — Ren, sau The Severing).

### 9. Absolution — Map 5 (TẠM, chờ thiết kế khu bí mật Map 5)
- Ý tưởng: kiếm của Darwin thời còn là tông đồ — chính thanh hắn dùng "trừng phạt" Thrag ở Angel Bird; hắn gọi đó là sự xá tội. Cất trong Crypt of the Haloed.
- Nội tại **Penance** (tạm): mỗi lần trúng đòn, nhát chém kế tiếp **+30%** sát thương (cộng dồn tối đa 3 lần, mất khi chém trượt).
- Màu: vàng nhạt, đỏ sẫm.

## Khu bí mật chứa katana (chốt 16/11 14h50)
Nguyên tắc: mỗi khu đủ 4 lớp — (1) MANH MỐI (lời đồn NPC / ghi chép + dấu hiệu nhỏ, không biển "ánh vàng"), (2) CHÌA KHOÁ / CHIÊU lấy ở chỗ khác TRONG CÙNG MAP
(không bắt quay về map trước — game không đi ngược map), (3) THỬ THÁCH bên trong gắn chủ đề thanh kiếm, (4) PHẦN THƯỞNG có lore (hài cốt / lăng của chủ cũ + ghi chép).
Map chính phải sửa sao cho lối sang map phụ hợp lý (không phi logic).

| Thanh | Khu | Lối vào (map chính) | Chìa / điều kiện | Thử thách | Trạng thái |
|---|---|---|---|---|---|
| Moonlit Edge | Map 2 Containment Vault | Reliquary of the Ascended | Choir Vault Key (Records Vault, khoá 3 vòng) | Boss tuỳ chọn Specimen Zero | ĐẠT (có sẵn) |
| Oathbreaker | **Map 1b Hermit's Hollow** (sâu trong Old Copper Mine) | Miệng mỏ đã sập (đống đá chặn) — chỉ Earthshatter phá được (§261; xe quặng nằm trên cỏ nên bỏ ý "vết nứt sau xe quặng") | Earthshatter (rương Hob) + manh mối thợ rèn / Hob ("tiếng lách cách khi bước, im khi đứng") | Collapsed Gallery (tổ nhện) → Green Seam (vũng Ether độc) → The Still Walk (bẫy tên CHỈ bắn khi Ren di chuyển — dạy Iaijutsu) → Hermit's Cell; cửa gỗ cài then (mở từ phía buồng) = lối tắt một chiều về Gallery (§262) | Đã dựng tmx (§261), chờ test |
| Ironcrown | **Map 2b The Kroven Deep** (bể chứa cổ của kinh đô Kroven dưới Undercity) | Nắp hầm Forgotten Store | Ổ khoá vương miện — 3 huy hiệu Kroven chép trong Malric's Archive | Đoạn bẫy cận vệ (code bẫy Map 4 cũ) + boss nhỏ The Last Guard | | Đã dựng §281, chờ test |
| Crimson Oath | **Map 3b mở rộng + The Red Field** | Bậc đá dưới gốc cây chết bờ nam Sinking Fields (bia "...ROW", lời đồn Aldous) | Earthshatter phá phiến đá vương miện dưới quan tài trống | The Drowned Muster (2 đợt) -> boss bí mật The Red Field (hàng lính hồn / rừng giáo / lõi ấn) -> lăng Borrow, gỡ kiếm khỏi tay vua | Đã dựng §330, chờ test |
| Eventide | **Map 3d The First Cup** (dưới hầm mộ nhà Vey) | Cửa hầm mộ House Vey (đang đóng kín) | Vey Signet (sau Bower) | Câu đố rót chén theo dòng máu (gia phả) + Vey Kin / dơi; tha Isolde -> bà để lại thứ tự | Chưa |
| Arthur's Accord | **Map 3c Hall of Three Crowns** (dưới lăng Borrow) | Phòng sâu nhất King's Barrow, sau The Red Field | Đặt đủ Oathbreaker + Ironcrown + Crimson Oath | — (phần thưởng hoàn tất) | Chưa |
| Namesake / Absolution | Map 4 / Map 5 | — | — | — | Chờ map |

## Thiết kế hình (16/11)
- KHÔNG đổi màu trên khung kiếm có sẵn. Mỗi thanh vẽ từ đầu bằng `_tools/lore_1116/katana_forge.py` theo thông số trong `katana_specs.py` (dáng, hamon, tsuba, kiểu quấn, hoa văn vỏ riêng). Tờ duyệt: `_tools/lore_1116/preview/katana_forge.png`. Cả 7 thanh (kể cả 3 thanh cũ, vẽ lại 16/11 §256) đều có thông số trong `katana_specs.py` (khoá: ronin, moonlit, crimson, oathbreaker, ironcrown, eventide, accord).

## Ghi chú code
- Nội tại có sẵn: `WeaponTraitKind.Counterstroke / Vigil / BloodOath` (`Assets/Scripts/Gameplay/Player/Weapons/WeaponTrait.cs`).
- Cần thêm: `Iaijutsu`, `LastStand`, `Hunger`, `Accord` (đợt E); `Namebound`, `Penance` khi làm Map 4–5. Số liệu lấy từ `window / bonus / leech / threshold` của WeaponTrait.

## Hiệu ứng lên quái + thước nội tại (16/11, §275 — người dùng duyệt bảng đề xuất)
| Kiếm | Hiệu ứng | Khi nào | Số liệu |
|---|---|---|---|
| Ronin's Katana | — (kiếm gốc, mộc) | — | — |
| Moonlit Edge | **Bind** (rễ trói, nháy tím) | khi Vigil đang sáng | chậm 40%, 2 s |
| Crimson Oath | **Bleed** (chảy máu, cộng dồn) | mọi nhát | 2,5 / s mỗi tầng, 3 s, tối đa 3 tầng |
| Oathbreaker | **Stun** (sẵn trong Iaijutsu) | đòn giữ qua mốc | 0,8 s (boss không choáng) |
| Ironcrown | **Chill** (băng giá, nháy xanh lạnh) | nhát cuối combo | chậm 30%, 1,5 s |
| Eventide | **Mark** (dấu khát máu) | mọi nhát | 4 s; quái mang dấu chết (bất kỳ nguồn) -> hồi 8% máu |
| Arthur's Accord | **Holy fire** (thiêu, nháy vàng) | nhát cuối combo | 5 / s, 3 s |
Boss: chậm còn một nửa; quái miễn khống chế: không chậm. ~~Thước nội tại~~ -> §279: Ô NỘI TẠI cạnh Đánh / Lăn (icon `ic_trait_<kiểu>`, sáng khi bật, vòng chỉ cho Counterstroke / Vigil / Accord, số tầng Penance / Hunger) + vạch ngưỡng trên thanh máu (Blood Oath / Last Stand).
Hình biểu tượng trạng thái: CHỜ PixelLab (Resources/VFX/status_bleed|burn|bind|chill|mark).
