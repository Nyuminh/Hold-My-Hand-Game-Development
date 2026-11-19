# 🎮 CLAUDE MASTER PROMPT — HOLD MY HAND: PlayScene3 Polish & Completion
> Phiên bản tối ưu chi phí. Mỗi task được chia nhỏ, có phạm vi rõ ràng. Claude chỉ làm đúng 1 task mỗi lần được yêu cầu.

---

## ⚡ ECONOMY RULES (BẮT BUỘC TUÂN THỦ — KHÔNG ĐƯỢC BỎ QUA)

1. **MAX 2 tool calls / lượt** — làm xong 1 bước, báo cáo ngắn, dừng lại chờ.
2. **KHÔNG tự chụp ảnh** — chỉ chụp khi người dùng nói: "cho xem ảnh" / "chụp lại".
3. **Log gọn** — `result.Log("Done: X")` — tuyệt đối không in danh sách dài.
4. **Trả lời ngắn gọn, action-first** — không chào hỏi, không lý thuyết.
5. **Nhắc `/compact`** khi hội thoại > 20 tin nhắn hoặc hoàn thành 1 feature lớn.
6. **Tuyệt đối KHÔNG sửa .unity bằng text editor** — chỉ dùng `Unity_RunCommand`.

---

## 🛠 UNITY MCP GOLDEN TEMPLATE

```csharp
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var target = GameObject.Find("Target_Name");
        if (target == null) { result.LogError("Not found!"); return; }

        Undo.RecordObject(target, "Modify");
        // ... thao tác ...
        EditorSceneManager.MarkSceneDirty(target.scene);
        EditorSceneManager.SaveOpenScenes();
        result.Log("Done: {0}", target.name);
    }
}
```

**Lưu ý Unity phiên bản này:**
- Dùng `obj.GetEntityId()` thay `obj.GetInstanceID()` (deprecated)
- Dùng `FindObjectsByType<T>(FindObjectsSortMode.None)` thay `FindObjectsOfType<T>()`
- Namespace tất cả scripts: `Blocks.Gameplay.Core`

---

## 🗺 BẢN ĐỒ PLAYSCENE3 — CẤU TRÚC 8 ZONE

```
[START] Zone1 (Lều Trại) → Zone2 (Thang Cẩu) → Zone3 (Bánh Răng Tời)
      → Zone4 (Cove Vịnh + Tàu Đắm) → Zone5 (Vách Đá Spiral) 
      → Zone6 (Tháp Canh + Dây Cáp) → Zone7 (Lâu Đài + Sân Trong) 
      → Zone8 (Khinh Khí Cầu Zephyr) [END]
```

### Vị trí thế giới (World Positions)
| Zone | Mô tả | Position (x, y, z) |
|------|--------|-------------------|
| 0 | Khởi đầu (Lều trại) | (-1, 1.25, 5) |
| 1 | Sau cầu rút Zone1 | (-1, 1.25, -15) |
| 2 | Bến cáp treo Islet | (-1, 10.85, -23) |
| 3 | Cao nguyên bánh răng | (-1, 14.5, -48) |
| 4 | Bến cầu vịnh Cove | (26, 3.8, -75) |
| 5 | Sàn tàu đắm (key) | (44.5, 4.5, -54) |
| 6 | Chân cầu thang vách đá | (-11.5, 14, -73.5) |
| 7 | Đỉnh Tháp Canh | (-10.5, 31.8, -87) |
| 8 | Sân cổng lâu đài | (6, 24.5, -101) |
| 9 | Sân trong lâu đài | (0, 24.5, -118.5) |
| 10 | Bệ khinh khí cầu | (0, 25, -135) |

---

## 📖 CỐT CHUYỆN "HOLD MY HAND" — PLAYSCENE3

### Tổng quan cảm xúc (Phong cách: IT TAKES TWO)
> Hai cha con cùng vượt qua vùng hoang dã cổ đại — không phải vì họ mạnh nhất, mà vì họ **không bỏ nhau lại**. Mỗi cơ chế phản chiếu mối quan hệ cha-con: ba dùng sức, con dùng sự linh hoạt. Cùng nhau mới làm được.

