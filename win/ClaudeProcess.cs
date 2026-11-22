using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32;

namespace ClaudeLauncher;

// Finding, launching and closing Claude Desktop processes.
public static class Claude
{
    // Executable used to start Claude Desktop, or null when it is not installed.
    //  1. MSIX (Microsoft Store / current installer): the app execution alias claude-desktop.exe.
    //     Going through the alias keeps package identity and passes arguments through.
    //  2. Legacy Squirrel install: %LOCALAPPDATA%\AnthropicClaude\claude.exe
    public static string? LaunchPath
    {
        get
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var alias = Path.Combine(local, "Microsoft", "WindowsApps", "claude-desktop.exe");
            if (File.Exists(alias)) return alias;
            var squirrel = Path.Combine(local, "AnthropicClaude", "claude.exe");
            if (File.Exists(squirrel)) return squirrel;
            var pkg = PackageRoot();
            if (pkg != null)
            {
                var exe = Path.Combine(pkg, "app", "Claude.exe");
                if (File.Exists(exe)) return exe;
            }
            return null;
        }
    }

    // Install folder of the Claude MSIX package, from the per-user package repository.
    public static string? PackageRoot()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages");
            if (key == null) return null;
            foreach (var name in key.GetSubKeyNames().Where(n => n.StartsWith("Claude_", StringComparison.OrdinalIgnoreCase)).OrderByDescending(n => n))
            {
                using var sub = key.OpenSubKey(name);
                if (sub?.GetValue("PackageRootFolder") is string root && Directory.Exists(root)) return root;
            }
        }
        catch { }
        return null;
    }

    public static string Key(Profile p) => p.IsDefault ? Profile.DefaultId : Paths.Normalize(p.DataDir);

    // Claude Desktop main process: the Electron binary from the MSIX package or the Squirrel folder.
    // Claude Code's CLI is also called claude.exe, so the image path decides, not the name.
    private static bool IsDesktopImage(string path)
    {
        var p = path.ToLowerInvariant();
        return p.Contains(@"\windowsapps\claude_") || p.Contains(@"\anthropicclaude\");
    }

    public sealed record Proc(int Pid, string? DataDir);

    // Profile key -> pid of its main Claude process (Electron helpers carry --type=).
    public static Dictionary<string, int> RunningMains()
    {
        var map = new Dictionary<string, int>();
        foreach (var proc in Process.GetProcessesByName("claude"))
        {
            using (proc)
            {
                var info = Inspect(proc.Id);
                if (info == null) continue;
                var (image, cmd) = info.Value;
                if (!IsDesktopImage(image) || cmd.Contains(" --type=")) continue;
                var dir = UserDataDir(cmd);
                var key = dir == null ? Profile.DefaultId : Paths.Normalize(dir);
                // Keep the oldest pid when several match (a hand-off process may live for a moment).
                if (!map.ContainsKey(key)) map[key] = proc.Id;
            }
        }
        return map;
    }

    // All processes (main + helpers) of the instance that runs from this data dir.
    public static List<int> AllPids(Profile p)
    {
        var key = Key(p);
        var list = new List<int>();
        foreach (var proc in Process.GetProcessesByName("claude"))
        {
            using (proc)
            {
                var info = Inspect(proc.Id);
                if (info == null || !IsDesktopImage(info.Value.image)) continue;
                var dir = UserDataDir(info.Value.cmd);
                var k = dir == null ? Profile.DefaultId : Paths.Normalize(dir);
                if (k == key) list.Add(proc.Id);
            }
        }
        return list;
    }

    // Value of --user-data-dir=..., quoted or not. A literal " --" inside the path does not cut it short.
    public static string? UserDataDir(string cmd)
    {
        const string marker = "--user-data-dir=";
        int i = cmd.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (i < 0) return null;
        int s = i + marker.Length;
        if (s < cmd.Length && (cmd[s] == '"' || cmd[s] == '\''))
        {
            char q = cmd[s];
            int e = cmd.IndexOf(q, s + 1);
            return e < 0 ? cmd.Substring(s + 1) : cmd.Substring(s + 1, e - s - 1);
        }
        int end = cmd.IndexOf(' ', s);
        return end < 0 ? cmd.Substring(s) : cmd.Substring(s, end - s);
    }

    private static (string image, string cmd)? Inspect(int pid)
    {
        var h = Native.OpenProcess(Native.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (h == IntPtr.Zero) return null;
        try
        {
            var sb = new StringBuilder(1024);
            int size = sb.Capacity;
            if (!Native.QueryFullProcessImageName(h, 0, sb, ref size)) return null;
            var image = sb.ToString();
            return (image, CommandLine(h) ?? "");
        }
        finally { Native.CloseHandle(h); }
    }

    // ProcessCommandLineInformation returns a UNICODE_STRING followed by its buffer.
    private static string? CommandLine(IntPtr h)
    {
        int len = 4096;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            var buf = Marshal.AllocHGlobal(len);
            try
            {
                int status = Native.NtQueryInformationProcess(h, Native.ProcessCommandLineInformation, buf, len, out int ret);
                if (status == 0)
                {
                    ushort bytes = (ushort)Marshal.ReadInt16(buf);
                    var ptr = Marshal.ReadIntPtr(buf, IntPtr.Size); // Length(2) MaxLength(2) pad -> Buffer
                    return ptr == IntPtr.Zero ? "" : Marshal.PtrToStringUni(ptr, bytes / 2);
                }
                if (ret > len) { len = ret; continue; }
                return null;
            }
            finally { Marshal.FreeHGlobal(buf); }
        }
        return null;
    }

    public static bool Launch(Profile p, out string? error)
    {
        error = null;
        var exe = LaunchPath;
        if (exe == null)
        {
            error = "Claude Desktop tidak ditemukan. Install Claude Desktop dulu dari https://claude.ai/download lalu coba lagi.";
            return false;
        }
        try
        {
            var psi = new ProcessStartInfo(exe) { UseShellExecute = true };
            if (!p.IsDefault)
            {
                Directory.CreateDirectory(p.DataDir);
                // A second process with the same data dir hands over to the running one, which shows its window.
                psi.Arguments = "--user-data-dir=\"" + p.DataDir + "\"";
            }
            Process.Start(psi);
            return true;
        }
        catch (Exception e)
        {
            error = e.Message;
            return false;
        }
    }

    // Ask the main window to close, then end the whole instance (Claude may only hide to the tray).
    public static void Close(Profile p, int mainPid)
    {
        try
        {
            var hwnd = Arranger.MainWindow(mainPid);
            if (hwnd != IntPtr.Zero) Native.PostMessage(hwnd, Native.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        }
        catch { }
        Thread.Sleep(1500);
        foreach (var pid in AllPids(p))
        {
            try { using var proc = Process.GetProcessById(pid); proc.Kill(entireProcessTree: true); } catch { }
        }
    }
}
