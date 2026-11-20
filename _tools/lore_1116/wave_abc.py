import sys, os, json, re
sys.path.insert(0, os.path.dirname(__file__))
from patch_lib import Doc, N
ROOT=os.path.abspath(os.path.join(os.path.dirname(__file__),'..','..'))
A=os.path.join(ROOT,'Assets'); G=os.path.join(A,'GamePackage')
BK=os.path.join(ROOT,'_backups','2026-11-16_lore_wave_ABC')
M1=os.path.join(G,'ERW - Grass Land 2.0 v2.1/TiledMap Editor/Maps/map1_greystone_plains.tmx')
M2=os.path.join(G,'EPIC RPG World - Sewers V1.5/TiledMap Editor/Maps/map2_undercity_of_vael.tmx')
M3=os.path.join(G,'EPIC RPG World Pack - Cemetery V 1.6/TiledMap Editor/Maps/map3_vale_of_graves.tmx')
M3B=os.path.join(G,'EPIC RPG World Pack - Cemetery V 1.6/TiledMap Editor/Maps/map3b_kings_barrow.tmx')
EXPECT={M1:'05df6db5382d350966e2a3f2b9b51e04',M2:'383ca0217592e36d1ecf7a54d69046bb',M3:'a089de155dedf6554c2443a6c509d22d',M3B:'5b37cfdd70e903ad52fe90c3ede0af05'}
RED_HI='#e05a64'; RED_LO='#c0303c'
report=[]
def done(d,label):
    if d.err:
        report.append('!! %s ERRORS (not saved): %s'%(label,d.err)); return
    after=d.save(); report.append('%s %s -> %s  (%d edits)'%(label,d.md5_before,after,len(d.log)))

# ---------------- MAP 1 ----------------
d=Doc(M1,BK)
assert d.md5_before==EXPECT[M1], 'map1 changed since survey'
d.sub(4370,'lines',"Every new moon Grok's tax-men come for our grain. Short a sack, and a barn burns.",
      "No Gift in Millbrook, so no soldiers for Millbrook. Every new moon Grok's tax-men come for our grain instead. Short a sack, and a barn burns.")
d.sub(4363,'lines',"The well's gone bitter since spring. Tastes like an old copper pot.",
      "The well's gone bitter since spring - since the goblins started digging up at the stone ring. Tastes like an old copper pot.")
d.sub(4311,'hint_text','- Todd"', N('''- Todd"

Pasted over an older notice, under a red wax seal: "BY GRACE OF THE ETERNAL KING - every child of age who takes the Gift at the Chapel of the Vale is AWAKENED, and kept under the King's protection." No soldier has ever come to collect a child from Millbrook.'''))
d.sub(4398,'hint_text','the mantra scroll my father brought down from the monastery','the mantra scroll my father brought down from the old cloister on the ruins road')
d.set(3032,'hint_text',N('''A dying seer whispers: "The hooded one came in spring, swinging a red lantern and singing. He said if we dug up the stone ring, our sick young would heal. We dug. The water turned green. Grok drank his medicine... and became something else."

"Before the green water, our grandmothers' grandmothers had names like yours."'''))
d.sub(3036,'hint_text',"and the water in it smells green.","and the water in it smells green. The slabs heaved out of it bear a noble house's crest, worn almost smooth.")
d.sub(4439,'hint_text',"A ruin-warden's copper flask","A cloister brother's copper flask")
d.set(3037,'intro_line3',"And you... you smell of nothing. No Gift in you at all.")
d.sub(4388,'hint_text',"Then the ring shudders, and the stones begin to sink...","Then the ring shudders. Something deep below beats, once, like a heart - and the stones begin to sink...")
d.set(2981,'title',"The Seer's Tent")
d.set(2982,'title',"Cloister Ruins")
d.sub(3034,'stage_name',"ruins=Ruins Path","ruins=Cloister Ruins")
done(d,'map1')

