# §370: bảng đen to hơn mà vẫn 1:1 điểm ảnh — kéo dài kiểu 9-slice (nhân cột / hàng đồng màu, giữ nguyên cột gỗ, góc, huy hiệu sao)
from PIL import Image
import numpy as np
b=np.array(Image.open('gen/board_3.png').convert('RGBA'))
light=(b[...,0]>170)&(b[...,1]>170)&(b[...,2]>170); b[light]=0
EX_X=29   # mỗi bên -> lòng bảng 42 + 58 = 100 px
EX_Y=12   # lòng bảng 21 + 12 = 33 px
cols=list(range(0,35))+[34]*EX_X+list(range(35,63))+[62]*EX_X+list(range(63,b.shape[1]))
b=b[:,cols]
EX_LEG=10  # chân cột dài thêm -> bảng đứng cao ngang tầm mắt
rows=list(range(0,36))+[35]*EX_Y+list(range(36,52))+[51]*EX_LEG+list(range(52,b.shape[0]))
b=b[rows,:]
# vết phấn đã lau (mờ) trên lòng bảng cho đỡ phẳng
rng=np.random.default_rng(7)
sl=(b[...,0]==37)&(b[...,1]==52)&(b[...,2]==67)
for k in range(9):
    y=int(rng.integers(28,55)); x=int(rng.integers(30,120)); L=int(rng.integers(6,16))
    for i in range(L):
        if x+i<b.shape[1] and sl[y,x+i] and rng.random()<0.8: b[y,x+i,:3]=(46,61,76)
im=Image.fromarray(b); bb=im.getbbox(); im=im.crop(bb)
im.save('out/props/drill_board.png')
sx0,sx1=27,68+2*EX_X; sy0,sy1=25,45+EX_Y
cx=(sx0+sx1)/2-bb[0]; cy=(sy0+sy1)/2-bb[1]; W,H=im.size
print('board',im.size,'slate',sx1-sx0+1,'x',sy1-sy0+1,'centre (u) x',round((cx+0.5-W/2)/100,3),'y',round((H-cy-0.5)/100,3))
