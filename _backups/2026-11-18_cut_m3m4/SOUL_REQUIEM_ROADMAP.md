# SOUL REQUIEM — Tầm nhìn & việc còn lại (cập nhật 18/11/2026 14:40)

> File này trả lời 3 câu: **game hướng tới đâu**, **đang ở đâu**, **còn phải làm gì (theo thứ tự)**.
> Chi tiết kỹ thuật / quy tắc làm việc: `SOUL_REQUIEM_HANDOFF.md`. Nhật ký từng thay đổi: `SOUL_REQUIEM_NOTES.md` (mục mới nhất §322).
> Cốt truyện đã chốt: HANDOFF mục 6–8 + `SOUL_REQUIEM_LORE_2026-11-16.docx`. Katana: `SOUL_REQUIEM_KATANAS.md`.
> Mọi thứ ghi "chờ duyệt" = PHẢI hỏi người dùng (2–4 phương án, đánh dấu Recommended) trước khi dựng.

---

## 1. Tầm nhìn

**Soul Requiem** — action RPG top-down dark fantasy (Unity 6 URP 2D, pixel art 32 px), 5 map chính + các khu / map bí mật.
Ren, kiếm khách Unawakened ~20 tuổi, đi từ "giúp một làng" tới chỗ biết mình là **Candidate VII** — vật chứa được nuôi để ghép lại
thân xác The Ascended — rồi quyết định số phận trái tim của Ngài.

Trụ cột thiết kế (người dùng đã khẳng định nhiều lần):
1. **Kể chuyện bằng môi trường + cutscene sử thi liền mạch** — không tiêu đề chương, không spoil trước Map 4, năm viết bằng số, không hình Jesus.
2. **Mỗi boss mang một bộ phận thánh -> rơi bộ phận đó -> Ren nhận một bộ Mantra** (Eye, Vessel, Ear, …, Name). Map 4 xé MỌI bộ phận; còn lại Mantras of the Name (mạnh nhất).
3. **Bẫy / thử thách công bằng**: luôn có báo trước + cách né; mục tiêu nằm bên kia thử thách, không lối tắt khi chưa được yêu cầu.
4. **Đồ trang trí theo cụm kể chuyện, kiến trúc đúng logic** (cửa đúng tường, khoá đúng cửa, vật đứng có chân chặn).
5. **Hoạt ảnh tự nhiên, không lố, không VFX thừa** (đặc biệt Ren áo tế Map 4).
6. **Ba kết** (The Face / Silence / Requiem) + **Seven Mercies** tuỳ chọn quyết định kết thật.

Cấu trúc truyện 5 map: HANDOFF mục 7 (bảng "Kẻ giữ / Ren muốn / Sự thật / Ren rời map với").

---

## 2. Đang ở đâu (18/11/2026)

| Phần | Trạng thái |
|---|---|
| Map 1 Greystone Plains + 1b Hermit's Hollow (Oathbreaker) | Chơi được. 1b chờ người dùng test xong. |
| Map 2 Undercity of Vael + 2b The Kroven Deep (Ironcrown, boss The Last Guard) | Chơi được. 2b + boss Last Guard: chưa có phản hồi test (§284–§294). |
| Map 3 Vale of Graves + 3b King's Barrow | Chơi được. Cuối Map 3: Absalom đánh lén -> Map 4. |
| **Map 4 The Catacombs of Naming** | **ĐANG LÀM.** Bản đồ + logic + bẫy + truyện + quái xong; đang sửa theo phản hồi chạy thử (§313–§321). Chưa có: boss The Registrar bản mới, boss Hall of Naming (Candidate VI + The Severing + Pell chết), khu bí mật Namesake. |
| Map 5 The Faint Lake | CHƯA LÀM (mới có thiết kế truyện). |
| Map bí mật 3b mở rộng / 3c / 3d / Namesake / Absolution | CHƯA LÀM (đã có ý tưởng duyệt — mục 4). |
| Âm thanh | Code xong (§294), file âm chưa tải về (cần ElevenLabs). |
| Giao diện đổi phím, cân bằng boss Map 2–3 | Chưa làm. |

---

## 3. Map 4 — phần còn lại (theo thứ tự)

1. **Đọc phản hồi chạy thử của §319–§321 rồi sửa** (checklist ở cuối mỗi mục NOTES). Các điểm còn nghi vấn:
   - Ren còn "đè cổng" ở cổng thật hay không. Về lý thuyết đã đúng: lintel order 2 + pivot chân tường, Renderer2D Custom Axis (0,1,0), DepthEffects không làm mờ cổng/lintel. Nếu còn thì xin ảnh + vị trí minimap.
   - Quái đứng im ngoài Pit Bat: kiểm zone start (`MapEncounterDirector.Stages.cs`), `stand_in`, leash.
   - Cửa Crypt (letters / vault / seal / hall / exit) và cổng sọ chưa có lintel. Cứ chỗ nào Ren đi qua khung cửa bằng ô tường thì tách lintel như `m4_fix_1402.py`.
