# Bảng ảnh: mỗi loại prop có collider -> ảnh + khung collider hiện tại (đỏ), để chọn vật tròn (§303).
import sys; sys.path.insert(0, '.')
from tmxlib import objects, owner, PATHS
from analyze import MAPS, GP
from PIL import Image, ImageDraw
seen = {}
for k in MAPS:
    props, cols = objects(GP / MAPS[k])
    for c in cols:
        p = owner(c, props)
        if not p: continue
        key = p['img']
        if key in seen: continue
        seen[key] = (k, p, c)
S = 2; cells = []
for key, (k, p, c) in seen.items():
    try: im = Image.open(PATHS[key]).convert('RGBA')
    except Exception: continue
    im = im.resize((int(p['w']), int(p['h'])), Image.NEAREST) if im.size != (int(p['w']), int(p['h'])) else im
    pad = 10; cv = Image.new('RGBA', (im.width + 2 * pad, im.height + 2 * pad + 12), (60, 60, 70, 255)); cv.alpha_composite(im, (pad, pad))
    cv = cv.resize((cv.width * S, cv.height * S), Image.NEAREST); d = ImageDraw.Draw(cv)
    x0 = (c['x'] - p['x'] + pad) * S; y0 = (c['y'] - p['y'] + pad) * S
    (d.ellipse if c['shape'] == 'ell' else d.rectangle)([x0, y0, x0 + c['w'] * S, y0 + c['h'] * S], outline=(255, 40, 40, 255), width=2)
    d.text((4, cv.height - 30), '%s %s' % (k, key[:28]), fill=(255, 255, 0, 255))
    cells.append(cv)
W = 2000; x = y = rowh = 0; pos = []
for cv in cells:
    if x + cv.width > W: x = 0; y += rowh + 6; rowh = 0
    pos.append((x, y)); x += cv.width + 6; rowh = max(rowh, cv.height)
out = Image.new('RGBA', (W, y + rowh), (30, 30, 30, 255))
for cv, pp in zip(cells, pos): out.alpha_composite(cv, pp)
out.save('sheet.png'); print(out.size, len(cells))
