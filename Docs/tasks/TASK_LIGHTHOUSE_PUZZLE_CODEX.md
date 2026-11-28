# 📋 TASK REVISION 4: CLIENT-HOST POSITION DESYNC FIX & RUNTIME TRANSFORM ENFORCEMENT
> **Dành cho**: OpenAI Codex / Lập trình viên  
> **Dự án**: Hold My Hand · Nắm tay cha (Unity 6 / Netcode for GameObjects)  
> **Scene mục tiêu**: `Assets/Core/Scenes/PlayScene2.unity` (Stage 4: Misty Marsh & Twin Beacons)  
> **Lỗi cần giải quyết**: BÊN HOST ĐÃ THẤY SỬA VỊ TRÍ, NHƯNG BÊN CLIENT (BẠN CHƠI) VẪN THẤY VÔ-LĂNG & LĂNG KÍNH Ở VỊ TRÍ CŨ LƠ LỬNG.

---

## 🔍 1. NGUYÊN NHÂN GÂY LỖI LỆCH VỊ TRÍ GIỮA HOST VÀ CLIENT (ROOT CAUSES)

Tại sao Host thấy vị trí mới trên bệ đá, còn Client (bạn cùng chơi) lại thấy vị trí cũ lơ lửng?
1. **Scene `PlayScene2.unity` chưa được commit & push lên Git**:
   - Host chạy lệnh bake scene `LevelTwoSceneBuilder.BakeStageThree()` trên máy cục bộ của mình.
   - Nhưng file scene `Assets/Core/Scenes/PlayScene2.unity` hiện đang ở trạng thái `modified` (chưa commit trong commit `425d2b6`).
   - Khi người bạn kéo code (`git pull`), Client **không nhận được scene mới** mà vẫn dùng file scene cũ từ trước!
2. **In-Scene NetworkObjects không tự đồng bộ Transform tĩnh**:
   - Các vật thể như `East_ItemSocket`, `East_TurnWheel`, `East_BeaconPedestal` là in-scene placed GameObjects không có component `NetworkTransform` riêng.
   - Khi Client kết nối, Client tải vị trí từ file `.unity` cục bộ của Client. Nếu file scene của Client là bản cũ, Client sẽ hiển thị vật thể tại vị trí cũ ($y = 3.2\text{m}$ lơ lửng).
   - Khi Lăng kính được cắm vào socket (`isSocketed = true`), hàm `CoopCarryableItem` thực thi trên Client:
     `transform.SetPositionAndRotation(socketOwner.itemSocket.position, socketOwner.itemSocket.rotation);`
     Do `itemSocket` trên máy Client ở vị trí cũ, Lăng kính trên màn hình của bạn chơi cũng bị kéo lên vị trí cũ lơ lửng!

---

## 🛡️ 2. GIẢI PHÁP KÉP BẢO VỆ 100% (THE BULLETPROOF 2-LAYER FIX)

Để giải quyết triệt để và đảm bảo **dù Client có bị lệch cache scene hay chưa kịp pull scene mới thì khi vào game vẫn hiển thị đúng 100%**, ta áp dụng 2 lớp giải pháp:

### 🔹 LỚP 1: ÉP VỊ TRÍ CHUẨN XÁC KHI KHỞI CHẠY (RUNTIME ENFORCEMENT TRONG C#)
Trong script `MarshBeaconTrigger.cs`, thêm hàm cưỡng chế vị trí chuẩn xác trong `Awake()` và `OnNetworkSpawn()`. Hàm này chạy trên cả Host và Client khi scene được tải, ngay lập tức di dời các Transform về đúng tọa độ mặt đất:

