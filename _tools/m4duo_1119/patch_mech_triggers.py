# 20/11 (§348): 12 object cơ quan mới của Warden Gallery thiếu unity:IsTrigger -> SuperTiled2Unity tạo va chạm ĐẶC
# (duo_arena phủ cả sàn phòng = tường vô hình, Ren không vào được). Thêm unity:IsTrigger = true. Kiểm md5, sao lưu, giữ CRLF.
import re, sys, hashlib, shutil, os
R = os.path.expanduser('~/mnt/Soul Requiem/')
M4 = R + 'Assets/GamePackage/EPIC RPG World Pack - Crypt V.1.6/TiledMap Editor/Maps/map4_catacombs_of_naming.tmx'
md5 = lambda p: hashlib.md5(open(p, 'rb').read()).hexdigest()
if md5(M4) != sys.argv[1]: sys.exit('DỪNG: md5 ' + md5(M4))
shutil.copyfile(M4, R + '_backups/2026-11-19_m4gallery/map4_before_mech_triggers.tmx')
s = open(M4, 'rb').read().decode('utf-8'); NL = '\r\n' if '\r\n' in s else '\n'
TRIG = '<property name="unity:IsTrigger" type="bool" value="true"/>'
names = ['warden_statue_%d' % i for i in range(3)] + ['powder_keg_%d' % i for i in range(6)] + ['duo_gallery', 'duo_landing', 'duo_arena']
for n in names:
    m = re.search(r'<object id="\d+" name="%s"[^>]*?/>' % n, s)
    if m:
        b = m.group(0)
        s = s.replace(b, b[:-2] + '>' + NL + '   <properties>' + NL + '    ' + TRIG + NL + '   </properties>' + NL + '  </object>', 1)
        continue
    m = re.search(r'<object id="\d+" name="%s"[^>]*[^/]>.*?</object>' % n, s, re.S); assert m, n
    b = m.group(0); assert TRIG not in b
    s = s.replace(b, b.replace('</properties>', ' ' + TRIG + NL + '   </properties>', 1), 1)
open(M4, 'wb').write(s.encode('utf-8')); print('xong md5', md5(M4))
