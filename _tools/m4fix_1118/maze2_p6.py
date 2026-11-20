# 18/11 (lần 2, người dùng chê bản 1): Blade Yard (p6) -> MÊ CUNG BẪY 6x4 ô (hành lang 3–4,5 ô đủ chỗ né, vách = hố gai 2 ô).
# Nguyên tắc (tham khảo thiết kế mê cung): nhìn thấy đích (cầu) từ sớm; 2 nhánh cụt DÀI có phần thưởng (phanh bẫy / điểm nghỉ ẩn);
# đường chính và đường về KHÁC nhau; mỗi ô một kiểu bẫy, khó dần; bẫy bắn (launcher = lỗ / tượng trên TƯỜNG) chỉ gắn trên tường thật của phòng.
# Dựng lại từ bản TRƯỚC mê cung 1 (_backups/2026-11-18_m4_maze/), kiểm md5 bản hiện tại (mê cung 1) trước khi ghi đè.
import hashlib, re, os, shutil, sys, random
ROOT = os.path.expanduser(os.environ.get('M4ROOT', '~/mnt/Soul Requiem'))
TMX = ROOT + "/Assets/GamePackage/EPIC RPG World Pack - Crypt V.1.6/TiledMap Editor/Maps/map4_catacombs_of_naming.tmx"
BASE = os.path.expanduser('~/mnt/Soul Requiem') + "/_backups/2026-11-18_m4_maze/map4_catacombs_of_naming.tmx"
cur = open(TMX, 'rb').read(); assert hashlib.md5(cur).hexdigest() == sys.argv[1], hashlib.md5(cur).hexdigest()
b = open(BASE, 'rb').read(); assert hashlib.md5(b).hexdigest() == 'f8fe5f1aaf272963d45acc0aad34c61d'
os.makedirs(ROOT + "/_backups/2026-11-18_m4_maze2", exist_ok=True)
shutil.copy2(TMX, ROOT + "/_backups/2026-11-18_m4_maze2/map4_maze1.tmx")
s = b.decode('utf-8'); crlf = '\r\n' in s
if crlf: s = s.replace('\r\n', '\n')
MW, MH, T = 114, 124, 32
random.seed(18112)
# ------------------------------------------------------------------ lưới mê cung (đỉnh lưới của bộ wang góc)
VX = [(35, 37), (40, 42), (45, 47), (50, 52), (55, 57), (60, 64)]          # cột A..F (đoạn vách ngang nằm trên cột)
WX = [(38, 39), (43, 44), (48, 49), (53, 54), (58, 59)]                    # vách dọc giữa cột i và i+1
VY = [(84, 86), (89, 91), (94, 96), (99, 102)]                             # hàng 0..3
WY = [(87, 88), (92, 93), (97, 98)]                                        # vách ngang giữa hàng j và j+1
CX = [(34, 37.5), (39.5, 42.5), (44.5, 47.5), (49.5, 52.5), (54.5, 57.5), (59.5, 65)]   # lòng ô (để đặt bẫy)
CY = [(83, 86.5), (88.5, 91.5), (93.5, 96.5), (98.5, 103)]
cols = 'ABCDEF'
def cell(n): return cols.index(n[0]), int(n[1])
OPEN = set()
def link(a, b):
    (c1, r1), (c2, r2) = cell(a), cell(b)
    assert abs(c1 - c2) + abs(r1 - r2) == 1, (a, b)
    OPEN.add(('v', min(c1, c2), r1) if r1 == r2 else ('h', c1, min(r1, r2)))
MAIN = ['F2', 'F3', 'E3', 'D3', 'D2', 'E2', 'E1', 'D1', 'C1', 'C2', 'C3', 'B3', 'A3']          # lối vào -> tời cầu (A3)
BACK = ['A3', 'A2', 'A1', 'A0', 'B0', 'C0', 'D0', 'E0']                                     # tời -> cầu (E0)
DEAD = [['E1', 'F1', 'F0'], ['C2', 'B2', 'B1']]                                             # F0: bánh phanh; B1: điểm nghỉ ẩn
for path in [MAIN, BACK] + DEAD:
    for a, c in zip(path, path[1:]): link(a, c)
