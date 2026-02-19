namespace KanbanFiles.Messages;

public class ColumnWidthChangedMessage
{
    public double Width { get; }

    public ColumnWidthChangedMessage(double width)
    {
        Width = width;
    }
}
