# KanbanFiles — AI Agent Instructions

## Build & Run

```bash
cd KanbanFiles
dotnet build          # Build (multi-platform: x86, x64, ARM64)
dotnet run            # Run the WinUI 3 desktop app
```

- Solution: `KanbanFiles/KanbanFiles.slnx` (single-project, .NET 10 + Windows App SDK 2.0)
- No test project exists — validate changes by building successfully (expect ~19 AOT warnings, 0 errors)

## Architecture

- **WinUI 3 desktop app** using MVVM with [CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/) source generators
- **No DI container** — services are manually instantiated in `MainViewModel` constructor
- **Event-driven dialogs** — ViewModels raise events (e.g., `OpenFolderRequested`), code-behind subscribes and shows `ContentDialog`
- **`WeakReferenceMessenger`** for cross-component messaging (see [Messages/](KanbanFiles/Messages/))
- Static `App.MainWindow` and `App.MainDispatcher` for global window/dispatcher access

### Folder layout

| Folder        | Contents                                                                    |
| ------------- | --------------------------------------------------------------------------- |
| `Models/`     | Plain data classes — no `ObservableObject` base (except `ChatMessage`)      |
| `Services/`   | Business logic + interfaces side-by-side (`I{Name}` → `{Name}`)             |
| `ViewModels/` | `partial class` ViewModels extending `BaseViewModel` (→ `ObservableObject`) |
| `Views/`      | XAML pages wired to ViewModels                                              |
| `Controls/`   | Reusable `UserControl` subclasses                                           |
| `Converters/` | `IValueConverter` implementations                                           |

## Code Style

- **C# latest**, nullable enabled, implicit usings enabled — see [KanbanFiles/.editorconfig](KanbanFiles/.editorconfig)
- Prefer explicit types over `var` (`.editorconfig: csharp_style_var_*=false:suggestion`)
- File-scoped namespaces: `namespace KanbanFiles.Models;`
- Global usings in [Imports.cs](KanbanFiles/Imports.cs) — only add `using` for non-global types (e.g., `System.Text.Json`)
- `[ObservableProperty]` on `_camelCase` private fields → generates PascalCase properties
- `[RelayCommand]` on methods → generates `IRelayCommand` properties
- Async methods suffixed with `Async`; XAML event handlers named `ElementName_EventName`
- `async void` only for UI event handlers — always wrap body in try-catch

## Serialization

- **System.Text.Json** only (no Newtonsoft)
- Shared static `JsonSerializerOptions` with `CamelCase` policy and `WriteIndented = true`
- Config file: `.kanban.json` — camelCase on disk, no `[JsonPropertyName]` attributes on models
- `DragPayload` uses PascalCase default serialization (intentional — see CLAUDE.md drag-drop fix)

## Key Conventions

- **Error handling**: `try-catch` + `Debug.WriteLine()` for diagnostics; `INotificationService.ShowNotification()` for user-facing InfoBar messages. No logging framework.
- **File I/O**: `FileSystemService` methods throw `IOException` with context. Callers catch and notify via `INotificationService`.
- **Thread safety**: `ConcurrentDictionary`, `SemaphoreSlim`, `lock` where needed. File watcher events marshaled to UI thread via `DispatcherQueue`.
- **Collections**: `ObservableCollection<T>` for UI-bound data; `List<T>` for model/config data. Both `= new()` and `= []` initializers are used.
- **Controls**: Wire events in `Loaded`, unwire in `Unloaded`. Cast `DataContext` to typed ViewModel. Track subscription state with `_eventsSubscribed` bool.

## Existing Documentation

`CLAUDE.md` at repo root contains detailed implementation history, bug fix notes, and phase completion status. Consult it for context on past decisions and known patterns.
