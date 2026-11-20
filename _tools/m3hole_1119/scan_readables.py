# 19/11 (§342): tìm vật ĐỌC ĐƯỢC (sign / note / mechanic note, nhấn T) mà không có hình nào ở chỗ đó (tile object gid, ô tile không trống, sprite).
import re,os,sys,glob,html
for f in sorted(glob.glob('Assets/GamePackage/**/map*.tmx',recursive=True)):
    s=open(f,encoding='utf-8').read()
    W=int(re.search(r'<map [^>]*width="(\d+)"',s).group(1)); H=int(re.search(r'<map [^>]*height="(\d+)"',s).group(1))
    # các lớp tile (bỏ lớp nền / sàn): lớp có tên chứa decor / prop / object / wall / deco / above / furniture
    layers=[]
    for m in re.finditer(r'<layer id="\d+" name="([^"]+)"[^>]*>.*?<data encoding="csv">(.*?)</data>',s,re.S):
        nm=m.group(1).lower()
        if nm not in ('ground detail','grates','wall details','stone detail','fences','stone fences','deco','deco2','pil','rails','ruin walls','veins'): continue
        vals=[int(v) for v in m.group(2).replace('\n','').split(',') if v.strip()]
        if len(vals)==W*H: layers.append((nm,vals))
    gobjs=[]
    for m in re.finditer(r'<object id="\d+"(?: name="[^"]*")?(?: type="[^"]*")? gid="\d+" x="([\d.\-]+)" y="([\d.\-]+)" width="([\d.]+)" height="([\d.]+)"',s):
        x,y,w,h=map(float,m.groups()); gobjs.append((x,y-h,w,h))
    for m in re.finditer(r'<object id="(\d+)" name="([^"]*)" type="([^"]*)" x="([\d.\-]+)" y="([\d.\-]+)" width="([\d.]+)" height="([\d.]+)"[^>]*>(.*?)</object>',s,re.S):
        oid,name,typ=m.group(1),m.group(2),m.group(3); x,y,w,h=map(float,m.group(4,5,6,7)); body=m.group(8)
        mech=re.search(r'name="mechanic" value="([^"]*)"',body)
        readable = (typ=='sign' and 'name="auto" type="bool" value="true"' not in body and name not in ('r1_hatch','r2_spout_hint','stair_sign')) or (mech and mech.group(1)=='note') or typ=='note'
        if not readable: continue
        if re.search(r'name="(sprite|marker)"',body): continue
        hint=re.search(r'name="hint_text" value="([^"]*)"',body)
        if hint and html.unescape(hint.group(1)).startswith('Press '): continue
        pad=12
        hit=any(gx<x+w+pad and gx+gw>x-pad and gy<y+h+pad and gy+gh>y-pad for gx,gy,gw,gh in gobjs)
        if not hit:
            for nm,vals in layers:
                for ty in range(max(0,int((y-pad)//32)),min(H,int((y+h+pad)//32)+1)):
                    for tx in range(max(0,int((x-pad)//32)),min(W,int((x+w+pad)//32)+1)):
                        if vals[ty*W+tx]: hit=True;break
                    if hit:break
                if hit:break
        if not hit:
            sp=re.search(r'name="speaker" value="([^"]*)"',body)
            print('%-28s %5s %-26s %-6s tile(%.1f,%.1f) %s | %s'%(os.path.basename(f),oid,name,typ,x/32,y/32,sp.group(1) if sp else '', html.unescape(hint.group(1))[:70] if hint else ''))
