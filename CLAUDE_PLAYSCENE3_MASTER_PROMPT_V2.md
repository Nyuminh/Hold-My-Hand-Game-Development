# 🎮 CLAUDE MASTER PROMPT v2 — HOLD MY HAND: PlayScene3
> **Tối ưu chi phí.** Mỗi lần chỉ làm 1 task. Hỏi trước khi làm. Báo ngắn sau khi xong.

---

## ⚡ QUY TẮC VÀNG (BẮT BUỘC — KHÔNG ĐƯỢC BỎ QUA)

| # | Quy tắc |
|---|---------|
| 1 | **Max 2 tool call / lượt** — xong 1 bước → báo cáo 2 dòng → dừng chờ |
| 2 | **KHÔNG tự chụp ảnh** — chỉ chụp khi user nói "cho xem ảnh" / "chụp lại" |
| 3 | **Log gọn ≤ 2 dòng** — `result.Log("Done: X")` |
| 4 | **Action-first** — không chào hỏi, không lý thuyết dài |
| 5 | **Nhắc `/compact`** khi hội thoại > 20 tin hoặc xong 1 feature lớn |
| 6 | **TUYỆT ĐỐI KHÔNG** sửa `.unity` bằng text editor — chỉ dùng `Unity_RunCommand` |
| 7 | **KHÔNG** dùng `GameObject.CreatePrimitive()` thay asset 3D gốc |
| 8 | **Hỏi trước** nếu task không rõ scope — không làm rộng hơn được yêu cầu |

---

## 🛠 UNITY MCP — TEMPLATE CHUẨN

```csharp
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var target = GameObject.Find("Target_Name");
        if (target == null) { result.LogError("Not found: Target_Name"); return; }

        Undo.RecordObject(target, "Modify Target");
        // --- thao tác ---
        EditorSceneManager.MarkSceneDirty(target.scene);
        EditorSceneManager.SaveOpenScenes();
        result.Log("Done: {0}", target.name);
    }
}
```

### ⚠️ Lưu ý API Unity phiên bản này
- ❌ `obj.GetInstanceID()` → ✅ `obj.GetEntityId()`
- ❌ `FindObjectsOfType<T>()` → ✅ `FindObjectsByType<T>(FindObjectsSortMode.None)`
- ❌ `NetworkObject.SetParent()` runtime → TUYỆT ĐỐI KHÔNG dùng
- Namespace mọi script mới: `Blocks.Gameplay.Core`
- Unity MCP có thể timeout 3 phút khi load scene lớn — không phải lỗi code, retry lại

---

## 🗺 BẢN ĐỒ PLAYSCENE3 — 8 ZONE

```
[START]
  Zone1 (Lều Trại)
    → Cầu Rút Drawbridge (Ba + Con cùng kéo cần)
  Zone2 (Thang Cẩu Bỏ Hoang)
    → Cáp Treo Stage3Zipline (Con lên trước, Ba kéo đối trọng)
  Zone3 (Bánh Răng Tời — Cove_Winch_Assembly)
    → Con tháo chốt → Ba quay tời → Cầu Cove hạ xuống
  Zone4 (Vịnh Hẹp Cove + Xác Tàu Đắm)
    → Ba giữ thang → Con xuống tàu → Xoay bánh lái → Rương mở → Lấy Chìa Khóa
    → Leo cầu thang gỗ cảng (Cove_Staircase_New) lên Tháp Canh
  Zone5 (Vách Đá Xoắn Ốc — Spiral Stairs)
    → Leo xoắn ốc theo vách núi
  Zone6 (Tháp Canh Left_DefenseTower)
    → Zipline (CastleZiplineRideController) trượt sang Lâu Đài
  Zone7 (Lâu Đài + Sân Trong)
    → Con cắm chìa khóa mở cổng
    → Ba đứng Bệ Mặt Trời + Con đứng Bệ Ánh Trăng → Cổng Sập mở
  Zone8 (Khinh Khí Cầu Zephyr) [END]
    → Hai cha con bước lên → Cutscene kết thúc
```

### Checkpoint Positions (đã có trong PlayScene3RespawnManager.cs)
| CP | Tên | Position |
|----|-----|----------|
| 0 | Khởi đầu (Lều trại) | (-1, 1.25, 5) |
| 1 | Sau cầu rút Zone1 | (-1, 1.25, -15) |
| 2 | Bến cáp treo Islet | (-1, 10.85, -23) |
| 3 | Cao nguyên bánh răng | (-1, 14.5, -48) |
| 4 | Bến cầu vịnh Cove | (26, 3.8, -75) |
| 5 | Sàn tàu đắm | (44.5, 4.5, -54) |
| 6 | Chân cầu thang vách đá | (-11.5, 14, -73.5) |
| 7 | Đỉnh Tháp Canh | (-10.5, 31.8, -87) |
| 8 | Sân cổng lâu đài | (6, 24.5, -101) |
| 9 | Sân trong lâu đài | (0, 24.5, -118.5) |
| 10 | Bệ khinh khí cầu | (0, 25, -135) |

