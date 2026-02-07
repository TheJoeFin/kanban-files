using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KanbanFiles.Models;
using KanbanFiles.Services;
using System;
using System.Threading.Tasks;

namespace KanbanFiles.ViewModels;

public partial class ItemDetailViewModel : ObservableObject
{
    private readonly FileSystemService _fileSystemService;
    private readonly FileWatcherService? _fileWatcherService;
    private readonly string _filePath;
    private string _originalContent;

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private string _content;

    [ObservableProperty]
    private string _renderedHtml;

    [ObservableProperty]
    private DateTime _lastModified;

    [ObservableProperty]
    private bool _hasUnsavedChanges;

    [ObservableProperty]
    private string _fileInfoText;

    public bool IsEditable { get; }
    public bool IsMarkdown { get; }

    public ItemDetailViewModel(KanbanItem item, FileSystemService fileSystemService, FileWatcherService? fileWatcherService = null)
    {
        _fileSystemService = fileSystemService;
        _fileWatcherService = fileWatcherService;
        _filePath = item.FilePath;
        _title = item.Title;
        _lastModified = item.LastModified;

        IsEditable = FileSystemService.IsTextFile(item.FilePath);
        IsMarkdown = Path.GetExtension(item.FilePath).Equals(".md", StringComparison.OrdinalIgnoreCase);

        // Load content will be called separately
        _content = string.Empty;
        _originalContent = string.Empty;
        _renderedHtml = string.Empty;
        _fileInfoText = IsEditable ? string.Empty : FileSystemService.GenerateFileTypePreview(item.FilePath);
    }

    partial void OnContentChanged(string value)
    {
        HasUnsavedChanges = value != _originalContent;
        if (IsMarkdown)
        {
            UpdateRenderedHtml();
        }
    }

    public async Task LoadContentAsync()
    {
        if (!IsEditable)
        {
            Content = string.Empty;
            _originalContent = string.Empty;
            HasUnsavedChanges = false;
            return;
        }

        try
        {
            Content = await File.ReadAllTextAsync(_filePath);
            _originalContent = Content;
            HasUnsavedChanges = false;
            UpdateRenderedHtml();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading file content from {_filePath}: {ex.Message}");
            // Set empty content as fallback
            Content = string.Empty;
            _originalContent = string.Empty;
        }
    }

    private void UpdateRenderedHtml()
    {
        if (!IsMarkdown)
        {
            RenderedHtml = string.Empty;
            return;
        }

        // Use Markdig to convert markdown to HTML with default pipeline
        var html = Markdig.Markdown.ToHtml(Content ?? string.Empty);
        RenderedHtml = WrapHtmlWithStyles(html);
    }

    private string WrapHtmlWithStyles(string bodyHtml)
    {
        return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, Cantarell, sans-serif;
            line-height: 1.6;
            padding: 16px;
            max-width: 100%;
            margin: 0;
            background: transparent;
            color: #1f1f1f;
        }}
        @media (prefers-color-scheme: dark) {{
            body {{
                color: #f0f0f0;
            }}
            a {{
                color: #4a9eff;
            }}
            code {{
                background: #2d2d2d;
            }}
            pre {{
                background: #2d2d2d;
            }}
            blockquote {{
                border-left-color: #555;
                color: #ccc;
            }}
        }}
        h1, h2, h3, h4, h5, h6 {{
            margin-top: 24px;
            margin-bottom: 12px;
        }}
        code {{
            font-family: 'Cascadia Code', 'Consolas', 'Courier New', monospace;
            background: #f4f4f4;
            padding: 2px 6px;
            border-radius: 3px;
        }}
        pre {{
            background: #f4f4f4;
            padding: 12px;
            border-radius: 6px;
            overflow-x: auto;
        }}
        pre code {{
            background: none;
            padding: 0;
        }}
        blockquote {{
            border-left: 4px solid #ddd;
            margin: 0;
            padding-left: 16px;
            color: #666;
        }}
        table {{
            border-collapse: collapse;
            width: 100%;
            margin: 12px 0;
        }}
        th, td {{
            border: 1px solid #ddd;
            padding: 8px 12px;
            text-align: left;
        }}
        th {{
            background: #f4f4f4;
            font-weight: 600;
        }}
        img {{
            max-width: 100%;
            height: auto;
        }}
        a {{
            color: #0066cc;
            text-decoration: none;
        }}
        a:hover {{
            text-decoration: underline;
        }}
    </style>
</head>
<body>
{bodyHtml}
</body>
</html>";
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            // Suppress file watcher event for our own change
            _fileWatcherService?.SuppressNextEvent(_filePath);
            await _fileSystemService.WriteItemContentAsync(_filePath, Content);
            
            _originalContent = Content;
            HasUnsavedChanges = false;
            
            // Update last modified from file system
            var fileInfo = new FileInfo(_filePath);
            LastModified = fileInfo.LastWriteTime;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving file {_filePath}: {ex.Message}");
            // Re-throw to let caller (view) handle with user notification
            throw;
        }
    }

    public void ReloadFromDisk(string newContent)
    {
        Content = newContent;
        _originalContent = newContent;
        HasUnsavedChanges = false;
        UpdateRenderedHtml();
    }

    public string GetFilePath() => _filePath;
}
