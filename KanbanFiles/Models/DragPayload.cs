using System.Text.Json.Serialization;

namespace KanbanFiles.Models;

public class DragPayload
{
    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = string.Empty;

    [JsonPropertyName("sourceColumnPath")]
    public string SourceColumnPath { get; set; } = string.Empty;

    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = string.Empty;
}
