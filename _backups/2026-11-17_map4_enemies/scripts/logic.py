# Map 4 — BƯỚC 3 PHẦN 1 (§304): object LOGIC — khu (zone), điểm nghỉ, chỗ tỉnh dậy, cổng có điều kiện mở, tời, xích, chìa, 2 ấn + ổ ấn,
# lối ra Map 5. Toạ độ ô (32 px), y xuống. Chữ trong game: tiếng Anh. Gọi SAU decor2 (write_real.py import).
# Cảnh cốt truyện thật (rung chấn, Pell, ghi chép) là PHẦN 3: ở đây chỉ có "cảnh nối" tối thiểu để chuỗi cờ chạy được và test được.
# Chuỗi cờ: m4_tremor (rung chấn) -> ren_gate mở -> oub_unbarred (nhấc then) -> oub_door -> chìa warden_key (phòng tra tấn) -> guard_door
#   -> m4_katana (lấy lại katana) -> chém xích pell_chain_cut -> quay tời pell_free -> pell_gate mở -> tời cầu pit_bridge_down -> cầu hạ
#   -> skull_gate_open (Pell, tạm) -> Ossuary -> c2_cleared / c4_cleared (cửa phòng phụ + nhặt ấn) -> seal1_set + seal2_set -> seal_gate
#   -> walk_crossed (lên đầu Walk) -> hall_door -> severing_done (đợt sau) -> exit_map5.
from m4_setup import objs as O
import gen_frame2 as F
import decor2 as D
T = 32
BRIDGE = {(x, y) for x in (54, 55) for y in range(79, 83)}     # ô ván cầu treo: không vào blockers, cổng pit_bridge chặn khi cầu chưa hạ

def r(group, name, typ, x, y, w, h, props=(), shape=None):
    return O.obj(group, name, typ, x * T, y * T, w * T, h * T, props=sorted(props, key=lambda p: p[0]), shape=shape)
TRIG = ('unity:IsTrigger', True)
f = lambda v: (str(v), 'float')

# ===================================================================== KHU (zone): ánh nền / tiêu đề / ải (stage = nhóm hồi quái + điểm hồi sinh)
PRISON, CRYPT = '#a9b2d8', '#9fb4e8'
ZONES = [   # id, tiêu đề, x0, y0, x1, y1 (ô, gồm cả 2 mép), ánh nền, màu, ải
    ('p1', 'The Oubliette', 99, 106, 112, 123, '0.30', PRISON, 'cells'),
    ('p2', 'The Cell Rows', 76, 88, 112, 105, '0.28', PRISON, 'cells'),
    ('p3', 'The Question Room', 76, 106, 86, 123, '0.26', PRISON, 'cells'),
    ('p4', "The Warders' Room", 87, 106, 98, 123, '0.30', PRISON, 'cells'),
    ('p5', 'The Rolling Hall', 68, 76, 75, 118, '0.24', PRISON, 'pit'),
    ('p6', 'The Blade Yard', 34, 83, 64, 103, '0.26', PRISON, 'pit'),
    ('p7', 'The Bleeding Way', 44, 63, 67, 82, '0.24', '#c0a0b0', 'pit'),
    ('c1', 'The Ossuary', 38, 43, 73, 62, '0.30', CRYPT, 'ossuary'),
    ('c2', 'The Scriptorium', 4, 48, 37, 61, '0.28', CRYPT, 'scriptorium'),
    ('c3', "A Mother's Letters", 10, 26, 27, 47, '0.30', '#c8b8e0', 'scriptorium'),
    ('c4', 'The Royal Reliquary', 74, 48, 107, 61, '0.28', '#d8c8a0', 'reliquary'),
    ('c5', 'The Inventory', 84, 26, 101, 47, '0.28', '#d8c8a0', 'reliquary'),
    ('c6', "The Penitent's Walk", 48, 21, 63, 42, '0.24', CRYPT, ''),
    ('c7', 'The Hall of Naming', 40, 0, 73, 20, '0.22', '#b0c0ff', 'hall'),
]
for zid, title, x0, y0, x1, y1, amb, col, stage in ZONES:
    p = [('ambient', amb), ('ambient_color', col), ('title', title), TRIG, ('zone_id', zid)]
    if stage: p.append(('stage', stage))
    r('zones', zid, 'zone', x0, y0, x1 - x0 + 1, y1 - y0 + 1, p)

# ===================================================================== tỉnh dậy + điểm nghỉ (trước mỗi trận + hub)
r('triggers', 'player_start', 'player_start', 107.5, 110.0, 1, 1, [TRIG, ('zone_id', 'p1')])
RESTS = [   # tên, x, y, w, h, khu, ải, tên ải
    ('rest_oubliette', 104.6, 116.6, 2.4, 2.0, 'p1', 'cells', 'The Cell Rows'),
    ('rest_pit', 52.0, 85.0, 2.0, 2.0, 'p6', 'pit', 'The Blood Pit'),
    ('rest_ossuary', 42.8, 54.4, 2.4, 2.2, 'c1', 'ossuary', 'The Ossuary'),
    ('rest_scriptorium', 34.4, 54.2, 2.2, 2.6, 'c2', 'scriptorium', 'The Scriptorium'),
    ('rest_reliquary', 75.4, 54.2, 2.2, 2.6, 'c4', 'reliquary', 'The Royal Reliquary'),
    ('rest_walk', 52.0, 27.0, 2.0, 2.0, 'c6', 'hall', 'The Hall of Naming'),
]
for name, x, y, w, h, zid, stage, sname in RESTS:
    r('triggers', name, 'checkpoint', x, y, w, h, [('marker', True), ('stage', stage), ('stage_name', sname), TRIG, ('zone_id', zid)])
