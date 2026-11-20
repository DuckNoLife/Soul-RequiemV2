# Thử: mặt nạ góc -> wang wall-N -> luật tác giả -> ảnh. python3 t_wall.py <1|2|3>
import sys,os,random
from pathlib import Path
CAT=Path.home()/"mnt/Catalog"; sys.path.insert(0,str(CAT)); sys.path.insert(0,str(CAT/"v2"))
import generate_map as gm, tiled_automap as ta
from mapkit import Tileset
n=sys.argv[1]
ED=Path("/tmp/gp/EPIC RPG World Pack - Crypt V.1.6/TiledMap Editor")
W,H=44,26
ter=Tileset(ED/"Tilesets/Tileset-Terrain.tsx"); wl=Tileset(ED/("Tilesets/Tileset - wall %s.tsx"%n))
ter.firstgid=1; wl.firstgid=1+ter.span
M=gm.grid(W+1,H+1)
def box(x0,y0,x1,y1):
    for y in range(y0,y1+1):
        for x in range(x0,x1+1): M[y][x]=1
box(0,0,44,3); box(0,0,2,26); box(42,0,44,26); box(0,24,44,26)      # viền khối
box(6,8,14,8); box(6,8,6,14)            # dải 1 hàng góc
box(18,8,26,9)                           # dải 2 hàng góc
box(30,8,38,11)                          # dải 4 hàng góc
box(10,17,20,20); box(14,15,16,17)       # khối có mấu
box(26,16,27,22)                         # vách đứng 2 góc
wsname=[w for w in wl.wangsets][0] if hasattr(wl,'wangsets') else None
tb=wl.wang({'1':'wall-111','2':'wall-2','3':'wall-3'}[n],1)
gm.legalize(M,tb,W,H)
rng=random.Random(3)
lay=gm.to_layer(M,tb,W,H,rng,wl.firstgid)
tsl=[(ter.firstgid,ter.path),(wl.firstgid,wl.path)]
am=ta.Automapper([ED/("Rules/wall-%s-rule1-places all tiles.tmx"%n),ED/("Rules/wall-%s-rule2-applies variations.tmx"%n)],tsl)
out=am.apply([r[:] for r in lay],"wall-1-2-3",rng)
g1=ter.wang("ground1 to ground2",1)[(1,1,1,1)]
ground=[[ter.firstgid+gm.pick(rng,g1) for x in range(W)] for y in range(H)]
def csv(g): return ',\n'.join(','.join(str(v) for v in r) for r in g)
p=ED/"Maps"/("_t_wall%s.tmx"%n)
open(p,'w').write('<?xml version="1.0" encoding="UTF-8"?>\n<map version="1.10" orientation="orthogonal" renderorder="right-down" width="%d" height="%d" tilewidth="32" tileheight="32" infinite="0">\n <tileset firstgid="%d" source="../Tilesets/Tileset-Terrain.tsx"/>\n <tileset firstgid="%d" source="../Tilesets/Tileset - wall %s.tsx"/>\n <layer id="1" name="g" width="%d" height="%d"><data encoding="csv">\n%s\n</data></layer>\n <layer id="2" name="w" width="%d" height="%d"><data encoding="csv">\n%s\n</data></layer>\n</map>\n'%(W,H,ter.firstgid,wl.firstgid,n,W,H,csv(ground),W,H,csv(out)))
sys.path.insert(0,'../lore_1116'); import render_tmx
render_tmx.main(str(p),0,0,W,H,'ref/t_wall%s.png'%n,1)
