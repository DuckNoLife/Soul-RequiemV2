# 19/11 (§333): Map 3b King's Barrow — BẢN GẬP "VÒNG QUANH SÂN BOSS" (người dùng: map thẳng tắp 34x129 làm minimap xấu -> gập gọn).
# 79x70 ô. Sân The Red Field ở giữa, lăng Borrow ngay sau sân (phía bắc); đường đi vòng ngược chiều kim đồng hồ quanh sân:
#   R0 Antechamber (tây nam) -[khung cửa đá lấp, Earthshatter]-> R1 The Drowned Muster -[cửa gỗ, dọn x3]->
#   T1 The Spear Walk (cột tây, gai sàn) -[khung cửa]-> T2 The Saw Gallery (dải bắc: lưỡi cưa, sàn sập, tên bắn từ tường bắc)
#   -[lối thông tường đông]-> T3 The Marching Hall (đông bắc, hàng lính hồn) -[LÍNH CANH MA chắn lối, tan khi x7_held]->
#   T4 The Arrow Walk (cột đông, đi XUỐNG: tên bắn hai vách + sàn sập) -[khung cửa]-> The Low Road (dải nam, đi về tây)
#   -[lối thông tường tây]-> R3 The Last Muster (điểm nghỉ ẩn) -[cổng sắt khoá đấu trường]-> R4 The Red Field -[cửa lăng]-> R5 Borrow's Rest.
# Quy tắc: cửa / khung cửa chỉ trên mặt tường gạch phía bắc của phòng phía nam; lối thông ngang chỉ giữa hai phòng CÙNG hàng viền trên
# (chung một cột tường), phòng thấp hơn mở hết chiều cao sàn sang phòng kia. Khối chặn tính theo lưới ô (tường - ô đã đục) rồi gộp hình chữ nhật.
# python3 gen_3d.py <tmx gốc 6a72b321> <tmx ra> <sizes3.json>
import sys, re, json, random, xml.sax.saxutils as sx
SRC, DST, SIZES = sys.argv[1], sys.argv[2], sys.argv[3]
raw = open(SRC, 'rb').read(); CRLF = b'\r\n' in raw
src = raw.decode('utf-8').replace('\r\n', '\n')
W0, H0 = 18, 15
W, H = 79, 70
OX, OY = 1, 55                            # R0 dời tới đây
rnd = random.Random(1119)
T = 32
JL, JR = 3091, 3089                       # góc lồi: viền đáy đi từ trái rẽ xuống / từ phải rẽ xuống

# ------------------------------------------------------------------ lớp ô
LAYER_RE = r'<layer id="(\d+)" name="([^"]+)" width="18" height="15"([^>]*)>(.*?)</layer>'
layers = {}
for m in re.finditer(LAYER_RE, src, re.S):
    data = re.search(r'<data encoding="csv">(.*?)</data>', m.group(4), re.S).group(1)
    vals = [int(v) for v in data.replace('\n', '').split(',') if v.strip()]
    assert len(vals) == W0 * H0, m.group(2)
    layers[m.group(2)] = vals
grid = {k: [0] * (W * H) for k in layers}
def G_(k, x, y): return grid[k][y * W + x]
def S_(k, x, y, v): grid[k][y * W + x] = v

ROOMS = {  # tên: (x0, y0, w, h) — y0 = hàng viền trên
    'GAL': (1, 0, 54, 8), 'HALL': (54, 0, 24, 14), 'SPEAR': (3, 10, 6, 26), 'R1': (1, 39, 24, 13), 'R0': (OX, OY, 18, 15),
    'TOMB': (35, 10, 16, 13), 'ARENA': (27, 26, 32, 24), 'R3': (37, 50, 12, 9), 'LOW': (48, 50, 20, 6), 'ARROW': (64, 17, 8, 26)}
# nối dọc: (phòng nam, phòng bắc, gx) — khe 2 cột [gx, gx+1]
VCONN = [('R0', 'R1', 15), ('R1', 'SPEAR', 5), ('SPEAR', 'GAL', 5), ('ARROW', 'HALL', 67), ('LOW', 'ARROW', 65), ('R3', 'ARENA', 42), ('ARENA', 'TOMB', 42)]
# nối ngang: (phòng tây, phòng đông, cột chung)
SCONN = [('GAL', 'HALL', 54), ('R3', 'LOW', 48)]
for a, b, col in SCONN:
    assert ROOMS[a][1] == ROOMS[b][1] and ROOMS[a][0] + ROOMS[a][2] - 1 == col == ROOMS[b][0], (a, b)

SLICE = ['ground', 'rocky', 'stone', 'walls']
def sx_(dx, w): return dx if dx < 2 else (W0 - (w - dx)) if dx >= w - 2 else rnd.randrange(2, W0 - 2)
def sy_(dy, h): return dy if dy < 4 else (H0 - (h - dy)) if dy >= h - 2 else rnd.randrange(4, H0 - 2)
ORDER = ['HALL', 'GAL', 'SPEAR', 'R1', 'TOMB', 'ARENA', 'R3', 'LOW', 'ARROW']   # phòng cao vẽ trước, phòng thấp chung cột vẽ sau
for name in ORDER:
    x0, y0, w, h = ROOMS[name]
    cols = [sx_(dx, w) for dx in range(w)]
    for dy in range(h):
        sy = sy_(dy, h)
        for dx in range(w):
            sxv = cols[dx] if dy < 4 or dy >= h - 2 else (dx if dx < 2 else (W0 - (w - dx)) if dx >= w - 2 else rnd.randrange(2, W0 - 2))
            for k in SLICE:
                grid[k][(y0 + dy) * W + x0 + dx] = layers[k][sy * W0 + sxv]
