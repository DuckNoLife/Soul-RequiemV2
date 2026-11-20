# 20/11 (§353): cột pha lê -> KỆ PHA LÊ 54x78. Sửa kích thước ảnh xem trước (tsx ô 8-11 + vật crystal_<c>_art), nới va chạm chân kệ,
# bỏ 4 cụm pha lê dưới sàn (kệ đã chứa pha lê — tránh rối). Kiểm md5, sao lưu, giữ CRLF.
import re, sys, hashlib, shutil, os
R = os.path.expanduser('~/mnt/Soul Requiem/')
TM = R + 'Assets/GamePackage/EPIC RPG World Pack - Crypt V.1.6/TiledMap Editor/'
M4, TSX = TM + 'Maps/map4_catacombs_of_naming.tmx', TM + 'Tilesets/SR-lists-art.tsx'
md5 = lambda p: hashlib.md5(open(p, 'rb').read()).hexdigest()
if md5(M4) != sys.argv[1]: sys.exit('DỪNG: md5 ' + md5(M4))
B = R + '_backups/2026-11-20_353/'
os.makedirs(B, exist_ok=True)
shutil.copyfile(M4, B + 'map4_before_shelves.tmx')
W, H = 54, 78
# tsx
t = open(TSX, 'rb').read().decode('utf-8')
for c in ('crimson', 'azure', 'verdant', 'gilded'):
    t, n = re.subn(r'(crystal_pillar_%s\.png") width="\d+" height="\d+"' % c, r'\g<1> width="%d" height="%d"' % (W, H), t); assert n == 1, c
open(TSX, 'wb').write(t.encode('utf-8'))
# tmx
s = open(M4, 'rb').read().decode('utf-8')
for c in ('crimson', 'azure', 'verdant', 'gilded'):
    m = re.search(r'(<object id="\d+" name="crystal_%s_art" gid="\d+") x="([\d.]+)" y="([\d.]+)" width="([\d.]+)" height="([\d.]+)"' % c, s); assert m, c
    cx = float(m.group(2)) + float(m.group(4)) / 2
    s = s.replace(m.group(0), m.group(1) + ' x="%g" y="%s" width="%d" height="%d"' % (cx - W / 2, m.group(3), W, H), 1)
    m = re.search(r'(<object id="\d+" name="crystal_%s" type="crystal_pillar") x="([\d.]+)" y="([\d.]+)" width="([\d.]+)" height="([\d.]+)"' % c, s); assert m, c
    cx = float(m.group(2)) + float(m.group(4)) / 2
    s = s.replace(m.group(0), m.group(1) + ' x="%g" y="%s" width="48" height="%s"' % (cx - 24, m.group(3), m.group(5)), 1)
    s, n = re.subn(r'[ \t]*<object id="\d+" name="crystal_cluster_%s_0"[^>]*/>\r?\n' % c, '', s); assert n == 1, c
open(M4, 'wb').write(s.encode('utf-8'))
print('xong', md5(M4))
