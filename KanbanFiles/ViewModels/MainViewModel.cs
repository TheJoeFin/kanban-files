using System.Collections.ObjectModel;
using KanbanFiles.Services;
using Microsoft.UI.Xaml.Controls;

namespace KanbanFiles.ViewModels
{
    public partial class MainViewModel : BaseViewModel
    {
        private readonly BoardConfigService _boardConfigService;
        private readonly FileSystemService _fileSystemService;
        private readonly GroupService _groupService;
        private readonly INotificationService _notificationService;
        private readonly IFocusManagerService _focusManagerService;
        private readonly IRecentFoldersService _recentFoldersService;
        private FileWatcherService? _fileWatcherService;
        private Models.Board? _board;

        public MainViewModel()
        {
            Title = "Home";
            _boardConfigService = new BoardConfigService();
            _fileSystemService = new FileSystemService();
            _groupService = new GroupService();
            _notificationService = new NotificationService();
            ((NotificationService)_notificationService).SetMainViewModel(this);
            _focusManagerService = new FocusManagerService();
            _recentFoldersService = new RecentFoldersService();
            Columns = new ObservableCollection<ColumnViewModel>();
            RecentFolders = new ObservableCollection<string>();
            _ = LoadRecentFoldersAsync();
        }

        public ObservableCollection<ColumnViewModel> Columns { get; }
        
        public ObservableCollection<string> RecentFolders { get; }
        
        public bool HasRecentFolders => RecentFolders.Count > 0;

        [ObservableProperty]
        private string _boardName = "Kanban Board";

        [ObservableProperty]
        private bool _isLoaded = false;
        
        [ObservableProperty]
        private bool _isNotificationVisible = false;
        
        [ObservableProperty]
        private string _notificationTitle = string.Empty;
        
        [ObservableProperty]
        private string _notificationMessage = string.Empty;
        
        [ObservableProperty]
        private InfoBarSeverity _notificationSeverity = InfoBarSeverity.Informational;

        public event EventHandler? OpenFolderRequested;
        public event EventHandler<string>? AddColumnRequested;

        [RelayCommand]
        private void OpenFolder()
        {
            OpenFolderRequested?.Invoke(this, EventArgs.Empty);
        }

        public async Task LoadBoardAsync(string folderPath)
        {
            // Stop any existing file watcher
            _fileWatcherService?.Dispose();
            
            _board = await _boardConfigService.LoadOrInitializeAsync(folderPath);
            
            // Check if config was corrupted and recovered
            if (_boardConfigService.WasConfigCorrupted)
            {
                ShowNotification(
                    "Configuration Recovered",
                    "Board configuration was corrupted and has been reset. A backup was saved.",
                    InfoBarSeverity.Warning);
                _boardConfigService.ResetCorruptionFlag();
            }

            // Start file watcher first
            var dispatcher = App.MainDispatcher;
            if (dispatcher != null)
            {
                _fileWatcherService = new FileWatcherService(_board.RootPath, dispatcher);
                _fileWatcherService.ItemCreated += OnItemCreated;
                _fileWatcherService.ItemDeleted += OnItemDeleted;
                _fileWatcherService.ItemRenamed += OnItemRenamed;
                _fileWatcherService.ItemContentChanged += OnItemContentChanged;
                _fileWatcherService.ColumnCreated += OnColumnCreated;
                _fileWatcherService.ColumnDeleted += OnColumnDeleted;
                _fileWatcherService.Start();
            }

            var columns = await _fileSystemService.EnumerateColumnsAsync(_board);
            
            Columns.Clear();
            foreach (var column in columns)
            {
                var columnViewModel = new ColumnViewModel(column, _fileSystemService, _boardConfigService, _groupService, _board, _fileWatcherService, _notificationService);
                columnViewModel.DeleteRequested += OnColumnDeleteRequested;
                
                // Load groups and populate UngroupedItems/Groups collections
                await columnViewModel.LoadItemsAsync();
                
                Columns.Add(columnViewModel);
            }
            
            BoardName = _board.Name;
            IsLoaded = true;
            
            // Add to recent folders after successful load
            try
            {
                await _recentFoldersService.AddRecentFolderAsync(folderPath);
                await LoadRecentFoldersAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to add recent folder: {ex.Message}");
            }
        }

        [RelayCommand]
        private void AddColumn()
        {
            AddColumnRequested?.Invoke(this, string.Empty);
        }

