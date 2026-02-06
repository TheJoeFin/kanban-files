using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using KanbanFiles.ViewModels;

namespace KanbanFiles.Controls;

public sealed partial class GroupHeaderControl : UserControl
{
    private GroupViewModel? ViewModel => DataContext as GroupViewModel;

    public GroupHeaderControl()
    {
        this.InitializeComponent();
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        
        if (ViewModel != null)
        {
            UpdateChevronIcon();
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GroupViewModel.IsCollapsed))
        {
            UpdateChevronIcon();
        }
    }

    private void UpdateChevronIcon()
    {
        if (ViewModel != null && ChevronIcon != null)
        {
            ChevronIcon.Glyph = ViewModel.IsCollapsed ? "\uE76C" : "\uE70D";
        }
    }

    private void ChevronButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.IsCollapsed = !ViewModel.IsCollapsed;
        }
    }

    private void NameTextBlock_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        EnterEditMode();
    }

    private void RenameMenuItem_Click(object sender, RoutedEventArgs e)
    {
        EnterEditMode();
    }

    private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ViewModel?.Delete();
    }

    private void EnterEditMode()
    {
        if (ViewModel == null) return;

        NameTextBlock.Visibility = Visibility.Collapsed;
        NameEditTextBox.Visibility = Visibility.Visible;
        NameEditTextBox.Text = ViewModel.Name;
        NameEditTextBox.Focus(FocusState.Programmatic);
        NameEditTextBox.SelectAll();
    }

    private void ExitEditMode()
    {
        NameEditTextBox.Visibility = Visibility.Collapsed;
        NameTextBlock.Visibility = Visibility.Visible;
    }

    private void NameEditTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        SaveNameChange();
        ExitEditMode();
    }

    private void NameEditTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            SaveNameChange();
            ExitEditMode();
            e.Handled = true;
        }
        else if (e.Key == Windows.System.VirtualKey.Escape)
        {
            NameEditTextBox.Text = ViewModel?.Name ?? string.Empty;
            ExitEditMode();
            e.Handled = true;
        }
    }

    private async void SaveNameChange()
    {
        if (ViewModel == null) return;

        var newName = NameEditTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(newName))
        {
            NameEditTextBox.Text = ViewModel.Name;
            return;
        }

        if (newName != ViewModel.Name)
        {
            // Trigger rename through event - the parent will handle the actual rename
            ViewModel.Rename();
        }
    }

    private void HeaderBorder_DragStarting(UIElement sender, DragStartingEventArgs args)
    {
        if (ViewModel == null)
        {
            args.Cancel = true;
            return;
        }

        args.Data.Properties.Add("KanbanGroupReorder", ViewModel.Name);
        args.Data.RequestedOperation = DataPackageOperation.Move;
        HeaderBorder.Opacity = 0.5;
    }

    private void HeaderBorder_DropCompleted(UIElement sender, DropCompletedEventArgs args)
    {
        HeaderBorder.Opacity = 1.0;
    }

    private void HeaderBorder_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        VisualStateManager.GoToState(this, "PointerOver", true);
    }

    private void HeaderBorder_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        VisualStateManager.GoToState(this, "Normal", true);
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
