namespace KanbanFiles.Models;

public class DragPayload
{
    public string FilePath { get; set; } = string.Empty;
    public string SourceColumnPath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string? GroupName { get; set; }
}