### Lore theo zone (Dùng cho dialog popup / gợi ý nhiệm vụ)

**Zone 1 — Vực Đá Lở:**
> *"Ba nắm tay con — qua bên kia thôi."*
> Hai cha con phát hiện cây cầu rút cổ. Ba giữ cần phải, con giữ cần trái. Cầu hạ xuống.

**Zone 2 — Thang Cẩu Bỏ Hoang:**
> *"Ba đứng đây giữ, con leo lên trước nha."*
> Con leo lên qua thang cẩu (cáp treo). Ba điều chỉnh đối trọng bên dưới để con qua được. Rồi con sửa hộp điện kéo Ba lên.

**Zone 3 — Cao Nguyên Bánh Răng Cổ:**
> *"Cái bánh răng này bị kẹt chốt, con tháo ra ba mới xoay được."*
> Con tháo chốt kẹt. Ba quay tời. Cầu rút Cove hạ xuống qua vụng biển.

**Zone 4 — Vịnh Hẹp Cove & Tàu Đắm:**
> *"Xuống thuyền đó đi con, trong đó có chìa khóa. Ba trông chừng cho."*
> Ba giữ thang cân bằng để con xuống xác tàu. Con xoay bánh lái mở rương — lấy Chìa Khóa Cổ Thành.
> Rồi hai người leo cầu thang gỗ cảng lên Tháp Canh.

**Zone 5 — Vách Đá Xoắn Ốc:**
> *"Từng bước một, con. Ba ở sau lưng con."*
> Hai cha con leo xoắn ốc theo vách núi để lên cao.

**Zone 6 — Tháp Canh & Dây Cáp Treo:**
> *"Bám chặt vào — con nhìn thấy lâu đài chưa?"*
> Từ đỉnh Tháp Canh, hai cha con bấm [E] ôm thanh dây cáp trượt qua vực sâu, đáp vào Sân Cổng Lâu Đài.

**Zone 7 — Lâu Đài Hoàng Gia:**
> *"Chìa khóa con cầm đó, cắm vào đây đi."*
> Con dùng Chìa Khóa Cổ Thành mở cổng lâu đài đôi. Bên trong: Ba bước lên Bệ Mặt Trời (vàng), Con bước lên Bệ Ánh Trăng (xanh) — Cổng Sập Hoàng Gia hé mở.

**Zone 8 — Khinh Khí Cầu Zephyr (Ending):**
> *"Lên đây rồi ba con mình về nhà thôi."*
> Hai cha con cùng bước lên khinh khí cầu. Cắt cảnh kết thúc — cầu treo dây cáp bốc lửa sáng lên trời đêm.

---

## 🎯 DANH SÁCH TASK (Chọn 1 task mỗi lần — đọc kỹ task trước khi làm)

---

### TASK 1 — Hướng Dẫn Nội Màn (In-Game Guidance UI)
**Script cần sửa:** `PlayScene3ObjectiveDirector.cs`
**Yêu cầu:**
- Mỗi khi người chơi tiến vào trigger zone, hiển thị popup "Gợi ý nhiệm vụ" ở góc dưới màn.
- Popup gồm: **icon nhân vật** (👨 Ba / 👦 Con) + **text hành động** ngắn.
- Popup tự ẩn sau 5 giây. Có thể gọi lại bằng [Tab].
- Dùng `OnGUI` hoặc `UIDocument` đã có. Không tạo thêm Canvas.

---

### TASK 2 — Dọn Asset Thừa Trong Scene
**Danh sách object AN TOÀN ĐỂ XÓA** (các object Legacy không dùng):
- `Cove_Drawbridge_CreviceWall_0_Legacy`
- `Cove_Drawbridge_CreviceWall_1_Legacy`
- `Cove_Drawbridge_ReleasePin_Legacy`
- `Cove_Drawbridge_Chain_R_Legacy`
- `Cove_Drawbridge_Chain_L_Legacy`
- `Cove_Drawbridge_CreviceLid_Legacy`
- `Cove_Drawbridge_WinchPost_Legacy`
- `Cove_Drawbridge_PawlLever_Legacy`

