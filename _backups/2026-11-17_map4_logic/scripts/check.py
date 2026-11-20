# Map 4 — KIỂM TRA TỰ ĐỘNG khung + cửa (chạy sau mỗi lần dựng):
#  1) đi thử theo từng giai đoạn mở cổng: cổng chưa mở thì KHÔNG được tới khu phía sau (không đường tắt)
#  2) mỗi cửa Crypt nằm đúng mặt gạch bắc (ô ngay trên sàn phòng dưới là mặt gạch/lối) ; cổng xà lim nằm trên đoạn vòm 6 ô
#  3) giữa 2 phòng Crypt chồng dọc có >= 1 hàng đá + 3 hàng mặt gạch
import sys; sys.path.insert(0, '.')
from collections import deque
import gen_frame2 as F
k, o, W, H = F.k, F.o, F.W, F.H
def op_plat(x, y): M = o.Mplat; return 0 <= x < W and 0 <= y < H and M[y][x] and M[y][x + 1] and M[y + 1][x] and M[y + 1][x + 1]
walk = set()
for y in range(H):
    for x in range(W):
        if (x, y) in k.area:
            if (x, y) in k.floor and (x, y) not in k.blocked: walk.add((x, y))
        elif (x, y) in o.area:
            if op_plat(x, y) and (x, y) not in o.cell_block: walk.add((x, y))
# vùng chặn của từng cổng (khi ĐÓNG)
G = {}
def rect(x, y, w, h): return {(i, j) for j in range(y, y + h) for i in range(x, x + w)}
G['ren_cell'] = rect(107, 113, 2, 3)          # lòng vòm xà lim Ren (đoạn vòm x104..109, mở ở +2..+3)
for x, kind in F.CELLS: G['cell_%d' % x] = rect(x + 3, 98, 2, 3)
G['guard'] = rect(93, 106, 2, 3); G['oub'] = rect(102, 106, 2, 3)     # §303: lòng vòm cửa gỗ = 2 ô (door+1, door+2)
G['skull'] = rect(55, 64, 3, 3)
G['seal'] = rect(55, 43, 3, 5); G['hall'] = rect(55, 21, 2, 5); G['letters'] = rect(19, 40, 2, 8); G['vault'] = rect(92, 40, 2, 8)
G['exit5'] = rect(56, 0, 2, 4)
BRIDGE = rect(54, 79, 2, 4)
def reach(open_set, bridge):
    W2 = set(walk) | (BRIDGE if bridge else set())
    for g, cells in G.items():
        if g not in open_set: W2 -= cells
    start = (108, 110); seen = {start}; q = deque([start])
    while q:
        x, y = q.popleft()
        for d in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            n = (x + d[0], y + d[1])
            if n in W2 and n not in seen: seen.add(n); q.append(n)
    return seen
P = {'Oubliette': (101, 115), 'Hành lang xà lim': (90, 103), 'Xà lim Pell': (93, 95), 'Phòng gác': (93, 115), 'Phòng tra tấn': (82, 115),
     'Rolling Hall': (71, 98), 'Sân lưỡi xoay': (45, 92), 'Lối vũng máu': (56, 74), 'Ossuary': (45, 55), 'Scriptorium': (15, 56),
     'Phòng thư': (18, 32), 'Reliquary': (90, 56), 'Kho': (92, 32), 'Penitent Walk': (55, 38), 'Hall of Naming': (55, 14), 'Ra Map 5': (56, 1)}
for p, c in P.items():
    if c not in walk: print('!! điểm mốc không đi được:', p, c)
stages = [('0 tỉnh dậy (cổng Ren kẹt mở)', {'ren_cell'}, False),
          ('1 + cửa Oubliette', {'ren_cell', 'oub'}, False),
          ('2 + chìa phòng gác', {'ren_cell', 'oub', 'guard'}, False),
          ('3 + chém xích tời Pell', {'ren_cell', 'oub', 'guard', 'cell_90'}, False),
          ('4 + hạ cầu treo', {'ren_cell', 'oub', 'guard', 'cell_90'}, True),
          ('5 + Pell mở cổng đầu lâu', {'ren_cell', 'oub', 'guard', 'cell_90', 'skull'}, True),
          ('6 + 2 cửa phòng phụ', {'ren_cell', 'oub', 'guard', 'cell_90', 'skull', 'letters', 'vault'}, True),
          ('7 + 2 ấn (cổng Ossuary)', {'ren_cell', 'oub', 'guard', 'cell_90', 'skull', 'letters', 'vault', 'seal'}, True),
          ('8 + cửa Hall', {'ren_cell', 'oub', 'guard', 'cell_90', 'skull', 'letters', 'vault', 'seal', 'hall'}, True),
          ('9 + sau The Severing', set(G), True)]
for name, op, br in stages:
    r = reach(op | {'cell_83', 'cell_97'}, br)
    print('%-28s' % name, ' '.join(('●' if c in r else '·') + p for p, c in P.items()))
# 2) cửa Crypt: hàng dưới mặt gạch phải là sàn phòng; lối sau cửa phải đi được khi mở
for name, art, x, top, note in F.DOORS:
    if art.startswith('cr_'):
        wd = 4 if art != 'cr_bossdoor' else 3
        below = [(x + i, top + 3) for i in range(wd)]
        ok = all(c in k.floor for c in below)
        print('cửa %-12s %-12s x%d hàng %d..%d  chân cửa trên sàn: %s' % (name, art, x, top, top + 2, 'ĐẠT' if ok else 'LỖI'))
# 3) phòng Crypt chồng dọc