---

## 📖 CỐT CHUYỆN "HOLD MY HAND" — PLAYSCENE3

### Tinh thần truyện (Phong cách: IT TAKES TWO)
> Hai cha con cùng vượt vùng hoang dã cổ đại — không phải vì họ mạnh nhất, mà vì họ **không bỏ nhau lại**.
> Ba dùng **sức**, Con dùng **sự linh hoạt**. Mỗi cơ chế phản chiếu mối quan hệ cha-con: cùng nhau mới làm được.

### Dialog theo Zone (Trigger khi người chơi bước vào vùng)

| Zone | Nhân vật | Câu thoại | Trigger Object |
|------|----------|-----------|----------------|
| Trước cầu rút | Ba | "Con cầm cần bên phải nha, ba cầm bên này." | Near Cove_Winch_Assembly Z1 |
| Qua cầu xong | Con | "Ba ơi, còn xa không?" | Past drawbridge |
| Trước cáp treo | Ba | "Lên trước đi, ba theo sau." | Near Stage3ZiplineCoopController |
| Trước tàu đắm | Con | "Trong đây có gì vậy ba?" | Near Shipwreck_Coop_Controller |
| Nhặt chìa khóa | Ba | "Con cầm cái này, đừng để mất." | Near Captain_Gold_Key |
| Trước cổng lâu đài | Con | "Ba, chìa khóa vừa khớp rồi!" | Near CastleGateKeyUnlocker |
| Bệ Rune sáng | Ba | "Đứng đây không nhúc nhích nha con." | CastleCourtyardCoopController |
| Trước zipline | Con | "Ba... cao lắm!" | Near CastleZiplineRideController launch |
| Lên khinh khí cầu | Ba | "Mình về nhà thôi con." | Balloon boarding trigger |

---

## 🎯 DANH SÁCH TASK (Làm 1 task mỗi lần — đọc scope kỹ trước khi bắt đầu)

---

### STATUS HIỆN TẠI
| Script | Trạng thái |
|--------|-----------|
| `PlayScene3RespawnManager.cs` | CÓ — killPlaneY=-3.5f, 11 checkpoints, flash đỏ chưa implement |
| `PlayScene3ObjectiveDirector.cs` | CÓ — 8 stages, có UI OnGUI, chưa có ShowDialog() |
| `CastleCourtyardCoopController.cs` | CÓ — Bệ Rune Ba + Con |
| `CastleGateKeyUnlocker.cs` | CÓ — Mở cổng bằng CastleKeyPickup |
| `CastleKeyPickup.cs` | CÓ |
| `CastleZiplineRideController.cs` | CÓ — rideDuration=2.8f, Bezier lerp |
| `CastleGateClimaxManager.cs` | CÓ |
| `CoopDrawbridgeController.cs` | Fix lỗi CS0116 xong |
| `DialogTriggerZone.cs` | CHƯA CÓ — cần tạo (TASK 2) |
| Collider thang/slope | Chưa kiểm tra đủ |
| Flash đỏ respawn visual | Chưa implement |
| Legacy objects dọn dẹp | Chưa xóa |

---

### TASK 1 — Thêm ShowDialog() vào ObjectiveDirector
**Script:** `PlayScene3ObjectiveDirector.cs`
**Scope chỉ làm:**
- Thêm method `public void ShowDialog(string speaker, string line, float duration)` vào class
- Dùng `OnGUI` đã có để render popup dialog ở giữa-dưới màn hình
- Format: [Ba] hoặc [Con] + text câu thoại, font lớn hơn hint card hiện tại
- Tự ẩn sau `duration` giây. Có thể gọi lại bằng [Tab]
- KHÔNG tạo Canvas mới, KHÔNG import package

---

### TASK 2 — Tạo DialogTriggerZone.cs
**Script mới:** `Assets/Core/Scripts/Runtime/Components/DialogTriggerZone.cs`
**Scope chỉ làm:**
```csharp
namespace Blocks.Gameplay.Core
{
    public class DialogTriggerZone : MonoBehaviour
    {
        public string speaker; // "Ba" / "Con"
        [TextArea] public string line;
        public float duration = 3.5f;
        private bool triggered = false;

        private void OnTriggerEnter(Collider other)
        {
            if (triggered || !other.CompareTag("Player")) return;
            triggered = true;
            PlayScene3ObjectiveDirector.Instance?.ShowDialog(speaker, line, duration);
        }
    }
}
```
Sau đó dùng Unity_RunCommand để thêm DialogTriggerZone vào scene cho các vị trí trong bảng dialog ở trên.

