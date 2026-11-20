from door_fit import *
k = CK(30, 22); o = OPK(k); o.zone_area(0, 0, 30, 22)
o.plat(0, 0, 30, 22)
# phòng tường dày: khối wall-2 x2..17 y2..14, ruột x4..15 y4..12 (tường dày 2)
o.wall(2, 2, 16, 13)
for cy in range(5, 13):
    for cx in range(5, 16): o.Mwall[cy][cx] = 0
# khe cửa ở tường nam (y13..14) x9..10
for cy in range(13, 16):
    for cx in range(9, 12): o.Mwall[cy][cx] = 0
# khối dày thứ 2: dải tường ngang chạy hết (x20..29, y8..10) — cho cổng đầu lâu
o.wall(20, 8, 10, 3)
o.build()
im = write(k, o.ORDER, '_t_op2')
grid(im).save('ref/t_op2.png')
