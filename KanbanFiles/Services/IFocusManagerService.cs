namespace KanbanFiles.Services;

public interface IFocusManagerService
{
    int FocusedColumnIndex { get; }
    int FocusedItemIndex { get; }
    
    bool MoveLeft(int totalColumns);
    bool MoveRight(int totalColumns);
    bool MoveUp(int itemsInColumn);
    bool MoveDown(int itemsInColumn);
    
    void SetFocus(int columnIndex, int itemIndex);
    void ClearFocus();
    
    event EventHandler<FocusChangedEventArgs>? FocusChanged;
}

public class FocusChangedEventArgs : EventArgs
{
    public int ColumnIndex { get; set; }
    public int ItemIndex { get; set; }
}
