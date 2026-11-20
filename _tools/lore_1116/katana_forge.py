# Lò rèn katana (16/11, §255): vẽ TỪ ĐẦU ảnh trưng bày 256x96 (vỏ trên, kiếm rút dưới), bảng UV 64x32 (hàng 0-7 lưỡi, 8-15 tsuba,
# 16-23 chuôi, 24-29 vỏ, 30 dây, 31 kim loại; alpha<255 = phát sáng) và hình nhặt 48x14 — mỗi thanh một dáng, vân, tsuba, kiểu quấn, hoa văn riêng.
import math
from PIL import Image
OUT=(14,10,12,255)
def C(*v): return tuple(v)+((255,) if len(v)==3 else ())
def ramp(cs,t):
    t=max(0.0,min(0.999,t)); return cs[int(t*len(cs))]
def mix(a,b,k): return tuple(int(a[i]+(b[i]-a[i])*k) for i in range(3))+(a[3] if len(a)>3 else 255,)
# ---------------------------------------------------------------- vân lưỡi (hamon): ranh giới (0..1 theo bề ngang, 1 = lưỡi cắt)
def hamon(kind,x):
    if kind=='suguha': return 0.66
    if kind=='notare': return 0.62+0.07*math.sin(x/9.0)
    if kind=='gunome':                                           # sóng tròn đều như răng thành
        p=(x%8)/8.0; return 0.70-0.16*math.sqrt(max(0,1-(2*p-1)**2))
    if kind=='choji':                                            # chùm nụ đinh hương (như giọt)
        p=(x%11)/11.0; return 0.72-0.20*max(0,math.sin(p*math.pi))**3-0.06*max(0,math.sin(p*math.pi*2))
    if kind=='midare':                                           # bất thường như ngọn lửa đèn
        return 0.66-0.14*abs(math.sin(x*0.31)*math.cos(x*0.13))-0.06*math.sin(x*0.9)
    if kind=='sanbonsugi':                                       # ba ngọn tuyết tùng nhọn, cụm lặp
        p=x%15; k=p%5; tall=0.22 if p//5==1 else 0.14
        return 0.72-tall*(1-abs(k-2.5)/2.5)
    if kind=='hitatsura':                                        # đốm tôi rải khắp lưỡi
        return 0.68
    return 0.65
