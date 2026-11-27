# 📋 TASK REVISION: LIGHTHOUSE CONTROLS, MOUSE STEERING & TOP-LANTERN BEAM
> **Dành cho**: OpenAI Codex / Lập trình viên  
> **Dự án**: Hold My Hand · Nắm tay cha (Unity 6 / Netcode for GameObjects)  
> **Scene mục tiêu**: `Assets/Core/Scenes/PlayScene2.unity` (Stage 4: Misty Marsh & Twin Beacons)  
> **Nhiệm vụ**: Tinh chỉnh điều khiển chuột, phím F, hạ bệ điều khiển xuống chân tháp và phát tia sáng từ ĐỈNH hải đăng.

---

## 📌 1. BỐI CẢNH & CÁC LỖI THỰC TẾ CẦN SỬA (THE 4 CRITICAL REFINEMENTS)

Dựa trên phản hồi và ảnh chụp trực tiếp từ người chơi:

1. **Đổi phím điều khiển**:
   - **Xoay tia sáng**: Đổi từ phím `A / D` sang **Xoay chuột ngang (Mouse X movement)**. Khi đang lái vô-lăng, người chơi lia chuột trái/phải để xoay góc đèn mượt mà như góc nhìn thứ ba/thứ nhất.
   - **Nhặt & Gắn vật phẩm**: Đổi từ phím `[E]` sang phím **`[F]`** (nhặt `[F]`, gắn vào bệ `[F]`, giữ `[G]` để thả).
2. **Khắc phục vị trí lơ lửng, khó đứng lên (Ảnh 1)**:
   - Hiện tại: Vô-lăng và Ổ cắm đang bị đặt ở $y = 3.2m$, lơ lửng giữa trời, nhân vật đứng dưới nước không chạm tới được và rất khó nhảy lên.
   - Sửa lại: Đặt **Bệ Máy Điều Khiển (Beacon Console Pedestal)** nằm vững vàng trên mặt sàn bệ đá đảo hải đăng ($y \approx 1.25f - 1.5f$). Vô-lăng và ổ cắm gắn trên bệ này, ngang tầm tay nhân vật (ngang ngực). Thêm cầu dốc bước chân để đi từ thuyền lên sàn đảo dễ dàng.
3. **Phát tia sáng từ ĐỈNH của ngọn hải đăng (Ảnh 2)**:
   - Hiện tại: Tia sáng phóng ra từ chiếc hộp lơ lửng ngang hông tháp ở $y = 3.5m$.
   - Sửa lại: Tia sáng phải phát ra từ **Lồng Kính trên ĐỈNH tháp hải đăng** (vị trí `tower.GetComponentInChildren<Light>(true).transform.position` ở độ cao $y \approx 9m$).
   - Bánh lái ở dưới đất xoay cơ cấu đài đèn trên đỉnh tháp. Khi xoay chuột dưới đất, chùm sáng từ trên đỉnh tháp quét ngang trời cực kỳ tráng lệ.

---

## 🛠️ 2. CHI TIẾT KỸ THUẬT CẦN SỬA (FILE-BY-FILE CODE CHANGES)

### File 1: `Assets/Core/Scripts/Runtime/Components/CoopCarryableItem.cs`
- **Đổi phím Nhặt và Gắn sang `[F]`**:
  ```csharp
  // Khi đang cầm vật phẩm:
  if (Keyboard.current.gKey.wasPressedThisFrame) RequestDropRpc();
  else if (Keyboard.current.fKey.wasPressedThisFrame && CanSocket(local)) RequestSocketRpc();

  // Khi chưa cầm vật phẩm:
  if (!IsCarried && !IsSocketed && Keyboard.current.fKey.wasPressedThisFrame &&
      Vector3.Distance(local.transform.position, transform.position) < 2.5f && IsClosestPickup(local))
  {
      RequestPickupRpc();
  }
  ```
- **Cập nhật UI Prompt**:
  - Gần vật phẩm: `"[F] Nhặt " + (itemType == ItemType.IgnitionCrank ? "Tay Quay" : "Lăng Kính Cổ")`
  - Đang cầm và gần ổ cắm: `"[F] Gắn vào Ổ Cắm Hải Đăng"`
  - Đang cầm tự do: `"[G] Thả vật phẩm"`

---

### File 2: `Assets/Core/Scripts/Runtime/Components/LighthouseTurnWheel.cs`
- **Chuyển sang điều khiển xoay bằng Chuột (Mouse Delta X)**:
  - Khi bắt đầu lái (bấm `[E]` hoặc `[F]` tại vô-lăng):
    - Khóa con trỏ hoặc đọc `Mouse.current.delta.x.ReadValue()`.
  - Trong hàm `Update()`:
    ```csharp
    // Phím thoát chế độ lái: [E], [F] hoặc [Space]
    if (Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.fKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame)
    {
        localSteering = false;
        ReleaseControlRpc();
        return;
    }

    // Đọc di chuyển chuột ngang
    float mouseDeltaX = Mouse.current != null ? Mouse.current.delta.x.ReadValue() : 0f;
    // Hỗ trợ thêm A/D làm fallback nhẹ nếu không có chuột
    float keyInput = (Keyboard.current.dKey.isPressed ? 1f : 0f) - (Keyboard.current.aKey.isPressed ? 1f : 0f);
    float turnInput = Mathf.Abs(mouseDeltaX) > 0.1f ? mouseDeltaX * 0.15f : keyInput;

    if (Mathf.Abs(turnInput) > 0.01f)
    {
        RotateRpc(turnInput, Mathf.Min(Time.deltaTime, 0.1f));
    }
    ```
