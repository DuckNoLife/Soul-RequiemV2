# 17/11 (§284): xuất bộ hình The Last Guard v3 từ ZIP PixelLab (a3d65a38 trạng thái Idle + Last_Stand) thành dải ngang ô vuông.
# python3 import_last_guard.py <thư mục đã giải nén> <thư mục ra>
# Ra: Last_Guard_<State>[_S|_N].png (pha 1), Last_Guard_LS_<State>[_S|_N].png (pha 2), Last_Guard_meta.txt, Last_Guard_LS_meta.txt,
#     Last_Guard_ShieldDropped.png. Mọi khung dời về cùng một điểm chân (tâm bóng đổ) -> ô CELL, chân ở (CELL/2, CELL-FOOT).
import sys, os, glob
from PIL import Image
SRC, OUT = sys.argv[1], sys.argv[2]
CELL, FOOT = 192, 36
os.makedirs(OUT, exist_ok=True)
DIRS = (('east', ''), ('south', '_S'), ('north', '_N'))

# (state, nguồn trạng thái, tên clip PixelLab, chỉ số khung hoặc None = tất cả)
P1 = [('Idle', 'Idle', 'lg3_guard', None), ('Move', 'Idle', 'lg3_step', None),
      ('Aim', 'Idle', 'lg3_thrust_v3', None),                       # giáo hạ dần về ngang (báo trước)
      ('Stuck', 'Idle', 'lg3_thrust_v3', [8, 6, 4, 2, 1, 0]),        # hụt: mũi giáo cắm chéo xuống sàn, giữ khung cuối
      ('Pull', 'Idle', 'lg3_thrust_v3', [0, 1, 3, 5, 7, 8]),         # rút giáo lên lại
      ('Charge', 'Idle', 'lg3_charge', None), ('Bash', 'Idle', 'lg3_bash_v3', None),
      ('Dazed', 'Idle', 'lg3_dazed', None), ('Stagger', 'Idle', 'lg_stagger', [0, 1, 2, 3, 4]), ('Death', 'Idle', 'lg_death', None)]
P2 = [('Idle', 'Last_Stand', 'ls_guard', None), ('Move', 'Last_Stand', 'ls_walk', None),
      ('Thrust', 'Last_Stand', 'ls_thrust_v3', None), ('Whirl', 'Last_Stand', 'ls_whirl', None),
      ('Leap', 'Last_Stand', 'ls_leap', None), ('Death', 'Last_Stand', 'ls_death', None)]

SKIP = {('Bash', 'north')}   # bản bash nhìn lên vẽ sai phối cảnh (mặt khiên quay ra camera) -> dùng bản ngang

def anchor(im):
    """Tâm bóng đổ dưới chân: hàng thấp nhất có một dải điểm ảnh liền >= 18 px (mũi giáo chỉ là nét mảnh)."""
    a = im.getchannel('A').load(); w, h = im.size
    for y in range(h - 1, -1, -1):
        run = best = 0; bx = 0; start = 0
        for x in range(w + 1):
            on = x < w and a[x, y] > 0
            if on:
                if run == 0: start = x
                run += 1
            else:
                if run > best: best, bx = run, start + run / 2.0
                run = 0
        if best >= 18: return bx, y
    return w / 2.0, h - 1

def frames(state, clip, d):
    fs = sorted(glob.glob(f'{SRC}/{state}/animations/{clip}/{d}/frame_*.png'))
    return [Image.open(f).convert('RGBA') for f in fs]

def build(prefix, table):
    made = []
    for name, st, clip, idx in table:
        for d, suf in DIRS:
            if (name, d) in SKIP: continue
            fr = frames(st, clip, d)
            if not fr: continue
            ax, ay = anchor(fr[0])
            pick = [fr[i] for i in idx if i < len(fr)] if idx else fr
            strip = Image.new('RGBA', (CELL * len(pick), CELL), (0, 0, 0, 0))
            for i, f in enumerate(pick):
                ox = int(round(CELL / 2 - ax)); oy = int(round(CELL - FOOT - ay))
                cell = Image.new('RGBA', (CELL, CELL), (0, 0, 0, 0)); cell.paste(f, (ox, oy), f)   # cắt gọn trong ô, không tràn sang ô bên
                strip.paste(cell, (i * CELL, 0))
            p = f'{OUT}/{prefix}_{name}{suf}.png'; strip.save(p); made.append((os.path.basename(p), len(pick)))
    open(f'{OUT}/{prefix}_meta.txt', 'w').write('%d %d %.4f %.4f %.4f\n' % (CELL, CELL, FOOT / CELL, FOOT / CELL, FOOT / CELL))
    return made

m1 = build('Last_Guard', P1); m2 = build('Last_Guard_LS', P2)
for m in m1 + m2: print(*m)

# khiên rơi: cắt khiên ở bản xoay south (pha 1), đặt nằm nghiêng trên sàn
rot = Image.open(f'{SRC}/Idle/rotations/south.png').convert('RGBA')
a = rot.getchannel('A'); w, h = rot.size
# khiên ở nửa phải ảnh (tay trái nhân vật): cột có điểm ảnh từ 62% chiều rộng trở đi
box = rot.crop((int(w * 0.62), 0, w, h)).getbbox()
sh = rot.crop((int(w * 0.62) + box[0], box[1], int(w * 0.62) + box[2], box[3]))
lay = sh.rotate(78, expand=True, resample=Image.NEAREST)
lay = lay.resize((lay.width, max(8, int(lay.height * 0.6))), Image.NEAREST)
canvas = Image.new('RGBA', (lay.width + 8, lay.height + 6), (0, 0, 0, 0))
shadow = Image.new('RGBA', lay.size, (12, 10, 18, 110)); shadow.putalpha(lay.getchannel('A').point(lambda v: 110 if v else 0))
canvas.alpha_composite(shadow, (5, 5)); canvas.alpha_composite(lay, (2, 0))
canvas.save(f'{OUT}/Last_Guard_ShieldDropped.png'); print('shield', canvas.size)
