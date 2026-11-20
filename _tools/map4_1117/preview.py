# Ghi bản xem trước + vẽ phủ bẫy (chỉ ảnh), cắt vùng: python3 preview.py <prison|crypt>
import sys, os
from PIL import Image, ImageDraw
import m4_setup as S
part = sys.argv[1] if len(sys.argv) > 1 else 'prison'
D = __import__('decor_' + part)
p = S.write('_m4_preview')
os.environ['SR_CLEAN'] = '1'
import render_tmx
x0, y0, x1, y1 = (0, 0, 120, 46) if part == 'prison' else (0, 44, 120, 104)
render_tmx.main(p, x0, y0, x1 - x0, y1 - y0, '/tmp/_m4p.png', 1)
im = Image.open('/tmp/_m4p.png').convert('RGBA'); ov = Image.new('RGBA', im.size, (0, 0, 0, 0)); d = ImageDraw.Draw(ov)
COL = {'roller': (255, 160, 40, 50), 'hatch': (255, 160, 40, 140), 'blades': (255, 60, 60, 90), 'spikes': (200, 200, 255, 90), 'saw': (255, 60, 60, 90),
       'launcher': (255, 220, 60, 140), 'fire': (255, 100, 30, 140), 'ice': (120, 200, 255, 140), 'plate': (255, 255, 255, 110), 'pit': (0, 0, 0, 120)}
for kind, x, y, w, h in getattr(D, 'TRAPS', []):
    d.rectangle(((x - x0) * 32, (y - y0) * 32, (x - x0 + w) * 32 - 1, (y - y0 + h) * 32 - 1), fill=COL.get(kind, (255, 0, 255, 90)), outline=COL.get(kind, (255, 0, 255, 255))[:3] + (255,))
im.alpha_composite(ov); im.save('ref/%s_full.png' % part); print(im.size)