        public async Task CreateColumnAsync(string columnName)
        {
            if (_board == null) return;

            try
            {
                var sanitizedName = SanitizeFolderName(columnName);
                var newFolderPath = Path.Combine(_board.RootPath, sanitizedName);

                // Suppress the folder watcher event
                _fileWatcherService?.SuppressNextEvent(newFolderPath);

                await _fileSystemService.CreateColumnFolderAsync(_board.RootPath, sanitizedName);

                var newColumnConfig = new Models.ColumnConfig
                {
                    FolderName = sanitizedName,
                    DisplayName = columnName,
                    SortOrder = _board.Columns.Count,
                    ItemOrder = new List<string>()
                };

                _board.Columns.Add(newColumnConfig);
                await _boardConfigService.SaveAsync(_board);

                var column = new Models.Column
                {
                    Name = columnName,
                    FolderPath = newFolderPath,
                    SortOrder = newColumnConfig.SortOrder
                };

                var columnViewModel = new ColumnViewModel(column, _fileSystemService, _boardConfigService, _groupService, _board, _fileWatcherService, _notificationService);
                columnViewModel.DeleteRequested += OnColumnDeleteRequested;
                Columns.Add(columnViewModel);
            }
            catch (UnauthorizedAccessException)
            {
                ShowNotification("Permission Denied", 
                    $"Cannot create column '{columnName}'. Check file permissions.", 
                    InfoBarSeverity.Error);
            }
            catch (IOException ex)
            {
                ShowNotification("Error Creating Column", 
                    ex.Message, 
                    InfoBarSeverity.Error);
            }
        }

        private void OnColumnDeleteRequested(object? sender, string folderPath)
        {
            var columnViewModel = Columns.FirstOrDefault(c => c.FolderPath == folderPath);
            if (columnViewModel != null)
            {
                Columns.Remove(columnViewModel);
            }
        }

        public async Task ReorderColumnAsync(ColumnViewModel sourceColumn, int targetIndex)
        {
            if (_board == null) return;

            // Get current index
            var currentIndex = Columns.IndexOf(sourceColumn);
            if (currentIndex == -1) return;

            // Adjust target index if moving down
            if (targetIndex > currentIndex)
            {
                targetIndex--;
            }

            // Don't move if dropping at same position
            if (currentIndex == targetIndex)
                return;

            // Move in the observable collection
            Columns.Move(currentIndex, targetIndex);

            // Update SortOrder for all columns
            for (int i = 0; i < Columns.Count; i++)
            {
                var columnViewModel = Columns[i];
                var folderName = Path.GetFileName(columnViewModel.FolderPath);
                var columnConfig = _board.Columns.FirstOrDefault(c => c.FolderName == folderName);
                if (columnConfig != null)
                {
                    columnConfig.SortOrder = i;
                }
            }

            // Save to config
            await _boardConfigService.SaveAsync(_board);
        }

        private string SanitizeFolderName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sanitized = string.Join("", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
            return string.IsNullOrWhiteSpace(sanitized) ? "New Column" : sanitized;
        }

        private async void OnItemCreated(object? sender, ItemChangedEventArgs e)
        {
            if (_board == null) return;

            // Find the column for this item
            var columnPath = Path.GetDirectoryName(e.FilePath);
            if (columnPath == null) return;

            var columnViewModel = Columns.FirstOrDefault(c => c.FolderPath == columnPath);
            if (columnViewModel == null) return;

            // Read the item content
            var content = await _fileSystemService.ReadItemContentAsync(e.FilePath);
            var fileName = Path.GetFileName(e.FilePath);
            var title = Path.GetFileNameWithoutExtension(fileName);
            var preview = FileSystemService.GenerateContentPreview(content);

            // Create the item model
            var itemModel = new Models.KanbanItem
            {
                Title = title,
                ContentPreview = preview,
                FilePath = e.FilePath,
                FileName = fileName,
                FullContent = content,
                LastModified = File.GetLastWriteTime(e.FilePath)
            };

            // Create the item viewmodel
            var item = new KanbanItemViewModel(itemModel, _fileSystemService, _boardConfigService, _board, columnViewModel, _fileWatcherService, _notificationService);

            // Add to column
            columnViewModel.Items.Add(item);

            // Update ItemOrder in config
            var folderName = Path.GetFileName(columnPath);
            var columnConfig = _board.Columns.FirstOrDefault(c => c.FolderName == folderName);
            if (columnConfig != null)
            {
                columnConfig.ItemOrder.Add(fileName);
                await _boardConfigService.SaveAsync(_board);
            }
        }

