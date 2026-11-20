# 20/11 (§352): chỉnh lại trang trí The Reliquary Lists theo góc nhìn thiết kế map (người dùng: không hợp style, đồ lố, banner lệch).
#  - BỎ: 3 cờ tím PixelLab (khác kiểu cờ của gói, treo thấp hơn cờ cam, đè lên khung cửa), 2 bánh răng (steampunk lạc đề, đè khung cửa),
#        3 đống xu + 2 lò lửa giữa sàn (rác thị giác trên sàn đấu, che vòng cảnh báo), cụm pha lê thứ hai mỗi cột (đối xứng copy-paste),
#        2 chân nến chép từ Reliquary ở góc đông bắc + va chạm + đèn của chúng (chen vào cột pha lê / lò lửa).
#  - CỜ TƯỜNG TÂY: dùng đúng cờ của gói (gid như 2 cờ cam bên đông), cùng cao độ, đặt vào 2 trụ giữa các cửa (x83, x86).
#  - Ảnh tỉ lệ 1:1 (bỏ phóng 1,1 / 1,6 / 1,7 — mixel), cột pha lê dịu màu, bệ pháo vẽ lại theo bảng màu tường crypt.
#  - Cột pha lê lùi khỏi mặt tường (không đè khung cửa), đối xứng quanh tâm phòng; mỗi cột một cụm pha lê nép chân phía ngoài.
#  - Lò lửa hồn chỉ ở hai bên cổng vòm (chân tường); 4 rune tím của gói Crypt quanh bệ pháo (nối bệ với sàn).
import re, sys, hashlib, shutil, os
R = os.path.expanduser('~/mnt/Soul Requiem/')
M4 = R + 'Assets/GamePackage/EPIC RPG World Pack - Crypt V.1.6/TiledMap Editor/Maps/map4_catacombs_of_naming.tmx'
md5 = lambda p: hashlib.md5(open(p, 'rb').read()).hexdigest()
if md5(M4) != sys.argv[1]: sys.exit('DỪNG: md5 ' + md5(M4))
shutil.copyfile(M4, R + '_backups/2026-11-20_lists/map4_before_restyle.tmx')
s = open(M4, 'rb').read().decode('utf-8'); NL = '\r\n' if '\r\n' in s else '\n'
T = 32
FIRST = int(re.search(r'<tileset firstgid="(\d+)" source="../Tilesets/SR-lists-art.tsx"/>', s).group(1))
TID = {'soul_brazier': 2, 'crystal_cluster_crimson': 4, 'crystal_cluster_azure': 5, 'crystal_cluster_verdant': 6, 'crystal_cluster_gilded': 7,
       'crystal_pillar_crimson': 8, 'crystal_pillar_azure': 9, 'crystal_pillar_verdant': 10, 'crystal_pillar_gilded': 11, 'engine_pedestal': 12,
       'floor_rune_0': 13, 'floor_rune_1': 14, 'floor_rune_2': 15, 'floor_rune_3': 16}
from PIL import Image
SIZE = {n: Image.open(R + 'Assets/Resources/VFX/props/' + n + '.png').size for n in TID}

def remove(pattern, label, expect=None):
    global s
    ms = list(re.finditer(r'(\r?\n)[ \t]*<object id="\d+"[^>]*?(?:/>|>.*?</object>)', s, re.S))
    n = 0
    for m in reversed(ms):
        head = re.match(r'(\r?\n)[ \t]*(<object [^>]*>)', m.group(0)).group(2)
        if re.search(pattern, head) or re.search(pattern, m.group(0)[:400]):
            s = s[:m.start()] + s[m.end():]; n += 1
    print('bỏ', n, label)
    if expect is not None: assert n == expect, (label, n)
remove(r'name="lists_banner_\d"', 'cờ tím', 3)
remove(r'name="lists_gear_\d"', 'bánh răng', 2)
remove(r'name="lists_coins_\d"', 'đống xu', 3)
remove(r'name="lists_brazier_[23]"', 'lò lửa giữa sàn', 2)
remove(r'name="crystal_cluster_\w+_1"', 'cụm pha lê thừa', 4)
remove(r'<object id="\d+" gid="10381" x="(3078.4|3379.2)" y="(3112.8|3151.2)"', 'chân nến góc đông bắc', 2)
remove(r'name="prop collider" type="obstacle" x="(3100.8|3401.6)" y="(3104|3142.4)"', 'va chạm chân nến', 2)
remove(r'name="wg_cdl_[01]"', 'đèn chân nến', 2)

