# GoalOverlay 🎯

[![.NET 8.0](https://img.shields.io/badge/.NET-8.0--windows-blue?logo=dotnet)](https://dotnet.microsoft.com/)
[![WPF](https://img.shields.io/badge/UI-WPF%20XAML-purple)](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/)
[![Architecture](https://img.shields.io/badge/Architecture-MVVM%20Toolkit-success)](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/)
[![Tests](https://img.shields.io/badge/Tests-39%20Passed%20(100%25)-brightgreen)](https://github.com/dangbinhduongwork-ai/Target_X)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%2F11%20x64-informational)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

**GoalOverlay** là ứng dụng Windows Desktop hiện đại, tối giản và hiệu suất cao, giúp hiển thị các mục tiêu hàng ngày (*Daily Goals*) và hàng tháng (*Monthly Goals*) dưới dạng HUD Overlay bán trong suốt ở góc trên bên phải màn hình. Ứng dụng luôn hiển thị trên mọi phần mềm khác (`Topmost`), không cướp focus bàn phím, tự khởi động ngầm cùng Windows và cung cấp giao diện quản lý CRUD trực quan.

---

## ✨ Tính năng Nổi bật

- 📌 **HUD Overlay ghim màn hình**: Nổi trên mọi ứng dụng (Chrome, Word, Excel, Video borderless), tự căn chỉnh theo DPI (PerMonitorV2) và cách mép 16px.
- ⚡ **Không cướp Focus (`WS_EX_NOACTIVATE`)**: Thao tác nhấp chuột trên Overlay không bao giờ làm mất con trỏ soạn thảo hoặc gián đoạn công việc ở các phần mềm khác.
- 🖱️ **Chế độ Click-Through (`WS_EX_TRANSPARENT`)**: Cho phép click chuột xuyên thấu qua Overlay tới ứng dụng nằm bên dưới.
- 🎨 **Live Preview thời gian thực**: Tùy chỉnh độ trong suốt (20% - 100%), cỡ chữ (11px - 20px), màu chữ (Trắng, Cyan, Vàng, Xanh lá, Hồng) và phản chiếu ngay lập tức lên Overlay.
- 🔄 **Tự chuyển tiếp mục tiêu chưa xong (Carry-over)**: Tự động dời các mục tiêu chưa hoàn thành của hôm qua sang hôm nay khi bước qua 00:00 nửa đêm.
- ⌨️ **Phím tắt toàn cục (Global Hotkey)**: Nhấn **`Ctrl + Alt + G`** từ bất kỳ ứng dụng nào để ẩn hoặc hiện nhanh Overlay.
- 🔔 **Khay hệ thống (System Tray)**: Tích hợp menu chuột phải đầy đủ (*Mở quản lý, Ẩn/Hiện overlay, Cài đặt, Thoát*). Bấm nút X chỉ ẩn xuống khay hệ thống, không tắt app.
- 🛡️ **SingleInstanceGuard**: Sử dụng `Mutex` ngăn chạy trùng lặp; khởi động lần 2 sẽ tự động đưa cửa sổ quản lý của phiên bản đang chạy lên phía trước.
- 🚀 **Tự khởi động cùng Windows**: Tự đăng ký Registry Run với cờ `--minimized` (chỉ hiện Overlay và khay hệ thống, không bật cửa sổ quản lý). Hỗ trợ cơ chế tự sửa đường dẫn (*Self-healing*) nếu người dùng di chuyển file exe.
- 💾 **Lưu trữ an toàn tuyệt đối**: Ghi file an toàn 2 bước (`.tmp` -> `File.Replace` -> `.bak`). Tự động cô lập file hỏng (`.corrupt-<timestamp>`) và không bao giờ crash.

---

## 📥 Tải về & Cài đặt

### Cách 1: Sử dụng bộ cài đặt tự động (Khuyên dùng)
1. Tải file **`GoalOverlay-Setup.exe`** từ mục [Releases](https://github.com/dangbinhduongwork-ai/Target_X/releases) (hoặc tự đóng gói từ mã nguồn).
2. Chạy file cài đặt:
   - Ứng dụng được cài đặt vào `%LOCALAPPDATA%\Programs\GoalOverlay` theo chuẩn hiện đại.
   - **Không yêu cầu quyền Quản trị viên (UAC Administrator)** - cài đặt 1-click nhanh chóng.
   - Tự động tạo Shortcut trong Start Menu và Desktop (tùy chọn).
   - Tùy chọn bật "Khởi động cùng Windows".

### Cách 2: Sử dụng bản Portable (Chạy trực tiếp)
- Tải file **`GoalOverlay.exe`** (Single-file Self-contained) và chạy ngay mà không cần cài đặt. Toàn bộ .NET Runtime đã được nhúng sẵn.

---

## 📖 Hướng dẫn Sử dụng

### 1. Quản lý mục tiêu (MainWindow)
- **Thêm mục tiêu mới**: Nhập nội dung vào ô nhập liệu và nhấn phím **Enter** (hoặc bấm nút `+ Thêm`). Nội dung tối đa 120 ký tự, tự động cắt khoảng trắng thừa.
- **Hoàn thành mục tiêu**: Nhấp chuột vào ô tròn Checkbox. Mục tiêu hoàn thành sẽ có đường gạch ngang mờ và thanh tiến độ tự cập nhật.
- **Sửa nhanh tại chỗ (In-place Edit)**: Nhấp đúp chuột (**Double-click**) vào dòng chữ mục tiêu -> gõ nội dung mới -> nhấn **Enter** để lưu (hoặc **Escape** để hủy).
- **Kéo thả sắp xếp thứ tự**: Nhấn giữ chuột vào biểu tượng tay cầm `⠿` ở đầu mỗi dòng và kéo đến vị trí mong muốn.
- **Xóa mục tiêu**: Bấm nút `✕` màu đỏ ở bên phải mỗi dòng.

### 2. Các cách mở giao diện Quản lý khi phần mềm đang chạy
1. **Trực tiếp trên Overlay HUD**:
   - Bấm nút **`➕ Thêm`** ở góc trên bên phải HUD: Mở ngay cửa sổ và focus sẵn vào ô nhập mục tiêu.
   - Bấm nút **`⚙️`**: Mở cửa sổ quản lý và chuyển thẳng đến tab Cài đặt.
   - **Nhấp đúp chuột (Double-click)** vào bất kỳ vị trí nào trên thẻ HUD: Mở ngay cửa sổ quản lý.
   - **Chuột phải (Right-click)** vào HUD: Mở menu ngữ cảnh nhanh để thêm mục tiêu hôm nay, mục tiêu tháng này, hoặc cài đặt.
2. **Phím tắt toàn cục (Global Hotkey)**:
   - **`Ctrl + Alt + M`**: Mở ngay cửa sổ Quản lý mục tiêu từ bất kỳ ứng dụng nào trên Windows.
   - **`Ctrl + Alt + G`**: Ẩn hoặc hiện nhanh cửa sổ HUD Overlay.
3. **Mở từ Desktop / Start Menu**:
   - Khi ứng dụng đang chạy ngầm, bạn chỉ cần nhấp đúp vào icon **GoalOverlay** trên Desktop hoặc Start Menu, cửa sổ quản lý sẽ tự động được đưa lên trên cùng (Single Instance Guard).
4. **Từ Khay hệ thống (System Tray Icon)**:
   - **Nhấp đúp hoặc nhấp chuột trái** vào biểu tượng GoalOverlay ở góc đồng hồ taskbar.
   - **Nhấp chuột phải** chọn **`🎯 Mở quản lý`**.

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
# 1. Chạy toàn bộ 39 bài kiểm thử Unit Test (xUnit)
dotnet test GoalOverlay.sln

# 2. Chạy ứng dụng trực tiếp từ mã nguồn
dotnet run --project src/GoalOverlay/GoalOverlay.csproj

# 3. Publish bản phát hành độc lập (Single-file Self-contained)
dotnet publish src/GoalOverlay/GoalOverlay.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o src/GoalOverlay/bin/Release/net8.0-windows/win-x64/publish

# 4. Tự động biên dịch và đóng gói bộ cài đặt Inno Setup
powershell -ExecutionPolicy Bypass -File installer/build_installer.ps1
```
> Kết quả bộ cài đặt sẽ được tạo tại: `installer/output/GoalOverlay-Setup.exe`.

---

## 🗑️ Hướng dẫn Gỡ cài đặt Sạch sẽ (Clean Uninstall)

1. Mở **Windows Settings** (`Win + I`) -> **Apps** -> **Installed apps** (hoặc mở *Control Panel > Programs and Features*).
2. Tìm **GoalOverlay** và chọn **Uninstall**.
3. Trình gỡ cài đặt sẽ tự động:
   - Đóng tiến trình GoalOverlay nếu đang chạy ngầm.
   - Xóa bỏ khóa khởi động trong Registry (`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`).
   - Xóa toàn bộ file thực thi và lối tắt Start Menu / Desktop.
   - **Hộp thoại xác nhận**: Hỏi người dùng *"Bạn có muốn xóa toàn bộ dữ liệu mục tiêu và cấu hình cá nhân tại %APPDATA%\GoalOverlay không?"*:
     - Chọn **Yes**: Xóa sạch toàn bộ dữ liệu, logs và backups không để lại rác trên máy tính.
     - Chọn **No**: Giữ lại file mục tiêu phòng trường hợp bạn muốn cài lại trong tương lai.

---

## 🏗️ Cấu trúc Thư mục Dự án

```
Target_X/
├── GoalOverlay.sln               # Visual Studio Solution File
├── README.md                     # Tài liệu hướng dẫn sử dụng & phát triển
├── .editorconfig                 # Tiêu chuẩn định dạng mã nguồn .NET
├── .gitignore                    # Cấu hình bỏ qua tệp tạm Git
├── docs/
│   └── TEST-CHECKLIST.md         # Bảng kiểm thử toàn diện 8 kịch bản (100% PASS)
├── src/
│   └── GoalOverlay/              # Dự án WPF Desktop chính (.NET 8 Windows)
│       ├── app.manifest          # Khai báo PerMonitorV2 DPI awareness & asInvoker
│       ├── EnsureIcon.ps1        # Kịch bản tự động khởi tạo app.ico hợp lệ
│       ├── Assets/               # Biểu tượng ứng dụng (app.ico)
│       ├── Models/               # Mô hình dữ liệu (Goal, GoalType, AppSettings, ScreenCorner)
│       ├── Services/             # Logic nghiệp vụ (GoalService, AutostartService, HotkeyService, Repositories)
│       ├── ViewModels/           # Logic giao diện MVVM (MainViewModel, OverlayViewModel, GoalItemViewModel)
│       ├── Views/                # Giao diện XAML (MainWindow, OverlayWindow)
│       └── Native/               # P/Invoke Win32 API, DpiScale, OverlayPositionCalculator
├── tests/
│   └── GoalOverlay.Tests/        # Bộ kiểm thử đơn vị tự động (xUnit - 39 tests)
│       ├── GoalServiceTests.cs
│       ├── JsonGoalRepositoryTests.cs
│       ├── AutostartServiceTests.cs
│       ├── OverlayPositionCalculatorTests.cs
│       ├── AutostartAndHotkeyTests.cs
│       └── Mocks/                # FakeRegistryService, TestClock, TestLogger
└── installer/                    # Kịch bản đóng gói cài đặt
    ├── GoalOverlay.iss           # Script Inno Setup 6 (Non-admin, Clean Uninstall)
    ├── build_installer.ps1       # Script tự động hóa 1-click Publish & Đóng gói Setup
    └── output/                   # Chứa file GoalOverlay-Setup.exe thành phẩm
```

---

## ⚙️ Quy chuẩn & Tiêu chuẩn Kỹ thuật

- **Kiến trúc**: MVVM thuần (`CommunityToolkit.Mvvm`).
- **DPI Awareness**: `PerMonitorV2` (tự động điều chỉnh kích thước sắc nét khi kéo thả qua các màn hình có DPI khác nhau: 100%, 125%, 150%, 200%).
- **Lưu trữ dữ liệu**: `%APPDATA%\GoalOverlay\goals.json` & `settings.json`.
- **Ghi log lỗi**: `%APPDATA%\GoalOverlay\logs\errors.log` (mọi thao tác I/O, Win32 và Registry đều có try-catch).
- **Quyền thực thi**: `asInvoker` (chạy an toàn với quyền người dùng thông thường, không yêu cầu UAC Administrator).
- **Mức tiêu hao tài nguyên**: RAM ổn định ~40MB - 60MB, CPU nhàn rỗi ~0.0%.

---

## ⚠️ Known Issues & Các Lỗi Đã Xử Lý

Chi tiết đầy đủ về các lỗi kỹ thuật đã được khắc phục triệt để trong quá trình phát triển:
1. **Lỗi hàm `WriteAllBytes` trong MSBuild (MSB4185)**: Đã thay thế sang task `Exec` gọi PowerShell giải mã `Assets/app.ico` trước khi biên dịch.
2. **Lỗi Win32 resource `CS7065` do file icon 0 bytes**: Đã tạo script `EnsureIcon.ps1` dùng GDI+ tạo file icon 32x32 chuẩn Win32.
3. **Lỗi thuộc tính `LetterSpacing` trong WPF XAML (MC3072)**: Đã chuẩn hóa loại bỏ thuộc tính không hỗ trợ trong WPF TextBlock.
4. **Lỗi nạp chồng Generic `WeakReferenceMessenger.Register` (CS0305)**: Chuẩn hóa `OverlayWindow` kế thừa `IRecipient<SettingsChangedMessage>`.
5. **Trùng lặp phím tắt toàn cục (Global Hotkey Conflict)**: Bắt mã lỗi từ `RegisterHotKey`, ghi log cảnh báo và thông báo trạng thái nhẹ trong Cài đặt, không crash.
6. **Chế độ Game Toàn màn hình Độc quyền (Exclusive Fullscreen)**: Direct3D/Vulkan độc quyền bypass Windows DWM; người dùng chỉ cần chọn chế độ hiển thị *Borderless Windowed* để Overlay hiển thị bình thường.
7. **Cơ chế tự sửa đường dẫn Autostart (Self-healing)**: Tự động cập nhật Registry Run khi người dùng di chuyển file exe sang thư mục khác.
8. **Khôi phục dữ liệu an toàn khi file JSON bị lỗi**: Tự đổi tên file hỏng thành `goals.json.corrupt-<timestamp>` và khôi phục về danh sách rỗng.
