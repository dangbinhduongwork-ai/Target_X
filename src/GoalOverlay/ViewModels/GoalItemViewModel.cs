using CommunityToolkit.Mvvm.ComponentModel;
using GoalOverlay.Models;

namespace GoalOverlay.ViewModels;

/// <summary>
/// Wrapper quanh Model Goal bổ sung trạng thái sửa nhanh (In-place editing) trên giao diện MainWindow
/// </summary>
public partial class GoalItemViewModel : ObservableObject
{
    public Goal Model { get; }

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _editText = string.Empty;

    public GoalItemViewModel(Goal model)
    {
        Model = model;
        _editText = model.Text;
    }

    public string Text
    {
        get => Model.Text;
        set
        {
            if (Model.Text != value)
            {
                Model.Text = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsDone
    {
        get => Model.IsDone;
        set
        {
            if (Model.IsDone != value)
            {
                Model.IsDone = value;
                OnPropertyChanged();
            }
        }
    }
}
