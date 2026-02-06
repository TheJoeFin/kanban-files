# Phase 7 — Polish & Edge Cases

## Objective
Harden the application by handling edge cases, improving usability with keyboard shortcuts, supporting theming, persisting window state, and providing good error handling and user feedback.

---

## 7.1 — Opening Folders with Existing Subfolders

### What
When a user opens a folder that already contains subfolders (but no `.kanban.json`), the app should treat existing subfolders as columns and generate a board config automatically.

### Behavior
1. User opens a folder via "Open Folder"
2. `BoardConfigService` checks for `.kanban.json` — not found
3. Enumerate subfolders in the root directory
4. **If subfolders exist**:
   - Create a `Board` with one `ColumnConfig` per subfolder
   - Sort columns alphabetically by folder name
   - Assign `SortOrder` values (0, 1, 2, ...)
   - Set `DisplayName` = folder name
   - Write `.kanban.json` to disk
5. **If no subfolders exist**:
   - Create default columns ("To Do", "In Progress", "Done") — already handled in Phase 1

### Edge Cases
- Subfolders with names that are unusual (spaces, special characters, very long names) — use as-is, the file system already accepted them
- Hidden folders (starting with `.`) — exclude from columns
- Nested subfolders within column folders — ignore (only top-level subfolders become columns)

### Acceptance Criteria
- Opening a folder with existing subfolders creates a board with those subfolders as columns
- `.kanban.json` is written with the generated config
- Hidden/dot-prefixed folders are excluded

---

## 7.2 — Non-Markdown File Handling

### What
Gracefully handle files that are not `.md` files within column folders.

### Behavior
| File Type | Handling |
|---|---|
| `*.md` | Displayed as Kanban item cards |
| `groups.json` | Used internally, never shown as a card |
| `.kanban.json` | Used internally (root only), never shown as a card |
| Other files (`*.txt`, `*.png`, images, etc.) | **Ignored** — not displayed, not tracked |
| Hidden files (starting with `.`) | **Ignored** |
| System files | **Ignored** |
| Directories inside column folders | **Ignored** (not treated as nested columns) |

### Implementation
In `FileSystemService.EnumerateItemsAsync()`:
```csharp
var mdFiles = Directory.GetFiles(columnFolderPath, "*.md")
    .Where(f => !Path.GetFileName(f).StartsWith('.'))
    .Where(f => (File.GetAttributes(f) & FileAttributes.Hidden) == 0)
    .OrderBy(f => f);
```

### `FileSystemWatcher` Filter
Column watchers already filter to `*.md` via `Filter = "*.md"`, so non-markdown files won't trigger sync events.

### Acceptance Criteria
- Only `.md` files appear as cards
- Internal JSON config files are never shown
- Non-markdown files in column folders don't cause errors

---

## 7.3 — Config File Corruption Handling

### What
Handle scenarios where `.kanban.json` or `groups.json` are malformed, missing fields, or corrupted.

### `.kanban.json` Recovery
1. Attempt to deserialize
2. If deserialization fails (`JsonException`):
   - Log the error
   - Back up the corrupt file as `.kanban.json.bak` (overwrite previous backup)
   - Re-initialize: generate config from existing subfolders
   - Show an `InfoBar`: "Board configuration was corrupted and has been reset. A backup was saved."
3. If deserialization succeeds but data is incomplete:
   - Missing `Columns` array → regenerate from subfolders
   - Missing `Name` → default to folder name
   - Column references a folder that doesn't exist → remove from config
   - Folder exists but not in config → add to config at the end

### `groups.json` Recovery
1. Attempt to deserialize
2. If deserialization fails:
   - Log the error
   - Back up as `groups.json.bak`
   - Return empty `GroupsConfig` (no groups)
   - Show an `InfoBar`: "Group configuration in '{column}' was corrupted and has been reset."
3. If deserialization succeeds but data references missing files:
   - Remove stale file references silently
   - Save the cleaned-up config

### Acceptance Criteria
- Corrupt `.kanban.json` is backed up and regenerated
- Corrupt `groups.json` is backed up and cleared
- User is notified of recovery actions
- No crashes from malformed config files

---

## 7.4 — Keyboard Shortcuts

### What
Implement global and contextual keyboard shortcuts for efficient navigation and actions.

