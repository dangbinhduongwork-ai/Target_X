using System;
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

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = ViewModel;

        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.InitializeCommand.ExecuteAsync(null);
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
