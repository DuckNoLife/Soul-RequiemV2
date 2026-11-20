# §303 (17/11) — Map 1: 7 cây trồng thêm ở làng (collider gốc 29x16 / 19x13 hình chữ nhật, khác 270 cây cũ dùng ellipse)
#                Map 3: 2 trụ gạch bát giác 'pillar - 1' -> ELLIPSE. Kiểm md5 trước, sao lưu, chèn <ellipse/> giữ nguyên mọi byte khác.
import hashlib, shutil, re, sys
from pathlib import Path
R = Path.home() / "mnt/Soul Requiem"; GP = R / "Assets/GamePackage"
JOBS = [(GP / "ERW - Grass Land 2.0 v2.1/TiledMap Editor/Maps/map1_greystone_plains.tmx", '00b045a3d64d222493fc90ac4775d414',
         {'3309': 44.0, '3329': 64.4, '3337': 57.2, '3342': 85.9, '3345': 68.0, '3346': 110.4, '3348': 77.8}),
        (GP / "EPIC RPG World Pack - Cemetery V 1.6/TiledMap Editor/Maps/map3_vale_of_graves.tmx", '0d75127e6d78640b716c0dc2283eedd3',
         {'4738': 707.0, '4739': 805.0})]
bk = R / "_backups/2026-11-17_doors_colliders"; bk.mkdir(parents=True, exist_ok=True)
for P, before, ids in JOBS:
    raw = P.read_bytes(); md5 = hashlib.md5(raw).hexdigest()
    if md5 != before: sys.exit('%s: md5 khác (%s), dừng' % (P.name, md5))
    shutil.copyfile(P, bk / P.name); s = raw.decode('utf-8'); done = set()
    def one(m):
        tag = m.group(0); i = re.search(r' id="(\d+)"', tag).group(1)
        if i not in ids: return tag
        assert abs(float(re.search(r' x="([-\d.]+)"', tag).group(1)) - ids[i]) < 0.6, i
        done.add(i)
        if tag.endswith('/>'): return tag[:-2].rstrip() + '><ellipse/></object>'
        return tag                                   # có thân -> chèn sau, ở bước 2
    s = re.sub(r'<object [^>]*>', one, s)
    for i in ids:                                    # object có <properties>: chèn <ellipse/> ngay trước </object> của nó
        if i in done and re.search(r'<object id="%s"[^>]*><ellipse/></object>' % i, s): continue
        m = re.search(r'(<object id="%s"[^>]*[^/]>)(.*?)(</object>)' % i, s, flags=re.S)
        assert m and '<ellipse' not in m.group(2), i
        s = s[:m.start(3)] + '<ellipse/>' + s[m.start(3):]; done.add(i)
    assert done == set(ids), (P.name, done)
    P.write_bytes(s.encode('utf-8'))
    print(P.name, 'đổi', len(done), 'md5', hashlib.md5(P.read_bytes()).hexdigest())