for k, v in layers.items():                # R0 nguyên bản
    for y in range(H0):
        for x in range(W0): grid[k][(y + OY) * W + x + OX] = v[y * W0 + x]

# ---- lưới chặn / đi được
INSIDE = [[False] * W for _ in range(H)]   # thuộc khối lăng (phòng / hành lang)
WALK = [[False] * W for _ in range(H)]
for name, (x0, y0, w, h) in ROOMS.items():
    for y in range(y0, y0 + h):
        for x in range(x0, x0 + w):
            INSIDE[y][x] = True
            if x0 < x < x0 + w - 1 and y0 + 3 <= y <= y0 + h - 2: WALK[y][x] = True
def floor_cell(x, y):
    sxv, syv = rnd.randrange(2, W0 - 2), rnd.randrange(4, H0 - 2)
    for k in ('ground', 'rocky', 'stone'): S_(k, x, y, layers[k][syv * W0 + sxv])
    for k in grid:
        if k not in ('ground', 'rocky', 'stone'): S_(k, x, y, 0)
LEFT_SIDE = [3124, 3283, 3315]
RIGHT_SIDE = [3120, 3298, 3282]
corridors = []
for south, north, gx in VCONN:
    xs, ys, ws, hs = ROOMS[south]; xn, yn, wn, hn = ROOMS[north]
    assert xs < gx and gx + 1 < xs + ws - 1 and xn < gx and gx + 1 < xn + wn - 1, (south, north)
    rb = yn + hn - 1; c0, c1 = rb + 1, ys - 1
    corridors.append((south, north, gx, c0, c1, rb, ys))
    for x in (gx, gx + 1): floor_cell(x, rb); WALK[rb][x] = True
    S_('walls', gx - 1, rb, JL); S_('walls', gx + 2, rb, JR)
    for i, y in enumerate(range(c0, c1 + 1)):
        for x in (gx - 1, gx, gx + 1, gx + 2): floor_cell(x, y); INSIDE[y][x] = True
        for x in (gx, gx + 1): WALK[y][x] = True
        S_('walls', gx - 1, y, LEFT_SIDE[i % 3]); S_('walls', gx + 2, y, RIGHT_SIDE[i % 3])
    for y in (ys, ys + 1, ys + 2):
        for x in (gx, gx + 1): floor_cell(x, y); WALK[y][x] = True
for a, b, col in SCONN:
    xa, ya, wa, ha = ROOMS[a]; xb, yb, wb, hb = ROOMS[b]
    hs = min(ha, hb); short_is_west = ha < hb
    tall = ROOMS[b] if short_is_west else ROOMS[a]
    tx = tall[0] + 3                                      # cột giữa của phòng cao để chép mặt tường
    for y in (ya, ya + 1, ya + 2):
        for k in SLICE: S_(k, col, y, G_(k, tx, y))
    for y in range(ya + 3, ya + hs - 1):
        floor_cell(col, y); WALK[y][col] = True
    S_('walls', col, ya + hs - 1, JL if short_is_west else JR)
# rêu trên mặt tường R0 chỗ khe cửa
for y in (OY + 2, OY + 3):
    for x in (15, 16): S_('grass1', x, y, 0)

def csv(vals):
    rows = [','.join(str(v) for v in vals[y * W:(y + 1) * W]) for y in range(H)]
    return '\n' + ',\n'.join(rows) + '\n'
def repl_layer(m):
    k = m.group(2)
    body = re.sub(r'<data encoding="csv">.*?</data>', '<data encoding="csv">' + csv(grid[k]) + '</data>', m.group(4), flags=re.S)
    return '<layer id="%s" name="%s" width="%d" height="%d"%s>%s</layer>' % (m.group(1), k, W, H, m.group(3), body)
src = re.sub(LAYER_RE, repl_layer, src, flags=re.S)
src = src.replace('width="18" height="15" tilewidth', 'width="%d" height="%d" tilewidth' % (W, H), 1)
# camera: phòng rời giữa nền đen -> luôn đi theo Ren (không kẹp theo khung map)
assert '<property name="flag_group" value="vale_of_graves"/>' in src and 'camera_clamp' not in src
src = src.replace('  <property name="flag_group" value="vale_of_graves"/>\n', '  <property name="camera_clamp" value="none"/>\n  <property name="flag_group" value="vale_of_graves"/>\n', 1)

# ------------------------------------------------------------------ tileset SR-barrow
SZ = json.load(open(SIZES))
assert max(int(g) for g in re.findall(r'<tileset firstgid="(\d+)"', src)) == 4987
BAR = 4988
src = src.replace(' <tileset firstgid="4987" source="../Tilesets/SR-relic-crimson.tsx"/>\n',
                  ' <tileset firstgid="4987" source="../Tilesets/SR-relic-crimson.tsx"/>\n <tileset firstgid="%d" source="../Tilesets/SR-barrow.tsx"/>\n' % BAR, 1)
def gid(n): return BAR + SZ[n][0]
def size(n): return SZ[n][1], SZ[n][2]

