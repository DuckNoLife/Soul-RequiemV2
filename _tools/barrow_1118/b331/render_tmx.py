# 18/11 (§331): vẽ map .tmx ra PNG (lớp ô + tile object), tuỳ chọn phủ lớp logic. Chạy trên máy người dùng (cần ảnh tileset).
# python3 render_tmx.py <map.tmx> <out.png> [--logic] [--crop x0,y0,x1,y1 (ô)] [--scale s] [--base <thư mục Maps>]
import sys, os, re, xml.etree.ElementTree as ET
from PIL import Image, ImageDraw
args = sys.argv[1:]
TMX, OUT = args[0], args[1]
LOGIC = '--logic' in args
crop = None; scale = 1.0
if '--crop' in args: crop = [float(v) for v in args[args.index('--crop') + 1].split(',')]
if '--scale' in args: scale = float(args[args.index('--scale') + 1])
root = ET.parse(TMX).getroot(); base = os.path.dirname(os.path.abspath(TMX))
if '--base' in args: base = args[args.index('--base') + 1]          # tmx thử để ngoài Assets: tileset tính từ thư mục Maps thật
W, H, T = int(root.get('width')), int(root.get('height')), int(root.get('tilewidth'))
FH, FV, FD = 0x80000000, 0x40000000, 0x20000000
sets = []
for ts in root.findall('tileset'):
    first = int(ts.get('firstgid')); src = ts.get('source')
    tp = os.path.normpath(os.path.join(base, src)); t = ET.parse(tp).getroot(); tdir = os.path.dirname(tp)
    img = t.find('image'); d = {'first': first, 'tw': int(t.get('tilewidth')), 'th': int(t.get('tileheight'))}
    if img is not None:
        d['img'] = os.path.normpath(os.path.join(tdir, img.get('source'))); d['cols'] = int(t.get('columns')); d['count'] = int(t.get('tilecount'))
        sp = int(t.get('spacing') or 0); mg = int(t.get('margin') or 0); d['sp'] = sp; d['mg'] = mg
    else:
        d['tiles'] = {}
        for tile in t.findall('tile'):
            im = tile.find('image')
            if im is not None: d['tiles'][int(tile.get('id'))] = os.path.normpath(os.path.join(tdir, im.get('source')))
        d['count'] = max(d['tiles']) + 1 if d['tiles'] else 0
    sets.append(d)
sets.sort(key=lambda d: d['first'])
cache = {}
def tile_img(g):
    raw = g; g &= 0x0FFFFFFF
    if g == 0: return None
    if raw in cache: return cache[raw]
    d = [s for s in sets if s['first'] <= g][-1]; lid = g - d['first']; im = None
    try:
        if 'img' in d:
            key = d['img']
            if key not in cache: cache[key] = Image.open(key).convert('RGBA')
            sheet = cache[key]; c, r = lid % d['cols'], lid // d['cols']
            x = d['mg'] + c * (d['tw'] + d['sp']); y = d['mg'] + r * (d['th'] + d['sp'])
            im = sheet.crop((x, y, x + d['tw'], y + d['th']))
        elif lid in d['tiles']:
            im = Image.open(d['tiles'][lid]).convert('RGBA')
    except Exception as e:
        print('thiếu ảnh', g, e)
    if im is not None:
        if raw & FD: im = im.transpose(Image.TRANSPOSE)
        if raw & FH: im = im.transpose(Image.FLIP_LEFT_RIGHT)
        if raw & FV: im = im.transpose(Image.FLIP_TOP_BOTTOM)
    cache[raw] = im; return im
canvas = Image.new('RGBA', (W * T, H * T), (12, 12, 16, 255))
LOGIC_GROUPS = ('zones', 'spawns', 'triggers', 'gates', 'obstacle colliders', 'water colliders', 'blockers', 'map bounds', 'mechanics')
logic = []
def visible(e): return e.get('visible') != '0'
for e in root:
    if e.tag == 'layer' and visible(e):
        data = [int(v) for v in e.find('data').text.replace('\n', '').split(',') if v.strip()]
        for i, g in enumerate(data):
            if not g: continue
            im = tile_img(g)
            if im is None: continue
            x, y = (i % W) * T, (i // W) * T
            canvas.alpha_composite(im, (x, y + T - im.height))
    elif e.tag == 'objectgroup':
        nm = e.get('name')
        for o in e.findall('object'):
            x, y = float(o.get('x')), float(o.get('y')); w, h = float(o.get('width') or 0), float(o.get('height') or 0)
            if o.get('gid'):
                if nm == 'gates' and o.get('type') != 'gate_visual': continue
                im = tile_img(int(o.get('gid')))
                if im is None: continue
                if w and h and (int(w), int(h)) != im.size: im = im.resize((max(1, int(round(w))), max(1, int(round(h)))), Image.NEAREST)
                canvas.alpha_composite(im, (int(round(x)), int(round(y - im.height))))
            if nm in LOGIC_GROUPS: logic.append((nm, o.get('name') or '', o.get('type') or '', x, y, w, h, o.find('point') is not None, bool(o.get('gid'))))
if LOGIC:
    ov = Image.new('RGBA', canvas.size, (0, 0, 0, 0)); dr = ImageDraw.Draw(ov)
    col = {'blockers': (255, 60, 60), 'obstacle colliders': (255, 160, 40), 'triggers': (60, 200, 255), 'mechanics': (200, 90, 255),
           'gates': (255, 255, 60), 'zones': (80, 255, 120), 'spawns': (255, 255, 255), 'map bounds': (120, 120, 120), 'water colliders': (60, 90, 255)}
    for nm, n, t, x, y, w, h, pt, isg in logic:
        c = col.get(nm, (255, 255, 255))
        if isg: continue
        if pt: dr.ellipse((x - 4, y - 4, x + 4, y + 4), fill=c + (255,)); dr.text((x + 5, y - 5), n, fill=c + (255,)); continue
        if nm == 'zones': dr.rectangle((x, y, x + w, y + h), outline=c + (255,), width=2); dr.text((x + 3, y + 3), n, fill=c + (255,)); continue
        a = 70 if nm in ('blockers', 'obstacle colliders') else 60
        dr.rectangle((x, y, x + w, y + h), fill=c + (a,), outline=c + (220,))
        if nm not in ('blockers', 'obstacle colliders', 'map bounds'): dr.text((x + 2, y + 2), n[:22], fill=(255, 255, 255, 255))
    canvas.alpha_composite(ov)
if crop: canvas = canvas.crop(tuple(int(v * T) for v in crop))
if scale != 1: canvas = canvas.resize((int(canvas.width * scale), int(canvas.height * scale)), Image.NEAREST)
canvas.convert('RGB').save(OUT); print('đã vẽ', OUT, canvas.size)
