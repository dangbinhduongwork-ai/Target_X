# GoalOverlay 🎯

Ứng dụng Windows Desktop hiện đại được phát triển bằng **C# / .NET 8 / WPF**, giúp hiển thị mục tiêu hàng ngày (Daily Goals) và hàng tháng (Monthly Goals) dạng HUD Overlay bán trong suốt ở góc trên bên phải màn hình, luôn hiển thị trên mọi phần mềm khác (`Topmost`), tự chạy khi bật máy, và cung cấp giao diện quản lý CRUD đầy đủ.

---

## 🏗️ Cấu trúc Thư mục

```
GoalOverlay/
├── GoalOverlay.sln
├── .gitignore
├── .editorconfig
├── README.md
├── src/
│   └── GoalOverlay/              # Dự án WPF chính (.NET 8, net8.0-windows)
│       ├── app.manifest          # Khai báo PerMonitorV2 DPI awareness & asInvoker
│       ├── Assets/               # Tài nguyên hình ảnh, biểu tượng (app.ico)
│       ├── Models/               # Mô hình dữ liệu (GoalItem, GoalType, AppSettings)
│       ├── Services/             # Hạ tầng I/O (%APPDATA%, Logging, Registry)
│       ├── ViewModels/           # Lớp logic trình bày MVVM (CommunityToolkit.Mvvm)
│       ├── Views/                # Giao diện XAML (MainWindow, OverlayWindow)
│       └── Native/               # P/Invoke Win32 API & DpiHelper
├── tests/
│   └── GoalOverlay.Tests/        # Dự án Unit Test (xUnit)
└── installer/                    # Chứa kịch bản đóng gói cài đặt (Giai đoạn 8)
```

---

## 🚀 Hướng dẫn Build & Chạy

### Yêu cầu môi trường:
- Windows 10 (bản 1809 trở lên) hoặc Windows 11.
- .NET 8.0 SDK (hoặc mới hơn).

### Lệnh thực thi:

```powershell
# 1. Khôi phục gói và biên dịch toàn bộ solution
dotnet build GoalOverlay.sln

# 2. Chạy ứng dụng GoalOverlay
dotnet run --project src/GoalOverlay/GoalOverlay.csproj

# 3. Chạy toàn bộ bộ test kiểm thử
dotnet test GoalOverlay.sln
```

---

## ⚙️ Quy chuẩn & Tiêu chuẩn Kỹ thuật
- **Kiến trúc**: MVVM thuần với `CommunityToolkit.Mvvm`.
- **DPI Awareness**: `PerMonitorV2` (tự động thích ứng sắc nét trên màn hình 4K / đa màn hình).
- **Lưu trữ dữ liệu**: `%APPDATA%\GoalOverlay\goals.json` & `settings.json`.
- **Ghi log an toàn**: `%APPDATA%\GoalOverlay\logs\goaloverlay_{yyyyMMdd}.log`.
- **Quyền thực thi**: `asInvoker` (chạy an toàn với quyền người dùng thông thường, không yêu cầu UAC Administrator).
