# §360: Map 4 — thay câu đố cọc tập (Tally Room) bằng kho quân nhu đẩy thùng + nâng cấp Drill Yard (3 trạm).
# md5 kiểm tra cb823fab..., sao lưu _backups/2026-11-20_360/, giữ CRLF. Chạy từ gốc dự án.
import hashlib, os, re, shutil, sys
from PIL import Image
sys.path.insert(0, '_tools/m4drill_1120/s360')
from sok import solve
TM = 'Assets/GamePackage/EPIC RPG World Pack - Crypt V.1.6/TiledMap Editor/'
M4 = TM + 'Maps/map4_catacombs_of_naming.tmx'
TSX = TM + 'Tilesets/SR-drill-art.tsx'
PROPS = 'Assets/GamePackage/EPIC RPG World Pack - Crypt V.1.6/Props/'
RV = 'Assets/Resources/VFX/props/'
B = '_backups/2026-11-20_360/'
md5 = lambda p: hashlib.md5(open(p, 'rb').read()).hexdigest()
assert md5(M4) == 'cb823fab379fa6c117a46efcc1cade68', md5(M4)
os.makedirs(B, exist_ok=True)
shutil.copy2(M4, B); shutil.copy2(TSX, B)

# ---------------------------------------------------------------- màn Sokoban (kiểm có lời giải)
PEN_X, PEN_Y = 29, 133
LAYOUT = ["#########", "#__#____#", "-_$_____#", "#$__$_#_#", "#__##..##", "#_#_____-", "#______.#", "#########"]
W, H = len(LAYOUT[0]), len(LAYOUT)
grid = ['-' * (W + 2)] + ['-' + r.replace('_', ' ') + '-' for r in LAYOUT] + ['-' * (W + 2)]
grid[3] = '@' + grid[3][1:]
res = solve(grid)
assert res, 'màn không giải được!'
print('Sokoban: %d lượt đẩy, %d trạng thái' % (res[0], res[2]))

# ---------------------------------------------------------------- ảnh: thùng, chồng thùng, xem trước tấm ép / bệ đèn
c3 = Image.open(PROPS + 'SR-trimmed/crate 3.png').convert('RGBA'); c4 = Image.open(PROPS + 'SR-trimmed/crate 4.png').convert('RGBA')
c3.save(RV + 'drill_crate.png')
def stack(bot, top):
    im = Image.new('RGBA', (32, bot.height + 13)); im.alpha_composite(top, (0, 0)); im.alpha_composite(bot, (0, 13)); return im
