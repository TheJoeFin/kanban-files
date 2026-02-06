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

## Completed Phases
- ✅ Phase 1: Project scaffolding, core models, BoardConfigService, FileSystemService
- ✅ Phase 2: Basic Board UI with full CRUD operations (create/read/update/delete columns and items)
- ✅ Phase 3: Drag & Drop (items between/within columns, column reordering)
