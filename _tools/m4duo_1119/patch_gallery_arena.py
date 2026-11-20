# 19/11 (§345): thêm mốc duo_arena (khung sàn đấu Warden Gallery) — hai boss lấy làm tâm / bán kính đấu trường.
import re, sys, hashlib, shutil, os
R = os.path.expanduser('~/mnt/Soul Requiem/')
M4 = R + 'Assets/GamePackage/EPIC RPG World Pack - Crypt V.1.6/TiledMap Editor/Maps/map4_catacombs_of_naming.tmx'
md5 = lambda p: hashlib.md5(open(p, 'rb').read()).hexdigest()
if md5(M4) != sys.argv[1]: sys.exit('DỪNG: md5 ' + md5(M4))
shutil.copyfile(M4, R + '_backups/2026-11-19_m4gallery/map4_before_arena_marker.tmx')
s = open(M4, 'rb').read().decode('utf-8'); NL = '\r\n' if '\r\n' in s else '\n'
nid = int(re.search(r'nextobjectid="(\d+)"', s).group(1))
o = '<object id="%d" name="duo_arena" type="ritual_pillar" x="%g" y="%g" width="%g" height="%g"/>' % (nid, 78 * 32, 96.3 * 32, 29.6 * 32, 12.4 * 32)
k = s.index('name="duo_landing"'); k = s.index('/>', k) + 2
s = s[:k] + NL + '  ' + o + s[k:]
s = re.sub(r'nextobjectid="\d+"', 'nextobjectid="%d"' % (nid + 1), s, count=1)
open(M4, 'wb').write(s.encode('utf-8')); print('xong md5', md5(M4))
