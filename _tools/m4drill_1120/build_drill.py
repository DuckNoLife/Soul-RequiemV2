# 20/11 (§356): Map 4 — CÁNH PHÍA TÂY NAM "THE WARDERS' DRILL": 3 khu mới nối từ tường tây The Rolling Hall:
#   n1 The Penance Run (bẫy: sóng gai, 3 rãnh cưa, tên từ hai vách) -> n2 The Tally Room (câu đố thứ tự bài tập, 4 cọc người nộm + sổ tập)
#   -> n3 The Drill Yard (minigame né người nộm, Trung sĩ Burlap). Chìa giám ngục (warden_key) mở cửa drill_door; qua minigame lấy
#   chìa của trung sĩ (drill_key) mở phòng lính gác (guard_door) -> đồ của Ren.
#   Lối vào: cửa thông Question Room -> đoạn cuối Rolling Hall (hàng 141-142), rào chắn vĩnh viễn ngang Rolling Hall (hàng ~131) tách đoạn cuối
#   khỏi phần bắc (phần bắc vẫn chỉ mở sau cảnh Pell như cũ); khúc gỗ lăn dừng ở rào.
# Kiểm md5 (tham số 1), sao lưu, giữ CRLF, ghi tileset SR-drill-art.tsx. Chạy: python3 build_drill.py <md5> [out]
import re, sys, hashlib, shutil, os, random
R = os.path.expanduser('~/mnt/Soul Requiem/')
TM = R + 'Assets/GamePackage/EPIC RPG World Pack - Crypt V.1.6/TiledMap Editor/'
M4 = TM + 'Maps/map4_catacombs_of_naming.tmx'
TSX = TM + 'Tilesets/SR-drill-art.tsx'
OUT = sys.argv[2] if len(sys.argv) > 2 else M4
md5 = lambda p: hashlib.md5(open(p, 'rb').read()).hexdigest()
if md5(M4) != sys.argv[1]: sys.exit('DỪNG: md5 ' + md5(M4))
B = R + '_backups/2026-11-20_356/'
os.makedirs(B, exist_ok=True)
if OUT == M4: shutil.copyfile(M4, B + 'map4_before_drill.tmx')
s = open(M4, 'rb').read().decode('utf-8'); NL = '\r\n' if '\r\n' in s else '\n'
W, H, T = 114, 148, 32
random.seed(356)

# ================================================================== tileset SR-drill-art (ảnh xem trước trong Tiled + đồ đặt)
FIRST = 10552
assert 'SR-drill-art.tsx' not in s
tiles = [  # (id, ảnh, type, thuộc tính)
    (0, '../../../../Resources/VFX/props/drill_horn.png', None, {}),
    (1, '../../../../Resources/VFX/props/drill_ledger.png', 'sprite_prop', {'sprite': 'VFX/props/drill_ledger', 'collider': '0.36,0.14'}),
    (2, '../../../../Resources/VFX/props/drill_keyped.png', 'sprite_prop', {'sprite': 'VFX/props/drill_keyped', 'collider': '0.26,0.12'}),
    (3, '../../../../Resources/VFX/props/drill_keyring.png', None, {}),
    (4, '../../Props/SR-drill-art/drill_sack_prev.png', None, {}),
    (5, '../../Props/SR-drill-art/drill_straw_prev.png', None, {}),
    (6, '../../Props/SR-drill-art/drill_pell_prev.png', None, {}),
    (7, '../../Props/SR-drill-art/drill_bust_prev.png', None, {}),
    (8, '../../Props/SR-drill-art/drill_king_prev.png', None, {}),
]
from PIL import Image
def img_size(rel):
    return Image.open(os.path.normpath(os.path.join(TM + 'Tilesets/', rel))).size
SIZE = {}
lines = ['<?xml version="1.0" encoding="UTF-8"?>',
         '<tileset version="1.10" tiledversion="1.12.2" name="SR-drill-art" tilewidth="32" tileheight="32" tilecount="%d" columns="0">' % len(tiles),
         ' <grid orientation="orthogonal" width="1" height="1"/>']
for tid, rel, typ, pr in tiles:
    w, h = img_size(rel); SIZE[tid] = (w, h)
    lines.append(' <tile id="%d"%s>' % (tid, ' type="%s"' % typ if typ else ''))
    if pr:
        lines.append('  <properties>')
        for k, v in pr.items(): lines.append('   <property name="%s" value="%s"/>' % (k, v))
        lines.append('  </properties>')
    lines.append('  <image source="%s" width="%d" height="%d"/>' % (rel, w, h))
    lines.append(' </tile>')
