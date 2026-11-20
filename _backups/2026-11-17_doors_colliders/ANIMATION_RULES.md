# Soul Requiem — Quy tắc động tác nhân vật nhiều hướng (bắt buộc đọc trước khi tạo / nhập animation)

Lập 17/11 (§292) sau lỗi The Last Guard: Charge hướng North khiên lệch ra sau lưng (§290), giáo chĩa NGANG khi nhìn
lên/xuống (Aim/Stuck/Pull N+S, Idle_S, LS_Thrust_S), thân xoay mặt ra camera khi đâm sang phải (LS_Thrust E).
Người dùng yêu cầu: không lặp lại các động tác sai logic / sai thực tế.

## 1. Quy ước hướng (top-down, low view)
| Hướng | Nhân vật nhìn | Tay PHẢI của nhân vật nằm ở | Tay TRÁI nằm ở |
|---|---|---|---|
| E (gốc, lật X cho W) | sang phải, thân nghiêng / profile | phía trước-phải | phía sau (gần camera) |
| S (`_S`) | xuống, MẶT hướng camera | bên TRÁI màn hình | bên PHẢI màn hình |
| N (`_N`) | lên, LƯNG hướng camera | bên PHẢI màn hình | bên TRÁI màn hình |

`EnemyAI` không lật ảnh _S/_N, nên tay cầm vũ khí phải đúng như bảng trên ở mọi khung.

## 2. Luật động tác (kiểm TỪNG khung)
1. **Vũ khí chĩa THEO hướng nhìn.** E: sang phải. S: về phía camera (chéo xuống trước thân, hoặc rút ngắn mũi chĩa xuống
   đáy hình). N: ra xa camera (mũi nhô trên vai / đỉnh mũ, hoặc khuất sau thân). Clip S/N **không bao giờ** có giáo/kiếm
   nằm ngang chĩa sang trái/phải. Đây là lỗi PixelLab hay gặp nhất.
2. **Khiên ở tay trái, che TRƯỚC thân khi thủ / lao / đẩy.** N: khiên khuất sau lưng, chỉ lộ mép quanh vai/mũ (KHÔNG thò
   ra ngoài thân về phía sau). S: che trước thân, mặt khiên hướng camera. E: ở phía trước bên phải.
3. **Thân không đổi hướng giữa clip.** Đâm sang phải thì thân giữ nghiêng phải từ đầu đến cuối; không xoay mặt ra camera.
4. **Đúng vật lý.** Lao / đâm: đổ người theo hướng ra đòn, chân bước. Giáo cắm sàn: mũi ở PHÍA TRƯỚC theo hướng nhìn
   (N: bị thân che; S: trước mũi chân). Rút giáo: đi ngược về tư thế thủ. Đẩy khiên: gồng -> đẩy tới -> thu.
5. **Khung đầu của động tác = tư thế đang đứng ngay trước nó** (Idle -> Aim; Aim cuối -> Stuck đầu; Stuck cuối -> Pull đầu
   -> Idle), để không giật khi chuyển state.
6. **Khớp thời gian với code.** Đọc chú thích ở `*Skills.cs` (vd LastGuardSkills: Thrust pha 2 "khung 0..3 rút, 4..5 đâm";
   Aim 9 khung ~ thrustWarn) và đặt khung ra đòn đúng chỉ số đó.
7. **Đối chiếu hướng E** (thường đúng nhất) và giữ cùng cỡ thân / điểm chân với các dải cùng nhân vật.

## 3. Quy trình bắt buộc
1. Tạo bằng PixelLab -> `pixelart_workbench inspect` xem từng khung của CẢ 3 hướng.
2. Dựng sheet so sánh mọi dải của nhân vật (mẫu: `_tools/kroven_2b/prev/north_all.png`, `south_all.png`, `east_all.png`)
   rồi soát theo mục 2. Có khung sai thì KHÔNG nhập.
3. Cách sửa (theo thứ tự ưu tiên):
   - Chọn lọc khung đúng từ một hoặc nhiều clip rồi ghép theo chỉ số (`_tools/kroven_2b/build_lg_fixes.py`: neo bóng đổ
     theo khung 0 của từng clip nguồn nên ghép khác clip vẫn đứng cùng điểm chân).
   - Tạo lại với `custom_start_frame_url` = một khung ĐÚNG (giữ tư thế khiên / giáo), mô tả động tác theo hướng nhìn.
   - Thử `mode: pixminimax` (giữ thân nhất quán tốt hơn khi đâm ngang).
4. Mẹo PixelLab: v3 thường vẽ giáo chĩa vào / ra camera thành NGANG. Viết rõ "never sideways"; nếu vẫn sai thì dùng quy
   ước chéo (S: chéo xuống trước thân; N: mũi nhô trên vai phải / khuất sau thân) và chọn lọc khung.
5. Không tải được backblaze / api.pixellab (proxy 403) -> lấy pixel qua ảnh xem trước 1:1 của
   `pixelart_workbench join <clip> --as sheet --columns C`. Ảnh có dải nhãn trên cùng; màu nền không thuộc bảng màu sprite
   (kiểm: số màu = bảng màu + 1) nên khoá nền theo màu.
6. Sau khi ghi: so pixel trên máy (md5 của `tobytes()`), ghi NOTES + HANDOFF, chặn importer cũ ghi đè (`FIXED` /
   `CLIP_OVERRIDE` trong `import_last_guard.py`).
