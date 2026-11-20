# Dựng Last_Guard_Charge_N.png từ sheet 4x2 (ô 156) của PixelLab; màu nền xem trước không thuộc bảng màu sprite
# (đã kiểm: 36 màu sprite + 1 màu nền) nên khoá nền theo màu trên toàn ô, kể cả lỗ kín; căn chân như import_last_guard.py
import sys
from PIL import Image
from collections import deque
SRC, OUT = sys.argv[1], sys.argv[2]
CELL, FOOT, TOP, FW = 192, 36, 18, 156
im = Image.open(SRC).convert('RGBA')
bg = im.getpixel((0, im.height - 1))
frames = []
for i in range(8):
    cx, cy = (i % 4) * FW, TOP + (i // 4) * FW
    f = im.crop((cx, cy, cx + FW, cy + FW)).copy(); px = f.load()
    seen = set(); q = deque()
    for x in range(FW):
        q.append((x, 0)); q.append((x, FW - 1))
    for y in range(FW):
        q.append((0, y)); q.append((FW - 1, y))
    while q:
        x, y = q.popleft()
        if (x, y) in seen or not (0 <= x < FW and 0 <= y < FW): continue
        seen.add((x, y))
        if px[x, y][:3] != bg[:3]: continue
        px[x, y] = (0, 0, 0, 0)
        q.extend(((x+1, y), (x-1, y), (x, y+1), (x, y-1)))
    for y in range(FW):
        for x in range(FW):
            if px[x, y][:3] == bg[:3]: px[x, y] = (0, 0, 0, 0)
    left = sum(1 for p in f.getdata() if p[:3] == bg[:3] and p[3])
    frames.append(f); print('frame', i, 'bbox', f.getbbox(), 'bg-left', left)

def anchor(im):
    a = im.getchannel('A').load(); w, h = im.size
    for y in range(h - 1, -1, -1):
        run = best = 0; bx = 0; start = 0
        for x in range(w + 1):
            on = x < w and a[x, y] > 0
            if on:
                if run == 0: start = x
                run += 1
            else:
                if run > best: best, bx = run, start + run / 2.0
                run = 0
        if best >= 18: return bx, y
    return w / 2.0, h - 1

ax, ay = anchor(frames[0]); print('anchor', ax, ay)
strip = Image.new('RGBA', (CELL * 8, CELL), (0, 0, 0, 0))
for i, f in enumerate(frames):
    ox = int(round(CELL / 2 - ax)); oy = int(round(CELL - FOOT - ay))
    cell = Image.new('RGBA', (CELL, CELL), (0, 0, 0, 0)); cell.paste(f, (ox, oy), f)
    strip.paste(cell, (i * CELL, 0))
strip.save(OUT); print('saved', strip.size)
