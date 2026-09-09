# 📘 HƯỚNG DẪN CÀI ĐẶT & KHỞI CHẠY DỰ ÁN "HOLD MY HAND" (Dành cho Thành Viên Team)

Tài liệu này hướng dẫn cách clone và thiết lập môi trường làm việc trên máy cá nhân để có cấu trúc thư mục đồng bộ 100% với hệ thống chuẩn của dự án.

---

## 🧭 Cấu Trúc Dự Án Thực Tế

Trên máy tính làm việc, cả hai repository được lồng vào nhau thành một thư mục duy nhất để tiện phát triển:

```text
Hold My Hand/                           <-- Root Folder (Repo 1: Game Client Unity)
├── Assets/
├── Packages/
├── ProjectSettings/
│
└── HoldMyHand.Backend/                 <-- Subfolder (Repo 2: Backend .NET)
    ├── HoldMyHand.API/
    ├── HoldMyHand.Application/
    ├── HoldMyHand.Core/
    └── HoldMyHand.Infrastructure/
```

> **Lưu ý quan trọng**: File `.gitignore` ở root đã cấu hình bỏ qua thư mục `HoldMyHand.Backend/`, vì vậy khi làm việc với Git, hai repository này hoạt động hoàn toàn độc lập, không bị conflict (xung đột) với nhau.

---

## 🛠️ 1. Yêu Cầu Môi Trường (Prerequisites)

1. **Git**: [Tải tại đây](https://git-scm.com/) (đã bật Git LFS).
2. **Unity Editor**: Phiên bản **`Unity 6000.5.10f1`** (hoặc Unity 6 tương thích cài qua Unity Hub).
3. **.NET 8 SDK**: [Tải .NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).
4. **MongoDB Community Server**: [Tải MongoDB Community](https://www.mongodb.com/try/download/community) (Port mặc định: `27017`).
5. *(Tùy chọn)* **MongoDB Compass**: [Tải Compass](https://www.mongodb.com/products/tools/compass) để xem giao diện Database trực quan.
6. *(Tùy chọn)* **Redis**: Mặc định Backend đã tích hợp sẵn cơ chế **in-memory fallback**, nên nếu máy cá nhân chưa cài Redis thì hệ thống vẫn tự động chạy bình thường.

---

## 📥 2. Hướng Dẫn Clone Code Về Máy

Mở **Terminal (PowerShell / Command Prompt / Git Bash)** tại thư mục bạn muốn lưu dự án (ví dụ: `D:\Projects` hoặc `C:\Users\<Name>`):

### Bước 2.1: Clone Game Client (Unity)
```bash
git clone https://github.com/Nyuminh/Hold-My-Hand-Game-Development.git "Hold My Hand"
```

### Bước 2.2: Di chuyển vào thư mục vừa clone và Clone tiếp Backend vào trong
```bash
cd "Hold My Hand"
git clone https://github.com/Nyuminh/Hold-My-Hand-Backend.git "HoldMyHand.Backend"
```

👉 **Xong!** Lúc này trên máy bạn đã có cấu trúc chuẩn:
- Thư mục ngoài: `Hold My Hand` (quản lý bởi repo Unity).
- Thư mục trong: `HoldMyHand.Backend` (quản lý bởi repo Backend).

---

## 🚀 3. Khởi Chạy Backend API

### Bước 3.1: Kiểm tra MongoDB
Đảm bảo dịch vụ MongoDB đang chạy trên cổng `27017`.

### Bước 3.2: Chạy Backend
Mở Terminal tại thư mục `Hold My Hand/HoldMyHand.Backend`:
```bash
cd HoldMyHand.API
dotnet run
```
Khi thấy dòng:
```text
Now listening on: http://localhost:5181
Application started. Press Ctrl+C to shut down.
```
tức là Backend đã khởi động thành công và tự động seed sẵn dữ liệu vào MongoDB.

### Bước 3.3: Mở Swagger UI kiểm tra API
Mở trình duyệt truy cập:
👉 **`http://localhost:5181/swagger`**

### 🔑 Tài khoản mẫu có sẵn trong hệ thống:
- **Admin**: `admin` / `Admin@123` (Role: Admin)
- **Moderator**: `moderator` / `Mod@123` (Role: Moderator)
- **Player 1**: `player1` / `Player@123` (Role: Player - Dad)
- **Player 2**: `player2` / `Player@123` (Role: Player - Child)

---

## 🎮 4. Mở Dự Án Game Client Bằng Unity

1. Mở **Unity Hub**.
2. Nhấn nút **Add** -> **Add project from disk**.
3. Chọn thư mục root: **`Hold My Hand`**.
4. Chọn Editor Version **`6000.5.10f1`** (Unity 6).
5. Nhấn vào project để mở. Unity sẽ tự động import packages và build cache `Library/` trong lần đầu mở.

---

## 🔄 5. Quy Trình Làm Việc Hằng Ngày Với Git

- **Khi làm việc với Unity**:
  - Mở terminal tại thư mục root `Hold My Hand`.
  - Dùng các lệnh git bình thường: `git pull`, `git add Assets/`, `git commit`, `git push`.
- **Khi làm việc với Backend**:
  - Mở terminal tại thư mục `Hold My Hand/HoldMyHand.Backend`.
  - Dùng các lệnh git bình thường: `git pull`, `git commit`, `git push`.
