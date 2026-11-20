# Map 4 — BƯỚC 3 PHẦN 4 (§307): ĐIỂM SINH QUÁI + vùng trận boss. Toạ độ ô (điểm chân). Gọi sau story.py.
# Phân bố (người dùng chốt §296): Oubliette + nửa đầu Cell Rows không quái (chưa katana); cầm katana -> 3 Bone Jailer ập vào;
# Blood Pit: Pit Bat + Cutthroat; Ossuary an toàn; Scriptorium: Cantor + Cutthroat + The Registrar (Ấn 1);
# Reliquary: Warden + Bone Jailer, Lantern Knight giữ Ấn 2; Penitent's Walk chỉ bẫy; Hall: boss đợt sau.
from m4_setup import objs as O
T = 32
SPAWNS = [   # khu, quái, x, y, đợt, thêm
    ('p2', 'Bone_Jailer', 82.0, 103.2, 1, [('provoke', True)]),
    ('p2', 'Bone_Jailer', 90.0, 103.6, 1, [('provoke', True)]),
    ('p2', 'Bone_Jailer', 100.0, 103.2, 1, [('provoke', True)]),
    ('p6', 'Pit_Bat', 60.0, 96.0, 1, []),
    ('p6', 'Choir_Cutthroat', 47.0, 86.8, 1, []),
    ('p7', 'Pit_Bat', 49.0, 68.6, 1, []),
    ('p7', 'Pit_Bat', 64.0, 68.6, 1, []),
    ('p7', 'Choir_Cutthroat', 56.5, 72.6, 1, []),
    ('c2', 'Hollow_Cantor', 27.5, 51.6, 1, []),
    ('c2', 'Choir_Cutthroat', 17.5, 58.6, 1, []),
    ('c2', 'Choir_Cutthroat', 30.0, 58.6, 1, []),
    ('c2', 'The_Registrar', 11.5, 49.9, 2, [('boss', True)]),
    ('c4', 'Lantern_Warden', 86.0, 51.2, 1, []),
    ('c4', 'Bone_Jailer', 79.5, 58.6, 1, []),
    ('c4', 'Bone_Jailer', 95.5, 57.6, 1, []),
    ('c4', 'Lantern_Knight', 101.0, 56.6, 2, [('boss', True)]),
]
for i, (zid, enemy, x, y, wave, extra) in enumerate(SPAWNS):
    O.obj('spawns', '%s_%d' % (enemy, i), 'spawn', x * T, y * T,
          props=sorted([('enemy', enemy), ('wave', wave), ('zone_id', zid)] + extra, key=lambda p: p[0]), shape='point')
# vùng trận boss (BossIntro: bảng tên, màu, câu phụ)
BOSS = [('registrar_fight', 4, 48, 13, 8, 'c2', '#5a6b8f', 'Keeper of the Register of Names'),
        ('reliquary_knight_fight', 94, 48, 14, 13, 'c4', '#8f6b3a', 'Sworn to the Lantern')]
for name, x, y, w, h, zid, col, sub in BOSS:
    O.obj('triggers', name, 'boss', x * T, y * T, w * T, h * T, props=sorted([('card_color', col), ('card_sub', sub), ('entrance', 'turn'),
          ('unity:IsTrigger', True), ('zone_id', zid)], key=lambda p: p[0]))
