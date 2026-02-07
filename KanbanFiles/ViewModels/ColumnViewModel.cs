using System.Collections.ObjectModel;
using KanbanFiles.Models;
using KanbanFiles.Services;
using Microsoft.UI.Xaml.Controls;

namespace KanbanFiles.ViewModels;

public partial class ColumnViewModel : BaseViewModel
{
    private readonly FileSystemService _fileSystemService;
    private readonly BoardConfigService _boardConfigService;
    private readonly GroupService _groupService;
    private readonly Board _board;
    private readonly FileWatcherService? _fileWatcherService;
    private readonly INotificationService? _notificationService;

    [ObservableProperty]
    private string _name = string.Empty;

    public ObservableCollection<KanbanItemViewModel> Items { get; } = new();
    public ObservableCollection<KanbanItemViewModel> UngroupedItems { get; } = new();
    public ObservableCollection<GroupViewModel> Groups { get; } = new();

    public string FolderPath { get; }

    public event EventHandler<string>? DeleteRequested;
    public event EventHandler? AddItemRequested;
    public event EventHandler<string>? RenameRequested;
    public event EventHandler<GroupViewModel>? GroupRenameRequested;
    public event EventHandler<GroupViewModel>? GroupDeleteRequested;

    public ColumnViewModel(Column column, FileSystemService fileSystemService, BoardConfigService boardConfigService, GroupService groupService, Board board, FileWatcherService? fileWatcherService = null, INotificationService? notificationService = null)
    {
        Name = column.Name;
        FolderPath = column.FolderPath;
        _fileSystemService = fileSystemService;
        _boardConfigService = boardConfigService;
        _groupService = groupService;
        _board = board;
        _fileWatcherService = fileWatcherService;
        _notificationService = notificationService;

        foreach (KanbanItem item in column.Items)
        {
            var itemViewModel = new KanbanItemViewModel(item, _fileSystemService, _boardConfigService, _board, this, _fileWatcherService, _notificationService);
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
        try
        {
            var filePath = await _fileSystemService.CreateItemAsync(FolderPath, title);
            var fileName = Path.GetFileName(filePath);

            // Suppress the file watcher event
            _fileWatcherService?.SuppressNextEvent(filePath);

            // Update item order in config
            ColumnConfig? columnConfig = _board.Columns.FirstOrDefault(c => Path.Combine(_board.RootPath, c.FolderName) == FolderPath);
            if (columnConfig != null)
            {
                columnConfig.ItemOrder.Add(fileName);
                await _boardConfigService.SaveAsync(_board);
            }

            // Read the newly created item
            List<KanbanItem> items = await _fileSystemService.EnumerateItemsAsync(FolderPath);
            KanbanItem? newItem = items.FirstOrDefault(i => i.FileName == fileName);
            if (newItem != null)
            {
                var itemViewModel = new KanbanItemViewModel(newItem, _fileSystemService, _boardConfigService, _board, this, _fileWatcherService, _notificationService);
                Items.Add(itemViewModel);
                
                // New items always start as ungrouped
                    UngroupedItems.Add(itemViewModel);
            }
        }
        catch (UnauthorizedAccessException)
        {
            _notificationService?.ShowNotification("Permission Denied", 
                $"Cannot create item '{title}'. Check file permissions.", 
                InfoBarSeverity.Error);
        }
        catch (IOException ex)
        {
            _notificationService?.ShowNotification("Error Creating Item", 
                ex.Message, 
                InfoBarSeverity.Error);
        }
    }

    [RelayCommand]
    public void RenameColumn()
    {
        RenameRequested?.Invoke(this, Name);
    }

    public async Task RenameColumnAsync(string newName)
    {
        try
        {
            var sanitizedName = SanitizeFolderName(newName);
            var oldFolderPath = FolderPath;
            var newFolderPath = Path.Combine(Path.GetDirectoryName(FolderPath)!, sanitizedName);

            // Suppress the folder watcher events
            _fileWatcherService?.SuppressNextEvent(oldFolderPath);
            _fileWatcherService?.SuppressNextEvent(newFolderPath);

            // Rename folder
            await Task.Run(() => Directory.Move(oldFolderPath, newFolderPath));

            // Update config
            ColumnConfig? columnConfig = _board.Columns.FirstOrDefault(c => Path.Combine(_board.RootPath, c.FolderName) == Path.GetFileName(oldFolderPath));
            if (columnConfig != null)
            {
                columnConfig.FolderName = sanitizedName;
                columnConfig.DisplayName = newName;
                await _boardConfigService.SaveAsync(_board);
            }

            Name = newName;
        }
        catch (UnauthorizedAccessException)
        {
            _notificationService?.ShowNotification("Permission Denied", 
                $"Cannot rename column '{Name}'. Check file permissions.", 
                InfoBarSeverity.Error);
            throw;
        }
        catch (IOException ex)
        {
            _notificationService?.ShowNotification("Error Renaming Column", 
                ex.Message, 
                InfoBarSeverity.Error);
            throw;
        }
    }

    [RelayCommand]
    public void DeleteColumn()
    {
        DeleteRequested?.Invoke(this, FolderPath);
    }

    public async Task DeleteColumnAsync()
    {
        try
        {
            // Suppress the folder watcher event
            _fileWatcherService?.SuppressNextEvent(FolderPath);

            // Delete folder
            await _fileSystemService.DeleteColumnFolderAsync(FolderPath);

            // Update config
            ColumnConfig? columnConfig = _board.Columns.FirstOrDefault(c => Path.Combine(_board.RootPath, c.FolderName) == Path.GetFileName(FolderPath));
            if (columnConfig != null)
            {
                _board.Columns.Remove(columnConfig);
                await _boardConfigService.SaveAsync(_board);
            }
        }
        catch (UnauthorizedAccessException)
        {
            _notificationService?.ShowNotification("Permission Denied", 
                $"Cannot delete column '{Name}'. Check file permissions.", 
                InfoBarSeverity.Error);
            throw;
        }
        catch (IOException ex)
        {
            _notificationService?.ShowNotification("Error Deleting Column", 
                ex.Message, 
                InfoBarSeverity.Error);
            throw;
        }
    }

    public void RemoveItem(KanbanItemViewModel item)
    {
        Items.Remove(item);
        UngroupedItems.Remove(item);
        foreach (GroupViewModel group in Groups)
        {
            group.Items.Remove(item);
        }
    }

    public async Task UpdateItemOrderAsync()
    {
        ColumnConfig? columnConfig = _board.Columns.FirstOrDefault(c => Path.Combine(_board.RootPath, c.FolderName) == Path.GetFileName(FolderPath));
        if (columnConfig != null)
        {
            columnConfig.ItemOrder = Items.Select(i => i.FileName).ToList();
            await _boardConfigService.SaveAsync(_board);
        }
    }

    public async Task<bool> MoveItemToColumnAsync(KanbanItemViewModel sourceItem, ColumnViewModel sourceColumnViewModel, int targetIndex)
    {
        try
        {
            // Suppress the file watcher events (old and new paths)
            _fileWatcherService?.SuppressNextEvent(sourceItem.FilePath);
            var expectedNewPath = Path.Combine(FolderPath, sourceItem.FileName);
            _fileWatcherService?.SuppressNextEvent(expectedNewPath);

            // Move the physical file
            var newFilePath = await _fileSystemService.MoveItemAsync(sourceItem.FilePath, FolderPath);

            // Remove from source column (both flat and UI-bound collections)
            sourceColumnViewModel.RemoveItem(sourceItem);

            // Update the item's parent column and file path
            sourceItem.UpdateParentColumn(this);
            sourceItem.UpdateFilePath(newFilePath);

            // Insert at target index in this column
            if (targetIndex < 0 || targetIndex > Items.Count)
            {
                targetIndex = Items.Count;
            }
            Items.Insert(targetIndex, sourceItem);
            UngroupedItems.Add(sourceItem);

            // Update item order in both columns
            await sourceColumnViewModel.UpdateItemOrderAsync();
            await UpdateItemOrderAsync();

            return true;
        }
        catch (UnauthorizedAccessException)
        {
            _notificationService?.ShowNotification("Permission Denied", 
                $"Cannot move item '{sourceItem.FileName}'. Check file permissions.", 
                InfoBarSeverity.Error);
            return false;
        }
        catch (IOException ex)
        {
            _notificationService?.ShowNotification("Error Moving Item", 
                ex.Message, 
                InfoBarSeverity.Error);
            return false;
        }
        catch (Exception ex)
        {
            _notificationService?.ShowNotification("Error", 
                $"An unexpected error occurred: {ex.Message}", 
                InfoBarSeverity.Error);
            return false;
        }
    }

    private string SanitizeFolderName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(sanitized) ? "Column" : sanitized;
    }

