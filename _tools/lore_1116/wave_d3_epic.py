import sys,re; sys.path.insert(0,'.')
from patch_lib import Doc
d=Doc('../../Assets/GamePackage/ERW - Grass Land 2.0 v2.1/TiledMap Editor/Maps/map1_greystone_plains.tmx','../../_backups/2026-11-16_waveD3_epic')
assert d.md5_before=='1d2b804ccc7a04b5b6a33df4f05695bf', d.md5_before
oid=int(re.search(r'<object id="(\d+)" name="prologue"',d.src).group(1))
lines='''
fade out 0.01
slide Story/epic_three_crowns: I. THE THREE CROWNS|Before the Kingdom of Vael, there were three: Arden in the west, Kroven in the east, and Miravel between them - the richest of all.|For 300 years they traded, married, and kept the peace.
slide Story/epic_war: II. THE WAR|In the winter of 34 Before the Crown, King Arthur of Miravel died without an heir. Arden and Kroven marched on his empty throne.|Four years of war ended on a single field. 600,000 men fell there, and the rivers of the east ran red.
slide Story/epic_butcher_king: III. THE BUTCHER-KING|From the ashes rose Borrow, a peasant's son who took Miravel by the sword - and then the world.|Kroven became a land of slaves. Arden burned. For twenty years Borrow sat his throne and called it peace.
slide Story/epic_ascended: IV. THE ASCENDED|Then a wanderer came out of the western fields. Where he walked, crops grew and the sick rose from their beds.|The people called him the Ascended. In the year 7 Before the Crown they followed him to Borrow's gates - and the Butcher-King fell.
slide Story/prologue_2: V. THE CROWN|But the Ascended was betrayed. Thrag, the last king of Arden, feared a holy man the people loved more than any crown - and put him to death.|His most faithful apostle, Darwin, struck the traitor down. He bound Arden, Miravel and Kroven into one realm, and named it Vael.|Heaven blessed him for it. He has not aged a day since.
slide Story/prologue_1: VI. THE ETERNAL KING|From his white palace, Darwin has ruled for 1,176 years.|Every coin bears his face. Every prayer ends with his name.
slide Story/epic_madness: VII. THE GIFT|In the Year 776, a madness rose from beneath the earth. Men turned on their neighbours. Beasts became monsters.|Four years later, the King opened a spring sealed since the death of the Ascended: the Tears of the Ascended, His last gift to His people.
slide Story/prologue_3: All who drank were spared, and made strong. Since that day, every child of age drinks the green water at the Awakening.|The Awakened serve the Crown. The Crown protects them.
slide Story/prologue_4: Those who never drink are the Unawakened. No soldier marches to save their villages.|And in the hills live the goblins - the ones the madness took before the Tears could reach them. So the priests say.
slide Story/prologue_5: In the old graveyards a heresy endures: the Soul Requiem.|They spit on the King's blessing and sing that their dead god will rise again. The Crown hunts them like wolves.
slide Story/prologue_6: YEAR 1176 OF THE ETERNAL KING|Ren is a wandering swordsman of the eastern roads. He has never taken the Gift.|He draws his blade for those no soldier will defend. It pays poorly. He keeps doing it.|This year the ground itself has begun to tremble across the Greystone Plains, and no one can say why.
slide Story/prologue_7: On the morning of the new moon, smoke rises over the village of Millbrook.|Ren quickens his pace.
slide end
fade in 1.2
'''
d.set(oid,'script','\\n'.join(l.strip() for l in lines.strip().split('\n') if l.strip()))
print(d.err, d.save())
