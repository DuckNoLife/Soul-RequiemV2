# 18/11 (§324): hình cho 2 đồ nhặt Map 4 — bộ chìa treo tường (warden_key) + quần áo gấp & katana của Ren trên bàn lính gác (guard_katana).
# Ảnh PixelLab (map object 1e9b0dc1 / 5103124a) + katana trong vỏ từ katana_ground.py ronin. Đặt tile object, pickup `hide` = tên hình -> nhặt xong hình biến mất.
import hashlib, re, os, shutil, uuid
from PIL import Image
ROOT = os.path.expanduser('~/mnt/Soul Requiem')
CRYPT = ROOT + "/Assets/GamePackage/EPIC RPG World Pack - Crypt V.1.6/"
TMX = CRYPT + "TiledMap Editor/Maps/map4_catacombs_of_naming.tmx"
TSX = CRYPT + "TiledMap Editor/Tilesets/SR-props-trimmed.tsx"
IMG = CRYPT + "Props/SR-trimmed/"
SRC = ROOT + "/_tools/m4fix_1118/art_new/"
EXP = {'m4_warden_keys.png': '0a139715f7edb60da56111f02653c9e9', 'm4_ren_belongings.png': '0f40ae4cb821192155ad0cf45e1e9840'}
for f, h in EXP.items():
    assert hashlib.md5(Image.open(SRC + f).convert('RGBA').tobytes()).hexdigest() == h, f
b = open(TMX, 'rb').read(); assert hashlib.md5(b).hexdigest() == 'dd74455ef5b1814bd8abbf4dcce15a32', hashlib.md5(b).hexdigest()
BK = ROOT + "/_backups/2026-11-18_m4_props/"; os.makedirs(BK, exist_ok=True); shutil.copy2(TMX, BK); shutil.copy2(TSX, BK)
s = b.decode('utf-8'); crlf = '\r\n' in s; s = s.replace('\r\n', '\n')
tsx = open(TSX, encoding='utf-8').read(); nxt = int(re.search(r'tilecount="(\d+)"', tsx).group(1)); FG = 10372
meta_t = open(IMG + 'op_door_h.png.meta', encoding='utf-8').read()
gids = {}
for f in EXP:
    shutil.copyfile(SRC + f, IMG + f)                                        # PNG trước, .meta sau
    open(IMG + f + '.meta', 'w', encoding='utf-8', newline='').write(re.sub(r'guid: \w+', 'guid: ' + uuid.uuid4().hex, meta_t, count=1))
    w, h = Image.open(IMG + f).size
    tsx = tsx.replace('</tileset>', ' <tile id="%d">\n  <image source="../../Props/SR-trimmed/%s" width="%d" height="%d"/>\n </tile>\n</tileset>' % (nxt, f, w, h))
    gids[f] = (FG + nxt, w, h); nxt += 1
tsx = re.sub(r'tilecount="\d+"', 'tilecount="%d"' % nxt, tsx, count=1)
nid = int(re.search(r'nextobjectid="(\d+)"', s).group(1))
T = 32
# bộ chìa: treo dưới móc key_hook (84.1, 132.3) trên mặt tường -> lớp "wall props"
g, w, h = gids['m4_warden_keys.png']
keys = '  <object id="%d" name="warden_keys_art" gid="%d" x="%g" y="%g" width="%d" height="%d"/>\n' % (nid, g, 83.7 * T, 133.1 * T, w, h); nid += 1
m = re.search(r'<objectgroup id="\d+" name="wall props"[^>]*>\n', s); e = s.index(' </objectgroup>', m.end()); s = s[:e] + keys + s[e:]
# quần áo + katana: trên mặt bàn lính gác (mặt bàn x 91.75–94.06, y 136.0–137.25) -> lớp mới "props on tables" (order 3, luôn trên mặt bàn)
g, w, h = gids['m4_ren_belongings.png']
tab = ('<objectgroup id="%d" name="props on tables">\n  <properties>\n   <property name="unity:SortingOrder" type="int" value="3"/>\n  </properties>\n'
       '  <object id="%d" name="ren_belongings_art" gid="%d" x="%g" y="%g" width="%d" height="%d"/>\n </objectgroup>\n ')
lid = int(re.search(r'nextlayerid="(\d+)"', s).group(1))
tab = tab % (lid, nid, g, 91.8 * T, 137.15 * T, w, h); nid += 1
s = re.sub(r'nextlayerid="\d+"', 'nextlayerid="%d"' % (lid + 1), s, count=1)
i = s.index('<objectgroup id="25" name="ambient fx"'); s = s[:i] + tab + s[i:]
# pickup: hide = tên hình
for oid, art in ((916, 'warden_keys_art'), (917, 'ren_belongings_art')):
    m = re.search(r'<object id="%d" [^>]*>\n   <properties>\n' % oid, s); assert m, oid
    s = s[:m.end()] + '    <property name="hide" value="%s"/>\n' % art + s[m.end():]
s = re.sub(r'nextobjectid="\d+"', 'nextobjectid="%d"' % nid, s, count=1)
if crlf: s = s.replace('\n', '\r\n')
open(TMX, 'wb').write(s.encode('utf-8')); open(TSX, 'w', encoding='utf-8', newline='').write(tsx)
print('gids', gids, 'md5', hashlib.md5(s.encode('utf-8')).hexdigest())
