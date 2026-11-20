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
    # §293 pha 2 hướng N/S: giáo DỰNG trước thân như hướng E (bản cũ vắt chéo, mũi chĩa ngang ra ngoài thân)
    'ls_guard_s':   (180, 180, 5, 8, 36),  # ls5_guard_s     69203962
    'ls_guard_n':   (180, 180, 5, 8, 20),  # ls5_guard_n     86b049d2
    'ls_walk_s':    (180, 196, 4, 8, 21),  # ls5_walk_s_pm   18ab1837 (pixminimax, start = guard_s khung 4)
    'ls_walk_n':    (180, 204, 4, 8, 17),  # ls5_walk_n_pm   b7df9af5 (pixminimax, start = guard_n khung 4)
    'ls_thrust_s2': (192, 192, 5, 9, 26),  # ls5_thrust_s    5eee19c8 (start = guard_s khung 4)
    'ls_thrust_n2': (208, 216, 4, 9, 21),  # ls5_thrust_n    5da00b4b (start = guard_n khung 4)
    'ls_leap_n_v3': (180, 208, 5, 10, 20), # ls5_leap_n      34ac6122 (v3: khung đáp 7..9 đúng cỡ nhưng giáo chĩa ngang -> xoá phần thò ra)
}
# nguồn pixminimax không có bóng đổ chuẩn ở khung 0 -> căn khớp điểm ảnh với khung mở đầu đã dùng để tạo nó
REF = {'ls_walk_s': ('ls_guard_s', 4), 'ls_walk_n': ('ls_guard_n', 4)}

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
    # §293: Thrust S/N làm lại từ dáng thủ giáo dựng (bản §292 gồng bằng dáng vắt chéo cũ, mũi chĩa ngang)
    'Last_Guard_LS_Thrust_S': [('ls_thrust_s2', i) for i in (0, 1, 1, 5, 6, 6, 7, 8, 0)],   # dựng -> hạ chéo về camera -> đâm xuống trước mũi chân (bỏ khung ngang 2,3,4); 0..3 rút, 4..5 đâm
    'Last_Guard_LS_Thrust_N': [('ls_thrust_n2', i) for i in (0, 1, 2, 2, 3, 3, 8, 2, 0)],  # dựng -> nghiêng tới -> đâm ra xa (giáo khuất trước thân; bỏ khung ngang 4..7)
    'Last_Guard_LS_Idle_S':   [('ls_guard_s', i) for i in (3, 4, 5, 6, 7, 6, 5, 4)],       # giáo dựng trước ngực, hai tay
    'Last_Guard_LS_Idle_N':   [('ls_guard_n', i) for i in (2, 3, 4, 5, 6, 7, 6, 5)],       # giáo dựng trước thân, chỉ lộ mũi trên mũ
    'Last_Guard_LS_Move_S':   [('ls_walk_s', i) for i in range(8)],
    'Last_Guard_LS_Move_N':   [('ls_walk_n', i) for i in range(8)],
    # Leap_N: 0..2 khom lấy đà (dáng khom đúng cỡ của v3, đảo tay cầm: chuôi lên), 3..7 nhảy lên giữ giáo dựng mũi xuống trước thân, 8..9 đáp khom cắm giáo TRƯỚC thân
    # (bản cũ: mũi giáo thò dưới chân = đâm ra sau lưng). Giữ đúng cỡ người suốt đòn (bản pixminimax thu nhỏ lúc bay).
    'Last_Guard_LS_Leap_N':   [('ls_guard_n', 4), ('ls_leap_n_v3', 7, 'clean_butt'), ('ls_leap_n_v3', 7, 'clean_butt'),
                               ('ls_guard_n', 4, 'lift', 8, 24), ('ls_guard_n', 4, 'lift', 18, 20), ('ls_guard_n', 4, 'lift', 24, 17),
                               ('ls_guard_n', 4, 'lift', 18, 20), ('ls_guard_n', 4, 'lift', 6, 24),
                               ('ls_leap_n_v3', 7, 'clean_butt'), ('ls_leap_n_v3', 8, 'clean_butt')],
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


