## Issue 1 3:15 PM 2/6/2026 - RESOLVED 9:19 PM 2/6/2026

- The columns are not populated with files that are in the folders. this is a core function of the application

**Resolution**: Fixed by adding `await columnViewModel.LoadItemsAsync()` call in `MainViewModel.LoadBoardAsync()`. Items were being loaded into the `Items` collection but the UI binds to `UngroupedItems`/`Groups` which were never populated. The LoadItemsAsync() method properly loads groups and distributes items into the correct collections for display.



## Issue 2 3:16 PM 2/6/2026
- The "add item" button does nothing. it should be an inline entry for a new item
- When the new item gets added a corresponding markdown file should get added in the corresponding folder


## Issue 3 3:18 PM 2/6/2026
- The initial launch screen should display a list of recently opened folders for quicker access

