namespace KanbanFiles.Models;

public class KanbanItem
{
    public string Title { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string ContentPreview { get; set; } = string.Empty;
    public string FullContent { get; set; } = string.Empty;
    public string? GroupName { get; set; }
    public DateTime LastModified { get; set; }
}
