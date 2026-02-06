using System.Text.Json;
using System.Text.Json.Serialization;
using KanbanFiles.Models;

namespace KanbanFiles.Services;

public class GroupService
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private const string GroupsFileName = "groups.json";

    public async Task<GroupsConfig> LoadGroupsAsync(string columnFolderPath)
    {
        var groupsPath = Path.Combine(columnFolderPath, GroupsFileName);

        if (!File.Exists(groupsPath))
        {
            return new GroupsConfig();
        }

        try
        {
            var json = await File.ReadAllTextAsync(groupsPath);
            var groupsConfig = JsonSerializer.Deserialize<GroupsConfig>(json, _jsonOptions);
            if (groupsConfig != null)
            {
                return groupsConfig;
            }
        }
        catch (JsonException ex)
        {
            await BackupCorruptGroupsFileAsync(groupsPath);
            Console.WriteLine($"Warning: Corrupt groups.json detected. Backed up and returning empty config. Error: {ex.Message}");
        }

        return new GroupsConfig();
    }

    public async Task SaveGroupsAsync(string columnFolderPath, GroupsConfig groupsConfig)
    {
        var groupsPath = Path.Combine(columnFolderPath, GroupsFileName);
        var json = JsonSerializer.Serialize(groupsConfig, _jsonOptions);
        await File.WriteAllTextAsync(groupsPath, json);
    }

    public async Task CreateGroupAsync(string columnFolderPath, string groupName)
    {
        var groupsConfig = await LoadGroupsAsync(columnFolderPath);

        // Ensure unique name
        var uniqueName = GetUniqueGroupName(groupsConfig, groupName);

        groupsConfig.Groups.Add(new Group
        {
            Name = uniqueName,
            ItemFileNames = new List<string>(),
            IsCollapsed = false
        });

        await SaveGroupsAsync(columnFolderPath, groupsConfig);
    }

    public async Task DeleteGroupAsync(string columnFolderPath, string groupName)
    {
        var groupsConfig = await LoadGroupsAsync(columnFolderPath);
        var group = groupsConfig.Groups.FirstOrDefault(g => g.Name == groupName);
        
        if (group != null)
        {
            groupsConfig.Groups.Remove(group);
            await SaveGroupsAsync(columnFolderPath, groupsConfig);
        }
    }

    public async Task RenameGroupAsync(string columnFolderPath, string oldName, string newName)
    {
        var groupsConfig = await LoadGroupsAsync(columnFolderPath);
        var group = groupsConfig.Groups.FirstOrDefault(g => g.Name == oldName);
        
        if (group != null)
        {
            // Ensure unique name (excluding the group being renamed)
            var uniqueName = GetUniqueGroupName(groupsConfig, newName, excludeGroup: group);
            group.Name = uniqueName;
            await SaveGroupsAsync(columnFolderPath, groupsConfig);
        }
    }

    public async Task AddItemToGroupAsync(string columnFolderPath, string groupName, string fileName)
    {
        var groupsConfig = await LoadGroupsAsync(columnFolderPath);

        // Remove from any existing group first
        foreach (var group in groupsConfig.Groups)
        {
            group.ItemFileNames.Remove(fileName);
        }

        // Add to target group
        var targetGroup = groupsConfig.Groups.FirstOrDefault(g => g.Name == groupName);
        if (targetGroup != null && !targetGroup.ItemFileNames.Contains(fileName))
        {
            targetGroup.ItemFileNames.Add(fileName);
        }

        await SaveGroupsAsync(columnFolderPath, groupsConfig);
    }

    public async Task RemoveItemFromGroupAsync(string columnFolderPath, string fileName)
    {
        var groupsConfig = await LoadGroupsAsync(columnFolderPath);
        
        foreach (var group in groupsConfig.Groups)
        {
            group.ItemFileNames.Remove(fileName);
        }

        await SaveGroupsAsync(columnFolderPath, groupsConfig);
    }

    public async Task MoveItemBetweenGroupsAsync(string columnFolderPath, string fileName, string targetGroupName)
    {
        await AddItemToGroupAsync(columnFolderPath, targetGroupName, fileName);
    }

    public async Task ReorderGroupsAsync(string columnFolderPath, List<string> orderedGroupNames)
    {
        var groupsConfig = await LoadGroupsAsync(columnFolderPath);
        
        var reorderedGroups = new List<Group>();
        
        // Add groups in the specified order
        foreach (var groupName in orderedGroupNames)
        {
            var group = groupsConfig.Groups.FirstOrDefault(g => g.Name == groupName);
            if (group != null)
            {
                reorderedGroups.Add(group);
            }
        }

        // Add any groups not in the ordered list (shouldn't happen, but defensive)
        foreach (var group in groupsConfig.Groups)
        {
            if (!reorderedGroups.Contains(group))
            {
                reorderedGroups.Add(group);
            }
        }

        groupsConfig.Groups = reorderedGroups;
        await SaveGroupsAsync(columnFolderPath, groupsConfig);
    }

    public async Task CleanupStaleReferencesAsync(string columnFolderPath, IEnumerable<string> currentFileNames)
    {
        var groupsConfig = await LoadGroupsAsync(columnFolderPath);
        var currentFileNameSet = new HashSet<string>(currentFileNames);
        
        bool hasChanges = false;

        foreach (var group in groupsConfig.Groups)
        {
            var staleFiles = group.ItemFileNames.Where(f => !currentFileNameSet.Contains(f)).ToList();
            if (staleFiles.Count > 0)
            {
                foreach (var staleFile in staleFiles)
                {
                    group.ItemFileNames.Remove(staleFile);
                }
                hasChanges = true;
            }
        }

        if (hasChanges)
        {
            await SaveGroupsAsync(columnFolderPath, groupsConfig);
        }
    }

    private string GetUniqueGroupName(GroupsConfig groupsConfig, string baseName, Group? excludeGroup = null)
    {
        var existingNames = groupsConfig.Groups
            .Where(g => g != excludeGroup)
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

    private async Task BackupCorruptGroupsFileAsync(string groupsPath)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var timestampedBackupPath = $"{groupsPath}.{timestamp}.bak";

        if (File.Exists(groupsPath))
        {
            await Task.Run(() => File.Copy(groupsPath, timestampedBackupPath, overwrite: true));
        }
    }
}
