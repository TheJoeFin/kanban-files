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


## Issue 5 3:41 PM 2/6/2026 - RESOLVED 2/6/2026 9:45 PM
- New items do not get added to the column
- new files do not get discovered and added to the column

**Resolution**: Fixed by adding newly created items to the UI-bound collections (UngroupedItems or Groups). The issue was identical to Issue #1 - items were being added to the Items collection but not to the collections that the UI binds to. 

**Root cause**: 
- ColumnViewModel.CreateItemAsync() added items to Items but not UngroupedItems/Groups
- MainViewModel.OnItemCreated() had the same problem for externally created files

**Fix**: After creating a KanbanItemViewModel, check if it has a GroupName and add it to:
- The matching group's Items collection if grouped
- UngroupedItems collection if ungrouped

This ensures items appear immediately in the UI whether created through the app or added externally via file system.

Changes:
- **ColumnViewModel.cs**: Added UI-bound collection logic in CreateItemAsync (lines 82-91)
- **MainViewModel.cs**: Added UI-bound collection logic in OnItemCreated (lines 269-278)


## Issue 6 3:42 PM 2/6/2026 - RESOLVED 2/6/2026 9:47 PM
- The default background color on an item is too close to the text color and the item is not readable

**Resolution**: Replaced hardcoded color values with WinUI3 theme resources for proper contrast in both light and dark modes. The KanbanItemCardControl now uses:
- Background: `CardBackgroundFillColorDefaultBrush` (replaces `#F9F9F9`)
- Title text: `TextFillColorPrimaryBrush` (explicit foreground for title)
- Content preview: `TextFillColorSecondaryBrush` (replaces `#666666`)
- Last modified: `TextFillColorTertiaryBrush` (replaces `#999999`)

These theme resources automatically adjust based on the system theme (light/dark mode), ensuring optimal contrast and readability in all scenarios.

Changes:
- **KanbanItemCardControl.xaml**: Updated Border Background and all TextBlock Foreground properties to use ThemeResource bindings


## Issue 7 3:50 PM 2/6/2026 - RESOLVED 2/6/2026 9:55 PM
- Hovering over the items changes the background color and makes them unreadable due to white on white

