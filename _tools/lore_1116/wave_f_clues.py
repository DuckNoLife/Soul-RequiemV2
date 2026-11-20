# Đợt F (16/11): gieo manh mối lore mới vào ghi chép có sẵn — Darwin gom xác ("the Eye, whole", "the King's collection"), Morrow nghe bằng đôi tai thánh.
# Không spoil trước Map 4. Kiểm md5 trước khi ghi, tự sao lưu.
import sys; sys.path.insert(0,'.')
from patch_lib import Doc
G='../../Assets/GamePackage/'; BK='../../_backups/2026-11-16_waveF_clues'
d=Doc(G+'EPIC RPG World - Sewers V1.5/TiledMap Editor/Maps/map2_undercity_of_vael.tmx',BK)
assert d.md5_before=='54166f00d56130ee4e8e3e6ac3fa0a1f', d.md5_before
d.sub(1069,'text','   - Overseer Grisk',
      "   - Overseer Grisk\\n\\nP.S. The courier from the capital asked after the Eye again. Not the Ether - the Eye, and whether it is still whole. I told him it sleeps. He said the King would like it to go on sleeping, unharmed, until it is sent for.")
print('M2',d.err,d.save())
d=Doc(G+'EPIC RPG World Pack - Cemetery V 1.6/TiledMap Editor/Maps/map3_vale_of_graves.tmx',BK)
assert d.md5_before=='b3a87fd34c7aac10b7c902837a64246f', d.md5_before
d.sub(5170,'text','Any man who drinks his blood or wastes it answers to Morrow.',
      "Keep the bells ringing. The Precentor hears what the bells hear - he will know where the man is before you see him.\\n\\nAny man who drinks his blood or wastes it answers to Morrow.")
d.sub(5188,'text','We do not dig there. We do not even knock.',
      "We do not dig there. We do not even knock.\\n\\nThe King's riders came again tonight. They want the thing the drain-man carries - for the King's collection, they said, and whole. Grandmother says if she brings it to him, he will lift her up into the light at last. She has been promised that before.")
d.sub(5222,'text','Break the lamps first, or nothing here stays dead.',
      "Break the lamps first, or nothing here stays dead.\\n\\nCaptain read us the King's own order: the drain-man's Eye goes to the capital whole. Not a scratch on it. Nobody says what for.")
print('M3',d.err,d.save())