# ------------------------------------------------------------------ dời object cũ của R0, bỏ thứ không dùng
def shift_obj(m):
    t = m.group(0)
    t = re.sub(r' x="([-\d.]+)"', lambda k: ' x="%g"' % round(float(k.group(1)) + OX * T, 3), t, count=1)
    t = re.sub(r' y="([-\d.]+)"', lambda k: ' y="%g"' % round(float(k.group(1)) + OY * T, 3), t, count=1)
    return t
src = re.sub(r'<object id="\d+"[^>]*?>', shift_obj, src)
def drop_object(oid):
    global src
    m = re.search(r'\n\s*<object id="%d"[^>]*/>' % oid, src) or re.search(r'\n\s*<object id="%d"[^>]*[^/]>.*?</object>' % oid, src, re.S)
    assert m, oid
    src = src[:m.start()] + src[m.end():]
for oid in (5252, 5253, 5211, 3721, 5208,
            5145, 5147, 5148, 5149, 5150, 5151, 5152, 5153, 5159, 5160, 5161, 5162,
            263):                                   # 263: phiến đá trơn cạnh quan tài -> thay bằng nắp quan tài khắc chữ
    drop_object(oid)
def set_attr(oid, attr, value):
    global src
    m = re.search(r'<object id="%d"[^>]*?>' % oid, src); blk = m.group(0)
    new = re.sub(r' %s="[^"]*"' % attr, ' %s="%s"' % (attr, value), blk, count=1); assert new != blk, (oid, attr)
    src = src.replace(blk, new)
set_attr(3795, 'x', '%g' % ((OX + 15.95) * T)); set_attr(4799, 'x', '%g' % ((OX + 16.31) * T))
def set_prop(oid, name, value):
    global src
    m = re.search(r'<object id="%d"[^>]*>.*?</object>' % oid, src, re.S); blk = m.group(0)
    new = re.sub(r'(<property name="%s"(?: type="[^"]*")? value=")[^"]*(")' % name, lambda k: k.group(1) + sx.escape(value, {'"': '&quot;'}) + k.group(2), blk, count=1)
    assert new != blk or value in blk, (oid, name)
    src = src.replace(blk, new)
def del_prop(oid, name):
    global src
    m = re.search(r'<object id="%d"[^>]*>.*?</object>' % oid, src, re.S); blk = m.group(0)
    new = re.sub(r'\n\s*<property name="%s"[^>]*/>' % name, '', blk, count=1); assert new != blk, (oid, name)
    src = src.replace(blk, new)
set_prop(4513, 'arrive_at', '127.6,78.9')
set_prop(4513, 'depart_text', 'You climb the rope, then the broken stair, back up into the marsh.')
del_prop(4513, 'requires'); del_prop(4513, 'locked_text')
del_prop(4426, 'fog')                               # khu x2: sương thường (trước đây fog = off -> minimap lộ hết)

# ------------------------------------------------------------------ đối tượng mới
nid = [int(re.search(r'nextobjectid="(\d+)"', src).group(1))]
def new_id():
    nid[0] += 1; return nid[0] - 1
def P(k, v):
    if isinstance(v, bool): return '    <property name="%s" type="bool" value="%s"/>' % (k, 'true' if v else 'false')
    if isinstance(v, int): return '    <property name="%s" type="int" value="%d"/>' % (k, v)
    if isinstance(v, float): return '    <property name="%s" type="float" value="%s"/>' % (k, repr(v))
    v = str(v).replace('\n', '\\n')
    return '    <property name="%s" value="%s"/>' % (k, sx.escape(v, {'"': '&quot;'}))
def obj(name=None, typ=None, x=0, y=0, w=None, h=None, g=None, props=None, shape=None, trig=False):
    a = '  <object id="%d"' % new_id()
    if name: a += ' name="%s"' % name
    if typ: a += ' type="%s"' % typ
    if g: a += ' gid="%d"' % g
    a += ' x="%g" y="%g"' % (round(x, 2), round(y, 2))
    if w is not None: a += ' width="%g" height="%g"' % (round(w, 2), round(h, 2))
    ps = list(props or [])
    if trig: ps.append(('unity:IsTrigger', True))
    body = []
    if ps: body = ['   <properties>'] + [P(k, v) for k, v in sorted(ps, key=lambda kv: kv[0])] + ['   </properties>']
    if shape == 'point': body.append('   <point/>')
    if shape == 'ellipse': body.append('   <ellipse/>')
    if not body: return [a + '/>']
    return [a + '>'] + body + ['  </object>']
def rect(name, typ, tx, ty, tw, th, props=None, trig=True, shape=None):
    return obj(name, typ, tx * T, ty * T, tw * T, th * T, props=props, trig=trig, shape=shape)
def tile(n, cx, by, name=None, props=None, typ=None, trig=False):
    g = gid(n); w, h = size(n)
    return obj(name, typ, cx * T - w / 2, by * T, w, h, g=g, props=props, trig=trig)
G = {k: [] for k in ['ground decals', 'props', 'zones', 'spawns', 'triggers', 'gates', 'obstacle colliders', 'blockers', 'mechanics']}
def light(name, tx, ty, color, radius, inten=None, anim=None, amount=None, speed=None, off_when=None, on_when=None):
    p = [('color', color), ('light', 'torch'), ('mechanic', 'light'), ('radius', float(radius))]
    if inten is not None: p.append(('intensity', float(inten)))
    if anim: p.append(('anim', anim))
    if amount is not None: p.append(('anim_amount', float(amount)))
    if speed is not None: p.append(('anim_speed', float(speed)))
    if off_when: p.append(('off_when', off_when))
    if on_when: p.append(('on_when', on_when))
    G['mechanics'].extend(obj(name, 'light', tx * T, ty * T, T, T, props=p, trig=True))
