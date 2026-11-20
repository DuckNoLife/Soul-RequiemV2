# 18/11 (§319): mê cung Blade Yard theo bố cục người dùng vẽ lại trong Tiled (lưới 6x4 ô A-F x 0-3, cột tường gai ở x 37.5/42.5/47.5/52.5/57.5,
# y 110.5/115.5/120.5). Xoá toàn bộ bẫy mz_* cũ (lệch, thừa) + cần hãm yard_brake (tắt bẫy ở xa -> vô lý), đặt lại 10 bẫy khớp lối đi.
# Đường đi: vào F2 -> F3 -> E3 -> D3 -> (khe D) -> D2 -> E2 -> E1 -> D1 -> C1 -> C2 -> B2 -> B1 (tời cầu)
#           về: B2 -> C2 -> C3 -> B3 -> A3 -> A2 -> A1 -> A0 -> B0 -> C0 -> D0 -> E0 (cầu).
# Kèm: cảnh "lantern_line" (Across the pit) chỉ còn đặt cờ, không diễn; máy bắn Penitent's Walk áp đúng mép tường.
import re, sys, hashlib, shutil, os
T = os.path.expanduser("~/mnt/Soul Requiem/Assets/GamePackage/EPIC RPG World Pack - Crypt V.1.6/TiledMap Editor/Maps/map4_catacombs_of_naming.tmx")
EXPECT = "38ca60af26f03c852864810ca9412e2b"
s = open(T, encoding='utf-8').read()
md5 = hashlib.md5(open(T, 'rb').read()).hexdigest()
if md5 != EXPECT and '--force' not in sys.argv: sys.exit("md5 lệch: " + md5)
shutil.copy(T, os.path.expanduser("~/mnt/Soul Requiem/_backups/2026-11-18_m4_maze3/map4_catacombs_of_naming.tmx"))

# 1) xoá bẫy cũ
drop = list(range(1116, 1147))           # 1116..1146 (gồm yard_brake 1137)
n0 = len(s)
for i in drop:
    s, k = re.subn(r'\n  <object id="%d" [^\n]*?/>(?=\n)' % i, '', s)
    if k == 0:
        s, k = re.subn(r'\n  <object id="%d" .*?\n  </object>(?=\n)' % i, '', s, flags=re.S)
    if k != 1: sys.exit("không xoá được object %d (%d)" % (i, k))

# 2) bẫy mới (toạ độ theo ô, nhân 32)
def P(n, v, t=None):
    tt = ' type="%s"' % t if t else ''
    return '    <property name="%s"%s value="%s"/>' % (n, tt, v)
def obj(oid, name, x, y, w, h, props):
    body = '\n'.join(P(*p) for p in props)
    return ('  <object id="%d" name="%s" type="%s" x="%g" y="%g" width="%g" height="%g">\n   <properties>\n%s\n   </properties>\n  </object>'
            % (oid, name, dict(props_t(props)).get('mechanic', ''), x * 32, y * 32, w * 32, h * 32, body))
def props_t(props): return [(p[0], p[1]) for p in props]

saw = lambda speed, off: [('damage', '16'), ('kind', 'saw'), ('knock', '3'), ('mechanic', 'blade'), ('offset', off), ('pause', '0.3'), ('speed', speed)]
fire = lambda d, period, off, rng: [('burn_dps', '4'), ('burn_seconds', '2'), ('dir', d), ('kind', 'fire'), ('mechanic', 'launcher'), ('mode', 'cycle'),
                                    ('offset', off), ('period', period), ('range', rng), ('warn_seconds', '0.6')]
arrow = lambda d, period, off, rng: [('dir', d), ('kind', 'arrow'), ('mechanic', 'launcher'), ('mode', 'cycle'), ('offset', off),
                                     ('period', period), ('range', rng), ('warn_seconds', '0.6')]