# ---------------- MAP 2 ----------------
d=Doc(M2,BK)
assert d.md5_before==EXPECT[M2], 'map2 changed since survey'
d.set(1041,'text',N('''Hierophant Absalom came down to us with a body: legs, a right arm, a right eye, stitched onto a frame of red Ether and sewn shut. The thing his people call SPECIMEN ZERO. It breathes. It has never once been anyone.

He wanted my family's relic for it - the left eye, kept in Luke's chest since the year they killed Him. I gave the Eye a body of its own instead.

It sings to anyone who has drunk of Him. Green or red, it makes no difference: not one of the Hierophant's knights could stand before it.

The Sleeper is sealed in this vault. It wakes every hundred years. If the Eye is ever lost, the Sleeper must never learn of it.   - M.'''))
d.sub(1045,'text',"When it is ripe, we drink IT - and goblin-kind stands beside the gods.","When it is ripe, every serf in Vael drinks of IT - and stands level with the deathless lords.")
d.set(1045,'title',"Journal of Malric")
d.sub(1013,'text',"Forty barrels to the Eye this week.","Forty barrels drawn for the Gift this week.")
d.sub(1013,'text',"Malric's clerk scratched a mark","The old clerk - the goblin who was here before any of us - scratched a mark")
d.sub(1562,'text',"Day thirty-one since the Eye woke.","Day thirty-one since the Eye woke. A month before it did, a stranger with a red lantern came down the old ring shaft and walked straight past the crew, singing.")
d.sub(1069,'text',"Specimen G-7","CROWN ETHER-WORKS - by order of House Vey.\\n\\nSpecimen G-7")
d.sub(1841,'text',"in the ninth year of the Drowning, when the Choir still called itself a church of mercy","in the first years after the Angel Bird, when the Children of Light were still what their name said")
d.sub(1513,'relic_text',"a knot of violet roots","a knot of red roots")
for k,v in (('relic_color',RED_HI),('wake_color',RED_HI),('card_color','#5a1e28')): d.set(1513,k,v)
d.sub(701,'relic_text',"a core of pure, blue light.","a core of pure, blue light. It searches you for something to hold, finds nothing - and holds on.")
d.sub(1817,'hint_text',"ICHOR FLASK. Throw raw Ichor","BLIGHT FLASK. Throw raw Ether")
for oid in (1040,1086,1087,1088,1225,1388,1465,1466,1467,1468): d.set(oid,'color',RED_HI)
for oid in (1599,1600): d.set(oid,'color',RED_LO)
d.set(640,'ambient_color','#c8949c')
d.gsub('Culture Hall','Vat Hall')
d.gsub('PUMP ROOM','PUMP CONTROL')
d.gsub('Pump Room Key','Pump Control Key')
d.gsub('pump-room door','Pump Control door')
d.gsub('Pump Room key','Pump Control key')
d.gsub('pump room','pump control room')
d.set(631,'title','The Vat Hall')
done(d,'map2')

# ---------------- MAP 3 ----------------
d=Doc(M3,BK)
assert d.md5_before==EXPECT[M3], 'map3 changed since survey'
d.sub(4494,'hint_text',"By order of the Choir of the Requiem.","By order of the Soul Requiem.")
d.sub(5170,'text',"three hundred years","four hundred years")
d.set(4502,'hint_text',"AUREL VEY. First of the House. He held the cup at the lake.")
d.sub(5190,'hint',"from the first Named to the last","from the first who drank to the last")
d.set(5194,'title',"Sermon of the Gift")
d.set(5194,'text',N('''The Eternal King gives, and his Gift answers.

The serf drank, and worked without rest. The soldier drank, and did not fall. The noble drank of the older water,

and did not die.

Give thanks for the Gift. Ask nothing more of it.'''))
d.set(5181,'text',N('''Pages torn from the Book of Names, blown out across the flagstones.

...every child of the Vale, in its twelfth year, is brought to the Chapel to drink of the Font. Only then is its name written in the Book of Names.

A name written is a soul bound. A soul bound is a soul kept.

- Chapter of the Naming, verse 4'''))
d.set(4516,'hint_text',"The Font of the Gift. Raw Ether wells up through it from the pipes below - and shrinks away from you.")
d.sub(4498,'hint_text',"A violet ward","A red ward")
d.sub(4519,'hint_text',"A violet ward","A red ward")
d.gsub("For the first time in a thousand years","For the first time in seven hundred years")
d.sub(5430,'intro_line1',"The Church would pay a great deal for you.","My King would pay a great deal for you.")
d.set(5430,'intro_line3',"Kneel, and I will cut the Eye out of you gently. You will hardly miss the rest.")
d.set(4520,'intro_line2',"The Ascended never rose. He was held down at the lake - by ringed hands, eleven hundred years ago.")
d.set(4520,'intro_line3',"Give me the Eye, and the Choir will lift Him up at last.")
d.sub(4521,'depart_text',"The violet light","The red light")
d.sub(4521,'open_text',"Violet light","Red light")
d.sub(5226,'text',"and the hands on him wear the rings of the great Houses.","and the hands on him wear the rings of the great Houses. Above them, white birds. Beside His heart, one hand that wears no ring at all.")
d.set(4429,'title',"The Precentor's Court")
for oid in (5180,5189,5249,5418): d.set(oid,'color',RED_HI)
d.set(4430,'ambient_color','#e0a8b0')
done(d,'map3')

