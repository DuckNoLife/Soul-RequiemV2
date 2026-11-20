# Icon 64x64 cho bộ Mantras of the Ear (16/11, §252): khung lấy từ ic_vessel_hand, nền tối, biểu tượng màu hymn xanh băng của Morrow.
import math, sys
from PIL import Image
sys.argv=[sys.argv[0]]
from relic_dry import ears
R='../../Assets/Resources/UI/Requiem/'
ICE=[(26,40,74,255),(62,104,170,255),(122,172,232,255),(196,224,255,255),(250,252,255,255)]
def base():
    im=Image.open(R+'ic_vessel_hand.png').convert('RGBA'); px=im.load()
    for y in range(6,58):
        for x in range(6,58):
            d=math.hypot(x-31.5,y-31.5)/32
            k=max(0,1-d)
            px[x,y]=(int(12+26*k),int(16+40*k),int(26+64*k),255)
    return im
def put(px,x,y,c):
    if 6<=x<58 and 6<=y<58: px[x,y]=c
def arc(px,cx,cy,r,a0,a1,w,ramp,alpha=255):
    for y in range(6,58):
        for x in range(6,58):
            dx,dy=x+.5-cx,y+.5-cy; d=math.hypot(dx,dy); a=math.degrees(math.atan2(dy,dx))%360
            ok=(a0<=a<=a1) if a0<a1 else (a>=a0 or a<=a1)
            if ok and r-w/2<=d<=r+w/2:
                t=(d-(r-w/2))/max(0.01,w); c=ramp[min(len(ramp)-1,int(t*len(ramp)))]
                if alpha<255:
                    o=px[x,y]; c=tuple(int(o[i]+(c[i]-o[i])*alpha/255) for i in range(3))+(255,)
                px[x,y]=c
def afterecho():
    im=base(); px=im.load()
    arc(px,26,38,20,250,40,6,ICE[1:],alpha=110)          # bóng vọng (mờ, lệch)
    arc(px,34,30,20,250,40,5,ICE[1:])                    # nhát chính
    for k,r in enumerate((6,10,14)): arc(px,16,46,r,190,260,1.4,[ICE[2]],alpha=230-50*k)   # sóng âm
    return im
def discord():
    im=base(); px=im.load()
    e=ears(); e=e.crop((2,0,18,26))                       # một chiếc tai
    e=e.resize((e.width*2,e.height*2),Image.NEAREST); ep=e.load()
    for y in range(e.height):                             # nhuộm băng
        for x in range(e.width):
            r,g,b,a=ep[x,y]
            if a: l=(r+g+b)/765; ep[x,y]=ICE[min(4,int(l*5.5))] if (r,g,b)!=(20,14,10) else (8,12,24,255)
    im.alpha_composite(e,(30,8))
    for s in range(60):                                   # lời thì thầm xoắn chui vào tai
        t=s/59; a=t*9.5; r=13*(1-t)+1.5
        x=int(20+math.cos(a)*r*0.8-t*1); y=int(34+math.sin(a)*r)
        put(px,x,y,ICE[3] if s%5 else ICE[4])
    return im
def forewarning():
    im=base(); px=im.load()
    for y in range(14,44):                                # quả chuông
        w=4+ (y-14)*0.42 if y<38 else 4+24*0.42+ (y-38)*1.2
        for x in range(int(32-w),int(32+w)+1):
            t=(x-(32-w))/(2*w+0.01); c=ICE[1] if t>0.7 else ICE[2] if t>0.35 else ICE[3]
            if y in (14,) or abs(x-32)>=w-0.6: c=ICE[0]
            put(px,x,y,c)
    for y in (44,45):
        for x in range(30,35): put(px,x,y,ICE[3])
    for k,r in enumerate((20,24)): arc(px,32,30,r,200,340,1.4,[ICE[3]],alpha=220-70*k)  # rung trước
    for i in range(18):                                   # lưỡi kiếm lướt qua phía sau
        put(px,10+i*2,52-i,ICE[4]); put(px,11+i*2,52-i,ICE[3])
    return im
if __name__=='__main__':
    out={'ic_ear_afterecho':afterecho(),'ic_ear_discord':discord(),'ic_ear_forewarning':forewarning()}
    sheet=Image.new('RGBA',(64*3*4+40,64*4),(30,30,36,255))
    for i,(n,im) in enumerate(out.items()): sheet.alpha_composite(im.resize((256,256),Image.NEAREST),(i*270,0))
    sheet.save('preview/ear_icons.png')
    import os
    if os.environ.get('WRITE'):
        for n,im in out.items(): im.save(R+n+'.png'); print('wrote',n)
    print('ok')
