using Microsoft.UI.Xaml.Input;

namespace KanbanFiles.Views;

public sealed partial class ItemDetailView : UserControl
{
    private ItemDetailViewModel? _viewModel;
    private KanbanItemViewModel? _kanbanItemViewModel;
    private FileWatcherService? _fileWatcherService;
    private EventHandler<ItemChangedEventArgs>? _fileChangedHandler;
    private bool _isSaving;
    private bool _webViewReady;

    public event EventHandler? CloseRequested;

    public ItemDetailView()
    {
        InitializeComponent();
    }

    public async Task LoadAsync(KanbanItemViewModel kanbanItemViewModel, ItemDetailViewModel detailViewModel, FileWatcherService? fileWatcherService)
    {
        _kanbanItemViewModel = kanbanItemViewModel;
        _viewModel = detailViewModel;
        _fileWatcherService = fileWatcherService;

        DataContext = _viewModel;

        await _viewModel.LoadContentAsync();

        EditorTextBox.Text = _viewModel.Content;
        UpdateSaveStatus();

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        if (_webViewReady)
        {
            PreviewWebView.NavigateToString(_viewModel.RenderedHtml);
        }

        SubscribeFileWatcher();

        EditorTextBox.Focus(FocusState.Programmatic);
    }

    public void Unload()
    {
        UnsubscribeFileWatcher();

        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = null;
        _kanbanItemViewModel = null;
    }

