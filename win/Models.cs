using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClaudeLauncher;

// MARK: - Paths

public static class Paths
{
    public static readonly string Home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    public static readonly string Root = Path.Combine(Home, ".claude-launcher");
    public static readonly string Profiles = Path.Combine(Root, "profiles");

    private static string? _defaultData;

    // Data folder of the stock Claude Desktop install (the "Utama" profile).
    //  - Current MSIX builds move it to %LOCALAPPDATA%\Claude-Data\claude_<id>
    //  - Older / Squirrel builds use %APPDATA%\Claude
    //  - MSIX without the move keeps it virtualised under Packages\Claude_*\LocalCache\Roaming\Claude
    public static string DefaultData
    {
        get
        {
            if (_defaultData != null && File.Exists(Path.Combine(_defaultData, "config.json"))) return _defaultData;
            _defaultData = FindDefaultData();
            return _defaultData;
        }
    }

    private static string FindDefaultData()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var candidates = new List<string>();
        try
        {
            var cd = Path.Combine(local, "Claude-Data");
            if (Directory.Exists(cd)) candidates.AddRange(Directory.GetDirectories(cd, "claude_*"));
        }
        catch { }
        candidates.Add(Path.Combine(roaming, "Claude"));
        try
        {
            var pk = Path.Combine(local, "Packages");
            if (Directory.Exists(pk))
                foreach (var d in Directory.GetDirectories(pk, "Claude_*"))
                    candidates.Add(Path.Combine(d, "LocalCache", "Roaming", "Claude"));
        }
        catch { }

        var withConfig = candidates
            .Where(c => File.Exists(Path.Combine(c, "config.json")))
            .OrderByDescending(c => File.GetLastWriteTimeUtc(Path.Combine(c, "config.json")))
            .FirstOrDefault();
        return withConfig ?? candidates.FirstOrDefault(Directory.Exists) ?? Path.Combine(roaming, "Claude");
    }

    public static string Normalize(string path)
    {
        try { path = Path.GetFullPath(path.Trim().Trim('"')); } catch { }
        return path.TrimEnd('\\', '/').ToLowerInvariant();
    }
}

// MARK: - Palette

public static class Palette
{
    public static readonly (string Name, string Hex)[] Colors =
    {
        ("Oranye", "#D97757"), ("Biru", "#5B9BD5"), ("Hijau", "#62B480"), ("Ungu", "#AA84D6"),
        ("Kuning", "#D6A848"), ("Merah muda", "#D67096"), ("Toska", "#48ACA6"), ("Abu-abu", "#80868E")
    };
}

// MARK: - Models

public sealed class Profile
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("color")] public string Color { get; set; } = "#D97757";
    [JsonPropertyName("isTemplate")] public bool IsTemplate { get; set; }

    public const string DefaultId = "default";
    [JsonIgnore] public bool IsDefault => Id == DefaultId;
    [JsonIgnore] public string DataDir => IsDefault ? Paths.DefaultData : Path.Combine(Paths.Profiles, Id);
    [JsonIgnore] public string Subtitle => IsDefault ? "Profil bawaan aplikasi" : "~\\.claude-launcher\\profiles\\" + Id;
}

// Rect relative to the monitor work area: origin top-left, 0...1.
public readonly record struct ZRect(double X, double Y, double W, double H)
{
    public double Right => X + W;
    public double Bottom => Y + H;
    public double MidX => X + W / 2;
    public double MidY => Y + H / 2;
}

// Stored like Swift's CGRect Codable: [[x, y], [w, h]] (same file format as the macOS build).
public sealed class ZRectConverter : JsonConverter<ZRect>
{
    public override ZRect Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var a = JsonSerializer.Deserialize<double[][]>(ref reader) ?? Array.Empty<double[]>();
        if (a.Length == 2 && a[0].Length == 2 && a[1].Length == 2) return new ZRect(a[0][0], a[0][1], a[1][0], a[1][1]);
        return new ZRect(0, 0, 1, 1);
    }

    public override void Write(Utf8JsonWriter writer, ZRect value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteStartArray(); writer.WriteNumberValue(value.X); writer.WriteNumberValue(value.Y); writer.WriteEndArray();
        writer.WriteStartArray(); writer.WriteNumberValue(value.W); writer.WriteNumberValue(value.H); writer.WriteEndArray();
        writer.WriteEndArray();
    }
}

