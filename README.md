# GoalOverlay 🎯

[![.NET 8.0](https://img.shields.io/badge/.NET-8.0--windows-blue?logo=dotnet)](https://dotnet.microsoft.com/)
[![Version](https://img.shields.io/badge/Version-1.1.0-orange?logo=windows)](https://github.com/dangbinhduongwork-ai/Target_X/releases)
[![WPF](https://img.shields.io/badge/UI-WPF%20XAML-purple)](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/)
[![Architecture](https://img.shields.io/badge/Architecture-MVVM%20Toolkit-success)](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/)
[![Tests](https://img.shields.io/badge/Tests-65%20Passed%20(100%25)-brightgreen)](https://github.com/dangbinhduongwork-ai/Target_X)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%2F11%20x64-informational)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

**GoalOverlay** là ứng dụng Windows Desktop hiện đại, tối giản và hiệu suất cao, giúp hiển thị các mục tiêu hàng ngày (*Daily Goals*) và hàng tháng (*Monthly Goals*) dưới dạng HUD Overlay bán trong suốt ở góc trên bên phải màn hình. Ứng dụng luôn hiển thị trên mọi phần mềm khác (`Topmost`), không cướp focus bàn phím, tự khởi động ngầm cùng Windows, tích hợp hệ thống hẹn giờ thông minh và cửa sổ nhắc nhở tự động.

---

## ✨ Tính năng Nổi bật

### 1. Nền tảng HUD Overlay Cốt lõi (V1.0)
- 📌 **HUD Overlay ghim màn hình**: Nổi trên mọi ứng dụng (Chrome, Word, Excel, Video borderless), tự căn chỉnh theo DPI (PerMonitorV2) và cách mép màn hình 16px.
- ⚡ **Không cướp Focus (`WS_EX_NOACTIVATE`)**: Thao tác nhấp chuột trên Overlay không bao giờ làm mất con trỏ soạn thảo hoặc gián đoạn công việc ở các phần mềm khác.
- 🖱️ **Chế độ Click-Through (`WS_EX_TRANSPARENT`)**: Cho phép click chuột xuyên thấu qua Overlay tới ứng dụng nằm bên dưới.
- 🎨 **Live Preview thời gian thực**: Tùy chỉnh độ trong suốt (20% - 100%), cỡ chữ (11px - 20px), màu chữ (Trắng, Cyan, Vàng, Xanh lá, Hồng) và phản chiếu ngay lập tức lên Overlay.
- 🔄 **Tự chuyển tiếp mục tiêu chưa xong (Carry-over)**: Tự động dời các mục tiêu chưa hoàn thành của hôm qua sang hôm nay khi bước qua 00:00 nửa đêm.
- ⌨️ **Phím tắt toàn cục (Global Hotkey)**:
  - **`Ctrl + Alt + G`**: Ẩn hoặc hiện nhanh Overlay từ bất kỳ ứng dụng nào.
  - **`Ctrl + Alt + M`**: Mở ngay cửa sổ Quản lý mục tiêu.
- 🔔 **Khay hệ thống (System Tray)**: Tích hợp menu chuột phải đầy đủ (*Mở quản lý, Ẩn/Hiện overlay, Không làm phiền, Kiểm tra nhắc nhở thử, Cài đặt, Thoát*).
- 🛡️ **SingleInstanceGuard**: Sử dụng `Mutex` và Windows Message / EventWaitHandle ngăn chạy trùng lặp; khởi động lần 2 sẽ tự động kích hoạt cửa sổ quản lý hiện tại.
- 🚀 **Tự khởi động cùng Windows**: Tự đăng ký Registry Run với cờ `--minimized` kèm cơ chế tự sửa đường dẫn (*Self-healing*).
- 💾 **Lưu trữ an toàn tuyệt đối**: Ghi file an toàn 2 bước (`.tmp` -> `File.Replace` -> `.bak`). Tự động cô lập file hỏng (`.corrupt-<timestamp>`) và không bao giờ crash.

### 2. Hệ thống Hẹn giờ & Cửa sổ Nhắc nhở Thông minh (Mới trong V1.1)
- 🕒 **Bộ chọn giờ tùy chỉnh (TimePickerBox)**: 2 ComboBox giờ (00–23) và phút (00–59 bước 5, cho phép gõ tay), nút xóa giờ nhanh `✕`, cùng 3 phím tắt tiện lợi: `+15m`, `+1h`, `20:00`.
- 🏷️ **Badge màu sắc trạng thái thời gian thực trên HUD**:
  - 🔵 **Xanh chuẩn (`#60A5FA`)**: Bình thường (còn hơn 30 phút là đến hạn).
  - 🟡 **Vàng cảnh báo (`#F59E0B`)**: Sắp đến hạn (còn dưới 30 phút).
  - 🔴 **Đỏ báo động (`#EF4444`)**: Đã quá hạn mà chưa hoàn thành.
  - ⚪ **Xám mờ + Gạch ngang (`#94A3B8`)**: Đã hoàn thành.
  - Tự động làm mới màu sắc mỗi 30 giây mà **hoàn toàn không giành focus** của người dùng.
- 🔔 **Cửa sổ Nhắc nhở Nổi bật (ReminderWindow)**:
  - Tự động gom nhóm toàn bộ các mục đến hạn trong cùng một thời điểm vào **MỘT cửa sổ duy nhất**.
  - Nếu cửa sổ đang mở, mục mới đến hạn sẽ tự động được nối thêm vào danh sách.
  - Căn giữa màn hình làm việc (WorkArea) chuẩn DPI.
  - Cờ Win32 `WS_EX_NOACTIVATE` giúp bạn tiếp tục gõ chữ trên Word/Notepad mà **không bị mất con trỏ soạn thảo**.
  - **Cơ chế chống bấm nhầm (Safety Guard)**: Vô hiệu hóa phím `Enter` và `Space` trong **1.5 giây đầu tiên** khi cửa sổ vừa xuất hiện.
  - 3 nút hành động tiện lợi cho mỗi mục:
    - `[✓ Đã hoàn thành]`: Cập nhật ngay lập tức lên Overlay HUD và file dữ liệu.
    - `[⏰ Nhắc lại sau ▾]`: Menu chọn nhanh hoãn báo lại `5 phút`, `10 phút`, `30 phút`, hoặc `60 phút`.
    - `[✕ Bỏ qua]`: Bỏ qua thông báo cho lần hẹn này.
  - Cửa sổ tự động đóng lại khi người dùng đã xử lý xong toàn bộ các mục trong danh sách.
- 🌙 **Chế độ Không làm phiền (Do Not Disturb - DND)**:
  - Khi bật DND, toàn bộ thông báo popup và âm thanh sẽ được tạm dừng mà vẫn ghi nhận trạng thái đến hạn vào hàng chờ ngầm.
  - Ngay khi tắt DND, một cửa sổ gom nhóm sẽ lập tức xuất hiện hiển thị các mục tiêu đang chờ.
- ⚙️ **Tùy chỉnh Nhắc nhở Linh hoạt**:
  - Tùy chọn Bật/Tắt âm thanh chuông báo hệ thống.
  - Nhắc nhở sớm trước thời hạn (`EarlyReminderMinutes`: 5, 10, 15, 30 phút).
  - Nhắc nhở lặp lại chu kỳ nếu chưa hoàn thành (`RepeatReminderMinutes`: 5, 10, 15, 30 phút).

---

## 📥 Tải về & Cài đặt

### Cách 1: Sử dụng bộ cài đặt Setup (Khuyên dùng)
1. Tải file **`GoalOverlay-Setup.exe`** từ mục [Releases](https://github.com/dangbinhduongwork-ai/Target_X/releases).
2. Chạy file cài đặt:
   - Cài đặt vào `%LOCALAPPDATA%\Programs\GoalOverlay` theo chuẩn hiện đại.
   - **Không yêu cầu quyền Quản trị viên (UAC Administrator)** - cài đặt 1-click nhanh chóng.
   - Hỗ trợ **cài đè trực tiếp lên V1.0** mà vẫn bảo toàn 100% dữ liệu mục tiêu và cấu hình khởi động.
   - Tự động tạo Shortcut trong Start Menu và Desktop.

### Cách 2: Sử dụng bản Portable (Chạy trực tiếp)
- Tải file **`GoalOverlay-v1.1.0-win-x64-portable.zip`**, giải nén và chạy file `GoalOverlay.exe` ngay mà không cần cài đặt. Toàn bộ .NET Runtime đã được nhúng sẵn.

---

## 📖 Hướng dẫn Sử dụng

### 1. Quản lý mục tiêu (MainWindow)
- **Thêm mục tiêu có hẹn giờ**:
  - Tab "Hôm nay": Nhập nội dung, chọn giờ hẹn qua `TimePickerBox` (hoặc bấm `+15m`, `+1h`, `20:00`) -> Nhấn **Enter** hoặc bấm `+ Thêm`.
  - Nếu chọn giờ đã qua trong ngày, ứng dụng sẽ hiện cảnh báo nhỏ *"⚠️ Giờ này đã qua, sẽ nhắc ngay khi lưu"* nhưng vẫn cho phép lưu.
  - Tab "Tháng này": Chọn ngày hạn chót trong DatePicker và giờ hẹn tùy chọn trong `TimePickerBox`.
- **Sửa nhanh tại chỗ (In-place Edit)**:
  - Nhấp đúp chuột (**Double-click**) vào dòng mục tiêu -> khung sửa xuất hiện cho phép sửa cả nội dung văn bản, ngày hạn và giờ hẹn -> bấm `✓` để lưu (hoặc `✕` để hủy).
- **Kéo thả sắp xếp thứ tự**: Nhấn giữ biểu tượng tay cầm `⠿` ở đầu dòng và kéo thả đến vị trí mong muốn.

### 2. Tương tác với Cửa sổ Nhắc nhở (ReminderWindow)
- Khi đến giờ hẹn (hoặc đến mốc nhắc sớm), cửa sổ sẽ tự động nổi lên ở giữa màn hình cùng âm thanh chuông báo và icon Taskbar nhấp nháy.
- Thao tác xử lý:
  - Bấm `[✓ Đã hoàn thành]`: Mục tiêu được đánh dấu xong ngay lập tức trên HUD.
  - Bấm `[⏰ Nhắc lại sau ▾]`: Chọn thời gian hoãn (5, 10, 30, 60 phút).
  - Bấm `[✕ Bỏ qua]`: Bỏ qua lời nhắc cho chu kỳ này.
  - Sau khi xử lý hết mọi mục, cửa sổ sẽ tự động đóng lại.

### 3. Khay hệ thống (System Tray Menu)
- Nhấp chuột phải vào icon GoalOverlay ở khay hệ thống góc phải taskbar:
  - **`🎯 Mở quản lý`**: Mở cửa sổ chính.
  - **`👁️ Ẩn/Hiện overlay`**: Bật/tắt nhanh HUD (Ctrl + Alt + G).
  - **`🌙 Không làm phiền (Do Not Disturb)`**: Bật/tắt nhanh chế độ im lặng.
  - **`🔔 Kiểm tra nhắc nhở thử`**: Bật ngay một cửa sổ popup demo 2 mục tiêu để trải nghiệm tính năng.
  - **`⚙️ Cài đặt`**: Chuyển thẳng đến tab Cài đặt.
  - **`❌ Thoát`**: Đóng hoàn toàn ứng dụng.

---

## 🛠️ Hướng dẫn Biên dịch từ Mã nguồn

### Yêu cầu môi trường:
- Windows 10 (bản 1809 trở lên) hoặc Windows 11 (64-bit).
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) hoặc mới hơn.
- (Tùy chọn) [Inno Setup 6](https://jrsoftware.org/isinfo.php) để đóng gói file Setup:
  ```powershell
  winget install JRSoftware.InnoSetup
  ```

### Các lệnh thực thi (PowerShell):

```powershell
# 1. Chạy toàn bộ 65 bài kiểm thử Unit Test (xUnit)
dotnet test GoalOverlay.sln

# 2. Chạy ứng dụng trực tiếp từ mã nguồn
dotnet run --project src/GoalOverlay/GoalOverlay.csproj

# 3. Publish bản phát hành Release x64 Single-file
dotnet publish src/GoalOverlay/GoalOverlay.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o src/GoalOverlay/bin/Release/net8.0-windows/win-x64/publish

# 4. Tự động kiểm thử, biên dịch và đóng gói Installer Setup + Portable ZIP
powershell -ExecutionPolicy Bypass -File installer/build_installer.ps1
```
> Kết quả đóng gói sẽ được tạo tại thư mục `installer/output/`:
> - `GoalOverlay-Setup.exe` (Bộ cài đặt Windows tự động)
> - `GoalOverlay-v1.1.0-win-x64-portable.zip` (Bản chạy trực tiếp không cần cài đặt)

---

## 🏗️ Cấu trúc Thư mục Dự án

```
Target_X/
├── GoalOverlay.sln               # Solution Visual Studio
├── README.md                     # Tài liệu hướng dẫn sử dụng & phát triển
├── docs/
│   └── TEST-CHECKLIST.md         # Bảng kiểm thử nghiệm thu 18 kịch bản V1.0 & V1.1 (100% PASS)
├── src/
│   └── GoalOverlay/              # Dự án WPF Desktop chính (.NET 8 Windows)
│       ├── Assets/               # Biểu tượng ứng dụng (app.ico)
│       ├── Models/               # Mô hình dữ liệu (Goal, GoalDueStatus, AppSettings, ScreenCorner)
│       ├── Services/             # Logic nghiệp vụ (GoalService, ReminderService, AutostartService, Repositories)
│       ├── ViewModels/           # Logic giao diện MVVM (MainViewModel, OverlayViewModel, ReminderWindowViewModel)
│       ├── Views/
│       │   ├── MainWindow.xaml   # Cửa sổ quản lý mục tiêu & Cài đặt
│       │   ├── OverlayWindow.xaml# Cửa sổ HUD Overlay ghim góc màn hình
│       │   ├── ReminderWindow.xaml # Cửa sổ nhắc nhở đến giờ (NoActivate, Safety Guard)
│       │   ├── Controls/         # UserControl chọn giờ tùy chỉnh (TimePickerBox)
│       │   └── Converters/       # Value converters (Minutes, Visibility, Colors)
│       └── Native/               # P/Invoke Win32 API, DpiScale, OverlayPositionCalculator
├── tests/
│   └── GoalOverlay.Tests/        # Bộ kiểm thử tự động xUnit (65 tests PASS 100%)
│       ├── GoalServiceTests.cs
│       ├── JsonGoalRepositoryTests.cs
│       ├── AutostartServiceTests.cs
│       ├── OverlayPositionCalculatorTests.cs
│       ├── AutostartAndHotkeyTests.cs
│       ├── ReminderServiceTests.cs
│       ├── Phase8ModelAndMigrationTests.cs
│       ├── Phase9UiAndDueStatusTests.cs
│       ├── Phase10ReminderSchedulerTests.cs
│       ├── Phase11ReminderSchedulerTests.cs
│       └── Mocks/                # TestClock, TestLogger, FakeRegistryService
└── installer/                    # Kịch bản đóng gói cài đặt
    ├── GoalOverlay.iss           # Script Inno Setup 6 (Non-admin, Cài đè giữ dữ liệu)
    ├── build_installer.ps1       # Script tự động hóa 1-click Test, Publish & Đóng gói
    └── output/                   # File cài đặt và file zip thành phẩm
```