nid = int(re.search(r'nextobjectid="(\d+)"', s).group(1))
def set_tile(name, tile, cx, foot, flip=False):
    """Đặt lại tile object `name` (lớp mechanics_art): ảnh, tỉ lệ 1:1, chân giữa tại (cx, foot) ô."""
    global s
    m = re.search(r'<object id="(\d+)" name="%s" gid="\d+" x="[\d.\-]+" y="[\d.\-]+" width="[\d.]+" height="[\d.]+"' % re.escape(name), s)
    assert m, name
    w, h = SIZE[tile]
    gid = FIRST + TID[tile] + (0x80000000 if flip else 0)
    s = s.replace(m.group(0), '<object id="%s" name="%s" gid="%d" x="%g" y="%g" width="%d" height="%d"' % (m.group(1), name, gid, cx * T - w / 2, foot * T, w, h), 1)
def set_rect(name, x, y, w, h):
    global s
    m = re.search(r'(<object id="\d+" name="%s" type="[\w_]+") x="[\d.]+" y="[\d.]+" width="[\d.]+" height="[\d.]+"' % re.escape(name), s)
    assert m, name
    s = s.replace(m.group(0), m.group(1) + ' x="%g" y="%g" width="%g" height="%g"' % (x * T, y * T, w * T, h * T), 1)
def new_tile(name, tile, cx, foot, flip=False):
    global nid
    w, h = SIZE[tile]
    o = '<object id="%d" name="%s" gid="%d" x="%g" y="%g" width="%d" height="%d"/>' % (nid, name, FIRST + TID[tile] + (0x80000000 if flip else 0), cx * T - w / 2, foot * T, w, h)
    nid += 1
    return o
# lò lửa hai bên cổng vòm
set_tile('lists_brazier_0', 'soul_brazier', 90.6, 96.75)
set_tile('lists_brazier_1', 'soul_brazier', 95.4, 96.75, True)
# cột pha lê: đối xứng quanh tâm phòng (x 92.5), lùi khỏi tường
CR = {'crimson': (80.5, 98.7), 'azure': (104.5, 98.7), 'verdant': (80.5, 108.0), 'gilded': (104.5, 108.0)}
for c, (x, y) in CR.items():
    set_rect('crystal_' + c, x - 0.5, y - 0.5, 1.0, 0.5)
    set_tile('crystal_%s_art' % c, 'crystal_pillar_' + c, x, y + 0.06)
    side = -1 if x < 92.5 else 1
    set_tile('crystal_cluster_%s_0' % c, 'crystal_cluster_' + c, x + side * 0.95, y + 0.15, side > 0)
# bệ pháo tỉ lệ 1:1
m = re.search(r'<object id="\d+" name="duo_core" type="ritual_pillar" x="([\d.]+)" y="([\d.]+)" width="([\d.]+)" height="([\d.]+)"', s)
cx = (float(m.group(1)) + float(m.group(3)) / 2) / T; foot = (float(m.group(2)) + float(m.group(4))) / T
set_tile('duo_core_art', 'engine_pedestal', cx, foot + 0.1 / 0.32)
# rune tím quanh bệ (nằm bẹt trên sàn)
runes = [new_tile('lists_rune_%d' % i, 'floor_rune_%d' % i, x, y) for i, (x, y) in enumerate(((cx - 3.2, foot + 0.6), (cx + 3.2, foot + 0.6), (cx - 1.9, foot + 3.0), (cx + 1.9, foot + 3.0)))]
mm = re.search(r'(<objectgroup id="27" name="mechanics_art"[^>]*>.*?)(\s*</objectgroup>)', s, re.S)
s = s[:mm.end(1)] + ''.join(NL + '  ' + o for o in runes) + s[mm.end(1):]
# cờ của gói trên 2 trụ tường tây (cùng cao độ với cờ cam phía đông)
ban = []
for x, gid in ((83, 9925), (86, 9924)):
    ban.append('<object id="%d" gid="%d" x="%g" y="3067.2" width="32" height="96"/>' % (nid, gid, x * T)); nid += 1
m = re.search(r'<object id="\d+" gid="9925" x="3344" y="3067.2" width="32" height="96"/>', s)
assert m, 'không thấy cờ cam phía đông'
s = s[:m.end()] + ''.join(NL + '  ' + o for o in ban) + s[m.end():]
s = re.sub(r'nextobjectid="\d+"', 'nextobjectid="%d"' % nid, s, count=1)
open(M4, 'wb').write(s.encode('utf-8'))
print('xong', md5(M4))