# ---------------------------------------------------------------- ảnh trưng bày
def showcase(S):
    W,H=256,96; im=Image.new('RGBA',(W,H)); px=im.load()
    def put(x,y,c):
        if 0<=x<W and 0<=y<H: px[x,y]=c
    # ===== kiếm rút (dưới) =====
    by=66; hab=158; L=S.get('length',146); curve=S['curve']; w0=S['width']
    cols={}
    for x in range(hab-L,hab+1):
        t=(hab-x)/L; cy=by-curve*t*t; hw=w0/2*(1-0.28*t)
        top=cy-hw; bot=cy+hw
        if t>0.88:                                                # mũi kiếm (kissaki)
            k=(t-0.88)/0.12; bot=cy+hw-k*k*hw*1.5; top=cy-hw+k*hw*0.35
        cols[x]=(top,bot)
        for y in range(int(math.floor(top)),int(math.ceil(bot))+1):
            if not (top-0.5<=y<=bot+0.5): continue
            u=(y-top)/max(0.5,bot-top)
            hb=hamon(S['hamon'],x)
            if u<0.14: c=S['steel'][0]                           # sống lưng
            elif u<0.22: c=S['steel'][3]                          # gờ shinogi
            elif u<hb: c=ramp(S['steel'][1:3],(x*0.37+y*1.7)%1.0*0.6+0.2) if S.get('hada') else S['steel'][2]
            else: c=S['hamon_c'] if u<0.93 else S['edge']
            if S['hamon']=='hitatsura' and 0.25<u<0.66 and (x*7+y*11)%19==0: c=S['hamon_c']
            if S.get('engrave') and 0.3<u<0.55 and 0.08<t<0.45 and (x%3==0 or (x+y)%5==0): c=S['engrave']
            if S.get('bohi') and 0.26<u<0.36 and 0.05<t<0.72: c=S['steel'][0]
            put(x,y,C(*c[:3]))
    # chữ khắc gần chuôi (Accord: ba dấu vương quốc)
    for i,(mx,col) in enumerate(S.get('marks',[])):
        tp,bt=cols[mx]; yy=int((tp+bt)/2)-1
        for dx,dy in ((0,0),(1,0),(0,1),(1,1)): put(mx+dx,yy+dy,col)
    # habaki
    for x in range(hab,hab+5):
        for y in range(by-int(w0/2)-1,by+int(w0/2)+2): put(x,y,C(*ramp(S['habaki'],0.3+0.6*(y-(by-w0/2))/w0)[:3]))
    # tsuba
    tx=hab+8
    tsuba(px,tx,by,S)
    # chuôi
    h0,h1=tx+5,236; hh=S.get('hilt_h',5.5)
    for x in range(h0,h1):
        k=(x-h0)/(h1-h0); half=hh*(1-0.12*abs(k-0.55))
        for y in range(int(by-half),int(by+half)+1):
            u=(y-(by-half))/(2*half); put(x,y,C(*wrap(S,x-h0,u)[:3]))
    # menuki giữa chuôi
    mk=S.get('menuki')
    if mk:
        for (dx,dy) in mk['shape']: put(h0+30+dx,by+dy,C(*mk['color'][:3]))
    # kashira
    for x in range(h1,h1+6):
        for y in range(int(by-hh-0.5),int(by+hh+1.5)):
            put(x,y,C(*ramp(S['metal'],0.25+0.6*(y-(by-hh))/(2*hh))[:3]))
    # ===== vỏ (trên) =====
    sy=26; s1=236; s0=s1-(S.get('length',146)+72); sh=S.get('saya_h',5.5)
    for x in range(s0,s1+1):
        t=(s1-x)/(s1-s0); cy=sy-curve*0.75*t*t
        half=sh*(1-0.15*t)
        if x<s0+4: half*=0.8+0.05*(x-s0)
        for y in range(int(cy-half),int(cy+half)+1):
            u=(y-(cy-half))/(2*half); put(x,y,C(*saya_px(S,x,u,t)[:3]))
    saya_extra(px,S,sy,s0,s1,sh,curve)
    outline(im)
    saya_after(px,S,sy,s0,s1,sh,curve)
    return im
