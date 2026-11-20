# Map 4 — trang trí NGỤC (Z1 Oubliette, Z2 Cell Rows + phòng tra tấn + phòng lính gác, Z3 Blood Pit) theo CỤM KỂ CHUYỆN.
# Toạ độ ô; put(..., cx, by) = chân giữa-dưới. Bẫy ở TRAPS chỉ để vẽ xem trước (cơ chế thật đặt ở bước 3).
from m4_setup import objs as O, AID, o as OP
T = 32
BL, WARM = '#7fa8ff', '#ffb070'
def P(tid, cx, by, g='props', flip=False, block=None, name=None):
    O.put(g, 'op', tid, cx, by, flip=flip, name=name)
    if block:                                   # block = (rộng, sâu) ô, tính từ chân
        bw, bd = block; O.block(cx - bw / 2, by - bd, bw, bd)
def bone(n, cx, by, g='ground decals'): P(244 + n, cx, by, g)
def stone(n, cx, by): P(676 + n, cx, by, 'ground decals')
def paper(n, cx, by): P(442 + n, cx, by, 'ground decals')
def blood(cx, cy, kind=0):                      # vết máu 2x2 ô (ô địa hình Old Prison)
    c0 = (35, 38)[kind]
    for dy in (0, 1):
        for dx in (0, 1):
            O.obj('ground decals', None, None, (cx + dx) * T, (cy + dy + 1) * T, 32, 32, gid=OP.ter.firstgid + (12 + dy) * 65 + c0 + dx)
def flame_stand(cx, by, name):                  # giá sắt + lửa ma xanh (mockup Old Prison)
    P(666, cx, by, block=(0.6, 0.4))
    O.put('props', 'flame', 0, cx, by - 1.45)
    O.light(name, cx, by - 1.6, BL, 3.2, 0.85, 'flicker', 0.18, 0.9)
def flame_wall(cx, face_bottom, name):          # giá gắn tường + lửa xanh trên mặt tường
    O.put('wall props', 'op', 674, cx, face_bottom - 0.55)
    O.put('wall props', 'flame', 0, cx, face_bottom - 0.9)
    O.light(name, cx, face_bottom - 0.4, BL, 2.8, 0.8, 'flicker', 0.18, 0.9)
def candle(cx, by, name, tid=370):
    P(tid, cx, by); O.light(name, cx, by - 0.3, WARM, 1.8, 0.6, 'flicker', 0.12, 0.7)
def candelabrum(cx, by, name):
    P(344, cx, by, block=(0.6, 0.4)); O.light(name, cx, by - 1.4, WARM, 2.6, 0.7, 'flicker', 0.12, 0.7)
def art_tl(name, a, x_px, y_top_px, group='gates', typ='gate_visual'):
    w, h = O.ts['art'].sizes[AID[a]]
    O.obj(group, name, typ, x_px, y_top_px + h, w, h, gid=O.gid('art', AID[a]))
def cellgate(name, gx, top, a='op_cellgate1'):  # cổng xà lim: mép trên dầm (y63 trong khung) = mép trên tường
    art_tl(name, a, gx * T, top * T - 63)
TRAPS = []                                      # (kiểu, x, y, w, h) ô — chỉ để vẽ xem trước

# ============================================================ Z1 THE OUBLIETTE — Ren tỉnh dậy trong xà lim
# xà lim Ren: trong x104..110, hàng 9..11; tường nam 12..14, khe cổng 105..108
P(374, 105.5, 9.05, 'wall props'); P(375, 109.5, 9.05, 'wall props')          # còng xích trống trên vách (của Ren)
bone(8, 110.2, 11.8); P(335, 104.6, 11.9, block=(0.5, 0.3)); stone(3, 107.8, 9.9)
cellgate('ren_gate', 105, 12)
art_tl('ren_winch', 'op_gear', 109.2 * T, 13.95 * T, 'mechanics_art', None)   # tời bánh răng ngay ngoài cột phải (thò tay qua song)
# góc cai ngục: bàn, 2 ghế, nến, giấy, cái đĩa — cai ngục đi vắng
P(785, 100.0, 17.7, block=(3, 1.2)); P(793, 98.1, 17.6); P(799, 102.0, 17.6, flip=True)
candle(99.4, 16.95, 'oub_candle', 371); paper(3, 100.6, 16.9); paper(9, 101.5, 18.6)
P(16, 114.6, 7.4, block=(0.8, 0.4)); P(17, 115.5, 7.9, block=(0.8, 0.4)); P(753, 113.2, 7.9, block=(1.6, 0.4))
P(765, 113.6, 17.4, 'props front')                                             # lồng treo trống
flame_stand(101.6, 9.2, 'oub_flame_w'); flame_stand(112.8, 12.6, 'oub_flame_e')
P(606, 99.0, 4.9, 'wall props'); P(607, 115.0, 4.9, 'wall props')
P(386, 98.5, 12.6, 'ground decals'); bone(17, 103.0, 16.4); blood(108, 15.6)
# cửa gỗ dọc ở cột tây (khe hàng 14..16): x = cột*32-1, đáy khung = mép dưới khe + 9 px
art_tl('oub_door', 'op_door_v', 96 * T - 1, 17 * T + 9 - 128)

