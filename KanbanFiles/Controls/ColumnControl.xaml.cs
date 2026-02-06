using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using System.Text.Json;
using KanbanFiles.Models;

namespace KanbanFiles.Controls;

public sealed partial class ColumnControl : UserControl
{
    public ColumnControl()
    {
        this.InitializeComponent();
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        
        if (DataContext is ViewModels.ColumnViewModel viewModel)
        {
            viewModel.AddItemRequested += OnAddItemRequested;
            viewModel.RenameRequested += OnRenameRequested;
            viewModel.DeleteRequested += OnDeleteRequested;
        }
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
        if (DataContext is ViewModels.ColumnViewModel viewModel)
        {
            viewModel.AddItem();
        }
    }

    private async void OnAddItemRequested(object? sender, EventArgs e)
    {
        if (DataContext is not ViewModels.ColumnViewModel viewModel) return;

        var textBox = new TextBox
        {
            PlaceholderText = "Enter item title...",
            MinWidth = 300
        };

        var dialog = new ContentDialog
        {
            Title = "New Item",
            Content = textBox,
            PrimaryButtonText = "Create",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            var title = textBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(title))
            {
                await ShowErrorAsync("Item title cannot be empty.");
                return;
            }

            try
            {
                await viewModel.CreateItemAsync(title);
            }
            catch (Exception ex)
            {
                await ShowErrorAsync($"Failed to create item: {ex.Message}");
            }
        }
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

    private void ItemsControl_DragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.Text))
        {
            e.AcceptedOperation = DataPackageOperation.Move;
            e.DragUIOverride.Caption = "Move item";
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
            // Get the drag payload
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

            // Determine drop position
            int dropIndex = GetDropIndex(e);

            // Check if this is a move between columns or reorder within same column
            bool isSameColumn = dragPayload.SourceColumnPath.Equals(targetColumnViewModel.FolderPath, StringComparison.OrdinalIgnoreCase);

            if (isSameColumn)
            {
                // Reorder within the same column
                await HandleReorderAsync(targetColumnViewModel, dragPayload, dropIndex);
            }
            else
            {
                // Move between different columns
                await HandleMoveAsync(targetColumnViewModel, dragPayload, dropIndex);
            }
        }
        catch (Exception ex)
        {
            await ShowErrorAsync($"Failed to drop item: {ex.Message}");
        }
    }

    private int GetDropIndex(DragEventArgs e)
    {
        if (DataContext is not ViewModels.ColumnViewModel viewModel)
        {
            return 0;
        }

        // Get the position relative to the ItemsControl
        var position = e.GetPosition(ItemsControlElement);
        
        // Find which item the pointer is over
        int dropIndex = viewModel.Items.Count;
        
        for (int i = 0; i < ItemsControlElement.Items.Count; i++)
        {
            var container = ItemsControlElement.ContainerFromIndex(i) as FrameworkElement;
            if (container != null)
            {
                var containerPosition = container.TransformToVisual(ItemsControlElement).TransformPoint(new Windows.Foundation.Point(0, 0));
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
