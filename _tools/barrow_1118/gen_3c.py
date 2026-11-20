# 18/11 (§331): Map 3b King's Barrow — BẢN NỐI LIỀN (người dùng: "mở cửa và đi sang hẳn map bên đấy thay vì teleport"; thêm màn vượt bẫy trước boss).
# Một khối lăng mộ dọc, đi từ NAM lên BẮC, mọi cửa nằm trên mặt tường gạch phía bắc của phòng, nối bằng hành lang 2 ô sàn. KHÔNG còn portal.
#   R0 Antechamber (phòng cũ, giữ nguyên tile / đồ, dời xuống đáy) --(khung cửa bị đá lấp: CHỈ Earthshatter phá)-->
#   R1 The Drowned Muster (2 đợt quái, 2 ghi chép) --(cửa gỗ, mở khi dọn xong khu x3)-->
#   T1 The Spear Walk (hành lang 4 ô, 4 dải gai sàn đâm theo sóng từ bắc xuống, dải 3 tách trái / phải lệch nhịp) --(cửa gỗ mở sẵn)-->
#   T2 The Marching Hall (cơ quan `ranks`: hàng lính hồn hành quân ngang, trụ 8 hàng -> cờ x7_held) --(cổng sắt, mở khi x7_held)-->
#   R3 The Last Muster (điểm nghỉ ẩn, ngay trước sân) --(cổng sắt khoá đấu trường: đóng khi trận bắt đầu)-->
#   R4 The Red Field (boss) --(cửa lăng, mở khi x4_cleared)--> hành lang --> R5 Borrow's Rest (ngai, Crimson Oath).
# Lối duy nhất ra ngoài: sợi dây ở R0 (leo lên Map 3).
# python3 gen_3c.py <tmx gốc 6a72b321> <tmx ra> <sizes2.json>
import sys, re, json, random, xml.sax.saxutils as sx
SRC, DST, SIZES = sys.argv[1], sys.argv[2], sys.argv[3]
raw = open(SRC, 'rb').read(); CRLF = b'\r\n' in raw
src = raw.decode('utf-8').replace('\r\n', '\n')
W0, H0 = 18, 15
W, H = 34, 129
OX, OY = 1, 114                           # R0 dời tới đây
rnd = random.Random(1118)
T = 32
JL, JR = (3091, 3089)                     # góc lồi hai bên khe ở viền đáy phòng phía bắc (thử bằng ảnh)
JL_TOP, JR_TOP = None, None               # góc mặt tường phòng phía nam hai bên khe (None = để nguyên mặt tường)
if len(sys.argv) > 4:
    v = sys.argv[4].split(',')
    JL, JR = int(v[0]), int(v[1])
    if len(v) > 2: JL_TOP, JR_TOP = (int(v[2]), int(v[3])), (int(v[4]), int(v[5]))

# ------------------------------------------------------------------ lớp ô
LAYER_RE = r'<layer id="(\d+)" name="([^"]+)" width="18" height="15"([^>]*)>(.*?)</layer>'
layers = {}
for m in re.finditer(LAYER_RE, src, re.S):
    data = re.search(r'<data encoding="csv">(.*?)</data>', m.group(4), re.S).group(1)
    vals = [int(v) for v in data.replace('\n', '').split(',') if v.strip()]
    assert len(vals) == W0 * H0, m.group(2)
    layers[m.group(2)] = vals
grid = {k: [0] * (W * H) for k in layers}
for k, v in layers.items():
    for y in range(H0):
        for x in range(W0): grid[k][(y + OY) * W + x + OX] = v[y * W0 + x]
def G_(k, x, y): return grid[k][y * W + x]
def S_(k, x, y, v): grid[k][y * W + x] = v

ROOMS = {  # tên: (x0, y0, w, h) — y0 = hàng viền trên
    'R5': (9, 0, 16, 13), 'R4': (1, 16, 32, 24), 'R3': (9, 40, 12, 9), 'T2': (4, 52, 26, 14),
    'T1': (21, 69, 6, 26), 'R1': (4, 98, 24, 13), 'R0': (OX, OY, 18, 15)}
