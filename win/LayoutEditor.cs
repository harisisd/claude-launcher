using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ClaudeLauncher;

// Small read-only drawing of zones.
public sealed class ZonePreview : FrameworkElement
{
    public List<ZRect> Zones = new();

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        dc.DrawRectangle(Ui.SurfaceHover, null, new Rect(0, 0, w, h));
        var pen = new Pen(Ui.Border, 1);
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        for (int i = 0; i < Zones.Count; i++)
        {
            var z = Zones[i];
            var r = new Rect(z.X * w + 1, z.Y * h + 1, Math.Max(0, z.W * w - 2), Math.Max(0, z.H * h - 2));
            dc.DrawRectangle(Ui.Bg, pen, r);
            if (r.Width > 16 && r.Height > 16)
            {
                var ft = new FormattedText((i + 1).ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"), 11, Ui.Muted, dpi);
                dc.DrawText(ft, new Point(r.X + (r.Width - ft.Width) / 2, r.Y + (r.Height - ft.Height) / 2));
            }
        }
    }
}

public sealed class LayoutEditor : Window
{
    readonly AppModel model;
    readonly List<MonitorInfo> monitors = Arranger.Monitors();
    string monitorId = "";
    string layoutKey = "grid";
    bool gap = true;
    HashSet<string> ids = new();
    readonly StackPanel monitorBar = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
    readonly StackPanel body = new() { Margin = new Thickness(24, 0, 24, 16) };

    int Count => Math.Max(1, ids.Count);

    (double w, double h) WorkSize
    {
        get
        {
            var m = monitors.FirstOrDefault(x => x.Id == monitorId) ?? monitors.FirstOrDefault();
            return m == null ? (16, 9) : (m.Work.Width, m.Work.Height);
        }
    }

    public LayoutEditor(Window owner, AppModel model)
    {
        this.model = model;
        Owner = owner;
        Title = "Tata letak jendela";
        Width = 1000; Height = 740; MinWidth = 760; MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        Background = Ui.Bg;
        Ui.DarkTitleBar(this);

        var s = model.Layout;
        monitorId = monitors.Any(m => m.Id == s.Monitor) ? s.Monitor : monitors.FirstOrDefault()?.Id ?? "";
        layoutKey = s.Layout;
        gap = s.Gap;
        ids = new HashSet<string>(model.Profiles.Where(s.Includes).Select(p => p.Id));

        var root = new DockPanel();
        var top = new Border { Background = Ui.TopBar, Padding = new Thickness(0, 18, 0, 18), Child = monitorBar };
        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);

        var bottom = new DockPanel { Margin = new Thickness(24, 16, 24, 16) };
        var right = new StackPanel { Orientation = Orientation.Horizontal };
        var create = Ui.Btn("+  Buat layout baru", "Secondary", NewLayout);
        create.Margin = new Thickness(0, 0, 16, 0);
        var cancel = Ui.Btn("Batal", "Secondary", Close);
        cancel.IsCancel = true;
        var save = Ui.Btn("Simpan", "Primary", Save);
        save.IsDefault = true;
        save.Margin = new Thickness(8, 0, 0, 0);
        right.Children.Add(create); right.Children.Add(cancel); right.Children.Add(save);
        DockPanel.SetDock(right, Dock.Right);
        bottom.Children.Add(right);
        bottom.Children.Add(Dialogs.Check("Beri jarak antar jendela", () => gap, v => gap = v));
        DockPanel.SetDock(bottom, Dock.Bottom);
        root.Children.Add(bottom);

        root.Children.Add(new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Content = root;
        Render();
    }

