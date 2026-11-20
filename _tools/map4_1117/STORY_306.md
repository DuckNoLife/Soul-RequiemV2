# Map 4 — Lời thoại & ghi chép (BẢN NHÁP §306, chờ duyệt)

Sửa câu nào thì ghi lại số mục + câu mới; tôi sửa trong `_tools/map4_1117/story.py` rồi dựng lại map.

Ký hiệu: `|` = sang trang, xuống dòng giữ nguyên như trong game.

## 1. Tỉnh dậy trong xà lim (rung chấn)  (`m4_wake`)
- `fade out 0.01`
- `wait 0.5`
- `narrate Red light. A hand on the back of your neck. A voice counting in a language with no vowels.|Then stone, and cold, and nothing.`
- `fade in 2.0`
- `wait 0.8`
- `bubble ren: A cell. ...And my blade is gone.`
- `bubble ren: The Eye - I can still feel it, but something is pressing it down. A binding.`
- `wait 0.6`
- `shake 0.03 2.5`
- `narrate Above the cell, many feet pass in step. Chains drag across stone. Something enormous is being carried.`
- `shake 0.07 1.0`
- `set m4_tremor`
- `wait 0.5`
- `bubble ren: The gate jumped its rail. Whatever that was, it just opened my cell.`
- `hint Find your blade.`

## 2. Nhấc then cửa Oubliette  (`oub_bar`)
- `set oub_unbarred`
- `hint The bar lifts. The door was only barred from this side.`

## 3. Pell trong xà lim (nói qua song)  (`pell`)
- lines: Swordsman? Here - by the bars. They dragged you past me a day ago. | The winch outside my cell. It's chained through the gear and locked to the floor ring. | Bare hands won't do it. They threw a blade on the warders' table like a prize. Find it.
- lines_1: You found it. Now the chain - cut it, then turn the gear.
- lines_1_when: m4_katana

## 4. Thả Pell + gỡ phong ấn Mantra  (`pell_freed`)
- `wait 0.4`
- `walk pell 93.5,101.6`
- `face 93.5,101.6`
- `say Pell: Four hundred years I swept a dead man's floor, and the Choir thought a cage would hold me.|They carried you down past my bars. The one with the Eye in him.`
- `say Pell: Hold out your hand. They put a binding on you while you slept.|Malric taught me how these are made. And how they are unmade.`
- `glow 93.5,101.6 #8fd0ff 2.0 1.2`
- `shake 0.02 0.4`
- `set m4_mantras_free`
- `hint Your mantras answer you again.`
- `bubble ren: ...It's gone. I can breathe again.`
- `say Pell: The Choir drains this whole prison through the old culverts, and I still fit through them.|Meet me at the skull gate - north, past the Rolling Hall and the blade yard. Go carefully. They built this place for people like us.`
- `fade out 0.6`
- `set pell_left`
- `fade in 0.6`

## 5. Đoàn đèn đỏ Black Lantern trên gờ xa  (`lantern_line`)
- `face 15,70`
- `cam 15,70 1.1`
- `wait 0.6`
- `shake 0.02 3.0`
- `glow 1,70 #ff3b2f 1.3 3.0`
- `wait 0.5`
- `glow 4,70 #ff3b2f 1.3 3.0`
- `wait 0.5`
- `glow 7,70 #ff3b2f 1.3 3.0`
- `narrate Across the pit, on a ledge no one could reach, red lanterns move in a long and patient line.|Between them, on poles and chains, they carry something far too large to be a man.`
- `glow 11,70 #ff3b2f 1.3 3.0`
- `wait 0.5`
- `glow 15,70 #ff3b2f 1.3 3.0`
- `wait 0.5`
- `glow 19,70 #ff3b2f 1.3 3.0`
- `wait 0.5`
- `glow 23,70 #ff3b2f 1.3 3.0`
- `wait 1.2`
- `camren`
- `bubble ren: Black Lantern. Whatever that is, they carry it like a saint.`

## 6. Pell ở cổng đầu lâu  (`pell_g`)
- lines: This gate answers to its chain, not a key. Come close and I'll haul it.

## 7. Pell mở cổng đầu lâu  (`skull_open`)
- `bubble pell_g: There you are. You crossed the blades. Malric would have liked you.`
- `say Pell: They brought it through here not an hour ago - the Hierophant's Specimen. The Black Lantern carried it down to the Hall of Naming.|Whatever he is building, he is building it out of the dead.`
- `say Pell: Stand back.`
- `walk pell_g 56.5,67.6`
- `shake 0.04 0.8`
- `set skull_gate_open`
- `say Pell: The Ossuary is below - the keepers' dead are kept there. Rest. You will need it.`

