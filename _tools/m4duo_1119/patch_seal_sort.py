# 19/11 (§344): Map 4 — ấn (seal1_item / seal2_item trên bệ, seal1_in / seal2_in trên hốc cửa ấn) bị bệ / hốc che: cùng sortingOrder 2, chân ảnh ấn
# cao hơn chân bệ -> sắp xếp theo Y vẽ ấn PHÍA SAU bệ. Thêm unity:SortingOrder = 3 cho 4 object đó (SuperTiled2Unity đọc thuộc tính trên object).
import re, sys, hashlib, shutil, os
R = os.path.expanduser('~/mnt/Soul Requiem/')
M4 = R + 'Assets/GamePackage/EPIC RPG World Pack - Crypt V.1.6/TiledMap Editor/Maps/map4_catacombs_of_naming.tmx'
md5 = lambda p: hashlib.md5(open(p, 'rb').read()).hexdigest()
if md5(M4) != '67d487123d5d8fc9dbcd059ea258a199': sys.exit('DỪNG: md5 ' + md5(M4))
shutil.copyfile(M4, R + '_backups/2026-11-19_m4duo/map4_catacombs_of_naming.tmx')
s = open(M4, 'rb').read().decode('utf-8'); NL = '\r\n' if '\r\n' in s else '\n'
for oid in (924, 927, 925, 928):
    m = re.search(r'<object id="%d"[^>]*?/>' % oid, s); assert m, oid
    tag = m.group(0)[:-2]
    s = s.replace(m.group(0), tag + '>' + NL + '   <properties>' + NL + '    <property name="unity:SortingOrder" type="int" value="3"/>' + NL + '   </properties>' + NL + '  </object>')
open(M4, 'wb').write(s.encode('utf-8')); print(md5(M4))
