using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GoalOverlay.ViewModels;

namespace GoalOverlay.Views;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; }

    private Point _dragStartPoint;
    private GoalItemViewModel? _draggedItem;
    private ListBox? _activeDragSource;
    private bool _isExplicitExit = false;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = ViewModel;

        Loaded += MainWindow_Loaded;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        try
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            int trueValue = 1;
            // Kích hoạt giao diện tối Immersive Dark Mode trên Windows 10/11 cho viền và drop shadow
            DwmSetWindowAttribute(hwnd, 20, ref trueValue, sizeof(int));
            DwmSetWindowAttribute(hwnd, 19, ref trueValue, sizeof(int));
        }
        catch { }
    }

    #region Điều khiển thanh tiêu đề Custom Dark TitleBar

    private void TitleBar_Minimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void TitleBar_Maximize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        UpdateMaximizeButtonIcon();
    }

    private void TitleBar_Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void UpdateMaximizeButtonIcon()
    {
        if (BtnTitleBarMax != null)
        {
            BtnTitleBarMax.Content = WindowState == WindowState.Maximized ? "❐" : "⬜";
            BtnTitleBarMax.ToolTip = WindowState == WindowState.Maximized ? "Khôi phục kích thước" : "Phóng to";
        }
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        UpdateMaximizeButtonIcon();
    }

    #endregion

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.InitializeCommand.ExecuteAsync(null);
    }

    /// <summary>
    /// Khi người dùng nhấn nút X đỏ, cửa sổ chỉ ẩn xuống khay hệ thống (Tray) thay vì thoát ứng dụng
    /// </summary>
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        base.OnClosing(e);

        if (!_isExplicitExit)
        {
            e.Cancel = true;
            Hide();
        }
    }

    /// <summary>
    /// Khôi phục cửa sổ và đưa lên trên cùng khi được gọi từ Tray Menu hoặc phím tắt
    /// </summary>
    public void ShowAndActivate()
    {
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Show();
        Activate();
        Focus();
    }

    /// <summary>
    /// Chuyển trực tiếp sang tab Mục tiêu Lặp lại theo tuần
    /// </summary>
    public void SelectRecurringTab()
    {
        MainTabs.SelectedIndex = 2;
    }

    private void NavToRecurringTab_Click(object sender, RoutedEventArgs e)
    {
        SelectRecurringTab();
    }

    /// <summary>
    /// Chuyển trực tiếp sang tab Cài đặt
    /// </summary>
    public void SelectSettingsTab()
    {
        MainTabs.SelectedIndex = 3;
    }

    /// <summary>
    /// Mở giao diện quản lý và focus vào ô nhập mục tiêu hôm nay
    /// </summary>
    public void OpenAndFocusDailyGoalInput()
    {
        ShowAndActivate();
        MainTabs.SelectedIndex = 0;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(() =>
        {
            TxtDailyInput?.Focus();
        }));
    }

    /// <summary>
    /// Mở giao diện quản lý và focus vào ô nhập mục tiêu tháng này
    /// </summary>
    public void OpenAndFocusMonthlyGoalInput()
    {
        ShowAndActivate();
        MainTabs.SelectedIndex = 1;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(() =>
        {
            TxtMonthlyInput?.Focus();
        }));
    }

    /// <summary>
    /// Mở giao diện quản lý và chuyển sang tab Cài đặt
    /// </summary>
    public void OpenSettingsTab()
    {
        ShowAndActivate();
        SelectSettingsTab();
    }

    /// <summary>
    /// Cho phép đóng hoàn toàn cửa sổ khi người dùng bấm Thoát từ Tray Icon
    /// </summary>
    public void CloseExplicitly()
    {
        _isExplicitExit = true;
        Close();
    }

    // --- Xử lý phím Enter thêm nhanh mục tiêu ---
    private void TxtDailyInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ViewModel.AddDailyGoalCommand.Execute(null);
        }
    }

    private void TxtMonthlyInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ViewModel.AddMonthlyGoalCommand.Execute(null);
        }
    }

    // --- Xử lý Sửa nhanh (In-place edit qua double-click) ---
    private void GoalText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && sender is FrameworkElement element && element.DataContext is GoalItemViewModel item)
        {
            ViewModel.StartEdit(item);
        }
    }

    private void EditGoalTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is TextBox textBox && textBox.DataContext is GoalItemViewModel item)
        {
            if (e.Key == Key.Enter)
            {
                _ = ViewModel.SaveEditCommand.ExecuteAsync(item);
            }
            else if (e.Key == Key.Escape)
            {
                ViewModel.CancelEdit(item);
            }
        }
    }

    private void EditGoalTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox && textBox.DataContext is GoalItemViewModel item)
        {
            _ = ViewModel.SaveEditCommand.ExecuteAsync(item);
        }
    }

    // --- Xử lý Kéo thả sắp xếp thứ tự (Drag & Drop Reorder) ---
    private void ListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
        if (sender is ListBox listBox)
        {
            _activeDragSource = listBox;
            _draggedItem = GetItemUnderMouse(listBox, e.GetPosition(listBox));
        }
    }

    private void ListBox_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && _draggedItem != null && _activeDragSource != null)
        {
            Point mousePos = e.GetPosition(null);
            Vector diff = _dragStartPoint - mousePos;

            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                DragDrop.DoDragDrop(_activeDragSource, _draggedItem, DragDropEffects.Move);
                _draggedItem = null;
                _activeDragSource = null;
            }
        }
    }

    private async void ListDailyGoals_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(GoalItemViewModel)) is GoalItemViewModel droppedItem)
        {
            int oldIndex = ViewModel.TodayGoals.IndexOf(droppedItem);
            if (oldIndex < 0) return;

            int targetIndex = GetDropTargetIndex(ListDailyGoals, e.GetPosition(ListDailyGoals), ViewModel.TodayGoals.Count);
            if (targetIndex >= 0 && targetIndex != oldIndex)
            {
                await ViewModel.ReorderDailyGoalsAsync(oldIndex, targetIndex);
            }
        }
    }

    private async void ListMonthlyGoals_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(GoalItemViewModel)) is GoalItemViewModel droppedItem)
        {
            int oldIndex = ViewModel.MonthGoals.IndexOf(droppedItem);
            if (oldIndex < 0) return;

            int targetIndex = GetDropTargetIndex(ListMonthlyGoals, e.GetPosition(ListMonthlyGoals), ViewModel.MonthGoals.Count);
            if (targetIndex >= 0 && targetIndex != oldIndex)
            {
                await ViewModel.ReorderMonthGoalsAsync(oldIndex, targetIndex);
            }
        }
    }

    private static GoalItemViewModel? GetItemUnderMouse(ListBox listBox, Point position)
    {
        var element = listBox.InputHitTest(position) as DependencyObject;
        while (element != null && element != listBox)
        {
            if (element is ListBoxItem item && item.DataContext is GoalItemViewModel vm)
            {
                return vm;
            }
            element = VisualTreeHelper.GetParent(element);
        }
        return null;
    }

    private static int GetDropTargetIndex(ListBox listBox, Point position, int totalCount)
    {
        for (int i = 0; i < totalCount; i++)
        {
            if (listBox.ItemContainerGenerator.ContainerFromIndex(i) is ListBoxItem item)
            {
                Point itemPos = item.TransformToAncestor(listBox).Transform(new Point(0, 0));
                if (position.Y < itemPos.Y + item.ActualHeight)
                {
                    return i;
                }
            }
        }
        return totalCount - 1;
    }
}
