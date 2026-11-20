# Map 2b — The Kroven Deep: mọi object (khu, lối ra, bẫy, boss, ghi chép, đồ trang trí theo cụm). gen_map.py gọi build(O, groups).
# Toạ độ ô (32 px), y hướng xuống. Chữ trong game: tiếng Anh.
import xml.etree.ElementTree as ET
from grid import W, H, FLOOR, ROOMS
T = 32
TS_DIR = "../../Assets/GamePackage/EPIC RPG World - Sewers V1.5/TiledMap Editor/tilesets/"
STOP = 'g4_cleared'            # người gác ngã -> mọi bẫy dừng (đường về yên)

# ---- kích thước tile theo gid (đọc tsx của Map 2)
import tmxio, re
SIZE = {}
for line in tmxio.tileset_lines():
    m = re.search(r'firstgid="(\d+)" source="([^"]+)"', line)
    first, src = int(m.group(1)), m.group(2).replace('&amp;', '&')
    path = "../../Assets/GamePackage/EPIC RPG World - Sewers V1.5/TiledMap Editor/Maps/" + src
    try: r = ET.parse(path).getroot()
    except Exception: continue
    tw, th = int(r.get('tilewidth')), int(r.get('tileheight'))
    tiles = r.findall('tile')
    if r.find('image') is not None:
        n = int(r.get('tilecount'))
        for i in range(n): SIZE[first + i] = (tw, th)
    for t in tiles:
        im = t.find('image')
        if im is not None: SIZE[first + int(t.get('id'))] = (int(im.get('width')), int(im.get('height')))

