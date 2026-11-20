# Map 4 — BƯỚC 3 PHẦN 2 (§305): BẪY bằng cơ chế có sẵn + sàn sập (cơ chế mới `pit`). Toạ độ ô. Gọi sau logic.py.
# Bẫy gắn vào kiến trúc thật (quy tắc §298): gỗ lăn ra từ miệng hầm tối, lưỡi xoay trên ray sàn, lỗ bắn / tượng mặt trên MẶT BỆ hoặc mặt gạch
# (hình do cơ chế tự vẽ: rect = 1 ô, mép dưới = chân hình cũ), cưa chạy rãnh sàn, gai sàn thành sóng, tấm áp lực ẩn dưới bệ thánh tích rỗng.
from m4_setup import objs as O
T = 32
TRIG = ('unity:IsTrigger', True)
f = lambda v: (str(v), 'float')
def m(name, typ, x, y, w, h, props):
    O.obj('mechanics', name, typ, x * T, y * T, w * T, h * T, props=sorted([('mechanic', typ), TRIG] + list(props), key=lambda p: p[0]))
WALK_STOP = 'walk_crossed'          # qua hết Walk lần đầu -> bẫy Walk dừng (đường về yên)

# ---------------------------------------------------------------- p5 ROLLING HALL: khúc gỗ gai lăn từ miệng hầm bắc xuống vực nam (kín làn)
m('hall_roller', 'roller', 69, 79, 6, 39, [('damage', f(22)), ('dir', 'down'), ('fit_lane', True), ('knock', f(5)), ('log_fill', f(0.75)),
  ('offset', f(0.4)), ('period', f(3.4)), ('rearm', f(0.4)), ('speed', f(7))])
O.obj('triggers', 'roll_hint', 'sign', 75 * T, 101 * T, 1 * T, 5 * T, props=[('hint_text', 'Something heavy rumbles in the dark at the top of the hall. Cross between the rolls.'), TRIG, ('zone_id', 'p5')])

# ---------------------------------------------------------------- p6 BLADE YARD: 3 lưỡi xoay trên ray (2 ray dọc cạnh cầu, 1 ray ngang giữa 2 hố gai)
for i, (x, y, w, h, off) in enumerate(((57, 85, 1, 15, 0.0), (61, 85, 1, 15, 0.5), (38, 92, 18, 1, 0.25))):
    m('yard_blade_%d' % i, 'blade', x, y, w, h, [('damage', f(16)), ('kind', 'blades'), ('knock', f(3)), ('offset', f(off)), ('pause', f(0.3)), ('speed', f(2.2))])

# ---------------------------------------------------------------- c2 SCRIPTORIUM: 2 ống phun BĂNG trên mặt bệ tây bắc, kẹp bậc lên bệ (Ấn 1)
for i, cx in enumerate((5.5, 14.5)):
    m('scr_ice_%d' % i, 'launcher', cx - 0.5, 53.8, 1, 1, [('chill', f(0.55)), ('chill_seconds', f(2)), ('damage', f(10)), ('dir', 'down'), ('kind', 'ice'),
      ('mode', 'cycle'), ('offset', f(i * 1.3)), ('period', f(2.6)), ('range', f(8)), ('warn_seconds', f(0.6))])

# ---------------------------------------------------------------- c4 ROYAL RELIQUARY: giẫm tấm ẩn trước bệ thánh tích rỗng -> 3 lỗ mặt gạch bắc bắn loạt tên
for i, cx in enumerate((80.5, 83.5, 86.5)):
    m('rel_arrow_%d' % i, 'launcher', cx - 0.5, 46.7, 1, 1, [('burst', 3), ('burst_gap', f(0.18)), ('damage', f(14)), ('dir', 'down'), ('kind', 'arrow'),
      ('mode', 'trigger'), ('range', f(15)), ('speed', f(10)), ('trigger', 'rel_volley'), ('warn_seconds', f(0.35))])
for r_, by in enumerate((53.6, 56.8, 59.8)):
    for cx in (81.0, 84.5, 88.0, 91.5):
        m('rel_plate_%d_%d' % (int(cx * 10), r_), 'plate', cx - 1.0, by, 2.0, 0.9, [('by', 'any'), ('delay', f(0.1)), ('fires', 'rel_volley'), ('rearm', f(2.5))])
# sàn sập trước cửa kho: 2 tấm 2x2 x91..94 (tâm 93 = tâm cửa), hàng 48..49 — chừa cột 95 (cọc lan can bệ đông bắc); rơi -> về giữa phòng
m('vault_pit', 'pit', 91, 48, 4, 2, [('damage', f(14)), ('hint', 'The flags before the door ring hollow underfoot.'), ('open_seconds', f(2.5)),
  ('return_at', '93.5,51.5'), ('size', 2), ('text', 'The floor gives way. You haul yourself out of the rubble.'), ('warn_seconds', f(0.45))])

# ---------------------------------------------------------------- c6 PENITENT'S WALK (đi từ cổng 2 ấn hàng 42 lên bệ cao hàng 26..30)
m('walk_spikes', 'spikes', 49, 41, 14, 2, [('damage', f(14)), ('mode', 'cycle'), ('off_seconds', f(1.4)), ('on_seconds', f(0.8)), ('stop_when', WALK_STOP),
  ('style', 1), ('wave_axis', 'x'), ('wave_step', f(0.08)), ('warn_seconds', f(0.5))])
for i, (y, off) in enumerate(((36, 0.0), (39, 0.5))):
    m('walk_saw_%d' % i, 'blade', 49, y, 14, 1, [('damage', f(14)), ('kind', 'saw'), ('offset', f(off)), ('pause', f(0.3)), ('speed', f(3)), ('stop_when', WALK_STOP)])
for i, (cx, kind, per, off) in enumerate(((49.5, 'fire', 3.0, 0.0), (62.5, 'fire', 3.0, 1.5), (51.5, 'arrow', 2.2, 0.5), (60.5, 'arrow', 2.2, 1.6))):
    m('walk_shot_%d' % i, 'launcher', cx - 0.5, 33.9, 1, 1, [('damage', f(16 if kind == 'fire' else 12)), ('dir', 'down'), ('kind', kind), ('mode', 'cycle'),
      ('offset', f(off)), ('period', f(per)), ('range', f(10)), ('stop_when', WALK_STOP), ('warn_seconds', f(0.6))] + ([('flip', True)] if cx > 56 else []))
O.obj('triggers', 'walk_hint', 'sign', 55 * T, 42.4 * T, 3 * T, 1 * T, props=[('hint_text', 'The iron rises in a wave. Wait at the gate, then step in behind it.'), TRIG, ('zone_id', 'c6')])
