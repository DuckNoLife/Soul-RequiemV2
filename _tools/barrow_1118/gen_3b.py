# 18/11 (§330): Map 3b King's Barrow mở rộng — khu bí mật Crimson Oath (boss bí mật The Red Field, lăng Borrow).
# Giữ nguyên phòng cũ R0 (0,0,18,15); thêm 3 phòng xa nhau trong cùng map, nối bằng portal (bậc đá / cửa trên mặt tường bắc):
#   R0 Antechamber --(phiến đá nứt, Earthshatter phá -> bậc xuống)--> R1 The Drowned Muster --(cửa gỗ đông)--> R2 The Red Field (boss)
#   --(cửa lớn, mở khi hạ boss)--> R3 Borrow's Rest (ngai, Crimson Oath). Phòng mới dựng bằng 9-slice các lớp nền / tường của R0.
# python3 gen_3b.py <tmx vào> <tmx ra> <sizes.json>
import sys, re, json, random, xml.sax.saxutils as sx
SRC, DST, SIZES = sys.argv[1], sys.argv[2], sys.argv[3]
raw = open(SRC, 'rb').read(); CRLF = b'\r\n' in raw
src = raw.decode('utf-8').replace('\r\n', '\n')
W0, H0 = 18, 15
W, H = 64, 42
rnd = random.Random(1118)
T = 32

# ------------------------------------------------------------------ lớp ô
layers = {}
for m in re.finditer(r'<layer id="(\d+)" name="([^"]+)" width="18" height="15"([^>]*)>(.*?)</layer>', src, re.S):
    data = re.search(r'<data encoding="csv">(.*?)</data>', m.group(4), re.S).group(1)
    vals = [int(v) for v in data.replace('\n', '').split(',') if v.strip()]
    assert len(vals) == W0 * H0, m.group(2)
    layers[m.group(2)] = vals
grid = {k: [0] * (W * H) for k in layers}
for k, v in layers.items():
    for y in range(H0):
        for x in range(W0): grid[k][y * W + x] = v[y * W0 + x]

SLICE = ['ground', 'rocky', 'stone', 'walls']
ROOMS = {'R1': (22, 0, 40, 12), 'R2': (22, 16, 32, 24), 'R3': (2, 20, 17, 15)}
def sx_(dx, w): return dx if dx < 2 else (W0 - (w - dx)) if dx >= w - 2 else rnd.randrange(2, W0 - 2)
def sy_(dy, h): return dy if dy < 4 else (H0 - (h - dy)) if dy >= h - 2 else rnd.randrange(4, H0 - 2)
for name, (x0, y0, w, h) in ROOMS.items():
    cols = [sx_(dx, w) for dx in range(w)]
    for dy in range(h):
        sy = sy_(dy, h)
        for dx in range(w):
            sxv = cols[dx] if dy < 4 or dy >= h - 2 else (dx if dx < 2 else (W0 - (w - dx)) if dx >= w - 2 else rnd.randrange(2, W0 - 2))
            for k in SLICE:
                grid[k][(y0 + dy) * W + x0 + dx] = layers[k][sy * W0 + sxv]

def csv(vals):
    rows = [','.join(str(v) for v in vals[y * W:(y + 1) * W]) for y in range(H)]
    return '\n' + ',\n'.join(rows) + '\n'
def repl_layer(m):
    k = m.group(2)
    body = re.sub(r'<data encoding="csv">.*?</data>', '<data encoding="csv">' + csv(grid[k]) + '</data>', m.group(4), flags=re.S)
    return '<layer id="%s" name="%s" width="%d" height="%d"%s>%s</layer>' % (m.group(1), k, W, H, m.group(3), body)
src = re.sub(r'<layer id="(\d+)" name="([^"]+)" width="18" height="15"([^>]*)>(.*?)</layer>', repl_layer, src, flags=re.S)
src = src.replace('width="18" height="15" tilewidth', 'width="%d" height="%d" tilewidth' % (W, H), 1)

