# 18/11 14h (§321): sửa phản hồi chạy thử 14:02.
#  1) 10 bẫy mz_* mới (§319) thiếu unity:IsTrigger -> ST2U dựng va chạm ĐẶC = tường vô hình (cưa E3 chắn ngang hành lang: "không bước qua được").
#  2) Vòm ô tường không có va chạm -> Ren bước vào giữa mặt tường, bị vẽ đè lên vòm ("trèo lên cổng"):
#     - vòm vào ô giam cụt (86,122) (100,122) và vòm cụt đầu Rolling Hall (71,100): thêm khối chặn phủ cả vòm.
#     - vòm là lối đi thật (81,130 -> phòng giữ chìa khoá): cắt thành hình lintel (lớp props, chân = chân tường) như 4 cổng §318.
#  3) Chưa cứu Pell đã đi được sang Rolling Hall / Blade Yard: thêm rào gỗ "west_barricade" chắn đầu hành lang tây (x 76-78, y 125-130),
#     mở bằng cờ west_barricade_down đặt trong cảnh pell_freed (lúc màn tối, Pell dỡ rào từ phía cống).
import hashlib, re, os, shutil, sys, uuid
import xml.etree.ElementTree as ET
from PIL import Image
ROOT = os.path.expanduser('~/mnt/Soul Requiem')
MAPS = ROOT + "/Assets/GamePackage/EPIC RPG World Pack - Crypt V.1.6/TiledMap Editor/Maps/"
TMX = MAPS + "map4_catacombs_of_naming.tmx"
TSX = ROOT + "/Assets/GamePackage/EPIC RPG World Pack - Crypt V.1.6/TiledMap Editor/Tilesets/SR-props-trimmed.tsx"
IMGDIR = ROOT + "/Assets/GamePackage/EPIC RPG World Pack - Crypt V.1.6/Props/SR-trimmed/"
b = open(TMX, 'rb').read(); assert hashlib.md5(b).hexdigest() == '2a8bb75428b5ec9e43ffeb3ec7093e66', hashlib.md5(b).hexdigest()
BK = ROOT + "/_backups/2026-11-18_m4_fix1402/"; os.makedirs(BK, exist_ok=True)
shutil.copy2(TMX, BK); shutil.copy2(TSX, BK)
s = b.decode('utf-8'); crlf = '\r\n' in s
if crlf: s = s.replace('\r\n', '\n')
T = 32
nid = int(re.search(r'nextobjectid="(\d+)"', s).group(1))

# ---- 1) IsTrigger cho bẫy mới
TRIG = '    <property name="unity:IsTrigger" type="bool" value="true"/>\n'
cnt = 0
for oid in range(1203, 1213):
    m = re.search(r'<object id="%d" [^>]*>\n   <properties>\n' % oid, s)
    assert m, oid
    s = s[:m.end()] + TRIG + s[m.end():]; cnt += 1
print('IsTrigger thêm cho', cnt, 'bẫy')

# ---- 2a) khối chặn vòm cụt
blk = ''
for (x, y, w, h, why) in [(86, 122, 2, 3, 'vòm ô giam cụt tây Pell'), (100, 122, 2, 3, 'vòm ô giam cụt đông Pell'), (71, 100, 2, 3, 'vòm cụt đầu Rolling Hall')]:
    blk += '  <object id="%d" name="arch_seal" type="blocker" x="%d" y="%d" width="%d" height="%d"/>\n' % (nid, x * T, y * T, w * T, h * T); nid += 1
m = re.search(r'<objectgroup id="\d+" name="blockers"[^>]*>\n', s); e = s.index(' </objectgroup>', m.end())
s = s[:e] + blk + s[e:]

