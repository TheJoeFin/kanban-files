namespace KanbanFiles.Models;

public class ColumnConfig
{
    public string FolderName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public List<string> ItemOrder { get; set; } = new();
}
