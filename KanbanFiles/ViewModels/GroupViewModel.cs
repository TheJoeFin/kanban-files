using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace KanbanFiles.ViewModels;

public partial class GroupViewModel : BaseViewModel
{
    [ObservableProperty]
    private string _name = string.Empty;

    public ObservableCollection<KanbanItemViewModel> Items { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ItemCount))]
    private bool _isCollapsed;

    public int ItemCount => Items.Count;

    public event EventHandler? RenameRequested;
    public event EventHandler? DeleteRequested;

    public GroupViewModel(string name)
    {
        Name = name;
        Items.CollectionChanged += OnItemsCollectionChanged;
    }

    [RelayCommand]
    public void ToggleCollapse()
    {
        IsCollapsed = !IsCollapsed;
    }

    [RelayCommand]
    public void Rename()
    {
        RenameRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    public void Delete()
    {
        DeleteRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(ItemCount));
    }
}
