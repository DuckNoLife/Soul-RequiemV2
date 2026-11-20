# Map 1b Hermit's Hollow — vẽ bộ tile SR-mine (trần đá tối + mép đá, vũng Ether xanh, vân quặng, bậc đá "đứng yên", vạch đếm năm...)
# python3 hollow_tiles.py   -> Props/SR-mine/*.png + TiledMap Editor/Tilesets/SR-mine.tsx (image collection)
import os, random, math
from PIL import Image, ImageDraw, ImageFilter
ROOT="../../Assets/GamePackage/ERW - Grass Land 2.0 v2.1/"
OUT=ROOT+"Props/SR-mine/"
os.makedirs(OUT,exist_ok=True)
def hx(s): s=s.lstrip('#'); return tuple(int(s[i:i+2],16) for i in (0,2,4))
CEIL=[hx('#18110e'),hx('#1d1511'),hx('#140e0b'),hx('#221914')]
ROCK_D,ROCK_M,ROCK_L,ROCK_H,ROCK_S=hx('#5e4535'),hx('#7d5c45'),hx('#957257'),hx('#b18664'),hx('#3b2a20')
files=[]
def save(im,name):
    im.save(OUT+name+'.png'); files.append((name,im.size))

def ceiling(seed):
    rnd=random.Random(seed); im=Image.new('RGBA',(32,32),CEIL[0]+(255,)); px=im.load()
    for y in range(32):
        for x in range(32):
            v=rnd.random()
            if v<0.10: px[x,y]=CEIL[1]+(255,)
            elif v<0.16: px[x,y]=CEIL[2]+(255,)
            elif v<0.18: px[x,y]=CEIL[3]+(255,)
    # vài khối đá mờ cho đỡ phẳng (liền mạch giữa các ô vì chỉ là đốm nhỏ)
    for _ in range(3):
        cx,cy=rnd.randrange(4,28),rnd.randrange(4,28); r=rnd.randrange(2,4)
        for y in range(cy-r,cy+r+1):
            for x in range(cx-r,cx+r+1):
                if 0<=x<32 and 0<=y<32 and (x-cx)**2+(y-cy)**2<=r*r: px[x,y]=CEIL[3]+(255,)
        if 0<=cy+r+1<32:
            for x in range(cx-r+1,cx+r): 
                if 0<=x<32: px[x,cy+r+1]=CEIL[2]+(255,)
    return im

def lip_profile(rnd,n=32,base=5,amp=2):
    # mép đá gãy: độ dày thay đổi từ từ theo từng đoạn
    out=[];v=base
    for i in range(n):
        if i%4==0: v=max(3,min(base+amp,v+rnd.choice((-1,0,0,1))))
        out.append(v)
    return out

def draw_lip(im,edge,rnd):
    px=im.load(); prof=lip_profile(rnd)
    for i in range(32):
        d=prof[i]
        for k in range(d):
            # k=0 sát mép (sáng nhất) -> vào trong tối dần
            col=ROCK_H if k==0 else ROCK_L if k==1 else ROCK_M if k<d-1 else ROCK_D
            if k>=2 and rnd.random()<0.18: col=ROCK_D
            if edge=='N': x,y=i,k
            elif edge=='S': x,y=i,31-k
            elif edge=='W': x,y=k,i
            else: x,y=31-k,i
            px[x,y]=col+(255,)
        # bóng một px sau lớp đá
        k=d
        if edge=='N': x,y=i,k
        elif edge=='S': x,y=i,31-k
        elif edge=='W': x,y=k,i
        else: x,y=31-k,i
        px[x,y]=ROCK_S+(255,)

# 16 ô trần theo mặt nạ cạnh hở: N=1 E=2 S=4 W=8 (cạnh hở = có lối đi / mặt vách bên cạnh)
for m in range(16):
    rnd=random.Random(100+m); im=ceiling(7+m)
    for bit,e in ((1,'N'),(2,'E'),(4,'S'),(8,'W')):
        if m&bit: draw_lip(im,e,rnd)
    save(im,'ceil_%02d'%m)
# trần trơn thêm 3 biến thể
for v in range(3): save(ceiling(300+v),'ceil_plain_%d'%v)