lines.append('</tileset>')
open(TSX, 'wb').write((NL.join(lines) + NL).encode('utf-8'))
m = re.search(r'( <tileset firstgid="10535" source="\.\./Tilesets/SR-lists-art\.tsx"/>)', s); assert m
s = s.replace(m.group(1), m.group(1) + NL + ' <tileset firstgid="%d" source="../Tilesets/SR-drill-art.tsx"/>' % FIRST, 1)

# ================================================================== lớp ô
def get_layer(name):
    m = re.search(r'(<layer id="\d+" name="%s"[^>]*>.*?<data encoding="csv">)(.*?)(</data>)' % re.escape(name), s, re.S)
    vals = [int(v) for v in m.group(2).replace('\r', '').replace('\n', '').split(',') if v.strip()]
    assert len(vals) == W * H, name
    return m, vals
def put_layer(name, vals):
    global s
    m, _ = get_layer(name)
    rows = [','.join(str(v) for v in vals[y * W:(y + 1) * W]) for y in range(H)]
    s = s[:m.start(2)] + NL + (',' + NL).join(rows) + NL + s[m.end(2):]
m0, v0 = get_layer('rock'); old = s; put_layer('rock', v0); assert s == old, 'csv round-trip lệch'
P = get_layer('op_plat')[1]; CELLS = get_layer('op_cells')[1]
P0 = list(P)
def p(x, y, v): P[y * W + x] = v
FLOOR = [8415] * 55 + [8447] * 52 + [8449] * 51 + [8432] * 49 + [8414] * 47 + [8431] * 42 + [8446] * 41 + [8430] * 41 + [8416] * 31 + [8140] * 26 + \
        [8410] * 18 + [8428] * 16 + [8443] * 15 + [8412] * 14 + [8442] * 14 + [8427] * 13 + [8445] * 12 + [8411] * 9 + [8448] * 8 + [8426] * 8
fl = lambda: random.choice(FLOOR)
WALKABLE = set()
def room(x0, x1, top, fy1):
    """khung x0..x1 (cột mép), hàng mép trên `top`, sàn tới hàng fy1, mặt vách dưới fy1+1..fy1+3."""
    p(x0, top, 8122); p(x1, top, 8126)
    for x in range(x0 + 1, x1): p(x, top, 3113)
    for y in range(top + 1, fy1 + 1):
        p(x0, y, random.choice((3095, 3111))); p(x1, y, random.choice((3112, 3096)))
        for x in range(x0 + 1, x1): p(x, y, fl()); WALKABLE.add((x, y))
    b = fy1 + 1
    for i, (sw, se) in enumerate(((3102, 3105), (2965, 2969), (2981, 2985))): p(x0, b + i, sw); p(x1, b + i, se)
    for x in range(x0 + 1, x1):
        k = random.choice((0, 0, 0, 1, 2, 3))
        p(x, b, 3045 + k); p(x, b + 1, 3061 + k); p(x, b + 2, 3077 + k)
def opening(R, G, L, y0, y1):
    """lỗ thông giữa mép phải phòng tây (cột R), khe G, mép trái phòng đông (cột L); sàn hàng y0..y1 (mẫu lối Blade Yard -> Rolling Hall)."""
    p(R, y0 - 1, 8125); p(L, y0 - 1, 8123)
    if G is not None: p(G, y0 - 1, 8108)
    for y in range(y0, y1 + 1):
        for x in (R, G, L):
            if x is not None: p(x, y, fl()); WALKABLE.add((x, y))
    for i, (r, g, l) in enumerate(((2952, 3045, 2950), (2927, 3061, 2926), (2943, 3077, 2942))):
        p(R, y1 + 1 + i, r); p(L, y1 + 1 + i, l)
        if G is not None: p(G, y1 + 1 + i, g)
