# Map 4 — The Catacombs of Naming: KHUNG bố cục (địa hình + khối kiến trúc chính). 17/11/2026.
# Bắc: ngục Old Prison (y0..43). Nam: hầm mộ Crypt (y44..). Toạ độ ô.
import sys, os; sys.path.insert(0, '.'); sys.path.insert(0, '../lore_1116')
from ck import CK
from opk import OPK
W, H = 120, 104
k = CK(W, H, seed=44); o = OPK(k)
# ===================================================== NGỤC (Old Prison)
o.zone_area(0, 0, W, 44)
# Z1 The Oubliette (đông bắc): phòng tường mỏng, xà lim của Ren (cổng xà lim 4 ô, tời ngay ngoài song)
o.plat(96, 2, 22, 20); o.cell(96, 2, 22, 20, gap_w=(14, 15, 16))      # cửa gỗ dọc ở cột tây, hàng 14..16
o.cell(103, 6, 9, 9, gate=105)
# Z2 The Cell Rows: 6 xà lim bắc (cổng 4 ô), hành lang = làn gỗ lăn hàng 13..17 (x47..95), phòng tra tấn + phòng lính gác phía nam
o.plat(46, 3, 52, 16); o.plat(50, 19, 48, 11)        # hành lang hàng 3..18; dải phòng x50..97 hàng 19..29 (không chạm bến nam)
CELLS = [(50, 'open'), (57, 'shut'), (64, 'open'), (71, 'pell'), (78, 'open'), (85, 'shut')]
for x, kind in CELLS:
    o.cell(x, 4, 8, 9, gate=x + 2)
o.cell(50, 18, 15, 12, door_n=56)        # phòng tra tấn (vòm mở) — trong x51..63, hàng 21..26
o.cell(70, 18, 19, 12, door_n=78)        # phòng lính gác (cửa gỗ khoá) — trong x71..87, hàng 21..26
o.dirt(53, 22, 7, 3); o.dirt(77, 23, 7, 3); o.dirt(60, 14, 9, 3)
# Z3 The Blood Pit (tây): bến -> cầu treo (khe x20..23) -> mỏm tây (sân bẫy) -> cầu về -> bến nam -> cổng xuống hầm mộ
o.plat(36, 10, 11, 11)                                  # bến (làn gỗ lăn rơi xuống hố ở mép tây x36)
o.plat(24, 18, 12, 2); o.plat(14, 18, 6, 2)             # cầu đá 2 hàng, khe 4 ô x20..23 cho cầu treo
o.plat(3, 8, 12, 25)                                    # mỏm tây
o.plat(15, 30, 25, 2)                                   # cầu về
o.plat(40, 29, 9, 15)                                   # bến nam x40..48 (cột x49 là hố: tách khỏi dải phòng)
o.hwall(40, 48, 37, 'south', gate=44, join_l=True, join_r=True)   # tường chắn + cổng xà lim (Pell mở)
o.plat(44, 40, 5, 8)                                    # xuống bậc hầm mộ
o.plat(6, 1, 34, 3)                                     # gờ xa phía bắc (đoàn Black Lantern) — không tới được
import random as _r
_rb = _r.Random(7); top = 6
for cx in range(0, 48):                                 # hồ máu: mép trên lượn, phủ hết hố phía tây
    top = max(5, min(8, top + _rb.choice((-1, 0, 0, 1))))
    for cy in range(top, 45): o.Mblood[cy][cx] = 1
