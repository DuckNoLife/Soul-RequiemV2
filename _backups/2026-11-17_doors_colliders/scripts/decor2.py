# Map 4 v2 — trang trí theo MẬT ĐỘ và KIỂU CỤM của ảnh mẫu (cr_11/13/15/16/17, op_37/38/39/43/44). Toạ độ ô; chân = giữa-dưới.
import random
from m4_setup import objs as O, AID, o as OP, k as CR
import gen_frame2 as F, doors as D
T = 32; R = random.Random(17)
BL, WARM, GREEN = '#7fa8ff', '#ffb070', '#9fe0a0'
TRAPS = []
# ------------------------------------------------------------------ tiện ích
def P(tid, cx, by, g='props', flip=False, block=None, name=None):            # đồ Old Prison
    O.put(g, 'op', tid, cx, by, flip=flip, name=name)
    if block: O.block(cx - block[0] / 2, by - block[1], block[0], block[1])
def C(tid, cx, by, g='props', flip=False, block=None, name=None):            # đồ Crypt
    O.put(g, 'cr', tid, cx, by, flip=flip, name=name)
    if block: O.block(cx - block[0] / 2, by - block[1], block[0], block[1])
def art(name, a, px, py, grp='gates', typ='gate_visual'):
    w, h = O.ts['art'].sizes[AID[a]]; O.obj(grp, name, typ, px, py + h, w, h, gid=O.gid('art', AID[a]))
def light(name, cx, cy, col, r=2.6, i=0.75): O.light(name, cx, cy, col, r, i, 'flicker', 0.15, 0.8)
_n = [0]
def free(x, y):
    """Ô dưới chân (x, y toạ độ ô) đứng được: sàn hầm mộ không bị chặn, hoặc sàn bệ ngục ngoài tường mỏng."""
    t = (int(x), int(y - 0.1))
    if t in CR.area: return t in CR.floor and t not in CR.blocked and CR.L['rails'][t[1]][t[0]] == 0
    M = OP.Mplat; X, Y = t
    return t in OP.area and t not in OP.cell_block and M[Y][X] and M[Y][X + 1] and M[Y + 1][X] and M[Y + 1][X + 1]
def nm(p): _n[0] += 1; return '%s_%d' % (p, _n[0])
# Crypt: cụm chân vách (bình + nến + xương) dày như mẫu
VASES = [203, 204, 205, 206, 207, 208, 210, 235, 236, 237, 238, 240, 241, 243]
BONES = list(range(41, 85))
def wallbase(x0, x1, row, dens=0.55, candles=True, bones=True):
    """Dải đồ sát chân vách bắc (row = hàng sàn đầu tiên)."""
    x = x0
    while x <= x1:
        r = R.random()
        if not free(x, row + 1.45) or not free(x, row + 0.7): x += 0.6; continue
        if r < dens * 0.45: C(R.choice(VASES), x + R.uniform(-0.2, 0.2), row + 0.75 + R.uniform(0, 0.25))
        elif r < dens * 0.7 and candles:
            C(119 + R.randrange(4), x, row + 0.7 + R.uniform(0, 0.3))
        elif r < dens and bones: C(R.choice(BONES), x, row + 1.0 + R.uniform(0, 0.4), 'ground decals')
        x += R.uniform(0.45, 0.8)
def heap(cx, cy, n=8, spread=1.2, vases=True):
    """Đống xương + bình + nến như góc mẫu."""
    for _ in range(n):
        dx, dy = R.uniform(-spread, spread), R.uniform(-spread * 0.6, spread * 0.6)
        if not free(cx + dx, cy + dy + 0.3): continue
        r = R.random()
        if r < 0.5: C(R.choice(BONES), cx + dx, cy + dy, 'ground decals')
        elif r < 0.8 and vases: C(R.choice(VASES), cx + dx, cy + dy + 0.3)
        else: C(119 + R.randrange(4), cx + dx, cy + dy + 0.3)
