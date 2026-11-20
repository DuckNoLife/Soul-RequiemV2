# Map 4 — The Catacombs of Naming: KHUNG v2 (17/11/2026, sau soát lỗi §298).
# Nguyên tắc: mọi cửa khoá nằm trên MẶT TƯỜNG BẮC (đi lên để vào); ngục (Old Prison) ở NAM, hầm mộ (Crypt) ở BẮC.
# Ngục: tường mỏng gạch xanh đậm (bộ xà lim), cổng xà lim trong ĐOẠN VÒM 6 ô, cửa gỗ trong vòm 3 ô, cổng đầu lâu trong dải tường chắn hết bề ngang.
# Hầm mộ: cửa sắt/gỗ/boss trên mặt gạch bắc + lối 2–3 ô xuyên dải đá; bẫy gắn trên mặt bệ nổi; cửa chuyển màn (Map 5) sau bệ lễ Hall.
import sys, os; sys.path.insert(0, '.'); sys.path.insert(0, '../lore_1116')
from ck import CK
from opk import OPK
W, H = 114, 124
k = CK(W, H, seed=46); o = OPK(k)
DOORS = []        # (tên, kiểu ảnh, x ô, mép trên ô của mặt tường, ghi chú)
# ===================================================================== HẦM MỘ (bắc, y0..62)
k.zone_area(0, 0, W, 63)
# Z8 HALL OF NAMING — sàn x40..73, hàng 4..17; bệ lễ phía bắc; cửa chuyển màn sang Map 5 trên mặt gạch sau bệ
k.room(40, 4, 34, 17)
k.passage(56, 0, 2, 4)                                         # lối sau cửa chuyển màn (ra mép map)
for px in (47, 65):
    for py in (6, 14): k.pillar(px, py, 2, 1)                   # 4 trụ wall-3 (đỉnh tối + mặt gạch 2 hàng) quanh bệ giữa
