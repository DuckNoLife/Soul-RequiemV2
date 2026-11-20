# Sơ đồ ô Map 2b The Kroven Deep (W x H). '#' đá, '.' sàn (kể cả ô nước kênh — nước chặn bằng blocker riêng). Toạ độ ô, y hướng xuống.
# Đường đi (một mạch): L -> C1 -> R1 Gatehouse -> C2 -> R2 Rolling Hall -> C3 -> R3 Sluice Walk -> C4 -> R4 Guard's Post -> C5 (cổng) -> R5 Nolan's Rest
# 17/11 (§284): R2 bỏ hốc tường, cao 6 hàng (làn lăn dọc); R3 = lối đi 2 hàng sát vách bắc + kênh nước 3 hàng; R5 nới sang tây để đồ dựa vách.
W,H=54,41
G=[['#']*W for _ in range(H)]
def room(x0,y0,x1,y1):
    for y in range(y0,y1+1):
        for x in range(x0,x1+1): G[y][x]='.'
ROOMS={}
def R(name,x0,y0,x1,y1): ROOMS[name]=(x0,y0,x1,y1); room(x0,y0,x1,y1)
R('L',3,32,9,37)        # Landing — cầu thang đá lên Forgotten Store (vòm trong vách bắc)
R('C1',10,34,14,36)
R('R1',15,31,28,38)     # Gatehouse — rèm tên từ vách bắc + tấm sàn khắc vương miện gọi loạt tên ngay trên đầu
R('C2',25,22,27,30)
R('R2',22,16,44,21)     # Rolling Hall — 3 làn khúc gỗ gai lăn dọc từ nắp hầm sát vách bắc; khe giữa các làn là chỗ đứng an toàn
R('C3',41,10,43,15)
R('R3',32,5,50,9)       # Sluice Walk — lối đi hàng 5..6, kênh nước hàng 7..9 (trừ bến đá x40..44 nơi C3 lên)
R('C4',25,5,31,7)
R('R4',10,3,24,11)      # The Guard's Post — boss
R('C5',15,12,18,15)     # lối xuống phòng mộ: 4 ô = lỗ hổng vách bắc R5 (mẫu r2_gate Map 2)
R('R5',10,16,19,21)     # Nolan's Rest — quan tài đứng + giá sách dựa vách bắc bên trái cổng (x20..21 là vách ngăn với R2)
# ô nước kênh Sluice Walk
WET=set((x,y) for y in range(7,10) for x in list(range(32,40))+list(range(45,51)))
S=[''.join(r) for r in G]
FLOOR=set((x,y) for y in range(H) for x in range(W) if S[y][x]=='.')