D.candles(104.6 + 1.2, 116.5, 3, 'rest_oub_c')          # nến điểm nghỉ (hub Ossuary đã có ghế + nến)
D.candles(53.0, 84.9, 2, 'rest_pit_c'); D.candles(35.5, 54.1, 2, 'rest_scr_c'); D.candles(76.5, 54.1, 2, 'rest_rel_c'); D.candles(53.0, 26.9, 2, 'rest_walk_c')

# ===================================================================== CỔNG: collider `gate` (cùng tên hình) — điều kiện mở
GATES = [   # tên, x, y, w, h (ô bị chặn khi ĐÓNG), thuộc tính
    ('ren_gate', 107, 113, 2, 3, [('unlock_when', 'm4_tremor')]),
    ('oub_door', 102, 106, 2, 3, [('unlock_when', 'oub_unbarred')]),
    ('guard_door', 93, 106, 2, 3, [('requires_key', 'warden_key'), ('locked_say', "Locked. The warders kept their key close - somewhere they could reach it.")]),
    ('pell_gate', 93, 98, 2, 3, [('unlock_when', 'pell_free')]),
    ('pit_bridge', 54, 79, 2, 4, [('unlock_when', 'pit_bridge_down')]),
    ('skull_gate', 55, 64, 3, 3, [('unlock_when', 'skull_gate_open')]),
    ('seal_gate', 55, 44, 3, 4, [('unlock_when', 'seal1_set,seal2_set')]),
    ('letters_door', 19, 44, 2, 4, [('zone_id', 'c2')]),            # mở khi dọn xong Scriptorium
    ('vault_door', 92, 44, 2, 4, [('zone_id', 'c4')]),              # mở khi dọn xong Reliquary
    ('hall_door', 55, 22, 2, 4, [('unlock_when', 'walk_crossed')]),
    ('exit_map5', 56, 1, 2, 3, [('unlock_when', 'severing_done')]),  # sau The Severing (đợt sau)
]
for name, x, y, w, h, extra in GATES:
    r('gates', name, 'gate', x, y, w, h, [('collider_kind', 'gate'), ('starts_closed', True)] + extra)
for x, kind in F.CELLS:                                   # xà lim đóng chết: chặn vĩnh viễn ở lòng vòm
    if kind == 'shut': r('obstacle colliders', 'cell_%d_gate' % x, 'obstacle', x + 3, 98, 2, 3, [('collider_kind', 'gate')])

# ===================================================================== TỜI, XÍCH, CHÌA, KATANA
r('mechanics', 'pell_chain', 'breakable', 95.2, 102.0, 1.6, 1.4, [
    ('hint', 'Chain is wound through the gear and padlocked to the floor ring.'), ('hits', 2), ('mechanic', 'breakable'),
    ('requires', 'm4_katana'), ('locked_text', 'Iron chain, wound tight. Bare hands will not do it.'),
    ('sets', 'pell_chain_cut'), ('text', 'The chain parts and slithers off the gear.'), TRIG])
r('mechanics', 'pell_winch', 'lever', 95.5, 102.2, 1.0, 1.0, [
    ('hint', 'A gear winch for the cell gates.'), ('locked_say', "Chained. It won't turn while that's wound through it."),
    ('mechanic', 'lever'), ('range', f(0.9)), ('requires', 'pell_chain_cut'), ('sets', 'pell_free'), ('sprite', 'trap_gear'),
    ('text', 'The gear turns. Across the corridor, a cell gate grinds upward.'), TRIG, ('verb', 'Turn')])
r('mechanics', 'bridge_winch', 'lever', 57.1, 83.6, 1.0, 1.0, [
    ('hint', 'A winch on the near bank. Its chains run out over the pit.'), ('mechanic', 'lever'), ('range', f(0.9)),
    ('sets', 'pit_bridge_down'), ('sprite', 'trap_gear'), ('text', 'Chains clatter. The bridge rises out of the pit and settles across it.'),
    TRIG, ('verb', 'Turn')])
r('triggers', 'warden_key', 'pickup', 83.6, 109.0, 1.2, 1.0, [
    ('hint_text', 'An iron key, hung where the questioners could reach it.'), ('item', 'key'),
    ('key_desc', 'A heavy iron key on a ring, hung beside the chains in the question room.'), ('key_icon', 'key_pump'), ('key_id', 'warden_key'),
    ('key_name', "The Warders' Key"), ('key_opens', "The warders' room - Cell Rows"), ('marker', True), TRIG, ('zone_id', 'p3')])
