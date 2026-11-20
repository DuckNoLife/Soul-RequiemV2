# Map 4 — BƯỚC 3 PHẦN 3 (§306): CỐT TRUYỆN. Chữ trong game tiếng Anh (BẢN NHÁP — chờ người dùng duyệt từng câu).
# Người dùng chốt 17/11: Pell gỡ phong ấn Mantra khi được thả; đoàn Black Lantern = đèn đỏ + lời kể; "Ren" là tên mẹ đặt (khâu vào góc tã).
# Thứ tự lộ sự thật: sổ Candidate (Ren = VII) -> thư mẹ (tên Ren, parents_heard) -> biên niên Keepers (tên DARWIN + tranh Angel Bird)
#   -> lời tiên tri gốc có ấn vua -> sổ kiểm kê Reliquary ("Let them carry it.") -> Hall (đợt sau). Sổ tên 6 trang (đọc hết = name_reclaimed).
# Trong giá trị thuộc tính: '\\n' = xuống dòng (literal \n trong Tiled), '|' = sang trang (say / narrate / slide).
from m4_setup import objs as O
import decor2 as D
T = 32
TRIG = ('unity:IsTrigger', True)
f = lambda v: (str(v), 'float')
def obj(group, name, typ, x, y, w, h, props, shape=None):
    return O.obj(group, name, typ, x * T, y * T, w * T, h * T, props=sorted(list(props) + [TRIG], key=lambda p: p[0]), shape=shape)
def scene(name, x, y, w, h, script, trigger='enter', **kw):
    p = [('script', '\\n'.join(script)), ('trigger', trigger)] + [(k, v) for k, v in kw.items()]
    obj('triggers', name, 'story', x, y, w, h, p)
def note(name, x, y, w, h, title, text, sets=None, verb='Read', rng=1.0, extra=()):
    p = [('mechanic', 'note'), ('marker', True), ('range', f(rng)), ('text', text), ('title', title), ('verb', verb)] + list(extra)
    if sets: p.append(('sets', sets))
    obj('mechanics', name, 'note', x, y, w, h, p)

# ===================================================================== THUỘC TÍNH MAP: trói khi vào Map 4 (MapRestraint)
MAP_PROPS = [('disarm_until', 'm4_katana'), ('disarm_text', 'Your blade is gone. The warders took it.'),
             ('mantra_seal_until', 'm4_mantras_free'), ('mantra_seal_text', 'Your mantras are bound. The binding drinks every word.')]

# ===================================================================== PELL — 3 chỗ đứng cố định (người dùng chốt §296)
def pell(name, x, y, face, lines, extra=()):
    obj('npcs', name, 'villager', x - 0.5, y - 0.5, 1, 1, [('face', face), ('kind', 'pell'), ('lines', lines), ('scale', f(0.52)),
        ('speaker', 'Pell'), ('walk', False)] + list(extra))
pell('pell', 93.5, 96.6, 'S',
     "Swordsman? Here - by the bars. They dragged you past me a day ago. | The winch outside my cell. It's chained through the gear and locked to the floor ring. | Bare hands won't do it. They threw a blade on the warders' table like a prize. Find it.",
     [('hide_when', 'pell_left'), ('lines_1', "You found it. Now the chain - cut it, then turn the gear."), ('lines_1_when', 'm4_katana')])
pell('pell_g', 58.6, 68.6, 'W',
     "This gate answers to its chain, not a key. Come close and I'll haul it.",
     [('show_when', 'pell_left'), ('hide_when', 'skull_gate_open')])
pell('pell_o', 49.0, 58.2, 'E',
     "Two seals lock the way down to the Hall. The Registrar keeps one in the Scriptorium, west. | The other sits in the Royal Reliquary, east. The King's own vault - down here, under a cult's floor. Ask yourself why. | Rest when you need to. Old bones like mine are good at waiting.",
     [('show_when', 'skull_gate_open'),
      ('lines_1', "You found her letters. She wrote to a man who never once wrote back. | I knew your face was familiar. I swept the floor outside her cell."), ('lines_1_when', 'parents_heard'),
      ('lines_2', "Both seals. Then the way is open - and so is everything that waits behind it. | I'm coming with you. Don't argue with a man four hundred years older than your grandmother."), ('lines_2_when', 'seal1_set,seal2_set')])

