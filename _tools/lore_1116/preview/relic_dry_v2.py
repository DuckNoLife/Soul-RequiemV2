# Thánh tích KHÔ QUẮT (bản 2, 16/11): da ướp như gỗ mục, nếp nhăn dọc thớ, vết nứt, khớp xương nhô.
# Không dây thần kinh, không rễ. Tim: ánh xanh dương rỉ qua kẽ nứt.
import math, random
from PIL import Image
from relic_art import depth, outline, blank, ell, line, SCLERA, BLUE, GREEN, RED

OUT=(20,14,10,255)
DRY=[(34,26,20,255),(58,45,35,255),(84,67,52,255),(112,92,72,255),(142,120,96,255),(172,152,124,255),(198,182,154,255)]
DRYH=[(30,14,14,255),(52,24,22,255),(78,38,32,255),(104,56,44,255),(132,78,60,255),(160,104,82,255)]   # tim: nâu đỏ khô
BONE=[(60,50,40,255),(98,86,70,255),(138,124,102,255),(176,162,136,255),(208,196,170,255),(228,220,198,255)]

def vnoise(x,y,seed):
    def h(i,j): 
        n=(i*374761393+j*668265263+seed*1442695041)&0xffffffff; n=(n^(n>>13))*1274126177&0xffffffff; return (n&0xffff)/65535
    i,j=int(math.floor(x)),int(math.floor(y)); fx,fy=x-i,y-j
    sx,sy=fx*fx*(3-2*fx),fy*fy*(3-2*fy)
    a=h(i,j)+(h(i+1,j)-h(i,j))*sx; b=h(i,j+1)+(h(i+1,j+1)-h(i,j+1))*sx
    return a+(b-a)*sy

def rough(mask,seed,amt=0.62):
    """Làm mép gồ ghề: gặm bớt pixel mép theo nhiễu."""
    H=len(mask); W=len(mask[0]); d=depth(mask,W,H)
    for y in range(H):
        for x in range(W):
            if d[y][x]==1 and vnoise(x*0.7,y*0.7,seed+99)>amt: mask[y][x]=False
    return mask

def dry_fill(im,mask,ramp,cx,cy,grain=(0,1),freq=1.6,seed=1,crack=0.10,glow=None,dirw=0.6):
    """Tô khối khô: độ sâu + hướng sáng, cộng thớ nhăn chạy theo `grain` (vector đơn vị), vết nứt tối; glow: màu rỉ qua kẽ nứt."""
    W,H=im.size; px=im.load(); d=depth(mask,W,H); md=max(max(r) for r in d) or 1
    gx,gy=grain; nx,ny=-gy,gx   # pháp tuyến của thớ
    for y in range(H):
        for x in range(W):
            if not mask[y][x]: continue
            v=min(1,d[y][x]/md)*0.55 + dirw*(-(x-cx)-(y-cy))/max(W,H) + 0.22
            # thớ nhăn: sóng theo pháp tuyến, uốn bởi nhiễu
            u=(x*nx+y*ny)*freq + 3.0*vnoise(x*0.25,y*0.25,seed)
            w=math.sin(u)
            v+= 0.26*w
            n2=vnoise(x*0.5,y*0.5,seed+7)
            cr = w<-0.72 and n2>0.30            # nếp nhăn sâu
            if d[y][x]==1: v-=0.12              # mép tối, co rút
            v=max(0,min(0.999,v))
            c=ramp[int(v*len(ramp))]
            if cr: c=ramp[0]
            if vnoise(x*0.9,y*0.9,seed+3)<crack: c=(glow if glow and d[y][x]>1 else ramp[0])
            px[x,y]=c

