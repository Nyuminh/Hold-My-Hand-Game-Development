# 🏛️ Hold My Hand — Kiến Trúc Hệ Thống (System Architecture)

## 1. Tổng Quan Kiến Trúc
Hệ thống **Hold My Hand** kết hợp kiến trúc Game Client - Backend Server và hệ sinh thái Multi-Agent Orchestration cục bộ:

```text
┌─────────────────────────────────────────────────────────────────────────────┐
│                            LOCAL MULTI-AGENT ORCHESTRATOR                   │
│                                                                             │
│  [Planner] ──> [Task State Machine] ──> [Task Isolation & Concurrency Guard]│
│                        │                                                    │
│                        ├──> [Context Builder (Minimal, Diff-based)]         │
│                        ├──> [Local AI Adapter (Ollama Qwen 3.5:4B GPU)]      │
│                        ├──> [Antigravity Packet Adapter (Manual / Direct)]  │
│                        └──> [Automated Test Runner & DocGen (Word/PDF)]     │
└──────────────────────────────────────┬──────────────────────────────────────┘
                                       │
                    ┌──────────────────┴──────────────────┐
                    ▼                                     ▼
┌───────────────────────────────────────┐ ┌───────────────────────────────────┐
│       UNITY CLIENT (Hold My Hand)     │ │   BACKEND API (HoldMyHand.Backend)│
│                                       │ │                                   │
│  - Runtime: Unity 6 (6000.5.10f1)     │ │  - Runtime: Node.js 18+ (ESM)     │
│  - Codebase: C# (Asmdefs)             │ │  - Framework: Express 4           │
│  - Networking:                        │ │  - DB: MongoDB (Persistence)      │
│    • ApiClient.cs                     │ │  - Cache/Session: Redis / In-Mem  │
│    • ModApiClient.cs                  │ │  - Auth: JWT Bearer + RBAC        │
│    • AdminApiClient.cs                │ │  - Endpoints: Auth, Game, Mod, Adm│
└───────────────────────────────────────┘ └───────────────────────────────────┘
```

---

## 2. Các Thành Phần Chính

### 2.1. Unity Game Client
- **Thư mục gốc**: `Hold My Hand/`
- **Các Asmdef**: `Blocks.Common`, `Blocks.Gameplay.Core`, `Blocks.Gameplay.Platformer`, `Blocks.Gameplay.Shooter`, `Blocks.Sessions`.
- **Cơ chế API Client**: Sử dụng `UnityWebRequest` bất đồng bộ bọc trong `ApiClient.cs`, tự động gắn JWT Bearer token và xử lý serialization qua `JsonUtility`.

### 2.2. Backend RESTful API
- **Thư mục con**: `Hold My Hand/HoldMyHand.Backend/` (quản lý bởi Git repository độc lập).
- **Trạng thái Lobby**: Lưu trữ trong `lobbyStore` (Redis hoặc In-memory fallback), cập nhật trạng thái các slots: `player1`, `player2`, `status` (`WAITING`, `READY`, `IN_GAME`, `CLOSED`).
- **Phân quyền Middleware**: `authenticate` giải mã JWT token, `authorize('Moderator')` bảo vệ các endpoint quản trị và kiểm duyệt.

### 2.3. Local-First Multi-Agent Orchestrator
- **Ngôn ngữ**: Python 3.12 (có type hints đầy đủ).
- **Cơ chế Context Tối thiểu**: Chỉ nạp file liên quan, tính hash để tránh gửi trùng lặp, dùng Git diff khi review.
- **Bảo vệ Concurrency & Khóa File**: Không cho phép 2 task cùng sửa 1 file, tối đa 1 AI task thực thi đồng thời.
- **Local AI Engine**: Tương thích Ollama API (`http://127.0.0.1:11434`), sử dụng GPU NVIDIA RTX 3050 Ti với model `qwen3.5:4b`. API cloud bị khóa cứng với ngân sách 0 VNĐ.
- **Bộ xuất tài liệu DocGen**: Chạy độc lập qua `python-docx` và `ReportLab`, không phụ thuộc AI.
