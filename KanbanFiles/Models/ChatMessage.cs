namespace KanbanFiles.Models;

public partial class ChatMessage : ObservableObject
{
    public required string Role { get; init; }

    [ObservableProperty]
    private string _text = string.Empty;

    [ObservableProperty]
    private bool _hasBeenApplied;

    public bool IsUser => string.Equals(Role, "user", StringComparison.OrdinalIgnoreCase);
    public bool IsAssistant => string.Equals(Role, "assistant", StringComparison.OrdinalIgnoreCase);
}