# ===================================================================== CẢNH
scene('m4_wake', 105.0, 109.0, 6.0, 4.0, [
    'fade out 0.01', 'wait 0.5',
    'narrate The blow from behind. Then the sway of being carried - down, and down.|A hand on the back of your neck. A voice counting in a language with no vowels.|Then stone, and cold, and nothing.',
    'fade in 2.0', 'wait 0.8',
    'bubble ren: A cell. ...Someone was waiting behind me when Morrow fell.',
    'bubble ren: My blade is gone. They took my clothes, too - this is offering linen. The robe they put on the ones they give away.',
    'bubble ren: The Eye - I can still feel it, but something is pressing it down. A binding.',
    'wait 0.6', 'shake 0.03 2.5',
    'narrate Above the cell, many feet pass in step. Chains drag across stone. Something enormous is being carried.',
    'shake 0.07 1.0', 'set m4_tremor', 'wait 0.5',
    'bubble ren: The gate jumped its rail. Whatever that was, it just opened my cell.',
    'hint Find your blade.'], trigger='start', unless='m4_tremor')
scene('pell_freed', 92.0, 101.0, 4.0, 2.0, [
    'wait 0.4', 'walk pell 93.5,101.6', 'face 93.5,101.6',
    'say Pell: Four hundred years I swept a dead man\'s floor, and the Choir thought a cage would hold me.|They carried you down past my bars. The one with the Eye in him.',
    'say Pell: Hold out your hand. They put a binding on you while you slept.|Malric taught me how these are made. And how they are unmade.',
    'glow 93.5,101.6 #8fd0ff 2.0 1.2', 'shake 0.02 0.4', 'set m4_mantras_free',
    'hint Your mantras answer you again.',
    'bubble ren: ...It\'s gone. I can breathe again.',
    'say Pell: The Choir drains this whole prison through the old culverts, and I still fit through them.|Meet me at the skull gate - north, past the Rolling Hall and the blade yard. Go carefully. They built this place for people like us.',
    'fade out 0.6', 'set pell_left', 'fade in 0.6'], trigger='flag', when='pell_free')
scene('lantern_line', 52.0, 75.0, 9.0, 3.0, [                     # bờ bắc cầu treo: nhìn sang gờ xa phía tây
    'face 15,70', 'cam 15,70 1.1', 'wait 0.6', 'shake 0.02 3.0',
    'glow 1,70 #ff3b2f 1.3 3.0', 'wait 0.5', 'glow 4,70 #ff3b2f 1.3 3.0', 'wait 0.5', 'glow 7,70 #ff3b2f 1.3 3.0',
    'narrate Across the pit, on a ledge no one could reach, red lanterns move in a long and patient line.|Between them, on poles and chains, they carry something far too large to be a man.',
    'glow 11,70 #ff3b2f 1.3 3.0', 'wait 0.5', 'glow 15,70 #ff3b2f 1.3 3.0', 'wait 0.5', 'glow 19,70 #ff3b2f 1.3 3.0', 'wait 0.5', 'glow 23,70 #ff3b2f 1.3 3.0',
    'wait 1.2', 'camren',
    'bubble ren: Black Lantern. Whatever that is, they carry it like a saint.'], unless='lantern_seen')
scene('skull_open', 53.0, 67.0, 7.0, 2.0, [
    'bubble pell_g: There you are. You crossed the blades. Malric would have liked you.',
    'say Pell: They brought it through here not an hour ago - the Hierophant\'s Specimen. The Black Lantern carried it down to the Hall of Naming.|Whatever he is building, he is building it out of the dead.',
    'say Pell: Stand back.', 'walk pell_g 56.5,67.6', 'shake 0.04 0.8', 'set skull_gate_open',
    'say Pell: The Ossuary is below - the keepers\' dead are kept there. Rest. You will need it.'], when='pell_left', unless='skull_gate_open')

