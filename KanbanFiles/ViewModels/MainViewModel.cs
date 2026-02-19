using Microsoft.UI.Dispatching;
using System.Collections.ObjectModel;

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
        private readonly TagService _tagService;
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
            _tagService = new TagService(_boardConfigService);
            Columns = [];
            RecentFolders = [];
            _ = LoadRecentFoldersAsync();
        }

        public ObservableCollection<ColumnViewModel> Columns { get; }

        public ObservableCollection<string> RecentFolders { get; }

        public bool HasRecentFolders => RecentFolders.Count > 0;

        public ObservableCollection<TagDefinition> AvailableTags { get; } = [];

        public ObservableCollection<TagDefinition> ActiveTagFilters { get; } = [];

        public bool HasActiveTagFilter => ActiveTagFilters.Count > 0;

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

        [ObservableProperty]
        private bool _isItemDetailOpen = false;

        [ObservableProperty]
        private ItemDetailViewModel? _currentItemDetail;

        private KanbanItemViewModel? _currentKanbanItem;

        public event EventHandler? OpenFolderRequested;
        public event EventHandler<string>? AddColumnRequested;
        public event EventHandler? EditFileFilterRequested;
        public event EventHandler? ManageTagsRequested;

        public TagService TagService => _tagService;
        public Models.Board? Board => _board;

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
            DispatcherQueue? dispatcher = App.MainDispatcher;
            if (dispatcher != null)
            {
                _fileWatcherService = new FileWatcherService(_board.RootPath, dispatcher);
                _boardConfigService.FileWatcher = _fileWatcherService;
                _fileWatcherService.ItemCreated += OnItemCreated;
                _fileWatcherService.ItemDeleted += OnItemDeleted;
                _fileWatcherService.ItemRenamed += OnItemRenamed;
                _fileWatcherService.ItemContentChanged += OnItemContentChanged;
                _fileWatcherService.ColumnCreated += OnColumnCreated;
                _fileWatcherService.ColumnDeleted += OnColumnDeleted;
                _fileWatcherService.Start();
            }

            List<Column> columns = await _fileSystemService.EnumerateColumnsAsync(_board);

            Columns.Clear();
            foreach (Column column in columns)
            {
                ColumnViewModel columnViewModel = new(column, _fileSystemService, _boardConfigService, _groupService, _board, _fileWatcherService, _notificationService, _tagService);
                columnViewModel.DeleteRequested += OnColumnDeleteRequested;

                // Load groups and populate UngroupedItems/Groups collections
                await columnViewModel.LoadItemsAsync();

                Columns.Add(columnViewModel);
            }

            BoardName = _board.Name;
            IsLoaded = true;
            RefreshAvailableTags();

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

        [RelayCommand]
        private void EditFileFilter()
        {
            EditFileFilterRequested?.Invoke(this, EventArgs.Empty);
        }

        public FileFilterConfig GetFileFilter()
        {
            return _board?.FileFilter ?? new FileFilterConfig();
        }

        public async Task UpdateFileFilterAsync(FileFilterConfig filter)
        {
            if (_board == null) return;

            // Normalize: store null when both lists are empty
            _board.FileFilter = (filter.IncludeExtensions.Count == 0 && filter.ExcludeExtensions.Count == 0)
                ? null
                : filter;

            await _boardConfigService.SaveAsync(_board);
            await LoadBoardAsync(_board.RootPath);

            ShowNotification("File Filter Updated",
                "The board has been reloaded with the new file filter.",
                InfoBarSeverity.Success);
        }

        public async Task CreateColumnAsync(string columnName)
        {
            if (_board == null) return;

            try
            {
                string sanitizedName = SanitizeFolderName(columnName);
                string newFolderPath = Path.Combine(_board.RootPath, sanitizedName);

                // Suppress the folder watcher event
                _fileWatcherService?.SuppressNextEvent(newFolderPath);

                await _fileSystemService.CreateColumnFolderAsync(_board.RootPath, sanitizedName);

                ColumnConfig newColumnConfig = new()
                {
                    FolderName = sanitizedName,
                    DisplayName = columnName,
                    SortOrder = _board.Columns.Count,
                    ItemOrder = []
                };

                _board.Columns.Add(newColumnConfig);
                await _boardConfigService.SaveAsync(_board);

                Column column = new()
                {
                    Name = columnName,
                    FolderPath = newFolderPath,
                    SortOrder = newColumnConfig.SortOrder
                };

                ColumnViewModel columnViewModel = new(column, _fileSystemService, _boardConfigService, _groupService, _board, _fileWatcherService, _notificationService, _tagService);
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
            ColumnViewModel? columnViewModel = Columns.FirstOrDefault(c => c.FolderPath == folderPath);
            if (columnViewModel != null)
            {
                Columns.Remove(columnViewModel);
            }
        }

        public async Task ReorderColumnAsync(ColumnViewModel sourceColumn, int targetIndex)
        {
            if (_board == null) return;

            // Get current index
            int currentIndex = Columns.IndexOf(sourceColumn);
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
                ColumnViewModel columnViewModel = Columns[i];
                string folderName = Path.GetFileName(columnViewModel.FolderPath);
                ColumnConfig? columnConfig = _board.Columns.FirstOrDefault(c => c.FolderName == folderName);
                columnConfig?.SortOrder = i;
            }

            // Save to config
            await _boardConfigService.SaveAsync(_board);
        }

        private static string SanitizeFolderName(string name)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            string sanitized = string.Join("", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
            return string.IsNullOrWhiteSpace(sanitized) ? "New Column" : sanitized;
        }

        private async void OnItemCreated(object? sender, ItemChangedEventArgs e)
        {
            if (_board == null) return;

            // Find the column for this item
            string? columnPath = Path.GetDirectoryName(e.FilePath);
            if (columnPath == null) return;

            string fileName = Path.GetFileName(e.FilePath);

            // Skip excluded files
            if (FileSystemService.IsExcludedItemFile(fileName)) return;

            // Check file filter
            if (!FileSystemService.PassesFilter(e.FilePath, _board.FileFilter)) return;

            ColumnViewModel? columnViewModel = Columns.FirstOrDefault(c => c.FolderPath == columnPath);
            if (columnViewModel == null) return;

            // Read the item content
            bool isText = FileSystemService.IsTextFile(e.FilePath);
            string content = isText ? await _fileSystemService.ReadItemContentAsync(e.FilePath) : string.Empty;
            string title = Path.GetFileNameWithoutExtension(fileName);
            string preview = isText ? FileSystemService.GenerateContentPreview(content) : FileSystemService.GenerateFileTypePreview(e.FilePath);

            // Create the item model
            KanbanItem itemModel = new()
            {
                Title = title,
                ContentPreview = preview,
                FilePath = e.FilePath,
                FileName = fileName,
                FullContent = content,
                LastModified = File.GetLastWriteTime(e.FilePath),
                IsTextFile = isText
            };

            // Create the item viewmodel
            KanbanItemViewModel item = new(itemModel, _fileSystemService, _boardConfigService, _board, columnViewModel, _fileWatcherService, _notificationService, _tagService);

            // Add to column
            columnViewModel.Items.Add(item);

            // New items always start as ungrouped
            columnViewModel.UngroupedItems.Add(item);

            // Update ItemOrder in config
            string folderName = Path.GetFileName(columnPath);
            ColumnConfig? columnConfig = _board.Columns.FirstOrDefault(c => c.FolderName == folderName);
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
            string? columnPath = Path.GetDirectoryName(e.FilePath);
            if (columnPath == null) return;

            ColumnViewModel? columnViewModel = Columns.FirstOrDefault(c => c.FolderPath == columnPath);
            if (columnViewModel == null) return;

            string fileName = Path.GetFileName(e.FilePath);
            KanbanItemViewModel? item = columnViewModel.Items.FirstOrDefault(i => i.FileName == fileName);
            if (item != null)
            {
                columnViewModel.RemoveItem(item);

                // Update ItemOrder in config
                string folderName = Path.GetFileName(columnPath);
                ColumnConfig? columnConfig = _board.Columns.FirstOrDefault(c => c.FolderName == folderName);
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

            string oldFileName = Path.GetFileName(e.OldFilePath);
            string newFileName = Path.GetFileName(e.NewFilePath);

            // Hidden → visible: treat as create
            if (oldFileName.StartsWith('.') && !newFileName.StartsWith('.'))
            {
                OnItemCreated(sender, new ItemChangedEventArgs(e.NewFilePath));
                return;
            }

            // Visible → hidden: treat as delete
            if (!oldFileName.StartsWith('.') && newFileName.StartsWith('.'))
            {
                OnItemDeleted(sender, new ItemChangedEventArgs(e.OldFilePath));
                return;
            }

            // Normal rename
            string? columnPath = Path.GetDirectoryName(e.NewFilePath);
            if (columnPath == null) return;

            ColumnViewModel? columnViewModel = Columns.FirstOrDefault(c => c.FolderPath == columnPath);
            if (columnViewModel == null) return;

            KanbanItemViewModel? item = columnViewModel.Items.FirstOrDefault(i => i.FileName == oldFileName);
            if (item != null)
            {
                item.FileName = newFileName;
                item.Title = Path.GetFileNameWithoutExtension(newFileName);
                item.FilePath = e.NewFilePath;
                item.IsTextFile = FileSystemService.IsTextFile(e.NewFilePath);

                // Update ItemOrder in config
                string folderName = Path.GetFileName(columnPath);
                ColumnConfig? columnConfig = _board.Columns.FirstOrDefault(c => c.FolderName == folderName);
                if (columnConfig != null)
                {
                    int index = columnConfig.ItemOrder.IndexOf(oldFileName);
                    if (index >= 0)
                    {
                        columnConfig.ItemOrder[index] = newFileName;
                        await _boardConfigService.SaveAsync(_board);
                    }
                }
            }
            else
            {
                // Item not found by old name - might be a newly visible file
                OnItemCreated(sender, new ItemChangedEventArgs(e.NewFilePath));
            }
        }

        private async void OnItemContentChanged(object? sender, ItemChangedEventArgs e)
        {
            if (_board == null) return;

            string fileName = Path.GetFileName(e.FilePath);

            // Handle group .json file changes (any .json in a column folder)
            if (Path.GetExtension(e.FilePath).Equals(".json", StringComparison.OrdinalIgnoreCase)
                && !fileName.Equals(".kanban.json", StringComparison.OrdinalIgnoreCase))
            {
                string? columnPath = Path.GetDirectoryName(e.FilePath);
                if (columnPath == null) return;

                ColumnViewModel? columnViewModel = Columns.FirstOrDefault(c => c.FolderPath == columnPath);
                if (columnViewModel != null)
                {
                    await columnViewModel.LoadItemsAsync();
                }
                return;
            }

            // Handle .kanban.json changes
            if (fileName == ".kanban.json")
            {
                // Reload the entire board configuration from disk
                // No notification needed - the UI update provides sufficient feedback
                await LoadBoardAsync(_board.RootPath);
                return;
            }

            // Handle item file content changes (all file types)
            string? itemColumnPath = Path.GetDirectoryName(e.FilePath);
            if (itemColumnPath == null) return;

            ColumnViewModel? itemColumnViewModel = Columns.FirstOrDefault(c => c.FolderPath == itemColumnPath);
            if (itemColumnViewModel == null) return;

            string itemFileName = Path.GetFileName(e.FilePath);
            KanbanItemViewModel? item = itemColumnViewModel.Items.FirstOrDefault(i => i.FileName == itemFileName);
            if (item != null)
            {
                if (item.IsTextFile)
                {
                    // Re-read content
                    string content = await _fileSystemService.ReadItemContentAsync(e.FilePath);
                    item.FullContent = content;
                    item.ContentPreview = FileSystemService.GenerateContentPreview(content);
                }
                else
                {
                    item.ContentPreview = FileSystemService.GenerateFileTypePreview(e.FilePath);
                }
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
            ColumnViewModel? columnViewModel = Columns.FirstOrDefault(c => c.FolderPath == e.FolderPath);
            if (columnViewModel != null)
            {
                Columns.Remove(columnViewModel);
            }
        }

        public (KanbanItemViewModel kanbanItem, ItemDetailViewModel detailVm, FileWatcherService? fileWatcher) OpenItemDetail(KanbanItemViewModel kanbanItemViewModel)
        {
            KanbanItem itemModel = new()
            {
                Title = kanbanItemViewModel.Title,
                FilePath = kanbanItemViewModel.FilePath,
                FileName = kanbanItemViewModel.FileName,
                ContentPreview = kanbanItemViewModel.ContentPreview,
                FullContent = kanbanItemViewModel.FullContent,
                LastModified = kanbanItemViewModel.LastModified
            };

            ItemDetailViewModel detailViewModel = new(itemModel, _fileSystemService, _fileWatcherService);
            CurrentItemDetail = detailViewModel;
            _currentKanbanItem = kanbanItemViewModel;
            IsItemDetailOpen = true;

            return (kanbanItemViewModel, detailViewModel, _fileWatcherService);
        }

        public void CloseItemDetail()
        {
            IsItemDetailOpen = false;
            CurrentItemDetail = null;
            _currentKanbanItem = null;
        }

        public void RefreshAvailableTags()
        {
            AvailableTags.Clear();
            if (_board == null) return;

            List<TagDefinition> definitions = _tagService.GetTagDefinitions(_board);
            foreach (TagDefinition tag in definitions)
            {
                AvailableTags.Add(tag);
            }

            // Remove stale filters
            List<TagDefinition> staleFilters = ActiveTagFilters
                .Where(f => !definitions.Any(d => d.Name == f.Name))
                .ToList();
            foreach (TagDefinition stale in staleFilters)
            {
                ActiveTagFilters.Remove(stale);
            }
            OnPropertyChanged(nameof(HasActiveTagFilter));
        }

        public void ToggleTagFilter(TagDefinition tag)
        {
            TagDefinition? existing = ActiveTagFilters.FirstOrDefault(t => t.Name == tag.Name);
            if (existing != null)
            {
                ActiveTagFilters.Remove(existing);
            }
            else
            {
                ActiveTagFilters.Add(tag);
            }
            OnPropertyChanged(nameof(HasActiveTagFilter));
            ApplyTagFilter();
        }

        [RelayCommand]
        private void ClearTagFilters()
        {
            ActiveTagFilters.Clear();
            OnPropertyChanged(nameof(HasActiveTagFilter));
            ApplyTagFilter();
        }

        [RelayCommand]
        private void ManageTags()
        {
            ManageTagsRequested?.Invoke(this, EventArgs.Empty);
        }

        public async Task CreateTagAsync(string name, string color)
        {
            if (_board == null) return;
            await _tagService.CreateTagAsync(_board, name, color);
            RefreshAvailableTags();
        }

        public async Task DeleteTagAsync(string tagName)
        {
            if (_board == null) return;
            await _tagService.DeleteTagAsync(_board, tagName);
            RefreshAvailableTags();

            // Reload tags on all items and groups
            ReloadAllTags();
        }

        public async Task RenameTagAsync(string oldName, string newName)
        {
            if (_board == null) return;
            await _tagService.RenameTagAsync(_board, oldName, newName);
            RefreshAvailableTags();
            ReloadAllTags();
        }

        public async Task UpdateTagColorAsync(string tagName, string newColor)
        {
            if (_board == null) return;
            await _tagService.UpdateTagColorAsync(_board, tagName, newColor);
            RefreshAvailableTags();
            ReloadAllTags();
        }

        private void ReloadAllTags()
        {
            foreach (ColumnViewModel column in Columns)
            {
                foreach (KanbanItemViewModel item in column.Items)
                {
                    item.LoadTags();
                }
                foreach (GroupViewModel group in column.Groups)
                {
                    group.LoadTags();
                }
            }
            ApplyTagFilter();
        }

        public bool ItemPassesTagFilter(KanbanItemViewModel item)
        {
            if (ActiveTagFilters.Count == 0) return true;
            return ActiveTagFilters.All(filter => item.Tags.Any(t => t.Name == filter.Name));
        }

        public bool GroupPassesTagFilter(GroupViewModel group)
        {
            if (ActiveTagFilters.Count == 0) return true;

            // A group passes if the group itself is tagged, or any of its items pass
            bool groupTagged = ActiveTagFilters.All(filter => group.Tags.Any(t => t.Name == filter.Name));
            if (groupTagged) return true;

            return group.Items.Any(item => ItemPassesTagFilter(item));
        }

        public void ApplyTagFilter()
        {
            foreach (ColumnViewModel column in Columns)
            {
                foreach (KanbanItemViewModel item in column.Items)
                {
                    item.IsVisible = ItemPassesTagFilter(item);
                }
                foreach (GroupViewModel group in column.Groups)
                {
                    group.IsVisible = GroupPassesTagFilter(group);
                }
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
                    App.MainDispatcher?.TryEnqueue(() => IsNotificationVisible = false);
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

            int colIndex = _focusManagerService.FocusedColumnIndex;
            if (colIndex < 0 || colIndex >= Columns.Count)
            {
                _focusManagerService.SetFocus(0, 0);
                return;
            }

            ColumnViewModel column = Columns[colIndex];
            _focusManagerService.MoveUp(column.Items.Count);
        }

        public void NavigateDown()
        {
            if (!IsLoaded || Columns.Count == 0) return;

            int colIndex = _focusManagerService.FocusedColumnIndex;
            if (colIndex < 0 || colIndex >= Columns.Count)
            {
                _focusManagerService.SetFocus(0, 0);
                return;
            }

            ColumnViewModel column = Columns[colIndex];
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
                List<string> recentFolders = await _recentFoldersService.GetRecentFoldersAsync();
                RecentFolders.Clear();
                foreach (string folder in recentFolders)
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