pieces = []                                      # (vx0, vx1, vy0, vy1)
for i, (a, c) in enumerate(WX):
    for j, (p, q) in enumerate(WY): pieces.append((a, c, p, q))                     # trụ
for i, (a, c) in enumerate(WX):
    for r, (p, q) in enumerate(VY):
        if ('v', i, r) not in OPEN: pieces.append((a, c, p, q))
for j, (p, q) in enumerate(WY):
    for ci, (a, c) in enumerate(VX):
        if ('h', ci, j) not in OPEN: pieces.append((a, c, p, q))
V = set()
for vx0, vx1, vy0, vy1 in pieces:
    for vx in range(vx0, vx1 + 1):
        for vy in range(vy0, vy1 + 1): V.add((vx, vy))
TILE = {(0, 0, 1, 0): 546, (0, 0, 1, 1): 547, (0, 0, 0, 1): 614, (0, 1, 1, 0): 675, (1, 0, 0, 1): 679,
        (0, 1, 0, 0): 806, (1, 1, 0, 0): 807, (1, 0, 0, 0): 744,
        (0, 1, 1, 1): 611, (1, 0, 1, 1): 613, (1, 1, 1, 0): 741, (1, 1, 0, 1): 743, (1, 1, 1, 1): 677}
FULL = [677, 677, 676, 678, 742]; FG = 3621
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
            assert k in TILE, ('ô chéo không có hình', x, y, k)
            t = TILE[k]; t = random.choice(FULL) if t == 677 else t
            rows[y][x] = str(FG + t); n += 1
    print('ô hố gai:', n)
s = set_layer(s, 'op_spk', paint)
pitrects = []
for vx0, vx1, vy0, vy1 in pieces:
    x0, x1, y0, y1 = vx0 - .5, vx1 + .5, vy0 - .5, vy1 + .5
    if x0 < 35: x0 = 34
    if x1 > 64: x1 = 65
    if y0 < 84: y0 = 83
    if y1 > 102: y1 = 103
    pitrects.append((x0, y0, x1, y1))
# ------------------------------------------------------------------ tiện ích vật thể
nid = [int(re.search(r'nextobjectid="(\d+)"', s).group(1))]
def P(k, v):
    if isinstance(v, bool): return '    <property name="%s" type="bool" value="%s"/>' % (k, 'true' if v else 'false')
    if isinstance(v, (int, float)): return '    <property name="%s" type="float" value="%g"/>' % (k, v)
    return '    <property name="%s" value="%s"/>' % (k, str(v).replace('&', '&amp;').replace('"', '&quot;'))
def obj(name, typ, x, y, w, h, props, trigger=True):
    i = nid[0]; nid[0] += 1
    pr = dict(props)
    if trigger: pr['unity:IsTrigger'] = True
    L = ['  <object id="%d" name="%s" type="%s" x="%g" y="%g" width="%g" height="%g">' % (i, name, typ, round(x * T, 1), round(y * T, 1), round(w * T, 1), round(h * T, 1)), '   <properties>']
    L += [P(k, pr[k]) for k in sorted(pr)] + ['   </properties>', '  </object>']
    return '\n'.join(L) + '\n'
def add_to_group(s, group, text):
    m = re.search(r'<objectgroup id="\d+" name="%s"[^>]*>\n' % re.escape(group), s); assert m, group
    e = s.index(' </objectgroup>', m.end()); return s[:e] + text + s[e:]
def remove(s, oid):
    s2, k = re.subn(r'  <object id="%d"[^>]*?(/>|>.*?</object>)\n' % oid, '', s, count=1, flags=re.S); assert k == 1, oid; return s2
def move(s, oid, x, y):
    m = re.search(r'<object id="%d"([^>]*?) x="[\d.]+" y="[\d.]+"' % oid, s); assert m, oid
    return s.replace(m.group(0), '<object id="%d"%s x="%g" y="%g"' % (oid, m.group(1), round(x * T, 1), round(y * T, 1)), 1)
