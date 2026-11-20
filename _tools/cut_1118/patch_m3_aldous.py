# 18/11 (§328): Aldous ở commons (id 5467) đứng nhìn ngang (face W) -> nhìn xuống camera (S) như aldous_field.
import sys; sys.path.insert(0,'../lore_1116')
from patch_lib import Doc
P='../../Assets/GamePackage/EPIC RPG World Pack - Cemetery V 1.6/TiledMap Editor/Maps/map3_vale_of_graves.tmx'
d=Doc(P,'../../_backups/2026-11-18_aldous_v2')
assert d.md5_before=='88a9cdfa9eccc209f5d9904e2599117f', d.md5_before
assert d.get(5467,'face')=='W'
d.set(5467,'face','S')
assert not d.err, d.err
print('M3 md5', d.save())
