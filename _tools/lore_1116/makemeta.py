import sys, uuid, re, os
# makemeta.py <template.meta> <center|feet> <png...>  -> viết .meta (sprite, point, không nén) với guid mới
tpl=open(sys.argv[1],encoding='utf-8').read(); mode=sys.argv[2]
for png in sys.argv[3:]:
    m=tpl
    m=re.sub(r'^guid: [0-9a-f]+', 'guid: '+uuid.uuid4().hex, m, flags=re.M)
    if mode=='center':
        m=re.sub(r'alignment: \d+','alignment: 0',m,count=1)
        m=re.sub(r'spritePivot: \{[^}]*\}','spritePivot: {x: 0.5, y: 0.5}',m,count=1)
    m=re.sub(r'spriteID: [0-9a-f]+', lambda k: 'spriteID: '+uuid.uuid4().hex, m)
    open(png+'.meta','w',encoding='utf-8',newline='').write(m)
    print('meta',os.path.basename(png))