## 8. Pell ở Ossuary (hub)  (`pell_o`)
- lines: Two seals lock the way down to the Hall. The Registrar keeps one in the Scriptorium, west. | The other sits in the Royal Reliquary, east. The King's own vault - down here, under a cult's floor. Ask yourself why. | Rest when you need to. Old bones like mine are good at waiting.
- lines_1: You found her letters. She wrote to a man who never once wrote back. | I knew your face was familiar. I swept the floor outside her cell.
- lines_1_when: parents_heard
- lines_2: Both seals. Then the way is open - and so is everything that waits behind it. | I'm coming with you. Don't argue with a man four hundred years older than your grandmother.
- lines_2_when: seal1_set,seal2_set

## 9. Sổ Candidate (bục Sổ Tên, Scriptorium)  (`register_vii`)
**The Register of Candidates**

Kept by the Registrar of the Choir. Each Candidate is entered at the Naming and struck at the end.  
  
I. Taken 1069. Made hollow. The Eye did not come. Struck.  
II. Taken 1081. Died in the second year of silence. Struck.  
III. Taken 1094. Spoke a name in her sleep. Corrected. Struck.  
IV. Taken 1110. Made hollow. Reached for the Eye and burned. Struck.  
V. Taken 1127. Ran. Returned. Struck.  
VI. Taken 1140. Hollow. Kept. Given the right eye of the Ascended, that it may see what it waits for. NOT struck.  
VII. Born 1156 in the lower cells, of a Nameless woman. Taken at the first breath, before she could name it. Lost the same winter on the west road, when the cart went into the river at the ford. Presumed drowned. Struck.  
  
Under VII, in newer ink: Not drowned. Raised at Corwen's ford. He carries the Eye. Bring him to the Hall.

## 10. Ren đọc xong sổ Candidate  (`vii_read`)
- `wait 0.3`
- `bubble ren: Seven. ...Corwen's ford. That's me. I was the seventh.`

## 11. Trang sổ tên 1  (`names_p1`)
**A Page of Struck Names**

Struck at the Naming, year 1101:  
Asha of Millbrook. Tomas Reed. Old Wenna, who would not drink. Hal and his brother Hob. A girl from Harlan's farm, no name given.  
  
Every name has a single neat line through it.

## 12. Trang sổ tên 2  (`names_p2`)
**A Page of Struck Names**

Struck at the Naming, year 1119:  
Corran the miller's son. Isbet Dunmore. Pip. Mother Gale. Two travellers from the west road, names not asked.  
  
Every name has a single neat line through it.

## 13. Trang sổ tên 3  (`names_p3`)
**A Page of Struck Names**

Struck at the Naming, year 1133:  
Mirelle. Ansel of the ford. Brannoc the tinker. The Askew twins. A boy who said his name was Fox.  
  
Every name has a single neat line through it.

## 14. Trang sổ tên 4  (`names_p4`)
**A Page of Struck Names**

Struck at the Naming, year 1148:  
Willem Hale. Nan Ostry. Edda. Edda's daughter. Rook. A woman who gave her name only as 'Mine'.  
  
Every name has a single neat line through it.

## 15. Trang sổ tên 5  (`names_p5`)
**A Page of Struck Names**

Struck at the Naming, year 1160:  
Beck. Lia Thorne. Garrow. Old Mott, who sang. Three goblin children, names unknown, struck anyway.  
  
Every name has a single neat line through it.

## 16. Trang sổ tên 6  (`names_p6`)
**A Page of Struck Names**

Struck at the Naming, year 1171:  
Sela. Tam. Jory Ashdown. Kell of Greystone. Wren. The page ends with a column of names scratched out so hard the pen went through.  
  
Every name has a single neat line through it.

## 17. Đọc hết 6 trang sổ tên (name_reclaimed)  (`names_all`)
- `wait 0.4`
- `narrate You read every name in the register aloud, one after another, until the Scriptorium is full of them.|No one has said them in this room for a very long time.`
- `set name_reclaimed`
- `bubble ren: They had names. All of them.`

## 18. Thư mẹ (phòng thư)  (`letters_note`)
**Letters, Never Sent**

A bundle tied with thread. Every letter is addressed to the Hierophant. None was ever opened.  
  
