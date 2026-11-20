# 20/11 (§350): Reliquary Lists -> 4 relic_rack thành 4 crystal_pillar (crimson / azure / verdant / gilded) + trang trí phòng
# (cờ thi đấu, bánh răng đồng trên tường bắc, lò lửa hồn, đống xu, cụm pha lê cạnh mỗi cột) — sprite_prop lớp mechanics. Kiểm md5, sao lưu.
import re, sys, hashlib, shutil, os
R = os.path.expanduser('~/mnt/Soul Requiem/')
M4 = R + 'Assets/GamePackage/EPIC RPG World Pack - Crypt V.1.6/TiledMap Editor/Maps/map4_catacombs_of_naming.tmx'
OUT = sys.argv[2] if len(sys.argv) > 2 else M4
md5 = lambda p: hashlib.md5(open(p, 'rb').read()).hexdigest()
if md5(M4) != sys.argv[1]: sys.exit('DỪNG: md5 ' + md5(M4))
if OUT == M4: shutil.copyfile(M4, R + '_backups/2026-11-20_lists/map4_before_crystals.tmx')
s = open(M4, 'rb').read().decode('utf-8'); NL = '\r\n' if '\r\n' in s else '\n'
T = 32
CR = {'halberd': 'crimson', 'hammer': 'azure', 'flail': 'verdant', 'axes': 'gilded'}
for w, c in CR.items():
    m = re.search(r'<object id="\d+" name="relic_rack_%s" type="relic_rack".*?</object>' % w, s, re.S); b = m.group(0)
    nb = b.replace('name="relic_rack_%s" type="relic_rack"' % w, 'name="crystal_%s" type="crystal_pillar"' % c)
    nb = nb.replace('<property name="weapon" value="%s"/>' % w, '<property name="crystal" value="%s"/>' % c)
    nb = re.sub(r'x="([\d.]+)" y="([\d.]+)" width="[\d.]+" height="[\d.]+"',
                lambda mm: 'x="%g" y="%g" width="%g" height="%g"' % (float(mm.group(1)) + 0.2 * T, float(mm.group(2)), 1.0 * T, 0.5 * T), nb, count=1)
    s = s.replace(b, nb, 1)
nid = int(re.search(r'nextobjectid="(\d+)"', s).group(1))
def prop(name, cx, foot, w, h, props):
    global nid
    props = [('unity:IsTrigger', True)] + props
    p = ''.join('    <property name="%s"%s value="%s"/>%s' % (k, ' type="bool"' if isinstance(v, bool) else (' type="float"' if isinstance(v, float) else ''),
                ('true' if v else 'false') if isinstance(v, bool) else ('%g' % v if isinstance(v, float) else v), NL) for k, v in props)
    o = '<object id="%d" name="%s" type="sprite_prop" x="%g" y="%g" width="%g" height="%g">%s   <properties>%s%s   </properties>%s  </object>' % (
        nid, name, (cx - w / 2) * T, (foot - h) * T, w * T, h * T, NL, NL, p, NL)
    nid += 1
    return o
new = []
for i, x in enumerate((84.5, 88.5, 101.0)):
    new.append(prop('lists_banner_%d' % i, x, 95.85, 1.0, 0.4, [('sprite', 'VFX/props/lists_banner'), ('collider', '0'), ('scale', 1.1)]))
for i, x in enumerate((79.7, 106.0)):
    new.append(prop('lists_gear_%d' % i, x, 95.6, 1.2, 0.4, [('sprite', 'VFX/props/wall_gear'), ('collider', '0'), ('flip', i == 1)]))
for i, (x, y) in enumerate(((90.3, 97.0), (95.7, 97.0), (87.6, 105.2), (97.4, 105.2))):
    new.append(prop('lists_brazier_%d' % i, x, y, 0.8, 0.4, [('sprite', 'VFX/props/soul_brazier'), ('collider', '0.26,0.12'), ('collider_shape', 'round'),
                                                       ('lit', True), ('light_color', '#a46bff'), ('light_radius', 1.9), ('light_intensity', 0.85), ('scale', 1.1)]))
for i, (x, y) in enumerate(((90.4, 106.8), (95.4, 100.2), (100.2, 107.9))):
    new.append(prop('lists_coins_%d' % i, x, y, 1.2, 0.4, [('sprite', 'VFX/props/coin_heap'), ('collider', '0'), ('flat', True)]))
CL = {'crimson': ((79.4, 98.1), (82.9, 98.5)), 'azure': ((101.9, 99.0), (102.7, 98.0)),
      'verdant': ((79.4, 108.5), (82.9, 108.2)), 'gilded': ((102.3, 108.2), (105.8, 108.5))}
LC = {'crimson': '#ff5a40', 'azure': '#66c8ff', 'verdant': '#70ff66', 'gilded': '#ffd85a'}
for c, pts in CL.items():
    for i, (x, y) in enumerate(pts):
        new.append(prop('crystal_cluster_%s_%d' % (c, i), x, y, 0.8, 0.3, [('sprite', 'VFX/props/crystal_cluster_' + c), ('collider', '0'),
                                                                         ('lit', True), ('light_color', LC[c]), ('light_radius', 1.1), ('light_intensity', 0.6), ('flip', i == 1)]))
mm = re.search(r'(<objectgroup id="35" name="mechanics"[^>]*>.*?)(\s*</objectgroup>)', s, re.S)
s = s[:mm.end(1)] + ''.join(NL + '  ' + o for o in new) + s[mm.end(1):]
s = re.sub(r'nextobjectid="\d+"', 'nextobjectid="%d"' % nid, s, count=1)
open(OUT, 'wb').write(s.encode('utf-8'))
print('xong ->', OUT, md5(OUT), len(new), 'đồ trang trí')