def candles(cx, by, n=3, name=None):
    for i in range(n): C(119 + i % 4, cx + (i - (n - 1) / 2) * 0.42 + R.uniform(-0.1, 0.1), by + (i % 2) * 0.22)
    light(name or nm('cand'), cx, by - 0.3, WARM, 2.0, 0.6)
def candelabrum(cx, by, name=None): C(115, cx, by, block=(0.6, 0.4)); light(name or nm('cdl'), cx, by - 1.4, WARM, 2.8, 0.75)
def torch_cr(cx, face_bottom, name=None):        # đuốc lửa xanh lá gắn mặt gạch
    O.put('wall props', 'torch', 0, cx, face_bottom - 0.85); light(name or nm('torch'), cx, face_bottom - 0.6, GREEN, 3.2, 0.8)
def banner(cx, face_bottom, tid=0): C(tid, cx, face_bottom - 0.15, 'wall props')
def coffins(x0, y, n, step=1.6, vertical=True):
    for i in range(n): C((128 if vertical else 123) + (i * 2 + R.randrange(2)) % (6 if vertical else 5), x0 + i * step, y, block=(1.0, 0.6) if vertical else (2.6, 0.6))
# Old Prison
def flame_stand(cx, by, name=None):
    P(666, cx, by, block=(0.6, 0.4)); O.put('ambient fx', 'flame', 0, cx, by - 1.45); light(name or nm('blue'), cx, by - 1.6, BL, 3.2, 0.85)
def flame_wall(cx, face_bottom, name=None):
    O.put('wall props', 'op', 674, cx, face_bottom - 0.55); O.put('wall props', 'flame', 0, cx, face_bottom - 0.9); light(name or nm('bluew'), cx, face_bottom - 0.4, BL, 2.8, 0.8)
def opbone(cx, by): P(R.choice(range(245, 282)), cx, by, 'ground decals')
def stone(cx, by): P(R.choice(range(677, 715)), cx, by, 'ground decals')
def blood(cx, cy, kind=0):
    c0 = (35, 38)[kind]
    for dy in (0, 1):
        for dx in (0, 1): O.obj('ground decals', None, None, (cx + dx) * T, (cy + dy + 1) * T, 32, 32, gid=OP.ter.firstgid + (12 + dy) * 65 + c0 + dx)
def oplitter(x0, y0, x1, y1, n):
    for _ in range(n):
        r = R.random(); cx, cy = R.uniform(x0, x1), R.uniform(y0, y1)
        if not free(cx, cy): continue
        if r < 0.45: opbone(cx, cy)
        elif r < 0.75: stone(cx, cy)
        else: P(R.choice(range(383, 400)), cx, cy, 'ground decals')

# ================================================================== CỬA / CỔNG (khớp tường theo doors.py)
cells = []
for (ax, top) in F.o.arch6s:
    kind = dict(F.CELLS).get(ax - 1, 'ren')
    a = {'open': 'op_cellgate1_open', 'shut': 'op_cellgate2', 'pell': 'op_cellgate1', 'ren': 'op_cellgate1'}[kind]
    cells.append(('pell_gate' if kind == 'pell' else ('ren_gate' if kind == 'ren' else 'cell_%d_gate' % (ax - 1)), a, ax, top, kind))
D.put_all(O, AID, F.DOORS + cells)

