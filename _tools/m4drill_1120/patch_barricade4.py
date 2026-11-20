# 20/11 (§358 c): rào lùi vào trong sân bị đi vòng (Blade Yard mở rộng xuống nam) -> rào trở lại ĐÚNG LỖ THÔNG (cột 65-66, hàng 115-120) như §358;
# điểm nghỉ rest_pit (vốn nằm trong lỗ thông, bị rào đè) dời vào trong sân, ngay sau lỗ thông (60,6-62,6 / 118,4-120,4). Kiểm md5, sao lưu.
import re, sys, hashlib, shutil, os
R = os.path.expanduser('~/mnt/Soul Requiem/')
M4 = R + 'Assets/GamePackage/EPIC RPG World Pack - Crypt V.1.6/TiledMap Editor/Maps/map4_catacombs_of_naming.tmx'
md5 = lambda p: hashlib.md5(open(p, 'rb').read()).hexdigest()
if md5(M4) != sys.argv[1]: sys.exit('DỪNG: md5 ' + md5(M4))
shutil.copyfile(M4, R + '_backups/2026-11-20_358/map4_before_barricade4.tmx')
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
move(1217, 65, 115, 1.4, 5)
move(1218, 65.5, 117.7); move(1219, 66.8, 116.2); move(1220, 65.6, 118.5); move(1221, 66.5, 118.8); move(1222, 65.5, 120.1)
move(879, 60.6, 118.4)                                # rest_pit
open(M4, 'wb').write(s.encode('utf-8')); print('xong', md5(M4))