**TUYỆT ĐỐI KHÔNG XÓA:** Cove_Drawbridge_CoopController, Cove_Winch_Assembly, Shipwreck_Coop_Controller.

---

### TASK 3 — Fix Collider Thang & Mặt Phẳng Walkable
**Vị trí cần check:**
- `Cove_Staircase_New` → Stair_Arrival_Deck, Stair_Mid_Landing
- `Spiral_Stairs_SmoothRamps`
- `Cliff_Stairs_SmoothRampCollider`
- `Watchtower_Ascent_Stairs`
- Mọi mặt dốc < 45° cần PhysicsMaterial `dynamicFriction = 0.9`

---

### TASK 4 — Cơ Chế Rơi Chết & Respawn
**Script đã có:** `PlayScene3RespawnManager.cs`
- `killPlaneY = -3.5f` → khi `y < killPlaneY` → trigger respawn
- Flash đỏ 0.3s → text "💀 Hồi sinh..." 1.5s → teleport về checkpoint
- Checkpoint auto-trigger khi player bước vào radius

---

### TASK 5 — Visual Feedback Cho Cơ Chế

| Cơ chế | Hiệu ứng |
|--------|---------|
| Bánh lái xoay 100% | Light pulse vàng |
| Rương kho báu mở | Particle sparkle vàng |
| Chìa khóa nhặt | Nhân vật giơ cao 0.5s |
| Cổng lâu đài mở | Ánh sáng vàng từ khe cửa |
| Bệ Rune | Light intensity tăng khi bước lên |
| Checkpoint | Flash xanh + text "✓ Điểm lưu!" |
| Zipline bắt đầu | Camera shake nhẹ |

---

### TASK 6 — Dialog Cảm Xúc (Emotional Dialogue)
Tạo `DialogTriggerZone.cs`:
```csharp
public class DialogTriggerZone : MonoBehaviour
{
    public string speaker; // "Ba" / "Con"
    public string line;
    public float duration = 3.5f;
    private bool triggered = false;
    
    private void OnTriggerEnter(Collider other)
    {
        if (triggered || !other.CompareTag("Player")) return;
        triggered = true;
        PlayScene3ObjectiveDirector.Instance?.ShowDialog(speaker, line, duration);
    }
}
```

**Dialog list:**
| Vị trí | Nhân vật | Câu thoại |
|--------|----------|-----------|
| Trước cầu rút Zone1 | Ba | *"Con cầm cần bên phải nha, ba cầm bên này."* |
| Sau qua cầu Zone1 | Con | *"Ba ơi, còn xa không?"* |
| Trước cáp treo Zone2 | Ba | *"Lên trước đi, ba theo sau."* |
| Trước rương kho báu | Con | *"Trong đây có gì vậy ba?"* |
| Nhặt chìa khóa | Ba | *"Con cầm cái này, đừng để mất."* |
| Trước cổng lâu đài | Con | *"Ba, chìa khóa vừa khớp rồi!"* |
| Bệ Rune sáng lên | Ba | *"Đứng đây không nhúc nhích nha con."* |
| Trước zipline | Con | *"Ba... cao lắm!"* |
| Lên khinh khí cầu | Ba | *"Mình về nhà thôi con."* |

---

### TASK 7 — Tối Ưu Zipline / Cáp Treo
**Script:** `CastleZiplineRideController.cs`
- Kiểm tra `launchPoint`, `landPoint` không null
- Disable `CharacterController` rider khi trượt, enable lại khi đáp
- Lerp vị trí theo Bezier, `rideDuration = 2.8f`
- Sau đáp: add impulse nhỏ về phía trước

---

