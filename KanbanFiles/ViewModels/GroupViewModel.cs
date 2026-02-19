using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace KanbanFiles.ViewModels;

public partial class GroupViewModel : BaseViewModel
{
    [ObservableProperty]
    private string _name = string.Empty;

    public ObservableCollection<KanbanItemViewModel> Items { get; } = new();
    public ObservableCollection<TagDefinition> Tags { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ItemCount))]
    private bool _isCollapsed;

    [ObservableProperty]
    private bool _isVisible = true;

    public int ItemCount => Items.Count;

    private readonly TagService? _tagService;
    private readonly Board? _board;
    private readonly string _columnFolderName;

    public event EventHandler? RenameRequested;
    public event EventHandler? DeleteRequested;

    public GroupViewModel(string name, TagService? tagService = null, Board? board = null, string columnFolderName = "")
    {
        Name = name;
        _tagService = tagService;
        _board = board;
        _columnFolderName = columnFolderName;
        Items.CollectionChanged += OnItemsCollectionChanged;

        LoadTags();
    }

    public void LoadTags()
    {
        Tags.Clear();
        if (_tagService == null || _board == null) return;

        List<string> tagNames = _tagService.GetTagsForGroup(_board, _columnFolderName, Name);
        List<TagDefinition> definitions = _tagService.GetTagDefinitions(_board);

        foreach (string tagName in tagNames)
        {
            TagDefinition? def = definitions.FirstOrDefault(d => d.Name == tagName);
            if (def != null)
            {
                Tags.Add(def);
            }
        }
    }

    public async Task ToggleTagAsync(string tagName)
    {
        if (_tagService == null || _board == null) return;

        await _tagService.ToggleGroupTagAsync(_board, _columnFolderName, Name, tagName);
        LoadTags();
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
