# CK — bộ dựng địa hình gói Crypt theo ĐÚNG cách tác giả ghép (Sample Map.tmx + Rules/*.tmx). 17/11/2026.
#   rock  : khối đá + mặt gạch 3 hàng (wang wall-111 + luật wall-1)            -> mọi ô không phải sàn
#   plat  : bệ nổi đi được trên mặt (wang wall-2 + luật wall-2), mép có lan can (luật balluster1-wall2), mặt gạch 3 hàng phía nam
#   stamp : cụm ô cắt nguyên từ Sample Map (vòm cuốn, hốc thờ, cửa vòm, bậc thang)
# Toạ độ ô, y xuống. Mặt nạ là lưới GÓC (W+1)x(H+1).
import sys, random
import xml.etree.ElementTree as ET
from pathlib import Path
CAT = Path.home() / "mnt/Catalog"; sys.path.insert(0, str(CAT)); sys.path.insert(0, str(CAT / "v2"))
import generate_map as gm, tiled_automap as ta
from mapkit import Tileset
GP = Path.home() / "mnt/Soul Requiem/Assets/GamePackage"
CRYPT = GP / "EPIC RPG World Pack - Crypt V.1.6"
ED = CRYPT / "TiledMap Editor"
TSD = ED / "Tilesets"; RULES = ED / "Rules"
SPECIAL = {1: "Ignore", 2: "NonEmpty", 3: "Empty"}

class Sample:
    """Map mẫu của tác giả: đọc lớp tile theo (tên tsx, id cục bộ) để cắt khuôn."""
    def __init__(s, path):
        r = ET.parse(path).getroot(); s.W = int(r.get('width')); s.H = int(r.get('height'))
        s.ts = sorted((int(t.get('firstgid')), Path(t.get('source')).name) for t in r.findall('tileset'))
        s.L = {l.get('name'): [int(v) for v in l.find('data').text.replace('\n', '').split(',') if v.strip()] for l in r.iter('layer')}
    def loc(s, g):
        g &= 0x0fffffff
        if not g: return None
        f = [t for t in s.ts if t[0] <= g][-1]; return (f[1], g - f[0])
    def cut(s, layer, x0, y0, w, h, only=None):
        out = []
        for dy in range(h):
            for dx in range(w):
                t = s.loc(s.L[layer][(y0 + dy) * s.W + x0 + dx])
                if t and (only is None or t[0] == only): out.append((dx, dy, t))
        return out

SM = Sample(ED / "Sample Map.tmx")
T_ = 'Tileset-Terrain.tsx'
# khuôn: tên -> [(dx, dy, (tsx, id))]; mốc (0,0) = góc trên-trái; ghi chú hàng: 3 hàng mặt gạch = dy 0..2 (trừ khi nói khác)
STAMPS = {
    'arcade5': SM.cut('wall-details', 4, 0, 11, 3),            # 5 vòm cuốn, rộng 11
    'arcade5_grates': SM.cut('wall-details2', 4, 0, 11, 3),    # song sắt nhỏ dưới 2 vòm (phủ lên arcade5)
    'arcade5b': SM.cut('wall-details', 24, 0, 11, 3),
    'arcade5b_grates': SM.cut('wall-details2', 24, 0, 11, 3),
    'shrine_a': SM.cut('wall-details', 15, 0, 2, 4),           # hốc thờ có mái (cao 4: thò xuống sàn 1 hàng)
    'shrine_b': SM.cut('wall-details', 22, 0, 2, 4),
    'door_arch': SM.cut('wall-1-2-3', 18, 1, 3, 3, only=T_),   # cửa vòm tối 3x3 (hàng mặt gạch 2..3 + 1)
    'arcade4_broken': SM.cut('wall-details', 3, 13, 10, 3),    # 4 vòm, đầu phải gãy (trên mặt bệ)
    'niche_a': SM.cut('wall-details', 27, 13, 3, 3),           # hốc vòm tròn có tượng
    'niche_b': SM.cut('wall-details', 31, 13, 3, 3),
    'stairs5': SM.cut('wall-1-2-3', 17, 7, 5, 4, only=T_),     # bậc thang rộng 5 (3 ô đi được ở giữa), cao 4 hàng
    'bridge': SM.cut('pillar', 14, 12, 11, 3),                 # nhịp cầu vòm bắc qua lối đi giữa hai bệ
}