    void Render()
    {
        monitorBar.Children.Clear();
        foreach (var m in monitors)
        {
            var sel = m.Id == monitorId;
            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var num = Ui.T(m.Number.ToString(), 24, sel ? Ui.Accent : Ui.Text, FontWeights.SemiBold); num.HorizontalAlignment = HorizontalAlignment.Center;
            var res = Ui.T($"{m.Frame.Width} × {m.Frame.Height}", 12, sel ? Ui.Accent : Ui.Muted); res.HorizontalAlignment = HorizontalAlignment.Center;
            var nm = Ui.T(m.Name + (m.IsMain ? "  (utama)" : ""), 11, sel ? Ui.Accent : Ui.Muted); nm.HorizontalAlignment = HorizontalAlignment.Center;
            stack.Children.Add(num); stack.Children.Add(res); stack.Children.Add(nm);
            var card = Ui.Card(stack, 20, new Thickness(8));
            card.Width = 190; card.Height = 88; card.Margin = new Thickness(8, 0, 8, 0); card.Cursor = Cursors.Hand;
            Ui.Select(card, sel);
            var id = m.Id;
            card.MouseLeftButtonUp += (_, _) => { monitorId = id; Render(); };
            monitorBar.Children.Add(card);
        }

        body.Children.Clear();
        body.Children.Add(Section("Instance"));
        body.Children.Add(Ui.T("Instance yang dibuka & dirapikan. Nomor zona mengikuti urutan di daftar launcher.", 12, Ui.Muted));
        var checks = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        foreach (var p in model.Profiles)
        {
            var pid = p.Id;
            var c = Ui.CheckRow(p.Name, ids.Contains(pid), v => { if (v) ids.Add(pid); else ids.Remove(pid); Render(); });
            ((FrameworkElement)c).MinWidth = 170;
            checks.Children.Add(c);
        }
        body.Children.Add(checks);

        body.Children.Add(Section("Template"));
        var templates = new WrapPanel();
        for (int i = 0; i < Templates.Keys.Length; i++)
        {
            var key = Templates.Keys[i];
            templates.Children.Add(LayoutCard(Templates.Names[i], Templates.Zones(key, Count), layoutKey == key, () => { layoutKey = key; Render(); }));
        }
        body.Children.Add(templates);

        body.Children.Add(Section("Custom"));
        if (model.Customs.Count == 0)
            body.Children.Add(Ui.T("Belum ada layout custom. Klik \"+ Buat layout baru\" untuk membuat zona sendiri.", 12, Ui.Muted));
        var customs = new WrapPanel();
        foreach (var c in model.Customs.ToList())
        {
            var cc = c;
            customs.Children.Add(LayoutCard(c.Name, c.Zones, layoutKey == Templates.CustomPrefix + c.Name,
                () => { layoutKey = Templates.CustomPrefix + cc.Name; Render(); },
                onEdit: () => Edit(cc, false),
                onDelete: () => Delete(cc)));
        }
        body.Children.Add(customs);
    }

    static TextBlock Section(string title)
    {
        var t = Ui.T(title, 17, null, FontWeights.SemiBold);
        t.Margin = new Thickness(0, 16, 0, 8);
        return t;
    }

    FrameworkElement LayoutCard(string title, List<ZRect> zones, bool selected, Action onSelect, Action? onEdit = null, Action? onDelete = null)
    {
        var stack = new StackPanel();
        var head = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
        if (onDelete != null) head.Children.Add(Glyph("", "Hapus", onDelete));
        if (onEdit != null) head.Children.Add(Glyph("", "Edit", onEdit));
        foreach (UIElement el in head.Children) DockPanel.SetDock(el, Dock.Right);
        head.Children.Add(Ui.T($"{title}  ·  {zones.Count} zona", 13, selected ? Ui.Accent : Ui.Text, FontWeights.SemiBold));
        stack.Children.Add(head);
        stack.Children.Add(new ZonePreview { Zones = zones, Height = 100 });
        var card = Ui.Card(stack, 20, new Thickness(16));
        card.Width = 210; card.Margin = new Thickness(0, 0, 12, 12); card.Cursor = Cursors.Hand;
        Ui.Select(card, selected);
        card.MouseLeftButtonUp += (_, _) => onSelect();
        return card;
    }

    static Button Glyph(string glyph, string tip, Action a)
    {
        var b = new Button
        {
            Style = (Style)Application.Current.Resources["Bare"], ToolTip = tip, Margin = new Thickness(8, 0, 0, 0),
            Content = new TextBlock { Text = glyph, FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 13, Foreground = Ui.Muted },
        };
        b.Click += (_, e) => { e.Handled = true; a(); };
        return b;
    }

    void NewLayout()
    {
        int n = model.Customs.Count + 1;
        while (model.Customs.Any(c => c.Name == $"Custom layout {n}")) n++;
        Edit(new ZoneLayout { Name = $"Custom layout {n}", Zones = Templates.Resolve(layoutKey, Count, model.Customs) }, true);
    }

    void Edit(ZoneLayout layout, bool isNew)
    {
        var editor = new ZoneEditor(this, model, layout.Clone(), WorkSize);
        if (editor.ShowDialog() != true || editor.Result == null) return;
        var result = editor.Result;
        if (isNew)
        {
            model.Customs.Add(result);
            layoutKey = Templates.CustomPrefix + result.Name;
        }
        else
        {
            int i = model.Customs.FindIndex(c => c.Id == result.Id);
            if (i >= 0)
            {
                bool wasSelected = layoutKey == Templates.CustomPrefix + model.Customs[i].Name;
                model.Customs[i] = result;
                if (wasSelected) layoutKey = Templates.CustomPrefix + result.Name;
            }
        }
        model.SaveCustoms();
        Render();
    }

    void Delete(ZoneLayout d)
    {
        if (!Dialogs.Confirm(this, "Hapus layout?", $"Layout \"{d.Name}\" akan dihapus.", "Hapus")) return;
        if (layoutKey == Templates.CustomPrefix + d.Name) layoutKey = "grid";
        model.Customs.RemoveAll(c => c.Id == d.Id);
        model.SaveCustoms();
        Render();
    }

    void Save()
    {
        model.Layout.Monitor = monitorId;
        model.Layout.Layout = layoutKey;
        model.Layout.Gap = gap;
        model.Layout.Ids = model.Profiles.Where(p => ids.Contains(p.Id)).Select(p => p.Id).ToList();
        model.SaveLayout();
        model.Notify();
        Close();
    }
}
