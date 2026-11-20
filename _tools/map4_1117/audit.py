# Map 4 — KIỂM TRA VỊ TRÍ MỌI ĐỒ VẬT theo loại ô bên dưới (chạy sau decor). In lỗi theo khu.
#  Loại ô: F = sàn đi được (nền), P = mặt bệ đi được, R = hàng lan can, W = mặt tường (gạch đứng), X = đá/đỉnh tường/hố (không đứng được)
#  Quy tắc: props / props front / ground decals: chân ở F hoặc P (không R/W/X), cả bề ngang chân (trừ 15%% mép) cũng vậy;
#           wall props: chân ở W; ánh sáng không xét. Khối chặn (obstacle) không được cắt đường đi chính (check lại reach).
import sys, re; sys.path.insert(0, '.')
import m4_setup as S
import decor2 as D
import logic as LG                                   # §304: object logic (nến điểm nghỉ cũng được kiểm)
import traps                                         # §305: bẫy (để write_real/audit cùng một bộ object)
import story                                         # §306
import enemies                                       # §307
F = S.F; k, o = F.k, F.o; W, H = F.W, F.H
def op_plat(x, y): M = o.Mplat; return 0 <= x < W and 0 <= y < H and M[y][x] and M[y][x + 1] and M[y + 1][x] and M[y + 1][x + 1]
rail = {(x, y) for y in range(H) for x in range(W) if k.L['rails'][y][x]}
CLS = {}
for y in range(H):
    for x in range(W):
        c = 'X'
        if (x, y) in k.area:
            if (x, y) in rail: c = 'R'
            elif (x, y) in k.plat_top and (x, y) not in k.plat_block and (x, y) in k.floor: c = 'P'
            elif (x, y) in k.floor and (x, y) not in k.blocked: c = 'F'
            elif k.L['rock'][y][x] or k.L['plat'][y][x] or k.L['pil'][y][x]:
                below = [(x, y + d) for d in (1, 2, 3)]
                c = 'W' if any(b in k.floor for b in below) and not ((x, y) in k.floor and (x, y) in k.plat_top) else 'X'
                if (x, y) in k.walk: c = 'F'
        elif (x, y) in o.area:
            if (x, y) in o.cell_block:
                c = 'W'
            elif op_plat(x, y): c = 'F'
            else:
                ow = k.L['op_walls'][y][x]
                c = 'W' if ow and any(op_plat(x, y + d) for d in (1, 2, 3)) else 'X'
        CLS[(x, y)] = c
ZONES = [('Hall', 38, 0, 76, 19), ('Walk', 46, 19, 66, 44), ('Phòng thư', 8, 24, 30, 42), ('Kho', 82, 24, 104, 42),
         ('Scriptorium', 2, 44, 38, 63), ('Ossuary', 36, 42, 76, 63), ('Reliquary', 74, 44, 110, 63),
         ('Cổng đầu lâu', 40, 63, 70, 80), ('Sân lưỡi xoay', 30, 80, 68, 104), ('Rolling Hall', 66, 74, 77, 120),
         ('Xà lim', 74, 86, 114, 106), ('Tra tấn/Gác/Oubliette', 74, 106, 114, 124), ('Gờ xa', 0, 64, 32, 76)]
def zone(x, y):
    for n, x0, y0, x1, y1 in ZONES:
        if x0 <= x < x1 and y0 <= y < y1: return n
    return '?'
errs = {}
for g in ('props', 'props front', 'ground decals', 'wall props'):
    for _, xml in D.O.G[g]:
        m = dict(re.findall(r'(\w+)="([^"]*)"', xml))
        if 'gid' not in m: continue
        x, y, w, h = float(m['x']), float(m['y']), float(m['width']), float(m['height'])
        name = m.get('name', '')
        fy = int((y - 3) // 32)
        xs = sorted({int((x + w * f) // 32) for f in (0.2, 0.5, 0.8)})
        cls = [CLS.get((cx, fy), 'X') for cx in xs]
        gid = int(m['gid']) & 0x0fffffff
        if gid - D.O.ts['op'].firstgid in range(540, 553): ok = all(c == 'X' for c in cls)     # giáo cắm dưới hố: phải ở hố
        else: ok = all(c in 'FP' for c in cls) if g != 'wall props' else all(c == 'W' for c in cls)
        if not ok:
            zn = zone(int((x + w / 2) // 32), fy)
            errs.setdefault(zn, []).append('%-12s %-6s chân (%.1f,%d) ô=%s %s' % (g, name[:14], (x + w / 2) / 32, fy, ''.join(cls), 'gid%s' % m['gid']))
# §304: object LOGIC (điểm nghỉ, chỗ tỉnh dậy, vật nhặt, tời, xích) — tâm phải trên ô đứng được, không trùng khối chặn đồ vật
OBST = []
for _, s in D.O.G['obstacle colliders']:
    m = re.search(r'x="([\d.-]+)" y="([\d.-]+)" width="([\d.]+)" height="([\d.]+)"', s)
    if m: OBST.append(tuple(float(v) / 32 for v in m.groups()))
for g in ('triggers', 'mechanics'):
    for _, s in D.O.G[g]:
        t = re.search(r'type="(\w+)"', s); nm_ = re.search(r'name="([^"]+)"', s)
        if not t or t.group(1) not in ('checkpoint', 'player_start', 'pickup', 'lever', 'breakable'): continue
        m = re.search(r'x="([\d.-]+)" y="([\d.-]+)" width="([\d.]+)" height="([\d.]+)"', s)
        x, y, w, h = (float(v) / 32 for v in m.groups()); cx, cy = x + w / 2, y + h / 2
        bad = CLS[(int(cx), int(cy))] not in 'FP' and t.group(1) not in ('lever', 'breakable')
        hit = [o_ for o_ in OBST if o_[0] < cx < o_[0] + o_[2] and o_[1] < cy < o_[1] + o_[3]] if t.group(1) in ('checkpoint', 'player_start') else []
        if bad or hit:
            errs.setdefault('LOGIC', []).append('%-12s %-18s tâm (%.1f,%.1f) ô=%s %s' % (t.group(1), nm_.group(1), cx, cy, CLS[(int(cx), int(cy))], 'đè khối chặn' if hit else ''))
tot = 0
for zn, l in errs.items():
    print('== %s: %d lỗi' % (zn, len(l))); tot += len(l)
    for e in l[:40]: print('   ', e)
print('TỔNG LỖI ĐỒ VẬT:', tot)
if __name__ == '__main__' and '--map' in sys.argv:
    for n, x0, y0, x1, y1 in ZONES:
        if n == sys.argv[-1]:
            for yy in range(y0, y1): print('%3d ' % yy + ''.join(CLS[(xx, yy)] for xx in range(x0, x1)))