### TASK 8 — Hoàn Chỉnh Flow Tàu Đắm
**Script:** `ShipwreckCoopController.cs`

Flow: Con leo thang dây → vào đài quan sát → hạ cầu → Ba qua → Ba xoay bánh lái → rương mở → lấy chìa khóa.

Các vị trí:
- Bánh lái: `Ship_SteeringWheel_Helm` tại `(49.66, 6.21, -55.7)`
- Rương: `GameObject.Find("HMH_West_Chest")`
- Chìa khóa: `Captain_Gold_Key`

---

### TASK 9 — Smooth Platforming
- Jump Pads: scale tối thiểu `(2.0, 0.3, 2.0)` cho 2 người đứng
- Mặt dốc > 50°: thêm wall collider dày 0.1m
- 2 người cùng đứng platform: cả 2 bật lên cùng lúc

---

### TASK 10 — Kiểm Tra Tổng Thể

| Zone | Cơ chế | Collider | Dialog | Respawn | % Hoàn thành |
|------|--------|----------|--------|---------|-------------|
| 1 | ✅/❌ | ✅/❌ | ✅/❌ | ✅/❌ | % |
| ... | ... | ... | ... | ... | ... |

---

## 🔗 KEY SCRIPTS

| Script | Mô tả |
|--------|-------|
| `ShipwreckCoopController.cs` | Cơ chế tàu đắm Zone 4 |
| `CoveDrawbridgeCoopController.cs` | Cầu rút vịnh hẹp Zone 3-4 |
| `CastleGateKeyUnlocker.cs` | Mở cổng lâu đài bằng key |
| `CastleCourtyardCoopController.cs` | Bệ Rune sân lâu đài Zone 7 |
| `CastleZiplineRideController.cs` | Zipline Tháp Canh → Lâu Đài |
| `PlayScene3RespawnManager.cs` | Checkpoint & Kill Zone |
| `PlayScene3ObjectiveDirector.cs` | UI gợi ý nhiệm vụ |
| `PlayScene3CoopBootstrap.cs` | Khởi tạo môi trường co-op |
| `CoopRopeLadderDrop.cs` | Thang dây leo trên tàu |
| `Stage3ZiplineCoopController.cs` | Cáp treo Zone 2 |

---

## 🎮 NGUYÊN TẮC DESIGN — IT TAKES TWO STYLE

1. **Mỗi cơ chế cần CẢ 2 người** — không ai làm 1 mình được.
2. **Phản hồi ngay lập tức** — bấm nút → thấy kết quả trong < 0.5s.
3. **Checkpoint gần, không phạt nặng** — sai thì thử lại được.
4. **Camera thấy cả 2 nhân vật** — không split quá xa.
5. **Audio/Visual cho mọi tương tác** — không được im lặng.
6. **Dialog cảm xúc ngắn** — không lore dump, không giải thích quá dài.

---

## 🚫 KHÔNG ĐƯỢC LÀM

- Xóa object có MonoBehaviour active mà không check kỹ
- Dùng `GameObject.CreatePrimitive()` thay asset 3D gốc
- Sửa `.unity` bằng text editor
- Tạo Canvas mới khi đã có UIDocument sẵn
- Import package mới
- Gọi `NetworkObject.SetParent()` / `transform.SetParent()` trên NetworkObject runtime
- Log dài hơn 2 dòng per lượt

---

## 📋 CÁCH SỬ DỤNG

**Bước 1:** Copy toàn bộ file này làm system context cho Claude.

**Bước 2:** Mỗi lần ra lệnh, chỉ nêu task number:
```
"Làm TASK 4 - Respawn. Flash đỏ alpha 0.3 thôi."
```

**Bước 3:** Làm xong 1 task → kiểm tra → mới sang task tiếp theo.

**Bước 4:** Khi context > 20 tin nhắn → gõ `/compact` để tiết kiệm token.

---
*Hold My Hand Game — PlayScene3 Master Prompt v1.0 — 2026-11-18*
