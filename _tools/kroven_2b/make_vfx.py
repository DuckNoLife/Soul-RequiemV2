# 17/11 (§285): chuyển game pack PixelLab VFX -> Resources/VFX/<tên>.png (dải ngang) + .meta (cắt khung, pivot = điểm neo của hiệu ứng).
import json, os, re, uuid, random, sys
from PIL import Image
PK = '/home/claude/vfx/packs'
OUT = '/home/claude/vfx/out'
TPL = open('/mnt/user-data/uploads/Soul Requiem/Assets/Resources/VFX/hit.png.meta').read()
os.makedirs(OUT, exist_ok=True)

def frames_of(pack, clip):
    m = json.load(open(f'{PK}/{pack}/manifest.json'))
    c = [c for c in m['clips'] if c['name'] == clip][0]
    w, h = c['frame']['w'], c['frame']['h']
    fr = []
    for sh in c['sheets']:
        im = Image.open(f"{PK}/{pack}/{sh['file']}").convert('RGBA')
        for i in range(sh['frames']):
            col, row = i % sh['columns'], i // sh['columns']
            fr.append(im.crop((col * w, row * h, col * w + w, row * h + h)))
    return fr, w, h, c['offset'], c['start_frame'], m['fps'], m['loop']

def meta(name, n, w, h, px, py):
    head = TPL[:TPL.index('  internalIDToNameTable:')]
    mid = TPL[TPL.index('  externalObjects:'):TPL.index('    sprites:') + len('    sprites:\n')]
    entry = TPL[TPL.index('    - serializedVersion: 2\n      name: hit_0'):TPL.index('    - serializedVersion: 2\n      name: hit_1')]
    tail = TPL[TPL.index('    outline: []\n    customData:'):TPL.index('    nameFileIdTable:')]
    end = TPL[TPL.index('  mipmapLimitGroupName:'):]
    ids = [random.randint(10 ** 17, 9 * 10 ** 18) for _ in range(n)]
    s = re.sub(r'guid: \w+', 'guid: ' + uuid.uuid4().hex, head, count=1)
    s += '  internalIDToNameTable:\n' + ''.join('  - first:\n      213: %d\n    second: %s_%d\n' % (ids[i], name, i) for i in range(n)) + mid
    for i in range(n):
        e = entry.replace('name: hit_0', 'name: %s_%d' % (name, i))
        e = re.sub(r'x: 0\n        y: 0\n        width: 63\n        height: 55', 'x: %d\n        y: 0\n        width: %d\n        height: %d' % (i * w, w, h), e)
        e = e.replace('pivot: {x: 0.492063, y: 0.618182}', 'pivot: {x: %.6f, y: %.6f}' % (px, py))
        e = re.sub(r'spriteID: \w+', 'spriteID: ' + uuid.uuid4().hex, e)
        e = re.sub(r'internalID: -?\d+', 'internalID: %d' % ids[i], e)
        s += e
    s += tail + '    nameFileIdTable:\n' + ''.join('      %s_%d: %d\n' % (name, i, ids[i]) for i in range(n)) + end
    assert 'pivot: {x: %.6f' % px in s
    return s

info = {}
def make(name, pack, clip):
    fr, w, h, off, start, fps, loop = frames_of(pack, clip)
    strip = Image.new('RGBA', (w * len(fr), h), (0, 0, 0, 0))
    for i, f in enumerate(fr): strip.paste(f, (i * w, 0))
    strip.save(f'{OUT}/{name}.png')
    px = -off['x'] / w; py = 1 + off['y'] / h          # neo = góc trên trái - offset; pivot Unity tính từ đáy
    open(f'{OUT}/{name}.png.meta', 'w', newline='\n').write(meta(name, len(fr), w, h, px, py))
    info[name] = dict(frames=len(fr), w=w, h=h, pivot=(round(px, 3), round(py, 3)), start=start, fps=fps, loop=loop)

make('lg_whirl_back', 'whirl', 'behind'); make('lg_whirl_front', 'whirl', 'front')
make('lg_slam', 'slam', 'whole'); make('lg_thrust', 'thrust', 'whole'); make('lg_spark', 'spark', 'whole')
make('lg_aura_back', 'aura', 'behind'); make('lg_aura_front', 'aura', 'front')
make('lg_dust', 'dust', 'whole'); make('lg_break', 'break', 'whole')
make('lg_stun_back', 'stun', 'behind'); make('lg_stun_front', 'stun', 'front')
for k, v in info.items(): print(k, v)
