# 20/11 (§349): Warden Gallery -> "THE RELIQUARY LISTS": san phẳng bệ pháo (bỏ ô plat/rails + khối chắn bệ), bỏ tượng / thùng / mốc bệ,
# Engine đứng GIỮA phòng (mốc duo_core), 4 giá vũ khí relic_rack ở 4 góc (minigame đua giành vũ khí). Kiểm md5 (tham số 1), sao lưu, giữ CRLF.
import re, sys, hashlib, shutil, os
R = os.path.expanduser('~/mnt/Soul Requiem/')
M4 = R + 'Assets/GamePackage/EPIC RPG World Pack - Crypt V.1.6/TiledMap Editor/Maps/map4_catacombs_of_naming.tmx'
OUT = sys.argv[2] if len(sys.argv) > 2 else M4
md5 = lambda p: hashlib.md5(open(p, 'rb').read()).hexdigest()
if md5(M4) != sys.argv[1]: sys.exit('DỪNG: md5 ' + md5(M4))
if OUT == M4: shutil.copyfile(M4, R + '_backups/2026-11-19_m4gallery/map4_before_lists.tmx')
s = open(M4, 'rb').read().decode('utf-8'); NL = '\r\n' if '\r\n' in s else '\n'
W, H, T = 114, 148, 32

def layer(name):
    m = re.search(r'(<layer id="\d+" name="%s"[^>]*>.*?<data encoding="csv">)(.*?)(</data>)' % name, s, re.S)
    return m, [int(v) for v in m.group(2).replace('\r', '').replace('\n', '').split(',') if v.strip()]
def put_layer(name, vals):
    global s
    m, _ = layer(name)
    rows = [','.join(str(v) for v in vals[y * W:(y + 1) * W]) for y in range(H)]
    s = s[:m.start(2)] + NL + (',' + NL).join(rows) + NL + s[m.end(2):]
_, plat = layer('plat'); _, rails = layer('rails')
for y in range(91, 104):
    for x in range(95, 111): plat[y * W + x] = 0
for y in range(96, 101):
    for x in range(95, 108): rails[y * W + x] = 0
put_layer('plat', plat); put_layer('rails', rails)

def obj_blocks():
    return list(re.finditer(r'<object id="\d+"[^>]*?/>|<object id="\d+"[^>]*[^/]>.*?</object>', s, re.S))
def remove_where(pred, label):
    global s
    n = 0
    for m in reversed(obj_blocks()):
        b = m.group(0)
        if pred(b):
            a, e = m.start(), m.end()
            while a > 0 and s[a - 1] in ' ': a -= 1
            if s[a - len(NL):a] == NL: a -= len(NL)
            s = s[:a] + s[e:]; n += 1
    print('bỏ', n, label)
# khối chắn bệ pháo (px)
dais = {(95 * T, 96 * T, 1 * T, 7 * T), (96 * T, 99 * T, 3 * T, 4 * T), (99 * T, 99 * T, 5 * T, 4 * T), (104 * T, 99 * T, 4 * T, 4 * T)}
def is_dais(b):
    m = re.search(r'name="blocker" type="blocker" x="([\d.]+)" y="([\d.]+)" width="([\d.]+)" height="([\d.]+)"', b)
    return bool(m) and tuple(float(v) for v in m.groups()) in {tuple(float(v) for v in d) for d in dais}
remove_where(is_dais, 'khối chắn bệ')
remove_where(lambda b: re.search(r'name="(warden_statue_\d|powder_keg_\d|duo_gallery|duo_landing)"', b) is not None, 'tượng / thùng / mốc bệ')
# mốc sân: khung sàn mới (cả góc đông bắc)
m = re.search(r'<object id="\d+" name="duo_arena"[^>]*>', s)
s = s.replace(m.group(0), re.sub(r'x="[\d.]+" y="[\d.]+" width="[\d.]+" height="[\d.]+"', 'x="%g" y="%g" width="%g" height="%g"' % (78 * T, 96.3 * T, 29.7 * T, 12.4 * T), m.group(0)), 1)
# spawn: Engine giữa phòng, Champion phía tây
for name, (x, y) in (('Brazen_Reliquary_15', (92.5, 103.0)), ('Hollow_Champion_16', (86.0, 102.6))):
    m = re.search(r'<object id="\d+" name="%s"[^>]*>' % name, s)
    s = s.replace(m.group(0), re.sub(r'x="[\d.]+" y="[\d.]+"', 'x="%g" y="%g"' % (x * T, y * T), m.group(0)), 1)
# cơ quan mới
nid = int(re.search(r'nextobjectid="(\d+)"', s).group(1))
def rect(name, typ, x, y, w, h, props):
    global nid
    p = ''.join('    <property name="%s"%s value="%s"/>%s' % (k, ' type="bool"' if isinstance(v, bool) else '', ('true' if v else 'false') if isinstance(v, bool) else v, NL) for k, v in props)
    o = '<object id="%d" name="%s" type="%s" x="%g" y="%g" width="%g" height="%g">%s   <properties>%s%s   </properties>%s  </object>' % (
        nid, name, typ, x * T, y * T, w * T, h * T, NL, NL, p, NL)
    nid += 1
    return o
new = [rect('duo_core', 'ritual_pillar', 92.0, 102.2, 1.0, 1.0, [('unity:IsTrigger', True)])]
for w, (cx, cy) in (('halberd', (81.0, 97.5)), ('hammer', (104.0, 97.5)), ('flail', (81.0, 108.7)), ('axes', (104.0, 108.7))):
    new.append(rect('relic_rack_' + w, 'relic_rack', cx - 0.7, cy - 0.55, 1.4, 0.55, [('unity:IsTrigger', True), ('weapon', w)]))
mm = re.search(r'(<objectgroup id="35" name="mechanics"[^>]*>.*?)(\s*</objectgroup>)', s, re.S)
s = s[:mm.end(1)] + ''.join(NL + '  ' + o for o in new) + s[mm.end(1):]
s = re.sub(r'nextobjectid="\d+"', 'nextobjectid="%d"' % nid, s, count=1)
open(OUT, 'wb').write(s.encode('utf-8'))
print('xong ->', OUT, md5(OUT))
