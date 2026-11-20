# Dựng địa hình (gen_frame) + gom tileset đồ vật, trả về (k, o, objs, tilesets, layers). Dùng chung cho decor_*.py.
import sys, os; sys.path.insert(0, '.'); sys.path.insert(0, '../lore_1116')
import gen_frame2 as F
from m4lib import TSX, Objs, make_art_tsx, CRT, OPT, ART
k, o = F.k, F.o
ARTS = ['op_cellgate1', 'op_cellgate1_open', 'op_cellgate2', 'op_cellgate2_open', 'op_door_h', 'op_door_h_open', 'op_door_v', 'op_door_v_open',
        'op_skullgate', 'op_gear', 'op_bridge_h', 'op_bridge_h_open', 'cr_bossdoor', 'cr_metal1', 'cr_metal2', 'cr_metal3', 'cr_metal4', 'cr_metal5',
        'cr_wood1', 'cr_door2_down', 'cr_door2_down_frame', 'cr_exit_stairs', 'op_bridge_v', 'op_bridge_v_open']
MIRROR = "/tmp/gp/EPIC RPG World Pack - Crypt V.1.6/TiledMap Editor/Maps/"
art_tsx = MIRROR + "_SR-m4-art.tsx"
make_art_tsx(art_tsx, [str(ART / (a + '.png')) for a in ARTS])
AID = {a: i for i, a in enumerate(ARTS)}
extra = [TSX(OPT / "atlas-props-sprites.tsx", 'op'), TSX(CRT / "Atlas-Props-sprites.tsx", 'cr'),
         TSX(CRT / "SR-animT-props-magic-flame.tsx", 'flame'), TSX(CRT / "SR-animT-animated-torch-burning.tsx", 'torch'),
         TSX(CRT / "SR-animT-animated-candle-burning.tsx", 'candle'), TSX(CRT / "SR-traps.tsx", 'traps'), TSX(art_tsx, 'art')]
g = k.next_gid
for t in extra: t.firstgid = g; g += t.span
class _TerrTS:                                           # tileset địa hình cho Objs (decal ô đơn)
    def __init__(s, t, key): s.firstgid, s.path, s.key, s.sizes = t.firstgid, t.path, key, {}
terr = [_TerrTS(o.ter, 'opter'), _TerrTS(k.ter, 'crter')]
objs = Objs(extra + terr)
for t in terr: objs.ts[t.key].sizes = type('S', (), {'__getitem__': lambda self, i: (32, 32)})()
tilesets = list(k.tiles) + extra
ORDER = F.ORDER
def layers(): return [(n, k.L[n]) for n in ORDER]
def write(name):
    from m4lib import write_tmx
    p = MIRROR + name + '.tmx'; assert '/mnt/' not in os.path.realpath(MIRROR)
    root = str(k.ter.path.parents[3])
    class P:                                             # đường dẫn tileset: gói thật -> bản mirror /tmp/gp
        def __init__(s, t): s.firstgid, s.path = t.firstgid, (str(t.path).replace(root, '/tmp/gp') if root in str(t.path) else str(t.path))
    write_tmx(p, F.W, F.H, [P(t) for t in tilesets], layers(), objs); return p
