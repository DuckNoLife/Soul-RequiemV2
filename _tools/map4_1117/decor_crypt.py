# Map 4 — trang trí HẦM MỘ (Z4 Ossuary, Z5 Scriptorium + phòng thư, Z6 Reliquary + kho, Z7 Walk + sảnh chờ, Z8 Hall, Z9 Descent).
# Phong cách theo Sample Map của tác giả: bình gốm + nến thành cụm ở chân vách, xương rải theo cụm, cờ trên mặt gạch, quan tài xếp hàng,
# tượng trên bệ, bục sách. Bẫy ở TRAPS chỉ để vẽ xem trước (cơ chế thật ở bước 3).
from m4_setup import objs as O, AID
T = 32
WARM, GREEN, COLD = '#ffb070', '#9fe0a0', '#b8d8ff'
def C(tid, cx, by, g='props', flip=False, block=None, name=None):
    O.put(g, 'cr', tid, cx, by, flip=flip, name=name)
    if block:
        bw, bd = block; O.block(cx - bw / 2, by - bd, bw, bd)
def banner(cx, room_top, tid=0): C(tid, cx, room_top - 0.15, 'wall props')            # cờ dài treo trên mặt gạch
def torch(cx, room_top, name):
    O.put('wall props', 'torch', 0, cx, room_top - 0.85); O.light(name, cx, room_top - 0.6, GREEN, 3.2, 0.75, 'flicker', 0.2, 0.9)   # đuốc Crypt lửa xanh lá
