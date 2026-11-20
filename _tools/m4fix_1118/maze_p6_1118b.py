# 18/11 (người dùng): Blade Yard (p6) -> MÊ CUNG BẪY. Lối vào ở tường đông (y 92-96) -> tiền sảnh -> 7 làn dọc rộng 2 ô ngăn bằng hố gai
# (đi rắn bò lên xuống) -> bãi trên (tời cầu ở đầu tây, đi về phía đông tới cầu x 54-56). Không có đường vòng: hố gai = vách + blocker.
# Hố gai vẽ bằng bộ wang GÓC "spikes" của Tileset-Terrain-old prison (firstgid 3621) trên lớp op_spk — cùng ô với hố gai cũ.
# Kiểm md5, sao lưu _backups/2026-11-18_m4_maze/.
import hashlib, re, os, shutil, sys, random
ROOT = os.path.expanduser(os.environ.get('M4ROOT', '~/mnt/Soul Requiem'))
TMX = ROOT + "/Assets/GamePackage/EPIC RPG World Pack - Crypt V.1.6/TiledMap Editor/Maps/map4_catacombs_of_naming.tmx"
MD5 = sys.argv[1]
b = open(TMX, 'rb').read(); assert hashlib.md5(b).hexdigest() == MD5, hashlib.md5(b).hexdigest()
os.makedirs(ROOT + "/_backups/2026-11-18_m4_maze", exist_ok=True)
shutil.copy2(TMX, ROOT + "/_backups/2026-11-18_m4_maze/map4_catacombs_of_naming.tmx")
s = b.decode('utf-8'); crlf = '\r\n' in s
if crlf: s = s.replace('\r\n', '\n')
MW, MH, T = 114, 124, 32
random.seed(1118)
# ------------------------------------------------------------------ hố gai (đỉnh lưới, toạ độ ô)
# (vx0, vx1, vy0, vy1) bao gồm; hố hiện từ vx0-0.5 tới vx1+0.5
PITS = [
    (37, 64, 86, 87),                       # dải trên: ngăn bãi trên với mê cung (hở phía tây cho làn 7)
    (61, 62, 86, 100), (53, 54, 86, 100), (45, 46, 86, 100), (37, 38, 86, 100),   # cột "hở đáy" (nối dải trên)
    (57, 58, 90, 102), (49, 50, 90, 102), (41, 42, 90, 102),                      # cột "hở đỉnh"
]
V = set()
for vx0, vx1, vy0, vy1 in PITS:
    for vx in range(vx0, vx1 + 1):
        for vy in range(vy0, vy1 + 1): V.add((vx, vy))
# góc ô -> id ô (wangid góc: TR, BR, BL, TL)
TILE = {  # (TL, TR, BR, BL) -> id ô bộ wang "spikes" của Tileset-Terrain-old prison (giống hố gai cũ của Map 4)
    (0, 0, 1, 0): 546, (0, 0, 1, 1): 547, (0, 0, 0, 1): 614,
    (0, 1, 1, 0): 675, (1, 0, 0, 1): 679,
    (0, 1, 0, 0): 806, (1, 1, 0, 0): 807, (1, 0, 0, 0): 744,
    (0, 1, 1, 1): 611, (1, 0, 1, 1): 613, (1, 1, 1, 0): 741, (1, 1, 0, 1): 743,
    (1, 1, 1, 1): 677,
}
FULL = [677, 677, 676, 678, 742]
FG = 3621
def set_layer(s, name, fn):
    m = re.search(r'(<layer id="\d+" name="%s"[^>]*>.*?<data encoding="csv">\n)(.*?)(\n</data>)' % re.escape(name), s, re.S); assert m, name
    rows = [r.rstrip(',').split(',') for r in m.group(2).split('\n')]; assert len(rows) == MH
    fn(rows)
    txt = '\n'.join(','.join(r) + (',' if i < MH - 1 else '') for i, r in enumerate(rows))
    return s[:m.start(2)] + txt + s[m.end(2):]
