# Phase 2 — Basic Board UI ✅ COMPLETE

## Objective
Build the core visual layout of the Kanban board: a horizontal scrollable set of columns, each containing a vertical list of item cards. Implement folder opening, and the ability to create new items and columns.

**Status**: Completed - All acceptance criteria met

---

## 2.1 — Main Window Layout

### What
Create the `MainWindow` with a top command bar and a horizontal scrolling board area.

### Layout Structure
```
┌──────────────────────────────────────────────────┐
│  [Open Folder]  [+ Column]       Board Name      │  ← Command Bar
├──────────────────────────────────────────────────┤
│  ┌──────┐  ┌──────┐  ┌──────┐                   │
│  │Col 1 │  │Col 2 │  │Col 3 │    ← Horizontal   │
│  │      │  │      │  │      │      ScrollViewer  │
│  │ Card │  │ Card │  │ Card │                    │
│  │ Card │  │      │  │ Card │                    │
│  │      │  │      │  │ Card │                    │
│  │[+New]│  │[+New]│  │[+New]│                    │
│  └──────┘  └──────┘  └──────┘                    │
└──────────────────────────────────────────────────┘
```

### XAML Structure (MainWindow.xaml)
- Root: `Grid` with two rows (command bar, board)
- Command bar: `CommandBar` or `StackPanel` with buttons
- Board area: `ScrollViewer` (horizontal scroll enabled, vertical disabled)
  - Inside: `ItemsRepeater` or `ItemsControl` with horizontal `StackLayout`
  - ItemTemplate: `ColumnControl`

### ViewModel: `MainViewModel`
```csharp
public partial class MainViewModel : ObservableObject
{
    [ObservableProperty] private string boardName = "Kanban Board";
    [ObservableProperty] private string rootPath = string.Empty;
    [ObservableProperty] private bool isBoardLoaded = false;
    public ObservableCollection<ColumnViewModel> Columns { get; } = new();

    [RelayCommand] private async Task OpenFolder() { ... }
    [RelayCommand] private async Task AddColumn() { ... }
}
```

### Acceptance Criteria ✅
- ✅ Window displays a command bar and empty board area
- ✅ Horizontal scrolling works when columns overflow the window width
- ✅ Board area shows a placeholder message when no folder is opened

---

## 2.2 — Column Control

### What
A reusable `UserControl` that displays a single Kanban column with a header and vertical list of items.

### Visual Design
```
┌─────────────────────┐
│  Column Name    ⋯   │  ← Header (editable name + options menu)
├─────────────────────┤
│  ┌───────────────┐  │
│  │  Item Card    │  │
│  └───────────────┘  │
│  ┌───────────────┐  │
│  │  Item Card    │  │
│  └───────────────┘  │
│                     │
│  [+ Add Item]       │  ← Footer button
└─────────────────────┘
```

### Properties
- Fixed width: ~280px
- Full height of the board area (stretch vertically)
- Internal vertical `ScrollViewer` for overflow items
- Column background: subtle contrast from board background (`SolidColorBrush` from theme)

### ViewModel: `ColumnViewModel`
```csharp
public partial class ColumnViewModel : ObservableObject
{
    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private string folderPath = string.Empty;
    [ObservableProperty] private int sortOrder;
    public ObservableCollection<KanbanItemViewModel> Items { get; } = new();

    [RelayCommand] private async Task AddItem() { ... }
    [RelayCommand] private async Task RenameColumn() { ... }
    [RelayCommand] private async Task DeleteColumn() { ... }
}
```

### Options Menu (⋯ button)
- Rename Column
- Delete Column (with confirmation dialog)
- Add Group (Phase 5)

### Acceptance Criteria ✅
- ✅ Column renders with header, scrollable item list, and footer
- ✅ Column has a fixed width and stretches vertically
- ✅ Options menu opens on click

---

## 2.3 — Kanban Item Card Control

### What
A `UserControl` that displays a single Kanban item card within a column.

### Visual Design
```
┌───────────────────────┐
│  Task Title           │  ← Bold, single line, ellipsis on overflow
│  First line of the    │  ← Content preview, 2 lines max, muted color
│  markdown content...  │
│                  📎 3 │  ← Optional: metadata row (future)
└───────────────────────┘
```

