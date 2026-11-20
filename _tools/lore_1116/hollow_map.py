# Map 1b — Hermit's Hollow (khu bí mật Oathbreaker, dưới Old Copper Mine Map 1). Sinh trọn file .tmx từ sơ đồ ô dưới đây.
# python3 hollow_map.py   -> .../Maps/map1b_hermits_hollow.tmx
import json, random, xml.sax.saxutils as sx
GL="../../Assets/GamePackage/ERW - Grass Land 2.0 v2.1/TiledMap Editor/"
M1=GL+"Maps/map1_greystone_plains.tmx"
OUT=GL+"Maps/map1b_hermits_hollow.tmx"
IDS=json.load(open('hollow_ids.json'))
MINE_FIRST=12084
def mine(n): return MINE_FIRST+IDS[n]

# ------------------------------------------------------------------ sơ đồ (34 x 26). '.' sàn mỏ  ',' sàn vỉa xanh  's' The Still Walk  'c' buồng ẩn sĩ
G=[
"##################################",  # 0
"##################################",  # 1
"##################################",  # 2
"##################################",  # 3
"######################ccccccc#####",  # 4
"##,,,,,,ssssssssssssssccccccccc###",  # 5
"##,,,,,,ssssssssssssssccccccccc###",  # 6
"##,,,,,,ssssssssssssssccccccccc###",  # 7
"##,,,,,,##############cccccccc####",  # 8
"###,,,,,###############cccccc#####",  # 9
"###,,,,,,##############ccccc######",  # 10
"##,,,,,,,#########################",  # 11
"##,,,,,,##########################",  # 12
"##,,,,,,##########################",  # 13
"###,,,,,##########################",  # 14
"###,,,,,##..............##########",  # 15
"##,,,,,,#................#########",  # 16
"##,,,,,,,,...............#########",  # 17
"###,,,,,,,...............#########",  # 18
"###,,,,##................#########",  # 19
"#########...............##########",  # 20
"###############...################",  # 21
"###############...################",  # 22
"##############....################",  # 23
"###############...################",  # 24
"###############...################",  # 25
]
W,H=len(G[0]),len(G)
assert all(len(r)==W for r in G)
FLOOR=set((x,y) for y in range(H) for x in range(W) if G[y][x]!='#')
def fl(x,y): return (x,y) in FLOOR
# mặt vách: 2 ô đá ngay trên mỗi ô sàn có đá phía trên
FACE={}
for (x,y) in FLOOR:
    if 0<=y-1 and not fl(x,y-1):
        FACE[(x,y-1)]=2
        if y-2>=0 and not fl(x,y-2) and (x,y-2) not in FACE: FACE[(x,y-2)]=1
def open_(x,y): return fl(x,y) or (x,y) in FACE
rnd=random.Random(1116)

N=W*H
ground=[0]*N; floor2=[0]*N; walls=[0]*N; veins=[0]*N; ceil=[0]*N; corners=[0]*N
DIRT=[142,143,144,145,146,197,198,199,200,201,252,253,254,255,256]          # đất mỏ (bộ đặc)
SEAM=[8824,8825,8826]                                # đất sẫm ánh xanh (hole3)
FACE_UP=[9191,9192,9193,9194,9195]
FACE_LO=[9198,9199,9200,9201,9202,9203]
for y in range(H):
    for x in range(W):
        i=y*W+x; c=G[y][x]
        if (x,y) in FLOOR or (x,y) in FACE:
            ground[i]=rnd.choice(DIRT)
        pass  # 16/11: bỏ ô đất sẫm (nhìn thành ô vuông) — vỉa xanh thể hiện bằng vũng + vân + ánh sáng
        if (x,y) in FACE:
            walls[i]=rnd.choice(FACE_UP if FACE[(x,y)]==1 else FACE_LO)
        elif (x,y) not in FLOOR:
            m=0
            if y>0 and open_(x,y-1): m|=1
            if x<W-1 and open_(x+1,y): m|=2
            if y<H-1 and open_(x,y+1): m|=4
            if x>0 and open_(x-1,y): m|=8
            ceil[i]=mine('ceil_%02d'%m) if m else mine('ceil_plain_%d'%rnd.randrange(3))
            # góc trong: chỉ đường chéo hở
            for name,dx,dy,b1,b2 in (('ic_ne',1,-1,1,2),('ic_se',1,1,4,2),('ic_sw',-1,1,4,8),('ic_nw',-1,-1,1,8)):
                if not (m&b1) and not (m&b2) and 0<=x+dx<W and 0<=y+dy<H and open_(x+dx,y+dy):
                    corners[i]=mine(name)   # một ô chỉ một mẩu góc là đủ ở map này