def paint(rows):
    n = 0
    for y in range(82, 104):
        for x in range(33, 66):
            rows[y][x] = '0'
            k = (int((x, y) in V), int((x + 1, y) in V), int((x + 1, y + 1) in V), int((x, y + 1) in V))
            if k == (0, 0, 0, 0): continue
            assert k in TILE, (x, y, k)
            tid = TILE[k]
            if tid == 677: tid = random.choice(FULL)
            rows[y][x] = str(FG + tid); n += 1
    print('op_spk ô:', n)
s = set_layer(s, 'op_spk', paint)
# ------------------------------------------------------------------ vật thể
nid = [int(re.search(r'nextobjectid="(\d+)"', s).group(1))]
def P(k, v):
    if isinstance(v, bool): return '    <property name="%s" type="bool" value="%s"/>' % (k, 'true' if v else 'false')
    if isinstance(v, (int, float)) and not isinstance(v, bool): return '    <property name="%s" type="float" value="%s"/>' % (k, ('%g' % v))
    return '    <property name="%s" value="%s"/>' % (k, str(v).replace('&', '&amp;').replace('"', '&quot;'))
def obj(name, typ, x, y, w, h, props, trigger=True):
    i = nid[0]; nid[0] += 1
    pr = dict(props)
    if trigger: pr['unity:IsTrigger'] = True
    lines = ['  <object id="%d" name="%s" type="%s" x="%g" y="%g" width="%g" height="%g">' % (i, name, typ, round(x * T, 1), round(y * T, 1), round(w * T, 1), round(h * T, 1)), '   <properties>']
    lines += [P(k, pr[k]) for k in sorted(pr)]
    lines += ['   </properties>', '  </object>']
    return '\n'.join(lines) + '\n'
def add_to_group(s, group, text):
    m = re.search(r'<objectgroup id="\d+" name="%s"[^>]*>\n' % re.escape(group), s); assert m, group
    e = s.index(' </objectgroup>', m.end())
    return s[:e] + text + s[e:]
def remove(s, oid):
    pat = re.compile(r'  <object id="%d"[^>]*?(/>|>.*?</object>)\n' % oid, re.S)
    s2, k = pat.subn('', s, count=1); assert k == 1, oid; return s2
def move(s, oid, x, y):
    m = re.search(r'<object id="%d"([^>]*?) x="[\d.]+" y="[\d.]+"' % oid, s); assert m, oid
    return s.replace(m.group(0), '<object id="%d"%s x="%g" y="%g"' % (oid, m.group(1), round(x * T, 1), round(y * T, 1)), 1)
for oid in (935, 936, 937): s = remove(s, oid)                     # lưỡi dao cũ
# trang trí nằm trong hố -> bỏ
pitrects = [(vx0 - .5, vy0 - .5, vx1 + .5, vy1 + .5) for vx0, vx1, vy0, vy1 in PITS]
gone = 0
for gname in ('ground decals', 'props', 'props front'):
    gm = re.search(r'<objectgroup id="\d+" name="%s"[^>]*>\n(.*?) </objectgroup>' % re.escape(gname), s, re.S)
    body = gm.group(1); new = body
    for om in re.finditer(r'  <object id="(\d+)"[^>]*? gid="\d+" x="([\d.]+)" y="([\d.]+)" width="([\d.]+)"[^>]*?(/>|>.*?</object>)\n', body, re.S):
        fx = (float(om.group(2)) + float(om.group(4)) / 2) / T; fy = float(om.group(3)) / T
        if any(a <= fx <= c and bb <= fy <= d for a, bb, c, d in pitrects): new = new.replace(om.group(0), '', 1); gone += 1
    s = s[:gm.start(1)] + new + s[gm.end(1):]
