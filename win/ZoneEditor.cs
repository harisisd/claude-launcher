using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ClaudeLauncher;

public sealed class ZoneEditor : Window
{
    readonly AppModel model;
    readonly ZoneLayout target;
    readonly ZoneCanvas canvas;
    readonly TextBox name;
    readonly TextBlock info;
    readonly Button removeBtn;
    public ZoneLayout? Result;
    const string Help = "Geser zona untuk memindah  ·  tarik tepi/sudut untuk ubah ukuran  ·  klik dua kali untuk membagi (Shift: atas/bawah)  ·  Delete untuk hapus";

    public ZoneEditor(Window owner, AppModel model, ZoneLayout target, (double w, double h) area)
    {
        this.model = model;
        this.target = target;
        Owner = owner;
        Title = "Editor zona";
        Width = 1000; Height = 700; MinWidth = 720; MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        Background = Ui.Bg;
        Ui.DarkTitleBar(this);

        canvas = new ZoneCanvas(target.Zones, area.w, area.h);
        canvas.SelectionChanged += () => removeBtn!.IsEnabled = canvas.Selected != null;

        var root = new DockPanel { Margin = new Thickness(20) };

        var top = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
        name = new TextBox { Style = (Style)Application.Current.Resources["Field"], Text = target.Name, Width = 300 };
        var tools = new StackPanel { Orientation = Orientation.Horizontal };
        removeBtn = Ui.Btn("Hapus zona", "Secondary", canvas.RemoveSelected);
        removeBtn.IsEnabled = false;
        foreach (var b in new[]
        {
            Ui.Btn("+  Zona", "Secondary", canvas.AddZone),
            removeBtn,
            Ui.Btn("Kolom", "Secondary", () => canvas.SetZones(Templates.Zones("columns", Math.Max(2, canvas.Zones.Count)))),
            Ui.Btn("Grid", "Secondary", () => canvas.SetZones(Templates.Zones("grid", Math.Max(2, canvas.Zones.Count)))),
        })
        { b.Margin = new Thickness(8, 0, 0, 0); tools.Children.Add(b); }
        DockPanel.SetDock(tools, Dock.Right);
        top.Children.Add(tools);
        name.HorizontalAlignment = HorizontalAlignment.Left;
        top.Children.Add(name);
        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);

        var bottom = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
        var btns = new StackPanel { Orientation = Orientation.Horizontal };
        var cancel = Ui.Btn("Batal", "Secondary", () => { DialogResult = false; });
        cancel.IsCancel = true;
        var save = Ui.Btn("Simpan", "Primary", Save);
        save.Margin = new Thickness(8, 0, 0, 0);
        btns.Children.Add(cancel); btns.Children.Add(save);
        DockPanel.SetDock(btns, Dock.Right);
        bottom.Children.Add(btns);
        info = Ui.Wrap(Help, 12, Ui.Muted);
        info.VerticalAlignment = VerticalAlignment.Center;
        info.Margin = new Thickness(0, 0, 12, 0);
        bottom.Children.Add(info);
        DockPanel.SetDock(bottom, Dock.Bottom);
        root.Children.Add(bottom);

        root.Children.Add(canvas);
        Content = root;
        name.TextChanged += (_, _) => { info.Text = Help; info.Foreground = Ui.Muted; };
        Loaded += (_, _) => canvas.Focus();
    }

    void Error(string e) { info.Text = e; info.Foreground = Ui.Danger; }

    void Save()
    {
        var value = name.Text.Trim();
        if (value.Length == 0) { Error("Nama layout tidak boleh kosong."); return; }
        if (model.Customs.Any(c => c.Id != target.Id && string.Equals(c.Name, value, StringComparison.OrdinalIgnoreCase)))
        { Error("Nama layout sudah dipakai."); return; }
        if (canvas.Zones.Count == 0) { Error("Minimal harus ada satu zona."); return; }
        Result = new ZoneLayout { Id = target.Id, Name = value, Zones = canvas.Zones.ToList() };
        DialogResult = true;
    }
}

// Interactive zone canvas: drag to move, edges/corners to resize, double-click to split.
public sealed class ZoneCanvas : FrameworkElement
{
    public List<ZRect> Zones;
    public int? Selected { get; private set; }
    public event Action? SelectionChanged;
    readonly double aspectW, aspectH;

    [Flags] enum Edges { None = 0, Left = 1, Right = 2, Top = 4, Bottom = 8 }
    const double SnapDistance = 0.015, MinSize = 0.05, EdgePx = 8, CornerPx = 14;

    int? dragIndex;
    Edges dragEdges;
    Point dragOrigin;
    ZRect dragStart;

    public ZoneCanvas(List<ZRect> zones, double aspectW, double aspectH)
    {
        Zones = new List<ZRect>(zones);
        this.aspectW = Math.Max(1, aspectW);
        this.aspectH = Math.Max(1, aspectH);
        Focusable = true;
        FocusVisualStyle = null;
        ClipToBounds = true;
    }

    void Select(int? i) { Selected = i; SelectionChanged?.Invoke(); InvalidateVisual(); }