# góc trong: chỉ đường chéo hở -> một mẩu mép nhỏ ở góc (lớp phủ trong suốt)
for name,(cx,cy) in {'ic_ne':(31,0),'ic_se':(31,31),'ic_sw':(0,31),'ic_nw':(0,0)}.items():
    im=Image.new('RGBA',(32,32),(0,0,0,0)); px=im.load()
    for y in range(32):
        for x in range(32):
            d=max(abs(x-cx),abs(y-cy))
            if d<=5:
                col=ROCK_H if d==0 else ROCK_L if d<=1 else ROCK_M if d<=4 else ROCK_S
                px[x,y]=col+(255,)
    save(im,name)

# vân quặng xanh trên mặt vách (đồng gỉ xanh — Ether rỉ qua) : lớp phủ 32x32, alpha < 255 = phát sáng nhẹ
GREEN=[hx('#2f7d55'),hx('#46b07a'),hx('#7cf0b0'),hx('#c9ffe4')]
for v in range(3):
    rnd=random.Random(500+v); im=Image.new('RGBA',(32,32),(0,0,0,0)); d=ImageDraw.Draw(im)
    x,y=rnd.randrange(4,12),rnd.randrange(2,8); pts=[(x,y)]
    for _ in range(6):
        x+=rnd.randrange(2,6); y+=rnd.randrange(-2,6); pts.append((min(31,x),max(0,min(31,y))))
    d.line(pts,fill=GREEN[0]+(255,),width=3); d.line(pts,fill=GREEN[1]+(255,),width=2); d.line(pts,fill=GREEN[2]+(230,),width=1)
    for p in pts[1:-1:2]:
        d.point(p,fill=GREEN[3]+(220,))
    save(im,'vein_%d'%v)

# vũng Ether rỉ (xanh), hình bầu dục méo, viền sáng
def pool(w,h,seed):
    rnd=random.Random(seed); im=Image.new('RGBA',(w,h),(0,0,0,0)); px=im.load()
    cx,cy=w/2,h/2; ph=[rnd.uniform(0,6.28) for _ in range(3)]
    def r(a): return 1+0.10*math.sin(3*a+ph[0])+0.07*math.sin(5*a+ph[1])+0.05*math.sin(2*a+ph[2])
    for y in range(h):
        for x in range(w):
            dx,dy=(x+0.5-cx)/(w/2-1),(y+0.5-cy)/(h/2-1); a=math.atan2(dy,dx); q=math.hypot(dx,dy)/r(a)*1.12
            if q<=1:
                if q>0.86: col=GREEN[2]+(235,)
                elif q>0.78: col=GREEN[1]+(230,)
                else:
                    t=q/0.78; base=hx('#0f3a28'); 
                    col=tuple(int(base[i]*(1-t*0.4)+GREEN[0][i]*t*0.4) for i in range(3))+(225,)
                    if rnd.random()<0.025: col=GREEN[2]+(200,)
                px[x,y]=col
    return im
save(pool(96,56,1),'seep_a'); save(pool(64,40,2),'seep_b'); save(pool(128,64,3),'seep_c'); save(pool(48,32,4),'seep_d')

# bậc đá "đứng yên": phiến đá phẳng mòn, khắc vòng tròn nhỏ (dấu người ẩn sĩ) — đánh dấu cột an toàn ở The Still Walk
for v in range(3):
    rnd=random.Random(700+v); im=Image.new('RGBA',(32,32),(0,0,0,0)); d=ImageDraw.Draw(im)
    g1,g2,g3=hx('#77706a'),hx('#8f877d'),hx('#5a534d')
    d.rounded_rectangle((2,3,29,29),5,fill=g3+(255,)); d.rounded_rectangle((2,2,29,27),5,fill=g1+(255,))
    d.rounded_rectangle((4,3,27,12),4,fill=g2+(255,))
    for _ in range(14):
        x,y=rnd.randrange(4,28),rnd.randrange(4,26); d.point((x,y),fill=g3+(255,))
    d.ellipse((11,10,20,19),outline=hx('#3e3833')+(255,)); d.point((15,14),fill=hx('#3e3833')+(255,))
    save(im,'still_slab_%d'%v)

