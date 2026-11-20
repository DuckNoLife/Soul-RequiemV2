# Đổi tím/xanh-tím -> xanh lam Ascended (hue 196..222), giữ độ sáng / alpha. Dùng cho chiêu của Ren (chưa nhiễm gì).
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
            nh=196+(h-175)/170*26
            nr,ng,nb=colorsys.hsv_to_rgb(nh/360,s,v)
            px[x,y]=(round(nr*255),round(ng*255),round(nb*255),a)
    return im
