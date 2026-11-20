# 20/11 (§353): 4 KỆ PHA LÊ (thay cột pha lê) từ ảnh PixelLab shelf_src.png:
#   đá xám -> tông đá teal của Crypt, khung đồng giữ nguyên, pha lê tím -> màu nguyên tố,
#   đĩa khắc ở chân kệ -> vẽ ký hiệu nguyên tố (lửa / bông tuyết / lá / mặt trời) sáng màu pha lê.
#   Ra: crystal_shelf_<c>.png (sáng) + crystal_shelf_<c>_spent.png (cạn: pha lê xám, ký hiệu tắt).
import colorsys, sys, os
from PIL import Image

SRC, OUT = sys.argv[1], sys.argv[2]
os.makedirs(OUT, exist_ok=True)
STONE = [(4, 16, 28), (5, 27, 46), (9, 36, 55), (17, 44, 64), (24, 55, 76), (31, 57, 75), (35, 66, 84),
         (38, 71, 92), (47, 77, 104), (66, 99, 122), (92, 126, 146)]
COL = {  # hue, sat, (sáng nhất, giữa, lõi) của ký hiệu
    'crimson': (0.005, 0.85, (255, 120, 90), (214, 52, 40), (255, 236, 200)),
    'azure':   (0.56, 0.80, (150, 220, 255), (52, 140, 220), (240, 252, 255)),
    'verdant': (0.33, 0.78, (160, 240, 120), (60, 160, 60), (236, 255, 210)),
    'gilded':  (0.14, 0.62, (255, 220, 110), (210, 150, 40), (255, 250, 220)),
}
GLYPH = {
    'crimson': ["....#....", "....##...", "...#o#...", "..#oo#.#.", "..#ooo##.", ".#oo*oo#.", ".#o***o#.", "..#***#..", "...###..."],
    'azure':   ["....#....", ".#..#..#.", "..#.#.#..", "...#o#...", "####*####", "...#o#...", "..#.#.#..", ".#..#..#.", "....#...."],
    'verdant': ["......###", "....##oo#", "...#oo*o#", "..#oo*oo#", "..#o*oo#.", ".#o*oo#..", ".#*oo#...", ".###.....", "#........"],
    'gilded':  ["....#....", ".#..#..#.", "..#####..", "..#ooo#..", "###o*o###", "..#ooo#..", "..#####..", ".#..#..#.", "....#...."],
}
PLAQUE = (31.5, 66)       # tâm đĩa khắc (toạ độ ảnh gốc 64x88)


def lum(r, g, b): return 0.3 * r + 0.55 * g + 0.15 * b


def purple(r, g, b):
    h, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
    return 0.70 <= h <= 0.95 and s >= 0.18 and v >= 0.25


def kind(r, g, b, near_purple):
    h, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
    if purple(r, g, b): return 'crystal'
    if v >= 0.86 and s < 0.12 and near_purple: return 'crystal'   # đốm sáng trắng trên pha lê (không ăn đốm sáng của khung đồng)
    if 0.06 <= h <= 0.18 and s >= 0.35 and v >= 0.2: return 'brass'
    return 'stone'


def build(c, spent, emissive=False):
    im = Image.open(SRC).convert('RGBA')
    em = Image.new('RGBA', im.size, (0, 0, 0, 0)); ep = em.load()
    px = im.load()
    src = im.copy().load()
    hue, sat, hi, mid, core = COL[c]
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            if a < 10: continue
            near = any(0 <= x + dx < im.width and 0 <= y + dy < im.height and src[x + dx, y + dy][3] > 10 and purple(*src[x + dx, y + dy][:3])
                       for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
            k = kind(r, g, b, near)
            if k == 'stone':
                t = min(1.0, lum(r, g, b) / 120.0)
                px[x, y] = STONE[min(len(STONE) - 1, int(round(t * (len(STONE) - 1))))] + (a,)
            elif k == 'crystal':
                _, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
                if spent:
                    l = int(lum(r, g, b) * 0.42)
                    px[x, y] = (l, int(l * 1.1), int(l * 1.25), a)
                else:
                    nr, ng, nb = colorsys.hsv_to_rgb(hue, min(0.9, 0.25 + s * sat), min(1.0, v * (1.2 if c == 'gilded' else 1.05)))
                    px[x, y] = (int(nr * 255), int(ng * 255), int(nb * 255), a)
                    ep[x, y] = px[x, y]
            elif spent:   # đồng xỉn khi pha lê cạn
                px[x, y] = (int(r * 0.7), int(g * 0.7), int(b * 0.7), a)
    # ký hiệu nguyên tố trên đĩa khắc
    gl = GLYPH[c]
    ox, oy = int(PLAQUE[0] - 4), int(PLAQUE[1] - 4)
    for j, row in enumerate(gl):
        for i, ch in enumerate(row):
            if ch == '.': continue
            col = {'#': hi, 'o': mid, '*': core}[ch]
            if spent: col = tuple(int(v * 0.32) for v in mid)
            px[ox + i, oy + j] = col + (255,)
            ep[ox + i, oy + j] = col + (255,)
    bb = im.getbbox()
    return (em if emissive else im).crop(bb)


for c in COL:
    build(c, False).save(os.path.join(OUT, 'crystal_shelf_%s.png' % c))
    build(c, True).save(os.path.join(OUT, 'crystal_shelf_%s_spent.png' % c))
    build(c, False, True).save(os.path.join(OUT, 'crystal_shelf_%s_lit.png' % c))   # lớp tự sáng (pha lê + ký hiệu), vẽ Unlit đè lên
print('ok')
