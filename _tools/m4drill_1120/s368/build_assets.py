# §368: dựng asset trạm 3 FINAL EXAM từ ảnh PixelLab (cửa sập, bảng đen, pháo cối, đống rơm, chân + vương miện Burlap)
from PIL import Image
import numpy as np
G='gen/'; O='out/'
def crop(im): return im.crop(im.getbbox())

# ---- cửa sập: 6 khung 48x48 (0 đóng ... 5 mở hẳn) -> drill/fx_trapdoor.png
tr=Image.open(G+'trap_10.png').convert('RGBA')
a=np.array(tr).astype(np.float32)
X0,X1,Y0,Y1=11,36,12,35          # lòng cửa (bao trong khung sắt)
mid=(X0+X1+1)//2                 # khe giữa 2 cánh
L=a[Y0:Y1+1, X0:mid].copy(); R=a[Y0:Y1+1, mid:X1+1].copy()
def pit():
    h=Y1-Y0+1; w=X1-X0+1
    p=np.zeros((h,w,4),np.float32); p[...,3]=255
    for y in range(h):
        t=min(1.0,y/5.0)
        c=np.array([46,40,54])*(1-t)+np.array([12,10,18])*t
        p[y,:,:3]=c
    p[:,0:2,:3]*=0.75; p[:,-2:,:3]*=0.75
    return p
frames=[]
for f in [1.0,0.72,0.45,0.22,0.08,0.0]:
    b=a.copy()
    if f<1.0:
        b[Y0:Y1+1,X0:X1+1]=pit()
        for leaf,left in ((L,True),(R,False)):
            lw=leaf.shape[1]; w=int(round(lw*f))
            if w<=0: continue
            img=Image.fromarray(leaf.astype(np.uint8)).resize((w,leaf.shape[0]),Image.NEAREST)
            l=np.array(img).astype(np.float32); l[...,:3]*=(0.5+0.5*f)
            x=X0 if left else X1+1-w
            m=l[...,3:4]/255.0
            b[Y0:Y1+1,x:x+w,:3]=l[...,:3]*m+b[Y0:Y1+1,x:x+w,:3]*(1-m)
    frames.append(Image.fromarray(np.clip(b,0,255).astype(np.uint8)))
strip=Image.new('RGBA',(48*len(frames),48))
for i,fr in enumerate(frames): strip.paste(fr,(i*48,0))
strip.save(O+'drill/fx_trapdoor.png')

# ---- bảng đen, pháo, đống rơm (chân ảnh ở mép dưới)
board=crop(Image.open(G+'board_2.png').convert('RGBA')); board.save(O+'props/drill_board.png')
crop(Image.open(G+'mortar_12.png').convert('RGBA')).save(O+'props/drill_mortar.png')
crop(Image.open(G+'hay_0.png').convert('RGBA')).save(O+'props/drill_haystack.png')
# lòng bảng (vùng tối) để in chữ phấn
ba=np.array(board).astype(int); dark=(ba[...,3]>200)&(ba[...,:3].max(axis=2)<70)&(ba[...,:3].max(axis=2)>20)
ys,xs=np.where(dark); 
print('board size',board.size,'slate bbox x',xs.min(),xs.max(),'y',ys.min(),ys.max())

# ---- Burlap: chân lộn ngược (cắm đầu vào rơm) + vương miện rơi
k=Image.open('ref/king_idle.png').convert('RGBA').crop((0,0,74,77))
legs=k.crop((20,58,54,77)).transpose(Image.FLIP_TOP_BOTTOM); crop(legs).save(O+'props/drill_king_legs.png')
ka=np.array(k).astype(int)
cr=np.zeros_like(ka)
for y in range(0,25):
    for x in range(74):
        r,g,bb,al=ka[y,x]
        if al<128: continue
        gold = r>150 and g>100 and bb<90
        twig = r>80 and r<170 and g<110 and bb<90 and r>g+15 and y<20
        dark = r<60 and g<60 and bb<60
        if gold or twig or (dark and y<22): cr[y,x]=ka[y,x]
ci=Image.fromarray(cr.astype(np.uint8)); ci=crop(ci); ci.save(O+'props/drill_king_crown.png')
for n in ['props/drill_board.png','props/drill_mortar.png','props/drill_haystack.png','props/drill_king_legs.png','props/drill_king_crown.png','drill/fx_trapdoor.png']:
    print(n, Image.open(O+n).size)
