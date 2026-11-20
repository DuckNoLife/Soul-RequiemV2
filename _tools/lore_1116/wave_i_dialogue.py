# Đợt I (16/11, §260): soát + sửa chữ Map 1–3 theo lore mới; Pell (Map 2) có lời theo tình huống. Kiểm md5, tự sao lưu.
import sys; sys.path.insert(0,'.')
from patch_lib import Doc
G='../../Assets/GamePackage/'; BK='../../_backups/2026-11-16_dialogue_audit'
N='\\n'
# ---------------- Map 1
d=Doc(G+'ERW - Grass Land 2.0 v2.1/TiledMap Editor/Maps/map1_greystone_plains.tmx',BK)
assert d.md5_before=='0691f429bb1b212eec6e4e0fc8f92edc', d.md5_before
d.sub(4452,'script',"bubble ren: The ground... it's beating. Like something under it has a heart.",
      "bubble ren: The ground... it's moving. Like something down there is turning in its sleep.")
d.sub(4388,'hint_text',"Something deep below beats, once, like a heart - and the stones begin to sink...",
      "Something deep below turns over, like a sleeper in the dark - and the stones begin to sink...")
d.sub(4371,'lines',"Bring back Grok's tusk and I'll hang it over the village gate.",
      "Bring back Grok's tusk and I'll hang it over the village gate. | My grandfather swore an old swordsman out of the three kingdoms lived out his days in the copper mine. Never once drew his blade, they said. The deep shafts caved in long before my time.")
print('M1',d.err,d.save())
# ---------------- Map 2
d=Doc(G+'EPIC RPG World - Sewers V1.5/TiledMap Editor/Maps/map2_undercity_of_vael.tmx',BK)
assert d.md5_before=='8a44ca3f737a96aa314e20b4462becca', d.md5_before
d.set(1853,'script',N.join([
 "bubble pell: Don't strike! I'm not one of the mad ones. Not yet.",
 "say Pell: My name is Pell. I kept Malric's books, long ago - his youngest student, before the green made me what I am.|He was a man, you know. A scholar. He wanted every serf in Vael to stand level with the deathless lords.|He did terrible things for it. So did I. I held the knife when we fed the Sleeper.|In the end he chained the Eye with his own dying breath. The pumps, the pipes, the brimming basin - all of it is his seal.|It calls to everything with the green in it. Every night it asks me to open the basin. Every night I sweep a little louder.|You want the crank. It is the only way out, and you will take it. Just... know what you are turning.",
 "bubble ren: I'm sorry. I have to get out.",
 "bubble pell: He said someone would. He hoped they would at least be sorry."]))
d.set(1852,'lines',"He built that seal with the last of his life. Please - don't be the one who undoes it. | Twenty years he worked down here. I have been sweeping his floor for four hundred. | The Eye cannot call you, can it? You never drank. I knew the moment you walked in - you smell of nothing at all. | If you find his last page, bring it to me. He hid it from the Hierophant. He hid it from me, too.")
d.set(1852,'lines_1_when','mercy_2')
d.set(1852,'lines_1',"I have read it nine times now. His hand shakes on the last line. | Four hundred years, and he finally said it. | Go on. You still have to turn that crank. I know.")
d.set(1852,'lines_2_when','ichor_diverted')
d.set(1852,'lines_2',"I heard the basin empty from here. Forty barrels a week for four hundred years - and it took you one turn. | The Eye is awake now, and it has your scent. Go, before it learns anything else about you. | If it takes you... I will sweep that floor too.")
d.set(1852,'lines_3_when','ichor_diverted,mercy_2')
d.set(1852,'lines_3',"You kept his page and broke his seal in the same night. He would have laughed at that. | The Eye is awake. Go. And - thank you, swordsman.")
d.add('triggers','pell_after_pumps','story',9.5,36.5,1,1,{
 'trigger':'enter','radius':'1.3','when':'ichor_diverted','unless':'r8_cleared',
 'script':N.join(["say Pell: So. It is done.|I felt it through the floor. The basin is draining, and the Eye is waking angry.|Don't look at me like that. He knew someone would come. He only hoped it would be someone who could walk away from what is in that pit.",
                  "bubble pell: Can you? We will see."])})
d.add('triggers','pell_after_sleeper','story',9.5,36.5,1,1,{
 'trigger':'enter','radius':'1.3','when':'vault_cleared','unless':'r8_cleared',
 'script':N.join(["say Pell: You went down to the Sleeper - and came back.|Malric drove a holy blade into it to buy a hundred years at a time. You pulled out what it was built on. It will not stand up again.|The Hierophant will come for its body all the same. He does not like to lose a single bone."])})
d.sub(1041,'text',"No eyes. No heart.","No eyes - his men set dead eyes in the vault walls to watch for it. No heart.")
print('M2',d.err,d.save())
# ---------------- Map 3
d=Doc(G+'EPIC RPG World Pack - Cemetery V 1.6/TiledMap Editor/Maps/map3_vale_of_graves.tmx',BK)
assert d.md5_before=='0ab5494563a0ae1dcc43c671c2ceb461', d.md5_before
d.sub(5471,'script',"With that green light in your chest.","With that light in your chest. Blue, too - funny, they say the thing in the pit glowed green.")
d.set(5226,'text',"Moonlight floods the glass, and the picture is not the one in the prayer books. No glory, no gold. A figure lies on its back above dark water, and many hands reach over it - each hand wearing the ring of a great House. White birds rise from the water. Close to the figure's chest, one hand wears no ring at all.")
print('M3',d.err,d.save())
