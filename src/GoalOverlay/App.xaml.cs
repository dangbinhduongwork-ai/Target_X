using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.Messaging;
using GoalOverlay.Models;
using GoalOverlay.Native;
using GoalOverlay.Services;
using GoalOverlay.ViewModels;
using GoalOverlay.Views;

namespace GoalOverlay;

/// <summary>
/// Interaction logic for App.xaml
/// Quản lý vòng đời ứng dụng GoalOverlay:
/// - SingleInstanceGuard qua Mutex và Windows Message
/// - Tự khởi động cùng Windows (--minimized)
/// - Quản lý System Tray Icon (H.NotifyIcon) và menu điều hướng
/// - Đăng ký phím tắt toàn cục Ctrl + Alt + G
/// </summary>
public partial class App : Application
{
    private const string MutexName = "GoalOverlay_App_Mutex_8F7E4D1B";
    private const string ActivateMessageName = "GoalOverlay_ActivateRunningInstance";
    private const string EventWaitHandleName = "GoalOverlay_ShowManager_Event_8F7E4D1B";

    private static Mutex? _mutex;
    private static EventWaitHandle? _eventWaitHandle;
    private static RegisteredWaitHandle? _registeredWait;
    private H.NotifyIcon.TaskbarIcon? _trayIcon;

    public static uint ActivateMessageId { get; private set; }

    public static ILoggerService Logger { get; } = new LoggerService();
    public static ISettingsService SettingsService { get; } = new SettingsService(Logger);
    public static IGoalRepository GoalRepository { get; } = new JsonGoalRepository(Logger);
    public static IClock Clock { get; } = new SystemClock();
    public static IGoalService GoalService { get; } = new GoalService(GoalRepository, Clock, Logger);
    public static IAutostartService AutostartService { get; } = new AutostartService(Logger);
    public static IHotkeyService HotkeyService { get; } = new HotkeyService(Logger);
    public static IReminderService ReminderService { get; } = new ReminderService(GoalRepository, GoalService, Clock, Logger);