def inter(a, b): return a[0] < b[2] and b[0] < a[2] and a[1] < b[3] and b[1] < a[3]
for oid in (935, 936, 937): s = remove(s, oid)
# ------------------------------------------------------------------ bẫy (toạ độ ô; mỗi bẫy phải nằm gọn trong lòng ô / lối mở, không chạm hố, không chạm chân đèn)
LAMPS = [(51.3, 84.2, 51.9, 84.6), (59.3, 84.2, 59.9, 84.6), (34.8, 101.4, 35.4, 101.8), (62.5, 101.4, 63.1, 101.8), (34.8, 83.2, 35.4, 83.6)]
traps = []; M = ''
def floor_trap(text, rect):
    global M
    for pr in pitrects: assert not inter(rect, pr), ('bẫy chạm hố', text[:60], rect, pr)
    for lp in LAMPS: assert not inter(rect, lp), ('bẫy chạm chân đèn', text[:60], rect)
    traps.append(rect); M += text
def saw(n, x0, y0, w, h, off, sp=3.2, stop=''):
    # ray lưỡi cưa = đoạn thẳng (w = 0: dọc, h = 0: ngang); vật Tiled dày 1 ô quanh đường ray
    pr = {'mechanic': 'blade', 'kind': 'saw', 'damage': 16, 'knock': 3, 'speed': sp, 'pause': 0.25, 'offset': off}
    if stop: pr['stop_when'] = stop
    ox, oy, ow, oh = (x0 - .5, y0, 1, h) if w == 0 else (x0, y0 - .5, w, 1)
    floor_trap(obj(n, 'blade', ox, oy, ow, oh, pr), (x0 - .45, y0 - .45, x0 + w + .45, y0 + h + .45))   # lưỡi cưa ~0,9 ô
def spikes(n, x0, y0, w, h, mode='cycle', off=0.0, on=0.8, offs=1.3, step=0.0, axis='y', stop=''):
    pr = {'mechanic': 'spikes', 'mode': mode, 'damage': 14, 'style': 1, 'warn_seconds': 0.5 if mode == 'cycle' else 0.35,
          'on_seconds': on, 'off_seconds': offs, 'offset': off, 'wave_axis': axis, 'wave_step': step}
    if stop: pr['stop_when'] = stop
    floor_trap(obj(n, 'spikes', x0, y0, w, h, pr), (x0, y0, x0 + w, y0 + h))
def pittrap(n, x0, y0, w, h, back):
    floor_trap(obj(n, 'pit', x0, y0, w, h, {'mechanic': 'pit', 'damage': 14, 'open_seconds': 2.2, 'warn_seconds': 0.45, 'size': 2, 'return_at': back,
               'text': 'The floor gives way. You haul yourself out of the dark.'}), (x0, y0, x0 + w, y0 + h))
def wall_shooter(n, x, y, kind, d, period, off, rng, stop=''):
    # lỗ / tượng bắn gắn trên TƯỜNG thật của phòng (x 34 tây, x 65 đông, y 83 bắc)
    global M
    pr = {'mechanic': 'launcher', 'kind': kind, 'dir': d, 'mode': 'cycle', 'period': period, 'offset': off, 'range': rng, 'warn_seconds': 0.6}
    if kind == 'fire': pr.update({'burn_dps': 4, 'burn_seconds': 2})
    if stop: pr['stop_when'] = stop
    M += obj(n, 'launcher', x, y, 1, 1, pr)
