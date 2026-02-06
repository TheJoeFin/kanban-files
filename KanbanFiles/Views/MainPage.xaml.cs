using KanbanFiles.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.Storage.Pickers;
using WinRT.Interop;
using Windows.ApplicationModel.DataTransfer;

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

                var folder = await folderPicker.PickSingleFolderAsync();
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

            var result = await dialog.ShowAsync();
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
            var sourceColumn = ViewModel.Columns.FirstOrDefault(c => 
                Path.GetFileName(c.FolderPath) == folderName);
            if (sourceColumn == null)
                return;

            // Calculate drop position
            var dropPosition = e.GetPosition(ColumnsItemsControl);
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
                var firstColumn = ViewModel.Columns.FirstOrDefault();
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
                    var firstColumn = ViewModel.Columns.FirstOrDefault();
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
