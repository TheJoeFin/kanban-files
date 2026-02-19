using KanbanFiles.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.Storage.Pickers;
using WinRT.Interop;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Foundation;

namespace KanbanFiles.Views
{
    /// <summary>
    /// A simple page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public partial class MainPage : Page
    {
        private const string ColumnDragDataFormat = "KanbanColumnReorder";

        public MainPage()
        {
            this.InitializeComponent();
            ViewModel.OpenFolderRequested += OnOpenFolderRequested;
            ViewModel.AddColumnRequested += OnAddColumnRequested;
            ViewModel.EditFileFilterRequested += OnEditFileFilterRequested;
            ViewModel.ManageTagsRequested += OnManageTagsRequested;
            ItemDetailViewControl.CloseRequested += OnItemDetailCloseRequested;

            WeakReferenceMessenger.Default.Register<Messages.OpenItemDetailMessage>(this, (r, m) =>
            {
                var page = (MainPage)r;
                page.HandleOpenItemDetail(m.KanbanItemViewModel);
            });
        }

        private async void OnOpenFolderRequested(object? sender, EventArgs e)
        {
            try
            {
                var folderPicker = new FolderPicker
                {
                    SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                    ViewMode = PickerViewMode.List
                };
                folderPicker.FileTypeFilter.Add("*");

                var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
                InitializeWithWindow.Initialize(folderPicker, hwnd);

                StorageFolder folder = await folderPicker.PickSingleFolderAsync();
                if (folder != null)
                {
                    await ViewModel.LoadBoardAsync(folder.Path);
                }
            }
            catch (Exception ex)
            {
                ViewModel.ShowNotification(
                    "Error Opening Folder",
                    $"An unexpected error occurred: {ex.Message}",
                    InfoBarSeverity.Error);
                System.Diagnostics.Debug.WriteLine($"Error in OnOpenFolderRequested: {ex}");
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            App.NavigationService.NavigateTo(typeof(SettingsViewModel).FullName!, ViewModel.Board?.RootPath);
        }

        protected override async void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is string folderPath && !string.IsNullOrEmpty(folderPath))
            {
                try
                {
                    await ViewModel.LoadBoardAsync(folderPath);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error restoring board on navigation: {ex}");
                }
            }
        }

        private async void OnAddColumnRequested(object? sender, string e)
        {
            var dialog = new ContentDialog
            {
                Title = "New Column",
                Content = CreateTextBoxContent("Column name:", "Enter column name..."),
                PrimaryButtonText = "Create",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };

            ContentDialogResult result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var textBox = dialog.Content as TextBox;
                var columnName = textBox?.Text?.Trim() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(columnName))
                {
                    await ShowErrorDialogAsync("Column name cannot be empty.");
                    return;
                }

                // Check for duplicate
                if (ViewModel.Columns.Any(c => c.Name.Equals(columnName, StringComparison.OrdinalIgnoreCase)))
                {
                    await ShowErrorDialogAsync("A column with this name already exists.");
                    return;
                }

