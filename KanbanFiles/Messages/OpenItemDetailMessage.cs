namespace KanbanFiles.Messages;

public class OpenItemDetailMessage
{
    public KanbanItemViewModel KanbanItemViewModel { get; }

    public OpenItemDetailMessage(KanbanItemViewModel kanbanItemViewModel)
    {
        KanbanItemViewModel = kanbanItemViewModel;
    }
}