def obstacle(tx, ty, tw, th, ell=False):
    G['obstacle colliders'].extend(obj('prop collider', 'obstacle', tx * T, ty * T, tw * T, th * T, props=[('collider_kind', 'prop')], shape='ellipse' if ell else None))
BONES = [4692, 4693, 4694, 4695, 4696, 4697, 4699, 4701, 4703, 4704, 4705]
def bones(x0, y0, x1, y1, n, avoid=()):
    for _ in range(n):
        tx = rnd.uniform(x0, x1); ty = rnd.uniform(y0, y1)
        if any(ax0 <= tx <= ax1 and ay0 <= ty <= ay1 for ax0, ay0, ax1, ay1 in avoid): continue
        G['ground decals'].extend(obj(None, None, tx * T, ty * T, 32, 32, g=rnd.choice(BONES)))
def zone(zid, room, title, ambient, color, fight=None, fog='', extra=()):
    x0, y0, w, h = ROOMS[room]
    p = [('zone_id', zid), ('title', title), ('optional', True), ('ambient', ambient), ('ambient_color', color), ('hint_text', '')]
    if fog: p.append(('fog', fog))
    if fight is not None: p.append(('ambient_fight', fight))
    G['zones'].extend(rect(zid, 'zone', x0 + 1, y0 + 3, w - 2, h - 4, props=p + list(extra)))
def door_gate(name, gx, south, art, anim, props, fps=12.0):
    ys = ROOMS[south][1]
    G['gates'].extend(rect(name, 'gate', gx, ys, 2, 3, props=[('collider_kind', 'gate')] + props, trig=False))
    G['gates'].extend(tile(art, gx + 1.0, ys + 3 + 1.0 / 32, name=name, typ='gate_visual', trig=True, props=[('anim', anim), ('anim_fps', fps)]))
def arch(gx, south, name=None):
    G['props'].extend(tile('door_arch', gx + 1.0, ROOMS[south][1] + 3 + 1.0 / 32, name=name))

# ---- khối chặn: ô thuộc khối lăng mà không đi được, gộp hình chữ nhật (theo hàng rồi nối dọc)
runs = {}
for y in range(H):
    x = 0
    while x < W:
        if INSIDE[y][x] and not WALK[y][x]:
            s0 = x
            while x < W and INSIDE[y][x] and not WALK[y][x]: x += 1
            runs.setdefault((s0, x), []).append(y)
        else: x += 1
for (a, b), ys in sorted(runs.items()):
    ys.sort(); start = prev = ys[0]
    for y in ys[1:] + [None]:
        if y is not None and y == prev + 1: prev = y; continue
        G['blockers'].extend(obj('blocker', 'blocker', a * T, start * T, (b - a) * T, (prev - start + 1) * T, props=[('collider_kind', 'terrain')]))
        if y is not None: start = prev = y

# ---- đuốc tường bắc (tránh khe cửa / lối thông)
north_gap = {s: gx for s, n, gx in VCONN}
for name, (x0, y0, w, h) in ROOMS.items():
    if name in ('R0', 'SPEAR', 'ARROW'): continue
    gx = north_gap.get(name, -99)
    for tx in range(x0 + 4, x0 + w - 3, 7):
        if abs(tx + 0.5 - (gx + 1)) < 3: continue
        if name == 'GAL' and 31 <= tx <= 45: continue          # mặt tường có máy bắn tên
        G['props'].extend(obj(None, None, tx * T, (y0 + 2.04) * T, 32, 27, g=4978))
        light('%s_torch_%d' % (name.lower(), tx), tx, y0 + 2.1, '#ff9a4a', 3.2, anim='flicker', amount=0.25)

# ================================================================== R0 Antechamber
x0, y0, w, h = ROOMS['R0']; gx = 15
G['props'].extend(tile('door_arch', gx + 1.0, y0 + 3 + 1.0 / 32, name='x2_arch'))
G['mechanics'].extend(rect('x2_rubble', 'breakable', gx - 0.15, y0 + 2.35, 2.3, 0.85, props=[
    ('mechanic', 'breakable'), ('hits', 1), ('min_radius', 0.9), ('sets', 'x2_rubble_broken'),
    ('sprite', 'VFX/props/barrow_rubble_0'), ('sprite_broken', 'VFX/props/barrow_rubble_broken'),
    ('sprite_count', '1'), ('sprite_width', 0.76),
    ('hint', 'A doorway, choked with fallen stone. Cold air breathes between the blocks.'),
    ('weak_hint', 'The stones do not even shift. It would take a blow that shakes the ground itself.'),
    ('text', 'The fallen stone bursts apart. Beyond the doorway, a passage runs north into the dark.')]))
G['gates'].extend(rect('x2_rubble', 'gate', gx, y0, 2, 3, props=[('collider_kind', 'gate'), ('starts_closed', True), ('unlock_when', 'x2_rubble_broken')], trig=False))
light('x2_draught', gx + 1.0, y0 + 2.6, '#9ab0e8', 2.2, inten=0.55, anim='pulse', amount=0.15, speed=0.4, off_when='x2_rubble_broken')
# nắp quan tài khắc chữ (thay phiến đá trơn 263) + chỗ đọc đặt lên nắp
G['ground decals'].extend(tile('coffin_lid', OX + 10.9, OY + 7.3, name='x2_lid'))
G['triggers'].extend(rect('x2_coffin', 'sign', OX + 10.1, OY + 5.9, 1.6, 1.3, props=[
    ('zone_id', 'x2'), ('interact', True), ('verb', 'Read'),
    ('hint_text', 'The lid has been dragged aside. Under a carved crown: "BORROW, WHO MADE MIRAVEL WHOLE." The coffin beside it is empty. The dust inside has never been disturbed.')]))
