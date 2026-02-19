using Windows.Storage;

namespace KanbanFiles.Services;

public class SettingsService : ISettingsService
{
    private const string ColumnWidthKey = "ColumnWidth";
    private const double DefaultColumnWidth = 280;

    private readonly ApplicationDataContainer _settings = ApplicationData.Current.LocalSettings;

    public double ColumnWidth
    {
        get
        {
            if (_settings.Values.TryGetValue(ColumnWidthKey, out object? value) && value is double width)
            {
                return width;
            }

            return DefaultColumnWidth;
        }
        set
        {
            _settings.Values[ColumnWidthKey] = value;
            WeakReferenceMessenger.Default.Send(new ColumnWidthChangedMessage(value));
        }
    }
}
