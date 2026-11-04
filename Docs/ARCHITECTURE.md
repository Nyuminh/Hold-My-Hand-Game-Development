# 🏛️ Hold My Hand — Kiến Trúc Hệ Thống (System Architecture)

> **Mã Đề Tài**: FA26SE047  
> **Phiên Bản Kiến Trúc**: 2.0 (Microservices & Distributed Hybrid Architecture)  
> **Sơ Đồ Đồ Họa Độ Nét Cao**: [`Docs/Report 1-5/diagrams/system_architecture_detailed.png`](file:///C:/Users/Admin/Hold%20My%20Hand/Docs/Report%201-5/diagrams/system_architecture_detailed.png)

---

## 1. Tổng Quan Kiến Trúc (Architecture Overview)

Kiến trúc hệ thống **Hold My Hand** được thiết kế theo mô hình **Phân tán lai (Distributed Hybrid Microservices & Real-Time Relay)** nhằm tách biệt tuyệt đối giữa các tác vụ mạng game thời gian thực (Real-time UDP/WSS), dịch vụ sảnh chờ & quản trị (REST API / JWT RBAC), và hệ thống đồng bộ dữ liệu đám mây (Cloud Database & Cache):

```text
┌─────────────────┐       ┌─────────────────┐       ┌───────────────────────────────┐
│  CLIENT SIDE    │       │ GATEWAY / PROXY │       │   SERVER SIDE (MICROSERVICES) │
│                 │       │                 │       │                               │
│  [Unity Dad]────┼─HTTP─►│  [NGINX / Edge] ┼─HTTP─►│  [API Gateway (JWT / RBAC)]   │
│                 │  WSS  │                 │  WSS  │               │               │
│  [Unity Child]──┼──────►│                 │──────►│  [Identity]   │   [Lobby]     │
│                 │       │                 │       │   (Users DB)  │   (Redis)     │
│  [Web Portal]───┼──────►│                 │       │       │       │      │        │
│                 │       │                 │       │  [Voice Relay Service (WSS)]  │
│                 │       │                 │       │  [Moderation] │   [Session]   │
└─────────────────┘       └─────────────────┘       └───────┬───────────────┬───────┘
                                                            │               │
                                              ┌─────────────▼───────────────▼───────┐
                                              │           3RD PARTY SERVICES        │
                                              │  [UGS]   [MongoDB]   [Upstash]      │
                                              │  [Discord Webhook]   [Steamworks]   │
                                              └─────────────────────────────────────┘
```

---

## 2. Chi Tiết Các Tầng Thành Phần (Detailed Architecture Breakdown)

### 2.1. Tầng Khách (Client Side)
Nằm trong khung viền phân rã độc lập phía bên trái:
1. **Unity Game Client — Dad Player (Win64 PC)**:
   - Framework: Unity 6 (6000.5.10f1), Universal Render Pipeline (URP).
   - Module: `Blocks.Common`, `Blocks.Gameplay.Core`, `Blocks.Gameplay.Platformer`.
   - Kết nối: Gửi HTTP REST requests (qua `ApiClient.cs`), truyền/nhận frame âm thanh WSS nhị phân 644 bytes (qua `VoiceChatManager.cs`), lọc âm qua `VoiceDSPProcessor.cs` (hạ $-4$ bán âm).
2. **Unity Game Client — Child Player (Win64 PC)**:
   - Module: `Blocks.Gameplay.Shooter`, `Blocks.Sessions`.
   - Lọc âm qua `VoiceDSPProcessor.cs` (nâng $+5$ bán âm giọng trẻ em).
3. **Web Portal & Dashboard (Player & Admin)**:
   - Framework: ReactJS 18, Vite, TailwindCSS, Axios HTTP.
   - Vai trò: Tra cứu bảng xếp hạng, quản lý hồ sơ, giao diện Moderation đóng phòng (`MOD-001`).

---

### 2.2. Tầng Reverse Proxy & API Gateway
Nằm giữa Client và Server Side:
1. **NGINX / Vercel Edge Ingress**:
   - Chấm dứt SSL/TLS, định tuyến cân bằng tải.
   - Tách luồng lưu lượng: định tuyến URL thông thường `/api/*` tới Express API, định tuyến WebSocket `/voice` tới Voice Relay Function.
2. **API Gateway (Auth, Routing & RBAC)**:
   - Middleware `authenticate`: Giải mã và xác thực chữ ký JWT Bearer token (RS256/HS256).
   - Middleware `authorize`: Kiểm tra quyền hạn theo vai trò (`Player`, `Moderator`, `Admin`).
   - CORS, Request Throttling và Rate Limiting chống spam request.

---

### 2.3. Tầng Dịch Vụ Máy Chủ (Server Side Microservices)
Toàn bộ các dịch vụ nội bộ được đóng gói trong ranh giới Server Side:
1. **Identity & Auth Service** $\leftrightarrow$ **Users DB (MongoDB Atlas)**:
   - Quản lý đăng ký, đăng nhập, mã hóa mật khẩu bcrypt (12 rounds), cấp phát JWT token có thời hạn 24 giờ.
2. **Lobby & Matchmaking Service** $\leftrightarrow$ **Lobby Cache (Redis / In-Memory Store)**:
   - Máy trạng thái sảnh chờ (`WAITING`, `READY`, `IN_GAME`, `CLOSED`).
   - Tạo mã phòng ngẫu nhiên 6 ký tự, gán vị trí Dad/Child, xử lý đổi vai trò (`/swap`) và trạng thái sẵn sàng (`/ready`).
3. **Voice Relay Service (WSS)** $\leftrightarrow$ **Room Sockets Registry (In-Memory)**:
   - Nhận bắt tay WSS, xác thực Unity Token qua UGS JWKS.
   - Chuyển tiếp frame âm thanh nhị phân (644 bytes: 4 bytes sequence + 320 mẫu PCM int16 16kHz mono) giữa 2 người chơi trong cùng một phòng.
4. **Moderation Service (`MOD-001`)** $\leftrightarrow$ **Audit Logs DB (MongoDB Atlas)**:
   - Điểm cuối `POST /api/mod/lobby/:code/close` cho phép Moderator/Admin đóng phòng vi phạm hoặc bị treo, lưu vết audit trail.
5. **Game Session & ELO Service** $\leftrightarrow$ **Game Sessions DB (MongoDB Atlas)**:
   - Ghi nhận thời gian hoàn thành màn chơi, tính toán điểm số ELO xếp hạng theo công thức Logistic Rating.
6. **Companion Bot AI Service** $\leftrightarrow$ **Bot Config DB (`GameDB.xlsx`)**:
   - Cung cấp tham số phản xạ, độ trễ và logic điều khiển nhân vật ảo khi người dùng chơi đơn hoặc khi bạn chơi rớt mạng.

#### Thành phần hỗ trợ trong Server Side:
- **Event Bus / Message Broker (Redis Pub/Sub)**:
  - Cầu nối đồng bộ trạng thái giữa các instance Vercel Serverless Function không chia sẻ RAM.
  - Nhận sự kiện *Publish Event* và phát tán *Subscribe Event* cho sảnh chờ và voice relay.
- **Telemetry & Blob Storage**:
  - Lưu trữ log kiểm thử, bản ghi lỗi mạng và telemetry tệp tin game.

---

### 2.4. Tầng Dịch Vụ Bên Thứ Ba (3rd Party Services)
1. **Unity Gaming Services (UGS)**:
   - Unity Lobby Service: Duy trì danh sách phòng chơi phía client.
   - Unity Relay Service: Dự phòng kết nối UDP peer-to-peer.
   - Unity JWKS Endpoint: Cung cấp public key để Backend xác thực tính toàn vẹn của Access Token.
2. **MongoDB Atlas (Cloud Multi-Region)**:
   - Lưu trữ dữ liệu bền vững: bộ sưu tập `users`, `gameSessions`, `auditLogs`.
3. **Upstash Redis (Serverless In-Memory Cache)**:
   - Lưu trạng thái Lobby tạm thời với TTL 30 phút, hỗ trợ cơ chế Pub/Sub cross-instance.
4. **Discord Webhook API**:
   - Đẩy thông báo mời bạn bè vào sảnh qua mã 6 ký tự, phát thông tin giải đấu và vinh danh kỷ lục speedrun.
5. **Steamworks SDK (PC Standalone)**:
   - Đồng bộ hóa thành tựu Steam và danh sách bạn bè trực tuyến.

---

## 3. Quy Ước Mũi Tên và Luồng Dữ Liệu (Data Flow Conventions)
- **Mũi tên liền nét đen (`──►`)**: HTTP Request truyền dữ liệu JSON.
- **Mũi tên đứt nét (`◄┈┈`)**: HTTP Response phản hồi trạng thái từ dịch vụ.
- **Mũi tên đỏ đậm (`══►`)**: Luồng nhị phân WSS âm thanh thời gian thực (16 kHz mono PCM, 20ms frame).
- **Mũi tên chấm xanh (`:..►`)**: Giao tiếp nội bộ gRPC / Internal service call giữa Auth và Moderation/Session.
- **Mũi tên cam (`──►`)**: Sự kiện Publish / Subscribe thông qua Redis Message Broker.
