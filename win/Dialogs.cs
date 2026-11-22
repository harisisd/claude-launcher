using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ClaudeLauncher;

public static class Dialogs
{
    // Checkbox row that re-renders itself when toggled.
    public static ContentControl Check(string title, Func<bool> get, Action<bool> set)
    {
        var host = new ContentControl();
        void Render() => host.Content = Ui.CheckRow(title, get(), v => { set(v); Render(); });
        Render();
        return host;
    }

    static StackPanel Buttons(params Button[] buttons)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        foreach (var b in buttons) { b.Margin = new Thickness(8, 0, 0, 0); row.Children.Add(b); }
        return row;
    }

    public static void Alert(Window? owner, string title, string message)
    {
        var w = Ui.Dialog(owner, title, 440);
        var stack = new StackPanel { Margin = new Thickness(24) };
        stack.Children.Add(Ui.T(title, 15, null, FontWeights.SemiBold));
        var msg = Ui.Wrap(message, 13);
        msg.Margin = new Thickness(0, 10, 0, 0);
        stack.Children.Add(msg);
        var ok = Ui.Btn("OK", "Primary", () => w.Close());
        ok.IsDefault = true; ok.IsCancel = true;
        stack.Children.Add(Buttons(ok));
        w.Content = stack;
        w.ShowDialog();
    }

    public static bool Confirm(Window? owner, string title, string message, string okText)
    {
        bool result = false;
        var w = Ui.Dialog(owner, title, 440);
        var stack = new StackPanel { Margin = new Thickness(24) };
        stack.Children.Add(Ui.T(title, 15, null, FontWeights.SemiBold));
        var msg = Ui.Wrap(message, 13);
        msg.Margin = new Thickness(0, 10, 0, 0);
        stack.Children.Add(msg);
        var cancel = Ui.Btn("Batal", "Secondary", () => w.Close());
        cancel.IsCancel = true;
        var ok = Ui.Btn(okText, "DangerBtn", () => { result = true; w.Close(); });
        ok.IsDefault = true;
        stack.Children.Add(Buttons(cancel, ok));
        w.Content = stack;
        w.ShowDialog();
        return result;
    }

    // Name prompt with validation and an optional live hint.
    public static void Name(Window owner, string title, string message, string initial, string okText,
                            Func<string, string> hint, Action<string> onSubmit, Func<string, string?>? validate = null)
    {
        validate ??= AppModel.ValidateName;
        var w = Ui.Dialog(owner, title, 440);
        var stack = new StackPanel { Margin = new Thickness(24) };
        stack.Children.Add(Ui.T(title, 15, null, FontWeights.SemiBold));
        var msg = Ui.Wrap(message, 13);
        msg.Margin = new Thickness(0, 10, 0, 12);
        stack.Children.Add(msg);
        var field = new TextBox { Style = (Style)Application.Current.Resources["Field"], Text = initial };
        stack.Children.Add(field);
        var info = Ui.T("", 12, Ui.Muted);
        info.Margin = new Thickness(4, 8, 0, 0);
        info.Height = 18;
        stack.Children.Add(info);
        void UpdateHint() { info.Foreground = Ui.Muted; info.Text = hint(field.Text.Trim()); }
        field.TextChanged += (_, _) => UpdateHint();
        UpdateHint();

        void Submit()
        {
            var value = field.Text.Trim();
            var e = validate(value);
            if (e != null) { info.Text = e; info.Foreground = Ui.Danger; return; }
            w.Close();
            onSubmit(value);
        }
        var cancel = Ui.Btn("Batal", "Secondary", () => w.Close());
        cancel.IsCancel = true;
        var ok = Ui.Btn(okText, "Primary", Submit);
        ok.IsDefault = true;
        stack.Children.Add(Buttons(cancel, ok));
        w.Content = stack;
        w.Loaded += (_, _) => { field.Focus(); field.SelectAll(); };
        w.ShowDialog();
    }

    public static void CopySetup(Window owner, AppModel model, Profile target)
    {
        var sources = model.Profiles.Where(p => p.Id != target.Id).ToList();
        var source = sources.FirstOrDefault(p => p.IsTemplate) ?? sources.FirstOrDefault(p => p.IsDefault) ?? sources.FirstOrDefault();
        var o = new CopyOptions();
        var w = Ui.Dialog(owner, "Salin setup", 480);
        var stack = new StackPanel { Margin = new Thickness(24) };
        stack.Children.Add(Ui.T($"Salin setup ke \"{target.Name}\"", 15, null, FontWeights.SemiBold));

        var lbl1 = Ui.T("Ambil dari", 12, Ui.Muted); lbl1.Margin = new Thickness(0, 14, 0, 4);
        stack.Children.Add(lbl1);
        var sourceList = new WrapPanel();
        void RenderSources()
        {
            sourceList.Children.Clear();
            foreach (var p in sources)
            {
                var pp = p;
                sourceList.Children.Add(Ui.CheckRow(p.IsTemplate ? p.Name + "  (template)" : p.Name, source == p, _ => { source = pp; RenderSources(); }));
            }
        }
        RenderSources();
        stack.Children.Add(sourceList);

        var lbl2 = Ui.T("Yang disalin", 12, Ui.Muted); lbl2.Margin = new Thickness(0, 14, 0, 4);
        stack.Children.Add(lbl2);
        stack.Children.Add(Check("MCP server (mode chat)", () => o.Mcp, v => o.Mcp = v));
        stack.Children.Add(Check("Preferensi aplikasi (Cowork, browser, izin, dll.)", () => o.Prefs, v => o.Prefs = v));
        stack.Children.Add(Check("Status on/off tool MCP", () => o.ToolToggles, v => o.ToolToggles = v));
        stack.Children.Add(Check("Preset sesi Claude Code", () => o.Presets, v => o.Presets = v));
        stack.Children.Add(Check("Extension", () => o.Extensions, v => o.Extensions = v));
        var note = Ui.Wrap("Login tidak ikut disalin. File lama di tujuan dibackup ke folder launcher-backup.\nSkill, memory, CLAUDE.md dan MCP Claude Code (%USERPROFILE%\\.claude) sudah sama di semua instance.", 12, Ui.Muted);
        note.Margin = new Thickness(0, 12, 0, 0);
        stack.Children.Add(note);

        var cancel = Ui.Btn("Batal", "Secondary", () => w.Close());
        cancel.IsCancel = true;
        var ok = Ui.Btn("Salin", "Primary", () =>
        {
            if (source == null) return;
            w.Close();
            model.CopySetup(source, target, o);
        });
        ok.IsDefault = true;
        stack.Children.Add(Buttons(cancel, ok));
        w.Content = stack;
        w.ShowDialog();
    }
}
