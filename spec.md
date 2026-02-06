The goal of this application is to emulate the usefulness of organizing via a Kanban board. With the local file system and static simple files of folders and text documents. Different lanes in the Kanban are represented on disk by different folders, and different items are represented by files in those folders.



* Framework WinUI3 / WinAppSDK
* 2 way sync between file changes
* Creating a file creates a new .md file
* Editing in the app will edit the .md file
* Editing outside of the app will sync the content in the app
* Files can be grouped, this makes a .json file in the directory to store details about the group
* drag and drop between lanes moves the file on disk
* Columns can be reodered
* details about the board are stored on a .json in the directory which was opened
* items can be opened to be a full page modal for more rich editing of the markdown file

---

## Technology & Decisions
- **Framework**: WinUI3 / WinAppSDK (.NET 10, C# + XAML)
- **Project**: Single-project solution, incremental delivery
- **Board config**: `.kanban.json` at the opened directory root
- **Group metadata**: `groups.json` per column directory; groups are collapsible, draggable sections within a column
- **Default behavior**: If opened folder has no subfolders, auto-create default columns ("To Do", "In Progress", "Done"). If subfolders exist but no config, generate config from existing folders.
- **File watching**: `FileSystemWatcher` for external change detection
- **Markdown**: Markdig for parsing; WebView2 for preview rendering

---

## Implementation Plan

### Phase 1 — Project Scaffolding & Core Models ✅
- [x] **1.1** Create WinUI3 packaged app project (`KanbanFiles`) targeting .NET 10 + WinAppSDK
- [x] **1.2** Define core models: `Board`, `ColumnConfig`, `Column`, `KanbanItem`, `Group`, `GroupsConfig`
- [x] **1.3** Implement `BoardConfigService` — read/write `.kanban.json`, auto-create defaults, handle corruption
- [x] **1.4** Implement `FileSystemService` — enumerate folders/files, CRUD operations, content preview generation

### Phase 2 — Basic Board UI
- [ ] **2.1** Create `MainWindow` with command bar + horizontal scrollable board
- [ ] **2.2** Create `ColumnControl` — header, vertical item list, add item footer
- [ ] **2.3** Create `KanbanItemControl` — card with title, preview, hover state, context menu
- [ ] **2.4** Implement "Open Folder" command via `FolderPicker`
- [ ] **2.5** Implement "New Item" — create `.md` file, add card
- [ ] **2.6** Implement "New Column" — create subfolder, update config

### Phase 3 — Drag & Drop
- [ ] **3.1** Drag-and-drop items between columns (moves `.md` file on disk)
- [ ] **3.2** Reorder items within a column (persists to `.kanban.json`)
- [ ] **3.3** Reorder columns via drag-and-drop on header (persists to `.kanban.json`)

### Phase 4 — 2-Way File System Sync
- [ ] **4.1** Implement `FileWatcherService` — root watcher + per-column watchers, self-change suppression
- [ ] **4.2** Sync external file creates → new card appears
- [ ] **4.3** Sync external file deletes → card removed
- [ ] **4.4** Sync external file renames → card title updated
- [ ] **4.5** Sync external content changes → preview and open editor updated
- [ ] **4.6** Debounce rapid events, marshal to UI thread via `DispatcherQueue`
- [ ] **4.7** Conflict handling — auto-reload or prompt when file edited in app and externally

### Phase 5 — Grouping
- [ ] **5.1** Define `groups.json` schema (name, item list, collapsed state per group)
- [ ] **5.2** Implement `GroupService` — CRUD for groups, item assignments
- [ ] **5.3** Update `ColumnControl` with collapsible group sections (ungrouped items at top)
- [ ] **5.4** Drag-and-drop groups to reorder within column
- [ ] **5.5** Drag-and-drop items into/out of groups
- [ ] **5.6** UI for create/rename/delete groups

### Phase 6 — Rich Markdown Editing (Full-Page Modal)
- [ ] **6.1** Create `ItemDetailDialog` — full-window overlay with title bar, split pane, status bar
- [ ] **6.2** Markdown editor — monospace `TextBox`, keyboard shortcuts (bold, italic, link, save)
- [ ] **6.3** Markdig + WebView2 preview — live render with theme-aware CSS
- [ ] **6.4** Save to disk — explicit save, suppress watcher, update card preview
- [ ] **6.5** External change detection while modal open — auto-reload or prompt

### Phase 7 — Polish & Edge Cases
- [ ] **7.1** Handle opening folders with existing subfolders
- [ ] **7.2** Handle non-`.md` files gracefully (ignore)
- [ ] **7.3** Handle config file corruption (backup + regenerate + notify)
- [ ] **7.4** Keyboard shortcuts (global nav, board nav, modal editing)
- [ ] **7.5** Theming — light/dark via WinUI3 system theme + WebView2 sync
- [ ] **7.6** Persist window size/position + last opened folder
- [ ] **7.7** Error handling & `InfoBar` notifications with severity levels

---

## Notes
- All `.json` files should be human-readable with indented formatting
- File operations should be async to keep UI responsive
- `FileSystemWatcher` events should be debounced (~300ms) and dispatched to UI thread via `DispatcherQueue`
- Items not in any group appear as "ungrouped" at the top of the column
- Column folders deleted externally should be handled gracefully (remove from board, prompt user)
- Detailed specifications for each phase are in the `specs/` folder

