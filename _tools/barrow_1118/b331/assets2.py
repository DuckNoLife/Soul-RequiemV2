# 18/11 (§331): tài sản cho bản NỐI LIỀN của King's Barrow (bỏ portal):
#   - 3 dải hoạt ảnh cửa (Resources/VFX/barrow_door_{wood,gate,tomb}: khung 0 = ĐÓNG = đúng ảnh tile SR-barrow, khung cuối = MỞ = ảnh
#     edit_image_pro_flash của PixelLab cùng khung 128 px), pivot dưới-trái (GateAnimator khớp mép trên-trái hình trên map).
#   - ảnh tile `door_arch` (khung cửa đổ nát, mở toang) cho lối ra phòng đầu R0 (bị đá lấp, Earthshatter phá).
#   - đá lấp barrow_rubble_0 / barrow_rubble_broken: đổi màu đá đổ (nâu gỗ) sang ramp đá xanh tím của tường 3b.
import os, re, uuid, sys
from PIL import Image
sys.path.insert(0, '/home/claude/barrow')
T = '/tmp/claude-tool-results/949ad0d3-1107-5f85-bbec-3a629568c41e/'
U = '/mnt/user-data/uploads/Soul Requiem/'
O = '/mnt/user-data/outputs/b331/'
CEM = 'Assets/GamePackage/EPIC RPG World Pack - Cemetery V 1.6/'
def out(rel):
    p = O + rel; os.makedirs(os.path.dirname(p), exist_ok=True); return p
# vmeta: dùng lại hàm của assets.py (chỉ phần định nghĩa, không chạy lại xuất ảnh cũ)
src = open('/home/claude/barrow/assets.py').read()
ns = {}
exec(src[:src.index('# ---- kiểm khoá nền')], ns)
exec('TPL=open(U+"Assets/Resources/VFX/hit.png.meta").read()\n' + src[src.index('def vmeta'):src.index('sp=cells(')], ns)
vmeta = ns['vmeta']

DOORS = {'wood': ('mcp-pixellab-blob-1795006692613-gnyue4-84ac86d2.png', 'mcp-pixellab-blob-1795015057689-ifwfb7-9fcb97b8.png'),
         'gate': ('mcp-pixellab-blob-1795006904582-9tdurt-6130476a.png', 'mcp-pixellab-blob-1795015061118-b7jltg-9dc61ff9.png'),
         'tomb': ('mcp-pixellab-blob-1795006820045-9xm1dl-523ff7e2.png', 'mcp-pixellab-blob-1795015061822-2x283v-f8ea9c09.png')}

