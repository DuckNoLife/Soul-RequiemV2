# 18/11 (kế hoạch §1): Ren bị vẽ ĐÈ lên vòm / khung cửa vì tường là lớp ô (order 0) còn Ren order 2.
# Cách sửa: cắt phần tường quanh mỗi cổng / cửa (cột cổng ±1, các hàng của cổng) thành MỘT hình prop (lớp props, order 2, xếp theo y như Ren),
# chân hình = chân tường -> Ren ở phía bắc / trong ô cửa bị tường che, đứng phía nam thì vẽ đè lên tường. Lòng vòm tối (khe cửa) làm trong suốt.
# Ảnh mới: Props/SR-trimmed/lintel_<cổng>.png, thêm vào SR-props-trimmed.tsx (tileset cuối, thêm id an toàn). Kiểm md5, sao lưu.
import hashlib, re, os, shutil, sys
import xml.etree.ElementTree as ET
from PIL import Image
ROOT = os.path.expanduser('~/mnt/Soul Requiem')
MAPS = ROOT + "/Assets/GamePackage/EPIC RPG World Pack - Crypt V.1.6/TiledMap Editor/Maps/"
TMX = MAPS + "map4_catacombs_of_naming.tmx"
TSX = ROOT + "/Assets/GamePackage/EPIC RPG World Pack - Crypt V.1.6/TiledMap Editor/Tilesets/SR-props-trimmed.tsx"
IMGDIR = ROOT + "/Assets/GamePackage/EPIC RPG World Pack - Crypt V.1.6/Props/SR-trimmed/"
b = open(TMX, 'rb').read(); assert hashlib.md5(b).hexdigest() == sys.argv[1], hashlib.md5(b).hexdigest()
BK = ROOT + "/_backups/2026-11-18_m4_lintels/"; os.makedirs(BK, exist_ok=True)
shutil.copy2(TMX, BK); shutil.copy2(TSX, BK)
s = b.decode('utf-8'); crlf = '\r\n' in s
if crlf: s = s.replace('\r\n', '\n')
root = ET.fromstring(s); MW = int(root.get('width')); T = 32
# --- nạp ô
tiles = {}
for ts in root.findall('tileset'):
    fg = int(ts.get('firstgid')); src = os.path.normpath(os.path.join(MAPS, ts.get('source'))); d = os.path.dirname(src)
    t = ET.parse(src).getroot(); ie = t.find('image')
    if ie is not None:
        tw, th = int(t.get('tilewidth')), int(t.get('tileheight')); cols = int(t.get('columns')); sp = int(t.get('spacing', 0)); mg = int(t.get('margin', 0))
        p = os.path.normpath(os.path.join(d, ie.get('source')))
        for i in range(int(t.get('tilecount'))): tiles[fg + i] = (p, (mg + (i % cols) * (tw + sp), mg + (i // cols) * (th + sp), mg + (i % cols) * (tw + sp) + tw, mg + (i // cols) * (th + sp) + th))
cache = {}
def tile_img(gid):
    g = gid & 0x1FFFFFFF
    if g not in tiles: return None
    p, box = tiles[g]
    if p not in cache: cache[p] = Image.open(p).convert('RGBA')
    im = cache[p].crop(box)
    if gid & 0x80000000: im = im.transpose(Image.FLIP_LEFT_RIGHT)
    if gid & 0x40000000: im = im.transpose(Image.FLIP_TOP_BOTTOM)
    return im
WALL_LAYERS = ['op_walls', 'op_cells']
def layer_rows(name):
    m = re.search(r'(<layer id="\d+" name="%s"[^>]*>.*?<data encoding="csv">\n)(.*?)(\n</data>)' % re.escape(name), s, re.S)
    return m, [r.rstrip(',').split(',') for r in m.group(2).split('\n')]
data = {n: layer_rows(n)[1] for n in WALL_LAYERS}
gates = {}
for o in root.iter('object'):
    if o.get('type') == 'gate': gates[o.get('name')] = tuple(float(o.get(k)) / T for k in ('x', 'y', 'width', 'height'))
DO = ['ren_gate', 'pell_gate', 'oub_door', 'guard_door']
tsx = open(TSX, encoding='utf-8').read(); nxt = int(re.search(r'tilecount="(\d+)"', tsx).group(1))
FG = 10372
new_objs = ''; nid = int(re.search(r'nextobjectid="(\d+)"', s).group(1))
for name in DO:
    gx, gy, gw, gh = gates[name]
    c0, c1 = int(gx) - 1, int(round(gx + gw)) + 1               # cột [c0, c1)
    r0, r1 = int(gy), int(round(gy + gh))                         # hàng [r0, r1)
    W, H = (c1 - c0) * T, (r1 - r0) * T
    im = Image.new('RGBA', (W, H), (0, 0, 0, 0)); n = 0
    for ln in WALL_LAYERS:
        rows = data[ln]
        for y in range(r0, r1):
            for x in range(c0, c1):
                gid = int(rows[y][x])
                if not gid: continue
                ti = tile_img(gid)
                if ti is None: continue
                im.alpha_composite(ti, ((x - c0) * T, (y - r0) * T + T - ti.height)); rows[y][x] = '0'; n += 1
    # lòng cửa: loang từ giữa đáy, chỉ trong bề ngang cổng, màu gần màu điểm gốc (hốc tối) -> trong suốt
    px = im.load(); sx = int((gx + gw / 2 - c0) * T); sy = H - 2
    base = px[sx, sy]; xa, xb = int((gx - c0) * T), int((gx + gw - c0) * T)
    def near(p): return p[3] == 0 or (abs(p[0] - base[0]) + abs(p[1] - base[1]) + abs(p[2] - base[2]) < 40)
    st = [(sx, sy)]; seen = {(sx, sy)}; cleared = 0
    while st:
        x, y = st.pop()
        if not near(px[x, y]): continue
        px[x, y] = (0, 0, 0, 0); cleared += 1
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            q = (x + dx, y + dy)
            if xa <= q[0] < xb and 0 <= q[1] < H and q not in seen: seen.add(q); st.append(q)
    fn = 'lintel_%s.png' % name; im.save(IMGDIR + fn)
    import uuid
    meta = open(IMGDIR + 'op_door_h.png.meta', encoding='utf-8').read()
    open(IMGDIR + fn + '.meta', 'w', encoding='utf-8', newline='').write(re.sub(r'guid: \w+', 'guid: ' + uuid.uuid4().hex, meta, count=1))
    tid = nxt; nxt += 1
    tsx = tsx.replace('</tileset>', ' <tile id="%d">\n  <image source="../../Props/SR-trimmed/%s" width="%d" height="%d"/>\n </tile>\n</tileset>' % (tid, fn, W, H))
    new_objs += '  <object id="%d" name="lintel_%s" gid="%d" x="%d" y="%d" width="%d" height="%d"/>\n' % (nid, name, FG + tid, c0 * T, r1 * T, W, H); nid += 1
    print(name, 'ô tường chuyển:', n, 'điểm lòng cửa trong suốt:', cleared, 'màu gốc', base, 'cột', c0, c1, 'hàng', r0, r1)
tsx = re.sub(r'tilecount="\d+"', 'tilecount="%d"' % nxt, tsx, count=1)
for ln in WALL_LAYERS:
    m, _ = layer_rows(ln); rows = data[ln]
    txt = '\n'.join(','.join(r) + (',' if i < len(rows) - 1 else '') for i, r in enumerate(rows))
    s = s[:m.start(2)] + txt + s[m.end(2):]
m = re.search(r'<objectgroup id="\d+" name="props"[^>]*>\n', s); e = s.index(' </objectgroup>', m.end())
s = s[:e] + new_objs + s[e:]
s = re.sub(r'nextobjectid="\d+"', 'nextobjectid="%d"' % nid, s, count=1)
if crlf: s = s.replace('\n', '\r\n')
open(TMX, 'wb').write(s.encode('utf-8')); open(TSX, 'w', encoding='utf-8', newline='').write(tsx)
print('md5', hashlib.md5(s.encode('utf-8')).hexdigest())
