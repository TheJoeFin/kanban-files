using System.Text.Json;
using System.Text.Json.Serialization;
using KanbanFiles.Models;

namespace KanbanFiles.Services;

public class BoardConfigService
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private const string ConfigFileName = ".kanban.json";

    public FileWatcherService? FileWatcher { get; set; }

    public bool WasConfigCorrupted { get; private set; }
    
    public void ResetCorruptionFlag() => WasConfigCorrupted = false;

    public async Task<Board> LoadOrInitializeAsync(string rootPath)
    {
        var configPath = Path.Combine(rootPath, ConfigFileName);

        if (File.Exists(configPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(configPath);
                Board? board = JsonSerializer.Deserialize<Board>(json, _jsonOptions);
                if (board != null)
                {
                    board.RootPath = rootPath;
                    return board;
                }
            }
            catch (JsonException ex)
            {
                // Corrupt config - backup and regenerate
                await BackupCorruptConfigAsync(configPath);
                WasConfigCorrupted = true;
                Console.WriteLine($"Warning: Corrupt .kanban.json detected. Backed up and regenerating. Error: {ex.Message}");
            }
        }

        // No config or corrupt - initialize new
        return await InitializeNewBoardAsync(rootPath);
    }

    public async Task SaveAsync(Board board)
    {
        var configPath = Path.Combine(board.RootPath, ConfigFileName);
        FileWatcher?.SuppressNextEvent(configPath);
        var json = JsonSerializer.Serialize(board, _jsonOptions);
        await File.WriteAllTextAsync(configPath, json);
    }

    private async Task<Board> InitializeNewBoardAsync(string rootPath)
    {
        var board = new Board
        {
            Name = Path.GetFileName(rootPath) ?? "Kanban Board",
            RootPath = rootPath
        };

        // Check if subfolders already exist
        var existingFolders = Directory.GetDirectories(rootPath)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrEmpty(name))
            .OrderBy(name => name)
            .ToList();

        if (existingFolders.Count > 0)
        {
            // Generate config from existing folders
            board.Columns = existingFolders.Select((folderName, index) => new ColumnConfig
            {
                FolderName = folderName!,
                DisplayName = folderName!,
                SortOrder = index,
                ItemOrder = new List<string>()
            }).ToList();
        }
        else
        {
            // Create default columns
            (string, int)[] defaultColumns = new[]
            {
                ("To Do", 0),
                ("In Progress", 1),
                ("Done", 2)
            };

            foreach ((string? name, int order) in defaultColumns)
            {
                var folderPath = Path.Combine(rootPath, name);
                Directory.CreateDirectory(folderPath);

                board.Columns.Add(new ColumnConfig
                {
                    FolderName = name,
                    DisplayName = name,
                    SortOrder = order,
                    ItemOrder = new List<string>()
                });
            }
        }

        // Save the new config
        await SaveAsync(board);
        return board;
    }

    private async Task BackupCorruptConfigAsync(string configPath)
    {
        var backupPath = $"{configPath}.bak";
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var timestampedBackupPath = $"{configPath}.{timestamp}.bak";

        if (File.Exists(configPath))
        {
            await Task.Run(() => File.Copy(configPath, timestampedBackupPath, overwrite: true));
        }
    }
}
