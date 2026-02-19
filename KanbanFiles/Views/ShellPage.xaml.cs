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
        App.NavigationService.Navigated += OnNavigated;

        App.NavigationService.NavigateTo(typeof(MainViewModel).FullName!);
    }

    private void OnNavigated(object sender, NavigationEventArgs e)
    {
        if (e.SourcePageType == typeof(SettingsPage))
        {
            NavigationViewControl.SelectedItem = NavigationViewControl.SettingsItem;
        }
        else
        {
            foreach (NavigationViewItem item in NavigationViewControl.MenuItems.OfType<NavigationViewItem>())
            {
                if (item.Tag is string tag && App.PageService.GetPageType(tag) == e.SourcePageType)
                {
                    NavigationViewControl.SelectedItem = item;
                    return;
                }
            }
        }
    }

    private void NavigationViewControl_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
    {
        App.NavigationService.GoBack();
    }

    private void NavigationViewControl_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.IsSettingsInvoked)
        {
            App.NavigationService.NavigateTo(typeof(SettingsViewModel).FullName!);
        }
        else if (args.InvokedItemContainer is NavigationViewItem item && item.Tag is string tag)
        {
            App.NavigationService.NavigateTo(tag);
        }
    }
}
