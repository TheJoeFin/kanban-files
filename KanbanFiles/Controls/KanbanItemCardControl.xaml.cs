using Microsoft.UI.Xaml.Input;
using System.Text.Json;
using Windows.ApplicationModel.DataTransfer;

namespace KanbanFiles.Controls;

public sealed partial class KanbanItemCardControl : UserControl
{
    public KanbanItemViewModel? ViewModel => DataContext as KanbanItemViewModel;

    public KanbanItemCardControl()
    {
        this.InitializeComponent();
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (DataContext is KanbanItemViewModel viewModel)
        {
            viewModel.DeleteRequested += OnDeleteRequested;
            viewModel.RenameRequested += OnRenameRequested;
        }
    }

    private void CardBorder_DragStarting(UIElement sender, DragStartingEventArgs args)
    {
        if (ViewModel == null) return;

        var dragPayload = new
        {
            FilePath = ViewModel.FilePath,
            SourceColumnPath = ViewModel.SourceColumnPath,
            FileName = ViewModel.FileName
        };

        string json = JsonSerializer.Serialize(dragPayload);
        args.Data.SetText(json);
        args.Data.RequestedOperation = DataPackageOperation.Move;

        // Reduce opacity during drag
        CardBorder.Opacity = 0.5;
    }

    private void CardBorder_DropCompleted(UIElement sender, DropCompletedEventArgs args)
    {
        // Restore opacity after drag
        CardBorder.Opacity = 1.0;
    }

    private void CardBorder_DragOver(object sender, DragEventArgs e)
    {
        // Allow text data (our kanban item drag payload)
        if (e.DataView.Contains(StandardDataFormats.Text))
        {
            e.AcceptedOperation = DataPackageOperation.Move;
            e.DragUIOverride.Caption = "Move item";
            // Removed e.Handled = true to allow event bubbling to parent ItemsControl
        }
        else
        {
            e.AcceptedOperation = DataPackageOperation.None;
        }
    }

    private void CardBorder_Drop(object sender, DragEventArgs e)
    {
        // Let the parent ItemsControl handle the actual drop logic
        // This handler just allows the drop to propagate
        // The ColumnControl will handle the actual file movement
    }

    private void CardBorder_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border border)
        {
            // Use theme-aware hover background
            border.Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"];
        }
    }

    private void CardBorder_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border border)
        {
            // Restore original theme-aware background
            border.Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"];
        }
    }

    private void OpenItem_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is KanbanItemViewModel viewModel)
        {
            viewModel.OpenDetailCommand.Execute(null);
        }
    }

    private void CardBorder_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (DataContext is KanbanItemViewModel viewModel)
        {
            viewModel.OpenDetailCommand.Execute(null);
        }
    }

    private void RenameItem_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is KanbanItemViewModel viewModel)
        {
            viewModel.RenameCommand.Execute(null);
        }
    }

    private void DeleteItem_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is KanbanItemViewModel viewModel)
        {
            viewModel.DeleteCommand.Execute(null);
        }
    }

    private async void OnRenameRequested(object? sender, EventArgs e)
    {
        if (DataContext is not KanbanItemViewModel viewModel) return;

        TextBox textBox = new()
        {
            Text = viewModel.Title,
            MinWidth = 300
        };
        textBox.SelectAll();

        ContentDialog dialog = new()
        {
            Title = "Rename Item",
            Content = textBox,
            PrimaryButtonText = "Rename",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };

        ContentDialogResult result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            string newTitle = textBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(newTitle))
            {
                await ShowErrorAsync("Item title cannot be empty.");
                return;
            }

            try
            {
                await viewModel.RenameAsync(newTitle);
            }
            catch (Exception ex)
            {
                await ShowErrorAsync($"Failed to rename item: {ex.Message}");
            }
        }
    }

    private async void OnDeleteRequested(object? sender, EventArgs e)
    {
        if (DataContext is not KanbanItemViewModel viewModel) return;

        ContentDialog dialog = new()
        {
            Title = "Delete Item",
            Content = $"Are you sure you want to delete \"{viewModel.Title}\"?",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };

        ContentDialogResult result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            try
            {
                await viewModel.DeleteAsync();
            }
            catch (Exception ex)
            {
                await ShowErrorAsync($"Failed to delete item: {ex.Message}");
            }
        }
    }

    private async Task ShowErrorAsync(string message)
    {
        ContentDialog dialog = new()
        {
            Title = "Error",
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = this.XamlRoot
        };
        await dialog.ShowAsync();
    }

    private void CardContextMenu_Opening(object sender, object e)
    {
        MoveToGroupSubMenu.Items.Clear();

        if (ViewModel?.ParentColumn is not { } column || column.Groups.Count == 0)
        {
            MoveToGroupSubMenu.Visibility = Visibility.Collapsed;
            GroupMenuSeparator.Visibility = Visibility.Collapsed;
            return;
        }

        MoveToGroupSubMenu.Visibility = Visibility.Visible;
        GroupMenuSeparator.Visibility = Visibility.Visible;

        // Determine which group the item currently belongs to
        string? currentGroupName = null;
        foreach (GroupViewModel group in column.Groups)
        {
            if (group.Items.Contains(ViewModel))
            {
                currentGroupName = group.Name;
                break;
            }
        }

        // Add "Ungrouped" option if the item is currently in a group
        if (currentGroupName != null)
        {
            var ungroupedItem = new MenuFlyoutItem
            {
                Text = "Ungrouped",
                Icon = new FontIcon { Glyph = "\uE8B7" }
            };
            ungroupedItem.Click += (s, args) => MoveToGroup_Click(null);
            MoveToGroupSubMenu.Items.Add(ungroupedItem);
            MoveToGroupSubMenu.Items.Add(new MenuFlyoutSeparator());
        }

        // Add each group as a menu option
        foreach (GroupViewModel group in column.Groups)
        {
            var menuItem = new MenuFlyoutItem { Text = group.Name };
            if (group.Name == currentGroupName)
            {
                menuItem.Icon = new FontIcon { Glyph = "\uE73E" };
                menuItem.IsEnabled = false;
            }
            string targetGroupName = group.Name;
            menuItem.Click += (s, args) => MoveToGroup_Click(targetGroupName);
            MoveToGroupSubMenu.Items.Add(menuItem);
        }
    }

    private async void MoveToGroup_Click(string? targetGroupName)
    {
        if (ViewModel?.ParentColumn is not { } column) return;

        try
        {
            await column.MoveItemToGroupAsync(ViewModel.FileName, targetGroupName);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync($"Failed to move item to group: {ex.Message}");
        }
    }
}