class CK:
    def __init__(s, W, H, seed=4):
        s.W, s.H = W, H; s.rng = random.Random(seed); s.noise = gm.Noise(seed)
        s.ter = Tileset(TSD / "Tileset-Terrain.tsx"); s.w1 = Tileset(TSD / "Tileset - wall 1.tsx")
        s.w2 = Tileset(TSD / "Tileset - wall 2.tsx"); s.w3 = Tileset(TSD / "Tileset - wall 3.tsx")
        s.tiles = [s.ter, s.w1, s.w2, s.w3]
        g = 1
        for t in s.tiles: t.firstgid = g; g += t.span
        s.next_gid = g
        s.floor = set()                       # ô sàn hầm mộ (kể cả mặt bệ)
        s.area = set()                        # ô thuộc vùng hầm mộ (sàn + đá quanh) — ngoài vùng này CK không vẽ gì
        s.Mp = gm.grid(W + 1, H + 1)          # bệ (wall-2)
        s.M = {k: gm.grid(W + 1, H + 1) for k in ('g2', 'slope', 'carpet', 'dirt')}
        s.rail_skip = set()                   # ô mép bệ KHÔNG đặt lan can
        s.stairs = []                         # (x, y_ring) góc trên-trái khuôn stairs5
        s.pillars = []
        s.deco = {}; s.deco2 = {}; s.over = {}  # (x,y) -> gid : chi tiết mặt vách / lớp phủ trên đầu (nhịp cầu)
        s.block = set(); s.walk = set()
        s.L = {}
    def gid(s, t):
        for ts in s.tiles:
            if Path(ts.path).name == t[0]: return ts.firstgid + t[1]
        raise KeyError(t)
    # ---- khai báo
    def room(s, x, y, w, h):
        for j in range(y, y + h):
            for i in range(x, x + w): s.floor.add((i, j))
    def passage(s, x, y, w, h):
        """Lối xuyên đá sau cửa: là sàn và luôn đi được (mép bệ chìm không chặn)."""
        s.room(x, y, w, h)
        for j in range(y - 1, y + h + 1):
            for i in range(x - 1, x + w + 1):
                if y <= j < y + h and x <= i < x + w: s.walk.add((i, j))
                s.rail_skip.add((i, j))                      # không lan can quanh miệng lối
    def pillar(s, x, y, w=2, h=1):
        """Trụ vuông (wall-3: đỉnh tối + mặt gạch 2 hàng) như sân trụ đuốc xanh của mẫu. Góc mặt nạ x..x+w, y..y+h;
        hình chiếm khoảng (x-0.5..x+w+0.5) x (y-0.5..y+h+2.5); ô bị chặn = ô có hình (tính khi build)."""
        s.pillars.append((x, y, w, h))
    def zone_area(s, x, y, w, h):
        for j in range(y, y + h):
            for i in range(x, x + w):
                if 0 <= i < s.W and 0 <= j < s.H: s.area.add((i, j))
    def _corn(s, M, x, y, w, h):
        for cy in range(max(0, y), min(s.H, y + h) + 1):
            for cx in range(max(0, x), min(s.W, x + w) + 1): M[cy][cx] = 1
    def plat(s, x, y, w, h, attach=''):
        """Bệ nổi: mặt bệ đi được = ô [x,x+w) x [y,y+h). attach: 'n','w','e' = cạnh đó áp vào vách đá (không viền, không lan can)."""
        x0, y0, x1, y1 = x, y, x + w, y + h
        if 'n' in attach: y0 = max(0, y0 - 4)          # mép bệ chìm vào dải đá phía trên mặt gạch
        if 'w' in attach: x0 -= 2
        if 'e' in attach: x1 += 2
        s._corn(s.Mp, x0, y0, x1 - x0, y1 - y0)
    def mask(s, key, x, y, w, h): s._corn(s.M[key], x, y, w, h)
    def band(s, key, cx, y, w, h):
        """Dải wang (thảm, viền) CĂN GIỮA theo cx (toạ độ ô, vd tâm bậc thang). Mặt nạ góc: tâm hình = x + w/2."""
        s._corn(s.M[key], int(round(cx - w / 2)), y, w, h)
    def stair(s, x, y_ring):
        """Bậc thang 5 ô (x..x+4), hàng trên cùng = hàng mép nam của bệ (hàng ngay dưới mặt bệ)."""
        s.stairs.append((x, y_ring))
        for i in range(x, x + 5):
            for j in range(y_ring - 1, y_ring + 1): s.rail_skip.add((i, j))
    def niche(s, x, top, col, w=2, where='deco'):
        """Hốc tường 2x3 cắt thẳng từ Tileset-Terrain hàng 25..27 (col 16/18/20/22 khung trống, 24/26/28 tủ cao, 30..38 miếu nhỏ)."""
        d = getattr(s, where)
        for j in range(3):
            for i in range(w): d[(x + i, top + j)] = s.ter.firstgid + (25 + j) * 50 + col + i
    def stamp(s, name, x, y, where='deco'):
        d = getattr(s, where)
        for dx, dy, t in STAMPS[name]: d[(x + dx, y + dy)] = s.gid(t)
    # ---- dựng
    def _rules(s, files, target):
        return ta.Automapper([RULES / f for f in files], [(t.firstgid, t.path) for t in s.tiles])
    def _multi(s, rule_file, grids, out_name):
        """Luật 2 lớp vào (input_<lớp>) -> 1 lớp ra; MatchInOrder."""
        r = ET.parse(RULES / rule_file).getroot(); RW, RH = int(r.get('width')), int(r.get('height'))
        rts = sorted((int(t.get('firstgid')), Path(t.get('source')).name) for t in r.findall('tileset'))
        def loc(g):
            if not g: return None
            f = [t for t in rts if t[0] <= g][-1]
            return ('auto', g - f[0]) if 'automap' in f[1] else (f[1], g - f[0])
        RL = {l.get('name'): [int(v) for v in l.find('data').text.replace('\n', '').split(',') if v.strip()] for l in r.iter('layer')}
        ins = {n.split('_', 1)[1]: d for n, d in RL.items() if n.startswith('input_')}
        outd = RL['output_' + out_name]
        used = {(i % RW, i // RW) for d in list(ins.values()) + [outd] for i, g in enumerate(d) if g}
        seen = set(); comps = []
        for c in sorted(used, key=lambda c: (c[1], c[0])):
            if c in seen: continue
            st = [c]; seen.add(c); comp = []
            while st:
                p = st.pop(); comp.append(p)
                for d in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    q = (p[0] + d[0], p[1] + d[1])
                    if q in used and q not in seen: seen.add(q); st.append(q)
            comps.append(comp)
        loc_map = lambda g: next(((Path(t.path).name, (g & 0x0fffffff) - t.firstgid) for t in reversed(s.tiles) if t.firstgid <= (g & 0x0fffffff)), None) if g else None
        G = grids[out_name]; n = 0
        for comp in comps:
            ox, oy = min(p[0] for p in comp), min(p[1] for p in comp)
            conds = [(x - ox, y - oy, ln, loc(d[y * RW + x])) for (x, y) in comp for ln, d in ins.items() if d[y * RW + x]]
            outs = [(x - ox, y - oy, loc(outd[y * RW + x])) for (x, y) in comp if outd[y * RW + x]]
            if not conds or not outs: continue
            for y in range(-2, s.H + 2):
                for x in range(-2, s.W + 2):
                    ok = True
                    for dx, dy, ln, c in conds:
                        xx, yy = x + dx, y + dy
                        cur = loc_map(grids[ln][yy][xx]) if 0 <= xx < s.W and 0 <= yy < s.H else None
                        if c[0] == 'auto':
                            k = SPECIAL.get(c[1], 'Ignore')
                            if (k == 'NonEmpty' and cur is None) or (k == 'Empty' and cur is not None): ok = False; break
                        elif cur != c: ok = False; break
                    if not ok: continue
                    for dx, dy, t in outs:
                        xx, yy = x + dx, y + dy
                        if 0 <= xx < s.W and 0 <= yy < s.H and t[0] != 'auto': G[yy][xx] = s.gid(t); n += 1
        return n
    def build(s):
        W, H, rng, T = s.W, s.H, s.rng, s.ter
        inA = lambda x, y: (x, y) in s.area
        # ---- nền
        g1 = T.wang("ground1 to ground2", 1)[(1, 1, 1, 1)]
        s.L['ground'] = [[T.firstgid + gm.pick(rng, g1) if inA(x, y) else 0 for x in range(W)] for y in range(H)]
        def wang_layer(ts, wsname, M):
            tb = ts.wang(wsname, 1); gm.legalize(M, tb, W, H)
            return gm.to_layer(M, tb, W, H, rng, ts.firstgid), tb
        # nền xương: wangset 'ground1 to dirt' màu 1 = ground1 -> đảo mặt nạ; ô (0,0,0,0) = đất xương (Terrain hàng 21..23, cột 26..30)
        Md = s.M['dirt']; near = set()
        for cy in range(H + 1):
            for cx in range(W + 1):
                if Md[cy][cx]:
                    for dy in range(-2, 2):
                        for dx in range(-2, 2): near.add((cx + dx, cy + dy))
        Mi = [[0 if Md[cy][cx] else 1 for cx in range(W + 1)] for cy in range(H + 1)]
        tbd = T.wang("ground1 to dirt", 1); gm.legalize(Mi, tbd, W, H)
        DIRT = [T.firstgid + r * 50 + c for r in (21, 22, 23) for c in range(26, 31)]
        s.L['dirt'] = [[0] * W for _ in range(H)]
        for y in range(H):
            for x in range(W):
                if (x, y) not in near or not inA(x, y): continue
                key = gm.cell_key(Mi, x, y)
                if key == (0, 0, 0, 0): s.L['dirt'][y][x] = rng.choice(DIRT)
                elif key in tbd: s.L['dirt'][y][x] = T.firstgid + gm.pick(rng, tbd[key])
        s.L['ground2'], _ = wang_layer(T, "ground2 to transparency", s.M['g2'])
        s.L['slope'], _ = wang_layer(T, "slope-transparency", s.M['slope'])
        s.L['carpet'], _ = wang_layer(T, "path-transparency", s.M['carpet'])
        # ---- bệ (wall-2) + lan can
        base, tb2 = wang_layer(s.w2, "wall-2", s.Mp)
        full2 = {s.w2.firstgid + t for t, _ in tb2.get((1, 1, 1, 1), [])}
        MARK = T.firstgid + 1852
        rail = gm.grid(W, H)
        for y in range(H):
            for x in range(W):
                if base[y][x] and base[y][x] not in full2 and (x, y) not in s.rail_skip and (x, y) in s.floor: rail[y][x] = MARK   # chỉ mép bệ nằm trên sàn (cạnh áp vách không có lan can)
        marks = {(x, y) for y in range(H) for x in range(W) if rail[y][x]}
        hit = s._multi("balluster1-wall2-rule1-places all tiles.tmx", {'wall-1-2-3': base, 'balluster-1': rail}, 'balluster-1')
        written = {(x, y) for y in range(H) for x in range(W) if rail[y][x] and rail[y][x] != MARK}
        for (x, y) in marks:                      # ô đánh dấu không khớp luật nào -> bỏ
            if rail[y][x] == MARK and (x, y) not in s._rail_true: rail[y][x] = 0
        try: rail = s._rules(["balluster1-wall2-rule2-dead ends and variations.tmx"], 'balluster-1').apply(rail, "balluster-1", random.Random(11))
        except Exception as ex: print('  (bỏ qua dead-ends lan can:', ex, ')')
        platL = s._rules(["wall-2-rule1-places all tiles.tmx", "wall-2-rule2-applies variations.tmx"], 'x').apply([r[:] for r in base], "wall-1-2-3", rng)
        for y in range(H):                        # mặt bệ: ô đầy trong suốt -> bỏ ô (đi được); phần còn lại chặn
            for x in range(W):
                if platL[y][x] in full2 and base[y][x] in full2: pass
        s.plat_block = {(x, y) for y in range(H) for x in range(W) if platL[y][x] and not (base[y][x] in full2 and platL[y][x] == base[y][x])}
        s.plat_top = {(x, y) for y in range(H) for x in range(W) if base[y][x] in full2}
        for (x, y) in list(s.plat_block):
            if base[y][x] in full2: s.plat_block.discard((x, y))
        # bậc thang: thay ô mặt bệ, mở lối
        for (sx, sy) in s.stairs:
            for dx, dy, t in STAMPS['stairs5']:
                if 0 <= sx + dx < W and 0 <= sy + dy < H: platL[sy + dy][sx + dx] = s.gid(t)
            for dy in range(4):
                for dx in range(5):
                    c = (sx + dx, sy + dy); rail[c[1]][c[0]] = 0
                    if 1 <= dx <= 3: s.plat_block.discard(c); s.walk.add(c)
                    else: s.plat_block.add(c)
            for dx in range(5):
                if rail[sy - 1][sx + dx]: rail[sy - 1][sx + dx] = 0
        s.L['plat'] = platL; s.L['rails'] = rail
        # trụ wall-3
        Mp3 = gm.grid(W + 1, H + 1)
        for (x, y, w, h) in s.pillars:
            for cy in range(y, y + h + 1):
                for cx in range(x, x + w + 1): Mp3[cy][cx] = 1
        tb3 = s.w3.wang('wall-3', 1); gm.legalize(Mp3, tb3, W, H); p3 = gm.to_layer(Mp3, tb3, W, H, rng, s.w3.firstgid)
        s.L['pil'] = s._rules(["wall-3-rule1-places all tiles.tmx", "wall-3-rule2-applies variations.tmx"], 'x').apply(p3, "wall-1-2-3", rng)
        s.pil_block = {(x, y) for y in range(H) for x in range(W) if s.L['pil'][y][x]}
        # ---- đá (wall-1): mọi ô trong vùng không phải sàn
        solid = {c for c in s.area if c not in s.floor}
        S2 = lambda x, y: (x, y) in solid or not inA(x, y)
        mass = {c for c in solid if S2(c[0], c[1] + 1) and S2(c[0], c[1] + 2)}
        M2 = lambda x, y: (x, y) in mass or not inA(x, y)
        Mw = gm.grid(W + 1, H + 1)
        for cy in range(H + 1):
            for cx in range(W + 1):
                cells = ((cx - 1, cy - 1), (cx, cy - 1), (cx - 1, cy), (cx, cy))
                if any(inA(*c) for c in cells) and all(M2(*c) for c in cells): Mw[cy][cx] = 1
        tw = s.w1.wang("wall-111", 1); gm.legalize(Mw, tw, W, H)
        wl = gm.to_layer(Mw, tw, W, H, rng, s.w1.firstgid)
        s.L['rock'] = s._rules(["wall-1-rule1-places all tiles.tmx", "wall-1-rule2-applies variations.tmx"], 'x').apply(wl, "wall-1-2-3", rng)
        for nm in ('deco', 'deco2', 'over'):
            g = gm.grid(W, H)
            for (x, y), v in getattr(s, nm).items():
                if 0 <= x < W and 0 <= y < H: g[y][x] = v
            s.L[nm] = g
        s.solid = solid
        for nm, g in s.L.items():                 # cắt gọn trong vùng hầm mộ (không lòi mặt gạch sang vùng ngục)
            for y in range(H):
                for x in range(W):
                    if (x, y) not in s.area: g[y][x] = 0
        s.blocked = (solid | s.plat_block | s.block | s.pil_block) - s.walk
    _rail_true = set()
    def floor_or_face(s): return s.area
    ORDER = ['ground', 'dirt', 'ground2', 'slope', 'carpet', 'plat', 'pil', 'rails', 'rock', 'deco', 'deco2', 'over']
