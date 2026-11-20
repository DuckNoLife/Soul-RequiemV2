# Xem từng nhóm lớp của một map mẫu: python3 layers_view.py <tmx trong /tmp/gp mirror> out.png "l1,l2" "l3" ...
import sys,os,copy
import xml.etree.ElementTree as ET
sys.path.insert(0,os.path.join(os.path.dirname(os.path.abspath(__file__)),'..','lore_1116'))
import render_tmx
from PIL import Image
src=sys.argv[1]; out=sys.argv[2]; groups=[g.split(',') for g in sys.argv[3:]]
t=ET.parse(src); r=t.getroot(); W,H=int(r.get('width')),int(r.get('height'))
os.environ['SR_CLEAN']='1'; ims=[]
for i,g in enumerate(groups):
    rr=copy.deepcopy(r)
    for ch in list(rr):
        if ch.tag in('layer','objectgroup') and ch.get('name') not in g: rr.remove(ch)
    p=os.path.join(os.path.dirname(src),'_tmp_view_%d.tmx'%i)
    assert '/mnt/' not in os.path.realpath(os.path.dirname(p)), 'thư mục tạm trỏ vào máy người dùng: '+os.path.realpath(p); ET.ElementTree(rr).write(p)
    o='/tmp/_lv_%d.png'%i; render_tmx.main(p,0,0,W,H,o,1); ims.append(Image.open(o)); os.remove(p)
sheet=Image.new('RGBA',(sum(i.width for i in ims)+8*(len(ims)-1),ims[0].height),(255,0,255,255)); x=0
for im in ims: sheet.paste(im,(x,0)); x+=im.width+8
sheet.save(out); print(out,sheet.size)