    public async Task LoadItemsAsync()
    {
        // Load groups and build a fileName → groupName lookup
        List<Group> loadedGroups = await _groupService.LoadGroupsAsync(FolderPath);
        var fileToGroup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Group g in loadedGroups)
        {
            foreach (string fileName in g.ItemFileNames)
            {
                fileToGroup[fileName] = g.Name;
            }
        }

        // Rebuild group ViewModels
        Groups.Clear();
        foreach (Group group in loadedGroups)
        {
            var groupViewModel = new GroupViewModel(group.Name)
            {
                IsCollapsed = group.IsCollapsed
            };
            groupViewModel.RenameRequested += (sender, e) => GroupRenameRequested?.Invoke(this, groupViewModel);
            groupViewModel.DeleteRequested += (sender, e) => GroupDeleteRequested?.Invoke(this, groupViewModel);
            Groups.Add(groupViewModel);
        }

        // Enumerate items from file system
        List<KanbanItem> items = await _fileSystemService.EnumerateItemsAsync(FolderPath);

        // Clear existing items
        Items.Clear();
        UngroupedItems.Clear();

        // Create ViewModels and organize into groups using the lookup
        foreach (KanbanItem item in items)
        {
            var itemViewModel = new KanbanItemViewModel(item, _fileSystemService, _boardConfigService, _board, this, _fileWatcherService, _notificationService);
            Items.Add(itemViewModel);

            if (fileToGroup.TryGetValue(item.FileName, out string? groupName))
            {
                GroupViewModel? group = Groups.FirstOrDefault(g => g.Name == groupName);
                if (group != null)
                {
                    group.Items.Add(itemViewModel);
                }
                else
                {
                    UngroupedItems.Add(itemViewModel);
                }
            }
            else
            {
                UngroupedItems.Add(itemViewModel);
            }
        }
    }

    public async Task LoadGroupsAsync()
    {
        List<Group> loadedGroups = await _groupService.LoadGroupsAsync(FolderPath);

        Groups.Clear();

        foreach (Group group in loadedGroups)
        {
            var groupViewModel = new GroupViewModel(group.Name)
            {
                IsCollapsed = group.IsCollapsed
            };
            
            // Wire up events
            groupViewModel.RenameRequested += (sender, e) => GroupRenameRequested?.Invoke(this, groupViewModel);
            groupViewModel.DeleteRequested += (sender, e) => GroupDeleteRequested?.Invoke(this, groupViewModel);
            
            Groups.Add(groupViewModel);
        }
    }

    public async Task CreateGroupAsync(string groupName)
    {
        string sanitized = GroupService.SanitizeGroupName(groupName);
        List<Group> existingGroups = await _groupService.LoadGroupsAsync(FolderPath);
        string actualName = _groupService.GetUniqueGroupName(existingGroups, sanitized);

        SuppressGroupFileWatcher(actualName);

        var newGroup = new Group
        {
            Name = actualName,
            SortOrder = existingGroups.Count > 0 ? existingGroups.Max(g => g.SortOrder) + 1 : 0,
            ItemFileNames = [],
            IsCollapsed = false
        };
        await _groupService.SaveGroupAsync(FolderPath, newGroup);

        var groupViewModel = new GroupViewModel(actualName);
        groupViewModel.RenameRequested += (sender, e) => GroupRenameRequested?.Invoke(this, groupViewModel);
        groupViewModel.DeleteRequested += (sender, e) => GroupDeleteRequested?.Invoke(this, groupViewModel);
        Groups.Add(groupViewModel);
    }

    public async Task RenameGroupAsync(string oldName, string newName)
    {
        string sanitized = GroupService.SanitizeGroupName(newName);
        List<Group> existingGroups = await _groupService.LoadGroupsAsync(FolderPath);
        string actualNewName = _groupService.GetUniqueGroupName(existingGroups, sanitized, excludeGroupName: oldName);

        SuppressGroupFileWatcher(oldName);
        SuppressGroupFileWatcher(actualNewName);

        await _groupService.RenameGroupFileAsync(FolderPath, oldName, actualNewName);

        // Update the group view model
        GroupViewModel? group = Groups.FirstOrDefault(g => g.Name == oldName);
        if (group != null)
        {
            group.Name = actualNewName;
        }
    }

    public async Task DeleteGroupAsync(string groupName)
    {
        GroupViewModel? group = Groups.FirstOrDefault(g => g.Name == groupName);
        if (group == null) return;

        // Move all items from group to ungrouped
        var itemsToMove = group.Items.ToList();
        foreach (KanbanItemViewModel? item in itemsToMove)
        {
            await MoveItemToGroupAsync(item.FileName, null);
        }

        SuppressGroupFileWatcher(groupName);
        await _groupService.DeleteGroupFileAsync(FolderPath, groupName);
        Groups.Remove(group);
    }

    public async Task MoveItemToGroupAsync(string itemFileName, string? groupName)
    {
        // Suppress affected group files
        foreach (GroupViewModel group in Groups)
        {
            if (group.Items.Any(i => i.FileName == itemFileName))
            {
                SuppressGroupFileWatcher(group.Name);
                break;
            }
        }
        if (groupName != null)
        {
            SuppressGroupFileWatcher(groupName);
        }

        if (groupName == null)
        {
            // Move to ungrouped
            await _groupService.RemoveItemFromGroupAsync(FolderPath, itemFileName);
        }
        else
        {
            // Move to specified group
            await _groupService.AddItemToGroupAsync(FolderPath, groupName, itemFileName);
        }

        // Update UI organization
        KanbanItemViewModel? item = Items.FirstOrDefault(i => i.FileName == itemFileName);
        if (item != null)
        {
            // Remove from current location
            UngroupedItems.Remove(item);
            foreach (GroupViewModel group in Groups)
            {
                group.Items.Remove(item);
            }
            
            // Add to new location
            if (groupName == null)
            {
                UngroupedItems.Add(item);
            }
            else
            {
                GroupViewModel? targetGroup = Groups.FirstOrDefault(g => g.Name == groupName);
                targetGroup?.Items.Add(item);
            }
        }
    }

    public async Task ReorderGroupsAsync()
    {
        foreach (GroupViewModel group in Groups)
        {
            SuppressGroupFileWatcher(group.Name);
        }

        var orderedNames = Groups.Select(g => g.Name).ToList();
        await _groupService.ReorderGroupsAsync(FolderPath, orderedNames);
    }

    public async Task MoveGroupFromColumnAsync(string groupName, ColumnViewModel sourceColumn)
    {
        try
        {
            // Load the group from the source column
            List<Group> sourceGroups = await _groupService.LoadGroupsAsync(sourceColumn.FolderPath);
            Group? sourceGroup = sourceGroups.FirstOrDefault(g => g.Name == groupName);
            if (sourceGroup == null) return;

            // Determine a unique group name in the target column
            List<Group> targetGroups = await _groupService.LoadGroupsAsync(FolderPath);
            string actualName = _groupService.GetUniqueGroupName(targetGroups, groupName);
            int nextSortOrder = targetGroups.Count > 0 ? targetGroups.Max(g => g.SortOrder) + 1 : 0;

            // Move each item file from source to target column
            var movedFileNames = new List<string>();
            GroupViewModel? sourceGroupVm = sourceColumn.Groups.FirstOrDefault(g => g.Name == groupName);
            var itemsToMove = sourceGroupVm?.Items.ToList() ?? [];

            foreach (KanbanItemViewModel item in itemsToMove)
            {
                _fileWatcherService?.SuppressNextEvent(item.FilePath);
                var expectedNewPath = Path.Combine(FolderPath, item.FileName);
                _fileWatcherService?.SuppressNextEvent(expectedNewPath);

                var newFilePath = await _fileSystemService.MoveItemAsync(item.FilePath, FolderPath);
                string newFileName = Path.GetFileName(newFilePath);

                // Remove from source column UI
                sourceColumn.RemoveItem(item);

                // Update item state
                item.UpdateParentColumn(this);
                item.UpdateFilePath(newFilePath);

                // Add to target column
                Items.Add(item);

                movedFileNames.Add(newFileName);
            }

            // Delete the group file from the source column
            sourceColumn.SuppressGroupFileWatcher(groupName);
            await _groupService.DeleteGroupFileAsync(sourceColumn.FolderPath, groupName);
            if (sourceGroupVm != null)
            {
                sourceColumn.Groups.Remove(sourceGroupVm);
            }

            // Create the group file in the target column with the moved items
            SuppressGroupFileWatcher(actualName);
            var newGroup = new Group
            {
                Name = actualName,
                SortOrder = nextSortOrder,
                ItemFileNames = movedFileNames,
                IsCollapsed = sourceGroup.IsCollapsed
            };
            await _groupService.SaveGroupAsync(FolderPath, newGroup);

            // Create the group ViewModel in the target column and populate it
            var newGroupVm = new GroupViewModel(actualName) { IsCollapsed = sourceGroup.IsCollapsed };
            newGroupVm.RenameRequested += (sender, e) => GroupRenameRequested?.Invoke(this, newGroupVm);
            newGroupVm.DeleteRequested += (sender, e) => GroupDeleteRequested?.Invoke(this, newGroupVm);

            foreach (KanbanItemViewModel item in itemsToMove)
            {
                newGroupVm.Items.Add(item);
            }
            Groups.Add(newGroupVm);

            // Update item order configs for both columns
            await sourceColumn.UpdateItemOrderAsync();
            await UpdateItemOrderAsync();
        }
        catch (UnauthorizedAccessException)
        {
            _notificationService?.ShowNotification("Permission Denied",
                $"Cannot move group '{groupName}'. Check file permissions.",
                InfoBarSeverity.Error);
        }
        catch (IOException ex)
        {
            _notificationService?.ShowNotification("Error Moving Group",
                ex.Message,
                InfoBarSeverity.Error);
        }
    }

    public async Task Refresh()
    {
        await LoadItemsAsync();
    }

    public void SuppressGroupFileWatcher(string groupName)
    {
        _fileWatcherService?.SuppressNextEvent(_groupService.GetGroupFilePath(FolderPath, groupName));
    }
}
