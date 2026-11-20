# Liệt kê collider vật cản của từng map + ảnh prop sở hữu -> tìm vật TRÒN đang dùng hình chữ nhật.
import sys, collections; sys.path.insert(0, '.')
from tmxlib import objects, owner
from pathlib import Path
GP = Path.home() / "mnt/Soul Requiem/Assets/GamePackage"
MAPS = {'m1b': "ERW - Grass Land 2.0 v2.1/TiledMap Editor/Maps/map1b_hermits_hollow.tmx",
        'm2b': "EPIC RPG World - Sewers V1.5/TiledMap Editor/Maps/map2b_kroven_deep.tmx",
        'm3b': "EPIC RPG World Pack - Cemetery V 1.6/TiledMap Editor/Maps/map3b_kings_barrow.tmx",
        'm4': "EPIC RPG World Pack - Crypt V.1.6/TiledMap Editor/Maps/map4_catacombs_of_naming.tmx"}
for k in (sys.argv[1:] or MAPS):
    props, cols = objects(GP / MAPS[k]); c2 = collections.Counter()
    print('==', k, len(cols), 'collider')
    for c in cols:
        p = owner(c, props); n = p['img'] if p else '(không có prop)'
        c2[(n, c['shape'], round(c['w']), round(c['h']))] += 1
    for (n, sh, w, h), v in sorted(c2.items()): print('  %3d %-4s %4dx%-4d %s' % (v, sh, w, h, n))
