namespace KanbanFiles.Models;

public class FileFilterConfig
{
    public List<string> IncludeExtensions { get; set; } = [];
    public List<string> ExcludeExtensions { get; set; } = [];
}