- I am the woman in the lower cells. You have my son. I do not want the honour you say he will have. I want him.  
  
- They let me copy in the Scriptorium now, so my hands are useful. I write his name in the margins where no one reads.  
  
- I named him before they took him. I never said it aloud. I sewed it into the corner of his blanket while I was carrying him, in thread the colour of the blanket, so they would not see. Ren. His name is Ren.  
  
- They say the cart went into the river at the ford. They say he drowned. Rivers carry things. Let someone kind find him. Let them read the corner of the blanket.  
  
- This is the last one. They have stopped bringing paper. If you ever read this: he was never yours.

## 19. Ren đọc xong thư mẹ  (`letters_read`)
- `wait 0.4`
- `bubble ren: Ren. ...She gave it to me. It was always mine.`

## 20. Biên niên Keepers (Ossuary)  (`keepers_chronicle`)
**Chronicle of the First Keepers**

Copied in the third generation of the Children of Light.  
  
In the last year before the Count, King Thrag and his nobles put the Ascended to death on the shore of Nova. It was not their thought. It was the counsel of the King's friend, who had walked beside the Ascended and knew what a crown should fear: a man the people loved more than any king.  
  
His name was DARWIN. The disciple's diary that came to us had that name cut out of it, as if paper could be ashamed. We write it here whole.  
  
When it was done, Darwin named Thrag the murderer, and the people made Darwin king in his place. The kingdom took a new name: Vael.  
  
Luke and Emma, who saw it, carried away what they could - an eye, a hand - and went where no king would follow. The rest was left to us to keep. In the first hundred years the King's men came down with lanterns and took it, piece by piece, into his Reliquary.

## 21. Tranh Angel Bird (sau biên niên)  (`angel_bird`)
- `wait 0.3`
- `fade out 0.6`
- `slide Story/angel_bird_1: On the shore of Nova, a king and his nobles put to death the one the people called the Ascended.|The order was not the king's. It was his friend's.`
- `slide Story/angel_bird_2: They laid their hands on what was left of him, and each one called it his right.`
- `slide Story/angel_bird_3: His blood ran down the steps into the lake. The nobles knelt, and drank, and never died again.`
- `slide Story/angel_bird_4: When he died, a flight of white birds rose from the water and did not come back.|They named that year Angel Bird. Everything since has been counted from it.`
- `slide end`
- `fade in 0.8`
- `bubble ren: Darwin. The King's friend became the King - and he never died.`

## 22. Lời tiên tri gốc (rương kho)  (`prophecy_original`)
**The Prophecy of the Eye**

The original leaf, older than any copy in the Scriptorium.  
  
THE EYE SHALL BE FREED BY ONE WHO HAS NO NAME.  
  
Written in a careful royal hand, and sealed beneath with the crown and three drops of red wax: the seal of the Eternal King.  
  
A note pinned to it, in the Hierophant's hand: Given to the Choir in 476, the same year we were let into the Reliquary. A gift, we thought.

## 23. Ren đọc lời tiên tri  (`prophecy_seen`)
- `wait 0.3`
- `bubble ren: The King's seal. He wrote it. He sent them hunting for someone like me.`

## 24. Sổ kiểm kê Royal Reliquary  (`inventory_note`)
**Inventory of the Royal Reliquary**

Relics of the Ascended, as entered by the King's Keeper.  
  
The Heart - kept upon the King's own person.  
The Left Eye - lost in the year of Angel Bird (the disciple Luke).  
The Left Hand - lost in the year of Angel Bird (the woman Emma).  
The Spine - removed 476, by the Hierophant's people.  
The Ears - removed 476, by the Hierophant's people.  
The Right Eye - removed 476, by the Hierophant's people.  
The Blood - the lake at Nova. The house of Vey drinks.  
  
Beside every empty line, in a different hand, the same three words: Let them carry it.

## 25. Ren đọc sổ kiểm kê  (`inventory_seen`)
- `wait 0.3`
- `bubble ren: 'Let them carry it.' He let them steal it - so someone else would carry it all back to him.`

## 26. Lên đầu Penitent's Walk  (`walk_top`)
- `set walk_crossed`
- `hint Far above, a bolt slides back.`

## Chữ khi bị trói (thuộc tính map)
- disarm_until: m4_katana
- disarm_text: Your blade is gone. The warders took it.
- mantra_seal_until: m4_mantras_free
- mantra_seal_text: Your mantras are bound. The binding drinks every word.