# ===================================================================== GHI CHÉP
# 1) Sổ Candidate (bục Sổ Tên, bệ tây bắc Scriptorium): Ren = Candidate VII
note('register_vii', 9.4, 49.3, 1.2, 1.2, 'The Register of Candidates',
     "Kept by the Registrar of the Choir. Each Candidate is entered at the Naming and struck at the end.\\n\\n"
     "I. Taken 1069. Made hollow. The Eye did not come. Struck.\\n"
     "II. Taken 1081. Died in the second year of silence. Struck.\\n"
     "III. Taken 1094. Spoke a name in her sleep. Corrected. Struck.\\n"
     "IV. Taken 1110. Made hollow. Reached for the Eye and burned. Struck.\\n"
     "V. Taken 1127. Ran. Returned. Struck.\\n"
     "VI. Taken 1140. Hollow. Kept. Given the right eye of the Ascended, that it may see what it waits for. NOT struck.\\n"
     "VII. Born 1156 in the lower cells, of a Nameless woman. Taken at the first breath, before she could name it. "
     "Lost the same winter on the west road, when the cart went into the river at the ford. Presumed drowned. Struck.\\n\\n"
     "Under VII, in newer ink: Not drowned. Raised at Corwen's ford. He carries the Eye. Bring him to the Hall.",
     sets='m4_candidate_vii')
scene('vii_read', 9.0, 49.0, 2.0, 2.0, ['wait 0.3', 'bubble ren: Seven. ...Corwen\'s ford. That\'s me. I was the seventh.'], trigger='flag', when='m4_candidate_vii')
# 2) Sổ tên — 6 trang trên 6 bàn chép (đọc hết -> name_reclaimed)
PAGES = [
    "Struck at the Naming, year 1101:\\nAsha of Millbrook. Tomas Reed. Old Wenna, who would not drink. Hal and his brother Hob. A girl from Harlan's farm, no name given.",
    "Struck at the Naming, year 1119:\\nCorran the miller's son. Isbet Dunmore. Pip. Mother Gale. Two travellers from the west road, names not asked.",
    "Struck at the Naming, year 1133:\\nMirelle. Ansel of the ford. Brannoc the tinker. The Askew twins. A boy who said his name was Fox.",
    "Struck at the Naming, year 1148:\\nWillem Hale. Nan Ostry. Edda. Edda's daughter. Rook. A woman who gave her name only as 'Mine'.",
    "Struck at the Naming, year 1160:\\nBeck. Lia Thorne. Garrow. Old Mott, who sang. Three goblin children, names unknown, struck anyway.",
    "Struck at the Naming, year 1171:\\nSela. Tam. Jory Ashdown. Kell of Greystone. Wren. The page ends with a column of names scratched out so hard the pen went through."]
for i, (cx, by) in enumerate(((20.0, 54.0), (27.5, 54.0), (20.0, 57.0), (27.5, 57.0), (20.0, 60.0), (27.5, 60.0)), 1):
    note('names_p%d' % i, cx - 1.3, by - 1.0, 2.6, 1.3, 'A Page of Struck Names',
         PAGES[i - 1] + "\\n\\nEvery name has a single neat line through it.", sets='names_p%d' % i, verb='Read the names', rng=1.2)
scene('names_all', 17.0, 52.0, 14.0, 9.0, [
    'wait 0.4', 'narrate You read every name in the register aloud, one after another, until the Scriptorium is full of them.|No one has said them in this room for a very long time.',
    'set name_reclaimed', 'bubble ren: They had names. All of them.'], trigger='flag', when=','.join('names_p%d' % i for i in range(1, 7)))
# 3) Thư mẹ (bục giữa phòng thư) -> parents_heard
note('letters_note', 17.8, 34.0, 1.4, 1.2, 'Letters, Never Sent',
     "A bundle tied with thread. Every letter is addressed to the Hierophant. None was ever opened.\\n\\n"
     "- I am the woman in the lower cells. You have my son. I do not want the honour you say he will have. I want him.\\n\\n"
     "- They let me copy in the Scriptorium now, so my hands are useful. I write his name in the margins where no one reads.\\n\\n"
     "- I named him before they took him. I never said it aloud. I sewed it into the corner of his blanket while I was carrying him, "
     "in thread the colour of the blanket, so they would not see. Ren. His name is Ren.\\n\\n"
     "- They say the cart went into the river at the ford. They say he drowned. Rivers carry things. Let someone kind find him. "
     "Let them read the corner of the blanket.\\n\\n"
     "- This is the last one. They have stopped bringing paper. If you ever read this: he was never yours.",
     sets='parents_heard', extra=[('key_desc', 'A bundle of letters to the Hierophant, never opened. His mother wrote them.'), ('key_id', 'mother_letters'),
                                  ('key_kind', 'Document'), ('key_name', 'Letters, Never Sent'), ('pickup', True)])
