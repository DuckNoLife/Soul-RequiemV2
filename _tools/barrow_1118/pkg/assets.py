# 18/11 (§330): tài sản khu The Red Field (Crimson Oath, Map 3b). Cắt từ ảnh xem trước 1:1 của pixelart_workbench / ảnh PixelLab.
import os, re, uuid, random, shutil
from PIL import Image
T='/tmp/claude-tool-results/949ad0d3-1107-5f85-bbec-3a629568c41e/'
U='/mnt/user-data/uploads/Soul Requiem/'
O='/mnt/user-data/outputs/barrow/'
CEM='Assets/GamePackage/EPIC RPG World Pack - Cemetery V 1.6/'
BG=(31,37,44)
def out(rel):
    p=O+rel; os.makedirs(os.path.dirname(p),exist_ok=True); return p
def cells(path,cell,cols,n,top=18):
    im=Image.open(path).convert('RGBA'); fr=[]
    for i in range(n):
        c=im.crop(((i%cols)*cell,top+(i//cols)*cell,(i%cols)*cell+cell,top+(i//cols)*cell+cell)); px=c.load()
        for y in range(cell):
            for x in range(cell):
                if px[x,y][:3]==BG: px[x,y]=(0,0,0,0)
        fr.append(c)
    return fr
def strip(fr):
    w,h=fr[0].size; s=Image.new('RGBA',(w*len(fr),h),(0,0,0,0))
    for i,f in enumerate(fr): s.alpha_composite(f,(i*w,0))
    return s
# ---- kiểm khoá nền: khung 0 của sheet idle phải trùng ảnh gốc trong suốt
ref=Image.open(T+'mcp-pixellab-blob-1795005802679-9vaxwn-db833e3c.png').convert('RGBA')
idle=cells(T+'mcp-pixellab-blob-1795005945197-n100x1-3bd19f6b.png',192,5,9)
a=ref.load(); b=idle[0].load()
bad=sum(1 for y in range(192) for x in range(192) if (a[x,y][3]>0)!=(b[x,y][3]>0))
print('kiểm khoá nền boss (pixel lệch):',bad)
slam=cells(T+'mcp-pixellab-blob-1795005950509-m5bph9-6378b554.png',192,5,9)
opn=cells(T+'mcp-pixellab-blob-1795005952703-dvu0q2-de9e55a4.png',192,5,9)
E='Assets/Enemies/Map3b/Sprites/Red_Field/'
for name,fr in (('Idle',idle[1:]),('Slam',slam),('Open',opn)):
    strip(fr).save(out(E+'Red_Field_%s.png'%name))
bb=ref.getbbox(); bottom=bb[3]                       # chân = đáy khối xác
piv=(192-bottom+6)/192.0                             # nhích 6 px lên: chân đứng trong vũng, không trên mép
open(out(E+'Red_Field_meta.txt'),'w').write('192 192 %.4f %.4f %.4f'%(piv,piv,piv))
print('boss bbox',bb,'pivotY',round(piv,4))
# ---- VFX: rừng giáo (GroundStrike), lính hồn
TPL=open(U+'Assets/Resources/VFX/hit.png.meta').read()
def vmeta(name,n,w,h,px,py):
    head=TPL[:TPL.index('  internalIDToNameTable:')]
    mid=TPL[TPL.index('  externalObjects:'):TPL.index('    sprites:')+len('    sprites:\n')]
    entry=TPL[TPL.index('    - serializedVersion: 2\n      name: hit_0'):TPL.index('    - serializedVersion: 2\n      name: hit_1')]
    tail=TPL[TPL.index('    outline: []\n    customData:'):TPL.index('    nameFileIdTable:')]
    end=TPL[TPL.index('  mipmapLimitGroupName:'):]
    ids=[random.randint(10**17,9*10**18) for _ in range(n)]
    s=re.sub(r'guid: \w+','guid: '+uuid.uuid4().hex,head,count=1)
    s+='  internalIDToNameTable:\n'+''.join('  - first:\n      213: %d\n    second: %s_%d\n'%(ids[i],name,i) for i in range(n))+mid
    for i in range(n):
        e=entry.replace('name: hit_0','name: %s_%d'%(name,i))
        e=re.sub(r'x: 0\n        y: 0\n        width: 63\n        height: 55','x: %d\n        y: 0\n        width: %d\n        height: %d'%(i*w,w,h),e)
        e=e.replace('pivot: {x: 0.492063, y: 0.618182}','pivot: {x: %.6f, y: %.6f}'%(px,py))
        e=re.sub(r'spriteID: \w+','spriteID: '+uuid.uuid4().hex,e)
        e=re.sub(r'internalID: -?\d+','internalID: %d'%ids[i],e)
        s+=e
    s+=tail+'    nameFileIdTable:\n'+''.join('      %s_%d: %d\n'%(name,i,ids[i]) for i in range(n))+end
    assert 'pivot: {x: %.6f'%px in s
    return s
sp=cells(T+'mcp-pixellab-blob-1795006135332-i02sn4-09654504.png',96,8,8)
sp=sp[1:7]                                             # khung 1..6: nứt -> trồi -> tan
bot=max(f.getbbox()[3] for f in sp if f.getbbox())
strip(sp).save(out('Assets/Resources/VFX/rf_spear.png'))
open(out('Assets/Resources/VFX/rf_spear.png.meta'),'w',newline='\n').write(vmeta('rf_spear',len(sp),96,96,0.5,(96-bot+10)/96.0))
gh=cells(T+'mcp-pixellab-blob-1795005825076-wv5opw-5d30c012.png',128,3,3)
for i,d in enumerate('SEN'):
    g=gh[i]; gb=g.getbbox(); g.save(out('Assets/Resources/VFX/rf_ghost_%s.png'%d))
    open(out('Assets/Resources/VFX/rf_ghost_%s.png.meta'%d),'w',newline='\n').write(vmeta('rf_ghost_%s'%d,1,128,128,0.5,(128-gb[3]+4)/128.0))
print('ghost bbox',gh[1].getbbox())
# ---- sprite đơn (Resources/VFX/props): phiến đá nứt (Earthshatter) + miệng hầm bậc thang sau khi vỡ
PM=open(U+'Assets/Resources/VFX/props/barrel_up.png.meta').read()
def pmeta(): return re.sub(r'guid: \w+','guid: '+uuid.uuid4().hex,PM,count=1)
# ---- ảnh tile object (tileset SR-barrow, Cemetery/Props/SR-barrow)
PB=CEM+'Props/SR-barrow/'
TM=open(U+CEM+'Props/SR-relics/relic_crimson.png.meta').read()
imgs=[('borrow_throne','mcp-pixellab-blob-1795005823084-9bs2fl-436a9029.png'),
      ('borrow_throne_empty','mcp-pixellab-blob-1795005935695-wkse7v-123bc761.png'),
      ('barrow_entrance','mcp-pixellab-blob-1795005823757-8msqvy-484d217c.png'),
      ('spears_planted','mcp-pixellab-blob-1795006045169-2omyso-e85ce701.png'),
      ('stair_hole','mcp-pixellab-blob-1795006136170-8wb70n-2021347d.png'),
      ('barrow_slab','mcp-pixellab-blob-1795006046636-hqo83w-e3a7e2d6.png')]
for n,src in imgs: Image.open(T+src).convert('RGBA').save(out(PB+n+'.png'))
for n in ('cr_bossdoor','cr_wood1','cr_metal3'):
    Image.open(U+'Assets/GamePackage/EPIC RPG World Pack - Crypt V.1.6/Props/SR-m4-art/%s.png'%n).save(out(PB+n+'.png'))
names=[n for n,_ in imgs]+['cr_bossdoor','cr_wood1','cr_metal3']
for n in names:
    open(out(PB+n+'.png.meta'),'w',newline='\n').write(re.sub(r'guid: \w+','guid: '+uuid.uuid4().hex,TM,count=1))
tsx=['<?xml version="1.0" encoding="UTF-8"?>','<tileset version="1.10" tiledversion="1.11.2" name="SR-barrow" tilewidth="128" tileheight="128" tilecount="%d" columns="0">'%len(names),
     ' <grid orientation="orthogonal" width="1" height="1"/>']
sizes={}
for i,n in enumerate(names):
    w,h=Image.open(O+PB+n+'.png').size; sizes[n]=(i,w,h)
    tsx+=[' <tile id="%d">'%i,'  <image source="../../Props/SR-barrow/%s.png" width="%d" height="%d"/>'%(n,w,h),' </tile>']
tsx.append('</tileset>')
open(out(CEM+'TiledMap Editor/Tilesets/SR-barrow.tsx'),'w',newline='\n').write('\n'.join(tsx)+'\n')
TSM=open(U+CEM+'TiledMap Editor/Tilesets/SR-relic-crimson.tsx.meta').read()
open(out(CEM+'TiledMap Editor/Tilesets/SR-barrow.tsx.meta'),'w',newline='\n').write(re.sub(r'guid: \w+','guid: '+uuid.uuid4().hex,TSM,count=1).replace('numberOfObjectsImported: 4','numberOfObjectsImported: %d'%(len(names)+3)))
import json; json.dump(sizes,open('/home/claude/barrow/sizes.json','w')); print(sizes)
