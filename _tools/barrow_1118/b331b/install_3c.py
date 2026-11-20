# 18/11 (§331): cài bản King's Barrow NỐI LIỀN. Chạy trên máy người dùng: python3 install_3c.py
# Kiểm md5 đầu vào (khác -> DỪNG), sao lưu _backups/2026-11-18_barrow_connected/, giữ CRLF, sửa theo id object, in md5 mới.
import os, re, sys, shutil, hashlib, json
R = os.path.expanduser('~/mnt/Soul Requiem/'); PKG = os.path.dirname(os.path.abspath(__file__)) + '/'
B = R + '_backups/2026-11-18_barrow_connected/'
MAPS = R + 'Assets/GamePackage/EPIC RPG World Pack - Cemetery V 1.6/TiledMap Editor/Maps/'
M3B, M3 = MAPS + 'map3b_kings_barrow.tmx', MAPS + 'map3_vale_of_graves.tmx'
QJ = R + 'Assets/Resources/Quests/Map3b_KingsBarrow.json'
GR = R + 'Assets/Scripts/Gameplay/Enemies/Bosses/GhostRank.cs'
RF = R + 'Assets/Scripts/Gameplay/Enemies/Bosses/RedFieldSkills.cs'
REG = R + 'Assets/Scripts/Gameplay/World/Mechanics/MechanismRegistry.cs'
RANK = R + 'Assets/Scripts/Gameplay/World/Mechanics/Hazards/RankMechanism.cs'
def md5(p): return hashlib.md5(open(p, 'rb').read()).hexdigest()
EXPECT = {M3B: '5d652be0644d18873dc1ae5bea931b2f', M3: 'd4595c40c17e3da8be1eb9ba027cd7cb'}
for p, m in EXPECT.items():
    if md5(p) != m: sys.exit('DỪNG: %s đã đổi (md5 %s, chờ %s) — đọc lại trước khi sửa' % (os.path.basename(p), md5(p), m))
assert not os.path.exists(RANK), 'RankMechanism.cs đã có'
os.makedirs(B, exist_ok=True)
for p in (M3B, M3, QJ, GR, RF, REG, R + 'Assets/Resources/Minimap/Map3b_KingsBarrow.png'):
    shutil.copyfile(p, B + os.path.basename(p))
def read(p):
    raw = open(p, 'rb').read(); return raw.decode('utf-8').replace('\r\n', '\n'), b'\r\n' in raw
def write(p, s, crlf):
    open(p, 'wb').write((s.replace('\n', '\r\n') if crlf else s).encode('utf-8'))

# ---- Map 3b: bản sinh từ gen_3c.py (tất định) — kiểm md5 gói
NEW = PKG + 'map3b_kings_barrow.tmx'
assert md5(NEW) == '27b274c5539a2bfb84d77f8553313c94', md5(NEW)
shutil.copyfile(NEW, M3B)
# ---- Map 3: barrow_stair (id 5480) tới chỗ R0 mới (R0 dời +1, +114 ô)
s, crlf = read(M3)
m = re.search(r'<object id="5480"[^>]*>.*?</object>', s, re.S); blk = m.group(0)
assert 'name="barrow_stair"' in blk and '<property name="arrive_at" value="4.6,5.4"/>' in blk
s = s.replace(blk, blk.replace('<property name="arrive_at" value="4.6,5.4"/>', '<property name="arrive_at" value="5.6,119.4"/>'))
write(M3, s, crlf)
# ---- quest
s, crlf = read(QJ); q = json.load(open(PKG + 'Map3b_KingsBarrow.json', encoding='utf-8'))
write(QJ, json.dumps(q, indent=1, ensure_ascii=False), crlf)
# ---- code: GhostRank giới hạn chiều dọc (tuỳ chọn), boss + sảnh dùng
s, crlf = read(GR)
a = "        private float alpha;\n"
assert a in s and 'hitLo' not in s
s = s.replace(a, a + "        private float hitLo = float.NegativeInfinity, hitHi = float.PositiveInfinity;   // 18/11 (§331): dải dọc nhận đòn\n", 1)
a = "        private void Start() => t0 = Time.time;\n"
assert a in s
s = s.replace(a, "        /// <summary>18/11 (§331): chỉ gây sát thương khi Ren đứng trong dải dọc [lo, hi] (sảnh Marching Hall: không chạm Ren ở hành lang).</summary>\n"
                 "        public GhostRank Bound(float lo, float hi) { hitLo = lo; hitHi = hi; return this; }\n\n" + a, 1)
a = "            if (Mathf.Abs(p.x - x) > 0.16f) return;\n"
assert a in s
s = s.replace(a, a + "            if (p.y < hitLo || p.y > hitHi) return;                      // 18/11 (§331): ngoài dải (vd đứng ở hành lang)\n", 1)
write(GR, s, crlf)
s, crlf = read(RF)
a = "                            warn, speed, travel, rankDamage, 0.7f, Layer, Order, tint);\n"
assert a in s
s = s.replace(a, "                            warn, speed, travel, rankDamage, 0.7f, Layer, Order, tint)\n"
                 "                     .Bound(yMin - 0.6f, yMax + 0.6f);                         // 18/11 (§331): phủ cả sân, không với ra hành lang nam\n", 1)
write(RF, s, crlf)
s, crlf = read(REG)
a = '            { "pit", () => new PitTrapMechanism() },'
assert a in s and '"ranks"' not in s
s = s.replace(a, '            { "ranks", () => new RankMechanism() },              // 18/11 (§331): hàng lính hồn hành quân (Marching Hall, Map 3b)\n' + a, 1)
write(REG, s, crlf)
shutil.copyfile(PKG + 'RankMechanism.cs', RANK)
crlf_any = b'\r\n' in open(GR, 'rb').read()
if crlf_any: write(RANK, read(RANK)[0], True)
for p in (M3B, M3, QJ, GR, RF, REG, RANK): print(md5(p), os.path.relpath(p, R))
