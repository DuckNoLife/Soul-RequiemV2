import sys, os, re
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from patch_lib import Doc, N, esc
ROOT=os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)),'..','..'))
G=os.path.join(ROOT,'Assets','GamePackage'); BK=os.path.join(ROOT,'_backups','2026-11-16_waveD_data')
M1=os.path.join(G,'ERW - Grass Land 2.0 v2.1/TiledMap Editor/Maps/map1_greystone_plains.tmx')
M2=os.path.join(G,'EPIC RPG World - Sewers V1.5/TiledMap Editor/Maps/map2_undercity_of_vael.tmx')
M3=os.path.join(G,'EPIC RPG World Pack - Cemetery V 1.6/TiledMap Editor/Maps/map3_vale_of_graves.tmx')
EXPECT={M1:'5de4afa88affd2a64e859afc3ef0d9e2',M2:'bb0f16677c53c0491b7efe33abf07756',M3:'8bf61208361a3efc6a86699832f9ba92'}
report=[]
def S(lines): return '\\n'.join(l.strip() for l in lines.strip().split('\n') if l.strip())
def ensure_npcs(d):
    if re.search(r'<objectgroup [^>]*name="npcs"',d.src): return
    m=re.search(r'nextlayerid="(\d+)"',d.src); lid=int(m.group(1))
    d.src=d.src[:m.start(1)]+str(lid+1)+d.src[m.end(1):]
    g=re.search(r' <objectgroup [^>]*name="triggers"',d.src)
    nl='\r\n' if d.crlf else '\n'
    block=(' <objectgroup id="%d" name="npcs" opacity="0.5">'%lid+nl+'  <properties>'+nl+
           '   <property name="unity:IsTrigger" type="bool" value="true"/>'+nl+'  </properties>'+nl+' </objectgroup>'+nl)
    d.src=d.src[:g.start()]+block+d.src[g.start():]; d.log.append('npcs layer %d'%lid)
def story(d,name,tx,ty,tw,th,script,**p):
    p['script']=S(script); d.add('triggers',name,'story',tx,ty,tw,th,p)
def done(d,label):
    if d.err: report.append('!! %s ERRORS (not saved): %s'%(label,d.err)); return
    report.append('%s %s -> %s (%d)'%(label,d.md5_before,d.save(),len(d.log)))

# ============ MAP 1 ============
d=Doc(M1,BK); assert d.md5_before==EXPECT[M1],'map1 changed'
d.add('npcs','goblin_child','villager',9,25,1,1,{'kind':'goblin_child','walk':False,'face':'E','speaker':'Goblin Child',
  'lines':"...cold. Always cold, since the water went green. | Grandmother says the green will make me strong. It only makes me tired."})
d.set(3033,'unless','mercy_1')
story(d,'prologue',104,85,1,1,'''
fade out 0.01
slide Story/angel_bird_1: Long ago, on the shore of Lake Nova, a gentle man was bound to a post.|They called him the Ascended. He had fed them, healed them, and asked for nothing.
slide Story/angel_bird_2: The great Houses feared a man the people loved more than them. So they set their ringed hands on him.
slide Story/angel_bird_3: His blood ran into the lake. The nobles drank of it, and did not die.|They have not died since.
slide Story/angel_bird_4: When he breathed his last, white birds rose from the water. His people named that day the Angel Bird.
narrate Eleven hundred and seventy-six years later, his body is still not at rest.|Year 1176 of the Eternal King.
slide end
fade in 1.4
''',trigger='start',unless='intro_done')
story(d,'tremor_caves',2,2,1,1,'''
shake 0.05 1.4
bubble ren: The ground... it's beating. Like something under it has a heart.
hint Deep below Greystone, something stirs.
''',trigger='flag',when='z2_cleared')
story(d,'tremor_ruins',2,3,1,1,'''
shake 0.04 1.2
bubble ren: Again. Stronger this time - and closer to that ring of stones.
''',trigger='flag',when='z4_started')
story(d,'ring_look',94,12,4,10,'''
face 111,19.8
cam 111,19.8 1.1
wait 1.6
beat 111,19.8 #8dff5a 3
camren
wait 0.8
bubble ren: Grain, thrown into a pit like an offering... and something down there breathing.
''',trigger='enter',when='z4_cleared',unless='z5_started')
story(d,'give_tonic',9,25,1,1,'''
set mercy_1
bubble goblin_child: ...for me?
bubble ren: Drink it slowly. It burns.
hint Some mercies are never repaid. They are only remembered.
''',trigger='interact',verb='Give the green tonic',unless='picked_z3b_tonic,mercy_1')
done(d,'map1')

# ============ MAP 2 ============
d=Doc(M2,BK); assert d.md5_before==EXPECT[M2],'map2 changed'
ensure_npcs(d)
d.add('npcs','pell','villager',10,37,1,1,{'kind':'pell','walk':False,'face':'S','speaker':'Pell','show_when':'r6_cleared','hide_when':'r8_cleared',
  'lines':"He built that seal with the last of his life. Please - don't be the one who undoes it. | I kept his books for him. He lived forty years down here. I have been sweeping his floor for four hundred. | If you find his last page, bring it to me. He hid it from the Hierophant. He hid it from me, too."})
