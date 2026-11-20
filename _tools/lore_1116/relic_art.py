# Thánh tích của The Ascended (the Ascended's Remains) — vẽ bằng code, pixel art.
# Mỗi hàm trả về PIL Image RGBA. Khối được tô theo "độ sâu" (khoảng cách tới mép) + ánh sáng từ trên-trái.
import math
from PIL import Image

OUT=(24,12,10,255)

def depth(mask,W,H):
    d=[[0]*W for _ in range(H)]
    cur=[[1 if mask[y][x] else 0 for x in range(W)] for y in range(H)]
    k=0
    while any(any(r) for r in cur):
        k+=1; nxt=[[0]*W for _ in range(H)]
        for y in range(H):
            for x in range(W):
                if cur[y][x]:
                    d[y][x]=k
                    if all(0<=x+u<W and 0<=y+v<H and cur[y+v][x+u] for u,v in((1,0),(-1,0),(0,1),(0,-1))): nxt[y][x]=1
        cur=nxt
    return d

def shade(im,mask,ramp,cx,cy,dirw=0.55,maxd=None,keep=None):
    """Tô vùng mask bằng ramp (tối -> sáng)."""
    W,H=im.size; px=im.load(); d=depth(mask,W,H)
    md=maxd or max(max(r) for r in d) or 1
    for y in range(H):
        for x in range(W):
            if not mask[y][x]: continue
            if keep and keep(x,y): continue
            v=min(1,d[y][x]/md)*0.75 + dirw*(-(x-cx)-(y-cy))/max(W,H)
            v=max(0,min(0.999,v+0.15))
            px[x,y]=ramp[int(v*len(ramp))]

def outline(im,col=OUT):
    W,H=im.size; px=im.load(); a=[[px[x,y][3] for x in range(W)] for y in range(H)]
    for y in range(H):
        for x in range(W):
            if not a[y][x] and any(0<=x+u<W and 0<=y+v<H and a[y+v][x+u] for u,v in((1,0),(-1,0),(0,1),(0,-1))): px[x,y]=col

def blank(W,H): return [[False]*W for _ in range(H)]
def ell(m,cx,cy,rx,ry,val=True):
    H=len(m); W=len(m[0])
    for y in range(H):
        for x in range(W):
            if ((x+.5-cx)/rx)**2+((y+.5-cy)/ry)**2<=1: m[y][x]=val
def line(m,x0,y0,x1,y1,r,val=True):
    H=len(m); W=len(m[0]); n=int(max(abs(x1-x0),abs(y1-y0))*3)+1
    for i in range(n+1):
        t=i/n; x=x0+(x1-x0)*t; y=y0+(y1-y0)*t
        for yy in range(int(y-r-1),int(y+r+2)):
            for xx in range(int(x-r-1),int(x+r+2)):
                if 0<=xx<W and 0<=yy<H and (xx+.5-x)**2+(yy+.5-y)**2<=r*r: m[yy][xx]=val

SCLERA=[(120,104,92,255),(170,156,140,255),(212,202,186,255),(236,230,216,255),(252,250,242,255)]
FLESH =[(62,30,24,255),(98,52,38,255),(138,82,60,255),(176,122,92,255),(208,160,124,255)]
HEART =[(48,8,14,255),(86,14,24,255),(130,24,36,255),(172,44,54,255),(210,84,90,255)]
BLUE  =[(18,40,90,255),(42,111,192,255),(108,196,255,255),(200,236,255,255)]
GREEN =[(28,70,30,255),(70,150,60,255),(150,220,110,255)]
RED   =[(90,14,24,255),(170,36,48,255),(230,96,104,255)]

