using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using BTWidget.Models;
using BTWidget.Services;
using FormsScreen = System.Windows.Forms.Screen;

namespace BTWidget;

public partial class MainWindow : Window
{
    private readonly WidgetSettings _settings;
    private readonly TrayController _tray;
    private readonly DispatcherTimer _timer;
    private BluetoothSnapshot _snapshot = BluetoothSnapshot.Empty;
    private int _refreshing;
    private bool _placed;
    private bool _allowExit;

    public MainWindow(WidgetSettings settings)
    {
        _settings = settings;
        InitializeComponent();
        Topmost = settings.KeepAboveWindows;
        if (settings.Left is double left && settings.Top is double top)
        {
            Left = left;
            Top = top;
        }

        _tray = new TrayController(this);
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        _timer.Tick += async (_, _) => await RefreshAsync();
        Loaded += async (_, _) =>
        {
            PlaceOnScreen();
            await RefreshAsync();
            _timer.Start();
        };
        Closed += (_, _) => _tray.Dispose();
    }

    public void Reveal()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        PlaceOnScreen();
    }

    public void Persist()
    {
        if (_placed && Left is var left && Top is var top && double.IsFinite(left) && double.IsFinite(top))
        {
            _settings.Left = left;
            _settings.Top = top;
        }

        try
        {
            SettingsStore.Save(_settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    public void RequestExit() => Exit();

    private void MenuButton_Click(object sender, RoutedEventArgs e)
    {
        RebuildMenu();
        var area = SystemParameters.WorkArea;
        MenuPopup.Placement = Top + ActualHeight + 220 > area.Bottom
            ? System.Windows.Controls.Primitives.PlacementMode.Top
            : System.Windows.Controls.Primitives.PlacementMode.Bottom;
        MenuPopup.HorizontalOffset = -150;
        MenuPopup.IsOpen = true;
    }

    private void Hide_Click(object sender, RoutedEventArgs e) => HideWidget();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        if (MenuPopup.IsOpen)
        {
            MenuPopup.IsOpen = false;
        }
        else
        {
            HideWidget();
        }

        e.Handled = true;
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || IsInsideButton(e.OriginalSource as DependencyObject))
        {
            return;
        }

        try
        {
            DragMove();
            Persist();
        }
        catch (InvalidOperationException)
        {
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_allowExit)
        {
            e.Cancel = true;
            HideWidget();
            return;
        }

        _timer.Stop();
        Persist();
        base.OnClosing(e);
    }

    private void HideWidget()
    {
        MenuPopup.IsOpen = false;
        Persist();
        Hide();
    }

    private void Exit()
    {
        _allowExit = true;
        Close();
        System.Windows.Application.Current.Shutdown();
    }

    private void PlaceOnScreen()
    {
        var width = ActualWidth > 1 ? ActualWidth : Width;
        var height = ActualHeight > 1 ? ActualHeight : 180;
        if (_settings.Left is double left && _settings.Top is double top && IsOnScreen(left, top, width, height))
        {
            Left = left;
            Top = top;
        }
        else
        {
            var area = SystemParameters.WorkArea;
            Left = Math.Max(area.Left + 8, area.Right - width - 16);
            Top = Math.Max(area.Top + 8, area.Bottom - height - 16);
        }

        _placed = true;
    }

    private bool IsOnScreen(double left, double top, double width, double height)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        var rect = new System.Drawing.Rectangle(
            (int)Math.Round(left * dpi.DpiScaleX),
            (int)Math.Round(top * dpi.DpiScaleY),
            Math.Max(1, (int)Math.Round(width * dpi.DpiScaleX)),
            Math.Max(1, (int)Math.Round(height * dpi.DpiScaleY)));

        foreach (var screen in FormsScreen.AllScreens)
        {
            var intersection = System.Drawing.Rectangle.Intersect(rect, screen.WorkingArea);
            if (intersection.Width >= 48 && intersection.Height >= 48)
            {
                return true;
            }
        }

        return false;
    }

    private async Task RefreshAsync()
    {
        if (Interlocked.Exchange(ref _refreshing, 1) == 1)
        {
            return;
        }

        try
        {
            var snapshot = await Task.Run(BluetoothDeviceReader.Read);
            _snapshot = snapshot;
            Render();
        }
        finally
        {
            Interlocked.Exchange(ref _refreshing, 0);
        }
    }

    private void Render()
    {
        var rows = _snapshot.Devices
            .Select(device => DeviceRow.From(device, _settings.LowBatteryPercent))
            .ToList();
        DeviceList.ItemsSource = rows;
        EmptyText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyText.Text = _snapshot.Error ?? "No connected devices";

        if (_snapshot.Error != null && rows.Count > 0)
        {
            StatusText.Text = "Couldn't refresh";
        }
        else if (rows.Count > 0)
        {
            StatusText.Text = "Updated " + DateTime.Now.ToString("t");
        }
        else
        {
            StatusText.Text = "";
        }

        var anyCritical = rows.Any(row => row.Level == BatteryLevel.Critical);
        var anyLow = rows.Any(row => row.IsLow);
        _tray.Update(_snapshot.Devices, anyLow, anyCritical);
        Dispatcher.BeginInvoke(KeepInsideWorkArea, DispatcherPriority.Loaded);
    }

    private void KeepInsideWorkArea()
    {
        if (ActualWidth < 1 || ActualHeight < 1)
        {
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        var widthPx = Math.Max(1, (int)Math.Round(ActualWidth * dpi.DpiScaleX));
        var heightPx = Math.Max(1, (int)Math.Round(ActualHeight * dpi.DpiScaleY));
        var leftPx = (int)Math.Round(Left * dpi.DpiScaleX);
        var topPx = (int)Math.Round(Top * dpi.DpiScaleY);
        var rect = new System.Drawing.Rectangle(leftPx, topPx, widthPx, heightPx);
        var screen = FormsScreen.AllScreens.FirstOrDefault(candidate => candidate.WorkingArea.IntersectsWith(rect))
            ?? FormsScreen.PrimaryScreen;
        if (screen == null)
        {
            return;
        }

        var work = screen.WorkingArea;
        if (leftPx + widthPx > work.Right - 8)
        {
            leftPx = work.Right - widthPx - 8;
        }

        if (topPx + heightPx > work.Bottom - 8)
        {
            topPx = work.Bottom - heightPx - 8;
        }

        if (leftPx < work.Left + 8)
        {
            leftPx = work.Left + 8;
        }

        if (topPx < work.Top + 8)
        {
            topPx = work.Top + 8;
        }

        var left = leftPx / dpi.DpiScaleX;
        var top = topPx / dpi.DpiScaleY;
        if (Math.Abs(Left - left) > 1 || Math.Abs(Top - top) > 1)
        {
            Left = left;
            Top = top;
            _settings.Left = left;
            _settings.Top = top;
        }
    }

    private void RebuildMenu()
    {
        var panel = new StackPanel { Margin = new Thickness(4) };
        panel.Children.Add(MenuItem("Refresh", false, () =>
        {
            MenuPopup.IsOpen = false;
            _ = RefreshAsync();
        }));
        panel.Children.Add(MenuItem("Keep above other windows", _settings.KeepAboveWindows, () =>
        {
            _settings.KeepAboveWindows = !_settings.KeepAboveWindows;
            Topmost = _settings.KeepAboveWindows;
            MenuPopup.IsOpen = false;
            Persist();
        }));
        var canStart = StartupService.CurrentExecutable() != null;
        panel.Children.Add(MenuItem("Start with Windows", _settings.LaunchAtStartup, ToggleStartup, canStart));
        panel.Children.Add(new TextBlock
        {
            Text = "Low battery",
            Margin = new Thickness(10, 8, 10, 2),
            FontSize = 11,
            Foreground = FindBrush("TextMuted")
        });

        foreach (var choice in WidgetSettings.LowBatteryChoices)
        {
            var percent = choice;
            panel.Children.Add(MenuItem($"{percent}%", _settings.LowBatteryPercent == percent, () =>
            {
                _settings.LowBatteryPercent = percent;
                MenuPopup.IsOpen = false;
                Persist();
                Render();
            }));
        }

        panel.Children.Add(new Border
        {
            Height = 1,
            Margin = new Thickness(6, 6, 6, 6),
            Background = FindBrush("WindowBorder")
        });
        panel.Children.Add(MenuItem("Exit", false, Exit));

        MenuPopup.Child = new Border
        {
            MinWidth = 240,
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(0),
            Background = FindBrush("MenuBackground"),
            BorderBrush = FindBrush("WindowBorder"),
            Child = panel
        };
    }

    private Button MenuItem(string label, bool check, Action action, bool enabled = true)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var mark = new TextBlock
        {
            Text = "\uE73E",
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = check ? Visibility.Visible : Visibility.Hidden,
            Foreground = FindBrush("TextPrimary")
        };
        var text = new TextBlock
        {
            Text = label,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 13
        };
        Grid.SetColumn(text, 1);
        grid.Children.Add(mark);
        grid.Children.Add(text);

        var button = new Button
        {
            Style = (Style)FindResource("MenuButton"),
            Content = grid,
            IsEnabled = enabled
        };
        button.Click += (_, _) =>
        {
            try
            {
                action();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or System.Security.SecurityException)
            {
                MenuPopup.IsOpen = false;
                System.Windows.MessageBox.Show(
                    "That setting could not be saved.",
                    "BTWidget",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        };
        return button;
    }

    private void ToggleStartup()
    {
        var enabled = !_settings.LaunchAtStartup;
        StartupService.Apply(enabled);
        _settings.LaunchAtStartup = enabled;
        MenuPopup.IsOpen = false;
        Persist();
    }

    private Brush FindBrush(string key) => (Brush)FindResource(key);

    private static bool IsInsideButton(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is Button)
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }
}
