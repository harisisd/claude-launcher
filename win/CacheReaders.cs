using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace ClaudeLauncher;

internal static class Blobs
{
    // Files of Claude's claude.ai IndexedDB blob store, newest first.
    public static List<(string path, DateTime date)> Files(string dir)
    {
        var list = new List<(string, DateTime)>();
        var blob = Path.Combine(dir, "IndexedDB", "https_claude.ai_0.indexeddb.blob");
        if (!Directory.Exists(blob)) return list;
        try
        {
            foreach (var f in new DirectoryInfo(blob).EnumerateFiles("*", SearchOption.AllDirectories))
            {
                try { if (f.Length < 50_000_000) list.Add((f.FullName, f.LastWriteTimeUtc)); } catch { }
            }
        }
        catch { }
        list.Sort((a, b) => b.Item2.CompareTo(a.Item2));
        return list;
    }

    // Read while Claude has the file open.
    public static byte[]? Read(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var data = new byte[fs.Length];
            int read = 0;
            while (read < data.Length)
            {
                int n = fs.Read(data, read, data.Length - read);
                if (n <= 0) break;
                read += n;
            }
            return data;
        }
        catch { return null; }
    }

    public static int IndexOf(byte[] hay, byte[] needle, int from, int to)
    {
        to = Math.Min(to, hay.Length);
        if (needle.Length == 0 || from < 0) return -1;
        for (int i = from; i <= to - needle.Length; i++)
        {
            int j = 0;
            while (j < needle.Length && hay[i + j] == needle[j]) j++;
            if (j == needle.Length) return i;
        }
        return -1;
    }
}

// MARK: - Signed-in account

// Reads the signed-in email from Claude's cached claude.ai account data. Only the
// account uuid (config.json) and the cached account record are read; never tokens.
public static class AccountInfo
{
    private static readonly Dictionary<string, (DateTime stamp, string? email)> Cache = new();
    private static readonly object Lock = new();
    private static readonly Regex UuidRegex = new("\"lastKnownAccountUuid\"\\s*:\\s*\"([0-9a-fA-F-]{36})\"", RegexOptions.Compiled);
    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    public static string? Email(string dir)
    {
        var config = Path.Combine(dir, "config.json");
        if (!File.Exists(config)) return null;
        var files = Blobs.Files(dir);
        if (files.Count == 0) return null;
        var configDate = File.GetLastWriteTimeUtc(config);
        var stamp = configDate > files[0].date ? configDate : files[0].date;

        lock (Lock) { if (Cache.TryGetValue(dir, out var hit) && hit.stamp == stamp) return hit.email; }

        string? result = null;
        try
        {
            var text = File.ReadAllText(config);
            var m = UuidRegex.Match(text);
            if (m.Success)
            {
                var uuid = Encoding.UTF8.GetBytes(m.Groups[1].Value);
                foreach (var f in files)
                {
                    var data = Blobs.Read(f.path);
                    if (data == null) continue;
                    result = Find(data, uuid);
                    if (result != null) break;
                }
            }
        }
        catch { }
        lock (Lock) Cache[dir] = (stamp, result);
        return result;
    }

    // The cache is V8-serialized: "email_address" is followed by '"', a varint length, then the bytes.
    private static string? Find(byte[] data, byte[] uuid)
    {
        var key = Encoding.ASCII.GetBytes("email_address");
        int from = 0;
        while (true)
        {
            int r = Blobs.IndexOf(data, uuid, from, data.Length);
            if (r < 0) return null;
            from = r + uuid.Length;
            int k = Blobs.IndexOf(data, key, from, Math.Min(data.Length, from + 400));
            if (k < 0) continue;
            int p = k + key.Length;
            if (p >= data.Length || data[p] != 0x22) continue;
            p++;
            int length = 0, shift = 0;
            while (p < data.Length && shift < 28)
            {
                int b = data[p++];
                length |= (b & 0x7F) << shift;
                if ((b & 0x80) == 0) break;
                shift += 7;
            }
            if (length <= 0 || length > 254 || p + length > data.Length) continue;
            var s = Encoding.Latin1.GetString(data, p, length);
            if (EmailRegex.IsMatch(s)) return s;
        }
    }
}

