using System.Text;
using KanbanFiles.Models;

namespace KanbanFiles.Services;

public class FileSystemService
{
    private static readonly string[] ExcludedFiles = { ".kanban.json", "groups.json" };

    public async Task<List<Column>> EnumerateColumnsAsync(Board board)
    {
        var columns = new List<Column>();

        foreach (var columnConfig in board.Columns.OrderBy(c => c.SortOrder))
        {
            var folderPath = Path.Combine(board.RootPath, columnConfig.FolderName);

            if (!Directory.Exists(folderPath))
            {
                continue;
            }

            var column = new Column
            {
                Name = columnConfig.DisplayName,
                FolderPath = folderPath,
                SortOrder = columnConfig.SortOrder
            };

            var items = await EnumerateItemsAsync(folderPath);

            // Apply item order from config
            var orderedItems = new List<KanbanItem>();
            foreach (var fileName in columnConfig.ItemOrder)
            {
                var item = items.FirstOrDefault(i => i.FileName == fileName);
                if (item != null)
                {
                    orderedItems.Add(item);
                }
            }

            // Add items not in order list (new files)
            foreach (var item in items)
            {
                if (!orderedItems.Contains(item))
                {
                    orderedItems.Add(item);
                }
            }

            foreach (var item in orderedItems)
            {
                column.Items.Add(item);
            }

            columns.Add(column);
        }

        return columns;
    }

    public async Task<List<KanbanItem>> EnumerateItemsAsync(string folderPath)
    {
        var items = new List<KanbanItem>();

        if (!Directory.Exists(folderPath))
        {
            return items;
        }

        var mdFiles = Directory.GetFiles(folderPath, "*.md", SearchOption.TopDirectoryOnly)
            .Where(path => !IsExcludedFile(Path.GetFileName(path)))
            .Where(path => !IsHiddenOrSystem(path));

        foreach (var filePath in mdFiles)
        {
            try
            {
                var fileName = Path.GetFileName(filePath);
                var title = Path.GetFileNameWithoutExtension(fileName);
                var content = await File.ReadAllTextAsync(filePath);
                var preview = GenerateContentPreview(content);
                var lastModified = File.GetLastWriteTime(filePath);

                items.Add(new KanbanItem
                {
                    Title = title,
                    FileName = fileName,
                    FilePath = filePath,
                    ContentPreview = preview,
                    FullContent = content,
                    LastModified = lastModified
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not read file {filePath}: {ex.Message}");
            }
        }

        return items;
    }

    public async Task<string> ReadItemContentAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return string.Empty;
        }

        try
        {
            return await File.ReadAllTextAsync(filePath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading file {filePath}: {ex.Message}");
            return string.Empty;
        }
    }

    public async Task WriteItemContentAsync(string filePath, string content)
    {
        try
        {
            await File.WriteAllTextAsync(filePath, content, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            throw new IOException($"Failed to write to {filePath}: {ex.Message}", ex);
        }
    }

    public async Task<string> CreateItemAsync(string folderPath, string title)
    {
        try
        {
            if (!Directory.Exists(folderPath))
            {
                throw new DirectoryNotFoundException($"Column folder does not exist: {folderPath}");
            }

            var fileName = SanitizeFileName(title) + ".md";
            var filePath = Path.Combine(folderPath, fileName);

            // Ensure unique filename
            var counter = 1;
            while (File.Exists(filePath))
            {
                fileName = $"{SanitizeFileName(title)}-{counter}.md";
                filePath = Path.Combine(folderPath, fileName);
                counter++;
            }

            await File.WriteAllTextAsync(filePath, $"# {title}\n\n", Encoding.UTF8);
            return filePath;
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new IOException($"Permission denied creating file '{title}' in {folderPath}", ex);
        }
        catch (IOException ex)
        {
            throw new IOException($"Failed to create file '{title}': {ex.Message}", ex);
        }
    }

    public async Task DeleteItemAsync(string filePath)
    {
        try
        {
            await Task.Run(() =>
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new IOException($"Permission denied deleting file: {Path.GetFileName(filePath)}", ex);
        }
        catch (IOException ex)
        {
            throw new IOException($"Failed to delete file '{Path.GetFileName(filePath)}': {ex.Message}", ex);
        }
    }

    public async Task<string> MoveItemAsync(string sourceFilePath, string targetFolderPath)
    {
        try
        {
            if (!File.Exists(sourceFilePath))
            {
                throw new FileNotFoundException($"Source file not found: {sourceFilePath}");
            }

            if (!Directory.Exists(targetFolderPath))
            {
                throw new DirectoryNotFoundException($"Target folder does not exist: {targetFolderPath}");
            }

            var fileName = Path.GetFileName(sourceFilePath);
            var targetFilePath = Path.Combine(targetFolderPath, fileName);

            // Ensure unique filename in target
            var counter = 1;
            var baseFileName = Path.GetFileNameWithoutExtension(fileName);
            var extension = Path.GetExtension(fileName);

            while (File.Exists(targetFilePath))
            {
                fileName = $"{baseFileName}-{counter}{extension}";
                targetFilePath = Path.Combine(targetFolderPath, fileName);
                counter++;
            }

            await Task.Run(() => File.Move(sourceFilePath, targetFilePath));
            return targetFilePath;
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new IOException($"Permission denied moving file: {Path.GetFileName(sourceFilePath)}", ex);
        }
        catch (IOException ex)
        {
            throw new IOException($"Failed to move file '{Path.GetFileName(sourceFilePath)}': {ex.Message}", ex);
        }
    }

    public async Task CreateColumnFolderAsync(string rootPath, string folderName)
    {
        try
        {
            var folderPath = Path.Combine(rootPath, folderName);
            await Task.Run(() => Directory.CreateDirectory(folderPath));
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new IOException($"Permission denied creating folder: {folderName}", ex);
        }
        catch (IOException ex)
        {
            throw new IOException($"Failed to create folder '{folderName}': {ex.Message}", ex);
        }
    }

    public async Task DeleteColumnFolderAsync(string folderPath)
    {
        try
        {
            await Task.Run(() =>
            {
                if (Directory.Exists(folderPath))
                {
                    Directory.Delete(folderPath, recursive: true);
                }
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new IOException($"Permission denied deleting folder: {Path.GetFileName(folderPath)}", ex);
        }
        catch (IOException ex)
        {
            throw new IOException($"Failed to delete folder '{Path.GetFileName(folderPath)}': {ex.Message}", ex);
        }
    }

    public static string GenerateContentPreview(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var previewLines = new List<string>();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // Skip empty lines and markdown headers
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#"))
            {
                continue;
            }

            previewLines.Add(trimmed);

            if (previewLines.Count >= 2)
            {
                break;
            }
        }

        return string.Join(" ", previewLines);
    }

    private bool IsExcludedFile(string fileName)
    {
        return ExcludedFiles.Contains(fileName, StringComparer.OrdinalIgnoreCase) ||
               fileName.StartsWith(".");
    }

    private bool IsHiddenOrSystem(string filePath)
    {
        try
        {
            var attributes = File.GetAttributes(filePath);
            return (attributes & FileAttributes.Hidden) == FileAttributes.Hidden ||
                   (attributes & FileAttributes.System) == FileAttributes.System;
        }
        catch
        {
            return false;
        }
    }

    private string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("", fileName.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(sanitized) ? "untitled" : sanitized;
    }
}
