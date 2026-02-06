## Issue 1 3:15 PM 2/6/2026 - RESOLVED 9:19 PM 2/6/2026

- The columns are not populated with files that are in the folders. this is a core function of the application

**Resolution**: Fixed by adding `await columnViewModel.LoadItemsAsync()` call in `MainViewModel.LoadBoardAsync()`. Items were being loaded into the `Items` collection but the UI binds to `UngroupedItems`/`Groups` which were never populated. The LoadItemsAsync() method properly loads groups and distributes items into the correct collections for display.



## Issue 2 3:16 PM 2/6/2026 - RESOLVED 2/6/2026 9:25 PM
- The "add item" button does nothing. it should be an inline entry for a new item
- When the new item gets added a corresponding markdown file should get added in the corresponding folder

**Resolution**: Implemented inline text entry for adding items. When clicking "+ Add Item", the button is replaced with a text input field, "Add" and "Cancel" buttons. Users can type the item title and press Enter to create or Escape to cancel. The UI provides a more streamlined UX compared to the previous dialog approach. The corresponding .md file is created in the folder as expected via `FileSystemService.CreateItemAsync()`.

Changes:
- **ColumnControl.xaml**: Added collapsible StackPanel with TextBox and action buttons
- **ColumnControl.xaml.cs**: Implemented inline UI toggle, Enter/Escape key handlers, and form validation
- **ColumnViewModel.cs**: Retained AddItem() command and AddItemRequested event for keyboard shortcut support (Ctrl+N)



## Issue 3 3:18 PM 2/6/2026 - RESOLVED
- The initial launch screen should display a list of recently opened folders for quicker access

**Resolution**: Implemented recent folders list feature with RecentFoldersService. The empty state (when no board is loaded) now displays up to 10 recently opened folders. Features include:
- JSON persistence in %LOCALAPPDATA%\KanbanFiles\recent-folders.json
- Click folder to open, or click X button to remove from list
- Auto-removes invalid/deleted folders on app startup
- Thread-safe operations with case-insensitive path deduplication
- Professional card-style UI with tooltips showing full paths

Changes:
- **IRecentFoldersService.cs, RecentFoldersService.cs**: New service for recent folders management
- **MainViewModel.cs**: Added RecentFolders collection, LoadRecentFoldersAsync(), OpenRecentFolderCommand, RemoveRecentFolderCommand
- **MainPage.xaml**: Added recent folders list UI to empty state with card styling and hover effects


## Issue 4 3:28 PM 2/6/2026 - RESOLVED 2/6/2026 9:41 PM
- drop on other columns does not work, this is core functionality

**Resolution**: Fixed JSON property name mismatch in DragPayload model. The issue was caused by a discrepancy between serialization and deserialization:
- KanbanItemCardControl serialized with PascalCase property names (FilePath, SourceColumnPath, FileName)
- DragPayload model had `[JsonPropertyName]` attributes expecting camelCase (filePath, sourceColumnPath, fileName)
- This caused deserialization to fail silently, resulting in null dragPayload and ignored drop operations

**Fix**: Removed `[JsonPropertyName]` attributes from DragPayload.cs to use default PascalCase property names matching the serialized JSON. Items can now be dragged and dropped between columns successfully.


## Issue 5 3:41 PM 2/6/2026
- New items do not get added to the column
- new files do not get discovered and added to the column


## Issue 6 3:42 PM 2/6/2026
- The default background color on an item is too close to the text color and the item is not readable

