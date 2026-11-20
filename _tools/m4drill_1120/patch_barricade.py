# 20/11 (§357): người dùng chạy thử — đi từ Question Room lên hành lang Cell Rows sang tây thì bị rào west_barricade chặn (lối tây đúng là phải đi
# qua đây). Dời rào giám ngục (cổng west_barricade + 5 hình, vẫn mở bằng cờ west_barricade_down của cảnh Pell) từ đầu tây hành lang Cell Rows
# vào giữa Rolling Hall (hàng ~123, ngay dưới lối ra Blade Yard): hành lang Cell Rows -> đoạn dưới Rolling Hall -> cửa Drill luôn đi được;
# phần trên Rolling Hall / Blade Yard vẫn chỉ mở sau Pell. Bỏ rào vĩnh viễn §356 (khối 1341 + 4 hình rh_barricade); khúc gỗ lăn dừng ở rào mới
# (hàng 103..123). Lời Pell sửa cho khớp. Kiểm md5, sao lưu, giữ CRLF.
import re, sys, hashlib, shutil, os
R = os.path.expanduser('~/mnt/Soul Requiem/')
M4 = R + 'Assets/GamePackage/EPIC RPG World Pack - Crypt V.1.6/TiledMap Editor/Maps/map4_catacombs_of_naming.tmx'
md5 = lambda p: hashlib.md5(open(p, 'rb').read()).hexdigest()
if md5(M4) != sys.argv[1]: sys.exit('DỪNG: md5 ' + md5(M4))
os.makedirs(R + '_backups/2026-11-20_357/', exist_ok=True)
shutil.copyfile(M4, R + '_backups/2026-11-20_357/map4_before_barricade.tmx')
s = open(M4, 'rb').read().decode('utf-8'); T = 32
def blk(oid):
    m = re.search(r'<object id="%d"[^>]*?/>' % oid, s) or re.search(r'<object id="%d"[^>]*[^/]>.*?</object>' % oid, s, re.S)
    assert m, oid; return m.group(0)
def put(old, new):
    global s
    assert s.count(old) == 1, old[:80]; s = s.replace(old, new)
def drop(oid):
    global s
    b = blk(oid); m = re.search(r'[ \t]*' + re.escape(b) + r'\r?\n', s); assert m, oid
    s = s.replace(m.group(0), '', 1)
def move(oid, x=None, y=None, w=None, h=None):
    b = blk(oid); b2 = b
    for k, v in (('x', x), ('y', y), ('width', w), ('height', h)):
        if v is not None: b2 = re.sub(r' %s="[\d.\-]+"' % k, ' %s="%g"' % (k, v * T), b2, count=1)
    put(b, b2)
for oid in (1341, 1342, 1343, 1344, 1345): drop(oid)
move(1217, 68, 123.0, 7, 1.3)                       # cổng (va chạm) ngang Rolling Hall
move(1218, 70.9, 124.7)                             # bàn dựng đứng (10500)
move(1222, 68.9, 124.5); move(1219, 74.0, 124.4)    # bàn ván (10455), thùng (10450)
move(1220, 70.4, 124.9); move(1221, 68.2, 124.9)    # hòm, hòm vỡ
b = blk(1217); assert 'west_barricade_down' in b
# thêm một tấm ván nữa cho kín bề ngang (hình 10455 thứ hai) — cùng tên cổng nên cũng ẩn khi mở
nid = int(re.search(r'nextobjectid="(\d+)"', s).group(1))
b = blk(1222); put(b, b + '\r\n  ' + re.sub(r'<object id="\d+"', '<object id="%d"' % nid, re.sub(r' x="[\d.\-]+"', ' x="%g"' % (72.9 * T), b, count=1), count=1))
s = re.sub(r'nextobjectid="\d+"', 'nextobjectid="%d"' % (nid + 1), s, count=1)
move(933, h=20)                                     # gỗ lăn hàng 103..123
put("The warders barricaded the west passage when they brought you down.", "The warders barricaded the Rolling Hall when they brought you down.")
put("The way to the Rolling Hall is open.", "The way north up the Rolling Hall is open.")
open(M4, 'wb').write(s.encode('utf-8'))
print('xong', md5(M4))
