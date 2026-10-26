using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using GoalOverlay.Models;
using GoalOverlay.Native;
using GoalOverlay.Services;
using GoalOverlay.ViewModels;

namespace GoalOverlay.Views;

/// <summary>
/// Interaction logic for ReminderWindow.xaml
/// Cửa sổ nhắc nhở mục tiêu đến hạn dạng HUD nổi bật:
/// - WS_EX_NOACTIVATE: không cướp focus bàn phím của người dùng
/// - Chống bấm nhầm (Safety Guard): khóa phím Enter/Space trong 1.5 giây đầu
/// - Căn giữa màn hình WorkArea theo chuẩn DPI
/// - Quản lý Singleton: nếu cửa sổ đang mở, các mục mới được thêm vào danh sách thay vì mở cửa sổ khác
/// </summary>
public partial class ReminderWindow : Window
{
    public static ReminderWindow? Current { get; private set; }

    public ReminderWindowViewModel ViewModel { get; }

    private readonly AppSettings _settings;
    private readonly DispatcherTimer _safetyTimer;
    private bool _isInputBlocked = true;
    private IntPtr _hWnd;

    public ReminderWindow(ReminderWindowViewModel viewModel, AppSettings settings)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = ViewModel;
        _settings = settings;

        ViewModel.RequestClose += OnViewModelRequestClose;

        // Cơ chế chống bấm nhầm: Vô hiệu hóa phím Enter/Space trong 1.5 giây đầu
        _safetyTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1500)
        };
        _safetyTimer.Tick += (s, e) =>
        {
            _isInputBlocked = false;
            _safetyTimer.Stop();
        };
        _safetyTimer.Start();

        SourceInitialized += ReminderWindow_SourceInitialized;
        Loaded += ReminderWindow_Loaded;
        Closed += ReminderWindow_Closed;
        PreviewKeyDown += ReminderWindow_PreviewKeyDown;
    }

    /// <summary>
    /// Hiển thị cửa sổ nhắc nhở hoặc gom thêm mục tiêu mới vào cửa sổ đang mở
    /// </summary>
    public static void ShowOrAddGoals(
        IEnumerable<Goal> goals,
        AppSettings settings,
        IReminderService reminderService,
        IClock clock)
    {
        var goalList = goals.ToList();
        if (goalList.Count == 0) return;

        Application.Current?.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
        {
            if (Current != null && Current.IsLoaded)
            {
                Current.ViewModel.AddGoals(goalList);
                Current.FlashAndPlaySound(settings);
            }
            else
            {
                var vm = new ReminderWindowViewModel(reminderService, clock, goalList, settings.SnoozeOptionsMinutes);
                Current = new ReminderWindow(vm, settings);
                Current.Show();
            }
        }));
    }

    private void ReminderWindow_SourceInitialized(object? sender, EventArgs e)
    {
        try
        {
            _hWnd = new WindowInteropHelper(this).Handle;

            // 1. Áp dụng cờ WS_EX_NOACTIVATE (không cướp focus khi người dùng đang gõ phím ở ứng dụng khác)
            NativeMethods.SetNoActivateOnly(_hWnd);

            // 2. Thiết lập Topmost trên cùng Z-Order
            NativeMethods.SetTopmost(_hWnd);

            // 3. Phát âm thanh & Nhấp nháy Taskbar
            FlashAndPlaySound(_settings);
        }
        catch { }
    }

    private void ReminderWindow_Loaded(object sender, RoutedEventArgs e)
    {
        CenterOnWorkArea();
    }

    /// <summary>
    /// Căn giữa màn hình WorkArea của màn hình được chọn theo chuẩn DPI
    /// </summary>
    private void CenterOnWorkArea()
    {
        try
        {
            Rect workArea = NativeMethods.GetMonitorWorkArea(_settings.MonitorIndex, this);
            Left = workArea.Left + Math.Max(0, (workArea.Width - ActualWidth) / 2);
            Top = workArea.Top + Math.Max(0, (workArea.Height - ActualHeight) / 2);
        }
        catch
        {
            // Fallback căn giữa màn hình chính nếu có lỗi đọc đa màn hình
            Left = (SystemParameters.WorkArea.Width - ActualWidth) / 2;
            Top = (SystemParameters.WorkArea.Height - ActualHeight) / 2;
        }
    }

    public void FlashAndPlaySound(AppSettings settings)
    {
        try
        {
            if (_hWnd != IntPtr.Zero)
            {
                NativeMethods.FlashWindow(_hWnd);
            }

            if (settings.ReminderSoundEnabled)
            {
                GoalOverlay.Services.SoundHelper.PlayNotificationChime();
            }
        }
        catch { }
    }

    private void ReminderWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Vô hiệu hóa phím Enter và Space trong 1.5 giây đầu tiên sau khi cửa sổ hiện ra
        if (_isInputBlocked && (e.Key == Key.Enter || e.Key == Key.Space))
        {
            e.Handled = true;
        }
    }

    private void SnoozeButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.ContextMenu != null)
        {
            btn.ContextMenu.PlacementTarget = btn;
            btn.ContextMenu.Placement = PlacementMode.Bottom;
            btn.ContextMenu.IsOpen = true;
        }
    }

    private void OnViewModelRequestClose()
    {
        Close();
    }

    private void ReminderWindow_Closed(object? sender, EventArgs e)
    {
        ViewModel.RequestClose -= OnViewModelRequestClose;
        _safetyTimer.Stop();

        if (ReferenceEquals(Current, this))
        {
            Current = null;
        }
    }
}
