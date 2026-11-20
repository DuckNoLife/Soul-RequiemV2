# Bảng xem đồ vật (sprite rời) có nhãn: python3 propsheet.py <tsx> out.png "tiền tố1|tiền tố2" [max_w]
import sys, os, re, xml.etree.ElementTree as ET
from PIL import Image, ImageDraw, ImageFont
tsx, out, pref = sys.argv[1], sys.argv[2], sys.argv[3].split('|'); MW = int(sys.argv[4]) if len(sys.argv) > 4 else 1800
r = ET.parse(tsx).getroot(); base = os.path.dirname(tsx); items = []
for t in r.findall('tile'):
    i = t.find('image'); n = os.path.basename(i.get('source'))
    if any(n.startswith(p) for p in pref): items.append((int(t.get('id')), n, Image.open(os.path.normpath(os.path.join(base, i.get('source')))).convert('RGBA')))
f = ImageFont.load_default(size=10); x = y = rh = 0; pos = []
for tid, n, im in items:
    w = max(im.width, 70) + 6
    if x + w > MW: x = 0; y += rh + 14; rh = 0
    pos.append((x, y, tid, n, im)); x += w; rh = max(rh, im.height)
sheet = Image.new('RGBA', (MW, y + rh + 16), (58, 52, 66, 255)); d = ImageDraw.Draw(sheet)
for x, y, tid, n, im in pos:
    sheet.alpha_composite(im, (x, y + 12)); lab = '%d %s' % (tid, re.sub(r'\.png$', '', n)[-12:]); d.text((x, y), lab, fill=(255, 230, 120), font=f)
sheet.save(out); print(len(items), sheet.size)
