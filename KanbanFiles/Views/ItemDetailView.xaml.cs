using Microsoft.UI.Xaml.Input;
using Windows.Storage;

namespace KanbanFiles.Views;

public sealed partial class ItemDetailView : UserControl
{
    private ItemDetailViewModel? _viewModel;
    private KanbanItemViewModel? _kanbanItemViewModel;
    private FileWatcherService? _fileWatcherService;
    private EventHandler<ItemChangedEventArgs>? _fileChangedHandler;
    private bool _isSaving;
    private bool _webViewReady;
    private AiChatViewModel? _aiChatViewModel;
    private bool _aiPaneVisible;

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

        // Adjust layout based on file type
        if (_viewModel.IsEditable && !_viewModel.IsMarkdown)
        {
            // Non-markdown text file: full-width editor, no preview
            EditorPreviewGrid.ColumnDefinitions[1].Width = new GridLength(0);
        }
        else
        {
            EditorPreviewGrid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
        }

        if (_viewModel.IsEditable)
        {
            await _viewModel.LoadContentAsync();
            EditorTextBox.Text = _viewModel.Content;
        }

        UpdateSaveStatus();

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        if (_viewModel.IsMarkdown && _webViewReady)
        {
            PreviewWebView.NavigateToString(_viewModel.RenderedHtml);
        }

