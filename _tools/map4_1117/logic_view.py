# §304: ảnh kiểm tra lớp LOGIC Map 4 — vẽ đè lên ảnh render_tmx: cổng (đỏ), tời/xích (vàng), vật nhặt (xanh lơ), điểm nghỉ (xanh lá),
# cảnh nối (tím), lối ra (cam), chỗ tỉnh dậy (trắng). python3 logic_view.py <render.png> <x0> <y0> <scale> <out.png>
import sys, xml.etree.ElementTree as ET
from pathlib import Path
from PIL import Image, ImageDraw
TMX = Path.home() / "mnt/Soul Requiem/Assets/GamePackage/EPIC RPG World Pack - Crypt V.1.6/TiledMap Editor/Maps/map4_catacombs_of_naming.tmx"
src, x0, y0, sc, out = sys.argv[1], float(sys.argv[2]), float(sys.argv[3]), float(sys.argv[4]), sys.argv[5]
im = Image.open(src).convert('RGB'); d = ImageDraw.Draw(im)
COL = {'gate': (255, 60, 60), 'lever': (255, 220, 40), 'breakable': (255, 160, 40), 'pickup': (60, 230, 255), 'checkpoint': (80, 255, 120),
       'story': (230, 90, 255), 'exit': (255, 150, 60), 'player_start': (255, 255, 255),
       'roller': (255, 90, 160), 'blade': (255, 90, 160), 'spikes': (255, 90, 160), 'launcher': (255, 120, 0), 'plate': (180, 255, 60), 'pit': (255, 255, 0), 'toggle': (120, 120, 255), 'sign': (200, 200, 200)}
for g in ET.parse(TMX).getroot().iter('objectgroup'):
    for o in g.findall('object'):
        t = o.get('type')
        if t not in COL or o.get('gid'): continue
        x, y, w, h = (float(o.get(k, 0)) for k in ('x', 'y', 'width', 'height'))
        X, Y = (x / 32 - x0) * 32 * sc, (y / 32 - y0) * 32 * sc
        if X + w * sc < 0 or Y + h * sc < 0 or X > im.width or Y > im.height: continue
        d.rectangle([X, Y, X + w * sc, Y + h * sc], outline=COL[t], width=2)
        d.text((X + 2, Y + 1), o.get('name'), fill=COL[t])
im.save(out); print(out, im.size)