# mũi tên cắm sàn (gợi ý làn bắn) và mũi tên gãy
for v in range(3):
    im=Image.new('RGBA',(32,32),(0,0,0,0)); d=ImageDraw.Draw(im)
    x=10+v*5; ang=[-0.25,0.15,0.4][v]
    x2=x+int(14*math.sin(ang)); y2=26-int(14*math.cos(ang))
    d.line((x,26,x2,y2),fill=hx('#6b4a2b')+(255,),width=2)
    d.polygon([(x2-3,y2),(x2+3,y2),(x2,y2-4)] if False else [(x2-3,y2+1),(x2+2,y2-3),(x2+1,y2+2)],fill=hx('#d8d0c0')+(255,))
    d.line((x-1,27,x+2,27),fill=hx('#3b2a20')+(200,))
    save(im,'arrow_stuck_%d'%v)

# vạch đếm năm khắc trên vách (14 nhóm năm vạch... ở đây: 14 vạch nhóm 5) 64x32
im=Image.new('RGBA',(64,32),(0,0,0,0)); d=ImageDraw.Draw(im); c=hx('#3b2a20')+(255,); h=hx('#c9a07c')+(255,)
x=4
for i in range(14):
    if i%5==4: d.line((x-16,8,x+1,22),fill=c,width=1); d.line((x-15,8,x+2,22),fill=h,width=1); x+=6; continue
    d.line((x,8,x,22),fill=c); d.line((x+1,8,x+1,22),fill=h); x+=4
save(im,'tally')

# lỗ máng quặng trên sàn buồng ẩn sĩ (64x48): miệng tối + viền gỗ
im=Image.new('RGBA',(64,48),(0,0,0,0)); d=ImageDraw.Draw(im)
d.rounded_rectangle((4,8,59,44),8,fill=hx('#6b4a2b')+(255,)); d.rounded_rectangle((8,12,55,40),6,fill=hx('#0b0806')+(255,))
d.rounded_rectangle((8,12,55,20),6,fill=hx('#1d1511')+(255,))
for x in (4,58): d.rectangle((x,6,x+1,46),fill=hx('#4a3220')+(255,))
d.line((6,9,57,9),fill=hx('#8a6440')+(255,))
save(im,'chute_top')


# bản "trong hang" của vài vật Grass Land: bỏ cỏ xanh ở chân (trong mỏ không có cỏ)
def strip_green(src):
    im=Image.open(ROOT+src).convert('RGBA'); px=im.load()
    for y in range(im.height):
        for x in range(im.width):
            r,g,b,a=px[x,y]
            if a and g>r+12 and g>b+12: px[x,y]=(0,0,0,0)
    return im
save(strip_green("Props/Static props/sheet1-sprites/doorway - entrance - gateway.png"),'mine_arch')
save(strip_green("Props/SR-trimmed/rocks on grass - color scheme 2 - 5.png"),'mine_rocks')
save(strip_green("Props/SR-trimmed/sign post.png"),'mine_signpost')
save(strip_green("Props/SR-trimmed/wooden table - rustic - on grass.png"),'mine_table')
# đống đá sập (64x40): khối đá màu vách mỏ
for v in range(2):
    rnd=random.Random(900+v); im=Image.new('RGBA',(64,40),(0,0,0,0)); d=ImageDraw.Draw(im)
    d.ellipse((4,28,60,39),fill=(20,14,10,120))
    rocks=[(rnd.randrange(6,50),rnd.randrange(10,26),rnd.randrange(7,13)) for _ in range(9)]
    rocks.sort(key=lambda r:r[1])
    for (x,y,r) in rocks:
        d.ellipse((x,y,x+r*1.3,y+r),fill=ROCK_D+(255,)); d.ellipse((x+1,y,x+r*1.2,y+r*0.7),fill=ROCK_M+(255,))
        d.ellipse((x+2,y+1,x+r*0.7,y+r*0.35),fill=ROCK_L+(255,)); d.point((x+3,y+1),fill=ROCK_H+(255,))
    save(im,'rockfall_%d'%v)