story(d,'pell_meet',10,37,1,1,'''
bubble pell: Don't strike! I'm not one of the mad ones. Not yet.
say Pell: My name is Pell. I was Malric's clerk, long ago - before the green made me what I am.|He was a man, you know. A scholar. He wanted every serf in Vael to stand level with the deathless lords.|He did terrible things for it. So did I. I held the knife when we fed the Sleeper.|In the end he chained the Eye with his own dying breath. The pumps, the pipes, the brimming basin - all of it is his seal.|You want the crank. It is the only way out, and you will take it. Just... know what you are turning.
bubble ren: I'm sorry. I have to get out.
bubble pell: He said someone would. He hoped they would at least be sorry.
''',trigger='enter',radius=0.95,when='r6_cleared')
story(d,'pell_page',10,37,1,1,'''
set mercy_2
say Pell: His hand. I would know it anywhere.|"If this is read, the seal is broken, and I was right to fear it. Whoever carries the Eye now: it cannot take you. Do not let it teach you to want to be taken."|"And Pell - forgive me. You were the only one who stayed."
bubble pell: ...Thank you. Now go. Get out before it wakes.
''',trigger='interact',verb="Give Malric's last page",when='malric_page',unless='mercy_2')
d.add('triggers','malric_page','pickup',12,7,1,1,{'item':'key','key_id':'malric_last_page','key_name':"Malric's Last Page",
  'key_desc':"A single page in Malric's hand, folded small and hidden under the vault's lining. It is addressed to no one - and then, at the bottom, to Pell.",
  'key_icon':'item_note','key_opens':'Pell, in the laboratory','flag':'malric_page','marker':'true','zone_id':'rec',
  'hint_text':"Hidden under the lining of the vault: a last page in Malric's hand. The old clerk in the laboratory should have this."})
story(d,'hub_grate',51,56,3,3,'''
cam 69,96 0.9
wait 1.6
beat 69,96 #e05a64 3
camren
wait 0.6
bubble ren: Something under this place is chained... and it's counting.
''',trigger='enter',when='r4_started',unless='vault_cleared')
story(d,'pump_seal',58,68,1,1,'''
face 58,67
say Scratched into the console:If you are reading this, you want out. So did I.|Do not turn it. I am asking you as a dead man.|- M.
bubble ren: ...I'm sorry, Malric. There is no other way up.
''',trigger='enter',radius=0.95,when='lever_handle',unless='ichor_diverted')
done(d,'map2')

# ============ MAP 3 ============
d=Doc(M3,BK); assert d.md5_before==EXPECT[M3],'map3 changed'
ensure_npcs(d)
d.add('npcs','aldous','villager',86,37,1,1,{'kind':'aldous','walk':False,'face':'W','speaker':'Aldous','hide_when':'x1_wall_broken',
  'lines':"Don't finish them in the green. Drag them onto dry ground first. Learned that one the hard way. | The Lady of House Vey keeps the green for the King. If anyone can pull that thing out of you, it's her. Whether you'd live through it is another matter. | Me? Never took the Gift. The dead don't seem to notice me. One of the few perks of the job."})
d.add('npcs','aldous_field','villager',50,67,1,1,{'kind':'aldous','walk':False,'face':'S','speaker':'Aldous','show_when':'x1_wall_broken',
  'lines':"The only quiet patch in the Vale. Babes who died before they could drink. Not one of them has stood up. | Funny. The Choir calls these the lost ones."})
story(d,'outfall',65,5,1,1,'''
fade out 0.01
narrate Far below, in the Undercity, red lanterns came down the old ring shaft.|The Hierophant's men chained the Sleeper's body and carried it away.|They took the old goblin, too. He did not fight them.
fade in 1.5
cam 60,12 1
wait 1.2
glow 60,12 #8dff5a 2.4 3
shake 0.03 0.8
camren
wait 0.6
bubble ren: The water from the pumps... it's running into the graves. I did this.
''',trigger='start')
story(d,'commons_bell',2,2,1,1,'''
shake 0.03 0.6
bubble ren: That bell... the thing in my chest is singing back to it.
hint Every bell that tolls tells the Choir where you are. Silence them.
''',trigger='flag',when='v3_started')
story(d,'aldous_meet',86,37,1,1,'''
bubble aldous: Easy, easy. I'm still breathing - unlike most of my parish.
say Aldous: Aldous. Sexton of the Vale, for what's left of it.|You came up out of the pump works drain, didn't you? With that green light in your chest. The Choir has your face on every gate.|If you want it out of you, only one soul keeps the green in this valley: the Lady of House Vey. West, along the Avenue of Peers.|Mind her, though. She's been "the Lady" for four hundred years.
set aldous_met
''',trigger='enter',radius=1.0,unless='x1_wall_broken')
story(d,'aldous_field',50,67,1,1,'''
say Aldous: Followed you. Hope you don't mind.|These are the ones who died before they could drink. The Choir calls them lost.|Strange, isn't it? They're the only dead in the Vale who sleep.
''',trigger='enter',radius=1.0,when='x1_wall_broken')
story(d,'morrow_door',66,114,1,1,'''
cam 66,116 1
wait 1.4
shake 0.05 1.2
glow 66,116 #e05a64 3 4
narrate With his last breath, Morrow strikes his bell once more. The Requiem Door tears open.|Red light pours out of it - and the Eye inside you answers.
exit to_map4
''',trigger='flag',when='v11_cleared')
done(d,'map3')
print('\n'.join(report))