spk = [('damage', '14'), ('mechanic', 'spikes'), ('mode', 'cycle'), ('off_seconds', '1.4'), ('offset', '0'), ('on_seconds', '0.8'),
       ('style', '1'), ('warn_seconds', '0.5')]
traps = [
    # đoạn 1: hành lang đáy phía đông
    ('mz_E3_saw',     55.5, 122.5, 1, 4.5, saw('3.0', '0')),          # cưa chạy dọc chắn ngang hành lang đáy
    ('mz_D_gap_spk',  49.5, 120.5, 3, 2,   spk),                      # khe D3 -> D2: gai nhô theo nhịp
    # đoạn 2: hành lang giữa (đi về tây), lửa từ tường đông bắn dọc theo lưng
    ('mz_D1_saw',     50.5, 112.5, 1, 3,   saw('3.4', '0.4')),
    ('mz_F1_fire',    64.0, 113.5, 1, 1,   fire('left', '2.6', '0', '22')),
    # đoạn 3 (sau tời): hành lang đáy phía tây — tượng lửa tường tây + cưa
    ('mz_A3_fire_a',  34.0, 123.25, 1, 1,  fire('right', '2.2', '0', '14')),
    ('mz_A3_fire_b',  34.0, 125.25, 1, 1,  fire('right', '2.2', '1.1', '14')),
    ('mz_B3_saw',     40.5, 122.5, 1, 4.5, saw('3.2', '0.5')),
    # đoạn 4: cột A đi lên — một máy bắn tên ngang cột
    ('mz_A2_arrow',   34.0, 118.5, 1, 1,   arrow('right', '1.4', '0', '4')),
    # đoạn 5: hàng 0 tới cầu — tên bắn từ tường bắc xuống
    ('mz_B0_arrow',   40.5, 106.0, 1, 1,   arrow('down', '1.5', '0', '4')),
    ('mz_D0_arrow',   50.5, 106.0, 1, 1,   arrow('down', '1.5', '0.75', '4')),
]
m = re.search(r'nextobjectid="(\d+)"', s); nid = int(m.group(1))
blocks = []
for name, x, y, w, h, pr in traps:
    blocks.append(obj(nid, name, x, y, w, h, pr)); nid += 1
s = s.replace(m.group(0), 'nextobjectid="%d"' % nid, 1)
# chèn ngay sau tời cầu (id 915) trong lớp mechanics
i = s.index('<object id="915" ')
j = s.index('\n  </object>', i) + len('\n  </object>')
s = s[:j] + '\n' + '\n'.join(blocks) + s[j:]

# 3) cảnh lantern_line: bỏ diễn, chỉ đặt cờ (lớp p7 vẫn mở theo scene_lantern_line)
s, k = re.subn(r'(<object id="969" name="lantern_line" type="story"[^>]*>\s*<properties>\s*<property name="script" value=")[^"]*(")',
               r'\1set lantern_seen\2', s)
if k != 1: sys.exit("không sửa được lantern_line")

# 4) Penitent's Walk: máy bắn tên áp sát mép tường (tường x 48.4 / 63.6), bỏ flip (hình giờ xoay theo dir)
s, k1 = re.subn(r'(<object id="\d+" name="walk2_arrow_0" type="[^"]*" x=")[^"]*(")', r'\g<1>%g\2' % (48.4 * 32), s)
s, k2 = re.subn(r'(<object id="\d+" name="walk2_arrow_1" type="[^"]*" x=")[^"]*(")', r'\g<1>%g\2' % (62.6 * 32), s)
if k1 != 1 or k2 != 1: sys.exit("không thấy walk2_arrow (%d,%d)" % (k1, k2))
i = s.index('name="walk2_arrow_1"'); j = s.index('</object>', i)
seg = re.sub(r'\n\s*<property name="flip"[^\n]*', '', s[i:j]); s = s[:i] + seg + s[j:]

open(T, 'w', encoding='utf-8', newline='\r\n').write(s)
print("ok", hashlib.md5(open(T, 'rb').read()).hexdigest(), "nextobjectid", nid)