stack(c4, c3).save(RV + 'drill_crate_stack.png'); stack(c3, c4).save(RV + 'drill_crate_stack2.png')
os.makedirs(PROPS + 'SR-drill-art', exist_ok=True)
Image.open('Assets/Resources/VFX/drill/fx_plate.png').crop((0, 0, 32, 32)).save(PROPS + 'SR-drill-art/drill_plate_prev.png')
ped = Image.open(RV + 'drill_keyped.png').convert('RGBA'); lan = Image.open(RV + 'drill_lantern.png').convert('RGBA')
comp = Image.new('RGBA', (24, 24 + lan.height)); comp.alpha_composite(ped, (0, comp.height - ped.height)); comp.alpha_composite(lan, ((24 - lan.width) // 2, 0))
comp.save(PROPS + 'SR-drill-art/drill_lantern_prev.png')

# ---------------------------------------------------------------- tileset: thêm ô 9..15
t = open(TSX, 'rb').read().decode('utf-8'); NLt = '\r\n' if '\r\n' in t else '\n'
assert 'tilecount="9"' in t
NEW = [(9, '../../../../Resources/VFX/props/drill_crate.png'), (10, '../../../../Resources/VFX/props/drill_crate_stack.png'),
       (11, '../../../../Resources/VFX/props/drill_crate_stack2.png'), (12, '../../Props/SR-drill-art/drill_plate_prev.png'),
       (13, '../../../../Resources/VFX/props/drill_launcher.png'), (14, '../../../../Resources/VFX/props/drill_rack.png'),
       (15, '../../Props/SR-drill-art/drill_lantern_prev.png')]
SIZE = {}
add = []
for tid, rel in NEW:
    w, h = Image.open(os.path.normpath(os.path.join(TM + 'Tilesets/', rel))).size; SIZE[tid] = (w, h)
    add += [' <tile id="%d">' % tid, '  <image source="%s" width="%d" height="%d"/>' % (rel, w, h), ' </tile>']
t = t.replace('tilecount="9"', 'tilecount="16"', 1).replace('</tileset>', NLt.join(add) + NLt + '</tileset>', 1)
open(TSX, 'wb').write(t.encode('utf-8'))
FIRST = 10552
G = lambda i: FIRST + i

# ---------------------------------------------------------------- tmx
s = open(M4, 'rb').read().decode('utf-8'); NL = '\r\n' if '\r\n' in s else '\n'
nid = int(re.search(r'nextobjectid="(\d+)"', s).group(1))
def remove(name):
    global s
    pat = re.compile(r'[ \t]*<object id="\d+" name="%s"[^>]*?(/>|>.*?</object>)\r?\n' % re.escape(name), re.S)
    s, n = pat.subn('', s, count=1)
    assert n == 1, name
for n in ['tally_post_%d' % i for i in range(1, 5)] + ['tally_post_%d_art' % i for i in range(1, 5)] + ['tally_ledger', 'tally_ledger_art']:
    remove(n)

def props(d):
    out = ['   <properties>']
    for k, v in d.items():
        typ = ' type="bool"' if isinstance(v, bool) else ' type="float"' if isinstance(v, float) else ' type="int"' if isinstance(v, int) else ''
        val = ('true' if v else 'false') if isinstance(v, bool) else str(v)
        val = val.replace('&', '&amp;').replace('"', '&quot;').replace('<', '&lt;').replace('>', '&gt;')
        out.append('    <property name="%s"%s value="%s"/>' % (k, typ, val))
    out.append('   </properties>')
    return out
def rect(name, typ, x, y, w, h, d):
    global nid
    o = ['  <object id="%d" name="%s" type="%s" x="%g" y="%g" width="%g" height="%g">' % (nid, name, typ, round(x * 32, 2), round(y * 32, 2), round(w * 32, 2), round(h * 32, 2))]
    nid += 1
    return NL.join(o + props(d) + ['  </object>'])
def tobj(name, gid, left_px, bottom_px, w, h, d=None):
    global nid
    head = '  <object id="%d" name="%s" gid="%d" x="%g" y="%g" width="%d" height="%d"' % (nid, name, gid, round(left_px, 2), round(bottom_px, 2), w, h)
    nid += 1
    if not d: return head + '/>'
    return NL.join([head + '>'] + props(d) + ['  </object>'])
def add_to_group(layer, objs):
    global s
    m = re.search(r'<objectgroup id="\d+" name="%s"[^>]*>' % re.escape(layer), s); assert m, layer
    e = s.index('</objectgroup>', m.end())
    s = s[:e] + NL.join(objs) + NL + ' ' + s[e:].lstrip(' ')

# drill_yard: bỏ target, thêm vị trí đèn / giá / máy phóng
m = re.search(r'(<object id="\d+" name="drill_yard" type="drill_yard".*?<properties>)(.*?)(</properties>)', s, re.S); assert m
body = re.sub(r'[ \t]*<property name="target"[^>]*/>\r?\n', '', m.group(2))
extra = ''.join('    <property name="%s" value="%s"/>%s' % (k, v, NL) for k, v in
                [('lantern_at', '17.5,141.8'), ('rack_at', '6.5,122.75'), ('launcher_1', '3.2,128.9,1,drill_launch_w'), ('launcher_2', '21.8,135.9,-1,drill_launch_e')])
extra += '    <property name="morale" type="int" value="5"/>' + NL
body = body.rstrip(' ') + extra + '   '
s = s[:m.start(2)] + body + s[m.end(2):]

TRIG = {'unity:IsTrigger': True}
mech = [
    rect('store_crates', 'crate_puzzle', PEN_X, PEN_Y, W, H, dict(layout='/'.join(LAYOUT), sets='tally_done', reset='store_reset', mechanic='crate_puzzle',
         hint="The warders' supply pen. Push all three crates onto the pressure plates. Walk into a crate to shove it - you can't pull. Stuck? Pull the reset winch.",
         text="The last plate sinks with a heavy clunk. Somewhere west, a bolt slides back. From beyond the door: 'FINALLY! Get IN here, recruit!'", **TRIG)),
    rect('store_reset_lever', 'lever', 27.0, 134.0, 1, 1, dict(mechanic='lever', sprite='trap_gear', fires='store_reset', rearm=1.5, range=0.9, verb='Reset',
         hint='A winch. Its chains run to every crate in the pen.', text='Chains rattle overhead. The crates are hauled back to their places.', **TRIG)),
    rect('drill_lever_w', 'lever', 2.6, 130.0, 1, 1, dict(mechanic='lever', sprite='trap_gear', fires='drill_launch_w', rearm=2.2, range=0.9, verb='Pull',
         hint='A launcher lever. Pull it to hurl a hay bale down the lane.', text='CLUNK! The launcher hurls a hay bale.', **TRIG)),
    rect('drill_lever_e', 'lever', 20.0, 133.2, 1, 1, dict(mechanic='lever', sprite='trap_gear', fires='drill_launch_e', rearm=2.2, range=0.9, verb='Pull',
         hint='A launcher lever. Pull it to hurl a hay bale down the lane.', text='CLUNK! The launcher hurls a hay bale.', **TRIG)),
]
add_to_group('mechanics', mech)

art = []
for y, row in enumerate(LAYOUT):
    for x, c in enumerate(row):
        tx, ty = PEN_X + x, PEN_Y + y
        if c == '#':
            tid = 11 if (x * 7 + y * 3) % 3 == 0 else 10
            w, h = SIZE[tid]; art.append(tobj('store_shelf_art', G(tid), tx * 32 + (32 - w) / 2, (ty + 1) * 32, w, h, {'art_for': 'store_crates'}))
        if c in '.*':
            art.append(tobj('store_plate_art', G(12), tx * 32, (ty + 1) * 32, 32, 32, {'art_for': 'store_crates'}))
        if c in '$*':
            w, h = SIZE[9]; art.append(tobj('store_crate_art', G(9), tx * 32 + (32 - w) / 2, (ty + 1) * 32, w, h, {'art_for': 'store_crates'}))
w, h = SIZE[13]
art.append(tobj('drill_launcher_w_art', G(13) | 0x80000000, 3.2 * 32 - w / 2, 128.9 * 32, w, h, {'art_for': 'drill_yard'}))
art.append(tobj('drill_launcher_e_art', G(13), 21.8 * 32 - w / 2, 135.9 * 32, w, h, {'art_for': 'drill_yard'}))
w, h = SIZE[14]; art.append(tobj('drill_rack_art', G(14), 6.5 * 32 - w / 2, 122.75 * 32, w, h, {'art_for': 'drill_yard'}))
w, h = SIZE[15]; art.append(tobj('drill_lantern_art', G(15), 17.5 * 32 - w / 2, 141.8 * 32, w, h, {'art_for': 'drill_yard'}))
add_to_group('mechanics_art', art)

# tên khu / điểm nghỉ
for old, new in [('<property name="title" value="The Tally Room"/>', '<property name="title" value="The Quartermaster\'s Store"/>'),
                 ('<property name="stage_name" value="The Tally Room"/>', '<property name="stage_name" value="The Quartermaster\'s Store"/>')]:
    assert s.count(old) == 1, old
    s = s.replace(old, new)
s = re.sub(r'nextobjectid="\d+"', 'nextobjectid="%d"' % nid, s, count=1)
open(M4, 'wb').write(s.encode('utf-8'))
print('xong md5', md5(M4), 'nextobjectid', nid)
