# 🎮 Hold My Hand — Game Design Document (GDD)

## 1. Giới thiệu Dự án
- **Tên dự án**: Hold My Hand
- **Thể loại**: Co-op Multiplayer 3D Puzzle / Platformer / Shooter
- **Đối tượng người chơi**: 2 người chơi cộng tác (Co-op 2 Players)
- **Nền tảng**: PC (Windows, Steam), phát triển bằng Unity 6
- **Cơ chế đặc trưng**: 
  - Hai người chơi đảm nhận 2 vai trò: **Dad Player** (Người cha - nhân vật to lớn, thể lực, chịu lực) và **Child Player** (Người con - nhỏ gọn, nhanh nhẹn, chui qua khe hẹp).
  - Khả năng **Đổi vai trò linh hoạt (Dynamic Swap Role)** trong sảnh chờ (Lobby).
  - Tương tác vật lý cộng tác: Nắm tay (Hold Hands), kéo/đẩy vật thể, kích hoạt bẫy logic song song.

---

## 2. Hệ Thống Gameplay Blocks
Game chia thành các module cốt lõi (Assembly Definitions):
1. **Core Gameplay (`Blocks.Gameplay.Core`)**: Quản lý tương tác nhân vật, mạng client-server, chỉ số động (Dynamic Stats), chuyển tiếp màn chơi.
2. **Platformer Block (`Blocks.Gameplay.Platformer`)**: Cơ chế nhảy bậc thang, đu dây, bập bênh trọng lực phối hợp cha - con.
3. **Shooter Block (`Blocks.Gameplay.Shooter`)**: Cơ chế giải đố bằng công cụ bắn hạt năng lượng / kích hoạt công tắc từ xa.
4. **Sessions Block (`Blocks.Sessions`)**: Quản lý vòng đời phòng chơi, kết nối mạng và đồng bộ trạng thái sảnh chờ.

---

## 3. Hệ Thống Vai Trò & Phân Quyền (RBAC)
Hệ thống quản lý 3 cấp người dùng:
1. **Player**:
   - Tham gia hoặc tạo Lobby (mã gồm 6 ký tự).
   - Chọn vai trò (Dad hoặc Child) và thực hiện Swap vai trò.
   - Bấm Ready để chốt cấu hình và bắt đầu game session.
2. **Moderator**:
   - Giám sát các phiên chơi và nội dung.
   - Điều chỉnh chỉ số nhân vật động (Dynamic Character Stats) theo thời gian thực.
   - Có thẩm quyền đóng hoặc kết thúc các phòng chơi (Lobby) vi phạm hoặc bị treo (`MOD-001`).
3. **Admin**:
   - Quản lý toàn bộ tài khoản, Ban/Unban, kiểm tra Audit Log hệ thống.