BR = 'yard_brake'
# --- đường chính
spikes('mz_F2_spk_a', 60.0, 93.7, 2.1, 2.6, off=0.0)                       # F2 lối vào: gai hai nửa thay phiên -> học nhịp
spikes('mz_F2_spk_b', 62.6, 93.7, 2.1, 2.6, off=1.05)
wall_shooter('mz_F3_arrow_a', 64.6, 98.9, 'arrow', 'left', 1.6, 0.0, 16)    # F3->E3->D3: tên từ tường đông bay dọc hàng dưới, hai độ cao so le
wall_shooter('mz_F3_arrow_b', 64.6, 101.3, 'arrow', 'left', 1.6, 0.8, 16)
pittrap('mz_E3_pit', 55.2, 100.0, 1.8, 1.4, '58.3,100.7')                   # E3: sàn sập giữa ô, đi men mép trên / dưới
saw('mz_D3_saw', 51.0, 99.2, 0.0, 3.2, 0.0)                                 # D3: cưa chạy dọc giữa ô, né hai bên
spikes('mz_D2_step', 50.0, 94.0, 2.0, 2.0, mode='step', on=0.9)            # D2: gai "bước là trồi"
saw('mz_E2_saw', 55.1, 95.0, 1.8, 0.0, 0.3, 3.4)                            # E2: cưa ngang chắn đường đi lên
spikes('mz_E1_wave', 54.7, 88.7, 2.6, 2.6, off=0.0, on=0.7, offs=1.1, step=0.12, axis='x')   # E1 (ngã ba): gai sóng
saw('mz_D1_saw_a', 50.4, 89.0, 0.0, 2.0, 0.0, 3.4)                          # D1: hai cưa dọc cắt ngang đường đi tây
saw('mz_D1_saw_b', 51.7, 89.0, 0.0, 2.0, 0.5, 3.4)
pittrap('mz_C1_pit', 45.2, 89.0, 1.6, 1.8, '48.2,90.0')                     # C1: sàn sập
spikes('mz_C2_wave', 44.7, 93.7, 2.6, 2.6, off=0.4, on=0.7, offs=1.0, step=0.12)   # C2 (ngã ba): gai sóng dọc
saw('mz_C3_saw', 45.0, 99.3, 0.0, 3.0, 0.2, 3.6)                            # C3: cưa dọc ngay trước cửa tây
saw('mz_B3_saw_a', 40.4, 99.3, 0.0, 3.0, 0.0, 3.8)                          # B3: hai cưa dọc lệch nhịp
saw('mz_B3_saw_b', 41.7, 99.3, 0.0, 3.0, 0.5, 3.8)
wall_shooter('mz_A3_fire_a', 33.6, 98.7, 'fire', 'right', 1.8, 0.0, 4)      # A3 (tời cầu): tượng phun lửa tường tây
wall_shooter('mz_A3_fire_b', 33.6, 100.6, 'fire', 'right', 1.8, 0.9, 4)
# --- nhánh cụt 1 (E1 -> F1 -> F0): phần thưởng = bánh phanh, tắt bẫy đường về
wall_shooter('mz_F1_fire', 64.6, 89.5, 'fire', 'left', 2.0, 0.3, 6)
saw('mz_F1_saw', 61.0, 89.0, 0.0, 2.0, 0.0, 3.4)
wall_shooter('mz_F0_arrow', 61.6, 82.6, 'arrow', 'down', 1.7, 0.0, 4)
M += obj('yard_brake', 'lever', 63.3, 84.0, 1, 1, {'mechanic': 'lever', 'range': 0.9, 'sets': BR, 'sprite': 'trap_gear', 'verb': 'Turn',
         'hint': 'A brake wheel, wound tight. Its chains run into the walls of the yard.',
         'text': 'Gears grind to a stop somewhere along the west wall.'})
