# Hình katana NẰM ĐẤT (nhặt được) 64x26: vỏ (saya) nằm dưới + : kiếm ĐÃ RÚT đầy đủ — mũi, lưỡi + vân, habaki, tsuba, chuôi quấn, kashira — vẽ thẳng từ thông số katana_specs.
# (16/11 §262: bản cũ 48x14 thu nhỏ từ phần VỎ ở ảnh trưng bày nên trông như cái bao kiếm.)
# python3 katana_ground.py <key> <out.png>
import sys
from PIL import Image
from katana_specs import SPECS
from katana_forge import outline, C, ramp
def ground(S):
    W,H=64,26; im=Image.new('RGBA',(W,H),(0,0,0,0)); px=im.load()
    # 16/11 (§263): VỎ KIẾM nằm song song phía dưới (người dùng: thiếu bao kiếm)
    sc=S.get('saya_c',[(60,50,40),(90,74,58),(120,100,80),(150,128,104)]); mt=S['metal']
    def sspine(x): t=(44-x)/42; return 18-int(round(max(1,min(3,S.get('curve',6)//3))*t*t))
    for x in range(2,46):
        y0=sspine(x)
        for k in range(4):
            c=sc[3] if k==0 else sc[2] if k==1 else sc[1] if k==2 else sc[0]
            if x in (2,3) or x in (44,45): c=mt[2] if k<2 else mt[1]          # kojiri / koiguchi
            elif S.get('seal') and 34<=x<=36: c=(236,232,218) if k<3 else (200,196,184)   # giấy niêm
            elif x%9==0 and k==1: c=sc[3]
            px[x,y0+k]=C(*c[:3])
    cord=S.get('cord',[(200,200,196),(236,236,232)])
    for i,(x,y) in enumerate(((40,sspine(40)+4),(41,sspine(40)+5),(42,sspine(40)+5),(43,sspine(40)+4))):
        px[x,y]=C(*cord[i%2][:3])
    steel=S['steel']; edge=S.get('edge',steel[-1]); hc=S.get('hamon_c',steel[-1])
    base=8; curve=max(1,min(3,S.get('curve',6)//3))
    TIP,BL=1,41                      # mũi -> hết lưỡi
    def spine(x):                    # cong nhẹ: mũi vểnh lên
        t=(BL-x)/(BL-TIP); return base-int(round(curve*t*t))
    for x in range(TIP,BL+1):
        y0=spine(x); w=3 if x>TIP+3 else (2 if x>TIP+1 else 1)
        for k in range(w):
            y=y0+k
            if k==0: c=steel[0]                                  # sống lưng
            elif k==w-1: c=edge                                  # lưỡi cắt
            else: c=steel[2]
            px[x,y]=C(*c[:3])
        if w==3:
            px[x,y0+1]=C(*(hc if (x//3)%2 or S.get('hamon')=='suguha' else steel[1])[:3])   # vân lưỡi
            if S.get('bohi') and TIP+8<x<BL-4: px[x,y0+1]=C(*steel[1][:3])                 # rãnh bohi chạy dọc
            px[x,y0+1]=px[x,y0+1] if x%7 else C(*steel[3][:3])                              # ánh thép lấp lánh
    # habaki
    hb=S.get('habaki',S['metal'])
    for x in (BL+1,BL+2):
        for y in range(spine(BL)-1,spine(BL)+4): px[x,y]=C(*hb[1 if y<spine(BL)+2 else 0][:3])
    # tsuba (đứng, elip dẹt)
    tc=S['tsuba_c']; rim=S.get('tsuba_rim',[tc[0],tc[-1]])
    cy=spine(BL)+1
    for x in (BL+3,BL+4):
        for y in range(cy-4,cy+5):
            edgep=y in (cy-4,cy+4)
            px[x,y]=C(*(rim[1] if (edgep and x==BL+3) else rim[0] if edgep else tc[1 if x==BL+3 else 0])[:3])
    # chuôi quấn
    h0,h1=BL+5,BL+18
    wc=S['wrap_c']; same=S.get('same',(220,220,220))
    for x in range(h0,h1+1):
        for k,y in enumerate(range(cy-1,cy+2)):
            c=wc
            if S['wrap'] in ('hishi','diamond') and (x-h0)%3==1 and k==1: c=same
            elif S['wrap'] not in ('hishi','diamond') and (x-h0)%2==0 and k!=1: c=same
            px[x,y]=C(*c[:3])
        if x==h0+5 and 'menuki' in S: px[x,cy]=C(*S['menuki']['color'][:3])
    # kashira
    for x in (h1+1,h1+2):
        for y in range(cy-1,cy+2): px[x,y]=C(*S['metal'][2 if y==cy-1 else 1][:3])
    outline(im)
    # bóng đổ mờ dưới thân kiếm
    sh=Image.new('RGBA',(W,H),(0,0,0,0)); sp=sh.load()
    for x in range(W):
        ys=[y for y in range(H) if px[x,y][3]==255]
        if ys and max(ys)+1<H: sp[x,max(ys)+1]=(0,0,0,70)
    sh.alpha_composite(im); return sh
if __name__=='__main__' and len(sys.argv)<=3:
    ground(SPECS[sys.argv[1]]).save(sys.argv[2]); print('ok',sys.argv[2])

# 16/11 (§268): KIẾM NẰM TRONG VỎ (người dùng: thanh kiếm trong bao, nằm trước mặt xác). 64x14:
# vỏ (kojiri trái -> koiguchi) + tsuba + chuôi quấn + kashira, cong nhẹ, bóng đổ. Dùng: python3 katana_ground.py <key> <out> sheathed
def sheathed(S):
    W,H=64,14; im=Image.new('RGBA',(W,H),(0,0,0,0)); px=im.load()
    sc=S.get('saya_c',[(60,50,40),(90,74,58),(120,100,80),(150,128,104)]); mt=S['metal']
    cv=max(1,min(3,S.get('curve',6)//3))
    def sp(x): t=(44-x)/42; return 5-int(round(cv*t*t))+cv     # vỏ cong: đầu kojiri vểnh lên
    for x in range(2,46):
        y0=sp(x)
        for k in range(4):
            c=sc[3] if k==0 else sc[2] if k==1 else sc[1] if k==2 else sc[0]
            if x in (2,3): c=mt[2] if k<2 else mt[1]                                   # kojiri
            elif x in (44,45): c=mt[3 if k==0 else 2 if k<3 else 1]                    # koiguchi
            elif S.get('seal') and 33<=x<=35: c=(236,232,218) if k<3 else (200,196,184)   # giấy niêm
            elif x%9==0 and k==1: c=sc[3]
            px[x,y0+k]=C(*c[:3])
    cord=S.get('cord',[(200,200,196),(236,236,232)])
    for i,(x,dy) in enumerate(((38,4),(39,5),(40,5),(41,4))):                          # dây sageo thòng xuống
        px[x,sp(x)+dy]=C(*cord[i%2][:3])
    cy=sp(45)+1
    tc=S['tsuba_c']; rim=S.get('tsuba_rim',[tc[0],tc[-1]])
    for x in (46,47):
        for y in range(cy-3,cy+5):
            e=y in (cy-3,cy+4)
            px[x,y]=C(*(rim[1] if (e and x==46) else rim[0] if e else tc[1 if x==46 else 0])[:3])
    wc=S['wrap_c']; same=S.get('same',(220,220,220))
    for x in range(48,60):
        for k,y in enumerate(range(cy,cy+3)):
            c=wc
            if S['wrap'] in ('hishi','diamond') and (x-48)%3==1 and k==1: c=same
            elif S['wrap'] not in ('hishi','diamond') and (x-48)%2==0 and k!=1: c=same
            px[x,y]=C(*c[:3])
        if x==53 and 'menuki' in S: px[x,cy+1]=C(*S['menuki']['color'][:3])
    for x in (60,61):
        for y in range(cy,cy+3): px[x,y]=C(*mt[2 if y==cy else 1][:3])
    outline(im)
    sh=Image.new('RGBA',(W,H),(0,0,0,0)); shp=sh.load()
    for x in range(W):
        ys=[y for y in range(H) if px[x,y][3]==255]
        if ys and max(ys)+1<H: shp[x,max(ys)+1]=(0,0,0,70)
    sh.alpha_composite(im); return sh
if __name__=='__main__' and len(sys.argv)>3 and sys.argv[3]=='sheathed':
    sheathed(SPECS[sys.argv[1]]).save(sys.argv[2]); print('sheathed ok',sys.argv[2])