print('bỏ trang trí trong hố:', gone)
# blocker phủ hố (kéo sát tường nếu chỉ còn khe < 1 ô)
bl = ''
for (x0, y0, x1, y1) in pitrects:
    if x1 > 64: x1 = 65
    if y1 > 102: y1 = 103
    bl += obj('blocker', 'blocker', x0, y0, x1 - x0, y1 - y0, {'collider_kind': 'terrain'}, trigger=False)
s = add_to_group(s, 'blockers', bl)
# ------------------------------------------------------------------ bẫy
M = ''
def saw(name, x0, y0, w, h, off, speed=3.0, dmg=16):
    return obj(name, 'blade', x0, y0, w, h, {'mechanic': 'blade', 'kind': 'saw', 'damage': dmg, 'knock': 3, 'speed': speed, 'pause': 0.25, 'offset': off})
def spikes(name, x0, y0, w, h, mode='cycle', off=0, on=0.8, offs=1.3, step=0.0, axis='y', style=1):
    pr = {'mechanic': 'spikes', 'mode': mode, 'damage': 14, 'style': style, 'warn_seconds': 0.5 if mode == 'cycle' else 0.35,
          'on_seconds': on, 'off_seconds': offs, 'offset': off, 'wave_axis': axis, 'wave_step': step}
    return obj(name, 'spikes', x0, y0, w, h, pr)
def launcher(name, x, y, kind, d, period, off, rng=14, dmg=None, mode='cycle', trig='', burst=1):
    pr = {'mechanic': 'launcher', 'kind': kind, 'dir': d, 'mode': mode, 'period': period, 'offset': off, 'range': rng, 'warn_seconds': 0.6}
    if dmg: pr['damage'] = dmg
    if kind == 'ice': pr.update({'chill': 0.55, 'chill_seconds': 2})
    if kind in ('fire', 'fire_hole'): pr.update({'burn_dps': 4, 'burn_seconds': 2})
    if trig: pr['trigger'] = trig
    if burst > 1: pr.update({'burst': burst, 'burst_gap': 0.28})
    return obj(name, 'launcher', x, y, 1, 1, pr)
# Tiền sảnh (x 62.5-65): gai sóng — học nhịp
M += spikes('maze_v_spk', 62.5, 97, 2.5, 3, step=0.12)
# Làn 1 (x 58.5-60.5, đi LÊN): 2 lưỡi cưa ngang, ray chui vào hố hai bên
M += saw('maze_l1_saw_a', 56.6, 97, 6.3, 1, 0.0)
M += saw('maze_l1_saw_b', 56.6, 92, 6.3, 1, 0.5)
# Làn 2 (x 54.5-56.5, đi XUỐNG): 2 lỗ lửa ở đáy bắn ngược lên, so le trái / phải -> lách theo nhịp
M += launcher('maze_l2_fire_l', 54.5, 101.6, 'fire_hole', 'up', 1.8, 0.0, 15, 12)
M += launcher('maze_l2_fire_r', 55.5, 101.6, 'fire_hole', 'up', 1.8, 0.9, 15, 12)
# Làn 3 (x 50.5-52.5, đi LÊN): gai sóng chạy dọc làn + 1 lưỡi cưa ngang giữa làn
M += spikes('maze_l3_spk', 50.5, 90, 2, 10, on=0.7, offs=1.1, step=0.14)
M += saw('maze_l3_saw', 48.6, 95, 6.3, 1, 0.25, 3.4)
# Làn 4 (x 46.5-48.5, đi XUỐNG): 2 lỗ băng (làm chậm) bắn lên, nhịp nhanh hơn + lưỡi cưa ngang
M += launcher('maze_l4_ice_l', 46.5, 101.6, 'ice', 'up', 1.5, 0.0, 15, 9)
M += launcher('maze_l4_ice_r', 47.5, 101.6, 'ice', 'up', 1.5, 0.75, 15, 9)
M += saw('maze_l4_saw', 44.6, 93, 6.3, 1, 0.6, 3.4)
# Làn 5 (x 42.5-44.5, đi LÊN): bước lên tấm ép ở đầu làn -> tên bắn loạt 3 từ đỉnh làn (nửa trái) ; 1 lưỡi cưa ngang
M += obj('maze_l5_plate', 'plate', 42.6, 99.6, 1.8, 0.9, {'mechanic': 'plate', 'by': 'any', 'delay': 0.35, 'fires': 'maze_l5_volley', 'rearm': 2.2})
M += launcher('maze_l5_arrow', 42.6, 88.0, 'arrow', 'down', 3, 0, 15, 12, mode='trigger', trig='maze_l5_volley', burst=3)
M += saw('maze_l5_saw', 40.6, 94, 6.3, 1, 0.1, 3.6)
# Làn 6 (x 38.5-40.5, đi XUỐNG): 2 lưỡi cưa (chờ ở ngoài vùng gai) + gai "bước lên là trồi" ở giữa -> không được đứng lại
M += saw('maze_l6_saw_a', 36.6, 91, 6.3, 1, 0.0, 3.6)
M += saw('maze_l6_saw_b', 36.6, 98, 6.3, 1, 0.5, 3.6)
M += spikes('maze_l6_step', 38.5, 93, 2, 4, mode='step', on=0.9)
# Làn 7 (x 34-36.5, đi LÊN tới bãi trên): mặt quỷ phun lửa trên tường tây bắn ngang qua làn, 3 hàng so le
for i, (yy, off) in enumerate([(99, 0.0), (95, 0.6), (91, 1.2)]):
    M += launcher('maze_l7_fire_%d' % i, 33.2, yy, 'fire', 'right', 1.8, off, 4.5, 14)
