# 20/11 (§358 b): rào trong lỗ thông đè lên điểm nghỉ rest_pit -> lùi rào vào trong Blade Yard (x 62,6-64,6, hàng 117,4-120,2: lối đi duy nhất
# giữa hố gai và mép nam ở đầu sân). Điểm nghỉ ở lỗ thông vẫn dùng được từ Rolling Hall. Kiểm md5, sao lưu.
import re, sys, hashlib, shutil, os
R = os.path.expanduser('~/mnt/Soul Requiem/')
M4 = R + 'Assets/GamePackage/EPIC RPG World Pack - Crypt V.1.6/TiledMap Editor/Maps/map4_catacombs_of_naming.tmx'
md5 = lambda p: hashlib.md5(open(p, 'rb').read()).hexdigest()
if md5(M4) != sys.argv[1]: sys.exit('DỪNG: md5 ' + md5(M4))
shutil.copyfile(M4, R + '_backups/2026-11-20_358/map4_before_barricade3.tmx')
s = open(M4, 'rb').read().decode('utf-8'); T = 32
def blk(oid):
    m = re.search(r'<object id="%d"[^>]*?/>' % oid, s) or re.search(r'<object id="%d"[^>]*[^/]>.*?</object>' % oid, s, re.S)
    assert m, oid; return m.group(0)
def move(oid, x=None, y=None, w=None, h=None):
    global s
    b = blk(oid); b2 = b
    for k, v in (('x', x), ('y', y), ('width', w), ('height', h)):
        if v is not None: b2 = re.sub(r' %s="[\d.\-]+"' % k, ' %s="%g"' % (k, v * T), b2, count=1)
    assert s.count(b) == 1; s = s.replace(b, b2)
move(1217, 62.6, 117.4, 2.0, 2.8)
move(1218, 62.6, 119.6); move(1222, 62.6, 120.4); move(1220, 63.9, 119.0); move(1221, 63.6, 120.4); move(1219, 64.0, 118.6)
open(M4, 'wb').write(s.encode('utf-8')); print('xong', md5(M4))