---

### TASK 3 — Visual Respawn Flash Đỏ
**Script:** `PlayScene3RespawnManager.cs`
**Scope chỉ làm:**
- Khi player rơi xuống killPlaneY = -3.5f → flash màn hình đỏ 0.3s (alpha 0.4)
- Text "Đang hồi sinh..." ở giữa màn 1.5s
- Teleport về checkpoint gần nhất → text xanh "Đã hồi sinh" 1s
- Dùng `OnGUI` — KHÔNG tạo Canvas

---

### TASK 4 — Dọn Asset Thừa (Legacy Objects)
**Scope chỉ làm — Dùng Unity_RunCommand:**
**AN TOÀN ĐỂ XÓA:**
- Cove_Drawbridge_CreviceWall_0_Legacy
- Cove_Drawbridge_CreviceWall_1_Legacy
- Cove_Drawbridge_ReleasePin_Legacy
- Cove_Drawbridge_Chain_R_Legacy
- Cove_Drawbridge_Chain_L_Legacy
- Cove_Drawbridge_CreviceLid_Legacy
- Cove_Drawbridge_WinchPost_Legacy
- Cove_Drawbridge_PawlLever_Legacy

**TUYỆT ĐỐI KHÔNG XÓA:** Cove_Drawbridge_CoopController, Cove_Winch_Assembly, Shipwreck_Coop_Controller, CastleGateClimaxManager

---

### TASK 5 — Fix Collider Thang & Slope Walkable
**Scope chỉ làm — Dùng Unity_RunCommand:**
- Check và thêm BoxCollider hoặc MeshCollider walkable cho:
  - Cove_Staircase_New → Stair_Arrival_Deck, Stair_Mid_Landing
  - Spiral_Stairs_SmoothRamps
  - Cliff_Stairs_SmoothRampCollider
  - Watchtower_Ascent_Stairs
- Mọi mặt dốc < 45° cần PhysicsMaterial dynamicFriction = 0.9, staticFriction = 0.9
- Jump Pads: scale tối thiểu (2.0, 0.3, 2.0) để 2 người cùng đứng

---

### TASK 6 — Visual Feedback Cơ Chế
**Scope chỉ làm — Dùng Unity_RunCommand hoặc sửa script tương ứng:**

| Cơ chế | Hiệu ứng cần thêm |
|--------|------------------|
| Bánh lái xoay 100% | Light pulse vàng 0.5s trên Ship_WOW_KeyLight |
| Rương mở | Particle sparkle vàng (dùng ParticleSystem có sẵn, không import mới) |
| Chìa khóa nhặt | Nhân vật giơ cao Vector3.up * 0.5f trong 0.5s |
| Cổng lâu đài mở | Ánh sáng vàng từ khe cửa (enable CastleGate_KeyLight nếu có) |
| Bệ Rune | Light intensity tăng từ 0 → 1 trong 1s khi bước lên |
| Checkpoint | Flash xanh 0.3s + text "Điểm lưu!" 2s |
| Zipline bắt đầu | Camera shake nhẹ: transform.localPosition += Random.insideUnitSphere * 0.05f 1 frame |

---

### TASK 7 — Hoàn Chỉnh Flow Tàu Đắm (Zone 4)
**Script:** `ShipwreckCoopController.cs`
**Scope chỉ làm:**
1. Verify flow: Con leo thang dây → vào đài → hạ cầu → Ba qua → Ba xoay bánh lái → rương mở → Captain_Gold_Key active
2. Nếu isMastUnlocked: 0 hoặc isBridgeLowered: 0 trong scene → set = 1 bằng Unity_RunCommand
3. Ship_SteeringWheel_Helm position: (49.66, 6.21, -55.7) — verify đúng vị trí

---

### TASK 8 — Smooth Zipline Tháp Canh → Lâu Đài
**Script:** `CastleZiplineRideController.cs`
**Scope chỉ làm:**
- Verify launchPoint và landPoint không null trong Inspector
- Disable CharacterController khi trượt → enable lại khi đáp
- Bezier lerp: rideDuration = 2.8f, mid-point cong nhẹ xuống giữa đường
- Sau đáp: AddForce(forward * 2f, ForceMode.Impulse) nhỏ
- Cả 2 player (Ba + Con) đều có thể dùng zipline độc lập

---

### TASK 9 — Kiểm Tra Tổng Thể Gameplay Flow
**Scope chỉ làm:**
Dùng Unity_RunCommand để teleport character thử từng checkpoint và kiểm tra:

