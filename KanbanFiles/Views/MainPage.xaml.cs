using KanbanFiles.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace KanbanFiles.Views
{
    /// <summary>
    /// A simple page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
            ViewModel.OpenFolderRequested += OnOpenFolderRequested;
            ViewModel.AddColumnRequested += OnAddColumnRequested;
        }

        private async void OnOpenFolderRequested(object? sender, EventArgs e)
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
    }
}
