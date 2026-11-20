# Phòng thử phong cách: dựng lại bố cục kiểu Sample Map bằng CK, xuất ảnh.
import sys; sys.path.insert(0, '.'); sys.path.insert(0, '../lore_1116')
from ck import CK, ED
import render_tmx
W, H = 44, 30
k = CK(W, H); k.zone_area(0, 0, W, H)
k.room(3, 5, 38, 22)                         # sàn x3..40, y5..26 (mặt gạch bắc hàng 2..4)
k.plat(15, 5, 13, 4, attach='n')             # bệ giữa áp vách bắc, mặt bệ y5..8
k.stair(19, 9)                               # bậc thang nam bệ (mép nam = hàng 9)
k.plat(3, 12, 10, 3, attach='w')             # bệ tây
k.plat(30, 12, 11, 3, attach='e')            # bệ đông
k.mask('carpet', 20, 13, 3, 12); k.mask('carpet', 20, 22, 14, 3)
k.stamp('arcade5', 3, 2); k.stamp('arcade5_grates', 3, 2); k.stamp('arcade5b', 29, 2); k.stamp('arcade5b_grates', 29, 2)
k.stamp('shrine_a', 16, 2); k.stamp('shrine_b', 25, 2); k.stamp('door_arch', 20, 2)
k.build()
k.stamp('arcade4_broken', 3, 15); k.stamp('niche_a', 32, 15); k.stamp('niche_b', 36, 15)
import ck as _c
for nm in ('deco',):
    for (x, y), v in k.deco.items(): k.L['deco'][y][x] = v
def csv(g): return ',\n'.join(','.join(str(v) for v in r) for r in g)
out = ['<?xml version="1.0" encoding="UTF-8"?>', '<map version="1.10" orientation="orthogonal" renderorder="right-down" width="%d" height="%d" tilewidth="32" tileheight="32" infinite="0">' % (W, H)]
for t in k.tiles: out.append(' <tileset firstgid="%d" source="../Tilesets/%s"/>' % (t.firstgid, t.path.name))
for i, n in enumerate(k.ORDER): out.append(' <layer id="%d" name="%s" width="%d" height="%d"><data encoding="csv">\n%s\n</data></layer>' % (i + 1, n, W, H, csv(k.L[n])))
out.append('</map>')
p = "/tmp/gp/EPIC RPG World Pack - Crypt V.1.6/TiledMap Editor/Maps/_proof.tmx"; open(p, 'w').write('\n'.join(out))
import os; os.environ['SR_CLEAN'] = '1'
render_tmx.main(p, 0, 0, W, H, 'ref/proof.png', 1)
