# 19/11 (§342): lối xuống King's Barrow = HỐ TỐI giữa hai cây chết bờ nam Sinking Fields (người dùng: bậc đá đỏ quá lộ, khác hẳn đất Map 3).
# Map 3: bỏ tile object barrow_entrance + 2 khối chặn của nó; exit barrow_stair dời ra mép nam của hố (ô 117-120, 74-78), "Climb down";
#        bia barrow_stone dời lên hòn đá ở mép đông bắc hố; lời Aldous "old stair" -> "a pit". Map 3b: x2_climb arrive_at -> cạnh hố.
# Kiểm md5 trước, sao lưu _backups/2026-11-19_fix342/, giữ kiểu xuống dòng.
import re, sys, hashlib, shutil, os
R = os.path.expanduser('~/mnt/Soul Requiem/')
MAPS = R + 'Assets/GamePackage/EPIC RPG World Pack - Cemetery V 1.6/TiledMap Editor/Maps/'
M3, M3B = MAPS + 'map3_vale_of_graves.tmx', MAPS + 'map3b_kings_barrow.tmx'
B = R + '_backups/2026-11-19_fix342/'
def md5(p): return hashlib.md5(open(p, 'rb').read()).hexdigest()
for p, m in {M3: 'f57f0749fa8304dd776a19bba86a53e5', M3B: 'd6851dd66f7aabca369456a2bfec9935'}.items():
    if md5(p) != m: sys.exit('DỪNG: %s md5 %s (chờ %s)' % (os.path.basename(p), md5(p), m))
os.makedirs(B, exist_ok=True)
for p in (M3, M3B): shutil.copyfile(p, B + os.path.basename(p))
def read(p):
    raw = open(p, 'rb').read(); return raw.decode('utf-8'), (b'\r\n' in raw)
def write(p, s): open(p, 'wb').write(s.encode('utf-8'))
T = 32
s, crlf = read(M3)
NL = '\r\n' if crlf else '\n'
def drop(oid):
    global s
    m = re.search(r'\r?\n[ \t]*<object id="%d"[^>]*?(?:/>|>.*?</object>)' % oid, s, re.S)
    assert m, oid
    s = s[:m.start()] + s[m.end():]
for oid in (5479, 5482, 5483):                      # tile bậc đá + 2 khối chặn hai bên
    drop(oid)
def retag(oid, x, y, w, h):
    global s
    m = re.search(r'<object id="%d"([^>]*?) x="[\d.\-]+" y="[\d.\-]+" width="[\d.]+" height="[\d.]+"' % oid, s); assert m, oid
    s = s[:m.start()] + '<object id="%d"%s x="%g" y="%g" width="%g" height="%g"' % (oid, m.group(1), x, y, w, h) + s[m.end():]
def setp(oid, k, v):
    global s
    m = re.search(r'<object id="%d"[^>]*>.*?</object>' % oid, s, re.S); assert m, oid
    blk = m.group(0)
    nb, n = re.subn(r'(<property name="%s" value=")[^"]*(")' % re.escape(k), lambda mm: mm.group(1) + v + mm.group(2), blk)
    assert n == 1, (oid, k)
    s = s.replace(blk, nb)
retag(5480, 117.4 * T, 77.0 * T, 2.2 * T, 1.2 * T)   # mép nam của hố (Ren đứng dưới hố nhấn T)
setp(5480, 'verb', 'Climb down')
setp(5480, 'depart_text', 'The pit is deeper than the others, and its walls are cut stone under the mud. You lower yourself down on a rotten rope someone left tied to a root.')
retag(5481, 119.3 * T, 73.9 * T, 1.3 * T, 1.1 * T)   # bia: hòn đá ở mép đông bắc hố
old = 'There is an old stair down there, under the dead trees on the south bank.'
assert s.count(old) == 1
s = s.replace(old, 'There is a pit down there, between the dead trees on the south bank - deeper than the others, with a crowned stone at its lip.')
write(M3, s)
t, _ = read(M3B)
o = '<property name="arrive_at" value="127.6,78.9"/>'
assert t.count(o) == 1
t = t.replace(o, '<property name="arrive_at" value="118.6,79.2"/>')
write(M3B, t)
print('M3', md5(M3)); print('M3B', md5(M3B))
