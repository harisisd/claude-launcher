using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ClaudeLauncher;

public sealed class MainWindow : Window
{
    readonly AppModel model;
    readonly StackPanel list = new();
    readonly TextBlock stats = Ui.T("", 12, Ui.Muted);
    readonly Button arrangeBtn;
    static readonly CultureInfo Id = new("id-ID");

    public MainWindow(AppModel model)
    {
        this.model = model;
        Title = "Claude Launcher";
        Width = 720; Height = 600; MinWidth = 640; MinHeight = 440;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Ui.Bg;
        try { Icon = System.Windows.Media.Imaging.BitmapFrame.Create(new Uri("pack://application:,,,/AppIcon.ico")); } catch { }
        Ui.DarkTitleBar(this);

        var root = new DockPanel();

        // Header
        var header = new DockPanel { Margin = new Thickness(24, 20, 24, 16), LastChildFill = true };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        Button menuBtn = null!;
        menuBtn = Ui.IconBtn("", () => Ui.OpenMenu(AppMenu(), menuBtn), "Preset, tutup semua, dan pengaturan");
        var gear = Ui.IconBtn("", OpenLayoutEditor, "Atur tata letak jendela");
        arrangeBtn = Ui.Btn("", "Secondary", () => _ = model.OpenAndArrange(), "Buka instance yang dipilih lalu susun jendelanya");
        var add = Ui.Btn("+  Tambah", "Primary", AddProfile);
        foreach (var b in new[] { menuBtn, gear, arrangeBtn, add }) { b.Margin = new Thickness(8, 0, 0, 0); actions.Children.Add(b); }
        DockPanel.SetDock(actions, Dock.Right);
        header.Children.Add(actions);
        var titles = new StackPanel();
        var title = Ui.T("Claude Launcher", 26, null, FontWeights.SemiBold);
        titles.Children.Add(title);
        stats.Margin = new Thickness(0, 4, 0, 0);
        titles.Children.Add(stats);
        header.Children.Add(titles);
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        // Footer hint
        var hint = Ui.T("Klik dua kali kartu untuk membuka  ·  Klik kanan untuk opsi", 12, Ui.Muted);
        hint.HorizontalAlignment = HorizontalAlignment.Center;
        hint.Margin = new Thickness(0, 10, 0, 10);
        DockPanel.SetDock(hint, Dock.Bottom);
        root.Children.Add(hint);

        list.Margin = new Thickness(24, 4, 24, 4);
        root.Children.Add(new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = false });

