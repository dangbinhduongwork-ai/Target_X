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
    public static ISettingsService SettingsService { get; } = new SettingsService(Logger);
    public static IGoalRepository GoalRepository { get; } = new JsonGoalRepository(Logger);
    public static IClock Clock { get; } = new SystemClock();
    public static IGoalService GoalService { get; } = new GoalService(GoalRepository, Clock, Logger);
    public static IStartupService StartupService { get; } = new WindowsStartupService(Logger);

    public static OverlayWindow? OverlayWindow { get; private set; }

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

        Logger.Info("=== Ứng dụng GoalOverlay khởi động (Giai đoạn 3: Overlay HUD) ===");

        try
        {
            await SettingsService.LoadSettingsAsync();

            var overlayVm = new OverlayViewModel(GoalService, Clock, Logger);
            OverlayWindow = new OverlayWindow(overlayVm);
            OverlayWindow.Show();

            Logger.Info("Đã khởi tạo và hiển thị thành công cửa sổ OverlayWindow (HUD).");
        }
        catch (Exception ex)
        {
            Logger.Error("Lỗi trong quá trình khởi tạo ứng dụng", ex);
        }
    }
}
