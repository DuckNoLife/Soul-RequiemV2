# 18/11 (§327): cảnh chuyển Map 3 -> Map 4 — kẻ trùm mũ (Absalom, chưa nêu tên) đánh lén Ren trước Requiem Door.
# Chạy: cd _tools/cut_1118 && python3 patch_m3m4.py  (kiểm md5 đầu vào, sao lưu, giữ CRLF)
import sys, re; sys.path.insert(0,'../lore_1116')
from patch_lib import Doc, esc
BK='../../_backups/2026-11-18_cut_m3m4'
GP='../../Assets/GamePackage/'
M3=GP+'EPIC RPG World Pack - Cemetery V 1.6/TiledMap Editor/Maps/map3_vale_of_graves.tmx'
M4=GP+'EPIC RPG World Pack - Crypt V.1.6/TiledMap Editor/Maps/map4_catacombs_of_naming.tmx'
M3_MD5='d77ea43961efb7141630e1ea603bb11a'
M4_MD5='4427905492be5883a66df9d65980179e'

def typed(d, oid, name, typ, value):
    """Đặt thuộc tính có kiểu (bool/float) — Doc.set chỉ ghi chuỗi."""
    a,b=d._blk(oid); blk=d.src[a:b]
    m=re.search(r'<property name="%s"[^/]*/>'%re.escape(name),blk)
    line='<property name="%s" type="%s" value="%s"/>'%(name,typ,value)
    if m: blk=blk[:m.start()]+line+blk[m.end():]
    else:
        i=blk.rfind('</properties>'); nl='\r\n' if d.crlf else '\n'
        blk=blk[:i]+line+nl+'   '+blk[i:]
    d.src=d.src[:a]+blk+d.src[b:]

# ---------------------------------------------------------------- Map 3
MORROW_DOOR = '\\n'.join([
 'cam 67.9,117 1',
 'wait 1.2',
 'shake 0.05 1.2',
 'glow 67.9,117.4 #e05a64 3 5',
 'narrate With his last breath, Morrow strikes his bell once more. The Requiem Door tears open.|Red light pours out of it, and the Eye inside you answers.',
 'fade out 0.6',
 'ren warp 67.9,120.3',
 'face 67.9,117.4',
 'set hooded_watch',
 'cam 66.2,119.6 1',
 'fade in 1.0',
 'ren draw',
 'wait 0.8',
 "bubble ren: It's over. The bells are quiet.",
 'ren sheathe',
 'wait 1.4',
 'walk hooded_one 66.9,120.4',
 'wait 0.3',
 'bubble ren: ...Footsteps?',
 'face 66.9,120.4',
 'play hooded_one strike nowait fps=11',
 'wait 0.36',
 'shake 0.09 0.3',
 'ren fall',
 'wait 0.3',
 'fade out 0.15',
 'wait 1.0',
 'say The hooded one: Gently. He is not to be broken.|Not yet.',
 'set ren_taken',
 'slide Story/m3_carried_down: They carry you down, past the last of the graves, into the dark beneath the Vale.',
 'exit to_map4',
])

d=Doc(M3,BK)
assert d.md5_before==M3_MD5, ('Map 3 md5 khác -> dừng, đọc lại', d.md5_before)
d.set(5473,'script',MORROW_DOOR)
d.set(4521,'depart_text','Down. Then further down.')
typed(d,4521,'quake','bool','false')
assert 'name="hooded_one"' not in d.src
# NPC kẻ trùm mũ: đứng trong bóng tối cạnh cột đèn tây (chân ở 61.9,120.4), hiện khi cảnh bật cờ hooded_watch
m=re.search(r'nextobjectid="(\d+)"',d.src); oid=int(m.group(1))
d.src=d.src[:m.start(1)]+str(oid+1)+d.src[m.end(1):]
nl='\r\n'
blk=nl.join([
 '  <object id="%d" name="hooded_one" type="villager" x="%g" y="%g" width="32" height="32">'%(oid,61.4*32,119.4*32),
 '   <properties>',
 '    <property name="face" value="E"/>',
 '    <property name="hide_when" value="ren_taken"/>',
 '    <property name="kind" value="hooded_one"/>',
 '    <property name="scale" type="float" value="0.66"/>',
 '    <property name="show_when" value="hooded_watch"/>',
 '    <property name="speaker" value="The hooded one"/>',
 '    <property name="speed" type="float" value="0.5"/>',
 '    <property name="unity:IsTrigger" type="bool" value="true"/>',
 '    <property name="walk" type="bool" value="false"/>',
 '   </properties>',
 '  </object>'])+nl+' '
g=re.search(r'<objectgroup [^>]*name="npcs"[^>]*>',d.src); end=d.src.find('</objectgroup>',g.end())
assert d.src[end-3:end]=='\r\n ', repr(d.src[end-5:end])
d.src=d.src[:end-1]+blk[:-1]+d.src[end-1:]
print('M3 err',d.err,'npc id',oid)
assert not d.err
print('M3 md5',d.save())

# ---------------------------------------------------------------- Map 4
d=Doc(M4,BK)
assert d.md5_before==M4_MD5, ('Map 4 md5 khác -> dừng, đọc lại', d.md5_before)
d.sub(967,'script','fade out 0.01\\nwait 0.5\\n','fade out 0.01\\nren fall\\nwait 0.5\\n')
d.sub(967,'script','narrate The blow from behind. Then the sway of being carried - down, and down.|A hand on the back of your neck.','narrate A hand on the back of your neck.')
d.sub(967,'script','fade in 2.0\\nwait 0.8\\n','fade in 2.0\\nwait 0.6\\nren getup\\nwait 1.4\\n')
print('M4 err',d.err); assert not d.err
print('M4 md5',d.save())

# ---- bổ sung (cùng §327): chờ MapRestraint đổi Animator sang áo tế (Update đầu tiên) rồi mới cho Ren nằm, tránh đổi controller làm mất Dead
def m4_wait_robe():
    d=Doc(M4,BK+'_b')
    assert d.md5_before=='97055e9cc641d69852a88f7aae4ce86a', d.md5_before
    d.sub(967,'script','fade out 0.01\\nren fall\\n','fade out 0.01\\nwait 0.3\\nren fall\\n')
    assert not d.err, d.err
    print('M4 md5 (b)', d.save())
if __name__=='__main__' and len(sys.argv)>1 and sys.argv[1]=='b': pass
