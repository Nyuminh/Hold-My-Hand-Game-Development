# 📋 TASK ĐẶC TẢ KỸ THUẬT & HƯỚNG DẪN CHƠI PUZZLE HẢI ĐĂNG (PLAYSCENE2)
> **Dành cho**: OpenAI Codex / Lập trình viên  
> **Dự án**: Hold My Hand · Nắm tay cha (Unity 6 / Netcode for GameObjects)  
> **Scene mục tiêu**: `Assets/Core/Scenes/PlayScene2.unity` (Stage 4: Misty Marsh & Twin Beacons, Z: 155 - 215)  
> **Trạng thái**: TÁI CẤU TRÚC & HOÀN THIỆN TOÀN DIỆN (RE-WRITE & COMPLETE POLISH)

---

## ⚠️ 1. NGUYÊN NHÂN NGƯỜI CHƠI THẤY CƠ CHẾ VẪN NHƯ CŨ (ROOT CAUSES)
Trong lần sửa trước, mặc dù các script C# mới đã được tạo (`CoopCarryableItem.cs`, `LighthouseLightBeam.cs`, `LighthouseTurnWheel.cs`), nhưng người chơi kiểm tra thực tế vẫn thấy cơ chế như cũ vì 3 lý do chí mạng:
1. **Scene `PlayScene2.unity` chưa được bake và lưu ra file trên ổ đĩa**: `LevelTwoSceneBuilder.WireStageThree()` chỉ nằm trong code Editor mà chưa được chạy để lưu đè vào file scene `PlayScene2.unity`.
2. **Các GameObject cũ vẫn hiển thị trong Scene**: Hai chiếc lá sen khổng lồ (`GiantLilyPad_1`, `GiantLilyPad_2`) và bệ ném cũ (`ThrowPad`) vẫn còn tồn tại, không bị ẩn hay dọn dẹp, khiến người chơi lầm tưởng cơ chế cũ vẫn đang chạy.
3. **Thiếu hoàn toàn hệ thống HUD Hướng Dẫn & Prompt Trực Quan**:
   - Khi người chơi đến ngọn hải đăng, không có Banner thông báo mục tiêu cần làm gì.
   - Khi đứng cạnh vật phẩm `IgnitionCrank`, không có chữ nổi nhắc `[E] Nhặt`.
   - Khi đứng cạnh thân cây đổ `FallenLog`, người Ba không thấy nhắc `[E] Nâng giữ thân cây`, Con không biết phải chui vào hốc.
   - Khi đứng cạnh vô-lăng, không biết bấm gì để lái tia sáng.

---

## 📖 2. HƯỚNG DẪN CHƠI TOÀN TẬP Ở NGỌN HẢI ĐĂNG (PLAYER GAMEPLAY GUIDE)
*(Phần này được tích hợp vào Game HUD và tài liệu hướng dẫn cho người chơi)*

### 🎯 Bối Cảnh & Mục Tiêu (Objective):
Hai cha con chèo thuyền đến đầm lầy sương mù mù mịt (Misty Marsh). Phía trước là cối xay nước khổng lồ (`GiantWatermillObstacle`) đang bị rào chắn an toàn khóa kín. Trên đỉnh cối xay là một **Mắt Ngọc Cảm Biến Quang Học (Lumina Sensor)** đang ngủ yên.  
**Mục tiêu**: Kích hoạt ngọn đèn Tháp Tây $\rightarrow$ Dẫn tia sáng qua Tháp Đông $\rightarrow$ Bẻ cong tia sáng bắn vào Mắt Ngọc Cối Xay để tan biến sương mù và mở toang cổng cối xay!

### 🎮 Các Bước Phối Hợp Cha - Con Chi Tiết:

#### 🔹 BƯỚC 1: THẮP SÁNG HẢI ĐĂNG TÂY (Nhặt & Gắn Tay Quay)
- **Địa điểm**: Đảo Hải Đăng Tây (Bên trái dòng sông).
- **Thực hiện**:
  - Tại bờ đá gần mép nước ($x=95, z=170$), có một chiếc **Tay Quay Khởi Động (`IgnitionCrank`)**.
  - **Đứa Con**: Di chuyển lại gần tay quay, bấm phím **[E]** để nhặt và ôm chiếc tay quay trước ngực.
  - Con có thể mang lại gần Ba bấm **[E]** để chuyền cho Ba, HOẶC trực tiếp mang lên bệ Tháp Tây.
  - Đứng sát Ổ Cắm Tháp Tây (`West_ItemSocket`), bấm phím **[E]** để gắn tay quay vào.