# ------------------------------------------------------------------ đối tượng
oid=[0]
def nid(): oid[0]+=1; return oid[0]
T=32
def P(k,v,typ=None):
    t=' type="%s"'%typ if typ else ''
    if typ is None and isinstance(v,bool): t=' type="bool"'; v='true' if v else 'false'
    elif typ is None and isinstance(v,int): t=' type="int"'
    elif typ is None and isinstance(v,float): t=' type="float"'
    v=str(v).replace('\n','\\n')   # 16/11 (§270): xuống dòng ghi dạng chữ "\n" như các map khác (xuống dòng thật trong thuộc tính XML bị gộp thành dấu cách)
    return '    <property name="%s"%s value="%s"/>'%(k,t,sx.escape(v,{'"':'&quot;'}))
def obj(name=None,typ=None,x=0,y=0,w=None,h=None,gid=None,props=None,shape=None):
    a=' <object id="%d"'%nid()
    if name: a+=' name="%s"'%name
    if typ: a+=' type="%s"'%typ
    if gid: a+=' gid="%d"'%gid
    a+=' x="%s" y="%s"'%(round(x,1),round(y,1))
    if w is not None: a+=' width="%s" height="%s"'%(round(w,1),round(h,1))
    body=[]
    if props:
        body.append('   <properties>')
        for k,v in props:
            body.append('  '+(P(k,v[0],v[1]) if isinstance(v,tuple) else P(k,v)))
        body.append('   </properties>')
    if shape=='point': body.append('   <point/>')
    if shape=='ellipse': body.append('   <ellipse/>')
    if not body: return [a+'/>']
    return [a+'>']+body+[' </object>']
from tmx_sizes import Sizer
SZ=Sizer(M1,extra=[(MINE_FIRST,'../Tilesets/SR-mine.tsx')])
def tile(gid,cx,by,flip=False):
    """tile object: tâm ngang cx (ô), chân by (ô)"""
    w,h=SZ(gid); g=gid|(0x80000000 if flip else 0)
    return obj(gid=g,x=cx*T-w/2,y=by*T,w=w,h=h)

groups={k:[] for k in ['tree shadows','ground decals','props','props front','ambient fx','zones','spawns','triggers','gates','obstacle colliders','water colliders','blockers','map bounds','mechanics']}
D=groups['ground decals']; PR=groups['props']; ME=groups['mechanics']; TR=groups['triggers']; OB=groups['obstacle colliders']
def obstacle(x,y,w,h,ell=False): OB.extend(obj('prop collider','obstacle',x,y,w,h,props=[('collider_kind','prop')],shape='ellipse' if ell else None))
def light(name,cx,cy,color,radius,inten=None,anim=None,amount=None,speed=None,off_when=None):
    p=[('color',color),('light','torch'),('mechanic','light'),('radius',(str(radius),'float'))]
    if inten is not None: p.append(('intensity',(str(inten),'float')))
    if anim: p.append(('anim',anim))
    if amount is not None: p.append(('anim_amount',(str(amount),'float')))
    if speed is not None: p.append(('anim_speed',(str(speed),'float')))
    if off_when: p.append(('off_when',off_when))
    p.append(('unity:IsTrigger',True))
    ME.extend(obj(name,'light',cx*T-16,cy*T-16,32,32,props=sorted(p)))