        private async void OnItemDeleted(object? sender, ItemChangedEventArgs e)
        {
            if (_board == null) return;

            // Find the column and item
            var columnPath = Path.GetDirectoryName(e.FilePath);
            if (columnPath == null) return;

            var columnViewModel = Columns.FirstOrDefault(c => c.FolderPath == columnPath);
            if (columnViewModel == null) return;

            var fileName = Path.GetFileName(e.FilePath);
            var item = columnViewModel.Items.FirstOrDefault(i => i.FileName == fileName);
            if (item != null)
            {
                columnViewModel.Items.Remove(item);

                // Update ItemOrder in config
                var folderName = Path.GetFileName(columnPath);
                var columnConfig = _board.Columns.FirstOrDefault(c => c.FolderName == folderName);
                if (columnConfig != null)
                {
                    columnConfig.ItemOrder.Remove(fileName);
                    await _boardConfigService.SaveAsync(_board);
                }
            }
        }

        private async void OnItemRenamed(object? sender, ItemRenamedEventArgs e)
        {
            if (_board == null) return;

            var oldFileName = Path.GetFileName(e.OldFilePath);
            var newFileName = Path.GetFileName(e.NewFilePath);
            var oldExt = Path.GetExtension(e.OldFilePath);
            var newExt = Path.GetExtension(e.NewFilePath);

            // Handle edge case: non-.md → .md (treat as create)
            if (oldExt != ".md" && newExt == ".md")
            {
                OnItemCreated(sender, new ItemChangedEventArgs(e.NewFilePath));
                return;
            }

            // Handle edge case: .md → non-.md (treat as delete)
            if (oldExt == ".md" && newExt != ".md")
            {
                OnItemDeleted(sender, new ItemChangedEventArgs(e.OldFilePath));
                return;
            }

            // Normal rename within .md files
            var columnPath = Path.GetDirectoryName(e.NewFilePath);
            if (columnPath == null) return;

            var columnViewModel = Columns.FirstOrDefault(c => c.FolderPath == columnPath);
            if (columnViewModel == null) return;

            var item = columnViewModel.Items.FirstOrDefault(i => i.FileName == oldFileName);
            if (item != null)
            {
                item.FileName = newFileName;
                item.Title = Path.GetFileNameWithoutExtension(newFileName);
                item.FilePath = e.NewFilePath;

                // Update ItemOrder in config
                var folderName = Path.GetFileName(columnPath);
                var columnConfig = _board.Columns.FirstOrDefault(c => c.FolderName == folderName);
                if (columnConfig != null)
                {
                    var index = columnConfig.ItemOrder.IndexOf(oldFileName);
                    if (index >= 0)
                    {
                        columnConfig.ItemOrder[index] = newFileName;
                        await _boardConfigService.SaveAsync(_board);
                    }
                }
            }
        }

        private async void OnItemContentChanged(object? sender, ItemChangedEventArgs e)
        {
            if (_board == null) return;

            var fileName = Path.GetFileName(e.FilePath);

            // Handle groups.json changes
            if (fileName == "groups.json")
            {
                var columnPath = Path.GetDirectoryName(e.FilePath);
                if (columnPath == null) return;

                var columnViewModel = Columns.FirstOrDefault(c => c.FolderPath == columnPath);
                if (columnViewModel != null)
                {
                    await columnViewModel.LoadGroupsAsync();
                }
                return;
            }

            // Handle .kanban.json changes
            if (fileName == ".kanban.json")
            {
                // Reload the entire board configuration from disk
                await LoadBoardAsync(_board.RootPath);
                ShowNotification("Configuration Updated", 
                    "Board configuration has been reloaded from .kanban.json", 
                    InfoBarSeverity.Informational);
                return;
            }

            // Handle .md file content changes
            if (Path.GetExtension(e.FilePath) != ".md")
                return;

            // Find the column and item
            var itemColumnPath = Path.GetDirectoryName(e.FilePath);
            if (itemColumnPath == null) return;

            var itemColumnViewModel = Columns.FirstOrDefault(c => c.FolderPath == itemColumnPath);
            if (itemColumnViewModel == null) return;

            var itemFileName = Path.GetFileName(e.FilePath);
            var item = itemColumnViewModel.Items.FirstOrDefault(i => i.FileName == itemFileName);
            if (item != null)
            {
                // Re-read content
                var content = await _fileSystemService.ReadItemContentAsync(e.FilePath);
                item.FullContent = content;
                item.ContentPreview = FileSystemService.GenerateContentPreview(content);
                item.LastModified = File.GetLastWriteTime(e.FilePath);
            }
        }

        private void OnColumnCreated(object? sender, ColumnChangedEventArgs e)
        {
            // For now, just log - full implementation would reload board
            // This is intentionally minimal as user-initiated column creation is already handled
        }

