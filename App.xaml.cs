using System.Windows;
using BTWidget.Services;

namespace BTWidget;

public partial class App : Application
{
    private const string MutexName = @"Local\BTWidget.SingleInstance";
    private const string ShowEventName = @"Local\BTWidget.Show";

    private Mutex? _mutex;
    private EventWaitHandle? _showEvent;
    private Thread? _showThread;
    private MainWindow? _window;

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnUnhandledException;

        _mutex = new Mutex(true, MutexName, out var createdNew);
        if (!createdNew)
        {
            SignalExistingInstance();
            Shutdown();
            return;
        }

        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        _showThread = new Thread(WaitForShowRequests)
        {
            IsBackground = true,
            Name = "BTWidget.Show"
        };
        _showThread.Start();

        var settings = SettingsStore.Load();
        try
        {
            StartupService.Apply(settings.LaunchAtStartup);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            settings.LaunchAtStartup = false;
        }

        ThemeResources.Apply(ThemeResources.IsLightTheme());
        SystemEventsBridge.Subscribe(this);

        _window = new MainWindow(settings);
        _window.Show();

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SystemEventsBridge.Unsubscribe(this);
        _window?.Persist();
        _showEvent?.Dispose();
        _mutex?.Dispose();
        base.OnExit(e);
    }

    internal void ShowFromAnotherInstance()
    {
        if (_window == null)
        {
            return;
        }

        _window.Reveal();
    }

    private void WaitForShowRequests()
    {
        var handle = _showEvent;
        if (handle == null)
        {
            return;
        }

        try
        {
            while (handle.WaitOne())
            {
                Dispatcher.BeginInvoke(ShowFromAnotherInstance);
            }
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private static void SignalExistingInstance()
    {
        try
        {
            using var handle = EventWaitHandle.OpenExisting(ShowEventName);
            handle.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
        }
    }

    private void OnUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        MessageBox.Show(
            "BTWidget hit an unexpected error and will close.",
            "BTWidget",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        Shutdown(1);
    }
}
