using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Win32;
using GoalOverlay.Native;
using GoalOverlay.ViewModels;

namespace GoalOverlay.Views;

/// <summary>
/// Interaction logic for OverlayWindow.xaml
/// Cửa sổ HUD Overlay ghim góc trên bên phải màn hình, quản lý Win32 NoActivate/ClickThrough và tự làm mới
/// </summary>
public partial class OverlayWindow : Window, IRecipient<SettingsChangedMessage>
{
    public OverlayViewModel ViewModel { get; }

    private readonly DispatcherTimer _topmostTimer;
    private DateTime _lastCheckedDate;
    private IntPtr _hWnd;
    private bool _isDisposed;

    public OverlayWindow(OverlayViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = ViewModel;

        _lastCheckedDate = DateTime.Today;

        // Thiết lập timer định kỳ 30 giây để gia cố Topmost và kiểm tra bước qua 00:00
        _topmostTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(30)
        };
        _topmostTimer.Tick += TopmostTimer_Tick;

        SourceInitialized += OverlayWindow_SourceInitialized;
        Loaded += OverlayWindow_Loaded;
        SizeChanged += OverlayWindow_SizeChanged;
        DpiChanged += OverlayWindow_DpiChanged;
        Closed += OverlayWindow_Closed;

        // Đăng ký sự kiện hệ thống Windows
        SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;
        SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;

