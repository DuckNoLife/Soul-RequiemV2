from door_fit import *
# --- CRYPT: phòng trên U (y1..6), phòng dưới L (y12..18), lối dọc 2 ô; phòng phải R nối ngang
k = CK(44, 20); k.zone_area(0, 0, 44, 20)
k.room(2, 1, 24, 6)           # U
k.room(2, 12, 24, 7)          # L
k.room(8, 7, 2, 5)            # lối 2 ô U->L (cửa mặt bắc của L: khung metal 4 ô ở x7..10)
k.room(19, 7, 3, 5)           # lối 3 ô U->L (cửa mặt nam của U: doors2 xuống ở x19..21)
k.room(30, 4, 12, 15)         # R
k.room(26, 15, 4, 3)          # lối ngang L->R 3 hàng (cửa dọc doors2 ở x29)
k.build()
a, b = frames(CR / "doors/doors-metal-door frame 1-opening.png", 9)
c, d = frames(CR / "doors2/wooden doors2-horizontal-downwards-opening.png", 10)
fr = Image.open(CR / "doors2/doors2-frame-horizontal-downwards.png").convert('RGBA')
e, f = frames(CR / "doors2/wooden doors2-vertical-rightwards-opening.png", 8)
fv = Image.open(CR / "doors2/doors2-frame-vertical-rightwards.png").convert('RGBA')
bd, bd2 = frames(CR / "doors/Boss door-opening.png", 40)
for tag, arts in (('closed', (a, c, e, bd)), ('open', (b, d, f, bd2))):
    im = write(k, k.ORDER, '_df_crypt')
    paste(im, arts[0], 7, 12)                       # khung metal: đáy = mép trên sàn L (hàng 12)
    paste(im, fr, 19, 10); paste(im, arts[1], 19, 11)  # doors2 xuống: khung 3x3 ngay dưới sàn U
    paste(im, fv, 29, 18); paste(im, arts[2], 27, 18)  # doors2 dọc
    paste(im, arts[3], 34, 7)                       # cửa boss trên mặt bắc R (3 ô)
    grid(im).save('ref/df_crypt_%s.png' % tag)