2. **Boss The Registrar làm lại** (kế hoạch `_tools/m4fix_1118/MAP4_FIX_PLAN_1118.md` §5). **Chờ duyệt** bố cục 2 phòng bẫy, lượt bẫy, thoại.
   - Boss to gấp 3 lần Ren.
   - Pha 1–2: dịch chuyển Ren vào phòng bẫy "The Margin" / "The Index". Bẫy chạy theo lượt kiểu obby; boss mệt = cửa sổ đánh.
   - Pha 3: về Scriptorium, gọi quái + chưởng khó né.
   - Code mới: `TrialTeleport`, `TrapSequencer`, state machine `RegistrarSkills`.
   - Hiện tạm: moveSpeed 0.8 cho đỡ đứng im.
3. **Hall of Naming — boss cuối Map 4** (CHƯA thiết kế chi tiết; **chờ duyệt**):
   - Absalom xuất hiện lần đầu. Hình riêng: hiện chưa có, KHÔNG dùng GameMaster.
   - **Candidate VI** (vỏ rỗng mang mắt phải, dùng chính chiêu của Ren). Tha -> `candidate_vi_freed` (Mercy); giết -> rơi mắt phải.
   - **The Severing**: xé mọi bộ phận thánh khỏi Ren, gỡ các bộ Mantra cũ, trao **Mantras of the Name** (cần thiết kế 3 chiêu — bộ mạnh nhất).
   - **Pell chết** che chắn cho Ren.
   - Absalom mang tất cả đi: *"Only the heart remains. The King will bring it himself."*
   - Lối ra Map 5 mở sẵn.
   - Tranh cutscene `Resources/Story/angel_bird_1..4` đã dành cho Map 4. Ghi chép sự thật (sổ VII, thư mẹ, biên niên Luke / Darwin, lời tiên tri gốc, sổ kiểm kê) đã đặt.
4. **Khu bí mật Namesake** (katana thứ 8, tạm): thiết kế khu, lore, nội tại "Namebound". **Chờ duyệt.** Gắn với `name_reclaimed` (đọc hết sổ tên) càng tốt.
5. Hoàn thiện Map 4: cân bằng máu / sát thương quái mới; Skull Guard đỡ khiên; Cantor dịch chuyển; bake lại minimap nếu đổi bố cục; quest json theo bố cục cuối.
6. Thêm Map 4 vào Build All Map Scenes khi đã ổn (hiện KHÔNG — tránh dựng lại scene cũ).

---

## 4. Sau Map 4

### Map 5 — The Faint Lake (gói Volcano) — CHƯA LÀM
- Lộ trình: The Pilgrim Stair (giáo phái vs lính hoàng gia) -> Crypt of the Haloed (tha Isolde thì bà mở lối tắt) ->
  The Forge (Wall of Voices; hạ Absalom; Darwin giật tay Absalom, mang thân xác xuống hồ) -> trận cuối 3 pha:
  1. **The Haloed King** (kiếm sĩ).
  2. **The Remade God** — Darwin nhập thân xác đủ bộ phận. Dùng lại chiêu của từng bộ phận: mắt trái / Plague Eye, cột sống / Specimen Zero, tai / Morrow, mắt phải / Candidate VI, tay trái / Absalom, máu / Isolde.
  3. **The Return** — phá 3 neo, nói tên thật để lấy tim. Tha Absalom thì hắn giữ một neo.
- **Ba kết** (Ren cầm trái tim trên bãi hành hình):
  - The Face: luôn có.
  - Silence: luôn có.
  - **Requiem** (kết thật): cần `name_reclaimed` + ≥ 5/7 mercy.
- Cần làm hệ thống: đếm Seven Mercies xuyên map (GameSession + SaveGame), màn chọn kết, cutscene kết, credits.
- Khu bí mật **Absolution** (katana 9, Darwin thời tông đồ, nội tại "Penance" — tạm, chờ duyệt).

### Map / khu bí mật đã duyệt ý tưởng (chưa dựng) — xem `SOUL_REQUIEM_KATANAS.md` bảng "khu bí mật"
1. **Map 3b mở rộng + boss bí mật The Red Field** (hồn 600.000 người chết, Vael đánh thức) -> mở lăng Borrow -> **Crimson Oath**.
   Dời lối xuống 3b ra mép đầm Sinking Fields, khuất sau cây chết; bỏ gợi ý "ánh vàng". Viết lại side quest 3b theo manh mối.
2. **Map 3c Hall of Three Crowns** (dưới lăng Borrow): đặt đủ Oathbreaker + Crimson Oath + Ironcrown -> hầm Arthur mở -> **Arthur's Accord**.
3. **Map 3d The First Cup** (dưới hầm mộ nhà Vey, mở bằng Vey Signet): câu đố rót chén theo gia phả; tha Isolde thì bà để lại thứ tự. Thưởng **Eventide**.
Quy tắc khu bí mật: phần thưởng CHỈ tới được qua thử thách; side quest `kind side` theo manh mối; `fog off`; minimap bake; NOTES + md5 HANDOFF + checklist.

