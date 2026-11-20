# Hộp thánh tích hoàng gia (đợt H, 16/11): hộp cũ rỗng (Chapel of Naming) + hộp nhỏ mới chờ Con Mắt (House Vey). Pixel art, sáng từ trên-trái.
import math
from PIL import Image
OUT=(18,10,8,255)
WOOD=[(40,22,16,255),(66,38,26,255),(92,56,38,255),(118,76,52,255)]
LAC=[(14,12,16,255),(26,22,30,255),(42,36,48,255),(62,54,70,255)]
GOLD=[(96,70,24,255),(150,112,40,255),(206,164,70,255),(244,214,128,255)]
TARN=[(70,60,34,255),(104,90,52,255),(138,122,74,255)]
VEL=[(44,8,16,255),(74,14,26,255),(104,24,38,255),(140,40,54,255)]
VELN=[(70,6,20,255),(116,14,34,255),(160,30,50,255),(196,60,74,255)]
def rect(px,x0,y0,x1,y1,c):
    for y in range(y0,y1+1):
        for x in range(x0,x1+1): px[x,y]=c
def outline(im):
    W,H=im.size; px=im.load(); a=[[px[x,y][3] for x in range(W)] for y in range(H)]
    for y in range(H):
        for x in range(W):
            if not a[y][x] and any(0<=x+u<W and 0<=y+v<H and a[y+v][x+u] for u,v in((1,0),(-1,0),(0,1),(0,-1))): px[x,y]=OUT
def hollow(px,cells,vel):
    for (x,y) in cells: px[x,y]=vel[0]
    for (x,y) in cells:
        if (x,y-1) not in cells: px[x,y]=OUT[:3]+(255,) if False else vel[0]
    # mép dưới hốc sáng nhẹ (nhung bị ép)
    for (x,y) in cells:
        if (x,y+1) not in cells and px[x,y+1][3]: px[x,y+1]=vel[3]
def ell_cells(cx,cy,rx,ry):
    return {(x,y) for y in range(int(cy-ry-1),int(cy+ry+2)) for x in range(int(cx-rx-1),int(cx+rx+2)) if ((x+.5-cx)/rx)**2+((y+.5-cy)/ry)**2<=1}
def old_case():
    """Hộp cũ 44x30: nắp gỗ dựng phía sau (ấn vương miện vàng xỉn), lòng nhung đỏ cũ có 4 hốc rỗng: rãnh dài có đốt (cột sống), 2 hốc cong (tai), 1 hốc tròn (mắt). Bụi, góc đồng xỉn."""
    W,H=44,30; im=Image.new('RGBA',(W,H)); px=im.load()
    # nắp dựng phía sau (nghiêng)
    rect(px,4,1,39,9,WOOD[2]); rect(px,4,1,39,1,WOOD[3]); rect(px,5,8,38,9,WOOD[1])
    for x in (4,39):
        for y in range(1,10): px[x,y]=TARN[1]
    # ấn vương miện trên nắp
    for (x,y) in [(19,5),(21,4),(23,5),(25,4),(27,5),(19,6),(20,6),(21,6),(22,6),(23,6),(24,6),(25,6),(26,6),(27,6),(20,7),(21,7),(22,7),(23,7),(24,7),(25,7),(26,7)]: px[x-1,y]=TARN[2] if y<6 else TARN[1]
    # thân hộp (nhìn chéo từ trên): mặt trên = lòng nhung, mặt trước = gỗ
    rect(px,2,10,41,21,WOOD[1])                    # viền miệng hộp
    rect(px,4,11,39,20,VEL[1])                     # nhung
    for y in range(11,21):                         # vân nhung + bụi
        for x in range(4,40):
            if (x*7+y*13)%11==0: px[x,y]=VEL[2]
            if (x*5+y*3)%17==0: px[x,y]=(122,92,80,255)
    rect(px,2,21,41,28,WOOD[2]); rect(px,2,21,41,21,WOOD[3]); rect(px,2,27,41,28,WOOD[0])
    for x in (2,41):
        for y in range(10,29): px[x,y]=TARN[0]
    for (x,y) in ((2,21),(41,21),(2,28),(41,28),(2,10),(41,10)): px[x,y]=TARN[2]
    for x in range(16,28): px[x,24]=TARN[1]        # tấm đồng khắc chữ trên mặt trước
    for x in range(16,28): px[x,25]=TARN[0]
    # hốc rỗng
    spine=set()
    for k in range(10):
        for (x,y) in ell_cells(8+k*2.6,15.5,1.5,1.9): spine.add((x,y))
    hollow(px,spine,VEL)
    for cx in (34.3,37.6):
        e=ell_cells(cx,14.3,1.4,2.5)-ell_cells(cx+0.9,14.6,0.7,1.3)
        hollow(px,e,VEL)
    hollow(px,ell_cells(35.8,18.6,1.6,1.4),VEL)
    outline(im); return im
def eye_case():
    """Hộp nhỏ mới 24x20: sơn mài đen, viền vàng sáng, ấn hoàng gia; nắp mở; MỘT hốc tròn lót nhung đỏ mới — chờ Con Mắt."""
    W,H=24,20; im=Image.new('RGBA',(W,H)); px=im.load()
    rect(px,3,1,20,6,LAC[2]); rect(px,3,1,20,1,LAC[3]); rect(px,4,5,19,6,LAC[1])
    for x in (3,20):
        for y in range(1,7): px[x,y]=GOLD[2]
    for (x,y) in [(10,3),(12,2),(14,3),(10,4),(11,4),(12,4),(13,4),(14,4)]: px[x-0,y]=GOLD[3] if y<4 else GOLD[2]
    rect(px,2,7,21,13,LAC[1]); rect(px,4,8,19,12,VELN[1])
    for y in range(8,13):
        for x in range(4,20):
            if (x*7+y*13)%9==0: px[x,y]=VELN[2]
    rect(px,2,13,21,18,LAC[2]); rect(px,2,13,21,13,LAC[3]); rect(px,2,18,21,18,LAC[0])
    for x in (2,21):
        for y in range(7,19): px[x,y]=GOLD[1]
    for (x,y) in ((2,7),(21,7),(2,18),(21,18),(2,13),(21,13)): px[x,y]=GOLD[3]
    for x in range(9,15): px[x,15]=GOLD[2]
    for x in range(10,14): px[x,16]=GOLD[1]
    c=ell_cells(11.8,10.3,2.2,1.7)
    for (x,y) in c: px[x,y]=VELN[0]
    for (x,y) in c:
        if (x,y+1) not in c: px[x,y+1]=VELN[3]
    px[10,9]=VELN[2]
    outline(im); return im
if __name__=='__main__':
    import sys
    a=old_case(); b=eye_case()
    S=8; sh=Image.new('RGBA',(a.width*S+b.width*S+60,a.height*S+60),(40,36,44,255))
    sh.alpha_composite(a.resize((a.width*S,a.height*S),Image.NEAREST),(10,10))
    sh.alpha_composite(b.resize((b.width*S,b.height*S),Image.NEAREST),(a.width*S+40,10))
    sh.alpha_composite(a,(10,a.height*S+20)); sh.alpha_composite(b,(a.width*S+40,a.height*S+20))
    sh.save('preview/reliquary_cases.png')
    if len(sys.argv)>1:
        a.save(sys.argv[1]+'/reliquary_royal_old.png'); b.save(sys.argv[1]+'/reliquary_royal_eye.png'); print('saved')
    print('ok')
