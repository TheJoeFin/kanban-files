# Phase 6 — Rich Markdown Editing (Full-Page Modal)

## Objective
Allow users to open a Kanban item to view and edit its full markdown content in a rich, full-page modal dialog with live preview.

---

## 6.1 — Item Detail Dialog

### What
Create a full-page overlay or `ContentDialog` that displays the full content of a Kanban item for viewing and editing.

### Visual Design
```
┌──────────────────────────────────────────────────────┐
│  ← Back    Task Title                        [Save]  │  ← Title bar
├──────────────────────────────────────────────────────┤
│                                                      │
│  ┌──────────────────┐  ┌──────────────────────────┐  │
│  │                  │  │                          │  │
│  │   Markdown       │  │    Rendered Preview      │  │
│  │   Editor         │  │                          │  │
│  │   (TextBox)      │  │    # Task Title          │  │
│  │                  │  │                          │  │
│  │   # Task Title   │  │    This is the task      │  │
│  │                  │  │    description with       │  │
│  │   This is the    │  │    **bold** and *italic*  │  │
│  │   task desc...   │  │    formatting.            │  │
│  │                  │  │                          │  │
│  │                  │  │    - Item 1               │  │
│  │                  │  │    - Item 2               │  │
│  │                  │  │                          │  │
│  └──────────────────┘  └──────────────────────────┘  │
│                                                      │
│  Last modified: 2026-02-06 5:30 PM                   │  ← Status bar
└──────────────────────────────────────────────────────┘
```

### Layout Options
1. **Split pane** (recommended): Editor on left, preview on right — side by side
2. **Tabbed**: Toggle between "Edit" and "Preview" tabs
3. **Single pane with toggle**: User switches between edit mode and preview mode

Implement **split pane** as default with an option to toggle preview off for a full-width editor.

### Implementation Approach
Use a full-window overlay (`Grid` layer) rather than `ContentDialog` for better control over layout and sizing.

```xaml
<!-- In MainWindow.xaml, as an overlay grid -->
<Grid x:Name="ItemDetailOverlay" Visibility="Collapsed" Background="{ThemeResource LayerFillColorDefaultBrush}">
    <Grid MaxWidth="1200" Margin="40" Padding="24" CornerRadius="8"
          Background="{ThemeResource CardBackgroundFillColorDefaultBrush}"
          Shadow="{ThemeResource SharedShadow}">
        <!-- Title bar row -->
        <!-- Split content row -->
        <!-- Status bar row -->
    </Grid>
</Grid>
```

### Opening the Modal
- **Trigger**: Click on a Kanban item card, or press Enter on a focused card
- **Action**:
  1. Load full file content from disk (if not already cached)
  2. Set `ItemDetailViewModel` properties
  3. Show overlay with entrance animation (slide up or fade in)

### Closing the Modal
- **Trigger**: Click "← Back" button, press Escape, or click outside the modal
- **Action**:
  1. If there are unsaved changes, prompt: "You have unsaved changes. [Save & Close] [Discard] [Cancel]"
  2. If no changes or user confirms, hide overlay with exit animation
  3. Update the card's content preview in the column

### ViewModel: `ItemDetailViewModel`
```csharp
public partial class ItemDetailViewModel : ObservableObject
{
    [ObservableProperty] private string title = string.Empty;
    [ObservableProperty] private string fileName = string.Empty;
    [ObservableProperty] private string filePath = string.Empty;
    [ObservableProperty] private string markdownContent = string.Empty;
    [ObservableProperty] private string renderedHtml = string.Empty;
    [ObservableProperty] private bool hasUnsavedChanges = false;
    [ObservableProperty] private bool isPreviewVisible = true;
    [ObservableProperty] private DateTime lastModified;
    [ObservableProperty] private bool isExternalChangeDetected = false;

    [RelayCommand] private async Task Save() { ... }
    [RelayCommand] private void Close() { ... }
    [RelayCommand] private void TogglePreview() { ... }
    [RelayCommand] private void ReloadFromDisk() { ... }
}
```

### Acceptance Criteria
- Clicking a card opens a full-page modal overlay
- Modal shows the item title, editor, and preview
- Modal can be closed with back button or Escape
- Unsaved changes prompt before close

