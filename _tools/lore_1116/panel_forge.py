# Soul Requiem — vẽ lại nền bảng UI (panel_stone 79x57, panel_small 37x32): nền mực đen phẳng, viền thép + chỉ vàng, góc hoa văn nhỏ.
# Không còn nền đá tím loang (người dùng 16/11: khó đọc, "ớn"). Lát 9 ô giữ nguyên: trái 16, trên 18, phải 16, dưới 16.
from PIL import Image, ImageDraw
def hx(s): s=s.lstrip('#'); return tuple(int(s[i:i+2],16) for i in (0,2,4))+(255,)
OUT=hx('#0a080c'); STEEL=hx('#2b2631'); STEEL_H=hx('#3d3746'); STEEL_D=hx('#1d1a22')
GOLD=hx('#c9a24a'); GOLD_H=hx('#f0d48a'); GOLD_D=hx('#7a5c26'); GAP=hx('#110e14')
TOP=(0x1b,0x17,0x21); BOT=(0x11,0x0f,0x15)
def panel(W,H,orn=True):
    im=Image.new('RGBA',(W,H),(0,0,0,0)); d=ImageDraw.Draw(im); px=im.load()
    d.rectangle((0,0,W-1,H-1),fill=OUT)
    d.rectangle((1,1,W-2,H-2),fill=STEEL)
    d.line((2,1,W-3,1),fill=STEEL_H); d.line((1,2,1,H-3),fill=STEEL_H)
    d.line((2,H-2,W-3,H-2),fill=STEEL_D); d.line((W-2,2,W-2,H-3),fill=STEEL_D)
    d.rectangle((4,4,W-5,H-5),outline=GOLD_D); d.rectangle((3,3,W-4,H-4),outline=GOLD)
    d.line((4,3,W-5,3),fill=GOLD_H)
    d.rectangle((5,5,W-6,H-6),fill=GAP)
    # nền phẳng, chuyển rất nhẹ theo chiều dọc
    for y in range(7,H-7):
        t=(y-7)/max(1,H-15); c=tuple(int(TOP[i]+(BOT[i]-TOP[i])*t) for i in range(3))+(255,)
        d.line((7,y,W-8,y),fill=c)
    d.rectangle((6,6,W-7,H-7),outline=GOLD_D)
    if orn:
        for (cx,cy,sx,sy) in ((3,3,1,1),(W-4,3,-1,1),(3,H-4,1,-1),(W-4,H-4,-1,-1)):
            # góc: ô vuông vàng chéo + móc chữ L
            for k in range(0,7):
                px[cx+sx*k,cy]=GOLD_H if k<3 else GOLD; px[cx,cy+sy*k]=GOLD_H if k<3 else GOLD
            for (dx,dy) in ((1,1),(2,1),(1,2),(2,2)): px[cx+sx*dx,cy+sy*dy]=GOLD
            px[cx+sx*1,cy+sy*1]=GOLD_H
            # hạt kim cương nhỏ phía trong góc
            ix,iy=cx+sx*8,cy+sy*8
            for (dx,dy) in ((0,0),(1,0),(-1,0),(0,1),(0,-1)): px[ix+dx,iy+dy]=GOLD if (dx or dy) else GOLD_H
    return im
panel(79,57).save('panel_stone.png')
panel(37,32).save('panel_small.png')
# xem thử: kéo dãn 9 ô như trong game
def nine(im,W,H,l=16,t=18,r=16,b=16):
    w,h=im.size; out=Image.new('RGBA',(W,H))
    xs=[(0,l,0,l),(l,w-r,l,W-r),(w-r,w,W-r,W)]; ys=[(0,t,0,t),(t,h-b,t,H-b),(h-b,h,H-b,H)]
    for (sx0,sx1,dx0,dx1) in xs:
        for (sy0,sy1,dy0,dy1) in ys:
            out.paste(im.crop((sx0,sy0,sx1,sy1)).resize((dx1-dx0,dy1-dy0),0),(dx0,dy0))
    return out
prev=Image.new('RGBA',(900,360),(60,52,40,255))
prev.alpha_composite(nine(panel(79,57),540,170).resize((540*1,170*1),0),(20,20))
d=ImageDraw.Draw(prev); d.text((140,40),"The Swordmaster of Arden",fill=(236,200,112)); d.text((60,80),"Plain grey steel across the knees of a man who died sitting up.",fill=(225,215,195))
prev.alpha_composite(panel(79,57).resize((316,228),0),(580,20))
prev.save('/tmp/panel_prev.png')