public sealed class ZoneLayout
{
    [JsonPropertyName("id")] public string Id { get; set; } = Guid.NewGuid().ToString().ToUpperInvariant();
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("zones")] public List<ZRect> Zones { get; set; } = new();

    public ZoneLayout Clone() => new() { Id = Id, Name = Name, Zones = new List<ZRect>(Zones) };
}

public sealed class LayoutSettings
{
    [JsonPropertyName("monitor")] public string Monitor { get; set; } = "";
    [JsonPropertyName("layout")] public string Layout { get; set; } = "grid";
    [JsonPropertyName("gap")] public bool Gap { get; set; } = true;
    [JsonPropertyName("ids")] public List<string>? Ids { get; set; } // null = every profile

    public bool Includes(Profile p) => Ids == null || Ids.Contains(p.Id);
}

public sealed class AppSettings
{
    [JsonPropertyName("autoStart")] public bool AutoStart { get; set; }
    [JsonPropertyName("arrangeOnStart")] public bool ArrangeOnStart { get; set; }
    [JsonPropertyName("closeToTray")] public bool CloseToTray { get; set; } = true;
    [JsonPropertyName("hotkeys")] public bool Hotkeys { get; set; } = true;
}

public sealed class Preset
{
    [JsonPropertyName("id")] public string Id { get; set; } = Guid.NewGuid().ToString().ToUpperInvariant();
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("layout")] public string Layout { get; set; } = "grid";
    [JsonPropertyName("monitor")] public string Monitor { get; set; } = "";
    [JsonPropertyName("gap")] public bool Gap { get; set; } = true;
    [JsonPropertyName("ids")] public List<string> Ids { get; set; } = new();

    [JsonIgnore] public string Label => $"{Name}  ({Ids.Count} instance)";
}

public static class Json
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new ZRectConverter() },
    };

    public static T? Read<T>(string path) where T : class
    {
        try
        {
            if (!File.Exists(path)) return null;
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options);
        }
        catch { return null; }
    }

    public static void Write<T>(string path, T value)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(value, Options));
        }
        catch { }
    }
}

// MARK: - Layout templates

public static class Templates
{
    public static readonly string[] Keys = { "focus", "columns", "rows", "grid", "priority" };
    public static readonly string[] Names = { "Focus", "Kolom", "Baris", "Grid", "Priority Grid" };
    public const string CustomPrefix = "custom:";

    public static List<ZRect> Zones(string key, int count)
    {
        int n = Math.Max(1, count);
        double f = n;
        var z = new List<ZRect>();
        switch (key)
        {
            case "columns":
                for (int i = 0; i < n; i++) z.Add(new ZRect(i / f, 0, 1 / f, 1));
                return z;
            case "rows":
                for (int i = 0; i < n; i++) z.Add(new ZRect(0, i / f, 1, 1 / f));
                return z;
            case "focus":
            {
                double step = n == 1 ? 0 : Math.Min(0.05, 0.3 / (n - 1));
                for (int i = 0; i < n; i++) z.Add(new ZRect(0.15 + i * step, 0.1 + i * step, 0.6, 0.65));
                return z;
            }
            case "priority" when n >= 3:
            {
                z.Add(new ZRect(0.25, 0, 0.5, 1));
                int rest = n - 1, left = (rest + 1) / 2, right = rest - left;
                for (int i = 0; i < left; i++) z.Add(new ZRect(0, (double)i / left, 0.25, 1.0 / left));
                for (int i = 0; i < right; i++) z.Add(new ZRect(0.75, (double)i / right, 0.25, 1.0 / right));
                return z;
            }
            case "priority":
                return Zones("columns", n);
            default:
            {
                // grid: up to 3 side by side, then a square-ish grid whose last row stretches
                int cols = n <= 3 ? n : (int)Math.Ceiling(Math.Sqrt(n));
                int rows = (int)Math.Ceiling((double)n / cols);
                for (int i = 0; i < n; i++)
                {
                    int row = i / cols, col = i % cols;
                    int inRow = row == rows - 1 ? n - row * cols : cols;
                    z.Add(new ZRect((double)col / inRow, (double)row / rows, 1.0 / inRow, 1.0 / rows));
                }
                return z;
            }
        }
    }

    public static List<ZRect> Resolve(string key, int count, IList<ZoneLayout> customs)
    {
        if (key.StartsWith(CustomPrefix))
        {
            var name = key.Substring(CustomPrefix.Length);
            var c = customs.FirstOrDefault(x => x.Name == name);
            return c != null ? new List<ZRect>(c.Zones) : Zones("grid", count);
        }
        return Zones(Keys.Contains(key) ? key : "grid", count);
    }
}