### Global Shortcuts (Main Window)
| Shortcut | Action |
|---|---|
| `Ctrl+O` | Open Folder |
| `Ctrl+N` | New Item in focused column |
| `Ctrl+Shift+N` | New Column |
| `F5` | Refresh board (re-read all files) |
| `Escape` | Close modal / deselect / cancel |

### Board Navigation
| Shortcut | Action |
|---|---|
| `←` / `→` | Move focus between columns |
| `↑` / `↓` | Move focus between items in a column |
| `Enter` | Open focused item in detail modal |
| `Delete` | Delete focused item (with confirmation) |
| `F2` | Rename focused item or column |

### Detail Modal Shortcuts
| Shortcut | Action |
|---|---|
| `Ctrl+S` | Save |
| `Ctrl+B` | Bold selection |
| `Ctrl+I` | Italic selection |
| `Ctrl+K` | Insert link |
| `Ctrl+Shift+P` | Toggle preview |
| `Escape` | Close modal |

### Implementation
Use `KeyboardAccelerator` in XAML for global shortcuts:
```xaml
<Window.KeyboardAccelerators>
    <KeyboardAccelerator Modifiers="Control" Key="O" Invoked="OpenFolder_Invoked" />
    <KeyboardAccelerator Modifiers="Control" Key="N" Invoked="NewItem_Invoked" />
    <KeyboardAccelerator Modifiers="Control,Shift" Key="N" Invoked="NewColumn_Invoked" />
    <KeyboardAccelerator Key="F5" Invoked="Refresh_Invoked" />
</Window.KeyboardAccelerators>
```

### Focus Management
- Track which column and item currently has focus
- Arrow keys move focus logically through the board
- Opening a modal traps focus within the modal
- Closing a modal returns focus to the previously focused item

### Acceptance Criteria
- All listed shortcuts work correctly
- Arrow key navigation moves through columns and items
- Focus is properly managed across modal open/close

---

## 7.5 — Theming (Light/Dark Mode)

### What
Support the system light and dark themes via WinUI3's built-in theming.

### Implementation
WinUI3 respects the system theme by default. The app should:

1. **Use theme-aware resources** — all colors and brushes should reference `ThemeResource` rather than hardcoded values:
   ```xaml
   Background="{ThemeResource ApplicationPageBackgroundThemeBrush}"
   Foreground="{ThemeResource TextFillColorPrimaryBrush}"
   ```

2. **Card styling** — use `CardBackgroundFillColorDefaultBrush` for card backgrounds

3. **WebView2 preview theme** — pass theme info to the HTML template:
   ```csharp
   var theme = Application.Current.RequestedTheme;
   var isDark = theme == ApplicationTheme.Dark;
   // Inject CSS variables for dark mode
   ```

4. **Respond to theme changes at runtime** — if the user changes system theme while the app is running:
   ```csharp
   // In App.xaml.cs or MainWindow
   var uiSettings = new UISettings();
   uiSettings.ColorValuesChanged += (s, e) =>
   {
       DispatcherQueue.TryEnqueue(() => UpdateTheme());
   };
   ```

### Custom Theme Colors (Optional)
If needed, define custom brushes in `App.xaml` `ResourceDictionary`:
```xaml
<ResourceDictionary.ThemeDictionaries>
    <ResourceDictionary x:Key="Light">
        <SolidColorBrush x:Key="BoardBackgroundBrush" Color="#F3F3F3" />
        <SolidColorBrush x:Key="ColumnBackgroundBrush" Color="#FFFFFF" />
    </ResourceDictionary>
    <ResourceDictionary x:Key="Dark">
        <SolidColorBrush x:Key="BoardBackgroundBrush" Color="#1E1E1E" />
        <SolidColorBrush x:Key="ColumnBackgroundBrush" Color="#2D2D2D" />
    </ResourceDictionary>
</ResourceDictionary.ThemeDictionaries>
```

### Acceptance Criteria
- App follows system light/dark theme
- All UI elements are readable in both themes
- WebView2 preview matches the app theme
- Theme changes at runtime are reflected without restart

---

## 7.6 — Persist Window Size and Position

### What
Remember the window size and position between app launches.

