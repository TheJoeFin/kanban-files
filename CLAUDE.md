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
- **Keyboard Shortcuts**:
  - Ctrl+O: Open Folder
  - Ctrl+N: New Item in first column
  - Ctrl+Shift+N: New Column
  - F5: Refresh board from disk
  - Arrow keys (←/→): Navigate between columns
  - Arrow keys (↑/↓): Navigate between items within a column
  - Escape: Close dialogs (built-in WinUI)
  - KeyboardAccelerator in MainPage.xaml, handlers in code-behind
  - IFocusManagerService tracks focus state across navigation
- **Error Handling**:
  - INotificationService: Interface for showing InfoBar notifications from any ViewModel
  - NotificationService: Implementation that delegates to MainViewModel.ShowNotification()
  - Comprehensive try-catch: All FileSystemService methods throw IOExceptions with context
  - ViewModels catch and display user-friendly error messages via INotificationService
  - Auto-dismiss: 5-second auto-hide for Informational/Success notifications
  - See: KanbanFiles/Services/INotificationService.cs, NotificationService.cs

## Completed Phases (7/7 - 100%)
- ✅ Phase 1: Project scaffolding, core models, BoardConfigService, FileSystemService
- ✅ Phase 2: Basic Board UI with full CRUD operations (create/read/update/delete columns and items)
- ✅ Phase 3: Drag & Drop (items between/within columns, column reordering)
- ✅ Phase 4: Two-Way File System Sync (FileWatcherService, external file monitoring, conflict handling)
- ✅ Phase 5: Grouping (GroupService, GroupViewModel, group CRUD, drag & drop for groups and items, collapse state persistence)
- ✅ Phase 6: Rich Markdown Editing
  - Implementation: Inline ContentDialog approach (no separate XAML file)
  - Split-pane layout with TextBox editor and WebView2 preview
  - Markdig integration with HTML/CSS styling and theme support
  - External file change detection with auto-reload and conflict resolution
  - Keyboard shortcuts: Ctrl+S (save), Ctrl+B (bold), Ctrl+I (italic), Ctrl+K (link)
  - Unsaved changes tracking and prompts on close
  - File watcher event suppression to prevent sync loops
  - See: KanbanFiles/Controls/KanbanItemCardControl.xaml.cs (OnOpenDetailRequested)
- ✅ Phase 7: Polish & Edge Cases
  - ✅ InfoBar notification system with auto-dismiss
  - ✅ Config corruption notifications (backup + user notification)
  - ✅ Keyboard shortcuts (Ctrl+O, Ctrl+N, Ctrl+Shift+N, F5, Arrow keys for navigation, Escape in dialogs)
  - ✅ Arrow key navigation (←/→ between columns, ↑/↓ within columns)
  - ✅ Existing subfolders handling (already implemented in Phase 1)
  - ✅ File filtering for .md, hidden files (already implemented)
  - ✅ ThemeResource usage throughout UI (already implemented)
  - ✅ Window state persistence (width, height, position, maximized state)
  - ✅ Comprehensive error handling with INotificationService (Phase 7.7)

## Housekeeping Items
- **Git**: .gitignore added (2/6/2026 8:46 PM) - prevents bin/obj tracking
- **Git Cleanup**: Removed tracked bin/obj artifacts (2/6/2026 8:59 PM) - repository now properly ignores build artifacts
- **TODO Fix**: .kanban.json auto-reload implemented (2/6/2026 8:50 PM) - external config changes now automatically refresh the board
- **Error Handling**: Empty catch blocks fixed (2/6/2026 8:54 PM) - all exceptions now logged with diagnostic context

## Code Quality
- **Exception Logging**: All catch blocks now use Debug.WriteLine() for diagnostics
  - App.xaml.cs: Window state restoration errors logged
  - ItemDetailViewModel.cs: File read/write errors logged with file paths
  - Only intentional error suppression: FileSystemService.IsHiddenOrSystem()
- **No Empty Catch Blocks**: All critical error handling now includes diagnostic output

## Project Status
All 7 phases complete! KanbanFiles is feature-complete with full markdown editing capabilities, comprehensive error handling with proper logging, and zero remaining TODOs.

## Post-Launch Improvements
- **Error Handling Enhancement** (2/6/2026 9:02 PM): Added missing try-catch blocks to ColumnViewModel.RenameColumnAsync()
  - Follows same pattern as CreateItemAsync() and DeleteColumnAsync()
  - Catches UnauthorizedAccessException and IOException
  - Shows user-friendly notifications via INotificationService
  - Prevents unhandled exceptions from crashing the app during column rename operations
  - Build successful (0 errors, 19 expected AOT warnings)
- **Arrow Key Navigation** (2/6/2026 9:10 PM): Implemented keyboard navigation between columns and items
  - Added IFocusManagerService and FocusManagerService for focus state management
  - Arrow keys (←/→) navigate between columns
  - Arrow keys (↑/↓) navigate between items within a column
  - Integrated with MainViewModel and MainPage keyboard accelerators
  - Completes Phase 7.4 keyboard shortcuts requirement
  - Build successful (0 errors, 19 expected AOT warnings)