# ------------------------------------------------------------------ tileset mới
SZ = json.load(open(SIZES))
first = max(int(g) for g in re.findall(r'<tileset firstgid="(\d+)"', src))
assert first == 4987
BAR = 4988                                   # SR-relic-crimson có 1 ô (4987) -> SR-barrow bắt đầu 4988
src = src.replace(' <tileset firstgid="4987" source="../Tilesets/SR-relic-crimson.tsx"/>\n',
                  ' <tileset firstgid="4987" source="../Tilesets/SR-relic-crimson.tsx"/>\n <tileset firstgid="%d" source="../Tilesets/SR-barrow.tsx"/>\n' % BAR, 1)
def gid(n): return BAR + SZ[n][0]
def size(n): return SZ[n][1], SZ[n][2]

# ------------------------------------------------------------------ đối tượng
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
def tile(n_or_gid, cx, by, name=None, props=None, w=None, h=None):
    """tile object: tâm ngang cx, chân by (ô)."""
    if isinstance(n_or_gid, str): g = gid(n_or_gid); w, h = size(n_or_gid)
    else: g = n_or_gid
    return obj(name, None, cx * T - w / 2, by * T, w, h, g=g, props=props)

G = {k: [] for k in ['ground decals', 'props', 'zones', 'spawns', 'triggers', 'obstacle colliders', 'blockers', 'map bounds', 'mechanics']}
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

# ---- tường mỗi phòng (khối chặn như R0: 3 hàng trên, cột trái / phải, hàng đáy)
for name, (x0, y0, w, h) in ROOMS.items():
    for (bx, by, bw, bh) in ((x0, y0, w, 3), (x0, y0 + 3, 1, h - 4), (x0 + w - 1, y0 + 3, 1, h - 4), (x0, y0 + h - 1, w, 1)):
        G['blockers'].extend(obj('blocker', 'blocker', bx * T, by * T, bw * T, bh * T, props=[('collider_kind', 'terrain')]))
    # đuốc trên mặt tường bắc (như R0: chân ở hàng 2.04)
    for tx in range(x0 + 4, x0 + w - 3, 8):
        G['props'].extend(obj(None, None, tx * T, (y0 + 2.04) * T, 32, 27, g=4978))
        light('%s_torch_%d' % (name.lower(), tx), tx, y0 + 2.1, '#ff9a4a', 3.2, anim='flicker', amount=0.25)

def door(n, x_left, by, name):
    """cửa vẽ vào mặt tường bắc: khung cửa 4 ô [x_left, x_left+4), tâm giữa, chân = mép trên sàn (+1 px như mặt nạ)."""
    G['props'].extend(tile(n, x_left + 2.0, by + 1.0 / 32, name=name))
def portal(name, tx, ty, tw, th, target, requires=None, locked=None, hint=''):
    p = [('target', target)]
    if requires: p.append(('requires', requires))
    if locked: p.append(('locked_text', locked))
    if hint: p.append(('hint_text', hint))
    G['triggers'].extend(rect(name, 'portal', tx, ty, tw, th, props=p))

# ================================================================== R0 Antechamber (phòng cũ) — thêm manh mối + phiến đá
G['ground decals'].extend(tile('barrow_slab', 5.5, 13.0, name='barrow_slab_art'))
G['ground decals'].extend(tile('stair_hole', 5.5, 13.0, name='barrow_stair_art'))
G['mechanics'].extend(rect('x2_slab', 'breakable', 4.3, 10.4, 2.4, 2.4, props=[
    ('mechanic', 'breakable'), ('hits', 1), ('min_radius', 0.9), ('sets', 'x2_slab_broken'),
    ('hint', 'A flagstone carved with a crown. It rings hollow underfoot.'),
    ('weak_hint', 'The slab does not even crack. It would take a blow that shakes the ground itself.'),
    ('text', 'The slab shatters and falls through. Stone steps lead down into the dark.')]))
G['mechanics'].extend(rect('x2_slab_toggle', 'toggle', 4.5, 10.5, 1, 1, props=[
    ('mechanic', 'toggle'), ('when', 'x2_slab_broken'), ('hide_objects', 'barrow_slab_art'), ('show_objects', 'barrow_stair_art')]))
portal('x2_down', 4.7, 10.9, 1.6, 1.3, '26,4.8', requires='x2_slab_broken',
       locked='It rings hollow. Something lies beneath the slab.', hint='The steps go down a long way. The air turns wet and cold.')
