using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace TristansTrackers;

internal sealed record HudMenuItem(
    string Label,
    Action Execute,
    bool IsEnabled = true,
    bool SeparatorBefore = false,
    bool IsDestructive = false,
    string? IconGlyph = null);

internal enum HudMenuAnchorKind
{
    Element,
    Cursor
}

internal readonly record struct HudMenuAnchor(Rect Bounds, HudMenuAnchorKind Kind)
{
    public static HudMenuAnchor ForElement(Rect bounds) => new(bounds, HudMenuAnchorKind.Element);

    public static HudMenuAnchor AtCursor(Point point) => new(new Rect(point, new Size(0, 0)), HudMenuAnchorKind.Cursor);
}

public partial class HudMenuWindow : Window
{
    private bool _isClosing;
    private bool _closeQueued;

    internal HudMenuWindow(IEnumerable<HudMenuItem> items)
    {
        InitializeComponent();

        foreach (HudMenuItem item in items)
        {
            if (item.SeparatorBefore)
            {
                ItemsPanel.Children.Add(new Border
                {
                    Height = 1,
                    Margin = new Thickness(4, 3, 4, 3),
                    Background = Brushes.Black
                });
            }

            var button = new Button
            {
                Content = CreateItemContent(item),
                Foreground = item.IsDestructive
                    ? new SolidColorBrush(Color.FromRgb(0xFF, 0x72, 0x72))
                    : new SolidColorBrush(Color.FromRgb(0xF4, 0xF4, 0xF4)),
                IsEnabled = item.IsEnabled,
                Tag = item
            };
            button.Click += MenuItem_Click;
            ItemsPanel.Children.Add(button);
        }

        SourceInitialized += (_, _) => ApplyToolWindowStyle();
        Deactivated += Window_Deactivated;
    }

    internal void ShowAt(HudMenuAnchor anchor)
    {
        Rect workArea = SystemParameters.WorkArea;
        MenuScrollViewer.MaxHeight = Math.Max(1, workArea.Height - 2);

        Opacity = 0;
        Left = anchor.Bounds.Left;
        Top = anchor.Bounds.Bottom;
        Show();
        UpdateLayout();

        double left = Math.Clamp(
            anchor.Bounds.Left,
            workArea.Left,
            Math.Max(workArea.Left, workArea.Right - ActualWidth));
        double top = anchor.Kind == HudMenuAnchorKind.Element
            ? PlaceAroundElement(anchor.Bounds, workArea)
            : PlaceAroundCursor(anchor.Bounds.TopLeft, workArea);

        Left = left;
        Top = top;
        Opacity = 1;
        Activate();
        ItemsPanel.Children.OfType<Button>().FirstOrDefault()?.Focus();
        EnsureTopmost();
    }

    internal void EnsureTopmost()
    {
        if (_isClosing || !IsVisible)
        {
            return;
        }

        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.SetWindowPos(
            hwnd,
            NativeMethods.HWND_TOPMOST,
            0,
            0,
            0,
            0,
            NativeMethods.SWP_NOMOVE |
            NativeMethods.SWP_NOSIZE |
            NativeMethods.SWP_NOACTIVATE |
            NativeMethods.SWP_NOOWNERZORDER);
    }

    private static object CreateItemContent(HudMenuItem item)
    {
        if (string.IsNullOrWhiteSpace(item.IconGlyph))
        {
            return item.Label;
        }

        var content = new StackPanel { Orientation = Orientation.Horizontal };
        content.Children.Add(new TextBlock
        {
            Width = 20,
            Text = item.IconGlyph,
            VerticalAlignment = VerticalAlignment.Center
        });
        content.Children.Add(new TextBlock
        {
            Text = item.Label,
            VerticalAlignment = VerticalAlignment.Center
        });
        return content;
    }

    private void ApplyToolWindowStyle()
    {
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        int exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLong(
            hwnd,
            NativeMethods.GWL_EXSTYLE,
            exStyle | NativeMethods.WS_EX_TOOLWINDOW);
    }

    private double PlaceAroundElement(Rect anchor, Rect workArea)
    {
        double availableBelow = Math.Max(0, workArea.Bottom - anchor.Bottom);
        double availableAbove = Math.Max(0, anchor.Top - workArea.Top);
        bool placeBelow = ActualHeight <= availableBelow ||
            (ActualHeight > availableAbove && availableBelow >= availableAbove);

        ConstrainMenuHeight(placeBelow ? availableBelow : availableAbove);
        if (placeBelow)
        {
            return anchor.Bottom;
        }

        return Math.Max(workArea.Top, anchor.Top - ActualHeight);
    }

    private double PlaceAroundCursor(Point cursor, Rect workArea)
    {
        double availableBelow = Math.Max(0, workArea.Bottom - cursor.Y);
        double availableAbove = Math.Max(0, cursor.Y - workArea.Top);
        bool placeBelow = ActualHeight <= availableBelow ||
            (ActualHeight > availableAbove && availableBelow >= availableAbove);

        ConstrainMenuHeight(placeBelow ? availableBelow : availableAbove);
        if (placeBelow)
        {
            return cursor.Y;
        }

        return Math.Max(workArea.Top, cursor.Y - ActualHeight);
    }

    private void ConstrainMenuHeight(double availableHeight)
    {
        const double frameHeight = 8;
        if (ActualHeight <= availableHeight)
        {
            return;
        }

        MenuScrollViewer.MaxHeight = Math.Max(1, availableHeight - frameHeight);
        UpdateLayout();
    }

    private void MenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: HudMenuItem item })
        {
            return;
        }

        Close();
        item.Execute();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        _isClosing = true;
        base.OnClosing(e);
    }

    private void Window_Deactivated(object? sender, EventArgs e)
    {
        if (_isClosing || _closeQueued)
        {
            return;
        }

        _closeQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Normal, () =>
        {
            _closeQueued = false;
            if (!_isClosing && IsVisible)
            {
                Close();
            }
        });
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }
}
