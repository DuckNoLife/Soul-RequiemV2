# Map 2: nắp hầm Kroven ở Forgotten Store (khoá vương miện 3 huy hiệu) + ghi chép của Malric ở Archive. Kiểm md5 trước khi sửa.
import sys, re
sys.path.insert(0,'../lore_1116')
from patch_lib import Doc, esc
M="../../Assets/GamePackage/EPIC RPG World - Sewers V1.5/TiledMap Editor/Maps/map2_undercity_of_vael.tmx"
d=Doc(M,'../../_backups/2026-11-16_kroven_2b')
assert d.md5_before=='aa7ee85d5225a8bf01e4576f3c4e2c30', d.md5_before
def add_tile(layer,name,gid,cx,by,w,h):
    m=re.search(r'nextobjectid="(\d+)"',d.src); oid=int(m.group(1))
    d.src=d.src[:m.start(1)]+str(oid+1)+d.src[m.end(1):]
    g=re.search(r'<objectgroup [^>]*name="%s"[^>]*>'%re.escape(layer),d.src); end=d.src.find('</objectgroup>',g.end())
    nl='\r\n' if d.crlf else '\n'; ins=d.src.rfind(nl,0,end)+len(nl)
    d.src=d.src[:ins]+'  <object id="%d" name="%s" gid="%d" x="%g" y="%g" width="%d" height="%d"/>%s'%(oid,name,gid,cx*32-w/2,by*32,w,h,nl)+d.src[ins:]
    d.log.append('tile %s #%d'%(name,oid))
# ---- Forgotten Store: nắp hầm sắt + khoá + lối xuống
add_tile('props','kroven_hatch',14633,25.0,37.6,48,47)        # lớp props (tile thường) — ở mechanics_art tile này là 'passage'
d.add('mechanics','crown_lock','cipher',24.2,36.1,1.6,1.5,{
 'mechanic':'cipher','answer':'0,1,2','perm':'3,4,0,2,1','glyph_res':'UI/Kroven/crest_',
 'title':'The Crown Lock',
 'hint':"An iron hatch older than the Undercity, ringed by three wheels of bronze crests around a small iron crown. Kroven work. Nothing here says which crests, or in what order.",
 'sets':'crown_lock_open',
 'text':"The three crests sink home. Under your feet, a bolt older than Vael draws back."})
d.add('triggers','to_kroven','exit',24.2,36.1,1.6,1.5,{
 'arrive_at':'6.5,33.6','arrive_title':'The Kroven Deep','arrive_subtitle':'Beneath the Forgotten Store',
 'depart_text':'You lift the iron hatch. A ladder goes down into air that has not moved in a very long time.',
 'interact':True,'quake':False,'range':'0.8','requires':'crown_lock_open','target':'map2b_kroven_deep','verb':'Climb down'})
# ---- Malric's Archive: trang ghi chép (mang theo trong túi đồ)
NOTE={
 'mechanic':'note','pickup':True,'key_id':'kroven_notes','key_kind':'Document','key_name':"Malric's Notes on the Old Cistern",
 'key_icon':'item_note','key_desc':"Malric's hand: a hatch under the Forgotten Store, three crests, and the order they stood in.",
 'key_opens':'The Crown Lock - Forgotten Store','verb':'Read','title':'Notes on the Old Cistern',
 'text':"The floor of the Forgotten Store rings hollow. Under the rotten boards: an iron hatch, older than anything of ours. Older than Vael. Kroven work - the northmen who were here before the city.\\n\\nAround its crown, three wheels of crests. Above our own cistern stair the same crests are cut in the gate-stone, in the order the old houses stood at their king's gate: the Anvil, the Tower, the Ram.\\n\\nI have not turned the wheels. Whatever Kroven locked away down there, it locked away from a king. I have enough kings of my own.\\n\\n- M."}
m=re.search(r'nextobjectid="(\d+)"',d.src); oid=int(m.group(1)); d.src=d.src[:m.start(1)]+str(oid+1)+d.src[m.end(1):]
g=re.search(r'<objectgroup [^>]*name="mechanics_art"[^>]*>',d.src); end=d.src.find('</objectgroup>',g.end())
nl='\r\n' if d.crlf else '\n'; ins=d.src.rfind(nl,0,end)+len(nl)
lines=['  <object id="%d" name="kroven_notes" type="note" gid="14631" x="%g" y="%g" width="12" height="16">'%(oid,19.6*32-6,22.55*32),'   <properties>']
for k in sorted(NOTE):
    v=NOTE[k]
    lines.append('    <property name="%s"%s value="%s"/>'%(k,' type="bool"' if isinstance(v,bool) else '',('true' if v else 'false') if isinstance(v,bool) else esc(v)))
lines.append('    <property name="range" type="float" value="1.2"/>')
lines+=['   </properties>','  </object>']
d.src=d.src[:ins]+nl.join(lines)+nl+d.src[ins:]; d.log.append('note kroven_notes #%d'%oid)
# kiểu float / bool cho lối xuống
a2,b2=d._blk(int(re.search(r'<object id="(\d+)" name="to_kroven"',d.src).group(1)))
blk=d.src[a2:b2].replace('<property name="range" value="0.8"/>','<property name="range" type="float" value="0.8"/>').replace('<property name="quake" value="False"/>','<property name="quake" type="bool" value="false"/>')
d.src=d.src[:a2]+blk+d.src[b2:]
print(d.err, d.log)
print('md5', d.save())
