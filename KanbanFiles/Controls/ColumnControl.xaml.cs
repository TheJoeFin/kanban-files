using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

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
}
