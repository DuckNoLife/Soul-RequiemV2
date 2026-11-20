# Cổ vật cột sống thánh (Specimen Zero): xương trắng ngà, rễ Ether đỏ quấn quanh. 20x40 px.
import math
from PIL import Image
W,H=20,40
OUT=(28,14,12,255); B0=(120,100,84,255); B1=(196,180,156,255); B2=(236,226,206,255); B3=(255,250,236,255)
RD=(110,18,30,255); RM=(192,48,60,255); RL=(240,110,116,255)
def spine():
    im=Image.new('RGBA',(W,H)); px=im.load()
    def put(x,y,c):
        if 0<=x<W and 0<=y<H: px[x,y]=c
    cx=W//2
    n=9
    for i in range(n):
        y0=3+i*4
        curve=int(round(1.2*math.sin(i*0.55)))   # cột sống hơi cong chữ S
        x=cx+curve
        w=3+ (1 if 2<=i<=6 else 0)               # thân đốt to ở giữa
        # mấu gai hai bên
        for dx in range(-w-2,w+3):
            put(x+dx,y0+1,B1 if abs(dx)<=w else B0)
        # thân đốt
        for dy in range(0,3):
            for dx in range(-w+ (1 if dy!=1 else 0), w+1-(1 if dy!=1 else 0)):
                put(x+dx,y0+dy, B3 if (dy==0 and dx<0) else (B2 if dy<2 else B1))
        # đĩa đệm tối
        for dx in range(-1,2): put(x+dx,y0+3,B0)
    # rễ đỏ quấn xoắn quanh cột
    for k,ph in enumerate((0.0,math.pi)):
        for s in range(260):
            t=s/259; y=2+t*35
            i=min(n-1,int((y-3)/4)); x=cx+1.2*math.sin(i*0.55)+5.2*math.sin(t*9.5+ph)
            front=math.cos(t*9.5+ph)>0
            c=(RL if front and s%17<3 else RM) if front else RD
            if front or px[int(x),int(y)][3]==0: put(int(x),int(y),c)
    # rễ thả xuống đáy
    for i,(dx,ln) in enumerate(((-3,4),(2,3),(4,2))):
        for s in range(ln): put(cx+dx+(s%2)*(1 if dx>0 else -1),37+s,RM)
    # viền
    a=[[px[x,y][3] for x in range(W)] for y in range(H)]
    for y in range(H):
        for x in range(W):
            if not a[y][x] and any(0<=x+u<W and 0<=y+v<H and a[y+v][x+u] for u,v in((1,0),(-1,0),(0,1),(0,-1))): px[x,y]=OUT
    return im
if __name__=='__main__':
    im=spine(); bg=Image.new('RGBA',(W*7,H*7),(40,40,44,255)); bg.alpha_composite(im.resize((W*7,H*7),Image.NEAREST)); bg.save('preview/spine_proto.png')