G['triggers'].extend(rect('x2_coffin', 'sign', 8.0, 6.3, 2.0, 0.9, props=[
    ('zone_id', 'x2'), ('interact', True), ('verb', 'Read'),
    ('hint_text', 'The lid reads: "BORROW, WHO MADE MIRAVEL WHOLE." The coffin is empty. The dust inside has never been disturbed.')]))

# ================================================================== R1 The Drowned Muster
x0, y0, w, h = ROOMS['R1']
door('door_wood', 24, 3.0, 'x3_door_west'); door('door_wood', 56, 3.0, 'x3_door_east')
portal('x3_up', 24.6, 3.0, 2.8, 0.9, '5.5,9.4', hint='You climb back up into the antechamber.')
portal('x3_east', 56.6, 3.0, 2.8, 0.9, '27,21', hint='Past the door the floor turns soft and red, as though the marsh had been ploughed with spears.')
G['zones'].extend(rect('x3', 'zone', x0 + 1, y0 + 3, w - 2, h - 4, props=[
    ('zone_id', 'x3'), ('title', 'The Drowned Muster'), ('optional', True), ('fog', 'off'),
    ('ambient', 0.18), ('ambient_color', '#7f8fb8'), ('ambient_fight', 0.15), ('hint_text', '')]))
for i, (tx, ty, en, wave) in enumerate([(34, 6.5, 'Undead', 1), (42, 8.5, 'Undead', 1), (50, 5.5, 'Undead', 1),
                                        (46, 7.5, 'Grave_Worm', 2), (54, 8.5, 'Grave_Worm', 2), (38, 9.0, 'Undead', 2)]):
    G['spawns'].extend(obj(en, 'spawn', tx * T, ty * T, props=[('enemy', en), ('wave', wave), ('zone_id', 'x3')], shape='point'))
# xác hai đạo quân: xương rải dọc sảnh (decal), tượng + bình sát tường bắc
for i in range(26):
    tx = rnd.uniform(x0 + 2.5, x0 + w - 3); ty = rnd.uniform(y0 + 4.2, y0 + h - 1.5)
    G['ground decals'].extend(obj(None, None, tx * T, ty * T, 32, 32, g=rnd.choice([4692, 4693, 4694, 4695, 4696, 4697, 4699, 4701, 4703, 4704, 4705])))
for tx, gg in ((32, 4715), (40, 4716), (48, 4715)):
    G['props'].extend(obj(None, None, tx * T - 32, (y0 + 4.6) * T, 64, 96, g=gg)); obstacle(tx - 0.45, y0 + 4.2, 0.9, 0.4)
for tx in (29.5, 37.5, 45.5, 53.5):
    G['props'].extend(obj(None, None, tx * T - 16, (y0 + 3.75) * T, 32, 22, g=rnd.choice([4979, 4980, 4983]))); obstacle(tx - 0.16, y0 + 3.55, 0.32, 0.2, ell=True)
G['mechanics'].extend(rect('x3_order', 'note', 31.0, 7.0, 1.0, 0.8, props=[
    ('mechanic', 'note'), ('sprite', 'VFX/props/note_paper'), ('pickup', True), ('sets', 'x3_order_read'), ('verb', 'Read'),
    ('key_id', 'x3_order'), ('key_name', 'Standing Order of Miravel'),
    ('title', 'Standing Order of the Crown of Miravel'),
    ('text', "By the King's oath, sworn on the blade: no quarter, no prisoners, no retreat.\n\nAny man who turns from the field will be drowned in the marsh beside the ones who turned before him.\n\nThe King is with you. The King is always with you.")]))
G['mechanics'].extend(rect('x3_letter', 'note', 47.0, 9.0, 1.0, 0.8, props=[
    ('mechanic', 'note'), ('sprite', 'VFX/props/note_paper'), ('pickup', True), ('sets', 'x3_letter_read'), ('verb', 'Read'),
    ('key_id', 'x3_letter'), ('key_name', 'An Unsent Letter'),
    ('title', 'An Unsent Letter'),
    ('text', "Mira -\n\nThey say there are six hundred thousand of us on the field, both armies counted. From the ridge I cannot see the end of it.\n\nThe Miravel men do not sing any more. Their king carries a red sword now, and they say it is thirsty.\n\nIf we lose tomorrow, burn this, so they never learn your name.\n\n- T.")]))
