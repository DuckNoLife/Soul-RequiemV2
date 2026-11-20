# 18/11 (kế hoạch §9): Penitent's Walk quá ngắn -> CHÈN 24 hàng ngay dưới cửa Hall of Naming (y = 26) làm đoạn bẫy mới 3 phần.
# Mọi thứ từ hàng 26 trở xuống dời xuống 24 ô (ô, vật, toạ độ trong kịch bản / thuộc tính, mục tiêu nhiệm vụ). Hall of Naming + cửa hall_door + vạch đích walk_top giữ nguyên.
# Điểm nghỉ rest_walk (bãi trên cũ) thành điểm giữa đường. Kiểm md5, sao lưu.
import hashlib, re, os, shutil, sys, json
ROOT = os.path.expanduser(os.environ.get('M4ROOT', '~/mnt/Soul Requiem'))
TMX = ROOT + "/Assets/GamePackage/EPIC RPG World Pack - Crypt V.1.6/TiledMap Editor/Maps/map4_catacombs_of_naming.tmx"
QJ = ROOT + "/Assets/Resources/Quests/Map4_CatacombsOfNaming.json"
b = open(TMX, 'rb').read(); assert hashlib.md5(b).hexdigest() == sys.argv[1], hashlib.md5(b).hexdigest()
BK = ROOT + "/_backups/2026-11-18_m4_walk/"; os.makedirs(BK, exist_ok=True); shutil.copy2(TMX, BK); shutil.copy2(QJ, BK)
s = b.decode('utf-8'); crlf = '\r\n' in s
if crlf: s = s.replace('\r\n', '\n')
T = 32; Y0 = 26; Y1 = 22; N = 24; MH = 124; MW = 114; CX0, CX1 = 46, 65   # cột hành lang Penitent's Walk
KEEP = {'walk_top'}
# ---- ô: chèn N hàng (chép luân phiên hàng 29 / 30 — sàn bãi trên, có tường hai bên)
def fix_layer(m):
    head, body, tail = m.group(1), m.group(2), m.group(3)
    rows = body.split('\n'); assert len(rows) == MH
    norm = [r.rstrip(',') for r in rows]
    cells = [r.split(',') for r in norm]
    out = [[None] * MW for _ in range(MH + N)]
    for x in range(MW):
        ins = Y0 if CX0 <= x <= CX1 else Y1                           # hành lang: chèn ở 26; cột khác: chèn ở 22 (khe tối giữa Hall và các phòng)
        col = [cells[y][x] for y in range(MH)]
        fill = [(cells[29][x] if i % 2 == 0 else cells[30][x]) for i in range(N)] if ins == Y0 else [cells[Y1][x]] * N
        newc = col[:ins] + fill + col[ins:]
        for y in range(MH + N): out[y][x] = newc[y]
    new = [','.join(r) for r in out]
    head = head.replace('height="%d"' % MH, 'height="%d"' % (MH + N))
    return head + '\n'.join(r + (',' if i < len(new) - 1 else '') for i, r in enumerate(new)) + tail
s, k = re.subn(r'(<layer id="\d+" name="[^"]*" width="%d" height="%d">.*?<data encoding="csv">\n)(.*?)(\n</data>)' % (MW, MH), fix_layer, s, flags=re.S)
print('lớp ô:', k)
s = s.replace('<map version="1.10" tiledversion="1.12.2" orientation="orthogonal" renderorder="right-down" width="114" height="124"',
              '<map version="1.10" tiledversion="1.12.2" orientation="orthogonal" renderorder="right-down" width="114" height="%d"' % (MH + N), 1)
assert 'height="%d"' % (MH + N) in s[:400]
# ---- vật thể
def fix_obj(m):
    tag = m.group(0)
    name = re.search(r' name="([^"]*)"', tag); name = name.group(1) if name else ''
    if name in KEEP: return tag
    y = float(re.search(r' y="([-\d.]+)"', tag).group(1))
    hm = re.search(r' height="([\d.]+)"', tag); h = float(hm.group(1)) if hm else 0.0
    gid = ' gid="' in tag
    top = y - h if gid else y; bot = top + h
    x = float(re.search(r' x="([-\d.]+)"', tag).group(1)); wm = re.search(r' width="([\d.]+)"', tag); w = float(wm.group(1)) if wm else 0.0
    cx = (x + w / 2) / T
    yc = Y0 if CX0 <= cx <= CX1 + 1 else Y1
    if top >= yc * T - 0.01:
        return re.sub(r' y="[-\d.]+"', ' y="%g"' % round(y + N * T, 2), tag, count=1)
    if not gid and bot > yc * T + 0.01:
        return re.sub(r' height="[\d.]+"', ' height="%g"' % round(h + N * T, 2), tag, count=1)
    return tag
