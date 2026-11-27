# 📋 TASK REVISION 3: CRITICAL PUZZLE REFINEMENTS & DYNAMIC FOG LOGIC
> **Dành cho**: OpenAI Codex / Lập trình viên  
> **Dự án**: Hold My Hand · Nắm tay cha (Unity 6 / Netcode for GameObjects)  
> **Scene mục tiêu**: `Assets/Core/Scenes/PlayScene2.unity` (Stage 4: Misty Marsh & Twin Beacons)  
> **Trạng thái**: KHẮC PHỤC 5 ĐIỂM BẤT CẬP THEO ẢNH CHỤP THỰC TẾ CỦA NGƯỜI CHƠI

---

## 📌 1. NĂM ĐIỂM BẤT CẬP CẦN KHẮC PHỤC TRIỆT ĐỂ (THE 5 CORE REFINEMENTS)

### 🔴 Điểm 1: Tia sáng bị mờ, không thấy rõ ở một số góc nhìn (Ảnh 1)
- **Hiện trạng**: `LineRenderer` chỉ rộng $0.35\text{m}$, dùng `Default-Line.mat` không có phát sáng (Emission), bị chìm vào nền trời/nước.
- **Yêu cầu sửa**:
  - Tăng độ rộng tia sáng: `startWidth = 0.8f`, `endWidth = 1.1f`.
  - Tạo/gán Material phát quang rực rỡ (Shader `Universal Render Pipeline/Unlit`, màu vàng kim có HDR Intensity $\times 3$).
  - Thêm một vầng sáng / Flare Marker tại điểm va chạm `hit.point` để người chơi thấy rõ điểm tiếp xúc của tia sáng trên địa hình/mục tiêu.

---

### 🔴 Điểm 2: Sương mù phải biến mất / xuất hiện lại ĐỘNG theo tia sáng (Ảnh 2)
- **Hiện trạng**:
  - Collider của Mắt Ngọc Cối Xay quá to ($2.2\text{m} \times 2.2\text{m}$), tia sáng mới chiếu chệch sang bên trái đã kích hoạt trúng collider tàng hình.
  - Khi đã kích hoạt, biến `isPowered` bị khóa vĩnh viễn, người chơi xoay tia sáng lệch ra ngoài mà sương mù vẫn không quay trở lại!
- **Yêu cầu sửa**:
  - Đổi collider của `LuminaMillSensor` sang `SphereCollider` gắn trực tiếp vào quả cầu cảm biến với bán kính $r = 0.55\text{m}$ (khớp chuẩn xác với mắt ngọc).
  - **Cơ chế sương mù động thời gian thực (Dynamic Fog & Safety Boom)**:
    - **Khi tia sáng chiếu TRÚNG Mắt Ngọc**: Mắt Ngọc sáng xanh, sau $0.3\text{s}$ sương mù bắt đầu tan dần, rào chắn an toàn cối xay nâng lên.
    - **Khi xoay tia sáng LỆCH RA NGOÀI Mắt Ngọc**: Mắt Ngọc tắt về màu đỏ, **SƯƠNG MÙ LẬP TỨC QUAY TRỞ LẠI DÀY ĐẶC** (`fogDensity` tăng lại mức $0.065$), rào chắn an toàn hạ xuống!
    - *(Chỉ khi người chơi duy trì tia sáng đúng vị trí VÀ chèo thuyền vượt qua cổng cối xay `watermill.Passed == true` thì chặng đầm lầy mới khóa hoàn thành vĩnh viễn)*.

---

### 🔴 Điểm 3: Bệ Lăng Kính và Vô-Lăng phải nằm vững trên BỆ ĐÁ dưới chân hải đăng, KHÔNG được lơ lửng ngoài mép (Ảnh 3)
- **Hiện trạng**: Bệ socket và vô-lăng đặt ở $x=110$ và $y=3.2\text{m}$, nằm ngoài rìa bán kính đảo đá ($x=117$) nên bị thò ra ngoài nước và lơ lửng giữa trời.
- **Yêu cầu sửa**:
  - Tọa độ tâm đảo đá: Tháp Tây tại $(83.0f, 0.9f, 175.0f)$, Tháp Đông tại $(117.0f, 0.9f, 190.0f)$. Mặt sàn bệ đá phẳng ở độ cao $y \approx 1.3\text{m}$.
  - Tạo khối bệ đá mỹ thuật `BeaconPedestal` đặt vững vàng trên sàn đảo, ngay trước lối vào hải đăng:
    - **Tháp Tây**: Vị trí `Vector3(84.6f, 1.3f, 175.8f)`.
    - **Tháp Đông**: Vị trí `Vector3(115.4f, 1.3f, 189.2f)`.
  - Trên bệ đá này:
    - Ổ cắm Lăng Kính / Tay Quay đặt phẳng trên mặt bệ đá ở $y = 1.7\text{m}$.
    - Bánh lái vô-lăng dựng đứng trên mặt bệ đá ở $y = 1.6\text{m}$.
    - Nhân vật đứng trên sàn bệ đá phẳng, ngang tầm tay bấm **`[F]`** để cắm hoặc cầm lái, không còn bất kỳ chi tiết nào lơ lửng ngoài không trung!