def build(O, G):
    D, WP, PR, ME, MA, TR, Z, SP, GA, OB = (G[k] for k in ['ground decals', 'wall props', 'props', 'mechanics', 'mechanics_art', 'triggers', 'zones', 'spawns', 'gates', 'obstacle colliders'])
    IRON = G['_ironcrown_gid']

    def tile(lst, gid, cx, by, flip=False, name=None, typ=None, props=None):
        w, h = SIZE.get(gid & 0x1fffffff, (32, 32))
        lst.extend(O.obj(name, typ, cx * T - w / 2, by * T, w, h, gid=gid | (0x80000000 if flip else 0), props=props))
        return w, h
    def obstacle(x, y, w, h):
        OB.extend(O.obj('prop collider', 'obstacle', x * T, y * T, w * T, h * T, props=[('collider_kind', 'prop')]))
    def light(name, cx, cy, color, radius, inten=0.8, anim=None, amount=None, speed=None, off_when=None):
        p = [('color', color), ('light', 'torch'), ('mechanic', 'light'), ('radius', (str(radius), 'float')), ('intensity', (str(inten), 'float')), ('unity:IsTrigger', True)]
        if anim: p += [('anim', anim), ('anim_amount', (str(amount), 'float')), ('anim_speed', (str(speed), 'float'))]
        if off_when: p.append(('off_when', off_when))
        ME.extend(O.obj(name, 'light', cx * T - 16, cy * T - 16, 32, 32, props=p))
    def note(name, x, y, w, h, title, text, verb='Read', extra=()):
        ME.extend(O.obj(name, 'note', x * T, y * T, w * T, h * T, props=[('mechanic', 'note'), ('range', ('1.2', 'float')), ('title', title),
                  ('text', text), ('verb', verb), ('unity:IsTrigger', True)] + list(extra)))
    def torch(cx, face_y):
        tile(WP, 13959, cx, face_y)
        light('torch_%d_%d' % (int(cx * 10), int(face_y * 10)), cx, face_y + 0.4, '#ffb070', 3.0, 0.75, 'flicker', 0.2, 0.9)

    # ================================================================== khu (zone): map bí mật — không sương, tuỳ chọn
    def zone(zid, title, x0, y0, x1, y1, amb, col, extra=()):
        Z.extend(O.obj(zid, 'zone', x0 * T, y0 * T, (x1 - x0 + 1) * T, (y1 - y0 + 1) * T, props=[
            ('ambient', amb), ('ambient_color', col), ('fog', 'off'), ('hint_text', ''), ('optional', True), ('title', title),
            ('unity:IsTrigger', True), ('zone_id', zid)] + list(extra)))
    zone('k0', 'The Cistern Stair', 3, 32, 14, 37, '0.30', '#9fb8b0')
    zone('k1', 'The Gatehouse', 15, 31, 28, 38, '0.24', '#a8b0a0')
    zone('k2', 'The Rolling Hall', 22, 16, 44, 30, '0.22', '#9aa8a0')
    zone('k3', 'The Sluice Walk', 25, 3, 50, 15, '0.22', '#8fb0b8')
    zone('g4', "The Guard's Post", 10, 3, 24, 11, '0.26', '#9cc0a0')
    zone('k5', "Nolan's Rest", 12, 12, 19, 21, '0.28', '#b8b4a0')

    # ================================================================== Landing: thang lên Forgotten Store, điểm nghỉ
    TR.extend(O.obj('player_start', 'player_start', 6 * T, 34.5 * T, T, T, props=[('unity:IsTrigger', True), ('zone_id', 'k0')]))
    tile(WP, 14632, 6.5, 32.15)                                            # thang bảo trì bám vách bắc
    tile(PR, 13951, 6.5, 33.6)                                             # ánh sáng lọt qua nắp hầm
    light('stair_daylight', 6.5, 32.8, '#c8e0d8', 2.6, 0.6)
    TR.extend(O.obj('to_store', 'exit', 5.6 * T, 31.7 * T, 1.8 * T, 0.9 * T, props=sorted([
        ('arrive_at', '25,35.6'), ('arrive_subtitle', 'The Undercity of Vael'), ('arrive_title', 'Forgotten Store'),
        ('depart_text', 'You climb the old ladder back toward the lamp-smell of the Undercity.'), ('interact', True), ('quake', False),
        ('range', ('0.8', 'float')), ('target', 'map2_undercity_of_vael'), ('unity:IsTrigger', True), ('verb', 'Climb up')])))
    TR.extend(O.obj('stair_rest', 'checkpoint', 3.6 * T, 35.2 * T, 2.4 * T, 1.8 * T, props=[('marker', True), ('unity:IsTrigger', True), ('zone_id', 'k0')]))
    # cụm kể chuyện: lối xuống đầu tiên — dấu khắc của cận vệ + bộ hài cốt ngồi tựa vách
    tile(D, 14340, 8.2, 33.4); tile(D, 14271, 8.8, 34.3)
    tile(PR, 14324, 8.6, 33.2); obstacle(8.2, 32.9, 0.8, 0.3)
    note('stair_carving', 7.7, 31.6, 1.6, 1.8, 'Cut Above the Stair',
         "Square letters, cut deep, in the old tongue of the north. Someone has scratched a plainer version beneath.\n\nKROVEN IS CROWNED WITH A SWORD.\nWHAT THE KING CANNOT KEEP, THE GUARD KEEPS.\n\nBeneath, in a shakier hand: We kept it. The king did not come down with us.")

    # ================================================================== Gatehouse (R1): rèm tên từ vách bắc (nhịp, chạy sóng sang đông)
    # + tấm sàn khắc vương miện: giẫm lên -> tên từ vách đông bắn dọc hàng đó
    for i, cx in enumerate(range(16, 25)):
        ME.extend(O.obj('gate_arrow_%d' % cx, 'launcher', cx * T, 30 * T, T, T, props=sorted([
            ('damage', ('14', 'float')), ('dir', 'down'), ('kind', 'arrow'), ('mechanic', 'launcher'), ('mode', 'cycle'),
            ('offset', ('%.2f' % (i * 0.2), 'float')), ('period', ('3.2', 'float')), ('range', ('9', 'float')), ('speed', ('8', 'float')),
            ('stop_when', STOP), ('unity:IsTrigger', True), ('warn_seconds', ('0.6', 'float'))])))
    for (px, py) in [(18, 35), (22, 35), (20, 37), (24, 37), (17, 33)]:
        ME.extend(O.obj('gate_plate_%d_%d' % (px, py), 'plate', px * T, py * T, 2 * T, T, props=sorted([
            ('by', 'player'), ('delay', ('0.15', 'float')), ('fires', 'row_%d' % py), ('mechanic', 'plate'), ('rearm', ('1.5', 'float')),
            ('sprite', 'trap_plate_h'), ('stop_when', STOP), ('unity:IsTrigger', True)])))
    for ry in (33, 35, 37):
        ME.extend(O.obj('gate_side_%d' % ry, 'launcher', 29 * T, ry * T, T, T, props=sorted([
            ('burst', 2), ('burst_gap', ('0.25', 'float')), ('damage', ('16', 'float')), ('dir', 'left'), ('kind', 'arrow'), ('mechanic', 'launcher'),
            ('mode', 'trigger'), ('range', ('15', 'float')), ('speed', ('9', 'float')), ('stop_when', STOP), ('trigger', 'row_%d' % ry),
            ('unity:IsTrigger', True), ('warn_seconds', ('0.35', 'float'))])))
    # cụm kể chuyện: người lính gác cổng chết dưới làn tên — tên cắm quanh, khiên vỡ
    tile(D, 14352, 21.2, 32.6); tile(D, 14252, 22.4, 32.4); tile(D, 14356, 20.4, 32.1); tile(D, 14339, 23.3, 32.0)
    note('gate_bones', 20.4, 31.4, 2.2, 1.4, 'A Gate-Warden of Kroven',
         "Armour gone green, a short sword still in the hand. The arrows around him are his own side's - fletched in Kroven grey.\n\nHe set the curtain himself, then walked into it so no one would have to come back and reset it.")
    torch(15.6, 30.0); torch(27.6, 30.0)

    # ================================================================== Rolling Hall (R2): trụ gai lăn từ đầu đông sang tây, núp vào hốc vách bắc
    x0, y0, x1, y1 = ROOMS['R2']
    ME.extend(O.obj('hall_roller', 'roller', x0 * T, y0 * T, (x1 - x0 + 1) * T, (y1 - y0 + 1) * T, props=sorted([
        ('damage', ('22', 'float')), ('dir', 'left'), ('knock', ('5', 'float')), ('mechanic', 'roller'), ('offset', ('1.5', 'float')),
        ('period', ('4.4', 'float')), ('rearm', ('2', 'float')), ('speed', ('6', 'float')), ('stop_when', STOP), ('unity:IsTrigger', True)])))
    # cụm kể chuyện: kẻ trộm không kịp núp (hài cốt bẹp, đuốc rơi) ngay giữa làn; vết lăn trên sàn
    tile(D, 14326, 31.0, 21.3); tile(D, 14324 , 37.6, 20.9, True); tile(D, 14168, 36.5, 21.5)
    note('hall_thief', 36.4, 19.9, 2.2, 1.4, 'Flattened Bones',
         "Not Kroven. A Miravel buckle, a sack of nothing, a crowbar.\n\nWhoever came looking for the King's sword after the war learned what the alcoves in this hall are for. Too late.")
    tile(WP, 14266, 28.0, 15.2); tile(WP, 14266, 34.0, 15.2)              # cờ hiệu rách trong hốc tường
    light('alcove_a', 28.0, 16.6, '#b0c8b8', 1.6, 0.5); light('alcove_b', 34.0, 16.6, '#b0c8b8', 1.6, 0.5)
    torch(23.0, 17.0); torch(44.0, 17.0)

    # ================================================================== Sluice Walk (R3): gai sàn chạy thành sóng về tây + lưỡi cưa trên ray dọc
    ME.extend(O.obj('sluice_spikes', 'spikes', 33 * T, 3 * T, 8 * T, 7 * T, props=sorted([
        ('damage', ('12', 'float')), ('mechanic', 'spikes'), ('mode', 'cycle'), ('off_seconds', ('1.4', 'float')), ('on_seconds', ('0.7', 'float')),
        ('stop_when', STOP), ('style', 2), ('unity:IsTrigger', True), ('warn_seconds', ('0.45', 'float')), ('wave_axis', '-x'), ('wave_step', ('0.22', 'float'))])))
    ME.extend(O.obj('sluice_saw', 'blade', 37 * T, 3 * T, T, 7 * T, props=sorted([
        ('damage', ('12', 'float')), ('kind', 'saw'), ('mechanic', 'blade'), ('pause', ('0.4', 'float')), ('speed', ('2.6', 'float')),
        ('stop_when', STOP), ('unity:IsTrigger', True)])))
    tile(D, 14322, 46.0, 5.0); tile(D, 14322, 46.5, 8.6)                 # nước đen đọng của bể cũ
    tile(D, 14327, 44.2, 8.6); tile(D, 14353, 45.0, 8.8)
    note('sluice_guard', 43.4, 7.6, 2.0, 1.4, 'A Guard Who Sat Down',
         "He took off his helm and set it beside him, very straight, and sat with his back to the water.\n\nScratched on the helm: NINE OF US CAME DOWN. THE SPIKES ARE FOR THEM, NOT FOR US. WE KNOW THE STEP.\n\nThe step: wait for the iron to sink, then walk behind it.")
    torch(41.0, 2.6); torch(49.0, 2.6)

    # điểm nghỉ trước trạm gác (gục ở trận boss không phải đi lại 3 phòng bẫy)
    TR.extend(O.obj('door_rest', 'checkpoint', 27.4 * T, 6.2 * T, 2.4 * T, 2.4 * T, props=[('marker', True), ('unity:IsTrigger', True), ('zone_id', 'k3')]))
    light('door_lamp', 28.6, 6.0, '#ffd0a0', 2.4, 0.6, 'flicker', 0.12, 0.7)
    # ================================================================== The Guard's Post (R4): boss
    x0, y0, x1, y1 = ROOMS['R4']
    TR.extend(O.obj('guard_fight', 'boss', x0 * T, y0 * T, (x1 - x0 + 1) * T, (y1 - y0 + 1) * T, props=sorted([
        ('card_color', '#4b6b4f'), ('card_sub', 'Sworn to King Nolan'), ('entrance', 'turn'),
        ('unity:IsTrigger', True), ('zone_id', 'g4')])))
    # cổng sắt khoá trận (mẫu r8_lock Map 2): song sắt dọc ở cột đầu hành lang C4 (x25), phủ 2 hàng mặt vách + 3 hàng sàn;
    # close_on_enter -> đóng khi Ren bước vào trạm gác, mở khi người gác ngã. Có cổng thật nên ArenaSeal KHÔNG dựng màn chắn Ether.
    GA.extend(O.obj('post_lock', 'gate', 25 * T, 4 * T, T, 5 * T, props=sorted([
        ('close_on_enter', True), ('collider_kind', 'gate'), ('starts_closed', False), ('unlock_when', STOP), ('zone_id', 'g4')])))
    for yb in (5, 6, 7, 8, 9):
        GA.extend(O.obj('post_lock', 'gate_visual', 25 * T, yb * T, 32, 32, gid=12559))
    SP.extend(O.obj('Last_Guard', 'spawn', 16.5 * T, 9.6 * T, props=sorted([('boss', True), ('enemy', 'Last_Guard'), ('stand_in', True), ('wave', 1), ('zone_id', 'g4')]), shape='point'))
    tile(WP, 14268, 14.0, 2.6); tile(WP, 14268, 19.0, 2.6)                # cờ hiệu Kroven hai bên cổng mộ
    torch(11.5, 2.6); torch(23.5, 2.6)
    tile(D, 14340, 21.0, 9.4); tile(D, 14271, 12.0, 6.0)
    # cổng xuống phòng mộ — theo mẫu r2_gate (Map 2): vòm portcullis 128x96 (gid 14234) lấp lỗ hổng 4 ô ở 3 hàng mặt vách bắc của R5
    # (hàng 13..15), đáy hình lệch 0,77 ô trên mép sàn; hai chân vòm có khối chặn riêng; khối chặn song sắt 2,31 ô ở hàng cuối. Mở khi người gác ngã.
    GA.extend(O.obj('rest_gate', 'gate', 15.84 * T, 15.0 * T, 2.31 * T, 0.73 * T, props=sorted([
        ('collider_kind', 'gate'), ('starts_closed', True), ('unlock_when', STOP), ('zone_id', 'g4')])))
    GA.extend(O.obj('rest_gate', 'gate_visual', 15.0 * T, 15.77 * T, 128, 96, gid=14234))
    PR.extend(O.obj('rest_gate_arch', None, 15.0 * T, 15.73 * T, 128, 96, gid=14233))        # vòm đá (chân cột gạch) — lớp props, như r2_gate_arch
    OB.extend(O.obj('rest_gate_arch_L', 'obstacle', 15.0 * T, 13.0 * T, 27, 96, props=[('collider_kind', 'prop')]))
    OB.extend(O.obj('rest_gate_arch_R', 'obstacle', 18.16 * T, 13.0 * T, 27, 96, props=[('collider_kind', 'prop')]))

    # ================================================================== Nolan's Rest (R5): quan tài, Ironcrown trong vỏ, sách The Rampart Forms
    tile(PR, 14358, 13.2, 19.6); obstacle(12.6, 19.1, 1.3, 0.5)          # quan tài vua Nolan — sát vách tây, không chắn lối vào từ cổng (x15.8..18.2)
    note('nolan_lid', 12.3, 17.4, 1.9, 2.3, 'The King of Kroven',
         "Carved on the lid, worn smooth by no hand but the damp:\n\nNOLAN, KING OF KROVEN. HE WAS CROWNED WITH A SWORD AND FELL HOLDING IT.\n\nHis guards brought him home in pieces, under the city that Borrow's men were burning. The sword they would not let the burners have.")
    iw, ih = 64, 14                                                    # relic_ironcrown (tileset riêng SR-relic-ironcrown)
    ME.extend(O.obj('rest_ironcrown', 'note', 13.2 * T - iw / 2, 20.75 * T - ih, iw, ih, gid=IRON, props=sorted([
        ('key_got', "Ironcrown. Below half health you take less damage and heavy blows cannot stagger you. The last cut of a combo leaves frost."),
        ('mechanic', 'note'), ('pickup', True), ('range', ('1.3', 'float')), ('sets', 'ironcrown_taken'), ('title', 'The Iron Crown of Kroven'),
        ('unity:IsTrigger', True), ('verb', 'Take the blade'), ('weapon_id', 'katana_ironcrown'),
        ('text', "A sheathed blade of cold iron, the guard worked like a narrow crown. It lies across the stone before the King, where his guard set it down.\n\nKroven crowned its kings with a sword. This one was never given to another head.")])))
    light('rest_blade_glow', 13.2, 20.6, '#b8d8ff', 2.0, 0.6, 'pulse', 0.2, 0.4, 'ironcrown_taken')
    tile(PR, 14278, 18.4, 18.6); obstacle(17.9, 18.2, 1.0, 0.35)          # giá sách
    ME.extend(O.obj('book_rampart', 'pickup', 17.9 * T, 17.4 * T, 1.2 * T, 1.2 * T, props=sorted([
        ('book', 'rampart'), ('hint_text', "The Rampart Forms. Stand your ground: after a breath without moving, you take less damage until you step again."),
        ('item', 'skill_book'), ('marker', True), ('unity:IsTrigger', True), ('zone_id', 'k5')])))
    tile(WP, 14359, 13.0, 15.6)                                           # cờ hiệu bên trái cổng (bên phải chỉ còn 1 ô vách)
    light('rest_candle', 13.4, 17.8, '#ffd8a0', 2.6, 0.7, 'flicker', 0.12, 0.6)

    # ================================================================== mốc nhiệm vụ phụ (Resources/Quests/Map2b_KrovenDeep.json)
    def mark(name, flag, x, y, w, h):
        TR.extend(O.obj(name, 'story', x * T, y * T, w * T, h * T, props=sorted([('script', 'set ' + flag), ('trigger', 'enter'), ('unity:IsTrigger', True), ('unless', flag)])))
    mark('mark_gate', 'kroven_gate', 25, 26, 3, 2)      # qua cổng tên
    mark('mark_hall', 'kroven_hall', 41, 12, 3, 2)      # qua hành lang lăn
    mark('mark_post', 'kroven_post', 25, 6, 2, 3)       # tới trạm gác

    # ================================================================== va chạm: mọi ô không phải sàn (gộp hình chữ nhật) + biên map
    solid = [[(x, y) not in FLOOR for x in range(W)] for y in range(H)]
    used = [[False] * W for _ in range(H)]
    for y in range(H):
        for x in range(W):
            if not solid[y][x] or used[y][x]: continue
            w = 1
            while x + w < W and solid[y][x + w] and not used[y][x + w]: w += 1
            h = 1
            while y + h < H and all(solid[y + h][x + k] and not used[y + h][x + k] for k in range(w)): h += 1
            for yy in range(y, y + h):
                for xx in range(x, x + w): used[yy][xx] = True
            G['blockers'].extend(O.obj('blocker', 'blocker', x * T, y * T, w * T, h * T, props=[('collider_kind', 'terrain')]))
    for (x, y, w, h) in [(-T, -T, W * T + 2 * T, T), (-T, H * T, W * T + 2 * T, T), (-T, 0, T, H * T), (W * T, 0, T, H * T)]:
        G['map bounds'].extend(O.obj('map_bound', 'bound', x, y, w, h, props=[('collider_kind', 'bound')]))