# ---- lối vào / ra (đường hầm phía nam, dốc lên miệng mỏ)
TR.extend(obj('player_start','player_start',15*T,22*T,T,T,props=[('unity:IsTrigger',True),('zone_id','h1')]))
TR.extend(obj('hollow_out','exit',15*T,25.35*T,3*T,0.65*T,props=sorted([
    ('arrive_at','10.5,49.4'),('arrive_subtitle','Greystone Plains'),('arrive_title','Old Copper Mine'),
    ('depart_text','You climb back toward the grey light of the mine mouth.'),('quake',False),
    ('target','map1_greystone_plains'),('unity:IsTrigger',True)])))
light('h1_daylight',16.5,24.6,'#c8d4ff',3.2,0.55)
PR.extend(tile(mine('mine_arch'),16.5,21.15))                       # khung gỗ chống lò ở cửa hầm
obstacle(15.0*T,20.75*T,0.45*T,0.35*T,True); obstacle(17.55*T,20.75*T,0.45*T,0.35*T,True)   # §303 cột tròn: trượt quanh, không kẹt góc

# ---- Collapsed Gallery (h1): ổ nhện, gỗ chống sập, quặng xanh rơi vãi, tấm bảng của Oswin
for (g,cx,by,fp) in [(11630,12.2,16.4,False),(11630,21.0,20.6,True),(11630,11.0,19.6,True),
                     (9431,13.4,17.3,False),(9468,18.6,15.9,False),(9431,22.4,19.2,True)]:
    D.extend(tile(g,cx,by,fp))
for (g,cx,by) in [(mine('rockfall_0'),10.7,15.9),(mine('mine_rocks'),24.0,18.4),(mine('rockfall_1'),19.6,21.0),(mine('rockfall_0'),9.9,20.3)]:
    PR.extend(tile(g,cx,by)); w,h=SZ(g); obstacle(cx*T-w*0.35,by*T-10,w*0.7,9,True)
for (g,cx,by) in [(11699,14.6,19.4),(11699,11.8,18.0)]:   # quặng đồng rơi vãi
    D.extend(tile(g,cx,by))
PR.extend(tile(mine('mine_table'),13.6,15.75)); obstacle(11.8*T,15.35*T,3.6*T,0.4*T)   # bàn dụng cụ của thợ mỏ, sát vách bắc
# tấm bảng Oswin (cha Hob) cạnh lối hầm phía tây
PR.extend(tile(mine('mine_signpost'),9.7,17.0)); obstacle(9.45*T,16.75*T,0.5*T,0.25*T,True)
ME.extend(obj('oswin_board','note',9.0*T,15.6*T,1.4*T,1.4*T,props=sorted([
    ('mechanic','note'),('range',('1.2','float')),('title',"A Board by the West Tunnel"),('unity:IsTrigger',True),
    ('text',"Scratched into the board with a nail, the letters gone grey with age.\n\nDEEP VEIN - SHUT.\n\nNot for the copper. The copper ran out years back. Shut for the green, and for what's past it.\n\nSomething down there counts your steps. You walk, it clicks. You stand, it stops. I stood a long time.\n\n- O.")])))
# ổ nhện + nhện
# 16/11 (§264): bỏ Cave_Spawner (hình hang rêu xanh lạc tông mỏ) -> nhện thường, 2 đợt
for (x,y,wv) in [(14.2,18.2,1),(18.4,19.0,1),(21.0,16.6,1),(11.6,17.0,2),(19.8,17.6,2),(23.2,18.8,2)]:
    groups['spawns'].extend(obj('Spider','spawn',x*T,y*T,props=sorted([('enemy','Spider'),('wave',wv),('zone_id','h1')]),shape='point'))