# cửa: phòng phía nam -> phòng phía bắc, khe 2 cột [gx, gx+1]
DOORS = [('R0', 'R1', 15), ('R1', 'T1', 23), ('T1', 'T2', 23), ('T2', 'R3', 13), ('R3', 'R4', 15), ('R4', 'R5', 16)]
SLICE = ['ground', 'rocky', 'stone', 'walls']
def sx_(dx, w): return dx if dx < 2 else (W0 - (w - dx)) if dx >= w - 2 else rnd.randrange(2, W0 - 2)
def sy_(dy, h): return dy if dy < 4 else (H0 - (h - dy)) if dy >= h - 2 else rnd.randrange(4, H0 - 2)
for name, (x0, y0, w, h) in ROOMS.items():
    if name == 'R0': continue
    cols = [sx_(dx, w) for dx in range(w)]
    for dy in range(h):
        sy = sy_(dy, h)
        for dx in range(w):
            sxv = cols[dx] if dy < 4 or dy >= h - 2 else (dx if dx < 2 else (W0 - (w - dx)) if dx >= w - 2 else rnd.randrange(2, W0 - 2))
            for k in SLICE:
                grid[k][(y0 + dy) * W + x0 + dx] = layers[k][sy * W0 + sxv]
def floor_cell(x, y):
    """sàn giữa phòng (lấy ngẫu nhiên từ lòng R0)."""
    sxv, syv = rnd.randrange(2, W0 - 2), rnd.randrange(4, H0 - 2)
    for k in ('ground', 'rocky', 'stone'): S_(k, x, y, layers[k][syv * W0 + sxv])
    for k in grid:
        if k not in ('ground', 'rocky', 'stone'): S_(k, x, y, 0)
LEFT_SIDE = [3124, 3283, 3315]            # cột tường trái của hành lang (như cột tây R0)
RIGHT_SIDE = [3120, 3298, 3282]           # cột tường phải (như cột đông R0)
corridors = []
for south, north, gx in DOORS:
    xs, ys, ws, hs = ROOMS[south]; xn, yn, wn, hn = ROOMS[north]
    rb = yn + hn - 1                      # hàng viền đáy phòng bắc
    c0, c1 = rb + 1, ys - 1               # hàng hành lang (có thể rỗng)
    corridors.append((gx, c0, c1, rb, ys))
    # khe ở viền đáy phòng bắc
    for x in (gx, gx + 1): floor_cell(x, rb)
    S_('walls', gx - 1, rb, JL); S_('walls', gx + 2, rb, JR)
    # hành lang
    for i, y in enumerate(range(c0, c1 + 1)):
        for x in (gx - 1, gx, gx + 1, gx + 2): floor_cell(x, y)
        S_('walls', gx - 1, y, LEFT_SIDE[i % 3]); S_('walls', gx + 2, y, RIGHT_SIDE[i % 3])
    # mặt tường bắc phòng nam: lòng khe thành sàn (khung cửa vẽ đè lên)
    for y in (ys, ys + 1, ys + 2):
        for x in (gx, gx + 1): floor_cell(x, y)
    if JL_TOP:
        S_('walls', gx - 1, ys, JL_TOP[0]); S_('walls', gx - 1, ys + 1, JL_TOP[1])
        S_('walls', gx + 2, ys, JR_TOP[0]); S_('walls', gx + 2, ys + 1, JR_TOP[1])

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
# 19/11 (§332): camera chỉ kẹp chiều dọc — phòng rời trôi giữa nền đen, kẹp ngang theo sân boss làm phòng hẹp lệch sang một bên
assert '<property name="flag_group" value="vale_of_graves"/>' in src and 'camera_clamp' not in src
src = src.replace('  <property name="flag_group" value="vale_of_graves"/>\n', '  <property name="camera_clamp" value="y"/>\n  <property name="flag_group" value="vale_of_graves"/>\n', 1)

# ------------------------------------------------------------------ tileset SR-barrow
SZ = json.load(open(SIZES))
assert max(int(g) for g in re.findall(r'<tileset firstgid="(\d+)"', src)) == 4987
BAR = 4988
src = src.replace(' <tileset firstgid="4987" source="../Tilesets/SR-relic-crimson.tsx"/>\n',
                  ' <tileset firstgid="4987" source="../Tilesets/SR-relic-crimson.tsx"/>\n <tileset firstgid="%d" source="../Tilesets/SR-barrow.tsx"/>\n' % BAR, 1)
