# Hình nhặt Crimson Oath 36x56 (đặt chéo trên nắp quan tài Borrow) theo thiết kế lò rèn mới (16/11, §256).
import math
from PIL import Image
from katana_forge import outline
def draw():
    W,H=36,56; im=Image.new('RGBA',(W,H)); px=im.load()
    def put(x,y,c):
        if 0<=x<W and 0<=y<H: px[x,y]=c+(255,) if len(c)==3 else c
    ax,ay,bx,by=11,40,30,4                     # gốc lưỡi -> mũi
    L=math.hypot(bx-ax,by-ay); ux,uy=(bx-ax)/L,(by-ay)/L; nx,ny=-uy,ux
    for i in range(int(L)+1):
        t=i/L; cx,cy=ax+ux*i,ay+uy*i
        hw=2.4*(1-0.3*t) if t<0.9 else 2.4*0.7*(1-(t-0.9)/0.1)
        for k in range(-3,4):
            o=k*0.8
            if abs(o)>hw: continue
            x,y=int(round(cx+nx*o)),int(round(cy+ny*o))
            u=(o+hw)/(2*hw)
            spike=0.72-0.2*(1-abs((i%5)-2.5)/2.5)*(1 if (i//5)%3==1 else 0.6)
            c=(52,36,40) if u<0.2 else (140,120,124) if u<spike else (186,70,76) if u<0.92 else (236,214,212)
            put(x,y,c)
    tx,ty=int(ax-ux*1.5),int(ay-uy*1.5)        # tsuba mặt trời gai
    for y in range(ty-5,ty+6):
        for x in range(tx-5,tx+6):
            d=math.hypot(x-tx,y-ty); ang=math.atan2(y-ty,x-tx)
            if d<=3.2+1.6*max(0,math.cos(ang*8))**4: put(x,y,(238,198,100) if d>1.6 else (110,20,30))
    for i in range(1,11):                     # cán vảy rồng
        cx,cy=tx-ux*(i+2),ty-uy*(i+2)
        for k in (-1,0,1):
            x,y=int(round(cx+nx*k)),int(round(cy+ny*k))
            put(x,y,(220,176,82) if (i+k)%3==0 else (120,18,30))
    ex,ey=int(tx-ux*14),int(ty-uy*14)         # chuôi vàng
    for dx in (-1,0,1):
        for dy in (-1,0,1): put(ex+dx,ey+dy,(232,194,98))
    outline(im); return im
if __name__=='__main__':
    im=draw(); bg=Image.new('RGBA',(36*8,56*8),(40,40,46,255)); bg.alpha_composite(im.resize((288,448),Image.NEAREST)); bg.save('preview/relic_crimson_new.png'); print('ok')
