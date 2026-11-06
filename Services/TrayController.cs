using System.Drawing;
using System.Drawing.Drawing2D;
using BTWidget.Models;
using DrawingColor = System.Drawing.Color;
using Forms = System.Windows.Forms;

namespace BTWidget.Services;

public sealed class TrayController : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private Icon? _currentIcon;

    public TrayController(MainWindow window)
    {
        _icon = new Forms.NotifyIcon
        {
            Visible = true,
            Text = "BTWidget"
        };
        _icon.DoubleClick += (_, _) => window.Dispatcher.Invoke(window.Reveal);
        var menu = new Forms.ContextMenuStrip();
        var open = new Forms.ToolStripMenuItem("Open");
        open.Click += (_, _) => window.Dispatcher.Invoke(window.Reveal);
        var exit = new Forms.ToolStripMenuItem("Exit");
        exit.Click += (_, _) => window.Dispatcher.Invoke(window.RequestExit);
        menu.Items.Add(open);
        menu.Items.Add(exit);
        _icon.ContextMenuStrip = menu;
        Update([], false, false);
    }

    public void Update(IReadOnlyList<ConnectedDevice> devices, bool anyLow, bool anyCritical)
    {
        var next = CreateIcon(anyLow, anyCritical);
        var previous = _currentIcon;
        _currentIcon = next;
        _icon.Icon = next;
        previous?.Dispose();
        _icon.Text = BuildTip(devices);
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Icon = null;
        _currentIcon?.Dispose();
        _currentIcon = null;
        _icon.Dispose();
    }

    private static string BuildTip(IReadOnlyList<ConnectedDevice> devices)
    {
        if (devices.Count == 0)
        {
            return "BTWidget\nNo connected devices";
        }

        var lines = new List<string> { "BTWidget" };
        foreach (var device in devices.Take(4))
        {
            var battery = device.BatteryPercent is int percent ? $"{percent}%" : "no battery";
            var charging = device.IsCharging ? " charging" : "";
            lines.Add($"{device.Name} {battery}{charging}");
        }

        var tip = string.Join("\n", lines);
        return tip.Length <= 120 ? tip : tip[..120];
    }

    private static Icon CreateIcon(bool low, bool critical)
    {
        using var bitmap = new Bitmap(32, 32);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(DrawingColor.Transparent);
            var fill = critical
                ? DrawingColor.FromArgb(255, 209, 52, 56)
                : low
                    ? DrawingColor.FromArgb(255, 224, 161, 0)
                    : DrawingColor.FromArgb(255, 0, 120, 212);
            using var body = new SolidBrush(fill);
            graphics.FillRectangle(body, 6, 8, 16, 16);
            graphics.FillRectangle(body, 22, 12, 3, 8);
            using var cap = new SolidBrush(DrawingColor.White);
            graphics.FillRectangle(cap, 8, 14, 12, 4);
        }

        var handle = bitmap.GetHicon();
        try
        {
            using var icon = Icon.FromHandle(handle);
            return (Icon)icon.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);
}
