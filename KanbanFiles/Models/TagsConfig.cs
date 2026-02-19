namespace KanbanFiles.Models;

public class TagsConfig
{
    public List<TagDefinition> Definitions { get; set; } = [];
    public Dictionary<string, List<string>> Assignments { get; set; } = [];
}
