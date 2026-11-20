# 19/11 (§333): cài King's Barrow bản GẬP. Kiểm md5 đầu vào (khác -> DỪNG), sao lưu _backups/2026-11-19_barrow_ring/, giữ CRLF, in md5 mới.
import os, re, sys, shutil, hashlib, json
R = os.path.expanduser('~/mnt/Soul Requiem/'); PKG = os.path.dirname(os.path.abspath(__file__)) + '/'
B = R + '_backups/2026-11-19_barrow_ring/'
MAPS = R + 'Assets/GamePackage/EPIC RPG World Pack - Cemetery V 1.6/TiledMap Editor/Maps/'
M3B, M3 = MAPS + 'map3b_kings_barrow.tmx', MAPS + 'map3_vale_of_graves.tmx'
QJ = R + 'Assets/Resources/Quests/Map3b_KingsBarrow.json'; MM = R + 'Assets/Resources/Minimap/Map3b_KingsBarrow.png'
def md5(p): return hashlib.md5(open(p, 'rb').read()).hexdigest()
for p, m in {M3B: '265d10c6cea5d70c6db6c19b053e0445', M3: '91cbbf5fb129c304e6b2c40c64b42f8b'}.items():
    if md5(p) != m: sys.exit('DỪNG: %s đã đổi (md5 %s, chờ %s)' % (os.path.basename(p), md5(p), m))
os.makedirs(B, exist_ok=True)
for p in (M3B, M3, QJ, MM): shutil.copyfile(p, B + os.path.basename(p))
def read(p):
    raw = open(p, 'rb').read(); return raw.decode('utf-8').replace('\r\n', '\n'), b'\r\n' in raw
def write(p, s, crlf): open(p, 'wb').write((s.replace('\n', '\r\n') if crlf else s).encode('utf-8'))
NEW = PKG + 'map3b_kings_barrow.tmx'
assert md5(NEW) == sys.argv[1], md5(NEW)
shutil.copyfile(NEW, M3B)
s, crlf = read(M3)
m = re.search(r'<object id="5480"[^>]*>.*?</object>', s, re.S); blk = m.group(0)
a = '<property name="arrive_at" value="5.6,119.4"/>'
assert 'name="barrow_stair"' in blk and a in blk
s = s.replace(blk, blk.replace(a, '<property name="arrive_at" value="5.6,60.4"/>')); write(M3, s, crlf)
s, crlf = read(QJ); write(QJ, json.dumps(json.load(open(PKG + 'Map3b_KingsBarrow.json', encoding='utf-8')), indent=1, ensure_ascii=False), crlf)
for p in (M3B, M3, QJ): print(md5(p), os.path.relpath(p, R))
