# Map 4 (§304) — dải hoạt ảnh cổng cho GateAnimator: Resources/VFX/m4_<ảnh>.png (+ .meta Sprite Multiple, pivot dưới-trái, PPU 100).
# Khung GIỮ NGUYÊN KÍCH THƯỚC như ảnh art/<ảnh>.png đặt trong Tiled (KHÔNG xén) -> GateAnimator khớp mép trên-trái là trùng khít.
# Khung 0 = ĐÓNG (đúng ảnh trong Tiled), khung cuối = MỞ. Chạy lại an toàn (giữ guid .meta cũ).
import os, json, re, subprocess, sys
from pathlib import Path
from PIL import Image
HOME = Path.home() / "mnt"
GP = HOME / "Soul Requiem/Assets/GamePackage"
A = GP / "EPIC RPG World Pack - Crypt V.1.6/Props/animated/doors"
OP = GP / "EPIC RPG World Pack - Old Prison V1.7.1/Props"
VFX = HOME / "Soul Requiem/Assets/Resources/VFX"
ART = Path(__file__).resolve().parent / "art"
SPEC = {   # tên ảnh Tiled: (nguồn, rộng khung, số khung)
    'cr_metal2': (A / "doors-metal-door frame 2-opening.png", 128, 9),
    'cr_metal3': (A / "doors-metal-door frame 3-opening.png", 128, 9),
    'cr_metal5': (A / "doors-metal-door frame 5-opening.png", 128, 9),
    'cr_wood1': (A / "doors-wood-door frame 1-opening.png", 128, 9),
    'cr_bossdoor': (A / "Boss door-opening.png", 128, 30),                     # §303: 30 khung 128x96
    'op_cellgate1': (OP / "cell gate1 - openning1.png", 128, 23),
    'op_door_h': (OP / "Wooden Doors/Wooden door1/horizontal-wooden door - opening animation.png", 96, 9),
    'op_skullgate': (OP / "Prison Gate-with wall.png", 96, 18),
}
man = {}
for name, (src, fw, n) in SPEC.items():
    im = Image.open(src).convert('RGBA'); fh = im.height
    assert im.width == fw * n, (name, im.size)
    f0 = im.crop((0, 0, fw, fh))
    assert f0.tobytes() == Image.open(ART / (name + '.png')).convert('RGBA').tobytes(), name + ': khung 0 khác ảnh Tiled'
    im.save(VFX / ('m4_' + name + '.png')); man['m4_' + name] = [fw, fh, 0, 0, n]
# cầu treo dọc: khung 0 = chỉ xích (ảnh Tiled op_bridge_v), rồi 15 khung "lifting" (ván từ dưới hố kéo lên nằm ngang kênh);
# phần ván còn treo dưới mép nam kênh (y >= 150 trong khung, hàng 83 trở xuống = sàn sân lưỡi xoay) bị xoá -> ván "trồi lên từ hố".
src = Image.open(OP / "hanging bridge-lifting animation-vertical.png").convert('RGBA'); fw, fh = 128, src.height
closed = Image.open(ART / 'op_bridge_v.png').convert('RGBA'); assert closed.size == (fw, fh)
frames = [closed]
for i in range(15):
    f = src.crop((i * fw, 0, (i + 1) * fw, fh)); px = f.load()
    for y in range(150, fh):
        for x in range(fw): px[x, y] = (0, 0, 0, 0)
    frames.append(f)
assert frames[-1].tobytes() == Image.open(ART / 'op_bridge_v_open.png').convert('RGBA').tobytes()
strip = Image.new('RGBA', (fw * len(frames), fh))
for i, f in enumerate(frames): strip.paste(f, (i * fw, 0))
strip.save(VFX / 'm4_op_bridge_v.png'); man['m4_op_bridge_v'] = [fw, fh, 0, 0, len(frames)]
# §305: sàn sập (PitTrapMechanism) — dải gốc 21 khung 64x64 (khung 0 sàn lành -> hố tối), giữ nguyên
pit = Image.open(GP / "EPIC RPG World Pack - Crypt V.1.6/Props/animated/pit trap2-2x2.png").convert('RGBA'); assert pit.size == (1344, 64)
pit.save(VFX / 'trap_pit2.png'); man['trap_pit2'] = [64, 64, 0, 0, 21]
mp = Path(__file__).resolve().parent / 'm4_gate_manifest.json'; json.dump(man, open(mp, 'w'), indent=1)
subprocess.run([sys.executable, str(HOME / "Catalog/v2/tools/vfx_meta.py"), str(VFX), str(mp)], check=True)
for name in man:                                            # dải dài (cửa boss 3840 px) -> cho phép 8192
    m = VFX / (name + '.png.meta'); s = m.read_text(encoding='utf-8')
    s2 = re.sub(r"maxTextureSize: \d+", "maxTextureSize: 8192", s)
    if s2 != s: m.write_text(s2, encoding='utf-8')
print('xong', len(man), 'dải')