- **Hiệu ứng**:
  - Hải đăng Tây bùng sáng ngọn lửa vàng!
  - Một chùm sáng vàng rực rỡ (`Primary Light Beam`) phóng thẳng qua dòng sông hướng về phía Tháp Đông!

#### 🔹 BƯỚC 2: GIẢI CỨU LĂNG KÍNH ĐÔNG (Nâng Thân Cây & Chui Khe Hẹp)
- **Địa điểm**: Khu rừng cây rậm rạp cạnh Tháp Đông (Bên phải dòng sông, $x=115, z=192$).
- **Thực hiện**:
  - Một thân cây cổ thụ mục đổ chắn ngang lối vào hốc rương vàng (`FallenLog`).
  - **Người Ba**: Tiến lại gần điểm cầm tay màu gỗ (`DadGrip`), bấm phím **[E]** để gồng mình nâng thân cây lên cao.
  - **Đứa Con**: Trong lúc Ba đang nâng giữ thân cây, Con chạy lại chui qua khe hẹp (`CoopCrawlVolume`) bò vào bên trong hốc cây.
  - Con mở chiếc rương vàng cổ (`Pfb_AncientChest`), bấm phím **[E]** để nhặt **Khối Lăng Kính Khúc Xạ (`AncientPrism`)**.
  - Con ôm Lăng Kính bò ra ngoài, chạy lên bệ tròn Tháp Đông.
  - Đứng cạnh bệ xoay Tháp Đông (`East_ItemSocket`), bấm **[E]** để gắn Lăng Kính vào ổ.
- **Hiệu ứng**:
  - Lăng Kính sáng lập lòe, sẵn sàng đón nhận tia sáng và khúc xạ.

#### 🔹 BƯỚC 3: GIAO THOA ÁNH SÁNG (Căn Chỉnh Góc Tia Sáng)
- **Địa điểm**: Vô-lăng điều khiển ở 2 ngọn tháp.
- **Thực hiện**:
  - **Người Ba (ở Tháp Tây)**: Lại gần vô-lăng (`West_TurnWheel`), bấm **[E]** để vào chế độ lái đèn. Dùng phím **A / D** (hoặc Mũi tên Trái / Phải) xoay góc chiếu đèn sao cho tia sáng vàng bắn thẳng trúng vào Khối Lăng Kính trên Tháp Đông.
  - **Đứa Con (ở Tháp Đông)**: Khi tia sáng từ Tháp Tây chiếu trúng Lăng Kính, Lăng Kính sẽ phóng ra tia sáng thứ hai (`Secondary Refracted Beam`). Con lại gần vô-lăng Tháp Đông (`East_TurnWheel`), bấm **[E]** để điều khiển, dùng phím **A / D** xoay góc tia phản xạ chiếu thẳng vào **Mắt Ngọc Cảm Biến** trên Cối Xay Nước!
  - Bấm **[E]** hoặc **[Space]** để rời khỏi vô-lăng bất cứ lúc nào.
- **🎯 KẾT THÚC THẮNG LỢI (Victory)**:
  - Khi tia sáng chiếu trúng tâm Mắt Ngọc Cối Xay trong 0.5s:
  - Mắt Ngọc đổi từ Đỏ sang Xanh ngọc phát sáng chói lòa!
  - Tiếng chuông thánh thót ngân vang (`LVL2_Chime.wav`).
  - Sương mù đầm lầy tan biến hoàn toàn (`RenderSettings.fogDensity` giảm xuống mức trong vắt).
  - Rào chắn an toàn cối xay nước kéo lên cao vĩnh viễn!
  - Hai cha con cùng nhảy lên thuyền, chèo qua cối xay tiến vào Đền Haven Harbor!

---

## 🛠️ 3. NHIỆM VỤ KỸ THUẬT CODEX PHẢI THỰC HIỆN

