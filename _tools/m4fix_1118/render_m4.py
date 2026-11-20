# 18/11: render Map 4 (.tmx) ra ảnh để soát bố cục: lớp ô + vật thể có gid + lớp logic (blocker/collider/cổng/story/zone).
# python3 render_m4.py <out.png> [scale] [x0 y0 x1 y1 (ô)] [--logic]
import sys, os, xml.etree.ElementTree as ET
from PIL import Image, ImageDraw
TMX = os.path.expanduser("~/mnt/Soul Requiem/Assets/GamePackage/EPIC RPG World Pack - Crypt V.1.6/TiledMap Editor/Maps/map4_catacombs_of_naming.tmx")
args = [a for a in sys.argv[1:] if not a.startswith('--')]
logic = '--logic' in sys.argv
ONLY = [a[7:] for a in sys.argv if a.startswith('--only=')]
ONLY = ONLY[0].split(',') if ONLY else None
NOOBJ = '--noobj' in sys.argv
out = args[0]; sc = float(args[1]) if len(args) > 1 else 0.25
reg = [int(v) for v in args[2:6]] if len(args) >= 6 else None
root = ET.parse(TMX).getroot(); base = os.path.dirname(TMX)
TW = 32; MW, MH = int(root.get('width')), int(root.get('height'))
tiles = {}          # gid -> (image path, box or None)
imcache = {}
def img(p):
    if p not in imcache:
        try: imcache[p] = Image.open(p).convert('RGBA')
        except Exception: imcache[p] = None
    return imcache[p]
for ts in root.findall('tileset'):
    fg = int(ts.get('firstgid')); src = os.path.normpath(os.path.join(base, ts.get('source'))); d = os.path.dirname(src)
    t = ET.parse(src).getroot()
    tw, th = int(t.get('tilewidth')), int(t.get('tileheight')); sp = int(t.get('spacing', 0)); mg = int(t.get('margin', 0))
    ie = t.find('image')
    if ie is not None:
        cols = int(t.get('columns')); cnt = int(t.get('tilecount')); p = os.path.normpath(os.path.join(d, ie.get('source')))
        for i in range(cnt):
            x = mg + (i % cols) * (tw + sp); y = mg + (i // cols) * (th + sp)
            tiles[fg + i] = (p, (x, y, x + tw, y + th))
    for te in t.findall('tile'):
        ie2 = te.find('image')
        if ie2 is not None: tiles[fg + int(te.get('id'))] = (os.path.normpath(os.path.join(d, ie2.get('source'))), None)
def tile_img(gid):
    fh, fv = bool(gid & 0x80000000), bool(gid & 0x40000000); g = gid & 0x1FFFFFFF
    if g not in tiles: return None
    p, box = tiles[g]; im = img(p)
    if im is None: return None
    if box: im = im.crop(box)
    if fh: im = im.transpose(Image.FLIP_LEFT_RIGHT)
    if fv: im = im.transpose(Image.FLIP_TOP_BOTTOM)
    return im
x0, y0, x1, y1 = reg if reg else (0, 0, MW, MH)
W, H = (x1 - x0) * TW, (y1 - y0) * TW
canvas = Image.new('RGBA', (W, H), (20, 16, 24, 255))
def visible(e): return e.get('visible', '1') != '0'
for e in root:
    if e.tag == 'layer' and visible(e) and (ONLY is None or e.get('name') in ONLY):
        data = [int(v) for v in e.find('data').text.replace('\n', '').split(',') if v.strip()]
        for ty in range(y0, y1):
            for tx in range(x0, x1):
                gid = data[ty * MW + tx]
                if not gid: continue
                im = tile_img(gid)
                if im is None: continue
                canvas.alpha_composite(im, ((tx - x0) * TW, (ty - y0) * TW + TW - im.height))
    elif e.tag == 'objectgroup' and visible(e) and not NOOBJ:
        objs = [o for o in e.findall('object') if o.get('gid') and o.get('visible', '1') != '0']
        objs.sort(key=lambda o: float(o.get('y')))
        for o in objs:
            im = tile_img(int(o.get('gid')))
            if im is None: continue
            w = int(round(float(o.get('width', im.width)))); h = int(round(float(o.get('height', im.height))))
            if (w, h) != im.size and w > 0 and h > 0: im = im.resize((w, h), Image.NEAREST)
            X = float(o.get('x')) - x0 * TW; Y = float(o.get('y')) - y0 * TW - h
            if X > W or Y > H or X + w < 0 or Y + h < 0: continue
            canvas.paste(im, (int(X), int(Y)), im)
if logic:
    d = ImageDraw.Draw(canvas)
    COL = {'blocker': (255, 0, 0), 'gate': (255, 140, 0), 'story': (230, 90, 255), 'zone': (80, 200, 255), 'pickup': (60, 255, 230),
           'lever': (255, 230, 40), 'exit': (255, 255, 255), 'player_start': (255, 255, 255), 'checkpoint': (80, 255, 120), 'obstacle': (255, 80, 80),
           'pit': (255, 255, 0), 'roller': (255, 90, 160), 'sign': (200, 200, 200)}
    for e in root.iter('objectgroup'):
        for o in e.findall('object'):
            t = o.get('type') or ''
            if o.get('gid') or t not in COL: continue
            x, y, w, h = (float(o.get(k, 0)) for k in ('x', 'y', 'width', 'height'))
            X, Y = x - x0 * TW, y - y0 * TW
            if X > W or Y > H or X + w < 0 or Y + h < 0: continue
            wd = 1 if t in ('zone', 'obstacle') else 2
            d.rectangle([X, Y, X + w, Y + h], outline=COL[t], width=wd)
            if t not in ('obstacle', 'blocker'): d.text((X + 2, Y + 1), (o.get('name') or '')[:18], fill=COL[t])
    # lưới ô mỗi 4 ô
    for gx in range(x0 - x0 % 4, x1, 4):
        d.line([((gx - x0) * TW, 0), ((gx - x0) * TW, H)], fill=(255, 255, 255, 40)); d.text(((gx - x0) * TW + 1, 1), str(gx), fill=(255, 255, 0))
    for gy in range(y0 - y0 % 4, y1, 4):
        d.line([(0, (gy - y0) * TW), (W, (gy - y0) * TW)], fill=(255, 255, 255, 40)); d.text((1, (gy - y0) * TW + 1), str(gy), fill=(255, 255, 0))
if sc != 1: canvas = canvas.resize((int(W * sc), int(H * sc)), Image.LANCZOS if sc < 1 else Image.NEAREST)
canvas.convert('RGB').save(out); print(out, canvas.size)
