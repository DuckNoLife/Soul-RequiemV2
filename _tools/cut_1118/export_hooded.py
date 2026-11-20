# 18/11 (§327): tách khung kẻ trùm mũ từ ảnh xem trước 1:1 của pixelart_workbench -> canvas 144, chân hàng 120 (như Pell)
import sys, os
from PIL import Image
from collections import deque
T='/tmp/claude-tool-results/949ad0d3-1107-5f85-bbec-3a629568c41e/'
SHEETS={'walk':(T+'mcp-pixellab-blob-1795001507904-brqt80-7eed543f.png',6,['S','E','N']),
        'idle':(T+'mcp-pixellab-blob-1795001511324-7oifxz-83a8eb19.png',4,['S','E','N'])}
STRIKE=T+'mcp-pixellab-blob-1795001531777-3o116n-239c67d0.png'
BG=(31,37,44); TOP=18; C=160; OUT=sys.argv[1]
os.makedirs(OUT,exist_ok=True)
def cell(im,col,row):
    c=im.crop((col*C,TOP+row*C,col*C+C,TOP+row*C+C)).convert('RGBA'); px=c.load()
    for y in range(C):
        for x in range(C):
            if px[x,y][:3]==BG: px[x,y]=(0,0,0,0)
    return c
def despeck(c,maxn=8):
    px=c.load(); W,H=c.size; seen=set(); comps=[]
    for y in range(H):
        for x in range(W):
            if px[x,y][3] and (x,y) not in seen:
                q=deque([(x,y)]); seen.add((x,y)); comp=[]
                while q:
                    a,b=q.popleft(); comp.append((a,b))
                    for dx in (-1,0,1):
                        for dy in (-1,0,1):
                            n=(a+dx,b+dy)
                            if 0<=n[0]<W and 0<=n[1]<H and n not in seen and px[n][3]: seen.add(n); q.append(n)
                comps.append(comp)
    comps.sort(key=len,reverse=True); removed=0
    for comp in comps[1:]:
        if len(comp)<=maxn:
            for p in comp: px[p]=(0,0,0,0)
            removed+=len(comp)
    return removed
frames={}
for anim,(path,n,dirs) in SHEETS.items():
    im=Image.open(path)
    for r,d in enumerate(dirs): frames[(anim,d)]=[cell(im,i,r) for i in range(n)]
im=Image.open(STRIKE)
frames[('strike','E')]=[cell(im,i%3,i//3) for i in range(9)]
rep=[]
for k,fs in frames.items():
    rm=[despeck(f, 24 if k[0]=="strike" else 8) for f in fs]; rep.append((k,rm))
# neo: chân (đáy bbox khung 0) -> hàng 120; tâm ngang khung 0 -> 72 (strike neo theo idle_E)
def anchor(f): b=f.getbbox(); return (b[0]+b[2])/2, b[3]-1, b
ref={}
for k,fs in frames.items():
    cx,bot,b=anchor(fs[0]); ref[k]=(cx,bot)
for k,fs in frames.items():
    # cùng hướng = cùng hệ toạ độ canvas PixelLab -> dùng chung độ dời của idle cùng hướng (đổi state không giật)
    cx,bot=ref[('idle',k[1])]
    dx=round(72-cx); dy=120-bot
    for i,f in enumerate(fs):
        o=Image.new('RGBA',(144,144),(0,0,0,0)); o.paste(f,(dx-8+8,dy),f)  # paste tại (dx,dy) trong canvas 144
        o=Image.new('RGBA',(144,144),(0,0,0,0)); o.alpha_composite(f.crop((max(0,-dx),max(0,-dy),160,160)),(max(0,dx),max(0,dy)))
        bb=o.getbbox(); assert bb and bb[0]>0 and bb[2]<144 and bb[1]>0, (k,i,bb)
        o.save(os.path.join(OUT,'%s_%s_%02d.png'%(k[0],k[1],i)))
    print(k, 'dx',dx,'dy',dy,'bbox0',fs[0].getbbox(),'removed',dict(rep)[k])
cols=set()
for k,fs in frames.items():
    for f in fs: cols|=set(c for _,c in f.getcolors(100000) if c[3])
print('colours',len(cols))
# strike_E_04: xoá tay đốm sáng (92..94, 87..89)
