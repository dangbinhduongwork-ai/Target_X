using System;
using System.Windows;
using GoalOverlay.Services;
using GoalOverlay.ViewModels;
using GoalOverlay.Views;

namespace GoalOverlay;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public static ILoggerService Logger { get; } = new LoggerService();
    public static IDataService DataService { get; } = new JsonDataService(Logger);
    public static ISettingsService SettingsService { get; } = new SettingsService(Logger);
    public static IStartupService StartupService { get; } = new WindowsStartupService(Logger);

    public static OverlayViewModel? OverlayViewModel { get; private set; }
    public static OverlayWindow? OverlayWindow { get; private set; }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Bắt lỗi toàn cục cấp AppDomain
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                Logger.Error("Ngoại lệ chưa được xử lý cấp AppDomain", ex);
            }
        };

        // Bắt lỗi toàn cục cấp Dispatcher (UI Thread)
        DispatcherUnhandledException += (s, args) =>
        {
            Logger.Error("Ngoại lệ chưa được xử lý cấp UI Dispatcher", args.Exception);
            args.Handled = true;
        };

        Logger.Info("=== Ứng dụng GoalOverlay khởi động (Giai đoạn 2: Overlay HUD & Win32) ===");

        try
        {
            // Nạp cấu hình và dữ liệu ban đầu từ %APPDATA%
            await SettingsService.LoadSettingsAsync();
            var goals = await DataService.LoadGoalsAsync();
            Logger.Info($"Khởi tạo dữ liệu thành công. Đã nạp {goals.Count} mục tiêu từ AppData.");

            // Khởi tạo và hiển thị cửa sổ Overlay HUD ghim góc trên bên phải màn hình
            OverlayViewModel = new OverlayViewModel(DataService, SettingsService, Logger);
            OverlayWindow = new OverlayWindow(OverlayViewModel);
            OverlayWindow.Show();
            Logger.Info("Đã hiển thị thành công cửa sổ OverlayWindow (HUD).");
        }
        catch (Exception ex)
        {
            Logger.Error("Lỗi trong quá trình khởi tạo Overlay HUD", ex);
        }

        // Hiển thị cửa sổ kiểm tra / quản lý
        var mainWindow = new MainWindow();
        mainWindow.Show();
    }
}
