namespace KanbanFiles.Models;

public class TagDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#3498DB";

    public static List<string> DefaultColors =>
    [
        "#E74C3C", // Red
        "#E67E22", // Orange
        "#F1C40F", // Yellow
        "#2ECC71", // Green
        "#1ABC9C", // Teal
        "#3498DB", // Blue
        "#9B59B6", // Purple
        "#E91E8F", // Pink
        "#607D8B", // Blue Grey
        "#795548"  // Brown
    ];
}
