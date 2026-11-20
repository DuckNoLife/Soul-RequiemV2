# 18/11 (§329): bảng Mantra mở bằng phím L ở mọi nơi -> lời nhặt cuộn chiêu Map 2 không còn bảo "Rest to attune it."
import sys; sys.path.insert(0,'../lore_1116')
from patch_lib import Doc
P='../../Assets/GamePackage/EPIC RPG World - Sewers V1.5/TiledMap Editor/Maps/map2_undercity_of_vael.tmx'
d=Doc(P,'../../_backups/2026-11-18_mantras_L')
assert d.md5_before=='741bdd0e688e64bf0db7a3514d58a1d3', d.md5_before
n=d.src.count('Rest to attune it.'); assert n==2, n
d.gsub('Rest to attune it.','Press {mantras} to attune it.')
assert not d.err, d.err
print('M2 md5', d.save())
