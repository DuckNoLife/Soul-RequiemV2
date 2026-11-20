# Bản thử búi rễ đỏ (sẽ chép sang C# BossRelic.KnotSprite)
# Mỗi sợi vẽ viền tối trước rồi lõi sáng -> sợi sau đè sợi trước, thấy rõ đan chéo.
import math
from PIL import Image
W=H=28
OUT,DARK,MID,LIT,VEIN=(30,4,7,255),(110,18,30,255),(192,48,60,255),(232,96,104,255),(255,170,160,255)
def knot():
    im=Image.new('RGBA',(W,H)); px=im.load()
    cx=cy=(W-1)/2
    def dot(x,y,r,c):
        for yy in range(int(y-r-1),int(y+r+2)):
            for xx in range(int(x-r-1),int(x+r+2)):
                if 0<=xx<W and 0<=yy<H and (xx-x+.5)**2+(yy-y+.5)**2<=r*r: px[xx,yy]=c
    def strand(pts,w0,w1,c):
        n=len(pts)
        for k,(x,y) in enumerate(pts): dot(x,y,(w0+(w1-w0)*k/n)+0.9,OUT)
        for k,(x,y) in enumerate(pts): dot(x,y,(w0+(w1-w0)*k/n),c)
    dot(cx,cy,3.6,OUT); dot(cx,cy,2.6,DARK)
    # rễ thò ra (vẽ trước, nằm dưới)
    for i,a0 in enumerate((0.4,1.6,2.7,3.8,5.1)):
        pts=[]
        for s in range(50):
            t=s/49; r=4+t*8.5; a=a0+0.6*math.sin(t*4.2+i*1.3)
            pts.append((cx+math.cos(a)*r, cy+math.sin(a)*r*0.92))
        strand(pts,0.9,0.15,MID)
    # 3 sợi quấn quanh lõi
    for i,c in enumerate((MID,LIT,MID)):
        ph=i*2.09; pts=[]
        for s in range(120):
            th=s/119*2*math.pi*1.1
            r=4.0+1.8*math.sin(2*th+ph)
            pts.append((cx+math.cos(th+ph)*r, cy+math.sin(th+ph)*r*0.9))
        strand(pts,0.75,0.6,c)
    for dx,dy in ((-3,-2),(3,-3),(2,3),(-2,3)):
        x,y=int(cx+dx),int(cy+dy)
        if px[x,y][3] and px[x,y]!=OUT: px[x,y]=VEIN
    return im
im=knot(); bg=Image.new('RGBA',(W*8,H*8),(40,40,44,255)); bg.alpha_composite(im.resize((W*8,H*8),Image.NEAREST)); bg.save('preview/knot_proto.png')