light('x3_glow_order', 31.3, 7.2, '#ffd27a', 1.6, inten=0.6, off_when='x3_order_read')
light('x3_glow_letter', 47.3, 9.2, '#ffd27a', 1.6, inten=0.6, off_when='x3_letter_read')

# ================================================================== R2 The Red Field (boss bí mật)
x0, y0, w, h = ROOMS['R2']
door('door_gate', 25, 19.0, 'x4_gate'); door('door_tomb', 36, 19.0, 'x4_tomb_door')
portal('x4_back', 25.6, 19.0, 2.8, 0.9, '58,4.8', requires='x4_cleared', locked='The gate has dropped shut behind you.')
portal('x4_tomb', 36.6, 19.0, 2.8, 0.9, '6,24.8', requires='x4_cleared',
       locked='The great door will not move while the dead still hold the field.', hint='Beyond the door, a narrow stair, and a room that smells of old incense.')
G['zones'].extend(rect('x4', 'zone', x0 + 1, y0 + 3, w - 2, h - 4, props=[
    ('zone_id', 'x4'), ('title', 'The Red Field'), ('optional', True), ('fog', 'off'),
    ('ambient', 0.16), ('ambient_color', '#c07a7a'), ('ambient_fight', 0.14), ('hint_text', '')]))
G['spawns'].extend(obj('Red_Field', 'spawn', 38 * T, 29.5 * T, props=[('enemy', 'Red_Field'), ('wave', 1), ('zone_id', 'x4'), ('boss', 'true')], shape='point'))
G['triggers'].extend(rect('red_field_fight', 'boss', x0 + 1, y0 + 4.5, w - 2, h - 5.5, props=[
    ('zone_id', 'x4'), ('entrance', 'rise'), ('card_color', '#7a2a2a'), ('card_sub', 'Six hundred thousand, and not one of them buried')]))
for (tx, by) in ((25.5, 24), (50.5, 24), (25.5, 36), (50.5, 36), (31, 37.6), (45, 37.6), (24.5, 30), (51.5, 30)):
    G['props'].extend(tile('spears_planted', tx, by)); obstacle(tx - 0.5, by - 0.55, 1.0, 0.5, ell=True)
for i in range(30):
    tx = rnd.uniform(x0 + 2.5, x0 + w - 3); ty = rnd.uniform(y0 + 4.5, y0 + h - 1.5)
    if abs(tx - 38) < 4 and abs(ty - 28) < 3.5: continue
    G['ground decals'].extend(obj(None, None, tx * T, ty * T, 32, 32, g=rnd.choice([4692, 4693, 4694, 4695, 4696, 4697, 4699, 4701, 4703])))
light('x4_core', 38, 27.5, '#ff3a3a', 5.5, inten=0.9, anim='pulse', amount=0.3, speed=0.5, off_when='x4_cleared')
for (tx, ty) in ((26, 25), (50, 25), (26, 35), (50, 35)):
    light('x4_ember_%d_%d' % (tx, ty), tx, ty, '#ff5a4a', 3.0, inten=0.6, anim='flicker', amount=0.2)

# ================================================================== R3 Borrow's Rest
x0, y0, w, h = ROOMS['R3']
door('door_tomb', 4, 23.0, 'x5_door')
portal('x5_back', 4.6, 23.0, 2.8, 0.9, '38,20.8')
G['zones'].extend(rect('x5', 'zone', x0 + 1, y0 + 3, w - 2, h - 4, props=[
    ('zone_id', 'x5'), ('title', "Borrow's Rest"), ('optional', True), ('fog', 'off'),
    ('ambient', 0.22), ('ambient_color', '#c09a80'), ('hint_text', '')]))