# ---------------- MAP 3b ----------------
d=Doc(M3B,BK)
assert d.md5_before==EXPECT[M3B], 'map3b changed since survey'
d.set(5252,'title',"The Purified King")
d.set(5252,'text',N('''He drank the most, and he drowned the first.

A boy from a poor village in the east swore this blade to a broken kingdom, and made it whole. Then the field of six hundred thousand woke something, and the blade drank the rest of the war for him.

The Ascended's light burned it out of him. He walked into these marshes and did not walk out. No one ever found the body; no one was meant to. His hands are still folded over the hilt.'''))
done(d,'map3b')

# ---------------- QUESTS / ASSETS ----------------
def textfile(path, subs, label):
    import shutil
    os.makedirs(BK,exist_ok=True); shutil.copy2(path,os.path.join(BK,os.path.basename(path)))
    s=open(path,encoding='utf-8').read(); miss=[o for o,n in subs if o not in s]
    if miss: report.append('!! %s miss %s (not saved)'%(label,miss)); return
    for o,n in subs: s=s.replace(o,n)
    open(path,'w',encoding='utf-8',newline='').write(s); report.append('%s ok (%d)'%(label,len(subs)))
Q=os.path.join(A,'Resources','Quests'); W=os.path.join(A,'Resources','Weapons'); S=os.path.join(A,'Resources','Skills')
textfile(os.path.join(Q,'Map1_GreystonePlains.json'),[('Follow the Ruins Path east','Follow the Cloister Ruins east')],'quest1')
textfile(os.path.join(Q,'Map2_UndercityOfVael.json'),[('Open the Pump Room','Open the Pump Control')],'quest2')
textfile(os.path.join(Q,'Map3_ValeOfGraves.json'),[('Mausoleum Forecourt',"Precentor's Court")],'quest3')
textfile(os.path.join(S,'IchorFlask.asset'),[('displayName: Ichor Flask','displayName: Blight Flask'),('a flask of raw Ichor','a flask of raw Ether'),('born of Ichor','born of Ether')],'skill_ichor')
textfile(os.path.join(W,'Katana.asset'),[
 ('A travelling smith in a market town hammered it out in four days and took payment in coin, not in oaths.',
  'Old Corwen bought it from a travelling smith in a market town and put it in Ren\'s hands the winter Ren turned twelve, with one rule: draw it for other people.'),
 ('Ren has carried it through eleven winters of contract work. ','')],'katana_ronin')
textfile(os.path.join(W,'Katana_Moonlit.asset'),[
 ('epithet: Relic of the Ascended - kept lit for a thousand years','epithet: Relic of the Children of Light - its lamp never once let go out'),
 ('in the years when the Choir still called itself a church of mercy','in the first years after the Angel Bird, when the Children of Light were still what their name said'),
 ('When the Requiem came,','When the Children curdled into the Soul Requiem,')],'katana_moonlit')
textfile(os.path.join(W,'Katana_Crimson.asset'),[
 ('epithet: Oath-blade of the Great Houses - sworn at the Requiem','epithet: Blade of Borrow, the Purified King'),
 ('Six were made and six were sworn, one to each house that set its ring to the deed. The oath was read aloud over the quench: that no power above a man would stand above the houses again, and that the blade would answer for the hand that broke faith.\\n\\nThey quenched it in blood rather than oil. It still drinks that way.',
  'A boy from a poor village in the east swore it to a broken kingdom, and made the kingdom whole. Then the field of six hundred thousand woke something, and the blade drank the rest of the war for him. It still drinks that way.'),
 ('This one was buried with the man who carried it, under a mound nobody tends, his hands folded over the hilt. The coffin plate gives a house and no name. Whoever put him here wanted the oath buried quietly, and went to some trouble to do it a long way from any road.',
  'The Ascended\'s light burned the war out of him. He walked into the Sinking Fields and did not walk out, and no one ever found the body. His hands are still folded over the hilt.')],'katana_crimson')
print('\n'.join(report))