# ---- The Green Seam (h2): vỉa đồng gỉ xanh, Ether rỉ qua đá -> vũng độc; đường đi zíc zắc
pools=[('seep_c',4.1,11.2),('seep_a',5.9,14.9),('seep_b',3.4,17.6),('seep_d',5.2,7.9)]
for k,(n,cx,by) in enumerate(pools):
    g=mine(n); w,h=SZ(g); D.extend(tile(g,cx,by))
    ME.extend(obj('seam_pool_%d'%k,'hazard',cx*T-w*0.42,by*T-h*0.86,w*0.84,h*0.76,props=sorted([
        ('dps_percent',('6','float')),('mechanic','hazard'),('poison_percent',('8','float')),('poison_seconds',('2.5','float')),
        ('shape','ellipse'),('unity:IsTrigger',True)])))
    light('seam_glow_%d'%k,cx,by-h/T/2,'#5dffa0',round(2.0+w/96,2),0.7,'pulse',0.25,round(0.35+0.07*k,2))
for (x,y,v) in [(3,4,0),(5,3,1),(6,4,2),(2,15,1),(4,3,2)]:
    if (x,y) in FACE: veins[y*W+x]=mine('vein_%d'%v)
for (x,y,v) in [(10,13,0),(11,14,2),(9,14,1)]:      # vân xanh lan sang vách tây phòng sập (dẫn mắt về phía tây)
    if (x,y) in FACE: veins[y*W+x]=mine('vein_%d'%v)
for (x,y) in [(4.2,9.0),(6.6,16.6)]:
    groups['spawns'].extend(obj('Lizard','spawn',x*T,y*T,props=sorted([('enemy','Lizard'),('wave',1),('zone_id','h2')]),shape='point'))
D.extend(tile(11699,2.9,13.3))

# ---- The Still Walk (h3): máy bắn tên của người ẩn sĩ CHỈ bắn khi Ren đang bước
LANES=[9,10,12,13,15,16,18,19]; SAFE=[11,14,17,20]
ME.extend(obj('still_walk','stillness',8.6*T,5*T,12.4*T,3*T,props=sorted([
    ('fires','still_walk'),('hint',"The air is dead still. Somewhere in the rock, something waits to hear you move."),
    ('mechanic','stillness'),('rearm',('0.8','float')),('speed_min',('0.35','float')),('unity:IsTrigger',True)])))
for c in LANES:
    late=c>=15
    ME.extend(obj('still_arrow_%d'%c,'launcher',c*T,4*T,T,T,props=sorted([
        ('burst',3 if late else 2),('burst_gap',('0.28','float')),('damage',('18','float')),('dir','down'),('kind','arrow'),
        ('mechanic','launcher'),('mode','trigger'),('range',('4','float')),('speed',('8','float')),('trigger','still_walk'),
        ('unity:IsTrigger',True),('warn_seconds',('0.6','float')),('art','mine')])))
for c in SAFE+[8,21]:
    for r in (5,6,7):
        D.extend(tile(mine('still_slab_%d'%((c+r)%3)),c+0.5,r+1))
for (c,r,v) in [(9,6,0),(10,7,1),(13,5,2),(12,7,0),(15,6,1),(16,5,2),(18,7,0),(19,6,1),(16,7,0)]:
    D.extend(tile(mine('arrow_stuck_%d'%v),c+0.5,r+0.95))
ME.extend(obj('still_carving','note',7.6*T,3.2*T,1.6*T,1.8*T,props=sorted([
    ('mechanic','note'),('range',('1.3','float')),('title','Cut into the Rock'),('unity:IsTrigger',True),('verb','Read'),
    ('text',"Letters cut deep and careful, by someone with time.\n\nSTAND.  BREATHE.  THEN WALK.\n\nThe arrows were not set for beasts. Beasts do not wait.\n\nOn the flat stones you may stand. Anywhere else, do not stop.")])))

