# Vệt chém riêng cho từng katana (16/11, §258): lấy HÌNH CONG + điểm gốc từ bộ vệt cũ (khớp động tác 8 hướng × 3 nhát),
# vẽ lại chất liệu + hạt riêng theo thanh kiếm. Ra: Resources/VFX/slash3/<style>_<nhát>_<HƯỚNG>.png + .meta (cắt khung như bộ gốc, guid mới).
import sys, os, re, uuid, zlib
import numpy as np
from PIL import Image
D='../../Assets/Resources/VFX/slash3/'
KEYS=['E','NE','N','NW','W','SW','S','SE']
BASE={'oathbreaker':'moonlit','ironcrown':'crimson','eventide':'moonlit','accord':'ronin','namesake':'ronin','absolution':'crimson'}
def sh(m,dx,dy):
    o=np.zeros_like(m)
    H,W=m.shape
    o[max(0,dy):H+min(0,dy),max(0,dx):W+min(0,dx)]=m[max(0,-dy):H+min(0,-dy),max(0,-dx):W+min(0,-dx)]
    return o
def erode(m): return m & sh(m,1,0) & sh(m,-1,0) & sh(m,0,1) & sh(m,0,-1)
def dilate(m): return m | sh(m,1,0) | sh(m,-1,0) | sh(m,0,1) | sh(m,0,-1)
def depth(m,k=6):
    d=np.zeros(m.shape,np.int32); cur=m.copy()
    for i in range(k):
        d+=cur; cur=erode(cur)
    return d
def ring(m,a,b):
    x=m.copy(); out=np.zeros_like(m)
    for i in range(b):
        x=dilate(x)
        if i+1>=a: out|=x
    return out & ~m
def frames_of(meta):
    return len(re.findall(r'^      name: ',open(meta,encoding='utf-8').read(),flags=re.M)) or 1
def restyle(style,arr,name,fw):
    A=arr[...,3].astype(np.float32); M=A>24
    L=(0.3*arr[...,0]+0.59*arr[...,1]+0.11*arr[...,2])/255.0
    H,W=M.shape; rng=np.random.RandomState(zlib.crc32(name.encode()))
    hsh=rng.rand(H,W); xs=np.arange(W)[None,:].repeat(H,0); ys=np.arange(H)[:,None].repeat(W,1)
    safe=((xs%fw)>4)&((xs%fw)<fw-5)&(ys>2)&(ys<H-3)
    out=np.zeros((H,W,4),np.float32)
    def paint(mask,rgb,alpha):
        out[mask,0]=rgb[0]; out[mask,1]=rgb[1]; out[mask,2]=rgb[2]
        out[mask,3]=alpha[mask] if isinstance(alpha,np.ndarray) else alpha
    if style=='oathbreaker':                       # nước lặng: lưỡi mảnh trong + bóng mờ phản chiếu + giọt gợn vòng
        m2=erode(M); m2=m2 if m2.sum()>M.sum()*0.25 else M
        ghost=sh(M,-4,3)&~M; out[ghost]=(150,200,240,85)
        ghost2=sh(M,-8,6)&~M&~ghost; out[ghost2]=(150,200,240,40)
        d=depth(m2)
        paint(m2&(d==1),(150,200,240),A*0.6); paint(m2&(d>=2),(236,246,255),A)
        drops=ring(M,2,6)&(hsh<0.02)&safe
        for y,x in zip(*np.nonzero(drops)):
            for dx,dy in ((-2,0),(2,0),(0,-2),(0,2),(-1,-1),(1,-1),(-1,1),(1,1)):
                if 0<=y+dy<H and 0<=x+dx<W and not M[y+dy,x+dx]: out[y+dy,x+dx]=(186,222,250,170)
    elif style=='ironcrown':                       # lò rèn: rất dày, lõi sắt nung, viền sắt đen, tia lửa dày đặc + vảy sắt
        m2=dilate(dilate(M)); d=depth(m2)
        paint(m2&(d==1),(46,42,46),np.maximum(A,210)); paint(m2&(d==2),(120,52,24),np.maximum(A,225))
        core=m2&(d>=3); t=np.clip(L,0,1)
        out[core,0]=255; out[core,1]=(130+110*t)[core]; out[core,2]=(40+120*t)[core]; out[core,3]=np.maximum(A,235)[core]
        sp=ring(m2,1,9)&(hsh<0.03)&safe
        for y,x in zip(*np.nonzero(sp)):
            for k,c in enumerate(((255,240,160,255),(255,190,80,230),(255,120,40,170))):
                if x+k<W and y-k//2>=0: out[y-k//2,x+k]=c
        fl=ring(m2,2,7)&(hsh>0.992)&safe
        for y,x in zip(*np.nonzero(fl)): out[y:y+2,x:x+2]=(64,60,64,255)
    elif style=='eventide':                        # máu đêm: sương máu, mép răng cưa, lõi đỏ, giọt máu dài rơi
        mist=ring(M,1,4); out[mist]=(90,6,20,90)
        d=depth(M)
        edge=M&(d==1)&~(hsh<0.3)
        paint(edge,(42,4,10),A); paint(M&(d==2),(132,10,30),A); paint(M&(d>=3),(228,38,58),A)
        hi=M&(d>=3)&(L>0.7); paint(hi,(255,140,150),A)
        dr=ring(M,1,3)&(hsh<0.035)&safe
        for y,x in zip(*np.nonzero(dr)):
            n=3+int(hsh[y,x]*120)%4
            for k in range(1,n+1):
                if y+k<H: out[y+k,x]=(160,12,32,235) if k<n else (90,6,18,200)
    elif style=='accord':                          # hoà ước: BA vệt song song, ba màu ba nước + ánh vàng
        up=sh(M,0,-4)&~M; dn=sh(M,0,4)&~M&~up
        out[dn]=(176,126,70,220)                                  # Kroven: đồng
        out[up]=(176,40,56,220)                                   # Miravel: đỏ
        d=depth(M); t=np.clip(L,0,1); core=M
        out[core,0]=(110+100*t)[core]; out[core,1]=(140+90*t)[core]; out[core,2]=(190+60*t)[core]; out[core,3]=A[core]   # Arden: xanh xám
        paint(M&(d==1),(70,90,130),A)
        gl=(M|up|dn)&(hsh<0.025); out[gl]=(255,238,170,255)
    elif style=='namesake':                        # tên khắc: trắng ngà, quầng xanh, nét chữ trong vệt + chữ cổ bay quanh
        d=depth(M)
        glow=ring(M,1,3); out[glow]=(120,170,250,60)
        paint(M&(d==1),(90,150,240),A*0.85); paint(M&(d>=2),(242,238,224),A)
        g=M&(d>=2)&((xs%5)==0)&(hsh<0.4)
        for y,x in zip(*np.nonzero(g)):
            out[y,x]=(70,110,190,255)
            if y+1<H and M[y+1,x]: out[y+1,x]=(70,110,190,255)
        runes=[((0,0),(1,0),(2,0),(1,1),(1,2)),((0,0),(0,1),(0,2),(1,1),(2,0),(2,2)),((0,0),(2,0),(1,1),(0,2),(2,2)),((1,0),(0,1),(2,1),(1,2))]
        rr=ring(M,3,8)&(hsh<0.008)&safe
        for y,x in zip(*np.nonzero(rr)):
            for dx,dy in runes[int(hsh[y,x]*1000)%4]:
                if y+dy<H and x+dx<W: out[y+dy,x+dx]=(110,160,245,230)
    elif style=='absolution':                      # xá tội: quầng hào quang nhiều lớp, viền đỏ sẫm, lông vũ trắng bay
        g1=ring(M,1,2); g2=ring(M,3,4)
        out[g2]=(255,226,140,45); out[g1]=(255,232,160,110)
        d=depth(M)
        paint(M&(d==1),(110,14,24),A); paint(M&(d==2),(222,182,92),A); paint(M&(d>=3),(255,244,212),A)
        fe=ring(M,2,9)&(hsh<0.012)&safe
        for y,x in zip(*np.nonzero(fe)):
            for k in range(4):
                if 0<=y-k<H and x+k<W: out[y-k,x+k]=(252,252,255,230) if k<3 else (210,210,230,150)
            if y+1<H and x+1<W: out[y+1,x+1]=(220,220,236,140)
    return Image.fromarray(np.clip(out,0,255).astype(np.uint8),'RGBA')