# 16/11 (§262): cửa gỗ chặn hầm nối buồng ẩn sĩ <-> phòng sập (top-down: nhìn mặt trước từ phía nam). 64x64
WOOD_D,WOOD_M,WOOD_L,IRON=hx('#3a2617'),hx('#6b4a2b'),hx('#8a6440'),hx('#3c3a38')
def door_frame():
    im=Image.new('RGBA',(64,64),(0,0,0,0)); d=ImageDraw.Draw(im)
    d.rectangle((2,4,9,63),fill=WOOD_D+(255,)); d.rectangle((3,4,8,63),fill=WOOD_M+(255,)); d.line((4,6,4,62),fill=WOOD_L+(255,))
    d.rectangle((54,4,61,63),fill=WOOD_D+(255,)); d.rectangle((55,4,60,63),fill=WOOD_M+(255,)); d.line((56,6,56,62),fill=WOOD_L+(255,))
    d.rectangle((0,0,63,9),fill=WOOD_D+(255,)); d.rectangle((1,1,62,7),fill=WOOD_M+(255,)); d.line((2,2,61,2),fill=WOOD_L+(255,))
    return im,d
im,d=door_frame()
for i,x in enumerate(range(10,54,11)):
    d.rectangle((x,10,x+10,63),fill=(WOOD_M if i%2 else WOOD_L)+(255,)); d.line((x,10,x,63),fill=WOOD_D+(255,))
for y in (18,46):
    d.rectangle((10,y,53,y+3),fill=IRON+(255,))
    for x in (13,24,35,46): d.point((x,y+1),fill=hx('#8a8580')+(255,))
d.rectangle((10,60,53,63),fill=WOOD_D+(255,))
save(im,'mine_door')
im,d=door_frame()
d.rectangle((10,10,53,63),fill=hx('#0e0a08')+(255,)); d.rectangle((10,10,53,20),fill=hx('#1d1511')+(255,))
d.rectangle((10,10,15,63),fill=WOOD_M+(255,)); d.line((15,10,15,63),fill=WOOD_D+(255,)); d.rectangle((10,20,15,22),fill=IRON+(255,)); d.rectangle((10,48,15,50),fill=IRON+(255,))
save(im,'mine_door_open')
# thanh chặn cửa (phía bắc, trong buồng ẩn sĩ) — nằm ngang trên giá
im=Image.new('RGBA',(64,20),(0,0,0,0)); d=ImageDraw.Draw(im)
for x in (4,56): d.rectangle((x,2,x+4,19),fill=WOOD_D+(255,))
d.rectangle((0,6,63,12),fill=WOOD_M+(255,)); d.line((0,7,63,7),fill=WOOD_L+(255,)); d.line((0,12,63,12),fill=WOOD_D+(255,))
save(im,'door_bar')
im=Image.new('RGBA',(64,20),(0,0,0,0)); d=ImageDraw.Draw(im)
for x in (4,56): d.rectangle((x,2,x+4,19),fill=WOOD_D+(255,))
save(im,'door_bar_off')

# ảnh có sẵn trong SR-trimmed (vẽ ở đợt E): hài cốt ẩn sĩ, thanh Oathbreaker nằm đất
EXTRA=[('../SR-trimmed/hermit_remains',(44,46)),('../SR-trimmed/relic_oathbreaker',(64,14))]
files+=EXTRA
# tsx (image collection)
tsx=['<?xml version="1.0" encoding="UTF-8"?>',
     '<tileset version="1.10" tiledversion="1.11.2" name="SR-mine" tilewidth="32" tileheight="32" tilecount="%d" columns="0">'%len(files),
     ' <grid orientation="orthogonal" width="1" height="1"/>']
for i,(n,(w,h)) in enumerate(files):
    tsx.append(' <tile id="%d">'%i); tsx.append('  <image source="../../Props/SR-mine/%s.png" width="%d" height="%d"/>'%(n,w,h)); tsx.append(' </tile>')
tsx.append('</tileset>')
open(ROOT+"TiledMap Editor/Tilesets/SR-mine.tsx",'w',newline='\r\n').write('\n'.join(tsx)+'\n')
import json; json.dump({n:i for i,(n,_) in enumerate(files)},open('hollow_ids.json','w'),indent=0)
print(len(files),'tiles')
