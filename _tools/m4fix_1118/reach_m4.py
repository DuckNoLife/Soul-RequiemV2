# 18/11: kiểm đường đi Map 4 theo từng giai đoạn cờ (BFS trên lưới 1/2 ô; chặn = blocker + obstacle + cổng còn đóng).
import os, xml.etree.ElementTree as ET
from collections import deque
TMX = os.path.expanduser("~/mnt/Soul Requiem/Assets/GamePackage/EPIC RPG World Pack - Crypt V.1.6/TiledMap Editor/Maps/map4_catacombs_of_naming.tmx")
r = ET.parse(TMX).getroot(); MW, MH = int(r.get('width')), int(r.get('height')); S = 2   # 2 ô con / ô
solid, gates, named = [], {}, {}
for o in r.iter('object'):
    t = o.get('type') or ''; x, y, w, h = (float(o.get(k, 0)) / 32 for k in ('x', 'y', 'width', 'height'))
    p = {q.get('name'): (q.get('value') or '') for q in o.iter('property')}
    if t in ('blocker', 'obstacle', 'bound'): solid.append((x, y, x + w, y + h))
    if t == 'gate': gates[o.get('name')] = ((x, y, x + w, y + h), p)
    if o.get('name'): named[o.get('name')] = ((x, y, x + w, y + h), p, t)
def blocked_grid(flags):
    G = [[False] * (MW * S) for _ in range(MH * S)]
    rects = list(solid)
    for n, (rc, p) in gates.items():
        uw = p.get('unlock_when', '') or (n + '_unlocked' if p.get('requires_key') else '')
        if p.get('starts_closed', 'true') == 'true' and not (uw and all(f in flags for f in uw.split(','))): rects.append(rc)
    for (x0, y0, x1, y1) in rects:
        for gy in range(max(0, int(y0 * S)), min(MH * S, int(y1 * S + 0.999))):
            for gx in range(max(0, int(x0 * S)), min(MW * S, int(x1 * S + 0.999))):
                cx, cy = (gx + 0.5) / S, (gy + 0.5) / S
                if x0 < cx < x1 and y0 < cy < y1: G[gy][gx] = True
    return G
def reach(flags, start):
    G = blocked_grid(flags); sx, sy = int(start[0] * S), int(start[1] * S)
    seen = {(sx, sy)}; q = deque([(sx, sy)])
    while q:
        x, y = q.popleft()
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nx, ny = x + dx, y + dy
            if 0 <= nx < MW * S and 0 <= ny < MH * S and not G[ny][nx] and (nx, ny) not in seen: seen.add((nx, ny)); q.append((nx, ny))
    return seen
def can(seen, name, pad=0.6):
    (x0, y0, x1, y1) = named[name][0]
    return any((gx, gy) in seen for gy in range(int((y0 - pad) * S), int((y1 + pad) * S) + 1) for gx in range(int((x0 - pad) * S), int((x1 + pad) * S) + 1))
start = (108, 110.5)
stages = [
    ([], ['oub_bar', 'rest_oubliette']),
    (['m4_tremor'], ['oub_bar', 'rest_oubliette']),
    (['m4_tremor', 'oub_unbarred'], ['warden_key', 'guard_katana']),
    (['m4_tremor', 'oub_unbarred', 'guard_door_unlocked'], ['guard_katana', 'pell_winch']),
    (['m4_tremor', 'oub_unbarred', 'guard_door_unlocked', 'pell_free'], ['pell_freed', 'roll_hint', 'bridge_winch']),
    (['m4_tremor', 'oub_unbarred', 'guard_door_unlocked', 'pell_free', 'pit_bridge_down'], ['lantern_line', 'skull_open', 'rest_ossuary']),
    (['m4_tremor', 'oub_unbarred', 'guard_door_unlocked', 'pell_free', 'pit_bridge_down', 'skull_gate_open'], ['rest_ossuary', 'socket1_set']),
]
for fl, targets in stages:
    seen = reach(set(fl), start)
    print('+'.join(fl) or '(đầu)', '->', {t: can(seen, t) for t in targets})
# bar có với được từ TRONG ô giam (trước rung chấn) không: khoảng cách tâm ô đi được gần nhất tới hình chữ nhật bar
seen0 = reach(set(), start); (bx0, by0, bx1, by1) = named['oub_bar'][0]; rng = float(named['oub_bar'][1].get('range', 1))
best = min(max(bx0 - (gx + .5) / S, 0, (gx + .5) / S - bx1) ** 2 + max(by0 - (gy + .5) / S, 0, (gy + .5) / S - by1) ** 2 for gx, gy in seen0) ** .5
print('bar: khoảng cách nhỏ nhất từ trong ô giam =', round(best, 2), 'ô; tầm =', rng)
