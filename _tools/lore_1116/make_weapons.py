# Tạo WeaponDefinition + ảnh trưng bày + bảng UV cho các katana chưa có (16/11, §257). Khuôn: Resources/Weapons/Katana.asset.
import re, uuid
from katana_forge import showcase, design
from katana_specs import SPECS
R='../../Assets/Resources/Weapons/'
W=[
 dict(key='ironcrown',name='Katana_Ironcrown',id='katana_ironcrown',disp='Ironcrown',dmg=20,fps=11,rec=0.4,
      desc='A short, heavy northern blade, iron-dark from the forge. It does not cut so much as hold the line.',
      epi="The crown of Kroven - its kings were crowned with a blade",
      lore="Kroven never crowned its kings with gold. Each was handed this blade instead, and the realm was his for as long as he could hold it.\\n\\nKing Nolan held it to the last day. When the capital fell in the year the old wars ended, his guard carried it down into the deep places under the city and sealed every way behind them with traps, so that the conqueror would never hang it on his wall.\\n\\nHe never did.",
      found='The Kroven Deep, beneath the Undercity',glow=(1.2,0.85,0.55),trail=(1,0.78,0.5),
      trait=(5,'Last Stand',"Below half health you take 20% less damage, and heavy blows no longer stagger you. The crown is held, not worn.",0,0.2,0,0.5)),
 dict(key='eventide',name='Katana_Eventide',id='katana_eventide',disp='Eventide',dmg=17,fps=14,rec=0.3,
      desc='Long, slender, deeply curved. In the dark its temper line glows the colour of old wine.',
      epi="Aurel Vey's blade - it has not seen the sun since the first cup",
      lore="Aurel Vey wore it on the night of the first cup, when the great Houses drank together and swore to stand above the rest for ever.\\n\\nThey kept the oath. From that night the blade was carried only after dusk, and it has not seen the sun since. The Vey say it still remembers the taste of what was in the cup.",
      found='The First Cup, beneath the Matriarch\\x27s Bower',glow=(1.5,0.3,0.35),trail=(1,0.35,0.4),
      trait=(6,'Hunger',"Every foe your blade finishes returns 8% of your health. Your flasks heal 30% less - it would rather drink.",0,0.3,0.08,0)),
 dict(key='accord',name='Katana_Accord',id='katana_accord',disp="Arthur's Accord",dmg=18,fps=13,rec=0.3,
      desc='A bright, classical blade with three small marks at the root - one for each crown that once kept the peace.',
      epi="King Arthur's blade - three crowns, one peace",
      lore="In Arthur's time Arden, Miravel and Kroven met at one stone table and kept the peace for a generation. Each king laid his blade on the table as a promise; Arthur laid his last.\\n\\nWhen he died the peace died with him, and the three blades went back to war. His stayed sealed in the hall, waiting for the other three to be laid beside it again.",
      found='The Hall of Three Crowns',glow=(0.9,1.0,1.3),trail=(0.85,0.93,1),
      trait=(7,'Accord',"The last cut of a combo strikes 40% harder, and if it lands you take 15% less damage for 2s.",2,0.4,0.15,0)),
 dict(key='namesake',name='Katana_Namesake',id='katana_namesake',disp='Namesake',dmg=16,fps=14,rec=0.28,
      desc='A plain ceremonial blade of bone-white fittings. A name is cut into the steel in fine blue lines.',
      epi="Forged for Candidate VII - the name was always on it",
      lore="The Choir forged a blade for every Candidate and cut the child's true name into the steel, so that the Eye would know whom to answer.\\n\\nSix of them were buried with their Candidates. The seventh was never handed over.\\n\\n(Placeholder - Catacombs of Naming, Map 4.)",
      found='The Catacombs of Naming (not yet built)',glow=(0.6,0.85,1.4),trail=(0.7,0.88,1),
      trait=(8,'Namebound',"Every mantra recovers 15% faster. It answers to your name.",0,0.15,0,0)),
 dict(key='absolution',name='Katana_Absolution',id='katana_absolution',disp='Absolution',dmg=18,fps=13,rec=0.32,
      desc='Pale gold, very old, and too clean. Something dark has dried in the mouth of the scabbard.',
      epi="The apostle's blade - he called the killing an absolution",
      lore="An apostle carried it, in the years when the apostles carried nothing sharper than their words.\\n\\nHe drew it once, before the whole city, to punish the king who had killed his master, and the city called what he did an absolution. He has kept it ever since, the way some men keep a letter they cannot bring themselves to burn.\\n\\n(Placeholder - Crypt of the Haloed, Map 5.)",
      found='The Crypt of the Haloed (not yet built)',glow=(1.3,1.1,0.6),trail=(1,0.92,0.6),
      trait=(9,'Penance',"Each time you are hit, your next cut strikes 30% harder, up to three times. Swing, hit or miss, and the debt is paid.",0,0.3,0,0.3)),
]
def meta(src,dst):
    t=open(src,encoding='utf-8',newline='').read(); g=uuid.uuid4().hex
    t=re.sub(r'^guid: [0-9a-f]+','guid: '+g,t,flags=re.M); t=re.sub(r'spriteID: [0-9a-f]+',lambda k:'spriteID: '+uuid.uuid4().hex,t)
    open(dst,'w',encoding='utf-8',newline='').write(t); return g
