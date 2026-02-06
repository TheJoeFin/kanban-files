using System.Collections.ObjectModel;

namespace KanbanFiles.Models;

public class Column
{
    public string Name { get; set; } = string.Empty;
    public string FolderPath { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public ObservableCollection<KanbanItem> Items { get; set; } = new();
    public ObservableCollection<Group> Groups { get; set; } = new();
}
