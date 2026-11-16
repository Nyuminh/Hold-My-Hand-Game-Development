# Hold My Hand — Unity Co-op Tuning

Cách làm việc khi chỉnh cơ chế co-op trong repo này. Mục tiêu: ít token, sửa chuẩn, không lòng vòng.

## Trước khi sửa
- Đọc Docs `TUTORIAL_NARRATIVE_LEVEL_DESIGN.md` để biết màn nào thuộc beat cảm xúc nào.
- Mỗi màn chỉ MỘT ý tưởng cơ chế. Thấy màn nào nhồi 2 ý tưởng thì tách hoặc cắt bớt.
- Dạy bằng môi trường (ánh sáng, màu sắc, animation), không thêm bảng chữ hướng dẫn dài.

## Quy tắc sửa
- Đọc file bằng `grep`/`sed` lấy excerpt, không đọc cả file YAML scene lớn.
- Sửa tối thiểu: giữ API probe hiện có (`ChapterProbe`, `IsCompleted`, `CurrentStage`) để `TutorialObjectiveDirector` không phải sửa theo.
- Xóa code chết và log verbose, giữ log theo format `[TênScript] message`.
- Cơ chế bất đối xứng Ba/Con: Ba mạnh nặng, Con nhẹ nhanh. Mọi câu đố phải cần cả hai.
- Xong mỗi sửa: nêu tên file + dòng đổi, không dump code.

## Kinh nghiệm PlayScene3 (2026-11-16)
- Tuyệt đối KHÔNG dùng `splice` số dòng trần trên file `.unity` sau khi đã sửa nội dung: số dòng lệch là cắt sai. Thay vào đó xóa theo fileID block (`--- !u!... &ID` đến block `---` kế tiếp) bằng script duyệt block.
- Enum `ChapterProbe` trong scene lưu bằng số. Khi xóa entry giữa enum, gán số cố định cho entry còn lại trước, rồi mới remap probe trong scene.
- Kiếm tra trùng lặp: cơ chế quay/tời chỉ giữ 1 kiểu, cơ chế dùng 1 lần thì cắt.
- Unity Editor phải mở project `game-unity/Hold-My-Hand-Game-Development` trước khi dùng Unity MCP.
- Sau khi sửa script, yêu cầu compile không lỗi trước khi đụng tới scene.
- Không commit khi chưa được yêu cầu rõ.
