using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace ClaudeLauncher;

public static class Ui
{
    public static Brush B(string key) => (Brush)Application.Current.Resources[key];
    public static readonly Brush Bg = Hex("#000000"), Surface = Hex("#111111"), SurfaceHover = Hex("#1A1A1A"),
        Border = Hex("#262626"), BorderHover = Hex("#3A3A3A"), Text = Hex("#FAFAFA"), Muted = Hex("#8C8C8C"),
        Accent = Hex("#FAFAFA"), Running = Hex("#4ADE80"), Danger = Hex("#F87171"), Canvas = Hex("#0A0A0A"),
        GridLine = Hex("#161616"), TopBar = Hex("#0A0A0A");

    public static SolidColorBrush Hex(string hex)
    {
        try
        {
            var c = (Color)ColorConverter.ConvertFromString(hex.StartsWith("#") ? hex : "#" + hex);
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }
        catch { return new SolidColorBrush(Color.FromRgb(0xD9, 0x77, 0x57)); }
    }

    public static TextBlock T(string text, double size = 13, Brush? color = null, FontWeight? weight = null)
    {
        return new TextBlock
        {
            Text = text, FontSize = size, Foreground = color ?? Text, FontWeight = weight ?? FontWeights.Normal,
            VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis,
        };
    }

    public static TextBlock Wrap(string text, double size = 13, Brush? color = null)
    {
        var t = T(text, size, color);
        t.TextWrapping = TextWrapping.Wrap;
        t.TextTrimming = TextTrimming.None;
        return t;
    }

    public static Button Btn(string label, string style, Action onClick, string? tip = null)
    {
        var b = new Button { Content = label, Style = (Style)Application.Current.Resources[style] };
        b.Click += (_, _) => onClick();
        if (tip != null) b.ToolTip = tip;
        return b;
    }

    // Segoe Fluent Icons glyph button.
    public static Button IconBtn(string glyph, Action onClick, string? tip = null) => Btn(glyph, "Icon", onClick, tip);

    public static MenuItem Item(string header, Action? onClick, bool enabled = true, bool isChecked = false)
    {
        var mi = new MenuItem { Header = header, IsEnabled = enabled, IsChecked = isChecked };
        if (onClick != null) mi.Click += (_, _) => onClick();
        return mi;
    }

    public static MenuItem Toggle(string header, bool value, Action<bool> set) => Item(header, () => set(!value), isChecked: value);

    public static void OpenMenu(ContextMenu menu, FrameworkElement anchor)
    {
        menu.PlacementTarget = anchor;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    // Circle with the first letter of the name.
    public static FrameworkElement Avatar(string name, string color, double size = 40)
    {
        var g = new Grid { Width = size, Height = size };
        g.Children.Add(new Ellipse { Fill = Hex(color) });
        var letter = name.Trim().Length > 0 ? name.Trim().Substring(0, 1).ToUpperInvariant() : "?";
        g.Children.Add(new TextBlock
        {
            Text = letter, FontSize = size * 0.4, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
        });
        return g;
    }

    // White-on-black pill switch.
    public static FrameworkElement PillSwitch(bool on, Action<bool> set, string? tip = null)
    {
        var track = new Border
        {
            Width = 40, Height = 22, CornerRadius = new CornerRadius(11), BorderThickness = new Thickness(1),
            Background = on ? Accent : Bg, BorderBrush = on ? Accent : BorderHover, Cursor = Cursors.Hand,
            ToolTip = tip,
        };
        var knob = new Ellipse
        {
            Width = 16, Height = 16, Fill = on ? Brushes.Black : Muted, Margin = new Thickness(2, 0, 2, 0),
            HorizontalAlignment = on ? HorizontalAlignment.Right : HorizontalAlignment.Left,
        };
        track.Child = knob;
        track.MouseLeftButtonUp += (_, e) => { e.Handled = true; set(!on); };
        return track;
    }

    // Checkbox row: rounded square + label.
    public static FrameworkElement CheckRow(string title, bool on, Action<bool> set)
    {
        var box = new Border
        {
            Width = 18, Height = 18, CornerRadius = new CornerRadius(5), BorderThickness = new Thickness(on ? 0 : 1),
            Background = on ? Accent : Surface, BorderBrush = BorderHover,
        };
        if (on)
            box.Child = new TextBlock
            {
                Text = "", FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 11,
                FontWeight = FontWeights.Bold, Foreground = Brushes.Black,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Background = Brushes.Transparent, Cursor = Cursors.Hand, Margin = new Thickness(0, 3, 16, 3) };
        row.Children.Add(box);
        var label = T(title, 13);
        label.Margin = new Thickness(10, 0, 0, 0);
        row.Children.Add(label);
        row.MouseLeftButtonUp += (_, _) => set(!on);
        return row;
    }

    public static Border Card(UIElement child, double radius = 22, Thickness? padding = null)
    {
        var b = new Border
        {
            Background = Surface, BorderBrush = Border, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(radius), Padding = padding ?? new Thickness(18, 0, 18, 0), Child = child,
        };
        b.MouseEnter += (_, _) => { b.Background = SurfaceHover; if (!Equals(b.Tag, "selected")) b.BorderBrush = BorderHover; };
        b.MouseLeave += (_, _) => { b.Background = Surface; if (!Equals(b.Tag, "selected")) b.BorderBrush = Border; };
        return b;
    }

    public static void Select(Border card, bool selected)
    {
        card.Tag = selected ? "selected" : null;
        card.BorderBrush = selected ? Accent : Border;
        card.BorderThickness = new Thickness(selected ? 1.5 : 1);
    }

    // Dark native title bar (Windows 10 2004+ / 11).
    public static void DarkTitleBar(Window w)
    {
        w.SourceInitialized += (_, _) =>
        {
            var h = new WindowInteropHelper(w).Handle;
            int on = 1;
            Native.DwmSetWindowAttribute(h, Native.DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, sizeof(int));
        };
    }

    public static Window Dialog(Window? owner, string title, double width, double? height = null)
    {
        var w = new Window
        {
            Title = title, Width = width, SizeToContent = height == null ? SizeToContent.Height : SizeToContent.Manual,
            ResizeMode = ResizeMode.NoResize, ShowInTaskbar = owner == null, Background = Bg,
            WindowStartupLocation = owner != null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen,
            FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"),
        };
        if (height != null) w.Height = height.Value;
        if (owner != null) w.Owner = owner;
        DarkTitleBar(w);
        return w;
    }
}
