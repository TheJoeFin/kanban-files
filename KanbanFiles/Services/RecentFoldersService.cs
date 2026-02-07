using System.Diagnostics;
using System.Text.Json;

namespace KanbanFiles.Services;

public class RecentFoldersService : IRecentFoldersService
{
    private const int MaxRecentFolders = 10;
    private readonly string _recentFoldersPath;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    public RecentFoldersService()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appDataFolder = Path.Combine(localAppData, "KanbanFiles");
        Directory.CreateDirectory(appDataFolder);
        _recentFoldersPath = Path.Combine(appDataFolder, "recent-folders.json");
    }

    public async Task<List<string>> GetRecentFoldersAsync()
    {
        await _fileLock.WaitAsync();
        try
        {
            if (!File.Exists(_recentFoldersPath))
            {
                return new List<string>();
            }

            var json = await File.ReadAllTextAsync(_recentFoldersPath);
            List<string> folders = JsonSerializer.Deserialize<List<string>>(json, _jsonOptions) ?? new List<string>();

            // Validate folders exist and remove invalid ones
            var validFolders = folders.Where(Directory.Exists).ToList();

            // If we removed any invalid folders, save the cleaned list
            if (validFolders.Count != folders.Count)
            {
                await SaveFoldersAsync(validFolders);
            }

            return validFolders;
        }
        catch (IOException ex)
        {
            Debug.WriteLine($"RecentFoldersService: IOException reading recent folders: {ex.Message}");
            return new List<string>();
        }
        catch (UnauthorizedAccessException ex)
        {
            Debug.WriteLine($"RecentFoldersService: UnauthorizedAccessException reading recent folders: {ex.Message}");
            return new List<string>();
        }
        catch (JsonException ex)
        {
            Debug.WriteLine($"RecentFoldersService: JsonException deserializing recent folders: {ex.Message}");
            return new List<string>();
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task AddRecentFolderAsync(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return;
        }

        await _fileLock.WaitAsync();
        try
        {
            List<string> folders = await GetRecentFoldersInternalAsync();

            // Normalize path for comparison (case-insensitive on Windows)
            var normalizedPath = Path.GetFullPath(folderPath);

            // Remove existing entry (case-insensitive)
            folders.RemoveAll(f => string.Equals(
                Path.GetFullPath(f), 
                normalizedPath, 
                StringComparison.OrdinalIgnoreCase));

            // Add to front
            folders.Insert(0, normalizedPath);

            // Trim to max size
            if (folders.Count > MaxRecentFolders)
            {
                folders = folders.Take(MaxRecentFolders).ToList();
            }

            await SaveFoldersAsync(folders);
        }
        catch (IOException ex)
        {
            Debug.WriteLine($"RecentFoldersService: IOException adding recent folder '{folderPath}': {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            Debug.WriteLine($"RecentFoldersService: UnauthorizedAccessException adding recent folder '{folderPath}': {ex.Message}");
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task RemoveRecentFolderAsync(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return;
        }

        await _fileLock.WaitAsync();
        try
        {
            List<string> folders = await GetRecentFoldersInternalAsync();

            // Normalize path for comparison (case-insensitive on Windows)
            var normalizedPath = Path.GetFullPath(folderPath);

            // Remove matching entry (case-insensitive)
            var initialCount = folders.Count;
            folders.RemoveAll(f => string.Equals(
                Path.GetFullPath(f), 
                normalizedPath, 
                StringComparison.OrdinalIgnoreCase));

            // Only save if we actually removed something
            if (folders.Count != initialCount)
            {
                await SaveFoldersAsync(folders);
            }
        }
        catch (IOException ex)
        {
            Debug.WriteLine($"RecentFoldersService: IOException removing recent folder '{folderPath}': {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            Debug.WriteLine($"RecentFoldersService: UnauthorizedAccessException removing recent folder '{folderPath}': {ex.Message}");
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private async Task<List<string>> GetRecentFoldersInternalAsync()
    {
        try
        {
            if (!File.Exists(_recentFoldersPath))
            {
                return new List<string>();
            }

            var json = await File.ReadAllTextAsync(_recentFoldersPath);
            return JsonSerializer.Deserialize<List<string>>(json, _jsonOptions) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private async Task SaveFoldersAsync(List<string> folders)
    {
        var json = JsonSerializer.Serialize(folders, _jsonOptions);
        await File.WriteAllTextAsync(_recentFoldersPath, json);
    }
}
