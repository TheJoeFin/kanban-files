using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI;
using WinRT.Interop;
using Windows.Graphics;
using Windows.Storage;

namespace KanbanFiles
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private Window? window;
        private AppWindow? appWindow;
        private OverlappedPresenter? presenter;

        public static Window? MainWindow { get; private set; }
        public static DispatcherQueue? MainDispatcher => MainWindow?.DispatcherQueue;

        private const string WINDOW_WIDTH_KEY = "WindowWidth";
        private const string WINDOW_HEIGHT_KEY = "WindowHeight";
        private const string WINDOW_X_KEY = "WindowX";
        private const string WINDOW_Y_KEY = "WindowY";
        private const string IS_MAXIMIZED_KEY = "IsMaximized";
        private const int DEFAULT_WIDTH = 1280;
        private const int DEFAULT_HEIGHT = 800;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            window ??= new Window();
            MainWindow = window;

            if (window.Content is not Frame rootFrame)
            {
                rootFrame = new Frame();
                rootFrame.NavigationFailed += OnNavigationFailed;
                window.Content = rootFrame;
            }

            InitializeWindowState();
            _ = rootFrame.Navigate(typeof(MainPage), e.Arguments);
            window.Activate();
        }

        private void InitializeWindowState()
        {
            if (window == null) return;

            try
            {
                var windowHandle = WindowNative.GetWindowHandle(window);
                var windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
                appWindow = AppWindow.GetFromWindowId(windowId);

                if (appWindow == null) return;

                presenter = appWindow.Presenter as OverlappedPresenter;

                // Subscribe to window events
                window.Closed += OnWindowClosed;
                appWindow.Changed += OnAppWindowChanged;

                // Restore window state
                RestoreWindowState();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing window state: {ex.Message}");
            }
        }

        private void RestoreWindowState()
        {
            if (appWindow == null) return;

            try
            {
                var settings = ApplicationData.Current.LocalSettings;

                // Restore maximized state
                bool isMaximized = settings.Values.TryGetValue(IS_MAXIMIZED_KEY, out var maxValue) && (bool)maxValue;

                if (presenter != null && isMaximized)
                {
                    presenter.Maximize();
                    return; // No need to restore size/position if maximized
                }

                // Restore size
                int width = settings.Values.TryGetValue(WINDOW_WIDTH_KEY, out var widthValue) ? (int)widthValue : DEFAULT_WIDTH;
                int height = settings.Values.TryGetValue(WINDOW_HEIGHT_KEY, out var heightValue) ? (int)heightValue : DEFAULT_HEIGHT;

                // Validate dimensions
                width = Math.Max(800, Math.Min(width, 3840)); // Min 800, Max 4K width
                height = Math.Max(600, Math.Min(height, 2160)); // Min 600, Max 4K height

                // Restore position
                if (settings.Values.TryGetValue(WINDOW_X_KEY, out var xValue) &&
                    settings.Values.TryGetValue(WINDOW_Y_KEY, out var yValue))
                {
                    int x = (int)xValue;
                    int y = (int)yValue;

                    // Validate position is within screen bounds
                    if (IsPositionValid(x, y, width, height))
                    {
                        appWindow.MoveAndResize(new RectInt32(x, y, width, height));
                        return;
                    }
                }

                // If position is invalid or not saved, just resize and center
                appWindow.Resize(new SizeInt32(width, height));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error restoring window state: {ex.Message}");
                
                // Fallback to default size
                try
                {
                    appWindow.Resize(new SizeInt32(DEFAULT_WIDTH, DEFAULT_HEIGHT));
                }
                catch { }
            }
        }

        private bool IsPositionValid(int x, int y, int width, int height)
        {
            try
            {
                var displayArea = DisplayArea.GetFromWindowId(appWindow!.Id, DisplayAreaFallback.Primary);
                if (displayArea == null) return false;

                var workArea = displayArea.WorkArea;

                // Check if at least 100x100 pixels of the window would be visible
                bool hasVisibleArea = x + width > workArea.X + 100 &&
                                     x < workArea.X + workArea.Width - 100 &&
                                     y + height > workArea.Y + 100 &&
                                     y < workArea.Y + workArea.Height - 100;

                return hasVisibleArea;
            }
            catch
            {
                // If we can't determine display area, assume position is invalid
                return false;
            }
        }

        private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
        {
            // Save state on size or position changes
            if (args.DidSizeChange || args.DidPositionChange || args.DidPresenterChange)
            {
                SaveWindowState();
            }
        }

        private void OnWindowClosed(object sender, WindowEventArgs args)
        {
            SaveWindowState();

            // Unsubscribe from events
            if (window != null)
            {
                window.Closed -= OnWindowClosed;
            }

            if (appWindow != null)
            {
                appWindow.Changed -= OnAppWindowChanged;
            }
        }

        private void SaveWindowState()
        {
            if (appWindow == null) return;

            try
            {
                var settings = ApplicationData.Current.LocalSettings;

                // Save maximized state
                bool isMaximized = presenter?.State == OverlappedPresenterState.Maximized;
                settings.Values[IS_MAXIMIZED_KEY] = isMaximized;

                // Only save size/position if not maximized
                if (!isMaximized)
                {
                    var position = appWindow.Position;
                    var size = appWindow.Size;

                    settings.Values[WINDOW_WIDTH_KEY] = size.Width;
                    settings.Values[WINDOW_HEIGHT_KEY] = size.Height;
                    settings.Values[WINDOW_X_KEY] = position.X;
                    settings.Values[WINDOW_Y_KEY] = position.Y;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving window state: {ex.Message}");
            }
        }

        /// <summary>
        /// Invoked when Navigation to a certain page fails
        /// </summary>
        /// <param name="sender">The Frame which failed navigation</param>
        /// <param name="e">Details about the navigation failure</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }
    }
}
