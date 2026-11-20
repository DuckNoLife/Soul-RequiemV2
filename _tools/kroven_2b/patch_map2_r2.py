# 17/11 (§286): dời lối xuống Kroven từ Forgotten Store sang Records Vault — cầu thang đá chìm xuống sàn, song sắt khoá vương miện (mở = song sắt biến mất).
# Kiểm md5 trước khi sửa; sao lưu ../../_backups/2026-11-17_entrance_r5/.
import sys, re
sys.path.insert(0,'../lore_1116')
from patch_lib import Doc, esc
M="../../Assets/GamePackage/EPIC RPG World - Sewers V1.5/TiledMap Editor/Maps/map2_undercity_of_vael.tmx"
d=Doc(M,'../../_backups/2026-11-17_entrance_r5')
assert d.md5_before=='759e91aeef17ff0c7d2da4b1675aef09', d.md5_before
nl='\r\n' if d.crlf else '\n'
def oid_of(name):
    m=re.findall(r'<object id="(\d+)" name="%s"'%re.escape(name),d.src); assert len(m)==1,(name,m); return m[0]
# 1) tileset mới: cầu thang xuống + song sắt (firstgid ngay sau tileset cuối 14642)
last=list(re.finditer(r' <tileset firstgid="(\d+)" source="[^"]+"/>',d.src))[-1]
assert last.group(1)=='14642', last.group(1)
d.src=d.src[:last.end()]+nl+' <tileset firstgid="14643" source="../tilesets/SR-stair-down-kroven.tsx"/>'+nl+' <tileset firstgid="14644" source="../tilesets/SR-stair-grate-kroven.tsx"/>'+d.src[last.end():]
# 2) bỏ nắp hầm sắt ở Forgotten Store
h=re.search(r'\s*<object id="\d+" name="kroven_hatch" gid="14633"[^>]*/>',d.src); assert h
d.src=d.src[:h.start()]+d.src[h.end():]; d.log.append('del kroven_hatch')
# 3) dời khoá + lối xuống tới Records Vault (trước miệng hầm, x17.1..18.7, y7.2..8.4)
for nm in ('crown_lock','to_kroven'):
    o=oid_of(nm); a,b=d._blk(o); blk=d.src[a:b]
    blk=re.sub(r' x="[^"]+" y="[^"]+" width="[^"]+" height="[^"]+"',' x="%g" y="%g" width="%g" height="%g"'%(17.1*32,7.2*32,1.6*32,1.2*32),blk,count=1)
    d.src=d.src[:a]+blk+d.src[b:]
o=oid_of('crown_lock')
d.set(o,'hint',"A stair cut down into the floor, older than the Undercity, barred by an iron grate. Its lock is ringed by three wheels of bronze crests around a small iron crown. Kroven work. Nothing here says which crests, or in what order.")
d.set(o,'text',"The three crests sink home. The grate shudders and slides back into the stone.")
o=oid_of('to_kroven')
d.set(o,'depart_text',"You go down the worn steps, into air that has not moved in a very long time.")
d.set(o,'arrive_subtitle',"Beneath the Records Vault")
# 4) ghi chép của Malric: hầm nằm dưới Records Vault
o=oid_of('kroven_notes')
d.sub(o,'text',"The floor of the Forgotten Store rings hollow. Under the rotten boards: an iron hatch, older than anything of ours.","In the corner of the Records Vault, under the cabinets we dragged aside: a stair cut down into the stone, barred with iron, older than anything of ours.")
d.sub(o,'text',"Around its crown, three wheels of crests.","Its lock is a crown with three wheels of crests.")
d.set(o,'key_desc',"Malric's hand: a barred stair in the Records Vault, three crests, and the order they stood in.")
d.set(o,'key_opens',"The Crown Lock - Records Vault")
# 5) hình: miệng hầm + song sắt (lớp ground decals: Ren đi đè lên mép được) và khối chặn lòng hầm
def add_tile(layer,name,gid,x,by,w,h):
    m=re.search(r'nextobjectid="(\d+)"',d.src); oid=int(m.group(1)); d.src=d.src[:m.start(1)]+str(oid+1)+d.src[m.end(1):]
    g=re.search(r'<objectgroup [^>]*name="%s"[^>]*>'%re.escape(layer),d.src); end=d.src.find('</objectgroup>',g.end())
    ins=d.src.rfind(nl,0,end)+len(nl)
    nm=' name="%s"'%name if name else ''
    d.src=d.src[:ins]+'  <object id="%d"%s gid="%d" x="%g" y="%g" width="%d" height="%d"/>%s'%(oid,nm,gid,x*32,by*32,w,h,nl)+d.src[ins:]
    d.log.append('tile %s #%d'%(name,oid))
add_tile('ground decals','kroven_stair',14643,16.9,8.0,64,64)
add_tile('ground decals','kroven_grate',14644,16.9,8.0,64,64)
oc=d.add('obstacle colliders','kroven_stair_hole','obstacle',17.05,6.05,1.7,1.45,{'collider_kind':'prop'})
# khối chặn không phải trigger
a,b=d._blk(oc); blk=re.sub(r'\s*<property name="unity:IsTrigger" type="bool" value="true"/>','',d.src[a:b]); d.src=d.src[:a]+blk+d.src[b:]
d.add('mechanics','kroven_grate_open','toggle',16.9,6.0,2,2,{'mechanic':'toggle','when':'crown_lock_open','hide_objects':'kroven_grate'})
print(d.err, d.log)
print('md5', d.save())
