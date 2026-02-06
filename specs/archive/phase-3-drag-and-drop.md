# Phase 3 — Drag & Drop ✅

## Status: COMPLETE (2026-02-06)

## Objective
Enable drag-and-drop interactions so users can move items between columns, reorder items within a column, and reorder columns themselves. All visual changes must be reflected on the file system.

---

## 3.1 — Drag & Drop Items Between Columns ✅

### What
Allow users to drag a Kanban item card from one column and drop it into another column. The underlying `.md` file is moved between folders on disk.

### Implementation Approach

#### Drag Source (KanbanItemControl)
- Set `CanDrag="True"` on the card control
- Handle `DragStarting` event:
  - Set `DataPackage` with a custom format containing the source file path and source column path
  - Apply a drag visual (semi-transparent snapshot of the card)
  - Optionally reduce opacity of the source card during drag

```csharp
private void OnDragStarting(UIElement sender, DragStartingEventArgs args)
{
    args.Data.SetText(JsonSerializer.Serialize(new DragPayload
    {
        FilePath = ViewModel.FilePath,
        SourceColumnPath = ViewModel.ColumnFolderPath,
        FileName = ViewModel.FileName
    }));
    args.Data.RequestedOperation = DataPackageOperation.Move;
}
```

#### Drop Target (ColumnControl item list area)
- Set `AllowDrop="True"` on the column's item list area
- Handle `DragOver`:
  - Validate the drag data contains a Kanban item
  - Show insertion indicator (line between cards showing where item will land)
  - Set `AcceptedOperation = DataPackageOperation.Move`
- Handle `Drop`:
  - Extract payload from `DataPackage`
  - Determine drop index from pointer position relative to existing cards
  - Execute move operation

#### Move Operation (on Drop)
1. Parse the drag payload (source file path, source column)
2. If source column == target column → reorder only (see 3.2)
3. If source column != target column:
   - Call `FileSystemService.MoveItemAsync(sourceFilePath, targetColumnFolderPath)`
   - This physically moves the `.md` file on disk
   - Remove item from source `ColumnViewModel.Items`
   - Insert item at drop index in target `ColumnViewModel.Items`
   - Update `ItemOrder` in both columns' `ColumnConfig` in `.kanban.json`
   - Save `.kanban.json`

### Visual Feedback
- **Dragging**: Source card becomes semi-transparent (opacity 0.5)
- **Over valid target**: Column background subtly highlights; insertion line shows between cards
- **Over invalid target**: No highlight, cursor shows "not allowed"
- **Drop**: Card animates into position (WinUI3 implicit animations)

### Edge Cases
- Dropping on an empty column → item becomes the only item
- File move fails (permission denied, disk full) → show error, revert UI
- Dropping onto the same position → no-op
- File was deleted externally during drag → show error, refresh board

### Acceptance Criteria ✅
- ✅ Items can be dragged from one column and dropped into another
- ✅ The `.md` file is physically moved between folders on disk
- ✅ The item appears at the correct position in the target column
- ✅ `.kanban.json` is updated with new item orders
- ✅ Visual feedback is provided during the drag operation

### Implementation Notes
- DragPayload model created with JSON serialization
- KanbanItemCardControl implements drag source with opacity feedback
- ColumnControl implements drop target with position calculation
- ColumnViewModel.MoveItemToColumnAsync handles file moves and collection updates

---

## 3.2 — Reorder Items Within a Column ✅

### What
Allow users to drag items up and down within the same column to change their display order.

### Implementation

#### Detection
When a drop occurs in the same column as the drag source, treat it as a reorder rather than a move.

#### Reorder Operation
1. Determine the new index from the drop position
2. If new index == old index → no-op
3. Remove item from old index in `ColumnViewModel.Items`
4. Insert item at new index
5. Update `ItemOrder` in the column's `ColumnConfig`
6. Save `.kanban.json`

#### Insertion Indicator
- Show a horizontal line between cards indicating where the dragged item will be placed
- Calculate insertion index based on the vertical midpoint of each card:
  - If pointer is above the midpoint of a card → insert before that card
  - If pointer is below the midpoint → insert after that card

### Persistence
Item order is stored in `.kanban.json` under each column's `ItemOrder` array:
```json
{
  "FolderName": "To Do",
  "ItemOrder": ["task-b.md", "task-a.md", "task-c.md"]
}
```

Files not listed in `ItemOrder` (e.g., created externally) are appended at the end in alphabetical order.

### Acceptance Criteria ✅
- ✅ Items can be reordered within a column via drag-and-drop
- ✅ New order is persisted in `.kanban.json`
- ✅ Drop position calculated based on pointer location
- ✅ Externally created files appear at the end of the list

### Implementation Notes
- Same drag/drop infrastructure as cross-column moves
- ColumnControl.HandleReorderAsync uses ObservableCollection.Move
- Drop index calculation handles edge cases for same-list movement

---

## 3.3 — Column Reordering ✅

### What
Allow users to drag columns left and right to change their display order.

### Implementation

#### Drag Source (ColumnControl header)
- The column header area (not the entire column) is the drag handle
- Set `CanDrag="True"` on the column header
- `DragStarting`: Package column identifier (folder name) in `DataPackage`

#### Drop Target (Board-level horizontal area)
- The board's horizontal `ItemsControl`/`ItemsRepeater` handles column drops
- `DragOver`: Show vertical insertion indicator between columns
- `Drop`: Reorder column in the collection

#### Reorder Operation
1. Determine new column index from drop position
2. If same index → no-op
3. Move `ColumnViewModel` in `MainViewModel.Columns` collection
4. Update `SortOrder` for all columns in `Board.Columns`
5. Save `.kanban.json`

### Visual Feedback
- Column header shows drag handle cursor on hover
- During drag: semi-transparent column snapshot as drag visual
- Insertion indicator: vertical line between columns at drop position
- Other columns shift slightly to indicate valid drop zone

### Persistence
Column order is stored via the `SortOrder` field in `.kanban.json`:
```json
{
  "Columns": [
    { "FolderName": "In Progress", "SortOrder": 0 },
    { "FolderName": "To Do", "SortOrder": 1 },
    { "FolderName": "Done", "SortOrder": 2 }
  ]
}
```

### Acceptance Criteria ✅
- ✅ Columns can be reordered via drag-and-drop on the header
- ✅ New order is persisted in `.kanban.json` via SortOrder property
- ✅ Visual feedback (opacity) during drag operation
- ✅ Column content remains intact after reordering

### Implementation Notes
- Column header Grid made draggable with CanDrag="True"
- MainPage ItemsControl handles column drops
- MainViewModel.ReorderColumnAsync updates SortOrder for all columns
- Custom drag format "KanbanColumnReorder" prevents conflicts with item drags
- Horizontal position calculation based on 280px column width + spacing