scene('letters_read', 16.0, 33.0, 5.0, 4.0, ['wait 0.4', 'bubble ren: Ren. ...She gave it to me. It was always mine.'], trigger='flag', when='parents_heard')
# 4) Biên niên Keepers (bục mới, Ossuary tây) -> tên DARWIN + tranh Angel Bird
D.C(112, 46.4, 51.6, block=(1.0, 0.5)); D.C(111, 46.4, 50.95, 'props front', name='keepers_chronicle_book')
note('keepers_chronicle', 45.8, 50.6, 1.2, 1.2, 'Chronicle of the First Keepers',
     "Copied in the third generation of the Children of Light.\\n\\n"
     "In the last year before the Count, King Thrag and his nobles put the Ascended to death on the shore of Nova. "
     "It was not their thought. It was the counsel of the King's friend, who had walked beside the Ascended and knew what a crown should fear: "
     "a man the people loved more than any king.\\n\\n"
     "His name was DARWIN. The disciple's diary that came to us had that name cut out of it, as if paper could be ashamed. We write it here whole.\\n\\n"
     "When it was done, Darwin named Thrag the murderer, and the people made Darwin king in his place. The kingdom took a new name: Vael.\\n\\n"
     "Luke and Emma, who saw it, carried away what they could - an eye, a hand - and went where no king would follow. "
     "The rest was left to us to keep. In the first hundred years the King's men came down with lanterns and took it, piece by piece, into his Reliquary.",
     sets='chronicle_read')
scene('angel_bird', 44.0, 50.0, 5.0, 4.0, [
    'wait 0.3', 'fade out 0.6',
    'slide Story/angel_bird_1: On the shore of Nova, a king and his nobles put to death the one the people called the Ascended.|The order was not the king\'s. It was his friend\'s.',
    'slide Story/angel_bird_2: They laid their hands on what was left of him, and each one called it his right.',
    'slide Story/angel_bird_3: His blood ran down the steps into the lake. The nobles knelt, and drank, and never died again.',
    'slide Story/angel_bird_4: When he died, a flight of white birds rose from the water and did not come back.|They named that year Angel Bird. Everything since has been counted from it.',
    'slide end', 'fade in 0.8',
    'bubble ren: Darwin. The King\'s friend became the King - and he never died.'], trigger='flag', when='chronicle_read')
# 5) Lời tiên tri gốc (rương kho kiểm kê)
note('prophecy_original', 98.4, 27.4, 1.4, 1.2, 'The Prophecy of the Eye',
     "The original leaf, older than any copy in the Scriptorium.\\n\\n"
     "THE EYE SHALL BE FREED BY ONE WHO HAS NO NAME.\\n\\n"
     "Written in a careful royal hand, and sealed beneath with the crown and three drops of red wax: the seal of the Eternal King.\\n\\n"
     "A note pinned to it, in the Hierophant's hand: Given to the Choir in 476, the same year we were let into the Reliquary. A gift, we thought.",
     sets='prophecy_read')
scene('prophecy_seen', 97.0, 27.0, 4.0, 3.0, ['wait 0.3', 'bubble ren: The King\'s seal. He wrote it. He sent them hunting for someone like me.'], trigger='flag', when='prophecy_read')
# 6) Sổ kiểm kê Royal Reliquary (bục kho)
note('inventory_note', 92.3, 32.0, 1.4, 1.2, 'Inventory of the Royal Reliquary',
     "Relics of the Ascended, as entered by the King's Keeper.\\n\\n"
     "The Heart - kept upon the King's own person.\\n"
     "The Left Eye - lost in the year of Angel Bird (the disciple Luke).\\n"
     "The Left Hand - lost in the year of Angel Bird (the woman Emma).\\n"
     "The Spine - removed 476, by the Hierophant's people.\\n"
     "The Ears - removed 476, by the Hierophant's people.\\n"
     "The Right Eye - removed 476, by the Hierophant's people.\\n"
     "The Blood - the lake at Nova. The house of Vey drinks.\\n\\n"
     "Beside every empty line, in a different hand, the same three words: Let them carry it.",
     sets='inventory_read')
scene('inventory_seen', 91.0, 31.0, 5.0, 3.0, ['wait 0.3', "bubble ren: 'Let them carry it.' He let them steal it - so someone else would carry it all back to him."],
      trigger='flag', when='inventory_read')
