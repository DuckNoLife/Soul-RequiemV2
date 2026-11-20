# §305: vẽ 2 ẤN (22x22) cho Map 4 — Ấn Người Chép (bạc, bút lông) / Ấn Thánh Tích (vàng xỉn, vương miện 3 đỉnh). Ghi art/seal_*.png.
import math
from PIL import Image
def seal(path, base, rim, dark, glyph):
    S = 22; im = Image.new('RGBA', (S, S), (0, 0, 0, 0)); px = im.load(); c = (S - 1) / 2
    for y in range(S):
        for x in range(S):
            d = math.hypot(x - c, y - c)
            if d <= 10.2: px[x, y] = (dark if d > 9.2 else (rim if d > 7.6 and x + y < S else base)) + (255,)
    for a in range(0, 360, 8):
        px[round(c + 6.6 * math.cos(math.radians(a))), round(c + 6.6 * math.sin(math.radians(a)))] = dark + (255,)
    for (x, y) in glyph: px[x, y] = dark + (255,)
    im.save(path)
quill = [(13, 5), (12, 6), (12, 7), (11, 8), (11, 9), (10, 10), (10, 11), (9, 12), (9, 13), (8, 14), (8, 15),      # cán
         (11, 6), (10, 7), (10, 8), (9, 9), (9, 10), (8, 11), (13, 7), (13, 8), (12, 9), (12, 10), (11, 11)]          # lông
crown = [(x, y) for y in (11, 12, 13) for x in range(7, 15)] + [(x, 10) for x in (7, 8, 10, 11, 13, 14)] + \
        [(x, y) for y in (8, 9) for x in (7, 10, 11, 14)] + [(10, 7), (11, 7)]
seal('art/seal_scribe.png', (196, 200, 214), (232, 236, 244), (46, 52, 74), quill)
seal('art/seal_reliquary.png', (184, 148, 70), (226, 196, 112), (70, 46, 20), crown)
if __name__ == '__main__':
    c = Image.new('RGBA', (60, 30), (40, 40, 50, 255))
    c.alpha_composite(Image.open('art/seal_scribe.png'), (4, 4)); c.alpha_composite(Image.open('art/seal_reliquary.png'), (32, 4))
    c.resize((360, 180), Image.NEAREST).save('ref/_seals.png')
