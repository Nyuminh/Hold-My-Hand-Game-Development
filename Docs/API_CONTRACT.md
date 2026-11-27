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

---

## 5. Voice relay for Unity Lobby (`VOICE-001`–`VOICE-005`)

- Deployed endpoint: `wss://hold-my-hand-backend.vercel.app/voice`. Vercel routes it to a dedicated WebSocket Function. Local development can use `ws://127.0.0.1:5181/voice` after starting the backend.
- Unity Lobby remains the source of room membership. The client sends the **first WebSocket text frame** after connecting:

  ```json
  {"type":"auth","lobbyId":"<Unity lobby ID>","lobbyCode":"ABC123","playerId":"<Unity Authentication PlayerId>","token":"<Unity Authentication AccessToken>"}
  ```

- The backend verifies the token's RS256 signature, expiry, issuer and `project_id` against Unity JWKS, then calls Unity Lobby `GET /v1/{lobbyId}` using that token. It checks that the verified `sub` equals `playerId`, that this player is in the lobby, and that the lobby's code matches. The backend's `UNITY_PROJECT_ID` must match the Unity project (default `7b831d30-2f9b-4ac8-93d5-15760c6be5b9`). Do not log or place the token in the URL.
- On success the server sends `{"type":"ready","playerId":"...","lobbyCode":"..."}`. The client sends audio only after this frame. Unauthorized connections close with WebSocket code `1008`.
- Each following binary frame is 644 bytes: 4-byte unsigned sequence number in little-endian order followed by 320 signed 16-bit little-endian PCM samples, mono at 16 kHz (20 ms). The server forwards each frame to the other authenticated player in the same Unity Lobby ID, never back to its sender. No audio is persisted.
- The client reconnects after a disconnect and re-authenticates with its current Unity token. Leaving the lobby closes the socket and stops the microphone. The WebSocket stays attached to `VoiceChatManager` across lobby-to-game scene changes.
- The current relay stores room sockets in Function memory. Vercel may place two clients on different instances, so production two-client delivery requires shared room fan-out (for example Redis pub/sub) and must be verified on the deployed endpoint. A successful WebSocket handshake alone does not establish that audio will reach the peer.

## Đăng ký và cấu hình chiến dịch

- `POST /api/auth/register` nhận `username`, `email`, `password` (từ 8 ký tự, tối đa 72 byte) và `displayName` tùy chọn. Trả 201 cùng `userId`, `username`, `role: Player`, `displayName`. Dữ liệu sai trả 400, trùng username hoặc email trả 409. Đăng nhập qua `/api/auth/login`.
- `GET /api/game/campaign` công khai trả `{"chapters":[{"scene":"PlayScene1","quests":["tree","chasm","mud"]}]}` với các màn và quest đang mở. Màn 1, 2, 3 lần lượt dùng PlayScene1, PlayScene2, PlayScene4.
- Mod hoặc Admin gọi `PUT /api/mod/chapters/{id}` hoặc `PUT /api/mod/quests/{id}` với `{"isActive":false}` để tắt và `true` để bật lại. Dùng danh sách mod để lấy ID, kể cả nội dung đang tắt. Cấu hình màn được kiểm tra trước khi host bắt đầu hoặc chuyển màn. Quest được áp dụng khi vào màn.

## Ví Coin và cửa hàng skin

Các endpoint sau yêu cầu Bearer JWT và phiên đăng nhập còn hiệu lực. Account ID được lấy từ phiên. ID skin dùng 1–61 ký tự ASCII thường, chữ số, dấu gạch ngang hoặc gạch dưới và bắt đầu bằng chữ hoặc số. Giá và số dư là số nguyên Coin.

