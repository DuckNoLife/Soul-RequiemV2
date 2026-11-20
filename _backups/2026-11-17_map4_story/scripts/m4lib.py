# Map 4 — thư viện object: tileset đồ vật, đặt tile object theo CHÂN (giữa-dưới), đèn, khối chặn, ghi .tmx đủ lớp. 17/11/2026.
import os, xml.etree.ElementTree as ET
from pathlib import Path
from PIL import Image
from ck import GP
T = 32
CRT = GP / "EPIC RPG World Pack - Crypt V.1.6/TiledMap Editor/Tilesets"
OPT = GP / "EPIC RPG World Pack - Old Prison V1.7.1/TiledMap Editor New/Tilesets"
HERE = Path(__file__).resolve().parent
ART = HERE / "art"

class TSX:
    """Tileset dạng collection (mỗi tile 1 ảnh) hoặc dải ảnh; size(tid) -> (w, h)."""
    def __init__(s, path, key):
        s.path = Path(path); s.key = key; r = ET.parse(s.path).getroot(); s.root = r
        s.tw, s.th = int(r.get('tilewidth')), int(r.get('tileheight'))
        s.sizes = {}; s.names = {}
        img = r.find('image')
        if img is not None:
            n = int(r.get('tilecount'))
            for i in range(n): s.sizes[i] = (s.tw, s.th)
            s.span = n
        else:
            mx = 0
            for t in r.findall('tile'):
                i = t.find('image'); tid = int(t.get('id'))
                s.sizes[tid] = (int(i.get('width')), int(i.get('height'))); s.names[tid] = os.path.basename(i.get('source')); mx = max(mx, tid)
            s.span = mx + 1
        s.firstgid = 0
    def find(s, name):
        for tid, n in s.names.items():
            if n == name: return tid
        raise KeyError(name)

def make_art_tsx(path, files, name='SR-m4-art'):
    """Tileset collection cho ảnh cửa / cơ quan (khung 0). files: danh sách đường dẫn ảnh."""
    lines = ['<?xml version="1.0" encoding="UTF-8"?>',
             '<tileset version="1.10" tiledversion="1.11.2" name="%s" tilewidth="128" tileheight="275" tilecount="%d" columns="0">' % (name, len(files)),
             ' <grid orientation="orthogonal" width="1" height="1"/>']
    for i, f in enumerate(files):
        w, h = Image.open(f).size
        lines.append(' <tile id="%d">\n  <image source="%s" width="%d" height="%d"/>\n </tile>' % (i, os.path.relpath(f, Path(path).parent), w, h))
    lines.append('</tileset>'); Path(path).write_text('\n'.join(lines))

