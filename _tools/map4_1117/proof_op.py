import sys, os; sys.path.insert(0, '.'); sys.path.insert(0, '../lore_1116')
from ck import CK
from opk import OPK
import render_tmx
W, H = 44, 32
k = CK(W, H); o = OPK(k); o.zone_area(0, 0, W, H)
o.plat(2, 3, 40, 22)                     # sàn ngục
o.plat(18, 25, 5, 6)                     # cầu đá xuống nam
o.wall(2, 0, 12, 7)                      # khối phòng gác (tây bắc)
o.wall(14, 0, 28, 3)
o.dirt(6, 14, 8, 5); o.dirt(28, 16, 9, 6)
o.blood(33, 20, 8, 5)
o.cell(16, 6, 8, 9, door=19); o.cell(23, 6, 8, 9, bars=(26,)); o.cell(30, 6, 8, 9, door=33)
o.build()
k.L['ground'] = [[0] * W for _ in range(H)]
def csv(g): return ',\n'.join(','.join(str(v) for v in r) for r in g)
out = ['<?xml version="1.0" encoding="UTF-8"?>', '<map version="1.10" orientation="orthogonal" renderorder="right-down" width="%d" height="%d" tilewidth="32" tileheight="32" infinite="0">' % (W, H)]
base = "/tmp/gp/EPIC RPG World Pack - Crypt V.1.6/TiledMap Editor/Maps/"
for t in k.tiles: out.append(' <tileset firstgid="%d" source="%s"/>' % (t.firstgid, os.path.relpath(str(t.path).replace(str(k.ter.path.parents[3]), '/tmp/gp'), base)))
for i, n in enumerate(o.ORDER): out.append(' <layer id="%d" name="%s" width="%d" height="%d"><data encoding="csv">\n%s\n</data></layer>' % (i + 1, n, W, H, csv(k.L[n])))
out.append('</map>')
p = base + "_proof_op.tmx"; assert '/mnt/' not in os.path.realpath(base); open(p, 'w').write('\n'.join(out))
os.environ['SR_CLEAN'] = '1'; render_tmx.main(p, 0, 0, W, H, 'ref/proof_op.png', 1)
