using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media;

namespace KanbanFiles;

public sealed partial class MainWindow : Window
{
    public Frame NavigationFrame => RootFrame;

    public MainWindow()
    {
        this.InitializeComponent();

        Title = "Kanban Files";
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
            AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        }

        AppTitleBar.Loaded += AppTitleBar_Loaded;
        AppTitleBar.SizeChanged += AppTitleBar_SizeChanged;
        Activated += MainWindow_Activated;
    }

    private void AppTitleBar_Loaded(object sender, RoutedEventArgs e)
    {
        if (ExtendsContentIntoTitleBar)
        {
            SetTitleBarPadding();
        }
    }

    private void AppTitleBar_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (ExtendsContentIntoTitleBar)
        {
            SetTitleBarPadding();
        }
    }

    private void SetTitleBarPadding()
    {
        double scale = AppTitleBar.XamlRoot.RasterizationScale;
        LeftPaddingColumn.Width = new GridLength(AppWindow.TitleBar.LeftInset / scale);
        RightPaddingColumn.Width = new GridLength(AppWindow.TitleBar.RightInset / scale);
    }

    private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            TitleBarTextBlock.Foreground =
                (SolidColorBrush)App.Current.Resources["WindowCaptionForegroundDisabled"];
        }
        else
        {
            TitleBarTextBlock.Foreground =
                (SolidColorBrush)App.Current.Resources["WindowCaptionForeground"];
        }
    }
}
