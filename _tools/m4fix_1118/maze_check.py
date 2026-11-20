# kiểm mê cung p6: đường ngắn nhất từ lối vào đông tới tời cầu (BFS 1/4 ô, blocker + obstacle chặn)
import os, sys, xml.etree.ElementTree as ET
from collections import deque
TMX = sys.argv[1]
r = ET.parse(TMX).getroot(); S = 4
solid = []
for o in r.iter('object'):
    if o.get('type') in ('blocker', 'obstacle', 'bound'):
        x, y, w, h = (float(o.get(k, 0)) / 32 for k in ('x', 'y', 'width', 'height')); solid.append((x, y, x + w, y + h))
X0, X1, Y0, Y1 = 30, 70, 78, 106
def free(gx, gy):
    cx, cy = gx / S + 0.5 / S, gy / S + 0.5 / S
    r_ = 0.15                                   # nửa bề rộng chân Ren ~0,15 ô
    return not any(a - r_ < cx < c + r_ and b - r_ < cy < d + r_ for a, b, c, d in solid)
start = (int(66.5 * S), int(94 * S)); goal = (int(35.1 * S), int(84 * S))
dist = {start: 0}; q = deque([start])
while q:
    p = q.popleft()
    pass
    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        n = (p[0] + dx, p[1] + dy)
        if X0 * S <= n[0] < X1 * S and Y0 * S <= n[1] < Y1 * S and n not in dist and free(*n): dist[n] = dist[p] + 1; q.append(n)
print('tới tời cầu:', goal in dist, 'độ dài ~', round(dist.get(goal, -1) / S, 1), 'ô')
bridge = (int(55 * S), int(83.5 * S)); print('tời -> cầu (từ lối vào):', round(dist.get(bridge, -1) / S, 1), 'ô')
