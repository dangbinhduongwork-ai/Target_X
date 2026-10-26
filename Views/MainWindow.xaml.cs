using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace GoalOverlay.Views;

/// <summary>
/// Interaction logic for MainWindow.xaml (Giai đoạn 1: Xác thực hạ tầng)
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await RefreshStatusAsync();
    }

    private async Task RefreshStatusAsync()
    {
        try
        {
            TxtDataPath.Text = App.DataService.DataFilePath;
            TxtLogPath.Text = App.Logger.LogsDirectory;

            var goals = await App.DataService.LoadGoalsAsync();
            TxtGoalsStatus.Text = $"Đã tải {goals.Count} mục tiêu hợp lệ. (File: {App.DataService.DataFilePath})";

            bool isAutoStart = App.StartupService.IsAutoStartEnabled();
            TxtStartupStatus.Text = isAutoStart ? "Đang BẬT (Registry: HKCU\\...\\Run\\GoalOverlay)" : "Đang TẮT";
        }
        catch (Exception ex)
        {
            App.Logger.Error("Lỗi khi cập nhật trạng thái UI MainWindow", ex);
            TxtActionResult.Text = $"Lỗi: {ex.Message}";
        }
    }

    private void BtnOpenAppData_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string targetFolder = Path.Combine(appData, "GoalOverlay");

            if (Directory.Exists(targetFolder))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = targetFolder,
                    UseShellExecute = true
                });
                TxtActionResult.Text = $"Đã mở thư mục: {targetFolder}";
            }
            else
            {
                TxtActionResult.Text = "Thư mục chưa được tạo.";
            }
        }
        catch (Exception ex)
        {
            App.Logger.Error("Lỗi khi mở thư mục AppData", ex);
            TxtActionResult.Text = $"Lỗi khi mở thư mục: {ex.Message}";
        }
    }

    private void BtnTestLog_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            App.Logger.Info("Người dùng nhấn nút 'Ghi Log Thử nghiệm' từ giao diện MainWindow.");
            App.Logger.Warn("Cảnh báo thử nghiệm: LoggerService hoạt động ổn định và chính xác.");
            TxtActionResult.Text = $"[OK] Đã ghi log thành công vào: {App.Logger.LogsDirectory}";
        }
        catch (Exception ex)
        {
            TxtActionResult.Text = $"Lỗi ghi log: {ex.Message}";
        }
    }

    private async void BtnToggleStartup_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            bool current = App.StartupService.IsAutoStartEnabled();
            bool result = App.StartupService.SetAutoStart(!current);

            if (result)
            {
                await RefreshStatusAsync();
                TxtActionResult.Text = $"Đã chuyển trạng thái tự khởi động thành: {(!current ? "BẬT" : "TẮT")}";
            }
            else
            {
                TxtActionResult.Text = "Không thể thay đổi cài đặt Registry.";
            }
        }
        catch (Exception ex)
        {
            App.Logger.Error("Lỗi khi đổi trạng thái AutoStart", ex);
            TxtActionResult.Text = $"Lỗi: {ex.Message}";
        }
    }

    private void BtnToggleOverlay_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (App.OverlayWindow != null)
            {
                if (App.OverlayWindow.IsVisible)
                {
                    App.OverlayWindow.Hide();
                    TxtActionResult.Text = "Đã ẩn cửa sổ Overlay HUD.";
                }
                else
                {
                    App.OverlayWindow.Show();
                    TxtActionResult.Text = "Đã hiển thị cửa sổ Overlay HUD ở góc trên bên phải.";
                }
            }
        }
        catch (Exception ex)
        {
            App.Logger.Error("Lỗi khi chuyển đổi hiển thị Overlay HUD", ex);
            TxtActionResult.Text = $"Lỗi: {ex.Message}";
        }
    }
}
