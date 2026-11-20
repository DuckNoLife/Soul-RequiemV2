# 18/11 (§328): Crimson Oath nằm trên nắp quan tài bị quan tài (props 3852, chân 6.24) vẽ đè -> trông như mất kiếm.
# sort_at = chân quan tài nhích xuống -> kiếm vẽ trên nắp, Ren đứng phía trước vẫn che kiếm.
import sys; sys.path.insert(0,'../lore_1116')
from patch_lib import Doc
P='../../Assets/GamePackage/EPIC RPG World Pack - Cemetery V 1.6/TiledMap Editor/Maps/map3b_kings_barrow.tmx'
d=Doc(P,'../../_backups/2026-11-18_3b_oath')
assert d.md5_before=='f0202c8aac1aa388449178c7f53f0939', d.md5_before
d.set(5252,'sort_at','9.0,6.3')
assert not d.err, d.err
print('3b md5', d.save())