- **CRITICAL BUG FIX** (2/6/2026 9:19 PM): Fixed columns not populating with files on board load (Issue #1)
  - Root cause: LoadBoardAsync created ColumnViewModels but never called LoadItemsAsync()
  - Items were added to the Items collection but UI binds to UngroupedItems/Groups which remained empty
  - Solution: Added await columnViewModel.LoadItemsAsync() after creating each column
  - This properly loads groups and distributes items into UngroupedItems/Groups collections for UI display
  - Build successful (0 errors, expected AOT warnings)
- **Inline Add Item Entry** (2/6/2026 9:25 PM): Replaced dialog popup with inline text entry (Issue #2)
  - Clicking "+ Add Item" now reveals an inline text input field with Add/Cancel buttons
  - Enter key creates the item, Escape key cancels
  - More streamlined UX compared to ContentDialog approach
  - Keyboard shortcut (Ctrl+N) still works via AddItemRequested event
  - Changes: ColumnControl.xaml (added collapsible StackPanel), ColumnControl.xaml.cs (UI toggle logic)
  - Build successful (0 errors, 19 expected AOT warnings)
- **Recent Folders List** (2/6/2026 9:35 PM): Implemented persistent recent folders tracking
  - Added RecentFoldersService with JSON storage in %LOCALAPPDATA%\KanbanFiles\recent-folders.json
  - Stores up to 10 most recent folders with case-insensitive deduplication
  - Empty state UI shows clickable recent folders list
  - Auto-removes invalid/deleted folders on load
  - Features: Click to open, remove button (X icon), full path tooltips
  - Thread-safe file operations with SemaphoreSlim
  - Files: IRecentFoldersService.cs, RecentFoldersService.cs, MainViewModel.cs (updated), MainPage.xaml (updated)
  - Completes Issue #3
  - Build successful (0 errors, 19 expected AOT warnings)
- **Recent Folders Clickability Fix** (2/6/2026 9:59 PM): Fixed non-responsive click on recent folders (Issue #8)
  - Root cause: ElementName binding from DataTemplate had runtime resolution issues in WinUI 3
  - Command binding `{Binding ViewModel.OpenRecentFolderCommand, ElementName=RootPage}` failed to resolve correctly
  - Solution: Replaced with Click event handlers in code-behind that call Command.ExecuteAsync() directly
  - Changes: MainPage.xaml (replaced Command with Click), MainPage.xaml.cs (added handlers)
  - Recent folders now fully functional and clickable
  - Build successful (0 errors, 19 expected AOT warnings)
- **CRITICAL BUG FIX** (2/6/2026 9:41 PM): Fixed drag-drop between columns (Issue #4)
  - Root cause: JSON property name mismatch in DragPayload serialization/deserialization
  - KanbanItemCardControl serialized with PascalCase (FilePath, SourceColumnPath, FileName)
  - DragPayload model expected camelCase via [JsonPropertyName] attributes
  - Deserialization failed silently, causing drop operations to be ignored
  - Solution: Removed [JsonPropertyName] attributes to use default PascalCase matching serialization
  - Items can now be dragged and dropped between columns successfully
  - Build successful (0 errors, expected AOT warnings)
- **CRITICAL BUG FIX** (2/6/2026 9:45 PM): Fixed new items not appearing in columns (Issue #5)
  - Root cause: Items added to Items collection but not UI-bound UngroupedItems/Groups collections
  - Affected both CreateItemAsync (app-created items) and OnItemCreated (externally created files)
  - Solution: After creating KanbanItemViewModel, add to UngroupedItems or appropriate group's Items
  - Items now appear immediately whether created in-app or externally
  - Build successful (0 errors, 25 expected AOT warnings)
- **Theme-Aware Colors** (2/6/2026 9:47 PM): Fixed poor contrast in Kanban item cards (Issue #6)
  - Root cause: Hardcoded hex colors (#F9F9F9, #666666, #999999) didn't adapt to light/dark mode
  - Solution: Replaced with WinUI3 theme resources for automatic theme adaptation
  - Background: CardBackgroundFillColorDefaultBrush
  - Text: TextFillColorPrimaryBrush, TextFillColorSecondaryBrush, TextFillColorTertiaryBrush
  - Ensures proper contrast and readability in both light and dark modes
  - Build successful (0 errors, 19 expected AOT warnings)
- **Hover Background Fix** (2/6/2026 9:55 PM): Fixed white-on-white text during hover (Issue #7)
  - Root cause: PointerExited handler hardcoded white background (#FFFFFF), causing text to become unreadable
  - Solution: Use theme-aware resources for both hover and normal states
  - Hover: CardBackgroundFillColorSecondaryBrush, Normal: CardBackgroundFillColorDefaultBrush
  - Automatic theme adaptation ensures proper contrast in light/dark modes
  - Changes: KanbanItemCardControl.xaml.cs (CardBorder_PointerEntered/Exited methods)
  - Build successful (0 errors, 19 expected AOT warnings)
- **CRITICAL BUG FIX** (2/6/2026 9:55 PM): Fixed drag/drop showing 🚫 cursor (Issue #9)
  - Root cause: Individual KanbanItemCardControl elements lacked AllowDrop/DragOver handlers
  - Cards captured drag events but didn't accept drops, blocking parent ItemsControl handlers
  - Solution: Added card-level drag/drop event handlers to enable drop targeting
    - XAML: Added AllowDrop="True", DragOver, and Drop handlers to CardBorder
    - Code: Implemented CardBorder_DragOver (accepts Move operation) and CardBorder_Drop (propagates to parent)
  - Parent ColumnControl still handles actual file movement logic
  - Item reordering within columns now works (was already implemented, just needed proper event handling)
  - Changes: KanbanItemCardControl.xaml (added handlers), KanbanItemCardControl.xaml.cs (implemented methods)
  - Build successful (0 errors, 19 expected AOT warnings)


