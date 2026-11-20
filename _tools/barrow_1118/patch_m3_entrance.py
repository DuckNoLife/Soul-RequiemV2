# 18/11 (§330): Map 3 — lối xuống King's Barrow dời ra mép nam Sinking Fields, dưới gốc cây chết (ý tưởng đã duyệt 16/11);
# bỏ 3 lối "trèo xuống hố" + gợi ý "ánh vàng"; thêm bia nửa chìm + lời đồn Aldous. md5 kiểm, sao lưu, giữ CRLF.
import sys, re; sys.path.insert(0, '../lore_1116')
from patch_lib import Doc, esc
P = '../../Assets/GamePackage/EPIC RPG World Pack - Cemetery V 1.6/TiledMap Editor/Maps/map3_vale_of_graves.tmx'
d = Doc(P, '../../_backups/2026-11-18_red_field')
assert d.md5_before == '81eda2ae6525a58ff646202a7b0b5be7', d.md5_before
NL = '\r\n' if d.crlf else '\n'
T = 32
BAR = 4989                                   # SR-barrow sau SR-reliquary-eye (4988, 1 ô)
ENTR = BAR + 2                               # barrow_entrance (id 2 trong SR-barrow.tsx), 96x96

assert 'SR-barrow.tsx' not in d.src
d.gsub(' <tileset firstgid="4988" source="../Tilesets/SR-reliquary-eye.tsx"/>' + NL,
       ' <tileset firstgid="4988" source="../Tilesets/SR-reliquary-eye.tsx"/>' + NL + ' <tileset firstgid="%d" source="../Tilesets/SR-barrow.tsx"/>' % BAR + NL)

def drop(oid):
    m = re.search(r'\r?\n\s*<object id="%d"[^>]*[^/]>.*?</object>' % oid, d.src, re.S)
    assert m, oid
    d.src = d.src[:m.start()] + d.src[m.end():]
for oid in (4509, 5441, 5442, 4510, 5204):   # 3 lối hố cũ + bia "ánh vàng" + đèn vàng dưới hố
    drop(oid)

nid = [int(re.search(r'nextobjectid="(\d+)"', d.src).group(1))]
def new_id():
    nid[0] += 1; return nid[0] - 1
def P_(k, v):
    if isinstance(v, bool): return '    <property name="%s" type="bool" value="%s"/>' % (k, 'true' if v else 'false')
    return '    <property name="%s" value="%s"/>' % (k, esc(str(v).replace('\n', '\\n')))
def obj(name, typ, x, y, w, h, gid=None, props=None):
    a = '  <object id="%d"' % new_id() + (' name="%s"' % name if name else '') + (' type="%s"' % typ if typ else '') + (' gid="%d"' % gid if gid else '')
    a += ' x="%g" y="%g" width="%g" height="%g"' % (x, y, w, h)
    if not props: return [a + '/>']
    return [a + '>', '   <properties>'] + [P_(k, v) for k, v in sorted(props)] + ['   </properties>', '  </object>']
def add(layer, lines):
    m = re.search(r'<objectgroup id="\d+" name="%s"[^>]*>' % re.escape(layer), d.src); assert m, layer
    end = d.src.index(NL + ' </objectgroup>', m.end())
    d.src = d.src[:end] + NL + NL.join(lines) + d.src[end:]

CX, BY = 127.6, 78.2                          # tâm ngang / chân bậc đá (ô)
add('ground decals', obj('barrow_entrance', None, CX * T - 48, BY * T, 96, 96, gid=ENTR))
add('triggers', obj('barrow_stair', 'exit', (CX - 1.0) * T, (BY - 1.6) * T, 2.0 * T, 1.3 * T, props=[
    ('target', 'map3b_kings_barrow'), ('arrive_at', '4.6,5.4'), ('interact', True), ('verb', 'Descend'), ('quake', False),
    ('arrive_title', "King's Barrow"), ('arrive_subtitle', 'Beneath the Sinking Fields'),
    ('depart_text', 'The old stair sinks into the marsh and ends in a collapse. You lower yourself down the last of it on a rotten rope.'),
    ('zone_id', 'v8'), ('unity:IsTrigger', True)]))
add('triggers', obj('barrow_stone', 'sign', 124.6 * T, 75.9 * T, 1.4 * T, 0.9 * T, props=[
    ('zone_id', 'v8'), ('interact', True), ('verb', 'Read'), ('unity:IsTrigger', True),
    ('hint_text', 'A stone half sunk in the mud, its crown worn smooth. One word is left on it: "...ROW".')]))
add('obstacle colliders', obj('barrow_entrance collider', 'obstacle', (CX - 1.45) * T, (BY - 2.9) * T, 0.35 * T, 2.4 * T, props=[('collider_kind', 'prop')]))
add('obstacle colliders', obj('barrow_entrance collider', 'obstacle', (CX + 1.1) * T, (BY - 2.9) * T, 0.35 * T, 2.4 * T, props=[('collider_kind', 'prop')]))
d.src = re.sub(r'nextobjectid="\d+"', 'nextobjectid="%d"' % nid[0], d.src, count=1)

# lời đồn Aldous (manh mối): thêm câu cuối vào bộ lời gốc của Aldous ở commons
d.sub(5467, 'lines', "One of the few perks of the job.",
      "One of the few perks of the job. | My grandfather swore a king walked into the Sinking Fields and never walked out. There is an old stair down there, under the dead trees on the south bank. I have never had a reason to find out where it goes.")
# lối lên từ 3b phải về đúng bậc đá mới -> sửa ở map3b (x2_climb arrive_at) trong gen_3b.py
assert not d.err, d.err
print('M3 md5', d.save())