BRIDGE_GAP = (20, 18)                                   # góc trên-trái khe cầu treo (4x2)
# ===================================================== HẦM MỘ (Crypt)
k.zone_area(0, 44, W, H - 44)
k.room(45, 44, 3, 4); k.stamp('stairs5', 44, 44)                                    # bậc từ ngục xuống (xuyên vách bắc của sảnh)
# Z4 The Ossuary (sảnh trung tâm)
k.room(45, 48, 38, 20)
k.plat(56, 48, 16, 5, attach='n'); k.stair(61, 53)
k.mask('carpet', 62, 57, 3, 11)
# Z5 Scriptorium of Names (cánh tây)
k.room(38, 56, 7, 3); k.room(4, 48, 34, 22)
k.plat(4, 48, 12, 5, attach='nw'); k.stair(7, 53)
k.room(10, 70, 3, 4); k.room(4, 74, 16, 12)            # phòng thư của mẹ
# Z6 The Royal Reliquary (cánh đông)
k.room(83, 56, 7, 3); k.room(90, 48, 26, 22)
k.plat(104, 48, 12, 5, attach='ne'); k.stair(107, 53)
k.room(106, 70, 3, 4); k.room(100, 74, 17, 7)           # kho kiểm kê (cửa doors2 hướng xuống ở tường nam Reliquary)
# Z7 The Penitent's Walk — mở sẵn, chữ L: đi xuống rồi rẽ đông vào sảnh chờ
k.room(61, 68, 5, 4)                                    # vòm mở từ tường nam Ossuary
k.room(57, 72, 13, 24)                                  # đoạn dọc y72..95
k.room(57, 96, 32, 5)                                   # sảnh chờ y96..100 (mặt gạch bắc y93..95 = chỗ cửa boss)
# Z8 Hall of Naming — nằm TRÊN sảnh chờ; cửa boss 3x3 ở mặt gạch bắc sảnh chờ (x78..80), đi lên để vào
k.room(72, 72, 26, 18)
k.room(78, 90, 3, 6)                                    # lối sau cửa boss xuyên đá
k.plat(78, 72, 20, 4, attach='n'); k.stair(85, 76)      # bệ lễ phía bắc Hall
# Z9 The Descent — từ góc đông-nam Hall rẽ đông xuống bậc
k.room(98, 87, 4, 3); k.room(102, 87, 14, 14)
# vòm cuốn / hốc thờ trên mặt vách bắc
k.stamp('arcade5b', 72, 45); k.stamp('arcade5b_grates', 72, 45)
k.stamp('shrine_a', 58, 45); k.stamp('shrine_b', 68, 45); k.stamp('door_arch', 63, 45)
k.stamp('arcade5', 16, 45); k.stamp('arcade5b', 27, 45)
k.stamp('arcade5', 90, 45); k.stamp('arcade5_grates', 90, 45)
k.stamp('arcade5', 72, 69); k.stamp('arcade5_grates', 72, 69); k.stamp('arcade5b', 87, 69); k.stamp('shrine_a', 83, 69); k.stamp('shrine_b', 92, 69)
DOORS = [('boss', 78, 93), ('metal', 0, 0)]
k.build(); o.build()
# (bỏ hốc niche_a giữa sàn Ossuary — đặt nhầm)
for (x, y), v in k.deco.items(): k.L['deco'][y][x] = v
ORDER = o.ORDER[:4] + k.ORDER[:6] + o.ORDER[4:] + k.ORDER[6:]
def csv(g): return ',\n'.join(','.join(str(v) for v in r) for r in g)
out = ['<?xml version="1.0" encoding="UTF-8"?>', '<map version="1.10" orientation="orthogonal" renderorder="right-down" width="%d" height="%d" tilewidth="32" tileheight="32" infinite="0">' % (W, H)]
base = "/tmp/gp/EPIC RPG World Pack - Crypt V.1.6/TiledMap Editor/Maps/"
root = str(k.ter.path.parents[3])
for t in k.tiles: out.append(' <tileset firstgid="%d" source="%s"/>' % (t.firstgid, os.path.relpath(str(t.path).replace(root, '/tmp/gp'), base)))
for i, n in enumerate(ORDER): out.append(' <layer id="%d" name="%s" width="%d" height="%d"><data encoding="csv">\n%s\n</data></layer>' % (i + 1, n, W, H, csv(k.L[n])))
out.append('</map>')
p = base + "_frame.tmx"; assert '/mnt/' not in os.path.realpath(base); open(p, 'w').write('\n'.join(out))
if __name__ == '__main__':
    import render_tmx; os.environ['SR_CLEAN'] = '1'
    sc = float(sys.argv[1]) if len(sys.argv) > 1 else 0.5
    render_tmx.main(p, 0, 0, W, H, 'ref/frame.png', sc)
