using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using Forms = System.Windows.Forms;

namespace ClaudeLauncher;

// Global hotkeys Ctrl+Alt+1...9 (open instance N) and Ctrl+Alt+0 (open & arrange).
public sealed class HotkeyCenter
{
    public static readonly HotkeyCenter Shared = new();
    public const int ArrangeId = 0;
    public Action<int>? Handler;
    public readonly List<int> Failed = new();
    HwndSource? source;

    public void Apply(bool on)
    {
        Unregister();
        if (!on) return;
        if (source == null)
        {
            source = new HwndSource(new HwndSourceParameters("ClaudeLauncherHotkeys") { Width = 0, Height = 0, ParentWindow = new IntPtr(-3) /* HWND_MESSAGE */ });
            source.AddHook((IntPtr hwnd, int msg, IntPtr w, IntPtr l, ref bool handled) =>
            {
                if (msg == Native.WM_HOTKEY) { Handler?.Invoke(w.ToInt32()); handled = true; }
                return IntPtr.Zero;
            });
        }
        for (int n = 0; n <= 9; n++)
            if (!Native.RegisterHotKey(source.Handle, n, Native.MOD_CONTROL | Native.MOD_ALT | Native.MOD_NOREPEAT, (uint)('0' + n)))
                Failed.Add(n);
    }

    void Unregister()
    {
        Failed.Clear();
        if (source == null) return;
        for (int n = 0; n <= 9; n++) Native.UnregisterHotKey(source.Handle, n);
    }
}

public partial class App : Application
{
    const string MutexName = "ClaudeLauncher.SingleInstance";
    const string ShowEventName = "ClaudeLauncher.Show";
    Mutex? mutex;
    EventWaitHandle? showEvent;
    Forms.NotifyIcon? tray;
    public AppModel Model = null!;
    MainWindow? window;
    public static bool Quitting;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        mutex = new Mutex(true, MutexName, out bool first);
        if (!first)
        {
            // Already running: bring the existing window to front instead.
            try { EventWaitHandle.OpenExisting(ShowEventName).Set(); } catch { }
            Shutdown();
            return;
        }
        showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        new Thread(() =>
        {
            while (showEvent.WaitOne()) Dispatcher.Invoke(ShowMain);
        }) { IsBackground = true }.Start();

        Model = new AppModel(Dispatcher);
        Model.ShowAlert = (t, m) => Dialogs.Alert(window != null && window.IsVisible ? window : null, t, m);
        Model.AskConfirm = (t, m, ok) => Dialogs.Confirm(window != null && window.IsVisible ? window : null, t, m, ok);

        HotkeyCenter.Shared.Handler = id =>
        {
            if (id == HotkeyCenter.ArrangeId) _ = Model.OpenAndArrange();
            else if (id >= 1 && id <= Model.Profiles.Count) Model.Launch(Model.Profiles[id - 1]);
        };
        HotkeyCenter.Shared.Apply(Model.Settings.Hotkeys);

        SetupTray();

        // Started at login: stay in the tray instead of opening the window.
        if (!(Model.Autostarted && Model.Settings.CloseToTray)) ShowMain();
    }

    public void ShowMain()
    {
        if (window == null)
        {
            window = new MainWindow(Model);
            window.Closed += (_, _) => window = null;
        }
        window.Show();
        if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
        window.Activate();
        window.Topmost = true;
        window.Topmost = false;
        window.Focus();
    }

    // Main window closed: keep running in the tray, or quit.
    public void MainClosed()
    {
        if (!Model.Settings.CloseToTray) Quit();
    }

    public void Quit()
    {
        Quitting = true;
        HotkeyCenter.Shared.Apply(false);
        if (tray != null) { tray.Visible = false; tray.Dispose(); }
        window?.Close();
        Shutdown();
    }

    void SetupTray()
    {
        System.Drawing.Icon icon;
        try
        {
            var res = GetResourceStream(new Uri("pack://application:,,,/AppIcon.ico"));
            icon = new System.Drawing.Icon(res!.Stream, Forms.SystemInformation.SmallIconSize);
        }
        catch { icon = System.Drawing.SystemIcons.Application; }

        tray = new Forms.NotifyIcon { Icon = icon, Text = "Claude Launcher", Visible = true };
        tray.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) ShowMain(); };
        tray.MouseUp += (_, e) =>
        {
            if (e.Button != Forms.MouseButtons.Right) return;
            var menu = BuildTrayMenu();
            // A hidden owner window lets the WPF menu take focus and close when clicking elsewhere.
            var host = new Window { Width = 0, Height = 0, WindowStyle = WindowStyle.None, ShowInTaskbar = false, AllowsTransparency = true, Background = null, Left = -10000, Top = -10000 };
            host.Show();
            host.Activate();
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            menu.Closed += (_, _) => host.Close();
            menu.IsOpen = true;
        };
    }

    System.Windows.Controls.ContextMenu BuildTrayMenu()
    {
        var m = new System.Windows.Controls.ContextMenu();
        m.Items.Add(Ui.Item("Buka Claude Launcher", ShowMain));
        m.Items.Add(new System.Windows.Controls.Separator());
        m.Items.Add(Ui.Item("Instance", null, enabled: false));
        for (int i = 0; i < Model.Profiles.Count; i++)
        {
            var p = Model.Profiles[i];
            var label = (Model.IsRunning(p) ? "●  " : "○  ") + p.Name + (i < 9 ? $"    Ctrl+Alt+{i + 1}" : "");
            m.Items.Add(Ui.Item(label, () => Model.Launch(p)));
        }
        m.Items.Add(new System.Windows.Controls.Separator());
        m.Items.Add(Ui.Item($"Buka & rapikan ({Model.ActiveCount})", () => _ = Model.OpenAndArrange()));
        if (Model.Presets.Count > 0)
        {
            var sub = Ui.Item("Preset", null);
            foreach (var p in Model.Presets) sub.Items.Add(Ui.Item(p.Label, () => _ = Model.RunPreset(p)));
            m.Items.Add(sub);
        }
        m.Items.Add(Ui.Item("Tutup semua instance", Model.CloseAll));
        m.Items.Add(new System.Windows.Controls.Separator());
        m.Items.Add(Ui.Item("Keluar", Quit));
        return m;
    }
}
