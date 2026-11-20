# 18/11 (§328): Aldous v2 (PixelLab 4f8c8996, style Lantern Knight) -> Resources/NPCs/aldous (canvas 144, chân hàng 120)
# Nguồn: ảnh xem trước 1:1 của pixelart_workbench (nhãn 18 px trên cùng, nền #1f252c). Hướng E: xoá bóng đổ elip PixelLab tự vẽ
# (S/N không có bóng -> quay người không nhấp nháy bóng), rồi vẽ lại viền đáy giày.
import sys, os
from PIL import Image
from collections import deque
T='/tmp/claude-tool-results/949ad0d3-1107-5f85-bbec-3a629568c41e/'
SRC={('walk','S'):(T+'mcp-pixellab-blob-1795003131980-x2v349-11455911.png',0,6),
     ('walk','N'):(T+'mcp-pixellab-blob-1795003131980-x2v349-11455911.png',2,6),
     ('walk','E'):(T+'mcp-pixellab-blob-1795003208634-mzuh2w-3fbe4aa9.png',0,6),   # bản e2 (đèn lồng lộ dần, tay vung nhẹ)
     ('idle','S'):(T+'mcp-pixellab-blob-1795003135111-34paat-43d22a89.png',0,4),
     ('idle','E'):(T+'mcp-pixellab-blob-1795003135111-34paat-43d22a89.png',1,4),
     ('idle','N'):(T+'mcp-pixellab-blob-1795003135111-34paat-43d22a89.png',2,4)}
BG=(31,37,44); TOP=18; C=160; OUTLINE=(25,14,30,255); OUT=sys.argv[1]
os.makedirs(OUT,exist_ok=True)
def cell(path,row,col):
    c=Image.open(path).crop((col*C,TOP+row*C,col*C+C,TOP+row*C+C)).convert('RGBA'); px=c.load()
    for y in range(C):
        for x in range(C):
            if px[x,y][:3]==BG: px[x,y]=(0,0,0,0)
    return c
dark=lambda p: p[3] and sum(p[:3])<110
brown=lambda p: p[3] and p[0]>p[2]+15 and sum(p[:3])>=110
SH=(38,27,41)                                              # màu chính của bóng elip (#261b29)
def unshadow(c):
    """Bóng elip ở chân (chỉ hướng E). Từ hàng bóng bắt đầu rộng ra (>= 8 px màu bóng): chỉ giữ giày (nâu) + viền tối sát giày."""
    px=c.load(); b=c.getbbox()
    ys=next(y for y in range(b[3]-20,b[3]) if sum(1 for x in range(C) if px[x,y][3] and px[x,y][:3]==SH)>=8)
    rm=[]
    for y in range(ys,C):
        for x in range(C):
            p=px[x,y]
            if not p[3] or brown(p): continue
            near=any(0<=x+dx<C and 0<=y+dy<C and brown(px[x+dx,y+dy]) for dx in (-1,0,1) for dy in (-1,0,1))
            if not (near and sum(p[:3])<110): rm.append((x,y))
    for q in rm: px[q]=(0,0,0,0)
    return ys,len(rm)
frames={k:[cell(p,r,i) for i in range(n)] for k,(p,r,n) in SRC.items()}
for k,fs in frames.items():
    if k[1]=='E': print(k,'bóng xoá/viền',[unshadow(f) for f in fs])
ref={}
for d in 'SEN':
    b=frames[('idle',d)][0].getbbox(); ref[d]=(round(72-(b[0]+b[2])/2), 120-(b[3]-1))
for k,fs in frames.items():
    dx,dy=ref[k[1]]
    if k==('walk','E'): dy=120-(fs[0].getbbox()[3]-1)          # bản e2 là job khác: neo chân riêng
    for i,f in enumerate(fs):
        o=Image.new('RGBA',(144,144),(0,0,0,0)); o.alpha_composite(f.crop((max(0,-dx),max(0,-dy),C,C)),(max(0,dx),max(0,dy)))
        bb=o.getbbox(); assert bb and bb[0]>0 and bb[2]<144 and bb[1]>0,(k,i,bb)
        o.save(os.path.join(OUT,'%s_%s_%02d.png'%(k[0],k[1],i)))
    print(k,'dx,dy',ref[k[1]],'bbox0',Image.open(os.path.join(OUT,'%s_%s_00.png'%k)).getbbox())