def eye(right=False):
    """Nhãn cầu: không dây thần kinh. Lòng trắng ngà cũ, tròng xanh dương; vân nhiễm: trái xanh lá, phải đỏ."""
    W=H=24; im=Image.new('RGBA',(W,H)); px=im.load()
    cx,cy,r=11.5,11.5,9.4
    IVORY=[(110,96,78,255),(156,142,120,255),(196,186,164,255),(222,214,194,255),(240,236,222,255)]
    m=blank(W,H); ell(m,cx,cy,r,r)
    d=depth(m,W,H); md=max(max(rr) for rr in d)
    for y in range(H):
        for x in range(W):
            if m[y][x]:
                v=min(1,d[y][x]/md)*0.75+0.9*(-(x-cx+3)-(y-cy+3))/W+0.15
                v=max(0,min(0.999,v)); px[x,y]=IVORY[int(v*len(IVORY))]
    vein=GREEN if not right else RED
    for a in (0.3,1.4,2.3,3.4,4.4,5.4):
        x0,y0=cx+math.cos(a)*r*0.93, cy+math.sin(a)*r*0.93
        for s in range(5):
            t=s/5; x=x0-math.cos(a)*t*3.6+math.sin(a*3+s)*0.6; y=y0-math.sin(a)*t*3.6
            if px[int(x),int(y)][3]: px[int(x),int(y)]=vein[1 if s<3 else 0]
    ix,iy=cx+(1.4 if not right else -1.4),cy+0.8
    for y in range(H):
        for x in range(W):
            dd=math.hypot(x+.5-ix,y+.5-iy)
            if dd<=4.6 and px[x,y][3]:
                px[x,y]=(10,16,30,255) if dd<=1.7 else (BLUE[2] if (x+y)%3 else BLUE[3]) if dd<=3.0 else BLUE[1] if dd<=3.9 else (vein[0] if right else BLUE[0])
    px[int(ix-2),int(iy-2)]=(255,255,255,255); px[int(cx-4),int(cy-5)]=(255,255,255,255)
    outline(im,OUT); return im

def arm_left():
    """Tay trái khô quắt như cành gỗ mục: cẳng gầy gồ ghề, khuỷu / cổ tay nhô xương, bàn tay co như vuốt, khớp ngón to."""
    W,H=30,58; im=Image.new('RGBA',(W,H)); px=im.load(); m=blank(W,H)
    line(m,16,55,15.4,46,3.1); line(m,15.4,46,15,36,2.5)           # cánh tay trên teo
    ell(m,15,35,3.0,2.6)                                            # khuỷu nhô
    line(m,15,35,13.2,22,2.0)                                       # cẳng tay gầy
    line(m,14.6,30,13.6,24,2.3)                                     # bắp cơ khô còn sót
    ell(m,13,21.5,2.8,2.0)                                          # cổ tay nhô
    ell(m,12.6,16.8,4.2,3.6)                                        # bàn tay
    fingers=[((8.8,14.5),(5.4,9.6),(6.4,4.6)),((11.2,13.2),(9.6,6.6),(11.2,2.6)),((13.8,13.2),(15.0,6.8),(17.4,3.6)),((15.8,14.6),(19.2,10.4),(20.8,7.0))]
    for a_,b_,c_ in fingers:
        line(m,*a_,*b_,1.05); line(m,*b_,*c_,0.8); ell(m,b_[0],b_[1],1.45,1.45)
    line(m,16.6,18.2,21.4,16.6,1.1); line(m,21.4,16.6,23.4,13.0,0.85); ell(m,21.4,16.6,1.4,1.4)   # ngón cái
    rough(m,11,0.66)
    dry_fill(im,m,DRY,9,12,grain=(0.1,1),freq=1.15,seed=11,crack=0.05)
    for b_ in [f[1] for f in fingers]+[(21.4,16.6),(15,35),(13,21.5)]:
        x,y=int(b_[0]-0.5),int(b_[1]-0.5)
        if px[x,y][3]: px[x,y]=DRY[6]
    for f in fingers:                                                # móng đen khô ở đầu ngón
        x,y=int(f[2][0]),int(f[2][1])
        if px[x,y][3]: px[x,y]=DRY[0]
    for x in range(12,20):                                           # mặt cắt ở vai: xương lộ
        if px[x,56][3]: px[x,56]=BONE[3 if x%2 else 2]
    outline(im,OUT); return im

def ears():
    """Đôi tai teo quắt, nhăn, mép cong vào, rách nhẹ."""
    W,H=34,26; im=Image.new('RGBA',(W,H)); px=im.load()
    for ox,flip,seed in ((9.5,False,21),(24.5,True,22)):
        f=1 if not flip else -1
        m=blank(W,H)
        ell(m,ox,11,5.6,8.4); ell(m,ox-f*0.6,18.6,2.8,3.2)
        ell(m,ox-f*4.8,13,1.8,3.0,False)                      # khuyết trước (bình tai)
        ell(m,ox+f*5.4,7.0,1.0,1.4,False)                     # mép rách
        rough(m,seed,0.70)
        dry_fill(im,m,DRY,ox-2,7,grain=(f*0.3,1),freq=1.5,seed=seed,crack=0.04)
        d=depth(m,W,H)
        for y in range(H):
            for x in range(W):
                if not m[y][x]: continue
                back=(x+.5-ox)*f>-1.0 or y<7
                if back and y<16:
                    if d[y][x]==1: px[x,y]=DRY[5]
                    elif d[y][x]==2: px[x,y]=DRY[1]
                dd=((x+.5-(ox-f*0.8))/2.3)**2+((y+.5-12.8)/3.4)**2
                if dd<=1: px[x,y]=DRY[0] if dd<0.4 else DRY[1]
        tx=int(ox-f*3.4)
        for y in (12,13): 
            if m[y][tx]: px[tx,y]=DRY[4]
    outline(im,OUT); return im