    public void SetZones(List<ZRect> z) { Zones = z; Select(null); }

    public void AddZone()
    {
        double k = Zones.Count % 5;
        Zones.Add(new ZRect(0.3 + 0.03 * k, 0.25 + 0.03 * k, 0.4, 0.5));
        Select(Zones.Count - 1);
    }

    public void RemoveSelected()
    {
        if (Selected is int s && s >= 0 && s < Zones.Count) { Zones.RemoveAt(s); Select(null); }
    }

    Rect Area()
    {
        double s = Math.Min(ActualWidth / aspectW, ActualHeight / aspectH);
        double w = aspectW * s, h = aspectH * s;
        return new Rect((ActualWidth - w) / 2, (ActualHeight - h) / 2, w, h);
    }

    Rect ToScreen(ZRect z, Rect a) => new(a.X + z.X * a.Width, a.Y + z.Y * a.Height, z.W * a.Width, z.H * a.Height);

    static Rect CloseBox(Rect r) => new(r.Right - 8 - 24, r.Top + 8, 24, 24);

    protected override void OnRender(DrawingContext dc)
    {
        var a = Area();
        dc.DrawRectangle(Ui.Bg, null, new Rect(0, 0, ActualWidth, ActualHeight)); // hit target
        dc.DrawRectangle(Ui.Canvas, null, a);
        var gridPen = new Pen(Ui.GridLine, 1);
        for (int i = 1; i < 10; i++)
        {
            double x = a.X + a.Width * i / 10, y = a.Y + a.Height * i / 10;
            dc.DrawLine(gridPen, new Point(x, a.Top), new Point(x, a.Bottom));
            dc.DrawLine(gridPen, new Point(a.Left, y), new Point(a.Right, y));
        }
        dc.DrawRectangle(null, new Pen(Ui.Border, 1), a);

        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var face = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
        var faceN = new Typeface("Segoe UI");
        var tint = new SolidColorBrush(Color.FromArgb(15, 0xFA, 0xFA, 0xFA));
        for (int i = 0; i < Zones.Count; i++)
        {
            var z = Zones[i];
            var r = ToScreen(z, a);
            r.Inflate(-1.5, -1.5);
            if (r.Width <= 0 || r.Height <= 0) continue;
            bool sel = Selected == i;
            dc.DrawRectangle(Ui.Surface, null, r);
            if (sel) dc.DrawRectangle(tint, null, r);
            dc.DrawRectangle(null, new Pen(sel ? Ui.Accent : Ui.BorderHover, sel ? 2 : 1), r);
            if (r.Width > 70 && r.Height > 70)
            {
                var num = new FormattedText((i + 1).ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face, 30, sel ? Ui.Accent : Ui.Text, dpi);
                var size = new FormattedText(
                    $"{Math.Round(z.W * aspectW)} × {Math.Round(z.H * aspectH)}   ({Math.Round(z.W * 100)}% × {Math.Round(z.H * 100)}%)",
                    CultureInfo.InvariantCulture, FlowDirection.LeftToRight, faceN, 11, Ui.Muted, dpi);
                double total = num.Height + 4 + size.Height;
                double top = r.Y + (r.Height - total) / 2;
                dc.DrawText(num, new Point(r.X + (r.Width - num.Width) / 2, top));
                if (size.Width < r.Width - 8) dc.DrawText(size, new Point(r.X + (r.Width - size.Width) / 2, top + num.Height + 4));
            }
            else
            {
                var num = new FormattedText((i + 1).ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face, 13, Ui.Text, dpi);
                dc.DrawText(num, new Point(r.X + (r.Width - num.Width) / 2, r.Y + (r.Height - num.Height) / 2));
            }
            if (r.Width > 60 && r.Height > 50)
            {
                var c = CloseBox(r);
                dc.DrawEllipse(Ui.SurfaceHover, null, new Point(c.X + 12, c.Y + 12), 12, 12);
                var pen = new Pen(Ui.Muted, 1.6);
                dc.DrawLine(pen, new Point(c.X + 8, c.Y + 8), new Point(c.X + 16, c.Y + 16));
                dc.DrawLine(pen, new Point(c.X + 16, c.Y + 8), new Point(c.X + 8, c.Y + 16));
            }
        }
    }

    // Topmost zone under the point and which edges are grabbed.
    (int index, Edges edges, bool close)? Hit(Point p)
    {
        var a = Area();
        for (int i = Zones.Count - 1; i >= 0; i--)
        {
            var r = ToScreen(Zones[i], a);
            r.Inflate(-1.5, -1.5);
            if (!r.Contains(p)) continue;
            if (r.Width > 60 && r.Height > 50 && CloseBox(r).Contains(p)) return (i, Edges.None, true);
            var e = Edges.None;
            bool nearL = p.X - r.Left < CornerPx, nearR = r.Right - p.X < CornerPx, nearT = p.Y - r.Top < CornerPx, nearB = r.Bottom - p.Y < CornerPx;
            if ((nearL || nearR) && (nearT || nearB))
            {
                e |= nearL ? Edges.Left : Edges.Right;
                e |= nearT ? Edges.Top : Edges.Bottom;
            }
            else
            {
                if (p.X - r.Left < EdgePx) e |= Edges.Left;
                else if (r.Right - p.X < EdgePx) e |= Edges.Right;
                if (p.Y - r.Top < EdgePx) e |= Edges.Top;
                else if (r.Bottom - p.Y < EdgePx) e |= Edges.Bottom;
            }
            return (i, e, false);
        }
        return null;
    }

