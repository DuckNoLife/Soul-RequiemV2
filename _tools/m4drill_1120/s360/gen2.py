# Kho quân nhu: chuồng 9x8 (tường = kệ thùng), 2 lối vào 1 ô; ngoài chuồng là sàn trống ('-' = thùng không ra được)
import random, sys
from sok import solve
PW,PH=9,8          # chuồng gồm cả tường
def build(seed):
    rnd=random.Random(seed)
    W,H=PW+2,PH+2   # thêm vòng ngoài
    g=[['-']*W for _ in range(H)]
    for y in range(1,H-1):
        for x in range(1,W-1):
            edge = x in (1,W-2) or y in (1,H-2)
            g[y][x]='#' if edge else ' '
    # 2 lối vào (ô lối = '-' để thùng không đẩy ra ngoài)
    g[3][1]='-'; g[6][W-2]='-'
    inner=[(x,y) for y in range(2,H-2) for x in range(2,W-2)]
    for _ in range(rnd.randint(4,8)):
        x,y=rnd.choice(inner)
        if (x,y) in [(2,3),(W-3,6)]: continue
        g[y][x]='#'
    free=[c for c in inner if g[c[1]][c[0]]==' ']
    rnd.shuffle(free)
    for (x,y) in free[:3]: g[y][x]='.'
    for (x,y) in free[3:6]: g[y][x]='$'
    g[3][0]='@'
    return [''.join(r) for r in g]
res=[]
for seed in range(int(sys.argv[1]),int(sys.argv[2])):
    L=build(seed)
    r=solve(L,200000)
    if not r: continue
    p,path,st=r
    # đổi thùng
    sw=0
    for i in range(1,len(path)):
        (c0,d0),(c1,d1)=path[i-1],path[i]
        if c1!=(c0[0]+d0[0],c0[1]+d0[1]): sw+=1
    res.append((p,sw,st,seed))
res.sort(reverse=True)
for r in res[:15]: print(r)
