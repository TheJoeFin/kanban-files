# Phase 5 — Grouping

## Objective
Allow users to organize items within a column into named, collapsible, draggable groups. Group metadata is stored in a `groups.json` file within each column folder.

---

## 5.1 — `groups.json` Schema

### What
Define the JSON schema for group metadata stored in each column folder.

### File Location
Each column folder may contain a `groups.json` file:
```
Board Root/
├── To Do/
│   ├── groups.json        ← group metadata for this column
│   ├── task-a.md
│   ├── task-b.md
│   └── task-c.md
├── In Progress/
│   └── feature-x.md
└── .kanban.json
```

### Schema
```json
{
  "groups": [
    {
      "name": "High Priority",
      "itemFileNames": ["task-a.md", "task-c.md"],
      "isCollapsed": false
    },
    {
      "name": "Backlog",
      "itemFileNames": ["task-b.md"],
      "isCollapsed": true
    }
  ]
}
```

### Rules
- Each item can belong to **at most one group**
- Items not listed in any group's `itemFileNames` are considered **ungrouped**
- Ungrouped items are displayed at the **top** of the column, before any groups
- The order of groups in the `groups` array determines display order
- The order of `itemFileNames` within a group determines item order within that group
- If a file referenced in `groups.json` no longer exists on disk, it is silently removed from the group on next load
- `groups.json` is ignored by the item enumeration (it is not a Kanban card)

### JSON Serialization
Same options as `.kanban.json`:
```csharp
new JsonSerializerOptions
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
};
```

---

## 5.2 — GroupService

### What
A service for reading and writing `groups.json` files.

### File: `Services/GroupService.cs`

### Methods
```csharp
public class GroupService
{
    // Load groups from a column's groups.json
    Task<GroupsConfig> LoadGroupsAsync(string columnFolderPath);
    
    // Save groups to a column's groups.json
    Task SaveGroupsAsync(string columnFolderPath, GroupsConfig config);
    
    // Add item to a group
    Task AddItemToGroupAsync(string columnFolderPath, string groupName, string fileName, int? index = null);
    
    // Remove item from its group (becomes ungrouped)
    Task RemoveItemFromGroupAsync(string columnFolderPath, string fileName);
    
    // Create a new empty group
    Task CreateGroupAsync(string columnFolderPath, string groupName);
    
    // Delete a group (items become ungrouped)
    Task DeleteGroupAsync(string columnFolderPath, string groupName);
    
    // Rename a group
    Task RenameGroupAsync(string columnFolderPath, string oldName, string newName);
    
    // Reorder groups
    Task ReorderGroupsAsync(string columnFolderPath, List<string> groupNamesInOrder);
    
    // Move item between groups or to ungrouped
    Task MoveItemBetweenGroupsAsync(string columnFolderPath, string fileName, string? sourceGroup, string? targetGroup, int targetIndex);
    
    // Clean up references to files that no longer exist
    Task CleanupStaleReferencesAsync(string columnFolderPath, IEnumerable<string> existingFileNames);
}
```

### Error Handling
- If `groups.json` does not exist → return empty `GroupsConfig` (no groups)
- If `groups.json` is malformed → log warning, back up as `groups.json.bak`, return empty config
- Duplicate group names → reject with error
- Adding an item already in a group → move it from old group to new group

### Acceptance Criteria
- Groups can be loaded, saved, created, deleted, and renamed
- Item-group assignments persist correctly
- Missing `groups.json` results in a groupless column (no errors)
- Stale file references are cleaned up

---

## 5.3 — Column UI with Groups

### What
Update the `ColumnControl` to render items organized into collapsible group sections.

### Visual Layout
```
┌─────────────────────┐
│  Column Name    ⋯   │
├─────────────────────┤
│  ┌───────────────┐  │  ← Ungrouped items (at top)
│  │  Item Card    │  │
│  └───────────────┘  │
│                     │
│  ▼ High Priority    │  ← Group header (collapsible)
│  ┌───────────────┐  │
│  │  Item Card    │  │
│  └───────────────┘  │
│  ┌───────────────┐  │
│  │  Item Card    │  │
│  └───────────────┘  │
│                     │
│  ▶ Backlog (2)      │  ← Collapsed group (shows count)
│                     │
│  [+ Add Item]       │
└─────────────────────┘
```

### Group Header Control
A clickable header row for each group:

```
┌─────────────────────────┐
│  ▼ Group Name    (3) ⋯  │
└─────────────────────────┘
```

