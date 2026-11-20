import sys, os
from PIL import Image, ImageDraw
import m4_setup as S, decor2 as D
p = S.write('_m4v3'); os.environ['SR_CLEAN'] = '1'
import render_tmx
render_tmx.main(p, 0, 0, S.F.W, S.F.H, 'ref/v3_full.png', 1)
im = Image.open('ref/v3_full.png').convert('RGBA'); ov = Image.new('RGBA', im.size, (0, 0, 0, 0)); d = ImageDraw.Draw(ov)
COL = {'roller': (255, 160, 40, 40), 'blades': (255, 60, 60, 80), 'spikes': (200, 200, 255, 80), 'saw': (255, 60, 60, 80), 'launcher': (255, 220, 60, 120),
       'fire': (255, 100, 30, 120), 'ice': (120, 200, 255, 120), 'plate': (255, 255, 255, 90), 'pit': (0, 0, 0, 120)}
for kd, x, y, w, h in D.TRAPS:
    c = COL.get(kd, (255, 0, 255, 90)); d.rectangle((x * 32, y * 32, (x + w) * 32 - 1, (y + h) * 32 - 1), fill=c, outline=c[:3] + (255,))
im.alpha_composite(ov); im.save('ref/v3_traps.png'); im.convert('RGB').resize((im.width * 2 // 5, im.height * 2 // 5), Image.LANCZOS).save('ref/v3_small.png')
print('ok', len(D.TRAPS))