def gid(n): return BAR + SZ[n][0]
def size(n): return SZ[n][1], SZ[n][2]

# ------------------------------------------------------------------ dời mọi object cũ của R0 + bỏ những thứ không còn dùng
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
for oid in (5252, 5253, 5211,               # Crimson Oath + ánh sáng (dời vào lăng R5), cần kéo dây (Ren xuống bằng dây)
            3721, 5208,                     # đuốc đông tường bắc R0 (chỗ khung cửa mới)
            5145, 5147, 5148, 5149, 5150, 5151, 5152, 5153,   # khối chặn R0 (dựng lại theo khe cửa)
            5159, 5160, 5161, 5162):        # biên map cũ
    drop_object(oid)
def set_attr(oid, attr, value):
    global src
    m = re.search(r'<object id="%d"[^>]*?>' % oid, src); blk = m.group(0)
    new = re.sub(r' %s="[^"]*"' % attr, ' %s="%s"' % (attr, value), blk, count=1); assert new != blk, (oid, attr)
    src = src.replace(blk, new)
set_attr(3795, 'x', '%g' % ((OX + 15.95) * T))      # bình gốm: dời khỏi lối trước cửa, sát góc đông
set_attr(4799, 'x', '%g' % ((OX + 16.31) * T))
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
    """tile object: tâm ngang cx, chân by (ô)."""
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
def blocker(bx, by, bw, bh):
    if bw > 0 and bh > 0:
        G['blockers'].extend(obj('blocker', 'blocker', bx * T, by * T, bw * T, bh * T, props=[('collider_kind', 'terrain')]))
def bones(x0, y0, x1, y1, n, avoid=()):
    for _ in range(n):
        tx = rnd.uniform(x0, x1); ty = rnd.uniform(y0, y1)
        if any(ax0 <= tx <= ax1 and ay0 <= ty <= ay1 for ax0, ay0, ax1, ay1 in avoid): continue
        G['ground decals'].extend(obj(None, None, tx * T, ty * T, 32, 32, g=rnd.choice([4692, 4693, 4694, 4695, 4696, 4697, 4699, 4701, 4703, 4704, 4705])))
def zone(zid, room, title, ambient, color, fight=None, extra=()):
    x0, y0, w, h = ROOMS[room]
    p = [('zone_id', zid), ('title', title), ('optional', True), ('fog', 'off'), ('ambient', ambient), ('ambient_color', color), ('hint_text', '')]
    if fight is not None: p.append(('ambient_fight', fight))
    G['zones'].extend(rect(zid, 'zone', x0 + 1, y0 + 3, w - 2, h - 4, props=p + list(extra)))

# ---- khối chặn: tường mỗi phòng (chừa khe cửa) + hai vách hành lang
north_gap = {s: gx for s, n, gx in DOORS}          # phòng có cửa trên tường bắc
south_gap = {n: gx for s, n, gx in DOORS}          # phòng có khe ở viền đáy
for name, (x0, y0, w, h) in ROOMS.items():
    if name in north_gap:
        gx = north_gap[name]; blocker(x0, y0, gx - x0, 3); blocker(gx + 2, y0, x0 + w - gx - 2, 3)
    else: blocker(x0, y0, w, 3)
    blocker(x0, y0 + 3, 1, h - 4); blocker(x0 + w - 1, y0 + 3, 1, h - 4)
    if name in south_gap:
        gx = south_gap[name]; blocker(x0, y0 + h - 1, gx - x0, 1); blocker(gx + 2, y0 + h - 1, x0 + w - gx - 2, 1)
    else: blocker(x0, y0 + h - 1, w, 1)
for gx, c0, c1, rb, ys in corridors:
    if c1 >= c0: blocker(gx - 1, c0, 1, c1 - c0 + 1); blocker(gx + 2, c0, 1, c1 - c0 + 1)
