# Phase 4 — Two-Way File System Sync

## Status: ✅ COMPLETE

## Objective
Implement real-time synchronization between the file system and the app UI. External changes (files created, deleted, renamed, or modified outside the app) are reflected in the board immediately, and all app-initiated changes are already written to disk (from previous phases).

---

## Implementation Summary

### ✅ 4.1 — FileWatcherService
**Status**: Complete

Implemented `Services/FileWatcherService.cs` with:
- Two separate `FileSystemWatcher` instances: one for folders (root), one for files (all subdirectories)
- Self-initiated change suppression via `SuppressNextEvent()` and `HashSet<string>`
- DispatcherQueue integration for UI thread marshaling
- Events: ItemCreated, ItemDeleted, ItemRenamed, ItemContentChanged, ColumnCreated, ColumnDeleted
- Proper disposal and lifecycle management

**Note**: Simplified architecture uses two watchers instead of per-column watchers (one for root folders, one for all .md files recursively).

### ✅ 4.2 — Sync External File Creates
**Status**: Complete

- Creates new `KanbanItemViewModel` when .md file is created externally
- Includes debouncing (300ms) and retry logic (3 attempts, 500ms apart)
- Updates ItemOrder in `.kanban.json`
- Properly marshals to UI thread

### ✅ 4.3 — Sync External File Deletes
**Status**: Complete

- Removes `KanbanItemViewModel` from column when file is deleted
- Updates ItemOrder in `.kanban.json`
- Handles suppression for app-initiated deletes

### ✅ 4.4 — Sync External File Renames
**Status**: Complete

- Updates item title, filename, and file path on external renames
- Handles edge cases: non-.md → .md (create), .md → non-.md (delete)
- Updates ItemOrder in `.kanban.json`

### ✅ 4.5 — Sync External Content Changes
**Status**: Complete

- Re-reads file content on external modifications
- Updates ContentPreview and FullContent properties
- Includes debouncing (300ms) to handle multiple change events
- Retry logic for locked files

### ✅ 4.6 — Debouncing and Thread Marshaling
**Status**: Complete

- Per-file debounce using `ConcurrentDictionary<string, CancellationTokenSource>`
- 300ms debounce delay for creates and changes
- All events marshaled to UI thread via `DispatcherQueue.TryEnqueue()`

### ⚠️ 4.7 — Conflict Handling
**Status**: Not Implemented (Deferred to Phase 6)

Conflict handling (info bar in detail modal) is deferred to Phase 6 when the rich markdown editor is implemented. Currently, external changes update the card silently, which is safe since there's no in-app editing yet.

---

## 4.1 — FileWatcherService

### What
A service that monitors the board's root directory and all column subdirectories for file system changes using `FileSystemWatcher`.

### File: `Services/FileWatcherService.cs`

### Architecture
```
FileWatcherService
├── _rootWatcher       (watches root dir for folder create/delete/rename)
├── _columnWatchers    (Dictionary<string, FileSystemWatcher> — one per column folder)
└── Events → UI thread via DispatcherQueue
```

### Setup
```csharp
public class FileWatcherService : IDisposable
{
    private FileSystemWatcher? _rootWatcher;
    private readonly Dictionary<string, FileSystemWatcher> _columnWatchers = new();
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly CancellationTokenSource _debounceTokenSource = new();

    // Events raised on UI thread
    public event EventHandler<ItemCreatedEventArgs>? ItemCreated;
    public event EventHandler<ItemDeletedEventArgs>? ItemDeleted;
    public event EventHandler<ItemRenamedEventArgs>? ItemRenamed;
    public event EventHandler<ItemChangedEventArgs>? ItemContentChanged;
    public event EventHandler<ColumnCreatedEventArgs>? ColumnCreated;
    public event EventHandler<ColumnDeletedEventArgs>? ColumnDeleted;
    public event EventHandler<ColumnRenamedEventArgs>? ColumnRenamed;
}
```