def tsuba(px,tx,cy,S):
    st=S['tsuba']; body=S['tsuba_c']; rim=S['tsuba_rim']
    def put(x,y,c):
        if 0<=x<256 and 0<=y<96: px[x,y]=C(*c[:3])
    rx,ry=4.5,13
    for y in range(int(cy-ry-4),int(cy+ry+5)):
        for x in range(tx-8,tx+9):
            dx,dy=(x+.5-tx)/rx,(y+.5-cy)/ry; d=dx*dx+dy*dy
            inside=d<=1
            if st=='crown':                                     # vành có răng thành ở trên + dưới
                inside=abs(x+.5-tx)<=4 and abs(y+.5-cy)<=ry-1
                if abs(y+.5-cy)>ry-1 and abs(y+.5-cy)<=ry+2 and int(x-tx+8)%3==0: inside=True
            if st=='batwing':                                   # cánh dơi: vạt vỏ sò trên dưới
                inside=d<=1 or (abs(y+.5-cy)<=ry+4 and abs(x+.5-tx)<=2.2+1.5*math.cos((y-cy)*0.9))
            if st=='trirings':
                inside=any(((x+.5-tx)/4.2)**2+((y+.5-(cy+o))/5.2)**2<=1 for o in (-8,0,8))
            if st in ('ripple','plain','sunburst'): inside=d<=1
            if st=='mokko':                                     # bốn thuỳ
                ang=math.atan2((y+.5-cy)/ry,(x+.5-tx)/rx); inside=d<=(1-0.16*math.cos(ang*4))**2
            if st=='halo':                                      # vòng hào quang hở
                inside=0.45<=d<=1.05 and not (abs(x+.5-tx)<1.2 and y<cy)
            if st=='sunburst':                                  # mặt trời gai: tia nhọn toả ra
                ang=math.atan2((y+.5-cy)/ry,(x+.5-tx)/rx); spike=1+0.32*max(0,math.cos(ang*8))**4
                inside=d<=spike*spike
            if st=='lantern':                                   # đèn lồng chữ nhật, khung + ô cửa
                inside=abs(x+.5-tx)<=4 and abs(y+.5-cy)<=ry-2 or (abs(x+.5-tx)<=2 and abs(y+.5-cy)<=ry+1)
            if not inside: continue
            c=ramp(body,0.25+0.6*((y-(cy-ry))/(2*ry)))
            if st=='ripple' and any(abs(math.sqrt(d)-r)<0.09 for r in (0.45,0.75)): c=rim[1]
            if st=='crown' and abs(y+.5-cy)>ry-1: c=rim[1]
            if st=='batwing':
                if abs((x+.5-tx+1.2)**2/9+(y+.5-cy)**2/30-1)<0.35 and x<=tx: c=rim[1]    # trăng lưỡi liềm bạc
            if st=='trirings':
                for o in (-8,0,8):
                    e=((x+.5-tx)/4.2)**2+((y+.5-(cy+o))/5.2)**2
                    if 0.55<e<=1: c=rim[1]
            if st=='plain':
                if d>0.78: c=rim[0]
                if ((x+.5-tx)**2+(y+.5-(cy-6))**2)<2.2 or ((x+.5-tx)**2+(y+.5-(cy+6))**2)<2.2: c=(12,10,10)   # 2 lỗ hitsu
            if st=='sunburst' and d<=0.35: c=rim[1]
            if st=='lantern':
                gx,gy=abs(x+.5-tx),abs(y+.5-cy)
                if gx>3 or gy>ry-3 or int(y-cy)%5==0: c=rim[0]
                elif gx<=3: c=S.get('lantern_glow',rim[1])            # ô cửa sáng
            if st=='mokko' and d>0.7: c=rim[1]
            if st=='halo': c=rim[1] if d>0.8 else c
            if d>0.82 and st in ('ripple','batwing'): c=rim[0]
            put(x,y,c)
