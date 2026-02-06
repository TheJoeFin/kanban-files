# KanbanFiles - Developer Notes

## Build Commands
- **Build project**: `cd D:\source\kanban-files\KanbanFiles; dotnet build`
- **Run project**: `cd D:\source\kanban-files\KanbanFiles; dotnet run`

## Project Structure
- **Models/**: Core domain models (Board, Column, KanbanItem, Group, etc.)
- **Services/**: Business logic services (BoardConfigService, FileSystemService)
- **ViewModels/**: MVVM ViewModels with CommunityToolkit.Mvvm
- **Views/**: WinUI3 XAML pages
- **Controls/**: Reusable UserControls (ColumnControl, KanbanItemCardControl)

## Key Implementation Details
- **JSON Config**: `.kanban.json` stores board configuration with camelCase naming
- **File Filtering**: Only `.md` files are processed as Kanban items
- **Content Preview**: First 2 non-empty, non-header lines from markdown
- **Default Columns**: Auto-created on empty folders: "To Do", "In Progress", "Done"
- **Corrupt Config Handling**: Automatically backs up corrupt `.kanban.json` with timestamp
- **Dialog Pattern**: ContentDialogs for user input (add/rename/delete operations)
- **Event-Driven UI**: ViewModels raise events handled by code-behind for dialogs
- **Drag & Drop**: 
  - Items: DragPayload model with JSON serialization, opacity feedback during drag
  - Columns: Custom "KanbanColumnReorder" data format, horizontal position calculation
  - Drop index calculation based on pointer position relative to element midpoints
  - FileSystemService.MoveItemAsync handles physical file moves
  - ItemOrder/SortOrder persisted to `.kanban.json`
- **File Watching**:
  - FileWatcherService monitors all file system changes with two FileSystemWatcher instances
  - Self-initiated change suppression prevents duplicate events during app operations
  - Debouncing (300ms) for creates/changes to handle multiple events
  - Retry logic (3 attempts, 500ms) for file access errors
  - All events marshaled to UI thread via DispatcherQueue
  - App.MainDispatcher static property provides DispatcherQueue access

## Completed Phases
- ✅ Phase 1: Project scaffolding, core models, BoardConfigService, FileSystemService
- ✅ Phase 2: Basic Board UI with full CRUD operations (create/read/update/delete columns and items)
- ✅ Phase 3: Drag & Drop (items between/within columns, column reordering)
- ✅ Phase 4: Two-Way File System Sync (FileWatcherService, external file monitoring)
- ✅ Phase 5: Grouping (GroupService, GroupViewModel, group CRUD, drag & drop for groups and items, collapse state persistence)

## In Progress
- 🚧 Phase 6: Rich Markdown Editing
  - Markdig NuGet package added (v0.44.0)
  - BoolToVisibilityConverter created for UI bindings
  - **Known Issue**: XAML compiler error (exit code 1) when adding ItemDetailPage.xaml
    - Error doesn't provide specific details in build output
    - Persists through clean builds and obj/bin folder deletion
    - Baseline code (without Phase 6 files) builds successfully
    - May need investigation into WinUI3 XAML compilation pipeline or SDK version compatibility
