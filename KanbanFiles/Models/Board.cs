namespace KanbanFiles.Models;

public class Board
{
    public string Name { get; set; } = "Kanban Board";
    public string RootPath { get; set; } = string.Empty;
    public List<ColumnConfig> Columns { get; set; } = new();
}
