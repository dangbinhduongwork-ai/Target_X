using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using GoalOverlay.Native;
using GoalOverlay.ViewModels;

namespace GoalOverlay.Views;

/// <summary>
/// Interaction logic for OverlayWindow.xaml
/// Cửa sổ HUD Overlay ghim góc trên bên phải màn hình, tích hợp Win32 API và hỗ trợ DPI
/// </summary>
public partial class OverlayWindow : Window
{
    public OverlayViewModel ViewModel { get; }

    public OverlayWindow(OverlayViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = ViewModel;

        SourceInitialized += OverlayWindow_SourceInitialized;
        Loaded += OverlayWindow_Loaded;
        Deactivated += OverlayWindow_Deactivated;
        LocationChanged += OverlayWindow_LocationChanged;
    }

    /// <summary>
    /// Khi Handle của Window được tạo, áp dụng các cờ Win32 cấp hệ thống
    /// </summary>
    private void OverlayWindow_SourceInitialized(object? sender, EventArgs e)
    {
        try
        {
            IntPtr hWnd = new WindowInteropHelper(this).Handle;

            // 1. Áp dụng cờ WS_EX_TOOLWINDOW: ẩn hoàn toàn cửa sổ khỏi Alt+Tab và Taskbar
            Win32Methods.MakeToolWindow(hWnd);

            // 2. Ghim cửa sổ ở vị trí Z-order cao nhất (Topmost tuyệt đối)
            Win32Methods.ReinforceTopmost(hWnd);
        }
        catch (Exception ex)
        {
            App.Logger.Error("Lỗi trong quá trình khởi tạo Win32 styles cho OverlayWindow", ex);
        }
    }

    /// <summary>
    /// Khi cửa sổ nạp xong, tính toán vị trí góc trên bên phải màn hình dựa trên DpiHelper
    /// </summary>
    private async void OverlayWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var settings = App.SettingsService.CurrentSettings;

            // Nếu người dùng chưa từng kéo vị trí tùy chỉnh (WindowLeft < 0), tự động ghim góc trên bên phải
            if (settings.WindowLeft < 0)
            {
                var (left, top) = DpiHelper.CalculateTopRightPosition(ActualWidth, marginX: 20, marginY: 20);
                Left = left;
                Top = top;
            }
            else
            {
                Left = settings.WindowLeft;
                Top = settings.WindowTop;
            }

            // Nạp danh sách mục tiêu
            await ViewModel.LoadGoalsCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            App.Logger.Error("Lỗi khi định vị hoặc nạp dữ liệu cho OverlayWindow", ex);
        }
    }

    /// <summary>
    /// Khi ứng dụng khác được focus, gia cố lại thuộc tính Topmost để đảm bảo HUD không bị che khuất
    /// </summary>
    private void OverlayWindow_Deactivated(object? sender, EventArgs e)
    {
        try
        {
            Topmost = true;
            IntPtr hWnd = new WindowInteropHelper(this).Handle;
            Win32Methods.ReinforceTopmost(hWnd);
        }
        catch (Exception ex)
        {
            App.Logger.Error("Lỗi khi củng cố cờ Topmost của OverlayWindow", ex);
        }
    }

    /// <summary>
    /// Cho phép người dùng kéo thả cửa sổ khi chưa bật chế độ khóa vị trí (IsLocked = false)
    /// </summary>
    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!ViewModel.IsLocked && e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    /// <summary>
    /// Lưu lại tọa độ khi người dùng kéo di chuyển cửa sổ
    /// </summary>
    private void OverlayWindow_LocationChanged(object? sender, EventArgs e)
    {
        if (!ViewModel.IsLocked && IsLoaded)
        {
            App.SettingsService.CurrentSettings.WindowLeft = Left;
            App.SettingsService.CurrentSettings.WindowTop = Top;
            _ = App.SettingsService.SaveSettingsAsync(App.SettingsService.CurrentSettings);
        }
    }

    /// <summary>
    /// Mở hoặc kích hoạt cửa sổ Quản lý mục tiêu chính
    /// </summary>
    private void BtnOpenManager_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Tìm cửa sổ MainWindow hiện tại hoặc hiển thị lại nếu đang ẩn
            foreach (Window win in Application.Current.Windows)
            {
                if (win is MainWindow mainWindow)
                {
                    if (mainWindow.WindowState == WindowState.Minimized)
                    {
                        mainWindow.WindowState = WindowState.Normal;
                    }
                    mainWindow.Show();
                    mainWindow.Activate();
                    return;
                }
            }

            var newMainWindow = new MainWindow();
            newMainWindow.Show();
        }
        catch (Exception ex)
        {
            App.Logger.Error("Lỗi khi mở giao diện Quản lý từ OverlayWindow", ex);
        }
    }
}