        if (_viewModel.IsEditable)
        {
            SubscribeFileWatcher();
            EditorTextBox.Focus(FocusState.Programmatic);
        }
    }

    public void Unload()
    {
        UnsubscribeFileWatcher();

        _viewModel?.PropertyChanged -= OnViewModelPropertyChanged;

        if (_aiChatViewModel != null)
        {
            _aiChatViewModel.Messages.CollectionChanged -= AiMessages_CollectionChanged;
            _aiChatViewModel.PropertyChanged -= AiChatViewModel_PropertyChanged;
            _aiChatViewModel.Dispose();
            _aiChatViewModel = null;
        }

        _viewModel = null;
        _kanbanItemViewModel = null;

        // Reset layout for next use
        EditorPreviewGrid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
        HideAiPane();
    }

    public bool HasUnsavedChanges => _viewModel?.HasUnsavedChanges ?? false;

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ItemDetailViewModel.RenderedHtml) && _webViewReady && _viewModel?.IsMarkdown == true)
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
        _viewModel?.Content = EditorTextBox.Text;
    }

    private async void EditorTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        bool isCtrlPressed = Microsoft.UI.Input.InputKeyboardSource
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

    private async void OpenExternalButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;

        try
        {
            StorageFile file = await Windows.Storage.StorageFile.GetFileFromPathAsync(_viewModel.GetFilePath());
            await Windows.System.Launcher.LaunchFileAsync(file);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to open file externally: {ex.Message}");
        }
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
            ContentDialog dialog = new()
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

            ContentDialog dialog = new()
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
                    ContentDialog dialog = new()
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
        int selectionStart = EditorTextBox.SelectionStart;
        int selectionLength = EditorTextBox.SelectionLength;
        string text = EditorTextBox.Text;

        if (selectionLength > 0)
        {
            string selectedText = text.Substring(selectionStart, selectionLength);
            string wrappedText = prefix + selectedText + suffix;

            EditorTextBox.Text = text[..selectionStart] + wrappedText + text[(selectionStart + selectionLength)..];
            EditorTextBox.SelectionStart = selectionStart + prefix.Length;
            EditorTextBox.SelectionLength = selectedText.Length;
        }
        else
        {
            string placeholder = prefix == "**" ? "bold" : "italic";
            string wrappedText = prefix + placeholder + suffix;

            EditorTextBox.Text = text[..selectionStart] + wrappedText + text[selectionStart..];
            EditorTextBox.SelectionStart = selectionStart + prefix.Length;
            EditorTextBox.SelectionLength = placeholder.Length;
        }

        EditorTextBox.Focus(FocusState.Programmatic);
    }

    private void InsertLink()
    {
        int selectionStart = EditorTextBox.SelectionStart;
        int selectionLength = EditorTextBox.SelectionLength;
        string text = EditorTextBox.Text;

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

        EditorTextBox.Text = text[..selectionStart] + linkMarkdown + text[(selectionStart + selectionLength)..];

        int urlStart = selectionStart + linkText.Length + 3;
        EditorTextBox.SelectionStart = urlStart;
        EditorTextBox.SelectionLength = 3;

        EditorTextBox.Focus(FocusState.Programmatic);
    }

    // --- AI Chat Pane ---

    private void AiToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (_aiPaneVisible)
            HideAiPane();
        else
            ShowAiPane();
    }

    private void ShowAiPane()
    {
        _aiPaneVisible = true;
        AiPaneColumn.Width = new GridLength(320);
        AiChatPane.Visibility = Visibility.Visible;
        AiPaneSeparator.Visibility = Visibility.Visible;
        AiToggleButton.IsChecked = true;

        // Recreate AI ViewModel if it was disposed
        if (_aiChatViewModel == null && _viewModel?.IsEditable == true)
        {
            System.Diagnostics.Debug.WriteLine("[ItemDetailView] Recreating AiChatViewModel...");
            _aiChatViewModel = new AiChatViewModel
            {
                GetEditorContent = () => EditorTextBox.Text,
                ApplyToEditor = ApplyAiResponseToEditor
            };
            _aiChatViewModel.Messages.CollectionChanged += AiMessages_CollectionChanged;
            _aiChatViewModel.PropertyChanged += AiChatViewModel_PropertyChanged;
            AiMessagesItemsControl.ItemsSource = _aiChatViewModel.Messages;
            System.Diagnostics.Debug.WriteLine("[ItemDetailView] Starting AiChatViewModel initialization...");
            _ = _aiChatViewModel.InitializeAsync();
        }

        UpdateAiStatusDisplay();
    }

    private void HideAiPane()
    {
        _aiPaneVisible = false;
        AiPaneColumn.Width = new GridLength(0);
        AiChatPane.Visibility = Visibility.Collapsed;
        AiPaneSeparator.Visibility = Visibility.Collapsed;
        AiToggleButton.IsChecked = false;

        // Clear chat and dispose AI resources when pane is closed
        if (_aiChatViewModel != null)
        {
            _aiChatViewModel.Messages.CollectionChanged -= AiMessages_CollectionChanged;
            _aiChatViewModel.PropertyChanged -= AiChatViewModel_PropertyChanged;
            _aiChatViewModel.Dispose();
            _aiChatViewModel = null;
        }
    }

    private void UpdateAiStatusDisplay()
    {
        if (_aiChatViewModel == null) return;

        System.Diagnostics.Debug.WriteLine($"[ItemDetailView] UpdateAiStatusDisplay - IsModelLoading: {_aiChatViewModel.IsModelLoading}, IsModelAvailable: {_aiChatViewModel.IsModelAvailable}, StatusMessage: '{_aiChatViewModel.StatusMessage}'");

        if (_aiChatViewModel.IsModelLoading)
        {
            AiStatusPanel.Visibility = Visibility.Visible;
            AiLoadingRing.IsActive = true;
            AiStatusText.Text = _aiChatViewModel.StatusMessage;
            AiInputTextBox.IsEnabled = false;
            AiSendButton.IsEnabled = false;
        }
        else if (!_aiChatViewModel.IsModelAvailable)
        {
            AiStatusPanel.Visibility = Visibility.Visible;
            AiLoadingRing.IsActive = false;
            AiStatusText.Text = _aiChatViewModel.StatusMessage;
            AiInputTextBox.IsEnabled = false;
            AiSendButton.IsEnabled = false;
            QuickActionPlanButton.IsEnabled = false;
            QuickActionTodayButton.IsEnabled = false;
        }
        else
        {
            AiStatusPanel.Visibility = _aiChatViewModel.Messages.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            AiLoadingRing.IsActive = false;
            AiStatusText.Text = "Ask a question about this file, or try a quick action above.";
            AiInputTextBox.IsEnabled = !_aiChatViewModel.IsGenerating;
            AiSendButton.IsEnabled = !_aiChatViewModel.IsGenerating;
            QuickActionPlanButton.IsEnabled = !_aiChatViewModel.IsGenerating;
            QuickActionTodayButton.IsEnabled = !_aiChatViewModel.IsGenerating;
        }

        AiApplyButton.Visibility = _aiChatViewModel.LastAssistantResponse != null
            ? Visibility.Visible
            : Visibility.Collapsed;

        AiApplyButton.IsEnabled = _aiChatViewModel.ApplyResponseCommand.CanExecute(null);
    }

    private void AiChatViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AiChatViewModel.IsModelLoading)
            or nameof(AiChatViewModel.IsModelAvailable)
            or nameof(AiChatViewModel.StatusMessage)
            or nameof(AiChatViewModel.IsGenerating)
            or nameof(AiChatViewModel.LastAssistantResponse))
        {
            UpdateAiStatusDisplay();
        }
    }

    private void AiMessages_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        UpdateAiStatusDisplay();

        // Auto-scroll only when items are added, not when properties change
        if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
        {
            // Delay scroll slightly to let UI update
            _ = Task.Delay(50).ContinueWith(_ =>
            {
                App.MainDispatcher?.TryEnqueue(() =>
                {
                    if (AiMessagesScrollViewer.ScrollableHeight > 0)
                    {
                        AiMessagesScrollViewer.ChangeView(null, AiMessagesScrollViewer.ScrollableHeight, null, false);
                    }
                });
            });
        }
    }

    private async void AiSendButton_Click(object sender, RoutedEventArgs e)
    {
        await SendAiMessageAsync();
    }

    private async void AiInputTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            bool isShiftPressed = Microsoft.UI.Input.InputKeyboardSource
                .GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift)
                .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

            if (!isShiftPressed)
            {
                e.Handled = true;
                await SendAiMessageAsync();
            }
        }
    }

    private async Task SendAiMessageAsync()
    {
        if (_aiChatViewModel == null || string.IsNullOrWhiteSpace(AiInputTextBox.Text)) return;

        _aiChatViewModel.UserInput = AiInputTextBox.Text;
        AiInputTextBox.Text = string.Empty;
        await _aiChatViewModel.SendCommand.ExecuteAsync(null);
    }

    private async void QuickActionPlan_Click(object sender, RoutedEventArgs e)
    {
        if (_aiChatViewModel != null)
            await _aiChatViewModel.SendQuickActionAsync("Help me plan what to do next with this file. Suggest concrete next steps based on the current content.");
    }

    private async void QuickActionToday_Click(object sender, RoutedEventArgs e)
    {
        if (_aiChatViewModel != null)
            await _aiChatViewModel.SendQuickActionAsync("Based on the current content of this file, what can I realistically work on today? Give me focused, actionable tasks.");
    }

    private void AiApplyButton_Click(object sender, RoutedEventArgs e)
    {
        _aiChatViewModel?.ApplyResponseCommand.Execute(null);
    }

    private void AiClearButton_Click(object sender, RoutedEventArgs e)
    {
        _aiChatViewModel?.ClearChatCommand.Execute(null);
    }

    private void ApplyAiResponseToEditor(string content)
    {
        // Append to the end of the editor with a newline separator if there's existing content
        if (!string.IsNullOrEmpty(EditorTextBox.Text))
        {
            EditorTextBox.Text += Environment.NewLine + Environment.NewLine + content;
        }
        else
        {
            EditorTextBox.Text = content;
        }

        // Move cursor to the end
        EditorTextBox.SelectionStart = EditorTextBox.Text.Length;
        EditorTextBox.Focus(FocusState.Programmatic);
    }
}
