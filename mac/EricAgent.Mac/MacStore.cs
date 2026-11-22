using System.Text.Json;

namespace IDCXray;

public sealed class MacStore
{
    public static string DirectoryPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "Eric agent");
    public static string FilePath => Path.Combine(DirectoryPath, "nodes.json");
    public static UnixFileMode DirectoryPermissions => UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    public static UnixFileMode FilePermissions => UnixFileMode.UserRead | UnixFileMode.UserWrite;
    public List<Node> Nodes { get; set; } = new();
    public List<RouteRule> Rules { get; set; } = new();
    public List<Subscription> Subscriptions { get; set; } = new();
    public int Socks { get; set; } = 10808;
    public int Http { get; set; } = 10809;
    public bool SystemProxy { get; set; }
    public bool DefaultNodeInitialized { get; set; }
    public bool UsNodeInitialized { get; set; }

    public void Load()
    {
        SecureDirectory();
        if (File.Exists(FilePath))
        {
            try
            {
                var loaded = JsonSerializer.Deserialize<MacStore>(File.ReadAllText(FilePath));
                if (loaded is not null)
                {
                    Nodes = loaded.Nodes ?? new(); Rules = loaded.Rules ?? new(); Subscriptions = loaded.Subscriptions ?? new();
                    Socks = loaded.Socks; Http = loaded.Http; SystemProxy = loaded.SystemProxy;
                    DefaultNodeInitialized = loaded.DefaultNodeInitialized || Nodes.Count > 0;
                    UsNodeInitialized = loaded.UsNodeInitialized;
                }
            }
            catch { Nodes.Clear(); Rules.Clear(); Subscriptions.Clear(); DefaultNodeInitialized = false; UsNodeInitialized = false; }
        }
        if (SeedDefaultIfNeeded() || !File.Exists(FilePath)) Save();
    }

    private bool SeedDefaultIfNeeded()
    {
        var changed = false;
        if (Nodes.Count == 0 && !DefaultNodeInitialized) { Nodes.Add(BuiltInDefaults.CreateOracleNode()); DefaultNodeInitialized = true; changed = true; }
        if (!UsNodeInitialized) { if (!Nodes.Any(BuiltInDefaults.IsUsNode)) Nodes.Add(BuiltInDefaults.CreateUsNode()); UsNodeInitialized = true; changed = true; }
        return changed;
    }

    public void Save()
    {
        SecureDirectory();
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        SetMode(temp, FilePermissions);
        File.Move(temp, FilePath, true);
        SetMode(FilePath, FilePermissions);
    }

    private static void SecureDirectory()
    {
        Directory.CreateDirectory(DirectoryPath);
        SetMode(DirectoryPath, DirectoryPermissions);
    }

    private static void SetMode(string path, UnixFileMode mode)
    {
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, mode);
    }
}