---

## 6.2 — Markdown Editor

### What
Implement the text editing pane with features that make markdown editing comfortable.

### Editor Component
Use a standard `TextBox` with these configurations:
```xaml
<TextBox x:Name="MarkdownEditor"
         AcceptsReturn="True"
         TextWrapping="Wrap"
         IsSpellCheckEnabled="True"
         FontFamily="Cascadia Code, Consolas, monospace"
         FontSize="14"
         Padding="16"
         BorderThickness="0"
         ScrollViewer.VerticalScrollBarVisibility="Auto" />
```

### Features
1. **Monospace font**: `Cascadia Code` or `Consolas` for consistent markdown formatting
2. **Word wrap**: Enabled by default
3. **Spell check**: Enabled via `IsSpellCheckEnabled`
4. **Tab handling**: Insert 2 or 4 spaces on Tab key (override default focus behavior)
5. **Auto-indent**: On Enter, match indentation of previous line (for lists)
6. **Undo/Redo**: Built-in TextBox undo support

### Keyboard Shortcuts
| Shortcut | Action |
|---|---|
| `Ctrl+S` | Save file |
| `Ctrl+B` | Wrap selection in `**bold**` |
| `Ctrl+I` | Wrap selection in `*italic*` |
| `Ctrl+K` | Wrap selection in `[text](url)` link |
| `Ctrl+Shift+P` | Toggle preview pane |
| `Escape` | Close modal |

### Change Detection
- Listen to `TextChanged` event
- Compare current text with last-saved text to determine `HasUnsavedChanges`
- Update the rendered preview on each change (debounced 200ms for performance)

### Acceptance Criteria
- Editor supports multiline markdown text input
- Monospace font and word wrap are applied
- Keyboard shortcuts work for bold, italic, save
- Unsaved changes are tracked

---

## 6.3 — Markdown Rendering with Markdig

### What
Convert markdown text to HTML and display it in a preview pane using WebView2 or custom XAML rendering.

### Approach: WebView2 Preview

#### Why WebView2
- Full HTML/CSS rendering support
- Markdown-to-HTML is well supported by Markdig
- Easy to style with custom CSS
- Supports syntax highlighting for code blocks (via highlight.js or Prism.js)

#### Markdig Pipeline
```csharp
using Markdig;

private static readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
    .UseAdvancedExtensions()  // tables, task lists, emoji, etc.
    .UseAutoLinks()
    .UseTaskLists()
    .UseEmojiAndSmiley()
    .Build();

public static string ConvertToHtml(string markdown)
{
    var body = Markdown.ToHtml(markdown, _pipeline);
    return WrapInHtmlDocument(body);
}
```

#### HTML Template
```csharp
private static string WrapInHtmlDocument(string bodyHtml)
{
    return $"""
    <!DOCTYPE html>
    <html>
    <head>
        <meta charset="utf-8">
        <style>
            body {{
                font-family: 'Segoe UI', sans-serif;
                font-size: 14px;
                line-height: 1.6;
                padding: 16px;
                color: var(--text-color, #1a1a1a);
                background: transparent;
            }}
            h1, h2, h3 {{ margin-top: 1em; }}
            code {{
                background: rgba(128,128,128,0.15);
                padding: 2px 6px;
                border-radius: 3px;
                font-family: 'Cascadia Code', Consolas, monospace;
            }}
            pre code {{
                display: block;
                padding: 12px;
                overflow-x: auto;
            }}
            blockquote {{
                border-left: 3px solid #ccc;
                margin-left: 0;
                padding-left: 16px;
                color: #666;
            }}
            table {{ border-collapse: collapse; width: 100%; }}
            th, td {{ border: 1px solid #ddd; padding: 8px; text-align: left; }}
            img {{ max-width: 100%; }}
            input[type="checkbox"] {{ margin-right: 8px; }}
        </style>
    </head>
    <body>{bodyHtml}</body>
    </html>
    """;
}
```

#### WebView2 Control
```xaml
<WebView2 x:Name="PreviewWebView"
          DefaultBackgroundColor="Transparent"
          NavigationStarting="PreviewWebView_NavigationStarting" />
```

