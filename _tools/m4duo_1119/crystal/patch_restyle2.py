# 20/11 (§352 b): bệ pháo = tháp đá crypt 128x134 (bảng màu tường), vòng 4 rune đều quanh chân bệ. Kiểm md5, sao lưu.
import re, sys, hashlib, shutil, os
from PIL import Image
R = os.path.expanduser('~/mnt/Soul Requiem/')
M4 = R + 'Assets/GamePackage/EPIC RPG World Pack - Crypt V.1.6/TiledMap Editor/Maps/map4_catacombs_of_naming.tmx'
md5 = lambda p: hashlib.md5(open(p, 'rb').read()).hexdigest()
if md5(M4) != sys.argv[1]: sys.exit('DỪNG: md5 ' + md5(M4))
shutil.copyfile(M4, R + '_backups/2026-11-20_lists/map4_before_restyle2.tmx')
s = open(M4, 'rb').read().decode('utf-8'); T = 32
P = R + 'Assets/Resources/VFX/props/'
m = re.search(r'<object id="\d+" name="duo_core" type="ritual_pillar" x="([\d.]+)" y="([\d.]+)" width="([\d.]+)" height="([\d.]+)"', s)
cx = (float(m.group(1)) + float(m.group(3)) / 2) / T; foot = (float(m.group(2)) + float(m.group(4))) / T + 0.1 / 0.32
def place(name, img, x, y):
    global s
    w, h = Image.open(P + img + '.png').size
    mm = re.search(r'(<object id="\d+" name="%s" gid="\d+") x="[\d.\-]+" y="[\d.\-]+" width="[\d.]+" height="[\d.]+"' % name, s); assert mm, name
    s = s.replace(mm.group(0), mm.group(1) + ' x="%g" y="%g" width="%d" height="%d"' % (x * T - w / 2, y * T, w, h), 1)
place('duo_core_art', 'engine_pedestal', cx, foot)
for i, (dx, dy) in enumerate(((-3.0, -1.3), (3.0, -1.3), (-3.0, 1.9), (3.0, 1.9))):   # 4 góc chéo, cách chân bệ đều nhau
    place('lists_rune_%d' % i, 'floor_rune_%d' % i, cx + dx, foot + dy)
open(M4, 'wb').write(s.encode('utf-8')); print('xong', md5(M4))