# ---- Hermit's Cell (h4)
D.extend(tile(mine('tally'),23.3,3.9))                                    # 14 vạch: 14 năm
ME.extend(obj('merek_page','note',22.4*T,2.2*T,2.2*T,1.9*T,props=sorted([
    ('mechanic','note'),('range',('1.3','float')),('title','Cut Beside the Tally'),('unity:IsTrigger',True),('verb','Read'),
    ('text',"Fourteen marks, then the same careful hand.\n\nI was the King's sword in Arden. In the 14th year of King Lucas he gave me a village in the west and a child the stars had marked, and told me to come home alone.\n\nI found the child. I drew. I did not cut.\n\nA man who swears to a king swears to the king's hand. I broke that oath in a barn in the west, and I have kept the broken half ever since. Let them call the blade what they like.\n\nI set the arrows in the long hall so that hunters who run will die and men who wait may pass. Lucas never sent a patient man.\n\nIf you are reading this, you waited. Take the sword. Draw it the way I learned to down here: once, and only when you are sure.\n\n- Merek")])))
PR.extend(tile(mine('../SR-trimmed/hermit_remains'),25.6,5.55)); obstacle(25.0*T,5.25*T,1.2*T,0.3*T,True)
light('cell_blade_glow',25.6,6.0,'#9fb8d6',2.2,0.7,'pulse',0.2,0.4,'oathbreaker_taken')
ow,oh=SZ(mine('../SR-trimmed/relic_oathbreaker'))
ME.extend(obj('cell_oathbreaker','note',25.6*T-ow/2,6.45*T-oh,ow,oh,gid=mine('../SR-trimmed/relic_oathbreaker'),props=sorted([
    ('key_got',"Oathbreaker. Hold your attack and wait while the meter beside you fills, then release: the cut is drawn like his - far heavier, and it staggers."),
    ('mechanic','note'),('pickup',True),('range',('1.3','float')),('sets','oathbreaker_taken'),('unity:IsTrigger',True),
    ('title','The Swordmaster of Arden'),('verb','Take the blade'),('weapon_id','katana_oathbreaker'),
    ('text',"A sheathed blade in plain grey steel, laid on the stone before a man who died sitting up. The seal on the scabbard was never broken.\n\nThe King's swordmaster carried it into the west to end a child. It came back out of the west unbloodied, and its master never came back at all.")])))
PR.extend(tile(9376,27.6,8.4)); obstacle(26.9*T,7.95*T,1.4*T,0.4*T,True)       # bếp lửa nguội
PR.extend(tile(11694,28.7,6.1)); obstacle(28.35*T,5.85*T,0.7*T,0.25*T,True)          # giỏ
D.extend(tile(11630,27.0,10.4))                                                # khúc gỗ làm ghế
# 16/11 (§265): KHÔNG có lối tắt nào về Gallery — đường duy nhất tới thanh kiếm là qua The Still Walk (người dùng).

# ---- 16/11 (§267): mốc nhiệm vụ phụ (Resources/Quests/Map1b_HermitsHollow.json) — bước vào là ghi cờ
def mark(name,flag,x,y,w,h):
    TR.extend(obj(name,'story',x*T,y*T,w*T,h*T,props=sorted([('script','set '+flag),('trigger','enter'),('unity:IsTrigger',True),('unless',flag)])))
mark('mark_seam','hollow_seam',2,8,6,12)          # vào vỉa xanh
mark('mark_walk','hollow_walk',8,5,2,3)           # tới đầu hành lang bẫy
mark('mark_cell','hollow_cell',22,4,8,7)          # qua được hành lang, vào buồng

# ---- khu (zone)
def zone(zid,title,x,y,w,h,amb,col,fight):
    groups['zones'].extend(obj(zid,'zone',x*T,y*T,w*T,h*T,props=sorted([
        ('ambient',amb),('ambient_color',col),('ambient_fight',fight),('fog','off'),   # 16/11 (§267): map bí mật hiện rõ, không phủ sương('hint_text',''),('optional',True),
        ('title',title),('unity:IsTrigger',True),('zone_id',zid)])))
zone('h1','Collapsed Gallery',8.5,14.5,17,11.5,'0.24','#9a8f86','0.2')
zone('h2','The Green Seam',1,4.5,7.5,15.5,'0.2','#7fa08f','0.17')
zone('h3','The Still Walk',8.5,4,13,4.5,'0.17','#8a90a6','0.15')
zone('h4',"Hermit's Cell",21.5,3,9,8.5,'0.2','#8f9fb8','0.18')

