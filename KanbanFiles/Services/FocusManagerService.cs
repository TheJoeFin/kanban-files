namespace KanbanFiles.Services;

public class FocusManagerService : IFocusManagerService
{
    private int _focusedColumnIndex = -1;
    private int _focusedItemIndex = -1;
    
    public int FocusedColumnIndex => _focusedColumnIndex;
    public int FocusedItemIndex => _focusedItemIndex;
    
    public event EventHandler<FocusChangedEventArgs>? FocusChanged;
    
    public bool MoveLeft(int totalColumns)
    {
        if (_focusedColumnIndex <= 0) return false;
        
        _focusedColumnIndex--;
        _focusedItemIndex = 0;
        RaiseFocusChanged();
        return true;
    }
    
    public bool MoveRight(int totalColumns)
    {
        if (_focusedColumnIndex >= totalColumns - 1) return false;
        
        _focusedColumnIndex++;
        _focusedItemIndex = 0;
        RaiseFocusChanged();
        return true;
    }
    
    public bool MoveUp(int itemsInColumn)
    {
        if (_focusedItemIndex <= 0) return false;
        
        _focusedItemIndex--;
        RaiseFocusChanged();
        return true;
    }
    
    public bool MoveDown(int itemsInColumn)
    {
        if (_focusedItemIndex >= itemsInColumn - 1) return false;
        
        _focusedItemIndex++;
        RaiseFocusChanged();
        return true;
    }
    
    public void SetFocus(int columnIndex, int itemIndex)
    {
        _focusedColumnIndex = columnIndex;
        _focusedItemIndex = itemIndex;
        RaiseFocusChanged();
    }
    
    public void ClearFocus()
    {
        _focusedColumnIndex = -1;
        _focusedItemIndex = -1;
        RaiseFocusChanged();
    }
    
    private void RaiseFocusChanged()
    {
        FocusChanged?.Invoke(this, new FocusChangedEventArgs
        {
            ColumnIndex = _focusedColumnIndex,
            ItemIndex = _focusedItemIndex
        });
    }
}
