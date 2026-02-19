namespace KanbanFiles.ViewModels;

public partial class KanbanItemViewModel : BaseViewModel
{
    private readonly FileSystemService _fileSystemService;
    private readonly BoardConfigService _boardConfigService;
    private readonly Board _board;
    private ColumnViewModel _parentColumn;
    private readonly FileWatcherService? _fileWatcherService;
    private readonly INotificationService? _notificationService;
    private readonly TagService? _tagService;

    [ObservableProperty]
    private string _contentPreview = string.Empty;

    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private string _fileName = string.Empty;

    [ObservableProperty]
    private string _fullContent = string.Empty;

    public ColumnViewModel ParentColumn => _parentColumn;
    public string SourceColumnPath => _parentColumn.FolderPath;
    public DateTime LastModified { get; set; }
    public string LastModifiedDisplay => LastModified.ToString("MMM d, yyyy");
    public bool IsTextFile { get; set; } = true;

    [ObservableProperty]
    private bool _isVisible = true;

    public System.Collections.ObjectModel.ObservableCollection<TagDefinition> Tags { get; } = [];

    public event EventHandler? DeleteRequested;
    public event EventHandler? RenameRequested;

    public KanbanItemViewModel(KanbanItem item, FileSystemService fileSystemService, BoardConfigService boardConfigService, Board board, ColumnViewModel parentColumn, FileWatcherService? fileWatcherService = null, INotificationService? notificationService = null, TagService? tagService = null)
    {
        Title = item.Title;
        ContentPreview = item.ContentPreview;
        FilePath = item.FilePath;
        FileName = item.FileName;
        FullContent = item.FullContent;
        LastModified = item.LastModified;
        IsTextFile = item.IsTextFile;
        _fileSystemService = fileSystemService;
        _boardConfigService = boardConfigService;
        _board = board;
        _parentColumn = parentColumn;
        _fileWatcherService = fileWatcherService;
        _notificationService = notificationService;
        _tagService = tagService;

        LoadTags();
    }

    public void LoadTags()
    {
        Tags.Clear();
        if (_tagService == null) return;

        string columnFolderName = Path.GetFileName(_parentColumn.FolderPath);
        List<string> tagNames = _tagService.GetTagsForItem(_board, columnFolderName, FileName);
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
        if (_tagService == null) return;

        string columnFolderName = Path.GetFileName(_parentColumn.FolderPath);
        await _tagService.ToggleItemTagAsync(_board, columnFolderName, FileName, tagName);
        LoadTags();
    }

    [RelayCommand]
    private void OpenDetail()
    {
        WeakReferenceMessenger.Default.Send(new Messages.OpenItemDetailMessage(this));
    }

    [RelayCommand]
    private void Delete()
    {
        DeleteRequested?.Invoke(this, EventArgs.Empty);
    }

    public async Task DeleteAsync()
    {
        try
        {
            // Suppress the file watcher event
            _fileWatcherService?.SuppressNextEvent(FilePath);

            await _fileSystemService.DeleteItemAsync(FilePath);

            // Update item order in config
            ColumnConfig? columnConfig = _board.Columns.FirstOrDefault(c => Path.Combine(_board.RootPath, c.FolderName) == Path.GetDirectoryName(FilePath));
            if (columnConfig != null)
            {
                columnConfig.ItemOrder.Remove(FileName);
                await _boardConfigService.SaveAsync(_board);
            }

            // Clean up tag assignments
            string columnFolderName = Path.GetFileName(Path.GetDirectoryName(FilePath) ?? string.Empty);
            if (_tagService != null)
            {
                await _tagService.RemoveAssignmentAsync(_board, columnFolderName, FileName);
            }

            _parentColumn.RemoveItem(this);
        }
        catch (UnauthorizedAccessException)
        {
            _notificationService?.ShowNotification("Permission Denied",
                $"Cannot delete '{FileName}'. Check file permissions.",
                InfoBarSeverity.Error);
            throw;
        }
        catch (IOException ex)
        {
            _notificationService?.ShowNotification("Error Deleting Item",
                ex.Message,
                InfoBarSeverity.Error);
            throw;
        }
    }

