# Đọc .tmx: gid -> tên ảnh (tileset atlas hoặc bộ sưu tập ảnh), object prop + collider. Dùng chung cho phân tích collider (§303).
import xml.etree.ElementTree as ET, os, re
PATHS = {}
from pathlib import Path
def tilesets(tmx):
    r = ET.parse(tmx).getroot(); out = []
    for t in r.findall('tileset'):
        fg = int(t.get('firstgid')); src = t.get('source')
        if src is None or src.startswith(':'): out.append((fg, None, {})); continue
        p = (Path(tmx).parent / src).resolve()
        try: tr = ET.parse(p).getroot()
        except Exception: out.append((fg, p, {})); continue
        names = {}
        for tile in tr.findall('tile'):
            im = tile.find('image')
            if im is not None: names[int(tile.get('id'))] = Path(im.get('source')).stem; PATHS[Path(im.get('source')).stem] = (p.parent / im.get('source')).resolve()
        out.append((fg, p, names))
    return sorted(out, key=lambda x: x[0])
def gidname(ts, gid):
    gid &= 0x1fffffff; best = None
    for fg, p, names in ts:
        if fg <= gid: best = (fg, p, names)
    if not best: return '?'
    fg, p, names = best
    return names.get(gid - fg, '%s#%d' % (p.stem if p else '?', gid - fg))
def objects(tmx):
    r = ET.parse(tmx).getroot(); ts = tilesets(tmx); props, cols = [], []
    for g in r.iter('objectgroup'):
        gn = g.get('name')
        for o in g.findall('object'):
            x, y = float(o.get('x')), float(o.get('y')); w, h = float(o.get('width', 0)), float(o.get('height', 0))
            if o.get('gid'):
                props.append(dict(id=o.get('id'), grp=gn, img=gidname(ts, int(o.get('gid'))), x=x, y=y - h, w=w, h=h, name=o.get('name')))
            elif gn == 'obstacle colliders':
                sh = 'ell' if o.find('ellipse') is not None else ('poly' if o.find('polygon') is not None else 'rect')
                cols.append(dict(id=o.get('id'), name=o.get('name'), x=x, y=y, w=w, h=h, shape=sh))
    return props, cols
def owner(c, props):
    """Prop có chân (đáy ảnh) gần collider nhất và chứa tâm collider theo chiều ngang."""
    cx, cy = c['x'] + c['w'] / 2, c['y'] + c['h'] / 2; best = None
    for p in props:
        if p['grp'] not in ('props', 'props front', 'wall props', 'gates', 'mechanics_art'): continue
        if not (p['x'] - 2 <= cx <= p['x'] + p['w'] + 2 and p['y'] - 2 <= cy <= p['y'] + p['h'] + 8): continue
        d = abs((p['y'] + p['h']) - (c['y'] + c['h'])) + abs((p['x'] + p['w'] / 2) - cx) * 0.5
        if best is None or d < best[0]: best = (d, p)
    return best[1] if best else None
