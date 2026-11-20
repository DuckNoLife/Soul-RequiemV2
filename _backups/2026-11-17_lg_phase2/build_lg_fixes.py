# §292 (17/11): sửa động tác sai hướng của The Last Guard — ghép khung đúng từ các clip PixelLab vẽ lại.
# python3 build_lg_fixes.py <thư mục nguồn lg4_src> <thư mục ra>
# Nguồn = ảnh xem trước 1:1 của pixelart_workbench `join <clip> --as sheet --columns C` (dải nhãn trên cùng + lưới ô),
# màu nền xem trước KHÔNG thuộc bảng màu sprite (kiểm: số màu = bảng màu + 1) -> khoá nền theo màu.
# Căn chân như import_last_guard.py: ô 192, chân cách đáy 36, neo theo bóng đổ ở khung 0 của TỪNG clip nguồn
# (khung lấy từ nhiều clip khác nhau vẫn đứng cùng một điểm chân).
import sys, os
from PIL import Image

SRC, OUT = sys.argv[1], sys.argv[2]
CELL, FOOT = 192, 36
os.makedirs(OUT, exist_ok=True)

# tên nguồn: (ô rộng, ô cao, số cột, số khung, số màu bảng sprite)
SOURCES = {
    'aim_n':       (156, 156, 6, 9, 33),   # lg4_aim_n       a664b765 (pha 1, north)
    'guard_s':     (168, 168, 6, 8, 38),   # lg4_guard_s     99809eb3 (pha 1, south)
    'stuck_n':     (156, 160, 6, 9, 32),   # lg4_stuck_n     8f11ff09
    'stuck_s':     (168, 168, 6, 9, 46),   # lg4_stuck_s     ea05ff15
    'bash_n':      (156, 156, 6, 9, 35),   # lg4_bash_n      e4a19058
    'ls_thrust_s': (180, 180, 5, 9, 41),   # ls4_thrust_s    876bb1b3 (pha 2)
    'ls_thrust_n': (180, 180, 5, 9, 25),   # ls4_thrust_n    e0d7c518
    'ls_thrust_e': (180, 180, 5, 9, 18),   # ls4_thrust_e_b  7d59c45a (pixminimax)
}

# dải ra: tên file -> danh sách (nguồn, chỉ số khung). Lý do chọn khung ghi trong NOTES §292.
STRIPS = {
    'Last_Guard_Aim_N':      [('aim_n', i) for i in (0, 1, 2, 3, 4, 4, 4, 4, 4)],          # giáo dựng -> nghiêng chĩa tới (mũi nhô trên vai)
    'Last_Guard_Stuck_N':    [('stuck_n', i) for i in (1, 3, 4, 5, 6, 7)],                 # lao tới, giáo cắm sàn phía trước (thân che)
    'Last_Guard_Pull_N':     [('stuck_n', 7), ('stuck_n', 5), ('stuck_n', 3), ('aim_n', 4), ('aim_n', 2), ('aim_n', 0)],
    'Last_Guard_Bash_N':     [('bash_n', i) for i in (0, 1, 2, 3, 4, 5, 5, 4, 3)],         # khiên trước thân: gồng -> đẩy -> thu
    'Last_Guard_Idle_S':     [('guard_s', i) for i in (4, 5, 6, 7, 6, 5)],                 # giáo dựng tay phải (trước: chĩa ngang)
    'Last_Guard_Aim_S':      [('guard_s', i) for i in (7, 6, 5, 4, 3, 2, 1, 0, 0)],        # giáo dựng -> hạ chéo xuống về phía camera
    'Last_Guard_Stuck_S':    [('stuck_s', i) for i in (3, 4, 5, 6, 7, 8)],                 # giáo đâm chéo xuống sàn trước mũi chân
    'Last_Guard_Pull_S':     [('stuck_s', 8), ('stuck_s', 6), ('stuck_s', 4), ('stuck_s', 0), ('guard_s', 3), ('guard_s', 4)],
    'Last_Guard_LS_Thrust_S': [('ls_thrust_s', i) for i in (0, 1, 2, 2, 5, 6, 7, 7, 0)],   # đâm thẳng về phía camera (bỏ khung chĩa ngang 3,4,8); khung 0..3 rút, 4..5 đâm (khớp LastGuardSkills)
    'Last_Guard_LS_Thrust_N': [('ls_thrust_n', i) for i in (0, 1, 2, 2, 3, 3, 7, 7, 8)],   # đâm ra xa (bỏ khung chĩa ngang 4,5,6); khung 0..3 rút, 4..5 đâm
    'Last_Guard_LS_Thrust':   [('ls_thrust_e', i) for i in (0, 1, 2, 2, 3, 4, 5, 7, 8)],   # thân giữ nghiêng phải suốt đòn; khung 0..3 rút, 4..5 đâm
}


def load(name):
    w, h, cols, n, pal = SOURCES[name]
    im = Image.open(f'{SRC}/{name}.png').convert('RGBA')
    rows = (n + cols - 1) // cols
    top = im.height - rows * h                      # dải nhãn phía trên
    assert top >= 0, name
    bg = im.getpixel((im.width - 1, im.height - 1))
    body = im.crop((0, top, im.width, im.height))
    ncol = len(set(body.getdata()))
    assert ncol == pal + 1, f'{name}: {ncol} màu, mong {pal}+1 — nền trùng màu sprite?'
    frames = []
    for i in range(n):
        x, y = (i % cols) * w, top + (i // cols) * h
        f = im.crop((x, y, x + w, y + h)); px = f.load()
        for yy in range(h):
            for xx in range(w):
                if px[xx, yy][:3] == bg[:3]: px[xx, yy] = (0, 0, 0, 0)
        frames.append(f)
    return frames


def anchor(im):
    """Tâm bóng đổ dưới chân: hàng thấp nhất có dải điểm ảnh liền >= 18 px (giống import_last_guard.py)."""
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


cache = {}
for name in SOURCES:
    fr = load(name)
    cache[name] = (fr, anchor(fr[0]))
    print('nguồn', name, len(fr), 'neo', cache[name][1])

for out, picks in STRIPS.items():
    strip = Image.new('RGBA', (CELL * len(picks), CELL), (0, 0, 0, 0))
    for i, (src, idx) in enumerate(picks):
        fr, (ax, ay) = cache[src]
        f = fr[idx]
        ox = int(round(CELL / 2 - ax)); oy = int(round(CELL - FOOT - ay))
        cell = Image.new('RGBA', (CELL, CELL), (0, 0, 0, 0)); cell.paste(f, (ox, oy), f)
        strip.paste(cell, (i * CELL, 0))
    strip.save(f'{OUT}/{out}.png')
    print('ra', out, len(picks))