def eye(right=False,taint='green'):
    """Nhãn cầu thánh tích: tròng xanh dương (thánh thể), vân nhiễm (trái: xanh lá Plague Eye; phải: đỏ Vael), dây thần kinh khô."""
    W=H=26; im=Image.new('RGBA',(W,H)); px=im.load()
    cx,cy,r=12.5,11.5,9.2
    # dây thần kinh khô thò ra phía sau
    nm=blank(W,H); sx=-1 if right else 1
    for k in range(18):
        t=k/17; x=cx+sx*(4+t*7.5)+math.sin(t*5)*1.1; y=cy+5+t*7.5-t*t*2
        line(nm,x,y,x,y,2.2*(1-t)+0.7)
    NERVE=[(70,26,30,255),(112,46,48,255),(150,76,72,255),(184,112,100,255)]
    shade(im,nm,NERVE,cx,cy,0.2)
    m=blank(W,H); ell(m,cx,cy,r,r); shade(im,m,SCLERA,cx-3,cy-3,0.9)
    # vân máu / vân nhiễm
    vein=GREEN if taint=='green' else RED
    for k,a in enumerate((0.3,1.4,2.3,3.4,4.4,5.4)):
        x0,y0=cx+math.cos(a)*r*0.92, cy+math.sin(a)*r*0.92
        for s in range(5):
            t=s/5; x=x0-math.cos(a)*t*3.6+math.sin(a*3+s)*0.6; y=y0-math.sin(a)*t*3.6
            if 0<=int(x)<W and 0<=int(y)<H and px[int(x),int(y)][3]: px[int(x),int(y)]=vein[1 if s<3 else 0]
    # tròng (nhìn hơi lệch, như vật đã chết)
    ix,iy=cx+(1.5 if not right else -1.5),cy+1.0
    for y in range(H):
        for x in range(W):
            dd=math.hypot(x+.5-ix,y+.5-iy)
            if dd<=4.6 and px[x,y][3]:
                if dd<=1.7: px[x,y]=(10,16,30,255)
                elif dd<=3.0: px[x,y]=BLUE[2] if (x+y)%3 else BLUE[3]
                elif dd<=3.9: px[x,y]=BLUE[1]
                else: px[x,y]=vein[0] if taint=='red' else BLUE[0]
    # chấm sáng ẩm
    px[int(ix-2),int(iy-2)]=(255,255,255,255); px[int(cx-4),int(cy-5)]=(255,255,255,255); px[int(cx-5),int(cy-5)]=SCLERA[4]
    outline(im); return im

def ears():
    """Đôi tai khô (một cặp), da nâu ướp, vành sáng, hõm tai tối; một sợi rễ đỏ nối hai tai (của Morrow)."""
    W,H=34,26; im=Image.new('RGBA',(W,H)); px=im.load()
    def one(ox,flip):
        m=blank(W,H)
        ell(m,ox,11,5.6,8.4); ell(m,ox+(0.6 if flip else -0.6),18.5,3.0,3.4)
        # khuyết phía trước (bình tai)
        fx=ox+(-4.6 if not flip else 4.6)
        ell(m,fx,13,1.8,3.0,False)
        shade(im,m,FLESH,ox-2,8,0.7)
        d=depth(m,W,H); f=1 if not flip else -1          # f: phía trước mặt (-x với tai phải)
        for y in range(H):
            for x in range(W):
                if not m[y][x]: continue
                back=(x+.5-ox)*f>-1.2 or y<8                   # nửa sau + đỉnh = vành tai
                if back and y<17:
                    if d[y][x]==1: px[x,y]=FLESH[4] if y<12 else FLESH[3]     # vành sáng
                    elif d[y][x]==2: px[x,y]=FLESH[1]                          # rãnh vành
        # hõm tai chữ C
        cxo=ox-f*0.6
        for y in range(H):
            for x in range(W):
                dd=((x+.5-cxo)/2.3)**2+((y+.5-12.8)/3.3)**2
                if dd<=1 and m[y][x]: px[x,y]=FLESH[0] if dd<0.35 else FLESH[1]
        # bình tai (tragus) nhô trước hõm
        tx=int(ox-f*3.2)
        for y in (12,13,14):
            if 0<=tx<W and m[y][tx]: px[tx,y]=FLESH[3]
    one(9.5,False); one(24.5,True)
    outline(im)
    # sợi rễ đỏ nối hai tai (ghép trên người Morrow)
    for s in range(40):
        t=s/39; x=10+t*14; y=22.5+math.sin(t*math.pi)*2.2
        if px[int(x),int(y)][3]==0 or px[int(x),int(y)]==OUT: px[int(x),int(y)]=RED[1] if s%7 else RED[2]
    return im

