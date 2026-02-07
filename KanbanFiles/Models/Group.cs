namespace KanbanFiles.Models;

public class Group
{
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public List<string> ItemFileNames { get; set; } = new();
    public bool IsCollapsed { get; set; } = false;
}
