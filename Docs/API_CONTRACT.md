# 📜 Hold My Hand — API Contract

## 1. Quy Chuẩn Chung
- **Base URL**: `http://localhost:5181/api`
- **Authentication**: `Authorization: Bearer <JWT_TOKEN>`
- **Content-Type**: `application/json`
- **Mã lỗi chuẩn**:
  - `400 Bad Request`: Thiếu tham số hoặc dữ liệu không hợp lệ.
  - `401 Unauthorized`: Chưa đăng nhập hoặc token hết hạn.
  - `403 Forbidden`: Không đủ quyền (ví dụ Player gọi endpoint Moderator).
  - `404 Not Found`: Không tìm thấy tài nguyên (Lobby không tồn tại).
  - `409 Conflict`: Thao tác lặp lại hoặc trạng thái tài nguyên xung đột.

---

## 2. Authentication Endpoints (`/api/auth`)
| Method | Endpoint | Quyền | Mô tả |
|---|---|---|---|
| `POST` | `/api/auth/login` | Public | Đăng nhập tài khoản, trả về JWT Token và Role. |
| `POST` | `/api/auth/register` | Public | Đăng ký tài khoản người chơi mới. |

---

## 3. Lobby Endpoints (`/api/game/lobby`)
| Method | Endpoint | Quyền | Mô tả |
|---|---|---|---|
| `GET` | `/api/game/lobby/current` | Player | Lấy thông tin lobby hiện tại của người chơi. |
| `POST` | `/api/game/lobby/create` | Player | Tạo lobby mới (sinh mã 6 ký tự). |
| `POST` | `/api/game/lobby/join` | Player | Tham gia lobby với `lobbyCode`. |
| `GET` | `/api/game/lobby/:code` | Player, Mod | Lấy chi tiết trạng thái lobby theo mã. |
| `POST` | `/api/game/lobby/:code/swap` | Player | Đổi vai trò Dad ↔ Child trong lobby. |
| `POST` | `/api/game/lobby/:code/ready` | Player | Đánh dấu người chơi sẵn sàng. |
| `DELETE` | `/api/game/lobby/:code` | Player | Rời khỏi lobby. |

---

## 4. Moderator Endpoints (`/api/mod`) — Bao gồm MOD-001
Cần quyền: `Role == "Moderator"` hoặc `"Admin"`.

### 4.1. Đóng Lobby Đang Hoạt Động (`MOD-001`)
- **Endpoint**: `POST /api/mod/lobby/:code/close`
- **Mục đích**: Moderator cưỡng chế đóng hoặc hủy một lobby đang hoạt động hoặc bị treo.
- **Request Headers**:
  ```http
  Authorization: Bearer <MODERATOR_JWT_TOKEN>
  Content-Type: application/json
  ```
- **Request Body (JSON)**:
  ```json
  {
    "reason": "Lobby idle timeout / Inappropriate behavior / Server maintenance",
    "notifyPlayers": true
  }
  ```
- **Response Success (`200 OK`)**:
  ```json
  {
    "success": true,
    "lobbyCode": "AB12CD",
    "previousStatus": "WAITING",
    "currentStatus": "CLOSED",
    "closedBy": "670f...",
    "closedAt": "2026-10-17T01:30:00.000Z",
    "reason": "Lobby idle timeout / Inappropriate behavior / Server maintenance",
    "affectedPlayers": [
      { "playerId": "670e...", "username": "player1", "role": "Dad" },
      { "playerId": "670d...", "username": "player2", "role": "Child" }
    ]
  }
  ```
- **Xử lý ca biên (Edge Cases)**:
  - `404 Not Found`: Lobby không tồn tại hoặc đã bị xóa.
  - `409 Conflict`: Lobby đã ở trạng thái `CLOSED` từ trước (thao tác lặp).
  - `403 Forbidden`: Người gửi request không có quyền Moderator hoặc Admin.
  - Lobby rỗng (0 người chơi): Đóng bình thường, ghi nhận audit log.
  - Người chơi bị mất kết nối: Hệ thống giải phóng slot và đánh dấu hủy session.

### 4.2. Unity Client Contract (`ModApiClient.cs`)
```csharp
[Serializable]
public class CloseLobbyRequest
{
    public string reason;
    public bool notifyPlayers;
}

[Serializable]
public class CloseLobbyResponse
{
    public bool success;
    public string lobbyCode;
    public string previousStatus;
    public string currentStatus;
    public string closedAt;
    public string reason;
}

public static async Task<CloseLobbyResponse> CloseLobbyAsync(string lobbyCode, CloseLobbyRequest req);
```
