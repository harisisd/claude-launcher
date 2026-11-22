using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Threading;
using Microsoft.Win32;

namespace ClaudeLauncher;

public sealed class AppModel
{
    public List<Profile> Profiles = new();
    public Dictionary<string, int> Running = new();          // profile id -> pid
    public Dictionary<string, string> Emails = new();        // profile id -> signed-in email
    public Dictionary<string, UsageSnapshot> Usages = new(); // profile id -> last cached limit snapshot
    public LayoutSettings Layout = new();
    public List<ZoneLayout> Customs = new();
    public AppSettings Settings = new();
    public List<Preset> Presets = new();
    public string? ArrangeStatus;

    // UI hooks
    public event Action? Changed;
    public Action<string, string>? ShowAlert;
    public Func<string, string, string, bool>? AskConfirm;

    public readonly bool Autostarted = Environment.GetCommandLineArgs().Contains("--autostart");

    readonly string profilesPath = Path.Combine(Paths.Root, "profiles.json");
    readonly string layoutPath = Path.Combine(Paths.Root, "layout.json");
    readonly string customsPath = Path.Combine(Paths.Root, "layouts.json");
    readonly string settingsPath = Path.Combine(Paths.Root, "settings.json");
    readonly string presetsPath = Path.Combine(Paths.Root, "presets.json");
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string RunValue = "ClaudeLauncher";

    readonly Dispatcher ui;
    readonly DispatcherTimer timer;
    bool refreshing;

    public AppModel(Dispatcher dispatcher)
    {
        ui = dispatcher;
        Load();
        Refresh();
        timer = new DispatcherTimer(TimeSpan.FromSeconds(2.5), DispatcherPriority.Background, (_, _) => Refresh(), ui);
        timer.Start();
        if (Autostarted && Settings.ArrangeOnStart)
            ui.InvokeAsync(async () => { await Task.Delay(4000); await OpenAndArrange(); });
    }

    public void Notify() => Changed?.Invoke();
    void Alert(string title, string message) => ShowAlert?.Invoke(title, message);
    bool Confirm(string title, string message, string ok) => AskConfirm?.Invoke(title, message, ok) ?? true;

    // MARK: persistence

    void Load()
    {
        var list = Json.Read<List<Profile>>(profilesPath);
        if (list != null && list.Count > 0) Profiles = list;
        else
        {
            Profiles = new List<Profile> { new() { Id = Profile.DefaultId, Name = "Utama", Color = Palette.Colors[0].Hex } };
            SaveProfiles();
        }
        Layout = Json.Read<LayoutSettings>(layoutPath) ?? new LayoutSettings();
        Customs = Json.Read<List<ZoneLayout>>(customsPath) ?? new List<ZoneLayout>();
        Settings = Json.Read<AppSettings>(settingsPath) ?? new AppSettings();
        Presets = Json.Read<List<Preset>>(presetsPath) ?? new List<Preset>();
        Settings.AutoStart = AutoStartRegistered();
    }

    public void SaveProfiles() => Json.Write(profilesPath, Profiles);
    public void SaveLayout() => Json.Write(layoutPath, Layout);
    public void SaveCustoms() => Json.Write(customsPath, Customs);
    void SaveSettings() => Json.Write(settingsPath, Settings);

    // MARK: status

