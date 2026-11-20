# Map 4 — đặt HÌNH CỬA / CỔNG đúng khớp tường (quy tắc §298, đã thử khớp từng loại). Mỗi hàm trả về toạ độ px góc trên-trái khung.
T = 32
def place(kind, x, top):
    if kind.startswith('cr_metal') or kind.startswith('cr_wood'):
        return x * T, top * T                         # khung 128x117, hình đặc 4 ô x 3 hàng ở MÉP TRÊN = mặt gạch
    if kind == 'cr_bossdoor': return x * T - 16, top * T   # §303: khung THẬT 128x96 (dải 30 khung, không phải 40) = 4 ô x 3 hàng mặt gạch;
                                                          #   x = đầu lối 3 ô phía sau -> khung lùi 16px để tâm vòm = tâm lối (x+1.5), che nửa ô mép 2 bên
    if kind == 'op_skullgate': return x * T, top * T  # hình 96x96 (có sẵn tường) ở mép trên khung 96x118
    if kind.startswith('op_cellgate'): return (x + 1) * T, top * T - 63 + 4     # x = đầu đoạn vòm 6 ô
    if kind.startswith('op_door_h'): return (x + 2) * T - 47, (top + 3) * T - 110     # §303: tâm cánh (px 47) = tâm lòng vòm 4 ô (x+2), đáy cánh = chân tường
    if kind.startswith('op_door_v'): return x * T - 1, (top + 3) * T + 9 - 128          # x = cột trụ, top = hàng đầu khe
    if kind.startswith('op_bridge_v'): return 55 * T - 65, top * T - 22                 # ván (lúc nâng) phủ hàng top..top+3
    raise KeyError(kind)
# §304: cổng có logic -> hình `gate_visual` + `anim` = dải Resources/VFX/m4_<ảnh> (gate_strips.py, khung 0 = ảnh Tiled = ĐÓNG).
ANIM = {'cr_metal2': 'm4_cr_metal2', 'cr_metal3': 'm4_cr_metal3', 'cr_metal5': 'm4_cr_metal5', 'cr_wood1': 'm4_cr_wood1',
        'cr_bossdoor': 'm4_cr_bossdoor', 'op_cellgate1': 'm4_op_cellgate1', 'op_door_h': 'm4_op_door_h',
        'op_skullgate': 'm4_op_skullgate', 'op_bridge_v': 'm4_op_bridge_v'}
FPS = {'cr_bossdoor': 16, 'op_cellgate1': 16, 'op_bridge_v': 12}
def put_all(O, AID, doors, cells_kind=None):
    """doors: (tên, ảnh, x, top, ghi chú). Xà lim `cell_*` (mở sẵn / đóng chết) = hình tĩnh lớp props; còn lại = hình cổng có hoạt ảnh.
    Cầu treo nằm lớp 'gates_floor' (ghi ra tên 'gates', thứ tự vẽ dưới nhân vật — người đứng trên ván không bị ván che)."""
    for name, art, x, top, _ in doors:
        px, py = place(art, x, top)
        w, h = O.ts['art'].sizes[AID[art]]
        if name.startswith('cell_'):
            O.obj('props', name, None, px, py + h, w, h, gid=O.gid('art', AID[art])); continue
        grp = 'gates_floor' if art.startswith('op_bridge') else 'gates'
        O.obj(grp, name, 'gate_visual', px, py + h, w, h, gid=O.gid('art', AID[art]),
              props=[('anim', ANIM[art]), ('anim_fps', (str(FPS.get(art, 14)), 'float'))])
