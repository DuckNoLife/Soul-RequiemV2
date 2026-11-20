# Ghép dải khung ngang + .meta (spriteMode Multiple) cho biểu tượng trạng thái
import json, re, uuid, random
from PIL import Image
T = open('../../Assets/Resources/VFX/hit.png.meta').read()
head = T[:T.index('  internalIDToNameTable:')]
mid = T[T.index('  externalObjects:'):T.index('    sprites:')+len('    sprites:\n')]
entry = T[T.index('    - serializedVersion: 2\n      name: hit_0'):T.index('    - serializedVersion: 2\n      name: hit_1')]
tail = T[T.index('    outline: []\n    customData:'):T.index('    nameFileIdTable:')]
end = T[T.index('  mipmapLimitGroupName:'):]
import os; os.makedirs('out', exist_ok=True)
for k in ['bleed', 'burn', 'bind', 'chill', 'mark']:
    js = json.load(open(k + '.json')); sheet = Image.open(k + '.png').convert('RGBA')
    fr = [sheet.crop((f['frame']['x'], f['frame']['y'], f['frame']['x'] + f['frame']['w'], f['frame']['y'] + f['frame']['h'])) for f in js['frames']]
    bb = None
    for f in fr:
        b = f.getbbox()
        if b: bb = b if bb is None else (min(bb[0], b[0]), min(bb[1], b[1]), max(bb[2], b[2]), max(bb[3], b[3]))
    x0, y0, x1, y1 = bb
    w, h = x1 - x0, y1 - y0
    strip = Image.new('RGBA', (w * len(fr), h))
    for i, f in enumerate(fr): strip.paste(f.crop(bb), (i * w, 0))
    name = 'status_' + k
    strip.save('out/' + name + '.png')
    ids = [random.randint(-2**62, 2**62) for _ in fr]
    m = head.replace(re.search(r'guid: \w+', head).group(0), 'guid: ' + uuid.uuid4().hex)
    m += '  internalIDToNameTable:\n' + ''.join('  - first:\n      213: %d\n    second: %s_%d\n' % (ids[i], name, i) for i in range(len(fr)))
    m += mid
    for i in range(len(fr)):
        e = entry.replace('name: hit_0', 'name: %s_%d' % (name, i))
        e = re.sub(r'x: 0\n        y: 0\n        width: 63\n        height: 55', 'x: %d\n        y: 0\n        width: %d\n        height: %d' % (i * w, w, h), e)
        e = e.replace('pivot: {x: 0.492063, y: 0.618182}', 'pivot: {x: 0.5, y: 0}')
        e = re.sub(r'spriteID: \w+', 'spriteID: ' + uuid.uuid4().hex, e)
        e = re.sub(r'internalID: -?\d+', 'internalID: %d' % ids[i], e)
        assert 'x: %d' % (i * w) in e
        m += e
    m += tail + '    nameFileIdTable:\n' + ''.join('      %s_%d: %d\n' % (name, i, ids[i]) for i in range(len(fr))) + end
    open('out/' + name + '.png.meta', 'w', newline='\n').write(m)
    print(name, len(fr), 'khung', w, 'x', h)