# ================================================================== Z1 OUBLIETTE (trong x100..111, hàng 109..119)
# xà lim Ren: trong x105..111 hàng 109..112 (chung tường bắc/đông Oubliette), tường nam hàng 113..115 có vòm x105..110
P(374, 106.5, 108.95, 'wall props'); P(375, 110.0, 108.95, 'wall props')            # còng trống của Ren trên vách
P(283, 108.3, 108.95, 'wall props')                                                  # bạn tù cũ bị xích
P(335, 110.6, 111.9, block=(0.5, 0.3)); opbone(105.8, 111.6); blood(109, 110, 1)
for cx in (106.4, 107.6, 108.4): stone(cx, 116.9)                                    # đá vụn rơi khi rung chấn (cổng kẹt)
P(786, 101.3, 114.6, block=(2.0, 2.4)); P(797, 102.7, 113.6); P(794, 101.0, 111.3)   # bàn trực dọc + ghế (cai ngục vắng)
P(371, 101.1, 112.6); light('oub_candle', 101.1, 112.3, WARM, 2.0, 0.6)
P(447, 101.7, 113.5, 'ground decals'); P(452, 100.7, 115.4, 'ground decals')
P(16, 110.6, 119.6, block=(0.8, 0.4)); P(17, 109.6, 119.7, block=(0.8, 0.4)); P(18, 111.0, 118.4, block=(0.8, 0.4))
P(765, 103.0, 118.4, 'props front'); flame_wall(100.6, 109.0, 'oub_flame_w'); flame_stand(103.0, 116.2, 'oub_flame_s')
oplitter(100.8, 116.5, 108, 119.8, 6)

# ================================================================== Z2 CELL ROWS (xà lim trong x+1..x+6 hàng 91..97; hành lang 101..105)
for x, kind in F.CELLS:
    if kind == 'shut':
        P(284 + x % 4, x + 4.0, 91.05, 'wall props'); P(833, x + 2.0, 93.0, block=(1.8, 0.9)); blood(x + 3, 94)
        P(433, x + 5.4, 96.4); opbone(x + 4.2, 96.8); P(388, x + 3.0, 95.8, 'ground decals'); P(336, x + 6.0, 92.6)
    elif kind == 'open':                                   # chỗ núp khi gỗ lăn — giữa trống, đồ dồn góc
        P(282, x + 4.0, 91.05, 'wall props'); P(249, x + 1.8, 92.8); opbone(x + 6.0, 92.4); P(337, x + 6.2, 96.8)
        P(401 if x % 2 else 400, x + 4.0, 92.6, 'props front')
    else:                                                  # xà lim Pell: nến, giấy viết kín, bát nước
        P(372, x + 2.0, 92.6); light('pell_candle', x + 2.0, 92.4, WARM, 2.2, 0.65)
        for i, (dx, dy) in enumerate(((3.0, 92.2), (4.0, 92.8), (5.2, 92.3), (3.6, 93.6), (5.6, 94.2))): P(443 + i * 3, x + dx, dy, 'ground decals')
        P(338, x + 6.0, 96.6); P(376, x + 6.6, 91.05, 'wall props')
    if x != 104: flame_wall(x + 7.0, 101.0)
    P(606 + x % 6, x + 1.0 if kind != 'pell' else x + 6.6, 100.6, 'wall props')
