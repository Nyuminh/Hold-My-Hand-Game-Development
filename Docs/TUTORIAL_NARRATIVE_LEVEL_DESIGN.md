# 🤝 HOLD MY HAND — TUTORIAL NARRATIVE & LEVEL DESIGN SPEC
> **Triết lý Thiết kế**: Kế thừa phong cách *It Takes Two* & *Brothers: A Tale of Two Sons* — **"Cơ chế là cốt truyện, môi trường là người hướng dẫn, không dùng chữ để cầm tay chỉ việc"**.

---

## 1. CỐT TRUYỆN MỞ ĐẦU: "BƯỚC CHÂN ĐẦU TIÊN" (The First Step)

### 1.1. Bối cảnh (The Narrative Hook)
* Sau cơn giông bão trên biển, hai cha con bị trôi dạt vào phế tích rừng cổ (*The Sunken Archipelago & Castle Fortress*).
* Đứa con đánh rơi **Chiếc Hộp Nhạc Kỷ Vật của Mẹ** xuống bờ bên kia của vách đá.
* Khi nhìn thấy những cây cầu gãy mục, hố sâu sương mù và những cỗ máy đá cổ khổng lồ, đứa con sợ hãi co cụm lại, không dám bước tiếp.
* Người cha to lớn không quát mắng hay thúc ép. Người cha quỳ một gối xuống ngang tầm mắt con, mỉm cười và chìa bàn tay gân guốc ấm áp ra:
  > *"Đừng sợ, nắm lấy tay Ba nào. Chỉ cần hai cha con mình không buông tay, không có thử thách nào chia cắt được chúng ta."*

### 1.2. Biểu tượng cốt lõi (Core Metaphor)
Mỗi màn giải đố trong Tutorial là **một bước thăng hoa trong tình phụ tử**:
1. **Sự An Tâm (Comfort)**: Con sợ bóng tối & vực thẳm ➔ Nắm tay Ba là con vững tin bước qua.
2. **Nâng Đỡ (Elevation)**: Bờ tường quá cao con không leo tới ➔ Ba cúi mình làm bệ phóng đưa con lên cao; Con thả dây kéo Ba lên.
3. **Hy Sinh & Đối Trọng (Sacrifice & Trust)**: Vực thẳm chia cắt ➔ Ba tình nguyện ở lại đáy vực tối để con lên chỗ sáng trước; Con dũng cảm tìm cơ chế cứu Ba.
4. **Học Cách Buông Tay (Letting Go)**: Cửa hầm hẹp Ba không chui lọt ➔ Lần đầu tiên Ba phải buông tay để con tự bước vào bóng tối, tin tưởng sự dũng cảm của con.
5. **Cú Nhảy Niềm Tin (The Leap of Faith)**: Cầu sụp, bão tới ➔ Cùng nhảy vào khoảng không đu cáp Zipline; khoảnh khắc Ba nắm chặt tay con suýt rơi giữa trời.

---

## 2. NGUYÊN TẮC THIẾT KẾ KIỂU "IT TAKES TWO"

### 2.1. Visual Affordance — Môi trường tự nói chuyện (Không dùng bảng chữ)
* **Màu vàng hổ phách ấm áp (Warm Amber Glow)**:
  - Ánh sáng đèn lồng, vệt phấn màu, đàn đom đóm ➔ Chỉ lối cho **Con** (khe hẹp, bậc leo trèo nhẹ, chỗ chui).
* **Màu sắt rỉ & Dầm gỗ nặng (Heavy Iron & Oak)**:
  - Tay đòn khổng lồ, xích sắt, tảng đá đối trọng ➔ Chỉ dành riêng cho sức mạnh cơ bắp của **Ba**.
* **Ngôn ngữ Hình thể (Body Language Animations)**:
  - Khi Con đứng sát mép hố sâu một mình: Animation con co người, hai tay ôm ngực, đầu gối run run lùi lại.
  - Khi Ba đến gần bấm [H] nắm tay: Con thở phào, bàn tay siết chặt tay Ba, dáng đi tự tin chạy theo nhịp sải chân của người cha.
  - Khi Ba đến gần khe đá hẹp: Ba gập người thử chui nhưng bị kẹt vai, lắc đầu nhìn sang Con.

### 2.2. Cơ chế Bất đối xứng (Asymmetric Co-op)
* **Ba (Khối lượng 85kg)**: Sức kéo cực khỏe, chịu va đập, ném bổng con lên cao, làm đối trọng kéo cẩu trục, quay tời nặng. Nhược điểm: Nặng nề, không thể nhảy xa hoặc chui khe nhỏ.
* **Con (Khối lượng 28kg)**: Nhẹ như lông vũ, leo trèo gờ đá mỏng, chui lọt khe ngầm, mở chốt từ bên trong. Nhược điểm: Yếu ớt, sợ bóng tối, không tự vượt qua hố gió bão nếu không nắm tay Ba.

