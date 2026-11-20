# 19/11 (§345): cổng kho phía bắc Reliquary (vault_door) mở khi hạ xong song đấu ở Warden Gallery (c4b) — giữ đúng tiến trình cũ
# (trước đây Warden ở đợt 2 của c4 nên c4_cleared = đã hạ Warden).
import re, sys, hashlib, shutil, os
R = os.path.expanduser('~/mnt/Soul Requiem/')
M4 = R + 'Assets/GamePackage/EPIC RPG World Pack - Crypt V.1.6/TiledMap Editor/Maps/map4_catacombs_of_naming.tmx'
md5 = lambda p: hashlib.md5(open(p, 'rb').read()).hexdigest()
if md5(M4) != sys.argv[1]: sys.exit('DỪNG: md5 ' + md5(M4))
shutil.copyfile(M4, R + '_backups/2026-11-19_m4gallery/map4_before_vault_door.tmx')
s = open(M4, 'rb').read().decode('utf-8')
m = re.search(r'<object id="908"[^>]*>.*?</object>', s, re.S); b = m.group(0)
assert b.count('value="c4"') == 1
s = s.replace(b, b.replace('value="c4"', 'value="c4b"'))
open(M4, 'wb').write(s.encode('utf-8')); print('xong md5', md5(M4))
