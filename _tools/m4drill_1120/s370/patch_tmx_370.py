# -*- coding: utf-8 -*-
"""§370: thêm tile object xem trước cho trạm 3 Final Exam (bảng đen, 4 cửa sập, 2 pháo cối, 3 đống rơm) vào lớp mechanics_art,
art_for = drill_yard + role -> người dùng thấy / kéo thả trong Tiled; code đọc vị trí từ đây. Thêm 4 ô vào SR-drill-art.tsx (id 16..19)."""
import hashlib, os, shutil, sys
ROOT = sys.argv[1]
TMX = os.path.join(ROOT, "Assets/GamePackage/EPIC RPG World Pack - Crypt V.1.6/TiledMap Editor/Maps/map4_catacombs_of_naming.tmx")
TSX = os.path.join(ROOT, "Assets/GamePackage/EPIC RPG World Pack - Crypt V.1.6/TiledMap Editor/Tilesets/SR-drill-art.tsx")
BK = os.path.join(ROOT, "_backups/2026-11-20_370")
MD5_IN = "ca8cac54e1b9dba9cda9cc92cb63e970"
raw = open(TMX, "rb").read()
assert hashlib.md5(raw).hexdigest() == MD5_IN, "md5 .tmx khác (" + hashlib.md5(raw).hexdigest() + ") — người dùng đã sửa trong Tiled? Dừng."
os.makedirs(BK, exist_ok=True)
shutil.copy2(TMX, BK); shutil.copy2(TSX, BK)
s = raw.decode("utf-8")
assert "\r\n" in s
FIRST = 10552
# ---- tileset
t = open(TSX, "rb").read().decode("utf-8")
assert 'tilecount="16"' in t and '<tile id="16"' not in t
tiles = [
    (16, "../../../../Resources/VFX/props/drill_board.png", 118, 66),
    (17, "../../Props/SR-drill-art/drill_trapdoor_prev.png", 60, 60),
    (18, "../../../../Resources/VFX/props/drill_mortar.png", 52, 63),
    (19, "../../../../Resources/VFX/props/drill_haystack.png", 72, 52),
]
add = "".join(' <tile id="%d">\r\n  <image source="%s" width="%d" height="%d"/>\r\n </tile>\r\n' % x for x in tiles)
t = t.replace('tilecount="16"', 'tilecount="20"').replace("</tileset>", add + "</tileset>")
open(TSX, "wb").write(t.encode("utf-8"))
# ---- object
import re
m = re.search(r'nextobjectid="(\d+)"', s); nid = int(m.group(1))
objs = []
def obj(name, tid, x, y, w, h, role):
    global nid
    o = ('  <object id="%d" name="%s" gid="%d" x="%s" y="%s" width="%d" height="%d">\r\n   <properties>\r\n'
         '    <property name="art_for" value="drill_yard"/>\r\n    <property name="role" value="%s"/>\r\n   </properties>\r\n  </object>\r\n') % (nid, name, FIRST + tid, x, y, w, h, role)
    nid += 1; objs.append(o)
obj("drill_board_art", 16, 486, 3985, 118, 66, "board")
for i, cx in enumerate((220, 340, 460, 580)):
    obj("drill_trapdoor_art_%d" % (i + 1), 17, cx - 30, 4267, 60, 60, "door")
for i, fx in enumerate((272, 528)):
    obj("drill_mortar_art_%d" % (i + 1), 18, fx - 26, 4371, 52, 63, "mortar")
for i, (fx, fy) in enumerate(((160, 4493), (653, 4166), (147, 4090))):
    obj("drill_hay_art_%d" % (i + 1), 19, fx - 36, fy, 72, 52, "hay")
k = s.index('name="mechanics_art"')
end = s.index(" </objectgroup>", k)
s = s[:end] + "".join(objs) + s[end:]
s = s.replace(m.group(0), 'nextobjectid="%d"' % nid, 1)
out = s.encode("utf-8")
open(TMX, "wb").write(out)
print("tmx md5", hashlib.md5(out).hexdigest(), "objects", len(objs), "nextobjectid", nid)
