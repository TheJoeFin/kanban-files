using Microsoft.UI.Xaml.Controls;

namespace KanbanFiles.Services;

public interface INotificationService
{
    void ShowNotification(string title, string message, InfoBarSeverity severity);
}
