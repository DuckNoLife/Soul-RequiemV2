import sys,re; sys.path.insert(0,'.')
from patch_lib import Doc
d=Doc('../../Assets/GamePackage/ERW - Grass Land 2.0 v2.1/TiledMap Editor/Maps/map1_greystone_plains.tmx','../../_backups/2026-11-16_waveD5_gift')
assert d.md5_before=='4936d05fcb7e3b0dbcd4d44abbad5587', d.md5_before
oid=int(re.search(r'<object id="(\d+)" name="prologue"',d.src).group(1))
lines='''
fade out 0.01
slide Story/prologue_1: THE KINGDOM OF VAEL ~ For 1,176 years, one kingdom has stood where three once warred - and one king has ruled it for every one of those years.|He does not age. He does not die. His people call him the Eternal King.
slide Story/prologue_2: Every child knows how it began. ~ A holy man, the Ascended, ended the age of war - and was murdered by Thrag, a jealous king.|His most faithful apostle, Darwin, cut the murderer down and bound the warring crowns into one realm. Heaven blessed him with a life without end.
slide Story/epic_black_year: THE BLACK YEAR ~ In the Year 776, heretics of the Soul Requiem broke open a forbidden grave beneath the earth.|A madness poured out of it. Men turned on their neighbours. Beasts became monsters. Whole towns were lost.
slide Story/epic_first_cup: THE GIFT ~ In the Year 780, the Lady Isolde of House Vey knelt in the square of the capital and gave the first cup to a dying child.|The child rose. The crowd fell to its knees.|The King proclaimed it the Tears of the Ascended, sealed since His death for the day His people would need them. They shine green, like the fields that grew wherever He walked.
slide Story/prologue_3: Since that day, every child of age drinks the Tears at the Awakening. ~ The Awakened grow strong. The madness has never returned.
slide Story/epic_classes: THE ORDER OF VAEL ~ Above all stand the great Houses, blessed by Heaven at the King's side. They do not grow old.|Below them, the Awakened: soldiers and labourers, strong with the Gift and protected by the Crown.
slide Story/prologue_4: Last come the Unawakened - those who never drank. No soldier marches to save their villages.|And in the hills, the goblins: the ones the madness took before the Tears could reach them. So the priests say.
slide Story/prologue_6: YEAR 1176 OF THE ETERNAL KING ~ Ren is a wandering swordsman. He has never taken the Gift.|He draws his blade for those no soldier will defend. It pays poorly. He keeps doing it.|This year the ground itself has begun to tremble across the Greystone Plains. No one can say why.
slide Story/prologue_7: On the morning of the new moon, smoke rises over the village of Millbrook. ~ Ren quickens his pace.
slide end
fade in 1.2
'''
d.set(oid,'script','\\n'.join(l.strip() for l in lines.strip().split('\n') if l.strip()))
print(d.err, d.save())