---

### 🔴 Điểm 4: Tia sáng phải phát ra từ ÁNH ĐÈN Ở ĐỈNH NGỌN HẢI ĐĂNG (Ảnh 4)
- **Hiện trạng**: Emitter tia sáng đang bị gắn ngang hông bệ dưới đất ở $y = 3.5\text{m}$.
- **Yêu cầu sửa**:
  - Điểm phát tia sáng (`beam.emitter`) phải gắn trực tiếp vào **Lồng Kính trên ĐỈNH tháp hải đăng** (vị trí `Light` của prefab tại $y = 7.56\text{m}$ đến $9.0\text{m}$ như trong Ảnh 4):
    - Đỉnh Tháp Tây: `Vector3(82.99f, 7.56f, 174.99f)`.
    - Đỉnh Tháp Đông: `Vector3(117.01f, 7.56f, 190.01f)`.
  - Vô-lăng ở dưới chân tháp điều khiển xoay đài đèn / lăng kính trên đỉnh tháp (`lanternHead`).
  - Tia sáng phóng ra từ vòm kính rực sáng ở đỉnh tháp, quét từ trên cao qua mặt đầm lầy xuống mục tiêu.

---

### 🔴 Điểm 5: Đổi điều khiển vô-lăng từ xoay chuột sang LĂN CHUỘT (Mouse Scroll Wheel)
- **Hiện trạng**: Lia chuột ngang dễ xung đột với camera xoay góc nhìn.
- **Yêu cầu sửa**:
  - Khi đang cầm lái vô-lăng:
    - **Lăn chuột LÊN (Scroll Up > 0)**: Xoay tia sáng sang Phải.
    - **Lăn chuột XUỐNG (Scroll Down < 0)**: Xoay tia sáng sang Trái.
    - Giữ phím `A / D` làm phương án hỗ trợ phụ.
    - Bấm **`[F]`** hoặc **`[Space]`** để rời khỏi vô-lăng.

---

## 🛠️ 2. CHI TIẾT THAY ĐỔI CODE & BAKE SCENE

### File 1: `Assets/Core/Scripts/Runtime/Components/LighthouseTurnWheel.cs`
- Cập nhật hàm `Update()`:
  ```csharp
  // Thoát lái:
  if (Keyboard.current.fKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame)
  {
      localSteering = false;
      ReleaseControlRpc();
      return;
  }

  // Đọc lăn chuột:
  float scroll = Mouse.current != null ? Mouse.current.scroll.y.ReadValue() : 0f;
  float keyInput = (Keyboard.current.dKey.isPressed ? 1f : 0f) - (Keyboard.current.aKey.isPressed ? 1f : 0f);
  
  float turnInput = 0f;
  if (Mathf.Abs(scroll) > 0.05f) turnInput = Mathf.Sign(scroll) * 1.5f;
  else if (keyInput != 0f) turnInput = keyInput;

  if (turnInput != 0f) RotateRpc(turnInput, Mathf.Min(Time.deltaTime, 0.1f));
  ```
- Cập nhật text hiển thị:
  `"Lăn chuột [▲ ▼] hoặc phím A/D để xoay tia sáng · [F / Space] Rời vô-lăng"`

---

### File 2: `Assets/Core/Scripts/Runtime/Components/LuminaMillSensor.cs`
- Cập nhật hàm kích hoạt động:
  ```csharp
  public void NotifySensorActivated(bool active)
  {
      if (!IsServer) return;
      if (active)
      {
          hitDuration += Time.deltaTime;
          if (hitDuration >= requiredHitSeconds && !isPowered.Value)
          {
              isPowered.Value = true;
              west?.SetLit(true);
              east?.SetLit(true);
              PoweredClientRpc(true);
          }
      }
      else
      {
          hitDuration = 0f;
          // NẾU TIA SÁNG BỊ XOAY LỆCH RA NGOÀI VÀ THUYỀN CHƯA VƯỢT QUA CỐI XAY:
          if (isPowered.Value && (west != null && east != null))
          {
              var mill = UnityEngine.Object.FindAnyObjectByType<GiantWatermillObstacle>();
              if (mill == null || !mill.Passed)
              {
                  isPowered.Value = false;
                  west.SetLit(false);
                  east.SetLit(false);
                  PoweredClientRpc(false);
              }
          }
      }
  }
  ```