        private void OnColumnDeleted(object? sender, ColumnChangedEventArgs e)
        {
            // Find and remove the column
            var columnViewModel = Columns.FirstOrDefault(c => c.FolderPath == e.FolderPath);
            if (columnViewModel != null)
            {
                Columns.Remove(columnViewModel);
            }
        }
        
        public void ShowNotification(string title, string message, InfoBarSeverity severity)
        {
            NotificationTitle = title;
            NotificationMessage = message;
            NotificationSeverity = severity;
            IsNotificationVisible = true;
            
            // Auto-dismiss for informational and success notifications after 5 seconds
            if (severity == InfoBarSeverity.Informational || 
                severity == InfoBarSeverity.Success)
            {
                Task.Delay(5000).ContinueWith(_ =>
                {
                    if (App.MainDispatcher != null)
                    {
                        App.MainDispatcher.TryEnqueue(() => IsNotificationVisible = false);
                    }
                });
            }
        }
        
        public void NavigateLeft()
        {
            if (!IsLoaded || Columns.Count == 0) return;
            
            if (_focusManagerService.FocusedColumnIndex < 0)
            {
                _focusManagerService.SetFocus(0, 0);
            }
            else
            {
                _focusManagerService.MoveLeft(Columns.Count);
            }
        }
        
        public void NavigateRight()
        {
            if (!IsLoaded || Columns.Count == 0) return;
            
            if (_focusManagerService.FocusedColumnIndex < 0)
            {
                _focusManagerService.SetFocus(0, 0);
            }
            else
            {
                _focusManagerService.MoveRight(Columns.Count);
            }
        }
        
        public void NavigateUp()
        {
            if (!IsLoaded || Columns.Count == 0) return;
            
            var colIndex = _focusManagerService.FocusedColumnIndex;
            if (colIndex < 0 || colIndex >= Columns.Count)
            {
                _focusManagerService.SetFocus(0, 0);
                return;
            }
            
            var column = Columns[colIndex];
            _focusManagerService.MoveUp(column.Items.Count);
        }
        
        public void NavigateDown()
        {
            if (!IsLoaded || Columns.Count == 0) return;
            
            var colIndex = _focusManagerService.FocusedColumnIndex;
            if (colIndex < 0 || colIndex >= Columns.Count)
            {
                _focusManagerService.SetFocus(0, 0);
                return;
            }
            
            var column = Columns[colIndex];
            _focusManagerService.MoveDown(column.Items.Count);
        }
        
        public (int columnIndex, int itemIndex) GetCurrentFocus()
        {
            return (_focusManagerService.FocusedColumnIndex, _focusManagerService.FocusedItemIndex);
        }
        
        private async Task LoadRecentFoldersAsync()
        {
            try
            {
                var recentFolders = await _recentFoldersService.GetRecentFoldersAsync();
                RecentFolders.Clear();
                foreach (var folder in recentFolders)
                {
                    RecentFolders.Add(folder);
                }
                OnPropertyChanged(nameof(HasRecentFolders));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load recent folders: {ex.Message}");
            }
        }
        
        [RelayCommand]
        private async Task OpenRecentFolderAsync(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath))
                return;
                
            try
            {
                if (!Directory.Exists(folderPath))
                {
                    ShowNotification("Folder Not Found", 
                        $"The folder '{folderPath}' no longer exists.", 
                        InfoBarSeverity.Error);
                    
                    await _recentFoldersService.RemoveRecentFolderAsync(folderPath);
                    await LoadRecentFoldersAsync();
                    return;
                }
                
                await LoadBoardAsync(folderPath);
            }
            catch (UnauthorizedAccessException)
            {
                ShowNotification("Permission Denied", 
                    $"Cannot access folder '{folderPath}'. Check file permissions.", 
                    InfoBarSeverity.Error);
            }
            catch (IOException ex)
            {
                ShowNotification("Error Opening Folder", 
                    ex.Message, 
                    InfoBarSeverity.Error);
            }
            catch (Exception ex)
            {
                ShowNotification("Error Opening Folder", 
                    $"An unexpected error occurred: {ex.Message}", 
                    InfoBarSeverity.Error);
            }
        }
        
        [RelayCommand]
        private async Task RemoveRecentFolderAsync(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath))
                return;
                
            try
            {
                await _recentFoldersService.RemoveRecentFolderAsync(folderPath);
                await LoadRecentFoldersAsync();
                OnPropertyChanged(nameof(HasRecentFolders));
            }
            catch (Exception ex)
            {
                ShowNotification("Error Removing Folder", 
                    $"Failed to remove folder from recent list: {ex.Message}", 
                    InfoBarSeverity.Error);
            }
        }
    }
}
