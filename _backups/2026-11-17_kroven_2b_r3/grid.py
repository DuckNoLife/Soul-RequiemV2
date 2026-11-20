# Sơ đồ ô Map 2b The Kroven Deep (W x H). '#' đá, '.' sàn. Toạ độ ô, y hướng xuống.
# Đường đi (một mạch, không lối tắt): L -> C1 -> R1 Gatehouse -> C2 -> R2 Rolling Hall -> C3 -> R3 Sluice Walk -> C4 -> R4 Guard's Post -> C5 (cổng) -> R5 Nolan's Rest
W,H=54,41
G=[['#']*W for _ in range(H)]
def room(x0,y0,x1,y1):
    for y in range(y0,y1+1):
        for x in range(x0,x1+1): G[y][x]='.'
ROOMS={}
def R(name,x0,y0,x1,y1): ROOMS[name]=(x0,y0,x1,y1); room(x0,y0,x1,y1)
R('L',3,32,9,37)        # Landing — thang xuống từ nắp hầm Forgotten Store
R('C1',10,34,14,36)
R('R1',15,31,28,38)     # Gatehouse — tấm sàn kích tên + máy bắn tên nhịp
R('C2',25,22,27,30)
R('R2',22,18,44,21)     # Rolling Hall — trụ gai lăn theo làn, hốc tường để núp
for ax in (27,33): room(ax,16,ax+2,17)     # hốc tường 3 ô (thứ ba là miệng C3)
R('C3',41,10,43,17)
R('R3',32,3,50,9)       # Sluice Walk — gai sàn theo sóng + lưỡi cưa trên ray
R('C4',25,6,31,8)
R('R4',10,3,24,11)      # The Guard's Post — boss
R('C5',15,12,18,15)     # lối xuống phòng mộ: 4 ô = lỗ hổng vách bắc R5, vòm cổng 4x3 ô lấp đúng 3 hàng mặt vách (mẫu r2_gate Map 2)
R('R5',12,16,19,21)     # Nolan's Rest
S=[''.join(r) for r in G]
FLOOR=set((x,y) for y in range(H) for x in range(W) if S[y][x]=='.')
