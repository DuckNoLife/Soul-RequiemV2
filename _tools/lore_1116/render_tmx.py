# Dựng ảnh một vùng map .tmx (lớp tile + tile object + khung collider) để xem trước khi đặt vật.
# python3 render_tmx.py <map.tmx> <x0> <y0> <w> <h> <out.png> [scale]   (đơn vị: ô)
import sys, os, re, base64, zlib, struct
import xml.etree.ElementTree as ET
from PIL import Image, ImageDraw
F_H,F_V,F_D=0x80000000,0x40000000,0x20000000
def load_tsx(path):
    t=ET.parse(path).getroot(); return t,os.path.dirname(path)
class TS:
    def __init__(s,first,root,base):
        s.first=first; s.tw=int(root.get('tilewidth')); s.th=int(root.get('tileheight'))
        s.cols=int(root.get('columns') or 0); s.img=None; s.tiles={}
        im=root.find('image')
        if im is not None:
            p=os.path.normpath(os.path.join(base,im.get('source')))
            s.img=Image.open(p).convert('RGBA') if os.path.exists(p) else None
            if not s.cols and s.img: s.cols=max(1,s.img.width//s.tw)
        for t in root.findall('tile'):
            i=t.find('image')
            if i is not None:
                p=os.path.normpath(os.path.join(base,i.get('source')))
                s.tiles[int(t.get('id'))]=p
        s.cache={}
    def get(s,lid):
        if lid in s.cache: return s.cache[lid]
        r=None
        if lid in s.tiles:
            p=s.tiles[lid]; r=Image.open(p).convert('RGBA') if os.path.exists(p) else None
        elif s.img is not None and s.cols:
            x=(lid%s.cols)*s.tw; y=(lid//s.cols)*s.th
            if y+s.th<=s.img.height: r=s.img.crop((x,y,x+s.tw,y+s.th))
        s.cache[lid]=r; return r
def main(path,x0,y0,w,h,out,scale=2):
    root=ET.parse(path).getroot(); base=os.path.dirname(path)
    T=int(root.get('tilewidth'))
    tss=[]
    for t in root.findall('tileset'):
        first=int(t.get('firstgid'))
        if t.get('source'):
            p=os.path.normpath(os.path.join(base,t.get('source')))
            if not os.path.exists(p): continue
            r,b=load_tsx(p); tss.append(TS(first,r,b))
        else: tss.append(TS(first,t,base))
    tss.sort(key=lambda z:z.first)
    def tile(g):
        g&=~(F_H|F_V|F_D)
        if g==0: return None
        ts=None
        for z in tss:
            if z.first<=g: ts=z
        return ts.get(g-ts.first) if ts else None
    W,H=int(w*T),int(h*T); img=Image.new('RGBA',(W,H),(20,20,24,255))
    def walk(node,visible=True):
        for ch in node:
            if ch.tag=='group': walk(ch, visible and ch.get('visible')!='0')
            elif ch.tag=='layer' and visible and ch.get('visible')!='0':
                data=ch.find('data'); lw=int(ch.get('width'))
                enc=data.get('encoding')
                if enc=='csv': ids=[int(v) for v in data.text.replace('\n','').split(',') if v.strip()]
                elif enc=='base64':
                    raw=base64.b64decode(data.text.strip())
                    if data.get('compression')=='zlib': raw=zlib.decompress(raw)
                    ids=list(struct.unpack('<%dI'%(len(raw)//4),raw))
                else: continue
                for ty in range(int(y0),int(y0+h)):
                    for tx in range(int(x0),int(x0+w)):
                        i=ty*lw+tx
                        if 0<=i<len(ids) and ids[i]:
                            g=ids[i]; t=tile(g)
                            if t is None: continue
                            if g&F_H: t=t.transpose(Image.FLIP_LEFT_RIGHT)
                            if g&F_V: t=t.transpose(Image.FLIP_TOP_BOTTOM)
                            img.alpha_composite(t,(int((tx-x0)*T),int((ty-y0)*T+T-t.height)))
            elif ch.tag=='objectgroup' and visible:
                for o in ch.findall('object'):
                    gid=o.get('gid'); x=float(o.get('x'))-x0*T; y=float(o.get('y'))-y0*T
                    if gid:
                        t=tile(int(gid))
                        if t is not None:
                            ow,oh=float(o.get('width',t.width)),float(o.get('height',t.height))
                            t=t.resize((max(1,int(ow)),max(1,int(oh))))
                            if int(gid)&F_H: t=t.transpose(Image.FLIP_LEFT_RIGHT)
                            img.alpha_composite(t,(int(x),int(y-oh)))
    walk(root)
    if os.environ.get('SR_CLEAN'):                         # 16/11: ảnh sạch (không khung va chạm / lưới) — dùng bake minimap
        img.save(out); print(out,img.size); return
    d=ImageDraw.Draw(img)
    col={'obstacle colliders':(255,60,60),'blockers':(255,140,0),'water colliders':(60,140,255),'mechanics':(80,255,120),'triggers':(255,255,0)}
    for g in root.iter('objectgroup'):
        c=col.get(g.get('name'))
        if not c: continue
        for o in g.findall('object'):
            if o.get('gid'): continue
            x=float(o.get('x'))-x0*T; y=float(o.get('y'))-y0*T; ow=float(o.get('width',0)); oh=float(o.get('height',0))
            if ow and oh: d.rectangle([x,y,x+ow,y+oh],outline=c)
            poly=o.find('polygon')
            if poly is not None:
                pts=[(x+float(a),y+float(b)) for a,b in (p.split(',') for p in poly.get('points').split())]
                d.polygon(pts,outline=c)
    for ty in range(int(y0),int(y0+h)):                     # lưới ô + số toạ độ mỗi 5 ô
        if ty%5==0: d.line([0,(ty-y0)*T,W,(ty-y0)*T],fill=(255,255,255,50)); d.text((2,(ty-y0)*T+1),str(ty),fill=(255,255,255))
    for tx in range(int(x0),int(x0+w)):
        if tx%5==0: d.line([(tx-x0)*T,0,(tx-x0)*T,H],fill=(255,255,255,50)); d.text(((tx-x0)*T+2,2),str(tx),fill=(255,255,255))
    if scale!=1: img=img.resize((int(W*scale),int(H*scale)),Image.NEAREST)
    img.save(out); print(out,img.size)
if __name__=='__main__':
    a=sys.argv; main(a[1],float(a[2]),float(a[3]),float(a[4]),float(a[5]),a[6],float(a[7]) if len(a)>7 else 1)
