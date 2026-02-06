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
- ✅ Phase 7: Polish & Edge Cases
  - ✅ InfoBar notification system with auto-dismiss
  - ✅ Config corruption notifications (backup + user notification)
  - ✅ Keyboard shortcuts (Ctrl+O, Ctrl+Shift+N, F5)
  - ✅ Existing subfolders handling (already implemented in Phase 1)
  - ✅ File filtering for .md, hidden files (already implemented)
  - ✅ ThemeResource usage throughout UI (already implemented)
  - ✅ Window state persistence (width, height, position, maximized state)

## In Progress
- 🚧 Phase 6: Rich Markdown Editing
  - **BLOCKED**: WindowsAppSDK 1.8.251222000 XAML Compiler Bug
  - Cannot add new XAML files to project - compiler fails with exit code 1
  - Affects ANY new XAML file (Page or UserControl), even minimal definitions
  - ItemDetailViewModel.cs is complete with Markdig integration
  - Event wiring for OpenDetailCommand is complete
  - Workaround needed: ContentDialog with inline XAML or WindowsAppSDK upgrade
  - See ISSUES.md (2/6/2026 7:56 PM) for full investigation details