# tời mở xà lim Pell: trên sàn hành lang trước vòm, quấn xích (chém đứt bằng katana)
art('pell_winch', 'op_gear', 95.0 * T, 101.2 * T, 'mechanics_art', None); P(384, 95.9, 102.9, 'props'); P(387, 95.4, 103.2, 'props')
# hành lang: lồng treo + xích trần (như mẫu op_38), máu, đồ rơi
for cx in (81.5, 88.5, 102.5): P(766 + int(cx) % 3, cx, 103.4, 'props front')
P(405, 99.5, 104.2, 'props front'); blood(84, 102); blood(99, 103, 1); oplitter(77, 101.5, 111, 105.5, 12)
# phòng tra tấn (trong x77..86, hàng 109..119; vòm mở x80..82)
P(833, 78.2, 111.6, block=(1.8, 0.9)); P(835, 85.2, 111.6, block=(2.6, 0.9))
P(839, 78.4, 116.4, block=(1.8, 0.8)); P(843, 82.0, 115.0, block=(1.8, 0.8)); P(844, 84.8, 118.4, block=(1.8, 0.6))
P(337, 80.2, 119.4); P(339, 86.0, 119.6); blood(81, 116); blood(83, 117, 1); opbone(85.4, 116.0); opbone(79.8, 119.6)
P(404, 82.0, 113.4, 'props front'); P(376, 77.6, 108.95, 'wall props'); P(377, 86.4, 108.95, 'wall props')
P(380, 84.2, 108.95, 'wall props', name='key_hook'); P(345, 77.6, 119.7, block=(0.6, 0.4)); light('tor_cdl', 77.6, 118.3, WARM, 2.6, 0.7)
# phòng gác (trong x88..98, hàng 109..119; cửa gỗ trong vòm x92..94)
P(578, 90.0, 108.95, 'wall props'); P(608, 89.0, 108.4, 'wall props'); P(608, 91.0, 108.4, 'wall props')   # chân dung nhà vua giữa 2 cờ
flame_wall(96.5, 109.0, 'guard_flame')
P(785, 93.0, 113.8, block=(3, 1.1)); P(793, 91.1, 113.7); P(799, 95.0, 113.7, flip=True); P(806, 93.0, 112.5)
P(347, 92.4, 112.9); light('guard_cdl', 92.4, 112.0, WARM, 2.8, 0.75)
P(450, 93.6, 113.3, 'ground decals'); P(455, 94.4, 115.2, 'ground decals'); P(461, 90.4, 116.0, 'ground decals')
P(787, 93.6, 118.2, block=(3, 1.0)); P(795, 91.8, 118.0); P(801, 95.4, 118.0, flip=True)
P(16, 97.6, 110.4, block=(0.8, 0.4)); P(17, 97.6, 111.6, block=(0.8, 0.4)); P(80, 96.6, 110.6, block=(0.8, 0.4))
P(753, 97.2, 119.6, block=(1.6, 0.4)); P(759, 97.6, 118.2); P(409, 89.4, 119.6, block=(1.8, 0.6)); P(666, 88.6, 112.6)
P(412, 89.0, 117.4)                                                                  # thùng vỡ

# ================================================================== Z2b ROLLING HALL (x69..74, hàng 79..117): miệng hầm tối ở đầu bắc, vực ở đầu nam
TRAPS.append(('roller', 69, 79, 6, 39))
for by in (86, 99, 110): opbone(R.uniform(69.5, 74.5), by)
flame_wall(69.4, 79.0, 'hall_tunnel_w'); flame_wall(74.6, 79.0, 'hall_tunnel_e')
P(541, 71.0, 119.4); P(547, 73.6, 119.2)                                            # giáo dưới vực cuối làn

# ================================================================== Z3a SÂN LƯỠI XOAY (x34..64, hàng 83..102) + cầu treo + tời
TRAPS += [('blades', 57, 85, 1, 15), ('blades', 61, 85, 1, 15), ('blades', 38, 92, 18, 1)]
art('bridge_winch', 'op_gear', 56.6 * T, 82.6 * T, 'mechanics_art', None)          # tời hạ cầu ở bờ nam
flame_stand(51.6, 84.6, 'bridge_flame_w'); flame_stand(58.4, 84.6, 'bridge_flame_e')
P(253, 36.6, 86.4); P(454, 37.6, 86.2, 'ground decals'); opbone(38.4, 86.8); P(387, 36.2, 88.2, 'ground decals')   # kẻ vượt ngục chết, nhìn sang gờ xa
oplitter(35, 84, 63, 101, 22); blood(46, 89); blood(52, 99, 1)
for (cx, by) in ((34.3, 83.6), (34.3, 101.8), (63.6, 101.8)): flame_stand(cx + (0.8 if cx < 40 else -0.8), by)
# ================================================================== Z3b LỐI VŨNG MÁU (x51..59, hàng 67..78) + CỔNG ĐẦU LÂU
for (cx, by) in ((48.0, 73.6), (49.6, 77.4), (63.0, 72.8), (65.0, 77.0), (46.4, 78.6)): P(541 + int(cx) % 12, cx, by)   # giáo cắm dưới hố
flame_stand(56.5, 74.9, 'skull_flame')                                                # giá lửa xanh giữa đường đá (như mẫu)
flame_stand(47.0, 69.9, 'skull_flame_w'); flame_stand(66.0, 69.9, 'skull_flame_e')
P(400, 53.4, 66.95, 'wall props'); P(401, 59.6, 66.95, 'wall props', flip=True)     # xích vắt từ cột xuống cổng
for (cx, by) in ((48.6, 68.2), (51.0, 70.4), (61.4, 68.6), (64.2, 70.2)): stone(cx, by)
oplitter(52.5, 71.5, 60.5, 78.8, 6)
# gờ xa (x0..29, hàng 68..73): đường đoàn Black Lantern — giá lửa soi đường
for i, cx in enumerate((6, 13, 20, 27)): flame_stand(cx, 70.4, 'ledge_flame_%d' % i)

