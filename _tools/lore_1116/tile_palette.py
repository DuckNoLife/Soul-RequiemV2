# Bảng xem ô tile theo gid của một map: python3 tile_palette.py <tmx> <out.png> gid1-gid2 [gid3-gid4 ...]
import sys, os
import xml.etree.ElementTree as ET
from PIL import Image, ImageDraw
from render_tmx import TS, load_tsx
tmx=sys.argv[1]; out=sys.argv[2]; rngs=sys.argv[3:]
root=ET.parse(tmx).getroot(); base=os.path.dirname(tmx); tss=[]
for t in root.findall('tileset'):
    p=os.path.normpath(os.path.join(base,t.get('source')))
    if os.path.exists(p): r,b=load_tsx(p); tss.append(TS(int(t.get('firstgid')),r,b))
tss.sort(key=lambda z:z.first)
gids=[]
for r in rngs:
    a,b=(r.split('-')+[r])[:2]; gids+=list(range(int(a),int(b)+1))
cols=16; cell=40
im=Image.new('RGBA',(cols*cell,((len(gids)+cols-1)//cols)*(cell+10)),(40,40,48,255)); d=ImageDraw.Draw(im)
for i,g in enumerate(gids):
    ts=None
    for z in tss:
        if z.first<=g: ts=z
    t=ts.get(g-ts.first) if ts else None
    x,y=(i%cols)*cell,(i//cols)*(cell+10)
    if t is not None:
        t=t.copy(); t.thumbnail((32,32)); im.alpha_composite(t,(x+4,y))
    d.text((x+2,y+32),str(g),fill=(255,255,255))
im.save(out); print(out,im.size)
