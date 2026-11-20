# 18/11 15h (§325): cầu Blade Yard chỉ hạ khi quay ĐỦ 2 tời — tời tây (bridge_winch, người dùng đã dời tới 40.46,113.69) + tời MỚI ở góc đông bắc
# (ngõ cụt F0, trước đây bỏ trống). Mỗi tời đặt cờ riêng; cảnh `bridge_lowered` (trigger flag, khi đủ 2 cờ) lia máy quay tới cầu và đặt pit_bridge_down.
# Bẫy giữ góc đông bắc: cưa ngang chắn khe F1->F0 (lửa tường đông mz_F1_fire đã quét hàng 1). Không động tới vị trí người dùng đã đặt.
import hashlib, re, os, shutil, sys
ROOT = os.path.expanduser('~/mnt/Soul Requiem')
TMX = ROOT + "/Assets/GamePackage/EPIC RPG World Pack - Crypt V.1.6/TiledMap Editor/Maps/map4_catacombs_of_naming.tmx"
b = open(TMX, 'rb').read(); md5 = hashlib.md5(b).hexdigest()
assert md5 == sys.argv[1], md5
BK = ROOT + "/_backups/2026-11-18_m4_twowinch/"; os.makedirs(BK, exist_ok=True); shutil.copy2(TMX, BK)
s = b.decode('utf-8'); crlf = '\r\n' in s; s = s.replace('\r\n', '\n')
T = 32
def setprop(oid, name, value):
    global s
    m = re.search(r'<object id="%d" [^>]*>\n   <properties>\n(.*?)   </properties>' % oid, s, re.S); assert m, oid
    body = m.group(1)
    pat = r'    <property name="%s"( type="\w+")? value="[^"]*"/>\n' % re.escape(name)
    new = '    <property name="%s" value="%s"/>\n' % (name, value)
    body2, k = re.subn(pat, new, body)
    if k == 0: body2 = body + new
    s = s[:m.start(1)] + body2 + s[m.end(1):]
# 1) tời tây: cờ riêng + lời mới
setprop(915, 'sets', 'bridge_winch_w')
setprop(915, 'text', 'The chain draws taut and locks. The bridge does not move - a second chain still holds it from across the yard.')
# 2) tời đông bắc + cưa ngang + cảnh hạ cầu
nid = int(re.search(r'nextobjectid="(\d+)"', s).group(1))
P = lambda n, v, t=None: '    <property name="%s"%s value="%s"/>\n' % (n, ' type="%s"' % t if t else '', v)
lever = ('  <object id="%d" name="bridge_winch_e" type="lever" x="%g" y="%g" width="32" height="32">\n   <properties>\n' % (nid, 63.2 * T, 108.0 * T) +
         P('hint', 'A second winch, its chain running west toward the pit.') + P('mechanic', 'lever') + P('range', '0.9') +
         P('sets', 'bridge_winch_e') + P('sprite', 'trap_gear') +
         P('text', 'The chain draws taut and locks. Somewhere to the west, the other winch still waits.') +
         P('unity:IsTrigger', 'true', 'bool') + P('verb', 'Turn') + '   </properties>\n  </object>\n'); nid += 1
saw = ('  <object id="%d" name="mz_F0_saw" type="blade" x="%g" y="%g" width="%g" height="32">\n   <properties>\n' % (nid, 59.5 * T, 111.0 * T, 5.5 * T) +
       P('damage', '16') + P('kind', 'saw') + P('knock', '3') + P('mechanic', 'blade') + P('offset', '0.6') + P('pause', '0.3') + P('speed', '3.0') +
       P('unity:IsTrigger', 'true', 'bool') + '   </properties>\n  </object>\n'); nid += 1
i = s.index('<object id="915" '); j = s.index('\n  </object>', i) + len('\n  </object>\n')
s = s[:j] + lever + saw + s[j:]
scene = ('  <object id="%d" name="bridge_lowered" type="story" x="%g" y="%g" width="32" height="32">\n   <properties>\n' % (nid, 63.2 * T, 108.0 * T) +
         P('script', 'wait 0.6\\ncam 55,104.6 1.0\\nwait 0.5\\nset pit_bridge_down\\nshake 0.02 1.4\\nwait 1.8\\ncamren\\nhint Both chains hold. The bridge has come down over the pit.') +
         P('trigger', 'flag') + P('unity:IsTrigger', 'true', 'bool') + P('when', 'bridge_winch_w,bridge_winch_e') + '   </properties>\n  </object>\n'); nid += 1
m = re.search(r'<objectgroup id="\d+" name="triggers"[^>]*>\n', s); e = s.index(' </objectgroup>', m.end()); s = s[:e] + scene + s[e:]
s = re.sub(r'nextobjectid="\d+"', 'nextobjectid="%d"' % nid, s, count=1)
if crlf: s = s.replace('\n', '\r\n')
open(TMX, 'wb').write(s.encode('utf-8'))
print('md5', hashlib.md5(s.encode('utf-8')).hexdigest(), 'nextobjectid', nid)
