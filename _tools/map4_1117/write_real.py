# Map 4 — GHI .tmx THẬT vào Assets (chỉ chạy khi check.py + audit.py đều ĐẠT). 17/11/2026.
#   map: <Crypt>/TiledMap Editor/Maps/map4_catacombs_of_naming.tmx (+ .meta sortingMode 1 nếu chưa có)
#   ảnh cửa/cơ quan: <Crypt>/Props/SR-m4-art/*.png + tileset <Crypt>/TiledMap Editor/Tilesets/SR-m4-art.tsx
import sys, os, shutil, hashlib, uuid, re; sys.path.insert(0, '.')
from pathlib import Path
import m4_setup as S
import decor2 as D
import logic                                                   # §304: khu, điểm nghỉ, cổng logic, tời, chìa, ấn, lối ra
import traps                                                   # §305: bẫy
import story                                                   # §306: cốt truyện (Pell, cảnh, ghi chép, thuộc tính map)
import enemies                                                 # §307: điểm sinh quái + vùng trận boss
import audit as A
from m4lib import make_art_tsx, ART
F = S.F; k = F.k
GP = Path.home() / "mnt/Soul Requiem/Assets/GamePackage"
CR = GP / "EPIC RPG World Pack - Crypt V.1.6"
OUT = CR / "TiledMap Editor/Maps/map4_catacombs_of_naming.tmx"
assert sum(len(v) for v in A.errs.values()) == 0, 'audit còn lỗi'
# 1) ảnh + tileset SR-m4-art trong Assets (cùng thứ tự id với bản xem trước)
artdir = CR / "Props/SR-m4-art"; artdir.mkdir(exist_ok=True)
files = []
for a in S.ARTS:
    dst = artdir / (a + '.png')
    if not dst.exists() or dst.read_bytes() != (ART / (a + '.png')).read_bytes(): shutil.copyfile(ART / (a + '.png'), dst)
    files.append(str(dst))
tsx = CR / "TiledMap Editor/Tilesets/SR-m4-art.tsx"
make_art_tsx(str(tsx), files)
S.objs.ts['art'].path = tsx
# 2) khối chặn địa hình: mọi ô không đứng được (gộp hình chữ nhật) + biên map
W, H = F.W, F.H
solid = [[A.CLS[(x, y)] not in 'FP' and (x, y) not in logic.BRIDGE for x in range(W)] for y in range(H)]
used = [[False] * W for _ in range(H)]; T = 32
for y in range(H):
    for x in range(W):
        if not solid[y][x] or used[y][x]: continue
        w = 1
        while x + w < W and solid[y][x + w] and not used[y][x + w]: w += 1
        h = 1
        while y + h < H and all(solid[y + h][x + i] and not used[y + h][x + i] for i in range(w)): h += 1
        for yy in range(y, y + h):
            for xx in range(x, x + w): used[yy][xx] = True
        S.objs.obj('blockers', 'blocker', 'blocker', x * T, y * T, w * T, h * T, props=[('collider_kind', 'terrain')])
for (x, y, w, h) in [(-T, -T, W * T + 2 * T, T), (-T, H * T, W * T + 2 * T, T), (-T, 0, T, H * T), (W * T, 0, T, H * T)]:
    S.objs.obj('map bounds', 'map_bound', 'bound', x, y, w, h, props=[('collider_kind', 'bound')])
# 3) ghi
LORD = {'op_pit': -30, 'op_plat': -25, 'op_dirt': -24, 'op_blood': -23, 'op_spk': -22, 'ground': -20, 'dirt': -19, 'ground2': -18, 'slope': -17,
        'carpet': -16, 'plat': -10, 'pil': 0, 'rails': 0, 'op_walls': 0, 'op_cells': 0, 'rock': 0, 'deco': 1, 'deco2': 1, 'over': 20}
GORD = {'ground decals': -5, 'gates_floor': -4, 'wall props': 1, 'props': 2, 'props front': 2, 'gates': 2, 'mechanics_art': 2, 'ambient fx': 10}
TRIG = {'zones', 'triggers', 'mechanics'}
prop = lambda n, v: '   <property name="%s" type="%s" value="%s"/>' % (n, 'int' if isinstance(v, int) and not isinstance(v, bool) else 'bool', str(v).lower() if isinstance(v, bool) else v)
out = ['<?xml version="1.0" encoding="UTF-8"?>',
       '<map version="1.10" tiledversion="1.11.2" orientation="orthogonal" renderorder="right-down" width="%d" height="%d" tilewidth="32" tileheight="32" infinite="0" nextlayerid="100" nextobjectid="%d">' % (W, H, S.objs.nid + 1)]
out += [' <properties>'] + ['  <property name="%s" value="%s"/>' % (k_, v_) for k_, v_ in story.MAP_PROPS] + [' </properties>']   # §306: thuộc tính map (MapRestraint)
for t in S.tilesets:
    out.append(' <tileset firstgid="%d" source="%s"/>' % (t.firstgid, os.path.relpath(str(t.path), OUT.parent).replace('\\', '/').replace('&', '&amp;')))
lid = 0
for n in F.ORDER:
    lid += 1; g = k.L[n]
    out += [' <layer id="%d" name="%s" width="%d" height="%d">' % (lid, n, W, H), '  <properties>', prop('unity:SortingOrder', LORD[n]), '  </properties>',
            '  <data encoding="csv">', ',\n'.join(','.join(str(v) for v in r) for r in g), '</data>', ' </layer>']
for gname in S.objs.GROUPS:
    lid += 1; items = S.objs.G[gname]
    if gname in GORD: items = sorted(items, key=lambda t: t[0])
    op = ' opacity="0.35"' if gname in S.objs.LOGIC else ''
    out.append(' <objectgroup id="%d" name="%s"%s>' % (lid, 'gates' if gname == 'gates_floor' else gname, op))   # §304: 2 lớp tên 'gates' (MapLayout đọc theo tên)
    pp = ([prop('unity:SortingOrder', GORD[gname])] if gname in GORD else []) + ([prop('unity:IsTrigger', True)] if gname in TRIG else [])
    if pp: out += ['  <properties>'] + pp + ['  </properties>']
    out += [x for _, x in items]; out.append(' </objectgroup>')
out.append('</map>')
if OUT.exists():
    bk = Path.home() / "mnt/Soul Requiem/_backups/2026-11-17_map4_write"; bk.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(OUT, bk / ('%s_%s' % (hashlib.md5(OUT.read_bytes()).hexdigest()[:8], OUT.name)))   # §304: mỗi bản cũ một tên theo md5, không ghi đè
OUT.write_text('\n'.join(out) + '\n', newline='\n')
meta = Path(str(OUT) + '.meta')
if not meta.exists():
    ref = (GP / "EPIC RPG World - Sewers V1.5/TiledMap Editor/Maps/map2b_kroven_deep.tmx.meta").read_text()
    meta.write_text(re.sub(r'guid: [0-9a-f]+', 'guid: ' + uuid.uuid4().hex, ref, count=1))
print('ghi', OUT.name, 'md5', hashlib.md5(OUT.read_bytes()).hexdigest(), 'object', S.objs.nid - 1)
