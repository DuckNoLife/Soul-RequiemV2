# 19/11 (§333): tài sản cho King's Barrow bản gập (vòng quanh sân boss) + chiêu máu của The Red Field.
#   - Props/SR-barrow: kings_censer (lư hương King's Censer, PixelLab 2cced57f), coffin_lid (nắp quan tài khắc vương miện + chữ cổ, PixelLab 7817fcb2,
#     dòng chữ sinh ra là chữ cái vô nghĩa -> khắc lại thành ký tự cổ trừu tượng), ghost_ward (khung 0 của cổng lính hồn).
#   - Resources/VFX/barrow_ghost_ward: lính hồn đứng chắn lối (khung 0) -> tan dần lên trên -> trống (khung cuối) — gate_visual của lối ra Marching Hall.
#   - Resources/VFX/rf_blood_snare: vũng máu loang + tua máu trồi lên + sọ ma (PixelLab VFX 440dd970), 22 khung — chiêu đè mới (đảo phím).
#   - Resources/VFX/rf_confuse_ring: vòng sao máu quay trên đầu Ren khi bị đảo phím (PixelLab VFX 78bf9b9f), 12 khung lặp.
import os, re, uuid, sys, random
from PIL import Image
T = '/tmp/claude-tool-results/949ad0d3-1107-5f85-bbec-3a629568c41e/'
U = '/mnt/user-data/uploads/Soul Requiem/'
O = '/mnt/user-data/outputs/b333/'
CEM = 'Assets/GamePackage/EPIC RPG World Pack - Cemetery V 1.6/'
def out(rel):
    p = O + rel; os.makedirs(os.path.dirname(p), exist_ok=True); return p
src = open('/home/claude/barrow/assets.py').read()
ns = {'U': U}
exec(src[:src.index('# ---- kiểm khoá nền')], ns)
exec('TPL=open(U+"Assets/Resources/VFX/hit.png.meta").read()\n' + src[src.index('def vmeta'):src.index('sp=cells(')], ns)
vmeta = ns['vmeta']
TILE_META = open('/mnt/user-data/outputs/barrow/' + CEM + 'Props/SR-barrow/door_wood.png.meta').read()
def tile_png(img, name):
    img.save(out(CEM + 'Props/SR-barrow/%s.png' % name))
    open(out(CEM + 'Props/SR-barrow/%s.png.meta' % name), 'w', newline='\n').write(re.sub(r'guid: \w+', 'guid: ' + uuid.uuid4().hex, TILE_META, count=1))
    print('tile', name, img.size)
def strip(frames, name, px, py):
    w, h = frames[0].size
    s = Image.new('RGBA', (w * len(frames), h), (0, 0, 0, 0))
    for i, f in enumerate(frames): s.alpha_composite(f, (i * w, 0))
    s.save(out('Assets/Resources/VFX/%s.png' % name))
    open(out('Assets/Resources/VFX/%s.png.meta' % name), 'w', newline='\n').write(vmeta(name, len(frames), w, h, px, py))
    print('vfx', name, len(frames), (w, h))
    return s

# ---- lư hương + nắp quan tài
c = Image.open(T + 'mcp-pixellab-blob-1795024227323-2u2wup-a71c35f7.png').convert('RGBA')
tile_png(c.crop(c.getbbox()), 'kings_censer')
lid = Image.open('/home/claude/b333/coffin_lid_raw.png').convert('RGBA')
tile_png(lid.crop(lid.getbbox()), 'coffin_lid')

# ---- cổng lính hồn: một lính canh ma đứng chắn lối, tan dần lên trên
g = Image.open(U + 'Assets/Resources/VFX/rf_ghost_S.png').convert('RGBA')
g = g.crop(g.getbbox())                                    # 58x116
W, H = 64, 120
base = Image.new('RGBA', (W, H), (0, 0, 0, 0)); base.alpha_composite(g, ((W - g.width) // 2, H - g.height))
rnd = random.Random(1119)
N = 7
frames = []
for k in range(N):
    f = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    if k < N - 1:
        t = k / (N - 1)                                    # 0 -> 1: tan
        a = 0.86 * (1 - t)
        src_p = base.load(); p = f.load()
        cut = int(H * (1 - t * 1.15))                      # phần thân dưới tan trước, bốc lên
        for y in range(H):
            for x in range(W):
                r, gg, b, al = src_p[x, y]
                if al == 0: continue
                if y > cut and k > 0: continue
                if k > 0 and rnd.random() < t * 0.55: continue          # rã từng hạt
                dy = -int(t * 10 * rnd.random()) if k > 0 else 0
                yy = y + dy
                if 0 <= yy < H: p[x, yy] = (min(255, r + int(30 * t)), gg, b, int(al * a))
    frames.append(f)
strip(frames, 'barrow_ghost_ward', 0.0, 0.0)
tile_png(frames[0], 'ghost_ward')

# ---- VFX chiêu đè (vũng máu ma mị) + vòng choáng
sheet = Image.open(T + 'mcp-pixellab-blob-1795024205969-9esvc2-3a687d59.png').convert('RGBA')
fw, fh = 152, 139
fr = [sheet.crop(((i % 5) * fw, (i // 5) * fh, (i % 5) * fw + fw, (i // 5) * fh + fh)) for i in range(22)]
strip(fr, 'rf_blood_snare', 76 / fw, 1 - 108 / fh)
sheet = Image.open(T + 'mcp-pixellab-blob-1795024223661-zpbgij-46859ac8.png').convert('RGBA')
print('confuse sheet', sheet.size)
fw, fh = 39, 23
cols = sheet.width // fw
fr = [sheet.crop(((i % cols) * fw, (i // cols) * fh, (i % cols) * fw + fw, (i // cols) * fh + fh)) for i in range(12)]
strip(fr, 'rf_confuse_ring', 19 / fw, 1 - 15 / fh)
# xem trước
pv = Image.new('RGBA', (900, 300), (30, 34, 50, 255)); x = 0
for n in ('kings_censer', 'coffin_lid', 'ghost_ward'):
    im = Image.open(O + CEM + 'Props/SR-barrow/%s.png' % n); pv.alpha_composite(im, (x, 0)); x += im.width + 10
s = Image.open(O + 'Assets/Resources/VFX/barrow_ghost_ward.png'); pv.alpha_composite(s, (0, 130))
s = Image.open(O + 'Assets/Resources/VFX/rf_confuse_ring.png'); pv.alpha_composite(s.crop((0, 0, 39 * 4, 23)), (500, 20))
pv.resize((1800, 600), Image.NEAREST).save('/tmp/a3_prev.png')