def arm_left():
    """Cánh tay trái khô của thánh (vai -> bàn tay ngửa, ngón xoè), da ướp nâu, gân tối; đầu vai quấn rễ đỏ (chỗ Absalom ghép)."""
    W,H=24,52; im=Image.new('RGBA',(W,H)); px=im.load()
    m=blank(W,H)
    line(m,13,49,12,32,3.2)    # cánh tay trên
    line(m,12,32,10,17,2.6)    # cẳng tay
    ell(m,10.5,13.5,3.6,3.8)   # lòng bàn tay
    # ngón tay (trái: ngón cái bên phải khi ngửa bàn)
    for (x0,y0,x1,y1) in ((7.6,11,6.2,3.5),(9.6,10,9.2,1.8),(11.6,10,12.0,2.4),(13.2,11,14.6,4.6)):
        line(m,x0,y0,x1,y1,0.85)
    line(m,13.6,14.5,17.4,11.0,1.0)   # ngón cái
    shade(im,m,FLESH,8,14,0.5)
    # khớp, gân, xương lộ ở khuỷu
    for (x,y) in ((12,32),(11,31),(13,33)): px[x,y]=SCLERA[2]
    for y in range(18,46,3): 
        x=int(12.5+(y-32)*-0.05)+(1 if y%2 else 0)
        if px[x,y][3]: px[x,y]=FLESH[0]
    for (x,y) in ((6,4),(9,2),(12,3),(14,5),(17,11)): 
        if px[x,y][3]: px[x,y]=SCLERA[1]   # móng
    outline(im)
    # rễ đỏ quấn đầu vai
    for s in range(70):
        t=s/69; y=40+t*11; x=13+4.2*math.sin(t*11)
        front=math.cos(t*11)>0
        if 0<=int(x)<W and int(y)<H and (front or px[int(x),int(y)][3]==0): px[int(x),int(y)]=RED[1] if front else RED[0]
    return im

def heart():
    """Trái tim thánh: thịt đỏ sẫm còn sống, mạch máu là ánh xanh dương (Ether nguyên bản, không nhiễm Vael), cuống động mạch phía trên."""
    W,H=28,30; im=Image.new('RGBA',(W,H)); px=im.load()
    m=blank(W,H)
    ell(m,10,15,7.2,7.6); ell(m,17,14.5,6.6,7.4)
    for y in range(H):
        for x in range(W):   # phần nhọn phía dưới
            if 18<=y<=27 and abs(x+.5-13.5+(y-18)*0.15)<=(27-y)*0.85: m[y][x]=True
    # cung động mạch + 2 ống
    tube=blank(W,H)
    line(tube,13,9,12.5,3.5,2.2); line(tube,12.5,3.5,17.5,3.0,1.9); line(tube,17.5,3.0,19.5,7.5,1.7)
    line(tube,8.5,9,7.2,4.2,1.4)
    shade(im,tube,HEART,10,4,0.5)
    shade(im,m,HEART,9,10,0.8)
    # vách ngăn
    for s in range(14):
        t=s/13; x=14.5+math.sin(t*2.6)*1.2-t*1.6; y=9+t*13
        if px[int(x),int(y)][3]: px[int(x),int(y)]=HEART[0]
    # mạch xanh dương phát sáng
    for (x0,y0,ang) in ((10,12,1.9),(17,11,1.3),(12,19,1.6),(18,17,2.1)):
        for s in range(7):
            x=x0+math.cos(ang)*s*0.9+math.sin(s)*0.5; y=y0+math.sin(ang)*s*0.9
            if 0<=int(x)<W and 0<=int(y)<H and px[int(x),int(y)][3]: px[int(x),int(y)]=BLUE[2] if s%3 else BLUE[3]
    px[7,11]=(255,220,224,255); px[8,10]=(255,200,206,255)
    outline(im); return im

if __name__=='__main__':
    import sys, os
    from spine_proto import spine
    parts=[('relic_eye_left',eye(False,'green')),('relic_eye_right',eye(True,'red')),('relic_ears',ears()),
           ('relic_arm_left',arm_left()),('relic_heart',heart()),('relic_spine',spine())]
    S=6; cw=max(i.width for _,i in parts)*S+20; ch=max(i.height for _,i in parts)*S+20
    sheet=Image.new('RGBA',(cw*len(parts),ch+40),(36,34,38,255))
    for k,(n,i) in enumerate(parts):
        big=i.resize((i.width*S,i.height*S),Image.NEAREST)
        sheet.alpha_composite(big,(k*cw+(cw-big.width)//2,(ch-big.height)//2))
        # cỡ thật bên dưới
        sheet.alpha_composite(i,(k*cw+(cw-i.width)//2,ch+4))
    sheet.save('preview/relics_sheet.png')
    if len(sys.argv)>1:
        for n,i in parts: i.save(os.path.join(sys.argv[1],n+'.png'))
    print('ok')
