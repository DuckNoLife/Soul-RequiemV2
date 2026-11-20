# §303 (ĐÃ CHẠY 17/11, md5 sau 5961b741…) — Map 1b: đổi collider vật tròn sang ELLIPSE (cột chống lò, đống đá sập, cột bảng, hài cốt ngồi, giỏ). Kiểm md5 trước/sau, sao lưu, giữ kiểu xuống dòng.
import hashlib, shutil, re, sys
from pathlib import Path
R = Path.home() / "mnt/Soul Requiem"
P = R / "Assets/GamePackage/ERW - Grass Land 2.0 v2.1/TiledMap Editor/Maps/map1b_hermits_hollow.tmx"
BEFORE = '164409ac0971665486d6ab12c0030529'
IDS = {'5': 480.0, '6': 561.6, '14': 320.0, '16': 745.6, '18': 604.8, '20': 294.4, '26': 302.4, '89': 800.0, '95': 907.2}   # id -> x để chắc đúng object
raw = P.read_bytes(); md5 = hashlib.md5(raw).hexdigest()
if md5 != BEFORE: sys.exit('md5 khác (%s) — map đã bị sửa, dừng' % md5)
bk = R / "_backups/2026-11-17_doors_colliders"; bk.mkdir(parents=True, exist_ok=True); shutil.copyfile(P, bk / P.name)
s = raw.decode('utf-8'); n = 0
def fix(m):
    global n
    tag, body = m.group(1), m.group(2)
    i = re.search(r' id="(\d+)"', tag).group(1)
    if i not in IDS or 'name="prop collider"' not in tag or '<ellipse' in (body or ''): return m.group(0)
    assert abs(float(re.search(r' x="([\d.]+)"', tag).group(1)) - IDS[i]) < 0.6, i
    n += 1
    return tag + body.replace('</properties>', '</properties><ellipse/>', 1) if '</properties>' in body else tag + '<ellipse/>' + body
s = re.sub(r'(<object [^>]*[^/]>)(.*?</object>)', fix, s, flags=re.S)
assert n == len(IDS), n
P.write_bytes(s.encode('utf-8'))
print('đổi', n, 'collider; md5', hashlib.md5(P.read_bytes()).hexdigest())