### Properties
- Card background: elevated surface color (card-like appearance)
- Corner radius: 4–8px
- Padding: 12px
- Margin between cards: 6px
- Hover: subtle elevation or border change
- Click: opens item detail (Phase 6); for now, no-op or inline edit

### ViewModel: `KanbanItemViewModel`
```csharp
public partial class KanbanItemViewModel : ObservableObject
{
    [ObservableProperty] private string title = string.Empty;
    [ObservableProperty] private string fileName = string.Empty;
    [ObservableProperty] private string filePath = string.Empty;
    [ObservableProperty] private string contentPreview = string.Empty;
    [ObservableProperty] private string fullContent = string.Empty;
    [ObservableProperty] private string? groupName;
    [ObservableProperty] private DateTime lastModified;

    [RelayCommand] private void OpenDetail() { ... }
    [RelayCommand] private async Task Delete() { ... }
}
```

### Context Menu (Right-Click)
- Open
- Rename
- Move to → (submenu of other columns)
- Delete (with confirmation)

### Acceptance Criteria ✅
- ✅ Card displays title and 2-line preview
- ✅ Card has visual hover state
- ✅ Right-click context menu appears with options
- ✅ Click triggers open action (placeholder for Phase 6)

---

## 2.4 — Open Folder Command

### What
Allow the user to pick a folder from disk to open as a Kanban board.

### Flow
1. User clicks "Open Folder" in the command bar
2. `FolderPicker` dialog opens
3. User selects a folder
4. `BoardConfigService.LoadOrInitializeAsync(path)` is called:
   - If `.kanban.json` exists → load it
   - If no subfolders exist → create defaults ("To Do", "In Progress", "Done")
   - If subfolders exist but no config → generate config from folders
5. `FileSystemService` enumerates all columns and items
6. `MainViewModel.Columns` is populated, UI updates

### WinUI3 Folder Picker
```csharp
var picker = new FolderPicker();
picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
picker.FileTypeFilter.Add("*");

// Initialize with window handle (required for WinUI3)
var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

var folder = await picker.PickSingleFolderAsync();
```

### Acceptance Criteria ✅
- ✅ Folder picker opens and returns selected path
- ✅ Board loads correctly from folder with existing config
- ✅ Board initializes defaults for empty folder
- ✅ Board generates config from existing subfolders
- ✅ UI populates with columns and items after loading

---

## 2.5 — New Item Command

### What
Create a new `.md` file in a column's folder and display it as a new card.

### Flow
1. User clicks "+ Add Item" at the bottom of a column
2. An inline text input appears (or a small dialog) asking for the item name
3. On confirm:
   - Sanitize the name for use as a filename (replace invalid chars, trim whitespace)
   - Append `.md` extension if not present
   - Call `FileSystemService.CreateItemAsync(columnPath, fileName)`
   - File is created with default content: `# {Item Name}\n\n`
   - New `KanbanItemViewModel` added to the column's `Items` collection
   - Item order in `.kanban.json` is updated

### Filename Sanitization
- Replace characters not allowed in Windows filenames: `\ / : * ? " < > |`
- Replace spaces with hyphens (or keep spaces — user preference; keep spaces for readability)
- Limit to 200 characters
- If file already exists, append `(1)`, `(2)`, etc.

### Acceptance Criteria ✅
- ✅ New item card appears in the column immediately
- ✅ `.md` file is created on disk with correct content
- ✅ `.kanban.json` item order is updated
- ✅ Duplicate names are handled gracefully

---

## 2.6 — New Column Command

### What
Create a new subfolder and add it as a column to the board.

### Flow
1. User clicks "+ Column" in the command bar
2. A dialog or inline input asks for the column name
3. On confirm:
   - Create subfolder in the board root directory
   - Add `ColumnConfig` entry to `Board.Columns` with next sort order
   - Save `.kanban.json`
   - Add `ColumnViewModel` to `MainViewModel.Columns`

### Validation
- Column name must not be empty
- Column name must be a valid folder name (same sanitization as items)
- Column name must not duplicate an existing column

### Acceptance Criteria ✅
- ✅ New empty column appears on the board
- ✅ Subfolder is created on disk
- ✅ `.kanban.json` is updated with the new column
- ✅ Duplicate column names are rejected with a message
