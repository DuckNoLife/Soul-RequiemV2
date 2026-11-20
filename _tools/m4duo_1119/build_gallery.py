# 19/11 (§345): Map 4 — mở rộng khoảng trống phía nam The Royal Reliquary thành phòng đấu riêng THE WARDEN GALLERY (khu c4b) cho song đấu
# Brazen Reliquary + Hollow Champion. Chép bộ ô tường/sàn/bục của Reliquary xuống +24 hàng (cùng phong cách), mở hành lang x92-93 nối hai
# phòng (cổng warden_gate tự đóng khi vào trận), bục phía đông thành BỆ PHÁO kín (bỏ cầu thang) cho Engine, đặt tượng che chắn + thùng thuốc nổ.
# Kiểm md5 (tham số 1), sao lưu, giữ kiểu xuống dòng. Chạy: python3 build_gallery.py <md5>
import re, sys, hashlib, shutil, os
R = os.path.expanduser('~/mnt/Soul Requiem/')
M4 = R + 'Assets/GamePackage/EPIC RPG World Pack - Crypt V.1.6/TiledMap Editor/Maps/map4_catacombs_of_naming.tmx'
OUT = sys.argv[2] if len(sys.argv) > 2 else M4
md5 = lambda p: hashlib.md5(open(p, 'rb').read()).hexdigest()
if md5(M4) != sys.argv[1]: sys.exit('DỪNG: md5 ' + md5(M4))
if OUT == M4: shutil.copyfile(M4, R + '_backups/2026-11-19_m4gallery/map4_before_gallery.tmx')
s = open(M4, 'rb').read().decode('utf-8'); NL = '\r\n' if '\r\n' in s else '\n'
W, H, T, DY = 114, 148, 32, 24

# ------------------------------------------------------------------ lớp ô
def get_layer(name):
    m = re.search(r'(<layer id="\d+" name="%s"[^>]*>.*?<data encoding="csv">)(.*?)(</data>)' % re.escape(name), s, re.S)
    vals = [int(v) for v in m.group(2).replace(NL, '').replace('\n', '').split(',') if v.strip()]
    assert len(vals) == W * H, name
    return m, vals
def put_layer(name, vals):
    global s
    m, _ = get_layer(name)
    rows = [','.join(str(v) for v in vals[y * W:(y + 1) * W]) for y in range(H)]
    body = NL + (',' + NL).join(rows) + NL
    s = s[:m.start(2)] + body + s[m.end(2):]
# kiểm round-trip
for nm in ('rock',):
    m, v = get_layer(nm); old = s; put_layer(nm, v); assert s == old, 'csv round-trip lệch'

L = {n: get_layer(n)[1] for n in ('op_pit', 'ground', 'ground2', 'rock', 'deco', 'plat', 'rails')}
def g(n, x, y): return L[n][y * W + x]
def p(n, x, y, v): L[n][y * W + x] = v
X0, X1 = 77, 113
# 1) xoá vực đen trong vùng phòng mới
for y in range(87, 111):
    for x in range(X0, X1 + 1): p('op_pit', x, y, 0)
# 2) sàn: chép nguyên lớp ground (vốn phủ kín) + vệt sàn ground2 dưới bàn cũ
for y in range(87, 111):
    for x in range(X0, X1 + 1): p('ground', x, y, g('ground', x, y - DY if y - DY <= 86 else 84))
for y in range(102, 109):
    for x in range(79, 96): p('ground2', x, y, g('ground2', x, y - DY))
# 3) tường đá
FILL, RE, LE = 2935, 2937, 2933            # đá đặc / mép phải (hở bên phải) / mép trái (hở bên trái)
for y in range(87, 93):
    for x in range(X0, X1 + 1): p('rock', x, y, FILL)
    p('rock', X0, y, LE)
for y in range(93, 111):
    for x in range(X0, X1 + 1): p('rock', x, y, g('rock', x, y - DY))
