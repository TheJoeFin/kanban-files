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

        foreach (var item in column.Items)
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
                var itemViewModel = new KanbanItemViewModel(newItem, _fileSystemService, _boardConfigService, _board, this, _fileWatcherService, _notificationService);
                Items.Add(itemViewModel);
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
            var columnConfig = _board.Columns.FirstOrDefault(c => Path.Combine(_board.RootPath, c.FolderName) == Path.GetFileName(oldFolderPath));
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
            var columnConfig = _board.Columns.FirstOrDefault(c => Path.Combine(_board.RootPath, c.FolderName) == Path.GetFileName(FolderPath));
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

            // Remove from source column
            sourceColumnViewModel.Items.Remove(sourceItem);

            // Update the item's parent column and file path
            sourceItem.UpdateParentColumn(this);
            sourceItem.UpdateFilePath(newFilePath);

            // Insert at target index in this column
            if (targetIndex < 0 || targetIndex > Items.Count)
            {
                targetIndex = Items.Count;
            }
            Items.Insert(targetIndex, sourceItem);

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
        await LoadGroupsAsync();
        
        // Enumerate items from file system
        var items = await _fileSystemService.EnumerateItemsAsync(FolderPath);
        
        // Clear existing items
        Items.Clear();
        UngroupedItems.Clear();
        foreach (var group in Groups)
        {
            group.Items.Clear();
        }
        
        // Create ViewModels and organize into groups
        foreach (var item in items)
        {
            var itemViewModel = new KanbanItemViewModel(item, _fileSystemService, _boardConfigService, _board, this, _fileWatcherService, _notificationService);
            Items.Add(itemViewModel);
            
            // Organize into groups based on group membership
            var group = Groups.FirstOrDefault(g => g.Name == item.GroupName);
            if (group != null)
            {
                group.Items.Add(itemViewModel);
            }
            else
            {
                UngroupedItems.Add(itemViewModel);
            }
        }
    }

    public async Task LoadGroupsAsync()
    {
        var groupsConfig = await _groupService.LoadGroupsAsync(FolderPath);
        
        Groups.Clear();
        
        foreach (var group in groupsConfig.Groups)
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
        await _groupService.CreateGroupAsync(FolderPath, groupName);
        await LoadGroupsAsync();
    }

    public async Task RenameGroupAsync(string oldName, string newName)
    {
        await _groupService.RenameGroupAsync(FolderPath, oldName, newName);
        
        // Update the group view model
        var group = Groups.FirstOrDefault(g => g.Name == oldName);
        if (group != null)
        {
            group.Name = newName;
        }
    }

    public async Task DeleteGroupAsync(string groupName)
    {
        var group = Groups.FirstOrDefault(g => g.Name == groupName);
        if (group == null) return;
        
        // Move all items from group to ungrouped
        var itemsToMove = group.Items.ToList();
        foreach (var item in itemsToMove)
        {
            await MoveItemToGroupAsync(item.FileName, null);
        }
        
        await _groupService.DeleteGroupAsync(FolderPath, groupName);
        Groups.Remove(group);
    }

    public async Task MoveItemToGroupAsync(string itemFileName, string? groupName)
    {
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
        var item = Items.FirstOrDefault(i => i.FileName == itemFileName);
        if (item != null)
        {
            // Remove from current location
            UngroupedItems.Remove(item);
            foreach (var group in Groups)
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
                var targetGroup = Groups.FirstOrDefault(g => g.Name == groupName);
                targetGroup?.Items.Add(item);
            }
        }
    }

    public async Task Refresh()
    {
        await LoadItemsAsync();
    }
}
