# Ảnh toàn cảnh map thu nhỏ + khung khu (zone) + nhãn + điểm đánh dấu — để phân tích vị trí bí mật.
import sys, re, html
from PIL import Image, ImageDraw
from render_tmx import main as render
def overview(tmx,out,scale,marks):
    s=open(tmx,encoding='utf-8').read()
    mt=re.search(r'<map [^>]*>',s).group(0); W=int(re.search(r' width="(\d+)"',mt).group(1)); H=int(re.search(r' height="(\d+)"',mt).group(1))
    render(tmx,0,0,W,H,'/tmp/full.png',1)
    im=Image.open('/tmp/full.png').convert('RGBA'); im=im.resize((int(im.width*scale),int(im.height*scale)),Image.BOX)
    d=ImageDraw.Draw(im); k=32*scale
    for m in re.finditer(r'<object id="\d+" name="([^"]*)" type="zone" x="([\d.]+)" y="([\d.]+)" width="([\d.]+)" height="([\d.]+)"[^>]*>(.*?)</object>',s,flags=re.S):
        t=re.search(r'name="title" value="([^"]*)"',m.group(6)); opt='name="optional"' in m.group(6)
        x,y,w,h=[float(v)*scale for v in m.group(2,3,4,5)]
        col=(255,200,60,255) if not opt else (120,220,255,255)
        d.rectangle([x,y,x+w,y+h],outline=col,width=2)
        d.text((x+3,y+2),(m.group(1)+' '+html.unescape(t.group(1)) if t else m.group(1))[:26],fill=col)
    for (tx,ty,label,col) in marks:
        x,y=tx*k,ty*k; d.ellipse([x-7,y-7,x+7,y+7],outline=col,width=3); d.text((x+9,y-6),label,fill=col)
    im.save(out); print(out,im.size)
if __name__=='__main__':
    pass
