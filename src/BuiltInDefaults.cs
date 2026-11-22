namespace IDCXray;

/// <summary>
/// First-run placeholder profiles. These are documentation/example values only
/// (TEST-NET-3 addresses and dummy credentials) and will not connect to a real server.
/// Import or subscribe your own nodes after install.
/// </summary>
internal static class BuiltInDefaults
{
    public const string UsHost = "203.0.113.20";
    public const int UsPort = 38501;

    public static Node CreateOracleNode() => new()
    {
        Name = "osaka-amd",
        Protocol = "vless",
        Host = "203.0.113.10",
        Port = 443,
        Id = "00000000-0000-4000-8000-000000000001",
        Flow = "xtls-rprx-vision",
        Network = "tcp",
        Security = "reality",
        Sni = "www.microsoft.com",
        Fingerprint = "chrome",
        PublicKey = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
        ShortId = "",
        IsBuiltInOracle = true
    };

    public static Node CreateUsNode() => new()
    {
        Name = "us",
        Protocol = "vless",
        Host = UsHost,
        Port = UsPort,
        Id = "00000000-0000-4000-8000-000000000002",
        Flow = "xtls-rprx-vision",
        Network = "tcp",
        Security = "reality",
        Sni = "swdist.apple.com",
        Fingerprint = "chrome",
        PublicKey = "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB",
        ShortId = "",
        IsBuiltInOracle = false
    };

    public static bool IsUsNode(Node n) =>
        n is not null
        && string.Equals(n.Protocol, "vless", StringComparison.OrdinalIgnoreCase)
        && string.Equals(n.Host, UsHost, StringComparison.OrdinalIgnoreCase)
        && n.Port == UsPort;
}
