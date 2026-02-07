using KanbanFiles.Services;
using Microsoft.UI.Xaml.Controls;

namespace KanbanFiles.ViewModels;

public partial class KanbanItemViewModel : BaseViewModel
{
    private readonly FileSystemService _fileSystemService;
    private readonly BoardConfigService _boardConfigService;
    private readonly Board _board;
    private ColumnViewModel _parentColumn;
    private readonly FileWatcherService? _fileWatcherService;
    private readonly INotificationService? _notificationService;

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

    public event EventHandler? DeleteRequested;
    public event EventHandler? RenameRequested;

    public KanbanItemViewModel(KanbanItem item, FileSystemService fileSystemService, BoardConfigService boardConfigService, Board board, ColumnViewModel parentColumn, FileWatcherService? fileWatcherService = null, INotificationService? notificationService = null)
    {
        Title = item.Title;
        ContentPreview = item.ContentPreview;
        FilePath = item.FilePath;
        FileName = item.FileName;
        FullContent = item.FullContent;
        LastModified = item.LastModified;
        _fileSystemService = fileSystemService;
        _boardConfigService = boardConfigService;
        _board = board;
        _parentColumn = parentColumn;
        _fileWatcherService = fileWatcherService;
        _notificationService = notificationService;
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
            ColumnConfig? columnConfig = _board.Columns.FirstOrDefault(c => Path.Combine(_board.RootPath, c.FolderName) == Path.GetFileName(Path.GetDirectoryName(FilePath)));
            if (columnConfig != null)
            {
                columnConfig.ItemOrder.Remove(FileName);
                await _boardConfigService.SaveAsync(_board);
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
            string newFileName = sanitizedTitle + ".md";
            string newFilePath = Path.Combine(folderPath, newFileName);

            // Ensure unique filename
            int counter = 1;
            while (File.Exists(newFilePath) && newFilePath != FilePath)
            {
                newFileName = $"{sanitizedTitle}-{counter}.md";
                newFilePath = Path.Combine(folderPath, newFileName);
                counter++;
            }

            // Read content
            string content = await _fileSystemService.ReadItemContentAsync(FilePath);

            // Update first line if it's a heading
            string[] lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.None);
            if (lines.Length > 0 && lines[0].StartsWith("# "))
            {
                lines[0] = $"# {newTitle}";
                content = string.Join(Environment.NewLine, lines);
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

            // Update config
            ColumnConfig? columnConfig = _board.Columns.FirstOrDefault(c => Path.Combine(_board.RootPath, c.FolderName) == Path.GetFileName(folderPath));
            if (columnConfig != null)
            {
                int index = columnConfig.ItemOrder.IndexOf(FileName);
                if (index >= 0)
                {
                    columnConfig.ItemOrder[index] = newFileName;
                }
                await _boardConfigService.SaveAsync(_board);
            }

            Title = newTitle;
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

    private string SanitizeFileName(string fileName)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        string sanitized = string.Join("", fileName.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(sanitized) ? "untitled" : sanitized;
    }
}
