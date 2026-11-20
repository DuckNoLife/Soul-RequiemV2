# Hermit's Hollow (16/11, §255): hài cốt ẩn sĩ ngồi tựa vách (trang trí) + đống đá sập chặn cửa hầm mỏ (breakable, Earthshatter phá).
import math, random, sys
from PIL import Image
from relic_dry import vnoise, rough, dry_fill, blank, ell, line, depth, outline, BONE, DRY
OUT=(18,12,10,255)
CLOAK=[(34,34,40,255),(52,52,60,255),(72,72,80,255),(96,96,104,255)]
ROCK=[(46,34,28,255),(70,54,42,255),(98,78,60,255),(128,104,80,255),(156,130,102,255)]
WOOD=[(46,30,20,255),(74,50,32,255),(104,72,46,255),(134,98,64,255)]
def hermit():
    """44x46: bộ xương trong áo choàng xám rách, ngồi tựa vách, gối co, hai bàn tay xương đặt ngửa trên đùi (chỗ thanh kiếm từng nằm)."""
    W,H=44,46; im=Image.new('RGBA',(W,H)); px=im.load()
    cl=blank(W,H)
    ell(cl,22,24,12,13)                     # thân áo choàng
    ell(cl,22,12,7.5,7.5)                   # mũ trùm
    line(cl,10,30,7,42,3.2); line(cl,34,30,37,42,3.2)   # vạt áo rủ xuống đất
    ell(cl,22,40,14,4.5)                    # vạt áo trải trên đất
    rough(cl,5,0.72)
    dry_fill(im,cl,CLOAK,16,8,grain=(0.15,1),freq=1.6,seed=5,crack=0.04,dirw=0.5)
    # mặt sọ trong mũ trùm
    sk=blank(W,H); ell(sk,22,13.5,4.2,4.6)
    for y in range(H):
        for x in range(W):
            if sk[y][x]: px[x,y]=BONE[3] if (x+y)%3 else BONE[2]
    for (x,y) in ((20,13),(24,13),(20,14),(24,14)): px[x,y]=(14,10,10,255)   # hốc mắt
    px[22,16]=(30,22,20,255)
    for x in range(20,25): px[x,17]=BONE[1] if x%2 else BONE[4]               # răng
    # gối co (hai đầu gối xương nhô dưới áo)
    for cx in (15,29):
        k=blank(W,H); ell(k,cx,27,3.6,3.2)
        for y in range(H):
            for x in range(W):
                if k[y][x]: px[x,y]=CLOAK[2] if (x+y)%4 else CLOAK[3]
    # hai bàn tay xương ngửa trên đùi
    for hx,dirx in ((17,1),(27,-1)):
        for i in range(4):
            line_px=[(hx+dirx*i, 32-(i%2)), (hx+dirx*i, 33)]
            for (x,y) in line_px: px[x,y]=BONE[4] if i%2 else BONE[3]
        px[hx-dirx,33]=BONE[2]
    outline(im,OUT); return im
def rock_fill(im,mask,cx,cy,seed,crack=0.0):
    """Đá: khối tròn theo độ sâu + sáng trên-trái + lốm đốm nhiễu (không sọc)."""
    W,H=im.size; px=im.load(); d=depth(mask,W,H); md=max(max(r) for r in d) or 1
    for y in range(H):
        for x in range(W):
            if not mask[y][x]: continue
            v=min(1,d[y][x]/md)*0.5+0.7*(-(x-cx)-(y-cy))/max(W,H)+0.35
            v+=0.22*(vnoise(x*0.45,y*0.45,seed)-0.5)+0.12*(vnoise(x*1.3,y*1.3,seed+5)-0.5)
            if d[y][x]==1: v-=0.15
            v=max(0,min(0.999,v)); c=ROCK[int(v*len(ROCK))]
            if crack and vnoise(x*0.8,y*0.8,seed+9)<crack: c=ROCK[0]
            px[x,y]=c

def rubble(stage):
    """88x60: đống đá sập + cột chống gỗ gãy chắn cửa hầm. stage 0 = nguyên, 1–2 = nứt dần, 3 = mảnh vụn (sau khi phá)."""
    W,H=88,60; im=Image.new('RGBA',(W,H)); px=im.load()
    rnd=random.Random(17)
    if stage==3:
        for i in range(26):
            cx,cy=rnd.uniform(8,80),rnd.uniform(40,57); m=blank(W,H); ell(m,cx,cy,rnd.uniform(1.6,3.6),rnd.uniform(1.2,2.4)); rough(m,i,0.8)
            L=Image.new('RGBA',(W,H)); rock_fill(L,m,cx-1,cy-1,i); outline(L,OUT); im.alpha_composite(L)
        b=blank(W,H); line(b,20,54,40,51,1.8); L=Image.new('RGBA',(W,H)); dry_fill(L,b,WOOD,20,50,grain=(1,0.1),freq=2.5,seed=3,crack=0); outline(L,OUT); im.alpha_composite(L)
        return im
    stones=[(44,40,18,15),(24,46,13,11),(64,45,14,12),(34,26,11,10),(56,24,10,9),(12,52,8,6),(78,53,8,6),(46,14,8,7),(30,55,9,5),(60,56,10,4)]
    for i,(cx,cy,rx,ry) in enumerate(stones):
        m=blank(W,H); ell(m,cx,cy,rx,ry); rough(m,30+i,0.7)
        L=Image.new('RGBA',(W,H)); rock_fill(L,m,cx-rx*0.4,cy-ry*0.5,40+i,0.04*stage); outline(L,OUT); im.alpha_composite(L)
    # cột chống gãy chéo
    b=blank(W,H); line(b,8,20,40,50,2.4); line(b,58,12,80,36,2.0)
    L=Image.new('RGBA',(W,H)); dry_fill(L,b,WOOD,10,14,grain=(0.7,0.7),freq=2.2,seed=9,crack=0.0,dirw=0.5); outline(L,OUT); im.alpha_composite(L)
    # vết nứt theo giai đoạn
    for s in range(stage):
        x,y=40+s*8,18
        for k in range(22):
            x+=rnd.choice((-1,0,1)); y+=1
            if 0<=x<W and 0<=y<H and px[x,y][3] and px[x,y]!=OUT: px[x,y]=(18,12,10,255)
    return im
if __name__=='__main__':
    h=hermit(); rs=[rubble(i) for i in range(4)]
    S=5; sh=Image.new('RGBA',(h.width*S+sum(r.width*3 for r in rs)+60,max(h.height*S,rs[0].height*3)+20),(30,30,36,255))
    sh.alpha_composite(h.resize((h.width*S,h.height*S),Image.NEAREST),(4,4)); x=h.width*S+20
    for r in rs: sh.alpha_composite(r.resize((r.width*3,r.height*3),Image.NEAREST),(x,4)); x+=r.width*3+8
    sh.save('preview/hollow_art.png')
    if len(sys.argv)>1 and sys.argv[1]=='write':
        h.save('../../Assets/GamePackage/ERW - Grass Land 2.0 v2.1/Props/SR-trimmed/hermit_remains.png')
        for i,r in enumerate(rs): r.save('../../Assets/Resources/VFX/props/'+('rubble_broken' if i==3 else 'rubble_%d'%i)+'.png')
        print('written')
    print('ok')