---

## 3. LỘ TRÌNH 5 MÀN GIẢI ĐỐ (5 EMOTIONAL BEATS)

### MÀN 1: "ĐỪNG SỢ, NẮM LẤY TAY BA"
* **Địa điểm**: Cây cầu gỗ mục gãy đôi qua suối sương mù (Zone 1 Entrance).
* **Tình huống**: Con đứng sững lại trước vũng nước xoáy và thân gỗ đung đưa, không dám bước.
* **Cơ chế**:
  - Ba tiến lại gần, giữ phím **[H]** trong 0.5s (`CoopHoldHandAbility`).
  - Hai bàn tay kết nối trực tiếp. Hiệu ứng hạt ánh sáng ấm áp (Warm Glow Particle) tỏa ra quanh hai bàn tay.
  - Đứa con thoát trạng thái hoảng sợ. Ba sải bước kéo con băng qua thân cây gỗ rung lắc mà không bị trượt ngã.
* **Controller**: `CoopHoldHandAbility.cs`, `WalkAbility.cs`.

---

### MÀN 2: "ĐÔI CÁNH CỦA CHA"
* **Địa điểm**: Bờ tường thành sạt lở cao 3.2m (Castle Broken Rampart).
* **Tình huống**: Con đường phía trước bị cắt đứt bởi vách đá dựng đứng. Không có bậc thang nào.
* **Cơ chế**:
  - Ba đứng sát chân tường, nhấn giữ **[F]** (`CoopBoostThrowAbility`). Ba ngồi thụp xuống khum hai bàn tay, hiện đường parabol cong nhẹ hướng lên gờ tường.
  - Con chạy tới nhảy lên bàn tay Ba. Ba gồng sức bật đứng dậy tung Con bổng lên đỉnh tường thành!
  - Con tiếp đất an toàn trên bờ tường, chạy lại đạp cuộn thang dây (Rope Ladder) rơi xuống đất để Ba trèo lên cùng.
* **Controller**: `CoopBoostThrowAbility.cs`, `CoopBoostPad.cs`.

---

### MÀN 3: "BẬP BÊNH VỰC THẨM"
* **Địa điểm**: Hẻm núi sâu phân cách Zone 2 & Zone 3 (The Chasm Crane).
* **Tình huống**: Cẩu nâng cổ (`CraneElevatorController`) nằm dưới đáy vực, chỉ đủ chỗ nâng 1 người lên trước.
* **Cơ chế**:
  - Ba (85kg) bước lên bàn đạp đối trọng, ép đòn bẩy xuống dưới đáy vực, đẩy sàn nâng đưa Con (28kg) vọt lên đỉnh vách đá đối diện.
  - Ba chấp nhận đứng lại dưới đáy tối.
  - Con dũng cảm chạy tới tháo chốt tảng đá lăn 100kg xuống đối trọng, kéo chiếc sàn nâng đưa Ba từ đáy vực lên đoàn tụ với mình.
* **Controller**: `CraneElevatorController.cs`.

---

### MÀN 4: "ĐÔI MẮT CON TRONG BÓNG TỐI"
* **Địa điểm**: Cổng vòm sắt cổ & Khe đá ngầm (`GiantGearTurntable`).
* **Tình huống**: Cánh cổng đá nặng hàng tấn bị khóa chốt bên trong hang tối. Người cha to lớn không thể chui lọt.
* **Cơ chế**:
  - Ba cúi người mở chiếc đèn lồng nhỏ trao cho Con. Con bò trườn qua khe hẹp (`logCrawlTrigger`) vào sâu trong lòng đất.
  - Con lần theo âm thanh trục xích trong bóng tối, gạt then khóa sắt (`lockPin`).
  - Bên ngoài nghe tiếng then mở, Ba dồn hết sức quay cối xay bánh răng đá (`GiantGearTurntablePivot`), nâng cánh cổng vòm lên và dang rộng hai tay đón con ùa ra.
* **Controller**: `GiantGearTurntableController.cs`, `CoveDrawbridgeCoopController.cs`.

---

