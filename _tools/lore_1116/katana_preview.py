import sys
from PIL import Image, ImageDraw
from katana_forge import showcase, design, pickup
from katana_specs import SPECS
names=sys.argv[1:] or list(SPECS)
rows=[]
for n in names:
    sc=showcase(SPECS[n]); de=design(SPECS[n]); pk=pickup(sc); rows.append((n,sc,de,pk))
W=256*3+8+64*4+40; H=len(rows)*(96*3+20)
sh=Image.new('RGBA',(W,H),(32,30,36,255)); d=ImageDraw.Draw(sh)
for i,(n,sc,de,pk) in enumerate(rows):
    y=i*(96*3+20)
    sh.alpha_composite(sc.resize((768,288),Image.NEAREST),(4,y+10))
    sh.alpha_composite(de.resize((256,128),Image.NEAREST),(780,y+10))
    sh.alpha_composite(pk.resize((192,56),Image.NEAREST),(780,y+150)); sh.alpha_composite(pk,(990,y+170))
    d.text((782,y+220),n,fill=(230,230,230))
sh.save('preview/katana_forge.png'); print(sh.size)