# ============================================================ Z2 THE CELL ROWS — hành lang = làn gỗ lăn hàng 13..17 (để TRỐNG đồ trên sàn)
for x, kind in [(50, 'open'), (57, 'shut'), (64, 'open'), (71, 'pell'), (78, 'open'), (85, 'shut')]:
    gx = x + 2
    if kind == 'open':                          # cổng gỉ đã kéo lên -> chỗ núp khi gỗ lăn
        cellgate('cell_%d_gate' % x, gx, 10, 'op_cellgate1_open')
        P(282 + (x % 3), x + 4.0, 7.05, 'wall props')          # xác bị xích trên vách
        P(336, x + 6.2, 9.8, block=(0.5, 0.3)); bone(23, x + 1.8, 9.7)
    elif kind == 'shut':
        cellgate('cell_%d_gate' % x, gx, 10, 'op_cellgate2')
        P(249, x + 3.0, 8.9); P(433, x + 5.6, 9.6); bone(4, x + 4.4, 9.8); P(384, x + 2.2, 9.4, 'ground decals')
    else:                                       # xà lim Pell: nến, giấy viết đầy, bát nước
        cellgate('pell_gate', gx, 10, 'op_cellgate1')
        candle(x + 1.7, 9.6, 'pell_candle', 372); paper(1, x + 3.2, 8.6); paper(5, x + 4.1, 9.1); paper(12, x + 5.0, 8.4)
        P(338, x + 6.1, 9.7)
    if x != 85: flame_wall(x + 7.0, 13.0, 'cells_flame_%d' % x)                # lửa xanh trên cột giữa 2 xà lim, mặt hướng hành lang
for (dx, n) in ((48, 21), (61, 18), (75, 26), (90, 29)): bone(n, dx, 15.2)
for cx in (53, 68, 83): P(387 + cx % 4, cx, 16.6, 'ground decals')
blood(60, 15); blood(88, 13, 1)
TRAPS.append(('roller', 47, 13, 49, 5)); TRAPS.append(('hatch', 92, 13, 4, 2))
# phòng tra tấn: trong x51..63, hàng 21..26; vòm mở x56..58 (cửa giữa x57)
P(833, 52.4, 23.3, block=(1.8, 0.9)); P(835, 55.0, 23.3, block=(2.6, 0.9))           # iron maiden dựa vách bắc
P(839, 62.0, 23.4, block=(1.8, 0.8))                                                  # máy chém góc đông
P(843, 56.6, 25.6, block=(1.8, 0.8)); P(845, 59.6, 26.4, block=(1.8, 0.6))            # giá kéo / cùm chân
P(337, 52.0, 26.6); bone(39, 54.2, 26.9); bone(6, 53.0, 25.0); blood(56, 24); blood(59, 22, 1)
P(404, 57.4, 24.0, 'props front'); P(376, 51.6, 21.05, 'wall props'); P(377, 63.2, 21.05, 'wall props')
P(380, 61.0, 21.0, 'wall props', name='key_hook')                                     # móc treo chìa cai ngục (bước 3: nhặt chìa)
candelabrum(51.6, 26.8, 'tor_cand_w'); candelabrum(63.3, 26.8, 'tor_cand_e')
# phòng lính gác: trong x71..87, hàng 21..27; cửa gỗ ở vòm x78..80
art_tl('guard_door', 'op_door_h', 78 * T, 21 * T + 18 - 128)
P(577, 75.0, 20.95, 'wall props'); P(606, 73.6, 20.4, 'wall props'); P(606, 76.4, 20.4, 'wall props')   # chân dung nhà vua giữa 2 cờ
flame_wall(83.0, 21.0, 'guard_flame')
P(785, 78.6, 24.9, block=(3, 1.1)); P(793, 76.7, 24.8); P(799, 80.6, 24.8, flip=True); P(806, 78.6, 23.6)  # bàn trực + ghế
candle(78.0, 24.15, 'guard_candle', 371); paper(7, 79.2, 24.1); paper(15, 77.4, 26.2)
P(787, 84.6, 25.9, block=(3, 1.0)); P(795, 82.8, 25.8); paper(21, 84.0, 25.3)       # bàn phụ (giấy tờ sổ trực)
P(16, 86.6, 22.3, block=(0.8, 0.4)); P(17, 86.6, 23.5, block=(0.8, 0.4)); P(18, 85.5, 22.5, block=(0.8, 0.4))
P(756, 86.4, 26.8, block=(1.6, 0.4)); P(409, 72.6, 26.9, block=(1.8, 0.6))
art_tl('cells_winch', 'op_gear', 71.0 * T, 21.1 * T, 'mechanics_art', None)          # tời mở xà lim (dãy bắc) — gắn vách tây
candelabrum(86.9, 26.8, 'guard_cand')