G['props'].extend(tile('borrow_throne', 11.5, 26.7, name='borrow_throne'))
G['props'].extend(tile('borrow_throne_empty', 11.5, 26.7, name='borrow_throne_empty'))
obstacle(10.3, 25.6, 2.4, 1.1)
for tx in (8.0, 15.0):
    G['props'].extend(obj(None, None, tx * T - 32, 26.4 * T, 64, 60, g=4981)); obstacle(tx - 0.2, 26.1, 0.4, 0.3, ell=True)
    light('x5_candle_%d' % int(tx), tx, 24.8, '#ffb45a', 2.5, anim='flicker', amount=0.2)
G['mechanics'].extend(rect('barrow_oath', 'note', 10.6, 26.7, 1.8, 0.9, props=[
    ('mechanic', 'note'), ('pickup', True), ('range', 1.2), ('sets', 'oath_taken'), ('verb', 'Take the blade'),
    ('weapon_id', 'katana_crimson'),
    ('key_got', 'Crimson Oath. It drinks what it cuts, and it answers hardest when there is little left of you.'),
    ('title', 'The Purified King'),
    ('text', "He drank the most, and he drowned the first.\n\nA boy from a poor village in the east swore this blade to a broken kingdom, and made it whole. Then the field of six hundred thousand woke something, and the blade drank the rest of the war for him.\n\nThe Ascended's light burned it out of him. He walked into these marshes and did not walk out. No one ever found the body; no one was meant to. His hands are still folded over the hilt.")]))
G['mechanics'].extend(rect('x5_throne_toggle', 'toggle', 3.5, 31.5, 1, 1, props=[
    ('mechanic', 'toggle'), ('when', 'oath_taken'), ('hide_objects', 'borrow_throne'), ('show_objects', 'borrow_throne_empty')]))
light('barrow_oath_glow', 11.5, 25.8, '#ff6a5a', 2.2, inten=0.75, anim='pulse', amount=0.22, speed=0.4, off_when='oath_taken')
G['triggers'].extend(rect('x5_enter', 'story', 4.5, 24.0, 3.0, 1.5, props=[
    ('trigger', 'enter'),
    ('script', 'wait 0.3\ncam 11.5,25.6 1\nwait 1.4\nbubble ren: So this is where he went. Not the coffin upstairs - a chair, and his own blade across his knees.\nwait 0.4\ncamren')]))
G['triggers'].extend(rect('x5_taken', 'story', 3.5, 32.0, 1, 1, props=[
    ('trigger', 'flag'), ('when', 'oath_taken'),
    ('script', 'wait 0.4\nshake 0.02 0.9\nnarrate The fingers do not want to let go. One by one, you open them.|Far above, in the marsh, six hundred thousand voices go quiet.')]))

# ---- R0 cũ: bỏ Crimson Oath + ánh sáng cũ (đã dời vào R3)
def drop_object(oid):
    global src
    m = re.search(r'\n\s*<object id="%d"[^>]*[^/]>.*?</object>' % oid, src, re.S)
    assert m, oid
    src = src[:m.start()] + src[m.end():]
drop_object(5252); drop_object(5253)
drop_object(5211)                            # cần kéo dây: Ren xuống bằng chính sợi dây đó (lối mới qua bậc đá Sinking Fields)
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

# ---- biên map
mb = []
for (bx, by, bw, bh) in ((-1, -1, W + 2, 1), (-1, H, W + 2, 1), (-1, 0, 1, H), (W, 0, 1, H)):
    mb += obj('map_bound', 'bound', bx * T, by * T, bw * T, bh * T, props=[('collider_kind', 'bound')])
src = re.sub(r'(<objectgroup id="\d+" name="map bounds"[^>]*>).*?(\n </objectgroup>)', lambda m: m.group(1) + '\n' + '\n'.join(mb) + m.group(2), src, count=1, flags=re.S)

for gname, lines in G.items():
    if gname == 'map bounds' or not lines: continue
    m = re.search(r'<objectgroup id="\d+" name="%s"[^>]*>' % re.escape(gname), src)
    assert m, gname
    end = src.index('\n </objectgroup>', m.end())
    src = src[:end] + '\n' + '\n'.join(lines) + src[end:]
src = re.sub(r'nextobjectid="\d+"', 'nextobjectid="%d"' % nid[0], src, count=1)
out = src.replace('\n', '\r\n') if CRLF else src
open(DST, 'wb').write(out.encode('utf-8'))
print('ok', W, H, 'objects up to', nid[0])