---

### File 3: `Assets/Core/Scripts/Runtime/Components/LighthouseLightBeam.cs`
- Độ rộng tia sáng:
  ```csharp
  line.startWidth = 0.85f;
  line.endWidth = 1.15f;
  line.startColor = new Color(1f, 0.88f, 0.35f, 1f);
  line.endColor = new Color(1f, 0.7f, 0.2f, 0.85f);
  ```
- Tạo một Flare Marker / Sphere sáng nhỏ tại `hit.point` để hiển thị chấm sáng va chạm rõ ràng.

---

### File 4: `Assets/Core/Scripts/Editor/LevelTwoSceneBuilder.cs`
- Đặt bệ đá vững chãi trên sàn đảo:
  - Tháp Tây: `consolePos = new Vector3(84.6f, 1.3f, 175.8f)`
  - Tháp Đông: `consolePos = new Vector3(115.4f, 1.3f, 189.2f)`
  - Kích thước bệ: `Box(side + "_BeaconPedestal", root, consolePos, new Vector3(1.5f, 0.8f, 1.2f), stoneMat);`
  - Ổ cắm `itemSocket` đặt trên mặt bệ đá ở $y = 1.7\text{m}$.
  - Vô-lăng `turnWheel` gắn trên mặt bệ đá ở $y = 1.6\text{m}$.
- Gắn `emitter` và `lanternHead` vào đúng **đèn trên đỉnh tháp**:
  - `westLampPos = new Vector3(82.99f, 7.56f, 174.99f)`
  - `eastLampPos = new Vector3(117.01f, 7.56f, 190.01f)`
- Thu nhỏ collider của `LuminaMillSensor` thành `SphereCollider` bán kính $r = 0.55\text{m}$.
- **Bắt buộc**: Chạy `EditorSceneManager.MarkSceneDirty` và `EditorSceneManager.SaveScene` để lưu đè `Assets/Core/Scenes/PlayScene2.unity`.

---

## 🎮 3. HƯỚNG DẪN CHƠI HOÀN CHỈNH (UPDATED PLAYER GUIDE)

1. **Nhặt và Cắm vật phẩm**:
   - Dùng phím **`[F]`** để nhặt **Tay Quay Khởi Động** (ở bờ đá Tây) và **Lăng Kính Cổ** (trong hốc cây Đông khi Ba nâng cây).
   - Đứng cạnh bệ đá dưới chân hải đăng $\rightarrow$ Bấm **`[F]`** để cắm vào bệ.
2. **Lái Vô-lăng & Căn tia sáng**:
   - Lại gần bánh lái trên bệ đá dưới chân tháp $\rightarrow$ Bấm **`[F]`** để cầm lái.
   - **LĂN CHUỘT LÊN / XUỐNG** : Chùm sáng rực rỡ từ **ĐỈNH THÁP** sẽ quét qua lại.
   - Bấm **`[F]`** hoặc **`[Space]`** để rời vô-lăng.
3. **Cơ chế Sương mù Động**:
   - Căn tia sáng từ đỉnh Tháp Đông chiếu trúng Mắt Ngọc Cối Xay $\rightarrow$ Mắt Ngọc sáng xanh, sương mù bắt đầu tan, cổng cối xay mở ra!
   - Nếu xoay tia sáng lệch ra ngoài trước khi thuyền qua cổng $\rightarrow$ Sương mù sẽ tràn lại và cổng cối xay đóng lại! Hãy phối hợp giữ tia sáng đúng vị trí để thuyền vượt cối xay an toàn.

---

## 🏁 ACCEPTANCE CRITERIA
1. [ ] Tia sáng dày $0.85\text{m} - 1.15\text{m}$, sáng rõ rực rỡ từ mọi góc nhìn với vệt sáng va chạm tại điểm tiếp xúc.
2. [ ] Sương mù tan khi tia sáng chiếu trúng mắt ngọc, và **quay trở lại ngay khi xoay tia sáng lệch ra ngoài**.
3. [ ] Bệ cắm và bánh lái đặt vững vàng trên bệ đá dưới chân tháp ($y \approx 1.3\text{m} - 1.7\text{m}$), nhân vật đứng trên sàn đảo tương tác dễ dàng, không còn chi tiết nào lơ lửng ngoài mép.
4. [ ] Tia sáng phóng ra từ đỉnh lồng kính của ngọn hải đăng ($y \approx 7.56\text{m} - 9.0\text{m}$).
5. [ ] Lăn chuột lên/xuống xoay tia sáng mượt mà.
6. [ ] Chạy `LevelTwoValidation.RunStageThree()` đạt kết quả PASS 100%.
