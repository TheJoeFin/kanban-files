using Microsoft.UI.Xaml.Controls;
using KanbanFiles.ViewModels;

namespace KanbanFiles.Services;

public class NotificationService : INotificationService
{
    private MainViewModel? _mainViewModel;

    public void SetMainViewModel(MainViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
    }

    public void ShowNotification(string title, string message, InfoBarSeverity severity)
    {
        _mainViewModel?.ShowNotification(title, message, severity);
    }
}