    [RelayCommand]
    private void Rename()
    {
        RenameRequested?.Invoke(this, EventArgs.Empty);
    }

    public async Task RenameAsync(string newTitle)
    {
        try
        {
            string folderPath = Path.GetDirectoryName(FilePath)!;
            string sanitizedTitle = SanitizeFileName(newTitle);
            string extension = Path.GetExtension(FileName);
            string newFileName = sanitizedTitle + extension;
            string newFilePath = Path.Combine(folderPath, newFileName);

            // Ensure unique filename
            int counter = 1;
            while (File.Exists(newFilePath) && newFilePath != FilePath)
            {
                newFileName = $"{sanitizedTitle}-{counter}{extension}";
                newFilePath = Path.Combine(folderPath, newFileName);
                counter++;
            }

            if (IsTextFile)
            {
                // Read content
                string content = await _fileSystemService.ReadItemContentAsync(FilePath);

                // Update first line if it's a markdown heading
                if (extension.Equals(".md", StringComparison.OrdinalIgnoreCase))
                {
                    string[] lines = content.Split(['\r', '\n'], StringSplitOptions.None);
                    if (lines.Length > 0 && lines[0].StartsWith("# "))
                    {
                        lines[0] = $"# {newTitle}";
                        content = string.Join(Environment.NewLine, lines);
                    }
                }

                // Suppress the file watcher events
                _fileWatcherService?.SuppressNextEvent(newFilePath); // Write
                if (newFilePath != FilePath)
                {
                    _fileWatcherService?.SuppressNextEvent(FilePath); // Delete old
                    _fileWatcherService?.SuppressNextEvent(newFilePath); // Rename detection
                }
                else
                {
                    _fileWatcherService?.SuppressNextEvent(FilePath); // Content change
                }

                // Write to new file
                await _fileSystemService.WriteItemContentAsync(newFilePath, content);

                // Delete old file if renamed
                if (newFilePath != FilePath)
                {
                    await _fileSystemService.DeleteItemAsync(FilePath);
                }
            }
            else
            {
                // Non-text file: just rename via move
                if (newFilePath != FilePath)
                {
                    _fileWatcherService?.SuppressNextEvent(FilePath);
                    _fileWatcherService?.SuppressNextEvent(newFilePath);
                    await Task.Run(() => File.Move(FilePath, newFilePath));
                }
            }

            // Update config
            ColumnConfig? columnConfig = _board.Columns.FirstOrDefault(c => Path.Combine(_board.RootPath, c.FolderName) == folderPath);
            if (columnConfig != null)
            {
                int index = columnConfig.ItemOrder.IndexOf(FileName);
                if (index >= 0)
                {
                    columnConfig.ItemOrder[index] = newFileName;
                }
                await _boardConfigService.SaveAsync(_board);
            }

            // Update tag assignment key
            string columnFolderName = Path.GetFileName(folderPath);
            if (_tagService != null && newFileName != FileName)
            {
                await _tagService.UpdateItemKeyAsync(_board, columnFolderName, FileName, columnFolderName, newFileName);
            }

            Title = newTitle;
            UpdateFilePath(newFilePath);
            LoadTags();
        }
        catch (UnauthorizedAccessException)
        {
            _notificationService?.ShowNotification("Permission Denied",
                $"Cannot rename '{FileName}'. Check file permissions.",
                InfoBarSeverity.Error);
            throw;
        }
        catch (IOException ex)
        {
            _notificationService?.ShowNotification("Error Renaming Item",
                ex.Message,
                InfoBarSeverity.Error);
            throw;
        }
    }

    public void UpdateParentColumn(ColumnViewModel newParentColumn)
    {
        _parentColumn = newParentColumn;
        OnPropertyChanged(nameof(SourceColumnPath));
    }

    public void UpdateFilePath(string newFilePath)
    {
        FilePath = newFilePath;
        FileName = Path.GetFileName(newFilePath);
    }

    private static string SanitizeFileName(string fileName)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        string sanitized = string.Join("", fileName.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(sanitized) ? "untitled" : sanitized;
    }
}