# đuốc tường bắc (tránh khe cửa)
for name, (x0, y0, w, h) in ROOMS.items():
    if name == 'R0': continue
    gx = north_gap.get(name, -99)
    for tx in range(x0 + 4, x0 + w - 3, 7):
        if abs(tx + 0.5 - (gx + 1)) < 3: continue
        G['props'].extend(obj(None, None, tx * T, (y0 + 2.04) * T, 32, 27, g=4978))
        light('%s_torch_%d' % (name.lower(), tx), tx, y0 + 2.1, '#ff9a4a', 3.2, anim='flicker', amount=0.25)

def door_gate(name, gx, south, art, anim, props, fps=12.0):
    """cửa trên mặt tường bắc phòng `south`: gate chặn khe (3 hàng tường) + hình gate_visual có hoạt ảnh."""
    ys = ROOMS[south][1]
    G['gates'].extend(rect(name, 'gate', gx, ys, 2, 3, props=[('collider_kind', 'gate')] + props, trig=False))
    G['gates'].extend(tile(art, gx + 1.0, ys + 3 + 1.0 / 32, name=name, typ='gate_visual', trig=True,
                           props=[('anim', anim), ('anim_fps', fps)]))

# ================================================================== R0 Antechamber — khung cửa bị đá lấp (Earthshatter)
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
G['triggers'].extend(rect('x2_coffin', 'sign', OX + 8.0, OY + 6.3, 2.0, 0.9, props=[
    ('zone_id', 'x2'), ('interact', True), ('verb', 'Read'),
    ('hint_text', 'The lid reads: "BORROW, WHO MADE MIRAVEL WHOLE." The coffin is empty. The dust inside has never been disturbed.')]))

# ================================================================== R1 The Drowned Muster
x0, y0, w, h = ROOMS['R1']
zone('x3', 'R1', 'The Drowned Muster', 0.18, '#7f8fb8', 0.15)
door_gate('x3_door', 23, 'R1', 'door_wood', 'barrow_door_wood', [('starts_closed', True), ('zone_id', 'x3'),
          ('locked_say', 'The door is barred from this side. Not while the dead still walk the hall.')])
for tx, ty, en, wave in [(9, 104.5, 'Undead', 1), (16, 106.5, 'Undead', 1), (23, 103.5, 'Undead', 1),
                         (19, 105.5, 'Grave_Worm', 2), (25, 107.5, 'Grave_Worm', 2), (11, 107.0, 'Undead', 2)]:
    G['spawns'].extend(obj(en, 'spawn', tx * T, ty * T, props=[('enemy', en), ('wave', wave), ('zone_id', 'x3')], shape='point'))
bones(x0 + 2.5, y0 + 4.2, x0 + w - 3, y0 + h - 1.5, 22, avoid=[(14, 107, 18, 111)])
for tx, gg in ((11, 4715), (19, 4716)):          # tượng lệch khỏi đuốc tường (x 8, 15)
    G['props'].extend(obj(None, None, tx * T - 32, (y0 + 4.6) * T, 64, 96, g=gg)); obstacle(tx - 0.45, y0 + 4.2, 0.9, 0.4)
for tx in (6.5, 13.5, 21.2, 26.5):
    G['props'].extend(obj(None, None, tx * T - 16, (y0 + 3.75) * T, 32, 22, g=rnd.choice([4979, 4980, 4983]))); obstacle(tx - 0.16, y0 + 3.55, 0.32, 0.2, ell=True)
G['mechanics'].extend(rect('x3_order', 'note', 7.0, 106.0, 1.0, 0.8, props=[
    ('mechanic', 'note'), ('sprite', 'VFX/props/note_paper'), ('pickup', True), ('sets', 'x3_order_read'), ('verb', 'Read'),
    ('key_id', 'x3_order'), ('key_name', 'Standing Order of Miravel'),
    ('title', 'Standing Order of the Crown of Miravel'),
    ('text', "By the King's oath, sworn on the blade: no quarter, no prisoners, no retreat.\n\nAny man who turns from the field will be drowned in the marsh beside the ones who turned before him.\n\nThe King is with you. The King is always with you.")]))
