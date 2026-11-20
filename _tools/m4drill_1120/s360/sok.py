# Sokoban solver (BFS theo lượt ĐẨY, chuẩn hoá vị trí người chơi) cho Tally Room
from collections import deque
DIRS=[(1,0),(-1,0),(0,1),(0,-1)]
def parse(rows):
    H=len(rows); W=len(rows[0]); walls=set(); goals=set(); crates=set(); nocrate=set(); start=None
    for y,r in enumerate(rows):
        assert len(r)==W,(y,r,len(r))
        for x,c in enumerate(r):
            if c=='#': walls.add((x,y))
            if c in '.*+': goals.add((x,y))
            if c in '$*': crates.add((x,y))
            if c=='-': nocrate.add((x,y))
            if c in '@+': start=(x,y)
    return W,H,walls,goals,frozenset(crates),nocrate,start
def solve(rows, maxstates=3_000_000):
    W,H,walls,goals,crates,nocrate,start=parse(rows)
    inb=lambda p:0<=p[0]<W and 0<=p[1]<H
    def reach(p,cr):
        seen={p}; q=[p]
        while q:
            c=q.pop()
            for d in DIRS:
                n=(c[0]+d[0],c[1]+d[1])
                if inb(n) and n not in walls and n not in cr and n not in seen: seen.add(n); q.append(n)
        return seen
    # ô chết: góc tường (không phải đích)
    def blocked(p): return (not inb(p)) or p in walls
    dead=set()
    for x in range(W):
        for y in range(H):
            p=(x,y)
            if p in walls or p in goals: continue
            if p in nocrate: dead.add(p); continue
            for a,b in [((1,0),(0,1)),((1,0),(0,-1)),((-1,0),(0,1)),((-1,0),(0,-1))]:
                if blocked((x+a[0],y+a[1])) and blocked((x+b[0],y+b[1])): dead.add(p)
    r0=reach(start,crates); key0=(min(r0),crates)
    prev={key0:None}; q=deque([(crates,r0,key0)]); pushes={key0:0}
    while q:
        cr,rc,key=q.popleft()
        if set(cr)==goals:
            # truy vết
            path=[];k=key
            while prev[k]: k,mv=prev[k]; path.append(mv)
            return pushes[key], path[::-1], len(prev)
        for c in cr:
            for d in DIRS:
                behind=(c[0]-d[0],c[1]-d[1]); to=(c[0]+d[0],c[1]+d[1])
                if behind not in rc: continue
                if not inb(to) or to in walls or to in cr or to in dead: continue
                ncr=frozenset(cr-{c}|{to})
                nr=reach(c,ncr); nk=(min(nr),ncr)
                if nk in prev: continue
                prev[nk]=(key,(c,d)); pushes[nk]=pushes[key]+1; q.append((ncr,nr,nk))
                if len(prev)>maxstates: return None
    return None
