using System.Collections.Concurrent;
using Microsoft.UI.Dispatching;

namespace KanbanFiles.Services;

public class FileWatcherService : IDisposable
{
    private readonly string _rootPath;
    private readonly DispatcherQueue _dispatcherQueue;
    private FileSystemWatcher? _folderWatcher;
    private FileSystemWatcher? _fileWatcher;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _debouncers = new();
    private readonly HashSet<string> _suppressedPaths = new();
    private readonly object _suppressLock = new();
    
    private const int DebounceDelayMs = 300;
    private const int FileAccessRetries = 3;
    private const int FileAccessRetryDelayMs = 500;

    public event EventHandler<ItemChangedEventArgs>? ItemCreated;
    public event EventHandler<ItemChangedEventArgs>? ItemDeleted;
    public event EventHandler<ItemRenamedEventArgs>? ItemRenamed;
    public event EventHandler<ItemChangedEventArgs>? ItemContentChanged;
    public event EventHandler<ColumnChangedEventArgs>? ColumnCreated;
    public event EventHandler<ColumnChangedEventArgs>? ColumnDeleted;

    public FileWatcherService(string rootPath, DispatcherQueue dispatcherQueue)
    {
        _rootPath = rootPath;
        _dispatcherQueue = dispatcherQueue;
    }

    public void Start()
    {
        // Watch for folder changes (columns)
        _folderWatcher = new FileSystemWatcher(_rootPath)
        {
            NotifyFilter = NotifyFilters.DirectoryName,
            IncludeSubdirectories = false
        };
        _folderWatcher.Created += OnFolderCreated;
        _folderWatcher.Deleted += OnFolderDeleted;
        _folderWatcher.EnableRaisingEvents = true;

        // Watch for file changes (items and config files) in all subdirectories
        _fileWatcher = new FileSystemWatcher(_rootPath)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
            IncludeSubdirectories = true
        };
        _fileWatcher.Created += OnFileCreated;
        _fileWatcher.Deleted += OnFileDeleted;
        _fileWatcher.Renamed += OnFileRenamed;
        _fileWatcher.Changed += OnFileChanged;
        _fileWatcher.EnableRaisingEvents = true;
    }

    public void Stop()
    {
        if (_folderWatcher != null)
        {
            _folderWatcher.EnableRaisingEvents = false;
            _folderWatcher.Dispose();
            _folderWatcher = null;
        }

        if (_fileWatcher != null)
        {
            _fileWatcher.EnableRaisingEvents = false;
            _fileWatcher.Dispose();
            _fileWatcher = null;
        }

        // Cancel all pending debounces
        foreach (CancellationTokenSource cts in _debouncers.Values)
        {
            cts.Cancel();
            cts.Dispose();
        }
        _debouncers.Clear();
    }

    public void SuppressNextEvent(string path)
    {
        lock (_suppressLock)
        {
            _suppressedPaths.Add(Path.GetFullPath(path));
        }
    }

    private bool IsEventSuppressed(string path)
    {
        lock (_suppressLock)
        {
            var fullPath = Path.GetFullPath(path);
            if (_suppressedPaths.Remove(fullPath))
            {
                return true;
            }
        }
        return false;
    }

    private void OnFolderCreated(object sender, FileSystemEventArgs e)
    {
        if (IsEventSuppressed(e.FullPath))
            return;

        _dispatcherQueue.TryEnqueue(() =>
        {
            ColumnCreated?.Invoke(this, new ColumnChangedEventArgs(e.FullPath));
        });
    }

    private void OnFolderDeleted(object sender, FileSystemEventArgs e)
    {
        if (IsEventSuppressed(e.FullPath))
            return;

        _dispatcherQueue.TryEnqueue(() =>
        {
            ColumnDeleted?.Invoke(this, new ColumnChangedEventArgs(e.FullPath));
        });
    }

    private void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        if (IsEventSuppressed(e.FullPath))
            return;

        // Only handle .md files for item events
        if (Path.GetExtension(e.FullPath) != ".md")
            return;

        DebounceEvent(e.FullPath, async () =>
        {
            await WaitForFileAvailableAsync(e.FullPath);
            _dispatcherQueue.TryEnqueue(() =>
            {
                ItemCreated?.Invoke(this, new ItemChangedEventArgs(e.FullPath));
            });
        });
    }

    private void OnFileDeleted(object sender, FileSystemEventArgs e)
    {
        if (IsEventSuppressed(e.FullPath))
            return;

        // Only handle .md files for item events
        if (Path.GetExtension(e.FullPath) != ".md")
            return;

        _dispatcherQueue.TryEnqueue(() =>
        {
            ItemDeleted?.Invoke(this, new ItemChangedEventArgs(e.FullPath));
        });
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        if (IsEventSuppressed(e.OldFullPath) || IsEventSuppressed(e.FullPath))
            return;

        // Only handle .md files for item events
        var oldExt = Path.GetExtension(e.OldFullPath);
        var newExt = Path.GetExtension(e.FullPath);
        if (oldExt != ".md" && newExt != ".md")
            return;

        _dispatcherQueue.TryEnqueue(() =>
        {
            ItemRenamed?.Invoke(this, new ItemRenamedEventArgs(e.OldFullPath, e.FullPath));
        });
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        if (IsEventSuppressed(e.FullPath))
            return;

        DebounceEvent(e.FullPath, async () =>
        {
            await WaitForFileAvailableAsync(e.FullPath);
            _dispatcherQueue.TryEnqueue(() =>
            {
                ItemContentChanged?.Invoke(this, new ItemChangedEventArgs(e.FullPath));
            });
        });
    }

    private void DebounceEvent(string path, Func<Task> action)
    {
        // Cancel any existing debounce for this file
        if (_debouncers.TryGetValue(path, out CancellationTokenSource? existingCts))
        {
            existingCts.Cancel();
            existingCts.Dispose();
        }

        var cts = new CancellationTokenSource();
        _debouncers[path] = cts;

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(DebounceDelayMs, cts.Token);
                await action();
            }
            catch (OperationCanceledException)
            {
                // Expected when debounce is cancelled
            }
            finally
            {
                _debouncers.TryRemove(path, out _);
                cts.Dispose();
            }
        });
    }

    private async Task WaitForFileAvailableAsync(string filePath)
    {
        for (int i = 0; i < FileAccessRetries; i++)
        {
            try
            {
                using FileStream stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                return;
            }
            catch (IOException)
            {
                if (i < FileAccessRetries - 1)
                {
                    await Task.Delay(FileAccessRetryDelayMs);
                }
            }
        }
    }

    public void Dispose()
    {
        Stop();
    }
}

public class ItemChangedEventArgs : EventArgs
{
    public string FilePath { get; }

    public ItemChangedEventArgs(string filePath)
    {
        FilePath = filePath;
    }
}

public class ItemRenamedEventArgs : EventArgs
{
    public string OldFilePath { get; }
    public string NewFilePath { get; }

    public ItemRenamedEventArgs(string oldFilePath, string newFilePath)
    {
        OldFilePath = oldFilePath;
        NewFilePath = newFilePath;
    }
}

public class ColumnChangedEventArgs : EventArgs
{
    public string FolderPath { get; }

    public ColumnChangedEventArgs(string folderPath)
    {
        FolderPath = folderPath;
    }
}
