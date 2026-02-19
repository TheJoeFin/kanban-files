using Microsoft.UI.Xaml.Input;
using System.Text.Json;
using Windows.ApplicationModel.DataTransfer;

namespace KanbanFiles.Controls;

public sealed partial class KanbanItemCardControl : UserControl
{
    private bool _eventsSubscribed = false;

    public KanbanItemViewModel? ViewModel => DataContext as KanbanItemViewModel;

    public KanbanItemCardControl()
    {
        this.InitializeComponent();
        this.Loaded += KanbanItemCardControl_Loaded;
        this.Unloaded += KanbanItemCardControl_Unloaded;
    }

    private void KanbanItemCardControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (_eventsSubscribed) return;

        if (DataContext is KanbanItemViewModel viewModel)
        {
            viewModel.DeleteRequested += OnDeleteRequested;
            viewModel.RenameRequested += OnRenameRequested;
            _eventsSubscribed = true;
        }
    }

    private void KanbanItemCardControl_Unloaded(object sender, RoutedEventArgs e)
    {
        if (!_eventsSubscribed) return;

        if (DataContext is KanbanItemViewModel viewModel)
        {
            viewModel.DeleteRequested -= OnDeleteRequested;
            viewModel.RenameRequested -= OnRenameRequested;
            _eventsSubscribed = false;
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
        AddTagButton.Opacity = 1;
    }

    private void CardBorder_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border border)
        {
            // Restore original theme-aware background
            border.Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"];
        }
        AddTagButton.Opacity = 0;
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

    private void AddTagButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel == null) return;

        MenuFlyout flyout = BuildTagFlyout();
        flyout.ShowAt(AddTagButton);
    }

    private MenuFlyout BuildTagFlyout()
    {
        MenuFlyout flyout = new();

        // Find MainViewModel to get available tags
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
                        System.Diagnostics.Debug.WriteLine($"Failed to toggle tag: {ex.Message}");
                    }
                };
                flyout.Items.Add(menuItem);
            }

            flyout.Items.Add(new MenuFlyoutSeparator());
        }

        // "Create new tag" option
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

        // Color picker as a grid of preset color buttons
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

            // Auto-assign the new tag to this item
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

        // Try from the page
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
