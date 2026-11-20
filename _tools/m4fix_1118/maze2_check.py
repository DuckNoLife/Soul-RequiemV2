# kiểm mê cung p6 bản 2: BFS 1/4 ô từ lối vào; độ dài tới các đích; cầu chỉ tới được sau ô tời.
import sys, xml.etree.ElementTree as ET
from collections import deque
r = ET.parse(sys.argv[1]).getroot(); S = 4; R = 0.15
solid = []
for o in r.iter('object'):
    if o.get('type') in ('blocker', 'obstacle', 'bound'):
        x, y, w, h = (float(o.get(k, 0)) / 32 for k in ('x', 'y', 'width', 'height')); solid.append((x, y, x + w, y + h))
def free(gx, gy):
    cx, cy = (gx + .5) / S, (gy + .5) / S
    return not any(a - R < cx < c + R and b - R < cy < d + R for a, b, c, d in solid)
def bfs(start, ban=None):
    st = (int(start[0] * S), int(start[1] * S)); dist = {st: 0}; q = deque([st])
    while q:
        p = q.popleft()
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            n = (p[0] + dx, p[1] + dy)
            if 30 * S <= n[0] < 70 * S and 78 * S <= n[1] < 106 * S and n not in dist and free(*n) and not (ban and ban(n)): dist[n] = dist[p] + 1; q.append(n)
    return dist
d = bfs((66.5, 94.5))
T = {'tời cầu A3': (35.0, 99.6), 'cầu E0': (55.5, 83.6), 'bánh phanh F0': (63.5, 84.8), 'điểm nghỉ ẩn B1': (41, 90)}
for k, (x, y) in T.items(): print(k, round(d.get((int(x * S), int(y * S)), -4) / S, 1), 'ô')
# chặn ô A3 (tời) -> cầu không được tới
inA3 = lambda n: 34 * S <= n[0] < 37.5 * S and 98.5 * S <= n[1] < 103 * S
d2 = bfs((66.5, 94.5), inA3)
print('tới cầu mà KHÔNG qua ô tời:', (int(55.5 * S), int(83.6 * S)) in d2)