### MÀN 5: "CÚ NHẢY NIỀM TIN & HẢI ĐĂNG TÀU ĐẮM"
* **Địa điểm**: Bến tàu đắm bão biển (`Whispering Cove & Sunken Galleon`).
* **Tình huống**: Cây cầu gỗ dẫn ra biển bắt đầu sụp gãy từng đoạn. Dây cáp Zipline là lối thoát duy nhất.
* **Cơ chế**:
  - Ba dùng sức quay tời khóa căng sợi cáp Zipline (`Stage3ZiplineCoopController`).
  - Cầu rung chuyển dữ dội. Hai cha con cùng giữ chặt tay nhau [H], chạy nước rút qua các nhịp cầu gãy và cùng nhảy bám vào ròng rọc trượt vút qua biển sóng gầm!
  - **Khoảnh khắc cao trào**: Khi tiếp đất trượt chân, Con suýt ngã xuống vực biển. Ba kịp thò cánh tay túm chặt lấy tay con giữa không trung và kéo mạnh vào lòng. Ánh bình minh rọi sáng chiếc rương kỷ vật phía trước.
* **Controller**: `Stage3ZiplineCoopController.cs`, `ShipwreckCoopController.cs`.

---

## 4. CHECKLIST THỰC HIỆN TỪNG BƯỚC

- [x] **Bước 1: Màn 1 — Cơ chế Nắm Tay Cảm Xúc**
  - Kích hoạt trạng thái sợ hãi / chần chừ của Con khi đứng trước suối vực một mình.
  - Thêm hiệu ứng hạt ánh sáng ấm (Golden Warm Particle Glow) khi kết nối tay thành công [H].
  - Cho Ba sải bước vững chãi kéo Con băng qua chướng ngại vật mượt mà.
- [x] **Bước 2: Màn 2 — Cơ chế Kiệu Vai & Tung Bổng [F]**
  - Đặt bờ tường thành cổ 3.2m `BrokenRampart_Wall_Stage2` tại z = -8m.
  - Tích hợp cuộn thang dây bung rủ `CoopRopeLadderDrop.cs` khi Con lên bờ tường đạp chốt.
  - Tích hợp hỗ trợ thị giác & quỹ đạo ném `CoopBoostWallAffordance.cs` khi Ba đứng trước bờ tường.
- [x] **Bước 3: Màn 3 — Cẩu Trục Đối Trọng Tình Cha**
  - Tinh chỉnh `CraneElevatorController` hiển thị sự hy sinh của Ba ở lại dưới đáy đưa Con lên trước.
- [x] **Bước 4: Màn 4 — Khe Ngầm Đèn Lồng & Bánh Răng Khổng Lồ**
  - Thiết lập góc nhìn camera cận cảnh khi Ba cúi xuống trao niềm tin cho Con chui vào hốc đá.
- [x] **Bước 5: Màn 5 — Zipline Biển Bão & Cú Nắm Tay Giữa Không Trung**
  - Đồng bộ chuyển động trượt cáp đôi và khoảnh khắc cứu nguy đặc trưng "Hold My Hand".

---

## 5. HỆ THỐNG CƠ CHẾ ĐỔI MỚI (DIVERSIFIED GAMEPLAY MECHANICS)
Nhằm xóa bỏ sự lặp lại đơn điệu và đẩy cao tính vui nhộn, giải trí đúng chất *It Takes Two*, 3 cơ cấu hành động vật lý mới đã được tích hợp song song:

### 5.1. Cơ cấu 1: "Đại Bác Bập Bênh" (`CoopSeesawCatapult.cs`)
* **Bản chất**: Trò chơi bập bênh công viên phiên bản siêu anh hùng.
* **Tương tác**:
  - Con đứng chờ ở đầu thấp (`childSeatPoint`).
  - Ba nhảy dộng từ mỏm đá cao 3m xuống đầu cao (`dadSlamPoint`).
  - Lực giáng 85kg hất tung Con vút lên không trung theo quỹ đạo parabol cao 5.5m (`flightDuration = 1.35s`), xoay nhào lộn đáp thẳng lên gờ vách đá cao (`launchTargetLanding`).
* **Probe**: `ChapterProbe.SeesawCatapultCompleted`.

### 5.2. Cơ cấu 2: "Dây Buộc Sinh Mệnh & Đu Đáy Vực" (`CoopTetherAbyssBelay.cs`)
* **Bản chất**: Ba làm mỏ neo sống, Con đu dây con lắc làm thợ lặn đáy vực.
* **Tương tác**:
  - Ba cắm chân vững chắc tại gờ đá (`dadAnchorPoint`), giữ dây thừng `LineRenderer`.
  - Con tụt xuống đáy vực: phím **[W]/[S]** thu/thả độ dài dây (2m - 8.5m), phím **[A]/[D]** lấy đà đung đưa người như con lắc vật lý (`swingForce = 4.5`).
  - Khi đung đưa tới mỏm then chốt cổ (`abyssMechanismLatch`), Con nhấn **[F]** giật chốt, mở cổng đá.
  - Ba bên trên gồng tay kéo dây đưa Con vút ngược lên mặt đất an toàn.