                try
                {
                    await ViewModel.CreateColumnAsync(columnName);
                }
                catch (Exception ex)
                {
                    await ShowErrorDialogAsync($"Failed to create column: {ex.Message}");
                }
            }
        }

        private TextBox CreateTextBoxContent(string label, string placeholder)
        {
            var stackPanel = new StackPanel();
            stackPanel.Children.Add(new TextBlock { Text = label, Margin = new Microsoft.UI.Xaml.Thickness(0, 0, 0, 8) });

            var textBox = new TextBox
            {
                PlaceholderText = placeholder,
                MinWidth = 300
            };

            return textBox;
        }

        private async void OnEditFileFilterRequested(object? sender, EventArgs e)
        {
            if (!ViewModel.IsLoaded) return;

            FileFilterConfig current = ViewModel.GetFileFilter();

            var includeBox = new TextBox
            {
                PlaceholderText = "e.g.  .md, .txt, .cs",
                Text = string.Join(", ", current.IncludeExtensions),
                MinWidth = 360,
                AcceptsReturn = false
            };

            var excludeBox = new TextBox
            {
                PlaceholderText = "e.g.  .exe, .dll, .bin",
                Text = string.Join(", ", current.ExcludeExtensions),
                MinWidth = 360,
                AcceptsReturn = false
            };

            var panel = new StackPanel { Spacing = 12 };
            panel.Children.Add(new TextBlock
            {
                Text = "Show only these extensions (leave empty to show all):",
                TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap
            });
            panel.Children.Add(includeBox);
            panel.Children.Add(new TextBlock
            {
                Text = "Exclude these extensions:",
                TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap
            });
            panel.Children.Add(excludeBox);
            panel.Children.Add(new TextBlock
            {
                Text = "Separate extensions with commas. Include list takes priority over exclude list.",
                FontSize = 12,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap
            });

            var dialog = new ContentDialog
            {
                Title = "File Type Filter",
                Content = panel,
                PrimaryButtonText = "Apply",
                SecondaryButtonText = "Clear Filter",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };

            ContentDialogResult result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                var filter = new FileFilterConfig
                {
                    IncludeExtensions = ParseExtensions(includeBox.Text),
                    ExcludeExtensions = ParseExtensions(excludeBox.Text)
                };
                await ViewModel.UpdateFileFilterAsync(filter);
            }
            else if (result == ContentDialogResult.Secondary)
            {
                // Clear filter
                await ViewModel.UpdateFileFilterAsync(new FileFilterConfig());
            }
        }

        private static List<string> ParseExtensions(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return [];

            return text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(ext => ext.StartsWith('.') ? ext : "." + ext)
                .Where(ext => ext.Length > 1)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void TagFilterButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ViewModel.IsLoaded) return;

            MenuFlyout flyout = new();
            System.Collections.ObjectModel.ObservableCollection<TagDefinition> availableTags = ViewModel.AvailableTags;
            HashSet<string> activeNames = new(ViewModel.ActiveTagFilters.Select(t => t.Name));

            if (availableTags.Count == 0)
            {
                MenuFlyoutItem noTags = new() { Text = "No tags defined", IsEnabled = false };
                flyout.Items.Add(noTags);
            }
            else
            {
                foreach (TagDefinition tag in availableTags)
                {
                    ToggleMenuFlyoutItem item = new()
                    {
                        Text = tag.Name,
                        IsChecked = activeNames.Contains(tag.Name),
                        Icon = new FontIcon
                        {
                            Glyph = "\u25CF",
                            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe UI"),
                            FontSize = 16,
                            Foreground = ParseTagColorBrush(tag.Color)
                        }
                    };
                    TagDefinition capturedTag = tag;
                    item.Click += (s, args) => ViewModel.ToggleTagFilter(capturedTag);
                    flyout.Items.Add(item);
                }
            }

            flyout.Items.Add(new MenuFlyoutSeparator());
            MenuFlyoutItem manageItem = new()
            {
                Text = "Manage Tags...",
                Icon = new FontIcon { Glyph = "\uE115" }
            };
            manageItem.Click += (s, args) => OnManageTagsRequested(this, EventArgs.Empty);
            flyout.Items.Add(manageItem);

            if (sender is FrameworkElement element)
            {
                flyout.ShowAt(element);
            }
        }

        private void RemoveTagFilter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string tagName)
            {
                TagDefinition? tag = ViewModel.ActiveTagFilters.FirstOrDefault(t => t.Name == tagName);
                if (tag != null)
                {
                    ViewModel.ToggleTagFilter(tag);
                }
            }
        }

        private async void OnManageTagsRequested(object? sender, EventArgs e)
        {
            if (!ViewModel.IsLoaded || ViewModel.Board == null) return;

            try
            {
                await ShowManageTagsDialogAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in OnManageTagsRequested: {ex}");
            }
        }

        private async Task ShowManageTagsDialogAsync()
        {
            List<TagDefinition> tags = ViewModel.TagService.GetTagDefinitions(ViewModel.Board!);

            StackPanel panel = new() { Spacing = 8, MinWidth = 360 };

            if (tags.Count == 0)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = "No tags defined yet. Use the tag button on items or groups to create tags.",
                    TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
                    Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
                });
            }
            else
            {
                foreach (TagDefinition tag in tags)
                {
                    Grid row = new();
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });

                    // Color dot
                    Border colorDot = new()
                    {
                        Width = 16,
                        Height = 16,
                        CornerRadius = new CornerRadius(8),
                        Margin = new Thickness(0, 0, 8, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                        Background = ParseTagColorBrush(tag.Color)
                    };
                    Grid.SetColumn(colorDot, 0);
                    row.Children.Add(colorDot);

                    // Name
                    TextBlock nameText = new()
                    {
                        Text = tag.Name,
                        VerticalAlignment = VerticalAlignment.Center,
                        FontSize = 14
                    };
                    Grid.SetColumn(nameText, 1);
                    row.Children.Add(nameText);

                    // Delete button
                    string capturedName = tag.Name;
                    Button deleteBtn = new()
                    {
                        Content = new FontIcon { Glyph = "\uE74D", FontSize = 12 },
                        Width = 28,
                        Height = 28,
                        Padding = new Thickness(0),
                        Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent),
                        BorderThickness = new Thickness(0)
                    };
                    ToolTipService.SetToolTip(deleteBtn, "Delete tag");
                    deleteBtn.Click += async (s, args) =>
                    {
                        try
                        {
                            await ViewModel.DeleteTagAsync(capturedName);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to delete tag: {ex.Message}");
                        }
                    };
                    Grid.SetColumn(deleteBtn, 2);
                    row.Children.Add(deleteBtn);

                    panel.Children.Add(row);
                }
            }

            ContentDialog dialog = new()
            {
                Title = "Manage Tags",
                Content = panel,
                CloseButtonText = "Close",
                XamlRoot = this.XamlRoot
            };

            await dialog.ShowAsync();
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

        private async Task ShowErrorDialogAsync(string message)
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

        private void ColumnsItemsControl_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperation.Move;
            return;

            // Check if this is a column drag operation
            if (e.DataView.Properties.ContainsKey(ColumnDragDataFormat))
            {
                e.AcceptedOperation = DataPackageOperation.Move;
                e.DragUIOverride.IsCaptionVisible = false;
                e.DragUIOverride.IsGlyphVisible = false;
            }
            else
            {
                e.AcceptedOperation = DataPackageOperation.None;
            }
        }

        private async void ColumnsItemsControl_Drop(object sender, DragEventArgs e)
        {
            // Check if this is a column drag operation
            if (!e.DataView.Properties.ContainsKey(ColumnDragDataFormat))
                return;

            // Get the dragged column folder name from the data package
            var folderName = e.DataView.Properties[ColumnDragDataFormat] as string;
            if (string.IsNullOrEmpty(folderName))
                return;

            // Find the source column
            ColumnViewModel? sourceColumn = ViewModel.Columns.FirstOrDefault(c => 
                Path.GetFileName(c.FolderPath) == folderName);
            if (sourceColumn == null)
                return;

            // Calculate drop position
            Point dropPosition = e.GetPosition(ColumnsItemsControl);
            var targetIndex = CalculateDropIndex(dropPosition);

            // Don't reorder if dropping in the same position
            var currentIndex = ViewModel.Columns.IndexOf(sourceColumn);
            if (currentIndex == targetIndex || (currentIndex + 1 == targetIndex))
                return;

            try
            {
                await ViewModel.ReorderColumnAsync(sourceColumn, targetIndex);
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync($"Failed to reorder column: {ex.Message}");
            }
        }

        private int CalculateDropIndex(Windows.Foundation.Point dropPosition)
        {
            var columnWidth = 280.0; // ColumnControl width
            var spacing = 16.0; // StackPanel spacing
            var margin = 16.0; // StackPanel margin

            // Calculate the index based on horizontal position
            var adjustedX = dropPosition.X - margin;
            if (adjustedX < 0)
                return 0;

            var index = (int)((adjustedX + spacing / 2) / (columnWidth + spacing));
            
            // Clamp to valid range
            if (index < 0)
                return 0;
            if (index > ViewModel.Columns.Count)
                return ViewModel.Columns.Count;

            return index;
        }
        
        private void OpenFolder_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;
            ViewModel.OpenFolderCommand.Execute(null);
        }
        
        private void NewItem_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;
            if (ViewModel.Columns.Count > 0)
            {
                ColumnViewModel? firstColumn = ViewModel.Columns.FirstOrDefault();
                firstColumn?.AddItem();
            }
            else
            {
                ViewModel.ShowNotification("No Columns", "Please create a column first before adding items.", Microsoft.UI.Xaml.Controls.InfoBarSeverity.Warning);
            }
        }
        
        private void NewColumn_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;
            ViewModel.AddColumnCommand.Execute(null);
        }
        
        private async void Refresh_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;
            try
            {
                if (!string.IsNullOrEmpty(ViewModel.BoardName) && ViewModel.Columns.Count > 0)
                {
                    // Get the current folder path from the first column
                    ColumnViewModel? firstColumn = ViewModel.Columns.FirstOrDefault();
                    if (firstColumn != null)
                    {
                        var boardPath = Path.GetDirectoryName(firstColumn.FolderPath);
                        if (boardPath != null)
                        {
                            await ViewModel.LoadBoardAsync(boardPath);
                            ViewModel.ShowNotification("Refreshed", "Board has been reloaded from disk.", Microsoft.UI.Xaml.Controls.InfoBarSeverity.Success);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ViewModel.ShowNotification(
                    "Error Refreshing",
                    $"Failed to refresh board: {ex.Message}",
                    Microsoft.UI.Xaml.Controls.InfoBarSeverity.Error);
                System.Diagnostics.Debug.WriteLine($"Error in Refresh_Invoked: {ex}");
            }
        }
        
        private void NavigateLeft_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;
            ViewModel.NavigateLeft();
        }
        
        private void NavigateRight_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;
            ViewModel.NavigateRight();
        }
        
        private void NavigateUp_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;
            ViewModel.NavigateUp();
        }
        
        private void NavigateDown_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;
            ViewModel.NavigateDown();
        }

        private void Escape_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (ViewModel.IsItemDetailOpen)
            {
                args.Handled = true;
                ItemDetailViewControl.RequestClose();
            }
        }

        private async void HandleOpenItemDetail(KanbanItemViewModel kanbanItemViewModel)
        {
            var args = ViewModel.OpenItemDetail(kanbanItemViewModel);
            ItemDetailOverlay.Visibility = Visibility.Visible;
            await ItemDetailViewControl.LoadAsync(args.kanbanItem, args.detailVm, args.fileWatcher);
        }

        private void OnItemDetailCloseRequested(object? sender, EventArgs e)
        {
            ItemDetailViewControl.Unload();
            ItemDetailOverlay.Visibility = Visibility.Collapsed;
            ViewModel.CloseItemDetail();
        }
        
        private async void RecentFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button button && button.CommandParameter is string folderPath)
                {
                    if (ViewModel.OpenRecentFolderCommand.CanExecute(folderPath))
                    {
                        await ViewModel.OpenRecentFolderCommand.ExecuteAsync(folderPath);
                    }
                }
            }
            catch (Exception ex)
            {
                ViewModel.ShowNotification(
                    "Error Opening Folder",
                    $"An unexpected error occurred: {ex.Message}",
                    InfoBarSeverity.Error);
                System.Diagnostics.Debug.WriteLine($"Error in RecentFolderButton_Click: {ex}");
            }
        }
        
        private async void RemoveRecentFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button button && button.CommandParameter is string folderPath)
                {
                    if (ViewModel.RemoveRecentFolderCommand.CanExecute(folderPath))
                    {
                        await ViewModel.RemoveRecentFolderCommand.ExecuteAsync(folderPath);
                    }
                }
            }
            catch (Exception ex)
            {
                ViewModel.ShowNotification(
                    "Error Removing Folder",
                    $"An unexpected error occurred: {ex.Message}",
                    InfoBarSeverity.Error);
                System.Diagnostics.Debug.WriteLine($"Error in RemoveRecentFolderButton_Click: {ex}");
            }
        }
    }
}