G['mechanics'].extend(rect('x3_letter', 'note', 24.5, 108.0, 1.0, 0.8, props=[
    ('mechanic', 'note'), ('sprite', 'VFX/props/note_paper'), ('pickup', True), ('sets', 'x3_letter_read'), ('verb', 'Read'),
    ('key_id', 'x3_letter'), ('key_name', 'An Unsent Letter'),
    ('title', 'An Unsent Letter'),
    ('text', "Mira -\n\nThey say there are six hundred thousand of us on the field, both armies counted. From the ridge I cannot see the end of it.\n\nThe Miravel men do not sing any more. Their king carries a red sword now, and they say it is thirsty.\n\nIf we lose tomorrow, burn this, so they never learn your name.\n\n- T.")]))
light('x3_glow_order', 7.3, 106.2, '#ffd27a', 1.6, inten=0.6, off_when='x3_order_read')
light('x3_glow_letter', 24.8, 108.2, '#ffd27a', 1.6, inten=0.6, off_when='x3_letter_read')

# ================================================================== T1 The Spear Walk
x0, y0, w, h = ROOMS['T1']                  # sàn cột 22..25, hàng 72..93
zone('x6', 'T1', 'The Spear Walk', 0.16, '#8a96c0')
door_gate('x6_door', 23, 'T1', 'door_wood', 'barrow_door_wood', [('starts_closed', False)])
def spikes(name, tx, ty, tw, th, offset, extra=()):
    p = [('mechanic', 'spikes'), ('mode', 'cycle'), ('off_seconds', 1.1), ('warn_seconds', 0.5), ('on_seconds', 1.3),
         ('offset', float(offset)), ('wave_axis', 'y'), ('wave_step', 0.08), ('damage', 15.0), ('style', 2)] + list(extra)
    G['mechanics'].extend(rect(name, 'spikes', tx, ty, tw, th, props=p))
# dải gai từ NAM lên BẮC (người chơi đi lên); sóng chạy từ bắc xuống: dải phía bắc đâm trước
spikes('x6_band1', 22, 89, 4, 3, 0.0)
spikes('x6_band2', 22, 84, 4, 3, 0.55)
spikes('x6_band3a', 22, 79, 2, 3, 1.10)
spikes('x6_band3b', 24, 79, 2, 3, 1.10 + 1.45)   # nửa phải lệch nửa nhịp: chọn bên
spikes('x6_band4', 22, 74, 4, 3, 1.65)
for i, ty in enumerate((92.5, 87.5, 82.5, 77.5, 72.5)):
    light('x6_lamp_%d' % i, 24.0, ty, '#9ab0e8', 2.4, inten=0.7, anim='flicker', amount=0.12)
G['triggers'].extend(rect('x6_enter', 'story', 22, 92.0, 4, 1.5, props=[
    ('trigger', 'enter'),
    ('script', 'wait 0.2\nbubble ren: Spear-butts set in the floor. The whole corridor is a drill yard - and the drill never stopped.')]))

# ================================================================== T2 The Marching Hall
x0, y0, w, h = ROOMS['T2']                  # sàn cột 5..28, hàng 55..64
zone('x7', 'T2', 'The Marching Hall', 0.17, '#a08a9a', 0.15)
door_gate('x7_door', 13, 'T2', 'door_gate', 'barrow_door_gate', [('starts_closed', True), ('unlock_when', 'x7_held'),
          ('locked_say', 'The portcullis is down. The ranks have not finished their march.')], fps=10.0)
G['mechanics'].extend(rect('x7_ranks', 'ranks', x0 + 1, y0 + 3, w - 2, h - 4, props=[
    ('mechanic', 'ranks'), ('count', 8), ('interval', 2.3), ('warn', 1.0), ('speed', 2.0), ('gap_half', 0.42), ('damage', 18.0),
    ('double_from', 5), ('double_lag', 1.2), ('sets', 'x7_held'),
    ('hint', 'The dead are mustering. Hold the hall until the last rank has passed - find the gap in each line, or roll through it.'),
    ('text', 'The last rank passes into the wall. Ahead, the portcullis grinds upward.'),
    ('reset_text', 'You leave the hall. Behind you the ranks fall back into the dark - they will march again.')]))
bones(x0 + 2, y0 + 4, x0 + w - 2, y0 + h - 1.5, 26)
for tx in (6.0, 27.0):
    for by in (58.6, 63.6):
        light('x7_ember_%d_%d' % (int(tx), int(by)), tx, by - 0.6, '#ff5a4a', 2.6, inten=0.5, anim='flicker', amount=0.2)

