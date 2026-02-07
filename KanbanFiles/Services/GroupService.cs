using System.Text.Json;
using System.Text.Json.Serialization;

namespace KanbanFiles.Services;

public class GroupService
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private const string GroupFileExtension = ".json";
    private const string LegacyGroupsFileName = "groups.json";

    private static readonly HashSet<string> ReservedFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".kanban.json",
        LegacyGroupsFileName
    };

    public async Task<List<Group>> LoadGroupsAsync(string columnFolderPath)
    {
        if (!Directory.Exists(columnFolderPath))
            return [];

        await MigrateLegacyGroupsAsync(columnFolderPath);

        List<Group> groups = [];

        IEnumerable<string> jsonFiles = Directory.GetFiles(columnFolderPath, $"*{GroupFileExtension}", SearchOption.TopDirectoryOnly)
            .Where(f => !ReservedFileNames.Contains(Path.GetFileName(f)));

        foreach (string filePath in jsonFiles)
        {
            try
            {
                string json = await File.ReadAllTextAsync(filePath);
                Group? group = JsonSerializer.Deserialize<Group>(json, _jsonOptions);
                if (group != null)
                {
                    group.Name = Path.GetFileNameWithoutExtension(filePath);
                    groups.Add(group);
                }
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"Warning: Could not read group file {filePath}: {ex.Message}");
            }
        }

        groups.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));
        return groups;
    }

    public static async Task SaveGroupAsync(string columnFolderPath, Group group)
    {
        string filePath = GetGroupFilePath(columnFolderPath, group.Name);
        string json = JsonSerializer.Serialize(group, _jsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    public static async Task DeleteGroupFileAsync(string columnFolderPath, string groupName)
    {
        string filePath = GetGroupFilePath(columnFolderPath, groupName);
        if (File.Exists(filePath))
        {
            await Task.Run(() => File.Delete(filePath));
        }
    }

    public static async Task RenameGroupFileAsync(string columnFolderPath, string oldName, string newName)
    {
        string oldPath = GetGroupFilePath(columnFolderPath, oldName);
        if (!File.Exists(oldPath)) return;

        string json = await File.ReadAllTextAsync(oldPath);
        Group? group = JsonSerializer.Deserialize<Group>(json, _jsonOptions);
        if (group != null)
        {
            group.Name = newName;
            await SaveGroupAsync(columnFolderPath, group);

            string newPath = GetGroupFilePath(columnFolderPath, newName);
            if (!oldPath.Equals(newPath, StringComparison.OrdinalIgnoreCase))
            {
                await Task.Run(() => File.Delete(oldPath));
            }
        }
    }

    public async Task AddItemToGroupAsync(string columnFolderPath, string groupName, string fileName)
    {
        // Remove from any existing group first
        await RemoveItemFromGroupAsync(columnFolderPath, fileName);

        // Add to target group
        string filePath = GetGroupFilePath(columnFolderPath, groupName);
        if (!File.Exists(filePath)) return;

        string json = await File.ReadAllTextAsync(filePath);
        Group? group = JsonSerializer.Deserialize<Group>(json, _jsonOptions);
        if (group != null && !group.ItemFileNames.Contains(fileName))
        {
            group.Name = groupName;
            group.ItemFileNames.Add(fileName);
            await SaveGroupAsync(columnFolderPath, group);
        }
    }

    public async Task RemoveItemFromGroupAsync(string columnFolderPath, string fileName)
    {
        List<Group> groups = await LoadGroupsAsync(columnFolderPath);
        foreach (Group group in groups)
        {
            if (group.ItemFileNames.Remove(fileName))
            {
                await SaveGroupAsync(columnFolderPath, group);
            }
        }
    }

    public async Task ReorderGroupsAsync(string columnFolderPath, List<string> orderedGroupNames)
    {
        List<Group> groups = await LoadGroupsAsync(columnFolderPath);

        for (int i = 0; i < orderedGroupNames.Count; i++)
        {
            Group? group = groups.FirstOrDefault(g => g.Name == orderedGroupNames[i]);
            if (group != null && group.SortOrder != i)
            {
                group.SortOrder = i;
                await SaveGroupAsync(columnFolderPath, group);
            }
        }
    }

    public async Task CleanupStaleReferencesAsync(string columnFolderPath, IEnumerable<string> currentFileNames)
    {
        HashSet<string> currentFileNameSet = [.. currentFileNames];
        List<Group> groups = await LoadGroupsAsync(columnFolderPath);

        foreach (Group group in groups)
        {
            List<string> staleFiles = group.ItemFileNames.Where(f => !currentFileNameSet.Contains(f)).ToList();
            if (staleFiles.Count > 0)
            {
                foreach (string staleFile in staleFiles)
                {
                    group.ItemFileNames.Remove(staleFile);
                }
                await SaveGroupAsync(columnFolderPath, group);
            }
        }
    }

    public static string GetGroupFilePath(string columnFolderPath, string groupName)
    {
        return Path.Combine(columnFolderPath, groupName + GroupFileExtension);
    }

    public static string GetUniqueGroupName(IEnumerable<Group> existingGroups, string baseName, string? excludeGroupName = null)
    {
        HashSet<string> existingNames = existingGroups
            .Where(g => !string.Equals(g.Name, excludeGroupName, StringComparison.OrdinalIgnoreCase))
            .Select(g => g.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!existingNames.Contains(baseName))
        {
            return baseName;
        }

        int counter = 1;
        string candidateName;
        do
        {
            candidateName = $"{baseName} ({counter})";
            counter++;
        } while (existingNames.Contains(candidateName));

        return candidateName;
    }

    public static string SanitizeGroupName(string name)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        string sanitized = string.Join("", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(sanitized) ? "Group" : sanitized;
    }

    private static async Task MigrateLegacyGroupsAsync(string columnFolderPath)
    {
        string legacyPath = Path.Combine(columnFolderPath, LegacyGroupsFileName);
        if (!File.Exists(legacyPath)) return;

        try
        {
            string json = await File.ReadAllTextAsync(legacyPath);
            GroupsConfig? legacyConfig = JsonSerializer.Deserialize<GroupsConfig>(json, _jsonOptions);
            if (legacyConfig?.Groups != null)
            {
                for (int i = 0; i < legacyConfig.Groups.Count; i++)
                {
                    Group group = legacyConfig.Groups[i];
                    group.SortOrder = i;
                    await SaveGroupAsync(columnFolderPath, group);
                }
            }

            File.Delete(legacyPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to migrate legacy groups.json in {columnFolderPath}: {ex.Message}");
        }
    }
}
