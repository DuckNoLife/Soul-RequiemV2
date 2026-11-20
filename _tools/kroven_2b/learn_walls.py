# Học bảng tường tự động từ Map 2: khoá = cửa sổ 3x5 (dx -1..1, dy -1..3) ô "mở" (lớp walls trống) -> các gid tường đã dùng
import sys, json, collections
import xml.etree.ElementTree as ET
M="../../Assets/GamePackage/EPIC RPG World - Sewers V1.5/TiledMap Editor/Maps/map2_undercity_of_vael.tmx"
r=ET.parse(M).getroot(); W=int(r.get('width')); H=int(r.get('height'))
def lay(n):
    L=[l for l in r.findall('layer') if l.get('name')==n][0]
    return [int(v)&0x1fffffff for v in L.find('data').text.replace('\n','').split(',')]
walls=lay('walls'); ground=lay('ground'); det=lay('wall details')
def op(x,y):
    if not(0<=x<W and 0<=y<H): return 0
    return 1 if walls[y*W+x]==0 else 0
def key(x,y): return ''.join(str(op(x+dx,y+dy)) for dy in range(-1,4) for dx in (-1,0,1))
T=collections.defaultdict(collections.Counter); D=collections.defaultdict(collections.Counter)
for y in range(H):
    for x in range(W):
        g=walls[y*W+x]
        if g: T[key(x,y)][g]+=1
        if det[y*W+x]: D[str(walls[y*W+x])][det[y*W+x]]+=1
G=collections.Counter(g for g in ground if g)
json.dump({'walls':{k:dict(v) for k,v in T.items()},'ground':dict(G.most_common(12)),'details':{k:dict(v) for k,v in D.items()}},open('walls_table.json','w'))
print(len(T),'khoá; ground top',G.most_common(8))
