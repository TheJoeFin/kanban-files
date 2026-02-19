namespace KanbanFiles.Views;

public sealed partial class SettingsPage : Page
{
    private string? _folderPath;

    public SettingsPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is string folderPath)
        {
            _folderPath = folderPath;
        }
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        App.NavigationService.NavigateTo(typeof(MainViewModel).FullName!, _folderPath, clearNavigation: true);
    }
}
