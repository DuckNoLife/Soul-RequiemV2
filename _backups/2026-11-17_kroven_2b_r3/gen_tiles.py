import json, random
from grid import W,H,S,FLOOR
T=json.load(open('walls_table.json'))
TAB={}
for k,v in T['walls'].items():
    tot=sum(v.values()); TAB[k]=[(g,n) for g,n in v.items() if n>=0.15*tot]
GROUND=[int(g) for g in T['ground']]
rnd=random.Random(2026)
def op(x,y): return 1 if (x,y) in FLOOR else 0
def key(x,y): return ''.join(str(op(x+dx,y+dy)) for dy in range(-1,4) for dx in (-1,0,1))
def pick(k):
    if k in TAB: c=TAB[k]
    else:
        best=min(TAB,key=lambda kk:sum(a!=b for a,b in zip(kk,k))*4 - (1 if kk[:]==k else 0)); c=TAB[best]; MISS.append(k)
    tot=sum(n for g,n in c); r=rnd.random()*tot
    for g,n in c:
        r-=n
        if r<=0: return int(g)
    return int(c[0][0])
MISS=[]
walls=[0]*(W*H); ground=[0]*(W*H)
SOLID=7120
for y in range(H):
    for x in range(W):
        i=y*W+x
        if (x,y) in FLOOR:
            ground[i]=rnd.choice(GROUND[:6])
        else:
            k=key(x,y)
            walls[i]=SOLID if k=='0'*15 else pick(k)
            if walls[i]!=SOLID: ground[i]=rnd.choice(GROUND[:6])
print('khoá thiếu',len(MISS),sorted(set(MISS))[:20])