### Task 1: Bổ Sung Hệ Thống HUD Banner & Floating Prompts
Cập nhật `MarshBeaconTrigger.cs`, `LighthouseFallenLog.cs`, `LighthouseTurnWheel.cs`, `CoopCarryableItem.cs`:
1. **Top Objective Banner (Luôn hiện khi ở Stage 4 - Z: 150 -> 215)**:
   - Nếu `!westCrankSocketed`:  
     `"MỤC TIÊU 1: Con nhặt Tay Quay [E] ở bờ đá đem cắm vào Tháp Tây để phát sáng."`
   - Nếu `westCrankSocketed && !eastPrismSocketed`:  
     `"MỤC TIÊU 2: Ba nhấn [E] nâng giữ thân cây, Con chui qua hốc nhặt Lăng Kính Cổ [E] cắm lên Tháp Đông."`
   - Nếu `eastPrismSocketed && !beaconsLit`:  
     `"MỤC TIÊU 3: Cả hai dùng vô-lăng [E] (phím A/D) căn tia sáng chiếu trúng Mắt Ngọc Cối Xay!"`
   - Nếu `beaconsLit`:  
     `"HOÀN THÀNH: Hai hải đăng đã sáng rực, sương mù tan biến! Cùng lên thuyền vượt cối xay."`
2. **Floating Proximity Hints**:
   - `CoopCarryableItem`: Hiện nhãn vàng `[E] Nhặt Tay Quay` / `[E] Nhặt Lăng Kính` khi đứng trong 2.5m. Khi đang cầm: `[E] Gắn vào Ổ cắm` (nếu gần ổ) hoặc `[G] Thả xuống`.
   - `LighthouseFallenLog`:
     - Nếu là Ba: Hiện `[E] Nâng thân cây cổ thụ` (hoặc `[E] Hạ thân cây` khi đang giữ).
     - Nếu là Con: Hiện `Ba đang nâng cây! Hãy chui vào trong` (nếu cây đang nâng).
   - `LighthouseTurnWheel`: Hiện `[E] Cầm lái vô-lăng` khi gần. Khi đang lái: `[A/D] Xoay hướng tia sáng · [E/Space] Thoát`.

### Task 2: Dọn Dẹp GameObject Cũ & Bake Scene `PlayScene2.unity`
Trong `LevelTwoSceneBuilder.cs` (hàm `WireStageThree`):
1. **Xóa / Ẩn triệt để vật thể cũ**:
   - Ẩn hoặc xóa `GiantLilyPad_1` và `GiantLilyPad_2`.
   - Ẩn hoặc xóa `LuminaCrystal_Gem` cũ và `ThrowPlatform`.
2. **Bake và Lưu Scene**:
   - Đảm bảo lệnh `EditorSceneManager.MarkSceneDirty` và `EditorSceneManager.SaveScene` được thực thi và file `Assets/Core/Scenes/PlayScene2.unity` được commit vào Git với đầy đủ các đối tượng mới (`IgnitionCrank`, `East_FallenLogPuzzle`, `LuminaMillSensor_Lens`, v.v.).

### Task 3: Chạy & Đảm Bảo Bộ Test PASS 100%
- Chạy script kiểm tra `LevelTwoValidation.cs`.
- Đảm bảo tất cả 6 step của Level 2 (từ chèo thuyền, slalom, nhặt crank, nâng log, nhặt prism, bắn tia sáng đến cối xay) đều xanh (PASS).

---

## 🏁 TIÊU CHÍ NGHIỆM THU (ACCEPTANCE CRITERIA)
1. [ ] Mở `PlayScene2.unity` trong Unity: thấy rõ mô hình `IgnitionCrank`, thân cây đổ `FallenLog`, rương vàng `AncientPrism`, vô-lăng và thấu kính trên 2 tháp. KHÔNG còn thấy lá sen ném gem cũ.
2. [ ] HUD hiển thị Banner mục tiêu to rõ, đổi chữ tự động qua từng giai đoạn.
3. [ ] Con nhặt được `IgnitionCrank` bằng [E], mang cắm vào Tháp Tây $\rightarrow$ Tia sáng vàng bắn qua sông.
4. [ ] Ba nâng giữ thân cây bằng [E] $\rightarrow$ Con chui vào nhặt `AncientPrism` cắm vào Tháp Đông.
5. [ ] Người chơi bấm [E] lái vô-lăng, phím A/D xoay tia sáng mượt mà.
6. [ ] Căn tia sáng trúng Cối Xay $\rightarrow$ Mắt Ngọc sáng xanh, rào chắn nâng lên, sương mù tan, thuyền vượt cối xay thành công.
