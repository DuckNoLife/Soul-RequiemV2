# 19/11 (§343): Map 3 — gục giữa trận Isolde phải đánh lại: khu x3 `reset_on_death` = west_bell_rung,isolde_summoned (gỡ cờ -> rung lại chuông Tây,
# cảnh bị kéo xuống Bower chạy lại); isolde_summon thêm `unless` = x3_cleared (đã hạ Isolde thì rung chuông không bị kéo xuống nữa). Kiểm md5, sao lưu.
import re, sys, hashlib, shutil, os
R = os.path.expanduser('~/mnt/Soul Requiem/')
M3 = R + 'Assets/GamePackage/EPIC RPG World Pack - Cemetery V 1.6/TiledMap Editor/Maps/map3_vale_of_graves.tmx'
B = R + '_backups/2026-11-19_zone_audio/'
md5 = lambda p: hashlib.md5(open(p, 'rb').read()).hexdigest()
if md5(M3) != '8989c54b9930566018774df22b3472f1': sys.exit('DỪNG: md5 ' + md5(M3))
os.makedirs(B, exist_ok=True); shutil.copyfile(M3, B + 'map3_vale_of_graves.tmx')
s = open(M3, 'rb').read().decode('utf-8')
NL = '\r\n' if '\r\n' in s else '\n'
def add_prop(oid, k, v):
    global s
    m = re.search(r'(<object id="%d"[^>]*>\s*<properties>)' % oid, s); assert m, oid
    blk = s[m.start():s.index('</object>', m.end())]
    assert 'name="%s"' % k not in blk, (oid, k)
    s = s[:m.end()] + NL + '    <property name="%s" value="%s"/>' % (k, v) + s[m.end():]
add_prop(4422, 'reset_on_death', 'west_bell_rung,isolde_summoned')
add_prop(5429, 'unless', 'x3_cleared')
open(M3, 'wb').write(s.encode('utf-8'))
print(md5(M3))