# ============================================================ Z3 THE BLOOD PIT
# bến bắc (x36..46, hàng 10..20): gỗ lăn rơi khỏi mép tây xuống hồ máu
for (cx, by, t) in ((35.3, 12.4, 541), (34.8, 15.6, 545), (35.6, 22.8, 549), (23.0, 17.0, 543), (17.2, 23.5, 547), (30.5, 24.2, 551), (26.4, 12.6, 542)):
    P(t, cx, by)                                                                 # giáo nhọn cắm trong hồ máu
flame_stand(37.4, 17.4, 'pit_flame_n'); P(383, 41.5, 19.6, 'ground decals'); bone(12, 44.2, 11.3)
art_tl('pit_winch', 'op_gear', 37.0 * T, 19.6 * T, 'mechanics_art', None)            # tời hạ cầu treo (bờ gần)
art_tl('pit_bridge', 'op_bridge_h', 20 * T, 18 * T - 59 + 0, 'mechanics_art', None)  # cầu treo (khe x20..23)
# mỏm tây (x3..14, hàng 8..32): sân bẫy lưỡi xoay — 3 ray ngang, gai sàn giữa
TRAPS += [('blades', 4, 21, 10, 1), ('blades', 4, 25, 10, 1), ('blades', 4, 28, 10, 1), ('spikes', 6, 23, 6, 1)]
P(249, 5.0, 10.6); bone(40, 6.6, 11.4); P(386, 7.8, 11.0, 'ground decals')        # kẻ vượt ngục trước — ngồi chết ở góc
paper(25, 6.2, 10.9)
bone(30, 11.6, 22.6); bone(11, 4.6, 26.2); stone(30, 12.5, 30.1); stone(22, 3.8, 14.4); blood(9, 19, 1)
flame_stand(13.2, 17.4, 'pit_flame_w'); flame_stand(13.6, 30.6, 'pit_flame_sw')
# cầu về (x15..39, hàng 30..31) + bến nam (x40..51, hàng 29..43) + cổng xuống hầm mộ (khe x44..47, tường hàng 37..39)
for (cx, by, t) in ((20.5, 29.2, 544), (28.0, 34.6, 550), (34.5, 28.9, 546), (12.0, 37.5, 552)): P(t, cx, by)
cellgate('descent_gate', 44, 37, 'op_cellgate1')
flame_stand(41.6, 36.6, 'gate_flame_w'); flame_stand(47.8, 36.6, 'gate_flame_e')
bone(20, 47.4, 31.5); stone(5, 41.2, 33.0); P(389, 45.0, 34.5, 'ground decals')
# gờ xa (x6..39, hàng 1..3): đường đoàn Black Lantern đi qua — giá lửa soi đường, nhìn thấy từ bên kia hố
for i, cx in enumerate((10, 18, 26, 34)): flame_stand(cx, 2.9, 'ledge_flame_%d' % i)
