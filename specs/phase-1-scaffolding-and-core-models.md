# Phase 1 — Project Scaffolding & Core Models

## Status: ✅ COMPLETE

## Objective
Set up the WinUI3 project structure, define all core data models, and implement the foundational services for reading/writing board configuration and file system data.

---

## 1.1 — Create WinUI3 Project ✅

### What
Create a WinUI3 packaged desktop app project named `KanbanFiles`.

### Details
- **Target**: .NET 10, WinAppSDK (latest stable)
- **Project type**: WinUI3 Blank App, Packaged (MSIX)
- **Solution name**: `KanbanFiles`
- **Single-project** structure with folders for separation of concerns

### Folder Structure
```
KanbanFiles/
├── App.xaml / App.xaml.cs
├── MainWindow.xaml / MainWindow.xaml.cs
├── Models/
├── Services/
├── Controls/
├── ViewModels/
├── Helpers/
├── Assets/
└── Package.appxmanifest
```

### NuGet Packages
| Package | Purpose |
|---|---|
| `Microsoft.WindowsAppSDK` | WinUI3 framework |
| `Microsoft.Windows.SDK.BuildTools` | Windows SDK build support |
| `CommunityToolkit.Mvvm` | MVVM helpers (ObservableObject, RelayCommand) |
| `CommunityToolkit.WinUI.UI` | UI helpers and converters |
| `Markdig` | Markdown parsing (used in Phase 6, install now) |
| `System.Text.Json` | JSON serialization (included in .NET) |

### Acceptance Criteria
- Project builds and launches an empty window
- All NuGet packages restored successfully
- Folder structure created

---

## 1.2 — Define Core Models ✅

### What
Create C# model classes that represent the domain: boards, columns, items, and groups.

### Models

#### `Board`
Represents the entire Kanban board. Serialized to/from `.kanban.json`.

```csharp
namespace KanbanFiles.Models;

public class Board
{
    public string Name { get; set; } = "Kanban Board";
    public string RootPath { get; set; } = string.Empty;
    public List<ColumnConfig> Columns { get; set; } = new();
}
```

#### `ColumnConfig`
Stored inside `.kanban.json` to persist column ordering and metadata. References a folder by name.

```csharp
public class ColumnConfig
{
    public string FolderName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public List<string> ItemOrder { get; set; } = new(); // filenames in display order
}
```

#### `Column`
Runtime model representing a loaded column (not serialized directly).

```csharp
public class Column
{
    public string Name { get; set; } = string.Empty;
    public string FolderPath { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public ObservableCollection<KanbanItem> Items { get; set; } = new();
    public ObservableCollection<Group> Groups { get; set; } = new();
}
```

#### `KanbanItem`
Represents a single `.md` file as a Kanban card.

```csharp
public class KanbanItem
{
    public string Title { get; set; } = string.Empty;       // filename without .md extension
    public string FileName { get; set; } = string.Empty;     // e.g. "my-task.md"
    public string FilePath { get; set; } = string.Empty;     // full path
    public string ContentPreview { get; set; } = string.Empty; // first ~2 lines
    public string FullContent { get; set; } = string.Empty;
    public string? GroupName { get; set; }                    // null = ungrouped
    public DateTime LastModified { get; set; }
}
```

#### `Group`
Represents a collapsible group of items within a column. Serialized to `groups.json`.

```csharp
public class Group
{
    public string Name { get; set; } = string.Empty;
    public List<string> ItemFileNames { get; set; } = new(); // ordered list of filenames
    public bool IsCollapsed { get; set; } = false;
}
```

#### `GroupsConfig`
Root object for `groups.json` per column folder.

```csharp
public class GroupsConfig
{
    public List<Group> Groups { get; set; } = new();
}
```

### Acceptance Criteria
- All model classes compile
- JSON serialization round-trips correctly for `Board` and `GroupsConfig`

---

## 1.3 — Implement `BoardConfigService` ✅

### What
A service that reads and writes the `.kanban.json` file at the root of the opened directory.

### File: `Services/BoardConfigService.cs`

### Responsibilities
1. **Load**: Read `.kanban.json` from a given directory path, deserialize to `Board`
2. **Save**: Serialize `Board` to `.kanban.json` with indented formatting
3. **Initialize**: If `.kanban.json` does not exist and no subfolders are present, create default columns:
   - `To Do` (folder: `To Do`)
   - `In Progress` (folder: `In Progress`)
   - `Done` (folder: `Done`)
   - Create the corresponding subfolders on disk
   - Write the default `.kanban.json`
4. **Initialize (existing folders)**: If `.kanban.json` does not exist but subfolders are present, generate a config from existing subfolders (alphabetical order)

### `.kanban.json` Example
```json
{
  "Name": "My Project Board",
  "Columns": [
    {
      "FolderName": "To Do",
      "DisplayName": "To Do",
      "SortOrder": 0,
      "ItemOrder": ["task-1.md", "task-2.md"]
    },
    {
      "FolderName": "In Progress",
      "DisplayName": "In Progress",
      "SortOrder": 1,
      "ItemOrder": []
    },
    {
      "FolderName": "Done",
      "DisplayName": "Done",
      "SortOrder": 2,
      "ItemOrder": []
    }
  ]
}
```

### JSON Serialization Options
```csharp
private static readonly JsonSerializerOptions _jsonOptions = new()
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
};
```

### Error Handling
- If `.kanban.json` is malformed, log warning, back up the corrupt file as `.kanban.json.bak`, and re-initialize
- All file I/O should be `async` (`ReadAllTextAsync`, `WriteAllTextAsync`)

### Acceptance Criteria
- Loading a valid `.kanban.json` returns a correct `Board` object
- Saving a `Board` writes human-readable JSON
- Opening an empty folder auto-creates 3 default columns + folders + config
- Opening a folder with existing subfolders generates config from those folders

---

## 1.4 — Implement `FileSystemService` ✅

### What
A service that enumerates the file system to build the runtime board model from folders and `.md` files.

### File: `Services/FileSystemService.cs`

### Responsibilities
1. **Enumerate columns**: List subfolders in the root directory, map to `Column` objects using order from `Board.Columns`
2. **Enumerate items**: For each column folder, list `*.md` files, map to `KanbanItem` objects
3. **Read item content**: Read full `.md` content from disk (async)
4. **Write item content**: Write content to `.md` file (async)
5. **Create item**: Create a new `.md` file with a given name in a column folder
6. **Delete item**: Delete a `.md` file from disk
7. **Move item**: Move a `.md` file from one column folder to another
8. **Create column folder**: Create a new subfolder
9. **Delete column folder**: Delete a subfolder (with confirmation — it may contain files)
10. **Generate content preview**: Extract first ~2 non-empty lines from `.md` content for card preview

### File Filtering Rules
- **Include**: `*.md` files only as Kanban items
- **Exclude**: `.kanban.json`, `groups.json`, any dotfiles, hidden files, system files
- **Ignore**: Subdirectories within column folders (they are not items)

### Content Preview Logic
```
Given file content:
  # My Task Title
  
  This is the description of the task.
  It has multiple lines.

Preview: "This is the description of the task. It has multiple lines."
(Skip lines starting with # for preview, take first 2 non-empty content lines)
```

### Error Handling
- File not found → return null / empty, log warning
- Permission denied → throw with user-friendly message
- All I/O is async

### Acceptance Criteria
- Enumerating a folder with `.md` files returns correct `KanbanItem` list
- Content preview extracts meaningful text (skips headings, blank lines)
- Create/delete/move operations work correctly on disk
- Non-`.md` files are ignored