### Root Watcher Configuration
```csharp
_rootWatcher = new FileSystemWatcher(rootPath)
{
    NotifyFilter = NotifyFilters.DirectoryName,
    IncludeSubdirectories = false,
    EnableRaisingEvents = true
};
_rootWatcher.Created += OnColumnFolderCreated;
_rootWatcher.Deleted += OnColumnFolderDeleted;
_rootWatcher.Renamed += OnColumnFolderRenamed;
```

### Column Watcher Configuration (one per column folder)
```csharp
var watcher = new FileSystemWatcher(columnFolderPath)
{
    Filter = "*.md",
    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
    IncludeSubdirectories = false,
    EnableRaisingEvents = true
};
watcher.Created += OnItemFileCreated;
watcher.Deleted += OnItemFileDeleted;
watcher.Renamed += OnItemFileRenamed;
watcher.Changed += OnItemFileChanged;
```

### Self-Initiated Change Suppression
The app itself writes to the file system (creating items, moving files, etc.). These app-initiated changes should NOT trigger a sync cycle.

**Strategy**: Maintain a `HashSet<string> _pendingOperations` of file paths currently being modified by the app. Before raising a sync event, check if the path is in the pending set. If so, remove it and skip the event.

```csharp
public void SuppressNextEvent(string filePath)
{
    _pendingOperations.Add(Path.GetFullPath(filePath));
}

private void OnItemFileCreated(object sender, FileSystemEventArgs e)
{
    if (_pendingOperations.Remove(Path.GetFullPath(e.FullPath))) return;
    // proceed with sync...
}
```

### Lifecycle
- `StartWatching(string rootPath)` — initialize root + column watchers
- `StopWatching()` — dispose all watchers
- `AddColumnWatcher(string folderPath)` — when a new column is created
- `RemoveColumnWatcher(string folderPath)` — when a column is deleted
- `Dispose()` — clean up all watchers

### Acceptance Criteria
- Watchers are created for root and all column folders on board load
- Watchers are properly disposed when the board is closed or a new folder is opened
- App-initiated changes do not trigger duplicate sync events

---

## 4.2 — Sync External File Creates

### What
When a `.md` file is created in a column folder outside the app, a new card should appear in the corresponding column.

### Flow
1. `FileSystemWatcher.Created` fires for a new `.md` file
2. Debounce (300ms) — wait for the file to be fully written
3. After debounce, attempt to read the file
4. If read succeeds:
   - Create a new `KanbanItemViewModel` from the file
   - Add to the appropriate `ColumnViewModel.Items` (appended at the end)
   - Update `ItemOrder` in `.kanban.json`
5. Dispatch to UI thread via `DispatcherQueue.TryEnqueue()`

### Edge Cases
- File is still being written (locked) → retry after delay (up to 3 retries, 500ms apart)
- File was created then immediately deleted → skip
- Non-`.md` file created → ignore

### Acceptance Criteria
- Creating a `.md` file in Explorer causes a card to appear in the app
- Card shows correct title and content preview

---

## 4.3 — Sync External File Deletes

### What
When a `.md` file is deleted from a column folder outside the app, the corresponding card is removed.

### Flow
1. `FileSystemWatcher.Deleted` fires
2. Find the `KanbanItemViewModel` in the column by matching `FileName`
3. Remove from `ColumnViewModel.Items`
4. Remove from `ItemOrder` in `.kanban.json`
5. Save `.kanban.json`

### Edge Cases
- File was moved (not deleted) — `FileSystemWatcher` reports this as delete + create. Handle gracefully by not showing error for the delete, and creating a new card for the create.
- File was already removed from UI (app-initiated delete) → suppressed via pending operations

### Acceptance Criteria
- Deleting a `.md` file in Explorer removes the card from the app
- No error shown for normal deletions

---

## 4.4 — Sync External File Renames