# ================================================================== Z4 THE OSSUARY (sàn x38..73, hàng 48..61; bệ thờ x49..63: mặt bệ 48..51, lan can 52, mặt gạch 53..55)
banner(53.0, 48); banner(60.0, 48, 1)                                                 # 2 cờ đỏ kẹp cổng 2 ấn x55..57
C(173, 52.6, 51.7, block=(1.2, 0.6), name='seal_socket_1'); C(173, 60.4, 51.7, block=(1.2, 0.6), name='seal_socket_2')
candelabrum(50.6, 50.4); candelabrum(62.4, 50.4)
heap(51.4, 57.0, 9, 1.0); heap(61.6, 57.0, 9, 1.0)                                    # cụm dày 2 chân bậc thờ (như mẫu)
wallbase(38.6, 47.4, 48, 0.7); wallbase(65.0, 72.6, 48, 0.7)
torch_cr(43.5, 48); torch_cr(68.5, 48)
coffins(40.0, 60.4, 4, 1.6); coffins(67.0, 60.4, 4, 1.6)
C(270, 52.6, 61.9, block=(1.4, 0.7)); C(271, 60.4, 61.9, block=(1.4, 0.7))          # 2 tượng kẹp lối nam (lối x54..58)
candles(44.0, 54.4, 3, 'oss_rest'); C(37, 44.0, 56.2, block=(2.4, 0.4))              # điểm nghỉ: ghế + nến
heap(46.0, 57.6, 6, 0.9); heap(67.4, 57.6, 6, 0.9)

# ================================================================== Z5 SCRIPTORIUM (sàn x4..33, hàng 48..60); bệ tây bắc x4..15 hàng 48..51
C(112, 10.0, 50.4, block=(1.0, 0.5)); C(111, 10.0, 49.75, 'props front', name='name_register')   # Sổ Tên
C(174, 13.6, 50.4, block=(1.2, 0.6), name='seal1_pedestal'); candelabrum(5.4, 50.6); candelabrum(14.8, 49.4)
banner(6.0, 48, 3); banner(13.0, 48, 4); wallbase(17.4, 33, 48, 0.6); torch_cr(17.5, 48); torch_cr(28.5, 48)
TRAPS += [('ice', 5, 52, 1, 3), ('ice', 14, 52, 1, 3)]                               # lỗ phun băng trên MẶT BỆ, bắn xuống
for cx in (5.5, 14.5): C(193, cx, 54.8, 'wall props')
for r, by in enumerate((54.0, 57.0, 60.0)):                                           # 3 dãy bàn chép (bục + sách + ghế)
    for cx in (20.0, 27.5):
        for j in range(3):
            C(112 + (j + r) % 3, cx + (j - 1) * 1.5, by - 0.6, block=(1.0, 0.45)); C(109 + (j + r) % 3, cx + (j - 1) * 1.5, by - 1.2, 'props front')
        C(37, cx, by + 0.3, block=(2.6, 0.4))