    static Cursor CursorFor(Edges e) => e switch
    {
        Edges.Left or Edges.Right => Cursors.SizeWE,
        Edges.Top or Edges.Bottom => Cursors.SizeNS,
        Edges.Left | Edges.Top or Edges.Right | Edges.Bottom => Cursors.SizeNWSE,
        Edges.Right | Edges.Top or Edges.Left | Edges.Bottom => Cursors.SizeNESW,
        _ => Cursors.SizeAll,
    };

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        Focus();
        var p = e.GetPosition(this);
        var hit = Hit(p);
        if (hit == null) { Select(null); return; }
        var (i, edges, close) = hit.Value;
        if (close) { Zones.RemoveAt(i); Select(null); return; }
        if (e.ClickCount == 2) { Split(i); return; }
        Select(i);
        dragIndex = i; dragEdges = edges; dragOrigin = p; dragStart = Zones[i];
        CaptureMouse();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var p = e.GetPosition(this);
        if (dragIndex is int i && e.LeftButton == MouseButtonState.Pressed && i < Zones.Count)
        {
            var a = Area();
            double dx = (p.X - dragOrigin.X) / a.Width, dy = (p.Y - dragOrigin.Y) / a.Height;
            Zones[i] = Resized(dragStart, i, dragEdges, dx, dy);
            InvalidateVisual();
            return;
        }
        var hit = Hit(p);
        Cursor = hit == null ? Cursors.Arrow : hit.Value.close ? Cursors.Hand : CursorFor(hit.Value.edges);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        dragIndex = null;
        ReleaseMouseCapture();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Delete || e.Key == Key.Back) { RemoveSelected(); e.Handled = true; }
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo info) { base.OnRenderSizeChanged(info); InvalidateVisual(); }

    ZRect Resized(ZRect r, int i, Edges edges, double dx, double dy)
    {
        double l = r.X, t = r.Y, rr = r.Right, b = r.Bottom;
        if (edges == Edges.None)
        {
            double w = r.W, h = r.H;
            l = Clamp(l + dx, 0, 1 - w);
            t = Clamp(t + dy, 0, 1 - h);
            double byLeft = Snap(l, i, true), byRight = Snap(l + w, i, true) - w;
            l = Clamp(Math.Abs(byLeft - l) <= Math.Abs(byRight - l) ? byLeft : byRight, 0, 1 - w);
            double byTop = Snap(t, i, false), byBottom = Snap(t + h, i, false) - h;
            t = Clamp(Math.Abs(byTop - t) <= Math.Abs(byBottom - t) ? byTop : byBottom, 0, 1 - h);
            rr = l + w; b = t + h;
        }
        else
        {
            if (edges.HasFlag(Edges.Left)) l = Math.Min(Snap(Clamp(l + dx, 0, 1), i, true), rr - MinSize);
            if (edges.HasFlag(Edges.Right)) rr = Math.Max(Snap(Clamp(rr + dx, 0, 1), i, true), l + MinSize);
            if (edges.HasFlag(Edges.Top)) t = Math.Min(Snap(Clamp(t + dy, 0, 1), i, false), b - MinSize);
            if (edges.HasFlag(Edges.Bottom)) b = Math.Max(Snap(Clamp(b + dy, 0, 1), i, false), t + MinSize);
        }
        return new ZRect(l, t, rr - l, b - t);
    }

    double Snap(double v, int self, bool horizontal)
    {
        var candidates = new List<double> { 0, 1 };
        for (int j = 0; j < Zones.Count; j++)
        {
            if (j == self) continue;
            var z = Zones[j];
            if (horizontal) { candidates.Add(z.X); candidates.Add(z.Right); }
            else { candidates.Add(z.Y); candidates.Add(z.Bottom); }
        }
        foreach (var c in candidates) if (Math.Abs(v - c) < SnapDistance) return c;
        return Math.Round(v * 100) / 100;
    }

    static double Clamp(double v, double lo, double hi) => Math.Max(lo, Math.Min(hi, v));

    // Left/right, or top/bottom with Shift.
    void Split(int i)
    {
        var z = Zones[i];
        ZRect a, b;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            if (z.H / 2 < MinSize) return;
            a = new ZRect(z.X, z.Y, z.W, z.H / 2);
            b = new ZRect(z.X, z.MidY, z.W, z.H / 2);
        }
        else
        {
            if (z.W / 2 < MinSize) return;
            a = new ZRect(z.X, z.Y, z.W / 2, z.H);
            b = new ZRect(z.MidX, z.Y, z.W / 2, z.H);
        }
        Zones[i] = a;
        Zones.Insert(i + 1, b);
        Select(i + 1);
    }
}
