using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using System.Text.Json;
using KanbanFiles.Models;
using KanbanFiles.Services;

namespace KanbanFiles.Controls;

public sealed partial class ColumnControl : UserControl
{
    private GroupService? _groupService;

    private bool _eventsSubscribed = false;

    public ColumnControl()
    {
        this.InitializeComponent();
        this.Loaded += ColumnControl_Loaded;
        this.Unloaded += ColumnControl_Unloaded;
    }

    private void ColumnControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (_eventsSubscribed) return;

        if (DataContext is ViewModels.ColumnViewModel viewModel)
        {
            // Initialize GroupService if needed
            if (_groupService == null)
            {
                _groupService = new GroupService();
            }

            viewModel.AddItemRequested += OnAddItemRequested;
            viewModel.RenameRequested += OnRenameRequested;
            viewModel.DeleteRequested += OnDeleteRequested;
            viewModel.GroupRenameRequested += OnGroupRenameRequested;
            viewModel.GroupDeleteRequested += OnGroupDeleteRequested;
            
            _eventsSubscribed = true;
        }
    }

    private void ColumnControl_Unloaded(object sender, RoutedEventArgs e)
    {
        if (!_eventsSubscribed) return;

        if (DataContext is ViewModels.ColumnViewModel viewModel)
        {
            viewModel.AddItemRequested -= OnAddItemRequested;
            viewModel.RenameRequested -= OnRenameRequested;
            viewModel.DeleteRequested -= OnDeleteRequested;
            viewModel.GroupRenameRequested -= OnGroupRenameRequested;
            viewModel.GroupDeleteRequested -= OnGroupDeleteRequested;
            
            _eventsSubscribed = false;
        }
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
    }

    private void NameTextBlock_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (DataContext is ViewModels.ColumnViewModel viewModel)
        {
            viewModel.RenameColumn();
        }
    }

    private void RenameMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.ColumnViewModel viewModel)
        {
            viewModel.RenameColumn();
        }
    }

    private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.ColumnViewModel viewModel)
        {
            viewModel.DeleteColumn();
        }
    }

    private void AddItemButton_Click(object sender, RoutedEventArgs e)
    {
        // Show inline entry, hide button
        AddItemButton.Visibility = Visibility.Collapsed;
        AddItemPanel.Visibility = Visibility.Visible;
        AddItemTextBox.Text = string.Empty;
        AddItemTextBox.Focus(FocusState.Programmatic);
    }

    private void AddItemTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            e.Handled = true;
            AddItemConfirm_Click(sender, e);
        }
        else if (e.Key == Windows.System.VirtualKey.Escape)
        {
            e.Handled = true;
            AddItemCancel_Click(sender, e);
        }
    }

    private async void AddItemConfirm_Click(object sender, RoutedEventArgs e)
    {
        var title = AddItemTextBox.Text?.Trim() ?? string.Empty;
        
        if (string.IsNullOrWhiteSpace(title))
        {
            await ShowErrorAsync("Item title cannot be empty.");
            return;
        }

        if (DataContext is ViewModels.ColumnViewModel viewModel)
        {
            try
            {
                await viewModel.CreateItemAsync(title);
                
                // Hide inline entry, show button
                AddItemPanel.Visibility = Visibility.Collapsed;
                AddItemButton.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                await ShowErrorAsync($"Failed to create item: {ex.Message}");
            }
        }
    }

    private void AddItemCancel_Click(object sender, RoutedEventArgs e)
    {
        // Hide inline entry, show button
        AddItemPanel.Visibility = Visibility.Collapsed;
        AddItemButton.Visibility = Visibility.Visible;
    }

    private void OnAddItemRequested(object? sender, EventArgs e)
    {
        // Activate inline entry UI (same as button click)
        AddItemButton.Visibility = Visibility.Collapsed;
        AddItemPanel.Visibility = Visibility.Visible;
        AddItemTextBox.Text = string.Empty;
        AddItemTextBox.Focus(FocusState.Programmatic);
    }

    private async void OnRenameRequested(object? sender, string currentName)
    {
        if (DataContext is not ViewModels.ColumnViewModel viewModel) return;

        var textBox = new TextBox
        {
            Text = currentName,
            MinWidth = 300
        };
        textBox.SelectAll();

        var dialog = new ContentDialog
        {
            Title = "Rename Column",
            Content = textBox,
            PrimaryButtonText = "Rename",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            var newName = textBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(newName))
            {
                await ShowErrorAsync("Column name cannot be empty.");
                return;
            }

            try
            {
                await viewModel.RenameColumnAsync(newName);
            }
            catch (Exception ex)
            {
                await ShowErrorAsync($"Failed to rename column: {ex.Message}");
            }
        }
    }

    private async void OnDeleteRequested(object? sender, string folderPath)
    {
        if (DataContext is not ViewModels.ColumnViewModel viewModel) return;

        var dialog = new ContentDialog
        {
            Title = "Delete Column",
            Content = $"Are you sure you want to delete \"{viewModel.Name}\" and all its items?",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            try
            {
                await viewModel.DeleteColumnAsync();
            }
            catch (Exception ex)
            {
                await ShowErrorAsync($"Failed to delete column: {ex.Message}");
            }
        }
    }

    private async Task ShowErrorAsync(string message)
    {
        var dialog = new ContentDialog
        {
            Title = "Error",
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = this.XamlRoot
        };
        await dialog.ShowAsync();
    }

    private async void OnGroupRenameRequested(object? sender, ViewModels.GroupViewModel groupViewModel)
    {
        if (DataContext is not ViewModels.ColumnViewModel columnViewModel) return;

        var textBox = new TextBox
        {
            Text = groupViewModel.Name,
            MinWidth = 300
        };
        textBox.SelectAll();

        var dialog = new ContentDialog
        {
            Title = "Rename Group",
            Content = textBox,
            PrimaryButtonText = "Rename",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            var newName = textBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(newName))
            {
                await ShowErrorAsync("Group name cannot be empty.");
                return;
            }

            try
            {
                await columnViewModel.RenameGroupAsync(groupViewModel.Name, newName);
            }
            catch (Exception ex)
            {
                await ShowErrorAsync($"Failed to rename group: {ex.Message}");
            }
        }
    }

    private async void OnGroupDeleteRequested(object? sender, ViewModels.GroupViewModel groupViewModel)
    {
        if (DataContext is not ViewModels.ColumnViewModel columnViewModel) return;

        var dialog = new ContentDialog
        {
            Title = "Delete Group",
            Content = $"Are you sure you want to delete \"{groupViewModel.Name}\"? Items will be moved to ungrouped.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            try
            {
                await columnViewModel.DeleteGroupAsync(groupViewModel.Name);
            }
            catch (Exception ex)
            {
                await ShowErrorAsync($"Failed to delete group: {ex.Message}");
            }
        }
    }

    private async void CreateGroupMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ViewModels.ColumnViewModel viewModel) return;

        var textBox = new TextBox
        {
            PlaceholderText = "Enter group name...",
            MinWidth = 300
        };

        var dialog = new ContentDialog
        {
            Title = "Create Group",
            Content = textBox,
            PrimaryButtonText = "Create",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            var groupName = textBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(groupName))
            {
                await ShowErrorAsync("Group name cannot be empty.");
                return;
            }

            try
            {
                await viewModel.CreateGroupAsync(groupName);
            }
            catch (Exception ex)
            {
                await ShowErrorAsync($"Failed to create group: {ex.Message}");
            }
        }
    }

    private void ItemsControl_DragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.Text))
        {
            e.AcceptedOperation = DataPackageOperation.Move;
            e.DragUIOverride.Caption = "Move item";
            e.DragUIOverride.IsCaptionVisible = true;
            e.DragUIOverride.IsGlyphVisible = true;
        }
        else if (e.DataView.Contains("KanbanGroupReorder"))
        {
            e.AcceptedOperation = DataPackageOperation.Move;
            e.DragUIOverride.Caption = "Reorder group";
            e.DragUIOverride.IsCaptionVisible = true;
            e.DragUIOverride.IsGlyphVisible = true;
        }
        else
        {
            e.AcceptedOperation = DataPackageOperation.None;
        }
    }

    private async void ItemsControl_Drop(object sender, DragEventArgs e)
    {
        if (DataContext is not ViewModels.ColumnViewModel targetColumnViewModel)
        {
            return;
        }

        try
        {
            // Handle group reorder
            if (e.DataView.Contains("KanbanGroupReorder"))
            {
                await HandleGroupReorderDropAsync(e);
                return;
            }

            // Handle item drop
            if (!e.DataView.Contains(StandardDataFormats.Text))
            {
                return;
            }

            var jsonText = await e.DataView.GetTextAsync();
            if (string.IsNullOrWhiteSpace(jsonText))
            {
                return;
            }

            DragPayload? dragPayload;
            try
            {
                dragPayload = JsonSerializer.Deserialize<DragPayload>(jsonText);
            }
            catch (JsonException)
            {
                // Not our drag payload format, ignore
                return;
            }

            if (dragPayload == null || string.IsNullOrEmpty(dragPayload.FilePath))
            {
                return;
            }

            // Detect drop zone (ungrouped or specific group)
            string? targetGroupName = GetDropTargetGroup(e);

            // Check if this is a move between columns or reorder within same column
            bool isSameColumn = dragPayload.SourceColumnPath.Equals(targetColumnViewModel.FolderPath, StringComparison.OrdinalIgnoreCase);

            if (isSameColumn)
            {
                // Reorder within the same column or move between groups
                await HandleItemReorderOrGroupMoveAsync(targetColumnViewModel, dragPayload, targetGroupName);
            }
            else
            {
                // Move between different columns
                await HandleMoveAsync(targetColumnViewModel, dragPayload, GetDropIndex(e));
                
                // If dropping into a group, also assign group membership
                if (targetGroupName != null)
                {
                    var fileName = Path.GetFileName(dragPayload.FilePath);
                    await targetColumnViewModel.MoveItemToGroupAsync(fileName, targetGroupName);
                }
            }
        }
        catch (Exception ex)
        {
            await ShowErrorAsync($"Failed to drop item: {ex.Message}");
        }
    }

    private void GroupItemsControl_Loaded(object sender, RoutedEventArgs e)
    {
        // Wire up DragOver and Drop handlers for the nested ItemsControl in groups
        if (sender is ItemsControl itemsControl)
        {
            itemsControl.DragOver += ItemsControl_DragOver;
            itemsControl.Drop += ItemsControl_Drop;
        }
    }

    private async Task HandleGroupReorderDropAsync(DragEventArgs e)
    {
        if (DataContext is not ViewModels.ColumnViewModel viewModel) return;

        try
        {
            var groupName = await e.DataView.GetTextAsync("KanbanGroupReorder");
            if (string.IsNullOrWhiteSpace(groupName)) return;

            // Find the source group
            var sourceGroup = viewModel.Groups.FirstOrDefault(g => g.Name == groupName);
            if (sourceGroup == null) return;

            // Calculate drop index based on Y position
            int dropIndex = GetGroupDropIndex(e);
            int currentIndex = viewModel.Groups.IndexOf(sourceGroup);

            // Adjust drop index if moving within same list
            if (dropIndex > currentIndex)
            {
                dropIndex--;
            }

            // Don't move if dropping at same position
            if (currentIndex == dropIndex)
            {
                return;
            }

            // Move the group in the collection
            viewModel.Groups.Move(currentIndex, dropIndex);

            // Update the config
            await UpdateGroupOrderAsync(viewModel);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync($"Failed to reorder group: {ex.Message}");
        }
    }

    private async Task HandleItemReorderOrGroupMoveAsync(ViewModels.ColumnViewModel columnViewModel, DragPayload dragPayload, string? targetGroupName)
    {
        var fileName = Path.GetFileName(dragPayload.FilePath);
        
        // Find the source item
        var sourceItem = columnViewModel.Items.FirstOrDefault(i => i.FilePath.Equals(dragPayload.FilePath, StringComparison.OrdinalIgnoreCase));
        if (sourceItem == null) return;

        // Move to target group (or ungrouped if null)
        await columnViewModel.MoveItemToGroupAsync(fileName, targetGroupName);
    }

    private string? GetDropTargetGroup(DragEventArgs e)
    {
        if (DataContext is not ViewModels.ColumnViewModel viewModel) return null;

        // Get the position relative to the GroupsControlElement
        if (GroupsControlElement == null) return null;

        var position = e.GetPosition(GroupsControlElement);

        // Iterate through groups to find which one the pointer is over
        for (int i = 0; i < GroupsControlElement.Items.Count; i++)
        {
            var container = GroupsControlElement.ContainerFromIndex(i) as FrameworkElement;
            if (container != null)
            {
                var containerPosition = container.TransformToVisual(GroupsControlElement).TransformPoint(new Windows.Foundation.Point(0, 0));
                var containerHeight = container.ActualHeight;

                // Check if pointer is within this group's bounds
                if (position.Y >= containerPosition.Y && position.Y < containerPosition.Y + containerHeight)
                {
                    var group = viewModel.Groups[i];
                    return group.Name;
                }
            }
        }

        // Not over any group, so it's in the ungrouped area
        return null;
    }

    private int GetGroupDropIndex(DragEventArgs e)
    {
        if (DataContext is not ViewModels.ColumnViewModel viewModel)
        {
            return 0;
        }

        if (GroupsControlElement == null) return viewModel.Groups.Count;

        // Get the position relative to the GroupsControlElement
        var position = e.GetPosition(GroupsControlElement);

        // Find which group the pointer is over
        int dropIndex = viewModel.Groups.Count;

        for (int i = 0; i < GroupsControlElement.Items.Count; i++)
        {
            var container = GroupsControlElement.ContainerFromIndex(i) as FrameworkElement;
            if (container != null)
            {
                var containerPosition = container.TransformToVisual(GroupsControlElement).TransformPoint(new Windows.Foundation.Point(0, 0));
                var containerHeight = container.ActualHeight;

                // If pointer is in the top half of this group, insert before it
                if (position.Y < containerPosition.Y + (containerHeight / 2))
                {
                    dropIndex = i;
                    break;
                }
            }
        }

        return dropIndex;
    }

    private async Task UpdateGroupOrderAsync(ViewModels.ColumnViewModel viewModel)
    {
        if (_groupService == null) return;

        // Load current groups config
        var groupsConfig = await _groupService.LoadGroupsAsync(viewModel.FolderPath);
        
        // Update the order to match the current collection
        groupsConfig.Groups.Clear();
        foreach (var group in viewModel.Groups)
        {
            var groupConfig = new Group
            {
                Name = group.Name,
                IsCollapsed = group.IsCollapsed,
                ItemFileNames = group.Items.Select(i => i.FileName).ToList()
            };
            groupsConfig.Groups.Add(groupConfig);
        }

        await _groupService.SaveGroupsAsync(viewModel.FolderPath, groupsConfig);
    }

    private int GetDropIndex(DragEventArgs e)
    {
        if (DataContext is not ViewModels.ColumnViewModel viewModel)
        {
            return 0;
        }

        // Get the position relative to the UngroupedItemsControl
        var position = e.GetPosition(UngroupedItemsControlElement);
        
        // Find which item the pointer is over
        int dropIndex = viewModel.UngroupedItems.Count;
        
        for (int i = 0; i < UngroupedItemsControlElement.Items.Count; i++)
        {
            var container = UngroupedItemsControlElement.ContainerFromIndex(i) as FrameworkElement;
            if (container != null)
            {
                var containerPosition = container.TransformToVisual(UngroupedItemsControlElement).TransformPoint(new Windows.Foundation.Point(0, 0));
                var containerHeight = container.ActualHeight;
                
                // If pointer is in the top half of this item, insert before it
                if (position.Y < containerPosition.Y + (containerHeight / 2))
                {
                    dropIndex = i;
                    break;
                }
            }
        }
        
        return dropIndex;
    }

    private async Task HandleReorderAsync(ViewModels.ColumnViewModel columnViewModel, DragPayload dragPayload, int dropIndex)
    {
        // Find the source item
        var sourceItem = columnViewModel.Items.FirstOrDefault(i => i.FilePath.Equals(dragPayload.FilePath, StringComparison.OrdinalIgnoreCase));
        if (sourceItem == null)
        {
            return;
        }

        int currentIndex = columnViewModel.Items.IndexOf(sourceItem);
        if (currentIndex == -1)
        {
            return;
        }

        // Adjust drop index if moving within same list
        if (dropIndex > currentIndex)
        {
            dropIndex--;
        }

        // Don't move if dropping at same position
        if (currentIndex == dropIndex)
        {
            return;
        }

        // Move the item in the collection
        columnViewModel.Items.Move(currentIndex, dropIndex);

        // Update the config
        await columnViewModel.UpdateItemOrderAsync();
    }

    private async Task HandleMoveAsync(ViewModels.ColumnViewModel targetColumnViewModel, DragPayload dragPayload, int dropIndex)
    {
        try
        {
            // Get MainViewModel to find the source column
            var mainViewModel = GetMainViewModel();
            if (mainViewModel == null)
            {
                await ShowErrorAsync("Unable to access main board view.");
                return;
            }

            // Find the source column
            var sourceColumn = mainViewModel.Columns.FirstOrDefault(c => 
                c.FolderPath.Equals(dragPayload.SourceColumnPath, StringComparison.OrdinalIgnoreCase));
            
            if (sourceColumn == null)
            {
                await ShowErrorAsync("Source column not found.");
                return;
            }

            // Find the source item
            var sourceItem = sourceColumn.Items.FirstOrDefault(i => 
                i.FilePath.Equals(dragPayload.FilePath, StringComparison.OrdinalIgnoreCase));
            
            if (sourceItem == null)
            {
                await ShowErrorAsync("Source item not found.");
                return;
            }

            // Perform the move
            await targetColumnViewModel.MoveItemToColumnAsync(sourceItem, sourceColumn, dropIndex);
            
            // Optional: Show brief success message (can be removed if too noisy)
            // await ShowSuccessAsync($"Moved '{sourceItem.Title}' to '{targetColumnViewModel.Name}'");
        }
        catch (IOException ioEx)
        {
            await ShowErrorAsync($"File operation failed: {ioEx.Message}");
        }
        catch (UnauthorizedAccessException authEx)
        {
            await ShowErrorAsync($"Access denied: {authEx.Message}");
        }
        catch (Exception ex)
        {
            await ShowErrorAsync($"Failed to move item: {ex.Message}");
        }
    }

    private ViewModels.MainViewModel? GetMainViewModel()
    {
        // Walk up the visual tree to find MainPage
        DependencyObject? current = this;
        
        while (current != null)
        {
            if (current is Views.MainPage mainPage)
            {
                return mainPage.DataContext as ViewModels.MainViewModel;
            }
            
            current = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(current);
        }
        
        return null;
    }

    private void Header_DragStarting(UIElement sender, DragStartingEventArgs args)
    {
        if (DataContext is not ViewModels.ColumnViewModel viewModel)
        {
            args.Cancel = true;
            return;
        }

        // Set the payload - just the folder path as text
        args.Data.SetText(viewModel.FolderPath);
        args.Data.RequestedOperation = DataPackageOperation.Move;

        // Reduce opacity during drag
        this.Opacity = 0.5;
    }

    private void Header_DropCompleted(UIElement sender, DropCompletedEventArgs args)
    {
        // Restore opacity after drag completes
        this.Opacity = 1.0;
    }
}
