# thêm "tên uuid" (mỗi dòng) vào urls.json — URL = B + uuid + /<tên đổi _ thành ->.wav
import json,os,sys
p='urls.json'; d=json.load(open(p)) if os.path.exists(p) else {}
B="https://backblaze.pixellab.ai/file/pixellab-characters/audio/6c0ef4b9-2a2d-4259-9d52-214a2d4ce26b/"
for line in sys.stdin.read().split():
    n,u=line.split('=')
    d[n]=B+u+'/'+n.replace('_','-')+'.wav'
json.dump(d,open(p,'w'),indent=0); print(len(d))
