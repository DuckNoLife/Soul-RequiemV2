# 18/11 (§330): ghi map3b mới (sinh bằng gen_3b.py từ bản 6a72b321…) vào Assets — kiểm md5 đầu vào, sao lưu.
import hashlib, shutil, os
P='../../Assets/GamePackage/EPIC RPG World Pack - Cemetery V 1.6/TiledMap Editor/Maps/map3b_kings_barrow.tmx'
md5=lambda p: hashlib.md5(open(p,'rb').read()).hexdigest()
assert md5(P)=='6a72b3212240fae02a0e1c3a121ef734', md5(P)
B='../../_backups/2026-11-18_red_field'; os.makedirs(B,exist_ok=True); shutil.copy2(P,B+'/map3b_kings_barrow.tmx')
open(P,'wb').write(open('map3b_new.tmx','rb').read())
print('3b md5', md5(P))