# Bãi trên (y 83-85.5): 2 lưỡi cưa dọc cắt ngang bãi (ray chui vào dải hố) trên đường từ tời về cầu
M += saw('maze_top_saw_a', 43, 82.6, 1, 5.2, 0.0, 3.2)
M += saw('maze_top_saw_b', 49, 82.6, 1, 5.2, 0.5, 3.2)
s = add_to_group(s, 'mechanics', M)
# tời cầu: dời ra đầu tây bãi trên (cuối mê cung); điểm nghỉ: tiền sảnh + bãi trên
s = move(s, 915, 34.6, 83.4)
s = move(s, 879, 62.7, 88.0); s = move(s, 890, 63.2, 87.3)
s = add_to_group(s, 'triggers', obj('rest_maze_top', 'checkpoint', 36.8, 83.2, 2, 2, {'marker': True, 'stage': 'pit', 'stage_name': 'The Blood Pit', 'zone_id': 'p6'}))
s = add_to_group(s, 'mechanics', obj('rest_maze_top_c', 'light', 37.3, 82.5, 1, 1, {'anim': 'flicker', 'anim_amount': 0.15, 'anim_speed': 0.8, 'color': '#ffb070', 'intensity': 0.6, 'light': 'torch', 'mechanic': 'light', 'radius': 2}))
# quái: dơi ở tiền sảnh, Cutthroat phục ở bãi trên
s = re.sub(r'(<object id="994" name="Pit_Bat_3" type="spawn") x="[\d.]+" y="[\d.]+"', r'\1 x="%g" y="%g"' % (63.7 * T, 91 * T), s, count=1)
s = re.sub(r'(<object id="995" name="Choir_Cutthroat_4" type="spawn") x="[\d.]+" y="[\d.]+"', r'\1 x="%g" y="%g"' % (47 * T, 84.3 * T), s, count=1)
s = re.sub(r'nextobjectid="\d+"', 'nextobjectid="%d"' % nid[0], s, count=1)
if crlf: s = s.replace('\n', '\r\n')
open(TMX, 'wb').write(s.encode('utf-8')); print('md5', hashlib.md5(s.encode('utf-8')).hexdigest(), 'next id', nid[0])
