# 20/11 (§350): đổi màu lửa phép (tím) của Hollow Champion + VFX + pha lê sang 4 màu pha lê. Chạy lại được.
from PIL import Image
import colorsys, os
R = '../../../Assets/'
COL = {'crimson': (0.005, 1.0), 'azure': (0.56, 1.0), 'verdant': (0.33, 0.95), 'gilded': (0.115, 1.35)}   # hue đích, nhân độ sáng
def purple(h, s, v): return 0.66 <= h <= 0.93 and s >= 0.22 and v >= 0.22
def shift(im, hue, vmul, test):
    im = im.convert('RGBA'); px = im.load()
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            if a < 10: continue
            h, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
            if not test(h, s, v): continue
            nr, ng, nb = colorsys.hsv_to_rgb(hue, min(1, s * 1.1), min(1, v * vmul))
            px[x, y] = (int(nr * 255), int(ng * 255), int(nb * 255), a)
    return im
# 1) Champion
CH = R + 'Enemies/Map4/Sprites/Hollow_Champion/'
for c, (hue, vm) in COL.items():
    os.makedirs(CH + c, exist_ok=True)
    for f in os.listdir(CH):
        if f.endswith('.png'): shift(Image.open(CH + f), hue, vm, purple).save(CH + c + '/' + f)
# 2) VFX lửa hồn / vòng nện
V = R + 'Resources/VFX/'
for base in ('duo_soulfire', 'duo_soul_slam'):
    for c, (hue, vm) in COL.items(): shift(Image.open(V + base + '.png'), hue, vm, purple).save(V + base + '_' + c + '.png')
# 3) pha lê: phần pha lê (xanh nhạt / trắng sáng) -> màu đích; đá xám giữ nguyên
def crystal(h, s, v): return (0.42 <= h <= 0.7 and s >= 0.08 and v >= 0.45) or v >= 0.88
P = V + 'props/'
def tint_crystal(src, hue, vm):
    im = Image.open(src).convert('RGBA'); px = im.load()
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            if a < 10: continue
            h, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
            if not crystal(h, s, v): continue
            nr, ng, nb = colorsys.hsv_to_rgb(hue, min(0.82, 0.3 + s * 1.0), min(0.95, v * 0.92 * vm))   # 20/11 (§352): dịu màu cho hợp tông crypt
            px[x, y] = (int(nr * 255), int(ng * 255), int(nb * 255), a)
    return im.crop(im.getbbox())
def spent(im):
    im = im.copy(); px = im.load()
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            l = int((r * 0.3 + g * 0.5 + b * 0.2) * 0.55)
            px[x, y] = (l, l, int(l * 1.08), a)
    return im
for c, (hue, vm) in COL.items():
    im = tint_crystal('crystal.png', hue, vm); im.save(P + 'crystal_pillar_' + c + '.png'); spent(im).save(P + 'crystal_pillar_' + c + '_spent.png')
    tint_crystal('cluster.png', hue, vm).save(P + 'crystal_cluster_' + c + '.png')
    tint_crystal('shard.png', hue, vm).save(P + 'crystal_shard_' + c + '.png')
# 4) đồ trang trí + bệ pháo (cắt sát hình)
for n, out in (('ped_d', 'engine_pedestal'), ('coins', 'coin_heap'), ('gear', 'wall_gear'), ('banner', 'lists_banner'), ('brazier', 'soul_brazier')):
    im = Image.open(n + '.png').convert('RGBA'); im.crop(im.getbbox()).save(P + out + '.png')
print('xong')