// MARK: - Usage limit snapshot

public sealed class UsageWindow
{
    public double? Utilization; // 0...1
    public DateTime? ResetsAt;  // UTC
    public bool HasReset => ResetsAt.HasValue && ResetsAt.Value <= DateTime.UtcNow;
}

public sealed class UsageSnapshot
{
    public UsageWindow? FiveHour;
    public UsageWindow? Weekly;
    public DateTime Taken; // UTC
}

// Last rate-limit snapshot Claude cached with a conversation. It is NOT live: Claude fetches
// current usage from the server and does not store it, so this can be days old.
public static class UsageInfo
{
    private static readonly Dictionary<string, (DateTime stamp, UsageSnapshot? usage)> Cache = new();
    private static readonly object Lock = new();
    private static readonly Regex IsoRegex = new(@"20\d\d-\d\d-\d\dT\d\d:\d\d:\d\d(\.\d+)?Z", RegexOptions.Compiled);

    public static UsageSnapshot? Latest(string dir)
    {
        var files = Blobs.Files(dir);
        var stamp = files.Count > 0 ? files[0].date : DateTime.MinValue;
        lock (Lock) { if (Cache.TryGetValue(dir, out var hit) && hit.stamp == stamp) return hit.usage; }

        UsageSnapshot? best = null;
        foreach (var f in files)
        {
            var data = Blobs.Read(f.path);
            if (data == null) continue;
            var raw = Unwrap(data);
            if (raw == null) continue;
            var found = Parse(raw);
            if (found != null && found.Taken > (best?.Taken ?? DateTime.MinValue)) best = found;
        }
        lock (Lock) Cache[dir] = (stamp, best);
        return best;
    }

    // Chromium IndexedDB values: 0xFF <version> then 0x02 = Snappy-compressed V8 data.
    private static byte[]? Unwrap(byte[] d)
    {
        if (d.Length <= 4 || d[0] != 0xFF) return null;
        return d[2] == 0x02 ? Snappy(d, 3) : d;
    }

    // Raw (unframed) Snappy block decoder.
    private static byte[]? Snappy(byte[] src, int start)
    {
        int pos = start, length = 0, shift = 0;
        while (pos < src.Length)
        {
            int b = src[pos++];
            length |= (b & 0x7F) << shift;
            if ((b & 0x80) == 0) break;
            shift += 7;
        }
        if (length < 0 || length > 200_000_000) return null;
        var dst = new byte[length];
        int d = 0;
        while (pos < src.Length && d < length)
        {
            int tag = src[pos++];
            if ((tag & 3) == 0)
            {
                int n = tag >> 2;
                if (n >= 60)
                {
                    int bytes = n - 59;
                    n = 0;
                    for (int i = 0; i < bytes; i++) { if (pos >= src.Length) return null; n |= src[pos++] << (8 * i); }
                }
                n += 1;
                if (n < 0 || pos + n > src.Length || d + n > length) return null;
                Buffer.BlockCopy(src, pos, dst, d, n);
                pos += n; d += n;
            }
            else
            {
                int n, offset;
                switch (tag & 3)
                {
                    case 1:
                        if (pos >= src.Length) return null;
                        n = ((tag >> 2) & 7) + 4; offset = ((tag >> 5) << 8) | src[pos]; pos += 1;
                        break;
                    case 2:
                        if (pos + 1 >= src.Length) return null;
                        n = (tag >> 2) + 1; offset = src[pos] | (src[pos + 1] << 8); pos += 2;
                        break;
                    default:
                        if (pos + 3 >= src.Length) return null;
                        n = (tag >> 2) + 1;
                        offset = src[pos] | src[pos + 1] << 8 | src[pos + 2] << 16 | src[pos + 3] << 24;
                        pos += 4;
                        break;
                }
                if (offset <= 0 || offset > d || d + n > length) return null;
                for (int i = 0; i < n; i++) { dst[d] = dst[d - offset]; d++; }
            }
        }
        return dst;
    }

