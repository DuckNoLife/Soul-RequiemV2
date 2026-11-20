# Map 2b — The Kroven Deep. python3 gen_map.py -> Maps/map2b_kroven_deep.tmx (sinh trọn file; sửa map thì sửa script rồi chạy lại)
from grid import W, H, FLOOR
from gen_tiles import walls, ground
from gen_water import surface, curb, water   # 17/11 (§284): kênh nước Sluice Walk
import tmxio, objects

# Lỗ cổng trong vách bắc (mẫu r2_gate Map 2): mặt vách hai bên lỗ chạy THẲNG tới chân vòm (không bo góc), vòm 4x3 ô che phần giữa.
def gate_cut(x0, face_top):
    for x in (x0 - 1, x0 + 4):
        for k, g in enumerate((7230, 7246, 7262)):
            walls[(face_top + k) * W + x] = g
gate_cut(15, 13)          # rest_gate: lỗ x15..18, mặt vách hàng 13..15 của Nolan's Rest
import re as _re
# tileset riêng của 2b xếp NGAY SAU tileset cuối của Map 2 (17/11 §286: Map 2 thêm SR-stair-down/grate 14643, 14644 -> tự tính, không ghi cứng)
_last = max(int(m.group(1)) for m in (_re.search(r'firstgid="(\d+)"', l) for l in tmxio.tileset_lines()) if m)
IRON_FIRST = _last + 1
STAIR_FIRST = IRON_FIRST + 1                         # vòm cầu thang đá lên Forgotten Store (SR-stair-kroven)
O = tmxio.Objs()
groups = {k: [] for k in ['ground decals', 'wall props', 'props', 'props front', 'ambient fx', 'mechanics_art', 'zones', 'spawns', 'triggers', 'gates', 'obstacle colliders', 'blockers', 'map bounds', 'mechanics']}
groups['_ironcrown_gid'] = IRON_FIRST
groups['_stair_gid'] = STAIR_FIRST
objects.build(O, groups)
del groups['_ironcrown_gid']; del groups['_stair_gid']
layers = [('ground', ground, [tmxio.P('unity:SortingOrder', -20)]),
          ('curb surface', surface, [tmxio.P('unity:SortingOrder', -13)], '#b4e0b8'),
          ('curb', curb, [tmxio.P('unity:SortingOrder', -12)]),
          ('curb water', water, [tmxio.P('unity:SortingOrder', -11)], '#a6d4ae'),
          ('walls', walls, None)]
tmxio.write(tmxio.OUT, W, H, layers, groups, O, [('flag_group', 'Map2_UndercityOfVael')], extra_ts=[(IRON_FIRST, '../tilesets/SR-relic-ironcrown.tsx'), (STAIR_FIRST, '../tilesets/SR-stair-kroven.tsx')])
print('ok', tmxio.OUT, 'objects', O.n)