| Method và endpoint | JSON gửi lên | JSON trả về khi thành công (200) |
|---|---|---|
| GET /api/wallet/balance | — | `{"coins":0,"packages":[{"packageId":"vnd_50000","amountVnd":50000,"coins":550}],"sandboxEnabled":true}` (ví dụ rút gọn danh sách gói) |
| POST /api/wallet/topup | `{"packageId":"vnd_50000","requestId":"<uuid>"}` | `{"coins":550,"transactionId":"<id>"}` |
| GET /api/store/catalog | — | `{"skins":[{"skinId":"dad_skin_flannel_01","name":"Flannel Dad","role":"Dad","price":500,"isActive":true}]}` |
| POST /api/store/buy | `{"skinId":"dad_skin_flannel_01"}` | `{"coins":50,"transactionId":"<id>"}` |
| GET /api/store/inventory | — | `{"skins":[],"equippedDadSkinId":null,"equippedChildSkinId":null}`. Mỗi skin có cùng schema như catalog, bao gồm cả skin đã mua và sau đó ngừng bán. |
| POST /api/store/equip | `{"skinId":"dad_skin_flannel_01"}` | `{"equippedDadSkinId":"dad_skin_flannel_01","equippedChildSkinId":null}` |

Sandbox cần `STORE_SANDBOX_ENABLED=true` trên backend development/test. Production trả 403. Backend định giá từng gói và trả danh sách gói qua balance. Mỗi lần nạp mới dùng một requestId mới (8–80 ký tự chữ, số, gạch ngang/gạch dưới). Khi retry phải gửi lại cùng ID và gói. Ví và lệnh mua sử dụng MongoDB replica set hoặc sharded cluster hỗ trợ transaction. Số dư và ledger được commit trong cùng giao dịch.

Catalog chỉ trả skin đang bán, với role `Dad` hoặc `Child`. Mua thiếu Coin trả 400 cùng message `Insufficient balance`, mua trùng trả 409, skin ngừng bán trả 404. Trang bị skin chưa sở hữu trả 400. Response lỗi có dạng `{"statusCode":400,"message":"Insufficient balance"}`.

### Admin Store

Tất cả endpoint dưới đây yêu cầu role `Admin`. Player và Moderator nhận 403. Mọi thay đổi danh mục và điều chỉnh ví đều ghi audit với Admin ID trong cùng MongoDB transaction.

| Method và endpoint | JSON gửi lên | Kết quả |
|---|---|---|
| GET /api/admin/store/skins | — | 200, `{"skins":[...]}` gồm cả skin đã ẩn, cùng schema với catalog. |
| POST /api/admin/store/skins | `{"skinId":"new_outfit","name":"New Outfit","role":"Child","price":500,"isActive":true}` | 201, object skin mới. isActive mặc định true. |
| PUT /api/admin/store/skins/{skinId} | `{"name":"New Name","price":450,"isActive":false}` | 200, object skin sau khi sửa. Chỉ gửi các trường muốn đổi. |
| DELETE /api/admin/store/skins/{skinId} | — | 204, ẩn khỏi danh mục bán. Chủ sở hữu vẫn trang bị được. |
| POST /api/admin/wallets/{accountId}/adjust | `{"amount":-50,"reason":"Wallet correction","requestId":"<uuid>"}` | 200, `{"coins":450,"transactionId":"<id>"}`. amount là số nguyên khác 0, reason dài 1–500 ký tự. Cùng requestId và tham số trả lại kết quả ban đầu. |
| GET /api/admin/store/transactions | — | 200, `{"transactions":[{"transactionId":"<id>","accountId":"<id>","type":"ADJUSTMENT","amount":-50,"coins":450,"reason":"Wallet correction","adminId":"<id>","status":"COMPLETED","createdAt":"2026-01-01T00:00:00.000Z"}]}` |

Lịch sử trả tối đa 500 giao dịch mới nhất. Loại TOPUP có packageId và amountVnd, PURCHASE có skinId, ADJUSTMENT có reason và adminId. Giá skin và số dư sau giao dịch nằm trong khoảng 0–2.147.483.647.