    public static OverlayWindow? OverlayWindow { get; private set; }
    public static MainWindow? MainWindowInstance { get; private set; }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                Logger.Error("Ngoại lệ chưa được xử lý cấp AppDomain", ex);
            }
        };

        DispatcherUnhandledException += (s, args) =>
        {
            Logger.Error("Ngoại lệ chưa được xử lý cấp UI Dispatcher", args.Exception);
            args.Handled = true;
        };

        // 1. SingleInstanceGuard: Kiểm tra instance duy nhất bằng Mutex
        _mutex = new Mutex(true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            Logger.Info("Phát hiện instance khác của GoalOverlay đang chạy. Đưa cửa sổ hiện tại lên và thoát instance mới.");

            // Kích hoạt qua EventWaitHandle (IPC độ trễ cực thấp, không bị chặn bởi UIPI Windows)
            try
            {
                if (EventWaitHandle.TryOpenExisting(EventWaitHandleName, out var existingEvent))
                {
                    existingEvent.Set();
                    existingEvent.Dispose();
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"Không thể kích hoạt EventWaitHandle: {ex.Message}");
            }

            // Dự phòng thêm qua Windows Message broadcast
            uint msg = NativeMethods.RegisterWindowMessage(ActivateMessageName);
            if (msg != 0)
            {
                NativeMethods.PostMessage(NativeMethods.HWND_BROADCAST, msg, IntPtr.Zero, IntPtr.Zero);
            }

            _mutex.Dispose();
            _mutex = null;
            Shutdown();
            return;
        }

        ActivateMessageId = NativeMethods.RegisterWindowMessage(ActivateMessageName);

        // Đăng ký EventWaitHandle để lắng nghe yêu cầu kích hoạt cửa sổ quản lý từ instance thứ 2
        try
        {
            _eventWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset, EventWaitHandleName);
            _registeredWait = ThreadPool.RegisterWaitForSingleObject(_eventWaitHandle, (state, timedOut) =>
            {
                Dispatcher.Invoke(() =>
                {
                    MainWindowInstance?.ShowAndActivate();
                });
            }, null, -1, false);
        }
        catch (Exception ex)
        {
            Logger.Warn($"Không thể tạo EventWaitHandle: {ex.Message}");
        }

        Logger.Info("=== Ứng dụng GoalOverlay khởi động (Giai đoạn 5: Chạy nền & Tự khởi động) ===");

        try
        {
            // 2. Self-healing Registry Run path nếu file exe bị di chuyển
            AutostartService.VerifyAndUpdatePath();

            // 3. Tải cấu hình ứng dụng
            await SettingsService.LoadSettingsAsync();

            // 4. Kiểm tra tham số dòng lệnh --minimized
            bool isMinimized = e.Args.Any(arg => string.Equals(arg, "--minimized", StringComparison.OrdinalIgnoreCase));

            // 5. Khởi tạo và hiển thị OverlayWindow (HUD ở góc trên bên phải màn hình)
            var overlayVm = new OverlayViewModel(GoalService, Clock, Logger);
            OverlayWindow = new OverlayWindow(overlayVm);
            OverlayWindow.Show();

            // 6. Khởi tạo MainWindow (Quản lý mục tiêu & Cài đặt)
            var mainVm = new MainViewModel(GoalService, GoalRepository, Clock, Logger, AutostartService, HotkeyService);
            MainWindowInstance = new MainWindow(mainVm);

            if (!isMinimized)
            {
                MainWindowInstance.ShowAndActivate();
            }
            else
            {
                Logger.Info("Khởi động ở chế độ --minimized: MainWindow được ẩn xuống khay hệ thống.");
            }

            // 7. Khởi tạo System Tray Icon (H.NotifyIcon)
            InitializeTrayIcon();

            // 8. Bắt đầu bộ định thời quét nhắc nhở mục tiêu (V1.1)
            ReminderService.GoalsReminderBatchDue += (s, args) =>
            {
                ReminderWindow.ShowOrAddGoals(args.Goals, SettingsService.CurrentSettings, ReminderService, Clock);
            };
            ReminderService.Start();

            Logger.Info("Đã khởi tạo thành công Overlay, MainWindow, System Tray Icon và ReminderService.");
        }
        catch (Exception ex)
        {
            Logger.Error("Lỗi trong quá trình khởi tạo ứng dụng", ex);
        }
    }

    /// <summary>
    /// Khởi tạo biểu tượng khay hệ thống (System Tray Icon) và menu điều hướng
    /// </summary>
    private void InitializeTrayIcon()
    {
        try
        {
            _trayIcon = new H.NotifyIcon.TaskbarIcon
            {
                ToolTipText = "GoalOverlay - Mục tiêu hàng ngày & hàng tháng"
            };

            // Nạp icon từ resource hoặc file exe
            try
            {
                _trayIcon.IconSource = new BitmapImage(new Uri("pack://application:,,,/GoalOverlay;component/Assets/app.ico", UriKind.Absolute));
            }
            catch
            {
                try
                {
                    string localIco = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app.ico");
                    if (File.Exists(localIco))
                    {
                        _trayIcon.IconSource = new BitmapImage(new Uri(localIco));
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn($"Không thể nạp icon khay hệ thống: {ex.Message}");
                }
            }

            // Tạo ContextMenu theo chuẩn yêu cầu
            var contextMenu = new ContextMenu();

            var openItem = new MenuItem { Header = "🎯 Mở quản lý" };
            openItem.Click += (s, e) => MainWindowInstance?.ShowAndActivate();

            var toggleOverlayItem = new MenuItem { Header = "👁️ Ẩn/Hiện overlay (Ctrl+Alt+G)" };
            toggleOverlayItem.Click += (s, e) => OverlayWindow?.ToggleVisibility();

            // Menu item: Không làm phiền (Do Not Disturb)
            var dndItem = new MenuItem
            {
                Header = "🌙 Không làm phiền (Do Not Disturb)",
                IsCheckable = true,
                IsChecked = SettingsService.CurrentSettings.DoNotDisturb
            };
            dndItem.Click += async (s, e) =>
            {
                var settings = SettingsService.CurrentSettings;
                settings.DoNotDisturb = !settings.DoNotDisturb;
                dndItem.IsChecked = settings.DoNotDisturb;
                await SettingsService.SaveSettingsAsync(settings);
                WeakReferenceMessenger.Default.Send(new SettingsChangedMessage(settings));
            };

            // Đồng bộ trạng thái checkbox khi DND được thay đổi từ MainWindow
            WeakReferenceMessenger.Default.Register<App, SettingsChangedMessage>(this, (r, m) =>
            {
                r.Dispatcher.BeginInvoke(() => { dndItem.IsChecked = m.Settings.DoNotDisturb; });
            });

            // Menu item: Kiểm tra nhắc nhở thử (popup demo)
            var testReminderItem = new MenuItem { Header = "🔔 Kiểm tra nhắc nhở thử" };
            testReminderItem.Click += (s, e) =>
            {
                var demoGoals = new System.Collections.Generic.List<Goal>
                {
                    new Goal
                    {
                        Text = "Mục tiêu demo kiểm tra chuông nhắc nhở (Đúng giờ)",
                        DueAt = Clock.Now,
                        Type = GoalType.Daily
                    },
                    new Goal
                    {
                        Text = "Mục tiêu demo kiểm tra cảnh báo quá hạn (Đã quá 15 phút)",
                        DueAt = Clock.Now.AddMinutes(-15),
                        Type = GoalType.Daily
                    }
                };
                ReminderWindow.ShowOrAddGoals(demoGoals, SettingsService.CurrentSettings, ReminderService, Clock);
            };

            var settingsItem = new MenuItem { Header = "⚙️ Cài đặt" };
            settingsItem.Click += (s, e) =>
            {
                MainWindowInstance?.ShowAndActivate();
                MainWindowInstance?.SelectSettingsTab();
            };

            var exitItem = new MenuItem { Header = "❌ Thoát" };
            exitItem.Click += (s, e) => ShutdownApp();

            contextMenu.Items.Add(openItem);
            contextMenu.Items.Add(toggleOverlayItem);
            contextMenu.Items.Add(dndItem);
            contextMenu.Items.Add(testReminderItem);
            contextMenu.Items.Add(settingsItem);
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(exitItem);

            _trayIcon.ContextMenu = contextMenu;

            // Nhấp đúp chuột hoặc nhấp chuột trái vào icon khay hệ thống để mở cửa sổ quản lý
            _trayIcon.TrayMouseDoubleClick += (s, e) => MainWindowInstance?.ShowAndActivate();
            _trayIcon.TrayLeftMouseDown += (s, e) => MainWindowInstance?.ShowAndActivate();
        }
        catch (Exception ex)
        {
            Logger.Error("Lỗi khi tạo System Tray Icon", ex);
        }
    }

    /// <summary>
    /// Đóng ứng dụng an toàn: dọn dẹp TrayIcon, đóng các cửa sổ và giải phóng Mutex
    /// </summary>
    public void ShutdownApp()
    {
        try
        {
            Logger.Info("Người dùng yêu cầu thoát ứng dụng GoalOverlay.");

            _trayIcon?.Dispose();
            _trayIcon = null;

            ReminderService.Stop();

            _registeredWait?.Unregister(null);
            _registeredWait = null;
            _eventWaitHandle?.Dispose();
            _eventWaitHandle = null;

            MainWindowInstance?.CloseExplicitly();
            OverlayWindow?.Close();
            ReminderWindow.Current?.Close();

            if (_mutex != null)
            {
                _mutex.ReleaseMutex();
                _mutex.Dispose();
                _mutex = null;
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Lỗi khi thoát ứng dụng", ex);
        }
        finally
        {
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _trayIcon?.Dispose();
            _trayIcon = null;

            ReminderService.Stop();

            _registeredWait?.Unregister(null);
            _registeredWait = null;
            _eventWaitHandle?.Dispose();
            _eventWaitHandle = null;

            if (_mutex != null)
            {
                _mutex.ReleaseMutex();
                _mutex.Dispose();
                _mutex = null;
            }
        }
        catch { }

        base.OnExit(e);
    }
}
