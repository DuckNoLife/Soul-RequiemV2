from door_fit import *
import generate_map as gm, tiled_automap as ta
from ck import RULES
k = CK(24, 12); k.zone_area(0, 0, 24, 12); k.room(1, 1, 22, 10)
k.build()
M = gm.grid(25, 13)
for (x, y, w, h) in ((3, 3, 2, 1), (7, 3, 2, 2), (11, 3, 3, 2), (16, 3, 2, 3)):
    for cy in range(y, y + h + 1):
        for cx in range(x, x + w + 1): M[cy][cx] = 1
tb = k.w3.wang('wall-3', 1); gm.legalize(M, tb, 24, 12); lay = gm.to_layer(M, tb, 24, 12, k.rng, k.w3.firstgid)
lay = ta.Automapper([RULES / "wall-3-rule1-places all tiles.tmx", RULES / "wall-3-rule2-applies variations.tmx"], [(t.firstgid, t.path) for t in k.tiles]).apply(lay, "wall-1-2-3", k.rng)
k.L['plat'] = lay
grid(write(k, k.ORDER, '_t_pil')).save('ref/t_pillar.png')