        Content = root;
        model.Changed += Render;
        Closed += (_, _) =>
        {
            model.Changed -= Render;
            if (!App.Quitting) ((App)Application.Current).MainClosed();
        };
        Render();
    }

    void Render()
    {
        stats.Text = $"{model.Profiles.Count} instance  ·  {model.Running.Count} berjalan  ·  {model.ActiveCount} aktif";
        arrangeBtn.Content = model.ArrangeStatus ?? $"Buka & rapikan ({model.ActiveCount})";
        arrangeBtn.IsEnabled = model.ArrangeStatus == null;
        list.Children.Clear();
        foreach (var p in model.Profiles) list.Children.Add(ProfileCard(p));
    }

    // MARK: actions

    void AddProfile()
    {
        var template = model.Profiles.FirstOrDefault(p => p.IsTemplate);
        Dialogs.Name(this, "Tambah instance",
            "Beri nama instance baru. Login dan datanya tersimpan terpisah dari instance lain." +
            (template != null ? $"\n\nSetup disalin otomatis dari template \"{template.Name}\"." : ""),
            "", "Tambah",
            v => v.Length == 0 ? "" : "Folder data: ~\\.claude-launcher\\profiles\\" + model.NewId(v),
            model.Add);
    }

    void OpenLayoutEditor() => new LayoutEditor(this, model).ShowDialog();

    void SavePresetPrompt()
    {
        var names = string.Join(", ", model.Profiles.Where(model.Layout.Includes).Select(p => p.Name));
        Dialogs.Name(this, "Simpan preset",
            $"Simpan tata letak sekarang sebagai preset: {model.ActiveCount} instance aktif ({names}), layout, monitor, dan jarak.\nNama yang sama akan menimpa preset lama.",
            "", "Simpan", _ => "", model.SavePreset);
    }

    ContextMenu AppMenu()
    {
        var m = new ContextMenu();
        m.Items.Add(Ui.Item("Preset", null, enabled: false));
        foreach (var p in model.Presets) m.Items.Add(Ui.Item("    " + p.Label, () => _ = model.RunPreset(p)));
        if (model.Presets.Count == 0) m.Items.Add(Ui.Item("    Belum ada preset", null, enabled: false));
        m.Items.Add(Ui.Item("Simpan tata letak sekarang sebagai preset…", SavePresetPrompt));
        if (model.Presets.Count > 0)
        {
            var del = Ui.Item("Hapus preset", null);
            foreach (var p in model.Presets) del.Items.Add(Ui.Item(p.Name, () => model.DeletePreset(p)));
            m.Items.Add(del);
        }
        m.Items.Add(new Separator());
        var closeAll = Ui.Item("Tutup semua instance", model.CloseAll);
        closeAll.Foreground = Ui.Danger;
        m.Items.Add(closeAll);
        m.Items.Add(new Separator());
        var s = model.Settings;
        m.Items.Add(Ui.Toggle("Jalan otomatis saat login", s.AutoStart, model.SetAutoStart));
        m.Items.Add(Ui.Toggle("Buka & rapikan otomatis saat launcher jalan otomatis", s.ArrangeOnStart, v => model.UpdateSettings(x => x.ArrangeOnStart = v)));
        m.Items.Add(Ui.Toggle("Tutup jendela = tetap jalan di system tray", s.CloseToTray, v => model.UpdateSettings(x => x.CloseToTray = v)));
        m.Items.Add(Ui.Toggle("Hotkey global (Ctrl+Alt+1–9, Ctrl+Alt+0)", s.Hotkeys, model.SetHotkeys));
        m.Items.Add(Ui.Item("Daftar hotkey…", model.HotkeyHelp));
        m.Items.Add(new Separator());
        m.Items.Add(Ui.Item("Keluar dari launcher", () => ((App)Application.Current).Quit()));
        return m;
    }

    // MARK: card

    FrameworkElement ProfileCard(Profile p)
    {
        bool running = model.IsRunning(p);
        var grid = new Grid { Height = 96 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Avatar + running dot
        var avatar = new Grid { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 0) };
        avatar.Children.Add(Ui.Avatar(p.Name, p.Color));
        if (running)
            avatar.Children.Add(new Ellipse
            {
                Width = 13, Height = 13, Fill = Ui.Running, Stroke = Ui.Surface, StrokeThickness = 2.5,
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, -2, -2),
            });
        grid.Children.Add(avatar);

        // Text
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var nameRow = new StackPanel { Orientation = Orientation.Horizontal };
        nameRow.Children.Add(Ui.T(p.Name, 14, null, FontWeights.SemiBold));
        if (p.IsTemplate)
        {
            var badge = new Border
            {
                BorderBrush = Ui.Accent, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(9),
                Padding = new Thickness(7, 0, 7, 1), Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center,
                Child = Ui.T("Template", 11, Ui.Accent),
            };
            nameRow.Children.Add(badge);
        }
        text.Children.Add(nameRow);

        var status = new TextBlock { FontSize = 12, Margin = new Thickness(0, 3, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis };
        status.Inlines.Add(new System.Windows.Documents.Run(running ? "Berjalan" : "Tidak berjalan") { Foreground = running ? Ui.Running : Ui.Muted });
        model.Emails.TryGetValue(p.Id, out var email);
        status.Inlines.Add(new System.Windows.Documents.Run("  ·  " + (email ?? (p.IsDefault ? "Belum login" : p.Subtitle)))
            { Foreground = email == null ? Ui.Muted : Ui.Text });
        text.Children.Add(status);
        model.Usages.TryGetValue(p.Id, out var usage);
        var usageRow = UsageRow(usage);
        usageRow.Margin = new Thickness(0, 5, 0, 0);
        text.Children.Add(usageRow);
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);

        // Controls
        var controls = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var sw = Ui.PillSwitch(model.Layout.Includes(p), v => model.SetActive(p, v), "Ikut \"Buka & rapikan\"");
        sw.Margin = new Thickness(8, 0, 16, 0);
        controls.Children.Add(sw);
        var open = Ui.Btn(running ? "Tampilkan" : "Buka", "Primary", () => model.Launch(p));
        open.MinWidth = 96;
        controls.Children.Add(open);
        Button more = null!;
        more = Ui.IconBtn("", () => Ui.OpenMenu(CardMenu(p), more));
        more.Margin = new Thickness(8, 0, 0, 0);
        controls.Children.Add(more);
        Grid.SetColumn(controls, 2);
        grid.Children.Add(controls);

        var card = Ui.Card(grid);
        card.Margin = new Thickness(0, 0, 0, 8);
        card.MouseLeftButtonDown += (_, e) => { if (e.ClickCount == 2) model.Launch(p); };
        card.ContextMenu = CardMenu(p);
        card.ContextMenuOpening += (_, _) => card.ContextMenu = CardMenu(p);
        return card;
    }

    ContextMenu CardMenu(Profile p)
    {
        int index = model.Profiles.IndexOf(p);
        bool running = model.IsRunning(p);
        var m = new ContextMenu();
        m.Items.Add(Ui.Item(running ? "Tampilkan" : "Buka", () => model.Launch(p)));
        m.Items.Add(Ui.Item("Tutup instance", () => model.CloseInstance(p), enabled: running));
        m.Items.Add(new Separator());
        m.Items.Add(Ui.Item("Ganti nama…", () => Dialogs.Name(this, "Ganti nama",
            "Nama tampilan untuk instance ini. Folder data dan login tidak berubah.", p.Name, "Simpan", _ => "",
            name => model.Update(p, x => x.Name = name))));
        m.Items.Add(Ui.Item("Salin setup dari…", () => Dialogs.CopySetup(this, model, p), enabled: model.Profiles.Count >= 2));
        m.Items.Add(Ui.Item(p.IsTemplate ? "Berhenti jadi template" : "Jadikan template", () => model.ToggleTemplate(p)));
        var colors = Ui.Item("Warna", null);
        foreach (var (name, hex) in Palette.Colors)
        {
            var mi = Ui.Item(name, () => model.Update(p, x => x.Color = hex), isChecked: string.Equals(hex, p.Color, StringComparison.OrdinalIgnoreCase));
            mi.Icon = new Ellipse { Width = 12, Height = 12, Fill = Ui.Hex(hex), Margin = new Thickness(-22, 0, 0, 0) };
            colors.Items.Add(mi);
        }
        m.Items.Add(colors);
        m.Items.Add(Ui.Item("Pindah ke atas", () => model.Move(p, -1), enabled: index > 0));
        m.Items.Add(Ui.Item("Pindah ke bawah", () => model.Move(p, 1), enabled: index < model.Profiles.Count - 1));
        m.Items.Add(Ui.Item("Buka folder data", () => model.OpenFolder(p)));
        m.Items.Add(new Separator());
        var remove = Ui.Item("Hapus dari daftar", () =>
        {
            if (Dialogs.Confirm(this, "Hapus dari daftar?", $"\"{p.Name}\" dihapus dari launcher. Folder data dan login tidak dihapus.", "Hapus"))
                model.Remove(p);
        });
        remove.Foreground = Ui.Danger;
        m.Items.Add(remove);
        return m;
    }

    // "5 jam [bar] 24%   Mingguan [bar] 38%   · data 30 Okt"
    static FrameworkElement UsageRow(UsageSnapshot? u)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        if (u == null)
        {
            row.Children.Add(Ui.T("Limit: belum ada data", 11, Ui.Muted));
            row.ToolTip = "Belum ada snapshot limit di cache instance ini.";
            return row;
        }
        if (u.FiveHour != null) row.Children.Add(Meter("5 jam", u.FiveHour));
        if (u.Weekly != null) row.Children.Add(Meter("Mingguan", u.Weekly));
        row.Children.Add(Ui.T("· data " + u.Taken.ToLocalTime().ToString("d MMM", Id), 11, Ui.Muted));

        var lines = new List<string>();
        foreach (var (label, w) in new[] { ("5 jam", u.FiveHour), ("Mingguan", u.Weekly) })
        {
            if (w == null) continue;
            var reset = w.ResetsAt?.ToLocalTime().ToString("ddd, d MMM HH.mm", Id) ?? "?";
            lines.Add(w.HasReset ? $"{label}: sudah reset ({reset})" : $"{label}: {Math.Round((w.Utilization ?? 0) * 100)}%, reset {reset}");
        }
        lines.Add("");
        lines.Add("Snapshot terakhir di cache: " + u.Taken.ToLocalTime().ToString("d MMM yyyy HH.mm", Id));
        lines.Add("Bukan angka live; buka popup limit di Claude untuk angka terkini.");
        row.ToolTip = string.Join("\n", lines);
        return row;
    }

    static FrameworkElement Meter(string label, UsageWindow w)
    {
        double value = w.HasReset ? 0 : Math.Min(1, Math.Max(0, w.Utilization ?? 0));
        var s = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 12, 0) };
        s.Children.Add(Ui.T(label, 11, Ui.Muted));
        var bar = new Grid { Width = 44, Height = 4, Margin = new Thickness(6, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
        bar.Children.Add(new Border { Background = Ui.Border, CornerRadius = new CornerRadius(2) });
        if (value > 0)
            bar.Children.Add(new Border
            {
                Background = value >= 0.9 ? Ui.Danger : Ui.Text, CornerRadius = new CornerRadius(2),
                Width = Math.Max(4, 44 * value), HorizontalAlignment = HorizontalAlignment.Left,
            });
        s.Children.Add(bar);
        s.Children.Add(Ui.T(w.HasReset ? "reset" : $"{Math.Round(value * 100)}%", 11, w.HasReset ? Ui.Muted : Ui.Text));
        return s;
    }
}