s, k = re.subn(r'<object id="\d+"[^>]*>|<object id="\d+"[^>]*/>', fix_obj, s); print('vật thể xét:', k)
# ---- toạ độ ô trong kịch bản / thuộc tính "x,y"
def shift_xy(txt):
    x, y = txt.split(','); yf = float(y)
    yc = Y0 if CX0 <= float(x) <= CX1 + 1 else Y1
    return txt if yf < yc else '%s,%g' % (x, yf + N)
XY = r'-?\d+(?:\.\d+)?,-?\d+(?:\.\d+)?'
def fix_script(v):
    out = []
    for line in v.split('\\n'):
        parts = line.split(' ', 2)
        cmd = parts[0]
        if cmd in ('cam', 'glow', 'beat') and len(parts) > 1 and re.fullmatch(XY, parts[1]): parts[1] = shift_xy(parts[1])
        elif cmd in ('walk', 'face') and len(parts) > 2 and re.fullmatch(XY, parts[2].split(' ')[0]):
            q = parts[2].split(' ', 1); q[0] = shift_xy(q[0]); parts[2] = ' '.join(q)
        elif cmd in ('walk', 'face') and len(parts) > 1 and re.fullmatch(XY, parts[1]): parts[1] = shift_xy(parts[1])
        elif cmd == 'bubble' and len(parts) > 1:
            who = line[len('bubble '):].split(':', 1)[0].strip()
            if re.fullmatch(XY, who): line = line.replace(who, shift_xy(who), 1); out.append(line); continue
        out.append(' '.join(parts))
    return '\\n'.join(out)
def fix_prop(m):
    name, val = m.group(1), m.group(2)
    if name == 'script': return m.group(0).replace(val, fix_script(val))
    if re.fullmatch(XY, val): return m.group(0).replace(val, shift_xy(val))
    return m.group(0)
s = re.sub(r'<property name="([^"]+)" value="([^"]*)"', fix_prop, s)
# ---- bẫy đoạn mới (toạ độ cuối; dải mới y 26..50, lòng hành lang x 48.6..63.4)
nid = [int(re.search(r'nextobjectid="(\d+)"', s).group(1))]
def P(k, v):
    if isinstance(v, bool): return '    <property name="%s" type="bool" value="%s"/>' % (k, 'true' if v else 'false')
    if isinstance(v, (int, float)): return '    <property name="%s" type="float" value="%g"/>' % (k, v)
    return '    <property name="%s" value="%s"/>' % (k, str(v).replace('&', '&amp;').replace('"', '&quot;'))
def obj(name, typ, x, y, w, h, props):
    i = nid[0]; nid[0] += 1; pr = dict(props); pr['unity:IsTrigger'] = True
    L = ['  <object id="%d" name="%s" type="%s" x="%g" y="%g" width="%g" height="%g">' % (i, name, typ, round(x * T, 1), round(y * T, 1), round(w * T, 1), round(h * T, 1)), '   <properties>']
    return '\n'.join(L + [P(k, pr[k]) for k in sorted(pr)] + ['   </properties>', '  </object>']) + '\n'
ST = 'walk_crossed'
M = ''
# A — "Sắt trồi": 3 dải gai sóng chắn ngang toàn bề rộng, lệch nhịp -> canh khe sóng mà leo
for i, (yy, off) in enumerate([(47.0, 0.0), (44.0, 0.7), (41.0, 1.4)]):
    M += obj('walk2_spk_%d' % i, 'spikes', 48.8, yy, 14.4, 1.2, {'mechanic': 'spikes', 'mode': 'cycle', 'damage': 14, 'style': 1, 'warn_seconds': 0.5,
             'on_seconds': 0.8, 'off_seconds': 1.4, 'offset': off, 'wave_axis': 'x', 'wave_step': 0.1, 'stop_when': ST})
# B — 2 lưỡi cưa chạy ngang toàn bề rộng + tên từ hai tường bắn vào khe giữa hai cưa
for i, (yy, off) in enumerate([(38.8, 0.0), (35.6, 0.5)]):
    M += obj('walk2_saw_%d' % i, 'blade', 49.0, yy - 0.5, 14.0, 1.0, {'mechanic': 'blade', 'kind': 'saw', 'damage': 16, 'knock': 3, 'speed': 3.4,
             'pause': 0.25, 'offset': off, 'stop_when': ST})
