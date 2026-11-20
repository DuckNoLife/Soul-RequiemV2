# §369: asset Final Exam vẽ lại theo bảng màu / góc nhìn gói Old Prison (nhìn thẳng, mật độ điểm ảnh 1:1, không phóng)
from PIL import Image
import numpy as np
G='gen/'; O='out/'
def crop(im): return im.crop(im.getbbox())

# ---- bảng đen (#3): bỏ dải nền trắng, kéo rộng lòng bảng (nhân cột 34 và 62, tránh huy hiệu sao ở giữa)
b=np.array(Image.open(G+'board_3.png').convert('RGBA'))
light=(b[...,0]>170)&(b[...,1]>170)&(b[...,2]>170)
b[light]=0
EXTRA=14
cols=list(range(0,35))+[34]*EXTRA+list(range(35,63))+[62]*EXTRA+list(range(63,b.shape[1]))
b2=b[:,cols]
board=Image.fromarray(b2); bb=board.getbbox(); board=board.crop(bb); board.save(O+'props/drill_board.png')
sx0,sx1=27,68+2*EXTRA; sy0,sy1=25,45
cx=(sx0+sx1)/2-bb[0]; cy=(sy0+sy1)/2-bb[1]
W,H=board.size
print('board',board.size,'slate px',sx1-sx0+1,'x',sy1-sy0+1,'slate centre from pivot (u):',round((cx+0.5-W/2)/100,3), round((H-cy-0.5)/100,3))

# ---- cửa sập (#2): 6 khung đóng -> mở, cắt theo khung sắt
tr=Image.open(G+'trap_2.png').convert('RGBA')
a=np.array(tr).astype(np.float32)
X0,X1,Y0,Y1=24,71,24,71
mid=(X0+X1+1)//2
L=a[Y0:Y1+1, X0:mid].copy(); R=a[Y0:Y1+1, mid:X1+1].copy()
def pit():
    h=Y1-Y0+1; w=X1-X0+1
    p=np.zeros((h,w,4),np.float32); p[...,3]=255
    for y in range(h):
        t=min(1.0,y/7.0)
        p[y,:,:3]=np.array([30,36,56])*(1-t)+np.array([6,8,16])*t      # vách hố xanh đen như đá gói, đáy đen
    p[:,0:3,:3]*=0.7; p[:,-3:,:3]*=0.7
    return p
frames=[]
for f in [1.0,0.72,0.45,0.22,0.08,0.0]:
    c=a.copy()
    if f<1.0:
        c[Y0:Y1+1,X0:X1+1]=pit()
        for leaf,left in ((L,True),(R,False)):
            lw=leaf.shape[1]; w=int(round(lw*f))
            if w<=0: continue
            l=np.array(Image.fromarray(leaf.astype(np.uint8)).resize((w,leaf.shape[0]),Image.NEAREST)).astype(np.float32)
            l[...,:3]*=(0.45+0.55*f)
            x=X0 if left else X1+1-w
            m=l[...,3:4]/255.0
            c[Y0:Y1+1,x:x+w,:3]=l[...,:3]*m+c[Y0:Y1+1,x:x+w,:3]*(1-m)
    frames.append(Image.fromarray(np.clip(c,0,255).astype(np.uint8)).crop((18,18,78,78)))
strip=Image.new('RGBA',(60*len(frames),60))
for i,fr in enumerate(frames): strip.paste(fr,(i*60,0))
strip.save(O+'drill/fx_trapdoor.png')
print('trapdoor frame 60x60, interior', X1-X0+1)

m=crop(Image.open(G+'mortar_0.png').convert('RGBA')); m.save(O+'props/drill_mortar.png'); print('mortar',m.size)
h=crop(Image.open(G+'hay_2.png').convert('RGBA')); h.save(O+'props/drill_haystack.png'); print('hay',h.size)