### Storage
Use `ApplicationData.Current.LocalSettings` (WinUI3 app settings):
```csharp
var localSettings = ApplicationData.Current.LocalSettings;
localSettings.Values["WindowWidth"] = window.Width;
localSettings.Values["WindowHeight"] = window.Height;
localSettings.Values["WindowLeft"] = appWindow.Position.X;
localSettings.Values["WindowTop"] = appWindow.Position.Y;
localSettings.Values["IsMaximized"] = presenter.State == OverlappedPresenterState.Maximized;
```

### Restore on Launch
```csharp
private void RestoreWindowState()
{
    var localSettings = ApplicationData.Current.LocalSettings;
    if (localSettings.Values.TryGetValue("WindowWidth", out var width))
    {
        // Validate that saved position is still on-screen
        // (user may have disconnected a monitor)
        appWindow.Resize(new SizeInt32((int)width, (int)height));
        appWindow.Move(new PointInt32((int)left, (int)top));
    }
    if (localSettings.Values.TryGetValue("IsMaximized", out var isMaximized) && (bool)isMaximized)
    {
        (appWindow.Presenter as OverlappedPresenter)?.Maximize();
    }
}
```

### Also Persist
- Last opened folder path → auto-reopen on launch (optional, prompt user)
- Split pane ratio in detail modal
- Preview pane visibility state

### Validation
- Before restoring position, verify the saved coordinates are within current screen bounds
- If not (e.g., monitor removed), fall back to centered default position

### Acceptance Criteria
- Window opens at the same size and position as when last closed
- Maximized state is restored
- Invalid positions (off-screen) fall back to defaults

---

## 7.7 — Error Handling & User Notifications

### What
Provide consistent, user-friendly error handling and notifications throughout the app.

### Notification System
Use WinUI3 `InfoBar` control for non-blocking notifications:

```xaml
<InfoBar x:Name="NotificationBar"
         IsOpen="{x:Bind ViewModel.IsNotificationVisible, Mode=TwoWay}"
         Title="{x:Bind ViewModel.NotificationTitle, Mode=OneWay}"
         Message="{x:Bind ViewModel.NotificationMessage, Mode=OneWay}"
         Severity="{x:Bind ViewModel.NotificationSeverity, Mode=OneWay}"
         IsClosable="True" />
```

### Severity Levels
| Severity | Use Case |
|---|---|
| `Informational` | File reloaded, board config regenerated |
| `Success` | Item saved, column created |
| `Warning` | Config was corrupted and recovered, file locked |
| `Error` | Permission denied, disk full, file operation failed |

### Common Error Scenarios

| Scenario | Response |
|---|---|
| Permission denied (file/folder) | Error InfoBar: "Cannot access '{path}'. Check file permissions." |
| Disk full | Error InfoBar: "Cannot save — disk is full." |
| File locked by another process | Warning InfoBar: "File is in use by another application. Retry?" with retry button |
| Folder deleted externally while board is open | Warning InfoBar: "Column folder '{name}' was deleted. Column removed from board." |
| `.kanban.json` write failure | Error InfoBar: "Failed to save board configuration." + retry |
| Network drive disconnected | Error InfoBar: "Cannot access board folder. Check network connection." |
| Very long file/folder names | Truncate display, keep full path in tooltips |

### Error Handling Pattern
```csharp
try
{
    await FileSystemService.MoveItemAsync(source, target);
}
catch (IOException ex) when (ex.HResult == -2147024864) // file in use
{
    ShowNotification("File In Use",
        $"'{fileName}' is being used by another application.",
        InfoBarSeverity.Warning);
}
catch (UnauthorizedAccessException)
{
    ShowNotification("Permission Denied",
        $"Cannot access '{fileName}'. Check file permissions.",
        InfoBarSeverity.Error);
}
catch (Exception ex)
{
    ShowNotification("Error",
        $"An unexpected error occurred: {ex.Message}",
        InfoBarSeverity.Error);
    _logger.LogError(ex, "Failed to move item");
}
```

### Auto-Dismiss
- Informational and Success notifications auto-dismiss after 5 seconds
- Warning and Error notifications persist until manually dismissed

### Acceptance Criteria
- All file operations have try/catch with user-friendly messages
- InfoBar notifications appear for errors, warnings, and confirmations
- No unhandled exceptions crash the app
- Auto-dismiss works for low-severity notifications
- Error messages include actionable information (file name, suggested fix)