import numpy as np


def offset_to(src, ref):
    """(dx, dy) sao cho src(x+dx, y+dy) ~ ref(x, y): đếm điểm ảnh trùng màu, thử lệch ±30."""
    a = np.asarray(src, dtype=np.int32); b = np.asarray(ref, dtype=np.int32)
    H = max(a.shape[0], b.shape[0]) + 64; W = max(a.shape[1], b.shape[1]) + 64
    def pad(m):
        z = np.zeros((H, W, 4), np.int32); z[32:32 + m.shape[0], 32:32 + m.shape[1]] = m; return z
    A, B = pad(a), pad(b)
    ka = (A[..., 3] > 0); kb = (B[..., 3] > 0)
    ca = (A[..., 0] << 16) | (A[..., 1] << 8) | A[..., 2]; cb = (B[..., 0] << 16) | (B[..., 1] << 8) | B[..., 2]
    best = (-1, 0, 0)
    for dy in range(-30, 31):
        for dx in range(-30, 31):
            sa = np.roll(np.roll(ca, -dy, 0), -dx, 1); ma = np.roll(np.roll(ka, -dy, 0), -dx, 1)
            n = int(np.count_nonzero(ma & kb & (sa == cb)))
            if n > best[0]: best = (n, dx, dy)
    return best[1], best[2], best[0]


SHAFT = ('#150a20', '#4d494f', '#2d2730', '#5a575b')   # viền, sáng, tối, đầu chuôi


def hexc(h): return tuple(int(h[i:i + 2], 16) for i in (1, 3, 5)) + (255,)


def head_top(f):
    """Hàng đỉnh mũ (hàng đầu tiên có dải liền >= 12 px) + tâm x của dải đó."""
    a = f.getchannel('A').load(); w, h = f.size
    for y in range(h):
        run = 0; st = 0
        for x in range(w + 1):
            on = x < w and a[x, y] > 0
            if on:
                if run == 0: st = x
                run += 1
            else:
                if run >= 12: return y, st + run // 2
                run = 0
    return 0, w // 2


def butt_up(f, cx=None):
    """Giáo cắm mũi xuống TRƯỚC thân (bị thân che): xoá mọi thứ trên đỉnh mũ, vẽ đoạn chuôi dựng lộ trên mũ (sau mũ)."""
    f = f.copy(); px = f.load(); w, h = f.size
    top, hx = head_top(f)
    xs = [x for y in range(top) for x in range(w) if px[x, y][3]]
    if cx is None: cx = int(round(sum(xs) / len(xs))) if xs else hx
    for y in range(top):
        for x in range(w): px[x, y] = (0, 0, 0, 0)
    o, l, d, k = (hexc(c) for c in SHAFT)
    for y in range(top - 16, top + 4):
        for x, c in ((cx - 2, o), (cx - 1, l), (cx, d), (cx + 1, o)):
            if 0 <= y < h and px[x, y][3] == 0: px[x, y] = c
    for x, c in ((cx - 2, o), (cx - 1, k), (cx, k), (cx + 1, o)):          # đầu chuôi
        px[x, top - 17] = o; px[x, top - 18] = o if x in (cx - 2, cx + 1) else k
    return f


def strip_side_spear(f, pad=2):
    """Xoá phần giáo chĩa NGANG thò ra hai bên thân: bề ngang thân lấy theo vai/áo choàng (nửa trên khối thân,
    nơi giáo không đi qua), mọi điểm ảnh ngoài khoảng đó (+pad) bị xoá ở mọi hàng."""
    f = f.copy(); px = f.load(); w, h = f.size
    top, _ = head_top(f); bb = f.getbbox()
    lo, hi = w, -1
    for y in range(top, top + int((bb[3] - top) * 0.45)):
        for x in range(w):
            if px[x, y][3]: lo = min(lo, x); hi = max(hi, x)
    for y in range(h):
        for x in range(w):
            if (x < lo - pad or x > hi + pad) and px[x, y][3]: px[x, y] = (0, 0, 0, 0)
    return f


