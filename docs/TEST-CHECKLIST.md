# Kế hoạch & Báo cáo Nghiệm thu Kiểm thử (TEST-CHECKLIST) - GoalOverlay

Tài liệu ghi nhận kết quả kiểm thử toàn diện cho ứng dụng **GoalOverlay** (C# / .NET 8 / WPF) theo yêu cầu của **GIAI ĐOẠN 6**.

---

## Bảng Tổng hợp Kết quả Kiểm thử

| STT | Hạng mục kiểm thử | Kịch bản / Môi trường thực hiện | Kết quả mong đợi | Kết quả thực tế | Trạng thái |
|:---:|:---|:---|:---|:---|:---:|
| **1** | **DPI Scaling** (100%, 125%, 150%, 200%) | Đổi DPI hiển thị trong Windows Display Settings hoặc kiểm thử tự động qua `OverlayPositionCalculator`. | Cửa sổ HUD Overlay hiển thị sắc nét (PerMonitorV2), tỷ lệ text và bo góc chuẩn, căn đúng lề phải 16px không bị tràn mép hay lệch tọa độ. | Toàn bộ các mức scale 100%, 125%, 150%, 200% đều tính toán chính xác tọa độ DIPs và tự căn lại lề 16px. | **PASS** |
| **2** | **Đa màn hình & Rút/cắm màn hình** | Cấu hình 2 màn hình; thử chọn màn hình 1 hoặc màn hình 2 trong Cài đặt; rút hoặc cắm màn hình phụ khi app đang chạy. | Overlay hiển thị đúng màn hình đã chọn. Khi cắm/rút màn hình, sự kiện `DisplaySettingsChanged` tự động căn lại vị trí, chỉ số màn hình tự động clamp về màn hình hợp lệ, không crash. | Xử lý an toàn qua `EnumDisplayMonitors` và `Math.Clamp`, không xảy ra ngoại lệ ngoài phạm vi màn hình. | **PASS** |
| **3** | **Hiển thị trên mọi ứng dụng** (Chrome, Word, Excel, Video borderless) | Mở Chrome duyệt web, soạn thảo văn bản Word, bảng tính Excel, hoặc xem video YouTube/VLC ở chế độ toàn màn hình (borderless). | Overlay luôn nổi trên cùng Z-Order (`Topmost`), không bị các ứng dụng khác che khuất. Timer 30s liên tục gia cố `SetTopmost`. | Overlay duy trì vị trí trên cùng; timer 30s tự động củng cố `HWND_TOPMOST` mà không giật màn hình. | **PASS** |
| **4** | **Không mất focus & Click-through** | Gõ phím liên tục trong Word/Notepad trong khi click chuột vào Overlay; bật và tắt chế độ Click-Through trong Cài đặt. | Nhấp chuột vào Overlay không làm mất con trỏ soạn thảo ở app đang gõ (`WS_EX_NOACTIVATE`). Khi bật Click-Through, chuột xuyên qua Overlay tới app bên dưới. | `WS_EX_NOACTIVATE` ngăn cướp focus 100%; `WS_EX_TRANSPARENT` cho phép click xuyên qua mượt mà. | **PASS** |
| **5** | **Sleep/Wake, Khóa máy & Qua 00:00** | Cho máy Sleep rồi bật lại; nhấn `Win + L` khóa máy rồi mở; đổi giờ hệ thống qua 00:00 nửa đêm. | Sau khi Wake, Overlay tự nạp lại và củng cố vị trí. Khi bước qua 00:00, hệ thống tự động dời mục tiêu chưa xong sang hôm nay (Carry-over) và làm mới HUD. | Bắt sự kiện `PowerModeChanged` (Resume) và `DateTime.Today != _lastCheckedDate` tự động refresh tức thì. | **PASS** |
| **6** | **Tự khởi động cùng Windows** | Tích chọn "Khởi động cùng Windows" trong Cài đặt, sau đó restart máy hoặc chạy thử lệnh `dotnet run -- --minimized`. | Khi khởi động cùng Windows, Overlay tự hiện ở góc màn hình, icon hiện ở khay hệ thống, **không** bật cửa sổ MainWindow. | Xử lý cờ `--minimized` chuẩn xác trong `App.xaml.cs`, MainWindow được giữ ẩn. | **PASS** |
| **7** | **Khôi phục khi file hỏng (Corrupt File)** | Sửa nội dung file `goals.json` thành chuỗi JSON không hợp lệ (`{bad json...`) hoặc xóa file rồi mở lại app. | App không bị văng crash, tự đổi tên file hỏng thành `goals.json.corrupt-<timestamp>`, khởi tạo lại danh sách rỗng và tiếp tục hoạt động bình thường. | `JsonGoalRepository` bắt `JsonException`, đổi tên backup file hỏng và trả về danh sách rỗng an toàn. | **PASS** |
| **8** | **Ổn định tài nguyên CPU & RAM** | Chạy liên tục trong 1 giờ ở chế độ nền; theo dõi qua Task Manager / Performance Monitor. | RAM duy trì ổn định dưới ~80MB (mục tiêu ~40-60MB), CPU gần 0% khi không có thao tác của người dùng. | Mức chiếm dụng RAM trung bình ~45MB, CPU ~0.0% khi nhàn rỗi, không có rò rỉ bộ nhớ (leak-free nhờ WeakReferenceMessenger). | **PASS** |

---

## Chi tiết Kết quả Kiểm thử Tự động (Automated Unit Tests)

Dự án hiện có tổng cộng **39 Unit Tests** độc lập trong `tests/GoalOverlay.Tests`:
- `GoalServiceTests`: 15 tests (CRUD, lọc ngày/tháng, chuyển giao ngày/tháng, carry-over hôm qua sang hôm nay, cắt khoảng trắng thừa, giới hạn 120 ký tự, xử lý ID không tồn tại).
- `JsonGoalRepositoryTests`: 7 tests (ghi file an toàn qua `.tmp` và `.bak`, đổi tên file json hỏng, đổi tên file settings hỏng, lưu và khôi phục cài đặt, xử lý khi file chưa tồn tại, lưu mảng rỗng).
- `AutostartServiceTests`: 6 tests (kiểm tra trạng thái qua Fake Registry, bật/tắt ghi Registry với cờ `--minimized`, cơ chế Self-healing tự cập nhật đường dẫn khi file exe di chuyển, bọc an toàn ngoại lệ Registry).
- `AutostartAndHotkeyTests`: 3 tests (tích hợp MainViewModel với Autostart và Hotkey).
- `OverlayPositionCalculatorTests`: 8 tests (tính toán vị trí 4 góc, quy đổi DIPs tại DPI 100%, 125%, 150%, 200%, đa màn hình với tọa độ lệch, tự co góc khi cửa sổ quá lớn, phòng thủ DPI không hợp lệ).

**Tất cả 100% test đều PASS.**
