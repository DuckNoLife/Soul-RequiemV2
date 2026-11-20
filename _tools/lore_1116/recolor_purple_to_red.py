# Đổi tím/xanh-tím -> đỏ Vael, giữ độ sáng / độ bão hoà, giữ alpha
import colorsys
from PIL import Image
def shift(im):
    im=im.convert('RGBA'); px=im.load(); W,H=im.size
    for y in range(H):
        for x in range(W):
            r,g,b,a=px[x,y]
            if a==0: continue
            h,s,v=colorsys.rgb_to_hsv(r/255,g/255,b/255); h*=360
            if s<0.10 or not (175<=h<345): continue
            # 175..345 (xanh-tím..hồng) dồn về 342..8 quanh đỏ #c0303c
            t=(h-175)/170.0
            nh=(342+t*26)%360
            ns=min(1.0,s*1.08)
            nr,ng,nb=colorsys.hsv_to_rgb(nh/360,ns,v)
            px[x,y]=(round(nr*255),round(ng*255),round(nb*255),a)
    return im