* **Probe**: `ChapterProbe.TetherAbyssCompleted`.

### 5.3. Cơ cấu 3: "Xe Trượt Khiên Vượt Dốc Biển Bão" (`CoopShieldSledRide.cs`)
* **Bản chất**: Mini-game trượt dốc tốc độ cao (Co-op Downhill Rail), đổi nhịp từ giải đố tĩnh sang hành động kích thích.
* **Tương tác**:
  - Ba rút khiên sắt lớn làm xe trượt xuống dốc thành cổ ra vịnh biển.
  - Ba ngồi sau: bấm **[A]/[D]** bẻ lái lạng lách né đá tảng rải rác trên dốc (`slideSpeed = 12m/s`).
  - Con ngồi trước: bấm **[C]** cúi rạp người né cành cây thấp, bấm **[F]** gạt barrier mở đường ray.
  - Đoạn kết: Xe trượt lao qua bờ gỗ gãy, phóng qua không trung đáp bồng bềnh xuống bãi cát Tàu Đắm!
* **Probe**: `ChapterProbe.ShieldSledCompleted`.

---

## 6. CƠ CHẾ KẾT MÀN & CHUYỂN TIẾP SANG MÀN TIẾP THEO (STAGE ADVANCE)

### 6.1. Vấn đề giải quyết
* Trước đây người chơi sau khi vượt qua thuyền đắm và mở rương kho báu (`ShipwreckCoopController`) thì hành trình bị ngắt quãng, không có lối thoát khỏi bến tàu và không có cơ chế chuyển màn sang thế giới chính (`PlayScene1`).

### 6.2. Cơ cấu hoàn chỉnh: "Cổng Dịch Chuyển Hải Đăng Cổ Đại" (`CoopLevelExitPortal.cs`)
* **Vị trí**: Bến gỗ Whispering Cove `(28.2f, 5.95f, -73.4f)`, ngay sau cầu tàu thoát hiểm của Thuyền Đắm.
* **Kiến trúc**:
  - Đài đá cổ tròn (`Portal_Base_Dais`) với 2 cột trụ đá và lăng kính hải đăng chiếu chùm sáng thiên thanh xuyên thẳng lên tầng mây (`Portal_Sky_Beam`).
  - Hai phiến đá rune áp lực: Bệ Trái vàng kim (dành cho Ba), Bệ Phải xanh ngọc (dành cho Con).
  - Vòng xoáy năng lượng ma thuật ở trung tâm (`Sanctuary_Vortex_Pad`).
* **Quy trình tương tác chuẩn Co-op kiểu "It Takes Two"**:
  1. **Bước 1 — Mở Rương & Xuất Bến**: Mở rương thuyền trưởng lấy Chìa Khóa Vàng ➔ Cầu tàu hạ xuống nối ra bến gỗ.
  2. **Bước 2 — Hai Cha Con Vào Vị Trí**: Ba đứng bệ trái, Con đứng bệ phải ➔ Hai cột rune bừng sáng.
  3. **Bước 3 — Cú Nắm Tay Chiến Thắng [H] (Core "Hold My Hand" Mechanic)**:
     - Dòng chữ hiện ra: *"🤝 HAI CHA CON ĐANG TRÊN BỆ! HÃY NẮM CHẶT TAY NHAU [H] ĐỂ BƯỚC VÀO CỔNG!"*
     - Khi hai cha con bấm giữ **[H]** nắm tay nhau: Cổng ma thuật bùng nổ hào quang, âm thanh khải hoàn vang dội!
  4. **Bước 4 — Bảng Vinh Danh & Chuyển Màn Tự Động**:
     - Hiển thị Bảng Vinh Danh Chiến Thắng Hoàn Thành Màn Tutorial.
     - Đồng hồ đếm ngược 5 giây (hoặc bấm [Space] để vào ngay).
     - Tự động gọi chuyển cảnh mượt mà sang **`PlayScene1`** (bằng `NetworkManager.Singleton.SceneManager.LoadScene("PlayScene1")` nếu chơi mạng Co-op, hoặc `SceneManager.LoadScene("PlayScene1")` nếu chơi solo/local)!
* **Probe Director**: `ChapterProbe.NextLevelPortalActivated` (Chương 8).
