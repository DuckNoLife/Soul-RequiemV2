# Cắt vùng ảnh frame/decor có lưới + số ô: python3 view.py <png toàn map tỉ lệ 1> x0 y0 x1 y1 out [zoom]
import sys
from PIL import Image, ImageDraw, ImageFont
src, x0, y0, x1, y1, out = sys.argv[1], *map(int, sys.argv[2:6]), sys.argv[6]
z = float(sys.argv[7]) if len(sys.argv) > 7 else 1
im = Image.open(src).convert('RGB').crop((x0 * 32, y0 * 32, x1 * 32, y1 * 32))
im = im.resize((int(im.width * z), int(im.height * z)), Image.NEAREST); d = ImageDraw.Draw(im); T = 32 * z
f = ImageFont.load_default(size=max(10, int(11 * z)))
for i in range(x1 - x0 + 1):
    d.line([(i * T, 0), (i * T, im.height)], fill=(255, 255, 255) if (x0 + i) % 5 == 0 else (90, 90, 90), width=1)
for j in range(y1 - y0 + 1):
    d.line([(0, j * T), (im.width, j * T)], fill=(255, 255, 255) if (y0 + j) % 5 == 0 else (90, 90, 90), width=1)
for i in range(0, x1 - x0, 1):
    if (x0 + i) % 5 == 0: d.text((i * T + 2, 2), str(x0 + i), fill=(255, 255, 0), font=f)
for j in range(0, y1 - y0, 1):
    if (y0 + j) % 5 == 0: d.text((2, j * T + 2), str(y0 + j), fill=(0, 255, 255), font=f)
im.save(out); print(im.size)
