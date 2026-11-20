# 20/11 (§358): người dùng: rào giữa Rolling Hall chặn mất bẫy gỗ lăn -> đặt rào ở CỬA VÀO BLADE YARD (lỗ thông Blade Yard <-> Rolling Hall,
# cột 65-67 hàng 115-119) thay vì ngang hành lang. Rolling Hall đi được trọn (gỗ lăn lại chạy hàng 103..142 — Ren phải băng qua gỗ để tới cửa Drill);
# Blade Yard vẫn khoá tới cảnh Pell (cờ west_barricade_down). Bỏ tấm ván thêm ở §357. Lời Pell sửa cho khớp. Kiểm md5, sao lưu, giữ CRLF.
import re, sys, hashlib, shutil, os
R = os.path.expanduser('~/mnt/Soul Requiem/')
M4 = R + 'Assets/GamePackage/EPIC RPG World Pack - Crypt V.1.6/TiledMap Editor/Maps/map4_catacombs_of_naming.tmx'
md5 = lambda p: hashlib.md5(open(p, 'rb').read()).hexdigest()
if md5(M4) != sys.argv[1]: sys.exit('DỪNG: md5 ' + md5(M4))
os.makedirs(R + '_backups/2026-11-20_358/', exist_ok=True)
shutil.copyfile(M4, R + '_backups/2026-11-20_358/map4_before_barricade2.tmx')
s = open(M4, 'rb').read().decode('utf-8'); T = 32
def blk(oid):
    m = re.search(r'<object id="%d"[^>]*?/>' % oid, s) or re.search(r'<object id="%d"[^>]*[^/]>.*?</object>' % oid, s, re.S)
    assert m, oid; return m.group(0)
def put(old, new):
    global s
    assert s.count(old) == 1, old[:80]; s = s.replace(old, new)
def move(oid, x=None, y=None, w=None, h=None):
    b = blk(oid); b2 = b
    for k, v in (('x', x), ('y', y), ('width', w), ('height', h)):
        if v is not None: b2 = re.sub(r' %s="[\d.\-]+"' % k, ' %s="%g"' % (k, v * T), b2, count=1)
    put(b, b2)
# tấm ván thêm của §357 (bản sao 1222, cùng tên cổng)
extra = [m for m in re.finditer(r'[ \t]*<object id="(\d+)" name="west_barricade" type="gate_visual" gid="10455"[^>]*/>\r?\n', s) if m.group(1) != '1222']
assert len(extra) == 1, len(extra)
s = s.replace(extra[0].group(0), '', 1)
# rào đứng trong lỗ thông Blade Yard -> Rolling Hall (như bố cục rào gốc ở hành lang Cell Rows, dời -10,5 / -10 ô)
move(1217, 65, 115, 1.4, 5)
move(1218, 65.5, 117.7); move(1219, 66.8, 116.2); move(1220, 65.6, 118.5); move(1221, 66.5, 118.8); move(1222, 65.5, 120.1)
move(933, h=39)                                     # gỗ lăn lại chạy trọn hành lang
put("The warders barricaded the Rolling Hall when they brought you down.", "The warders barricaded the way into the blade yard when they brought you down.")
put("The way north up the Rolling Hall is open.", "The way into the blade yard is open.")
open(M4, 'wb').write(s.encode('utf-8'))
print('xong', md5(M4))
