# §360b: ảnh xem trước trong Tiled theo asset vẽ lại (nhìn thẳng top-down): thùng, máy phóng (bản gốc quay PHẢI), giá đèn x1.25, bệ đèn.
import hashlib, os, re, shutil
from PIL import Image
TM = 'Assets/GamePackage/EPIC RPG World Pack - Crypt V.1.6/TiledMap Editor/'
M4 = TM + 'Maps/map4_catacombs_of_naming.tmx'; TSX = TM + 'Tilesets/SR-drill-art.tsx'
PROPS = 'Assets/GamePackage/EPIC RPG World Pack - Crypt V.1.6/Props/'; RV = 'Assets/Resources/VFX/props/'
B = '_backups/2026-11-20_360b/'
md5 = lambda p: hashlib.md5(open(p, 'rb').read()).hexdigest()
assert md5(M4) == 'd822d54c8c03ecfdedd936043846da29', md5(M4)
os.makedirs(B, exist_ok=True); shutil.copy2(M4, B); shutil.copy2(TSX, B)
# bệ đèn + đèn (xem trước)
ped = Image.open(RV + 'drill_keyped.png').convert('RGBA'); lan = Image.open(RV + 'drill_lantern.png').convert('RGBA')
comp = Image.new('RGBA', (max(24, lan.width), 24 + lan.height)); comp.alpha_composite(ped, ((comp.width - 24) // 2, comp.height - ped.height)); comp.alpha_composite(lan, ((comp.width - lan.width) // 2, 0))
comp.save(PROPS + 'SR-drill-art/drill_lantern_prev.png')
# tsx: cập nhật kích thước ảnh
t = open(TSX, 'rb').read().decode('utf-8')
def fix(rel, path):
    global t
    w, h = Image.open(path).size
    t, n = re.subn(r'(<image source="%s" width=")\d+(" height=")\d+(")' % re.escape(rel), r'\g<1>%d\g<2>%d\g<3>' % (w, h), t); assert n == 1, rel
    return w, h
SZ = {9: fix('../../../../Resources/VFX/props/drill_crate.png', RV + 'drill_crate.png'),
      13: fix('../../../../Resources/VFX/props/drill_launcher.png', RV + 'drill_launcher.png'),
      14: fix('../../../../Resources/VFX/props/drill_rack.png', RV + 'drill_rack.png'),
      15: fix('../../Props/SR-drill-art/drill_lantern_prev.png', PROPS + 'SR-drill-art/drill_lantern_prev.png')}
open(TSX, 'wb').write(t.encode('utf-8'))
s = open(M4, 'rb').read().decode('utf-8')
FIRST = 10552
def setobj(name, gid, cx_tiles, bottom_tiles, w, h, count=1):
    """đặt lại gid / x / y / w / h của tile object theo tên (chân giữa = cx, đáy)."""
    global s
    pat = re.compile(r'<object id="(\d+)" name="%s" gid="\d+" x="[^"]*" y="[^"]*" width="\d+" height="\d+"' % re.escape(name))
    ms = list(pat.finditer(s)); assert len(ms) == count, (name, len(ms))
    for m, (cx, b) in reversed(list(zip(ms, cx_tiles))):
        new = '<object id="%s" name="%s" gid="%d" x="%g" y="%g" width="%d" height="%d"' % (m.group(1), name, gid, round(cx * 32 - w / 2, 2), round(b * 32, 2), w, h)
        s = s[:m.start()] + new + s[m.end():]
# 3 thùng: giữ ô cũ (x // 32), căn giữa ô theo bề ngang ảnh mới, đáy giữ nguyên
w, h = SZ[9]
pat = re.compile(r'<object id="(\d+)" name="store_crate_art" gid="\d+" x="([^"]*)" y="([^"]*)" width="\d+" height="\d+"')
out = []
for m in reversed(list(pat.finditer(s))):
    cell = int(float(m.group(2)) // 32)
    new = '<object id="%s" name="store_crate_art" gid="%d" x="%g" y="%s" width="%d" height="%d"' % (m.group(1), FIRST + 9, cell * 32 + (32 - w) / 2, m.group(3), w, h)
    s = s[:m.start()] + new + s[m.end():]
w, h = SZ[13]
setobj('drill_launcher_w_art', FIRST + 13, [(3.2, 128.9)], None, w, h)                     # bắn sang đông = hình gốc
setobj('drill_launcher_e_art', (FIRST + 13) | 0x80000000, [(21.8, 135.9)], None, w, h)     # bắn sang tây = lật ngang
w, h = SZ[14]; setobj('drill_rack_art', FIRST + 14, [(6.5, 122.75)], None, round(w * 1.25), round(h * 1.25))
w, h = SZ[15]; setobj('drill_lantern_art', FIRST + 15, [(17.5, 141.8)], None, w, h)
open(M4, 'wb').write(s.encode('utf-8'))
print('xong md5', md5(M4))
