namespace KanbanFiles.ViewModels;

public partial class SettingsViewModel : BaseViewModel
{
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private double _columnWidth;

    public SettingsViewModel()
    {
        Title = "Settings";
        _settingsService = App.SettingsService;
        _columnWidth = _settingsService.ColumnWidth;
    }

    partial void OnColumnWidthChanged(double value)
    {
        _settingsService.ColumnWidth = value;
    }
}
