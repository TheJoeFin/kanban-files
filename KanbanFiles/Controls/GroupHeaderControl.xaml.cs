using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using KanbanFiles.ViewModels;
using System.Text.Json;

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

        // Find the parent ColumnViewModel to get the source column path
        string sourceColumnPath = string.Empty;
        DependencyObject? current = this;
        while (current != null)
        {
            if (current is FrameworkElement fe && fe.DataContext is ColumnViewModel columnVm)
            {
                sourceColumnPath = columnVm.FolderPath;
                break;
            }
            current = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(current);
        }

        var payload = new DragPayload
        {
            GroupName = ViewModel.Name,
            SourceColumnPath = sourceColumnPath
        };

        args.Data.SetText(JsonSerializer.Serialize(payload));
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

    private void GroupAddTagButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel == null) return;

        MenuFlyout flyout = BuildGroupTagFlyout();
        flyout.ShowAt(GroupAddTagButton);
    }

    private MenuFlyout BuildGroupTagFlyout()
    {
        MenuFlyout flyout = new();

        MainViewModel? mainVm = FindMainViewModel();
        if (mainVm?.Board == null) return flyout;

        List<TagDefinition> allTags = mainVm.TagService.GetTagDefinitions(mainVm.Board);

        if (allTags.Count > 0)
        {
            HashSet<string> currentTagNames = new(ViewModel!.Tags.Select(t => t.Name));

            foreach (TagDefinition tag in allTags)
            {
                ToggleMenuFlyoutItem menuItem = new()
                {
                    Text = tag.Name,
                    IsChecked = currentTagNames.Contains(tag.Name),
                    Icon = new FontIcon
                    {
                        Glyph = "\u25CF",
                        FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe UI"),
                        FontSize = 16,
                        Foreground = ParseTagColorBrush(tag.Color)
                    }
                };
                string tagName = tag.Name;
                menuItem.Click += async (s, args) =>
                {
                    try
                    {
                        await ViewModel!.ToggleTagAsync(tagName);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to toggle group tag: {ex.Message}");
                    }
                };
                flyout.Items.Add(menuItem);
            }

            flyout.Items.Add(new MenuFlyoutSeparator());
        }

        MenuFlyoutItem createItem = new()
        {
            Text = "Create new tag...",
            Icon = new FontIcon { Glyph = "\uE710" }
        };
        createItem.Click += async (s, args) =>
        {
            try
            {
                await ShowCreateTagDialogAsync(mainVm);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to create tag: {ex.Message}");
            }
        };
        flyout.Items.Add(createItem);

        return flyout;
    }

    private async Task ShowCreateTagDialogAsync(MainViewModel mainVm)
    {
        TextBox nameBox = new()
        {
            PlaceholderText = "Tag name",
            MinWidth = 260
        };

        GridView colorGrid = new()
        {
            ItemsSource = TagDefinition.DefaultColors,
            SelectedIndex = 0,
            SelectionMode = ListViewSelectionMode.Single
        };
        colorGrid.ItemTemplate = (DataTemplate)Microsoft.UI.Xaml.Markup.XamlReader.Load(
            @"<DataTemplate xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"">
                <Border Width=""28"" Height=""28"" CornerRadius=""14"" Margin=""2"">
                    <Border.Background>
                        <SolidColorBrush Color=""{Binding}"" />
                    </Border.Background>
                </Border>
            </DataTemplate>");

        StackPanel panel = new() { Spacing = 12 };
        panel.Children.Add(nameBox);
        panel.Children.Add(new TextBlock
        {
            Text = "Color:",
            FontSize = 13,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
        });
        panel.Children.Add(colorGrid);

        ContentDialog dialog = new()
        {
            Title = "Create New Tag",
            Content = panel,
            PrimaryButtonText = "Create",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };

        ContentDialogResult result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            string tagName = nameBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(tagName))
            {
                await ShowErrorAsync("Tag name cannot be empty.");
                return;
            }

            string color = colorGrid.SelectedItem as string ?? TagDefinition.DefaultColors[0];
            await mainVm.CreateTagAsync(tagName, color);

            // Auto-assign to this group
            await ViewModel!.ToggleTagAsync(tagName);
        }
    }

    private static Microsoft.UI.Xaml.Media.SolidColorBrush ParseTagColorBrush(string hex)
    {
        try
        {
            hex = hex.TrimStart('#');
            byte r = Convert.ToByte(hex[..2], 16);
            byte g = Convert.ToByte(hex[2..4], 16);
            byte b = Convert.ToByte(hex[4..6], 16);
            return new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, r, g, b));
        }
        catch
        {
            return new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 52, 152, 219));
        }
    }

    private MainViewModel? FindMainViewModel()
    {
        DependencyObject? current = this;
        while (current != null)
        {
            if (current is FrameworkElement fe && fe.DataContext is MainViewModel mainVm)
            {
                return mainVm;
            }
            current = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(current);
        }

        if (App.MainWindow?.Content is FrameworkElement rootElement)
        {
            if (rootElement is Frame frame && frame.Content is FrameworkElement page && page.DataContext is MainViewModel pageMainVm)
            {
                return pageMainVm;
            }
            if (rootElement.DataContext is MainViewModel directMainVm)
            {
                return directMainVm;
            }
        }

        return null;
    }
}
