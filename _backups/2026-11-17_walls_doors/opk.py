# OPK — phần Old Prison: hố xương (pit), bệ lát đá (wall-1 OP), khối tường phòng (wall-2 OP), xà lim (tường mỏng 3 hàng), vũng máu.
# Ghép vào CK (cùng lưới, cùng thứ tự gid). Cách ghép học từ 'Old Prison example map.tmx' + mockup của tác giả. 17/11/2026.
import random
from pathlib import Path
import generate_map as gm, tiled_automap as ta
from mapkit import Tileset
from ck import GP
OP = GP / "EPIC RPG World Pack - Old Prison V1.7.1"
OPT = OP / "TiledMap Editor New"; OPR = OPT / "Rules"
C = 65
PIT = [656, 657, 658, 660, 661, 723, 724, 725, 726, 786, 788, 789]
def rc(r, c): return r * C + c

class OPK:
    def __init__(s, k):
        s.k = k; T = lambda n: Tileset(OPT / "Tilesets" / n)
        s.ter, s.w1, s.w2 = T("Tileset-Terrain-old prison.tsx"), T("Tileset - wall 1.tsx"), T("Tileset - wall 2.tsx")
        s.bloodts = T("Blood pool - style2 - with spikes - transparency.tsx")
        for t in (s.ter, s.w1, s.w2, s.bloodts):
            t.firstgid = k.next_gid; k.next_gid += t.span; k.tiles.append(t)
        W, H = k.W, k.H
        s.area = set()                         # ô thuộc vùng ngục (vẽ hố xương nền)
        s.Mplat = gm.grid(W + 1, H + 1)        # bệ đi được
        s.Mwall = gm.grid(W + 1, H + 1)        # khối tường phòng
        s.Mdirt = gm.grid(W + 1, H + 1); s.Mblood = gm.grid(W + 1, H + 1); s.Mspk = gm.grid(W + 1, H + 1)
        s.cells = {}                           # (x,y) -> gid lớp op cells
        s.cell_block = set(); s.cell_open = set()
        s.gates = []                           # (x, top) khe cổng xà lim 4 ô
        s.arches = []                          # (x, top) vòm 3 ô (cửa gỗ ngang đặt vào)
        s.arch6s = []; s.holes = []
    def zone_area(s, x, y, w, h):
        for j in range(y, y + h):
            for i in range(x, x + w): s.area.add((i, j))
    def plat(s, x, y, w, h): s.k._corn(s.Mplat, x, y, w, h)
    def wall(s, x, y, w, h): s.k._corn(s.Mwall, x, y, w, h)
    def carve(s, M, x, y, w, h):
        for cy in range(y + 1, y + h):
            for cx in range(x + 1, x + w): M[cy][cx] = 0
    def room(s, x, y, w, h, t=1):
        """Phòng tường dày t ô: khối wall-2 bao quanh, khoét ruột (ruột = x+t..x+w-1-t, y+t..y+h-1-t; mặt tường 3 hàng vẽ ở đầu ruột)."""
        s.wall(x, y, w, h)
        for cy in range(y + t + 1, y + h - t):
            for cx in range(x + t + 1, x + w - t): s.Mwall[cy][cx] = 0
    def opening(s, x, y, w, h):
        """Khoét cửa qua tường (ô x..x+w-1, y..y+h-1)."""
        for cy in range(y, y + h + 1):
            for cx in range(x, x + w + 1): s.Mwall[cy][cx] = 0
    def dirt(s, x, y, w, h): s.k._corn(s.Mdirt, x, y, w, h)
    def blood(s, x, y, w, h): s.k._corn(s.Mblood, x, y, w, h)
    def spikes(s, x, y, w, h): s.k._corn(s.Mspk, x, y, w, h)        # hố gai sàn (wang 'spikes' của gói)
    # ---- xà lim (tường mỏng): theo đúng bộ ô tác giả dùng trong example map
    def _put(s, x, y, rcpair, block=True):
        s.cells[(x, y)] = s.ter.firstgid + rc(*rcpair)
        if block: s.cell_block.add((x, y))
    def hwall(s, x0, x1, top, kind='north', door=None, bars=(), join_l=False, join_r=False, gate=None, arch6=None, hole=None):
        """Tường ngang 3 hàng (top..top+2) từ x0..x1. kind 'north' = mặt trong bắc của xà lim, 'south' = mặt trước có vòm trang trí.
        door = x bắt đầu vòm cửa rộng 4 (2 ô giữa đi được); bars = các x bắt đầu cụm song sắt 2 ô."""
        rng = s.k.rng; skip = set()
        if arch6 is not None:                     # đoạn vòm 6 ô (hàng 17..19, cột 39..44) — cổng xà lim đặt ở arch6+1
            for i in range(6):
                for j in range(3): s._put(arch6 + i, top + j, (17 + j, 39 + i), block=not (i in (2, 3)))
            for j in (0, 1, 2):
                for i in (2, 3): s.cell_open.add((arch6 + i, top + j))
            skip |= set(range(arch6, arch6 + 6)); s.arch6s.append((arch6, top))
        if hole is not None:                      # chừa ô cho hình cửa có sẵn tường (cổng đầu lâu)
            hx, hw = hole; skip |= set(range(hx, hx + hw)); s.holes.append((hx, top, hw))
        if gate is not None:                      # khe 4 ô cho cổng xà lim (cell gate), cột trụ ở gate-1 và gate+4
            skip |= {gate, gate + 1, gate + 2, gate + 3}
            for px in (gate - 1, gate + 4):
                for j, p in enumerate([(14, 31), (15, 31), (16, 31)]): s._put(px, top + j, p)
            skip |= {gate - 1, gate + 4}
            s.gates.append((gate, top))
        if door is not None:                      # §303: vòm cửa = 4 ô cột 40..43 (giữa đoạn vòm 6 ô của gói); lòng vòm = 2 ô giữa
            for i, col in enumerate((40, 41, 42, 43)):
                for j in range(3): s._put(door + i, top + j, (17 + j, col), block=i not in (1, 2))   # lòng vòm đi xuyên cả 3 hàng
            for j in (0, 1, 2): s.cell_open |= {(door + 1, top + j), (door + 2, top + j)}
            s.arches.append((door, top))
            skip |= {door, door + 1, door + 2, door + 3}
        for b in bars:
            for i, col in enumerate((18, 19)):
                for j in range(3): s._put(b + i, top + j, (20 + j, col))
            skip |= {b, b + 1}
        for x in range(x0, x1 + 1):
            if x in skip: continue
            if x == x0:
                cols = [(23, 40), (15, 19), (16, 19)] if join_l else [(14, 31), (15, 31), (16, 31)]
            elif x == x1:
                cols = [(23, 41), (15, 25), (16, 25)] if join_r else [(14, 31), (15, 30), (16, 30)]
            elif kind == 'north' and x == x0 + 1: cols = [(14, 33), (15, 33), (16, 33)]
            elif kind == 'north' and x == x1 - 1: cols = [(14, 36), (15, 36), (16, 36)]
            elif kind == 'north':
                c = rng.choice([19, 20, 21, 22, 23, 24, 26, 27]); cols = [(17, c), (18, c), (19, c)]
            else:
                c = rng.choice([19, 19, 20, 21, 22, 23, 24]); cols = [(14, c), (15, c), (16, c)]
            for j, p in enumerate(cols): s._put(x, top + j, p)
    def vpost(s, x, y0, y1, gaps=()):
        for y in range(y0, y1 + 1):
            if y in gaps: s.cells.pop((x, y), None); s.cell_block.discard((x, y)); continue
            s._put(x, y, s.k.rng.choice([(17, 30), (18, 30), (18, 31), (20, 30)]))
    def cell(s, x0, y0, w, h, door=None, bars=(), door_n=None, gap_w=(), gap_e=(), gate=None, arch6=None):
        """Xà lim / phòng tường mỏng: tường bắc (y0..y0+2), cột hai bên, tường nam (y0+h-3..y0+h-1).
        door/bars = vòm cửa / song sắt ở tường nam; door_n = vòm cửa ở tường bắc; gap_w/gap_e = các hàng để trống ở cột tây/đông."""
        s.hwall(x0, x0 + w - 1, y0, 'north', door=door_n)
        s.vpost(x0, y0 + 3, y0 + h - 4, gap_w); s.vpost(x0 + w - 1, y0 + 3, y0 + h - 4, gap_e)
        s.hwall(x0, x0 + w - 1, y0 + h - 3, 'south', door=door, bars=bars, join_l=True, join_r=True, gate=gate, arch6=arch6)
    # ---- dựng
    def build(s):
        k = s.k; W, H, rng = k.W, k.H, k.rng
        A = lambda x, y: (x, y) in s.area
        k.L['op_pit'] = [[s.ter.firstgid + rng.choice(PIT) if A(x, y) else 0 for x in range(W)] for y in range(H)]
        def wl(ts, name, M, rules, target):
            tb = ts.wang(name, 1); gm.legalize(M, tb, W, H); lay = gm.to_layer(M, tb, W, H, rng, ts.firstgid)
            if rules: lay = ta.Automapper([OPR / f for f in rules], [(t.firstgid, t.path) for t in k.tiles]).apply(lay, target, rng)
            return lay, tb
        k.L['op_plat'], tb1 = wl(s.w1, 'wall-1', s.Mplat, ["wall-1-rule1-places all tiles.tmx", "wall-1-rule2-applies variations.tmx"], 'wall-1')
        k.L['op_dirt'], _ = wl(s.ter, 'grass/dirt - transparency', s.Mdirt, None, None)
        k.L['op_blood'], _ = wl(s.bloodts, 'Blood pool - style 2 - with spikes - transparency', s.Mblood, None, None)
        k.L['op_spk'], _ = wl(s.ter, 'spikes', s.Mspk, None, None)
        k.L['op_walls'], tb2 = wl(s.w2, 'wall-2', s.Mwall, ["wall-2-rule1-places all tiles.tmx", "wall-2-rule2-applies variations.tmx"], 'wall-2')
        g = gm.grid(W, H)
        for (x, y), v in s.cells.items():
            if 0 <= x < W and 0 <= y < H: g[y][x] = v
        k.L['op_cells'] = g
        for n in ('op_plat', 'op_dirt', 'op_blood', 'op_spk', 'op_walls', 'op_cells'):      # cắt gọn trong vùng ngục
            for y in range(H):
                for x in range(W):
                    if (x, y) not in s.area: k.L[n][y][x] = 0
    ORDER = ['op_pit', 'op_plat', 'op_dirt', 'op_blood', 'op_spk', 'op_walls', 'op_cells']