class Objs:
    GROUPS = ['ground decals', 'gates_floor', 'wall props', 'props', 'props front', 'ambient fx', 'gates', 'mechanics_art', 'zones', 'spawns',
              'triggers', 'obstacle colliders', 'blockers', 'map bounds', 'mechanics']
    LOGIC = {'zones', 'spawns', 'triggers', 'obstacle colliders', 'blockers', 'map bounds', 'mechanics'}
    def __init__(s, tilesets):
        s.ts = {t.key: t for t in tilesets}; s.G = {g: [] for g in s.GROUPS}; s.nid = 1
    def gid(s, key, tid): return s.ts[key].firstgid + tid
    def obj(s, group, name=None, typ=None, x=0, y=0, w=None, h=None, gid=None, props=(), shape=None):
        a = ['id="%d"' % s.nid]; s.nid += 1
        if name: a.append('name="%s"' % name)
        if typ: a.append('type="%s"' % typ)
        if gid: a.append('gid="%d"' % gid)
        a.append('x="%.2f" y="%.2f"' % (x, y))
        if w is not None: a.append('width="%.2f" height="%.2f"' % (w, h))
        body = ''
        if props:
            pl = []
            for k, v in props:
                if isinstance(v, bool): pl.append('<property name="%s" type="bool" value="%s"/>' % (k, 'true' if v else 'false'))
                elif isinstance(v, tuple): pl.append('<property name="%s" type="%s" value="%s"/>' % (k, v[1], v[0]))
                elif isinstance(v, int): pl.append('<property name="%s" type="int" value="%d"/>' % (k, v))
                else: pl.append('<property name="%s" value="%s"/>' % (k, str(v).replace('&', '&amp;').replace('"', '&quot;').replace('<', '&lt;').replace('\n', '&#10;')))
            body += '<properties>' + ''.join(pl) + '</properties>'
        if shape == 'point': body += '<point/>'
        elif shape == 'ellipse': body += '<ellipse/>'                 # ST2U: tròn -> CircleCollider2D, bầu dục -> đa giác (§303)
        s.G[group].append((y if not gid else y, '  <object %s>%s</object>' % (' '.join(a), body) if body else '  <object %s/>' % ' '.join(a)))
        return s.nid - 1
    def put(s, group, key, tid, cx, by, flip=False, name=None, typ=None, props=(), dx=0):
        """Đặt ảnh theo chân: giữa-dưới tại (cx, by) ô."""
        w, h = s.ts[key].sizes[tid]
        g = s.gid(key, tid) | (0x80000000 if flip else 0)
        return s.obj(group, name, typ, cx * T - w / 2 + dx, by * T, w, h, gid=g, props=props)
    def put_tl(s, group, key, tid, x, top, name=None, typ=None, props=()):
        """Đặt ảnh theo góc trên-trái (ô) — dùng cho cửa / cổng khớp mép tường."""
        w, h = s.ts[key].sizes[tid]
        return s.obj(group, name, typ, x * T, top * T + h, w, h, gid=s.gid(key, tid), props=props)
    def light(s, name, cx, cy, color, radius, inten=0.8, anim='flicker', amount=0.15, speed=0.8, off_when=None):
        p = [('anim', anim), ('anim_amount', (str(amount), 'float')), ('anim_speed', (str(speed), 'float')), ('color', color),
             ('intensity', (str(inten), 'float')), ('light', 'torch'), ('mechanic', 'light'), ('radius', (str(radius), 'float')), ('unity:IsTrigger', True)]
        if off_when: p.append(('off_when', off_when))
        s.obj('mechanics', name, 'light', cx * T - 16, cy * T - 16, 32, 32, props=p)
    def block(s, x, y, w, h, kind='prop', shape=None):
        s.obj('obstacle colliders', 'prop collider', 'obstacle', x * T, y * T, w * T, h * T, props=[('collider_kind', kind)], shape=shape)
    def xml(s):
        out = []; gid_id = 100
        for g in s.GROUPS:
            items = s.G[g]
            if g in ('props', 'props front', 'wall props', 'ground decals', 'mechanics_art'): items = sorted(items, key=lambda t: t[0])
            op = ' opacity="0.35"' if g in s.LOGIC else ''
            out.append(' <objectgroup id="%d" name="%s"%s>' % (gid_id, g, op)); gid_id += 1
            out += [t[1] for t in items]; out.append(' </objectgroup>')
        return out

def write_tmx(path, W, H, tilesets, layers, objs, rel_root=None):
    """tilesets: list object có .firstgid, .path; layers: [(tên, lưới)]; objs: Objs."""
    csv = lambda g: ',\n'.join(','.join(str(v) for v in r) for r in g)
    out = ['<?xml version="1.0" encoding="UTF-8"?>',
           '<map version="1.10" tiledversion="1.11.2" orientation="orthogonal" renderorder="right-down" width="%d" height="%d" tilewidth="32" tileheight="32" infinite="0" nextlayerid="200" nextobjectid="%d">' % (W, H, objs.nid + 1)]
    base = Path(path).parent
    for t in tilesets:
        src = str(t.path)
        if rel_root: src = src.replace(rel_root[0], rel_root[1])
        out.append(' <tileset firstgid="%d" source="%s"/>' % (t.firstgid, os.path.relpath(src, base).replace('&', '&amp;')))
    for i, (n, g) in enumerate(layers):
        out.append(' <layer id="%d" name="%s" width="%d" height="%d"><data encoding="csv">\n%s\n</data></layer>' % (i + 1, n, W, H, csv(g)))
    out += objs.xml(); out.append('</map>')
    Path(path).write_text('\n'.join(out))