for y in range(96, 109): p('rock', X0, y, RE)           # tường tây kín (bỏ cửa sang phòng tây của bản gốc)
# hành lang x92-93 từ sàn Reliquary (hàng 84) xuống cổng phòng mới (hàng 93-95 là mặt tường có vòm như cổng kho phía bắc)
p('rock', 91, 85, 2921); p('rock', 94, 85, 2917)
for y in range(85, 93):
    p('rock', 92, y, 0); p('rock', 93, y, 0)
    if y >= 86: p('rock', 91, y, RE); p('rock', 94, y, LE)
for y in range(85, 96):
    for x in (92, 93): p('ground', x, y, g('ground', x, 64 + (y - 85) % 8))
# 4) mặt tường trang trí (vòm, cột)
for y in range(93, 96):
    for x in range(X0, X1 + 1): p('deco', x, y, g('deco', x, y - DY))
# 5) bục -> BỆ PHÁO: chép bục, bỏ cầu thang (thay bằng mặt bệ), lan can liền một dải
for y in range(91, 104):
    for x in range(95, 111): p('plat', x, y, g('plat', x, y - DY))
for x in range(99, 104):
    p('plat', x, 100, 3285); p('plat', x, 101, 3301); p('plat', x, 102, 3317); p('plat', x, 103, 0)
for y in range(96, 101):
    for x in range(95, 108): p('rails', x, y, g('rails', x, y - DY))
for x in range(99, 104): p('rails', x, 99, 1803)
for n, v in L.items(): put_layer(n, v)

# ------------------------------------------------------------------ object
nid = int(re.search(r'nextobjectid="(\d+)"', s).group(1))
def new_id():
    global nid
    nid += 1; return nid - 1
def blk(oid):
    m = re.search(r'<object id="%d"[^>]*?/>' % oid, s) or re.search(r'<object id="%d"[^>]*[^/]>.*?</object>' % oid, s, re.S)
    assert m, oid; return m.group(0)
def put(old, new):
    global s
    assert s.count(old) == 1, old[:80]; s = s.replace(old, new)
def shifted(oid, name=None, dy=DY * T):
    b = blk(oid)
    b = re.sub(r'<object id="%d"' % oid, '<object id="%d"' % new_id(), b, count=1)
    b = re.sub(r' y="([\d.]+)"', lambda m: ' y="%g"' % (float(m.group(1)) + dy), b, count=1)
    if name: b = re.sub(r' name="[^"]*"', ' name="%s"' % name, b, count=1)
    return b
def after(oid, *news):
    b = blk(oid); put(b, b + ''.join(NL + '  ' + n for n in news))
def props(d):
    out = []
    for k, v in d.items():
        if isinstance(v, bool): out.append('<property name="%s" type="bool" value="%s"/>' % (k, 'true' if v else 'false'))
        elif isinstance(v, int): out.append('<property name="%s" type="int" value="%d"/>' % (k, v))
        elif isinstance(v, float): out.append('<property name="%s" type="float" value="%g"/>' % (k, v))
        else: out.append('<property name="%s" value="%s"/>' % (k, v))
    return '<properties>' + NL + ''.join('    ' + o + NL for o in out) + '   </properties>'
def rect(name, typ, x, y, w, h, d=None):
    body = '<object id="%d" name="%s" type="%s" x="%g" y="%g" width="%g" height="%g"' % (new_id(), name, typ, x * T, y * T, w * T, h * T)
    return body + ('>' + NL + '   ' + props(d) + NL + '  </object>' if d else '/>')

# cổng hành lang (giống cổng kho phía bắc) — khoá đấu trường
gate = shifted(908, 'warden_gate')
gate = gate.replace('<property name="starts_closed" type="bool" value="true"/>', '<property name="close_on_enter" type="bool" value="true"/>' + NL + '    <property name="starts_closed" type="bool" value="false"/>').replace('value="c4"', 'value="c4b"')
after(908, gate); after(5, shifted(5, 'warden_gate'))
# trang trí tường + tượng trên bệ + va chạm tượng bệ
after(653, shifted(651), shifted(653)); after(640, shifted(639), shifted(640)); after(636, shifted(633), shifted(636))
after(637, shifted(634), shifted(637))
# đèn
after(654, shifted(652, 'wg_torch_0'), shifted(654, 'wg_torch_1'))
after(638, shifted(635, 'wg_cdl_0'), shifted(638, 'wg_cdl_1'))
# khối chắn: bỏ khối vực 30x30 cũ, thay bằng tường bắc (hai bên hành lang), tường nam, bệ pháo
bl = lambda x, y, w, h: rect('blocker', 'blocker', x, y, w, h, {'collider_kind': 'blocker'})
put(blk(1056), (NL + '  ').join([bl(78, 85, 14, 11), bl(94, 85, 14, 11), bl(78, 109, 30, 6),
                               bl(95, 96, 1, 7), bl(96, 99, 3, 4), bl(99, 99, 5, 4), bl(104, 99, 4, 4)]))
