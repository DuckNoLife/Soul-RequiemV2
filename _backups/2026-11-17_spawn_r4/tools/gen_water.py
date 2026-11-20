# Kênh nước Sluice Walk theo bảng học từ Map 2 (water_table.json, khoá 3x3 ô ướt): 3 lớp curb surface / curb / curb water.
import json, random
from grid import W, H, WET
T=json.load(open('water_table.json'))
rnd=random.Random(77)
def k(x,y): return ''.join('1' if (x+dx,y+dy) in WET else '0' for dy in (-1,0,1) for dx in (-1,0,1))
def near(tab,kk):
    best=min(tab,key=lambda q:sum(a!=b for a,b in zip(q,kk))); return tab[best]
def pick(c):
    tot=sum(c.values()); r=rnd.random()*tot
    for g,n in c.items():
        r-=n
        if r<=0: return int(g)
    return int(next(iter(c)))
surface=[0]*(W*H); curb=[0]*(W*H); water=[0]*(W*H)
for y in range(H):
    for x in range(W):
        i=y*W+x; kk=k(x,y); wet=(x,y) in WET
        if wet:
            surface[i]=14599
            if kk!='1'*9: curb[i]=pick(T['curb'][kk] if kk in T['curb'] else near({q:v for q,v in T['curb'].items() if q[4]=='1'},kk))
            if kk=='1'*9:
                edge=any((x+dx,y+dy) not in WET for dx in (-2,0,2) for dy in (-2,0) if (dx,dy)!=(0,0))
                water[i]=14455 if edge else 14591
            else:
                water[i]=pick(T['water'][kk] if kk in T['water'] else near(T['water'],kk))
        elif kk in T['curb']:
            curb[i]=pick(T['curb'][kk])          # mép bờ trên ô khô sát nước (13234/13235/13236)
