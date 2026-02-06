using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI;
using Windows.UI;
using Windows.ApplicationModel.DataTransfer;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using KanbanFiles.Services;
using KanbanFiles.ViewModels;

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
            viewModel.OpenDetailRequested += OnOpenDetailRequested;
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

    private async void OnOpenDetailRequested(object? sender, EventArgs e)
    {
        if (DataContext is not KanbanItemViewModel kanbanItemViewModel) return;

        try
        {
            // Get required services from ViewModel
            var fileSystemService = kanbanItemViewModel.GetType()
                .GetField("_fileSystemService", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
                .GetValue(kanbanItemViewModel) as FileSystemService;
            
            var fileWatcherService = kanbanItemViewModel.GetType()
                .GetField("_fileWatcherService", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
                .GetValue(kanbanItemViewModel) as FileWatcherService;

            if (fileSystemService == null) return;

            // Create item model for ItemDetailViewModel
            var itemModel = new Models.KanbanItem
            {
                Title = kanbanItemViewModel.Title,
                FilePath = kanbanItemViewModel.FilePath,
                FileName = kanbanItemViewModel.FileName,
                ContentPreview = kanbanItemViewModel.ContentPreview,
                FullContent = kanbanItemViewModel.FullContent,
                LastModified = kanbanItemViewModel.LastModified
            };

            // Create ItemDetailViewModel
            var detailViewModel = new ItemDetailViewModel(itemModel, fileSystemService, fileWatcherService);
            await detailViewModel.LoadContentAsync();

            // Create the editor UI
            var editor = new TextBox
            {
                Text = detailViewModel.Content,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Cascadia Code, Consolas, Courier New"),
                FontSize = 14,
                Margin = new Thickness(0)
            };

            // Subscribe to content changes to update ViewModel
            editor.TextChanged += (s, args) => detailViewModel.Content = editor.Text;

            // Create WebView2 for preview
            var webView = new WebView2
            {
                Margin = new Thickness(0)
            };

            // Initialize WebView2 and navigate to content
            await webView.EnsureCoreWebView2Async();
            webView.NavigateToString(detailViewModel.RenderedHtml);

            // Update preview when content changes
            detailViewModel.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(ItemDetailViewModel.RenderedHtml))
                {
                    webView.NavigateToString(detailViewModel.RenderedHtml);
                }
            };

            // Create split view with editor and preview
            var leftColumn = new Grid
            {
                Padding = new Thickness(8)
            };
            leftColumn.Children.Add(editor);

            var rightColumn = new Grid
            {
                Padding = new Thickness(8)
            };
            rightColumn.Children.Add(webView);

            var splitGrid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
                },
                MinHeight = 500,
                MinWidth = 800
            };

            Grid.SetColumn(leftColumn, 0);
            Grid.SetColumn(rightColumn, 1);
            splitGrid.Children.Add(leftColumn);
            splitGrid.Children.Add(rightColumn);

            // Create save button with Ctrl+S handler
            var saveButton = new Button
            {
                Content = "Save",
                Margin = new Thickness(0, 12, 0, 0)
            };

            var rootStack = new StackPanel();
            rootStack.Children.Add(splitGrid);
            rootStack.Children.Add(saveButton);

            // Create the dialog
            var dialog = new ContentDialog
            {
                Title = kanbanItemViewModel.Title,
                Content = rootStack,
                CloseButtonText = "Close",
                XamlRoot = this.XamlRoot,
                DefaultButton = ContentDialogButton.Close,
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
            };

            // Track if save is in progress
            bool isSaving = false;

            // Save handler
            async Task SaveAsync()
            {
                if (isSaving) return;
                isSaving = true;

                try
                {
                    saveButton.IsEnabled = false;
                    saveButton.Content = "Saving...";
                    await detailViewModel.SaveCommand.ExecuteAsync(null);
                    saveButton.Content = "Saved ✓";
                    
                    // Update the KanbanItemViewModel with new content
                    kanbanItemViewModel.FullContent = detailViewModel.Content;
                    kanbanItemViewModel.ContentPreview = FileSystemService.GenerateContentPreview(detailViewModel.Content);
                    kanbanItemViewModel.LastModified = detailViewModel.LastModified;

                    await Task.Delay(1500);
                    if (detailViewModel.HasUnsavedChanges)
                    {
                        saveButton.Content = "Save";
                    }
                }
                catch (Exception ex)
                {
                    await ShowErrorAsync($"Failed to save: {ex.Message}");
                    saveButton.Content = "Save";
                }
                finally
                {
                    saveButton.IsEnabled = true;
                    isSaving = false;
                }
            }

            saveButton.Click += async (s, args) => await SaveAsync();

            // Add keyboard shortcut handlers
            editor.KeyDown += async (s, args) =>
            {
                var isCtrlPressed = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
                
                // Ctrl+S: Save
                if (args.Key == Windows.System.VirtualKey.S && isCtrlPressed)
                {
                    args.Handled = true;
                    await SaveAsync();
                }
                // Ctrl+B: Bold
                else if (args.Key == Windows.System.VirtualKey.B && isCtrlPressed)
                {
                    args.Handled = true;
                    WrapSelection(editor, "**", "**");
                }
                // Ctrl+I: Italic
                else if (args.Key == Windows.System.VirtualKey.I && isCtrlPressed)
                {
                    args.Handled = true;
                    WrapSelection(editor, "*", "*");
                }
                // Ctrl+K: Link
                else if (args.Key == Windows.System.VirtualKey.K && isCtrlPressed)
                {
                    args.Handled = true;
                    InsertLink(editor);
                }
            };

            // External file change detection
            EventHandler<ItemChangedEventArgs>? fileChangedHandler = null;
            if (fileWatcherService != null)
            {
                fileChangedHandler = async (s, e) =>
                {
                    if (e.FilePath != kanbanItemViewModel.FilePath) return;

                    // Read the new content from disk
                    string? newContent = null;
                    try
                    {
                        newContent = await File.ReadAllTextAsync(e.FilePath);
                    }
                    catch
                    {
                        return; // File might be temporarily locked
                    }

                    // If content is the same, nothing to do
                    if (newContent == detailViewModel.Content) return;

                    // Dispatch to UI thread
                    App.MainDispatcher!.TryEnqueue(async () =>
                    {
                        if (detailViewModel.HasUnsavedChanges)
                        {
                            // User has unsaved changes - prompt them
                            var conflictDialog = new ContentDialog
                            {
                                Title = "External File Change Detected",
                                Content = "This file was modified externally. Do you want to reload it? Your unsaved changes will be lost.",
                                PrimaryButtonText = "Reload",
                                SecondaryButtonText = "Keep My Changes",
                                CloseButtonText = "Cancel",
                                XamlRoot = this.XamlRoot
                            };

                            var result = await conflictDialog.ShowAsync();
                            if (result == ContentDialogResult.Primary)
                            {
                                detailViewModel.ReloadFromDisk(newContent);
                                editor.Text = detailViewModel.Content;
                            }
                        }
                        else
                        {
                            // No unsaved changes - auto-reload
                            detailViewModel.ReloadFromDisk(newContent);
                            editor.Text = detailViewModel.Content;
                        }
                    });
                };

                fileWatcherService.ItemContentChanged += fileChangedHandler;
            }

            // Show unsaved changes prompt on close
            dialog.Closing += async (s, args) =>
            {
                // Unsubscribe from file watcher
                if (fileWatcherService != null && fileChangedHandler != null)
                {
                    fileWatcherService.ItemContentChanged -= fileChangedHandler;
                }

                if (detailViewModel.HasUnsavedChanges)
                {
                    args.Cancel = true;

                    var confirmDialog = new ContentDialog
                    {
                        Title = "Unsaved Changes",
                        Content = "You have unsaved changes. Do you want to save before closing?",
                        PrimaryButtonText = "Save",
                        SecondaryButtonText = "Don't Save",
                        CloseButtonText = "Cancel",
                        XamlRoot = this.XamlRoot
                    };

                    var confirmResult = await confirmDialog.ShowAsync();
                    if (confirmResult == ContentDialogResult.Primary)
                    {
                        await SaveAsync();
                        dialog.Hide();
                    }
                    else if (confirmResult == ContentDialogResult.Secondary)
                    {
                        dialog.Hide();
                    }
                }
            };

            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            await ShowErrorAsync($"Failed to open editor: {ex.Message}");
        }
    }

    /// <summary>
    /// Wraps the selected text with the specified prefix and suffix.
    /// If no text is selected, inserts placeholder text with the wrapping.
    /// </summary>
    private void WrapSelection(TextBox textBox, string prefix, string suffix)
    {
        var selectionStart = textBox.SelectionStart;
        var selectionLength = textBox.SelectionLength;
        var text = textBox.Text;

        if (selectionLength > 0)
        {
            // Wrap the selected text
            var selectedText = text.Substring(selectionStart, selectionLength);
            var wrappedText = prefix + selectedText + suffix;
            
            textBox.Text = text.Substring(0, selectionStart) + wrappedText + text.Substring(selectionStart + selectionLength);
            
            // Select the wrapped content (excluding the wrapper syntax)
            textBox.SelectionStart = selectionStart + prefix.Length;
            textBox.SelectionLength = selectedText.Length;
        }
        else
        {
            // Insert placeholder with wrapping
            var placeholder = prefix == "**" ? "bold" : "italic";
            var wrappedText = prefix + placeholder + suffix;
            
            textBox.Text = text.Substring(0, selectionStart) + wrappedText + text.Substring(selectionStart);
            
            // Select the placeholder text
            textBox.SelectionStart = selectionStart + prefix.Length;
            textBox.SelectionLength = placeholder.Length;
        }
        
        textBox.Focus(FocusState.Programmatic);
    }

    /// <summary>
    /// Inserts a markdown link at the cursor position or wraps selected text in a link.
    /// Places the cursor in the URL portion for easy editing.
    /// </summary>
    private void InsertLink(TextBox textBox)
    {
        var selectionStart = textBox.SelectionStart;
        var selectionLength = textBox.SelectionLength;
        var text = textBox.Text;

        string linkText;
        string linkMarkdown;

        if (selectionLength > 0)
        {
            // Wrap selected text as link
            linkText = text.Substring(selectionStart, selectionLength);
            linkMarkdown = $"[{linkText}](url)";
        }
        else
        {
            // Insert placeholder link
            linkText = "text";
            linkMarkdown = "[text](url)";
        }

        textBox.Text = text.Substring(0, selectionStart) + linkMarkdown + text.Substring(selectionStart + selectionLength);
        
        // Position cursor in the URL portion for easy editing
        var urlStart = selectionStart + linkText.Length + 3; // after "[linkText]("
        textBox.SelectionStart = urlStart;
        textBox.SelectionLength = 3; // select "url"
        
        textBox.Focus(FocusState.Programmatic);
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
