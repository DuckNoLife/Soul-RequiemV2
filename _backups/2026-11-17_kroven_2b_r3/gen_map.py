# Map 2b — The Kroven Deep. python3 gen_map.py -> Maps/map2b_kroven_deep.tmx (sinh trọn file; sửa map thì sửa script rồi chạy lại)
from grid import W, H, FLOOR
from gen_tiles import walls, ground
import tmxio, objects

# Lỗ cổng trong vách bắc (mẫu r2_gate Map 2): mặt vách hai bên lỗ chạy THẲNG tới chân vòm (không bo góc), vòm 4x3 ô che phần giữa.
def gate_cut(x0, face_top):
    for x in (x0 - 1, x0 + 4):
        for k, g in enumerate((7230, 7246, 7262)):
            walls[(face_top + k) * W + x] = g
gate_cut(15, 13)          # rest_gate: lỗ x15..18, mặt vách hàng 13..15 của Nolan's Rest
IRON_FIRST = 14643                                   # ngay sau tileset cuối của Map 2 (SR-relic-moonlit 14642)
O = tmxio.Objs()
groups = {k: [] for k in ['ground decals', 'wall props', 'props', 'props front', 'ambient fx', 'mechanics_art', 'zones', 'spawns', 'triggers', 'gates', 'obstacle colliders', 'blockers', 'map bounds', 'mechanics']}
groups['_ironcrown_gid'] = IRON_FIRST
objects.build(O, groups)
del groups['_ironcrown_gid']
layers = [('ground', ground, [tmxio.P('unity:SortingOrder', -20)]), ('walls', walls, None)]
tmxio.write(tmxio.OUT, W, H, layers, groups, O, [('flag_group', 'Map2_UndercityOfVael')], extra_ts=[(IRON_FIRST, '../tilesets/SR-relic-ironcrown.tsx')])
print('ok', tmxio.OUT, 'objects', O.n)
