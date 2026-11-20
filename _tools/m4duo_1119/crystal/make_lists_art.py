# 20/11 (§351): đồ trang trí / cột pha lê / bệ pháo của Reliquary Lists THẤY ĐƯỢC TRONG TILED (người dùng: chỉ thấy khung trống).
# 1) tileset mới SR-lists-art.tsx (ảnh Resources/VFX/props, tile ghi sẵn class sprite_prop + thuộc tính mặc định) — như SR-mech-art của Map 2.
# 2) Map 4: thêm tileset (firstgid sau SR-props-trimmed), 20 sprite_prop hình chữ nhật -> tile object trong lớp mechanics_art (kéo thả được),
#    4 cột pha lê + duo_core thêm hình xem trước `art_for` (game vẫn tự vẽ, ảnh tile bị tắt). Kiểm md5, sao lưu.
import re, sys, hashlib, shutil, os
from PIL import Image
R = os.path.expanduser('~/mnt/Soul Requiem/')
CRYPT = R + 'Assets/GamePackage/EPIC RPG World Pack - Crypt V.1.6/TiledMap Editor/'
M4 = CRYPT + 'Maps/map4_catacombs_of_naming.tmx'
TSX = CRYPT + 'Tilesets/SR-lists-art.tsx'
PROPS = R + 'Assets/Resources/VFX/props/'
md5 = lambda p: hashlib.md5(open(p, 'rb').read()).hexdigest()
if md5(M4) != sys.argv[1]: sys.exit('DỪNG: md5 ' + md5(M4))
shutil.copyfile(M4, R + '_backups/2026-11-20_lists/map4_before_lists_art.tmx')
COLS = ['crimson', 'azure', 'verdant', 'gilded']
LC = {'crimson': '#ff5a40', 'azure': '#66c8ff', 'verdant': '#70ff66', 'gilded': '#ffd85a'}
tiles = [('lists_banner', 'sprite_prop', [('collider', '0')]),
         ('wall_gear', 'sprite_prop', [('collider', '0')]),
         ('soul_brazier', 'sprite_prop', [('collider', '0.26,0.12'), ('collider_shape', 'round'), ('lit', True), ('light_color', '#a46bff'), ('light_radius', 1.9), ('light_intensity', 0.85)]),
         ('coin_heap', 'sprite_prop', [('collider', '0'), ('flat', True)])]
tiles += [('crystal_cluster_' + c, 'sprite_prop', [('collider', '0'), ('lit', True), ('light_color', LC[c]), ('light_radius', 1.1), ('light_intensity', 0.6)]) for c in COLS]
tiles += [('crystal_pillar_' + c, '', []) for c in COLS] + [('engine_pedestal', '', [])]
tiles += [('floor_rune_%d' % i, 'sprite_prop', [('collider', '0'), ('flat', True)]) for i in range(4)]   # 20/11 (§352): rune sàn của gói Crypt
def pv(k, v):
    if isinstance(v, bool): return '<property name="%s" type="bool" value="%s"/>' % (k, 'true' if v else 'false')
    if isinstance(v, float): return '<property name="%s" type="float" value="%g"/>' % (k, v)
    return '<property name="%s" value="%s"/>' % (k, v)
size = {}
out = ['<?xml version="1.0" encoding="UTF-8"?>',
       '<tileset version="1.10" tiledversion="1.12.2" name="SR-lists-art" tilewidth="32" tileheight="32" tilecount="%d" columns="0">' % len(tiles),
       ' <grid orientation="orthogonal" width="1" height="1"/>']
for i, (n, cls, props) in enumerate(tiles):
    w, h = Image.open(PROPS + n + '.png').size; size[n] = (w, h)
    out.append(' <tile id="%d"%s>' % (i, ' type="%s"' % cls if cls else ''))
    out.append('  <properties>')
    out.append('   ' + pv('sprite', 'VFX/props/' + n))
    for k, v in props: out.append('   ' + pv(k, v))
    out.append('  </properties>')
    out.append('  <image source="../../../../Resources/VFX/props/%s.png" width="%d" height="%d"/>' % (n, w, h))
    out.append(' </tile>')
