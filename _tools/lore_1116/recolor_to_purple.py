# Đổi xanh dương / xanh lơ -> tím Specimen Zero (hue ~268-290), giữ độ sáng + alpha. Dùng cho Moonlit Edge (rơi từ Specimen Zero).
import colorsys
from PIL import Image
def shift(im):
    im=im.convert('RGBA'); px=im.load(); W,H=im.size
    for y in range(H):
        for x in range(W):
            r,g,b,a=px[x,y]
            if a==0: continue
            h,s,v=colorsys.rgb_to_hsv(r/255,g/255,b/255); h*=360
            if s<0.08 or not (165<=h<262): continue
            nh=268+(h-165)/97*22
            nr,ng,nb=colorsys.hsv_to_rgb(nh/360,min(1,s*1.05),v)
            px[x,y]=(round(nr*255),round(ng*255),round(nb*255),a)
    return im