### Hệ thống / hoàn thiện toàn game
- **Âm thanh** (§294): code Sfx / SfxRunner / SfxHooks xong. Còn lại:
  - Chạy nốt các node của flow ElevenLabs `EVLxBJiWqsWWZXQEeRK9`.
  - Mở quyền tên miền `elevenlabsusercontent.com`.
  - Chạy `_tools/sfx/fetch_sfx.py` để lấy file về `Assets/Resources/Audio/SFX/`.
  - Chỉnh Gain / MinGap.
  - Sau đó: nhạc nền từng map.
- **Hoạt ảnh NPC 8 hướng** (Pell, Aldous, Goblin Child): hiện có E/N/S, W lật. Nâng cấp khi cần (MAP4_FIX_PLAN §2 cũ có cách làm).
- Giao diện đổi phím; cân bằng boss Map 2–3 (+ 2b Last Guard máu 620); Mantra "Canticle of Stillness" (thưởng 1b, chờ thiết kế).
- Dọn rác: `_to_delete/`, `Assets/_Recovery/`, `Resources/VFX/specimen/blue/`, nhóm thử PixelLab thừa — **chỉ khi người dùng bảo**.
- Build bản chơi thử (Build Settings đủ scene, Title -> Map 1 -> … -> Map 5), test toàn tuyến.

---

## 5. Quyết định còn chờ người dùng
- Giữ tên **Borrow** (vần với Morrow)? Giữ **Darwin** (hay Darven)?
- Bỏ nghỉ ở lửa trại / nến thật (hồi máu + bình)? Bỏ đốm lấp lánh trên đồ nhặt?
- Lấy Oathbreaker xong, đi ngược The Still Walk: bẫy vẫn bắn hay tắt?
- Đổi màu Mantras of the Eye theo Plague Eye? Chiêu tia mắt của Specimen Zero (nó đã mù)? Cơ chế Grace?
- Map 4: bố cục + thoại boss Registrar (§5); thiết kế Hall of Naming; Namesake.
- Xoá các file thừa liệt kê ở HANDOFF mục 10 "Quyết định còn chờ".

---

## 6. Chuyển sang tài khoản Claude mới — việc cần làm
1. Mở chat trong **app Claude desktop** (để liên kết máy), cấp quyền thư mục:
   - `C:\Users\Admin\Soul Requiem` (bắt buộc).
   - `C:\Users\Admin\Desktop\Catalog` (công cụ map cũ, minimap bake, trim_props).
   - `C:\Users\Admin\AppData\Local\Unity\Editor` (đọc `Editor.log` xem lỗi biên dịch).
2. Kết nối connector:
   - **PixelLab MCP**: tài khoản của người dùng, Claude không đăng nhập hộ.
   - **ElevenLabs** (nếu làm âm thanh).
3. Tin nhắn mở đầu: HANDOFF mục 1. Thứ tự đọc:
   1. `SOUL_REQUIEM_HANDOFF.md` (mục 10.0 trước).
   2. File này.
   3. `_tools/m4fix_1118/MAP4_FIX_PLAN_1118.md`.
   4. `SOUL_REQUIEM_NOTES.md` từ dưới lên (§309–§322).
   5. `ARCHITECTURE.md`.
   6. `SOUL_REQUIEM_KATANAS.md`, `HANDOFF_REN_KATANA.md` (khi đụng Ren / katana).
4. Project claude.ai "Soul Requiem" (nếu chuyển được): `claude/SOUL_REQUIEM_HANDOFF.md`, `claude/SOUL_REQUIEM_ROADMAP.md`,
   `claude/map4_fix_plan_1118.md`, `claude/lore_ban_chot_16-11.md`… Nguồn sự thật vẫn là file trong thư mục project trên máy.
   Link claude.ai/artifact trong NOTES thuộc tài khoản cũ, tài khoản mới không mở được.
5. Tài sản PixelLab đang dùng (ID để tìm lại):
   - Ren áo tế: `1b0f7997` (state "Ren Offering Robe").
   - Pell: `66154674-dfd5-4c92-aac1-798ba653e77f`.
   - The Last Guard: `a3d65a38-4d9a-4bd4-b316-43c042a73159`, Last Stand `c6c3511e-2105-42a4-b622-9fbd45f537c9`.
   - Style chung: Lantern Knight `49d0004b`.
   - Nhóm thừa chưa xoá: lg3_charge_north_fix_a/b/d, lg4_*, ls4_*, ls5_*, walk Pell `ffcbf09f`.
6. Trước khi sửa bất kỳ .tmx nào: so md5 với HANDOFF mục 3 (người dùng hay sửa tay trong Tiled).