def write_meta(src,dst,old,new):
    t=open(src,encoding='utf-8',newline='').read()
    t=re.sub(r'^guid: [0-9a-f]+','guid: '+uuid.uuid4().hex,t,flags=re.M)
    t=re.sub(r'spriteID: [0-9a-f]+',lambda k:'spriteID: '+uuid.uuid4().hex,t)
    t=t.replace(old+'_',new+'_')
    open(dst,'w',encoding='utf-8',newline='').write(t)
def build(style,write=False):
    base=BASE[style]; outs=[]
    for n in (1,2,3):
        for k in KEYS:
            src=D+'%s_%d_%s.png'%(base,n,k); meta=src+'.meta'
            arr=np.array(Image.open(src).convert('RGBA'))
            fw=arr.shape[1]//frames_of(meta)
            im=restyle(style,arr,'%s_%d_%s'%(style,n,k),fw)
            if write:
                dst=D+'%s_%d_%s.png'%(style,n,k); im.save(dst); write_meta(meta,dst+'.meta',base,style)
            outs.append(((n,k),im,fw))
    return outs
if __name__=='__main__':
    write=len(sys.argv)>1 and sys.argv[1]=='write'
    styles=sys.argv[2:] if write and len(sys.argv)>2 else list(BASE)
    rows=[]
    for st in styles:
        o=build(st,write); pick=[x for x in o if x[0] in ((1,'E'),(3,'S'))]
        rows.append((st,pick)); print(st,'ok' if not write else 'written')
    if not write:
        Wd=1300; Hh=sum(max(p[1].height for p in r[1])+16 for r in rows)
        sheet=Image.new('RGBA',(Wd,Hh),(26,26,32,255)); y=0
        for st,pick in rows:
            x=0
            for (_,im,fw) in pick:
                c=im.crop((0,0,min(im.width,fw*6),im.height)); sheet.alpha_composite(c,(x,y)); x+=c.width+12
            y+=max(p[1].height for p in pick)+16
        sheet.save('preview/slash_forge.png')
