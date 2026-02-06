using System.Collections.ObjectModel;
using KanbanFiles.Models;
using KanbanFiles.Services;

namespace KanbanFiles.ViewModels;

public partial class ColumnViewModel : BaseViewModel
{
    private readonly FileSystemService _fileSystemService;
    private readonly BoardConfigService _boardConfigService;
    private readonly Board _board;

    [ObservableProperty]
    private string _name = string.Empty;

    public ObservableCollection<KanbanItemViewModel> Items { get; } = new();

    public string FolderPath { get; }

    public event EventHandler<string>? DeleteRequested;
    public event EventHandler? AddItemRequested;
    public event EventHandler<string>? RenameRequested;

    public ColumnViewModel(Column column, FileSystemService fileSystemService, BoardConfigService boardConfigService, Board board)
    {
        Name = column.Name;
        FolderPath = column.FolderPath;
        _fileSystemService = fileSystemService;
        _boardConfigService = boardConfigService;
        _board = board;

        foreach (var item in column.Items)
        {
            var itemViewModel = new KanbanItemViewModel(item, _fileSystemService, _boardConfigService, _board, this);
            Items.Add(itemViewModel);
        }
    }

    [RelayCommand]
    public void AddItem()
    {
        AddItemRequested?.Invoke(this, EventArgs.Empty);
    }

    public async Task CreateItemAsync(string title)
    {
        var filePath = await _fileSystemService.CreateItemAsync(FolderPath, title);
        var fileName = Path.GetFileName(filePath);

        // Update item order in config
        var columnConfig = _board.Columns.FirstOrDefault(c => Path.Combine(_board.RootPath, c.FolderName) == FolderPath);
        if (columnConfig != null)
        {
            columnConfig.ItemOrder.Add(fileName);
            await _boardConfigService.SaveAsync(_board);
        }

        // Read the newly created item
        var items = await _fileSystemService.EnumerateItemsAsync(FolderPath);
        var newItem = items.FirstOrDefault(i => i.FileName == fileName);
        if (newItem != null)
        {
            var itemViewModel = new KanbanItemViewModel(newItem, _fileSystemService, _boardConfigService, _board, this);
            Items.Add(itemViewModel);
        }
    }

    [RelayCommand]
    public void RenameColumn()
    {
        RenameRequested?.Invoke(this, Name);
    }

    public async Task RenameColumnAsync(string newName)
    {
        var sanitizedName = SanitizeFolderName(newName);
        var oldFolderPath = FolderPath;
        var newFolderPath = Path.Combine(Path.GetDirectoryName(FolderPath)!, sanitizedName);

        // Rename folder
        await Task.Run(() => Directory.Move(oldFolderPath, newFolderPath));

        // Update config
        var columnConfig = _board.Columns.FirstOrDefault(c => Path.Combine(_board.RootPath, c.FolderName) == Path.GetFileName(oldFolderPath));
        if (columnConfig != null)
        {
            columnConfig.FolderName = sanitizedName;
            columnConfig.DisplayName = newName;
            await _boardConfigService.SaveAsync(_board);
        }

        Name = newName;
    }

    [RelayCommand]
    public void DeleteColumn()
    {
        DeleteRequested?.Invoke(this, FolderPath);
    }

    public async Task DeleteColumnAsync()
    {
        // Delete folder
        await _fileSystemService.DeleteColumnFolderAsync(FolderPath);

        // Update config
        var columnConfig = _board.Columns.FirstOrDefault(c => Path.Combine(_board.RootPath, c.FolderName) == Path.GetFileName(FolderPath));
        if (columnConfig != null)
        {
            _board.Columns.Remove(columnConfig);
            await _boardConfigService.SaveAsync(_board);
        }
    }

    public void RemoveItem(KanbanItemViewModel item)
    {
        Items.Remove(item);
    }

    public async Task UpdateItemOrderAsync()
    {
        var columnConfig = _board.Columns.FirstOrDefault(c => Path.Combine(_board.RootPath, c.FolderName) == Path.GetFileName(FolderPath));
        if (columnConfig != null)
        {
            columnConfig.ItemOrder = Items.Select(i => i.FileName).ToList();
            await _boardConfigService.SaveAsync(_board);
        }
    }

    private string SanitizeFolderName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(sanitized) ? "Column" : sanitized;
    }
}
