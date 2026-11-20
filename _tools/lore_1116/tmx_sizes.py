# kích thước ảnh của gid trong một map: from tmx_sizes import Sizer; S=Sizer(tmx); S(gid)->(w,h)
import os, xml.etree.ElementTree as ET
from render_tmx import TS, load_tsx
class Sizer:
    def __init__(s,tmx,extra=()):
        root=ET.parse(tmx).getroot(); base=os.path.dirname(tmx); s.tss=[]
        for t in list(root.findall('tileset'))+list(extra):
            src=t.get('source') if hasattr(t,'get') else t[1]
            first=int(t.get('firstgid')) if hasattr(t,'get') else t[0]
            p=os.path.normpath(os.path.join(base,src))
            if os.path.exists(p): r,b=load_tsx(p); s.tss.append(TS(first,r,b))
        s.tss.sort(key=lambda z:z.first)
    def __call__(s,g):
        ts=None
        for z in s.tss:
            if z.first<=g: ts=z
        im=ts.get(g-ts.first); return im.size if im is not None else None
