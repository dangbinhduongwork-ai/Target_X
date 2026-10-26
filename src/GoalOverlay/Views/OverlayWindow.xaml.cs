using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using GoalOverlay.Native;
using GoalOverlay.ViewModels;

namespace GoalOverlay.Views;

/// <summary>
/// Interaction logic for OverlayWindow.xaml
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

    private void OverlayWindow_SourceInitialized(object? sender, EventArgs e)
    {
        try
        {
            IntPtr hWnd = new WindowInteropHelper(this).Handle;
            Win32Methods.MakeToolWindow(hWnd);
            Win32Methods.ReinforceTopmost(hWnd);
        }
        catch
        {
            // Bỏ qua nếu lỗi Win32 style
        }
    }

    private async void OverlayWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var (left, top) = DpiHelper.CalculateTopRightPosition(ActualWidth, marginX: 20, marginY: 20);
            Left = left;
            Top = top;

            await ViewModel.LoadGoalsCommand.ExecuteAsync(null);
        }
        catch
        {
            // Bỏ qua lỗi nạp dữ liệu ban đầu
        }
    }

    private void OverlayWindow_Deactivated(object? sender, EventArgs e)
    {
        try
        {
            Topmost = true;
            IntPtr hWnd = new WindowInteropHelper(this).Handle;
            Win32Methods.ReinforceTopmost(hWnd);
        }
        catch
        {
            // Bỏ qua
        }
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!ViewModel.IsLocked && e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void OverlayWindow_LocationChanged(object? sender, EventArgs e)
    {
        // Lưu vị trí khi cần
    }

    private void BtnOpenManager_Click(object sender, RoutedEventArgs e)
    {
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
}
