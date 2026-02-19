using Microsoft.UI.Xaml.Navigation;

namespace KanbanFiles.Views;

public sealed partial class ShellPage : Page
{
    public ShellViewModel ViewModel { get; }

    public ShellPage()
    {
        ViewModel = new ShellViewModel(App.NavigationService);
        InitializeComponent();

        App.NavigationService.Frame = NavigationFrame;

        App.NavigationService.NavigateTo(typeof(MainViewModel).FullName!, App.ActivationFolderPath);
    }
}
