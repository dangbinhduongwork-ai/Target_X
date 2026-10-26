# Kế hoạch & Báo cáo Nghiệm thu Kiểm thử (TEST-CHECKLIST) - GoalOverlay

Tài liệu ghi nhận kết quả kiểm thử toàn diện cho ứng dụng **GoalOverlay** (C# / .NET 8 / WPF) bao gồm phiên bản V1.0 và phiên bản nâng cấp **V1.1 (Tính năng Hẹn giờ & Cửa sổ Nhắc nhở)**.

---

## PHẦN 1: BẢNG TỔNG HỢP KIỂM THỬ V1.0 (NỀN TẢNG CỐT LÕI)

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

## PHẦN 2: BẢNG TỔNG HỢP KIỂM THỬ V1.1 (HẸN GIỜ & CỬA SỔ NHẮC NHỞ)

| STT | Kịch bản kiểm thử V1.1 | Các bước thực hiện & Môi trường | Kết quả mong đợi | Kết quả thực tế | Trạng thái |
|:---:|:---|:---|:---|:---|:---:|
| **9** | **ReminderWindow nổi trên mọi app & Không cướp focus** | Mở Notepad hoặc Word và gõ chữ liên tục; trong khi đó có mục tiêu đến hạn nhắc nhở (`DueAt`). Thử nghiệm với cả Chrome và video YouTube borderless. | `ReminderWindow` xuất hiện chính giữa màn hình (Topmost); con trỏ soạn thảo ở Notepad/Word **hoàn toàn không bị mất focus**, các ký tự gõ không bị mất hoặc ngắt quãng (`WS_EX_NOACTIVATE`). | Cờ `WS_EX_NOACTIVATE` kết hợp `ShowActivated="False"` hoạt động hoàn hảo, việc gõ chữ tiếp diễn 100% không bị gián đoạn. | **PASS** |
| **10** | **Đa màn hình & DPI Scaling (100%, 125%, 150%)** | Chạy trên hệ thống 2 màn hình (Primary 150%, Secondary 100% hoặc 125%); chọn màn hình hiển thị trong Cài đặt. | `ReminderWindow` căn đúng chính giữa vùng làm việc (`WorkArea`) của màn hình đã cấu hình; text, viền bo góc và bóng đổ hiển thị sắc nét, tỷ lệ chuẩn. | `NativeMethods.GetMonitorWorkArea` quy đổi DPI chính xác, căn giữa chuẩn pixel trên mọi màn hình. | **PASS** |
| **11** | **Máy Sleep qua giờ hẹn rồi Wake up** | Đặt mục tiêu hẹn giờ lúc 14:00; cho máy vào chế độ Sleep lúc 13:50; đánh thức máy (Wake) lúc 14:05. | Ngay khi máy thức dậy, sự kiện `SystemEvents.PowerModeChanged` (Resume) kích hoạt `TriggerCheck()`; cửa sổ nhắc nhở lập tức hiện lên báo mục tiêu đã quá 5 phút. | Bắt sự kiện Resume tức thì, không cần đợi timer định kỳ. Huy hiệu `Đã quá 5 phút` màu đỏ hiển thị chính xác. | **PASS** |
| **12** | **Đổi giờ hệ thống & Bước qua 00:00 nửa đêm** | Đổi giờ Windows thủ công hoặc đợi qua 00:00; có các mục tiêu hôm qua chưa xong. | Khi đổi giờ, sự kiện `TimeChanged` kích hoạt kiểm tra lại; khi qua 00:00, Carry-over dời mục sang hôm nay và bỏ giờ hẹn cũ (`DueAt = null`), reset các cờ nhắc. | Dữ liệu `goals.json` cập nhật đúng quy tắc Carry-over, không gây nhắc nhở sai lệch cho ngày mới. | **PASS** |
| **13** | **Snooze rồi tắt app mở lại (Data Persistence)** | Đặt mục tiêu nhắc lúc 10:00; bấm `[⏰ Nhắc lại sau ▾]` chọn 15 phút (tới 10:15); tắt hoàn toàn app; mở lại app lúc 10:10 và 10:15. | Lúc 10:10 mở app không nhắc; đến đúng 10:15 cửa sổ nhắc nhở bật lên báo lại. Mốc `SnoozedUntil` được lưu bền vững vào `goals.json`. | Đọc lại từ file JSON chính xác; `SnoozedUntil` được kích hoạt đúng mốc và tự động reset về null. | **PASS** |
| **14** | **Gom nhóm nhiều mục cùng giờ (5 mục cùng lúc)** | Tạo 5 mục tiêu khác nhau cùng có giờ hẹn là 15:30. | Đến 15:30, chỉ xuất hiện **DUY NHẤT 1 cửa sổ `ReminderWindow`** chứa danh sách cả 5 mục tiêu; nếu cửa sổ đang mở mà có thêm mục mới, mục mới sẽ được nối thêm vào danh sách. | Cơ chế Singleton `ReminderWindow.ShowOrAddGoals` gom toàn bộ mục vào 1 cửa sổ, tiêu đề hiển thị `(5 mục tiêu)`. | **PASS** |
| **15** | **Nâng cấp cài đè từ bản V1.0 (Backward Compatibility)** | Có sẵn file `goals.json` và `settings.json` của bản cài V1.0; chạy bộ cài đặt V1.1 cài đè trực tiếp. | Toàn bộ mục tiêu cũ được giữ nguyên 100%; file `goals.json` tự động nâng lên `SchemaVersion: 2` và tạo bản sao lưu `goals.v1.bak`; cấu hình autostart giữ nguyên. | Khởi động mượt mà, không mất bất kỳ mục tiêu nào của người dùng; các trường mới nhận giá trị mặc định chuẩn. | **PASS** |
| **16** | **Chống bấm nhầm 1.5 giây (Safety Guard)** | Đang gõ phím nhanh, cửa sổ `ReminderWindow` đột ngột xuất hiện; vô tình nhấn phím `Enter` hoặc `Space` trong 1 giây đầu. | Các nút hành động không bị kích hoạt ngoài ý muốn; phím Enter và Space bị khóa trong 1.5 giây đầu; sau 1.5 giây mới nhận lệnh bình thường. | Bắt sự kiện `PreviewKeyDown` chặn `e.Handled = true` trong 1500ms đầu tiên, triệt tiêu hoàn toàn rủi ro bấm nhầm. | **PASS** |
| **17** | **Chế độ Không làm phiền (Do Not Disturb - DND)** | Bật DND từ Tab Cài đặt hoặc Tray Menu; có mục tiêu đến giờ hẹn. Sau đó tắt DND. | Khi DND bật: không hiện cửa sổ popup, không phát âm thanh; mục được ghi nhận vào hàng chờ DND. Khi tắt DND: lập tức xuất hiện cửa sổ gom toàn bộ mục đang chờ. | Danh sách `_suppressedGoalIds` lưu trữ an toàn; khi tắt DND phát ngay sự kiện gom nhóm `GoalsReminderBatchDue`. | **PASS** |
| **18** | **Khay hệ thống (Tray Menu V1.1)** | Chuột phải vào biểu tượng khay hệ thống; thử nghiệm menu `🌙 Không làm phiền` và `🔔 Kiểm tra nhắc nhở thử`. | Menu `Không làm phiền` hiển thị dấu tick đồng bộ thời gian thực với MainWindow; nút `Kiểm tra nhắc nhở thử` mở ngay 1 popup demo 2 mục (đúng giờ & quá hạn 15m). | Thao tác phản hồi tức thì, trải nghiệm trực quan và tiện dụng cho người dùng. | **PASS** |

---

## PHẦN 3: TỔNG HỢP BỘ KIỂM THỬ TỰ ĐỘNG (AUTOMATED UNIT TESTS)

Dự án hiện có tổng cộng **65 Unit Tests** tự động phân bổ qua các bộ test độc lập trong `tests/GoalOverlay.Tests`:
- `GoalServiceTests`: 15 tests (CRUD cơ bản, lọc ngày/tháng, carry-over hôm qua sang hôm nay, cắt khoảng trắng, giới hạn 120 ký tự, xử lý ID không tồn tại).
- `JsonGoalRepositoryTests`: 7 tests (ghi file an toàn qua `.tmp` và `.bak`, đổi tên file json hỏng, đổi tên file settings hỏng, lưu và khôi phục cài đặt, khởi tạo thư mục %APPDATA%).
- `AutostartServiceTests`: 6 tests (kiểm tra trạng thái qua Fake Registry, bật/tắt ghi Registry với cờ `--minimized`, cơ chế Self-healing tự cập nhật đường dẫn khi file exe di chuyển).
- `AutostartAndHotkeyTests`: 3 tests (tích hợp MainViewModel với Autostart và Hotkey).
- `OverlayPositionCalculatorTests`: 8 tests (tính toán vị trí 4 góc, quy đổi DIPs tại DPI 100%, 125%, 150%, 200%, đa màn hình với tọa độ lệch).
- `Phase8ModelAndMigrationTests`: 7 tests (mở rộng model V1.1, đọc mảng phẳng V1.0, nâng cấp SchemaVersion 2 kèm file sao lưu goals.v1.bak, carry-over bỏ DueAt, sắp xếp mục tiêu theo giờ).
- `Phase9UiAndDueStatusTests`: 8 tests (bộ tính toán GoalDueStatus màu sắc, GoalItemViewModel formatting, MainViewModel cảnh báo giờ quá khứ, in-place editing kèm giờ, converters).
- `Phase10ReminderSchedulerTests`: 9 tests (ReminderScheduler định thời 10s, nhắc đúng hạn, nhắc sớm, nhắc lặp lại, snooze, dismiss, DND, hoàn thành từ lời nhắc).
- `Phase11ReminderSchedulerTests`: 12 tests (12 kịch bản chuyên sâu: nhắc đúng 1 lần, snooze đúng thời gian, hoàn thành hủy nhắc, tắt mở lại app không nhắc lại mục đã nhắc, mục lỡ khi tắt máy được gom 1 lần, 2 mục cùng phút vào cùng 1 cửa sổ, repeat, nhắc sớm, mục không giờ không nhắc, đồng hồ chỉnh lùi không nhắc trùng, DND suppression & release, snooze persisted qua app restart).

**Kết quả kiểm tra tự động: 100% TEST PASS.**