base=open(R+'Katana.asset',encoding='utf-8',newline='').read()
for w in W:
    S=SPECS[w['key']]
    showcase(S).save(R+'Showcase/%s.png'%w['name']); design(S).save(R+'Designs/%s_design.png'%w['name'])
    gs=meta(R+'Showcase/Katana.png.meta',R+'Showcase/%s.png.meta'%w['name'])
    gd=meta(R+'Designs/Katana_design.png.meta',R+'Designs/%s_design.png.meta'%w['name'])
    s=base
    def sub(a,b):
        global s; n=len(re.findall(a,s,flags=re.M)); assert n==1,(a,n); s=re.sub(a,lambda m:b,s,flags=re.M)
    q=lambda v: v.replace('\\x27',"'")
    sub(r'^  m_Name: .*$','  m_Name: '+w['name']); sub(r'^  id: .*$','  id: '+w['id']); sub(r'^  displayName: .*$','  displayName: '+w['disp'])
    sub(r'^  description: .*$','  description: '+w['desc']); sub(r'^  epithet: .*$','  epithet: '+w['epi'])
    sub(r'^  lore: .*$','  lore: "'+q(w['lore'])+'"'); sub(r'^  foundAt: .*$','  foundAt: '+q(w['found']))
    sub(r'^  showcase: .*$','  showcase: {fileID: 21300000, guid: %s, type: 3}'%gs); sub(r'^  design: .*$','  design: {fileID: 2800000, guid: %s, type: 3}'%gd)
    sub(r'^  glowTint: .*$','  glowTint: {r: %g, g: %g, b: %g, a: 1}'%w['glow']); sub(r'^  trailColor: .*$','  trailColor: {r: %g, g: %g, b: %g, a: 1}'%w['trail'])
    sub(r'^  slashStyle: .*$','  slashStyle: '); sub(r'^  finisherRecovery: .*$','  finisherRecovery: %g'%w['rec'])
    k,title,text,win,bon,lee,thr=w['trait']
    a=s.index('  trait:'); b=s.index('    threshold:',a); b=s.index('\n',b)+1
    s=s[:a]+'  trait:\n    kind: %d\n    title: %s\n    text: "%s"\n    window: %g\n    bonus: %g\n    leech: %g\n    threshold: %g\n'%(k,title,text,win,bon,lee,thr)+s[b:]
    s=re.sub(r'damage: 16\b','damage: %d'%w['dmg'],s); s=re.sub(r'fps: 13\b','fps: %d'%w['fps'],s)
    open(R+w['name']+'.asset','w',encoding='utf-8',newline='').write(s)
    open(R+w['name']+'.asset.meta','w',newline='\n').write("fileFormatVersion: 2\nguid: %s\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"%uuid.uuid4().hex)
    print('made',w['name'])
