# 18/11 (người dùng): sửa Map 4 — (1) bỏ hành lang thừa phía tây (x 0-31, y 66-78, không nối khu nào);
# (2) "Lift the bar" thu nhỏ + dời sát cửa, tầm 0,6 -> không với được xuyên tường từ trong ô giam;
# (3) cảnh tỉnh dậy: sau rung chấn, máy quay lia tới cửa ô giam đang bật lên rồi về Ren (người chơi thấy đường ra).
# Kiểm md5 trước khi sửa; sao lưu ở _backups/2026-11-18_m4_fix/.
import hashlib, re, os, shutil, sys
ROOT = os.path.expanduser("~/mnt/Soul Requiem")
TMX = ROOT + "/Assets/GamePackage/EPIC RPG World Pack - Crypt V.1.6/TiledMap Editor/Maps/map4_catacombs_of_naming.tmx"
MD5 = sys.argv[1]
b = open(TMX, 'rb').read(); assert hashlib.md5(b).hexdigest() == MD5, hashlib.md5(b).hexdigest()
os.makedirs(ROOT + "/_backups/2026-11-18_m4_fix", exist_ok=True)
shutil.copy2(TMX, ROOT + "/_backups/2026-11-18_m4_fix/map4_catacombs_of_naming.tmx")
s = b.decode('utf-8'); crlf = '\r\n' in s
if crlf: s = s.replace('\r\n', '\n')
MW = 114
# (1a) xoá ô lớp op_plat trong vùng hành lang
def clear_layer(s, name, x0, x1, y0, y1):
    m = re.search(r'(<layer id="\d+" name="%s"[^>]*>.*?<data encoding="csv">\n)(.*?)(\n</data>)' % re.escape(name), s, re.S)
    assert m, name
    rows = m.group(2).split('\n'); assert len(rows) == 124, len(rows)
    n = 0
    for y in range(y0, y1):
        cells = rows[y].rstrip(',').split(','); tail = rows[y].endswith(',')
        assert len(cells) == MW
        for x in range(x0, x1):
            if cells[x] != '0': cells[x] = '0'; n += 1
        rows[y] = ','.join(cells) + (',' if tail else '')
    print('clear', name, n)
    return s[:m.start(2)] + '\n'.join(rows) + s[m.end(2):]
s = clear_layer(s, 'op_plat', 0, 32, 66, 79)
# (1b) xoá vật thể của hành lang: 4 đèn đứng (props), 4 lửa (ambient fx), 4 collider, 4 đèn sáng
for oid in [297, 301, 305, 309, 299, 303, 307, 311, 298, 302, 306, 310, 300, 304, 308, 312]:
    pat = re.compile(r'  <object id="%d"[^>]*?(/>|>.*?</object>)\n' % oid, re.S)
    s, k = pat.subn('', s, count=1); assert k == 1, oid
print('removed 16 objects')
# (2) oub_bar
m = re.search(r'<object id="930" name="oub_bar" type="story" x="[\d.]+" y="[\d.]+" width="[\d.]+" height="[\d.]+">', s); assert m
s = s.replace(m.group(0), '<object id="930" name="oub_bar" type="story" x="3264" y="3488" width="51.2" height="32">', 1)
a = m.end(); e = s.index('</object>', a)
blk = s[a:e]; assert '<property name="range" type="float" value="1"/>' in blk
s = s[:a] + blk.replace('<property name="range" type="float" value="1"/>', '<property name="range" type="float" value="0.6"/>', 1) + s[e:]
# (3) m4_wake: lia máy quay tới cửa ô giam
old = r'set m4_tremor\nwait 0.5\nbubble ren: The gate jumped its rail.'
assert s.count(old) == 1
s = s.replace(old, r'set m4_tremor\ncam 108,114.6\nwait 1.6\ncamren\nwait 0.4\nbubble ren: The gate jumped its rail.', 1)
if crlf: s = s.replace('\n', '\r\n')
open(TMX, 'wb').write(s.encode('utf-8'))
print('md5', hashlib.md5(s.encode('utf-8')).hexdigest())