# lư hương King's Censer (vật nhặt `kings_relic` có sẵn nhưng chưa từng có hình)
G['props'].extend(tile('kings_censer', OX + 13.0, OY + 9.55, name='kings_censer'))
G['mechanics'].extend(rect('kings_censer_toggle', 'toggle', OX + 1.5, OY + 12.5, 1, 1, props=[
    ('mechanic', 'toggle'), ('when', 'f_upgrade_burn'), ('hide_objects', 'kings_censer')]))
light('kings_censer_glow', OX + 13.0, OY + 8.9, '#ffd27a', 1.5, inten=0.6, anim='pulse', amount=0.2, speed=0.5, off_when='f_upgrade_burn')

# ================================================================== R1 The Drowned Muster
x0, y0, w, h = ROOMS['R1']                  # sàn cột 2..23, hàng 42..50
zone('x3', 'R1', 'The Drowned Muster', 0.18, '#7f8fb8', 0.15)
door_gate('x3_door', 5, 'R1', 'door_wood', 'barrow_door_wood', [('starts_closed', True), ('zone_id', 'x3'),
          ('locked_say', 'The door is barred from this side. Not while the dead still walk the hall.')])
for tx, ty, en, wave in [(6, 45.5, 'Undead', 1), (13, 47.5, 'Undead', 1), (20, 44.5, 'Undead', 1),
                         (16, 46.5, 'Grave_Worm', 2), (22, 48.5, 'Grave_Worm', 2), (8, 48.0, 'Undead', 2)]:
    G['spawns'].extend(obj(en, 'spawn', tx * T, ty * T, props=[('enemy', en), ('wave', wave), ('zone_id', 'x3')], shape='point'))
bones(x0 + 2.5, y0 + 4.2, x0 + w - 3, y0 + h - 1.5, 22, avoid=[(14, 48, 18, 52)])
for tx, gg in ((15.5, 4715), (22.0, 4716)):
    G['props'].extend(obj(None, None, tx * T - 32, (y0 + 4.6) * T, 64, 96, g=gg)); obstacle(tx - 0.45, y0 + 4.2, 0.9, 0.4)
for tx in (3.5, 9.5, 12.5, 19.0):
    G['props'].extend(obj(None, None, tx * T - 16, (y0 + 3.75) * T, 32, 22, g=rnd.choice([4979, 4980, 4983]))); obstacle(tx - 0.16, y0 + 3.55, 0.32, 0.2, ell=True)
G['mechanics'].extend(rect('x3_order', 'note', 4.0, 47.0, 1.0, 0.8, props=[
    ('mechanic', 'note'), ('sprite', 'VFX/props/note_paper'), ('pickup', True), ('sets', 'x3_order_read'), ('verb', 'Read'),
    ('key_id', 'x3_order'), ('key_name', 'Standing Order of Miravel'), ('title', 'Standing Order of the Crown of Miravel'),
    ('text', "By the King's oath, sworn on the blade: no quarter, no prisoners, no retreat.\n\nAny man who turns from the field will be drowned in the marsh beside the ones who turned before him.\n\nThe King is with you. The King is always with you.")]))
G['mechanics'].extend(rect('x3_letter', 'note', 21.5, 49.0, 1.0, 0.8, props=[
    ('mechanic', 'note'), ('sprite', 'VFX/props/note_paper'), ('pickup', True), ('sets', 'x3_letter_read'), ('verb', 'Read'),
    ('key_id', 'x3_letter'), ('key_name', 'An Unsent Letter'), ('title', 'An Unsent Letter'),
    ('text', "Mira -\n\nThey say there are six hundred thousand of us on the field, both armies counted. From the ridge I cannot see the end of it.\n\nThe Miravel men do not sing any more. Their king carries a red sword now, and they say it is thirsty.\n\nIf we lose tomorrow, burn this, so they never learn your name.\n\n- T.")]))
light('x3_glow_order', 4.3, 47.2, '#ffd27a', 1.6, inten=0.6, off_when='x3_order_read')
light('x3_glow_letter', 21.8, 49.2, '#ffd27a', 1.6, inten=0.6, off_when='x3_letter_read')

# ================================================================== T1 The Spear Walk (cột tây, đi lên)
x0, y0, w, h = ROOMS['SPEAR']               # sàn cột 4..7, hàng 13..34
zone('x6', 'SPEAR', 'The Spear Walk', 0.16, '#8a96c0')
arch(5, 'SPEAR', name='x6_arch')
def spikes(name, tx, ty, tw, th, offset, extra=()):
    p = [('mechanic', 'spikes'), ('mode', 'cycle'), ('off_seconds', 1.1), ('warn_seconds', 0.5), ('on_seconds', 1.3),
         ('offset', float(offset)), ('wave_axis', 'y'), ('wave_step', 0.08), ('damage', 15.0), ('style', 2)] + list(extra)
    G['mechanics'].extend(rect(name, 'spikes', tx, ty, tw, th, props=p))