    public void Refresh()
    {
        if (refreshing) return;
        refreshing = true;
        var snapshot = Profiles.ToList();
        Task.Run(() =>
        {
            var procs = Claude.RunningMains();
            var map = new Dictionary<string, int>();
            var mail = new Dictionary<string, string>();
            var usage = new Dictionary<string, UsageSnapshot>();
            foreach (var p in snapshot)
            {
                if (procs.TryGetValue(Claude.Key(p), out var pid)) map[p.Id] = pid;
                try { var e = AccountInfo.Email(p.DataDir); if (e != null) mail[p.Id] = e; } catch { }
                try { var u = UsageInfo.Latest(p.DataDir); if (u != null) usage[p.Id] = u; } catch { }
            }
            return (map, mail, usage);
        }).ContinueWith(t =>
        {
            refreshing = false;
            if (t.IsFaulted) return;
            var (map, mail, usage) = t.Result;
            bool changed = !Same(map, Running) || !Same(mail, Emails) || !SameUsage(usage, Usages);
            Running = map; Emails = mail; Usages = usage;
            if (changed) Notify();
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    static bool Same<T>(Dictionary<string, T> a, Dictionary<string, T> b) =>
        a.Count == b.Count && a.All(kv => b.TryGetValue(kv.Key, out var v) && Equals(v, kv.Value));

    static bool SameUsage(Dictionary<string, UsageSnapshot> a, Dictionary<string, UsageSnapshot> b) =>
        a.Count == b.Count && a.All(kv => b.TryGetValue(kv.Key, out var v) && v.Taken == kv.Value.Taken);

    public bool IsRunning(Profile p) => Running.ContainsKey(p.Id);
    public int ActiveCount => Profiles.Count(Layout.Includes);

    // The card switch and the layout editor's instance checks share Layout.Ids.
    public void SetActive(Profile p, bool active)
    {
        var ids = new HashSet<string>(Layout.Ids ?? Profiles.Select(x => x.Id).ToList());
        if (active) ids.Add(p.Id); else ids.Remove(p.Id);
        Layout.Ids = Profiles.Select(x => x.Id).Where(ids.Contains).ToList();
        SaveLayout();
        Notify();
    }

    // MARK: settings

    public void UpdateSettings(Action<AppSettings> change)
    {
        change(Settings);
        SaveSettings();
        Notify();
    }

    static bool AutoStartRegistered()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(RunValue) != null;
        }
        catch { return false; }
    }

    public void SetAutoStart(bool on)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (on) key.SetValue(RunValue, $"\"{Environment.ProcessPath}\" --autostart");
            else key.DeleteValue(RunValue, false);
            UpdateSettings(s => s.AutoStart = on);
        }
        catch (Exception e) { Alert("Gagal mengubah", e.Message); }
    }

    public void SetHotkeys(bool on)
    {
        UpdateSettings(s => s.Hotkeys = on);
        HotkeyCenter.Shared.Apply(on);
    }

    public void HotkeyHelp()
    {
        var lines = new List<string> { "Ctrl+Alt+0  →  Buka & rapikan" };
        for (int i = 0; i < Math.Min(9, Profiles.Count); i++) lines.Add($"Ctrl+Alt+{i + 1}  →  {Profiles[i].Name}");
        lines.Add("");
        lines.Add(Settings.Hotkeys ? "Hotkey sedang aktif. Nomor mengikuti urutan daftar." : "Hotkey sedang nonaktif. Aktifkan dari menu ≡.");
        if (HotkeyCenter.Shared.Failed.Count > 0)
            lines.Add("Gagal didaftarkan (dipakai aplikasi lain): " + string.Join(", ", HotkeyCenter.Shared.Failed.Select(n => "Ctrl+Alt+" + n)));
        Alert("Hotkey global", string.Join("\n", lines));
    }

    // MARK: closing

    public void CloseInstance(Profile p)
    {
        if (!Running.TryGetValue(p.Id, out var pid)) return;
        if (!Confirm("Tutup instance", $"Tutup \"{p.Name}\"? Claude di instance ini akan keluar.", "Tutup")) return;
        Task.Run(() => Claude.Close(p, pid)).ContinueWith(_ => Refresh(), TaskScheduler.FromCurrentSynchronizationContext());
    }

    public void CloseAll()
    {
        var targets = Profiles.Where(IsRunning).ToList();
        if (targets.Count == 0) { Alert("Tidak ada yang berjalan", "Belum ada instance yang sedang berjalan."); return; }
        var names = string.Join("\n", targets.Select(t => "• " + t.Name));
        if (!Confirm("Tutup semua instance", $"Tutup {targets.Count} instance yang sedang berjalan?\n\n" + names, "Tutup semua")) return;
        var pairs = targets.Select(t => (t, Running[t.Id])).ToList();
        Task.Run(() => Parallel.ForEach(pairs, x => Claude.Close(x.t, x.Item2)))
            .ContinueWith(_ => Refresh(), TaskScheduler.FromCurrentSynchronizationContext());
    }

    // MARK: presets

    public void SavePreset(string name)
    {
        var active = Profiles.Where(Layout.Includes).Select(p => p.Id).ToList();
        Presets.RemoveAll(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        Presets.Add(new Preset { Name = name, Layout = Layout.Layout, Monitor = Layout.Monitor, Gap = Layout.Gap, Ids = active });
        Json.Write(presetsPath, Presets);
        Notify();
    }

    public void DeletePreset(Preset p)
    {
        if (!Confirm("Hapus preset", $"Hapus preset \"{p.Name}\"?", "Hapus")) return;
        Presets.RemoveAll(x => x.Id == p.Id);
        Json.Write(presetsPath, Presets);
        Notify();
    }

    public async Task RunPreset(Preset p)
    {
        Layout.Layout = p.Layout;
        Layout.Monitor = p.Monitor;
        Layout.Gap = p.Gap;
        Layout.Ids = Profiles.Select(x => x.Id).Where(p.Ids.Contains).ToList();
        SaveLayout();
        Notify();
        await OpenAndArrange();
    }

    // MARK: profiles

    public string NewId(string name)
    {
        var slug = Regex.Replace(name.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        var b = slug.Length == 0 ? "instance" : slug;
        string id = b; int n = 2;
        while (id == Profile.DefaultId || Profiles.Any(p => p.Id == id)) { id = $"{b}-{n}"; n++; }
        return id;
    }

    public static string? ValidateName(string name)
    {
        if (name.Length == 0) return "Nama tidak boleh kosong.";
        if (name.Length > 40) return "Maksimal 40 karakter.";
        return null;
    }

    public void Add(string name)
    {
        var profile = new Profile { Id = NewId(name), Name = name, Color = Palette.Colors[Profiles.Count % Palette.Colors.Length].Hex };
        Profiles.Add(profile);
        SaveProfiles();
        var template = Profiles.FirstOrDefault(p => p.IsTemplate);
        if (template != null)
        {
            try { SetupCopier.Copy(template, profile, new CopyOptions()); }
            catch (Exception e) { Alert("Template gagal disalin", $"Instance \"{name}\" sudah dibuat, tapi setup dari template gagal disalin.\n\n{e.Message}"); }
        }
        Notify();
        Refresh();
    }

    public void Update(Profile p, Action<Profile> change)
    {
        change(p);
        SaveProfiles();
        Notify();
    }

    public void ToggleTemplate(Profile p)
    {
        bool make = !p.IsTemplate;
        foreach (var x in Profiles) x.IsTemplate = false;
        p.IsTemplate = make;
        SaveProfiles();
        Notify();
    }

    public void Move(Profile p, int delta)
    {
        int i = Profiles.IndexOf(p), j = i + delta;
        if (i < 0 || j < 0 || j >= Profiles.Count) return;
        (Profiles[i], Profiles[j]) = (Profiles[j], Profiles[i]);
        SaveProfiles();
        Notify();
    }

    public void Remove(Profile p)
    {
        Profiles.RemoveAll(x => x.Id == p.Id);
        SaveProfiles();
        Notify();
    }

    public void Launch(Profile p)
    {
        if (Running.TryGetValue(p.Id, out var pid))
        {
            var hwnd = Arranger.MainWindow(pid);
            if (hwnd != IntPtr.Zero) { Arranger.Activate(hwnd); return; }
        }
        if (!Claude.Launch(p, out var error)) { Alert("Gagal membuka Claude", error ?? ""); return; }
        ui.InvokeAsync(async () => { await Task.Delay(1500); Refresh(); });
    }

    public void OpenFolder(Profile p)
    {
        if (Directory.Exists(p.DataDir)) Process.Start(new ProcessStartInfo("explorer.exe", "\"" + p.DataDir + "\"") { UseShellExecute = true });
        else Alert("Folder belum ada", "Folder dibuat otomatis saat instance ini pertama kali dibuka.\n\n" + p.DataDir);
    }

    public void CopySetup(Profile from, Profile to, CopyOptions options)
    {
        if (IsRunning(to))
        {
            Alert("Tutup dulu", $"\"{to.Name}\" sedang berjalan. Tutup instance itu dulu, karena Claude menimpa setting-nya saat keluar.");
            return;
        }
        try { Alert("Setup disalin", SetupCopier.Copy(from, to, options)); }
        catch (Exception e) { Alert("Gagal menyalin", e.Message); }
    }

    // MARK: arrange

    public async Task OpenAndArrange()
    {
        if (ArrangeStatus != null) return;
        var targets = Profiles.Where(Layout.Includes).ToList();
        if (targets.Count == 0)
        {
            Alert("Belum ada instance", "Pilih instance yang mau dirapikan lewat tombol pengaturan tata letak.");
            return;
        }
        if (Claude.LaunchPath == null)
        {
            Alert("Claude Desktop tidak ditemukan", "Install Claude Desktop dulu dari https://claude.ai/download lalu coba lagi.");
            return;
        }
        void Status(string? s) { ArrangeStatus = s; Notify(); }
        Status("Membuka...");
        try
        {
            var procs = await Task.Run(Claude.RunningMains);
            bool launched = false;
            foreach (var t in targets)
            {
                if (procs.TryGetValue(Claude.Key(t), out var pid) && Arranger.MainWindow(pid) != IntPtr.Zero) continue;
                Claude.Launch(t, out _);
                launched = true;
            }

            var deadline = DateTime.Now.AddSeconds(30);
            var windows = new List<IntPtr>();
            while (true)
            {
                procs = await Task.Run(Claude.RunningMains);
                windows = targets.Select(t => procs.TryGetValue(Claude.Key(t), out var pid) ? Arranger.MainWindow(pid) : IntPtr.Zero).ToList();
                int found = windows.Count(w => w != IntPtr.Zero);
                Status($"Menunggu {found}/{targets.Count}");
                if (found == targets.Count || DateTime.Now > deadline) break;
                await Task.Delay(700);
            }

            var ready = windows.Where(w => w != IntPtr.Zero).ToList();
            var monitors = Arranger.Monitors();
            var monitor = monitors.FirstOrDefault(m => m.Id == Layout.Monitor) ?? monitors.FirstOrDefault();
            if (monitor == null) return;
            var zones = Templates.Resolve(Layout.Layout, ready.Count, Customs);
            Status("Merapikan...");
            // Claude restores its own saved window frame on startup; arrange after that settles.
            if (launched) await Task.Delay(1500);
            Arranger.Arrange(ready, monitor, zones, Layout.Gap);
            if (launched)
            {
                await Task.Delay(1500);
                Arranger.Arrange(ready, monitor, zones, Layout.Gap);
            }
            if (ready.Count < targets.Count)
                Alert("Sebagian belum muncul",
                    $"{ready.Count} dari {targets.Count} jendela dirapikan. Sisanya belum muncul dalam 30 detik; klik \"Buka & rapikan\" lagi setelah terbuka.");
        }
        finally
        {
            Status(null);
            Refresh();
        }
    }
}