| Zone | Cơ chế | Collider | Dialog | Respawn |
|------|--------|----------|--------|---------|
| 1 — Cầu rút | ? | ? | ? | ? |
| 2 — Cáp treo | ? | ? | ? | ? |
| 3 — Bánh răng | ? | ? | ? | ? |
| 4 — Tàu đắm | ? | ? | ? | ? |
| 5 — Vách đá | ? | ? | ? | ? |
| 6 — Zipline | ? | ? | ? | ? |
| 7 — Lâu đài | ? | ? | ? | ? |
| 8 — Khinh khí cầu | ? | ? | ? | ? |

Báo cáo dạng bảng. Chỉ làm fix nhỏ nếu rõ ràng an toàn, task lớn thì list ra để user xác nhận.

---

## 🔗 KEY SCRIPTS THAM KHẢO

| Script | Mô tả |
|--------|-------|
| `ShipwreckCoopController.cs` | Cơ chế tàu đắm Zone4 |
| `CoopDrawbridgeController.cs` | Cầu rút vịnh hẹp |
| `CastleGateKeyUnlocker.cs` | Mở cổng lâu đài bằng key |
| `CastleCourtyardCoopController.cs` | Bệ Rune sân lâu đài |
| `CastleZiplineRideController.cs` | Zipline Tháp Canh → Lâu Đài |
| `PlayScene3RespawnManager.cs` | Checkpoint & Kill Zone |
| `PlayScene3ObjectiveDirector.cs` | UI gợi ý nhiệm vụ |
| `PlayScene3CoopBootstrap.cs` | Khởi tạo môi trường co-op |
| `Stage3ZiplineCoopController.cs` | Cáp treo Zone2 |
| `CastleGateClimaxManager.cs` | Climax cổng lâu đài |

---

## 🎮 NGUYÊN TẮC DESIGN — IT TAKES TWO STYLE

1. **Mỗi cơ chế cần CẢ 2 người** — không ai làm 1 mình được
2. **Phản hồi ngay lập tức** — bấm nút → thấy kết quả trong < 0.5s
3. **Checkpoint gần, không phạt nặng** — sai thì thử lại được ngay
4. **Camera thấy cả 2 nhân vật** — không split quá xa
5. **Audio/Visual cho mọi tương tác** — không được im lặng
6. **Dialog cảm xúc ngắn** — không lore dump, không giải thích quá dài
7. **Hướng dẫn ngầm** — đặt ánh sáng/màu sắc/arrow để player tự hiểu việc tiếp theo

---

## CẤM

- Xóa object có MonoBehaviour active mà không check kỹ
- Dùng `GameObject.CreatePrimitive()` thay asset 3D gốc
- Sửa `.unity` bằng text editor
- Tạo Canvas mới khi đã có UIDocument/OnGUI sẵn
- Import package mới
- Gọi `NetworkObject.SetParent()` / `transform.SetParent()` trên NetworkObject runtime
- Log dài hơn 2 dòng per lượt
- Làm rộng hơn task được yêu cầu

---

## CÁCH SỬ DỤNG

**Bước 1:** Copy toàn bộ file này làm System Prompt cho Claude khi mở project.

**Bước 2:** Mỗi lần ra lệnh, chỉ cần nêu task number + chi tiết nhỏ nếu có:
```
"Làm TASK 3 — flash đỏ, alpha 0.4 thôi, không cần text."
"Làm TASK 2 — tạo DialogTriggerZone.cs."
"Làm TASK 4 — dọn legacy objects."
```

**Bước 3:** Làm xong 1 task → kiểm tra → mới sang task tiếp theo.

**Bước 4:** Khi context > 20 tin nhắn → gõ /compact để tiết kiệm token.

---

## PROGRESS TRACKER

| Task | Mô tả | Status |
|------|-------|--------|
| TASK 1 | ShowDialog() trong ObjectiveDirector | Chưa |
| TASK 2 | DialogTriggerZone.cs + thêm vào scene | Chưa |
| TASK 3 | Visual Respawn Flash đỏ | Chưa |
| TASK 4 | Dọn Legacy Objects | Chưa |
| TASK 5 | Fix Collider Thang & Slope | Chưa |
| TASK 6 | Visual Feedback Cơ Chế | Chưa |
| TASK 7 | Hoàn chỉnh Flow Tàu Đắm | Chưa |
| TASK 8 | Smooth Zipline | Chưa |
| TASK 9 | Kiểm Tra Tổng Thể | Chưa |

---

Hold My Hand Game — PlayScene3 Master Prompt v2.0 — 2026-11-18
Dùng với Claude Sonnet/Opus để tối ưu chi phí