    private static int Find(string needle, byte[] hay, int from, int to) => Blobs.IndexOf(hay, Encoding.ASCII.GetBytes(needle), from, to);

    // rate_limit_info.unifiedWindows = { five_hour: { resetsAt, status, utilization }, seven_day: { ... } }
    private static UsageSnapshot? Parse(byte[] raw)
    {
        UsageSnapshot? best = null;
        int from = 0;
        while (true)
        {
            int i = Find("unifiedWindows", raw, from, raw.Length);
            if (i < 0) break;
            from = i + 1;
            int end = Math.Min(raw.Length, i + 600);
            var five = Window(raw, i, end, "five_hour");
            var week = Window(raw, i, end, "seven_day");
            if (five == null && week == null) continue;
            var taken = Timestamp(raw, i);
            if (taken == null) continue;
            if (taken > (best?.Taken ?? DateTime.MinValue)) best = new UsageSnapshot { FiveHour = five, Weekly = week, Taken = taken.Value };
        }
        return best;
    }

    // V8 object: key string, then the value ('o' starts an object, '{' ends it).
    private static UsageWindow? Window(byte[] raw, int from, int end, string name)
    {
        int k = Find(name + "o", raw, from, end);
        if (k < 0) return null;
        int objEnd = Array.IndexOf(raw, (byte)'{', k, end - k);
        if (objEnd < 0) objEnd = end;
        var w = new UsageWindow { Utilization = Number(raw, k, objEnd, "utilization") };
        var r = Number(raw, k, objEnd, "resetsAt");
        if (r.HasValue)
        {
            var secs = r.Value > 1e12 ? r.Value / 1000 : r.Value;
            try { w.ResetsAt = DateTimeOffset.FromUnixTimeMilliseconds((long)(secs * 1000)).UtcDateTime; } catch { }
        }
        if (w.Utilization > 1) w.Utilization /= 100;
        return w.Utilization != null || w.ResetsAt != null ? w : null;
    }

    // 'N' = double (8 bytes LE), 'I' = zigzag varint int32.
    private static double? Number(byte[] raw, int from, int end, string key)
    {
        int k = Find(key, raw, from, end);
        if (k < 0) return null;
        int p = k + key.Length;
        if (p >= raw.Length) return null;
        if (raw[p] == (byte)'N' && p + 9 <= raw.Length) return BitConverter.ToDouble(raw, p + 1);
        if (raw[p] == (byte)'I')
        {
            long v = 0; int shift = 0;
            p++;
            while (p < raw.Length && shift < 35)
            {
                v |= (long)(raw[p] & 0x7F) << shift;
                if ((raw[p] & 0x80) == 0) break;
                p++; shift += 7;
            }
            return (v >> 1) ^ -(v & 1);
        }
        return null;
    }

    private static DateTime? Timestamp(byte[] raw, int at)
    {
        DateTime? Iso(int lo, int hi, bool last)
        {
            lo = Math.Max(0, lo); hi = Math.Min(raw.Length, hi);
            if (hi <= lo) return null;
            var chars = new char[hi - lo];
            for (int i = lo; i < hi; i++) chars[i - lo] = raw[i] < 0x80 ? (char)raw[i] : '.';
            var ms = IsoRegex.Matches(new string(chars));
            if (ms.Count == 0) return null;
            var text = (last ? ms[ms.Count - 1] : ms[0]).Value;
            return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt) ? dt : null;
        }
        return Iso(at, at + 900, false) ?? Iso(at - 600, at, true);
    }
}

// MARK: - Setup copy

public sealed class CopyOptions
{
    public bool Mcp = true, Prefs = true, ToolToggles = true, Presets = true, Extensions = true;
}

