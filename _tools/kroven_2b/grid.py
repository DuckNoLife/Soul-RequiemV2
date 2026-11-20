# Sơ đồ ô Map 2b The Kroven Deep (W x H). '#' đá, '.' sàn (kể cả ô nước kênh — nước chặn bằng blocker riêng). Toạ độ ô, y hướng xuống.
# Đường đi (một mạch): L -> C1 -> R1 Gatehouse -> C2 -> R2 Rolling Hall -> C3 -> R3 Sluice Walk -> C4 -> R4 Guard's Post -> C5 (cổng) -> R5 Nolan's Rest
# 17/11 (§285): R2 trở lại làn lăn ngang + hốc 3x3; R3 thành dải gai sàn (không nước); R5 nới sang tây để đồ dựa vách.
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
R('R2',22,17,44,21)     # Rolling Hall — khúc gỗ gai lăn NGANG từ đầu đông (cửa C3) về tây; núp vào 2 hốc vách bắc
for ax in (28,34): room(ax,14,ax+2,16)    # 17/11 (§285): hốc 3 ô x 3 hàng (như một nhánh hành lang cụt) -> vách vẽ đúng
R('C3',41,10,43,16)
R('R3',32,5,44,9)       # The Spiked Walk — 3 dải gai sàn (2 ô x 5 hàng) chạy thành sóng về tây, khe 1 ô giữa các dải là chỗ đứng
R('C4',25,5,31,7)
R('R4',10,3,24,11)      # The Guard's Post — boss
R('C5',15,12,18,15)     # lối xuống phòng mộ: 4 ô = lỗ hổng vách bắc R5 (mẫu r2_gate Map 2)
R('R5',10,16,19,21)     # Nolan's Rest — quan tài đứng + giá sách dựa vách bắc bên trái cổng (x20..21 là vách ngăn với R2)
# ô nước kênh Sluice Walk
WET=set()                # 17/11 (§285): bỏ kênh nước (người dùng: xấu)
S=[''.join(r) for r in G]
FLOOR=set((x,y) for y in range(H) for x in range(W) if S[y][x]=='.')