# 3 phòng: n3 Drill Yard (x1..23), n2 Tally Room (x25..41), n1 Penance Run (x43..65)
room(1, 23, 120, 143); room(25, 41, 131, 143); room(43, 65, 132, 143)
OY0, OY1 = 137, 139
opening(23, 24, 25, OY0, OY1); opening(41, 42, 43, OY0, OY1)
# lối Penance Run -> Rolling Hall: cột 65 (mép phải n1), 66 khe, 67 (mép trái Rolling Hall) + gỡ vách ô giam cột 68
opening(65, 66, 67, OY0, OY1)
for y in range(OY0, OY1 + 1): CELLS[y * W + 68] = 0; WALKABLE.add((68, y))
# hàng 143 cột 67 vốn là góc dưới-trái của Rolling Hall (3102) — giữ nguyên
# cửa Question Room <-> đoạn cuối Rolling Hall: gỡ vách ô giam cột 76 hàng 141-142
for y in (141, 142): CELLS[y * W + 76] = 0
over = [(i % W, i // W) for i in range(W * H) if P0[i] and P[i] != P0[i] and i % W < 65]
assert not over, ('đè ô có sẵn', over[:10])
put_layer('op_plat', P); put_layer('op_cells', CELLS)

# ================================================================== object
nid = int(re.search(r'nextobjectid="(\d+)"', s).group(1))
def new_id():
    global nid
    nid += 1; return nid - 1
def blk(oid):
    m = re.search(r'<object id="%d"[^>]*?/>' % oid, s) or re.search(r'<object id="%d"[^>]*[^/]>.*?</object>' % oid, s, re.S)
    assert m, oid; return m.group(0)
def put(old, new):
    global s
    assert s.count(old) == 1, old[:80]; s = s.replace(old, new)
def esc(v): return str(v).replace('&', '&amp;').replace('"', '&quot;').replace('<', '&lt;').replace('>', '&gt;')
def props(d):
    out = []
    for k, v in d.items():
        if isinstance(v, bool): out.append('<property name="%s" type="bool" value="%s"/>' % (k, 'true' if v else 'false'))
        elif isinstance(v, int): out.append('<property name="%s" type="int" value="%d"/>' % (k, v))
        elif isinstance(v, float): out.append('<property name="%s" type="float" value="%g"/>' % (k, v))
        else: out.append('<property name="%s" value="%s"/>' % (k, esc(v)))
    return '<properties>' + NL + ''.join('    ' + o + NL for o in out) + '   </properties>'
def rect(name, typ, x, y, w, h, d=None):
    body = '<object id="%d" name="%s"%s x="%g" y="%g" width="%g" height="%g"' % (new_id(), name, ' type="%s"' % typ if typ else '', x * T, y * T, w * T, h * T)
    return body + ('>' + NL + '   ' + props(d) + NL + '  </object>' if d else '/>')
def tobj(name, gid, x, bottom, w_px, h_px, d=None):
    body = '<object id="%d"%s gid="%d" x="%g" y="%g" width="%d" height="%d"' % (new_id(), ' name="%s"' % name if name else '', gid, x * T, bottom * T, w_px, h_px)
    return body + ('>' + NL + '   ' + props(d) + NL + '  </object>' if d else '/>')
def add_to_group(gname, items, gid_attr=None):
    global s
    mm = re.search(r'(<objectgroup id="\d+" name="%s"[^>]*>.*?)(\s*</objectgroup>)' % re.escape(gname), s, re.S)
    assert mm, gname
    s = s[:mm.end(1)] + ''.join(NL + '  ' + x for x in items) + s[mm.end(1):]
def add_to_group_id(gid, items):
    global s
    mm = re.search(r'(<objectgroup id="%d" [^>]*>.*?)(\s*</objectgroup>)' % gid, s, re.S)
    assert mm, gid
    s = s[:mm.end(1)] + ''.join(NL + '  ' + x for x in items) + s[mm.end(1):]
TRIG = {'unity:IsTrigger': True}
def light(name, x, y, color='#7fa8ff', rad=2.8, inten=0.8):
    return rect(name, 'light', x - 0.5, y - 0.5, 1, 1, dict(anim='flicker', anim_amount='0.15', anim_speed='0.8', color=color, intensity=str(inten),
                                                         light='torch', mechanic='light', radius=str(rad), **TRIG))

# ---- khối chắn: bỏ các khối vực cũ, dựng lại theo lưới ô (ô không đi được trong vùng U)
OLD = [1066, 1063, 1093, 1082, 1105]
U = [(0, 98, 30, 148), (30, 92, 34, 148), (34, 127, 65, 148), (65, 120, 69, 141), (65, 141, 68, 148)]
for oid in OLD:
    b = blk(oid); assert 'blocker' in b, oid
    mm = re.search(r'[ \t]*' + re.escape(b) + r'\r?\n', s); assert mm, oid
    s = s.replace(mm.group(0), '', 1)
inU = lambda x, y: any(a <= x < c and b <= y < d for a, b, c, d in U)
blocked = {(x, y) for x in range(0, 69) for y in range(92, 148) if inU(x, y) and (x, y) not in WALKABLE}
# gộp thành hình chữ nhật: chạy ngang theo hàng, rồi nối dọc các chạy trùng khoảng
runs = {}
for y in range(92, 148):
    x = 0
    while x < 69:
        if (x, y) in blocked:
            x1 = x
            while (x1 + 1, y) in blocked: x1 += 1
            runs.setdefault((x, x1), []).append(y); x = x1 + 1
        else: x += 1
rects = []
for (xa, xb), ys in runs.items():
    ys.sort(); st = ys[0]; pv = ys[0]
    for yy in ys[1:] + [None]:
        if yy is not None and yy == pv + 1: pv = yy; continue
        rects.append((xa, st, xb - xa + 1, pv - st + 1))
        if yy is not None: st = pv = yy
bl = lambda x, y, w, h: rect('blocker', 'blocker', x, y, w, h, {'collider_kind': 'terrain'})
new_blk = [bl(*r) for r in rects]
# cửa Question Room <-> Rolling Hall: tách khối 1106 (76,141,1,6) -> chỉ còn từ hàng 143
b = blk(1106); put(b, b.replace('y="%g"' % (141 * T), 'y="%g"' % (143 * T)).replace('height="%g"' % (6 * T), 'height="%g"' % (4 * T)))
# rào chắn vĩnh viễn ngang Rolling Hall (tách đoạn cuối khỏi phần bắc)
new_blk.append(bl(68, 130.7, 7, 1.1))
add_to_group('blockers', new_blk)
print('khối chắn mới:', len(new_blk))

# ---- khúc gỗ lăn dừng ở rào (hàng 103..130)
b = blk(933); put(b, b.replace('height="%g"' % (39 * T), 'height="%g"' % (27 * T)))
# ---- hình rào ngang Rolling Hall (bàn dựng + ván + thùng, như rào phía tây)
add_to_group('props', [tobj('rh_barricade_0', 10455, 68.9, 131.9, 64, 55), tobj('rh_barricade_1', 10500, 70.9, 131.9, 64, 88),
                       tobj('rh_barricade_2', 10455, 72.9, 131.9, 64, 55), tobj('rh_barricade_3', 10459, 70.4, 132.2, 32, 27)])

# ---- cổng
DOOR_V = 10346 + 6
add_to_group_id(26, [
    rect('drill_door', 'gate', 66, OY0, 1, 3, {'collider_kind': 'gate', 'starts_closed': 'true', 'requires_key': 'warden_key',
         'locked_say': "Barred. The lock is stamped with a little straw dummy - the warders' ring of keys would fit it."}),
    tobj('drill_door', DOOR_V, 65.97, 140.28, 96, 128, {'anim': 'm4_op_door_v', 'anim_fps': '8'}).replace('<object ', '<object type="gate_visual" ', 1),
    rect('tally_gate', 'gate', 24, OY0, 1, 3, {'collider_kind': 'gate', 'starts_closed': 'true', 'unlock_when': 'tally_done'}),
    tobj('tally_gate', DOOR_V, 23.97, 140.28, 96, 128, {'anim': 'm4_op_door_v', 'anim_fps': '8'}).replace('<object ', '<object type="gate_visual" ', 1),
])
# guard_door cần chìa của trung sĩ
b = blk(902)
put(b, b.replace('value="warden_key"', 'value="drill_key"').replace(
    'Locked. The warders kept their key close - somewhere they could reach it.',
    "Locked. A board is nailed across it: WARDERS ONLY. DRILLED MEN ONLY. SEE THE SERGEANT."))
b = blk(916)
put(b, b.replace("The warders' room - Cell Rows", "The drill-yard door - Rolling Hall").replace(
    'A heavy iron key on a ring, hung beside the chains in the question room.',
    'A heavy ring of iron keys, hung beside the chains in the question room. The biggest one is stamped with a little straw dummy.'))

# ---- khu
Z = lambda zid, title, x, y, w, h: rect(zid, 'zone', x, y, w, h, {'ambient': '0.27', 'ambient_color': '#b8b2c8', 'stage': 'cells', 'title': title,
                                                                    'unity:IsTrigger': True, 'zone_id': zid})
add_to_group('zones', [Z('n1', 'The Penance Run', 44, 133, 22, 11), Z('n2', 'The Tally Room', 26, 132, 15, 12), Z('n3', 'The Drill Yard', 2, 121, 21, 23)])

# ---- cơ quan
STOP = {'stop_when': 'drill_passed'}
mech = []
# n1 bẫy
mech.append(rect('pr_spikes', 'spikes', 58, 133, 5, 11, dict(damage='10', mechanic='spikes', mode='cycle', off_seconds='1.3', warn_seconds='0.45', on_seconds='0.7',
                                                         offset='0', style='1', wave_axis='y', wave_step='0.1', **STOP, **TRIG)))
for i, (x, off) in enumerate(((50.5, '0'), (53.0, '0.33'), (55.5, '0.66'))):
    mech.append(rect('pr_saw_%d' % i, 'blade', x, 133.3, 1, 10.4, dict(damage='12', kind='saw', knock='3', mechanic='blade', offset=off, pause='0.25', speed='3.2', **STOP, **TRIG)))
for i, (x, y, d, off) in enumerate(((45, 132, 'down', '0'), (46.5, 132, 'down', '0.55'), (48, 132, 'down', '1.1'),
                                    (45.75, 144, 'up', '0.3'), (47.25, 144, 'up', '0.85'))):
    mech.append(rect('pr_arrow_%d' % i, 'launcher', x, y, 1, 1, dict(damage='8', dir=d, kind='arrow', mechanic='launcher', mode='cycle', offset=off,
                                                                    period='1.7', range='12', warn_seconds='0.5', **STOP, **TRIG)))
mech += [light('pr_lamp_0', 63.5, 133.6), light('pr_lamp_1', 56.8, 142.6), light('pr_lamp_2', 49.0, 133.6), light('pr_lamp_3', 44.6, 142.6)]
# n2 câu đố
POSTS = (('sack', 28.5, 1), ('straw', 31.5, 2), ('pell', 34.5, 3), ('bust', 37.5, 4))
for k, x, note in POSTS:
    mech.append(rect('tally_post_%d' % note, 'drill_post', x - 0.5, 135.3, 1, 0.7, dict(kind=k, note=note, group='tally', answer='2,1,4,3', sets='tally_done',
        mechanic='drill_post', range='0.6', text="The four posts snap to attention. Something heavy slides back on the west door. From beyond it: 'FINALLY. Get IN here, recruit!'",
        **TRIG)))
LEDGER = ("DRILL ORDER OF THE DAY\\n- by order of Sgt. Burlap, Warders' Drill\\n\\n"
          "Recruits will DRILL the four posts in the CORRECT order, or be drilled BY them.\\n\\n"
          "1. The Pell is NEVER first. The Pell is LAST, as all pells deserve.\\n"
          "2. The Sack follows the Straw. ALWAYS. They are friends. Do not separate them.\\n"
          "3. The Bust of the Sergeant is drilled THIRD, and RESPECTFULLY.\\n\\n"
          "Failure = one (1) wallop. Repeat failures = wallops, plural.\\n\\n"
          "Under it, in a smaller hand: the sergeant is a stuffed dummy. please stop saluting it. - Warder Hobb")
mech.append(rect('tally_ledger', 'note', 38.6, 140.5, 1.1, 1.0, dict(marker=True, mechanic='note', range='1', text=LEDGER, title='Drill Order of the Day', verb='Read', **TRIG)))
mech += [light('tl_lamp_0', 27.0, 133.0, '#ffb070', 2.6, 0.75), light('tl_lamp_1', 39.5, 133.0, '#ffb070', 2.6, 0.75), light('tl_lamp_2', 33.0, 142.6, '#7fa8ff')]
# n3 minigame
mech.append(rect('drill_yard', 'drill_yard', 2, 121, 21, 23, dict(target=3000, sets='drill_passed', king_at='12.5,123.6', key_at='12.5,122.2', key_id='drill_key',
                                                                  mechanic='drill_yard', **TRIG)))
mech.append(rect('drill_horn', 'drill_horn', 20.6, 140.7, 0.8, 0.5, dict(yard='drill_yard', mechanic='drill_horn', range='0.7', **TRIG)))
mech += [light('dy_lamp_%d' % i, x, y, '#ffcf8a' if i < 2 else '#7fa8ff', 3.2, 0.8) for i, (x, y) in enumerate(((5.0, 121.8), (20.0, 121.8), (3.2, 142.6), (21.8, 142.6)))]
add_to_group('mechanics', mech)

# ---- hình trong Tiled (mechanics_art): sổ tập, bệ chìa, xem trước người nộm / tù và / chìa / trung sĩ
G = lambda i: FIRST + i
art = []
w, h = SIZE[1]; art.append(tobj('tally_ledger_art', G(1), 39.15 - w / 64, 141.5, w, h))
w, h = SIZE[2]; art.append(tobj('drill_keyped_art', G(2), 12.5 - w / 64, 122.2, w, h))
w, h = SIZE[3]; art.append(tobj('drill_keyring_art', G(3), 12.5 - w / 64, 122.2, w, h, {'art_for': 'drill_yard'}))
w, h = SIZE[8]; art.append(tobj('drill_king_art', G(8), 12.5 - w / 64, 123.6, w, h, {'art_for': 'drill_yard'}))
w, h = SIZE[0]; art.append(tobj('drill_horn_art', G(0), 21.0 - w / 64, 141.2, w, h, {'art_for': 'drill_horn'}))
for (k, x, note), tid in zip(POSTS, (4, 5, 6, 7)):
    w, h = SIZE[tid]; art.append(tobj('tally_post_%d_art' % note, G(tid), x - w / 64, 136.02, w, h, {'art_for': 'tally_post_%d' % note}))
add_to_group('mechanics_art', art)

# ---- điểm nghỉ + chìa của trung sĩ
add_to_group('triggers', [
    rect('rest_drill', 'checkpoint', 26.6, 140.2, 2.2, 2.0, {'marker': True, 'stage': 'cells', 'stage_name': 'The Tally Room', 'unity:IsTrigger': True, 'zone_id': 'n2'}),
    rect('drill_key', 'pickup', 12.0, 121.5, 1.0, 0.9, {'item': 'key', 'key_id': 'drill_key', 'key_name': "Sergeant Burlap's Key", 'key_icon': 'key_pump',
         'key_desc': "Brass, still warm from a stuffed fist. The tag reads: WARDERS' ROOM - FOR DRILLED MEN ONLY.",
         'key_opens': "The warders' room - Question Room", 'requires': 'drill_passed',
         'locked_text': "Sergeant Burlap looms over it. 'EARN IT, RECRUIT. Sound the horn.'", 'hint_text': "The sergeant's key.",
         'marker': True, 'unity:IsTrigger': True, 'zone_id': 'n3'}),
])

# ---- đồ bày (ít, sát tường — sân giữa để trống cho minigame): thùng, hòm, xô, xương; va chạm ở chân
DECO = [  # (gid, x trái, đáy, w px, h px, va chạm (w, h) hoặc None)
    (10450, 2.1, 122.5, 32, 62, (0.6, 0.3)), (10460, 3.05, 122.5, 32, 62, (0.6, 0.3)), (10452, 21.0, 122.5, 32, 62, (0.6, 0.3)),
    (10458, 2.2, 144.0, 32, 27, (0.7, 0.3)), (10459, 3.2, 144.0, 32, 27, (0.7, 0.3)), (10434, 21.9, 144.0, 32, 27, (0.5, 0.25)),
    (10459, 26.1, 133.4, 32, 27, (0.7, 0.3)), (10461, 40.0, 133.6, 32, 62, (0.6, 0.3)), (10434, 26.2, 144.0, 32, 27, (0.5, 0.25)),
    (10487, 57.2, 143.7, 32, 23, None), (10487, 49.6, 133.9, 32, 23, None), (10509, 63.8, 144.0, 32, 27, (0.5, 0.25)),
]
add_to_group('props', [tobj(None, g, x, b, w, h) for g, x, b, w, h, c in DECO])
add_to_group('obstacle colliders', [rect('prop collider', 'obstacle', x + w / 64 - c[0] / 2, b - c[1] - 0.05, c[0], c[1], {'collider_kind': 'prop'})
                                    for g, x, b, w, h, c in DECO if c])

s = re.sub(r'nextobjectid="\d+"', 'nextobjectid="%d"' % nid, s, count=1)
open(OUT, 'wb').write(s.encode('utf-8'))
print('xong ->', OUT, 'md5', md5(OUT), 'nextobjectid', nid)