# ================================================================== R3 The Last Muster (điểm nghỉ ẩn)
x0, y0, w, h = ROOMS['R3']                  # sàn cột 10..19, hàng 43..47
zone('x8', 'R3', 'The Last Muster', 0.2, '#8a96c0')
G['triggers'].extend(rect('rest_barrow', 'checkpoint', 11.0, 45.0, 2.4, 2.0, props=[
    ('marker', False), ('stage', 'barrow_field'), ('stage_name', 'The Last Muster'), ('zone_id', 'x8')]))
door_gate('x4_lock', 15, 'R3', 'door_gate', 'barrow_door_gate', [('starts_closed', False), ('close_on_enter', True), ('zone_id', 'x4'),
          ('unlock_when', 'x4_cleared')], fps=14.0)
G['triggers'].extend(rect('x8_enter', 'story', 14.5, 46.5, 3.0, 1.5, props=[
    ('trigger', 'enter'),
    ('script', 'wait 0.3\nbubble ren: Quiet. Whatever gave the ranks their orders is past that gate.')]))
light('x8_glow', 12.2, 45.6, '#ffd27a', 3.0, inten=0.7, anim='flicker', amount=0.15)
for tx in (10.6, 19.4):
    G['props'].extend(obj(None, None, tx * T - 32, 44.4 * T, 64, 60, g=4981)); obstacle(tx - 0.2, 44.1, 0.4, 0.3, ell=True)

# ================================================================== R4 The Red Field (boss)
x0, y0, w, h = ROOMS['R4']
zone('x4', 'R4', 'The Red Field', 0.16, '#c07a7a', 0.14, extra=[('stage', 'barrow_field')])   # gục trong sân -> hồi sinh ở The Last Muster
G['spawns'].extend(obj('Red_Field', 'spawn', 17 * T, 29.5 * T, props=[('enemy', 'Red_Field'), ('wave', 1), ('zone_id', 'x4'), ('boss', 'true')], shape='point'))
G['triggers'].extend(rect('red_field_fight', 'boss', x0 + 1, y0 + 4.5, w - 2, h - 6.5, props=[
    ('zone_id', 'x4'), ('entrance', 'rise'), ('card_color', '#7a2a2a'), ('card_sub', 'Six hundred thousand, and not one of them buried')]))
door_gate('x4_tomb', 16, 'R4', 'door_tomb', 'barrow_door_tomb', [('starts_closed', True), ('unlock_when', 'x4_cleared'), ('zone_id', 'x4'),
          ('locked_say', 'The great door will not move while the dead still hold the field.')], fps=10.0)
for (tx, by) in ((4.5, 24), (29.5, 24), (4.5, 36), (29.5, 36), (10, 37.6), (24, 37.6), (3.5, 30), (30.5, 30)):
    G['props'].extend(tile('spears_planted', tx, by)); obstacle(tx - 0.5, by - 0.55, 1.0, 0.5, ell=True)
for i in range(30):
    tx = rnd.uniform(x0 + 2.5, x0 + w - 3); ty = rnd.uniform(y0 + 4.5, y0 + h - 1.5)
    if abs(tx - 17) < 4 and abs(ty - 28) < 3.5: continue
    G['ground decals'].extend(obj(None, None, tx * T, ty * T, 32, 32, g=rnd.choice([4692, 4693, 4694, 4695, 4696, 4697, 4699, 4701, 4703])))
light('x4_core', 17, 27.5, '#ff3a3a', 5.5, inten=0.9, anim='pulse', amount=0.3, speed=0.5, off_when='x4_cleared')
for (tx, ty) in ((5, 25), (29, 25), (5, 35), (29, 35)):
    light('x4_ember_%d_%d' % (tx, ty), tx, ty, '#ff5a4a', 3.0, inten=0.6, anim='flicker', amount=0.2)

