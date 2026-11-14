using System.Security.Cryptography;
using System.IO;
using System.Text;
using System.Text.Json;

namespace IDCXray;

public sealed class Store
{
    public static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IDCXray");
    private static readonly string FilePath = Path.Combine(Dir, "nodes.dpapi");

    public List<Node> Nodes { get; set; } = new();
    public List<RouteRule> Rules { get; set; } = new();
    public int Socks { get; set; } = 10808;
    public int Http { get; set; } = 10809;
    public bool SystemProxy { get; set; }
    public bool AutoConnect { get; set; }
    public bool StartMinimized { get; set; }
    public bool StartWithWindows { get; set; }
    public bool ChatAi { get; set; } = true;
    public string Mode { get; set; } = "Rule-based";
    public string Language { get; set; } = "中文";
    public bool PreviousProxyEnabled { get; set; }
    public string PreviousProxyServer { get; set; } = "";
    public bool ProxyWasCaptured { get; set; }
    public bool DefaultNodeInitialized { get; set; }

    public void Load()
    {
        Directory.CreateDirectory(Dir);
        if (!File.Exists(FilePath))
        {
            SeedDefaultIfNeeded();
            Save();
            return;
        }

        try
        {
            var encrypted = Convert.FromBase64String(File.ReadAllText(FilePath));
            var json = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            var loaded = JsonSerializer.Deserialize<Store>(json) ?? throw new InvalidDataException("Saved settings are empty.");
            Nodes = loaded.Nodes ?? new();
            Rules = loaded.Rules ?? new();
            Socks = loaded.Socks;
            Http = loaded.Http;
            SystemProxy = loaded.SystemProxy;
            AutoConnect = loaded.AutoConnect;
            StartMinimized = loaded.StartMinimized;
            StartWithWindows = loaded.StartWithWindows;
            ChatAi = loaded.ChatAi;
            Mode = loaded.Mode ?? "Rule-based";
            Language = loaded.Language ?? "中文";
            PreviousProxyEnabled = loaded.PreviousProxyEnabled;
            PreviousProxyServer = loaded.PreviousProxyServer ?? "";
            ProxyWasCaptured = loaded.ProxyWasCaptured;
            DefaultNodeInitialized = loaded.DefaultNodeInitialized || Nodes.Count > 0;
        }
        catch
        {
            Nodes.Clear();
            Rules.Clear();
            DefaultNodeInitialized = false;
        }

        if (SeedDefaultIfNeeded()) Save();
    }

    private bool SeedDefaultIfNeeded()
    {
        if (Nodes.Count != 0 || DefaultNodeInitialized) return false;
        Nodes.Add(BuiltInDefaults.CreateOracleNode());
        DefaultNodeInitialized = true;
        return true;
    }

    public void Save()
    {
        Directory.CreateDirectory(Dir);
        var raw = JsonSerializer.Serialize(this);
        var encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(raw), null, DataProtectionScope.CurrentUser);
        File.WriteAllText(FilePath, Convert.ToBase64String(encrypted));
    }
}