    public bool HasUnsavedChanges => _viewModel?.HasUnsavedChanges ?? false;

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ItemDetailViewModel.RenderedHtml) && _webViewReady)
        {
            PreviewWebView.NavigateToString(_viewModel!.RenderedHtml);
        }
        else if (e.PropertyName == nameof(ItemDetailViewModel.HasUnsavedChanges))
        {
            UpdateSaveStatus();
        }
    }

    private void UpdateSaveStatus()
    {
        if (_viewModel == null) return;

        if (_viewModel.HasUnsavedChanges)
        {
            SaveButton.Visibility = Visibility.Visible;
            SaveStatusText.Text = "Unsaved changes";
        }
        else
        {
            SaveButton.Visibility = Visibility.Collapsed;
            SaveStatusText.Text = "";
        }
    }

    private async void PreviewWebView_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await PreviewWebView.EnsureCoreWebView2Async();
            _webViewReady = true;

            if (_viewModel != null)
            {
                PreviewWebView.NavigateToString(_viewModel.RenderedHtml);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to initialize WebView2: {ex.Message}");
        }
    }

    private void EditorTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.Content = EditorTextBox.Text;
        }
    }

    private async void EditorTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        var isCtrlPressed = Microsoft.UI.Input.InputKeyboardSource
            .GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        if (e.Key == Windows.System.VirtualKey.S && isCtrlPressed)
        {
            e.Handled = true;
            await SaveAsync();
        }
        else if (e.Key == Windows.System.VirtualKey.B && isCtrlPressed)
        {
            e.Handled = true;
            WrapSelection("**", "**");
        }
        else if (e.Key == Windows.System.VirtualKey.I && isCtrlPressed)
        {
            e.Handled = true;
            WrapSelection("*", "*");
        }
        else if (e.Key == Windows.System.VirtualKey.K && isCtrlPressed)
        {
            e.Handled = true;
            InsertLink();
        }
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        RequestClose();
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        await SaveAsync();
    }

    private void BoldButton_Click(object sender, RoutedEventArgs e)
    {
        WrapSelection("**", "**");
    }

    private void ItalicButton_Click(object sender, RoutedEventArgs e)
    {
        WrapSelection("*", "*");
    }

    private void LinkButton_Click(object sender, RoutedEventArgs e)
    {
        InsertLink();
    }

    public async void RequestClose()
    {
        if (_viewModel?.HasUnsavedChanges == true)
        {
            var dialog = new ContentDialog
            {
                Title = "Unsaved Changes",
                Content = "You have unsaved changes. Do you want to save before closing?",
                PrimaryButtonText = "Save",
                SecondaryButtonText = "Don't Save",
                CloseButtonText = "Cancel",
                XamlRoot = this.XamlRoot
            };

            ContentDialogResult result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                await SaveAsync();
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
            else if (result == ContentDialogResult.Secondary)
            {
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
            // Cancel: do nothing, stay in editor
        }
        else
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task SaveAsync()
    {
        if (_viewModel == null || _isSaving) return;
        _isSaving = true;

        try
        {
            SaveButton.IsEnabled = false;
            SaveStatusText.Text = "Saving...";

            await _viewModel.SaveCommand.ExecuteAsync(null);

            SaveStatusText.Text = "Saved ✓";

            if (_kanbanItemViewModel != null)
            {
                _kanbanItemViewModel.FullContent = _viewModel.Content;
                _kanbanItemViewModel.ContentPreview = FileSystemService.GenerateContentPreview(_viewModel.Content);
                _kanbanItemViewModel.LastModified = _viewModel.LastModified;
            }

            await Task.Delay(1500);

            UpdateSaveStatus();
        }
        catch (Exception ex)
        {
            SaveStatusText.Text = "Save failed";

            var dialog = new ContentDialog
            {
                Title = "Error",
                Content = $"Failed to save: {ex.Message}",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
        finally
        {
            SaveButton.IsEnabled = true;
            _isSaving = false;
        }
    }

    private void SubscribeFileWatcher()
    {
        if (_fileWatcherService == null || _viewModel == null) return;

        _fileChangedHandler = async (s, e) =>
        {
            if (e.FilePath != _viewModel.GetFilePath()) return;

            string? newContent = null;
            try
            {
                newContent = await File.ReadAllTextAsync(e.FilePath);
            }
            catch
            {
                return;
            }

            if (newContent == _viewModel.Content) return;

            App.MainDispatcher!.TryEnqueue(async () =>
            {
                if (_viewModel.HasUnsavedChanges)
                {
                    var dialog = new ContentDialog
                    {
                        Title = "External File Change Detected",
                        Content = "This file was modified externally. Do you want to reload it? Your unsaved changes will be lost.",
                        PrimaryButtonText = "Reload",
                        SecondaryButtonText = "Keep My Changes",
                        CloseButtonText = "Cancel",
                        XamlRoot = this.XamlRoot
                    };

                    ContentDialogResult result = await dialog.ShowAsync();
                    if (result == ContentDialogResult.Primary)
                    {
                        _viewModel.ReloadFromDisk(newContent);
                        EditorTextBox.Text = _viewModel.Content;
                    }
                }
                else
                {
                    _viewModel.ReloadFromDisk(newContent);
                    EditorTextBox.Text = _viewModel.Content;
                }
            });
        };

        _fileWatcherService.ItemContentChanged += _fileChangedHandler;
    }

    private void UnsubscribeFileWatcher()
    {
        if (_fileWatcherService != null && _fileChangedHandler != null)
        {
            _fileWatcherService.ItemContentChanged -= _fileChangedHandler;
            _fileChangedHandler = null;
        }
    }

    private void WrapSelection(string prefix, string suffix)
    {
        var selectionStart = EditorTextBox.SelectionStart;
        var selectionLength = EditorTextBox.SelectionLength;
        var text = EditorTextBox.Text;

        if (selectionLength > 0)
        {
            var selectedText = text.Substring(selectionStart, selectionLength);
            var wrappedText = prefix + selectedText + suffix;

            EditorTextBox.Text = text.Substring(0, selectionStart) + wrappedText + text.Substring(selectionStart + selectionLength);
            EditorTextBox.SelectionStart = selectionStart + prefix.Length;
            EditorTextBox.SelectionLength = selectedText.Length;
        }
        else
        {
            var placeholder = prefix == "**" ? "bold" : "italic";
            var wrappedText = prefix + placeholder + suffix;

            EditorTextBox.Text = text.Substring(0, selectionStart) + wrappedText + text.Substring(selectionStart);
            EditorTextBox.SelectionStart = selectionStart + prefix.Length;
            EditorTextBox.SelectionLength = placeholder.Length;
        }

        EditorTextBox.Focus(FocusState.Programmatic);
    }

    private void InsertLink()
    {
        var selectionStart = EditorTextBox.SelectionStart;
        var selectionLength = EditorTextBox.SelectionLength;
        var text = EditorTextBox.Text;

        string linkText;
        string linkMarkdown;

        if (selectionLength > 0)
        {
            linkText = text.Substring(selectionStart, selectionLength);
            linkMarkdown = $"[{linkText}](url)";
        }
        else
        {
            linkText = "text";
            linkMarkdown = "[text](url)";
        }

        EditorTextBox.Text = text.Substring(0, selectionStart) + linkMarkdown + text.Substring(selectionStart + selectionLength);

        var urlStart = selectionStart + linkText.Length + 3;
        EditorTextBox.SelectionStart = urlStart;
        EditorTextBox.SelectionLength = 3;

        EditorTextBox.Focus(FocusState.Programmatic);
    }
}