# ---- 2b) lintel cho vòm lối đi (81,130)
root = ET.fromstring(s)
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
tsx = open(TSX, encoding='utf-8').read(); nxt = int(re.search(r'tilecount="(\d+)"', tsx).group(1)); FG = 10372
props_new = ''
for name, (gx, gy, gw, gh) in [('arch_warden', (81, 130, 2, 3))]:
    c0, c1, r0, r1 = gx - 1, gx + gw + 1, gy, gy + gh
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
    px = im.load(); sx = int((gx + gw / 2 - c0) * T); sy = H - 2; base = px[sx, sy]
    xa, xb = int((gx - c0) * T), int((gx + gw - c0) * T)
    def near(p): return p[3] == 0 or abs(p[0] - base[0]) + abs(p[1] - base[1]) + abs(p[2] - base[2]) < 40
    st = [(sx, sy)]; seen = {(sx, sy)}; cleared = 0
    while st:
        x, y = st.pop()
        if not near(px[x, y]): continue
        px[x, y] = (0, 0, 0, 0); cleared += 1
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            q = (x + dx, y + dy)
            if xa <= q[0] < xb and 0 <= q[1] < H and q not in seen: seen.add(q); st.append(q)
    fn = 'lintel_%s.png' % name; im.save(IMGDIR + fn)
    meta = open(IMGDIR + 'op_door_h.png.meta', encoding='utf-8').read()
    open(IMGDIR + fn + '.meta', 'w', encoding='utf-8', newline='').write(re.sub(r'guid: \w+', 'guid: ' + uuid.uuid4().hex, meta, count=1))
    tid = nxt; nxt += 1
    tsx = tsx.replace('</tileset>', ' <tile id="%d">\n  <image source="../../Props/SR-trimmed/%s" width="%d" height="%d"/>\n </tile>\n</tileset>' % (tid, fn, W, H))
    props_new += '  <object id="%d" name="lintel_%s" gid="%d" x="%d" y="%d" width="%d" height="%d"/>\n' % (nid, name, FG + tid, c0 * T, r1 * T, W, H); nid += 1
    print(name, 'ô tường chuyển:', n, 'điểm lòng vòm trong suốt:', cleared, 'màu gốc', base)
tsx = re.sub(r'tilecount="\d+"', 'tilecount="%d"' % nxt, tsx, count=1)
for ln in WALL_LAYERS:
    m, _ = layer_rows(ln); rows = data[ln]
    txt = '\n'.join(','.join(r) + (',' if i < len(rows) - 1 else '') for i, r in enumerate(rows))
    s = s[:m.start(2)] + txt + s[m.end(2):]
m = re.search(r'<objectgroup id="\d+" name="props"[^>]*>\n', s); e = s.index(' </objectgroup>', m.end())
s = s[:e] + props_new + s[e:]

# ---- 3) rào gỗ chắn hành lang tây (cổng; các hình cùng tên = phần cổng, mở thì ẩn)
def tobj(oid, gid, x, ybottom, w, h):
    return '  <object id="%d" name="west_barricade" type="gate_visual" gid="%d" x="%g" y="%g" width="%d" height="%d"/>\n' % (oid, gid, x * T, ybottom * T, w, h)
gate = ('  <object id="%d" name="west_barricade" type="gate" x="%d" y="%d" width="%d" height="%d">\n   <properties>\n'
        '    <property name="collider_kind" value="gate"/>\n    <property name="starts_closed" type="bool" value="true"/>\n'
        '    <property name="unlock_when" value="west_barricade_down"/>\n   </properties>\n  </object>\n') % (nid, 76 * T, 125 * T, 2 * T, 5 * T); nid += 1
vis = ''
for gid, x, yb, w, h in [(FG + 128, 76.0, 127.7, 64, 88),     # bàn dựng đứng
                         (FG + 78, 77.3, 126.2, 32, 62),      # thùng phía sau
                         (FG + 87, 76.1, 128.5, 32, 27),      # thùng gỗ
                         (FG + 130, 77.0, 128.8, 32, 27),     # thùng vỡ
                         (FG + 83, 76.0, 130.1, 64, 55)]:     # ván gãy
    vis += tobj(nid, gid, x, yb, w, h); nid += 1
m = re.search(r'<objectgroup id="26" name="gates"[^>]*>\n', s); e = s.index(' </objectgroup>', m.end())
s = s[:e] + gate + vis + s[e:]
# cảnh pell_freed: Pell dỡ rào lúc màn tối
old = 'fade out 0.6\\nset pell_left\\nfade in 0.6'
new = ("say Pell: The warders barricaded the west passage when they brought you down.|I'll pull it apart from the culvert side. Give me a moment.\\n"
       "fade out 0.6\\nset pell_left\\nset west_barricade_down\\nfade in 0.6\\nshake 0.03 0.8\\nhint Somewhere west, timber crashes down. The way to the Rolling Hall is open.")
assert s.count(old) == 1; s = s.replace(old, new)

s = re.sub(r'nextobjectid="\d+"', 'nextobjectid="%d"' % nid, s, count=1)
if crlf: s = s.replace('\n', '\r\n')
open(TMX, 'wb').write(s.encode('utf-8')); open(TSX, 'w', encoding='utf-8', newline='').write(tsx)
print('md5', hashlib.md5(s.encode('utf-8')).hexdigest(), 'nextobjectid', nid)
