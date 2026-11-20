# BFS kiểm đường đi map 3b mới (lưới 1/4 ô, chặn = blockers + obstacle colliders + bound), theo portal và cờ.
import sys, xml.etree.ElementTree as ET
from collections import deque
r = ET.parse(sys.argv[1]).getroot(); W, H = int(r.get('width')), int(r.get('height')); S = 4; T = 32.0
block = [[False] * (W * S) for _ in range(H * S)]
objs = []
for g in r.iter('objectgroup'):
    for o in g.iter('object'):
        pr = {p.get('name'): p.get('value') for p in o.iter('property')}
        x, y = float(o.get('x')) / T, float(o.get('y')) / T; w, h = float(o.get('width') or 0) / T, float(o.get('height') or 0) / T
        objs.append((g.get('name'), o.get('name'), o.get('type'), x, y, w, h, pr))
        if g.get('name') in ('blockers', 'obstacle colliders'):
            for yy in range(int(y * S), int((y + h) * S + 0.999)):
                for xx in range(int(x * S), int((x + w) * S + 0.999)):
                    if 0 <= yy < H * S and 0 <= xx < W * S: block[yy][xx] = True
def reach(start):
    sx, sy = int(start[0] * S), int(start[1] * S); seen = {(sx, sy)}; q = deque([(sx, sy)])
    assert not block[sy][sx], ('start blocked', start)
    while q:
        x, y = q.popleft()
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            n = (x + dx, y + dy)
            if 0 <= n[0] < W * S and 0 <= n[1] < H * S and n not in seen and not block[n[1]][n[0]]: seen.add(n); q.append(n)
    return seen
def hits(seen, name):
    for g, n, t, x, y, w, h, pr in objs:
        if n == name:
            return any((xx, yy) in seen for yy in range(int(y * S), int((y + h) * S + 0.999)) for xx in range(int(x * S), int((x + w) * S + 0.999)))
    raise KeyError(name)
checks = [((4.6, 5.4), ['x2_climb', 'x2_slab', 'x2_down', 'x2_coffin', 'kings_relic']),
          ((26, 4.8), ['x3_up', 'x3_east', 'x3_order', 'x3_letter']),
          ((27, 21), ['x4_back', 'x4_tomb', 'red_field_fight']),
          ((6, 24.8), ['x5_back', 'barrow_oath', 'x5_enter'])]
ok = True
for st, names in checks:
    s = reach(st)
    for n in names:
        h = hits(s, n); ok &= h
        print('%-14s từ %-10s %s' % (n, st, 'ĐẠT' if h else 'KHÔNG TỚI'))
# đích portal không nằm trong khối chặn / không nằm trong chính portal khác
for g, n, t, x, y, w, h, pr in objs:
    if t == 'portal':
        tx, ty = [float(v) for v in pr['target'].split(',')]
        b = block[int(ty * S)][int(tx * S)]
        inside = [m for gg, m, tt, X, Y, WW, HH, pp in objs if tt == 'portal' and X <= tx <= X + WW and Y <= ty <= Y + HH]
        print('portal %-8s -> %-9s chặn=%s trong portal=%s' % (n, pr['target'], b, inside)); ok &= (not b and not inside)
print('KẾT QUẢ', 'ĐẠT' if ok else 'LỖI')
