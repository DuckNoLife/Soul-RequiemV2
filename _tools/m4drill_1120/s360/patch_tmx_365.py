# §365: trạm 3 đổi sang Sergeant Says -> gỡ 2 cần gạt máy phóng, ảnh xem trước máy phóng, thuộc tính launcher_1/2 của drill_yard. md5 kiểm, sao lưu, giữ CRLF.
import hashlib, os, re, shutil
M4 = 'Assets/GamePackage/EPIC RPG World Pack - Crypt V.1.6/TiledMap Editor/Maps/map4_catacombs_of_naming.tmx'
B = '_backups/2026-11-20_365/'
md5 = lambda p: hashlib.md5(open(p, 'rb').read()).hexdigest()
assert md5(M4) == '679008621e74a54bbba078f9dd01079e', md5(M4)
os.makedirs(B, exist_ok=True); shutil.copy2(M4, B)
s = open(M4, 'rb').read().decode('utf-8')
for name in ['drill_lever_w', 'drill_lever_e', 'drill_launcher_w_art', 'drill_launcher_e_art']:
    pat = re.compile(r'[ \t]*<object id="\d+" name="%s"[^>]*?(/>|>.*?</object>)\r?\n' % re.escape(name), re.S)
    s, n = pat.subn('', s, count=1); assert n == 1, name
for key in ['launcher_1', 'launcher_2']:
    s, n = re.subn(r'[ \t]*<property name="%s" value="[^"]*"/>\r?\n' % key, '', s, count=1); assert n == 1, key
open(M4, 'wb').write(s.encode('utf-8'))
print('xong md5', md5(M4))