heap(6.0, 59.4, 7, 0.9); heap(32.0, 59.6, 6, 0.9); candles(24.0, 50.0, 3)
# hốc tủ xương (x22..32 mặt gạch): xương + sọ trong hốc, đống xương dày dưới chân (như mẫu cr_11)
for x in (22, 25, 28, 31):
    for t, dx, dy in ((43, 0.6, 46.6), (70, 1.3, 46.9), (52, 1.0, 47.5)): C(t, x + dx, dy, 'wall props')
    heap(x + 1.0, 49.6, 7, 0.9)

C(141, 32.6, 51.8, block=(1.0, 0.6)); C(144, 32.6, 58.0, block=(1.0, 0.6))              # quan tài đứng góc đông
heap(17.4, 60.2, 7, 1.0); heap(25.0, 60.4, 6, 1.0)
candelabrum(24.0, 60.8)
# PHÒNG THƯ CỦA MẸ (sàn x10..27, hàng 26..39): mộ phủ vải đỏ giữa 2 chân nến + bục thư (như mẫu cr_13)
C(125, 18.5, 32.6, block=(2.8, 1.0)); C(179, 18.5, 31.9, 'props front')              # mộ + vải đỏ phủ
C(161, 18.5, 30.2, block=(1.2, 0.6)); candelabrum(15.4, 33.2); candelabrum(21.6, 33.2)
C(112, 18.5, 35.4, block=(1.0, 0.5)); C(111, 18.5, 34.75, 'props front', name='mother_letters')
wallbase(10.5, 27, 26, 0.65); torch_cr(12.5, 26); torch_cr(24.5, 26); heap(12.0, 38.6, 6, 0.9); heap(25.8, 38.6, 6, 0.9)

# ================================================================== Z6 ROYAL RELIQUARY (sàn x78..107, hàng 48..60); bệ đông bắc x96..107
C(175, 102.4, 50.4, block=(1.2, 0.6), name='seal2_pedestal'); candelabrum(97.2, 49.4); candelabrum(106.6, 50.6)
banner(98.0, 48, 9); banner(105.0, 48, 10); wallbase(78.5, 94.2, 48, 0.55); torch_cr(81.5, 48); torch_cr(89.5, 48)
for r, by in enumerate((53.6, 56.8, 59.8)):                                           # bệ thánh tích RỖNG, mỗi bệ 1 tấm áp lực
    for cx in (81.0, 84.5, 88.0, 91.5):
        C(172 + (r + int(cx)) % 2, cx, by, block=(1.2, 0.6)); TRAPS.append(('plate', int(cx) - 1, int(by), 2, 1))
TRAPS += [('launcher', 80, 46, 1, 1), ('launcher', 83, 46, 1, 1), ('launcher', 86, 46, 1, 1)]   # lỗ bắn trên mặt gạch bắc (tránh cửa kho x91..94)
for cx in (80.5, 83.5, 86.5): C(193, cx, 47.7, 'wall props')
TRAPS.append(('pit', 90, 48, 6, 2))                                                   # sàn sập trước cửa kho
heap(106.4, 59.6, 7); heap(96.0, 59.8, 5)
# hốc thánh tích TRỐNG (x78..88): mỗi hốc 1 tấm nhãn rơi dưới chân; bình + nến dày dọc vách; quan tài mở nắp góc tây nam
for x in (78, 81, 84, 87): O.put('ground decals', 'op', R.choice(range(443, 470)), x + 1.0, 48.9)
wallbase(97.5, 107.4, 52.6, 0.0)
for cx, by in ((79.0, 60.8), (80.6, 60.8)): C(129, cx, by, block=(1.0, 0.6))
C(150, 82.6, 60.6, 'ground decals'); heap(84.4, 60.2, 6, 0.8); heap(106.2, 56.4, 7, 0.9); heap(96.4, 55.6, 6, 0.8)
candelabrum(95.0, 60.8); candles(100.6, 60.4, 4)
# KHO KIỂM KÊ (sàn x84..101, hàng 26..39): rương mở toang, bục sổ kiểm kê
for (cx, by, t) in ((86.4, 28.6, 675), (89.4, 28.6, 676), (99.0, 28.4, 409), (99.4, 31.6, 410), (86.6, 38.4, 409)): O.put('props', 'op', t, cx, by)
C(112, 93.0, 33.4, block=(1.0, 0.5)); C(111, 93.0, 32.75, 'props front', name='inventory_book'); candles(91.2, 34.4, 2)
wallbase(84.5, 101, 26, 0.45, bones=False); torch_cr(87.0, 26); torch_cr(98.5, 26)
for _ in range(10): O.put('ground decals', 'op', R.choice(range(443, 470)), R.uniform(85, 100), R.uniform(29, 39))   # giấy tờ vương vãi

