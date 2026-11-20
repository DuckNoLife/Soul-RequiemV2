import sys, os, re
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from patch_lib import Doc
ROOT=os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)),'..','..'))
G=os.path.join(ROOT,'Assets','GamePackage'); BK=os.path.join(ROOT,'_backups','2026-11-16_waveD2_prologue')
M1=os.path.join(G,'ERW - Grass Land 2.0 v2.1/TiledMap Editor/Maps/map1_greystone_plains.tmx')
M2=os.path.join(G,'EPIC RPG World - Sewers V1.5/TiledMap Editor/Maps/map2_undercity_of_vael.tmx')
M3=os.path.join(G,'EPIC RPG World Pack - Cemetery V 1.6/TiledMap Editor/Maps/map3_vale_of_graves.tmx')
EXPECT={M1:'9d66595b7e2eb22a7036f203825a809c',M2:'8d0e0d06bec59f33a19c7ff93c12c5f2',M3:'b3a87fd34c7aac10b7c902837a64246f'}
def S(t): return '\\n'.join(l.strip() for l in t.strip().split('\n') if l.strip())
def oid(d,name): return int(re.search(r'<object id="(\d+)" name="%s"'%re.escape(name),d.src).group(1))
out=[]
def done(d,l): out.append('%s %s'%(l,d.err if d.err else d.save()))

# ---- Map 1: mở đầu = điều ai cũng biết + hoàn cảnh của Ren (KHÔNG lộ sự thật Angel Bird)
d=Doc(M1,BK); assert d.md5_before==EXPECT[M1]
d.set(oid(d,'prologue'),'script',S('''
fade out 0.01
slide Story/prologue_1: This is the Kingdom of Vael.|For eleven hundred and seventy-six years, one king has sat its throne. He does not age. He does not die. His people call him the Eternal King.
slide Story/prologue_2: Every child knows the story.|Long ago a holy man walked among us - the Ascended. The mad king Thrag put him to death.|His most faithful apostle, Darwin, cut the tyrant down. Heaven granted him a life without end, that he might guard the faithful forever.
slide Story/prologue_3: Four centuries ago, the King gave his people a Gift.|Every child of age drinks the green water at the Awakening. The Awakened grow strong, and serve, and are protected.
slide Story/prologue_4: Those who never drink are the Unawakened. No soldier marches to save their villages.|And in the hills live the goblins. Some say they were people once. No one says it loudly.
slide Story/prologue_5: In the old graveyards a heresy endures: the Soul Requiem.|Hunted by the Crown, they sing that their dead god will rise again.
slide Story/prologue_6: Ren was raised by the Corwens, farmers in the far east of the kingdom.|They gave him a plain blade and one rule: draw it for other people.|He has never taken the Gift. No one has ever asked him to.
slide Story/prologue_7: Year 1176. For weeks the ground has trembled across the Greystone Plains.|On the night of the new moon, Ren reaches the village of Millbrook - and it is burning.
slide end
fade in 1.2
'''))
done(d,'map1')

# ---- Map 2: mảnh ghép 1 — nhật ký Luke (tên kẻ phản bội bị xé)
d=Doc(M2,BK); assert d.md5_before==EXPECT[M2]
d.add('triggers','luke_diary','sign',13,19,1,1,{'zone_id':'arch','verb':'Read','speaker':"Luke's Diary",'sets':'luke_diary_read',
  'hint_text':"A crumbling diary in a hand far older than Malric's. On the cover, one word: LUKE. | ...they did not take Him in the night. They took Him at noon, on the shore of the lake, so that everyone would see. | Thrag gave the order. But it was not Thrag who brought the great Houses down to the shore. It was [the name is torn away] - our brother, who sat at His right hand. | Emma and I took what we could carry. His left eye. His left hand. Forgive us. It was all of Him we could save."})
done(d,'map2')

# ---- Map 3: sự thật Angel Bird ghép lại SAU cửa sổ hoa hồng (mảnh ghép cuối của Map 1–3; tên kẻ phản bội để dành Map 4)
d=Doc(M3,BK); assert d.md5_before==EXPECT[M3]
d.add('triggers','angel_bird_truth','story',59,73,1,1,{'trigger':'flag','when':'window_solved','script':S('''
wait 1.2
slide Story/angel_bird_1: Luke's diary. Aurel Vey's tomb. The sermon of the older water. And now this window.|Piece by piece, the story every child is taught comes apart.
slide Story/angel_bird_2: The Ascended was not killed by one mad king.|Many hands held Him down - and every one of them wore the ring of a great House.
slide Story/angel_bird_3: His blood ran into Lake Nova. Aurel Vey held the first cup.|The nobles drank of Him, and did not die. They have not died since.
slide Story/angel_bird_4: When He breathed His last, white birds rose from the water. Luke named that day the Angel Bird.|And one name in Luke's diary is torn away - the apostle who sat at His right hand.
slide end
fade in 0.8
bubble ren: Eleven hundred years... and every great House in Vael has been drinking from Him since.
''')})
done(d,'map3')
print('\n'.join(out))