spikes('x6_band1', 4, 30, 4, 3, 0.0)
spikes('x6_band2', 4, 25, 4, 3, 0.55)
spikes('x6_band3a', 4, 20, 2, 3, 1.10)
spikes('x6_band3b', 6, 20, 2, 3, 1.10 + 1.45)
spikes('x6_band4', 4, 15, 4, 3, 1.65)
for i, ty in enumerate((33.5, 28.5, 23.5, 18.5, 13.5)):
    light('x6_lamp_%d' % i, 6.0, ty, '#9ab0e8', 2.4, inten=0.7, anim='flicker', amount=0.12)
G['triggers'].extend(rect('x6_enter', 'story', 4, 33.0, 4, 1.5, props=[('trigger', 'enter'),
    ('script', 'wait 0.2\nbubble ren: Spear-butts set in the floor. The whole corridor is a drill yard - and the drill never stopped.')]))

# ================================================================== T2 The Saw Gallery (dải bắc, đi sang đông)
x0, y0, w, h = ROOMS['GAL']                 # sàn cột 2..53, hàng 3..6
zone('x9', 'GAL', 'The Saw Gallery', 0.16, '#8a96c0')
def saw(name, tx, offset, speed=3.0):
    G['mechanics'].extend(rect(name, 'blade', tx, 3, 1, 4, props=[('mechanic', 'blade'), ('kind', 'saw'), ('speed', float(speed)), ('pause', 0.3),
                                                                 ('offset', float(offset)), ('damage', 14.0), ('knock', 3.0)]))
for i, (tx, off) in enumerate(((10, 0.0), (14, 0.33), (18, 0.66))): saw('x9_saw_a%d' % i, tx, off)
G['mechanics'].extend(rect('x9_pit', 'pit', 22, 3, 8, 4, props=[('mechanic', 'pit'), ('size', 2), ('damage', 14.0), ('warn_seconds', 0.45),
    ('open_seconds', 2.5), ('return_at', '20.5,5'), ('hint', 'The flagstones ahead ring hollow underfoot.'),
    ('text', 'The floor gives way. You haul yourself back out of the pit.')]))
for i, tx in enumerate((33, 36, 39, 42)):
    G['mechanics'].extend(rect('x9_arrow_%d' % i, 'launcher', tx, 1, 1, 1, props=[('mechanic', 'launcher'), ('kind', 'arrow'), ('dir', 'down'),
        ('mode', 'cycle'), ('period', 1.8), ('offset', round(i * 0.45, 2)), ('warn_seconds', 0.6), ('speed', 8.0), ('range', 6.0), ('damage', 12.0)]))
for i, (tx, off) in enumerate(((46, 0.2), (49, 0.7), (52, 0.45))): saw('x9_saw_b%d' % i, tx, off, 4.0)
for tx in (8, 21, 31, 44):
    light('x9_lamp_%d' % tx, tx, 5.0, '#9ab0e8', 2.6, inten=0.7, anim='flicker', amount=0.12)
G['triggers'].extend(rect('x9_enter', 'story', 4, 3, 4, 2, props=[('trigger', 'enter'),
    ('script', 'wait 0.2\nbubble ren: Blades in the floor, holes in the wall. Someone built this to keep the living out.')]))
bones(3, 3.2, 53, 6.6, 18)

# ================================================================== T3 The Marching Hall (đông bắc)
x0, y0, w, h = ROOMS['HALL']                # sàn cột 55..76, hàng 3..12
zone('x7', 'HALL', 'The Marching Hall', 0.17, '#a08a9a', 0.15)
G['mechanics'].extend(rect('x7_ranks', 'ranks', x0 + 1, y0 + 3, w - 2, h - 4, props=[
    ('mechanic', 'ranks'), ('count', 8), ('interval', 2.3), ('warn', 1.0), ('speed', 2.0), ('gap_half', 0.42), ('damage', 18.0),
    ('double_from', 5), ('double_lag', 1.2), ('sets', 'x7_held'),
    ('hint', 'The dead are mustering. Hold the hall until the last rank has passed - find the gap in each line, or roll through it.'),
    ('text', 'The last rank passes into the wall. The sentry on the stair lowers its spear.'),
    ('reset_text', 'You leave the hall. Behind you the ranks fall back into the dark - they will march again.')]))
bones(x0 + 2, y0 + 4, x0 + w - 2, y0 + h - 1.5, 20)
for tx in (56.0, 75.0):
    for by in (6.6, 11.6):
        light('x7_ember_%d_%d' % (int(tx), int(by)), tx, by - 0.6, '#ff5a4a', 2.6, inten=0.5, anim='flicker', amount=0.2)
# lối ra: khe viền đáy -> hành lang xuống Arrow Walk; LÍNH CANH MA đứng chắn tới khi trụ đủ 8 hàng
G['gates'].extend(rect('x7_ward', 'gate', 67, 14, 2, 2, props=[('collider_kind', 'gate'), ('starts_closed', True), ('unlock_when', 'x7_held'),
    ('locked_say', 'A dead sentry bars the stair, spear across the way. It is waiting for the march to end.')], trig=False))
G['gates'].extend(tile('ghost_ward', 68.0, 16.2, name='x7_ward', typ='gate_visual', trig=True, props=[('anim', 'barrow_ghost_ward'), ('anim_fps', 9.0)]))
light('x7_ward_glow', 68.0, 14.5, '#ff6a6a', 1.8, inten=0.55, anim='pulse', amount=0.2, speed=0.6, off_when='x7_held')