def wrap(S,x,u):
    st=S['wrap']; a,b=S['wrap_c'],S['wrap_c2']
    edge=u<0.12 or u>0.88
    if st=='hishi':                                             # mắt quả trám để lộ da cá đuối
        p=x%9; dd=abs(p-4.5)/4.5+abs(u-0.5)/0.32
        return S['same'] if dd<0.85 else (a if not edge else mix(a,(0,0,0),0.4))
    if st=='wire':                                              # da thuộc + dây đồng quấn xoắn
        k=(x+u*6)%6
        return b if k<1.4 else (mix(a,(0,0,0),0.35) if edge else a)
    if st=='braid3':                                            # bện ba màu chữ V
        cs=S['braid']; k=int((x+abs(u-0.5)*10)//4)%3
        c=cs[k]; return mix(c,(0,0,0),0.4) if edge else c
    if st=='silkcross':                                         # tơ đen chéo + mắt đỏ
        p=x%8; dd=abs(p-4)/4+abs(u-0.5)/0.3
        return b if dd<0.75 else (mix(a,(0,0,0),0.3) if edge else a)
    if st=='cord':                                              # dây thừng xoắn đã sờn (đơn sơ)
        k=(x*2+int(u*7)*3)%7
        c=b if k<2 else a
        if (x*13+int(u*9)*7)%29==0: c=mix(a,(0,0,0),0.5)        # chỗ sờn
        return mix(c,(0,0,0),0.35) if edge else c
    if st=='ribbon':                                            # dải lụa bản rộng, khe hở lộ màu đệm
        k=x%10
        return b if k in (0,1) else (mix(a,(0,0,0),0.25) if edge else a)
    if st=='scale':                                             # vảy rồng xếp lớp
        row=int(u*3); ox=4 if row%2 else 0; p=(x+ox)%8
        arc=abs((p-4)/4.0)**2*0.33+row/3
        return b if abs(u-arc)<0.06 else (mix(a,(0,0,0),0.3) if edge else a)
    if st=='linen':                                             # vải lanh quấn chéo (của giáo phái)
        k=(x+int(u*8)*2)%8
        c=b if k==0 else (mix(a,(0,0,0),0.12) if k<4 else a)
        return mix(c,(0,0,0),0.3) if edge else c
    if st=='gilt':                                              # dây vàng quấn khít ngang
        return b if x%3==0 else (mix(a,(0,0,0),0.3) if edge else a)
    return a
def saya_px(S,x,u,t):
    st=S['saya']; base=S['saya_c']
    v=0.75-0.55*u                                               # sáng trên, tối dưới
    c=ramp(base,v)
    if st=='ash':                                               # gỗ tần bì mờ: vân gỗ NGANG mảnh, lượn nhẹ
        g=math.sin(u*19.0+math.sin(x*0.07)*2.2+x*0.013)
        if g>0.93: c=base[0]
        elif g>0.75: c=mix(c,base[0],0.45)
    if st=='iron':                                              # gỗ đen bọc sắt: vân búa
        if (x*7+int(u*20)*3)%23==0: c=base[0]
    if st=='lacquer':                                           # sơn mài bóng + ngả đỏ rượu về đuôi
        c=mix(c,S['saya_tail'],max(0,t-0.35)/0.65*0.8)
        if 0.18<u<0.27: c=mix(c,(255,255,255),0.35)
    if st=='ivory':
        if 0.18<u<0.26: c=mix(c,(255,255,255),0.4)
    if st=='chipped':                                           # sơn đen mờ, trầy xước, sứt
        if (x*11+int(u*13)*5)%37==0 or ((x//9)%7==3 and abs(u-0.5)<0.08): c=base[-1]
    if st=='starlacquer':                                       # sơn tím thẫm lấm tấm sao bạc
        if (x*31+int(u*11)*17)%53==0: c=S['star']
        if 0.18<u<0.26: c=mix(c,(255,255,255),0.25)
    if st=='bone':                                              # trắng ngà, khắc hàng tên li ti
        if 0.3<u<0.7 and int(u*10)%2==0 and (x*5)%7<3: c=base[0]
    if st=='gilded':                                            # sơn vàng nhạt, rỉ đỏ sẫm ở miệng vỏ
        if 0.16<u<0.24: c=mix(c,(255,255,255),0.35)
        if t<0.12 and (x*7+int(u*9))%5<2: c=S['drip']
    if st=='royal':                                             # sơn đỏ bóng + chỉ vàng gấp khúc trên dưới
        if 0.16<u<0.24: c=mix(c,(255,255,255),0.3)
        zz=abs((x%8)-4)/4
        if abs(u-(0.08+zz*0.08))<0.05 or abs(u-(0.92-zz*0.08))<0.05: c=S['gold']
    return c
def saya_extra(px,S,sy,s0,s1,sh,curve):
    def put(x,y,c):
        if 0<=x<256 and 0<=y<96: px[x,y]=C(*c[:3])
    def cy(x): t=(s1-x)/(s1-s0); return sy-curve*0.75*t*t
    met=S['metal']
    for x in range(s1-5,s1+1):                                   # miệng vỏ (koiguchi)
        for y in range(int(cy(x)-sh-0.5),int(cy(x)+sh+1)): put(x,y,ramp(met,0.2+0.6*(y-(cy(x)-sh))/(2*sh)))
    kl=S.get('kojiri_len',6)
    for x in range(s0,s0+kl):                                     # đuôi vỏ (kojiri)
        for y in range(int(cy(x)-sh),int(cy(x)+sh)+1): put(x,y,ramp(met,0.2+0.6*(y-(cy(x)-sh))/(2*sh)))
    for (bx,bw) in S.get('bands',[]):                             # đai kim loại
        for x in range(bx,bx+bw):
            for y in range(int(cy(x)-sh-1),int(cy(x)+sh+2)): put(x,y,ramp(met,0.2+0.6*(y-(cy(x)-sh))/(2*sh)))
        if S.get('rivets'):
            for y in (int(cy(bx)-sh+2),int(cy(bx)+sh-2)): put(bx+bw//2,y,met[-1])
    for rx in S.get('rings',[]):                                  # nhẫn bạc (vòng mảnh)
        for y in range(int(cy(rx)-sh-1),int(cy(rx)+sh+2)): put(rx,y,S['ring_c'][1]); put(rx+1,y,S['ring_c'][0])
        put(rx,int(cy(rx)-sh-1),S['ring_c'][2])
    for (mx,shape,col) in S.get('crests',[]):                     # huy hiệu nhỏ trên vỏ
        for dx,dy in shape: put(mx+dx,int(cy(mx))+dy,col)
    kx=s1-30                                                      # núm buộc dây (kurikata)
    for x in range(kx,kx+4): put(x,int(cy(x)+sh+1),met[1]); put(x,int(cy(x)+sh+2),met[0])
def saya_after(px,S,sy,s0,s1,sh,curve):
    """Vẽ SAU viền: dây buộc, dải giấy niêm phong (đè ra ngoài viền vỏ)."""
    def put(x,y,c):
        if 0<=x<256 and 0<=y<96: px[x,y]=C(*c[:3])
    def cy(x): t=(s1-x)/(s1-s0); return sy-curve*0.75*t*t
    cord=S['cord']; kx=s1-30
    for i in range(9):                                            # dây sageo buông thõng
        put(kx+1+i//3,int(cy(kx)+sh+3+i),cord[1] if i%2 else cord[0])
    for i in range(7): put(kx+5+i,int(cy(kx)+sh+3+i//2),cord[1])
    for x in range(kx-3,kx+8): put(x,int(cy(x)-sh-1),cord[1]); put(x,int(cy(x)+sh+1),cord[0])
    if S.get('charm'):                                          # bùa đèn canh nhỏ treo ở dây
        cx_,cy_=kx+3,int(cy(kx)+sh+12)
        for dx,dy in ((0,-1),(-1,0),(0,0),(1,0),(-1,1),(0,1),(1,1),(0,2)): put(cx_+dx,cy_+dy,S['charm'] if (dx,dy)!=(0,0) else (255,240,255))
        put(cx_,cy_-2,(90,80,100))
    if S.get('seal'):                                             # giấy niêm phong rách quấn qua miệng vỏ
        x0=s1-13
        for x in range(x0,x0+8):
            for y in range(int(cy(x)-sh-3),int(cy(x)+sh+4)):
                c=(238,234,220) if (x*3+y)%7 else (220,214,196)
                if x in (x0,x0+7) or y in (int(cy(x)-sh-3),): c=(150,144,128)
                put(x,y,c)
            if x in (x0+2,x0+4,x0+5):                             # nét mực
                for y in range(int(cy(x)-sh-1),int(cy(x)+sh+2),2): put(x,y,(36,30,40))
        for i,(dx,dy) in enumerate([(1,0),(1,1),(2,2),(2,3),(1,4),(2,5),(3,6),(3,7)]):   # hai đuôi giấy rách
            put(x0+dx,int(cy(x0)+sh+4+dy),(238,234,220)); put(x0+dx+1,int(cy(x0)+sh+4+dy),(200,194,176))
            put(x0+5+dx//2,int(cy(x0)+sh+4+dy//2),(238,234,220))
def outline(im):
    W,H=im.size; px=im.load(); a=[[px[x,y][3] for x in range(W)] for y in range(H)]
    for y in range(H):
        for x in range(W):
            if not a[y][x] and any(0<=x+u<W and 0<=y+v<H and a[y+v][x+u] for u,v in((1,0),(-1,0),(0,1),(0,-1))): px[x,y]=OUT
# ---------------------------------------------------------------- bảng UV 64x32
def design(S):
    im=Image.new('RGBA',(64,32)); px=im.load()
    for x in range(64):
        for r in range(8):                                         # lưỡi: x = gốc -> mũi, hàng = sống -> lưỡi cắt
            u=r/7; hb=hamon(S['hamon'],x*2.3)
            if x<2: c=S['habaki'][2]
            elif u<0.15: c=S['steel'][0]
            elif u<0.3: c=S['steel'][3]
            elif u<hb: c=S['steel'][2]
            else: c=S['hamon_c'] if r<7 else S['edge']
            a=255
            if S.get('glow_hamon') and u>=hb: a=200
            if S.get('glow_edge') and r==7: a=210
            px[x,r]=C(*c[:3])[:3]+(a,)
        for r in range(8,16):                                       # tsuba: thân + vành / hoạ tiết
            c=ramp(S['tsuba_c'],(r-8)/8)
            if r>=13 or (S['tsuba'] in ('ripple','trirings') and x%6==0): c=S['tsuba_rim'][1]
            if S['tsuba']=='lantern' and r in (10,11) and x%4 in (1,2): c=S.get('lantern_glow',c)
            px[x,r]=C(*c[:3])
        for r in range(16,24): px[x,r]=C(*wrap(S,x*1.6,(r-16)/7)[:3])   # chuôi
        for r in range(24,30):                                      # vỏ
            u=(r-24)/5; c=saya_px(S,x*3.4,u,x/63)
            st=S['saya']
            if st=='iron' and x%16 in (0,1): c=S['metal'][2]                       # đai đồng
            if st=='lacquer' and x%10==0: c=S['ring_c'][1]                         # nhẫn bạc
            if st=='ivory' and x%21 in (10,11) and r in (25,26,27): c=S['metal'][2]  # vương miện vàng
            if st=='ash' and x>=56: c=(236,232,218)                                 # giấy niêm phong
            if st=='royal' and r in (24,29) and x%4<2: c=S['gold']
            px[x,r]=C(*c[:3])
        px[x,30]=C(*S['cord'][x%2][:3]) if S['wrap']!='braid3' else C(*S['braid'][(x//3)%3][:3])
        px[x,31]=C(*ramp(S['metal'],x/64)[:3])
    return im
# ---------------------------------------------------------------- hình nhặt 48x14 (kiếm trong vỏ), lấy màu trội từng ô
def pickup(sc):
    src=sc.crop((14,8,242,46)); W,H=48,14
    out=Image.new('RGBA',(W,H)); op=out.load(); sp=src.load(); sw,sh=src.size
    from collections import Counter
    for y in range(H):
        for x in range(W):
            x0,x1=int(x*sw/W),int((x+1)*sw/W); y0,y1=int(y*sh/H),int((y+1)*sh/H)
            cnt=Counter(sp[i,j] for i in range(x0,max(x1,x0+1)) for j in range(y0,max(y1,y0+1)) if sp[i,j][3] and sp[i,j]!=OUT)
            tot=(max(x1,x0+1)-x0)*(max(y1,y0+1)-y0)
            if cnt and sum(cnt.values())>tot*0.35: op[x,y]=cnt.most_common(1)[0][0]
    outline(out); return out
