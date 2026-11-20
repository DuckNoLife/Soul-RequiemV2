# 20/11 (§354): ấn đã lắp (seal1_in / seal2_in) bị đế ấn seal_socket_1/2 che: cùng lớp `props` sắp theo chân, chân đế thấp hơn chân ấn
# -> chuyển 2 ấn sang lớp `props on tables` (SortingOrder 3, như đồ đặt trên bàn) để luôn vẽ trên đế. Kiểm md5, sao lưu, giữ CRLF.
import re, sys, hashlib, shutil, os
R = os.path.expanduser('~/mnt/Soul Requiem/')
M4 = R + 'Assets/GamePackage/EPIC RPG World Pack - Crypt V.1.6/TiledMap Editor/Maps/map4_catacombs_of_naming.tmx'
md5 = lambda p: hashlib.md5(open(p, 'rb').read()).hexdigest()
if md5(M4) != sys.argv[1]: sys.exit('DỪNG: md5 ' + md5(M4))
os.makedirs(R + '_backups/2026-11-20_354/', exist_ok=True)
shutil.copyfile(M4, R + '_backups/2026-11-20_354/map4_before_seal_layer.tmx')
s = open(M4, 'rb').read().decode('utf-8')
moved = []
for n in ('seal1_in', 'seal2_in'):
    m = re.search(r'[ \t]*<object id="\d+" name="%s" [^>]*/>\r\n' % n, s); assert m, n
    s = s.replace(m.group(0), '', 1); moved.append(m.group(0))
m = re.search(r'(<objectgroup id="100" name="props on tables">.*?)([ \t]*</objectgroup>)', s, re.S); assert m
s = s[:m.end(1)] + ''.join(moved) + s[m.end(1):]
open(M4, 'wb').write(s.encode('utf-8'))
print('xong', md5(M4))