def vases(cx, by, n=3, s=0):
    pal = [203, 205, 207, 236, 238, 240, 210, 242]
    for i in range(n): C(pal[(s + i) % len(pal)], cx + (i - (n - 1) / 2) * 0.62, by + (0.18 if i % 2 else 0), block=(0.5, 0.3) if i == n // 2 else None)
def candles(cx, by, name, n=3):
    for i in range(n): C(119 + (i % 4), cx + (i - (n - 1) / 2) * 0.45, by + (0.15 if i % 2 else 0))
    O.light(name, cx, by - 0.3, WARM, 2.2, 0.65, 'flicker', 0.15, 0.8)
def candelabrum(cx, by, name, tid=115):
    C(tid, cx, by, block=(0.6, 0.4)); O.light(name, cx, by - 1.4, WARM, 2.8, 0.75, 'flicker', 0.12, 0.7)
def bones(cx, by, ids=(41, 47, 52, 60)):
    for i, t in enumerate(ids): C(t, cx + (i - 1.5) * 0.7, by + (i % 2) * 0.35, 'ground decals')
def art_tl(name, a, x_px, y_top_px, group='gates', typ='gate_visual'):
    w, h = O.ts['art'].sizes[AID[a]]
    O.obj(group, name, typ, x_px, y_top_px + h, w, h, gid=O.gid('art', AID[a]))
TRAPS = []

# ============================================================ Z4 THE OSSUARY — sảnh trung tâm (x45..82, hàng 48..67), điểm nghỉ
# bệ thờ bắc (x56..71, hàng 48..52): tượng giữa, chân nến hai bên, bình gốm ở góc; cờ đỏ giáo phái trên mặt gạch
C(272, 63.5, 50.9, block=(1.6, 0.8)); candelabrum(60.0, 51.9, 'oss_alt_w'); candelabrum(67.0, 51.9, 'oss_alt_e')
vases(57.6, 49.6, 3, 0); vases(70.4, 49.6, 3, 3); candles(63.5, 52.6, 'oss_alt_c', 4)
banner(57.0, 48, 0); banner(70.0, 48, 1); banner(61.0, 48, 18); banner(66.0, 48, 18)
# dãy quan tài đứng hai cánh sảnh (kho hài cốt) — chừa lối giữa 6 ô
for i, cx in enumerate((47.0, 48.6, 50.2, 51.8, 53.4)):
    C(128 + i % 6, cx, 58.6, block=(1.0, 0.6)); C(128 + (i + 2) % 6, cx, 64.6, block=(1.0, 0.6))
for i, cx in enumerate((73.6, 75.2, 76.8, 78.4, 80.0)):
    C(128 + (i + 3) % 6, cx, 58.6, block=(1.0, 0.6)); C(128 + (i + 1) % 6, cx, 64.6, block=(1.0, 0.6))
bones(50.0, 60.6); bones(77.0, 61.2, (44, 49, 63, 70)); bones(48.0, 66.4, (43, 55, 58, 66)); bones(79.6, 66.4, (42, 51, 61, 68))
vases(49.6, 49.4, 2, 1); vases(81.2, 49.4, 2, 5); torch(50.5, 48, 'oss_torch_w'); torch(80.5, 48, 'oss_torch_e'); banner(53.0, 48, 2)
# điểm nghỉ: ghế dài + nến dưới chân bậc thờ
C(37, 58.6, 56.6, block=(2.0, 0.5)); C(37, 68.4, 56.6, block=(2.0, 0.5)); candles(56.8, 56.0, 'oss_rest_w', 2); candles(70.2, 56.0, 'oss_rest_e', 2)
vases(60.0, 67.2, 2, 2); vases(67.0, 67.2, 2, 6)                                     # hai bên vòm xuống Penitent's Walk

# ============================================================ Z5 SCRIPTORIUM OF NAMES (x4..37, hàng 48..69) — nơi chép Sổ Tên
# bệ tây bắc (x4..15, hàng 48..52): bục Sổ Tên + Ấn 1 (The Registrar đứng đây)
C(112, 9.5, 50.4, block=(1.0, 0.5)); C(111, 9.5, 49.75, 'props front')
C(174, 13.4, 50.8, block=(1.2, 0.6), name='seal1_pedestal'); candelabrum(5.4, 51.6, 'scr_alt_w'); candelabrum(14.6, 52.0, 'scr_alt_e')
banner(6.0, 48, 3); banner(13.0, 48, 4)
# 4 dãy bàn chép (bục sách + ghế dài) — lối giữa x24..26 và hai mép
for r, by in enumerate((58.5, 61.5, 64.5, 67.5)):
    for cx in (19.5, 31.0):
        for j in range(3):
            C(112 + (j + r) % 3, cx + (j - 1) * 1.6, by - 0.6, block=(1.0, 0.45))
            C(109 + (j + r) % 3, cx + (j - 1) * 1.6, by - 1.2, 'props front')
        C(37, cx, by + 0.35, block=(2.6, 0.4))
for x in (6, 11, 16, 21, 26, 31, 36): pass
TRAPS += [('ice', 17, 46, 1, 1), ('ice', 23, 46, 1, 1), ('ice', 29, 46, 1, 1), ('ice', 35, 46, 1, 1)]   # hốc phun băng trên mặt gạch bắc (bắn xuống)
for cx in (17.0, 23.0, 29.0, 35.0): C(193, cx + 0.5, 47.7, 'wall props')            # lỗ phun trên vách
C(180, 21.0, 47.95, 'wall props'); C(181, 33.0, 47.95, 'wall props')                 # rèm treo
vases(36.4, 57.0, 2, 4); vases(36.4, 68.8, 3, 1); vases(5.2, 68.8, 3, 2); candles(5.0, 60.0, 'scr_c1', 3); candles(36.6, 62.0, 'scr_c2', 3)
bones(10.0, 64.0); C(57, 9.0, 66.4, 'ground decals'); torch(19.5, 48, 'scr_torch_1'); torch(31.5, 48, 'scr_torch_2')
# cửa xuống phòng thư của mẹ: doors2 hướng xuống (tường nam, lối x10..12)
art_tl('letters_door_frame', 'cr_door2_down_frame', 10 * T, 70 * T, 'props', None); art_tl('letters_door', 'cr_door2_down', 10 * T, 70 * T)
# PHÒNG THƯ CỦA MẸ (x4..19, hàng 74..85): nhà nguyện nhỏ yên tĩnh — bàn thư, nến, tượng người mẹ ôm con, rèm
C(167, 11.5, 76.8, block=(1.4, 0.7)); candles(9.6, 77.4, 'let_c1', 3); candles(13.4, 77.4, 'let_c2', 3)
C(112, 11.5, 80.6, block=(1.0, 0.5)); C(111, 11.5, 79.95, 'props front', name='mother_letters')
C(37, 11.5, 82.4, block=(2.6, 0.4)); C(182, 6.0, 73.95, 'wall props'); C(182, 17.0, 73.95, 'wall props')
vases(5.0, 85.4, 2, 0); vases(18.4, 85.4, 2, 3); C(110, 15.6, 84.2, 'ground decals'); C(109, 7.0, 83.6, 'ground decals')

# ============================================================ Z6 THE ROYAL RELIQUARY (x90..115, hàng 48..69) — hộp thánh tích TRỐNG
# bệ đông bắc (x104..115, hàng 48..52): Ấn 2, Lantern Knight canh
C(175, 110.5, 50.8, block=(1.2, 0.6), name='seal2_pedestal'); candelabrum(105.4, 52.0, 'rel_alt_w'); candelabrum(114.6, 51.6, 'rel_alt_e')
banner(106.0, 48, 9); banner(113.0, 48, 10)                                          # cờ vàng hoàng gia
# 3 hàng bệ thánh tích rỗng (mỗi bệ một tấm áp lực -> tên bắn từ vách tây)
for r, by in enumerate((58.0, 62.0, 66.0)):
    for cx in (94.0, 98.0, 102.0, 106.0, 110.0):
        C(172 + (r + int(cx)) % 2, cx, by, block=(1.2, 0.6)); TRAPS.append(('plate', int(cx) - 1, int(by), 2, 1))
    TRAPS.append(('launcher', 90, int(by) - 1, 1, 1))
C(176, 99.0, 47.95, 'wall props'); C(178, 95.0, 47.95, 'wall props'); torch(92.0, 48, 'rel_torch_1'); torch(100.5, 48, 'rel_torch_2')
vases(114.6, 57.0, 2, 1); vases(114.6, 68.8, 3, 4); vases(91.0, 68.8, 3, 0); candles(103.0, 69.0, 'rel_c', 3)
C(185, 90.4, 61.4, 'wall props')
# cửa kho kiểm kê: doors2 hướng xuống (lối x106..108) + sàn sập trước cửa
art_tl('vault_door_frame', 'cr_door2_down_frame', 106 * T, 70 * T, 'props', None); art_tl('vault_door', 'cr_door2_down', 106 * T, 70 * T)
TRAPS.append(('pit', 105, 67, 5, 2))
# KHO KIỂM KÊ (x100..116, hàng 74..80): rương sắt mở toang, bục sổ kiểm kê (Darwin: "Let them carry it.")
O.put('props', 'op', 675, 102.6, 77.0); O.put('props', 'op', 676, 105.2, 77.0); O.put('props', 'op', 409, 113.6, 76.8); O.put('props', 'op', 410, 115.4, 79.6)
C(112, 109.0, 78.6, block=(1.0, 0.5)); C(111, 109.0, 77.95, 'props front', name='inventory_book'); candles(107.2, 79.6, 'vault_c', 2)
C(177, 104.0, 73.95, 'wall props'); C(179, 112.0, 73.95, 'wall props')

# ============================================================ Z7 THE PENITENT'S WALK (x57..69, hàng 72..95) + SẢNH CHỜ (x57..88, hàng 96..100)
TRAPS += [('spikes', 58, 74, 11, 1), ('spikes', 58, 76, 11, 1), ('spikes', 58, 78, 11, 1),        # sóng gai 3 dải
          ('saw', 58, 81, 11, 1), ('saw', 58, 85, 11, 1),                                       # cưa chạy trong rãnh sàn
          ('launcher', 57, 88, 1, 1), ('launcher', 69, 90, 1, 1), ('launcher', 57, 92, 1, 1),   # tên bắn ngang từ hốc vách hông
          ('plate', 61, 89, 2, 1), ('plate', 65, 91, 2, 1),
          ('fire', 66, 93, 1, 1), ('fire', 70, 93, 1, 1), ('fire', 74, 93, 1, 1), ('fire', 84, 93, 1, 1)]  # tượng mặt phun lửa (mặt gạch bắc sảnh chờ)
for y in (81.5, 85.5): C(194, 63.5, y + 0.5, 'ground decals')                         # rãnh cưa
for cx in (66.5, 70.5, 74.5, 84.5): C(189 + int(cx) % 2, cx, 95.9, 'wall props')     # tượng mặt trên vách
bones(59.4, 80.0, (45, 53, 62)); bones(67.6, 87.2, (40, 56, 71)); bones(60.0, 95.0, (48, 59, 67)); C(77, 64.0, 83.4, 'ground decals')
candles(58.2, 72.6, 'walk_c1', 2); candles(68.6, 72.6, 'walk_c2', 2)
torch(59.5, 72, 'walk_torch_w'); torch(67.5, 72, 'walk_torch_e')
# cửa boss 3x3 (mép trên = mép trên mặt gạch hàng 93) + 2 ổ ấn hai bên
art_tl('hall_door', 'cr_bossdoor', 78 * T, 93 * T)
C(173, 76.4, 97.6, block=(1.2, 0.6), name='seal_socket_1'); C(173, 81.6, 97.6, block=(1.2, 0.6), name='seal_socket_2')
candelabrum(75.0, 97.0, 'ante_c1'); candelabrum(83.0, 97.0, 'ante_c2'); vases(87.4, 100.6, 3, 2); vases(58.6, 100.6, 2, 5)

# ============================================================ Z8 HALL OF NAMING (x72..97, hàng 72..89) — sân đấu boss (đợt sau): giữ giữa trống
C(270, 87.0, 74.6, block=(1.6, 0.8)); candelabrum(80.0, 75.6, 'hall_alt_w'); candelabrum(95.0, 75.6, 'hall_alt_e')
for cx in (73.6, 96.4):
    for by in (79.0, 83.5, 88.0): C(268 + int(by) % 2, cx, by, block=(1.4, 0.7))     # hàng tượng thánh dọc hai vách
banner(79.0, 72, 0); banner(95.0, 72, 1); C(176, 87.0, 71.95, 'wall props')
vases(75.6, 89.6, 3, 0); vases(94.4, 89.6, 3, 3)

# ============================================================ Z9 THE DESCENT (x102..115, hàng 87..100) — bậc đi xuống (lối sang Map 5)
art_tl('descent_stairs', 'cr_exit_stairs', 105 * T, 92 * T, 'props', None)
bones(103.4, 99.0, (46, 54, 64, 69)); C(80, 113.0, 90.0, 'ground decals'); candles(112.6, 99.6, 'desc_c', 2); torch(108.5, 87, 'desc_torch')