        // Lắng nghe thay đổi cài đặt từ MainWindow để Live Preview
        WeakReferenceMessenger.Default.Register<SettingsChangedMessage>(this);
    }

    /// <summary>
    /// Khi Handle của Window được tạo, áp dụng các cờ Win32: ToolWindow, NoActivate và Click-Through
    /// </summary>
    private void OverlayWindow_SourceInitialized(object? sender, EventArgs e)
    {
        try
        {
            _hWnd = new WindowInteropHelper(this).Handle;

            // 1. Áp dụng WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE (không hiện Alt+Tab, không cướp focus bàn phím)
            NativeMethods.SetToolWindowAndNoActivate(_hWnd);

            // 2. Thiết lập cờ click-through nếu được bật trong cấu hình
            NativeMethods.SetClickThrough(_hWnd, ViewModel.ClickThrough);

            // 3. Đưa cửa sổ lên đầu Z-Order
            NativeMethods.SetTopmost(_hWnd);

            // 4. Hook thông điệp Windows để nhận lệnh kích hoạt từ instance thứ 2
            var source = HwndSource.FromHwnd(_hWnd);
            source?.AddHook(WndProc);

            // 5. Đăng ký 2 phím tắt toàn cục:
            // - Ctrl + Alt + G: Ẩn/Hiện Overlay HUD
            // - Ctrl + Alt + M: Mở ngay cửa sổ Quản lý mục tiêu
            App.HotkeyService.Register(_hWnd, ToggleVisibility, () => App.MainWindowInstance?.ShowAndActivate());
        }
        catch (Exception ex)
        {
            App.Logger.Error("Lỗi khi cấu hình cờ Win32 cho OverlayWindow", ex);
        }
    }

    #region Các Tương Tác Trực Tiếp Trên HUD (Mở Quản Lý, Thêm Mục Tiêu, Cài Đặt)

    private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            App.MainWindowInstance?.ShowAndActivate();
        }
    }

    private void HeaderTitle_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            App.MainWindowInstance?.ShowAndActivate();
        }
    }

    private void OpenManagement_Click(object sender, RoutedEventArgs e)
    {
        App.MainWindowInstance?.ShowAndActivate();
    }

    private void AddDailyGoal_Click(object sender, RoutedEventArgs e)
    {
        App.MainWindowInstance?.OpenAndFocusDailyGoalInput();
    }

    private void AddMonthlyGoal_Click(object sender, RoutedEventArgs e)
    {
        App.MainWindowInstance?.OpenAndFocusMonthlyGoalInput();
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        App.MainWindowInstance?.OpenSettingsTab();
    }

    private void ToggleOverlay_Click(object sender, RoutedEventArgs e)
    {
        ToggleVisibility();
    }

    private void ExitApp_Click(object sender, RoutedEventArgs e)
    {
        if (Application.Current is App app)
        {
            app.ShutdownApp();
        }
    }

    #endregion

    /// <summary>
    /// Xử lý thông điệp broadcast từ SingleInstanceGuard khi người dùng mở ứng dụng lần thứ hai
    /// </summary>
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (App.ActivateMessageId != 0 && (uint)msg == App.ActivateMessageId)
        {
            Dispatcher.Invoke(() =>
            {
                App.MainWindowInstance?.ShowAndActivate();
            });
            handled = true;
        }
        return IntPtr.Zero;
    }

    /// <summary>
    /// Bật/tắt hiển thị Overlay HUD khi nhấn Ctrl + Alt + G hoặc từ menu khay hệ thống
    /// </summary>
    public void ToggleVisibility()
    {
        if (Visibility == Visibility.Visible)
        {
            Hide();
        }
        else
        {
            Show();
            if (_hWnd != IntPtr.Zero)
            {
                NativeMethods.SetTopmost(_hWnd);
            }
        }
    }

    private AppSettings? _cachedSettings;

    /// <summary>
    /// Nhận thông điệp thay đổi cài đặt từ MainWindow để cập nhật Live Preview tức thì
    /// </summary>
    public void Receive(SettingsChangedMessage message)
    {
        _cachedSettings = message.Settings;
        if (_hWnd != IntPtr.Zero)
        {
            NativeMethods.SetClickThrough(_hWnd, message.Settings.ClickThrough);
            Dispatcher.InvokeAsync(() => RepositionWindow(message.Settings));
        }
    }

    private async void OverlayWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _cachedSettings = App.SettingsService.CurrentSettings;
            await ViewModel.LoadGoalsCommand.ExecuteAsync(null);
            RepositionWindow(_cachedSettings);
            _topmostTimer.Start();
        }
        catch (Exception ex)
        {
            App.Logger.Error("Lỗi khi nạp dữ liệu ban đầu cho OverlayWindow", ex);
        }
    }

    /// <summary>
    /// Tự động căn chỉnh vị trí ở góc trên bên phải của màn hình đã chọn, cách mép 16px
    /// </summary>
    public void RepositionWindow(AppSettings? customSettings = null)
    {
        if (_hWnd == IntPtr.Zero || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        try
        {
            var settings = customSettings ?? _cachedSettings ?? App.SettingsService.CurrentSettings;
            _cachedSettings = settings;

            var monitors = NativeMethods.GetOrderedMonitors(this);
            if (monitors.Count > 0)
            {
                int index = Math.Clamp(settings.MonitorIndex, 0, monitors.Count - 1);
                var selected = monitors[index];

                double targetDpiX = selected.DpiScaleX > 0 ? selected.DpiScaleX : 1.0;
                double targetDpiY = selected.DpiScaleY > 0 ? selected.DpiScaleY : 1.0;

                int physicalWidth = (int)Math.Round(ActualWidth * targetDpiX);
                int physicalHeight = (int)Math.Round(ActualHeight * targetDpiY);
                int physicalMargin = (int)Math.Round(OverlayPositionCalculator.DefaultMargin * targetDpiX);

                int targetPhysicalX;
                int targetPhysicalY;

                switch (settings.Corner)
                {
                    case GoalOverlay.Models.ScreenCorner.TopLeft:
                        targetPhysicalX = selected.Info.rcWork.Left + physicalMargin;
                        targetPhysicalY = selected.Info.rcWork.Top + physicalMargin;
                        break;
                    case GoalOverlay.Models.ScreenCorner.BottomLeft:
                        targetPhysicalX = selected.Info.rcWork.Left + physicalMargin;
                        targetPhysicalY = selected.Info.rcWork.Bottom - physicalHeight - physicalMargin;
                        break;
                    case GoalOverlay.Models.ScreenCorner.BottomRight:
                        targetPhysicalX = selected.Info.rcWork.Right - physicalWidth - physicalMargin;
                        targetPhysicalY = selected.Info.rcWork.Bottom - physicalHeight - physicalMargin;
                        break;
                    case GoalOverlay.Models.ScreenCorner.TopRight:
                    default:
                        targetPhysicalX = selected.Info.rcWork.Right - physicalWidth - physicalMargin;
                        targetPhysicalY = selected.Info.rcWork.Top + physicalMargin;
                        break;
                }

                targetPhysicalX = Math.Max(selected.Info.rcWork.Left, targetPhysicalX);
                targetPhysicalY = Math.Max(selected.Info.rcWork.Top, targetPhysicalY);

                // Di chuyển HWND trực tiếp bằng Win32 SetWindowPos - tức thì và không bị WPF clamp giới hạn màn hình
                NativeMethods.SetWindowPos(
                    _hWnd,
                    NativeMethods.HWND_TOPMOST,
                    targetPhysicalX,
                    targetPhysicalY,
                    0, 0,
                    NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);

                // Cập nhật Left, Top trong WPF (đơn vị DIPs)
                Rect workArea = OverlayPositionCalculator.ConvertPhysicalWorkAreaToDips(
                    selected.Info.rcWork.Left,
                    selected.Info.rcWork.Top,
                    selected.Info.rcWork.Right,
                    selected.Info.rcWork.Bottom,
                    selected.DpiScaleX,
                    selected.DpiScaleY);

                Point targetPos = OverlayPositionCalculator.CalculatePosition(
                    workArea,
                    ActualWidth,
                    ActualHeight,
                    settings.Corner);

                Left = targetPos.X;
                Top = targetPos.Y;
            }
            else
            {
                Rect workArea = SystemParameters.WorkArea;
                Point targetPos = OverlayPositionCalculator.CalculatePosition(
                    workArea,
                    ActualWidth,
                    ActualHeight,
                    settings.Corner);

                Left = targetPos.X;
                Top = targetPos.Y;
                NativeMethods.SetTopmost(_hWnd);
            }
        }
        catch (Exception ex)
        {
            App.Logger.Error("Lỗi khi căn chỉnh vị trí OverlayWindow", ex);
        }
    }

    /// <summary>
    /// Tự động căn lại vị trí khi kích thước cửa sổ co giãn theo nội dung
    /// </summary>
    private void OverlayWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        RepositionWindow();
    }

    /// <summary>
    /// Tự động căn lại vị trí khi DPI của màn hình thay đổi (PerMonitorV2)
    /// </summary>
    private void OverlayWindow_DpiChanged(object sender, DpiChangedEventArgs e)
    {
        RepositionWindow();
    }

    /// <summary>
    /// Tự động căn lại vị trí khi đổi độ phân giải màn hình hoặc cắm/rút màn hình
    /// </summary>
    private void SystemEvents_DisplaySettingsChanged(object? sender, EventArgs e)
    {
        Dispatcher.InvokeAsync(RepositionWindow);
    }

    /// <summary>
    /// Tự động làm mới dữ liệu và căn lại vị trí khi máy thức dậy từ chế độ Sleep
    /// </summary>
    private async void SystemEvents_PowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
        {
            App.Logger.Info("Hệ điều hành thức dậy sau Sleep (PowerModes.Resume). Làm mới OverlayWindow.");
            await Dispatcher.InvokeAsync(async () =>
            {
                await ViewModel.RefreshCommand.ExecuteAsync(null);
                RepositionWindow();
                NativeMethods.SetTopmost(_hWnd);
            });
        }
    }

    /// <summary>
    /// Định kỳ 30 giây: gọi lại SetTopmost để củng cố Z-Order và kiểm tra bước qua 00:00 nửa đêm
    /// </summary>
    private async void TopmostTimer_Tick(object? sender, EventArgs e)
    {
        try
        {
            // Củng cố vị trí nổi trên cùng mà không chiếm focus
            if (_hWnd != IntPtr.Zero)
            {
                NativeMethods.SetTopmost(_hWnd);
            }

            // Kiểm tra nếu đã bước qua ngày mới (sau 00:00)
            if (DateTime.Today != _lastCheckedDate)
            {
                _lastCheckedDate = DateTime.Today;
                App.Logger.Info("Phát hiện chuyển sang ngày mới (qua 00:00). Tự động chuyển tiếp mục tiêu và làm mới HUD.");
                await ViewModel.RefreshCommand.ExecuteAsync(null);
            }
        }
        catch (Exception ex)
        {
            App.Logger.Error("Lỗi trong timer định kỳ của OverlayWindow", ex);
        }
    }

    private void OverlayWindow_Closed(object? sender, EventArgs e)
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _topmostTimer.Stop();

        // Hủy đăng ký phím tắt toàn cục
        if (_hWnd != IntPtr.Zero)
        {
            App.HotkeyService.Unregister(_hWnd);
        }

        // Hủy đăng ký nhận tin nhắn Messenger
        WeakReferenceMessenger.Default.Unregister<SettingsChangedMessage>(this);

        // Hủy đăng ký sự kiện tĩnh để tránh rò rỉ tài nguyên
        SystemEvents.DisplaySettingsChanged -= SystemEvents_DisplaySettingsChanged;
        SystemEvents.PowerModeChanged -= SystemEvents_PowerModeChanged;
    }
}