def heart():
    """Tim khô teo như quả sung khô, nhăn sâu; ánh xanh dương (Ether nguyên bản) rỉ qua kẽ nứt."""
    W,H=26,28; im=Image.new('RGBA',(W,H)); px=im.load(); m=blank(W,H)
    ell(m,9.5,14,6.4,6.8); ell(m,15.8,13.6,5.8,6.6)
    for y in range(H):
        for x in range(W):
            if 17<=y<=25 and abs(x+.5-12.6+(y-17)*0.2)<=(25-y)*0.75: m[y][x]=True
    ell(m,3.6,12,1.3,1.6,False); ell(m,22,17,1.2,1.8,False)   # co rút, méo
    t=blank(W,H); line(t,12,9,11.6,4.2,1.7); line(t,11.6,4.2,15.8,3.4,1.4); line(t,15.8,3.4,17.4,6.6,1.2); line(t,7.6,8.6,6.6,4.6,1.0)
    dry_fill(im,t,DRYH,10,4,grain=(1,0.2),freq=1.8,seed=31,crack=0.0)
    for (x,y) in ((11,3),(16,3),(6,4)):
        if px[x,y][3]: px[x,y]=DRYH[0]                            # miệng ống khô
    dry_fill(im,m,DRYH,8,9,grain=(0.35,1),freq=1.25,seed=33,crack=0.0)
    # kẽ nứt sâu rỉ ánh xanh
    for (x0,y0,a,n) in ((9,11,1.75,8),(16,10,1.35,8),(12.5,18,1.55,6),(18,15,2.2,5)):
        for s in range(n):
            x=x0+math.cos(a)*s*0.95+math.sin(s*1.7)*0.6; y=y0+math.sin(a)*s*0.95
            if 0<=int(x)<W and 0<=int(y)<H and px[int(x),int(y)][3]:
                px[int(x),int(y)]=BLUE[2] if s%3 else BLUE[3]
    outline(im,OUT); return im

def spine():
    """Cột sống khô: đốt xương ố vàng, cong vẹo, gân khô nối các đốt; không rễ."""
    W,H=18,42; im=Image.new('RGBA',(W,H)); px=im.load()
    cx=W/2-0.5; n=9
    sin=blank(W,H)
    for i in range(n-1):
        y=4+i*4; x=cx+1.4*math.sin(i*0.6)
        x2=cx+1.4*math.sin((i+1)*0.6)
        line(sin,x,y+1.5,x2,y+5.5,1.8)
    dry_fill(im,sin,DRY,cx,10,grain=(0,1),freq=2.2,seed=41,crack=0.0)
    for i in range(n):
        y0=3+i*4; x=cx+1.4*math.sin(i*0.6)
        w=2.6+(0.8 if 2<=i<=6 else 0)
        m=blank(W,H); ell(m,x,y0+1.8,w,2.1)
        line(m,x-w-1.2,y0+1.2,x+w+1.2,y0+1.6,0.75)                    # mấu ngang ngắn
        line(m,x,y0+2,x+0.6,y0+3.6,0.6)                                 # mấu gai
        if i in (3,6): ell(m,x+w+1.6,y0+1.6,0.9,0.9,False)             # mẻ
        dry_fill(im,m,BONE,x-2,y0,grain=(1,0),freq=2.4,seed=50+i,crack=0.05,dirw=0.9)
    outline(im,OUT); return im

if __name__=='__main__':
    import sys, os
    parts=[('relic_eye_left',eye(False)),('relic_eye_right',eye(True)),('relic_ears',ears()),
           ('relic_arm_left',arm_left()),('relic_heart',heart()),('relic_spine',spine())]
    S=6; cw=max(i.width for _,i in parts)*S+20; ch=max(i.height for _,i in parts)*S+20
    sheet=Image.new('RGBA',(cw*len(parts),ch+40),(36,34,38,255))
    for k,(n,i) in enumerate(parts):
        big=i.resize((i.width*S,i.height*S),Image.NEAREST)
        sheet.alpha_composite(big,(k*cw+(cw-big.width)//2,(ch-big.height)//2))
        sheet.alpha_composite(i,(k*cw+(cw-i.width)//2,ch+4))
    sheet.save('preview/relics_dry_sheet.png')
    if len(sys.argv)>1:
        for n,i in parts: i.save(os.path.join(sys.argv[1],n+'.png'))
    print('ok')