public static class SetupCopier
{
    const string DesktopConfig = "claude_desktop_config.json";
    static readonly string[] ExtensionDirs = { "Claude Extensions", "Claude Extensions Settings" };
    // Per-machine session/sidebar state that should not follow a profile.
    static readonly HashSet<string> SkipPrefKeys = new() { "epitaxyPrefs", "launchPreviewPersistedWorkspaces", "launchPreviewSessionScopedSessions" };

    // Login (config.json, cookies) is never copied.
    public static string Copy(Profile from, Profile to, CopyOptions o)
    {
        string src = from.DataDir, dst = to.DataDir;
        if (!Directory.Exists(src))
            throw new InvalidOperationException($"Folder data \"{from.Name}\" belum ada. Buka instance itu sekali dulu.");
        Directory.CreateDirectory(dst);
        var backup = Path.Combine(dst, "launcher-backup", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        var done = new List<string>();

        void BackupItem(string path)
        {
            if (!File.Exists(path) && !Directory.Exists(path)) return;
            Directory.CreateDirectory(backup);
            var target = Path.Combine(backup, Path.GetFileName(path));
            Delete(target);
            CopyAny(path, target);
        }

        bool Replace(string name)
        {
            string s = Path.Combine(src, name), d = Path.Combine(dst, name);
            if (!File.Exists(s) && !Directory.Exists(s)) return false;
            BackupItem(d);
            Delete(d);
            CopyAny(s, d);
            return true;
        }

        if (o.Mcp || o.Prefs)
        {
            var srcCfg = ReadJson(Path.Combine(src, DesktopConfig));
            var dstPath = Path.Combine(dst, DesktopConfig);
            var dstCfg = ReadJson(dstPath);
            if (o.Mcp)
            {
                var servers = srcCfg["mcpServers"];
                dstCfg["mcpServers"] = servers?.DeepClone();
                done.Add($"MCP server ({(servers as JsonObject)?.Count ?? 0})");
            }
            if (o.Prefs)
            {
                foreach (var (k, v) in srcCfg)
                    if (k != "mcpServers" && k != "preferences") dstCfg[k] = v?.DeepClone();
                var prefs = dstCfg["preferences"] as JsonObject ?? new JsonObject();
                if (srcCfg["preferences"] is JsonObject sp)
                    foreach (var (k, v) in sp)
                        if (!SkipPrefKeys.Contains(k)) prefs[k] = v?.DeepClone();
                dstCfg["preferences"] = prefs.Parent == null ? prefs : prefs.DeepClone();
                done.Add("Preferensi aplikasi");
            }
            BackupItem(dstPath);
            File.WriteAllText(dstPath, dstCfg.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
        if (o.ToolToggles && Replace("mcp-user-tool-toggles.json")) done.Add("Status tool MCP");
        if (o.Presets && Replace("ccd-session-presets.json")) done.Add("Preset sesi Claude Code");
        if (o.Extensions)
        {
            bool any = false;
            foreach (var dir in ExtensionDirs) if (Replace(dir)) any = true;
            if (any) done.Add("Extension");
        }

        if (done.Count == 0) return "Tidak ada yang disalin. Sumber belum punya setup yang dipilih.";
        var summary = "Disalin: " + string.Join(", ", done) + ".";
        if (Directory.Exists(backup)) summary += "\n\nFile lama dibackup ke:\n" + backup;
        return summary;
    }

    static JsonObject ReadJson(string path)
    {
        try
        {
            if (File.Exists(path) && JsonNode.Parse(File.ReadAllText(path)) is JsonObject o) return o;
        }
        catch { }
        return new JsonObject();
    }

    static void Delete(string path)
    {
        if (File.Exists(path)) File.Delete(path);
        else if (Directory.Exists(path)) Directory.Delete(path, true);
    }

    static void CopyAny(string src, string dst)
    {
        if (File.Exists(src)) { File.Copy(src, dst, true); return; }
        Directory.CreateDirectory(dst);
        foreach (var f in Directory.GetFiles(src)) File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), true);
        foreach (var d in Directory.GetDirectories(src)) CopyAny(d, Path.Combine(dst, Path.GetFileName(d)));
    }
}
