namespace KanbanFiles.Services;

public class TagService
{
    private readonly BoardConfigService _boardConfigService;

    public TagService(BoardConfigService boardConfigService)
    {
        _boardConfigService = boardConfigService;
    }

    public TagsConfig GetOrCreateTagsConfig(Board board)
    {
        board.Tags ??= new TagsConfig();
        return board.Tags;
    }

    public List<TagDefinition> GetTagDefinitions(Board board)
    {
        return GetOrCreateTagsConfig(board).Definitions;
    }

    public async Task<TagDefinition> CreateTagAsync(Board board, string name, string color)
    {
        TagsConfig config = GetOrCreateTagsConfig(board);

        string sanitized = name.Trim();
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = "Tag";
        }

        // Ensure unique name
        string actualName = GetUniqueTagName(config.Definitions, sanitized);

        TagDefinition tag = new()
        {
            Name = actualName,
            Color = color
        };
        config.Definitions.Add(tag);
        await _boardConfigService.SaveAsync(board);

        return tag;
    }

    public async Task DeleteTagAsync(Board board, string tagName)
    {
        TagsConfig config = GetOrCreateTagsConfig(board);

        // Remove the definition
        config.Definitions.RemoveAll(t => t.Name == tagName);

        // Remove from all assignments
        List<string> keysToUpdate = [];
        foreach (KeyValuePair<string, List<string>> kvp in config.Assignments)
        {
            if (kvp.Value.Remove(tagName) && kvp.Value.Count == 0)
            {
                keysToUpdate.Add(kvp.Key);
            }
        }
        foreach (string key in keysToUpdate)
        {
            config.Assignments.Remove(key);
        }

        NormalizeConfig(board);
        await _boardConfigService.SaveAsync(board);
    }

    public async Task RenameTagAsync(Board board, string oldName, string newName)
    {
        TagsConfig config = GetOrCreateTagsConfig(board);

        string sanitized = newName.Trim();
        if (string.IsNullOrWhiteSpace(sanitized)) return;

        TagDefinition? tag = config.Definitions.FirstOrDefault(t => t.Name == oldName);
        if (tag == null) return;

        string actualName = GetUniqueTagName(config.Definitions, sanitized, excludeName: oldName);
        tag.Name = actualName;

        // Update all assignments
        foreach (KeyValuePair<string, List<string>> kvp in config.Assignments)
        {
            int index = kvp.Value.IndexOf(oldName);
            if (index >= 0)
            {
                kvp.Value[index] = actualName;
            }
        }

        await _boardConfigService.SaveAsync(board);
    }

    public async Task UpdateTagColorAsync(Board board, string tagName, string newColor)
    {
        TagsConfig config = GetOrCreateTagsConfig(board);

        TagDefinition? tag = config.Definitions.FirstOrDefault(t => t.Name == tagName);
        if (tag == null) return;

        tag.Color = newColor;
        await _boardConfigService.SaveAsync(board);
    }

    public List<string> GetTagsForItem(Board board, string columnFolderName, string fileName)
    {
        string key = BuildItemKey(columnFolderName, fileName);
        TagsConfig config = GetOrCreateTagsConfig(board);
        return config.Assignments.TryGetValue(key, out List<string>? tags) ? tags : [];
    }

    public List<string> GetTagsForGroup(Board board, string columnFolderName, string groupName)
    {
        string key = BuildGroupKey(columnFolderName, groupName);
        TagsConfig config = GetOrCreateTagsConfig(board);
        return config.Assignments.TryGetValue(key, out List<string>? tags) ? tags : [];
    }

    public async Task ToggleItemTagAsync(Board board, string columnFolderName, string fileName, string tagName)
    {
        string key = BuildItemKey(columnFolderName, fileName);
        await ToggleTagAssignmentAsync(board, key, tagName);
    }

    public async Task ToggleGroupTagAsync(Board board, string columnFolderName, string groupName, string tagName)
    {
        string key = BuildGroupKey(columnFolderName, groupName);
        await ToggleTagAssignmentAsync(board, key, tagName);
    }

    public async Task UpdateItemKeyAsync(Board board, string oldColumnFolderName, string oldFileName, string newColumnFolderName, string newFileName)
    {
        string oldKey = BuildItemKey(oldColumnFolderName, oldFileName);
        string newKey = BuildItemKey(newColumnFolderName, newFileName);

        if (oldKey == newKey) return;

        TagsConfig config = GetOrCreateTagsConfig(board);
        if (config.Assignments.Remove(oldKey, out List<string>? tags))
        {
            config.Assignments[newKey] = tags;
            await _boardConfigService.SaveAsync(board);
        }
    }

    public async Task UpdateGroupKeyAsync(Board board, string columnFolderName, string oldGroupName, string newGroupName)
    {
        string oldKey = BuildGroupKey(columnFolderName, oldGroupName);
        string newKey = BuildGroupKey(columnFolderName, newGroupName);

        if (oldKey == newKey) return;

        TagsConfig config = GetOrCreateTagsConfig(board);
        if (config.Assignments.Remove(oldKey, out List<string>? tags))
        {
            config.Assignments[newKey] = tags;
            await _boardConfigService.SaveAsync(board);
        }
    }

    public async Task RemoveAssignmentAsync(Board board, string columnFolderName, string fileName)
    {
        string key = BuildItemKey(columnFolderName, fileName);
        TagsConfig config = GetOrCreateTagsConfig(board);
        if (config.Assignments.Remove(key))
        {
            NormalizeConfig(board);
            await _boardConfigService.SaveAsync(board);
        }
    }

    private async Task ToggleTagAssignmentAsync(Board board, string key, string tagName)
    {
        TagsConfig config = GetOrCreateTagsConfig(board);

        if (!config.Assignments.TryGetValue(key, out List<string>? tags))
        {
            tags = [];
            config.Assignments[key] = tags;
        }

        if (!tags.Remove(tagName))
        {
            tags.Add(tagName);
        }

        // Clean up empty assignments
        if (tags.Count == 0)
        {
            config.Assignments.Remove(key);
        }

        NormalizeConfig(board);
        await _boardConfigService.SaveAsync(board);
    }

    private static string BuildItemKey(string columnFolderName, string fileName)
    {
        return $"{columnFolderName}/{fileName}";
    }

    private static string BuildGroupKey(string columnFolderName, string groupName)
    {
        return $"group:{columnFolderName}/{groupName}";
    }

    private static string GetUniqueTagName(List<TagDefinition> definitions, string baseName, string? excludeName = null)
    {
        HashSet<string> existing = definitions
            .Where(t => !string.Equals(t.Name, excludeName, StringComparison.OrdinalIgnoreCase))
            .Select(t => t.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!existing.Contains(baseName))
        {
            return baseName;
        }

        int counter = 1;
        string candidate;
        do
        {
            candidate = $"{baseName} ({counter})";
            counter++;
        } while (existing.Contains(candidate));

        return candidate;
    }

    private static void NormalizeConfig(Board board)
    {
        if (board.Tags is { Definitions.Count: 0, Assignments.Count: 0 })
        {
            board.Tags = null;
        }
    }
}
