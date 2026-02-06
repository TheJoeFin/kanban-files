namespace KanbanFiles.Models;

public class Group
{
    public string Name { get; set; } = string.Empty;
    public List<string> ItemFileNames { get; set; } = new();
    public bool IsCollapsed { get; set; } = false;
}