# ================================================================== T4 The Arrow Walk (cột đông, đi XUỐNG)
x0, y0, w, h = ROOMS['ARROW']               # sàn cột 65..70, hàng 20..41
zone('x10', 'ARROW', 'The Arrow Walk', 0.15, '#8a96c0')
arch(67, 'ARROW', name='x10_arch')
for i, ty in enumerate((23, 27, 31, 35, 39)):
    G['mechanics'].extend(rect('x10_w%d' % i, 'launcher', 64, ty, 1, 1, props=[('mechanic', 'launcher'), ('kind', 'arrow'), ('dir', 'right'),
        ('mode', 'cycle'), ('period', 1.7), ('offset', round(i * 0.34, 2)), ('warn_seconds', 0.6), ('speed', 8.0), ('range', 7.0), ('damage', 12.0)]))
for i, ty in enumerate((25, 29, 33, 37)):
    G['mechanics'].extend(rect('x10_e%d' % i, 'launcher', 71, ty, 1, 1, props=[('mechanic', 'launcher'), ('kind', 'arrow'), ('dir', 'left'),
        ('mode', 'cycle'), ('period', 1.7), ('offset', round(0.85 + i * 0.34, 2)), ('warn_seconds', 0.6), ('speed', 8.0), ('range', 7.0), ('damage', 12.0)]))
for i, (px, py, pw, ret) in enumerate(((65, 26, 2, '66,24.6'), (69, 30, 2, '69.5,28.6'), (66, 34, 4, '68,32.6'), (65, 38, 2, '66,36.6'))):
    G['mechanics'].extend(rect('x10_pit_%d' % i, 'pit', px, py, pw, 2, props=[('mechanic', 'pit'), ('size', 2), ('damage', 14.0), ('warn_seconds', 0.45),
        ('open_seconds', 2.5), ('return_at', ret), ('hint', 'Loose flagstones. Keep moving.'),
        ('text', 'The floor gives way. You climb back out of the pit.')]))
for i, ty in enumerate((21.5, 28.5, 35.5, 40.5)):
    light('x10_lamp_%d' % i, 67.5, ty, '#9ab0e8', 2.4, inten=0.65, anim='flicker', amount=0.12)
G['triggers'].extend(rect('x10_enter', 'story', 66, 20, 4, 1.5, props=[('trigger', 'enter'),
    ('script', 'wait 0.2\nbubble ren: Murder holes in both walls. Down is the only way on.')]))

# ================================================================== The Low Road (dải nam, đi về tây)
x0, y0, w, h = ROOMS['LOW']                 # sàn cột 49..66, hàng 53..54
zone('x11', 'LOW', 'The Low Road', 0.18, '#8a96c0')
arch(65, 'LOW', name='x11_arch')
bones(49.5, 53.1, 64, 54.7, 10)
for tx in (52, 59):
    light('x11_lamp_%d' % tx, tx, 53.6, '#9ab0e8', 2.2, inten=0.6, anim='flicker', amount=0.12)

# ================================================================== R3 The Last Muster (điểm nghỉ ẩn)
x0, y0, w, h = ROOMS['R3']                  # sàn cột 38..47, hàng 53..57
zone('x8', 'R3', 'The Last Muster', 0.2, '#8a96c0')
G['triggers'].extend(rect('rest_barrow', 'checkpoint', 38.6, 54.6, 2.4, 2.0, props=[
    ('marker', False), ('stage', 'barrow_field'), ('stage_name', 'The Last Muster'), ('zone_id', 'x8')]))
door_gate('x4_lock', 42, 'R3', 'door_gate', 'barrow_door_gate', [('starts_closed', False), ('close_on_enter', True), ('zone_id', 'x4'),
          ('unlock_when', 'x4_cleared')], fps=14.0)
G['triggers'].extend(rect('x8_enter', 'story', 45.5, 53.0, 2.0, 2.0, props=[('trigger', 'enter'),
    ('script', 'wait 0.3\nbubble ren: Quiet. Whatever gave the ranks their orders is past that gate.')]))
light('x8_glow', 39.8, 55.4, '#ffd27a', 3.0, inten=0.7, anim='flicker', amount=0.15)
for tx in (38.6, 47.0):
    G['props'].extend(obj(None, None, tx * T - 32, 57.6 * T, 64, 60, g=4981)); obstacle(tx - 0.2, 57.3, 0.4, 0.3, ell=True)

# ================================================================== R4 The Red Field (boss)
x0, y0, w, h = ROOMS['ARENA']
zone('x4', 'ARENA', 'The Red Field', 0.16, '#c07a7a', 0.14, extra=[('stage', 'barrow_field')])
G['spawns'].extend(obj('Red_Field', 'spawn', 43 * T, 39.5 * T, props=[('enemy', 'Red_Field'), ('wave', 1), ('zone_id', 'x4'), ('boss', 'true')], shape='point'))
G['triggers'].extend(rect('red_field_fight', 'boss', x0 + 1, y0 + 4.5, w - 2, h - 6.5, props=[
    ('zone_id', 'x4'), ('entrance', 'rise'), ('card_color', '#7a2a2a'), ('card_sub', 'Six hundred thousand, and not one of them buried')]))
door_gate('x4_tomb', 42, 'ARENA', 'door_tomb', 'barrow_door_tomb', [('starts_closed', True), ('unlock_when', 'x4_cleared'), ('zone_id', 'x4'),
          ('locked_say', 'The great door will not move while the dead still hold the field.')], fps=10.0)