- **▼ / ▶**: Toggle collapse/expand (chevron icon)
- **Group Name**: Editable on double-click
- **(3)**: Item count
- **⋯**: Options menu (Rename, Delete Group, Collapse/Expand)

### ViewModel: `GroupViewModel`
```csharp
public partial class GroupViewModel : ObservableObject
{
    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private bool isCollapsed = false;
    public ObservableCollection<KanbanItemViewModel> Items { get; } = new();
    
    [RelayCommand] private void ToggleCollapse() => IsCollapsed = !IsCollapsed;
    [RelayCommand] private async Task Rename() { ... }
    [RelayCommand] private async Task Delete() { ... }
}
```

### Column ViewModel Update
```csharp
public partial class ColumnViewModel : ObservableObject
{
    public ObservableCollection<KanbanItemViewModel> UngroupedItems { get; } = new();
    public ObservableCollection<GroupViewModel> Groups { get; } = new();
}
```

### Rendering Strategy
Use a single `ItemsControl` with a heterogeneous item template selector, or structure as:
1. `ItemsControl` for ungrouped items
2. `ItemsRepeater` for groups, each containing an `Expander`-like control with nested `ItemsControl`

### Acceptance Criteria
- Ungrouped items appear at the top of the column
- Groups appear below ungrouped items with collapsible headers
- Collapsed groups show item count but hide items
- Collapse state persists in `groups.json`

---

## 5.4 — Drag & Drop Groups (Reorder)

### What
Allow users to reorder groups within a column by dragging the group header.

### Implementation
- Drag handle: the group header area
- Drop target: between other group headers in the same column
- On drop:
  1. Reorder `Groups` in `ColumnViewModel`
  2. Update `groups.json` group order
  3. Save `groups.json`

### Visual Feedback
- Horizontal insertion line between groups during drag
- Dragged group header shows as semi-transparent drag visual
- Groups can only be reordered within the same column (no cross-column group drag)

### Constraints
- Groups cannot be dragged to other columns
- The ungrouped section is always at the top and cannot be reordered

### Acceptance Criteria
- Groups can be reordered via drag-and-drop on the group header
- New order is persisted in `groups.json`
- Ungrouped section stays at the top

---

## 5.5 — Drag & Drop Items Into/Out of Groups

### What
Allow items to be dragged into groups, out of groups, and between groups within the same column.

### Scenarios

#### 1. Ungrouped → Group
- Drag an ungrouped item and drop it into a group section
- Item is added to the group's `itemFileNames` at the drop index
- Item is removed from ungrouped items
- Save `groups.json`

#### 2. Group → Ungrouped
- Drag an item from a group and drop it in the ungrouped area (top of column)
- Item is removed from the group's `itemFileNames`
- Item appears in ungrouped items
- Save `groups.json`

#### 3. Group → Different Group
- Drag an item from one group and drop it into another group
- Remove from source group, add to target group at drop index
- Save `groups.json`

#### 4. Cross-Column with Group
- When an item is moved to a different column (Phase 3), it becomes ungrouped in the target column
- The source column's `groups.json` is updated to remove the item
- The file is moved on disk as before

### Drop Zone Detection
The column control needs to distinguish between:
- Dropping in the ungrouped area (top of column, before first group)
- Dropping in a specific group (between group items)
- Dropping between groups (not into a group)

Use hit-testing against group header bounds and item bounds to determine the target.

### Acceptance Criteria
- Items can be moved between ungrouped area and groups
- Items can be moved between different groups
- Cross-column moves remove item from source group
- `groups.json` is updated correctly for all operations

---

## 5.6 — Group CRUD UI

### What
Provide UI controls for creating, renaming, and deleting groups.

### Create Group
- **Trigger**: Column options menu (⋯) → "Add Group"
- **Flow**: Dialog or inline input for group name → create empty group → appears at bottom of groups list
- **Validation**: Name cannot be empty, must be unique within the column

### Rename Group
- **Trigger**: Double-click group header name, or group options menu → "Rename"
- **Flow**: Inline text edit on the group header → save to `groups.json`
- **Validation**: Same as create

### Delete Group
- **Trigger**: Group options menu (⋯) → "Delete Group"
- **Flow**: Confirmation dialog: "Delete group '{name}'? Items will become ungrouped."
- **On confirm**:
  - Items in the group are moved to ungrouped
  - Group is removed from `groups.json`
  - UI updates immediately

### Acceptance Criteria
- Groups can be created with a name
- Groups can be renamed inline or via menu
- Deleting a group moves its items to ungrouped (items are NOT deleted from disk)
- All operations persist to `groups.json`
