# 20/11 (§359): lớp "mechanics" bị ẩn trong Tiled (visible="0", lưu lúc người dùng sửa sau §354) -> SuperTiled2Unity bỏ qua cả lớp:
# mọi bẫy (gỗ lăn...), đèn, cơ quan Map 4 không chạy ("8 cơ quan", "0 đèn đuốc" trong Editor.log). Bỏ visible="0" ở MỌI lớp logic. Kiểm md5, sao lưu.
import re, sys, hashlib, shutil, os
R = os.path.expanduser('~/mnt/Soul Requiem/')
M4 = R + 'Assets/GamePackage/EPIC RPG World Pack - Crypt V.1.6/TiledMap Editor/Maps/map4_catacombs_of_naming.tmx'
md5 = lambda p: hashlib.md5(open(p, 'rb').read()).hexdigest()
if md5(M4) != sys.argv[1]: sys.exit('DỪNG: md5 ' + md5(M4))
shutil.copyfile(M4, R + '_backups/2026-11-20_359/map4_before_unhide.tmx')
s = open(M4, 'rb').read().decode('utf-8')
hidden = re.findall(r'<(?:objectgroup|layer) id="\d+" name="([^"]+)"[^>]* visible="0"', s)
print('lớp đang ẩn:', hidden)
s = re.sub(r'(<(?:objectgroup|layer) id="\d+" name="[^"]+"[^>]*?) visible="0"', r'\1', s)
open(M4, 'wb').write(s.encode('utf-8')); print('xong', md5(M4))