for (tx, by) in ((30.5, 34), (55.5, 34), (30.5, 46), (55.5, 46), (36, 47.6), (50, 47.6), (29.5, 40), (56.5, 40)):
    G['props'].extend(tile('spears_planted', tx, by)); obstacle(tx - 0.5, by - 0.55, 1.0, 0.5, ell=True)
for i in range(30):
    tx = rnd.uniform(x0 + 2.5, x0 + w - 3); ty = rnd.uniform(y0 + 4.5, y0 + h - 1.5)
    if abs(tx - 43) < 4 and abs(ty - 38) < 3.5: continue
    G['ground decals'].extend(obj(None, None, tx * T, ty * T, 32, 32, g=rnd.choice(BONES[:9])))
light('x4_core', 43, 37.5, '#ff3a3a', 5.5, inten=0.9, anim='pulse', amount=0.3, speed=0.5, off_when='x4_cleared')
for (tx, ty) in ((31, 35), (55, 35), (31, 45), (55, 45)):
    light('x4_ember_%d_%d' % (tx, ty), tx, ty, '#ff5a4a', 3.0, inten=0.6, anim='flicker', amount=0.2)

# ================================================================== R5 Borrow's Rest
x0, y0, w, h = ROOMS['TOMB']                # sàn cột 36..49, hàng 13..21
zone('x5', 'TOMB', "Borrow's Rest", 0.22, '#c09a80', fog='secret')
G['props'].extend(tile('borrow_throne', 43.0, 16.5, name='borrow_throne'))
G['props'].extend(tile('borrow_throne_empty', 43.0, 16.5, name='borrow_throne_empty'))
obstacle(41.8, 15.4, 2.4, 1.1)
for tx in (39.0, 47.0):
    G['props'].extend(obj(None, None, tx * T - 32, 16.2 * T, 64, 60, g=4981)); obstacle(tx - 0.2, 15.9, 0.4, 0.3, ell=True)
    light('x5_candle_%d' % int(tx), tx, 14.6, '#ffb45a', 2.5, anim='flicker', amount=0.2)
G['mechanics'].extend(rect('barrow_oath', 'note', 42.1, 16.5, 1.8, 0.9, props=[
    ('mechanic', 'note'), ('pickup', True), ('range', 1.2), ('sets', 'oath_taken'), ('verb', 'Take the blade'),
    ('weapon_id', 'katana_crimson'),
    ('key_got', 'Crimson Oath. It drinks what it cuts, and it answers hardest when there is little left of you.'),
    ('title', 'The Purified King'),
    ('text', "He drank the most, and he drowned the first.\n\nA boy from a poor village in the east swore this blade to a broken kingdom, and made it whole. Then the field of six hundred thousand woke something, and the blade drank the rest of the war for him.\n\nThe Ascended's light burned it out of him. He walked into these marshes and did not walk out. No one ever found the body; no one was meant to. His hands are still folded over the hilt.")]))
G['mechanics'].extend(rect('x5_throne_toggle', 'toggle', 36.5, 19.5, 1, 1, props=[
    ('mechanic', 'toggle'), ('when', 'oath_taken'), ('hide_objects', 'borrow_throne'), ('show_objects', 'borrow_throne_empty')]))
light('barrow_oath_glow', 43.0, 15.6, '#ff6a5a', 2.2, inten=0.75, anim='pulse', amount=0.22, speed=0.4, off_when='oath_taken')
G['triggers'].extend(rect('x5_enter', 'story', 41.5, 20.0, 3.0, 1.5, props=[('trigger', 'enter'),
    ('script', 'wait 0.3\ncam 43,15.4 1\nwait 1.4\nbubble ren: So this is where he went. Not the coffin upstairs - a chair, and his own blade across his knees.\nwait 0.4\ncamren')]))
G['triggers'].extend(rect('x5_taken', 'story', 48.0, 20.0, 1, 1, props=[('trigger', 'flag'), ('when', 'oath_taken'),
    ('script', 'wait 0.4\nshake 0.02 0.9\nnarrate The fingers do not want to let go. One by one, you open them.|Far above, in the marsh, six hundred thousand voices go quiet.')]))

# ---- biên map
mb = []
for (bx, by, bw, bh) in ((-1, -1, W + 2, 1), (-1, H, W + 2, 1), (-1, 0, 1, H), (W, 0, 1, H)):
    mb += obj('map_bound', 'bound', bx * T, by * T, bw * T, bh * T, props=[('collider_kind', 'bound')])
src = re.sub(r'(<objectgroup id="\d+" name="map bounds"[^>]*>)(.*?)(\n </objectgroup>)', lambda m: m.group(1) + '\n' + '\n'.join(mb) + m.group(3), src, count=1, flags=re.S)
assert '<objectgroup id="28" name="gates">\n  <properties>\n   <property name="unity:SortingOrder" type="int" value="2"/>' in src
for gname, lines in G.items():
    if not lines: continue
    m = re.search(r'<objectgroup id="\d+" name="%s"[^>]*>' % re.escape(gname), src)
    assert m, gname
    end = src.index('\n </objectgroup>', m.end())
    src = src[:end] + '\n' + '\n'.join(lines) + src[end:]
src = re.sub(r'nextobjectid="\d+"', 'nextobjectid="%d"' % nid[0], src, count=1)
assert 'type="portal"' not in src and 'name="fog" value="off"' not in src
out = src.replace('\n', '\r\n') if CRLF else src
open(DST, 'wb').write(out.encode('utf-8'))
print('ok', W, H, 'objects up to', nid[0])
