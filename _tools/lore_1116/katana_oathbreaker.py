# Oathbreaker (16/11, §255): biến tấu từ Ronin's Katana — Arden: thép xám lam, phụ kiện thiếc (không vàng), vỏ gỗ tần bì nhạt,
# dây buộc trắng + DẢI GIẤY NIÊM PHONG đã rách quấn qua miệng vỏ (thanh kiếm "chưa từng rút ra" từ ngày ông bỏ trốn).
import colorsys, sys
from PIL import Image
R='../../Assets/Resources/Weapons/'
ASH=[(70,62,52),(98,88,74),(126,114,96),(152,140,118),(176,164,140),(198,188,164)]
SLATE=[(24,28,38),(36,42,56),(52,60,78)]
PEWTER=[(92,98,110),(128,136,150),(168,176,190),(206,212,222)]
def lum(c): return (0.3*c[0]+0.59*c[1]+0.11*c[2])/255
def pick(ramp,l,lo=0.0,hi=0.6):
    t=min(0.999,max(0,(l-lo)/(hi-lo))); return ramp[int(t*len(ramp))]
def recolor_px(c, region):
    r,g,b,a=c
    if a==0: return c
    h,s,v=colorsys.rgb_to_hsv(r/255,g/255,b/255); h*=360; l=lum(c)
    if 25<=h<=65 and s>0.35 and v>0.35: return pick(PEWTER,l,0.25,0.75)+(a,)          # vàng -> thiếc
    if region=='saya':
        if (h<25 or h>340) and s>0.4 and v<0.5: return (226,224,214,a) if l>0.08 else (190,188,178,a)   # dây đỏ nâu -> dây trắng
        if l<0.35: return pick(ASH,l,0.0,0.28)+(a,)                                   # sơn đen -> gỗ tần bì
    if region=='hilt':
        if l>0.6: return (178,192,212,a)                                              # hoa văn quấn
        if l<0.3: return pick(SLATE,l,0.0,0.2)+(a,)
    if region=='blade' and s<0.25 and l>0.3:                                          # thép hơi lạnh
        return (max(0,r-6),max(0,g-2),min(255,b+8),a)
    return c
def showcase():
    im=Image.open(R+'Showcase/Katana.png').convert('RGBA'); px=im.load(); W,H=im.size
    for y in range(H):
        for x in range(W):
            if not px[x,y][3]: continue
            region='saya' if y<=39 else ('hilt' if x>=205 else 'blade')
            px[x,y]=recolor_px(px[x,y],region)
    # dải giấy niêm phong rách, quấn qua miệng vỏ (gần chuôi lúc tra vào vỏ: phía phải vỏ)
    sx=200
    ys=[y for y in range(11,40) if px[sx,y][3]]
    if ys:
        y0,y1=min(ys),max(ys)
        for x in range(sx,sx+7):
            for y in range(y0,y1+1):
                if px[x,y][3]:
                    px[x,y]=(236,232,218,255) if (x+y)%5 else (214,208,190,255)
                    if x==sx+3 and (y-y0)%3==1: px[x,y]=(40,34,40,255)                 # nét chữ mực
        for k,(dx,dy) in enumerate([(2,1),(3,2),(4,3),(4,4),(5,5),(6,6),(6,7)]):      # đuôi giấy rách bay xuống
            x,y=sx+dx,y1+dy
            if y<H: px[x,y]=(236,232,218,255); px[x+1,y]=(200,194,176,255)
    return im
def design():
    d=Image.open(R+'Designs/Katana_design.png').convert('RGBA'); px=d.load()
    for y in range(32):
        for x in range(64):
            c=px[x,y]
            if not c[3]: continue
            if y<=7: px[x,y]=recolor_px(c,'blade')
            elif y<=12: px[x,y]=pick(SLATE+[(70,78,96)],lum(c),0,0.3)+(c[3],)
            elif y<=15: px[x,y]=pick(PEWTER,lum(c),0.2,0.7)+(c[3],)
            elif y<=23: px[x,y]=recolor_px(c,'hilt')
            elif y<=29: px[x,y]=pick(ASH,lum(c),0.0,0.25)+(c[3],)
            elif y==30: px[x,y]=(226,224,214,c[3])
    return d
def pickup(sc):
    """Hình nhặt dưới đất 48x14: kiếm TRONG VỎ (băng giấy niêm phong) — lấy dải vỏ của ảnh trưng bày thu nhỏ."""
    top=sc.crop((14,9,238,42))
    small=top.resize((48,8),Image.BOX); sp=small.load()
    for y in range(8):
        for x in range(48):
            r,g,b,a=sp[x,y]; sp[x,y]=(r,g,b,255 if a>90 else 0)
    out=Image.new('RGBA',(48,14)); out.alpha_composite(small,(0,3))
    p=out.load()
    for y in range(14):                                   # viền tối
        for x in range(48):
            if not p[x,y][3] and any(0<=x+u<48 and 0<=y+v<14 and p[x+u,y+v][3]==255 and p[x+u,y+v][:3]!=(20,16,18) for u,v in ((1,0),(-1,0),(0,1),(0,-1))): p[x,y]=(20,16,18,255)
    return out
if __name__=='__main__':
    sc=showcase(); de=design(); pk=pickup(sc)
    sh=Image.new('RGBA',(520,330),(34,32,38,255))
    sh.alpha_composite(sc.resize((512,192),Image.NEAREST),(4,4))
    sh.alpha_composite(de.resize((256,128),Image.NEAREST),(4,198))
    sh.alpha_composite(pk.resize((192,56),Image.NEAREST),(280,210)); sh.alpha_composite(pk,(300,290))
    sh.save('preview/oathbreaker.png')
    if len(sys.argv)>1 and sys.argv[1]=='write':
        sc.save(R+'Showcase/Katana_Oathbreaker.png'); de.save(R+'Designs/Katana_Oathbreaker_design.png')
        pk.save('../../Assets/GamePackage/ERW - Grass Land 2.0 v2.1/Props/SR-trimmed/relic_oathbreaker.png'); print('written')
    print('ok')
