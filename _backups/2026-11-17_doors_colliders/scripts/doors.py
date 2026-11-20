# Map 4 — đặt HÌNH CỬA / CỔNG đúng khớp tường (quy tắc §298, đã thử khớp từng loại). Mỗi hàm trả về toạ độ px góc trên-trái khung.
T = 32
def place(kind, x, top):
    if kind.startswith('cr_metal') or kind.startswith('cr_wood'):
        return x * T, top * T                         # khung 128x117, hình đặc 4 ô x 3 hàng ở MÉP TRÊN = mặt gạch
    if kind == 'cr_bossdoor': return x * T, top * T   # 96x96 = 3x3 mặt gạch
    if kind == 'op_skullgate': return x * T, top * T  # hình 96x96 (có sẵn tường) ở mép trên khung 96x118
    if kind.startswith('op_cellgate'): return (x + 1) * T, top * T - 63 + 4     # x = đầu đoạn vòm 6 ô
    if kind.startswith('op_door_h'): return (x + 1.5) * T - 47, (top + 3) * T - 110   # giữa vòm 3 ô, đáy cánh = chân tường
    if kind.startswith('op_door_v'): return x * T - 1, (top + 3) * T + 9 - 128          # x = cột trụ, top = hàng đầu khe
    if kind.startswith('op_bridge_v'): return 55 * T - 65, top * T - 22                 # ván (lúc nâng) phủ hàng top..top+3
    raise KeyError(kind)
def put_all(O, AID, doors, cells_kind=None):
    """doors: (tên, ảnh, x, top, ghi chú). Ảnh xem trước dùng khung ĐÓNG; cổng 'open' dùng khung MỞ."""
    for name, art, x, top, _ in doors:
        px, py = place(art, x, top)
        w, h = O.ts['art'].sizes[AID[art]]
        grp = 'gates' if not art.startswith('op_bridge') else 'mechanics_art'
        O.obj(grp, name, 'gate_visual' if grp == 'gates' else None, px, py + h, w, h, gid=O.gid('art', AID[art]))
