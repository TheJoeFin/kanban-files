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



## Issue 3 3:18 PM 2/6/2026
- The initial launch screen should display a list of recently opened folders for quicker access


## Issue 4 3:28 PM 2/6/2026
- drop on other columns does not work, this is core functionality