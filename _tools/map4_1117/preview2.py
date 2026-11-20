# Xem trước khung v2 + cửa (chưa trang trí): python3 preview2.py
import os
from PIL import Image
import m4_setup as S, gen_frame2 as F, doors as D
cells = []
for (ax, top) in F.o.arch6s:
    x0 = ax - 1
    kind = dict(F.CELLS).get(x0, 'ren')
    art = {'open': 'op_cellgate1_open', 'shut': 'op_cellgate2', 'pell': 'op_cellgate1', 'ren': 'op_cellgate1'}[kind]
    cells.append(('gate_%d_%d' % (ax, top), art, ax, top, kind))
D.put_all(S.objs, S.AID, F.DOORS + cells)
p = S.write('_m4v2'); os.environ['SR_CLEAN'] = '1'
import render_tmx
render_tmx.main(p, 0, 0, F.W, F.H, 'ref/v2_full.png', 1)
im = Image.open('ref/v2_full.png'); im.convert('RGB').resize((im.width * 2 // 5, im.height * 2 // 5), Image.LANCZOS).save('ref/v2_small.png')
