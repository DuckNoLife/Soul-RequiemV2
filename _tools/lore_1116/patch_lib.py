import re, html, os, shutil, hashlib
def md5(p): return hashlib.md5(open(p,'rb').read()).hexdigest()
def esc(v): return v.replace('&','&amp;').replace('<','&lt;').replace('>','&gt;').replace('"','&quot;')
def N(t): return t.strip('\n').replace('\r','').replace('\n','\\n')
class Doc:
    def __init__(s,path,backup_dir):
        s.path=path; raw=open(path,'rb').read(); s.crlf=b'\r\n' in raw
        s.src=raw.decode('utf-8'); s.log=[]; s.err=[]
        os.makedirs(backup_dir,exist_ok=True); shutil.copy2(path,os.path.join(backup_dir,os.path.basename(path)))
        s.md5_before=md5(path)
    def _blk(s,oid):
        m=re.search(r'<object id="%s"[ >]'%oid,s.src)
        if not m: raise KeyError(oid)
        e=s.src.find('</object>',m.start())
        nxt=s.src.find('<object ',m.start()+5)
        if e<0 or (0<=nxt<e): raise KeyError('noprops %s'%oid)
        return m.start(),e
    def get(s,oid,name):
        a,b=s._blk(oid); blk=s.src[a:b]
        m=re.search(r'<property name="%s"(?: type="[^"]*")? value="([^"]*)"'%re.escape(name),blk)
        return html.unescape(m.group(1)) if m else None
    def set(s,oid,name,value):
        try: a,b=s._blk(oid)
        except KeyError as e: s.err.append('missing object %s'%e); return
        blk=s.src[a:b]; v=esc(value)
        m=re.search(r'(<property name="%s"(?: type="[^"]*")? value=")([^"]*)(")'%re.escape(name),blk)
        if m: blk=blk[:m.start(2)]+v+blk[m.end(2):]
        else:
            i=blk.rfind('</properties>')
            if i<0: s.err.append('no properties %s'%oid); return
            ind='\r\n' if s.crlf else '\n'
            blk=blk[:i]+'<property name="%s" value="%s"/>%s   '%(name,v,ind)+blk[i:]
        s.src=s.src[:a]+blk+s.src[b:]; s.log.append('%s.%s'%(oid,name))
    def sub(s,oid,name,old,new):
        cur=s.get(oid,name)
        if cur is None or old not in cur: s.err.append('sub miss %s.%s: %r'%(oid,name,old[:50])); return
        s.set(oid,name,cur.replace(old,new))
    def gsub(s,old,new):
        n=s.src.count(old)
        if n==0: s.err.append('gsub miss %r'%old); return
        s.src=s.src.replace(old,new); s.log.append('gsub x%d %r'%(n,old))
    def save(s):
        open(s.path,'wb').write(s.src.encode('utf-8')); return md5(s.path)

def _add_object(self, layer, name, otype, tx, ty, tw=1.0, th=1.0, props=None):
    """Thêm object hình chữ nhật (ô Tiled) vào objectgroup `layer`; tăng nextobjectid."""
    m=re.search(r'nextobjectid="(\d+)"',self.src); oid=int(m.group(1))
    self.src=self.src[:m.start(1)]+str(oid+1)+self.src[m.end(1):]
    g=re.search(r'<objectgroup [^>]*name="%s"[^>]*>'%re.escape(layer),self.src)
    if not g: self.err.append('no layer %s'%layer); return None
    end=self.src.find('</objectgroup>',g.end())
    nl='\r\n' if self.crlf else '\n'
    ps=dict(props or {}); ps.setdefault('unity:IsTrigger','true')
    lines=['  <object id="%d" name="%s" type="%s" x="%g" y="%g" width="%g" height="%g">'%(oid,esc(name),otype,tx*32,ty*32,tw*32,th*32),'   <properties>']
    for k in sorted(ps):
        v=ps[k]
        if k=='unity:IsTrigger' or isinstance(v,bool): lines.append('    <property name="%s" type="bool" value="%s"/>'%(k,str(v).lower()))
        else: lines.append('    <property name="%s" value="%s"/>'%(k,esc(str(v))))
    lines+=['   </properties>','  </object>']
    block=nl.join(lines)+nl+' '
    # chèn trước </objectgroup> (giữ thụt lề ' </objectgroup>')
    ins=self.src.rfind(nl,0,end)+len(nl)
    self.src=self.src[:ins]+block.rstrip(' ')+self.src[ins:]
    self.log.append('add %s #%d'%(name,oid)); return oid
Doc.add=_add_object