def frames_for(kind, C, O_):
    w, h = C.size; c = C.load(); o = O_.load()
    D = [[c[x, y] != o[x, y] for x in range(w)] for y in range(h)]
    xs = [x for y in range(h) for x in range(w) if D[y][x]]; ys = [y for y in range(h) for x in range(w) if D[y][x]]
    x0, x1, y0, y1 = min(xs), max(xs), min(ys), max(ys)
    n = {'wood': 7, 'gate': 8, 'tomb': 8}[kind]
    res = []
    for k in range(n):
        f = 1 - k / (n - 1)                   # phần cánh cửa còn che (1 = đóng)
        im = O_.copy(); p = im.load()
        if k == 0: res.append(C.copy()); continue
        if k == n - 1: res.append(O_.copy()); continue
        if kind == 'gate':                    # song sắt kéo thẳng lên, chỉ trong LÒNG cửa (vùng tối của ảnh mở) — bậu đá không trôi theo
            M = [[D[y][x] and sum(o[x, y][:3]) < 90 and o[x, y][3] > 0 for x in range(w)] for y in range(h)]
            ys2 = [y for y in range(h) for x in range(w) if M[y][x]]; my0, my1 = min(ys2), max(ys2)
            s = int(round((1 - f) * (my1 - my0 + 1)))
            for y in range(my0, my1 + 1):
                sy = y + s
                for x in range(x0, x1 + 1):
                    if not M[y][x] or sy > my1 or not M[sy][x]: continue
                    p[x, y] = c[x, sy]
        else:                                 # cánh cửa xoay vào trong: ép bề ngang về phía bản lề, tối dần
            halves = [(x0, x1, +1)] if kind == 'wood' else [(x0, (x0 + x1) // 2, +1), ((x0 + x1) // 2 + 1, x1, -1)]
            dark = 1 - 0.4 * (1 - f)
            for a, b, sgn in halves:
                wid = b - a + 1; nw = max(1, int(round(wid * f)))
                for i in range(nw):
                    x = a + i if sgn > 0 else b - i
                    si = int(i / f) if f > 0 else 0
                    sx = a + si if sgn > 0 else b - si
                    for y in range(y0, y1 + 1):
                        if not D[y][x] or not D[y][sx]: continue
                        r, g, bb, al = c[sx, y]
                        if al == 0: continue
                        p[x, y] = (int(r * dark), int(g * dark), int(bb * dark), al)
        res.append(im)
    return res

for kind, (cs, os_) in DOORS.items():
    C = Image.open(T + cs).convert('RGBA'); O_ = Image.open(T + os_).convert('RGBA')
    bb = C.getbbox(); assert bb == O_.getbbox(), (kind, bb, O_.getbbox())
    tile = Image.open(U + 'x').convert('RGBA') if False else None
    fr = [f.crop(bb) for f in frames_for(kind, C, O_)]
    w, h = fr[0].size
    # khung 0 phải trùng ảnh tile đang dùng trên map
    ref = Image.open('/mnt/user-data/outputs/barrow/' + CEM + 'Props/SR-barrow/door_%s.png' % kind).convert('RGBA')
    assert ref.size == (w, h) and ref.tobytes() == fr[0].tobytes(), kind
    s = Image.new('RGBA', (w * len(fr), h), (0, 0, 0, 0))
    for i, f in enumerate(fr): s.alpha_composite(f, (i * w, 0))
    name = 'barrow_door_' + kind
    s.save(out('Assets/Resources/VFX/%s.png' % name))
    open(out('Assets/Resources/VFX/%s.png.meta' % name), 'w', newline='\n').write(vmeta(name, len(fr), w, h, 0.0, 0.0))
    print(name, len(fr), (w, h))
    if kind == 'wood':                       # khung cửa trống (lối ra R0, đá lấp)
        fr[-1].save(out(CEM + 'Props/SR-barrow/door_arch.png'))
        TM = open('/mnt/user-data/outputs/barrow/' + CEM + 'Props/SR-barrow/door_wood.png.meta').read()
        open(out(CEM + 'Props/SR-barrow/door_arch.png.meta'), 'w', newline='\n').write(re.sub(r'guid: \w+', 'guid: ' + uuid.uuid4().hex, TM, count=1))

# ---- đá lấp: ramp màu từ phiến đá style-match 3b
slab = Image.open('/mnt/user-data/outputs/barrow/' + CEM + 'Props/SR-barrow/barrow_slab.png').convert('RGBA')
cols = sorted({px[:3] for px in slab.getdata() if px[3] > 200}, key=lambda c: 0.3 * c[0] + 0.59 * c[1] + 0.11 * c[2])
ramp = [cols[int(i * (len(cols) - 1) / 7)] for i in range(8)]
print('ramp', ramp)
def recolor(im):
    im = im.convert('RGBA'); p = im.load()
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = p[x, y]
            if a == 0: continue
            L = (0.3 * r + 0.59 * g + 0.11 * b) / 255.0
            q = ramp[min(7, int(L * 1.35 * 8))]
            p[x, y] = q + (a,)
    return im
PM = open(U + 'Assets/Resources/VFX/props/rubble_0.png.meta').read()
for a, b in (('rubble_0', 'barrow_rubble_0'), ('rubble_broken', 'barrow_rubble_broken')):
    recolor(Image.open(U + 'Assets/Resources/VFX/props/%s.png' % a)).save(out('Assets/Resources/VFX/props/%s.png' % b))
    open(out('Assets/Resources/VFX/props/%s.png.meta' % b), 'w', newline='\n').write(re.sub(r'guid: \w+', 'guid: ' + uuid.uuid4().hex, PM, count=1))
# xem trước
prev = []
for kind in DOORS:
    s = Image.open(O + 'Assets/Resources/VFX/barrow_door_%s.png' % kind); prev.append(s)
W = max(s.width for s in prev) + 200; H = sum(s.height + 6 for s in prev)
cv = Image.new('RGBA', (W, H), (30, 30, 38, 255)); y = 0
for s in prev: cv.alpha_composite(s, (0, y)); y += s.height + 6
r0 = Image.open(O + 'Assets/Resources/VFX/props/barrow_rubble_0.png'); cv.alpha_composite(r0, (W - 190, 10))
cv.alpha_composite(Image.open(O + 'Assets/Resources/VFX/props/barrow_rubble_broken.png'), (W - 95, 10))
cv.resize((W * 2, H * 2), Image.NEAREST).save('/tmp/doors_prev.png')
