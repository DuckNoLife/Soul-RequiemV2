# Thử khớp cửa: dựng tường thật bằng CK/OPK rồi dán khung 0 (đóng) + khung cuối (mở) của từng loại cửa lên đúng ô.
import sys, os; sys.path.insert(0, '.'); sys.path.insert(0, '../lore_1116')
from PIL import Image, ImageDraw
from ck import CK, GP
from opk import OPK
import render_tmx
os.environ['SR_CLEAN'] = '1'
CR = GP / "EPIC RPG World Pack - Crypt V.1.6/Props/animated/"
OPP = GP / "EPIC RPG World Pack - Old Prison V1.7.1/Props/"
def frames(p, n):
    im = Image.open(p).convert('RGBA'); fw = im.width // n
    return im.crop((0, 0, fw, im.height)), im.crop((fw * (n - 1), 0, fw * n, im.height))
def write(k, order, name):
    W, H = k.W, k.H
    csv = lambda g: ',\n'.join(','.join(str(v) for v in r) for r in g)
    out = ['<?xml version="1.0" encoding="UTF-8"?>', '<map version="1.10" orientation="orthogonal" renderorder="right-down" width="%d" height="%d" tilewidth="32" tileheight="32" infinite="0">' % (W, H)]
    base = "/tmp/gp/EPIC RPG World Pack - Crypt V.1.6/TiledMap Editor/Maps/"; root = str(k.ter.path.parents[3])
    for t in k.tiles: out.append(' <tileset firstgid="%d" source="%s"/>' % (t.firstgid, os.path.relpath(str(t.path).replace(root, '/tmp/gp'), base)))
    for i, n in enumerate(order):
        if n in k.L: out.append(' <layer id="%d" name="%s" width="%d" height="%d"><data encoding="csv">\n%s\n</data></layer>' % (i + 1, n, W, H, csv(k.L[n])))
    out.append('</map>'); p = base + name + '.tmx'; assert '/mnt/' not in os.path.realpath(base); open(p, 'w').write('\n'.join(out))
    o = '/tmp/' + name + '.png'; render_tmx.main(p, 0, 0, W, H, o, 1); return Image.open(o).convert('RGBA')
def paste(img, art, x_tile, bottom_tile, dx=0, dy=0):
    """Tile object kiểu Tiled: góc dưới-trái = (x_tile, bottom_tile) ô."""
    img.alpha_composite(art, (int(x_tile * 32 + dx), int(bottom_tile * 32 - art.height + dy)))
def grid(img):
    d = ImageDraw.Draw(img)
    for x in range(0, img.width, 32): d.line([(x, 0), (x, img.height)], fill=(255, 255, 255, 40))
    for y in range(0, img.height, 32): d.line([(0, y), (img.width, y)], fill=(255, 255, 255, 40))
    return img