out.append('</tileset>')
open(TSX, 'w', encoding='utf-8', newline='\n').write('\n'.join(out) + '\n')
tid = {n: i for i, (n, _, _) in enumerate(tiles)}

s = open(M4, 'rb').read().decode('utf-8'); NL = '\r\n' if '\r\n' in s else '\n'
T = 32
last = list(re.finditer(r'<tileset firstgid="(\d+)" source="([^"]+)"/>', s))[-1]
assert 'SR-props-trimmed' in last.group(2), last.group(2)
FIRST = int(last.group(1)) + 163
if 'SR-lists-art.tsx' not in s:
    s = s[:last.end()] + NL + ' <tileset firstgid="%d" source="../Tilesets/SR-lists-art.tsx"/>' % FIRST + s[last.end():]
nid = int(re.search(r'nextobjectid="(\d+)"', s).group(1))
def tobj(name, tile, cx, foot, scale=1.0, flip=False, props=()):
    global nid
    w, h = size[tile]
    gid = FIRST + tid[tile] + (0x80000000 if flip else 0)
    p = ''
    if props: p = '>' + NL + '   <properties>' + NL + ''.join('    ' + pv(k, v) + NL for k, v in props) + '   </properties>' + NL + '  </object>'
    o = '<object id="%d" name="%s" gid="%d" x="%g" y="%g" width="%g" height="%g"%s' % (nid, name, gid, cx * T - w * scale / 2, foot * T, w * scale, h * scale, p if p else '/>')
    nid += 1
    return o
# 1) chuyển 20 sprite_prop hình chữ nhật -> tile object
arts = []
for m in reversed(list(re.finditer(r'(\r?\n)\s*<object id="\d+" name="([^"]+)" type="sprite_prop" x="([\d.]+)" y="([\d.]+)" width="([\d.]+)" height="([\d.]+)">(.*?)</object>', s, re.S))):
    name = m.group(2)
    if not (name.startswith('lists_') or name.startswith('crystal_cluster_')): continue
    x, y, w, h = (float(m.group(i)) for i in range(3, 7))
    props = dict(re.findall(r'name="([^"]+)"(?: type="\w+")? value="([^"]*)"', m.group(7)))
    tile = props['sprite'].split('/')[-1]
    arts.append(tobj(name, tile, (x + w / 2) / T, (y + h) / T, float(props.get('scale', '1')), props.get('flip') == 'true'))
    s = s[:m.start()] + s[m.end():]
assert len(arts) == 20, len(arts)
# 2) hình xem trước cho cột pha lê + bệ pháo
for c in COLS:
    m = re.search(r'<object id="\d+" name="crystal_%s" type="crystal_pillar" x="([\d.]+)" y="([\d.]+)" width="([\d.]+)" height="([\d.]+)"' % c, s)
    x, y, w, h = (float(v) for v in m.groups())
    arts.append(tobj('crystal_%s_art' % c, 'crystal_pillar_' + c, (x + w / 2) / T, (y + h) / T + 0.06, 1.6, False, [('art_for', 'crystal_' + c)]))
m = re.search(r'<object id="\d+" name="duo_core" type="ritual_pillar" x="([\d.]+)" y="([\d.]+)" width="([\d.]+)" height="([\d.]+)"', s)
x, y, w, h = (float(v) for v in m.groups())
arts.append(tobj('duo_core_art', 'engine_pedestal', (x + w / 2) / T, (y + h) / T + 0.1 / 0.32, 1.7, False, [('art_for', 'duo_core')]))
mm = re.search(r'(<objectgroup id="27" name="mechanics_art"[^>]*>.*?)(\s*</objectgroup>)', s, re.S)
s = s[:mm.end(1)] + ''.join(NL + '  ' + o for o in arts) + s[mm.end(1):]
s = re.sub(r'nextobjectid="\d+"', 'nextobjectid="%d"' % nid, s, count=1)
open(M4, 'wb').write(s.encode('utf-8'))
print('xong', md5(M4), 'firstgid', FIRST, len(arts), 'tile object')