r('triggers', 'guard_katana', 'pickup', 92.3, 113.9, 1.4, 1.0, [   # PHẦN 3: trả lại đúng thanh katana bị tịch thu (code WeaponRing)
    ('flag', 'm4_katana'), ('hint_text', "Your katana - thrown on the warders' table like a trophy. You take it back."), ('item', 'flag'),
    ('marker', True), ('say', 'Mine.'), TRIG, ('zone_id', 'p4')])

# ===================================================================== 2 ẤN + ổ ấn trên bệ thờ Ossuary
SEALS = [('seal1', 'seal_scribe', "The Scribe's Seal", 13.0, 50.4, 'c2', 'The Registrar kept it on the lectern beside the Name Register.'),
         ('seal2', 'seal_reliquary', 'The Reliquary Seal', 101.8, 50.4, 'c4', 'It was set on the empty plinth like the last relic in the room.')]
for i, (name, kid, kname, x, y, zid, desc) in enumerate(SEALS, 1):
    r('triggers', name, 'pickup', x, y, 1.2, 0.9, [
        ('hint_text', kname + '.'), ('item', 'key'), ('key_desc', desc), ('key_id', kid), ('key_name', kname),
        ('key_opens', 'The sealed gate behind the Ossuary altar'), ('locked_text', 'Its keeper still stands guard over it.'),
        ('hide', name + '_item'), ('marker', True), ('requires', zid + '_cleared'), TRIG, ('zone_id', zid)])
    sx = (52.6, 60.4)[i - 1]
    r('triggers', 'socket%d_look' % i, 'story', sx - 0.7, 51.6, 1.4, 1.0, [
        ('range', f(1.0)), ('repeat', True), ('script', 'bubble ren: An empty socket, cut to the shape of a seal.'), TRIG,
        ('trigger', 'interact'), ('unless', 'key_%s,seal%d_set' % (kid, i)), ('verb', 'Look')])
    r('triggers', 'socket%d_set' % i, 'story', sx - 0.7, 51.6, 1.4, 1.0, [
        ('range', f(1.0)), ('script', 'set seal%d_set\\nshake 0.02 0.4\\nhint The seal sinks into the stone and locks.' % i), TRIG,
        ('trigger', 'interact'), ('unless', 'seal%d_set' % i), ('verb', 'Set the seal'), ('when', 'key_' + kid)])

# §305: HÌNH ẤN — nằm trên bệ (ẩn khi nhặt: `hide`), hiện trong ổ trên bệ thờ khi đặt (toggle theo cờ seal*_set)
from m4_setup import AID
for i, (art, px_, by_) in enumerate((('seal_scribe', 13.6, 50.0), ('seal_reliquary', 102.4, 50.0)), 1):
    w_, h_ = O.ts['art'].sizes[AID[art]]
    O.obj('props', 'seal%d_item' % i, None, px_ * T - w_ / 2, by_ * T, w_, h_, gid=O.gid('art', AID[art]))
    sx = (52.6, 60.4)[i - 1]
    O.obj('props', 'seal%d_in' % i, None, sx * T - w_ / 2, 51.35 * T, w_, h_, gid=O.gid('art', AID[art]))
    r('mechanics', 'seal%d_show' % i, 'toggle', sx - 0.5, 50.6, 1, 1, [('mechanic', 'toggle'), ('show_objects', 'seal%d_in' % i), TRIG, ('when', 'seal%d_set' % i)])

# ===================================================================== CẢNH NỐI TẠM (PHẦN 3 thay bằng cảnh thật)
# LƯU Ý: `once` của StoryScenes là TÊN CỜ (mặc định scene_<tên>), không phải bool -> không ghi `once`.
# §306: cảnh tỉnh dậy thật nằm ở story.py (m4_wake)
r('triggers', 'oub_bar', 'story', 101.8, 108.9, 2.4, 1.2, [    # then cửa Oubliette cài từ phía trong
    ('range', f(1.0)), ('script', 'set oub_unbarred\\nhint The bar lifts. The door was only barred from this side.'), TRIG,
    ('trigger', 'interact'), ('unless', 'oub_unbarred'), ('verb', 'Lift the bar'), ('when', 'm4_tremor')])
# §306: Pell mở cổng đầu lâu = cảnh skull_open trong story.py
r('triggers', 'walk_top', 'story', 53.0, 26.0, 6.0, 1.6, [     # lên tới đầu Walk -> cửa Hall mở
    ('script', 'set walk_crossed\\nhint Far above, a bolt slides back.'), TRIG, ('trigger', 'enter'), ('unless', 'walk_crossed')])

# ===================================================================== LỐI RA Map 5 (sau cửa chuyển màn; Map 5 chưa có scene -> báo "chưa dựng")
r('triggers', 'to_map5', 'exit', 56.0, 0.0, 2.0, 1.0, [
    ('depart_text', 'Beyond the last door, the stair runs down into the dark.'), ('interact', True), ('locked_text', 'The door will not answer you yet.'),
    ('range', f(0.9)), ('requires', 'severing_done'), ('target', 'map5'), TRIG, ('verb', 'Go on')])
