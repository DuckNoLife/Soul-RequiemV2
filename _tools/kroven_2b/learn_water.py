# Học bảng kênh nước (curb + curb water + curb surface) từ Map 2: khoá = 3x3 ô nước quanh ô -> gid từng lớp
import json, collections
import xml.etree.ElementTree as ET
M="../../Assets/GamePackage/EPIC RPG World - Sewers V1.5/TiledMap Editor/Maps/map2_undercity_of_vael.tmx"
r=ET.parse(M).getroot(); W=int(r.get('width')); H=int(r.get('height'))
def lay(n):
    L=[l for l in r.findall('layer') if l.get('name')==n][0]
    return [int(v)&0x1fffffff for v in L.find('data').text.replace('\n','').split(',')], L.get('tintcolor')
c,_=lay('curb'); cw,tw=lay('curb water'); cs,ts=lay('curb surface')
WET=set(i for i,v in enumerate(cw) if v)
def k(x,y): return ''.join('1' if 0<=x+dx<W and 0<=y+dy<H and (y+dy)*W+x+dx in WET else '0' for dy in (-1,0,1) for dx in (-1,0,1))
T={'curb':collections.defaultdict(collections.Counter),'water':collections.defaultdict(collections.Counter),'surface':collections.defaultdict(collections.Counter)}
for y in range(H):
    for x in range(W):
        i=y*W+x; kk=k(x,y)
        if c[i]: T['curb'][kk][c[i]]+=1
        if cw[i]: T['water'][kk][cw[i]]+=1
        if cs[i]: T['surface'][kk][cs[i]]+=1
json.dump({n:{kk:dict(v) for kk,v in t.items()} for n,t in T.items()},open('water_table.json','w'))
print({n:len(t) for n,t in T.items()}, 'tint',tw,ts)
for kk,v in sorted(T['curb'].items()): print('curb',kk,dict(v))