for i, (x, d, off) in enumerate([(48.2, 'right', 0.0), (63.0, 'left', 0.8)]):
    pr = {'mechanic': 'launcher', 'kind': 'arrow', 'dir': d, 'mode': 'cycle', 'period': 1.6, 'offset': off, 'range': 16, 'warn_seconds': 0.6, 'damage': 12, 'stop_when': ST}
    if d == 'left': pr['flip'] = True
    M += obj('walk2_arrow_%d' % i, 'launcher', x, 36.7, 1, 1, pr)
# C — sàn sập so le (2 hàng ô sập, khe an toàn lệch nhau -> đi zic-zac) + tượng lửa trên tường Hall bắn dọc hai mép
for i, xx in enumerate([49.0, 52.9, 56.8, 60.7]):
    M += obj('walk2_pitA_%d' % i, 'pit', xx, 31.2, 2.4, 1.8, {'mechanic': 'pit', 'damage': 14, 'open_seconds': 2.2, 'warn_seconds': 0.45, 'size': 2,
             'return_at': '56,34', 'stop_when': ST, 'text': 'The floor gives way. You haul yourself out of the dark.'})
for i, xx in enumerate([50.95, 54.85, 58.75]):
    M += obj('walk2_pitB_%d' % i, 'pit', xx, 28.6, 2.4, 1.8, {'mechanic': 'pit', 'damage': 14, 'open_seconds': 2.2, 'warn_seconds': 0.45, 'size': 2,
             'return_at': '56,34', 'stop_when': ST, 'text': 'The floor gives way. You haul yourself out of the dark.'})
for i, (x, off) in enumerate([(50.5, 0.0), (61.5, 1.0)]):
    M += obj('walk2_fire_%d' % i, 'launcher', x, 25.6, 1, 1, {'mechanic': 'launcher', 'kind': 'fire', 'dir': 'down', 'mode': 'cycle', 'period': 2.0,
             'offset': off, 'range': 8, 'warn_seconds': 0.6, 'damage': 16, 'burn_dps': 4, 'burn_seconds': 2, 'stop_when': ST})
m = re.search(r'<objectgroup id="\d+" name="mechanics"[^>]*>\n', s); e = s.index(' </objectgroup>', m.end()); s = s[:e] + M + s[e:]
# tường hai bên đoạn mới (nền tối ngoài hành lang) — blocker
BL = ''
for x0, x1 in ((30.0, 48.4), (63.6, 80.0)):
    i = nid[0]; nid[0] += 1
    BL += '  <object id="%d" name="blocker" type="blocker" x="%g" y="%g" width="%g" height="%g">\n   <properties>\n    <property name="collider_kind" value="terrain"/>\n   </properties>\n  </object>\n' % (i, x0 * T, Y0 * T, (x1 - x0) * T, N * T)
m = re.search(r'<objectgroup id="\d+" name="blockers"[^>]*>\n', s); e = s.index(' </objectgroup>', m.end()); s = s[:e] + BL + s[e:]
s = re.sub(r'nextobjectid="\d+"', 'nextobjectid="%d"' % nid[0], s, count=1)
if crlf: s = s.replace('\n', '\r\n')
open(TMX, 'wb').write(s.encode('utf-8')); print('md5', hashlib.md5(s.encode('utf-8')).hexdigest())
# ---- nhiệm vụ: dời mục tiêu (trừ đỉnh Walk), cầu tời mới ở mê cung, khung nhìn cao thêm
q = json.load(open(QJ, encoding='utf-8')); q['view']['h'] = MH + N
for st in q['steps']:
    for t in st['targets']:
        if t['label'] == 'The bridge winch': t['x'], t['y'] = 35.0, 99.1
        if t['label'] != 'The top of the Walk':
            yc = Y0 if CX0 <= t['x'] <= CX1 + 1 else Y1
            if t['y'] >= yc: t['y'] = round(t['y'] + N, 2)
            if 'preY' in t:
                yp = Y0 if CX0 <= t['preX'] <= CX1 + 1 else Y1
                if t['preY'] >= yp: t['preY'] = round(t['preY'] + N, 2)
json.dump(q, open(QJ, 'w', encoding='utf-8'), ensure_ascii=False)
print('quest ok')