# --- nhánh cụt 2 (C2 -> B2 -> B1): phần thưởng = điểm nghỉ ẩn
saw('mz_B2_saw', 40.0, 95.0, 2.0, 0.0, 0.0, 3.4)
# --- đường về (tời -> cầu); bánh phanh tắt các bẫy có stop_when
wall_shooter('mz_A2_arrow_a', 33.6, 93.8, 'arrow', 'right', 1.5, 0.0, 4, BR)
wall_shooter('mz_A2_arrow_b', 33.6, 95.4, 'arrow', 'right', 1.5, 0.75, 4, BR)
saw('mz_A1_saw_a', 34.6, 89.2, 2.4, 0.0, 0.0, 3.6, BR)
saw('mz_A1_saw_b', 34.6, 90.8, 2.4, 0.0, 0.5, 3.6, BR)
wall_shooter('mz_B0_arrow_a', 40.0, 82.6, 'arrow', 'down', 1.5, 0.0, 4)
wall_shooter('mz_B0_arrow_b', 41.6, 82.6, 'arrow', 'down', 1.5, 0.75, 4)
wall_shooter('mz_C0_fire', 45.6, 82.6, 'fire', 'down', 2.0, 0.4, 4)
spikes('mz_D0_step', 49.8, 85.0, 2.5, 1.3, mode='step', on=0.9)
s = add_to_group(s, 'mechanics', M)
# ------------------------------------------------------------------ tời cầu, điểm nghỉ, quái
s = move(s, 915, 34.4, 98.6)                                                 # tời cầu: góc tây bắc ô A3 (đích sâu nhất)
s = move(s, 879, 65.4, 92.3); s = move(s, 890, 65.9, 91.6)                 # điểm nghỉ lối vào (khe giữa Rolling Hall và mê cung)
s = add_to_group(s, 'triggers', obj('rest_maze_mid', 'checkpoint', 40.0, 89.0, 2, 2, {'marker': True, 'stage': 'pit', 'stage_name': 'The Blood Pit', 'zone_id': 'p6'}))
s = add_to_group(s, 'triggers', obj('rest_maze_top', 'checkpoint', 34.6, 84.0, 2, 2, {'marker': True, 'stage': 'pit', 'stage_name': 'The Blood Pit', 'zone_id': 'p6'}))
LIGHT = {'anim': 'flicker', 'anim_amount': 0.15, 'anim_speed': 0.8, 'color': '#ffb070', 'intensity': 0.6, 'light': 'torch', 'mechanic': 'light', 'radius': 2}
s = add_to_group(s, 'mechanics', obj('rest_maze_mid_c', 'light', 40.5, 88.4, 1, 1, LIGHT) + obj('rest_maze_top_c', 'light', 35.1, 83.4, 1, 1, LIGHT))
s = re.sub(r'(<object id="994" name="Pit_Bat_3" type="spawn") x="[\d.]+" y="[\d.]+"', r'\1 x="%g" y="%g"' % (56 * T, 90 * T), s, count=1)
s = re.sub(r'(<object id="995" name="Choir_Cutthroat_4" type="spawn") x="[\d.]+" y="[\d.]+"', r'\1 x="%g" y="%g"' % (51 * T, 85.8 * T), s, count=1)
# ------------------------------------------------------------------ blocker phủ hố + dọn trang trí nằm trong hố / dưới bẫy
s = add_to_group(s, 'blockers', ''.join(obj('blocker', 'blocker', x0, y0, x1 - x0, y1 - y0, {'collider_kind': 'terrain'}, trigger=False) for x0, y0, x1, y1 in pitrects))
gone = 0
for gname in ('ground decals', 'props', 'props front'):
    gm = re.search(r'<objectgroup id="\d+" name="%s"[^>]*>\n(.*?) </objectgroup>' % re.escape(gname), s, re.S)
    body = gm.group(1); new = body
    for om in re.finditer(r'  <object id="(\d+)"[^>]*? gid="\d+" x="([\d.]+)" y="([\d.]+)" width="([\d.]+)" height="([\d.]+)"[^>]*?(/>|>.*?</object>)\n', body, re.S):
        x0 = float(om.group(2)) / T; y1 = float(om.group(3)) / T; w = float(om.group(4)) / T; h = float(om.group(5)) / T
        r = (x0, y1 - min(h, 1.0), x0 + w, y1)                              # phần chân (≤1 ô dưới cùng) của hình
        if 34 <= x0 + w / 2 <= 65 and 83 <= y1 <= 103.2 and any(inter(r, q) for q in pitrects + traps):
            new = new.replace(om.group(0), '', 1); gone += 1
    s = s[:gm.start(1)] + new + s[gm.end(1):]
print('bỏ trang trí trong hố / dưới bẫy:', gone)
s = re.sub(r'nextobjectid="\d+"', 'nextobjectid="%d"' % nid[0], s, count=1)
if crlf: s = s.replace('\n', '\r\n')
open(TMX, 'wb').write(s.encode('utf-8')); print('md5', hashlib.md5(s.encode('utf-8')).hexdigest(), 'next id', nid[0], 'mảnh hố', len(pieces), 'bẫy sàn', len(traps))
