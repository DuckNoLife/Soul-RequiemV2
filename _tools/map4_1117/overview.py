# Ảnh toàn cảnh khung Map 4 có nhãn khu, đường đi chính, và hình cửa thật (khung đóng) ở các chỗ đã chốt.
from PIL import Image, ImageDraw, ImageFont
from door_fit import frames, CR, OPP
im = Image.open('ref/frame.png').convert('RGBA')          # tỉ lệ 1 (32 px/ô)
def door(path, n, x, y_top, art_top=0):
    a, _ = frames(path, n); im.alpha_composite(a, (int(x * 32), int(y_top * 32 - art_top)))
door(CR / "doors/Boss door-opening.png", 30, 78, 93)                       # cửa Hall of Naming (mặt gạch bắc sảnh chờ)
s = 0.4; im = im.convert('RGB').resize((int(im.width * s), int(im.height * s)), Image.LANCZOS); d = ImageDraw.Draw(im)
f = ImageFont.load_default(size=18); T = 32 * s
Z = [('1 Oubliette', 99, 9), ('2 Cell Rows', 62, 15), ('Torture', 51, 25), ('Guard room', 74, 25), ('3 Blood Pit', 5, 22),
     ('4 Ossuary (hub, rest)', 50, 63), ('5 Scriptorium', 8, 62), ("Mother's letters", 6, 79), ('6 Royal Reliquary', 93, 62),
     ('Inventory vault', 102, 77), ("7 Penitent's Walk", 58, 84), ('antechamber + 2 seals', 58, 98), ('8 Hall of Naming', 80, 82),
     ('9 Descent', 104, 95)]
for t, x, y in Z:
    X, Y = x * T, y * T; bb = d.textbbox((X, Y), t, font=f); d.rectangle((bb[0] - 3, bb[1] - 2, bb[2] + 3, bb[3] + 2), fill=(0, 0, 0)); d.text((X, Y), t, font=f, fill=(255, 230, 120))
P = [(107, 16), (97, 15), (70, 17), (46, 15), (36, 15), (14, 15), (8, 20), (8, 31), (40, 31), (46, 40), (46, 50), (63, 58), (20, 58), (63, 58), (102, 58), (63, 58),
     (63, 98), (79, 98), (79, 80), (96, 88), (108, 94)]
d.line([(x * T, y * T) for x, y in P], fill=(255, 80, 60), width=3)
im.save('ref/frame_overview.png'); print(im.size)
