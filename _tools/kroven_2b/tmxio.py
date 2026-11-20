# Ghi .tmx cho Map 2b: dùng đúng danh sách tileset của Map 2 (gid trùng), thêm tileset riêng nếu cần.
import xml.sax.saxutils as sx
import xml.etree.ElementTree as ET
MAPS="../../Assets/GamePackage/EPIC RPG World - Sewers V1.5/TiledMap Editor/Maps/"
M2=MAPS+"map2_undercity_of_vael.tmx"; OUT=MAPS+"map2b_kroven_deep.tmx"
def tileset_lines(extra=()):
    r=ET.parse(M2).getroot()
    out=[' <tileset firstgid="%s" source="%s"/>'%(t.get('firstgid'),sx.escape(t.get('source'),{'"':'&quot;'})) for t in r.findall('tileset')]
    out+=[' <tileset firstgid="%d" source="%s"/>'%(f,s) for f,s in extra]
    return out
def P(k,v,typ=None):
    t=' type="%s"'%typ if typ else ''
    if typ is None and isinstance(v,bool): t=' type="bool"'; v='true' if v else 'false'
    elif typ is None and isinstance(v,int): t=' type="int"'
    elif typ is None and isinstance(v,float): t=' type="float"'
    v=str(v).replace('\n','\\n')
    return '    <property name="%s"%s value="%s"/>'%(k,t,sx.escape(v,{'"':'&quot;'}))
# 17/11 (sau §294): chỉnh tay của người dùng trong Tiled (dời đuốc vách) — áp lại mỗi lần sinh map để không mất.
# khoá = (tên object hoặc gid, x, y do script sinh) -> (x, y) người dùng đặt.
USER_POS = {
    ('13959', 720.0, 544.0): (722, 523),     ('torch_235_170', 736.0, 540.8): (739, 517.8),
    ('13959', 1235.2, 544.0): (1220.2, 517), ('torch_396_170', 1251.2, 540.8): (1236.2, 521.8),
    ('13959', 1168.0, 160.0): (1105, 140),   ('torch_375_50', 1184.0, 156.8): (1120, 142.8),
    ('13959', 1312.0, 160.0): (1307, 143),   ('torch_420_50', 1328.0, 156.8): (1325, 137.8),
    ('13959', 336.0, 83.2): (335, 77.2),     ('torch_115_26', 352.0, 80.0): (351, 65),
    ('13959', 720.0, 83.2): (720, 73.2),     ('torch_235_26', 736.0, 80.0): (737, 63),
}
USER_HIT = set()
class Objs:
    def __init__(s): s.n=0
    def obj(s,name=None,typ=None,x=0,y=0,w=None,h=None,gid=None,props=None,shape=None):
        k=(name if name else str(gid), round(x,2), round(y,2))
        if k in USER_POS: USER_HIT.add(k); x,y=USER_POS[k]
        s.n+=1; a=' <object id="%d"'%s.n
        if name: a+=' name="%s"'%sx.escape(name,{'"':'&quot;'})
        if typ: a+=' type="%s"'%typ
        if gid: a+=' gid="%d"'%gid
        a+=' x="%s" y="%s"'%(round(x,2),round(y,2))
        if w is not None: a+=' width="%s" height="%s"'%(round(w,2),round(h,2))
        body=[]
        if props:
            body.append('   <properties>')
            for k,v in sorted(props,key=lambda p:p[0]):
                body.append('  '+(P(k,v[0],v[1]) if isinstance(v,tuple) else P(k,v)))
            body.append('   </properties>')
        if shape=='point': body.append('   <point/>')
        if shape=='ellipse': body.append('   <ellipse/>')
        if not body: return [a+'/>']
        return [a+'>']+body+[' </object>']
def write(path,W,H,layers,groups,objs,map_props,extra_ts=()):
    body=[]; lid=0
    for L in layers:
        name,data,props=L[:3]; tint=L[3] if len(L)>3 else None      # 17/11: lớp nước có tintcolor như Map 2
        lid+=1; body.append(' <layer id="%d" name="%s" width="%d" height="%d"%s>'%(lid,name,W,H,' tintcolor="%s"'%tint if tint else ''))
        if props: body+=['  <properties>']+['   '+p.strip() for p in props]+['  </properties>']
        body.append('  <data encoding="csv">')
        body.append(',\n'.join(','.join(str(v) for v in data[y*W:(y+1)*W]) for y in range(H)))
        body+=['</data>',' </layer>']
    SORT={'ground decals':-5,'props':2,'props front':2,'ambient fx':10,'gates':2,'mechanics_art':2,'wall props':1}
    TRIG={'zones','triggers','mechanics'}
    OPAC={'zones','triggers','obstacle colliders','water colliders','blockers','map bounds','mechanics'}
    for name,lst in groups.items():
        lid+=1; body.append(' <objectgroup id="%d" name="%s"%s>'%(lid,name,' opacity="0.35"' if name in OPAC else ''))
        pp=[]
        if name in SORT: pp.append(P('unity:SortingOrder',SORT[name]))
        if name in TRIG: pp.append(P('unity:IsTrigger',True))
        if pp: body+=['  <properties>']+['   '+p.strip() for p in pp]+['  </properties>']
        body+=[' '+l for l in lst]; body.append(' </objectgroup>')
    L=['<?xml version="1.0" encoding="UTF-8"?>',
       '<map version="1.10" tiledversion="1.12.2" orientation="orthogonal" renderorder="right-down" width="%d" height="%d" tilewidth="32" tileheight="32" infinite="0" nextlayerid="%d" nextobjectid="%d">'%(W,H,lid+1,objs.n+1)]
    if map_props: L+=[' <properties>']+['  '+P(k,v).strip() for k,v in map_props]+[' </properties>']
    L+=tileset_lines(extra_ts)+body+['</map>']
    open(path,'w',newline='\n').write('\n'.join(L)+'\n')
    miss=set(USER_POS)-USER_HIT
    if miss: print('CHU Y: chinh tay khong khop (object da doi?):', sorted(miss))