# ================================================================== Z7 PENITENT'S WALK (sàn x48..63, hàng 26..42; bệ cao: mặt bệ 26..30, lan can 31, mặt gạch 32..34; sàn dưới 35..42)
TRAPS += [('fire', 49, 32, 1, 3), ('fire', 62, 32, 1, 3), ('launcher', 51, 32, 1, 3), ('launcher', 60, 32, 1, 3),
          ('saw', 49, 36, 14, 1), ('saw', 49, 39, 14, 1), ('spikes', 49, 41, 14, 2)]
for cx, t in ((49.5, 189), (62.5, 190), (51.5, 193), (60.5, 193)): C(t, cx, 34.9, 'wall props')   # tượng mặt / lỗ bắn trên mặt bệ
for y in (36.5, 39.5): C(194, 55.5, y + 0.5, 'ground decals')                         # rãnh cưa
banner(52.6, 26); banner(59.4, 26, 1); torch_cr(50.5, 26); torch_cr(61.5, 26)
C(268, 50.4, 29.6, block=(1.4, 0.7)); C(269, 61.6, 29.6, block=(1.4, 0.7))           # tượng canh 2 bên cửa Hall (trên bệ)
heap(49.4, 42.4, 5, 0.6); heap(62.4, 42.4, 5, 0.6)

# ================================================================== Z8 HALL OF NAMING (sàn x40..73, hàng 4..20): 4 trụ wall-3 (ô chặn x46..49 / x64..67, hàng 5..9 / 13..17) quanh bệ giữa có viền (như mẫu cr_15)
PILLARS = ((48.0, 10), (66.0, 10), (48.0, 18), (66.0, 18))           # (tâm x, hàng dưới mặt trụ)
for (cx, fb) in PILLARS: torch_cr(cx, fb)                                             # đuốc trên mặt trụ
C(272, 56.5, 12.6, block=(1.6, 0.8)); candles(55.0, 13.2, 3, 'hall_c1'); candles(58.0, 13.2, 3, 'hall_c2')
banner(52.6, 4); banner(60.4, 4, 1); torch_cr(43.5, 4); torch_cr(70.5, 4)
wallbase(40.6, 52.0, 4, 0.6); wallbase(61.0, 73.0, 4, 0.6)
for (cx, fb) in PILLARS: heap(cx, fb + 0.7, 5, 0.6)                                   # bình + nến chân trụ
blocked = F.k.pil_block
for _ in range(55):
    x, y = R.uniform(44.5, 69.5), R.uniform(6.5, 20.8)
    if 50.5 < x < 62.5 and 8.5 < y < 15.6: continue                                   # bệ giữa sạch
    if not (free(x - 0.45, y) and free(x + 0.45, y)) or any((int(x) + dx, int(y) + dy) in blocked for dx in (-1, 0, 1) for dy in (-1, 0)): continue
    C(R.choice(BONES), x, y, 'ground decals')
