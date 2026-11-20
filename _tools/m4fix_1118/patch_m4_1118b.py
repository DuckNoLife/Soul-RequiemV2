# 18/11 (lần 2): Map 4 — (a) gộp 2 lớp object trùng tên "gates" (id 21: chỉ 1 hình cầu pit_bridge; id 26: 11 cổng + 10 hình)
# thành một lớp (Map 4 là map DUY NHẤT có 2 lớp cùng tên — nghi SuperTiled2Unity đặt tên trùng khiến MapLayout không nhận cổng);
# (b) oub_bar tầm 0,6 -> 0,2: tầm tính bằng ĐƠN VỊ THẾ GIỚI (1 ô = 0,32), 0,6 = 1,9 ô vẫn với xuyên tường.
import hashlib, re, os, shutil, sys
ROOT = os.path.expanduser("~/mnt/Soul Requiem")
TMX = ROOT + "/Assets/GamePackage/EPIC RPG World Pack - Crypt V.1.6/TiledMap Editor/Maps/map4_catacombs_of_naming.tmx"
MD5 = sys.argv[1]
b = open(TMX, 'rb').read(); assert hashlib.md5(b).hexdigest() == MD5, hashlib.md5(b).hexdigest()
shutil.copy2(TMX, ROOT + "/_backups/2026-11-18_m4_fix/map4_catacombs_of_naming.after_fix1.tmx")
s = b.decode('utf-8'); crlf = '\r\n' in s
if crlf: s = s.replace('\r\n', '\n')
m = re.search(r' <objectgroup id="21" name="gates">\n(.*?) </objectgroup>\n', s, re.S); assert m
inner = m.group(1); assert inner.count('<object ') == 1, inner.count('<object ')
s = s[:m.start()] + s[m.end():]
m2 = re.search(r'( <objectgroup id="26" name="gates">\n)', s); assert m2
s = s[:m2.end()] + inner + s[m2.end():]
assert s.count('name="gates"') == 1
a = s.index('name="oub_bar"'); e = s.index('</object>', a)
blk = s[a:e]; old = '<property name="range" type="float" value="0.6"/>'; assert old in blk
s = s[:a] + blk.replace(old, '<property name="range" type="float" value="0.2"/>', 1) + s[e:]
if crlf: s = s.replace('\n', '\r\n')
open(TMX, 'wb').write(s.encode('utf-8')); print('md5', hashlib.md5(s.encode('utf-8')).hexdigest())