k.mask('dirt', 44, 6, 26, 14); k.band('slope', 56.5, 9, 10, 6)  # nền xương + bệ giữa có viền (như sân trụ của mẫu)
DOORS.append(('exit_map5', 'cr_metal5', 55, 1, 'cửa chuyển màn (between levels) sau bệ lễ'))
# Z7 PENITENT'S WALK — sàn x48..63, hàng 23..42; bệ cao phía bắc (mặt gạch hàng 29..31) mang máy bắn; bậc giữa
k.room(48, 26, 16, 17); k.mask('dirt', 49, 39, 14, 3)
k.plat(48, 26, 16, 6, attach='n'); k.stair(53, 32)
k.passage(55, 21, 2, 5)                                        # lối xuyên đá lên Hall (sau cửa sắt)
DOORS.append(('hall_door', 'cr_metal3', 54, 23, 'cửa sắt mặt gạch bắc Walk -> Hall'))
# Z4 THE OSSUARY — sàn x38..73, hàng 48..58; bệ thờ phía bắc; cổng 2 ấn (cửa boss 3x3) trên mặt gạch sau bệ
k.room(38, 48, 36, 14)
k.plat(49, 48, 15, 5, attach='n'); k.stair(54, 53)
k.passage(55, 43, 3, 5)                                        # lối sau cổng 2 ấn xuống Walk (3 ô, tâm 56.5 = tâm bệ/bậc); khung cửa boss 4 ô lùi nửa ô che 2 mép (§303)
DOORS.append(('seal_gate', 'cr_bossdoor', 55, 45, 'cổng 2 ấn sau bệ thờ Ossuary'))
k.passage(54, 62, 5, 1)                                     # lối nam ra cổng đầu lâu (ngục)
k.band('carpet', 56.5, 56, 3, 7); k.mask('dirt', 39, 56, 12, 5); k.mask('dirt', 63, 56, 10, 5); k.mask('g2', 40, 49, 8, 4); k.mask('g2', 66, 49, 7, 4)
# Z5 SCRIPTORIUM — sàn x4..33, hàng 48..62; bệ tây bắc (Ấn 1 + The Registrar); cửa gỗ lên phòng thư
k.room(4, 48, 30, 13); k.room(34, 54, 4, 3); k.mask('g2', 17, 55, 16, 6); k.mask('dirt', 5, 56, 9, 5)
k.plat(4, 48, 12, 4, attach='nw'); k.stair(8, 52)
k.room(10, 26, 18, 14)                                      # phòng thư của mẹ (sàn hàng 26..39)
k.mask('slope', 14, 29, 10, 7)                               # bệ mộ có viền (như mẫu: mộ phủ vải đỏ giữa 2 chân nến)
k.passage(19, 40, 2, 8)                                        # lối xuyên đá
DOORS.append(('letters_door', 'cr_wood1', 18, 45, 'cửa gỗ mặt gạch bắc Scriptorium -> phòng thư'))
# Z6 ROYAL RELIQUARY — sàn x78..107, hàng 48..62; bệ đông bắc (Ấn 2 + Lantern Knight); cửa sắt lên kho kiểm kê
k.room(78, 48, 30, 13); k.room(74, 54, 4, 3); k.mask('g2', 80, 55, 15, 6)
k.plat(96, 48, 12, 4, attach='ne'); k.stair(99, 52)
k.room(84, 26, 18, 14)                                      # kho kiểm kê
k.mask('g2', 86, 28, 14, 10)
k.passage(92, 40, 2, 8)
DOORS.append(('vault_door', 'cr_metal2', 91, 45, 'cửa sắt mặt gạch bắc Reliquary -> kho'))
# vòm cuốn / hốc thờ trên mặt gạch (đúng kiểu mẫu: vòm hai bên, cửa giữa cờ đỏ)
k.stamp('arcade5', 38, 45); k.stamp('arcade5_grates', 38, 45); k.stamp('arcade5b', 63, 45); k.stamp('arcade5b_grates', 63, 45)
k.stamp('shrine_a', 50, 45); k.stamp('shrine_b', 60, 45)
k.stamp('arcade5', 4, 45)
for i, x in enumerate((22, 25, 28, 31)): k.niche(x, 45, (24, 18, 26, 20)[i])          # hốc tủ xương Scriptorium (như mẫu cr_11)
k.stamp('arcade5b', 97, 45)
for i, x in enumerate((78, 81, 84, 87)): k.niche(x, 45, (16, 18, 20, 22)[i])          # hốc thánh tích TRỐNG Reliquary
k.stamp('arcade5', 40, 1); k.stamp('arcade5b', 63, 1)
k.stamp('shrine_a', 49, 23); k.stamp('shrine_b', 61, 23)
# ===================================================================== NGỤC (nam, y63..123)
o.zone_area(0, 63, W, H - 63)
# Z3b lối đi lên cổng đầu lâu: bệ x45..65 hàng 63..78; tường chắn hàng 64..66 chạy hết bề ngang, cổng đầu lâu x54..56
o.plat(45, 63, 23, 8); o.plat(52, 71, 9, 8)                  # sân trước cổng (rộng) + đường đá hẹp 9 ô giữa hố xương (như mẫu)
o.hwall(45, 67, 64, 'south', hole=(55, 3), join_l=True, join_r=True)
DOORS.append(('skull_gate', 'op_skullgate', 55, 64, 'cổng đầu lâu (Pell mở)'))
# kênh hố hàng 79..82 + cầu treo dọc x53..56
DOORS.append(('pit_bridge', 'op_bridge_v', 53, 79, 'cầu treo dọc qua kênh (tời ở sân lưỡi xoay)'))
# Z3a sân lưỡi xoay: x34..64, hàng 83..102
o.plat(34, 83, 31, 20)
o.plat(65, 92, 3, 4)                                        # nối sang Rolling Hall
o.spikes(40, 96, 6, 4); o.spikes(48, 96, 6, 4); o.spikes(40, 88, 4, 3); o.dirt(36, 84, 10, 6); o.dirt(55, 97, 8, 5)
# gờ xa (đoàn Black Lantern): đảo x0..29, hàng 68..73, chạy vào mép tây map
o.plat(0, 68, 30, 6)
# Z2b ROLLING HALL: x68..75, hàng 76..118; đầu bắc là tường có vòm mở (miệng hầm tối) — gỗ lăn chui ra; đầu nam là vực
o.plat(68, 76, 8, 43)
o.hwall(68, 75, 76, 'south', door=70, join_l=True, join_r=True)
o.vpost(68, 79, 116, gaps=range(92, 96)); o.vpost(75, 79, 116, gaps=range(101, 106))
# Z2 CELL ROWS: bệ x76..112, hàng 88..123; 5 xà lim (cổng trong vòm 6 ô), hành lang hàng 101..105, 3 phòng phía nam
o.plat(76, 88, 37, 36)
CELLS = [(76, 'shut'), (83, 'open'), (90, 'pell'), (97, 'open'), (104, 'shut')]
for x, kind in CELLS: o.cell(x, 88, 8, 13, arch6=x + 1)
o.cell(76, 106, 12, 17, door_n=80)                          # phòng tra tấn (vòm mở)
o.cell(87, 106, 13, 17, door_n=92)                          # phòng lính gác (cửa gỗ, khoá chìa)
o.cell(99, 106, 14, 17, door_n=101)                         # OUBLIETTE (cửa gỗ ra hành lang)
o.vpost(104, 109, 112); o.hwall(104, 112, 113, 'south', arch6=105, join_l=True, join_r=True)   # xà lim của Ren (trong x105..111, hàng 109..112)
DOORS += [('guard_door', 'op_door_h', 92, 106, 'cửa gỗ phòng gác'), ('oub_door', 'op_door_h', 101, 106, 'cửa gỗ Oubliette')]
o.dirt(78, 114, 8, 5); o.dirt(89, 112, 7, 4); o.dirt(80, 102, 20, 3)
k.build(); o.build()
for (x, y), v in k.deco.items(): k.L['deco'][y][x] = v
ORDER = o.ORDER[:5] + k.ORDER[:8] + o.ORDER[5:] + k.ORDER[8:]