#### Code cập nhật trong `Assets/Core/Scripts/Runtime/Components/MarshBeaconTrigger.cs`:
```csharp
public Transform pedestal;

private void Awake()
{
    EnforceAuthoritativeTransforms();
}

public override void OnNetworkSpawn()
{
    base.OnNetworkSpawn();
    EnforceAuthoritativeTransforms();
    ...
}

public void EnforceAuthoritativeTransforms()
{
    bool isWest = (requiredItem != null && requiredItem.itemType == CoopCarryableItem.ItemType.IgnitionCrank) ||
                  name.StartsWith("West");

    // Tọa độ chuẩn xác trên bệ đá đảo hải đăng:
    Vector3 targetPedestal = isWest ? new Vector3(84.6f, 1.3f, 175.8f) : new Vector3(115.4f, 1.3f, 189.2f);
    Vector3 targetSocket   = isWest ? new Vector3(84.6f, 1.7f, 175.8f) : new Vector3(115.4f, 1.7f, 189.2f);
    Vector3 targetWheel    = isWest ? new Vector3(85.2f, 1.6f, 175.8f) : new Vector3(114.8f, 1.6f, 189.2f);
    Vector3 targetLamp     = isWest ? new Vector3(82.99f, 7.56f, 174.99f) : new Vector3(117.01f, 7.56f, 190.01f);

    // 1. Cập nhật bệ đá (Pedestal)
    if (pedestal == null)
    {
        var foundPedestal = transform.Find(isWest ? "West_BeaconPedestal" : "East_BeaconPedestal")
                         ?? transform.Find(isWest ? "West_BeaconConsole" : "East_BeaconConsole");
        if (foundPedestal != null) pedestal = foundPedestal;
    }
    if (pedestal != null) pedestal.position = targetPedestal;

    // 2. Cập nhật ổ cắm Lăng kính / Tay quay (ItemSocket)
    if (itemSocket != null) itemSocket.position = targetSocket;

    // 3. Cập nhật vô-lăng và điểm tương tác (TurnWheel)
    if (turnWheel != null)
    {
        if (turnWheel.interactionPoint != null) turnWheel.interactionPoint.position = targetWheel;
        if (turnWheel.lanternHead != null) turnWheel.lanternHead.position = targetLamp;
    }

    // 4. Cập nhật emitter tia sáng trên đỉnh tháp
    if (beam != null && beam.emitter != null)
    {
        beam.emitter.position = targetLamp;
    }
}
```

#### Code cập nhật trong `Assets/Core/Scripts/Runtime/Components/CoopCarryableItem.cs`:
Khi vật phẩm đã được cắm vào socket, bảo đảm Client cập nhật vị trí theo tọa độ thực tế:
```csharp
private void LateUpdate()
{
    if (!IsSpawned) return;
    if (IsSocketed && socketOwner != null && socketOwner.itemSocket != null)
    {
        transform.SetPositionAndRotation(socketOwner.itemSocket.position, socketOwner.itemSocket.rotation);
        return;
    }
    ...
}
```

---

### 🔹 LỚP 2: BAKE, COMMIT VÀ PUSH SCENE `PlayScene2.unity`
1. Đảm bảo file `Assets/Core/Scenes/PlayScene2.unity` và các file liên quan được commit vào Git:
   - `Assets/Core/Scenes/PlayScene2.unity`
   - `Assets/Core/Materials/LVL2_LighthouseBeam.mat`
   - `Assets/Core/Materials/LVL2_LighthouseBeam.mat.meta`
2. Người bạn chơi (Client) sau khi pull code hoặc tải bản build mới sẽ có đúng 100% dữ liệu scene gốc.

---

## 🧪 3. KIỂM THỬ XÁC NHẬN (VERIFICATION PLAN)

1. **Kiểm tra tự động với Netcode Host & Client**:
   - Chạy `LevelTwoValidation.RunStageThree()`.
   - Kiểm tra `west.itemSocket.position` và `east.itemSocket.position` chuẩn xác tại:
     - West Socket: `(84.6, 1.7, 175.8)`
     - East Socket: `(115.4, 1.7, 189.2)`
   - Kiểm tra `west.turnWheel.interactionPoint.position` và `east.turnWheel.interactionPoint.position` tại:
     - West Wheel: `(85.2, 1.6, 175.8)`
     - East Wheel: `(114.8, 1.6, 189.2)`
2. **Kiểm tra trực quan trên 2 máy**:
   - Máy 1 (Host - Ba): Nhìn thấy bệ đá, ổ cắm và vô-lăng đặt ngay dưới chân tháp.
   - Máy 2 (Client - Con): Kết nối vào phòng, nhìn thấy bệ đá, ổ cắm và vô-lăng **ở cùng vị trí chính xác dưới chân tháp, không còn bị lơ lửng**.
   - Con cắm Lăng Kính vào ổ Tháp Đông: Cả hai máy đều thấy Lăng Kính nằm gọn trên bệ đá dưới chân tháp!

---

## 🏁 ACCEPTANCE CRITERIA
1. [ ] `EnforceAuthoritativeTransforms()` được gọi trong `Awake()` và `OnNetworkSpawn()` của `MarshBeaconTrigger.cs`, bảo đảm Client tự động cập nhật vị trí vô-lăng và lăng kính khi tải scene.
2. [ ] Máy Client (bạn cùng chơi) nhìn thấy vô-lăng và lăng kính đặt ngay trên bệ đá dưới chân hải đăng ($y \approx 1.6\text{m} - 1.7\text{m}$), trùng khớp 100% với màn hình của Host.
3. [ ] `PlayScene2.unity` và `LVL2_LighthouseBeam.mat` được thêm vào commit Git.
4. [ ] `LevelTwoValidation.RunStageThree()` tiếp tục pass 100%.
idcc