# khu đấu
zone = rect('c4b', 'zone', 77, 95, 31, 14, {'ambient': '0.3', 'ambient_color': '#d8b890', 'stage': 'reliquary', 'title': 'The Warden Gallery',
                                             'unity:IsTrigger': True, 'zone_id': 'c4b'})
after(873, zone)
# boss: chuyển xuống phòng mới
b = blk(1008)
b2 = re.sub(r'x="[\d.]+" y="[\d.]+" width="[\d.]+" height="[\d.]+"', 'x="%g" y="%g" width="%g" height="%g"' % (78 * T, 96 * T, 30 * T, 13 * T), b, count=1)
put(b, b2.replace('value="c4"', 'value="c4b"'))
for oid, (x, y) in ((1006, (101.5, 97.6)), (1235, (87.5, 101.5))):
    b = blk(oid)
    b2 = re.sub(r'x="[\d.]+" y="[\d.]+"', 'x="%g" y="%g"' % (x * T, y * T), b, count=1)
    put(b, b2.replace('value="c4"', 'value="c4b"').replace('<property name="wave" type="int" value="2"/>', '<property name="wave" type="int" value="1"/>'))
# ấn Reliquary: giờ cần hạ hai Warden
b = blk(921)
put(b, b.replace('value="c4_cleared"', 'value="c4b_cleared"').replace('Its keeper still stands guard over it.', 'Its keepers wait in the gallery below.'))
# cơ chế môi trường + mốc cho boss (đọc theo tên)
mech = []
for i, (x, y) in enumerate(((86.5, 99.6), (90.6, 104.8), (82.6, 105.6))):
    mech.append(rect('warden_statue_%d' % i, 'warden_statue', x - 0.7, y - 0.55, 1.4, 0.55,
                     {'sprite': 'VFX/props/warden_statue_%d' % (i % 2), 'sprite_damaged': 'VFX/props/warden_statue_%d_c1,VFX/props/warden_statue_%d_c2' % (i % 2, i % 2),
                      'sprite_broken': 'VFX/props/warden_plinth_%d' % (i % 2), 'hits': 6}))
for i, (x, y) in enumerate(((80.6, 97.9), (89.6, 98.3), (84.4, 102.7), (93.6, 107.0), (98.6, 106.2), (105.4, 107.0))):
    mech.append(rect('powder_keg_%d' % i, 'powder_keg', x - 0.33, y - 0.3, 0.66, 0.3, {'sprite': 'VFX/props/powder_keg'}))
mech.append(rect('duo_gallery', 'ritual_pillar', 97.3, 96.2, 9.4, 2.5))     # đường ray bệ pháo (Engine đi lại trong khung này)
mech.append(rect('duo_landing', 'ritual_pillar', 100.5, 105.2, 2, 1.6))     # chỗ Engine nhảy xuống khi Champion gục
mm = re.search(r'(<objectgroup id="35" name="mechanics"[^>]*>.*?)(\s*</objectgroup>)', s, re.S)
s = s[:mm.end(1)] + ''.join(NL + '  ' + x for x in mech) + s[mm.end(1):]
s = re.sub(r'nextobjectid="\d+"', 'nextobjectid="%d"' % nid, s, count=1)
open(OUT, 'wb').write(s.encode('utf-8'))
print('xong ->', OUT, 'md5', md5(OUT), 'nextobjectid', nid)
