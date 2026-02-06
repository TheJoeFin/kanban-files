using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI;
using Windows.UI;
using Windows.ApplicationModel.DataTransfer;
using System.Text.Json;

namespace KanbanFiles.Controls;

public sealed partial class KanbanItemCardControl : UserControl
{
    public ViewModels.KanbanItemViewModel? ViewModel => DataContext as ViewModels.KanbanItemViewModel;

    public KanbanItemCardControl()
    {
        this.InitializeComponent();
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        
        if (DataContext is ViewModels.KanbanItemViewModel viewModel)
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

        var json = JsonSerializer.Serialize(dragPayload);
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

    private void CardBorder_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border border)
        {
            border.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Color.FromArgb(255, 243, 243, 243));
        }
    }

    private void CardBorder_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border border)
        {
            border.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Color.FromArgb(255, 255, 255, 255));
        }
    }

    private void OpenItem_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.KanbanItemViewModel viewModel)
        {
            viewModel.OpenDetailCommand.Execute(null);
        }
    }

    private void RenameItem_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.KanbanItemViewModel viewModel)
        {
            viewModel.RenameCommand.Execute(null);
        }
    }

    private void DeleteItem_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.KanbanItemViewModel viewModel)
        {
            viewModel.DeleteCommand.Execute(null);
        }
    }

    private async void OnRenameRequested(object? sender, EventArgs e)
    {
        if (DataContext is not ViewModels.KanbanItemViewModel viewModel) return;

        var textBox = new TextBox
        {
            Text = viewModel.Title,
            MinWidth = 300
        };
        textBox.SelectAll();

        var dialog = new ContentDialog
        {
            Title = "Rename Item",
            Content = textBox,
            PrimaryButtonText = "Rename",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            var newTitle = textBox.Text?.Trim() ?? string.Empty;
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
        if (DataContext is not ViewModels.KanbanItemViewModel viewModel) return;

        var dialog = new ContentDialog
        {
            Title = "Delete Item",
            Content = $"Are you sure you want to delete \"{viewModel.Title}\"?",
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