### What
When a `.md` file is renamed outside the app, the card title and references are updated.

### Flow
1. `FileSystemWatcher.Renamed` fires with old and new file names
2. Find the `KanbanItemViewModel` by old `FileName`
3. Update `FileName`, `FilePath`, and `Title` (derive from new filename)
4. Update `ItemOrder` in `.kanban.json` (replace old filename with new)
5. Update `groups.json` if the item was in a group (replace old filename with new)
6. Save config files

### Edge Cases
- File renamed to a non-`.md` extension → treat as delete
- Non-`.md` file renamed to `.md` → treat as create

### Acceptance Criteria
- Renaming a file in Explorer updates the card title in the app
- Config files are updated with the new filename

---

## 4.5 — Sync External Content Changes

### What
When the content of a `.md` file is modified outside the app (e.g., in VS Code), the card's preview and any open editor should update.

### Flow
1. `FileSystemWatcher.Changed` fires
2. Debounce (300ms) — text editors may trigger multiple change events during save
3. Re-read the file content
4. Update `KanbanItemViewModel.ContentPreview` and `FullContent`
5. If the item detail modal is open for this item, refresh the editor/preview

### Edge Cases
- File is locked during read → retry with backoff
- Content hasn't actually changed (touch/attribute update) → compare hash, skip if same
- App is currently editing this file → conflict handling (see 4.7)

### Acceptance Criteria
- Editing a file in an external editor updates the card preview in real-time
- Open detail modal refreshes when the file is changed externally

---

## 4.6 — Debouncing and Thread Marshaling

### What
Ensure file system events are properly debounced and all UI updates happen on the UI thread.

### Debounce Strategy
`FileSystemWatcher` is notorious for firing multiple events for a single logical operation (e.g., saving a file triggers Created + Changed, or multiple Changed events).

**Per-file debounce** using a dictionary of `CancellationTokenSource`:
```csharp
private readonly ConcurrentDictionary<string, CancellationTokenSource> _debounceTimers = new();

private async void OnItemFileChanged(object sender, FileSystemEventArgs e)
{
    var path = e.FullPath;
    
    // Cancel previous debounce for this file
    if (_debounceTimers.TryGetValue(path, out var existing))
        existing.Cancel();
    
    var cts = new CancellationTokenSource();
    _debounceTimers[path] = cts;
    
    try
    {
        await Task.Delay(300, cts.Token);
        _debounceTimers.TryRemove(path, out _);
        
        _dispatcherQueue.TryEnqueue(() =>
        {
            ItemContentChanged?.Invoke(this, new ItemChangedEventArgs(path));
        });
    }
    catch (TaskCanceledException) { /* debounced */ }
}
```

### Thread Marshaling
All events raised by `FileWatcherService` must be dispatched to the UI thread:
```csharp
_dispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, () =>
{
    // UI update code here
});
```

### Acceptance Criteria
- Rapid file changes result in a single UI update (not one per event)
- No `System.Runtime.InteropServices.COMException` from cross-thread UI access
- Debounce delay is short enough to feel responsive (~300ms)

---

## 4.7 — Conflict Handling

### What
Handle the scenario where a file is being edited both in the app and externally simultaneously.

### Strategy: Last-Write-Wins with Notification

1. When the app detects an external change to a file that is currently open in the detail modal:
   - Show an **info bar** at the top of the modal: "This file was changed externally. [Reload] [Keep Mine]"
   - **Reload**: Discard app changes, load external content
   - **Keep Mine**: Dismiss the notification, continue editing. Next save overwrites the external changes.
2. If the file is NOT open in the modal (just a card in the board):
   - Silently update the preview and content — no conflict possible since user isn't editing.

### Future Enhancement (Out of Scope)
- Three-way merge
- Diff view showing both versions

### Acceptance Criteria
- Editing a file in the app while it's changed externally shows a notification
- User can choose to reload or keep their changes
- No data loss in either scenario
