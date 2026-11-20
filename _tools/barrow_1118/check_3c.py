# 18/11 (§331): kiểm map 3b NỐI LIỀN theo từng giai đoạn cờ (BFS lưới 1/4 ô; chặn = blockers + obstacle colliders + bound + cổng đang đóng).
# Mỗi giai đoạn: những gì PHẢI tới được và những gì KHÔNG được tới (không đi tắt qua bẫy / boss). Thêm: kiểm vết chân object.
import sys, xml.etree.ElementTree as ET
from collections import deque
r = ET.parse(sys.argv[1]).getroot(); W, H = int(r.get('width')), int(r.get('height')); S = 4; T = 32.0
objs = []
for g in r.iter('objectgroup'):
    for o in g.iter('object'):
        pr = {p.get('name'): p.get('value') for p in o.iter('property')}
        x, y = float(o.get('x')) / T, float(o.get('y')) / T; w, h = float(o.get('width') or 0) / T, float(o.get('height') or 0) / T
        if o.get('gid'): y -= h                                     # tile object: y = chân
        objs.append(dict(g=g.get('name'), n=o.get('name'), t=o.get('type'), x=x, y=y, w=w, h=h, pr=pr, gid=o.get('gid')))
def cells(o):
    return [(xx, yy) for yy in range(int(o['y'] * S), int((o['y'] + o['h']) * S + 0.999)) for xx in range(int(o['x'] * S), int((o['x'] + o['w']) * S + 0.999))]
static = set()
for o in objs:
    if o['g'] in ('blockers', 'obstacle colliders'): static.update(cells(o))
gates = [o for o in objs if o['g'] == 'gates' and o['t'] == 'gate']
def gate_open(o, flags, fight=False):
    p = o['pr']
    if p.get('close_on_enter') == 'true': return not fight
    if p.get('starts_closed') == 'false': return True
    uw = p.get('unlock_when')
    if uw: return all(f.strip() in flags for f in uw.split(','))
    return (p.get('zone_id', '') + '_cleared') in flags
def reach(start, flags, fight=False):
    block = set(static)
    for o in gates:
        if not gate_open(o, flags, fight): block.update(cells(o))
    s = (int(start[0] * S), int(start[1] * S)); assert s not in block, ('start blocked', start)
    seen = {s}; q = deque([s])
    while q:
        x, y = q.popleft()
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            n = (x + dx, y + dy)
            if 0 <= n[0] < W * S and 0 <= n[1] < H * S and n not in seen and n not in block: seen.add(n); q.append(n)
    return seen
def find(name, typ=None):
    for o in objs:
        if o['n'] == name and (typ is None or o['t'] == typ) and o['g'] != 'gates': return o
    raise KeyError(name)
def hits(seen, name):
    o = find(name)
    if o['w'] == 0: return (int(o['x'] * S), int(o['y'] * S)) in seen
    return any(c in seen for c in cells(o))
START = (find('player_start')['x'] + 0.5, find('player_start')['y'] + 0.5)
ARENA = (17, 34)
F0 = set()
F1 = {'x2_rubble_broken'}
F2 = F1 | {'x3_cleared'}
F3 = F2 | {'x7_held'}
F4 = F3 | {'x4_cleared'}
plan = [
    ('0 chưa phá đá', START, F0, False, ['x2_climb', 'x2_coffin', 'kings_relic', 'x2_rubble'], ['x3', 'x3_order']),
    ('1 phá đá', START, F1, False, ['x3', 'x3_order', 'x3_letter'], ['x6', 'x6_band1']),
    ('2 dọn x3', START, F2, False, ['x6_enter', 'x6_band1', 'x6_band4', 'x7_ranks'], ['x8', 'rest_barrow']),
    ('3 trụ sảnh', START, F3, False, ['rest_barrow', 'x8_enter', 'red_field_fight', 'x4'], ['x5', 'barrow_oath']),
    ('3b đang đánh boss', ARENA, F3, True, ['red_field_fight'], ['rest_barrow', 'x5']),
    ('4 hạ boss', START, F4, False, ['barrow_oath', 'x5_enter', 'x5_taken', 'x2_climb'], []),
]
ok = True
for label, st, fl, fight, must, mustnot in plan:
    s = reach(st, fl, fight)
    for n in must:
        h = hits(s, n); ok &= h; print('%-18s %-16s %s' % (label, n, 'ĐẠT' if h else '!! KHÔNG TỚI'))
    for n in mustnot:
        h = hits(s, n); ok &= not h; print('%-18s %-16s %s' % (label, n, 'chặn đúng' if not h else '!! LỌT TỚI'))
# vết chân: vật tương tác / điểm sinh / điểm nghỉ / đèn không nằm hẳn trong khối chặn
for o in objs:
    if o['g'] in ('triggers', 'spawns', 'mechanics') and o['t'] not in ('light', 'toggle'):
        cs = cells(o) if o['w'] else [(int(o['x'] * S), int(o['y'] * S))]
        free = [c for c in cs if c not in static]
        if not free: ok = False; print('!! %s (%s) nằm trong khối chặn' % (o['n'], o['t']))
    if o['t'] == 'portal': ok = False; print('!! còn portal', o['n'])
print('KẾT QUẢ', 'ĐẠT' if ok else 'LỖI')