# ================================================================== R5 Borrow's Rest
x0, y0, w, h = ROOMS['R5']                  # sàn cột 10..23, hàng 3..11
zone('x5', 'R5', "Borrow's Rest", 0.22, '#c09a80')
G['props'].extend(tile('borrow_throne', 17.0, 6.5, name='borrow_throne'))
G['props'].extend(tile('borrow_throne_empty', 17.0, 6.5, name='borrow_throne_empty'))
obstacle(15.8, 5.4, 2.4, 1.1)
for tx in (13.0, 21.0):
    G['props'].extend(obj(None, None, tx * T - 32, 6.2 * T, 64, 60, g=4981)); obstacle(tx - 0.2, 5.9, 0.4, 0.3, ell=True)
    light('x5_candle_%d' % int(tx), tx, 4.6, '#ffb45a', 2.5, anim='flicker', amount=0.2)
G['mechanics'].extend(rect('barrow_oath', 'note', 16.1, 6.5, 1.8, 0.9, props=[
    ('mechanic', 'note'), ('pickup', True), ('range', 1.2), ('sets', 'oath_taken'), ('verb', 'Take the blade'),
    ('weapon_id', 'katana_crimson'),
    ('key_got', 'Crimson Oath. It drinks what it cuts, and it answers hardest when there is little left of you.'),
    ('title', 'The Purified King'),
    ('text', "He drank the most, and he drowned the first.\n\nA boy from a poor village in the east swore this blade to a broken kingdom, and made it whole. Then the field of six hundred thousand woke something, and the blade drank the rest of the war for him.\n\nThe Ascended's light burned it out of him. He walked into these marshes and did not walk out. No one ever found the body; no one was meant to. His hands are still folded over the hilt.")]))
G['mechanics'].extend(rect('x5_throne_toggle', 'toggle', 10.5, 9.5, 1, 1, props=[
    ('mechanic', 'toggle'), ('when', 'oath_taken'), ('hide_objects', 'borrow_throne'), ('show_objects', 'borrow_throne_empty')]))
light('barrow_oath_glow', 17.0, 5.6, '#ff6a5a', 2.2, inten=0.75, anim='pulse', amount=0.22, speed=0.4, off_when='oath_taken')
G['triggers'].extend(rect('x5_enter', 'story', 15.5, 10.0, 3.0, 1.5, props=[
    ('trigger', 'enter'),
    ('script', 'wait 0.3\ncam 17,5.4 1\nwait 1.4\nbubble ren: So this is where he went. Not the coffin upstairs - a chair, and his own blade across his knees.\nwait 0.4\ncamren')]))
G['triggers'].extend(rect('x5_taken', 'story', 22.0, 10.0, 1, 1, props=[
    ('trigger', 'flag'), ('when', 'oath_taken'),
    ('script', 'wait 0.4\nshake 0.02 0.9\nnarrate The fingers do not want to let go. One by one, you open them.|Far above, in the marsh, six hundred thousand voices go quiet.')]))

# ---- biên map
mb = []
for (bx, by, bw, bh) in ((-1, -1, W + 2, 1), (-1, H, W + 2, 1), (-1, 0, 1, H), (W, 0, 1, H)):
    mb += obj('map_bound', 'bound', bx * T, by * T, bw * T, bh * T, props=[('collider_kind', 'bound')])
src = re.sub(r'(<objectgroup id="\d+" name="map bounds"[^>]*>)(.*?)(\n </objectgroup>)', lambda m: m.group(1) + '\n' + '\n'.join(mb) + m.group(3), src, count=1, flags=re.S)
# lớp gates: sắp như lớp props (cửa vẽ trên mặt tường, Ren đi khuất sau khung cửa)
assert '<objectgroup id="28" name="gates">\n  <properties>\n   <property name="unity:SortingOrder" type="int" value="2"/>' in src   # lớp gates gốc đã sắp như props
for gname, lines in G.items():
    if not lines: continue
    m = re.search(r'<objectgroup id="\d+" name="%s"[^>]*>' % re.escape(gname), src)
    assert m, gname
    if src[m.end():m.end() + 2] == '/>' : raise SystemExit('nhóm rỗng tự đóng: ' + gname)
    end = src.index('\n </objectgroup>', m.end())
    src = src[:end] + '\n' + '\n'.join(lines) + src[end:]
src = re.sub(r'nextobjectid="\d+"', 'nextobjectid="%d"' % nid[0], src, count=1)
assert 'type="portal"' not in src
out = src.replace('\n', '\r\n') if CRLF else src
open(DST, 'wb').write(out.encode('utf-8'))
print('ok', W, H, 'objects up to', nid[0])