#### Updating Preview
```csharp
private async Task UpdatePreviewAsync(string markdown)
{
    var html = MarkdownRenderer.ConvertToHtml(markdown);
    await PreviewWebView.EnsureCoreWebView2Async();
    PreviewWebView.NavigateToString(html);
}
```

#### Theme Support
- Detect system theme (light/dark) and pass CSS variables to the HTML template
- Update theme when system theme changes

### Alternative: Custom XAML Renderer (Optional, more complex)
If WebView2 dependency is undesirable, implement a custom XAML renderer:
- Parse markdown with Markdig to an AST
- Walk the AST and create WinUI3 controls (`TextBlock`, `RichTextBlock`, `Border`, etc.)
- More complex but fully native

**Recommendation**: Start with WebView2, consider custom XAML renderer as a future enhancement.

### Acceptance Criteria
- Markdown is rendered to formatted HTML in the preview pane
- Preview updates live as the user types (debounced)
- Headings, bold, italic, lists, code blocks, tables, links, images, and task lists render correctly
- Theme (light/dark) is applied to the preview

---

## 6.4 — Save to Disk

### What
Write the editor content back to the `.md` file on disk, either on explicit save or automatically.

### Save Modes

#### Explicit Save (Default)
- Triggered by `Ctrl+S` or clicking the "Save" button
- Writes the full content of the editor to the file
- Updates `HasUnsavedChanges` to `false`
- Updates `LastModified` from file metadata
- Suppresses the next `FileSystemWatcher.Changed` event for this file (via `FileWatcherService.SuppressNextEvent()`)

#### Auto-Save (Optional Enhancement)
- Save automatically after 2 seconds of inactivity (no keystrokes)
- Show subtle "Saved" indicator in the status bar
- Can be toggled in settings

### Save Flow
```csharp
public async Task SaveAsync()
{
    _fileWatcherService.SuppressNextEvent(FilePath);
    await File.WriteAllTextAsync(FilePath, MarkdownContent);
    
    var fileInfo = new FileInfo(FilePath);
    LastModified = fileInfo.LastWriteTime;
    HasUnsavedChanges = false;
    
    // Update the card preview in the column
    _parentItem.ContentPreview = FileSystemService.GeneratePreview(MarkdownContent);
    _parentItem.FullContent = MarkdownContent;
}
```

### Error Handling
- File is locked by another process → show error, allow retry
- Disk full → show error with message
- File was deleted externally while editing → prompt to "Save As" or recreate

### Acceptance Criteria
- `Ctrl+S` saves content to the `.md` file on disk
- File content matches editor content after save
- Save does not trigger a sync event back to the UI
- Error cases are handled with user-friendly messages

---

## 6.5 — External Change Detection While Modal Is Open

### What
When the item detail modal is open and the underlying file is changed externally, notify the user and allow them to respond.

### Detection
The `FileWatcherService` raises `ItemContentChanged` events. The `ItemDetailViewModel` subscribes to this event and checks if the changed file matches the currently open item.

### UI
Display an `InfoBar` at the top of the modal:

```
┌──────────────────────────────────────────────────────┐
│  ⚠ This file was modified externally.                │
│  [Reload from Disk]  [Keep My Changes]  [Dismiss]    │
└──────────────────────────────────────────────────────┘
```

### Behavior

#### If user has NO unsaved changes:
- Auto-reload the content silently (no prompt)
- Update editor and preview
- Show a brief notification: "File reloaded"

#### If user HAS unsaved changes:
- Show the `InfoBar` warning
- **Reload from Disk**: Discard local edits, load file content, update editor and preview
- **Keep My Changes**: Dismiss the notification, continue editing. The user's next save will overwrite the external changes.
- **Dismiss**: Same as "Keep My Changes" but less explicit

### Implementation
```csharp
private void OnExternalFileChanged(string changedFilePath)
{
    if (changedFilePath != FilePath) return;
    
    if (!HasUnsavedChanges)
    {
        _ = ReloadFromDiskAsync(); // silent reload
    }
    else
    {
        IsExternalChangeDetected = true; // show InfoBar
    }
}
```

### Acceptance Criteria
- External changes to the open file are detected while the modal is open
- If no local edits, content reloads automatically
- If local edits exist, user is prompted with reload/keep options
- No data loss in any scenario
