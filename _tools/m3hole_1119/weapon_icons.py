# 19/11 (§342): icon túi đồ weapon_<id>.png (64x64) cho các katana còn thiếu — cắt THANH KIẾM (nửa dưới) từ tranh trưng bày
# Weapons/Showcase/<Tên>.png (256x96), lật cho chuôi sang trái, xoay chéo ~28° (mũi chỉ lên phải) như icon gốc, thu nhỏ kiểu pixel (lấy mẫu gần nhất
# trên ảnh đã lọc màu chủ đạo), thêm viền tối 1 px. Không ghi đè icon đã có.  python3 weapon_icons.py <Resources> [--force-test]
import sys, os
from PIL import Image
R = sys.argv[1]
OUT = os.path.join(R, 'UI/Requiem')
ids = {'katana_absolution': 'Katana_Absolution', 'katana_accord': 'Katana_Accord', 'katana_eventide': 'Katana_Eventide',
       'katana_ironcrown': 'Katana_Ironcrown', 'katana_namesake': 'Katana_Namesake', 'katana_oathbreaker': 'Katana_Oathbreaker'}
test = '--test' in sys.argv
if test: ids = {'test_crimson': 'Katana_Crimson'}
def make(src):
    im = Image.open(src).convert('RGBA')
    blade = im.crop((0, 48, 256, 96))
    bb = blade.getbbox(); blade = blade.crop(bb)
    blade = blade.transpose(Image.FLIP_LEFT_RIGHT)
    rot = blade.rotate(28, resample=Image.NEAREST, expand=True)
    rot = rot.crop(rot.getbbox())
    k = 61.0 / rot.width
    small = rot.resize((max(1, round(rot.width * k)), max(1, round(rot.height * k))), Image.BOX)
    # alpha cứng (pixel art)
    px = small.load()
    for y in range(small.height):
        for x in range(small.width):
            r, g, b, a = px[x, y]
            px[x, y] = (r, g, b, 255) if a >= 110 else (0, 0, 0, 0)
    canvas = Image.new('RGBA', (64, 64), (0, 0, 0, 0))
    ox, oy = 2 + (61 - small.width) // 2, 20 + (34 - small.height) // 2
    canvas.alpha_composite(small, (ox, oy))
    # viền tối 1 px
    out = canvas.copy(); cp = canvas.load(); op = out.load()
    for y in range(64):
        for x in range(64):
            if cp[x, y][3]: continue
            if any(0 <= x + dx < 64 and 0 <= y + dy < 64 and cp[x + dx, y + dy][3] for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                op[x, y] = (24, 14, 18, 255)
    return out
for wid, name in ids.items():
    dst = os.path.join(OUT, 'weapon_%s.png' % wid) if not test else os.path.join(sys.argv[2], 'test_crimson.png')
    if os.path.exists(dst) and not test: print('giữ', dst); continue
    make(os.path.join(R, 'Weapons/Showcase/%s.png' % name)).save(dst); print('ghi', dst)
