# 20/11 (§353): đổi spritesheet lưới của PixelLab VFX (png + json) thành dải ngang 1 hàng cho Map4EnemyBuilder.Slice,
# in luôn dòng DuoVfx (rộng khung, pivot = anchor của json). Dùng: python3 vfx_to_strip.py <thư mục png+json> <thư mục ra>
import json, os, sys
from PIL import Image
src, out = sys.argv[1], sys.argv[2]
os.makedirs(out, exist_ok=True)
for f in sorted(os.listdir(src)):
    if not f.endswith('.json'): continue
    n = f[:-5]; j = json.load(open(os.path.join(src, f))); im = Image.open(os.path.join(src, n + '.png')).convert('RGBA')
    fr = j['frames']; w, h = fr[0]['frame']['w'], fr[0]['frame']['h']
    strip = Image.new('RGBA', (w * len(fr), h))
    for i, x in enumerate(fr):
        r = x['frame']; strip.paste(im.crop((r['x'], r['y'], r['x'] + w, r['y'] + h)), (i * w, 0))
    name = n.replace('-', '_'); strip.save(os.path.join(out, name + '.png'))
    a = j['meta']['anchor']
    print('("%s.png", %d, %gf, %gf),  # %d khung' % (name, w, round(a['x'] / w, 4), round(1 - a['y'] / h, 4), len(fr)))