- **Cập nhật UI Prompt**:
  - Khi đứng gần vô-lăng: `"[F] Cầm lái Vô-lăng căn tia sáng"`
  - Khi đang lái: `"Lia Chuột [◄ ►] để xoay tia sáng · [F / Space] Rời vô-lăng"`

---

### File 3: `Assets/Core/Scripts/Editor/LevelTwoSceneBuilder.cs`
Cập nhật hàm `WireStageThree()`:

#### A. Hạ Bệ Điều Khiển & Vô-Lăng xuống chân tháp (Console Pedestal):
- Vị trí chân tháp trên đảo:
  - Tháp Tây: Đảo tại $x=83, z=175$. Đặt bệ console tại `Vector3(85.5f, 1.25f, 175f)`.
  - Tháp Đông: Đảo tại $x=117, z=190$. Đặt bệ console tại `Vector3(114.5f, 1.25f, 190f)`.
- Tạo một khối bệ đá console vững chắc:
  ```csharp
  var console = Box(side + "_BeaconConsole", root, consolePos, new Vector3(1.6f, 0.9f, 1.2f), stoneMat);
  ```
- **Ổ cắm (`itemSocket`)**: Đặt trên đỉnh bệ console tại `consolePos + Vector3.up * 0.5f` ($y \approx 1.75f$), người chơi đứng trên bệ đảo là bấm **[F]** cắm vào được ngay!
- **Bánh lái (`turnWheel`)**: Gắn vào mặt trước bệ console tại `consolePos + Vector3.up * 0.4f + forward * 0.6f`, xoay dựng đứng, ngang tầm tay nhân vật.
- Thêm đường dốc / cầu gỗ (`WoodenRamp`) từ mạn thuyền nối thẳng lên bệ đá đảo để người chơi đi bộ lên êm ái.

#### B. Đưa Emitter và Tia Sáng lên ĐỈNH Hải Đăng:
- Tìm đèn trên đỉnh tháp hải đăng:
  ```csharp
  var tower = Find(side + "Beacon_Tower");
  var lamp = tower.GetComponentInChildren<Light>(true);
  Vector3 topLanternPosition = lamp.transform.position; // Cao ~9m
  ```
- **Pivot xoay (`lanternHead`)**:
  - Gắn tại đỉnh tháp `topLanternPosition`.
  - Vô-lăng dưới đất sẽ điều khiển xoay góc của `lanternHead` trên đỉnh tháp!
- **`beam.emitter`**:
  - Đặt tại `topLanternPosition`.
  - Tháp Tây: Emitter trên đỉnh Tháp Tây chiếu chùm sáng từ trên cao hướng sang đỉnh Tháp Đông.
  - Tháp Đông: Khối Lăng Kính tiếp nhận đặt tại đỉnh Tháp Đông. Khi nhận được tia sáng Tây, tia sáng thứ cấp phóng từ đỉnh Tháp Đông rọi xuống `LuminaMillSensor` của Cối Xay Nước!
- Xóa bỏ hoàn toàn các khối hộp lơ lửng cũ ở giữa không trung!

---

### File 4: `Assets/Core/Scripts/Editor/LevelTwoValidation.cs`
- Cập nhật test step 2 & 4: kiểm tra nhặt và gắn bằng phím `[F]`.
- Kiểm tra `west.beam.emitter.position.y > 6.0f` (xác nhận tia sáng phát ra từ đỉnh tháp cao).
- Kiểm tra `west.turnWheel.interactionPoint.position.y < 2.0f` (xác nhận vô-lăng đặt ở tầm tay dưới đất).

---

## 🎮 3. HƯỚNG DẪN CHƠI CẬP NHẬT (UPDATED PLAYER GUIDE)

1. **Nhặt và Gắn vật phẩm**:
   - Sử dụng phím **`[F]`** để nhặt **Tay Quay Khởi Động** và **Lăng Kính Cổ**.
   - Mang lại bệ console đá dưới chân hải đăng, bấm **`[F]`** để gắn vào ổ cắm.
2. **Lái Vô-lăng & Căn chỉnh tia sáng**:
   - Lại gần bánh lái dưới chân hải đăng, bấm **`[F]`** để cầm lái.
   - **Di chuyển chuột sang Trái / Phải** để xoay góc chùm sáng phát ra từ đỉnh ngọn hải đăng.
   - Bấm **`[F]`** hoặc **`[Space]`** để buông tay lái.
3. **Mục tiêu**:
   - Ba xoay chuột Tháp Tây nhắm tia sáng từ đỉnh Tháp Tây trúng lồng kính đỉnh Tháp Đông.
   - Con xoay chuột Tháp Đông nhắm tia phản xạ trúng Mắt Ngọc Cối Xay $\rightarrow$ Hoàn thành màn chơi!

---

## 🏁 ACCEPTANCE CRITERIA
1. [ ] Phím `[F]` hoạt động trơn tru cho cả Nhặt và Cắm vật phẩm (không dùng E cho item nữa).
2. [ ] Bánh lái vô-lăng và Ổ cắm đặt vững trên bệ đá dưới chân hải đăng ($y \approx 1.3f - 1.7f$), không còn lơ lửng giữa trời như Hình 1.
3. [ ] Chùm sáng phát ra từ đỉnh lồng kính của ngọn hải đăng trên cao ($y > 8m$) như Hình 2.
4. [ ] Xoay chuột trái/phải điều khiển góc tia sáng mượt mà.
5. [ ] Căn tia sáng trúng Cối Xay mở rào chắn an toàn thành công.
6. [ ] Chạy `LevelTwoValidation.RunStageThree()` kiểm tra PASS 100%.
