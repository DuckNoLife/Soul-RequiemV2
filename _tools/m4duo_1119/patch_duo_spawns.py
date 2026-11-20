# 19/11 (§344): Map 4 The Royal Reliquary (khu c4) — thay Lantern_Knight (trùng boss Map 3) bằng SONG ĐẤU Brazen_Reliquary + Hollow_Champion
# (cùng đợt 2, cùng boss = true; Engine trước -> boss chính); Lantern_Warden đợt 1 (cùng hình Knight_LVL4 lửa đỏ) -> Choir_Cutthroat;
# object boss reliquary_knight_fight: thẻ tên "THE RELIQUARY WARDENS" / "Brass and Bone", bỏ màn xuất hiện `turn` (pháo không có hình quay lưng),
# thay bằng intro_anim Fire + intro_roar. Kiểm md5 (tham số), sao lưu, giữ kiểu xuống dòng.
import re, sys, hashlib, shutil, os
R = os.path.expanduser('~/mnt/Soul Requiem/')
M4 = R + 'Assets/GamePackage/EPIC RPG World Pack - Crypt V.1.6/TiledMap Editor/Maps/map4_catacombs_of_naming.tmx'
md5 = lambda p: hashlib.md5(open(p, 'rb').read()).hexdigest()
if md5(M4) != sys.argv[1]: sys.exit('DỪNG: md5 ' + md5(M4))
shutil.copyfile(M4, R + '_backups/2026-11-19_m4duo/map4_before_duo.tmx')
s = open(M4, 'rb').read().decode('utf-8'); NL = '\r\n' if '\r\n' in s else '\n'
def blk(oid):
    m = re.search(r'<object id="%d"[^>]*>.*?</object>' % oid, s, re.S); assert m, oid; return m.group(0)
def put(old, new):
    global s
    assert s.count(old) == 1; s = s.replace(old, new)
T = 32
b = blk(1006)
nb = b.replace('name="Lantern_Knight_15"', 'name="Brazen_Reliquary_15"').replace('value="Lantern_Knight"', 'value="Brazen_Reliquary"')
nb = re.sub(r'x="[\d.]+" y="[\d.]+"', 'x="%g" y="%g"' % (104.5 * T, 82.5 * T), nb, count=1)
nid = int(re.search(r'nextobjectid="(\d+)"', s).group(1))
champ = nb.replace('<object id="1006" name="Brazen_Reliquary_15"', '<object id="%d" name="Hollow_Champion_16"' % nid).replace('value="Brazen_Reliquary"', 'value="Hollow_Champion"')
champ = re.sub(r'x="[\d.]+" y="[\d.]+"', 'x="%g" y="%g"' % (98.5 * T, 82.0 * T), champ, count=1)
put(b, nb + NL + '  ' + champ)
s = re.sub(r'nextobjectid="\d+"', 'nextobjectid="%d"' % (nid + 1), s, count=1)
w = blk(1003)
put(w, w.replace('name="Lantern_Warden_12"', 'name="Choir_Cutthroat_12"').replace('value="Lantern_Warden"', 'value="Choir_Cutthroat"'))
k = blk(1008)
k2 = k.replace('<property name="card_sub" value="Sworn to the Lantern"/>', '<property name="card_name" value="THE RELIQUARY WARDENS"/>' + NL + '    <property name="card_sub" value="Brass and Bone"/>')
k2 = k2.replace('<property name="entrance" value="turn"/>', '<property name="intro_anim" value="Fire"/>' + NL + '    <property name="intro_roar" type="bool" value="true"/>')
assert k2 != k and 'turn' not in k2
put(k, k2)
open(M4, 'wb').write(s.encode('utf-8')); print(md5(M4))