def lift(f, ay, dy, shadow_w):
    """Thân nhảy lên dy px (cắt phần bóng đổ cũ dưới chân), bóng đổ mới ở mặt đất, hẹp dần khi lên cao."""
    w, h = f.size
    body = f.crop((0, 0, w, ay - 5))
    out = Image.new('RGBA', (w, h), (0, 0, 0, 0)); sp = out.load()
    _, cx = head_top(f)
    sc = hexc('#251a2b')
    for y in range(ay - 6, ay + 1):
        t = (y - (ay - 3)) / 3.5
        half = int(shadow_w * max(0.0, 1 - t * t) ** 0.5)
        for x in range(cx - half, cx + half + 1):
            if 0 <= x < w: sp[x, y] = sc
    out.alpha_composite(body, (0, -dy))
    return out


cache = {}
for name in SOURCES:
    fr = load(name)
    cache[name] = [fr, anchor(fr[0])]
for name, (rname, ridx) in REF.items():
    rfr, (rax, ray) = cache[rname]
    dx, dy, n = offset_to(cache[name][0][0], rfr[ridx])
    cache[name][1] = (rax + dx, ray + dy)
    print('căn', name, 'theo', rname, ridx, 'lệch', dx, dy, 'trùng', n)
for name in SOURCES:
    print('nguồn', name, len(cache[name][0]), 'neo', cache[name][1])


def frame_of(pick):
    """pick = (nguồn, khung) hoặc (nguồn, khung, thao_tác, tham số...)."""
    src, idx = pick[0], pick[1]
    fr, (ax, ay) = cache[src]
    f = fr[idx]
    op = pick[2] if len(pick) > 2 else None
    if op == 'butt': f = butt_up(f)
    elif op == 'clean_butt': f = butt_up(strip_side_spear(f))
    elif op == 'lift': f = lift(butt_up(f), int(ay), pick[3], pick[4])
    return f, ax, ay


for out, picks in STRIPS.items():
    strip = Image.new('RGBA', (CELL * len(picks), CELL), (0, 0, 0, 0))
    for i, pick in enumerate(picks):
        f, ax, ay = frame_of(pick)
        ox = int(round(CELL / 2 - ax)); oy = int(round(CELL - FOOT - ay))
        cell = Image.new('RGBA', (CELL, CELL), (0, 0, 0, 0)); cell.paste(f, (ox, oy), f)
        strip.paste(cell, (i * CELL, 0))
    strip.save(f'{OUT}/{out}.png')
    print('ra', out, len(picks))

# §293: Whirl S/N giữ nguyên (xoay giáo ngang là đúng khi quét vòng), chỉ thay KHUNG CUỐI (dáng vắt chéo cũ) bằng dáng thủ mới
# để chuyển về Idle không giật. Nguồn = dải cũ đã ở ô 192 (whirl_*_old.png, bản gốc trước §293).
for name, src, idx in (('Last_Guard_LS_Whirl_S', 'ls_guard_s', 4), ('Last_Guard_LS_Whirl_N', 'ls_guard_n', 4)):
    old = Image.open(f'{SRC}/{name[-7:].lower()}_old.png').convert('RGBA')   # whirl_s_old.png / whirl_n_old.png
    k = old.width // CELL
    f, ax, ay = frame_of((src, idx))
    cell = Image.new('RGBA', (CELL, CELL), (0, 0, 0, 0)); cell.paste(f, (int(round(CELL / 2 - ax)), int(round(CELL - FOOT - ay))), f)
    old.paste(Image.new('RGBA', (CELL, CELL), (0, 0, 0, 0)), ((k - 1) * CELL, 0))
    old.paste(cell, ((k - 1) * CELL, 0))
    old.save(f'{OUT}/{name}.png')
    print('ra', name, k, '(thay khung cuối)')
