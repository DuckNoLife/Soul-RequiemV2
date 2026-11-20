from door_fit import *
k = CK(30, 20); o = OPK(k); o.zone_area(0, 0, 30, 20)
o.plat(1, 1, 28, 18)
# xà lim trái: tường nam y10..12, khe 4 ô x4..7 cho cổng xà lim (cột trụ hai bên)
o.cell(1, 2, 11, 11)
for x in range(4, 8):
    for y in range(10, 13): o.cells.pop((x, y), None); o.cell_block.discard((x, y))
for x in (3, 8):
    for j, p in enumerate([(14, 31), (15, 31), (16, 31)]): o._put(x, 10 + j, p)
# phòng gác phải: tường bắc y2..4 có vòm 3 ô x18..20 (cửa gỗ), cột tây có khe 3 hàng y8..10 (cửa gỗ dọc)
o.cell(14, 2, 12, 11, door_n=18, gap_w=(8, 9, 10))
# khối tường dày wall-2 + cổng đầu lâu trên mặt tường
o.wall(1, 14, 10, 5); o.plat(1, 14, 10, 5)
o.build()
order = o.ORDER
g, go = frames(OPP / "cell gate1 - openning1.png", 23)
w, wo = frames(OPP / "Wooden Doors/Wooden door1/horizontal-wooden door - opening animation.png", 9)
v, vo = frames(OPP / "Wooden Doors/Wooden door1/vertical-wooden door - opening animation.png", 8)
s, so = frames(OPP / "Prison Gate-with wall.png", 18)
ims = []
for arts in ((g, w, v, s), (go, wo, vo, so)):
    im = write(k, order, '_df_op')
    im.alpha_composite(arts[0], (4 * 32, 10 * 32 - 63))            # mép trên thanh dầm = mép trên tường
    im.alpha_composite(arts[1], (18 * 32, 5 * 32 + 18 - 128))      # đáy cánh cửa = chân tường
    im.alpha_composite(arts[2], (14 * 32 - 1, 11 * 32 + 9 - 128))  # cửa dọc trong khe cột tây
    ims.append(grid(im))
s2 = Image.new('RGBA', (ims[0].width * 2 + 8, ims[0].height), (255, 0, 255, 255)); s2.paste(ims[0], (0, 0)); s2.paste(ims[1], (ims[0].width + 8, 0))
s2 = s2.resize((s2.width * 3 // 2, s2.height * 3 // 2), Image.NEAREST); s2.save('ref/df_op.png')
