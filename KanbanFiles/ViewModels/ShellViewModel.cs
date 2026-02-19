using Microsoft.UI.Xaml.Navigation;

namespace KanbanFiles.ViewModels;

public partial class ShellViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isBackEnabled;

    [ObservableProperty]
    private object? _selected;

    public INavigationService NavigationService { get; }

    public ShellViewModel(INavigationService navigationService)
    {
        NavigationService = navigationService;
        NavigationService.Navigated += OnNavigated;
    }

    private void OnNavigated(object sender, NavigationEventArgs e)
    {
        IsBackEnabled = NavigationService.CanGoBack;
    }
}