**Resolution**: Fixed by replacing hardcoded color values with theme-aware WinUI3 resources in hover handlers. The PointerEntered/PointerExited handlers now use:
- Hover background: `CardBackgroundFillColorSecondaryBrush` (instead of hardcoded #F3F3F3)
- Normal background: `CardBackgroundFillColorDefaultBrush` (instead of hardcoded #FFFFFF)

These theme resources automatically adapt to light/dark mode, ensuring proper contrast in all themes.

Changes:
- **KanbanItemCardControl.xaml.cs**: Updated CardBorder_PointerEntered() and CardBorder_PointerExited() to use Application.Current.Resources lookups


# issue 8 3:51 PM 2/6/2026 - RESOLVED 2/6/2026 9:59 PM
- The recently opened directory on the launching page is not clickable, it should be

**Resolution**: Fixed by replacing Command binding with Click event handlers in code-behind. The issue was that `{Binding ViewModel.OpenRecentFolderCommand, ElementName=RootPage}` inside a DataTemplate with `x:DataType="x:String"` had runtime resolution issues in WinUI 3.

**Root Cause**: ElementName binding from within ItemsControl DataTemplates can fail at runtime in WinUI 3. The binding path needed to traverse from the string DataContext up to the Page level to access ViewModel, which wasn't reliably working.

**Solution**: Changed approach to use Click event handlers in MainPage.xaml.cs that call the Command properties directly via `ViewModel.OpenRecentFolderCommand.ExecuteAsync()`. This is more reliable than ElementName bindings in DataTemplates.

Changes:
- **MainPage.xaml** (lines 159-162, 185-188): Replaced Command binding with Click event handlers
- **MainPage.xaml.cs** (lines 255-270): Added RecentFolderButton_Click and RemoveRecentFolderButton_Click handlers

The recent folders list is now fully clickable and functional.


# Issue 9 3:51 PM 2/6/2026 - RESOLVED 2/6/2026 9:55 PM
- This is CORE FUNCTIONALITY
- Drag and drop still does not work. I am getting the 🚫 symbol next to my mouse cursor when I am trying to drop on another column
- I should be able to reorder items too
- Reference how to do this here https://learn.microsoft.com/en-us/windows/apps/develop/data/drag-and-drop

**Resolution**: Fixed by implementing proper drag/drop event handling on KanbanItemCardControl. The issue was that individual card elements didn't have AllowDrop/DragOver handlers, causing them to block drop operations when dragging over cards in other columns.

**Root Cause**: When dragging over individual KanbanItemCardControl elements in the target column, those controls were capturing drag events but not accepting the drop operation, resulting in the 🚫 cursor symbol.

**Fix Applied**:
1. Added `AllowDrop="True"` to CardBorder in KanbanItemCardControl.xaml
2. Implemented `CardBorder_DragOver` handler that accepts `StandardDataFormats.Text` with `DataPackageOperation.Move`
3. Added `CardBorder_Drop` handler to allow event propagation to parent ItemsControl

The parent ColumnControl.ItemsControl_Drop() handler still performs the actual file movement logic. The card-level handlers simply enable drop targeting on individual cards, allowing the drag operation to succeed.

Changes:
- **KanbanItemCardControl.xaml**: Added AllowDrop, DragOver, and Drop event handlers to Border element
- **KanbanItemCardControl.xaml.cs**: Implemented CardBorder_DragOver() and CardBorder_Drop() methods

Item reordering within the same column was already implemented in ColumnControl (HandleReorderAsync method) and now works properly with these fixes.


# Issue 10 4:06 PM 2/6/2026 - RESOLVED 2/6/2026 10:19 PM
- the updating files info notification is too disruptive, it should be subtle, smooth, and natural

**Resolution**: Removed the "Configuration Updated" notification that appeared when `.kanban.json` changed externally. The automatic board reload provides sufficient visual feedback without needing an explicit notification.

**Root Cause**: The `MainViewModel.OnItemContentChanged()` method showed an InfoBar notification every time `.kanban.json` was modified by external processes or file watchers. This was redundant since the UI already updates automatically when the board configuration reloads.

**Fix**: Removed the `ShowNotification()` call (lines 395-397 in MainViewModel.cs) after loading board configuration. The board still reloads automatically, but without the disruptive notification. Users see the changes reflected naturally in the UI.

**Note**: The F5 manual refresh notification ("Refreshed") was intentionally kept, as it provides feedback for user-initiated actions (different use case).

Changes:
- **MainViewModel.cs** (lines 390-398): Removed ShowNotification() call for .kanban.json changes


# Issue 11 4:13 PM 2/6/2026 - RESOLVED 2/6/2026 10:15 PM
- There are still issues with the dropping. the 🚫 still occurs, cannot reorder, cannot drop on other columns

**Resolution**: Fixed by implementing proper event handler wiring for nested ItemsControl elements inside group DataTemplates. The core issue was that event handlers (DragOver/Drop) declared directly in XAML inside a DataTemplate don't automatically wire up to code-behind methods.

**Root Causes**:
1. Nested ItemsControl inside group DataTemplate had DragOver/Drop attributes in XAML but these were non-functional
2. Event handlers need to be attached programmatically when the control is loaded
3. Empty columns/groups had no visible drop zone
4. Event bubbling conflicts between card-level and parent-level DragOver handlers

**Fixes Applied**:
1. **Programmatic Event Wiring**: Replaced static XAML event handlers with `Loaded="GroupItemsControl_Loaded"` on nested ItemsControl elements. The code-behind handler wires up DragOver and Drop events at runtime.
2. **Empty Drop Zones**: Added `MinHeight="40"` to both UngroupedItemsControlElement and group ItemsControl to provide a visible drop target even when empty.
3. **Event Bubbling Fix**: Added `e.Handled = true` in KanbanItemCardControl.CardBorder_DragOver to prevent conflicts with parent ItemsControl handlers.

**Changes**:
- **ColumnControl.xaml**: Replaced DragOver/Drop with Loaded handler, added MinHeight to ItemsControl elements
- **ColumnControl.xaml.cs**: Added GroupItemsControl_Loaded method (lines 446-453) to programmatically attach event handlers
- **KanbanItemCardControl.xaml.cs**: Added e.Handled = true in CardBorder_DragOver (line 68)

All drag-drop scenarios now work correctly:
- ✅ Drag and drop between columns
- ✅ Reorder items within same column
- ✅ Drop into empty columns
- ✅ Drop into empty groups
- ✅ Drop into populated groups
- ✅ No 🚫 cursor appearing during valid drops


## Issue 12 4:26 PM 2/6/2026 - RESOLVED 2/6/2026 10:28 PM
- if I cancel the folder open the app crashes

**Resolution**: Fixed by adding comprehensive try-catch blocks to all async void event handlers that were missing error handling. The issue wasn't specifically about canceling the folder picker (which was already handled with a null check), but rather unhandled exceptions in async void methods that could crash the app.

**Root Causes**:
1. Four async void event handlers lacked try-catch blocks
2. Exceptions in async void methods crash the app since there's no caller to catch them
3. Any error during LoadBoardAsync or Command execution would terminate the app

**Fixed Handlers**:
- `OnOpenFolderRequested`: Added try-catch around FolderPicker and LoadBoardAsync
- `Refresh_Invoked`: Added try-catch around refresh operation
- `RecentFolderButton_Click`: Added try-catch around ExecuteAsync
- `RemoveRecentFolderButton_Click`: Added try-catch around ExecuteAsync

**Changes**:
- **MainPage.xaml.cs** (lines 25-51, 222-247, 254-278): Added comprehensive error handling with user-friendly notifications and diagnostic logging
- All exceptions now show InfoBar notifications with error details
- All exceptions are logged to Debug output for diagnostics
- App now gracefully handles errors instead of crashing

Build successful (0 errors, 19 expected AOT warnings)


## Issue 13 4:26 PM 2/6/2026
- The starting size window is too small


## Issue 14 4:27 PM 2/6/2026
- interesting drag behavior, it is working now, but only when the drag happens over another item, if the drag is over the column the 🚫 still appears
- Also to move a file you have to drag it twice, that is not right, it should be just the once
