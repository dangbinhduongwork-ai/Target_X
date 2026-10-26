using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Win32;
using GoalOverlay.Native;
using GoalOverlay.ViewModels;

namespace GoalOverlay.Views;

/// <summary>
/// Interaction logic for OverlayWindow.xaml
/// Cửa sổ HUD Overlay ghim góc trên bên phải màn hình, quản lý Win32 NoActivate/ClickThrough và tự làm mới
/// </summary>
public partial class OverlayWindow : Window
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
        CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Register<SettingsChangedMessage>(this, (r, msg) =>
        {
            if (_hWnd != IntPtr.Zero)
            {
                NativeMethods.SetClickThrough(_hWnd, msg.Settings.ClickThrough);
                Dispatcher.InvokeAsync(RepositionWindow);
            }
        });
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
        }
        catch (Exception ex)
        {
            App.Logger.Error("Lỗi khi cấu hình cờ Win32 cho OverlayWindow", ex);
        }
    }

    private async void OverlayWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await ViewModel.LoadGoalsCommand.ExecuteAsync(null);
            RepositionWindow();
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
    public void RepositionWindow()
    {
        if (_hWnd == IntPtr.Zero || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        try
        {
            int monitorIndex = App.SettingsService.CurrentSettings.MonitorIndex;
            Rect workArea = NativeMethods.GetMonitorWorkArea(monitorIndex, this);

            // Cách mép phải 16px, cách mép trên 16px
            const double margin = 16.0;

            double targetLeft = workArea.Right - ActualWidth - margin;
            double targetTop = workArea.Top + margin;

            Left = Math.Max(workArea.Left, targetLeft);
            Top = Math.Max(workArea.Top, targetTop);
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

        // Hủy đăng ký sự kiện tĩnh để tránh rò rỉ tài nguyên
        SystemEvents.DisplaySettingsChanged -= SystemEvents_DisplaySettingsChanged;
        SystemEvents.PowerModeChanged -= SystemEvents_PowerModeChanged;
    }
}