# ---- va chạm: mọi ô không phải sàn (gộp hình chữ nhật)
solid=[[ (x,y) not in FLOOR for x in range(W)] for y in range(H)]
used=[[False]*W for _ in range(H)]
for y in range(H):
    for x in range(W):
        if not solid[y][x] or used[y][x]: continue
        w=1
        while x+w<W and solid[y][x+w] and not used[y][x+w]: w+=1
        h=1
        while y+h<H and all(solid[y+h][x+k] and not used[y+h][x+k] for k in range(w)): h+=1
        for yy in range(y,y+h):
            for xx in range(x,x+w): used[yy][xx]=True
        groups['blockers'].extend(obj('blocker','blocker',x*T,y*T,w*T,h*T,props=[('collider_kind','terrain')]))
for (x,y,w,h) in [(-T,-T,W*T+2*T,T),(-T,H*T,W*T+2*T,T),(-T,0,T,H*T),(W*T,0,T,H*T)]:
    groups['map bounds'].extend(obj('map_bound','bound',x,y,w,h,props=[('collider_kind','bound')]))

# ------------------------------------------------------------------ ghi file
import xml.etree.ElementTree as ET
m1=ET.parse(M1).getroot()
ts_lines=[' <tileset firstgid="%s" source="%s"/>'%(t.get('firstgid'),sx.escape(t.get('source'),{'"':'&quot;'})) for t in m1.findall('tileset')]
ts_lines.append(' <tileset firstgid="%d" source="../Tilesets/SR-mine.tsx"/>'%MINE_FIRST)
L=['<?xml version="1.0" encoding="UTF-8"?>']
lid=[0]
def layer(name,data,props=None):
    lid[0]+=1; out=[' <layer id="%d" name="%s" width="%d" height="%d">'%(lid[0],name,W,H)]
    if props: out+=['  <properties>']+['  '+p.strip() and '   '+p.strip() for p in props]+['  </properties>']
    out.append('  <data encoding="csv">')
    rows=[','.join(str(v) for v in data[y*W:(y+1)*W]) for y in range(H)]
    out.append(',\n'.join(rows)); out.append('</data>'); out.append(' </layer>'); return out
body=[]
body+=layer('ground',ground,[P('unity:SortingOrder',-20)])
body+=layer('seam floor',floor2)
body+=layer('ceiling',ceil)
body+=layer('ceiling corners',corners)
body+=layer('walls',walls)
body+=layer('veins',veins)
SORT={'tree shadows':-6,'ground decals':-5,'props':2,'props front':2,'ambient fx':10,'gates':2}
TRIG={'zones','triggers','mechanics'}
OPAC={'zones','triggers','obstacle colliders','water colliders','blockers','map bounds','mechanics'}
for name,objs in groups.items():
    lid[0]+=1
    body.append(' <objectgroup id="%d" name="%s"%s>'%(lid[0],name,' opacity="0.35"' if name in OPAC else ''))
    pp=[]
    if name in SORT: pp.append(P('unity:SortingOrder',SORT[name]))
    if name in TRIG: pp.append(P('unity:IsTrigger',True))
    if name=='water colliders': pp.append(P('unity:layer','Water'))
    if pp: body+=['  <properties>']+['   '+p.strip() for p in pp]+['  </properties>']
    body+=[' '+l for l in objs]
    body.append(' </objectgroup>')
L.append('<map version="1.10" tiledversion="1.12.2" orientation="orthogonal" renderorder="right-down" width="%d" height="%d" tilewidth="32" tileheight="32" infinite="0" nextlayerid="%d" nextobjectid="%d">'%(W,H,lid[0]+1,oid[0]+1))
L+=[' <properties>','  '+P('flag_group','Map1_GreystonePlains').strip(),' </properties>']
L+=ts_lines+body+['</map>']
open(OUT,'w',newline='\n').write('\n'.join(L)+'\n')
print('ok',OUT,W,H,'objects',oid[0])
